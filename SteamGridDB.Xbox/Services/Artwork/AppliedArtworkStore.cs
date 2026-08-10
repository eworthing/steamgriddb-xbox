using System;
using System.Collections.Generic;
using System.Linq;
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

        // Loaded once and written through. The widget is the only writer, and a Game Bar widget has a
        // single instance, so there is no reconciling to do against another process.
        private static readonly JsonRecordStore<int> store = new JsonRecordStore<int>(
            fileName, "applied artwork", ReadArtworkId, artworkId => JsonValue.CreateNumberValue(artworkId));

        /// <summary>
        /// Where the record is kept. Defaults to the widget's own local data, which is what it always
        /// uses in the app.
        ///
        /// Settable because ApplicationData.Current only resolves inside an app container - it is the
        /// single reason this type could not otherwise be exercised outside one. Assigning also drops
        /// the loaded map, which belongs to whichever folder it was read from.
        /// </summary>
        internal static StorageFolder RecordFolder
        {
            get => store.RecordFolder;
            set => store.RecordFolder = value;
        }

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

            return await store.ReadAsync(map =>
                map.TryGetValue(Key(imageFilePath), out int id) ? id : (int?)null);
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

            string key = Key(imageFilePath);

            await store.UpdateAsync(map =>
            {
                if (map.TryGetValue(key, out int existing) && existing == artworkId)
                {
                    return false;
                }

                map[key] = artworkId;

                return true;
            });
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

            await store.UpdateAsync(map => map.Remove(Key(imageFilePath)));
        }

        /// <summary>
        /// Forgets every record the caller judges orphaned.
        ///
        /// The predicate decides; this only owns the walk and the write. That split is deliberate -
        /// the judgement is about which images still belong to a game, which is
        /// <see cref="Xbox.XboxTiles.IsOrphanedRecord"/>'s business and is covered on its own, while
        /// this side is the locking and the file that a bulk removal must not get wrong.
        ///
        /// A sweep that finds nothing does not write. This runs on every library load, and rewriting an
        /// untouched record each time would be a pointless chance to corrupt it.
        /// </summary>
        /// <param name="isOrphaned">Given a record's key, whether it should go.</param>
        internal static async Task ForgetWhereAsync(Func<string, bool> isOrphaned)
        {
            if (isOrphaned == null)
            {
                return;
            }

            await store.UpdateAsync(map =>
            {
                List<string> orphaned = map.Keys.Where(isOrphaned).ToList();

                foreach (string key in orphaned)
                {
                    map.Remove(key);
                }

                return orphaned.Count > 0;
            });
        }

        private static string Key(string imageFilePath)
        {
            return imageFilePath.ToLowerInvariant();
        }

        private static bool ReadArtworkId(IJsonValue value, out int result)
        {
            if (value.ValueType == JsonValueType.Number)
            {
                result = (int)value.GetNumber();

                return true;
            }

            result = 0;

            return false;
        }
    }
}
