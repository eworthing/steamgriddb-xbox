using SteamGridDB.Xbox.Services.Stores;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Reducing a title to what two different stores would agree on.
    ///
    /// Only the pure part is covered here. The rest of StoreNameLookup calls GOG, Epic and Ubisoft
    /// over the network, and a test that did that would be grading their uptime.
    /// </summary>
    public class StoreNameLookupTests
    {
        [Theory]
        [InlineData("Rocket League®", "rocketleague")]
        [InlineData("Rocket League", "rocketleague")]
        [InlineData("ROCKET LEAGUE", "rocketleague")]
        [InlineData("Tom Clancy's The Division™", "tomclancysthedivision")]
        [InlineData("Assassin's Creed: Odyssey", "assassinscreedodyssey")]
        [InlineData("F.E.A.R.", "fear")]
        [InlineData("Half-Life 2", "halflife2")]
        public void Reduces_a_title_to_letters_and_digits_in_lower_case(string name, string expected)
        {
            Assert.Equal(expected, StoreNameLookup.NormaliseGameName(name));
        }

        [Fact]
        public void Titles_that_differ_only_in_punctuation_and_case_normalise_alike()
        {
            // The whole point: the same game named two ways by two stores has to compare equal.
            Assert.Equal(
                StoreNameLookup.NormaliseGameName("Rocket League®"),
                StoreNameLookup.NormaliseGameName("rocket league"));
        }

        [Fact]
        public void Different_games_still_differ()
        {
            Assert.NotEqual(
                StoreNameLookup.NormaliseGameName("Halo 3"),
                StoreNameLookup.NormaliseGameName("Halo 4"));
        }

        [Fact]
        public void An_absent_name_normalises_to_empty_rather_than_throwing()
        {
            Assert.Equal(string.Empty, StoreNameLookup.NormaliseGameName(null));
            Assert.Equal(string.Empty, StoreNameLookup.NormaliseGameName(string.Empty));
        }

        [Fact]
        public void A_name_that_is_all_punctuation_normalises_to_empty()
        {
            // Worth knowing rather than assuming: an empty normalised name matches every other empty
            // one, so anything comparing on this needs to treat empty as "no answer", not as a match.
            Assert.Equal(string.Empty, StoreNameLookup.NormaliseGameName("!!! ---"));
        }
    }
}
