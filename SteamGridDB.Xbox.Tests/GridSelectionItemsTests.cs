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

        /// <summary>Builds a single-grid result, the shape most of these tests need.</summary>
        private static GridSelectionItems.Result Only(SteamGridDbGrid grid, int? applied = null, string unknownAuthor = UnknownAuthor)
        {
            return GridSelectionItems.BuildOrdered(new List<SteamGridDbGrid> { grid }, null, "Game", applied, 0, unknownAuthor).Single();
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
            Assert.Equal(7, Only(Grid(7)).Id);
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
            Assert.Equal(
                "https://example.test/full.png",
                Only(Grid(1, url: "https://example.test/full.png", thumb: null)).ThumbUrl);
        }

        [Fact]
        public void Present_thumbnail_is_used_as_is()
        {
            Assert.Equal("https://example.test/thumb.png", Only(Grid(1, thumb: "https://example.test/thumb.png")).ThumbUrl);
        }

        [Fact]
        public void Missing_author_falls_back_to_the_caller_supplied_unknown_name()
        {
            Assert.Equal("Nobody", Only(Grid(1, author: null), unknownAuthor: "Nobody").Author);
        }

        [Fact]
        public void Present_author_name_is_used_as_is()
        {
            Assert.Equal("Some Uploader", Only(Grid(1, author: "Some Uploader")).Author);
        }

        [Fact]
        public void Missing_style_falls_back_to_default()
        {
            Assert.Equal("default", Only(Grid(1, style: null)).Style);
        }

        [Fact]
        public void Present_style_is_used_as_is()
        {
            Assert.Equal("alternate", Only(Grid(1, style: "alternate")).Style);
        }

        // ---- IsApplied ----

        [Theory]
        [InlineData(42, true)]
        [InlineData(99, false)]
        [InlineData(null, false)]
        public void IsApplied_reflects_whether_the_tile_matches_the_applied_artwork_id(int? appliedArtworkId, bool expected)
        {
            Assert.Equal(expected, Only(Grid(42), applied: appliedArtworkId).IsApplied);
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
            GridSelectionItems.Result result = Only(Grid(1, width: 1024, height: 512));

            Assert.Equal(1024, result.Width);
            Assert.Equal(512, result.Height);
        }
    }
}
