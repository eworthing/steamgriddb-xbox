using System;
using System.Collections.Generic;
using System.Linq;

namespace SteamGridDB.Xbox.Services.Xbox
{
    /// <summary>
    /// Decides which cached images are a game's tile, by exact content.
    ///
    /// The Xbox app fetches the same artwork at several widths - a library grid, a sidebar row, a
    /// recently-played strip - and caches each response separately, so one game owns anything from one
    /// to eight cached files and changing its tile means changing all of them.
    ///
    /// A cached file is one game's when its bytes are exactly the bytes the CDN returns for that
    /// game's artwork at that width. There is no threshold and nothing to tune, and a game whose
    /// artwork merely resembles another's is never confused with it.
    ///
    /// What that cannot settle is two products that publish the *same* artwork. Minecraft for Windows
    /// and Minecraft: Java Edition are different Store assets carrying an identical picture - identical
    /// to the byte - so the app caches them under two keys holding the same content, and nothing in
    /// either file says which product fetched it. Those are reported as ambiguous rather than given to
    /// both games, which is what made changing one Minecraft change the other.
    /// </summary>
    internal static class TileRenditionMatcher
    {
        /// <summary>
        /// One game's cached renditions, and any it could not be given outright.
        /// </summary>
        internal readonly struct Result
        {
            internal Result(IReadOnlyList<string> renditionFileNames, IReadOnlyList<string> ambiguousFileNames)
            {
                RenditionFileNames = renditionFileNames;
                AmbiguousFileNames = ambiguousFileNames;
            }

            /// <summary>Cache files that are unambiguously this game's, largest first.</summary>
            internal IReadOnlyList<string> RenditionFileNames { get; }

            /// <summary>
            /// Cache files holding this game's artwork that another cached file matches byte for byte,
            /// so which of them belongs to this game cannot be told from content alone.
            /// </summary>
            internal IReadOnlyList<string> AmbiguousFileNames { get; }

            internal bool HasAmbiguity => AmbiguousFileNames.Count > 0;
        }

        /// <summary>
        /// The cached images matching a game's artwork.
        /// </summary>
        /// <param name="artworkDigests">SHA-256 of the CDN's response for this game's artwork, one per width fetched.</param>
        /// <param name="cached">The indexed cache.</param>
        internal static Result Match(
            IEnumerable<string> artworkDigests,
            IEnumerable<ImageCacheIndex.CachedImage> cached)
        {
            List<string> renditions = new List<string>();
            List<string> ambiguous = new List<string>();

            if (artworkDigests == null || cached == null)
            {
                return new Result(renditions, ambiguous);
            }

            HashSet<string> wanted = new HashSet<string>(
                artworkDigests.Where(d => !string.IsNullOrEmpty(d)), StringComparer.OrdinalIgnoreCase);

            if (wanted.Count == 0)
            {
                return new Result(renditions, ambiguous);
            }

            // Grouped by content, because that is precisely what makes a file attributable: a digest
            // held by one cache file belongs to whichever game's artwork produced it, and a digest held
            // by several means several products published the same picture
            foreach (var group in cached
                .Where(image => image?.Digest != null && wanted.Contains(image.Digest))
                .GroupBy(image => image.Digest, StringComparer.OrdinalIgnoreCase))
            {
                List<ImageCacheIndex.CachedImage> files = group
                    .OrderByDescending(image => image.PixelSize)
                    .ToList();

                (files.Count == 1 ? renditions : ambiguous).AddRange(files.Select(image => image.FileName));
            }

            return new Result(
                renditions.OrderByDescending(name => PixelSizeOf(cached, name)).ToList(),
                ambiguous);
        }

        private static int PixelSizeOf(IEnumerable<ImageCacheIndex.CachedImage> cached, string fileName)
        {
            return cached.FirstOrDefault(image => image.FileName == fileName)?.PixelSize ?? 0;
        }
    }
}
