# 13 Steam titles the Xbox app records but never renders

Investigation log, 2026-08-07. Written for a reviewer who has not seen the session it came from.

## The question

Thirteen installed Steam games appear in the Xbox app's own manifest but have no artwork on disk,
do not appear in the Xbox app's library UI, and therefore do not appear in this widget. The concern
that prompted this document is whether **this widget caused it**. It did not. The root cause is the
Xbox app's Steam `appinfo.vdf` launch-entry filter: these 13 games do not expose an installed Windows
launch entry whose `type` is `default`, `none`, or omitted. Xbox records their IDs during the
install-manifest scan, but its later app-info initialization cannot select an executable, so it
never creates a usable third-party game object, artwork file, or library tile.

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

## Root cause

The Xbox app has two distinct Steam parsing steps in `XboxPcAppFT.exe`:

1. `SteamLibraryProvider::ReadAppManifestFiles` reads `steamapps\appmanifest_*.acf` and records
   installed Steam IDs in `steam.manifest`.
2. `SteamLibraryProvider::ReadAppInfoFile` parses Steam's binary `appcache\appinfo.vdf` and calls
   `VdfReaderUtils::ReadExeNameAndIconInfo` to choose a launch executable and `clienticon`.

The installed Xbox build is `2608.1001.17.0`. Its native binary contains the relevant source and
diagnostic strings, including:

```text
XboxAppCommon\src\Vdf\VdfReaderUtils.cpp
executable
type
default
none
oslist
windows
clienticon
BetaKey
... Unable to find executable name
```

That code accepts a Windows launch entry only when the Steam launch `type` is `default`, `none`, or
absent. Types such as `option1`, `option2`, `option3`, `vr`, `othervr`, `openxr`, and `editor` are
not usable by this parser. A custom launcher URI such as `link2ea://...` is not a local executable.
Inactive branch entries whose files are not installed also cannot satisfy the executable step.

We parsed the current Steam `appinfo.vdf`, resolved every launch entry against the actual Steam
install directory, and applied that predicate to all 154 IDs in Xbox's `steam.manifest`:

```text
predicted failures: 13
observed failures : 13
false positives   : 0
false negatives   : 0
```

The per-title reason is:

| Steam ID | Title | Why Xbox cannot select a launch entry |
|---|---|---|
| 1097150 | Fall Guys | Only installed Windows entry is `option2` |
| 1172380 | STAR WARS Jedi: Fallen Order | `none` entry is a `link2ea://` URI; local EXE is `option1` on an EA test branch |
| 1237950 | STAR WARS Battlefront II | Only entry is a `link2ea://` URI |
| 1259420 | Days Gone | Only Windows entry is `option1` |
| 1774580 | STAR WARS Jedi: Survivor | Un-typed entry is a `link2ea://` URI; installed local EXE is `option1` |
| 1849900 | Among Us 3D: VR | Entries are `vr` and `othervr` |
| 285920 | TerraTech | Installed Windows entries are `option1` and `option2` |
| 3527290 | PEAK | Windows entries are `option1`/`option2`/`option3`; its `default` entry is Linux-only |
| 356400 | Thumper | Entries are `option1`, `vr`, and `othervr` |
| 359320 | Elite Dangerous | Entries are `option1` and `vr` |
| 546560 | Half-Life: Alyx | Windows entries are `vr` and `editor` |
| 617830 | SUPERHOT VR | Installed entry is `vr`; the un-typed `.lnk` belongs to an inactive branch and is absent |
| 916840 | The Walking Dead: Saints & Sinners | Only entry is `vr` |

This is not a missing-artwork problem. All 13 have valid Steam app-info records and `clienticon`
hashes, and 11 have normal Steam library artwork. The failure occurs while Xbox chooses a launch
entry, before its third-party item is initialized and before artwork is emitted.

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
unaided. The selector is the Xbox app's local Steam app-info parser described above.

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
| Missing or corrupt Steam `appinfo.vdf` records | Parsed the current version-41 binary VDF and inspected `common`, `config`, and `launch` for all 154 IDs | Dead — all 13 parse cleanly and contain names, icons, and launch records |
| Microsoft Store/PCGA catalog mapping is required | Inspected Xbox Mode's shipped `nativeOnlyAssembledItems` code | Dead — third-party identifiers (`idType: Other`) are assembled without Microsoft catalog hydration; the item must first survive the native Steam provider |
| A hidden-games preference | Searched the rebuilt package state and compared the exact launch-metadata discriminator | Dead — no hidden manifest exists, while the launch predicate reproduces all 13 and only those 13 |
| The widget caused it | Full app-data reset, rebuild with no widget artifacts | **Dead — identical 141/13 split reproduced** |
| A second widget (the upstream production build) running side by side | Uninstalled; checked for leftover package data | Not disproved, but irrelevant — the post-reset rebuild reproduces the split with no widget installed state at all |

