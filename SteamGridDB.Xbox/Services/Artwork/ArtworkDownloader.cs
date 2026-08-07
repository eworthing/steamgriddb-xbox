using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Windows.Storage.Streams;
using Windows.Web.Http;

using SteamGridDB.Xbox.Services.SteamGridDB.Models;

namespace SteamGridDB.Xbox.Services.Artwork
{
    /// <summary>
    /// Downloads artwork candidates and decides which one becomes the tile: the best-ranked upload
    /// that actually fills the square, replaced with a later candidate when the winner looks nothing
    /// like the game's official store artwork. No UI, no GameEntry, no Dispatcher - every caller reaches
    /// this after ranking (<see cref="ArtworkRanker"/>) and before the UI-bound write
    /// (ReplaceImageCoreAsync in PrimaryWidget), which is a separate concern this does not touch.
    /// </summary>
    internal static class ArtworkDownloader
    {
        // How far down the ranked candidates the downloader will look. Five covered the tile-filling
        // check; the official-artwork gate occasionally has to reach further to find its replacement.
        internal const int MaxCandidates = 8;

        // Colour-match band for the official-artwork gate (see FindOfficialLookalikeAsync). Graded over
        // the whole library: the winner must be below the floor and the replacement above the ceiling.
        // Dropping the ceiling and keeping only the floor was tried and rejected - it let artwork move
        // on differences of a few hundredths, which is inside the measure's own noise. The gap the two
        // leave between them is that guard: nothing moves unless the replacement is a quarter better.
        // The floor was 0.50 for one grading round, which left Mad Max on a 0.51 match while four
        // candidates above 0.85 sat untouched - a hundredth of slack either side of a hard edge.
        private const double officialArtworkFloor = 0.60;
        private const double officialArtworkCeiling = 0.85;

        private static readonly HttpClient sharedHttpClient = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient();

            AppIdentity.Identify(client.DefaultRequestHeaders);

            return client;
        }

        /// <summary>
        /// Downloads one artwork, returning null rather than throwing when it cannot be fetched.
        /// </summary>
        internal static async Task<IBuffer> DownloadArtworkAsync(string url)
        {
            try
            {
                HttpResponseMessage response = await sharedHttpClient.GetAsync(new Uri(url));

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content.ReadAsBufferAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error downloading artwork {url}: {ex.Message}");

                return null;
            }
        }

        /// <summary>
        /// Downloads the best-ranked grid that fills the square tile, skipping uploads with transparent
        /// corners (rounded icon-style art and physical case mockups that metadata cannot identify).
        /// When the winner looks nothing like the game's official store artwork, a later candidate that
        /// clearly does is taken instead - see <see cref="FindOfficialLookalikeAsync"/>.
        /// Returns the chosen grid's image bytes, or the best-ranked grid's bytes when none pass.
        /// </summary>
        /// <param name="rankedGrids">Candidates in ranking order.</param>
        /// <param name="gameName">Game name, for the demotion check on replacement candidates.</param>
        /// <param name="officialCapsuleUrl">Valve's own artwork for this game, or null when it has none.</param>
        internal static async Task<(IBuffer Bytes, int ArtworkId)> DownloadBestTileFillingImageAsync(IReadOnlyList<SteamGridDbGrid> rankedGrids, string gameName, string officialCapsuleUrl)
        {
            IBuffer fallback = null;
            int fallbackId = 0;

            for (int i = 0; i < rankedGrids.Count && i < MaxCandidates; i++)
            {
                IBuffer imageBytes = await DownloadArtworkAsync(rankedGrids[i].Url);

                if (imageBytes == null)
                {
                    continue;
                }

                if (fallback == null)
                {
                    fallback = imageBytes;
                    fallbackId = rankedGrids[i].Id;
                }

                if (await TileImage.FillsTileAsync(imageBytes))
                {
                    (IBuffer Bytes, int ArtworkId) replacement = await FindOfficialLookalikeAsync(rankedGrids, i, imageBytes, gameName, officialCapsuleUrl);

                    return replacement.Bytes != null ? replacement : (imageBytes, rankedGrids[i].Id);
                }
            }

            return (fallback, fallbackId);
        }

