using SteamGridDB.Xbox.Models;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// GamePlatformHelper used to hold two independent switch statements over GamePlatform - one per
    /// direction it converts - with no shared source of truth: nothing failed to compile if a platform
    /// was added to one and forgotten in the other. Folded into one table; these tests pin every row's
    /// value in both directions, plus the legacy-alias and not-found paths a table lookup could get
    /// wrong in ways a switch's compiler-checked cases would not (a typo'd key silently returns Unknown
    /// or null instead of failing to compile).
    /// </summary>
    public class GamePlatformHelperTests
    {
        [Theory]
        [InlineData("steam", GamePlatform.Steam)]
        [InlineData("gog", GamePlatform.GOG)]
        [InlineData("epic", GamePlatform.Epic)]
        [InlineData("ubisoft", GamePlatform.Ubisoft)]
        [InlineData("battlenet", GamePlatform.BattleNet)]
        [InlineData("ea", GamePlatform.EA)]
        [InlineData("customlibrarymanagement", GamePlatform.Custom)]
        public void FromXboxDirectory_maps_current_folder_names(string folderName, GamePlatform expected)
        {
            Assert.Equal(expected, GamePlatformHelper.FromXboxDirectory(folderName));
        }

        [Theory]
        [InlineData("ubi", GamePlatform.Ubisoft)]
        [InlineData("bnet", GamePlatform.BattleNet)]
        public void FromXboxDirectory_maps_legacy_folder_names(string folderName, GamePlatform expected)
        {
            Assert.Equal(expected, GamePlatformHelper.FromXboxDirectory(folderName));
        }

        [Theory]
        [InlineData("STEAM")]
        [InlineData("Steam")]
        [InlineData("UBI")]
        public void FromXboxDirectory_is_case_insensitive(string folderName)
        {
            Assert.NotEqual(GamePlatform.Unknown, GamePlatformHelper.FromXboxDirectory(folderName));
        }

        [Fact]
        public void FromXboxDirectory_returns_Unknown_for_null()
        {
            Assert.Equal(GamePlatform.Unknown, GamePlatformHelper.FromXboxDirectory(null));
        }

        [Fact]
        public void FromXboxDirectory_returns_Unknown_for_unrecognised_name()
        {
            Assert.Equal(GamePlatform.Unknown, GamePlatformHelper.FromXboxDirectory("origin-launcher"));
        }

        [Theory]
        [InlineData(GamePlatform.Steam, "steam")]
        [InlineData(GamePlatform.GOG, "gog")]
        [InlineData(GamePlatform.Epic, "egs")]
        [InlineData(GamePlatform.Ubisoft, "uplay")]
        [InlineData(GamePlatform.BattleNet, "bnet")]
        [InlineData(GamePlatform.EA, "origin")]
        public void GamePlatformToSGDBApiString_maps_known_platforms(GamePlatform platform, string expected)
        {
            Assert.Equal(expected, GamePlatformHelper.GamePlatformToSGDBApiString(platform));
        }

        [Fact]
        public void GamePlatformToSGDBApiString_returns_null_for_Custom()
        {
            // Custom is a real row (it has an Xbox folder name), but SteamGridDB has no "custom
            // library" store to search - distinct from Unknown below, which is not a row at all.
            Assert.Null(GamePlatformHelper.GamePlatformToSGDBApiString(GamePlatform.Custom));
        }

        [Fact]
        public void GamePlatformToSGDBApiString_returns_null_for_Unknown()
        {
            Assert.Null(GamePlatformHelper.GamePlatformToSGDBApiString(GamePlatform.Unknown));
        }
    }
}
