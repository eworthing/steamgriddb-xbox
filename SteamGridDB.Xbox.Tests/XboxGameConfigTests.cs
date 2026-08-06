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
    /// folders beside the game that carry a Store ID exactly as it does; the only thing separating
    /// them at this level is an empty TitleId.
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
        public void Reads_the_title_id()
        {
            Assert.Equal("68d51b74", XboxGameConfig.Parse(gameConfig).TitleId);
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

            // It still carries a Store ID - that is exactly why the empty TitleId has to be what
            // decides, rather than the presence of an ID
            Assert.Equal("9N57K40Q94MV", result.StoreId);
            Assert.Null(result.TitleId);
            Assert.False(result.LooksLikeGame);
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
            Assert.Null(result.TitleId);
            Assert.False(result.LooksLikeGame);
        }

        [Fact]
        public void Trims_surrounding_whitespace()
        {
            XboxGameConfig.Result result = XboxGameConfig.Parse(
                "<Game>\n  <StoreId>\n    9NS86BQ33SPX\n  </StoreId>\n  <TitleId> 68d51b74 </TitleId>\n</Game>");

            Assert.Equal("9NS86BQ33SPX", result.StoreId);
            Assert.Equal("68d51b74", result.TitleId);
        }
    }
}
