# 13 Steam titles the Xbox app records but never renders

Investigation log, 2026-08-07. Written for a reviewer who has not seen the session it came from.

## The question

Thirteen installed Steam games appear in the Xbox app's own manifest but have no artwork on disk,
do not appear in the Xbox app's library UI, and therefore do not appear in this widget. The concern
that prompted this document is whether **this widget caused it**. The short answer is no, and the
evidence for that is in [The decisive experiment](#the-decisive-experiment). What actually causes it
is still unknown.

## Background: how the widget sees a game

The Xbox app keeps third-party games under:

```
%LOCALAPPDATA%\Packages\Microsoft.GamingApp_8wekyb3d8bbwe\LocalState\ThirdPartyLibraries\<Store>\
    <store>.manifest      JSON; gameCache maps entry id -> { id, addedDate }
    <store>_<id>.png      the tile the app renders
```

The widget reads the manifest, derives each entry's expected image path
(`entryId.Replace(":", "_") + ".png"`), and shows a row when that file exists — or when a `.bak` or
`.new` sidecar exists, so a row with something to restore is never dropped. An entry with none of
those is skipped and counted. See `Services/Library/ManifestEntryImage.cs`.

Critically: **the widget only ever writes image files.** It has no mechanism to add or remove entries
from the manifest, and none to affect what the Xbox app chooses to display. The manifest is the Xbox
app's own record of detected games; the app decides separately what to render.

## The observation

On the machine under investigation:

| | count |
|---|---|
| entries in `Steam.manifest` | 154 |
| entries with a `steam_<id>.png` | 141 |
| entries with **no** artwork | **13** |

The 13, resolved to titles by reading Steam's own `appmanifest_<id>.acf`:

| Steam ID | Title | StateFlags | SharedDepots |
|---|---|---|---|
| 1097150 | Fall Guys | 4 | yes |
| 1172380 | STAR WARS Jedi: Fallen Order | 6 | yes |
| 1237950 | STAR WARS Battlefront II | 6 | yes |
| 1259420 | Days Gone | 4 | yes |
| 1774580 | STAR WARS Jedi: Survivor | 6 | yes |
| 1849900 | Among Us 3D: VR | 4 | no |
| 285920 | TerraTech | 4 | no |
| 3527290 | PEAK | 4 | no |
| 356400 | Thumper | 4 | yes |
| 359320 | Elite Dangerous | 4 | yes |
| 546560 | Half-Life: Alyx | 4 | yes |
| 617830 | SUPERHOT VR | 4 | no |
| 916840 | The Walking Dead: Saints & Sinners | 4 | yes |

All 13 are installed, and their install directories exist on disk. The user confirms none of them
appear in the Xbox app's library UI.

## The decisive experiment

The Xbox app's data was **fully reset** (Windows Settings → Apps → Xbox → Advanced options → Reset),
which wipes `LocalState` and forces the app to rebuild `ThirdPartyLibraries` from scratch.

After the rebuild, with **zero widget artifacts present** — no `.bak`, no `.new`, no prior
customisation, nothing the widget had ever written:

```
141 .png
  1 .manifest
  0 widget sidecars
```

The manifest again contained 154 entries, and **the same 13 again received no artwork**.

This is conclusive on the question that prompted the document. The widget cannot be the cause: there
was no widget state for the app to react to, and the app reproduced the identical 141/13 split
unaided. Whatever selects these 13 lives inside the Xbox app.

## What has been ruled out

Each of these was tested and disproved, with the method recorded so nobody repeats it.

| Hypothesis | Test | Result |
|---|---|---|
| Games are in an unscanned Steam library folder | Mapped all 13 against `libraryfolders.vdf` | Dead — split 6/7 across both libraries, both of which contain many working games |
| The Xbox app changed its file naming and the widget can't keep up | Compared file names and timestamps in the store folders | Dead — `<lowercase>_<id>.png` is current (newest file: today); `<PascalFolder>-<id>.bmp` is abandoned (newest: Jul 2025) |
| Stale Steam install paths | Read `HKCU\Software\Valve\Steam`, `HKLM\...\WOW6432Node\Valve\Steam`, `libraryfolders.vdf` | Dead — all paths present and valid |
| Corrupt `ThirdPartyLibraries` contents | Restored a verified byte-identical backup | Dead — no change |
| The "trouble connecting to Steam" error (`0x80070002`) | Cleared the error, restarted, rescanned | Dead — error gone, 13 still absent |
| Steam-side install state | Compared `StateFlags` and `SharedDepots` against working controls | Dead — 10 of 13 are StateFlags 4, same as controls; Family Sharing is mixed across both groups (Stardew Valley, Mad Max, Celeste, Elden Ring are all shared and all work) |
| The widget caused it | Full app-data reset, rebuild with no widget artifacts | **Dead — identical 141/13 split reproduced** |
| A second widget (the upstream production build) running side by side | Uninstalled; checked for leftover package data | Not disproved, but irrelevant — the post-reset rebuild reproduces the split with no widget installed state at all |

## The only signal found

After the rebuild, the manifest's `addedDate` values fall into two distinct batches:

```
the 141 that get artwork : 2026-08-07 13:51:01
the 13 that do not       : 2026-08-07 13:54:33
```

Every working entry shares one timestamp; every failing entry shares another, ~3.5 minutes later.
The Xbox app discovers them in a **separate, later pass** — it finds them, records them, and then
does not fetch artwork for them.

This is the strongest lead in the document. It suggests the 13 are handled by a different code path
or a second-stage enumeration that completes registration but never triggers the artwork fetch.

## Open questions for a reviewer

1. **What is the second pass?** The two `addedDate` clusters are reproducible across rescans. What
   would cause the Xbox app to enumerate a subset of installed Steam games several minutes after the
   main batch, and why would that path skip artwork?
2. **Is there a common property of the 13 not visible from the filesystem?** Candidates not yet
   tested: VR-only titles (4 of the 13 are VR: Alyx, SUPERHOT VR, Among Us 3D VR, Saints & Sinners),
   titles requiring a secondary launcher (3 are EA app titles, 1 is Frontier, 1 is Epic/EOS), and
   titles delisted or region-restricted on the Microsoft Store side.
3. **Where does the app store its display list?** `LocalState\AsyncCache.db` is a 40 MB SQLite file
   and is the likely source of truth for the library UI. A raw ASCII probe was too noisy to be
   conclusive: some of the 13 appear as substrings and some do not, but so do titles from store
   browsing, so presence proves nothing. A proper SQLite read of its schema would settle whether the
   app has these games in its library table at all.
4. **Does the artwork fetch have a failure log?** The app's `LocalState\Logs\*.etl` are binary traces;
   a string extraction found nothing readable. Decoding them with the correct provider manifests may
   name the failure directly.

## Corrections — dead ends already walked

Recorded so a reviewer does not re-derive them.

- **"The 13 were added yesterday, so they are new installs."** Wrong. `addedDate` reflects the most
  recent rescan batch, not when the game was installed. After the reset every entry was re-dated to
  today. The batching is real; the recency was an artifact.
- **"The Xbox app migrated to `<Folder>-<id>.bmp` and the widget is reading the old scheme."**
  Backwards. Timestamps show `.bmp` is the abandoned format (newest Jul 2025) and `steam_<id>.png`
  is current (newest today). The widget implements the only live scheme.
- **"The manifest's `title`/`isInstalled` fields distinguish the entries."** There are no such
  fields. Entries carry only `id` and `addedDate`.
- **"Writing the artwork back may make the games reappear."** No. `ThirdPartyLibraries` is downstream
  of the app's library — the app writes an image there when it decides to display a game. A missing
  PNG is a symptom, not a cause.
- **"Delete `ThirdPartyLibraries` to force a rebuild"** (advice currently in `README.md`). The Xbox
  app treats the folder's *absence* as a hard error — `Error Code: -2147024894` = `0x80070002`
  `ERROR_FILE_NOT_FOUND`, surfaced in the UI as "We're having trouble connecting to Steam". The
  folder must exist. **This README line should be corrected.**

## Related but separate: 4 Ubisoft entries

Also skipped by the widget, for understood reasons:

- `ubi:17903`, `ubi:4740` — in the live `ubi` folder with no artwork. Same class as the Steam 13.
- `5266`, `5487` — the entire contents of an abandoned `Ubisoft` folder, all files dated 2025-09-29.
  The Xbox app renamed `Ubisoft` to `ubi` and left the old folder behind. `5266` (Far Cry 6) is live
  in the new folder and displays correctly; `5487` is a dead entry.

## Reproducing the checks

```powershell
$dir = "$env:LOCALAPPDATA\Packages\Microsoft.GamingApp_8wekyb3d8bbwe\LocalState\ThirdPartyLibraries\Steam"
$gc  = (Get-Content "$dir\steam.manifest" -Raw | ConvertFrom-Json).gameCache

# entries with and without artwork, plus the addedDate batching
@($gc.PSObject.Properties | Where-Object { $_.Name -ne 'version' }) | ForEach-Object {
  $id = $_.Name -replace '^steam:',''
  [pscustomobject]@{
    Id      = $id
    HasArt  = Test-Path "$dir\steam_$id.png"
    Added   = [DateTimeOffset]::FromUnixTimeMilliseconds([long]$_.Value.addedDate).LocalDateTime
  }
} | Group-Object HasArt, Added | Select-Object Count, Name
```

Resolve IDs to titles from Steam's own manifests in each library folder listed by
`steamapps\libraryfolders.vdf`:

```powershell
Select-String -Path "<library>\steamapps\appmanifest_<id>.acf" -Pattern '"name"\s+"(.+?)"'
```

## State of the widget

Unrelated to this issue, but relevant context for anyone reading the code:

- The widget correctly mirrors what the Xbox app displays. Skipping these entries is right — there is
  no image to show and nothing to act on.
- The load reports them as *"N other entries the Xbox app is not showing"* and names each one in
  `last-load.log` with the folder it came from.
- A row is kept when a `.bak` **or** a `.new` sidecar survives, so artwork the user chose is never
  hidden or stranded even when the image itself is gone.
