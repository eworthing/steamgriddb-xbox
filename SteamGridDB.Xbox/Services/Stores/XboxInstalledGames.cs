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
    /// Most of them write a <see cref="XboxGameConfig"/> beside their executable naming their Store
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
    /// The Store's older titles carry no config at all, so neither sweep can name their product: they
    /// are picked out of the package query by <see cref="PackageManifest"/> instead and looked up by
    /// package family name. That costs a request each, which is why it is reserved for packages that
    /// had no config to read.
    ///
    /// Nothing here decides what is a game. Both routes hand the catalogue every product that could be
    /// one and let its own product kind settle it, and a product the Xbox app has never drawn a tile
    /// for has nothing in its image cache to find - so guessing wide costs a lookup, never a wrong row.
    ///
    /// The result is deliberately the catalogue's products and not the configs' own names, because
    /// DefaultDisplayName is the shell's ("Minecraft- Java Edition", "MWII- Campaign Pack") and it is a
    /// SteamGridDB search that has to be fed.
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
            PackageSweep sweep = await ReadPackagesAsync();

            configs.AddRange(sweep.Configs);

            List<string> storeIds = SelectGameStoreIds(configs);
            List<StoreCatalog.Product> products = new List<StoreCatalog.Product>();

            if (storeIds.Count > 0)
            {
                products.AddRange(await catalog.GetByStoreIdsAsync(storeIds));
            }

            products.AddRange(await catalog.GetByPackageFamilyNamesAsync(sweep.ConfiglessGameFamilyNames));

            List<StoreCatalog.Product> games = SelectGames(products);
            int asked = storeIds.Count + sweep.ConfiglessGameFamilyNames.Count;

            LoadSummary = asked == 0
                ? $"no installed games found ({fromFolders} from {GamesFolderName}, {sweep.Configs.Count} from packages)"
                : $"{games.Count} game{(games.Count == 1 ? string.Empty : "s")} "
                    + $"from {asked} product{(asked == 1 ? string.Empty : "s")} "
                    + $"({fromFolders} from {GamesFolderName}, {sweep.Configs.Count} from packages, "
                    + $"{sweep.ConfiglessGameFamilyNames.Count} by package family)";

            return games;
        }

        /// <summary>
        /// The products worth listing, with duplicates collapsed: the ones the catalogue itself calls
        /// games.
        ///
        /// This is where the content packs a sweep could not rule out are dropped, and the only place
        /// that judgement is made - <see cref="XboxGameConfig.Result.LooksLikeGame"/> and
        /// <see cref="PackageManifest.DeclaresXboxLiveGame"/> both only decide what is worth asking
        /// about.
        /// </summary>
        /// <param name="products">Everything both catalogue lookups returned.</param>
        internal static List<StoreCatalog.Product> SelectGames(IEnumerable<StoreCatalog.Product> products)
        {
            List<StoreCatalog.Product> games = new List<StoreCatalog.Product>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (StoreCatalog.Product product in products ?? Enumerable.Empty<StoreCatalog.Product>())
            {
                if (product.IsGame && seen.Add(product.StoreId))
                {
                    games.Add(product);
                }
            }

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
        /// What the package query found, which is two different things: the configs of the packages
        /// that have one, and the identity of the games that do not.
        /// </summary>
        private readonly struct PackageSweep
        {
            internal PackageSweep(List<XboxGameConfig.Result> configs, List<string> configlessGameFamilyNames)
            {
                Configs = configs;
                ConfiglessGameFamilyNames = configlessGameFamilyNames;
            }

            /// <summary>Every MicrosoftGame.config found beside an installed package.</summary>
            internal List<XboxGameConfig.Result> Configs { get; }

            /// <summary>
            /// Package family names of the installed games that carry no config, and so can only be
            /// named to the catalogue by the package they are.
            /// </summary>
            internal List<string> ConfiglessGameFamilyNames { get; }
        }

        /// <summary>
        /// Walks every installed package for the two things that can identify a game: the config beside
        /// it, or failing that a manifest that declares it an Xbox Live title.
        ///
        /// The config is tried first and settles it - a package that has one has already named its
        /// product, and asking the catalogue by family name as well would be a second request for an
        /// answer in hand.
        /// </summary>
        private static async Task<PackageSweep> ReadPackagesAsync()
        {
            List<XboxGameConfig.Result> configs = new List<XboxGameConfig.Result>();
            List<string> configlessGameFamilyNames = new List<string>();
            IEnumerable<Package> packages;

            try
            {
                packages = new PackageManager().FindPackagesForUser(string.Empty);
            }
            catch (Exception ex)
            {
                // packageQuery not granted, or the query is unavailable - the XboxGames sweep still stands
                System.Diagnostics.Debug.WriteLine($"Could not query installed packages: {ex.Message}");

                return new PackageSweep(configs, configlessGameFamilyNames);
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

                    continue;
                }

                // No config, so this is either not a game or one of the Store's older titles that
                // predate the file. Its own manifest is the only thing left that tells the two apart.
                if (await DeclaresXboxLiveGameAsync(installFolder))
                {
                    configlessGameFamilyNames.Add(package.Id.FamilyName);
                }
            }

            return new PackageSweep(configs, configlessGameFamilyNames);
        }

        /// <summary>
        /// Whether a package's own manifest declares it an Xbox Live title, which for a package with no
        /// MicrosoftGame.config is the only claim to being a game it can make.
        ///
        /// Reached for every installed package that had no config, which on an ordinary machine is a
        /// hundred manifests written by a hundred different publishers. So anything at all that goes
        /// wrong reading one is that package not being a game - the same rule
        /// <see cref="Xbox.ImageCacheIndex.BuildAsync"/> applies to a cached file it cannot decode.
        /// Letting a single unreadable manifest out of here would take the entire first-party section
        /// with it, and the games are the packages that read fine.
        /// </summary>
        /// <param name="folder">An installed package's folder.</param>
        internal static async Task<bool> DeclaresXboxLiveGameAsync(StorageFolder folder)
        {
            try
            {
                StorageFile file = await folder.GetFileAsync(PackageManifest.FileName);

                return PackageManifest.DeclaresXboxLiveGame(await FileIO.ReadTextAsync(file));
            }
            catch (Exception ex) when (!(ex is FileNotFoundException))
            {
                // FileNotFoundException is filtered out above rather than logged: a package with no
                // manifest to read is the ordinary answer here, not something that went wrong
                System.Diagnostics.Debug.WriteLine($"Could not read {PackageManifest.FileName} in {folder.Path}: {ex.Message}");

                return false;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }
    }
}
