using System.Collections.Generic;

using SteamGridDB.Xbox.Services.Library;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The progress and summary lines a library run shows.
    ///
    /// This is the only thing the user sees while a fix or revert is working, and every part of it was
    /// previously assembled by hand in three places.
    /// </summary>
    public class OperationReportTests
    {
        [Fact]
        public void Names_the_game_and_its_place_in_the_run()
        {
            var report = new OperationReport("Reverting", 12);

            Assert.Equal("Reverting Halo 3 (1/12)...", report.Step("Halo 3"));
        }

        [Fact]
        public void Counts_from_one_to_the_total_with_no_gaps_or_repeats()
        {
            // The bug this replaces: the position used to be the sum of every outcome counter, written
            // out by name at the call site. A sum that missed a counter left a game looking like it was
            // still to come after it had been done, so the run appeared to stall and then jump.
            var report = new OperationReport("Fixing", 3);

            var seen = new List<string>();

            for (int i = 0; i < 3; i++)
            {
                seen.Add(report.Step("game"));
            }

            Assert.Equal(
                new[] { "Fixing game (1/3)...", "Fixing game (2/3)...", "Fixing game (3/3)..." },
                seen);
        }

        [Fact]
        public void Counts_a_game_that_was_skipped_just_the_same()
        {
            // Callers Step() first and only afterwards discover the outcome - a game with no backup, an
            // unsupported platform. It still took its turn, so the next one must not reuse its number.
            var report = new OperationReport("Reverting", 2);

            report.Step("skipped-game");

            Assert.Equal("Reverting next-game (2/2)...", report.Step("next-game"));
            Assert.Equal(2, report.Started);
        }

        [Theory]
        [InlineData(0, "0 errors")]
        [InlineData(1, "1 error")]
        [InlineData(2, "2 errors")]
        public void Pluralises_a_counted_noun(int count, string expected)
        {
            Assert.Equal(expected, OperationReport.Plural(count, "error"));
        }

        [Theory]
        [InlineData(0, "0 directories")]
        [InlineData(1, "1 directory")]
        [InlineData(2, "2 directories")]
        public void Pluralises_a_counted_noun_with_an_irregular_plural(int count, string expected)
        {
            Assert.Equal(expected, OperationReport.Plural(count, "directory", "directories"));
        }

        [Fact]
        public void A_summary_with_nothing_to_add_is_just_its_opening()
        {
            Assert.Equal("Revert complete: 7 restored to Xbox defaults",
                OperationReport.Summary("Revert complete: 7 restored to Xbox defaults"));
        }

        [Fact]
        public void Summary_clauses_follow_the_opening_in_order()
        {
            Assert.Equal("Revert complete: 5 restored to Xbox defaults, 2 skipped (no backup), 1 error",
                OperationReport.Summary(
                    "Revert complete: 5 restored to Xbox defaults",
                    "2 skipped (no backup)",
                    OperationReport.Plural(1, "error")));
        }

        [Fact]
        public void Clauses_that_did_not_happen_are_left_out_entirely()
        {
            // So a caller can pass a sometimes-relevant clause without wrapping it in an if.
            Assert.Equal("Revert complete: 5 restored to Xbox defaults",
                OperationReport.Summary(
                    "Revert complete: 5 restored to Xbox defaults",
                    OperationReport.When(0, "0 skipped (no backup)"),
                    OperationReport.When(0, OperationReport.Plural(0, "error"))));
        }

        [Fact]
        public void When_keeps_a_clause_that_did_happen()
        {
            Assert.Equal("2 skipped", OperationReport.When(2, "2 skipped"));
            Assert.Null(OperationReport.When(0, "0 skipped"));
        }

        [Fact]
        public void A_summary_survives_being_given_no_clauses_at_all()
        {
            Assert.Equal("Done", OperationReport.Summary("Done", null));
        }

        [Fact]
        public void Reproduces_the_revert_summary_the_widget_used_to_build_by_hand()
        {
            // Pinned against the strings the operation shipped with, so the extraction is checkable
            // rather than merely plausible.
            Assert.Equal("Revert complete: 3 restored to Xbox defaults, 1 skipped (no backup), 2 errors",
                OperationReport.Summary(
                    "Revert complete: 3 restored to Xbox defaults",
                    OperationReport.When(1, "1 skipped (no backup)"),
                    OperationReport.When(2, OperationReport.Plural(2, "error"))));
        }

        [Fact]
        public void Reproduces_the_fix_summary_including_its_always_shown_error_count()
        {
            // Fix differs from the others: it states the error count even when it is zero.
            Assert.Equal("Fixing library is complete: 10 updated, 2 had no artwork in the database, 0 errors",
                OperationReport.Summary(
                    "Fixing library is complete: 10 updated, 2 had no artwork in the database",
                    OperationReport.When(0, "0 skipped (unsupported platform)"),
                    OperationReport.Plural(0, "error")));
        }

        [Fact]
        public void States_which_tile_a_partial_write_could_not_write()
        {
            Assert.Equal("1 tile could not be written: grid",
                OperationReport.WriteFailureClause(new[] { "grid" }));
        }

        [Fact]
        public void Pluralises_the_tile_count_in_a_partial_write_clause()
        {
            Assert.Equal("2 tiles could not be written: grid; icon",
                OperationReport.WriteFailureClause(new[] { "grid", "icon" }));
        }
    }
}
