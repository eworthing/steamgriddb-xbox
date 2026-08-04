### Loop Counter

Loop 3 of 10 (cap)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Independent re-derivation from current source (build-verified green both before and after this
loop's change) confirms `PrimaryWidget.xaml.cs` is still the churn-flagged leaky seam, now reduced
from 2,530 to 2,299 LOC after this loop closed the third of its four originally-merged concerns:
store-name resolution moved to `Services/Stores/StoreNameLookup.cs`, mirroring the existing
`EpicLibrary.cs` sibling. Two concerns (UI event handling, backup/restore orchestration) remain
merged with no Interface boundary, and F3/F4 are untouched this loop.

## Scorecard (1-10)

- **Architecture quality:** 5.5 | UP | `PrimaryWidget.xaml.cs` shrank from 2,530 to 2,299 LOC
  (231 lines, -9%) this loop: `GetGogGameNameAsync`, `GetEpicGameNameAsync`,
  `LoadUbisoftGameListAsync`, `GetUbisoftGameNameAsync`, `FindGameByNameAsync`,
  `NormaliseGameName`, and their four cache fields moved to the new
  `Services/Stores/StoreNameLookup.cs:1-263`, closing the third of the original four merged
  concerns (artwork ranking closed loop 1). Two concerns still remain merged in
  `PrimaryWidget.xaml.cs` (UI event handling, backup/restore orchestration at `:2158-2241`), so this
  is a partial-credit move within "5 - Middling" territory, not a jump to "7" — main ownership is
  not yet clear at the Module level.
- **State management and runtime ownership:** 6.5 | SAME | Unaffected by this loop's change.
  `AppliedArtworkStore`'s lock symmetry (F2, resolved loop 2) and `isLibraryOperationRunning`
  remain unchanged. The Store-name-resolution-caches Authority Map entry is updated below to
  reflect the new physical location, but the write-authority itself (single caller,
  `LoadGameEntriesAsync`) is unchanged, so no delta here.
- **Domain modeling:** 5.5 | SAME | Unaffected by this loop's change;
  `SteamGridDbClient.ParseOfficialCapsuleUrl` (`SteamGridDbClient.cs:144-199`) still hand-parsed
  outside the DTO's own deserialization, verified unchanged this loop.
- **Data flow and dependency design:** 6.0 | UP | GOG/Epic/Ubisoft network calls (`true-external`
  dependencies) are no longer instantiated inside `PrimaryWidget` — they moved to
  `Services/Stores/StoreNameLookup.cs`, which owns its own `HttpClient` field
  (`StoreNameLookup.cs:37-39`), mirroring the already-established `EpicLibrary.cs` pattern in the
  same directory. No port/interface was introduced (none was needed — see Simplification Check), so
  this does not reach the 7-anchor ("dependencies acyclic... enforced by convention"); it is a real
  but partial win: one fewer file to read to trace a true-external dependency, not yet a DAG
  enforced by source.
- **Framework / platform best practices:** 6.0 | SAME | Unaffected by this loop's change. The two
  JSON idioms (`DataContractJsonSerializer` vs. ad hoc `Windows.Data.Json`) still coexist, verbatim,
  in both the old and new locations.
- **Concurrency and runtime safety:** 5.5 | SAME | Unaffected by this loop's change. F3's fully
  sequential per-game round-trips (`PrimaryWidget.xaml.cs:324-741`) remain open and still cap this
  dimension; the four store-name caches are still plain, non-thread-safe `Dictionary`s (now on
  `StoreNameLookup`, same non-thread-safety as before the move).
- **Code simplicity and clarity:** 6.0 | UP | Store-name-resolution logic for all three stores
  (GOG/Epic/Ubisoft) now lives in one 263-line file instead of being interleaved inside a
  2,500-line UI class; a reader tracing "how does the widget resolve a third-party store's game
  name" now reads one dedicated file instead of searching a god-class. The move added no new
  abstraction, protocol, or ceremony — it reuses the exact existing code, matching the smallest
  honest fix. Not a bigger jump because F4's duplicate lazy-cache skeleton (a separate simplicity
  finding) is untouched, and the new file itself has one residual asymmetry (see Finding #1
  evidence) that is not yet a simplification win.
