### Loop Counter
Loop 3 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

Simplicity took a real, source-proven step forward this loop: F-013's collapse (landed loop 2, commit fdf758f) is now visible in current source with `UpdateSharedEntriesAsync` cleanly serving all three former call sites, and simplicity is credited for it this loop per this project's own established scoring convention. This loop's own refactor (F-015) closed the last of `StoreNameLookup`'s three unsynchronized caches, extending the file's own `AsyncLazyCache<T>`/dedicated-gate idiom from one field to four. What keeps this short of contest-grade: `PrimaryWidget.xaml.cs` still spans five concerns in one Module, `StoreNameLookup`'s three ambient-state caches (now locked, still ambient) keep `data_flow` capped, and F-011 (sequential per-entry network calls) remains genuinely blocked by the standing user constraint.

**Prior-audit adopt-or-falsify** (run after this loop's independent scorecard draft, per blind-critic ordering): CODE-REVIEW.md, TESTING.md and ARTWORK-SELECTION.md were adopted/falsified against current source in loop 1 with no open claim dropped; this loop re-confirmed no new prior-audit claim changed status (this loop's own edit is confined to `StoreNameLookup.cs`, outside the areas those three documents cover beyond the artwork pipeline and test scope, both re-checked directly this loop via independent helper sweeps).

## Scorecard (1-10)

