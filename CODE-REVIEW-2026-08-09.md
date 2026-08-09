# Code review — in-place tile writes and rendition refresh

**Date:** 2026-08-09, updated same day with a verification pass.
**Scope:** the uncommitted working tree (in-place writes, rendition refresh, `WriteResult`) plus a
read of the surrounding codebase for anything the change lands on.
**Method:** static reading, then a second pass that re-verified each finding against the code and —
for the migration findings — against the actual vault and image cache on this machine (read-only).
Findings carry a verdict: **CONFIRMED** (re-derived and, where possible, measured on disk),
**CONFIRMED, corrected** (real, but a detail in the first write-up was wrong), or **RETRACTED**.

**Commits in range:** `a1cdd60` (pushed) and the working tree on top of it — `ArtworkFiles.cs`,
`TileImage.cs`, `XboxTiles.cs`, `XboxLibrary.cs`, `TileRenditionMatcher.cs`,
`PrimaryWidget.xaml.cs`, plus `ArtworkFilesInPlaceTests.cs` and `ImageCacheIndexHandleTests.cs`.

**Outcome (same day, deployed as 1.4.33.0):** findings **1–4, 6 and 12 are fixed** — sidecars now
*fit* rather than *match* (a write grows the file when it must, which only a mapped tile refuses),
a reapply brings a pre-padding `.new` up to date so the overwrite check converges, a restore
destroys the sidecars only after the write succeeds, `RestoreAsync` and `ReapplyOverwrittenAsync`
contain refusals per rendition (with failures returned and partial restores reported as partial),
and the load-time reapply moved out of the row-building `try` so a refused write can no longer cost
the game its row. 481 tests pass, including four new length-mismatch tests and three new
containment tests. Findings 5, 7–11 and 13–16 remain open as noted per finding.

---

## Summary

The in-place write is the right fix for `ERROR_USER_MAPPED_FILE`, and the two hard parts — never
assigning `stream.Size`, and re-basing the overwrite check from length onto content — are both
correct and well argued in the comments.

What the change introduces is a **new invariant that nothing enforces and that every existing
customisation violates**: that a `.new` sidecar is exactly as long as the tile it belongs to. The
verification pass measured this rather than arguing it — see the table below. Findings 1–4 are all
consequences of that one assumption, and **all 23 customised renditions on this machine are in a
failure bucket**. They are the ones to act on before this ships.

Findings 5–8 are real but smaller. 9–15 are efficiency and cleanup.

---

## Measured: the vault today

The old build wrote the tile as the artwork's own bytes (`Replace` mode) and saved the `.new`
unpadded, so today every customised tile's length equals its `.new` length — spot-checked against
the live cache for seven files, all matching. The `.bak` holds the Xbox app's own download, i.e. the
tile's *natural* length, which the app restores whenever it re-fetches.

