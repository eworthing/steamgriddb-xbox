using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Windows.Data.Xml.Dom;
using Windows.Storage;

using SteamGridDB.Xbox.Services;

namespace SteamGridDB.Xbox.Services.Stores
{
    /// <summary>
    /// Reads game names out of the EA app's own per-game install manifests.
    ///
    /// The Xbox app records an EA game as "ea:&lt;contentId&gt;" and nothing else - no title - and
    /// SteamGridDB cannot resolve that ID either: its "origin" platform is keyed by Origin offer IDs
    /// ("Origin.OFR.50.0002694", "OFB-EAST:109552316"), never by the numeric content ID the Xbox app
    /// carries. So the platform-ID lookup misses for every EA entry, not just some - the name stays
    /// "Unknown", and the SteamGridDB name search that rescues GOG/Epic/Ubisoft entries is skipped,
    /// because it is only attempted once something has produced a name to search for.
    ///
    /// EA writes the answer into every installed game's own directory:
    /// &lt;install dir&gt;\__Installer\installerdata.xml carries both the contentID the Xbox app keys
    /// on and the game's title. That title is also the plain one - "Plants vs Zombies Battle for
    /// Neighborville" - where the Uninstall and "Origin Games" registry entries carry the store's
    /// edition-suffixed spelling ("... Deluxe Edition"), which no longer matches SteamGridDB's own.
    ///
    /// There is deliberately no online fallback, unlike <see cref="EpicLibrary"/>'s. EA's public
    /// catalogue API (api1/api2.origin.com), which is what every other implementation used to resolve
    /// these names, now answers every request with "Origin has shut down"; Playnite removed its Origin
    /// plugin over exactly this in October 2025. One consequence is worth knowing: only *installed* EA
    /// games resolve a name here, because the manifest lives in the install directory. An entry the
    /// Xbox app kept for an uninstalled EA game still shows as "Unknown", and nothing can currently fix
    /// that but a manual search.
    /// </summary>
    internal static class EaLibrary
    {
        // Not Environment.SpecialFolder.CommonApplicationData: inside an app container that resolves to
        // the app's own LocalState, so it would build a path within the sandbox that can never exist.
        // The environment variable is not redirected. Same reason, same fix as EpicLibrary's.
        private static readonly string machineIniPath = Path.Combine(
            Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData",
            @"EA Desktop\machine.ini");

        /// <summary>
        /// Where the EA app installs games when machine.ini names no location - its own out-of-the-box
        /// default, and the only guess worth making.
        /// </summary>
        internal const string DefaultInstallRoot = @"C:\Program Files\EA Games";

        /// <summary>
        /// The machine.ini key holding the configured install root.
        /// </summary>
        private const string InstallRootKey = "machine.downloadinplacedir";

        /// <summary>
        /// What the manifest read found, for the library-load log. A name that fails to resolve is
        /// indistinguishable from the EA app not being installed unless this says which - the same
        /// reason <see cref="EpicLibrary.LoadSummary"/> exists.
        /// </summary>
        public static string LoadSummary { get; private set; } = "not read yet";

        // The load runs from inside the per-game loop, so two entries can reach it at once
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        private static readonly AsyncLazyCache<Dictionary<string, string>> nameCache =
            new AsyncLazyCache<Dictionary<string, string>>(gate, LoadInstalledNamesAsync);

        /// <summary>
        /// The installed game's display name, or null when no EA manifest claims that content ID.
        /// </summary>
        /// <param name="contentId">EA content ID - everything after "ea:" in the Xbox entry ID.</param>
        public static async Task<string> GetDisplayNameAsync(string contentId)
        {
            Dictionary<string, string> map = await nameCache.GetOrLoadAsync();

            return !string.IsNullOrEmpty(contentId) && map.TryGetValue(contentId, out string name)
                ? name
                : null;
        }

        /// <summary>
        /// One installed game's manifest, reduced to the two things this needs from it.
        /// </summary>
        internal readonly struct InstallerManifest
        {
            internal InstallerManifest(string title, IReadOnlyList<string> contentIds)
            {
                Title = title;
                ContentIds = contentIds;
            }

            /// <summary>The game's title, or null when the manifest carries none.</summary>
            internal string Title { get; }

            /// <summary>Every content ID the manifest claims. Never null; empty when it claims none.</summary>
            internal IReadOnlyList<string> ContentIds { get; }
        }

