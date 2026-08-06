using System;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.SteamGridDB;
using SteamGridDB.Xbox.Services.SteamGridDB.Models;
using SteamGridDB.Xbox.Services.Stores;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Resolves one manifest entry's SteamGridDB match and display name - the network-dependent half of
    /// LoadGameEntriesAsync's per-entry parsing (see PrimaryWidget.xaml.cs) that runs after
    /// <see cref="ManifestEntryIdentity"/> has already derived the entry's default name and platform ID.
    /// First tries an exact SteamGridDB platform-ID match; if that fails, asks the entry's own store for
    /// its display name (GOG/Epic/Ubisoft/EA) and falls back to a SteamGridDB name search. Every call
    /// here happens in exactly the order, count and condition it always did inline - this is a
    /// relocation, not a behavior change, per the standing constraint on per-game network-call
    /// ordering/concurrency. EA's lookup, added later, is local file reads only and makes no request.
    ///
    /// <see cref="SelectStoreNameLookupTarget"/> and <see cref="BuildUnmatchedLogLine"/> are pure and
    /// tested directly. <see cref="ResolveAsync"/> itself is not: it is real network I/O, the same carve-out
    /// TESTING.md already documents for StoreNameLookup's own fetch methods (only their pure parts are
    /// covered - a test exercising the network would be grading SteamGridDB/GOG/Epic/Ubisoft's uptime).
    /// </summary>
    internal static class GameMatchResolver
    {
        /// <summary>
        /// A resolved entry's final display name and SteamGridDB match state, ready for
        /// <c>GameEntry</c>'s constructor.
        /// </summary>
        internal readonly struct Result
        {
            internal Result(string gameName, bool hasSteamGridDbMatch, string officialCapsuleUrl, int steamGridDbGameId)
            {
                GameName = gameName;
                HasSteamGridDbMatch = hasSteamGridDbMatch;
                OfficialCapsuleUrl = officialCapsuleUrl;
                SteamGridDbGameId = steamGridDbGameId;
            }

            internal string GameName { get; }

            internal bool HasSteamGridDbMatch { get; }

            internal string OfficialCapsuleUrl { get; }

            internal int SteamGridDbGameId { get; }
        }

        /// <summary>
        /// Which store's own name-lookup method, if any, applies to an unmatched entry on this platform.
        /// A pure mapping split out from the awaited calls themselves (which stay in
        /// <see cref="ResolveAsync"/>, in the exact order they always ran) so the platform-to-store
        /// decision is testable without a network.
        /// </summary>
        internal enum StoreNameLookupTarget
        {
            None,
            Gog,
            Epic,
            Ubisoft,
            Ea
        }

        internal static StoreNameLookupTarget SelectStoreNameLookupTarget(GamePlatform platform)
        {
            switch (platform)
            {
                case GamePlatform.GOG:
                    return StoreNameLookupTarget.Gog;
                case GamePlatform.Epic:
                    return StoreNameLookupTarget.Epic;
                case GamePlatform.Ubisoft:
                    return StoreNameLookupTarget.Ubisoft;
                case GamePlatform.EA:
                    return StoreNameLookupTarget.Ea;
                default:
                    return StoreNameLookupTarget.None;
            }
        }

        /// <summary>
        /// Formats FixLog's per-entry "unmatched" audit line - the exact text an operator reads to see
        /// why a game still shows as "Unknown" after a load, or which SteamGridDB ID a name search
        /// landed on. Epic and EA entries additionally carry the summary of their store's own local
        /// manifest read, since a failed name resolution against a launcher's on-disk manifests is
        /// otherwise indistinguishable from that launcher not being installed at all. Epic carries its
        /// catalog item ID too, being the one platform whose entry ID holds two identifiers.
        /// </summary>
        internal static string BuildUnmatchedLogLine(GamePlatform platform, string externalPlatformId, string epicCatalogItemId, string storeLoadSummary, string gameName, int steamGridDbGameId)
        {
            string storeSegment;

            switch (platform)
            {
                case GamePlatform.Epic:
                    storeSegment = $" catalog={epicCatalogItemId ?? "none"} epic=[{storeLoadSummary}]";
                    break;
                case GamePlatform.EA:
                    storeSegment = $" ea=[{storeLoadSummary}]";
                    break;
                default:
                    storeSegment = string.Empty;
                    break;
            }

            return $"unmatched {platform}/{externalPlatformId}"
                + storeSegment
                + $" name={gameName} sgdbId={steamGridDbGameId}";
        }

        /// <param name="sgdbClient">Null when no API key is configured; guarded by <paramref name="canQuerySteamGridDb"/> before every use.</param>
        /// <param name="canQuerySteamGridDb">Whether a SteamGridDB API key is configured at all.</param>
        /// <param name="platform">The entry's platform.</param>
        /// <param name="externalPlatformId">The entry's own store ID, as derived by <see cref="ManifestEntryIdentity"/>.</param>
        /// <param name="epicCatalogItemId">Epic's separate catalog item ID, or null for every other platform.</param>
        /// <param name="entryId">The manifest entry's raw "id" field, used only for the SteamGridDB-lookup-failed log line.</param>
        /// <param name="gameName">The entry's default display name so far (from <see cref="ManifestEntryIdentity"/>), overwritten only if a match is found.</param>
        /// <param name="unknownName">The sentinel default name; a SteamGridDB name search is skipped when the name is still this.</param>
        internal static async Task<Result> ResolveAsync(
            SteamGridDbClient sgdbClient,
            bool canQuerySteamGridDb,
            GamePlatform platform,
            string externalPlatformId,
            string epicCatalogItemId,
            string entryId,
            string gameName,
            string unknownName)
        {
            bool hasSteamGridDbMatch = false;
            string officialCapsuleUrl = null;
            int steamGridDbGameId = 0;

            // Try to fetch game name from SteamGridDB API
            try
            {
                string platformString = GamePlatformHelper.GamePlatformToSGDBApiString(platform);

                if (canQuerySteamGridDb && !string.IsNullOrEmpty(platformString))
                {
                    SteamGridDbGame gameInfo = await sgdbClient.GetGameByPlatformIdAsync(platformString, externalPlatformId);

                    if (gameInfo != null && !string.IsNullOrEmpty(gameInfo.Name))
                    {
                        gameName = gameInfo.Name;
                        hasSteamGridDbMatch = true;

                        // Comes back on this same lookup; see the official-artwork gate
                        officialCapsuleUrl = gameInfo.OfficialCapsuleUrl;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - game name is optional, default is "Unknown"
                System.Diagnostics.Debug.WriteLine($"Could not fetch game name for {entryId} from SteamGridDB: {ex.Message}");
            }

            if (!hasSteamGridDbMatch)
            {
                // Set by the stores that read their own local manifests, so the audit line below can
                // say whether that read found anything - each store reports its own, at the point it
                // is consulted, rather than the log line reaching into all of them
                string storeLoadSummary = null;

                switch (SelectStoreNameLookupTarget(platform))
                {
                    case StoreNameLookupTarget.Gog:
                        string gogName = await StoreNameLookup.GetOrFetchGogNameAsync(externalPlatformId);

                        if (!string.IsNullOrEmpty(gogName))
                        {
                            gameName = gogName;
                        }

                        break;

                    case StoreNameLookupTarget.Epic:
                        string epicName = await StoreNameLookup.GetOrFetchEpicNameAsync(externalPlatformId, epicCatalogItemId);

                        if (!string.IsNullOrEmpty(epicName))
                        {
                            gameName = epicName;
                        }

                        storeLoadSummary = EpicLibrary.LoadSummary;

                        break;

                    case StoreNameLookupTarget.Ubisoft:
                        string ubisoftName = await StoreNameLookup.GetUbisoftGameNameAsync(externalPlatformId);

                        if (!string.IsNullOrEmpty(ubisoftName))
                        {
                            gameName = ubisoftName;
                        }

                        break;

                    case StoreNameLookupTarget.Ea:
                        // Local file reads only; EA's public catalogue API is gone. See EaLibrary.
                        string eaName = await EaLibrary.GetDisplayNameAsync(externalPlatformId);

                        if (!string.IsNullOrEmpty(eaName))
                        {
                            gameName = eaName;
                        }

                        storeLoadSummary = EaLibrary.LoadSummary;

                        break;
                }

                // A name is enough to find the game even when no store ID matches - SteamGridDB has
                // entries linked to no store at all. Custom entries are included deliberately, despite
                // being the one kind that made no store request before: someone adding a shortcut by
                // hand wants artwork for it more than most, not less. The result cache keeps the cost to
                // once per name.
                if (canQuerySteamGridDb && gameName != unknownName)
                {
                    steamGridDbGameId = await StoreNameLookup.FindGameByNameAsync(sgdbClient, gameName);
                    hasSteamGridDbMatch = steamGridDbGameId > 0;
                }

                FixLog.Write(BuildUnmatchedLogLine(platform, externalPlatformId, epicCatalogItemId, storeLoadSummary, gameName, steamGridDbGameId));
            }

            return new Result(gameName, hasSteamGridDbMatch, officialCapsuleUrl, steamGridDbGameId);
        }
    }
}
