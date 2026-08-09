using SteamGridDB.Xbox.Services.Stores;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The one test that lets a package with no MicrosoftGame.config into the library.
    ///
    /// It runs against every installed package that had no config to read - around a hundred on an
    /// ordinary machine, of which three are games - so both halves of it matter. Saying no to a game
    /// is Microsoft Mahjong, Solitaire and Halo: Spartan Strike missing from the list with nothing to
    /// show why; saying yes to an app is a wasted round trip to the Store catalogue for every load.
    /// </summary>
    public class PackageManifestTests
    {
        private static string Manifest(string extensions)
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8"" standalone=""yes""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""
         xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10"">
  <Identity Name=""Microsoft.MicrosoftMahjong"" Publisher=""CN=Microsoft Corporation"" Version=""4.6.12100.0"" />
  <Applications>
    <Application Id=""App"" Executable=""Mahjong.exe"">
      <Extensions>{extensions}</Extensions>
    </Application>
  </Applications>
</Package>";
        }

        private const string xboxLiveProtocol =
            @"<uap:Extension Category=""windows.protocol""><uap:Protocol Name=""xboxliveapp-1297290225"" /></uap:Extension>";

        [Fact]
        public void An_xbox_live_protocol_declares_a_game()
        {
            Assert.True(PackageManifest.DeclaresXboxLiveGame(Manifest(xboxLiveProtocol)));
        }

        [Fact]
        public void A_games_own_protocol_alongside_it_makes_no_difference()
        {
            // Mahjong declares both, in this order - the app's own scheme first
            Assert.True(PackageManifest.DeclaresXboxLiveGame(Manifest(
                @"<uap:Extension Category=""windows.protocol""><uap:Protocol Name=""microsoftmahjong"" /></uap:Extension>"
                + xboxLiveProtocol)));
        }

        [Fact]
        public void An_ordinary_protocol_handler_does_not_declare_a_game()
        {
            Assert.False(PackageManifest.DeclaresXboxLiveGame(Manifest(
                @"<uap:Extension Category=""windows.protocol""><uap:Protocol Name=""ms-settings"" /></uap:Extension>")));
        }

        [Fact]
        public void A_manifest_with_no_extensions_at_all_does_not_declare_a_game()
        {
            Assert.False(PackageManifest.DeclaresXboxLiveGame(Manifest(string.Empty)));
        }

        [Fact]
        public void The_namespace_prefix_is_not_assumed()
        {
            // "uap" is convention, not schema. A manifest is free to bind the namespace to any prefix,
            // or to none, and dropping a game over its choice of prefix would be invisible.
            Assert.True(PackageManifest.DeclaresXboxLiveGame(@"<?xml version=""1.0""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""
         xmlns:x=""http://schemas.microsoft.com/appx/manifest/uap/windows10"">
  <Applications><Application Id=""App""><Extensions>
    <x:Extension Category=""windows.protocol""><x:Protocol Name=""xboxliveapp-1297290225"" /></x:Extension>
  </Extensions></Application></Applications>
</Package>"));
        }

        [Fact]
        public void The_prefix_appearing_anywhere_else_is_not_enough()
        {
            // The substring test that rejects most manifests without parsing is a filter, not the
            // answer: what counts is a protocol actually registered under the name
            Assert.False(PackageManifest.DeclaresXboxLiveGame(Manifest(
                @"<uap:Extension Category=""windows.protocol""><uap:Protocol Name=""notxboxliveapp-1297290225"" /></uap:Extension>")));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not xml at all")]
        [InlineData(@"<Package><Applications><uap:Protocol Name=""xboxliveapp-1"" ")]
        public void Unreadable_input_is_not_a_game_rather_than_throwing(string xml)
        {
            Assert.False(PackageManifest.DeclaresXboxLiveGame(xml));
        }
    }
}
