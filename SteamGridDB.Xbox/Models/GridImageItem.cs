using Windows.UI.Xaml;

using SteamGridDB.Xbox.Services.Artwork;

namespace SteamGridDB.Xbox.Models
{
    /// <summary>
    /// Represents a grid image item for display in the selection panel.
    ///
    /// Built once per tile by <see cref="GridImageItem(GridSelectionItems.Result)"/> and never mutated
    /// afterward - PrimaryWidget.xaml.cs only reads <see cref="Url"/>, <see cref="Id"/> and
    /// <see cref="SessionId"/> back out of it - so this carries plain auto-properties rather than
    /// INotifyPropertyChanged, and the three members PrimaryWidget.xaml binds
    /// (<see cref="Description"/>, <see cref="ThumbUrl"/>, <see cref="AppliedVisibility"/>) are bound
    /// Mode=OneTime.
    /// </summary>
    public class GridImageItem
    {
        internal GridImageItem(GridSelectionItems.Result artwork)
        {
            Id = artwork.Id;
            Url = artwork.Url;
            ThumbUrl = artwork.ThumbUrl;
            Author = artwork.Author;
            Style = artwork.Style;
            Width = artwork.Width;
            Height = artwork.Height;
            IsApplied = artwork.IsApplied;
            SessionId = artwork.SessionId;
        }

        public string Url
        {
            get; set;
        }

        public string ThumbUrl
        {
            get; set;
        }

        public string Style
        {
            get; set;
        }

        public string Author
        {
            get; set;
        }

        public int Width
        {
            get; set;
        }

        public int Height
        {
            get; set;
        }

        /// <summary>
        /// Style, uploader and resolution of this artwork, shown as the thumbnail's tooltip so the data
        /// behind the ordering is visible when picking artwork by hand. Community score is deliberately
        /// absent: SteamGridDB retired it and always reports 0, so showing it implied a ranking signal
        /// that does not exist. Icons in .ico format report no size, so the resolution line is dropped
        /// rather than shown as 0x0.
        /// </summary>
        public string Description => Width > 0 && Height > 0
            ? $"Style: {Style}\nAuthor: {Author}\nSize: {Width}x{Height}"
            : $"Style: {Style}\nAuthor: {Author}";

        public int Id
        {
            get; set;
        }

        /// <summary>
        /// True when this is the artwork currently on the game's tile. Nothing on disk records that, so
        /// it comes from what the widget remembered when it wrote the image.
        /// </summary>
        public bool IsApplied
        {
            get; set;
        }

        /// <summary>
        /// Shows the "in use" marker on the thumbnail.
        /// </summary>
        public Visibility AppliedVisibility => IsApplied ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// Which picker session this tile was created for. PrimaryWidget's GridImage_Click compares
        /// this against the panel's current session before writing artwork, so a tile left over from a
        /// superseded population (the picker was opened again, for the same game or a different one,
        /// before this tile was clicked) is ignored instead of applying its artwork to whichever game
        /// happens to be selected by the time the click is handled.
        /// </summary>
        public int SessionId
        {
            get; set;
        }
    }
}
