using System.IO;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Xbox;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Dropping the applied-artwork records of first-party renditions that cannot be carrying a
    /// customisation.
    ///
    /// The record is what marks an artwork <em>In use</em> in the picker, and nothing else knows it -
    /// a tile on disk is just an image. Two states leave one behind describing nothing: a rendition
    /// that has left the game's set, whose path nothing will look up again, and a game with no backup,
    /// which cannot be customised at all because applying always leaves a backup behind it.
    ///
    /// What matters most here is what this does <em>not</em> touch. It corrects a claim about artwork;
    /// deleting the artwork, its backup or its saved customisation while doing so would trade a wrong
    /// label for a real loss.
    /// </summary>
    public class XboxTilesArtworkRecordTests
    {
        private static string PathIn(TempFolder folder, string fileName)
        {
            return Path.Combine(folder.FullPath, fileName);
        }

        [Fact]
        public async Task Forgets_the_record_of_a_rendition_it_is_given()
        {
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = records.Folder;

                await AppliedArtworkStore.SetAsync(PathIn(cache, "123456"), 4321);

                await XboxTiles.ForgetArtworkRecordsAsync(cache.Folder, new[] { "123456" });

                Assert.Null(await AppliedArtworkStore.GetAsync(PathIn(cache, "123456")));
            }
        }

        [Fact]
        public async Task Leaves_the_records_of_renditions_it_is_not_given()
        {
            // The sibling renditions of a game that lost only one of them, and every third-party
            // game's record, all live in the same store.
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = records.Folder;

                await AppliedArtworkStore.SetAsync(PathIn(cache, "111"), 11);
                await AppliedArtworkStore.SetAsync(PathIn(cache, "222"), 22);

                await XboxTiles.ForgetArtworkRecordsAsync(cache.Folder, new[] { "111" });

                Assert.Null(await AppliedArtworkStore.GetAsync(PathIn(cache, "111")));
                Assert.Equal(22, await AppliedArtworkStore.GetAsync(PathIn(cache, "222")));
            }
        }

        [Fact]
        public async Task Never_touches_the_tile_its_backup_or_its_saved_customisation()
        {
            // The point of the whole thing. A wrong "In use" marker is worth less than the artwork it
            // describes, so this may only ever remove the claim.
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = records.Folder;

                await cache.WriteAsync("123456", "the tile");
                await cache.WriteAsync("123456.bak", "the original");
                await cache.WriteAsync("123456.new", "the customisation");

                await AppliedArtworkStore.SetAsync(PathIn(cache, "123456"), 4321);

                await XboxTiles.ForgetArtworkRecordsAsync(cache.Folder, new[] { "123456" });

                Assert.True(File.Exists(PathIn(cache, "123456")));
                Assert.True(File.Exists(PathIn(cache, "123456.bak")));
                Assert.True(File.Exists(PathIn(cache, "123456.new")));
            }
        }

        [Fact]
        public async Task Forgets_every_rendition_of_a_game_at_once()
        {
            // The no-backup case hands over the game's whole set, since any of them could have been
            // the largest at the time the record was written.
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = records.Folder;

                await AppliedArtworkStore.SetAsync(PathIn(cache, "aaa"), 1);
                await AppliedArtworkStore.SetAsync(PathIn(cache, "bbb"), 2);

                await XboxTiles.ForgetArtworkRecordsAsync(cache.Folder, new[] { "aaa", "bbb" });

                Assert.Null(await AppliedArtworkStore.GetAsync(PathIn(cache, "aaa")));
                Assert.Null(await AppliedArtworkStore.GetAsync(PathIn(cache, "bbb")));
            }
        }

        [Fact]
        public async Task Does_nothing_when_there_is_nothing_to_forget()
        {
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = records.Folder;

                await AppliedArtworkStore.SetAsync(PathIn(cache, "keep"), 7);

                await XboxTiles.ForgetArtworkRecordsAsync(cache.Folder, null);
                await XboxTiles.ForgetArtworkRecordsAsync(null, new[] { "keep" });

                Assert.Equal(7, await AppliedArtworkStore.GetAsync(PathIn(cache, "keep")));
            }
        }
    }
}
