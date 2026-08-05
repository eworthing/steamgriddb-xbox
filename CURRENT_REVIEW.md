### Discovery
- Source roots: SteamGridDB.Xbox/, SteamGridDB.Xbox.Tests/
- Test command: `powershell -NoProfile -File ./run-tests.ps1`
- Build command: `msbuild SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo` (msbuild at `C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe`, resolvable via vswhere.exe)
- ADRs found: none
- Domain terms (CONTEXT.md): none
- Selected lens: Generic (always-included: lens-security.md, lens-efficiency.md)
- Prior audit docs: ARTWORK-SELECTION.md (2026-08-03), CODE-REVIEW.md (2026-08-03), TESTING.md (2026-08-03)
- Churn top files: PrimaryWidget.xaml.cs (38 edits), SteamGridDbClient.cs (8), StoreNameLookup.cs (5) — all three mandatory deep-review targets, all read in full this loop.
- Working tree dirty paths at Step 0: CURRENT_REVIEW.json (deleted by `--reset`), REVIEW_HISTORY.md (archive divider appended by `--reset`). Neither overlaps this loop's blast radius.

### Loop Counter
Loop 1 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

The artwork pipeline (ArtworkRanker/ArtworkDownloader/TileImage/ArtworkSignature) is genuinely deep, pure and well-tested, and PrimaryWidget.xaml.cs's own state-ownership discipline keeps improving loop over loop. This loop independently re-derived the same Serious reentrancy gap loop 10's Critic-only pass surfaced (F-014) and closed it, but its existence after nine prior dedicated session-guard sweeps, plus a fresh latent inconsistency this loop found in StoreNameLookup's caches (F-015), keep this codebase short of contest-grade.

