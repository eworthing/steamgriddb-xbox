using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

using SteamGridDB.Xbox.Controls;
using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services;
using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Library;
using SteamGridDB.Xbox.Services.SteamGridDB;
using SteamGridDB.Xbox.Services.SteamGridDB.Models;
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
        private const string unknownName = LibraryLoader.UnknownName;
        private const string busyStatusText = "Another library operation is still running - please wait for it to finish";

        private enum RestoreBackupResult
        {
            Restored,
            BackupMissing,
            Error
        }

        /// <summary>
        /// Per-panel state for the grid picker and search panel. Introduced to replace six loose fields
        /// (gridPanelSessionId/searchPanelSessionId, gridPanelFocusRestoreTarget/searchPanelFocusRestoreTarget,
        /// gridPanelCloseGuard/searchPanelCloseGuard) that all existed for the same two panels and had to
        /// be threaded through <see cref="HidePanelAsync"/> as three delegates apiece - see each member
        /// below for what it replaces.
        /// </summary>
        private sealed class PanelState
        {
            // Incremented every time the artwork picker is (re)populated - by EditGameImage_Click,
            // SearchGameImage_Click or a search result choosing a game - and stamped onto every
            // GridImageItem that population creates (see PopulateGridSelectionPanelAsync). Opening the
            // picker for a different game does not wait for the previous one to finish: ShowGridPanelAsync
            // alone takes ~250ms, during which the previous game's tiles are still on screen and clickable
            // while CurrentSelectedGame may already point at the new game. Without this, clicking one of
            // those stale tiles would write its artwork to the wrong game with no error. GridImage_Click
            // compares a clicked tile's stamp against gridPanel's SessionId and ignores the click when
            // they differ.
            //
            // searchPanel's own SessionId is the same shape, one screen upstream: PerformGameSearchAsync
            // can be triggered again (Enter key or the Search button) before a prior search's network
            // round trip has returned, and ShowSearchPanelAsync can be reopened for the same or a
            // different game while one is still in flight - neither is blocked by anything but the
            // bulk-operation gate. Bumped by both, so a stale search's completion is discarded whether it
            // is superseded by a new search or by the panel being shown again. Checked in
            // PerformGameSearchAsync before any result-list write.
            internal int SessionId;

            // Guards HidePanelAsync against running twice at once for the same session: each panel's own
            // Close button and its own successful-download auto-close (DownloadAndReplaceImageAsync's
            // call at the end of a successful download) both call the same Hide method, and nothing stops
            // a user closing the panel by hand while their own click's download is still in flight from
            // the auto-close arriving a moment later. SessionId above already guards against a
            // *different* session's stale close finishing late; it does nothing for two closes of the
            // *same* session overlapping, which would run the slide-down animation and the
            // visibility/selection teardown twice. Reuses LibraryOperationGuard's own TryBegin/End shape
            // rather than a fourth hand-rolled bool flag - see its own doc comment.
            internal readonly LibraryOperationGuard CloseGuard = new LibraryOperationGuard();

            // Button to restore focus to once this panel closes. Each panel owns exactly one of these:
            // EditGameImage_Click sets gridPanel's for the grid picker; SearchGameImage_Click sets
            // searchPanel's for the search panel. The one exception is SearchResult_Click, which hands
            // searchPanel's over to gridPanel's before opening the grid picker for the chosen result -
            // focus should return to the button that originally opened the search panel once the grid
            // picker (opened next, not the search panel) eventually closes. That handoff is the only
            // place either is written by anything other than its own panel's own open handler or its own
            // panel's own close handler - see the handoff's own comment.
            internal Button FocusRestoreTarget;
        }

        // Guards the library-wide operations against each other - they all rewrite the same files and
        // rebuild the same collection, so overlapping runs duplicate entries or race on disk - AND
        // against a single-game write (GridImage_Click, RestoreBackup_Click): those also write files a
        // library-wide reload can be mid-rebuild of, and TryBeginLibraryOperation/EndLibraryOperation
        // wrap both kinds of caller identically so the two block each other. The guard's own
        // begin/end/is-running rule lives in LibraryOperationGuard, not here - see its doc comment for
        // why: this class binds to Windows.UI.Xaml and has no desktop test projection, so the rule
        // could not be tested in place.
        private readonly LibraryOperationGuard libraryOperationGuard = new LibraryOperationGuard();

        private readonly PanelState gridPanel = new PanelState();
        private readonly PanelState searchPanel = new PanelState();

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

        /// <summary>
        /// True while a library-wide operation is in flight. The per-game buttons check this because
        /// disabling the header is not enough: restoring or replacing one game's artwork from a row
        /// while a bulk pass is rewriting the same files is the same concurrent-writer race.
        /// </summary>
        /// <param name="reportTo">Where to say so, or null for the main status bar. A caller reached
        /// from behind one of the slide-up panels has to name its own panel's status line: the panels
        /// are opaque and full-screen, so a refusal written to <c>StatusText</c> is one the user cannot
        /// see, and the click reads as simply not responding.</param>
        private bool IsLibraryOperationBlocking(TextBlock reportTo = null)
        {
            if (!libraryOperationGuard.IsRunning)
            {
                return false;
            }

            (reportTo ?? StatusText).Text = busyStatusText;

            return true;
        }

        /// <summary>
        /// Marks the start of a library-wide operation and disables the header buttons for its duration.
        /// Returns false when another operation is already running.
        /// </summary>
        /// <param name="reportTo">Where to report a refusal - see <see cref="IsLibraryOperationBlocking"/>.</param>
        private bool TryBeginLibraryOperation(TextBlock reportTo = null)
        {
            if (!libraryOperationGuard.TryBegin())
            {
                (reportTo ?? StatusText).Text = busyStatusText;

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
        /// <param name="reportTo">Where to report a refusal - see <see cref="IsLibraryOperationBlocking"/>.</param>
        private async Task RunUnderLibraryOperationGuardAsync(Func<Task> action, TextBlock reportTo = null)
        {
            if (!TryBeginLibraryOperation(reportTo))
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
            await OnUiThreadAsync(() => StatusText.Text = text);
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
        /// Decodes a game image at list-thumbnail size. See <see cref="ThumbnailDecoder"/>, which owns
        /// the dispatcher handling; this only supplies the page's own dispatcher.
        /// </summary>
        /// <param name="file">Image file to decode.</param>
        /// <returns>The decoded image, or null when it could not be decoded.</returns>
        private async Task<BitmapImage> CreateThumbnailAsync(StorageFile file)
        {
            return await ThumbnailDecoder.CreateAsync(Dispatcher, file);
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

        /// <summary>
        /// Reads the library and shows it, in the two passes <see cref="LibraryLoader"/> is split into.
        ///
        /// What is left here is what only the widget can do: clearing the list, saying what is
        /// happening, decoding each row's thumbnail, and turning rows into the bound entries the list
        /// holds. The <c>FixLog</c> run brackets the whole of it rather than either pass, so that a
        /// load which failed before reaching a pass still leaves a log describing itself.
        /// </summary>
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
                    thirdPartyFolder = await LibraryLoader.ThirdPartyLibrariesFolderAsync();
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

                // Without an API key the library still loads - names stay "Unknown" and artwork cannot be
                // fetched, but the list, the backups and the restore/revert buttons all keep working
                bool canQuerySteamGridDb = HasSteamGridDbApiKey;
                SteamGridDbClient sgdbClient = canQuerySteamGridDb ? new SteamGridDbClient(steamGridDbApiKey) : null;

                int staleEntryCount = 0;
                List<LibraryRow> xboxRows = new List<LibraryRow>();

                try
                {
                    LibraryLoader.ThirdPartyLoad thirdParty = await LibraryLoader.ThirdPartyRowsAsync(
                        thirdPartyFolder, sgdbClient, canQuerySteamGridDb, SetStatusAsync);

                    staleEntryCount = thirdParty.StaleEntryCount;

                    // Shown before the first-party pass rather than after it. That pass has to ask the
                    // Store's CDN about each game it has not seen before, which is the one genuinely
                    // slow part of a load and happens only once per game - but on a first run it is
                    // long enough that holding back a library that is already sitting in memory makes
                    // the whole widget look stuck.
                    await ShowRowsAsync(
                        thirdParty.Rows,
                        thirdPartySection,
                        () => $"Found {OperationReport.Plural(thirdParty.Rows.Count, "game")} - looking for Xbox app games...");

                    // Runs inside the same try so it shares the one SteamGridDB client the whole load
                    // uses, and after the third-party walk so a first-party failure cannot cost the
                    // library its main half
                    xboxRows = await LibraryLoader.XboxRowsAsync(sgdbClient, canQuerySteamGridDb, SetStatusAsync);
                }
                finally
                {
                    sgdbClient?.Dispose();
                }

                await ShowRowsAsync(xboxRows, xboxSection, () => LoadSummary(staleEntryCount, canQuerySteamGridDb));
            }
            catch (Exception ex)
            {
                await SetStatusAsync($"Error: {ex.Message}");
            }
            finally
            {
                // In a finally rather than at the end of the try: the run worth having a log for is the
                // one that failed, and every early return and every throw above used to leave
                // last-load.log holding a previous, unrelated load.
                await FixLog.SaveAsync();
            }
        }

        /// <summary>
        /// Decodes each row's thumbnail, turns the rows into bound entries and puts them on screen.
        ///
        /// The decode happens before the dispatcher hop rather than inside it because it is awaited and
        /// the hop's callback cannot be; the entries then reach both collections and the status line in
        /// one hop, so the list never renders a half-filled section.
        /// </summary>
        /// <param name="rows">Rows to show.</param>
        /// <param name="section">The section they belong under.</param>
        /// <param name="statusText">What to say once they are on screen - evaluated on the UI thread,
        /// after the additions, so it can count what is now in <see cref="GameEntries"/>.</param>
        private async Task ShowRowsAsync(IReadOnlyList<LibraryRow> rows, GameEntrySection section, Func<string> statusText)
        {
            List<GameEntry> entries = new List<GameEntry>(rows.Count);

            foreach (LibraryRow row in rows)
            {
                entries.Add(new GameEntry
                {
                    Name = row.Name,
                    ExternalPlatformId = row.ExternalPlatformId,
                    Platform = row.Platform,
                    AddedDate = row.AddedDate,
                    ImageFileName = row.ImageFileName,
                    ImageFilePath = row.ImageFilePath,
                    ImageFolder = row.ImageFolder,
                    XboxRenditions = row.XboxRenditions,
                    HasBackup = row.HasBackup,
                    HasSteamGridDBMatch = row.HasSteamGridDBMatch,
                    OfficialCapsuleUrl = row.OfficialCapsuleUrl,
                    SteamGridDbGameId = row.SteamGridDbGameId,
                    Image = row.ThumbnailSource == null ? null : await CreateThumbnailAsync(row.ThumbnailSource)
                });
            }

            await OnUiThreadAsync(() =>
            {
                foreach (GameEntry entry in entries)
                {
                    GameEntries.Add(entry);
                    section.Add(entry);
                }

                StatusText.Text = statusText();
            });
        }

        /// <summary>
        /// The line the status bar settles on once a load finishes.
        /// </summary>
        /// <param name="staleEntryCount">Manifest entries the Xbox app is not showing - see <see cref="LibraryLoader.ThirdPartyLoad.StaleEntryCount"/>.</param>
        /// <param name="canQuerySteamGridDb">Whether SteamGridDB could be queried at all.</param>
        private string LoadSummary(int staleEntryCount, bool canQuerySteamGridDb)
        {
            string summary = $"Found {OperationReport.Plural(GameEntries.Count, "game")}";

            if (staleEntryCount > 0)
            {
                // Not "stale manifest entries", which reads as damage the user should go and
                // repair. There is nothing to repair and nothing missing: the Xbox app's
                // manifest is a record of the third-party games it has detected, and it decides
                // separately which of them to show. An entry it never fetched artwork for is
                // one it is not showing, so the widget's library matches the Xbox app's by
                // skipping it. Graded against a real library where all 17 were checked by hand:
                // every one was either absent from the Xbox app or a leftover of a store folder
                // it had abandoned. The count stays because a library that silently shrinks is
                // worse than one that says why - see last-load.log for which entries they were.
                summary += $", {OperationReport.Plural(staleEntryCount, "other entry", "other entries")} the Xbox app is not showing";
            }

            if (!canQuerySteamGridDb)
            {
                summary += " - SteamGridDB API key is not set, artwork cannot be fetched";
            }

            return summary;
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
            // Opened before the first early return, not after them, so that the finally below always
            // has a log belonging to this run to write. Starting it later meant a run that declined
            // to do anything - no API key, nothing eligible - left last-fix.log describing some
            // earlier run, which reads as if this one had done that work.
            FixLog.Start(refixCustomised ? "Re-fix all games" : "Fix my library");

            try
            {
                if (!HasSteamGridDbApiKey)
                {
                    await SetStatusAsync("SteamGridDB API key is not set - artwork cannot be downloaded");

                    FixLog.Write("nothing attempted: SteamGridDB API key is not set");

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
                    $"{OperationReport.Plural(firstPartyCount, "Xbox app game")} left alone (they already have the Store's own artwork)");

                if (eligibleGames.Count == 0)
                {
                    await SetStatusAsync(OperationReport.Summary(
                        refixCustomised
                            ? "No eligible artworks to fix (no games have a match in SteamGridDB)"
                            : "No eligible artworks to fix (all games either were already modified or have no match in SteamGridDB)",
                        firstPartyClause));

                    FixLog.Write($"nothing eligible: {firstPartyCount} first-party game(s) left alone");

                    return;
                }

                await SetStatusAsync("Fixing library artwork...");

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

                                bool downloaded = best.Bytes != null && (await ReplaceImageCoreAsync(game, best.Bytes, false, best.ArtworkId)).Succeeded;

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

                                    FixLog.Write("  icon lookup failed - counted as an error");

                                    System.Diagnostics.Debug.WriteLine($"Icon lookup failed for {game.Name}");

                                    continue;
                                }

                                if (icons.Count > 0)
                                {
                                    SteamGridDbGrid bestIcon = ArtworkRanker.RankIcons(icons).First();
                                    bool downloaded = (await DownloadAndReplaceImageCoreAsync(game, bestIcon.Url, false, bestIcon.Id)).Succeeded;

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
            finally
            {
                // In a finally rather than at the end of the try, for the same reason
                // LoadGameEntriesAsync's is: the run worth having a log for is the one that failed,
                // and every early return and every throw above used to leave last-fix.log holding a
                // previous, unrelated run.
                await FixLog.SaveAsync();
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

                // One listing for the whole run, as the library load does - this walks every game too
                HashSet<string> vaultFileNames = await XboxTileStore.VaultFileNamesAsync();

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
                            ? await XboxTiles.ReapplyOverwrittenAsync(game.ImageFolder, game.XboxRenditions, vaultFileNames)
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
                        OperationReport.When(noArtworkCount, $"{noArtworkCount} had no artwork saved"),
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
        private async Task<WriteResult> DownloadAndReplaceImageCoreAsync(GameEntry game, string imageUrl, bool updateStatusText = true, int appliedArtworkId = 0)
        {
            IBuffer imageBytes = await ArtworkDownloader.DownloadArtworkAsync(imageUrl);

            return imageBytes == null
                ? WriteResult.Failed("the artwork could not be downloaded")
                : await ReplaceImageCoreAsync(game, imageBytes, updateStatusText, appliedArtworkId);
        }

        /// <summary>
        /// Whether a write happened, and when it did not, what stopped it.
        ///
        /// The reason travels with the answer rather than being written to the status bar where it is
        /// produced, because the one caller who most needs it cannot see that bar: the picker panel is
        /// an opaque full-screen sibling of the main grid and covers StatusText completely. Explaining
        /// a failure there tells it to an empty room, which is how "Failed to download or save image"
        /// came to be the whole of what a user was told when a tile could not be written.
        /// </summary>
        private readonly struct WriteResult
        {
            private WriteResult(bool succeeded, string failure)
            {
                Succeeded = succeeded;
                Failure = failure;
            }

            /// <summary>Whether the artwork is on the tile.</summary>
            internal bool Succeeded { get; }

            /// <summary>What stopped the write, or null when nothing did.</summary>
            internal string Failure { get; }

            internal static WriteResult Success => new WriteResult(true, null);

            internal static WriteResult Failed(string failure) => new WriteResult(false, failure);
        }

        /// <summary>
        /// What to say after a write that succeeded, at least in part.
        ///
        /// A first-party game is several cached images and any of them can be refused on its own, so
        /// "updated successfully" is only the whole truth when none were. Where some were, the surfaces
        /// that did change and the ones that did not are both real, and a library still showing the old
        /// tile is exactly what an unqualified success would fail to explain.
        /// </summary>
        /// <param name="game">The game that was written.</param>
        /// <param name="imageFileName">Its image's name, for a game the manifests never named.</param>
        /// <param name="writeFailures">Renditions the cache refused, from <see cref="XboxTiles.ApplyAsync"/>.</param>
        private string AppliedMessage(GameEntry game, string imageFileName, IReadOnlyList<string> writeFailures)
        {
            string applied = game.Name == unknownName
                ? $"Artwork {imageFileName} updated successfully"
                : $"Artwork for {game.Name} updated successfully";

            return writeFailures.Count == 0
                ? applied
                : $"Artwork for {DisplayName(game)} partly updated - " + OperationReport.WriteFailureClause(writeFailures);
        }

        /// <summary>
        /// Replaces a game's image with the provided image bytes, backing up the original first.
        /// </summary>
        private async Task<WriteResult> ReplaceImageCoreAsync(GameEntry game, IBuffer imageBytes, bool updateStatusText = true, int appliedArtworkId = 0)
        {
            try
            {
                string imageFileName = Path.GetFileName(game.ImageFilePath);
                bool backupExists;

                // Renditions the Xbox app's cache refused this write, for the status line. Empty for a
                // third-party game, which is one file that either wrote or threw.
                IReadOnlyList<string> writeFailures = Array.Empty<string>();

                if (game.IsXboxTile)
                {
                    // The record naming this game's renditions was written when it was first found, and
                    // the Xbox app caches a tile per surface lazily - so a game seen before its library
                    // tile had been drawn is recorded as a thumbnail and nothing else. Asked again here,
                    // where there is artwork in hand to fill whatever has appeared since.
                    if (updateStatusText)
                    {
                        await SetStatusAsync($"Checking the tiles for {DisplayName(game)}...");
                    }

                    IReadOnlyList<string> renditions = await XboxLibrary.RefreshRenditionsAsync(
                        game.ImageFolder, game.ExternalPlatformId, game.XboxRenditions);

                    // One first-party game is several cached images, one per surface the Xbox app shows
                    // it on, and the tile only changes everywhere when all of them do
                    (int written, IReadOnlyList<string> failures, bool hasBackup) =
                        await XboxTiles.ApplyAsync(game.ImageFolder, renditions, imageBytes);

                    writeFailures = failures;

                    if (written == 0)
                    {
                        return WriteResult.Failed(failures.Count > 0
                            ? $"no tile could be written - {string.Join("; ", failures)}"
                            : "this game has no cached tile to write to");
                    }

                    string primaryPath = Path.Combine(game.ImageFolder.Path, renditions[0]);

                    if (!string.Equals(primaryPath, game.ImageFilePath, StringComparison.OrdinalIgnoreCase))
                    {
                        // A larger rendition has joined the set, and the largest stands in for the game
                        // wherever one path is needed. The applied-artwork record is keyed by that path,
                        // so the old key now describes nothing and would leave a stale In use mark on
                        // the picker - the same case XboxTiles.ForgetArtworkRecordsAsync exists for.
                        await AppliedArtworkStore.ClearAsync(game.ImageFilePath);

                        game.ImageFilePath = primaryPath;
                        imageFileName = Path.GetFileName(primaryPath);
                    }

                    game.XboxRenditions = renditions;
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
                    updateStatusText ? AppliedMessage(game, imageFileName, writeFailures) : null);

                return WriteResult.Success;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ReplaceImageCoreAsync for {game.Name}: {ex.Message}");

                return WriteResult.Failed($"{ex.GetType().Name}: {ex.Message}");
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

                if (cropped != null && (await ReplaceImageCoreAsync(game, cropped, false, candidate.Id)).Succeeded)
                {
                    FixLog.Write($"  applied {candidate.Id} (portrait, cropped)");

                    System.Diagnostics.Debug.WriteLine($"Used cropped portrait art {candidate.Id} for {game.Name}");

                    return true;
                }
            }

            FixLog.Write("  no portrait candidate survived download and crop - trying icons");

            return false;
        }

        /// <summary>
        /// Handle edit button click to show grid selection panel
        /// </summary>
        private async void EditGameImage_Click(object sender, RoutedEventArgs e)
        {
            // Find the folder for this game
            await HandleGameImagePanelButtonClickAsync(
                sender, button => gridPanel.FocusRestoreTarget = button, LoadGridSelectionPanelAsync);
        }

        /// <summary>
        /// Load and display available grids for the selected game
        /// </summary>
        private async Task LoadGridSelectionPanelAsync(GameEntry game)
        {
            await LoadGridSelectionAsync(
                ArtworkSource.SourceFor(game.SteamGridDbGameId, game.Platform, game.ExternalPlatformId),
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
            int session = ++gridPanel.SessionId;

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
                    List<SteamGridDbGrid> grids = await client.GetTitleBearingGridsAsync(source);
                    List<SteamGridDbGrid> icons = await iconsTask;

                    // A newer picker session has started while this fetch was in flight (its own
                    // LoadGridSelectionAsync call already owns the panel). This session's results are
                    // still fetched above - so a superseded fetch never wastes a retry and this closes
                    // no faster than it otherwise would - but they must not touch the panel now: doing
                    // so could clear or append onto whatever the live session already showed, mixing a
                    // stale (and, per GridImage_Click's session check, permanently unclickable) tile set
                    // into the current one, or hiding its loading state.
                    if (session != gridPanel.SessionId)
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
        /// Populates the grid selection panel with the provided grids and icons.
        /// </summary>
        /// <param name="grids">Collection of grid artworks</param>
        /// <param name="icons">Collection of icon artworks</param>
        /// <param name="sessionId">The picker session these artworks were fetched for - see <see cref="PanelState.SessionId"/>.</param>
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
            if (sessionId != gridPanel.SessionId)
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
                GridImagesView.Items.Add(new GridImageItem(artwork));
            }

            int gridCount = grids?.Count ?? 0;
            int iconCount = icons?.Count ?? 0;

            GridPanelStatus.Text = $"Found {OperationReport.Plural(gridCount, "grid")} and {OperationReport.Plural(iconCount, "icon")} ({sortedArtworks.Count} total)";

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
        /// <see cref="PanelState.SessionId"/>: the picker was opened again since this tile was rendered,
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
            if (e.ClickedItem is GridImageItem gridItem && CurrentSelectedGame != null && gridItem.SessionId == gridPanel.SessionId)
            {
                // The refusal goes to the panel's own status line, not the main one: this panel is an
                // opaque full-screen sibling of the main grid and covers StatusText completely, so a
                // busy line written there is one the user cannot see and the click reads as simply not
                // responding.
                await RunUnderLibraryOperationGuardAsync(() => DownloadAndReplaceImageAsync(gridItem), GridPanelStatus);
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
                WriteResult result = await DownloadAndReplaceImageCoreAsync(CurrentSelectedGame, gridItem.Url, true, gridItem.Id);

                // A newer picker session has started while this download was in flight (its own
                // GridImage_Click check only verified the session at click time, before this method's
                // own await). The write above still lands on the right game - CurrentSelectedGame was
                // read before any await, so it could not have changed out from under it - but the panel
                // this method is about to touch below now belongs to a different, live session: writing
                // its status text, then closing it, would interrupt whatever the live session is doing.
                if (gridItem.SessionId != gridPanel.SessionId)
                {
                    return;
                }

                if (result.Succeeded)
                {
                    GridPanelStatus.Text = "Image updated successfully";

                    // Close panel after short delay
                    await Task.Delay(250);

                    await HideGridPanelAsync();
                }
                else
                {
                    // The reason, not just the fact. This panel covers the status bar, so whatever it
                    // does not say here is said nowhere the user is looking.
                    GridPanelStatus.Text = $"Could not update the artwork: {result.Failure}";
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
            PanelState state,
            TranslateTransform transform,
            UIElement panel,
            ItemsControl itemsControl,
            Action extraTeardown = null)
        {
            if (!state.CloseGuard.TryBegin())
            {
                return;
            }

            try
            {
                int session = state.SessionId;

                await SlidePanelAsync(transform, 0, 800, 200, EasingMode.EaseIn);

                if (session != state.SessionId)
                {
                    return;
                }

                panel.Visibility = Visibility.Collapsed;
                itemsControl.Items.Clear();
                extraTeardown?.Invoke();

                state.FocusRestoreTarget?.Focus(FocusState.Programmatic);
                state.FocusRestoreTarget = null;
            }
            finally
            {
                state.CloseGuard.End();
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
                gridPanel,
                GridPanelTransform,
                GridSelectionPanel,
                GridImagesView,
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
            await HandleGameImagePanelButtonClickAsync(
                sender, button => searchPanel.FocusRestoreTarget = button, gameEntry => ShowSearchPanelAsync());
        }

        /// <summary>
        /// The shape shared by every button that opens an artwork panel for the row it sits on: bail out
        /// while a library operation is running, resolve the row's <see cref="GameEntry"/> from the
        /// button that was clicked, remember that button so focus can return to it when the panel
        /// closes, select the row, then open the panel. <see cref="EditGameImage_Click"/> and
        /// <see cref="SearchGameImage_Click"/> differ only in which focus-restore field they set and
        /// which panel they open.
        /// </summary>
        /// <param name="sender">The event's sender, expected to be the row's <see cref="Button"/>.</param>
        /// <param name="setFocusRestoreTarget">Remembers the button, for the panel that is about to open.</param>
        /// <param name="openPanelAsync">Opens the panel for the resolved game.</param>
        private async Task HandleGameImagePanelButtonClickAsync(
            object sender, Action<Button> setFocusRestoreTarget, Func<GameEntry, Task> openPanelAsync)
        {
            if (IsLibraryOperationBlocking())
            {
                return;
            }

            Button button = sender as Button;

            if (button?.Tag is GameEntry gameEntry)
            {
                setFocusRestoreTarget(button);
                CurrentSelectedGame = gameEntry;

                await openPanelAsync(gameEntry);
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
            int session = ++searchPanel.SessionId;

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
                    if (session != searchPanel.SessionId)
                    {
                        return;
                    }

                    // Null is the client's "the request itself failed" - telling the user SteamGridDB
                    // has no such game when it was never reached sends them off renaming their search
                    if (results == null)
                    {
                        SearchPanelStatus.Text = "Could not reach SteamGridDB - try again";
                        SearchLoadingRing.IsActive = false;

                        return;
                    }

                    if (results.Count == 0)
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

                    SearchPanelStatus.Text = $"Found {OperationReport.Plural(results.Count, "game")}";
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
                gridPanel.FocusRestoreTarget = searchPanel.FocusRestoreTarget;
                searchPanel.FocusRestoreTarget = null;

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
            // different game) - see PanelState.SessionId.
            ++searchPanel.SessionId;

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
                // No Select call here: this branch is reached exactly when the box is empty, so
                // placing the caret at the end of the text is placing it at 0, which is where it
                // already is. GameSearchBox_GotFocus does the positioning that matters.
                GameSearchBox.Focus(FocusState.Programmatic);
            }
        }

        /// <summary>
        /// Hide the search panel with animation. See <see cref="HidePanelAsync"/> for the shared
        /// sequence; the search panel has no extra teardown of its own. The focus-restore target is
        /// already null here when <see cref="SearchResult_Click"/> has just handed it over to
        /// <see cref="PanelState.FocusRestoreTarget"/> on <see cref="gridPanel"/> instead of clearing it -
        /// <see cref="HidePanelAsync"/>'s null-conditional focus call stays correct either way.
        /// </summary>
        private async Task HideSearchPanelAsync()
        {
            await HidePanelAsync(
                searchPanel,
                SearchPanelTransform,
                GameSearchPanel,
                SearchResultsListView);
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
                ArtworkFiles.RestoreOutcome outcome;
                IReadOnlyList<string> restoreFailures = Array.Empty<string>();

                if (game.IsXboxTile)
                {
                    (outcome, restoreFailures) = await XboxTiles.RestoreAsync(game.ImageFolder, game.XboxRenditions);
                }
                else
                {
                    outcome = await ArtworkFiles.RestoreOriginalAsync(game.ImageFolder, imageFileName);
                }

                if (outcome == ArtworkFiles.RestoreOutcome.BackupMissing)
                {
                    if (restoreFailures.Count > 0)
                    {
                        // The backups exist - every write of them was refused. Saying "not found"
                        // here would send the user looking for a file that is sitting right there.
                        if (updateStatusText)
                        {
                            await SetStatusAsync(
                                $"Could not restore backup for {backupGameName}: {string.Join("; ", restoreFailures)}");
                        }

                        return RestoreBackupResult.Error;
                    }

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
                bool backupRemaining = game.IsXboxTile
                    && XboxTiles.HasBackup(game.XboxRenditions, await XboxTileStore.VaultFileNamesAsync());

                // A first-party restore can succeed on some surfaces and be refused on others, and a
                // library still showing the old tile somewhere is exactly what an unqualified
                // success would fail to explain
                string restoredStatus = restoreFailures.Count == 0
                    ? $"Backup restored for {backupGameName}"
                    : $"Backup partly restored for {backupGameName} - " + OperationReport.WriteFailureClause(restoreFailures);

                await UpdateSharedEntriesAsync(
                    game,
                    imageFileName,
                    restoredImage,
                    backupRemaining,
                    updateStatusText ? restoredStatus : null);

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
        /// Puts the caret at the end of whatever the search box already holds, and asks for the
        /// on-screen keyboard - which <see cref="VirtualKeyboard"/> supplies only for focus that
        /// arrived without a pointer.
        /// </summary>
        private async void GameSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.SelectionStart = textBox.Text.Length;
                textBox.SelectionLength = 0;

                await VirtualKeyboard.ShowForAsync(textBox.FocusState);
            }
        }

        /// <summary>
        /// Takes the on-screen keyboard back down with the focus that called for it.
        /// </summary>
        private void GameSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            VirtualKeyboard.Hide();
        }
    }
}
