using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services.Library;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The pure parts of resolving a manifest entry's SteamGridDB match - split out of
    /// LoadGameEntriesAsync's per-entry parsing (see PrimaryWidget.xaml.cs) so the
    /// platform-to-store-lookup dispatch decision and the FixLog audit line's exact shape are exercised
    /// directly, instead of only by inspection several hundred lines into a UI-bound method.
    ///
    /// <see cref="GameMatchResolver.ResolveAsync"/> itself is not covered here: it is real network I/O
    /// against SteamGridDB/GOG/Epic/Ubisoft, the same carve-out TESTING.md already documents for
    /// StoreNameLookup's own fetch methods - a test exercising the network would be grading their
    /// uptime, not this codebase.
    /// </summary>
    public class GameMatchResolverTests
    {
        // ---- SelectStoreNameLookupTarget: platform -> which store's name lookup applies ----

        [Fact]
        public void GOG_platform_selects_the_Gog_target()
        {
            Assert.Equal(GameMatchResolver.StoreNameLookupTarget.Gog, GameMatchResolver.SelectStoreNameLookupTarget(GamePlatform.GOG));
        }

        [Fact]
        public void Epic_platform_selects_the_Epic_target()
        {
            Assert.Equal(GameMatchResolver.StoreNameLookupTarget.Epic, GameMatchResolver.SelectStoreNameLookupTarget(GamePlatform.Epic));
        }

        [Fact]
        public void Ubisoft_platform_selects_the_Ubisoft_target()
        {
            Assert.Equal(GameMatchResolver.StoreNameLookupTarget.Ubisoft, GameMatchResolver.SelectStoreNameLookupTarget(GamePlatform.Ubisoft));
        }

        [Fact]
        public void EA_platform_selects_the_Ea_target()
        {
            Assert.Equal(GameMatchResolver.StoreNameLookupTarget.Ea, GameMatchResolver.SelectStoreNameLookupTarget(GamePlatform.EA));
        }

        [Theory]
        [InlineData(GamePlatform.Steam)]
        [InlineData(GamePlatform.Custom)]
        [InlineData(GamePlatform.BattleNet)]
        [InlineData(GamePlatform.Unknown)]
        public void Platforms_with_no_store_name_lookup_select_None(GamePlatform platform)
        {
            Assert.Equal(GameMatchResolver.StoreNameLookupTarget.None, GameMatchResolver.SelectStoreNameLookupTarget(platform));
        }

        // ---- BuildUnmatchedLogLine: the FixLog audit line's exact shape ----

        [Fact]
        public void Platforms_with_no_local_manifest_read_omit_the_store_segment()
        {
            string line = GameMatchResolver.BuildUnmatchedLogLine(
                GamePlatform.GOG, "gog:1234", epicCatalogItemId: null, storeLoadSummary: null, gameName: "Halo", steamGridDbGameId: 0);

            Assert.Equal("unmatched GOG/gog:1234 name=Halo sgdbId=0", line);
        }

        [Fact]
        public void Epic_platform_includes_the_catalog_item_id_and_epic_load_summary()
        {
            string line = GameMatchResolver.BuildUnmatchedLogLine(
                GamePlatform.Epic, "epic:Sugar", epicCatalogItemId: "abc123", storeLoadSummary: "4 manifests from C:\\Epic", gameName: "Sugar", steamGridDbGameId: 42);

            Assert.Equal("unmatched Epic/epic:Sugar catalog=abc123 epic=[4 manifests from C:\\Epic] name=Sugar sgdbId=42", line);
        }

        [Fact]
        public void Epic_platform_with_no_catalog_item_id_shows_none()
        {
            string line = GameMatchResolver.BuildUnmatchedLogLine(
                GamePlatform.Epic, "epic:Sugar", epicCatalogItemId: null, storeLoadSummary: "not read yet", gameName: "Sugar", steamGridDbGameId: 0);

            Assert.Equal("unmatched Epic/epic:Sugar catalog=none epic=[not read yet] name=Sugar sgdbId=0", line);
        }

        [Fact]
        public void EA_platform_includes_its_load_summary_but_no_catalog_item_id()
        {
            // EA entries carry one identifier, not Epic's two - but the same "did the launcher's own
            // manifests get read at all" question, which is otherwise invisible
            string line = GameMatchResolver.BuildUnmatchedLogLine(
                GamePlatform.EA, "194814", epicCatalogItemId: null, storeLoadSummary: "3 content ids from C:\\Program Files\\EA Games", gameName: "Unknown", steamGridDbGameId: 0);

            Assert.Equal("unmatched EA/194814 ea=[3 content ids from C:\\Program Files\\EA Games] name=Unknown sgdbId=0", line);
        }

        // ---- AnswersTheName: which cached entries stand on their own ----

        [Fact]
        public void A_cached_miss_with_no_name_does_not_stand_on_its_own()
        {
            // The verdict is days-old fact; the missing name is not. Installing the game fills it in
            // without SteamGridDB changing, and holding "Unknown" for two days hides exactly that.
            Assert.False(GameMatchResolver.AnswersTheName(GamePlatform.EA, matched: false, cachedName: "Unknown", unknownName: "Unknown"));
        }

        [Fact]
        public void A_cached_miss_carrying_no_name_at_all_does_not_either()
        {
            // GameMatchCache omits the name rather than writing an empty one, so both shapes arrive
            Assert.False(GameMatchResolver.AnswersTheName(GamePlatform.EA, matched: false, cachedName: null, unknownName: "Unknown"));
        }

        [Fact]
        public void A_cached_miss_with_a_real_name_is_a_whole_answer()
        {
            // The name search was performed against this name and found nothing - re-running the store
            // lookup would produce the same name and the same miss
            Assert.True(GameMatchResolver.AnswersTheName(GamePlatform.EA, matched: false, cachedName: "Plants vs Zombies GW2", unknownName: "Unknown"));
        }

        [Fact]
        public void A_cached_match_is_a_whole_answer_even_carrying_no_name()
        {
            // The case that pins the "matched" clause down: a match's name came from SteamGridDB
            // itself, so there is no installed file to reopen whether or not one was written
            Assert.True(GameMatchResolver.AnswersTheName(GamePlatform.EA, matched: true, cachedName: null, unknownName: "Unknown"));
        }

        [Theory]
        [InlineData(GamePlatform.GOG)]
        [InlineData(GamePlatform.Ubisoft)]
        [InlineData(GamePlatform.Steam)]
        [InlineData(GamePlatform.Custom)]
        public void A_nameless_miss_stands_where_no_installed_file_would_answer_it(GamePlatform platform)
        {
            // GOG's name comes from its API and Ubisoft's from a list on GitHub - no install changes
            // either, and StoreNameLookup caches nothing when a store is down, so reopening these
            // would re-ask a failing API on every widget open. Steam and Custom have no lookup at all.
            Assert.True(GameMatchResolver.AnswersTheName(platform, matched: false, cachedName: "Unknown", unknownName: "Unknown"));
        }

        // ---- ResolvesNameFromInstalledFiles: which stores an install event can answer ----

        [Fact]
        public void The_stores_that_read_their_own_installed_files_are_reopened()
        {
            // EA reads installerdata.xml and has no online fallback left; Epic tries its own manifests
            // before the community database. Both answer an installed game from disk.
            Assert.True(GameMatchResolver.ResolvesNameFromInstalledFiles(GameMatchResolver.StoreNameLookupTarget.Ea));
            Assert.True(GameMatchResolver.ResolvesNameFromInstalledFiles(GameMatchResolver.StoreNameLookupTarget.Epic));
        }

        [Fact]
        public void The_stores_that_ask_the_network_are_not()
        {
            // Neither answer is one an install can change, and a store that is down caches nothing -
            // so reopening these would re-ask a failing API on every widget open
            Assert.False(GameMatchResolver.ResolvesNameFromInstalledFiles(GameMatchResolver.StoreNameLookupTarget.Gog));
            Assert.False(GameMatchResolver.ResolvesNameFromInstalledFiles(GameMatchResolver.StoreNameLookupTarget.Ubisoft));
            Assert.False(GameMatchResolver.ResolvesNameFromInstalledFiles(GameMatchResolver.StoreNameLookupTarget.None));
        }

        // ---- LearnedSomethingNew: whether a name-only retry may rewrite its entry ----

        [Fact]
        public void A_name_only_retry_that_found_no_name_must_not_rewrite_its_entry()
        {
            // It asked SteamGridDB nothing. Restamping the entry would restart the miss's two-day
            // clock on every load, so the platform-ID lookup would never be re-asked at all.
            Assert.False(GameMatchResolver.LearnedSomethingNew(nameOnlyRetry: true, gameName: "Unknown", unknownName: "Unknown"));
        }

        [Fact]
        public void A_name_only_retry_that_found_a_name_may()
        {
            // A name appeared and was searched for - a genuinely new answer, worth its new timestamp
            Assert.True(GameMatchResolver.LearnedSomethingNew(nameOnlyRetry: true, gameName: "SimCity 2000 Special Edition", unknownName: "Unknown"));
        }

        [Fact]
        public void A_full_resolve_always_may_even_when_it_found_no_name()
        {
            // Nothing was standing in for the platform-ID lookup here, so the miss is this resolve's
            // own finding and dates from now
            Assert.True(GameMatchResolver.LearnedSomethingNew(nameOnlyRetry: false, gameName: "Unknown", unknownName: "Unknown"));
        }

        // ---- ShouldRemember: what may be written to the match cache ----

        [Fact]
        public void An_answered_lookup_is_worth_remembering()
        {
            Assert.True(GameMatchResolver.ShouldRemember(
                canQuerySteamGridDb: true, lookupThrew: false, unansweredBefore: 0, unansweredAfter: 0));
        }

        [Fact]
        public void A_lookup_made_without_an_api_key_is_not()
        {
            // Nothing was asked, so "no match" is not an answer about the game - and caching it would
            // leave that miss waiting for the moment a key is finally configured.
            Assert.False(GameMatchResolver.ShouldRemember(
                canQuerySteamGridDb: false, lookupThrew: false, unansweredBefore: 0, unansweredAfter: 0));
        }

        [Fact]
        public void A_lookup_that_threw_is_not()
        {
            // A timeout or a dead network, which looks exactly like "SteamGridDB does not have this
            // game" by the time it reaches the caller.
            Assert.False(GameMatchResolver.ShouldRemember(
                canQuerySteamGridDb: true, lookupThrew: true, unansweredBefore: 0, unansweredAfter: 0));
        }

        [Fact]
        public void A_lookup_that_went_unanswered_anywhere_along_the_way_is_not()
        {
            // The counter is the only evidence: a rate-limited or failed lookup returns the same null
            // a genuine miss does. Writing that would turn a bad minute into days of Unknown.
            Assert.False(GameMatchResolver.ShouldRemember(
                canQuerySteamGridDb: true, lookupThrew: false, unansweredBefore: 2, unansweredAfter: 3));
        }

        [Fact]
        public void Failures_from_before_this_game_do_not_disqualify_it()
        {
            // An earlier game in the same load went unanswered and this one did not. Only the change
            // across this resolve says anything about this resolve.
            Assert.True(GameMatchResolver.ShouldRemember(
                canQuerySteamGridDb: true, lookupThrew: false, unansweredBefore: 5, unansweredAfter: 5));
        }

        // ---- WasLoggedFresh: which cached games still get an audit line ----

        [Fact]
        public void A_game_matched_by_its_store_id_is_not_logged()
        {
            // The fresh path only logs from the branch taken when the platform-ID lookup missed, so a
            // store-ID match never produced a line. Logging one from the cache made a warm load's audit
            // six times longer than a cold one's, which is the comparison the log exists for.
            Assert.False(GameMatchResolver.WasLoggedFresh(matched: true, steamGridDbGameId: 0));
        }

        [Fact]
        public void A_game_found_by_name_search_is_logged()
        {
            // A name search is the only thing that sets this ID, so carrying one means the store ID
            // missed and the fresh path would have written the line.
            Assert.True(GameMatchResolver.WasLoggedFresh(matched: true, steamGridDbGameId: 5309266));
        }

        [Fact]
        public void An_unmatched_game_is_logged()
        {
            // The line that answers "why is this one still Unknown", which is the whole point.
            Assert.True(GameMatchResolver.WasLoggedFresh(matched: false, steamGridDbGameId: 0));
        }

        // ---- Result: a plain readonly carrier, no behavior of its own ----

        [Fact]
        public void Result_carries_every_field_through_unchanged()
        {
            var result = new GameMatchResolver.Result("Halo", true, "https://example/capsule.png", 99);

            Assert.Equal("Halo", result.GameName);
            Assert.True(result.HasSteamGridDbMatch);
            Assert.Equal("https://example/capsule.png", result.OfficialCapsuleUrl);
            Assert.Equal(99, result.SteamGridDbGameId);
        }
    }
}
