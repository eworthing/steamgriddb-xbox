using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.Storage;

namespace SteamGridDB.Xbox.Services.Stores
{
    /// <summary>
    /// Finds the games the Xbox app installed itself, as opposed to the third-party library entries it
    /// merely keeps a record of.
    ///
    /// Every one of them writes a <see cref="XboxGameConfig"/> beside its executable naming its Store
    /// product, and they land in one of two places depending on how they were packaged:
    ///
    /// <list type="bullet">
    /// <item>MSIXVC titles - most large games - unpack into &lt;drive&gt;:\XboxGames\&lt;title&gt;\Content\</item>
    /// <item>plain MSIX titles install into WindowsApps like any other app and have no XboxGames folder</item>
    /// </list>
    ///
    /// Both are scanned, because neither alone is complete. The XboxGames sweep needs no capability and
    /// no API and works for any user; the package query catches the plain-MSIX titles the sweep cannot
    /// see, at the cost of the packageQuery capability in the manifest. When the package query is
    /// refused - or WindowsApps is not readable, which depends on the user - the sweep still returns
    /// the MSIXVC games rather than nothing.
    ///
    /// The result is deliberately Store IDs and not names. Names come from the catalogue, because the
    /// config's own DefaultDisplayName is the shell's ("Minecraft- Java Edition", "MWII- Campaign
    /// Pack") and it is a SteamGridDB search that has to be fed.
    /// </summary>
    internal static class XboxInstalledGames
    {
        /// <summary>The subfolder an MSIXVC title unpacks its payload into.</summary>
        internal const string ContentFolderName = "Content";

        /// <summary>The folder name the Xbox app installs MSIXVC titles under, at the root of a drive.</summary>
        internal const string GamesFolderName = "XboxGames";

        /// <summary>
        /// What the enumeration found, for the library-load log - an empty first-party section is
        /// otherwise indistinguishable from a machine with no Store games on it, the same reason
        /// <see cref="EaLibrary.LoadSummary"/> exists.
        /// </summary>
        internal static string LoadSummary { get; private set; } = "not read yet";

        /// <summary>
        /// Every installed game the Store catalogue confirms is a game, deduplicated by Store ID.
        /// </summary>
        /// <param name="catalog">Catalogue client to resolve names and artwork with.</param>
        internal static async Task<List<StoreCatalog.Product>> LoadAsync(StoreCatalog catalog)
        {
            List<XboxGameConfig.Result> configs = new List<XboxGameConfig.Result>();

            configs.AddRange(await ReadInstalledGamesFolderConfigsAsync());

            int fromFolders = configs.Count;

            configs.AddRange(await ReadPackageConfigsAsync());

            List<string> storeIds = SelectGameStoreIds(configs);

            if (storeIds.Count == 0)
            {
                LoadSummary = $"no installed games found ({fromFolders} from {GamesFolderName}, {configs.Count - fromFolders} from packages)";

                return new List<StoreCatalog.Product>();
            }

            List<StoreCatalog.Product> products = await catalog.GetByStoreIdsAsync(storeIds);
            List<StoreCatalog.Product> games = products.Where(p => p.IsGame).ToList();

            LoadSummary = $"{games.Count} game{(games.Count == 1 ? string.Empty : "s")} "
                + $"from {storeIds.Count} product{(storeIds.Count == 1 ? string.Empty : "s")} "
                + $"({fromFolders} from {GamesFolderName}, {configs.Count - fromFolders} from packages)";

            return games;
        }

        /// <summary>
        /// The Store IDs worth asking the catalogue about: everything that names a product and carries a
        /// title ID, with duplicates collapsed.
        ///
        /// Duplicates are the normal case rather than an edge one - an MSIXVC game is found twice, once
        /// under XboxGames and once as its own registered package - so this runs over the two sweeps
        /// combined rather than either one alone.
        /// </summary>
        /// <param name="configs">Every config both sweeps read.</param>
        internal static List<string> SelectGameStoreIds(IEnumerable<XboxGameConfig.Result> configs)
        {
            List<string> storeIds = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (XboxGameConfig.Result config in configs ?? Enumerable.Empty<XboxGameConfig.Result>())
            {
                if (config.LooksLikeGame && seen.Add(config.StoreId))
                {
                    storeIds.Add(config.StoreId);
                }
            }

            return storeIds;
        }

