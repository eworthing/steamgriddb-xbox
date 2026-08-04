### Loop Counter

Loop 4 of 10 (cap)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Independent re-derivation from current source (build-verified green both before and after this
loop's change) confirms `PrimaryWidget.xaml.cs` is unchanged at the concern-count level this loop:
still the churn-flagged leaky seam, still merging UI event handling and backup/restore
orchestration with no Interface boundary (F1/F-001, Serious). This loop instead closed the smaller,
lower-risk residual the backlog itself named as the fallback: the GOG slice of the split-cache-
ownership gap loop 3 left in `Services/Stores/StoreNameLookup.cs`. `StoreNameLookup` now fully owns
the GOG name-caching decision (was previously split between that file and `PrimaryWidget.xaml.cs`);
the equivalent Epic slice remains open. F1's core claim, F-003, and F-004 are all untouched.

## Scorecard (1-10)

- **Architecture quality:** 5.5 | SAME | `PrimaryWidget.xaml.cs` is 2,291 LOC (down 8 lines from
  2,299, all from the GOG cache-check block shrinking to a 4-line call) — not a reduction in merged-
  concern count. F1's core claim (UI event handling, backup/restore orchestration still merged with
  no Interface boundary) is completely unaffected by this loop's fix, which targeted a narrower,
  already-extracted module (`StoreNameLookup`), not the god-class itself. No structural proof
  supports moving this dimension this loop.
- **State management and runtime ownership:** 6.5 | SAME | The Authority Map's Store-name-
  resolution-caches concern was already "Single and clear" before this loop (one writer per cache,
  no ambiguity) — this loop closed a Locality/Interface-coherence gap for that concern (see Code
  simplicity below), not a write-authority correctness defect, so this dimension does not move.
  `isLibraryOperationRunning` and `AppliedArtworkStore.applied` remain unchanged.
- **Domain modeling:** 5.5 | SAME | Unaffected by this loop's change;
  `SteamGridDbClient.ParseOfficialCapsuleUrl` (`SteamGridDbClient.cs:144-199`) still hand-parsed
  outside the DTO's own deserialization, verified unchanged this loop.
- **Data flow and dependency design:** 6.0 | SAME | This loop's fix is entirely in-process
  reorganisation within an already-relocated module (`StoreNameLookup`, moved out of `PrimaryWidget`
  in loop 3) — no dependency moved, no new port introduced, no call site outside `StoreNameLookup`
  gained or lost a dependency it didn't already have. Deliberately not scored up for the same diff
  that earns the Code simplicity credit below (loop 3's own scorecard-humility note flagged this
  exact double-counting risk).
- **Framework / platform best practices:** 6.0 | SAME | Unaffected by this loop's change. The two
  JSON idioms (`DataContractJsonSerializer` vs. ad hoc `Windows.Data.Json`) still coexist, verbatim.
- **Concurrency and runtime safety:** 5.5 | SAME | Unaffected by this loop's change. F-003's fully
  sequential per-game round-trips (`PrimaryWidget.xaml.cs:324-733`) remain open. `gogNameCache` is
  still a plain, non-thread-safe `Dictionary` after this loop — narrowing its visibility to `private`
  changed nothing about its thread-safety.
- **Code simplicity and clarity:** 6.5 | UP | `StoreNameLookup.GetOrFetchGogNameAsync`
  (`StoreNameLookup.cs:87-102`, new this loop) now owns the full check-cache/fetch/populate decision
  for GOG names in one place, matching the shape `GetUbisoftGameNameAsync` already used in the same
  file. The call site in `PrimaryWidget.xaml.cs` shrank from a 17-line inline cache-check/fetch/
  populate block (`:597-613` before this loop) to a 4-line call-and-assign
  (`PrimaryWidget.xaml.cs:599-604` now). `gogNameCache` moved from `internal` back to `private`
  (`StoreNameLookup.cs:28`) now that only `StoreNameLookup` itself touches it — Interface now
  `>>` Implementation for this slice, closing the shallow-module gap Finding #1's loop-3 evidence
  named. Not a bigger jump: the identical gap for Epic's cache (`epicNameCache`) is untouched, and
  F1's dominant concerns (UI handling, backup/restore) are untouched.
