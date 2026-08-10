using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Storage;

namespace SteamGridDB.Xbox.Services.Xbox
{
    /// <summary>
    /// Remembers which cached images belong to which first-party game.
    ///
    /// This is not a cache that can be rebuilt. Renditions are discovered by comparing the cached
    /// images against the artwork the Store says the tile came from - so the moment a rendition is
    /// overwritten with someone's chosen art, it stops resembling what would find it, and the game
    /// becomes undiscoverable. The record written here is the only way back to those files, which is
    /// why it is saved the instant discovery succeeds and before any bytes are applied.
    ///
    /// It also carries the backups. The Xbox app enumerates its own cache folder and removes files it
    /// did not put there, so the .bak and .new siblings cannot sit beside the images the way they do
    /// for third-party tiles; <see cref="VaultFolderAsync"/> is where they go instead. Their names are
    /// unchanged, because the app names cached files by a hash that is unique across the whole cache.
    /// </summary>
    internal static class XboxTileStore
    {
        private const string fileName = "xbox-tiles.json";

        /// <summary>Folder holding the .bak and .new siblings of every first-party tile.</summary>
        private const string vaultFolderName = "XboxTiles";

        // Same shape and the same reasoning as AppliedArtworkStore's gate: writes are rare, but a bulk
        // fix and a per-row button can both reach here, and a half-written record loses the only route
        // back to a game's renditions.
        private static readonly JsonRecordStore<List<string>> store = new JsonRecordStore<List<string>>(
            fileName, "the Xbox tile record", ReadRenditions, WriteRenditions);

        private static StorageFolder vaultFolder;

        /// <summary>
        /// Where the record and the vault are kept. Defaults to the widget's own local data.
        ///
        /// Settable for the same reason <see cref="Artwork.AppliedArtworkStore.RecordFolder"/> is:
        /// ApplicationData.Current only resolves inside an app container, and that is the only thing
        /// stopping this type being exercised outside one.
        /// </summary>
        internal static StorageFolder RecordFolder
        {
            get => store.RecordFolder;

            set
            {
                vaultFolder = null;
                store.RecordFolder = value;
            }
        }

        /// <summary>
        /// The folder holding first-party backups, created on first use and remembered after it.
        ///
        /// Every <see cref="XboxTiles"/> entry point opens with this, and a library load reaches two of
        /// them per game, so resolving it once matters more than the one-line call site suggests. No
        /// lock: two callers racing both get the same folder, because OpenIfExists is what creation
        /// means here.
        /// </summary>
        internal static async Task<StorageFolder> VaultFolderAsync()
        {
            return vaultFolder ?? (vaultFolder =
                await RecordFolder.CreateFolderAsync(vaultFolderName, CreationCollisionOption.OpenIfExists));
        }

        /// <summary>
        /// The name of every file in the vault, for a pass that is about to ask after many of them.
        ///
        /// The same trade <c>XboxLibrary.CacheFileNamesAsync</c> makes for the image cache, and
        /// for the same reason: the questions <see cref="XboxTiles"/> asks the vault - has this
        /// rendition a backup, has it a saved customisation - are questions about a name existing, and
        /// answering them one <c>GetFileAsync</c> at a time costs a brokered call per rendition per
        /// game per load, nearly all of which miss and so pay for a thrown FileNotFoundException as
        /// well.
        ///
        /// Names only, deliberately. Fetching each file's properties here would put back most of the
        /// round trips this exists to remove; the two sizes that are genuinely needed are read lazily,
        /// and only for the renditions this listing says have a customisation saved at all - which on
        /// a library nobody has customised is none of them.
        ///
        /// A snapshot, so it is only good for as long as nothing writes to the vault. That holds for
        /// the passes that use it: applying, restoring and discarding are all single-game actions the
        /// library-operation guard keeps from overlapping a load.
        /// </summary>
        internal static async Task<HashSet<string>> VaultFileNamesAsync()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (StorageFile file in await (await VaultFolderAsync()).GetFilesAsync())
            {
                names.Add(file.Name);
            }