- **Test strategy and regression resistance:** 3.0 | SAME | No test project exists; standing user
  instruction prohibits adding one this run. Verified unchanged this loop (no `*.csproj` matching a
  test name found; `SteamGridDB.Xbox.sln` project list unchanged). Named, non-backlog-item blocker,
  as recorded loops 1-2.
- **Overall implementation credibility:** 5.5 | SAME | The three store-name-resolution methods
  still swallow failures via `Debug.WriteLine` only — this loop moved that code verbatim (same
  swallow pattern, same file:line shape, just relocated), so the cited weakness persists unchanged
  in its new location (`StoreNameLookup.cs:66-69,150-153,216-219,239-242,269-272`).

## Authority Map

(Re-emitted this loop: the Store-name-resolution-caches concern's physical location changed.)

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
  - Observers / readers: `GetAsync`, also gated by `gate` (F2, resolved loop 2)
  - Persistence seam: `applied-artwork.json` in `ApplicationData.Current.LocalFolder`
  - Async mutation entry points: `SetAsync` (from `ReplaceImageCoreAsync`), `ClearAsync` (from
    `RestoreBackupCoreAsync`)
  - Verdict: **Single and clear** — unaffected this loop.

- **Concern:** Store-name resolution caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`,
  `ubisoftGameLookupCache`)
  - Owner: `Services/Stores/StoreNameLookup` (fields relocated here this loop — previously static
    fields on `PrimaryWidget`)
  - Allowed writers: `gogNameCache`/`epicNameCache` — `PrimaryWidget.LoadGameEntriesAsync` only,
    via direct field access (same single call site as before the move, now qualified
    `StoreNameLookup.gogNameCache`/`.epicNameCache`). `nameMatchCache`/`ubisoftGameLookupCache` —
    `StoreNameLookup.FindGameByNameAsync`/`.LoadUbisoftGameListAsync` only (unchanged; now private
    to the class that uses them, tighter than before).
  - Observers / readers: the same methods (`TryGetValue` before writing)
  - Persistence seam: none (in-memory, per-process)
  - Async mutation entry points: inside `LoadGameEntriesAsync`'s per-entry body (gog/epic); inside
    `StoreNameLookup`'s own methods (nameMatch/ubisoft)
  - Verdict: **Single and clear today** — same caveat as loops 1-2 (holds only because F3's
    sequential-loop defect keeps these `Dictionary`s race-free). New Locality caveat this loop:
    `gogNameCache`/`epicNameCache` are `internal` fields on `StoreNameLookup` that only
    `PrimaryWidget` touches — the class holding the data does not yet own the caching *decision*
    for those two (contrast with `nameMatchCache`/`ubisoftGameLookupCache`, which are private and
    fully owned). No write-authority ambiguity exists (still one caller), so this stays "Single and
    clear," but see Finding #1 and the matching Deepening Candidate for the Interface-coherence gap
    this leaves.

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design
  (`Services/SteamGridDB/ArtworkSource.cs:15-51`) makes "neither a platform-ID nor a game-ID"
  unrepresentable — a genuine smart-constructor, not decoration. Re-verified unchanged this loop.
- The official-artwork gate (`FindOfficialLookalikeAsync`, `PrimaryWidget.xaml.cs:1436-1507` — line
  numbers shifted by -8 this loop from the field-block removal near the top of the file; content
  unaffected) is a narrow, evidence-tuned veto whose code comments cite the specific regression case
  and slack margin that motivated it. Re-verified unchanged this loop.
- This loop's extraction mirrors `EpicLibrary.cs`'s existing shape (a plain `internal static class`
  in `Services/Stores/`, no interface, no DI) rather than inventing a new pattern for the fourth
  store-facing lookup type added to that directory — one fewer shape for a future contributor to
  choose between when the next store integration is added.

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling and backup/restore orchestration behind zero Interface boundary

**Why it matters** — The churn-flagged god-class (21 edits, still the largest file in the repo)
continues to bundle two structurally distinct concerns with no Module boundary between them, so a
change to either risks touching the other.

**What is wrong** — UI event handling (the `*_Click` handlers, grid/search panel management,
artwork download/replace flow) and backup/restore orchestration (`RestoreBackupCoreAsync`,
`RestoreBackupAsync`, the `RestoreBackupResult` enum) remain private members on one 2,299-line
`Page`-derived class with no Interface separating them. Store-name resolution
(`GetGogGameNameAsync`, `GetEpicGameNameAsync`, `LoadUbisoftGameListAsync`,
`GetUbisoftGameNameAsync`, `FindGameByNameAsync`, `NormaliseGameName`, plus the four cache fields)
was extracted to `Services/Stores/StoreNameLookup.cs` this loop, closing the third of the four
original concerns (artwork ranking was the first, closed loop 1).

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:2158-2241` (`RestoreBackupCoreAsync` — backup/restore
  orchestration, still inline)
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:1-263` (new file this loop — store-name
  resolution now has its own Module)
- `SteamGridDB.Xbox/SteamGridDB.Xbox.csproj:131` (new `Compile` entry)
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:27-28` (`gogNameCache`/`epicNameCache` are
  `internal` fields written and read only by `PrimaryWidget.xaml.cs:597-613,616-630`, not by
  `StoreNameLookup` itself — residual split ownership left by this loop's minimal-risk move; see
  Deepening Candidates)

**Architectural test failed** — n/a — different category (ownership/coupling sprawl, not a
removable Seam or wrapper)

**Dependency category** — n/a (the remaining scope — UI event handling and backup/restore — is not
primarily a domain↔framework/persistence leakage concern; the true-external piece of F1 was closed
this loop, see the Data flow scorecard entry)

**Leverage impact** — Every future backup/restore fix or UI change still touches the same file; a
maintainer touching either risks disturbing the other via shared `Dispatcher`/`StatusText`
plumbing.

**Locality impact** — A maintainer fixing a UI bug still reads through ~2,300 lines including
unrelated backup/restore logic to find the relevant lines — narrower than before (was ~2,530
including store-name resolution too) but still spread across two concerns.

**Metric signal** — `PrimaryWidget.xaml.cs`: 2,299 LOC (re-measured this loop, down from 2,530 at
loop 2's end) — 231 lines / 9% smaller after this loop's extraction.

**Why this weakens submission** — Ownership of the two remaining distinct concerns (UI event
handling, backup/restore orchestration) is still untraceable from any single Module; the file is
smaller but not yet at the one-or-two-shallow-wrapper bar the architecture-quality 7-anchor
requires.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Backup/restore orchestration is the next self-contained candidate,
though it is more entangled with UI (`Dispatcher.RunAsync`, `StatusText`, `EntriesSharingImage`)
than store-name resolution was — extracting it cleanly needs either a UI-update callback parameter
or accepting a partial split. Do not attempt the same mechanical-move shape without first
confirming which `Dispatcher.RunAsync` blocks can move to the caller without changing update
timing.

**Blast radius** — Change (next loop): `PrimaryWidget.xaml.cs`, a new file for backup/restore (if
pursued) OR `Services/Stores/StoreNameLookup.cs` (if the split-cache residual is pursued instead).
Avoid: `Services/SteamGridDB/*`, `Services/Artwork/ArtworkRanker.cs`.

---

### Finding #2: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** — Load time scales linearly with library size and network latency on the
widget's primary open path — the one flow every user hits every time.

**What is wrong** — The `gameCache` `foreach` loop in `LoadGameEntriesAsync` awaits
`sgdbClient.GetGameByPlatformIdAsync` (and the GOG/Epic/Ubisoft name fallbacks, now routed through
`StoreNameLookup`) one game at a time; nothing overlaps the independent per-game network calls.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:324-741` (per-folder, per-entry `foreach`; awaits
  `GetGameByPlatformIdAsync` at `:577` and the `StoreNameLookup` fallbacks at `:601,623,638,658`,
  with nothing overlapped — re-verified at current line numbers this loop after F1's extraction
  shifted them)

**Architectural test failed** — n/a — different category (D2, structural waste per
`lens-efficiency.md`, not a Seam)

**Dependency category** — `true-external`

**Leverage impact** — There is only one call site (the load loop); a future second caller of the
same pattern would inherit the same linear cost with no leverage from batching, since none exists.

**Locality impact** — The fix is local to `LoadGameEntriesAsync`'s loop body and
`StoreNameLookup`'s cache field declarations; it does not need to spread to callers.

**Metric signal** — One HTTP round-trip per game per store lookup; a 100-game library issues 100+
sequential requests with no overlap (D2, `lens-efficiency.md`).

**Why this weakens submission** — Structural waste on the widget's primary hot path. The fix is
well-understood (bounded concurrency) but crosses a real risk boundary: the four store-name caches
(now on `StoreNameLookup`) are still not thread-safe and currently rely on this exact sequencing to
stay race-free.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Bound concurrency (e.g. `SemaphoreSlim(4-8)` + `Task.WhenAll`) around
the per-entry body, and switch `StoreNameLookup`'s `gogNameCache`/`epicNameCache`/`nameMatchCache`/
`ubisoftGameLookupCache` to `ConcurrentDictionary` *before* parallelizing — do not parallelize
without that change, or the caches race.

**Blast radius** — Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync`),
`Services/Stores/StoreNameLookup.cs` (the four cache fields). Avoid: `Services/Artwork/*`,
`Services/SteamGridDB/*`.

---

### Finding #3: Hand-rolled double-checked-locking cache pattern duplicated between AppliedArtworkStore and EpicLibrary

**Why it matters** — The same ~25-line lazy-load-with-gate skeleton was written twice by hand
instead of once; a future third cache (the store-name caches in F1/F3) would make it three.

**What is wrong** — `AppliedArtworkStore.LoadAsync` (`AppliedArtworkStore.cs:95-147`) and
`EpicLibrary.LoadAsync` (`EpicLibrary.cs:67-89`) both implement: check-null, await `SemaphoreSlim`
gate, re-check-null, populate, release — identical structure, no shared helper. Unaffected by this
loop's fix, which targeted store-name resolution in a different file (`StoreNameLookup.cs`'s
`LoadUbisoftGameListAsync` uses a simpler unlocked lazy-init, not this pattern, so no third copy was
introduced).

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

