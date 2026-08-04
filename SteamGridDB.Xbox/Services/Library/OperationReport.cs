using System;
using System.Collections.Generic;
using System.Text;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Keeps count of a run over the library and turns it into the lines the status bar shows.
    ///
    /// Fixing, reverting and restoring all report the same way - a line naming the game and its place
    /// in the run, then a summary of how it went - and all three previously kept their own counters and
    /// built both strings by hand. Two things went wrong there often enough to be worth removing.
    ///
    /// The position in the progress line was the sum of every outcome counter, written out by name at
    /// the call site. Adding an outcome meant remembering to add it to that sum as well, and a sum that
    /// missed one counted a game as still to come after it had been done, so the run appeared to stall
    /// and then jump. Here the position is counted once, by <see cref="Step"/>, and cannot disagree
    /// with itself.
    ///
    /// The summaries hand-rolled their own pluralisation and their own "only mention this if it
    /// happened" logic, three times, slightly differently.
    /// </summary>
    internal sealed class OperationReport
    {
        private readonly string verb;
        private readonly int total;

        private int started;

        /// <param name="verb">Present participle naming the operation, e.g. "Reverting".</param>
        /// <param name="total">How many games the run will visit.</param>
        internal OperationReport(string verb, int total)
        {
            this.verb = verb;
            this.total = total;
        }

        /// <summary>How many games the run will visit.</summary>
        internal int Total => total;

        /// <summary>How many games the run has reached so far, including the one in hand.</summary>
        internal int Started => started;

        /// <summary>
        /// Counts the next game as reached and returns the line naming it, e.g.
        /// "Reverting Halo 3 (4/12)...". Call once per game, before doing the work, whatever the
        /// outcome turns out to be - a game that is skipped still took its turn.
        /// </summary>
        /// <param name="gameName">Name to show, already resolved to something the user will recognise.</param>
        internal string Step(string gameName)
        {
            started++;

            return $"{verb} {gameName} ({started}/{total})...";
        }

        /// <summary>
        /// A count with its noun pluralised, e.g. "1 error" or "2 errors".
        /// </summary>
        /// <param name="count">How many.</param>
        /// <param name="singular">The noun in its singular form.</param>
        internal static string Plural(int count, string singular)
        {
            return count == 1 ? $"{count} {singular}" : $"{count} {singular}s";
        }

        /// <summary>
        /// An opening statement followed by whichever clauses have something to say, comma separated.
        /// Null and empty clauses are dropped, so a caller can pass a clause that is only sometimes
        /// worth mentioning without wrapping it in an if.
        /// </summary>
        /// <param name="opening">Always shown, e.g. "Revert complete: 7 restored to Xbox defaults".</param>
        /// <param name="clauses">Optional additions, e.g. "2 skipped (no backup)".</param>
        internal static string Summary(string opening, params string[] clauses)
        {
            var summary = new StringBuilder(opening);

            foreach (string clause in clauses ?? Array.Empty<string>())
            {
                if (!string.IsNullOrEmpty(clause))
                {
                    summary.Append(", ").Append(clause);
                }
            }

            return summary.ToString();
        }

        /// <summary>
        /// <paramref name="clause"/> when <paramref name="count"/> is more than none, otherwise nothing -
        /// for the outcomes a summary only mentions when they actually happened.
        /// </summary>
        internal static string When(int count, string clause)
        {
            return count > 0 ? clause : null;
        }
    }
}
