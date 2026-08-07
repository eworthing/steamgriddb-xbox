using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.ViewManagement.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services;
using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Library;
using SteamGridDB.Xbox.Services.SteamGridDB;
using SteamGridDB.Xbox.Services.SteamGridDB.Models;
using SteamGridDB.Xbox.Services.Stores;
using SteamGridDB.Xbox.Services.Xbox;

namespace SteamGridDB.Xbox
{
    /// <summary>
    /// Primary widget page that loads and displays Xbox app third-party games.
    /// </summary>
    public sealed partial class PrimaryWidget : Page, INotifyPropertyChanged
    {
        /// <summary>
        /// Every row in the library, flat. This is what all the logic works from - deduplicating by
        /// image, choosing which games a bulk run visits, stamping a written image onto the rows that
        /// share it - because none of that cares which section a row is displayed under.
        /// </summary>
        public ObservableCollection<GameEntry> GameEntries
        {
            get; set;
        }

        /// <summary>
        /// The same rows, grouped for display. Holds the identical <see cref="GameEntry"/> instances
        /// rather than copies, so everything the logic changes on a row reaches the UI on its own and
        /// only the initial fill has to touch both collections.
        /// </summary>
        public ObservableCollection<GameEntrySection> GameSections
        {
            get; set;
        }

        private readonly GameEntrySection xboxSection = new GameEntrySection("Xbox app library");
        private readonly GameEntrySection thirdPartySection = new GameEntrySection("Third-party libraries");

        private readonly string steamGridDbApiKey = Environment.GetEnvironmentVariable("STEAMGRIDDB_API_KEY");
        private readonly string thirdPartyLibrariesPath = XboxAppData.ThirdPartyLibrariesPath;
        private const string unknownName = "Unknown";
        private const string imageExtension = ".png";
        private const string manifestFileExtension = ".manifest";
        private const string busyStatusText = "Another library operation is still running - please wait for it to finish";

        // Artwork is shown in an 80px list thumbnail; decoding the 512-1024px source at full size would
        // hold tens of megabytes of bitmaps for a large library. 160px covers 2x display scaling.
        private const int thumbnailDecodePixelWidth = 160;

        private enum RestoreBackupResult
        {
            Restored,
            BackupMissing,
            Error
        }

        // Buttons to restore focus to once their panel closes. Each panel owns exactly one of these:
        // EditGameImage_Click sets gridPanelFocusRestoreTarget for the grid picker; SearchGameImage_Click
        // sets searchPanelFocusRestoreTarget for the search panel. The one exception is SearchResult_Click,
        // which hands searchPanelFocusRestoreTarget over to gridPanelFocusRestoreTarget before opening the
        // grid picker for the chosen result - focus should return to the button that originally opened
        // the search panel once the grid picker (opened next, not the search panel) eventually closes.
        // That handoff is the only place either field is written by anything other than its own panel's
        // own open handler or its own panel's own close handler - see the handoff's own comment.
        private Button gridPanelFocusRestoreTarget;
        private Button searchPanelFocusRestoreTarget;

        // Guards the library-wide operations against each other - they all rewrite the same files and
        // rebuild the same collection, so overlapping runs duplicate entries or race on disk - AND
        // against a single-game write (GridImage_Click, RestoreBackup_Click): those also write files a
        // library-wide reload can be mid-rebuild of, and TryBeginLibraryOperation/EndLibraryOperation
        // wrap both kinds of caller identically so the two block each other. The guard's own
        // begin/end/is-running rule lives in LibraryOperationGuard, not here - see its doc comment for
        // why: this class binds to Windows.UI.Xaml and has no desktop test projection, so the rule
        // could not be tested in place.
        private readonly LibraryOperationGuard libraryOperationGuard = new LibraryOperationGuard();

        // Incremented every time the artwork picker is (re)populated - by EditGameImage_Click,
        // SearchGameImage_Click or a search result choosing a game - and stamped onto every
        // GridImageItem that population creates (see PopulateGridSelectionPanelAsync). Opening the
        // picker for a different game does not wait for the previous one to finish: ShowGridPanelAsync
        // alone takes ~250ms, during which the previous game's tiles are still on screen and clickable
        // while CurrentSelectedGame may already point at the new game. Without this, clicking one of
        // those stale tiles would write its artwork to the wrong game with no error. GridImage_Click
        // compares a clicked tile's stamp against this field and ignores the click when they differ.
        private int gridPanelSessionId;

        // Same shape as gridPanelSessionId, one screen upstream: PerformGameSearchAsync can be
        // triggered again (Enter key or the Search button) before a prior search's network round trip
        // has returned, and ShowSearchPanelAsync can be reopened for the same or a different game while
        // one is still in flight - neither is blocked by anything but the bulk-operation gate. Bumped by
        // both, so a stale search's completion is discarded whether it is superseded by a new search or
        // by the panel being shown again. Checked in PerformGameSearchAsync before any result-list write.
        private int searchPanelSessionId;

        // Guards HideGridPanelAsync/HideSearchPanelAsync against running twice at once for the same
        // session: each panel's own Close button and its own successful-download auto-close
        // (DownloadAndReplaceImageAsync's call at the end of a successful download) both call the same
        // Hide method, and nothing stops a user closing the panel by hand while their own click's
        // download is still in flight from the auto-close arriving a moment later. The session field
        // above already guards against a *different* session's stale close finishing late; it does
        // nothing for two closes of the *same* session overlapping, which would run the slide-down
        // animation and the visibility/selection teardown twice. Reuses LibraryOperationGuard's own
        // TryBegin/End shape rather than a fourth hand-rolled bool flag - see its own doc comment.
        private readonly LibraryOperationGuard gridPanelCloseGuard = new LibraryOperationGuard();
        private readonly LibraryOperationGuard searchPanelCloseGuard = new LibraryOperationGuard();

        // Whitespace counts as missing, matching SteamGridDbClient's own validation - a key that fails
        // this test would otherwise sail past the guards and throw out of the client's constructor.
        private bool HasSteamGridDbApiKey => !string.IsNullOrWhiteSpace(steamGridDbApiKey);

        private GameEntry currentSelectedGame;
        public GameEntry CurrentSelectedGame
        {
            get => currentSelectedGame;
            set
            {
                if (currentSelectedGame != value)
                {
                    currentSelectedGame = value;
                    OnPropertyChanged(nameof(CurrentSelectedGame));
                }
            }
        }

        private string gridPanelHeaderText;
        public string GridPanelHeaderText
        {
            get => gridPanelHeaderText;
            set
            {
                if (gridPanelHeaderText != value)
                {
                    gridPanelHeaderText = value;
                    OnPropertyChanged(nameof(GridPanelHeaderText));
                }
            }
        }

        private string searchPanelHeaderText;
        public string SearchPanelHeaderText
        {
            get => searchPanelHeaderText;
            set
            {
                if (searchPanelHeaderText != value)
                {
                    searchPanelHeaderText = value;
                    OnPropertyChanged(nameof(SearchPanelHeaderText));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public PrimaryWidget()
        {
            InitializeComponent();
            GameEntries = new ObservableCollection<GameEntry>();

            // Xbox app games first: they are the shorter half of the list, and the one whose artwork the
            // Xbox app can undo on its own, so it is the half worth having in view
            GameSections = new ObservableCollection<GameEntrySection> { xboxSection, thirdPartySection };
            GroupedGameEntries.Source = GameSections;

            Loaded += PrimaryWidget_Loaded;
        }

        private async void PrimaryWidget_Loaded(object sender, RoutedEventArgs e)
        {
            await RunUnderLibraryOperationGuardAsync(LoadGameEntriesAsync);

            // Set default focus to Fix my library button for controller navigation. Outside the guard
            // so a repeat Loaded - Game Bar re-parenting the widget - still lands focus somewhere.
            FixLibraryButton.Focus(FocusState.Programmatic);
        }

        private async Task<StorageFolder> GetThirdPartyLibrariesFolderAsync()
        {
            try
            {
                // Try to get folder directly with broadFileSystemAccess permission
                return await StorageFolder.GetFolderFromPathAsync(thirdPartyLibrariesPath);
            }
            catch (UnauthorizedAccessException)
            {
                // Access denied - user needs to grant permission in Windows Settings
                return null;
            }
            catch (FileNotFoundException)
            {
                // Directory doesn't exist
                throw new DirectoryNotFoundException($"ThirdPartyLibraries folder not found at: {thirdPartyLibrariesPath}");
            }
            catch
            {
                // Other error
                return null;
            }
        }

        /// <summary>
        /// True while a library-wide operation is in flight. The per-game buttons check this because
        /// disabling the header is not enough: restoring or replacing one game's artwork from a row
        /// while a bulk pass is rewriting the same files is the same concurrent-writer race.
        /// </summary>
        private bool IsLibraryOperationBlocking()
        {
            if (!libraryOperationGuard.IsRunning)
            {
                return false;
            }

            StatusText.Text = busyStatusText;

            return true;
        }

        /// <summary>
        /// Marks the start of a library-wide operation and disables the header buttons for its duration.
        /// Returns false when another operation is already running.
        /// </summary>
        private bool TryBeginLibraryOperation()
        {
            if (!libraryOperationGuard.TryBegin())
            {
                StatusText.Text = busyStatusText;

                return false;
            }

            SetHeaderButtonsEnabled(false);

            return true;
        }

        /// <summary>
        /// Marks the end of a library-wide operation and re-enables the header buttons.
        /// </summary>
        private void EndLibraryOperation()
        {
            libraryOperationGuard.End();
            SetHeaderButtonsEnabled(true);
        }

        /// <summary>
        /// Runs <paramref name="action"/> under the library-operation guard, doing nothing when another
        /// library operation is already running. Owns the TryBeginLibraryOperation/EndLibraryOperation
        /// pairing that PrimaryWidget_Loaded, RefreshButton_Click, GridImage_Click, RestoreBackup_Click
        /// and ConfirmAndRunAsync each used to duplicate - the same shape SlidePanelAsync and
        /// ConfirmAndRunAsync itself already consolidated for their own repeated ceremony.
        /// </summary>
        /// <param name="action">The guarded operation to run.</param>
        private async Task RunUnderLibraryOperationGuardAsync(Func<Task> action)
        {
            if (!TryBeginLibraryOperation())
            {
                return;
            }

            try
            {
                await action();
            }
            finally
            {
                EndLibraryOperation();
            }
        }

        private void SetHeaderButtonsEnabled(bool enabled)
        {
            FixLibraryButton.IsEnabled = enabled;
            RestoreChangesButton.IsEnabled = enabled;
            RevertDefaultsButton.IsEnabled = enabled;
            RefreshButton.IsEnabled = enabled;
        }

        /// <summary>
        /// Shows a line in the status bar, from whichever thread happens to be running.
        ///
        /// Most of this page's work runs off the UI thread - file writes, downloads, decoding - and
        /// reports as it goes, so nearly every status update needed the same four-line dispatcher
        /// block around it. Forgetting one is not a compile error and not obviously a runtime one
        /// either: it throws only when that particular branch is reached from a background thread.
        /// </summary>
        /// <param name="text">The line to show.</param>
        private async Task SetStatusAsync(string text)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StatusText.Text = text;
            });
        }

