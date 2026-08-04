using System;
using System.Text;
using System.Threading.Tasks;

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
    }
}
