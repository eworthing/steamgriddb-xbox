# Code review — whole codebase

Reviewed 2026-08-03 against `dfa22fb`. Scope: the full repository (not just recent commits) —
`SteamGridDB.Xbox/**/*.cs`, `PrimaryWidget.xaml`, `Package.appxmanifest`, `deploy-dev.ps1`.

Findings marked **Confirmed** were reproduced against live data (the local Xbox app manifests
and the SteamGridDB API); the rest are read from the code.

## Status — all fixed

Every finding below has been applied and the package rebuilt and deployed. Two of them were fixed
deeper than described:

- **#2** — rather than just swapping the argument, `GameEntry.XboxPlatformId` was removed
  altogether. It had no remaining readers, and keeping a second near-identical identifier next to
  `ExternalPlatformId` is what invited the wrong one to be used.
- **#5** — legacy folder names are now recognised, *and* manifest entries with no image and no
  backup are skipped instead of being listed as unfixable ghost rows. The status bar reports the
  count ("skipped 2 stale manifest entries") so nothing disappears silently.

Not done, deliberately: the orphaned `.new` files noted at the end. They are the payload
"Restore my changes" replays after the Xbox app resets artwork, and deleting files that a user
might still want restored is not a cleanup worth automating.

---

## 1. Epic games can never auto-match — wrong ID segment (Confirmed)

`SteamGridDB.Xbox/PrimaryWidget.xaml.cs:375-384`

```csharp
string[] parts = entryId.Split(':');
if (parts.Length >= 3) { externalPlatformId = parts[2]; }
```

The Xbox app stores Epic entries as `epic:<namespace>:<catalogItemId>:<appName>`, so `parts[2]`
is the **catalog item ID**. SteamGridDB's `egs` platform key is the **appName** (last segment).

Verified against the API with the local library's real IDs:

| Request | Result |
| --- | --- |
| `/games/egs/530145df28a24424923f5828cc9031a1` (`parts[2]`) | 404 |
| `/games/egs/9773aa1aa54f4f7b80e44bef04986cea` (`parts[1]`, namespace) | 404 |
| `/games/egs/Sugar` (last segment, appName) | 200 — Rocket League, id 3113 |
| `/grids/egs/Sugar?dimensions=512x512,1024x1024` | 200 — 36 grids |

Consequence: every Epic game gets `HasSteamGridDBMatch = false`, so it is excluded from
"Fix my library" entirely, shows the manual-search button instead of the artwork picker, and
falls back to the GitHub name lookup for its title. The README lists Epic auto-matching as
unsupported — this is why, and it is fixable.

**Fix:** `externalPlatformId = parts[parts.Length - 1];` (guarding `parts.Length >= 3`).

---

## 2. Grid and icon fetches pass `XboxPlatformId` instead of `ExternalPlatformId` (Confirmed)

`PrimaryWidget.xaml.cs:758`, `:763`, `:789`, `:1234`, `:1235`

```csharp
List<SteamGridDbGrid> grids = await client.GetSquareGridsByPlatformIdAsync(platformString, game.XboxPlatformId);
```

The game *lookup* uses `ExternalPlatformId` (line 396) but every artwork *fetch* uses
`XboxPlatformId`. For Steam/GOG/uplay the two are identical, which hides the bug. For Epic,
`XboxPlatformId` is `"<namespace>:<catalogItemId>:<appName>"` — verified 404 against
`/grids/egs/...`. So even after fixing #1, artwork fetch would still fail for Epic.

**Fix:** use `game.ExternalPlatformId` at all five call sites. `XboxPlatformId` should only be
used for on-disk filenames and display.

---

## 3. A missing API key aborts the whole library load

`PrimaryWidget.xaml.cs:218-223`

```csharp
if (string.IsNullOrEmpty(steamGridDbApiKey))
{
    StatusText.Text = "Error: SteamGridDB API key is not set.";
}

using (SteamGridDbClient sgdbClient = new SteamGridDbClient(steamGridDbApiKey))
```

