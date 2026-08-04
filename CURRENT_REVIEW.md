### Loop Counter

Loop 5 of 10 (cap)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Independent re-derivation from current source (build-verified green both before and after this
loop's change) confirms `PrimaryWidget.xaml.cs` is unchanged at the concern-count level this loop:
still the churn-flagged leaky seam, still merging UI event handling and backup/restore
orchestration with no Interface boundary (F1/F-001, Serious). This loop closed the other half of
the split-cache-ownership residual loop 4 left behind: the Epic slice in
`Services/Stores/StoreNameLookup.cs`, mirroring loop 4's already-approved GOG fix exactly.
`StoreNameLookup` now fully owns the name-caching decision for all four of its caches (GOG, Epic,
name-match, Ubisoft) - the split-cache-ownership residual is fully closed. F1's core claim, F-003,
and F-004 are all untouched.

## Scorecard (1-10)

- **Architecture quality:** 5.5 | SAME | `PrimaryWidget.xaml.cs` is 2,278 LOC (down 13 lines from
  2,291, all from the Epic cache-check block shrinking to a short call-and-assign) - not a
  reduction in merged-concern count. F1's core claim (UI event handling, backup/restore
  orchestration still merged with no Interface boundary) is completely unaffected by this loop's
  fix, which targeted a narrower, already-extracted module (`StoreNameLookup`), not the god-class
  itself. No structural proof supports moving this dimension this loop.
- **State management and runtime ownership:** 6.5 | SAME | The Authority Map's Store-name-
  resolution-caches concern was already "Single and clear" before this loop (one writer per cache,
  no ambiguity - `epicNameCache` had exactly one writer, `PrimaryWidget.LoadGameEntriesAsync`,
  both before and after this loop's edit) - this loop closed a Locality/Interface-coherence gap for
  that concern (see Code simplicity below), not a write-authority correctness defect, so this
  dimension does not move. `isLibraryOperationRunning` and `AppliedArtworkStore.applied` remain
  unchanged.
- **Domain modeling:** 5.5 | SAME | Unaffected by this loop's change;
  `SteamGridDbClient.ParseOfficialCapsuleUrl` (`SteamGridDbClient.cs:144-199`) still hand-parsed
  outside the DTO's own deserialization, verified unchanged this loop.
- **Data flow and dependency design:** 6.0 | SAME | This loop's fix is entirely in-process
  reorganisation within an already-relocated module (`StoreNameLookup`, moved out of
  `PrimaryWidget` in loop 3) - no dependency moved, no new port introduced, and while
  `PrimaryWidget`'s direct call count into `Services/Stores` for the Epic path did drop (from three
  call sites - `epicNameCache` field access, `EpicLibrary.GetDisplayNameAsync`,
  `StoreNameLookup.GetEpicGameNameAsync` - to one, `GetOrFetchEpicNameAsync`), that is the same
  shape of evidence loop 4 deliberately credited to Code simplicity, not Data flow, for the
  identical GOG fix, to avoid double-counting one diff across two dimensions. Staying consistent
  with that established convention rather than re-litigating the judgment call.
- **Framework / platform best practices:** 6.0 | SAME | Unaffected by this loop's change. The two
  JSON idioms (`DataContractJsonSerializer` vs. ad hoc `Windows.Data.Json`) still coexist, verbatim.
- **Concurrency and runtime safety:** 5.5 | SAME | Unaffected by this loop's change. F-003's fully
  sequential per-game round-trips (`PrimaryWidget.xaml.cs:324-720`) remain open. `epicNameCache` is
  still a plain, non-thread-safe `Dictionary` after this loop - narrowing its visibility to
  `private` changed nothing about its thread-safety, same as `gogNameCache` last loop.
