using System;
using System.Collections.Generic;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Xbox;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Whether a first-party game can be reverted to the Xbox app's own artwork.
    ///
    /// The answer decides whether a row shows its restore button, and - through
    /// <see cref="XboxTiles.ForgetArtworkRecordsAsync"/> - whether the applied-artwork record for a
    /// game with no backup is dropped as describing nothing. It used to be an awaited walk that opened
    /// one file per rendition and read its answer out of the FileNotFoundException it caught, so it
    /// could only be checked by running the app. It is now a question about which names a listing
    /// holds, which is a plain function, which is this file.
    /// </summary>
    public class XboxTileBackupTests
    {
        private static ISet<string> Vault(params string[] fileNames)
        {
            return new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void A_rendition_with_a_backup_counts()
        {
            Assert.True(XboxTiles.HasBackup(
                new[] { "111" },
                Vault("111" + ArtworkFiles.BackupExtension)));
        }

        [Fact]
        public void A_backup_on_any_rendition_is_enough()
        {
            // Applying artwork writes a backup per rendition, but a revert consumes them one at a
            // time - so a partly reverted game still has something left to restore
            Assert.True(XboxTiles.HasBackup(
                new[] { "111", "222", "333" },
                Vault("333" + ArtworkFiles.BackupExtension)));
        }

        [Fact]
        public void A_vault_holding_nothing_for_this_game_does_not()
        {
            Assert.False(XboxTiles.HasBackup(
                new[] { "111" },
                Vault("999" + ArtworkFiles.BackupExtension)));
        }

        [Fact]
        public void A_saved_customisation_is_not_a_backup()
        {
            // The two sidecars are written at different moments and removed by different things: a
            // .new says the user's artwork can be put back, only a .bak says the Xbox app's can be
            Assert.False(XboxTiles.HasBackup(
                new[] { "111" },
                Vault("111" + ArtworkFiles.CustomisedExtension)));
        }

        [Fact]
        public void Names_are_matched_without_regard_to_case()
        {
            // The cache names files by a hash, and nothing guarantees the record and the directory
            // entry agree on the case of its hex digits
            Assert.True(XboxTiles.HasBackup(
                new[] { "AbCdEf" },
                Vault("abcdef" + ArtworkFiles.BackupExtension)));
        }

        [Fact]
        public void A_game_with_no_renditions_has_no_backup()
        {
            Assert.False(XboxTiles.HasBackup(null, Vault("111" + ArtworkFiles.BackupExtension)));
            Assert.False(XboxTiles.HasBackup(new string[0], Vault("111" + ArtworkFiles.BackupExtension)));
        }

        [Fact]
        public void An_unread_vault_is_not_read_as_an_empty_one()
        {
            // Same reasoning as XboxTiles.IsOrphanedRecord's own empty-set guard: "nothing known" must
            // not become "nothing there", because the caller acts on the negative answer
            Assert.False(XboxTiles.HasBackup(new[] { "111" }, null));
        }
    }
}
