using System;
using System.IO;

using SteamGridDB.Xbox.Services.Xbox;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Which source the Xbox app's paths are built from.
    ///
    /// This looks like it is restating the implementation, and that is the point: the bug it guards
    /// against is not a wrong path but a wrong *source*. Reading %LOCALAPPDATA% gives the right answer
    /// everywhere except inside an app container, which is the only place the widget ever runs -
    /// Windows rewrites it there to the widget's own sandbox, so the Xbox app's folders come back as
    /// simply not existing. That shipped once: the first-party section rendered empty on a machine
    /// with eight installed games and no error anywhere, because "the folder is missing" and "there
    /// are no Store games" are the same outcome.
    ///
    /// These tests cannot reproduce the redirection - it does not happen in a plain test host, which
    /// is exactly why it went unnoticed - so what they pin is that the construction never goes back to
    /// an environment variable that packaging rewrites.
    /// </summary>
    public class XboxAppDataTests
    {
        private static string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        [Fact]
        public void Package_data_is_built_from_the_user_profile()
        {
            Assert.Equal(
                Path.Combine(UserProfile, @"AppData\Local\Packages\Microsoft.GamingApp_8wekyb3d8bbwe"),
                XboxAppData.PackageDataPath);
        }

        [Fact]
        public void Paths_point_at_the_xbox_app_and_not_at_whatever_app_is_asking()
        {
            // The shape the redirection produces: a path under some *other* package's folder. Both of
            // these live under Microsoft.GamingApp's, always.
            Assert.StartsWith(XboxAppData.PackageDataPath, XboxAppData.ThirdPartyLibrariesPath, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith(XboxAppData.PackageDataPath, XboxAppData.ImageCachePath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Third_party_libraries_and_the_image_cache_are_in_different_halves_of_the_app_data()
        {
            // LocalState and LocalCache are separate trees; reading one never reaches the other, which
            // is why first-party games needed a second path at all
            Assert.EndsWith(@"LocalState\ThirdPartyLibraries", XboxAppData.ThirdPartyLibrariesPath);
            Assert.EndsWith(@"LocalCache\ImageCache", XboxAppData.ImageCachePath);
        }
    }
}
