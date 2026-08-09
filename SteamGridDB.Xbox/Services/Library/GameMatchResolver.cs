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
    /// One cached answer is not whole, and does not skip the sequence: a miss carrying no name, on a
    /// platform whose name comes out of installed files (EA and Epic). Its SteamGridDB verdict is
    /// honoured and its platform-ID lookup is not repeated, but the store's own name lookup runs again,
    /// because installing the game changes that name without SteamGridDB changing at all - see
    /// <see cref="AnswersTheName"/>. Such an entry produces a full "unmatched" audit line rather than a
    /// "cached" one, which is the intended reading: work was done, and the line carries the store load
    /// summary that says whether the manifests were read.
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

        /// <summary>
        /// Whether a cached entry answers everything the resolve needs of it, or only half.
        ///
        /// The cache remembers what SteamGridDB said, and a miss it recorded stays true for days. The
        /// name sitting beside that miss is a different kind of fact: on the store-backed platforms it
        /// comes from the launcher's own on-disk manifests, so it turns from "Unknown" into a real
        /// title the moment the game is installed - an event SteamGridDB knows nothing about, and one
        /// the miss's own lifetime therefore has no bearing on. Remembering "Unknown" for two days
        /// hides exactly the event the user is waiting on: install a game, reopen the widget, and it is
        /// still nameless, with nothing on screen to say why.
        ///
        /// So a nameless miss is treated as half an answer, on the platforms where that reasoning holds.
        /// The SteamGridDB verdict in it is still honoured - the platform-ID lookup it already paid for
        /// is not asked again - and only the store's own name lookup is reopened.
        ///
        /// It holds on exactly the stores whose name comes out of installed files, which is what
        /// <see cref="ResolvesNameFromInstalledFiles"/> names. Everywhere else the entry is left alone,
        /// and not only because there is no install event to react to: GOG's name comes from its API
        /// and Ubisoft's from a list on GitHub, and neither answer is one an install can change. Worse,
        /// <see cref="StoreNameLookup"/> deliberately caches only what a store actually answered, so a
        /// store that is down or rate-limiting caches nothing - reopening those entries would re-ask a
        /// failing API once per nameless game on every single widget open, for as long as it stayed
        /// down, which is precisely the traffic the cache exists to prevent. The 2-day miss lifetime
        /// already covers a store having had a bad afternoon.
        ///
        /// A miss that carries a real name is a whole answer on every platform and returns straight
        /// from the cache: the name search was performed, and it found nothing.
        /// </summary>
        /// <param name="platform">The entry's platform, which decides where its name would come from.</param>
        /// <param name="matched">Whether the cached entry says SteamGridDB knows the game.</param>
        /// <param name="cachedName">The name on the cached entry, if it carries one.</param>
        /// <param name="unknownName">The sentinel default name.</param>
        internal static bool AnswersTheName(GamePlatform platform, bool matched, string cachedName, string unknownName)
        {
            if (matched || (!string.IsNullOrEmpty(cachedName) && cachedName != unknownName))
            {
                return true;
            }

            return !ResolvesNameFromInstalledFiles(SelectStoreNameLookupTarget(platform));
        }

        /// <summary>
        /// Whether this store answers "what is this game called" out of the launcher's own installed
        /// files rather than off the network.
        ///
        /// EA reads installerdata.xml and nothing else - its catalogue API is gone, so there is no
        /// online fallback to have. Epic tries its own install manifests before the community database,
        /// so an installed game is answered locally there too. Those are the two names that change the
        /// moment a game is installed, and the only two worth reopening a cached miss for.
        ///
        /// Split out rather than folded into <see cref="AnswersTheName"/> because it is a fact about
        /// each store, in the same shape and on the same axis as
        /// <see cref="SelectStoreNameLookupTarget"/> - so a store added there has to be considered here
        /// too, instead of silently inheriting whichever behaviour the enum's default happened to give
        /// it.
        /// </summary>
        /// <param name="target">The store lookup that applies to the entry's platform.</param>
        internal static bool ResolvesNameFromInstalledFiles(StoreNameLookupTarget target)
        {
            switch (target)
            {
                case StoreNameLookupTarget.Ea:
                case StoreNameLookupTarget.Epic:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether a resolve produced anything the cache does not already hold.
        ///
        /// Only ever false on a name-only retry - see <see cref="AnswersTheName"/> - that came back as
        /// nameless as the entry which triggered it. Writing that would stamp a fresh timestamp on a
        /// verdict this resolve never re-asked, restarting the miss's two-day clock on every library
        /// load; the platform-ID lookup the lifetime exists to re-ask would then never be re-asked at
        /// all, and a game added to SteamGridDB after the first miss would stay missing forever.
        /// Leaving the entry alone lets it age out exactly as it would have.
        /// </summary>
        /// <param name="nameOnlyRetry">Whether the cache supplied this resolve's SteamGridDB verdict.</param>
        /// <param name="gameName">The name the resolve settled on.</param>
        /// <param name="unknownName">The sentinel default name.</param>
        internal static bool LearnedSomethingNew(bool nameOnlyRetry, string gameName, string unknownName)
        {
            return !nameOnlyRetry || gameName != unknownName;
        }

        /// <summary>
        /// Whether a fresh resolve of this outcome would have written an audit line, and therefore
        /// whether a cached one should.
        ///
        /// <see cref="BuildUnmatchedLogLine"/> is reached only from the branch taken when the
        /// platform-ID lookup did not match, so a game found by its store ID is never logged. That is
        /// most of a library, and it is deliberate: the log exists to explain the games that needed
        /// more than a store ID, not to list every game twice.
        ///
        /// The cache does not record which route produced a match, but it does not need to - the two
        /// are distinguishable from what they leave behind. A name search is the only thing that sets
        /// a SteamGridDB game ID, so a match carrying one came the long way round; a match without one
        /// came from its store ID; and no match at all was logged either way.
        /// </summary>
        /// <param name="matched">Whether SteamGridDB knows the game at all.</param>
        /// <param name="steamGridDbGameId">The game's own SteamGridDB ID, set only by a name search.</param>
        internal static bool WasLoggedFresh(bool matched, int steamGridDbGameId)
        {
            return !matched || steamGridDbGameId > 0;
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

            // Set when the cache holds this game's SteamGridDB verdict but no name to go with it, and
            // only the name is being resolved again - see AnswersTheName. The verdict stands, so every
            // SteamGridDB call below is conditioned on this being false.
            bool nameOnlyRetry = false;

            if (canQuerySteamGridDb)
            {
                GameMatchCache.Entry? remembered = await GameMatchCache.GetAsync(platform, externalPlatformId, now);

                if (remembered.HasValue)
                {
                    GameMatchCache.Entry cached = remembered.Value;

                    if (AnswersTheName(platform, cached.Matched, cached.Name, unknownName))
                    {
                        // Logged only for the games a fresh resolve would have logged - see WasLoggedFresh.
                        // Logging every cached game instead made a warm load's audit six times longer than
                        // a cold one's and of a different shape, which defeats the one thing this log is
                        // for: comparing two loads to see why a game is still showing as Unknown.
                        if (WasLoggedFresh(cached.Matched, cached.SteamGridDbGameId))
                        {
                            FixLog.Write($"cached {platform}/{externalPlatformId} name={cached.Name ?? unknownName} sgdbId={cached.SteamGridDbGameId} matched={cached.Matched}");
                        }

                        return new Result(cached.Name ?? gameName, cached.Matched, cached.CapsuleUrl, cached.SteamGridDbGameId);
                    }

                    nameOnlyRetry = true;
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

                // Skipped outright on a name-only retry: the cache is standing in for this exact call,
                // and re-asking it would spend the request the cached verdict exists to save
                if (canQuerySteamGridDb && !nameOnlyRetry && !string.IsNullOrEmpty(platformString))
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

            if (LearnedSomethingNew(nameOnlyRetry, gameName, unknownName)
                && ShouldRemember(canQuerySteamGridDb, lookupThrew, unansweredBefore, canQuerySteamGridDb ? sgdbClient.UnansweredResponses : 0))
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
