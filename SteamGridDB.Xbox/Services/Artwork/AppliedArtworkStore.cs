using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Storage;

namespace SteamGridDB.Xbox.Services.Artwork
{
    /// <summary>
    /// Remembers which SteamGridDB artwork was applied to each image the widget has written.
    ///
    /// Nothing else knows this. The tile on disk is just a PNG - once written, the artwork it came
    /// from is unrecoverable, so the picker cannot show which one is in use, re-fixing a library
    /// silently reshuffles picks whenever SteamGridDB's ordering shifts, and comparing one run
    /// against another means rebuilding the whole comparison from scratch every time.
    ///
    /// Keyed by image path because that is what the bulk operations already deduplicate on: stale
    /// Xbox app manifests list one image under several entries.
    /// </summary>
    internal static class AppliedArtworkStore
    {
        private const string fileName = "applied-artwork.json";

        // Writes are rare - one per artwork applied - but a bulk operation and a per-row button can
        // both reach here, and a half-written file would be read back as damaged. GetAsync and
        // UpdateAsync both take this same gate directly (below) to serialize against each other and
        // against the lazy load - not a second lock of their own.
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        // Loaded once and written through. The widget is the only writer, and a Game Bar widget has a
        // single instance, so there is no reconciling to do against another process.
        private static readonly AsyncLazyCache<Dictionary<string, int>> appliedCache =
            new AsyncLazyCache<Dictionary<string, int>>(gate, LoadMapFromDiskAsync);

        /// <summary>
        /// The artwork applied to an image, or null when the widget did not write it or the record
        /// predates this store.
        /// </summary>
        /// <param name="imageFilePath">Full path of the tile image.</param>
        public static async Task<int?> GetAsync(string imageFilePath)
        {
            if (string.IsNullOrEmpty(imageFilePath))
            {
                return null;
            }

            Dictionary<string, int> map = await appliedCache.GetOrLoadAsync();

            // UpdateAsync holds `gate` while it mutates this same Dictionary instance in place; a read
            // that skipped the gate could race that mutation. Same lock, read or write.
            await gate.WaitAsync();

            try
            {
                return map.TryGetValue(Key(imageFilePath), out int id) ? id : (int?)null;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Records the artwork now applied to an image.
        /// </summary>
        /// <param name="imageFilePath">Full path of the tile image.</param>
        /// <param name="artworkId">SteamGridDB ID of the artwork written there.</param>
        public static async Task SetAsync(string imageFilePath, int artworkId)
        {
            if (string.IsNullOrEmpty(imageFilePath) || artworkId <= 0)
            {
                return;
            }

            await UpdateAsync(map => map[Key(imageFilePath)] = artworkId);
        }

        /// <summary>
        /// Forgets an image, for when its original is restored or it reverts to the Xbox app's own art.
        /// </summary>
        /// <param name="imageFilePath">Full path of the tile image.</param>
        public static async Task ClearAsync(string imageFilePath)
        {
            if (string.IsNullOrEmpty(imageFilePath))
            {
                return;
            }

            await UpdateAsync(map => map.Remove(Key(imageFilePath)));
        }

        private static string Key(string imageFilePath)
        {
            return imageFilePath.ToLowerInvariant();
        }

        private static async Task<Dictionary<string, int>> LoadMapFromDiskAsync()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            try
            {
                StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync(fileName);
                string json = await FileIO.ReadTextAsync(file);

                if (JsonObject.TryParse(json, out JsonObject root))
                {
                    foreach (var pair in root)
                    {
                        if (pair.Value.ValueType == JsonValueType.Number)
                        {
                            map[pair.Key] = (int)pair.Value.GetNumber();
                        }
                    }
                }
            }
            catch (System.IO.FileNotFoundException)
            {
                // Nothing applied yet
            }
            catch (Exception ex)
            {
                // A damaged record is not worth failing a library load over - start again
                System.Diagnostics.Debug.WriteLine($"Could not read applied artwork: {ex.Message}");
            }

            return map;
        }

        private static async Task UpdateAsync(Action<Dictionary<string, int>> change)
        {
            Dictionary<string, int> map = await appliedCache.GetOrLoadAsync();

            await gate.WaitAsync();

            try
            {
                change(map);

                var root = new JsonObject();

                foreach (var pair in map)
                {
                    root[pair.Key] = JsonValue.CreateNumberValue(pair.Value);
                }

                StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync(
                    fileName, CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteTextAsync(file, root.Stringify());
            }
            catch (Exception ex)
            {
                // Losing the record costs the picker its marker, not the artwork
                System.Diagnostics.Debug.WriteLine($"Could not save applied artwork: {ex.Message}");
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