- **Code simplicity and clarity:** 7.0 | UP | `StoreNameLookup.GetOrFetchEpicNameAsync`
  (`StoreNameLookup.cs:203-222`, new this loop) now owns the full check-cache/fetch/populate
  decision for Epic names in one place, mirroring `GetOrFetchGogNameAsync`'s shape exactly and
  preserving the two-source fallback order (`EpicLibrary.GetDisplayNameAsync` tried first,
  `StoreNameLookup.GetEpicGameNameAsync` second). The call site in `PrimaryWidget.xaml.cs` shrank
  from a 20-line inline cache-check/fetch/populate block (`:606-627` before this loop) to a 7-line
  call-and-assign (`:606-612` now). `epicNameCache` moved from `internal` back to `private`
  (`StoreNameLookup.cs:28`) now that only `StoreNameLookup` itself touches it. This closes the last
  remaining instance of the shallow-module gap Finding #1's evidence has tracked since loop 3: all
  four `StoreNameLookup` caches (GOG, Epic, name-match, Ubisoft) now share the same
  Interface-owns-the-decision shape, with zero external field access into the module. Same
  magnitude of structural proof as loop 4's GOG move, applied to the matching residual instance -
  not a bigger jump: F1's dominant concerns (UI handling, backup/restore) remain untouched.
- **Test strategy and regression resistance:** 3.0 | SAME | No test project exists; standing user
  instruction prohibits adding one this run. Verified unchanged this loop. Named, non-backlog-item
  blocker, as recorded loops 1-4.
- **Overall implementation credibility:** 5.5 | SAME | `GetOrFetchEpicNameAsync` introduces no new
  swallow site - it has no `try`/`catch` of its own, relying on `EpicLibrary.GetDisplayNameAsync`
  and `StoreNameLookup.GetEpicGameNameAsync`'s existing internal catches, exactly matching
  `GetOrFetchGogNameAsync`'s shape. `GetGogGameNameAsync`'s own failure handling
  (`StoreNameLookup.cs:70-74`, swallows via `Debug.WriteLine` only) is untouched. Neither improves
  nor regresses this dimension.

## Authority Map

(Re-emitted this loop: the Store-name-resolution-caches concern's write-authority for the Epic
slice changed - the last of the four caches to close.)

- **Concern:** Library-operation exclusivity (`isLibraryOperationRunning`)
  - Owner: `PrimaryWidget` instance
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Observers / readers: `IsLibraryOperationBlocking`, all four header-button click handlers,
    `EditGameImage_Click`, `SearchGameImage_Click`, `RestoreBackup_Click`
  - Persistence seam: none (in-memory only)
  - Async mutation entry points: `TryBeginLibraryOperation`/`EndLibraryOperation`, called from
    every `*_Click` handler via a try/finally
  - Verdict: **Single and clear** - unaffected this loop.

- **Concern:** Applied-artwork record (`AppliedArtworkStore.applied`)
  - Owner: `AppliedArtworkStore` (static, `Services/Artwork/`)
  - Allowed writers: `UpdateAsync` (via `SetAsync`/`ClearAsync`), gated by `gate`
  - Observers / readers: `GetAsync`, also gated by `gate` (F-002, resolved loop 2)
  - Persistence seam: `applied-artwork.json` in `ApplicationData.Current.LocalFolder`
  - Async mutation entry points: `SetAsync` (from `ReplaceImageCoreAsync`), `ClearAsync` (from
    `RestoreBackupCoreAsync`)
  - Verdict: **Single and clear** - unaffected this loop.