The guard sets a status message and then falls straight into a constructor that throws
`ArgumentException("API key is required")` for an empty key. The outer catch overwrites the
message with `Error: API key is required` and the game list stays empty — the user cannot see
their library or use "Restore my changes"/"Revert to defaults", none of which need the API.

**Fix:** `return`-free degradation — skip the SGDB enrichment block when the key is missing and
still enumerate manifests, images, and backups.

---

## 4. Extension rewriting via `String.Replace(".png", …)` can destroy the original image

`PrimaryWidget.xaml.cs:312`, `:873`, `:991-992`, `:1789-1790`

```csharp
string backupFileName = imageFileName.Replace(imageExtension, backupImageExtension);
```

Two distinct failure modes:

1. `Replace` rewrites **every** occurrence, not the extension — `my.png.cover.png` becomes
   `my.bak.cover.bak`.
2. If the filename is not `.png` the replace is a **no-op**, so `backupFileName == imageFileName`.
   `ReplaceImageCoreAsync` then finds the current image when probing for a backup
   (line 999), sets `backupExists = true`, never makes a backup, and overwrites the original
   irrecoverably while marking `HasBackup = true`. A later "Restore backup" deletes that file
   (line 1817) and the rename on the now-deleted handle fails — the game is left with no image.

Custom games take their path verbatim from the manifest (`imagePath`, line 301), so the
extension is whatever the Xbox app wrote. Today's entries are all `.png`, but the Xbox app's
older scheme wrote `.bmp` — `ThirdPartyLibraries\Epic\` still holds
`Epic-…%3ASugar.bmp` files from it. Nothing in the code guarantees `.png`.

**Fix:** `Path.ChangeExtension(imageFileName, ".bak")` / `".new"`, and skip entries whose
extension is unknown rather than silently no-op.

---

## 5. Legacy capitalised store folders load as `Unknown` and are never skipped (Confirmed)

`SteamGridDB.Xbox/Models/GamePlatform.cs:19-37`

`FromXboxDirectory` maps `"ubi"`, `"bnet"`, `"ea"` but the Xbox app also keeps the older
`Ubisoft`, `BattleNet`, and `Epic` folders side by side. Local state right now:

```
BattleNet/            (empty)
Ubisoft/              Ubisoft.manifest, Ubisoft-5266.png, Ubisoft-5487.png
ubi/                  ubi.manifest, ubi_5266.png
```

`"Ubisoft".ToLower()` hits `default:` → `GamePlatform.Unknown`, so those two entries load with
no platform string, no SGDB lookup, and an image path of `5266.png` (the file on disk is
`Ubisoft-5266.png`). Result: two permanent `Unknown / Not found` rows that no button can fix.
The same path means a populated legacy `BattleNet` folder would *not* hit the
`platform == GamePlatform.BattleNet` skip at line 229 — only the lowercase `bnet` folder does.

**Fix:** match the legacy names too (`"ubisoft"`, `"battlenet"`, `"gog"`, `"epic"` already
works), and skip folders whose manifest entries have no matching image rather than listing
ghosts. Note `ToLower()` is culture-sensitive — `ToLowerInvariant()` or a
`StringComparer.OrdinalIgnoreCase` switch is the right primitive here.

---

## 6. `RestoreBackupCoreAsync` deletes the live image before the rename that would replace it

`PrimaryWidget.xaml.cs:1814-1839`

The backup is located first (good — that was the fix for the "130 errors" run), but the current
image is then deleted before `backupFile.RenameAsync(imageFileName, NameCollisionOption.ReplaceExisting)`.
`ReplaceExisting` already overwrites the target, so the delete is redundant *and* it opens a
window where a failed rename (file locked by the Xbox app, for example) leaves the game with no
image at all.

**Fix:** drop the delete of `imageFileName` and let the rename replace it.

---

## 7. No re-entrancy guard; `LoadGameEntriesAsync` appends without clearing

`PrimaryWidget.xaml.cs:165` (append at `:500-505`), `:519`, `:529`, `:565`, `:592`

`GameEntries.Clear()` lives in `RefreshButton_Click`, not in the loader. Two overlapping loads —
a double-clicked Refresh, or a second `Loaded` event — both append to the same collection and
produce duplicate rows. None of the long-running handlers (`FixLibraryButton_Click`,
`RestoreChangesButton_Click`, `RevertDefaultsButton_Click`, `RefreshButton_Click`) disable
their buttons, so a Fix and a Revert can interleave writes on the same image files.

**Fix:** a single `isBusy` flag that disables the header buttons for the duration, and move the
`Clear()` inside `LoadGameEntriesAsync`.

---

## 8. Every game's artwork is decoded at full resolution and held in memory

`PrimaryWidget.xaml.cs:337-351`

`new BitmapImage()` with no `DecodePixelWidth`/`DecodePixelHeight` decodes each 512×512 (or
1024×1024) PNG at native size. The list renders them at 80×80 (`PrimaryWidget.xaml:233-246`).
For a 130-game library that is roughly 130 MB of decoded bitmaps held for the widget's lifetime;
at 1024² it is four times that. Game Bar widgets are memory-constrained.

**Fix:** set `DecodePixelWidth = 160` (80 px at 2× scale) before `SetSourceAsync`.

---

## 9. Image streams are never disposed

`PrimaryWidget.xaml.cs:334`, `:896`, `:1033`, `:1843`

Each `OpenReadAsync()` stream is handed to a fire-and-forget `SetSourceAsync` and dropped. On
load that is one open handle per game against the Xbox app's own image files, released only by
finalisation. The fire-and-forget task's exceptions are also unobserved, so a corrupt image
fails silently.

**Fix:** `await` `SetSourceAsync` inside the dispatcher callback and dispose the stream in a
`finally`.

---

## 10. `GridMetadata` is recomputed three times per grid on every ranking pass

`PrimaryWidget.xaml.cs:1300-1309`

`RankGrids` calls `GridMetadata(g)` in the demote key, again inside `IsEditionMismatch`, and
again in the boost key. Each call runs three `Regex.Replace` operations, so ranking 50 grids
costs ~450 regex passes — repeated for all 130 games during "Re-fix all games", and again every
time the artwork picker opens.

**Fix:** project once — `grids.Select(g => new { Grid = g, Meta = GridMetadata(g) })` — then
order on the precomputed field. The three ranking regexes are already `Compiled`; the three
inside `GridMetadata` are static-method calls that could be `Compiled` fields too.

---

## 11. `DownloadAndReplaceImageCoreAsync` builds its own `HttpClient`

`PrimaryWidget.xaml.cs:959-971`

A per-call `using (HttpClient …)` while `sharedHttpClient` (line 73) is used by every other
download path in the same file, including `DownloadBestTileFillingImageAsync` 100 lines below.

**Fix:** use `sharedHttpClient`.

---

## 12. `SteamGridDbClient` duplicates its URL builder six times, two of them dead

`SteamGridDbClient.cs:127-148`, `:186-205`, `:248-267`, `:297-311`, `:342-361`, `:401-420`

The same `StringBuilder` + `queryParams` + `dimensions`/`styles` join is copy-pasted across
`GetGridsByPlatformIdAsync`, `GetGridsByGameIdAsync`, `GetHeroesByPlatformIdAsync`,
`GetLogosByPlatformIdAsync`, `GetIconsByPlatformIdAsync`, and `GetIconsByGameIdAsync` — about
120 lines that differ only in the path segment. `GetHeroesByPlatformIdAsync` and
`GetLogosByPlatformIdAsync` have no callers at all.

**Fix:** one `BuildUrl(string path, string[] dimensions, string[] styles)` helper; delete the
two unused endpoints (they are trivially recoverable from git if heroes/logos are ever wanted).

---

## 13. `FixLibraryAsync` re-implements the style-tier check

`PrimaryWidget.xaml.cs:760`

```csharp
!grids.Any(g => Array.IndexOf(textBearingGridStyles, g.Style) >= 0)
```

This is exactly `GridStylePriority(g.Style) == 0` (line 1257). Two copies of the same rule means
a future change to the style tiers silently applies to ranking but not to the "should I make the
rescue API call" decision.

**Fix:** call `GridStylePriority`.

---

## 14. Thumbnail clip rectangle is larger than the element it clips

`PrimaryWidget.xaml:444-453`

```xml
<Grid Width="128" Height="128" CornerRadius="6">
    <Grid.Clip><RectangleGeometry Rect="0,0,140,140" /></Grid.Clip>
