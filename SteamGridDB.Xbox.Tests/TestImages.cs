using System;
using System.Collections.Generic;
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

        /// <summary>
        /// A real multi-frame .ico, each frame a solid-colour PNG. Frames go in the order given -
        /// pass them smallest first, because that is the order real icons use and the reason reading
        /// "the" frame of one used to mean reading its worst.
        /// </summary>
        internal static async Task<IBuffer> IcoAsync(params (int Size, byte R, byte G, byte B)[] frames)
        {
            var payloads = new List<byte[]>();

            foreach ((int Size, byte R, byte G, byte B) frame in frames)
            {
                payloads.Add(ToArray(await SolidColorPngAsync(frame.R, frame.G, frame.B, frame.Size, frame.Size)));
            }

            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            {
                writer.Write((ushort)0);              // reserved
                writer.Write((ushort)1);              // type: icon
                writer.Write((ushort)frames.Length);

                int offset = 6 + (16 * frames.Length);

                for (int i = 0; i < frames.Length; i++)
                {
                    // A directory dimension of 0 means 256, the largest the format can name
                    writer.Write((byte)(frames[i].Size >= 256 ? 0 : frames[i].Size));
                    writer.Write((byte)(frames[i].Size >= 256 ? 0 : frames[i].Size));
                    writer.Write((byte)0);            // palette
                    writer.Write((byte)0);            // reserved
                    writer.Write((ushort)1);          // colour planes
                    writer.Write((ushort)32);         // bits per pixel
                    writer.Write(payloads[i].Length);
                    writer.Write(offset);

                    offset += payloads[i].Length;
                }

                foreach (byte[] payload in payloads)
                {
                    writer.Write(payload);
                }

                writer.Flush();

                return CryptographicBuffer.CreateFromByteArray(stream.ToArray());
            }
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
        /// A PNG carrying the storefront badge <see cref="BadgeOverlay"/> looks for, painted from that
        /// class's own reference over an otherwise plain image.
        ///
        /// Sized to <see cref="BadgeOverlay.ScaledSize"/> exactly so the decode does no scaling: a
        /// resample would blur the badge's hard edges and the test would be measuring the interpolator
        /// rather than the check. Real artwork is 512 or 1024 square and does get scaled, which is why
        /// the limit sits far from the badge's own distance rather than beside it.
        /// </summary>
        /// <param name="badged">Whether to paint the badge at all; false gives the same image without it.</param>
        /// <param name="rendering">Which of <see cref="BadgeOverlay.Renderings"/> to paint.</param>
        internal static Task<IBuffer> BadgedPngAsync(bool badged = true, int rendering = 0)
        {
            int size = (int)BadgeOverlay.ScaledSize;
            var badge = new Dictionary<int, (byte R, byte G, byte B)>();

            foreach (uint packed in BadgeOverlay.Renderings[rendering])
            {
                badge[(int)(packed >> 24)] = (
                    R: (byte)((packed >> 16) & 0xFF),
                    G: (byte)((packed >> 8) & 0xFF),
                    B: (byte)(packed & 0xFF));
            }

            return FromPixelsAsync(size, size, (x, y) =>
            {
                if (badged && x < BadgeOverlay.CornerSize && y < BadgeOverlay.CornerSize
                    && badge.TryGetValue((y * BadgeOverlay.CornerSize) + x, out var colour))
                {
                    return (B: colour.B, G: colour.G, R: colour.R, A: (byte)255);
                }

                // Deliberately not flat - artwork the badge sits on is never flat, and a flat
                // background would let an indexing mistake read a neighbouring pixel and still pass
                byte level = (byte)(((x * 7) + (y * 13)) % 256);

                return (B: level, G: (byte)(255 - level), R: (byte)((level + 128) % 256), A: (byte)255);
            });
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
