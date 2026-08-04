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
    }
}