- **Test strategy and regression resistance:** 3.0 | SAME | No test project exists; standing user
  instruction prohibits adding one this run. Verified unchanged this loop. Named, non-backlog-item
  blocker, as recorded loops 1-3.
- **Overall implementation credibility:** 5.5 | SAME | `GetGogGameNameAsync`'s own failure handling
  (`StoreNameLookup.cs:71-75`, swallows via `Debug.WriteLine` only) is untouched by this loop — the
  new `GetOrFetchGogNameAsync` wrapper adds no new error handling and introduces no new swallow site,
  so the cited weakness neither improves nor regresses.

## Authority Map

(Re-emitted this loop: the Store-name-resolution-caches concern's write-authority for the GOG slice
changed.)

- **Concern:** Library-operation exclusivity (`isLibraryOperationRunning`)
  - Owner: `PrimaryWidget` instance
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Observers / readers: `IsLibraryOperationBlocking`, all four header-button click handlers,
    `EditGameImage_Click`, `SearchGameImage_Click`, `RestoreBackup_Click`
  - Persistence seam: none (in-memory only)
  - Async mutation entry points: `TryBeginLibraryOperation`/`EndLibraryOperation`, called from
    every `*_Click` handler via a try/finally
  - Verdict: **Single and clear** — unaffected this loop.

- **Concern:** Applied-artwork record (`AppliedArtworkStore.applied`)
  - Owner: `AppliedArtworkStore` (static, `Services/Artwork/`)
  - Allowed writers: `UpdateAsync` (via `SetAsync`/`ClearAsync`), gated by `gate`
  - Observers / readers: `GetAsync`, also gated by `gate` (F-002, resolved loop 2)
  - Persistence seam: `applied-artwork.json` in `ApplicationData.Current.LocalFolder`
  - Async mutation entry points: `SetAsync` (from `ReplaceImageCoreAsync`), `ClearAsync` (from
    `RestoreBackupCoreAsync`)
  - Verdict: **Single and clear** — unaffected this loop.

- **Concern:** Store-name resolution caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`,
  `ubisoftGameLookupCache`)
  - Owner: `Services/Stores/StoreNameLookup`
  - Allowed writers: `gogNameCache` — **now `StoreNameLookup.GetOrFetchGogNameAsync` only**, via
    private field access (changed this loop; was `PrimaryWidget.LoadGameEntriesAsync` writing the
    field directly). `epicNameCache` — still `PrimaryWidget.LoadGameEntriesAsync` only, via direct
    field access (unchanged; same residual as loop 3). `nameMatchCache`/`ubisoftGameLookupCache` —
    `StoreNameLookup.FindGameByNameAsync`/`.LoadUbisoftGameListAsync` only (unchanged, private).
  - Observers / readers: the same methods that write each cache (`TryGetValue` before writing)
  - Persistence seam: none (in-memory, per-process)
  - Async mutation entry points: `GetOrFetchGogNameAsync` (GOG, now internal to `StoreNameLookup`);
    inside `LoadGameEntriesAsync`'s per-entry body (Epic only, now); inside `StoreNameLookup`'s own
    methods (nameMatch/Ubisoft)
  - Verdict: **Single and clear** — three of four caches (`gogNameCache` as of this loop,
    `nameMatchCache`, `ubisoftGameLookupCache`) are now fully owned in both write-authority and
    Locality by `StoreNameLookup` itself. `epicNameCache` still carries the same Interface-coherence
    gap this concern's loop-3 entry described for both GOG and Epic: no write-authority ambiguity
    (still one caller), but the class holding the data does not yet own the caching decision for
    that one field. See Finding #1 and the matching Deepening Candidate.

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design
  (`Services/SteamGridDB/ArtworkSource.cs:15-51`) makes "neither a platform-ID nor a game-ID"
  unrepresentable — a genuine smart-constructor, not decoration. Re-verified unchanged this loop.
- The official-artwork gate (`FindOfficialLookalikeAsync`, `PrimaryWidget.xaml.cs:1428-1499`) is a
  narrow, evidence-tuned veto whose code comments cite the specific regression case and slack margin
  that motivated it. Re-verified unchanged this loop (line numbers shifted by -8 from this loop's
  edit near the top of the store-name-resolution block; content unaffected).
- `StoreNameLookup.GetOrFetchGogNameAsync` (new this loop) mirrors the exact check-cache/fetch/
  populate shape `GetUbisoftGameNameAsync` already used in the same file, rather than inventing a
  new caching idiom — one fewer shape for a future contributor to choose between when Epic's
  equivalent gets the same treatment.

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling and backup/restore orchestration behind zero Interface boundary

**Why it matters** — The churn-flagged god-class (21 edits, still the largest file in the repo)
continues to bundle two structurally distinct concerns with no Module boundary between them, so a
change to either risks touching the other.

**What is wrong** — UI event handling (the `*_Click` handlers, grid/search panel management,
artwork download/replace flow) and backup/restore orchestration (`RestoreBackupCoreAsync`,
`RestoreBackupAsync`, the `RestoreBackupResult` enum) remain private members on one 2,291-line
`Page`-derived class with no Interface separating them. This loop closed the GOG slice of the
split-cache-ownership residual loop 3 left behind: `StoreNameLookup.GetOrFetchGogNameAsync` now
owns the full check-cache/fetch/populate decision for GOG names, and `gogNameCache` is private
again. The Epic slice (`epicNameCache`, still internal, still written directly by
`PrimaryWidget.LoadGameEntriesAsync`) remains open. This fix does not touch the file's two dominant
merged concerns (UI event handling, backup/restore orchestration) at all — F1's core claim is
unchanged from loop 3.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:2150-2233` (`RestoreBackupCoreAsync` — backup/restore
  orchestration, still inline)
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:87-102` (new this loop —
  `GetOrFetchGogNameAsync` owns GOG's cache decision; `gogNameCache` narrowed to `private` at `:28`)
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:608-620` (`epicNameCache` still `internal`, still
  read/written directly by `LoadGameEntriesAsync` — the unclosed half of the split-cache-ownership
  residual)

