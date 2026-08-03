using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

using SteamGridDB.Xbox.Services.SteamGridDB.Models;

namespace SteamGridDB.Xbox.Services.SteamGridDB
{
    /// <summary>
    /// Client for interacting with the SteamGridDB API.
    /// Documentation: https://www.steamgriddb.com/api/v2
    /// </summary>
    public class SteamGridDbClient : IDisposable
    {
        private readonly HttpClient httpClient;
        private readonly string baseUrl = "https://www.steamgriddb.com/api/v2";

        // Root for Valve's own store assets. The paths SteamGridDB reports under platformdata are
        // relative to "<appid>/", and the cloudflare-branded host redirects here.
        private const string steamStoreAssetsUrl = "https://shared.steamstatic.com/store_item_assets/steam/apps";

        // Icon sizes worth using for a tile. Smaller sizes exist down to 8px and are never wanted here.
        private static readonly string[] squareIconDimensions = { "128", "256", "512", "1024" };

        // Portrait box art, used only when a game has no square artwork at all and would otherwise
        // fall back to an icon. Cropped to a square before it becomes a tile.
        private static readonly string[] portraitGridDimensions = { "600x900", "342x482", "660x930" };

        private readonly TimeSpan timeout;
        private bool disposed = false;

        /// <summary>
        /// Initialises a new SteamGridDB client with API key.
        /// </summary>
        /// <param name="apiKey">SteamGridDB API key.</param>
        /// <param name="timeoutSeconds">Request timeout in seconds (default is 30).</param>
        public SteamGridDbClient(string apiKey, int timeoutSeconds = 30)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key is required", nameof(apiKey));
            }

            if (timeoutSeconds <= 0)
            {
                throw new ArgumentException("Timeout must be greater than 0", nameof(timeoutSeconds));
            }