        /// <summary>
        /// Rescues the cases the notes cannot: when two thirds of games have every ranking key tied, the
        /// winner is whatever SteamGridDB happened to return first, and sometimes that is art for the
        /// wrong game entirely. Valve's own store capsule says what the cover really looks like.
        ///
        /// Deliberately a narrow veto, not a ranking key. Ranking by similarity outright was tried and
        /// moved most of the library, including picks that had already been graded as good. The
        /// replacement must clear every one of these, or the original stands:
        ///   - the chosen artwork barely resembles the official capsule at all
        ///   - the replacement resembles it strongly, not merely more
        ///   - the replacement's layout is no worse, so a colour-only coincidence cannot win
        ///   - the replacement is not itself demoted, or a badged console reissue would score highly
        ///     and win precisely because it is the real cover with a storefront banner on it
        /// </summary>
        /// <param name="rankedGrids">Candidates in ranking order.</param>
        /// <param name="chosenIndex">Index of the candidate that won on ranking alone.</param>
        /// <param name="chosenBytes">Image bytes of that candidate.</param>
        /// <param name="gameName">Game name, for the demotion check.</param>
        /// <param name="officialCapsuleUrl">Valve's own artwork, or null when it has none.</param>
        /// <returns>Replacement bytes and artwork ID, or a null buffer to keep the original choice.</returns>
        internal static async Task<(IBuffer Bytes, int ArtworkId)> FindOfficialLookalikeAsync(IReadOnlyList<SteamGridDbGrid> rankedGrids, int chosenIndex, IBuffer chosenBytes, string gameName, string officialCapsuleUrl)
        {
            if (string.IsNullOrEmpty(officialCapsuleUrl))
            {
                FixLog.Write("  gate: no official capsule for this game");

                return (null, 0);
            }

            ArtworkSignature official = await ArtworkSignature.CreateAsync(await DownloadArtworkAsync(officialCapsuleUrl));
            ArtworkSignature chosen = await ArtworkSignature.CreateAsync(chosenBytes);

            if (official == null || chosen == null)
            {
                // Distinct from "the artwork already matches": this is the gate unable to run at all,
                // which is indistinguishable from it declining unless it says so. It reported nothing
                // for an entire library once, because a bad crop transform made every signature fail.
                FixLog.Write($"  gate: unreadable ({(official == null ? "capsule" : "chosen artwork")})");

                return (null, 0);
            }

            double chosenMatch = official.ColourMatch(chosen);

            if (ChosenAlreadyMatchesOfficialArt(chosenMatch))
            {
                FixLog.Write($"  gate: chosen already matches official art ({chosenMatch:F2})");

                return (null, 0);
            }

            double chosenLayout = official.LayoutMatch(chosen);

            FixLog.Write($"  gate: chosen matches only {chosenMatch:F2}, looking for a replacement above {officialArtworkCeiling:F2}");

            // Everything before chosenIndex already failed the tile-fill check on the way here, so it
            // can only fail it again - starting past the winner saves re-fetching and re-decoding them.
            for (int i = chosenIndex + 1; i < rankedGrids.Count && i < MaxCandidates; i++)
            {
                if (ArtworkRanker.IsDemotedGrid(rankedGrids[i], gameName))
                {
                    continue;
                }

                IBuffer candidateBytes = await DownloadArtworkAsync(rankedGrids[i].Url);
                ArtworkSignature candidate = await ArtworkSignature.CreateAsync(candidateBytes);

                if (candidate == null)
                {
                    FixLog.Write($"    {rankedGrids[i].Id}: unreadable");

                    continue;
                }

                double candidateMatch = official.ColourMatch(candidate);
                double candidateLayout = official.LayoutMatch(candidate);

                if (!PassesColourAndLayoutGate(candidateMatch, candidateLayout, chosenLayout)
                    || !await TileImage.FillsTileAsync(candidateBytes))
                {
                    FixLog.Write($"    {rankedGrids[i].Id}: colour {candidateMatch:F2}, layout {candidateLayout:F2} vs {chosenLayout:F2} - rejected");

                    continue;
                }

                FixLog.Write($"    {rankedGrids[i].Id}: colour {candidateMatch:F2}, layout {candidateLayout:F2} - REPLACED {rankedGrids[chosenIndex].Id}");

                return (candidateBytes, rankedGrids[i].Id);
            }

            return (null, 0);
        }

        /// <summary>
        /// Two of the replacement gate's three checks (see <see cref="FindOfficialLookalikeAsync"/>):
        /// close enough to the official capsule's colour, and no worse a layout match than the artwork
        /// it would replace. Kept separate from the third check, whether the candidate fills the tile,
        /// because that one needs a decode this gate exists partly to avoid paying for when colour or
        /// layout alone already reject the candidate.
        /// </summary>
        /// <param name="candidateMatch">Candidate's <see cref="ArtworkSignature.ColourMatch"/> against the official capsule.</param>
        /// <param name="candidateLayout">Candidate's <see cref="ArtworkSignature.LayoutMatch"/> against the official capsule.</param>
        /// <param name="chosenLayout">The artwork currently chosen's layout match against the official capsule - the bar the candidate must not fall below.</param>
        internal static bool PassesColourAndLayoutGate(double candidateMatch, double candidateLayout, double chosenLayout)
        {
            return candidateMatch > officialArtworkCeiling && candidateLayout >= chosenLayout;
        }

        /// <summary>
        /// The replacement gate's entry check (see <see cref="FindOfficialLookalikeAsync"/>): whether the
        /// already-chosen artwork resembles the official capsule closely enough that no replacement search
        /// is worth running at all. See <see cref="officialArtworkFloor"/>'s own doc comment for how this
        /// bound was calibrated, including the incident (Mad Max at 0.51) that set it where it is.
        /// </summary>
        /// <param name="chosenMatch">Chosen candidate's <see cref="ArtworkSignature.ColourMatch"/> against the official capsule.</param>
        internal static bool ChosenAlreadyMatchesOfficialArt(double chosenMatch)
        {
            return chosenMatch >= officialArtworkFloor;
        }
    }
}