**Architectural test failed** — n/a — different category (ownership/coupling sprawl, not a
removable Seam or wrapper)

**Dependency category** — n/a (unaffected by this loop; the true-external piece of F1 was closed
loop 3)

**Leverage impact** — Every future backup/restore fix or UI change still touches the same file; a
maintainer touching either risks disturbing the other via shared `Dispatcher`/`StatusText`
plumbing.

**Locality impact** — Unaffected: a maintainer fixing a UI bug still reads through ~2,290 lines
including unrelated backup/restore logic.

**Metric signal** — `PrimaryWidget.xaml.cs`: 2,291 LOC (down 8 lines this loop, all from the GOG
cache block shrinking). `StoreNameLookup.cs`: 288 LOC (up 25 lines this loop).

**Why this weakens submission** — Ownership of the two remaining distinct concerns (UI event
handling, backup/restore orchestration) is still untraceable from any single Module; the file is
not meaningfully smaller this loop and is still well above the one-or-two-shallow-wrapper bar the
architecture-quality 7-anchor requires.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Fold the Epic slice next (`StoreNameLookup.GetOrFetchEpicNameAsync`,
preserving the `EpicLibrary.GetDisplayNameAsync` fallback-first order exactly), then attempt
backup/restore orchestration — more UI-entangled than either cache fold, needing either a
UI-update callback parameter or an accepted partial split; do not attempt it as a same-shape
mechanical move without first confirming which `Dispatcher.RunAsync` blocks can move to the caller
without changing update timing.

**Blast radius** — Change (next loop): `Services/Stores/StoreNameLookup.cs`,
`PrimaryWidget.xaml.cs` (Epic slice) OR a new file for backup/restore. Avoid: `Services/SteamGridDB/*`,
`Services/Artwork/ArtworkRanker.cs`.

---

### Finding #2: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** — Load time scales linearly with library size and network latency on the
widget's primary open path — the one flow every user hits every time.

