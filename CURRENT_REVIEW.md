<!-- loop_cap: 15 -->
### Loop Counter
Loop 14 of 15 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
Good app, but not top-tier yet

This loop closed the exact duplication loop 13's own fresh sweep named and left unconsolidated: `HideGridPanelAsync` and `HideSearchPanelAsync`'s byte-near-identical panel-lifecycle ceremony is now one shared private `HidePanelAsync`. `PrimaryWidget.xaml.cs` shrank 1992 -> 1988 lines. The same investigation surfaced a small, real, previously-unnamed `simplicity` residual (F-024), correcting that dimension from an unexamined 10 down to an honestly-residualed 9.5. `data_flow` (7.5) was re-derived fully fresh per the standing directive rather than re-cited, and holds with materially stronger evidence.

## Scorecard (1-10)

- Architecture quality: 9.0 | SAME | This loop's fix (`HidePanelAsync`, `PrimaryWidget.xaml.cs:1553-1595`) is a private-method dedup inside `PrimaryWidget` itself, not a new tested Module in `Services/` - matching the `RunUnderLibraryOperationGuardAsync` precedent (loop 12), which was credited to `simplicity`, not `architecture_quality`, for the identical reason (no new Module boundary, no new Interface). Module graph unchanged this loop. Residual (queued, F-022): `PrimaryWidget.xaml.cs` (1988 lines) still co-locates network orchestration, file I/O/backup-restore invocation, and bulk-operation loops alongside UI event handling and panel navigation.
- State management and runtime ownership: 10 | SAME | `HidePanelAsync` introduces zero new fields; it operates on the existing `gridPanelSessionId`/`searchPanelSessionId`/`gridPanelCloseGuard`/`searchPanelCloseGuard`/`gridPanelFocusRestoreTarget`/`searchPanelFocusRestoreTarget` fields via delegate parameters, each still owned and written exactly where it was before. Field census unchanged from loop 13.
- Domain modeling: 9.5 | SAME | `GameEntry.cs` unchanged this loop (`git diff --stat` confirms only `PrimaryWidget.xaml.cs` changed). Residual accepted, unchanged from loop 12's Adversarial Pass.
- Data flow and dependency design: 7.5 | SAME | Re-derived fresh this loop per the standing directive rather than re-citing prior loops' "four scattered-cache sites." Current source shows FIVE classes holding deliberate, documented, process-lifetime static ambient state: `StoreNameLookup.cs:34-50` (four caches, each gated by its own `SemaphoreSlim`), `EpicLibrary.cs:41-44`, `AppliedArtworkStore.cs:30-35,54` (reassigned, not just read), `FixLog.cs:28-34`, and `SteamGridDbClient.cs` (its own line 60-61 comment confirms a matching cache). That exceeds the 9-anchor's "one or two documented ambient-context dependencies" allowance, so the anchor is genuinely not met. Not a valid backlog item: `StoreNameLookup.cs`'s own doc comment (line 21-23) states the design intent explicitly for a single-instance desktop-widget process with no multi-tenancy need; converting to instance-based DI would fail Simplify Pressure Test Q2 (wide blast radius across 5 classes) and Q5 (no measurable product improvement).
- Framework / platform best practices: 9.5 | SAME | `App.xaml.cs` unchanged. `HidePanelAsync`'s delegate-based generalization is itself idiomatic C#, matching the `RunUnderLibraryOperationGuardAsync`/`ConfirmAndRunAsync`/`SlidePanelAsync` precedent exactly. Residual re-confirmed SPT-rejected on Q5 against unchanged source.
- Concurrency and runtime safety: 9.5 | SAME | `HidePanelAsync`'s session-capture/await/recheck sequence is a mechanical parameterization of the exact prior guard logic, verified line-by-line against the pre-diff code: the live field is read via closure both before and after the animation's await, exactly as the original direct field reads did. No new lock/actor-isolation/`#if` boundary crossed. `risk_boundary_evidence: null`. Residual (discarded `Dispatcher.RunAsync` fire-and-forget) re-tested against unchanged source, still SPT-rejected on Q5.
- Code simplicity and clarity: 9.5 | DOWN | This loop both fixed one duplication (`HideGridPanelAsync`/`HideSearchPanelAsync`, now `HidePanelAsync`) and, in the course of the deeper read that finding required, surfaced a further one: `RestoreBackupAsync` (line 1863) and `RestoreBackupCoreAsync` (line 1888) each independently re-derive `DisplayName`'s (line 421) exact fallback ternary instead of calling it. Loop 13's own "10, SAME" carried this exact gap forward unnamed - its own Scorecard humility check flagged the risk. Correcting to 9.5 with the newly-named residual (F-024, queued) is the honest application of G6: a 10 requires no fix being available, and one was.
- Test strategy and regression resistance: 8.5 | SAME | `HidePanelAsync` is XAML-bound, untestable-by-design matching its three siblings' established carve-out - no new tests, none owed. 206 tests unchanged. The Authority-Map gap held at 8.5 across loops 11-14 is unaffected by this loop's change.
- Overall implementation credibility: 10 | SAME | `HidePanelAsync`'s doc comment accurately describes the shared sequence; `HideSearchPanelAsync`'s remaining doc comment correctly preserves the `SearchResult_Click` focus-handoff nuance, verified line-by-line against the delegate-based null-conditional call. No stale reference found on independent re-read.

