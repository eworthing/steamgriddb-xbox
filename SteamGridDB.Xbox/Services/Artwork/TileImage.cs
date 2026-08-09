using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SteamGridDB.Xbox.Services.Artwork
{
    /// <summary>
    /// Image work needed to turn a downloaded artwork into a tile: reading pixels, judging whether an
    /// image suits a square tile, cropping portrait box art to one, and making the bytes match the .png
    /// name the Xbox app owns.
    ///
    /// Separate from the widget because none of it touches the page, its dispatcher or its state, and
    /// because the platform decode/encode boilerplate is easy to get subtly inconsistent when it is
    /// spread across a UI file.
    /// </summary>
    internal static class TileImage
    {
        // The image is reduced to this many columns before its rows are measured for detail. Small
        // enough that a full-size grid costs nothing to profile, large enough to see a title.
        private const int cropProfileWidth = 64;

        // Quality first-party tiles are re-encoded at. The Store's own cached renditions land between
        // 20KB and 45KB at these sizes; this matches them, and the artefacts of anything lower show
        // plainly on a tile that is only a few hundred pixels across.
        private const double tileJpegQuality = 0.92;

        /// <summary>
        /// Qualities a tile is encoded at, best first, when it has to fit a byte budget.
        ///
        /// The first is <see cref="tileJpegQuality"/>, so a tile under no pressure is encoded exactly as
        /// it always was. The rest are only reached by a tile that has to fit the space a Store download
        /// left behind it, and stop well above the point where artefacts are obvious - a tile that will
        /// not fit at 0.45 is one that should be reported rather than smeared until it does.
        /// </summary>
        private static readonly double[] tileJpegQualitySteps = { tileJpegQuality, 0.85, 0.75, 0.65, 0.55, 0.45 };

        /// <summary>
        /// Decodes an image and hands the decoder to <paramref name="read"/>, returning
        /// <paramref name="onError"/> if it cannot be decoded or the read throws.
        /// </summary>
        /// <param name="imageBytes">Encoded image, or null.</param>
        /// <param name="read">Work to do with the decoder, before its backing stream closes.</param>
        /// <param name="onError">Value to return when the image cannot be read.</param>
        /// <param name="describeFailure">Prefix for the debug message on failure.</param>
        public static async Task<T> WithDecoderAsync<T>(IBuffer imageBytes, Func<BitmapDecoder, Task<T>> read, T onError, string describeFailure)
        {
            if (imageBytes == null)
            {
                return onError;
            }

            try
            {
                using (var stream = new InMemoryRandomAccessStream())
                {
                    await stream.WriteAsync(imageBytes);
                    stream.Seek(0);

                    return await read(await BitmapDecoder.CreateAsync(stream));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"{describeFailure}: {ex.Message}");

                return onError;
            }
        }

        /// <summary>
        /// The frame worth reading pixels from - the largest, which for everything but an .ico is the
        /// only one.
        ///
        /// BitmapDecoder reads a file's first frame unless told otherwise, and .ico files list theirs
        /// smallest first - so a seven-frame icon was being read at its 16px frame and upscaled to a
        /// 329px tile, a mush of squares, while a crisp 256px frame sat unread in the same file.
        /// About half of all icon artwork is .ico, so this is the routine case for icons rather than
        /// an oddity. Single-frame images return the decoder itself and cost nothing.
        /// </summary>
        /// <param name="decoder">Decoder for the image.</param>
        public static async Task<IBitmapFrame> LargestFrameAsync(BitmapDecoder decoder)
        {
            IBitmapFrame largest = decoder;

            for (uint i = 1; i < decoder.FrameCount; i++)
            {
                BitmapFrame frame = await decoder.GetFrameAsync(i);

                if (frame.PixelWidth > largest.PixelWidth)
                {
                    largest = frame;
                }
            }

            return largest;
        }

        /// <summary>
        /// Reads an image as BGRA at the requested size, optionally from a sub-rectangle.
        /// </summary>
        /// <param name="frame">The image frame to read - a BitmapDecoder is its own first frame.</param>
        /// <param name="bounds">Region to read, or null for the whole image.</param>
        /// <param name="width">Width to scale to.</param>
        /// <param name="height">Height to scale to.</param>
        /// <param name="alphaMode">How to treat the alpha channel.</param>
        public static async Task<byte[]> ScaledPixelsAsync(IBitmapFrame frame, BitmapBounds? bounds, uint width, uint height, BitmapAlphaMode alphaMode)
        {
            var transform = new BitmapTransform
            {
                ScaledWidth = width,
                ScaledHeight = height,
                InterpolationMode = BitmapInterpolationMode.Fant
            };

            if (bounds.HasValue)
            {
                transform.Bounds = bounds.Value;
            }

            PixelDataProvider data = await frame.GetPixelDataAsync(
                BitmapPixelFormat.Bgra8,
                alphaMode,
                transform,
                ExifOrientationMode.IgnoreExifOrientation,
                ColorManagementMode.DoNotColorManage);

            return data.DetachPixelData();
        }

        /// <summary>
        /// Reads the centre square of an image as a <paramref name="size"/> x <paramref name="size"/>
        /// BGRA buffer, so that images of different aspect ratios can be compared directly.
        ///
        /// The crop is done here rather than through BitmapTransform.Bounds because that is applied
        /// *after* scaling, so bounds have to be given in the scaled image's coordinates. Passing
        /// full-resolution bounds alongside a scale silently throws, which is how the official-artwork
        /// gate came to fail on every game while looking like it was simply declining to act.
        /// </summary>
        /// <param name="frame">The image frame to read - a BitmapDecoder is its own first frame.</param>
        /// <param name="size">Width and height of the square to return.</param>
        public static async Task<byte[]> CentreSquarePixelsAsync(IBitmapFrame frame, uint size)
        {
            // Scale the whole image so its short side is exactly the square we want, then take the
            // middle out of the result. No dependency on the order the platform applies transforms.
            double scale = (double)size / Math.Min(frame.PixelWidth, frame.PixelHeight);
            uint scaledWidth = Math.Max(size, (uint)Math.Round(frame.PixelWidth * scale));
            uint scaledHeight = Math.Max(size, (uint)Math.Round(frame.PixelHeight * scale));

            byte[] scaled = await ScaledPixelsAsync(frame, null, scaledWidth, scaledHeight, BitmapAlphaMode.Ignore);

            uint left = (scaledWidth - size) / 2;
            uint top = (scaledHeight - size) / 2;
            var square = new byte[size * size * 4];

            for (uint y = 0; y < size; y++)
            {
                uint source = (((top + y) * scaledWidth) + left) * 4;

                Array.Copy(scaled, source, square, y * size * 4, size * 4);
            }

            return square;
        }

        /// <summary>
        /// Perceived brightness of each pixel of a BGRA buffer.
        /// </summary>
        public static double[] Luma(byte[] bgra)
        {
            var luma = new double[bgra.Length / 4];

            for (int i = 0; i < luma.Length; i++)
            {
                int p = i * 4;

                luma[i] = (0.114 * bgra[p]) + (0.587 * bgra[p + 1]) + (0.299 * bgra[p + 2]);
            }

            return luma;
        }

        /// <summary>
        /// Encodes a bitmap as PNG.
        /// </summary>
        public static async Task<IBuffer> EncodePngAsync(SoftwareBitmap bitmap)
        {
            using (var target = new InMemoryRandomAccessStream())
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, target);

                encoder.SetSoftwareBitmap(bitmap);

                await encoder.FlushAsync();
                target.Seek(0);

                var encoded = new Windows.Storage.Streams.Buffer((uint)target.Size);

                await target.ReadAsync(encoded, (uint)target.Size, InputStreamOptions.None);

                return encoded;
            }
        }

        /// <summary>
        /// Encodes a bitmap as JPEG.
        /// </summary>
        /// <param name="bitmap">Bitmap to encode.</param>
        /// <param name="quality">Encoder quality, 0 to 1.</param>
        public static async Task<IBuffer> EncodeJpegAsync(SoftwareBitmap bitmap, double quality)
        {
            using (var target = new InMemoryRandomAccessStream())
            {
                var options = new BitmapPropertySet
                {
                    { "ImageQuality", new BitmapTypedValue(quality, Windows.Foundation.PropertyType.Single) }
                };

                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, target, options);

                encoder.SetSoftwareBitmap(bitmap);

                await encoder.FlushAsync();
                target.Seek(0);

                var encoded = new Windows.Storage.Streams.Buffer((uint)target.Size);

                await target.ReadAsync(encoded, (uint)target.Size, InputStreamOptions.None);

                return encoded;
            }
        }

        /// <summary>
        /// Re-encodes artwork as a JPEG of exactly <paramref name="pixels"/> square.
        ///
        /// This is what a first-party tile has to be. The Xbox app renders those from its own image
        /// cache, whose files it downloaded from the Store's CDN at a fixed size each - and it keeps no
        /// record of what it downloaded beyond the file itself, so a replacement is only ever noticed if
        /// it does not decode to the size the layout expects. JPEG rather than PNG for the same reason:
        /// match what the app put there rather than rely on the decoder sniffing content.
        ///
        /// Artwork that is not already square is centre-cropped, so a tall SteamGridDB grid gives its
        /// middle to the tile instead of being squashed.
        ///
        /// A byte budget can be imposed, and for a first-party tile there always is one. Those files
        /// are written without being resized - the Xbox app keeps them memory-mapped while it is showing
        /// them, and a mapped file can have its contents changed but not its length - so a tile that
        /// does not fit the space the Store's own download left cannot be written at all. Quality is
        /// stepped down until it fits rather than the tile being refused at the first attempt, because
        /// a slightly softer tile is worth incomparably more than no tile.
        /// </summary>
        /// <param name="imageBytes">Encoded artwork in any format the platform can decode.</param>
        /// <param name="pixels">Width and height the result must have.</param>
        /// <param name="maxBytes">Largest the result may be, or 0 for no limit.</param>
        /// <returns>
        /// JPEG bytes, or null when the artwork cannot be decoded or will not fit
        /// <paramref name="maxBytes"/> at any quality this is willing to drop to.
        /// </returns>
        public static async Task<IBuffer> EncodeSquareJpegAsync(IBuffer imageBytes, int pixels, uint maxBytes = 0)
        {
            if (imageBytes == null || pixels <= 0)
            {
                return null;
            }

            return await WithDecoderAsync(imageBytes, async decoder =>
            {
                // The largest frame, because artwork can be a multi-frame .ico and the first frame of
                // one is its smallest - see LargestFrameAsync
                byte[] square = await CentreSquarePixelsAsync(await LargestFrameAsync(decoder), (uint)pixels);

                using (SoftwareBitmap bitmap = SoftwareBitmap.CreateCopyFromBuffer(
                    square.AsBuffer(), BitmapPixelFormat.Bgra8, pixels, pixels, BitmapAlphaMode.Ignore))
                {
                    foreach (double quality in tileJpegQualitySteps)
                    {
                        IBuffer encoded = await EncodeJpegAsync(bitmap, quality);

                        if (encoded == null || maxBytes == 0 || encoded.Length <= maxBytes)
                        {
                            return encoded;
                        }
                    }

                    // Every step tried and none fit. Returning null rather than the smallest anyway: the
                    // caller cannot write it, and handing back bytes that will be refused only moves the
                    // failure somewhere with less to say about it.
                    return null;
                }
            }, null, $"Could not encode a {pixels}px tile");
        }

        /// <summary>
        /// Returns the image as PNG, re-encoding it when it is anything else.
        ///
        /// Roughly 45% of auto-selected artwork is served as JPEG, and about half of all icons are
        /// .ico, but the Xbox app's own filenames are always .png and it owns those names - so the
        /// bytes have to match the extension rather than the other way round. Windows imaging happens
        /// to sniff content, which is why this has worked so far; that is luck, not a contract, and
        /// the mismatched files also flow into the .bak and .new siblings.
        ///
        /// Format has twice graded as no guide to artwork quality, so this deliberately converts
        /// rather than influencing which artwork gets picked.
        /// </summary>
        /// <param name="imageBytes">Encoded image in any format the platform can decode.</param>
        /// <returns>PNG bytes, or the original bytes when they are already PNG or cannot be decoded.</returns>
        public static async Task<IBuffer> EnsurePngAsync(IBuffer imageBytes)
        {
            // Most artwork already is a PNG. Checking the signature avoids copying every one of those
            // into a stream and standing up a decoder only to discover there is nothing to do.
            if (imageBytes == null || IsPng(imageBytes))
            {
                return imageBytes;
            }

            // Better a mislabelled tile that renders than no tile at all, hence the original bytes on failure
            return await WithDecoderAsync(imageBytes, async decoder =>
            {
                // The largest frame, for the same reason EncodeSquareJpegAsync reads it: half of all
                // icon artwork is a multi-frame .ico, and its first frame is its smallest. Both
                // BitmapDecoder and BitmapFrame carry the software-bitmap read, but only through this
                // interface.
                var frame = (IBitmapFrameWithSoftwareBitmap)await LargestFrameAsync(decoder);

                using (SoftwareBitmap bitmap = await frame.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                {
                    return await EncodePngAsync(bitmap);
                }
            }, imageBytes, "Could not convert artwork to PNG, writing as-is");
        }

        /// <summary>
        /// True when the buffer starts with the PNG signature.
        /// </summary>
        private static bool IsPng(IBuffer imageBytes)
        {
            byte[] signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

            if (imageBytes.Length < signature.Length)
            {
                return false;
            }

            using (var reader = DataReader.FromBuffer(imageBytes))
            {
                byte[] head = new byte[signature.Length];

                reader.ReadBytes(head);

                for (int i = 0; i < signature.Length; i++)
                {
                    if (head[i] != signature[i])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// True when the image is opaque in its corners. Case mockups and rounded icon-style uploads
        /// have transparent corners; legitimate box art fills the whole square.
        /// </summary>
        public static async Task<bool> FillsTileAsync(IBuffer imageBytes)
        {
            // Undecodable here - accept and let the normal pipeline handle it
            return await WithDecoderAsync(imageBytes, async decoder =>
            {
                byte[] pixels = await ScaledPixelsAsync(decoder, null, 32, 32, BitmapAlphaMode.Straight);

                // Sample a 6x6 block in each corner of the 32x32 image; a corner counts as
                // transparent when over 40% of its pixels have near-zero alpha
                int transparentCorners = 0;

                foreach (var corner in new[] { (X: 0, Y: 0), (X: 26, Y: 0), (X: 0, Y: 26), (X: 26, Y: 26) })
                {
                    int transparentPixels = 0;

                    for (int y = corner.Y; y < corner.Y + 6; y++)
                    {
                        for (int x = corner.X; x < corner.X + 6; x++)
                        {
                            if (pixels[(((y * 32) + x) * 4) + 3] < 64)
                            {
                                transparentPixels++;
                            }
                        }
                    }

                    if (transparentPixels > 14)
                    {
                        transparentCorners++;
                    }
                }

                return transparentCorners < 2;
            }, true, "Error inspecting image corners");
        }

        /// <summary>
        /// Crops portrait box art down to the square the tile needs, choosing the vertical window that
        /// carries the most detail.
        ///
        /// The width is already right, so the only question is how far down to take the square. A fixed
        /// offset cannot answer it - titles sit at the top on some covers, across the middle on others,
        /// and along the bottom on a few - so the window is placed by content: the image is reduced to a
        /// per-row measure of edge energy and the square is put where that sums highest. Text and logos
        /// are high-contrast, so they pull the window onto themselves.
        ///
        /// Graded across 35 covers against fixed offsets of 0%, 10%, 15%, 20% and centre: this won 23,
        /// the best fixed offset won 7. A top-weighted variant was tried, because every one of the 11
        /// losses wanted a higher crop than this chose, and it was rejected - it lowered mean error by
        /// shrinking misses on covers that had already been rejected, while agreeing with fewer picks.
        /// </summary>
        /// <param name="imageBytes">Encoded portrait image.</param>
        /// <returns>Square PNG bytes, or null when the image cannot be read or is not portrait.</returns>
        public static async Task<IBuffer> CropPortraitToTileAsync(IBuffer imageBytes)
        {
            return await WithDecoderAsync<IBuffer>(imageBytes, async decoder =>
            {
                uint width = decoder.PixelWidth;
                uint height = decoder.PixelHeight;

                if (height <= width)
                {
                    return null;
                }

                double offset = await BestVerticalCropAsync(decoder);
                var bounds = new BitmapBounds
                {
                    X = 0,
                    Y = (uint)Math.Round((height - width) * offset),
                    Width = width,
                    Height = width
                };

                using (SoftwareBitmap cropped = await decoder.GetSoftwareBitmapAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform { Bounds = bounds },
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage))
                {
                    return await EncodePngAsync(cropped);
                }
            }, null, "Could not crop portrait artwork");
        }

        /// <summary>
        /// Where to place the square window down a portrait image, as a fraction of the spare height.
        /// </summary>
        /// <param name="decoder">Decoder for the portrait image.</param>
        private static async Task<double> BestVerticalCropAsync(BitmapDecoder decoder)
        {
            // Truncated, not rounded, to match the offsets the crop was graded against
            int profileHeight = Math.Max(
                cropProfileWidth + 1,
                (int)(cropProfileWidth * (double)decoder.PixelHeight / decoder.PixelWidth));

            double[] luma = Luma(await ScaledPixelsAsync(
                decoder, null, cropProfileWidth, (uint)profileHeight, BitmapAlphaMode.Ignore));

            // Laplacian per row. The border is left out rather than clamped: an edge filter that reads
            // its own border ends up scoring the outermost rows on brightness instead of edge strength,
            // which drags the window to whichever end of the cover happens to be brightest.
            var rowEnergy = new double[profileHeight];

            for (int y = 1; y < profileHeight - 1; y++)
            {
                double sum = 0;

                for (int x = 1; x < cropProfileWidth - 1; x++)
                {
                    int c = (y * cropProfileWidth) + x;
                    double v = (8 * luma[c])
                        - luma[c - cropProfileWidth - 1] - luma[c - cropProfileWidth] - luma[c - cropProfileWidth + 1]
                        - luma[c - 1] - luma[c + 1]
                        - luma[c + cropProfileWidth - 1] - luma[c + cropProfileWidth] - luma[c + cropProfileWidth + 1];

                    sum += Math.Min(255, Math.Max(0, v));
                }

                rowEnergy[y] = sum / (cropProfileWidth - 2);
            }

            // Sliding window the height of the square, in profile space. profileHeight is at least
            // cropProfileWidth + 1, so there is always at least one position to slide to.
            double running = 0;

            for (int y = 0; y < cropProfileWidth; y++)
            {
                running += rowEnergy[y];
            }

            double best = running;
            int bestTop = 0;
            int span = profileHeight - cropProfileWidth;

            for (int top = 1; top <= span; top++)
            {
                running += rowEnergy[top + cropProfileWidth - 1] - rowEnergy[top - 1];

                if (running > best)
                {
                    best = running;
                    bestTop = top;
                }
            }

            return (double)bestTop / span;
        }
    }
}