- **Concern:** Store-name resolution caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`,
  `ubisoftGameLookupCache`)
  - Owner: `Services/Stores/StoreNameLookup`
  - Allowed writers: `gogNameCache` - `StoreNameLookup.GetOrFetchGogNameAsync` only, private field
    access (closed loop 4). `epicNameCache` - **now `StoreNameLookup.GetOrFetchEpicNameAsync` only,
    private field access (changed this loop; was `PrimaryWidget.LoadGameEntriesAsync` writing the
    field directly)**. `nameMatchCache`/`ubisoftGameLookupCache` -
    `StoreNameLookup.FindGameByNameAsync`/`.LoadUbisoftGameListAsync` only (unchanged, private).
  - Observers / readers: the same methods that write each cache (`TryGetValue` before writing)
  - Persistence seam: none (in-memory, per-process)
  - Async mutation entry points: `GetOrFetchGogNameAsync` and `GetOrFetchEpicNameAsync` (both now
    fully internal to `StoreNameLookup`); inside `StoreNameLookup`'s own methods (nameMatch/Ubisoft)
  - Verdict: **Single and clear** - all four caches are now fully owned in both write-authority and
    Locality by `StoreNameLookup` itself. No Interface-coherence gap remains for this concern; the
    split-cache-ownership residual Finding #1 has tracked since loop 3 is fully closed.

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design
  (`Services/SteamGridDB/ArtworkSource.cs:15-51`) makes "neither a platform-ID nor a game-ID"
  unrepresentable - a genuine smart-constructor, not decoration. Re-verified unchanged this loop.
- The official-artwork gate (`FindOfficialLookalikeAsync`, `PrimaryWidget.xaml.cs:1415-1486`) is a
  narrow, evidence-tuned veto whose code comments cite the specific regression case and slack
  margin that motivated it. Re-verified unchanged this loop (line numbers shifted by -13 from this
  loop's edit earlier in the store-name-resolution block; content unaffected).
- `StoreNameLookup.GetOrFetchEpicNameAsync` (new this loop) completes the fold: all four of the
  module's caches now share the identical check-cache/fetch/populate ownership shape, and zero
  external code reaches into a `StoreNameLookup` field directly any more - one consistent Interface
  for a future contributor to learn, not four ad hoc ones.

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling and backup/restore orchestration behind zero Interface boundary

**Why it matters** - The churn-flagged god-class (21 edits, still the largest file in the repo)
continues to bundle two structurally distinct concerns with no Module boundary between them, so a
change to either risks touching the other.

**What is wrong** - UI event handling (the `*_Click` handlers, grid/search panel management,
artwork download/replace flow) and backup/restore orchestration (`RestoreBackupCoreAsync`,
`RestoreBackupAsync`, the `RestoreBackupResult` enum) remain private members on one 2,278-line
`Page`-derived class with no Interface separating them. This loop closed the Epic slice of the
split-cache-ownership residual loop 4 left behind: `StoreNameLookup.GetOrFetchEpicNameAsync` now
owns the full check-cache/fetch/populate decision for Epic names, mirroring
`GetOrFetchGogNameAsync`, and `epicNameCache` is private again. All four `StoreNameLookup` caches
(GOG, Epic, name-match, Ubisoft) are now fully owned by the module itself - the split-cache-
ownership residual is fully closed. This fix does not touch the file's two dominant merged
concerns (UI event handling, backup/restore orchestration) at all - F1's core claim is unchanged
from loop 4.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:2137-2220` (`RestoreBackupCoreAsync` - backup/restore
  orchestration, still inline)
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:203-222` (new this loop -
  `GetOrFetchEpicNameAsync` owns Epic's cache decision; `epicNameCache` narrowed to `private` at
  `:28`)
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:722-858` (`RefreshButton_Click` through
  `RevertDefaultsButton_Click` - UI event handlers invoking orchestration methods directly on the
  same class, no Interface between them)

**Architectural test failed** - n/a - different category (ownership/coupling sprawl, not a
removable Seam or wrapper)

**Dependency category** - n/a (unaffected by this loop; the true-external piece of F1 was closed
loop 3)

**Leverage impact** - Every future backup/restore fix or UI change still touches the same file; a
maintainer touching either risks disturbing the other via shared `Dispatcher`/`StatusText`
plumbing.

**Locality impact** - Unaffected: a maintainer fixing a UI bug still reads through ~2,278 lines
including unrelated backup/restore logic.

**Metric signal** - `PrimaryWidget.xaml.cs`: 2,278 LOC (down 13 lines this loop, all from the Epic
cache block shrinking). `StoreNameLookup.cs`: 317 LOC (up 29 lines this loop).

**Why this weakens submission** - Ownership of the two remaining distinct concerns (UI event
handling, backup/restore orchestration) is still untraceable from any single Module; the file is
not meaningfully smaller this loop and is still well above the one-or-two-shallow-wrapper bar the
architecture-quality 7-anchor requires.

**Severity** - Serious deduction

**ADR conflicts** - none

