using System;
using System.Collections.Generic;
using System.Linq;

namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Grouping library entries by the image file behind them.
    ///
    /// The Xbox app's manifests go stale: a game removed and re-added, or listed by more than one
    /// store, leaves several entries pointing at one image on disk. Every bulk operation therefore has
    /// to visit each image once rather than each entry once, and every single-game operation has to
    /// update every entry showing that image rather than only the one whose button was pressed.
    ///
    /// Generic over how an entry names its image so this stays clear of Windows.UI.Xaml - the entry
    /// type binds to it, this does not need to.
    /// </summary>
    internal static class GameImages
    {
        /// <summary>
        /// One entry per distinct image, keeping the first of each and the order they arrived in.
        ///
        /// Without this a bulk run does the same file twice: the second pass sees the artwork the first
        /// pass wrote as if it were the Xbox app's original.
        /// </summary>
        /// <param name="entries">Library entries.</param>
        /// <param name="imagePath">An entry's image path.</param>
        internal static List<T> DistinctByImage<T>(IEnumerable<T> entries, Func<T, string> imagePath)
        {
            return entries
                .GroupBy(imagePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// Every entry backed by the same image as <paramref name="entry"/>, including it.
        ///
        /// Without this the duplicate rows keep showing the previous artwork, and the previous
        /// restore/revert buttons, until the next refresh.
        /// </summary>
        /// <param name="entries">All library entries.</param>
        /// <param name="entry">The entry whose image is in question.</param>
        /// <param name="imagePath">An entry's image path.</param>
        internal static List<T> SharingImage<T>(IEnumerable<T> entries, T entry, Func<T, string> imagePath)
        {
            string path = imagePath(entry);

            List<T> shared = entries
                .Where(e => string.Equals(imagePath(e), path, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (shared.Count == 0)
            {
                // An entry the collection no longer holds - a refresh that landed mid-operation. It
                // still has to be updated, or the row the user pressed is the one row left stale.
                shared.Add(entry);
            }

            return shared;
        }
    }
}
