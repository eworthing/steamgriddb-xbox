using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.Storage.Streams;

using SteamGridDB.Xbox.Services.Artwork;

namespace SteamGridDB.Xbox.Services.Xbox
{
    /// <summary>
    /// Applies, restores and re-applies artwork across every cached rendition of one first-party game.
    ///
    /// A third-party game is one file, so the widget's whole pipeline is built around one. A
    /// first-party game is several - the Xbox app fetched its artwork once per surface size - and the
    /// tile only changes everywhere if all of them do. This is where that fan-out lives, so nothing
    /// above it has to know a game can be more than one file.
    ///
    /// Each rendition is re-encoded to its own size, read from the file that is already there. The size
    /// is not recorded anywhere and not guessed: a rendition the Xbox app has since deleted is skipped
    /// rather than recreated, because the app tracks its cache in a database of its own and a file it
    /// has no row for is one it will remove again.
    /// </summary>
    internal static class XboxTiles
    {
        /// <summary>
        /// Writes artwork over every rendition of a game's tile, preserving the Xbox app's originals the
        /// first time.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder.</param>
        /// <param name="renditionFileNames">The game's cached images, largest first.</param>
        /// <param name="artworkBytes">The artwork to apply, in any format a decoder can read.</param>
        /// <returns>How many renditions were written, and whether backups of the originals now exist.</returns>
        internal static async Task<(int Written, bool HasBackup)> ApplyAsync(
            StorageFolder cacheFolder,
            IReadOnlyList<string> renditionFileNames,
            IBuffer artworkBytes)
        {
            StorageFolder vault = await XboxTileStore.VaultFolderAsync();
            int written = 0;
            bool anyBackup = false;

            foreach (string fileName in renditionFileNames ?? new List<string>())
            {
                int size = await RenditionSizeAsync(cacheFolder, fileName);

                if (size <= 0)
                {
                    continue;
                }

                IBuffer tileBytes = await TileImage.EncodeSquareJpegAsync(artworkBytes, size);

                if (tileBytes == null)
                {
                    continue;
                }

                anyBackup |= await ArtworkFiles.ApplyEncodedAsync(cacheFolder, fileName, vault, tileBytes);
                written++;
            }

            return (written, anyBackup);
        }

        /// <summary>
        /// Puts the Xbox app's own artwork back across every rendition.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder.</param>
        /// <param name="renditionFileNames">The game's cached images.</param>
        /// <returns>Restored when at least one original was put back, BackupMissing when none was.</returns>
        internal static async Task<ArtworkFiles.RestoreOutcome> RestoreAsync(
            StorageFolder cacheFolder,
            IReadOnlyList<string> renditionFileNames)
        {
            StorageFolder vault = await XboxTileStore.VaultFolderAsync();
            bool restoredAny = false;

            foreach (string fileName in renditionFileNames ?? new List<string>())
            {
                ArtworkFiles.RestoreOutcome outcome = await ArtworkFiles.RestoreOriginalAsync(cacheFolder, fileName, vault);

                restoredAny |= outcome == ArtworkFiles.RestoreOutcome.Restored;
            }

            return restoredAny ? ArtworkFiles.RestoreOutcome.Restored : ArtworkFiles.RestoreOutcome.BackupMissing;
        }

        /// <summary>
        /// Puts saved customisations back over any rendition the Xbox app has overwritten.
        ///
        /// Run on every library load, because for first-party tiles being overwritten is the expected
        /// course of events rather than an accident: the app re-downloads a cached image whenever the
        /// file goes missing or its ninety-day lifetime runs out, and it keeps no checksum, size or
        /// validator that would let it notice the file had been replaced in the meantime.
        ///
        /// Renditions that already hold the customisation are left alone, so a load that has nothing to
        /// do writes nothing.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder.</param>
        /// <param name="renditionFileNames">The game's cached images.</param>
        /// <returns>How many renditions had to be put back.</returns>
        internal static async Task<int> ReapplyOverwrittenAsync(
            StorageFolder cacheFolder,
            IReadOnlyList<string> renditionFileNames)
        {
            StorageFolder vault = await XboxTileStore.VaultFolderAsync();
            int reapplied = 0;

            foreach (string fileName in renditionFileNames ?? new List<string>())
            {
                if (await MatchesSavedCustomisationAsync(cacheFolder, vault, fileName))
                {
                    continue;
                }

                if (await ArtworkFiles.ReapplyCustomisationAsync(cacheFolder, fileName, vault)
                    == ArtworkFiles.ReapplyOutcome.Reapplied)
                {
                    reapplied++;
                }
            }

            return reapplied;
        }

        /// <summary>
        /// Whether this game has backups, and so can be reverted to the Xbox app's own artwork.
        /// </summary>
        /// <param name="renditionFileNames">The game's cached images.</param>
        internal static async Task<bool> HasBackupAsync(IReadOnlyList<string> renditionFileNames)
        {
            StorageFolder vault = await XboxTileStore.VaultFolderAsync();

            foreach (string fileName in renditionFileNames ?? new List<string>())
            {
                if (await ArtworkFiles.HasBackupAsync(vault, fileName))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The pixel size of a cached rendition, or 0 when it is gone or unreadable.
        /// </summary>
        private static async Task<int> RenditionSizeAsync(StorageFolder cacheFolder, string fileName)
        {
            IBuffer bytes = await ReadIfPresentAsync(cacheFolder, fileName);

            return await TileImage.WithDecoderAsync(
                bytes,
                decoder => Task.FromResult((int)decoder.PixelWidth),
                0,
                $"Could not measure cached rendition {fileName}");
        }

        /// <summary>
        /// Whether a rendition already holds the bytes that were last applied to it.
        ///
        /// Compared by length rather than content: both files were written by this app in the same call,
        /// so they are either the identical buffer or the Xbox app has replaced one of them with a
        /// download that has no reason to match its size.
        /// </summary>
        private static async Task<bool> MatchesSavedCustomisationAsync(StorageFolder cacheFolder, StorageFolder vault, string fileName)
        {
            IBuffer current = await ReadIfPresentAsync(cacheFolder, fileName);
            IBuffer saved = await ReadIfPresentAsync(vault, ArtworkFiles.CustomisedNameFor(fileName));

            return current != null && saved != null && current.Length == saved.Length;
        }

        /// <summary>
        /// A file's bytes, or null when it is not there.
        /// </summary>
        private static async Task<IBuffer> ReadIfPresentAsync(StorageFolder folder, string fileName)
        {
            try
            {
                return await FileIO.ReadBufferAsync(await folder.GetFileAsync(fileName));
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
