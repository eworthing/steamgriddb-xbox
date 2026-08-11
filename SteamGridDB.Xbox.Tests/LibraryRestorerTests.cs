using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Library;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Reverting the whole library to the Xbox app's own artwork.
    ///
    /// The per-game restore itself is <c>ArtworkFiles</c>/<c>XboxTiles</c>' job and is covered by their
    /// own tests; what is covered here is the run around it - which games it visits, how it counts the
    /// three outcomes, and that a missing backup is reported as an ordinary state of the library rather
    /// than as a failure. That distinction is the whole reason <see cref="RestoreBackupResult"/> has
    /// three values instead of a bool.
    /// </summary>
    public class LibraryRestorerTests
    {
        private static LibraryRow Game(string name, bool hasBackup = true, string imagePath = null)
        {
            return new LibraryRow
            {
                Name = name,
                ImageFilePath = imagePath ?? $"C:\\images\\{name}.png",
                ImageFileName = $"{name}.png",
                HasBackup = hasBackup
            };
        }

        [Fact]
        public async Task A_library_with_nothing_customised_says_so_and_touches_no_game()
        {
            var target = new RecordingArtworkTarget();

            await LibraryRestorer.RevertAllToDefaultAsync(
                new List<LibraryRow> { Game("Halo", hasBackup: false) }, target);

            Assert.Equal("No customised games to revert", Assert.Single(target.Reports));
            Assert.Empty(target.Restored);
        }

        [Fact]
        public async Task Only_games_with_a_backup_are_visited()
        {
            var target = new RecordingArtworkTarget();

            await LibraryRestorer.RevertAllToDefaultAsync(
                new List<LibraryRow>
                {
                    Game("Halo", hasBackup: true),
                    Game("Doom", hasBackup: false),
                    Game("Myst", hasBackup: true)
                },
                target);

            Assert.Equal(new[] { "Halo", "Myst" }, target.Restored.Select(g => g.Name).ToArray());
        }

        [Fact]
        public async Task One_image_listed_under_several_stale_entries_is_reverted_once()
        {
            var target = new RecordingArtworkTarget();

            await LibraryRestorer.RevertAllToDefaultAsync(
                new List<LibraryRow>
                {
                    Game("Halo", imagePath: "C:\\images\\shared.png"),
                    Game("Halo again", imagePath: "C:\\images\\shared.png")
                },
                target);

            Assert.Equal("Halo", Assert.Single(target.Restored).Name);
        }

        [Fact]
        public async Task The_three_outcomes_are_counted_separately()
        {
            // A missing backup is not an error. Folding the two together would report a library where
            // most games were simply never customised as a run that mostly failed.
            var target = new RecordingArtworkTarget();

            target.RestoreOutcome = game =>
                game.Name == "Halo" ? RestoreBackupResult.Restored
                : game.Name == "Doom" ? RestoreBackupResult.BackupMissing
                : RestoreBackupResult.Error;

            await LibraryRestorer.RevertAllToDefaultAsync(
                new List<LibraryRow> { Game("Halo"), Game("Doom"), Game("Myst") }, target);

            Assert.Equal(
                "Revert complete: 1 restored to Xbox defaults, 1 skipped (no backup), 1 error",
                target.LastReport);
        }

        [Fact]
        public async Task A_clean_revert_mentions_neither_skips_nor_errors()
        {
            var target = new RecordingArtworkTarget();

            await LibraryRestorer.RevertAllToDefaultAsync(
                new List<LibraryRow> { Game("Halo"), Game("Doom") }, target);

            Assert.Equal("Revert complete: 2 restored to Xbox defaults", target.LastReport);
        }

        [Fact]
        public async Task Each_game_is_named_before_it_is_reverted()
        {
            // The progress line counts the game as reached before the work starts, whatever the outcome
            // turns out to be - a game that is skipped still took its turn. Reported after the fact, a
            // run appears to stall and then jump.
            var target = new RecordingArtworkTarget();

            await LibraryRestorer.RevertAllToDefaultAsync(
                new List<LibraryRow> { Game("Halo"), Game("Doom") }, target);

            Assert.Equal("Reverting Halo (1/2)...", target.Reports[0]);
            Assert.Equal("Reverting Doom (2/2)...", target.Reports[1]);
        }

        [Fact]
        public async Task An_unnamed_game_is_shown_by_its_image_file()
        {
            // "Unknown (3/12)..." three times in a row tells the user nothing about which game is
            // being worked on.
            var target = new RecordingArtworkTarget();

            await LibraryRestorer.RevertAllToDefaultAsync(
                new List<LibraryRow>
                {
                    new LibraryRow
                    {
                        Name = LibraryLoader.UnknownName,
                        ImageFilePath = "C:\\images\\gog_1234567890.png",
                        HasBackup = true
                    }
                },
                target);

            Assert.Equal("Reverting gog_1234567890.png (1/1)...", target.Reports[0]);
        }
    }
}
