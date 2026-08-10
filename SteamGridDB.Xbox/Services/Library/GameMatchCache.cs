using System;
using System.Linq;
using System.Threading.Tasks;

using Windows.Data.Json;
using Windows.Storage;

using SteamGridDB.Xbox.Models;
using SteamGridDB.Xbox.Services;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Remembers what SteamGridDB said about a game, so the next library load does not ask again.
    ///
    /// This is the app's single largest source of outbound requests, and almost all of it was repeat
    /// work. <see cref="GameMatchResolver.ResolveAsync"/> makes one <c>/games/{platform}/{id}</c> call
    /// per manifest entry, and the load runs on every widget open and every Refresh - so a hundred and
    /// fifty games opened five times was seven hundred and fifty requests for answers that had not
    /// changed. They are not the kind of answer that changes: a game's SteamGridDB name, its
    /// SteamGridDB ID, and the URL of Valve's own capsule are stable facts about a title that already
    /// exists.
    ///
    /// Two lifetimes, because the two kinds of answer age differently:
    ///
    /// <list type="bullet">
    /// <item>a match is a fact, kept for <see cref="MatchLifetime"/>. The only thing that invalidates
    /// it is SteamGridDB renaming or re-linking the game, which is rare and costs one stale display
    /// name until it expires.</item>
    /// <item>a miss is the absence of a fact, kept only for <see cref="MissLifetime"/>. Someone adding
    /// the game to SteamGridDB is exactly the event a cache must not hide, and it is a far more likely
    /// one than a rename - so a miss is re-asked within days rather than weeks.</item>
    /// </list>
    ///
    /// Nothing here is written from a load that could not reach SteamGridDB. A throttled or failed
    /// lookup looks like a miss from the outside, and caching one would turn a bad minute into days of
    /// a game showing as Unknown; <see cref="GameMatchResolver.ResolveAsync"/> checks the client's
    /// throttle counter before it writes.
    /// </summary>
    internal static class GameMatchCache
    {
        private const string fileName = "game-matches.json";

        /// <summary>How long a match is trusted. See the class remarks for why the two differ.</summary>
        internal static readonly TimeSpan MatchLifetime = TimeSpan.FromDays(30);

        /// <summary>How long a miss is trusted.</summary>
        internal static readonly TimeSpan MissLifetime = TimeSpan.FromDays(2);

        // Same shape and the same reasoning as AppliedArtworkStore's and XboxTileStore's gates: a
        // library load writes one entry per game in quick succession, and a half-written file would be
        // read back as damaged - which here costs a whole library's worth of lookups to rebuild.
        private static readonly JsonRecordStore<Entry> store = new JsonRecordStore<Entry>(
            fileName, "the game match cache", TryReadEntry, WriteEntry);

        /// <summary>
        /// Where the record is kept. Defaults to the widget's own local data.
        ///
        /// Settable for the same reason <see cref="Artwork.AppliedArtworkStore.RecordFolder"/> is:
        /// ApplicationData.Current only resolves inside an app container. Assigning drops the loaded
        /// map, which belongs to whichever folder it was read from.
        /// </summary>
        internal static StorageFolder RecordFolder
        {
            get => store.RecordFolder;
            set => store.RecordFolder = value;
        }

        /// <summary>
        /// One game's resolved identity, as <see cref="GameMatchResolver"/> produced it.
        /// </summary>
        internal readonly struct Entry
        {
            internal Entry(string name, bool matched, string capsuleUrl, int steamGridDbGameId, DateTimeOffset fetched)
            {
                Name = name;
                Matched = matched;
                CapsuleUrl = capsuleUrl;
                SteamGridDbGameId = steamGridDbGameId;
                Fetched = fetched;
            }

            /// <summary>The display name the resolve settled on, match or not.</summary>
            internal string Name { get; }

            /// <summary>Whether SteamGridDB knows this game at all.</summary>
            internal bool Matched { get; }

            /// <summary>Valve's own capsule URL, or null when the game has none.</summary>
            internal string CapsuleUrl { get; }

            /// <summary>SteamGridDB's own game ID when the match came from a name search, else 0.</summary>
            internal int SteamGridDbGameId { get; }

            /// <summary>When this was written, against which its lifetime is measured.</summary>
            internal DateTimeOffset Fetched { get; }
        }

        /// <summary>
        /// The cached answer for a game, or null when there is none or it has aged out.
        /// </summary>
        /// <param name="platform">The entry's platform.</param>
        /// <param name="externalPlatformId">The entry's own store ID.</param>
        /// <param name="now">Current time, against which the entry's lifetime is measured.</param>
        internal static async Task<Entry?> GetAsync(GamePlatform platform, string externalPlatformId, DateTimeOffset now)
        {
            if (string.IsNullOrEmpty(externalPlatformId))
            {
                return null;
            }

            return await store.ReadAsync(map =>
                map.TryGetValue(Key(platform, externalPlatformId), out Entry entry) && IsFresh(entry, now)
                    ? entry
                    : (Entry?)null);
        }

        /// <summary>
        /// Records what a resolve found. Call only when the lookup actually reached SteamGridDB - see
        /// the class remarks.
        /// </summary>
        /// <param name="platform">The entry's platform.</param>
        /// <param name="externalPlatformId">The entry's own store ID.</param>
        /// <param name="entry">What the resolve produced.</param>
        internal static async Task SetAsync(GamePlatform platform, string externalPlatformId, Entry entry)
        {
            if (string.IsNullOrEmpty(externalPlatformId))
            {
                return;
            }

            await store.UpdateAsync(map =>
            {
                map[Key(platform, externalPlatformId)] = entry;

                // Housekeeping on the write rather than the read: the write already has a moment to
                // measure against, and without this the file only ever grows - a machine that has had
                // fifty games uninstalled would carry them forever
                foreach (string stale in map.Where(pair => !IsFresh(pair.Value, entry.Fetched)).Select(pair => pair.Key).ToList())
                {
                    map.Remove(stale);
                }

                // Every entry carries a fresh Fetched timestamp, which is the value the freshness rule
                // above reads - so this write always changes something, even when it only touches the
                // one entry it just set.
                return true;
            });
        }

        /// <summary>
        /// Whether an entry is still within its lifetime.
        ///
        /// An entry dated in the future is stale, not eternally fresh: that is a clock that has moved
        /// backwards since the write, and the safe reading of an age that makes no sense is to ask
        /// again rather than to trust it until the clock catches up.
        /// </summary>
        /// <param name="entry">The entry to judge.</param>
        /// <param name="now">Current time.</param>
        internal static bool IsFresh(Entry entry, DateTimeOffset now)
        {
            TimeSpan age = now - entry.Fetched;

            return age >= TimeSpan.Zero && age < (entry.Matched ? MatchLifetime : MissLifetime);
        }

        /// <summary>
        /// The record's key for a game. Platform-qualified because store IDs are only unique within
        /// their own store, and lowercased because the manifests are not consistent about case.
        /// </summary>
        internal static string Key(GamePlatform platform, string externalPlatformId)
        {
            return $"{platform}/{externalPlatformId}".ToLowerInvariant();
        }

        private static bool TryReadEntry(IJsonValue value, out Entry result)
        {
            if (value.ValueType == JsonValueType.Object)
            {
                result = ReadEntry(value.GetObject());

                return true;
            }

            result = default(Entry);

            return false;
        }

        private static Entry ReadEntry(JsonObject entry)
        {
            return new Entry(
                JsonRead.String(entry, "name"),
                JsonRead.Boolean(entry, "matched"),
                JsonRead.String(entry, "capsule"),
                (int)JsonRead.Number(entry, "id"),
                DateTimeOffset.FromUnixTimeSeconds((long)JsonRead.Number(entry, "fetched")));
        }

        private static JsonObject WriteEntry(Entry entry)
        {
            var written = new JsonObject
            {
                ["matched"] = JsonValue.CreateBooleanValue(entry.Matched),
                ["id"] = JsonValue.CreateNumberValue(entry.SteamGridDbGameId),
                ["fetched"] = JsonValue.CreateNumberValue(entry.Fetched.ToUnixTimeSeconds())
            };

            // Written only when there is something to write: an absent name and an absent capsule are
            // both normal, and JsonValue has no null of its own to put in their place
            if (!string.IsNullOrEmpty(entry.Name))
            {
                written["name"] = JsonValue.CreateStringValue(entry.Name);
            }

            if (!string.IsNullOrEmpty(entry.CapsuleUrl))
            {
                written["capsule"] = JsonValue.CreateStringValue(entry.CapsuleUrl);
            }

            return written;
        }
    }
}
