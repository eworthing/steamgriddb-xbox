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
