using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Windows.Storage.Streams;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.SteamGridDB;
using SteamGridDB.Xbox.Services.SteamGridDB.Models;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Downloading the best artwork SteamGridDB has for every game that has a match there.
    ///
    /// Three shapes are tried in order and the order is the point: a square grid is what an Xbox tile
    /// actually is; portrait box art cropped to a square is a far better tile than an icon, and the
    /// three games in the test library that reach that fallback have 13, 5 and 6 portrait candidates
    /// between them; an icon is the last resort. Each step says in the run log what it found, because
    /// the games that reach the fallbacks used to be a capsule line followed by silence - a run's 8
    /// fallback games looked identical to 8 games nothing happened to.
    /// </summary>
    internal static class LibraryFixer
    {
        /// <summary>
        /// Fixes every eligible game, one per image, reporting as it goes.
        /// </summary>
        /// <param name="library">Every row in the library. Eligibility and the one-per-image rule are
        /// applied here rather than by the caller, so a run and its own "how many were left alone"
        /// count cannot disagree about what it visited.</param>
        /// <param name="refixCustomised">When true, also re-downloads artwork for games that were
        /// customised before (their original backups are preserved).</param>
        /// <param name="steamGridDbApiKey">SteamGridDB API key, or null/blank when there is none.</param>
        /// <param name="target">The library to write to and report through.</param>
        internal static async Task RunAsync<TGame>(
            IEnumerable<TGame> library,
            bool refixCustomised,
            string steamGridDbApiKey,
            IArtworkTarget<TGame> target) where TGame : ILibraryGame
        {
            // Opened before the first early return, not after them, so that the finally below always
            // has a log belonging to this run to write. Starting it later meant a run that declined
            // to do anything - no API key, nothing eligible - left last-fix.log describing some
            // earlier run, which reads as if this one had done that work.
            FixLog.Start(refixCustomised ? "Re-fix all games" : "Fix my library");

            try
            {
                // Whitespace counts as missing, matching SteamGridDbClient's own validation - a key
                // that fails this test would otherwise sail past the guards and throw out of the
                // client's constructor.
                if (string.IsNullOrWhiteSpace(steamGridDbApiKey))
                {
                    await target.ReportAsync("SteamGridDB API key is not set - artwork cannot be downloaded");

                    FixLog.Write("nothing attempted: SteamGridDB API key is not set");

                    return;
                }

                // Eligible: there is a match in SteamGridDB, it is not one of the Xbox app's own games,
                // and, unless re-fixing, there is no backup yet. See FixEligibility for why the Xbox
                // app's own games are left alone by the bulk runs.
                List<TGame> eligibleGames = OnePerImage(library, g =>
                    FixEligibility.ShouldFix(g.HasSteamGridDBMatch, g.IsXboxTile, g.HasBackup, refixCustomised));

                // Counted from the same deduplicated set the run itself walks, so a first-party game
                // listed under several stale manifest entries is one game here as well
                int firstPartyCount = OnePerImage(library, g =>
                    FixEligibility.SkippedAsFirstParty(g.HasSteamGridDBMatch, g.IsXboxTile, g.HasBackup, refixCustomised)).Count;

                string firstPartyClause = OperationReport.When(
                    firstPartyCount,
                    $"{OperationReport.Plural(firstPartyCount, "Xbox app game")} left alone (they already have the Store's own artwork)");

                if (eligibleGames.Count == 0)
                {
                    await target.ReportAsync(OperationReport.Summary(
                        refixCustomised
                            ? "No eligible artworks to fix (no games have a match in SteamGridDB)"
                            : "No eligible artworks to fix (all games either were already modified or have no match in SteamGridDB)",
                        firstPartyClause));

                    FixLog.Write($"nothing eligible: {firstPartyCount} first-party game(s) left alone");

                    return;
                }

                await target.ReportAsync("Fixing library artwork...");

                var report = new OperationReport("Fixing", eligibleGames.Count);

                int successCount = 0;
                int notFoundCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                foreach (string note in SteamGridDbClient.CapsuleParseNotes)
                {
                    FixLog.Write($"capsule parse: {note}");
                }

                // Set from inside the using below, because the summary is built after it closes
                bool stoppedForThrottling = false;

                using (SteamGridDbClient client = new SteamGridDbClient(steamGridDbApiKey))
                {
                    foreach (TGame game in eligibleGames)
                    {
                        // SteamGridDB has refused several requests in a row and the client has stopped
                        // asking. Walking the rest of the library would make a request per game that
                        // cannot be answered, which is the pattern the backoff exists to avoid - and
                        // every one of them would be counted as an error, burying however many games
                        // the run did fix under a wall of failures.
                        if (client.HasGivenUp)
                        {
                            stoppedForThrottling = true;

                            FixLog.Write($"stopped after {report.Started} of {report.Total}: SteamGridDB is rate limiting this client");

                            break;
                        }

                        try
                        {
                            // game.Name rather than the display name: this line has always shown
                            // "Unknown" for an unnamed game rather than falling back to its image
                            // file name
                            await target.ReportAsync(report.Step(game.Name));

                            ArtworkSource source = ArtworkSource.SourceFor(game.SteamGridDbGameId, game.Platform, game.ExternalPlatformId);

                            if (source == null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Skipping {game.Name}: unsupported platform");

                                // Counted separately from "no artwork found": nothing was looked up at all
                                skippedCount++;

                                continue;
                            }

                            // Prefer grids with title artwork so tiles match the native Xbox app look.
                            // Rank the unfiltered results client-side: tied scores are common, and the stable
                            // sort keeps SteamGridDB's canonical ordering for ties (the same image the site
                            // shows first, typically the official box art).
                            FixLog.Write($"{game.Name} capsule={(string.IsNullOrEmpty(game.OfficialCapsuleUrl) ? "none" : game.OfficialCapsuleUrl)}");

                            List<SteamGridDbGrid> grids = await client.GetTitleBearingGridsAsync(source);

                            if (grids == null)
                            {
                                // The request itself failed - throttled, offline, a bad gateway. Reporting
                                // that as "SteamGridDB has no artwork" would be a lie, and would make a
                                // graded comparison against the previous run meaningless.
                                errorCount++;

                                FixLog.Write("  square lookup failed - counted as an error");

                                System.Diagnostics.Debug.WriteLine($"Artwork lookup failed for {game.Name}");

                                continue;
                            }

                            if (grids.Count > 0)
                            {
                                // Rank candidates, then take the best one whose art actually fills the tile
                                List<SteamGridDbGrid> ranked = ArtworkRanker.RankGrids(grids, game.Name);

                                FixLog.Write($"  {grids.Count} square candidates, ranked: {string.Join(", ", ranked.Take(5).Select(g => g.Id))}");

                                (IBuffer Bytes, int ArtworkId) best = await ArtworkDownloader.DownloadBestTileFillingImageAsync(ranked, game.Name, game.OfficialCapsuleUrl);

                                bool downloaded = best.Bytes != null && (await target.ApplyAsync(game, best.Bytes, best.ArtworkId)).Succeeded;

                                // Written after the write rather than before it, so the line records
                                // what happened rather than what was hoped - this used to say
                                // "applied 0" when every candidate download failed
                                FixLog.Write(
                                    best.Bytes == null ? "  no candidate could be downloaded"
                                    : downloaded ? $"  applied {best.ArtworkId}"
                                    : $"  {best.ArtworkId} downloaded but could not be written");

                                if (downloaded)
                                {
                                    successCount++;
                                }
                                else
                                {
                                    errorCount++;
                                }
                            }
                            else if (await TryPortraitArtAsync(client, game, source, target))
                            {
                                successCount++;
                            }
                            else
                            {
                                // No square or portrait artwork - icons are the last resort
                                List<SteamGridDbGrid> icons = await client.GetSquareIconsAsync(source);

                                if (icons == null)
                                {
                                    errorCount++;

                                    FixLog.Write("  icon lookup failed - counted as an error");

                                    System.Diagnostics.Debug.WriteLine($"Icon lookup failed for {game.Name}");

                                    continue;
                                }

                                if (icons.Count > 0)
                                {
                                    SteamGridDbGrid bestIcon = ArtworkRanker.RankIcons(icons).First();
                                    bool downloaded = (await target.ApplyFromUrlAsync(game, bestIcon.Url, bestIcon.Id)).Succeeded;

                                    FixLog.Write(downloaded
                                        ? $"  applied {bestIcon.Id} (icon)"
                                        : $"  icon {bestIcon.Id} could not be downloaded and written");

                                    if (downloaded)
                                    {
                                        successCount++;
                                    }
                                    else
                                    {
                                        errorCount++;
                                    }
                                }
                                else
                                {
                                    notFoundCount++;

                                    FixLog.Write("  nothing on SteamGridDB in any shape - square, portrait or icon");

                                    System.Diagnostics.Debug.WriteLine($"No artwork found for {game.Name}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;

                            FixLog.Write($"  error ({ex.GetType().Name}: {ex.Message})");

                            System.Diagnostics.Debug.WriteLine($"Error processing {game.Name}: {ex.Message}");
                        }
                    }
                }

                // The error count is always shown here, unlike the other operations: a fix that reports
                // nothing about failures reads as a clean run when it may have touched almost nothing
                await target.ReportAsync(OperationReport.Summary(
                    stoppedForThrottling
                        ? $"Fixing library stopped early - SteamGridDB is rate limiting; try again later. {successCount} updated so far"
                        : $"Fixing library is complete: {successCount} updated, {notFoundCount} had no artwork in the database",
                    OperationReport.When(skippedCount, $"{skippedCount} skipped (unsupported platform)"),
                    firstPartyClause,
                    OperationReport.Plural(errorCount, "error")));
            }
            catch (Exception ex)
            {
                await target.ReportAsync($"Error fixing library: {ex.Message}");

                System.Diagnostics.Debug.WriteLine($"Error in LibraryFixer.RunAsync: {ex.Message}");
            }
            finally
            {
                // In a finally rather than at the end of the try, for the same reason the library
                // load's is: the run worth having a log for is the one that failed, and every early
                // return and every throw above used to leave last-fix.log holding a previous,
                // unrelated run.
                await FixLog.SaveAsync();
            }
        }

        /// <summary>
        /// The games a bulk run should visit: those matching <paramref name="eligible"/>, one per image.
        /// </summary>
        internal static List<TGame> OnePerImage<TGame>(IEnumerable<TGame> library, Func<TGame, bool> eligible)
            where TGame : ILibraryGame
        {
            return GameImages.DistinctByImage(library.Where(eligible), g => g.ImageFilePath);
        }

        /// <summary>
        /// Last chance before the icon fallback: a game with no square artwork often still has portrait
        /// box art, which cropped to a square makes a far better tile than an icon does.
        /// </summary>
        /// <param name="client">Client to fetch with.</param>
        /// <param name="game">Game being fixed.</param>
        /// <param name="source">How to address the game's artwork.</param>
        /// <param name="target">The library to write to.</param>
        /// <returns>True when a cropped tile was written.</returns>
        private static async Task<bool> TryPortraitArtAsync<TGame>(
            SteamGridDbClient client,
            TGame game,
            ArtworkSource source,
            IArtworkTarget<TGame> target) where TGame : ILibraryGame
        {
            List<SteamGridDbGrid> portraits = await client.GetPortraitGridsAsync(source);

            // Each outcome says so in the run log. The games that reach this method are exactly the
            // ones whose entries used to be a capsule line followed by silence, because only the
            // square-grid path wrote what it did - a run's 8 fallback games looked identical to 8
            // games nothing happened to.
            if (portraits == null)
            {
                FixLog.Write("  portrait lookup failed - trying icons");

                return false;
            }

            if (portraits.Count == 0)
            {
                FixLog.Write("  no portrait artwork either - trying icons");

                return false;
            }

            List<SteamGridDbGrid> ranked = ArtworkRanker.RankGrids(portraits, game.Name)
                .Take(ArtworkDownloader.MaxCandidates)
                .ToList();

            FixLog.Write($"  {portraits.Count} portrait candidates, ranked: {string.Join(", ", ranked.Take(5).Select(g => g.Id))}");

            foreach (SteamGridDbGrid candidate in ranked)
            {
                IBuffer cropped = await TileImage.CropPortraitToTileAsync(await ArtworkDownloader.DownloadArtworkAsync(candidate.Url));

                if (cropped != null && (await target.ApplyAsync(game, cropped, candidate.Id)).Succeeded)
                {
                    FixLog.Write($"  applied {candidate.Id} (portrait, cropped)");

                    System.Diagnostics.Debug.WriteLine($"Used cropped portrait art {candidate.Id} for {game.Name}");

                    return true;
                }
            }

            FixLog.Write("  no portrait candidate survived download and crop - trying icons");

            return false;
        }
    }
}
