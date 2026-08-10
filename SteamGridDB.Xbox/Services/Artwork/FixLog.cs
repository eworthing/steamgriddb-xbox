using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Windows.Storage;

namespace SteamGridDB.Xbox.Services.Artwork
{
    /// <summary>
    /// A record of what the last "fix library" run did, written to last-fix.log in the widget's local
    /// data.
    ///
    /// Artwork selection makes a chain of decisions per game - which candidates came back, which one
    /// ranking chose, whether the official-artwork gate could run and what it concluded - and every one
    /// of them was previously invisible. The gate failed on an entire library while looking exactly
    /// like a gate that had simply decided it had nothing to do, and the only way that was found was by
    /// diffing the artwork IDs on disk against a separate model of what should have happened.
    ///
    /// Lines are held in memory and written once at the end of a run, so a 150-game pass costs one
    /// file write.
    /// </summary>
    internal static class FixLog
    {
        // Start/Write are synchronous and only ever mutate lines/fileName together, so a plain lock
        // guards them - unlike StoreNameLookup's or SteamGridDbClient's gates, nothing here holds the
        // lock across an await. SaveAsync takes a point-in-time copy under the same lock, then does
        // its file I/O outside it: a lock statement cannot itself wrap an await.
        private static readonly object syncRoot = new object();

        private static readonly List<string> lines = new List<string>();

        /// <summary>The file a run is written to when the caller does not name one of its own.</summary>
        private const string DefaultFileName = "last-fix.log";

        private static string fileName = DefaultFileName;

        private static StorageFolder logFolder;

        /// <summary>
        /// Where the log is written. Defaults to the widget's own local data, which is what it always
        /// uses in the app; settable because ApplicationData.Current only resolves inside an app
        /// container.
        /// </summary>
        internal static StorageFolder LogFolder
        {
            get => logFolder ?? ApplicationData.Current.LocalFolder;
            set => logFolder = value;
        }

        /// <summary>
        /// Begins a new run, discarding whatever the previous one recorded.
        /// </summary>
        /// <param name="what">Short description of the operation, for the header.</param>
        /// <param name="file">File to write to, so a library load and a fix do not overwrite each other.</param>
        public static void Start(string what, string file = DefaultFileName)
        {
            lock (syncRoot)
            {
                fileName = file;
                lines.Clear();
                lines.Add($"{what} - {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
            }
        }

        /// <summary>
        /// Records one line against the run.
        /// </summary>
        public static void Write(string line)
        {
            lock (syncRoot)
            {
                lines.Add(line);
            }
        }

        /// <summary>
        /// Writes the run to disk.
        /// </summary>
        public static async Task SaveAsync()
        {
            string snapshotFileName;
            List<string> snapshotLines;

            lock (syncRoot)
            {
                snapshotFileName = fileName;
                snapshotLines = new List<string>(lines);
            }

            try
            {
                StorageFile file = await LogFolder.CreateFileAsync(
                    snapshotFileName, CreationCollisionOption.ReplaceExisting);

                await FileIO.WriteLinesAsync(file, snapshotLines);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not write the fix log: {ex.Message}");
            }
        }
    }
}
