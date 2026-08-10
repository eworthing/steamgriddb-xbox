using System;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.Artwork;

using Windows.Storage.Streams;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The operations that can destroy artwork the user cannot get back.
    ///
    /// Everything here runs against a real throwaway directory, because the failures worth catching -
    /// a backup overwritten with the artwork it was meant to protect, an image deleted when its backup
    /// was already gone - are failures of file-system semantics, and a substitute file system would only
    /// prove that the substitute agrees with itself.
    /// </summary>
    public class ArtworkFilesTests
    {
        private const string image = "game.png";
        private const string backup = "game.bak";
        private const string customised = "game.new";

        private const string xboxOriginal = "the-xbox-app-original-artwork";

        // ---- Backup preservation: the original must survive every path ----

        [Fact]
        public async Task Apply_backs_up_the_xbox_original_before_overwriting_it()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, xboxOriginal);

                bool hasBackup = await ArtworkFiles.ApplyAsync(temp.Folder, image, TestImages.Bytes("steamgriddb-artwork"));

                Assert.True(hasBackup);
                Assert.Equal(xboxOriginal, await temp.ReadAsync(backup));
            }
        }

        [Fact]
        public async Task Applying_a_second_time_keeps_the_first_backup()
        {
            // The one that matters most. If the second fix re-backed-up, the .bak would hold the first
            // SteamGridDB artwork instead of the Xbox app's own, and reverting to defaults would never
            // reach the original again - with nothing on disk left to recover it from.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, xboxOriginal);

                await ArtworkFiles.ApplyAsync(temp.Folder, image, TestImages.Bytes("first-artwork"));
                await ArtworkFiles.ApplyAsync(temp.Folder, image, TestImages.Bytes("second-artwork"));

                Assert.Equal(xboxOriginal, await temp.ReadAsync(backup));
            }
        }

        [Fact]
        public async Task Apply_reports_the_backup_that_already_existed()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, "current");
                await temp.WriteAsync(backup, xboxOriginal);

                Assert.True(await ArtworkFiles.ApplyAsync(temp.Folder, image, TestImages.Bytes("artwork")));
                Assert.Equal(xboxOriginal, await temp.ReadAsync(backup));
            }
        }

        [Fact]
        public async Task Apply_with_no_existing_image_makes_no_backup()
        {
            // A manifest entry whose image the Xbox app never wrote. There is no original to protect,
            // and inventing an empty .bak would make revert-to-defaults produce a broken tile.
            using (var temp = new TempFolder())
            {
                bool hasBackup = await ArtworkFiles.ApplyAsync(temp.Folder, image, TestImages.Bytes("artwork"));

                Assert.False(hasBackup);
                Assert.False(temp.Exists(backup));
                Assert.Equal("artwork", await temp.ReadAsync(image));
            }
        }

        [Fact]
        public async Task Apply_writes_the_same_bytes_to_the_image_and_the_saved_copy()
        {
            // The saved copy exists to put the customisation back after the Xbox app overwrites it.
            // If it ever diverged from what was written, restoring would silently install different art.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, xboxOriginal);

                await ArtworkFiles.ApplyAsync(temp.Folder, image, TestImages.Bytes("artwork"));

                Assert.Equal(temp.ReadBytes(image), temp.ReadBytes(customised));
            }
        }

        [Fact]
        public async Task Apply_leaves_exactly_the_three_expected_files()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, xboxOriginal);

                await ArtworkFiles.ApplyAsync(temp.Folder, image, TestImages.Bytes("artwork"));

                Assert.Equal(new[] { backup, customised, image }, temp.FileNames());
            }
        }

        // ---- Restore: never leave the game without an image ----

        [Fact]
        public async Task Restore_with_no_backup_changes_nothing()
        {
            // The dangerous shape would be delete-then-restore. With no backup to put back, that
            // leaves the game with no tile at all and no way to get one.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, "customised-artwork");

                ArtworkFiles.RestoreOutcome outcome = await ArtworkFiles.RestoreOriginalAsync(temp.Folder, image);

                Assert.Equal(ArtworkFiles.RestoreOutcome.BackupMissing, outcome);
                Assert.Equal("customised-artwork", await temp.ReadAsync(image));
            }
        }

        [Fact]
        public async Task Restore_with_no_backup_keeps_the_saved_customisation()
        {
            // The backup is located before the saved copy is deleted. Reversing that order would
            // destroy the only record of the customisation on a revert that then could not proceed.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, "customised-artwork");
                await temp.WriteAsync(customised, "customised-artwork");

                await ArtworkFiles.RestoreOriginalAsync(temp.Folder, image);

                Assert.True(temp.Exists(customised));
            }
        }

        [Fact]
        public async Task Restore_puts_the_original_back_and_consumes_the_backup()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, "customised-artwork");
                await temp.WriteAsync(backup, xboxOriginal);

                ArtworkFiles.RestoreOutcome outcome = await ArtworkFiles.RestoreOriginalAsync(temp.Folder, image);

                Assert.Equal(ArtworkFiles.RestoreOutcome.Restored, outcome);
                Assert.Equal(xboxOriginal, await temp.ReadAsync(image));
                Assert.False(temp.Exists(backup));
            }
        }

        [Fact]
        public async Task Restore_deletes_the_saved_customisation()
        {
            // Once the Xbox app's artwork is back, the game is not customised any more. Leaving the
            // saved copy behind would let a later "restore my changes" resurrect artwork the user
            // explicitly reverted.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, "customised-artwork");
                await temp.WriteAsync(backup, xboxOriginal);
                await temp.WriteAsync(customised, "customised-artwork");

                await ArtworkFiles.RestoreOriginalAsync(temp.Folder, image);

                Assert.Equal(new[] { image }, temp.FileNames());
            }
        }

        [Fact]
        public async Task Apply_then_restore_returns_the_original_bytes_exactly()
        {
            // The round trip a user sees as "fix my library" followed by "revert to Xbox defaults".
            using (var temp = new TempFolder())
            {
                IBuffer original = await TestImages.PngAsync(16, 16);

                await temp.WriteBytesAsync(image, TestImages.ToArray(original));

                await ArtworkFiles.ApplyAsync(temp.Folder, image, await TestImages.PngAsync(32, 32));
                await ArtworkFiles.RestoreOriginalAsync(temp.Folder, image);

                Assert.Equal(TestImages.ToArray(original), temp.ReadBytes(image));
            }
        }

        [Fact]
        public async Task Restore_when_the_image_is_already_gone_still_recovers_it()
        {
            // The library list keeps rows whose image has vanished but whose backup has not, precisely
            // so this is reachable.
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(backup, xboxOriginal);

                ArtworkFiles.RestoreOutcome outcome = await ArtworkFiles.RestoreOriginalAsync(temp.Folder, image);

                Assert.Equal(ArtworkFiles.RestoreOutcome.Restored, outcome);
                Assert.Equal(xboxOriginal, await temp.ReadAsync(image));
            }
        }

        // ---- Re-applying a customisation the Xbox app overwrote ----

        [Fact]
        public async Task Reapply_puts_the_saved_customisation_back_over_the_image()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, "xbox-app-overwrote-this");
                await temp.WriteAsync(customised, "my-artwork");

                ArtworkFiles.ReapplyOutcome outcome = await ArtworkFiles.ReapplyCustomisationAsync(temp.Folder, image);

                Assert.Equal(ArtworkFiles.ReapplyOutcome.Reapplied, outcome);
                Assert.Equal("my-artwork", await temp.ReadAsync(image));
            }
        }

        [Fact]
        public async Task Reapply_with_nothing_saved_changes_nothing()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, xboxOriginal);

                ArtworkFiles.ReapplyOutcome outcome = await ArtworkFiles.ReapplyCustomisationAsync(temp.Folder, image);

                Assert.Equal(ArtworkFiles.ReapplyOutcome.NothingSaved, outcome);
                Assert.Equal(xboxOriginal, await temp.ReadAsync(image));
            }
        }

        [Fact]
        public async Task Reapply_keeps_the_backup_so_reverting_still_works()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync(image, "xbox-app-overwrote-this");
                await temp.WriteAsync(backup, xboxOriginal);
                await temp.WriteAsync(customised, "my-artwork");

                await ArtworkFiles.ReapplyCustomisationAsync(temp.Folder, image);

                Assert.Equal(xboxOriginal, await temp.ReadAsync(backup));
            }
        }

        // ---- Sibling naming ----

        [Theory]
        [InlineData("game.png", "game.bak", "game.new")]
        [InlineData("BigFish_123.png", "BigFish_123.bak", "BigFish_123.new")]
        [InlineData("Game v1.2.png", "Game v1.2.bak", "Game v1.2.new")]
        public void Sibling_names_replace_only_the_final_extension(string imageName, string expectedBackup, string expectedCustomised)
        {
            Assert.Equal(expectedBackup, ArtworkFiles.BackupNameFor(imageName));
            Assert.Equal(expectedCustomised, ArtworkFiles.CustomisedNameFor(imageName));
        }

        [Fact]
        public void Sibling_names_differ_from_the_image_even_when_it_is_not_a_png()
        {
            // The reason this uses Path.ChangeExtension and not a string replace of ".png": a replace
            // does nothing to a name that has no .png in it, so the backup name would come back equal
            // to the image name and the copy would overwrite the original.
            Assert.NotEqual("artwork.jpg", ArtworkFiles.BackupNameFor("artwork.jpg"));
            Assert.NotEqual("noextension", ArtworkFiles.BackupNameFor("noextension"));
        }

        [Fact]
        public async Task HasBackup_reports_what_is_on_disk()
        {
            using (var temp = new TempFolder())
            {
                Assert.False(await ArtworkFiles.HasBackupAsync(temp.Folder, image));

                await temp.WriteAsync(backup, xboxOriginal);

                Assert.True(await ArtworkFiles.HasBackupAsync(temp.Folder, image));
            }
        }

        // ---- Sidecars held away from the image ----
        //
        // First-party tiles live in a cache the Xbox app owns and prunes: it removes files it did not
        // put there, so a .bak left beside the image would be deleted and the original lost for good.
        // Those sidecars go into this app's own storage instead. The guarantees above have to hold
        // identically across that split - the same original-survives-everything rules, plus one the
        // same-folder path never had to think about, that a rename cannot move between folders.

        [Fact]
        public async Task Apply_puts_the_backup_in_the_sidecar_folder_and_nothing_beside_the_image()
        {
            using (var images = new TempFolder())
            using (var vault = new TempFolder())
            {
                await images.WriteAsync(image, xboxOriginal);

                bool hasBackup = await ArtworkFiles.ApplyEncodedAsync(
                    images.Folder, image, vault.Folder, TestImages.Bytes("artwork"));

                Assert.True(hasBackup);
                Assert.Equal(xboxOriginal, await vault.ReadAsync(backup));
                Assert.Equal("artwork", await vault.ReadAsync(customised));

                // Anything this app leaves in the Xbox app's cache is a file the Xbox app will delete
                Assert.Equal(new[] { image }, images.FileNames());
            }
        }

        [Fact]
        public async Task Applying_a_second_time_keeps_the_first_backup_in_the_sidecar_folder()
        {
            using (var images = new TempFolder())
            using (var vault = new TempFolder())
            {
                await images.WriteAsync(image, xboxOriginal);

                await ArtworkFiles.ApplyEncodedAsync(images.Folder, image, vault.Folder, TestImages.Bytes("first"));
                await ArtworkFiles.ApplyEncodedAsync(images.Folder, image, vault.Folder, TestImages.Bytes("second"));

                Assert.Equal(xboxOriginal, await vault.ReadAsync(backup));
                Assert.Equal("second", await images.ReadAsync(image));
            }
        }

        [Fact]
        public async Task ApplyEncoded_writes_the_bytes_it_was_given_without_converting_them()
        {
            // The whole reason it is separate from ApplyAsync: a first-party tile has to stay the exact
            // JPEG of the exact size the caller encoded, where a third-party one is forced to PNG to
            // match the name the Xbox app gave it
            using (var images = new TempFolder())
            using (var vault = new TempFolder())
            {
                IBuffer jpeg = await TestImages.JpegAsync();

                await ArtworkFiles.ApplyEncodedAsync(images.Folder, image, vault.Folder, jpeg);

                Assert.Equal(TestImages.ToArray(jpeg), images.ReadBytes(image));
                Assert.False(TileImage.IsPng(images.ReadBytes(image)));
            }
        }

        [Fact]
        public async Task Restore_moves_the_original_back_out_of_the_sidecar_folder()
        {
            // Rename cannot leave a folder, so this path has to move instead - and if it silently did
            // nothing, the customisation would stay on the tile and the button would report success
            using (var images = new TempFolder())
            using (var vault = new TempFolder())
            {
                await images.WriteAsync(image, "customised");
                await vault.WriteAsync(backup, xboxOriginal);
                await vault.WriteAsync(customised, "customised");

                ArtworkFiles.RestoreOutcome outcome = await ArtworkFiles.RestoreOriginalAsync(
                    images.Folder, image, vault.Folder);

                Assert.Equal(ArtworkFiles.RestoreOutcome.Restored, outcome);
                Assert.Equal(xboxOriginal, await images.ReadAsync(image));
                Assert.Empty(vault.FileNames());
            }
        }

        [Fact]
        public async Task Restore_from_a_sidecar_folder_with_no_backup_changes_nothing()
        {
            using (var images = new TempFolder())
            using (var vault = new TempFolder())
            {
                await images.WriteAsync(image, "customised");
                await vault.WriteAsync(customised, "customised");

                ArtworkFiles.RestoreOutcome outcome = await ArtworkFiles.RestoreOriginalAsync(
                    images.Folder, image, vault.Folder);

                Assert.Equal(ArtworkFiles.RestoreOutcome.BackupMissing, outcome);
                Assert.Equal("customised", await images.ReadAsync(image));

                // The saved customisation is the only way back if the Xbox app overwrites the tile, so
                // a restore that found no backup must not have consumed it on the way
                Assert.Equal("customised", await vault.ReadAsync(customised));
            }
        }

        [Fact]
        public async Task Restore_puts_the_original_back_even_when_the_xbox_app_deleted_the_tile()
        {
            // The Xbox app evicts cached images on its own schedule, and a revert has to work
            // afterwards rather than leaving the backup stranded in the vault forever
            using (var images = new TempFolder())
            using (var vault = new TempFolder())
            {
                await vault.WriteAsync(backup, xboxOriginal);

                Assert.Equal(
                    ArtworkFiles.RestoreOutcome.Restored,
                    await ArtworkFiles.RestoreOriginalAsync(images.Folder, image, vault.Folder));

                Assert.Equal(xboxOriginal, await images.ReadAsync(image));
            }
        }

        [Fact]
        public async Task Reapply_writes_the_saved_customisation_from_the_sidecar_folder()
        {
            using (var images = new TempFolder())
            using (var vault = new TempFolder())
            {
                await images.WriteAsync(image, "what the xbox app re-downloaded");
                await vault.WriteAsync(customised, "the artwork that was applied");

                ArtworkFiles.ReapplyOutcome outcome = await ArtworkFiles.ReapplyCustomisationAsync(
                    images.Folder, image, vault.Folder);

                Assert.Equal(ArtworkFiles.ReapplyOutcome.Reapplied, outcome);
                Assert.Equal("the artwork that was applied", await images.ReadAsync(image));

                // Kept, not consumed - the Xbox app can overwrite the same tile again next month
                Assert.Equal("the artwork that was applied", await vault.ReadAsync(customised));
            }
        }

        [Fact]
        public async Task Reapply_from_a_sidecar_folder_with_nothing_saved_changes_nothing()
        {
            using (var images = new TempFolder())
            using (var vault = new TempFolder())
            {
                await images.WriteAsync(image, xboxOriginal);

                Assert.Equal(
                    ArtworkFiles.ReapplyOutcome.NothingSaved,
                    await ArtworkFiles.ReapplyCustomisationAsync(images.Folder, image, vault.Folder));

                Assert.Equal(xboxOriginal, await images.ReadAsync(image));
            }
        }

        [Fact]
        public async Task Sidecars_keep_the_image_name_so_two_games_never_collide()
        {
            // Every first-party tile's sidecars share one folder, which only works because the Xbox app
            // names cached files by a hash that is unique across its whole cache
            using (var images = new TempFolder())
            using (var vault = new TempFolder())
            {
                await images.WriteAsync("4590615919437530241", "first game's tile");
                await images.WriteAsync("6261343673410584925", "second game's tile");

                await ArtworkFiles.ApplyEncodedAsync(images.Folder, "4590615919437530241", vault.Folder, TestImages.Bytes("a"));
                await ArtworkFiles.ApplyEncodedAsync(images.Folder, "6261343673410584925", vault.Folder, TestImages.Bytes("b"));

                Assert.Equal("first game's tile", await vault.ReadAsync("4590615919437530241.bak"));
                Assert.Equal("second game's tile", await vault.ReadAsync("6261343673410584925.bak"));
            }
        }
    }
}
