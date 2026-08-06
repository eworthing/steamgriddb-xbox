<!-- loop_cap: 15 -->
### Loop Counter
Loop 15 of 15 (cap)

### System Flag
[STATE: HALT_LOOP_CAP]

---

## Contest Verdict
Good app, but not top-tier yet

This is the run's final loop. It landed the one clean, ready-to-land item left on the board - routing `RestoreBackupAsync`/`RestoreBackupCoreAsync`'s name-fallback computation through the existing `DisplayName` helper (F-024) - and, per the standing directive's instruction to re-derive rather than re-cite, spent the rest of the loop investigating both of loop 14's named F-022 candidates fresh from current source. Neither passes the Simplify Pressure Test as a same-loop slice: both are genuinely at a floor this loop can name precisely, not just assert. `simplicity` moves to a justified 10; `architecture_quality` holds at 9.0 with the remaining monolith concern now given a concrete, named remedy (a domain/view-model split) that is honestly out of one loop's scope rather than a vague line-count estimate.

## Scorecard (1-10)

- Architecture quality: 9.0 | SAME | Both of loop 14's named F-022 candidates were investigated fresh against current source this loop and neither yields an extractable Module: (a) `ReplaceImageCoreAsync` (PrimaryWidget.xaml.cs:1105-1137) and `RestoreBackupCoreAsync` (:1884-1929) are, on this loop's own line-by-line re-read, StorageFolder/BitmapImage/UpdateSharedEntriesAsync I/O sequencing with a single status-text ternary as their only "decision" - re-confirms loop 14's finding rather than merely repeating it; (b) `FixLibraryAsync`'s grid/portrait/icon fallback chain (:920-993) was investigated for the first time this loop: the three branches are shape-different (the grid branch ranks and probes up to 5 candidates via `DownloadBestTileFillingImageAsync`'s corner-fill check, the icon branch takes the single top-ranked candidate directly via `DownloadAndReplaceImageCoreAsync`, and `TryFixFromPortraitArtAsync` is already its own extracted method) and each branch's decision is gated on the actual network result of the previous branch, not a pre-computable priority list - forcing them into one helper would fail SPT Q3 exactly as loop 14 rejected doing the same to `ShowGridPanelAsync`/`ShowSearchPanelAsync`. Residual (queued, F-022): `PrimaryWidget.xaml.cs` (1987 lines) still co-locates network-orchestration control flow, file I/O/backup-restore invocation, and bulk-operation loops with UI event handling, because `GameEntry` itself carries `BitmapImage`/`Visibility` members that require `Windows.UI.Xaml` (TESTING.md's own documented reason) - the concrete remedy is a domain/view-model split (a plain `GameRecord` type distinct from the XAML-bound `GameEntry`), which is real but is a multi-loop redesign, not a same-loop slice.
- State management and runtime ownership: 10 | SAME | This loop's fix removes one local variable (`RestoreBackupAsync`'s now-dead `imageFileName`, previously computed only to feed the replaced ternary) and touches zero fields. Field census unchanged from loop 14.
- Domain modeling: 9.5 | SAME | `GameEntry.cs` re-read in full this loop (`git diff --stat` confirms only `PrimaryWidget.xaml.cs` changed); the accepted residual (`HasSteamGridDBMatch`/`OfficialCapsuleUrl`/`SteamGridDbGameId` admitting a combination `LoadGameEntriesAsync` never constructs) is unchanged and re-confirmed.
- Data flow and dependency design: 7.5 | SAME | Re-derived fresh this loop by direct read of `AsyncLazyCache.cs`, `StoreNameLookup.cs` (in full), `AppliedArtworkStore.cs`, `EpicLibrary.cs`, `FixLog.cs` and `SteamGridDbClient.cs`: five classes still hold deliberate, documented, process-lifetime static ambient state (`StoreNameLookup`'s four caches, `EpicLibrary.nameCache`, `AppliedArtworkStore.appliedCache`, `FixLog`'s static log buffer, `SteamGridDbClient.capsuleParseNotes`), exceeding the 9-anchor's "one or two documented" allowance. Not a valid backlog item: converting to instance-based DI fails Simplify Pressure Test Q2 (wide blast radius across 5 classes) and Q5 (no measurable product improvement for a single-instance desktop-widget process) - unchanged reasoning from loop 14, re-verified against unchanged source.
- Framework / platform best practices: 9.5 | SAME | `App.xaml.cs` unchanged; its stale `// TODO: Load state from previously suspended application` comment (line 120) re-confirmed present, SPT-rejected on Q5 (zero behavioral consequence) against unchanged source.
- Concurrency and runtime safety: 9.5 | SAME | `PopulateGridSelectionPanelAsync`'s discarded `var _ = Dispatcher.RunAsync(...)` fire-and-forget UI-focus call (PrimaryWidget.xaml.cs:1405, unchanged this loop, one line lower than loop 14's citation due to this loop's own -1 net line change above it) re-confirmed present and re-tested against unchanged source: still SPT-rejected on Q5. This loop's own fix touches no await ordering, no lock, no field.
- Code simplicity and clarity: 10 | UP | This loop fixed the one named residual (F-024): `RestoreBackupAsync` (PrimaryWidget.xaml.cs:1860-1862) and `RestoreBackupCoreAsync` (:1886-1887) now call `DisplayName(game)` instead of each independently re-deriving its exact fallback ternary; `RestoreBackupAsync`'s now-unused `imageFileName` local was deleted rather than left dead. `git diff --stat`: 2 insertions, 3 deletions, net -1 line (1988 -> 1987). This loop also performed the leaf-module duplication sweep (mandatory before a score this high) as a full fresh read of `PrimaryWidget.xaml.cs` (all 1987 lines, four passes) plus `StoreNameLookup.cs`/`AsyncLazyCache.cs` in full: Reuse - no re-implementation of an existing Services/ helper found; Simplification - no further duplicated ternary, dead code, or guard-clause-avoidable nesting found; Altitude - no special case layered on shared infrastructure at the wrong depth found; Efficiency - the one D2 (sequential independent awaits) finding is F-011, already tracked separately. One borderline candidate was considered and set aside: `StoreNameLookup.GetGogGameNameAsync`/`GetEpicGameNameAsync` share a "fetch URL, parse JSON, extract via JsonRead, catch-log-return-null" shape, but their JSON extraction paths differ in shape (nested `_embedded.product.title` vs flat `title`) enough that a shared helper needs an extractor-delegate parameter - the added indirection roughly matches the ~10 total lines it would save, failing SPT Q2. No further source-backed residual found this loop; per G6, the score moves to 10.
- Test strategy and regression resistance: 8.5 | SAME | This loop's fix sits entirely inside `RestoreBackupAsync`/`RestoreBackupCoreAsync`, XAML-bound and untestable-by-design matching `DisplayName`'s own established carve-out (both already untested for the same reason) - no new tests owed. 206 tests unchanged. The Authority-Map gap held at 8.5 across loops 11-15 (session IDs, `currentSelectedGame`, `GameEntries`, panel header/focus-restore state still have no direct test file) is unaffected by this loop's change.
- Overall implementation credibility: 10 | SAME | `RestoreBackupAsync`'s and `RestoreBackupCoreAsync`'s doc comments ("Restore image from backup file" / "Restores the original Xbox app artwork from the backup file...") remain accurate after the fix - neither describes the fallback-name computation, so neither needed updating. `DisplayName`'s own doc comment ("The name to show for a game in progress and status lines...") was already written generically enough to cover a third caller without change. No stale reference found on independent re-read.

## Authority Map
(Not re-emitted this loop: no authority/state-ownership finding was Priority 1 this loop - F-024 is a simplification finding, F-022 is a module-decomposition finding. See loop 8's archive for the last full Authority Map.)

## Strengths That Matter
- This loop's investigation of `FixLibraryAsync`'s grid/portrait/icon fallback chain - the one candidate from loop 14's own list that no prior loop had actually looked at - is a genuinely fresh Critic pass, not a re-citation: it reads the three branches' actual shapes (different download primitives, different candidate-count handling, one already its own extracted method) and reaches a source-backed SPT rejection rather than assuming a slice exists because the file is still long.
- The F-024 fix is smaller than it looked from loop 14's own description: `RestoreBackupAsync`'s `imageFileName` local was only ever used to feed the replaced ternary, so calling `DisplayName(game)` let a whole now-dead local be deleted too, not just the ternary - a small honest bonus a same-diff scan of the call sites turned up before committing.
- `architecture_quality`'s residual is stated this loop with a specific, testable remedy (a `GameRecord`/view-model split distinct from the XAML-bound `GameEntry`) rather than the run's earlier line-count-floor framing (loop 10's now-superseded "~1,600-1,800 line floor" prediction) - naming the actual blocking coupling (GameEntry's `BitmapImage`/`Visibility` members) is falsifiable in a way a bare line count never was.

## Findings

### Finding #1: PrimaryWidget.xaml.cs is a top-churn monolith co-locating UI event handling, network orchestration, file I/O/backup-restore, artwork-selection invocation, panel/search navigation, bulk-operation loops, and library-operation guarding in one class

**Why it matters** — Top-churn file by roughly 5x (38 edits/6mo vs 8 for the next file) before this run started; `architecture_quality` was stuck at 7.5 across 19 critic passes before loops 10-14 started landing sequenced slices out of it, moving it to 9.0.

**What is wrong** — `PrimaryWidget.xaml.cs` (1987 lines) still mixes UI event handling, file I/O/backup-restore invocation, artwork-selection invocation, panel/search navigation, bulk-operation loops, and library-operation guarding after loops 10-14's extractions. This loop investigated both of loop 14's named candidates fresh and confirmed neither is a same-loop slice: (a) `ReplaceImageCoreAsync`/`RestoreBackupCoreAsync`'s remaining bodies are StorageFolder/BitmapImage/UpdateSharedEntriesAsync I/O sequencing, not separable decision logic; (b) `FixLibraryAsync`'s grid-then-portrait-then-icon fallback chain is three shape-different branches whose order is dictated by which network call actually returned results, not a pre-computable priority list, and forcing the grid/icon branches into one helper would duplicate a layer that isn't actually duplicated (different download primitives: `DownloadBestTileFillingImageAsync` probes up to 5 ranked candidates for corner-fill; the icon branch downloads exactly the top-ranked one).

**Evidence** —
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1105-1137` (`ReplaceImageCoreAsync`) and `:1884-1929` (`RestoreBackupCoreAsync`): each is StorageFolder/BitmapImage I/O plus one status-text ternary; no further decision logic to extract.
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:920-993` (`FixLibraryAsync`'s grid/portrait/icon fallback) and `:1150-1172` (`TryFixFromPortraitArtAsync`, already extracted): three sequentially-gated, shape-different branches.
- `TESTING.md:49-56`: documents that `GameEntry` exposing `Visibility`/`BitmapImage` is why the bulk-operation loops and their UI-touching methods stay in the widget - the same documented reason blocks both candidates investigated this loop.
- Full build (msbuild, exit 0) and full test suite (`run-tests.ps1`: 206 passed/0 failed, unchanged) both re-run this loop.

**Architectural test failed** — n/a (no extraction is being proposed this loop; both candidates were investigated and rejected on Simplify Pressure Test grounds, not on an architectural test failure).

**Dependency category** — n/a.

**Leverage impact** — None from this loop's investigation directly; ruling out two candidates narrows what a future loop needs to re-examine.

**Locality impact** — Unaffected this loop; the remaining monolith concerns are unchanged in shape and count from loop 14's assessment.

**Metric signal** — churn_top20: 38 edits/6mo, ~5x the next-highest file (unchanged this loop; Discovery not re-run).

**Why this weakens submission** — `architecture_quality`'s own 9-anchor requires that a senior reviewer cannot identify a structural improvement that preserves behavior and improves Leverage or Locality within the scope of one loop; this loop confirmed neither of loop 14's two named candidates qualifies, but the underlying co-location is real and its remedy - separating `GameEntry`'s XAML-bound presentation members from a plain domain record - is a genuine, nameable redesign this run's loop budget cannot execute safely in one pass, especially given the recorded user constraint against changing observable network-call behavior.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — Not a same-loop fix. The concrete next step, if the user wants to continue this decomposition beyond this run's cap: introduce a plain `GameRecord` (or similarly-named) type holding the non-UI fields (`Name`, `ExternalPlatformId`, `ImageFileName`, `ImageFilePath`, `Platform`, `AddedDate`, `HasBackup`, `HasSteamGridDBMatch`, `OfficialCapsuleUrl`, `SteamGridDbGameId`) with no `BitmapImage`/`Visibility` members; move `LoadGameEntriesAsync`'s manifest-parsing and `FixLibraryAsync`'s/`ReplaceImageCoreAsync`'s/`RestoreBackupCoreAsync`'s decision logic to operate on it; keep a thin `GameEntry : GameRecord` (or a wrapping adapter) in the widget solely for the `BitmapImage`/`Visibility` presentation concerns and `INotifyPropertyChanged`. This is a multi-loop program (introduce the type, migrate call sites one bulk-operation method at a time, keep `GameEntries`' binding working throughout) with real regression risk against the recorded network-call-ordering constraint if rushed - explicitly not attempted this loop.

**Blast radius** — Change: none this loop. Avoid: everything - the redesign above is out of scope for a single loop and is not started.

---

### Finding #2: RestoreBackupAsync and RestoreBackupCoreAsync each independently re-derive DisplayName's exact name-fallback rule instead of calling it

**Why it matters** — `DisplayName(GameEntry)` already owns the "game.Name unless it's Unknown, then fall back to the image file name" rule; two other methods re-implementing the identical ternary is exactly the kind of independent-copy drift this codebase has already paid for once (`SlidePanelAsync`'s own doc comment: "the two Show/Hide pairs had already drifted apart once").

**What is wrong** — Before this loop's fix, `RestoreBackupAsync` and `RestoreBackupCoreAsync` both computed `string backupGameName = game.Name != unknownName ? game.Name : imageFileName;` independently of each other and of `DisplayName(GameEntry)`, which already returns the identical value.

**Evidence** —
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:421-424` — `DisplayName(GameEntry game)` owns this exact fallback rule (unchanged this loop).
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1860-1862` (post-fix) — `RestoreBackupAsync` now calls `DisplayName(game)`; its now-dead `imageFileName` local (used only for the old ternary) was deleted.
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1886-1887` (post-fix) — `RestoreBackupCoreAsync` now calls `DisplayName(game)`; its `imageFileName` local is retained because it is still used for `ArtworkFiles.RestoreOriginalAsync`, `GetFileAsync` and `UpdateSharedEntriesAsync`.
- `git diff --stat SteamGridDB.Xbox/PrimaryWidget.xaml.cs`: 2 insertions, 3 deletions; net -1 line (1988 -> 1987).
- Full build (msbuild, exit 0) and full test suite (`run-tests.ps1`: 206 passed/0 failed, unchanged) both re-run before and after this loop's own diff.

**Architectural test failed** — n/a.

**Dependency category** — n/a.

**Leverage impact** — `DisplayName`'s fallback rule now has exactly one owner; a future change to the rule needs one edit, not three.

**Locality impact** — Fully contained to `RestoreBackupAsync` and `RestoreBackupCoreAsync`.

**Metric signal** — none.

**Why this weakens submission** — Resolved this loop; no longer weakens the submission.

**Severity** — Noticeable weakness (as carried from loop 14; now resolved).

**ADR conflicts** — none.

**Minimal correction path** — Done: both call sites now call `DisplayName(game)`.

**Blast radius** — Changed: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (`RestoreBackupAsync`, `RestoreBackupCoreAsync` only). Avoided: everything else, confirmed by `git diff --stat` showing exactly one file touched.

---

### Finding #3: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `GameMatchResolver.ResolveAsync`'s per-entry network sequence runs in strict sequence across entries, even though the awaits are independent across entries. Unchanged this loop (this loop's own edit is entirely inside `RestoreBackupAsync`/`RestoreBackupCoreAsync`, well below `LoadGameEntriesAsync` in the file).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:426-697` (unchanged this loop, re-verified by direct read); `SteamGridDB.Xbox/Services/Library/GameMatchResolver.cs` (unchanged this loop).

**Architectural test failed** — n/a.

**Dependency category** — n/a.

**Leverage impact** — None currently actionable - see blocker.

**Locality impact** — Fully contained to `GameMatchResolver.ResolveAsync`.

**Metric signal** — none.

**Why this weakens submission** — A real, linearly-scaling latency cost, but BLOCKED: the recorded standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft) means this loop cannot land a change altering observable call ordering/concurrency/count without a product decision this loop cannot make. This is the run's final loop, so the blocker is being surfaced plainly rather than carried forward again.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — BLOCKED. Requires the user's explicit decision to accept a change in observable per-game network-call ordering/concurrency before any loop (this run or a future one) can act on it.

**Blast radius** — Change: none (blocked). Avoid: `SteamGridDB.Xbox/Services/Library/GameMatchResolver.cs` while blocked.

## Simplification Check
- Structurally necessary: Routing `RestoreBackupAsync`'s and `RestoreBackupCoreAsync`'s name-fallback computation through the existing `DisplayName` helper - removes a duplicated formula down to its one owner.
- New seam justified: false - no protocol/interface introduced (existing private method reused as-is).
- Helpful simplification: `RestoreBackupAsync` lost a now-dead local variable in addition to the duplicated ternary. `PrimaryWidget.xaml.cs`: 1988 -> 1987 lines (net -1; 2 insertions, 3 deletions).
- Should NOT be done: Extracting `FixLibraryAsync`'s grid/icon branches into one shared "rank, download, apply, count" helper - investigated and rejected this loop: the two branches use different download primitives (`DownloadBestTileFillingImageAsync` probes up to 5 candidates for corner-fill; the icon branch downloads exactly the top-ranked candidate) and forcing them into one shape would fail SPT Q3 (duplicate layer for something that isn't actually duplicated), the same reasoning loop 14 applied to `ShowGridPanelAsync`/`ShowSearchPanelAsync`. Also not done: consolidating `StoreNameLookup.GetGogGameNameAsync`/`GetEpicGameNameAsync`'s fetch-and-parse bodies - considered, set aside as failing SPT Q2 (an extractor-delegate parameter would add roughly the indirection it saves).
- Tests after fix: No new tests. `RestoreBackupAsync`/`RestoreBackupCoreAsync` stay XAML-bound and untestable-by-design, matching `DisplayName`'s own established carve-out. Verification: full build (msbuild, exit 0) and full test suite (`run-tests.ps1`: 206 passed/0 failed, unchanged count) both re-run before and after this loop's own diff, plus this loop's own independent review.

## Improvement Backlog
1. **[F-022]** Continue `PrimaryWidget.xaml.cs`'s monolith decomposition via a `GameRecord`/view-model split - structural, needed for winning. Why it matters: standing user directive, Priority 1 for this run; this loop's own investigation confirmed both of loop 14's named candidates are not same-loop slices and named the concrete remedy (separating `GameEntry`'s XAML-bound presentation members from a plain domain record) as the next actionable step, at multi-loop scope. Score impact: architecture_quality +0.5.
2. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution - structural, needed for winning. Why it matters: BLOCKED by the standing user constraint on per-game network-call ordering/concurrency; this is the run's final loop, so the blocker is named plainly rather than carried forward silently - the decision to accept an observable network-call-ordering change belongs to the user, not this loop. Score impact: concurrency +0.5.

