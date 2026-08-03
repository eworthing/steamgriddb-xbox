using System.Runtime.Serialization;

namespace SteamGridDB.Xbox.Services.SteamGridDB.Models
{
    /// <summary>
    /// Represents grid art/image from SteamGridDB.
    /// </summary>
    [DataContract]
    public class SteamGridDbGrid
    {
        [DataMember(Name = "id")]
        public int Id
        {
            get; set;
        }

        [DataMember(Name = "style")]
        public string Style
        {
            get; set;
        }

        /// <summary>
        /// Pixel width of the full-size image. Grids are requested at 512x512 and 1024x1024 together,
        /// so this is the only way to tell the two apart. Icons in .ico format report 0.
        /// </summary>
        [DataMember(Name = "width")]
        public int Width
        {
            get; set;
        }

        /// <summary>
        /// Pixel height of the full-size image. See <see cref="Width"/>.
        /// </summary>
        [DataMember(Name = "height")]
        public int Height
        {
            get; set;
        }

        /// <summary>
        /// Image format: "image/png", "image/jpeg" or "image/webp" for grids; icons are also served as
        /// "image/vnd.microsoft.icon". Not a quality signal - preferring one format over another graded
        /// worse for grids and made no difference for icons. Used only to group icons of the same kind
        /// so that size can separate them, and to decide whether a tile needs re-encoding on save.
        /// </summary>
        [DataMember(Name = "mime")]
        public string Mime
        {
            get; set;
        }

        [DataMember(Name = "url")]
        public string Url
        {
            get; set;
        }

        [DataMember(Name = "thumb")]
        public string Thumb
        {
            get; set;
        }

        [DataMember(Name = "tags")]
        public string[] Tags
        {
            get; set;
        }

        [DataMember(Name = "language")]
        public string Language
        {
            get; set;
        }

        [DataMember(Name = "notes")]
        public string Notes
        {
            get; set;
        }

        [DataMember(Name = "author")]
        public SteamGridDbAuthor Author
        {
            get; set;
        }
    }
}