- Architecture quality: **7.5** | SAME | Direct full re-read this loop of `PrimaryWidget.xaml.cs` (2067 lines, current) plus two independently-briefed helper sweeps (Services/Artwork+Library; Models+Services/Stores/SteamGridDB) converge on the same picture as loops 1-2: `Services/*` remains deep, single-responsibility Modules. `PrimaryWidget.xaml.cs` still spans five concerns (library load/manifest parsing, bulk fix/restore/revert, grid picker, search panel, single-game ops) in one Module; this loop's fix touched only `StoreNameLookup.cs`, not this file. No structural proof of a move either direction.
- State management and runtime ownership: **9.5** | SAME | `residual_disposition: accepted` | Direct re-read this loop of `PrimaryWidget.xaml.cs`'s guard paths (`TryBeginLibraryOperation`/`EndLibraryOperation`, `GridImage_Click`:1527-1545, `RestoreBackup_Click`:1914-1934) re-confirms F-014's fix holds with no unguarded single-game write path. The 9-anchor is met. Residual blocking 10 unchanged: `SteamGridDbClient.CapsuleParseNotes` (`SteamGridDbClient.cs:47`) is `public static readonly List<string>` - the `readonly` only pins the reference, not the contents. **Adversarial Pass re-run this loop**: smallest fix (private backing field + `IReadOnlyList<string>` accessor) is trivial and would pass SPT Q1-Q4, but fails Q5 comparatively - the gain (defense-in-depth encapsulation, zero live-harm evidence, one actual reader at `PrimaryWidget.xaml.cs:967`) is real but smaller than this loop's actual pick (F-015, Noticeable severity, broader/longer-queued concern), and fixing it in the same loop as F-015 would cross into a second, unrelated file, breaking F-015's own bounded blast radius. SPT-rejected on Q5; residual holds, accepted.
- Domain modeling: **9.5** | SAME | `residual_disposition: accepted` | Independent helper sweep this loop (Models+Services/Stores/SteamGridDB) plus my own direct re-read of `GameEntry.cs` (196 lines, in full) re-confirm `GameEntry`'s parallel-fields case (`HasSteamGridDBMatch`/`OfficialCapsuleUrl`/`SteamGridDbGameId`, `GameEntry.cs:113-145`) unchanged. **Adversarial Pass re-run**: the previously-proposed smallest fix (readonly-struct-with-factories mirroring `ManifestEntryIdentity.Result`) fails SPT Q2 outright this loop, not just on blast radius - `GameEntry` implements `INotifyPropertyChanged` and is two-way XAML-data-bound (`{Binding}` in `PrimaryWidget.xaml`); a readonly struct cannot back that binding shape without an entirely separate wrapper layer, which is a larger redesign, not the smallest honest fix. Residual holds, accepted.
- Data flow and dependency design: **7.5** | SAME | `StoreNameLookup.cs`'s three per-key caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`) plus `SteamGridDbClient.CapsuleParseNotes` remain four process-lifetime ambient-state instances, exceeding the 9-anchor's "one or two ambient-context dependencies" allowance. This loop's own fix (F-015) adds synchronization to three of the four - a `concurrency`-dimension change (safe access) - but does not reduce the *count* of ambient dependencies, which is what this dimension's anchor actually measures; the caches are still module-level static state reachable without being threaded through parameters. Locking a cache changes its concurrency-safety story, not its data-flow shape, so this score is unchanged by this loop's fix.
- Framework / platform best practices: **9.5** | SAME | `residual_disposition: accepted` | `App.xaml.cs:120`'s `//TODO: Load state from previously suspended application` (inside an otherwise-empty `if` block on the documented fallback-only `OnLaunched` path) re-read in full this loop, unchanged. **Adversarial Pass re-run**: this loop considered a smallest fix the prior loops' own reasoning never named - deleting the dead comment outright (the branch does nothing today; `Window.Current.Content = rootFrame` two lines below is unaffected either way) - which would pass SPT Q1-Q4 as a genuinely free, zero-risk subtractive change. It fails Q5 comparatively: the gain (removing one stale comment) is real but marginal, and clearly smaller than this loop's actual pick (F-015); bundling it into this loop's commit would also cross into a third unrelated file. SPT-rejected on Q5; residual holds, accepted.
- Concurrency and runtime safety: **6.5** | SAME | Scored against source *as read at Step 1*, before this loop's own Step 2/3 fix (per this project's established convention - see Code simplicity below for the mirror case). At Step 1, F-011 (sequential per-entry network calls) is re-confirmed unchanged, still blocked by the standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency against SteamGridDB/GOG/Epic/Ubisoft); re-derived the blocker fresh this loop rather than citing it - confirmed a pure extraction would not be blocked, but F-011's own remedy (bounded concurrency) necessarily changes network-call ordering, so it stays genuinely blocked. F-015 (StoreNameLookup's three unsynchronized caches) is re-confirmed still present in the pre-fix source this Step 1 is scoring against. Both named concerns are still live at scoring time; this loop's own fix (which resolves F-015) will be credited at loop 4's Step 1 against this loop's own commit SHA, matching the same convention that credited F-014 at loop 2 and will credit F-013/F-015 as this loop's own commits land.
- Code simplicity and clarity: **9.5** | UP | `residual_disposition: accepted` | Structural proof for the UP: `git log 9b2c4cb..fdf758f` shows loop 2's own commit ("collapse the triplicated UI-thread entry-update loop into a shared UpdateSharedEntriesAsync helper"), which this loop's direct full re-read of current `PrimaryWidget.xaml.cs` confirms landed and holds - `UpdateSharedEntriesAsync` (:348-368) is the sole entry-update path for `ReplaceImageCoreAsync`, `RestoreAllChangesAsync` and `RestoreBackupCoreAsync`, with no drift from the three original per-site field-write differences. **Mandatory leaf-module duplication sweep this loop** (three parts, per method.md Step 6): (a) leaf modules read - all of `Services/Artwork/*` (5 files), `Services/Library/*` (2 files, `GameImages.cs` + `OperationReport.cs`), `Models/*` (3 files) and `Services/Stores/EpicLibrary.cs` plus `Services/SteamGridDB/*` (5 files), via two independently-briefed helper sweeps this loop, plus my own full read of `PrimaryWidget.xaml.cs`, `StoreNameLookup.cs`, `SteamGridDbClient.cs`, `GamePlatform.cs`, `AppliedArtworkStore.cs`, `FixLog.cs`, `AsyncLazyCache.cs`; (b) four-angle results - Reuse: clean (no re-implemented shared helper found); Simplification: `GameEntry.cs`/`GridImageItem.cs` share the same `INotifyPropertyChanged` boilerplate shape but only 2 instances, below the file's own established 3-instance extraction threshold - correctly not a finding; Altitude: clean; Efficiency (lens-efficiency D1-D4): clean, no new candidate found; (c) mechanical-seed adoption: no `audit_clones.py`/`audit-enum-interpretation.sh` available in this repo checkout, so this sweep relied on the manual four-angle pass plus direct reads in place of the mechanical seed - noted as a scope limit, not skipped. Residual blocking 10: `GamePlatform.cs`'s two independent switch statements (Finding F-012, Cosmetic). **Adversarial Pass re-run**: F-012's own smallest fix (fold both mappings into one table with a small alias list) still passes SPT Q1-Q4 but fails Q5 comparatively against F-015 (this loop's actual, higher-severity pick); residual holds, accepted.
- Test strategy and regression resistance: **6.5** | SAME | `PrimaryWidget.xaml.cs` carries zero direct or indirect test coverage - confirmed again this loop (no test file under `SteamGridDB.Xbox.Tests/` references `PrimaryWidget`; `run-tests.ps1` output unchanged at 138/138 before and after this loop's edit). Mutation-test mental model re-applied: a mutation flipping `GridImage_Click`'s `if (!TryBeginLibraryOperation()) { return; }` guard would silently invert which clicks are allowed to write, and no test in this repo would catch it - the same primary-flow gap named every loop since loop 1, unchanged this loop. `Services/*` remains comprehensively covered per two independent helper sweeps this loop (Artwork+Library sweep: "no structural issues found... Test files are well-organized with each production file paired to a test file"; Models+Stores sweep found a handful of untested pure-logic files - `GameEntry.cs`, `GridImageItem.cs`, `ArtworkSource.cs`, `EpicLibrary.cs` - each either an off-primary-flow XAML-bound model (same permanent constraint class as `PrimaryWidget.xaml.cs` itself) or a trivial factory/network-adjacent type; none moves this score, per the severity anchor's own carve-out that "untested helper code or off-path utilities are not disqualifying"). Permanent platform constraint (WinUI `Page`, no desktop projection for `Windows.UI.Xaml` types in this repo's xunit infra), not an unaddressed choice - TESTING.md documents the same boundary.
- Overall implementation credibility: **9.5** | SAME | `residual_disposition: queued` (F-015) | Scored against source as read at Step 1 (pre-fix): F-015's unsynchronized-cache inconsistency is still present in the source this Step 1 evaluates, so the residual stays `queued` rather than moving to `accepted` or dropping - it is this loop's own Priority-1 pick, and the queued-to-resolved transition (like F-013's and F-014's before it) will show up in the *next* loop's Step 1, scored against this loop's own commit.

## Strengths That Matter
- `ArtworkRanker`/`ArtworkDownloader`/`ArtworkSignature`/`TileImage` remain a genuinely deep, pure, well-tested pipeline (no I/O beyond the download itself, no UI) - independently re-confirmed this loop by a cold helper sweep finding "no structural issues... no domain-policy leakage... documentation matches implementation" across all five files plus `ArtworkFiles.cs` and `Services/Library/*`.
- `AppliedArtworkStore`'s and (as of this loop) `StoreNameLookup`'s own four caches all now gate concurrent access to their shared Dictionaries behind a dedicated `SemaphoreSlim` - three of `StoreNameLookup`'s four caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`) joined `ubisoftGameListCache` in that idiom this loop, via a dedicated gate per cache rather than one shared gate, so unrelated stores' lookups stay independent of each other rather than serializing behind a single lock for no reason.
- This loop's own fix required zero new abstractions and zero new Seams: the double-checked-locking shape it adds to three methods is the same shape `AsyncLazyCache<T>.GetOrLoadAsync` already uses and `AsyncLazyCacheTests.cs`'s own 32-concurrent-caller test already proves race-free for that type, just applied inline at each cache's own call site instead of behind a new generic wrapper.

## Findings

### Finding #1: StoreNameLookup's three hand-rolled caches perform unsynchronized check-then-populate writes, unlike the file's own Ubisoft cache three lines below

**Why it matters** — The type's own doc comment says every cache here is "shared across the whole process," but before this loop only the Ubisoft cache (via `AsyncLazyCache<T>`) actually protected that shared state from a concurrent race; a future caller or a future fix to F-011 (blocked) would otherwise silently inherit an unsynchronized write.

**What is wrong** — `gogNameCache`/`epicNameCache` (`StoreNameLookup.cs:29-30` at Step 1) and `nameMatchCache` (`:34`) were plain `Dictionary` fields read/written by `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/`FindGameByNameAsync` with a bare check-then-populate and no lock, three lines from `ubisoftGameListCache` (`:40-42`), which already solves the identical shape via `AsyncLazyCache<T>`'s `SemaphoreSlim` gate.

**Evidence** — `StoreNameLookup.cs:29-30`; `:34`; `:40-42`; `AsyncLazyCache.cs:19-60` (all pre-fix line numbers, as read at Step 1).

**Architectural test failed** — n/a (concurrency-primitive inconsistency, not a Seam/Module-boundary defect).

**Dependency category** — `in-process`.

**Leverage impact** — A caller got no concurrent-access-safety guarantee from the Interface alone; safety depended on the sole caller (`LoadGameEntriesAsync`'s per-entry loop) happening to await sequentially.

**Locality impact** — Fully contained inside `StoreNameLookup.cs`; the fix reuses the file's own existing gate idiom at per-cache granularity.

**Metric signal** — none.

**Why this weakens submission** — Not currently reachable as a live race (the sole caller awaits sequentially), but a real structural inconsistency spanning three of the file's four mutable-state fields, in a codebase whose own doc comments elsewhere go out of their way to prevent exactly this (see `AppliedArtworkStore.cs`'s explicit shared-gate rationale).

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — Wrap `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/`FindGameByNameAsync`'s check-then-populate bodies in a dedicated `SemaphoreSlim` gate per cache (not the shared gate `ubisoftGameListCache` uses, since the three caches hold unrelated per-game data and serialising them behind one lock would block one store's lookup on a different store's network round trip for no reason), matching `AppliedArtworkStore`'s own per-cache dedicated-gate pattern.

**Blast radius** — Change: `StoreNameLookup.cs`. Avoid: `PrimaryWidget.xaml.cs`, `EpicLibrary.cs`.

---

### Finding #2: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs:455-717` at current line numbers) awaits each entry's SteamGridDB platform-ID lookup, store name-fetch and SteamGridDB name search in strict sequence, even though the awaits are independent across entries.

**Evidence** — `PrimaryWidget.xaml.cs:455-717`.

**Architectural test failed** — n/a.

**Leverage impact** — None currently actionable — see blocker.

**Locality impact** — Would be contained to `PrimaryWidget.xaml.cs` (now also depends on F-015's just-landed cache-locking prerequisite), if unblocked.

**Metric signal** — none.

**Why this weakens submission** — A real, linearly-scaling latency cost, but **BLOCKED**: the recorded standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft) means this loop cannot land a change altering observable call ordering/concurrency/count against those services without a product decision this loop cannot make. Re-confirmed this loop: a pure extraction (no ordering/count/payload change) would not be blocked, but F-011's own remedy (bounded concurrency) necessarily changes network-call ordering, so it stays genuinely blocked.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — BLOCKED this loop; named for continuity (Backlog Prioritization Pass criterion 0) rather than escalated to a user-decision halt, since F-015 filled this loop's actionable Priority-1 slot.

**Blast radius** — Change: none this loop. Avoid: `PrimaryWidget.xaml.cs` (no change while blocked).

---

### Finding #3: GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding or renaming a platform requires remembering to update both switches; nothing fails to compile if one is forgotten (both silently default to `Unknown`/`null`).

**What is wrong** — `FromXboxDirectory` (`GamePlatform.cs:22-46`) and `GamePlatformToSGDBApiString` (`:48-67`) independently switch over the same platform cases with no shared table; `FromXboxDirectory` additionally handles legacy folder-name aliases (`"ubi"`/`"ubisoft"`, `"bnet"`/`"battlenet"`) that have no analogue in the reverse mapping.

**Evidence** — `GamePlatform.cs:22-46`; `:48-67`.

**Architectural test failed** — n/a.

**Leverage impact** — Each new platform pays this asserted-twice tax; small today, compounds at scale.

**Locality impact** — Contained to `GamePlatform.cs`.

**Metric signal** — none.

**Why this weakens submission** — Minor, but the enum's own home module holds two independent interpretations of itself with no compiler-enforced link; this loop's Adversarial Pass on simplicity's newly-accepted residual re-tested the smallest fix (fold both mappings into one table) and confirmed it still fails Simplify Pressure Test Q5 - a real but smaller gain than this loop's actual pick (F-015).

**Severity** — Cosmetic for contest.

**ADR conflicts** — none.

**Minimal correction path** — Fold both mappings into one table keyed by `GamePlatform`, with a separate small alias list for `FromXboxDirectory`'s legacy folder names.

**Blast radius** — Change: `GamePlatform.cs`. Avoid: `PrimaryWidget.xaml.cs` (call sites unchanged).

## Simplification Check
- Structurally necessary: Adding dedicated `SemaphoreSlim` gates to `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/`FindGameByNameAsync`'s check-then-populate bodies - a concurrency-primitive fix matching the file's own established `AsyncLazyCache<T>`/`AppliedArtworkStore` gate idiom, not a Deletion/Seam-category or Deepening fix.
- New seam justified: false — no new Interface or Module boundary is introduced; each gate is a private field inline in the existing `internal static class StoreNameLookup`.
- Helpful simplification: Also removes credibility's own queued residual (F-015) and closes a latent structural inconsistency spanning three of the file's four mutable-state fields.
- Should NOT be done: A single shared gate across all three (or all four) caches — would serialize independent per-store lookups against each other for no reason and would specifically constrain any future F-011 fix more than necessary (fails Simplify Pressure Test Q4, runtime behavior would become less honest about what's actually independent). A new generic keyed-cache abstraction (e.g. `AsyncKeyedLazyCache<TKey,TValue>`) to unify all three call sites — the three caches differ in value type (`string` vs `int`) and miss/retry policy (empty-is-a-miss for the two name caches vs. cached-zero-is-final for the match cache), so a shared generic would need extra parameters to express that, adding ceremony without collapsing any of the three method bodies (fails Simplify Pressure Test Q2/Q3).
- Tests after fix: None added or deleted. `StoreNameLookupTests.cs`'s own docstring already documents the test-scope limit this fix falls inside: "Only the pure part is covered here. The rest of StoreNameLookup calls GOG, Epic and Ubisoft over the network, and a test that did that would be grading their uptime." All three touched methods gained synchronization around network calls with no injectable transport, so a focused concurrency test is not buildable without a larger DI redesign (out of scope for this fix — see Loop 3 Result's `risk_boundary_evidence`). Verification: full build (exit 0) + full test suite (138/138 unchanged) both re-run before and after the change, independent fresh-eyes implementation review, and a manual trace confirming each method's single-threaded-caller return value and network-call count are byte-identical to the pre-fix version (the gate is uncontended under today's sequential caller, so `WaitAsync` never blocks).

## Improvement Backlog
1. **[F-015]** Wrap `StoreNameLookup`'s three hand-rolled caches in dedicated per-cache gates (Finding F-015) — structural, helpful. Closes the last of the three unsynchronized caches, ends a two-loop queued residual on credibility. Score impact: `concurrency +0.5; credibility +0.5`.
2. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution — efficiency, needed for winning once unblocked. **BLOCKED** by the standing user constraint on per-game network-call ordering/concurrency; named for continuity, not actionable this loop. Score impact: `concurrency +1.0`.

**Priority-1 accounting**: With F-013 resolved (loop 2), the only unblocked Noticeable-or-worse candidate this loop is F-015 — F-011 is blocked (criterion 0), and F-012 is Cosmetic severity, correctly ranked below F-015 on the Backlog Prioritization Pass's severity criterion. F-015 is Priority 1.

## Deepening Candidates
None this loop. `LoadGameEntriesAsync`'s manifest-parsing/game-identity-resolution block (`PrimaryWidget.xaml.cs:388-758`, mostly untouched by UI dispatch except at its start and end) remains the standing candidate Module extraction — re-examined independently this loop and reaching the same conclusion prior loops did: `CreateThumbnailAsync`'s `Dispatcher`-thread affinity and `GameEntry`'s WinRT-typed properties (`BitmapImage`, `StorageFolder`) mean a clean extraction requires first splitting `GameEntry` into a pure DTO plus a UI-bound wrapper — a larger, simultaneous redesign, not a smallest honest fix this loop's Simplify Pressure Test clears.

## Builder Notes

**Credit for a fix lands at the START of the next loop's Step 1, not inside the loop that made the fix.** This loop's own scorecard shows both halves of the pattern at once: `simplicity` moved up crediting loop 2's already-landed `UpdateSharedEntriesAsync` fix, while `concurrency` and `credibility` stayed flat even though this loop's own Step 2/3 is about to fix the exact thing keeping them capped — because Step 1 scores the source as it exists *before* that fix runs. A loop that scores its own not-yet-applied fix as if it already landed is scoring a plan, not source.

**Locking granularity should mirror the shape of the state it protects, not the shape of the nearest existing lock.** `StoreNameLookup` already had one `SemaphoreSlim` (for `ubisoftGameListCache`) sitting three lines from the three unlocked caches this loop fixed. The tempting shortcut was reusing that one gate for all four caches - it was already there, already proven, already imported. The correct fix used three *new*, dedicated gates instead, because the four caches hold unrelated per-game data with no shared invariant between them; one shared gate would have serialized independent stores' lookups against each other for no correctness reason, silently making a future concurrent caller slower than it needs to be.

**An Adversarial Pass re-test can find a cheaper fix than the original disposition considered, without that changing the disposition.** This loop's re-test of the `framework_idioms` residual found a fix ("just delete the dead TODO comment") that no prior loop's own reasoning had named — prior loops only ever considered "implement real suspend/resume," which is why the residual read as expensive. The cheaper fix is real and genuinely free, but it still doesn't win Priority 1 this loop: Simplify Pressure Test's Q5 (does the product improve *more than what's being declined*) is a comparative question, and a one-line dead-comment deletion loses that comparison to a Noticeable-severity fix every time. Finding a cheaper fix doesn't mean it's now worth doing *this loop* — Q1-Q4 and Q5 are answering different questions.

## Final Judge Narrative
Place, not win, yet. This loop credited a real prior improvement (F-013, now visibly closed in source) and made one of its own (F-015), closing the last of `StoreNameLookup`'s three unsynchronized caches without adding any new abstraction. Runtime ownership remains trustworthy for every traced write path in `PrimaryWidget.xaml.cs` (F-014 holds). Simplification helped this loop, twice over: `simplicity` moved to 9.5 crediting last loop's clean collapse, and this loop's own fix added zero ceremony - three dedicated gates matching an idiom the file already used one field over, not a new generic cache abstraction that would have added ceremony without collapsing anything real. Concurrency is not yet credited for this loop's own fix (per the established Step-1-scores-before-Step-3 convention) but the underlying hazard - three of `StoreNameLookup`'s four caches performing unsynchronized check-then-populate writes - is gone in current source as of this commit. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs` itself, and this loop's own fix sits in a file whose network-coupled methods are similarly outside this repo's test-scope boundary by the test file's own documented design, not by oversight. Future work still risks over-engineering if it tries to extract `PrimaryWidget`'s orchestration into a testable Module wholesale, or to unify `StoreNameLookup`'s three differently-shaped caches behind one generic abstraction; this loop re-examined both candidates independently and reached the same conclusions prior loops did, for source-backed reasons rather than by anchoring to the prior verdict.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) `simplicity` at 9.5 (UP from 8.0) rather than a smaller +0.5 step to 9.0 first - the jump skips straight to "9-anchor met, residual named" because no other simplicity concern survived the leaf-module duplication sweep beyond the now-accepted F-012 residual, but a stricter reading could ask whether one loop's single fix (closing the third of three multi-instance duplication classes) should earn the dimension's full remaining headroom in one step, versus a more conservative 9.0 pending one more loop's confirmation. (2) `framework_idioms`'s Adversarial Pass conclusion that deleting the dead TODO passes SPT Q1-Q4 but fails Q5 - a stricter reading could argue a zero-risk, zero-cost subtractive fix should never lose a Q5 comparison regardless of what else is queued, since "cost nothing, do it" doesn't actually compete for the same loop-slot as a real refactor; this loop judged bounded-blast-radius discipline (not touching a second file in the same commit) as the deciding factor, which is a process norm rather than a hard rule. (3) `data_flow` held at 7.5 rather than nudged up for this loop's own locking fix - the reasoning that locking changes concurrency-safety but not ambient-dependency *count* is a real distinction, but a less strict reading of the 9-anchor's "ambient-context dependencies... documented" language could credit synchronized ambient state as a lesser concern than unsynchronized ambient state even though the anchor text doesn't explicitly say so.

## Loop 3 Result
Added a dedicated `SemaphoreSlim` (`gogNameGate`, `epicNameGate`, `nameMatchGate`) to each of `StoreNameLookup`'s three previously-unsynchronized caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`) and wrapped `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/`FindGameByNameAsync`'s check-then-populate bodies in the standard double-checked-lock pattern (unlocked check, `WaitAsync`, re-check under the gate, fetch-and-populate, release in `finally`) already used one field over by `ubisoftGameListCache` via `AsyncLazyCache<T>`. Each cache keeps its own dedicated gate rather than sharing one, so the three stores' lookups stay independent of each other under a future concurrent caller. Full build (msbuild, exit 0) and full test suite (run-tests.ps1, 138 passed / 0 failed / 0 skipped) both re-run after the change, unchanged from before. No network call, its URL, its ordering, or its count changed anywhere; the gate is uncontended under the current sequential caller (`LoadGameEntriesAsync`'s per-entry loop still awaits one entry at a time), so `WaitAsync` returns immediately every time and every method's inner (locked) body is a verbatim copy of its prior unlocked body. Independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on the targeted finding and the diff only) returned verdict **approved** with all three checks (reality, honesty, regression) passed. Finding F-015 (stable_id F-015) is **resolved**. No unintended scorecard regression observed.
