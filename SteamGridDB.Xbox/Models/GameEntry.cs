using System;
using System.Collections.Generic;
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
                    OnPropertyChanged(nameof(AddedDateSuffix));
                }
            }
        }

        public string AddedDateFormatted => AddedDate.ToString();

        /// <summary>
        /// The date for the platform line, separator included, or nothing when there is no date to
        /// show.
        ///
        /// Third-party entries carry the date the Xbox app added them to its manifest. First-party
        /// games have no equivalent - they are enumerated from what is installed, and the only
        /// timestamps near them belong to the cached tile, which this app rewrites - so rather than
        /// print an unset DateTime as "1/1/0001" the segment is left out entirely. The separator lives
        /// here because the line is built from Runs, which cannot be collapsed individually.
        /// </summary>
        public string AddedDateSuffix => AddedDate == default ? string.Empty : $" • {AddedDateFormatted}";

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

        /// <summary>
        /// For a first-party game, every cached image in the Xbox app's image cache that makes up its
        /// tile, largest first. Null for third-party games, which are one file each.
        ///
        /// The Xbox app fetched a game's artwork once per surface it shows it on and cached each size
        /// separately, so a tile only changes everywhere when all of them do.
        /// <see cref="ImageFilePath"/> names the largest, which stands in for the game everywhere one
        /// path is needed - deduplicating rows, recording which artwork was applied, decoding the
        /// thumbnail - while the writes fan out across this list.
        /// </summary>
        public IReadOnlyList<string> XboxRenditions
        {
            get; set;
        }

        /// <summary>Whether this row is a first-party game backed by the Xbox app's image cache.</summary>
        public bool IsXboxTile => XboxRenditions != null && XboxRenditions.Count > 0;

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
