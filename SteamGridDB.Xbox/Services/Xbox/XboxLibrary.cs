using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.Streams;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Stores;

namespace SteamGridDB.Xbox.Services.Xbox
{
    /// <summary>
    /// The Xbox app's own games, paired with the cached images that make up each one's tile.
    ///
    /// This is the first-party counterpart to the ThirdPartyLibraries walk in LoadGameEntriesAsync: it
    /// answers the same question - which games are there, and which file is each one's artwork - for
    /// the games the Xbox app installed itself, whose artwork it holds in a content-addressed cache
    /// rather than a folder named after the game.
    ///
    /// Discovery is the expensive step and is avoided wherever possible. A game whose renditions are
    /// already recorded and still on disk is taken straight from the record; the cache is only signed
    /// when at least one game is left over, and reference artwork is only fetched for those. On a
    /// settled library that is no work at all beyond a catalogue lookup.
    /// </summary>
    internal static class XboxLibrary
    {
        /// <summary>
        /// How many artwork requests are in flight at once during discovery. Eight took the measured
        /// cost of a ten-request round from 1.3 seconds to 0.3.
        /// </summary>
        private const int ConcurrentRequests = 8;


        /// <summary>
        /// One first-party game and the cached images that are its tile.
        /// </summary>
        internal sealed class Game
        {
            internal Game(string storeId, string title, IReadOnlyList<string> renditionFileNames)
            {
                StoreId = storeId;
                Title = title;
                RenditionFileNames = renditionFileNames;
            }

            internal string StoreId { get; }

            /// <summary>The Store's own title, which is what gets searched for on SteamGridDB.</summary>
            internal string Title { get; }

            /// <summary>The game's cached images, largest first. Never empty.</summary>
            internal IReadOnlyList<string> RenditionFileNames { get; }

            /// <summary>The rendition standing in for the game in the list - the largest.</summary>
            internal string PrimaryFileName => RenditionFileNames[0];
        }

        /// <summary>
        /// What the load found, for the library-load log.
        /// </summary>
        internal static string LoadSummary { get; private set; } = "not read yet";

        /// <summary>
        /// Every installed first-party game whose tile could be located in the Xbox app's image cache.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder.</param>
        internal static async Task<List<Game>> LoadAsync(StorageFolder cacheFolder)
        {
            List<StoreCatalog.Product> products;

            using (StoreCatalog catalog = new StoreCatalog())
            {
                products = await XboxInstalledGames.LoadAsync(catalog);
            }

            // One enumeration answers every "is this rendition still there" question the loop below
            // asks, rather than a file open apiece
            HashSet<string> cachedFileNames = await CacheFileNamesAsync(cacheFolder);

            List<Game> games = new List<Game>();
            List<StoreCatalog.Product> undiscovered = new List<StoreCatalog.Product>();

            foreach (StoreCatalog.Product product in products)
            {
                IReadOnlyList<string> known = await SurvivingRenditionsAsync(cacheFolder, cachedFileNames, product.StoreId);

                if (known != null)
                {
                    games.Add(new Game(product.StoreId, product.Title, known));
                }
                else if (product.TileArtworkUris.Count > 0)
                {
                    undiscovered.Add(product);
                }
            }

            int discovered = await DiscoverAsync(cacheFolder, undiscovered, games);

            // After discovery, so a game whose renditions were located on this very load counts as
            // claiming them rather than being swept for not having claimed them yet
            await ForgetOrphanedArtworkRecordsAsync(cacheFolder);

            LoadSummary = $"{games.Count} of {products.Count} game{(products.Count == 1 ? string.Empty : "s")} located"
                + $" ({discovered} newly discovered); {XboxInstalledGames.LoadSummary}";

            return games;
        }

