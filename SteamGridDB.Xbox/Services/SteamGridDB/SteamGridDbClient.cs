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
        private const string baseUrl = "https://www.steamgriddb.com/api/v2";

        // Root for Valve's own store assets. The paths SteamGridDB reports under platformdata are
        // relative to "<appid>/", and the cloudflare-branded host redirects here.
        private const string steamStoreAssetsUrl = "https://shared.steamstatic.com/store_item_assets/steam/apps";

        // Icon sizes worth using for a tile. Smaller sizes exist down to 8px and are never wanted here.
        private static readonly string[] squareIconDimensions = { "128", "256", "512", "1024" };

        // The two square shapes SteamGridDB offers. Requested together, which is why ranking has to
        // pick the sharper of the two client-side.
        private static readonly string[] squareGridDimensions = { "512x512", "1024x1024" };

        // Portrait box art, used only when a game has no square artwork at all and would otherwise
        // fall back to an icon. Cropped to a square before it becomes a tile.
        private static readonly string[] portraitGridDimensions = { "600x900", "342x482", "660x930" };

        /// <summary>
        /// Where the first few capsule-URL parses gave up. The parse runs during the library load, long
        /// before any run log exists, and a null result is indistinguishable from a game Valve has no
        /// artwork for - which is how it went unnoticed that every game was failing.
        ///
        /// Exposed as a read-only view: the backing list is written only by
        /// <see cref="NoteCapsuleParse"/> under <see cref="capsuleParseNotesGate"/>, so a caller that
        /// held the mutable <see cref="List{T}"/> itself (the field's shape before this) could bypass
        /// that gate and mutate or clear it from outside.
        /// </summary>
        public static IReadOnlyList<string> CapsuleParseNotes => capsuleParseNotes;

        private static readonly List<string> capsuleParseNotes = new List<string>();
        private static readonly object capsuleParseNotesGate = new object();

        /// <summary>
        /// Records a parse-failure note, capped at 5. Reachable only through
        /// <see cref="ParseOfficialCapsuleUrl"/>'s single-threaded-per-load caller today, same as
        /// every other cross-file mutable cache this codebase gates anyway (StoreNameLookup's three
        /// caches, AppliedArtworkStore's Dictionary) - this one now matches them rather than being the
        /// one unsynchronized check-then-populate write left in the codebase.
        /// </summary>
        internal static void NoteCapsuleParse(string note)
        {
            lock (capsuleParseNotesGate)
            {
                if (capsuleParseNotes.Count < 5)
                {
                    capsuleParseNotes.Add(note);
                }
            }
        }

        private readonly TimeSpan timeout;
        private readonly RequestThrottle throttle = new RequestThrottle();
        private bool disposed = false;

        /// <summary>
        /// Whether this client has stopped asking because SteamGridDB kept refusing - see
        /// <see cref="RequestThrottle"/>. A bulk run should check this between games and stop rather
        /// than walk the rest of the library making requests that will not be answered.
        /// </summary>
        internal bool HasGivenUp => throttle.HasGivenUp;

        /// <summary>
        /// How many requests this client failed to get an answer to, over its whole life - refusals,
        /// server errors and dead connections alike, but not a 404, which is SteamGridDB answering
        /// clearly that it does not have the thing asked for.
        ///
        /// Snapshot it around a piece of work to find out whether any part of that work went
        /// unanswered. Every one of these comes back to the caller as the same null a genuine miss
        /// does, so without this counter there is no way to tell "SteamGridDB has no match for this
        /// game" from "SteamGridDB did not say" - and the first is worth caching for days while the
        /// second is worth caching for no time at all.
        /// </summary>
        internal int UnansweredResponses => unansweredResponses;

        private int unansweredResponses;

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
            AppIdentity.Identify(httpClient.DefaultRequestHeaders);
        }

        /// <summary>
        /// Searches for a game by name.
        /// </summary>
        /// <param name="term">Search term.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Matching games, empty when there are none, null when the request failed.</returns>
        public async Task<List<SteamGridDbGame>> SearchGameByNameAsync(string term, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException("Search term cannot be empty", nameof(term));
            }

            var url = $"{baseUrl}/search/autocomplete/{Uri.EscapeDataString(term)}";
            var response = await GetAsync<SteamGridDbResponse<List<SteamGridDbGame>>>(url, cancellationToken);

            // The same null contract GetArtworkListAsync keeps, and for the same reason: an empty list
            // returned for a refused or unreachable request is indistinguishable from SteamGridDB
            // saying it has no such game, so the manual search reported "No games found" for a request
            // that was never answered.
            if (response == null || !response.Success)
            {
                return null;
            }

            return response.Data ?? new List<SteamGridDbGame>();
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
            // platformdata=steam asks SteamGridDB to attach Valve's own store asset manifest, which is
            // where OfficialCapsuleUrl comes from. It rides along on the lookup the widget already makes
            // to resolve the game's name, so it costs no extra request.
            // The /games/ endpoint addresses a game the same way as /grids/ and /icons/ do - but only
            // for the platform form, so this reuses the construction rather than taking a source.
            var url = $"{baseUrl}/games/{ArtworkSource.ForPlatform(platform, platformId).Segment}?platformdata=steam";
            var json = await GetStringAsync(url, cancellationToken);
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
                    NoteCapsuleParse($"TryParse failed on {json?.Length ?? 0} chars");

                    return null;
                }

                JsonObject data = JsonRead.Object(root, "data");
                JsonObject platformData = JsonRead.Object(data, "external_platform_data");
                JsonArray steamEntries = JsonRead.Array(platformData, "steam");

                if (steamEntries == null || steamEntries.Count == 0)
                {
                    NoteCapsuleParse($"data={data != null}, external_platform_data={platformData != null}, steam={(steamEntries == null ? "null" : steamEntries.Count.ToString())}, keys=[{(data == null ? "" : string.Join(",", data.Keys))}]");

                    return null;
                }

                JsonObject entry = steamEntries.GetObjectAt(0);
                string appId = JsonRead.String(entry, "id");
                JsonObject metadata = JsonRead.Object(entry, "metadata");

                if (string.IsNullOrEmpty(appId) || metadata == null)
                {
                    NoteCapsuleParse($"entry keys=[{string.Join(",", entry.Keys)}] appId={appId ?? "null"} metadata={metadata != null}");

                    return null;
                }

                // Prefer the 2x capsule, then the 1x, then the store header
                JsonObject capsule = JsonRead.Object(metadata, "library_capsule_full");
                string path = FirstLocalisedValue(JsonRead.Object(capsule, "image2x"))
                    ?? FirstLocalisedValue(JsonRead.Object(capsule, "image"))
                    ?? FirstLocalisedValue(JsonRead.Object(metadata, "header_image_full"));

                if (string.IsNullOrEmpty(path))
                {
                    NoteCapsuleParse($"metadata keys=[{string.Join(",", metadata.Keys)}] capsule={capsule != null}");

                    return null;
                }

                return $"{steamStoreAssetsUrl}/{appId}/{path}";
            }
            catch (Exception ex)
            {
                NoteCapsuleParse($"threw: {ex.GetType().Name} {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Could not read official capsule URL: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        /// Returns the first value of a {language: path} map, preferring English when it is present.
        /// </summary>
        private static string FirstLocalisedValue(JsonObject localised)
        {
            if (localised == null)
            {
                return null;
            }

            string preferred = JsonRead.String(localised, "english") ?? JsonRead.String(localised, "en");

            if (preferred != null)
            {
                return preferred;
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
        /// Gets square box art, the shape the Xbox tile needs.
        /// </summary>
        /// <param name="source">Which game to fetch for.</param>
        /// <param name="styles">Styles to filter by, or null for all.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Candidates, empty when there are none, null when the request failed.</returns>
        public async Task<List<SteamGridDbGrid>> GetSquareGridsAsync(ArtworkSource source, string[] styles = null, CancellationToken cancellationToken = default)
        {
            return await GetArtworkListAsync(BuildUrl($"grids/{source.Segment}", squareGridDimensions, styles), cancellationToken);
        }

        /// <summary>
        /// Gets portrait box art, used only when a game has no square artwork at all. Cropped to a
        /// square before it becomes a tile.
        /// </summary>
        /// <param name="source">Which game to fetch for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Candidates, empty when there are none, null when the request failed.</returns>
        public async Task<List<SteamGridDbGrid>> GetPortraitGridsAsync(ArtworkSource source, CancellationToken cancellationToken = default)
        {
            return await GetArtworkListAsync(BuildUrl($"grids/{source.Segment}", portraitGridDimensions, null), cancellationToken);
        }

        /// <summary>
        /// Gets icons, the last resort when a game has no box art in any shape.
        /// </summary>
        /// <param name="source">Which game to fetch for.</param>
        /// <param name="styles">Styles to filter by, or null for all.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Candidates, empty when there are none, null when the request failed.</returns>
        public async Task<List<SteamGridDbGrid>> GetSquareIconsAsync(ArtworkSource source, string[] styles = null, CancellationToken cancellationToken = default)
        {
            return await GetArtworkListAsync(BuildUrl($"icons/{source.Segment}", squareIconDimensions, styles), cancellationToken);
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
        /// Fetches a list of artwork. Returns null when the request itself failed and an empty list
        /// when SteamGridDB simply has none, so callers can tell "there is no artwork for this game"
        /// from "we could not ask" - a throttled run would otherwise look like an empty library.
        /// </summary>
        /// <param name="url">Fully built request URL.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        private async Task<List<SteamGridDbGrid>> GetArtworkListAsync(string url, CancellationToken cancellationToken)
        {
            var response = await GetAsync<SteamGridDbResponse<List<SteamGridDbGrid>>>(url, cancellationToken);

            if (response == null || !response.Success)
            {
                return null;
            }

            return response.Data ?? new List<SteamGridDbGrid>();
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
        ///
        /// Also where the client's manners live: a refused response is recorded and paces the next
        /// request rather than being treated as an ordinary failure, and once
        /// <see cref="RequestThrottle.GiveUpAfterConsecutive"/> refusals have arrived in a row this
        /// stops issuing requests altogether. See <see cref="RequestThrottle"/>; callers notice via
        /// <see cref="HasGivenUp"/>.
        /// </summary>
        private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
        {
            if (throttle.HasGivenUp)
            {
                // Counted, because a request never sent is as unanswered as one that failed - and a
                // library load that continued past this point would otherwise cache a miss for every
                // remaining game. Not logged per request: the run that hit the limit already said so.
                unansweredResponses++;

                return null;
            }

            try
            {
                var uri = new Uri(url);

                // Before the timeout clock starts, not inside it: the backoff can legitimately be
                // longer than a request timeout, and waiting it out under the timeout token would
                // cancel the wait rather than the request it is pacing
                TimeSpan backoff = throttle.WaitBefore(DateTimeOffset.UtcNow);

                if (backoff > TimeSpan.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"SteamGridDB asked us to wait; holding {backoff.TotalSeconds:F0}s");

                    await Task.Delay(backoff, cancellationToken);
                }

                // Create a linked cancellation token source for timeout
                using (var timeoutCts = new System.Threading.CancellationTokenSource(timeout))
                using (var linkedCts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token))
                {
                    var response = await httpClient.GetAsync(uri).AsTask(linkedCts.Token);

                    if (response.IsSuccessStatusCode)
                    {
                        throttle.ObserveServed();

                        return await response.Content.ReadAsStringAsync().AsTask(linkedCts.Token);
                    }
                    else if (RequestThrottle.IsThrottled((int)response.StatusCode))
                    {
                        response.Headers.TryGetValue("Retry-After", out string retryAfter);
                        throttle.ObserveThrottled(retryAfter, DateTimeOffset.UtcNow);
                        unansweredResponses++;

                        System.Diagnostics.Debug.WriteLine(
                            $"SteamGridDB is rate limiting us: {response.StatusCode}, Retry-After: {retryAfter ?? "none"}"
                            + $" ({throttle.Consecutive} in a row{(throttle.HasGivenUp ? ", giving up" : string.Empty)})");
                    }
                    else
                    {
                        // A 404 or a bad request is the service answering, not refusing - it says
                        // nothing about how often we are asking, so the streak starts over
                        throttle.ObserveServed();

                        // ...but only a 404 is an answer about the game. SteamGridDB returns one for a
                        // game it does not carry, which is exactly the fact worth remembering; a 500 or
                        // a bad gateway reaches the caller as the same null and must not be
                        if ((int)response.StatusCode != 404)
                        {
                            unansweredResponses++;
                        }

                        System.Diagnostics.Debug.WriteLine($"SteamGridDB API error: {response.StatusCode}");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                // Counted before the rethrow, like every other outcome that is not the service
                // answering. A caller that swallows this exception - StoreNameLookup.FindGameByNameAsync
                // does, returning "no match" - would otherwise leave this counter untouched, and
                // GameMatchResolver.ShouldRemember reads an unchanged counter as "SteamGridDB answered",
                // so a single timeout was written into GameMatchCache as a confirmed miss and kept for
                // MissLifetime. That is precisely the "bad minute becomes days of Unknown" outcome the
                // counter exists to prevent.
                unansweredResponses++;

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
                // A dead connection or a malformed URI - no answer, and swallowed rather than thrown,
                // so this counter is the only trace of it the caller will ever see
                unansweredResponses++;

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
