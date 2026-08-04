### Discovery (first loop only)
See Loop 1 Discovery.

### Loop Counter

Loop 6 of 10 (cap)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Independent re-derivation from current source (build-verified green both before and after this
loop's change) confirms `PrimaryWidget.xaml.cs` is byte-for-byte unchanged this loop: still the
churn-flagged leaky seam, still merging UI event handling and backup/restore orchestration with no
Interface boundary (F1/F-001, Serious). This loop instead closed a different, long-standing
duplication finding (F-004, open and unattempted since loop 1): `AppliedArtworkStore` and
`EpicLibrary` no longer hand-implement their own copy of the check-lock-recheck-populate lazy-load
skeleton; both now call a shared internal `AsyncLazyCache<T>`. F1's core claim, and F-003
(concurrency, ruled out this run by an explicit operational constraint), are both untouched.

## Scorecard (1-10)

- **Architecture quality:** 5.5 | SAME | `PrimaryWidget.xaml.cs` is 2,278 LOC, byte-for-byte
  unchanged this loop (`git diff` touches only `Services/Artwork/AppliedArtworkStore.cs`,
  `Services/Stores/EpicLibrary.cs`, a new `Services/AsyncLazyCache.cs`, and the `.csproj`). F1's core
  claim (UI event handling, backup/restore orchestration still merged with no Interface boundary) is
  completely unaffected by this loop's fix, which targeted an unrelated duplication finding (F-004)
  in two leaf modules, not the god-class. No structural proof supports moving this dimension this
  loop.
- **State management and runtime ownership:** 6.5 | SAME | The Applied-artwork-record concern's
  write-authority is unchanged: `UpdateAsync` is still the only writer, `gate` still serializes every
  access path, `GetAsync`'s read-lock (F-002's loop-2 fix) is untouched. The refactor renamed the
  lazily-loaded field (`applied` -> `appliedCache`) and moved the check-lock-recheck housekeeping into
  a shared generic type, but the same physical `SemaphoreSlim` instance is still passed to that type
  and still acquired directly by `GetAsync`/`UpdateAsync` for their own critical sections (verified by
  reading the field declarations: `gate` is declared once, `appliedCache`'s constructor takes it as a
  parameter, `GetAsync`/`UpdateAsync` reference the same field by name). Ownership did not move, only
  Locality of the load-once mechanism - a Code simplicity concern, not a State management one.
  `isLibraryOperationRunning` remains unchanged.
- **Domain modeling:** 5.5 | SAME | Unaffected by this loop's change;
  `SteamGridDbClient.ParseOfficialCapsuleUrl` (`SteamGridDbClient.cs:144-199`) still hand-parsed
  outside the DTO's own deserialization, verified unchanged this loop (`grep -n ParseOfficialCapsuleUrl`
  shows the same two call sites as loop 5).
- **Data flow and dependency design:** 6.0 | SAME | This loop's fix is entirely in-process
  reorganisation of two already-leaf modules (`AppliedArtworkStore`, `EpicLibrary`) - no dependency
  moved, no new port introduced, and the shape of evidence (two internal call sites now sharing one
  generic type instead of two hand-copies) is the same kind loop 4/5 deliberately credited to Code
  simplicity rather than Data flow for the parallel `StoreNameLookup` folds, to avoid double-counting
  one diff across two dimensions. Staying consistent with that established convention.
- **Framework / platform best practices:** 6.0 | SAME | Unaffected by this loop's change. The two
  JSON idioms (`DataContractJsonSerializer` vs. ad hoc `Windows.Data.Json`) still coexist, verbatim.