        /// <summary>
        /// The recorded renditions for a game that are still on disk, or null when it has none recorded
        /// or none of them survive.
        ///
        /// The second case is the one that matters: when the Store changes a game's artwork, the Xbox
        /// app fetches it under a new hash and the old files go, taking the customisation with them.
        /// Nothing on disk can prevent that, so the record is dropped and the game is discovered again
        /// against its new artwork.
        ///
        /// Which makes "gone" an answer worth being sure of. A record dropped by mistake cannot be
        /// rebuilt once artwork has been applied over the tiles - the whole point of
        /// <see cref="XboxTileStore"/> - so a cache that could not be read at all leaves every record
        /// alone rather than reading silence as absence.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder.</param>
        /// <param name="cachedFileNames">Every file in the cache, or null when it could not be listed.</param>
        /// <param name="storeId">The game's Microsoft Store product ID.</param>
        private static async Task<IReadOnlyList<string>> SurvivingRenditionsAsync(StorageFolder cacheFolder, HashSet<string> cachedFileNames, string storeId)
        {
            IReadOnlyList<string> known = await XboxTileStore.GetAsync(storeId);

            if (known == null || known.Count == 0)
            {
                return null;
            }

            if (cachedFileNames == null)
            {
                // Nothing could be read, so nothing is known to be gone. Taking the record at its word
                // costs at most a load where a write is skipped; reading it as gone costs the record.
                return known;
            }

            List<string> surviving = known.Where(cachedFileNames.Contains).ToList();
            List<string> lost = known.Where(name => !cachedFileNames.Contains(name)).ToList();

            // An evicted rendition comes back under the same name - the hash is of the request, and the
            // request does not change - so a saved customisation left behind for one would be written
            // straight back over it, resurrecting artwork that has since been reverted or replaced
            await XboxTiles.DiscardSavedCustomisationsAsync(lost);

            // Nothing will ever look these paths up again, so a record left under one describes a tile
            // this game no longer has - see ForgetArtworkRecordsAsync
            await XboxTiles.ForgetArtworkRecordsAsync(cacheFolder, lost);

            if (surviving.Count == 0)
            {
                await XboxTileStore.ClearAsync(storeId);

                return null;
            }

            if (lost.Count > 0)
            {
                await XboxTileStore.SetAsync(storeId, surviving);
            }

            return surviving;
        }

        /// <summary>
        /// Drops applied-artwork records for cached images no game claims any more.
        ///
        /// <see cref="XboxTiles.ForgetArtworkRecordsAsync"/> covers the two cases reachable from a game
        /// - a rendition that left its set, and a game with no backup - but both start from a game and
        /// walk to its renditions. A record whose image belongs to no game at all is reachable from
        /// neither, because nothing enumerates it; the only way to find one is to walk the records
        /// instead. That is what this does, once per load, over a file with as many entries as the user
        /// has customised games.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder.</param>
        private static async Task ForgetOrphanedArtworkRecordsAsync(StorageFolder cacheFolder)
        {
            HashSet<string> tracked = await XboxTileStore.AllRenditionsAsync();

            // Nothing known is not the same as nothing claimed. A tile record that failed to load would
            // otherwise make every first-party record look orphaned on the same pass - see
            // XboxTiles.IsOrphanedRecord, which refuses an empty set for the same reason. Checked here
            // as well so the walk is not even started.
            if (tracked.Count == 0)
            {
                return;
            }

            await AppliedArtworkStore.ForgetWhereAsync(
                key => XboxTiles.IsOrphanedRecord(key, cacheFolder.Path, tracked));
        }

