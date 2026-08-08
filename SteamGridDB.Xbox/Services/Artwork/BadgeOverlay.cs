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
    /// vocabulary works when an uploader says what they did ("Playstation 4", "Xbox One"), and one
    /// uploader's batch of 41 covers in the test library says nothing at all - every notes field is
    /// empty. Nor can the official-artwork gate: a badged cover *is* the real cover, so it scores
    /// ~0.9 against Valve's capsule and the gate approves it. The badge is only visible in pixels.
    ///
    /// Deliberately an exact-overlay test, not a "does this look like a logo" test. The overlay is
    /// composited, not blended, so it is bit-for-bit the same on every upload that carries it
    /// regardless of the art underneath - and that is the whole discriminating signal. Two vaguer
    /// measures were tried against the library first and both failed: contrast-normalised template
    /// correlation recalled 37% (it scored the reported Rayman Legends case 0.57, a miss), and
    /// "corner is a flat colour absent from the middle" recalled nothing useful because most cover
    /// art has a flat corner - clean artwork scored *higher* than badged artwork on it.
    ///
    /// Measured over 858 candidates from the test library: every one of the 50 badged uploads scores
    /// at most 0.9, and the closest clean artwork scores 29.7. Nothing lands in between, which is why
    /// <see cref="badgeDistanceLimit"/> can sit in the middle of a gap that wide and why the check
    /// reports no false positives. The reference was built from half the badged set and the other
    /// half held out; held-out artwork scores at most 0.8, and the test alone found badge uploads
    /// from two uploaders it was never shown.
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
        /// The badge as it actually is, at <see cref="CornerSize"/> square: packed as
        /// index &lt;&lt; 24 | r &lt;&lt; 16 | g &lt;&lt; 8 | b.
        ///
        /// Only the pixels the overlay covers on every sampled upload are here - 90 of the corner's
        /// 256. They were not chosen by hand or by eye: the badged corners were averaged and the
        /// per-pixel spread across *different games* measured, so a pixel the overlay owns is
        /// constant and a pixel showing artwork past it is not. The low-spread pixels are the badge.
        /// That is also what keeps the test honest - a mask drawn by hand around a logo would have
        /// included pixels that only look like part of it.
        /// </summary>
        private static readonly uint[] badgeReference =
        {
            0x041981B0, 0x051780B0, 0x061780B0, 0x071780B0, 0x081780B0, 0x091780B0,
            0x0A1780B0, 0x14157EB0, 0x15127DAE, 0x16147EAF, 0x171780B0, 0x181780B0,
            0x191780B0, 0x1A1780B1, 0x22177FB0, 0x23127CAE, 0x242586B3, 0x254598BE,
            0x26338EB8, 0x27137CAE, 0x281680B0, 0x291780B0, 0x2A1781B1, 0x32117CAE,
            0x334F9EC2, 0x34DBEBF2, 0x35F9FBFC, 0x36CCE2EC, 0x3770B0CC, 0x38137DAD,
            0x391780B1, 0x3A1781B1, 0x401981B0, 0x41157EB0, 0x422285B2, 0x43E0EEF4,
            0x44FFFFFF, 0x45B6D6E3, 0x464A9ABD, 0x484699BE, 0x49127DAF, 0x4A1781B1,
            0x501780B0, 0x51157FB0, 0x522486B3, 0x53A0CBDD, 0x54D2E7F0, 0x564F9EC0,
            0x57C7DFE9, 0x5872B1CE, 0x590F7BAD, 0x5A1780B1, 0x601780B0, 0x611680B0,
            0x621D82B1, 0x635FA6C6, 0x643790B7, 0x66E4F0F5, 0x67FFFFFF, 0x684D9CC0,
            0x69117CAE, 0x701780B0, 0x711780B0, 0x72127DAE, 0x7370B0CC, 0x75F0F6F8,
            0x76FFFFFF, 0x7798C6DA, 0x78167EAE, 0x791680B1, 0x801780B0, 0x811780B0,
            0x821780B0, 0x83127CAE, 0x844296BD, 0x856CAECB, 0x864D9CC0, 0x87167EAE,
            0x88167FB0, 0x901780B0, 0x911780B0, 0x921780B1, 0x93177FB1, 0x94127EAF,
            0x950F7BAE, 0x96117CAE, 0x971680B1, 0xA01780B0, 0xA11780B0, 0xA51781B1,
        };

        /// <summary>
        /// The reference, for tests that need to paint artwork carrying the badge. Packed as
        /// <see cref="badgeReference"/> describes.
        ///
        /// A test that paints from this table and then asserts the check finds it is not proving the
        /// reference is right - only the app's own library measurements can do that, and they are
        /// recorded on this class. What it does pin is the part that can silently be wrong: the
        /// unpacking and the row stride, where the corner is indexed at <see cref="CornerSize"/> but
        /// addressed in a buffer <see cref="ScaledSize"/> wide.
        /// </summary>
        internal static IReadOnlyList<uint> Reference => badgeReference;

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
        /// Mean per-channel difference between this artwork's corner and the reference badge, over
        /// the reference's pixels. Exposed for the tests, which need to see the margin rather than
        /// just which side of it a given image fell.
        /// </summary>
        /// <param name="bgraPixels">BGRA pixels of the whole image at <see cref="ScaledSize"/> square.</param>
        internal static double BadgeDistance(byte[] bgraPixels)
        {
            if (bgraPixels == null || bgraPixels.Length < ScaledSize * ScaledSize * 4)
            {
                return double.MaxValue;
            }

            double total = 0;

            foreach (uint packed in badgeReference)
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

            return total / badgeReference.Length;
        }
    }
}
