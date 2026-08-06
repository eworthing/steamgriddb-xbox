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
