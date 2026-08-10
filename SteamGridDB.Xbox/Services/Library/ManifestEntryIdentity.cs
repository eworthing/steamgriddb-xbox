using System.IO;

using Windows.Data.Json;

using SteamGridDB.Xbox.Models;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Derives a manifest entry's store identifier(s) and default display name from its platform and
    /// JSON fields.
    ///
    /// This is the one slice of LoadGameEntriesAsync's per-entry parsing with no dependency on image
    /// decode, backup state, or network results - everything it needs (the entry's JSON object, its
    /// platform, and its raw "id" field) is already known before any of those run. Split out so the
    /// platform-specific derivation (especially Epic's, which re-parses a colon-delimited id into two
    /// different identifiers) can be tested directly instead of only by inspection several hundred
    /// lines into a UI-bound method.
    /// </summary>
    internal static class ManifestEntryIdentity
    {
        internal readonly struct Result
        {
            internal string GameName { get; }

            internal string ExternalPlatformId { get; }

            internal string EpicCatalogItemId { get; }

            internal Result(string gameName, string externalPlatformId, string epicCatalogItemId)
            {
                GameName = gameName;
                ExternalPlatformId = externalPlatformId;
                EpicCatalogItemId = epicCatalogItemId;
            }
        }

        /// <summary>
        /// Derives the store ID(s) and default display name for one manifest entry.
        /// </summary>
        /// <param name="entryObject">The manifest entry's JSON object.</param>
        /// <param name="platform">The entry's platform.</param>
        /// <param name="entryId">
        /// The entry's raw "id" field. Every platform except Custom derives its store ID by stripping
        /// the platform prefix from this (Epic re-parses it further, into two different identifiers);
        /// Custom does not use it at all.
        /// </param>
        /// <param name="unknownGameNameDefault">
        /// The fallback name to return when nothing resolves one - passed in rather than hardcoded so
        /// this stays free of the caller's own fields/constants.
        /// </param>
        /// <returns>The derived game name, store ID, and (Epic only) catalog item ID.</returns>
        internal static Result Derive(JsonObject entryObject, GamePlatform platform, string entryId, string unknownGameNameDefault)
        {
            string gameName = unknownGameNameDefault;
            string externalPlatformId;
            string epicCatalogItemId = null;

            if (GamePlatformHelper.CarriesOwnPaths(platform))
            {
                // gameName keeps its default when title is missing or JSON null, same as every other
                // platform's fallback
                gameName = JsonRead.String(entryObject, "title") ?? gameName;
                externalPlatformId = Path.Combine(
                    JsonRead.String(entryObject, "installLocation") ?? string.Empty,
                    JsonRead.String(entryObject, "executableName") ?? string.Empty);
            }
            else
            {
                externalPlatformId = entryId.Substring(entryId.IndexOf(':') + 1);

                if (platform == GamePlatform.Epic)
                {
                    // Xbox stores Epic entries as "epic:<namespace>:<catalogItemId>:<appName>".
                    // SteamGridDB's egs identifier is the appName - the last segment (for example
                    // "Sugar" for Rocket League), not the catalog item ID.
                    string[] parts = entryId.Split(':');

                    if (parts.Length >= 3)
                    {
                        externalPlatformId = parts[parts.Length - 1];
                        epicCatalogItemId = parts.Length >= 4 ? parts[2] : null;
                    }
                }
            }

            return new Result(gameName, externalPlatformId, epicCatalogItemId);
        }
    }
}
