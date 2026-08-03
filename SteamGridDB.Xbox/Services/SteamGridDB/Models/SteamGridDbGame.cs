using System.Runtime.Serialization;

namespace SteamGridDB.Xbox.Services.SteamGridDB.Models
{
    /// <summary>
    /// Represents a game search result from SteamGridDB.
    /// </summary>
    [DataContract]
    public class SteamGridDbGame
    {
        [DataMember(Name = "id")]
        public int Id
        {
            get; set;
        }

        [DataMember(Name = "name")]
        public string Name
        {
            get; set;
        }

        [DataMember(Name = "types")]
        public string[] Types
        {
            get; set;
        }

        [DataMember(Name = "verified")]
        public bool Verified
        {
            get; set;
        }

        /// <summary>
        /// Valve's own library-capsule image for this game, or null when Valve has none. Not a
        /// serialised member: the client fills it in from the platformdata section of the response,
        /// whose per-language keys a data contract cannot describe.
        /// </summary>
        public string OfficialCapsuleUrl
        {
            get; set;
        }
    }
}