            return names;
        }

        /// <summary>
        /// The cached images known to be this game's tile, largest first, or null when it has never
        /// been discovered.
        ///
        /// Returns the map's own <see cref="List{T}"/> by reference, not a copy - cheap for the common
        /// case of reading it straight back out, but it means a caller that mutated the list and handed
        /// that same instance to <see cref="SetAsync"/> would be comparing the map's entry to itself and
        /// silently skip the write. No current caller does this: every value reaching SetAsync is a
        /// freshly built list.
        /// </summary>
        /// <param name="storeId">The game's Microsoft Store product ID.</param>
        internal static async Task<IReadOnlyList<string>> GetAsync(string storeId)
        {
            if (string.IsNullOrEmpty(storeId))
            {
                return null;
            }

            return await store.ReadAsync(map =>
                map.TryGetValue(storeId, out List<string> r) ? (IReadOnlyList<string>)r : null);
        }

        /// <summary>
        /// Every cached image this record claims, across all games it knows - installed or not.
        ///
        /// Deliberately the whole record rather than the games a load happened to find. A game the
        /// Xbox app has since uninstalled keeps its entry here, and its renditions are still this
        /// widget's: the only caller uses this to decide which images are <em>not</em> accounted for,
        /// and answering that from the installed subset would call an uninstalled game's tiles unknown.
        /// </summary>
        internal static async Task<HashSet<string>> AllRenditionsAsync()
        {
            return await store.ReadAsync(map =>
            {
                var all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pair in map)
                {
                    foreach (string fileName in pair.Value)
                    {
                        all.Add(fileName);
                    }
                }

                return all;
            });
        }

        /// <summary>
        /// Records the cached images making up a game's tile.
        /// </summary>
        /// <param name="storeId">The game's Microsoft Store product ID.</param>
        /// <param name="renditionFileNames">Cache file names, largest first.</param>
        internal static async Task SetAsync(string storeId, IEnumerable<string> renditionFileNames)
        {
            if (string.IsNullOrEmpty(storeId))
            {
                return;
            }

            List<string> renditions = renditionFileNames?.Where(n => !string.IsNullOrEmpty(n)).ToList()
                ?? new List<string>();

            if (renditions.Count == 0)
            {
                return;
            }

            await store.UpdateAsync(map =>
            {
                if (map.TryGetValue(storeId, out List<string> known)
                    && known.SequenceEqual(renditions, StringComparer.OrdinalIgnoreCase))
                {
                    return false;
                }

                map[storeId] = renditions;

                return true;
            });
        }

        /// <summary>
        /// Forgets a game, for when its renditions no longer exist and it has to be discovered again.
        /// </summary>
        /// <param name="storeId">The game's Microsoft Store product ID.</param>
        internal static async Task ClearAsync(string storeId)
        {
            if (string.IsNullOrEmpty(storeId))
            {
                return;
            }

            await store.UpdateAsync(map => map.Remove(storeId));
        }

        // A member that is not an array is refused outright; one that is an array but yields no usable
        // string is refused too, rather than kept as an empty list - an entry with no renditions
        // describes nothing and is exactly what SetAsync already declines to write.
        private static bool ReadRenditions(IJsonValue value, out List<string> result)
        {
            if (value.ValueType != JsonValueType.Array)
            {
                result = null;

                return false;
            }

            List<string> renditions = value.GetArray()
                .Where(v => v.ValueType == JsonValueType.String)
                .Select(v => v.GetString())
                .ToList();

            if (renditions.Count == 0)
            {
                result = null;

                return false;
            }

            result = renditions;

            return true;
        }

        private static IJsonValue WriteRenditions(List<string> renditions)
        {
            var array = new JsonArray();

            foreach (string name in renditions)
            {
                array.Add(JsonValue.CreateStringValue(name));
            }

            return array;
        }
    }
}
