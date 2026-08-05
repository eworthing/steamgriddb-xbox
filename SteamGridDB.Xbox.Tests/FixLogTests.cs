using System.Linq;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The only trail of what an artwork-fix or library-load run actually did. The official-artwork
    /// gate FixLog exists to make visible failed silently across an entire library once already, and
    /// was only found by diffing artwork IDs on disk by hand - if FixLog itself broke, that diagnostic
    /// would disappear the exact same way, with nothing left to notice it happened.
    /// </summary>
    public class FixLogTests
    {
        [Fact]
        public async Task Writes_the_header_and_every_line_to_disk_in_order()
        {
            using (var temp = new TempFolder())
            {
                FixLog.LogFolder = temp.Folder;

                FixLog.Start("Library load", "run.log");
                FixLog.Write("first line");
                FixLog.Write("second line");

                await FixLog.SaveAsync();

                string[] savedLines = await LinesAsync(temp, "run.log");

                Assert.Equal(3, savedLines.Length);
                Assert.Contains("Library load", savedLines[0]);
                Assert.Equal("first line", savedLines[1]);
                Assert.Equal("second line", savedLines[2]);
            }
        }

        [Fact]
        public async Task Starting_a_new_run_discards_the_previous_runs_lines()
        {
            // A library load and a fix share this same static state (deliberately, per FixLog's own
            // doc comment - lines are held in memory across a whole run). If Start stopped clearing
            // them, a later run's log would silently carry the previous run's lines forward.
            using (var temp = new TempFolder())
            {
                FixLog.LogFolder = temp.Folder;

                FixLog.Start("Run A", "run.log");
                FixLog.Write("A-only line");

                FixLog.Start("Run B", "run.log");
                FixLog.Write("B-only line");

                await FixLog.SaveAsync();

                string[] savedLines = await LinesAsync(temp, "run.log");

                Assert.Equal(2, savedLines.Length);
                Assert.Contains("Run B", savedLines[0]);
                Assert.Equal("B-only line", savedLines[1]);
                Assert.DoesNotContain(savedLines, line => line.Contains("Run A") || line.Contains("A-only line"));
            }
        }

        [Fact]
        public async Task Saves_under_the_file_name_given_to_start()
        {
            // A library load and a fix run write to different files (last-load.log / last-fix.log) so
            // neither overwrites the other's record.
            using (var temp = new TempFolder())
            {
                FixLog.LogFolder = temp.Folder;

                FixLog.Start("Custom run", "custom-name.log");

                await FixLog.SaveAsync();

                Assert.True(temp.Exists("custom-name.log"));
                Assert.False(temp.Exists("last-fix.log"));
            }
        }

        [Fact]
        public async Task Defaults_to_last_fix_log_when_no_file_name_is_given()
        {
            using (var temp = new TempFolder())
            {
                FixLog.LogFolder = temp.Folder;

                FixLog.Start("Default run");

                await FixLog.SaveAsync();

                Assert.True(temp.Exists("last-fix.log"));
            }
        }

        [Fact]
        public async Task Concurrent_writes_do_not_lose_or_corrupt_lines()
        {
            // Safe today only because a library-wide operation and a fix each run under
            // PrimaryWidget's own library-operation guard, so no two callers reach FixLog at once -
            // nothing about Start/Write/SaveAsync's own shape enforced that before this loop's fix.
            // List<T>.Add is not safe for concurrent callers even individually, so without the lock
            // this many concurrent writers can throw or silently drop lines.
            using (var temp = new TempFolder())
            {
                FixLog.LogFolder = temp.Folder;
                FixLog.Start("Concurrent run", "concurrent.log");

                const int writerCount = 50;
                var writers = Enumerable.Range(0, writerCount)
                    .Select(i => Task.Run(() => FixLog.Write($"line {i}")));

                await Task.WhenAll(writers);
                await FixLog.SaveAsync();

                string[] savedLines = await LinesAsync(temp, "concurrent.log");

                // One header line plus exactly one line per writer - none lost, none corrupted.
                Assert.Equal(writerCount + 1, savedLines.Length);
            }
        }

        private static async Task<string[]> LinesAsync(TempFolder temp, string fileName)
        {
            string text = await temp.ReadAsync(fileName);

            return text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        }
    }
}
