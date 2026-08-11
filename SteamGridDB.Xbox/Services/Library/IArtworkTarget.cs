using System.Threading.Tasks;

using Windows.Storage.Streams;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Whether a write happened, and when it did not, what stopped it.
    ///
    /// The reason travels with the answer rather than being written to the status bar where it is
    /// produced, because the one caller who most needs it cannot see that bar: the picker panel is
    /// an opaque full-screen sibling of the main grid and covers the status text completely.
    /// Explaining a failure there tells it to an empty room, which is how "Failed to download or save
    /// image" came to be the whole of what a user was told when a tile could not be written.
    /// </summary>
    public readonly struct WriteResult
    {
        private WriteResult(bool succeeded, string failure)
        {
            Succeeded = succeeded;
            Failure = failure;
        }

        /// <summary>Whether the artwork is on the tile.</summary>
        public bool Succeeded { get; }

        /// <summary>What stopped the write, or null when nothing did.</summary>
        public string Failure { get; }

        internal static WriteResult Success => new WriteResult(true, null);

        internal static WriteResult Failed(string failure) => new WriteResult(false, failure);
    }

    /// <summary>
    /// How restoring one game's original artwork went. A missing backup is not an error: it is the
    /// ordinary state of a game nobody has customised, and a revert over the whole library reports the
    /// two separately for that reason.
    /// </summary>
    public enum RestoreBackupResult
    {
        Restored,
        BackupMissing,
        Error
    }

    /// <summary>
    /// The library a bulk operation acts on, as the operation sees it.
    ///
    /// <see cref="LibraryFixer"/> and <see cref="LibraryRestorer"/> decide which games to visit, in
    /// what order, what to fetch and what to count. What they cannot do is the writing: applying
    /// artwork ends in a decoded thumbnail stamped onto every row sharing that image, on the UI
    /// thread, and that is the one part of these operations that binds to Windows.UI.Xaml. So it
    /// stays behind this interface, implemented by the widget.
    ///
    /// Generic over the row type rather than taking <see cref="ILibraryGame"/> directly so that the
    /// widget's implementation receives its own row type back and needs no cast - the same reason
    /// <see cref="GameImages"/> is generic over how a row names its image.
    ///
    /// The three methods that write are also the widget's single-game handlers: applying artwork from
    /// the picker and restoring one game's backup from its row button go through the same code the
    /// bulk runs reach here. That is deliberate - the two used to drift. They differ in one respect,
    /// which is why none of these three takes a "say so" flag: a single-game action reports its own
    /// outcome, because it is the only thing the user asked for, while a run over the library reports
    /// its own progress and would have every game's outcome overwrite the line saying where the run
    /// has got to. Everything reached through this interface is the quiet form.
    /// </summary>
    /// <typeparam name="TGame">The caller's own row type.</typeparam>
    public interface IArtworkTarget<TGame> where TGame : ILibraryGame
    {
        /// <summary>
        /// Says what is happening, from whichever thread the operation happens to be running on.
        /// </summary>
        Task ReportAsync(string status);

        /// <summary>
        /// Writes artwork already in hand to a game's tile, backing up the original first.
        /// </summary>
        /// <param name="game">The game to write to.</param>
        /// <param name="artwork">The image bytes.</param>
        /// <param name="artworkId">SteamGridDB's ID for this artwork, remembered so the picker can
        /// mark it as the one in use. Zero when it did not come from SteamGridDB.</param>
        Task<WriteResult> ApplyAsync(TGame game, IBuffer artwork, int artworkId);

        /// <summary>
        /// Downloads artwork and writes it, as <see cref="ApplyAsync"/> does.
        /// </summary>
        Task<WriteResult> ApplyFromUrlAsync(TGame game, string artworkUrl, int artworkId);

        /// <summary>
        /// Puts a game's original Xbox app artwork back and forgets the customisation.
        /// </summary>
        /// <param name="game">The game to restore.</param>
        Task<RestoreBackupResult> RestoreBackupAsync(TGame game);

        /// <summary>
        /// Re-reads a game's image from disk and shows it, for when something other than a write has
        /// changed what is on the tile - a customisation put back after the Xbox app overwrote it.
        /// </summary>
        Task RefreshAsync(TGame game, string imageFileName);
    }
}
