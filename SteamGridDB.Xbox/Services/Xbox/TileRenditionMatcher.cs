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
            List<ImageCacheIndex.CachedImage> renditions = new List<ImageCacheIndex.CachedImage>();
            List<string> ambiguous = new List<string>();

            if (artworkDigests == null || cached == null)
            {
                return new Result(new List<string>(), ambiguous);
            }

            HashSet<string> wanted = new HashSet<string>(
                artworkDigests.Where(d => !string.IsNullOrEmpty(d)), StringComparer.OrdinalIgnoreCase);

            if (wanted.Count == 0)
            {
                return new Result(new List<string>(), ambiguous);
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

                if (files.Count == 1)
                {
                    renditions.Add(files[0]);
                }
                else
                {
                    ambiguous.AddRange(files.Select(image => image.FileName));
                }
            }

            // Ordered from what the grouping already knows about each file, rather than by looking its
            // size back up in the index - which is a scan of the whole cache per rendition claimed
            return new Result(
                renditions.OrderByDescending(image => image.PixelSize).Select(image => image.FileName).ToList(),
                ambiguous);
        }

        /// <summary>
        /// A game's renditions as the cache has them now: everything already recorded for it, plus
        /// everything a fresh <see cref="Match"/> just attributed to it, largest first.
        ///
        /// A union rather than a replacement, and that is the whole point of it. A rendition someone
        /// has customised no longer holds the artwork that would find it, so a fresh match cannot
        /// return it - taking the match as the whole answer would drop it from the record, and the
        /// record is the only route back to that file. What is stranded is the backup holding the Xbox
        /// app's own artwork, which is the one thing here that cannot be fetched again.
        ///
        /// Sizes come from the index rather than from the disk, which keeps this a plain function over
        /// what the caller already has. A recorded file the index does not know - one the Xbox app
        /// evicted since the record was written - is kept and sorted last rather than dropped, for the
        /// same reason <c>XboxLibrary.SurvivingRenditionsAsync</c> refuses to read silence as absence.
        /// </summary>
        /// <param name="known">Renditions already recorded for the game, or null when it is new.</param>
        /// <param name="discovered">What <see cref="Match"/> just attributed to it.</param>
        /// <param name="cached">The indexed cache, for the sizes to order by.</param>
        internal static List<string> Merge(
            IEnumerable<string> known,
            IEnumerable<string> discovered,
            IEnumerable<ImageCacheIndex.CachedImage> cached)
        {
            Dictionary<string, int> sizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (ImageCacheIndex.CachedImage image in cached ?? Enumerable.Empty<ImageCacheIndex.CachedImage>())
            {
                if (image?.FileName != null)
                {
                    sizes[image.FileName] = image.PixelSize;
                }
            }

            List<string> merged = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string fileName in (known ?? Enumerable.Empty<string>())
                .Concat(discovered ?? Enumerable.Empty<string>()))
            {
                if (!string.IsNullOrEmpty(fileName) && seen.Add(fileName))
                {
                    merged.Add(fileName);
                }
            }

            // A stable sort, so the files no size is known for keep the order they arrived in behind
            // the ones that do rather than being shuffled among themselves
            return merged
                .OrderByDescending(name => sizes.TryGetValue(name, out int size) ? size : 0)
                .ToList();
        }
    }
}
