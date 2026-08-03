using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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

            var url = $"{baseUrl}/games/{platform}/{Uri.EscapeDataString(platformId)}";
            var response = await GetAsync<SteamGridDbResponse<SteamGridDbGame>>(url, cancellationToken);

            if (response != null && response.Success)
            {
                return response.Data;
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

            if (response != null && response.Success && response.Data != null)
            {
                return response.Data;
            }

            return new List<SteamGridDbGrid>();
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

            if (response != null && response.Success && response.Data != null)
            {
                return response.Data;
            }

            return new List<SteamGridDbGrid>();
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

            if (response != null && response.Success && response.Data != null)
            {
                return response.Data;
            }

            return new List<SteamGridDbGrid>();
        }

        /// <summary>
        /// Gets square icons for a game by platform ID - convenience method.
        /// Icons are typically square, but this ensures only 1:1 ratio icons are returned.
        /// </summary>
        /// <param name="platform">Platform type (steam, gog, epic, etc).</param>
        /// <param name="platformId">Platform-specific game ID.</param>
        /// <param name="dimensions">Preferred dimensions (e.g., new[] { "32", "64", "128" }). Use null for all sizes.</param>
        /// <param name="styles">Styles to filter by. Use null for all styles.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of square icons only.</returns>
        public async Task<List<SteamGridDbGrid>> GetSquareIconsByPlatformIdAsync(string platform, string platformId, string[] dimensions = null, string[] styles = null, CancellationToken cancellationToken = default)
        {
            return await GetIconsByPlatformIdAsync(platform, platformId, new[] { "128", "256", "512", "1024" }, styles, cancellationToken);
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

            if (response != null && response.Success && response.Data != null)
            {
                return response.Data;
            }

            return new List<SteamGridDbGrid>();
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
            return await GetIconsByGameIdAsync(gameId, new[] { "128", "256", "512", "1024" }, styles, cancellationToken);
        }

        /// <summary>
        /// Builds an API URL from a path and the optional dimensions/styles filters.
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

            if (queryParams.Count > 0)
            {
                urlBuilder.Append("?");
                urlBuilder.Append(string.Join("&", queryParams));
            }

            return urlBuilder.ToString();
        }

        /// <summary>
        /// Generic GET request helper.
        /// </summary>
        private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken) where T : class
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
                        var content = await response.Content.ReadAsStringAsync().AsTask(linkedCts.Token);
                        return DeserializeJson<T>(content);
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
