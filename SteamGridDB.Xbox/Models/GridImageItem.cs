using System.ComponentModel;
using Windows.UI.Xaml;
using System.Runtime.CompilerServices;

namespace SteamGridDB.Xbox.Models
{
    /// <summary>
    /// Represents a grid image item for display in the selection panel.
    /// </summary>
    public class GridImageItem : INotifyPropertyChanged
    {
        private string url;
        private string thumbUrl;
        private string style;
        private string author;
        private int width;
        private int height;
        private int id;
        private bool isApplied;

        public string Url
        {
            get => url;
            set
            {
                if (url != value)
                {
                    url = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ThumbUrl
        {
            get => thumbUrl;
            set
            {
                if (thumbUrl != value)
                {
                    thumbUrl = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Style
        {
            get => style;
            set
            {
                if (style != value)
                {
                    style = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public string Author
        {
            get => author;
            set
            {
                if (author != value)
                {
                    author = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public int Width
        {
            get => width;
            set
            {
                if (width != value)
                {
                    width = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public int Height
        {
            get => height;
            set
            {
                if (height != value)
                {
                    height = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Description));
                }
            }
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
            get => id;
            set
            {
                if (id != value)
                {
                    id = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// True when this is the artwork currently on the game's tile. Nothing on disk records that, so
        /// it comes from what the widget remembered when it wrote the image.
        /// </summary>
        public bool IsApplied
        {
            get => isApplied;
            set
            {
                if (isApplied != value)
                {
                    isApplied = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AppliedVisibility));
                }
            }
        }

        /// <summary>
        /// Shows the "in use" marker on the thumbnail.
        /// </summary>
        public Visibility AppliedVisibility => IsApplied ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
