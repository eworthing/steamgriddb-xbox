using System;

using Windows.Data.Xml.Dom;

namespace SteamGridDB.Xbox.Services.Stores
{
    /// <summary>
    /// Reads the MicrosoftGame.config every game the Xbox app installs writes beside its executable.
    ///
    /// This is the cheap half of finding first-party games. A game installed as MSIXVC lands in
    /// &lt;drive&gt;:\XboxGames\&lt;title&gt;\Content\, and its config carries the Store ID outright -
    /// no package query, no catalogue round trip, nothing but a file read. The other half
    /// (<see cref="XboxInstalledGames"/>) covers the games that install as plain MSIX packages and so
    /// have no XboxGames folder at all.
    ///
    /// The folder also fills up with content packs - "MWII PC MS DLC03 Cross-Gen Pack 02" and a dozen
    /// more like it - which carry a Store ID exactly as a game does and would otherwise appear in the
    /// library as their own rows. They are told apart by <see cref="TitleId"/>: every real game has
    /// one, every content pack leaves it empty. That is a hint rather than the decision, because the
    /// catalogue's own product kind is authoritative and is checked later; this just avoids asking
    /// about a dozen products that are certain not to be games.
    /// </summary>
    internal static class XboxGameConfig
    {
        /// <summary>The file every installed Xbox game keeps beside its executable.</summary>
        internal const string FileName = "MicrosoftGame.config";

        /// <summary>
        /// One MicrosoftGame.config, reduced to the three things this needs from it.
        /// </summary>
        internal readonly struct Result
        {
            internal Result(string storeId, string displayName, string titleId)
            {
                StoreId = storeId;
                DisplayName = displayName;
                TitleId = titleId;
            }

            /// <summary>The Microsoft Store product ID, or null when the config carries none.</summary>
            internal string StoreId { get; }

            /// <summary>The game's shell display name, or null when the config carries none.</summary>
            internal string DisplayName { get; }

            /// <summary>The Xbox Live title ID. Empty for content packs, set for real games.</summary>
            internal string TitleId { get; }

            /// <summary>
            /// Whether this config is worth asking the Store catalogue about: it has to name a product,
            /// and content packs are skipped rather than looked up only to be discarded.
            /// </summary>
            internal bool LooksLikeGame => !string.IsNullOrEmpty(StoreId) && !string.IsNullOrEmpty(TitleId);
        }

        /// <summary>
        /// Pulls the Store ID, display name and title ID out of one MicrosoftGame.config.
        /// </summary>
        /// <param name="xml">MicrosoftGame.config's full text.</param>
        /// <returns>What the config claims; all null when the XML will not parse.</returns>
        internal static Result Parse(string xml)
        {
            if (string.IsNullOrEmpty(xml))
            {
                return new Result(null, null, null);
            }

            XmlDocument document = new XmlDocument();

            try
            {
                document.LoadXml(xml);
            }
            catch (Exception ex)
            {
                // A config this app cannot read is one game missing from the list, not a failed load
                System.Diagnostics.Debug.WriteLine($"Could not parse a MicrosoftGame.config: {ex.Message}");

                return new Result(null, null, null);
            }

            return new Result(
                TextOf(document, "/Game/StoreId"),
                AttributeOf(document, "/Game/ShellVisuals", "DefaultDisplayName"),
                TextOf(document, "/Game/TitleId"));
        }

        /// <summary>The trimmed text of the first node matching <paramref name="xpath"/>, or null.</summary>
        private static string TextOf(XmlDocument document, string xpath)
        {
            string text = document.SelectSingleNode(xpath)?.InnerText?.Trim();

            return string.IsNullOrEmpty(text) ? null : text;
        }

        /// <summary>The trimmed value of an attribute on the first node matching <paramref name="xpath"/>, or null.</summary>
        private static string AttributeOf(XmlDocument document, string xpath, string attribute)
        {
            string value = document.SelectSingleNode(xpath)?.Attributes?.GetNamedItem(attribute)?.NodeValue?.ToString()?.Trim();

            return string.IsNullOrEmpty(value) ? null : value;
        }
    }
}
