using System;

using SteamGridDB.Xbox.Services;

using Windows.Data.Json;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The tolerant JSON lookups.
    ///
    /// These exist because Windows.Data.Json's own defaulting overloads throw on a member that is
    /// present and JSON null. That shipped: reading the Steam app ID threw for every game that had
    /// Steam platform data, and the catch above it reported "Valve has no artwork for this game" -
    /// which is true for some games, so a bug affecting all of them looked exactly like the truth.
    /// </summary>
    public class JsonReadTests
    {
        private static JsonObject Parse(string json)
        {
            return JsonObject.Parse(json);
        }

        [Fact]
        public void Reads_a_member_that_is_there()
        {
            Assert.Equal("Halo", JsonRead.String(Parse(@"{""name"":""Halo""}"), "name"));
        }

        [Fact]
        public void Returns_null_for_a_member_that_is_explicitly_json_null()
        {
            // The shipped failure. The built-in overloads throw here.
            Assert.Null(JsonRead.String(Parse(@"{""name"":null}"), "name"));
            Assert.Null(JsonRead.Object(Parse(@"{""platforms"":null}"), "platforms"));
            Assert.Null(JsonRead.Array(Parse(@"{""tags"":null}"), "tags"));
        }

        [Fact]
        public void Returns_null_for_a_member_that_is_absent()
        {
            Assert.Null(JsonRead.String(Parse(@"{}"), "name"));
            Assert.Null(JsonRead.Object(Parse(@"{}"), "platforms"));
            Assert.Null(JsonRead.Array(Parse(@"{}"), "tags"));
            Assert.Null(JsonRead.Value(Parse(@"{}"), "anything"));
        }

        [Fact]
        public void Returns_null_for_a_member_of_the_wrong_type()
        {
            // SteamGridDB has changed a field's type between responses before.
            Assert.Null(JsonRead.String(Parse(@"{""name"":123}"), "name"));
            Assert.Null(JsonRead.Object(Parse(@"{""platforms"":[]}"), "platforms"));
            Assert.Null(JsonRead.Array(Parse(@"{""tags"":""a,b""}"), "tags"));
        }

        [Fact]
        public void Returns_null_for_a_null_source_rather_than_throwing()
        {
            Assert.Null(JsonRead.String(null, "name"));
            Assert.Null(JsonRead.Object(null, "platforms"));
            Assert.Null(JsonRead.Array(null, "tags"));
            Assert.Null(JsonRead.Value(null, "anything"));
        }

        [Fact]
        public void Reads_a_number()
        {
            Assert.Equal(1080, JsonRead.Number(Parse(@"{""Width"":1080}"), "Width"));
        }

        [Fact]
        public void Falls_back_rather_than_throwing_for_a_number_that_is_not_one()
        {
            // The Store catalogue's image dimensions go through here, and a product whose Width came
            // back as a string would otherwise throw mid-parse and cost the whole response
            Assert.Equal(0, JsonRead.Number(Parse(@"{""Width"":null}"), "Width"));
            Assert.Equal(0, JsonRead.Number(Parse(@"{""Width"":""1080""}"), "Width"));
            Assert.Equal(0, JsonRead.Number(Parse(@"{}"), "Width"));
            Assert.Equal(0, JsonRead.Number(null, "Width"));
            Assert.Equal(-1, JsonRead.Number(Parse(@"{}"), "Width", -1));
        }

        [Fact]
        public void Reads_nested_members()
        {
            JsonObject steam = JsonRead.Object(Parse(@"{""platforms"":{""steam"":{""id"":""440""}}}"), "platforms");

            Assert.Equal("440", JsonRead.String(JsonRead.Object(steam, "steam"), "id"));
        }

        /// <summary>
        /// Proves the shipped-once failure this class exists to prevent, rather than only describing it
        /// in the class doc comment above. LoadGameEntriesAsync's manifest-entry loop called the raw
        /// Windows.Data.Json overloads directly at five sites (id/addedDate/imagePath/title/
        /// installLocation/executableName) with no per-entry try/catch around them; a single manifest
        /// entry with one of these fields present as JSON null threw here, uncaught until the
        /// per-folder catch several stack frames up, silently discarding every other entry in that
        /// platform folder. All five now route through JsonRead.
        /// </summary>
        [Fact]
        public void Raw_windows_data_json_overloads_throw_on_a_present_json_null_member()
        {
            JsonObject obj = Parse(@"{""id"":null}");

            Assert.Throws<InvalidOperationException>(() => obj.GetNamedString("id"));
            Assert.Throws<InvalidOperationException>(() => obj.GetNamedString("id", "0"));

            // ContainsKey does not distinguish "present and null" from "present and a real value" -
            // it is not a safe guard against the throw above, which is why JsonRead checks the return
            // value's type instead of asking the source whether the key exists.
            Assert.True(obj.ContainsKey("id"));
        }
    }
}
