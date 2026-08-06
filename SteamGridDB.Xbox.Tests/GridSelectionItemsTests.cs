using System.Collections.Generic;
using System.Linq;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.SteamGridDB.Models;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The ordering and per-tile display rules split out of PopulateGridSelectionPanelAsync (see
    /// PrimaryWidget.xaml.cs): which of the ranked grids/icons come first, and the fallback fields
    /// (thumbnail, author, style) and "already applied" flag each tile gets. Ranking itself
    /// (ArtworkRanker.RankGrids/RankIcons) is covered by ArtworkRankerTests.cs and is treated as a
    /// black box here - these tests use single-candidate lists so ranking order cannot mask a bug in
    /// the combining/mapping logic under test.
    /// </summary>
    public class GridSelectionItemsTests
    {
        private const string UnknownAuthor = "Unknown";

        private static SteamGridDbGrid Grid(int id, string url = "https://example.test/full.png", string thumb = null, string author = null, string style = null, int width = 512, int height = 512)
        {
            return new SteamGridDbGrid
            {
                Id = id,
                Url = url,
                Thumb = thumb,
                Author = author == null ? null : new SteamGridDbAuthor { Name = author },
                Style = style,
                Width = width,
                Height = height
            };
        }

        // ---- Ordering: ranked grids first, then ranked icons ----

        [Fact]
        public void Grids_come_before_icons_regardless_of_argument_order_in_the_result()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(1) };
            List<SteamGridDbGrid> icons = new List<SteamGridDbGrid> { Grid(2) };

            List<GridSelectionItems.Result> result = GridSelectionItems.BuildOrdered(grids, icons, "Game", null, 0, UnknownAuthor);

            Assert.Equal(new[] { 1, 2 }, result.Select(r => r.Id).ToArray());
        }

        [Fact]
        public void Null_grids_list_yields_icons_only()
        {
            List<SteamGridDbGrid> icons = new List<SteamGridDbGrid> { Grid(5) };

            List<GridSelectionItems.Result> result = GridSelectionItems.BuildOrdered(null, icons, "Game", null, 0, UnknownAuthor);

            Assert.Equal(new[] { 5 }, result.Select(r => r.Id).ToArray());
        }

        [Fact]
        public void Null_icons_list_yields_grids_only()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(7) };

            List<GridSelectionItems.Result> result = GridSelectionItems.BuildOrdered(grids, null, "Game", null, 0, UnknownAuthor);

            Assert.Equal(new[] { 7 }, result.Select(r => r.Id).ToArray());
        }

        [Fact]
        public void Both_null_yields_an_empty_result()
        {
            List<GridSelectionItems.Result> result = GridSelectionItems.BuildOrdered(null, null, "Game", null, 0, UnknownAuthor);

            Assert.Empty(result);
        }

        [Fact]
        public void Empty_lists_are_treated_the_same_as_null()
        {
            List<GridSelectionItems.Result> result = GridSelectionItems.BuildOrdered(
                new List<SteamGridDbGrid>(), new List<SteamGridDbGrid>(), "Game", null, 0, UnknownAuthor);

            Assert.Empty(result);
        }

        // ---- Fallback fields ----

        [Fact]
        public void Missing_thumbnail_falls_back_to_the_full_size_url()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(1, url: "https://example.test/full.png", thumb: null) };

            GridSelectionItems.Result result = GridSelectionItems.BuildOrdered(grids, null, "Game", null, 0, UnknownAuthor).Single();

            Assert.Equal("https://example.test/full.png", result.ThumbUrl);
        }

        [Fact]
        public void Present_thumbnail_is_used_as_is()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(1, thumb: "https://example.test/thumb.png") };

            GridSelectionItems.Result result = GridSelectionItems.BuildOrdered(grids, null, "Game", null, 0, UnknownAuthor).Single();

            Assert.Equal("https://example.test/thumb.png", result.ThumbUrl);
        }

        [Fact]
        public void Missing_author_falls_back_to_the_caller_supplied_unknown_name()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(1, author: null) };

            GridSelectionItems.Result result = GridSelectionItems.BuildOrdered(grids, null, "Game", null, 0, "Nobody").Single();

            Assert.Equal("Nobody", result.Author);
        }

        [Fact]
        public void Present_author_name_is_used_as_is()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(1, author: "Some Uploader") };

            GridSelectionItems.Result result = GridSelectionItems.BuildOrdered(grids, null, "Game", null, 0, UnknownAuthor).Single();

            Assert.Equal("Some Uploader", result.Author);
        }

        [Fact]
        public void Missing_style_falls_back_to_default()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(1, style: null) };

            GridSelectionItems.Result result = GridSelectionItems.BuildOrdered(grids, null, "Game", null, 0, UnknownAuthor).Single();

            Assert.Equal("default", result.Style);
        }

        [Fact]
        public void Present_style_is_used_as_is()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(1, style: "alternate") };

            GridSelectionItems.Result result = GridSelectionItems.BuildOrdered(grids, null, "Game", null, 0, UnknownAuthor).Single();

            Assert.Equal("alternate", result.Style);
        }

        // ---- IsApplied ----

        [Fact]
        public void Tile_matching_the_applied_artwork_id_is_marked_applied()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(42) };

            GridSelectionItems.Result result = GridSelectionItems.BuildOrdered(grids, null, "Game", 42, 0, UnknownAuthor).Single();

            Assert.True(result.IsApplied);
        }

        [Fact]
        public void Tile_not_matching_the_applied_artwork_id_is_not_marked_applied()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(42) };

            GridSelectionItems.Result result = GridSelectionItems.BuildOrdered(grids, null, "Game", 99, 0, UnknownAuthor).Single();

            Assert.False(result.IsApplied);
        }

        [Fact]
        public void Null_applied_artwork_id_marks_nothing_applied()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(42) };

            GridSelectionItems.Result result = GridSelectionItems.BuildOrdered(grids, null, "Game", null, 0, UnknownAuthor).Single();

            Assert.False(result.IsApplied);
        }

        // ---- SessionId stamping ----

        [Fact]
        public void Every_tile_is_stamped_with_the_supplied_session_id()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(1), Grid(2) };
            List<SteamGridDbGrid> icons = new List<SteamGridDbGrid> { Grid(3) };

            List<GridSelectionItems.Result> result = GridSelectionItems.BuildOrdered(grids, icons, "Game", null, 7, UnknownAuthor);

            Assert.All(result, r => Assert.Equal(7, r.SessionId));
        }

        // ---- Pass-through fields ----

        [Fact]
        public void Width_and_height_pass_through_unchanged()
        {
            List<SteamGridDbGrid> grids = new List<SteamGridDbGrid> { Grid(1, width: 1024, height: 512) };

            GridSelectionItems.Result result = GridSelectionItems.BuildOrdered(grids, null, "Game", null, 0, UnknownAuthor).Single();

            Assert.Equal(1024, result.Width);
            Assert.Equal(512, result.Height);
        }
    }
}
