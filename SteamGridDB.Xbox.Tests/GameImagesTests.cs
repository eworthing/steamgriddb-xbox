using System.Collections.Generic;
using System.Linq;

using SteamGridDB.Xbox.Services.Library;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Grouping library entries by the image behind them.
    ///
    /// Tested against a plain record rather than GameEntry, which binds to Windows.UI.Xaml - the point
    /// of the key-selector shape is that this logic never needed to know about the entry type.
    /// </summary>
    public class GameImagesTests
    {
        private sealed class Entry
        {
            internal Entry(string name, string imagePath)
            {
                Name = name;
                ImagePath = imagePath;
            }

            internal string Name { get; }

            internal string ImagePath { get; }
        }

        private static List<Entry> Entries(params (string Name, string Path)[] entries)
        {
            return entries.Select(e => new Entry(e.Name, e.Path)).ToList();
        }

        [Fact]
        public void Visits_an_image_once_however_many_entries_point_at_it()
        {
            // Without this a bulk run does the same file twice, and the second pass sees the artwork
            // the first pass wrote as if it were the Xbox app's original.
            List<Entry> entries = Entries(
                ("Halo from Steam", @"C:\img\a.png"),
                ("Halo from Epic", @"C:\img\a.png"),
                ("Doom", @"C:\img\b.png"));

            List<Entry> distinct = GameImages.DistinctByImage(entries, e => e.ImagePath);

            Assert.Equal(new[] { "Halo from Steam", "Doom" }, distinct.Select(e => e.Name));
        }

        [Fact]
        public void Ignores_case_when_deciding_two_entries_share_an_image()
        {
            // Manifest entries and folder enumeration disagree about casing on Windows.
            List<Entry> entries = Entries(
                ("one", @"C:\img\A.PNG"),
                ("two", @"c:\img\a.png"));

            Assert.Single(GameImages.DistinctByImage(entries, e => e.ImagePath));
        }

        [Fact]
        public void Keeps_the_order_the_entries_arrived_in()
        {
            // The library is sorted before the bulk operations run, and progress counts up that order.
            List<Entry> entries = Entries(
                ("c", @"C:\img\c.png"),
                ("a", @"C:\img\a.png"),
                ("b", @"C:\img\b.png"));

            Assert.Equal(new[] { "c", "a", "b" },
                GameImages.DistinctByImage(entries, e => e.ImagePath).Select(e => e.Name));
        }

        [Fact]
        public void Finds_every_entry_showing_the_same_image()
        {
            // Without this the duplicate rows keep showing the previous artwork, and the previous
            // restore button, until the next refresh.
            List<Entry> entries = Entries(
                ("Halo from Steam", @"C:\img\a.png"),
                ("Halo from Epic", @"C:\img\a.png"),
                ("Doom", @"C:\img\b.png"));

            List<Entry> shared = GameImages.SharingImage(entries, entries[0], e => e.ImagePath);

            Assert.Equal(new[] { "Halo from Steam", "Halo from Epic" }, shared.Select(e => e.Name));
        }

        [Fact]
        public void An_entry_with_no_duplicates_is_returned_alone()
        {
            List<Entry> entries = Entries(("Doom", @"C:\img\b.png"));

            Assert.Single(GameImages.SharingImage(entries, entries[0], e => e.ImagePath));
        }

        [Fact]
        public void An_entry_the_collection_no_longer_holds_is_still_returned()
        {
            // A refresh that landed mid-operation. Dropping it would leave the row the user pressed as
            // the one row left stale.
            List<Entry> entries = Entries(("Doom", @"C:\img\b.png"));

            var removed = new Entry("Halo", @"C:\img\gone.png");

            List<Entry> shared = GameImages.SharingImage(entries, removed, e => e.ImagePath);

            Assert.Equal(new[] { "Halo" }, shared.Select(e => e.Name));
        }
    }
}
