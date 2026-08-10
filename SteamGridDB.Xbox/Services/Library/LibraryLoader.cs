using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Storage;

using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.SteamGridDB;
using SteamGridDB.Xbox.Services.Xbox;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Reading the library: the Xbox app's third-party manifests, then its own installed games.
    ///
    /// This was the first half of PrimaryWidget's LoadGameEntriesAsync, and it is here because every
    /// decision in it - which manifest entries are worth showing, how far one bad entry is allowed to
    /// take the rest down with it, what a game is called when nothing names it - is a rule the widget
    /// could not test. The pieces it leans on (<see cref="ManifestGameCache"/>,
    /// <see cref="ManifestEntryImage"/>, <see cref="ManifestEntryIdentity"/>,
    /// <see cref="GameMatchResolver"/>, <see cref="XboxLibrary"/>) were extracted one at a time for
    /// the same reason; this is the loop that was left holding them together.
    ///
    /// Stops at the thumbnail: a row carries the file to decode, not the decoded image, because a
    /// BitmapImage binds to Windows.UI.Xaml. See <see cref="LibraryRow.ThumbnailSource"/>.
    ///
    /// Two entry points rather than one because the widget shows the third-party half before the
    /// first-party pass runs - that pass asks the Store's CDN about each game it has not seen before,
    /// which on a first run is long enough that holding back a library already sitting in memory makes
    /// the whole widget look stuck. The <c>FixLog</c> run that brackets both, and the SteamGridDB
    /// client both share, are owned by the caller for the same reason.
    /// </summary>
    internal static class LibraryLoader
    {
        /// <summary>
        /// What a game with no name is called. A third-party entry is named by its store ID, and some
        /// stores' IDs are opaque; a first-party game whose Store lookup failed has nothing at all.
        /// </summary>
        internal const string UnknownName = "Unknown";

        private const string imageExtension = ".png";
        private const string manifestFileExtension = ".manifest";

        /// <summary>
        /// The third-party half of a library load.
        /// </summary>
        internal readonly struct ThirdPartyLoad
        {
            internal ThirdPartyLoad(List<LibraryRow> rows, int staleEntryCount)
            {
                Rows = rows;
                StaleEntryCount = staleEntryCount;
            }

            /// <summary>The rows, sorted the way the library shows them.</summary>
            internal List<LibraryRow> Rows { get; }

            /// <summary>
            /// Manifest entries the Xbox app left behind for removed games: no image and no backup, so
            /// there is nothing to show and nothing any of the buttons could act on.
            /// </summary>
            internal int StaleEntryCount { get; }
        }

        /// <summary>
        /// The Xbox app's ThirdPartyLibraries folder.
        /// </summary>
        /// <returns>The folder, or null when access was denied.</returns>
        /// <exception cref="DirectoryNotFoundException">The folder is not there at all, which means no
        /// third-party games have ever been added to the Xbox app - a different thing to say than
        /// "access denied", and the two are the only reasons a load reports nothing.</exception>
        internal static async Task<StorageFolder> ThirdPartyLibrariesFolderAsync()
        {
            try
            {
                // Try to get folder directly with broadFileSystemAccess permission
                return await StorageFolder.GetFolderFromPathAsync(XboxAppData.ThirdPartyLibrariesPath);
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied - user needs to grant permission in Windows Settings
                return null;
            }
            catch (FileNotFoundException)
            {
                // Directory doesn't exist
                throw new DirectoryNotFoundException($"ThirdPartyLibraries folder not found at: {XboxAppData.ThirdPartyLibrariesPath}");
            }
            catch
            {
                // Other error
                return null;
            }
        }

        /// <summary>
        /// Every third-party game the Xbox app's manifests describe, as rows.
        /// </summary>
        /// <param name="root">The ThirdPartyLibraries folder.</param>
        /// <param name="sgdbClient">The load's SteamGridDB client, or null when there is no API key.</param>
        /// <param name="canQuerySteamGridDb">Whether SteamGridDB can be queried at all.</param>
        /// <param name="report">Where to say what is happening.</param>
        internal static async Task<ThirdPartyLoad> ThirdPartyRowsAsync(
            StorageFolder root,
            SteamGridDbClient sgdbClient,
            bool canQuerySteamGridDb,
            Func<string, Task> report)
        {
            IReadOnlyList<StorageFolder> folders = await root.GetFoldersAsync();

            string directoryNames = string.Join(", ", folders.Select(f => f.Name));

            await report($"Found {OperationReport.Plural(folders.Count, "directory", "directories")} ({directoryNames}). Loading and sorting...");

            List<LibraryRow> rows = new List<LibraryRow>();
            int staleEntryCount = 0;

            foreach (StorageFolder folder in folders)
            {
                GamePlatform platform = GamePlatformHelper.FromXboxDirectory(folder.Name);

                if (platform == GamePlatform.BattleNet)
                {
                    // Skip Battle.net folder as it is not currently supported - Xbox app does not store images here
                    continue;
                }

                staleEntryCount += await AddManifestFolderRowsAsync(folder, platform, sgdbClient, canQuerySteamGridDb, rows);
            }

            return new ThirdPartyLoad(SortedByName(rows), staleEntryCount);
        }

        /// <summary>
        /// Reads one Xbox app ThirdPartyLibraries folder's manifest and adds its rows to
        /// <paramref name="rows"/> - the per-folder body of <see cref="ThirdPartyRowsAsync"/>'s folder
        /// loop, kept separate so that loop reads as "for each folder, add its entries" rather than
        /// nesting the manifest read, the manifest's own entries and their per-entry try/catch inside
        /// the same method as everything else.
        /// </summary>
        /// <param name="folder">The Xbox app folder to read.</param>
        /// <param name="platform">The folder's platform, already derived from its name.</param>
        /// <param name="sgdbClient">The load's SteamGridDB client, or null when there is no API key.</param>
        /// <param name="canQuerySteamGridDb">Whether SteamGridDB can be queried at all.</param>
        /// <param name="rows">Collects every row this folder produced.</param>
        /// <returns>How many of this folder's manifest entries were stale.</returns>
        private static async Task<int> AddManifestFolderRowsAsync(
            StorageFolder folder,
            GamePlatform platform,
            SteamGridDbClient sgdbClient,
            bool canQuerySteamGridDb,
            List<LibraryRow> rows)
        {
            int staleCount = 0;
            string manifestFileName = $"{folder.Name}{manifestFileExtension}";

            try
            {
                // Try to get the manifest file
                StorageFile manifestFile = await folder.GetFileAsync(manifestFileName);

                // Read and parse the manifest JSON file
                string jsonContent = await FileIO.ReadTextAsync(manifestFile);

                foreach ((string entryId, JsonObject entryObject) in ManifestGameCache.Entries(jsonContent))
                {
                    // Scoped to this one entry, not to the folder. The folder-level
                    // catch below used to be the only one, so anything that threw part
                    // way through a manifest - a file removed between the lookup that
                    // found it and the read that opens it, a rendition the Xbox app has
                    // locked - silently discarded every remaining entry in that folder,
                    // uncounted and unlogged. That is the same failure the "id" comment
                    // in ParseManifestEntryAsync describes shipping once already;
                    // hardening one JSON read fixed that instance rather than the
                    // shape, so any other throw still reproduced it.
                    try
                    {
                        LibraryRow parsed = await ParseManifestEntryAsync(
                            entryObject, platform, entryId, folder, sgdbClient, canQuerySteamGridDb);

                        if (parsed == null)
                        {
                            staleCount++;
                        }
                        else
                        {
                            rows.Add(parsed);
                        }
                    }
                    catch (Exception ex)
                    {
                        // One entry that could not be read is one game missing, not the
                        // rest of the folder. Named in the load log for the same reason
                        // the stale entries are: a library that silently shrinks is
                        // worse than one that says why.
                        FixLog.Write($"not shown {platform}/{entryId} from {folder.Name} ({ex.GetType().Name}: {ex.Message})");

                        System.Diagnostics.Debug.WriteLine($"Skipping entry {entryId} in {folder.Name}: {ex.Message}");
                    }
                }
            }
            catch (FileNotFoundException)
            {
                // Manifest file doesn't exist in this directory, skip it
            }
            catch (Exception ex)
            {
                // Log error but continue processing other directories
                System.Diagnostics.Debug.WriteLine($"Error processing {folder.Name}: {ex.Message}");
            }

            return staleCount;
        }

        /// <summary>
        /// Rows sorted the way the library shows them - alphabetically, with unnamed games last.
        /// </summary>
        /// <param name="rows">Rows to sort.</param>
        internal static List<LibraryRow> SortedByName(IEnumerable<LibraryRow> rows)
        {
            return rows
                .OrderBy(g => g.Name == UnknownName ? 1 : 0)
                .ThenBy(g => g.Name)
                .ToList();
        }

        /// <summary>
        /// Turns one manifest entry into a library row, or null when it is stale.
        /// </summary>
        /// <param name="entryObject">The manifest entry's JSON object.</param>
        /// <param name="platform">The entry's platform.</param>
        /// <param name="entryId">The entry's own "id" field, already known to be non-empty.</param>
        /// <param name="folder">The Xbox app folder the manifest was read from.</param>
        /// <param name="sgdbClient">The load's SteamGridDB client, or null when there is no API key.</param>
        /// <param name="canQuerySteamGridDb">Whether SteamGridDB can be queried at all.</param>
        /// <returns>The row, or null when the entry has nothing on disk to show or act on.</returns>
        private static async Task<LibraryRow> ParseManifestEntryAsync(
            JsonObject entryObject,
            GamePlatform platform,
            string entryId,
            StorageFolder folder,
            SteamGridDbClient sgdbClient,
            bool canQuerySteamGridDb)
        {
            // Parse addedDate - it's stored as a string in JSON
            string addedDateString = JsonRead.String(entryObject, "addedDate") ?? "0";
            long timestamp = 0;

            if (!string.IsNullOrEmpty(addedDateString) && long.TryParse(addedDateString, out long parsedTimestamp))
            {
                timestamp = parsedTimestamp;
            }

            // The platform-dependent half of this entry's on-disk location - Custom's
            // full-path/folder-resolution branch, the standard from-ID path construction, and the
            // missing-image/has-backup "Not found" placeholder shape - lives in ManifestEntryImage so
            // it can be tested directly instead of only by inspection here. Returns null for every
            // stale-entry reason (see its own doc comment); the caller's own accounting
            // (staleEntryCount) stays there because it is a caller-local summary, not part of the
            // resolution itself.
            ManifestEntryImage.Result? imageLocation = await ManifestEntryImage.ResolveAsync(
                entryObject, platform, entryId, folder, XboxAppData.ThirdPartyLibrariesPath, imageExtension);

            if (imageLocation == null)
            {
                // The one place these entries are ever named. Answering "why is this game not in my
                // widget" previously meant reading the Xbox app's manifests off disk and deriving each
                // entry's expected image path by hand, because the load reported only a count. The
                // folder is named as well as the platform: two folders can map to the same platform
                // when the Xbox app has renamed one and left the old one behind, and knowing which of
                // them an entry came from is most of the answer when it happens.
                FixLog.Write($"not shown {platform}/{entryId} from {folder.Name} (no artwork on disk, no backup)");

                return null;
            }

            // The store's own game ID, as SteamGridDB knows it, and the default display name: derived
            // by ManifestEntryIdentity, which owns the platform-specific rules (Custom's
            // title/installLocation/executableName reads; every other platform's id-prefix strip;
            // Epic's further split into an appName and a separately-kept catalog item ID) so they are
            // testable on their own rather than only by inspection here.
            ManifestEntryIdentity.Result identity = ManifestEntryIdentity.Derive(entryObject, platform, entryId, UnknownName);

            // The SGDB platform-ID match attempt, GOG/Epic/Ubisoft store-name dispatch, and SGDB
            // name-search fallback for this not-yet-matched entry live in GameMatchResolver, so the
            // platform-to-store dispatch decision and the FixLog line format are testable directly
            // instead of only by inspection here. Every network call still runs in exactly the order,
            // count and condition it always did.
            GameMatchResolver.Result match = await GameMatchResolver.ResolveAsync(
                sgdbClient,
                canQuerySteamGridDb,
                platform,
                identity.ExternalPlatformId,
                identity.EpicCatalogItemId,
                entryId,
                identity.GameName,
                UnknownName);

            return new LibraryRow
            {
                Name = match.GameName,
                ExternalPlatformId = identity.ExternalPlatformId,
                ImageFileName = imageLocation.Value.ImageFileName,
                ImageFilePath = imageLocation.Value.ImageFilePath,
                ImageFolder = imageLocation.Value.ImageFolder,
                ThumbnailSource = imageLocation.Value.ExistingImageFile,
                Platform = platform,
                AddedDate = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime,
                HasBackup = imageLocation.Value.HasBackup,
                HasSteamGridDBMatch = match.HasSteamGridDbMatch,
                OfficialCapsuleUrl = match.OfficialCapsuleUrl,
                SteamGridDbGameId = match.SteamGridDbGameId
            };
        }

        /// <summary>
        /// The Xbox app's own games as library rows.
        ///
        /// The third-party half of the load reads a manifest that names every game and points at a file
        /// per game. There is no equivalent for the games the Xbox app installed itself: they are found
        /// by enumerating what is installed, asking the Store what each one is, and locating its tiles
        /// in the app's image cache by what they look like. All of that lives in
        /// <see cref="XboxLibrary"/>; what is left here is the same shape as the per-entry work above -
        /// tile, SteamGridDB match, row - plus putting back any customisation the Xbox app has
        /// overwritten since the last load, which for these is routine rather than a repair.
        ///
        /// Nothing here can fail the library load. A machine with no Store games, an unreachable
        /// catalogue and an Xbox app that has never rendered a tile all produce an empty list, which is
        /// simply an empty section.
        /// </summary>
        /// <param name="sgdbClient">The load's SteamGridDB client, or null when there is no API key.</param>
        /// <param name="canQuerySteamGridDb">Whether SteamGridDB can be queried at all.</param>
        /// <param name="report">Where to say what is happening.</param>
        internal static async Task<List<LibraryRow>> XboxRowsAsync(
            SteamGridDbClient sgdbClient,
            bool canQuerySteamGridDb,
            Func<string, Task> report)
        {
            List<LibraryRow> rows = new List<LibraryRow>();

            try
            {
                StorageFolder cacheFolder = await ImageCacheIndex.GetCacheFolderAsync();

                if (cacheFolder == null)
                {
                    return rows;
                }

                await report("Looking for Xbox app games...");

                List<XboxLibrary.Game> games = await XboxLibrary.LoadAsync(cacheFolder);

                FixLog.Write($"Xbox app library: {XboxLibrary.LoadSummary}");

                // Listed once for the whole pass rather than probed per rendition per game - see
                // XboxTileStore.VaultFileNamesAsync. Nothing writes to the vault while this runs: the
                // operations that do are single-game actions the library-operation guard holds off
                // for the duration of a load.
                HashSet<string> vaultFileNames = await XboxTileStore.VaultFileNamesAsync();

                foreach (XboxLibrary.Game game in games)
                {
                    // Before the tile is located, so the row shows what is actually on the tile.
                    // Outside the row-building try below, deliberately: a customisation that cannot be
                    // written back this load is a stale tile, not a missing game, and the row is what
                    // carries the Restore button that can still act on it. ReapplyOverwrittenAsync
                    // already contains refusals per rendition; this guards what remains around them.
                    try
                    {
                        await XboxTiles.ReapplyOverwrittenAsync(cacheFolder, game.RenditionFileNames, vaultFileNames);
                    }
                    catch (Exception ex)
                    {
                        FixLog.Write($"reapply skipped Xbox/{game.StoreId} ({ex.GetType().Name}: {ex.Message})");
                    }

                    // Scoped to this one game, for the same reason the third-party walk's is: one
                    // unreadable tile or failed lookup must not take every remaining first-party
                    // game with it.
                    try
                    {
                        string primaryFileName = game.PrimaryFileName;
                        StorageFile tile = null;

                        try
                        {
                            tile = await cacheFolder.GetFileAsync(primaryFileName);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Could not open the tile for {game.Title}: {ex.Message}");
                        }

                        // GamePlatform.Xbox has no SteamGridDB platform of its own, so this always takes the
                        // name-search path - which is the good one here, because unlike the ID-derived names
                        // the third-party manifests give, the Store's title is the game's actual title
                        GameMatchResolver.Result match = await GameMatchResolver.ResolveAsync(
                            sgdbClient,
                            canQuerySteamGridDb,
                            GamePlatform.Xbox,
                            game.StoreId,
                            null,
                            game.StoreId,
                            string.IsNullOrEmpty(game.Title) ? UnknownName : game.Title,
                            UnknownName);

                        bool hasBackup = XboxTiles.HasBackup(game.RenditionFileNames, vaultFileNames);

                        if (!hasBackup)
                        {
                            // Applying artwork always leaves a backup behind it, so a game without one is
                            // not customised whatever the applied-artwork record says - see
                            // XboxTiles.ForgetArtworkRecordsAsync. Reached from here rather than from
                            // XboxLibrary because this is where the backup is already looked up, and
                            // asking twice per game is the only cost worth avoiding here.
                            await XboxTiles.ForgetArtworkRecordsAsync(cacheFolder, game.RenditionFileNames);
                        }

                        rows.Add(new LibraryRow
                        {
                            Name = match.GameName,
                            ExternalPlatformId = game.StoreId,
                            ImageFileName = primaryFileName,
                            ImageFilePath = Path.Combine(cacheFolder.Path, primaryFileName),
                            ImageFolder = cacheFolder,
                            ThumbnailSource = tile,
                            Platform = GamePlatform.Xbox,
                            XboxRenditions = game.RenditionFileNames,
                            HasBackup = hasBackup,
                            HasSteamGridDBMatch = match.HasSteamGridDbMatch,
                            OfficialCapsuleUrl = match.OfficialCapsuleUrl,
                            SteamGridDbGameId = match.SteamGridDbGameId
                        });
                    }
                    catch (Exception ex)
                    {
                        FixLog.Write($"not shown Xbox/{game.StoreId} ({ex.GetType().Name}: {ex.Message})");

                        System.Diagnostics.Debug.WriteLine($"Skipping Xbox app game {game.Title}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                FixLog.Write($"Xbox app library could not be read: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Could not load Xbox app games: {ex.Message}");
            }

            return SortedByName(rows);
        }
    }
}