## Deepening Candidates
None this loop. The one candidate investigated (`FixLibraryAsync`'s fallback chain) failed the friction-proof requirement: the three branches are shape-different, not a shallow Interface hiding real Depth.

## Builder Notes
- Pattern 1: When a prior loop names two candidates for "what to investigate next" and both turn out to fail the Simplify Pressure Test, the honest write-up is not "no progress" - it is "both candidates ruled out, here is the source-backed reason for each," which is itself real information for whoever picks up the backlog next.
- Pattern 2: A duplication fix can turn up a second, smaller bonus once you actually look at both call sites side by side - `RestoreBackupAsync`'s dead local variable was only visible by checking whether `imageFileName` had any other use once the ternary it fed was replaced.
- Pattern 3: A "structural ceiling" claim is only as good as the remedy it names. This run's earlier "~1,600-1,800 line floor" was a line-count guess; this loop replaced it with a specific coupling (`GameEntry`'s `BitmapImage`/`Visibility` members forcing UI-bound types into every method that touches them) and a specific remedy (a domain/view-model split) - falsifiable by a future loop in a way the line count never was.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) `simplicity`'s move to a clean 10 - the leaf-module duplication sweep covered `PrimaryWidget.xaml.cs` in full and `StoreNameLookup.cs`/`AsyncLazyCache.cs`, but did not extend to every Services/ file (`ArtworkRanker.cs`, `ArtworkDownloader.cs`, `TileImage.cs`, `ArtworkSignature.cs` were not re-read this loop) - a stricter critic doing that full sweep might find something this loop missed, though none of those files have ever been the source of a simplicity finding in this run's 15-loop history. (2) `architecture_quality` holding at 9.0 rather than moving to 9.5 as an accepted residual - this loop chose to keep F-022 as a genuine backlog item (queued) rather than promote it to an accepted residual, on the reasoning that a real remedy (the domain/view-model split) exists and just needs more loop budget than this run has left; a stricter critic could argue that since no candidate passes SPT within a single loop's blast-radius discipline, the honest score is 9.5-accepted with the redesign as a "won't fix within this run's terms" residual, not a live backlog item that will sit unexecuted. (3) `FixLibraryAsync`'s fallback-chain rejection (candidate b) - the "shape-different branches" argument is sound for the grid-vs-icon comparison, but this loop did not separately stress-test whether the *counting* pattern (`if (downloaded) successCount++; else errorCount++;`, appearing near-identically in both branches) is itself worth a two-line extraction; it was judged too small to matter, but that judgment call could go either way.