The vault holds **23 renditions with a saved customisation** (plus two `.bak`-only entries from this
session's repair). Not one has `.new` equal in length to its `.bak`:

| Bucket | Count | What happens under the new build |
|---|---|---|
| `.bak` > `.new` | 13 | **Restore backup throws today.** `RestoreOriginalAsync` deletes the `.new`, then `WriteInPlaceAsync` refuses to fit the larger `.bak` into the tile (currently at `.new` length) — `InvalidOperationException`, after the customisation copy is already gone. Widest measured gap: `3160869401328248714`, a 55013-byte `.bak` against a 24705-byte tile. |
| `.new` > `.bak` | 10 | Restore works (pads). But **once the Xbox app overwrites the tile at its natural length** — routine: eviction, ninety-day refresh — the load-time reapply can never fit the larger `.new` back in. It throws on every load, and finding 3 makes that the game vanishing from the widget permanently. Includes `12826931490866612294`, the file **both Minecrafts** record: both rows go at once. |

The 13-bucket has a second act: after the app overwrites one of those tiles, restore starts working
(lengths align again) but reapply enters finding 1's permanent rewrite loop — the smaller `.new`
pads in fine and never compares equal.

---

## Correctness

### 1. Saved customisations from earlier builds are unpadded, and the reapply path assumes they are not — **CONFIRMED, corrected**

`SteamGridDB.Xbox/Services/Artwork/ArtworkFiles.cs:452` · `SteamGridDB.Xbox/Services/Xbox/XboxTiles.cs:177`

`ApplyEncodedAsync` now saves the `.new` as **what landed on disk** — the artwork padded to the
tile's length (`ArtworkFiles.cs:193–203`). `MatchesSavedCustomisationAsync` and
`ReapplyCustomisationAsync` both consume that assumption, and every existing `.new` violates it (see
table).

**Correction to the first write-up:** this does *not* fire "on the first load after this build".
While a tile still holds the old build's unpadded artwork, tile and `.new` are byte-identical, the
content check passes, and nothing happens. The trigger is the Xbox app overwriting the tile at its
natural length — after which, for the 13-bucket:

- `MatchesSavedCustomisationAsync` (`XboxTiles.cs:369`) compares lengths — natural vs `.new` — and
  reports the tile overwritten. Correct, once.
- `ReapplyCustomisationAsync` pads the `.new` to the natural length and writes it. Also correct.
- But the `.new` file is **never rewritten**, so the length mismatch is permanent: every subsequent
  load repeats the compare-fail-and-rewrite. The property `ReapplyOverwrittenAsync`'s doc claims —
  *"a load that has nothing to do writes nothing"* — is silently false forever for that rendition.

For the 10-bucket the same trigger is worse: the `.new` is *larger* than the natural tile, so the
reapply write throws instead — see finding 3 for where that lands.

*Suggested fix (covers 1–4):* stop requiring the `.new` to equal the tile's length. Treat a `.new`
shorter than the tile as artwork and compare/pad accordingly; treat one longer as artwork too, and
re-encode or refuse gracefully rather than throw. Rewriting the `.new` padded on first touch also
works for the short case, but only re-encoding rescues the long one.

### 2. Tile length is treated as invariant, and the dominant violation is already on disk — **CONFIRMED, reframed**

`SteamGridDB.Xbox/Services/Artwork/ArtworkFiles.cs:396–408`

`RestoreOriginalAsync`'s InPlace branch asserts:

> *The lengths match by construction here: the backup is the file this artwork was written over, and
> an in-place write is what has kept it that length ever since.*

"By construction" holds only for customisations both created and maintained by the new build. The
first write-up led with a speculative violation (the Store re-encoding an asset at the same URI —
possible, but the codebase's own docs say artwork changes usually arrive under a *new* cache key).
The verification pass found the real violation is simpler and universal: **every existing
customisation predates the construction.** Thirteen restores would throw today; the measured
worst is `3160869401328248714`, a 55013-byte `.bak` against a 24705-byte tile.

Same remedy as finding 1: require only that the payload fits, not that lengths are equal — and see
finding 4 for the ordering that makes the throw destructive.

### 3. A reapply throw at load drops the game from the library — and the new failure cause is permanent — **CONFIRMED, refined**

`SteamGridDB.Xbox/Services/Xbox/XboxTiles.cs:151` · `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:925`

`ReapplyOverwrittenAsync` is called at `PrimaryWidget.xaml.cs:925`, inside the same `try` that
builds the `GameEntry` at `:964`; a throw is caught at `:980`, logged `not shown Xbox/<storeId>`,
and the game gets no row — no thumbnail, no Restore button, no route to the `.bak`.

**Refinement:** the row-drop itself is pre-existing and deliberate — the comment at `:916–921` says
the per-game `try` exists precisely because reapply can throw `UnauthorizedAccessException` when the
app holds a file open, and skipping the game for one load beat losing every game after it. That was
an acceptable trade for a *transient* cause. What this change adds is a **persistent, on-disk**
cause: a `.new` larger than its tile (the measured 10-bucket) throws `InvalidOperationException`
from `WriteInPlaceAsync` on every load, and nothing ever clears it — the `.new` is only discarded
when its tile leaves the cache, and the tile is still there. A transient skip becomes a permanent
disappearance, for ten renditions on this machine including both Minecrafts.

`ApplyAsync` gained a per-rendition catch in this change with reasoning (`XboxTiles.cs:39–45`) that
applies verbatim to `RestoreAsync` and `ReapplyOverwrittenAsync`; neither got one, even though this
change is what made restore able to throw at all (under `Replace`, restore was an atomic move).

*Suggested fix:* per-rendition catches in both, mirroring `ApplyAsync`; and consider moving the
reapply call out of the row-building `try`, so a write failure costs the artwork and not the row.

### 4. `RestoreOriginalAsync` destroys the saved customisation before the write that can now fail — **CONFIRMED**

`SteamGridDB.Xbox/Services/Artwork/ArtworkFiles.cs:385–408`

The order is: locate `.bak` → **delete `.new`** → write the backup in place → delete `.bak`. Under
`Replace` the write was a one-step `RenameAsync`/`MoveAsync` chosen for exactly that atomicity (the
comment at `:410–412` says so); under `InPlace` it is a length-checked write that throws for the
13-bucket today.

Verified walk-through against the machine's actual state — *Restore backup* on Fuzion Frenzy
(`C2P985H1H42H`, renditions largest-first):

1. `8501456819577244772` (329px): `.bak` 48698, tile 48698 → restores, `.bak` deleted.
2. `15777377109655831270` (224px): `.bak` 25794, tile 25794 → restores, `.bak` deleted.
3. `7108528410040166518` (84px): `.new` (2968) **deleted first**, then `.bak` 5575 into a 2968-byte
   tile → `InvalidOperationException` → propagates through `RestoreAsync` (no per-rendition catch,
   finding 6) → `RestoreBackupCoreAsync`'s catch → *"Error restoring backup for Fuzion Frenzy"*.

Net state: two renditions restored, the 84px still customised with its `.new` gone, its `.bak`
stranded (so the Restore button stays), and every retry failing the same way.

*Suggested fix:* delete the `.new` after the write succeeds, not before.

### 5. A partial write reports unqualified success in the picker, which is the surface that needed the truth — **CONFIRMED**

`SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1938–1940` · `:1499–1509`

`ReplaceImageCoreAsync` returns `WriteResult.Success` whenever `written >= 1`, so
`DownloadAndReplaceImageAsync` prints `"Image updated successfully"` and closes the panel after
250ms. `AppliedMessage` builds exactly the message that matters — *"partly updated — 1 tile could
not be written"* — and hands it to `UpdateSharedEntriesAsync`, which writes it to `StatusText`.
Which `WriteResult`'s own doc comment says this panel covers completely.

The concrete case: the 329px write refused while 224px and 84px succeed. The picker says it worked
and closes; the library grid — which draws the 329px — is unchanged. That is the original symptom
this change set was chasing, still reachable through the partial path.

*Suggested fix:* carry `writeFailures` in `WriteResult` and show the partial message in
`GridPanelStatus`, without auto-closing.

### 6. `RestoreAsync` lacks the per-rendition catch `ApplyAsync` gained — **CONFIRMED**

`SteamGridDB.Xbox/Services/Xbox/XboxTiles.cs:112`

A throw on one rendition abandons the rest *and* propagates. The Fuzion Frenzy walk-through in
finding 4 shows the failure is ordering-dependent: had the 84px been first in the record, the two
renditions that can restore cleanly would never have been reached. Covered by finding 3's fix.

### 7. A backup taken before a failed write fakes a customisation — **CONFIRMED, pre-existing but amplified**

`SteamGridDB.Xbox/Services/Artwork/ArtworkFiles.cs:191` · `SteamGridDB.Xbox/Services/Xbox/XboxTiles.cs:229`

`BackupOnceAsync` runs before the write; a rendition whose write throws keeps its fresh `.bak` with
no `.new`. `XboxTiles.HasBackup` then reports the game customised, the Restore button appears for a
tile nobody changed, and the rule `ForgetArtworkRecordsAsync` documents — *no backup means no
customisation* — has its converse falsified, which `PrimaryWidget.xaml.cs:954–962` acts on.

**Refinement:** this ordering predates the change — the old write could throw too (that is the bug
this whole change fixes), leaving the same stranded `.bak`. What is new is that `ApplyAsync`'s
per-rendition catch turns a loud whole-apply failure into a quiet partial success, so the stranded
backup now arrives silently. Fix in `ApplyAsync`'s catch: delete a `.bak` this call created for a
rendition that then failed to write (`ApplyEncodedAsync` already knows whether it pre-existed).

### 8. Mutating `game.ImageFilePath` before `UpdateSharedEntriesAsync` orphans rows sharing the old path — **CONFIRMED, narrow**

`SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1563` vs `:1581`

`GameImages.SharingImage` matches on the entry's *current* path, which by then is the new one; rows
sharing the old path are never updated (their `ImageFileName` is bound at `PrimaryWidget.xaml:301`
and `:642`). Requires two first-party rows sharing a path *and* the primary changing in this apply —
narrow, but the first half is real today: both Minecrafts record `12826931490866612294`. The clicked
row itself always updates (`SharingImage` matches it against itself).

*Suggested fix:* capture `EntriesSharingImage(game)` before reassigning, or pass the old path in.

---

## Efficiency

### 9. The overwrite check now reads every customised rendition twice on every load — **CONFIRMED**

`SteamGridDB.Xbox/Services/Xbox/XboxTiles.cs:369–378`

The move from length to content is correct and necessary. The cost: `SizeIfPresentAsync` read
directory entries; `ReadIfPresentAsync` reads both files in full, per customised rendition, per
load. This machine's 23 customised renditions cost ~46 full reads of 3–55KB per widget open. A
digest stored beside the `.new`, or comparing only the first `saved.Length` bytes, gives the same
answer. (Note the fix for finding 1 changes what "equal" means here anyway — do these together.)

### 10. `RefreshRenditionsAsync` pays full discovery cost on every apply, including when complete — **CONFIRMED, accepted by design**

`SteamGridDB.Xbox/Services/Xbox/XboxLibrary.cs:355`

Each first-party apply costs a catalogue request, a header-decode of every cache file
(`ImageCacheIndex.BuildAsync`) and up to ~16 CDN fetches, even when the record is already complete —
`LocateAsync`'s `SequenceEqual` guard skips the *write*, not the work. The plan accepted this cost
against a deliberate user action that already downloads a full-size image, so this is a note, not a
defect. A cheap short-circuit exists if it ever matters: skip when the record's largest rendition
still exists at a size no cached candidate exceeds.

### 11. `ApplyAsync` reads each rendition in full to obtain two numbers — **CONFIRMED, minor**

`SteamGridDB.Xbox/Services/Xbox/XboxTiles.cs:337–353`

`GetBasicPropertiesAsync` gives the length without reading; the decoder needs only the header. Not a
regression — the old `RenditionSizeAsync` also read in full — but the method was rewritten here and
the cheaper shape was available.

---

## Test coverage

### 12. Nothing pins the invariant findings 1–4 turn on — **CONFIRMED**

`SteamGridDB.Xbox.Tests/ArtworkFilesInPlaceTests.cs`

The nine tests are well chosen, and `A_padded_jpeg_still_decodes_at_its_own_size` pins the
assumption the scheme rests on. But none varies the length between writing a sidecar and reading it
back — the exact case the measured migration data shows is every existing customisation. Three tests
worth writing against the fixed behaviour:

- a `.new` shorter than its tile (the 13-bucket) — assert the reapply *converges*: a second reapply
  writes nothing.
- a `.new` longer than its tile (the 10-bucket) — assert the intended behaviour, whatever the fix
  chooses, rather than an escaping `InvalidOperationException`.
- a restore whose in-place write fails — assert the `.new` survives.

`RestoreOriginalAsync`'s "lengths match by construction" is an invariant a comment states, no test
holds, and the disk falsifies. `TESTING.md` makes the argument itself: these files are the ones a
mistake destroys unrecoverably.

### 13. ~~One test covers a path production cannot reach~~ — **RETRACTED**

The first write-up claimed `WriteImageAsync`'s `FileNotFoundException` fallback (create the file)
was unreachable under `InPlace` because `ApplyAsync` skips absent renditions. Wrong: the fallback
*is* reachable through `RestoreOriginalAsync` and `ReapplyCustomisationAsync` when the Xbox app
evicts a tile between the library load and the button press — the load's `SurvivingRenditionsAsync`
filter only protects the load itself. The test stands.

What the verification pass noticed instead, for the record: that fallback **creates a file in the
Xbox app's cache folder**, which `XboxTiles`' own class doc says is never done — *"a rendition the
Xbox app has since deleted is skipped rather than recreated, because … a file it has no row for is
one it will remove again."* Pre-existing under `Replace` mode too (`CreateFileAsync` with
`ReplaceExisting` also creates absent files), so this is a doc/behaviour inconsistency of long
standing, not a regression. Minor.

---

## Cleanup

### 14. Raw HRESULT text reaches the user — **CONFIRMED**

`SteamGridDB.Xbox/Services/Xbox/XboxTiles.cs:99`

`$"{size}px ({ex.GetType().Name}: {ex.Message})"` produced, verbatim in the picker, the
`0x800704C8` message the user screenshotted. The encoder-budget message beside it is written in
plain words; the one refusal this change exists to handle deserves the same.

### 15. `AppliedMessage` re-derives `DisplayName`'s fallback inline — **CONFIRMED, softened**

`SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1503–1505`

**Correction:** the success-branch ternary was not newly written — it is the pre-existing status
text moved verbatim into `AppliedMessage`. And it is not a drop-in `DisplayName` call: the unknown
branch reads *"Artwork {file} updated"*, not *"Artwork for {file} updated"*, so unifying changes
user-visible phrasing slightly. Still worth doing — the failure branch two lines down already uses
`DisplayName`, and the two can drift — but it is a smaller item than first framed.

### 16. Two silent narrowings in the new write path — **CONFIRMED, minor**

`SteamGridDB.Xbox/Services/Artwork/ArtworkFiles.cs:299` and `:316`

`(uint)(…).Size` truncates a `ulong` unchecked, and `stream.WriteAsync(padded)`'s written-count is
discarded. Neither can bite on tile-sized files; both fail silently rather than loudly if that ever
stops holding.

---

## Checked and found clean

Worth recording so a later pass does not re-derive them:

- **`ImageCacheIndex` leaves no file handles open** — disproved as a cause by
  `ImageCacheIndexHandleTests.cs`, kept as a permanent invariant.
- **`TileRenditionMatcher.Merge` is a union, and has to be** — a customised rendition no longer
  matches the artwork that would find it; a replace would orphan its `.bak`.
- **`OrderByDescending` is stable in LINQ-to-Objects**, so `Merge`'s arrival-order comment is
  accurate.
- **`StoreCatalog.Product` is a `readonly struct`**, so `FirstOrDefault` yields a default with null
  `TileArtworkUris` rather than a null reference — the guard at `XboxLibrary.cs:373` is right.
- **`DeclaresXboxLiveGameAsync`'s exception-filter ordering is correct.**
- **Padded JPEGs index correctly** — `BuildAsync` decodes a padded tile to its true pixel size, so
  `Merge` orders customised renditions correctly.
- **`AppliedArtworkStore` old-key clear happens before the reassignment** it depends on
  (`PrimaryWidget.xaml.cs:1561` before `:1563`) — verified, correct order.
- **The refresh saves the record before any bytes are applied** (`LocateAsync` writes via
  `XboxTileStore.SetAsync` before returning) — the plan's ordering requirement holds.
- **The test project globs `Services\**\*.cs`**, so the two new test files need no `.csproj` entry;
  the app project still needs explicit entries for any new file (`TESTING.md`).

---

## Suggested order

1. **Findings 1–4 together, before this build's load path runs against the real vault.** They are
   one assumption; the fix is one shape: stop requiring `.new`/`.bak` to equal the tile's length
   (fit, don't match), handle the too-long case deliberately, and delete the `.new` only after a
   restore's write succeeds. The measured table says this is not defensive: all 23 existing
   customisations hit one bucket or the other.
2. Finding 3's per-rendition catches, and moving `ReapplyOverwrittenAsync` out of the row-building
   `try`. Cheap, and it turns every remaining case from "game vanishes" into "one tile did not
   update".
3. Finding 5 — the partial-write message. It is the symptom the user reported.
4. Finding 12's three tests, written against the fixed behaviour.
5. The rest as convenient.

**Caution resolved:** the warning that stood here — *Restore backup* on 1.4.32.0 deleting the
`.new` and then failing — is fixed in 1.4.33.0, which restores every measured bucket. A full
snapshot of the vault and both records as they stood before any of this was taken first and
verified byte-identical, at `C:\Users\jerem\SteamGridDB-vault-backup-2026-08-09` — delete it once
restores have been confirmed working.
