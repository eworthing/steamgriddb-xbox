using System.Collections.Generic;

using Windows.Storage;

using SteamGridDB.Xbox.Models;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// A library row, as everything outside the widget sees one.
    ///
    /// The bulk operations - fixing, restoring, reverting - are loops over library rows, and they
    /// lived in PrimaryWidget for one reason: they took a <c>GameEntry</c>, which exposes
    /// <c>Visibility</c> and <c>BitmapImage</c> and so binds to Windows.UI.Xaml. Windows.UI.Xaml has
    /// no desktop projection, so anything that names it cannot be linked into the test project, and
    /// the loops were therefore untestable in a file that already had no tests at all.
    ///
    /// This is the seam that lets them leave. It is the same trick
    /// <see cref="GameImages"/> uses to stay clear of the same dependency (it is generic over how a
    /// row names its image) and the same one <c>ArtworkSource.SourceFor</c> uses (it takes the three
    /// primitives the decision needs rather than the row) - just written once, in one place, rather
    /// than rediscovered per caller.
    ///
    /// Deliberately read-only. Every write to a row goes through PrimaryWidget's two UI chokepoints,
    /// <c>UpdateSharedEntriesAsync</c> and <c>WrittenThumbnailAsync</c>, because a row's image is a
    /// <c>BitmapImage</c> owned by the UI thread and because a write has to reach every row sharing
    /// that image, not only the one in hand. Adding a setter here would let a loop update one row
    /// off the UI thread and leave its duplicates stale - the exact failure
    /// <see cref="GameImages.SharingImage{T}"/> exists to prevent. Rows are handed to the loops to be
    /// read; what to do about them comes back as a result.
    ///
    /// Public rather than internal because <c>GameEntry</c> is public - XAML binds it - and a public
    /// class may not be constrained by a less accessible interface. <c>ArtworkSource</c> and
    /// <c>SteamGridDbClient</c> are public under Services\ for their own reasons, so this is not a
    /// new exception.
    /// </summary>
    public interface ILibraryGame
    {
        /// <summary>The game's name, or "Unknown" when the manifests never gave it one.</summary>
        string Name { get; }

        /// <summary>Which store this game came from.</summary>
        GamePlatform Platform { get; }

        /// <summary>The game's ID in its own store, in the form SteamGridDB expects.</summary>
        string ExternalPlatformId { get; }

        /// <summary>SteamGridDB's own ID, set when the game was found by name. Zero otherwise.</summary>
        int SteamGridDbGameId { get; }

        /// <summary>Valve's own library capsule for this game, or null. Used to sanity-check auto-selected artwork.</summary>
        string OfficialCapsuleUrl { get; }

        /// <summary>Whether SteamGridDB knows this game at all - nothing can be fetched without it.</summary>
        bool HasSteamGridDBMatch { get; }

        /// <summary>Whether the original artwork has been backed up, and so whether it can be restored.</summary>
        bool HasBackup { get; }

        /// <summary>Whether this row is a first-party game backed by the Xbox app's image cache.</summary>
        bool IsXboxTile { get; }

        /// <summary>The folder holding this game's image.</summary>
        StorageFolder ImageFolder { get; }

        /// <summary>The image's name within <see cref="ImageFolder"/>.</summary>
        string ImageFileName { get; }

        /// <summary>
        /// Full path of the image that stands in for this game - for a first-party game the largest
        /// rendition. Every operation keys off this: deduplicating rows, recording which artwork was
        /// applied, finding the other rows showing the same file.
        /// </summary>
        string ImageFilePath { get; }

        /// <summary>
        /// For a first-party game, every cached image making up its tile, largest first. Null for
        /// third-party games, which are one file each.
        /// </summary>
        IReadOnlyList<string> XboxRenditions { get; }
    }
}
