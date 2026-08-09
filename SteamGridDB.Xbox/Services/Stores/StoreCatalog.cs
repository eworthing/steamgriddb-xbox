using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

using SteamGridDB.Xbox.Services;

namespace SteamGridDB.Xbox.Services.Stores
{
    /// <summary>
    /// Reads product details out of the Microsoft Store's public display catalogue - the same service
    /// the Xbox app itself calls, and the source of the artwork it caches.
    ///
    /// This is what makes first-party support possible without touching the Xbox app's own databases.
    /// The app keeps everything needed in a 40MB SQLite file (LocalState\AsyncCache.db), but reading it
    /// would mean shipping SQLite interop to duplicate a service that answers the same questions over
    /// plain HTTPS with no authentication. Two facts come from here that nothing else provides:
    ///
    /// <list type="bullet">
    /// <item>the artwork URIs the Xbox app fetched, which is how its cached tiles are located at all -
    /// they are named by a hash of the request and can only be found by matching their content</item>
    /// <item>the product kind, which is the only reliable way to tell a game from the content packs
    /// that install exactly like one ("Call of Duty: Black Ops 6 - Content Pack 1" comes back as
    /// Durable, and there are a dozen of those on a typical Call of Duty install)</item>
    /// </list>
    ///
    /// Everything is asked for in the US/en-us market. Not a locale bug: the titles are fed to a
    /// SteamGridDB name search, and SteamGridDB names games in English - the same reason
    /// <see cref="EaLibrary.ParseInstallerManifest"/> prefers the en_US title over the user's own.
    /// </summary>
    internal class StoreCatalog : IDisposable
    {
        private const string baseUrl = "https://displaycatalog.mp.microsoft.com/v7.0/products";

        private const string market = "US";
        private const string language = "en-us";

        /// <summary>
        /// How many products one bigIds request asks for. The endpoint accepts more, but a request that
        /// grows past a few dozen IDs starts being refused for URL length rather than for its contents,
        /// and a library of any size is only two or three requests at this size anyway.
        /// </summary>
        internal const int BatchSize = 20;

        /// <summary>
        /// The purposes worth fingerprinting. The Xbox app's square tile is served from one of these
        /// two - which one varies by title, and for some games they are the same picture - so both are
        /// used as references and whichever matches wins. LOGO is deliberately excluded: it is a small
        /// square icon that several unrelated games share, and matching on it produces false positives.
        /// </summary>
        private static readonly string[] tileArtworkPurposes = { "BoxArt", "FeaturePromotionalSquareArt" };

        private readonly HttpClient httpClient;
        private readonly TimeSpan timeout;
        private bool disposed;

        internal StoreCatalog(int timeoutSeconds = 30)
        {
            timeout = TimeSpan.FromSeconds(timeoutSeconds);

            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Accept.Add(new HttpMediaTypeWithQualityHeaderValue("application/json"));
            AppIdentity.Identify(httpClient.DefaultRequestHeaders);

            // The catalogue expects a correlation vector on every request and answers 400 without one.
            // Its value is only ever used for Microsoft's own request tracing, so one per client is enough.
            httpClient.DefaultRequestHeaders.Add("MS-CV", Convert.ToBase64String(Guid.NewGuid().ToByteArray()).Substring(0, 16) + ".0");
        }

