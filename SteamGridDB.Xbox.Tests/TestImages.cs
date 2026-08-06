using System;
using System.Text;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;

using Windows.Graphics.Imaging;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Image and byte payloads for tests.
    ///
    /// The images are encoded by the platform rather than pasted in as base64 blobs, so a test that
    /// says "this is a JPEG" is asserting against something Windows itself considers a JPEG - which is
    /// the same decoder the app hands its downloads to.
    /// </summary>
    internal static class TestImages
    {
        /// <summary>A real PNG.</summary>
        internal static Task<IBuffer> PngAsync(int width = 8, int height = 8)
        {
            return EncodeAsync(BitmapEncoder.PngEncoderId, BitmapAlphaMode.Premultiplied, width, height);
        }

        /// <summary>A real JPEG - not a PNG, so it must be re-encoded before it can be written as a tile.</summary>
        internal static Task<IBuffer> JpegAsync(int width = 8, int height = 8)
        {
            return EncodeAsync(BitmapEncoder.JpegEncoderId, BitmapAlphaMode.Ignore, width, height);
        }

        /// <summary>Bytes that are not an image at all, for the paths that have to cope with junk.</summary>
        internal static IBuffer Bytes(string content)
        {
            return CryptographicBuffer.CreateFromByteArray(Encoding.UTF8.GetBytes(content));
        }

        /// <summary>A buffer's contents, for comparing what was written against what was handed in.</summary>
        internal static byte[] ToArray(IBuffer buffer)
        {
            CryptographicBuffer.CopyToByteArray(buffer, out byte[] bytes);

            return bytes;
        }

        /// <summary>Whether the bytes carry the PNG signature.</summary>
        internal static bool IsPng(byte[] bytes)
        {
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

            if (bytes == null || bytes.Length < signature.Length)
            {
                return false;
            }

            for (int i = 0; i < signature.Length; i++)
            {
                if (bytes[i] != signature[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static async Task<IBuffer> EncodeAsync(Guid encoderId, BitmapAlphaMode alpha, int width, int height)
        {
            using (var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, alpha))
            using (var stream = new InMemoryRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(encoderId, stream);

                encoder.SetSoftwareBitmap(bitmap);

                await encoder.FlushAsync();

                var buffer = new Windows.Storage.Streams.Buffer((uint)stream.Size);

                stream.Seek(0);

                return await stream.ReadAsync(buffer, (uint)stream.Size, InputStreamOptions.None);
            }
        }

        /// <summary>A fully opaque PNG - every pixel's alpha is 255, so no corner reads as transparent.</summary>
        internal static Task<IBuffer> OpaquePngAsync(int width = 32, int height = 32)
        {
            return FromPixelsAsync(width, height, (x, y) => (B: (byte)200, G: (byte)200, R: (byte)200, A: (byte)255));
        }

        /// <summary>
        /// A single-colour opaque PNG, for signature tests that need two images with no palette overlap.
        /// </summary>
        internal static Task<IBuffer> SolidColorPngAsync(byte r, byte g, byte b, int width = 32, int height = 32)
        {
            return FromPixelsAsync(width, height, (x, y) => (B: b, G: g, R: r, A: (byte)255));
        }

        /// <summary>
        /// A PNG whose four corners are fully transparent and whose centre is opaque - the shape a
        /// rounded icon or a physical-media mockup produces, which FillsTileAsync exists to reject.
        /// </summary>
        internal static Task<IBuffer> PngWithTransparentCornersAsync(int width = 32, int height = 32, int cornerSize = 6)
        {
            return FromPixelsAsync(width, height, (x, y) =>
            {
                bool inCorner = (x < cornerSize || x >= width - cornerSize) && (y < cornerSize || y >= height - cornerSize);

                return (B: (byte)200, G: (byte)200, R: (byte)200, A: inCorner ? (byte)0 : (byte)255);
            });
        }

        /// <summary>
        /// A portrait PNG with one flat, featureless band (grey, no edges) and one high-contrast
        /// checkerboard band (alternating black/white, dense edges) at the requested end - for testing
        /// that the crop window is drawn toward the band with detail, wherever it sits.
        /// </summary>
        /// <param name="width">Image width; also the height of the checkerboard band, so the band is
        /// exactly one crop-window tall.</param>
        /// <param name="totalHeight">Full image height. Must exceed <paramref name="width"/>.</param>
        /// <param name="checkerboardOnTop">Whether the checkerboard band is the first <paramref
        /// name="width"/> rows (true) or the last (false).</param>
        internal static Task<IBuffer> PortraitWithDetailBandAsync(int width, int totalHeight, bool checkerboardOnTop)
        {
            return FromPixelsAsync(width, totalHeight, (x, y) =>
            {
                bool inBand = checkerboardOnTop ? y < width : y >= totalHeight - width;
                byte level = inBand && ((x + y) % 2 == 0) ? (byte)0 : (byte)255;
                byte flat = 128;

                return inBand
                    ? (B: level, G: level, R: level, A: (byte)255)
                    : (B: flat, G: flat, R: flat, A: (byte)255);
            });
        }

        /// <summary>
        /// A square image split into four flat quadrants, light and dark on opposite corners.
        ///
        /// Scale-invariant on purpose: the same picture rendered at 72px and at 329px reduces to the
        /// same signature, which is exactly the property tile matching relies on - a reference fetched
        /// at one size has to match cached renditions at half a dozen others. A fine texture such as a
        /// one-pixel checkerboard does not have it: it averages away to flat grey at high resolution
        /// and survives at low, so the "same" image scores as two different ones.
        /// </summary>
        /// <param name="size">Width and height.</param>
        /// <param name="inverted">Swaps light for dark, giving a different picture built from the same palette.</param>
        internal static Task<IBuffer> QuadrantPngAsync(int size, bool inverted = false)
        {
            byte dark = inverted ? (byte)235 : (byte)20;
            byte light = inverted ? (byte)20 : (byte)235;

            return FromPixelsAsync(size, size, (x, y) =>
            {
                bool topLeftOrBottomRight = (x < size / 2) == (y < size / 2);
                byte level = topLeftOrBottomRight ? dark : light;

                return (B: level, G: level, R: level, A: (byte)255);
            });
        }

        /// <summary>Builds a PNG from an explicit per-pixel BGRA function, bypassing the encoder's own
        /// (opaque-black or fully-transparent) default fill so tests can control alpha and colour exactly.</summary>
        private static async Task<IBuffer> FromPixelsAsync(int width, int height, Func<int, int, (byte B, byte G, byte R, byte A)> pixelAt)
        {
            var pixels = new byte[width * height * 4];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    (byte B, byte G, byte R, byte A) = pixelAt(x, y);
                    int i = ((y * width) + x) * 4;

                    pixels[i] = B;
                    pixels[i + 1] = G;
                    pixels[i + 2] = R;
                    pixels[i + 3] = A;
                }
            }

            IBuffer pixelBuffer = CryptographicBuffer.CreateFromByteArray(pixels);

            using (SoftwareBitmap bitmap = SoftwareBitmap.CreateCopyFromBuffer(pixelBuffer, BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Straight))
            {
                return await TileImage.EncodePngAsync(bitmap);
            }
        }
    }
}
