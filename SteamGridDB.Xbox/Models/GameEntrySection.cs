using System.Collections.ObjectModel;

namespace SteamGridDB.Xbox.Models
{
    /// <summary>
    /// A titled group of rows in the library list.
    ///
    /// The list is grouped because the two halves of it are not managed the same way and the
    /// difference is worth seeing. A third-party game's tile is a file in a folder the Xbox app fills
    /// and then leaves alone. A first-party game's is an entry in a cache the app owns and refreshes
    /// on its own schedule, so its artwork is put back on every load rather than written once - which
    /// also means a change there can be undone by the Xbox app between two loads in a way a
    /// third-party one cannot.
    ///
    /// Grouping is done with a collection of these rather than by sorting a flat list, because that is
    /// what a ListView's own grouping wants: a source of groups, each of which is itself the
    /// collection of its items. The rows are the same <see cref="GameEntry"/> instances the widget
    /// works with elsewhere, so property changes reach the UI without the group knowing anything.
    /// </summary>
    public class GameEntrySection : ObservableCollection<GameEntry>
    {
        public GameEntrySection(string title)
        {
            Title = title;
        }

        /// <summary>Heading shown above this group's rows.</summary>
        public string Title
        {
            get;
        }
    }
}