        /// <summary>
        /// One catalogue product, reduced to what the library load needs.
        /// </summary>
        internal readonly struct Product
        {
            internal Product(string storeId, string title, string productKind, IReadOnlyList<string> tileArtworkUris)
            {
                StoreId = storeId;
                Title = title;
                ProductKind = productKind;
                TileArtworkUris = tileArtworkUris;
            }

            internal string StoreId { get; }

            internal string Title { get; }

            /// <summary>"Game" for a game, "Durable" for a content pack, and several others besides.</summary>
            internal string ProductKind { get; }

            /// <summary>
            /// Square artwork the Xbox app's tile could have been rendered from, largest first. Never
            /// null; empty when the product has none, which means its tile cannot be located.
            /// </summary>
            internal IReadOnlyList<string> TileArtworkUris { get; }

            /// <summary>Whether this product is a game rather than a content pack or subscription.</summary>
            internal bool IsGame => string.Equals(ProductKind, "Game", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Looks up products by Store ID, in batches. IDs the catalogue does not know are simply absent
        /// from the result rather than being an error.
        /// </summary>
        /// <param name="storeIds">Store IDs to look up.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task<List<Product>> GetByStoreIdsAsync(IEnumerable<string> storeIds, CancellationToken cancellationToken = default)
        {
            List<Product> products = new List<Product>();
            List<string> pending = storeIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? new List<string>();

            for (int offset = 0; offset < pending.Count; offset += BatchSize)
            {
                string batch = string.Join(",", pending.Skip(offset).Take(BatchSize));
                string url = $"{baseUrl}?bigIds={Uri.EscapeDataString(batch)}&market={market}&languages={language}&fieldsTemplate=Details";

                products.AddRange(ParseProducts(await GetStringAsync(url, cancellationToken)));
            }

            return products;
        }

        /// <summary>
        /// Looks up products by package family name, one request apiece.
        ///
        /// This is the only way to name the product behind a package that carries no
        /// MicrosoftGame.config, and it is markedly more expensive than the lookup above: the bigIds
        /// endpoint takes twenty product IDs in one request, while this one takes a single alternate ID
        /// and answers an empty list for a comma-separated pair rather than resolving both. A round
        /// trip per package is only affordable because so few packages ever reach here - the manifest
        /// test in <see cref="PackageManifest"/> reduces an ordinary machine's hundred-odd installed
        /// packages to the handful that are games.
        /// </summary>
        /// <param name="packageFamilyNames">Package family names to look up.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        internal async Task<List<Product>> GetByPackageFamilyNamesAsync(
            IEnumerable<string> packageFamilyNames,
            CancellationToken cancellationToken = default)
        {
            List<Product> products = new List<Product>();
            List<string> pending = packageFamilyNames?.Where(name => !string.IsNullOrEmpty(name))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? new List<string>();

            foreach (string familyName in pending)
            {
                string url = $"{baseUrl}/lookup?alternateId=PackageFamilyName&value={Uri.EscapeDataString(familyName)}"
                    + $"&market={market}&languages={language}&fieldsTemplate=Details";

                products.AddRange(ParseProducts(await GetStringAsync(url, cancellationToken)));
            }

            return products;
        }

        /// <summary>
        /// Reads a catalogue response into products. Kept separate from the request so the shape of the
        /// document - which is deeply nested and mostly irrelevant - is pinned by tests against a
        /// captured response rather than by running against a live service.
        /// </summary>
        /// <param name="json">A products response body.</param>
        /// <returns>Every product the document describes; empty when it describes none or will not parse.</returns>
        internal static List<Product> ParseProducts(string json)
        {
            List<Product> products = new List<Product>();

            if (string.IsNullOrEmpty(json) || !JsonObject.TryParse(json, out JsonObject root))
            {
                return products;
            }

            JsonArray entries = JsonRead.Array(root, "Products");

            if (entries == null)
            {
                return products;
            }

            foreach (IJsonValue entry in entries)
            {
                if (entry.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                JsonObject product = entry.GetObject();
                string storeId = JsonRead.String(product, "ProductId");

                if (string.IsNullOrEmpty(storeId))
                {
                    continue;
                }

                JsonArray localised = JsonRead.Array(product, "LocalizedProperties");
                JsonObject properties = localised != null && localised.Count > 0 && localised[0].ValueType == JsonValueType.Object
                    ? localised.GetObjectAt(0)
                    : null;

                products.Add(new Product(
                    storeId,
                    JsonRead.String(properties, "ProductTitle"),
                    JsonRead.String(product, "ProductKind"),
                    ReadTileArtworkUris(properties)));
            }

            return products;
        }

        /// <summary>
        /// The square artwork URIs worth fingerprinting, largest first.
        ///
        /// Sorted by size because the largest rendition is the one most likely to have been fetched at
        /// several sizes, and a reference fetched large downscales to match every cached size cleanly.
        /// </summary>
        private static IReadOnlyList<string> ReadTileArtworkUris(JsonObject properties)
        {
            List<(int Width, string Uri)> found = new List<(int, string)>();
            JsonArray images = JsonRead.Array(properties, "Images");

            if (images == null)
            {
                return Array.Empty<string>();
            }

            foreach (IJsonValue value in images)
            {
                if (value.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                JsonObject image = value.GetObject();
                string purpose = JsonRead.String(image, "ImagePurpose");

                if (purpose == null || !tileArtworkPurposes.Contains(purpose, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                int width = (int)JsonRead.Number(image, "Width");
                int height = (int)JsonRead.Number(image, "Height");

                if (width <= 0 || width != height)
                {
                    continue;
                }

                string uri = NormaliseUri(JsonRead.String(image, "Uri"));

                if (uri != null)
                {
                    found.Add((width, uri));
                }
            }

            return found.OrderByDescending(f => f.Width).Select(f => f.Uri).ToList();
        }

        /// <summary>
        /// The catalogue returns protocol-relative URIs ("//store-images.s-microsoft.com/..."), which
        /// no HTTP client will accept as-is.
        /// </summary>
        internal static string NormaliseUri(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return null;
            }

            return uri.StartsWith("//", StringComparison.Ordinal) ? "https:" + uri : uri;
        }

        /// <summary>
        /// GET returning the raw body, or null on any failure. A catalogue that cannot be reached means
        /// first-party games do not appear this load - never that the library failed to load.
        /// </summary>
        private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                using (CancellationTokenSource timeoutCts = new CancellationTokenSource(timeout))
                using (CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
                {
                    HttpResponseMessage response = await httpClient.GetAsync(new Uri(url)).AsTask(linkedCts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync().AsTask(linkedCts.Token);
                    }

                    System.Diagnostics.Debug.WriteLine($"Store catalogue error: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Store catalogue exception: {ex.Message}");
            }

            return null;
        }

        public void Dispose()
        {
            if (!disposed)
            {
                httpClient?.Dispose();
                disposed = true;
            }
        }
    }
}
