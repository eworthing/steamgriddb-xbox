using System;
using System.Threading.Tasks;

using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services.Library;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The record that stops every widget open from re-asking SteamGridDB what it already answered.
    ///
    /// This is the app's largest single reduction in outbound traffic, and the two ways it can be
    /// wrong pull in opposite directions: too eager and a game stays "Unknown" for days after it is
    /// added to SteamGridDB, too shy and the whole point is lost. The lifetimes and the freshness rule
    /// are where that balance lives, so they are pinned here.
    /// </summary>
    public class GameMatchCacheTests
    {
        private static readonly DateTimeOffset now = new DateTimeOffset(2026, 8, 7, 12, 0, 0, TimeSpan.Zero);

        private static GameMatchCache.Entry Match(DateTimeOffset fetched)
        {
            return new GameMatchCache.Entry("Half-Life 2", true, "https://example.test/capsule.png", 4321, fetched);
        }

        private static GameMatchCache.Entry Miss(DateTimeOffset fetched)
        {
            return new GameMatchCache.Entry("Unknown", false, null, 0, fetched);
        }

        [Fact]
        public async Task Remembers_what_steamgriddb_said_about_a_game()
        {
            using (var temp = new TempFolder())
            {
                GameMatchCache.RecordFolder = temp.Folder;

                await GameMatchCache.SetAsync(GamePlatform.Steam, "220", Match(now));

                GameMatchCache.Entry? remembered = await GameMatchCache.GetAsync(GamePlatform.Steam, "220", now);

                Assert.True(remembered.HasValue);
                Assert.Equal("Half-Life 2", remembered.Value.Name);
                Assert.True(remembered.Value.Matched);
                Assert.Equal("https://example.test/capsule.png", remembered.Value.CapsuleUrl);
                Assert.Equal(4321, remembered.Value.SteamGridDbGameId);
            }
        }

        [Fact]
        public async Task Reports_nothing_for_a_game_it_has_never_looked_up()
        {
            using (var temp = new TempFolder())
            {
                GameMatchCache.RecordFolder = temp.Folder;

                Assert.Null(await GameMatchCache.GetAsync(GamePlatform.Steam, "220", now));
            }
        }

        [Fact]
        public async Task Survives_a_reload_from_disk()
        {
            // The whole point. A cache that only lived in memory would be dropped every time the Game
            // Bar closes the widget, which is exactly the repeat traffic this exists to remove.
            using (var temp = new TempFolder())
            {
                GameMatchCache.RecordFolder = temp.Folder;

                await GameMatchCache.SetAsync(GamePlatform.Steam, "220", Match(now));

                GameMatchCache.RecordFolder = temp.Folder;

                Assert.Equal("Half-Life 2", (await GameMatchCache.GetAsync(GamePlatform.Steam, "220", now)).Value.Name);
            }
        }

        [Fact]
        public async Task Keeps_a_match_for_weeks_but_a_miss_only_for_days()
        {
            // A match is a fact about a game that exists. A miss is the absence of one, and someone
            // adding the game to SteamGridDB is precisely the event a cache must not hide.
            using (var temp = new TempFolder())
            {
                GameMatchCache.RecordFolder = temp.Folder;

                await GameMatchCache.SetAsync(GamePlatform.Steam, "220", Match(now));
                await GameMatchCache.SetAsync(GamePlatform.GOG, "1207658930", Miss(now));

                DateTimeOffset aWeekLater = now + TimeSpan.FromDays(7);

                Assert.NotNull(await GameMatchCache.GetAsync(GamePlatform.Steam, "220", aWeekLater));
                Assert.Null(await GameMatchCache.GetAsync(GamePlatform.GOG, "1207658930", aWeekLater));
            }
        }

        [Fact]
        public async Task Asks_again_once_a_match_has_aged_out()
        {
            using (var temp = new TempFolder())
            {
                GameMatchCache.RecordFolder = temp.Folder;

                await GameMatchCache.SetAsync(GamePlatform.Steam, "220", Match(now));

                Assert.Null(await GameMatchCache.GetAsync(
                    GamePlatform.Steam, "220", now + GameMatchCache.MatchLifetime + TimeSpan.FromMinutes(1)));
            }
        }

        [Fact]
        public void Treats_an_entry_dated_in_the_future_as_stale()
        {
            // A clock that has moved backwards since the write. An age that makes no sense should send
            // us back to SteamGridDB, not grant the entry eternal freshness until the clock catches up.
            Assert.False(GameMatchCache.IsFresh(Match(now + TimeSpan.FromDays(1)), now));
        }

        [Fact]
        public void Keys_a_game_by_its_platform_as_well_as_its_id()
        {
            // Store IDs are only unique within their own store, and "220" is a perfectly plausible ID
            // in more than one of them.
            Assert.NotEqual(
                GameMatchCache.Key(GamePlatform.Steam, "220"),
                GameMatchCache.Key(GamePlatform.GOG, "220"));
        }

        [Fact]
        public void Keys_a_game_the_same_however_the_manifest_spelled_it()
        {
            Assert.Equal(
                GameMatchCache.Key(GamePlatform.Epic, "AbCdEf"),
                GameMatchCache.Key(GamePlatform.Epic, "abcdef"));
        }

        [Fact]
        public async Task Round_trips_a_miss_that_has_no_capsule_or_id()
        {
            // The absent fields are the normal case for an unmatched game, and JsonValue has no null
            // of its own to write in their place - so they are simply left out and must read back as
            // absent rather than as an empty string.
            using (var temp = new TempFolder())
            {
                GameMatchCache.RecordFolder = temp.Folder;

                await GameMatchCache.SetAsync(GamePlatform.Ubisoft, "232", Miss(now));
                GameMatchCache.RecordFolder = temp.Folder;

                GameMatchCache.Entry remembered = (await GameMatchCache.GetAsync(GamePlatform.Ubisoft, "232", now)).Value;

                Assert.False(remembered.Matched);
                Assert.Null(remembered.CapsuleUrl);
                Assert.Equal(0, remembered.SteamGridDbGameId);
            }
        }

        [Fact]
        public async Task Drops_aged_out_entries_when_it_next_writes()
        {
            // Without this the file only ever grows: a machine that has had fifty games uninstalled
            // would carry them for as long as the install lasts.
            using (var temp = new TempFolder())
            {
                GameMatchCache.RecordFolder = temp.Folder;

                await GameMatchCache.SetAsync(GamePlatform.Steam, "220", Match(now));

                // A later write, long enough after that the first entry is past its lifetime
                DateTimeOffset muchLater = now + GameMatchCache.MatchLifetime + TimeSpan.FromDays(1);

                await GameMatchCache.SetAsync(GamePlatform.Steam, "400", Match(muchLater));

                // Read back from disk: the pruned entry should be gone from the file, not merely
                // filtered out on the way past the freshness check
                GameMatchCache.RecordFolder = temp.Folder;

                Assert.Null(await GameMatchCache.GetAsync(GamePlatform.Steam, "220", muchLater));
                Assert.NotNull(await GameMatchCache.GetAsync(GamePlatform.Steam, "400", muchLater));
            }
        }

        [Fact]
        public async Task Starts_empty_rather_than_throwing_on_a_damaged_record()
        {
            using (var temp = new TempFolder())
            {
                await temp.WriteAsync("game-matches.json", "{ this is not json");

                GameMatchCache.RecordFolder = temp.Folder;

                Assert.Null(await GameMatchCache.GetAsync(GamePlatform.Steam, "220", now));

                // and still usable afterwards
                await GameMatchCache.SetAsync(GamePlatform.Steam, "220", Match(now));

                Assert.NotNull(await GameMatchCache.GetAsync(GamePlatform.Steam, "220", now));
            }
        }

        [Fact]
        public async Task Ignores_an_entry_with_no_store_id()
        {
            using (var temp = new TempFolder())
            {
                GameMatchCache.RecordFolder = temp.Folder;

                await GameMatchCache.SetAsync(GamePlatform.Steam, string.Empty, Match(now));

                Assert.Null(await GameMatchCache.GetAsync(GamePlatform.Steam, string.Empty, now));
                Assert.Null(await GameMatchCache.GetAsync(GamePlatform.Steam, null, now));
            }
        }
    }
}
