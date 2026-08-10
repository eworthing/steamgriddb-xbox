using System;
using System.IO;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Storage;

using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services.Artwork;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Resolves where a manifest entry's image lives on disk, and whether a backup exists for it - the
    /// platform-dependent half of LoadGameEntriesAsync's per-entry parsing (see PrimaryWidget.xaml.cs)
    /// that sits between <see cref="ManifestEntryIdentity"/> (which never touches the file system) and
    /// the thumbnail decode PrimaryWidget itself still owns: turning a found file into a BitmapImage
    /// binds to Windows.UI.Xaml, which has no desktop test projection (see TESTING.md), so that one
    /// step - and only that step - stays in the widget.
    ///
    /// Custom-platform entries carry their own full image path in the manifest, and the folder that
    /// holds it must be resolved separately - a game whose folder the user has since removed makes that
    /// resolution throw, which is exactly the same "nothing left to show" outcome as every other
    /// stale-entry case below. Every other platform derives both the file name and the folder from the
    /// entry's own ID and the folder the manifest itself was read from.
    ///
    /// A missing image file is not automatically stale: if a backup still exists, the row is kept so
    /// <see cref="ArtworkFiles.RestoreOriginalAsync"/> can still act on it later. The three call sites
    /// this replaced handled that identically, using the literal string "Not found" as the placeholder
    /// file name - preserved here rather than reinterpreted.
    /// </summary>
    internal static class ManifestEntryImage
    {
        /// <summary>
        /// One manifest entry's resolved image location. <see cref="ExistingImageFile"/> is null exactly
        /// when the image itself is missing but a backup still exists - the "Not found" placeholder case.
        /// </summary>
        internal readonly struct Result
        {
            internal string ImageFilePath { get; }

            internal StorageFolder ImageFolder { get; }

            internal string ImageFileName { get; }

            internal bool HasBackup { get; }

            internal StorageFile ExistingImageFile { get; }

            internal Result(string imageFilePath, StorageFolder imageFolder, string imageFileName, bool hasBackup, StorageFile existingImageFile)
            {
                ImageFilePath = imageFilePath;
                ImageFolder = imageFolder;
                ImageFileName = imageFileName;
                HasBackup = hasBackup;
                ExistingImageFile = existingImageFile;
            }
        }

        /// <summary>
        /// Resolves one manifest entry's image location. Returns null when the entry has nothing on disk
        /// a caller could show or act on - the manifest's own stale-entry case, counted and skipped
        /// identically by the caller regardless of which of the three underlying reasons produced it:
        /// a Custom entry with no recorded image path, a Custom entry whose folder no longer resolves, or
        /// a standard entry whose image is gone with no backup to fall back to.
        /// </summary>
        /// <param name="entryObject">The manifest entry's JSON object.</param>
        /// <param name="platform">The entry's platform.</param>
        /// <param name="entryId">The entry's raw "id" field, used to derive the standard (non-Custom) image file name.</param>
        /// <param name="manifestFolder">The Xbox app folder the manifest itself was read from.</param>
        /// <param name="thirdPartyLibrariesPath">Root of the Xbox app's ThirdPartyLibraries tree, used to build the standard image path.</param>
        /// <param name="imageExtension">Extension the standard (non-Custom) image file name is given.</param>
        internal static async Task<Result?> ResolveAsync(
            JsonObject entryObject,
            GamePlatform platform,
            string entryId,
            StorageFolder manifestFolder,
            string thirdPartyLibrariesPath,
            string imageExtension)
        {
            string imageFilePath;
            StorageFolder imageFolder;

            if (GamePlatformHelper.CarriesOwnPaths(platform)) // Custom contains full path for the image filename
            {
                imageFilePath = JsonRead.String(entryObject, "imagePath");

                if (string.IsNullOrEmpty(imageFilePath))
                {
                    // Same outcome as the folder-resolution failure below - no path means nothing on
                    // disk this entry could point at
                    return null;
                }

                try
                {
                    imageFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(imageFilePath));
                }
                catch (Exception ex)
                {
                    // Folder of a removed custom game - skip this entry, not the whole manifest
                    System.Diagnostics.Debug.WriteLine($"Skipping custom entry {entryId}: {ex.Message}");

                    return null;
                }
            }
            else // Image filename is based on ID
            {
                imageFilePath = Path.Combine(thirdPartyLibrariesPath, manifestFolder.Name, entryId.Replace(":", "_") + imageExtension);
                imageFolder = manifestFolder;
            }

            string imageFileName = Path.GetFileName(imageFilePath);
            bool hasBackup = await ArtworkFiles.HasBackupAsync(imageFolder, imageFileName);

            try
            {
                StorageFile existingImageFile = await imageFolder.GetFileAsync(imageFileName);

                return new Result(imageFilePath, imageFolder, imageFileName, hasBackup, existingImageFile);
            }
            catch (FileNotFoundException)
            {
                // A saved customisation is as much a reason to keep the row as a backup is, and on the
                // graded library it was the only reason left for thirteen games: every one had its
                // image and its backup gone while the artwork the user had chosen sat beside them
                // untouched, and every one was invisible in the widget and in the Xbox app. Keeping
                // them lets Restore my changes write that artwork back - which it can, because
                // ReapplyCustomisationAsync creates the image rather than requiring one.
                //
                // Asked only here, in the branch where the image is already known to be missing, so a
                // library whose images are all present pays nothing for it.
                if (!hasBackup && !await ArtworkFiles.HasSavedCustomisationAsync(imageFolder, imageFileName))
                {
                    // Nothing on disk for this entry: either a game the Xbox app removed but left in the
                    // manifest, or one of the legacy store folders it abandoned (their images use a
                    // different naming scheme and it no longer reads them)
                    return null;
                }

                // Image is gone but something to act on is not - keep the row so it can be restored
                return new Result(imageFilePath, imageFolder, "Not found", hasBackup, null);
            }
        }
    }
}
