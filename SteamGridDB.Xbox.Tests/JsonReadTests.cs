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
        public void Reads_nested_members()
        {
            JsonObject steam = JsonRead.Object(Parse(@"{""platforms"":{""steam"":{""id"":""440""}}}"), "platforms");

            Assert.Equal("440", JsonRead.String(JsonRead.Object(steam, "steam"), "id"));
        }
    }
}
