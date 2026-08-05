using System.Linq;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Services.SteamGridDB;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// NoteCapsuleParse's cap-and-append rule - the one piece of SteamGridDbClient that touches no
    /// network and so could be tested without grading SteamGridDB's own uptime (see TESTING.md).
    /// Everything else in this class calls SteamGridDB directly and stays untested for that reason,
    /// the same carve-out StoreNameLookup's own tests document.
    /// </summary>
    public class SteamGridDbClientTests
    {
        [Fact]
        public async Task Concurrent_notes_are_capped_at_five_with_no_corruption()
        {
            // The mutation this catches: removing NoteCapsuleParse's lock leaves a TOCTOU
            // check-then-add race on a plain List<string>, which is not safe for concurrent Add calls
            // even individually - many threads racing here can throw, or leave the list holding more
            // than the 5-entry cap the check is supposed to enforce.
            var writers = Enumerable.Range(0, 50)
                .Select(i => Task.Run(() => SteamGridDbClient.NoteCapsuleParse($"note {i}")));

            await Task.WhenAll(writers);

            Assert.Equal(5, SteamGridDbClient.CapsuleParseNotes.Count);
        }
    }
}
