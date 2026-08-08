using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SteamGridDB.Xbox.Services.Artwork
{
    /// <summary>
    /// Recognises artwork carrying a storefront badge burned into a corner - a Steam roundel on a
    /// coloured tab, laid over what is otherwise the game's real cover.
    ///
    /// This exists because the notes cannot catch these. <see cref="ArtworkRanker"/>'s console-badge
    /// vocabulary works when an uploader says what they did ("Playstation 4", "Xbox One"), and the
    /// uploads this catches say nothing at all - every notes field is empty. Nor can the
    /// official-artwork gate: a badged cover *is* the real cover, so it scores ~0.9 against Valve's
    /// capsule and the gate approves it. The badge is only visible in pixels.
    ///
    /// Deliberately an exact-overlay test, not a "does this look like a logo" test, and one entry per
    /// *rendering* rather than per badge design. Both of those were learned the hard way:
    ///
    ///   - Contrast-normalised template correlation recalled 37% and scored a known-badged cover
    ///     0.57 - a miss. The badge drowns in whatever art is behind it.
    ///   - "Corner is a flat colour absent from the middle" did not separate at all. Most cover art
    ///     has a flat corner, so clean artwork scored *higher* than badged artwork.
    ///   - Averaging one badge design across the uploaders who use it destroys it. The dark tab is
    ///     drawn at slightly different scales by different people, and a reference fitted across
    ///     them matched any dark corner - one such fit claimed 226 of 861 candidates. Fitted per
    ///     rendering instead, the same images separate by a factor of thirty.
    ///
    /// What makes a single rendering work is that it is composited, not blended: bit-for-bit
    /// identical wherever it is used, whatever is underneath. That is the whole discriminating
    /// signal, and it is why <see cref="badgeDistanceLimit"/> sits in open space rather than on a
    /// boundary. Measured over 861 candidates from the test library:
    ///
    ///   reference        flags   worst flagged   nearest unflagged
    ///   SteamTabLight       50             0.8                29.8
    ///   SteamTabDarkA       10             0.6                30.5
    ///   SteamTabDarkB        2             0.9                24.9
    ///   SteamTabDarkC        2             0.0                23.6
    ///
    /// 64 of 861 flagged in total, all confirmed badged by eye. SteamTabLight was fitted on half its
    /// set with the other half held out, which scores no worse - and it went on to find badge uploads
    /// from two uploaders it had never been shown, which is the only evidence that any of this
    /// generalises past the batch it was fitted on.
    ///
    /// Adding a rendering means measuring one, not tuning this: grow the group from a reported
    /// upload, confirm the members by eye, average them, and keep the pixels whose spread across
    /// different games is lowest. A group whose members do not then sit far below the limit is not
    /// one rendering, and must be split rather than admitted.
    ///
    /// Known gap: only the Steam tab is described. The PlayStation and Xbox spines, the Epic and
    /// Switch roundels and the "PlayStation Hits" banner are all present in the library as their own
    /// renderings and are not yet here.
    /// </summary>
    internal static class BadgeOverlay
    {
        // The whole image is scaled to this, and the badge occupies the top-left corner of it. One
        // scale of the whole image rather than a crop transform, so this uses the decode path
        // TileImage already has.
        internal const uint ScaledSize = 64;

        // The top-left block of that scaled image the reference describes - the quarter the tab sits in.
        internal const int CornerSize = 16;

        // Mean per-channel difference from the reference, over the reference's pixels only. Badged
        // artwork measures under 1 and the nearest clean artwork measures 29.7, so this sits in open
        // space rather than on a boundary - the lesson ARTWORK-SELECTION.md records from the
        // official-artwork gate's thresholds, which did sit on one and had to be widened twice.
        private const double badgeDistanceLimit = 10.0;

        /// <summary>
        /// One entry per known rendering, each packed as
        /// index &lt;&lt; 24 | r &lt;&lt; 16 | g &lt;&lt; 8 | b over a <see cref="CornerSize"/> square.
        ///
        /// Only the pixels a rendering covers on every upload that carries it are listed. They were
        /// not chosen by hand: the group's corners were averaged and the per-pixel spread across
        /// *different games* measured, so a pixel the overlay owns is constant and a pixel showing
        /// artwork past it is not. The low-spread pixels are the badge. A mask drawn by eye around a
        /// logo would have included pixels that merely look like part of it.
        /// </summary>
        private static readonly uint[][] renderings =
        {
            // SteamTabLight - 90 entries
            new uint[]
            {
                0x041980B0, 0x051780B0, 0x061880B0, 0x071880B0, 0x081880B0, 0x091880B0,
                0x0A1880B0, 0x14157EB0, 0x15127DAE, 0x16147EAF, 0x171880B0, 0x181880B0,
                0x191880B0, 0x1A1780B0, 0x221780B0, 0x23127CAE, 0x242586B3, 0x254598BE,
                0x26338EB8, 0x27137CAE, 0x281680B0, 0x291780B0, 0x2A1781B1, 0x32127CAE,
                0x334F9EC2, 0x34DBEBF2, 0x35F9FCFC, 0x36CCE2EC, 0x38147EAE, 0x391780B0,
                0x3A1781B1, 0x401980B0, 0x41167EB0, 0x422285B2, 0x43E1EEF4, 0x44FFFFFF,
                0x464A9ABE, 0x484699BE, 0x49137DAF, 0x4A1781B1, 0x501780B0, 0x51167FB0,
                0x522486B4, 0x54D3E7F0, 0x57C7DFEA, 0x5872B1CE, 0x590F7BAD, 0x5A1780B1,
                0x601880B0, 0x611680B0, 0x621D82B2, 0x635FA6C6, 0x643790B8, 0x66E4F0F6,
                0x67FFFFFF, 0x684D9CC0, 0x69117DAE, 0x6A1780B1, 0x701880B0, 0x711880B0,
                0x72127DAE, 0x7370B0CD, 0x75F0F6F8, 0x76FFFFFF, 0x78167EAE, 0x791680B1,
                0x801880B0, 0x811880B0, 0x821880B0, 0x83127CAE, 0x844296BD, 0x856CAECC,
                0x864D9CC0, 0x87167EAE, 0x88167FB0, 0x901880B0, 0x911880B0, 0x921880B0,
                0x931780B0, 0x94127EAF, 0x950F7BAE, 0x96117DAE, 0x971680B1, 0xA01880B0,
                0xA11780B1, 0xA21781B1, 0xA31781B1, 0xA41780B1, 0xA51781B1, 0xA61780B1,
            },
            // SteamTabDarkA - 90 entries
            new uint[]
            {
                0x000C1B32, 0x010C1C33, 0x020C1D34, 0x030C1D35, 0x040C1E36, 0x050C1F37,
                0x060C2038, 0x070C2039, 0x080C213A, 0x090C223B, 0x0A0D233C, 0x100C1C33,
                0x110C1D34, 0x120C1D35, 0x130C1E36, 0x140C1F37, 0x150C1F38, 0x160C2039,
                0x170C213A, 0x180C223B, 0x190D223C, 0x1A0D233D, 0x200C1D34, 0x210C1D35,
                0x220C1E36, 0x230C1F37, 0x24051932, 0x25061B34, 0x26071C35, 0x27051B35,
                0x280C223C, 0x290D233C, 0x2A0D243E, 0x300C1D35, 0x310C1E36, 0x320C1E37,
                0x33071B34, 0x36B9BFC6, 0x37616F7F, 0x380A203A, 0x390C233D, 0x3A0C253F,
                0x400C1E36, 0x410C1F37, 0x42051932, 0x44FDFEFE, 0x45FFFFFF, 0x47A1AAB4,
                0x485F6F80, 0x49061E39, 0x4A0D2640, 0x500C1F37, 0x510C1F38, 0x52061B35,
                0x53AFB6BE, 0x54FFFFFF, 0x5632455B, 0x58B5BDC5, 0x590D253F, 0x5A0C2640,
                0x600C1F38, 0x610C2039, 0x620A2039, 0x63314358, 0x690C253F, 0x700C2039,
                0x710C213A, 0x72061C36, 0x73637181, 0x74818D9A, 0x76FDFDFD, 0x77FFFFFF,
                0x800C2139, 0x810C223B, 0x820C213B, 0x83112740, 0x84818E9B, 0x880F2942,
                0x900C223B, 0x910D223C, 0x920D233C, 0x930B233C, 0x94061E39, 0x95152D46,
                0x97051F3A, 0xA00D223C, 0xA10D233D, 0xA20D243E, 0xA30D253F, 0xA50B253F,
            },
            // SteamTabDarkB - 90 entries
            new uint[]
            {
                0x00000000, 0x01040A0E, 0x020E2C3E, 0x03123C56, 0x04103A55, 0x050D3954,
                0x060E3A56, 0x07113D58, 0x08113D58, 0x09113D58, 0x0A113E59, 0x0B0C3E5B,
                0x0C0B3F5C, 0x0D0B3F5C, 0x0E0A3F5C, 0x0F0A405D, 0x10040C10, 0x1110344B,
                0x12123E58, 0x130A3651, 0x170A3852, 0x18103C56, 0x19113E59, 0x1A113E59,
                0x200E3043, 0x21113D58, 0x220C3852, 0x2816415C, 0x290E3C58, 0x2A123E59,
                0x30113C57, 0x310A3650, 0x33FCFDFE, 0x34FFFFFF, 0x390C3B56, 0x3A123D59,
                0x40103A55, 0x41164059, 0x42DBE2E5, 0x43FFFFFF, 0x44FFFFFF, 0x46385D74,
                0x4746687D, 0x48CBD5DB, 0x49395F76, 0x4A0E3A56, 0x50103B55, 0x5116405A,
                0x5640647A, 0x58ECF0F2, 0x59527488, 0x5A0C3954, 0x60113C56, 0x61123C58,
                0x68FEFFFF, 0x693F657A, 0x6A0E3A55, 0x70113C57, 0x710C3854, 0x72829AA8,
                0x76FFFFFF, 0x77FFFFFF, 0x7910405C, 0x7A213E56, 0x80113D58, 0x81103C56,
                0x8218425D, 0x84DDE4E8, 0x85FFFFFF, 0x86FBFCFC, 0x89043C5A, 0x90113D58,
                0x91113D58, 0x920E3C58, 0x930C3C57, 0x9710415C, 0x98043C5A, 0xA0113D58,
                0xA1123D58, 0xA2123C57, 0xA3123C56, 0xA40E3954, 0xA50E3752, 0xA6143A54,
                0xB00E3F5A, 0xC00C3F5C, 0xC7B43C28, 0xD00C3F5C, 0xE00E3F5C, 0xF00E3F5C,
            },
            // SteamTabDarkC - 97 entries
            new uint[]
            {
                0x00000000, 0x01030407, 0x020C1420, 0x03101C2D, 0x040A1728, 0x05071426,
                0x06071528, 0x070C1A2D, 0x08122033, 0x09122134, 0x0A132135, 0x10030508,
                0x110E1927, 0x120F1C2E, 0x130E1A2C, 0x14495360, 0x157F8690, 0x167C838D,
                0x17424D5C, 0x180D1C30, 0x1A132237, 0x200D1724, 0x21101D2E, 0x22131F30,
                0x239FA4AB, 0x24FDFDFD, 0x25FFFFFF, 0x26FFFFFF, 0x27E8E9EB, 0x2889909A,
                0x2A122337, 0x30111D2F, 0x31091527, 0x327A818B, 0x33FFFFFF, 0x34FFFFFF,
                0x35FCFCFD, 0x36818892, 0x374F5A68, 0x38C1C5C9, 0x40101C2D, 0x41142032,
                0x42DADBDE, 0x43FFFFFF, 0x44FFFFFF, 0x45B3B7BE, 0x46384353, 0x471C2A3D,
                0x489399A2, 0x4A0E1F35, 0x50101D2F, 0x51142133, 0x527B838D, 0x53CBCED2,
                0x54D7D9DC, 0x55273547, 0x5619273B, 0x5779818C, 0x58E0E2E4, 0x60111F31,
                0x61111E31, 0x621A2739, 0x6319273A, 0x641C2A3D, 0x65334050, 0x66A3A9B1,
                0x67FEFEFE, 0x68FFFFFF, 0x6A0C1F36, 0x70121F32, 0x710C1B2E, 0x72515C6A,
                0x73CDD0D4, 0x7479818B, 0x75BEC2C7, 0x76FFFFFF, 0x77FFFFFF, 0x78F4F4F6,
                0x80122033, 0x81122034, 0x820F1E32, 0x83717A85, 0x84E7E9EB, 0x85FFFFFF,
                0x86FEFEFE, 0x87D7DADD, 0x88566373, 0x90122134, 0x92132236, 0x930C1C31,
                0x96465364, 0xA0132235, 0xA1132237, 0xA3132439, 0xA4112238, 0xA50D1F36,
                0xA60E2137,
            },
        };

        /// <summary>
        /// The renderings, for tests that need to paint artwork carrying one. Packed as
        /// <see cref="renderings"/> describes.
        ///
        /// A test that paints from this table and then asserts the check finds it is not proving the
        /// reference is right - only the app's own library measurements can do that, and they are
        /// recorded on this class. What it does pin is the part that can silently be wrong: the
        /// unpacking and the row stride, where the corner is indexed at <see cref="CornerSize"/> but
        /// addressed in a buffer <see cref="ScaledSize"/> wide.
        /// </summary>
        internal static IReadOnlyList<uint[]> Renderings => renderings;

        /// <summary>
        /// Whether this artwork carries a storefront badge burned into its corner.
        ///
        /// Undecodable artwork returns false. A tile that cannot be read is the download's problem to
        /// report, and answering "yes, badged" for it would quietly drop a candidate for a reason
        /// that has nothing to do with badges.
        /// </summary>
        /// <param name="imageBytes">Encoded artwork.</param>
        internal static async Task<bool> CarriesBadgeAsync(IBuffer imageBytes)
        {
            return await TileImage.WithDecoderAsync(
                imageBytes,
                async decoder => BadgeDistance(
                    await TileImage.ScaledPixelsAsync(
                        decoder, null, ScaledSize, ScaledSize, BitmapAlphaMode.Ignore))
                    <= badgeDistanceLimit,
                false,
                "Error inspecting artwork for a storefront badge");
        }

        /// <summary>
        /// How far this artwork's corner is from the nearest known rendering - the mean per-channel
        /// difference, over that rendering's own pixels. Exposed for the tests, which need to see the
        /// margin rather than just which side of it a given image fell.
        ///
        /// Nearest rather than any-within-the-limit because the renderings do not compete: an image
        /// carrying one is far from all the others, so the minimum is the only one that can be small.
        /// </summary>
        /// <param name="bgraPixels">BGRA pixels of the whole image at <see cref="ScaledSize"/> square.</param>
        internal static double BadgeDistance(byte[] bgraPixels)
        {
            if (bgraPixels == null || bgraPixels.Length < ScaledSize * ScaledSize * 4)
            {
                return double.MaxValue;
            }

            double nearest = double.MaxValue;

            foreach (uint[] rendering in renderings)
            {
                nearest = Math.Min(nearest, DistanceTo(bgraPixels, rendering));
            }

            return nearest;
        }

        /// <summary>
        /// Mean per-channel difference between the artwork's corner and one rendering.
        /// </summary>
        /// <param name="bgraPixels">BGRA pixels of the whole image at <see cref="ScaledSize"/> square.</param>
        /// <param name="rendering">One entry of <see cref="renderings"/>.</param>
        private static double DistanceTo(byte[] bgraPixels, uint[] rendering)
        {
            double total = 0;

            foreach (uint packed in rendering)
            {
                int index = (int)(packed >> 24);
                int x = index % CornerSize;
                int y = index / CornerSize;

                // BGRA, and the corner is the top-left of the full scaled image, so the row stride is
                // the scaled width rather than the corner's
                int offset = (((y * (int)ScaledSize) + x) * 4);

                int difference = Math.Abs(bgraPixels[offset + 2] - (int)((packed >> 16) & 0xFF))
                    + Math.Abs(bgraPixels[offset + 1] - (int)((packed >> 8) & 0xFF))
                    + Math.Abs(bgraPixels[offset] - (int)(packed & 0xFF));

                total += difference / 3.0;
            }

            return total / rendering.Length;
        }
    }
}
