using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace SteamGridDB.Xbox.Models
{
    public class GameEntry : INotifyPropertyChanged
    {
        private string name;
        private string externalPlatformId;
        private GamePlatform platform;
        private DateTime addedDate;
        private string imageFileName;
        private string imageFilePath;
        private BitmapImage image;
        private bool hasBackup;
        private bool hasSteamGridDBMatch;

        public StorageFolder ImageFolder
        {
            get; set;
        }

        public string Name
        {
            get => name;
            set
            {
                if (name != value)
                {
                    name = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// The game's ID in its own store, in the form SteamGridDB expects (for Epic that is the
        /// appName, the last segment of the Xbox manifest entry - not the catalog item ID).
        /// </summary>
        public string ExternalPlatformId
        {
            get => externalPlatformId;
            set
            {
                if (externalPlatformId != value)
                {
                    externalPlatformId = value;
                    OnPropertyChanged();
                }
            }
        }

        public GamePlatform Platform
        {
            get => platform;
            set
            {
                if (platform != value)
                {
                    platform = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime AddedDate
        {
            get => addedDate;
            set
            {
                if (addedDate != value)
                {
                    addedDate = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AddedDateFormatted));
                }
            }
        }

        public string AddedDateFormatted => AddedDate.ToString();

        public string ImageFileName
        {
            get => imageFileName;
            set
            {
                if (imageFileName != value)
                {
                    imageFileName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ImageFilePath
        {
            get => imageFilePath;
            set
            {
                if (imageFilePath != value)
                {
                    imageFilePath = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasSteamGridDBMatch
        {
            get => hasSteamGridDBMatch;
            set
            {
                if (hasSteamGridDBMatch != value)
                {
                    hasSteamGridDBMatch = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EditButtonVisibility));
                    OnPropertyChanged(nameof(SearchButtonVisibility));
                }
            }
        }

        /// <summary>
        /// Valve's own library capsule for this game, resolved during the SteamGridDB name lookup and
        /// used to sanity-check auto-selected artwork. Null when Valve has no capsule, or when the game
        /// is not linked to Steam.
        /// </summary>
        public string OfficialCapsuleUrl
        {
            get; set;
        }

        /// <summary>
        /// SteamGridDB's own ID for this game, set when it was found by name because no store ID
        /// matched. Zero otherwise, in which case artwork is fetched by store ID.
        /// </summary>
        public int SteamGridDbGameId
        {
            get; set;
        }

        // Edit button visible when there is a match
        public Visibility EditButtonVisibility => HasSteamGridDBMatch ? Visibility.Visible : Visibility.Collapsed;

        // Search button visible when there is no match
        public Visibility SearchButtonVisibility => !HasSteamGridDBMatch ? Visibility.Visible : Visibility.Collapsed;

        public bool HasBackup
        {
            get => hasBackup;
            set
            {
                if (hasBackup != value)
                {
                    hasBackup = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(RestoreButtonVisibility));
                }
            }
        }

        public Visibility RestoreButtonVisibility => HasBackup ? Visibility.Visible : Visibility.Collapsed;

        public BitmapImage Image
        {
            get => image;
            set
            {
                if (image != value)
                {
                    image = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasImage));
                    OnPropertyChanged(nameof(ImageVisibility));
                    OnPropertyChanged(nameof(PlaceholderVisibility));
                }
            }
        }

        public bool HasImage => Image != null;
        public Visibility ImageVisibility => Image != null ? Visibility.Visible : Visibility.Collapsed;
        public Visibility PlaceholderVisibility => Image == null ? Visibility.Visible : Visibility.Collapsed;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
