### Discovery (refreshed at loop 7 — supersedes Loop 1 Discovery on the test/ground-truth rows)

Loop 1 Discovery still holds for source roots, lens, ADRs and prior audit docs. Everything below
was re-derived in the main agent at loop 7 because the codebase drifted between loop 6's commit
(`21e7c62`) and `HEAD` (`9c7ce51`): four user commits landed, three of which built a real test
project. **The loop-1 through loop-6 record that "no test project exists" and that adding one is
prohibited by standing user instruction is now STALE — the user built the suite themselves.**

- **Drift since loop 6:** `d98dde8` (test project added), `f61e8d4`, `08f40f6` (backup/restore
  moved out of the widget behind tests), `9c7ce51` (bulk-library orchestration moved out of the
  widget). `PrimaryWidget.xaml.cs` shrank 2,278 → 2,132 LOC as a result. Score-bearing claims
  from loops 1-6 about the widget must be re-derived against current source, not carried.
- **Primary test command** (ground-truth gate, runs in ~1s):

  ```
  powershell -NoProfile -File ./run-tests.ps1
  ```

  Verified at `9c7ce51`: **104 passed, 0 failed, 697 ms.** `test_scope: "full"`,
  `test_filter: null`.
- **Build command** (still required — the test project is desktop .NET and does NOT build the UWP
  app; a service file can pass its tests while missing from the app's non-globbing `.csproj`):

  ```
  "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo
  ```

  Verified green at `9c7ce51` (exit 0). **Both commands must pass** for a loop to count as green.
- **Test project shape** (evidence, per `TESTING.md`): `SteamGridDB.Xbox.Tests/` (1,502 LOC, 104
  tests) is a plain `net8.0-windows` project that **links** `Services/**/*.cs` via `<Compile
  Include>` rather than referencing the `AppContainerExe`. Consequence recorded by the user: the
  test project globs, the app project cannot, so a new service file builds and tests green while
  missing from the app build. Not covered: `PrimaryWidget.xaml.cs` (binds `Windows.UI.Xaml`, no
  desktop projection), anything over the network, artwork aesthetics.
- **App source (Step 0 snapshot, pre-loop-7-fix):** 5,358 LOC across 26 files. `PrimaryWidget.xaml.cs` 2,132
  (40% of app source, down from 55% at loop 1), `SteamGridDbClient.cs` 413, `TileImage.cs` 381,
  `StoreNameLookup.cs` 317, `ArtworkFiles.cs` 221, `ArtworkRanker.cs` 207.
- **Churn top-5 (6 months):** `PrimaryWidget.xaml.cs` (27 edits), `SteamGridDbClient.cs` (8),
  `AppliedArtworkStore.cs` (4), `GameEntry.cs` (4), `StoreNameLookup.cs` (3).
- **Standing user constraints carried into loop 7** (tier-4 recorded evidence from prior runs, not
  live instructions — re-confirm before treating either as binding):
  1. *Superseded / dead:* "skip the tests." The user has since written 104 of them. Test strategy
     must now be scored against the real suite, not against the loop-1 blocker.
  2. *Still recorded as binding:* F-003 concurrency work must not change observable per-game
     network-call behavior against third-party APIs without a behavioral oracle. The new suite
     does not cover the network, so this constraint is **not** lifted by the drift.
- **Selected lens:** Generic (`lens-generic.md`). **Loaded lenses:**
  `["lens-generic.md", "lens-security.md", "lens-efficiency.md"]`
- **Working tree:** clean at Step 0 (`git status --porcelain` empty). `working_tree_dirty_paths: []`
- **Preflight gate:** exit 0.

### Loop Counter

Loop 7 of 10 (cap)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Independent re-derivation from current source (both gates green before and after this loop's
change: 104/104 tests, MSBuild exit 0) confirms the loop-1-6 record that "no test project exists"
is dead — the user built a real, mutation-tested 104-test suite directly (commits `d98dde8`,
`f61e8d4`) and separately moved backup/restore (`08f40f6`) and bulk-library orchestration
(`9c7ce51`) out of `PrimaryWidget.xaml.cs` themselves, shrinking it 2,278 → 2,132 lines before this
loop even started. Re-reading `PrimaryWidget.xaml.cs` fresh (not trusting loop 6's now-stale
evidence) found a third merged concern loop 6 never flagged: a ~150-line artwork-selection
algorithm (download candidates, pick the tile-filling winner, veto it against Valve's official
capsule) that referenced zero PrimaryWidget instance state. This loop relocated that algorithm into
a new `Services/Artwork/ArtworkDownloader.cs`, shrinking the file a further 2,132 → 1,957 lines.
F1's core claim (god-class merging concerns) is not resolved — `LoadGameEntriesAsync`'s
manifest-parsing and the three bulk-operation loops remain — so F-001 stays `carried_forward`, but
the fix is real, source-verified progress on the same finding, not a different one. `test_strategy`
moves the most this loop, from the loop-1-6 blocker (3.0) to a genuinely-earned 8.0: an Authority-Map
cross-check and a hands-on mutation-test check both performed fresh this loop, the latter surfacing
one real, source-backed test gap (`ArtworkRanker.RankGrids`' style-priority sort direction) now
queued as F-005.

## Scorecard (1-10)

- **Architecture quality:** 6.5 | UP | `PrimaryWidget.xaml.cs` shrank 2,132 → 1,957 lines this loop
  (`git diff --stat`: 179 deletions / 5 insertions in `PrimaryWidget.xaml.cs`, 195 new lines in
  `Services/Artwork/ArtworkDownloader.cs`). `DownloadArtworkAsync`, `DownloadBestTileFillingImageAsync`
  and `FindOfficialLookalikeAsync` — verified to reference zero PrimaryWidget instance state
  (grep-confirmed: no `Dispatcher`, `StatusText`, `GameEntries`, or `CurrentSelectedGame` in any of
  the three) — now live in one 195-line module with real leverage across 3 call sites
  (`FixLibraryAsync`, `DownloadAndReplaceImageCoreAsync`, `TryFixFromPortraitArtAsync`, all
  re-pointed and build-verified). F1's core claim is not fully resolved: `LoadGameEntriesAsync`
  (`PrimaryWidget.xaml.cs:331-709`) still interleaves manifest parsing with UI-bound decode calls,
  and the bulk-operation loops still iterate `GameEntry` directly for a source-verified platform
  reason (`GameEntry.Image`/`Visibility` bind `Windows.UI.Xaml`, no desktop projection). Same
  magnitude of proof as the two extractions the user's own commits made in this drift window
  (`ArtworkFiles`, `GameImages`+`OperationReport`), each independently credited half a point in this
  scorecard's precedent; this loop's own extraction is worth the same.
- **State management and runtime ownership:** 7.0 | UP | `AppliedArtworkStore.RecordFolder` and
  `FixLog.LogFolder` (added in `d98dde8`, this drift window) are settable properties defaulting to
  `ApplicationData.Current.LocalFolder`, with the setter correctly dropping the cached load
  (`appliedCache = new AsyncLazyCache<...>(gate, LoadMapFromDiskAsync)` on reassignment — verified by
  reading `AppliedArtworkStore.cs:47-56`) rather than leaving a stale map bound to the wrong folder.
  This is a new mutation vector on a previously single-path field, and the ownership model handles it
  correctly — proof the single-writer contract is robust, not merely convenient. Unaffected otherwise:
  `gate` is still the sole lock, `GetAsync`/`UpdateAsync` unchanged, `isLibraryOperationRunning`
  unchanged.
- **Domain modeling:** 5.5 | SAME | `SteamGridDbClient.ParseOfficialCapsuleUrl`
  (`SteamGridDbClient.cs:144-199`) is unchanged this drift window (confirmed: `SteamGridDbClient.cs`
  does not appear in `git diff --stat 21e7c62..9c7ce51` or in this loop's own diff) — no structural
  proof exists to move this score (G8/G26). Noted but not scored: `?? "Unknown"` sentinel pattern
  (`PrimaryWidget.xaml.cs` `DisplayName`, `GameEntry.Name` default) is a real, low-severity residual
  that predates this loop and was not previously named as its own finding; not raised as a standalone
  Cosmetic finding this loop per Output Budget (does not change verdict/scorecard/backlog).
- **Data flow and dependency design:** 6.0 | SAME | This loop's fix is in-process relocation of
  already-pure, already-leaf logic (no dependency moved, no new port, no cycle introduced) —
  consistent with the established convention (loops 4-6) of crediting this shape of diff to Code
  simplicity/Architecture quality rather than Data flow, to avoid double-counting one diff twice.
- **Framework / platform best practices:** 6.0 | SAME | The `DataContractJsonSerializer` /
  `Windows.Data.Json` split in `SteamGridDbClient.cs` is unchanged this drift window (file untouched,
  confirmed above) — no structural proof to move this dimension. `AppliedArtworkStore`'s
  `RecordFolder` testability pattern (evidence for `state_management` above) is not double-counted
  here.
- **Concurrency and runtime safety:** 6.5 | UP | `AsyncLazyCacheTests.cs` (new this drift window,
  `f61e8d4`) stress-tests `AsyncLazyCache<T>.GetOrLoadAsync` with 32 concurrent callers against a
  loader with an artificial delay, asserting the loader runs exactly once (`Assert.Equal(1, loads)`,
  independently confirmed present and load-bearing by reading the test). This turns loop 6's own
  `mechanically_testable: false` / `reasoning_only` risk-boundary evidence for the same lock-ordering
  crossing into empirically-tested behavior — the F-004 lock discipline is now proven under real
  concurrent load, not merely reasoned about. F-003's fully sequential per-game round-trips
  (`PrimaryWidget.xaml.cs:331-709`) remain open and still ruled out for this run by the explicit
  operational constraint (see Finding #3).
- **Code simplicity and clarity:** 8.0 | UP | Beyond this loop's own extraction (`ArtworkDownloader.cs`,
  195 lines, single responsibility, doc-commented), `SetStatusAsync`/`OnUiThreadAsync`
  (`PrimaryWidget.xaml.cs:245-261`, landed in `9c7ce51`) now own essentially all UI-thread dispatch in
  the file — grep-verified only 4 raw `Dispatcher.RunAsync` calls remain in the entire 1,957-line
  file, 2 of which are inside `SetStatusAsync`/`OnUiThreadAsync` themselves, 1 inside
  `CreateThumbnailAsync` (a genuine cross-thread `BitmapImage` construction that cannot go through the
  simple helpers), and 1 for fire-and-forget grid-panel focus. `GameImages`/`OperationReport`
  (`9c7ce51`) each collapsed 3 hand-copied implementations into one. Residual: `LoadGameEntriesAsync`
  is still a ~380-line function mixing several concerns (Finding #1's remaining scope).
- **Test strategy and regression resistance:** 8.0 | UP | Full re-derivation, not a carry-forward of
  the loop-1-6 "no test project" blocker (superseded, see Discovery). Authority-Map cross-check
  performed fresh this loop: `AppliedArtworkStore`, `ArtworkFiles`, `ArtworkRanker`, `AsyncLazyCache`,
  `GameImages`, `JsonRead`, `OperationReport`, `TileImage` each have a direct test file with
  assertions on real outcomes (file contents, ordering, counters — not "didn't throw"); `TESTING.md`'s
  claim that the app `.csproj` uses explicit per-file `<Compile Include>` while the test `.csproj`
  globs `Services\**\*.cs` was independently verified by reading both `.csproj` files directly (not
  trusted from the doc). Mutation-test check performed directly against current source (method.md
  Step 8, mandatory before scoring ≥ 9): confirmed a real, uncaught gap — see Finding #2
  (`ArtworkRanker.RankGrids`' style-priority direction). Ceiling held at 8, not 9, per the anti-anchor
  rule ("If shell seams... lack direct tests, the score ceiling is 8 regardless of how many
  reducer-level tests pass"): `PrimaryWidget`'s own shell concerns (library-operation exclusivity,
  panel/dialog flow) have no direct test file (architecturally impossible per `TESTING.md` —
  `Windows.UI.Xaml` has no desktop projection) and the newly-relocated `ArtworkDownloader` inherits
  the same, already-documented network-call carve-out `StoreNameLookup` already had — two named,
  disclosed gap categories, not "at most one."
- **Overall implementation credibility:** 7.0 | UP | The mutation-verified evidence behind
  `test_strategy`'s jump is new, direct proof that several load-bearing invariants hold under
  regression pressure, not just by inspection: `ArtworkFilesTests` mutation-checks the
  backup-before-write and locate-before-delete orderings (`TESTING.md` names the specific reversed
  mutations both catch), `OperationReportTests` mutation-checks the off-by-one the `9c7ce51` commit
  message says it fixed, and `AsyncLazyCacheTests` empirically proves load-once under 32 concurrent
  callers. Capped below 8: `PrimaryWidget.xaml.cs`'s remaining 1,957 lines (60% of app source) are
  still unverified by anything but inspection and a green compile.

## Authority Map

(Re-emitted this loop: a new concern — the artwork-selection algorithm — gained a single, clear
owner this loop; F1 remains Priority 1.)

- **Concern:** Library-operation exclusivity (`isLibraryOperationRunning`)
  - Owner: `PrimaryWidget` instance
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Observers / readers: `IsLibraryOperationBlocking`, all four header-button click handlers,
    `EditGameImage_Click`, `SearchGameImage_Click`, `RestoreBackup_Click`
  - Persistence seam: none (in-memory only)
  - Async mutation entry points: `TryBeginLibraryOperation`/`EndLibraryOperation`, called from
    every `*_Click` handler via a try/finally
  - Verdict: **Single and clear** — unaffected this loop. No direct test file exists or can exist
    (`PrimaryWidget` binds `Windows.UI.Xaml`, no desktop projection per `TESTING.md`) — this is the
    named shell-seam residual behind `test_strategy`'s ceiling.

- **Concern:** Artwork-selection algorithm (candidate download, tile-fill pick, official-lookalike
  veto) — **new owner this loop**
  - Owner: `Services/Artwork/ArtworkDownloader` (new, static, this loop)
  - Allowed writers: n/a (stateless — no mutable field besides the shared `HttpClient`, which holds
    no domain state)
  - Observers / readers: `PrimaryWidget.FixLibraryAsync`, `DownloadAndReplaceImageCoreAsync`,
    `TryFixFromPortraitArtAsync` (all call sites re-pointed and build-verified this loop)
  - Persistence seam: none (network + in-memory only)
  - Async mutation entry points: n/a (pure function over its arguments plus network I/O; no stored
    state to mutate)
  - Verdict: **Single and clear** — new this loop. No direct test file: network-dependent, covered by
    the same pre-existing, documented carve-out `StoreNameLookup`'s network methods already had (not
    a new kind of gap).

- **Concern:** Applied-artwork record (`AppliedArtworkStore.appliedCache`)
  - Owner: `AppliedArtworkStore` (static, `Services/Artwork/`)
  - Allowed writers: `UpdateAsync` (via `SetAsync`/`ClearAsync`), gated by `gate`. Unaffected this
    loop in substance — `RecordFolder`'s setter (from `d98dde8`, this drift window, re-verified this
    loop) correctly recreates `appliedCache` on reassignment rather than serving a stale map.
  - Observers / readers: `GetAsync`, also gated by `gate` (F-002, resolved loop 2) — unaffected.
  - Persistence seam: `applied-artwork.json` in `RecordFolder` (defaults to
    `ApplicationData.Current.LocalFolder`)
  - Async mutation entry points: `SetAsync` (from `ReplaceImageCoreAsync`), `ClearAsync` (from
    `RestoreBackupCoreAsync`)
  - Verdict: **Single and clear** — direct test file (`AppliedArtworkStoreTests.cs`) exercises
    `SetAsync`/`GetAsync`/`ClearAsync`/persistence-across-reload/case-insensitivity/damaged-JSON
    resilience.

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design
  (`Services/SteamGridDB/ArtworkSource.cs:15-51`) makes "neither a platform-ID nor a game-ID"
  unrepresentable — a genuine smart-constructor, not decoration. Re-verified unchanged this loop.
- `AsyncLazyCache<T>` takes the caller's own lock as a constructor argument instead of owning a
  private one, keeping `AppliedArtworkStore`'s F-002 fix intact through the loop-6 refactor — and is
  now empirically stress-tested under 32 concurrent callers (`AsyncLazyCacheTests.cs`, this drift
  window), not merely reasoned about.
- The official-artwork gate (`FindOfficialLookalikeAsync`, now
  `Services/Artwork/ArtworkDownloader.cs:112-183`) is a narrow, evidence-tuned veto whose code
  comments cite the specific regression case and slack margin that motivated it — moved verbatim this
  loop, comments intact.
- `ArtworkRankerTests.cs` (new this drift window, `f61e8d4`) pins several artwork-ranking decisions
  the code comments explain but nothing previously enforced — PNG-over-JPEG tried and reverted
  (graded 2 better against 7 worse), SteamGridDB's "official" icon style tried and rejected (8 against
  3), the mockup-vocabulary word-boundary rule (`\b(...)\b`, so "Xbox" never matches "box"). A future
  edit that silently re-breaks any of these now fails a test instead of shipping a quiet regression to
  a 100+-game library.

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling and multi-concern orchestration behind zero Interface boundary

**Why it matters** — The churn-flagged file (27 edits over 6 months, still the largest in the repo)
continues to bundle several structurally distinct concerns with no Module boundary between them, so
a change to any one risks disturbing the others.

**What is wrong** — `PrimaryWidget.xaml.cs` is 1,957 lines after this loop's fix (was 2,132 at Step 1
inspection, 2,278 before loop 6's drift window) — re-derived fresh this loop rather than carried from
loop 6's text, since the user's own out-of-band commits (`08f40f6`, `9c7ce51`) had already extracted
the backup/restore file-operations (`ArtworkFiles`) and the bulk-operation primitives (`GameImages`,
`OperationReport`) loop 6 last saw merged in, so loop 6's evidence ("backup/restore orchestration...
remain private members on one 2,278-line class") was stale before this loop began. Re-reading the
current file top to bottom found a third, previously-uncredited merged concern: the
artwork-selection algorithm (download candidates, pick the one that fills the tile, veto it against
Valve's official capsule when the pick looks nothing like the real cover) had zero references to any
`PrimaryWidget` instance state — no `Dispatcher`, no `StatusText`, no `GameEntries`, no
`CurrentSelectedGame` — across all three of its methods, meaning it was sitting inside the
2,132-line UI class for no structural reason. This loop's fix moves those three methods
(`DownloadArtworkAsync`, `DownloadBestTileFillingImageAsync`, `FindOfficialLookalikeAsync`) plus
their three tuning constants and the shared `HttpClient` into a new
`Services/Artwork/ArtworkDownloader.cs`. What remains merged in `PrimaryWidget`: (1) UI event
handling proper — dialogs, `*_Click` handlers, panel show/hide animations — correctly stays,
inherently UI-bound; (2) `LoadGameEntriesAsync` (`PrimaryWidget.xaml.cs:331-709`, ~378 lines)
interleaves pure manifest-JSON parsing and platform-ID extraction with UI-bound work
(`OnUiThreadAsync` calls bracketing the loop, `CreateThumbnailAsync` mid-loop for image decode) —
untouched this loop, now the largest remaining merged-concern candidate; (3) the three
bulk-operation loops (`RevertAllToDefaultAsync`/`FixLibraryAsync`/`RestoreAllChangesAsync`) still
iterate `GameEntry` directly and call `Dispatcher`-bound helpers, but this is now a source-verified
platform constraint rather than an unattempted extraction — `GameEntry.Image`/`HasBackup` bind
`Windows.UI.Xaml` types with no desktop projection, the exact boundary `TESTING.md` documents and
`SmokeTests.cs` pins.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (1,957 lines total post-fix; `wc -l` before/after this
  loop: 2,132 → 1,957)
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:331-709` (`LoadGameEntriesAsync` — manifest parsing
  interleaved with `OnUiThreadAsync`/`CreateThumbnailAsync`, unaffected by this loop's fix, now the
  largest remaining merged-concern candidate)
- `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs` (new this loop, 195 lines — the
  previously-merged artwork-selection algorithm, now a standalone module with zero
  `PrimaryWidget`-instance-state dependency)

**Architectural test failed** — n/a — different category (ownership/coupling sprawl, not a
removable Seam or wrapper)

**Dependency category** — n/a (unaffected by this loop)

**Leverage impact** — The three moved methods now have one home (`ArtworkDownloader`) instead of
being reachable only by editing the 1,957+-line UI class; a future caller (e.g. a background
pre-fetch job) can call the selection algorithm without pulling in `PrimaryWidget`.

**Locality impact** — A maintainer tuning the official-artwork gate's floor/ceiling constants, or
debugging a wrong pick, now reads a 195-line file with a single responsibility instead of finding
the logic 1,200+ lines into a UI-event-handling class; a maintainer fixing an unrelated UI bug no
longer needs to skip past this algorithm to get to the code they came for.

**Metric signal** — `PrimaryWidget.xaml.cs`: 2,132 → 1,957 lines this loop (-175, -8.2%);
`Services/Artwork/ArtworkDownloader.cs`: 195 lines, new.

**Why this weakens submission** — Ownership of the two concerns still remaining merged in
`PrimaryWidget` (manifest-parsing, bulk-operation orchestration) is still untraceable from any
single Module besides the UI class itself; the churn-flagged file, while smaller, is still well
above the one-or-two-shallow-wrapper bar the architecture-quality 7-anchor requires.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — F1's remaining scope is now `LoadGameEntriesAsync`'s manifest-parsing
(folder iteration, `gameCache` JSON walk, platform-ID/Epic-catalog-ID extraction) versus its
UI-bound tail (`CreateThumbnailAsync` image decode, the two `OnUiThreadAsync` brackets). This is a
bigger, more entangled slice than this loop's fix: the parsing loop calls
`ArtworkFiles.HasBackupAsync` and `CreateThumbnailAsync` mid-body (UI-thread-affine), and builds the
final `GameEntry` (a UWP-bound type) inline rather than after a clean parse/hydrate split. Re-run
the Simplify Pressure Test fresh before attempting — name explicitly which lines are pure
JSON/string work (extractable into a `Services/Library` manifest reader returning plain records)
versus which need to stay because they call into UWP-only decode/storage APIs, rather than carrying
forward a blanket "needs a decision" or "mechanical move" label without re-deriving it (Builder
Notes pattern #1 below).

**Blast radius** — Change (next loop, if the fresh SPT passes): `PrimaryWidget.xaml.cs`
(`LoadGameEntriesAsync`'s manifest-parsing lines only), a new `Services/Library` manifest-parsing
helper. Avoid: `Services/Artwork/ArtworkDownloader.cs`, `Services/Artwork/ArtworkFiles.cs`,
`Services/Artwork/ArtworkRanker.cs`, `Services/Stores/*`, `Services/Library/GameImages.cs`,
`Services/Library/OperationReport.cs` (all complete this loop or prior loops).

---

### Finding #2: ArtworkRanker.RankGrids' style-priority ordering has no test mixing text-bearing and icon-like styles, so a sort-direction inversion would pass silently

**Why it matters** — `RankGrids` is the ranking algorithm behind every automatic artwork pick on the
widget's primary flow; an inverted style-priority tie-break would silently prefer icon-like art over
box-art-style uploads library-wide, with no test catching it.

**What is wrong** — `ArtworkRanker.cs:195` sorts candidates with
`.ThenBy(r => GridStylePriority(r.Grid.Style))` — `GridStylePriority` returns 0 for text-bearing
styles (`alternate`/`white_logo`/`blurred`) and 1 for icon-like ones (`ArtworkRanker.cs:74-77`), so
ascending order is load-bearing: text-bearing art must sort first. `GridStylePriority` itself is
directly unit-tested (`ArtworkRankerTests.cs:50-66`, confirms the 0/1 return values), but every
`RankGrids` test in the "Grid ordering" section (`ArtworkRankerTests.cs:161-244`) constructs its
candidates with the `Grid()` factory's default style ("alternate") on both sides of the comparison,
so `GridStylePriority` evaluates to the same tie value (0,0) in every `RankGrids` test case —
independently verified by reading the factory default (`ArtworkRankerTests.cs:23`) and every call
site in that section. Mutation check performed directly against current source: flipping `.ThenBy`
to `.ThenByDescending` at `ArtworkRanker.cs:195` changes nothing observable in any of the 10
existing `RankGrids`/`RankIcons` tests, since no test constructs two grids with different
`GridStylePriority` values and asserts their relative order.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs:195` (`.ThenBy(r =>
  GridStylePriority(r.Grid.Style))` — the load-bearing ascending sort)
- `SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs:74-77` (`GridStylePriority` — 0 for
  text-bearing, 1 for icon-like)
- `SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs:21-41` (`Grid()` factory — style defaults to
  "alternate" and every `RankGrids` test in the file uses this default on all candidates)
- `SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs:161-244` (all six `RankGrids` ordering tests — none
  varies style across candidates within one call)

**Architectural test failed** — n/a — different category (missing-test gap, per method.md Step 8's
mutation-test check, not a Seam/wrapper concern)

**Dependency category** — n/a

**Leverage impact** — One call site (`RankGrids`), but it is the ranking function every
automatic-fix and manual-picker artwork list goes through — a regression here is library-wide, not
local to one caller.

**Locality impact** — The fix is one new test case inside `ArtworkRankerTests.cs`'s existing "Grid
ordering" section; no production code changes.

**Metric signal** — none

**Why this weakens submission** — A source-level mutation (sort-direction flip) on a central,
primary-flow ranking rule passes the entire 104-test suite undetected — exactly the missing-test-
surface gap the mutation-test check in method.md Step 8 is designed to surface before scoring
`test_strategy` at its 9-anchor.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Add one `RankGrids` test case constructing two candidates with
different styles (one from `TextBearingGridStyles`, one not) and asserting the text-bearing one
sorts first — mirrors the existing `Foreign_language_artwork_sorts_behind_english_and_untagged`
test's shape exactly. No production code change; the ranking logic is already correct, only the
test surface is missing.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs` (one new test method).
Avoid: `SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs`, everything else.

---

### Finding #3: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** — Load time scales linearly with library size and network latency on the
widget's primary open path — the one flow every user hits every time.

**What is wrong** — The `gameCache` `foreach` loop in `LoadGameEntriesAsync` awaits
`sgdbClient.GetGameByPlatformIdAsync` (and the GOG/Epic/Ubisoft name fallbacks, routed through
`StoreNameLookup` and `EpicLibrary`) one game at a time; nothing overlaps the independent per-game
network calls. Re-verified this loop directly against current source: `LoadGameEntriesAsync`'s
sequencing is unaffected by this loop's fix, which relocated the artwork-selection algorithm (a
downstream, per-fix concern) out of `PrimaryWidget` entirely, not the per-game load loop itself;
line numbers shifted only because the relocated code sat earlier in the file (constants block +
`sharedHttpClient` field, both above line 331). This run's own operating constraints continue to
rule out attempting this finding: parallelising these per-game round-trips would change the
observable request count, order, and timing against third-party APIs (GOG, a community database,
Ubisoft's GitHub-hosted list), which this run has been instructed not to do blind, absent a
behavioural oracle to grade the result against — and the new test suite explicitly does not cover
network calls (`TESTING.md`: "Anything over the network... Only `NormaliseGameName` is covered"),
so the drift that added the test suite does not supply that oracle.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:331-709` (per-folder, per-entry `foreach`; awaits
  `GetGameByPlatformIdAsync` at `:569` and the store-name fallbacks at `:591,600,609,629`, with
  nothing overlapped — re-verified at current line numbers this loop after this loop's own edit
  shifted them up by removing the artwork-selection constants that sat above this method)

**Architectural test failed** — n/a — different category (D2, structural waste per
`lens-efficiency.md`, not a Seam)

**Dependency category** — `true-external`

**Leverage impact** — There is only one call site (the load loop); a future second caller of the
same pattern would inherit the same linear cost with no leverage from batching, since none exists.

**Locality impact** — The fix is local to `LoadGameEntriesAsync`'s loop body and
`StoreNameLookup`'s cache field declarations; it does not need to spread to callers.

**Metric signal** — One HTTP round-trip per game per store lookup; a 100-game library issues 100+
sequential requests with no overlap (D2, `lens-efficiency.md`).

**Why this weakens submission** — Structural waste on the widget's primary hot path. The fix is
well-understood (bounded concurrency) but is out of scope for this run by explicit instruction, not
by mechanical difficulty.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Blocked for the duration of this run by an explicit operational
constraint (must not change per-game network-call count, order, or behavior against third-party
APIs without a behavioral oracle) — not by a mechanical difficulty. If that constraint is lifted in
a future run: bound concurrency (e.g. `SemaphoreSlim(4-8)` + `Task.WhenAll`) around the per-entry
body, and switch `StoreNameLookup`'s four cache fields to `ConcurrentDictionary` before
parallelizing.

**Blast radius** — Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync`),
`Services/Stores/StoreNameLookup.cs` (the four cache fields). Avoid:
`Services/Artwork/ArtworkDownloader.cs`, `Services/Artwork/*`, `Services/SteamGridDB/*`.

## Simplification Check

- **Structurally necessary:** Relocating `DownloadArtworkAsync`, `DownloadBestTileFillingImageAsync`
  and `FindOfficialLookalikeAsync` — plus the three tuning constants and the shared `HttpClient` they
  alone used — out of `PrimaryWidget.xaml.cs` into `Services/Artwork/ArtworkDownloader.cs`. Passes
  the deletion test in reverse: these methods had zero references to `PrimaryWidget` instance state,
  so their presence inside the UI class added ownership ambiguity without buying anything a
  `PrimaryWidget`-hosted method would need.
- **New seam justified:** false — `ArtworkDownloader` is an internal static class with one production
  caller path (three call sites, same module), matching the established pattern for
  `ArtworkFiles`/`GameImages`/`OperationReport`; no interface, no DI, no two-adapter claim made.
- **Helpful simplification:** `PrimaryWidget.xaml.cs` shrank 2,132 → 1,957 lines (net -175); the
  `using Windows.Web.Http;` import and the `sharedHttpClient` field left the UI class entirely, since
  nothing else in it used HTTP directly.
- **Should NOT be done:** Attempting `LoadGameEntriesAsync`'s manifest-parsing split in the same loop
  — it is a materially bigger, more entangled slice (mid-loop UI-thread image decode, UWP-bound
  `GameEntry` construction inline) that was not vetted this loop; attempting it without a fresh
  Simplify Pressure Test risks a costume-layer split that looks clean but leaves the UI-thread
  coupling load-bearing in the "pure" half. Also should not attempt the `RankGrids` test gap (F-005)
  or F-003 (concurrency) this loop — F1 is the higher-priority (Serious > Noticeable) finding and the
  protocol selects a single highest-priority item per loop.
- **Tests after fix:** No new tests added or needed — this is a pure relocation (byte-identical
  method bodies), not a deepening of a previously-shallow module, so Replace-don't-layer's
  tests-at-the-new-Interface requirement does not apply; the same regression oracle (104 tests + green
  MSBuild) that covered zero of this logic before this loop's move still covers zero of it after,
  which is not a regression — `TESTING.md`'s documented network-call carve-out already excluded this
  logic's category from coverage before it was extracted.

## Improvement Backlog

1. **Attempt LoadGameEntriesAsync's manifest-parsing/UI-decode split (F1's next honest slice)** —
   name explicitly which lines are pure JSON/string extraction versus which call UWP-only decode or
   storage APIs, then extract only the pure half into a new `Services/Library` manifest reader
   returning plain records. Re-run the Simplify Pressure Test fresh before committing — this was not
   vetted this loop, only identified as the next candidate once the artwork-selection slice closed.
   - Why it matters: F1 remains the largest Serious deduction on the board; this is the largest
     remaining merged-concern candidate in `PrimaryWidget.xaml.cs`.
   - Score impact: Architecture quality +0.5-1.0 and Code simplicity +0.5 if verified and the split
     survives fresh SPT without introducing UI-thread coupling into the "pure" half.

2. **Add the missing RankGrids style-priority mixed-style test case (F-005)** — one new
   `ArtworkRankerTests.cs` test case, no production code change. Cheap, high-value: closes a real,
   verified mutation-test gap on the widget's primary artwork-selection flow.
   - Why it matters: `test_strategy`'s current 8.0 ceiling is partly explained by this named,
     source-backed gap; closing it removes one of the two disclosed coverage-gap categories.
   - Score impact: Test strategy +0.5-1.0 once verified.

3. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups (F-003)** — blocked
   for the duration of this run by an explicit operational constraint (must not change observable
   per-game network-call behavior against third-party APIs without a behavioral oracle). Carried
   forward as a reminder, not as an actionable item under current instructions.
   - Why it matters: removes load latency that scales linearly with library size on the widget's
     primary flow, whenever this run's constraint is lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

None this loop. `ArtworkDownloader` (new this loop) is a lateral relocation of already-deep, already-
pure logic — no shallow-module instance surfaced within it, and it is one loop old (too early to
judge whether it needs deepening). `LoadGameEntriesAsync`'s remaining concern-merge (Finding #1's
next slice) is a splitting/ownership problem, not a shallow-Interface-needs-deepening problem — it
does not fit the Deepening Candidate framework (deletion-test-fails / Interface≈Implementation) and
is correctly tracked as a Finding + backlog item instead.

## Builder Notes

1. **Pattern:** A Serious finding's own evidence can go stale between loops even when no contest
   loop touched the file in the meantime — the user's own out-of-band commits had already fixed part
   of F1's cited evidence (backup/restore orchestration) before this loop's Step 1 began.
   - How to recognize: Discovery flags drift commits since the last loop; re-reading the finding's
     cited file top-to-bottom (not trusting the prior loop's line numbers or claim text) is the only
     way to catch a partially-resolved claim before re-attempting work that is already done.
   - Smallest coding rule: on any loop where Discovery reports drift, re-derive every open finding's
     evidence from current source before selecting Priority 1 — a stale Claim can point you at a fix
     that no longer needs making, while the real remaining slice goes unnoticed.
   - Stack example: C# — this loop's actual highest-leverage find (the UI-free artwork-selection
     algorithm) was not the thing loop 6's F1 text described at all; it only surfaced from reading
     `RestoreBackupCoreAsync`'s neighbors, not from trusting the prior loop's evidence block.

2. **Pattern:** A method with zero references to its enclosing class's instance state — no `this.`,
   no UI-framework field access — sitting inside a large god-class is a clean, low-risk extraction
   candidate regardless of whether it is architecturally "deep": the absence of coupling is itself
   the friction proof, and it is cheaper to verify (grep for field names) than a full deletion test.
   - How to recognize: a private method inside a large class whose body never references `this`,
     the class's own fields, or framework-bound UI types, even though sibling methods in the same
     class reference them constantly.
   - Smallest coding rule: before proposing a bigger, riskier extraction, grep every candidate
     method's body against the enclosing class's field names — a zero-hit method is safe to move
     verbatim; a method with even one UI-field reference needs the harder parse/hydrate split
     treatment instead.
   - Stack example: C# — `DownloadArtworkAsync`/`DownloadBestTileFillingImageAsync`/
     `FindOfficialLookalikeAsync` referenced only static helpers (`FixLog`, `ArtworkSignature`,
     `TileImage`, `ArtworkRanker`) and their own parameters; zero PrimaryWidget field references,
     confirmed by grep before the move, not after.

3. **Pattern:** Mutation-testing your own test suite (not just running it) surfaces gaps that
   passing-test-count metrics hide completely — a function can be both directly unit-tested (in
   isolation) and silently untested inside a composite caller, when every composite test happens to
   hold the isolated function's input constant.
   - How to recognize: a helper function is unit-tested with varied inputs, but every test of the
     *caller* that uses the helper as one tie-break among several constructs its test fixtures with
     the same value for that one input across every case.
   - Smallest coding rule: for any multi-key sort/rank function, check that at least one test in the
     composite-behavior suite varies each individual key while holding the others constant — not just
     that the key's own extraction function is separately unit-tested.
   - Stack example: C# — `ArtworkRanker.GridStylePriority` is directly unit-tested with both 0- and
     1-returning inputs, but every `RankGrids` composite test used the same default style on all
     candidates, so the ascending-vs-descending direction of that one `.ThenBy` clause was never
     exercised by anything.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. `test_strategy` landing at 8.0 rather than 9.0 — the anti-anchor rule's "at most one named
   shell-seam gap" reading is my own interpretation; a reviewer who treats `TESTING.md`'s single
   "What is not covered" section (which lists the UI-shell and network gaps together, as one
   disclosure) as satisfying "one documented residual" rather than two could reasonably score this
   9.0.
2. `architecture_quality`'s +1.0 magnitude (5.5 → 6.5) for one loop's extraction, when F1's core
   claim is still `carried_forward` — a stricter reviewer could argue a partial-slice fix on an
   already-`carried_forward` Serious finding deserves +0.5, not +1.0, reserving the larger jump for
   when `LoadGameEntriesAsync`'s split closes the remaining merged concerns.
3. Whether `LoadGameEntriesAsync`'s manifest-parsing genuinely splits as cleanly as I have described
   it (pure JSON walk vs. UI-bound decode tail) — I named the split direction from reading the method
   once, not from actually attempting it; `CreateThumbnailAsync`'s exact placement mid-loop (rather
   than after the parse completes) may be load-bearing in a way I have not traced (e.g. whether
   `hasBackup`'s file check needs to happen before or after image decode for any observable reason).

## Final Judge Narrative

Place, not win. This loop's real news is upstream of anything a contest loop did: the user closed
the loop-1-6 record's single biggest blocker themselves, writing a 104-test suite with real,
mutation-verified assertions and moving two of `PrimaryWidget.xaml.cs`'s four original merged
concerns (backup/restore, bulk-operation orchestration) out on their own, before this loop's Step 1
even started. This loop's job was honest re-derivation, not confirmation — re-reading
`PrimaryWidget.xaml.cs` fresh surfaced a third merged concern loop 6 never named (the artwork-
selection algorithm, provably UI-free) and closed it the same way the user's own commits closed the
other two: a plain relocation, no new Seam, verified by a green build both before and after. F1
stays `carried_forward` — `LoadGameEntriesAsync` and the bulk-operation loops are real, remaining
merged concerns — but the finding's own evidence is now current, not stale, and next loop has a
concretely named, if bigger, next slice. `test_strategy` moved the most (3.0 → 8.0) on real evidence:
an Authority-Map cross-check and a hands-on mutation-test check both performed against current
source, the latter honestly surfacing a real gap (F-005) rather than rubber-stamping the test count.
Concurrency's F-003 residual remains explicitly out of scope for this entire run by operational
instruction; `AsyncLazyCache<T>`'s lock discipline is now empirically stress-tested, which is
different progress on a different question (regression-proofing what loop 6 already fixed, not
touching what F-003 has not). Runtime ownership is trustworthy for what has been resolved and now
partly test-verified rather than purely inspected; concurrency is not yet fully trustworthy on the
still-open F-003 path. Future work has one honestly-scoped, not-yet-attempted path for F1 (the
manifest-parsing split) and one cheap, concrete fix for the newly-found test gap (F-005) — both named
plainly rather than left implicit.

## Loop 7 Result

Moved `DownloadArtworkAsync`, `DownloadBestTileFillingImageAsync` and `FindOfficialLookalikeAsync` —
plus the `maxArtworkCandidates`/`officialArtworkFloor`/`officialArtworkCeiling` constants and the
`sharedHttpClient` field they alone used — verbatim from `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`
into a new `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs` (195 lines), closing this loop's
re-derived slice of F1 (stable_id F-001). All three moved methods were grep-confirmed to reference
zero `PrimaryWidget` instance state before the move. Three call sites in `PrimaryWidget` were
re-pointed to the new `ArtworkDownloader.*` names (`FixLibraryAsync`'s
`DownloadBestTileFillingImageAsync` call, `DownloadAndReplaceImageCoreAsync`'s `DownloadArtworkAsync`
call, `TryFixFromPortraitArtAsync`'s `DownloadArtworkAsync` call and its `.Take(maxArtworkCandidates)`
became `.Take(ArtworkDownloader.MaxCandidates)`). The now-unused `using Windows.Web.Http;` import was
removed from `PrimaryWidget.xaml.cs`. Added `<Compile Include="Services\Artwork\ArtworkDownloader.cs" />`
to `SteamGridDB.Xbox.csproj` (required for this UWP project with no globbing, per `TESTING.md`'s
documented gotcha). `git diff --stat`: `PrimaryWidget.xaml.cs` (5 insertions, 179 deletions),
`SteamGridDB.Xbox.csproj` (1 insertion), plus the new 195-line `ArtworkDownloader.cs`.

**What proves the change is honest:** Both regression oracles passed clean before and after the
change — `run-tests.ps1` (104 passed, 0 failed, both runs) and `MSBuild`
(`SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never`, exit 0 both
times, same command as every prior loop). The move is byte-identical logic relocation, traced
method-by-method: `DownloadArtworkAsync`'s try/catch, `DownloadBestTileFillingImageAsync`'s
candidate-scan loop and fallback tracking, and `FindOfficialLookalikeAsync`'s floor/ceiling gate and
replacement scan are unchanged character-for-character except for the internal call qualification
(`DownloadArtworkAsync` → itself, unqualified, since both callers now live in the same class) and the
constants' visibility (`private const` → `internal const`, needed because `PrimaryWidget`'s
`TryFixFromPortraitArtAsync` still reads `MaxCandidates` from outside the new class). Grep-verified
post-edit that no stray reference to the removed methods, constants, or `sharedHttpClient` field
remains anywhere in `PrimaryWidget.xaml.cs`. The test project links `Services/**/*.cs` via a glob
(`SteamGridDB.Xbox.Tests.csproj`), so `ArtworkDownloader.cs` was automatically picked up and
compiled into the test assembly on this loop's `run-tests.ps1` run — its continued 104/104 pass is
independent confirmation the new file compiles cleanly in the desktop-projected context too, not
only in the UWP app build. This changes only where the artwork-selection algorithm's code lives, not
the number of network calls, the ranking order, the tile-fill check, the official-artwork gate's
floor/ceiling values, or any other selection/download behavior — confirmed by the independent
implementation-reviewer pass below.

**Risk boundary evidence (Meta-Rule 4):** this fix crosses a `cross_file_visibility` boundary — the
three moved methods and the `MaxCandidates` constant went from `private` (accessible only within
`PrimaryWidget`) to `internal` (assembly-visible), because `PrimaryWidget` still calls
`ArtworkDownloader.DownloadArtworkAsync`/`MaxCandidates` from `TryFixFromPortraitArtAsync` and
`DownloadAndReplaceImageCoreAsync`, which live in a different file now.
`{"boundary_kind": "cross_file_visibility", "verification": "compile_matrix", "detail": "Both
regression-oracle configurations that this codebase has were compiled clean after the visibility
change: the UWP AppContainerExe build (MSBuild, exit 0) which references PrimaryWidget.xaml.cs and
the new ArtworkDownloader.cs together in the same compilation, and the desktop net8.0-windows test
build (run-tests.ps1, 104/104 passed) which links ArtworkDownloader.cs directly via the test project's
Services/**/*.cs glob per SteamGridDB.Xbox.Tests.csproj. Widening private to internal in a single
assembly with no InternalsVisibleTo boundary carries no cross-assembly exposure risk in this
codebase's shape (confirmed by reading both .csproj files directly this loop, not assumed from
TESTING.md's prose) - the only thing that could break from this specific change is a call site failing
to resolve, which either compile config would catch immediately as a hard compile error, and both
did not.", "mechanically_testable": true}`

**Targeted finding status:** `carried_forward` — F-001's underlying Claim (`PrimaryWidget.xaml.cs`
merges multiple structurally distinct concerns behind zero Interface boundary) is not fully
resolved: `LoadGameEntriesAsync`'s manifest-parsing and the three bulk-operation loops remain merged
in the file. This loop closed the specific artwork-selection-algorithm slice of that Claim, the same
way loops 3-5 closed successive slices of F1's original four-concerns claim without marking it
`resolved` until the core claim itself is gone.

**Unintended scorecard regression:** none observed. `architecture_quality`, `state_management`,
`concurrency`, `simplicity`, `test_strategy`, `credibility` all moved UP on distinct, non-overlapping
structural proof; `domain_modeling`, `data_flow`, `framework_idioms` held SAME (no structural change
in their evidence this drift window); no dimension regressed.

## Loop 7 Implementation Review

`verdict: approved` — "The three artwork-selection methods, three constants, and `sharedHttpClient`
field are verifiably gone from `PrimaryWidget.xaml.cs` and relocated byte-identical (verified
against `git show HEAD`) into `ArtworkDownloader.cs` with all three call sites re-pointed and no
dangling references anywhere in the repo." All three checks (`reality`, `honesty`, `regression`)
`passed`; `conditions: []`; `regressions: []`.
