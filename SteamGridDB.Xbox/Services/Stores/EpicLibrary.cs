using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Windows.Data.Json;

using SteamGridDB.Xbox.Services;
using Windows.Storage;

namespace SteamGridDB.Xbox.Services.Stores
{
    /// <summary>
    /// Reads game names out of the Epic launcher's own install manifests.
    ///
    /// The Xbox app records an Epic game as "epic:namespace:catalogItemId:appName" and nothing else -
    /// no title. For most games the appName is readable enough for SteamGridDB to match on, but Epic
    /// assigns some titles an opaque GUID instead (Alan Wake 2 is "dc9d2e59..."), and SteamGridDB has
    /// no entry linked to it. Those games have no name from any online source either, so they showed
    /// as "Unknown" with no way to fix them but a manual search.
    ///
    /// Epic itself has the answer sitting on disk, keyed by both identifiers.
    /// </summary>
    internal static class EpicLibrary
    {
        private static readonly string manifestFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Epic\EpicGamesLauncher\Data\Manifests");

        private static Dictionary<string, string> names;

        // The load runs from inside the per-game loop, so two entries can reach it at once
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        /// <summary>
        /// The installed game's display name, or null when Epic has no manifest for it.
        /// </summary>
        /// <param name="appName">Epic app name - the last segment of the Xbox entry ID.</param>
        /// <param name="catalogItemId">Epic catalog item ID - the third segment.</param>
        public static async Task<string> GetDisplayNameAsync(string appName, string catalogItemId)
        {
            Dictionary<string, string> map = await LoadAsync();

            if (!string.IsNullOrEmpty(appName) && map.TryGetValue(appName, out string byAppName))
            {
                return byAppName;
            }

            if (!string.IsNullOrEmpty(catalogItemId) && map.TryGetValue(catalogItemId, out string byCatalogItem))
            {
                return byCatalogItem;
            }

            return null;
        }

        private static async Task<Dictionary<string, string>> LoadAsync()
        {
            if (names != null)
            {
                return names;
            }

            await gate.WaitAsync();

            try
            {
                if (names != null)
                {
                    return names;
                }

                return names = await ReadManifestsAsync();
            }
            finally
            {
                gate.Release();
            }
        }

        private static async Task<Dictionary<string, string>> ReadManifestsAsync()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(manifestFolder);

                foreach (StorageFile file in await folder.GetFilesAsync())
                {
                    if (!file.Name.EndsWith(".item", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!JsonObject.TryParse(await FileIO.ReadTextAsync(file), out JsonObject manifest))
                    {
                        continue;
                    }

                    string displayName = JsonRead.String(manifest, "DisplayName");

                    if (string.IsNullOrEmpty(displayName))
                    {
                        continue;
                    }

                    // Indexed under both identifiers, because which one the Xbox entry is keyed on
                    // depends on how the entry was parsed
                    foreach (string key in new[] { JsonRead.String(manifest, "AppName"), JsonRead.String(manifest, "CatalogItemId") })
                    {
                        if (!string.IsNullOrEmpty(key))
                        {
                            map[key] = displayName;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Epic not installed, or the folder is unreadable - names simply stay unresolved
                System.Diagnostics.Debug.WriteLine($"Could not read Epic manifests: {ex.Message}");
            }

            return map;
        }
    }
}
