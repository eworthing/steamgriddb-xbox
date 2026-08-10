using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;

using Windows.Storage.Streams;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The storefront-badge check (see <see cref="BadgeOverlay"/>) - whether artwork carries a
    /// platform roundel burned into its corner over the game's real cover.
    ///
    /// What these can and cannot prove is worth being clear about. Whether the reference *is* the
    /// badge, and whether the limit separates badged artwork from clean artwork, are facts about
    /// SteamGridDB's catalogue that only a measurement over real candidates establishes; those
    /// numbers are recorded on <see cref="BadgeOverlay"/> itself. What is testable here is everything
    /// that can silently break afterwards: the packing and stride arithmetic, the decode path, and
    /// the behaviour on artwork that is not badged or not readable at all.
    /// </summary>
    public class BadgeOverlayTests
    {
        // ---- BadgeDistance ----

        public static TheoryData<int> EveryRendering()
        {
            var data = new TheoryData<int>();

            for (int i = 0; i < BadgeOverlay.Renderings.Count; i++)
            {
                data.Add(i);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(EveryRendering))]
        public async Task Artwork_painted_with_any_known_rendering_is_flagged(int rendering)
        {
            // Pins the unpacking and the stride together: a rendering indexes a 16-wide corner but
            // addresses a 64-wide buffer, so reading a row at the wrong pitch lands on artwork - and
            // the painted background is deliberately noisy, so that would show up as a large distance
            // rather than passing on a flat neighbour. Run over every rendering so a table added later
            // with a malformed index cannot go unnoticed.
            IBuffer badged = await TestImages.BadgedPngAsync(rendering: rendering);

            Assert.True(await BadgeOverlay.CarriesBadgeAsync(badged));
        }

        [Fact]
        public void Every_rendering_is_described_and_indexable()
        {
            // A table is only meaningful if its indices address the corner it claims to describe, and
            // a rendering with too few pixels would measure noise.
            Assert.NotEmpty(BadgeOverlay.Renderings);

            foreach (uint[] rendering in BadgeOverlay.Renderings)
            {
                Assert.True(rendering.Length >= 32, "a rendering needs enough pixels to be a badge");
                Assert.All(rendering, packed =>
                    Assert.InRange((int)(packed >> 24), 0, (BadgeOverlay.CornerSize * BadgeOverlay.CornerSize) - 1));
            }
        }

        [Fact]
        public async Task The_same_artwork_without_the_badge_is_not_flagged()
        {
            // Same painter, same size, same background - the badge is the only difference, so this
            // isolates the check from everything else about the image.
            IBuffer clean = await TestImages.BadgedPngAsync(badged: false);

            Assert.False(await BadgeOverlay.CarriesBadgeAsync(clean));
        }

        [Fact]
        public void A_buffer_smaller_than_one_scaled_image_is_not_read()
        {
            // Reading past the end would be an access violation rather than a wrong answer, so this
            // guards the bound rather than the measure.
            Assert.Equal(double.MaxValue, BadgeOverlay.BadgeDistance(new byte[16]));
            Assert.Equal(double.MaxValue, BadgeOverlay.BadgeDistance(null));
        }

        [Fact]
        public void Distance_is_the_nearest_rendering_measured_per_channel()
        {
            // A buffer that is black everywhere sits a known distance from each rendering: the mean of
            // that rendering's own channel values, since every difference is the reference value
            // itself. The answer must be the smallest of those, not the first or the sum.
            var black = new byte[BadgeOverlay.ScaledSize * BadgeOverlay.ScaledSize * 4];

            double nearest = double.MaxValue;

            foreach (uint[] rendering in BadgeOverlay.Renderings)
            {
                double total = 0;

                foreach (uint packed in rendering)
                {
                    total += (((packed >> 16) & 0xFF) + ((packed >> 8) & 0xFF) + (packed & 0xFF)) / 3.0;
                }

                nearest = System.Math.Min(nearest, total / rendering.Length);
            }

            Assert.Equal(nearest, BadgeOverlay.BadgeDistance(black), 6);
        }

        // ---- CarriesBadgeAsync ----

        [Fact]
        public async Task Artwork_that_cannot_be_decoded_is_not_called_badged()
        {
            // The download reports an unreadable image its own way. Answering "badged" here would drop
            // the candidate for a reason that has nothing to do with badges.
            Assert.False(await BadgeOverlay.CarriesBadgeAsync(TestImages.Bytes("this is not an image")));

            // Cast needed only because the new IBitmapFrame overload (see BadgeOverlay's frame-taking
            // CarriesBadgeAsync) makes a bare null ambiguous between it and this IBuffer overload - both
            // are unrelated reference types a null literal converts to equally well. Still the same
            // IBuffer overload, same assertion; nothing about what this pins has changed.
            Assert.False(await BadgeOverlay.CarriesBadgeAsync((IBuffer)null));
        }

        [Fact]
        public async Task Ordinary_artwork_is_not_flagged()
        {
            Assert.False(await BadgeOverlay.CarriesBadgeAsync(await TestImages.OpaquePngAsync()));
            Assert.False(await BadgeOverlay.CarriesBadgeAsync(
                await TestImages.SolidColorPngAsync(r: 23, g: 128, b: 176)));
        }

        [Fact]
        public async Task A_flat_image_of_the_badge_s_own_blue_is_not_flagged()
        {
            // The nearest thing to a false positive the measure could have: the badge's dominant
            // colour, filling the corner, with no logo punched out of it. The reference carries the
            // white roundel as well as the blue, so a flat tab is not a badge.
            Assert.False(await BadgeOverlay.CarriesBadgeAsync(
                await TestImages.SolidColorPngAsync(r: 0x17, g: 0x80, b: 0xB0, width: 64, height: 64)));
        }

        // ---- CarriesBadgeAsync(IBitmapFrame) ----

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Frame_taking_overload_agrees_with_the_buffer_taking_one(bool badged)
        {
            // The buffer-taking entry point is now a thin wrapper opening one decoder and handing it to
            // this overload (see ArtworkDownloader, which shares that same open decoder with
            // TileImage.FillsTileAsync). Pinned here so the two paths cannot silently drift apart.
            IBuffer image = await TestImages.BadgedPngAsync(badged: badged);

            bool viaFrame = await TileImage.WithDecoderAsync(
                image, decoder => BadgeOverlay.CarriesBadgeAsync(decoder), false, "decode failed");

            Assert.Equal(await BadgeOverlay.CarriesBadgeAsync(image), viaFrame);
        }
    }
}