        /// <summary>
        /// The name of every file in the cache folder, or null when it could not be listed.
        /// </summary>
        /// <param name="cacheFolder">The Xbox app's image cache folder.</param>
        private static async Task<HashSet<string>> CacheFileNamesAsync(StorageFolder cacheFolder)
        {
            try
            {
                HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (StorageFile file in await cacheFolder.GetFilesAsync())
                {
                    names.Add(file.Name);
                }

                return names;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not list the Xbox app image cache: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        /// Signs the cache once and matches every game that still needs its tile located, appending
        /// what it finds to <paramref name="games"/>.
        /// </summary>
        /// <returns>How many games were newly discovered.</returns>
        private static async Task<int> DiscoverAsync(
            StorageFolder cacheFolder,
            List<StoreCatalog.Product> undiscovered,
            List<Game> games)
        {
            if (undiscovered.Count == 0)
            {
                return 0;
            }

            List<ImageCacheIndex.CachedImage> index = await ImageCacheIndex.BuildAsync(cacheFolder);
            List<int> sizes = ImageCacheIndex.CandidateSizes(index);
            int discovered = 0;

            foreach (StoreCatalog.Product product in undiscovered)
            {
                List<Response> responses = await ArtworkResponsesAsync(product, sizes);

                if (responses.Count == 0)
                {
                    continue;
                }

                // Only the cached files as long as one of these responses can possibly be a match, and
                // there are rarely more than a handful - so the cache is hashed a few files at a time
                // rather than in full
                await ImageCacheIndex.HashCandidatesAsync(
                    cacheFolder, index, new HashSet<ulong>(responses.Select(r => r.ByteLength)));

                TileRenditionMatcher.Result match = TileRenditionMatcher.Match(
                    responses.Select(r => r.Digest), index);

                if (match.HasAmbiguity)
                {
                    // Another Store product publishes the identical picture, so the cache holds two
                    // files with the same bytes and nothing says which product fetched which. Claiming
                    // both would put this game's artwork on the other game's tile.
                    FixLog.Write($"{product.Title}: {match.AmbiguousFileNames.Count} rendition(s) shared with"
                        + " another product's identical artwork, left alone");
                }

                if (match.RenditionFileNames.Count == 0)
                {
                    // Either the tile has never been rendered - so the app has never fetched it, and
                    // the game simply cannot be customised until it has been on screen - or every one
                    // of its renditions was ambiguous
                    continue;
                }

                // Saved before anything is applied, and before the game is even returned: once a
                // rendition is overwritten it no longer matches the artwork that just found it, so
                // this record becomes the only route back to these files
                await XboxTileStore.SetAsync(product.StoreId, match.RenditionFileNames);

                games.Add(new Game(product.StoreId, product.Title, match.RenditionFileNames));
                discovered++;
            }

            return discovered;
        }

        /// <summary>What the CDN returned for one artwork at one width.</summary>
        private readonly struct Response
        {
            internal Response(ulong byteLength, string digest)
            {
                ByteLength = byteLength;
                Digest = digest;
            }

            internal ulong ByteLength { get; }

            internal string Digest { get; }
        }

        /// <summary>
        /// What the CDN returns for a game's artwork at each width the cache uses.
        ///
        /// This is the only network cost of discovery and the only slow part of it, paid once per game
        /// ever. The requests are small but there are a dozen or so of them per game, and each is a
        /// round trip to a CDN - run one after another they add up to far longer than the work
        /// deserves, so they go out together instead. The cap is there because the point is to overlap
        /// latency, not to open a dozen connections to Microsoft at once.
        /// </summary>
        private static async Task<List<Response>> ArtworkResponsesAsync(StoreCatalog.Product product, IReadOnlyList<int> sizes)
        {
            // The width alone, with no height and no format: the exact request the Xbox app makes,
            // established by matching a cached file against every other candidate form
            List<string> urls = product.TileArtworkUris
                .SelectMany(uri => sizes.Select(size => $"{uri}?w={size}"))
                .ToList();

            List<Response> responses = new List<Response>();
            HashAlgorithmProvider sha256 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Sha256);

            for (int offset = 0; offset < urls.Count; offset += ConcurrentRequests)
            {
                IBuffer[] batch = await Task.WhenAll(urls
                    .Skip(offset)
                    .Take(ConcurrentRequests)
                    .Select(ArtworkDownloader.DownloadArtworkAsync));

                foreach (IBuffer bytes in batch)
                {
                    string digest = ImageCacheIndex.Digest(sha256, bytes);

                    if (digest != null)
                    {
                        responses.Add(new Response(bytes.Length, digest));
                    }
                }
            }

            return responses;
        }
    }
}