**Minimal correction path** - The split-cache-ownership residual is now fully closed (GOG loop 4,
Epic this loop) - no further cache-folding steps remain in `StoreNameLookup`. The only remaining
F1 slice is backup/restore orchestration (`RestoreBackupCoreAsync`, `RestoreBackupAsync`,
`RestoreAllChangesAsync`, `RevertAllToDefaultAsync`, `FixLibraryAsync` and the
`RestoreBackupResult` enum): extracting it needs either a UI-update callback parameter (an
interface or delegate `PrimaryWidget` implements to receive status-text and per-row image updates)
or an accepted partial split (move the file-system logic out, leave the
`Dispatcher`/`StatusText`/`GameEntries` plumbing in `PrimaryWidget`) - a design decision, not a
mechanical move. Do not attempt a same-shape mechanical relocation without resolving this first.

**Blast radius** - Change (next loop): `PrimaryWidget.xaml.cs` (backup/restore orchestration
slice) OR a new file, once the UI-update design decision is resolved. Avoid: `Services/SteamGridDB/*`,
`Services/Artwork/ArtworkRanker.cs`, `Services/Stores/StoreNameLookup.cs` (cache-folding work is
complete for this module - no more residual instances to fold).

---

### Finding #2: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** - Load time scales linearly with library size and network latency on the
widget's primary open path - the one flow every user hits every time.

**What is wrong** - The `gameCache` `foreach` loop in `LoadGameEntriesAsync` awaits
`sgdbClient.GetGameByPlatformIdAsync` (and the GOG/Epic/Ubisoft name fallbacks, routed through
`StoreNameLookup` and `EpicLibrary`) one game at a time; nothing overlaps the independent per-game
network calls. Unaffected by this loop's fix, which changed how the Epic branch resolves a name
(now via `StoreNameLookup.GetOrFetchEpicNameAsync`), not whether it does so sequentially.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:324-720` (per-folder, per-entry `foreach`; awaits
  `GetGameByPlatformIdAsync` at `:577` and the store-name fallbacks at `:599,608,617,637`, with
  nothing overlapped - re-verified at current line numbers this loop after F1's Epic-slice edit
  shifted them)

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
well-understood (bounded concurrency) but crosses a real risk boundary: the store-name caches
(`gogNameCache`, `epicNameCache`, `nameMatchCache`, `ubisoftGameLookupCache`, all on
`StoreNameLookup`) are still not thread-safe and currently rely on this exact sequencing to stay
race-free.

**Severity** - Noticeable weakness

**ADR conflicts** - none

**Minimal correction path** - Bound concurrency (e.g. `SemaphoreSlim(4-8)` + `Task.WhenAll`) around
the per-entry body, and switch `StoreNameLookup`'s four cache fields to `ConcurrentDictionary`
*before* parallelizing - do not parallelize without that change, or the caches race.

**Blast radius** - Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync`),
`Services/Stores/StoreNameLookup.cs` (the four cache fields). Avoid: `Services/Artwork/*`,
`Services/SteamGridDB/*`.

---

### Finding #3: Hand-rolled double-checked-locking cache pattern duplicated between AppliedArtworkStore and EpicLibrary

**Why it matters** - The same ~25-line lazy-load-with-gate skeleton was written twice by hand
instead of once; a future third cache would make it three.

