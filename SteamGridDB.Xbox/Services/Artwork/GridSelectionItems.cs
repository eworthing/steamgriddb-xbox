using System.Collections.Generic;
using System.Linq;

using SteamGridDB.Xbox.Services.SteamGridDB.Models;

namespace SteamGridDB.Xbox.Services.Artwork
{
    /// <summary>
    /// Builds the ordered, display-ready artwork list the picker panel shows - the part of
    /// PopulateGridSelectionPanelAsync (see PrimaryWidget.xaml.cs) that decides tile order and each
    /// tile's fallback fields, split out from the part that actually populates the XAML-bound
    /// GridImagesView. Returns plain data rather than GridImageItem, which binds to
    /// Windows.UI.Xaml.Visibility and has no desktop test projection - the same constraint
    /// GameImagesTests.cs documents for GameEntry.
    ///
    /// Ranking itself stays in <see cref="ArtworkRanker"/>, already tested on its own; this class owns
    /// only the ordering of the two ranked lists (grids first, then icons) and the per-tile display
    /// rules (thumbnail fallback, unknown-author fallback, default style, which tile is already applied)
    /// that previously lived inline in the foreach loop that built each GridImageItem.
    /// </summary>
    internal static class GridSelectionItems
    {
        /// <summary>
        /// One artwork tile's display-ready fields, in picker order.
        /// </summary>
        internal readonly struct Result
        {
            internal Result(int id, string url, string thumbUrl, string author, string style, int width, int height, bool isApplied, int sessionId)
            {
                Id = id;
                Url = url;
                ThumbUrl = thumbUrl;
                Author = author;
                Style = style;
                Width = width;
                Height = height;
                IsApplied = isApplied;
                SessionId = sessionId;
            }

            internal int Id { get; }

            internal string Url { get; }

            internal string ThumbUrl { get; }

            internal string Author { get; }

            internal string Style { get; }

            internal int Width { get; }

            internal int Height { get; }

            internal bool IsApplied { get; }

            internal int SessionId { get; }
        }

        /// <param name="grids">Square grid candidates, or null/empty when there are none.</param>
        /// <param name="icons">Icon candidates, or null/empty when there are none.</param>
        /// <param name="gameName">Game name, passed through to <see cref="ArtworkRanker.RankGrids"/>.</param>
        /// <param name="appliedArtworkId">The artwork currently on the game's tile, or null when none is recorded.</param>
        /// <param name="sessionId">The picker session these results belong to, stamped onto every tile.</param>
        /// <param name="unknownAuthorName">Fallback author name when SteamGridDB reports none.</param>
        /// <returns>Ranked grids first, then ranked icons; empty when both inputs are null/empty.</returns>
        internal static List<Result> BuildOrdered(
            IList<SteamGridDbGrid> grids,
            IList<SteamGridDbGrid> icons,
            string gameName,
            int? appliedArtworkId,
            int sessionId,
            string unknownAuthorName)
        {
            List<SteamGridDbGrid> sortedArtworks = new List<SteamGridDbGrid>();

            if (grids != null && grids.Count > 0)
            {
                sortedArtworks.AddRange(ArtworkRanker.RankGrids(grids, gameName));
            }

            if (icons != null && icons.Count > 0)
            {
                sortedArtworks.AddRange(ArtworkRanker.RankIcons(icons));
            }

            return sortedArtworks.Select(artwork => new Result(
                artwork.Id,
                artwork.Url,
                artwork.Thumb ?? artwork.Url,
                artwork.Author?.Name ?? unknownAuthorName,
                artwork.Style ?? "default",
                artwork.Width,
                artwork.Height,
                artwork.Id == appliedArtworkId,
                sessionId)).ToList();
        }
    }
}
