using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Xbox;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Finding applied-artwork records whose image no game claims.
    ///
    /// Every other cleanup walks forwards - from a game to its renditions - so a record whose file has
    /// stopped being any game's rendition is reachable from none of them: nothing enumerates it, so
    /// nothing thinks to clear it. This walks the records instead, which is the only direction that
    /// finds one, and also the only direction that can do real damage: it decides to delete on the
    /// strength of an absence. The guard tests below matter more than the rule.
    /// </summary>
    public class OrphanedArtworkRecordTests
    {
        private const string cache = @"C:\Users\x\AppData\Local\Packages\Microsoft.GamingApp_8wekyb3d8bbwe\LocalCache\ImageCache";

        private static ISet<string> Tracked(params string[] fileNames)
        {
            return new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void A_cached_image_no_game_claims_is_orphaned()
        {
            Assert.True(XboxTiles.IsOrphanedRecord(
                Path.Combine(cache, "14163050974037971509").ToLowerInvariant(),
                cache,
                Tracked("111", "222")));
        }

        [Fact]
        public void A_cached_image_a_game_still_claims_is_not()
        {
            Assert.False(XboxTiles.IsOrphanedRecord(
                Path.Combine(cache, "222").ToLowerInvariant(),
                cache,
                Tracked("111", "222")));
        }

        [Fact]
        public void A_third_party_record_is_never_judged()
        {
            // These live under ThirdPartyLibraries and are none of this rule's business. It has no idea
            // what accounts for them, and a rule that guessed would delete every one.
            Assert.False(XboxTiles.IsOrphanedRecord(
                @"c:\users\x\appdata\local\packages\microsoft.gamingapp_8wekyb3d8bbwe\localstate\thirdpartylibraries\steam\steam_413150.png",
                cache,
                Tracked("111")));
        }

        [Fact]
        public void An_empty_tracked_set_means_unknown_not_unclaimed()
        {
            // The one way this could do real harm: a tile record that failed to load would make every
            // first-party record look orphaned on the same pass.
            Assert.False(XboxTiles.IsOrphanedRecord(
                Path.Combine(cache, "14163050974037971509").ToLowerInvariant(),
                cache,
                Tracked()));

            Assert.False(XboxTiles.IsOrphanedRecord(
                Path.Combine(cache, "14163050974037971509").ToLowerInvariant(),
                cache,
                null));
        }

        [Fact]
        public void Compares_paths_without_caring_about_case()
        {
            // Record keys are lowercased on the way in; the folder's real path is not.
            Assert.False(XboxTiles.IsOrphanedRecord(
                Path.Combine(cache, "ABC").ToLowerInvariant(),
                cache,
                Tracked("abc")));
        }

        [Fact]
        public void Ignores_a_record_in_some_other_folder_entirely()
        {
            Assert.False(XboxTiles.IsOrphanedRecord(
                @"d:\somewhere\else\14163050974037971509",
                cache,
                Tracked("111")));
        }

        [Fact]
        public async Task Forgets_only_the_records_the_caller_condemns()
        {
            using (var records = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = records.Folder;

                await AppliedArtworkStore.SetAsync(@"C:\cache\orphan", 1);
                await AppliedArtworkStore.SetAsync(@"C:\cache\keep", 2);

                await AppliedArtworkStore.ForgetWhereAsync(key => key.EndsWith("orphan"));

                Assert.Null(await AppliedArtworkStore.GetAsync(@"C:\cache\orphan"));
                Assert.Equal(2, await AppliedArtworkStore.GetAsync(@"C:\cache\keep"));
            }
        }

        [Fact]
        public async Task A_sweep_that_finds_nothing_leaves_the_file_untouched()
        {
            // This runs on every library load. Rewriting an untouched record each time is a pointless
            // chance to corrupt it.
            using (var records = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = records.Folder;

                await AppliedArtworkStore.SetAsync(@"C:\cache\keep", 2);

                var written = File.GetLastWriteTimeUtc(Path.Combine(records.FullPath, "applied-artwork.json"));

                await Task.Delay(50);
                await AppliedArtworkStore.ForgetWhereAsync(key => false);

                Assert.Equal(written, File.GetLastWriteTimeUtc(Path.Combine(records.FullPath, "applied-artwork.json")));
                Assert.Equal(2, await AppliedArtworkStore.GetAsync(@"C:\cache\keep"));
            }
        }

        [Fact]
        public async Task Survives_being_handed_no_predicate()
        {
            using (var records = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = records.Folder;

                await AppliedArtworkStore.SetAsync(@"C:\cache\keep", 2);
                await AppliedArtworkStore.ForgetWhereAsync(null);

                Assert.Equal(2, await AppliedArtworkStore.GetAsync(@"C:\cache\keep"));
            }
        }
    }
}
