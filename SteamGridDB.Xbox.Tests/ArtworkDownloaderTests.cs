using SteamGridDB.Xbox.Services.Artwork;

using Xunit;

namespace SteamGridDB.Xbox.Tests
{
    /// <summary>
    /// The official-artwork replacement gate's non-network decision logic (see
    /// <see cref="ArtworkDownloader.FindOfficialLookalikeAsync"/>): the entry check deciding whether a
    /// replacement search is worth running at all, and whether a later candidate is worth paying for the
    /// tile-fill decode. The gate's third check, whether the candidate fills the tile, is
    /// <see cref="TileImage.FillsTileAsync"/> and is covered in <see cref="TileImageTests"/>; the image
    /// comparison the two doubles below come from is covered in <see cref="ArtworkSignatureTests"/>;
    /// downloading the candidates themselves is excluded from this suite for the reason
    /// TESTING.md gives under "Anything over the network".
    /// </summary>
    public class ArtworkDownloaderTests
    {
        // ---- ChosenAlreadyMatchesOfficialArt ----

        [Fact]
        public void Chosen_already_matches_official_art_when_the_match_is_exactly_at_the_floor()
        {
            // >=, not >: this exact boundary shipped inverted once (see officialArtworkFloor's own doc
            // comment - Mad Max sat on 0.51 against a 0.50 floor while candidates above 0.85 went untouched).
            Assert.True(ArtworkDownloader.ChosenAlreadyMatchesOfficialArt(chosenMatch: 0.60));
        }

        [Fact]
        public void Chosen_does_not_already_match_official_art_when_the_match_is_just_under_the_floor()
        {
            Assert.False(ArtworkDownloader.ChosenAlreadyMatchesOfficialArt(chosenMatch: 0.59));
        }

        // ---- PassesColourAndLayoutGate ----

        [Fact]
        public void Passes_when_colour_clears_the_ceiling_and_layout_is_better_than_the_artwork_it_would_replace()
        {
            Assert.True(ArtworkDownloader.PassesColourAndLayoutGate(candidateMatch: 0.90, candidateLayout: 0.60, chosenLayout: 0.50));
        }

        [Fact]
        public void Fails_when_the_colour_match_is_exactly_at_the_ceiling()
        {
            // Strictly greater-than: a candidate merely tied with the ceiling does not clear it - the
            // ceiling exists so a colour-only coincidence cannot win, and a tie is still a coincidence.
            Assert.False(ArtworkDownloader.PassesColourAndLayoutGate(candidateMatch: 0.85, candidateLayout: 0.90, chosenLayout: 0.50));
        }

        [Fact]
        public void Fails_when_the_colour_match_is_below_the_ceiling()
        {
            Assert.False(ArtworkDownloader.PassesColourAndLayoutGate(candidateMatch: 0.70, candidateLayout: 0.90, chosenLayout: 0.50));
        }

        [Fact]
        public void Fails_when_the_layout_match_is_worse_than_the_artwork_it_would_replace()
        {
            Assert.False(ArtworkDownloader.PassesColourAndLayoutGate(candidateMatch: 0.90, candidateLayout: 0.40, chosenLayout: 0.50));
        }

        [Fact]
        public void Passes_when_the_layout_match_exactly_ties_the_artwork_it_would_replace()
        {
            // >=, not >: a colour-clearing candidate that merely matches the existing layout can still
            // win - only a strictly worse layout match is disqualifying.
            Assert.True(ArtworkDownloader.PassesColourAndLayoutGate(candidateMatch: 0.90, candidateLayout: 0.50, chosenLayout: 0.50));
        }

        [Fact]
        public void Fails_when_both_colour_and_layout_reject_the_candidate()
        {
            Assert.False(ArtworkDownloader.PassesColourAndLayoutGate(candidateMatch: 0.70, candidateLayout: 0.40, chosenLayout: 0.50));
        }
    }
}
