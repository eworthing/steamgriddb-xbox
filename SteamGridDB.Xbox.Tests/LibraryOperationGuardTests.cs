using SteamGridDB.Xbox.Services.Library;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The mutual-exclusion guarantee every library-wide operation and every single-game write in
    /// PrimaryWidget relies on: at most one may run at a time. See PrimaryWidget's own
    /// TryBeginLibraryOperation/EndLibraryOperation/IsLibraryOperationBlocking for how the widget uses
    /// this - those three methods could not be tested directly (PrimaryWidget.xaml.cs has no desktop
    /// projection), so the guarantee they all depend on is tested here instead.
    /// </summary>
    public class LibraryOperationGuardTests
    {
        [Fact]
        public void Starts_not_running()
        {
            var guard = new LibraryOperationGuard();

            Assert.False(guard.IsRunning);
        }

        [Fact]
        public void TryBegin_succeeds_and_marks_running_when_nothing_else_is_running()
        {
            var guard = new LibraryOperationGuard();

            Assert.True(guard.TryBegin());
            Assert.True(guard.IsRunning);
        }

        [Fact]
        public void TryBegin_fails_and_leaves_the_guard_running_when_already_running()
        {
            // The mutation this catches: flipping TryBegin's "if (isRunning)" to "if (!isRunning)" would
            // let a second caller in while the first is still running - the exact race this guard exists
            // to prevent between a bulk reload and a single-game write (GridImage_Click, RestoreBackup_Click).
            var guard = new LibraryOperationGuard();
            guard.TryBegin();

            Assert.False(guard.TryBegin());
            Assert.True(guard.IsRunning);
        }

        [Fact]
        public void End_releases_the_guard_so_a_new_operation_can_begin()
        {
            var guard = new LibraryOperationGuard();
            guard.TryBegin();

            guard.End();

            Assert.False(guard.IsRunning);
            Assert.True(guard.TryBegin());
        }

        [Fact]
        public void End_is_safe_to_call_when_nothing_is_running()
        {
            var guard = new LibraryOperationGuard();

            guard.End();

            Assert.False(guard.IsRunning);
        }
    }
}
