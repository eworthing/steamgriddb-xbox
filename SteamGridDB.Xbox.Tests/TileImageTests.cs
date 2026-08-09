using System;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;

using Windows.Storage.Streams;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Making downloaded artwork fit the .png name the Xbox app owns.
    /// </summary>
    public class TileImageTests
    {
        [Fact]
        public async Task Leaves_artwork_that_is_already_a_png_untouched()
        {
            // Most artwork already is a PNG. Re-encoding all of it would cost a decode and an encode
            // per game and lose nothing but time.
            IBuffer png = await TestImages.PngAsync();

            IBuffer result = await TileImage.EnsurePngAsync(png);

            Assert.Equal(TestImages.ToArray(png), TestImages.ToArray(result));
        }

        [Fact]
        public async Task Re_encodes_artwork_that_is_not_a_png()
        {
            // The Xbox app reads the tile by its .png name. A JPEG written under it renders as a
            // broken tile rather than failing loudly.
            IBuffer jpeg = await TestImages.JpegAsync();

            Assert.False(TestImages.IsPng(TestImages.ToArray(jpeg)));

            IBuffer result = await TileImage.EnsurePngAsync(jpeg);

            Assert.True(TestImages.IsPng(TestImages.ToArray(result)));
        }

        [Fact]
        public async Task Writes_undecodable_bytes_through_unchanged()
        {
            // Better a mislabelled tile that might render than no tile at all - the documented
            // fallback. Asserted so a future change to throw instead is a deliberate one.
            IBuffer junk = TestImages.Bytes("this is not an image");

            IBuffer result = await TileImage.EnsurePngAsync(junk);

            Assert.Equal(TestImages.ToArray(junk), TestImages.ToArray(result));
        }

        [Fact]
        public async Task Passes_null_through_rather_than_throwing()
        {
            Assert.Null(await TileImage.EnsurePngAsync(null));
        }

        // ---- FillsTileAsync ----

        [Fact]
        public async Task Fills_tile_when_the_image_is_opaque_at_every_corner()
        {
            IBuffer opaque = await TestImages.OpaquePngAsync();

            Assert.True(await TileImage.FillsTileAsync(opaque));
        }

        [Fact]
        public async Task Does_not_fill_tile_when_the_corners_are_transparent()
        {
            // The shape a rounded icon or a physical-media mockup produces - opaque centre, transparent
            // corners - which the tile-fill gate exists to reject before it ever reaches the ranked pick.
            IBuffer roundedIcon = await TestImages.PngWithTransparentCornersAsync();

            Assert.False(await TileImage.FillsTileAsync(roundedIcon));
        }

        // ---- CropPortraitToTileAsync ----

        [Fact]
        public async Task Crop_returns_null_for_images_that_are_not_taller_than_wide()
        {
            IBuffer square = await TestImages.PngAsync(width: 32, height: 32);

            Assert.Null(await TileImage.CropPortraitToTileAsync(square));
        }

        [Fact]
        public async Task Crops_a_portrait_image_to_a_square_matching_the_source_width()
        {
            IBuffer portrait = await TestImages.OpaquePngAsync(width: 64, height: 192);

            IBuffer cropped = await TileImage.CropPortraitToTileAsync(portrait);

            (uint Width, uint Height) size = await TileImage.WithDecoderAsync(
                cropped,
                decoder => Task.FromResult((decoder.PixelWidth, decoder.PixelHeight)),
                (0u, 0u),
                "decode failed");

            Assert.Equal((64u, 64u), size);
        }

        [Fact]
        public async Task Crop_window_is_drawn_toward_a_high_detail_band_at_the_top()
        {
            // A flat, featureless band carries no edge energy; a checkerboard band carries a lot. The
            // window BestVerticalCropAsync places should land on the band with content, not the blank one.
            IBuffer portrait = await TestImages.PortraitWithDetailBandAsync(width: 64, totalHeight: 256, checkerboardOnTop: true);

            IBuffer cropped = await TileImage.CropPortraitToTileAsync(portrait);

            Assert.True(await ContainsDetailAsync(cropped));
        }

        [Fact]
        public async Task Crop_window_is_drawn_toward_a_high_detail_band_at_the_bottom()
        {
            IBuffer portrait = await TestImages.PortraitWithDetailBandAsync(width: 64, totalHeight: 256, checkerboardOnTop: false);

            IBuffer cropped = await TileImage.CropPortraitToTileAsync(portrait);

            Assert.True(await ContainsDetailAsync(cropped));
        }

        // ---- First-party tiles: an exact-size JPEG rather than a PNG of whatever size arrived ----

        [Theory]
        [InlineData(72)]
        [InlineData(224)]
        [InlineData(329)]
        public async Task EncodeSquareJpeg_produces_exactly_the_requested_size(int pixels)
        {
            // The Xbox app renders a cached image at the size it downloaded it, and keeps no record of
            // what that was beyond the file - so a replacement of the wrong size is the one way a
            // replacement gets noticed
            IBuffer tile = await TileImage.EncodeSquareJpegAsync(await TestImages.OpaquePngAsync(512, 512), pixels);

            Assert.Equal(((uint)pixels, (uint)pixels), await SizeOfAsync(tile));
        }

        [Fact]
        public async Task EncodeSquareJpeg_produces_a_jpeg_not_a_png()
        {
            IBuffer tile = await TileImage.EncodeSquareJpegAsync(await TestImages.OpaquePngAsync(64, 64), 72);

            Assert.False(TestImages.IsPng(TestImages.ToArray(tile)));
        }

        [Fact]
        public async Task EncodeSquareJpeg_squares_portrait_artwork_rather_than_squashing_it()
        {
            // SteamGridDB grids are not all square, and a tile that is has to come from somewhere -
            // the centre, so the subject survives
            IBuffer tile = await TileImage.EncodeSquareJpegAsync(
                await TestImages.PortraitWithDetailBandAsync(width: 64, totalHeight: 256, checkerboardOnTop: true), 128);

            Assert.Equal((128u, 128u), await SizeOfAsync(tile));
        }

        [Fact]
        public async Task EncodeSquareJpeg_upscales_artwork_smaller_than_the_tile()
        {
            IBuffer tile = await TileImage.EncodeSquareJpegAsync(await TestImages.OpaquePngAsync(32, 32), 329);

            Assert.Equal((329u, 329u), await SizeOfAsync(tile));
        }

        [Fact]
        public async Task EncodeSquareJpeg_returns_null_rather_than_writing_junk()
        {
            // Nothing decodable means nothing to write. Returning the original bytes - which is what
            // EnsurePngAsync does - would put a non-image into the Xbox app's cache under a name it
            // expects to be able to render.
            Assert.Null(await TileImage.EncodeSquareJpegAsync(TestImages.Bytes("not an image"), 224));
            Assert.Null(await TileImage.EncodeSquareJpegAsync(null, 224));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task EncodeSquareJpeg_refuses_a_size_that_is_not_a_size(int pixels)
        {
            Assert.Null(await TileImage.EncodeSquareJpegAsync(await TestImages.OpaquePngAsync(64, 64), pixels));
        }

        [Fact]
        public async Task EncodeSquareJpeg_drops_quality_until_the_tile_fits_its_budget()
        {
            // A first-party tile is written into the space the Store's own download left, without the
            // file being resized, so a budget it cannot meet is a tile that cannot be written at all.
            // Trading quality for that is worth it; the alternative is no artwork.
            IBuffer artwork = await TestImages.QuadrantPngAsync(329);
            IBuffer unbounded = await TileImage.EncodeSquareJpegAsync(artwork, 329);

            // A byte under what the top quality produced, so the budget is one the first attempt cannot
            // meet and any lower one can. Expressed against the fixture's own output rather than as a
            // fixed fraction of it: a flat synthetic image is already near the smallest a JPEG of its
            // dimensions can be, so what fraction is reachable is a property of the fixture, not of the
            // rule being tested.
            uint budget = unbounded.Length - 1;

            IBuffer fitted = await TileImage.EncodeSquareJpegAsync(artwork, 329, budget);

            Assert.NotNull(fitted);
            Assert.True(fitted.Length <= budget, $"{fitted.Length} bytes exceeds the {budget} byte budget");
            Assert.Equal((329u, 329u), await SizeOfAsync(fitted));
        }

        [Fact]
        public async Task EncodeSquareJpeg_at_its_best_quality_is_what_no_budget_produces()
        {
            // A budget it already meets must not cost anything. Everything written before this existed
            // was encoded at the top quality and has to go on being.
            IBuffer artwork = await TestImages.QuadrantPngAsync(329);
            IBuffer unbounded = await TileImage.EncodeSquareJpegAsync(artwork, 329);

            IBuffer generous = await TileImage.EncodeSquareJpegAsync(artwork, 329, unbounded.Length * 4);

            Assert.Equal(unbounded.Length, generous.Length);
        }

        [Fact]
        public async Task EncodeSquareJpeg_returns_nothing_for_a_budget_it_cannot_meet()
        {
            // Handing back the smallest it managed would only move the refusal to the write, where there
            // is less to say about why
            Assert.Null(await TileImage.EncodeSquareJpegAsync(await TestImages.QuadrantPngAsync(329), 329, 64));
        }

        private static Task<(uint Width, uint Height)> SizeOfAsync(IBuffer image)
        {
            return TileImage.WithDecoderAsync(
                image,
                decoder => Task.FromResult((decoder.PixelWidth, decoder.PixelHeight)),
                (0u, 0u),
                "decode failed");
        }

        /// <summary>
        /// True when the image is not a single flat colour - i.e. the crop landed on
        /// <see cref="TestImages.PortraitWithDetailBandAsync"/>'s checkerboard band rather than its flat
        /// grey one. A mutation that reverses the window-selection direction, or one that always crops to
        /// a fixed offset, would land on the flat band instead and this would return false.
        /// </summary>
        private static async Task<bool> ContainsDetailAsync(IBuffer image)
        {
            return await TileImage.WithDecoderAsync(
                image,
                async decoder =>
                {
                    byte[] pixels = await TileImage.ScaledPixelsAsync(
                        decoder, null, decoder.PixelWidth, decoder.PixelHeight, Windows.Graphics.Imaging.BitmapAlphaMode.Straight);

                    byte first = pixels[0];

                    for (int i = 0; i < pixels.Length; i += 4)
                    {
                        if (pixels[i] != first)
                        {
                            return true;
                        }
                    }

                    return false;
                },
                false,
                "decode failed");
        }
    }
}
