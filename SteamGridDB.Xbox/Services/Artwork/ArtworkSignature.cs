using System;
using System.Linq;
using System.Threading.Tasks;

using Windows.Storage.Streams;

namespace SteamGridDB.Xbox.Services.Artwork
{
    /// <summary>
    /// Compact description of an image, used to compare artwork against a game's official store
    /// capsule. Both measures work on the centre square so a 600x900 capsule and a 1024x1024 grid
    /// compare directly, and both are cheap enough to run on a handful of candidates per game.
    /// </summary>
    internal sealed class ArtworkSignature
    {
        // 4x4x4 RGB histogram: "is this the same palette". Coarse on purpose - it has to survive
        // recompression, crops and overlaid logos.
        private const int colourGridSize = 32;
        private const int colourBuckets = 64;

        // Contrast-normalised greyscale grid: "is this the same picture". A palette match with no
        // layout match is a coincidence, which is the failure the colour histogram alone cannot see.
        private const int layoutGridSize = 12;

        private readonly double[] colour;
        private readonly double[] layout;

        private ArtworkSignature(double[] colour, double[] layout)
        {
            this.colour = colour;
            this.layout = layout;
        }

        /// <summary>
        /// Builds a signature, or returns null when the image cannot be decoded.
        /// </summary>
        /// <param name="imageBytes">Encoded image, or null.</param>
        public static async Task<ArtworkSignature> CreateAsync(IBuffer imageBytes)
        {
            return await TileImage.WithDecoderAsync<ArtworkSignature>(imageBytes, async decoder =>
            {
                double[] histogram = ColourHistogram(
                    await TileImage.CentreSquarePixelsAsync(decoder, colourGridSize));

                double[] grid = LayoutGrid(
                    await TileImage.CentreSquarePixelsAsync(decoder, layoutGridSize));

                return new ArtworkSignature(histogram, grid);
            }, null, "Could not build artwork signature");
        }

        private static double[] ColourHistogram(byte[] pixels)
        {
            var histogram = new double[colourBuckets];

            for (int i = 0; i + 3 < pixels.Length; i += 4)
            {
                // BGRA in memory order
                int bucket = ((pixels[i + 2] / 64) * 16) + ((pixels[i + 1] / 64) * 4) + (pixels[i] / 64);

                histogram[bucket]++;
            }

            double magnitude = Math.Sqrt(histogram.Sum(v => v * v));

            if (magnitude > 0)
            {
                for (int i = 0; i < histogram.Length; i++)
                {
                    histogram[i] /= magnitude;
                }
            }

            return histogram;
        }

        private static double[] LayoutGrid(byte[] pixels)
        {
            double[] luma = TileImage.Luma(pixels);

            // Normalise out brightness and contrast so only the arrangement of light and dark counts
            double mean = luma.Average();
            double deviation = Math.Sqrt(luma.Sum(v => (v - mean) * (v - mean)) / luma.Length);

            if (deviation <= 0)
            {
                deviation = 1;
            }

            for (int i = 0; i < luma.Length; i++)
            {
                luma[i] = (luma[i] - mean) / deviation;
            }

            return luma;
        }

        /// <summary>
        /// Palette agreement, 0 (nothing in common) to 1 (identical distribution).
        /// </summary>
        public double ColourMatch(ArtworkSignature other)
        {
            return colour.Zip(other.colour, (a, b) => a * b).Sum();
        }

        /// <summary>
        /// Agreement on where the light and dark areas sit, -1 (inverted) to 1 (identical).
        /// </summary>
        public double LayoutMatch(ArtworkSignature other)
        {
            return layout.Zip(other.layout, (a, b) => a * b).Sum() / layout.Length;
        }
    }
}
