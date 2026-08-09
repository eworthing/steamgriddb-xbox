using SteamGridDB.Xbox.Services.Stores;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// MicrosoftGame.config is how a first-party game names the Store product it is, and getting it
    /// wrong is not visible in the UI - a game whose Store ID is missed simply never appears, which
    /// looks exactly like a game whose tile has never been rendered.
    ///
    /// The content-pack case is the one worth pinning. A single Call of Duty install leaves a dozen
    /// folders beside the game that carry a Store ID exactly as it does; what separates them at this
    /// level is that each names the main package it is content for.
    ///
    /// The classics are the other half of the same rule, and the reason it is not the TitleId test it
    /// used to be. Wolfenstein 3D carries a Store ID, no TitleId and no main package, and under the old
    /// rule it was dropped as if it were a content pack - a game silently missing from the library.
    /// </summary>
    public class XboxGameConfigTests
    {
        private const string gameConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Game configVersion=""1"">
  <Identity Name=""10192RubberBandGames.WobblyLife"" Publisher=""CN=387DEE76"" Version=""1.0.21.0"" />
  <ShellVisuals DefaultDisplayName=""Wobbly Life"" PublisherDisplayName=""RubberBandGames"" />
  <StoreId>9NS86BQ33SPX</StoreId>
  <TitleId>68d51b74</TitleId>
</Game>";

        private const string contentPackConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Game configVersion=""1"">
  <ShellVisuals DefaultDisplayName=""MWII PC MS DLC03 Cross-Gen Pack 02"" />
  <StoreId>9N57K40Q94MV</StoreId>
  <TitleId></TitleId>
  <AllowedProducts>
    <AllowedProduct>9N201KQXS5BM</AllowedProduct>
  </AllowedProducts>
  <DesktopRegistration>
    <MainPackageDependency Name=""38985CA0.COREBase"" />
    <ProcessorArchitecture>x64</ProcessorArchitecture>
  </DesktopRegistration>
</Game>";

        /// <summary>
        /// Wolfenstein 3D's own config, trimmed. The Store's re-released classics ship one of these:
        /// a real game, with a Store ID and no TitleId at all.
        /// </summary>
        private const string classicConfig = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Game configVersion=""1"">
  <Identity Name=""BethesdaSoftworks.Wolfenstein3D"" Publisher=""CN=21E520D9"" Version=""1.6.3.0"" />
  <StoreId>9P7Z1D3N8KR7</StoreId>
  <ShellVisuals DefaultDisplayName=""Wolfenstein 3D"" PublisherDisplayName=""Bethesda Softworks"" />
  <DesktopRegistration>
    <ProcessorArchitecture>x86</ProcessorArchitecture>
  </DesktopRegistration>
</Game>";

        [Fact]
        public void Reads_the_store_id()
        {
            Assert.Equal("9NS86BQ33SPX", XboxGameConfig.Parse(gameConfig).StoreId);
        }

        [Fact]
        public void Reads_the_display_name_from_the_shell_visuals_attribute()
        {
            Assert.Equal("Wobbly Life", XboxGameConfig.Parse(gameConfig).DisplayName);
        }

        [Fact]
        public void A_game_looks_like_a_game()
        {
            Assert.True(XboxGameConfig.Parse(gameConfig).LooksLikeGame);
        }

        [Fact]
        public void A_content_pack_does_not_look_like_a_game()
        {
            XboxGameConfig.Result result = XboxGameConfig.Parse(contentPackConfig);

            // It still carries a Store ID - that is exactly why naming a main package has to be what
            // decides, rather than the presence of an ID
            Assert.Equal("9N57K40Q94MV", result.StoreId);
            Assert.True(result.IsContentPack);
            Assert.False(result.LooksLikeGame);
        }

        [Fact]
        public void A_classic_with_no_title_id_still_looks_like_a_game()
        {
            XboxGameConfig.Result result = XboxGameConfig.Parse(classicConfig);

            // It has a DesktopRegistration like a content pack does, and no TitleId like a content pack
            // does. What it does not have is a main package it belongs to.
            Assert.Equal("9P7Z1D3N8KR7", result.StoreId);
            Assert.False(result.IsContentPack);
            Assert.True(result.LooksLikeGame);
        }

        [Fact]
        public void A_config_with_no_store_id_does_not_look_like_a_game()
        {
            XboxGameConfig.Result result = XboxGameConfig.Parse(
                @"<Game configVersion=""1""><TitleId>68d51b74</TitleId></Game>");

            Assert.Null(result.StoreId);
            Assert.False(result.LooksLikeGame);
        }

        [Fact]
        public void Ignores_a_main_package_dependency_that_is_not_under_DesktopRegistration()
        {
            // Guards the XPath the same way the Store ID's is guarded: a MainPackageDependency
            // somewhere else in the document would otherwise silently drop a real game
            XboxGameConfig.Result result = XboxGameConfig.Parse(
                @"<Game configVersion=""1""><StoreId>9NS86BQ33SPX</StoreId>
                  <ExtendedAttributeList><MainPackageDependency Name=""Elsewhere"" /></ExtendedAttributeList></Game>");

            Assert.False(result.IsContentPack);
            Assert.True(result.LooksLikeGame);
        }

        [Fact]
        public void Ignores_a_store_id_that_is_not_directly_under_Game()
        {
            // Guards the XPath against matching anywhere in the document: a nested element of the same
            // name would otherwise be read as the product's own ID
            XboxGameConfig.Result result = XboxGameConfig.Parse(
                @"<Game configVersion=""1""><ExtendedAttributeList><StoreId>9NOTTHISONE</StoreId></ExtendedAttributeList></Game>");

            Assert.Null(result.StoreId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not xml at all")]
        [InlineData("<Game><unclosed>")]
        public void Unreadable_input_yields_nothing_rather_than_throwing(string xml)
        {
            XboxGameConfig.Result result = XboxGameConfig.Parse(xml);

            Assert.Null(result.StoreId);
            Assert.Null(result.DisplayName);
            Assert.False(result.IsContentPack);
            Assert.False(result.LooksLikeGame);
        }

        [Fact]
        public void Trims_surrounding_whitespace()
        {
            XboxGameConfig.Result result = XboxGameConfig.Parse(
                "<Game>\n  <StoreId>\n    9NS86BQ33SPX\n  </StoreId>\n</Game>");

            Assert.Equal("9NS86BQ33SPX", result.StoreId);
        }
    }
}