            timeout = TimeSpan.FromSeconds(timeoutSeconds);

            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            httpClient.DefaultRequestHeaders.Accept.Add(new HttpMediaTypeWithQualityHeaderValue("application/json"));
        }

        /// <summary>
        /// Searches for a game by name.
        /// </summary>
        /// <param name="term">Search term.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of matching games.</returns>
        public async Task<List<SteamGridDbGame>> SearchGameByNameAsync(string term, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException("Search term cannot be empty", nameof(term));
            }

            var url = $"{baseUrl}/search/autocomplete/{Uri.EscapeDataString(term)}";
            var response = await GetAsync<SteamGridDbResponse<List<SteamGridDbGame>>>(url, cancellationToken);

            if (response != null && response.Success && response.Data != null)
            {
                return response.Data;
            }

            return new List<SteamGridDbGame>();
        }

        /// <summary>
        /// Gets game by platform-specific ID (e.g., Steam ID, GOG ID).
        /// </summary>
        /// <param name="platform">Platform type (steam, gog, epic, etc).</param>
        /// <param name="platformId">Platform-specific game ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Game information or null.</returns>
        public async Task<SteamGridDbGame> GetGameByPlatformIdAsync(string platform, string platformId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                throw new ArgumentException("Platform cannot be empty", nameof(platform));
            }

            if (string.IsNullOrWhiteSpace(platformId))
            {
                throw new ArgumentException("Platform ID cannot be empty", nameof(platformId));
            }

            // platformdata=steam asks SteamGridDB to attach Valve's own store asset manifest, which is
            // where OfficialCapsuleUrl comes from. It rides along on the lookup the widget already makes
            // to resolve the game's name, so it costs no extra request.
            var url = $"{baseUrl}/games/{platform}/{Uri.EscapeDataString(platformId)}?platformdata=steam";
            var json = await GetStringAsync(url, cancellationToken);

            if (json == null)
            {
                return null;
            }

            var response = DeserializeJson<SteamGridDbResponse<SteamGridDbGame>>(json);

            if (response == null || !response.Success || response.Data == null)
            {
                return null;
            }

            response.Data.OfficialCapsuleUrl = ParseOfficialCapsuleUrl(json);

            return response.Data;
        }

        /// <summary>
        /// Pulls Valve's official library-capsule URL out of a games response fetched with
        /// platformdata=steam. The paths sit under per-language keys that vary by game
        /// (external_platform_data.steam[0].metadata.library_capsule_full.image2x.{language}), which
        /// DataContractJsonSerializer cannot express, so this walks the document instead.
        /// Falls back to the header image, and returns null when Valve has neither.
        /// </summary>
        /// <param name="json">Raw games-endpoint response body.</param>
        private static string ParseOfficialCapsuleUrl(string json)
        {
            try
            {
                if (!JsonObject.TryParse(json, out JsonObject root))
                {
                    return null;
                }

                JsonObject data = root.GetNamedObject("data", null);
                JsonObject platformData = data?.GetNamedObject("external_platform_data", null);
                JsonArray steamEntries = platformData?.GetNamedArray("steam", null);

                if (steamEntries == null || steamEntries.Count == 0)
                {
                    return null;
                }

                JsonObject entry = steamEntries.GetObjectAt(0);
                string appId = entry.GetNamedString("id", null);
                JsonObject metadata = entry.GetNamedObject("metadata", null);

                if (string.IsNullOrEmpty(appId) || metadata == null)
                {
                    return null;
                }

                // Prefer the 2x capsule, then the 1x, then the store header
                JsonObject capsule = metadata.GetNamedObject("library_capsule_full", null);
                string path = FirstLocalisedValue(capsule?.GetNamedObject("image2x", null))
                    ?? FirstLocalisedValue(capsule?.GetNamedObject("image", null))
                    ?? FirstLocalisedValue(metadata.GetNamedObject("header_image_full", null));

                if (string.IsNullOrEmpty(path))
                {
                    return null;
                }

                return $"{steamStoreAssetsUrl}/{appId}/{path}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Could not read official capsule URL: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        /// Returns the first value of a {language: path} map, preferring English when it is present.
        /// </summary>
        private static string FirstLocalisedValue(JsonObject localised)
        {
            if (localised == null || localised.Count == 0)
            {
                return null;
            }

            foreach (string preferred in new[] { "english", "en" })
            {
                if (localised.ContainsKey(preferred))
                {
                    return localised.GetNamedString(preferred, null);
                }
            }

            foreach (var pair in localised)
            {
                if (pair.Value.ValueType == JsonValueType.String)
                {
                    return pair.Value.GetString();
                }
            }

            return null;
        }

        /// <summary>
        /// Gets grids (box art) for a game by platform ID.
        /// </summary>
        /// <param name="platform">Platform type (steam, gog, epic, etc).</param>
        /// <param name="platformId">Platform-specific game ID.</param>
        /// <param name="dimensions">Preferred dimensions (e.g., new[] { "600x900", "920x430" }). Use null for all sizes.</param>
        /// <param name="styles">Styles to filter by (e.g., new[] { "alternate", "white_logo" }). Use null for all styles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of available grids.</returns>
        public async Task<List<SteamGridDbGrid>> GetGridsByPlatformIdAsync(string platform, string platformId, string[] dimensions = null, string[] styles = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                throw new ArgumentException("Platform cannot be empty", nameof(platform));
            }

            if (string.IsNullOrWhiteSpace(platformId))
            {
                throw new ArgumentException("Platform ID cannot be empty", nameof(platformId));
            }

            var url = BuildUrl($"grids/{platform}/{Uri.EscapeDataString(platformId)}", dimensions, styles);
            var response = await GetAsync<SteamGridDbResponse<List<SteamGridDbGrid>>>(url, cancellationToken);

            if (response == null || !response.Success)
            {
                // Request failed. Null rather than an empty list so callers can tell "SteamGridDB has
                // no artwork for this game" from "we could not ask".
                return null;
            }

            return response.Data ?? new List<SteamGridDbGrid>();
        }

        /// <summary>
        /// Gets square grids (box art) for a game by platform ID - convenience method.
        /// </summary>
        /// <param name="platform">Platform type (steam, gog, epic, etc).</param>
        /// <param name="platformId">Platform-specific game ID.</param>
        /// <param name="styles">Styles to filter by. Use null for all styles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of square grids only.</returns>
        public async Task<List<SteamGridDbGrid>> GetSquareGridsByPlatformIdAsync(string platform, string platformId, string[] styles = null, CancellationToken cancellationToken = default)
        {
            return await GetGridsByPlatformIdAsync(platform, platformId, new[] { "512x512", "1024x1024" }, styles, cancellationToken);
        }

        /// <summary>
        /// Gets grids (box art) for a game by SteamGridDB game ID.
        /// </summary>
        /// <param name="gameId">SteamGridDB game ID.</param>
        /// <param name="dimensions">Preferred dimensions (e.g., new[] { "600x900", "920x430" }). Use null for all sizes.</param>
        /// <param name="styles">Styles to filter by (e.g., new[] { "alternate", "white_logo" }). Use null for all styles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of available grids.</returns>
        public async Task<List<SteamGridDbGrid>> GetGridsByGameIdAsync(int gameId, string[] dimensions = null, string[] styles = null, CancellationToken cancellationToken = default)
        {
            if (gameId <= 0)
            {
                throw new ArgumentException("Game ID must be greater than 0", nameof(gameId));
            }

            var url = BuildUrl($"grids/game/{gameId}", dimensions, styles);
            var response = await GetAsync<SteamGridDbResponse<List<SteamGridDbGrid>>>(url, cancellationToken);

            if (response == null || !response.Success)
            {
                // Request failed. Null rather than an empty list so callers can tell "SteamGridDB has
                // no artwork for this game" from "we could not ask".
                return null;
            }

            return response.Data ?? new List<SteamGridDbGrid>();
        }

        /// <summary>
        /// Gets square grids (box art) for a game by SteamGridDB game ID - convenience method.
        /// </summary>
        /// <param name="gameId">SteamGridDB game ID.</param>
        /// <param name="styles">Styles to filter by. Use null for all styles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of square grids only.</returns>
        public async Task<List<SteamGridDbGrid>> GetSquareGridsByGameIdAsync(int gameId, string[] styles = null, CancellationToken cancellationToken = default)
        {
            return await GetGridsByGameIdAsync(gameId, new[] { "512x512", "1024x1024" }, styles, cancellationToken);
        }

        /// <summary>
        /// Gets icons for a game by platform ID.
        /// </summary>
        /// <param name="platform">Platform type (steam, gog, epic, etc).</param>
        /// <param name="platformId">Platform-specific game ID.</param>
        /// <param name="dimensions">Preferred dimensions (e.g., new[] { "32", "64", "128" }). Use null for all sizes.</param>
        /// <param name="styles">Styles to filter by (e.g., new[] { "official", "custom" }). Use null for all styles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of available icons.</returns>
        public async Task<List<SteamGridDbGrid>> GetIconsByPlatformIdAsync(string platform, string platformId, string[] dimensions = null, string[] styles = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(platform))
            {
                throw new ArgumentException("Platform cannot be empty", nameof(platform));
            }

            if (string.IsNullOrWhiteSpace(platformId))
            {
                throw new ArgumentException("Platform ID cannot be empty", nameof(platformId));
            }

            var url = BuildUrl($"icons/{platform}/{Uri.EscapeDataString(platformId)}", dimensions, styles);
            var response = await GetAsync<SteamGridDbResponse<List<SteamGridDbGrid>>>(url, cancellationToken);

            if (response == null || !response.Success)
            {
                // Request failed. Null rather than an empty list so callers can tell "SteamGridDB has
                // no artwork for this game" from "we could not ask".
                return null;
            }

            return response.Data ?? new List<SteamGridDbGrid>();
        }

        /// <summary>
        /// Gets square icons for a game by platform ID - convenience method.
        /// Icons are typically square, but this ensures only 1:1 ratio icons are returned.
        /// </summary>
        /// <param name="platform">Platform type (steam, gog, epic, etc).</param>
        /// <param name="platformId">Platform-specific game ID.</param>
        /// <summary>
        /// Gets portrait box art for a game by platform ID - the shapes SteamGridDB uses for store
        /// capsules. Only useful once cropped to a square; see the tile crop in the widget.
        /// </summary>
        /// <param name="platform">Platform type (steam, gog, egs, etc).</param>
        /// <param name="platformId">Platform-specific game ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of portrait grids, empty when there are none, null when the request failed.</returns>
        public async Task<List<SteamGridDbGrid>> GetPortraitGridsByPlatformIdAsync(string platform, string platformId, CancellationToken cancellationToken = default)
        {
            return await GetGridsByPlatformIdAsync(platform, platformId, portraitGridDimensions, null, cancellationToken);
        }

        /// <param name="styles">Styles to filter by. Use null for all styles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of square icons only.</returns>
        public async Task<List<SteamGridDbGrid>> GetSquareIconsByPlatformIdAsync(string platform, string platformId, string[] styles = null, CancellationToken cancellationToken = default)
        {
            return await GetIconsByPlatformIdAsync(platform, platformId, squareIconDimensions, styles, cancellationToken);
        }

        /// <summary>
        /// Gets icons for a game by SteamGridDB game ID.
        /// </summary>
        /// <param name="gameId">SteamGridDB game ID.</param>
        /// <param name="dimensions">Preferred dimensions (e.g., new[] { "32", "64", "128" }). Use null for all sizes.</param>
        /// <param name="styles">Styles to filter by (e.g., new[] { "official", "custom" }). Use null for all styles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of available icons.</returns>
        public async Task<List<SteamGridDbGrid>> GetIconsByGameIdAsync(int gameId, string[] dimensions = null, string[] styles = null, CancellationToken cancellationToken = default)
        {
            if (gameId <= 0)
            {
                throw new ArgumentException("Game ID must be greater than 0", nameof(gameId));
            }

            var url = BuildUrl($"icons/game/{gameId}", dimensions, styles);
            var response = await GetAsync<SteamGridDbResponse<List<SteamGridDbGrid>>>(url, cancellationToken);

            if (response == null || !response.Success)
            {
                // Request failed. Null rather than an empty list so callers can tell "SteamGridDB has
                // no artwork for this game" from "we could not ask".
                return null;
            }

            return response.Data ?? new List<SteamGridDbGrid>();
        }

        /// <summary>
        /// Gets square icons for a game by SteamGridDB game ID - convenience method.
        /// Icons are typically square, but this ensures only 1:1 ratio icons are returned.
        /// </summary>
        /// <param name="gameId">SteamGridDB game ID.</param>
        /// <param name="styles">Styles to filter by. Use null for all styles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of square icons only.</returns>
        public async Task<List<SteamGridDbGrid>> GetSquareIconsByGameIdAsync(int gameId, string[] styles = null, CancellationToken cancellationToken = default)
        {
            return await GetIconsByGameIdAsync(gameId, squareIconDimensions, styles, cancellationToken);
        }

        /// <summary>
        /// Builds an API URL from a path and the optional dimensions/styles filters, plus the artwork
        /// filters every request should carry.
        /// The API expects them comma-separated: ?dimensions=600x900,920x430&amp;styles=alternate,white_logo
        /// </summary>
        /// <param name="path">Path below the API root, already escaped (e.g. "grids/steam/220").</param>
        /// <param name="dimensions">Dimensions to filter by, or null for all.</param>
        /// <param name="styles">Styles to filter by, or null for all.</param>
        private string BuildUrl(string path, string[] dimensions = null, string[] styles = null)
        {
            var urlBuilder = new StringBuilder($"{baseUrl}/{path}");
            var queryParams = new List<string>();

            if (dimensions != null && dimensions.Length > 0)
            {
                queryParams.Add($"dimensions={string.Join(",", dimensions.Select(Uri.EscapeDataString))}");
            }

            if (styles != null && styles.Length > 0)
            {
                queryParams.Add($"styles={string.Join(",", styles.Select(Uri.EscapeDataString))}");
            }

            // Artwork the widget must never install: an animated upload cannot be a static tile, and the
            // flagged categories are not what someone asking to fix their library is asking for. The API
            // already excludes nsfw and humor by default, so these change nothing today - they are here so
            // that a later upload in one of these categories cannot silently become a game's tile.
            queryParams.Add("types=static");
            queryParams.Add("nsfw=false");
            queryParams.Add("humor=false");
            queryParams.Add("epilepsy=false");

            urlBuilder.Append("?");
            urlBuilder.Append(string.Join("&", queryParams));

            return urlBuilder.ToString();
        }

        /// <summary>
        /// Generic GET request helper.
        /// </summary>
        private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken) where T : class
        {
            return DeserializeJson<T>(await GetStringAsync(url, cancellationToken));
        }

        /// <summary>
        /// GET returning the raw response body, for callers that need to read parts of the document
        /// the data contracts do not cover. Returns null on any non-success response.
        /// </summary>
        private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
        {
            try
            {
                var uri = new Uri(url);

                // Create a linked cancellation token source for timeout
                using (var timeoutCts = new System.Threading.CancellationTokenSource(timeout))
                using (var linkedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
                {
                    var response = await httpClient.GetAsync(uri).AsTask(linkedCts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        return await response.Content.ReadAsStringAsync().AsTask(linkedCts.Token);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"SteamGridDB API error: {response.StatusCode}");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    System.Diagnostics.Debug.WriteLine("SteamGridDB API request cancelled by user");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"SteamGridDB API request timed out after {timeout.TotalSeconds} seconds");
                }
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SteamGridDB API exception: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Deserialises JSON to object using DataContractJsonSerializer.
        /// </summary>
        private T DeserializeJson<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    return serializer.ReadObject(stream) as T;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"JSON deserialization error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Releases the resources used by the current instance of the class.
        /// </summary>
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
