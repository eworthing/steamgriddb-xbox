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
        Xbox,
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
        ///
        /// Xbox is null in both columns. It has no ThirdPartyLibraries folder at all - those games come
        /// from the Microsoft Store catalog and the Xbox app renders them from its own image cache, so
        /// they are enumerated rather than read out of a manifest - and SteamGridDB carries no Microsoft
        /// Store platform, so like Custom they are matched by name instead of by store ID.
        ///
        /// CarriesOwnPaths is true only for Custom: a Custom entry's manifest carries absolute paths of
        /// its own (its image path, and its install location/executable name in place of a store ID),
        /// where every other platform derives those from the entry ID and the manifest folder instead.
        /// Read by <see cref="CarriesOwnPaths"/>, whose two call sites - ManifestEntryIdentity and
        /// ManifestEntryImage - used to each write this same fact as a bare `platform == GamePlatform.Custom`.
        /// </summary>
        private static readonly (GamePlatform Platform, string XboxDirectory, string SGDBApiString, bool CarriesOwnPaths)[] platforms =
        {
            (GamePlatform.Steam, "steam", "steam", false),
            (GamePlatform.GOG, "gog", "gog", false),
            (GamePlatform.Epic, "epic", "egs", false),
            (GamePlatform.Ubisoft, "ubisoft", "uplay", false),
            (GamePlatform.BattleNet, "battlenet", "bnet", false),
            (GamePlatform.EA, "ea", "origin", false),
            (GamePlatform.Custom, "customlibrarymanagement", null, true),
            (GamePlatform.Xbox, null, null, false),
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
            // Checked before the table is walked, not folded into it: Xbox's XboxDirectory is null
            // because those games have no ThirdPartyLibraries folder, so a null argument would match
            // that row and quietly turn every unreadable folder name into an Xbox game
            if (platformString == null)
            {
                return GamePlatform.Unknown;
            }

            // Invariant casing: the folder names are fixed ASCII identifiers, not user text
            string normalised = platformString.ToLowerInvariant();

            foreach (var row in platforms)
            {
                if (row.XboxDirectory == normalised)
                {
                    return row.Platform;
                }
            }

            if (legacyXboxDirectoryAliases.TryGetValue(normalised, out GamePlatform legacyPlatform))
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

        /// <summary>
        /// Whether this platform's manifest entries carry their own absolute paths, rather than having
        /// their identifiers and image path derived from the entry ID and the manifest folder the way
        /// every other platform's entries are. True only for Custom.
        /// </summary>
        public static bool CarriesOwnPaths(GamePlatform platform)
        {
            foreach (var row in platforms)
            {
                if (row.Platform == platform)
                {
                    return row.CarriesOwnPaths;
                }
            }

            return false;
        }
    }
}