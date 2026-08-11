// System, for the extension method that makes `await someIAsyncOperation` compile - see TESTING.md.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Windows.Storage;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.Library;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Walking the Xbox app's ThirdPartyLibraries manifests.
    ///
    /// The pieces this leans on - <c>ManifestGameCache</c>, <c>ManifestEntryImage</c>,
    /// <c>ManifestEntryIdentity</c> - each have their own tests. What is covered here is the loop that
    /// holds them together, and specifically how far one bad folder or one bad entry is allowed to
    /// reach. That rule has been wrong before: the folder-level catch was once the only one, so anything
    /// throwing part-way through a manifest discarded every remaining entry in it, uncounted and
    /// unlogged.
    ///
    /// SteamGridDB is never queried - the loader is given no client and told it cannot ask, which is
    /// exactly what happens on a machine with no API key.
    /// </summary>
    public class LibraryLoaderTests
    {
        private static Task<LibraryLoader.ThirdPartyLoad> LoadAsync(TempFolder temp)
        {
            FixLog.LogFolder = temp.Folder;

            return LibraryLoader.ThirdPartyRowsAsync(temp.Folder, null, false, _ => Task.CompletedTask);
        }

        private static async Task<StorageFolder> StoreFolderAsync(TempFolder temp, string name, string manifestJson)
        {
            StorageFolder folder = await temp.Folder.CreateFolderAsync(name);

            if (manifestJson != null)
            {
                StorageFile manifest = await folder.CreateFileAsync($"{name}.manifest");

                await FileIO.WriteTextAsync(manifest, manifestJson);
            }

            return folder;
        }

        private static string Manifest(params string[] entryIds)
        {
            string entries = string.Join(",", entryIds.Select((id, i) => $"\"e{i}\":{{\"id\":\"{id}\"}}"));

            return $"{{\"version\":\"1\",\"gameCache\":{{{entries}}}}}";
        }

        private static async Task WriteArtworkAsync(StorageFolder folder, string fileName)
        {
            StorageFile file = await folder.CreateFileAsync(fileName);

            await FileIO.WriteTextAsync(file, "artwork-bytes");
        }

        [Fact]
        public async Task Reads_the_entries_a_manifest_has_artwork_on_disk_for()
        {
            using (var temp = new TempFolder())
            {
                StorageFolder gog = await StoreFolderAsync(temp, "gog", Manifest("gog:111", "gog:222"));

                await WriteArtworkAsync(gog, "gog_111.png");
                await WriteArtworkAsync(gog, "gog_222.png");

                LibraryLoader.ThirdPartyLoad load = await LoadAsync(temp);

                Assert.Equal(2, load.Rows.Count);
                Assert.Equal(0, load.StaleEntryCount);
                Assert.All(load.Rows, row => Assert.Equal(SteamGridDB.Xbox.Models.GamePlatform.GOG, row.Platform));
            }
        }

        [Fact]
        public async Task An_entry_with_no_artwork_and_no_backup_is_counted_rather_than_dropped_silently()
        {
            // A library that quietly shrinks is worse than one that says why. The count is what the
            // status line reports; last-load.log names which entries they were.
            using (var temp = new TempFolder())
            {
                StorageFolder gog = await StoreFolderAsync(temp, "gog", Manifest("gog:111", "gog:222"));

                await WriteArtworkAsync(gog, "gog_111.png");

                LibraryLoader.ThirdPartyLoad load = await LoadAsync(temp);

                Assert.Equal(1, load.StaleEntryCount);
                Assert.Equal("gog_111.png", Assert.Single(load.Rows).ImageFileName);
            }
        }

        [Fact]
        public async Task A_folder_with_no_manifest_does_not_fail_the_load()
        {
            using (var temp = new TempFolder())
            {
                await StoreFolderAsync(temp, "steam", null);

                StorageFolder gog = await StoreFolderAsync(temp, "gog", Manifest("gog:111"));

                await WriteArtworkAsync(gog, "gog_111.png");

                LibraryLoader.ThirdPartyLoad load = await LoadAsync(temp);

                Assert.Equal("gog_111.png", Assert.Single(load.Rows).ImageFileName);
            }
        }

        [Fact]
        public async Task A_folder_whose_manifest_will_not_parse_does_not_take_the_others_with_it()
        {
            using (var temp = new TempFolder())
            {
                await StoreFolderAsync(temp, "steam", "{ this is not json");

                StorageFolder gog = await StoreFolderAsync(temp, "gog", Manifest("gog:111"));

                await WriteArtworkAsync(gog, "gog_111.png");

                LibraryLoader.ThirdPartyLoad load = await LoadAsync(temp);

                Assert.Equal("gog_111.png", Assert.Single(load.Rows).ImageFileName);
            }
        }

        [Fact]
        public async Task Battle_net_is_skipped_because_the_xbox_app_keeps_no_artwork_for_it()
        {
            using (var temp = new TempFolder())
            {
                StorageFolder battleNet = await StoreFolderAsync(temp, "battlenet", Manifest("battlenet:111"));

                await WriteArtworkAsync(battleNet, "battlenet_111.png");

                LibraryLoader.ThirdPartyLoad load = await LoadAsync(temp);

                Assert.Empty(load.Rows);
                Assert.Equal(0, load.StaleEntryCount);
            }
        }

        [Fact]
        public async Task A_row_carries_the_file_to_decode_rather_than_a_decoded_image()
        {
            // The one step the widget still owns. If this came back null the library would render every
            // game as a placeholder, which is exactly what it does when the artwork is genuinely absent
            // - so the two must not be confusable.
            using (var temp = new TempFolder())
            {
                StorageFolder gog = await StoreFolderAsync(temp, "gog", Manifest("gog:111"));

                await WriteArtworkAsync(gog, "gog_111.png");

                LibraryLoader.ThirdPartyLoad load = await LoadAsync(temp);

                Assert.Equal("gog_111.png", Assert.Single(load.Rows).ThumbnailSource.Name);
            }
        }

        [Fact]
        public void Rows_sort_alphabetically_with_unnamed_games_last()
        {
            List<LibraryRow> sorted = LibraryLoader.SortedByName(new List<LibraryRow>
            {
                new LibraryRow { Name = "Myst" },
                new LibraryRow { Name = LibraryLoader.UnknownName },
                new LibraryRow { Name = "Doom" }
            });

            Assert.Equal(
                new[] { "Doom", "Myst", LibraryLoader.UnknownName },
                sorted.Select(r => r.Name).ToArray());
        }

        [Fact]
        public void Several_unnamed_games_all_sort_last()
        {
            List<LibraryRow> sorted = LibraryLoader.SortedByName(new List<LibraryRow>
            {
                new LibraryRow { Name = LibraryLoader.UnknownName },
                new LibraryRow { Name = "Zork" },
                new LibraryRow { Name = LibraryLoader.UnknownName },
                new LibraryRow { Name = "Adventure" }
            });

            Assert.Equal(
                new[] { "Adventure", "Zork", LibraryLoader.UnknownName, LibraryLoader.UnknownName },
                sorted.Select(r => r.Name).ToArray());
        }
    }
}