- **Structurally necessary:** Moving store-name-resolution methods and their four cache fields out
  of `PrimaryWidget.xaml.cs` into `Services/Stores/StoreNameLookup.cs` — closes the third of F1's
  four merged concerns; the smallest honest fix is a pure relocation, reusing the exact code (no new
  abstraction, no new ceremony).
- **New seam justified:** false — no new Seam; `StoreNameLookup` is a plain `internal static class`
  alongside its existing sibling `EpicLibrary`, not a port/interface.
- **Helpful simplification:** Reader locality for "how does the widget resolve a third-party
  store's game name" improved from "search a 2,500-line UI file" to "read one 263-line file."
- **Should NOT be done:** Encapsulating the GOG/Epic caching logic inside `StoreNameLookup` this
  loop — the original code's cache-hit/cache-miss branching treats an empty cached value as
  "needs refetch" (`PrimaryWidget.xaml.cs:599`'s `|| string.IsNullOrEmpty(gogName)` check); a naive
  wrapper method that returns on any cache hit (including empty) would silently change retry
  behavior with no test oracle to catch the difference. Deferred to a Deepening Candidate for a
  future loop that can verify it carefully.
- **Tests after fix:** No test project exists (standing instruction); `MSBuild` compile is the only
  regression oracle. This is a pure code-motion refactor — Meta-Rule 4 does not apply: no
  isolation/`Sendable`/conditional-compilation/cross-file-visibility/lock-ordering boundary was
  crossed. Visibility was deliberately *widened* (private → internal), not narrowed, for exactly
  the two fields (`gogNameCache`, `epicNameCache`) that need cross-file access — the opposite
  direction from the accidental-visibility-loss case Meta-Rule 4 warns about.

## Improvement Backlog

1. **Continue the PrimaryWidget.xaml.cs break-up (F1, next slice): backup/restore orchestration**
   — move `RestoreBackupCoreAsync`, `RestoreBackupAsync`, and the `RestoreBackupResult` enum out of
   `PrimaryWidget.xaml.cs`, OR close the split-cache-ownership residual left in
   `StoreNameLookup.cs` by this loop (see Deepening Candidates) — whichever the next loop's friction
   check favors. Backup/restore is more UI-entangled than store-name resolution was, so plan
   carefully before attempting a same-shape mechanical move.
   - Why it matters: F1 remains the largest Serious deduction on the board; two concerns are still
     merged in the god-class the churn signal flagged.
   - Score impact: Architecture quality +0.5, Code simplicity +0.5 once verified (backup/restore
     slice); or a smaller Code simplicity win from closing the cache-ownership residual.
2. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups** (F3), *after*
   switching the four static name-resolution caches (now on `StoreNameLookup`) to
   `ConcurrentDictionary` — structural, helpful.
   - Why it matters: removes load latency that scales linearly with library size on the widget's
     primary flow.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified.
3. **Extract a shared AsyncLazy-style cache helper for AppliedArtworkStore and EpicLibrary** (F4)
   — simplification, helpful.
   - Why it matters: collapses two hand-copied lazy-load-with-gate skeletons into one owner before
     a third makes it three.
   - Score impact: Code simplicity +0.5 once verified.

## Deepening Candidates

- **Candidate Module:** `Services/Stores/StoreNameLookup` — fold the GOG/Epic name-caching logic
  inside it, matching the pattern the same class already uses for Ubisoft/name-match.
  - Source friction proven: Finding #1's residual evidence — `gogNameCache`/`epicNameCache` are
    `internal` fields on `StoreNameLookup` (`StoreNameLookup.cs:27-28`) written and read only by
    `PrimaryWidget.xaml.cs` (`:597-613`, `:616-630`), while `nameMatchCache`/`ubisoftGameLookupCache`
    (same class) are `private` and the cache-check-then-fetch logic lives inside `StoreNameLookup`'s
    own methods (`FindGameByNameAsync`, `GetUbisoftGameNameAsync`) — an inconsistent Module where
    two of its four caches are genuinely owned and two are just storage for an external caller.
  - Why shallow/misplaced: `gogNameCache`/`epicNameCache` expose internal mutable state as a bare
    Interface (a `Dictionary` directly, not a method) — Interface ≈ Implementation for that slice,
    the shallow-module test's own definition of shallow.
  - Behaviour to move behind the deeper Interface: the check-cache/fetch/populate-cache logic
    currently inline in `LoadGameEntriesAsync` (`PrimaryWidget.xaml.cs:597-613`,`:616-630`) should
    become `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync` methods on `StoreNameLookup`,
    mirroring `GetUbisoftGameNameAsync`'s shape, with `gogNameCache`/`epicNameCache` becoming
    `private`.
  - Dependency category: `in-process`
  - Test surface after change: none (no test project; build-verified only, same as today)
  - Smallest first step: port GOG's caching first — it has no secondary fallback source to
    preserve (Epic's path also calls `EpicLibrary.GetDisplayNameAsync` first via `??`, which adds a
    second thing the wrapper must get right).
  - What not to do: do not change the "empty cached value still means refetch" semantics
    (`PrimaryWidget.xaml.cs:599`) while doing this — a wrapper that treats any cache hit
    (including an empty one) as final would silently change retry behavior. This is exactly why
    this loop's fix left it alone rather than encapsulating it under time pressure with no test
    oracle to catch the difference.

## Builder Notes

1. **Pattern:** A mechanical move (relocate code, qualify call sites) is a different, safer class of
   refactor than an encapsulating move (relocate code AND change how callers use it) — only the
   first is safe without a test oracle.
   - How to recognize: if the fix can be described as "cut these lines, paste them in a new file,
     add a class-name prefix at each call site" with the pasted lines byte-identical to the
     original, it is mechanical. If describing the fix requires "and now the caller doesn't need to
     check X first" or "and the wrapper handles the empty case," it is encapsulating — a different,
     higher-risk category.
   - Smallest coding rule: in a codebase with no test suite, prefer the mechanical move now and file
     the encapsulating move as a Deepening Candidate with its behavior-preservation trap named
     explicitly, rather than attempting both in one loop.
   - Stack example: C# — this loop moved `GetGogGameNameAsync` verbatim into `StoreNameLookup.cs`
     (mechanical) but explicitly deferred folding `gogNameCache`'s check-then-fetch logic into the
     same class (encapsulating — the empty-string-means-refetch branch in
     `PrimaryWidget.xaml.cs:599` is easy to flatten incorrectly).

2. **Pattern:** Extracting one slice of a multi-concern god-class narrows Locality for the
   *extracted* concern immediately, even before the whole file is broken up.
   - How to recognize: a single file's method list spans several unrelated verb groups (fetch, parse,
     click-handle, restore) with no sub-namespacing; pick the verb group with the fewest external
     dependencies (here: store-name fetch methods touched only a shared `HttpClient` and static
     caches, no `Dispatcher`/UI state) as the first slice — it is the lowest-risk mechanical move.
   - Smallest coding rule: rank remaining god-class slices by how many UI/framework types
     (`Dispatcher`, `StatusText`, view-bound collections) their methods touch; extract the
     zero-UI-dependency slices first.

