using System;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;

using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Writing a tile without resizing the file it goes into.
    ///
    /// The Xbox app keeps the tiles it is currently showing memory-mapped. Windows lets a mapped file's
    /// contents change but not its length, and refuses anything that would resize or replace it with
    /// ERROR_USER_MAPPED_FILE - so the create-with-replace every other path makes fails on precisely the
    /// tiles someone is looking at, which is to say whenever they have their library open to see whether
    /// the change worked. This is the write that does not.
    ///
    /// A throwaway directory cannot hold a mapped file, so what these pin is the property that makes the
    /// write legal rather than the refusal itself: the length never changes when the bytes fit, the
    /// artwork and the saved copy of it stay identical so the overwrite check can still tell them apart,
    /// and bytes that cannot fit - sidecars saved before padding existed, going back onto a tile the app
    /// has since rewritten at another length - grow the file rather than being refused outright, because
    /// only a mapped file forbids that and the file system says so itself when one does.
    /// </summary>
    public class ArtworkFilesInPlaceTests
    {
        private const string image = "tile";
        private const string backup = "tile.bak";
        private const string customised = "tile.new";

        /// <summary>An existing cached tile of a known length, standing in for a Store download.</summary>
        private static async Task<uint> WriteCachedTileAsync(TempFolder temp, int bytes)
        {
            await temp.WriteAsync(image, new string('x', bytes));

            return (uint)bytes;
        }

        [Fact]
        public async Task An_in_place_write_leaves_the_file_exactly_as_long_as_it_was()
        {
            using (var temp = new TempFolder())
            {
                uint room = await WriteCachedTileAsync(temp, 4096);

                await ArtworkFiles.ApplyEncodedAsync(
                    temp.Folder, image, temp.Folder, TestImages.Bytes("a much shorter tile"), ArtworkFiles.WriteMode.InPlace);

                StorageFile written = await temp.Folder.GetFileAsync(image);

                Assert.Equal(room, (uint)(await written.GetBasicPropertiesAsync()).Size);
            }
        }

        [Fact]
        public async Task The_saved_customisation_is_byte_for_byte_what_the_tile_now_holds()
        {
            // The overwrite check compares the two. Saving the artwork unpadded while writing it padded
            // would make them differ from the moment they were written, and every load would decide the
            // Xbox app had overwritten a tile it had not touched.
            using (var temp = new TempFolder())
            {
                await WriteCachedTileAsync(temp, 2048);

                await ArtworkFiles.ApplyEncodedAsync(
                    temp.Folder, image, temp.Folder, TestImages.Bytes("artwork"), ArtworkFiles.WriteMode.InPlace);

                Assert.Equal(temp.ReadBytes(image), temp.ReadBytes(customised));
            }
        }

        [Fact]
        public async Task Padding_leaves_the_artwork_itself_untouched_at_the_front()
        {
            using (var temp = new TempFolder())
            {
                await WriteCachedTileAsync(temp, 512);

                await ArtworkFiles.ApplyEncodedAsync(
                    temp.Folder, image, temp.Folder, TestImages.Bytes("artwork"), ArtworkFiles.WriteMode.InPlace);

                byte[] onDisk = temp.ReadBytes(image);
                byte[] artwork = TestImages.ToArray(TestImages.Bytes("artwork"));

                Assert.Equal(artwork, onDisk[0..artwork.Length]);

                // and nothing of the tile that was there before survives into the tail
                Assert.All(onDisk[artwork.Length..], b => Assert.Equal(0, b));
            }
        }

        [Fact]
        public async Task Artwork_larger_than_the_file_grows_it_rather_than_failing()
        {
            // The pad-in-place write exists for fitting into a mapped file without resizing it. When
            // the bytes cannot fit, the only write left is one that resizes - which nothing forbids
            // unless the Xbox app has the file mapped at that moment, and then the file system itself
            // refuses, loudly, for the callers to contain per rendition. A throwaway directory has
            // nothing mapped, so here the growth simply happens.
            using (var temp = new TempFolder())
            {
                await WriteCachedTileAsync(temp, 16);

                await ArtworkFiles.ApplyEncodedAsync(
                    temp.Folder, image, temp.Folder, TestImages.Bytes(new string('y', 64)), ArtworkFiles.WriteMode.InPlace);

                Assert.Equal(new string('y', 64), await temp.ReadAsync(image));
                Assert.Equal(new string('y', 64), await temp.ReadAsync(customised));
            }
        }

        [Fact]
        public async Task A_padded_jpeg_still_decodes_at_its_own_size()
        {
            // The assumption the whole scheme rests on: a decoder stops at the end-of-image marker, so
            // the padding a tile is written with is not part of the picture. If this were not true every
            // first-party tile would be written as a corrupt file.
            using (var temp = new TempFolder())
            {
                IBuffer jpeg = await TestImages.JpegAsync(329, 329);

                await temp.WriteAsync(image, new string('x', (int)jpeg.Length + 8192));

                await ArtworkFiles.ApplyEncodedAsync(temp.Folder, image, temp.Folder, jpeg, ArtworkFiles.WriteMode.InPlace);

                using (IRandomAccessStream stream = await (await temp.Folder.GetFileAsync(image)).OpenReadAsync())
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                    Assert.Equal(329u, decoder.PixelWidth);
                    Assert.Equal(329u, decoder.PixelHeight);
                }
            }
        }

        [Fact]
        public async Task An_in_place_restore_puts_the_original_back_and_drops_the_backup()
        {
            using (var temp = new TempFolder())
            {
                await WriteCachedTileAsync(temp, 64);

                byte[] original = temp.ReadBytes(image);

                await ArtworkFiles.ApplyEncodedAsync(
                    temp.Folder, image, temp.Folder, TestImages.Bytes("artwork"), ArtworkFiles.WriteMode.InPlace);

                Assert.Equal(
                    ArtworkFiles.RestoreOutcome.Restored,
                    await ArtworkFiles.RestoreOriginalAsync(temp.Folder, image, temp.Folder, ArtworkFiles.WriteMode.InPlace));

                Assert.Equal(original, temp.ReadBytes(image));

                await Assert.ThrowsAsync<System.IO.FileNotFoundException>(
                    () => temp.Folder.GetFileAsync(backup).AsTask());
            }
        }

        [Fact]
        public async Task An_in_place_reapply_puts_the_customisation_back_over_a_tile_the_app_replaced()
        {
            using (var temp = new TempFolder())
            {
                await WriteCachedTileAsync(temp, 128);

                await ArtworkFiles.ApplyEncodedAsync(
                    temp.Folder, image, temp.Folder, TestImages.Bytes("artwork"), ArtworkFiles.WriteMode.InPlace);

                byte[] applied = temp.ReadBytes(image);

                // The Xbox app re-downloads its own artwork over the tile, at the same length it always is
                await temp.WriteAsync(image, new string('z', 128));

                Assert.Equal(
                    ArtworkFiles.ReapplyOutcome.Reapplied,
                    await ArtworkFiles.ReapplyCustomisationAsync(temp.Folder, image, temp.Folder, ArtworkFiles.WriteMode.InPlace));

                Assert.Equal(applied, temp.ReadBytes(image));
            }
        }

        // ---- Sidecars written before padding existed: the vault every upgrade inherits ----

        [Fact]
        public async Task Restore_grows_a_tile_shorter_than_the_backup()
        {
            // The state every pre-padding customisation is in: the tile holds the artwork at its own
            // length, and the backup - the Xbox app's download - is longer. The whole original has to
            // go back regardless; a length difference is not a reason to strand it in the vault.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, "old artwork");
                await temp.WriteAsync(backup, new string('o', 64));
                await temp.WriteAsync(customised, "old artwork");

                Assert.Equal(
                    ArtworkFiles.RestoreOutcome.Restored,
                    await ArtworkFiles.RestoreOriginalAsync(temp.Folder, image, temp.Folder, ArtworkFiles.WriteMode.InPlace));

                Assert.Equal(new string('o', 64), await temp.ReadAsync(image));
                Assert.False(temp.Exists(backup));
                Assert.False(temp.Exists(customised));
            }
        }

        [Fact]
        public async Task Restore_keeps_the_sidecars_when_the_write_fails()
        {
            // The write into the Xbox app's live cache is the one step of a restore that can be
            // refused. A refusal must cost nothing but the retry: the backup is unrecoverable and the
            // customisation is the only copy of what the user chose, so both have to still be there.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, "customised");
                await temp.WriteAsync(backup, "original");
                await temp.WriteAsync(customised, "customised");

                using (System.IO.File.Open(
                    System.IO.Path.Combine(temp.FullPath, image),
                    System.IO.FileMode.Open,
                    System.IO.FileAccess.ReadWrite,
                    System.IO.FileShare.None))
                {
                    await Assert.ThrowsAnyAsync<Exception>(() => ArtworkFiles.RestoreOriginalAsync(
                        temp.Folder, image, temp.Folder, ArtworkFiles.WriteMode.InPlace));
                }

                Assert.True(temp.Exists(backup));
                Assert.True(temp.Exists(customised));
            }
        }

        [Fact]
        public async Task Reapply_updates_a_saved_customisation_that_predates_padding()
        {
            // A sidecar from before padding existed holds the artwork at its own length. Once the
            // Xbox app has overwritten the tile at its longer natural length, the reapply pads on the
            // way in - and must bring the saved copy up to date, or the overwrite check compares
            // unequal lengths forever and every load rewrites a tile that is already right.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, new string('z', 64));
                await temp.WriteAsync(customised, "artwork");

                Assert.Equal(
                    ArtworkFiles.ReapplyOutcome.Reapplied,
                    await ArtworkFiles.ReapplyCustomisationAsync(temp.Folder, image, temp.Folder, ArtworkFiles.WriteMode.InPlace));

                byte[] onDisk = temp.ReadBytes(image);
                byte[] artwork = TestImages.ToArray(TestImages.Bytes("artwork"));

                Assert.Equal(64, onDisk.Length);
                Assert.Equal(artwork, onDisk[0..artwork.Length]);

                // The saved copy is now exactly what the tile holds, so the next load's overwrite
                // check passes and writes nothing
                Assert.Equal(onDisk, temp.ReadBytes(customised));
            }
        }

        [Fact]
        public async Task Reapply_grows_a_tile_shorter_than_the_saved_customisation()
        {
            // The other pre-padding state: artwork longer than the Xbox app's own download, over a
            // tile the app has since rewritten at its shorter natural length. The customisation still
            // has to go back - by growing the file, which only a mapped tile refuses.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, new string('z', 16));
                await temp.WriteAsync(customised, new string('a', 64));

                Assert.Equal(
                    ArtworkFiles.ReapplyOutcome.Reapplied,
                    await ArtworkFiles.ReapplyCustomisationAsync(temp.Folder, image, temp.Folder, ArtworkFiles.WriteMode.InPlace));

                Assert.Equal(new string('a', 64), await temp.ReadAsync(image));
                Assert.Equal(new string('a', 64), await temp.ReadAsync(customised));
            }
        }

        [Fact]
        public async Task A_tile_the_app_never_cached_is_simply_created()
        {
            // No file means no length to preserve and nothing that could be mapped, so the in-place rule
            // has nothing to say and the write goes through as an ordinary one
            using (var temp = new TempFolder())
            {
                await ArtworkFiles.ApplyEncodedAsync(
                    temp.Folder, image, temp.Folder, TestImages.Bytes("artwork"), ArtworkFiles.WriteMode.InPlace);

                Assert.Equal("artwork", await temp.ReadAsync(image));
            }
        }

        [Fact]
        public async Task Replace_mode_is_unchanged_and_still_resizes_freely()
        {
            // Third-party tiles are this widget's own files in a folder the Xbox app only reads. Nothing
            // maps them, their length is free to change, and none of the above applies to them.
            using (var temp = new TempFolder())
            {
                await WriteCachedTileAsync(temp, 4096);

                await ArtworkFiles.ApplyEncodedAsync(temp.Folder, image, temp.Folder, TestImages.Bytes("artwork"));

                Assert.Equal("artwork", await temp.ReadAsync(image));
            }
        }
    }
}
