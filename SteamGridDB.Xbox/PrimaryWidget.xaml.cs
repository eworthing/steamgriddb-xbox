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
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Web.Http;

using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.SteamGridDB;
using SteamGridDB.Xbox.Services.SteamGridDB.Models;
using SteamGridDB.Xbox.Services.Stores;

namespace SteamGridDB.Xbox
{
    /// <summary>
    /// Primary widget page that loads and displays Xbox app third-party games.
    /// </summary>
    public sealed partial class PrimaryWidget : Page, INotifyPropertyChanged
    {
        public ObservableCollection<GameEntry> GameEntries
        {
            get; set;
        }

        private readonly string steamGridDbApiKey = Environment.GetEnvironmentVariable("STEAMGRIDDB_API_KEY");
        private readonly string thirdPartyLibrariesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            @"AppData\Local\Packages\Microsoft.GamingApp_8wekyb3d8bbwe\LocalState\ThirdPartyLibraries");
        private const string unknownName = "Unknown";
        private const string imageExtension = ".png";
        private const string backupImageExtension = ".bak";
        private const string newImageExtension = ".new";
        private const string manifestFileExtension = ".manifest";
        private const string busyStatusText = "Another library operation is still running - please wait for it to finish";

        // Artwork is shown in an 80px list thumbnail; decoding the 512-1024px source at full size would
        // hold tens of megabytes of bitmaps for a large library. 160px covers 2x display scaling.
        private const int thumbnailDecodePixelWidth = 160;

        // How far down the ranked candidates the downloader will look. Five covered the tile-filling
        // check; the official-artwork gate occasionally has to reach further to find its replacement.
        private const int maxArtworkCandidates = 8;

        // Colour-match band for the official-artwork gate (see FindOfficialLookalikeAsync). Graded over
        // the whole library: the winner must be below the floor and the replacement above the ceiling.
        // Dropping the ceiling and keeping only the floor was tried and rejected - it let artwork move
        // on differences of a few hundredths, which is inside the measure's own noise. The gap the two
        // leave between them is that guard: nothing moves unless the replacement is a quarter better.
        // The floor was 0.50 for one grading round, which left Mad Max on a 0.51 match while four
        // candidates above 0.85 sat untouched - a hundredth of slack either side of a hard edge.
        private const double officialArtworkFloor = 0.60;
        private const double officialArtworkCeiling = 0.85;

        private enum RestoreBackupResult
        {
            Restored,
            BackupMissing,
            Error
        }

        private static readonly HttpClient sharedHttpClient = new HttpClient();

        private Button lastFocusedButton;

