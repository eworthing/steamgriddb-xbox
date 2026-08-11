using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Xbox;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// The two library-wide undos, which sound alike and are opposites.
    ///
    /// <c>RestoreAllChangesAsync</c> puts the user's own artwork back after the Xbox app has
    /// overwritten it - the customisation is what survives. <c>RevertAllToDefaultAsync</c> throws the
    /// customisation away and puts the Xbox app's original back. One repairs the customisation, the
    /// other removes it.
    /// </summary>
    internal static class LibraryRestorer
    {
        /// <summary>
        /// Re-applies every saved customisation the Xbox app has since overwritten.
        /// </summary>
        /// <param name="library">Every row in the library; visited one per image.</param>
        /// <param name="target">The library to write to and report through.</param>
        internal static async Task RestoreAllChangesAsync<TGame>(
            IEnumerable<TGame> library,
            IArtworkTarget<TGame> target) where TGame : ILibraryGame
        {
            try
            {
                await target.ReportAsync("Restoring customisations...");

                int successCount = 0;
                int noArtworkCount = 0;
                int errorCount = 0;

                List<TGame> uniqueGames = LibraryFixer.OnePerImage(library, g => true);

                var report = new OperationReport("Restoring", uniqueGames.Count);

                // One listing for the whole run, as the library load does - this walks every game too
                HashSet<string> vaultFileNames = await XboxTileStore.VaultFileNamesAsync();

                foreach (TGame game in uniqueGames)
                {
                    string imageFileName = Path.GetFileName(game.ImageFilePath);
                    string gameName = OperationReport.DisplayName(game);

                    try
                    {
                        await target.ReportAsync(report.Step(gameName));

                        // A first-party game has one saved customisation per rendition, and any of them
                        // could be the one the Xbox app overwrote, so all are checked
                        ArtworkFiles.ReapplyOutcome outcome = game.IsXboxTile
                            ? await XboxTiles.ReapplyOverwrittenAsync(game.ImageFolder, game.XboxRenditions, vaultFileNames)
                            : await ArtworkFiles.ReapplyCustomisationAsync(game.ImageFolder, imageFileName);

                        if (outcome == ArtworkFiles.ReapplyOutcome.NothingSaved)
                        {
                            noArtworkCount++;
                            System.Diagnostics.Debug.WriteLine($"Skipping {gameName} for restoration: corresponding .new file not found");

                            continue;
                        }

                        // Whether a backup exists doesn't change by restoring a customisation, and this
                        // loop's own status line is set above via report.Step - so this only re-reads
                        // the image, leaving both alone
                        await target.RefreshAsync(game, imageFileName);

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;

                        System.Diagnostics.Debug.WriteLine($"Error restoring changes for {gameName}: {ex.Message}");
                    }
                }

                // Nothing restored and nothing failed means every game simply had no saved artwork,
                // which is a state of the library rather than a result worth counting out
                await target.ReportAsync(successCount == 0 && errorCount == 0
                    ? "No changes found to restore"
                    : OperationReport.Summary(
                        $"Restore complete: {successCount} restored",
                        OperationReport.When(noArtworkCount, $"{noArtworkCount} had no artwork saved"),
                        OperationReport.Plural(errorCount, "error")));
            }
            catch (Exception ex)
            {
                await target.ReportAsync($"Error restoring changes: {ex.Message}");

                System.Diagnostics.Debug.WriteLine($"Error in RestoreAllChangesAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Puts the Xbox app's own artwork back for every game that has a backup, removing the
        /// SteamGridDB artwork applied to it.
        /// </summary>
        /// <param name="library">Every row in the library; visited one per image.</param>
        /// <param name="target">The library to write to and report through.</param>
        internal static async Task RevertAllToDefaultAsync<TGame>(
            IEnumerable<TGame> library,
            IArtworkTarget<TGame> target) where TGame : ILibraryGame
        {
            try
            {
                List<TGame> customisedGames = LibraryFixer.OnePerImage(library, g => g.HasBackup);

                if (customisedGames.Count == 0)
                {
                    await target.ReportAsync("No customised games to revert");

                    return;
                }

                var report = new OperationReport("Reverting", customisedGames.Count);

                int successCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                foreach (TGame game in customisedGames)
                {
                    await target.ReportAsync(report.Step(OperationReport.DisplayName(game)));

                    switch (await target.RestoreBackupAsync(game))
                    {
                        case RestoreBackupResult.Restored:
                            successCount++;
                            break;
                        case RestoreBackupResult.BackupMissing:
                            skippedCount++;
                            break;
                        default:
                            errorCount++;
                            break;
                    }
                }

                await target.ReportAsync(OperationReport.Summary(
                    $"Revert complete: {successCount} restored to Xbox defaults",
                    OperationReport.When(skippedCount, $"{skippedCount} skipped (no backup)"),
                    OperationReport.When(errorCount, OperationReport.Plural(errorCount, "error"))));
            }
            catch (Exception ex)
            {
                await target.ReportAsync($"Error reverting to defaults: {ex.Message}");

                System.Diagnostics.Debug.WriteLine($"Error in RevertAllToDefaultAsync: {ex.Message}");
            }
        }
    }
}
