using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Web.Http;

using SteamGridDB.Xbox.Services;
using SteamGridDB.Xbox.Services.SteamGridDB;
using SteamGridDB.Xbox.Services.SteamGridDB.Models;

namespace SteamGridDB.Xbox.Services.Stores
{
    /// <summary>
    /// Resolves a third-party game's display name from each store's own API or community database,
    /// for entries the Xbox app records with no name of its own. Also finds the SteamGridDB game ID
    /// for entries whose store ID SteamGridDB does not recognise, by searching on the resolved name.
    ///
    /// The library reloads on every widget open, so every cache here is shared across the whole
    /// process and lives for as long as the widget does - without it, each reload would repeat the
    /// same lookups for the same unmatched games.
    /// </summary>
    internal static class StoreNameLookup
    {
        // GetOrFetchGogNameAsync, GetOrFetchEpicNameAsync and FindGameByNameAsync below each own the
        // "is this cached" decision for their store, matching GetUbisoftGameNameAsync's shape - and
        // each guards its whole check-then-populate body, the read included, with its own dedicated
        // gate immediately below its cache. A dedicated gate per cache, not one shared gate: the three
        // caches hold unrelated per-game data, so serialising them behind a single lock would block
        // one store's lookup on a completely different store's network round trip for no reason - the
        // same per-cache granularity AppliedArtworkStore's own single Dictionary already uses one
        // dedicated gate for.
        private static readonly Dictionary<string, string> gogNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim gogNameGate = new SemaphoreSlim(1, 1);
        private static readonly Dictionary<string, string> epicNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim epicNameGate = new SemaphoreSlim(1, 1);

        // Games found by name rather than by store ID. Misses are cached too: a miss walked the
        // whole result list to conclude nothing matched.
        private static readonly Dictionary<string, int> nameMatchCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static readonly SemaphoreSlim nameMatchGate = new SemaphoreSlim(1, 1);

        // Unlike the three caches above, this one is a single value loaded once rather than a
        // per-key lookup - the exact shape EpicLibrary's and AppliedArtworkStore's own caches have,
        // so this uses their same AsyncLazyCache<T> instead of a fourth hand-rolled copy of the same
        // check-then-populate logic.
        private static readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private static readonly AsyncLazyCache<Dictionary<string, string>> ubisoftGameListCache =
            new AsyncLazyCache<Dictionary<string, string>>(gate, LoadUbisoftGameListFromWebAsync);

        // A second long-lived client, separate from PrimaryWidget's artwork-download one: this one
        // only ever talks to the three store name-lookup endpoints below, and a Services/Stores type
        // should not reach back into the UI class for a shared instance.
        private static readonly HttpClient httpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();

            AppIdentity.Identify(client.DefaultRequestHeaders);

            return client;
        }

        /// <summary>
        /// What a store said about a name, keeping "there is no such game" apart from "the store could
        /// not be reached".
        ///
        /// The two used to be the same null, and the caches could only tell them apart by refusing to
        /// remember either - so a game GOG or the Epic database genuinely does not carry was looked up
        /// again on every single library load, forever, and always got the same answer. That is the
        /// worst kind of request to repeat: guaranteed to be useless, and aimed at services that are
        /// doing this app a favour by answering at all.
        /// </summary>
        internal readonly struct NameLookup
        {
            private NameLookup(string name, bool answered)
            {
                Name = name;
                Answered = answered;
            }

            /// <summary>The name, or null when the store has none for this ID.</summary>
            internal string Name { get; }

            /// <summary>
            /// Whether the store actually answered. False means the request failed, which is worth
            /// retrying; true with a null <see cref="Name"/> is a real answer and worth remembering.
            /// </summary>
            internal bool Answered { get; }

            /// <summary>The store answered, with a name or with nothing.</summary>
            internal static NameLookup Answer(string name)
            {
                return new NameLookup(name, true);
            }

            /// <summary>The store could not be reached, or failed in a way that may not repeat.</summary>
            internal static NameLookup Unavailable => new NameLookup(null, false);
        }