## Why the entries look "stale"

After the rebuild, the manifest's `addedDate` values fall into two distinct batches:

```
the 141 that get artwork : 2026-08-07 13:51:01
the 13 that do not       : 2026-08-07 13:54:33
```

Every working entry shares one timestamp; every failing entry shares another, ~3.5 minutes later.
The later pass records IDs found in Steam's install manifests, but app-info enrichment cannot produce
a supported executable for them. Consequently, the records remain in Xbox's provider manifest while
the active native game catalog never receives fully initialized items. Calling them "stale" is
slightly misleading: they are current installed-game cache records that Xbox itself cannot promote
into displayable games.

`LocalState\AsyncCache.db` was also inspected properly as SQLite. It is an Xbox catalog/editorial
cache (`product_summary`, `collections`, alternate Xbox IDs, and related scopes), not the installed
third-party library source of truth. Its presence or absence of a similarly named Microsoft Store
product does not determine whether a Steam shortcut is shown.

Xbox Mode's shipped UI confirms the next stage: `nativeOnlyAssembledItems` consumes the native game
catalog's `activeItemsCoreData`, and third-party identifiers are deliberately allowed without PCGA
catalog hydration. These 13 never reach that stage as active Steam items.

## Workarounds tested

### Hand-writing `CustomLibraryManagement.manifest` — does not work

Xbox Mode's "add a game" feature stores locally added entries in
`ThirdPartyLibraries\CustomLibraryManagement\CustomLibraryManagement.manifest`. The schema is simple
and fully visible from an app-created entry, and it bypasses the launch-entry filter entirely — a
custom entry names its executable outright and never consults `type`. That makes it the obvious
escape hatch, including for the three EA titles no branch or type change could ever fix:

```json
"{guid}": {
  "id": "{guid}", "addedDate": "<unix ms>", "lastPlayedDate": "<unix ms>",
  "title": "Days Gone",
  "installLocation": "D:\\SteamLibrary\\steamapps\\common\\Days Gone\\BendGame\\Binaries\\Win64\\",
  "executableName": "DaysGone.exe",
  "executableCommandArgs": "-Steamworks -Installed",
  "imagePath": "...\\LocalState\\CustomLibraryManagement\\Images\\<name>.png"
}
```

Tested 2026-08-07 with one entry for Days Gone, structurally identical to an app-created one: real
installed executable, the arguments from its own Steam launch record, and an image placed in the
app's own `LocalState\CustomLibraryManagement\Images` folder.

**The Xbox app discarded it three seconds after launch.** `XboxPcApp` started at 15:42:01; the
manifest was rewritten to its empty 79-byte form at 15:42:04, and the UI reported *"You don't have
any games added from this device."* The image file was left behind untouched.

So the app treats that manifest as its own output and reconciles it against internal state on
startup; entries it did not create do not survive. **The in-app "+" / add-a-game flow is the only
route**, and whatever registration it performs is not reproducible by writing the manifest alone.

A consequence worth flagging separately: **an Xbox app data reset destroys locally added games**, and
they cannot be restored by putting the manifest back. This machine lost its one existing custom entry
(the NVIDIA App) to the reset performed during this investigation, and re-adding it by file edit
fails for the same reason as above.

### Opting into a Steam beta branch — helps exactly one title

The executable-exists clause means a branch opt-in could in principle rescue a game whose only
qualifying record points at a file that was never installed. Across the 13 that is true for exactly
one: **SUPERHOT VR**, whose record 6 is un-typed and targets Windows but names
`SUPERHOT-Launcher.lnk` from the inactive `launcher-test` branch. The other twelve have no qualifying
record on any branch, so no opt-in can help them.

That also makes SUPERHOT VR the best available falsification test: opting into `launcher-test` should
install the `.lnk` and produce a tile. A prediction that changes an input is stronger evidence than
the 154/154 fit, which only explains existing data.

### Editing `appinfo.vdf` — not durable

The launch `type` is developer metadata delivered through Steam's product-info system; `appinfo.vdf`
is a cache of it. On this machine it was rewritten while Steam was running, minutes after being read,
and every app entry carries text and binary SHA-1 hashes in its header. An edit is neither
authoritative nor persistent. (`option1`/`option2`/`option3` and `default` are all exactly seven
characters, so an in-place byte swap is mechanically possible — but only as a momentary experiment,
never as a fix.)

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
- **"Xbox needs a matching Microsoft Store catalog product."** No. The shipped Xbox Mode assembly
  path explicitly supports native-only third-party identifiers without catalog hydration. Some of
  the 13 have Microsoft catalog matches and some do not; launch metadata is the exact discriminator.
