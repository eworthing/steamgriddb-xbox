using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

using SteamGridDB.Xbox.Services.SteamGridDB.Models;

namespace SteamGridDB.Xbox.Services.Artwork
{
    /// <summary>
    /// Classifies and orders SteamGridDB artwork candidates for auto-selection and the manual picker.
    /// Pure - takes candidates and a game name, returns them ordered; no I/O, no UI, no network.
    /// </summary>
    internal static class ArtworkRanker
    {
        // Grid styles that normally carry the game's title artwork, matching the look of native Xbox app tiles.
        // Ordered by preference; styles not listed here (no_logo, material) tend to look like plain icons.
        internal static readonly string[] TextBearingGridStyles = { "alternate", "white_logo", "blurred" };

        // Notes/tags vocabulary of physical-media mockups (word-bounded, so "Xbox" never matches "box").
        // "icon" is deliberately absent - it appears in legitimate source notes like "PS icon" too often.
        private static readonly Regex demotedGridMetadata = new Regex(@"\b(case|box|jewel|spine|cartridge|mock-?ups?|physical|ps1|ps2|psp|retro|custom|wallpapers?|iisu|game icons|wallhaven|artstation|deviantart)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Console-store artwork: the game's real cover with a storefront badge burned into it
        // ("PlayStation Hits" banner, a Switch or PS5 dashboard icon, an Xbox generation stamp).
        // The art underneath is usually right, which is why the similarity gate rates these highly and
        // why the vocabulary above misses them - they are not mockups, they are branded reissues.
        // "greatest hits" is deliberately absent: one upload advertises being the *non*-Hits version.
        //
        // A bare console name counts, not just the badge-shaped phrasings. Far Cry 6 was handed a cover
        // with a PS4 spine down its left edge whose notes read exactly "Playstation 4": the same
        // uploader had posted the set - "Xbox One" and "Xbox Series S/X" caught here, "Playstation 4"
        // and "Playstation 5" not - so half a batch of identically-badged art was being demoted and
        // half of it was winning. The Xbox names were already bare for this reason; the PlayStation
        // ones only appeared in their "PS5 dashboard icon" forms.
        private static readonly Regex consoleBadgeGridMetadata = new Regex(@"\b(playstation hits|ps hits|playstation ?[1-5]|ps ?[45] ?(dashboard |store )?icon|ps ?[45] ?square|nintendo switch|switch ?2? ?icon|dashboard icon|xbox one|xbox series)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Artwork for the game's soundtrack release rather than the game. It is official art, it is the
        // right franchise, and it is often striking - so nothing else here catches it. Slay the Spire
        // shipped the vinyl OST sleeve: it beat the real cover on SteamGridDB's own ordering with every
        // ranking key tied, and the similarity gate let it stand because a 0.79 colour match is well
        // above the floor. The real cover was one place further down at 0.93.
        //
        // "soundtrack" matches nothing in the library this was measured on - "vinyl" and "ost" catch
        // that one upload, "album cover" three more across two other games. It is in anyway, because
        // the hole §4.6b closed was a vocabulary that named one half of an idea more narrowly than the
        // other, and demoting "OST" while ignoring the word it abbreviates is exactly that shape.
        private static readonly Regex soundtrackGridMetadata = new Regex(@"\b(vinyl|soundtrack|ost|album cover|album art)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Uploads labelled as sourced from official store artwork ("offical" is a common uploader typo)
        // or citing an official platform-store domain. Press-kit mentions were tried and rejected:
        // press-kit art is often stylistic promo art rather than the game's real cover.
        private static readonly Regex boostedGridMetadata = new Regex(@"\b(official|offical)\b|xbox\.com|playstation\.com|nintendo\.com|microsoft\.com", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Edition markers in notes/tags; art is demoted when the marker is absent from the game's own name
        private static readonly Regex editionGridMetadata = new Regex(@"\b(deluxe|goty|game of the year|definitive|ultimate|premium|collector'?s?|complete|anniversary|remaster(ed)?|enhanced|legendary|gold)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Markdown/URL noise stripped from notes before keyword matching (see GridMetadata)
        private static readonly Regex crossReferenceLink = new Regex(@"\[>[^\]]*\]\s*\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex markdownLink = new Regex(@"\[([^\]]*)\]\s*\([^)]*\)", RegexOptions.Compiled);
        private static readonly Regex bareUrl = new Regex(@"https?://(?:www\.)?([^/\s)\]]+)\S*", RegexOptions.Compiled);

        /// <summary>
        /// Orders icons for the picker, and for the fallback used when a game has no square grid.
        ///
        /// Deliberately close to the order the API returned. Sorting on Score, as this did, was sorting
        /// on a constant the API retired, but grading 108 games showed nothing else beat that accidental
        /// order either: preferring PNG over .ico split 30/29, and preferring SteamGridDB's own
        /// "official" style over "custom" was actively worse at 8 against 3 - the official icon is often
        /// the small platform one (128px against a 512px custom upload), so a label was outranking size.
        ///
        /// The one rule the grading did support is narrow, so that is all this does: among icons that
        /// are the same kind - same format, same style - take the largest. Everything else keeps its
        /// original position. On the graded set that moved 14 picks, 6 onto the preferred artwork and 1
        /// onto artwork that had been rejected.
        /// </summary>
        /// <param name="icons">Icons as returned by the API.</param>
        internal static List<SteamGridDbGrid> RankIcons(IEnumerable<SteamGridDbGrid> icons)
        {
            // GroupBy yields groups in first-appearance order, so kinds stay where the API put them
            return icons
                .GroupBy(i => (i.Mime, i.Style))
                .SelectMany(kind => kind.OrderByDescending(i => i.Width))
                .ToList();
        }

        /// <summary>
        /// Returns the sort rank of a grid style - title-bearing box art styles first, icon-like styles last.
        /// Title-bearing styles rank equally: preferring one over another proved to mostly surface
        /// mis-tagged fan art while any of them already matches the native Xbox look.
        /// </summary>
        /// <param name="style">Grid style reported by SteamGridDB.</param>
        internal static int GridStylePriority(string style)
        {
            return Array.IndexOf(TextBearingGridStyles, style) >= 0 ? 0 : 1;
        }

        /// <summary>
        /// Combined notes and tags text used for metadata-based ranking. Cross-reference links to
        /// other uploads (SteamGridDB convention "[&gt;deluxe](url)") and URLs are stripped so they
        /// cannot trigger keyword matches; other links keep their text (e.g. "Official - Microsoft").
        /// </summary>
        private static string GridMetadata(SteamGridDbGrid grid)
        {
            string text = (grid.Notes ?? string.Empty) + " " + string.Join(" ", grid.Tags ?? Array.Empty<string>());

            text = crossReferenceLink.Replace(text, " ");
            text = markdownLink.Replace(text, "$1");
            text = bareUrl.Replace(text, " $1 ");

            return text;
        }

        /// <summary>
        /// True when the artwork's notes/tags name an edition (deluxe, GOTY, etc.) that is not part of
        /// the game's own name - e.g. "Deluxe Edition" art for a standard-edition game.
        /// </summary>
        /// <param name="metadata">Cleaned notes/tags text from <see cref="GridMetadata"/>.</param>
        /// <param name="gameName">Name of the game the artwork is being ranked for.</param>
        private static bool IsEditionMismatch(string metadata, string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
            {
                // Nothing to compare against. Treating that as a mismatch demoted every
                // edition-labelled candidate for any game whose name did not resolve, on no evidence.
                return false;
            }

            foreach (Match match in editionGridMetadata.Matches(metadata))
            {
                if (gameName.IndexOf(match.Value, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True when the artwork's notes/tags mark it as something other than the game's plain cover:
        /// a physical-media mockup, art for an edition the game is not, a console-store reissue with
        /// a storefront badge on it, or the soundtrack's sleeve rather than the game's. Such artwork is
        /// ranked last and is never accepted as a replacement by the official-artwork gate, which would
        /// otherwise rate a badged cover highly for matching the real one.
        /// </summary>
        /// <param name="grid">Artwork to test.</param>
        /// <param name="gameName">Name of the game the artwork is being ranked for.</param>
        internal static bool IsDemotedGrid(SteamGridDbGrid grid, string gameName)
        {
            return IsDemotedMetadata(GridMetadata(grid), gameName);
        }

        /// <summary>
        /// As <see cref="IsDemotedGrid"/>, for callers that have already built the metadata text.
        /// </summary>
        /// <param name="metadata">Cleaned notes/tags text from <see cref="GridMetadata"/>.</param>
        /// <param name="gameName">Name of the game the artwork is being ranked for.</param>
        private static bool IsDemotedMetadata(string metadata, string gameName)
        {
            return demotedGridMetadata.IsMatch(metadata)
                || consoleBadgeGridMetadata.IsMatch(metadata)
                || soundtrackGridMetadata.IsMatch(metadata)
                || IsEditionMismatch(metadata, gameName);
        }

        /// <summary>
        /// A grid with its ranking signals worked out once. Evaluating them inside the sort keys instead
        /// would rebuild and re-scan the same notes text three times for every candidate.
        /// </summary>
        private sealed class RankedGrid
        {
            public RankedGrid(SteamGridDbGrid grid, string gameName)
            {
                string metadata = GridMetadata(grid);

                Grid = grid;
                IsDemoted = IsDemotedMetadata(metadata, gameName);
                IsBoosted = boostedGridMetadata.IsMatch(metadata);
                IsForeignLanguage = !string.IsNullOrEmpty(grid.Language) && grid.Language != "en";
            }

            public SteamGridDbGrid Grid
            {
                get;
            }

            public bool IsDemoted
            {
                get;
            }

            public bool IsBoosted
            {
                get;
            }

            public bool IsForeignLanguage
            {
                get;
            }
        }

        /// <summary>
        /// Ranks grids for auto-selection: mockup/icon-labelled and wrong-edition uploads last,
        /// English (or untagged) language first, official store artwork boosted, then style
        /// preference, resolution and format. Ties keep SteamGridDB's canonical ordering (stable sort).
        /// </summary>
        internal static List<SteamGridDbGrid> RankGrids(IEnumerable<SteamGridDbGrid> grids, string gameName)
        {
            return grids
                .Select(g => new RankedGrid(g, gameName))
                .OrderBy(r => r.IsDemoted ? 1 : 0)
                .ThenBy(r => r.IsForeignLanguage ? 1 : 0)
                .ThenBy(r => GridStylePriority(r.Grid.Style))
                .ThenByDescending(r => r.IsBoosted ? 1 : 0)
                // 512x512 and 1024x1024 are requested together, so the sharper upload has to be picked
                // out here. Preferring PNG over JPEG was tried as a further tie-break and reverted: it
                // moved 26 picks and graded 2 better against 7 worse, because format says nothing about
                // whether the art is the game's real cover. The tile's filename claim is a separate
                // problem and belongs with the download, not the ranking.
                .ThenByDescending(r => r.Grid.Width)
                .Select(r => r.Grid)
                .ToList();
        }
    }
}