        /// <summary>
        /// Reads the config of every game under one XboxGames folder.
        ///
        /// Takes the folder rather than finding it, for the same reason
        /// <see cref="EaLibrary.ReadInstallerManifestsAsync"/> does: the part that walks real directories
        /// runs against a throwaway one in the tests, and only locating the real root stays uncovered.
        /// </summary>
        /// <param name="gamesRoot">An XboxGames folder.</param>
        internal static async Task<List<XboxGameConfig.Result>> ReadGameConfigsAsync(StorageFolder gamesRoot)
        {
            List<XboxGameConfig.Result> configs = new List<XboxGameConfig.Result>();

            foreach (StorageFolder gameFolder in await gamesRoot.GetFoldersAsync())
            {
                StorageFolder contentFolder;

                try
                {
                    contentFolder = await gameFolder.GetFolderAsync(ContentFolderName);
                }
                catch (FileNotFoundException)
                {
                    // Not a game folder, or one mid-install - neither is an error
                    continue;
                }

                XboxGameConfig.Result? config = await ReadConfigAsync(contentFolder);

                if (config.HasValue)
                {
                    configs.Add(config.Value);
                }
            }

            return configs;
        }

        /// <summary>
        /// One folder's MicrosoftGame.config, or null when it holds none - which is the normal answer
        /// for the great majority of installed packages, none of which are games.
        /// </summary>
        internal static async Task<XboxGameConfig.Result?> ReadConfigAsync(StorageFolder folder)
        {
            try
            {
                StorageFile file = await folder.GetFileAsync(XboxGameConfig.FileName);

                return XboxGameConfig.Parse(await FileIO.ReadTextAsync(file));
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>
        /// Sweeps every drive's XboxGames folder. Drive letters are tried one by one because UWP has no
        /// drive enumeration, and a letter with nothing at that path simply throws and is skipped.
        /// </summary>
        private static async Task<List<XboxGameConfig.Result>> ReadInstalledGamesFolderConfigsAsync()
        {
            List<XboxGameConfig.Result> configs = new List<XboxGameConfig.Result>();

            for (char letter = 'C'; letter <= 'Z'; letter++)
            {
                StorageFolder gamesRoot;

                try
                {
                    gamesRoot = await StorageFolder.GetFolderFromPathAsync($@"{letter}:\{GamesFolderName}");
                }
                catch (Exception)
                {
                    // No such drive, or no games installed on it
                    continue;
                }

                try
                {
                    configs.AddRange(await ReadGameConfigsAsync(gamesRoot));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not read {letter}:\\{GamesFolderName}: {ex.Message}");
                }
            }

            return configs;
        }

        /// <summary>
        /// Reads the config out of every installed package that has one. A package without one is not a
        /// game, which is what makes this both the lookup and the filter - the alternative would be
        /// asking the Store catalogue about every app on the machine.
        /// </summary>
        private static async Task<List<XboxGameConfig.Result>> ReadPackageConfigsAsync()
        {
            List<XboxGameConfig.Result> configs = new List<XboxGameConfig.Result>();
            IEnumerable<Package> packages;

            try
            {
                packages = new PackageManager().FindPackagesForUser(string.Empty);
            }
            catch (Exception ex)
            {
                // packageQuery not granted, or the query is unavailable - the XboxGames sweep still stands
                System.Diagnostics.Debug.WriteLine($"Could not query installed packages: {ex.Message}");

                return configs;
            }

            foreach (Package package in packages)
            {
                // Windows' own inbox components are signed by the system rather than the Store and are
                // never games. Skipping them before touching InstalledLocation matters: resolving a
                // package's folder and probing it for a config is the expensive part of this sweep, and
                // most of a machine's hundred-odd packages are these.
                if (package.IsFramework || package.IsResourcePackage
                    || package.SignatureKind == PackageSignatureKind.System)
                {
                    continue;
                }

                StorageFolder installFolder;

                try
                {
                    installFolder = package.InstalledLocation;
                }
                catch (Exception)
                {
                    // Registered but not actually on disk - a game that was uninstalled, or one on a
                    // drive that is not attached
                    continue;
                }

                if (installFolder == null)
                {
                    continue;
                }

                XboxGameConfig.Result? config = await ReadConfigAsync(installFolder);

                if (config.HasValue)
                {
                    configs.Add(config.Value);
                }
            }

            return configs;
        }
    }
}
