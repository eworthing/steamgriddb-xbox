<!-- loop_cap: 15 -->
### Loop Counter
Loop 12 of 15 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
Good app, but not top-tier yet

This loop re-derived (not re-cited) loop 11's own tentative claim that the bulk-operation report/status glue (`FixLibraryAsync`/`RevertAllToDefaultAsync`/`RestoreAllChangesAsync`) is at its honest floor, and independently confirmed it: `GamesToProcess`/`DisplayName` and `OperationReport` already own "which games to visit, the progress line, the summary" exactly as `TESTING.md` claims, backed by `GameImagesTests.cs`/`OperationReportTests.cs`. No further PrimaryWidget-decomposition slice was found this loop. A fresh, wider sweep did find and fix a real, separate, subtractive finding instead: five independent hand-rolled copies of the `TryBeginLibraryOperation`/`EndLibraryOperation` guard-and-run ceremony (including one still inline inside loop 8's own `ConfirmAndRunAsync`) collapsed into one `RunUnderLibraryOperationGuardAsync` helper. `PrimaryWidget.xaml.cs` shrank 2026 -> 1999 lines. `architecture_quality` (8.5) and `data_flow` (7.5) remain the two dimensions capping this below top-tier; F-022 (the monolith) is carried forward with no new decomposition slice confidently named.

## Scorecard (1-10)

