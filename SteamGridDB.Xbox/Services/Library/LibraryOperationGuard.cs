namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Mutual-exclusion guard for one caller-defined "operation": whether one is already running, and
    /// whether a caller may start one. Generic on purpose - PrimaryWidget holds three separate
    /// instances of this same type for three unrelated concerns that all reduce to the identical
    /// shape (library-wide operations racing single-game writes; the grid picker's own close racing
    /// its own download-success auto-close; the search panel's close racing the same way) rather than
    /// three hand-rolled bool fields repeating this class's five lines each time a new instance of the
    /// shape turned up.
    ///
    /// Extracted from PrimaryWidget so the guarantee - at most one may run at a time - is provable by a
    /// test rather than by reading a guard clause's call sites together and trusting they agree, which
    /// is what every prior review of this project could not do for the original library-operation case:
    /// PrimaryWidget.xaml.cs binds to Windows.UI.Xaml and has no desktop test projection (see
    /// TESTING.md), so that guard clause was named as an untested primary-flow mutation site on every
    /// loop of this project's review history. This type owns only whether an operation may start or has
    /// ended; the caller still owns what starting or ending one does (UI state, side effects) - the same
    /// Compute/Do split TESTING.md already documents for the bulk-operation loops (GameImages,
    /// OperationReport, ManifestEntryIdentity).
    /// </summary>
    internal sealed class LibraryOperationGuard
    {
        /// <summary>Whether an operation is currently running.</summary>
        internal bool IsRunning { get; private set; }

        /// <summary>
        /// Claims the guard for a new operation. Returns false, leaving the guard untouched, when one
        /// is already running.
        /// </summary>
        internal bool TryBegin()
        {
            if (IsRunning)
            {
                return false;
            }

            IsRunning = true;

            return true;
        }

        /// <summary>Releases the guard. Safe to call even when nothing is running.</summary>
        internal void End()
        {
            IsRunning = false;
        }
    }
}
