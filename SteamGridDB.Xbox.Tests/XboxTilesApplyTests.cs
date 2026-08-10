using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Xbox;

using Windows.Storage.Streams;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Writing one piece of artwork across a first-party game's several cached renditions.
    ///
    /// The artwork is decoded once for the whole call rather than once per rendition - see the
    /// <see cref="XboxTiles"/> class doc - so what matters here is that the shortcut is invisible from
    /// outside: every rendition still gets a square cut from the *source* frame, not from another
    /// rendition's own square, and a decode failure still costs every rendition its own line in
    /// <c>Failures</c> rather than collapsing to nothing written and nothing reported.
    /// </summary>
    public class XboxTilesApplyTests
    {
        /// <summary>
        /// A rendition file standing in for one the Xbox app already cached: a real JPEG of the given
        /// pixel size, so <c>RenditionAsync</c> can read its width, padded out to at least
        /// <paramref name="minRoom"/> bytes so <c>RenditionAsync</c> reports that much room too.
        /// </summary>
        private static async Task<uint> WriteRenditionPlaceholderAsync(TempFolder cache, string fileName, int pixelSize, uint minRoom)
        {
            byte[] placeholder = TestImages.ToArray(await TestImages.JpegAsync(pixelSize, pixelSize));
            uint room = Math.Max((uint)placeholder.Length, minRoom);
            var padded = new byte[room];

            Array.Copy(placeholder, padded, placeholder.Length);

            await cache.WriteBytesAsync(fileName, padded);

            return room;
        }

        [Fact]
        public async Task Apply_encodes_every_rendition_from_the_source_frame_directly()
        {
            // Two sizes far enough apart that scaling one rendition's own square down to the other's
            // size - rather than cutting both from the one decoded frame - would produce different
            // bytes. Compared against TileImage.EncodeSquareJpegAsync run independently per size, which
            // always decodes fresh: if ApplyAsync's hoisted frame ever fed a rendition anything but the
            // source, this is what would catch it.
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                XboxTileStore.RecordFolder = records.Folder;

                IBuffer artwork = await TestImages.QuadrantPngAsync(512);

                IBuffer unboundedBig = await TileImage.EncodeSquareJpegAsync(artwork, 329);
                IBuffer unboundedSmall = await TileImage.EncodeSquareJpegAsync(artwork, 84);

                uint roomBig = await WriteRenditionPlaceholderAsync(cache, "big", 329, unboundedBig.Length * 2);
                uint roomSmall = await WriteRenditionPlaceholderAsync(cache, "small", 84, unboundedSmall.Length * 2);

                (int written, IReadOnlyList<string> failures, bool hasBackup) =
                    await XboxTiles.ApplyAsync(cache.Folder, new[] { "big", "small" }, artwork);

                Assert.Equal(2, written);
                Assert.Empty(failures);
                Assert.True(hasBackup);

                byte[] expectedBig = TestImages.ToArray(await TileImage.EncodeSquareJpegAsync(artwork, 329, roomBig));
                byte[] expectedSmall = TestImages.ToArray(await TileImage.EncodeSquareJpegAsync(artwork, 84, roomSmall));

                // In-place writes pad the tail with zeroes rather than resizing the file - see
                // ArtworkFiles.WriteMode - so only the front of each written file is the tile itself
                Assert.Equal(expectedBig, cache.ReadBytes("big")[0..expectedBig.Length]);
                Assert.Equal(expectedSmall, cache.ReadBytes("small")[0..expectedSmall.Length]);
            }
        }

        [Fact]
        public async Task Apply_reports_one_failure_per_rendition_when_the_artwork_will_not_decode()
        {
            // Before the artwork's decode was hoisted, an undecodable image made
            // TileImage.EncodeSquareJpegAsync return null once per rendition, so Failures still had one
            // line per rendition. Hoisted, the decode now runs once for the whole call - so this pins
            // that a single failed decode still fans back out to one failure per rendition, rather than
            // leaving the caller with Written == 0 and Failures empty, which the widget would report as
            // this game having no cached tile to write to at all.
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                XboxTileStore.RecordFolder = records.Folder;

                await WriteRenditionPlaceholderAsync(cache, "big", 329, 0);
                await WriteRenditionPlaceholderAsync(cache, "small", 84, 0);

                (int written, IReadOnlyList<string> failures, bool hasBackup) = await XboxTiles.ApplyAsync(
                    cache.Folder, new[] { "big", "small" }, TestImages.Bytes("not an image"));

                Assert.Equal(0, written);
                Assert.Equal(2, failures.Count);
                Assert.False(hasBackup);
            }
        }

        [Fact]
        public async Task Apply_skips_a_rendition_no_longer_in_the_cache_without_reporting_a_failure()
        {
            // A rendition the Xbox app has since evicted is skipped rather than recreated - see the
            // class doc - and skipped is not the same as failed: nothing is added to Failures for it.
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                XboxTileStore.RecordFolder = records.Folder;

                await WriteRenditionPlaceholderAsync(cache, "present", 64, 0);

                (int written, IReadOnlyList<string> failures, bool hasBackup) = await XboxTiles.ApplyAsync(
                    cache.Folder, new[] { "present", "gone" }, await TestImages.OpaquePngAsync(64, 64));

                Assert.Equal(1, written);
                Assert.Empty(failures);
                Assert.True(hasBackup);
            }
        }
    }
}