        /// <summary>
        /// Fetches game name from GOG API by GOG ID.
        /// </summary>
        /// <param name="gogId">The GOG game ID</param>
        /// <returns>What GOG said - see <see cref="NameLookup"/>.</returns>
        internal static async Task<NameLookup> GetGogGameNameAsync(string gogId)
        {
            try
            {
                string url = $"https://api.gog.com/v2/games/{gogId}";
                HttpResponseMessage response = await httpClient.GetAsync(new Uri(url));

                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();

                    if (JsonObject.TryParse(jsonContent, out JsonObject gameData))
                    {
                        JsonObject embedded = JsonRead.Object(gameData, "_embedded");
                        JsonObject product = JsonRead.Object(embedded, "product");

                        // A parsed body with no title is still an answer: GOG has this product and it
                        // has no name we can use, which asking again will not change
                        return NameLookup.Answer(JsonRead.String(product, "title"));
                    }

                    // A 200 that will not parse is not an answer about the game
                    return NameLookup.Unavailable;
                }

                // Only "no such product" is a fact about the game. A 429, a 500 or a gateway error is
                // a fact about GOG's afternoon, and caching it would make this ID permanently nameless
                return response.StatusCode == HttpStatusCode.NotFound
                    ? NameLookup.Answer(null)
                    : NameLookup.Unavailable;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching GOG game name for {gogId}: {ex.Message}");

                return NameLookup.Unavailable;
            }
        }

        /// <summary>
        /// Resolves a GOG game's name, using the cached value when one is on file. See
        /// <see cref="GetOrFetchNameAsync"/> for the shared check-then-populate shape.
        /// </summary>
        /// <param name="gogId">The GOG game ID.</param>
        /// <returns>Game name, or null when GOG has none for it.</returns>
        internal static Task<string> GetOrFetchGogNameAsync(string gogId)
        {
            return GetOrFetchNameAsync(gogNameCache, gogNameGate, gogId, () => GetGogGameNameAsync(gogId));
        }

        /// <summary>
        /// Shared check-then-populate skeleton for <see cref="GetOrFetchGogNameAsync"/> and
        /// <see cref="GetOrFetchEpicNameAsync"/>: await the store's own gate, check the cache under
        /// it, fetch, and cache whatever the store actually answered.
        ///
        /// A cached null is a remembered "this store has no name for that ID", not an empty slot. That
        /// is the whole point of <see cref="NameLookup"/>: caching only non-empty names, as this did
        /// before, meant every genuine miss was re-fetched on every library load for the life of the
        /// install. Presence in the dictionary is now the cache hit, so the check is
        /// <c>TryGetValue</c> alone rather than <c>TryGetValue</c> plus a non-empty test.
        ///
        /// The check happens under the gate rather than in front of it. A double-checked version of
        /// this - unlocked read, gate, re-read - is what <see cref="AsyncLazyCache{T}.GetOrLoadAsync"/>
        /// does, but that reads a single reference, which is atomic; this reads a
        /// <see cref="Dictionary{TKey, TValue}"/>, which is not safe to read while another caller is
        /// writing it. A write that resizes replaces the buckets and entries arrays, and a reader mid
        /// probe can follow a stale bucket into the new entries and return the wrong name, throw, or
        /// spin. The gate is uncontended whenever nothing is fetching, which is the common case, so
        /// the pre-check bought nothing worth that.
        ///
        /// <see cref="FindGameByNameAsync"/> is still deliberately NOT routed through this: its cached
        /// value is an int rather than a string, and what makes its answer trustworthy is a check on
        /// the SteamGridDB client's unanswered-request counter that has no analogue for GOG or Epic.
        /// </summary>
        /// <param name="cache">The store's own name cache.</param>
        /// <param name="gate">The store's own dedicated gate.</param>
        /// <param name="key">Cache key.</param>
        /// <param name="fetch">Fetches the name when neither check finds a cached value.</param>
        /// <returns>Cached or freshly fetched name, or null when the store has none.</returns>
        private static async Task<string> GetOrFetchNameAsync(Dictionary<string, string> cache, SemaphoreSlim gate, string key, Func<Task<NameLookup>> fetch)
        {
            await gate.WaitAsync();

            try
            {
                if (cache.TryGetValue(key, out string cached))
                {
                    return cached;
                }

                NameLookup looked = await fetch();

                if (looked.Answered)
                {
                    cache[key] = looked.Name;
                }

                return looked.Name;
            }
            finally
            {
                gate.Release();
            }
        }

        /// <summary>
        /// Finds a game on SteamGridDB by name, for entries whose store ID it does not recognise.
        ///
        /// Only an exact match after normalisation is accepted. Search returns near-misses readily -
        /// "Alan Wake 2" also brings back Alan Wake, Alan Wake Remastered and Alan Wake's American
        /// Nightmare - and artwork for the wrong game is worse than no artwork, because nothing about
        /// the result says it is wrong.
        /// </summary>
        /// <param name="client">Client to search with.</param>
        /// <param name="gameName">Name as the store knows it.</param>
        /// <returns>SteamGridDB game ID, or 0 when nothing matches closely enough.</returns>
        internal static async Task<int> FindGameByNameAsync(SteamGridDbClient client, string gameName)
        {
            string wanted = NormaliseGameName(gameName);

            await nameMatchGate.WaitAsync();

            try
            {
                if (nameMatchCache.TryGetValue(wanted, out int cached))
                {
                    return cached;
                }

                int found = 0;
                int unansweredBefore = client.UnansweredResponses;

                try
                {
                    // Null is the client's "could not ask" - an empty list is a real, cacheable miss
                    foreach (SteamGridDbGame candidate in await client.SearchGameByNameAsync(gameName)
                        ?? Enumerable.Empty<SteamGridDbGame>())
                    {
                        if (NormaliseGameName(candidate.Name) == wanted)
                        {
                            found = candidate.Id;

                            break;
                        }
                    }

                    // A search that was refused or failed does not throw, so the catch below does not
                    // see it. The counter is what separates it from a genuine miss: caching a refusal
                    // would make a moment of rate limiting into a game that stays unmatched for the
                    // rest of the session.
                    if (client.UnansweredResponses == unansweredBefore)
                    {
                        nameMatchCache[wanted] = found;
                    }
                }
                catch (Exception ex)
                {
                    // Not cached - a failed request should be retried, unlike a genuine miss
                    System.Diagnostics.Debug.WriteLine($"Could not search SteamGridDB for {gameName}: {ex.Message}");
                }

                return found;
            }
            finally
            {
                nameMatchGate.Release();
            }
        }

        /// <summary>
        /// Reduces a title to what two stores would agree on: case, punctuation and trademark symbols
        /// all vary between them ("Rocket League&#174;" against "Rocket League").
        /// </summary>
        internal static string NormaliseGameName(string name)
        {
            return name == null
                ? string.Empty
                : new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        }

        /// <summary>
        /// Fetches an Epic game's name from a community database, for games the local Epic manifests
        /// do not cover - a title uninstalled from Epic while its Xbox entry lingers, for instance.
        /// Keyed by catalog item ID, not by the appName SteamGridDB wants.
        /// </summary>
        /// <param name="epicId">Epic catalog item ID.</param>
        /// <returns>What the database said - see <see cref="NameLookup"/>.</returns>
        internal static async Task<NameLookup> GetEpicGameNameAsync(string epicId)
        {
            try
            {
                string url = $"https://raw.githubusercontent.com/nachoaldamav/items-tracker/refs/heads/main/database/items/{epicId}.json";
                HttpResponseMessage response = await httpClient.GetAsync(new Uri(url));

                if (response.IsSuccessStatusCode)
                {
                    string jsonContent = await response.Content.ReadAsStringAsync();

                    if (JsonObject.TryParse(jsonContent, out JsonObject gameData))
                    {
                        return NameLookup.Answer(JsonRead.String(gameData, "title"));
                    }

                    return NameLookup.Unavailable;
                }

                // The database is a directory of files in a git repository, so a 404 is simply "not in
                // it" - the flat, permanent answer worth remembering. GitHub's own rate limiting and
                // outages come back as other codes and must not be
                return response.StatusCode == HttpStatusCode.NotFound
                    ? NameLookup.Answer(null)
                    : NameLookup.Unavailable;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching Epic game name for {epicId}: {ex.Message}");

                return NameLookup.Unavailable;
            }
        }

        /// <summary>
        /// Resolves an Epic game's name, using the cached value when one is on file. Tries Epic's own
        /// install manifests first, then the community database, matching the order
        /// LoadGameEntriesAsync used inline before this fold. See <see cref="GetOrFetchNameAsync"/> for
        /// the shared check-then-populate shape.
        /// </summary>
        /// <param name="appName">Epic app name - the last segment of the Xbox entry ID. Also the cache key.</param>
        /// <param name="catalogItemId">Epic catalog item ID - the third segment, or null when absent.</param>
        /// <returns>Game name, or null when neither source has it.</returns>
        internal static Task<string> GetOrFetchEpicNameAsync(string appName, string catalogItemId)
        {
            // Epic's own install manifests first: they are local, they carry the real title, and they
            // cover games the online database does not. The database is keyed by catalog item ID, not
            // by the appName SteamGridDB wants.
            return GetOrFetchNameAsync(epicNameCache, epicNameGate, appName, async () =>
            {
                string installed = await EpicLibrary.GetDisplayNameAsync(appName, catalogItemId);

                // A local manifest that names the game is as answered as an answer gets - no request
                // is made at all, and the file is not going to say something different next time
                return string.IsNullOrEmpty(installed)
                    ? await GetEpicGameNameAsync(catalogItemId ?? appName)
                    : NameLookup.Answer(installed);
            });
        }

        /// <summary>
        /// Downloads and parses the Ubisoft game list from GitHub. Loaded through
        /// <see cref="ubisoftGameListCache"/>, which runs this at most once and does not cache a
        /// failed or empty parse - built locally and only published once it has entries, so caching
        /// an empty result would make every later lookup this session skip retrying.
        /// </summary>
        /// <returns>ID-to-name map, or null when the fetch failed or found no entries.</returns>
        private static async Task<Dictionary<string, string>> LoadUbisoftGameListFromWebAsync()
        {
            try
            {
                string url = "https://raw.githubusercontent.com/Haoose/UPLAY_GAME_ID/refs/heads/master/README.md";
                HttpResponseMessage response = await httpClient.GetAsync(new Uri(url));

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                string content = await response.Content.ReadAsStringAsync();
                string[] lines = content.Split('\n');

                Dictionary<string, string> parsedGames = new Dictionary<string, string>();

                foreach (string line in lines)
                {
                    string trimmedLine = line.Trim();

                    if (string.IsNullOrEmpty(trimmedLine))
                    {
                        continue;
                    }

                    // Format: "232 - Beyond Good and Evil™"
                    int dashIndex = trimmedLine.IndexOf(" - ");

                    if (dashIndex > 0)
                    {
                        string idPart = trimmedLine.Substring(0, dashIndex).Trim();
                        string namePart = trimmedLine.Substring(dashIndex + 3).Trim();

                        if (!string.IsNullOrEmpty(idPart) && !string.IsNullOrEmpty(namePart))
                        {
                            parsedGames[idPart] = namePart;
                        }
                    }
                }

                return parsedGames.Count == 0 ? null : parsedGames;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading Ubisoft game list: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        /// Fetches game name from cached Ubisoft game list by Ubisoft ID.
        /// </summary>
        /// <param name="ubisoftId">The Ubisoft game ID</param>
        /// <returns>Game name or null if not found</returns>
        internal static async Task<string> GetUbisoftGameNameAsync(string ubisoftId)
        {
            try
            {
                Dictionary<string, string> games = await ubisoftGameListCache.GetOrLoadAsync();

                if (games != null && games.TryGetValue(ubisoftId, out string gameName))
                {
                    return gameName;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching Ubisoft game name for {ubisoftId}: {ex.Message}");
            }

            return null;
        }
    }
}