3. **Pattern:** Churn concentrates where concerns are merged (unchanged from loops 1-2 — still
   live, now measuring a smaller file).
   - How to recognize: one file dominates the six-month edit count and is several times larger than
     everything else, even after partial extraction.
   - Smallest coding rule: when a file's edit count and size both dominate the repo, extract the
     concern that changed rather than adding to the pile.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. `architecture_quality` moving UP to 5.5 rather than staying SAME — loop 1's structurally
   equivalent move (extracting the artwork-ranking cluster, one of four concerns, into its own
   file) scored SAME, not UP. I judged this loop's move differently because it leaves fewer
   concerns remaining (2 of 4, cumulative) than loop 1's did (3 of 4) — a stricter reviewer could
   reasonably hold both loops to the identical bar and keep this SAME too.
2. `data_flow` moving UP to 6.0 — the true-external network calls left the UI class, which is real,
   but no port/interface was introduced and the calls are still made directly from a concrete
   static class with no seam; a stricter reviewer could argue "moved to a different concrete class"
   doesn't yet earn a data-flow-dimension credit distinct from the architecture-quality credit
   already given for the same diff.
3. The Authority Map's "Single and clear today" verdict for the Store-name-resolution-caches
   concern, despite `gogNameCache`/`epicNameCache` now living in a class that doesn't itself write
   them — I judged this as an Interface-coherence gap (captured in Finding #1 + the Deepening
   Candidate) rather than a write-authority split, but a reviewer weighting Module boundaries over
   call-site count could reasonably call this "Split and ambiguous" instead.

