using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Library;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The bulk artwork fix, which until it moved out of PrimaryWidget.xaml.cs could not be run by
    /// anything but a person with a Game Bar open.
    ///
    /// What is covered here is the half that does not touch SteamGridDB: which games a run decides to
    /// visit, what it says about the ones it left alone, and - the reason this file exists - that a run
    /// which decides to do nothing still leaves behind a log describing itself rather than the run
    /// before it. That last rule is written in a comment above <c>FixLog.Start</c> because it shipped
    /// wrong once; a comment cannot fail a build.
    ///
    /// Everything past the eligibility check makes a request per game, so it is not covered, for the
    /// reason TESTING.md gives for the rest of the network code.
    /// </summary>
    public class LibraryFixerTests
    {
        private static LibraryRow Game(
            string name,
            bool hasMatch = true,
            bool hasBackup = false,
            bool isXboxTile = false,
            string imagePath = null)
        {
            return new LibraryRow
            {
                Name = name,
                ImageFilePath = imagePath ?? $"C:\\images\\{name}.png",
                ImageFileName = $"{name}.png",
                HasSteamGridDBMatch = hasMatch,
                HasBackup = hasBackup,
                XboxRenditions = isXboxTile ? new[] { $"{name}-tile" } : null
            };
        }

        private static async Task<string[]> LogLinesAsync(TempFolder temp)
        {
            string text = await temp.ReadAsync("last-fix.log");

            return text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        }

        /// <summary>
        /// Writes a log belonging to an earlier, unrelated run, so a test can tell whether the run under
        /// test replaced it or left it standing.
        /// </summary>
        private static async Task GivenAnEarlierRunAsync(TempFolder temp)
        {
            FixLog.LogFolder = temp.Folder;

            FixLog.Start("An earlier run", "last-fix.log");
            FixLog.Write("the earlier run fixed something");

            await FixLog.SaveAsync();
        }

        [Fact]
        public async Task A_run_with_no_api_key_leaves_a_log_describing_itself()
        {
            // FixLog.Start is called before the API-key check rather than after it. Move it after and
            // this fails twice over: the header still names the earlier run, and that run's lines are
            // still in the file - which reads as if this run had done that work.
            using (var temp = new TempFolder())
            {
                await GivenAnEarlierRunAsync(temp);

                var target = new RecordingArtworkTarget();

                await LibraryFixer.RunAsync(new List<LibraryRow>(), false, null, target);

                string[] lines = await LogLinesAsync(temp);

                Assert.Contains("Fix my library", lines[0]);
                Assert.Contains(lines, line => line.Contains("nothing attempted"));
                Assert.DoesNotContain(lines, line => line.Contains("the earlier run fixed something"));
                Assert.Contains(target.Reports, report => report.Contains("API key is not set"));
            }
        }

        [Fact]
        public async Task A_run_with_nothing_eligible_leaves_a_log_describing_itself()
        {
            // The second of the two early returns, and the one that happens on an ordinary machine:
            // every game already customised, so there is nothing to do. Same rule, separate exit.
            using (var temp = new TempFolder())
            {
                await GivenAnEarlierRunAsync(temp);

                var target = new RecordingArtworkTarget();

                await LibraryFixer.RunAsync(
                    new List<LibraryRow> { Game("Halo", hasMatch: false) }, false, "an-api-key", target);

                string[] lines = await LogLinesAsync(temp);

                Assert.Contains("Fix my library", lines[0]);
                Assert.Contains(lines, line => line.Contains("nothing eligible"));
                Assert.DoesNotContain(lines, line => line.Contains("the earlier run fixed something"));
                Assert.Contains(target.Reports, report => report.Contains("No eligible artworks to fix"));
            }
        }

        [Fact]
        public async Task A_re_fix_run_names_itself_in_the_log()
        {
            // The two runs share a log file, so the header is the only thing distinguishing them.
            using (var temp = new TempFolder())
            {
                FixLog.LogFolder = temp.Folder;

                await LibraryFixer.RunAsync(new List<LibraryRow>(), true, null, new RecordingArtworkTarget());

                Assert.Contains("Re-fix all games", (await LogLinesAsync(temp))[0]);
            }
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task A_blank_api_key_counts_as_no_api_key(string apiKey)
        {
            // Whitespace included, matching SteamGridDbClient's own validation - a key that passed here
            // and failed there would throw out of the client's constructor instead of saying so.
            using (var temp = new TempFolder())
            {
                FixLog.LogFolder = temp.Folder;

                var target = new RecordingArtworkTarget();

                await LibraryFixer.RunAsync(new List<LibraryRow> { Game("Halo") }, false, apiKey, target);

                Assert.Contains(target.Reports, report => report.Contains("API key is not set"));
                Assert.Empty(target.Applied);
            }
        }

        [Fact]
        public async Task Says_how_many_of_the_xbox_apps_own_games_it_left_alone()
        {
            // A library that is mostly Game Pass would otherwise report a fix that quietly did almost
            // nothing. Counted from the same deduplicated set the run walks, so the two cannot disagree.
            using (var temp = new TempFolder())
            {
                FixLog.LogFolder = temp.Folder;

                var target = new RecordingArtworkTarget();

                await LibraryFixer.RunAsync(
                    new List<LibraryRow>
                    {
                        Game("Forza", isXboxTile: true),
                        Game("Halo", isXboxTile: true),
                        Game("Doom", hasMatch: false)
                    },
                    false,
                    "an-api-key",
                    target);

                Assert.Contains(
                    target.Reports,
                    report => report.Contains("2 Xbox app games left alone"));
            }
        }

        [Fact]
        public async Task A_first_party_game_with_no_match_is_not_claimed_as_left_alone()
        {
            // It would have been skipped anyway for having no match, and counting it would claim the
            // first-party rule cost the user something it did not.
            using (var temp = new TempFolder())
            {
                FixLog.LogFolder = temp.Folder;

                var target = new RecordingArtworkTarget();

                await LibraryFixer.RunAsync(
                    new List<LibraryRow> { Game("Forza", hasMatch: false, isXboxTile: true) },
                    false,
                    "an-api-key",
                    target);

                Assert.DoesNotContain(target.Reports, report => report.Contains("left alone"));
            }
        }

        [Fact]
        public void One_image_listed_under_several_stale_entries_is_visited_once()
        {
            // The Xbox app's manifests go stale: a game removed and re-added, or listed by two stores,
            // leaves several entries pointing at one file. Visiting it twice makes the second pass treat
            // the artwork the first pass wrote as if it were the Xbox app's original.
            List<LibraryRow> visited = LibraryFixer.OnePerImage(
                new List<LibraryRow>
                {
                    Game("Halo", imagePath: "C:\\images\\shared.png"),
                    Game("Halo again", imagePath: "C:\\images\\shared.png"),
                    Game("Doom", imagePath: "C:\\images\\doom.png")
                },
                g => true);

            Assert.Equal(2, visited.Count);
            Assert.Equal(new[] { "Halo", "Doom" }, visited.Select(g => g.Name).ToArray());
        }

        [Fact]
        public void A_run_that_is_not_re_fixing_leaves_already_customised_games_alone()
        {
            List<LibraryRow> visited = LibraryFixer.OnePerImage(
                new List<LibraryRow>
                {
                    Game("Halo", hasBackup: true),
                    Game("Doom", hasBackup: false)
                },
                g => FixEligibility.ShouldFix(g.HasSteamGridDBMatch, g.IsXboxTile, g.HasBackup, false));

            Assert.Equal(new[] { "Doom" }, visited.Select(g => g.Name).ToArray());
        }
    }
}
