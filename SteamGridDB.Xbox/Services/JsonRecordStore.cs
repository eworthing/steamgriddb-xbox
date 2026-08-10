using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Storage;

namespace SteamGridDB.Xbox.Services
{
    /// <summary>
    /// Reads one JSON member into a <typeparamref name="TValue"/>, refusing it - by returning false -
    /// when the member's own shape or its value cannot become one.
    ///
    /// A delegate rather than <c>Func&lt;IJsonValue, TValue&gt;</c> because a refusal is sometimes
    /// value-dependent rather than type-dependent: a JSON array can parse fine as an array and still be
    /// refused because it yields zero usable entries, which a value-returning Func could only express
    /// with a sentinel TValue that every caller would then have to know to check for.
    /// </summary>
    /// <typeparam name="TValue">The decoded value's type.</typeparam>
    /// <param name="value">The raw JSON value read for one entry.</param>
    /// <param name="result">The decoded value, when this returns true. Undefined otherwise.</param>
    internal delegate bool JsonValueReader<TValue>(IJsonValue value, out TValue result);

    /// <summary>
    /// The persistence envelope AppliedArtworkStore, XboxTileStore and GameMatchCache each hand-rolled
    /// before this existed: a <see cref="Dictionary{TKey, TValue}"/> loaded once from a JSON file in
    /// <see cref="RecordFolder"/>, read and written under one gate, and rewritten in full whenever a
    /// caller reports that its change actually changed something.
    ///
    /// What differs between the three is only the file name, the log description, and how one value
    /// reads from and writes to JSON - which is exactly what the constructor takes. Everything else,
    /// including the locking, is shared here.
    /// </summary>
    /// <typeparam name="TValue">One record's value. The key is always a string.</typeparam>
    internal sealed class JsonRecordStore<TValue>
    {
        private readonly string fileName;
        private readonly string description;
        private readonly JsonValueReader<TValue> readValue;
        private readonly Func<TValue, IJsonValue> writeValue;

        // Writes are rare, but a bulk operation and a per-row button can both reach the same store, and
        // a half-written file would be read back as damaged. ReadAsync and UpdateAsync both take this
        // same gate directly to serialize against each other and against the lazy load below - not a
        // second lock of their own.
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

        private AsyncLazyCache<Dictionary<string, TValue>> cache;

        private StorageFolder recordFolder;

        /// <param name="fileName">The file this record is kept under, in <see cref="RecordFolder"/>.</param>
        /// <param name="description">Names this record in the Debug log when it cannot be read or saved.</param>
        /// <param name="readValue">Decodes one JSON member into a value, or refuses it.</param>
        /// <param name="writeValue">Encodes one value back to JSON.</param>
        internal JsonRecordStore(
            string fileName,
            string description,
            JsonValueReader<TValue> readValue,
            Func<TValue, IJsonValue> writeValue)
        {
            this.fileName = fileName;
            this.description = description;
            this.readValue = readValue;
            this.writeValue = writeValue;

            cache = new AsyncLazyCache<Dictionary<string, TValue>>(gate, LoadMapFromDiskAsync);
        }

        /// <summary>
        /// Where the record is kept. Defaults to the widget's own local data, which is what it always
        /// uses in the app.
        ///
        /// Settable because ApplicationData.Current only resolves inside an app container - it is the
        /// single reason a store could not otherwise be exercised outside one. Assigning also drops the
        /// loaded map, which belongs to whichever folder it was read from.
        /// </summary>
        internal StorageFolder RecordFolder
        {
            get => recordFolder ?? ApplicationData.Current.LocalFolder;

            set
            {
                recordFolder = value;
                cache = new AsyncLazyCache<Dictionary<string, TValue>>(gate, LoadMapFromDiskAsync);
            }
        }

        /// <summary>
        /// Runs a read-only query against the loaded map, under the gate so it cannot race
        /// <see cref="UpdateAsync"/> mutating the same Dictionary instance in place.
        ///
        /// LOCK ORDER, do not reorder: the map is loaded (<c>await cache.GetOrLoadAsync()</c>) before
        /// the gate is taken (<c>await gate.WaitAsync()</c>), never the other way round.
        /// <see cref="AsyncLazyCache{T}.GetOrLoadAsync"/> takes this exact gate itself on a cold load,
        /// and <see cref="SemaphoreSlim"/> is not reentrant and has no timeout or cancellation anywhere
        /// in this path - so taking the gate before the first GetOrLoadAsync call deadlocks the app
        /// permanently the first time this store is used.
        /// </summary>
        /// <param name="query">Reads whatever is needed from the loaded map.</param>
        internal async Task<TResult> ReadAsync<TResult>(Func<Dictionary<string, TValue>, TResult> query)
        {
            Dictionary<string, TValue> map = await cache.GetOrLoadAsync();

            await gate.WaitAsync();

            try
            {
                return query(map);
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Mutates the loaded map and, when the mutation changed anything, rewrites the record in full.
        ///
        /// Same lock order as <see cref="ReadAsync{TResult}"/> - see its remarks. Getting this backwards
        /// deadlocks the app the first time it runs.
        /// </summary>
        /// <param name="change">Mutates the map, returning whether it actually changed anything - false
        /// skips the write entirely.</param>
        internal async Task UpdateAsync(Func<Dictionary<string, TValue>, bool> change)
        {
            Dictionary<string, TValue> map = await cache.GetOrLoadAsync();

            await gate.WaitAsync();

            try
            {
                if (!change(map))
                {
                    return;
                }

                var root = new JsonObject();

                foreach (var pair in map)
                {
                    root[pair.Key] = writeValue(pair.Value);
                }

                StorageFile file = await RecordFolder.CreateFileAsync(
                    fileName, CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteTextAsync(file, root.Stringify());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not save {description}: {ex.Message}");
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<Dictionary<string, TValue>> LoadMapFromDiskAsync()
        {
            var map = new Dictionary<string, TValue>(StringComparer.OrdinalIgnoreCase);

            try
            {
                StorageFile file = await RecordFolder.GetFileAsync(fileName);
                string json = await FileIO.ReadTextAsync(file);

                if (JsonObject.TryParse(json, out JsonObject root))
                {
                    foreach (var pair in root)
                    {
                        if (readValue(pair.Value, out TValue value))
                        {
                            map[pair.Key] = value;
                        }
                    }
                }
            }
            catch (System.IO.FileNotFoundException)
            {
                // Nothing recorded yet
            }
            catch (Exception ex)
            {
                // A damaged record is not worth failing a library load over - start again
                System.Diagnostics.Debug.WriteLine($"Could not read {description}: {ex.Message}");
            }

            return map;
        }
    }
}
