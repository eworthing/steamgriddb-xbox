# SteamGridDB for Xbox

[![Get it from Microsoft](https://get.microsoft.com/images/en-us%20dark.svg)](https://apps.microsoft.com/detail/9pkqx0rjc32v)

Xbox Game Bar widget to customise Xbox PC app game artwork with images from [SteamGridDB](https://www.steamgriddb.com/). Replace wrong, low-resolution or missing images for games from third-party stores (Steam, GOG, Epic Games Store, EA App, Ubisoft Connect or manually added games) with high-quality artwork submitted by community.

Automatically detects the correct game through SteamGridDB's platform ID matching, which works for Steam games and most popular GOG and Epic titles. When no store ID matches, the game's name is resolved from its own store (GOG, Epic, Ubisoft Connect or the EA app) and SteamGridDB is searched by that name instead, so most of the remaining games are matched automatically as well. Use the built-in search feature to select a game by hand when the automatic match is missing or wrong.

Candidate artwork is ranked before anything is downloaded: uploads that read as case mockups, carry a storefront badge or are in another language get pushed down, and where Valve publishes its own store capsule for the game, a pick that looks nothing like it is passed over for one that does. Games with no square artwork at all get portrait box art cropped to a square, and fall back to an icon only when there is no box art either.

Games installed from the Xbox app itself (Game Pass and Microsoft Store titles) are supported too, and appear under their own heading in the list. They work differently from third-party games: the Xbox app renders those tiles from a cache of its own, refreshes it on its own schedule, and can overwrite a customisation at any time - so the widget puts the artwork back on every library load rather than writing it once.

The widget requires user to enable File system access under ***Settings > Privacy & security > File system > Let apps access your file system***, because by default it runs in a sandboxed environment where it is not allowed to access data of the other apps such as the Xbox app - this is the only way to enable such functionality. The only files being accessed are the Xbox app images downloaded for games installed from the third-party libraries, the Xbox app's own image cache, and the `MicrosoftGame.config` each installed game writes beside its executable.

### Toolbar buttons

The four buttons in the top right act on the whole library. The first three ask for confirmation before they change anything, and only one library operation runs at a time - pressing another while one is in progress does nothing rather than queueing it.

- **Fix my library** - downloads the best artwork for every game that has a SteamGridDB match. The dialog offers two runs: *Fix new games* only touches games that have not been customised yet, while *Re-fix all games* also re-downloads artwork for games customised earlier, replacing their current images. Games with no match are skipped by both.
- **Restore my changes** - puts your customised artwork back for every game whose image has since been overwritten, which is what the Xbox app does to first-party tiles on its own schedule. Nothing is downloaded: a copy of each applied image is kept beside the original.
- **Revert all to Xbox default artwork** - restores the Xbox app's own image for every customised game, leaving the library as it was before the widget touched it. The original is backed up the first time a game is customised and is never overwritten afterwards, so this stays possible no matter how many times artwork has been changed since.
- **Refresh** - re-reads the Xbox app's libraries and rebuilds the list.

Each game in the list carries its own buttons. **Change artwork** opens the picker of square grids and icons for a matched game, with the artwork currently applied marked *In use*; **Search manually** takes its place for a game that could not be matched, and looks the game up on SteamGridDB by name; **Restore backup** appears once a game has been customised and reverts just that one game.

### Currently known issues and/or limitations
- Demos are not supported for automatic matching, even from Steam - their ID is different from the main game, but their artwork can still be changed with manual search.
- The widget is specifically looking for square grids (512x512 or 1024x1024) or icons (which are always square), because the Xbox app is designed to show square artwork. That is why results from SteamGridDB are filtered and do not show all available images. The portrait box art used as an automatic fallback is not offered in the picker - only square artwork is.
- Freshly uploaded SteamGridDB artwork might not show up in the widget immediately due to SteamGridDB API caching.
- EA App names are read from each installed game's own `installerdata.xml`, so only installed EA games resolve a name — EA's public catalogue API has shut down and there is no online fallback.
- A first-party game only appears once the Xbox app has actually rendered its tile at least once, because that is what puts the artwork into the cache the widget matches against. If one is missing, open your Xbox app library, scroll to it, and refresh the widget.
- Battle.net games are still not supported. Unlike the other third-party stores they resolve to Microsoft Store products, so the same mechanism that covers first-party games would cover them — but mapping a Battle.net entry to its Store product needs the Xbox app's own database, which the widget does not read.
- Sometimes the Xbox app leaves behind manifest entries for removed games, causing them to appear in the widget. Conversely, some installed games can be missing from the manifest and not show up in the widget. To solve this, delete the `ThirdPartyLibraries` folder (located in `C:\Users\{yourWindowsUsername}\AppData\Local\Packages\Microsoft.GamingApp_8wekyb3d8bbwe\LocalState\`) or the manifest files (in `ThirdPartyLibraries` subfolders for corresponding stores) — the Xbox app will recreate them correctly.
- Only first 50 square grids and first 50 icons are loaded from SteamGridDB (paging is not implemented yet).

If you are building the project yourself, you will need your own SteamGridDB API key that can be obtained [here](https://www.steamgriddb.com/profile/preferences/api).

Powered by SteamGridDB API, GOG API and Steam's public store assets. Not affiliated with SteamGridDB, Xbox, Steam, GOG, Epic Games, Electronic Arts, Ubisoft or their subsidiaries. All trademarks are property of their respective owners.

Credit to https://github.com/nachoaldamav/items-tracker for Epic Games Store database.

Credit to https://github.com/Haoose/UPLAY_GAME_ID for Ubisoft Connect database. 
