using System;
using System.IO;
using System.Threading.Tasks;

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
        /// <returns>Whether a backup of the Xbox app's original now exists.</returns>
        internal static async Task<bool> ApplyEncodedAsync(StorageFolder imageFolder, string imageFileName, StorageFolder sidecarFolder, IBuffer tileBytes)
        {
            string backupFileName = BackupNameFor(imageFileName);
            string newFileName = CustomisedNameFor(imageFileName);

            // The backup is taken before anything is written, and only when there is not one already.
            // Both halves of that matter: writing first would back up the previous customisation
            // instead of the Xbox app's artwork, and overwriting an existing backup would do the same
            // on the second fix. Either one loses the original permanently.
            bool backupExists = false;

            try
            {
                await sidecarFolder.GetFileAsync(backupFileName);

                backupExists = true;
            }
            catch (FileNotFoundException)
            {
                try
                {
                    StorageFile existingImageFile = await imageFolder.GetFileAsync(imageFileName);

                    StorageFile backupFile = await sidecarFolder.CreateFileAsync(backupFileName, CreationCollisionOption.ReplaceExisting);
                    IBuffer existingBuffer = await FileIO.ReadBufferAsync(existingImageFile);

                    await FileIO.WriteBufferAsync(backupFile, existingBuffer);

                    backupExists = true;
                }
                catch (FileNotFoundException)
                {
                    // No existing image to back up - a game whose artwork the Xbox app never wrote
                }
            }

            StorageFile imageFile = await imageFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBufferAsync(imageFile, tileBytes);

            StorageFile newFile = await sidecarFolder.CreateFileAsync(newFileName, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteBufferAsync(newFile, tileBytes);

            return backupExists;
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
        internal static async Task<RestoreOutcome> RestoreOriginalAsync(StorageFolder imageFolder, string imageFileName, StorageFolder sidecarFolder)
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

            try
            {
                StorageFile newImageFile = await sidecarFolder.GetFileAsync(CustomisedNameFor(imageFileName));

                await newImageFile.DeleteAsync();
            }
            catch (FileNotFoundException)
            {
                // Saved customisation doesn't exist, that's okay
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

            return RestoreOutcome.Restored;
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
        internal static async Task<ReapplyOutcome> ReapplyCustomisationAsync(StorageFolder imageFolder, string imageFileName, StorageFolder sidecarFolder)
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

            IBuffer imageBytes = await FileIO.ReadBufferAsync(newFile);

            StorageFile imageFile = await imageFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteBufferAsync(imageFile, imageBytes);

            return ReapplyOutcome.Reapplied;
        }
    }
}
