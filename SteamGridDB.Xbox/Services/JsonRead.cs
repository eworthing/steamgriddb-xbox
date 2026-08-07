using Windows.Data.Json;

namespace SteamGridDB.Xbox.Services
{
    /// <summary>
    /// Named lookups that tolerate a member being absent, explicitly null, or the wrong type.
    ///
    /// Windows.Data.Json's own defaulting overloads only substitute the default when the member is
    /// missing. A member that is present and JSON null throws InvalidOperationException, and
    /// GetNamedString additionally rejects a null default outright, because its fallback crosses the
    /// WinRT boundary as an HSTRING which cannot be null.
    ///
    /// Both cases shipped. Reading the Steam app ID with a null default threw for every game that had
    /// Steam platform data, and the catch above it turned each one into "Valve has no artwork for this
    /// game" - which is true for some games, so a bug affecting every game looked exactly like the
    /// truth. Use these rather than the built-in overloads.
    /// </summary>
    internal static class JsonRead
    {
        /// <summary>
        /// A member, or null when it is absent.
        /// </summary>
        public static IJsonValue Value(JsonObject source, string name)
        {
            return source != null && source.ContainsKey(name) ? source[name] : null;
        }

        /// <summary>
        /// A member as an object, or null when it is absent, null or another type.
        /// </summary>
        public static JsonObject Object(JsonObject source, string name)
        {
            IJsonValue value = Value(source, name);

            return value?.ValueType == JsonValueType.Object ? value.GetObject() : null;
        }

        /// <summary>
        /// A member as an array, or null when it is absent, null or another type.
        /// </summary>
        public static JsonArray Array(JsonObject source, string name)
        {
            IJsonValue value = Value(source, name);

            return value?.ValueType == JsonValueType.Array ? value.GetArray() : null;
        }

        /// <summary>
        /// A member as a string, or null when it is absent, null or another type.
        /// </summary>
        public static string String(JsonObject source, string name)
        {
            IJsonValue value = Value(source, name);

            return value?.ValueType == JsonValueType.String ? value.GetString() : null;
        }

        /// <summary>
        /// A member as a number, or <paramref name="fallback"/> when it is absent, null or another type.
        /// </summary>
        public static double Number(JsonObject source, string name, double fallback = 0)
        {
            IJsonValue value = Value(source, name);

            return value?.ValueType == JsonValueType.Number ? value.GetNumber() : fallback;
        }

        /// <summary>
        /// A member as a boolean, or <paramref name="fallback"/> when it is absent, null or another type.
        /// </summary>
        public static bool Boolean(JsonObject source, string name, bool fallback = false)
        {
            IJsonValue value = Value(source, name);

            return value?.ValueType == JsonValueType.Boolean ? value.GetBoolean() : fallback;
        }
    }
}
