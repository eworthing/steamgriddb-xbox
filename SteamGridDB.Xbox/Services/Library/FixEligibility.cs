namespace SteamGridDB.Xbox.Services.Library
{
    /// <summary>
    /// Which games a bulk fix run should visit.
    ///
    /// The rule with a reason worth writing down is the first-party one. This widget exists because
    /// the Xbox app shows poor artwork for third-party games - it has no real cover for a Steam or GOG
    /// entry and derives something generic - but that is not true of its own games. A first-party tile
    /// is rendered from the Store's own BoxArt or FeaturePromotionalSquareArt: the publisher's official
    /// square cover, fetched from the same catalogue the Xbox app itself calls.
    ///
    /// Which is precisely the artwork this app treats as ground truth everywhere else.
    /// <see cref="Artwork.ArtworkDownloader.FindOfficialLookalikeAsync"/> exists to catch a
    /// top-ranked SteamGridDB pick that is art for the wrong game, and what it checks against is
    /// Valve's own store capsule. Sweeping first-party games into the fixer would replace the official
    /// cover with a community upload judged good by resembling an official cover - backwards, and on
    /// the run most people press first, since a stock first-party tile has no backup and so counted as
    /// "not customised yet".
    ///
    /// Nothing here stops anyone customising one deliberately: the per-game Edit button is untouched,
    /// and that is what first-party support was added for. This governs the bulk runs only, and only
    /// the fix ones - "Restore my changes" exists largely for first-party games, whose tiles the Xbox
    /// app overwrites on its own schedule, and "Revert all to Xbox default artwork" obviously has to
    /// reach them too.
    /// </summary>
    internal static class FixEligibility
    {
        /// <summary>
        /// Whether a bulk fix run should download new artwork for a game.
        /// </summary>
        /// <param name="hasSteamGridDbMatch">Whether SteamGridDB knows this game at all.</param>
        /// <param name="isXboxTile">Whether this is one of the Xbox app's own games.</param>
        /// <param name="hasBackup">Whether the game has been customised before.</param>
        /// <param name="refixCustomised">Whether this run revisits already-customised games.</param>
        internal static bool ShouldFix(bool hasSteamGridDbMatch, bool isXboxTile, bool hasBackup, bool refixCustomised)
        {
            return hasSteamGridDbMatch && !isXboxTile && (refixCustomised || !hasBackup);
        }

        /// <summary>
        /// Whether a game was passed over only for being one of the Xbox app's own - the games worth
        /// mentioning in the run's summary, so that a library which is mostly Game Pass does not report
        /// a fix that quietly did almost nothing.
        ///
        /// Deliberately not every first-party game: one with no SteamGridDB match would have been
        /// skipped anyway, and counting it here would claim this rule cost the user something it did
        /// not.
        /// </summary>
        /// <param name="hasSteamGridDbMatch">Whether SteamGridDB knows this game at all.</param>
        /// <param name="isXboxTile">Whether this is one of the Xbox app's own games.</param>
        /// <param name="hasBackup">Whether the game has been customised before.</param>
        /// <param name="refixCustomised">Whether this run revisits already-customised games.</param>
        internal static bool SkippedAsFirstParty(bool hasSteamGridDbMatch, bool isXboxTile, bool hasBackup, bool refixCustomised)
        {
            return isXboxTile && ShouldFix(hasSteamGridDbMatch, false, hasBackup, refixCustomised);
        }
    }
}