**What is wrong** — The `gameCache` `foreach` loop in `LoadGameEntriesAsync` awaits
`sgdbClient.GetGameByPlatformIdAsync` (and the GOG/Epic/Ubisoft name fallbacks, routed through
`StoreNameLookup` and `EpicLibrary`) one game at a time; nothing overlaps the independent per-game
network calls. Unaffected by this loop's fix, which changed how the GOG branch resolves a name,
not whether it does so sequentially.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:324-733` (per-folder, per-entry `foreach`; awaits
  `GetGameByPlatformIdAsync` at `:577` and the store-name fallbacks at `:599,614-615,630,650`, with
  nothing overlapped — re-verified at current line numbers this loop after F1's GOG-slice edit
  shifted them)

**Architectural test failed** — n/a — different category (D2, structural waste per
`lens-efficiency.md`, not a Seam)

**Dependency category** — `true-external`

**Leverage impact** — There is only one call site (the load loop); a future second caller of the
same pattern would inherit the same linear cost with no leverage from batching, since none exists.

**Locality impact** — The fix is local to `LoadGameEntriesAsync`'s loop body and `StoreNameLookup`'s
cache field declarations; it does not need to spread to callers.

**Metric signal** — One HTTP round-trip per game per store lookup; a 100-game library issues 100+
sequential requests with no overlap (D2, `lens-efficiency.md`).

**Why this weakens submission** — Structural waste on the widget's primary hot path. The fix is
well-understood (bounded concurrency) but crosses a real risk boundary: the store-name caches
(`gogNameCache`, `epicNameCache`, `nameMatchCache`, `ubisoftGameLookupCache`, all on
`StoreNameLookup`) are still not thread-safe and currently rely on this exact sequencing to stay
race-free.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Bound concurrency (e.g. `SemaphoreSlim(4-8)` + `Task.WhenAll`) around
the per-entry body, and switch `StoreNameLookup`'s four cache fields to `ConcurrentDictionary`
*before* parallelizing — do not parallelize without that change, or the caches race.

**Blast radius** — Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync`),
`Services/Stores/StoreNameLookup.cs` (the four cache fields). Avoid: `Services/Artwork/*`,
`Services/SteamGridDB/*`.

---

### Finding #3: Hand-rolled double-checked-locking cache pattern duplicated between AppliedArtworkStore and EpicLibrary

**Why it matters** — The same ~25-line lazy-load-with-gate skeleton was written twice by hand
instead of once; a future third cache would make it three.

