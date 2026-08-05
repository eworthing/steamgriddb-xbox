using System.Collections.Generic;

namespace SteamGridDB.Xbox.Models
{
    public enum GamePlatform
    {
        Steam,
        GOG,
        Epic,
        Ubisoft,
        BattleNet,
        EA,
        Custom,
        Unknown
    }

    public class GamePlatformHelper
    {
        /// <summary>
        /// One row per platform, holding both directions this type converts: the Xbox
        /// ThirdPartyLibraries folder name and the SteamGridDB API identifier. Previously these were
        /// two independent switch statements over <see cref="GamePlatform"/> - nothing failed to
        /// compile if a new platform was added to one and forgotten in the other; both silently
        /// defaulted to Unknown/null instead. One table, one place to add a row.
        ///
        /// Custom has no SteamGridDB API identifier (SteamGridDB does not carry a "custom library"
        /// store) - null here, matching <see cref="GamePlatformToSGDBApiString"/>'s original default.
        /// </summary>
        private static readonly (GamePlatform Platform, string XboxDirectory, string SGDBApiString)[] platforms =
        {
            (GamePlatform.Steam, "steam", "steam"),
            (GamePlatform.GOG, "gog", "gog"),
            (GamePlatform.Epic, "epic", "egs"),
            (GamePlatform.Ubisoft, "ubisoft", "uplay"),
            (GamePlatform.BattleNet, "battlenet", "bnet"),
            (GamePlatform.EA, "ea", "origin"),
            (GamePlatform.Custom, "customlibrarymanagement", null),
        };

        /// <summary>
        /// Old Xbox ThirdPartyLibraries folder names the app renamed at some point, left behind
        /// alongside the current name in <see cref="platforms"/> above - has no analogue in the
        /// SteamGridDB direction, so it stays a separate, smaller list rather than a second column.
        /// </summary>
        private static readonly Dictionary<string, GamePlatform> legacyXboxDirectoryAliases = new Dictionary<string, GamePlatform>
        {
            ["ubi"] = GamePlatform.Ubisoft,
            ["bnet"] = GamePlatform.BattleNet,
        };

        /// <summary>
        /// Maps an Xbox app ThirdPartyLibraries subfolder name to a platform. The Xbox app renamed these
        /// folders at some point and leaves the old ones behind, so both spellings are recognised
        /// ("Ubisoft" alongside "ubi", "BattleNet" alongside "bnet").
        /// </summary>
        public static GamePlatform FromXboxDirectory(string platformString)
        {
            // Invariant casing: the folder names are fixed ASCII identifiers, not user text
            string normalised = platformString?.ToLowerInvariant();

            foreach (var row in platforms)
            {
                if (row.XboxDirectory == normalised)
                {
                    return row.Platform;
                }
            }

            if (normalised != null && legacyXboxDirectoryAliases.TryGetValue(normalised, out GamePlatform legacyPlatform))
            {
                return legacyPlatform;
            }

            return GamePlatform.Unknown;
        }

        public static string GamePlatformToSGDBApiString(GamePlatform platform)
        {
            foreach (var row in platforms)
            {
                if (row.Platform == platform)
                {
                    return row.SGDBApiString;
                }
            }

            return null;
        }
    }
}