using System;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The record of which SteamGridDB artwork was applied to each tile.
    ///
    /// Nothing else knows this - the tile on disk is just a PNG - so if the record is wrong the picker
    /// shows the wrong artwork as current and a re-fix silently reshuffles picks.
    /// </summary>
    public class AppliedArtworkStoreTests
    {
        private const string tilePath = @"C:\Games\Images\game.png";

        [Fact]
        public async Task Remembers_the_artwork_applied_to_a_tile()
        {
            using (var temp = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = temp.Folder;

                await AppliedArtworkStore.SetAsync(tilePath, 4321);

                Assert.Equal(4321, await AppliedArtworkStore.GetAsync(tilePath));
            }
        }

        [Fact]
        public async Task Reports_nothing_for_a_tile_it_never_wrote()
        {
            using (var temp = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = temp.Folder;

                Assert.Null(await AppliedArtworkStore.GetAsync(@"C:\Games\Images\never-touched.png"));
            }
        }

        [Fact]
        public async Task Survives_a_reload_from_disk()
        {
            // The widget is restarted every time the Game Bar closes it. A record that only lived in
            // memory would make every session look like a library nobody had ever customised.
            using (var temp = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = temp.Folder;

                await AppliedArtworkStore.SetAsync(tilePath, 99);

                // Re-pointing at the same folder drops the loaded map, so this reads the file back
                AppliedArtworkStore.RecordFolder = temp.Folder;

                Assert.Equal(99, await AppliedArtworkStore.GetAsync(tilePath));
            }
        }

        [Fact]
        public async Task Treats_tile_paths_as_case_insensitive()
        {
            // Manifest entries and folder enumeration disagree about casing on Windows; the same tile
            // reached by two spellings is one tile.
            using (var temp = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = temp.Folder;

                await AppliedArtworkStore.SetAsync(@"C:\Games\Images\Game.png", 7);

                Assert.Equal(7, await AppliedArtworkStore.GetAsync(@"c:\games\images\game.PNG"));
            }
        }

        [Fact]
        public async Task Forgets_a_tile_when_its_customisation_is_reverted()
        {
            using (var temp = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = temp.Folder;

                await AppliedArtworkStore.SetAsync(tilePath, 4321);
                await AppliedArtworkStore.ClearAsync(tilePath);

                Assert.Null(await AppliedArtworkStore.GetAsync(tilePath));
            }
        }

        [Fact]
        public async Task Ignores_writes_that_carry_no_artwork_id()
        {
            // Artwork applied outside the picker arrives with id 0. Recording that would claim a
            // specific SteamGridDB artwork is in use when none is known.
            using (var temp = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = temp.Folder;

                await AppliedArtworkStore.SetAsync(tilePath, 0);

                Assert.Null(await AppliedArtworkStore.GetAsync(tilePath));
            }
        }

        [Fact]
        public async Task Starts_empty_rather_than_throwing_on_a_damaged_record()
        {
            // Losing the record costs the picker its marker. Failing the library load over it would
            // cost the user the whole widget.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync("applied-artwork.json", "{ this is not json");

                AppliedArtworkStore.RecordFolder = temp.Folder;

                Assert.Null(await AppliedArtworkStore.GetAsync(tilePath));

                // and still usable afterwards
                await AppliedArtworkStore.SetAsync(tilePath, 5);

                Assert.Equal(5, await AppliedArtworkStore.GetAsync(tilePath));
            }
        }

        [Fact]
        public async Task Ignores_an_empty_tile_path()
        {
            using (var temp = new TempFolder())
            {
                AppliedArtworkStore.RecordFolder = temp.Folder;

                await AppliedArtworkStore.SetAsync(string.Empty, 3);

                Assert.Null(await AppliedArtworkStore.GetAsync(string.Empty));
                Assert.Null(await AppliedArtworkStore.GetAsync(null));
            }
        }
    }
}
