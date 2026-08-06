using System;
using System.IO;

namespace SteamGridDB.Xbox.Services.Xbox
{
    /// <summary>
    /// Where the Xbox app keeps the two things this widget reads.
    ///
    /// Built from the user profile and a literal path rather than from %LOCALAPPDATA%, which is the
    /// whole reason this type exists. Windows redirects that variable for a packaged app to its own
    /// sandbox - inside this widget it resolves to
    /// ...\Packages\eworthing.SteamGridDBforXbox.Dev_...\LocalCache\Local - so a path built from it
    /// points at somewhere that has never held anything, and the folder simply appears to be missing.
    /// That failure is silent by nature: the widget cannot tell "the Xbox app has no image cache" from
    /// "the path was wrong", and both look like a machine with no first-party games on it.
    ///
    /// %ProgramData% is not redirected the same way, which is why <see cref="Stores.EaLibrary"/> and
    /// <see cref="Stores.EpicLibrary"/> can read it straight from the environment. Only the per-user
    /// AppData variables are rewritten.
    /// </summary>
    internal static class XboxAppData
    {
        /// <summary>The Xbox app's package family, which names its data folder.</summary>
        internal const string PackageFamilyName = "Microsoft.GamingApp_8wekyb3d8bbwe";

        /// <summary>Root of the Xbox app's own per-user data.</summary>
        internal static readonly string PackageDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            @"AppData\Local\Packages",
            PackageFamilyName);

        /// <summary>
        /// Where the Xbox app writes a tile per third-party game - one PNG per manifest entry, named
        /// after the entry's own ID.
        /// </summary>
        internal static readonly string ThirdPartyLibrariesPath =
            Path.Combine(PackageDataPath, @"LocalState\ThirdPartyLibraries");

        /// <summary>
        /// Where the Xbox app caches the artwork it renders first-party tiles from - files named by a
        /// hash of the request that fetched them, with no extension. Under LocalCache rather than
        /// LocalState, so it is not reachable from the third-party path above.
        /// </summary>
        internal static readonly string ImageCachePath =
            Path.Combine(PackageDataPath, @"LocalCache\ImageCache");
    }
}
