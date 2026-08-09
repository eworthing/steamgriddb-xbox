using System;
using System.IO;
using System.Threading.Tasks;

using Windows.Security.Cryptography;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SteamGridDB.Xbox.Services.Artwork
{
    /// <summary>
    /// The three files a customised game keeps side by side in the Xbox app's own image folder, and
    /// the only code that moves them around.
    ///
    /// <list type="bullet">
    /// <item>the image itself, named by the Xbox app and always .png</item>
    /// <item>.bak - the Xbox app's original, written once and never again, so reverting is possible</item>
    /// <item>.new - a copy of the applied artwork, so a customisation the Xbox app overwrites can be put back</item>
    /// </list>
    ///
    /// The two sidecars do not have to live beside the image. First-party tiles are held in the Xbox
    /// app's own image cache, which it enumerates and prunes - it deleted an unmanaged file placed in
    /// one of its folders during this feature's investigation, and its binary carries the eviction code
    /// to do it - so for those the sidecars go into this app's own storage instead, and every method
    /// here takes an optional folder for them. The sidecars keep the image's name either way, because
    /// the Xbox app's file names are hashes and unique across the whole cache.
    ///
    /// Separate from the widget because these are the operations that can destroy artwork the user
    /// cannot recover, and inside a UI file they could only ever be checked by running the app and
    /// looking. Every argument here is a folder and a file name rather than a GameEntry, so the whole
    /// module runs against a throwaway directory in a test - GameEntry binds to Windows.UI.Xaml, which
    /// exists only in an app container, and would have dragged all of this back in there with it.
    ///
    /// Reporting and record-keeping stay with the caller: nothing here writes status text, touches the
    /// dispatcher, or updates <see cref="AppliedArtworkStore"/>. Failures other than the expected
    /// missing-file cases are thrown, not swallowed, so the caller decides what the user is told.
    /// </summary>
    internal static class ArtworkFiles
    {
        /// <summary>The Xbox app's own artwork, preserved the first time a game is customised.</summary>
        internal const string BackupExtension = ".bak";

        /// <summary>A copy of the applied artwork, kept so it can be re-applied if it is overwritten.</summary>
        internal const string CustomisedExtension = ".new";

        /// <summary>
        /// How the image itself is written.
        ///
        /// First-party tiles live in the Xbox app's own image cache, and the app keeps the ones it is
        /// showing memory-mapped. Windows lets a mapped file's contents change but not its length, and
        /// refuses anything that would resize or replace it with ERROR_USER_MAPPED_FILE - so the write
        /// every other path makes, create-with-replace, fails outright on exactly the tiles the user is
        /// looking at. Which is to say: it fails whenever they have their library open to see whether it
        /// worked.
        /// </summary>
        internal enum WriteMode
        {
            /// <summary>
            /// Replace the file. What a third-party tile wants: it is this widget's own file in a folder
            /// the Xbox app only reads, its length is free to change, and replacing it is atomic.
            /// </summary>
            Replace,

            /// <summary>
            /// Overwrite the bytes and leave the length alone, padding the artwork out to the length
            /// already there - the only write a memory-mapped file accepts. Bytes that cannot fit fall
            /// back to replacing the file: sidecars saved before padding existed can be longer than
            /// the tile they go back onto, and a replace is legal on any file the app does not have
            /// mapped at that moment - and cleanly refused, per file, when it does.
            /// </summary>
            InPlace,
        }

        /// <summary>What <see cref="RestoreOriginalAsync"/> found to do.</summary>
        internal enum RestoreOutcome
        {
            /// <summary>The Xbox app's original is back in place and the backup is gone.</summary>
            Restored,

            /// <summary>No backup exists, so nothing was changed.</summary>
            BackupMissing,
        }

        /// <summary>What <see cref="ReapplyCustomisationAsync"/> found to do.</summary>
        internal enum ReapplyOutcome
        {
            /// <summary>The saved customisation is back in place as the image.</summary>
            Reapplied,

            /// <summary>No customisation was ever saved for this image, so nothing was changed.</summary>
            NothingSaved,
        }

        /// <summary>
        /// The name a sibling artefact takes for the given image.
        ///
        /// Path.ChangeExtension rather than a string replace: a replace rewrites every occurrence of
        /// ".png" in the name and silently does nothing for images that are not .png at all, which
        /// would make the backup name equal the image name and overwrite the original unrecoverably.
        /// </summary>
        internal static string SiblingNameFor(string imageFileName, string extension)
        {
            return Path.ChangeExtension(imageFileName, extension);
        }

        /// <summary>The backup's name for the given image.</summary>
        internal static string BackupNameFor(string imageFileName)
        {
            return SiblingNameFor(imageFileName, BackupExtension);
        }

        /// <summary>The saved-customisation's name for the given image.</summary>
        internal static string CustomisedNameFor(string imageFileName)
        {
            return SiblingNameFor(imageFileName, CustomisedExtension);
        }

        /// <summary>
        /// Whether this image has a backup, and so can be reverted to the Xbox app's own artwork.
        /// </summary>
        internal static async Task<bool> HasBackupAsync(StorageFolder folder, string imageFileName)
        {
            try
            {
                await folder.GetFileAsync(BackupNameFor(imageFileName));

                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Whether this image has a saved customisation, and so can have it put back.
        ///
        /// The counterpart to <see cref="HasBackupAsync"/>, and the reason both are asked: they are the
        /// two independent ways an entry can still be worth showing when its image has gone. A backup
        /// means the Xbox app's own artwork can be restored; a saved customisation means the user's
        /// chosen artwork can be. Either is something to act on, and neither implies the other - the
        /// two files are written at different moments and removed by different things.
        /// </summary>
        internal static async Task<bool> HasSavedCustomisationAsync(StorageFolder folder, string imageFileName)
        {
            try
            {
                await folder.GetFileAsync(CustomisedNameFor(imageFileName));

                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes artwork over a game's image, preserving the Xbox app's original the first time.
        /// </summary>
        /// <param name="folder">The Xbox app's image folder for this game.</param>
        /// <param name="imageFileName">The image's name, which the Xbox app owns and this never changes.</param>
        /// <param name="artworkBytes">The artwork to apply, in any format a decoder can read.</param>
        /// <returns>Whether a backup of the Xbox app's original now exists.</returns>
        internal static async Task<bool> ApplyAsync(StorageFolder folder, string imageFileName, IBuffer artworkBytes)
        {
            // The Xbox app names every third-party tile .png and we cannot rename its files, so anything
            // that is not already a PNG is re-encoded rather than written under a lying extension
            return await ApplyEncodedAsync(folder, imageFileName, folder, await TileImage.EnsurePngAsync(artworkBytes));
        }

        /// <summary>
        /// Writes already-encoded bytes over a game's image, preserving the Xbox app's original the
        /// first time.
        ///
        /// Separate from <see cref="ApplyAsync"/> because the caller has to have chosen the encoding
        /// before it gets here: third-party tiles must be PNG to match the name the Xbox app gave them,
        /// first-party tiles must be a JPEG of one exact size to match what the app cached. Neither
        /// choice can be made from a folder and a file name.
        /// </summary>
        /// <param name="imageFolder">The folder holding the image itself.</param>
        /// <param name="imageFileName">The image's name, which the Xbox app owns and this never changes.</param>
        /// <param name="sidecarFolder">Where the .bak and .new go. The image's own folder for third-party tiles.</param>
        /// <param name="tileBytes">The exact bytes to write.</param>
        /// <param name="mode">How to write the image. See <see cref="WriteMode"/>.</param>
        /// <returns>Whether a backup of the Xbox app's original now exists.</returns>
        internal static async Task<bool> ApplyEncodedAsync(
            StorageFolder imageFolder,
            string imageFileName,
            StorageFolder sidecarFolder,
            IBuffer tileBytes,
            WriteMode mode = WriteMode.Replace)
        {
            bool backupExists = await BackupOnceAsync(imageFolder, imageFileName, sidecarFolder);

            // What lands on disk, which under InPlace is the artwork padded out to the length that was
            // already there. The saved customisation has to be those same bytes rather than the artwork
            // alone: it is compared against the image to tell a surviving customisation from one the
            // Xbox app has overwritten, and two files that were never equal to begin with would report
            // every tile as overwritten on every load.
            IBuffer written = await WriteImageAsync(imageFolder, imageFileName, tileBytes, mode);

            StorageFile newFile = await sidecarFolder.CreateFileAsync(
                CustomisedNameFor(imageFileName), CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteBufferAsync(newFile, written);

            return backupExists;
        }

        /// <summary>
        /// Preserves the Xbox app's original, once and once only.
        ///
        /// Both halves of that matter: writing first would back up the previous customisation instead of
        /// the Xbox app's artwork, and overwriting an existing backup would do the same on the second
        /// fix. Either one loses the original permanently.
        /// </summary>
        /// <param name="imageFolder">The folder holding the image itself.</param>
        /// <param name="imageFileName">The image's name.</param>
        /// <param name="sidecarFolder">Where the .bak goes.</param>
        /// <returns>Whether a backup now exists.</returns>
        private static async Task<bool> BackupOnceAsync(StorageFolder imageFolder, string imageFileName, StorageFolder sidecarFolder)
        {
            string backupFileName = BackupNameFor(imageFileName);

            try
            {
                await sidecarFolder.GetFileAsync(backupFileName);

                return true;
            }
            catch (FileNotFoundException)
            {
                // Nothing preserved yet, so this write is the one that would destroy the original
            }

            try
            {
                StorageFile existingImageFile = await imageFolder.GetFileAsync(imageFileName);

                StorageFile backupFile = await sidecarFolder.CreateFileAsync(backupFileName, CreationCollisionOption.ReplaceExisting);
                IBuffer existingBuffer = await FileIO.ReadBufferAsync(existingImageFile);

                await FileIO.WriteBufferAsync(backupFile, existingBuffer);

                return true;
            }
            catch (FileNotFoundException)
            {
                // No existing image to back up - a game whose artwork the Xbox app never wrote
                return false;
            }
        }

        /// <summary>
        /// Writes bytes as the image, by whichever of the two routes the file allows.
        /// </summary>
        /// <param name="imageFolder">The folder holding the image.</param>
        /// <param name="imageFileName">The image's name.</param>
        /// <param name="bytes">The artwork to write.</param>
        /// <param name="mode">How to write it. See <see cref="WriteMode"/>.</param>
        /// <returns>The bytes that are now on disk, which under InPlace are padded.</returns>
        private static async Task<IBuffer> WriteImageAsync(
            StorageFolder imageFolder,
            string imageFileName,
            IBuffer bytes,
            WriteMode mode)
        {
            if (mode == WriteMode.InPlace)
            {
                try
                {
                    StorageFile existing = await imageFolder.GetFileAsync(imageFileName);
                    uint length = (uint)(await existing.GetBasicPropertiesAsync()).Size;

                    if (bytes.Length <= length)
                    {
                        return await WriteInPlaceAsync(existing, bytes, length);
                    }

                    // Longer than the space there is. Applies encode to fit - see
                    // TileImage.EncodeSquareJpegAsync's byte budget - so this is a sidecar saved
                    // before padding existed going back onto a tile the Xbox app has since rewritten
                    // shorter. The only write left is the replace below, which grows the file: legal
                    // on anything the app does not have mapped at this moment, and cleanly refused by
                    // the file system when it does - a refusal the callers contain per rendition.
                }
                catch (FileNotFoundException)
                {
                    // Nothing there to preserve the length of, and nothing there to have mapped either
                }
            }

            StorageFile imageFile = await imageFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteBufferAsync(imageFile, bytes);

            return bytes;
        }

        /// <summary>
        /// Overwrites a file's contents without changing its length.
        ///
        /// The stream's Size is deliberately never assigned - that is the one operation here a mapped
        /// file refuses, and leaving it alone is the whole point. Artwork shorter than the file is
        /// padded rather than left to trail the previous tile's bytes: a JPEG decoder stops at its
        /// end-of-image marker either way, but the padding is what makes the result a function of the
        /// artwork alone, so the same artwork applied twice leaves the same file both times.
        /// </summary>
        /// <param name="imageFile">The image to overwrite.</param>
        /// <param name="bytes">The artwork, no longer than the file. The caller checked.</param>
        /// <param name="length">The file's current length, which it keeps.</param>
        /// <returns>The padded bytes that are now on disk.</returns>
        private static async Task<IBuffer> WriteInPlaceAsync(StorageFile imageFile, IBuffer bytes, uint length)
        {
            IBuffer padded = PadTo(bytes, length);

            using (IRandomAccessStream stream = await imageFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                stream.Seek(0);

                await stream.WriteAsync(padded);
                await stream.FlushAsync();
            }

            return padded;
        }

        /// <summary>
        /// The buffer, zero-padded to an exact length. Returned as-is when it is already that long.
        /// </summary>
        internal static IBuffer PadTo(IBuffer bytes, uint length)
        {
            if (bytes.Length == length)
            {
                return bytes;
            }

            byte[] source = new byte[bytes.Length];

            using (DataReader reader = DataReader.FromBuffer(bytes))
            {
                reader.ReadBytes(source);
            }

            // Via a byte[] because the runtime guarantees it starts zeroed, which is what makes the
            // padding a stated value rather than whatever the allocator last had there
            byte[] padded = new byte[length];

            Array.Copy(source, padded, source.Length);

            return CryptographicBuffer.CreateFromByteArray(padded);
        }

        /// <summary>
        /// Puts the Xbox app's own artwork back and drops the customisation.
        /// </summary>
        /// <param name="folder">The Xbox app's image folder for this game.</param>
        /// <param name="imageFileName">The image's name.</param>
        internal static async Task<RestoreOutcome> RestoreOriginalAsync(StorageFolder folder, string imageFileName)
        {
            return await RestoreOriginalAsync(folder, imageFileName, folder);
        }

        /// <summary>
        /// Puts the Xbox app's own artwork back and drops the customisation, with the sidecars held
        /// somewhere other than beside the image.
        /// </summary>
        /// <param name="imageFolder">The folder holding the image itself.</param>
        /// <param name="imageFileName">The image's name.</param>
        /// <param name="sidecarFolder">Where the .bak and .new live.</param>
        /// <param name="mode">How to write the image. See <see cref="WriteMode"/>.</param>
        internal static async Task<RestoreOutcome> RestoreOriginalAsync(
            StorageFolder imageFolder,
            string imageFileName,
            StorageFolder sidecarFolder,
            WriteMode mode = WriteMode.Replace)
        {
            StorageFile backupFile;

            // Locate the backup first so a missing backup never leaves the game without an image
            try
            {
                backupFile = await sidecarFolder.GetFileAsync(BackupNameFor(imageFileName));
            }
            catch (FileNotFoundException)
            {
                return RestoreOutcome.BackupMissing;
            }

            if (mode == WriteMode.InPlace)
            {
                // Copied in rather than moved, because a move is a replace - the write a mapped file
                // refuses when the artwork does not fit, and the tile being restored is one the Xbox
                // app is perfectly likely to be showing. Both sidecars survive until the image holds
                // the original again: the backup is unrecoverable and the customisation is the only
                // copy of what the user chose, so a refused write has to cost nothing but the retry.
                await WriteImageAsync(imageFolder, imageFileName, await FileIO.ReadBufferAsync(backupFile), mode);

                await DiscardCustomisationAsync(sidecarFolder, imageFileName);
                await backupFile.DeleteAsync();

                return RestoreOutcome.Restored;
            }

            // Move rather than copy-then-delete: ReplaceExisting overwrites the current image in one
            // step, so the image is never absent in between. A copy that failed half way would leave
            // the game with a truncated tile and the backup already gone.
            //
            // Rename cannot leave a folder, so it only serves the case where the sidecars sit beside
            // the image. That is the third-party path, and it keeps the call it has always made.
            if (string.Equals(sidecarFolder.Path, imageFolder.Path, StringComparison.OrdinalIgnoreCase))
            {
                await backupFile.RenameAsync(imageFileName, NameCollisionOption.ReplaceExisting);
            }
            else
            {
                await backupFile.MoveAsync(imageFolder, imageFileName, NameCollisionOption.ReplaceExisting);
            }

            await DiscardCustomisationAsync(sidecarFolder, imageFileName);

            return RestoreOutcome.Restored;
        }

        /// <summary>
        /// Deletes the saved customisation, if there is one. Called only after the image holds what it
        /// should - the .new is the one copy of the user's choice, and deleting it ahead of a write
        /// that can be refused would trade it for an error message.
        /// </summary>
        private static async Task DiscardCustomisationAsync(StorageFolder sidecarFolder, string imageFileName)
        {
            try
            {
                StorageFile newImageFile = await sidecarFolder.GetFileAsync(CustomisedNameFor(imageFileName));

                await newImageFile.DeleteAsync();
            }
            catch (FileNotFoundException)
            {
                // Saved customisation doesn't exist, that's okay
            }
        }

        /// <summary>
        /// Puts a saved customisation back as the image, for when something outside the widget - the
        /// Xbox app refreshing its library, usually - has overwritten it.
        /// </summary>
        /// <param name="folder">The Xbox app's image folder for this game.</param>
        /// <param name="imageFileName">The image's name.</param>
        internal static async Task<ReapplyOutcome> ReapplyCustomisationAsync(StorageFolder folder, string imageFileName)
        {
            return await ReapplyCustomisationAsync(folder, imageFileName, folder);
        }

        /// <summary>
        /// Puts a saved customisation back as the image, with the sidecars held somewhere other than
        /// beside it.
        ///
        /// This is the routine path for first-party tiles rather than a repair: the Xbox app re-downloads
        /// a cached image whenever the file goes missing, its ninety-day lifetime runs out, or the Store
        /// changes the artwork - and it keeps no record that would let it notice a replacement, so the
        /// only way a customisation survives is to put it back.
        /// </summary>
        /// <param name="imageFolder">The folder holding the image itself.</param>
        /// <param name="imageFileName">The image's name.</param>
        /// <param name="sidecarFolder">Where the .bak and .new live.</param>
        /// <param name="mode">How to write the image. See <see cref="WriteMode"/>.</param>
        internal static async Task<ReapplyOutcome> ReapplyCustomisationAsync(
            StorageFolder imageFolder,
            string imageFileName,
            StorageFolder sidecarFolder,
            WriteMode mode = WriteMode.Replace)
        {
            StorageFile newFile;

            try
            {
                newFile = await sidecarFolder.GetFileAsync(CustomisedNameFor(imageFileName));
            }
            catch (FileNotFoundException)
            {
                return ReapplyOutcome.NothingSaved;
            }

            IBuffer saved = await FileIO.ReadBufferAsync(newFile);
            IBuffer written = await WriteImageAsync(imageFolder, imageFileName, saved, mode);

            // A customisation written by ApplyEncodedAsync goes back exactly as it came off - it was
            // saved as the very bytes the tile held. One saved before padding existed is the artwork
            // at its own length, so what lands differs from it, and the saved copy is brought up to
            // date the one time that happens: the overwrite check compares the two, and leaving them
            // unequal would report this tile overwritten - and rewrite it - on every load forever.
            if (written.Length != saved.Length)
            {
                await FileIO.WriteBufferAsync(newFile, written);
            }

            return ReapplyOutcome.Reapplied;
        }
    }
}
