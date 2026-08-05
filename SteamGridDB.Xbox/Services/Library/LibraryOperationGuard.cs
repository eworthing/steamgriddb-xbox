namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Mutual-exclusion guard for library-wide operations and the single-game writes that race them:
    /// whether one is already running, and whether a caller may start one.
    ///
    /// Extracted from PrimaryWidget so the guarantee - at most one may run at a time - is provable by a
    /// test rather than by reading IsLibraryOperationBlocking/TryBeginLibraryOperation/EndLibraryOperation
    /// together and trusting they agree, which is what every prior review of this project could not do:
    /// PrimaryWidget.xaml.cs binds to Windows.UI.Xaml and has no desktop test projection (see
    /// TESTING.md), so this exact guard clause was named as an untested primary-flow mutation site on
    /// every loop of this project's review history. This type owns only whether an operation may start
    /// or has ended; PrimaryWidget still owns what starting or ending one does to the UI (status text,
    /// header buttons) - the same Compute/Do split TESTING.md already documents for the bulk-operation
    /// loops (GameImages, OperationReport, ManifestEntryIdentity).
    /// </summary>
    internal sealed class LibraryOperationGuard
    {
        private bool isRunning;

        /// <summary>Whether an operation is currently running.</summary>
        internal bool IsRunning => isRunning;

        /// <summary>
        /// Claims the guard for a new operation. Returns false, leaving the guard untouched, when one
        /// is already running.
        /// </summary>
        internal bool TryBegin()
        {
            if (isRunning)
            {
                return false;
            }

            isRunning = true;

            return true;
        }

        /// <summary>Releases the guard. Safe to call even when nothing is running.</summary>
        internal void End()
        {
            isRunning = false;
        }
    }
}