**What is wrong** — `AppliedArtworkStore.LoadAsync` (`AppliedArtworkStore.cs:95-147`) and
`EpicLibrary.LoadAsync` (`EpicLibrary.cs:67-89`) both implement: check-null, await `SemaphoreSlim`
gate, re-check-null, populate, release — identical structure, no shared helper. Unaffected by this
loop's fix, which targeted the GOG cache fold in a different file (`StoreNameLookup.cs`). Re-verified
this loop: `StoreNameLookup.GetOrFetchGogNameAsync` uses a plain `TryGetValue`/populate pattern with
no lock (matching the rest of that class), not this locked skeleton, so no third copy was
introduced.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:95-147`
- `SteamGridDB.Xbox/Services/Stores/EpicLibrary.cs:67-89`

**Architectural test failed** — n/a — different category (leaf-module duplication, not a Seam)

**Dependency category** — n/a

**Leverage impact** — A shared lazy-cache primitive would pay for itself across at least these two
call sites.

**Locality impact** — Today a bug in the locking pattern must be fixed in two places; a shared
helper collapses that to one.

**Metric signal** — none

**Why this weakens submission** — Duplicate concurrency boilerplate is exactly the kind of
copy-paste-with-slight-variation the leaf-module duplication sweep looks for; it is real but
contained.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a small internal `AsyncLazy`-style helper
(check-lock-recheck-populate) that both call sites construct against. Do not add an interface or
DI — this is one concrete type serving two internal call sites, not a Seam.

**Blast radius** — Change: `Services/Artwork/AppliedArtworkStore.cs`,
`Services/Stores/EpicLibrary.cs`, one new small internal helper. Avoid: `PrimaryWidget.xaml.cs`,
`Services/Stores/StoreNameLookup.cs`.

## Simplification Check

- **Structurally necessary:** Folding GOG's check-cache/fetch/populate-cache logic into
  `StoreNameLookup.GetOrFetchGogNameAsync` — closes the shallow-module residual Finding #1's loop-3
  evidence named (Interface ≈ Implementation for `gogNameCache`, now Interface `>>` Implementation);
  the smallest honest fix is a targeted fold plus a `private` narrowing, not a new abstraction.
- **New seam justified:** false — no new Seam; `GetOrFetchGogNameAsync` is a plain internal method
  added to the existing static class, not a port/interface.
- **Helpful simplification:** `PrimaryWidget.xaml.cs`'s GOG branch shrank from 17 lines of inline
  cache-check/fetch/populate logic to a 4-line call-and-assign; the check-cache/fetch/populate
  decision for GOG names now lives entirely inside `StoreNameLookup`, matching the shape
  `GetUbisoftGameNameAsync` already used.
- **Should NOT be done:** Folding Epic's equivalent block in the same loop — Epic's path tries
  `EpicLibrary.GetDisplayNameAsync` first via `??` before falling back to
  `StoreNameLookup.GetEpicGameNameAsync`, a second fallback source GOG's path does not have, so the
  same mechanical fold carries more behavior-preservation risk without a test oracle. Deferred to a
  future loop per the Deepening Candidate below, following loop 3's own "smallest first step"
  ordering (GOG first, named explicitly in loop 3's Deepening Candidates).
- **Tests after fix:** No test project exists (standing instruction); `MSBuild` compile is the only
  regression oracle, verified green both before and after this loop's change (exit 0 both times).
  This is a behavior-preserving fold, not a Meta-Rule-4 risk-boundary crossing: `gogNameCache`'s
  visibility narrowed from `internal` to `private`, but its only external reader/writer
  (`PrimaryWidget.LoadGameEntriesAsync`) was rewritten in this same commit to go through
  `GetOrFetchGogNameAsync` instead — grep-verified post-edit that no file outside
  `StoreNameLookup.cs` references `gogNameCache` any more, and the green build independently proves
  no orphaned caller was left behind (a private field read from another class is a compile error).
  `loop_result.risk_boundary_evidence` is `null`.

## Improvement Backlog

1. **Continue the PrimaryWidget.xaml.cs break-up (F1, next slice): fold Epic's cache, or attempt
   backup/restore orchestration** — fold Epic's check-cache/fetch/populate logic into
   `StoreNameLookup.GetOrFetchEpicNameAsync` (mirroring this loop's GOG fix, preserving the
   `EpicLibrary`-first fallback order exactly), OR move `RestoreBackupCoreAsync`,
   `RestoreBackupAsync`, and the `RestoreBackupResult` enum out of `PrimaryWidget.xaml.cs` —
   whichever the next loop's friction check favors. Backup/restore is more UI-entangled than either
   cache fold, so plan carefully before attempting a same-shape mechanical move.
   - Why it matters: F1 remains the largest Serious deduction on the board; two concerns are still
     merged in the god-class the churn signal flagged, and closing Epic's cache slice finishes the
     smaller residual before the higher-risk backup/restore slice is attempted.
   - Score impact: Architecture quality +0.5, Code simplicity +0.5 once verified (backup/restore
     slice); or a smaller Code simplicity win from closing Epic's cache-ownership residual.
2. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups** (F-003), *after*
   switching the four store-name caches (on `StoreNameLookup`) to `ConcurrentDictionary` —
   structural, helpful.
   - Why it matters: removes load latency that scales linearly with library size on the widget's
     primary flow.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified.
3. **Extract a shared AsyncLazy-style cache helper for AppliedArtworkStore and EpicLibrary** (F-004)
   — simplification, helpful.
   - Why it matters: collapses two hand-copied lazy-load-with-gate skeletons into one owner before
     a third makes it three.
   - Score impact: Code simplicity +0.5 once verified.

## Deepening Candidates

- **Candidate Module:** `Services/Stores/StoreNameLookup` — fold the Epic name-caching logic inside
  it, matching the shape this loop just gave GOG's.
  - Source friction proven: Finding #1's residual evidence — `epicNameCache` is an `internal` field
    on `StoreNameLookup` (`StoreNameLookup.cs:29`) written and read only by `PrimaryWidget.xaml.cs`
    (`:608-620`), the same shallow-module gap `gogNameCache` had before this loop.
  - Why shallow/misplaced: `epicNameCache` exposes internal mutable state as a bare Interface (a
    `Dictionary` directly, not a method) — Interface ≈ Implementation for that slice, the
    shallow-module test's own definition of shallow.
  - Behaviour to move behind the deeper Interface: the check-cache/fetch/populate logic currently
    inline in `LoadGameEntriesAsync` (`PrimaryWidget.xaml.cs:608-620`) should become a
    `GetOrFetchEpicNameAsync` method on `StoreNameLookup`, mirroring `GetOrFetchGogNameAsync`'s
    shape, with `epicNameCache` becoming `private`.
  - Dependency category: `in-process`
  - Test surface after change: none (no test project; build-verified only, same as today)
  - Smallest first step: preserve the exact fallback order — `EpicLibrary.GetDisplayNameAsync`
    (local manifest read) tried first via `??`, only then `StoreNameLookup.GetEpicGameNameAsync`
    (remote community database) — the wrapper method must try both in that order, not just the
    second one GOG-style.
  - What not to do: do not collapse the two-source `??` fallback into a single call, and do not
    change the "empty cached value still means refetch" semantics while doing this — same trap this
    loop's GOG fix preserved explicitly.

## Builder Notes

1. **Pattern:** When a backlog item names two alternatives ("do the risky thing, or the safer
   fallback"), re-run the Simplify Pressure Test on the risky option fresh each loop rather than
   defaulting to whichever sounds more impactful.
   - How to recognize: the backlog item itself flags a design decision still needed (a callback
     parameter, an accepted partial split) before the riskier option can be attempted safely.
   - Smallest coding rule: in a codebase with no test suite, when the smallest honest fix and the
     "real" fix diverge, take the smallest honest fix this loop and leave the real fix as a
     concretely-scoped backlog item — don't force a design decision under loop time pressure.
   - Stack example: C# — this loop took the named fallback (fold GOG's cache) over the named primary
     ask (extract backup/restore) because the primary ask's own text said it "needs either a
     UI-update callback parameter or accepting a partial split," which is a design decision, not a
     mechanical move.

2. **Pattern:** A residual left half-closed by one loop (fixing GOG's cache but not Epic's) is a
   legitimate, named next step — not a sign the prior loop's work was wasted.
   - How to recognize: the Deepening Candidate or Finding evidence names two or more parallel
     instances of the same shallow-module shape (here: four caches, two now folded, two still to
     go), and the fix for one instance is a template for the rest.
   - Smallest coding rule: fix the lowest-risk instance first (the one with no secondary fallback
     source to preserve), and use it as the concrete "smallest first step" for the next one, rather
     than doing all instances in one loop.

3. **Pattern:** Churn concentrates where concerns are merged (unchanged from loops 1-3 — still
   live; this loop did not touch the churn-flagged file's concern count at all).
   - How to recognize: one file dominates the six-month edit count and is several times larger than
     everything else, even after partial extraction.
   - Smallest coding rule: when a file's edit count and size both dominate the repo, extract the
     concern that changed rather than adding to the pile — but a loop that instead does a smaller,
     safer fix elsewhere is not wasted, provided it's disclosed honestly as not moving the god-class
     dimension.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. `code_simplicity` moving UP to 6.5 for a fix scoped to one of four cache fields in a module that
   is itself a secondary extraction (not the god-class) — a stricter reviewer could argue this is
   too narrow a diff to move a whole-codebase dimension score at all, and that the credit belongs
   entirely in the Finding/Deepening-Candidate text rather than the scorecard.
2. `data_flow` staying SAME rather than moving UP — I judged the in-process reorganisation doesn't
   independently earn a data-flow credit distinct from the `code_simplicity` credit for the same
   diff, following loop 3's own scorecard-humility caution about double-counting; a less
   conservative reviewer could disagree and credit both dimensions for the same evidence.
3. `state_management` staying SAME despite the Authority Map entry text changing — I judged the
   write-authority correctness was never in question (still "Single and clear" before and after),
   so only Locality/Interface-coherence moved, which I attributed entirely to `code_simplicity`; a
   reviewer weighting "how many of a concern's writers-of-record are internal to its own Module" as
   a `state_management` fact rather than purely a `code_simplicity` one could score this dimension
   up instead.

## Final Judge Narrative

Place, not win. This loop did not touch `PrimaryWidget.xaml.cs`'s two remaining merged concerns
(UI event handling, backup/restore orchestration) at all — F1 (Serious) is exactly as large as it
was at the end of loop 3. Instead, the loop took the backlog's own named fallback: closing the GOG
slice of the split-cache-ownership residual loop 3 left in `Services/Stores/StoreNameLookup.cs`.
The primary ask (backup/restore extraction) was assessed and downgraded per the Simplify Pressure
Test — its own text ("needs either a UI-update callback parameter or accepting a partial split")
signals a design decision this codebase's lack of a test oracle makes too risky to attempt as a
same-loop mechanical move. `code_simplicity` moved up on real, narrow, build-verified structural
proof; no other dimension moved, honestly reflecting that this loop's fix, while genuine, was
smaller in scope than a god-class reduction. Runtime ownership remains trustworthy for what has
been resolved; concurrency is not yet trustworthy (F-003 open, and this loop's GOG fold left the
cache field just as non-thread-safe as before, only smaller in scope). Tests remain absent by
standing instruction. Future work risks the same trap this loop deliberately avoided: folding
Epic's cache needs its two-source fallback order preserved exactly, and backup/restore extraction
needs the UI-update design decision resolved before attempting a mechanical move.

## Loop 4 Result

Closed the GOG slice of F1's split-cache-ownership residual: added
`StoreNameLookup.GetOrFetchGogNameAsync` (`Services/Stores/StoreNameLookup.cs:87-102`), which owns
the full check-cache/fetch/populate decision for GOG names, and narrowed `gogNameCache` from
`internal` back to `private` (`StoreNameLookup.cs:28`) now that `StoreNameLookup` is its only
reader/writer. Rewrote the GOG branch in `PrimaryWidget.xaml.cs`'s `LoadGameEntriesAsync`
(`:597-604`) from a 17-line inline cache-check/fetch/populate block to a 4-line call-and-assign.
`git diff --stat`: `PrimaryWidget.xaml.cs` (2 insertions, 10 deletions), `StoreNameLookup.cs`
(28 insertions, 3 deletions).

**What proves the change is honest:** `MSBuild` (the sole regression oracle per Discovery — no test
project exists) passed clean both before and after the change (exit 0 both times, same command as
loops 1-3's baseline). The new `GetOrFetchGogNameAsync` preserves the original inline logic's
semantics exactly: a cache hit with a non-empty cached value returns it with zero network calls
(matching the original's `TryGetValue` + non-empty check); a cache miss or empty cached value makes
exactly one call to `GetGogGameNameAsync` (unchanged, matching the original's single fetch); the
cache is written only when that call returns a non-empty name (matching the original's
`if (!string.IsNullOrEmpty(gogName))` write guard). Grep-verified post-edit that `gogNameCache` is
referenced only inside `StoreNameLookup.cs`, confirming the `private` narrowing left no orphaned
external caller — independently confirmed by the green build (an orphaned private-field access from
another class would not compile). This changes where the cache-check/fetch/populate decision lives,
not the number of network calls per game, retry semantics, or any selection/ranking behavior —
confirmed by the independent implementation-reviewer pass (`verdict: approved`;
reality/honesty/regression all `passed`).

**Risk boundary evidence (Meta-Rule 4):** none — no isolation/`Sendable`/conditional-compilation/
lock-ordering boundary was crossed, and the visibility narrowing (`gogNameCache` `internal` →
`private`) is not the cross-file-visibility hazard Meta-Rule 4 targets (moving code between files
and accidentally losing access): the field never moved files, and its one external caller was
rewritten in this same commit to no longer need direct access. `loop_result.risk_boundary_evidence`
is `null`.

**Targeted finding status:** `carried_forward` — F1 (F-001) is evidenced by two remaining merged
concerns (UI event handling, backup/restore orchestration), neither of which this loop touched; the
GOG-cache residual this loop did close was cited as F1 *evidence*, not F1's core claim, so F1 stays
open for the next loop.

**Unintended scorecard regression:** none observed. `code_simplicity` moved UP on structural proof;
no other dimension regressed.

## Loop 4 Implementation Review

`verdict: approved` — "GetOrFetchGogNameAsync (StoreNameLookup.cs:87-102) now owns the GOG cache's
check/fetch/populate decision with gogNameCache correctly narrowed to private, closing the
shallow-module residual cited in Finding #1's evidence, and the cache-hit/miss/empty-retry/
write-only-on-success semantics are preserved exactly with no change in network-call count." All
three checks (`reality`, `honesty`, `regression`) `passed`; `conditions: []`; `regressions: []`.
