using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Graphics.Imaging;
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
using SteamGridDB.Xbox.Services.SteamGridDB;
using SteamGridDB.Xbox.Services.SteamGridDB.Models;

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

        // Grid styles that normally carry the game's title artwork, matching the look of native Xbox app tiles.
        // Ordered by preference; styles not listed here (no_logo, material) tend to look like plain icons.
        private static readonly string[] textBearingGridStyles = { "alternate", "white_logo", "blurred" };

        // Notes/tags vocabulary of physical-media mockups (word-bounded, so "Xbox" never matches "box").
        // "icon" is deliberately absent - it appears in legitimate source notes like "PS icon" too often.
        private static readonly Regex demotedGridMetadata = new Regex(@"\b(case|box|jewel|spine|cartridge|mock-?ups?|physical|ps1|ps2|psp|retro)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Uploads labelled as sourced from official store artwork
        private static readonly Regex boostedGridMetadata = new Regex(@"\bofficial\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Edition markers in notes/tags; art is demoted when the marker is absent from the game's own name
        private static readonly Regex editionGridMetadata = new Regex(@"\b(deluxe|goty|game of the year|definitive|ultimate|premium|collector'?s?|complete|anniversary|remaster(ed)?|enhanced|legendary|gold)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private enum RestoreBackupResult
        {
            Restored,
            BackupMissing,
            Error
        }

        private static Dictionary<string, string> ubisoftGameLookupCache = null;
        private static readonly Dictionary<string, string> gogNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, string> epicNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HttpClient sharedHttpClient = new HttpClient();

        private Button lastFocusedButton;

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
            await LoadGameEntriesAsync();

            // Set default focus to Fix my library button for controller navigation
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

        private async Task LoadGameEntriesAsync()
        {
            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
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

                // Check if API key is available
                if (string.IsNullOrEmpty(steamGridDbApiKey))
                {
                    StatusText.Text = "Error: SteamGridDB API key is not set.";
                }

                using (SteamGridDbClient sgdbClient = new SteamGridDbClient(steamGridDbApiKey))
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
                                        imageFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(imageFilePath));

                                    }
                                    else // Image filename is based on ID
                                    {
                                        imageFilePath = Path.Combine(thirdPartyLibrariesPath, folder.Name, entryId.Replace(":", "_") + imageExtension);
                                        imageFolder = folder;
                                    }

                                    string imageFileName = Path.GetFileName(imageFilePath);
                                    string backupFileName = imageFileName.Replace(imageExtension, backupImageExtension);

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

                                    // Load image on background thread, create BitmapImage on UI thread
                                    IRandomAccessStream imageStream = null;
                                    try
                                    {
                                        StorageFile imageFile = await imageFolder.GetFileAsync(imageFileName);
                                        imageStream = await imageFile.OpenReadAsync();

                                        // Create and set BitmapImage on UI thread because it has to be owned by it
                                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                        {
                                            try
                                            {
                                                image = new BitmapImage();
                                                // Fire-and-forget: async call will complete in background
                                                var _ = image.SetSourceAsync(imageStream);
                                            }
                                            catch
                                            {
                                                // Image loading failed, will be handled below
                                                image = null;
                                                imageStream?.Dispose();
                                            }
                                        });
                                    }
                                    catch (FileNotFoundException)
                                    {
                                        // Image doesn't exist, that's okay
                                        imageFileName = "Not found";
                                        imageStream?.Dispose();
                                    }

                                    string gameName = unknownName;
                                    string xboxPlatformId;
                                    string externalPlatformId;

                                    if (platform == GamePlatform.Custom)
                                    {
                                        gameName = entryObject.GetNamedString("title");
                                        xboxPlatformId = entryId;
                                        externalPlatformId = Path.Combine(entryObject.GetNamedString("installLocation"), entryObject.GetNamedString("executableName"));
                                    }
                                    else
                                    {
                                        xboxPlatformId = entryId.Substring(entryId.IndexOf(':') + 1);
                                        externalPlatformId = xboxPlatformId;

                                        if (platform == GamePlatform.Epic)
                                        {
                                            // For Epic, entryId format is "epic:namespace:ID"
                                            string[] parts = entryId.Split(':');

                                            if (parts.Length >= 3)
                                            {
                                                externalPlatformId = parts[2];
                                            }
                                        }
                                    }

                                    bool hasSteamGridDBMatch = false;

                                    // Try to fetch game name from SteamGridDB API
                                    try
                                    {
                                        string platformString = GamePlatformHelper.GamePlatformToSGDBApiString(platform);

                                        if (!string.IsNullOrEmpty(platformString))
                                        {
                                            SteamGridDbGame gameInfo = await sgdbClient.GetGameByPlatformIdAsync(platformString, externalPlatformId);

                                            if (gameInfo != null && !string.IsNullOrEmpty(gameInfo.Name))
                                            {
                                                gameName = gameInfo.Name;
                                                hasSteamGridDBMatch = true;
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
                                            if (!gogNameCache.TryGetValue(externalPlatformId, out string gogName) || string.IsNullOrEmpty(gogName))
                                            {
                                                gogName = await GetGogGameNameAsync(externalPlatformId);

                                                if (!string.IsNullOrEmpty(gogName))
                                                {
                                                    gogNameCache[externalPlatformId] = gogName;
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
                                            if (!epicNameCache.TryGetValue(externalPlatformId, out string epicName) || string.IsNullOrEmpty(epicName))
                                            {
                                                epicName = await GetEpicGameNameAsync(externalPlatformId);

                                                if (!string.IsNullOrEmpty(epicName))
                                                {
                                                    epicNameCache[externalPlatformId] = epicName;
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
                                            string ubisoftName = await GetUbisoftGameNameAsync(externalPlatformId);

                                            if (!string.IsNullOrEmpty(ubisoftName))
                                            {
                                                gameName = ubisoftName;
                                            }
                                        }
                                        else if (platform == GamePlatform.EA)
                                        {
                                            // TODO: Implement EA App name fetching if possible
                                        }
                                    }

                                    // Add to temporary list instead of directly to GameEntries
                                    tmpGameList.Add(new GameEntry
                                    {
                                        Name = gameName,
                                        XboxPlatformId = xboxPlatformId,
                                        ExternalPlatformId = externalPlatformId,
                                        ImageFileName = imageFileName,
                                        ImageFilePath = imageFilePath,
                                        ImageFolder = imageFolder,
                                        Platform = platform,
                                        AddedDate = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime,
                                        Image = image,
                                        HasBackup = hasBackup,
                                        HasSteamGridDBMatch = hasSteamGridDBMatch
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

                // Sort games alphabetically by name, with "Unknown" at the end
                List<GameEntry> sortedGames = tmpGameList
                    .OrderBy(g => g.Name == unknownName ? 1 : 0)
                    .ThenBy(g => g.Name)
                    .ToList();

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    foreach (GameEntry game in sortedGames)
                    {
                        GameEntries.Add(game);
                    }

                    StatusText.Text = $"Found {GameEntries.Count} game{(GameEntries.Count == 1 ? string.Empty : "s")}";
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
            GameEntries.Clear();

            await LoadGameEntriesAsync();
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

            if (result == ContentDialogResult.Primary)
            {
                await FixLibraryAsync(false);
            }
            else if (result == ContentDialogResult.Secondary)
            {
                await FixLibraryAsync(true);
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

            if (result == ContentDialogResult.Primary)
            {
                await RestoreAllChangesAsync();
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

            if (result == ContentDialogResult.Primary)
            {
                await RevertAllToDefaultAsync();
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
                int errorCount = 0;

                using (SteamGridDbClient client = new SteamGridDbClient(steamGridDbApiKey))
                {
                    foreach (GameEntry game in eligibleGames)
                    {
                        try
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                StatusText.Text = $"Fixing {game.Name} ({successCount + notFoundCount + errorCount + 1}/{eligibleGames.Count})...";
                            });

                            // Get the platform string for SteamGridDB API
                            string platformString = GamePlatformHelper.GamePlatformToSGDBApiString(game.Platform);

                            if (string.IsNullOrEmpty(platformString))
                            {
                                System.Diagnostics.Debug.WriteLine($"Skipping {game.Name}: unsupported platform");

                                continue;
                            }

                            // Prefer grids with title artwork so tiles match the native Xbox app look.
                            // Rank the unfiltered results client-side: tied scores are common, and the stable
                            // sort keeps SteamGridDB's canonical ordering for ties (the same image the site
                            // shows first, typically the official box art).
                            List<SteamGridDbGrid> grids = await client.GetSquareGridsByPlatformIdAsync(platformString, game.XboxPlatformId);

                            if (grids != null && grids.Count > 0 && !grids.Any(g => Array.IndexOf(textBearingGridStyles, g.Style) >= 0))
                            {
                                // First page is all icon-like styles - ask the server for title-bearing ones beyond it
                                List<SteamGridDbGrid> textBearingGrids = await client.GetSquareGridsByPlatformIdAsync(platformString, game.XboxPlatformId, textBearingGridStyles);

                                if (textBearingGrids != null && textBearingGrids.Count > 0)
                                {
                                    grids = textBearingGrids;
                                }
                            }

                            if (grids != null && grids.Count > 0)
                            {
                                // Rank candidates, then take the best one whose art actually fills the tile
                                IBuffer imageBytes = await DownloadBestTileFillingImageAsync(RankGrids(grids, game.Name));
                                bool downloaded = imageBytes != null && await ReplaceImageCoreAsync(game, imageBytes, false);

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
                                // If no grids, try icons
                                List<SteamGridDbGrid> icons = await client.GetSquareIconsByPlatformIdAsync(platformString, game.XboxPlatformId);

                                if (icons != null && icons.Count > 0)
                                {
                                    // Get the highest-scored icon
                                    SteamGridDbGrid bestIcon = icons.OrderByDescending(i => i.Score).First();
                                    bool downloaded = await DownloadAndReplaceImageCoreAsync(game, bestIcon.Url, false);

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

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StatusText.Text = $"Fixing library is complete: {successCount} updated, {notFoundCount} had no artwork in the database, {errorCount} error{(errorCount == 1 ? string.Empty : "s")}";
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

                        string newFileName = imageFileName.Replace(imageExtension, newImageExtension);

                        StorageFile newFile = null;

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

                        if (newFile != null)
                        {
                            var imageBytes = await FileIO.ReadBufferAsync(newFile);

                            StorageFile imageFile = await game.ImageFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);
                            await FileIO.WriteBufferAsync(imageFile, imageBytes);

                            var stream = await imageFile.OpenReadAsync();

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                try
                                {
                                    BitmapImage restoredImage = new BitmapImage();
                                    var _ = restoredImage.SetSourceAsync(stream);
                                    game.Image = restoredImage;
                                    game.ImageFileName = imageFileName;
                                }
                                catch
                                {
                                    stream?.Dispose();
                                }
                            });

                            successCount++;
                        }
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
        private async Task<bool> DownloadAndReplaceImageCoreAsync(GameEntry game, string imageUrl, bool updateStatusText = true)
        {
            try
            {
                // Download the image
                using (HttpClient httpClient = new HttpClient())
                {
                    HttpResponseMessage response = await httpClient.GetAsync(new Uri(imageUrl));

                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }

                    var imageBytes = await response.Content.ReadAsBufferAsync();

                    return await ReplaceImageCoreAsync(game, imageBytes, updateStatusText);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DownloadAndReplaceImageCoreAsync for {game.Name}: {ex.Message}");

                return false;
            }
        }

        /// <summary>
        /// Replaces a game's image with the provided image bytes, backing up the original first.
        /// </summary>
        private async Task<bool> ReplaceImageCoreAsync(GameEntry game, IBuffer imageBytes, bool updateStatusText = true)
        {
            try
            {
                {
                    // Generate the filenames
                    string imageFileName = Path.GetFileName(game.ImageFilePath);
                    string backupFileName = imageFileName.Replace(imageExtension, backupImageExtension);
                    string newFileName = imageFileName.Replace(imageExtension, newImageExtension);

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

                    // Save the new image (replaces current)
                    StorageFile imageFile = await game.ImageFolder.CreateFileAsync(imageFileName, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteBufferAsync(imageFile, imageBytes);

                    // Save a copy of the new image as .new file
                    StorageFile newFile = await game.ImageFolder.CreateFileAsync(newFileName, CreationCollisionOption.ReplaceExisting);
                    await FileIO.WriteBufferAsync(newFile, imageBytes);

                    // Reload the image in the UI - open stream before dispatching to UI thread
                    var stream = await imageFile.OpenReadAsync();

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        try
                        {
                            BitmapImage newImage = new BitmapImage();

                            // Fire-and-forget: async call will complete in background
                            var _ = newImage.SetSourceAsync(stream);

                            game.Image = newImage;
                            game.ImageFileName = imageFileName;
                            game.HasBackup = backupExists;

                            if (updateStatusText)
                            {
                                StatusText.Text = game.Name == unknownName ? $"Artwork {imageFileName} updated successfully" : $"Artwork for {game.Name} updated successfully";
                            }
                        }
                        catch
                        {
                            stream?.Dispose();
                        }
                    });

                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in DownloadAndReplaceImageAsync for {game.Name}: {ex.Message}");

                return false;
            }
        }

        /// <summary>
        /// Downloads the best-ranked grid that fills the square tile, skipping uploads with transparent
        /// corners (rounded icon-style art and physical case mockups that metadata cannot identify).
        /// Returns the first passing grid's image bytes, or the best-ranked grid's bytes when none pass.
        /// </summary>
        private async Task<IBuffer> DownloadBestTileFillingImageAsync(IReadOnlyList<SteamGridDbGrid> rankedGrids)
        {
            const int maxCandidates = 5;

            IBuffer fallback = null;

            for (int i = 0; i < rankedGrids.Count && i < maxCandidates; i++)
            {
                IBuffer imageBytes;

                try
                {
                    HttpResponseMessage response = await sharedHttpClient.GetAsync(new Uri(rankedGrids[i].Url));

                    if (!response.IsSuccessStatusCode)
                    {
                        continue;
                    }

                    imageBytes = await response.Content.ReadAsBufferAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error downloading grid candidate {rankedGrids[i].Url}: {ex.Message}");

                    continue;
                }

                if (fallback == null)
                {
                    fallback = imageBytes;
                }

                if (await ImageFillsTileAsync(imageBytes))
                {
                    return imageBytes;
                }
            }

            return fallback;
        }

        /// <summary>
        /// True when the image is opaque in its corners. Case mockups and rounded icon-style uploads
        /// have transparent corners; legitimate box art fills the whole square.
        /// </summary>
        private static async Task<bool> ImageFillsTileAsync(IBuffer imageBytes)
        {
            try
            {
                using (var stream = new InMemoryRandomAccessStream())
                {
                    await stream.WriteAsync(imageBytes);
                    stream.Seek(0);

                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                    var transform = new BitmapTransform
                    {
                        ScaledWidth = 32,
                        ScaledHeight = 32,
                        InterpolationMode = BitmapInterpolationMode.Fant
                    };

                    PixelDataProvider pixelData = await decoder.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Straight,
                        transform,
                        ExifOrientationMode.IgnoreExifOrientation,
                        ColorManagementMode.DoNotColorManage);

                    byte[] pixels = pixelData.DetachPixelData();

                    // Sample a 6x6 block in each corner of the 32x32 image; a corner counts as
                    // transparent when over 40% of its pixels have near-zero alpha
                    int transparentCorners = 0;

                    foreach (var corner in new[] { (X: 0, Y: 0), (X: 26, Y: 0), (X: 0, Y: 26), (X: 26, Y: 26) })
                    {
                        int transparentPixels = 0;

                        for (int y = corner.Y; y < corner.Y + 6; y++)
                        {
                            for (int x = corner.X; x < corner.X + 6; x++)
                            {
                                if (pixels[((y * 32) + x) * 4 + 3] < 64)
                                {
                                    transparentPixels++;
                                }
                            }
                        }

                        if (transparentPixels > 14)
                        {
                            transparentCorners++;
                        }
                    }

                    return transparentCorners < 2;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error inspecting image corners: {ex.Message}");

                return true; // undecodable here - accept and let the normal pipeline handle it
            }
        }

        /// <summary>
        /// Handle edit button click to show grid selection panel
        /// </summary>
        private async void EditGameImage_Click(object sender, RoutedEventArgs e)
        {
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
            try
            {
                // Update panel header with game info
                GridPanelHeaderText = $"Select artwork for {game.Name} (platform: {game.Platform}, ID: {game.XboxPlatformId})";

                // Show panel with animation
                await ShowGridPanelAsync();

                // Show loading indicator
                GridLoadingRing.IsActive = true;
                GridImagesView.Items.Clear();
                GridPanelStatus.Text = $"Loading artworks for {game.Name ?? $"{game.Platform} / {game.XboxPlatformId}"}...";

                // Get the platform string for SteamGridDB API
                string platformString = GamePlatformHelper.GamePlatformToSGDBApiString(game.Platform);

                if (string.IsNullOrEmpty(platformString))
                {
                    GridPanelStatus.Text = "Unsupported platform";
                    GridLoadingRing.IsActive = false;

                    return;
                }

                // Fetch grids and icons from SteamGridDB
                using (SteamGridDbClient client = new SteamGridDbClient(steamGridDbApiKey))
                {
                    // Fetch both grids and icons by platform ID
                    List<SteamGridDbGrid> grids = await client.GetSquareGridsByPlatformIdAsync(platformString, game.XboxPlatformId);
                    List<SteamGridDbGrid> icons = await client.GetSquareIconsByPlatformIdAsync(platformString, game.XboxPlatformId);

                    PopulateGridSelectionPanel(grids, icons);
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
        /// Returns the sort rank of a grid style - title-bearing box art styles first, icon-like styles last.
        /// Title-bearing styles rank equally: preferring one over another proved to mostly surface
        /// mis-tagged fan art while any of them already matches the native Xbox look.
        /// </summary>
        /// <param name="style">Grid style reported by SteamGridDB.</param>
        private static int GridStylePriority(string style)
        {
            return Array.IndexOf(textBearingGridStyles, style) >= 0 ? 0 : 1;
        }

        /// <summary>
        /// Combined notes and tags text used for metadata-based ranking. Cross-reference links to
        /// other uploads (SteamGridDB convention "[&gt;deluxe](url)") and URLs are stripped so they
        /// cannot trigger keyword matches; other links keep their text (e.g. "Official - Microsoft").
        /// </summary>
        private static string GridMetadata(SteamGridDbGrid grid)
        {
            string text = (grid.Notes ?? string.Empty) + " " + string.Join(" ", grid.Tags ?? Array.Empty<string>());

            text = Regex.Replace(text, @"\[>[^\]]*\]\s*\([^)]*\)", " ");
            text = Regex.Replace(text, @"\[([^\]]*)\]\s*\([^)]*\)", "$1");
            text = Regex.Replace(text, @"https?://\S+", " ");

            return text;
        }

        /// <summary>
        /// True when the artwork's notes/tags name an edition (deluxe, GOTY, etc.) that is not part of
        /// the game's own name - e.g. "Deluxe Edition" art for a standard-edition game.
        /// </summary>
        private static bool IsEditionMismatch(SteamGridDbGrid grid, string gameName)
        {
            foreach (Match match in editionGridMetadata.Matches(GridMetadata(grid)))
            {
                if (string.IsNullOrEmpty(gameName) || gameName.IndexOf(match.Value, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Ranks grids for auto-selection: mockup/icon-labelled and wrong-edition uploads last,
        /// English (or untagged) language first, official store artwork boosted, then style
        /// preference and community score. Ties keep SteamGridDB's canonical ordering (stable sort).
        /// </summary>
        private static List<SteamGridDbGrid> RankGrids(IEnumerable<SteamGridDbGrid> grids, string gameName)
        {
            return grids
                .OrderBy(g => demotedGridMetadata.IsMatch(GridMetadata(g)) || IsEditionMismatch(g, gameName) ? 1 : 0)
                .ThenBy(g => string.IsNullOrEmpty(g.Language) || g.Language == "en" ? 0 : 1)
                .ThenByDescending(g => boostedGridMetadata.IsMatch(GridMetadata(g)) ? 1 : 0)
                .ThenBy(g => GridStylePriority(g.Style))
                .ThenByDescending(g => g.Score)
                .ToList();
        }

        /// <summary>
        /// Populates the grid selection panel with the provided grids and icons.
        /// </summary>
        /// <param name="grids">Collection of grid artworks</param>
        /// <param name="icons">Collection of icon artworks</param>
        private void PopulateGridSelectionPanel(IList<SteamGridDbGrid> grids, IList<SteamGridDbGrid> icons)
        {
            // Combine grids and icons - ranked grids first (style, language, metadata), then icons
            List<SteamGridDbGrid> sortedArtworks = new List<SteamGridDbGrid>();

            if (grids != null && grids.Count > 0)
            {
                sortedArtworks.AddRange(RankGrids(grids, CurrentSelectedGame?.Name));
            }

            if (icons != null && icons.Count > 0)
            {
                sortedArtworks.AddRange(icons.OrderByDescending(i => i.Score));
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
                    Score = artwork.Score
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
                bool success = await DownloadAndReplaceImageCoreAsync(CurrentSelectedGame, gridItem.Url);

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
        /// Loads grid selection panel for a game by its SteamGridDB ID.
        /// Reuses the existing LoadGridSelectionPanelAsync logic.
        /// </summary>
        private async Task LoadGridSelectionByGameIdAsync(SteamGridDbGame game)
        {
            try
            {
                // Update panel header
                GridPanelHeaderText = $"Select artwork for {game.Name} (SteamGridDB ID: {game.Id})";

                // Show panel with animation
                await ShowGridPanelAsync();

                // Show loading indicator
                GridLoadingRing.IsActive = true;
                GridImagesView.Items.Clear();
                GridPanelStatus.Text = $"Loading artworks for {game.Name}...";

                // Fetch grids and icons from SteamGridDB by game ID
                using (SteamGridDbClient client = new SteamGridDbClient(steamGridDbApiKey))
                {
                    // Fetch both grids and icons by game ID
                    List<SteamGridDbGrid> grids = await client.GetSquareGridsByGameIdAsync(game.Id);
                    List<SteamGridDbGrid> icons = await client.GetSquareIconsByGameIdAsync(game.Id);

                    PopulateGridSelectionPanel(grids, icons);
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
            string backupFileName = imageFileName.Replace(imageExtension, backupImageExtension);
            string newFileName = imageFileName.Replace(imageExtension, newImageExtension);

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

                // Delete current image if it exists
                try
                {
                    StorageFile currentImageFile = await game.ImageFolder.GetFileAsync(imageFileName);

                    await currentImageFile.DeleteAsync();
                }
                catch (FileNotFoundException)
                {
                    // Current image doesn't exist, that's okay
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

                // Rename backup to become the main image
                await backupFile.RenameAsync(imageFileName, NameCollisionOption.ReplaceExisting);

                // Reload the image in the UI - open stream before dispatching to UI thread
                StorageFile imageFile = await game.ImageFolder.GetFileAsync(imageFileName);
                var stream = await imageFile.OpenReadAsync();

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    try
                    {
                        BitmapImage restoredImage = new BitmapImage();
                        // Fire-and-forget: async call will complete in background
                        var _ = restoredImage.SetSourceAsync(stream);

                        game.Image = restoredImage;
                        game.ImageFileName = imageFileName;
                        game.HasBackup = false; // Backup no longer exists

                        if (updateStatusText)
                        {
                            StatusText.Text = $"Backup restored for {backupGameName}";
                        }
                    }
                    catch
                    {
                        stream?.Dispose();
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
        /// Fetches game name from GOG API by GOG ID.
        /// </summary>
        /// <param name="gogId">The GOG game ID</param>
        /// <returns>Game name or null if not found</returns>
        private async Task<string> GetGogGameNameAsync(string gogId)
        {
            try
            {
                string url = $"https://api.gog.com/v2/games/{gogId}";
                HttpResponseMessage response = await sharedHttpClient.GetAsync(new Uri(url));

                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();

                    if (JsonObject.TryParse(jsonContent, out JsonObject gameData))
                    {
                        if (gameData.ContainsKey("_embedded") &&
                            gameData.GetNamedObject("_embedded").ContainsKey("product"))
                        {
                            JsonObject product = gameData.GetNamedObject("_embedded").GetNamedObject("product");

                            if (product.ContainsKey("title"))
                            {
                                return product.GetNamedString("title");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching GOG game name for {gogId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Fetches Epic Games Store game name from GitHub by external platform ID.
        /// </summary>
        /// <param name="epicId">The Epic Games Store ID.</param>
        /// <returns>Game name or null if not found.</returns>
        private async Task<string> GetEpicGameNameAsync(string epicId)
        {
            try
            {
                string url = $"https://raw.githubusercontent.com/nachoaldamav/items-tracker/refs/heads/main/database/items/{epicId}.json";
                HttpResponseMessage response = await sharedHttpClient.GetAsync(new Uri(url));

                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();

                    if (JsonObject.TryParse(jsonContent, out JsonObject gameData))
                    {
                        if (gameData.ContainsKey("title"))
                        {
                            return gameData.GetNamedString("title");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching Epic game name for {epicId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Downloads and parses the Ubisoft game list from GitHub.
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        private async Task<bool> LoadUbisoftGameListAsync()
        {
            if (ubisoftGameLookupCache != null)
            {
                return true;
            }

            try
            {
                string url = "https://raw.githubusercontent.com/Haoose/UPLAY_GAME_ID/refs/heads/master/README.md";
                HttpResponseMessage response = await sharedHttpClient.GetAsync(new Uri(url));

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                string content = await response.Content.ReadAsStringAsync();
                string[] lines = content.Split('\n');

                ubisoftGameLookupCache = new Dictionary<string, string>();

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    if (string.IsNullOrEmpty(trimmedLine))
                    {
                        continue;
                    }

                    // Format: "232 - Beyond Good and Evil™"
                    int dashIndex = trimmedLine.IndexOf(" - ");

                    if (dashIndex > 0)
                    {
                        string idPart = trimmedLine.Substring(0, dashIndex).Trim();
                        string namePart = trimmedLine.Substring(dashIndex + 3).Trim();

                        if (!string.IsNullOrEmpty(idPart) && !string.IsNullOrEmpty(namePart))
                        {
                            ubisoftGameLookupCache[idPart] = namePart;
                        }
                    }
                }

                return ubisoftGameLookupCache.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading Ubisoft game list: {ex.Message}");

                return false;
            }
        }

        /// <summary>
        /// Fetches game name from cached Ubisoft game list by Ubisoft ID.
        /// </summary>
        /// <param name="ubisoftId">The Ubisoft game ID</param>
        /// <returns>Game name or null if not found</returns>
        private async Task<string> GetUbisoftGameNameAsync(string ubisoftId)
        {
            try
            {
                await LoadUbisoftGameListAsync();

                if (ubisoftGameLookupCache != null && ubisoftGameLookupCache.TryGetValue(ubisoftId, out string gameName))
                {
                    return gameName;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching Ubisoft game name for {ubisoftId}: {ex.Message}");
            }

            return null;
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