## Final Judge Narrative
Place, not win. This is the run's final loop, and it did two things honestly rather than one thing optimistically: it landed the one real, ready-to-land item on the board (F-024, `RestoreBackupAsync`/`RestoreBackupCoreAsync` now calling the `DisplayName` helper instead of re-deriving its formula, with a small bonus dead-code deletion found along the way), and it spent real investigation time on both of loop 14's named F-022 candidates rather than assuming either was a slice - finding that neither is, for source-backed, specific reasons rather than a shrug. `architecture_quality` holds at 9.0 because the honest remedy for the remaining monolith concern is a domain/view-model split, named concretely this loop (separating `GameEntry`'s `BitmapImage`/`Visibility` members from a plain domain record) rather than restated as a vague line-count estimate - a genuine improvement over this run's earlier "~1,600-1,800 line floor" framing, and a redesign this run's remaining budget cannot safely execute in one pass, particularly given the standing constraint against changing observable per-game network-call behavior. `simplicity` reaches a justified 10 after a fresh full-file sweep found nothing left to fix beyond the one item just closed. Runtime ownership and concurrency both stay trustworthy: this loop's own diff touches zero fields, zero locks, zero await ordering. Tests do not reduce regression risk for this loop's own diff (it sits inside the same XAML-bound carve-out as everything else in this file), but the 206-test suite and the independent implementation review both re-confirm nothing else moved. The two items closing this run in the backlog are named plainly rather than glossed over: F-022 is real, multi-loop, and actionable with more budget; F-011 is real but genuinely blocked on a decision only the user can make.

## Loop 15 Result
Routed `RestoreBackupAsync` and `RestoreBackupCoreAsync` (PrimaryWidget.xaml.cs) through the existing `DisplayName(GameEntry)` helper instead of each independently re-deriving its exact name-fallback ternary (`game.Name != unknownName ? game.Name : imageFileName`); `RestoreBackupAsync`'s now-unused `imageFileName` local was deleted as part of the same fix. `PrimaryWidget.xaml.cs` line count: 1988 -> 1987 (net -1), verified via `(Get-Content SteamGridDB.Xbox/PrimaryWidget.xaml.cs).Count`. Full build (`msbuild SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never`, exit 0) and full test suite (`run-tests.ps1`: 206 passed/0 failed, unchanged) both re-run before and after. Finding F2 (stable_id F-024) is **resolved** - both call sites now call `DisplayName(game)`, verified identical to the prior computed value by direct comparison of `DisplayName`'s own formula against the replaced ternaries. Finding F1 (stable_id F-022) is **carried_forward** - this loop's own investigation of both of loop 14's named candidates found neither to be a same-loop slice, and named the concrete multi-loop remedy instead. No unintended scorecard regression: `state_management`, `domain_modeling`, `data_flow`, `framework_idioms`, and `concurrency` were each independently re-checked against the diff and hold; `test_strategy` and `credibility` likewise hold.

## Loop 15 Implementation Review
**Verdict: approved** (all three checks passed, first pass, no re-spawn needed). Reason: "Both call sites now call DisplayName(game), which computes the identical formula to the removed inline ternaries, RestoreBackupAsync's now-dead imageFileName local was correctly deleted while RestoreBackupCoreAsync's was correctly kept for its other three use sites, and the diff is a minimal same-file, same-method call-site substitution with no new smells introduced." No regressions flagged; no conditions.

## Run Close-Out (loop 15, cap reached)
This run began at loop 1 with `PrimaryWidget.xaml.cs` at 2,010 lines (pre-run baseline), peaked at 2,125 lines during intermediate work, and closes this loop at **1,987 lines** - net -23 from the pre-run baseline, -138 from the run's own peak. Tests grew from 167 to 206 across the run (all in `Services/`-linked modules; `PrimaryWidget.xaml.cs` itself has no desktop test projection per TESTING.md). `architecture_quality` moved 7.5 -> 9.0 (stuck at 7.5 across 19 critic passes before loop 10's first successful extraction). Re-derived assessment of what remains: two candidates named by loop 14 (`ReplaceImageCoreAsync`/`RestoreBackupCoreAsync`'s residual I/O sequencing; `FixLibraryAsync`'s grid/portrait/icon fallback chain) were both investigated fresh this loop and neither is a same-loop slice - the first is StorageFolder/BitmapImage I/O orchestration with no separable decision logic, the second is three shape-different, sequentially-gated branches that would fail SPT Q3 if forced into one helper. The genuine remaining floor is `GameEntry` itself carrying `BitmapImage`/`Visibility` presentation members (TESTING.md's own documented reason PrimaryWidget.xaml.cs has no desktop test projection), which pulls every method that touches a `GameEntry` back into the XAML-bound app container. The concrete redesign that would lift this ceiling: a plain `GameRecord` domain type without UI members, with `LoadGameEntriesAsync`/`FixLibraryAsync`/`ReplaceImageCoreAsync`/`RestoreBackupCoreAsync`'s decision logic operating on it and a thin `GameEntry` wrapper left in the widget solely for presentation - a multi-loop program, not a same-loop fix, and one this run's remaining budget cannot execute without risking the recorded network-call-ordering constraint if rushed.