**Prior-audit adopt-or-falsify** (run after this loop's independent scorecard draft, per blind-critic ordering): CODE-REVIEW.md (2026-08-03) is self-dispositioned "Status — all fixed" for all 15 findings except the deliberately-rejected `.new`-file cleanup; spot-checked three of its closed claims (#8 `DecodePixelWidth`, #10 `GridMetadata` memoization, #12 `BuildUrl` consolidation) directly against current source and confirmed all three still hold. TESTING.md is scope documentation, not a findings list — its "PrimaryWidget.xaml.cs is not covered" claim is the same boundary this loop's own `test_strategy` proof independently reached. ARTWORK-SELECTION.md (2026-08-03) is self-dispositioned via its own Status table — every proposal is implemented or tried-and-reverted except §4.8 (corner gate at 9% reach), which the document's own author already concluded "no evidence calls for it"; artwork-selection quality is also outside this rubric's architecture scope regardless. No open claim from any of the three docs was silently dropped.

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
- Structurally necessary: Closing F-014's guard-scope gap by extending the existing `TryBeginLibraryOperation`/`EndLibraryOperation` guard — a state-ownership fix, not a Deletion/Seam-category fix.
- New seam justified: false.
- Helpful simplification: none this loop beyond the fix itself.
- Should NOT be done: A dedicated single-game-operation guard/token distinct from `isLibraryOperationRunning` (duplicate layer). Wrapping `ReplaceImageCoreAsync`/`DownloadAndReplaceImageCoreAsync` themselves (would self-reject `FixLibraryAsync`'s own already-guarded bulk writes).
- Tests after fix: None added or deleted — `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface (WinUI Page, no desktop projection). Verification: full build + full test suite (138/138 unchanged) both re-run, independent fresh-eyes implementation review (approved, all three checks passed), manual trace confirming both guards hold across their full write duration.

## Improvement Backlog
1. **[F-014]** Extend the library-operation guard to single-game writes — structural, needed for winning. Closes a live, reachable, silently-corrupting reentrancy gap in the same class as F-001/F-005-009 (all fixed) — the largest-severity, fully-actionable candidate this loop. Score impact: `state_management +1.0`.
2. **[F-013]** Extract a shared entry-update helper for `ReplaceImageCoreAsync`/`RestoreAllChangesAsync`/`RestoreBackupCoreAsync` — simplification, helpful. Removes a third instance of the leaf-duplication class F-002/F-003 already fixed. Score impact: `simplicity +0.5`.
3. **[F-015]** Gate `StoreNameLookup`'s GOG/Epic caches the same way its own Ubisoft cache already is — structural, helpful. Closes a latent unsynchronized-write inconsistency and removes credibility's own queued residual. Score impact: `concurrency +0.5`.

**Priority-1 accounting**: F-014 moves `state_management` (the dimension the codebase's own history already attributes this defect class to) via a fully actionable, Serious-severity candidate; no higher-severity candidate was found this loop, and F-011's larger `concurrency` gain is blocked by the standing user constraint (criterion 0), so F-014 is Priority 1.

## Deepening Candidates
None this loop. The remaining findings (F-013's duplication, F-015's cache-locking gap) are simplification/consistency fixes, not Module-deepening opportunities — no caller reaches past an existing Interface, and no deletion test fails for any current Module.

## Builder Notes

**A mutual-exclusion guard's real coverage is defined by which call sites hold it, not by its own doc comment.** Grep every caller of a boolean `IsXBlocking()`-style check; confirm each also calls the paired `TryBeginX()/EndX()` claim around its own work. A guard-check function whose name is a present-participle predicate is a signal to verify a matching Begin/End pair at every caller performing the guarded work, not only the callers that look "library-wide" at a glance. C# example: `if (IsBusy) return;` without a paired `IsBusy = true; try { ... } finally { IsBusy = false; }` is a check without a claim — exactly what let F-014 (and originally F-005 through F-009) through several prior review passes.

**The same small primitive (a gated lazy cache) gets reinvented per-field instead of reused, and only the newest field gets the lock.** Grep for process-lifetime `Dictionary<...>` fields paired with a `TryGetValue`-then-conditional-write method nearby; if a sibling field in the same file uses a proper gate and this one doesn't, that's the tell. When a file already has one correctly-locked cache, a second cache added later should reuse the same gate or cache type. This codebase's own `AsyncLazyCache<T>` already solves this — `StoreNameLookup`'s Ubisoft cache uses it, its GOG/Epic caches (added earlier) don't.

**A near-identical block appearing a third time in one file is this codebase's own established threshold for "extract it."** It collapsed F-002 at 4 copies and F-003 at 3. On finding a third near-identical block, extract before writing a fourth — not after. F-013 is a not-yet-collapsed third instance of the same shape.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) credibility at 9.5 rather than 9.0 — F-015 is Noticeable, not Cosmetic, and a stricter reading could cap any Noticeable-severity residual at 9.0; 9.5 was judged defensible because F-015 is genuinely local and subtractive-fixable, but this is a judgment call rather than a bright line. (2) state_management held at 7.0 despite fixing F-014 this loop — this follows the project's own established convention (score reflects pre-fix source; credit lands next loop against this loop's commit sha), which a reviewer could reasonably expect same-loop credit for instead. (3) F-015's severity at Noticeable rather than Serious — judged latent based on `LoadGameEntriesAsync`'s single sequential per-entry loop being the sole caller; if some other code path reaches it concurrently that this loop's bounded read did not find, the severity should be higher.

## Final Judge Narrative
Place, not win, yet. The codebase has real depth in its artwork pipeline and increasingly disciplined ownership in `PrimaryWidget.xaml.cs`, but this loop independently re-derived and closed the sixth-and-latest instance of a defect class (reentrancy gaps between per-game and library-wide operations) that nine prior loops of dedicated sweeps had not fully closed — runtime ownership is more trustworthy after this loop's fix than before it, but a well-reviewed guard's own doc comment being incomplete for this long is a caution against declaring the class closed. Concurrency remains trustworthy for what actually runs today (nothing executes off the UI thread), though this loop surfaced a latent inconsistency (F-015) worth closing before any future change makes cache access concurrent. Simplification helped this loop: the fix reused an existing primitive rather than inventing a new guard type. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs` itself — every finding in this file was found and verified by direct reading and unchanged build/test evidence, not by a failing test turning green. Future work risks over-engineering if it tries to extract PrimaryWidget's orchestration into a testable Module wholesale, per this and prior loops' own Simplify Pressure Test analysis; the smaller F-013/F-015 fixes ahead carry no such risk.

## Loop 1 Result
Extended the existing `TryBeginLibraryOperation()`/`EndLibraryOperation()` guard (already held for the duration of `RefreshButton_Click` and `ConfirmAndRunAsync`'s bulk operations) to `GridImage_Click`'s `DownloadAndReplaceImageAsync` call and `RestoreBackup_Click`'s `RestoreBackupAsync` call — the two remaining call sites that started a single-game write without claiming the flag. Updated the `isLibraryOperationRunning` field comment and both methods' doc comments to describe the extended scope. Full build (exit 0) and full test suite (138/138 unchanged, expected — `PrimaryWidget.xaml.cs` is outside the test-linked surface) both re-run after the change. Neither guard touches, reorders, or wraps any network/file-write call — no SteamGridDB/GOG/Epic/Ubisoft call count, ordering, or payload changes anywhere, satisfying the standing user constraint. Independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on the finding and the diff only) returned verdict **approved** with all three checks (reality, honesty, regression) passed, explicitly verifying `ReplaceImageCoreAsync`/`DownloadAndReplaceImageCoreAsync` were correctly left unwrapped to avoid self-rejecting `FixLibraryAsync`'s own already-guarded bulk writes, and that no third unguarded single-game write path exists. Finding F-014 (stable_id F-014) is **resolved**. No unintended scorecard regression observed.
