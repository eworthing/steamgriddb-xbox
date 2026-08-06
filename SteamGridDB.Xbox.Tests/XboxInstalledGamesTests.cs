// System is not decorative here: it carries the extension method that makes `await` work on the
// WinRT IAsyncOperation the StorageFolder calls below return. See TESTING.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Stores;

using Windows.Storage;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The XboxGames sweep and the deduplication that follows it.
    ///
    /// Duplicates are the normal case, not an edge one: a game installed as MSIXVC is found once under
    /// XboxGames and again as its own registered package, so the two sweeps overlap by design and
    /// collapsing them is part of the answer rather than a tidy-up. The directory walk runs against a
    /// real throwaway directory for the same reason EaLibrary's does - what it has to get right is
    /// which folders it looks in and how it copes with ones that are not games.
    /// </summary>
    public class XboxInstalledGamesTests
    {
        private static string ConfigXml(string storeId, string titleId, string displayName = "A Game")
        {
            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Game configVersion=""1"">
  <ShellVisuals DefaultDisplayName=""{displayName}"" />
  <StoreId>{storeId}</StoreId>
  <TitleId>{titleId}</TitleId>
</Game>";
        }

        private static async Task WriteGameAsync(TempFolder root, string folderName, string storeId, string titleId)
        {
            StorageFolder gameFolder = await root.Folder.CreateFolderAsync(folderName, CreationCollisionOption.ReplaceExisting);
            StorageFolder contentFolder = await gameFolder.CreateFolderAsync(XboxInstalledGames.ContentFolderName);
            StorageFile file = await contentFolder.CreateFileAsync(XboxGameConfig.FileName);

            await FileIO.WriteTextAsync(file, ConfigXml(storeId, titleId));
        }

        [Fact]
        public async Task Reads_every_game_under_the_root()
        {
            using (var root = new TempFolder())
            {
                await WriteGameAsync(root, "Wobbly Life", "9NS86BQ33SPX", "68d51b74");
                await WriteGameAsync(root, "Fortnite", "BT5P2X999VH2", "0FF4B5BD");

                List<XboxGameConfig.Result> configs = await XboxInstalledGames.ReadGameConfigsAsync(root.Folder);

                Assert.Equal(
                    new[] { "9NS86BQ33SPX", "BT5P2X999VH2" },
                    configs.Select(c => c.StoreId).OrderBy(id => id).ToArray());
            }
        }

        [Fact]
        public async Task Skips_a_folder_with_no_Content_subfolder()
        {
            using (var root = new TempFolder())
            {
                await WriteGameAsync(root, "Wobbly Life", "9NS86BQ33SPX", "68d51b74");

                // A game mid-install, or a folder the Xbox app left behind
                await root.Folder.CreateFolderAsync("GameSave");

                List<XboxGameConfig.Result> configs = await XboxInstalledGames.ReadGameConfigsAsync(root.Folder);

                Assert.Single(configs);
            }
        }

        [Fact]
        public async Task Skips_a_Content_folder_with_no_config()
        {
            using (var root = new TempFolder())
            {
                StorageFolder gameFolder = await root.Folder.CreateFolderAsync("Half Installed");

                await gameFolder.CreateFolderAsync(XboxInstalledGames.ContentFolderName);

                Assert.Empty(await XboxInstalledGames.ReadGameConfigsAsync(root.Folder));
            }
        }

        [Fact]
        public async Task An_empty_root_yields_nothing()
        {
            using (var root = new TempFolder())
            {
                Assert.Empty(await XboxInstalledGames.ReadGameConfigsAsync(root.Folder));
            }
        }

        [Fact]
        public async Task Content_packs_are_read_but_not_selected()
        {
            using (var root = new TempFolder())
            {
                await WriteGameAsync(root, "9NS86BQ33SPX", "9NS86BQ33SPX", "68d51b74");
                await WriteGameAsync(root, "MW3 PC MS DLC01 Game Stub 01", "9PMD021RZT4Z", string.Empty);

                List<XboxGameConfig.Result> configs = await XboxInstalledGames.ReadGameConfigsAsync(root.Folder);

                // Both are read - the sweep does not judge - and the selection is what drops the stub
                Assert.Equal(2, configs.Count);
                Assert.Equal(new[] { "9NS86BQ33SPX" }, XboxInstalledGames.SelectGameStoreIds(configs).ToArray());
            }
        }

        [Fact]
        public void SelectGameStoreIds_collapses_the_same_game_found_by_both_sweeps()
        {
            var configs = new[]
            {
                XboxGameConfig.Parse(ConfigXml("9NS86BQ33SPX", "68d51b74")),
                XboxGameConfig.Parse(ConfigXml("9NS86BQ33SPX", "68d51b74")),
                XboxGameConfig.Parse(ConfigXml("BT5P2X999VH2", "0FF4B5BD")),
            };

            Assert.Equal(new[] { "9NS86BQ33SPX", "BT5P2X999VH2" }, XboxInstalledGames.SelectGameStoreIds(configs).ToArray());
        }

        [Fact]
        public void SelectGameStoreIds_keeps_the_order_the_sweeps_found_them_in()
        {
            var configs = new[]
            {
                XboxGameConfig.Parse(ConfigXml("BT5P2X999VH2", "0FF4B5BD")),
                XboxGameConfig.Parse(ConfigXml("9NS86BQ33SPX", "68d51b74")),
            };

            Assert.Equal(new[] { "BT5P2X999VH2", "9NS86BQ33SPX" }, XboxInstalledGames.SelectGameStoreIds(configs).ToArray());
        }

        [Fact]
        public void SelectGameStoreIds_treats_store_ids_case_insensitively()
        {
            var configs = new[]
            {
                XboxGameConfig.Parse(ConfigXml("9ns86bq33spx", "68d51b74")),
                XboxGameConfig.Parse(ConfigXml("9NS86BQ33SPX", "68d51b74")),
            };

            Assert.Single(XboxInstalledGames.SelectGameStoreIds(configs));
        }

        [Fact]
        public void SelectGameStoreIds_copes_with_null()
        {
            Assert.Empty(XboxInstalledGames.SelectGameStoreIds(null));
        }
    }
}