        /// <summary>
        /// The install root machine.ini configures, or null when it names none.
        ///
        /// machine.ini is flat "key=value" lines with no sections. Values are split on the first '='
        /// only: several of them (the telemetry and update-info keys) are JSON documents containing
        /// more of them, and splitting on all would truncate any value that happens to sit next to one.
        /// </summary>
        /// <param name="machineIniText">machine.ini's full text.</param>
        internal static string ParseInstallRoot(string machineIniText)
        {
            if (string.IsNullOrEmpty(machineIniText))
            {
                return null;
            }

            foreach (string line in machineIniText.Split('\n'))
            {
                int separator = line.IndexOf('=');

                if (separator <= 0)
                {
                    continue;
                }

                if (!string.Equals(line.Substring(0, separator).Trim(), InstallRootKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // EA writes this with a trailing separator ("C:\Program Files\EA Games\"), which
                // StorageFolder.GetFolderFromPathAsync rejects. Trimming it off a drive root would
                // leave "C:", which means something else entirely, so that one keeps its separator.
                string value = line.Substring(separator + 1).Trim();
                string trimmed = value.TrimEnd('\\', '/');

                return trimmed.Length == 0 || trimmed.EndsWith(":", StringComparison.Ordinal) ? value : trimmed;
            }

            return null;
        }

        /// <summary>
        /// Pulls the title and content IDs out of one installerdata.xml.
        ///
        /// The English title is preferred over the other locales the manifest carries, because it is
        /// SteamGridDB the name is going to be searched against and SteamGridDB names games in English.
        /// A manifest with no en_US title falls back to the first one it does carry, which still beats
        /// leaving the entry as "Unknown".
        /// </summary>
        /// <param name="xml">installerdata.xml's full text.</param>
        /// <returns>The manifest's title and content IDs; both empty when the XML will not parse.</returns>
        internal static InstallerManifest ParseInstallerManifest(string xml)
        {
            List<string> contentIds = new List<string>();

            if (string.IsNullOrEmpty(xml))
            {
                return new InstallerManifest(null, contentIds);
            }

            XmlDocument document = new XmlDocument();

            try
            {
                document.LoadXml(xml);
            }
            catch (Exception ex)
            {
                // A manifest this app cannot read is one game left unnamed, not a failed load
                System.Diagnostics.Debug.WriteLine($"Could not parse an EA installer manifest: {ex.Message}");

                return new InstallerManifest(null, contentIds);
            }

            foreach (IXmlNode node in document.SelectNodes("//contentIDs/contentID"))
            {
                string contentId = node.InnerText?.Trim();

                if (!string.IsNullOrEmpty(contentId))
                {
                    contentIds.Add(contentId);
                }
            }

            string title = null;

            foreach (IXmlNode node in document.SelectNodes("//gameTitles/gameTitle"))
            {
                string candidate = node.InnerText?.Trim();

                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                bool isEnglish = string.Equals(
                    node.Attributes?.GetNamedItem("locale")?.NodeValue?.ToString(),
                    "en_US",
                    StringComparison.OrdinalIgnoreCase);

                if (isEnglish)
                {
                    return new InstallerManifest(candidate, contentIds);
                }

                title = title ?? candidate;
            }

            return new InstallerManifest(title, contentIds);
        }

        /// <summary>
        /// Indexes every installed game under an install root by each content ID its manifest claims.
        ///
        /// Takes the folder rather than finding it, so this - the part that walks real directories and
        /// reads real files - is exercised against a throwaway directory in the tests, while only the
        /// step that locates the real one stays uncovered.
        /// </summary>
        /// <param name="installRoot">The folder EA installs games into.</param>
        internal static async Task<Dictionary<string, string>> ReadInstallerManifestsAsync(StorageFolder installRoot)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (StorageFolder gameFolder in await installRoot.GetFoldersAsync())
            {
                InstallerManifest manifest;

                try
                {
                    StorageFolder installerFolder = await gameFolder.GetFolderAsync("__Installer");
                    StorageFile manifestFile = await installerFolder.GetFileAsync("installerdata.xml");

                    manifest = ParseInstallerManifest(await FileIO.ReadTextAsync(manifestFile));
                }
                catch (FileNotFoundException)
                {
                    // Not an EA game directory, or one mid-install - neither is an error
                    continue;
                }

                if (string.IsNullOrEmpty(manifest.Title))
                {
                    continue;
                }

                // Indexed under every content ID the manifest claims, because which one the Xbox entry
                // is keyed on is the store's choice, not this app's
                foreach (string contentId in manifest.ContentIds)
                {
                    map[contentId] = manifest.Title;
                }
            }

            return map;
        }

        /// <summary>
        /// Locates the install root and reads every manifest under it. Runs at most once, through
        /// <see cref="nameCache"/>.
        /// </summary>
        private static async Task<Dictionary<string, string>> LoadInstalledNamesAsync()
        {
            string installRoot = DefaultInstallRoot;

            try
            {
                installRoot = await ReadConfiguredInstallRootAsync() ?? DefaultInstallRoot;

                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(installRoot);
                Dictionary<string, string> map = await ReadInstallerManifestsAsync(folder);

                LoadSummary = $"{map.Count} content id{(map.Count == 1 ? string.Empty : "s")} from {installRoot}";

                return map;
            }
            catch (Exception ex)
            {
                // EA app not installed, or the folder is unreadable - names simply stay unresolved
                LoadSummary = $"{ex.GetType().Name} reading {installRoot}: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Could not read EA install manifests: {ex.Message}");

                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// The install root the EA app records in machine.ini, or null when it does not say - in which
        /// case <see cref="DefaultInstallRoot"/> is still worth trying, since it is where the EA app
        /// puts games until someone changes it.
        /// </summary>
        private static async Task<string> ReadConfiguredInstallRootAsync()
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(machineIniPath);

                return ParseInstallRoot(await FileIO.ReadTextAsync(file));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not read {machineIniPath}: {ex.Message}");

                return null;
            }
        }
    }
}
