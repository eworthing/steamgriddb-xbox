using System;

namespace SteamGridDB.Xbox.Services.SteamGridDB
{
    /// <summary>
    /// Which game to fetch artwork for.
    ///
    /// SteamGridDB accepts either a store's own identifier (grids/steam/220) or its internal game ID
    /// (grids/game/1234), and the two differ only in that path segment. Games matched by name have no
    /// store identifier the API recognises - SteamGridDB's entry for Alan Wake 2, for instance, is
    /// linked to no store at all - so every artwork call has to work either way.
    /// </summary>
    public sealed class ArtworkSource
    {
        private ArtworkSource(string segment)
        {
            Segment = segment;
        }

        /// <summary>
        /// Path segment identifying the game, already escaped.
        /// </summary>
        internal string Segment
        {
            get;
        }

        /// <summary>
        /// Artwork for a game as the store knows it.
        /// </summary>
        /// <param name="platform">SteamGridDB platform key (steam, gog, egs, uplay).</param>
        /// <param name="platformId">The store's own game ID.</param>
        public static ArtworkSource ForPlatform(string platform, string platformId)
        {
            // Escaping matters: custom entries carry a full file path as their ID
            return new ArtworkSource($"{platform}/{Uri.EscapeDataString(platformId)}");
        }

        /// <summary>
        /// Artwork for a game as SteamGridDB knows it, for games reached by name rather than by store ID.
        /// </summary>
        /// <param name="gameId">SteamGridDB game ID.</param>
        public static ArtworkSource ForGame(int gameId)
        {
            if (gameId <= 0)
            {
                throw new ArgumentException("Game ID must be greater than 0", nameof(gameId));
            }

            return new ArtworkSource($"game/{gameId}");
        }
    }
}