## Authority Map
(Not re-emitted this loop: no authority/state-ownership finding was Priority 1 this loop - F-022 is a module-decomposition finding. See loop 8's archive for the last full Authority Map.)

## Strengths That Matter
- This loop's fresh read of `RestoreBackupAsync`/`RestoreBackupCoreAsync` while investigating the standing directive's candidate (b) surfaced a real, previously-unnoted duplication (`backupGameName` re-deriving `DisplayName`) that thirteen prior critic passes over this exact file had not named - direct evidence the "genuinely at the floor" claims this run keeps re-testing are not rhetorical.
- `HidePanelAsync`'s delegate-parameter design (`Func<int> getSessionId` / `Func<Button> getFocusRestoreTarget` / `Action<Button> setFocusRestoreTarget`) reads the live field on every call rather than snapshotting it into the helper, so the exact reentrancy-guard timing is preserved byte-for-byte against the pre-fix code - verified by direct side-by-side comparison, not by trusting the refactor's own shape.
- The Hide-panel consolidation is the second consecutive loop (after loop 13's `GridSelectionItems`) to act on a specifically-named prior-loop candidate rather than starting a fresh sweep from zero - loop 13's own builder note ("grep the file for the sibling of whatever you just extracted from") pointed directly at this duplication, and it held up under fresh verification.

## Findings

### Finding #1: PrimaryWidget.xaml.cs is a top-churn monolith co-locating UI event handling, network orchestration, file I/O/backup-restore, artwork-selection invocation, panel/search navigation, bulk-operation loops, and library-operation guarding in one class

**Why it matters** — Top-churn file by roughly 5x (38 edits/6mo vs 8 for the next file); `architecture_quality` was stuck at 7.5 across 19 critic passes before loops 10-13 started landing sequenced slices out of it.

**What is wrong** — `PrimaryWidget.xaml.cs` still mixes UI event handling, file I/O/backup-restore invocation, artwork-selection invocation, panel/search navigation, bulk-operation loops, and library-operation guarding after loops 10-13's extractions. This loop closed the specific duplication loop 13's own fresh sweep found and left unconsolidated: `HideGridPanelAsync` and `HideSearchPanelAsync`'s byte-near-identical guard-acquire/session-capture/animate-out/session-recheck/teardown/guard-release sequence is now one shared private `HidePanelAsync` helper both delegate to, matching the `SlidePanelAsync`/`RunUnderLibraryOperationGuardAsync` precedent for the same class of duplication.

**Evidence** —
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` pre-fix (loop-13 HEAD, 1992 lines): `HideGridPanelAsync` (1543-1586) and `HideSearchPanelAsync` (1788-1828) each independently implemented the identical guard/session/animate/recheck/teardown sequence over their own fields (`gridPanelCloseGuard` vs `searchPanelCloseGuard`, `gridPanelSessionId` vs `searchPanelSessionId`, `GridPanelTransform` vs `SearchPanelTransform`, `GridSelectionPanel` vs `GameSearchPanel`, `GridImagesView` vs `SearchResultsListView`, `gridPanelFocusRestoreTarget` vs `searchPanelFocusRestoreTarget`).
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` post-fix (1988 lines): `HidePanelAsync` (1553-1595, new) owns the shared sequence; `HideGridPanelAsync` (1597-1611) and `HideSearchPanelAsync` (1814-1825) now each pass their own guard/session-getter/transform/panel/itemsControl/focus-target-getter-setter/extra-teardown and delegate to it.
- `git diff --stat SteamGridDB.Xbox/PrimaryWidget.xaml.cs`: 63 insertions(+), 67 deletions(-); net -4 lines (1992 -> 1988).
- Full build (msbuild, exit 0) and full test suite (`run-tests.ps1`: 206 passed/0 failed, unchanged) both re-run before and after.

**Architectural test failed** — Deletion test (delete `HidePanelAsync` and the identical sequence reappears inline in both `HideGridPanelAsync` and `HideSearchPanelAsync`, exactly as it did before this loop).

**Dependency category** — n/a.

**Leverage impact** — The guard/session/animate/recheck/teardown sequence is now defined once; a future change can no longer diverge between the grid and search pickers the way `SlidePanelAsync`'s own doc comment already documents once happening (200ms hide vs 250ms show, independently re-derived each time before that consolidation).

**Locality impact** — Contained to `PrimaryWidget.xaml.cs`'s own panel-lifecycle methods; the remaining monolith concerns (network orchestration, file I/O/backup-restore, bulk-operation loops) are unaffected by this loop's change.

**Metric signal** — churn_top20: 38 edits/6mo, ~5x the next-highest file (unchanged this loop; Discovery not re-run).

**Why this weakens submission** — `architecture_quality`'s own 9-anchor requires that a senior reviewer cannot identify a structural improvement that preserves behavior and improves Leverage or Locality; this loop closed exactly the duplication loop 13 itself named as unconsolidated, but network orchestration, file I/O/backup-restore, and bulk-operation loops remain co-located in the same class.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — Sequenced, multi-loop decomposition continues. This loop: consolidated `HideGridPanelAsync`/`HideSearchPanelAsync`'s duplicated lifecycle ceremony into `HidePanelAsync`. Loop 15 should investigate: (a) whether `ReplaceImageCoreAsync`/`RestoreBackupCoreAsync`'s remaining file I/O sequences have any further extractable decision logic beyond the one small residual this loop already separated out (F-024) - this loop's own read found the rest almost entirely `StorageFolder`/`BitmapImage`/`UpdateSharedEntriesAsync` orchestration with little left to extract; (b) whether `FixLibraryAsync`'s grid-then-portrait-then-icon fallback chain has a pure "which source and which rank to try next" decision separable from its `ArtworkDownloader`/`ReplaceImageCoreAsync` I/O calls - not yet investigated, unconfirmed either way.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: everything else - loop 15's candidates named above are unconfirmed until investigated fresh.

---

### Finding #2: RestoreBackupAsync and RestoreBackupCoreAsync each independently re-derive DisplayName's exact name-fallback rule instead of calling it

**Why it matters** — `DisplayName(GameEntry)` already owns the "game.Name unless it's Unknown, then fall back to the image file name" rule; two other methods re-implementing the identical ternary is exactly the kind of independent-copy drift this codebase has already paid for once (`SlidePanelAsync`'s own doc comment: "the two Show/Hide pairs had already drifted apart once ... independently re-derived each time").

**What is wrong** — `RestoreBackupAsync` (line 1863) and `RestoreBackupCoreAsync` (line 1888) both compute `string backupGameName = game.Name != unknownName ? game.Name : imageFileName;`, where `imageFileName` is `Path.GetFileName(game.ImageFilePath)` in both methods - the exact value `DisplayName(GameEntry)` (line 421) already returns via `game.Name != unknownName ? game.Name : Path.GetFileName(game.ImageFilePath)`. Neither call site calls `DisplayName`; each re-derives its formula inline, independently of each other.

**Evidence** —
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:421-424` — `DisplayName(GameEntry game)` owns this exact fallback rule.
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1860-1863` — `RestoreBackupAsync` re-derives the identical ternary as `backupGameName`.
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1885-1888` — `RestoreBackupCoreAsync` re-derives the identical ternary as `backupGameName`, independently of `RestoreBackupAsync`'s own copy.

**Architectural test failed** — n/a.

**Dependency category** — n/a.

**Leverage impact** — None yet - calling `DisplayName(game)` instead removes the second independent copy of the fallback rule and leaves exactly one place that owns it.

**Locality impact** — Fully contained to `RestoreBackupAsync` and `RestoreBackupCoreAsync`; a one-line change at each site.

**Metric signal** — none.

**Why this weakens submission** — A real, source-backed reuse gap: `DisplayName` exists specifically to own this fallback rule, and two of the methods most likely to need exactly that (restore-backup status lines) do not call it, so a future change to the fallback rule would need to be made in three places to stay consistent instead of one.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — Replace `string backupGameName = game.Name != unknownName ? game.Name : imageFileName;` in both methods with `string backupGameName = DisplayName(game);` - a two-line, zero-risk change (identical value; both call sites already compute `imageFileName` earlier only for the file-system calls, not for this).

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (`RestoreBackupAsync`, `RestoreBackupCoreAsync` only). Avoid: everything else.

---

### Finding #3: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `GameMatchResolver.ResolveAsync`'s per-entry network sequence runs in strict sequence across entries, even though the awaits are independent across entries. Unchanged this loop (this loop's own edit is entirely inside `HideGridPanelAsync`/`HideSearchPanelAsync`, well below `LoadGameEntriesAsync` in the file).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:426-697` (unchanged this loop); `SteamGridDB.Xbox/Services/Library/GameMatchResolver.cs`.

**Architectural test failed** — n/a.

**Dependency category** — n/a.

**Leverage impact** — None currently actionable - see blocker.

**Locality impact** — Fully contained to `GameMatchResolver.ResolveAsync`.

**Metric signal** — none.

**Why this weakens submission** — A real, linearly-scaling latency cost, but BLOCKED: the recorded standing user constraint means this loop cannot land a change altering observable call ordering/concurrency/count without a product decision this loop cannot make.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — BLOCKED this loop; named for continuity per Backlog Prioritization Pass criterion 0.

**Blast radius** — Change: none (blocked). Avoid: `SteamGridDB.Xbox/Services/Library/GameMatchResolver.cs` while blocked.

## Simplification Check
- Structurally necessary: Consolidating `HideGridPanelAsync`'s and `HideSearchPanelAsync`'s byte-near-identical guard/session/animate/recheck/teardown sequence into `HidePanelAsync` - passes the deletion test.
- New seam justified: false - no protocol/interface introduced (plain private method with delegate parameters).
- Helpful simplification: `HideGridPanelAsync`'s body shrank from ~35 lines to 10; `HideSearchPanelAsync`'s body shrank from ~31 lines to 8. `PrimaryWidget.xaml.cs`: 1992 -> 1988 lines (net -4; 63 insertions, 67 deletions).
- Should NOT be done: Also consolidating `ShowGridPanelAsync`/`ShowSearchPanelAsync` - investigated and rejected: `ShowGridPanelAsync` is a 2-line visibility+slide call, while `ShowSearchPanelAsync` additionally builds header text, prefills the search box, and branches focus - the two Show methods are not the same shape, and forcing them into one helper would fail SPT Q3. Also not done: fixing the newly-found F-024 (`backupGameName`/`DisplayName` duplication) in the same diff - queued instead, to keep this loop's diff scoped to the one Finding the Implementation Reviewer checks against.
- Tests after fix: No new tests. `HidePanelAsync` stays XAML-bound and untestable-by-design, matching `RunUnderLibraryOperationGuardAsync`/`ConfirmAndRunAsync`/`SlidePanelAsync`'s own carve-out. Verification: full build (exit 0) and full test suite (206 passed/0 failed, unchanged count) both re-run before and after this loop's own independent review.

## Improvement Backlog
1. **[F-022]** Continue `PrimaryWidget.xaml.cs`'s monolith decomposition - structural, needed for winning. Why it matters: standing user directive, Priority 1 for this run; this loop's own investigation named two unconfirmed candidates for loop 15 (`ReplaceImageCoreAsync`/`RestoreBackupCoreAsync`'s remaining I/O sequences, and `FixLibraryAsync`'s grid/portrait/icon fallback chain). Score impact: architecture_quality +0.5.
2. **[F-024]** Route `RestoreBackupAsync`/`RestoreBackupCoreAsync`'s name-fallback computation through the existing `DisplayName` helper - simplification, nice to have. Why it matters: small, real, source-backed duplication of an already-owned formula; passes Simplify Pressure Test cleanly (smallest honest fix, zero risk) - ready to land without further investigation. Score impact: simplicity +0.5.
3. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution - structural, needed for winning. Why it matters: BLOCKED by the standing user constraint on per-game network-call ordering/concurrency; named for continuity per Backlog Prioritization Pass criterion 0 rather than silently dropped. Score impact: concurrency +0.5.

## Deepening Candidates
None this loop.

## Builder Notes
- Pattern 1: A prior loop's own fresh-grep finding (documented in a builder note, not yet acted on) can sit unconsolidated for exactly one more loop before someone verifies and lands it - the finding does not go stale just because the loop that found it chose a different Priority-1 item that loop.
- Pattern 2: Investigating a named candidate honestly, even when it turns out too thin to be the loop's main slice, can still surface a smaller-but-real adjacent finding worth naming separately.
- Pattern 3: A dimension held at a clean 10 across several loops is worth re-testing with a fresh, skeptical read specifically when that loop's own work just demonstrated a same-shaped fix was still available - a 10 that survives its own precedent's contradiction is honest; a 10 that ignores it is not.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) `data_flow`'s 7.5 SAME - re-derived fresh with five named static-cache classes rather than the prior "four scattered sites," but a stricter critic could argue `AppliedArtworkStore`'s cache (reassigned, not just read, per its own SetFolder-style method at line 54) is a materially different risk shape than the read-mostly `StoreNameLookup` caches and deserves separate accounting. (2) `simplicity`'s correction to 9.5 (from loop 13's 10) - the newly-named F-024 residual is real and small, but a stricter critic could argue this loop should have just fixed it in the same diff rather than queuing it and taking the score hit; keeping the diff scoped to one Finding for the Implementation Reviewer's sake is a process judgment call, not a purely architectural one. (3) `architecture_quality` staying at 9.0 SAME rather than crediting any fraction of it for `HidePanelAsync` - the precedent (loop 12's `RunUnderLibraryOperationGuardAsync` credited entirely to `simplicity`) is followed here, but `HidePanelAsync` is, by the rubric's own scale-agnostic Module definition, a real Module with two callers and a real 8-parameter Interface - a stricter critic could argue it deserves a small `architecture_quality` nudge too.

