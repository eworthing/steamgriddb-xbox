using System;
using System.Collections.Generic;

using Windows.Storage;

using SteamGridDB.Xbox.Models;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// One library row as <see cref="LibraryLoader"/> produces it, before the widget turns it into the
    /// bound <c>GameEntry</c> the list shows.
    ///
    /// This exists so the load can finish outside the widget. Everything a row is made of - which
    /// manifest entry it came from, where its image sits, what the game is called, whether SteamGridDB
    /// knows it - is decided by code that can be tested; the one step that cannot is decoding
    /// <see cref="ThumbnailSource"/> into a <c>BitmapImage</c>, because that type is owned by the UI
    /// thread. So the loader carries the file and the widget does the decode, which is the same place
    /// <c>ManifestEntryImage</c> already stops.
    ///
    /// Settable auto-properties rather than the get-only <c>Result</c> struct the smaller services in
    /// this namespace use: those carry two to five values and a constructor reads clearly, thirteen
    /// positional arguments does not. Nothing mutates a row after the loader hands it back.
    ///
    /// Implements <see cref="ILibraryGame"/>, which makes it the natural stand-in for a real row in
    /// tests of anything that reads one.
    /// </summary>
    internal sealed class LibraryRow : ILibraryGame
    {
        public string Name { get; set; }

        public GamePlatform Platform { get; set; }

        public string ExternalPlatformId { get; set; }

        public int SteamGridDbGameId { get; set; }

        public string OfficialCapsuleUrl { get; set; }

        public bool HasSteamGridDBMatch { get; set; }

        public bool HasBackup { get; set; }

        public StorageFolder ImageFolder { get; set; }

        public string ImageFileName { get; set; }

        public string ImageFilePath { get; set; }

        public IReadOnlyList<string> XboxRenditions { get; set; }

        public bool IsXboxTile => XboxRenditions != null && XboxRenditions.Count > 0;

        /// <summary>
        /// When the Xbox app added this game to its manifest. Left unset for a first-party game, which
        /// is enumerated from what is installed rather than listed with a date - see
        /// <c>GameEntry.AddedDateSuffix</c> for why an unset value is shown as nothing at all.
        /// </summary>
        public DateTime AddedDate { get; set; }

        /// <summary>
        /// The image file to decode for this row's thumbnail, or null when there is none to decode -
        /// a manifest entry whose artwork the Xbox app has not fetched but which has a backup, or a
        /// first-party game whose cached tile could not be opened.
        /// </summary>
        public StorageFile ThumbnailSource { get; set; }
    }
}
