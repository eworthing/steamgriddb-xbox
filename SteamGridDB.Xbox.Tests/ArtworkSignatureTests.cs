using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;

using Windows.Storage.Streams;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The official-artwork replacement gate's image-comparison half (see
    /// <see cref="ArtworkDownloader.FindOfficialLookalikeAsync"/>) - whether two images share a palette
    /// and an arrangement of light and dark. The gate's decision logic built on top of these two
    /// measures is covered in <see cref="ArtworkDownloaderTests"/>; downloading the images themselves is
    /// excluded from this suite for the reason TESTING.md gives under "Anything over the network".
    /// </summary>
    public class ArtworkSignatureTests
    {
        [Fact]
        public async Task CreateAsync_returns_null_for_undecodable_bytes()
        {
            IBuffer junk = TestImages.Bytes("this is not an image");

            Assert.Null(await ArtworkSignature.CreateAsync(junk));
        }

        // ---- ColourMatch ----

        [Fact]
        public async Task ColourMatch_of_a_signature_against_itself_is_effectively_one()
        {
            IBuffer image = await TestImages.OpaquePngAsync();
            ArtworkSignature signature = await ArtworkSignature.CreateAsync(image);

            Assert.True(signature.ColourMatch(signature) > 0.99);
        }

        [Fact]
        public async Task ColourMatch_of_a_solid_red_image_against_a_solid_blue_image_is_zero()
        {
            // Red and blue fall in different histogram buckets entirely (see ColourHistogram's bucket
            // math) - a palette match between them should find nothing in common, not a middling score.
            IBuffer red = await TestImages.SolidColorPngAsync(r: 255, g: 0, b: 0);
            IBuffer blue = await TestImages.SolidColorPngAsync(r: 0, g: 0, b: 255);

            ArtworkSignature redSignature = await ArtworkSignature.CreateAsync(red);
            ArtworkSignature blueSignature = await ArtworkSignature.CreateAsync(blue);

            Assert.Equal(0.0, redSignature.ColourMatch(blueSignature));
        }

        // ---- LayoutMatch ----

        [Fact]
        public async Task LayoutMatch_of_a_signature_against_itself_is_effectively_one()
        {
            // Needs real internal contrast (unlike a flat image, see the next test) so the layout
            // vector is not degenerate.
            IBuffer image = await TestImages.PortraitWithDetailBandAsync(width: 32, totalHeight: 64, checkerboardOnTop: true);
            ArtworkSignature signature = await ArtworkSignature.CreateAsync(image);

            Assert.True(signature.LayoutMatch(signature) > 0.99);
        }

        [Fact]
        public async Task LayoutMatch_of_a_flat_image_is_zero_not_undefined()
        {
            // Every pixel identical means zero luma variance. Without LayoutGrid's deviation<=0 guard
            // this divides by zero and produces NaN instead of a comparable score - guarded so a flat
            // capsule or a flat candidate degrades to "no layout signal" rather than poisoning every
            // comparison downstream with NaN.
            IBuffer flat = await TestImages.OpaquePngAsync();
            ArtworkSignature signature = await ArtworkSignature.CreateAsync(flat);

            Assert.Equal(0.0, signature.LayoutMatch(signature));
        }
    }
}
