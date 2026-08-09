using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Xbox;

using Windows.Storage;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Restoring and reapplying across a game's renditions when one of them refuses its write.
    ///
    /// The renditions live in the Xbox app's own live cache, so any one write can be refused for
    /// reasons that are about what the app is doing at that moment rather than about the game - and
    /// the largest rendition, the one most likely to be on screen, comes first. What these pin is
    /// that one refusal costs one rendition: the others are still visited, the refused one keeps the
    /// sidecars a retry needs, and the refusal is reported rather than thrown through the caller.
    /// </summary>
    public class XboxTilesRestoreTests
    {
        /// <summary>A file in the vault, written directly - the state an earlier apply left behind.</summary>
        private static async Task WriteVaultAsync(StorageFolder vault, string fileName, string content)
        {
            StorageFile file = await vault.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            await FileIO.WriteTextAsync(file, content);
        }

        private static async Task<bool> VaultHasAsync(StorageFolder vault, string fileName)
        {
            try
            {
                await vault.GetFileAsync(fileName);

                return true;
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        [Fact]
        public async Task Restore_carries_on_past_a_rendition_it_cannot_write()
        {
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                XboxTileStore.RecordFolder = records.Folder;
                StorageFolder vault = await XboxTileStore.VaultFolderAsync();

                await cache.WriteAsync("111", "custom one");
                await cache.WriteAsync("222", "custom two");
                await WriteVaultAsync(vault, "111.bak", "original one");
                await WriteVaultAsync(vault, "111.new", "custom one");
                await WriteVaultAsync(vault, "222.bak", "original two");
                await WriteVaultAsync(vault, "222.new", "custom two");

                ArtworkFiles.RestoreOutcome outcome;
                IReadOnlyList<string> failures;

                // The first rendition is held open the way the Xbox app holds a tile it is showing
                using (File.Open(
                    Path.Combine(cache.FullPath, "111"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    (outcome, failures) = await XboxTiles.RestoreAsync(cache.Folder, new[] { "111", "222" });
                }

                // The second rendition was still restored, and the refusal was reported, not thrown
                Assert.Equal(ArtworkFiles.RestoreOutcome.Restored, outcome);
                Assert.Single(failures);
                Assert.Equal("original two", await cache.ReadAsync("222"));

                // The refused rendition keeps the tile it had and both sidecars, so the retry has
                // everything this attempt had
                Assert.Equal("custom one", await cache.ReadAsync("111"));
                Assert.True(await VaultHasAsync(vault, "111.bak"));
                Assert.True(await VaultHasAsync(vault, "111.new"));
            }
        }

        [Fact]
        public async Task Restore_reports_backups_it_could_not_write_rather_than_calling_them_missing()
        {
            // BackupMissing with failures alongside means "the backups exist and every write was
            // refused". A caller that read the outcome alone would tell the user there is nothing to
            // restore while the vault holds exactly what they asked for.
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                XboxTileStore.RecordFolder = records.Folder;
                StorageFolder vault = await XboxTileStore.VaultFolderAsync();

                await cache.WriteAsync("111", "custom one");
                await WriteVaultAsync(vault, "111.bak", "original one");

                using (File.Open(
                    Path.Combine(cache.FullPath, "111"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    (ArtworkFiles.RestoreOutcome outcome, IReadOnlyList<string> failures) =
                        await XboxTiles.RestoreAsync(cache.Folder, new[] { "111" });

                    Assert.Equal(ArtworkFiles.RestoreOutcome.BackupMissing, outcome);
                    Assert.Single(failures);
                }

                Assert.True(await VaultHasAsync(vault, "111.bak"));
            }
        }

        [Fact]
        public async Task Reapply_carries_on_past_a_rendition_it_cannot_write()
        {
            // The load-time pass. Before this containment existed, one refused rendition threw the
            // whole game out of the library for as long as the cause persisted.
            using (var records = new TempFolder())
            using (var cache = new TempFolder())
            {
                XboxTileStore.RecordFolder = records.Folder;
                StorageFolder vault = await XboxTileStore.VaultFolderAsync();

                await cache.WriteAsync("111", "overwritten one");
                await cache.WriteAsync("222", "overwritten two");
                await WriteVaultAsync(vault, "111.new", "custom one");
                await WriteVaultAsync(vault, "222.new", "custom two");

                HashSet<string> vaultNames = await XboxTileStore.VaultFileNamesAsync();

                using (File.Open(
                    Path.Combine(cache.FullPath, "111"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    ArtworkFiles.ReapplyOutcome outcome = await XboxTiles.ReapplyOverwrittenAsync(
                        cache.Folder, new[] { "111", "222" }, vaultNames);

                    Assert.Equal(ArtworkFiles.ReapplyOutcome.Reapplied, outcome);
                }

                // The reachable rendition was put back - padded to the tile's length, as an in-place
                // write is - and its saved copy brought up to date to match
                byte[] tile = cache.ReadBytes("222");
                byte[] artwork = TestImages.ToArray(TestImages.Bytes("custom two"));

                Assert.Equal("overwritten two".Length, tile.Length);
                Assert.Equal(artwork, tile[0..artwork.Length]);
                Assert.All(tile[artwork.Length..], b => Assert.Equal(0, b));

                // The refused one keeps its .new for next load
                Assert.True(await VaultHasAsync(vault, "111.new"));
            }
        }
    }
}