## Final Judge Narrative
Place, not win. This loop picked up loop 13's own named next step rather than starting a fresh sweep, and verified - not assumed - that the claimed duplication was real before touching any code: a line-by-line comparison of `HideGridPanelAsync` and `HideSearchPanelAsync` confirmed the guard-acquire/session-capture/animate-out/session-recheck/teardown/guard-release sequence was byte-near-identical apart from which fields each closed over, and the resulting `HidePanelAsync` helper preserves that exact sequence and timing through delegate parameters that read live field values rather than stale snapshots. Runtime ownership and concurrency both stayed trustworthy for the same reason: no new field, no new lock, no new await ordering, and the reentrancy-guard timing was checked byte-for-byte against the pre-fix code. The more consequential move this loop made was smaller and less flattering to report: while investigating the standing directive's second named candidate (the decision logic inside `ReplaceImageCoreAsync`/`RestoreBackupCoreAsync`), that candidate turned out mostly too thin to be this loop's main slice - but the same close read surfaced `RestoreBackupAsync` and `RestoreBackupCoreAsync` both independently re-deriving `DisplayName`'s exact fallback formula instead of calling it, a real duplication no prior loop had named. That finding forced a correction: `simplicity` had sat at a clean 10 for three loops running, including through loop 13's own Scorecard humility check explicitly flagging the risk that 10 was premature - this loop's own fresh work proved the flag right, and the honest response is a downward correction to 9.5 with the new residual named and queued (F-024), not a quiet re-affirmation of the old ceiling. `data_flow` was re-derived fully fresh this loop rather than re-cited, per the standing directive's explicit instruction: current source shows five classes (not four) holding deliberate, documented, process-lifetime static ambient state, which exceeds the 9-anchor's "one or two documented" allowance and legitimately caps the dimension below 9.5; converting them to instance-based dependency injection would fail the Simplify Pressure Test for a single-instance desktop-widget process with no real multi-tenancy need. The risk to watch for loop 15 (the cap loop): F-022's remaining candidates are both named but neither is confirmed - loop 15 should investigate both honestly and, if neither passes the Simplify Pressure Test, say so plainly rather than manufacturing an extraction; F-024 is a small, safe, ready-to-land item that should not need another full investigation.