**What is wrong** - `AppliedArtworkStore.LoadAsync` (`AppliedArtworkStore.cs:95-147`) and
`EpicLibrary.LoadAsync` (`EpicLibrary.cs:67-89`) both implement: check-null, await `SemaphoreSlim`
gate, re-check-null, populate, release - identical structure, no shared helper. Unaffected by this
loop's fix, which targeted the Epic cache fold in a different file (`StoreNameLookup.cs`) and a
different pattern (plain `TryGetValue`/populate, no lock - matching the rest of that class).
Re-verified this loop: `StoreNameLookup.GetOrFetchEpicNameAsync` introduces no third copy of this
locked skeleton.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:95-147`
- `SteamGridDB.Xbox/Services/Stores/EpicLibrary.cs:67-89`

**Architectural test failed** - n/a - different category (leaf-module duplication, not a Seam)

**Dependency category** - n/a

**Leverage impact** - A shared lazy-cache primitive would pay for itself across at least these two
call sites.

**Locality impact** - Today a bug in the locking pattern must be fixed in two places; a shared
helper collapses that to one.

**Metric signal** - none

**Why this weakens submission** - Duplicate concurrency boilerplate is exactly the kind of
copy-paste-with-slight-variation the leaf-module duplication sweep looks for; it is real but
contained.

**Severity** - Noticeable weakness

**ADR conflicts** - none

**Minimal correction path** - Extract a small internal `AsyncLazy`-style helper
(check-lock-recheck-populate) that both call sites construct against. Do not add an interface or
DI - this is one concrete type serving two internal call sites, not a Seam.

**Blast radius** - Change: `Services/Artwork/AppliedArtworkStore.cs`,
`Services/Stores/EpicLibrary.cs`, one new small internal helper. Avoid: `PrimaryWidget.xaml.cs`,
`Services/Stores/StoreNameLookup.cs`.

## Simplification Check

- **Structurally necessary:** Folding Epic's check-cache/fetch/populate logic into
  `StoreNameLookup.GetOrFetchEpicNameAsync` - closes the last instance of the shallow-module
  residual Finding #1's evidence has tracked since loop 3 (Interface approx Implementation for
  `epicNameCache`, now Interface `>>` Implementation, matching GOG's shape from loop 4); the
  smallest honest fix is a targeted fold plus a `private` narrowing, not a new abstraction.
- **New seam justified:** false - no new Seam; `GetOrFetchEpicNameAsync` is a plain internal
  method added to the existing static class, not a port/interface.
- **Helpful simplification:** `PrimaryWidget.xaml.cs`'s Epic branch shrank from a 20-line inline
  cache-check/fetch/populate block to a 7-line call-and-assign; the check-cache/fetch/populate
  decision for Epic names now lives entirely inside `StoreNameLookup`, matching the shape
  `GetOrFetchGogNameAsync` already used, and preserving the exact two-source fallback order
  (`EpicLibrary.GetDisplayNameAsync` first, `StoreNameLookup.GetEpicGameNameAsync` second).
- **Should NOT be done:** Attempting backup/restore orchestration extraction in the same loop -
  its own evidence text says it "needs either a UI-update callback parameter or an accepted
  partial split," a design decision this codebase's lack of a test oracle makes too risky to
  attempt as a same-loop mechanical move (same judgment loop 4 reached; re-confirmed this loop by
  reading `RestoreBackupCoreAsync` directly - it calls `Dispatcher.RunAsync`, reads/writes
  `StatusText.Text`, and iterates `EntriesSharingImage`/`GameEntries`, all UI-bound state a
  non-UI class cannot reach without a passed-in abstraction).
- **Tests after fix:** No test project exists (standing instruction); `MSBuild` compile is the
  only regression oracle, verified green both before and after this loop's change (exit 0 both
  times). This is a behavior-preserving fold, not a Meta-Rule-4 risk-boundary crossing:
  `epicNameCache`'s visibility narrowed from `internal` to `private`, but its only external
  reader/writer (`PrimaryWidget.LoadGameEntriesAsync`) was rewritten in this same commit to go
  through `GetOrFetchEpicNameAsync` instead - grep-verified post-edit that no file outside
  `StoreNameLookup.cs` references `epicNameCache` any more, and the green build independently
  proves no orphaned caller was left behind (a private field read from another class is a compile
  error). `loop_result.risk_boundary_evidence` is `null`.

## Improvement Backlog

1. **Attempt backup/restore orchestration extraction (F1's only remaining slice)** - move
   `RestoreBackupCoreAsync`, `RestoreBackupAsync`, `RestoreAllChangesAsync`,
   `RevertAllToDefaultAsync`, `FixLibraryAsync`, and the `RestoreBackupResult` enum out of
   `PrimaryWidget.xaml.cs`. No cache-folding residual remains as a smaller substitute step - both
   `StoreNameLookup` slices (GOG loop 4, Epic this loop) are now closed. Requires resolving a
   design decision first: a UI-update callback parameter (interface/delegate for status-text and
   per-row image updates) or an accepted partial split (file-system logic moves out, UI plumbing
   stays). Re-run the Simplify Pressure Test on this fresh next loop rather than assuming the prior
   loops' caution still holds without re-checking.
   - Why it matters: F1 remains the largest Serious deduction on the board, and its only remaining
     smaller substitute step is now exhausted.
   - Score impact: Architecture quality +0.5, Code simplicity +0.5 once verified.
2. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups** (F-003), *after*
   switching the four store-name caches (on `StoreNameLookup`) to `ConcurrentDictionary` -
   structural, helpful.
   - Why it matters: removes load latency that scales linearly with library size on the widget's
     primary flow.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified.
3. **Extract a shared AsyncLazy-style cache helper for AppliedArtworkStore and EpicLibrary** (F-004)
   - simplification, helpful.
   - Why it matters: collapses two hand-copied lazy-load-with-gate skeletons into one owner before
     a third makes it three.
   - Score impact: Code simplicity +0.5 once verified.

## Deepening Candidates

- **Candidate Module:** none for `Services/Stores/StoreNameLookup` - the Deepening Candidate loop
  4 named for this module (fold Epic's cache) is fully closed this loop. All four caches now share
  the identical check-cache/fetch/populate ownership shape; there is no remaining shallow instance
  in this module to fold.
- **Candidate Module:** a small internal `AsyncLazy`-style helper shared by `AppliedArtworkStore`
  and `EpicLibrary` (see Finding #3 / F-004).
  - Source friction proven: Finding #3's evidence - both `AppliedArtworkStore.LoadAsync`
    (`AppliedArtworkStore.cs:95-147`) and `EpicLibrary.LoadAsync` (`EpicLibrary.cs:67-89`)
    hand-implement the identical check-null/gate/re-check-null/populate/release skeleton with no
    shared owner.
  - Why shallow/misplaced: each class's `LoadAsync` method exposes the full lazy-load mechanism as
    its own Implementation rather than delegating to a shared Interface - the same
    check-lock-recheck-populate logic is the Implementation in two places instead of the
    Implementation in one place behind two thin call sites.
  - Behaviour to move behind the deeper Interface: the check-null/`gate.WaitAsync()`/re-check-null/
    populate/`gate.Release()` skeleton, parameterised by a loader delegate
    (`Func<Task<TValue>>`) and the value type (`Dictionary<string,int>` for
    `AppliedArtworkStore`, `Dictionary<string,string>` for `EpicLibrary`).
  - Dependency category: `in-process`
  - Test surface after change: none (no test project; build-verified only, same as today)
  - Smallest first step: write the shared helper as an internal generic type taking a
    `SemaphoreSlim` and a loader delegate; convert `EpicLibrary.LoadAsync` first (it is the
    simpler of the two - no JSON-writeback path), then `AppliedArtworkStore.LoadAsync`
    (`UpdateAsync` also writes through the same gate and must keep doing so - do not fold the
    write path into the read-only helper).
  - What not to do: do not add an interface or DI for this - one concrete internal type serving
    two internal call sites is not a Seam per the Unified Seam Policy (no two-adapter justification,
    no policy/failure/platform isolation).

## Builder Notes

1. **Pattern:** When a prior loop names two alternatives ("fold the smaller residual instance, or
   attempt the risky thing") and the smaller instance is now closed, do not assume the risky
   option automatically becomes this loop's task - re-run the Simplify Pressure Test on it fresh,
   because the underlying blocker (a design decision the loop cannot make mechanically) does not
   resolve itself just because time has passed.
   - How to recognize: the backlog item's own text names a design decision still needed (a
     callback parameter, an accepted partial split) before the riskier option can be attempted
     safely, and no new source evidence has appeared that would resolve it.
   - Smallest coding rule: in a codebase with no test suite, when a "smaller instance" of a
     residual exists, close it before the harder core claim - but once every smaller instance is
     closed, say so plainly rather than picking a bigger, riskier move under the pressure to keep
     showing progress.
   - Stack example: C# - this loop closed Epic's cache fold (the last remaining smaller instance
     of F1's split-cache-ownership residual) rather than attempting backup/restore extraction,
     because the latter's own text still names an unresolved UI-update design decision with no new
     evidence this loop that would make it safer to attempt mechanically.

2. **Pattern:** A residual tracked across multiple loops (GOG in loop 4, Epic in loop 5) as
   parallel instances of the same shallow-module shape is fully closed once every named instance
   is folded - the Deepening Candidate for that specific module should then say so explicitly
   rather than being silently dropped or re-proposed without new evidence.
   - How to recognize: the Authority Map or Findings evidence names N parallel instances of one
     shape; each loop's fix closes one; once all N are closed, the "verdict" line changes from
     partial ("N of M closed") to complete ("no Interface-coherence gap remains").
   - Smallest coding rule: when the last instance of a tracked shape closes, update the Authority
     Map entry's verdict text to say so plainly, and explicitly retire the Deepening Candidate
     rather than leaving stale "still to go" language in the review.

3. **Pattern:** Churn concentrates where concerns are merged (unchanged from loops 1-4 - still
   live; this loop did not touch the churn-flagged file's concern count at all).
   - How to recognize: one file dominates the six-month edit count and is several times larger
     than everything else, even after partial extraction.
   - Smallest coding rule: when a file's edit count and size both dominate the repo, extract the
     concern that changed rather than adding to the pile - but a loop that instead closes the last
     safe residual elsewhere is not wasted, provided it is disclosed honestly as not moving the
     god-class dimension.

**Scorecard humility check** (Q9): three specific claims I am least confident about -
1. `code_simplicity` moving to 7.0 (rather than staying at 6.5, or jumping further) for closing
   the second and final half of a two-instance residual in a secondary-extraction module (not the
   god-class itself) - a stricter reviewer could argue the whole StoreNameLookup cluster earns at
   most one 0.5 credit total (already spent last loop on the GOG half), and this loop's Epic half
   is "finishing what was already credited," not new evidence for a second increment.
2. `data_flow` staying SAME despite `PrimaryWidget`'s direct call count into `Services/Stores` for
   the Epic path dropping from three call sites to one - I judged this belongs entirely to
   `code_simplicity` per loop 4's own precedent (same shape of evidence for the GOG fix), but a
   reviewer who does not treat that precedent as binding could credit `data_flow` independently for
   the reduced fan-out, since "each Module's inputs explicit, no back-channels" is literally a
   `data_flow` anchor criterion, not just a `code_simplicity` one.
3. The Deepening Candidates section's claim that "no remaining shallow instance" exists in
   `StoreNameLookup` - this is true for the four *name-caching* fields specifically, but I did not
   exhaustively re-audit every method in the file for other shallow-module shapes (e.g. whether
   `NormaliseGameName` or `FindGameByNameAsync` themselves have any residual issues); the claim is
   scoped narrowly to the cache-ownership shape Finding #1 has tracked, not a full-file clearance.

## Final Judge Narrative

Place, not win. This loop did not touch `PrimaryWidget.xaml.cs`'s two remaining merged concerns
(UI event handling, backup/restore orchestration) at all - F1 (Serious) is exactly as large as it
was at the end of loop 4. Instead, the loop closed the Epic slice of the split-cache-ownership
residual loop 4 left in `Services/Stores/StoreNameLookup.cs`, mirroring loop 4's GOG fix exactly
and completing the cluster: all four of that module's caches now share one consistent ownership
shape. The harder ask (backup/restore extraction) was re-examined against current source
(`RestoreBackupCoreAsync`'s direct `Dispatcher`/`StatusText`/`GameEntries` coupling) and still
fails the same test loop 4 applied - its own evidence text names an unresolved design decision, not
a mechanical move, and no new source evidence appeared this loop that would change that
assessment. `code_simplicity` moved up on real, narrow, build-verified structural proof, matching
the magnitude of loop 4's own move for the parallel half of the same residual; no other dimension
moved. Runtime ownership remains trustworthy for what has been resolved; concurrency is not yet
trustworthy (F-003 open, and both cache fields are exactly as non-thread-safe as before, only
smaller in scope now). Tests remain absent by standing instruction. Future work has run out of
smaller substitute steps for F1: next loop must either resolve the UI-update design decision and
attempt backup/restore extraction, or honestly pivot Priority 1 to a Noticeable-severity item
(F-003 or F-004) if that design decision still cannot be made without user input.

## Loop 5 Result

Closed the Epic slice of F1's split-cache-ownership residual: added
`StoreNameLookup.GetOrFetchEpicNameAsync` (`Services/Stores/StoreNameLookup.cs:203-222`), which
owns the full check-cache/fetch/populate decision for Epic names and preserves the exact
two-source fallback order (`EpicLibrary.GetDisplayNameAsync` tried first via `??`, then
`StoreNameLookup.GetEpicGameNameAsync`), and narrowed `epicNameCache` from `internal` back to
`private` (`StoreNameLookup.cs:28`) now that `StoreNameLookup` is its only reader/writer. Rewrote
the Epic branch in `PrimaryWidget.xaml.cs`'s `LoadGameEntriesAsync` (`:606-612`) from a 20-line
inline cache-check/fetch/populate block to a 7-line call-and-assign. `git diff --numstat`:
`PrimaryWidget.xaml.cs` (3 insertions, 16 deletions), `StoreNameLookup.cs` (33 insertions, 4
deletions).

**What proves the change is honest:** `MSBuild` (the sole regression oracle per Discovery - no
test project exists) passed clean both before and after the change (exit 0 both times, same
command as loops 1-4's baseline:
`"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal
/nologo`). The new `GetOrFetchEpicNameAsync` preserves the original inline logic's semantics
exactly, traced branch-by-branch against the pre-edit code: a cache hit with a non-empty cached
value returns it immediately with zero network calls (matching the original's `TryGetValue` +
non-empty check, `epicName = cached` -> `gameName = epicName`); a cache miss or empty cached value
tries `EpicLibrary.GetDisplayNameAsync` first, then `StoreNameLookup.GetEpicGameNameAsync` as a
`??` fallback, exactly the original's two-source order; the cache is written and `gameName` is set
only when that resolves to a non-empty name (matching the original's
`if (!string.IsNullOrEmpty(epicName))` write guard); when both sources return null/empty, the
value is not cached and `gameName` stays unset, exactly matching the original's silent fall-through
to the default "Unknown" (the original's structure had no `else` branch writing `gameName` on a
failed fetch, and neither does the new method). Grep-verified post-edit that `epicNameCache` is
referenced only inside `StoreNameLookup.cs`, confirming the `private` narrowing left no orphaned
external caller - independently confirmed by the green build (an orphaned private-field access
from another class would not compile). This changes where the cache-check/fetch/populate decision
lives, not the number of network calls per game, the fallback order, retry semantics, or any
selection/ranking behavior - confirmed by the independent implementation-reviewer pass
(`verdict: approved`; reality/honesty/regression all `passed`).

**Risk boundary evidence (Meta-Rule 4):** none - no isolation/`Sendable`/conditional-compilation/
lock-ordering boundary was crossed, and the visibility narrowing (`epicNameCache` `internal` ->
`private`) is not the cross-file-visibility hazard Meta-Rule 4 targets (moving code between files
and accidentally losing access): the field never moved files, and its one external caller
(`PrimaryWidget.LoadGameEntriesAsync`) was rewritten in this same commit to no longer need direct
access. `loop_result.risk_boundary_evidence` is `null`.

**Targeted finding status:** `carried_forward` - F1 (F-001) is evidenced by two remaining merged
concerns (UI event handling, backup/restore orchestration), neither of which this loop touched;
the Epic-cache residual this loop closed was cited as F1 *evidence*, not F1's core claim, so F1
stays open for the next loop.

**Unintended scorecard regression:** none observed. `code_simplicity` moved UP on structural proof;
no other dimension regressed.

## Loop 5 Implementation Review

`verdict: approved` - "The diff is a faithful, behavior-preserving relocation of Epic's
check-cache/fetch/populate logic into StoreNameLookup.GetOrFetchEpicNameAsync, mirroring the
already-approved GOG fold exactly, closing the specific epicNameCache residual evidence F1 cited
without overclaiming F1's core UI/backup-restore merge is resolved, and introduces no new
same-or-higher-severity finding." All three checks (`reality`, `honesty`, `regression`) `passed`;
`conditions: []`; `regressions: []`.
