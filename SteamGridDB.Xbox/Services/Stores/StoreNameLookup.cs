using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Web.Http;

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
        // GOG/Epic name caches. LoadGameEntriesAsync in PrimaryWidget owns the "is this cached"
        // decision and reads/writes these directly, so they are internal rather than private.
        internal static readonly Dictionary<string, string> gogNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal static readonly Dictionary<string, string> epicNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Games found by name rather than by store ID. Misses are cached too: a miss walked the
        // whole result list to conclude nothing matched.
        private static readonly Dictionary<string, int> nameMatchCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, string> ubisoftGameLookupCache = null;

        // A second long-lived client, separate from PrimaryWidget's artwork-download one: this one
        // only ever talks to the three store name-lookup endpoints below, and a Services/Stores type
        // should not reach back into the UI class for a shared instance.
        private static readonly HttpClient httpClient = new HttpClient();

        /// <summary>
        /// Fetches game name from GOG API by GOG ID.
        /// </summary>
        /// <param name="gogId">The GOG game ID</param>
        /// <returns>Game name or null if not found</returns>
        internal static async Task<string> GetGogGameNameAsync(string gogId)
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
                        if (gameData.ContainsKey("_embedded") &&
                            gameData.GetNamedObject("_embedded").ContainsKey("product"))
                        {
                            JsonObject product = gameData.GetNamedObject("_embedded").GetNamedObject("product");

                            if (product.ContainsKey("title"))
                            {
                                return product.GetNamedString("title");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching GOG game name for {gogId}: {ex.Message}");
            }

            return null;
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

            if (nameMatchCache.TryGetValue(wanted, out int cached))
            {
                return cached;
            }

            int found = 0;

            try
            {
                foreach (SteamGridDbGame candidate in await client.SearchGameByNameAsync(gameName))
                {
                    if (NormaliseGameName(candidate.Name) == wanted)
                    {
                        found = candidate.Id;

                        break;
                    }
                }

                nameMatchCache[wanted] = found;
            }
            catch (Exception ex)
            {
                // Not cached - a failed request should be retried, unlike a genuine miss
                System.Diagnostics.Debug.WriteLine($"Could not search SteamGridDB for {gameName}: {ex.Message}");
            }

            return found;
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
        /// <returns>Game name, or null when the database does not have it.</returns>
        internal static async Task<string> GetEpicGameNameAsync(string epicId)
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
                        if (gameData.ContainsKey("title"))
                        {
                            return gameData.GetNamedString("title");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching Epic game name for {epicId}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Downloads and parses the Ubisoft game list from GitHub.
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        internal static async Task<bool> LoadUbisoftGameListAsync()
        {
            if (ubisoftGameLookupCache != null)
            {
                return true;
            }

            try
            {
                string url = "https://raw.githubusercontent.com/Haoose/UPLAY_GAME_ID/refs/heads/master/README.md";
                HttpResponseMessage response = await httpClient.GetAsync(new Uri(url));

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                string content = await response.Content.ReadAsStringAsync();
                string[] lines = content.Split('\n');

                // Built locally and only published once it has entries: caching an empty result would
                // make the early return above skip every later attempt for the rest of the session
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

                if (parsedGames.Count == 0)
                {
                    return false;
                }

                ubisoftGameLookupCache = parsedGames;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading Ubisoft game list: {ex.Message}");

                return false;
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
                await LoadUbisoftGameListAsync();

                if (ubisoftGameLookupCache != null && ubisoftGameLookupCache.TryGetValue(ubisoftId, out string gameName))
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