```

A 140×140 clip on a 128×128 element is a no-op, so the picker's thumbnails are not clipped to
the rounded corners the way the 80×80 library rows are (`:238-240` uses a matching
`Rect="0,0,80,80"`). Likely a leftover from a 140 px design.

**Fix:** `Rect="0,0,128,128"`.

---

## 15. `LoadUbisoftGameListAsync` caches an empty parse permanently

`PrimaryWidget.xaml.cs:1962-2016`

`ubisoftGameLookupCache` is assigned before the parse loop. If the fetch succeeds but yields no
entries (upstream README reformat, HTML error page), the method returns `false` while leaving a
non-null empty dictionary — and the early return at line 1964 makes every later call a no-op.
Ubisoft names stay "Unknown" until the widget is restarted.

**Fix:** build into a local and only assign the static field when `Count > 0`.

---

## Smaller notes

- `PrimaryWidget.xaml.cs:747-752` — `continue` on an unsupported platform increments no counter,
  so the `(n/total)` progress and the final tally under-report. Unreachable today (eligibility
  requires a SGDB match, which requires a platform string) but it is the same accounting shape
  that produced the earlier "130 restored, 130 errors" report.
- `PrimaryWidget.xaml.cs:627-631` etc. — dedupe by `ImageFilePath` correctly processes each image
  once, but the `Image`/`HasBackup` updates are only applied to the group's first `GameEntry`;
  duplicate rows keep stale thumbnails and buttons until Refresh.
- `Package.appxmanifest:26` — `MinVersion="10.0.0.0" MaxVersionTested="10.0.0.0"` contradicts
  `TargetPlatformMinVersion` 10.0.19041.0 in the csproj; deployment gating can't work from that.
- `Models/GridImageItem.cs` — `Id`, `Author`, `Style`, `Score` are populated
  (`PrimaryWidget.xaml.cs:1341-1349`) but the picker's `DataTemplate` binds only `ThumbUrl`.
  Either surface them (author/style would be genuinely useful when picking by hand) or drop them.
- `deploy-dev.ps1:24` — `Get-AppxPackage -Name …` returns a collection; if two versions are ever
  registered, `Remove-AppxPackage $existing.PackageFullName` fails. `Select-Object -First 1` or a
  `foreach` would be safer.
- `.new` files outlive the games they belong to — the local Steam folder holds 134 `.new` against
  121 `.png`. Housekeeping only, no correctness impact.

---

## Suggested order

1. **#1 + #2 together** — one coherent change ("use the SGDB platform ID everywhere") that turns
   Epic from unsupported into supported. Worth verifying against the three local Epic entries.
2. **#4, #6** — the two paths that can leave a game with no image and no backup.
3. **#3, #5, #7** — visible correctness: empty library on missing key, ghost rows, duplicate rows.
4. **#8, #9** — memory and handles; cheap and self-contained.
5. **#10 – #15** — cleanup, safe to batch.
