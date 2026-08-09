using System.Collections.Generic;
using System.Linq;

using SteamGridDB.Xbox.Services.Xbox;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Which cached images are a given game's tile.
    ///
    /// This is the step with no second chance. A wrong answer writes someone's artwork over another
    /// game's tile, and once a rendition has been written over it no longer matches the artwork that
    /// would find it - so a mistake here is not simply retried on the next load.
    ///
    /// Attribution is by exact content, which is why there are no thresholds to test around. The
    /// Store's CDN serves an asset at a given width deterministically and the Xbox app writes that
    /// response to disk unmodified, so a cached file either is byte-for-byte what a game's artwork
    /// returns or it is not. What these pin is the one thing exactness cannot settle on its own: two
    /// products publishing the same picture, which is real - Minecraft for Windows and Minecraft: Java
    /// Edition are separate Store assets holding an identical image, cached under two keys.
    /// </summary>
    public class TileRenditionMatcherTests
    {
        /// <summary>
        /// A cached file already hashed. Length is what decides whether the real index bothers hashing
        /// a file at all, and plays no part in matching, so it is given a distinct value per digest
        /// purely so no two fixtures collide on it.
        /// </summary>
        private static ImageCacheIndex.CachedImage Cached(string fileName, int pixelSize, string digest)
        {
            return new ImageCacheIndex.CachedImage(fileName, pixelSize, (ulong)digest.GetHashCode())
            {
                Digest = digest,
            };
        }

        [Fact]
        public void Claims_the_cached_files_matching_the_games_artwork()
        {
            var cached = new List<ImageCacheIndex.CachedImage>
            {
                Cached("large", 329, "AAA"),
                Cached("small", 72, "BBB"),
                Cached("someone-elses", 280, "ZZZ"),
            };

            TileRenditionMatcher.Result result = TileRenditionMatcher.Match(new[] { "AAA", "BBB" }, cached);

            Assert.Equal(new[] { "large", "small" }, result.RenditionFileNames);
            Assert.False(result.HasAmbiguity);
        }

        [Fact]
        public void Returns_renditions_largest_first()
        {
            // One game owns one to eight cached files and the tile only changes everywhere when all of
            // them do, so all come back - in size order, because the caller takes the largest as the
            // row's representative image
            var cached = new List<ImageCacheIndex.CachedImage>
            {
                Cached("small", 72, "AAA"),
                Cached("large", 329, "BBB"),
                Cached("medium", 280, "CCC"),
            };

            TileRenditionMatcher.Result result = TileRenditionMatcher.Match(new[] { "AAA", "BBB", "CCC" }, cached);

            Assert.Equal(new[] { "large", "medium", "small" }, result.RenditionFileNames);
        }

        [Fact]
        public void Ignores_a_cached_file_whose_content_the_game_does_not_publish()
        {
            var cached = new List<ImageCacheIndex.CachedImage> { Cached("other-game", 329, "ZZZ") };

            Assert.Empty(TileRenditionMatcher.Match(new[] { "AAA" }, cached).RenditionFileNames);
        }

        [Fact]
        public void Two_products_with_identical_artwork_leave_their_shared_files_unclaimed()
        {
            // The Minecraft case. Both products publish the same picture, so the app cached it twice
            // under two keys with identical bytes - and nothing in either file says which product
            // fetched it. Handing both to whichever game asked first is what made changing one
            // Minecraft's tile change the other's.
            var cached = new List<ImageCacheIndex.CachedImage>
            {
                Cached("14163050974037971509", 329, "SHARED"),
                Cached("14525979875954995941", 329, "SHARED"),
            };

            TileRenditionMatcher.Result result = TileRenditionMatcher.Match(new[] { "SHARED" }, cached);

            Assert.Empty(result.RenditionFileNames);
            Assert.True(result.HasAmbiguity);
            Assert.Equal(
                new[] { "14163050974037971509", "14525979875954995941" },
                result.AmbiguousFileNames.OrderBy(n => n).ToArray());
        }

        [Fact]
        public void A_games_unshared_renditions_survive_its_shared_ones()
        {
            // Minecraft for Windows also publishes a promotional square Java Edition does not, so some
            // of its renditions are attributable even though its box art is not. Those are still its
            // own and are kept.
            var cached = new List<ImageCacheIndex.CachedImage>
            {
                Cached("shared-a", 329, "SHARED"),
                Cached("shared-b", 329, "SHARED"),
                Cached("mine-only", 280, "UNIQUE"),
            };

            TileRenditionMatcher.Result result = TileRenditionMatcher.Match(new[] { "SHARED", "UNIQUE" }, cached);

            Assert.Equal(new[] { "mine-only" }, result.RenditionFileNames);
            Assert.Equal(2, result.AmbiguousFileNames.Count);
        }

        [Fact]
        public void The_same_content_at_different_sizes_is_not_ambiguous()
        {
            // Ambiguity is two files with the *same* bytes. Two files of different sizes never have
            // that, however alike the pictures look, so a game with renditions at several widths must
            // not be mistaken for a collision.
            var cached = new List<ImageCacheIndex.CachedImage>
            {
                Cached("at-329", 329, "AAA"),
                Cached("at-280", 280, "BBB"),
                Cached("at-72", 72, "CCC"),
            };

            TileRenditionMatcher.Result result = TileRenditionMatcher.Match(new[] { "AAA", "BBB", "CCC" }, cached);

            Assert.Equal(3, result.RenditionFileNames.Count);
            Assert.False(result.HasAmbiguity);
        }

        [Fact]
        public void Digests_are_compared_without_regard_to_hex_casing()
        {
            var cached = new List<ImageCacheIndex.CachedImage> { Cached("tile", 329, "abcdef") };

            Assert.Equal(new[] { "tile" }, TileRenditionMatcher.Match(new[] { "ABCDEF" }, cached).RenditionFileNames);
        }

        [Fact]
        public void Nothing_to_match_against_matches_nothing()
        {
            // A game whose catalogue entry carries no square artwork cannot be located, and must not
            // instead match everything
            var cached = new List<ImageCacheIndex.CachedImage> { Cached("tile", 329, "AAA") };

            Assert.Empty(TileRenditionMatcher.Match(new string[0], cached).RenditionFileNames);
            Assert.Empty(TileRenditionMatcher.Match(null, cached).RenditionFileNames);
            Assert.Empty(TileRenditionMatcher.Match(new[] { "AAA" }, null).RenditionFileNames);
        }

        [Fact]
        public void CandidateSizes_prefers_the_widths_the_cache_actually_uses()
        {
            // The app reuses a few widths across its surfaces; the long tail belongs to store pages and
            // promotional strips. Fetching a game's artwork at every width seen anywhere would cost far
            // more requests than every real tile put together.
            var cached = new List<ImageCacheIndex.CachedImage>
            {
                Cached("a", 329, "1"), Cached("b", 329, "2"), Cached("c", 329, "3"),
                Cached("d", 280, "4"), Cached("e", 280, "5"),
                Cached("f", 95, "6"),
            };

            Assert.Equal(new[] { 329, 280, 95 }, ImageCacheIndex.CandidateSizes(cached));
        }

        [Fact]
        public void CandidateSizes_is_bounded()
        {
            var cached = Enumerable.Range(1, 40)
                .Select(i => Cached($"f{i}", i * 10, i.ToString()))
                .ToList();

            Assert.Equal(ImageCacheIndex.CandidateSizeLimit, ImageCacheIndex.CandidateSizes(cached).Count);
        }

        [Fact]
        public void An_unhashed_cached_file_is_never_a_match()
        {
            // The index only hashes files whose length says they could be one of the responses being
            // looked for. Everything else is left with no digest, and must not be treated as one that
            // failed to match - still less as one that did.
            var cached = new List<ImageCacheIndex.CachedImage>
            {
                new ImageCacheIndex.CachedImage("not-hashed", 329, 12345),
            };

            TileRenditionMatcher.Result result = TileRenditionMatcher.Match(new[] { "AAA" }, cached);

            Assert.Empty(result.RenditionFileNames);
            Assert.False(result.HasAmbiguity);
        }

        [Fact]
        public void CandidateSizes_copes_with_an_empty_cache()
        {
            Assert.Empty(ImageCacheIndex.CandidateSizes(new List<ImageCacheIndex.CachedImage>()));
            Assert.Empty(ImageCacheIndex.CandidateSizes(null));
        }

        [Fact]
        public void Merge_keeps_a_customised_rendition_a_fresh_match_can_no_longer_find()
        {
            // Fuzion Frenzy's case, and the reason this is a union rather than a replacement. Its 84px
            // rendition holds someone's chosen artwork, so it no longer matches what the Store serves
            // and Match cannot return it; its 329px rendition was cached eleven minutes after discovery
            // ran and still holds the Store's own. Dropping the customised one would leave its backup -
            // the Xbox app's original, which nothing else can recreate - with no record naming it.
            var cached = new List<ImageCacheIndex.CachedImage>
            {
                Cached("7108528410040166518", 84, "CUSTOMISED"),
                Cached("8501456819577244772", 329, "STORE-ART"),
            };

            Assert.Equal(
                new[] { "8501456819577244772", "7108528410040166518" },
                TileRenditionMatcher.Merge(new[] { "7108528410040166518" }, new[] { "8501456819577244772" }, cached));
        }

        [Fact]
        public void Merge_orders_the_union_largest_first()
        {
            // The largest stands in for the game wherever one path is needed - the row's thumbnail, the
            // applied-artwork key - so a rendition joining the set has to take that place if it is
            // bigger than what was there
            var cached = new List<ImageCacheIndex.CachedImage>
            {
                Cached("small", 72, "A"),
                Cached("large", 329, "B"),
                Cached("medium", 280, "C"),
            };

            Assert.Equal(
                new[] { "large", "medium", "small" },
                TileRenditionMatcher.Merge(new[] { "small", "medium" }, new[] { "large" }, cached));
        }

        [Fact]
        public void Merge_claims_a_rendition_only_once_however_it_arrived()
        {
            // The ordinary case: a game whose renditions still hold the Store's artwork is re-matched
            // to exactly what its record already names
            var cached = new List<ImageCacheIndex.CachedImage> { Cached("tile", 329, "A") };

            Assert.Equal(new[] { "tile" }, TileRenditionMatcher.Merge(new[] { "tile" }, new[] { "tile" }, cached));
        }

        [Fact]
        public void Merge_with_nothing_known_is_the_match_alone()
        {
            // What the first discovery of a game passes, so that one method serves both callers
            var cached = new List<ImageCacheIndex.CachedImage>
            {
                Cached("small", 84, "A"),
                Cached("large", 329, "B"),
            };

            Assert.Equal(new[] { "large", "small" }, TileRenditionMatcher.Merge(null, new[] { "small", "large" }, cached));
            Assert.Empty(TileRenditionMatcher.Merge(null, new string[0], cached));
        }

        [Fact]
        public void Merge_keeps_a_recorded_file_the_cache_no_longer_holds_but_sorts_it_last()
        {
            // A rendition the Xbox app has evicted has no entry in the index and so no size to sort by.
            // It is kept because a record dropped by mistake cannot be rebuilt once artwork has been
            // applied over the tiles, and sorted last so it never becomes the game's primary.
            var cached = new List<ImageCacheIndex.CachedImage> { Cached("still-here", 329, "A") };

            Assert.Equal(
                new[] { "still-here", "evicted" },
                TileRenditionMatcher.Merge(new[] { "evicted" }, new[] { "still-here" }, cached));
        }

        [Fact]
        public void Merge_copes_with_null()
        {
            Assert.Empty(TileRenditionMatcher.Merge(null, null, null));
            Assert.Equal(new[] { "tile" }, TileRenditionMatcher.Merge(new[] { "tile" }, null, null));
        }
    }
}