        // Guards the library-wide operations against each other: they all rewrite the same files and
        // rebuild the same collection, so overlapping runs duplicate entries or race on disk.
        private bool isLibraryOperationRunning;

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
            Loaded += PrimaryWidget_Loaded;
        }

        private async void PrimaryWidget_Loaded(object sender, RoutedEventArgs e)
        {
            if (TryBeginLibraryOperation())
            {
                try
                {
                    await LoadGameEntriesAsync();
                }
                finally
                {
                    EndLibraryOperation();
                }
            }

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
            if (!isLibraryOperationRunning)
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
            if (isLibraryOperationRunning)
            {
                StatusText.Text = busyStatusText;

                return false;
            }

            isLibraryOperationRunning = true;
            SetHeaderButtonsEnabled(false);

            return true;
        }

        /// <summary>
        /// Marks the end of a library-wide operation and re-enables the header buttons.
        /// </summary>
        private void EndLibraryOperation()
        {
            isLibraryOperationRunning = false;
            SetHeaderButtonsEnabled(true);
        }

        private void SetHeaderButtonsEnabled(bool enabled)
        {
            FixLibraryButton.IsEnabled = enabled;
            RestoreChangesButton.IsEnabled = enabled;
            RevertDefaultsButton.IsEnabled = enabled;
            RefreshButton.IsEnabled = enabled;
        }

        /// <summary>
        /// Returns the name a sibling artefact (.bak/.new) takes for the given image file.
        /// Path.ChangeExtension rather than a string replace: a replace rewrites every occurrence of
        /// ".png" in the name and silently does nothing for images that are not .png at all, which
        /// would make the backup name equal the image name and overwrite the original unrecoverably.
        /// </summary>
        private static string GetSiblingFileName(string imageFileName, string extension)
        {
            return Path.ChangeExtension(imageFileName, extension);
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
            List<GameEntry> shared = GameEntries
                .Where(g => string.Equals(g.ImageFilePath, game.ImageFilePath, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (shared.Count == 0)
            {
                shared.Add(game);
            }

            return shared;
        }

        private async Task LoadGameEntriesAsync()
        {
            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    // Clear here rather than in the callers so that a repeated load can never append
                    // a second copy of the library to the list
                    GameEntries.Clear();

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
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        StatusText.Text = "ThirdPartyLibraries folder was not found. Make sure games are added to the Xbox app.";
                        GameEntriesListView.Visibility = Visibility.Collapsed;
                    });

                    return;
                }

                if (thirdPartyFolder == null)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        StatusText.Text = "Access denied. Please grant file system permission.";
                        InstructionsPanel.Visibility = Visibility.Visible;
                        GameEntriesListView.Visibility = Visibility.Collapsed;
                    });

                    return;
                }

                // Get all subdirectories
                var folders = await thirdPartyFolder.GetFoldersAsync();

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    string directoryNames = string.Join(", ", folders.Select(f => f.Name));
                    StatusText.Text = $"Found {folders.Count} director{(folders.Count == 1 ? "y" : "ies")} ({directoryNames}). Loading and sorting...";
                });

                // Temporary list to collect games before sorting
                List<GameEntry> tmpGameList = new List<GameEntry>();

                // Manifest entries the Xbox app left behind for removed games: no image and no backup,
                // so there is nothing to show and nothing any of the buttons could act on
                int staleEntryCount = 0;

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

                                    // Only process entries that have an "id" property
                                    if (!entryObject.ContainsKey("id"))
                                    {
                                        continue;
                                    }

                                    // Get the ID from the "id" property (not from the key)
                                    string entryId = entryObject.GetNamedString("id");

                                    // Parse addedDate - it's stored as a string in JSON
                                    string addedDateString = entryObject.GetNamedString("addedDate", "0");
                                    long timestamp = 0;

                                    if (!string.IsNullOrEmpty(addedDateString) && long.TryParse(addedDateString, out long parsedTimestamp))
                                    {
                                        timestamp = parsedTimestamp;
                                    }

                                    string imageFilePath;
                                    StorageFolder imageFolder;

                                    if (platform == GamePlatform.Custom) // Custom contains full path for the image filename
                                    {
                                        imageFilePath = entryObject.GetNamedString("imagePath");

                                        try
                                        {
                                            imageFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(imageFilePath));
                                        }
                                        catch (Exception ex)
                                        {
                                            // Folder of a removed custom game - skip this entry, not the whole manifest
                                            System.Diagnostics.Debug.WriteLine($"Skipping custom entry {entryId}: {ex.Message}");

                                            staleEntryCount++;

                                            continue;
                                        }
                                    }
                                    else // Image filename is based on ID
                                    {
                                        imageFilePath = Path.Combine(thirdPartyLibrariesPath, folder.Name, entryId.Replace(":", "_") + imageExtension);
                                        imageFolder = folder;
                                    }

                                    string imageFileName = Path.GetFileName(imageFilePath);
                                    string backupFileName = GetSiblingFileName(imageFileName, backupImageExtension);

                                    BitmapImage image = null;
                                    bool hasBackup = false;

                                    // Check if backup exists
                                    try
                                    {
                                        await imageFolder.GetFileAsync(backupFileName);

                                        hasBackup = true;
                                    }
                                    catch (FileNotFoundException)
                                    {
                                        // Backup doesn't exist, that's okay
                                    }

                                    try
                                    {
                                        StorageFile imageFile = await imageFolder.GetFileAsync(imageFileName);

                                        image = await CreateThumbnailAsync(imageFile);
                                    }
                                    catch (FileNotFoundException)
                                    {
                                        if (!hasBackup)
                                        {
                                            // Nothing on disk for this entry: either a game the Xbox app removed but
                                            // left in the manifest, or one of the legacy store folders it abandoned
                                            // (their images use a different naming scheme and it no longer reads them)
                                            staleEntryCount++;

                                            continue;
                                        }

                                        // Image is gone but the backup is not - keep the row so it can be restored
                                        imageFileName = "Not found";
                                    }

                                    string gameName = unknownName;

                                    // The store's own game ID, as SteamGridDB knows it. There is deliberately
                                    // no second "Xbox-side" ID on the entry: the two are equal for every store
                                    // except Epic, where reaching for the wrong one silently breaks lookups.
                                    string externalPlatformId;

                                    // Epic's other identifier. The name sources are keyed on this, while
                                    // SteamGridDB is keyed on the appName, so both have to be kept.
                                    string epicCatalogItemId = null;

                                    if (platform == GamePlatform.Custom)
                                    {
                                        gameName = entryObject.GetNamedString("title");
                                        externalPlatformId = Path.Combine(entryObject.GetNamedString("installLocation"), entryObject.GetNamedString("executableName"));
                                    }
                                    else
                                    {
                                        externalPlatformId = entryId.Substring(entryId.IndexOf(':') + 1);

                                        if (platform == GamePlatform.Epic)
                                        {
                                            // Xbox stores Epic entries as "epic:<namespace>:<catalogItemId>:<appName>".
                                            // SteamGridDB's egs identifier is the appName - the last segment
                                            // (for example "Sugar" for Rocket League), not the catalog item ID.
                                            string[] parts = entryId.Split(':');

                                            if (parts.Length >= 3)
                                            {
                                                externalPlatformId = parts[parts.Length - 1];
                                                epicCatalogItemId = parts.Length >= 4 ? parts[2] : null;
                                            }
                                        }
                                    }

                                    bool hasSteamGridDBMatch = false;
                                    string officialCapsuleUrl = null;
                                    int steamGridDbGameId = 0;

                                    // Try to fetch game name from SteamGridDB API
                                    try
                                    {
                                        string platformString = GamePlatformHelper.GamePlatformToSGDBApiString(platform);

                                        if (canQuerySteamGridDb && !string.IsNullOrEmpty(platformString))
                                        {
                                            SteamGridDbGame gameInfo = await sgdbClient.GetGameByPlatformIdAsync(platformString, externalPlatformId);

                                            if (gameInfo != null && !string.IsNullOrEmpty(gameInfo.Name))
                                            {
                                                gameName = gameInfo.Name;
                                                hasSteamGridDBMatch = true;

                                                // Comes back on this same lookup; see the official-artwork gate
                                                officialCapsuleUrl = gameInfo.OfficialCapsuleUrl;
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // Log but don't fail - game name is optional, default is "Unknown"
                                        System.Diagnostics.Debug.WriteLine($"Could not fetch game name for {entryId} from SteamGridDB: {ex.Message}");
                                    }

                                    if (!hasSteamGridDBMatch)
                                    {
                                        if (platform == GamePlatform.GOG)
                                        {
                                            if (!StoreNameLookup.gogNameCache.TryGetValue(externalPlatformId, out string gogName) || string.IsNullOrEmpty(gogName))
                                            {
                                                gogName = await StoreNameLookup.GetGogGameNameAsync(externalPlatformId);

                                                if (!string.IsNullOrEmpty(gogName))
                                                {
                                                    StoreNameLookup.gogNameCache[externalPlatformId] = gogName;
                                                    gameName = gogName;
                                                }
                                            }
                                            else
                                            {
                                                gameName = gogName;
                                            }
                                        }
                                        else if (platform == GamePlatform.Epic)
                                        {
                                            if (!StoreNameLookup.epicNameCache.TryGetValue(externalPlatformId, out string epicName) || string.IsNullOrEmpty(epicName))
                                            {
                                                // Epic's own install manifests first: they are local, they
                                                // carry the real title, and they cover games the online
                                                // database does not. The database is keyed by catalog item
                                                // ID, not by the appName SteamGridDB wants.
                                                epicName = await EpicLibrary.GetDisplayNameAsync(externalPlatformId, epicCatalogItemId)
                                                    ?? await StoreNameLookup.GetEpicGameNameAsync(epicCatalogItemId ?? externalPlatformId);

                                                if (!string.IsNullOrEmpty(epicName))
                                                {
                                                    StoreNameLookup.epicNameCache[externalPlatformId] = epicName;
                                                    gameName = epicName;
                                                }
                                            }
                                            else
                                            {
                                                gameName = epicName;
                                            }
                                        }
                                        else if (platform == GamePlatform.Ubisoft)
                                        {
                                            string ubisoftName = await StoreNameLookup.GetUbisoftGameNameAsync(externalPlatformId);

                                            if (!string.IsNullOrEmpty(ubisoftName))
                                            {
                                                gameName = ubisoftName;
                                            }
                                        }
                                        else if (platform == GamePlatform.EA)
                                        {
                                            // TODO: Implement EA App name fetching if possible
                                        }

                                        // A name is enough to find the game even when no store ID
                                        // matches - SteamGridDB has entries linked to no store at all.
                                        // Custom entries are included deliberately, despite being the
                                        // one kind that made no store request before: someone adding a
                                        // shortcut by hand wants artwork for it more than most, not
                                        // less. The result cache keeps the cost to once per name.
                                        if (canQuerySteamGridDb && gameName != unknownName)
                                        {
                                            steamGridDbGameId = await StoreNameLookup.FindGameByNameAsync(sgdbClient, gameName);
                                            hasSteamGridDBMatch = steamGridDbGameId > 0;
                                        }

                                        FixLog.Write($"unmatched {platform}/{externalPlatformId}"
                                            + (platform == GamePlatform.Epic ? $" catalog={epicCatalogItemId ?? "none"} epic=[{EpicLibrary.LoadSummary}]" : string.Empty)
                                            + $" name={gameName} sgdbId={steamGridDbGameId}");
                                    }

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
                }
                finally
                {
                    sgdbClient?.Dispose();
                }

                // Sort games alphabetically by name, with "Unknown" at the end
                List<GameEntry> sortedGames = tmpGameList
                    .OrderBy(g => g.Name == unknownName ? 1 : 0)
                    .ThenBy(g => g.Name)
                    .ToList();

                await FixLog.SaveAsync();

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    foreach (GameEntry game in sortedGames)
                    {
                        GameEntries.Add(game);
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
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StatusText.Text = $"Error: {ex.Message}";
                });
            }
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryBeginLibraryOperation())
            {
                return;
            }

            try
            {
                await LoadGameEntriesAsync();
            }
            finally
            {
                EndLibraryOperation();
            }
        }

        /// <summary>
        /// Handles fix library button click to automatically download artwork for all eligible games.
        /// </summary>
        private async void FixLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation dialog
            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Fix my library",
                Content = "This will automatically download the best artwork from SteamGridDB for all games that have a direct SteamGridDB match.\n\n" +
                          "\"Fix new games\" only processes games that have not been modified yet. \"Re-fix all games\" also re-downloads artwork for games customised earlier, replacing their current images.\n\n" +
                          "Original Xbox app images are backed up and can always be restored later.",
                PrimaryButtonText = "Fix new games",
                SecondaryButtonText = "Re-fix all games",
                CloseButtonText = "Cancel",
                Style = Resources["DarkContentDialogStyle"] as Style,
                PrimaryButtonStyle = Resources["ContentDialogButtonStyle"] as Style,
                SecondaryButtonStyle = Resources["ContentDialogButtonStyle"] as Style,
                CloseButtonStyle = Resources["ContentDialogButtonStyle"] as Style
            };

            // Set XamlRoot for proper dialog display
            if (Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                confirmDialog.XamlRoot = Content.XamlRoot;
            }

            ContentDialogResult result = await confirmDialog.ShowAsync();

            if (result != ContentDialogResult.Primary && result != ContentDialogResult.Secondary)
            {
                return;
            }

            if (!TryBeginLibraryOperation())
            {
                return;
            }

            try
            {
                await FixLibraryAsync(result == ContentDialogResult.Secondary);
            }
            finally
            {
                EndLibraryOperation();
            }
        }

        private async void RestoreChangesButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Restore my changes",
                Content = "This will restore all previously customised artwork (useful if your changes were reset by the Xbox app).\n\n" +
                          "Do you want to continue?",
                PrimaryButtonText = "Restore my changes",
                CloseButtonText = "Cancel",
                Style = Resources["DarkContentDialogStyle"] as Style,
                PrimaryButtonStyle = Resources["ContentDialogButtonStyle"] as Style,
                CloseButtonStyle = Resources["ContentDialogButtonStyle"] as Style
            };

            if (Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                confirmDialog.XamlRoot = Content.XamlRoot;
            }

            ContentDialogResult result = await confirmDialog.ShowAsync();

            if (result != ContentDialogResult.Primary || !TryBeginLibraryOperation())
            {
                return;
            }

            try
            {
                await RestoreAllChangesAsync();
            }
            finally
            {
                EndLibraryOperation();
            }
        }

        private async void RevertDefaultsButton_Click(object sender, RoutedEventArgs e)
        {
            ContentDialog confirmDialog = new ContentDialog
            {
                Title = "Revert to Xbox defaults",
                Content = "This will restore the original Xbox app artwork for all customised games and remove the SteamGridDB artwork applied to them.\n\n" +
                          "Do you want to continue?",
                PrimaryButtonText = "Revert all",
                CloseButtonText = "Cancel",
                Style = Resources["DarkContentDialogStyle"] as Style,
                PrimaryButtonStyle = Resources["ContentDialogButtonStyle"] as Style,
                CloseButtonStyle = Resources["ContentDialogButtonStyle"] as Style
            };

            if (Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
            {
                confirmDialog.XamlRoot = Content.XamlRoot;
            }

            ContentDialogResult result = await confirmDialog.ShowAsync();

            if (result != ContentDialogResult.Primary || !TryBeginLibraryOperation())
            {
                return;
            }

            try
            {
                await RevertAllToDefaultAsync();
            }
            finally
            {
                EndLibraryOperation();
            }
        }

        /// <summary>
        /// Restores the original Xbox app artwork from backups for all customised games.
        /// </summary>
        private async Task RevertAllToDefaultAsync()
        {
            try
            {
                // Stale Xbox app manifests can list the same image under multiple entries - process each image only once
                List<GameEntry> customisedGames = GameEntries
                    .Where(g => g.HasBackup)
                    .GroupBy(g => g.ImageFilePath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (customisedGames.Count == 0)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        StatusText.Text = "No customised games to revert";
                    });

                    return;
                }

                int successCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                foreach (GameEntry game in customisedGames)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        StatusText.Text = $"Reverting {(game.Name == unknownName ? Path.GetFileName(game.ImageFilePath) : game.Name)} ({successCount + skippedCount + errorCount + 1}/{customisedGames.Count})...";
                    });

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

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    string summary = $"Revert complete: {successCount} restored to Xbox defaults";

                    if (skippedCount > 0)
                    {
                        summary += $", {skippedCount} skipped (no backup)";
                    }

                    if (errorCount > 0)
                    {
                        summary += $", {errorCount} error{(errorCount == 1 ? string.Empty : "s")}";
                    }

                    StatusText.Text = summary;
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StatusText.Text = $"Error reverting to defaults: {ex.Message}";
                });

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
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        StatusText.Text = "SteamGridDB API key is not set - artwork cannot be downloaded";
                    });

                    return;
                }

                // Get eligible games: there is a match in SteamGridDB and, unless re-fixing, no backup yet.
                // Stale Xbox app manifests can list the same image under multiple entries - process each image only once.
                List<GameEntry> eligibleGames = GameEntries
                    .Where(g => g.HasSteamGridDBMatch && (refixCustomised || !g.HasBackup))
                    .GroupBy(g => g.ImageFilePath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                if (eligibleGames.Count == 0)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        StatusText.Text = refixCustomised
                            ? "No eligible artworks to fix (no games have a match in SteamGridDB)"
                            : "No eligible artworks to fix (all games either were already modified or have no match in SteamGridDB)";
                    });

                    return;
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StatusText.Text = $"Fixing library artwork...";
                });

                int successCount = 0;
                int notFoundCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                FixLog.Start(refixCustomised ? "Re-fix all games" : "Fix my library");

                foreach (string note in SteamGridDbClient.CapsuleParseNotes)
                {
                    FixLog.Write($"capsule parse: {note}");
                }

                using (SteamGridDbClient client = new SteamGridDbClient(steamGridDbApiKey))
                {
                    foreach (GameEntry game in eligibleGames)
                    {
                        try
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                StatusText.Text = $"Fixing {game.Name} ({successCount + notFoundCount + skippedCount + errorCount + 1}/{eligibleGames.Count})...";
                            });

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

                                (IBuffer Bytes, int ArtworkId) best = await DownloadBestTileFillingImageAsync(ranked, game.Name, game.OfficialCapsuleUrl);

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

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    string summary = $"Fixing library is complete: {successCount} updated, {notFoundCount} had no artwork in the database";

                    if (skippedCount > 0)
                    {
                        summary += $", {skippedCount} skipped (unsupported platform)";
                    }

                    summary += $", {errorCount} error{(errorCount == 1 ? string.Empty : "s")}";

                    StatusText.Text = summary;
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StatusText.Text = $"Error fixing library: {ex.Message}";
                });

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
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StatusText.Text = "Restoring customisations...";
                });

                int successCount = 0;
                int noArtworkCount = 0;
                int errorCount = 0;

                // Stale Xbox app manifests can list the same image under multiple entries - process each image only once
                List<GameEntry> uniqueGames = GameEntries
                    .GroupBy(g => g.ImageFilePath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .ToList();

                foreach (GameEntry game in uniqueGames)
                {
                    string imageFileName = Path.GetFileName(game.ImageFilePath);
                    string gameName = game.Name == unknownName ? imageFileName : game.Name;

                    try
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            StatusText.Text = $"Restoring {gameName} ({successCount + noArtworkCount + errorCount + 1}/{uniqueGames.Count})...";
                        });

                        string newFileName = GetSiblingFileName(imageFileName, newImageExtension);

                        StorageFile newFile;

                        try
                        {
                            newFile = await game.ImageFolder.GetFileAsync(newFileName);
                        }
                        catch (FileNotFoundException)
                        {
                            noArtworkCount++;
                            System.Diagnostics.Debug.WriteLine($"Skipping {gameName} for restoration: corresponding .new file not found");

                            continue;
                        }

                        var imageBytes = await FileIO.ReadBufferAsync(newFile);

                        StorageFile imageFile = await game.ImageFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);
                        await FileIO.WriteBufferAsync(imageFile, imageBytes);

                        BitmapImage restoredImage = await CreateThumbnailAsync(imageFile);

                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            foreach (GameEntry entry in EntriesSharingImage(game))
                            {
                                entry.Image = restoredImage;
                                entry.ImageFileName = imageFileName;
                            }
                        });

                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;

                        System.Diagnostics.Debug.WriteLine($"Error restoring changes for {gameName}: {ex.Message}");
                    }
                }

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    if (successCount == 0 && errorCount == 0)
                    {
                        StatusText.Text = "No changes found to restore";
                    }
                    else
                    {
                        StatusText.Text = $"Restore complete: {successCount} restored, {noArtworkCount} had no artwork saved, {errorCount} error{(errorCount == 1 ? string.Empty : "s")}";
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StatusText.Text = $"Error restoring changes: {ex.Message}";
                });

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
            IBuffer imageBytes = await DownloadArtworkAsync(imageUrl);

            return imageBytes != null && await ReplaceImageCoreAsync(game, imageBytes, updateStatusText, appliedArtworkId);
        }

        /// <summary>
        /// Replaces a game's image with the provided image bytes, backing up the original first.
        /// </summary>
        private async Task<bool> ReplaceImageCoreAsync(GameEntry game, IBuffer imageBytes, bool updateStatusText = true, int appliedArtworkId = 0)
        {
            try
            {
                // Generate the filenames
                string imageFileName = Path.GetFileName(game.ImageFilePath);
                string backupFileName = GetSiblingFileName(imageFileName, backupImageExtension);
                string newFileName = GetSiblingFileName(imageFileName, newImageExtension);

                // Create backup of ORIGINAL image ONLY if backup doesn't already exist
                bool backupExists = false;

                try
                {
                    await game.ImageFolder.GetFileAsync(backupFileName);

                    backupExists = true;
                }
                catch (FileNotFoundException)
                {
                    // Backup doesn't exist, create it from current image
                    try
                    {
                        StorageFile existingImageFile = await game.ImageFolder.GetFileAsync(imageFileName);

                        // Backup the ORIGINAL image by copying to preserve it
                        StorageFile backupFile = await game.ImageFolder.CreateFileAsync(backupFileName, CreationCollisionOption.ReplaceExisting);
                        var existingBuffer = await FileIO.ReadBufferAsync(existingImageFile);

                        await FileIO.WriteBufferAsync(backupFile, existingBuffer);

                        backupExists = true;
                    }
                    catch (FileNotFoundException)
                    {
                        // No existing image to backup
                    }
                }

                // The Xbox app names every tile .png and we cannot rename its files, so anything that
                // is not already a PNG is re-encoded rather than written under a lying extension
                IBuffer tileBytes = await TileImage.EnsurePngAsync(imageBytes);

                // Save the new image (replaces current)
                StorageFile imageFile = await game.ImageFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteBufferAsync(imageFile, tileBytes);

                // Save a copy of the new image as .new file
                StorageFile newFile = await game.ImageFolder.CreateFileAsync(newFileName, CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteBufferAsync(newFile, tileBytes);

                // Nothing on disk says which artwork a tile came from, so remember it
                await AppliedArtworkStore.SetAsync(game.ImageFilePath, appliedArtworkId);

                // Reload the image in the UI
                BitmapImage newImage = await CreateThumbnailAsync(imageFile);

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    foreach (GameEntry entry in EntriesSharingImage(game))
                    {
                        entry.Image = newImage;
                        entry.ImageFileName = imageFileName;
                        entry.HasBackup = backupExists;
                    }

                    if (updateStatusText)
                    {
                        StatusText.Text = game.Name == unknownName ? $"Artwork {imageFileName} updated successfully" : $"Artwork for {game.Name} updated successfully";
                    }
                });

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ReplaceImageCoreAsync for {game.Name}: {ex.Message}");

                return false;
            }
        }

        /// <summary>
        /// Downloads the best-ranked grid that fills the square tile, skipping uploads with transparent
        /// corners (rounded icon-style art and physical case mockups that metadata cannot identify).
        /// When the winner looks nothing like the game's official store artwork, a later candidate that
        /// clearly does is taken instead - see <see cref="FindOfficialLookalikeAsync"/>.
        /// Returns the chosen grid's image bytes, or the best-ranked grid's bytes when none pass.
        /// </summary>
        /// <param name="rankedGrids">Candidates in ranking order.</param>
        /// <param name="gameName">Game name, for the demotion check on replacement candidates.</param>
        /// <param name="officialCapsuleUrl">Valve's own artwork for this game, or null when it has none.</param>
        private async Task<(IBuffer Bytes, int ArtworkId)> DownloadBestTileFillingImageAsync(IReadOnlyList<SteamGridDbGrid> rankedGrids, string gameName, string officialCapsuleUrl)
        {
            IBuffer fallback = null;
            int fallbackId = 0;

            for (int i = 0; i < rankedGrids.Count && i < maxArtworkCandidates; i++)
            {
                IBuffer imageBytes = await DownloadArtworkAsync(rankedGrids[i].Url);

                if (imageBytes == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = imageBytes;
                    fallbackId = rankedGrids[i].Id;
                }

                if (await TileImage.FillsTileAsync(imageBytes))
                {
                    (IBuffer Bytes, int ArtworkId) replacement = await FindOfficialLookalikeAsync(rankedGrids, i, imageBytes, gameName, officialCapsuleUrl);

                    return replacement.Bytes != null ? replacement : (imageBytes, rankedGrids[i].Id);
                }
            }

            return (fallback, fallbackId);
        }

        /// <summary>
        /// Downloads one artwork, returning null rather than throwing when it cannot be fetched.
        /// </summary>
        private async Task<IBuffer> DownloadArtworkAsync(string url)
        {
            try
            {
                HttpResponseMessage response = await sharedHttpClient.GetAsync(new Uri(url));

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadAsBufferAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading artwork {url}: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        /// Rescues the cases the notes cannot: when two thirds of games have every ranking key tied, the
        /// winner is whatever SteamGridDB happened to return first, and sometimes that is art for the
        /// wrong game entirely. Valve's own store capsule says what the cover really looks like.
        ///
        /// Deliberately a narrow veto, not a ranking key. Ranking by similarity outright was tried and
        /// moved most of the library, including picks that had already been graded as good. The
        /// replacement must clear every one of these, or the original stands:
        ///   - the chosen artwork barely resembles the official capsule at all
        ///   - the replacement resembles it strongly, not merely more
        ///   - the replacement's layout is no worse, so a colour-only coincidence cannot win
        ///   - the replacement is not itself demoted, or a badged console reissue would score highly
        ///     and win precisely because it is the real cover with a storefront banner on it
        /// </summary>
        /// <param name="rankedGrids">Candidates in ranking order.</param>
        /// <param name="chosenIndex">Index of the candidate that won on ranking alone.</param>
        /// <param name="chosenBytes">Image bytes of that candidate.</param>
        /// <param name="gameName">Game name, for the demotion check.</param>
        /// <param name="officialCapsuleUrl">Valve's own artwork, or null when it has none.</param>
        /// <returns>Replacement bytes and artwork ID, or a null buffer to keep the original choice.</returns>
        private async Task<(IBuffer Bytes, int ArtworkId)> FindOfficialLookalikeAsync(IReadOnlyList<SteamGridDbGrid> rankedGrids, int chosenIndex, IBuffer chosenBytes, string gameName, string officialCapsuleUrl)
        {
            if (string.IsNullOrEmpty(officialCapsuleUrl))
            {
                FixLog.Write("  gate: no official capsule for this game");

                return (null, 0);
            }

            ArtworkSignature official = await ArtworkSignature.CreateAsync(await DownloadArtworkAsync(officialCapsuleUrl));
            ArtworkSignature chosen = await ArtworkSignature.CreateAsync(chosenBytes);

            if (official == null || chosen == null)
            {
                // Distinct from "the artwork already matches": this is the gate unable to run at all,
                // which is indistinguishable from it declining unless it says so. It reported nothing
                // for an entire library once, because a bad crop transform made every signature fail.
                FixLog.Write($"  gate: unreadable ({(official == null ? "capsule" : "chosen artwork")})");

                return (null, 0);
            }

            double chosenMatch = official.ColourMatch(chosen);

            if (chosenMatch >= officialArtworkFloor)
            {
                FixLog.Write($"  gate: chosen already matches official art ({chosenMatch:F2})");

                return (null, 0);
            }

            double chosenLayout = official.LayoutMatch(chosen);

            FixLog.Write($"  gate: chosen matches only {chosenMatch:F2}, looking for a replacement above {officialArtworkCeiling:F2}");

            // Everything before chosenIndex already failed the tile-fill check on the way here, so it
            // can only fail it again - starting past the winner saves re-fetching and re-decoding them.
            for (int i = chosenIndex + 1; i < rankedGrids.Count && i < maxArtworkCandidates; i++)
            {
                if (ArtworkRanker.IsDemotedGrid(rankedGrids[i], gameName))
                {
                    continue;
                }

                IBuffer candidateBytes = await DownloadArtworkAsync(rankedGrids[i].Url);
                ArtworkSignature candidate = await ArtworkSignature.CreateAsync(candidateBytes);

                if (candidate == null)
                {
                    FixLog.Write($"    {rankedGrids[i].Id}: unreadable");

                    continue;
                }

                double candidateMatch = official.ColourMatch(candidate);
                double candidateLayout = official.LayoutMatch(candidate);

                if (candidateMatch <= officialArtworkCeiling || candidateLayout < chosenLayout
                    || !await TileImage.FillsTileAsync(candidateBytes))
                {
                    FixLog.Write($"    {rankedGrids[i].Id}: colour {candidateMatch:F2}, layout {candidateLayout:F2} vs {chosenLayout:F2} - rejected");

                    continue;
                }

                FixLog.Write($"    {rankedGrids[i].Id}: colour {candidateMatch:F2}, layout {candidateLayout:F2} - REPLACED {rankedGrids[chosenIndex].Id}");

                return (candidateBytes, rankedGrids[i].Id);
            }

            return (null, 0);
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

            foreach (SteamGridDbGrid candidate in ArtworkRanker.RankGrids(portraits, game.Name).Take(maxArtworkCandidates))
            {
                IBuffer cropped = await TileImage.CropPortraitToTileAsync(await DownloadArtworkAsync(candidate.Url));

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
                lastFocusedButton = button;
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

                    if (grids == null && icons == null)
                    {
                        // Distinguishing this from "no artwork" is the whole point of the null contract
                        GridPanelStatus.Text = "Could not reach SteamGridDB - try again";
                        GridLoadingRing.IsActive = false;

                        return;
                    }

                    await PopulateGridSelectionPanelAsync(grids, icons);
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
        private async Task PopulateGridSelectionPanelAsync(IList<SteamGridDbGrid> grids, IList<SteamGridDbGrid> icons)
        {
            // Which of these, if any, is already on the tile
            int? appliedArtworkId = await AppliedArtworkStore.GetAsync(CurrentSelectedGame?.ImageFilePath);

            // Combine grids and icons - ranked grids first (style, language, metadata), then icons
            List<SteamGridDbGrid> sortedArtworks = new List<SteamGridDbGrid>();

            if (grids != null && grids.Count > 0)
            {
                sortedArtworks.AddRange(ArtworkRanker.RankGrids(grids, CurrentSelectedGame?.Name));
            }

            if (icons != null && icons.Count > 0)
            {
                sortedArtworks.AddRange(ArtworkRanker.RankIcons(icons));
            }

            if (sortedArtworks.Count == 0)
            {
                GridPanelStatus.Text = "No artworks found for this game";

                return;
            }

            // Add items to grid view
            foreach (SteamGridDbGrid artwork in sortedArtworks)
            {
                GridImagesView.Items.Add(new GridImageItem
                {
                    Id = artwork.Id,
                    Url = artwork.Url,
                    ThumbUrl = artwork.Thumb ?? artwork.Url,
                    Author = artwork.Author?.Name ?? unknownName,
                    Style = artwork.Style ?? "default",
                    Width = artwork.Width,
                    Height = artwork.Height,
                    IsApplied = artwork.Id == appliedArtworkId
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
        /// </summary>
        private async void GridImage_Click(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is GridImageItem gridItem && CurrentSelectedGame != null)
            {
                await DownloadAndReplaceImageAsync(gridItem);
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
        /// Show the grid selection panel with animation
        /// </summary>
        private async Task ShowGridPanelAsync()
        {
            GridSelectionPanel.Visibility = Visibility.Visible;

            // Slide up from bottom animation (like Xbox notifications)
            DoubleAnimation animation = new DoubleAnimation
            {
                From = 800,  // Start below screen
                To = 0,      // End at normal position
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, GridPanelTransform);
            Storyboard.SetTargetProperty(animation, "Y");  // Animate Y instead of X

            storyboard.Begin();

            await Task.Delay(250);
        }

        /// <summary>
        /// Hide the grid selection panel with animation.
        /// </summary>
        private async Task HideGridPanelAsync()
        {
            // Slide down animation (reverse)
            DoubleAnimation animation = new DoubleAnimation
            {
                From = 0,
                To = 800,  // Slide below screen
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, GridPanelTransform);
            Storyboard.SetTargetProperty(animation, "Y");  // Animate Y instead of X

            storyboard.Begin();

            await Task.Delay(200);

            GridSelectionPanel.Visibility = Visibility.Collapsed;
            GridImagesView.Items.Clear();
            CurrentSelectedGame = null;

            // Restore focus to the button that opened this panel
            lastFocusedButton?.Focus(FocusState.Programmatic);
            lastFocusedButton = null;
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
                lastFocusedButton = button;
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

                // Hide search panel but don't clear lastFocusedButton yet
                await HideSearchPanelAsync(false);
                await LoadGridSelectionByGameIdAsync(selectedGame);
            }
        }


        /// <summary>
        /// Shows the search panel with animation.
        /// </summary>
        private async Task ShowSearchPanelAsync()
        {
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
            DoubleAnimation animation = new DoubleAnimation
            {
                From = 800,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(250),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, SearchPanelTransform);
            Storyboard.SetTargetProperty(animation, "Y");

            storyboard.Begin();

            await Task.Delay(250);

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
        /// Hide the search panel with animation
        /// </summary>
        private async Task HideSearchPanelAsync(bool restoreFocus = true)
        {
            // Slide down animation
            DoubleAnimation animation = new DoubleAnimation
            {
                From = 0,
                To = 800,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            Storyboard storyboard = new Storyboard();
            storyboard.Children.Add(animation);
            Storyboard.SetTarget(animation, SearchPanelTransform);
            Storyboard.SetTargetProperty(animation, "Y");

            storyboard.Begin();

            await Task.Delay(200);

            GameSearchPanel.Visibility = Visibility.Collapsed;
            SearchResultsListView.Items.Clear();

            // Restore focus to the button that opened this panel
            if (restoreFocus && lastFocusedButton != null)
            {
                lastFocusedButton.Focus(FocusState.Programmatic);
                lastFocusedButton = null;
            }
        }

        /// <summary>
        /// Handle close search panel button click
        /// </summary>
        private async void CloseSearchPanel_Click(object sender, RoutedEventArgs e)
        {
            await HideSearchPanelAsync();
        }

        /// <summary>
        /// Handle restore backup button click
        /// </summary>
        private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
        {
            if (IsLibraryOperationBlocking())
            {
                return;
            }

            Button button = sender as Button;

            if (button?.Tag is GameEntry gameEntry)
            {
                await RestoreBackupAsync(gameEntry);
            }
        }

        /// <summary>
        /// Restore image from backup file
        /// </summary>
        private async Task RestoreBackupAsync(GameEntry game)
        {
            string imageFileName = Path.GetFileName(game.ImageFilePath);
            string backupGameName = game.Name != unknownName ? game.Name : imageFileName;

            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StatusText.Text = $"Restoring backup for {backupGameName}...";
                });

                await RestoreBackupCoreAsync(game, true);
            }
            catch (Exception ex)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StatusText.Text = $"Error restoring backup: {ex.Message}";
                });

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
            string backupGameName = game.Name != unknownName ? game.Name : imageFileName;
            string backupFileName = GetSiblingFileName(imageFileName, backupImageExtension);
            string newFileName = GetSiblingFileName(imageFileName, newImageExtension);

            try
            {
                // Locate the backup first so a missing backup never leaves the game without an image
                StorageFile backupFile;

                try
                {
                    backupFile = await game.ImageFolder.GetFileAsync(backupFileName);
                }
                catch (FileNotFoundException)
                {
                    if (updateStatusText)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            StatusText.Text = $"Backup file not found for {backupGameName}";
                        });
                    }

                    return RestoreBackupResult.BackupMissing;
                }

                // Delete saved customisation if it exists
                try
                {
                    StorageFile newImageFile = await game.ImageFolder.GetFileAsync(newFileName);

                    await newImageFile.DeleteAsync();
                }
                catch (FileNotFoundException)
                {
                    // Saved customisation doesn't exist, that's okay
                }

                // Rename backup to become the main image. ReplaceExisting overwrites the current image,
                // so it is never deleted up front - a failed rename would leave the game with no image.
                await backupFile.RenameAsync(imageFileName, NameCollisionOption.ReplaceExisting);

                // The Xbox app's own artwork is back, so no SteamGridDB artwork applies any more
                await AppliedArtworkStore.ClearAsync(game.ImageFilePath);

                // Reload the image in the UI
                StorageFile imageFile = await game.ImageFolder.GetFileAsync(imageFileName);
                BitmapImage restoredImage = await CreateThumbnailAsync(imageFile);

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    foreach (GameEntry entry in EntriesSharingImage(game))
                    {
                        entry.Image = restoredImage;
                        entry.ImageFileName = imageFileName;
                        entry.HasBackup = false; // Backup no longer exists
                    }

                    if (updateStatusText)
                    {
                        StatusText.Text = $"Backup restored for {backupGameName}";
                    }
                });

                return RestoreBackupResult.Restored;
            }
            catch (Exception ex)
            {
                if (updateStatusText)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        StatusText.Text = $"Error restoring backup for {backupGameName}: {ex.Message}";
                    });
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
