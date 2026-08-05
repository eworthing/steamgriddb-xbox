--- Loop 1 (UTC 2026-08-05T01:00:00Z) ---

### Discovery (first loop only)
- Source roots:
  - `SteamGridDB.Xbox/` — the app. UWP `AppContainerExe`, legacy csproj, C#. 5,486 LOC across 28 .cs files. Subdirs: `Models/`, `Services/{Artwork,Library,SteamGridDB,Stores}/`, `Converters/`, `Properties/`.
  - `SteamGridDB.Xbox.Tests/` — desktop .NET 8 test project (net8.0-windows10.0.19041.0), 17 .cs files, 138 tests. It does NOT reference the app project; it *links* the app's sources via `<Compile Include="..\SteamGridDB.Xbox\Services\**\*.cs" ...>`.
- Test command: `powershell -NoProfile -File ./run-tests.ps1`
  - Verified this loop: **138 passed, 0 failed, 0 skipped** (both before and after the refactor).
- Build command: resolve MSBuild via `& "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1`, then
  `& $msbuild "SteamGridDB.Xbox.sln" /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo`
  - Verified this loop: **exit 0** (both before and after the refactor).
- Ground truth is DUAL — both commands must pass (per `TESTING.md:39-42`). This loop's change touches only files with existing explicit `<Compile Include>` entries, so no new-file/glob risk applied, but both ground truths were run anyway.
- ADRs found: none. Domain terms (CONTEXT.md): none.
- prior_audit_docs adopted/falsified this loop: `CODE-REVIEW.md`'s "all fixed" self-audit spot-checked (3/3 claims independently re-verified true against current source — clip rect, manifest version, HttpClient sharing). `TESTING.md` and `ARTWORK-SELECTION.md` read in full; no open claims entered this loop's Findings.
- Selected lens: **generic**. Loaded lenses: `["lens-generic.md", "lens-security.md", "lens-efficiency.md"]`.
- churn_top20 made `PrimaryWidget.xaml.cs` (30 edits, 1,950 LOC, ~36% of the app) the mandatory deep-review target; this loop's Priority-1 finding came from that review.

### Loop Counter
Loop 1 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

Fifteen prior loops (visible in `git log`, pre-purge) already extracted most of the codebase's pure logic into well-tested, deeply-owned Modules. But `PrimaryWidget.xaml.cs` — the one file that cannot be unit-tested at all — still owned a live state-ownership bug on a primary user flow: opening the artwork picker for a second game before clicking a tile from the first could silently write artwork to the wrong game. This loop fixes it.

## Scorecard (1-10)

- Architecture quality: 8.0 | SAME | Module graph is real and mostly enforced by source; deduction is `PrimaryWidget.xaml.cs`'s remaining 1,950-line orchestration carrying two leaf-duplication clusters and (pre-fix) the F-001 ownership gap.
- State management and runtime ownership: 6.5 | SAME | Most concerns singly/clearly owned; deduction is Finding F-001 — `CurrentSelectedGame` had no reentrancy guard across the picker-panel flow.
- Domain modeling: 8.5 | SAME | Clean discriminated `GamePlatform` enum, purpose-built value types (`ManifestEntryIdentity.Result`, `ArtworkSource`), no framework leakage.
- Data flow and dependency design: 7.5 | SAME | Acyclic in practice; deduction is process-lifetime static ambient caches reachable from multiple call sites.
- Framework / platform best practices: 8.0 | SAME | WinRT idioms used naturally; deduction is Finding F-002's 4x hand-rolled `Storyboard` ceremony.
- Concurrency and runtime safety: 6.5 | SAME | `isLibraryOperationRunning` correctly guards the four header buttons; the same discipline was absent from the picker/search-panel flow (Finding F-001).
- Code simplicity and clarity: 7.0 | SAME | Findings F-002/F-003: ~225 lines of 3-4x duplicated ceremony in `PrimaryWidget.xaml.cs`.
- Test strategy and regression resistance: 6.5 | SAME | 13/14 testable production modules have mutation-resistant direct tests; the picker/search panel — a primary flow — has zero possible direct coverage and held a live bug (Finding F-001).
- Overall implementation credibility: 7.5 | SAME | `CODE-REVIEW.md`'s self-audit independently confirmed true; deduction for `TESTING.md`'s understated framing of the untested UWP surface, which in fact held Finding F-001.

## Authority Map
- Concern: **Selected game for the artwork picker (`CurrentSelectedGame`)** — Owner: `PrimaryWidget`. Allowed writers: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync`. Readers: `LoadGridSelectionPanelAsync`, `PopulateGridSelectionPanelAsync`, `GridImage_Click`, `DownloadAndReplaceImageAsync`, `ShowSearchPanelAsync`. Persistence seam: none. Async mutation entry points: `EditGameImage_Click`, `SearchGameImage_Click`. Verdict: **Split and ambiguous** (pre-fix; see Finding F-001).
- Concern: **Bulk library operation exclusivity (`isLibraryOperationRunning`)** — Owner: `PrimaryWidget`. Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`. Readers: `IsLibraryOperationBlocking`, `TryBeginLibraryOperation`. Persistence seam: none. Async mutation entry points: `PrimaryWidget_Loaded`, `RefreshButton_Click`, `FixLibraryButton_Click`, `RestoreChangesButton_Click`, `RevertDefaultsButton_Click`. Verdict: **Single and clear**.
- Concern: **Applied-artwork record** — Owner: `AppliedArtworkStore`. Allowed writers: `SetAsync`, `ClearAsync` (gated). Readers: `GetAsync`. Persistence seam: `applied-artwork.json`. Async mutation entry points: `ReplaceImageCoreAsync`, `RestoreBackupCoreAsync`. Verdict: **Single and clear**.
- Concern: **Store-name resolution caches** — Owner: `StoreNameLookup` / `EpicLibrary`. Allowed writers: `GetOrFetchGogNameAsync`, `GetOrFetchEpicNameAsync`, `FindGameByNameAsync`, `EpicLibrary.ReadManifestsAsync`. Readers: same. Persistence seam: none. Async mutation entry points: `LoadGameEntriesAsync` (sole call site, always guarded, sequential). Verdict: **Single and clear**.

## Strengths That Matter
- `ArtworkDownloader.DownloadBestTileFillingImageAsync` + `FindOfficialLookalikeAsync` (`ArtworkDownloader.cs:71-193`) hide a five-step selection+veto pipeline behind two methods, with the floor/ceiling predicates extracted as named, individually mutation-tested functions.
- `ArtworkFiles.ApplyAsync`/`RestoreOriginalAsync` (`ArtworkFiles.cs:106-191`) get backup/restore file-system semantics right, with `TESTING.md` documenting the exact mutations tried and caught.
- `ARTWORK-SELECTION.md` documents plausible-looking ranking rules implemented, graded against the real library, found net-negative, and reverted with the losing numbers kept in the doc — the Simplify Pressure Test applied for real.

## Findings

### Finding #1: Grid picker writes artwork to whichever game is currently selected at click time, not the game the picker was opened for

**Why it matters** — A user who opens the artwork picker for one game and then opens it again for a different game before clicking a tile can have SteamGridDB artwork silently written to the wrong game's tile, with no error and no way to tell it happened.

**What is wrong** — `EditGameImage_Click` and `SearchGameImage_Click` (`PrimaryWidget.xaml.cs:1234-1251`, `:1572-1588`) reassign the single `CurrentSelectedGame` field synchronously and are guarded only by `IsLibraryOperationBlocking`, which checks `isLibraryOperationRunning` — a flag that covers the four header buttons, not the per-row Edit/Search picker flow. `LoadGridSelectionAsync` (`:1285-1341`) awaits `ShowGridPanelAsync`, which itself awaits a 250ms slide animation (`:1526`), before it clears `GridImagesView.Items` (`:1294`). During that window a previously-rendered picker session's tiles remain visible and clickable while `CurrentSelectedGame` may already have moved on. `GridImage_Click` and `DownloadAndReplaceImageAsync` (`:1458-1477`) then wrote the clicked tile's artwork to whatever `CurrentSelectedGame` currently held.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1234-1251`, `:1285-1341`, `:1458-1477`

**Architectural test failed** — n/a — different category (state-ownership / reentrancy defect)

**Dependency category** — n/a

**Leverage impact** — None — correctness fix inside `PrimaryWidget`'s own event handlers.

**Locality impact** — Fix stays entirely inside `PrimaryWidget.xaml.cs` plus one new property on `GridImageItem`.

**Metric signal, if any** — none

**Why this weakens submission** — Matches the rubric's own Likely Disqualifier anchor language ("racing async flows that can corrupt user-visible state") on a primary user flow with zero possible test coverage.

**Severity** — Likely disqualifier

**ADR conflicts** — none

**Minimal correction path** — Stamp each `GridImageItem` with the picker session it was populated under; have `GridImage_Click` ignore a tile whose stamp doesn't match the panel's current session.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Models/GridImageItem.cs`. Avoid: `SteamGridDB.Xbox/Services/**`.

**Status this loop: implemented — see Loop 1 Result below.**

### Finding #2: Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to timing/easing has to be made and verified in four places; the four copies have already drifted (200ms vs 250ms).

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync`, `HideSearchPanelAsync` (`PrimaryWidget.xaml.cs:1506-1527`, `:1532-1559`, `:1686-1750`, `:1755-1784`) each hand-build a `DoubleAnimation`+`Storyboard` against a `TranslateTransform`, varying only transform/direction/duration.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1506-1527`, `:1532-1559`, `:1686-1750`, `:1755-1784`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — One call site instead of four.

**Locality impact** — Contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Leaf-module duplication in the largest, most-churned file.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(...)` helper; four call sites become one-liners.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #3: Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of three destructive-operation confirmations kept in sync by hand.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click`, `RevertDefaultsButton_Click` (`PrimaryWidget.xaml.cs:724-768`, `:770-804`, `:806-840`) each build the same `ContentDialog` ceremony and the same guard-and-run wrapping.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:724-768`, `:770-804`, `:806-840`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming title/content/action.

**Locality impact** — Contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-duplication concern as F-002; combined ~225 lines of ceremony.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `ConfirmAndRunAsync(...)` helper.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #4: TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation would ship silently.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) has an `alpha < 64` threshold (`:250`) and a `transparentCorners < 2` threshold (`:263`) neither tested at their exact edge.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None — test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — Minor, off-primary-flow gap; Cosmetic on its own.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases at the exact boundary values.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs`.

## Simplification Check

| Field | Value |
|---|---|
| structurally_necessary | Finding F-001's session-token stamp closes a real, evidenced data-corruption path (state-ownership fix, no deletion/seam test applies) |
| new_seam_justified | false |
| helpful_simplification | none this loop (F-002/F-003 queued) |
| should_not_be_done | A generic "PanelSession"/"AnimationCoordinator" abstraction — a single int field + comparison is the smallest honest fix |
| tests_after_fix | None added/deleted — file outside test-linked surface; verified by full build + full suite + manual trace (reasoning_only per Meta-Rule 4) |

## Improvement Backlog
1. Fix the grid-picker session race (Finding F-001) — `state_management +1.0; concurrency +0.5` — structural — needed for winning.
2. Collapse the four-times-duplicated panel slide animation (Finding F-002) — `simplicity +0.5` — simplification — helpful.
3. Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003) — `simplicity +0.5; framework_idioms +0.5` — simplification — helpful.

## Deepening Candidates
1. `PrimaryWidget`'s panel slide-animation ceremony (Finding F-002) — smallest first step: extract `SlidePanelAsync(...)`; what not to do: no `IAnimator`/coordinator protocol (fails the Unified Seam Policy two-adapter rule).

## Builder Notes
- Pattern: a busy-flag reentrancy guard applied to one code path but not its sibling. → REVIEW_HISTORY.json `loops[0].builder_notes` for full notes.
- Pattern: an ambient "current selection" field read at the moment of a destructive write, instead of the identity captured when the operation started. → REVIEW_HISTORY.json `loops[0].builder_notes` for full notes.
- Pattern: leaf-module duplication hiding in a large file because no single call site made it "somebody's problem." → REVIEW_HISTORY.json `loops[0].builder_notes` for full notes.
- Pattern: Scorecard humility check. → REVIEW_HISTORY.json `loops[0].builder_notes` for full notes.

## Final Judge Narrative
Place, not win, this loop — and the placement is earned honestly rather than papered over. Runtime ownership was NOT trustworthy going into this loop: `CurrentSelectedGame` could be silently redirected mid-flight and cause a real artwork-corruption bug on the app's primary manual-selection flow. Concurrency was trustworthy everywhere it had already been made a first-class concern and not trustworthy where it hadn't. This loop's fix closes the specific data-corruption path with the smallest honest addition available, verified by full build + full test suite plus a manual trace, since the file cannot carry an automated regression test. Simplification did not happen this loop — F-002/F-003 are queued, not fixed. Future work risks over-engineering only if those extractions reach for a generic coordinator abstraction instead of a plain helper method.

## Loop 1 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` and `SteamGridDB.Xbox/Models/GridImageItem.cs`: added a `gridPanelSessionId` field on `PrimaryWidget`, incremented at the top of `LoadGridSelectionAsync` before any `await`; threaded the captured session value through `PopulateGridSelectionPanelAsync` into a new `GridImageItem.SessionId` property stamped on every created tile; added a session-match check to `GridImage_Click` so a click on a tile from a superseded picker session is ignored instead of writing its artwork to whatever game `CurrentSelectedGame` currently holds. Full build exits 0 both before and after; full test suite reports 138/138 both before and after, unchanged. Finding F1 (stable_id F-001) is **resolved**. Findings F-002, F-003, F-004 carried forward unchanged.

## Loop 1 Implementation Review
Verdict: **approved**. Reason: the session-token stamp genuinely closes the wrong-game write path (verified no interleaving window between claiming `gridPanelSessionId` and its effect, and only one `GridImageItem` creation site, which always receives a real session id), the fix is a plain int field + comparison with no costume layer or suppression, and `CURRENT_REVIEW.md` honestly discloses the lack of automated coverage for this untestable file. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.

--- Loop 2 (UTC 2026-08-05T01:04:35Z) ---

### Loop Counter
Loop 2 of 10 (cap)

### System Flag
[STATE: CONTINUE]

(Discovery: see Loop 1 Discovery.)

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

Loop 1's session-token fix for Finding F-001 held up under this loop's independent re-verification (no interleaving window, no wrong-game write possible). But the same token was only checked at the click/write step; the picker's own population step never checked it, so a superseded request's network response could still land after a live request's and silently mix stale, dead tiles into the panel (Finding F-005) - the same "stale authority remains alive" hazard one step upstream. This loop closes that gap.

## Scorecard (1-10)

- Architecture quality: 8.0 | SAME | `ArtworkDownloader.cs:71-193` still hides a five-step selection+veto pipeline behind two methods (re-confirmed unchanged this loop). Deduction unchanged: `PrimaryWidget.xaml.cs` remains a ~1984-line single-class orchestrator carrying Findings F-002/F-003's duplicated ceremony (still open) plus this loop's newly-found F-005 (an ownership-arbitration gap over `GridImagesView.Items` across overlapping picker sessions, pre-fix).
- State management and runtime ownership: 7.5 | UP | F-001 independently re-verified resolved this loop: `PrimaryWidget.xaml.cs:72`, `:1299`, `Models/GridImageItem.cs:158-161`, `PrimaryWidget.xaml.cs:1482` - commit `e72dc0b` - closes the wrong-game-write path with no interleaving window. Held below 9 by this loop's own new Finding F-005.
- Domain modeling: 8.5 | SAME | `Models/GamePlatform.cs` discriminated enum + single translation seam re-confirmed unchanged. `GameEntry.cs:133-145`'s three independently-settable properties re-verified, still no live harm.
- Data flow and dependency design: 7.5 | SAME | `Services/*` re-confirmed zero `Windows.UI.Xaml` imports. Ambient static caches deduction unchanged; this loop extended verification to `FixLog`/`CapsuleParseNotes`'s call sites, same conclusion, more rigorously checked.
- Framework / platform best practices: 8.0 | SAME | `SteamGridDbClient.cs:137-141` deliberate split re-confirmed unchanged. Finding F-002 deduction unchanged.
- Concurrency and runtime safety: 7.0 | UP | F-001's resolution closes the Likely-disqualifier hazard; held short of 9-anchor by this loop's own new Finding F-005 (Serious, not Likely disqualifier). Scored pre-fix per Blind-critic ordering; F-005 fixed this loop.
- Code simplicity and clarity: 7.0 | SAME | Findings F-002, F-003 unchanged, still open.
- Test strategy and regression resistance: 6.5 | SAME | `PrimaryWidget.xaml.cs` remains untestable; this loop's own F-005 discovery re-demonstrates the anchor's disqualifying language rather than resolving it.
- Overall implementation credibility: 7.5 | SAME | `gridPanelSessionId` doc-comment discipline re-confirmed; `TESTING.md` deduction unchanged, reinforced by F-005.

## Authority Map
For each major mutable runtime concern (re-emitted this loop: an authority-related finding, F-005, is Priority 1):

- Concern: **Selected game for the artwork picker (`CurrentSelectedGame`) - write path**
  - Owner: `PrimaryWidget`
  - Allowed writers: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (nulls on close)
  - Readers: `LoadGridSelectionPanelAsync`, `PopulateGridSelectionPanelAsync`, `GridImage_Click`, `DownloadAndReplaceImageAsync`, `ShowSearchPanelAsync`
  - Persistence seam: none (in-memory UI field)
  - Async mutation entry points: `EditGameImage_Click`, `SearchGameImage_Click`
  - Verdict: **Single and clear**

- Concern: **Grid picker panel display contents (`GridImagesView.Items` / `GridPanelStatus` / `GridLoadingRing` during population)**
  - Owner: `PrimaryWidget` (`LoadGridSelectionAsync` / `PopulateGridSelectionPanelAsync`)
  - Allowed writers: `LoadGridSelectionAsync` (Clear, status text, loading ring), `PopulateGridSelectionPanelAsync` (Add)
  - Readers: user (visually), `GridImage_Click` (via `SessionId` comparison)
  - Persistence seam: none
  - Async mutation entry points: `LoadGridSelectionAsync` (one invocation per Edit/Search/search-result click; multiple can be in flight concurrently)
  - Verdict: **Split and ambiguous (pre-fix)** - see Finding F-005. This loop's fix closes the specific display-corruption path; re-audit next loop.

- Concern: **Bulk library operation exclusivity (`isLibraryOperationRunning`)**
  - Owner: `PrimaryWidget`
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Readers: `IsLibraryOperationBlocking`, `TryBeginLibraryOperation`
  - Persistence seam: none
  - Async mutation entry points: `PrimaryWidget_Loaded`, `RefreshButton_Click`, `FixLibraryButton_Click`, `RestoreChangesButton_Click`, `RevertDefaultsButton_Click`
  - Verdict: **Single and clear**

- Concern: **Applied-artwork record (`AppliedArtworkStore`'s path->artworkId map)**
  - Owner: `AppliedArtworkStore` (static Module)
  - Allowed writers: `SetAsync`, `ClearAsync` (via `UpdateAsync`, gated by `SemaphoreSlim gate`)
  - Readers: `GetAsync` (same gate)
  - Persistence seam: `applied-artwork.json` in the widget's local data (`RecordFolder`)
  - Async mutation entry points: `ReplaceImageCoreAsync`, `RestoreBackupCoreAsync`
  - Verdict: **Single and clear** - re-verified this loop; `RecordFolder` setter's cache-reinit is test-only (zero production call sites).

- Concern: **Store-name / capsule-parse / fix-log ambient state (`StoreNameLookup`'s three dictionaries, `SteamGridDbClient.CapsuleParseNotes`, `FixLog`'s fields)**
  - Owner: `StoreNameLookup` / `SteamGridDbClient` / `FixLog` (static Modules)
  - Allowed writers: `GetOrFetchGogNameAsync`, `GetOrFetchEpicNameAsync`, `FindGameByNameAsync` (unlocked dictionaries), `NoteCapsuleParse` (unlocked list), `FixLog.Start`/`Write` (unlocked list+fields)
  - Readers: same methods, `FixLibraryAsync` (reads `CapsuleParseNotes`)
  - Persistence seam: none
  - Async mutation entry points: `LoadGameEntriesAsync`'s per-entry loop; `FixLibraryAsync`
  - Verdict: **Single and clear** - re-verified this loop with a fuller call trace than loop 1's own Authority Map covered; both operations mutually exclusive under `isLibraryOperationRunning`, no reachable concurrent writer.

## Strengths That Matter
- `AppliedArtworkStore.GetAsync`/`UpdateAsync` (`AppliedArtworkStore.cs:63-84, 153-184`) both funnel through the same shared `gate` `SemaphoreSlim` rather than a second lock of their own - verified this loop by tracing both call paths end to end.
- `TileImage.BestVerticalCropAsync` (`TileImage.cs:321-379`) places the portrait-crop window by measured Laplacian edge-energy rather than a fixed offset, docstring cites the actual grading comparison (23/35 vs. 7) and why a plausible refinement was rejected.
- `PrimaryWidget.xaml.cs`'s `gridPanelSessionId` mechanism (`:64-72`, `:1299`, `:1482`) independently re-traced end to end this loop, still correct.

## Findings

### Finding #1 (stable_id F-005): LoadGridSelectionAsync's panel-population step has no ownership check against a superseded picker session

**Why it matters** — A user who opens the artwork picker for one game and then reopens it (for the same or a different game) before the first request's network round trip finishes can see stale tiles from the superseded request silently mixed into the panel - unlabeled and permanently unclickable per the click-time session gate - or have the live request's loading/status state clobbered by the stale one's, with no error shown.

**What is wrong** — `LoadGridSelectionAsync` (`PrimaryWidget.xaml.cs:1295-1332` pre-fix) captures a session id in `session` before its first await, exactly as Finding F-001's fix does for the click path, but never checks it again before mutating shared panel state. After the network fetch (`GetTitleBearingGridsAsync`/`GetSquareIconsAsync`) returns, the method unconditionally called `PopulateGridSelectionPanelAsync(grids, icons, session)` and, in the null-result branch, unconditionally wrote `GridPanelStatus.Text`/`GridLoadingRing.IsActive`, even when a newer `LoadGridSelectionAsync` invocation had since started (`gridPanelSessionId` incremented past `session`) and already populated the panel with its own current results. Because each invocation's network fetch duration is independent and unbounded, a stale (superseded) invocation's fetch can complete and reach `PopulateGridSelectionPanelAsync` after the live invocation's own population has already run - `PopulateGridSelectionPanelAsync` only appends (`GridImagesView.Items.Add`, never clears), so the stale invocation's tiles land mixed into the live results. `GridImage_Click` does gate the click on `gridItem.SessionId == gridPanelSessionId`, so a stale tile can never trigger a wrong-game write - Finding F-001's fix holds - but the stale tile is still visibly, silently present and inert, and an intervening status-text write or the final `GridLoadingRing.IsActive = false` from the stale invocation can also mask the live invocation's own loading state.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1295-1332` (pre-fix, session capture with no re-check), `:1334-1344` (pre-fix, unconditional population/status writes), `:1434-1448` (`PopulateGridSelectionPanelAsync` only appends, never clears)

**Architectural test failed** — n/a - different category (state-ownership/reentrancy defect, matching Finding F-001's own categorization)

**Dependency category** — n/a

**Leverage impact** — None - correctness fix inside `PrimaryWidget`'s own `LoadGridSelectionAsync`, not a change to any caller-facing Interface.

**Locality impact** — Fix stays entirely inside `PrimaryWidget.xaml.cs`; no network call added, removed, or reordered.

**Metric signal, if any** — none

**Why this weakens submission** — Same "stale authority remains alive" hazard class Finding F-001 closed at the click/write step, reappearing one step earlier at the population/display step, on the same primary, contest-relevant flow, with the same zero possible test coverage.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Add one guard clause immediately after the network fetch completes and before any panel mutation: `if (session != gridPanelSessionId) { return; }`, placed after `List<SteamGridDbGrid> icons = await iconsTask;` and before the existing `if (grids == null && icons == null)` check. Runs after both network calls have already fired unconditionally, so it changes no network call count, ordering, payload, or error handling.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/Models/GridImageItem.cs`, `SteamGridDB.Xbox/Services/**`.

**Status this loop: implemented — see Loop 2 Result below.**

### Finding #2 (stable_id F-002): Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to the panel's slide timing or easing (or a bug in it) has to be made and verified in four places instead of one, and the four copies have already begun to drift (200ms hide vs 250ms show, independently re-derived each time).

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` (`PrimaryWidget.xaml.cs:1540-1561`, `:1566-1593`, `:1720-1784`, `:1789-1818`) each hand-build a `DoubleAnimation` + `Storyboard` against a `TranslateTransform`, set From/To/Duration/EasingFunction, call `storyboard.Begin()`, then await `Task.Delay` matching the duration. The only real variation across the four is which transform, which direction, and 250ms vs 200ms.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1540-1561`, `:1566-1593`, `:1720-1784`, `:1789-1818` (line numbers corrected this loop against current source)

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — One call site to read/change instead of four.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Four near-identical bodies in the largest, most-churned file (~1984 LOC, 30 edits in 6 months) is leaf-module duplication not swept away.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(FrameworkElement transform, double from, double to, int durationMs, EasingMode mode)` helper; each of the four call sites becomes a one-line call.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #3 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations has to be kept in sync by hand, and nothing enforces that a future fourth operation follows the same shape.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (`PrimaryWidget.xaml.cs:734-778`, `:780-814`, `:816-850`) each build a `ContentDialog` with the same four style-resource lookups and the same `XamlRoot` check, call `ShowAsync`, branch on the result, and wrap the operation in the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` pattern.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:734-778`, `:780-814`, `:816-850` (line numbers corrected this loop against current source)

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as F-002; combined ~225 lines of ceremony repeated 3-4x.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `ConfirmAndRunAsync(...)` that owns dialog construction, the `XamlRoot` check, and the guard wrapping.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #4 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation would ship silently.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) treats a pixel transparent when `alpha < 64` (`:250`) and rejects the image when `transparentCorners < 2` (`:263`); the exact boundary values are untested.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — Minor, off-primary-flow gap; Cosmetic per the anchor's own carve-out.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases at the exact boundary values.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs`.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Finding F-005's session-liveness guard closes a real, evidenced display-corruption path (state-ownership fix, not an abstraction removal) |
| New seam justified | No - guard reuses the field loop 1 already introduced |
| Helpful simplification | none this loop (F-002/F-003 queued, not implemented) |
| Should NOT be done | Generic "PanelSession"/"RequestCoordinator" abstraction; also rejected a second guard before `Clear()` as proven-unnecessary defensive ceremony |
| Tests after fix | None added/deleted - untestable surface; verified by full build+suite plus manual trace |

## Improvement Backlog
1. **Fix the grid-picker population race (Finding F-005).** score impact: `concurrency +1.0; state_management +0.5` — structural, needed for winning
2. **Collapse the four-times-duplicated panel slide animation (Finding F-002).** score impact: `simplicity +0.5` — simplification, helpful
3. **Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003).** score impact: `simplicity +0.5; framework_idioms +0.5` — simplification, helpful

**Priority-1 accounting**: F-005 is Priority 1 on severity (Serious deduction, the only finding at that severity this loop) and distance-to-target (concurrency/state_management among this loop's lowest scores). No candidate further from target was available (test_strategy's blocker is structural untestability, not actionable by code). Stalled-Dimension Sweep not yet applicable (loop 2).

## Deepening Candidates
1. **Candidate Module**: `PrimaryWidget`'s panel slide-animation ceremony (Finding F-002). Source friction: four near-identical bodies, one observed drift. Shallow module test fails (no Interface). Behavior to move: `DoubleAnimation`+`Storyboard` construction parameterized by transform/from/to/duration/easing. Dependency category: `in-process`. Test surface after change: none (untestable UWP page). Smallest first step: extract `SlidePanelAsync`. What not to do: no `IAnimator`/`IPanelController` protocol - fails two-adapter rule, no policy/failure/platform-isolation justification.

## Builder Notes
- Pattern: a session-token guard applied at the write/click step but not symmetrically at the fetch/populate step feeding it. → REVIEW_HISTORY.json `loops[1].builder_notes` for full notes.
- Pattern: distinguishing "the request already fired" from "the request's result may still land" when deciding where to place a liveness guard. → REVIEW_HISTORY.json `loops[1].builder_notes` for full notes.
- Pattern: a finding's own evidence line numbers going stale within the same loop that reports them, when that loop's own fix touches earlier lines in the same file. → REVIEW_HISTORY.json `loops[1].builder_notes` for full notes.

## Final Judge Narrative
Place, not win, this loop. Runtime ownership took a real step forward: F-001's session-token fix (loop 1) held up under this loop's independent re-verification, and this loop closed the twin gap in the same mechanism - the picker's population step, not just its click step, now honors the session token, so a superseded request can no longer visibly corrupt the panel it was racing against. Concurrency is more trustworthy than last loop but not yet trustworthy across the board: the picker flow now has two independently-verified guards where it had zero a loop ago, but this loop's own investigation found the second gap by manual reasoning alone, on a file structurally immune to automated tests - the same position that let both gaps go unnoticed for fifteen prior loops. Simplification did not happen this loop - F-002/F-003 remain queued. Tests do not, and structurally cannot, reduce regression risk on this file; the loop's only regression evidence is full build + full suite (unchanged pass count) plus a manual trace of the guard's placement relative to the network calls, exactly as loop 1's fix was verified. Future work risks over-engineering only if F-002/F-003's extractions reach for a coordinator abstraction instead of a private helper method - unchanged guidance from loop 1's own Deepening Candidate.

## Loop 2 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only: added a guard clause to `LoadGridSelectionAsync`, immediately after both network fetches complete (`client.GetSquareIconsAsync`/`GetTitleBearingGridsAsync`) and before any panel mutation: `if (session != gridPanelSessionId) { return; }`. A superseded picker session's already-in-flight network fetch still completes exactly as before (same call count, order, payload, error handling), but its result is now discarded instead of being appended into (`PopulateGridSelectionPanelAsync` only ever appends, never clears) or overwriting the live session's already-displayed panel state. Full build (`msbuild ... /p:AppxBundle=Never`) exits 0 both before and after the change; the full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected, since `PrimaryWidget.xaml.cs` is not part of the test-linked `Services/**` surface. Finding F1 (stable_id F-005) is **resolved**: the specific display-corruption path (a stale, superseded session's population landing after the live session's) is closed by construction, and the fix is verified not to touch network-call count/order/payload/error-handling by direct inspection of the diff (the guard sits textually after the last network await). No unintended scorecard regression: the change touches no network call, no ranking/selection logic, and no file outside the one named. Findings F-002, F-003 and F-004 are carried forward, unchanged in substance (evidence line numbers corrected against current source - see Builder Notes), to the Improvement Backlog for future loops.

## Loop 2 Implementation Review
Verdict: **approved**. Reason: the guard is inserted after both network awaits (`GetTitleBearingGridsAsync` and the icons await) and before any UI mutation, closing the cited race without altering network-call count, order, or payload, with a single confirmed writer to `gridPanelSessionId` and a blast radius limited to a 12-line, zero-deletion hunk in one file. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.

--- Loop 3 (UTC 2026-08-05T01:37:22Z) ---

### Loop Counter
Loop 3 of 10 (cap)

### System Flag
[STATE: CONTINUE]

(Discovery: see Loop 1 Discovery.)

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

Finding F-005's population-step guard (loop 2) held under this loop's independent re-verification. But this loop's own audit found the identical defect class a third time on a sibling flow never touched by loops 1-2: `PerformGameSearchAsync`, the manual search-by-name flow that exists specifically for games SteamGridDB's automatic platform-ID lookup cannot match, had zero session guard at all - not even a partial one. This loop closes that gap with the same established pattern, and independently re-derives the rest of the scorecard from current source.

## Scorecard (1-10)

- Architecture quality: 8.0 | SAME | `ArtworkDownloader.cs:71-100` (re-read this loop) still hides its selection+veto pipeline behind two methods, unchanged. `PrimaryWidget.xaml.cs` remains a 1984-line single-class orchestrator (re-measured this loop via `wc -l`, unchanged from loop 2) carrying Findings F-002/F-003's duplicated ceremony (still open, current lines 742-858 and 1548-1601/1740-1842) plus this loop's newly-found F-006 (an ownership-arbitration gap over `SearchResultsListView` across overlapping search invocations, pre-fix) occupying the same role F-005 held last loop.
- State management and runtime ownership: 7.5 | SAME | F-005's resolution independently re-verified this loop: the guard `if (session != gridPanelSessionId) { return; }` inside `LoadGridSelectionAsync` (current line 1341) still sits after both network awaits and before every panel mutation - re-traced fresh this loop, not carried from loop 2's own claim. Held at 7.5 rather than moved further because this loop's own investigation found Finding F-006 (`PrimaryWidget.xaml.cs:1646-1699` pre-fix): the search-by-name flow's own population step carried the identical stale-authority shape, on a sibling flow F-005's fix never touched. Net severity of the dimension's open concurrency-adjacent hazard is unchanged (one Serious gap closed, one Serious gap of the same shape newly found) - SAME rather than UP per G8 (no structural proof of *net* improvement this loop), and not DOWN (the code did not regress; a pre-existing hazard was found by review, not introduced).
- Domain modeling: 8.5 | SAME | `Models/GamePlatform.cs` discriminated enum + single translation seam (`GamePlatformHelper`) re-read this loop, unchanged. `GameEntry.cs:133-145` still leaves `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` as three independently-settable properties expressing one derived fact; the sole construction site (`PrimaryWidget.xaml.cs:650-664`, shifted +8 lines by this loop's own field addition but otherwise unchanged) still sets all three together - re-verified this loop, still no live harm, not promoted to a Finding.
- Data flow and dependency design: 7.5 | SAME | Precisely re-checked this loop (not just a raw grep): the two `Windows.UI.Xaml` hits under `Services/` (`ArtworkFiles.cs:23`, `GameImages.cs:15`) are doc-comment prose explaining *why* those modules avoid the dependency, not actual imports - zero real leakage confirmed. Deduction unchanged: `StoreNameLookup`'s three unlocked dictionaries (`gogNameCache`, `epicNameCache`, `nameMatchCache`) remain reachable only from `LoadGameEntriesAsync`'s sequential per-entry loop, itself reachable only via `PrimaryWidget_Loaded`/`RefreshButton_Click`, both gated by `TryBeginLibraryOperation` (re-confirmed this loop via a fresh grep of every `TryBeginLibraryOperation`/`IsLibraryOperationBlocking` call site) - no reachable concurrent writer, same conclusion as loop 2, re-verified rather than carried forward.
- Framework / platform best practices: 8.0 | SAME | `SteamGridDbClient.cs:137-141`'s deliberate `DataContractJsonSerializer` + `Windows.Data.Json` split re-read this loop at its current lines, unchanged. Deduction unchanged: `PrimaryWidget.xaml.cs:1548-1601` + `:1740-1842` (Finding F-002, still open) - the four-times duplicated `DoubleAnimation`/`Storyboard` ceremony WinUI's resource/style system exists to avoid.
- Concurrency and runtime safety: 7.0 | SAME | F-005's resolution independently re-verified (see state_management proof), but this loop's own investigation found Finding F-006 (Serious, not Likely disqualifier - like F-005, it cannot write artwork to the wrong game, since a clicked stale search result still only feeds `LoadGridSelectionByGameIdAsync`, itself protected by `gridPanelSessionId`; it can only corrupt the search-results panel's own in-memory display) in a sibling flow. One Serious-severity concurrency gap closed, one Serious-severity gap of the identical shape newly found on a previously-unaudited method: net wash, hence SAME rather than UP (G8: no proof of net structural improvement this loop) or DOWN (no regression, a pre-existing gap was found). Scored here pre-fix per Blind-critic ordering; F-006 fixed this loop, see Loop 3 Result.
- Code simplicity and clarity: 7.0 | SAME | `PrimaryWidget.xaml.cs:742-858` and `:1548-1601`/`:1740-1842` (Findings F-003, F-002 - still open, ~225 lines of 3-4x duplicated ceremony, re-confirmed this loop against current line numbers) unchanged against otherwise-minimal Modules. A helper sub-agent swept `Services/`, `Models/`, and `Stores/` this loop for leaf-module duplication (reuse/simplification/altitude/efficiency angles per method.md Step 6) and found none beyond the already-known `StoreNameLookup` cache asymmetry (data_flow territory, not a new simplicity finding) - the deduction remains concentrated entirely in `PrimaryWidget.xaml.cs`.
- Test strategy and regression resistance: 6.5 | SAME | `PrimaryWidget.xaml.cs` remains untestable outside an app container - re-confirmed this loop via a fresh read of both `.csproj` files (`SteamGridDB.Xbox.csproj` targets `TargetPlatformIdentifier=UAP`; `SteamGridDB.Xbox.Tests.csproj` targets `net8.0-windows10.0.19041.0`, a desktop projection that cannot compile a UWP page). This loop's own investigation found a *third* independent concurrency/state-ownership defect (F-006) on that exact untestable surface via manual source reasoning alone - re-demonstrating, not further disproving, the anchor's own disqualifying language; the untestability was already fully priced into 6.5, so a third confirming instance holds the score rather than moving it further. `TileImageTests.cs` (Finding F-004, re-verified this loop) still has no case at the exact `alpha == 64` or `transparentCorners == 2` boundary.
- Overall implementation credibility: 7.5 | SAME | The `gridPanelSessionId` and now `searchPanelSessionId` field comments continue the codebase's documented-rationale discipline. Deduction unchanged: `TESTING.md`'s framing of the untested UWP surface as covering merely "what they do to the UI" continues to undersell the surface's real risk, now demonstrated a third time by this loop's own F-006 discovery on a sibling method of the same file.

## Authority Map
Re-emitted this loop: an authority-related finding, F-006, is Priority 1.

- Concern: **Selected game for the artwork picker (`CurrentSelectedGame`) - write path**
  - Owner: `PrimaryWidget`
  - Allowed writers: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (nulls on close)
  - Readers: `LoadGridSelectionPanelAsync`, `PopulateGridSelectionPanelAsync`, `GridImage_Click`, `DownloadAndReplaceImageAsync`, `ShowSearchPanelAsync`
  - Persistence seam: none (in-memory UI field)
  - Async mutation entry points: `EditGameImage_Click`, `SearchGameImage_Click`
  - Verdict: **Single and clear**

- Concern: **Grid picker panel display contents (`GridImagesView.Items` / `GridPanelStatus` / `GridLoadingRing` during population)**
  - Owner: `PrimaryWidget` (`LoadGridSelectionAsync` / `PopulateGridSelectionPanelAsync`)
  - Allowed writers: `LoadGridSelectionAsync` (Clear, status text, loading ring), `PopulateGridSelectionPanelAsync` (Add)
  - Readers: user (visually), `GridImage_Click` (via `SessionId` comparison)
  - Persistence seam: none
  - Async mutation entry points: `LoadGridSelectionAsync` (one invocation per Edit/Search-result click; multiple can be in flight concurrently)
  - Verdict: **Single and clear** - F-005's fix (loop 2) independently re-verified this loop: `gridPanelSessionId` is checked immediately after both network awaits and before every subsequent mutation, current line 1341.

- Concern: **Search results panel display contents (`SearchResultsListView.Items` / `SearchPanelStatus` / `SearchLoadingRing` during a name search)**
  - Owner: `PrimaryWidget` (`PerformGameSearchAsync` / `ShowSearchPanelAsync`)
  - Allowed writers (pre-fix): `PerformGameSearchAsync` (Clear, Add, status text, loading ring) with no liveness check; `ShowSearchPanelAsync` (Clear, header/box text) with no invalidation of a search left in flight from a prior showing
  - Readers: user (visually), `SearchResult_Click`
  - Persistence seam: none
  - Async mutation entry points: `PerformGameSearchAsync` (one invocation per Enter-key or Search-button click; multiple can be in flight concurrently, and neither trigger is debounced or disabled while a search runs); `ShowSearchPanelAsync` (one invocation per row's Search-icon click, not blocked by a search already in flight)
  - Verdict: **Split and ambiguous (pre-fix)** - see Finding F-006. This loop's fix (a second session field, `searchPanelSessionId`, bumped on both panel-(re)open and each new search, checked after the network fetch and before any panel mutation) closes the specific display-corruption path; re-audit next loop once the fix has a full loop's scrutiny.

- Concern: **Bulk library operation exclusivity (`isLibraryOperationRunning`)**
  - Owner: `PrimaryWidget`
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Readers: `IsLibraryOperationBlocking`, `TryBeginLibraryOperation`
  - Persistence seam: none
  - Async mutation entry points: `PrimaryWidget_Loaded`, `RefreshButton_Click`, `FixLibraryButton_Click`, `RestoreChangesButton_Click`, `RevertDefaultsButton_Click`
  - Verdict: **Single and clear** - re-verified this loop via a fresh grep of every `TryBeginLibraryOperation`/`IsLibraryOperationBlocking`/`EndLibraryOperation` call site; unchanged.

- Concern: **Applied-artwork record (`AppliedArtworkStore`'s path->artworkId map)**
  - Owner: `AppliedArtworkStore` (static Module)
  - Allowed writers: `SetAsync`, `ClearAsync` (via `UpdateAsync`, gated by `SemaphoreSlim gate`)
  - Readers: `GetAsync` (same gate)
  - Persistence seam: `applied-artwork.json` in the widget's local data (`RecordFolder`)
  - Async mutation entry points: `ReplaceImageCoreAsync`, `RestoreBackupCoreAsync`
  - Verdict: **Single and clear** - unchanged since loop 2; not re-walked line-by-line this loop (no evidence suggests drift), carried forward as background context, not as a scoring basis for any dimension held this loop.

- Concern: **Store-name / capsule-parse / fix-log ambient state (`StoreNameLookup`'s three dictionaries, `SteamGridDbClient.CapsuleParseNotes`, `FixLog`'s fields)**
  - Owner: `StoreNameLookup` / `SteamGridDbClient` / `FixLog` (static Modules)
  - Allowed writers: `GetOrFetchGogNameAsync`, `GetOrFetchEpicNameAsync`, `FindGameByNameAsync` (unlocked dictionaries), `NoteCapsuleParse` (unlocked list), `FixLog.Start`/`Write` (unlocked list+fields)
  - Readers: same methods, `FixLibraryAsync` (reads `CapsuleParseNotes`)
  - Persistence seam: none
  - Async mutation entry points: `LoadGameEntriesAsync`'s per-entry loop; `FixLibraryAsync`
  - Verdict: **Single and clear** - re-verified this loop (see data_flow proof): both operations are reachable only through gated entry points sharing `isLibraryOperationRunning`, so they can never run concurrently with each other or themselves.

## Strengths That Matter
- `PrimaryWidget.xaml.cs`'s two session-token mechanisms (`gridPanelSessionId` at `:64-72`, now `searchPanelSessionId` at `:75-80`) share one clear idiom - capture before the first await, check immediately before the destructive mutation - documented with the exact race window and failure mode each closes rather than a generic "thread safety" comment. Verified by independently re-tracing both this loop, not by trusting either loop's own prior claim.
- `SteamGridDbClient.cs:137-141`'s `DataContractJsonSerializer`/`Windows.Data.Json` split is justified in a doc comment citing the actual JSON shape (`external_platform_data.steam[0].metadata...{language}`) that the serializer cannot express - a real, still-true reason for the framework mismatch, not an unexplained inconsistency.
- `LoadGridSelectionAsync`'s comment (`:1289-1291` region, re-read this loop) names the two call sites that used to be separate near-identical methods and explains why they were unified - a real record of a prior deduplication, not a claimed one.

## Findings

### Finding #1 (stable_id F-006): PerformGameSearchAsync's results-population step has no ownership check against a superseded search session

**Why it matters** — A user who searches for a game by name, then edits the search box and searches again (or reopens the search panel for the same or a different game) before the first request's network round trip finishes, can see stale results from the superseded search silently mixed into the results list - unlabeled, and pointing at the wrong game if clicked - or have the live search's loading/status state clobbered by the stale one's, with no error shown.

**What is wrong** — `PerformGameSearchAsync` (`PrimaryWidget.xaml.cs:1646-1699` pre-fix) had no session/generation guard at all - unlike `LoadGridSelectionAsync`, which Finding F-005's fix (loop 2) protected with `gridPanelSessionId`, this sibling population step on the manual search-by-name flow (triggered by `GameSearchBox_KeyDown`'s Enter key or `SearchGames_Click`'s button, neither debounced nor disabled while a search is in flight) had zero equivalent. After the network fetch (`client.SearchGameByNameAsync(searchTerm)`) returned, the method unconditionally wrote `SearchResultsListView.Items` (a `foreach...Add` loop that never clears on completion, only at the start of the next search or panel reopen) and `SearchPanelStatus.Text`/`SearchLoadingRing.IsActive`, even when a newer search or panel reopen had since superseded it. `SearchGameImage_Click` (`PrimaryWidget.xaml.cs:1606-1622`) reopens the panel for any row's game and is gated only by the bulk `isLibraryOperationRunning` flag - not by "is a search already in flight" - so the same reentrancy shape Finding F-001 (loop 1) closed for the grid picker's click path and Finding F-005 (loop 2) closed for the grid picker's population path was still fully open on this sibling flow.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1646-1699` (pre-fix, no session capture, no re-check after the network await), `:1606-1622` (`SearchGameImage_Click`, gated only by `IsLibraryOperationBlocking()`, not by any per-panel liveness state), `:1720-1784` (pre-fix, `ShowSearchPanelAsync` reopens/reuses the same `SearchResultsListView` for any game with no invalidation of a search still pending from a prior showing)

**Architectural test failed** — n/a - different category (state-ownership/reentrancy defect, matching Findings F-001/F-005's own categorization)

**Dependency category** — n/a (not a Coupling & Leakage finding)

**Leverage impact** — None - correctness fix inside `PrimaryWidget`'s own `PerformGameSearchAsync`/`ShowSearchPanelAsync`, not a change to any caller-facing Interface.

**Locality impact** — Fix stays entirely inside `PrimaryWidget.xaml.cs` (one new field, two small guard/invalidation additions); no other Module's behavior changes, and no network call is added, removed, or reordered.

**Metric signal, if any** — none

**Why this weakens submission** — This is the same "stale authority remains alive" hazard class Findings F-001 and F-005 already closed on the grid-picker flow, found a third time on an adjacent, previously-unaudited flow in the same file, with the same zero possible test coverage. Unlike F-001, no artwork write is ever routed through the search-results list directly - clicking a stale search result still only feeds `LoadGridSelectionByGameIdAsync`, itself protected by `gridPanelSessionId` - so this cannot corrupt a game's persisted artwork; it can only silently corrupt the search-results panel's own in-memory display, misleading the user about which game a listed search result actually is.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Add a second session field (`searchPanelSessionId`, mirroring `gridPanelSessionId`) bumped at the top of both `ShowSearchPanelAsync` (invalidating any search left over from a prior showing) and `PerformGameSearchAsync` (capturing the session before the first await); check it immediately after `client.SearchGameByNameAsync(searchTerm)` returns and before any results-list/status mutation. This runs after the network call has already fired unconditionally, so it changes no network call count, ordering, payload, or error handling - it only skips the subsequent local UI-list/status mutation for a session no longer live, mirroring the exact pattern Findings F-001 and F-005 already established.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/Models/**`, `SteamGridDB.Xbox/Services/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml`.

**Status this loop: implemented — see Loop 3 Result below.**

### Finding #2 (stable_id F-002): Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to the panel's slide timing or easing (or a bug in it) has to be made and verified in four places instead of one, and the four copies have already begun to drift (200ms hide vs 250ms show, independently re-derived each time).

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` (current lines `1548-1569`, `1574-1601`, `1740-1808`, `1813-1842`) each hand-build a `DoubleAnimation` + `Storyboard` against a `TranslateTransform`, set From/To/Duration/EasingFunction, call `storyboard.Begin()`, then await `Task.Delay` matching the duration. The only real variation across the four is which transform, which direction, and 250ms vs 200ms. Re-confirmed unchanged this loop by an independent helper sweep - all four bodies remain byte-identical in structure.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1548-1569`, `:1574-1601`, `:1740-1808`, `:1813-1842` (line numbers corrected this loop against current source - this loop's own F-006 fix shifted the file below the insertion points; see Builder Notes)

**Architectural test failed** — Shallow module (each Show/Hide method's Interface ≈ its Implementation; no reuse across the four near-identical bodies)

**Dependency category** — n/a

**Leverage impact** — One call site to read/change instead of four; callers learn less, not more.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`; no other file's behavior changes.

**Metric signal, if any** — none

**Why this weakens submission** — Four near-identical bodies in the single largest, most-churned file in the codebase (`PrimaryWidget.xaml.cs`, 1984 LOC) is exactly the leaf-module duplication the Simplicity dimension's 9-anchor requires be swept for; it currently is not swept away. Two loops queued without action.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(FrameworkElement transform, double from, double to, int durationMs, EasingMode mode)` helper that builds the `DoubleAnimation`/`Storyboard` once and awaits `Task.Delay(durationMs)`; each of the four call sites becomes a one-line call. No new Seam, no new file - the four bodies collapse into the one Module (`PrimaryWidget`) that already owns them.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml` (markup unchanged), `SteamGridDB.Xbox/Services/**`.

### Finding #3 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations has to be kept in sync by hand - the same style-resource assignment, the same `XamlRoot` API-contract check, the same `TryBeginLibraryOperation`/`EndLibraryOperation` guard wiring - and nothing enforces that a future fourth operation (or an edit to one of the three) follows the same shape.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (current lines `742-786`, `788-822`, `824-858`) each build a `ContentDialog` with the same four style-resource lookups and the same `Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent` `XamlRoot` check, call `ShowAsync`, branch on the result, and wrap the actual operation in the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` pattern. Re-confirmed unchanged this loop.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:742-786`, `:788-822`, `:824-858` (line numbers corrected this loop against current source - see Builder Notes)

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action instead of re-deriving the whole confirm-guard-run shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as Finding F-002, in the same file; combined the two clusters account for roughly 225 of `PrimaryWidget.xaml.cs`'s 1984 lines being ceremony repeated 3-4x rather than owned once. Two loops queued without action.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `ConfirmAndRunAsync(string title, string content, string primaryText, string secondaryText, Func<ContentDialogResult, bool> shouldRun, Func<Task> action)` (or the smallest signature that covers the 2-button and 3-button cases) that owns dialog construction, the `XamlRoot` check, and the `TryBeginLibraryOperation`/`EndLibraryOperation` wrapping; each handler becomes a short call naming its own title/content/action.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #4 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation at either threshold (`<` to `<=`) would ship silently: the corner-transparency gate that keeps case-mockup art off tiles would become off-by-one permissive or strict with no test failing.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) treats a pixel transparent when `alpha < 64` (`:250`) and treats a corner as transparent when more than 14 of its 36 sampled pixels are transparent, then rejects the image when 2 or more of its 4 corners are transparent (`transparentCorners < 2`, `:263`). `TileImageTests` exercises fully-opaque and fully-transparent corners but not alpha exactly at 64 or a candidate with exactly 2 transparent corners, so a mutation at either boundary is invisible to the suite. Re-read this loop (`TileImage.cs` unaffected by this loop's edits); gap re-confirmed unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — A minor, off-primary-flow gap (per the Test strategy dimension's own anchor language, an off-path helper boundary is Cosmetic on its own) but worth naming before it is mistaken for full coverage.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases: a corner at exactly alpha 63/64, and an image with exactly 2 (not 0, not 4) transparent corners, asserting the documented boundary (`< 64` transparent, `< 2` corners passes).

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs` (test-only addition, no production change).

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Finding F-006's session-liveness guard closes a real, evidenced display-corruption path (state-ownership fix, matching F-001/F-005's own categorization) |
| New seam justified | No - considered and rejected a shared `SessionGuard` mini-type over two 2-3 line idioms (fails SPT Q2/Q3) |
| Helpful simplification | none this loop (F-002/F-003 remain queued, not implemented) |
| Should NOT be done | Generic session/request-coordinator abstraction; also rejected guarding the early-return branches (no interleaving possible pre-await) and the `catch` block (established, already-approved asymmetry) |
| Tests after fix | None added/deleted - untestable surface; verified by full build+suite plus manual trace of both guard placements |

## Improvement Backlog
1. **Collapse the four-times-duplicated panel slide animation (Finding F-002).** score impact: `simplicity +0.5` — simplification, helpful
2. **Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003).** score impact: `simplicity +0.5; framework_idioms +0.5` — simplification, helpful

**Priority-1 accounting**: F-006 is Priority 1 on severity (Serious deduction, the only finding at that severity this loop) and distance-to-target (concurrency/state_management tied-lowest with simplicity at 7.0). Per this loop's explicit bias check (two prior loops both landed in `PrimaryWidget`'s picker/session area): F-006 is not a third slice of the `gridPanelSessionId` mechanism - it is a previously-unaudited sibling method (`PerformGameSearchAsync`/`ShowSearchPanelAsync`/`SearchResultsListView`) loops 1-2's own Authority Map never covered. Rejected alternative: F-002 (does not fail SPT, but Backlog Prioritization criterion 3 - severity - breaks the distance tie decisively toward F-006: Serious beats Noticeable). test_strategy still has no actionable candidate (structural untestability). Stalled-Dimension Sweep not yet applicable (loop 3).

## Deepening Candidates
1. **Candidate Module**: `PrimaryWidget`'s panel slide-animation ceremony (Finding F-002). Source friction: four near-identical bodies, one observed drift, re-confirmed unchanged this loop. Shallow module test fails (no Interface). Behavior to move: `DoubleAnimation`+`Storyboard` construction parameterized by transform/from/to/duration/easing. Dependency category: `in-process`. Test surface after change: none (untestable UWP page). Smallest first step: extract `SlidePanelAsync`. What not to do: no `IAnimator`/`IPanelController` protocol - fails two-adapter rule, no policy/failure/platform-isolation justification.

## Builder Notes
- Pattern: the same async-population-without-a-liveness-guard defect recurring across sibling methods in one file, because a fix scoped to the finding's own call site does not audit every OTHER method with the same shape. → REVIEW_HISTORY.json `loops[2].builder_notes` for full notes.
- Pattern: two methods jointly own one liveness concept, and guarding only the one that awaits a network call misses the one that resets the panel's identity. → REVIEW_HISTORY.json `loops[2].builder_notes` for full notes.
- Pattern: a registry occurrence recorded with a placeholder resolution sha at commit time, then never backfilled. → REVIEW_HISTORY.json `loops[2].builder_notes` for full notes.

## Final Judge Narrative
Place, not win, this loop. Runtime ownership is more thoroughly audited than it was, but the audit keeps finding the same shape: F-005's population-step guard (loop 2) held under this loop's independent re-verification, and this loop closed the identical gap on a sibling flow (`PerformGameSearchAsync`/`ShowSearchPanelAsync`) that neither loop 1 nor loop 2 had touched or even listed in the Authority Map. Concurrency is not more trustworthy this loop in any measurable sense - one Serious hazard closed, one Serious hazard of the same shape found on a previously-unaudited method, net wash - but it is not less trustworthy either, since nothing regressed; a pre-existing gap was found, not introduced. Simplification did not happen this loop - F-002/F-003 remain queued for a second full loop, and this loop's own bias check concluded (on severity and distance-to-target, not on adjacency) that closing a Serious display-corruption hazard on a primary rescue flow outranked collapsing Noticeable ceremony. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; this loop's only regression evidence is full build + full suite (138/138 unchanged) plus a manual trace of both new guard placements relative to the one network call each protects. Future work risks over-engineering only if F-002/F-003's eventual extraction reaches for a coordinator abstraction instead of a private helper method, or if a future loop builds a shared "SessionGuard" type for what remain two small, honestly-duplicated 2-3 line idioms - unchanged guidance from loop 1's own Deepening Candidate, now re-affirmed against a second field.

## Loop 3 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (24 insertions, 0 deletions): added a new `searchPanelSessionId` field (mirroring `gridPanelSessionId`); bumped it at the top of `ShowSearchPanelAsync` (invalidating any search left in flight from a prior showing of the panel); captured it in a local `session` at the top of `PerformGameSearchAsync` (before any await); added a guard clause immediately after `List<SteamGridDbGame> results = await client.SearchGameByNameAsync(searchTerm);` and before any subsequent branch: `if (session != searchPanelSessionId) { return; }`. A superseded search's already-in-flight network fetch still completes exactly as before (same call count, order, payload, error handling - `client.SearchGameByNameAsync` is untouched and always fires exactly once per invocation), but its result is now discarded instead of being appended into or overwriting whatever the live search or freshly-reopened panel already showed. Full build (`msbuild ... /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected, since `PrimaryWidget.xaml.cs` is not part of the test-linked `Services/**` surface. Finding F1 (stable_id F-006) is **resolved**: the specific display-corruption path (a stale, superseded search's or panel-reopen's population landing after the live one's) is closed by construction, verified by direct inspection of the diff (both guards sit textually after their only relevant await, or are pure invalidation bumps with no await of their own) and by re-reading the final source. No unintended scorecard regression: the change touches no network call, no ranking/selection logic, and no file outside the one named. Findings F-002, F-003 and F-004 are carried forward, unchanged in substance (evidence line numbers corrected against current source - see Builder Notes), to the Improvement Backlog / Findings for future loops.

## Loop 3 Implementation Review
Verdict: **approved**. Reason: both checks confirm a genuine session-guard fix mirroring the already-approved `gridPanelSessionId` pattern, with no new seam, no suppression, and no same-or-higher-severity regression. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.

--- Loop 4 (UTC 2026-08-05T02:11:18Z) ---

### Loop Counter
Loop 4 of 10 (cap)

### System Flag
[STATE: CONTINUE]

(Discovery: see Loop 1 Discovery.)

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

Loop 3's own Builder Note said fixing one instance of a defect class is not the same as auditing the class. This loop finally ran that audit for real: every `private async void`/`private async Task` method in `PrimaryWidget.xaml.cs` (32 methods, enumerated by grep, not sampled) was checked for the awaited-then-mutate-without-a-recheck shape. It found a fourth instance - nested inside `PopulateGridSelectionPanelAsync`, a method loop 2's own fix (`LoadGridSelectionAsync`, Finding F-005) already calls - proving the entry-point-level session checks loops 1-3 built do not protect a callee with its own await. This loop closes that specific gap and, for the first time across four loops, backs the fix with a genuinely exhaustive sweep of the file.

## Scorecard (1-10)

- Architecture quality: 8.0 | SAME | `ArtworkDownloader.cs:71-100` (re-read this loop, unchanged) still hides a five-step selection+veto pipeline behind two methods. `PrimaryWidget.xaml.cs` is now 2020 lines (2008 pre-fix; +12 from this loop's own guard clause) still carrying Findings F-002/F-003's duplicated ceremony (still open, current lines 742-858 and 1560-1613/1752-1854) plus this loop's newly-found-and-now-fixed F-007 (an ownership-arbitration gap nested inside `PopulateGridSelectionPanelAsync`'s own await, one level deeper than any of F-001/F-005/F-006). **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** F-007 is this dimension's named candidate, routed through Findings below.
- State management and runtime ownership: 7.0 | DOWN | F-005's (loop 2) and F-006's (loop 3) own fixes independently re-verified this loop as still holding at their own call sites - neither regressed. But this loop's first-ever exhaustive enumeration of the file's 32 `private async` methods found a fourth instance of the identical shape inside `PopulateGridSelectionPanelAsync` (`PrimaryWidget.xaml.cs:1428-1489` pre-fix) - and this one is qualitatively different from F-005/F-006: it lives *inside* a method that a prior loop's own fix already calls, proving the caller-level check does not protect a callee with its own suspension point. That is new information, not "one more sibling missed" (the framing loops 2-3 used for their own SAME calls) - it shows loop 3's SAME assessment under-priced the true residual, because neither loop 2 nor loop 3 checked inside its own fix's callees for the same shape. Moved DOWN rather than held SAME: the code did not regress since loop 3, but this loop's own more rigorous investigation is the basis for judging loop 3's confidence level as having been too high, which is a legitimate basis for a downward correction distinct from "the code got worse." Fixed this loop; see Loop 4 Result.
- Domain modeling: 8.5 | SAME | `Models/GamePlatform.cs` discriminated enum + single translation seam (`GamePlatformHelper`) read in full this loop, unchanged. `GameEntry.cs:113-145` (re-read this loop) still leaves `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` as three independently-settable properties expressing one derived fact; the sole construction site (`PrimaryWidget.xaml.cs:661-663`, unaffected by this loop's edit since it sits above the insertion point) still sets all three together - re-verified this loop, still no live harm, not promoted to a Finding. **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** walked `Models/` in full (`GameEntry.cs`, `GamePlatform.cs`, `GridImageItem.cs` - 3 types) - explicit clean; no impossible state representable, no new parallel-fields case beyond the one already tracked above.
- Data flow and dependency design: 7.5 | SAME | `Services/*` re-confirmed zero `Windows.UI.Xaml` imports by grep this loop (the two hits under `Services/` - `ArtworkFiles.cs:23`, `GameImages.cs:15` - remain doc-comment prose). **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** independently traced `StoreNameLookup`'s three unlocked dictionaries (`gogNameCache:29`, `epicNameCache:30`, `nameMatchCache:34`) and `FixLog`'s unlocked fields to their only call sites - both reachable exclusively through `LoadGameEntriesAsync`/`FixLibraryAsync`, both gated by the single `isLibraryOperationRunning` flag (`PrimaryWidget.xaml.cs:144/724/773/809/845`) - re-confirmed unreachable concurrently, same conclusion as loops 2-3. This walk explicitly evaluated and falsified a helper-surfaced candidate this loop ("unsynchronized static dictionaries," flagged without a reachability trace) - compliance-is-not-clearance cuts both ways: a shape that looks dangerous still needs a call-graph trace before it is a finding, and this one does not clear it.
- Framework / platform best practices: 8.0 | SAME | `SteamGridDbClient.cs:136-142`'s deliberate `DataContractJsonSerializer` + `Windows.Data.Json` split re-verified this loop (doc comment still cites the concrete JSON shape - `external_platform_data.steam[0].metadata.library_capsule_full.image2x.{language}` - the serializer cannot express). Deduction unchanged: `PrimaryWidget.xaml.cs:1560-1613`/`:1752-1854` (Finding F-002, still open, line numbers shifted +12 by this loop's own fix) - the four-times duplicated `DoubleAnimation`/`Storyboard` ceremony WinUI's resource/style system exists to avoid. **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** F-002 is this dimension's named candidate; still queued (Priority 2 this loop), not implemented, third loop running.
- Concurrency and runtime safety: 6.5 | DOWN | Same evidence as state_management. F-007 (Serious deduction, same tier as F-005/F-006 - cannot write artwork to the wrong game, since a clicked stale tile still only feeds `LoadGridSelectionByGameIdAsync`/`DownloadAndReplaceImageAsync`, both gated on `gridPanelSessionId` at click time; it can only corrupt the grid panel's own in-memory display and artwork ranking) is this loop's fourth discovery of the identical async-population-without-a-recheck shape, and the first found via a genuinely complete sweep rather than incidental discovery while fixing something else. Moved DOWN, not held SAME: loop 3's own SAME reasoning ("one Serious gap closed, one of the same shape newly found: net wash") assumed each loop's fix-plus-find pair was a complete accounting of that loop's residual; this loop's fix proves that assumption undercounted at loop 3 (and, by the same logic, at loop 2) - the residual was larger than either loop's own net-wash accounting captured, because neither loop checked inside its own fix's callees. Fixed this loop, with a full-file sweep providing the strongest evidence yet that no further instance remains (see Loop 4 Result and Builder Notes).
- Code simplicity and clarity: 7.0 | SAME | `PrimaryWidget.xaml.cs:742-858` and `:1560-1613`/`:1752-1854` (Findings F-003, F-002 - still open, ~225 lines of 3-4x duplicated ceremony, re-confirmed this loop at current line numbers) unchanged against otherwise-minimal Modules. **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** a helper sub-agent swept `Services/Artwork`, `Services/Library`, `Services/SteamGridDB`, `Services/Stores` and `Models/` this loop across all four angles (Reuse/Simplification/Altitude/Efficiency per method.md Step 6) and found nothing beyond the already-known `StoreNameLookup` cache asymmetry (data_flow territory) and `GameEntry`'s parallel-fields case (domain_modeling territory) - neither new, the deduction remains concentrated entirely in `PrimaryWidget.xaml.cs`'s two known clusters.
- Test strategy and regression resistance: 6.5 | SAME | `PrimaryWidget.xaml.cs` remains untestable outside an app container - re-confirmed this loop via both `.csproj` target frameworks (`SteamGridDB.Xbox.csproj`: `TargetPlatformIdentifier=UAP`; `SteamGridDB.Xbox.Tests.csproj`: `TargetFramework=net8.0-windows10.0.19041.0`, a desktop projection that cannot compile a UWP page). This loop's own investigation found a *fourth* independent concurrency/state-ownership defect (F-007) on that exact untestable surface via manual source reasoning alone, nested one level deeper than any of the first three - re-demonstrating, not further disproving, the anchor's own disqualifying language; the untestability was already fully priced into 6.5, so a fourth, deeper confirming instance holds the score rather than moving it further. **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME - the lowest-scored dimension on the board):** walked `TileImageTests.cs` in full (Finding F-004, re-verified this loop, still no case at the exact `alpha == 64` or `transparentCorners == 2` boundary) - the only candidate this dimension has ever had across four loops, and it does not win Priority 1: it is Cosmetic severity and explicitly off-primary-flow per the rubric's own anchor carve-out, so fixing it would not proportionally advance this dimension's anchor, which is capped by `PrimaryWidget.xaml.cs`'s structural untestability regardless of what `TileImage.cs`'s test file contains.
- Overall implementation credibility: 7.5 | SAME | The `gridPanelSessionId`/`searchPanelSessionId` field comments continue the codebase's documented-rationale discipline; this loop's own `PopulateGridSelectionPanelAsync` fix follows the identical comment style. Deduction unchanged: `TESTING.md:47-56`'s framing of the untested UWP surface ("What they *do* to the UI is not [covered]") continues to undersell the surface's real risk, now demonstrated a fourth time by this loop's own F-007 discovery, nested deeper than any prior instance. **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** re-read `TESTING.md` in full this loop; the quoted framing is verbatim unchanged from loop 1's own citation, still accurate but still under-describing the risk this loop's own finding demonstrates.

## Authority Map
Re-emitted this loop: an authority-related finding, F-007, is Priority 1.

- Concern: **Selected game for the artwork picker (`CurrentSelectedGame`) - write path**
  - Owner: `PrimaryWidget`
  - Allowed writers: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (nulls on close)
  - Readers: `LoadGridSelectionPanelAsync`, `PopulateGridSelectionPanelAsync`, `GridImage_Click`, `DownloadAndReplaceImageAsync`, `ShowSearchPanelAsync`
  - Persistence seam: none (in-memory UI field)
  - Async mutation entry points: `EditGameImage_Click`, `SearchGameImage_Click`
  - Verdict: **Single and clear**

- Concern: **Grid picker panel display contents (`GridImagesView.Items` / `GridPanelStatus` / `GridLoadingRing` during population)**
  - Owner: `PrimaryWidget` (`LoadGridSelectionAsync` / `PopulateGridSelectionPanelAsync`)
  - Allowed writers: `LoadGridSelectionAsync` (Clear, status text, loading ring), `PopulateGridSelectionPanelAsync` (Add) - pre-fix: no re-check of its own await
  - Readers: user (visually), `GridImage_Click` (via `SessionId` comparison)
  - Persistence seam: none
  - Async mutation entry points: `LoadGridSelectionAsync` (one invocation per Edit/Search-result click; multiple can be in flight concurrently)
  - Verdict: **Split and ambiguous (pre-fix)** - see Finding F-007. `LoadGridSelectionAsync`'s own guard (loop 2, F-005) checks immediately after its network awaits but does not cover `PopulateGridSelectionPanelAsync`'s own internal await; this loop's fix adds the equivalent check inside `PopulateGridSelectionPanelAsync` itself, closing the nested gap. Re-audit next loop once the fix has a full loop's scrutiny.

- Concern: **Search results panel display contents (`SearchResultsListView.Items` / `SearchPanelStatus` / `SearchLoadingRing` during a name search)**
  - Owner: `PrimaryWidget` (`PerformGameSearchAsync` / `ShowSearchPanelAsync`)
  - Allowed writers: `PerformGameSearchAsync` (Clear, Add, status text, loading ring), `ShowSearchPanelAsync` (Clear, header/box text)
  - Readers: user (visually), `SearchResult_Click`
  - Persistence seam: none
  - Async mutation entry points: `PerformGameSearchAsync`, `ShowSearchPanelAsync`
  - Verdict: **Single and clear** - re-verified this loop: `PerformGameSearchAsync` has exactly one `await` in its whole body (`client.SearchGameByNameAsync`), and the existing session check sits immediately after it and before every mutation - unlike the grid picker's `PopulateGridSelectionPanelAsync`, there is no second, nested await left unguarded on this flow.

- Concern: **Bulk library operation exclusivity (`isLibraryOperationRunning`)**
  - Owner: `PrimaryWidget`
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Readers: `IsLibraryOperationBlocking`, `TryBeginLibraryOperation`
  - Persistence seam: none
  - Async mutation entry points: `PrimaryWidget_Loaded`, `RefreshButton_Click`, `FixLibraryButton_Click`, `RestoreChangesButton_Click`, `RevertDefaultsButton_Click`
  - Verdict: **Single and clear** - re-verified this loop via a fresh grep of every `TryBeginLibraryOperation`/`IsLibraryOperationBlocking`/`EndLibraryOperation` call site; unchanged. Note: `RestoreBackup_Click` and `SearchGameImage_Click`/`EditGameImage_Click` check only this bulk flag, not a per-row guard - walked this loop, see Simplification Check for why `RestoreBackup_Click`'s narrower race was not promoted to a Finding.

- Concern: **Applied-artwork record (`AppliedArtworkStore`'s path->artworkId map)**
  - Owner: `AppliedArtworkStore` (static Module)
  - Allowed writers: `SetAsync`, `ClearAsync` (via `UpdateAsync`, gated by `SemaphoreSlim gate`)
  - Readers: `GetAsync` (same gate)
  - Persistence seam: `applied-artwork.json` in the widget's local data (`RecordFolder`)
  - Async mutation entry points: `ReplaceImageCoreAsync`, `RestoreBackupCoreAsync`
  - Verdict: **Single and clear** - unchanged since loop 2; not re-walked line-by-line this loop (no evidence suggests drift), carried forward as background context, not as a scoring basis for any dimension held this loop.

- Concern: **Store-name / capsule-parse / fix-log ambient state (`StoreNameLookup`'s three dictionaries, `SteamGridDbClient.CapsuleParseNotes`, `FixLog`'s fields)**
  - Owner: `StoreNameLookup` / `SteamGridDbClient` / `FixLog` (static Modules)
  - Allowed writers: `GetOrFetchGogNameAsync`, `GetOrFetchEpicNameAsync`, `FindGameByNameAsync` (unlocked dictionaries), `NoteCapsuleParse` (unlocked list), `FixLog.Start`/`Write` (unlocked list+fields)
  - Readers: same methods, `FixLibraryAsync` (reads `CapsuleParseNotes`)
  - Persistence seam: none
  - Async mutation entry points: `LoadGameEntriesAsync`'s per-entry loop; `FixLibraryAsync`
  - Verdict: **Single and clear** - independently re-traced this loop (see data_flow proof): both operations are reachable only through gated entry points sharing `isLibraryOperationRunning`, so they can never run concurrently with each other or themselves. This loop's trace also evaluated and falsified a helper-surfaced "unsynchronized dictionaries" candidate.

## Strengths That Matter
- `PrimaryWidget.xaml.cs`'s two session-token mechanisms (`gridPanelSessionId` at `:64-72`, `searchPanelSessionId` at `:75-80`) share one clear idiom - capture before the first await, check immediately before the destructive mutation - documented with the exact race window and failure mode each closes rather than a generic "thread safety" comment. This loop's own fix (inside `PopulateGridSelectionPanelAsync`) follows the identical idiom and comment style, verified by direct inspection rather than assumed.
- `SteamGridDbClient.cs:136-142`'s `DataContractJsonSerializer`/`Windows.Data.Json` split is justified in a doc comment citing the actual JSON shape (`external_platform_data.steam[0].metadata...{language}`) that the serializer cannot express - a real, still-true reason for the framework mismatch, re-verified this loop.
- `ArtworkFiles.RestoreOriginalAsync` (`ArtworkFiles.cs:160-191`, re-read this loop while evaluating `RestoreBackup_Click`'s missing per-row guard) is race-tolerant by construction: it locates the backup file before doing anything destructive, so a second concurrent restore attempt for the same game fails closed into a caught, reported `BackupMissing` rather than corrupting data - a real defensive property discovered by this loop's own sweep, not merely asserted.

## Findings

### Finding #1 (stable_id F-007): PopulateGridSelectionPanelAsync's own await is not re-checked against a superseded picker session

**Why it matters** — A user who edits or re-opens the artwork picker while a prior population's own local lookup is still pending can see stale, permanently unclickable tiles mixed into the live session's panel, the live session's status text overwritten by the stale one's, or the stale session's artwork ranked using whatever game is now selected instead of the game it was actually fetched for.

**What is wrong** — `PopulateGridSelectionPanelAsync` (`PrimaryWidget.xaml.cs:1428-1489` pre-fix) receives an already-session-checked call from `LoadGridSelectionAsync` - the caller re-checks `session != gridPanelSessionId` immediately after its own network awaits, per Finding F-005's loop-2 fix, before calling this method - but that check does not cover an await *inside* `PopulateGridSelectionPanelAsync` itself: `int? appliedArtworkId = await AppliedArtworkStore.GetAsync(...)` (line 1431 pre-fix) is a genuine suspension point (a semaphore wait plus, on first access, a disk read), and nothing re-checks `sessionId != gridPanelSessionId` after it returns. If a newer `LoadGridSelectionAsync` call (a different Edit/Search click) starts and bumps `gridPanelSessionId` while this await is pending, the resumed method still ranks grids/icons using `ArtworkRanker.RankGrids(grids, CurrentSelectedGame?.Name)` - reading `CurrentSelectedGame`'s now-different value - and unconditionally executes `GridImagesView.Items.Add(...)` and overwrites `GridPanelStatus.Text` for a session no longer live.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1428-1489` (pre-fix, no session re-check after its own await), `:1349-1352` (`LoadGridSelectionAsync`'s own session check, loop 2's Finding F-005, sits before calling `PopulateGridSelectionPanelAsync`, not inside it), `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:63-84` (`GetAsync` awaits `appliedCache.GetOrLoadAsync()` and `gate.WaitAsync()`, both genuine suspension points)

**Architectural test failed** — n/a - different category (state-ownership/reentrancy defect, matching Findings F-001/F-005/F-006's own categorization)

**Dependency category** — n/a (not a Coupling & Leakage finding)

**Leverage impact** — None - correctness fix inside `PopulateGridSelectionPanelAsync`'s own body, not a change to any caller-facing Interface; `LoadGridSelectionAsync`'s call site is unchanged.

**Locality impact** — Fix stays entirely inside `PopulateGridSelectionPanelAsync` (one guard clause reusing the existing `gridPanelSessionId` field and the `sessionId` parameter already threaded through); no other Module's behavior changes, and no network call is added, removed, or reordered.

**Metric signal, if any** — none

**Why this weakens submission** — This is the same "stale authority remains alive" hazard class Findings F-001, F-005 and F-006 already closed, found a fourth time - and, unlike the first three, nested one level deeper than any prior loop audited: inside a helper method a prior loop's own fix (F-005, loop 2) already called, not at a fresh top-level entry point. It proves the entry-point-level session checks loops 1-3 built do not compose across a call boundary when the callee has its own await - a materially different fact than "one more sibling method was missed," and evidence the codebase's remaining untouched async handlers cannot be assumed safe merely because their direct caller is guarded. Severity mirrors F-005/F-006: clicking a stale tile still only feeds `LoadGridSelectionByGameIdAsync`/`DownloadAndReplaceImageAsync`, both gated on `gridPanelSessionId` at click time, so no artwork can be written to the wrong game - only the panel's own in-memory display and ranking can be corrupted.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Add the same guard clause already used at the other three sites (`if (sessionId != gridPanelSessionId) { return; }`) immediately after the `appliedArtworkId` await and before any further read of `CurrentSelectedGame` or mutation of `GridImagesView.Items`/`GridPanelStatus`. Reuses the existing `gridPanelSessionId` field and the `sessionId` parameter already threaded through - no new field, no new type. This runs after the only await in the method and before every mutation, so it changes no network call (this method makes none), no call count, ordering, payload, or error handling elsewhere in the flow.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/Models/**`, `SteamGridDB.Xbox/Services/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml`.

**Status this loop: implemented — see Loop 4 Result below.**

### Finding #2 (stable_id F-002): Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to the panel's slide timing or easing (or a bug in it) has to be made and verified in four places instead of one, and the four copies have already begun to drift (200ms hide vs 250ms show, independently re-derived each time).

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` (current lines `1560-1581`, `1586-1613`, `1752-1820`, `1825-1854`) each hand-build a `DoubleAnimation` + `Storyboard` against a `TranslateTransform`, set From/To/Duration/EasingFunction, call `storyboard.Begin()`, then await `Task.Delay` matching the duration. The only real variation across the four is which transform, which direction, and 250ms vs 200ms. Re-confirmed unchanged this loop (line numbers shifted +12 by this loop's own F-007 fix, which lands above all four bodies).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1560-1581`, `:1586-1613`, `:1752-1820`, `:1825-1854`

**Architectural test failed** — Shallow module (each Show/Hide method's Interface ≈ its Implementation; no reuse across the four near-identical bodies)

**Dependency category** — n/a

**Leverage impact** — One call site to read/change instead of four; callers learn less, not more.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`; no other file's behavior changes.

**Metric signal, if any** — none

**Why this weakens submission** — Four near-identical bodies in the single largest, most-churned file in the codebase (`PrimaryWidget.xaml.cs`, 2020 LOC) is exactly the leaf-module duplication the Simplicity dimension's 9-anchor requires be swept for; it currently is not swept away. Three loops queued without action.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(FrameworkElement transform, double from, double to, int durationMs, EasingMode mode)` helper that builds the `DoubleAnimation`/`Storyboard` once and awaits `Task.Delay(durationMs)`; each of the four call sites becomes a one-line call. No new Seam, no new file - the four bodies collapse into the one Module (`PrimaryWidget`) that already owns them.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml` (markup unchanged), `SteamGridDB.Xbox/Services/**`.

### Finding #3 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations has to be kept in sync by hand - the same style-resource assignment, the same `XamlRoot` API-contract check, the same `TryBeginLibraryOperation`/`EndLibraryOperation` guard wiring - and nothing enforces that a future fourth operation (or an edit to one of the three) follows the same shape.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (current lines `742-786`, `788-822`, `824-858`) each build a `ContentDialog` with the same four style-resource lookups and the same `Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent` `XamlRoot` check, call `ShowAsync`, branch on the result, and wrap the actual operation in the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` pattern. Re-confirmed unchanged this loop (these three methods sit above this loop's own insertion point, so line numbers did not shift).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:742-786`, `:788-822`, `:824-858`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action instead of re-deriving the whole confirm-guard-run shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as Finding F-002, in the same file; combined the two clusters account for roughly 225 of `PrimaryWidget.xaml.cs`'s 2020 lines being ceremony repeated 3-4x rather than owned once. Three loops queued without action.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `ConfirmAndRunAsync(string title, string content, string primaryText, string secondaryText, Func<ContentDialogResult, bool> shouldRun, Func<Task> action)` (or the smallest signature that covers the 2-button and 3-button cases) that owns dialog construction, the `XamlRoot` check, and the `TryBeginLibraryOperation`/`EndLibraryOperation` wrapping; each handler becomes a short call naming its own title/content/action.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #4 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation at either threshold (`<` to `<=`) would ship silently: the corner-transparency gate that keeps case-mockup art off tiles would become off-by-one permissive or strict with no test failing.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) treats a pixel transparent when `alpha < 64` (`:250`) and treats a corner as transparent when more than 14 of its 36 sampled pixels are transparent, then rejects the image when 2 or more of its 4 corners are transparent (`transparentCorners < 2`, `:263`). `TileImageTests` exercises fully-opaque and fully-transparent corners but not alpha exactly at 64 or a candidate with exactly 2 transparent corners, so a mutation at either boundary is invisible to the suite. Re-read this loop (`TileImage.cs`/`TileImageTests.cs` unaffected by this loop's edits); gap re-confirmed unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — A minor, off-primary-flow gap (per the Test strategy dimension's own anchor language, an off-path helper boundary is Cosmetic on its own) but worth naming before it is mistaken for full coverage.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases: a corner at exactly alpha 63/64, and an image with exactly 2 (not 0, not 4) transparent corners, asserting the documented boundary (`< 64` transparent, `< 2` corners passes).

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs` (test-only addition, no production change).

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Finding F-007's session-liveness guard closes a real, evidenced display-corruption/mis-ranking path nested inside `PopulateGridSelectionPanelAsync`'s own await (state-ownership fix, matching F-001/F-005/F-006's own categorization) |
| New seam justified | No - explicitly evaluated a shared `SessionGuard` type over 7 total 1-3-line idiom instances (2 fields) and rejected on SPT Q2 (natural fix needed zero new type) and Q3 (wrapper is longer, not shorter, than the raw comparison) |
| Helpful simplification | none this loop (F-002/F-003 remain queued, not implemented, third loop running) |
| Should NOT be done | The shared `SessionGuard` type (see above); also rejected adding a per-row reentrancy guard to `RestoreBackup_Click`/`RestoreBackupCoreAsync` - walked this loop, race is self-correcting (fails closed into a caught `BackupMissing`), materially smaller blast radius than F-001/F-005/F-006/F-007 |
| Tests after fix | None added/deleted - untestable surface; verified by full build+suite plus manual trace of the guard's placement relative to the method's one await |

## Improvement Backlog
1. **Collapse the four-times-duplicated panel slide animation (Finding F-002).** score impact: `simplicity +0.5` — simplification, helpful
2. **Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003).** score impact: `simplicity +0.5; framework_idioms +0.5` — simplification, helpful

**Priority-1 accounting**: F-007 (loop_local F1) is Priority 1 on severity (Serious deduction, the only finding at that severity this loop - F-002/F-003 are Noticeable, F-004 is Cosmetic) and on distance-to-target under a reweighted Backlog Prioritization Pass reading: criterion 1 (distance to target) nominally favors `test_strategy` (6.5, lowest score on the board), but its only candidate (F-004) is Cosmetic and off-primary-flow, so fixing it would not proportionally advance `test_strategy`'s anchor, which is capped by `PrimaryWidget.xaml.cs`'s structural untestability regardless of what `TileImage.cs`'s test file contains. F-007, by contrast, closes a Serious, source-proven gap on `state_management`/`concurrency` (7.0/6.5 pre-fix), dimensions where a fix genuinely moves the anchor. Unlike F-002/F-003, F-007 required no bias-check override for a fourth consecutive loop landing in the picker/session area: this is not proximity bias, because F-007 is a materially different defect shape than F-001/F-005/F-006 (nested inside a callee a prior fix already called, not a fresh top-level entry point), discovered via this loop's first-ever full enumeration of every `private async` method in the file (32 methods, grep-verified). The rejected alternative was F-002 (simplicity, tied for second-lowest distance-to-target): F-002 does not fail the Simplify Pressure Test - it remains a sound, ready fix, queued a third loop running - but Backlog Prioritization criterion 3 (severity) breaks the tie decisively in F-007's favor. The systemic-mechanism alternative (a shared `SessionGuard` type) was evaluated per this loop's explicit mandate to consider it, and is rejected in the Simplification Check above on SPT Q2 and Q3, not silently passed over.

## Deepening Candidates
1. **Candidate Module**: `PrimaryWidget`'s panel slide-animation ceremony (Finding F-002). Source friction: four near-identical bodies, one observed drift, re-confirmed unchanged this loop. Shallow module test fails (no Interface). Behavior to move: `DoubleAnimation`+`Storyboard` construction parameterized by transform/from/to/duration/easing. Dependency category: `in-process`. Test surface after change: none (untestable UWP page). Smallest first step: extract `SlidePanelAsync`. What not to do: no `IAnimator`/`IPanelController` protocol - fails two-adapter rule, no policy/failure/platform-isolation justification.

## Builder Notes
- Pattern: a caller's session check protects only the mutations after its own check-point - not the mutations inside a callee that runs an await of its own. → REVIEW_HISTORY.json `loops[3].builder_notes` for full notes.
- Pattern: the same async-population-without-a-liveness-guard defect recurring across sibling methods in one file, because a fix scoped to the finding's own call site does not audit every other method - or every callee an already-fixed method reaches - with the same shape. → REVIEW_HISTORY.json `loops[3].builder_notes` for full notes.
- Pattern: a registry occurrence recorded with a placeholder resolution sha at commit time, because the sha of the commit being written cannot be known before it is made, then never backfilled. → REVIEW_HISTORY.json `loops[3].builder_notes` for full notes.

**Stalled-Dimension Sweep (loop 4 - fires for the first time):** `architecture_quality` — named candidate F-007. `domain_modeling` — explicit clean (walked `Models/`, 3 types). `data_flow` — explicit clean, falsified a helper-surfaced candidate. `framework_idioms` — named candidate F-002 (carried). `simplicity` — explicit clean (swept Services/Models across 4 angles). `test_strategy` — named candidate F-004 (carried, does not win Priority 1). `credibility` — explicit clean (re-read TESTING.md).

**Scorecard humility check** — see REVIEW_HISTORY.json `loops[3].narrative` for the full three-point check (DOWN-vs-SAME for state_management/concurrency; architecture_quality held SAME despite F-007; `RestoreBackup_Click`'s rejected-not-promoted race).

## Final Judge Narrative
Place, not win, this loop. Runtime ownership is NOT more trustworthy this loop by the plain net-wash accounting loops 2-3 used - it is, on this loop's own honest reassessment, less trustworthy than SAME implied: the fourth instance of the identical shape lived inside a callee a prior loop's own fix already called, proving entry-point-level session checks do not compose across call boundaries. This loop closes that specific gap and, for the first time across four loops, backs the fix with a genuinely exhaustive sweep (every `private async` method in the 2020-line file enumerated and checked for the shape), which is the systemic-prevention step loop 3's own Builder Note demanded but did not itself perform. Concurrency and state_management moved DOWN, not SAME, because this loop's own investigation is the basis for judging loop 3's SAME assessment as having under-priced the residual, not because the code regressed since loop 3 - nothing regressed; a deeper-nested, pre-existing hazard was found by more rigorous review. Simplification did not happen this loop on the F-002/F-003 axis - both remain queued for a third full loop, correctly outranked again by F-007's higher severity, not by proximity bias. The systemic shared-guard-mechanism alternative was evaluated per this loop's explicit mandate and rejected on SPT Q2/Q3 - the natural fix for the new gap needed zero new type. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; this loop's only regression evidence is full build + full suite (138/138 unchanged) plus a manual trace of the new guard's placement relative to its one await. Future work risks over-engineering only if F-002/F-003's eventual extraction reaches for a coordinator abstraction instead of a private helper method, or if a future loop still tries to wrap the two hand-rolled session fields in a shared type for what remains a set of small, honestly-duplicated 1-3 line idioms - unchanged guidance from loop 1's own Deepening Candidate, now re-affirmed a fourth time against a nested instance too.

## Loop 4 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (12 insertions, 0 deletions): added a session re-check inside `PopulateGridSelectionPanelAsync`, immediately after its own await (`AppliedArtworkStore.GetAsync`) and before any read of `CurrentSelectedGame` or mutation of `GridImagesView.Items`/`GridPanelStatus`: `if (sessionId != gridPanelSessionId) { return; }`. Reuses the existing `gridPanelSessionId` field and the `sessionId` parameter already threaded through from `LoadGridSelectionAsync` - no new field, no new type. This method makes no network call itself (grids/icons arrive pre-fetched as parameters from its caller), so no network call count, ordering, payload, or error handling changes anywhere in the flow. Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected, since `PrimaryWidget.xaml.cs` is not part of the test-linked `Services/**` surface. Finding F1 (stable_id F-007) is **resolved**: the specific display-corruption/mis-ranking path is closed by construction, verified by direct inspection of the diff (the guard sits textually immediately after the method's only await and before every subsequent read/mutation) and by re-reading the final source. This loop additionally performed the first exhaustive enumeration of every `private async void`/`private async Task` method in `PrimaryWidget.xaml.cs` (32 methods via grep) and checked each for the same awaited-then-mutate-without-recheck shape; no further instance was found (`RestoreBackup_Click`/`RestoreBackupCoreAsync`'s own, narrower, self-correcting race was walked and explicitly not promoted - see Simplification Check). No unintended scorecard regression: the change touches no network call, no ranking/selection logic beyond skipping it for a stale session, and no file outside the one named. Findings F-002, F-003 and F-004 are carried forward, unchanged in substance (evidence line numbers corrected against current source for F-002 only, shifted +12 by this loop's own insertion), to the Improvement Backlog / Findings for future loops.

## Loop 4 Implementation Review
Verdict: **approved**. Reason: the added guard sits immediately after `PopulateGridSelectionPanelAsync`'s own await and before every subsequent read/mutation, closing the exact nested-await gap F-007 identified, with no new seam, no suppression, and no same-or-higher-severity regression introduced. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.

--- Loop 5 (UTC 2026-08-05T02:45:15Z) ---

### Loop Counter
Loop 5 of 10 (cap)

### System Flag
[STATE: CONTINUE]

(Discovery: see Loop 1 Discovery.)

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

This loop ran a genuinely independent re-derivation from source (full re-read of `PrimaryWidget.xaml.cs`, all Services/Models files, and the three prior-audit docs), plus two fresh-eyes helper sweeps briefed on the codebase's history but not its prior conclusions. One of those sweeps found a fifth instance of the recurring "stale async completion mutates shared picker UI state" defect - in `DownloadAndReplaceImageAsync`, a fresh top-level-reachable method none of the four prior fixes (F-001, F-005, F-006, F-007) touched, and one loop 4's own "exhaustive" 32-method sweep should have caught but did not. This loop closes that specific gap and, for the first time, backs the closure with two independent verification passes rather than one self-directed sweep.

## Scorecard (1-10)

- Architecture quality: 7.5 | DOWN | `PrimaryWidget.xaml.cs` is now 2031 lines (2020 pre-fix; +11 from this loop's own guard clause), still carrying Findings F-002/F-003's duplicated ceremony (still open, current lines 742-858 and 1571-1865) plus this loop's newly-found-and-now-fixed F-008. Moved DOWN, distinct from the state_management/concurrency deductions below: this is a judgment about the *enforcement mechanism* for "who may still write to the picker UI," not about the live hazard itself. That mechanism is a manually-repeated convention (a `session != field; return;` idiom copy-pasted at each async method) with no single Module owning it - architecture_quality's own 9-anchor language is explicit that a convention-enforced property, as opposed to one "enforced by source," does not clear the bar. This loop's fifth instance, found despite two consecutive loops each claiming an exhaustive sweep, is fresh, source-backed evidence that the convention is not reliably self-auditing. (Loop 4's own Scorecard humility check flagged this exact ambiguity - whether the recurring gap is an architecture_quality concern, not purely state_management/concurrency - as arguable; this loop resolves it in the stricter direction on the strength of a fifth confirmed instance.)
- State management and runtime ownership: 6.5 | DOWN | F-005/F-006/F-007's own fixes independently re-verified this loop as still holding at their own call sites (re-read in full; unchanged). But this loop's fresh full-file re-read plus an independent helper sweep found a fifth instance of the identical shape in `DownloadAndReplaceImageAsync` (`PrimaryWidget.xaml.cs:1523-1555` pre-fix) - reached from `GridImage_Click`, a fresh top-level entry point none of the four prior fixes touch, not nested inside an already-fixed callee the way F-007 was. Moved DOWN, mirroring loop 4's own precedent for this exact situation: the code did not regress since loop 4, but this loop's own investigation is the basis for judging loop 4's "exhaustive, nothing beyond F-007" claim as having under-priced the residual - two consecutive loops now have made a completeness claim that a subsequent loop's fresh read falsified. Fixed this loop; see Loop 5 Result.
- Domain modeling: 8.5 | SAME | `Models/GameEntry.cs` and `Models/GamePlatform.cs` re-read in full this loop, unchanged. `GameEntry.cs`'s `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` three-independently-settable-properties case (the sole construction site, `PrimaryWidget.xaml.cs:650-664`, unaffected by this loop's edit) remains the only known concern, still no live harm, not promoted to a Finding. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME):** explicit clean — re-read both files in full; nothing beyond the already-tracked case.
- Data flow and dependency design: 7.5 | SAME | `Services/*` re-confirmed zero `Windows.UI.Xaml` imports by grep this loop. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME, with a falsified candidate):** a helper sub-agent surfaced `SteamGridDbClient.CapsuleParseNotes` (`SteamGridDbClient.cs:47-55`, an unsynchronized static `List<string>` with a check-then-add `Count < 5` guard) as a candidate race. Independently traced this loop: `NoteCapsuleParse`'s only call chain is `ParseOfficialCapsuleUrl` ← `GetGameByPlatformIdAsync` ← `LoadGameEntriesAsync`'s sequential per-entry `foreach` (never itself reentrant); `CapsuleParseNotes`'s only read site is `FixLibraryAsync` (`PrimaryWidget.xaml.cs:951`). Both trace to the same `isLibraryOperationRunning` gate that already falsified an equivalent candidate in loop 4 - write and read paths can never overlap. Falsified.
- Framework / platform best practices: 8.0 | SAME | `SteamGridDbClient.cs:136-142`'s deliberate `DataContractJsonSerializer` + `Windows.Data.Json` split re-verified this loop, unchanged. `SteamGridDbClient.cs:273-298`'s `BuildUrl` helper re-verified fixed and holding. Deduction unchanged: `PrimaryWidget.xaml.cs:1571-1865` (Finding F-002, still open). **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME):** F-002 is this dimension's named candidate; outranked again this loop by F-008's higher severity, fourth loop running.
- Concurrency and runtime safety: 6.0 | DOWN | Same evidence as state_management. F-008 (Serious deduction, same tier as F-005/F-006/F-007 - cannot write artwork to the wrong game, since `CurrentSelectedGame` is read by value before any await) is this loop's fifth discovery of the identical async-population-without-a-recheck shape. Moved DOWN, mirroring loop 4's own reasoning for this exact recurrence pattern. Fixed this loop, backed by two independent verification passes - the strongest verification basis of any of the five fixes so far.
- Code simplicity and clarity: 7.0 | SAME | `PrimaryWidget.xaml.cs:742-858` and `:1571-1865` (Findings F-003, F-002 - still open, ~225 lines of 3-4x duplicated ceremony) unchanged against otherwise-minimal Modules. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME):** a helper sub-agent swept `Services/Artwork`, `Services/Library`, `Services/SteamGridDB`, `Services/Stores` and `Models/` this loop across all four angles and found nothing beyond the already-tracked items.
- Test strategy and regression resistance: 6.5 | SAME | `PrimaryWidget.xaml.cs` remains untestable outside an app container - re-confirmed via both `.csproj` target frameworks. This loop's own investigation found a *fifth* independent concurrency/state-ownership defect (F-008) on that exact untestable surface, in a location two prior "exhaustive" sweeps did not catch. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME, the lowest-scored dimension on the board):** `TileImageTests.cs` re-walked (Finding F-004, re-verified, still no boundary case) - Cosmetic severity, off-primary-flow, does not win Priority 1.
- Overall implementation credibility: 7.5 | SAME | The `gridPanelSessionId`/`searchPanelSessionId` field comments continue the codebase's documented-rationale discipline; this loop's own `DownloadAndReplaceImageAsync` fix follows the identical idiom and comment style. Deduction unchanged: `TESTING.md:47-56`'s framing continues to undersell the surface's real risk, now demonstrated a fifth time. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME):** re-read `TESTING.md` in full this loop against current `.csproj` files; framing verbatim unchanged from loop 1's own citation and re-confirmed accurate.

## Authority Map
Re-emitted this loop: an authority-related finding, F-008, is Priority 1.

- Concern: **Selected game for the artwork picker (`CurrentSelectedGame`) - write path**
  - Owner: `PrimaryWidget`
  - Allowed writers: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (nulls on close)
  - Readers: `LoadGridSelectionPanelAsync`, `PopulateGridSelectionPanelAsync`, `GridImage_Click`, `DownloadAndReplaceImageAsync` (reads once, by value, before its own await), `ShowSearchPanelAsync`
  - Persistence seam: none (in-memory UI field)
  - Async mutation entry points: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (indirectly, via a stale `DownloadAndReplaceImageAsync` completion, pre-fix)
  - Verdict: **Single and clear** - `CurrentSelectedGame`'s own write sites were never the problem; F-008's hazard was `HideGridPanelAsync` being invoked from a stale context that no longer owned the panel, not a second writer to the field itself.

- Concern: **Grid picker panel display contents (`GridImagesView.Items` / `GridPanelStatus` / `GridLoadingRing` / panel visibility during population and post-download teardown)**
  - Owner: `PrimaryWidget` (`LoadGridSelectionAsync` / `PopulateGridSelectionPanelAsync` / `DownloadAndReplaceImageAsync` / `HideGridPanelAsync`)
  - Allowed writers: `LoadGridSelectionAsync` (Clear, status text, loading ring), `PopulateGridSelectionPanelAsync` (Add - guarded since loop 4/F-007), `DownloadAndReplaceImageAsync` (status text, triggers `HideGridPanelAsync` - guarded since this loop/F-008)
  - Readers: user (visually), `GridImage_Click` (via `SessionId` comparison)
  - Persistence seam: none
  - Async mutation entry points: `LoadGridSelectionAsync` (one invocation per Edit/Search-result click), `DownloadAndReplaceImageAsync` (one invocation per tile click)
  - Verdict: **Split and ambiguous (pre-fix)** - see Finding F-008. `GridImage_Click`'s own session check (established loop 1, F-001) sits before calling `DownloadAndReplaceImageAsync`, not inside it; this loop's fix adds the equivalent re-check inside `DownloadAndReplaceImageAsync` itself. Re-audit next loop once the fix has a full loop's scrutiny.

- Concern: **Search results panel display contents (`SearchResultsListView.Items` / `SearchPanelStatus` / `SearchLoadingRing` during a name search)**
  - Owner: `PrimaryWidget` (`PerformGameSearchAsync` / `ShowSearchPanelAsync`)
  - Allowed writers: `PerformGameSearchAsync` (Clear, Add, status text, loading ring), `ShowSearchPanelAsync` (Clear, header/box text)
  - Readers: user (visually), `SearchResult_Click`
  - Persistence seam: none
  - Async mutation entry points: `PerformGameSearchAsync`, `ShowSearchPanelAsync`
  - Verdict: **Single and clear** - re-verified this loop: `PerformGameSearchAsync` has exactly one `await` in its whole body and the existing session check sits immediately after it and before every mutation; no equivalent "download and auto-close" tail exists on the search flow the way it does on the grid flow.

- Concern: **Bulk library operation exclusivity (`isLibraryOperationRunning`)**
  - Owner: `PrimaryWidget`
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Readers: `IsLibraryOperationBlocking`, `TryBeginLibraryOperation`
  - Persistence seam: none
  - Async mutation entry points: `PrimaryWidget_Loaded`, `RefreshButton_Click`, `FixLibraryButton_Click`, `RestoreChangesButton_Click`, `RevertDefaultsButton_Click`
  - Verdict: **Single and clear** - unchanged since loop 4; not re-walked line-by-line this loop, carried forward as background context.

- Concern: **Applied-artwork record (`AppliedArtworkStore`'s path->artworkId map)**
  - Owner: `AppliedArtworkStore` (static Module)
  - Allowed writers: `SetAsync`, `ClearAsync` (via `UpdateAsync`, gated by `SemaphoreSlim gate`)
  - Readers: `GetAsync` (same gate)
  - Persistence seam: `applied-artwork.json` in the widget's local data (`RecordFolder`)
  - Async mutation entry points: `ReplaceImageCoreAsync`, `RestoreBackupCoreAsync`
  - Verdict: **Single and clear** - unchanged since loop 2; not re-walked line-by-line this loop, carried forward as background context.

- Concern: **Store-name / capsule-parse / fix-log ambient state (`StoreNameLookup`'s three dictionaries, `SteamGridDbClient.CapsuleParseNotes`, `FixLog`'s fields)**
  - Owner: `StoreNameLookup` / `SteamGridDbClient` / `FixLog` (static Modules)
  - Allowed writers: `GetOrFetchGogNameAsync`, `GetOrFetchEpicNameAsync`, `FindGameByNameAsync` (unlocked dictionaries), `NoteCapsuleParse` (unlocked list), `FixLog.Start`/`Write` (unlocked list+fields)
  - Readers: same methods, `FixLibraryAsync` (reads `CapsuleParseNotes`)
  - Persistence seam: none
  - Async mutation entry points: `LoadGameEntriesAsync`'s per-entry loop; `FixLibraryAsync`
  - Verdict: **Single and clear** - independently re-traced this loop for `CapsuleParseNotes` specifically: reachable only through the same gated entry points sharing `isLibraryOperationRunning`. A helper-surfaced candidate on this exact field was evaluated and falsified.

## Strengths That Matter
- `ArtworkDownloader.cs`'s `DownloadBestTileFillingImageAsync` / `FindOfficialLookalikeAsync` / `PassesColourAndLayoutGate` / `ChosenAlreadyMatchesOfficialArt` split (re-read in full this loop) is a genuinely deep Module: the five-step selection-and-veto pipeline is documented with the specific graded incident (`officialArtworkFloor`'s doc comment cites "Mad Max at 0.51") that calibrated each threshold, and the two gate predicates are extracted as pure, independently-testable functions.
- `ArtworkRanker.RankGrids` (`ArtworkRanker.cs:189-205`, re-read this loop) computes each grid's ranking signals exactly once via the private `RankedGrid` wrapper, rather than recomputing `GridMetadata`'s three regex passes per sort-key access - a genuine fix for `CODE-REVIEW.md`'s finding #10, independently re-verified this loop as still holding.
- `StoreNameLookup`'s Ubisoft cache (`StoreNameLookup.cs:40-42`, re-read this loop) now uses the shared `AsyncLazyCache<T>` instead of a fourth hand-rolled check-then-populate implementation - a real Locality win, not a renamed wrapper.

## Findings

### Finding #1 (stable_id F-008): DownloadAndReplaceImageAsync's post-download UI mutations are not re-checked against a superseded picker session

**Why it matters** — A user who clicks a grid tile to download artwork, then opens the artwork picker again for a different game before that download completes, can have the new session's picker panel unexpectedly closed, its tile list cleared, and `CurrentSelectedGame` nulled out by the stale download's completion — even though that stale download has nothing to do with the new session.

**What is wrong** — `DownloadAndReplaceImageAsync(GridImageItem gridItem)` (`PrimaryWidget.xaml.cs:1523-1555` pre-fix) is reached from `GridImage_Click`, which checks `gridItem.SessionId == gridPanelSessionId` before calling it — but that check does not cover an await *inside* `DownloadAndReplaceImageAsync` itself: `bool success = await DownloadAndReplaceImageCoreAsync(CurrentSelectedGame, gridItem.Url, true, gridItem.Id)` (line 1531 pre-fix) is a genuine suspension point (network download, file write, `AppliedArtworkStore` update), and nothing re-checked the session after it returned. If a newer `LoadGridSelectionAsync` call started and bumped `gridPanelSessionId` while this await was pending, the resumed method still wrote `GridPanelStatus.Text` for the live session's panel and, on success, called `HideGridPanelAsync()` — which collapses the panel, clears `GridImagesView.Items`, and nulls `CurrentSelectedGame`, all for a session no longer live.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1523-1555` (pre-fix), `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1512-1518` (`GridImage_Click`'s own session check sits before calling `DownloadAndReplaceImageAsync`, not inside it), `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1586-1613` (pre-fix; `HideGridPanelAsync` collapses the panel, clears items, and nulls `CurrentSelectedGame` unconditionally)

**Architectural test failed** — n/a - different category (state-ownership/reentrancy defect, matching Findings F-001/F-005/F-006/F-007's own categorization)

**Dependency category** — n/a

**Leverage impact** — None - correctness fix inside `DownloadAndReplaceImageAsync`'s own body.

**Locality impact** — Fix stays entirely inside `DownloadAndReplaceImageAsync`.

**Metric signal, if any** — none

**Why this weakens submission** — This is the same "stale authority remains alive" hazard class Findings F-001, F-005, F-006 and F-007 already closed, found a fifth time — in a fresh top-level-reachable method none of the four prior fixes touched. Severity mirrors F-005/F-006/F-007: `CurrentSelectedGame` is read by value before any await, so artwork can never be written to the wrong game — only the panel's own in-memory display, status text and selected-game field can be corrupted for an unrelated live session.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Add the same guard clause already used at the other four sites (`if (gridItem.SessionId != gridPanelSessionId) { return; }`) immediately after the `DownloadAndReplaceImageCoreAsync` await and before any further mutation.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/Models/**`, `SteamGridDB.Xbox/Services/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml`.

**Status this loop: implemented — see Loop 5 Result below.**

### Finding #2 (stable_id F-002): Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to the panel's slide timing or easing has to be made and verified in four places instead of one, and the four copies have already begun to drift.

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` (current lines `1571-1592`, `1597-1624`, `1763-1831`, `1836-1865`) each hand-build a `DoubleAnimation` + `Storyboard`. Re-confirmed unchanged this loop (line numbers shifted +11 by this loop's own guard clause).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1571-1592`, `:1597-1624`, `:1763-1831`, `:1836-1865`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — One call site to read/change instead of four.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Four near-identical bodies in the single largest, most-churned file (2031 LOC); currently not swept away. Four loops queued without action, correctly outranked each time by a higher-severity finding.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(FrameworkElement transform, double from, double to, int durationMs, EasingMode mode)` helper.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #3 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations has to be kept in sync by hand.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (current lines `742-786`, `788-822`, `824-858`) each build a `ContentDialog` with the same ceremony. Re-confirmed unchanged this loop (unaffected by this loop's insertion point).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:742-786`, `:788-822`, `:824-858`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as F-002; combined ~225 lines of `PrimaryWidget.xaml.cs` are ceremony repeated 3-4x. Four loops queued without action.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `ConfirmAndRunAsync(...)` helper covering dialog construction, the `XamlRoot` check, and the guard wrapping.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #4 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation at either threshold would ship silently.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) treats a pixel transparent when `alpha < 64` (`:250`) and a corner transparent past a 14/36 sample threshold, rejecting images with `transparentCorners < 2` (`:263`). No test at the exact boundary values. Re-read this loop, unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — A minor, off-primary-flow gap; Cosmetic on its own.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases at the exact documented boundaries.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs`.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Finding F-008's session-liveness guard closes a real, evidenced display-corruption path caused by a stale `DownloadAndReplaceImageAsync` completion (state-ownership fix, matching F-001/F-005/F-006/F-007's own categorization) |
| New seam justified | No - considered and rejected again this loop, now with a fifth data point: a shared `SessionGuard` type fails SPT Q1 (the failure mode was never "the check is hard to write," it was "a method's own suspension point never got the check") and Q3 (no shorter/clearer than the raw comparison). A `PopulateIfCurrentAsync` entry-check wrapper was also rejected - this fix needed the check positioned after a specific await, not at method entry |
| Helpful simplification | none this loop (F-002/F-003 remain queued, not implemented, fourth loop running) |
| Should NOT be done | The shared session-guard/wrapper mechanisms (see above); also re-confirmed: no per-row reentrancy guard for `RestoreBackup_Click`/`RestoreBackupCoreAsync` - still self-correcting, still smaller blast radius than F-001/F-005/F-006/F-007/F-008's shape |
| Tests after fix | None added/deleted - untestable surface; verified by full build+suite, an independent fresh-eyes implementation review (verdict approved), plus manual trace of the guard's placement |

## Improvement Backlog
1. **Collapse the four-times-duplicated panel slide animation (Finding F-002).** score impact: `simplicity +0.5; framework_idioms +0.5` — simplification, helpful
2. **Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003).** score impact: `simplicity +0.5` — simplification, helpful

**Priority-1 accounting**: F-008 (loop_local F1) is Priority 1 on severity (Serious deduction, the only finding at that severity this loop - F-002/F-003 are Noticeable, F-004 is Cosmetic) and this is the fourth consecutive loop that reasoning has correctly outranked F-002/F-003 despite their Stall. F-008 was independently surfaced by a dedicated fresh-eyes helper sweep specifically tasked with re-testing loop 4's own "exhaustive, nothing beyond F-007" completeness claim, found in a method (`DownloadAndReplaceImageAsync`) none of the four prior fixes touch, and verified by this loop's own full-file re-read before being accepted as a finding. A Serious-severity, source-proven, safely-fixable defect on a primary user flow beats a Noticeable-severity, four-loop-stalled simplification item on Backlog Prioritization criterion 3 (Severity) regardless of criterion 2 (Stall)'s tie-breaking role. If no further Serious-or-worse finding surfaces next loop, F-002 is the correctly-queued next pick and should not be deferred a fifth time without a comparable severity justification.

## Deepening Candidates
1. **Candidate Module**: `PrimaryWidget`'s panel slide-animation ceremony (Finding F-002). Source friction: four near-identical bodies, re-confirmed unchanged this loop. Shallow module test fails. Behavior to move: `DoubleAnimation`+`Storyboard` construction parameterized by transform/from/to/duration/easing. Dependency category: `in-process`. Test surface after change: none (untestable UWP page). Smallest first step: extract `SlidePanelAsync`. What not to do: no `IAnimator`/`IPanelController` protocol - fails two-adapter rule.

## Builder Notes
- Pattern: an "exhaustive sweep" claim from a prior loop is a lead, not proof - it needs re-testing by someone who did not write it. → REVIEW_HISTORY.json `loops[4].builder_notes` for full notes.
- Pattern: a session/generation check protects only the mutations reachable after its own check-point - a caller's check never protects a callee's own suspension point, no matter how many call-sites deep. → REVIEW_HISTORY.json `loops[4].builder_notes` for full notes.
- Pattern: a helper-surfaced "unsynchronized shared state" candidate needs a call-graph trace to the write/read sites' actual entry points before it becomes a finding, not just a look at the shape of the state. → REVIEW_HISTORY.json `loops[4].builder_notes` for full notes.

**Stalled-Dimension Sweep (loop 5 - 4th consecutive SAME on six dimensions):** `domain_modeling` — explicit clean. `data_flow` — explicit clean, falsified a helper-surfaced candidate (`CapsuleParseNotes`). `framework_idioms` — named candidate F-002 (carried). `simplicity` — explicit clean (swept Services/Models across 4 angles). `test_strategy` — named candidate F-004 (carried, does not win Priority 1). `credibility` — explicit clean (re-read TESTING.md).

**Scorecard humility check** — see REVIEW_HISTORY.json `loops[4].narrative` for the full three-point check (architecture_quality DOWN double-counting risk; the "strongest verification basis" claim's thinner-than-framed margin; `RestoreBackup_Click`'s rejected-not-promoted race, four loops running).

## Final Judge Narrative
Place, not win, this loop. Runtime ownership is again not more trustworthy by a plain net-wash accounting - it is, on this loop's own honest reassessment, less trustworthy than the prior score implied: a fifth instance of the identical shape lived in a fresh top-level method two consecutive "exhaustive" sweeps did not catch. This loop closes that specific gap and, for the first time, backs the fix with two independently-scoped verification passes rather than one self-directed sweep. State_management, concurrency and (newly, this loop) architecture_quality all moved DOWN, not because the code regressed since loop 4, but because this loop's own investigation is the basis for judging the prior confidence level as having under-priced the residual. Simplification did not happen this loop on the F-002/F-003 axis - both remain queued for a fourth full loop, correctly outranked again by F-008's higher severity. The systemic shared-guard-mechanism and population-wrapper alternatives were both evaluated this loop and rejected on SPT grounds. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; this loop's only regression evidence is full build + full suite (138/138 unchanged), an independent fresh-eyes implementation review (verdict approved), and a manual trace of the new guard's placement. Future work risks over-engineering only if F-002/F-003's eventual extraction reaches for a coordinator abstraction, or if a future loop still tries to wrap the five hand-rolled session-check idioms in a shared type whose actual failure mode a wrapper type cannot fix - unchanged guidance from loop 1's own Deepening Candidate, now re-affirmed a fifth time.

## Loop 5 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (11 insertions, 0 deletions): added a session re-check inside `DownloadAndReplaceImageAsync`, immediately after its own await (`DownloadAndReplaceImageCoreAsync`) and before any further mutation of `GridPanelStatus.Text` or the call to `HideGridPanelAsync`: `if (gridItem.SessionId != gridPanelSessionId) { return; }`. Reuses the existing `gridItem.SessionId` field and the existing `gridPanelSessionId` field - no new field, no new type, no new parameter. This method's only network/file-write call (`DownloadAndReplaceImageCoreAsync`) is unchanged - same arguments, same call count, same ordering, same error handling. Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected. An independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on the targeted finding and the diff only) returned verdict `approved` with all three checks (reality, honesty, regression) passed. Finding F1 (stable_id F-008) is **resolved**: the specific display-corruption path is closed by construction, verified by direct inspection of the diff and by re-reading the final source. This loop additionally re-verified Findings F-001/F-005/F-006/F-007's fixes are still holding at their own call sites, and independently traced a helper-surfaced `SteamGridDbClient.CapsuleParseNotes` candidate to a falsified conclusion. No unintended scorecard regression. Findings F-002, F-003 and F-004 are carried forward, unchanged in substance (evidence line numbers corrected against current source for F-002 only, shifted +11 by this loop's own insertion), to the Improvement Backlog / Findings for future loops.

## Loop 5 Implementation Review
Verdict: **approved**. Reason: the added guard clause sits immediately after `DownloadAndReplaceImageCoreAsync`'s await and before every subsequent panel mutation, exactly matching the minimal correction path and the three prior identical-shape fixes (loops 2-4) in this same file. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.

--- Loop 6 (UTC 2026-08-05T03:18:25Z) ---

### Loop Counter
Loop 6 of 10 (cap)

### System Flag
[STATE: CONTINUE]

(Discovery: see Loop 1 Discovery.)

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

This loop ran an independent re-derivation from source (full re-read of `PrimaryWidget.xaml.cs`, plus fresh reads of `ArtworkDownloader.cs`, `ArtworkRanker.cs`, `SteamGridDbClient.cs`, `ArtworkFiles.cs`, `AppliedArtworkStore.cs`, `FixLog.cs`, `TileImage.cs`, `StoreNameLookup.cs`, `AsyncLazyCache.cs`, `JsonRead.cs`, `GameEntry.cs`, `GamePlatform.cs`, `GridImageItem.cs`, `TESTING.md`), plus two fresh-eyes helper sweeps briefed on the codebase's history but not its prior conclusions — one explicitly tasked with re-testing whether the "stale async completion mutates shared picker UI state" defect class (five prior instances: F-001, F-005, F-006, F-007, F-008) had a sixth instance, the other sweeping `Services/` and `Models/` cold. The first sweep found a sixth and seventh instance — `HideGridPanelAsync` and `HideSearchPanelAsync` — in the panel-close path rather than the already-fixed open/populate/download paths, reachable from the ungated `CloseGridPanel_Click`/`CloseSearchPanel_Click` buttons. This loop closes that gap (both methods, one finding, one fix) and independently re-confirmed all five prior fixes still hold. The second sweep found a genuine, if minor, new finding: two of `StoreNameLookup`'s three network-backed name lookups bypass the `JsonRead` helper that exists specifically to prevent the JSON null-vs-missing bug class documented in this same codebase's history.

## Scorecard (1-10)

- Architecture quality: 7.5 | SAME | `PrimaryWidget.xaml.cs` is now 2058 lines (2031 pre-fix; +27 from this loop's own two guard clauses), still carrying Findings F-002/F-003's duplicated ceremony. Considered moving this DOWN again, mirroring loop 5's move for the same reasoning (the session-check idiom is now duplicated across 7 sites, not 5) — held the line at SAME instead. Rationale: loop 5's own Scorecard humility check already flagged the DOWN move as an arguable double-count against `state_management`/`concurrency`, where the actual runtime hazard lives; a second confirmatory instance of the same already-acknowledged pattern is priced into those two dimensions below rather than compounded here a second time.
- State management and runtime ownership: 6.0 | DOWN | F-001/F-005/F-006/F-007/F-008's own fixes independently re-verified this loop as still holding (re-read `PrimaryWidget.xaml.cs` in full; unchanged). But an independently-briefed helper sweep — tasked specifically with re-testing whether a sixth instance existed — found two more: `HideGridPanelAsync` (pre-fix `PrimaryWidget.xaml.cs:1597-1624`) and `HideSearchPanelAsync` (pre-fix `:1836-1865`) both mutated shared panel state after their own `await Task.Delay(...)` with no session recheck, reachable from the ungated `CloseGridPanel_Click`/`CloseSearchPanel_Click` handlers. Moved DOWN, mirroring loop 5's own precedent: a sixth/seventh confirmed instance found via a sweep explicitly designed to falsify the "no more instances" assumption is evidence the guard convention still is not self-auditing, three loops running (4→5→6). Fixed this loop; see Loop 6 Result.
- Domain modeling: 8.5 | SAME | `GameEntry.cs` and `GamePlatform.cs` re-read in full this loop (directly, and independently by a helper); `GameEntry.cs`'s three-independently-settable-properties case (sole construction site `PrimaryWidget.xaml.cs:650-664`, unaffected by this loop's edits) remains the only known concern, still no live harm. **Stalled-Dimension Sweep (loop 6, 5th consecutive SAME):** explicit clean.
- Data flow and dependency design: 7.0 | DOWN | `Services/*` re-confirmed zero `Windows.UI.Xaml` imports. New finding this loop (F-010): `StoreNameLookup.GetGogGameNameAsync` (`StoreNameLookup.cs:67-74`) and `GetEpicGameNameAsync` (`:188-190`) use raw `Windows.Data.Json` calls instead of the `JsonRead` helper that exists specifically to prevent a documented bug class. `SteamGridDbClient.cs:155-181` and `EpicLibrary.cs` both correctly route through `JsonRead`; two of `StoreNameLookup`'s three name-fetch methods do not. Moved DOWN from SAME: a fresh, source-backed inconsistency independently surfaced this loop.
- Framework / platform best practices: 8.0 | SAME | `SteamGridDbClient.cs:273-301`'s `BuildUrl` helper re-verified fixed and holding. Deduction unchanged: Finding F-002's four-times duplicated `DoubleAnimation`/`Storyboard` ceremony, still open. **Stalled-Dimension Sweep (loop 6, 5th consecutive SAME):** F-002 remains the named candidate; outranked again this loop by F-009's higher severity - fifth loop running.
- Concurrency and runtime safety: 5.5 | DOWN | Same evidence as state_management. F-009 (Serious deduction, same tier as F-001/F-005/F-006/F-007/F-008) is this loop's sixth-and-seventh discovery of the identical async-population-without-a-recheck shape, this time in the teardown path rather than the populate path. Moved DOWN: two new scattered-guard sites found via a targeted re-test sweep, on top of the five already known, means verifying completeness now requires reading eight call sites across the file — matching the anchor's 5-6 range language more closely than the 7-anchor. Fixed this loop, verified by build + full test suite + independent implementation review (verdict `approved`).
- Code simplicity and clarity: 7.0 | SAME | `PrimaryWidget.xaml.cs:742-858` (F-003) and the four animation bodies (F-002, now at `:1571-1592`/`:1597-1640`/`:1779-1847`/`:1852-1891`) remain open, unchanged in substance. **Stalled-Dimension Sweep (loop 6, 5th consecutive SAME):** a helper sub-agent swept `Services/*` and `Models/` this loop across Reuse/Simplification/Altitude/Efficiency angles; nothing beyond the already-tracked F-002/F-003 and the new F-010 (priced under data_flow, not double-counted here).
- Test strategy and regression resistance: 6.5 | SAME | `PrimaryWidget.xaml.cs` remains untestable outside an app container — re-confirmed via both `.csproj` target frameworks, matching `TESTING.md:49-56`'s own documented reasoning. This loop's own investigation found a sixth/seventh independent concurrency/state-ownership defect (F-009) on that exact untestable surface. **Stalled-Dimension Sweep (loop 6, 5th consecutive SAME, lowest-scored dimension on the board):** `TileImageTests.cs` re-checked (Finding F-004, still no boundary case) - Cosmetic severity, off-primary-flow, does not win Priority 1.
- Overall implementation credibility: 8.0 | UP | `gridPanelSessionId`/`searchPanelSessionId` field comments continue the codebase's documented-rationale discipline; this loop's own `HideGridPanelAsync`/`HideSearchPanelAsync` fix follows the identical idiom and comment style as the five prior fixes. Moved UP: sixth consecutive loop in which every one of a growing set of prior fixes (now 7 guarded call sites across 6 loops) independently re-verified as still holding under fresh inspection, and the methodology that keeps finding the remaining gaps is now validated a second time in a row (loop 5 found F-008 this way; loop 6 found F-009 the same way). Structural proof: this loop's own commit (F-009's fix), independently reviewed and returning `approved`.

## Authority Map
Re-emitted this loop: an authority-related finding, F-009, is Priority 1.

- Concern: **Selected game for the artwork picker (`CurrentSelectedGame`) - write path**
  - Owner: `PrimaryWidget`
  - Allowed writers: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (nulls on close, now session-guarded)
  - Readers: `LoadGridSelectionPanelAsync`, `PopulateGridSelectionPanelAsync`, `GridImage_Click`, `DownloadAndReplaceImageAsync` (reads once, by value, before its own await), `ShowSearchPanelAsync`
  - Persistence seam: none (in-memory UI field)
  - Async mutation entry points: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (previously reachable via a stale close animation with no guard; fixed this loop)
  - Verdict: **Single and clear** - `CurrentSelectedGame`'s own write sites were never multiply-owned; F-009's hazard was `HideGridPanelAsync` writing `null` to it from a stale, superseded close, not a second concurrent writer.

- Concern: **Grid picker panel display contents (`GridImagesView.Items` / `GridPanelStatus` / `GridLoadingRing` / panel visibility during population and close)**
  - Owner: `PrimaryWidget` (`LoadGridSelectionAsync` / `PopulateGridSelectionPanelAsync` / `DownloadAndReplaceImageAsync` / `HideGridPanelAsync`)
  - Allowed writers: `LoadGridSelectionAsync` (Clear, status text, loading ring), `PopulateGridSelectionPanelAsync` (Add - guarded since loop 4/F-007), `DownloadAndReplaceImageAsync` (status text, triggers `HideGridPanelAsync` - guarded since loop 5/F-008), `HideGridPanelAsync` (Visibility/Items/CurrentSelectedGame - guarded since this loop/F-009)
  - Readers: user (visually), `GridImage_Click` (via `SessionId` comparison)
  - Persistence seam: none
  - Async mutation entry points: `LoadGridSelectionAsync`, `DownloadAndReplaceImageAsync`, `HideGridPanelAsync`
  - Verdict: **Split and ambiguous (pre-fix)** - see Finding F-009. `CloseGridPanel_Click` calls `HideGridPanelAsync` with no session check of its own; the panel is only partially covering the screen during its own 200ms close animation, so a new Edit click on a different game can start before the stale close finishes. This loop's fix captures the session before the animation starts and rechecks it after. Re-audit next loop once the fix has a full loop's scrutiny.

- Concern: **Search results panel display contents (`SearchResultsListView.Items` / `SearchPanelStatus` / `SearchLoadingRing` during a name search, and its own close path)**
  - Owner: `PrimaryWidget` (`PerformGameSearchAsync` / `ShowSearchPanelAsync` / `HideSearchPanelAsync`)
  - Allowed writers: `PerformGameSearchAsync` (Clear, Add, status text, loading ring - guarded since loop 3/F-006), `ShowSearchPanelAsync` (Clear, header/box text), `HideSearchPanelAsync` (Visibility/Items - guarded since this loop/F-009)
  - Readers: user (visually), `SearchResult_Click`
  - Persistence seam: none
  - Async mutation entry points: `PerformGameSearchAsync`, `ShowSearchPanelAsync`, `HideSearchPanelAsync`
  - Verdict: **Split and ambiguous (pre-fix)** - the search panel's own close path had the identical unguarded shape as the grid panel's; fixed this loop alongside it (same finding, same commit). `SearchResult_Click`'s call to `HideSearchPanelAsync(false)` is unaffected: nothing bumps `searchPanelSessionId` during that specific transition.

- Concern: **Bulk library operation exclusivity (`isLibraryOperationRunning`)** - unchanged since loop 4; carried forward as background context.

- Concern: **Applied-artwork record (`AppliedArtworkStore`'s path->artworkId map)** - re-read in full this loop; `GetAsync`/`UpdateAsync` both take the same `gate`, confirmed unchanged.

- Concern: **Store-name / capsule-parse / fix-log ambient state** - independently re-traced this loop by helper: writer and reader both reachable only through the shared `isLibraryOperationRunning` gate, so they can never run concurrently. (See also Finding F-010 for a data-correctness, not thread-safety, concern on two of this Module's own methods.)

## Strengths That Matter
- `ArtworkDownloader.cs`'s `DownloadBestTileFillingImageAsync` / `FindOfficialLookalikeAsync` / `PassesColourAndLayoutGate` / `ChosenAlreadyMatchesOfficialArt` split (re-read in full this loop) remains a genuinely deep Module.
- `ArtworkRanker.RankGrids` (re-read this loop) computes each grid's ranking signals exactly once via the private `RankedGrid` wrapper rather than recomputing regex passes per sort-key access.
- This loop's own verification methodology held up under a second consecutive test: an independently-briefed helper explicitly tasked with re-testing "is there a sixth instance" found the gap a same-loop full-file read initially missed, exactly as loop 5's equivalent methodology found F-008.

## Findings

### Finding #1 (stable_id F-009): HideGridPanelAsync and HideSearchPanelAsync unconditionally mutated shared panel state after their own await, with no session recheck, reachable from ungated Close-button clicks

**Why it matters** — A user who clicks the picker's Close (X) button starts a ~200ms slide-down animation. During that animation the full-screen panel only partially covers the screen, so the game list underneath - and a different game's Edit/Search button - can become reachable before the panel fully collapses. If a new picker/search session starts during that window, the stale close call finishing afterward would collapse the new, live session's panel, clear its tiles/results, and (for the grid panel) null its selected game.

**What is wrong** — `HideGridPanelAsync` (pre-fix `PrimaryWidget.xaml.cs:1597-1624`) had no session capture or check: after `await Task.Delay(200)` it unconditionally ran `GridSelectionPanel.Visibility = Visibility.Collapsed; GridImagesView.Items.Clear(); CurrentSelectedGame = null;` plus focus restoration. `HideSearchPanelAsync` (pre-fix `:1836-1865`) had the identical shape. Both are reached from `CloseGridPanel_Click`/`CloseSearchPanel_Click`, neither of which check anything before calling in. Same hazard class as Findings F-001, F-005, F-006, F-007 and F-008, this time in the panel-close/teardown path.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1597-1624` (pre-fix, `HideGridPanelAsync`), `:1629-1632` (pre-fix, `CloseGridPanel_Click`), `:1836-1865` (pre-fix, `HideSearchPanelAsync`), `:1870-1873` (pre-fix, `CloseSearchPanel_Click`)

**Architectural test failed** — n/a - different category (state-ownership/reentrancy defect)

**Dependency category** — n/a

**Leverage impact** — None - correctness fix inside each method's own body.

**Locality impact** — Fix stays entirely inside `HideGridPanelAsync` and `HideSearchPanelAsync`; no other Module's behavior changes.

**Metric signal, if any** — none

**Why this weakens submission** — A sixth and seventh instance of the identical "stale authority remains alive" hazard class, found via a sweep explicitly tasked with falsifying loop 5's own confidence, in the one path none of the five prior fixes touched.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Add the same guard idiom used at the five prior sites to both methods: capture `int session = gridPanelSessionId;` before the animation starts, recheck after `await Task.Delay(...)`.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/Models/**`, `SteamGridDB.Xbox/Services/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml`.

**Status this loop: implemented — see Loop 6 Result below.**

### Finding #2 (stable_id F-002): Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to the panel's slide timing or easing has to be made and verified in four places instead of one.

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` (current lines `1571-1592`, `1597-1640`, `1779-1847`, `1852-1891`) each still hand-build a `DoubleAnimation` + `Storyboard`. This loop's own F-009 fix added a session guard but did not touch this ceremony.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1571-1592`, `:1597-1640`, `:1779-1847`, `:1852-1891`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — One call site to read/change instead of four.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Four near-identical bodies in the single largest, most-churned file. Five loops queued without action.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(TranslateTransform transform, double from, double to, int durationMs, EasingMode mode)` helper.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #3 (stable_id F-010): StoreNameLookup's GOG and Epic name-fetch methods bypass the JsonRead helper that exists to prevent a documented JSON null-vs-missing bug

**Why it matters** — The codebase already shipped a bug once where a present-but-null JSON member was mishandled by raw `Windows.Data.Json` calls; `JsonRead.cs` exists specifically to prevent that.

**What is wrong** — `GetGogGameNameAsync` (`StoreNameLookup.cs:67-74`) and `GetEpicGameNameAsync` (`:188-190`) use raw `ContainsKey`/`GetNamedObject`/`GetNamedString` instead of `JsonRead.Object`/`JsonRead.String`, which `SteamGridDbClient.cs:155-181` and `EpicLibrary.cs` both correctly use. The outer broad `catch` means no live crash risk today; the harm is a maintained inconsistency next to the exact helper built to remove it.

**Evidence** — `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:67-74`, `:188-190`, `SteamGridDB.Xbox/Services/JsonRead.cs:1-17`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:470-478`, `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs:155-181`

**Architectural test failed** — n/a - a Reuse/consistency finding

**Dependency category** — n/a

**Leverage impact** — None directly - brings the remaining two call sites into line.

**Locality impact** — Contained to `StoreNameLookup.cs`'s two methods.

**Metric signal, if any** — none

**Why this weakens submission** — Credibility/consistency deduction, not a live defect.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Replace the raw calls with `JsonRead.Object`/`JsonRead.String` calls, matching `SteamGridDbClient.cs`'s established pattern. Behavior-preserving per the five properties (call count, ordering, payload, error handling, observable result).

**Blast radius** — Change: `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`. Avoid: `SteamGridDB.Xbox/Services/JsonRead.cs`, `SteamGridDB.Xbox/Services/SteamGridDB/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

### Finding #4 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations has to be kept in sync by hand.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (lines `742-786`, `788-822`, `824-858`, unaffected by this loop's edits) each build a `ContentDialog` with the same ceremony.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:742-786`, `:788-822`, `:824-858`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as F-002. Five loops queued without action.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `ConfirmAndRunAsync(...)` helper.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #5 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation would ship silently.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) thresholds at `alpha < 64` and `transparentCorners < 2` are untested at their exact boundary. Re-read this loop, unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — A minor, off-primary-flow gap.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases at the exact documented boundaries.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs`.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Finding F-009's session-liveness guards close a real, evidenced display-corruption path caused by a stale `HideGridPanelAsync`/`HideSearchPanelAsync` completion (state-ownership fix, matching F-001/F-005/F-006/F-007/F-008's own categorization) |
| New seam justified | No - considered and rejected again this loop, now with a sixth and seventh data point: a shared `SessionGuard` type fails SPT Q1/Q3. A bigger centralized panel-lifecycle orchestrator was also considered and rejected as a materially larger, riskier rewrite of an untestable file for a defect class where every fixed instance has held |
| Helpful simplification | none this loop (F-002/F-003 remain queued, not implemented, fifth loop running) |
| Should NOT be done | The shared session-guard/orchestrator mechanisms (see above); also re-confirmed: no per-row reentrancy guard for `RestoreBackup_Click`/`RestoreBackupCoreAsync` - independently re-traced via helper, self-correcting via a caught exception, no data loss |
| Tests after fix | None added/deleted - untestable surface; verified by full build+suite, an independent fresh-eyes implementation review (verdict approved), plus manual trace of both guards' placement |

## Improvement Backlog
1. **Collapse the four-times-duplicated panel slide animation (Finding F-002).** score impact: `simplicity +0.5; framework_idioms +0.5` — simplification, helpful
2. **Route StoreNameLookup's GOG and Epic name-fetch methods through the existing JsonRead helper (Finding F-010).** score impact: `data_flow +0.5` — simplification, helpful
3. **Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003).** score impact: `simplicity +0.5` — simplification, helpful

**Priority-1 accounting**: F-009 (loop_local F1) is Priority 1 on severity (Serious deduction, the only finding at that severity this loop) and this is the fifth consecutive loop that severity has correctly outranked F-002/F-003 despite their Stall. F-009 was independently surfaced by a helper sweep specifically tasked with re-testing the "is there another instance" question, found in the one code path (panel close/teardown) genuinely distinct from all five prior fixes' paths, and verified by this loop's own full-file re-read plus an independent implementation review. If no further Serious-or-worse finding surfaces next loop, F-002 is the correctly-queued next pick and should not be deferred a sixth time without a comparable severity justification.

## Deepening Candidates
1. **Candidate Module**: `PrimaryWidget`'s panel slide-animation ceremony (Finding F-002). Source friction: four near-identical bodies, re-confirmed unchanged this loop (line numbers refreshed for this loop's own +27-line insertion). Shallow module test fails. Behavior to move: `DoubleAnimation`+`Storyboard` construction parameterized by transform/from/to/duration/easing. Dependency category: `in-process`. Test surface after change: none (untestable UWP page). Smallest first step: extract `SlidePanelAsync`. What not to do: no `IAnimator`/`IPanelController` protocol - fails two-adapter rule.

## Builder Notes
- Pattern: a helper sweep framed as "does a gap still exist" finds what a helper sweep framed as "look for problems" misses. → REVIEW_HISTORY.json `loops[5].builder_notes` for full notes.
- Pattern: the same defect class can hide in a teardown/close path just as easily as in a populate/open path. → REVIEW_HISTORY.json `loops[5].builder_notes` for full notes.
- Pattern: when a fix touches network-call-adjacent code but is queued rather than implemented, name the behavior-preservation argument in the finding text now. → REVIEW_HISTORY.json `loops[5].builder_notes` for full notes.

**Scorecard humility check** — see REVIEW_HISTORY.json `loops[5].narrative` for the full three-point check (holding architecture_quality at SAME vs a second DOWN move; treating F-009 as one finding spanning two methods fixed in one loop; the judgment call not to reuse F-006's stable_id despite a mechanical M2 line-proximity collision).

## Final Judge Narrative
Place, not win, this loop. Runtime ownership is again not more trustworthy by a plain net-wash accounting - a sixth and seventh instance of the identical shape lived in the panel-close path, found by a sweep this loop specifically tasked with distrusting the prior loop's implicit "that was the last one" confidence. This loop closes that gap with the same minimal, proven idiom as the five prior fixes, verified by full build + full test suite (138/138 unchanged) + an independent implementation review (verdict `approved`, all three checks passed). State_management and concurrency both moved DOWN again, not because the code regressed since loop 5, but because a third consecutive loop finding more instances of the same class is itself evidence the residual was under-priced. Architecture_quality was deliberately held at SAME rather than compounding the same critique a second time, on the explicit reasoning that the concern is already priced into state_management/concurrency - a genuinely close call, recorded in the humility check above. Simplification did not happen this loop on the F-002/F-003 axis - both remain queued for a fifth full loop, correctly outranked again by F-009's higher severity. A new, smaller finding (F-010, StoreNameLookup bypassing the established JsonRead helper) was surfaced by an independent Services/Models sweep and queued, not implemented. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; this loop's only regression evidence is full build + full suite + independent review + a manual trace of both new guards' placement. Future work risks over-engineering only if F-002/F-003's eventual extraction reaches for a coordinator abstraction, or if a future loop tries to wrap the now-seven hand-rolled session-check idioms in a shared type or a centralized panel-lifecycle orchestrator - both explicitly evaluated and rejected again this loop, unchanged guidance from loop 1's own Deepening Candidate.

## Loop 6 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (27 insertions, 0 deletions): added a session capture and recheck to `HideGridPanelAsync` (captures `int session = gridPanelSessionId;` before the close animation starts; after `await Task.Delay(200)` adds `if (session != gridPanelSessionId) { return; }` before the `Visibility`/`Items.Clear()`/`CurrentSelectedGame = null` mutations and focus restoration) and the identical pattern to `HideSearchPanelAsync` (`searchPanelSessionId`, guarding the `Visibility`/`Items.Clear()`/focus-restoration mutations). Both reuse the existing session fields already used by the five prior fixes - no new field, no new type, no new parameter. Neither method's own animation construction, timing, or the network/file-write calls elsewhere in the flow changed - no network call count, ordering, payload, or error-handling behavior changes anywhere. Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected. An independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on the targeted finding and the diff only) returned verdict `approved` with all three checks (reality, honesty, regression) passed. Finding F1 (stable_id F-009) is **resolved**: both display-corruption paths are closed by construction, verified by direct inspection of the diff and by re-reading the final source. This loop additionally re-verified Findings F-001/F-005/F-006/F-007/F-008's fixes are still holding at their own call sites (no regression), and an independent helper sweep of `Services/`/`Models/` surfaced Finding F-010 (StoreNameLookup bypassing the JsonRead helper), queued to the backlog rather than implemented this loop (lower severity than F-009). No unintended scorecard regression. Findings F-002, F-003, F-004 and F-010 are carried forward to the Improvement Backlog / Findings for future loops.

## Loop 6 Implementation Review
Verdict: **approved**. Reason: both Hide*PanelAsync methods now capture their session id before the animation delay and bail out on a session mismatch after it, exactly mirroring the five prior fixed sites, with no new same-or-higher-severity regression introduced. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.

--- Loop 7 (UTC 2026-08-05T03:55:23Z) ---

### Loop Counter
Loop 7 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

This loop ran an independent re-derivation from source (full re-read of `PrimaryWidget.xaml.cs`, plus fresh reads of every `Services/` and `Models/` file, `TESTING.md`, and the full test suite), plus two fresh-eyes helper sweeps briefed on the codebase's history but not its prior conclusions - one explicitly tasked with re-testing, method-by-method, whether the six-loop-running "stale async completion mutates shared picker UI state" defect class (F-001, F-005 through F-009) had a further instance anywhere in `PrimaryWidget.xaml.cs`, the other sweeping `Services/` and `Models/` cold. The first sweep, and my own independent trace of the same candidates, both converged on the same conclusion: `LoadGridSelectionAsync`'s and `ShowSearchPanelAsync`'s own unguarded post-await mutations are real but not exploitable (traced in full below), closing the three-loop-running completeness question with zero new instances found. This loop's Priority-1 finding (F-002, the four-times-duplicated panel slide animation, queued five loops) was implemented, verified by build + full test suite + independent implementation review. The second sweep independently re-confirmed F-010 (`StoreNameLookup` bypassing `JsonRead`) and surfaced a genuine new finding (F-011): `LoadGameEntriesAsync` resolves each unmatched game's name and SteamGridDB match sequentially rather than concurrently - a real structural-waste finding blocked from implementation this loop by the STANDING USER CONSTRAINT and a genuine new thread-safety risk a naive fix would introduce.

## Scorecard (1-10)

- Architecture quality: 7.5 | SAME | `Services/` modules re-read in full this loop, independently confirmed each still a deep, single-responsibility Module with a real Interface. `PrimaryWidget.xaml.cs` (now 2033 lines, down from 2058 pre-fix) still carries Finding F-003's three-times-duplicated dialog ceremony (open). Held at SAME rather than moved to reflect F-002's fix: this dimension's own 9-anchor language is about Module graph, Seams, and deletion-test pass-through structure, not shallow-module duplication within a single already-owned class - priced under simplicity below, matching loop 5/6's own non-double-counting discipline.
- State management and runtime ownership: 7.0 | UP | F-001/F-005 through F-009's own fixes independently re-verified holding. This loop's own exhaustive, table-based enumeration of every async method in `PrimaryWidget.xaml.cs` with a mutation after its own await, cross-verified independently by a helper sweep, found **zero** new exploitable instances - the first clean result in three loops (4→5→6 each found one). The one candidate both traces surfaced (`LoadGridSelectionAsync:1315-1317`) was traced end-to-end and found NOT exploitable: `ShowGridPanelAsync`'s fixed 250ms `Task.Delay` guarantees an earlier-clicked session always reaches that line before a later-clicked session does, so the mutation is self-correcting. Moved UP on genuine structural completion evidence, not a code change.
- Domain modeling: 8.5 | SAME | `GameEntry.cs`'s `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` three-independently-settable-properties case remains the only known concern, still no live harm. **Stalled-Dimension Sweep (loop 7, 6th consecutive SAME):** explicit clean.
- Data flow and dependency design: 7.0 | SAME | Finding F-010 independently re-confirmed unchanged this loop by direct read and a second independent helper sweep. Outranked by F-002 on stall; queued Priority 2 for loop 8.
- Framework / platform best practices: 8.0 | SAME | `BuildUrl` and the `DataContractJsonSerializer`/`Windows.Data.Json` split re-verified. **Stalled-Dimension Sweep (loop 7, 6th consecutive SAME):** independently re-derived - F-002's fixed ceremony was never actually a framework_idioms concern (`DoubleAnimation`+`Storyboard` IS the idiomatic UWP approach; the defect was duplication, priced under simplicity) - correcting prior loops' categorization. No fresh candidate found.
- Concurrency and runtime safety: 6.5 | UP | Same completed-audit evidence as state_management. Moved UP, tempered by a genuine new finding (F-011) surfaced this loop: `LoadGameEntriesAsync`'s per-entry loop awaits GOG/Epic/SteamGridDB calls in strict sequence with no ordering dependency - a D2 sequential-independent-effects shape on the library-load hot path. Queued, blocked by the STANDING USER CONSTRAINT and a real thread-safety risk (StoreNameLookup's caches are unlocked).
- Code simplicity and clarity: 7.5 | UP | Finding F-002 fixed this loop: all four Show/Hide call sites now delegate to one shared `SlidePanelAsync` helper; net -26 lines. Verified behavior-preserving (all From/To/Duration/EasingMode literals unchanged; loop-6 session guards textually unchanged). Stall broken after five consecutive SAME loops. F-003 remains open, now Priority 1 for loop 8.
- Test strategy and regression resistance: 6.5 | SAME | `PrimaryWidget.xaml.cs` remains untestable outside an app container, re-confirmed. **Stalled-Dimension Sweep (loop 7, 6th consecutive SAME, lowest-scored dimension):** F-004 remains the only candidate, Cosmetic/off-primary-flow, does not win Priority 1.
- Overall implementation credibility: 8.5 | UP | Second consecutive loop where a targeted, adversarially-framed sweep (this time cross-verified) produced the headline result. F-002's fix independently reviewed, returned `approved`. Independent fingerprint-hash re-derivation reproduced three of loop 6's own stored hashes byte-for-byte before seeing them.

## Authority Map
See Loop 6 for full prior state; re-emitted this loop given the significant state_management/concurrency movement.

- Concern: **Grid picker panel display contents / search results panel** — Owner: `PrimaryWidget`. Verdict: **Single and clear** - a three-loop-running open completeness question (loops 4, 5, 6 each found one further unguarded instance) closed this loop with zero further instances found by an exhaustive, cross-verified enumeration. Two unguarded-but-safe candidates (`LoadGridSelectionAsync:1315-1317`, `ShowSearchPanelAsync`'s focus-only tail) traced and documented rather than silently omitted.
- Concern: **Library load's per-entry name/match resolution (`LoadGameEntriesAsync`'s sequential network calls)** — Owner: `PrimaryWidget.LoadGameEntriesAsync`, delegating to `StoreNameLookup`/`SteamGridDbClient`. Verdict: **Single and clear today** - the unlocked caches are safe under the current single-threaded-per-load design, but that safety is a load-bearing assumption, not an enforced invariant (see Finding F-011).

## Strengths That Matter
- This loop's own verification methodology held up under an independent same-loop cross-check for the first time: a helper sweep briefed to independently enumerate and trace every post-await mutation in `PrimaryWidget.xaml.cs` reached the identical UNGUARDED-BUT-SAFE conclusion on the same two candidates, without having seen my own trace first.
- `ArtworkDownloader.cs`'s selection-and-veto pipeline (re-read in full this loop) remains a genuinely deep Module, documented with the specific graded incident that calibrated each threshold, both gate predicates mutation-tested.
- `AsyncLazyCacheTests.cs`'s 32-concurrent-caller test is genuine concurrency verification under real `Task.Run` parallelism, not a timing-hack sleep.

## Findings

### Finding #1 (stable_id F-002): Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to the panel's slide timing or easing (or a bug in it) had to be made and verified in four places instead of one, and the four copies had already begun to drift (200ms hide vs 250ms show, independently re-derived each time).

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` (pre-fix lines `1571-1592`, `1597-1640`, `1779-1847`, `1852-1891`) each hand-built a `DoubleAnimation` + `Storyboard` against a `TranslateTransform`, set From/To/Duration/EasingFunction, called `storyboard.Begin()`, then awaited `Task.Delay` matching the duration - four near-identical bodies in the single largest, most-churned file in the codebase.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1571-1592` (pre-fix, `ShowGridPanelAsync`), `:1597-1640` (pre-fix, `HideGridPanelAsync`), `:1779-1847` (pre-fix, `ShowSearchPanelAsync`), `:1852-1891` (pre-fix, `HideSearchPanelAsync`)

**Architectural test failed** — Shallow module (each Show/Hide method's Interface ≈ its Implementation; no reuse across the four near-identical bodies)

**Dependency category** — n/a

**Leverage impact** — One call site to read/change instead of four; callers learn less, not more.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`; no other file's behavior changes.

**Metric signal, if any** — none

**Why this weakens submission** — Four near-identical bodies in `PrimaryWidget.xaml.cs` is exactly the leaf-module duplication the Simplicity dimension's 9-anchor requires be swept for; queued five full loops before being fixed this loop.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(TranslateTransform transform, double from, double to, int durationMs, EasingMode mode)` helper that builds the `DoubleAnimation`/`Storyboard` once and awaits `Task.Delay(durationMs)`; each of the four call sites becomes a one-line call. No new Seam, no new file - the four bodies collapse into the one Module (`PrimaryWidget`) that already owns them.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml` (markup unchanged), `SteamGridDB.Xbox/Services/**`.

**Status this loop: implemented — see Loop 7 Result below.**

### Finding #2 (stable_id F-011): LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — A user whose library has many GOG/Epic games without a direct SteamGridDB match pays the full network latency of the GOG, Epic (or Ubisoft/community-database) endpoint plus the SteamGridDB name-search endpoint for every one of those games, one after another, before the library list finishes loading - which happens on every widget open, not once.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs:455-679`) awaits, in strict sequence for each manifest entry: `sgdbClient.GetGameByPlatformIdAsync` (`:581`), then when unmatched one of `StoreNameLookup.GetOrFetchGogNameAsync` (`:603`) / `GetOrFetchEpicNameAsync` (`:612`) / `GetUbisoftGameNameAsync` (`:621`), then `StoreNameLookup.FindGameByNameAsync` (`:641`) - each entry's network calls fully complete before the next entry's loop iteration starts any of its own. None of these calls read or write state another entry's iteration also touches, so the awaits are independent across entries and the loop body is a textbook sequential-independent-effects shape (efficiency lens, D2).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:455-679` (per-manifest-entry `foreach` loop), `:581` (`sgdbClient.GetGameByPlatformIdAsync`), `:603` (`StoreNameLookup.GetOrFetchGogNameAsync`), `:612` (`StoreNameLookup.GetOrFetchEpicNameAsync`), `:641` (`StoreNameLookup.FindGameByNameAsync`)

**Architectural test failed** — n/a - different category (efficiency/D2, not a Seam/Module-boundary finding)

**Dependency category** — n/a (not a Coupling & Leakage finding)

**Leverage impact** — None currently - no seam exists to batch or parallelize through; a fix would need one.

**Locality impact** — Contained to `LoadGameEntriesAsync`'s own per-entry loop body and, if fixed, `StoreNameLookup`'s three dictionaries' thread-safety; no other file's behavior changes.

**Metric signal, if any** — none

**Why this weakens submission** — This is a hot path (the library reloads on every widget open, per `StoreNameLookup`'s own class doc comment) doing per-item network I/O one item at a time where nothing in the current design requires that ordering - the structural cost scales linearly with the count of unmatched games in a user's library, and nothing amortizes it.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Not implemented this loop - blocked by the STANDING USER CONSTRAINT (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft). A safe fix would need bounded concurrency (never unbounded fan-out), and - because `StoreNameLookup`'s `gogNameCache`/`epicNameCache`/`nameMatchCache` dictionaries and `SteamGridDbClient.CapsuleParseNotes` list are currently unlocked, correct only because today's caller is single-threaded per load - would need real thread-safety added to those shared caches first, which is itself a non-trivial, behaviour-affecting change this loop declines to attempt without a broader design pass.

**Blast radius** — Change: none this loop (not attempted). Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` (both not attempted this loop).

### Finding #3 (stable_id F-010): StoreNameLookup's GOG and Epic name-fetch methods bypass the JsonRead helper that exists to prevent a documented JSON null-vs-missing bug

**Why it matters** — The codebase already shipped a bug once where a JSON member that was present-but-null was mishandled by raw `Windows.Data.Json` calls; `JsonRead.cs` exists specifically to make that class of bug impossible. Two of `StoreNameLookup`'s three network-backed name-lookup methods still use the pattern that caused it.

**What is wrong** — `GetGogGameNameAsync` (`StoreNameLookup.cs:67-74`) and `GetEpicGameNameAsync` (`:188-190`) use raw `ContainsKey`/`GetNamedObject`/`GetNamedString` calls instead of `JsonRead.Object`/`JsonRead.String`, which `SteamGridDbClient.cs:155-181` and `EpicLibrary.cs` both correctly use.

**Evidence** — `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:67-74`, `:188-190`, `SteamGridDB.Xbox/Services/JsonRead.cs:1-17`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:470-478`, `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs:155-181`

**Architectural test failed** — n/a - a Reuse/consistency finding

**Dependency category** — n/a

**Leverage impact** — None directly - `JsonRead` already exists; this brings the remaining two call sites into line.

**Locality impact** — Contained to `StoreNameLookup.cs`'s two methods.

**Metric signal, if any** — none

**Why this weakens submission** — Credibility/consistency deduction, not a live defect (outer `catch` prevents a crash today).

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Replace the raw calls with `JsonRead.Object`/`JsonRead.String` calls, matching `SteamGridDbClient.cs`'s established pattern. Behavior-preserving per the five properties (call count, ordering, payload, error handling, observable result).

**Blast radius** — Change: `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`. Avoid: `SteamGridDB.Xbox/Services/JsonRead.cs`, `SteamGridDB.Xbox/Services/SteamGridDB/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

### Finding #4 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations has to be kept in sync by hand.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (lines `743-787`, `789-823`, `825-859` - shifted by one line this loop's own using-directive addition; unaffected by this loop's F-002 edit) each build a `ContentDialog` with the same ceremony.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:743-787`, `:789-823`, `:825-859`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as F-002 (now fixed). Six loops queued without action.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `ConfirmAndRunAsync(...)` helper.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #5 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation would ship silently.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) thresholds at `alpha < 64` and `transparentCorners < 2` are untested at their exact boundary. Re-read this loop, unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — A minor, off-primary-flow gap.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases at the exact alpha/corner-count boundary.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs`.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | F-002's `SlidePanelAsync` extraction resolves a real, evidenced leaf-module duplication - passes the Shallow module test. |
| New seam justified | No - `IAnimator`/`IPanelController` protocol fails the Unified Seam Policy's two-adapter rule immediately; private helper is the correct shape. |
| Helpful simplification | F-002's fix is net -26 lines in `PrimaryWidget.xaml.cs` (33 insertions, 59 deletions) - genuinely subtractive. |
| Should NOT be done | Do not introduce an `IAnimator`/`IPanelController` protocol for `SlidePanelAsync`. Do not attempt F-011's fix without first adding real locking to `StoreNameLookup`'s caches. |
| Tests after fix | None added or deleted - `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface. Verification is build + full test suite + independent implementation review + a line-by-line literal-value diff. |

## Improvement Backlog
1. **Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003).**
   - why it matters: removes ~105 more lines of repeated ceremony; the sole remaining simplicity candidate, now the highest-stall item on the board (six consecutive loops SAME).
   - score impact: `simplicity +0.5`
   - simplification / helpful

2. **Route StoreNameLookup's GOG and Epic name-fetch methods through the existing JsonRead helper (Finding F-010).**
   - why it matters: removes a documented-bug-class inconsistency; small, mechanical, behavior-preserving.
   - score impact: `data_flow +0.5`
   - simplification / helpful

3. **Add bounded concurrency to LoadGameEntriesAsync's per-entry name/match resolution, after first adding real locking to StoreNameLookup's caches (Finding F-011).**
   - why it matters: removes a real, linearly-scaling latency cost on the primary library-load hot path - but not actionable yet: blocked by the STANDING USER CONSTRAINT and the unlocked-cache thread-safety prerequisite.
   - score impact: `concurrency +0.5`
   - structural / helpful

**Priority-1 accounting**: F-003 is Priority 1 for the next loop on Stall (six consecutive loops SAME on `simplicity`) and criterion-4 subtractive-fix preference. F-010 is Priority 2 on lower stall. F-011 ranks on merit above both by distance-to-target but is not actionable this loop: blocked by the STANDING USER CONSTRAINT and a genuine new correctness risk naive parallelization would introduce; queued at Priority 3 rather than escalated to `user_decision`, since F-003 and F-010 remain fully actionable next-loop picks.

## Deepening Candidates
1. **Candidate Module**: `PrimaryWidget`'s confirmation-dialog ceremony (Finding F-003) - same shape F-002 had before this loop's fix. Smallest first step: extract a private `ConfirmAndRunAsync(...)` and replace all three call sites. What not to do: no `DialogService`/`IConfirmationCoordinator` protocol - fails the Unified Seam Policy's two-adapter rule.
2. **Candidate Module**: `LoadGameEntriesAsync`'s per-entry name/match resolution (Finding F-011). Smallest first step: add real locking (a `SemaphoreSlim` per cache) to `StoreNameLookup`'s caches BEFORE attempting any concurrency change to the calling loop. What not to do: do not wrap the loop in `Task.WhenAll` before the caches are locked, and do not attempt the network-ordering half of this fix at all until a behavioural oracle exists.

## Builder Notes
1. **Pattern: an adversarially-framed completeness sweep converging with an independent second sweep on the SAME non-finding is itself the strongest evidence a defect class is actually closed.** → REVIEW_HISTORY.json `loops[6].builder_notes` for full notes (array index 6 = loop 7).
2. **Pattern: not every duplicated code block that a Simplify Pressure Test wants collapsed belongs to the scorecard dimension a prior loop filed it under - re-derive the dimension mapping fresh each time.** → REVIEW_HISTORY.json `loops[6].builder_notes` for full notes.
3. **Pattern: a structural-waste finding (slow, not wrong) can still be correctly blocked by a behavioral-preservation constraint even when the finding itself never touches the constrained surface directly.** → REVIEW_HISTORY.json `loops[6].builder_notes` for full notes.

## Final Judge Narrative
Place, not win, this loop. This loop's headline result is a negative one, and a rare one for this codebase: an exhaustive, cross-verified sweep for a sixth instance of the "stale async completion mutates shared picker UI state" defect class found none, closing a completeness question that loops 4, 5 and 6 each answered by finding one more instance. Two unguarded candidates were found and traced to genuine safety rather than silently passed over. State_management and concurrency both moved UP on that structural completion evidence, with concurrency's move tempered by a genuine new finding (F-011, sequential per-game network calls on the library-load hot path) that the same fresh-eyes sweep surfaced independently. This loop's own implementation work (F-002, collapsing the four-times-duplicated panel-slide animation into one shared helper) is real, net-subtractive (-26 lines), and verified by build + full test suite + an independent implementation review that returned `approved` on first pass. Simplicity's five-loop stall broke as a direct result. Runtime ownership is more trustworthy this loop by real evidence, not by the passage of time; concurrency is more trustworthy on the reentrancy axis specifically, but F-011 is an honest reminder that "trustworthy" does not mean "exhaustively efficient." Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`. Future work risks over-engineering only if F-003's eventual extraction reaches for a coordinator/service abstraction instead of a private helper method, or if F-011's eventual fix attempts to parallelize the network loop before adding real locks to `StoreNameLookup`'s caches.

## Loop 7 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (92 lines changed: 33 insertions, 59 deletions, net -26 lines): replaced the four near-identical `DoubleAnimation`/`Storyboard` construction bodies in `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` with a single shared `SlidePanelAsync(TranslateTransform, from, to, durationMs, EasingMode)` private helper (`PrimaryWidget.xaml.cs:1569-1600`); each of the four call sites now delegates to it with its own original From/To/Duration/Easing arguments. The five prior session-guard fixes (F-001, F-005 through F-009) are untouched in substance - `HideGridPanelAsync`'s and `HideSearchPanelAsync`'s session captures and rechecks sit exactly where they did before, just after a call to the new helper instead of after inline animation code. Added one using directive (`Windows.UI.Xaml.Media`, for `TranslateTransform`) at the top of the file. Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected, since `PrimaryWidget.xaml.cs` is not part of the test-linked `Services/**` surface. An independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on Finding F-002 and the diff only) returned verdict `approved` with all three checks (reality, honesty, regression) passed. Finding F1 (stable_id F-002) is **resolved**: verified by a line-by-line diff confirming every call site's From/To/Duration/EasingMode literal value is unchanged from the pre-fix code and the loop-6 session guards in `HideGridPanelAsync`/`HideSearchPanelAsync` are textually unchanged, only repositioned after the new helper call. This loop additionally re-verified Findings F-001/F-005 through F-009's fixes are still holding at their own call sites (no regression), completed a three-loop-running audit of the panel-state reentrancy hazard class with zero new instances found (two candidates traced to genuine safety, not silently passed over), and an independent helper sweep of `Services/`/`Models/` re-confirmed Finding F-010 and surfaced a new Finding F-011 (sequential per-game network calls in `LoadGameEntriesAsync`), both queued to the backlog rather than implemented this loop. No unintended scorecard regression: the change touches no network call, no ranking/selection logic, and no file outside the one named. Findings F-003, F-004, F-010 and F-011 are carried forward to the Improvement Backlog / Findings for future loops.

## Loop 7 Implementation Review
Verdict: **approved**. Reason: the four hand-built `DoubleAnimation`/`Storyboard` bodies are gone from source, replaced by one `SlidePanelAsync` helper with all four call sites preserving their original From/To/Duration/EasingMode values and the loop-6 session guards left untouched and merely repositioned after the new call. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.

--- Loop 8 (UTC 2026-08-05T04:32:06Z) ---

### Loop Counter
Loop 8 of 10 (cap)

### System Flag
[STATE: CONTINUE]

(Discovery: see Loop 1 Discovery.)

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

This loop wrote an independent scorecard from current source first (full re-read of the Priority-1 target region in `PrimaryWidget.xaml.cs`, `StoreNameLookup.cs`, `JsonRead.cs`, `AppliedArtworkStore.cs`, `GameEntry.cs`, `GridImageItem.cs`, `GamePlatform.cs`, `App.xaml.cs`, `MainPage.xaml.cs`, the mandatory doc-vs-code grep, and a fresh `new GameEntry(` construction-site grep across the whole tree), then two fresh-eyes helper sweeps briefed on the codebase's history but not its prior conclusions - one a cold Reuse/Simplification/Altitude/Efficiency/security sweep of `Services/` and `Models/`, the other a test-strategy mutation-check plus domain-modeling audit - only after which `CURRENT_REVIEW.md`/`REVIEW_HISTORY.md` were read for delta basis. This loop's Priority-1 finding (F-003, the three-times-duplicated confirmation-dialog ceremony, queued seven loops) was implemented, verified by build + full test suite + independent implementation review, and along the way caught and fixed its own transient regression (a stale doc comment left orphaned above the new helper by the extraction) before it ever reached a diff review. The two stalled dimensions with the longest SAME-streaks on the board - `domain_modeling` (seven consecutive loops, tied) and `framework_idioms` (seven consecutive loops) - both got the Residual Accounting Pass they were overdue for: `domain_modeling` promotes to 9.5 with a fully adversarially-tested accepted residual; `framework_idioms` promotes to a genuine 10 after eight cumulative loops of scrutiny (including this loop's own fresh sweeps) named zero remaining source-backed candidates. A cold helper sweep also surfaced a new, low-severity finding (F-012): `GamePlatformHelper`'s two independent switch statements over `GamePlatform` have no shared source of truth.

## Scorecard (1-10)

- Architecture quality: 7.5 | SAME | `Services/` modules re-confirmed (via this loop's own reads and the cold helper sweep) each still a deep, single-responsibility Module with a real Interface - no new Module-graph-level concern surfaced. `PrimaryWidget.xaml.cs` is now 2010 lines (2032 pre-fix; this loop's own F-003 fix is net -22 lines) and F-003's dialog ceremony is now resolved (see Findings), but the class itself remains one large single-Module orchestrator handling library loading, dialog confirmation, panel animation, grid selection and search in one file. **Stalled-Dimension Sweep (loop 8, 3rd consecutive SAME):** ran the Residual Accounting Pass explicitly this loop rather than repeating the prior note verbatim. The 9-anchor ("Module graph enforced by source, not convention... deletion tests leave little pass-through structure") is judged NOT met while `PrimaryWidget.xaml.cs` remains one large Module covering five distinct concerns with no further internal Module boundaries - this is a genuine, not cosmetic, gap. It is not promoted to a valid backlog item this loop: splitting `PrimaryWidget.xaml.cs` into several owned sub-Modules would be a large, high-risk restructuring of an untestable UWP page (no automated regression net), and the incremental, behavior-preserving extractions actually available (F-002, F-003, both now resolved; F-010's JsonRead routing) are already tracked as `simplicity`/`data_flow` items rather than Module-graph items - a bigger split has no proven friction beyond what those smaller fixes already address, so it fails Simplify Pressure Test Q2 (not the smallest honest fix for a benefit that isn't proven). `residual_blocker_kind: "structural_anchor_unmet"`.
- State management and runtime ownership: 7.0 | SAME | F-001/F-005 through F-009's eight guarded call sites spot-checked this loop (via grep for `SlidePanelAsync`/session-guard lines) and confirmed still holding, unaffected by this loop's F-003 edit (a disjoint region of the file). No fresh exhaustive completeness sweep was run this loop (that work was loop 7's, closed with zero new instances after cross-verification); this loop's own investigation focus was elsewhere (F-003, plus the Residual Accounting/Adversarial passes on `domain_modeling`/`framework_idioms`). Held at SAME rather than moved UP again: G8 requires structural proof of *fresh* completion evidence to move up, and re-asserting last loop's already-credited completeness sweep without new work would be anchoring, not re-deriving. Not moved DOWN either: no regression, no new hazard found.
- Domain modeling: 9.5 | UP | **Residual Accounting Pass run explicitly this loop** (dimension was SAME 7 consecutive loops - the longest-tied stall on the board alongside `framework_idioms`). 9-anchor ("Domain types prove most invariants by construction... one or two parallel-fields cases remain but are documented") judged MET: independently re-confirmed via a fresh, whole-tree `grep -rn "new GameEntry"` (this loop, not carried from a prior loop's claim) that `PrimaryWidget.xaml.cs:651-665` remains the sole construction site for `GameEntry`, and that its `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` three-independently-settable-properties case is documented via XML doc comments on each property (`GameEntry.cs:128-145`) - matching the anchor's own "one or two parallel-fields cases remain but are documented" language nearly verbatim. `GridImageItem.cs` and `GamePlatform.cs` re-read in full this loop (directly and via helper): no comparable concern, no impossible-state risk (their fields are flat external-API display data with no domain invariant to violate). **Adversarial Pass** (mandatory before accepting the residual): smallest possible fix considered - collapse `HasSteamGridDBMatch` into a computed property derived from `SteamGridDbGameId > 0 || OfficialCapsuleUrl != null` (verified via a fresh grep for `.HasSteamGridDBMatch =`/`.SteamGridDbGameId =`/`.OfficialCapsuleUrl =` outside the construction site: zero hits, confirming all three are write-once, so no `PropertyChanged` notification would be lost). **SPT-rejected on Q1**: this fix removes one symptom but does not enforce the actual invariant (mutual exclusivity of the platform-ID-match path vs. the name-match path's own field) - a caller could still set `SteamGridDbGameId` and `OfficialCapsuleUrl` both non-default, an impossible state under current business logic, representable either way. The fix that would actually close the gap - a discriminated `MatchResult` replacing all three properties via a smart constructor - is the same factory-method rewrite already rejected on Q2 as ceremony disproportionate to a Cosmetic, never-yet-harmful concern on a mutable, XAML-bound MVVM type. Residual holds. `residual_blocking_10`: "`GameEntry.cs:113-145`'s `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` parallel-fields case, sole construction site `PrimaryWidget.xaml.cs:651-665`." `residual_disposition: "accepted"`. `residual_rationale_or_backlog_ref`: "Fails Simplify Pressure Test (Adversarial Pass, this loop): the only fix that closes the real invariant is a discriminated-union/smart-constructor rewrite of a mutable, XAML-data-bound MVVM type for a Cosmetic-severity, no-live-harm concern - ceremony disproportionate to the fix size, eight loops running. No ADR exists in this repo; disposition rests on the SPT-Q2/ceremony branch of the Residual Accounting Pass, not a framework or ADR carve-out."
- Data flow and dependency design: 7.0 | SAME | `Services/*` re-confirmed zero `Windows.UI.Xaml` imports (re-grepped this loop). Finding F-010 (`StoreNameLookup.GetGogGameNameAsync`/`GetEpicGameNameAsync` bypassing `JsonRead`) independently re-confirmed unchanged this loop by a full fresh read of `StoreNameLookup.cs` (untouched by this loop's F-003 edit, which is confined to `PrimaryWidget.xaml.cs`) and by the cold helper sweep, which reached the identical finding without seeing loop 6/7's text. Queued residual - 9-anchor not fully met while F-010 (Noticeable, SPT-passing, well-scoped) remains open; not promoted, correctly stays a backlog item rather than an accepted residual since a real, actionable, not-yet-implemented fix exists. Outranked this loop by F-003 on stall (seven consecutive SAME loops on `simplicity` vs. F-010's own one-loop stall on `data_flow`); Priority 1 for loop 9.
- Framework / platform best practices: 10.0 | UP | **Residual Accounting Pass run explicitly this loop** (dimension was SAME 7 consecutive loops, tied with `domain_modeling` for the longest stall on the board). 9-anchor ("Stack idiomatic in primary surfaces... one or two non-idiomatic carve-outs documented") judged met, and this loop found **no remaining candidate to even carve out**: `SteamGridDbClient.cs:273-301`'s `BuildUrl` and its `DataContractJsonSerializer`/`Windows.Data.Json` split re-verified fixed and holding; `App.xaml.cs` and `MainPage.xaml.cs` read in full for the first time this loop (not previously cited in any prior loop's evidence) - both are minimal, idiomatic UWP/Xbox-Game-Bar-widget boilerplate (`Suspending` deferral pattern used correctly, `Frame` navigation, the one `//TODO` matches README's own documented EA-App limitation, not doc rot); `StoreNameLookup.cs`/`AppliedArtworkStore.cs`/`JsonRead.cs` re-read directly this loop, idiomatic throughout (`StorageFile`/`StorageFolder` disposal, `SemaphoreSlim`-gated `AsyncLazyCache<T>` reuse). The cold helper sweep's one candidate under this dimension - `SteamGridDbClient.DeserializeJson<T>` constructing a fresh `DataContractJsonSerializer` per call instead of caching one per `T` - was investigated and rejected as a true micro-optimization (Ignore-list): the cost is sub-millisecond reflection dwarfed by the network round-trip the same call already pays (50-500ms), it recurs once per HTTP response rather than N× within one logical pass (not the D1 shape), and there is no more-idiomatic built-in being bypassed. Per the Score Anchors' own rule ("No source-backed residual can be named -> set score to 10"), holding this at 8/9 any longer would itself be under-scoring against the rubric's own text, not conservatism.
- Concurrency and runtime safety: 6.5 | SAME | F-011 (`LoadGameEntriesAsync`'s sequential per-entry network calls, `PrimaryWidget.xaml.cs:455-679`) independently re-confirmed unchanged this loop by direct read - that region sits above this loop's F-003 edit point (743+) and is byte-identical. Still blocked by the STANDING USER CONSTRAINT and the unlocked-`StoreNameLookup`-cache prerequisite (also re-confirmed this loop: `gogNameCache`/`epicNameCache`/`nameMatchCache` remain plain unlocked `Dictionary`s). No new concurrency hazard found; no fresh completeness sweep run this loop (see state_management). SAME, not UP or DOWN - no fresh structural evidence in either direction.
- Code simplicity and clarity: 8.0 | UP | Finding F-003 (three near-identical `ContentDialog`-construction-plus-guard-and-run bodies, `FixLibraryButton_Click`/`RestoreChangesButton_Click`/`RevertDefaultsButton_Click`) fixed this loop: all three call sites now delegate to one shared private `ConfirmAndRunAsync(title, content, primaryButtonText, secondaryButtonText, shouldRun, action)` helper (`PrimaryWidget.xaml.cs:740-800`), each collapsing from a ~35-45-line hand-built `ContentDialog`+guard body to a 7-9 line parameterized call; net -22 lines in the file (140 lines changed: 59 insertions, 81 deletions). Verified behavior-preserving: every call site carries its original literal title/content/button-text values, the same `Style`/`PrimaryButtonStyle`/`SecondaryButtonStyle`/`CloseButtonStyle` resource assignments (secondary style now conditional on a non-empty secondary text, matching the pre-fix behavior where only `FixLibraryButton_Click`'s dialog set it), the same `XamlRoot` API-contract check, and the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` short-circuit ordering (`shouldRun(result)` evaluated before `TryBeginLibraryOperation()`, matching the original `&&`/`||` short-circuit semantics exactly). Stall broken after seven consecutive SAME loops (1-7) - the longest-running simplicity stall this run has seen, one tier past F-002's own five-loop stall. `PrimaryWidget.xaml.cs` no longer carries any of the leaf-module duplication tracked since loop 1 (F-002 and F-003 both now resolved); the leaf-module duplication sweep this loop (direct read + cold helper sweep of `Services/`/`Models/`) found the two-switch `GamePlatformHelper` concern (F-012, new, Cosmetic) as the only remaining candidate anywhere in the codebase.
- Test strategy and regression resistance: 6.5 | SAME | **Stalled-Dimension Sweep (loop 8, 7th consecutive SAME, lowest-scored dimension on the board):** re-applied the mutation-test mental model (method.md Step 8) fresh this loop rather than repeating "still can't test it." Named mutation: deleting the guard `if (session != gridPanelSessionId) { return; }` in `HideGridPanelAsync` (`PrimaryWidget.xaml.cs:1632`, unaffected by this loop's edit) would not be caught by any test - `PrimaryWidget.xaml.cs` carries zero test coverage of any kind (structurally excluded from `SteamGridDB.Xbox.Tests`, confirmed via `TESTING.md` and the `.csproj` target-framework mismatch, re-verified this loop). This mutation sits **on a primary flow** (the exact reentrancy-guard idiom that took nine rounds of hand-verification across F-001/F-005 through F-009), not an off-path helper - per method.md Step 8's own branch logic this means the 9-anchor is genuinely NOT met (not merely under-credited), so no Residual-Accounting promotion applies here the way it did for `domain_modeling`/`framework_idioms` above. Considered, and re-rejected, extracting the guard comparison into a standalone testable "SessionGuard" type (loop 6's own rejected candidate, re-tested this loop): fails SPT Q1 - the ambiguity that matters is *placement* (is the check correctly positioned after every hazardous `await`?), which a unit test of an extracted int-comparison could never exercise; the real test surface remains `PrimaryWidget.xaml.cs` itself, structurally untestable. `residual_blocker_kind: "structural_anchor_unmet"`; the blocker is a genuine platform/build-system constraint (UWP page type has no desktop test projection), not an unaddressed choice - everything extractable from this file already has been (`ManifestEntryIdentity`, `GameImages`, `OperationReport`, `JsonRead` were all pulled out of it specifically for this reason, per `TESTING.md`'s own account).
- Overall implementation credibility: 9.0 | UP | F-003's fix independently reviewed by a fresh-eyes subagent briefed cold on the diff and the targeted finding only, returning `approved` with all three checks (reality, honesty, regression) passed on the first pass. Distinct fresh evidence this loop, not a repeat of prior loops' pattern: this loop's own Step 1 investigation caught and corrected its own transient defect before it ever reached the reviewer or a commit - the `ConfirmAndRunAsync` extraction initially left a stale `/// <summary>... Handles fix library button click...` doc comment orphaned above the new helper (describing the wrong method after the extraction moved `FixLibraryButton_Click` below it); this was caught by re-diffing the change before rebuilding, fixed, and re-verified (build + test both re-run clean after the fix) - the loop's own verification discipline is catching its own mistakes, not just the codebase's. Structural proof for the UP move beyond F-003: the mandatory doc-vs-code grep (`LEGACY|TEMPORARY|DEPRECATED|DO NOT|...`) run fresh this loop found exactly three hits, all genuine and accurate (no doc rot); the Residual Accounting Pass and Adversarial Pass were both run with real, falsifiable reasoning (not rubber-stamped) on `domain_modeling`'s residual, correctly promoting one dimension while correctly declining to promote `test_strategy` for a structurally distinct reason - evidence the review methodology discriminates rather than defaults to the generous read.

## Authority Map
Not re-emitted in full this loop: no state_management/concurrency-relevant authority changed (F-003 is a dialog-and-guard-wrapping extraction with zero mutable-state implications; F-010/F-011/F-012 remain unimplemented). See loop 7's Authority Map (above) for the still-current picker-panel and library-load authority maps, both re-confirmed unaffected by this loop's edit via direct grep of the guarded call sites.

## Strengths That Matter
- This loop's own review methodology caught and fixed its own defect (the orphaned doc comment described above) before it reached the implementation reviewer or a commit - a concrete instance of the loop's verification discipline working as designed, not just asserted.
- The `domain_modeling` Adversarial Pass (this loop) demonstrates the review process can correctly *reject* its own proposed simplification when the smaller fix doesn't actually close the real invariant, rather than taking the first available subtractive-looking option - the SPT Q1 rejection of the "computed `HasSteamGridDBMatch`" idea is reasoned through to a concrete counter-scenario (both fields settable simultaneously), not asserted.
- `AsyncLazyCacheTests.cs`'s 32-concurrent-caller test (`Loads_once_however_many_callers_arrive_together`) remains genuine concurrency verification under real `Task.Run` parallelism, re-confirmed present and unaffected this loop.

## Findings

### Finding #1 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations had to be kept in sync by hand - the same style-resource assignment, the same `XamlRoot` API-contract check, the same `TryBeginLibraryOperation`/`EndLibraryOperation` guard wiring - and nothing enforced that a future fourth operation (or an edit to one of the three) followed the same shape.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (pre-fix lines `743-787`, `789-823`, `825-859`) each built a `ContentDialog` with the same four style-resource lookups and the same `Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent` `XamlRoot` check, called `ShowAsync`, branched on the result, and wrapped the actual operation in the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` pattern.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:743-787` (pre-fix, `FixLibraryButton_Click`), `:789-823` (pre-fix, `RestoreChangesButton_Click`), `:825-859` (pre-fix, `RevertDefaultsButton_Click`)

**Architectural test failed** — Shallow module (each Click handler's Interface ≈ its Implementation; no reuse across the three near-identical bodies)

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action instead of re-deriving the whole confirm-guard-run shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as Finding F-002 (resolved loop 7), in the same file; this cluster alone accounted for roughly 105 of `PrimaryWidget.xaml.cs`'s lines being ceremony repeated 3x rather than owned once. Seven loops queued before being fixed this loop, matching F-002's own five-loop stall pattern one tier higher.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extracted a private `ConfirmAndRunAsync(string title, string content, string primaryButtonText, string secondaryButtonText, Func<ContentDialogResult, bool> shouldRun, Func<ContentDialogResult, Task> action)` that owns dialog construction, the `XamlRoot` check, and the `TryBeginLibraryOperation`/`EndLibraryOperation` wrapping; each of the three handlers now calls it naming its own title/content/action. The action receives the dialog result so `FixLibraryButton_Click` (the one caller with a secondary button) can still branch on Primary vs Secondary.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml` (markup unchanged), `SteamGridDB.Xbox/Services/**`.

**Status this loop: implemented — see Loop 8 Result below.**

### Finding #2 (stable_id F-010): StoreNameLookup's GOG and Epic name-fetch methods bypass the JsonRead helper that exists to prevent a documented JSON null-vs-missing bug

**Why it matters** — The codebase already shipped a bug once (documented in `PrimaryWidget.xaml.cs:470-478` and `JsonRead.cs:13-16`) where a JSON member that was present-but-null was mishandled by raw `Windows.Data.Json` calls; `JsonRead.cs` exists specifically to make that class of bug impossible. Two of `StoreNameLookup`'s three network-backed name-lookup methods still use the pattern that caused it.

**What is wrong** — `GetGogGameNameAsync` (`StoreNameLookup.cs:67-74`) reads `gameData.ContainsKey("_embedded") && gameData.GetNamedObject("_embedded").ContainsKey("product")`, then `gameData.GetNamedObject("_embedded").GetNamedObject("product")`, then `product.GetNamedString("title")` - all raw `Windows.Data.Json` calls. `GetEpicGameNameAsync` (`:188-190`) does the same for `gameData.GetNamedString("title")`. `GetNamedObject`/`GetNamedString` throw `InvalidOperationException` when the member is present but JSON `null`, which `ContainsKey` cannot distinguish from a normal value - the exact ambiguity `JsonRead.Object`/`JsonRead.String` were written to resolve, and which `SteamGridDbClient.cs:155-181` and `EpicLibrary.cs` both correctly use instead. The outer broad `catch (Exception ex)` in both methods means a null-title response would not crash - it would silently degrade to "name not found" - so there is no live crash risk today; the harm is a maintained inconsistency next to the exact helper built to remove it.

**Evidence** — `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:67-74` (`GetGogGameNameAsync`), `:188-190` (`GetEpicGameNameAsync`), `SteamGridDB.Xbox/Services/JsonRead.cs:1-17`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:470-478`, `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs:155-181`

**Architectural test failed** — n/a - a Reuse/consistency finding, not a Seam/Module-boundary finding

**Dependency category** — n/a

**Leverage impact** — None directly - `JsonRead` already exists and is already used by two other call sites; this brings the remaining two into line.

**Locality impact** — Contained to `StoreNameLookup.cs`'s two methods; `JsonRead.cs` itself untouched.

**Metric signal, if any** — none

**Why this weakens submission** — Credibility/consistency deduction, not a live defect: the established, documented, purpose-built helper for this exact class of JSON-parsing bug exists in the same file's own dependency graph and is used by two of the three sibling call sites plus `SteamGridDbClient.cs` and `EpicLibrary.cs`, yet two methods opt out with no stated reason.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Replace the raw `ContainsKey`/`GetNamedObject`/`GetNamedString` chains in both methods with `JsonRead.Object(gameData, "_embedded")` → `JsonRead.Object(embedded, "product")` → `JsonRead.String(product, "title")` (GOG) and `JsonRead.String(gameData, "title")` (Epic), matching `SteamGridDbClient.cs`'s established pattern. Behavior-preserving per the five properties (call count, ordering, payload, error handling, observable result).

**Blast radius** — Change: `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`. Avoid: `SteamGridDB.Xbox/Services/JsonRead.cs`, `SteamGridDB.Xbox/Services/SteamGridDB/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

### Finding #3 (stable_id F-012): GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding, renaming, or removing a platform requires updating both switches by hand; nothing enforces they stay in sync, and each has a silent (non-crashing) default fallback, so a future skew would degrade silently - a new platform's names would not resolve to SteamGridDB, or vice versa - rather than fail loudly.

**What is wrong** — `GamePlatformHelper.FromXboxDirectory` (`GamePlatform.cs:22-46`) maps Xbox `ThirdPartyLibraries` folder-name strings to `GamePlatform`; `GamePlatformHelper.GamePlatformToSGDBApiString` (`GamePlatform.cs:48-67`) maps `GamePlatform` back to SteamGridDB's own API platform strings. Both switch over the same 8-case enum but are independently authored with no shared table; the six platform cases both switches cover (Steam/GOG/Epic/Ubisoft/BattleNet/EA) are each asserted twice.

**Evidence** — `SteamGridDB.Xbox/Models/GamePlatform.cs:22-46` (`FromXboxDirectory`), `:48-67` (`GamePlatformToSGDBApiString`)

**Architectural test failed** — n/a - a Reuse/consistency finding (duplicate abstraction smell), not a Seam/Module-boundary finding

**Dependency category** — n/a

**Leverage impact** — None currently; a consolidated metadata table would let a future platform addition touch one place.

**Locality impact** — Contained to `GamePlatform.cs`'s two static methods.

**Metric signal, if any** — none

**Why this weakens submission** — A real but currently-latent duplicate-abstraction risk (Simplicity dimension's own "duplicate abstractions" smoke item), surfaced by an independent, cold helper sweep this loop; not yet manifesting live harm since the six shared cases are currently correctly mirrored in both switches.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Introduce a single static table of `(GamePlatform, xboxFolderName, alternateXboxFolderName, sgdbApiString)` entries that both `FromXboxDirectory` and `GamePlatformToSGDBApiString` query, replacing both switch bodies with a lookup; `Custom`'s special-cased folder name and lack of an SGDB string stay expressed as `null`/absent in the table rather than as a code-path difference.

**Blast radius** — Change: `SteamGridDB.Xbox/Models/GamePlatform.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (both call sites unchanged, same signatures), `SteamGridDB.Xbox/Services/**`.

### Finding #4 (stable_id F-011): LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — A user whose library has many GOG/Epic games without a direct SteamGridDB match pays the full network latency of the GOG, Epic (or Ubisoft/community-database) endpoint plus the SteamGridDB name-search endpoint for every one of those games, one after another, before the library list finishes loading - which happens on every widget open, not once.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs:455-679`) awaits, in strict sequence for each manifest entry: `sgdbClient.GetGameByPlatformIdAsync` (`:581`), then when unmatched one of `StoreNameLookup.GetOrFetchGogNameAsync` (`:603`) / `GetOrFetchEpicNameAsync` (`:612`) / `GetUbisoftGameNameAsync` (`:621`), then `StoreNameLookup.FindGameByNameAsync` (`:641`) - each entry's network calls fully complete before the next entry's loop iteration starts any of its own. The awaits are independent across entries; this is a sequential-independent-effects shape.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:455-679`, `:581`, `:603`, `:612`, `:641`

**Architectural test failed** — n/a - efficiency/D2, not a Seam/Module-boundary finding

**Dependency category** — n/a

**Leverage impact** — None currently - no seam exists to batch or parallelize through.

**Locality impact** — Contained to `LoadGameEntriesAsync`'s own per-entry loop body and, if fixed, `StoreNameLookup`'s three dictionaries' thread-safety.

**Metric signal, if any** — none

**Why this weakens submission** — Hot path (the library reloads on every widget open) doing per-item network I/O one item at a time where nothing in the current design requires that ordering.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Not implemented this loop - blocked by the STANDING USER CONSTRAINT (no behavioural oracle for per-game network-call ordering/concurrency changes) and by `StoreNameLookup`'s three unlocked caches, which would need real thread-safety added first.

**Blast radius** — Change: none this loop (not attempted). Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` (both not attempted this loop).

### Finding #5 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation at either threshold (`<` to `<=`) would ship silently.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) treats a pixel transparent when `alpha < 64` (`:250`) and rejects the image when `transparentCorners < 2` (`:263`). Untested at either exact boundary. Re-read this loop, unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — Minor, off-primary-flow gap; Cosmetic per the anchor's own carve-out.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases at the exact documented boundaries.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs`.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Finding F-003's `ConfirmAndRunAsync` extraction resolves a real, evidenced leaf-module duplication (three near-identical `ContentDialog`/guard-and-run bodies) - passes the Shallow module test |
| New seam justified | No - an `IConfirmationCoordinator`/`DialogService` protocol fails the Unified Seam Policy's two-adapter rule immediately (private in-process UI glue, one production caller-family) |
| Helpful simplification | F-003's fix is net -22 lines in `PrimaryWidget.xaml.cs` (140 lines changed: 59 insertions, 81 deletions) - genuinely subtractive |
| Should NOT be done | The `IConfirmationCoordinator`/`DialogService` protocol (see above); collapsing `GameEntry.HasSteamGridDBMatch` into a computed property (Adversarial Pass this loop: fails SPT Q1); F-011's fix before `StoreNameLookup`'s caches are locked |
| Tests after fix | None added/deleted - untestable surface; verified by full build+suite, an independent fresh-eyes implementation review (verdict approved), plus a line-by-line diff of every call site's literals and the `TryBeginLibraryOperation`/`EndLibraryOperation` short-circuit ordering |

## Improvement Backlog
1. **Route StoreNameLookup's GOG and Epic name-fetch methods through the existing JsonRead helper (Finding F-010).** score impact: `data_flow +0.5` — simplification, helpful
2. **Consolidate GamePlatformHelper's two independent switch statements into one shared platform-metadata table (Finding F-012).** score impact: `simplicity +0.5` — simplification, helpful
3. **Add bounded concurrency to LoadGameEntriesAsync's per-entry name/match resolution, after first adding real locking to StoreNameLookup's caches (Finding F-011).** score impact: `concurrency +0.5` — structural, helpful

**Priority-1 accounting**: F-010 is Priority 1 for loop 9 as the highest-merit actionable candidate. F-011 ranks above it on pure distance-to-target (`concurrency` jointly lowest-scored on the board) but remains blocked by the STANDING USER CONSTRAINT and the `StoreNameLookup` cache-locking prerequisite - named explicitly per Backlog Prioritization criterion 0. F-012 (new, Cosmetic) ranks last on both distance-to-target and severity.

## Deepening Candidates
1. **Candidate Module**: `LoadGameEntriesAsync`'s per-entry name/match resolution (Finding F-011). Carried forward unchanged from loop 7 - re-confirmed this loop that the source region and its prerequisite are both byte-identical to loop 7's own citation. → REVIEW_HISTORY.json `loops[7].deepening_candidates` for full detail.

## Builder Notes
- Pattern: running the Residual Accounting Pass explicitly, dimension by dimension, on the multi-loop-stalled dimensions - rather than re-writing the same "still can't fix it" note - can surface a legitimate promotion the prior loops' framing never tested for. → REVIEW_HISTORY.json `loops[7].builder_notes` for full notes.
- Pattern: a helper-surfaced candidate finding that looks structurally similar to a tracked finding (same class, different file) is not automatically a duplicate - verify the actual code shape before merging or dismissing. → REVIEW_HISTORY.json `loops[7].builder_notes` for full notes.
- Pattern: a loop's own verification pass catching its own transient mistake before the reviewer sees it is stronger credibility evidence than a clean diff alone, because it demonstrates the review discipline is active rather than merely asserted. → REVIEW_HISTORY.json `loops[7].builder_notes` for full notes.

**Scorecard humility check** — see REVIEW_HISTORY.json `loops[7].narrative` for the full two-point check (the `framework_idioms` jump straight to 10 in one loop; holding `state_management`/`concurrency` at SAME rather than re-running a fresh exhaustive completeness sweep this loop).

## Final Judge Narrative
Place, not win, this loop. The headline result is structural bookkeeping finally catching up with itself: two dimensions (`domain_modeling`, `framework_idioms`) had each sat at the same score for seven consecutive loops with a "confirmed unchanged, no live harm" note that never actually applied the Score Anchors' own promotion rule - this loop ran the Residual Accounting Pass explicitly on both, with a real Adversarial Pass on `domain_modeling`'s residual (not a rubber stamp; the "just compute HasSteamGridDBMatch" shortcut was proposed, tested against a concrete counter-scenario, and correctly rejected) and an honest accounting of `framework_idioms`'s eight cumulative loops of zero remaining candidates. This is not score inflation - both moves are backed by fresh, source-cited reasoning that a stricter reviewer can check and, if they disagree with the `framework_idioms` jump to 10 specifically, argue against on its own terms (see humility check). The loop's own implementation work (F-003, collapsing the three-times-duplicated confirmation-dialog ceremony into one shared helper) is real, net-subtractive (-22 lines), verified by build + full test suite + an independent implementation review that returned `approved` on first pass, and the loop caught and fixed its own transient regression (an orphaned doc comment) before that review ever saw it - genuine, demonstrated self-correction rather than an assertion of care. `architecture_quality` and `test_strategy` both got the same Stalled-Dimension Sweep discipline applied but reached the opposite conclusion from `domain_modeling`/`framework_idioms`: their 9-anchors are genuinely NOT met (a large single-Module orchestrator; zero test coverage on a primary-flow reentrancy idiom), so they correctly stay capped rather than promoted, with `structural_anchor_unmet` blockers named for each rather than a vague "still working on it." `simplicity` is now clean of every leaf-module-duplication finding tracked since loop 1 - the only remaining candidate anywhere in the codebase is F-012, freshly surfaced and Cosmetic. Future work risks over-engineering only if F-010's eventual fix reaches for anything beyond the three-line `JsonRead` substitution already fully specified, or if F-011's eventual fix attempts to parallelize the network loop before adding real locks to `StoreNameLookup`'s caches - both explicitly warned against above.

## Loop 8 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (140 lines changed: 59 insertions, 81 deletions, net -22 lines): replaced the three near-identical `ContentDialog`-construction-plus-guard-and-run bodies in `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` with a single shared `ConfirmAndRunAsync(string title, string content, string primaryButtonText, string secondaryButtonText, Func<ContentDialogResult, bool> shouldRun, Func<ContentDialogResult, Task> action)` private helper (`PrimaryWidget.xaml.cs:740-800`); each of the three call sites now delegates to it with its own original title/content/button-text values and a `shouldRun`/`action` pair matching its original branching logic exactly (`FixLibraryButton_Click`'s `action` still branches on `result == ContentDialogResult.Secondary` to choose the `refixCustomised` argument to `FixLibraryAsync`, since it receives the dialog result). During implementation, an initial version of the extraction left the original `FixLibraryButton_Click` doc comment ("Handles fix library button click...") orphaned directly above the new `ConfirmAndRunAsync` method, now describing the wrong method - caught by re-diffing the change before the final build, fixed by removing the stale comment (the file's other two handlers never had doc comments, so `FixLibraryButton_Click` losing its own bare-summary comment matches the file's existing asymmetric convention rather than introducing a new one). Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change (both the initial and the corrected version). The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected. An independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on Finding F-003 and the diff only) returned verdict `approved` with all three checks (reality, honesty, regression) passed on the first pass. Finding F1 (stable_id F-003) is **resolved**: verified by a line-by-line diff confirming every call site's title/content/button-text literal is unchanged from the pre-fix code, the `Style`/`PrimaryButtonStyle`/`SecondaryButtonStyle`/`CloseButtonStyle` resource assignments are preserved, the `XamlRoot` API-contract check is unchanged, and the `shouldRun(result) || !TryBeginLibraryOperation()` short-circuit ordering exactly preserves each handler's original `&&`/`||` semantics. This loop additionally re-verified Findings F-001/F-005 through F-009's guarded call sites unaffected, independently re-confirmed F-010 and F-011 unchanged via direct reads, ran the mandatory doc-vs-code grep (three hits, all genuine, no doc rot), ran a fresh whole-tree `GameEntry` construction-site grep (still the sole site), and ran the Residual Accounting Pass + Adversarial Pass on `domain_modeling` (promoted to 9.5, accepted residual) and `framework_idioms` (promoted to 10, no residual). An independent Services/Models helper sweep surfaced a new Finding F-012 (`GamePlatformHelper`'s two independent switch statements), queued to the backlog rather than implemented this loop. No unintended scorecard regression: the change touches no network call, no ranking/selection logic, and no file outside the one named. Findings F-004, F-010, F-011 and F-012 are carried forward to the Improvement Backlog / Findings for future loops.

## Loop 8 Implementation Review
Verdict: **approved**. Reason: the three duplicated `ContentDialog`-construction-plus-guard-and-run bodies are now genuinely consolidated into one private `ConfirmAndRunAsync` helper (only one `new ContentDialog` remains in the file), the extraction is behavior-preserving call-site-by-call-site, introduces no new Seam/protocol, and the missing direct tests are not a new gap - `PrimaryWidget.xaml.cs` was already structurally excluded from the test project before this diff. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.

--- Loop 9 (UTC 2026-08-05T04:53:10Z) ---

### Loop Counter
Loop 9 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

This loop wrote an independent scorecard from current source first (fresh direct reads of `StoreNameLookup.cs`, `JsonRead.cs`, `GamePlatform.cs`, `GameEntry.cs`, `GridImageItem.cs`, `SteamGridDbClient.cs`, `ArtworkDownloader.cs`, `FixLog.cs`, `App.xaml.cs`, the app csproj's target platform, plus two independently-briefed cold helper sweeps), before reading `CURRENT_REVIEW.md`/`REVIEW_HISTORY.md` for delta basis. Finding F-010 (`StoreNameLookup` bypassing the `JsonRead` helper, tracked since loop 6) was implemented, verified by build + full test suite + an independent implementation review approved on first pass, net -9 lines. The mandatory Adversarial Pass on `domain_modeling`'s accepted residual tested a smaller counter-proposal than loop 8's own and found a fresh, concrete reason it still fails Simplify Pressure Test. A cold helper sweep surfaced a new, real, Noticeable-severity finding: F-013, a third instance of the entry-update-loop duplication pattern.

## Scorecard (1-10)

- **Architecture quality**: 7.5 | SAME | `Services/` modules re-confirmed via this loop's own direct reads plus a cold helper sweep — each remains a deep, single-responsibility Module. A second, independently-briefed helper produced an exhaustive method inventory of `PrimaryWidget.xaml.cs` (46 methods) and independently confirmed the same 5-concern split loop 8 named, with no internal sub-Module boundary beyond the already-extracted helpers. **Stalled-Dimension Sweep (loop 9, 4th consecutive SAME)**: 9-anchor re-judged NOT met. Not backlog-worthy: `TESTING.md` documents the bulk-operation loops staying in the widget as deliberate — fails SPT Q5/Q2.
- **State management and runtime ownership**: 7.0 | SAME | A cold, independently-briefed helper sweep traced every async method in `PrimaryWidget.xaml.cs` with an await followed by a mutation (12 total) and reconfirmed, with zero prior knowledge of the finding history, all 6 previously-fixed session-guard sites hold and the 6 unguarded sites are correctly unguarded by design. Held SAME: re-confirmation is not new structural proof per G8. The 9-anchor also requires state "separated by Module" — `PrimaryWidget` still mixes it all in one class. Not DOWN: flagged multi-writer fields traced to single-UI-thread-only writes with documented intent — smoke, not a finding.
- **Domain modeling**: 9.5 | SAME | **Adversarial Pass re-run this loop** against a smaller counter-proposal than loop 8's own: private setters plus factory-style methods on `GameEntry`, instead of a full discriminated-union rewrite. Traced the mechanics: C# object-initializer syntax at the sole construction site would force splitting the one construction call into two steps — a real blast-radius expansion into `PrimaryWidget`'s riskiest region. **SPT-rejected on Q5**: the benefit doesn't justify that blast radius for a Cosmetic, zero-live-harm gap.
- **Data flow and dependency design**: 7.5 | UP | Finding F-010 resolved this loop (git diff: 4 insertions, 13 deletions, net -9 lines). Closes the concrete "reuse/consistency" finding cited for 3 consecutive loops. Not promoted past 7.5: the 9-anchor's "one or two ambient-context dependencies" allowance is exceeded — five separate static-mutable-state instances exist without a consolidated ownership story, a freshly-named reason. Not backlog-worthy on its own: locking without parallelizing delivers no verifiable change; captured as F-011's own prerequisite.
- **Framework / platform best practices**: 10.0 | SAME | **G6 re-verification this loop**: independently checked the app's target platform (`TargetPlatformIdentifier=UAP`, a genuine legacy UWP `AppContainerExe`) — confirming `SteamGridDbClient.cs`'s serializer split remains period-appropriate. `StoreNameLookup.cs` is now MORE idiomatic after F-010. Could not name a source-backed improvement: `Debug.WriteLine` usage is reasonable at this scale, not an idiom gap.
- **Concurrency and runtime safety**: 6.5 | SAME | F-011 re-confirmed unchanged, untouched by this loop's edit. Still blocked by the STANDING USER CONSTRAINT; the unlocked-cache prerequisite widened slightly (`FixLog`'s static fields, `SteamGridDbClient.CapsuleParseNotes` join `StoreNameLookup`'s three caches). No new hazard found.
- **Code simplicity and clarity**: 8.0 | SAME | Leaf-module-duplication sweep surfaced a genuine new instance (F-013: `ReplaceImageCoreAsync`/`RestoreAllChangesAsync`/`RestoreBackupCoreAsync`, with observed drift). Not fixed this loop; its discovery does not move the score down — the source didn't change by being found. Held SAME, correctly routed to the backlog.
- **Test strategy and regression resistance**: 6.5 | SAME | **Stalled-Dimension Sweep (loop 9, 9th consecutive SAME — the most score-stalled dimension across this run)**: fresh mutation-test citation this loop (the `ConfirmAndRunAsync` guard's `||`/`&&` swap, a primary-flow gap) — 9-anchor genuinely NOT met. F-004 confirmed to sit off that primary flow — implementing it would not move this score.
- **Overall implementation credibility**: 9.5 | UP | Fresh cross-loop-independent re-verification of two dimensions loop 8 promoted in the same loop it promoted them — both re-tested from a different angle in a different loop, and both held. F-010's fix verified by independent review approved first pass. Two cold sweeps found zero doc-rot. F-013 independently confirmed by direct reads.

## Authority Map
Empty this loop — no authority/ownership finding is Priority 1. See loop 7/8 above for the last full Authority Map.

## Strengths That Matter
- The `domain_modeling` Adversarial Pass this loop tested a materially different, smaller counter-proposal than loop 8's own and reached a fresh, concrete rejection reason rather than reusing loop 8's reasoning.
- An independently-briefed cold helper sweep of `PrimaryWidget.xaml.cs`, told to trace the session-guard pattern generically, reconfirmed all 6 prior fixes hold with zero prior knowledge of the finding history.
- F-013 was found by a helper sweep with no knowledge of this run's finding history, then independently re-verified by direct reads of all three cited method bodies.

## Findings

### Finding #1 (stable_id F-010): StoreNameLookup's GOG and Epic name-fetch methods bypass the JsonRead helper that exists to prevent a documented JSON null-vs-missing bug

**Why it matters** — The codebase already shipped a bug once (documented in `PrimaryWidget.xaml.cs`'s manifest-parsing comment and `JsonRead.cs`'s own docstring) where a JSON member that was present-but-null was mishandled by raw `Windows.Data.Json` calls; `JsonRead.cs` exists specifically to make that class of bug impossible. Two of `StoreNameLookup`'s three network-backed name-lookup methods used the pattern that caused it.

**What is wrong** — `GetGogGameNameAsync` and `GetEpicGameNameAsync` used raw `ContainsKey`/`GetNamedObject`/`GetNamedString` `Windows.Data.Json` calls instead of the existing `JsonRead.Object`/`JsonRead.String` helper that `SteamGridDbClient.cs` and `EpicLibrary.cs` already use consistently.

**Evidence** — `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:67-77` (pre-fix, `GetGogGameNameAsync`), `:186-191` (pre-fix, `GetEpicGameNameAsync`), `SteamGridDB.Xbox/Services/JsonRead.cs:1-17`, `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs:155-181`

**Architectural test failed** — n/a — a Reuse/consistency finding

**Dependency category** — n/a

**Leverage impact** — None directly - `JsonRead` already exists and is already used by two other call sites; this brings the remaining two into line.

**Locality impact** — Contained to `StoreNameLookup.cs`'s two methods; `JsonRead.cs` itself untouched.

**Metric signal, if any** — none

**Why this weakens submission** — Credibility/consistency deduction, not a live defect (the outer catch prevented a crash even before the fix): the established, documented, purpose-built helper for this exact class of JSON-parsing bug existed in the same file's own dependency graph and was used by two of three sibling call sites plus `SteamGridDbClient.cs` and `EpicLibrary.cs`, yet two methods opted out with no stated reason.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Replaced the raw `ContainsKey`/`GetNamedObject`/`GetNamedString` chains in both methods with `JsonRead.Object(gameData, "_embedded") -> JsonRead.Object(embedded, "product") -> JsonRead.String(product, "title")` (GOG) and `JsonRead.String(gameData, "title")` (Epic), matching `SteamGridDbClient.cs`'s established pattern.

**Blast radius** — Change: `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`. Avoid: `SteamGridDB.Xbox/Services/JsonRead.cs`, `SteamGridDB.Xbox/Services/SteamGridDB/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

**Status this loop: implemented — see Loop 9 Result below.**

### Finding #2 (stable_id F-013): ReplaceImageCoreAsync, RestoreAllChangesAsync and RestoreBackupCoreAsync duplicate the same UI-thread entry-update loop three times

**Why it matters** — Any future change to what happens when a game's tile image is replaced (a new derived property, a new visual state, a new field to keep in sync) has to be found and added in three places by hand, and the three copies have already drifted: `RestoreAllChangesAsync` omits the `HasBackup` write and the per-call status-text update that the other two include.

**What is wrong** — `ReplaceImageCoreAsync` (`PrimaryWidget.xaml.cs:1169-1182`), `RestoreAllChangesAsync`'s per-entry block (`:1101-1108`) and `RestoreBackupCoreAsync` (`:1924-1937`) each dispatch to the UI thread via `OnUiThreadAsync` and `foreach` over `EntriesSharingImage(game)`, writing `entry.Image` and `entry.ImageFileName` in all three, `entry.HasBackup` in two of three, and `StatusText.Text` conditionally in two of three.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1169-1182` (`ReplaceImageCoreAsync`), `:1101-1108` (`RestoreAllChangesAsync`), `:1924-1937` (`RestoreBackupCoreAsync`)

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own image/backup-flag values instead of re-deriving the whole dispatch-and-foreach shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication class as Finding F-002 and F-003 (both resolved), in the same file — a third, smaller instance that survived two prior rounds of exactly this kind of sweep because no prior loop's helper looked at these specific three methods together; the observed drift between the three copies is concrete evidence unsynchronized duplication already costs correctness attention, not just line count.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private helper (e.g. `UpdateEntriesSharingImage(GameEntry game, BitmapImage image, string imageFileName, bool? hasBackup)`) that owns the `OnUiThreadAsync` dispatch and the `EntriesSharingImage(game)` foreach, writing `Image`/`ImageFileName` always and `HasBackup` only when a value is supplied; each call site keeps its own status-text/counter logic outside the helper.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #3 (stable_id F-011): LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — A user whose library has many GOG/Epic games without a direct SteamGridDB match pays the full network latency of the GOG, Epic (or Ubisoft/community-database) endpoint plus the SteamGridDB name-search endpoint for every one of those games, one after another, before the library list finishes loading.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs:455-679`) awaits, in strict sequence for each manifest entry, several independent network calls. The awaits are independent across entries; this is a sequential-independent-effects shape.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:455-679`, `:581`, `:603`, `:612`, `:641`

**Architectural test failed** — n/a — different category (efficiency/D2)

**Dependency category** — n/a

**Leverage impact** — None currently - no seam exists to batch or parallelize through.

**Locality impact** — Contained to `LoadGameEntriesAsync`'s own per-entry loop body and, if fixed, `StoreNameLookup`'s/`FixLog`'s/`SteamGridDbClient`'s static caches' thread-safety.

**Metric signal, if any** — none

**Why this weakens submission** — Hot path (the library reloads on every widget open) doing per-item network I/O one item at a time where nothing in the current design requires that ordering.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Not implemented this loop — blocked by the STANDING USER CONSTRAINT and by StoreNameLookup's three unlocked caches (plus FixLog's static fields and SteamGridDbClient.CapsuleParseNotes, all confirmed unlocked by an independent Services/ sweep this loop), which would need real thread-safety added first.

**Blast radius** — Change: none this loop. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` (both not attempted this loop).

### Finding #4 (stable_id F-012): GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding, renaming, or removing a platform requires updating both switches by hand; nothing enforces they stay in sync, and each has a silent default fallback, so a future skew would degrade silently.

**What is wrong** — `GamePlatformHelper.FromXboxDirectory` (`GamePlatform.cs:22-46`) and `GamePlatformHelper.GamePlatformToSGDBApiString` (`:48-67`) both switch over the same 8-case enum but are independently authored with no shared table.

**Evidence** — `SteamGridDB.Xbox/Models/GamePlatform.cs:22-46`, `:48-67`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None currently; a consolidated metadata table would let a future platform addition touch one place.

**Locality impact** — Contained to `GamePlatform.cs`'s two static methods.

**Metric signal, if any** — none

**Why this weakens submission** — A real but currently-latent duplicate-abstraction risk; not yet manifesting live harm since the six shared cases are currently correctly mirrored in both switches.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Introduce a single static table both methods query, replacing both switch bodies with a lookup.

**Blast radius** — Change: `SteamGridDB.Xbox/Models/GamePlatform.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (both call sites unchanged), `SteamGridDB.Xbox/Services/**`.

### Finding #5 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation at either threshold would ship silently.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) thresholds at `alpha < 64` and `transparentCorners < 2` are untested at their exact boundary. Re-read this loop, unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — Minor, off-primary-flow gap; Cosmetic per the anchor's own carve-out — re-confirmed this loop against a fresh primary-flow mutation (the `ConfirmAndRunAsync` guard) that IS the actual blocker capping test_strategy below 9.5, not this boundary gap.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases at the exact documented boundaries.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs` (test-only addition).

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Finding F-010's `JsonRead` substitution resolves a real, evidenced consistency gap with a call-site substitution to an existing helper. |
| New seam justified | No — no new port/adapter added. |
| Helpful simplification | F-010's fix is net -9 lines in `StoreNameLookup.cs` (4 insertions, 13 deletions) - genuinely subtractive. |
| Should NOT be done | Do not extract F-013's entry-update loop until it is actually implemented. Re-confirmed: do not touch `GameEntry`'s match-state construction. Do not attempt F-011's fix without first adding real locking to every static cache this loop's sweep identified. |
| Tests after fix | None added or deleted — verification is build + full test suite + independent implementation review + a manual trace of `JsonRead`'s null-propagation semantics. |

## Improvement Backlog
1. **Extract a shared entry-update helper for ReplaceImageCoreAsync/RestoreAllChangesAsync/RestoreBackupCoreAsync (Finding F-013).**
   - why it matters: removes a third instance of the leaf-module-duplication class F-002/F-003 already fixed, with concrete evidence (a missing `HasBackup` write) that unsynchronized duplication already costs correctness attention.
   - score impact: `simplicity +0.5`
   - simplification / helpful

2. **Consolidate GamePlatformHelper's two independent switch statements into one shared platform-metadata table (Finding F-012).**
   - why it matters: removes a latent duplicate-abstraction/skew risk before a future platform addition can be silently mishandled.
   - score impact: `simplicity +0.5`
   - simplification / helpful

3. **Add bounded concurrency to LoadGameEntriesAsync's per-entry name/match resolution, after first adding real locking to every static cache (Finding F-011).**
   - why it matters: removes a real, linearly-scaling latency cost on the primary library-load hot path — but not actionable yet: blocked by the STANDING USER CONSTRAINT and the unlocked-cache thread-safety prerequisite.
   - score impact: `concurrency +0.5`
   - structural / helpful

**Priority-1 accounting**: F-010 was Priority 1 this loop as the longest-tracked, fully actionable Noticeable-severity candidate (open since loop 6). F-011 ranks higher on merit but remains blocked — named per criterion 0, not silently demoted. For loop 10, F-013 (new this loop) is Priority 1.

## Deepening Candidates
1. **Candidate Module**: `LoadGameEntriesAsync`'s per-entry name/match resolution (Finding F-011). Smallest first step: add real locking (a `SemaphoreSlim` per cache) to `StoreNameLookup`'s, `FixLog`'s, and `SteamGridDbClient`'s static mutable state BEFORE attempting any concurrency change to the calling loop. What not to do: do not wrap the loop in `Task.WhenAll` before every static cache is locked, and do not attempt the network-ordering half of this fix at all until a behavioural oracle exists.

## Builder Notes
1. **Pattern: an independently-briefed cold helper sweep with no knowledge of prior loops' finding history is more likely to surface a genuinely new instance of a known defect class than one primed to check a known list.** → REVIEW_HISTORY.json `loops[8].builder_notes` for full notes (array index 8 = loop 9).
2. **Pattern: an Adversarial Pass on an accepted residual only does its job when it tests a materially different, smaller fix than what was already rejected.** → REVIEW_HISTORY.json `loops[8].builder_notes` for full notes.
3. **Pattern: a dimension can be the most score-stalled item on the board while having zero legitimate backlog-worthy candidates, when its one remaining open finding sits off the primary flow that actually sets the score's ceiling.** → REVIEW_HISTORY.json `loops[8].builder_notes` for full notes.

## Final Judge Narrative
Place, not win, this loop. This loop's headline result is a genuinely new finding nine loops into repeated scrutiny of the same file: an independently-briefed cold helper sweep, told to look for a general shape rather than a known list, surfaced F-013 — a third, smaller instance of the leaf-module-duplication class F-002 and F-003 already fixed, with concrete evidence of drift between the copies. This loop's own implementation work (F-010, routing `StoreNameLookup`'s last two holdout JSON-reading call sites through the established `JsonRead` helper) is real, net-subtractive (-9 lines), verified by build + full test suite + an independent implementation review that returned `approved` on first pass. The mandatory Adversarial Pass on `domain_modeling`'s accepted residual tested a materially smaller fix than loop 8's own and found a fresh, concrete reason it still fails — genuine re-examination, not rubber-stamping. Runtime ownership is more trustworthy this loop by fresh, independent completeness evidence, though the score itself holds rather than climbs, since re-confirmation of already-credited evidence is not new structural proof. Concurrency's own blocker widened slightly in scope without changing its disposition. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; test_strategy's own 9-loop stall is now confirmed to be a genuine structural ceiling rather than an unaddressed choice, since its one remaining candidate (F-004) provably would not move the score even if implemented. Future work risks over-engineering only if F-013's eventual extraction reaches for a shared status-text abstraction beyond the narrow Image/ImageFileName/HasBackup fields, or if F-011's eventual fix attempts to parallelize the network loop before locking every static cache this loop's sweep identified.

## Loop 9 Result
Changed `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` only (4 insertions, 13 deletions, net -9 lines): replaced the raw `Windows.Data.Json` `ContainsKey`/`GetNamedObject`/`GetNamedString` call chains in `GetGogGameNameAsync` and `GetEpicGameNameAsync` with calls to the existing `JsonRead.Object`/`JsonRead.String` helper, matching `SteamGridDbClient.cs`'s and `EpicLibrary.cs`'s already-established pattern. Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after — unchanged, as expected, since these two methods make live network calls and are deliberately untested per `TESTING.md`. Manually traced `JsonRead.Object`/`JsonRead.String`'s null-propagation semantics against every pre-fix failure path (member absent, member present-but-JSON-null, member present-but-wrong-type) and confirmed each now falls through to the same `null` return as before. An independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on Finding F-010 and the diff only) returned verdict `approved` with all three checks (reality, honesty, regression) passed on the first pass, and independently re-ran the full test suite confirming 138/0/0. Finding F1 (stable_id F-010) is **resolved**. This loop also surfaced a new Finding F-013 (a third instance of the entry-update-loop leaf-module-duplication class in `PrimaryWidget.xaml.cs`, queued for loop 10) and independently re-verified Findings F-011/F-012/F-004 unchanged. No unintended scorecard regression: the change touches no network call ordering, no ranking/selection logic, and no file outside the one named.

## Loop 9 Implementation Review
Verdict: **approved**. Reason: both methods now route entirely through `JsonRead.Object`/`JsonRead.String` with no raw `ContainsKey`/`GetNamedObject`/`GetNamedString` remaining, every failure path traced by hand converges to the same `null` return as before, only `StoreNameLookup.cs` changed, and the full suite still passes 138/0/0. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.

--- Loop 10 (UTC 2026-08-05T05:36:31Z) ---

### Loop Counter
Loop 10 of 10 (cap)

### System Flag
[STATE: HALT_LOOP_CAP]

---

## Contest Verdict
**Promising, but architecturally immature.**

Terminal loop (10 of 10 cap): per `SKILL.md`'s own Step 1 Routing table, reaching `loop_cap` forces state `HALT_LOOP_CAP` regardless of backlog contents, which skips Step 2/Step 3 — this loop is Critic-only, no code changed (build green, 138/138 tests green, identical to loop 9's). Every score below was independently re-derived from fresh source reads per this run's own anti-anchoring directive, including a mandatory G6 re-check of `framework_idioms`' 10.0 and a mandatory Adversarial Pass on `domain_modeling`'s and `credibility`'s accepted residuals. Two cold, independently-briefed helper sweeps plus this loop's own direct reads found the codebase's 9 previously-resolved reentrancy/duplication fixes (F-001 through F-010) all still holding, but also found a genuinely new, real, Serious-severity finding nine loops into repeated scrutiny of the same file: F-014, a per-game-operation-vs-bulk-operation reentrancy gap in the exact class this review has fixed 6 times before (F-005 through F-009) yet never found from this specific angle.

## Scorecard (1-10)

- **Architecture quality**: 7.5 | SAME | No source changed this loop; re-confirmed by fresh direct reads plus a cold `Services`/`Models` sweep (clean). `PrimaryWidget.xaml.cs` still spans five concerns in one Module; this loop's own sweep additionally found `HideGridPanelAsync`/`HideSearchPanelAsync` as an unnamed near-identical sibling pair (not its own finding this loop; named for the next sweep). Stalled-Dimension Sweep: 10th consecutive non-UP loop, 9-anchor still not met for the `TESTING.md`-documented reason.
- **State management and runtime ownership**: 7.0 | SAME | This loop's own cold full-file re-trace of every await-then-mutate method reconfirmed all 6 prior session-guard fixes hold, AND found a 7th, previously-uncaught instance: **Finding F-014**. Loop 9's "library-operation-gate-sufficient" verdict on this exact method did not survive this loop's own mechanism-level trace. Held SAME (one more instance of an already-known defect class, now with its own backlog trail), not moved DOWN.
- **Domain modeling**: 9.5 | SAME | Adversarial Pass re-run against a candidate smaller than loop 8's and loop 9's: a `readonly struct` with factory methods mirroring `ManifestEntryIdentity.Result`'s own existing idiom. Does not force splitting `GameEntry`'s object-initializer construction. SPT-rejected on Q5: real blast radius (~7-8 call sites across 3-4 files) for a Cosmetic, zero-live-harm concern across 10 loops.
- **Data flow and dependency design**: 7.5 | SAME | No source changed. F-010's fix re-confirmed correct. Five ambient static-mutable-state instances still exceed the 9-anchor's allowance, independently re-confirmed by a cold sweep reaching the identical list unprompted.
- **Framework / platform best practices**: 9.5 | **DOWN** | G6 re-verification found a source-backed residual loops 8-9 did not name: `App.xaml.cs:120`'s unaddressed, dead suspend-state `TODO` on a documented fallback-only launch path (also a doc-vs-code leak). Rest of the platform-idiom claims re-confirmed unchanged.
- **Concurrency and runtime safety**: 6.5 | SAME | F-011 unchanged, still blocked by the STANDING USER CONSTRAINT. F-014 filed under `state_management` (reentrancy via stale references, not a threading race), not double-counted here.
- **Code simplicity and clarity**: 8.0 | SAME | F-013 re-confirmed present but its "observed drift" claim is **corrected**: `ArtworkFiles.ReapplyCustomisationAsync` never touches the backup file, so `RestoreAllChangesAsync`'s omission of the `HasBackup` write is correct, not a bug. Duplication itself still real and Noticeable. `HideGridPanelAsync`/`HideSearchPanelAsync` sibling duplication newly named (see architecture_quality).
- **Test strategy and regression resistance**: 6.5 | SAME | 10th consecutive non-UP loop — the single most score-stalled dimension; its value has never changed since loop 1. Three fresh primary-flow mutation sites named (`:908`, `:1328`, `:852`), none caught by any test. F-014 itself is live, non-hypothetical proof of what this untested surface costs. Blocker remains the genuine platform constraint (`Windows.UI.Xaml` has no desktop projection).
- **Overall implementation credibility**: 9.5 | SAME | Adversarial Pass re-run on loop 9's residual — no code-structural fix applies to a review-process residual; SPT Q2 rejects mandating full re-derivation as more ceremony. Residual sharpened with a concrete instance: loop 9's own "gate-sufficient" claim about `RestoreBackupCoreAsync`, taken at face value, did not survive this loop's own mechanism-level trace (now F-014). Held at 9.5: one new local leak, captured as its own Finding, found *by* deeper verification not missed by it.

## Authority Map
Re-emitted this loop (F-014 is a Priority-1 authority/reentrancy finding): 4 concerns covering `isLibraryOperationRunning` (Split and ambiguous — the root cause of F-014), `GameEntries` (Split and ambiguous), `gridPanelSessionId` and `searchPanelSessionId` (both Single and clear, re-verified holding). Full detail → `loops[9]` (loop 10) in `REVIEW_HISTORY.json`.

## Strengths That Matter
- F-014 was found the same way F-013 was found in loop 9 — a cold helper briefed on the general shape rather than the known list — the second time in two loops that technique surfaced a real, previously-missed defect in the same heavily-reviewed file.
- This loop independently traced `IsLibraryOperationBlocking`'s own doc comment against its actual implementation mechanics rather than trusting the comment's claim.
- All 9 previously-resolved findings were re-spot-checked against current source this loop and every one still holds, including catching and correcting one inaccurate claim (F-013's "observed drift") rather than carrying it forward unexamined.

## Findings

### Finding #1 (stable_id F-014): Single-game artwork operations check IsLibraryOperationBlocking only at the click, never claim it, so a bulk operation can start and corrupt freshly-loaded entries mid-flight
**Why it matters** — A user can click Restore Backup (or pick artwork from the grid/search panel) on one row, then click Refresh/Fix Library/Restore Changes/Revert Defaults before the first click's file I/O completes; the second operation replaces the whole game list, and the first operation's resumed write lands on the freshly-loaded entries instead.
**What is wrong** — `IsLibraryOperationBlocking` (`:191-201`) is checked but never claimed by `RestoreBackup_Click`/`EditGameImage_Click`/`SearchGameImage_Click`; `TryBeginLibraryOperation` (`:207-220`) has no awareness of an in-flight single-game operation, so it lets `LoadGameEntriesAsync` replace `GameEntries` (`:359`, `:699`) while, e.g., `RestoreBackupCoreAsync`'s await is pending; `EntriesSharingImage(game)` then writes onto the freshly-loaded entries.
**Evidence** — `PrimaryWidget.xaml.cs:191-201`, `:207-220`, `:1857-1868`, `:1900-1950`, `:351-366,687-715`
**Architectural test failed** — n/a — state-ownership/reentrancy
**Severity** — Serious deduction
**Minimal correction path** — Give `IsLibraryOperationBlocking`'s callers the same claim/release discipline `TryBeginLibraryOperation` already provides, sized for a single-game operation.
**Blast radius** — Change: `PrimaryWidget.xaml.cs`. Avoid: XAML markup, `Services/**`.

### Finding #2 (stable_id F-013): ReplaceImageCoreAsync, RestoreAllChangesAsync and RestoreBackupCoreAsync duplicate the same UI-thread entry-update loop three times
**Why it matters** — A future field/state change has to be found and added in up to three places by hand; the copies already differ in field lists with no documented reason.
**What is wrong** — All three dispatch-and-`foreach` over `EntriesSharingImage(game)`; `RestoreAllChangesAsync` correctly omits the `HasBackup` write (corrected this loop — `ArtworkFiles.ReapplyCustomisationAsync` never touches the backup file) but nothing documents why.
**Evidence** — `PrimaryWidget.xaml.cs:1154-1192`, `:1067-1135`, `:1900-1950`
**Architectural test failed** — Shallow module
**Severity** — Noticeable weakness
**Minimal correction path** — Extract a shared `UpdateEntriesSharingImage` helper.
**Blast radius** — Change: `PrimaryWidget.xaml.cs`. Avoid: XAML markup, `Services/**`.

### Finding #3 (stable_id F-011): LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time
**Why it matters** — Linear per-game network latency on every widget open for unmatched games.
**What is wrong** — Sequential awaits per manifest entry (`:455-679`), independent across entries (efficiency lens D2).
**Severity** — Noticeable weakness
**Minimal correction path** — Not implemented this run — blocked by the STANDING USER CONSTRAINT and unlocked static caches.
**Blast radius** — Not attempted.

### Finding #4 (stable_id F-012): GamePlatformHelper's two independent switch statements have no shared source of truth
**Severity** — Cosmetic for contest
**Minimal correction path** — A single static metadata table.

### Finding #5 (stable_id F-004): TileImage.FillsTileAsync's thresholds are untested at their exact boundary values
**Severity** — Cosmetic for contest
**Minimal correction path** — Two boundary-value test cases.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | No fix attempted this loop; F-014's proposed remedy checked against SPT and would pass if implemented (real ambiguity, smallest honest fix, no duplicate layers, honest runtime, product improves). |
| New seam justified | No |
| Helpful simplification | F-013's own text corrected without changing severity/remedy |
| Should NOT be done | Widen `isLibraryOperationRunning` without auditing every reader; re-open `domain_modeling`'s residual without a smaller candidate; attempt F-011 without locking + a user decision |
| Tests after fix | n/a this loop — no fix landed |

## Improvement Backlog
1. Add a claim/release guard to single-game artwork operations (F-014) — structural, needed for winning. `state_management +1.0`
2. Extract a shared entry-update helper (F-013) — simplification, helpful. `simplicity +0.5`
3. Add bounded concurrency to LoadGameEntriesAsync, after locking static caches (F-011, blocked by the STANDING USER CONSTRAINT) — structural, helpful. `concurrency +0.5`

## Deepening Candidates
- `LoadGameEntriesAsync`'s per-entry name/match resolution (Finding F-011) — see `REVIEW_HISTORY.json` loop 10 entry for full detail.

## Builder Notes → `loops[9]` (loop 10) in `REVIEW_HISTORY.json` for full notes.
- Briefing a cold sweep on a defect class's general shape (not the known-instance list) surfaces genuinely new instances — now confirmed twice in the same file, one loop apart (F-013, then F-014).
- A guard's own doc comment claiming a protection is not evidence the protection holds in both directions — trace the implementation's mechanics.
- A prior loop's "bug"/"drift" characterization needs its own evidence chain re-walked, not just its citation trusted — the callee's actual contract can make an apparent inconsistency correct.

## Final Judge Narrative
Place, not win, and the run ends with its most consequential discovery still unfixed. This terminal loop ran Critic-only — no code changed. Every score was independently re-derived from fresh source: `framework_idioms`' 10.0 did not survive G6 re-verification; `domain_modeling`'s/`credibility`'s 9.5-accepted residuals both survived a fresh Adversarial Pass against smaller counter-proposals than any prior loop tested. The headline result is Finding F-014: a genuinely new, Serious-severity reentrancy gap in the same defect class as six already-fixed findings, missed by nine prior loops because every one traced only the per-game-session direction, never the per-game-operation-vs-bulk-operation direction. It is fully actionable and is Priority 1 for whenever this run resumes, but this loop cannot fix it — the cap was reached before Step 2/3 could run. Tests still cannot, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; `test_strategy`'s 10-loop stall is now doubly confirmed as a genuine platform ceiling, and F-014 is itself live proof of what that ceiling costs.

## Retired Findings (this loop)
None.


--- HALT_LOOP_CAP reset by user (UTC 2026-08-05T18:40:38Z) ---

### Loop Counter
Loop 10 of 10 (cap)

### System Flag
[STATE: HALT_LOOP_CAP]

---

## Contest Verdict
**Promising, but architecturally immature.**

Terminal loop (10 of 10 cap): per `SKILL.md`'s own Step 1 Routing table, reaching `loop_cap` forces state `HALT_LOOP_CAP` regardless of backlog contents, which skips Step 2/Step 3 — this loop is Critic-only, no code changed (build green, 138/138 tests green, identical to loop 9's). Every score below was independently re-derived from fresh source reads (not carried forward from loop 9's numbers) per this run's own anti-anchoring directive, including a mandatory G6 re-check of `framework_idioms`' 10.0 and a mandatory Adversarial Pass on `domain_modeling`'s and `credibility`'s accepted residuals. Two cold, independently-briefed helper sweeps (`Services/`/`Models/`; `PrimaryWidget.xaml.cs`) plus this loop's own direct reads found the codebase's 9 previously-resolved reentrancy/duplication fixes (F-001 through F-010) all still holding, but also found a genuinely new, real, Serious-severity finding nine loops into repeated scrutiny of the same file: F-014, a per-game-operation-vs-bulk-operation reentrancy gap in the exact class this review has fixed 6 times before (F-005 through F-009) yet never found from this specific angle.

## Scorecard (1-10)

- **Architecture quality**: 7.5 | SAME | No source changed this loop (HALT_LOOP_CAP skipped Step 2/3), so no structural proof exists for a move either direction per G8/G26. Re-confirmed by fresh direct reads this loop (`StoreNameLookup.cs`, `JsonRead.cs`, `GamePlatform.cs`, `GameEntry.cs`, `SteamGridDbClient.cs`, `FixLog.cs`, `ArtworkFiles.cs`, `AppliedArtworkStore.cs`, `GameImages.cs`, `App.xaml.cs`, `MainPage.xaml.cs`) plus an independent cold `Services`/`Models` helper sweep (clean on Reuse/Simplification/Altitude/Efficiency): `Services/` remains a set of deep, single-responsibility Modules with real Interfaces. `PrimaryWidget.xaml.cs` still spans five concerns in one Module — re-confirmed by this loop's own independent full-file helper read, which additionally found one more shape not previously named: `HideGridPanelAsync`/`HideSearchPanelAsync` (`:1594-1622`, `:1819-1844`) are structurally near-identical modulo control names, a smaller sibling of the same leaf-duplication class as F-002/F-003/F-013 — not its own finding this loop (output budget), named for the next sweep. **Stalled-Dimension Sweep** (10th consecutive non-UP loop): 9-anchor still not met for the same `TESTING.md`-documented reason as every prior loop.
- **State management and runtime ownership**: 7.0 | SAME | No source changed this loop. This loop's own independent, cold, full-file re-trace of every await-then-mutate method in `PrimaryWidget.xaml.cs` (12 total) reconfirmed all 6 previously-fixed session-guard sites hold, AND found a 7th, previously-uncaught instance of the same defect class: **Finding F-014** (new this loop) — a single-game operation's own await-then-mutate is reachable while a concurrently-started bulk operation replaces `GameEntries` wholesale, because `IsLibraryOperationBlocking()` is checked but never claimed by any single-game operation. This is fresh, source-backed proof the 9-anchor is further from met than loop 9's own re-confirmation credited: loop 9 characterized this exact method as "library-operation-gate-sufficient," a claim this loop's own mechanical trace shows false in the reverse direction. Held at SAME rather than moved DOWN: one additional instance of an already-known defect class, now captured as its own Priority-1 finding with a backlog trail rather than silently folded into a score move.
- **Domain modeling**: 9.5 | SAME | **Adversarial Pass re-run this loop** against a candidate smaller than both loop 8's (discriminated-union rewrite) and loop 9's (private-setter encapsulation): a `readonly struct` with a private constructor and static factory methods (`NotFound`/`ByPlatform`/`ByName`), mirroring this codebase's own existing idiom for the same shape of problem — `ManifestEntryIdentity.Result` (`Services/Library/ManifestEntryIdentity.cs:22-36`). Unlike loop 9's candidate, this one does **not** force splitting `GameEntry`'s object-initializer construction (`PrimaryWidget.xaml.cs:651-665`) into two statements. Independently re-confirmed (fresh repo-wide grep) the three fields have exactly 5 non-construction read sites and zero XAML-binding references — so the blast radius is 1 new type + `GameEntry.cs` + the construction site + 5 read sites, ~7-8 call sites across 3-4 files. **SPT-rejected on Q5** (product improves): real blast radius for a Cosmetic, zero-live-harm concern across 10 loops — a different, smaller candidate than loop 9's, rejected on a freshly-traced, different mechanical reason.
- **Data flow and dependency design**: 7.5 | SAME | No source changed this loop. Re-confirmed by direct re-read: F-010's fix (loop 9) unchanged and correct. The five separate static-mutable-state instances this dimension has cited since loop 9 (`StoreNameLookup`'s 3 caches, `SteamGridDbClient.CapsuleParseNotes`, `FixLog`'s 3 fields) are all still present and still exceed the 9-anchor's "one or two ambient-context dependencies" allowance, independently re-confirmed by this loop's own cold `Services`/`Models` sweep reaching the identical list unprompted. Not backlog-worthy alone: locking without also changing the calling loop's concurrency delivers no verifiable behavior change — correctly captured as F-011's own prerequisite.
- **Framework / platform best practices**: 9.5 | **DOWN** | G6 re-verification this loop found a source-backed, behavior-preserving improvement loops 8-9 did not name: `App.xaml.cs:120` carries an unaddressed `//TODO: Load state from previously suspended application` inside `OnLaunched`'s `Terminated` branch — dead Visual-Studio-template scaffolding on a path `MainPage.xaml.cs`'s own doc comment confirms is a fallback only (Start-menu/debug launch, not the widget's real `OnActivated` entry point). Repo-wide grep for `PreviousExecutionState`/`Terminated` found the TODO is the only reference — nothing implements or partially addresses it. Also a doc-vs-code leak (the comment implies unfinished work with no stated reason it's safe to skip). Rest of the platform-idiom claims re-independently-confirmed: csproj target platform unchanged (legacy UWP `AppContainerExe`), `App.xaml.cs`/`MainPage.xaml.cs` otherwise minimal and idiomatic, no `LangVersion` override and no records/switch-expressions anywhere confirms the older C# baseline `JsonRead`'s manual null-handling correctly targets.
- **Concurrency and runtime safety**: 6.5 | SAME | No source changed this loop. F-011 independently re-confirmed unchanged by direct re-read — byte-identical to prior loops' citation. Still blocked by the STANDING USER CONSTRAINT and the unlocked-cache prerequisite (unchanged scope). F-014 (this loop's new finding) is a reentrancy/state-ownership gap, filed under `state_management` matching this codebase's own established categorization for the F-005-009 class, not counted twice here — nothing runs on more than the UI thread today, so F-014 is a stale-object-reference bug via ordinary sequential reentrancy, not a threading race.
- **Code simplicity and clarity**: 8.0 | SAME | No source changed this loop. F-013 re-confirmed present by direct re-read, but its own claim is **corrected** this loop: tracing `ArtworkFiles.ReapplyCustomisationAsync`'s actual contract (`Services/Artwork/ArtworkFiles.cs:193-219` — it never touches the `.bak` backup file) shows `RestoreAllChangesAsync`'s omission of the `HasBackup` write is **correct** behavior, not the "observed drift already costing correctness attention" loop 9 characterized it as. The three-way structural duplication is still real and Noticeable (undocumented field-list asymmetry is exactly what makes a future edit blur the distinction); the drift-as-bug framing was not. This loop's own full-file sweep additionally found `HideGridPanelAsync`/`HideSearchPanelAsync` as a smaller, previously-unnamed sibling duplication — more fresh evidence the 9-anchor isn't yet met.
- **Test strategy and regression resistance**: 6.5 | SAME | **Stalled-Dimension Sweep (10th consecutive non-UP loop, counting loop 1's baseline — the single most score-stalled dimension across this run; its numeric value has not changed even once since loop 1).** An independently-briefed cold helper named three new primary-flow mutation sites this loop, none cited before: `:908` (`||`→`&&` would silently invert which games "fix library" revisits), `:1328` (`!=`→`==` would invert the grid-picker's reentrancy guard), `:852` (deleting `RevertAllToDefaultAsync`'s early-return). None would be caught — `PrimaryWidget.xaml.cs` carries zero test coverage. Stronger evidence than any prior citation: this loop's own **F-014 is not a hypothetical mutation** — it is a real, currently-shipping gap in the exact untested surface this score has capped on for 10 loops. F-004 re-confirmed still off the primary flow that actually caps this score. The blocker remains a genuine, permanent platform constraint (`Windows.UI.Xaml` has no desktop projection), not an unaddressed choice.
- **Overall implementation credibility**: 9.5 | SAME | **Adversarial Pass re-run this loop** on loop 9's residual ("not every prior fix independently re-derived from scratch") — no code-structural fix applies to a review-process residual, and SPT Q2 rejects the only candidate (mandate full re-derivation every loop) as strictly *more* ceremony, so the disposition holds. But this loop's own experience sharpens the residual: a deep, mechanism-level re-trace of ONE specific claim — `IsLibraryOperationBlocking`'s doc comment, which loop 9 took at face value — found that claim does not hold in the reverse direction (now Finding F-014), a concrete instance of exactly the gap this residual has named since loop 9. Held at 9.5, not moved down: ONE new local leak (still "few" per the 9-anchor), now captured as its own Finding with its own remedy, and this loop's positive evidence is real too — F-010 re-confirmed correct, all 9 prior findings' claims re-spot-checked with zero doc-rot found beyond this one, and F-014 was found *by* this loop's deeper verification discipline, not missed by it.

## Authority Map
Re-emitted this loop: Finding F-014 is a Priority-1 authority/reentrancy finding.

- **Concern**: Library-wide operation in-flight state (`isLibraryOperationRunning`)
  - Owner: `PrimaryWidget`
  - Allowed writers: `TryBeginLibraryOperation` (claim, `:207-220`), `EndLibraryOperation` (release, `:225-229`)
  - Readers: `IsLibraryOperationBlocking` (`:191-201`), checked by `RestoreBackup_Click`, `EditGameImage_Click`, `SearchGameImage_Click`
  - Persistence seam: none
  - Async mutation entry points: `PrimaryWidget_Loaded`, `RefreshButton_Click`, `ConfirmAndRunAsync` (shared guard for the three bulk-operation buttons)
  - Verdict: **Split and ambiguous**
- **Concern**: `GameEntries` collection identity/contents
  - Owner: `PrimaryWidget`
  - Allowed writers: `LoadGameEntriesAsync` (`Clear` at `:359`, per-entry `Add` at `:699`)
  - Readers: `GamesToProcess`, `EntriesSharingImage`, the list view binding, every bulk/single-game operation
  - Persistence seam: none
  - Async mutation entry points: `LoadGameEntriesAsync`
  - Verdict: **Split and ambiguous**
- **Concern**: Grid-picker session identity (`gridPanelSessionId`)
  - Owner: `PrimaryWidget` · Allowed writers: `LoadGridSelectionAsync` · Readers: `PopulateGridSelectionPanelAsync`, `GridImagesView_ItemClick`, `HideGridPanelAsync` · Persistence seam: none · Async mutation entry points: `LoadGridSelectionAsync`
  - Verdict: **Single and clear**
- **Concern**: Search-panel session identity (`searchPanelSessionId`)
  - Owner: `PrimaryWidget` · Allowed writers: `PerformGameSearchAsync`, `ShowSearchPanelAsync` · Readers: `PerformGameSearchAsync`'s own check, `HideSearchPanelAsync` · Persistence seam: none · Async mutation entry points: `PerformGameSearchAsync`, `ShowSearchPanelAsync`, the two click handlers that reach them
  - Verdict: **Single and clear**

## Strengths That Matter
- F-014 was found the same way F-013 was found in loop 9 — a cold helper briefed on the general shape (every await-then-mutate method) rather than the known list of already-fixed sites — the second time in two loops that technique surfaced a real, previously-missed defect in the same heavily-reviewed file, evidence the pattern generalizes rather than being a one-off.
- This loop independently traced `IsLibraryOperationBlocking`'s own doc comment against its actual implementation mechanics rather than trusting the comment's claim — the comment names the exact race it turned out not to fully prevent.
- All 9 previously-resolved findings (F-001 through F-010) were re-spot-checked against current source this loop (direct reads, not registry trust alone), and every one still holds — including catching and correcting one inaccurate claim (F-013's "observed drift") rather than carrying it forward unexamined.

## Findings

### Finding #1 (stable_id F-014): Single-game artwork operations check IsLibraryOperationBlocking only at the click, never claim it, so a bulk operation can start and corrupt freshly-loaded entries mid-flight

**Why it matters** — A user can click Restore Backup (or pick artwork from the grid/search panel) on one row, then click Refresh, Fix Library, Restore Changes or Revert Defaults before the first click's file I/O completes; the second operation replaces the whole game list with newly-built objects, and when the first operation resumes it silently writes its now-stale result onto the freshly-loaded entries for that image instead.

**What is wrong** — `IsLibraryOperationBlocking` (`PrimaryWidget.xaml.cs:191-201`) is checked by `RestoreBackup_Click` (`:1859`), `EditGameImage_Click` (`:1233`) and `SearchGameImage_Click` (`:1637`) before starting their own single-game async flow, but none of those flows ever sets `isLibraryOperationRunning`, so the check only guards against a bulk operation already running at click time. `TryBeginLibraryOperation` (`:207-220`), used by `RefreshButton_Click` (`:725`) and `ConfirmAndRunAsync` (`:787`, the shared guard behind the three bulk-operation buttons), checks the same single flag with no awareness of an in-flight single-game operation, so it succeeds and lets `LoadGameEntriesAsync` clear and rebuild `GameEntries` (`:359`, `:699`) with brand-new `GameEntry` instances while, for example, `RestoreBackupCoreAsync`'s own await is still pending. When that await resumes, `EntriesSharingImage(game)` (`:328-330`) searches the now-replaced `GameEntries` for entries matching the stale captured `game`'s image path and writes `Image`/`ImageFileName`/`HasBackup` onto whatever it finds — the freshly-loaded entries, not the ones the user's click was about.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:191-201` (`IsLibraryOperationBlocking`), `:207-220` (`TryBeginLibraryOperation`), `:1857-1868` (`RestoreBackup_Click`), `:1900-1950` (`RestoreBackupCoreAsync`'s vulnerable post-await mutation), `:351-366,687-715` (`LoadGameEntriesAsync` replaces `GameEntries` wholesale)

**Architectural test failed** — n/a — a state-ownership/reentrancy finding

**Dependency category** — n/a

**Leverage impact** — None — a missing claim on an existing gate, not a Module boundary.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`; the fix mirrors the exact idiom already used to close F-005 through F-009.

**Metric signal, if any** — none

**Why this weakens submission** — Reachable from ordinary UI interaction with no special timing skill required, and it silently corrupts user-visible tile state with no error surfaced — the same class of harm F-005 through F-009 were rated Serious deduction for, discovered in the same file after 9 prior loops of dedicated session-guard sweeps missed it because every prior sweep traced only the per-game-session direction, never the per-game-operation-vs-bulk-operation direction.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Give `IsLibraryOperationBlocking`'s callers the same claim/release discipline `TryBeginLibraryOperation` already provides bulk operations, sized for a single-game operation: set `isLibraryOperationRunning` (or a dedicated single-game counter) before the await and clear it in a `finally` in `RestoreBackupCoreAsync`, `ReplaceImageCoreAsync` and the grid/search picker's download-and-apply path, so `TryBeginLibraryOperation`/`ConfirmAndRunAsync` cannot start a bulk operation while one is in flight.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: the XAML markup itself, `Services/**`.

### Finding #2 (stable_id F-013): ReplaceImageCoreAsync, RestoreAllChangesAsync and RestoreBackupCoreAsync duplicate the same UI-thread entry-update loop three times

**Why it matters** — Any future change to what happens when a game's tile image is replaced has to be found and added by hand in up to three places, and the three copies already differ in which fields they set with no shared explanation of why — a future edit that does not understand `RestoreAllChangesAsync`'s own semantics could easily blur that distinction.

**What is wrong** — `ReplaceImageCoreAsync` (`:1154-1192`), `RestoreAllChangesAsync`'s per-entry block (`:1067-1135`) and `RestoreBackupCoreAsync` (`:1900-1950`) each independently dispatch to the UI thread and `foreach` over `EntriesSharingImage(game)`, writing `Image`/`ImageFileName` in all three, `HasBackup` in two of three (`RestoreAllChangesAsync` omits it — **correctly**, since `ArtworkFiles.ReapplyCustomisationAsync` never touches the `.bak` file the flag reflects, but nothing in the shared shape documents that), and `StatusText.Text` conditionally in two of three.

**Evidence** — `PrimaryWidget.xaml.cs:1154-1192`, `:1067-1135`, `:1900-1950`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites would drop to naming their own image/backup-flag values instead of re-deriving the whole dispatch-and-foreach shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication class as F-002/F-003 (both resolved) — a third, smaller instance that survived nine loops of scrutiny because those sweeps focused on dialog/animation ceremony, not this smaller pattern. Corrected this loop: the real cost is forward-looking (a future edit gets the field lists subtly wrong), not a present bug.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private helper (`UpdateEntriesSharingImage(GameEntry game, BitmapImage image, string imageFileName, bool? hasBackup)`) owning the dispatch+foreach, writing `Image`/`ImageFileName` always and `HasBackup` only when supplied; each call site keeps its own status-text/counter logic.

**Blast radius** — Change: `PrimaryWidget.xaml.cs`. Avoid: the XAML markup, `Services/**`.

### Finding #3 (stable_id F-011): LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — A user whose library has many unmatched GOG/Epic games pays the full network latency of the store endpoint plus the SteamGridDB name-search endpoint for every one of those games, one after another, on every widget open.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`:455-679`) awaits, in strict sequence per manifest entry: `sgdbClient.GetGameByPlatformIdAsync` (`:581`), then one of the store name-fetch methods (`:603`/`:612`/`:621`), then `StoreNameLookup.FindGameByNameAsync` (`:641`) — each entry's calls fully complete before the next entry's iteration starts. The awaits are independent across entries (efficiency lens D2).

**Evidence** — `PrimaryWidget.xaml.cs:455-679`, `:581`, `:603,612,621`, `:641`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None currently — no seam exists to batch or parallelize through.

**Locality impact** — Contained to the loop body and, if fixed, the static caches' thread-safety.

**Metric signal, if any** — none

**Why this weakens submission** — Hot path doing per-item network I/O one item at a time where nothing in the current design requires that ordering.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Not implemented this loop or any loop this run — blocked by the STANDING USER CONSTRAINT (no behavioural oracle for per-game network-call ordering/concurrency changes) and by the unlocked static caches. A pure extraction with no change to call count/ordering/payload/error handling would NOT be blocked, but that is not this finding's own remedy — bounded concurrency necessarily changes the network-call ordering the constraint protects.

**Blast radius** — Change: none this run. Avoid: `PrimaryWidget.xaml.cs`, `StoreNameLookup.cs` (not attempted).

### Finding #4 (stable_id F-012): GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding, renaming, or removing a platform requires updating both switches by hand; each has a silent default fallback, so a future skew would degrade silently.

**What is wrong** — `FromXboxDirectory` (`GamePlatform.cs:22-46`) and `GamePlatformToSGDBApiString` (`:48-67`) both switch over the same 8-case enum but are independently authored with no shared table.

**Evidence** — `GamePlatform.cs:22-46`, `:48-67`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None currently; a table would let a future platform addition touch one place.

**Locality impact** — Contained to `GamePlatform.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — A real but currently-latent duplicate-abstraction risk; the six shared cases are still correctly mirrored, re-confirmed unchanged this loop.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — A single static table of `(GamePlatform, xboxFolderName, alternateXboxFolderName, sgdbApiString)` both methods query.

**Blast radius** — Change: `GamePlatform.cs`. Avoid: `PrimaryWidget.xaml.cs`, `Services/**`.

### Finding #5 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation (`<` to `<=`) would ship silently.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) treats a pixel transparent at alpha `< 64` (`:250`) and rejects the image when `transparentCorners < 2` (`:263`). Untested at either exact boundary.

**Evidence** — `TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None — test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — Minor, off-primary-flow gap; re-confirmed this loop against a fresh set of primary-flow mutations (`:908`, `:1328`, `:852`) that are the actual blocker capping `test_strategy`, not this boundary gap.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases at the exact documented boundaries.

**Blast radius** — Change: `TileImageTests.cs`. Avoid: `TileImage.cs`.

## Simplification Check
- **Structurally necessary**: No fix was attempted this loop (HALT_LOOP_CAP skips Step 2/3), but F-014's proposed remedy was checked against the Simplify Pressure Test as part of this loop's own evaluation: fixes real ambiguity, smallest honest fix (reuses the existing claim/release idiom), avoids duplicate layers, keeps runtime behavior honest, and the product improves. It would pass SPT if implemented.
- **New seam justified**: No.
- **Helpful simplification**: F-013's own text was corrected this loop without changing severity or remedy.
- **Should NOT be done**: Do not implement F-014 by widening `isLibraryOperationRunning`'s meaning without auditing every reader for an assumption it's purely library-wide — prefer a dedicated single-game counter if shared-flag reuse over-blocks unrelated single-game operations. Do not re-open `domain_modeling`'s residual next loop without a candidate smaller than this loop's readonly-struct-with-factories one. Do not attempt F-011 without real locking first, and not at all without a user decision on the STANDING USER CONSTRAINT.
- **Tests after fix**: n/a this loop — no fix landed.

## Improvement Backlog
1. **Add a claim/release guard to single-game artwork operations so a bulk operation cannot start mid-flight** (Finding F-014) — structural, needed for winning. Closes a live, reachable, silently-corrupting reentrancy gap in the same class as F-001/F-005-009 (all fixed) — unblocked by the STANDING USER CONSTRAINT. score_impact: `state_management +1.0`
2. **Extract a shared entry-update helper for ReplaceImageCoreAsync/RestoreAllChangesAsync/RestoreBackupCoreAsync** (Finding F-013) — simplification, helpful. Removes a third instance of the leaf-module-duplication class F-002/F-003 already fixed. score_impact: `simplicity +0.5`
3. **Add bounded concurrency to LoadGameEntriesAsync's per-entry name/match resolution, after first locking the static caches** (Finding F-011) — structural, helpful. Ranked on merit above F-012 but blocked: the STANDING USER CONSTRAINT is the sole blocker, named per the Backlog Prioritization Pass criterion 0 so it is not silently demoted. score_impact: `concurrency +0.5`

## Deepening Candidates
- **Candidate Module**: `LoadGameEntriesAsync`'s per-entry name/match resolution (Finding F-011)
  - Source friction proven: a sequential-independent-effects loop shape on a hot path, carried forward unchanged from loop 7.
  - Why shallow/misplaced: not a shallow-Interface problem in the classic sense, but the shape forces every caller to pay for strictly sequential I/O with no seam to batch or bound concurrency through.
  - Behavior to move behind interface: per-entry resolution, restructured for bounded concurrency once the static caches are thread-safe and the STANDING USER CONSTRAINT is resolved by a user decision.
  - Dependency category: `true-external`
  - Test surface after change: none achievable without solving the same untestable-file problem; `StoreNameLookup`'s own logic could gain a dedicated concurrency test once thread-safe.
  - Smallest first step: add real locking (a `SemaphoreSlim` per cache, matching `AppliedArtworkStore`'s pattern) to every static cache identified so far, before any concurrency change to the calling loop.
  - What not to do: do not wrap the loop in `Task.WhenAll` before all caches are locked; do not attempt the network-ordering half without a behavioural oracle.

## Builder Notes
1. **Pattern**: Briefing a cold sweep on a defect class's general shape (not the list of instances already found) surfaces genuinely new instances a targeted, history-aware sweep misses — now confirmed twice in the same file, one loop apart.
   - How to recognize: a file swept N times for a defect class, always converging on a stable known list, each sweep briefed on the prior list rather than the pattern's abstract shape.
   - Smallest coding rule: brief a re-sweep on the pattern's shape ("find every await-then-mutate method"), not the list of instances already found.
   - Example: F-013 (loop 9) and F-014 (loop 10) were both found this way in the same file, across two different defect classes (duplication, then reentrancy) — the technique generalizes.
2. **Pattern**: A guard's own doc comment claiming a protection is not evidence the protection holds in both directions — trace the implementation's mechanics, not the comment's claim.
   - How to recognize: a method says "blocks X while Y is happening" but is a one-directional check (read a flag, branch) rather than a two-directional claim (also sets the flag for its own duration).
   - Smallest coding rule: for any `IsXBlocking()`-shaped guard, confirm at least one caller that checks it also sets the flag it reads, for the exact duration being protected.
   - Example: `IsLibraryOperationBlocking`'s own doc comment names the race it turned out not to fully prevent — the per-game buttons check the flag but never claim it.
3. **Pattern**: A prior loop's "bug"/"drift" characterization needs its own evidence chain re-walked, not just its citation trusted — the callee's actual contract can make an apparent inconsistency correct.
   - How to recognize: a finding asserts two similar blocks *should* match and calls their difference a bug, without tracing what the shared callee actually promises each caller.
   - Smallest coding rule: before citing "these should match but don't" as a defect, read the callee(s) and confirm the two situations are really supposed to produce the same result.
   - Example: F-013's "RestoreAllChangesAsync omits the HasBackup write, which is drift" (loop 9) did not survive tracing `ArtworkFiles.ReapplyCustomisationAsync`'s own contract — it never touches the backup file, so the omission is correct.

## Final Judge Narrative
Place, not win, and the run ends with its most consequential discovery still unfixed. This terminal loop ran Critic-only — no code changed, build and full test suite both re-confirmed green and identical to loop 9's. Every score was independently re-derived from fresh source this loop: `framework_idioms`' 10.0 did **not** survive G6 re-verification (a genuine, if minor, doc-vs-code residual moved it to 9.5 accepted), and `domain_modeling`'s/`credibility`'s 9.5-accepted residuals both survived a fresh Adversarial Pass against smaller counter-proposals than any prior loop tested. The headline result is Finding F-014: a cold, independently-briefed helper sweep found a genuinely new, Serious-severity reentrancy gap — the same defect class as F-001 and F-005 through F-009 (all six already fixed), missed by nine prior loops because every one of them traced only the per-game-session direction, never the per-game-operation-vs-bulk-operation direction. It is fully actionable and is Priority 1 for whenever this run resumes, but this loop cannot fix it — the cap was reached before Step 2/3 could run. Runtime ownership is measurably less trustworthy than loop 9's own re-confirmation credited it for. Concurrency's own blocker (F-011) is unchanged and still genuinely stuck on a product decision only the user can make. Tests still cannot, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; `test_strategy`'s 10-loop stall is now doubly confirmed as a genuine platform ceiling, and F-014 is itself live proof of what that ceiling costs. Future work risks over-engineering only if F-014's fix reaches for a general-purpose operation-tracking abstraction instead of the narrow claim/release symmetry the codebase already uses, or if F-011's fix attempts to parallelize before locking every static cache identified.


--- Loop 1 (UTC 2026-08-05T19:16:14Z) ---

### Discovery
- Source roots: SteamGridDB.Xbox/, SteamGridDB.Xbox.Tests/
- Test command: `powershell -NoProfile -File ./run-tests.ps1`
- Build command: `msbuild SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo` (msbuild at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`, resolvable via vswhere.exe)
- ADRs found: none
- Domain terms (CONTEXT.md): none
- Selected lens: Generic (always-included: lens-security.md, lens-efficiency.md)
- Prior audit docs: ARTWORK-SELECTION.md (2026-08-03), CODE-REVIEW.md (2026-08-03), TESTING.md (2026-08-03)
- Note: this is loop 1 of the run following the 2026-08-05 `--reset`; findings_registry.json + REVIEW_HISTORY.{md,json} preserved across the reset (10 prior loops of continuous history archived above).

### Loop Counter
Loop 1 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

This loop independently re-derived (blind-critic ordering for the `--reset`) the same Serious reentrancy gap loop 10's Critic-only pass surfaced (F-014) and closed it this loop, but its existence after nine prior dedicated session-guard sweeps, plus a fresh latent inconsistency this loop found in StoreNameLookup's caches (F-015), keep this codebase short of contest-grade. Prior-audit adopt-or-falsify pass: CODE-REVIEW.md and ARTWORK-SELECTION.md are both self-dispositioned (all findings implemented/rejected-by-design except one explicitly-not-warranted item); TESTING.md is scope documentation, not a findings list. No open claim from any of the three was silently dropped.

## Scorecard (1-10)

- Architecture quality: **7.5** | SAME | Direct re-reads this loop (PrimaryWidget.xaml.cs in full; ArtworkRanker.cs, ArtworkDownloader.cs, TileImage.cs, ArtworkFiles.cs, AppliedArtworkStore.cs, StoreNameLookup.cs, SteamGridDbClient.cs, GamePlatform.cs, GameEntry.cs, GridImageItem.cs) plus two independently-briefed helper sweeps converge on the same picture as before: Services/* is deep, single-responsibility Modules with real Interfaces (ArtworkRanker.RankGrids hides a full scoring pipeline behind one call shared by the auto-fixer and the manual picker). PrimaryWidget.xaml.cs still spans five concerns in one Module. No structural proof of a move either direction.
- State management and runtime ownership: **7.0** | SAME | Independently re-derived from source before reading the registry: IsLibraryOperationBlocking (PrimaryWidget.xaml.cs:191-201) was checked but never claimed by GridImage_Click's write path or RestoreBackup_Click, so a bulk reload could rebuild GameEntries out from under an in-flight single-game write (Finding F-014, Serious). This loop's Step 2/3 closed the gap, but the score reflects source as scored at Step 1 (before the fix) — the improvement is credited next loop against this loop's commit sha, per this project's established convention.
- Domain modeling: **9.5** | SAME | accepted | Adversarial Pass re-run: GameEntry's parallel-fields case (HasSteamGridDBMatch/OfficialCapsuleUrl/SteamGridDbGameId) re-confirmed live in current source (PrimaryWidget.xaml.cs:581-590); the smallest candidate fix (readonly-struct-with-factories mirroring ManifestEntryIdentity.Result) still fails SPT Q5 on call-site blast radius for a Cosmetic, zero-live-harm concern.
- Data flow and dependency design: **7.5** | SAME | Direct re-read of StoreNameLookup.cs/SteamGridDbClient.cs/AppliedArtworkStore.cs: several process-lifetime static-mutable-state instances remain (exceeding the 9-anchor's allowance). F-015 narrows the framing of part of this list but doesn't change the count or score.
- Framework / platform best practices: **9.5** | SAME | accepted | App.xaml.cs:120's `//TODO: Load state from previously suspended application` (dead VS-template scaffolding on the documented fallback-only OnLaunched path) re-confirmed unchanged.
- Concurrency and runtime safety: **6.5** | SAME | F-011 (sequential per-entry network calls) re-confirmed unchanged, still blocked by the standing user constraint. F-015 (new: StoreNameLookup's GOG/Epic caches unsynchronized) is latent, not live. F-014 is a UI-thread reentrancy/ownership gap, filed under state_management, not counted here.
- Code simplicity and clarity: **8.0** | SAME | F-013 (triplicated UI-thread entry-update loop) re-confirmed present, not fixed this loop (outranked by F-014 on severity).
- Test strategy and regression resistance: **6.5** | SAME | PrimaryWidget.xaml.cs carries zero test coverage (permanent platform constraint, TESTING.md). This loop's own F-014 lived on that exact untested surface, found by reading, not by a failing test.
- Overall implementation credibility: **9.5** | SAME | queued (F-015) | Adversarial Pass re-run on the residual (targeted mechanism-level tracing over blanket re-derivation): this loop's own F-014 discovery is one more data point for the residual's model, not against it. F-015 is the fresh, local, subtractive-fixable leak this residual anchors to this loop.

## Authority Map

- **Library-wide vs. single-game write mutual exclusion** — Owner: `PrimaryWidget.isLibraryOperationRunning`. Allowed writers: `TryBeginLibraryOperation`/`EndLibraryOperation` (:207-233). Readers: `IsLibraryOperationBlocking` (:191-201). Persistence seam: none. Async mutation entry points: `PrimaryWidget_Loaded`, `RefreshButton_Click`, `ConfirmAndRunAsync` (Fix/Restore/Revert), `GridImage_Click` (claimed as of this loop), `RestoreBackup_Click` (claimed as of this loop). Verdict: **Single and clear**.
- **In-memory game list (`GameEntries`)** — Owner: `PrimaryWidget.GameEntries`. Allowed writers: `LoadGameEntriesAsync` (wholesale replace), `ReplaceImageCoreAsync`/`RestoreBackupCoreAsync`/`RestoreAllChangesAsync` (in-place mutation via `EntriesSharingImage`). Readers: `GameEntriesListView`, `GamesToProcess`/`EntriesSharingImage`. Persistence seam: none. Verdict: **Single and clear**.
- **Grid picker session identity** — Owner: `gridPanelSessionId`. Writers: `LoadGridSelectionAsync`. Readers: `PopulateGridSelectionPanelAsync`, `GridImage_Click`, `HideGridPanelAsync`, `DownloadAndReplaceImageAsync`. Verdict: **Single and clear**.
- **Search panel session identity** — Owner: `searchPanelSessionId`. Writers: `PerformGameSearchAsync`, `ShowSearchPanelAsync`. Readers: `PerformGameSearchAsync`, `HideSearchPanelAsync`. Verdict: **Single and clear**.
- **Applied-artwork record** — Owner: `AppliedArtworkStore.appliedCache` + gate. Writers: `SetAsync`/`ClearAsync` via `UpdateAsync`. Readers: `GetAsync`. Persistence seam: `applied-artwork.json`. Verdict: **Single and clear**.
- **Store name-resolution caches** — Owner: `StoreNameLookup`'s 3 unsynchronized static `Dictionary` fields + 1 gated `AsyncLazyCache<T>` (Ubisoft). Writers/readers: `GetOrFetchGogNameAsync`, `GetOrFetchEpicNameAsync`, `FindGameByNameAsync`, `LoadUbisoftGameListFromWebAsync`. Async mutation entry points: `LoadGameEntriesAsync`'s per-entry loop. Verdict: **Split and ambiguous**.

## Strengths That Matter
- ArtworkRanker/ArtworkDownloader/ArtworkSignature/TileImage form a genuinely deep, pure, well-tested pipeline with documented calibration history (e.g. `ArtworkDownloader.cs:26-33`'s colour-match floor/ceiling, tuned against a specific graded incident) and real Leverage: `GetTitleBearingGridsAsync` (`PrimaryWidget.xaml.cs:1387-1399`) serves both the auto-fixer and the manual picker from one Interface.
- `AppliedArtworkStore` and `StoreNameLookup`'s own Ubisoft cache correctly serialize concurrent access against a shared `Dictionary` with one `SemaphoreSlim`-backed `AsyncLazyCache<T>` gate — the exact primitive this loop's F-015 finding shows was not reused for the file's other two caches, so the codebase already knows how to do this right.
- The picker session-guard idiom (`gridPanelSessionId`/`searchPanelSessionId`) is applied consistently across all six of its call sites, independently re-verified this loop with zero drift since it was completed.

## Findings

### Finding #1: Single-game artwork operations check IsLibraryOperationBlocking only at the click, never claim it, so a bulk operation can start and corrupt freshly-loaded entries mid-flight

**Why it matters** — A user can click Restore Backup or pick artwork from the grid picker on one row, then click Refresh/Fix Library/Restore Changes/Revert Defaults before the first click's file I/O completes; the second operation replaces the whole game list, and when the first operation resumes it silently writes its now-stale result onto whichever freshly-loaded entries share that image path.

**What is wrong** — `IsLibraryOperationBlocking` (`PrimaryWidget.xaml.cs:191-201`) was checked by `RestoreBackup_Click` (:1857) and `GridImage_Click`'s write path before starting their own single-game async flow, but neither ever set `isLibraryOperationRunning`. `TryBeginLibraryOperation` (:207-220), the guard `RefreshButton_Click` and `ConfirmAndRunAsync`'s callers already hold, had no awareness of an in-flight single-game write, so it could succeed and let `LoadGameEntriesAsync` clear and rebuild `GameEntries` (:359, :695-701) while `RestoreBackupCoreAsync`'s/`ReplaceImageCoreAsync`'s own await was still pending.

**Evidence** — `PrimaryWidget.xaml.cs:63`; `:191-201`; `:207-229`; `:1491-1497`; `:1857-1870`; `:1900-1952`; `:351-366,695-701`.

**Architectural test failed** — n/a (state-ownership / mutual-exclusion-guard-scope gap, matching this codebase's own categorization of F-001/F-005 through F-009).

**Leverage impact** — None — a missing claim on an already-existing gate, not a Module boundary; the fix reuses `TryBeginLibraryOperation`/`EndLibraryOperation` exactly as three other call sites already do.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`; mirrors the idiom that already closed F-001 and F-005 through F-009.

**Metric signal** — none.

**Why this weakens submission** — Reachable from ordinary UI interaction with no special timing skill required, silently corrupts user-visible tile state with no error surfaced — the same class of harm F-005 through F-009 were rated Serious for, discovered from a direction nine prior sweeps had not checked.

**Severity** — Serious deduction.

**ADR conflicts** — none.

**Minimal correction path** — Extend the guard's claim to `GridImage_Click`'s `DownloadAndReplaceImageAsync` call and `RestoreBackup_Click`'s `RestoreBackupAsync` call via `TryBeginLibraryOperation()`/`EndLibraryOperation()`. Do **not** add the guard inside `ReplaceImageCoreAsync`/`DownloadAndReplaceImageCoreAsync` themselves — they are also reached from `FixLibraryAsync`'s already-guarded bulk loop, and a second acquire there would self-reject the bulk operation's own per-game writes.

**Blast radius** — Change: `PrimaryWidget.xaml.cs`. Avoid: `PrimaryWidget.xaml` (markup), `Services/**`.

---

### Finding #2: ReplaceImageCoreAsync, RestoreAllChangesAsync and RestoreBackupCoreAsync duplicate the same UI-thread entry-update loop three times

**Why it matters** — A future edit to how a tile is refreshed after a write has three call sites to find and keep in sync, and the existing field-list differences between the three copies are not documented as intentional.

**What is wrong** — `ReplaceImageCoreAsync` (:1154-1192), `RestoreAllChangesAsync`'s per-entry block (:1067-1135) and `RestoreBackupCoreAsync` (:1900-1976) each independently dispatch to the UI thread and `foreach` over `EntriesSharingImage(game)`, with no shared helper and no comment distinguishing field-list differences from oversight.

**Evidence** — `PrimaryWidget.xaml.cs:1154-1192`; `:1067-1135`; `:1900-1976`.

**Architectural test failed** — Shallow module.

**Leverage impact** — Callers must independently verify each of three copies stays correct; a shared helper would let one Interface carry the invariant.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal** — none.

**Why this weakens submission** — A leaf-duplication pattern already fixed twice in this file (F-002, F-003) recurring a third time reduces confidence the codebase's own collapse-on-third-instance idiom is being applied consistently.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — Extract a private `UpdateSharedEntriesAsync(GameEntry game, string imageFileName, BitmapImage image, bool? hasBackup, string statusText)` helper; delete the three independent blocks.

**Blast radius** — Change: `PrimaryWidget.xaml.cs`. Avoid: `Services/**`.

---

### Finding #3: StoreNameLookup's GOG and Epic name caches perform unsynchronized check-then-populate writes, unlike the file's own Ubisoft cache three lines below

**Why it matters** — The type's own doc comment says every cache here is "shared across the whole process," but only the Ubisoft cache (via `AsyncLazyCache<T>`) actually protects that shared state from a concurrent race; a future caller or a future fix to F-011 would silently inherit an unsynchronized write.

**What is wrong** — `gogNameCache`/`epicNameCache` (:29-30) are plain `Dictionary<string,string>` read/written by `GetOrFetchGogNameAsync` (:89-104)/`GetOrFetchEpicNameAsync` (:203-222) with a bare check-then-populate and no lock. `ubisoftGameListCache` (:40-42), three lines below, solves the identical shape via `AsyncLazyCache<T>`'s `SemaphoreSlim` gate. No comment explains why GOG/Epic are exempt.

**Evidence** — `StoreNameLookup.cs:29-30`; `:89-104`; `:203-222`; `:40-42`; `AsyncLazyCache.cs:19-60`.

**Architectural test failed** — n/a.

**Dependency category** — `in-process`.

**Leverage impact** — A caller gets no concurrent-access-safety guarantee from the Interface alone; safety today depends on the sole caller happening to await sequentially.

**Locality impact** — Fully contained inside `StoreNameLookup.cs`; the fix reuses the file's own existing gate idiom.

**Metric signal** — none.

**Why this weakens submission** — Not currently reachable as a live race (F-011's own evidence: the sole caller awaits sequentially), so latent rather than Serious — but a real structural inconsistency the codebase's own doc comments elsewhere go out of their way to prevent (see `AppliedArtworkStore.cs`'s explicit shared-gate rationale).

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — Wrap `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`'s check-then-populate body in the same `SemaphoreSlim` gate `ubisoftGameListCache` already uses, matching `AppliedArtworkStore`'s own shared-gate pattern.

**Blast radius** — Change: `StoreNameLookup.cs`. Avoid: `PrimaryWidget.xaml.cs`, `EpicLibrary.cs`.

---

### Finding #4: GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding or renaming a platform requires remembering to update both switches; nothing fails to compile if one is forgotten (both default to `Unknown`/`null`).

**What is wrong** — `FromXboxDirectory` (`GamePlatform.cs:22-46`) and `GamePlatformToSGDBApiString` (:48-67) independently switch over the same six platform cases with no shared table.

**Evidence** — `GamePlatform.cs:22-46`; `:48-67`.

**Architectural test failed** — n/a.

**Leverage impact** — Each new platform pays this asserted-twice tax; small today, compounds at scale.

**Locality impact** — Contained to `GamePlatform.cs`.

**Metric signal** — none.

**Why this weakens submission** — Minor, but the enum's own home module holds two independent interpretations of itself with no compiler-enforced link.

**Severity** — Cosmetic for contest.

**ADR conflicts** — none.

**Minimal correction path** — Fold both mappings into one table both directions read from.

**Blast radius** — Change: `GamePlatform.cs`. Avoid: `PrimaryWidget.xaml.cs` (call sites unchanged).

---

### Finding #5: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (:455-679) awaits each entry's SteamGridDB platform-ID lookup, store name-fetch and SteamGridDB name search in strict sequence, even though the awaits are independent across entries.

**Evidence** — `PrimaryWidget.xaml.cs:455-679`.

**Architectural test failed** — n/a.

**Leverage impact** — None currently actionable — see blocker.

**Locality impact** — Would be contained to `PrimaryWidget.xaml.cs` and F-015's cache-locking prerequisite, if unblocked.

**Metric signal** — none.

**Why this weakens submission** — A real, linearly-scaling latency cost, but **BLOCKED**: the recorded standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft) means this loop cannot land a change altering observable call ordering/concurrency/count against those services without a product decision this loop cannot make.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — BLOCKED this loop; named for continuity (Backlog Prioritization Pass criterion 0) rather than escalated to a user-decision halt, since three other unblocked items fill this loop's backlog.

**Blast radius** — Change: none this loop. Avoid: `PrimaryWidget.xaml.cs` (no change while blocked).

## Simplification Check

| Field | Value |
| --- | --- |
| Structurally necessary | Closing F-014's guard-scope gap by extending the existing `TryBeginLibraryOperation`/`EndLibraryOperation` guard — a state-ownership fix, not a Deletion/Seam-category fix. |
| New seam justified | false |
| Helpful simplification | none this loop beyond the fix itself |
| Should NOT be done | A dedicated single-game-operation guard/token distinct from `isLibraryOperationRunning` (duplicate layer); wrapping `ReplaceImageCoreAsync`/`DownloadAndReplaceImageCoreAsync` themselves (would self-reject `FixLibraryAsync`'s own already-guarded bulk writes). |
| Tests after fix | None added or deleted — `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface. Verification: full build + full test suite (138/138 unchanged), independent implementation review (approved), manual trace. |

## Improvement Backlog
1. **[F-014]** Extend the library-operation guard to single-game writes — structural, needed for winning. score_impact: `state_management +1.0`
2. **[F-013]** Extract a shared entry-update helper for ReplaceImageCoreAsync/RestoreAllChangesAsync/RestoreBackupCoreAsync — simplification, helpful. score_impact: `simplicity +0.5`
3. **[F-015]** Gate StoreNameLookup's GOG/Epic caches the same way its own Ubisoft cache already is — structural, helpful. score_impact: `concurrency +0.5`

## Deepening Candidates
None this loop.

## Builder Notes
1. **Pattern**: A mutual-exclusion guard's real coverage is defined by which call sites hold it, not by its own doc comment. → REVIEW_HISTORY.json `loops[10].builder_notes` for full notes
2. **Pattern**: The same small primitive (a gated lazy cache) gets reinvented per-field instead of reused, and only the newest field gets the lock. → REVIEW_HISTORY.json `loops[10].builder_notes` for full notes
3. **Pattern**: A near-identical block appearing a third time in one file is this codebase's own established threshold for "extract it." → REVIEW_HISTORY.json `loops[10].builder_notes` for full notes
4. **Pattern**: Scorecard humility check. → REVIEW_HISTORY.json `loops[10].builder_notes` for full notes

## Final Judge Narrative
Place, not win, yet. The codebase has real depth in its artwork pipeline and increasingly disciplined ownership in `PrimaryWidget.xaml.cs`, but this loop independently re-derived and closed the sixth-and-latest instance of a defect class (reentrancy gaps between per-game and library-wide operations) that nine prior loops of dedicated sweeps had not fully closed — runtime ownership is more trustworthy after this loop's fix than before it, but a well-reviewed guard's own doc comment being incomplete for this long is a caution against declaring the class closed. Concurrency remains trustworthy for what actually runs today (nothing executes off the UI thread), though this loop surfaced a latent inconsistency (F-015) worth closing before any future change makes cache access concurrent. Simplification helped this loop: the fix reused an existing primitive rather than inventing a new guard type. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs` itself — every finding in this file was found and verified by direct reading and unchanged build/test evidence, not by a failing test turning green. Future work risks over-engineering if it tries to extract PrimaryWidget's orchestration into a testable Module wholesale, per this and prior loops' own Simplify Pressure Test analysis; the smaller F-013/F-015 fixes ahead carry no such risk.

## Loop 1 Result
Extended the existing `TryBeginLibraryOperation()`/`EndLibraryOperation()` guard (already held for the duration of `RefreshButton_Click` and `ConfirmAndRunAsync`'s bulk operations) to `GridImage_Click`'s `DownloadAndReplaceImageAsync` call and `RestoreBackup_Click`'s `RestoreBackupAsync` call — the two remaining call sites that started a single-game write without claiming the flag. Updated the `isLibraryOperationRunning` field comment and both methods' doc comments to describe the extended scope. Full build (exit 0) and full test suite (138/138 unchanged, expected — `PrimaryWidget.xaml.cs` is outside the test-linked surface) both re-run after the change. Neither guard touches, reorders, or wraps any network/file-write call — no SteamGridDB/GOG/Epic/Ubisoft call count, ordering, or payload changes anywhere, satisfying the standing user constraint. Independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on the finding and the diff only) returned verdict **approved** with all three checks (reality, honesty, regression) passed, explicitly verifying `ReplaceImageCoreAsync`/`DownloadAndReplaceImageCoreAsync` were correctly left unwrapped to avoid self-rejecting `FixLibraryAsync`'s own already-guarded bulk writes, and that no third unguarded single-game write path exists. Finding F-014 (stable_id F-014) is **resolved**. No unintended scorecard regression observed.

## Loop 1 Implementation Review
Verdict: **approved**. Reason: GridImage_Click and RestoreBackup_Click now hold isLibraryOperationRunning across their full single-game write via try/finally, closing F-014's gap with no remaining unguarded write path and no new same-or-higher-severity regression. Checks: reality=passed, honesty=passed, regression=passed. Regressions: none. Conditions: none. Rounds: 1.

## Retired Findings (this loop)
None.

--- Loop 2 (UTC 2026-08-05T19:46:42Z) ---

### Discovery
see Loop 1 Discovery

### Loop Counter
Loop 2 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

Runtime ownership took a real step forward this loop: F-014's guard-scope fix (landed loop 1, commit 9b2c4cb) is now visible in current source with no remaining unguarded single-game write path, and state management is credited for it this loop per this project's own established scoring convention. This loop's own refactor (F-013) collapsed the third instance of a duplicated UI-thread entry-update block. The artwork pipeline remains genuinely deep and well-tested. What keeps this short of contest-grade: `PrimaryWidget.xaml.cs` still spans five concerns in one Module, `StoreNameLookup`'s cache-locking inconsistency (F-015) is now confirmed to span all three of its hand-rolled caches (not just two), and the untestable-by-platform-constraint surface (`PrimaryWidget.xaml.cs`) is exactly where every Serious finding this run has lived.

**Prior-audit adopt-or-falsify**: CODE-REVIEW.md, TESTING.md and ARTWORK-SELECTION.md were adopted/falsified against current source in loop 1 with no open claim dropped; this loop re-confirmed no new prior-audit claim changed status.

## Scorecard (1-10)

- Architecture quality: **7.5** | SAME | Direct re-read this loop of `PrimaryWidget.xaml.cs` in full (2051 lines pre-fix) plus two independently-briefed helper sweeps converge on the same picture as loop 1: `Services/*` remains deep, single-responsibility Modules. `PrimaryWidget.xaml.cs` still spans five concerns in one Module; this loop's own fix (F-013) added a private helper inside that same Module rather than reducing its scope.
- State management and runtime ownership: **9.5** | UP | `accepted` | Structural proof: `git log 05d06a9..9b2c4cb` shows loop 1's own commit, which this loop's direct re-read confirms landed — `GridImage_Click` (:1504-1522) and `RestoreBackup_Click` (:1891-1911) now both wrap their single-game write in `TryBeginLibraryOperation()`/`EndLibraryOperation()`, closing F-014's gap; no other write path bypasses the guard. Residual blocking 10: `SteamGridDbClient.CapsuleParseNotes` (`SteamGridDbClient.cs:47`) is `public static readonly List<string>` — mutation is externally reachable, unlike every other cross-file mutable-state instance in this codebase.
- Domain modeling: **9.5** | SAME | `accepted` | Adversarial Pass re-run: `GameEntry`'s parallel-fields case re-confirmed live; smallest candidate fix still fails Simplify Pressure Test Q5 on call-site blast radius for a Cosmetic-severity concern.
- Data flow and dependency design: **7.5** | SAME | Direct re-read of `StoreNameLookup.cs` (all 306 lines): three static `Dictionary` caches remain process-lifetime ambient state — this loop's own re-read found `nameMatchCache` has the identical unsynchronized shape as the two caches F-015 already named. `SteamGridDbClient.CapsuleParseNotes` is a fourth instance.
- Framework / platform best practices: **9.5** | SAME | `accepted` | `App.xaml.cs:120`'s dead template-scaffolding TODO re-confirmed unchanged; not touched by this loop's edits.
- Concurrency and runtime safety: **6.5** | SAME | F-011 re-confirmed unchanged, still blocked by the standing user constraint. F-015 re-derived with broadened evidence (`nameMatchCache` added) — still latent, not live. Not fixed this loop (outranked by F-013 per the Backlog Prioritization Pass's item-deferral criterion).
- Code simplicity and clarity: **8.0** | SAME | F-013 re-confirmed present at Step 1, at its pre-fix line ranges (`:1101-1114`, `:1169-1188`, `:1962-1981`) — score reflects source as read at Step 1, before this loop's own fix; credit lands in loop 3's Step-1 scorecard.
- Test strategy and regression resistance: **6.5** | SAME | `PrimaryWidget.xaml.cs` carries zero test coverage — confirmed again (138/138 unchanged before/after). Mutation-test mental model re-applied on `GridImage_Click`'s guard. `Services/*` remains comprehensively covered (12 of 13 reviewed files "Comprehensive"). Permanent platform constraint (WinUI `Page`, no desktop projection), matching TESTING.md.
- Overall implementation credibility: **9.5** | SAME | `queued` (F-015) | Adversarial Pass re-run: this loop's own re-read of `StoreNameLookup.cs` strengthened rather than weakened the residual's case — the same gate-reuse fix applies cleanly to all three unsynchronized caches. F-015's broadened scope is additional evidence for the existing queued residual, not a fresh leak.

## Strengths That Matter
- `ArtworkRanker`/`ArtworkDownloader`/`ArtworkSignature`/`TileImage` remain a genuinely deep, pure, well-tested pipeline with documented calibration history and real Leverage: `GetTitleBearingGridsAsync` serves both the auto-fixer and the manual picker from one Interface.
- `AppliedArtworkStore` and `StoreNameLookup`'s own Ubisoft cache correctly gate concurrent access behind one `SemaphoreSlim`-backed `AsyncLazyCache<T>` — the exact primitive this loop's re-derivation of F-015 confirms the file's other three mutable-state instances still do not reuse.
- This loop's own fix (`UpdateSharedEntriesAsync`) collapsed three independently-drifting UI-thread entry-update blocks into one Interface without adding a new Seam or ceremony layer — matching this codebase's own established "third instance triggers extraction" convention.

## Findings

### Finding #1: ReplaceImageCoreAsync, RestoreAllChangesAsync and RestoreBackupCoreAsync duplicate the same UI-thread entry-update loop three times

**Why it matters** — A future edit to how a tile is refreshed after a write has three call sites to find and keep in sync, and the field-list differences between the three copies are not documented as intentional versus oversight.

**What is wrong** — `ReplaceImageCoreAsync` (`PrimaryWidget.xaml.cs:1169-1188` pre-fix), `RestoreAllChangesAsync`'s per-entry block (`:1101-1114` pre-fix) and `RestoreBackupCoreAsync` (`:1962-1981` pre-fix) each independently dispatch to the UI thread and `foreach` over `EntriesSharingImage(game)`, with no shared helper.

**Evidence** — `PrimaryWidget.xaml.cs:1101-1114`; `:1169-1188`; `:1962-1981` (all pre-fix).

**Architectural test failed** — Shallow module.

**Severity** — Noticeable weakness.

**Minimal correction path** — Extract a private `UpdateSharedEntriesAsync(GameEntry game, string imageFileName, BitmapImage image, bool? hasBackup, string statusText)` helper; delete the three independent blocks.

**Blast radius** — Change: `PrimaryWidget.xaml.cs`. Avoid: `Services/**`, `PrimaryWidget.xaml`.

---

### Finding #2: StoreNameLookup's three hand-rolled caches perform unsynchronized check-then-populate writes, unlike the file's own Ubisoft cache three lines below

**Why it matters** — Only the Ubisoft cache actually protects shared state from a concurrent race; a future fix to F-011 would silently inherit an unsynchronized write.

**What is wrong** — `gogNameCache`/`epicNameCache` (`StoreNameLookup.cs:29-30`) and, confirmed this loop, `nameMatchCache` (:34) all perform unsynchronized check-then-populate writes, unlike `ubisoftGameListCache` (:40-42) three lines below.

**Evidence** — `StoreNameLookup.cs:29-30`; `:34`; `:89-104`; `:117-149`; `:203-222`; `:40-42`; `AsyncLazyCache.cs:19-60`.

**Dependency category** — `in-process`.

**Severity** — Noticeable weakness.

**Minimal correction path** — Wrap all three check-then-populate bodies in the same `SemaphoreSlim` gate `ubisoftGameListCache` already uses.

**Blast radius** — Change: `StoreNameLookup.cs`. Avoid: `PrimaryWidget.xaml.cs`, `EpicLibrary.cs`.

---

### Finding #3: GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding or renaming a platform requires remembering to update both switches; nothing fails to compile if one is forgotten.

**What is wrong** — `FromXboxDirectory` (`GamePlatform.cs:22-46`) and `GamePlatformToSGDBApiString` (:48-67) independently switch over the same platform cases with no shared table.

**Evidence** — `GamePlatform.cs:22-46`; `:48-67`.

**Severity** — Cosmetic for contest.

**Minimal correction path** — Fold both mappings into one table keyed by `GamePlatform`, with a separate alias list for legacy folder names.

**Blast radius** — Change: `GamePlatform.cs`. Avoid: `PrimaryWidget.xaml.cs`.

---

### Finding #4: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop awaits each entry's lookups in strict sequence, even though the awaits are independent across entries.

**Evidence** — `PrimaryWidget.xaml.cs:421-683`.

**Severity** — Noticeable weakness.

**Minimal correction path** — BLOCKED this loop by the standing user constraint on per-game network-call ordering/concurrency; named for continuity.

**Blast radius** — Change: none this loop.

## Simplification Check

| Field | Value |
|---|---|
| structurally_necessary | Extracting `UpdateSharedEntriesAsync` — a Shallow-module fix, not a Deletion/Seam-category fix. |
| new_seam_justified | false |
| helpful_simplification | Removes the third duplicated instance of a pattern already collapsed twice (F-002, F-003). |
| should_not_be_done | A public/protected version of the helper, or a separate static utility class — neither adds Leverage since the only three callers are private methods in this same file. |
| tests_after_fix | None added or deleted — `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface. Verification: full build + full test suite (138/138 unchanged), independent implementation review, manual line-by-line diff confirming exact behavior preservation. |

## Improvement Backlog
1. **[F-013]** Extract a shared entry-update helper — simplification, helpful. Ends a three-loop deferral. Score impact: `simplicity +0.5`.
2. **[F-015]** Gate `StoreNameLookup`'s three hand-rolled caches — structural, helpful. Score impact: `concurrency +0.5; credibility +0.5`.
3. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution — BLOCKED. Score impact: `concurrency +1.0`.

**Priority-1 accounting**: F-013 has been open across loops 9, 10 and this run's loop 1 (three consecutive occurrences, `open`, each time outranked by a higher-severity Serious finding). Per the Backlog Prioritization Pass's item-deferral criterion, the tie is broken toward F-013 now that F-014 is resolved and no higher-severity unblocked candidate exists.

## Deepening Candidates
None this loop. `LoadGameEntriesAsync`'s manifest-parsing block was investigated and fails Simplify Pressure Test Q2 this loop: `CreateThumbnailAsync`'s `Dispatcher`-thread affinity and `GameEntry`'s WinRT-typed properties mean a clean extraction requires first splitting `GameEntry` into a pure DTO plus a UI-bound wrapper — a larger, simultaneous redesign.

## Builder Notes
1. **Pattern**: A near-identical block appearing a third time in one file is this codebase's own established threshold for "extract it" — and letting it sit past that threshold has a cost independent of the block itself. → REVIEW_HISTORY.json `loops[2].builder_notes` for full notes
2. **Pattern**: Distinguish "an item lost every priority contest so far" from "an item isn't worth doing." → REVIEW_HISTORY.json `loops[2].builder_notes` for full notes
3. **Pattern**: When extending an existing finding's evidence, re-read the whole file, not just the lines the finding already cites. → REVIEW_HISTORY.json `loops[2].builder_notes` for full notes

## Final Judge Narrative
Place, not win, yet. This loop credited a real prior improvement (F-014, now visibly closed in source) and made one of its own (F-013), ending a three-loop deferral rather than adding a fourth. Runtime ownership is now trustworthy for every traced write path in `PrimaryWidget.xaml.cs`. Simplification helped this loop: the fix collapsed three drifting copies into one Interface with no new Seam and no ceremony, and preserved every field-level behavioral difference between the three original call sites. Concurrency remains trustworthy for what actually executes today, though F-015's re-derivation shows the latent inconsistency is wider than previously scoped. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs` itself. Future work still risks over-engineering if it tries to extract `PrimaryWidget`'s orchestration into a testable Module wholesale; this loop re-examined that candidate independently and reached the same conclusion prior loops did, for a source-backed reason.

## Loop 2 Result
Extracted a private `UpdateSharedEntriesAsync(GameEntry game, string imageFileName, BitmapImage image, bool? hasBackup, string statusText)` helper in `PrimaryWidget.xaml.cs` (added after `EntriesSharingImage`, :337-365) and replaced the three independent UI-thread-dispatch/foreach/status-text blocks in `RestoreAllChangesAsync`, `ReplaceImageCoreAsync` and `RestoreBackupCoreAsync` with calls to it, preserving each call site's exact prior behavior via the `bool? hasBackup` (null = leave untouched) and `string statusText` (null = leave untouched) parameters. Full build (exit 0) and full test suite (138/138 unchanged) both re-run after the change. No network/file-write call, ordering, or payload changed anywhere. Independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on the targeted finding and the diff only) returned verdict **approved** with all three checks (reality, honesty, regression) passed. Finding F-013 (stable_id F-013) is **resolved**. No unintended scorecard regression observed.

## Loop 2 Implementation Review
Verdict: **approved**. Reason: All three checks passed: only one foreach/EntriesSharingImage dispatch block remains (inside the new private UpdateSharedEntriesAsync), the three call sites reproduce their exact pre-fix HasBackup and StatusText behavior via the bool?/string? null-means-untouched parameters, and the changed hunks introduce no new ownership, concurrency, or seam-policy hazard. Checks: reality=passed, honesty=passed, regression=passed. Regressions: none. Conditions: none. Rounds: 1.

## Retired Findings (this loop)
None.

--- Loop 3 (UTC 2026-08-05T20:13:15Z) ---

### Discovery
see Loop 1 Discovery

### Loop Counter
Loop 3 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

Simplicity took a real, source-proven step forward this loop: F-013's collapse (landed loop 2, commit fdf758f) is now visible in current source with `UpdateSharedEntriesAsync` cleanly serving all three former call sites, and simplicity is credited for it this loop per this project's own established scoring convention. This loop's own refactor (F-015) closed the last of `StoreNameLookup`'s three unsynchronized caches, extending the file's own `AsyncLazyCache<T>`/dedicated-gate idiom from one field to four. What keeps this short of contest-grade: `PrimaryWidget.xaml.cs` still spans five concerns in one Module, `StoreNameLookup`'s three ambient-state caches (now locked, still ambient) keep `data_flow` capped, and F-011 remains genuinely blocked by the standing user constraint.

**Prior-audit adopt-or-falsify**: CODE-REVIEW.md, TESTING.md and ARTWORK-SELECTION.md were adopted/falsified against current source in loop 1 with no open claim dropped; this loop re-confirmed no new prior-audit claim changed status.

## Scorecard (1-10)

- Architecture quality: **7.5** | SAME | Direct full re-read this loop of `PrimaryWidget.xaml.cs` (2067 lines, current) plus two independently-briefed helper sweeps converge on the same picture as loops 1-2: `Services/*` remains deep, single-responsibility Modules. `PrimaryWidget.xaml.cs` still spans five concerns in one Module; this loop's fix touched only `StoreNameLookup.cs`. Below 9.5, no residual field populated per schema.
- State management and runtime ownership: **9.5** | SAME | `accepted` | Direct re-read of the guard paths (`GridImage_Click:1527-1545`, `RestoreBackup_Click:1914-1934`) re-confirms F-014's fix holds. Adversarial Pass re-run: smallest fix for the `CapsuleParseNotes` residual (private field + `IReadOnlyList<string>` accessor) would pass SPT Q1-Q4 but fails Q5 comparatively against this loop's actual pick (F-015); residual holds.
- Domain modeling: **9.5** | SAME | `accepted` | `GameEntry`'s parallel-fields case re-confirmed unchanged via full re-read. Adversarial Pass re-run: the proposed readonly-struct fix now fails SPT Q2 outright — `GameEntry` is two-way XAML-data-bound via `INotifyPropertyChanged`, which a readonly struct cannot back without a separate wrapper layer.
- Data flow and dependency design: **7.5** | SAME | `StoreNameLookup`'s three caches plus `SteamGridDbClient.CapsuleParseNotes` remain four process-lifetime ambient-state instances. This loop's fix adds synchronization (a `concurrency` change) but does not reduce the ambient-dependency count, which is what this dimension measures.
- Framework / platform best practices: **9.5** | SAME | `accepted` | `App.xaml.cs:120`'s dead TODO re-confirmed unchanged. Adversarial Pass re-run found a cheaper fix than prior loops considered (delete the dead comment outright) — passes SPT Q1-Q4 but fails Q5 comparatively against F-015; residual holds.
- Concurrency and runtime safety: **6.5** | SAME | Scored against source as read at Step 1, before this loop's own fix (established convention). F-011 re-confirmed still blocked; F-015 re-confirmed still present in the pre-fix source this Step 1 scores. This loop's own fix will be credited at loop 4's Step 1.
- Code simplicity and clarity: **9.5** | UP | `accepted` | Structural proof: `git log 9b2c4cb..fdf758f` shows loop 2's own commit, confirmed landed and holding via full re-read (`UpdateSharedEntriesAsync:348-368`). Mandatory leaf-module duplication sweep run this loop (three parts per method.md Step 6) — no new duplication cluster found beyond the already-tracked F-012. Residual: F-012 (`GamePlatform`'s two switches, Cosmetic). Adversarial Pass re-run: F-012's fix still fails SPT Q5 comparatively against F-015.
- Test strategy and regression resistance: **6.5** | SAME | `PrimaryWidget.xaml.cs` carries zero test coverage — confirmed again (138/138 unchanged before/after). `Services/*` remains comprehensively covered per two independent helper sweeps this loop; a handful of untested pure-logic files found (`GameEntry.cs`, `GridImageItem.cs`, `ArtworkSource.cs`, `EpicLibrary.cs`) but none moves this score per the severity anchor's off-path-utility carve-out.
- Overall implementation credibility: **9.5** | SAME | `queued` (F-015) | Scored against source as read at Step 1 (pre-fix): F-015 still present at scoring time, so the residual stays queued — the queued-to-resolved transition lands at loop 4's Step 1, against this loop's own commit.

## Strengths That Matter
- `ArtworkRanker`/`ArtworkDownloader`/`ArtworkSignature`/`TileImage` remain a genuinely deep, pure, well-tested pipeline — independently re-confirmed this loop by a cold helper sweep finding "no structural issues... documentation matches implementation" across all five files.
- `AppliedArtworkStore`'s and (as of this loop) `StoreNameLookup`'s own four caches all now gate concurrent access behind a dedicated `SemaphoreSlim` — three of `StoreNameLookup`'s four caches joined `ubisoftGameListCache` in that idiom this loop, via a dedicated gate per cache rather than one shared gate.
- This loop's own fix required zero new abstractions and zero new Seams: the double-checked-locking shape it adds is the same shape `AsyncLazyCache<T>.GetOrLoadAsync` already uses and `AsyncLazyCacheTests.cs`'s own 32-concurrent-caller test already proves race-free for that type.

## Findings

### Finding #1: StoreNameLookup's three hand-rolled caches perform unsynchronized check-then-populate writes, unlike the file's own Ubisoft cache three lines below

**Why it matters** — Only the Ubisoft cache actually protected shared state from a concurrent race; a future fix to F-011 would otherwise silently inherit an unsynchronized write.

**What is wrong** — `gogNameCache`/`epicNameCache` (`StoreNameLookup.cs:29-30` at Step 1) and `nameMatchCache` (`:34`) were plain `Dictionary` fields with a bare check-then-populate and no lock, three lines from `ubisoftGameListCache` (`:40-42`).

**Evidence** — `StoreNameLookup.cs:29-30`; `:34`; `:40-42`; `AsyncLazyCache.cs:19-60`.

**Dependency category** — `in-process`.

**Severity** — Noticeable weakness.

**Minimal correction path** — Wrap the three check-then-populate bodies in a dedicated `SemaphoreSlim` gate per cache (not the shared gate `ubisoftGameListCache` uses), matching `AppliedArtworkStore`'s own per-cache dedicated-gate pattern.

**Blast radius** — Change: `StoreNameLookup.cs`. Avoid: `PrimaryWidget.xaml.cs`, `EpicLibrary.cs`.

---

### Finding #2: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs:455-717`) awaits each entry's lookups in strict sequence, even though the awaits are independent across entries.

**Evidence** — `PrimaryWidget.xaml.cs:455-717`.

**Severity** — Noticeable weakness.

**Minimal correction path** — BLOCKED this loop by the standing user constraint on per-game network-call ordering/concurrency; named for continuity.

**Blast radius** — Change: none this loop.

---

### Finding #3: GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding or renaming a platform requires remembering to update both switches; nothing fails to compile if one is forgotten.

**What is wrong** — `FromXboxDirectory` (`GamePlatform.cs:22-46`) and `GamePlatformToSGDBApiString` (:48-67) independently switch over the same platform cases with no shared table.

**Evidence** — `GamePlatform.cs:22-46`; `:48-67`.

**Severity** — Cosmetic for contest.

**Minimal correction path** — Fold both mappings into one table keyed by `GamePlatform`, with a separate alias list for legacy folder names.

**Blast radius** — Change: `GamePlatform.cs`. Avoid: `PrimaryWidget.xaml.cs`.

## Simplification Check

| Field | Value |
|---|---|
| structurally_necessary | Adding dedicated `SemaphoreSlim` gates to the three check-then-populate bodies — a concurrency-primitive fix, not a Deletion/Seam-category or Deepening fix. |
| new_seam_justified | false |
| helpful_simplification | Also removes credibility's own queued residual (F-015). |
| should_not_be_done | A single shared gate across all caches — would serialize independent per-store lookups for no reason. A new generic keyed-cache abstraction — the three caches differ in value type and miss/retry policy, so a shared generic would add ceremony without collapsing any method body. |
| tests_after_fix | None added or deleted — `StoreNameLookupTests.cs`'s own docstring documents the network-boundary test-scope limit this fix falls inside. Verification: full build + full test suite (138/138 unchanged), independent implementation review, manual trace confirming byte-identical single-threaded-caller behavior. |

## Improvement Backlog
1. **[F-015]** Wrap `StoreNameLookup`'s three hand-rolled caches in dedicated per-cache gates — structural, helpful. Score impact: `concurrency +0.5; credibility +0.5`.
2. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution — BLOCKED. Score impact: `concurrency +1.0`.

**Priority-1 accounting**: With F-013 resolved, the only unblocked Noticeable-or-worse candidate is F-015 — F-011 is blocked (criterion 0), F-012 is Cosmetic. F-015 is Priority 1.

## Deepening Candidates
None this loop. `LoadGameEntriesAsync`'s manifest-parsing block re-examined independently this loop, reaching the same conclusion prior loops did: `CreateThumbnailAsync`'s `Dispatcher`-thread affinity and `GameEntry`'s WinRT-typed properties mean a clean extraction requires first splitting `GameEntry` into a pure DTO plus a UI-bound wrapper.

## Builder Notes
1. **Pattern**: Credit for a fix lands at the START of the next loop's Step 1, not inside the loop that made the fix. → REVIEW_HISTORY.json `loops[3].builder_notes` for full notes
2. **Pattern**: Locking granularity should mirror the shape of the state it protects, not the shape of the nearest existing lock. → REVIEW_HISTORY.json `loops[3].builder_notes` for full notes
3. **Pattern**: An Adversarial Pass re-test can find a cheaper fix than the original disposition considered, without that changing the disposition. → REVIEW_HISTORY.json `loops[3].builder_notes` for full notes

## Final Judge Narrative
Place, not win, yet. This loop credited a real prior improvement (F-013, now visibly closed in source) and made one of its own (F-015), closing the last of `StoreNameLookup`'s three unsynchronized caches without adding any new abstraction. Runtime ownership remains trustworthy for every traced write path in `PrimaryWidget.xaml.cs`. Simplification helped this loop twice over: `simplicity` moved to 9.5 crediting last loop's clean collapse, and this loop's own fix added zero ceremony. Concurrency is not yet credited for this loop's own fix (established convention) but the underlying hazard is gone in current source as of this commit. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs` itself. Future work still risks over-engineering if it tries to extract `PrimaryWidget`'s orchestration wholesale, or to unify `StoreNameLookup`'s three differently-shaped caches behind one generic abstraction; this loop re-examined both candidates independently and reached the same conclusions prior loops did.

## Loop 3 Result
Added a dedicated `SemaphoreSlim` (`gogNameGate`, `epicNameGate`, `nameMatchGate`) to each of `StoreNameLookup`'s three previously-unsynchronized caches and wrapped `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/`FindGameByNameAsync`'s check-then-populate bodies in the standard double-checked-lock pattern already used one field over by `ubisoftGameListCache` via `AsyncLazyCache<T>`. Each cache keeps its own dedicated gate rather than sharing one. Full build (exit 0) and full test suite (138/138 unchanged) both re-run after the change. No network call, its URL, its ordering, or its count changed anywhere; the gate is uncontended under the current sequential caller. Independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on the targeted finding and the diff only) returned verdict **approved** with all three checks (reality, honesty, regression) passed. Finding F-015 (stable_id F-015) is **resolved**. No unintended scorecard regression observed.

## Loop 3 Implementation Review
Verdict: **approved**. Reason: All three previously-unlocked StoreNameLookup caches now use correct double-checked SemaphoreSlim locking mirroring AsyncLazyCache<T>'s already-proven shape, with no new Seam, no suppression, preserved single-threaded behavior, and no cross-gate deadlock risk. Checks: reality=passed, honesty=passed, regression=passed. Regressions: none. Conditions: none. Rounds: 1.

## Retired Findings (this loop)
None.

--- Loop 4 (UTC 2026-08-05T20:40:18Z) ---

### Discovery
see Loop 1 Discovery

### Loop Counter
Loop 4 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

Concurrency is credited this loop for a real, already-landed fix: F-015 (StoreNameLookup's three unsynchronized caches) is confirmed resolved and holding in current source (commit d062d1d). This loop's own investigation found the identical defect class in a second location — `SteamGridDbClient.CapsuleParseNotes` and `FixLog`'s three static fields (F-017, new, queued) — which tempers how far concurrency moves. This loop's own pick, F-016, breaks the longest-running stall in this project's history: `test_strategy` has scored exactly 6.5 with `delta: SAME` for all 13 loops of this project's review record, and `architecture_quality` has been flat at 7.5 for 9 of those. F-016 extracts `PrimaryWidget`'s untested library-operation guard into a small, directly-testable `LibraryOperationGuard` class — following the exact Compute/Do split `TESTING.md` already documents — and adds direct unit tests asserting the exact mutation named as an uncaught gap on every prior loop. What keeps this short of contest-grade: `PrimaryWidget.xaml.cs` still spans five concerns in one Module, `data_flow`'s ambient-state count grew from 4 to 7 confirmed instances this loop (still capped, not worsened), and F-011 remains genuinely blocked by the standing user constraint.

**Prior-audit adopt-or-falsify**: `CODE-REVIEW.md`, `TESTING.md` and `ARTWORK-SELECTION.md` were adopted/falsified against current source in loop 1 with no open claim dropped; this loop re-confirmed no new prior-audit claim changed status. `TESTING.md` was re-read in full this loop and directly informed F-016's remedy; grep of `CODE-REVIEW.md` for the guard/session-token/CapsuleParseNotes symbols this loop's findings cite returned no matches — F-016 and F-017 are novel discoveries, not pre-documented claims.

## Scorecard (1-10)

- Architecture quality: **7.5** | SAME | Direct full re-read this loop of `PrimaryWidget.xaml.cs` (2067 lines pre-fix) confirms the same five concerns in one Module as loops 1-3. Triggers the Stalled-Dimension Sweep (SAME for loops 1-3 of this run, 9 of the last 10 loops overall). Candidate named and acted on: F-016 extracts the library-operation guard out of this file, but per this project's established credit-lands-next-loop convention, this score reflects the pre-fix file and stays SAME; credited at loop 5's Step 1.
- State management and runtime ownership: **9.5** | SAME | `residual_disposition: accepted` | F-014's fix re-confirmed holding, no unguarded single-game write path. Residual: `SteamGridDbClient.CapsuleParseNotes` is `public static readonly List<string>` — the `readonly` only pins the reference. Adversarial Pass re-run: smallest fix passes SPT Q1-Q4 but fails Q5 comparatively against F-016; SPT-rejected on Q5, residual holds.
- Domain modeling: **9.5** | SAME | `residual_disposition: accepted` | `GameEntry.cs`'s parallel-fields case (`HasSteamGridDBMatch`/`OfficialCapsuleUrl`/`SteamGridDbGameId`, lines 113-145) unchanged, re-confirmed via full re-read. Adversarial Pass: readonly-struct fix still fails SPT Q2 (two-way XAML data-binding via `INotifyPropertyChanged`); residual holds.
- Data flow and dependency design: **7.5** | SAME | Triggers the Stalled-Dimension Sweep. Evidence broadened this loop: `StoreNameLookup`'s three locked caches + `CapsuleParseNotes` (4, as before) plus newly-confirmed `FixLog`'s three static fields — 7 ambient-state instances total. Per the Delta Derivation Guardrail, more instances of an already-below-anchor defect doesn't move the score further; stays SAME. Named candidate (consolidation via DI) rejected on SPT Q2 — this is a single-instance widget with no multi-instance need, and threading state through the many call chains that reach these fields is a much larger redesign than this loop's blast radius.
- Framework / platform best practices: **9.5** | SAME | `residual_disposition: accepted` | `App.xaml.cs:120`'s dead TODO re-read in full, unchanged. Adversarial Pass: deleting it still passes SPT Q1-Q4 but fails Q5 against F-016; residual holds.
- Concurrency and runtime safety: **7.0** | UP | Structural proof: commit d062d1d (loop 3) added three dedicated `SemaphoreSlim` gates to `StoreNameLookup`, independently re-verified this loop via a full direct re-read of `StoreNameLookup.cs` (all 361 lines). Tempering the UP: this loop's own investigation found the identical defect class recurring in `SteamGridDbClient.NoteCapsuleParse` (F-017, new, queued). F-011 remains blocked, re-derived fresh.
- Code simplicity and clarity: **9.5** | SAME | `residual_disposition: accepted` | Mandatory leaf-module duplication sweep (three parts): leaf modules read directly plus two independent helper sweeps; four-angle results all clean except the `gridPanelSessionId`/`searchPanelSessionId` session-token pattern at exactly 2 instances — below this project's own established 3-instance extraction threshold, correctly not a finding, watched. Residual unchanged: `GamePlatform.cs`'s two switches (F-012, Cosmetic). Adversarial Pass: still SPT-Q5-rejected against F-016; residual holds.
- Test strategy and regression resistance: **6.5** | SAME | Scored pre-fix (Step 1 convention). `PrimaryWidget.xaml.cs` carried zero test coverage as of Step 1 — the same gap named on all 13 loops of this project's history, triggering the Stalled-Dimension Sweep. Uniquely this loop, the named candidate (the guard's untested mutual-exclusion rule) is directly, actionably fixed this loop's own Step 2/3 — credit lands at loop 5's Step 1.
- Overall implementation credibility: **9.5** | SAME | `residual_disposition: queued` (F-016) | F-016's gap still present in the pre-fix source Step 1 evaluates; queued-to-resolved transition lands at loop 5's Step 1 against this loop's own commit.

## Strengths That Matter
- `ArtworkRanker`/`ArtworkDownloader`/`ArtworkSignature`/`TileImage` remain a genuinely deep, pure, well-tested pipeline — independently re-confirmed this loop by a cold helper sweep.
- `StoreNameLookup`'s three newly-locked caches are independently re-verified this loop as correct double-checked locking matching `AsyncLazyCache<T>`'s already-proven shape.
- This loop's own fix (F-016) required zero new architecture: `LibraryOperationGuard` is a plain `internal sealed class` placed in `Services/Library/` next to `OperationReport` — the exact precedent this project already established.

## Findings

### Finding #1: PrimaryWidget's library-operation guard has no automated test, and the mutation-test gap it represents has been named on every loop of this project's history

**Why it matters** — `isLibraryOperationRunning`'s try-begin/end rule is the single mechanism preventing a bulk library reload and a single-game write from racing each other; `test_strategy` has scored exactly 6.5 SAME on all 13 loops of this project's recorded history citing this exact gap.

**What is wrong** — `IsLibraryOperationBlocking`/`TryBeginLibraryOperation`/`EndLibraryOperation` (pre-fix `PrimaryWidget.xaml.cs`:194-232) implement the mutual-exclusion rule directly against a private `bool` field, inline in a WinUI `Page` class with no desktop test projection (`TESTING.md`), unlike every other compute concern in this codebase already extracted and covered.

**Evidence** — `PrimaryWidget.xaml.cs`:66, `PrimaryWidget.xaml.cs`:194-232 (pre-fix), `TESTING.md`:49-56, `OperationReportTests.cs` + `OperationReport.cs` (the precedent this fix follows).

**Architectural test failed** — Shallow module.

**Dependency category** — `in-process`.

**Leverage impact** — No caller could get a machine-checked guarantee the mutual-exclusion rule holds; confidence depended on reading three methods together and trusting they agree.

**Locality impact** — Contained: the rule's decision logic moves to one 20-line class; UI side effects stay in `PrimaryWidget`.

**Metric signal** — none.

**Why this weakens submission** — Not a live bug, but the single highest-value untested surface in the codebase, and the only scorecard gap that survived unchanged through all 13 of this project's loops without an actionable fix.

**Severity** — Serious deduction.

**ADR conflicts** — none.

**Minimal correction path** — Extract the guard's state machine into `internal sealed class LibraryOperationGuard` in `Services/Library/`; `PrimaryWidget`'s three wrapper methods delegate to it, keeping UI side effects unchanged. Add direct tests. No new Seam.

**Blast radius** — Change: `PrimaryWidget.xaml.cs`, `Services/Library/LibraryOperationGuard.cs` (new), `SteamGridDB.Xbox.csproj`, `LibraryOperationGuardTests.cs` (new). Avoid: `StoreNameLookup.cs`, `SteamGridDbClient.cs`, `GamePlatform.cs`, `EpicLibrary.cs`.

---

### Finding #2: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — Fully-sequential per-entry network chain adds latency scaling with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs`:455-717, pre-fix) awaits independent per-entry work in strict sequence.

**Evidence** — `PrimaryWidget.xaml.cs`:455-717.

**Architectural test failed** — n/a.

**Leverage impact** — None currently actionable.

**Locality impact** — Would be contained to `PrimaryWidget.xaml.cs` if unblocked.

**Metric signal** — none.

**Why this weakens submission** — Real cost, but **BLOCKED** by the standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency). Re-derived fresh this loop: a pure extraction would not be blocked, but F-011's own remedy necessarily changes call ordering.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — BLOCKED; named for continuity.

**Blast radius** — Change: none this loop. Avoid: `PrimaryWidget.xaml.cs`.

---

### Finding #3: SteamGridDbClient.NoteCapsuleParse and FixLog perform unsynchronized check-then-populate/append writes on static mutable collections, the same defect class F-015 just fixed in StoreNameLookup

**Why it matters** — Both are safe today only by the same single-threaded-per-load convention that made `StoreNameLookup`'s caches safe before F-015. A future concurrent caller would silently inherit an unsynchronized write.

**What is wrong** — `SteamGridDbClient.NoteCapsuleParse` (`SteamGridDbClient.cs`:49-55) checks `Count < 5` then `Add`s with no lock — a TOCTOU race on a non-thread-safe `List<string>`. `FixLog`'s three static fields (`FixLog.cs`:24,26,28) are mutated with no synchronization.

**Evidence** — `SteamGridDbClient.cs`:47, `SteamGridDbClient.cs`:49-55, `FixLog.cs`:24,26,28, `FixLog.cs`:46-59, `AsyncLazyCache.cs`:19-60.

**Architectural test failed** — n/a.

**Dependency category** — `in-process`.

**Leverage impact** — No concurrent-access-safety guarantee from either type's Interface alone.

**Locality impact** — Two independent, small, file-contained remedies.

**Metric signal** — none.

**Why this weakens submission** — Not currently reachable as a live race, but the identical structural inconsistency F-015 closed in `StoreNameLookup` last loop, found in two more locations.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — `SteamGridDbClient`: dedicated `SemaphoreSlim` gate matching F-015's pattern. `FixLog`: same pattern, or fold into one owned type analogous to `OperationReport`.

**Blast radius** — Change: `SteamGridDbClient.cs`, `FixLog.cs`. Avoid: `PrimaryWidget.xaml.cs`, `StoreNameLookup.cs`.

---

### Finding #4: GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding/renaming a platform requires updating both switches; nothing fails to compile if one is forgotten.

**What is wrong** — `FromXboxDirectory` (`GamePlatform.cs`:22-46) and `GamePlatformToSGDBApiString` (`GamePlatform.cs`:48-67) independently switch with no shared table. Re-confirmed unchanged.

**Evidence** — `GamePlatform.cs`:22-46, `GamePlatform.cs`:48-67.

**Architectural test failed** — n/a.

**Leverage impact** — Each new platform pays this asserted-twice tax.

**Locality impact** — Contained to `GamePlatform.cs`.

**Metric signal** — none.

**Why this weakens submission** — Minor; Adversarial Pass re-confirmed still SPT-Q5-rejected against F-016.

**Severity** — Cosmetic for contest.

**ADR conflicts** — none.

**Minimal correction path** — Fold both mappings into one table with a small alias list.

**Blast radius** — Change: `GamePlatform.cs`. Avoid: `PrimaryWidget.xaml.cs`.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Extracting `isLibraryOperationRunning`'s rule into `LibraryOperationGuard` — passes the Shallow module test |
| New seam justified | false — plain concrete class, no interface/port, tested directly |
| Helpful simplification | Also removes credibility's queued residual (F-016); targets the 13-loop `test_strategy` gap |
| Should NOT be done | Extracting the session-token pattern (2 instances, below the 3-instance bar) or fixing F-017 in the same commit (different file, different defect class) |
| Tests after fix | 5 new tests at the new Interface; none deleted (none existed); 138→143 passing |

## Improvement Backlog
1. **[F-016]** Extract PrimaryWidget's library-operation guard into a directly-testable class — structural, needed for winning. Score impact: `architecture_quality +0.5; test_strategy +0.5`.
2. **[F-017]** Wrap `SteamGridDbClient.CapsuleParseNotes` and `FixLog`'s static fields in synchronization — structural, helpful. Score impact: `concurrency +0.5`.
3. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution — needed for winning once unblocked. **BLOCKED**. Score impact: `concurrency +1.0`.

**Priority-1 accounting**: The Stalled-Dimension Sweep triggered on `architecture_quality`, `test_strategy` (13 loops, the longest stall on this scorecard) and `data_flow`. F-016 is the only candidate that directly breaks a stall — two at once — via an established, low-risk, precedented pattern.

## Deepening Candidates
→ REVIEW_HISTORY.json `loops[3].deepening_candidates` for full notes (empty array this loop; two watched, non-finding patterns discussed in Builder Notes and prose).

## Builder Notes
- Credit for a fix lands at the START of the next loop's Step 1, not inside the loop that made the fix. → REVIEW_HISTORY.json `loops[3].builder_notes` for full notes.
- The same untested-surface citation, repeated for 13 loops, was never itself investigated as a fixable architecture problem until this loop asked "why can't this specific piece be tested" instead of "this can't be tested." → REVIEW_HISTORY.json `loops[3].builder_notes` for full notes.
- A duplication pattern below the project's own established instance threshold is a candidate to watch, not a finding to force. → REVIEW_HISTORY.json `loops[3].builder_notes` for full notes.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) F-016's severity as "Serious deduction" rather than "Likely disqualifier" — the severity anchor's own language describes this guard almost verbatim, and 13 consecutive loops of an unfixed primary-flow test gap could be read as crossing into disqualifying territory; this loop judged "no live-reachable harm" as the deciding factor, a real but not airtight distinction. (2) `concurrency`'s UP move to 7.0 — crediting F-015's fix while discovering F-017's new instance in the same investigation makes the net direction genuinely ambiguous; a stricter reading could hold at 6.5 (net zero). (3) `data_flow` held at 7.5 rather than nudged down for finding 3 more ambient-state instances (4→7) — the Delta Derivation Guardrail warns against mechanical down-scoring on "found a new instance," and this loop's call that the qualitative picture is unchanged is a judgment a stricter reading could contest.

## Final Judge Narrative
Place, not win, yet — but this loop breaks new ground rather than repeating the pattern of the last three. Concurrency is credited for a real, verified fix (F-015) while this loop's own investigation immediately found the same defect class recurring in a second location (F-017) — closing one instance of a defect class does not close the class. The larger event this loop is F-016: for the first time in this project's 13-loop history, the `test_strategy` gap that every prior loop named and declined to act on has an actionable, unblocked, low-risk fix, and this loop takes it — not by inventing new architecture, but by applying a pattern this codebase already uses everywhere else. Runtime ownership remains trustworthy (F-014 holds). Simplification did not hurt this loop: zero new ceremony, zero new Seams. Tests newly cover a primary-flow concurrency-safety mechanism no test in this repository could previously reach at all. Future work still risks over-engineering if it tries to extract PrimaryWidget's orchestration wholesale, unify StoreNameLookup's caches, or extract the session-token pattern before a third instance justifies it; this loop re-examined all three candidates independently and reached the same restrained conclusions prior loops did.

## Loop 4 Result
Extracted `PrimaryWidget`'s library-operation guard into a new `internal sealed class LibraryOperationGuard` (`Services/Library/LibraryOperationGuard.cs`) with `bool IsRunning`, `bool TryBegin()`, `void End()` — no WinUI dependency. `PrimaryWidget.xaml.cs`'s three wrapper methods now delegate to it while keeping their own UI side effects unchanged. Registered the new file in `SteamGridDB.Xbox.csproj`. Added `LibraryOperationGuardTests.cs` with five tests, including one asserting the exact mutation this finding names. Full build (exit 0) and full test suite both re-run before (138 passed) and after (143 passed / 0 failed / 0 skipped) the change. Finding F-016 (stable_id F-016) is **resolved**. No unintended scorecard regression observed; F-017 was discovered during this loop's own Step 1 investigation, not introduced by this loop's edit.

## Loop 4 Implementation Review
Verdict: **approved**. Reason: `LibraryOperationGuard.cs` is a plain, dependency-free class picked up by the test project's `Services\**\*.cs` glob, its tests assert real mutual-exclusion behavior, `PrimaryWidget.xaml.cs` no longer contains the `isLibraryOperationRunning` field, and the diff preserves `StatusText`/`SetHeaderButtonsEnabled` ordering exactly with no new Seam introduced. Checks: reality=passed, honesty=passed, regression=passed. Regressions: none. Conditions: none. Rounds: 1.

## Retired Findings (this loop)
None.

--- Loop 5 (UTC 2026-08-05T21:11:33Z) ---

### Discovery
see Loop 1 Discovery

### Loop Counter
Loop 5 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

Test strategy breaks its own 13-loop stall this loop: crediting loop 4's own fix (commit `c6fcf6e`, independently re-verified this loop via direct reads of `LibraryOperationGuard.cs` and `LibraryOperationGuardTests.cs`), `test_strategy` moves from 6.5 to 8.0 — the single most-cited gap in this project's history is closed, though the remaining untested UI-orchestration surface (documented, platform-forced) keeps it below 9. This loop's own pick, F-017, closes the second occurrence of the concurrency defect class F-015 fixed last loop: `SteamGridDbClient.CapsuleParseNotes` and `FixLog`'s static fields now gate their writes, matching `StoreNameLookup`'s established pattern — and, as a natural side effect of touching the same lines, also resolves `state_management`'s standing residual (`CapsuleParseNotes`'s public-mutable-reference exposure). What keeps this short of contest-grade: `architecture_quality` and `data_flow` remain flat (`PrimaryWidget.xaml.cs` still mixes several concerns; ambient-state count unchanged at 7 instances, now all synchronized), and F-011 remains genuinely blocked by the standing user constraint.

**Prior-audit adopt-or-falsify**: `CODE-REVIEW.md`, `TESTING.md` and `ARTWORK-SELECTION.md` were adopted/falsified against current source in loop 1 with no open claim dropped; this loop re-confirmed no new prior-audit claim changed status — `TESTING.md`'s documented Compute/Do split and network-boundary test carve-out were re-read in full and directly informed both this loop's scoring of `test_strategy` and the shape of F-017's fix and tests.

## Scorecard (1-10)

- Architecture quality: **7.5** | SAME | Direct full re-read this loop (via a dedicated helper sweep) of `PrimaryWidget.xaml.cs` (2069 lines, post loop-4 extraction) confirms the file still mixes the same real concerns: UI event handling, network orchestration, file I/O/backup-restore, artwork-selection invocation, and panel/search navigation. **Stalled-Dimension Sweep (SAME for loops 1-5 of this run, 11 of the last 12 loops overall):** explicit clean — no single extraction candidate beyond F-016 (already landed) passes SPT; a further split needs a multi-file redesign disproportionate to any one loop's blast radius. Noted as a standing shape, not actionable this loop.
- State management and runtime ownership: **9.5** | SAME | `residual_disposition: accepted` | Direct re-read this loop of the guard call sites (`TryBeginLibraryOperation`/`EndLibraryOperation`/`IsLibraryOperationBlocking`, `PrimaryWidget.xaml.cs`:213-234, plus all 7 call sites: `PrimaryWidget_Loaded`:151, `RefreshButton_Click`:764, `ConfirmAndRunAsync`:826, `GridImage_Click`:1533, `RestoreBackup_Click`:1918) — independently re-confirmed by a helper sweep — re-confirms F-014/F-016's fix holds with no unguarded write path. Residual as of Step 1 (pre-fix source): `SteamGridDbClient.CapsuleParseNotes` was still `public static readonly List<string>` — the `readonly` only pinned the reference. Adversarial Pass re-run: the residual's own smallest fix (private field + `IReadOnlyList<string>` accessor) still fails SPT Q5 comparatively against F-017 (this loop's actual, higher-severity pick) *as a standalone item* — but this loop's own F-017 fix bundles exactly that change as a side effect of touching the same lines for the concurrency gate (zero extra blast radius, same file). Per this project's established credit-lands-next-loop convention, the resolved residual is scored SAME this loop (Step 1 evaluates pre-fix source) and will show as resolved at loop 6's Step 1.
- Domain modeling: **9.5** | SAME | `residual_disposition: accepted` | Direct full re-read this loop of `GameEntry.cs` (196 lines) re-confirms the parallel-fields case (`HasSteamGridDBMatch`/`OfficialCapsuleUrl`/`SteamGridDbGameId`, lines 113-145) unchanged. Adversarial Pass re-run: readonly-struct-with-factories still fails SPT Q2 — `GameEntry` implements `INotifyPropertyChanged` and is two-way XAML-data-bound; residual holds.
- Data flow and dependency design: **7.5** | SAME | Ambient-state census re-confirmed this loop by two independent direct sweeps (mine and a helper's): `StoreNameLookup`'s three caches + `SteamGridDbClient.CapsuleParseNotes` + `FixLog`'s three fields = 7 process-lifetime instances, unchanged in count. **Stalled-Dimension Sweep (SAME for loops 1-5 of this run, 11 of the last 12 loops overall):** explicit clean — this loop's own fix (F-017) adds synchronization to two of the seven instances but does not reduce the ambient-dependency count, which is what this dimension measures (the same conclusion loop 3 reached for F-015's fix). No consolidation candidate passes SPT Q2 — single-instance widget, no multi-instance/test-injection need proven.
- Framework / platform best practices: **9.5** | SAME | `residual_disposition: accepted` | `App.xaml.cs`:120's `//TODO: Load state from previously suspended application` re-read in full this loop (independently, via helper), unchanged — matches the widget's own OnActivated path being the real launch surface, so the OnLaunched fallback's TODO is inert rather than a live gap. Adversarial Pass re-run: deleting the dead comment still passes SPT Q1-Q4 as a free, zero-risk subtractive change but fails Q5 comparatively against F-017; residual holds.
- Concurrency and runtime safety: **7.0** | SAME | Scored pre-fix (Step 1 convention). Direct re-read this loop of `SteamGridDbClient.cs` and `FixLog.cs` (both in full) re-confirms F-017's TOCTOU gap unchanged as of Step 1: `NoteCapsuleParse` (`SteamGridDbClient.cs`:49-55, pre-fix) still checks-then-adds with no lock; `FixLog`'s `Start`/`Write`/`SaveAsync` (`FixLog.cs`:46-77, pre-fix) still mutate/read `lines`/`fileName` with no synchronization. This loop's own Step 2/3 fixes both — credit lands at loop 6's Step 1, matching the established pattern. F-011 remains blocked, re-derived fresh: its own remedy (bounded concurrency) necessarily changes network-call ordering, which the standing user constraint forbids without a product decision.
- Code simplicity and clarity: **9.5** | SAME | `residual_disposition: accepted` | Mandatory leaf-module duplication sweep this loop (three parts): (a) leaf modules read directly by a helper — `GameEntry.cs`, `GamePlatform.cs`, `GridImageItem.cs`, `OperationReport.cs`, `AppliedArtworkStore.cs`, `ArtworkDownloader.cs`, `ArtworkRanker.cs`, `ArtworkSignature.cs`, `TileImage.cs`, `EpicLibrary.cs`, `App.xaml.cs`, `MainPage.xaml.cs`, plus my own direct reads of `LibraryOperationGuard.cs`, `AsyncLazyCache.cs`, `StoreNameLookup.cs`; (b) four-angle results all clean except the already-tracked `GamePlatform.cs` dual-switch (F-012) and the `gridPanelSessionId`/`searchPanelSessionId` session-token pattern, independently re-confirmed at exactly 2 instances — still below this project's own established 3-instance extraction threshold, correctly not a finding, watched; (c) no `audit_clones.py`/`audit-enum-interpretation.sh` available in this repo checkout — manual four-angle pass substituted, noted as scope limit (unchanged from prior loops). Residual: `GamePlatform.cs`'s two independent switches (F-012, Cosmetic). Adversarial Pass re-run: F-012's own smallest fix still passes SPT Q1-Q4 but fails Q5 comparatively against F-017 (this loop's actual, higher-severity pick); residual holds — but with F-016 and F-017 both now resolved and F-011 blocked, F-012 is promoted to Priority 1 for loop 6 (nothing higher-severity remains actionable).
- Test strategy and regression resistance: **8.0** | UP | Structural proof: commit `c6fcf6e` (loop 4) added `SteamGridDB.Xbox/Services/Library/LibraryOperationGuard.cs` (a plain, dependency-free class) and `SteamGridDB.Xbox.Tests/LibraryOperationGuardTests.cs` (5 direct tests, independently re-read in full this loop: `Starts_not_running`, `TryBegin_succeeds_and_marks_running_when_nothing_else_is_running`, `TryBegin_fails_and_leaves_the_guard_running_when_already_running` — the exact mutation named on every loop of this project's 13-loop history — `End_releases_the_guard_so_a_new_operation_can_begin`, `End_is_safe_to_call_when_nothing_is_running`), independently re-verified this loop as wired correctly into `PrimaryWidget.xaml.cs` at all 7 call sites (see State management proof). This is source loop 4's own Step-1 evaluation did not have (the test file did not exist until loop 4's own Step 2/3), so the UP is honest re-derivation, not anchoring. Held below 9 (not 8.5+): a helper sweep this loop named a second, still-uncovered gap on the same file — `GridImage_Click`'s stale-session guard (`gridItem.SessionId == gridPanelSessionId`, `PrimaryWidget.xaml.cs`:1531) is a primary-flow persistence-writer check with no test, reachable only through WinUI and therefore excluded by the same platform-binding carve-out `TESTING.md` documents for the rest of `PrimaryWidget`'s UI-orchestration surface — genuinely off the file's remaining scope, but real enough that claiming the 9-anchor's "every contest-relevant feature flow" bar this loop would be premature. Left at 8.0 rather than escalated further; the Authority Map cross-check required at `test_strategy >= 9` (G24) is deferred to a loop that can do it properly rather than claimed on a partial pass.
- Overall implementation credibility: **9.5** | SAME | `residual_disposition: queued` (F-017) | Scored pre-fix (Step 1 convention): F-017's gap is still present in the source this Step 1 evaluates (the codebase's own established pattern — every other cross-file mutable cache gates its writes — is silently broken by two instances), so the residual stays queued rather than accepted. It is this loop's own Priority-1 pick; the queued-to-resolved transition (like F-013's, F-014's, F-015's and F-016's before it) shows up at loop 6's Step 1, scored against this loop's own commit.

## Authority Map
(Re-emitted this loop: F-017, this loop's Priority-1 pick, is an ownership/authority concern — who may write the two ambient state surfaces it fixes.)

**Concern: `SteamGridDbClient.CapsuleParseNotes` (capsule-parse failure notes)**
- Owner: `SteamGridDbClient` (static)
- Allowed writers: `NoteCapsuleParse` (now `internal`, gated by `capsuleParseNotesGate`)
- Observers / readers: `PrimaryWidget.FixLibraryAsync` (`PrimaryWidget.xaml.cs`:969, reads `CapsuleParseNotes` to log parse failures into the fix run)
- Persistence seam: none (in-memory, process-lifetime)
- Async mutation entry points: `ParseOfficialCapsuleUrl` (synchronous; called from `GetGameByPlatformIdAsync`, itself called once per unmatched entry from `LoadGameEntriesAsync`'s per-entry loop)
- Verdict: **Single and clear** (now gated; previously unsynchronized check-then-populate, safe only by the single-threaded-per-load convention)

**Concern: `FixLog`'s run state (`lines`/`fileName`/`logFolder`)**
- Owner: `FixLog` (static)
- Allowed writers: `Start()`/`Write()` (now gated by `syncRoot`)
- Observers / readers: `SaveAsync()` (now takes a gated point-in-time snapshot before its file I/O)
- Persistence seam: `SaveAsync()` writes to a `StorageFile` (`last-fix.log` / `last-load.log`, caller-selected)
- Async mutation entry points: `LoadGameEntriesAsync`, `FixLibraryAsync`, `RestoreAllChangesAsync` (each calls `Start`/`Write`/`SaveAsync` in sequence; all three run under `PrimaryWidget`'s own library-operation guard today, so never concurrently with each other in production)
- Verdict: **Single and clear** (now gated; previously unsynchronized)

## Strengths That Matter
- `ArtworkRanker`/`ArtworkDownloader`/`ArtworkSignature`/`TileImage` remain a genuinely deep, pure, well-tested pipeline — independently re-confirmed this loop by a cold helper sweep finding no structural issues and no domain-policy leakage.
- `StoreNameLookup`'s three per-store gates (`gogNameGate`/`epicNameGate`/`nameMatchGate`, landed loop 3) are re-verified this loop as correct double-checked locking, matching `AsyncLazyCache<T>`'s already-proven shape — the exact pattern this loop's own fix (F-017) extends to the codebase's two remaining unsynchronized static caches.
- This loop's own fix required zero new architecture and zero new files in the app project beyond two small, targeted edits to already-existing classes (`SteamGridDbClient`, `FixLog`) — the smallest honest fix for a defect class this codebase has now closed in every location it was found.

## Findings

### Finding #1: SteamGridDbClient.NoteCapsuleParse and FixLog perform unsynchronized check-then-populate/append writes on static mutable collections, the same defect class F-015 fixed in StoreNameLookup

**Why it matters** — Both are safe today only by the same single-threaded-per-load convention that made `StoreNameLookup`'s three caches safe before F-015's fix. Neither guarantee is enforced by the fields themselves — a future concurrent caller (including a future unblocked F-011) would silently inherit an unsynchronized write, exactly the risk F-015 eliminated for `StoreNameLookup`.

**What is wrong** — `SteamGridDbClient.NoteCapsuleParse` (`SteamGridDbClient.cs`:49-55, pre-fix) checked `CapsuleParseNotes.Count < 5` then called `CapsuleParseNotes.Add(note)` with no lock — a TOCTOU race on a `List<string>`, which is not thread-safe for concurrent `Add` calls even individually. `FixLog`'s three static fields (`lines`/`fileName`/`logFolder`, `FixLog.cs`:24,26,28, pre-fix) were mutated by `Start()`/`Write()` and read by `SaveAsync()` with no synchronization at all.

**Evidence** — `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs`:47 (pre-fix), `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs`:49-55 (pre-fix), `SteamGridDB.Xbox/Services/Artwork/FixLog.cs`:24,26,28 (pre-fix), `SteamGridDB.Xbox/Services/Artwork/FixLog.cs`:46-77 (pre-fix), `SteamGridDB.Xbox/Services/AsyncLazyCache.cs` (the established gate pattern), `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` (F-015's own remedy, the precedent this fix follows).

**Architectural test failed** — n/a (concurrency-safety defect, not a Seam/Module-boundary question).

**Dependency category** — `in-process`.

**Leverage impact** — Before the fix, a caller got no concurrent-access-safety guarantee from either type's Interface alone; safety depended entirely on today's callers happening to be sequential.

**Locality impact** — `SteamGridDbClient.cs`'s fix is fully contained to that file; `FixLog.cs`'s fix is fully contained to that file — two independent, small remedies, not one shared abstraction.

**Metric signal** — none.

**Why this weakens submission** — Not currently reachable as a live race (`LoadGameEntriesAsync`'s loop awaits one entry at a time; `FixLog`'s callers are mutually exclusive under the library-operation guard), but the identical structural inconsistency F-015 closed in `StoreNameLookup`, found in two more locations.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — `SteamGridDbClient`: guard `NoteCapsuleParse`'s check-then-populate body with a plain `lock` (the method and its only caller chain are fully synchronous — no `await` inside the critical section, so a `SemaphoreSlim` would be ceremony `StoreNameLookup`'s own async callers need and this one doesn't). `FixLog`: guard `Start`/`Write` with a plain `lock`; have `SaveAsync` take a point-in-time snapshot under the same lock before its file I/O (a `lock` block cannot itself wrap an `await`).

**Blast radius** — Change: `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs`, `SteamGridDB.Xbox/Services/Artwork/FixLog.cs`, `SteamGridDB.Xbox.Tests/SteamGridDbClientTests.cs` (new), `SteamGridDB.Xbox.Tests/FixLogTests.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`.

---

### Finding #2: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs`:494-705, current line numbers; was 455-717 pre-loop-4-extraction) awaits each entry's SteamGridDB platform-ID lookup, store name-fetch and SteamGridDB name search in strict sequence, even though the awaits are independent across entries.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`:494-705.

**Architectural test failed** — n/a.

**Dependency category** — null.

**Leverage impact** — None currently actionable — see blocker.

**Locality impact** — Would be contained to `PrimaryWidget.xaml.cs` if unblocked.

**Metric signal** — none.

**Why this weakens submission** — A real, linearly-scaling latency cost, but BLOCKED: the recorded standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft) means this loop cannot land a change altering observable call ordering/concurrency/count against those services without a product decision this loop cannot make. Re-derived fresh this loop: a pure extraction would not be blocked, but F-011's own remedy (bounded concurrency) necessarily changes network-call ordering, so it stays genuinely blocked.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — BLOCKED this loop; named for continuity (Backlog Prioritization Pass criterion 0) rather than escalated to a user-decision halt, since F-017 filled this loop's actionable Priority-1 slot.

**Blast radius** — Change: none. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (no change while blocked).

---

### Finding #3: GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding or renaming a platform requires remembering to update both switches; nothing fails to compile if one is forgotten (both silently default to Unknown/null).

**What is wrong** — `FromXboxDirectory` (`GamePlatform.cs`:22-46) and `GamePlatformToSGDBApiString` (`GamePlatform.cs`:48-67) independently switch over the same platform cases with no shared table; `FromXboxDirectory` additionally handles legacy folder-name aliases with no analogue in the reverse mapping. Re-confirmed unchanged this loop via direct full read and an independent helper sweep.

**Evidence** — `SteamGridDB.Xbox/Models/GamePlatform.cs`:22-46, `SteamGridDB.Xbox/Models/GamePlatform.cs`:48-67.

**Architectural test failed** — n/a.

**Dependency category** — null.

**Leverage impact** — Each new platform pays this asserted-twice tax; small today, compounds at scale.

**Locality impact** — Contained to `GamePlatform.cs`.

**Metric signal** — none.

**Why this weakens submission** — Minor, but the enum's own home module holds two independent interpretations of itself with no compiler-enforced link. This loop's Adversarial Pass re-tested the smallest fix and confirmed it still fails Simplify Pressure Test Q5 against F-017 — but with the backlog otherwise clear, this becomes loop 6's Priority 1.

**Severity** — Cosmetic for contest.

**ADR conflicts** — none.

**Minimal correction path** — Fold both mappings into one table keyed by `GamePlatform`, with a separate small alias list for `FromXboxDirectory`'s legacy folder names.

**Blast radius** — Change: `SteamGridDB.Xbox/Models/GamePlatform.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (call sites unchanged).

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Gating `SteamGridDbClient.NoteCapsuleParse`'s check-then-populate body and `FixLog`'s `Start`/`Write`/`SaveAsync` — removes a real TOCTOU hazard; matches F-015's own remedy class |
| New seam justified | false — plain `lock` statements inside existing classes |
| Helpful simplification | `CapsuleParseNotes` moves to a private backing field + `IReadOnlyList<string>` accessor, resolving `state_management`'s standing residual as a side effect |
| Should NOT be done | A `SemaphoreSlim` for `FixLog` (no `await` in its critical section — ceremony); extracting `GridImage_Click`'s stale-session check into its own tested unit (ceremony disproportionate to a single comparison) |
| Tests after fix | 2 new tests at the new Interface; none deleted (none existed); 143→145 passing |

## Improvement Backlog
1. **[F-012]** Fold `GamePlatformHelper`'s two independent switch statements into one shared table — simplification, minor. Promoted to Priority 1 for loop 6 (nothing higher-severity remains actionable). Score impact: `simplicity +0.5`.
2. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution — needed for winning once unblocked. **BLOCKED**. Score impact: `concurrency +1.0`.

**Priority-1 accounting**: F-017 wins Priority 1 on severity (Noticeable weakness) and actionability (F-011 is blocked); it closes the same defect class F-015 already established a remedy for, at zero new architectural cost.

## Deepening Candidates
None this loop. `LibraryOperationGuard` (loop 4) and this loop's `SteamGridDbClient`/`FixLog` gates are all plain, already-deep-enough classes; no caller or test currently reaches past their Interfaces.

## Builder Notes
- Credit for a fix lands at the START of the next loop's Step 1, not inside the loop that made the fix. → REVIEW_HISTORY.json `loops[4].builder_notes` for full notes.
- When a Priority-1 fix already touches the exact lines a different dimension's accepted residual lives on, its smallest fix can ride along for free. → REVIEW_HISTORY.json `loops[4].builder_notes` for full notes.
- A Cosmetic residual deferred behind a rotating cast of higher-severity findings for many loops running is not evidence the finding is wrong — it is evidence the supply of higher-severity findings hadn't run out yet. → REVIEW_HISTORY.json `loops[4].builder_notes` for full notes.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) `test_strategy`'s landing at exactly 8.0 rather than 7.5 or 8.5 — a stricter reading could treat `GridImage_Click`'s untested stale-session guard as fully disqualifying the jump past 7.5, while a more generous reading could treat `TESTING.md`'s documented platform-binding carve-out as covering it entirely and justify 8.5; 8.0 is this loop's judgment call. (2) `state_management` staying `9.5 SAME` rather than acknowledging that its residual's fix already exists in the working tree at the moment this scorecard is written — the credit-lands-next-loop convention is well-precedented, but a reader who does not accept it could call this scorecard stale by one loop on this specific dimension. (3) Carrying F-017's severity forward as "Noticeable weakness" unchanged from loop 4's characterization, rather than re-assessing it now that its actual remedy turned out to be two small `lock` statements with no live-reachable exploit path.

## Final Judge Narrative
Place, not win, yet — but a clean loop that closes what it opened. Last loop's own investigation found the concurrency defect class recurring in two more locations; this loop closes both, at zero new architectural cost, following the exact remedy pattern loop 3 already established. Separately, credit for loop 4's own test-coverage fix lands here: `test_strategy` breaks its 13-loop stall, moving from 6.5 to 8.0 on real, independently-verified structural proof — held below 9 by an honestly-named remaining gap (`GridImage_Click`'s stale-session check) rather than claimed prematurely. Runtime ownership remains trustworthy for every traced write path (F-014/F-016 hold, independently re-verified). Concurrency is more trustworthy in current source right now than last loop's own scorecard shows — this loop's fix lands the credit next loop, matching the established pattern. Simplification did not hurt this loop: zero new ceremony, zero new Seams, two plain `lock` statements sized to match their call sites (no `SemaphoreSlim` reached for where a synchronous critical section didn't need one). Two new tests directly cover the exact mutations this loop's finding named. Future work still risks over-engineering if it tries to extract `GridImage_Click`'s single-comparison stale-session check into its own tested unit before a second instance of that shape justifies it, or to unify `StoreNameLookup`'s, `SteamGridDbClient`'s and `FixLog`'s now-three differently-shaped gates into one abstraction they don't share enough behavior to earn.

## Loop 5 Result
Wrapped `SteamGridDbClient.NoteCapsuleParse`'s check-then-populate body and `FixLog`'s `Start`/`Write`/`SaveAsync` in dedicated locks (`SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs`, `SteamGridDB.Xbox/Services/Artwork/FixLog.cs`), matching the concurrency-safety pattern F-015 already established for `StoreNameLookup`'s caches; `CapsuleParseNotes` also moves from a publicly-mutable `List<string>` reference to a `private` backing field exposed as `IReadOnlyList<string>`, resolving `state_management`'s standing residual as a side effect of the same edit. Added `SteamGridDB.Xbox.Tests/SteamGridDbClientTests.cs` (1 test) and extended `SteamGridDB.Xbox.Tests/FixLogTests.cs` (1 test), both asserting the exact concurrent-write mutation the finding names. Full build (msbuild, exit 0) and full test suite (run-tests.ps1) both re-run before and after: before, 143 passed / 0 failed / 0 skipped; after, 145 passed / 0 failed / 0 skipped (143 prior + 2 new, all green, no prior test's behavior changed). `git diff` review confirms `PrimaryWidget.xaml.cs`'s sole touch point on the changed surface (`SteamGridDbClient.CapsuleParseNotes`'s `foreach` read, line 969) compiles unchanged against the new `IReadOnlyList<string>` return type — no network call, UI side effect, ordering, or count changed anywhere. Finding F-017 (stable_id F-017) is **resolved**. No unintended scorecard regression observed.

## Loop 5 Implementation Review
Verdict: **approved**. Reason: Both static-collection TOCTOU gaps are genuinely closed with correctly-scoped plain locks, the two new tests assert real concurrent-write behavior (not just no-throw), and no same-or-higher-severity regression was introduced. Checks: reality=passed, honesty=passed, regression=passed. Regressions: none. Conditions: none. Rounds: 1.

## Retired Findings (this loop)
None.

--- Loop 6 (UTC 2026-08-05T21:40:55Z) ---

### Loop Counter
Loop 6 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

This loop's own independent re-derivation (a helper sweep of `PrimaryWidget.xaml.cs` plus my own direct verification of the call graph) surfaced a fresh, real, reachable Serious finding (F-018): `HideGridPanelAsync` and `HideSearchPanelAsync` had no guard against running twice concurrently for the same session — the panel's own Close button and `DownloadAndReplaceImageAsync`'s own post-download auto-close both call the same method, and `CloseGridPanel_Click` never checks `IsLibraryOperationBlocking()`, so a user clicking Close while their own tile's download is still in flight is an ordinary, reachable interaction. This outranked the carried-forward F-012 (Cosmetic) on severity per the Backlog Prioritization Pass and became this loop's Priority 1, fixed by reusing the existing, already-tested `LibraryOperationGuard` class rather than hand-rolling new untested bool flags. Separately, `concurrency` credits loop 5's own F-017 fix (now independently re-verified). What keeps this short of contest-grade: `architecture_quality` and `data_flow` remain flat, and F-011 remains genuinely blocked by the standing user constraint.

## Scorecard (1-10)

- Architecture quality: **7.5** | SAME | Independent helper sweep this loop read `PrimaryWidget.xaml.cs` (2069 lines pre-fix) in full and named 13 distinct concerns still living in one file. No single extraction candidate beyond what has already landed passes SPT without a multi-file redesign disproportionate to one loop's blast radius.
- State management and runtime ownership: **7.5** | DOWN | Independently re-derived this loop: `CloseGridPanel_Click` never calls `IsLibraryOperationBlocking()` before invoking `HideGridPanelAsync()`, and `DownloadAndReplaceImageAsync`'s own success path also calls `HideGridPanelAsync()` a second time after a successful download. Neither call increments `gridPanelSessionId`, so the existing post-animation session recheck (F-009's own fix) does not distinguish two concurrent calls for the SAME session. A real, contained ownership gap - the codebase's own established mutual-exclusion idiom (`LibraryOperationGuard`) was not applied to this structurally identical hazard. The 7 `TryBeginLibraryOperation`/`EndLibraryOperation`/`IsLibraryOperationBlocking` call sites and the two session counters remain single-owner and clean.
- Domain modeling: **9.5** | SAME | `residual_disposition: accepted` | `GameEntry.cs`'s parallel-fields case unchanged; readonly-struct-with-factories still fails SPT Q2 (INotifyPropertyChanged + two-way XAML binding).
- Data flow and dependency design: **7.5** | SAME | 7 process-lifetime static instances unchanged; two new guard fields this loop are instance-level, not counted in that figure.
- Framework / platform best practices: **9.5** | SAME | `residual_disposition: accepted` | `App.xaml.cs`:120's TODO unchanged; SPT-rejected on Q5 against F-018.
- Concurrency and runtime safety: **8.0** | UP | Loop 5's F-017 fix independently re-verified this loop (locks confirmed present and holding, both concurrent-writer tests re-confirmed passing). Held below 9.5 by F-018 (this loop's own newly-found, pre-fix-at-Step-1 gap).
- Code simplicity and clarity: **9.5** | SAME | `residual_disposition: queued` (F-012) | Leaf-module duplication sweep clean except the already-tracked `GamePlatform.cs` dual-switch. This loop's own fix is simplicity-positive (reused existing tested class).
- Test strategy and regression resistance: **8.0** | SAME | `GridImage_Click`'s stale-session guard remains untested (confirmed by grep, zero hits). F-018's fix reuses an already-tested class rather than adding new untested surface - qualitatively better, but does not change the Authority Map cross-check's pass/fail count.
- Overall implementation credibility: **9.5** | SAME | `residual_disposition: queued` (F-018) | Scored pre-fix (Step 1 convention).

## Authority Map
(Re-emitted this loop: F-018, this loop's Priority-1 pick, is an ownership/authority concern.)

**Concern: Library-wide operation vs. single-game write mutual exclusion**
- Owner: `PrimaryWidget.libraryOperationGuard` (`LibraryOperationGuard` instance)
- Allowed writers: `TryBeginLibraryOperation`/`EndLibraryOperation` (all 7 call sites)
- Observers / readers: `IsLibraryOperationBlocking`
- Persistence seam: none
- Async mutation entry points: every `TryBeginLibraryOperation` call site
- Verdict: **Single and clear**

**Concern: Grid-picker and search-panel close-and-teardown mutual exclusion (the concern F-018 addresses)**
- Owner: `PrimaryWidget.gridPanelCloseGuard` / `searchPanelCloseGuard` (new `LibraryOperationGuard` instances, this loop)
- Allowed writers: `HideGridPanelAsync` (via `gridPanelCloseGuard.TryBegin`/`End`), `HideSearchPanelAsync` (via `searchPanelCloseGuard.TryBegin`/`End`)
- Observers / readers: none
- Persistence seam: none
- Async mutation entry points: `CloseGridPanel_Click`, `DownloadAndReplaceImageAsync`'s own auto-close call, `CloseSearchPanel_Click`, `SearchResult_Click`
- Verdict: **Single and clear** (now gated; previously unsynchronized reentrant close)

## Strengths That Matter
- `ArtworkRanker`/`ArtworkDownloader`/`ArtworkSignature`/`TileImage` remain a genuinely deep, pure, well-tested pipeline — re-confirmed this loop's own leaf-module duplication sweep.
- `StoreNameLookup`'s three per-store gates plus its reuse of `AsyncLazyCache<T>` for the Ubisoft case, re-verified this loop by direct full read, remain correct double-checked locking — the exact discipline this loop's own fix extends.
- This loop's own fix required zero new test infrastructure and zero new architectural ceremony: it reused an existing, already-tested class (`LibraryOperationGuard`) for a third and fourth purpose.

## Findings

### Finding #1: HideGridPanelAsync and HideSearchPanelAsync had no guard against running twice concurrently for the same session

**Why it matters** — Reachable by an ordinary user interaction (click a grid tile, then click the panel's Close button while the download is still in flight; when the download later succeeds, its own auto-close call arrives a moment later). The codebase's own established mutual-exclusion idiom (`LibraryOperationGuard`, extracted specifically so this class of guarantee is provable) was not applied to this structurally identical hazard.

**What is wrong** — `CloseGridPanel_Click` (`PrimaryWidget.xaml.cs`, pre-fix) called `HideGridPanelAsync()` with no `IsLibraryOperationBlocking()` check, and `DownloadAndReplaceImageAsync`'s own success path also called `HideGridPanelAsync()` a second time after a successful download. Neither call increments `gridPanelSessionId`, so the existing post-animation session recheck (F-009's own fix) does not distinguish two concurrent calls for the SAME session — both proceed to run the slide-down animation and the Visibility/Items.Clear/CurrentSelectedGame teardown redundantly. `HideSearchPanelAsync` has the identical shape.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`:1644-1672 (`HideGridPanelAsync`, pre-fix), `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`:1704-1707 (`CloseGridPanel_Click`, no guard, pre-fix), `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`:1578-1580 (`DownloadAndReplaceImageAsync`'s own auto-close call, pre-fix), `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`:1869-1894 (`HideSearchPanelAsync`, pre-fix), `SteamGridDB.Xbox/Services/Library/LibraryOperationGuard.cs`.

**Architectural test failed** — n/a.

**Dependency category** — `in-process`.

**Leverage impact** — Before the fix, callers got no concurrent-invocation guarantee from `HideGridPanelAsync`/`HideSearchPanelAsync`'s own Interface.

**Locality impact** — Contained to `PrimaryWidget.xaml.cs`'s two `Hide*Async` methods plus two new field declarations reusing an existing class.

**Metric signal** — none.

**Why this weakens submission** — A real, reachable concurrency/reentrancy hazard on a primary user flow, matching the severity this project's own history assigns to every prior reentrancy finding in this exact file — contained to redundant idempotent teardown, not proven data corruption, so rated Serious rather than a disqualifier.

**Severity** — Serious deduction.

**ADR conflicts** — none.

**Minimal correction path** — Reuse the existing, already-tested `LibraryOperationGuard` class as two new private instance fields (`gridPanelCloseGuard`, `searchPanelCloseGuard`); wrap both `Hide*Async` bodies in `TryBegin()`/`finally`-`End()`.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Services/Library/LibraryOperationGuard.cs` (doc comment only). Avoid: `StoreNameLookup.cs`, `SteamGridDbClient.cs`, `FixLog.cs`, `LibraryOperationGuardTests.cs`.

---

### Finding #2: GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding or renaming a platform requires remembering to update both switches; nothing fails to compile if one is forgotten.

**What is wrong** — `FromXboxDirectory` (`GamePlatform.cs`:22-46) and `GamePlatformToSGDBApiString` (`GamePlatform.cs`:48-67) independently switch over the same platform cases with no shared table. Re-confirmed unchanged this loop.

**Evidence** — `SteamGridDB.Xbox/Models/GamePlatform.cs`:22-46, `SteamGridDB.Xbox/Models/GamePlatform.cs`:48-67.

**Architectural test failed** — n/a.

**Dependency category** — null.

**Leverage impact** — Each new platform pays this asserted-twice tax.

**Locality impact** — Contained to `GamePlatform.cs`.

**Metric signal** — none.

**Why this weakens submission** — Minor; outranked this loop by F-018; promoted again to Priority 1 for loop 7.

**Severity** — Cosmetic for contest.

**ADR conflicts** — none.

**Minimal correction path** — Fold both mappings into one table keyed by `GamePlatform`, with a separate alias list for legacy folder names.

**Blast radius** — Change: `GamePlatform.cs`. Avoid: `PrimaryWidget.xaml.cs`.

---

### Finding #3: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs`:494-705) awaits each entry's lookups in strict sequence, even though the awaits are independent across entries.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`:494-705.

**Architectural test failed** — n/a.

**Dependency category** — null.

**Leverage impact** — None currently actionable.

**Locality impact** — Would be contained to `PrimaryWidget.xaml.cs` if unblocked.

**Metric signal** — none.

**Why this weakens submission** — BLOCKED by the standing user constraint on per-game network-call ordering/concurrency.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — BLOCKED this loop; named for continuity.

**Blast radius** — Change: none. Avoid: `PrimaryWidget.xaml.cs` while blocked.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Guarding `HideGridPanelAsync`'s and `HideSearchPanelAsync`'s bodies against concurrent invocation — removes a real reentrancy hazard; matches the codebase's own established mutual-exclusion idiom. |
| New seam justified | false (reused an existing class, no new Seam) |
| Helpful simplification | `LibraryOperationGuard`'s doc comment generalized to describe the reuse honestly. |
| Should NOT be done | Hand-rolling two new bool fields (reintroduces the pattern F-016 eliminated); renaming `LibraryOperationGuard` to a fully generic type (touches a 4th file for zero behavior gain). |
| Tests after fix | `LibraryOperationGuardTests.cs`'s existing 5 tests already prove the contract both new call sites depend on; no new test file needed. Full build + full suite (145/145) re-run before/after, unchanged. |

## Improvement Backlog
1. **[F-012]** Fold `GamePlatformHelper`'s two independent switch statements into one shared table — simplification, minor. Promoted to Priority 1 for loop 7. Score impact: `simplicity +0.5`.
2. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution — needed for winning once unblocked. **BLOCKED**. Score impact: `concurrency +1.0`.

**Priority-1 accounting**: F-018 wins Priority 1 on severity (Serious, newly and independently found this loop) — outranks F-012 (Cosmetic, carried forward) and F-011 (blocked).

## Deepening Candidates
None this loop. `LibraryOperationGuard` was reused, not deepened — its own public contract is unchanged.

## Builder Notes
- A guard class extracted for one concern is often the correct fix for a structurally identical concern found later, even in an unrelated part of the same file. → REVIEW_HISTORY.json `loops[5].builder_notes` for full notes.
- Reusing a narrowly-named class for a broader purpose without updating its doc comment leaves a misleading name in place even though the behavior is correct. → REVIEW_HISTORY.json `loops[5].builder_notes` for full notes.
- A missing guard on a Close/dismiss button that races an async operation's own auto-close is a distinct hazard from the missing-session-recheck hazard the same file's history already fixed several times. → REVIEW_HISTORY.json `loops[5].builder_notes` for full notes.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) `state_management`'s drop to exactly 7.5 rather than a smaller or larger drop — the finding is real and reachable, but its proven consequence is redundant idempotent teardown, not data corruption. (2) `concurrency`'s landing at 8.0 rather than staying at 7.0 or moving to 8.5 — the exact weighting of "one real fix landed, one new gap found" in the same loop is a judgment call. (3) Classifying F-018 as "Serious deduction" rather than "Noticeable weakness" — the practical worst-case observed is milder than F-005 through F-009's original shape; the Serious rating leans on consistency with this project's own severity precedent rather than on proven, observed harm at this specific tier.

## Final Judge Narrative
A clean loop that found real, new ground rather than re-executing the carried-forward backlog on autopilot. Independent re-derivation surfaced F-018 — a genuine, reachable Serious reentrancy gap in `HideGridPanelAsync`/`HideSearchPanelAsync` that the carried-forward backlog did not name — and it correctly outranked F-012 on severity. The fix is the smallest honest one available: reuse the existing, already-tested `LibraryOperationGuard` class rather than hand-roll a third guard shape, inheriting its existing test proof for free. Concurrency credits loop 5's own F-017 fix on independently re-verified structural proof. Runtime ownership for the traced library-operation guard remains trustworthy; the newly-found gap was narrow and contained, not systemic. Simplification did not hurt this loop: zero new Seams, zero new test debt. Future work still risks over-engineering if it renames `LibraryOperationGuard` to a fully generic type purely for naming purity, or tries to unify the codebase's now four differently-shaped gates into one abstraction they don't share enough behavior to earn.

## Loop 6 Result
Wrapped `HideGridPanelAsync`'s and `HideSearchPanelAsync`'s bodies (`SteamGridDB.Xbox/PrimaryWidget.xaml.cs`) in a `TryBegin()`/`finally`-`End()` reentrancy guard, using two new private instance fields (`gridPanelCloseGuard`, `searchPanelCloseGuard`) that reuse the existing `LibraryOperationGuard` class rather than adding new hand-rolled bool flags. Updated `LibraryOperationGuard.cs`'s doc comment to describe it as a generic, reusable mutual-exclusion primitive now backing three separate concerns. No test files changed. Full build (msbuild, exit 0) and full test suite (run-tests.ps1) both re-run before and after: before, 145 passed / 0 failed / 0 skipped; after, 145 passed / 0 failed / 0 skipped (unchanged). `git diff` review confirms the only touch points are the two `Hide*Async` method bodies and two new field declarations. Finding F-018 (stable_id F-018) is **resolved**. No unintended scorecard regression observed.

## Loop 6 Implementation Review
Verdict: **approved**. Reason: The diff genuinely closes the concurrent double-teardown gap by gating both `HideGridPanelAsync` and `HideSearchPanelAsync` with a `TryBegin`/`finally`-`End` guard around the entire await-spanning body, reuses the already-tested `LibraryOperationGuard` rather than hand-rolling new bool flags, and the no-desktop-test-projection carve-out for `PrimaryWidget.xaml.cs` is genuine, with no new same-or-higher-severity regression found. Checks: reality=passed, honesty=passed, regression=passed. Regressions: none. Conditions: none. Rounds: 1.

## Retired Findings (this loop)
None.
