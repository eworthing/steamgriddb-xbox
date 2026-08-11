using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Windows.Storage.Streams;

using SteamGridDB.Xbox.Services.Library;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Stands in for the widget while a bulk operation runs.
    ///
    /// The three library-wide operations were untestable until <see cref="IArtworkTarget{TGame}"/>
    /// gave them somewhere to write to that is not a XAML page. This is that somewhere: it records
    /// what it was told and what it was asked to do, and answers however the test needs it to.
    ///
    /// <see cref="LibraryRow"/> is the game type throughout, because it already implements
    /// <see cref="ILibraryGame"/> for the loader's benefit and so needs no second stand-in.
    /// </summary>
    internal sealed class RecordingArtworkTarget : IArtworkTarget<LibraryRow>
    {
        /// <summary>Every status line, in the order it was reported.</summary>
        internal List<string> Reports { get; } = new List<string>();

        /// <summary>Every game artwork was written to, in order.</summary>
        internal List<LibraryRow> Applied { get; } = new List<LibraryRow>();

        /// <summary>Every game a backup was put back for, in order.</summary>
        internal List<LibraryRow> Restored { get; } = new List<LibraryRow>();

        /// <summary>Every game whose image was re-read, in order.</summary>
        internal List<LibraryRow> Refreshed { get; } = new List<LibraryRow>();

        /// <summary>How a restore of a given game should turn out. Defaults to restored.</summary>
        internal Func<LibraryRow, RestoreBackupResult> RestoreOutcome { get; set; } =
            _ => RestoreBackupResult.Restored;

        /// <summary>The last status line reported, or null when there was none.</summary>
        internal string LastReport => Reports.Count == 0 ? null : Reports[Reports.Count - 1];

        public Task ReportAsync(string status)
        {
            Reports.Add(status);

            return Task.CompletedTask;
        }

        public Task<WriteResult> ApplyAsync(LibraryRow game, IBuffer artwork, int artworkId)
        {
            Applied.Add(game);

            return Task.FromResult(WriteResult.Success);
        }

        public Task<WriteResult> ApplyFromUrlAsync(LibraryRow game, string artworkUrl, int artworkId)
        {
            Applied.Add(game);

            return Task.FromResult(WriteResult.Success);
        }

        public Task<RestoreBackupResult> RestoreBackupAsync(LibraryRow game)
        {
            Restored.Add(game);

            return Task.FromResult(RestoreOutcome(game));
        }

        public Task RefreshAsync(LibraryRow game, string imageFileName)
        {
            Refreshed.Add(game);

            return Task.CompletedTask;
        }
    }
}
