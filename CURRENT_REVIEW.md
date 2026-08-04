### Loop Counter

Loop 2 of 10 (cap)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Independent re-derivation from current source (unchanged since `f47dcad`, loop 1's commit) confirms
the prior verdict on its own evidence rather than by anchoring to it: `PrimaryWidget.xaml.cs`
(2,530 LOC, three of the original four merged concerns still present after loop 1's artwork-ranking
extraction) remains the leaky seam the churn signal flagged, and `AppliedArtworkStore`'s read/write
lock asymmetry (F2) is still live in source at Step 1 inspection time. This loop closes F2.

## Scorecard (1-10)

- **Architecture quality:** 5.0 | SAME | `PrimaryWidget.xaml.cs` (2,530 LOC, re-measured this loop)
  still merges UI event handling, third-party store-name resolution
  (`PrimaryWidget.xaml.cs:2256-2472`), and backup/restore orchestration
  (`RestoreBackupCoreAsync`, `:2166-2247`) with no Interface separating them — unchanged since loop
  1, whose extraction of the artwork-ranking cluster addressed only one of the four original
  concerns. Not touched this loop (F2's fix is confined to `AppliedArtworkStore.cs`).
- **State management and runtime ownership:** 6.5 | UP | `AppliedArtworkStore.GetAsync`
  (`AppliedArtworkStore.cs:38-59`) now holds `gate` around its `TryGetValue` read, the same
  semaphore `UpdateAsync` (`:149-180`) already held around its read-modify-write of the identical
  `Dictionary` instance — closing the Applied-artwork-record Authority Map entry from "Split and
  ambiguous" to "Single and clear" (see Authority Map below). Structural proof: `AppliedArtworkStore.cs`
  diff this loop, lines 47-58 (new `await gate.WaitAsync(); try { ... } finally { gate.Release(); }`
  wrapping the existing return statement) — source the prior loop did not have. `isLibraryOperationRunning`
  remains a clean single owner, unaffected.
- **Domain modeling:** 5.5 | SAME | Unaffected by this loop's change; `SteamGridDbGame.OfficialCapsuleUrl`
  still hand-parsed in `SteamGridDbClient.ParseOfficialCapsuleUrl` (`SteamGridDbClient.cs:144-199`)
  outside the DTO's own deserialization, verified unchanged this loop.
- **Data flow and dependency design:** 5.5 | SAME | GOG/Epic/Ubisoft network calls still instantiated
  directly inside `PrimaryWidget`'s private methods via the shared static `sharedHttpClient`
  (`PrimaryWidget.xaml.cs:81`, fetch methods at `:2256-2472`), unchanged this loop — F1's remaining
  scope.
- **Framework / platform best practices:** 6.0 | SAME | Unaffected by this loop's change. Two JSON
  idioms (`DataContractJsonSerializer` vs. ad hoc `Windows.Data.Json`) still coexist.
- **Concurrency and runtime safety:** 5.5 | UP | The same `AppliedArtworkStore.cs` fix that raised
  State management also closes F2's concurrency hazard (a `Dictionary` read racing a `Dictionary`
  write is exactly as unsafe as two racing writes — now fixed by matching lock discipline).
  F3 (`LoadGameEntriesAsync`'s fully sequential per-game round-trips, `PrimaryWidget.xaml.cs:401-705`)
  remains open and caps this dimension below 6.0: the four static name-resolution caches
  (`gogNameCache`, `epicNameCache`, `nameMatchCache`, `ubisoftGameLookupCache`,
  `PrimaryWidget.xaml.cs:73-80`) are still plain, non-thread-safe `Dictionary`, still unaddressed.
- **Code simplicity and clarity:** 5.5 | SAME | This loop's fix (12 lines added, 1 removed in
  `AppliedArtworkStore.cs`) mirrors `UpdateAsync`'s existing lock pattern exactly — no new
  abstraction, no ceremony, but no consolidation win either (F4's duplicate lazy-cache skeleton is
  untouched). Net neutral for this dimension.
- **Test strategy and regression resistance:** 3.0 | SAME | No test project exists; standing
  user instruction prohibits adding one this run. Verified unchanged this loop
  (no `*.csproj` matching a test name found; `SteamGridDB.Xbox.sln` project list unchanged).
  Named, non-backlog-item blocker, as recorded loop 1.
- **Overall implementation credibility:** 5.5 | SAME | Unaffected by this loop's change. The three
  store-name-resolution methods that swallow failures via `Debug.WriteLine` only
  (`GetGogGameNameAsync`, `GetEpicGameNameAsync`, `LoadUbisoftGameListAsync`/`GetUbisoftGameNameAsync`,
  `PrimaryWidget.xaml.cs:2256-2472`) are unchanged.

## Authority Map

(Re-emitted this loop: the Applied-artwork-record concern was this loop's Priority 1 target.)

- **Concern:** Library-operation exclusivity (`isLibraryOperationRunning`)
  - Owner: `PrimaryWidget` instance
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Observers / readers: `IsLibraryOperationBlocking`, all four header-button click handlers,
    `EditGameImage_Click`, `SearchGameImage_Click`, `RestoreBackup_Click`
  - Persistence seam: none (in-memory only)
  - Async mutation entry points: `TryBeginLibraryOperation`/`EndLibraryOperation`, called from
    every `*_Click` handler via a try/finally
  - Verdict: **Single and clear**

- **Concern:** Applied-artwork record (`AppliedArtworkStore.applied`)
  - Owner: `AppliedArtworkStore` (static, `Services/Artwork/`)
  - Allowed writers: `UpdateAsync` (via `SetAsync`/`ClearAsync`), gated by `gate`
    (`AppliedArtworkStore.cs:149-180`)
  - Observers / readers: `GetAsync` — **now also gated by `gate`** (`AppliedArtworkStore.cs:38-59`,
    this loop's fix)
  - Persistence seam: `applied-artwork.json` in `ApplicationData.Current.LocalFolder`
  - Async mutation entry points: `SetAsync` (from `ReplaceImageCoreAsync`), `ClearAsync` (from
    `RestoreBackupCoreAsync`)
  - Verdict: **Single and clear** (promoted from "Split and ambiguous" this loop — F2 resolved)

- **Concern:** Store-name resolution caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`,
  `ubisoftGameLookupCache`)
  - Owner: `PrimaryWidget` static fields (conceptually a store-resolution concern, physically on
    the UI class — F1 remaining scope)
  - Allowed writers: the matching `Get*NameAsync`/`FindGameByNameAsync` method, only from within
    `LoadGameEntriesAsync`'s sequential per-entry loop
  - Observers / readers: the same methods (`TryGetValue` before writing)
  - Persistence seam: none (in-memory, per-process)
  - Async mutation entry points: inside `LoadGameEntriesAsync`'s per-entry body
  - Verdict: **Single and clear today** — unchanged caveat from loop 1: this holds only because
    F3's sequential-loop defect is also the only thing keeping these non-thread-safe `Dictionary`s
    race-free. Parallelizing F3 without converting these to `ConcurrentDictionary` first would
    turn this entry Split and ambiguous.

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design
  (`Services/SteamGridDB/ArtworkSource.cs:15-51`) makes "neither a platform-ID nor a game-ID"
  unrepresentable — a genuine smart-constructor, not decoration. Re-verified unchanged this loop.
- The official-artwork gate (`FindOfficialLookalikeAsync`, `PrimaryWidget.xaml.cs:1444-1515`) is a
  narrow, evidence-tuned veto whose code comments cite the specific regression case (a `Mad Max`
  false-positive at a 0.51 match) and the exact slack margin that motivated the floor/ceiling split.
  Re-verified unchanged this loop.
- This loop's fix mirrors an existing pattern rather than inventing a new one:
  `AppliedArtworkStore.GetAsync` now reuses the exact `gate`/`try`/`finally` shape `UpdateAsync`
  already used, so the Module gained a correct invariant (same lock covers every access path to
  `applied`) without a new synchronization primitive or abstraction.

## Findings

### Finding #1: PrimaryWidget.xaml.cs merges three unrelated concerns behind zero Interface boundary

**Why it matters** — The file the churn signal flagged as the leaky seam (21 edits, 2.6x the next
file as of loop 1's discovery) still has no module boundary separating UI orchestration from
business logic for three of the original four concerns, so a change to any one risks touching the
others.

**What is wrong** — UI event handling, third-party manifest/store-name resolution
(GOG/Epic/Ubisoft), and file backup/restore orchestration remain private methods on one 2,530-line
`Page`-derived class with no Interface separating them. The fourth original concern
(artwork ranking) was extracted to `Services/Artwork/ArtworkRanker.cs` in loop 1 and is resolved.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-749` (`LoadGameEntriesAsync` — manifest parsing +
  store-name resolution inlined)
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:2256-2472` (GOG/Epic/Ubisoft store-name fetch methods +
  `FindGameByNameAsync` — remaining F1 scope, re-verified at current line numbers this loop)
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:2166-2247` (`RestoreBackupCoreAsync` — backup/restore
  orchestration)
- `REVIEW_HISTORY.json loops[0].discovery.churn_top20` (`PrimaryWidget.xaml.cs`: 21 edits vs. 8 for
  the next file — no new commits touching this file since loop 1, so unchanged)

**Architectural test failed** — n/a — different category (ownership/coupling sprawl across an
undifferentiated class, not a removable Seam or wrapper)

**Dependency category** — `true-external` (GOG API, Epic community DB, Ubisoft GitHub-hosted
README are true-external dependencies instantiated directly inside this UI class with no port)

**Leverage impact** — Every future store-name fix or UI change still touches the same file.

**Locality impact** — A maintainer fixing a UI bug still reads through ~2,500 lines including
unrelated network-parsing code to find the relevant lines.

**Metric signal** — `PrimaryWidget.xaml.cs`: 2,530 LOC (re-measured this loop, down from 2,722 at
loop 1's start after loop 1's extraction), still the largest file by a wide margin.

**Why this weakens submission** — The module graph is not enforced by source in the largest file;
ownership of the three remaining distinct concerns is untraceable from any single Module.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Move store-name resolution (`GetGogGameNameAsync`,
`GetEpicGameNameAsync`, `LoadUbisoftGameListAsync`, `GetUbisoftGameNameAsync`,
`FindGameByNameAsync`, `NormaliseGameName`, plus the four cache fields) into `Services/Stores/`,
alongside the existing `EpicLibrary.cs`. Backup/restore orchestration is the last remaining slice
after that.

**Blast radius** — Change: `PrimaryWidget.xaml.cs`, `Services/Stores/*` (new file). Avoid:
`Services/SteamGridDB/*`, `Services/Artwork/*`.

---

### Finding #2: AppliedArtworkStore.GetAsync reads the shared cache without the lock that guards its writer

**Why it matters** — A non-thread-safe `Dictionary` read concurrently with an in-place write can
throw or corrupt internal state; the picker's "which artwork is applied" marker can silently go
wrong exactly when a fix operation is writing to the same store.

**What is wrong** — At Step 1 inspection time (source identical to loop 1's committed state),
`GetAsync` (`AppliedArtworkStore.cs:38-48`, pre-fix line numbers) called `TryGetValue` on the
shared `applied` dictionary after `LoadAsync` returned, without acquiring `gate`, while
`UpdateAsync` (`AppliedArtworkStore.cs:138-169`, pre-fix line numbers) held `gate` only around its
own read-modify-write of the very same dictionary instance — the lock protected the writer against
other writers but not readers against the writer. **This loop fixes it — see Loop 2 Result.**

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:38-48` (pre-fix)
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:138-169` (pre-fix)

**Architectural test failed** — n/a — different category (concurrency/ownership hazard, not a
Seam)

**Dependency category** — n/a (not a Coupling & Leakage finding)

**Leverage impact** — Callers cannot rely on `GetAsync` being safe to call concurrently with any
`Set`/`Clear`, so every call site would otherwise have to independently reason about an ordering
the Module itself should guarantee.

**Locality impact** — The hazard is contained to this one Module; fixing it here fixes every
caller at once.

**Metric signal** — none

**Why this weakens submission** — A concurrency hazard in a Module every artwork-write path
depends on is a real ownership defect, even though it has not yet been observed to fire.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Hold `gate` around the `TryGetValue` in `GetAsync` too (or snapshot
`applied` under the gate before releasing it), matching the pattern `UpdateAsync` already uses.

**Blast radius** — Change: `Services/Artwork/AppliedArtworkStore.cs`. Avoid: everything else.

---

### Finding #3: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** — Load time scales linearly with library size and network latency on the
widget's primary open path — the one flow every user hits every time.

**What is wrong** — The `gameCache` `foreach` loop in `LoadGameEntriesAsync` awaits
`sgdbClient.GetGameByPlatformIdAsync` (and the GOG/Epic/Ubisoft name fallbacks) one game at a
time; nothing overlaps the independent per-game network calls.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:401-705` (per-folder, per-entry `foreach`; awaits
  `GetGameByPlatformIdAsync` at `:585` and the store-name fallbacks at `:609,631,646,666`, with
  nothing overlapped — re-verified at current line numbers this loop)

**Architectural test failed** — n/a — different category (D2, structural waste per
`lens-efficiency.md`, not a Seam)

**Dependency category** — `true-external`

**Leverage impact** — There is only one call site (the load loop); a future second caller of the
same pattern would inherit the same linear cost with no leverage from batching, since none exists.

**Locality impact** — The fix is local to `LoadGameEntriesAsync`'s loop body; it does not need to
spread to callers.

**Metric signal** — One HTTP round-trip per game per store lookup; a 100-game library issues 100+
sequential requests with no overlap (D2, `lens-efficiency.md`).

**Why this weakens submission** — Structural waste on the widget's primary hot path. The fix is
well-understood (bounded concurrency) but crosses a real risk boundary: `gogNameCache`,
`epicNameCache`, `nameMatchCache`, and `ubisoftGameLookupCache` are plain `Dictionary`, not
thread-safe, and currently rely on this exact sequencing to stay race-free (see Authority Map).

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Bound concurrency (e.g. `SemaphoreSlim(4-8)` + `Task.WhenAll`)
around the per-entry body, and switch the four static caches to `ConcurrentDictionary` *before*
parallelizing — do not parallelize without that change, or the caches race.

**Blast radius** — Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync` + the four static
cache field declarations). Avoid: `Services/*`.

---

### Finding #4: Hand-rolled double-checked-locking cache pattern duplicated between AppliedArtworkStore and EpicLibrary

**Why it matters** — The same ~25-line lazy-load-with-gate skeleton was written twice by hand
instead of once; a future third cache (the store-name caches in F1/F3) would make it three.

**What is wrong** — `AppliedArtworkStore.LoadAsync` (`AppliedArtworkStore.cs:95-147`, current line
numbers post-F2-fix) and `EpicLibrary.LoadAsync` (`EpicLibrary.cs:67-89`) both implement:
check-null, await `SemaphoreSlim` gate, re-check-null, populate, release — identical structure,
no shared helper. F2's fix (this loop) touched `GetAsync`, a different method in the same file;
`LoadAsync`'s duplicated skeleton is untouched.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:95-147`
- `SteamGridDB.Xbox/Services/Stores/EpicLibrary.cs:67-89`

**Architectural test failed** — n/a — different category (leaf-module duplication, not a Seam)

**Dependency category** — n/a

**Leverage impact** — A shared lazy-cache primitive would pay for itself across at least these
two call sites, and the four `PrimaryWidget` caches touched by F1/F3 later.

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
`Services/Stores/EpicLibrary.cs`, one new small internal helper. Avoid: `PrimaryWidget.xaml.cs`.

## Simplification Check

- **Structurally necessary:** Widening `AppliedArtworkStore.GetAsync`'s lock coverage to match
  `UpdateAsync` (F2) — closes a real read/write race; the smallest honest fix reuses the semaphore
  already scoped to the exact data it protects, adding no new primitive.
- **New seam justified:** false — no new Seam; the fix is a lock-scope correction inside an
  existing Module.
- **Helpful simplification:** none this loop (F4's duplicate lazy-cache skeleton remains a
  deepening candidate below, not addressed this loop to keep the fix minimal and reviewable).
- **Should NOT be done:** Introduce a `ReaderWriterLockSlim`, a new cache wrapper type, or an
  `IAppliedArtworkStore` interface for this fix — one `SemaphoreSlim` already correctly scoped to
  the one `Dictionary` it protects is the whole fix; anything more is ceremony the Simplify
  Pressure Test rejects (Q2 — not the smallest honest fix).
- **Tests after fix:** No test project exists (standing user instruction); `MSBuild` compile is
  the only regression oracle. This fix is a lock-ordering change (Meta-Rule 4 risk boundary) with
  no mechanical race-detection tooling available for this UWP/C# stack in this environment — see
  Loop 2 Result `risk_boundary_evidence`.

## Improvement Backlog

1. **Continue the PrimaryWidget.xaml.cs break-up (F1, next slice): store-name resolution** —
   move `GetGogGameNameAsync`, `GetEpicGameNameAsync`, `LoadUbisoftGameListAsync`,
   `GetUbisoftGameNameAsync`, `FindGameByNameAsync`, `NormaliseGameName`, and the four cache
   fields into `Services/Stores/`, alongside the existing `EpicLibrary.cs` — structural, needed
   for winning.
   - Why it matters: removes the largest remaining slice of F1 (Serious deduction — higher
     contest impact than F3/F4's Noticeable weakness, hence reordered to Priority 1 now that F2
     is resolved).
   - Score impact: Architecture quality +0.5, Code simplicity +0.5 once verified.
2. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups** (F3), *after*
   switching the four static name-resolution caches to `ConcurrentDictionary` — structural,
   helpful.
   - Why it matters: removes load latency that scales linearly with library size on the widget's
     primary flow.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified.
3. **Extract a shared AsyncLazy-style cache helper for AppliedArtworkStore and EpicLibrary** (F4)
   — simplification, helpful.
   - Why it matters: collapses two hand-copied lazy-load-with-gate skeletons into one owner before
     a third (the store-name caches) makes it three.
   - Score impact: Code simplicity +0.5 once verified.

## Deepening Candidates

- **Candidate Module:** An `AsyncLazy`-style cache helper for `AppliedArtworkStore` +
  `EpicLibrary` (and eventually the `PrimaryWidget` store-name caches).
  - Source friction proven: F4 — identical double-checked-locking skeleton hand-copied between
    `AppliedArtworkStore.cs:95-147` and `EpicLibrary.cs:67-89` (re-verified at current line
    numbers this loop after F2's fix shifted `AppliedArtworkStore.cs`).
  - Why shallow/misplaced: each Module re-implements the same generic "lazily load once behind a
    gate" behaviour instead of delegating to one small internal owner of that pattern.
  - Behaviour to move behind the deeper Interface: check-null, `gate.WaitAsync`, re-check-null,
    populate-via-factory, `gate.Release`.
  - Dependency category: `in-process`
  - Test surface after change: no test project (standing instruction); build-verified only, same
    as today.
  - Smallest first step: write the helper against `AppliedArtworkStore` first (its `GetAsync`/
    `LoadAsync`/`UpdateAsync` triangle is now internally consistent after F2, giving the cleanest
    base to extract from), then `EpicLibrary` once the shape is proven.
  - What not to do: do not generalize this into a public caching framework or add a second
    production adapter to justify a Seam — two internal call sites do not need an interface, just
    a shared internal type.

## Builder Notes

1. **Pattern:** The smallest fix for a lock-coverage gap reuses the lock that already exists —
   it does not invent a new primitive.
   - How to recognize: a `SemaphoreSlim`/`lock` already wraps every write to a shared mutable
     collection; the safe fix for an unguarded reader is wrapping it with the *same* handle, not a
     `ReaderWriterLockSlim`, a new queue, or an actor-style wrapper type.
   - Smallest coding rule: before reaching for a new synchronization primitive, check whether an
     existing one is already scoped to exactly the data in question — if so, widen its coverage,
     don't add a second one.
   - Stack example: C# — `AppliedArtworkStore.GetAsync` this loop added `await gate.WaitAsync();
     try { ... } finally { gate.Release(); }` around its existing return statement, reusing the
     same `SemaphoreSlim` field `UpdateAsync` already held; zero new fields, zero new types.

2. **Pattern:** Churn concentrates where concerns are merged (unchanged from loop 1 — still live).
   - How to recognize: one file dominates the six-month edit count and is several times larger
     than everything else.
   - Smallest coding rule: when a file's edit count and size both dominate the repo, extract the
     concern that changed rather than adding to the pile.

3. **Pattern:** Sequential `await` in a per-item loop hides its own cost until the collection
   grows (unchanged from loop 1 — F3 still open).
   - How to recognize: a `foreach` loop with an `await` inside it, over items whose network calls
     don't depend on each other, no `Task.WhenAll`/bounded-parallel pattern nearby.
   - Smallest coding rule: before parallelizing, confirm what the loop body writes to outside
     itself is thread-safe first, or the "optimization" introduces the race the sequential version
     was accidentally preventing.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. `concurrency` moving UP to 5.5 rather than staying at 5.0 — uncertainty because F3 (the
   sequential-round-trip defect on the primary hot path) is arguably the dimension's dominant
   weakness, and a stricter reviewer could argue that closing one narrow, never-observed race
   while F3 remains fully open does not earn even a half-point move.
2. `state_management` at 6.5 — same reasoning in mirror: the store-name caches (a second,
   still-effectively-split state concern, per the Authority Map's "Single and clear today" caveat)
   remain untouched, so the +0.5 credits only one of two live state concerns in this dimension.
3. The `risk_boundary_evidence.verification: "reasoning_only"` call for F2's fix — uncertainty
   because this environment has no thread sanitizer or concurrency stress-test harness for
   C#/UWP, so "the lock now covers every access path" is verified by reading the code, not by
   executing a test that would fail if it were wrong; a reviewer with access to a stress-test
   tool could reasonably demand stronger evidence before crediting the fix at all.

## Final Judge Narrative

Place, not win. This loop closed a real, if narrow, concurrency hazard (F2) with the smallest
honest fix available — reusing `AppliedArtworkStore`'s existing lock rather than inventing new
synchronization machinery — and the Applied-artwork-record Authority Map entry is now genuinely
Single and clear. That is real progress, not cosmetic: `state_management` and `concurrency` both
moved up on structural proof. But `PrimaryWidget.xaml.cs` is still the churn-flagged god-class with
three of its original four concerns unseparated, `LoadGameEntriesAsync` still issues one sequential
network round-trip per game on the widget's primary flow (F3), and the duplicate lazy-cache
skeleton (F4) is untouched. Tests remain absent by standing instruction, so regression resistance
is unverifiable beyond a compile check for anything beyond what a fresh source read can confirm by
inspection — honestly reflected in the unchanged `test_strategy` score. Future work risks
overengineering only if F1's next slice reaches for an interface or DI container it does not need;
the Simplification Check above names that trap explicitly so the next loop does not walk into it.

## Loop 2 Result

Closed F2: `AppliedArtworkStore.GetAsync` (`Services/Artwork/AppliedArtworkStore.cs:38-59`) now
wraps its `TryGetValue` read in the same `gate.WaitAsync()`/`try`/`finally`/`gate.Release()` pattern
`UpdateAsync` (`:149-180`) already used around its read-modify-write of the identical `Dictionary`
instance. No other file touched; no new field, type, or abstraction added. `git diff --stat`:
`AppliedArtworkStore.cs | 13 +++++++++++-` (12 insertions, 1 deletion — the fix wraps the existing
one-line return statement).

**What proves the change is honest:** `MSBuild` (the sole regression oracle per Discovery — no
test project exists) passed clean after the change (exit 0, same command as loop 1's baseline,
run via the PowerShell tool after the Bash/Git-Bash tool mangled the `/p:` flags into path
fragments on this Windows host — a tooling quirk of this environment, not a build failure). The
diff is a pure lock-scope widening: the pre-existing return expression is untouched, only now
executed while `gate` is held, mirroring `UpdateAsync`'s established shape exactly.

**Risk boundary evidence (Meta-Rule 4):** this fix crosses a `lock_ordering` boundary (it adds a
new synchronous-with-writer guard around a previously-unguarded read). `{"boundary_kind":
"lock_ordering", "verification": "reasoning_only", "detail": "No thread sanitizer or concurrency
stress-test harness exists for this C#/UWP stack in this environment (no test project per standing
instruction; UWP has no TSAN-equivalent tool wired into this build). Verified by inspection: (1)
GetAsync's await LoadAsync() always completes and releases `gate` before GetAsync's own
gate.WaitAsync() call, so there is no self-deadlock — SemaphoreSlim(1,1) is not reentrant but these
are sequential, non-nested acquisitions; (2) every other access path to the `applied` Dictionary
(UpdateAsync's read-modify-write) already held the same `gate` before this fix, so GetAsync is the
only path that changed; (3) the fix changes only NEW code (lines 47-58, all additions) around an
unchanged return expression, so no other invariant moved.", "mechanically_testable": false}`
This is the smallest honest evidence available: a green single-config compile is deliberately not
claimed as proof (Meta-Rule 4 forbids that), and no stronger executable check exists for this
stack in this environment.

**Targeted finding status:** `resolved` — F2 as evidenced (the unguarded `TryGetValue` read racing
`UpdateAsync`'s guarded write) is gone from current source; `GetAsync` now takes the same lock on
every access path to `applied`.

**Unintended scorecard regression:** none observed. `state_management` and `concurrency` both
moved UP on structural proof; no other dimension regressed.

## Loop 2 Implementation Review

`verdict: approved` — "GetAsync now wraps its TryGetValue read in
gate.WaitAsync()/try/finally/gate.Release() matching UpdateAsync's existing lock scope, closing
the unguarded-read-vs-guarded-write race with a minimal in-place fix and no deadlock risk
(LoadAsync fully releases gate before returning, so GetAsync's own WaitAsync is sequential, not
nested)." All three checks (`reality`, `honesty`, `regression`) `passed`; `regressions: []`;
`conditions: []`.
