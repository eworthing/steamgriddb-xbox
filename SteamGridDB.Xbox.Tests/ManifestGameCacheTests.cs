using System.Collections.Generic;

using SteamGridDB.Xbox.Services.Library;

using Windows.Data.Json;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Reading the entries out of a manifest's "gameCache" object - split out of
    /// LoadGameEntriesAsync's folder loop (see PrimaryWidget.xaml.cs) so the manifest-shape rules,
    /// especially the "id" field's null-handling, are exercised directly rather than only by
    /// inspection deep inside a UI-bound method.
    /// </summary>
    public class ManifestGameCacheTests
    {
        [Fact]
        public void Well_formed_manifest_returns_every_entry_with_its_id()
        {
            string json = @"{
                ""gameCache"": {
                    ""steam:440"": { ""id"": ""steam:440"", ""title"": ""Team Fortress 2"" },
                    ""gog:123"": { ""id"": ""gog:123"" }
                }
            }";

            List<(string Id, JsonObject Entry)> entries = ManifestGameCache.Entries(json);

            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, e => e.Id == "steam:440");
            Assert.Contains(entries, e => e.Id == "gog:123");
        }

        [Fact]
        public void Missing_gameCache_returns_no_entries()
        {
            string json = @"{ ""somethingElse"": {} }";

            List<(string Id, JsonObject Entry)> entries = ManifestGameCache.Entries(json);

            Assert.Empty(entries);
        }

        [Fact]
        public void Unparsable_json_returns_no_entries_rather_than_throwing()
        {
            string json = "not valid json";

            List<(string Id, JsonObject Entry)> entries = ManifestGameCache.Entries(json);

            Assert.Empty(entries);
        }

        [Fact]
        public void GameCache_that_is_not_an_object_returns_no_entries()
        {
            string json = @"{ ""gameCache"": ""not an object"" }";

            List<(string Id, JsonObject Entry)> entries = ManifestGameCache.Entries(json);

            Assert.Empty(entries);
        }

        [Fact]
        public void Version_key_is_skipped_even_when_its_value_is_an_object()
        {
            // The value is deliberately an object (rather than a number, which the type check below
            // would also filter out) so this test exercises the key-based skip specifically, not the
            // "only process object entries" check.
            string json = @"{
                ""gameCache"": {
                    ""version"": { ""id"": ""should not be read as an entry"" },
                    ""steam:440"": { ""id"": ""steam:440"" }
                }
            }";

            List<(string Id, JsonObject Entry)> entries = ManifestGameCache.Entries(json);

            Assert.Single(entries);
            Assert.Equal("steam:440", entries[0].Id);
        }

        [Fact]
        public void Non_object_entry_is_skipped()
        {
            string json = @"{
                ""gameCache"": {
                    ""stray"": ""a string, not an entry object"",
                    ""steam:440"": { ""id"": ""steam:440"" }
                }
            }";

            List<(string Id, JsonObject Entry)> entries = ManifestGameCache.Entries(json);

            Assert.Single(entries);
            Assert.Equal("steam:440", entries[0].Id);
        }

        [Fact]
        public void Entry_with_no_id_field_is_skipped()
        {
            string json = @"{
                ""gameCache"": {
                    ""orphan"": { ""title"": ""No id here"" }
                }
            }";

            List<(string Id, JsonObject Entry)> entries = ManifestGameCache.Entries(json);

            Assert.Empty(entries);
        }

        [Fact]
        public void Entry_with_json_null_id_does_not_drop_the_entries_that_follow_it()
        {
            // The bug this whole extraction exists to keep fixed: GetNamedString throws on a
            // present-but-JSON-null "id", and nothing used to catch that, so a single null "id"
            // silently dropped every entry after it in the folder. JsonRead.String must keep treating
            // a null "id" the same as a missing one - a skip, not a throw that would abort the loop
            // and take "gog:123" down with it.
            string json = @"{
                ""gameCache"": {
                    ""nullEntry"": { ""id"": null },
                    ""gog:123"": { ""id"": ""gog:123"" }
                }
            }";

            List<(string Id, JsonObject Entry)> entries = ManifestGameCache.Entries(json);

            Assert.Single(entries);
            Assert.Equal("gog:123", entries[0].Id);
        }

        [Fact]
        public void Entry_with_empty_id_is_skipped()
        {
            string json = @"{
                ""gameCache"": {
                    ""blank"": { ""id"": """" }
                }
            }";

            List<(string Id, JsonObject Entry)> entries = ManifestGameCache.Entries(json);

            Assert.Empty(entries);
        }
    }
}
