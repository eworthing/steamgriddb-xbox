using System;
using System.IO;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services.Library;

using Windows.Data.Json;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Resolving where a manifest entry's image lives on disk - split out of LoadGameEntriesAsync's
    /// per-entry parsing (see PrimaryWidget.xaml.cs) so the Custom-platform full-path branch, the
    /// standard from-ID path branch, and the missing-image/has-backup "Not found" placeholder shape are
    /// exercised directly against a real throwaway directory, rather than only by inspection several
    /// hundred lines into a UI-bound method.
    ///
    /// Real StorageFolder/StorageFile throughout, matching ArtworkFilesTests's own rationale: these are
    /// sealed WinRT types with no interface to substitute, and the operations that decide whether a row
    /// is kept or dropped are exactly where a stub's opinion of the file system could diverge from the
    /// real thing.
    /// </summary>
    public class ManifestEntryImageTests
    {
        private const string imageExtension = ".png";
        private const string thirdPartyLibrariesPath = "C:\\ThirdPartyLibraries";

        private static JsonObject Parse(string json)
        {
            return JsonObject.Parse(json);
        }

        private static string EscapeForJson(string path)
        {
            return path.Replace("\\", "\\\\");
        }

        // ---- Standard (non-Custom) platforms: path built from the entry ID ----

        [Fact]
        public async Task Standard_platform_builds_the_image_file_name_from_the_id_with_colons_replaced()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync("gog_1234567890.png", "artwork-bytes");

                ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                    Parse("{}"), GamePlatform.GOG, "gog:1234567890", temp.Folder, thirdPartyLibrariesPath, imageExtension);

                Assert.NotNull(result);
                Assert.Equal("gog_1234567890.png", result.Value.ImageFileName);
                Assert.Equal(
                    Path.Combine(thirdPartyLibrariesPath, temp.Folder.Name, "gog_1234567890.png"),
                    result.Value.ImageFilePath);
            }
        }

        [Fact]
        public async Task Standard_platform_uses_the_manifest_folder_itself_as_the_image_folder()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync("steam_440.png", "artwork-bytes");

                ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                    Parse("{}"), GamePlatform.Steam, "steam:440", temp.Folder, thirdPartyLibrariesPath, imageExtension);

                Assert.Equal(temp.Folder.Path, result.Value.ImageFolder.Path);
            }
        }

        [Fact]
        public async Task Standard_platform_with_an_existing_image_returns_it_for_the_caller_to_decode()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync("epic_Sugar.png", "artwork-bytes");

                ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                    Parse("{}"), GamePlatform.Epic, "epic:Sugar", temp.Folder, thirdPartyLibrariesPath, imageExtension);

                Assert.NotNull(result.Value.ExistingImageFile);
                Assert.Equal("epic_Sugar.png", result.Value.ExistingImageFile.Name);
            }
        }

        // ---- Custom platform: the manifest carries the full path ----

        [Fact]
        public async Task Custom_platform_with_no_recorded_image_path_is_stale()
        {
            ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                Parse("{}"), GamePlatform.Custom, entryId: "custom:1", manifestFolder: null,
                thirdPartyLibrariesPath: thirdPartyLibrariesPath, imageExtension: imageExtension);

            Assert.Null(result);
        }

        [Fact]
        public async Task Custom_platform_with_a_json_null_image_path_is_stale()
        {
            // Same defaulting rule as everywhere else JsonRead.String is used: a present-but-null field
            // is treated as missing, not as a thrown InvalidOperationException.
            ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                Parse(@"{""imagePath"":null}"), GamePlatform.Custom, entryId: "custom:1", manifestFolder: null,
                thirdPartyLibrariesPath: thirdPartyLibrariesPath, imageExtension: imageExtension);

            Assert.Null(result);
        }

        [Fact]
        public async Task Custom_platform_with_a_removed_folder_is_stale()
        {
            // The folder a Custom entry's imagePath points at no longer exists - the game was
            // uninstalled or moved since the manifest was written.
            string missingPath = Path.Combine(
                Path.GetTempPath(), "sgdb-tests-missing-" + Guid.NewGuid().ToString("N"), "cover.png");

            ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                Parse($@"{{""imagePath"":""{EscapeForJson(missingPath)}""}}"), GamePlatform.Custom, entryId: "custom:1",
                manifestFolder: null, thirdPartyLibrariesPath: thirdPartyLibrariesPath, imageExtension: imageExtension);

            Assert.Null(result);
        }

        [Fact]
        public async Task Custom_platform_resolves_the_folder_the_recorded_path_lives_in()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync("cover.png", "artwork-bytes");

                string imagePath = Path.Combine(temp.FullPath, "cover.png");

                ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                    Parse($@"{{""imagePath"":""{EscapeForJson(imagePath)}""}}"), GamePlatform.Custom, entryId: "custom:1",
                    manifestFolder: null, thirdPartyLibrariesPath: thirdPartyLibrariesPath, imageExtension: imageExtension);

                Assert.NotNull(result);
                Assert.Equal(imagePath, result.Value.ImageFilePath);
                Assert.Equal("cover.png", result.Value.ImageFileName);
                Assert.NotNull(result.Value.ExistingImageFile);
            }
        }

        // ---- Missing image, with and without a backup ----

        [Fact]
        public async Task Missing_image_with_a_backup_keeps_the_row_with_the_not_found_placeholder()
        {
            using (var temp = new TempFolder())
            {
                // Only the backup exists - the image itself is gone
                await temp.WriteAsync("gog_1234567890.bak", "xbox-original");

                ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                    Parse("{}"), GamePlatform.GOG, "gog:1234567890", temp.Folder, thirdPartyLibrariesPath, imageExtension);

                Assert.NotNull(result);
                Assert.Equal("Not found", result.Value.ImageFileName);
                Assert.True(result.Value.HasBackup);
                Assert.Null(result.Value.ExistingImageFile);
            }
        }

        [Fact]
        public async Task Missing_image_with_no_backup_is_stale()
        {
            using (var temp = new TempFolder())
            {
                ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                    Parse("{}"), GamePlatform.GOG, "gog:1234567890", temp.Folder, thirdPartyLibrariesPath, imageExtension);

                Assert.Null(result);
            }
        }

        [Fact]
        public async Task Existing_image_with_no_backup_reports_has_backup_false()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync("gog_1234567890.png", "artwork-bytes");

                ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                    Parse("{}"), GamePlatform.GOG, "gog:1234567890", temp.Folder, thirdPartyLibrariesPath, imageExtension);

                Assert.False(result.Value.HasBackup);
            }
        }

        [Fact]
        public async Task Existing_image_with_a_backup_reports_has_backup_true()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync("gog_1234567890.png", "artwork-bytes");
                await temp.WriteAsync("gog_1234567890.bak", "xbox-original");

                ManifestEntryImage.Result? result = await ManifestEntryImage.ResolveAsync(
                    Parse("{}"), GamePlatform.GOG, "gog:1234567890", temp.Folder, thirdPartyLibrariesPath, imageExtension);

                Assert.True(result.Value.HasBackup);
                Assert.NotNull(result.Value.ExistingImageFile);
            }
        }
    }
}