## Final Judge Narrative

Place, not win. This loop closed the third of `PrimaryWidget.xaml.cs`'s four originally-merged
concerns with a pure, build-verified relocation — no new abstraction, no new ceremony, the moved
code byte-identical to its source. That is real, if incremental, progress: `architecture_quality`
and `data_flow` both moved up on structural proof, and `code_simplicity` improved because one
specific behavior (store-name resolution) now has a single dedicated file to read instead of a
god-class search. But `PrimaryWidget.xaml.cs` still merges UI event handling and backup/restore
orchestration with no Interface boundary (F1, still Serious), `LoadGameEntriesAsync` still issues
one sequential network round-trip per game on the widget's primary flow (F3), the duplicate
lazy-cache skeleton (F4) is untouched, and this loop's own fix leaves a smaller residual (two of
`StoreNameLookup`'s four cache fields are owned-in-name-only by that class). Runtime ownership
remains trustworthy for what has been resolved (Library-operation exclusivity, Applied-artwork
record both Single and clear); concurrency is not yet trustworthy (F3 open). Tests remain absent by
standing instruction, so regression resistance is unverifiable beyond compile-check and manual diff
inspection — honestly reflected in the unchanged `test_strategy` score. Future work risks
overengineering specifically at the Deepening Candidate above: folding GOG/Epic caching into
`StoreNameLookup` needs the empty-cache-means-refetch semantics preserved exactly, not "cleaned up"
into a different retry behavior.

