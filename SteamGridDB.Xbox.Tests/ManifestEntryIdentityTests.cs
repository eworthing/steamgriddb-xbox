using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services.Library;

using Windows.Data.Json;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Deriving a manifest entry's store ID(s) and default display name from its platform and JSON
    /// fields - split out of LoadGameEntriesAsync's per-entry parsing (see PrimaryWidget.xaml.cs) so
    /// the platform-specific rules, especially Epic's, are exercised directly rather than only by
    /// inspection several hundred lines into a UI-bound method.
    /// </summary>
    public class ManifestEntryIdentityTests
    {
        private static JsonObject Parse(string json)
        {
            return JsonObject.Parse(json);
        }

        [Fact]
        public void Custom_platform_uses_title_for_the_game_name()
        {
            JsonObject entry = Parse(@"{""title"":""Chess Ultra""}");

            ManifestEntryIdentity.Result result = ManifestEntryIdentity.Derive(entry, GamePlatform.Custom, entryId: null, unknownGameNameDefault: "Unknown");

            Assert.Equal("Chess Ultra", result.GameName);
        }

        [Fact]
        public void Custom_platform_falls_back_to_the_default_name_when_title_is_json_null()
        {
            // The raw-accessor bug this same field-extraction closed at its old call site (see
            // JsonReadTests): a present-but-null title must not throw, it must fall back.
            JsonObject entry = Parse(@"{""title"":null}");

            ManifestEntryIdentity.Result result = ManifestEntryIdentity.Derive(entry, GamePlatform.Custom, entryId: null, unknownGameNameDefault: "Unknown");

            Assert.Equal("Unknown", result.GameName);
        }

        [Fact]
        public void Custom_platform_combines_install_location_and_executable_name_into_the_store_id()
        {
            JsonObject entry = Parse(@"{""installLocation"":""C:\\Games\\Chess"",""executableName"":""chess.exe""}");

            ManifestEntryIdentity.Result result = ManifestEntryIdentity.Derive(entry, GamePlatform.Custom, entryId: null, unknownGameNameDefault: "Unknown");

            Assert.Equal(System.IO.Path.Combine("C:\\Games\\Chess", "chess.exe"), result.ExternalPlatformId);
            Assert.Null(result.EpicCatalogItemId);
        }

        [Fact]
        public void Custom_platform_treats_missing_install_fields_as_empty_rather_than_throwing()
        {
            JsonObject entry = Parse(@"{}");

            ManifestEntryIdentity.Result result = ManifestEntryIdentity.Derive(entry, GamePlatform.Custom, entryId: null, unknownGameNameDefault: "Unknown");

            Assert.Equal(string.Empty, result.ExternalPlatformId);
        }

        // Non-Epic platforms strip the prefix up to the first colon.
        //
        // The three Epic rows cover "epic:<namespace>:<catalogItemId>:<appName>" - SteamGridDB wants
        // appName, the name source (EpicLibrary/GetEpicGameNameAsync) wants catalogItemId - and the
        // boundary the raw code guards explicitly: parts.Length >= 3 for the split, >= 4 for the
        // catalog item. A mutation flipping either comparison would pass every other row here silently
        // without the three- and fewer-than-three-segment cases.
        [Theory]
        [InlineData(GamePlatform.GOG, "gog:1234567890", "1234567890", null)]
        [InlineData(GamePlatform.Epic, "epic:ns123:catalog456:Sugar", "Sugar", "catalog456")]
        [InlineData(GamePlatform.Epic, "epic:ns123:Sugar", "Sugar", null)]
        [InlineData(GamePlatform.Epic, "epic:Sugar", "Sugar", null)]
        public void Platform_specific_ids_are_parsed_into_the_external_platform_id_and_epic_catalog_item_id(
            GamePlatform platform, string entryId, string expectedExternalPlatformId, string expectedEpicCatalogItemId)
        {
            JsonObject entry = Parse(@"{}");

            ManifestEntryIdentity.Result result = ManifestEntryIdentity.Derive(entry, platform, entryId, unknownGameNameDefault: "Unknown");

            Assert.Equal(expectedExternalPlatformId, result.ExternalPlatformId);
            Assert.Equal(expectedEpicCatalogItemId, result.EpicCatalogItemId);
        }

        [Fact]
        public void Non_custom_platforms_keep_the_default_game_name()
        {
            JsonObject entry = Parse(@"{""title"":""Should not be read for this platform""}");

            ManifestEntryIdentity.Result result = ManifestEntryIdentity.Derive(entry, GamePlatform.Steam, entryId: "steam:440", unknownGameNameDefault: "Unknown");

            Assert.Equal("Unknown", result.GameName);
        }
    }
}
