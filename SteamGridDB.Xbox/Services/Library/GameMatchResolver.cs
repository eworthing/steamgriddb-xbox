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
    /// its display name (GOG/Epic/Ubisoft/EA) and falls back to a SteamGridDB name search. The order,
    /// count and condition of those calls is exactly what it always was inline; the one thing that has
    /// changed since is that the whole sequence is now skipped outright when
    /// <see cref="GameMatchCache"/> already holds a fresh answer for this game, which is the single
    /// largest reduction in the app's outbound traffic available - see that type for why. EA's lookup
    /// is local file reads only and makes no request.
    ///
    /// The cache is neither read nor written when there is no API key. Without one the SteamGridDB
    /// half of this never runs, so the result would be a miss recorded for a question that was never
    /// asked - and adding a key later would then find that miss waiting for it.
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

        /// <summary>
        /// Whether a resolve's outcome is worth writing to <see cref="GameMatchCache"/>.
        ///
        /// Three ways an outcome can be a non-answer, all of which look identical to a genuine "this
        /// game is not on SteamGridDB" from the outside, and all of which would be remembered as one:
        /// there was no API key so nothing was asked; the request threw, which is a timeout or a dead
        /// network; or something in it went unanswered - refused, a server error, a dead connection -
        /// which <see cref="SteamGridDbClient.UnansweredResponses"/> is the only evidence of, because
        /// every one of those reaches the caller as the same null a real miss does.
        ///
        /// Only a 404 counts as an answer, and that is the one that matters: it is what SteamGridDB
        /// returns for a game it does not carry, which is the fact worth keeping for days.
        ///
        /// Pure, and split out for that reason - the resolve around it is network I/O that TESTING.md
        /// carves out, and this is the part of it whose logic can actually be wrong.
        /// </summary>
        /// <param name="canQuerySteamGridDb">Whether a SteamGridDB API key is configured at all.</param>
        /// <param name="lookupThrew">Whether the platform-ID lookup threw.</param>
        /// <param name="unansweredBefore">The client's unanswered-request count before the resolve.</param>
        /// <param name="unansweredAfter">The client's unanswered-request count after it.</param>
        internal static bool ShouldRemember(bool canQuerySteamGridDb, bool lookupThrew, int unansweredBefore, int unansweredAfter)
        {
            return canQuerySteamGridDb && !lookupThrew && unansweredAfter == unansweredBefore;
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

            DateTimeOffset now = DateTimeOffset.UtcNow;

            if (canQuerySteamGridDb)
            {
                GameMatchCache.Entry? remembered = await GameMatchCache.GetAsync(platform, externalPlatformId, now);

                if (remembered.HasValue)
                {
                    GameMatchCache.Entry cached = remembered.Value;

                    // Logged like a fresh resolve would be, and marked as cached: an audit line that
                    // silently went missing for most of the library after the first load would make
                    // the log useless for the thing it exists to answer, which is why a given game is
                    // still showing as Unknown
                    FixLog.Write($"cached {platform}/{externalPlatformId} name={cached.Name ?? unknownName} sgdbId={cached.SteamGridDbGameId} matched={cached.Matched}");

                    return new Result(cached.Name ?? gameName, cached.Matched, cached.CapsuleUrl, cached.SteamGridDbGameId);
                }
            }

            // Whether anything in this resolve went unanswered rather than answered. A refused or
            // failed lookup returns the same null a genuine miss does, and writing that into the cache
            // would turn a bad minute into days of the game showing as Unknown
            int unansweredBefore = canQuerySteamGridDb ? sgdbClient.UnansweredResponses : 0;
            bool lookupThrew = false;

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
                lookupThrew = true;

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

            if (ShouldRemember(canQuerySteamGridDb, lookupThrew, unansweredBefore, canQuerySteamGridDb ? sgdbClient.UnansweredResponses : 0))
            {
                await GameMatchCache.SetAsync(
                    platform,
                    externalPlatformId,
                    new GameMatchCache.Entry(gameName, hasSteamGridDbMatch, officialCapsuleUrl, steamGridDbGameId, now));
            }

            return new Result(gameName, hasSteamGridDbMatch, officialCapsuleUrl, steamGridDbGameId);
        }
    }
}