## Loop 3 Result

Closed the third of F1's four originally-merged concerns: moved `GetGogGameNameAsync`,
`GetEpicGameNameAsync`, `LoadUbisoftGameListAsync`, `GetUbisoftGameNameAsync`, `FindGameByNameAsync`,
`NormaliseGameName`, and their four cache fields out of `PrimaryWidget.xaml.cs` into a new
`Services/Stores/StoreNameLookup.cs`, qualifying the five call sites in `LoadGameEntriesAsync` with
the new class name, and registering the new file in `SteamGridDB.Xbox.csproj`. `git diff --stat`:
`PrimaryWidget.xaml.cs` (8 insertions, 239 deletions), `SteamGridDB.Xbox.csproj` (+1 line),
`Services/Stores/StoreNameLookup.cs` (new, 263 lines).

**What proves the change is honest:** `MSBuild` (the sole regression oracle per Discovery — no test
project exists) passed clean both before and after the change (exit 0 both times, same command as
loops 1-2's baseline). The diff is a pure move with one disclosed, mechanical field-reference edit:
five of the six moved method bodies are byte-identical to their pre-move source; the three methods
that call GOG/Epic/Ubisoft (`GetGogGameNameAsync`, `GetEpicGameNameAsync`,
`LoadUbisoftGameListAsync`) have their `sharedHttpClient` reference changed to `httpClient`, a new
dedicated field on `StoreNameLookup`, so a `Services/Stores` type does not depend back on the UI
class for a shared instance. The qualifying `StoreNameLookup.` prefix was also added at the five
call sites in `LoadGameEntriesAsync`. This changes which `HttpClient` object instance issues the
same three store-lookup HTTP calls, not the number of calls, retry behavior, or selection semantics
— confirmed by the independent implementation-reviewer pass (`verdict: approved`;
reality/honesty/regression all `passed`), which flagged the "byte-identical" phrasing in an earlier
draft of this paragraph as understating the disclosed field-reference edit; corrected here.

**Risk boundary evidence (Meta-Rule 4):** none — no isolation/`Sendable`/conditional-compilation/
cross-file-visibility/lock-ordering boundary was crossed. The one visibility change
(`gogNameCache`/`epicNameCache` from `private` to `internal`) *widened* access deliberately for
exactly the two fields that need cross-file reads/writes; it did not narrow or accidentally drop
access the way Meta-Rule 4's cross-file-visibility risk describes. `loop_result.risk_boundary_evidence`
is `null`.

**Targeted finding status:** `carried_forward` — F1 (F-001) as evidenced (three merged concerns) is
reduced to two (UI event handling, backup/restore orchestration); it is not gone from current
source, so it remains open for the next loop.

**Unintended scorecard regression:** none observed. `architecture_quality`, `data_flow`, and
`code_simplicity` moved UP on structural proof; no other dimension regressed.

## Loop 3 Implementation Review

`verdict: approved` — "The six named methods and four cache fields are verifiably gone from
PrimaryWidget.xaml.cs and correctly relocated to StoreNameLookup.cs with all call sites properly
qualified, the move is honest (method bodies match except a disclosed, behavior-neutral HttpClient
field swap and the deliberate private-to-internal widening), and no same-or-higher-severity
regression was introduced by the move itself." All three checks (`reality`, `honesty`,
`regression`) `passed`; `conditions: []`.

The reviewer noted two lower-severity, non-blocking observations in `regressions[]` (per Check 3's
own text: "a regression at lower severity is acceptable — note it, don't reject for it"):

1. `StoreNameLookup.cs:27-28` — `gogNameCache`/`epicNameCache` are `internal` fields whose
   check-cache/fetch/populate logic still lives outside the class
   (`PrimaryWidget.xaml.cs:597-613,616-630`) — the same shallow-module residue already disclosed as
   Finding #1 evidence and the matching Deepening Candidate. Independently confirmed, not new
   information.
2. `StoreNameLookup.cs:37-39` — a second static `HttpClient` singleton now exists alongside
   `PrimaryWidget`'s `sharedHttpClient`; functionally inert, but the reviewer caught that this
   loop's original `evidence_change_is_honest` text overstated the moved methods as fully
   byte-identical when 3 of 6 had their `HttpClient` field reference changed. **Corrected** in the
   Loop 3 Result above and in `CURRENT_REVIEW.json.loop_result.evidence_change_is_honest` before
   commit.
