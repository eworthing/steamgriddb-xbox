using System.Collections.Generic;
using System.Linq;

using SteamGridDB.Xbox.Services.Artwork;
using SteamGridDB.Xbox.Services.SteamGridDB.Models;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// Which artwork gets picked.
    ///
    /// This is the part of the app graded by eye - a wrong pick is not an exception, it is a tile that
    /// looks slightly off - so the ranking rules were tuned against a hand-graded set and the reasons
    /// live in comments. These tests hold those reasons in place: several of the rules exist precisely
    /// because the obvious alternative graded worse, and nothing but a test says so at the call site.
    /// </summary>
    public class ArtworkRankerTests
    {
        private static SteamGridDbGrid Grid(
            int id = 1,
            string style = "alternate",
            int width = 512,
            string mime = "image/png",
            string notes = null,
            string language = null,
            params string[] tags)
        {
            return new SteamGridDbGrid
            {
                Id = id,
                Style = style,
                Width = width,
                Height = width,
                Mime = mime,
                Notes = notes,
                Language = language,
                Tags = tags,
            };
        }

        private static int[] IdsOf(IEnumerable<SteamGridDbGrid> grids)
        {
            return grids.Select(g => g.Id).ToArray();
        }

        // ---- Style priority ----

        [Theory]
        [InlineData("alternate")]
        [InlineData("white_logo")]
        [InlineData("blurred")]
        public void Title_bearing_styles_rank_ahead_of_icon_like_ones(string style)
        {
            Assert.Equal(0, ArtworkRanker.GridStylePriority(style));
        }

        [Theory]
        [InlineData("no_logo")]
        [InlineData("material")]
        [InlineData(null)]
        public void Styles_that_look_like_plain_icons_rank_last(string style)
        {
            Assert.Equal(1, ArtworkRanker.GridStylePriority(style));
        }

        [Fact]
        public void Title_bearing_styles_rank_equally_with_each_other()
        {
            // Preferring one over another surfaced mis-tagged fan art, and any of them already matches
            // the native Xbox look.
            Assert.Equal(
                ArtworkRanker.GridStylePriority("alternate"),
                ArtworkRanker.GridStylePriority("blurred"));
        }

        // ---- Demotion ----

        [Theory]
        [InlineData("Physical case mockup")]
        [InlineData("jewel case spine")]
        [InlineData("PS2 cartridge")]
        [InlineData("from wallhaven")]
        public void Physical_media_mockups_are_demoted(string notes)
        {
            Assert.True(ArtworkRanker.IsDemotedGrid(Grid(notes: notes), "Halo"));
        }

        [Fact]
        public void The_word_xbox_does_not_count_as_the_word_box()
        {
            // The mockup vocabulary is word-bounded on purpose. Without that, every upload that
            // mentions Xbox at all - which on an Xbox library is a great many of them - is demoted.
            Assert.False(ArtworkRanker.IsDemotedGrid(Grid(notes: "Xbox store art"), "Halo"));
        }

        [Theory]
        [InlineData("PlayStation Hits edition cover")]
        [InlineData("Nintendo Switch icon")]
        [InlineData("Xbox Series X cover")]
        public void Console_store_reissues_with_a_badge_burned_in_are_demoted(string notes)
        {
            // The art underneath is usually the real cover, which is why the similarity gate rates
            // these highly and why they have to be caught by name instead.
            Assert.True(ArtworkRanker.IsDemotedGrid(Grid(notes: notes), "Halo"));
        }

        [Theory]
        [InlineData("Playstation 4")]
        [InlineData("Playstation 5")]
        [InlineData("Xbox One")]
        [InlineData("Xbox Series S/X")]
        public void A_bare_console_name_is_as_much_a_badge_as_the_badge_shaped_phrasings(string notes)
        {
            // One uploader's set of Far Cry 6 covers, identical but for the console spine down the
            // left edge. The Xbox two were caught and the PlayStation two were not, so "Playstation 4"
            // won the tile on a game whose notes said what was wrong with it.
            Assert.True(ArtworkRanker.IsDemotedGrid(Grid(notes: notes), "Far Cry 6"));
        }

        [Fact]
        public void A_console_name_without_a_generation_is_not_a_badge()
        {
            // "Playstation" alone is how uploaders name the franchise and the storefront, not a spine
            // stamp; only the numbered forms mark art as a specific console's reissue. The Xbox half
            // of this vocabulary is generation-qualified for the same reason, and the separate test
            // above pins that a bare "Xbox" mention does not demote.
            Assert.False(ArtworkRanker.IsDemotedGrid(Grid(notes: "PlayStation store art"), "Halo"));
        }

        [Fact]
        public void Artwork_for_an_edition_the_game_is_not_is_demoted()
        {
            Assert.True(ArtworkRanker.IsDemotedGrid(Grid(notes: "Deluxe Edition"), "Rocket League"));
        }

        [Fact]
        public void Artwork_for_the_edition_the_game_actually_is_stays()
        {
            Assert.False(ArtworkRanker.IsDemotedGrid(Grid(notes: "Deluxe Edition"), "Rocket League Deluxe"));
        }

        [Fact]
        public void An_unresolved_game_name_does_not_demote_every_edition_labelled_upload()
        {
            // There is nothing to compare against, and treating that as a mismatch demoted every
            // edition-labelled candidate for any game whose name did not resolve, on no evidence.
            Assert.False(ArtworkRanker.IsDemotedGrid(Grid(notes: "Definitive Edition"), string.Empty));
            Assert.False(ArtworkRanker.IsDemotedGrid(Grid(notes: "Definitive Edition"), null));
        }

        [Fact]
        public void A_cross_reference_to_other_artwork_is_not_read_as_a_label_on_this_one()
        {
            // SteamGridDB's convention for "the deluxe version of this art is over there". The link
            // text describes a different upload, so matching keywords in it demotes the wrong one.
            Assert.False(ArtworkRanker.IsDemotedGrid(
                Grid(notes: "[>deluxe](https://steamgriddb.com/grid/999)"), "Rocket League"));
        }

        [Fact]
        public void A_source_url_still_counts_against_the_artwork_it_is_on()
        {
            // Unlike a cross-reference, the domain describes this upload's own origin.
            Assert.True(ArtworkRanker.IsDemotedGrid(
                Grid(notes: "grabbed from https://www.deviantart.com/someone/art/123"), "Halo"));
        }

        [Fact]
        public void Tags_count_as_well_as_notes()
        {
            Assert.True(ArtworkRanker.IsDemotedGrid(Grid(tags: new[] { "mockup" }), "Halo"));
        }

        [Fact]
        public void Plain_artwork_with_no_notes_or_tags_is_not_demoted()
        {
            Assert.False(ArtworkRanker.IsDemotedGrid(Grid(), "Halo"));
        }

        // ---- Grid ordering ----

        [Fact]
        public void Demoted_artwork_sorts_behind_everything_else()
        {
            List<SteamGridDbGrid> ranked = ArtworkRanker.RankGrids(
                new[]
                {
                    Grid(id: 1, notes: "physical case mockup"),
                    Grid(id: 2),
                },
                "Halo");

            Assert.Equal(new[] { 2, 1 }, IdsOf(ranked));
        }

        [Fact]
        public void Foreign_language_artwork_sorts_behind_english_and_untagged()
        {
            List<SteamGridDbGrid> ranked = ArtworkRanker.RankGrids(
                new[]
                {
                    Grid(id: 1, language: "ja"),
                    Grid(id: 2, language: "en"),
                    Grid(id: 3, language: null),
                },
                "Halo");

            Assert.Equal(1, ranked.Last().Id);
        }

        [Fact]
        public void Text_bearing_styles_sort_ahead_of_icon_like_styles_in_RankGrids()
        {
            // GridStylePriority is unit-tested directly above, but until now no RankGrids case varied
            // Style between two candidates - so a .ThenBy/.ThenByDescending direction swap on that
            // clause would have passed the whole suite silently.
            List<SteamGridDbGrid> ranked = ArtworkRanker.RankGrids(
                new[]
                {
                    Grid(id: 1, style: "no_logo"),
                    Grid(id: 2, style: "alternate"),
                },
                "Halo");

            Assert.Equal(new[] { 2, 1 }, IdsOf(ranked));
        }

        [Fact]
        public void Official_store_artwork_is_preferred_within_the_same_style()
        {
            List<SteamGridDbGrid> ranked = ArtworkRanker.RankGrids(
                new[]
                {
                    Grid(id: 1),
                    Grid(id: 2, notes: "Official artwork from xbox.com"),
                },
                "Halo");

            Assert.Equal(new[] { 2, 1 }, IdsOf(ranked));
        }

        [Fact]
        public void The_sharper_upload_wins_a_tie()
        {
            // 512x512 and 1024x1024 are requested together, so both arrive for most games.
            List<SteamGridDbGrid> ranked = ArtworkRanker.RankGrids(
                new[]
                {
                    Grid(id: 1, width: 512),
                    Grid(id: 2, width: 1024),
                },
                "Halo");

            Assert.Equal(new[] { 2, 1 }, IdsOf(ranked));
        }

        [Fact]
        public void Format_is_not_a_tie_break()
        {
            // Preferring PNG over JPEG was tried and reverted: it moved 26 picks and graded 2 better
            // against 7 worse, because format says nothing about whether the art is the real cover.
            // Equal on every ranked signal, so the API's own order has to survive.
            List<SteamGridDbGrid> ranked = ArtworkRanker.RankGrids(
                new[]
                {
                    Grid(id: 1, mime: "image/jpeg"),
                    Grid(id: 2, mime: "image/png"),
                },
                "Halo");

            Assert.Equal(new[] { 1, 2 }, IdsOf(ranked));
        }

        [Fact]
        public void Ties_keep_the_order_the_api_returned()
        {
            List<SteamGridDbGrid> ranked = ArtworkRanker.RankGrids(
                new[] { Grid(id: 7), Grid(id: 3), Grid(id: 9) },
                "Halo");

            Assert.Equal(new[] { 7, 3, 9 }, IdsOf(ranked));
        }

        // ---- Icon ordering ----

        [Fact]
        public void The_largest_icon_of_each_kind_comes_first_within_that_kind()
        {
            List<SteamGridDbGrid> ranked = ArtworkRanker.RankIcons(
                new[]
                {
                    Grid(id: 1, mime: "image/png", style: "official", width: 128),
                    Grid(id: 2, mime: "image/png", style: "official", width: 512),
                });

            Assert.Equal(new[] { 2, 1 }, IdsOf(ranked));
        }

        [Fact]
        public void Icon_kinds_stay_where_the_api_put_them()
        {
            // Deliberately close to the API's order: preferring PNG over .ico split 30/29 on the graded
            // set, and preferring "official" style over "custom" was actively worse at 8 against 3.
            List<SteamGridDbGrid> ranked = ArtworkRanker.RankIcons(
                new[]
                {
                    Grid(id: 1, mime: "image/vnd.microsoft.icon", style: "custom", width: 0),
                    Grid(id: 2, mime: "image/png", style: "official", width: 128),
                    Grid(id: 3, mime: "image/png", style: "official", width: 512),
                });

            // The .ico kind appeared first, so it stays first even though a larger PNG exists.
            Assert.Equal(new[] { 1, 3, 2 }, IdsOf(ranked));
        }
    }
}