- **"Steam has no metadata or icon for these games."** No. Every one of the 13 has a valid
  `appinfo.vdf` record and `clienticon`; the unsupported launch-entry type is the failure.
- **"The launch `type` alone decides it."** Not quite — the record's executable must also exist on
  disk, and that clause is load-bearing rather than defensive. SUPERHOT VR is the case that forces
  it: it has an un-typed Windows record, which any type-only reading accepts, but the file belongs to
  an inactive branch and was never installed. A type-only predicate reports it as a false positive.
- **"Then the discriminator is the beta branch."** No. Special-casing `BetaKey` needs a growing list
  of sentinels — `public`, the literal `NONE`, the user's own selected branch, a developer's renamed
  default branch — and still gets Overcooked! All You Can Eat wrong, whose four records are all
  branch-gated yet which displays fine. Testing whether the file is present subsumes all of it and
  matches the binary's own "Unable to find executable name" path.
- **"The 13 can be added by writing `CustomLibraryManagement.manifest`."** No. The Xbox app rewrites
  that file on startup and discards entries it did not create — see Workarounds above.
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

To reproduce the root-cause test, parse Steam's version-41 `appcache\appinfo.vdf` string table and
binary KeyValues records. For every ID in Xbox's manifest, inspect `appinfo/config/launch` and ask
whether at least one record:

1. targets Windows (`config/oslist` contains `windows`, or the OS is unspecified),
2. has `type` equal to `default`, `none`, or no `type`,
3. names a local executable rather than a custom URI, and
4. resolves to an installed file in that game's `steamapps/common/<installdir>` folder.

The complement of that set is exactly the 13-title table above.

That predicate is implemented in [`verify-launch-rule.ps1`](verify-launch-rule.ps1) in this repo. It
is read-only, takes no arguments, and prints the rule's prediction against reality for every entry in
the manifest, naming any disagreement. It was written independently of the binary analysis above —
parsing the v29 header, string table and KeyValues records from the format rather than from the
expected answer — and reaches the same 154/154 result, 141 with tiles and 13 without, with no
disagreement in either direction. Two derivations from opposite ends agreeing is the strongest
evidence in this document; either alone would be weaker.

Its output for the failing set doubles as the per-title reason table:

```text
Among Us 3D: VR      0=vr/windows  1=othervr/windows
Days Gone            1=option1/windows
Elite Dangerous      0=option1/windows  5=vr/windows
Fall Guys            1=option2/windows
Half-Life: Alyx      0=vr/windows  1=editor/windows  2=vr/linux
PEAK                 5=option1/windows  6=option2/windows  8=option3/windows  9=default/linux
SUPERHOT VR          0=vr/windows  6=<omitted>/windows  7=openxr/android  8=openxr/windows
TerraTech            1=option1/windows  4=<omitted>/linux  5=<omitted>/macos  10=option1/windows ...
Thumper              0=option1/windows  1=vr/windows  2=othervr/windows  6=option1/windows
```

PEAK and TerraTech are the most instructive: both *do* have records of an acceptable type, but only
for Linux and macOS. That is the `oslist` clause doing real work, and it is why "this is a VR
limitation" — the intuitive reading, given four VR titles in the set — is wrong. Six of the 13 are
neither VR nor EA; they are simply developers who published their Windows entries as `option1`.

## External corroboration

Microsoft says installed games from supported storefronts should automatically appear in the Xbox
PC app's aggregated library:

- <https://news.xbox.com/en-us/2025/06/23/xbox-insiders-aggregated-gaming-library-is-coming-to-the-xbox-pc-app/>
- <https://news.xbox.com/en-us/2025/09/15/access-gaming-library-xbox-on-windows-pc-and-handheld/?ver=3.7.1>

An independent Xbox Insider report describes the same failure mode: a Steam game is installed and
visible to Steam/Game Bar but absent from Xbox Mode; resets and relinking do not repair it, while
manually adding the executable does:

- <https://www.reddit.com/r/xboxinsiders/comments/1t12b6a/games_library_issue_game_not_showing_up_in_xbox/>

That is consistent with the local finding here. This is an Xbox Steam-parser compatibility bug, not
an intentional storefront restriction and not behavior caused by this widget.

## State of the widget

Unrelated to this issue, but relevant context for anyone reading the code:

- The widget correctly mirrors what the Xbox app displays. Skipping these entries is right — there is
  no image to show and nothing to act on.
- The load reports them as *"N other entries the Xbox app is not showing"* and names each one in
  `last-load.log` with the folder it came from.
- A row is kept when a `.bak` **or** a `.new` sidecar survives, so artwork the user chose is never
  hidden or stranded even when the image itself is gone.