- **Concurrency and runtime safety:** 5.5 | SAME | Unaffected by this loop's change. F-003's fully
  sequential per-game round-trips (`PrimaryWidget.xaml.cs:324-720`) remain open and are ruled out for
  this run by an explicit operational constraint (see Finding #2). `AsyncLazyCache<T>` preserves the
  exact same locking sequence its two predecessors used (verified branch-by-branch in Loop 6 Result);
  it neither improves nor regresses thread-safety.
- **Code simplicity and clarity:** 7.5 | UP | `AsyncLazyCache<T>` (`Services/AsyncLazyCache.cs`, new
  this loop, 61 lines including doc comments) now owns the check-null/gate/re-check-null/populate/
  release skeleton exactly once; `AppliedArtworkStore.cs` shrank from 182 to 165 lines (net -17,
  `LoadAsync`'s lazy-load wrapper deleted, its populate-only body kept as `LoadMapFromDiskAsync`) and
  `EpicLibrary.cs` shrank from 144 to 121 lines (net -23, `LoadAsync` and the `names` field deleted
  outright - `ReadManifestsAsync` now plugs directly into the shared helper). This closes F-004
  (Noticeable, open and unattempted since loop 1) completely: both classes now share the identical
  Interface-owns-the-decision shape `StoreNameLookup`'s four caches already had, with zero
  hand-duplicated lock code left anywhere in the repo. Same magnitude of structural proof as loop
  4/5's `StoreNameLookup` folds, applied to a distinct, previously-untouched duplication cluster - not
  a bigger jump, but not a re-spend of an already-credited residual either.
- **Test strategy and regression resistance:** 3.0 | SAME | No test project exists; standing user
  instruction prohibits adding one this run. Verified unchanged this loop. Named, non-backlog-item
  blocker, as recorded loops 1-5.
- **Overall implementation credibility:** 5.5 | SAME | `AsyncLazyCache<T>.GetOrLoadAsync` introduces
  no new swallow site - it has no `try`/`catch` of its own; both loaders it wraps
  (`LoadMapFromDiskAsync`, `ReadManifestsAsync`) already catch and log their own failures internally,
  exactly as `LoadAsync`'s callers relied on before. `GetGogGameNameAsync`'s own failure handling
  (`StoreNameLookup.cs:70-74`, swallows via `Debug.WriteLine` only) is untouched. Neither improves nor
  regresses this dimension.

## Authority Map

(Re-emitted this loop: the Applied-artwork-record concern's lazy-load mechanism changed, though its
write-authority did not.)

- **Concern:** Library-operation exclusivity (`isLibraryOperationRunning`)
  - Owner: `PrimaryWidget` instance
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Observers / readers: `IsLibraryOperationBlocking`, all four header-button click handlers,
    `EditGameImage_Click`, `SearchGameImage_Click`, `RestoreBackup_Click`
  - Persistence seam: none (in-memory only)
  - Async mutation entry points: `TryBeginLibraryOperation`/`EndLibraryOperation`, called from
    every `*_Click` handler via a try/finally
  - Verdict: **Single and clear** - unaffected this loop.

- **Concern:** Applied-artwork record (`AppliedArtworkStore.applied` / now `appliedCache`)
  - Owner: `AppliedArtworkStore` (static, `Services/Artwork/`)
  - Allowed writers: `UpdateAsync` (via `SetAsync`/`ClearAsync`), gated by `gate`. Unchanged this
    loop - the field that holds the lazily-loaded `Dictionary` was renamed and its load mechanism
    moved into `AsyncLazyCache<T>`, but `gate` is still the exact same `SemaphoreSlim` instance,
    passed into the new helper's constructor rather than duplicated, and `UpdateAsync` still acquires
    it directly for its own write section afterward.
  - Observers / readers: `GetAsync`, also gated by `gate` (F-002, resolved loop 2) - unaffected.
  - Persistence seam: `applied-artwork.json` in `ApplicationData.Current.LocalFolder`
  - Async mutation entry points: `SetAsync` (from `ReplaceImageCoreAsync`), `ClearAsync` (from
    `RestoreBackupCoreAsync`)
  - Verdict: **Single and clear** - unaffected in substance this loop; only the lazy-load
    Locality changed.

- **Concern:** Store-name resolution caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`,
  `ubisoftGameLookupCache`, all on `Services/Stores/StoreNameLookup`)
  - Owner: `Services/Stores/StoreNameLookup` - unaffected this loop. `StoreNameLookup.cs` is
    byte-for-byte unchanged (`git diff` touches no file under `Services/Stores/StoreNameLookup.cs`).
  - Verdict: **Single and clear** - unaffected this loop. (Note: this concern is distinct from
    `EpicLibrary`'s own manifest cache, touched this loop - `EpicLibrary`'s cache was never tracked
    here because it was never ambiguous: one loader, one lazy-load lock, no second writer, before or
    after this loop's change.)

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design
  (`Services/SteamGridDB/ArtworkSource.cs:15-51`) makes "neither a platform-ID nor a game-ID"
  unrepresentable - a genuine smart-constructor, not decoration. Re-verified unchanged this loop.
- The official-artwork gate (`FindOfficialLookalikeAsync`, `PrimaryWidget.xaml.cs:1415-1486`) is a
  narrow, evidence-tuned veto whose code comments cite the specific regression case and slack margin
  that motivated it. Re-verified unchanged this loop (byte-for-byte, since `PrimaryWidget.xaml.cs`
  was not touched).
- `AsyncLazyCache<T>` (new this loop) takes the caller's own lock as a constructor argument instead
  of owning a private one - the one design choice that keeps `AppliedArtworkStore`'s F-002 fix
  (`GetAsync`/`UpdateAsync` sharing one lock with the lazy-load) intact through the refactor. A
  generic helper that instead created its own lock internally would have silently reintroduced the
  read/write race loop 2 closed; this one does not, because it was designed against that constraint
  from the start (matching loop 5's own "smallest first step" plan for this exact residual).

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling and backup/restore orchestration behind zero Interface boundary

**Why it matters** - The churn-flagged god-class (21 edits, still the largest file in the repo)
continues to bundle two structurally distinct concerns with no Module boundary between them, so a
change to either risks touching the other.

**What is wrong** - UI event handling (the `*_Click` handlers, grid/search panel management,
artwork download/replace flow) and backup/restore orchestration (`RestoreBackupCoreAsync`,
`RestoreBackupAsync`, `RestoreAllChangesAsync`, `RevertAllToDefaultAsync`, `FixLibraryAsync`, the
`RestoreBackupResult` enum) remain private members on one 2,278-line `Page`-derived class with no
Interface separating them - `PrimaryWidget.xaml.cs` is byte-for-byte unchanged this loop (this
loop's fix, F-004, touched only `Services/Artwork/AppliedArtworkStore.cs` and
`Services/Stores/EpicLibrary.cs`). Re-read `RestoreBackupCoreAsync` directly this loop: it
interleaves pure file operations (locate the backup `StorageFile`, delete a stale `.new`
customisation, rename the backup to become the main image, clear `AppliedArtworkStore`) with
UI-bound work (`Dispatcher.RunAsync` calls writing `StatusText.Text`, a `foreach` over
`EntriesSharingImage(game)` mutating `GameEntry.Image`/`ImageFileName`/`HasBackup`, and a
`BitmapImage` built by `CreateThumbnailAsync`) at every step, confirming loop 4 and loop 5's read.
But the two remedies loop 5 named are not equally blocked: a UI-update callback/delegate Interface
invents a new Seam for a single caller with no tests to justify it, which the Unified Seam Policy
would likely reject outright - that alternative is genuinely a product decision. Extracting only the
pure file operations into a `Services/Artwork` helper returning a plain success/failure signal,
while `PrimaryWidget` keeps the Dispatcher calls, `StatusText`, `GameEntries` mutation and
`BitmapImage` construction, invents no new Seam and mirrors the pattern already used for
`StoreNameLookup`, `ArtworkRanker` and `EpicLibrary` - that alternative is not blocked on a decision,
only unattempted.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:2137-2220` (`RestoreBackupCoreAsync` - re-read this loop,
  byte-for-byte unchanged)
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:722-858` (`RefreshButton_Click` through
  `RevertDefaultsButton_Click` - UI event handlers invoking orchestration methods directly,
  unchanged)
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:2186-2187` (`CreateThumbnailAsync` call inside
  `RestoreBackupCoreAsync` - the one UI-affine step inside the otherwise-pure file operations, must
  stay caller-side or be handled carefully in any partial split)

**Architectural test failed** - n/a - different category (ownership/coupling sprawl, not a
removable Seam or wrapper)

**Dependency category** - n/a (unaffected by this loop)

**Leverage impact** - Every future backup/restore fix or UI change still touches the same file; a
maintainer touching either risks disturbing the other via shared `Dispatcher`/`StatusText`
plumbing.

**Locality impact** - Unaffected: a maintainer fixing a UI bug still reads through ~2,278 lines
including unrelated backup/restore logic.

**Metric signal** - `PrimaryWidget.xaml.cs`: 2,278 LOC, unchanged this loop (0 lines touched).

**Why this weakens submission** - Ownership of the two remaining distinct concerns (UI event
handling, backup/restore orchestration) is still untraceable from any single Module; the file is
not smaller this loop (0 lines changed) and is still well above the one-or-two-shallow-wrapper bar
the architecture-quality 7-anchor requires.

**Severity** - Serious deduction

**ADR conflicts** - none

**Minimal correction path** - F1's remaining scope is backup/restore orchestration. Two candidate
paths remain, not equally ready: (a) a UI-update callback/delegate Interface - still a genuine
product/ownership decision (single caller, no tests, likely Unified-Seam-Policy-rejected without a
second real Adapter); do not attempt without user input. (b) a partial split of
`RestoreBackupCoreAsync`'s pure file operations (locate backup / delete stale `.new` / rename /
clear `AppliedArtworkStore`) into a new `Services/Artwork` helper returning a plain result, leaving
`CreateThumbnailAsync`, every `Dispatcher.RunAsync` call, `StatusText`, and `GameEntries` mutation in
`PrimaryWidget` - no new Seam, same shape as the already-approved `StoreNameLookup`/`ArtworkRanker`/
`EpicLibrary` extractions. (b) is next loop's honest first attempt; re-run the Simplify Pressure
Test on it fresh before committing, since it was not fully vetted this loop.

**Blast radius** - Change (next loop, path b): `PrimaryWidget.xaml.cs` (`RestoreBackupCoreAsync`'s
file-operation lines only) and a new `Services/Artwork` helper file. Avoid:
`Services/SteamGridDB/*`, `Services/Artwork/ArtworkRanker.cs`, `Services/Stores/*`,
`Services/AsyncLazyCache.cs` (all cache-folding and dedup work in those files is complete).

---

### Finding #2: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** - Load time scales linearly with library size and network latency on the
widget's primary open path - the one flow every user hits every time.

**What is wrong** - The `gameCache` `foreach` loop in `LoadGameEntriesAsync` awaits
`sgdbClient.GetGameByPlatformIdAsync` (and the GOG/Epic/Ubisoft name fallbacks, routed through
`StoreNameLookup` and `EpicLibrary`) one game at a time; nothing overlaps the independent per-game
network calls. Unaffected by this loop's fix (F-004), which touched only `AppliedArtworkStore.cs`
and `EpicLibrary.cs`'s lazy-load skeletons, not `LoadGameEntriesAsync`'s sequencing - re-verified
this loop that `PrimaryWidget.xaml.cs` is byte-for-byte unchanged. This run's own operating
constraints additionally rule out attempting this finding at all for the duration of this run:
parallelising these per-game round-trips would change the observable request count, order, and
timing against third-party APIs (GOG, a community database, Ubisoft's GitHub-hosted list), which
this run has been instructed not to do blind, absent a behavioural oracle to grade the result
against.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:324-720` (per-folder, per-entry `foreach`; awaits
  `GetGameByPlatformIdAsync` at `:577` and the store-name fallbacks at `:599,608,617,637`, with
  nothing overlapped - re-verified at the same line numbers this loop since `PrimaryWidget.xaml.cs`
  was not touched)

**Architectural test failed** - n/a - different category (D2, structural waste per
`lens-efficiency.md`, not a Seam)

**Dependency category** - `true-external`

**Leverage impact** - There is only one call site (the load loop); a future second caller of the
same pattern would inherit the same linear cost with no leverage from batching, since none exists.

**Locality impact** - The fix is local to `LoadGameEntriesAsync`'s loop body and `StoreNameLookup`'s
cache field declarations; it does not need to spread to callers.

**Metric signal** - One HTTP round-trip per game per store lookup; a 100-game library issues 100+
sequential requests with no overlap (D2, `lens-efficiency.md`).

**Why this weakens submission** - Structural waste on the widget's primary hot path. The fix is
well-understood (bounded concurrency) but is out of scope for this run by explicit instruction, not
by mechanical difficulty.

**Severity** - Noticeable weakness

**ADR conflicts** - none

**Minimal correction path** - Blocked for the duration of this run by an explicit operational
constraint (must not change per-game network-call count, order, or behavior against third-party
APIs without a behavioral oracle) - not by a mechanical difficulty. If that constraint is lifted in
a future run: bound concurrency (e.g. `SemaphoreSlim(4-8)` + `Task.WhenAll`) around the per-entry
body, and switch `StoreNameLookup`'s four cache fields to `ConcurrentDictionary` before
parallelizing. `AsyncLazyCache<T>` (this loop's F-004 fix) is already safe under concurrent first
callers - the check-lock-recheck sequence it wraps was written for exactly that case - so it needs
no further change before that day comes.

**Blast radius** - Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync`),
`Services/Stores/StoreNameLookup.cs` (the four cache fields). Avoid: `Services/Artwork/*`,
`Services/SteamGridDB/*`.

---

### Finding #3: Hand-rolled double-checked-locking cache pattern duplicated between AppliedArtworkStore and EpicLibrary

**Why it matters** - The same lazy-load-with-gate skeleton was written twice by hand instead of
once; a future third cache would have made it three.

**What is wrong** - At Step 1 inspection time (source identical to loop 5's committed state),
`AppliedArtworkStore.LoadAsync` (`AppliedArtworkStore.cs:95-147`, pre-fix) and
`EpicLibrary.LoadAsync` (`EpicLibrary.cs:67-89`, pre-fix) both implemented: check-null, await
`SemaphoreSlim` gate, re-check-null, populate, release - identical structure, no shared helper,
open and unattempted since loop 1. **This loop fixes it - see Loop 6 Result.**

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:95-147` (pre-fix)
- `SteamGridDB.Xbox/Services/Stores/EpicLibrary.cs:67-89` (pre-fix)

**Architectural test failed** - n/a - different category (leaf-module duplication, not a Seam)

**Dependency category** - n/a

**Leverage impact** - A shared lazy-cache primitive pays for itself across at least these two call
sites.

**Locality impact** - Previously a bug in the locking pattern had to be fixed in two places; a
shared helper collapses that to one.

**Metric signal** - none

**Why this weakens submission** - Duplicate concurrency boilerplate is exactly the kind of
copy-paste-with-slight-variation the leaf-module duplication sweep looks for; a bug fixed in one
copy could easily be missed in the other.

**Severity** - Noticeable weakness

**ADR conflicts** - none

**Minimal correction path** - Extract a small internal `AsyncLazy`-style helper
(check-lock-recheck-populate) that both call sites construct against, taking the caller's own
`SemaphoreSlim` rather than owning one. Do not add an interface or DI. This loop's fix - see Loop 6
Result.

**Blast radius** - Change: `Services/Artwork/AppliedArtworkStore.cs`,
`Services/Stores/EpicLibrary.cs`, `Services/AsyncLazyCache.cs` (new). Avoid: `PrimaryWidget.xaml.cs`,
`Services/Stores/StoreNameLookup.cs`.

## Simplification Check

- **Structurally necessary:** Replacing `AppliedArtworkStore.LoadAsync` and `EpicLibrary.LoadAsync`'s
  hand-duplicated check-null/gate/re-check-null/populate/release skeleton with one shared
  `AsyncLazyCache<T>` - closes F-004 (open every loop since loop 1, never previously attempted), a
  genuine leaf-module duplication distinct from the `StoreNameLookup` residual loops 4-5 already
  closed; the smallest honest fix is a generic helper taking the caller's own `SemaphoreSlim` (not a
  new one), matching loop 5's own vetted "smallest first step" plan verbatim.
- **New seam justified:** false - `AsyncLazyCache<T>` is an internal sealed generic type constructed
  by two internal call sites, not a Seam/interface/DI boundary; no two-adapter claim is made,
  matching the Deepening Candidate's own "what not to do."
- **Helpful simplification:** `AppliedArtworkStore.cs` shrank 182 -> 165 lines (net -17);
  `EpicLibrary.cs` shrank 144 -> 121 lines (net -23); the two hand-copied lock skeletons collapsed
  into one 61-line generic helper (`Services/AsyncLazyCache.cs`) that both classes now share, closing
  F-004 completely - no third hand-copied instance was ever introduced, and now none can be without
  also reusing the shared type.
- **Should NOT be done:** Attempting F1's callback-interface alternative in the same loop - it would
  invent a new Seam for a single caller with zero tests to benefit from it, which the Unified Seam
  Policy would likely reject outright (no second real Adapter, no policy/failure/platform-isolation
  justification). Also should not attempt F-003 (bounded concurrency) this run - the operational
  constraint against changing observable per-game network-call behavior blind rules it out
  regardless of `AsyncLazyCache<T>`'s incidental thread-safety compatibility.
- **Tests after fix:** No test project exists (standing instruction); `MSBuild` compile is the only
  regression oracle, verified green both before and after this loop's change (exit 0 both times).
  `AsyncLazyCache<T>.GetOrLoadAsync` is the single generic implementation of the pattern both
  `AppliedArtworkStore` and `EpicLibrary` now call instead of hand-writing their own copy - a future
  third cache-consumer inherits a correct implementation instead of a third hand-copied one.

## Improvement Backlog

1. **Attempt the partial split of RestoreBackupCoreAsync's pure file operations (F1's non-blocked
   slice)** - extract locate-backup/delete-stale-`.new`/rename/clear-`AppliedArtworkStore` into a new
   `Services/Artwork` helper, leaving `CreateThumbnailAsync`, `Dispatcher.RunAsync`, `StatusText`,
   and `GameEntries` mutation in `PrimaryWidget`. No cache-folding residual remains as a smaller
   substitute step - F-004 (this loop) was the last one. Re-run the Simplify Pressure Test fresh next
   loop; this was not fully vetted this loop, only identified as not-decision-blocked.
   - Why it matters: F1 remains the largest Serious deduction on the board; its callback-interface
     alternative is still genuinely blocked on a product decision, but this file-operation slice is
     not.
   - Score impact: Code simplicity +0.5 if verified and the file shrinks measurably; Architecture
     quality unlikely to move (a small slice, not a full resolution of F1's merged-concerns claim).
2. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups** (F-003) - blocked for
   the duration of this run by an explicit operational constraint (must not change observable
   per-game network-call behavior against third-party APIs without a behavioral oracle). Carried
   forward as a reminder, not as an actionable item under current instructions.
   - Why it matters: removes load latency that scales linearly with library size on the widget's
     primary flow, whenever this run's constraint is lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

None this loop. The `AsyncLazyCache<T>` deepening candidate loop 5 named for `AppliedArtworkStore` +
`EpicLibrary` is fully closed (see Finding #3 / Loop 6 Result) - explicitly retired, not silently
dropped. No new shallow-module instance surfaced within the files read this loop
(`AppliedArtworkStore.cs`, `EpicLibrary.cs`, `StoreNameLookup.cs` unchanged, `PrimaryWidget.xaml.cs`
unchanged).

## Builder Notes

1. **Pattern:** A Noticeable-severity finding can sit unattempted in the backlog for many loops
   (F-004: first seen loop 1, still Priority 3 every loop through loop 5) without being cosmetic -
   it was simply lower priority than a Serious god-class finding and its own smaller substitute
   steps. Once the Serious item's smaller substitute steps are genuinely exhausted and its remaining
   slice is genuinely blocked (decision-bound or constraint-bound, re-verified fresh, not just
   carried-forward text), advancing to the next Noticeable item is the honest move, not a sign the
   run has run dry.
   - How to recognize: a Noticeable-severity finding with a long, unbroken "open" occurrence history
     and a fully-specified `minimal_correction_path` that nothing in several loops ever exercised.
   - Smallest coding rule: when Priority 1 and 2 are both genuinely blocked this loop, advance to
     Priority 3 rather than manufacturing a new finding or halting early - but say so plainly, and
     keep tracking the blocked items so they surface again the moment their blocker lifts.
   - Stack example: C# - `AsyncLazyCache<T>` took the exact "smallest first step" loop 5's Deepening
     Candidate already specified: an external `SemaphoreSlim` passed in, not owned, so
     `AppliedArtworkStore`'s `GetAsync`/`UpdateAsync` could keep using the same lock instance the
     helper's lazy-load used.

2. **Pattern:** A finding's two named remedies are not always equally blocked - re-reading the
   actual coupling (not just the prior loop's summary of it) can split "needs a design decision"
   into one path that genuinely does and one that doesn't.
   - How to recognize: the `minimal_correction_path` lists two alternatives in an either/or with a
     single blanket justification ("a design decision, not a mechanical move") - re-read the
     concrete code each alternative touches; one may invent a new public Seam (genuinely a decision)
     while the other only relocates private, already-precedented logic (not).
   - Smallest coding rule: before carrying forward a "blocked on a decision" finding unchanged,
     re-derive which specific parts of the fix require a decision and which don't - only the former
     stays blocked.
   - Stack example: C# - `RestoreBackupCoreAsync`'s file rename/delete/clear-record calls need no
     new interface to move; its `BitmapImage`/`Dispatcher`/`StatusText`/`GameEntries` calls do need
     to stay put or be threaded through a callback, which is the part that is genuinely a decision.

3. **Pattern:** Churn concentrates where concerns are merged (unchanged from loops 1-5 - still live;
   this loop did not touch the churn-flagged file's concern count at all).
   - How to recognize: one file dominates the six-month edit count and is several times larger than
     everything else, even after partial extraction elsewhere.
   - Smallest coding rule: when a file's edit count and size both dominate the repo, extract the
     concern that changed rather than adding to the pile - but a loop that instead closes a real,
     long-open residual elsewhere is not wasted, provided it is disclosed plainly as not moving the
     god-class dimension, so several loops of small wins don't quietly read as god-class progress.

**Scorecard humility check** (Q9): three specific claims I am least confident about -
1. `code_simplicity` moving to 7.5 (rather than staying at 7.0, or jumping to 8.0) for fully closing
   a two-site duplication cluster in one loop - a stricter reviewer could argue closing an entire
   cluster in one loop deserves +1.0 (double the per-half increment loops 4/5 used for the *other*
   two-site cluster split across two loops), or conversely that a ~40-line dedup offset by a new
   61-line (mostly-comment) helper file is worth less than +0.5 once the new file's line count is
   weighed against the two shrunk files.
2. Whether F1's newly-split guidance ("the partial split needs no design decision, the callback
   does") survives a fresh Simplify Pressure Test next loop without complications I have not fully
   traced - I did not verify whether `CreateThumbnailAsync`'s `BitmapImage` construction genuinely
   requires UWP's UI thread/`Dispatcher` context or merely conventionally runs there in this
   codebase, so the boundary I have drawn between "pure" and "UI-bound" work inside
   `RestoreBackupCoreAsync` may be less clean than stated.
3. `state_management` staying SAME at 6.5 rather than moving up slightly - the Applied-artwork-record
   Authority Map entry's load mechanism changed from a private method to a call through a shared
   generic type; I judged this as pure internal Locality, not an ownership-clarity improvement, but a
   reviewer who credits "one Module now calls through one shared Interface instead of hand-rolling
   its own" as itself a `state_management` anchor criterion (rather than purely a `code_simplicity`
   one) could read it differently.

## Final Judge Narrative

Place, not win. This loop did not touch `PrimaryWidget.xaml.cs` at all - F1 (Serious) is exactly as
large as it was at the end of loop 5, byte-for-byte. Instead, the loop closed F-004, a real,
long-standing (open since loop 1, never previously attempted) duplication finding in
`AppliedArtworkStore` and `EpicLibrary`: both classes' hand-copied lazy-load-with-lock skeletons now
share one generic `AsyncLazyCache<T>`, built specifically to preserve the exact lock instance and
sequencing loop 2's F-002 fix depends on. F1 was re-examined fresh against current source
(`RestoreBackupCoreAsync` read directly this loop) and split into two remedies with different
blockers: the callback-interface alternative remains a genuine product decision this loop correctly
declines to make unilaterally; a partial file-operations split does not require that decision and is
next loop's honest first attempt, not yet attempted or fully vetted. F-003 (concurrency) stays
explicitly out of scope for this entire run by operational instruction, not by mechanical
difficulty - carried forward as a reminder rather than a live target. `code_simplicity` moved up on
real, narrow, build-verified structural proof; no other dimension moved, and none regressed. Runtime
ownership remains trustworthy for what has been resolved; concurrency is not yet trustworthy (F-003
open, unaddressable this run). Tests remain absent by standing instruction. Future work has one
honest path left for F1 (the partial file-operations split) before that finding, too, would need to
wait on a product decision or a lifted constraint - next loop should attempt it and report plainly
whether it survives the Simplify Pressure Test.

## Loop 6 Result

Wrote a shared internal `AsyncLazyCache<T>` helper (`Services/AsyncLazyCache.cs`, new) implementing
the check-null/gate/re-check-null/populate/release skeleton once, and pointed both
`AppliedArtworkStore` (`Services/Artwork/AppliedArtworkStore.cs`) and `EpicLibrary`
(`Services/Stores/EpicLibrary.cs`) at it in place of their own hand-written copies, closing F-004.
`AppliedArtworkStore.LoadAsync`'s populate-only logic became `LoadMapFromDiskAsync`; `EpicLibrary`'s
`ReadManifestsAsync` (already a separate populate method) plugs straight in. `AppliedArtworkStore`'s
`gate` field was reordered above the new `appliedCache` field so the same `SemaphoreSlim` instance
`GetAsync`/`UpdateAsync` already used continues to serialize the lazy load - `GetOrLoadAsync` takes
the caller's `gate` as a constructor argument rather than owning one, per loop 5's own vetted plan.
Added the new file to `SteamGridDB.Xbox.csproj` (`<Compile Include="Services\AsyncLazyCache.cs" />`,
required for this UWP project with no globbing). `git diff --numstat`: `AppliedArtworkStore.cs` (27
insertions, 44 deletions), `EpicLibrary.cs` (4 insertions, 27 deletions), `SteamGridDB.Xbox.csproj`
(1 insertion), plus the new 61-line `AsyncLazyCache.cs`.

**What proves the change is honest:** `MSBuild` (the sole regression oracle per Discovery - no test
project exists) passed clean both before and after the change (exit 0 both times, same command as
loops 1-5's baseline: `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/
MSBuild.exe" SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never
/v:minimal /nologo`). `AsyncLazyCache<T>.GetOrLoadAsync` preserves both original methods' semantics
exactly, traced branch-by-branch: a non-null cached value returns immediately with zero work
(matching both originals' fast-path `if (value != null) return`); otherwise the caller takes `gate`,
re-checks for null (in case a concurrent caller already populated it while this one waited - matches
both originals' double-checked pattern exactly), and on a genuine miss calls the loader exactly once
and caches the result before releasing the lock (matching both originals' `populate; store; release`
sequence). For `AppliedArtworkStore`: `GetAsync` and `UpdateAsync` still separately acquire/release
`gate` for their own critical sections after the lazy-load returns, in the same two-step sequence as
before (verified by reading both methods post-edit: `appliedCache.GetOrLoadAsync()` fully completes,
then a fresh `await gate.WaitAsync()` follows, exactly mirroring the pre-edit `LoadAsync()` then
`gate.WaitAsync()` sequence) - the F-002 read/write lock discipline loop 2 established is untouched.
For `EpicLibrary`: `gate` is now used solely by `nameCache`, exactly as it was solely used by the old
`LoadAsync` before (no other method in the file ever referenced it). Grep-verified post-edit that
`applied`/`names` fields and both old `LoadAsync` methods no longer exist anywhere in the repository,
and that `appliedCache`/`nameCache` are the only readers of their respective `gate` fields at the
lazy-load site - independently confirmed by the green build (an orphaned reference to a deleted
private field or method would not compile). This changes only where the lazy-load's lock/check/
populate housekeeping lives, not the number of network calls or file reads per game, the fallback
order, retry semantics, or any selection/ranking behavior - confirmed by the independent
implementation-reviewer pass (see Loop 6 Implementation Review below).

**Risk boundary evidence (Meta-Rule 4):** this fix crosses a `lock_ordering` boundary (a lock
acquire/release sequence moved into a new shared type). `{"boundary_kind": "lock_ordering",
"verification": "reasoning_only", "detail": "AsyncLazyCache<T>.GetOrLoadAsync takes the caller's own
SemaphoreSlim as a constructor argument rather than owning a private one - AppliedArtworkStore's
gate field (unchanged instance) is passed in, and GetAsync/UpdateAsync continue to acquire/release
that exact same gate directly for their own read and write critical sections exactly as before; only
the lazy-load's acquire/check/populate/release sequence moved into the shared type, byte-identical to
the original AppliedArtworkStore.LoadAsync/EpicLibrary.LoadAsync bodies. Grep-verified only one
SemaphoreSlim field exists per class post-edit and it flows unchanged into the helper's constructor.
No thread-sanitizer or concurrency stress-test tooling exists for this C#/UWP stack in this
environment (no test project, per standing instruction); a green MSBuild compile confirms only that
the relocation type-checks, not that the lock sequence is unchanged - verified instead by direct
inspection of both call sites and the new type.", "mechanically_testable": false}` This is the same
evidence shape loop 2 used for the analogous `lock_ordering` crossing in this same file (F-002's
fix) - the smallest honest evidence available for this stack in this environment.

**Targeted finding status:** `resolved` - F-004 as evidenced (the hand-duplicated check-lock-recheck
skeleton in `AppliedArtworkStore.LoadAsync` and `EpicLibrary.LoadAsync`) is gone from current source;
both classes now call the same shared `AsyncLazyCache<T>` instead.

**Unintended scorecard regression:** none observed. `code_simplicity` moved UP on structural proof;
no other dimension regressed.

## Loop 6 Implementation Review

`verdict: approved` - "The diff faithfully relocates the identical check-lock-recheck-populate
skeleton from AppliedArtworkStore.LoadAsync and EpicLibrary.LoadAsync into a shared
AsyncLazyCache<T>, preserving AppliedArtworkStore's F-002 lock discipline (GetAsync/UpdateAsync still
acquire the same gate instance directly), introduces no new Seam, and does not overclaim F1's
core UI/backup-restore merge is resolved - F-004 is genuinely closed and no same-or-higher-severity
regression is introduced." All three checks (`reality`, `honesty`, `regression`) `passed`;
`conditions: []`; `regressions: []`.