- Architecture quality: 8.5 | SAME | Fresh re-read this loop of the six co-located concerns F-022 names (UI event handling, file I/O/backup-restore invocation, artwork-selection invocation, panel/search navigation, bulk-operation loops, library-operation guarding) confirms none moved out of `PrimaryWidget.xaml.cs` - this loop's own fix (`RunUnderLibraryOperationGuardAsync`) tightened the *calling convention* for library-operation guarding but did not extract that concern into a new class, matching the established precedent that this class of in-file ceremony-consolidation (loop 7's `SlidePanelAsync`, loop 8's `ConfirmAndRunAsync`) credits `simplicity`, not `architecture_quality` - confirmed by re-checking those two loops' own scorecards (`REVIEW_HISTORY.json` loop 7/8: `architecture_quality` SAME both loops, `simplicity` UP both loops). This loop's own investigation (direct re-read of `RevertAllToDefaultAsync`/`FixLibraryAsync`/`RestoreAllChangesAsync`, `LoadGameEntriesAsync`'s remaining manifest-traversal glue, and the panel/search navigation methods) found no new decomposition-into-new-class candidate - see Finding F2 (F-022) for detail. Score holds; residual unchanged (six co-located concerns, no new slice confidently named).
- State management and runtime ownership: 10 | SAME | This loop's own fix adds zero new fields: `RunUnderLibraryOperationGuardAsync` is a stateless private method (one `Func<Task> action` parameter, no fields, no new state), confirmed by direct read of the diff. `TryBeginLibraryOperation`/`EndLibraryOperation` themselves are unchanged - still delegate to the same `libraryOperationGuard.TryBegin()`/`.End()` calls they always did. Fresh field census (unchanged from loop 11): `gridPanelFocusRestoreTarget`, `searchPanelFocusRestoreTarget`, `libraryOperationGuard`, `gridPanelSessionId`, `searchPanelSessionId`, `gridPanelCloseGuard`, `searchPanelCloseGuard`, `currentSelectedGame`, `gridPanelHeaderText`, `searchPanelHeaderText`, `GameEntries` - identical set, no drift.
- Domain modeling: 9.5 | SAME | `GameEntry.cs` unchanged this loop (confirmed via `git diff --stat`: only `PrimaryWidget.xaml.cs` changed). The parallel-fields residual (`HasSteamGridDBMatch`/`OfficialCapsuleUrl`/`SteamGridDbGameId`, lines 113-145) is untouched. Adversarial Pass re-run against the identical unchanged source: no new alternative fix beyond the two already rejected in loop 9 (readonly struct fails the two-way XAML data-binding requirement; enum+setter-methods trades permissiveness for a new forgot-to-call-the-setter failure class). Residual accepted, unchanged.
- Data flow and dependency design: 7.5 | SAME | Untouched this loop for the 12th consecutive loop of this run. `RunUnderLibraryOperationGuardAsync` (this loop's own new surface, the only plausible place a new consolidation opportunity could appear) touches no cache or static field - confirmed stateless. Stalled-Dimension Sweep (3+ SAME, applies from loop 4 on - this dimension has been SAME for all 12 loops of this run): explicit clean - re-walked the four scattered-cache sites (`StoreNameLookup`'s three caches, `SteamGridDbClient.capsuleParseNotes`, `FixLog`'s fields, `AppliedArtworkStore`'s cache); all already lock-protected (F-015/F-017) and none consolidated - no candidate passes SPT Q2 for a single-instance widget with no multi-instance/test-injection need, the same conclusion 12 consecutive independent checks have now reached.
- Framework / platform best practices: 9.5 | SAME | `App.xaml.cs` unchanged this loop. The `//TODO: Load state from previously suspended application` comment remains on the debug-only `OnLaunched` fallback path. Adversarial Pass re-run against the identical unchanged source: deleting the comment remains genuinely zero-behavioral-consequence - SPT-rejected on Q5, consistent with every prior loop's own fresh test against the same unchanged source. This loop's own fix is itself idiomatic: `Func<Task>` delegate-based guard wrapping matches the pattern `ConfirmAndRunAsync` already established, not a new idiom.
- Concurrency and runtime safety: 9.5 | SAME | This loop's own fix preserves the guard's exact acquire/release timing at all five call sites - verified by direct comparison of each site's control flow before and after (see Simplification Check). No lock, actor-isolation, or `#if`-gated boundary crossed (`risk_boundary_evidence: null` - a plain code relocation inside a single UI-thread-bound class, no new async surface). The one accepted residual (`PopulateGridSelectionPanelAsync`'s discarded `var _ = Dispatcher.RunAsync(...)`, `PrimaryWidget.xaml.cs`, unchanged this loop) re-tested against the identical unchanged source and remains SPT-rejected on Q5: `GridImagesView.UpdateLayout()`/`ContainerFromIndex(0)` do not fail on a live `GridView` and `Focus()` is null-guarded via `?.`.
- Code simplicity and clarity: 10 | SAME | This loop's own broader sweep (grepped all 14 `catch (Exception ex)` blocks and all `TryBeginLibraryOperation`/`EndLibraryOperation` call sites in the file, going beyond loop 11's narrower `GameMatchResolver.cs`-only check) found and fixed a genuine residual loop 11's narrower scope had not covered: five independent copies of the guard-and-run ceremony, now one `RunUnderLibraryOperationGuardAsync` helper (see Finding F1/F-023). A second candidate from the same sweep - unifying the ~9 outer-level `catch` blocks that report errors to a status control - was evaluated and rejected: they clean up different UI state per call site (`StatusText` alone vs. `GridPanelStatus.Text`+`GridLoadingRing.IsActive` vs. `SearchPanelStatus.Text`+`SearchLoadingRing.IsActive`), so unifying them would need a cleanup-callback parameter that trades a few duplicated lines per site for real indirection - fails SPT Q2 (smallest honest fix) and Q3 (avoids duplicate layers, in the wrong direction: papering over a genuine difference). No protocol/interface introduced by this loop's fix (plain private method, Unified Seam Policy's two-adapter requirement does not apply). Post-fix, no further nameable residual survived this loop's sweep.
- Test strategy and regression resistance: 8.5 | SAME | This loop's fix touches zero test-relevant surface: `RunUnderLibraryOperationGuardAsync` stays XAML-bound (calls `TryBeginLibraryOperation`/`EndLibraryOperation`, which touch `StatusText.Text` and `SetHeaderButtonsEnabled`) and untestable-by-design, matching the already-accepted carve-out for `ConfirmAndRunAsync`/`SlidePanelAsync` (neither has direct tests either). No test files changed this loop (`git diff --stat` confirms). Authority-Map test-surface cross-check re-confirmed unchanged from loop 11: only the `LibraryOperationGuard`-shaped concern has a direct test file (`LibraryOperationGuardTests.cs`); the grid/search picker session IDs, `currentSelectedGame`, `GameEntries`, and panel header/focus-restore state remain untested by a direct file. Full build and full test suite (190 passed/0 failed, unchanged count) re-run before and after.
- Overall implementation credibility: 10 | SAME | Self-check this loop: every doc comment this loop touched or added (`RunUnderLibraryOperationGuardAsync`'s new XML doc, `ConfirmAndRunAsync`'s updated doc, `RestoreBackup_Click`'s updated doc) was re-read against the code it describes and matches exactly - independently re-verified by the implementation reviewer below. No stale reference survived (the `libraryOperationGuard` field's own doc comment, unchanged, still accurately describes `TryBeginLibraryOperation`/`EndLibraryOperation` as the guard's UI-side wrapper).

## Authority Map
(Not re-emitted this loop: no authority/state-ownership finding was Priority 1 this loop - F-023 is a duplicate-ceremony finding, F-022 is a module-decomposition finding. See loop 8's archive for the last full Authority Map.)

## Strengths That Matter
- This loop re-derived loop 11's "bulk-operation glue is at the honest floor" claim from current source rather than citing it, and the independent re-derivation converged on the same answer with sharper evidence: `GameImagesTests.cs` and `OperationReportTests.cs` directly prove `TESTING.md`'s own claim that "which games to visit, the progress line, the summary" are already extracted and covered.
- This loop's own wider sweep (all guard-acquire call sites, not just the one location the standing directive named) found a genuine, previously-untracked duplicate-ceremony finding (F-023) that loop 8's own `ConfirmAndRunAsync` fix had left half-done - it consolidated the dialog-construction half of the ceremony but kept its own private copy of the guard-acquire/release shape rather than delegating to a shared implementation.
- This loop evaluated and correctly rejected a second, superficially similar consolidation candidate (unifying ~9 heterogeneous error-reporting `catch` blocks) once closer reading showed they clean up genuinely different UI state per site - avoiding the over-abstraction trap the Simplify Pressure Test exists to catch.

## Findings

### Finding #1: PrimaryWidget's TryBeginLibraryOperation/EndLibraryOperation guard-and-run ceremony was duplicated across five call sites, including inside the already-consolidated ConfirmAndRunAsync

**Why it matters** — This exact defect class - a call site that starts a library-wide or single-game write without a guaranteed, correctly-paired release of the reentrancy guard - has produced this project's own F-005 through F-009, F-014 and F-018 Serious-severity reentrancy bugs; five independent hand-written copies of the guard-acquire/try/finally-release shape is five places a future edit can silently drop the pairing.

**What is wrong** — `PrimaryWidget_Loaded`, `RefreshButton_Click`, `GridImage_Click` and `RestoreBackup_Click` each independently wrote the identical `if (!TryBeginLibraryOperation()) { return; } try { await X(); } finally { EndLibraryOperation(); }` shape. `ConfirmAndRunAsync` - itself created in a prior loop specifically to consolidate the `ContentDialog`-construction half of this same ceremony for its own three callers - kept its own private copy of the guard-acquire/try/finally-release shape inline rather than delegating to a shared implementation, so the pattern this project had already decided was worth consolidating once was never actually collapsed to one implementation.

**Evidence** —
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` pre-fix: `PrimaryWidget_Loaded` (170-187), `RefreshButton_Click` (684-699), `GridImage_Click` (1451-1469), `RestoreBackup_Click` (1873-1893) and `ConfirmAndRunAsync` (715-761) each independently implementing the same guard-acquire/try/finally-release shape.
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` post-fix: all five call sites now delegate to the single new `RunUnderLibraryOperationGuardAsync(Func<Task> action)` helper (lines 244-269).

**Architectural test failed** — Deletion test (delete `RunUnderLibraryOperationGuardAsync` and the identical ceremony reappears at all five call sites, exactly as it did before this loop - the helper earns its keep).

**Dependency category** — n/a (not a Coupling & Leakage finding).

**Leverage impact** — Before, a behavior change to the guard-and-run shape (e.g. a new exception type, a busy-retry) required editing five near-identical blocks; now editing `RunUnderLibraryOperationGuardAsync` once covers all five callers.

**Locality impact** — The guard-acquire/release pairing - the exact shape whose past inconsistencies produced this project's F-005 through F-009/F-014/F-018 bug class - is now expressed in one place instead of five, reducing the chance a future call site (or an edit to an existing one) silently drops the pairing.

**Metric signal** — none.

**Why this weakens submission** — Duplicate ceremony across five call sites is the exact ceremony/duplicate-layer pattern the deletion test and Meta-Rule 5 (prefer subtractive fixes) target, and the same defect class this project's own F-002 and F-003 findings already established as real and worth fixing - reduced Locality (a future guard-behavior change needs five edits, not one) and reduced regression-resistance against exactly the bug class (F-005 through F-009, F-014, F-018) this project's history shows is real.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — Extract the shared `TryBeginLibraryOperation`/`EndLibraryOperation` acquire-try-finally-release shape into one private `RunUnderLibraryOperationGuardAsync(Func<Task> action)` helper (matching the established in-file-ceremony-consolidation pattern from `SlidePanelAsync` and `ConfirmAndRunAsync`), then delegate all five call sites to it, each passing its own original body as the action delegate and preserving each site's exact prior control flow (`PrimaryWidget_Loaded`'s non-early-return shape; `RestoreBackup_Click`'s button-tag cast happening inside the guarded body; `ConfirmAndRunAsync`'s own `shouldRun(result)` short-circuit staying outside the guard). **Fixed this loop.**

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: everything else - single-file, no csproj change, no new test file needed since the helper stays XAML-bound and untestable-by-design, matching `ConfirmAndRunAsync`/`SlidePanelAsync`'s own established carve-out.

---

### Finding #2: PrimaryWidget.xaml.cs is a top-churn monolith co-locating UI event handling, network orchestration, file I/O/backup-restore, artwork-selection invocation, panel/search navigation, bulk-operation loops, and library-operation guarding in one class

**Why it matters** — Top-churn file by roughly 5x (38 edits/6mo vs 8 for the next file); `architecture_quality` was stuck at 7.5 across 19 critic passes before loops 10-11 started landing sequenced slices out of it.

**What is wrong** — `PrimaryWidget.xaml.cs` still mixes six co-located concerns after loops 10-11's extractions (`ManifestEntryImage`, `ManifestEntryIdentity`, `GameMatchResolver`): UI event handling, file I/O/backup-restore invocation, artwork-selection invocation, panel/search navigation, bulk-operation loops, and library-operation guarding. This loop investigated three candidate areas fresh and confirmed none yields a new decomposition-into-new-class slice:
1. **Bulk-operation glue** (`RevertAllToDefaultAsync`/`FixLibraryAsync`/`RestoreAllChangesAsync`, lines ~780-1010) - re-derived (not re-cited) loop 11's own finding. `GamesToProcess` delegates to the already-tested `GameImages.DistinctByImage`; `DisplayName` is a one-line pure formatter; `OperationReport` (already tested) owns every progress-line/summary construction. `TESTING.md`'s own claim - "What they *compute* - which games to visit, the progress line, the summary - is extracted and covered" - is independently confirmed true by `GameImagesTests.cs` and `OperationReportTests.cs`. Genuinely at the honest floor.
2. **`LoadGameEntriesAsync`'s remaining glue** (lines ~411-660, post loop-10/11 extractions) - the manifest-folder-traversal/JSON-parsing loop now mostly sequences calls to already-extracted, already-tested modules (`ManifestEntryImage.ResolveAsync`, `ManifestEntryIdentity.Derive`, `GameMatchResolver.ResolveAsync`); the only inline logic left is a 4-line `addedDate` timestamp parse (too small to justify extraction under SPT Q5) and the final `GameEntry`/`BitmapImage` construction, which is inherently XAML-bound.
3. **Panel/search navigation** (`LoadGridSelectionAsync`, `PopulateGridSelectionPanelAsync`, `ShowGridPanelAsync`/`HideGridPanelAsync`, `PerformGameSearchAsync`, `ShowSearchPanelAsync`/`HideSearchPanelAsync`, lines ~1150-1860) - deeply XAML-bound (panel `Visibility`, `Storyboard`/`TranslateTransform` animation, `Dispatcher`, `GridView`/`ListView` item collections, session-guard state); `SourceFor` and `GetTitleBearingGridsAsync` are already the pure/network-bound pieces already pulled to static methods where possible.

This loop's own investigation instead found and fixed a different, real, subtractive fix (Finding F1/F-023) per the standing directive's explicit permission to substitute "a strictly better slice" when the named candidate is confirmed thin.

**Evidence** —
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (2026 lines before this loop's edit, 1999 after - this loop's net reduction came from Finding F1's ceremony consolidation, not a new decomposition slice).
- Discovery churn_top20: `PrimaryWidget.xaml.cs` at 38 edits/6mo, ~5x the next file.

**Architectural test failed** — n/a (no new extraction identified this loop; carried-forward finding).

**Dependency category** — n/a.

**Leverage impact** — Unchanged from loop 11: each future `PrimaryWidget` change targeting one of the six remaining concerns still has to be read and safely edited inside the same large class.

**Locality impact** — Unchanged: the six co-located concerns remain in `PrimaryWidget.xaml.cs`; this loop's own fix (F-023) tightened one cross-cutting calling convention (library-operation guarding) without moving that concern out of the file.

**Metric signal** — churn_top20: 38 edits/6mo, ~5x the next-highest file.

**Why this weakens submission** — Same as loop 11: `architecture_quality`'s 9-anchor requires no identifiable structural improvement remains; this loop's own fresh, focused investigation of three candidate areas found none passes SPT, which is itself source-backed evidence the gap is narrowing rather than static, but the six concerns remain un-decomposed.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — Sequenced, multi-loop decomposition continues. No new slice confidently named this loop after investigating three candidates (bulk-operation glue, `LoadGameEntriesAsync`'s remaining traversal glue, panel/search navigation) and finding each either already extracted-and-tested, too small to pass SPT, or inherently XAML-bound. Loop 13 should investigate: (a) whether `PopulateGridSelectionPanelAsync`'s `GridImageItem` list-construction (ranking + `IsApplied` stamping, lines ~1330-1400) has any pure sub-decision worth extracting on its own merits, independent of the surrounding `Dispatcher`/`GridImagesView` calls; (b) whether the library-operation-guard calling convention has any further consolidation now that F-023 is resolved (a fresh look, not an assumption); (c) if neither yields a slice, treat the remaining six concerns as genuinely near the honest XAML-bound floor and say so explicitly rather than searching the same ground a fourth time.

**Blast radius** — Change: not yet named with confidence. Avoid: the already-investigated-and-thin bulk-operation glue and the already-consolidated (this loop) guard-and-run ceremony.

---

### Finding #3: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `GameMatchResolver.ResolveAsync`'s per-entry network sequence (SteamGridDB platform-ID lookup, store name-fetch, SteamGridDB name search) runs in strict sequence across entries, even though the awaits are independent across entries. Unchanged this loop (this loop's fix does not touch `GameMatchResolver.cs` or `LoadGameEntriesAsync`'s network-call code).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:426-697` (current line numbers, shifted from 411-682 by this loop's own F-023 fix, which added a net +15 lines before `LoadGameEntriesAsync` - the sequential-await shape itself is unaffected); `SteamGridDB.Xbox/Services/Library/GameMatchResolver.cs`.

**Architectural test failed** — n/a.

**Dependency category** — n/a.

**Leverage impact** — None currently actionable - see blocker.

**Locality impact** — Fully contained to `GameMatchResolver.ResolveAsync`.

**Metric signal** — none.

**Why this weakens submission** — A real, linearly-scaling latency cost, but BLOCKED: the recorded standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft) means this loop cannot land a change altering observable call ordering/concurrency/count without a product decision this loop cannot make.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — BLOCKED this loop; named for continuity per Backlog Prioritization Pass criterion 0.

**Blast radius** — Change: none (blocked). Avoid: `SteamGridDB.Xbox/Services/Library/GameMatchResolver.cs` while blocked.

## Simplification Check
- Structurally necessary: Consolidating the five independent `TryBeginLibraryOperation`/`EndLibraryOperation` guard-acquire/try/finally-release ceremonies into one `RunUnderLibraryOperationGuardAsync(Func<Task> action)` helper - passes the deletion test (delete the helper and the identical ceremony reappears in all five callers, exactly as it did before this loop).
- New seam justified: false - no protocol/interface introduced (plain private method); Unified Seam Policy's two-adapter requirement does not apply.
- Helpful simplification: `PrimaryWidget_Loaded` shrinks from 11 lines to 1 for its guard wrapping; `RefreshButton_Click` from 9 lines to 1; `GridImage_Click`'s inner guard from 9 lines to 1; `RestoreBackup_Click` from 13 lines to 6; `ConfirmAndRunAsync`'s own guard block from 7 lines to 4. Net -27 lines file-wide (74 deletions, 47 insertions, including the new 15-line helper and doc-comment updates).
- Should NOT be done: A second candidate from this loop's own sweep - unifying the file's ~9 outer-level `catch (Exception ex)` error-reporting blocks - was considered and rejected. They report to different controls per call site (`StatusText.Text` alone in `RevertAllToDefaultAsync`/`FixLibraryAsync`/`RestoreAllChangesAsync`/`RestoreBackup_Click`; `GridPanelStatus.Text`+`GridLoadingRing.IsActive` in `LoadGridSelectionAsync`/`DownloadAndReplaceImageAsync`; `SearchPanelStatus.Text`+`SearchLoadingRing.IsActive` in `PerformGameSearchAsync`), so a shared helper would need a cleanup-callback parameter - ceremony added to paper over a genuine difference, failing SPT Q2 and Q3. Also not done: touching `ConfirmAndRunAsync`'s dialog-construction logic, `TryBeginLibraryOperation`/`EndLibraryOperation` themselves, or `LibraryOperationGuard.cs` - none needed to change for this fix.
- Tests after fix: No new tests. `RunUnderLibraryOperationGuardAsync` stays XAML-bound (calls `TryBeginLibraryOperation`/`EndLibraryOperation`, which touch `StatusText.Text` and `SetHeaderButtonsEnabled` - Page-bound UI state), matching the established untested-by-design carve-out already accepted for `ConfirmAndRunAsync` (loop 8) and `SlidePanelAsync` (loop 7), neither of which added tests either. No prior tests existed on the guard-and-run ceremony itself (it lived only inline across five call sites; `LibraryOperationGuardTests.cs` already covers the underlying `TryBegin`/`End` state machine, unchanged by this loop) so nothing was deleted under Replace-don't-layer. Verification: full build (`msbuild`, exit 0) and full test suite (`run-tests.ps1`: 190 passed/0 failed, unchanged count) both re-run before and after.

## Improvement Backlog
1. **[F-022]** Continue `PrimaryWidget.xaml.cs`'s monolith decomposition - structural, needed for winning. Why it matters: standing user directive; three candidate areas investigated this loop and found not yet actionable (see Finding F2) - loop 13 needs a fresh, narrower look at `PopulateGridSelectionPanelAsync`'s list-construction and a post-F-023 re-check of the guard convention before naming a target. Score impact: architecture_quality +0.5.
2. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution - structural, needed for winning. Why it matters: BLOCKED by the standing user constraint on per-game network-call ordering/concurrency; named for continuity per Backlog Prioritization Pass criterion 0 rather than silently dropped. Score impact: concurrency +0.5.

## Deepening Candidates
None this loop. `RunUnderLibraryOperationGuardAsync` is a new private Module with real Leverage (5 callers) but no further friction is proven - it is already the simplest honest shape for its own concern (acquire, run, release), and no caller needs anything past its current `Func<Task> action` Interface.

## Builder Notes

**Pattern 1** — A prior loop's own "already consolidated" helper can itself still contain an un-collapsed copy of the exact shape it was built to generalize, if the helper only unified part of the ceremony (the dialog construction) and kept the rest (the guard acquire/release) inline rather than delegating.
**How to recognize** — Grep for the literal duplicated shape across the *whole* file, not just at the specific call sites a prior finding named - a "fixed" helper from an earlier loop is a valid grep target too.
**Smallest coding rule** — When consolidating a duplicated shape into a helper, check whether any *other* helper in the file (including ones a prior loop already introduced for a narrower slice of the same duplication) still has its own private copy of that shape, and fold it in too.
**Stack example** — C#: `ConfirmAndRunAsync` (loop 8) collapsed the `ContentDialog`-construction half of the ceremony for its 3 dialog-based callers but kept `TryBeginLibraryOperation`/`EndLibraryOperation`'s try/finally inline; this loop's `RunUnderLibraryOperationGuardAsync` now backs both `ConfirmAndRunAsync` and the four non-dialog call sites, so the guard-acquire/release shape has exactly one implementation.

**Pattern 2** — Not every candidate that "looks like duplication" should be unified: several similarly-shaped `catch` blocks touched genuinely different UI state per site, and forcing them into one delegate-based helper would trade a few duplicated lines for an indirection layer parameterizing away the very differences that make each site correct for its own region.
**How to recognize** — Multiple similarly-shaped error-handling blocks that, on closer read, touch *different* controls or fields, not the same ones.
**Smallest coding rule** — Only extract a repeated shape into a shared helper when the callers' bodies are otherwise interchangeable (same state touched); when they differ in *what* they clean up, leave them separate rather than adding a callback parameter to paper over the difference.
**Stack example** — C#: the 5 `TryBeginLibraryOperation`/`EndLibraryOperation` call sites all touch the exact same state (one class-wide `libraryOperationGuard`, `SetHeaderButtonsEnabled`) so unifying them was a clean win; the file's ~9 outer-catch error-reporting blocks each touch a *different* status control, so they were left alone.

**Pattern 3** — Re-deriving a prior loop's "already at the honest floor" claim by reading the actual source (not the prior loop's prose) can both *confirm* the original finding and simultaneously surface a *different*, better-value fix nearby that the original investigation's narrower scope had not covered.
**How to recognize** — A standing directive names one specific candidate location to investigate; a genuinely fresh read of that location is the right first step, but the same close-reading effort, cast slightly wider across the file, is often what turns up the next real finding.
**Smallest coding rule** — When a directive says "investigate location X; if you find something strictly better, take that instead," read X fully *and* keep the same close-reading discipline applied to the surrounding file, not just the one thing named.
**Stack example** — C#: reading `FixLibraryAsync`/`RevertAllToDefaultAsync`/`RestoreAllChangesAsync` line-by-line confirmed `GamesToProcess`/`DisplayName`/`OperationReport` already cover "which games, the progress line, the summary"; the same reading pass, extended to the surrounding guard-and-run call sites, is what surfaced F-023.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) crediting this loop's fix entirely to `simplicity` and leaving `architecture_quality` at SAME - the new `RunUnderLibraryOperationGuardAsync` is, by the rubric's own scale-agnostic Module definition, a real Module with five callers, and a stricter critic could argue it deserves a small `architecture_quality` nudge too, not just a `simplicity` credit; the precedent (loops 7-8 crediting only `simplicity` for the same shape of fix) is followed here but is itself a judgment call, not a formula. (2) F-022's severity staying "Noticeable weakness" for a third consecutive loop despite the user's own sustained, explicit framing as this run's Priority 1 - the same tension loop 11 flagged and left unresolved. (3) `simplicity` reading as "10, SAME" rather than acknowledging that F-023's existence is itself proof the codebase was not actually flawless on this dimension going into this loop - a stricter bookkeeping could argue loop 11's "10" should be read as having been premature (an un-found residual existed in source even though no critic had located it yet), rather than the number simply holding unchanged across loops 11 and 12.

## Final Judge Narrative
Place, not win. This loop's most defensible move was negative: it independently re-derived, rather than cited, the standing directive's own named candidate (bulk-operation glue) and confirmed - with sharper evidence than loop 11 had (direct citation of `GameImagesTests.cs`/`OperationReportTests.cs`) - that it is genuinely at the honest floor `TESTING.md` describes. Runtime ownership stayed trustworthy: this loop's fix touches no state, changes no field, and the reviewer independently confirmed the guard's acquire/release timing is byte-identical at all five sites. Concurrency stayed trustworthy for the same reason - no lock, isolation, or await-ordering change. Tests do not reduce regression risk further this loop (none added; none needed, matching established carve-out), so `test_strategy` holds rather than moves. The risk to watch for loop 13: the standing directive's Priority 1 (F-022) is still open with no confidently-named next slice after three consecutive loops of investigation (10, 11, and this one) turning up progressively thinner candidates - loop 13 should not assume the decomposition sequence has an obvious next step just because loops 10-11 each found one, and should be willing to say plainly if the remaining six concerns really are at the honest floor rather than searching the same ground a fourth time.

## Loop 12 Result
Extracted the shared `TryBeginLibraryOperation`/`EndLibraryOperation` acquire-try-finally-release ceremony, independently duplicated at five call sites in `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (`PrimaryWidget_Loaded`, `RefreshButton_Click`, `GridImage_Click`, `RestoreBackup_Click`, and `ConfirmAndRunAsync`'s own inline copy), into one new private `RunUnderLibraryOperationGuardAsync(Func<Task> action)` helper; all five sites now delegate to it, each preserving its exact prior control flow. `PrimaryWidget.xaml.cs` shrank from 2026 to 1999 lines (net -27; 74 deletions, 47 insertions). Full build (`msbuild SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never`, exit 0) and full test suite (`run-tests.ps1`: 190 passed/0 failed, unchanged from before this loop - no test-relevant surface touched) both re-run before and after. Finding F1 (F-023) is **resolved**. Finding F2 (F-022, the monolith) is **carried_forward** - this loop's own fresh investigation of three candidate areas found no new decomposition-into-new-class slice, an honest negative result rather than a stall. No unintended scorecard regression: `state_management`, `concurrency`, and `credibility` were each independently re-checked against the diff and hold.

## Loop 12 Implementation Review
**Verdict: approved** (all three checks passed, first pass, no re-spawn needed). Reason: "The five duplicated TryBeginLibraryOperation/EndLibraryOperation ceremonies are now expressed in exactly one place (RunUnderLibraryOperationGuardAsync), and each of the five call sites' exact prior control flow (including PrimaryWidget_Loaded's unconditional post-guard Focus, RestoreBackup_Click's guard-before-cast ordering, and ConfirmAndRunAsync's shouldRun short-circuit) is preserved with no touch to any network-calling code." No regressions flagged; no conditions.
