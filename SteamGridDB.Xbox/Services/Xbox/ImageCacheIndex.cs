using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SteamGridDB.Xbox.Services.Xbox
{
    /// <summary>
    /// An index of the Xbox app's tile cache, by exactly what each file contains.
    ///
    /// The cache offers nothing to look a game up by: its files carry no extension and are named by a
    /// 64-bit hash of the request that fetched them. That hash was worked at hard and is not
    /// reproducible - FNV-1/1a, djb2, sdbm, Murmur64A, xxHash64, CRC64 and truncated MD5/SHA over
    /// fourteen forms of the URL in three encodings all fail against twenty-two verified
    /// (key, URL) pairs, and unwinding the keys backwards through the varying part of the URL does not
    /// converge, so it is not FNV over anything ending in the URL either.
    ///
    /// It does not need to be reproducible. The Store's CDN serves a given asset at a given width
    /// deterministically, and the Xbox app writes that response to disk unmodified - so refetching
    /// &lt;artwork uri&gt;?w=&lt;size&gt; reproduces a cached file byte for byte. Hashing both sides
    /// identifies which game a cached file belongs to exactly, with no thresholds and nothing to tune.
    ///
    /// An earlier version compared images perceptually instead. It worked, but it could only ever say
    /// two pictures look alike - and two Store products publishing the same artwork made it claim both
    /// games owned all of both games' files, so changing one tile changed the other.
    /// </summary>
    internal static class ImageCacheIndex
    {
        /// <summary>The cache's own index, which sits in the same folder and is not an image.</summary>
        private const string databaseExtension = ".db";

        /// <summary>
        /// Widths a tile is ever cached at. Above this are the hero and background images, which are
        /// never square and never a tile, and fetching a game's artwork at 1920 to discover that costs
        /// far more than every tile-sized request put together.
        /// </summary>
        internal const int LargestTileSize = 512;

        /// <summary>
        /// How many candidate widths a game's artwork is fetched at. The Xbox app draws its library on
        /// a handful of surfaces and reuses the same few widths for all of them, so the sizes present
        /// in the cache have a long tail of one-offs belonging to store pages and promotional strips.
        /// Taking the most common covers every real tile in a fraction of the requests.
        /// </summary>
        internal const int CandidateSizeLimit = 8;

        /// <summary>
        /// One cached image, by content.
        /// </summary>
        internal sealed class CachedImage
        {
            internal CachedImage(string fileName, int pixelSize, ulong byteLength)
            {
                FileName = fileName;
                PixelSize = pixelSize;
                ByteLength = byteLength;
            }

            /// <summary>The cache's own name for it - the hash, with no extension.</summary>
            internal string FileName { get; }

            /// <summary>Width and height in pixels. Always square; the cache holds a few that are not.</summary>
            internal int PixelSize { get; }

            /// <summary>
            /// Size on disk, which is what makes hashing the cache unnecessary. A cached file can only
            /// be a game's artwork if it is exactly as long as the CDN's response, so the handful of
            /// files that match a length are the only ones worth reading in full.
            /// </summary>
            internal ulong ByteLength { get; }

            /// <summary>
            /// Hex SHA-256 of the file's bytes, or null until <see cref="HashAsync"/> has been asked for
            /// it. Filled in only for files whose length made them a candidate.
            /// </summary>
            internal string Digest { get; set; }
        }

        /// <summary>
        /// The Xbox app's tile cache folder, or null when it does not exist or cannot be read.
        /// </summary>
        internal static async Task<StorageFolder> GetCacheFolderAsync()
        {
            try
            {
                return await StorageFolder.GetFolderFromPathAsync(XboxAppData.ImageCachePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not open the Xbox app image cache: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        /// Indexes every square image in a cache folder by its content.
        ///
        /// Takes the folder rather than finding it, so the walk runs against a throwaway directory in
        /// the tests while only locating the real one stays uncovered - the same split
        /// <see cref="Stores.EaLibrary.ReadInstallerManifestsAsync"/> uses.
        ///
        /// Deliberately cheap. Only each file's header is decoded, for its dimensions, and its length
        /// comes from the file system - nothing is read in full and nothing is hashed. A cache of
        /// several hundred files is a few hundred kilobytes of reads rather than the thirty-odd
        /// megabytes it holds.
        /// </summary>
        /// <param name="cacheFolder">Folder holding the cached images.</param>
        internal static async Task<List<CachedImage>> BuildAsync(StorageFolder cacheFolder)
        {
            List<CachedImage> images = new List<CachedImage>();

            foreach (StorageFile file in await cacheFolder.GetFilesAsync())
            {
                if (file.Name.EndsWith(databaseExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    ulong byteLength = (await file.GetBasicPropertiesAsync()).Size;
                    int pixelSize;

                    using (IRandomAccessStream stream = await file.OpenReadAsync())
                    {
                        BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                        pixelSize = decoder.PixelWidth == decoder.PixelHeight ? (int)decoder.PixelWidth : 0;
                    }

                    // Non-square images are the hero and poster art the app also caches; a tile is
                    // always square, so they can never be a match
                    if (pixelSize > 0 && pixelSize <= LargestTileSize)
                    {
                        images.Add(new CachedImage(file.Name, pixelSize, byteLength));
                    }
                }
                catch (Exception ex)
                {
                    // One unreadable cached file is one tile that cannot be matched, not a failed load
                    System.Diagnostics.Debug.WriteLine($"Could not index cached image {file.Name}: {ex.Message}");
                }
            }

            return images;
        }

        /// <summary>
        /// Hashes the cached files whose length says they could be one of these responses, so that a
        /// byte comparison can be made against the handful that could possibly match rather than the
        /// whole cache.
        /// </summary>
        /// <param name="cacheFolder">Folder holding the cached images.</param>
        /// <param name="images">The indexed cache; matching entries are filled in place.</param>
        /// <param name="responseLengths">Lengths of the CDN responses being looked for.</param>
        internal static async Task HashCandidatesAsync(
            StorageFolder cacheFolder,
            IEnumerable<CachedImage> images,
            ICollection<ulong> responseLengths)
        {
            HashAlgorithmProvider sha256 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);

            foreach (CachedImage image in images ?? Enumerable.Empty<CachedImage>())
            {
                if (image.Digest != null || !responseLengths.Contains(image.ByteLength))
                {
                    continue;
                }

                try
                {
                    image.Digest = Digest(sha256, await FileIO.ReadBufferAsync(await cacheFolder.GetFileAsync(image.FileName)));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not hash cached image {image.FileName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// The widths worth asking the CDN for, most common first.
        /// </summary>
        /// <param name="images">The indexed cache.</param>
        internal static List<int> CandidateSizes(IEnumerable<CachedImage> images)
        {
            return (images ?? Enumerable.Empty<CachedImage>())
                .GroupBy(image => image.PixelSize)
                .OrderByDescending(group => group.Count())
                .ThenByDescending(group => group.Key)
                .Take(CandidateSizeLimit)
                .Select(group => group.Key)
                .ToList();
        }

        /// <summary>
        /// Hex SHA-256 of a buffer, the form both sides of the comparison are reduced to.
        /// </summary>
        internal static string Digest(HashAlgorithmProvider sha256, IBuffer bytes)
        {
            if (bytes == null)
            {
                return null;
            }

            return Windows.Security.Cryptography.CryptographicBuffer.EncodeToHexString(sha256.HashData(bytes));
        }
    }
}