## Loop 14 Result
Consolidated `PrimaryWidget.xaml.cs`'s `HideGridPanelAsync` and `HideSearchPanelAsync` - previously each hand-implementing the identical guard-acquire/session-capture/animate-out/session-recheck/teardown/guard-release sequence over their own fields - into one shared private `HidePanelAsync(LibraryOperationGuard, Func<int>, TranslateTransform, UIElement, ItemsControl, Func<Button>, Action<Button>, Action)` helper; both call sites now delegate to it with their own guard/session-getter/transform/panel/itemsControl/focus-target-getter-setter/extra-teardown arguments, preserving each site's exact prior control flow and ordering. `PrimaryWidget.xaml.cs` line count: 1992 -> 1988 (net -4), verified via `(Get-Content SteamGridDB.Xbox/PrimaryWidget.xaml.cs).Count`. Full build (`msbuild SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never`, exit 0) and full test suite (`run-tests.ps1`: 206 passed/0 failed, unchanged) both re-run before and after. Finding F1 (stable_id F-022) is **carried_forward** - this loop's slice is real, tested (via reviewer-verified behavior preservation, no new tests owed per the XAML-bound carve-out), and reviewer-approved progress, but the finding's broader claim is not resolved. Finding F2 (stable_id F-024, new) is **carried_forward** (queued to backlog, not fixed this loop, by design). No unintended scorecard regression: `state_management`, `domain_modeling`, `framework_idioms`, `concurrency`, and `credibility` were each independently re-checked against the diff and hold.

## Loop 14 Implementation Review
**Verdict: approved** (all three checks passed, first pass, no re-spawn needed). Reason: "HideGridPanelAsync and HideSearchPanelAsync now both delegate to a single shared private HidePanelAsync method that preserves the exact original guard/session/animate/recheck/teardown order and per-side field wiring, with no new seam and no stale-capture reentrancy hazard introduced." No regressions flagged; no conditions.
