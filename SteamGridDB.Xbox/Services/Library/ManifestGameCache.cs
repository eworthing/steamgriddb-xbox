using System.Collections.Generic;

using Windows.Data.Json;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Reads the entries out of one Xbox app manifest's "gameCache" object - the manifest-shape half
    /// of LoadGameEntriesAsync's folder loop (see PrimaryWidget.xaml.cs) that has no dependency on
    /// image decode, backup state, or network results: it only cares about the JSON's own shape and
    /// the entry's raw "id" field, which every platform's own parsing (<see cref="ManifestEntryIdentity"/>,
    /// <see cref="ManifestEntryImage"/>) then reads off of.
    ///
    /// The "id" field is read here rather than deferred into that per-entry parsing because this read
    /// cannot throw, and because an entry with no ID is not a stale one: it names nothing on disk that
    /// could have gone missing, so it is left out of the result without being counted, exactly as it
    /// always was.
    ///
    /// Split out so the manifest-shape rules - especially the "id" field's null-handling, which shipped
    /// broken once already (see below) - are exercised directly rather than only by inspection deep
    /// inside a UI-bound method.
    /// </summary>
    internal static class ManifestGameCache
    {
        /// <summary>
        /// Every usable entry in a manifest's "gameCache" object, in the order they appear.
        ///
        /// Returns an empty list, rather than throwing, when the JSON does not parse, when it has no
        /// "gameCache" member, or when that member is not an object - all three are "nothing to load
        /// from this manifest", not an error the caller needs to distinguish, and the folder-level
        /// catch this replaces never distinguished them either.
        /// </summary>
        /// <param name="json">A manifest file's raw contents.</param>
        /// <returns>Each entry's "id" field and its own JSON object.</returns>
        internal static List<(string Id, JsonObject Entry)> Entries(string json)
        {
            List<(string Id, JsonObject Entry)> results = new List<(string Id, JsonObject Entry)>();

            if (!JsonObject.TryParse(json, out JsonObject root))
            {
                return results;
            }

            // Get the gameCache object
            JsonObject gameCache = JsonRead.Object(root, "gameCache");

            if (gameCache == null)
            {
                return results;
            }

            // Iterate through all entries in the gameCache
            foreach (KeyValuePair<string, IJsonValue> entry in gameCache)
            {
                // Skip the "version" property if it exists
                if (entry.Key == "version")
                {
                    continue;
                }

                // Only process entries that are objects
                if (entry.Value.ValueType != JsonValueType.Object)
                {
                    continue;
                }

                JsonObject entryObject = entry.Value.GetObject();

                // From the "id" property, not the gameCache key, and read with
                // JsonRead: that treats a present-but-JSON-null "id" the same as a
                // missing one, where the raw GetNamedString/ContainsKey pair it
                // replaced did not - ContainsKey returns true for a null-valued
                // member and GetNamedString throws on one, which nothing caught, so
                // a single null "id" silently dropped every entry after it in the
                // folder (JsonRead.cs's docstring has the same failure class
                // shipping once already).
                string entryId = JsonRead.String(entryObject, "id");

                if (string.IsNullOrEmpty(entryId))
                {
                    continue;
                }

                results.Add((entryId, entryObject));
            }

            return results;
        }
    }
}