        /// <summary>
        /// Runs work on the UI thread, for the updates that touch more than the status bar - entry
        /// properties the list is bound to, and the controls' own state.
        /// </summary>
        /// <param name="update">Work to run on the UI thread.</param>
        private async Task OnUiThreadAsync(Action update)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => update());
        }

        /// <summary>
        /// Decodes a game image at list-thumbnail size on the UI thread and releases the file handle
        /// as soon as decoding finishes.
        /// </summary>
        /// <param name="file">Image file to decode.</param>
        /// <returns>The decoded image, or null when it could not be decoded.</returns>
        private async Task<BitmapImage> CreateThumbnailAsync(StorageFile file)
        {
            IRandomAccessStream imageStream = await file.OpenReadAsync();

            try
            {
                // Every caller reaches this from a UI event handler, so decoding happens inline - no
                // dispatcher round trip that could leave the await hanging if the handler never runs
                if (Dispatcher.HasThreadAccess)
                {
                    return await DecodeThumbnailAsync(file, imageStream);
                }

                // BitmapImage must be created and sourced on the UI thread because it is owned by it
                TaskCompletionSource<BitmapImage> decoded = new TaskCompletionSource<BitmapImage>(TaskCreationOptions.RunContinuationsAsynchronously);

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    decoded.TrySetResult(await DecodeThumbnailAsync(file, imageStream));
                });

                return await decoded.Task;
            }
            finally
            {
                imageStream.Dispose();
            }
        }

        /// <summary>
        /// Decodes an already-open image stream at thumbnail size. Must run on the UI thread.
        /// </summary>
        /// <returns>The decoded image, or null when it could not be decoded.</returns>
        private static async Task<BitmapImage> DecodeThumbnailAsync(StorageFile file, IRandomAccessStream imageStream)
        {
            try
            {
                BitmapImage image = new BitmapImage { DecodePixelWidth = thumbnailDecodePixelWidth };

                await image.SetSourceAsync(imageStream);

                return image;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not decode {file.Name}: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        /// All entries backed by the same image file. Stale Xbox app manifests list one image under
        /// several entries, and the bulk operations process each image once - without this the
        /// duplicate rows keep showing the previous artwork and buttons until the next refresh.
        /// </summary>
        private List<GameEntry> EntriesSharingImage(GameEntry game)
        {
            return GameImages.SharingImage(GameEntries, game, g => g.ImageFilePath);
        }

        /// <summary>
        /// Applies a written image to every row sharing it and reports the outcome, on the UI thread.
        /// Shared by <see cref="ReplaceImageCoreAsync"/>, <see cref="RestoreAllChangesAsync"/> and
        /// <see cref="RestoreBackupCoreAsync"/>, which previously each hand-built this same
        /// dispatch/foreach/status-text block - the three had already drifted apart once on which
        /// fields they touched, which is what three copies of the same shape cost.
        /// </summary>
        /// <param name="game">Game whose written image this is - looked up by shared image path via <see cref="EntriesSharingImage"/>.</param>
        /// <param name="imageFileName">File name to stamp onto every shared entry.</param>
        /// <param name="image">Decoded image to stamp onto every shared entry.</param>
        /// <param name="hasBackup">New backup-exists value to stamp onto every shared entry, or null to leave it untouched (<see cref="RestoreAllChangesAsync"/> does not know this at this point in its flow).</param>
        /// <param name="statusText">Status bar text to show, or null to leave the status bar as it is.</param>
        private async Task UpdateSharedEntriesAsync(GameEntry game, string imageFileName, BitmapImage image, bool? hasBackup, string statusText)
        {
            await OnUiThreadAsync(() =>
            {
                foreach (GameEntry entry in EntriesSharingImage(game))
                {
                    entry.Image = image;
                    entry.ImageFileName = imageFileName;

                    if (hasBackup.HasValue)
                    {
                        entry.HasBackup = hasBackup.Value;
                    }
                }

                if (statusText != null)
                {
                    StatusText.Text = statusText;
                }
            });
        }

        /// <summary>
        /// The thumbnail to show after a write, or the one the row already has when the file it would
        /// be decoded from is not there.
        ///
        /// A third-party game is the one file that was just written, so it is always present. A
        /// first-party game is several and <see cref="GameEntry.ImageFilePath"/> names only the largest,
        /// which the Xbox app may have evicted - the smaller renditions were still written and the
        /// operation still succeeded, so failing to re-read that one file must not report it as failed.
        /// </summary>
        /// <param name="game">Game whose image was written.</param>
        /// <param name="imageFileName">Name of the image to decode.</param>
        private async Task<BitmapImage> WrittenThumbnailAsync(GameEntry game, string imageFileName)
        {
            try
            {
                return await CreateThumbnailAsync(await game.ImageFolder.GetFileAsync(imageFileName));
            }
            catch (FileNotFoundException)
            {
                return game.Image;
            }
        }

        /// <summary>
        /// The games a bulk run should visit: those matching <paramref name="eligible"/>, one per image.
        /// </summary>
        /// <param name="eligible">Which entries the operation applies to.</param>
        private List<GameEntry> GamesToProcess(Func<GameEntry, bool> eligible)
        {
            return GameImages.DistinctByImage(GameEntries.Where(eligible), g => g.ImageFilePath);
        }

        /// <summary>
        /// The name to show for a game in progress and status lines - its own, or the image file it is
        /// backed by when the manifests never gave it one.
        /// </summary>
        private string DisplayName(GameEntry game)
        {
            return game.Name != unknownName ? game.Name : Path.GetFileName(game.ImageFilePath);
        }

        private async Task LoadGameEntriesAsync()
        {
            try
            {
                await OnUiThreadAsync(() =>
                {
                    // Clear here rather than in the callers so that a repeated load can never append
                    // a second copy of the library to the list
                    GameEntries.Clear();
                    xboxSection.Clear();
                    thirdPartySection.Clear();

                    FixLog.Start("Library load", "last-load.log");

                    StatusText.Text = $"Attempting to access ThirdPartyLibraries...";
                    InstructionsPanel.Visibility = Visibility.Collapsed;
                    GameEntriesListView.Visibility = Visibility.Visible;
                });

                StorageFolder thirdPartyFolder = null;

                try
                {
                    thirdPartyFolder = await GetThirdPartyLibrariesFolderAsync();
                }
                catch (DirectoryNotFoundException)
                {
                    await OnUiThreadAsync(() =>
                    {
                        StatusText.Text = "ThirdPartyLibraries folder was not found. Make sure games are added to the Xbox app.";
                        GameEntriesListView.Visibility = Visibility.Collapsed;
                    });

                    return;
                }

                if (thirdPartyFolder == null)
                {
                    await OnUiThreadAsync(() =>
                    {
                        StatusText.Text = "Access denied. Please grant file system permission.";
                        InstructionsPanel.Visibility = Visibility.Visible;
                        GameEntriesListView.Visibility = Visibility.Collapsed;
                    });

                    return;
                }

                // Get all subdirectories
                var folders = await thirdPartyFolder.GetFoldersAsync();

                string directoryNames = string.Join(", ", folders.Select(f => f.Name));

                await SetStatusAsync($"Found {folders.Count} director{(folders.Count == 1 ? "y" : "ies")} ({directoryNames}). Loading and sorting...");

                // Temporary list to collect games before sorting
                List<GameEntry> tmpGameList = new List<GameEntry>();

                // Manifest entries the Xbox app left behind for removed games: no image and no backup,
                // so there is nothing to show and nothing any of the buttons could act on
                int staleEntryCount = 0;

                // The Xbox app's own games, which come from somewhere else entirely - see
                // LoadXboxGameEntriesAsync
                List<GameEntry> xboxGames = new List<GameEntry>();

                // Without an API key the library still loads - names stay "Unknown" and artwork cannot be
                // fetched, but the list, the backups and the restore/revert buttons all keep working
                bool canQuerySteamGridDb = HasSteamGridDbApiKey;
                SteamGridDbClient sgdbClient = canQuerySteamGridDb ? new SteamGridDbClient(steamGridDbApiKey) : null;

                try
                {
                    foreach (StorageFolder folder in folders)
                    {
                        GamePlatform platform = GamePlatformHelper.FromXboxDirectory(folder.Name);

                        if (platform == GamePlatform.BattleNet)
                        {
                            // Skip Battle.net folder as it is not currently supported - Xbox app does not store images here
                            continue;
                        }

                        string manifestFileName = $"{folder.Name}{manifestFileExtension}";

                        try
                        {
                            // Try to get the manifest file
                            StorageFile manifestFile = await folder.GetFileAsync(manifestFileName);

                            // Read and parse the manifest JSON file
                            string jsonContent = await FileIO.ReadTextAsync(manifestFile);

                            if (JsonObject.TryParse(jsonContent, out JsonObject root))
                            {
                                // Check if gameCache exists in the root
                                if (!root.ContainsKey("gameCache"))
                                {
                                    continue;
                                }

                                // Get the gameCache object
                                if (root.GetNamedValue("gameCache").ValueType != JsonValueType.Object)
                                {
                                    continue;
                                }

                                JsonObject gameCache = root.GetNamedObject("gameCache");

                                // Iterate through all entries in the gameCache
                                foreach (KeyValuePair<string, IJsonValue> entry in gameCache)
                                {
                                    // Skip the "version" property if it exists
                                    if (entry.Key == "version")
                                    {
                                        continue;
                                    }

                                    // Only process entries that are objects
                                    if (entry.Value.ValueType != JsonValueType.Object)
                                    {
                                        continue;
                                    }

                                    JsonObject entryObject = entry.Value.GetObject();

                                    // Get the ID from the "id" property (not from the key). JsonRead
                                    // treats a present-but-JSON-null "id" the same as a missing one -
                                    // the raw GetNamedString/ContainsKey pair below did not: ContainsKey
                                    // returns true for a null-valued member, and GetNamedString throws
                                    // InvalidOperationException on one, which nothing here caught, so a
                                    // single null "id" anywhere in a folder's manifest silently dropped
                                    // every entry after it in that folder (see JsonRead.cs's docstring
                                    // for the same failure class shipping once already).
                                    string entryId = JsonRead.String(entryObject, "id");

                                    // Only process entries that have an "id" property
                                    if (string.IsNullOrEmpty(entryId))
                                    {
                                        continue;
                                    }

                                    // Parse addedDate - it's stored as a string in JSON
                                    string addedDateString = JsonRead.String(entryObject, "addedDate") ?? "0";
                                    long timestamp = 0;

                                    if (!string.IsNullOrEmpty(addedDateString) && long.TryParse(addedDateString, out long parsedTimestamp))
                                    {
                                        timestamp = parsedTimestamp;
                                    }

                                    // The platform-dependent half of this entry's on-disk location -
                                    // Custom's full-path/folder-resolution branch, the standard
                                    // from-ID path construction, and the missing-image/has-backup
                                    // "Not found" placeholder shape - lives in ManifestEntryImage so it
                                    // can be tested directly instead of only by inspection here. Returns
                                    // null for every stale-entry reason (see its own doc comment); this
                                    // loop's own accounting (staleEntryCount) stays here because it is a
                                    // caller-local summary, not part of the resolution itself.
                                    ManifestEntryImage.Result? imageLocation = await ManifestEntryImage.ResolveAsync(
                                        entryObject, platform, entryId, folder, thirdPartyLibrariesPath, imageExtension);

                                    if (imageLocation == null)
                                    {
                                        staleEntryCount++;

                                        continue;
                                    }

                                    string imageFilePath = imageLocation.Value.ImageFilePath;
                                    StorageFolder imageFolder = imageLocation.Value.ImageFolder;
                                    string imageFileName = imageLocation.Value.ImageFileName;
                                    bool hasBackup = imageLocation.Value.HasBackup;

                                    BitmapImage image = null;

                                    if (imageLocation.Value.ExistingImageFile != null)
                                    {
                                        image = await CreateThumbnailAsync(imageLocation.Value.ExistingImageFile);
                                    }

                                    // The store's own game ID, as SteamGridDB knows it, and the default
                                    // display name: derived by ManifestEntryIdentity, which owns the
                                    // platform-specific rules (Custom's title/installLocation/executableName
                                    // reads; every other platform's id-prefix strip; Epic's further split
                                    // into an appName and a separately-kept catalog item ID) so they are
                                    // testable on their own rather than only by inspection here.
                                    ManifestEntryIdentity.Result identity = ManifestEntryIdentity.Derive(entryObject, platform, entryId, unknownName);
                                    string gameName = identity.GameName;
                                    string externalPlatformId = identity.ExternalPlatformId;
                                    string epicCatalogItemId = identity.EpicCatalogItemId;

                                    // The SGDB platform-ID match attempt, GOG/Epic/Ubisoft store-name
                                    // dispatch, and SGDB name-search fallback for this not-yet-matched
                                    // entry live in GameMatchResolver, so the platform-to-store dispatch
                                    // decision and the FixLog line format are testable directly instead
                                    // of only by inspection here. Every network call still runs in
                                    // exactly the order, count and condition it always did.
                                    GameMatchResolver.Result match = await GameMatchResolver.ResolveAsync(
                                        sgdbClient, canQuerySteamGridDb, platform, externalPlatformId, epicCatalogItemId, entryId, gameName, unknownName);

                                    gameName = match.GameName;
                                    bool hasSteamGridDBMatch = match.HasSteamGridDbMatch;
                                    string officialCapsuleUrl = match.OfficialCapsuleUrl;
                                    int steamGridDbGameId = match.SteamGridDbGameId;

                                    // Add to temporary list instead of directly to GameEntries
                                    tmpGameList.Add(new GameEntry
                                    {
                                        Name = gameName,
                                        ExternalPlatformId = externalPlatformId,
                                        ImageFileName = imageFileName,
                                        ImageFilePath = imageFilePath,
                                        ImageFolder = imageFolder,
                                        Platform = platform,
                                        AddedDate = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime,
                                        Image = image,
                                        HasBackup = hasBackup,
                                        HasSteamGridDBMatch = hasSteamGridDBMatch,
                                        OfficialCapsuleUrl = officialCapsuleUrl,
                                        SteamGridDbGameId = steamGridDbGameId
                                    });
                                }
                            }
                        }
                        catch (FileNotFoundException)
                        {
                            // Manifest file doesn't exist in this directory, skip it

                            continue;
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue processing other directories
                            System.Diagnostics.Debug.WriteLine($"Error processing {folder.Name}: {ex.Message}");
                        }
                    }

                    // Sort games alphabetically by name, with "Unknown" at the end
                    List<GameEntry> sortedGames = tmpGameList
                        .OrderBy(g => g.Name == unknownName ? 1 : 0)
                        .ThenBy(g => g.Name)
                        .ToList();

                    // Shown before the first-party pass rather than after it. That pass has to ask the
                    // Store's CDN about each game it has not seen before, which is the one genuinely
                    // slow part of a load and happens only once per game - but on a first run it is
                    // long enough that holding back a library that is already sitting in memory makes
                    // the whole widget look stuck.
                    await OnUiThreadAsync(() =>
                    {
                        foreach (GameEntry game in sortedGames)
                        {
                            GameEntries.Add(game);
                            thirdPartySection.Add(game);
                        }

                        StatusText.Text = $"Found {sortedGames.Count} game{(sortedGames.Count == 1 ? string.Empty : "s")}"
                            + " - looking for Xbox app games...";
                    });

                    // Runs inside the same try so it shares the one SteamGridDB client the whole load
                    // uses, and after the third-party walk so a first-party failure cannot cost the
                    // library its main half
                    xboxGames = await LoadXboxGameEntriesAsync(sgdbClient, canQuerySteamGridDb);
                }
                finally
                {
                    sgdbClient?.Dispose();
                }

                await FixLog.SaveAsync();

                await OnUiThreadAsync(() =>
                {
                    foreach (GameEntry game in xboxGames)
                    {
                        GameEntries.Add(game);
                        xboxSection.Add(game);
                    }

                    string summary = $"Found {GameEntries.Count} game{(GameEntries.Count == 1 ? string.Empty : "s")}";

                    if (staleEntryCount > 0)
                    {
                        summary += $", skipped {staleEntryCount} stale manifest entr{(staleEntryCount == 1 ? "y" : "ies")}";
                    }

                    if (!canQuerySteamGridDb)
                    {
                        summary += " - SteamGridDB API key is not set, artwork cannot be fetched";
                    }

                    StatusText.Text = summary;
                });
            }
            catch (Exception ex)
            {
                await SetStatusAsync($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// The Xbox app's own games as library rows.
        ///
        /// The third-party half of the load reads a manifest that names every game and points at a file
        /// per game. There is no equivalent for the games the Xbox app installed itself: they are found
        /// by enumerating what is installed, asking the Store what each one is, and locating its tiles
        /// in the app's image cache by what they look like. All of that lives in
        /// <see cref="XboxLibrary"/>; what is left here is the same shape as the per-entry work above -
        /// thumbnail, SteamGridDB match, row - plus putting back any customisation the Xbox app has
        /// overwritten since the last load, which for these is routine rather than a repair.
        ///
        /// Nothing here can fail the library load. A machine with no Store games, an unreachable
        /// catalogue and an Xbox app that has never rendered a tile all produce an empty list, which is
        /// simply an empty section.
        /// </summary>
        /// <param name="sgdbClient">The load's SteamGridDB client, or null when there is no API key.</param>
        /// <param name="canQuerySteamGridDb">Whether SteamGridDB can be queried at all.</param>
        private async Task<List<GameEntry>> LoadXboxGameEntriesAsync(SteamGridDbClient sgdbClient, bool canQuerySteamGridDb)
        {
            List<GameEntry> entries = new List<GameEntry>();

            try
            {
                StorageFolder cacheFolder = await ImageCacheIndex.GetCacheFolderAsync();

                if (cacheFolder == null)
                {
                    return entries;
                }

                await SetStatusAsync("Looking for Xbox app games...");

                List<XboxLibrary.Game> games = await XboxLibrary.LoadAsync(cacheFolder);

                FixLog.Write($"Xbox app library: {XboxLibrary.LoadSummary}");

                foreach (XboxLibrary.Game game in games)
                {
                    // Before the thumbnail is decoded, so the row shows what is actually on the tile
                    await XboxTiles.ReapplyOverwrittenAsync(cacheFolder, game.RenditionFileNames);

                    string primaryFileName = game.PrimaryFileName;
                    BitmapImage image = null;

                    try
                    {
                        image = await CreateThumbnailAsync(await cacheFolder.GetFileAsync(primaryFileName));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Could not decode the tile for {game.Title}: {ex.Message}");
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
                        string.IsNullOrEmpty(game.Title) ? unknownName : game.Title,
                        unknownName);

                    entries.Add(new GameEntry
                    {
                        Name = match.GameName,
                        ExternalPlatformId = game.StoreId,
                        ImageFileName = primaryFileName,
                        ImageFilePath = Path.Combine(cacheFolder.Path, primaryFileName),
                        ImageFolder = cacheFolder,
                        Platform = GamePlatform.Xbox,
                        Image = image,
                        XboxRenditions = game.RenditionFileNames,
                        HasBackup = await XboxTiles.HasBackupAsync(game.RenditionFileNames),
                        HasSteamGridDBMatch = match.HasSteamGridDbMatch,
                        OfficialCapsuleUrl = match.OfficialCapsuleUrl,
                        SteamGridDbGameId = match.SteamGridDbGameId
                    });
                }
            }
            catch (Exception ex)
            {
                FixLog.Write($"Xbox app library could not be read: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Could not load Xbox app games: {ex.Message}");
            }

            return entries
                .OrderBy(g => g.Name == unknownName ? 1 : 0)
                .ThenBy(g => g.Name)
                .ToList();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RunUnderLibraryOperationGuardAsync(LoadGameEntriesAsync);
        }

        /// <summary>
        /// Shows a confirm/cancel (or confirm/alternate/cancel) dialog, then runs the chosen action
        /// under the library-operation guard via <see cref="RunUnderLibraryOperationGuardAsync"/>. Owns
        /// the dialog construction and the XamlRoot API-contract check that FixLibraryButton_Click,
        /// RestoreChangesButton_Click and RevertDefaultsButton_Click each used to duplicate.
        /// <paramref name="action"/> receives the dialog result so a caller with a secondary button
        /// (FixLibraryButton_Click) can still branch on which one was pressed.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="content">Dialog body text.</param>
        /// <param name="primaryButtonText">Primary button text.</param>
        /// <param name="secondaryButtonText">Secondary button text, or null/empty for a two-button dialog.</param>
        /// <param name="shouldRun">Whether <paramref name="action"/> should run for a given result.</param>
        /// <param name="action">The guarded operation to run when <paramref name="shouldRun"/> allows it.</param>
        private async Task ConfirmAndRunAsync(
            string title,
            string content,
            string primaryButtonText,
            string secondaryButtonText,
            Func<ContentDialogResult, bool> shouldRun,
            Func<ContentDialogResult, Task> action)
        {
            ContentDialog confirmDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = "Cancel",
                Style = Resources["DarkContentDialogStyle"] as Style,
                PrimaryButtonStyle = Resources["ContentDialogButtonStyle"] as Style,
                CloseButtonStyle = Resources["ContentDialogButtonStyle"] as Style
            };

            if (!string.IsNullOrEmpty(secondaryButtonText))
            {
                confirmDialog.SecondaryButtonText = secondaryButtonText;
                confirmDialog.SecondaryButtonStyle = Resources["ContentDialogButtonStyle"] as Style;
            }

            // Set XamlRoot for proper dialog display
            if (Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                confirmDialog.XamlRoot = Content.XamlRoot;
            }

            ContentDialogResult result = await confirmDialog.ShowAsync();

            if (!shouldRun(result))
            {
                return;
            }

            await RunUnderLibraryOperationGuardAsync(() => action(result));
        }

        private async void FixLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            await ConfirmAndRunAsync(
                "Fix my library",
                "This will automatically download the best artwork from SteamGridDB for all games that have a direct SteamGridDB match.\n\n" +
                "\"Fix new games\" only processes games that have not been modified yet. \"Re-fix all games\" also re-downloads artwork for games customised earlier, replacing their current images.\n\n" +
                "Original Xbox app images are backed up and can always be restored later.",
                "Fix new games",
                "Re-fix all games",
                result => result == ContentDialogResult.Primary || result == ContentDialogResult.Secondary,
                result => FixLibraryAsync(result == ContentDialogResult.Secondary));
        }

        private async void RestoreChangesButton_Click(object sender, RoutedEventArgs e)
        {
            await ConfirmAndRunAsync(
                "Restore my changes",
                "This will restore all previously customised artwork (useful if your changes were reset by the Xbox app).\n\n" +
                "Do you want to continue?",
                "Restore my changes",
                null,
                result => result == ContentDialogResult.Primary,
                _ => RestoreAllChangesAsync());
        }

        private async void RevertDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            await ConfirmAndRunAsync(
                "Revert to Xbox defaults",
                "This will restore the original Xbox app artwork for all customised games and remove the SteamGridDB artwork applied to them.\n\n" +
                "Do you want to continue?",
                "Revert all",
                null,
                result => result == ContentDialogResult.Primary,
                _ => RevertAllToDefaultAsync());
        }

        /// <summary>
        /// Restores the original Xbox app artwork from backups for all customised games.
        /// </summary>
        private async Task RevertAllToDefaultAsync()
        {
            try
            {
                List<GameEntry> customisedGames = GamesToProcess(g => g.HasBackup);

                if (customisedGames.Count == 0)
                {
                    await SetStatusAsync("No customised games to revert");

                    return;
                }

                var report = new OperationReport("Reverting", customisedGames.Count);

                int successCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                foreach (GameEntry game in customisedGames)
                {
                    await SetStatusAsync(report.Step(DisplayName(game)));

                    switch (await RestoreBackupCoreAsync(game, false))
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

                await SetStatusAsync(OperationReport.Summary(
                    $"Revert complete: {successCount} restored to Xbox defaults",
                    OperationReport.When(skippedCount, $"{skippedCount} skipped (no backup)"),
                    OperationReport.When(errorCount, OperationReport.Plural(errorCount, "error"))));
            }
            catch (Exception ex)
            {
                await SetStatusAsync($"Error reverting to defaults: {ex.Message}");

                System.Diagnostics.Debug.WriteLine($"Error in RevertAllToDefaultAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Automatically downloads the best artwork for games with a match in SteamGridDB.
        /// </summary>
        /// <param name="refixCustomised">When true, also re-downloads artwork for games that were customised before (their original backups are preserved).</param>
        private async Task FixLibraryAsync(bool refixCustomised = false)
        {
            try
            {
                if (!HasSteamGridDbApiKey)
                {
                    await SetStatusAsync("SteamGridDB API key is not set - artwork cannot be downloaded");

                    return;
                }

                // Eligible: there is a match in SteamGridDB, it is not one of the Xbox app's own games,
                // and, unless re-fixing, there is no backup yet. See FixEligibility for why the Xbox
                // app's own games are left alone by the bulk runs.
                List<GameEntry> eligibleGames = GamesToProcess(g =>
                    FixEligibility.ShouldFix(g.HasSteamGridDBMatch, g.IsXboxTile, g.HasBackup, refixCustomised));

                // Counted from the same deduplicated set the run itself walks, so a first-party game
                // listed under several stale manifest entries is one game here as well
                int firstPartyCount = GamesToProcess(g =>
                    FixEligibility.SkippedAsFirstParty(g.HasSteamGridDBMatch, g.IsXboxTile, g.HasBackup, refixCustomised)).Count;

                string firstPartyClause = OperationReport.When(
                    firstPartyCount,
                    $"{firstPartyCount} Xbox app {(firstPartyCount == 1 ? "game" : "games")} left alone (they already have the Store's own artwork)");

                if (eligibleGames.Count == 0)
                {
                    await SetStatusAsync(OperationReport.Summary(
                        refixCustomised
                            ? "No eligible artworks to fix (no games have a match in SteamGridDB)"
                            : "No eligible artworks to fix (all games either were already modified or have no match in SteamGridDB)",
                        firstPartyClause));

                    return;
                }

                await SetStatusAsync("Fixing library artwork...");

                var report = new OperationReport("Fixing", eligibleGames.Count);

                int successCount = 0;
                int notFoundCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                FixLog.Start(refixCustomised ? "Re-fix all games" : "Fix my library");

                foreach (string note in SteamGridDbClient.CapsuleParseNotes)
                {
                    FixLog.Write($"capsule parse: {note}");
                }

                // Set from inside the using below, because the summary is built after it closes
                bool stoppedForThrottling = false;

                using (SteamGridDbClient client = new SteamGridDbClient(steamGridDbApiKey))
                {
                    foreach (GameEntry game in eligibleGames)
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
                            // game.Name rather than DisplayName: this line has always shown "Unknown"
                            // for an unnamed game rather than falling back to its image file name
                            await SetStatusAsync(report.Step(game.Name));

                            ArtworkSource source = SourceFor(game);

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

                            List<SteamGridDbGrid> grids = await GetTitleBearingGridsAsync(client, source);

                            if (grids == null)
                            {
                                // The request itself failed - throttled, offline, a bad gateway. Reporting
                                // that as "SteamGridDB has no artwork" would be a lie, and would make a
                                // graded comparison against the previous run meaningless.
                                errorCount++;

                                System.Diagnostics.Debug.WriteLine($"Artwork lookup failed for {game.Name}");

                                continue;
                            }

                            if (grids.Count > 0)
                            {
                                // Rank candidates, then take the best one whose art actually fills the tile
                                List<SteamGridDbGrid> ranked = ArtworkRanker.RankGrids(grids, game.Name);

                                FixLog.Write($"  {grids.Count} square candidates, ranked: {string.Join(", ", ranked.Take(5).Select(g => g.Id))}");

                                (IBuffer Bytes, int ArtworkId) best = await ArtworkDownloader.DownloadBestTileFillingImageAsync(ranked, game.Name, game.OfficialCapsuleUrl);

                                FixLog.Write($"  applied {best.ArtworkId}");
                                bool downloaded = best.Bytes != null && await ReplaceImageCoreAsync(game, best.Bytes, false, best.ArtworkId);

                                if (downloaded)
                                {
                                    successCount++;
                                }
                                else
                                {
                                    errorCount++;
                                }
                            }
                            else if (await TryFixFromPortraitArtAsync(client, game, source))
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

                                    System.Diagnostics.Debug.WriteLine($"Icon lookup failed for {game.Name}");

                                    continue;
                                }

                                if (icons.Count > 0)
                                {
                                    SteamGridDbGrid bestIcon = ArtworkRanker.RankIcons(icons).First();
                                    bool downloaded = await DownloadAndReplaceImageCoreAsync(game, bestIcon.Url, false, bestIcon.Id);

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

                                    System.Diagnostics.Debug.WriteLine($"No artwork found for {game.Name}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            errorCount++;

                            System.Diagnostics.Debug.WriteLine($"Error processing {game.Name}: {ex.Message}");
                        }
                    }
                }

                await FixLog.SaveAsync();

                // The error count is always shown here, unlike the other operations: a fix that reports
                // nothing about failures reads as a clean run when it may have touched almost nothing
                await SetStatusAsync(OperationReport.Summary(
                    stoppedForThrottling
                        ? $"Fixing library stopped early - SteamGridDB is rate limiting; try again later. {successCount} updated so far"
                        : $"Fixing library is complete: {successCount} updated, {notFoundCount} had no artwork in the database",
                    OperationReport.When(skippedCount, $"{skippedCount} skipped (unsupported platform)"),
                    firstPartyClause,
                    OperationReport.Plural(errorCount, "error")));
            }
            catch (Exception ex)
            {
                await SetStatusAsync($"Error fixing library: {ex.Message}");

                System.Diagnostics.Debug.WriteLine($"Error in FixLibraryAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores artwork customisation by using saved .new files to replace current images - for cases when customisation was overwritten externally, for example, by the Xbox app.
        /// </summary>
        private async Task RestoreAllChangesAsync()
        {
            try
            {
                await SetStatusAsync("Restoring customisations...");

                int successCount = 0;
                int noArtworkCount = 0;
                int errorCount = 0;

                List<GameEntry> uniqueGames = GamesToProcess(g => true);

                var report = new OperationReport("Restoring", uniqueGames.Count);

                foreach (GameEntry game in uniqueGames)
                {
                    string imageFileName = Path.GetFileName(game.ImageFilePath);
                    string gameName = DisplayName(game);

                    try
                    {
                        await SetStatusAsync(report.Step(gameName));

                        // A first-party game has one saved customisation per rendition, and any of them
                        // could be the one the Xbox app overwrote, so all are checked
                        ArtworkFiles.ReapplyOutcome outcome = game.IsXboxTile
                            ? await XboxTiles.ReapplyOverwrittenAsync(game.ImageFolder, game.XboxRenditions)
                            : await ArtworkFiles.ReapplyCustomisationAsync(game.ImageFolder, imageFileName);

                        if (outcome == ArtworkFiles.ReapplyOutcome.NothingSaved)
                        {
                            noArtworkCount++;
                            System.Diagnostics.Debug.WriteLine($"Skipping {gameName} for restoration: corresponding .new file not found");

                            continue;
                        }

                        BitmapImage restoredImage = await WrittenThumbnailAsync(game, imageFileName);

                        // hasBackup left untouched: whether a backup exists doesn't change by restoring
                        // a customisation, and this loop's own status line is set above via report.Step
                        await UpdateSharedEntriesAsync(game, imageFileName, restoredImage, null, null);

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
                await SetStatusAsync(successCount == 0 && errorCount == 0
                    ? "No changes found to restore"
                    : OperationReport.Summary(
                        $"Restore complete: {successCount} restored",
                        $"{noArtworkCount} had no artwork saved",
                        OperationReport.Plural(errorCount, "error")));
            }
            catch (Exception ex)
            {
                await SetStatusAsync($"Error restoring changes: {ex.Message}");

                System.Diagnostics.Debug.WriteLine($"Error in RestoreAllChangesAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Downloads and replaces an image for a specific game.
        /// </summary>
        /// <param name="game">The game to update</param>
        /// <param name="imageUrl">The URL of the image to download</param>
        /// <param name="updateStatusText">Whether to update the main status text</param>
        /// <returns>True if successful, false otherwise</returns>
        private async Task<bool> DownloadAndReplaceImageCoreAsync(GameEntry game, string imageUrl, bool updateStatusText = true, int appliedArtworkId = 0)
        {
            IBuffer imageBytes = await ArtworkDownloader.DownloadArtworkAsync(imageUrl);

            return imageBytes != null && await ReplaceImageCoreAsync(game, imageBytes, updateStatusText, appliedArtworkId);
        }

        /// <summary>
        /// Replaces a game's image with the provided image bytes, backing up the original first.
        /// </summary>
        private async Task<bool> ReplaceImageCoreAsync(GameEntry game, IBuffer imageBytes, bool updateStatusText = true, int appliedArtworkId = 0)
        {
            try
            {
                string imageFileName = Path.GetFileName(game.ImageFilePath);
                bool backupExists;

                if (game.IsXboxTile)
                {
                    // One first-party game is several cached images, one per surface the Xbox app shows
                    // it on, and the tile only changes everywhere when all of them do
                    (int written, bool hasBackup) = await XboxTiles.ApplyAsync(game.ImageFolder, game.XboxRenditions, imageBytes);

                    if (written == 0)
                    {
                        return false;
                    }

                    backupExists = hasBackup;
                }
                else
                {
                    backupExists = await ArtworkFiles.ApplyAsync(game.ImageFolder, imageFileName, imageBytes);
                }

                // Nothing on disk says which artwork a tile came from, so remember it
                await AppliedArtworkStore.SetAsync(game.ImageFilePath, appliedArtworkId);

                // Reload the image in the UI
                BitmapImage newImage = await WrittenThumbnailAsync(game, imageFileName);

                await UpdateSharedEntriesAsync(
                    game,
                    imageFileName,
                    newImage,
                    backupExists,
                    updateStatusText
                        ? (game.Name == unknownName ? $"Artwork {imageFileName} updated successfully" : $"Artwork for {game.Name} updated successfully")
                        : null);

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ReplaceImageCoreAsync for {game.Name}: {ex.Message}");

                return false;
            }
        }

        /// <summary>
        /// Last chance before the icon fallback: a game with no square artwork often still has portrait
        /// box art, which cropped to a square makes a far better tile than an icon does. The three games
        /// in the test library that reach this point have 13, 5 and 6 portrait candidates between them,
        /// and two of the three were being given a .ico file.
        /// </summary>
        /// <param name="client">Client to fetch with.</param>
        /// <param name="game">Game being fixed.</param>
        /// <param name="source">How to address the game's artwork.</param>
        /// <returns>True when a cropped tile was written.</returns>
        private async Task<bool> TryFixFromPortraitArtAsync(SteamGridDbClient client, GameEntry game, ArtworkSource source)
        {
            List<SteamGridDbGrid> portraits = await client.GetPortraitGridsAsync(source);

            if (portraits == null || portraits.Count == 0)
            {
                return false;
            }

            foreach (SteamGridDbGrid candidate in ArtworkRanker.RankGrids(portraits, game.Name).Take(ArtworkDownloader.MaxCandidates))
            {
                IBuffer cropped = await TileImage.CropPortraitToTileAsync(await ArtworkDownloader.DownloadArtworkAsync(candidate.Url));

                if (cropped != null && await ReplaceImageCoreAsync(game, cropped, false, candidate.Id))
                {
                    System.Diagnostics.Debug.WriteLine($"Used cropped portrait art {candidate.Id} for {game.Name}");

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Handle edit button click to show grid selection panel
        /// </summary>
        private async void EditGameImage_Click(object sender, RoutedEventArgs e)
        {
            if (IsLibraryOperationBlocking())
            {
                return;
            }

            Button button = sender as Button;

            if (button?.Tag is GameEntry gameEntry)
            {
                gridPanelFocusRestoreTarget = button;
                CurrentSelectedGame = gameEntry;

                // Find the folder for this game
                await LoadGridSelectionPanelAsync(gameEntry);
            }
        }

        /// <summary>
        /// Load and display available grids for the selected game
        /// </summary>
        private async Task LoadGridSelectionPanelAsync(GameEntry game)
        {
            await LoadGridSelectionAsync(
                SourceFor(game),
                $"Select artwork for {game.Name} (platform: {game.Platform}, ID: {game.ExternalPlatformId})",
                game.Name ?? $"{game.Platform} / {game.ExternalPlatformId}");
        }

        /// <summary>
        /// Loads the artwork picker for a game found by manual search, which has no store ID.
        /// </summary>
        /// <param name="game">Game as SteamGridDB returned it.</param>
        private async Task LoadGridSelectionByGameIdAsync(SteamGridDbGame game)
        {
            await LoadGridSelectionAsync(
                ArtworkSource.ForGame(game.Id),
                $"Select artwork for {game.Name} (SteamGridDB ID: {game.Id})",
                game.Name);
        }

        /// <summary>
        /// Shows the artwork picker for whichever way the game is addressed.
        ///
        /// The two entry points - a library row and a manual search result - differed only in that,
        /// which is why they were two near-identical methods that had already drifted apart once.
        /// </summary>
        /// <param name="source">How to address the game's artwork, or null when it cannot be.</param>
        /// <param name="header">Panel header.</param>
        /// <param name="describeGame">Name to show while loading.</param>
        private async Task LoadGridSelectionAsync(ArtworkSource source, string header, string describeGame)
        {
            // Claimed before any await, so even the earliest possible re-entry (another Edit/Search
            // click landing while this population is still in flight) is stamped into a newer session.
            int session = ++gridPanelSessionId;

            try
            {
                GridPanelHeaderText = header;

                await ShowGridPanelAsync();

                GridLoadingRing.IsActive = true;
                GridImagesView.Items.Clear();
                GridPanelStatus.Text = $"Loading artworks for {describeGame}...";

                if (source == null)
                {
                    GridPanelStatus.Text = "Unsupported platform";
                    GridLoadingRing.IsActive = false;

                    return;
                }

                if (!HasSteamGridDbApiKey)
                {
                    GridPanelStatus.Text = "SteamGridDB API key is not set";
                    GridLoadingRing.IsActive = false;

                    return;
                }

                using (SteamGridDbClient client = new SteamGridDbClient(steamGridDbApiKey))
                {
                    // Icons do not depend on the grids result, so the two round trips overlap
                    Task<List<SteamGridDbGrid>> iconsTask = client.GetSquareIconsAsync(source);
                    List<SteamGridDbGrid> grids = await GetTitleBearingGridsAsync(client, source);
                    List<SteamGridDbGrid> icons = await iconsTask;

                    // A newer picker session has started while this fetch was in flight (its own
                    // LoadGridSelectionAsync call already owns the panel). This session's results are
                    // still fetched above - so a superseded fetch never wastes a retry and this closes
                    // no faster than it otherwise would - but they must not touch the panel now: doing
                    // so could clear or append onto whatever the live session already showed, mixing a
                    // stale (and, per GridImage_Click's session check, permanently unclickable) tile set
                    // into the current one, or hiding its loading state.
                    if (session != gridPanelSessionId)
                    {
                        return;
                    }

                    if (grids == null && icons == null)
                    {
                        // Distinguishing this from "no artwork" is the whole point of the null contract
                        GridPanelStatus.Text = "Could not reach SteamGridDB - try again";
                        GridLoadingRing.IsActive = false;

                        return;
                    }

                    await PopulateGridSelectionPanelAsync(grids, icons, session);
                }

                GridLoadingRing.IsActive = false;
            }
            catch (Exception ex)
            {
                GridPanelStatus.Text = $"Error: {ex.Message}";
                GridLoadingRing.IsActive = false;

                System.Diagnostics.Debug.WriteLine($"Error loading artworks: {ex.Message}");
            }
        }

        /// <summary>
        /// How to address a game's artwork: by its store ID, or by SteamGridDB's own ID when the game
        /// was found by name because no store ID matched.
        /// </summary>
        /// <param name="game">Game to fetch artwork for.</param>
        /// <returns>The source, or null when the game cannot be addressed at all.</returns>
        private static ArtworkSource SourceFor(GameEntry game)
        {
            if (game.SteamGridDbGameId > 0)
            {
                return ArtworkSource.ForGame(game.SteamGridDbGameId);
            }

            string platform = GamePlatformHelper.GamePlatformToSGDBApiString(game.Platform);

            return string.IsNullOrEmpty(platform) || string.IsNullOrEmpty(game.ExternalPlatformId)
                ? null
                : ArtworkSource.ForPlatform(platform, game.ExternalPlatformId);
        }

        /// <summary>
        /// The square grids worth showing for a game: one page, and if that page happens to hold nothing
        /// but icon-like styles, a second request restricted to the title-bearing ones.
        ///
        /// Both the auto-fixer and the manual picker need this. When only the fixer made the second
        /// call, opening the picker on such a game offered a strictly worse set than the fixer had
        /// already chosen from - which reads as the picker being broken.
        /// </summary>
        /// <param name="client">Client to fetch with.</param>
        /// <param name="source">How to address the game's artwork.</param>
        /// <returns>Candidates, empty when there are none, null when the request failed.</returns>
        private static async Task<List<SteamGridDbGrid>> GetTitleBearingGridsAsync(SteamGridDbClient client, ArtworkSource source)
        {
            List<SteamGridDbGrid> grids = await client.GetSquareGridsAsync(source);

            if (grids == null || grids.Count == 0 || grids.Any(g => ArtworkRanker.GridStylePriority(g.Style) == 0))
            {
                return grids;
            }

            List<SteamGridDbGrid> textBearing = await client.GetSquareGridsAsync(source, ArtworkRanker.TextBearingGridStyles);

            return textBearing != null && textBearing.Count > 0 ? textBearing : grids;
        }

        /// <summary>
        /// Populates the grid selection panel with the provided grids and icons.
        /// </summary>
        /// <param name="grids">Collection of grid artworks</param>
        /// <param name="icons">Collection of icon artworks</param>
        /// <param name="sessionId">The picker session these artworks were fetched for - see <see cref="gridPanelSessionId"/>.</param>
        private async Task PopulateGridSelectionPanelAsync(IList<SteamGridDbGrid> grids, IList<SteamGridDbGrid> icons, int sessionId)
        {
            // Which of these, if any, is already on the tile
            int? appliedArtworkId = await AppliedArtworkStore.GetAsync(CurrentSelectedGame?.ImageFilePath);

            // A newer picker session has started while this awaited - the same shape LoadGridSelectionAsync
            // already guards against after its own network awaits, and PerformGameSearchAsync guards after
            // its own. The caller checked the session immediately before calling this method, but that
            // check does not cover this method's own await: the fetched grids/icons still belong to a
            // session no longer live, so ranking or adding them now would rank by a game name that has
            // since changed, or mix stale, permanently unclickable tiles into whatever the live session's
            // own population already claimed the panel with.
            if (sessionId != gridPanelSessionId)
            {
                return;
            }

            // Combine grids and icons (ranked grids first, then icons) and compute each tile's
            // display fields - the fallback rules and the "already applied" check - in one pure,
            // directly-tested pass. See GridSelectionItemsTests.cs.
            List<GridSelectionItems.Result> sortedArtworks = GridSelectionItems.BuildOrdered(
                grids, icons, CurrentSelectedGame?.Name, appliedArtworkId, sessionId, unknownName);

            if (sortedArtworks.Count == 0)
            {
                GridPanelStatus.Text = "No artworks found for this game";

                return;
            }

            // Add items to grid view
            foreach (GridSelectionItems.Result artwork in sortedArtworks)
            {
                GridImagesView.Items.Add(new GridImageItem
                {
                    Id = artwork.Id,
                    Url = artwork.Url,
                    ThumbUrl = artwork.ThumbUrl,
                    Author = artwork.Author,
                    Style = artwork.Style,
                    Width = artwork.Width,
                    Height = artwork.Height,
                    IsApplied = artwork.IsApplied,
                    SessionId = artwork.SessionId
                });
            }

            int gridCount = grids?.Count ?? 0;
            int iconCount = icons?.Count ?? 0;

            GridPanelStatus.Text = $"Found {gridCount} grid{(gridCount == 1 ? "" : "s")} and {iconCount} icon{(iconCount == 1 ? "" : "s")} ({sortedArtworks.Count} total)";

            // Focus the first artwork for controller navigation
            var _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (GridImagesView.Items.Count > 0)
                {
                    // Force layout update so containers are realised
                    GridImagesView.UpdateLayout();

                    // Get the first item container and focus it
                    GridViewItem firstContainer = GridImagesView.ContainerFromIndex(0) as GridViewItem;

                    firstContainer?.Focus(FocusState.Programmatic);
                }
            });
        }

        /// <summary>
        /// Handles grid image selection. Downloads and replaces the game's image.
        ///
        /// Ignores a tile whose <see cref="GridImageItem.SessionId"/> no longer matches
        /// <see cref="gridPanelSessionId"/>: the picker was opened again since this tile was rendered,
        /// so CurrentSelectedGame may no longer be the game this tile belongs to. Silently doing nothing
        /// is correct here - the tile is already gone from the panel a moment later once the newer
        /// session's population clears it, so there is no missed action to explain to the user.
        ///
        /// Claims the library-operation guard for the download-and-replace write below: opening the
        /// picker and browsing it does not, so a library-wide reload (Refresh/Fix Library/Restore
        /// Changes/Revert Defaults) can still start while the panel is open, but must not start once
        /// this write is in flight - DownloadAndReplaceImageAsync's own <see cref="CurrentSelectedGame"/>
        /// capture and its post-download <see cref="EntriesSharingImage"/> lookup both predate this
        /// guard and would otherwise land on whatever a concurrent reload just rebuilt
        /// <see cref="GameEntries"/> with, not the game the click was actually about. A bulk operation
        /// already in flight when the tile is clicked declines the click the same way
        /// <see cref="TryBeginLibraryOperation"/>'s other callers do (busy status text, no download).
        /// </summary>
        private async void GridImage_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is GridImageItem gridItem && CurrentSelectedGame != null && gridItem.SessionId == gridPanelSessionId)
            {
                await RunUnderLibraryOperationGuardAsync(() => DownloadAndReplaceImageAsync(gridItem));
            }
        }

        /// <summary>
        /// Downloads selected grid and replaces the game's image file.
        /// </summary>
        private async Task DownloadAndReplaceImageAsync(GridImageItem gridItem)
        {
            try
            {
                GridPanelStatus.Text = "Downloading image...";
                GridLoadingRing.IsActive = true;

                // Use the core download and replace logic
                bool success = await DownloadAndReplaceImageCoreAsync(CurrentSelectedGame, gridItem.Url, true, gridItem.Id);

                // A newer picker session has started while this download was in flight (its own
                // GridImage_Click check only verified the session at click time, before this method's
                // own await). The write above still lands on the right game - CurrentSelectedGame was
                // read before any await, so it could not have changed out from under it - but the panel
                // this method is about to touch below now belongs to a different, live session: writing
                // its status text, then closing it, would interrupt whatever the live session is doing.
                if (gridItem.SessionId != gridPanelSessionId)
                {
                    return;
                }

                if (success)
                {
                    GridPanelStatus.Text = "Image updated successfully";

                    // Close panel after short delay
                    await Task.Delay(250);

                    await HideGridPanelAsync();
                }
                else
                {
                    GridPanelStatus.Text = "Failed to download or save image";
                }

                GridLoadingRing.IsActive = false;
            }
            catch (Exception ex)
            {
                GridPanelStatus.Text = $"Error: {ex.Message}";
                GridLoadingRing.IsActive = false;
                System.Diagnostics.Debug.WriteLine($"Error downloading image: {ex.Message}");
            }
        }

        /// <summary>
        /// Slides a panel's transform between two Y offsets and waits for the animation to finish.
        ///
        /// Shared by all four Show/Hide panel methods below, which previously each hand-built this same
        /// DoubleAnimation/Storyboard: the two Show/Hide pairs had already drifted apart once (200ms
        /// hide vs 250ms show, independently re-derived each time), which is what four copies of the
        /// same six lines cost.
        /// </summary>
        /// <param name="transform">The panel's own TranslateTransform.</param>
        /// <param name="from">Starting Y offset.</param>
        /// <param name="to">Ending Y offset.</param>
        /// <param name="durationMs">Animation duration in milliseconds.</param>
        /// <param name="mode">Easing mode - EaseOut for showing, EaseIn for hiding.</param>
        private async Task SlidePanelAsync(TranslateTransform transform, double from, double to, int durationMs, EasingMode mode)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = mode }
            };

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, transform);
            Storyboard.SetTargetProperty(animation, "Y");  // Animate Y instead of X

            storyboard.Begin();

            await Task.Delay(durationMs);
        }

        /// <summary>
        /// Show the grid selection panel with animation
        /// </summary>
        private async Task ShowGridPanelAsync()
        {
            GridSelectionPanel.Visibility = Visibility.Visible;

            // Slide up from bottom animation (like Xbox notifications)
            await SlidePanelAsync(GridPanelTransform, 800, 0, 250, EasingMode.EaseOut);
        }

        /// <summary>
        /// Hides a panel with animation: guards against a second overlapping close for the same
        /// session (an in-flight close already owns finishing it), captures the session before the
        /// animation's own await, slides the panel down, then - only if no newer session started while
        /// the slide was in flight, which would mean a live session's own tiles are now on screen and
        /// must not be collapsed by this stale close - hides the panel, clears its items, runs any
        /// panel-specific extra teardown, and restores focus to whichever button opened it.
        ///
        /// Shared by <see cref="HideGridPanelAsync"/> and <see cref="HideSearchPanelAsync"/>, which
        /// previously each hand-built this same sequence, byte-near-identical apart from which fields
        /// and controls they closed over - the same shape <see cref="SlidePanelAsync"/> and
        /// <see cref="RunUnderLibraryOperationGuardAsync"/> were themselves extracted to stop duplicating.
        /// </summary>
        private async Task HidePanelAsync(
            LibraryOperationGuard closeGuard,
            Func<int> getSessionId,
            TranslateTransform transform,
            UIElement panel,
            ItemsControl itemsControl,
            Func<Button> getFocusRestoreTarget,
            Action<Button> setFocusRestoreTarget,
            Action extraTeardown = null)
        {
            if (!closeGuard.TryBegin())
            {
                return;
            }

            try
            {
                int session = getSessionId();

                await SlidePanelAsync(transform, 0, 800, 200, EasingMode.EaseIn);

                if (session != getSessionId())
                {
                    return;
                }

                panel.Visibility = Visibility.Collapsed;
                itemsControl.Items.Clear();
                extraTeardown?.Invoke();

                getFocusRestoreTarget()?.Focus(FocusState.Programmatic);
                setFocusRestoreTarget(null);
            }
            finally
            {
                closeGuard.End();
            }
        }

        /// <summary>
        /// Hide the grid selection panel with animation. See <see cref="HidePanelAsync"/> for the
        /// shared guard/session/animate/teardown sequence; the grid panel's own extra teardown clears
        /// <see cref="CurrentSelectedGame"/>, which the search panel must not do.
        /// </summary>
        private async Task HideGridPanelAsync()
        {
            await HidePanelAsync(
                gridPanelCloseGuard,
                () => gridPanelSessionId,
                GridPanelTransform,
                GridSelectionPanel,
                GridImagesView,
                () => gridPanelFocusRestoreTarget,
                target => gridPanelFocusRestoreTarget = target,
                () => CurrentSelectedGame = null);
        }

        /// <summary>
        /// Handles close button click.
        /// </summary>
        private async void CloseGridPanel_Click(object sender, RoutedEventArgs e)
        {
            await HideGridPanelAsync();
        }

        /// <summary>
        /// Handles search button click to show game search panel.
        /// </summary>
        private async void SearchGameImage_Click(object sender, RoutedEventArgs e)
        {
            if (IsLibraryOperationBlocking())
            {
                return;
            }

            Button button = sender as Button;

            if (button?.Tag is GameEntry gameEntry)
            {
                searchPanelFocusRestoreTarget = button;
                CurrentSelectedGame = gameEntry;

                await ShowSearchPanelAsync();
            }
        }

        /// <summary>
        /// Handles search box key down (Enter to search).
        /// </summary>
        private async void GameSearchBox_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await PerformGameSearchAsync();
            }
        }

        /// <summary>
        /// Handles search button click.
        /// </summary>
        private async void SearchGames_Click(object sender, RoutedEventArgs e)
        {
            await PerformGameSearchAsync();
        }

        /// <summary>
        /// Performs game search using SteamGridDB API.
        /// </summary>
        private async Task PerformGameSearchAsync()
        {
            // Claimed before any await, so a second search fired while this one is still in flight -
            // or the panel being reopened, see ShowSearchPanelAsync - is stamped into a newer session.
            int session = ++searchPanelSessionId;

            try
            {
                string searchTerm = GameSearchBox.Text?.Trim();

                if (string.IsNullOrEmpty(searchTerm))
                {
                    SearchPanelStatus.Text = "Please enter a game name";

                    return;
                }

                if (!HasSteamGridDbApiKey)
                {
                    SearchPanelStatus.Text = "SteamGridDB API key is not set";

                    return;
                }

                SearchLoadingRing.IsActive = true;
                SearchResultsListView.Items.Clear();
                SearchPanelStatus.Text = $"Searching for '{searchTerm}'...";

                using (SteamGridDbClient client = new SteamGridDbClient(steamGridDbApiKey))
                {
                    List<SteamGridDbGame> results = await client.SearchGameByNameAsync(searchTerm);

                    // A newer search (or a reopened panel) has superseded this one while the request was
                    // in flight - it must not touch the results list now: doing so would mix a stale,
                    // unrelated search's games into whatever the live search already showed.
                    if (session != searchPanelSessionId)
                    {
                        return;
                    }

                    if (results == null || results.Count == 0)
                    {
                        SearchPanelStatus.Text = "No games found";
                        SearchLoadingRing.IsActive = false;

                        return;
                    }

                    // Add results to list
                    foreach (SteamGridDbGame game in results)
                    {
                        SearchResultsListView.Items.Add(game);
                    }

                    SearchPanelStatus.Text = $"Found {results.Count} game{(results.Count == 1 ? "" : "s")}";
                }

                SearchLoadingRing.IsActive = false;
            }
            catch (Exception ex)
            {
                SearchPanelStatus.Text = $"Error: {ex.Message}";
                SearchLoadingRing.IsActive = false;
                System.Diagnostics.Debug.WriteLine($"Error searching games: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle search result selection
        /// </summary>
        private async void SearchResult_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is SteamGridDbGame selectedGame)
            {
                // DO NOT update current game's name - keep it as "Unknown" so the user can search again

                // Hand off focus-restore ownership from the search panel to the grid panel about to
                // open: the button that originally opened the search panel is the one focus should
                // return to once the grid panel (not the search panel) eventually closes.
                gridPanelFocusRestoreTarget = searchPanelFocusRestoreTarget;
                searchPanelFocusRestoreTarget = null;

                await HideSearchPanelAsync();
                await LoadGridSelectionByGameIdAsync(selectedGame);
            }
        }


        /// <summary>
        /// Shows the search panel with animation.
        /// </summary>
        private async Task ShowSearchPanelAsync()
        {
            // Invalidates any search still in flight from a prior showing of this panel (same or a
            // different game) - see searchPanelSessionId.
            ++searchPanelSessionId;

            // Update header with game information
            if (CurrentSelectedGame != null)
            {
                if (CurrentSelectedGame.Name == unknownName)
                {
                    SearchPanelHeaderText = $"Manual search for a game from {CurrentSelectedGame.Platform}, ID: {CurrentSelectedGame.ExternalPlatformId}";
                }
                else
                {
                    SearchPanelHeaderText = $"Manual search for {CurrentSelectedGame.Name}";
                }
            }
            else
            {
                SearchPanelHeaderText = "Manual search";
            }

            GameSearchPanel.Visibility = Visibility.Visible;

            // Prefill search box with game name if it's not "Unknown"
            if (CurrentSelectedGame != null && CurrentSelectedGame.Name != unknownName)
            {
                GameSearchBox.Text = CurrentSelectedGame.Name;
            }
            else
            {
                GameSearchBox.Text = string.Empty;
            }

            SearchResultsListView.Items.Clear();
            SearchPanelStatus.Text = "Enter game name to search";

            // Slide up from bottom animation
            await SlidePanelAsync(SearchPanelTransform, 800, 0, 250, EasingMode.EaseOut);

            // Focus search box if empty, otherwise focus search button
            if (!string.IsNullOrEmpty(GameSearchBox.Text))
            {
                SearchGamesButton.Focus(FocusState.Programmatic);
            }
            else
            {
                GameSearchBox.Focus(FocusState.Programmatic);

                // Position cursor at the end of the text
                GameSearchBox.Select(GameSearchBox.Text.Length, 0);
            }
        }

        /// <summary>
        /// Hide the search panel with animation. See <see cref="HidePanelAsync"/> for the shared
        /// sequence; the search panel has no extra teardown of its own. The focus-restore target is
        /// already null here when <see cref="SearchResult_Click"/> has just handed it over to
        /// <see cref="gridPanelFocusRestoreTarget"/> instead of clearing it - <see cref="HidePanelAsync"/>'s
        /// null-conditional focus call stays correct either way.
        /// </summary>
        private async Task HideSearchPanelAsync()
        {
            await HidePanelAsync(
                searchPanelCloseGuard,
                () => searchPanelSessionId,
                SearchPanelTransform,
                GameSearchPanel,
                SearchResultsListView,
                () => searchPanelFocusRestoreTarget,
                target => searchPanelFocusRestoreTarget = target);
        }

        /// <summary>
        /// Handle close search panel button click
        /// </summary>
        private async void CloseSearchPanel_Click(object sender, RoutedEventArgs e)
        {
            await HideSearchPanelAsync();
        }

        /// <summary>
        /// Handle restore backup button click.
        ///
        /// Claims the library-operation guard for the restore below via
        /// <see cref="RunUnderLibraryOperationGuardAsync"/>, the same way <see cref="RefreshButton_Click"/>
        /// and <see cref="ConfirmAndRunAsync"/>'s callers already do - RestoreBackupCoreAsync holds the
        /// clicked button's tagged game across its own await and, after it, looks the game's entries back
        /// up by image path (<see cref="EntriesSharingImage"/>); a library-wide reload that rebuilds
        /// <see cref="GameEntries"/> while that await is pending would otherwise have this restore's
        /// result land on the freshly-loaded entries instead of the ones the click was about. A bulk
        /// operation already in flight declines the click (busy status text, no restore) rather than let
        /// the two race.
        /// </summary>
        private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            await RunUnderLibraryOperationGuardAsync(() =>
            {
                Button button = sender as Button;

                return button?.Tag is GameEntry gameEntry ? RestoreBackupAsync(gameEntry) : Task.CompletedTask;
            });
        }

        /// <summary>
        /// Restore image from backup file
        /// </summary>
        private async Task RestoreBackupAsync(GameEntry game)
        {
            string backupGameName = DisplayName(game);

            try
            {
                await SetStatusAsync($"Restoring backup for {backupGameName}...");

                await RestoreBackupCoreAsync(game, true);
            }
            catch (Exception ex)
            {
                await SetStatusAsync($"Error restoring backup: {ex.Message}");

                System.Diagnostics.Debug.WriteLine($"Error in RestoreBackupAsync for {backupGameName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Restores the original Xbox app artwork from the backup file, removing the applied customisation.
        /// </summary>
        /// <param name="game">The game to restore</param>
        /// <param name="updateStatusText">Whether to report the outcome in the status bar</param>
        /// <returns>Whether the backup was restored, was missing, or failed to restore.</returns>
        private async Task<RestoreBackupResult> RestoreBackupCoreAsync(GameEntry game, bool updateStatusText = true)
        {
            string imageFileName = Path.GetFileName(game.ImageFilePath);
            string backupGameName = DisplayName(game);

            try
            {
                ArtworkFiles.RestoreOutcome outcome = game.IsXboxTile
                    ? await XboxTiles.RestoreAsync(game.ImageFolder, game.XboxRenditions)
                    : await ArtworkFiles.RestoreOriginalAsync(game.ImageFolder, imageFileName);

                if (outcome == ArtworkFiles.RestoreOutcome.BackupMissing)
                {
                    if (updateStatusText)
                    {
                        await SetStatusAsync($"Backup file not found for {backupGameName}");
                    }

                    return RestoreBackupResult.BackupMissing;
                }

                // The Xbox app's own artwork is back, so no SteamGridDB artwork applies any more
                await AppliedArtworkStore.ClearAsync(game.ImageFilePath);

                // Reload the image in the UI
                BitmapImage restoredImage = await WrittenThumbnailAsync(game, imageFileName);

                // The backup that was restored from no longer exists - but a first-party game has one
                // per rendition and only those with a backup were restored, so the button has to stay
                // for whatever is left rather than being cleared on the strength of the one that went
                bool backupRemaining = game.IsXboxTile && await XboxTiles.HasBackupAsync(game.XboxRenditions);

                await UpdateSharedEntriesAsync(
                    game,
                    imageFileName,
                    restoredImage,
                    backupRemaining,
                    updateStatusText ? $"Backup restored for {backupGameName}" : null);

                return RestoreBackupResult.Restored;
            }
            catch (Exception ex)
            {
                if (updateStatusText)
                {
                    await SetStatusAsync($"Error restoring backup for {backupGameName}: {ex.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"Error restoring backup for {backupGameName}: {ex.Message}");

                return RestoreBackupResult.Error;
            }
        }

        /// <summary>
        /// Handles the GotFocus event for the game search box, positioning the cursor at the end of the text and
        /// displaying the virtual keyboard when appropriate.
        /// </summary>
        /// <remarks>The virtual keyboard is shown only when focus is received via keyboard or gamepad
        /// navigation, not when using mouse or touch input. This behavior ensures that the keyboard does not appear
        /// unintentionally when the user clicks or taps the search box.</remarks>
        /// <param name="sender">The source of the event, expected to be a TextBox representing the game search box.</param>
        /// <param name="e">The event data associated with the GotFocus event.</param>
        private async void GameSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            // Position cursor at the end of the text
            if (sender is TextBox textBox)
            {
                textBox.SelectionStart = textBox.Text.Length;
                textBox.SelectionLength = 0;

                // Only show virtual keyboard for gamepad/controller input
                // FocusState.Keyboard indicates focus via keyboard/gamepad navigation
                // FocusState.Pointer indicates mouse/touch click - don't show keyboard for this
                if (textBox.FocusState == FocusState.Keyboard)
                {
                    // Delay showing the keyboard to prevent Game Bar from hiding on first focus
                    await Task.Delay(100);

                    try
                    {
                        CoreInputView.GetForCurrentView().TryShow((CoreInputViewKind)7); // 7 = keyboard gamepad
                    }
                    catch
                    {
                        // Keyboard input view not available or failed to show
                    }
                }
            }
        }

        /// <summary>
        /// Handles the LostFocus event for the game search box to hide the virtual keyboard when the control loses
        /// focus.
        /// </summary>
        /// <param name="sender">The source of the event, typically the game search box control.</param>
        /// <param name="e">The event data associated with the LostFocus event.</param>
        private void GameSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            // Hide virtual keyboard when focus is lost
            try
            {
                CoreInputView.GetForCurrentView().TryHide();
            }
            catch
            {
                // Keyboard input view not available or failed to hide
            }
        }
    }
}
