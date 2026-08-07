using SteamGridDB.Xbox.Services.Library;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Which games a bulk fix run visits.
    ///
    /// The first-party rule is the one worth pinning. It is invisible from inside the app - a run that
    /// wrongly included the Xbox app's own games would look like a successful fix, just with the
    /// publisher's official cover quietly replaced by a community upload on every Game Pass title.
    /// That is also the failure this rule was written to undo, so it is exactly the kind that could be
    /// reintroduced by someone tidying the predicate without knowing why the clause is there.
    /// </summary>
    public class FixEligibilityTests
    {
        [Fact]
        public void Fixes_a_matched_third_party_game_that_has_never_been_customised()
        {
            Assert.True(FixEligibility.ShouldFix(
                hasSteamGridDbMatch: true, isXboxTile: false, hasBackup: false, refixCustomised: false));
        }

        [Fact]
        public void Leaves_an_already_customised_game_alone_unless_re_fixing()
        {
            Assert.False(FixEligibility.ShouldFix(
                hasSteamGridDbMatch: true, isXboxTile: false, hasBackup: true, refixCustomised: false));

            Assert.True(FixEligibility.ShouldFix(
                hasSteamGridDbMatch: true, isXboxTile: false, hasBackup: true, refixCustomised: true));
        }

        [Fact]
        public void Skips_a_game_steamgriddb_does_not_know()
        {
            Assert.False(FixEligibility.ShouldFix(
                hasSteamGridDbMatch: false, isXboxTile: false, hasBackup: false, refixCustomised: false));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void Never_fixes_one_of_the_xbox_apps_own_games(bool hasBackup, bool refixCustomised)
        {
            // Whichever run it is and whatever state the tile is in. A first-party tile is the Store's
            // own BoxArt - the publisher's official cover, and the same kind of artwork the downloader
            // uses as its reference for whether a SteamGridDB pick is even the right game.
            Assert.False(FixEligibility.ShouldFix(
                hasSteamGridDbMatch: true, isXboxTile: true, hasBackup: hasBackup, refixCustomised: refixCustomised));
        }

        [Fact]
        public void Counts_a_first_party_game_the_run_would_otherwise_have_fixed()
        {
            Assert.True(FixEligibility.SkippedAsFirstParty(
                hasSteamGridDbMatch: true, isXboxTile: true, hasBackup: false, refixCustomised: false));
        }

        [Fact]
        public void Does_not_count_a_first_party_game_that_was_never_going_to_be_fixed()
        {
            // No SteamGridDB match, so it was out of scope regardless. Counting it would tell the user
            // this rule cost them artwork it did not.
            Assert.False(FixEligibility.SkippedAsFirstParty(
                hasSteamGridDbMatch: false, isXboxTile: true, hasBackup: false, refixCustomised: false));

            // Already customised, on a run that does not revisit customised games
            Assert.False(FixEligibility.SkippedAsFirstParty(
                hasSteamGridDbMatch: true, isXboxTile: true, hasBackup: true, refixCustomised: false));
        }

        [Fact]
        public void Does_not_count_third_party_games_as_skipped_first_party_ones()
        {
            Assert.False(FixEligibility.SkippedAsFirstParty(
                hasSteamGridDbMatch: true, isXboxTile: false, hasBackup: false, refixCustomised: false));
        }

        [Fact]
        public void Counts_a_customised_first_party_game_only_on_a_re_fix()
        {
            Assert.False(FixEligibility.SkippedAsFirstParty(
                hasSteamGridDbMatch: true, isXboxTile: true, hasBackup: true, refixCustomised: false));

            Assert.True(FixEligibility.SkippedAsFirstParty(
                hasSteamGridDbMatch: true, isXboxTile: true, hasBackup: true, refixCustomised: true));
        }
    }
}
