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
        /// Maps an Xbox app ThirdPartyLibraries subfolder name to a platform. The Xbox app renamed these
        /// folders at some point and leaves the old ones behind, so both spellings are recognised
        /// ("Ubisoft" alongside "ubi", "BattleNet" alongside "bnet").
        /// </summary>
        public static GamePlatform FromXboxDirectory(string platformString)
        {
            // Invariant casing: the folder names are fixed ASCII identifiers, not user text
            switch (platformString?.ToLowerInvariant())
            {
                case "steam":
                    return GamePlatform.Steam;
                case "gog":
                    return GamePlatform.GOG;
                case "epic":
                    return GamePlatform.Epic;
                case "ubi":
                case "ubisoft":
                    return GamePlatform.Ubisoft;
                case "bnet":
                case "battlenet":
                    return GamePlatform.BattleNet;
                case "ea":
                    return GamePlatform.EA;
                case "customlibrarymanagement":
                    return GamePlatform.Custom;
                default:
                    return GamePlatform.Unknown;
            }
        }

        public static string GamePlatformToSGDBApiString(GamePlatform platform)
        {
            switch (platform)
            {
                case GamePlatform.Steam:
                    return "steam";
                case GamePlatform.GOG:
                    return "gog";
                case GamePlatform.Epic:
                    return "egs";
                case GamePlatform.Ubisoft:
                    return "uplay";
                case GamePlatform.BattleNet:
                    return "bnet";
                case GamePlatform.EA:
                    return "origin";
                default:
                    return null;
            }
        }
    }
}