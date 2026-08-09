using System;

using Windows.Data.Xml.Dom;

namespace SteamGridDB.Xbox.Services.Stores
{
    /// <summary>
    /// Reads the AppxManifest.xml every installed package carries, for the one thing it can say that
    /// <see cref="XboxGameConfig"/> cannot: that a package with no MicrosoftGame.config beside it is a
    /// game anyway.
    ///
    /// The Store's older titles predate that file - Microsoft Mahjong, Microsoft Solitaire Collection
    /// and the Halo Spartan games all ship as plain UWP packages with nothing but a manifest - so both
    /// halves of <see cref="XboxInstalledGames"/> walk straight past them and they never reach the
    /// library at all. What they do carry is an xboxliveapp-&lt;title id&gt; protocol handler, which is
    /// how the shell hands a launch to an Xbox Live title and which an ordinary app has no reason to
    /// declare: on a machine with a hundred-odd packages installed it picks out the games and nothing
    /// else.
    ///
    /// It says nothing about <em>which</em> Store product the package is, though - the manifest names
    /// the package, not the product. That still has to be asked for by package family name, which is
    /// why this is only a filter and <see cref="StoreCatalog.GetByPackageFamilyNamesAsync"/> does the
    /// rest.
    /// </summary>
    internal static class PackageManifest
    {
        /// <summary>The manifest every installed package keeps at the root of its install folder.</summary>
        internal const string FileName = "AppxManifest.xml";

        /// <summary>The protocol an Xbox Live title registers, followed by its title ID in decimal.</summary>
        private const string xboxLiveProtocolPrefix = "xboxliveapp-";

        /// <summary>
        /// Whether a package manifest declares its app an Xbox Live title.
        /// </summary>
        /// <param name="xml">AppxManifest.xml's full text.</param>
        internal static bool DeclaresXboxLiveGame(string xml)
        {
            // Rejected on a substring before anything is parsed. This is asked of every installed
            // package that has no MicrosoftGame.config - a hundred of them on an ordinary machine, and
            // three answers of yes among them - so building a DOM per manifest would be almost all of
            // this sweep's cost for none of its results.
            if (string.IsNullOrEmpty(xml)
                || xml.IndexOf(xboxLiveProtocolPrefix, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            XmlDocument document = new XmlDocument();

            try
            {
                document.LoadXml(xml);
            }
            catch (Exception ex)
            {
                // One manifest this app cannot read is one game missing from the list, not a failed load
                System.Diagnostics.Debug.WriteLine($"Could not parse an {FileName}: {ex.Message}");

                return false;
            }

            // Matched on local name because Protocol lives in the uap namespace and the prefix bound to
            // it is the manifest's own choice - "uap" by convention, but nothing enforces that
            foreach (IXmlNode node in document.SelectNodes("//*[local-name()='Protocol']"))
            {
                string name = node.Attributes?.GetNamedItem("Name")?.NodeValue?.ToString();

                if (name != null && name.StartsWith(xboxLiveProtocolPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // The substring is in the file but not as a protocol name - a description, a content URI
            // rule, an unrelated attribute. Rare enough to be worth one parse to be sure of.
            return false;
        }
    }
}
