--- Loop 1 (UTC 2026-08-03T00:00:00Z) ---

### Discovery (first loop only)

- **Source roots:** `SteamGridDB.Xbox/` (single project, 4,976 LOC C#). Sub-roots: `Services/`
  (`Artwork/`, `SteamGridDB/`, `SteamGridDB/Models/`, `Stores/`), `Models/`, `Converters/`,
  `Properties/`. Root-level UI: `PrimaryWidget.xaml.cs` (2,722 LOC at loop start — 55% of all
  source), `App.xaml.cs`, `MainPage.xaml.cs`.
- **Test command:** **none — this repository has no test project.** Verified: no `*.csproj`
  matching a test name, no MSTest/xUnit/NUnit reference anywhere in the solution. Skipping tests
  is a standing instruction from the user in the originating session ("lets skip the tests"),
  which outranks this skill on the instruction-authority ladder. `test_scope: "full"`,
  `test_filter: null`.
- **Build command** (the ground-truth gate; `msbuild` is not on PATH, resolved via `vswhere`
  exactly as `deploy-dev.ps1` does):

  ```
  "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo
  ```

  Verified green at `92c598a` in 4 seconds (exit 0). Re-verified green after this loop's refactor.
- **ADRs found:** none. **Domain terms (CONTEXT.md):** none (absent).
- **Prior audit docs:** `CODE-REVIEW.md` (self-reports all 15 findings resolved via its own
  `## Status — all fixed` header; spot-checked 3 against current source, no falsifications) and
  `ARTWORK-SELECTION.md` (artwork-ranking-quality research log, out of this skill's architecture
  scope by definition; its one outstanding item is a product proposal the doc's own analysis
  already deprioritizes). No adopt-or-falsify action taken on either — see full loop-1
  `CURRENT_REVIEW.json.discovery` for the complete disposition.
- **Selected lens:** Generic (`lens-generic.md`) — C#/.NET/UWP matches no lens-registry row.
  Known gap (UWP-specific idioms the generic lens doesn't model) did not bite this loop.
- **Loaded lenses:** `["lens-generic.md", "lens-security.md", "lens-efficiency.md"]`
- **Working tree:** clean at Step 0. **Churn top-3:** `PrimaryWidget.xaml.cs` (21 edits),
  `SteamGridDbClient.cs` (8), `GameEntry.cs` (4) — full top-20 table in
  `REVIEW_HISTORY.json.loops[0].discovery.churn_top20`.
- **Preflight gate:** exit 0.

### Loop Counter

Loop 1 of 10 (cap)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Runtime ownership is mostly honest (a single clean `isLibraryOperationRunning` guard, real
observable domain types in `GameEntry`/`GridImageItem`) and several service modules
(`SteamGridDbClient`, `ArtworkSource`, `TileImage`) show genuine Depth. But the file the churn
signal already flagged as the leaky seam — `PrimaryWidget.xaml.cs`, 21 edits and 2.6x the next
file — merged UI orchestration with three unrelated business-logic concerns behind zero Interface
boundary, and there is no test suite (by standing instruction) to backstop any of it.

## Scorecard (1-10)

- **Architecture quality:** 5.0 | SAME | `PrimaryWidget.xaml.cs` (2,722 LOC at loop start, 55%
  of source) merged UI event handling, third-party manifest/store-name resolution, the
  artwork-ranking algorithm, and file backup/restore orchestration with no Interface separating
  any of them — contrasted with genuinely well-modularised siblings (`SteamGridDbClient.cs`,
  `ArtworkSource.cs`, `TileImage.cs`, `AppliedArtworkStore.cs`, `EpicLibrary.cs`), each with real
  Depth. The score averages a bimodal codebase rather than reading uniformly low or high.
- **State management and runtime ownership:** 6.0 | SAME | `isLibraryOperationRunning`
  (`PrimaryWidget.xaml.cs:242-272`) is a single, consistently-applied owner with a clean
  try/finally discipline; `GameEntry`/`GridImageItem` have honest single-owner backing fields.
  But `AppliedArtworkStore.GetAsync` (`AppliedArtworkStore.cs:38-48`) reads the shared `applied`
  dictionary without the lock that guards `UpdateAsync`'s in-place mutation of the same instance
  (`AppliedArtworkStore.cs:138-169`) — F2.
- **Domain modeling:** 5.5 | SAME | `GameEntry`/`GridImageItem` are real domain types with
  computed visibility properties; `GamePlatform`'s enum-to-string mapping lives beside the enum
  it serves (`GamePlatform.cs`). But `SteamGridDbGame.OfficialCapsuleUrl` is populated by
  hand-written parsing in the *client* (`SteamGridDbClient.cs:131,144-199`) rather than the DTO's
  own deserialization, because `DataContractJsonSerializer` cannot express the API's
  per-language-key shape — a domain interpretation living outside the type's home module.
- **Data flow and dependency design:** 5.5 | SAME | The dependency graph is acyclic in practice
  (`Models` ← `Services` ← root), but GOG/Epic-community-DB/Ubisoft-GitHub network calls were
  instantiated directly inside `PrimaryWidget`'s private methods via a shared static
  `HttpClient`, reached as ambient scope rather than threaded as an explicit collaborator
  (`PrimaryWidget.xaml.cs:2448-2664`, unchanged this loop — F1's remaining slices).
- **Framework / platform best practices:** 6.0 | SAME | UWP idioms are mostly used correctly:
  `Dispatcher.RunAsync` consistently marshals UI-thread work; the `XboxGameBarWidget` lifecycle
  in `App.xaml.cs` follows Microsoft's documented activation/suspend pattern with genuine
  explanatory comments. Deduction: two JSON idioms coexist without a chosen default —
  `DataContractJsonSerializer` for the SteamGridDB DTOs, ad hoc `Windows.Data.Json` walking for
  manifest reads, GOG/Epic responses, and the capsule-URL parse.
- **Concurrency and runtime safety:** 5.0 | SAME | Two concrete findings: F2 (an unguarded read
  against a guarded writer on a shared `Dictionary` in `AppliedArtworkStore`) and F3 (fully
  sequential per-game SteamGridDB round-trips in `LoadGameEntriesAsync`, `PrimaryWidget.xaml.cs:
  430-734`, D2 in `lens-efficiency.md`, with no bounded concurrency and no thread-safe backing
  for the four static name-resolution caches that would need to change first).
- **Code simplicity and clarity:** 5.5 | UP | `PrimaryWidget.xaml.cs` shed 208 net lines this
  loop (the artwork-ranking cluster moved to `Services/Artwork/ArtworkRanker.cs`, a pure,
  reviewer-approved relocation — see Loop 1 Result). Still weighed down by F1's remaining slices
  and F4 (the AppliedArtworkStore/EpicLibrary lazy-cache pattern hand-duplicated,
  `AppliedArtworkStore.cs:84-136` vs `EpicLibrary.cs:67-89`). Structural proof for the UP delta:
  `git diff --stat` this loop shows `PrimaryWidget.xaml.cs | 208 +-----` (208 lines removed net)
  against the new 207-line `ArtworkRanker.cs`, confirmed by the Implementation Review pass
  (`verdict: approved`, all three checks passed).
- **Test strategy and regression resistance:** 3.0 | SAME | No test project exists in the
  solution (verified: no `*.csproj` matching a test name, no MSTest/xUnit/NUnit reference
  anywhere) and a standing user instruction from the originating session prohibits adding one
  this run. Stateful/networking/persistence Modules (`AppliedArtworkStore`, `EpicLibrary`,
  `SteamGridDbClient`, the artwork-ranking algorithm) have zero test coverage — this matches the
  rubric's 3-anchor exactly ("stateful domain or runtime Modules lack meaningful tests"). This is
  a named, non-backlog-item blocker: the 9-anchor cannot be met while the no-test instruction
  stands, and the instruction outranks this skill on the authority ladder, so no backlog item is
  proposed for it.
- **Overall implementation credibility:** 5.5 | SAME | Doc comments throughout tie architecture
  decisions to measured outcomes with real specificity (e.g. the official-artwork gate's
  floor/ceiling constants cite a named regression case and an exact slack margin). That
  discipline does not extend evenly to observability: three of four store-name-resolution
  methods in `PrimaryWidget.xaml.cs` (`GetGogGameNameAsync`, `GetEpicGameNameAsync`,
  `LoadUbisoftGameListAsync`/`GetUbisoftGameNameAsync`) swallow failures with `Debug.WriteLine`
  only and never reach `FixLog`, unlike the SteamGridDB-match path in the very same method
  (`PrimaryWidget.xaml.cs:699-701`) which does.

## Authority Map

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
  - Observers / readers: `GetAsync` — **not gated**, the ambiguity F2 names
  - Persistence seam: `applied-artwork.json` in `ApplicationData.Current.LocalFolder`
  - Async mutation entry points: `SetAsync` (from `ReplaceImageCoreAsync`), `ClearAsync` (from
    `RestoreBackupCoreAsync`)
  - Verdict: **Split and ambiguous**

- **Concern:** Store-name resolution caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`,
  `ubisoftGameLookupCache`)
  - Owner: `PrimaryWidget` static fields (conceptually a store-resolution concern, physically on
    the UI class — part of F1's remaining scope)
  - Allowed writers: the matching `Get*NameAsync`/`FindGameByNameAsync` method, only from within
    `LoadGameEntriesAsync`'s sequential per-entry loop
  - Observers / readers: the same methods (`TryGetValue` before writing)
  - Persistence seam: none (in-memory, per-process)
  - Async mutation entry points: inside `LoadGameEntriesAsync`'s per-entry body
  - Verdict: **Single and clear today** — but only because the sequencing F3 flags as a
    performance defect also happens to be the only thing keeping these non-thread-safe
    `Dictionary`s race-free. Parallelizing F3 without addressing this Authority Map entry first
    would turn it into Split and ambiguous.

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design
  (`Services/SteamGridDB/ArtworkSource.cs:15-51`) makes "neither a platform-ID nor a game-ID"
  unrepresentable — a genuine smart-constructor, not decoration.
- The official-artwork gate (`FindOfficialLookalikeAsync`,
  `PrimaryWidget.xaml.cs:1444-1515` post-loop) is a narrow, evidence-tuned veto, not a ranking
  key, and the code comments cite the specific regression case (a `Mad Max` false-positive at a
  0.51 match) and the exact slack margin that motivated the floor/ceiling split — an
  architecture decision actually defended by measurement.
- `SteamGridDbClient.GetStringAsync` (`Services/SteamGridDB/SteamGridDbClient.cs:334-374`)
  distinguishes "the request failed" from "SteamGridDB returned zero results" all the way through
  the call chain (`null` vs. empty list), and `FixLibraryAsync`'s error/not-found counters
  (`PrimaryWidget.xaml.cs`) depend on that distinction to avoid miscounting a throttled run as an
  empty library.

## Findings

### Finding #1: PrimaryWidget.xaml.cs merges four unrelated concerns behind zero Interface boundary

**Why it matters** — The file the churn signal already flags as the leaky seam (21 edits, 2.6x
the next file, 55% of all source at loop start) has no module boundary separating UI
orchestration from business logic, so a change to any one concern risks touching the other three.

**What is wrong** — UI event handling, third-party manifest/store-name resolution
(GOG/Epic/Ubisoft), the artwork-ranking algorithm, and file backup/restore orchestration were all
private methods on one 2,722-line `Page`-derived class with no Interface separating them.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:361-778` (manifest parsing + store-name resolution
  inlined in `LoadGameEntriesAsync`)
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1707-1898` (artwork-ranking algorithm — `RankGrids`,
  `RankIcons`, `GridMetadata`, `RankedGrid` — location *before* this loop's extraction; now moved,
  see Loop 1 Result)
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:2448-2664` (GOG/Epic/Ubisoft name-fetch methods, still
  in place — remaining F1 scope)
- `CURRENT_REVIEW.json discovery.churn_top20` (`PrimaryWidget.xaml.cs`: 21 edits vs. 8 for the
  next file)

**Architectural test failed** — n/a — different category (ownership/coupling sprawl across an
undifferentiated class, not a removable Seam or wrapper)

**Dependency category** — `true-external` (the GOG API, Epic community DB, and Ubisoft
GitHub-hosted README are all true-external dependencies instantiated directly inside this UI
class with no port)

**Leverage impact** — Every future artwork-ranking tweak, store-name fix, or UI change touched
the same file, so no single change was ever isolated to its own concern.

**Locality impact** — A maintainer fixing a UI bug had to read through ~2,700 lines including
unrelated ranking-algorithm and network-parsing code to find the relevant 20 lines.

**Metric signal** — `PrimaryWidget.xaml.cs`: 2,722 LOC at loop start, 21 six-month edits (2.6x
next-highest file), 55% of all first-party source.

**Why this weakens submission** — The module graph is not enforced by source at all in the
largest file; ownership of four distinct concerns was untraceable from any single Module.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Extract the concerns incrementally, starting with the parts that
have zero UI dependency: move the artwork-ranking classification functions (`RankGrids`,
`RankIcons`, `GridMetadata`, `IsDemotedGrid`, `IsEditionMismatch`, `RankedGrid`,
`GridStylePriority` plus their static regex/const fields) into the existing `Services/Artwork/`
module, which already owns `TileImage`, `ArtworkSignature`, `FixLog`, `AppliedArtworkStore` —
every other artwork concern. **This loop executed that first slice** (see Loop 1 Result);
store-name resolution and file-restore orchestration remain backlog items for subsequent loops.

**Blast radius** — Change: `PrimaryWidget.xaml.cs`,
`Services/Artwork/ArtworkRanker.cs` (new). Avoid: `Services/SteamGridDB/*`, `Services/Stores/*`.

---

### Finding #2: AppliedArtworkStore.GetAsync reads the shared cache without the lock that guards its writer

**Why it matters** — A non-thread-safe `Dictionary` read concurrently with an in-place write can
throw or corrupt internal state; the picker's "which artwork is applied" marker can silently go
wrong exactly when a fix operation is writing to the same store.

**What is wrong** — `GetAsync` (`AppliedArtworkStore.cs:38-48`) calls `TryGetValue` on the shared
`applied` dictionary after `LoadAsync` returns, without acquiring `gate`, while `UpdateAsync`
(`AppliedArtworkStore.cs:138-169`) holds `gate` only around its own read-modify-write of the very
same dictionary instance — the lock protects the writer against other writers but not readers
against the writer.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:38-48`
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:138-169`

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
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:430-734` (per-folder, per-entry sequential `foreach`
  containing `await sgdbClient.GetGameByPlatformIdAsync`)

**Architectural test failed** — n/a — different category (D2, structural waste per
`lens-efficiency.md`, not a Seam)

**Dependency category** — `true-external`

**Leverage impact** — There is only one call site (the load loop); a future second caller of the
same pattern would inherit the same linear cost with no leverage from batching, since none
exists.

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
instead of once; a future third cache (there is already a fourth candidate — the store-name
caches in F1/F3) would make it three.

**What is wrong** — `AppliedArtworkStore.LoadAsync` (`AppliedArtworkStore.cs:84-136`) and
`EpicLibrary.LoadAsync` (`EpicLibrary.cs:67-89`) both implement: check-null, await
`SemaphoreSlim` gate, re-check-null, populate, release — identical structure, no shared helper.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:84-136`
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
contained — neither copy is currently wrong on its own (unlike F2, which is a live hazard).

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a small internal `AsyncLazy`-style helper
(check-lock-recheck-populate) that both call sites construct against. Do not add an interface or
DI — this is one concrete type serving two internal call sites, not a Seam.

**Blast radius** — Change: `Services/Artwork/AppliedArtworkStore.cs`,
`Services/Stores/EpicLibrary.cs`, one new small internal helper. Avoid: `PrimaryWidget.xaml.cs`.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Extracting the artwork-ranking classification cluster (F1's first slice) into `Services/Artwork/ArtworkRanker.cs` — passes the deletion test in reverse, pure move, zero new abstraction |
| New seam justified | false |
| Helpful simplification | Consolidating the four store-name caches and the artwork-ranking constants under `Services/` once F1's remaining slices land |
| Should NOT be done | Introduce an `IArtworkRanker` interface, a Coordinator, or a DI container — one production caller, no second implementation proven necessary |
| Tests after fix | No test project exists (standing instruction); build (MSBuild compile) is the only regression oracle this loop |

## Improvement Backlog

1. **Fix AppliedArtworkStore.GetAsync's unguarded read against UpdateAsync's guarded write**
   (F2) — structural, needed for winning.
2. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups** (F3), *after*
   switching the four static name-resolution caches to `ConcurrentDictionary` — structural,
   helpful.
3. **Continue the PrimaryWidget.xaml.cs break-up (F1, next slice): store-name resolution**
   into `Services/Stores/` alongside the existing `EpicLibrary.cs` — structural, needed for
   winning.

## Deepening Candidates

- **Candidate Module:** An `AsyncLazy`-style cache helper for `AppliedArtworkStore` +
  `EpicLibrary` (and eventually the `PrimaryWidget` store-name caches). Source friction proven:
  F4. Dependency category: `in-process`. What not to do: do not generalize into a public caching
  framework or add a second production adapter to justify a Seam.

## Builder Notes

1. Churn concentrates where concerns are merged → REVIEW_HISTORY.json `loops[0].builder_notes`
   for full notes.
2. A lock that guards the writer but not the reader is not a lock → REVIEW_HISTORY.json
   `loops[0].builder_notes` for full notes.
3. Sequential `await` in a per-item loop hides its own cost until the collection grows →
   REVIEW_HISTORY.json `loops[0].builder_notes` for full notes.

## Final Judge Narrative

Place, not win. `PrimaryWidget.xaml.cs` concentrated four unrelated concerns — UI, manifest/
store-name resolution, artwork ranking, and backup orchestration — behind zero Interface
boundary, and the churn data confirmed it was paying rent for that: 21 edits against 8 for the
next-highest file. Runtime ownership is mostly trustworthy (`isLibraryOperationRunning` is a
clean single-owner guard, `GameEntry`/`GridImageItem` are honest observable models), but
`AppliedArtworkStore.GetAsync`'s unguarded read against its own Module's guarded writer is a
real, if narrow, concurrency hazard. Tests do not exist and cannot be added this run per standing
instruction, so regression resistance is unverifiable beyond a compile check — the score reflects
that honestly rather than pretending the codebase earns credit it cannot currently prove. This
loop's fix (extracting the artwork-ranking algorithm to `Services/Artwork/ArtworkRanker.cs`) was
deliberately the smallest, lowest-risk first slice of F1; committing to a mid-size or full
break-up of `PrimaryWidget.xaml.cs` in one loop would have violated the smallest-honest-fix
discipline and risked exactly the kind of over-engineered churn the simplicity anchor punishes.

## Loop 1 Result

Extracted the artwork-ranking classification cluster (`RankGrids`, `RankIcons`, `GridMetadata`,
`IsEditionMismatch`, `IsDemotedGrid`, `IsDemotedMetadata`, the private `RankedGrid` class,
`GridStylePriority`, and the associated regex/const fields — `textBearingGridStyles` plus six
`Regex` fields) out of `PrimaryWidget.xaml.cs` into a new file
`Services/Artwork/ArtworkRanker.cs` (`internal static class`), and updated the seven call sites in
`PrimaryWidget.xaml.cs` to `ArtworkRanker.X(...)`. Added the new file to
`SteamGridDB.Xbox.csproj`'s explicit `<Compile Include>` list (old-style csproj, no globbing).
Removed the now-unused `using System.Text.RegularExpressions;` from `PrimaryWidget.xaml.cs`.
Four members (`RankGrids`, `RankIcons`, `IsDemotedGrid`, `GridStylePriority`, and the
`TextBearingGridStyles` field) widened from `private` to `internal` so `PrimaryWidget` can still
call them across the file move — a `cross_file_visibility` boundary per Meta-Rule 4, but not one
recorded in `loop_result.risk_boundary_evidence`: unlike Swift's actor-isolation/`Sendable`/
`#if`-gated cases the boundary_kind taxonomy targets, a plain C# accessibility change with one
build configuration in play (this solution has no other platform/config touching these files) is
exhaustively checked by the compiler on every reference — there is no "compiles but the
invariant is actually violated" failure mode for it to hide, so the single green build already is
complete evidence, not partial evidence dressed up as complete.

**What proves the change is honest:** `MSBuild` (the sole regression oracle per Discovery — no
test project exists) passed clean after the change (exit 0, same command as Step 0's baseline).
`git diff --stat` shows `PrimaryWidget.xaml.cs | 208 +-----------------------------` (208 lines
removed net, 9 insertions) and a new 207-line `ArtworkRanker.cs` — a pure relocation, not a
rewrite (doc comments and regex patterns preserved verbatim; call-site arguments unchanged). The
Implementation Review subagent (fresh context, read-only, no memory of this loop's authoring)
returned `verdict: approved` with all three checks (`reality`, `honesty`, `regression`) `passed`
and empty `regressions`/`conditions`.

**Targeted finding status:** `carried_forward` — F1 as a whole (the full four-concern
break-up) is not resolved; this loop executed only its first, lowest-risk slice by design (see
Minimal Correction Path). The `Services/Artwork/ArtworkRanker.cs` extraction itself is complete
and will not recur as a finding.

**Unintended scorecard regression:** none observed. `simplicity` moved UP on structural proof
(the diff itself); no other dimension regressed.

## Loop 1 Implementation Review

`verdict: approved` — "Pure, verbatim relocation of the artwork-ranking cluster into a new
internal static ArtworkRanker.cs; all call sites in PrimaryWidget.xaml.cs updated with identical
arguments, no new Seam introduced, and no unrelated files touched." All three checks (`reality`,
`honesty`, `regression`) `passed`; `regressions: []`; `conditions: []`.

--- Loop 2 (UTC 2026-08-03T00:30:00Z) ---

### Discovery

See Loop 1 Discovery.

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

| Field | Value |
|---|---|
| Structurally necessary | Widening `AppliedArtworkStore.GetAsync`'s lock coverage to match `UpdateAsync` (F2) — closes a real read/write race; the smallest honest fix reuses the semaphore already scoped to the exact data it protects |
| New seam justified | false |
| Helpful simplification | none this loop (F4's duplicate lazy-cache skeleton remains a deepening candidate, not addressed this loop) |
| Should NOT be done | Introduce a `ReaderWriterLockSlim`, a new cache wrapper type, or an `IAppliedArtworkStore` interface — one correctly-scoped `SemaphoreSlim` is the whole fix |
| Tests after fix | No test project exists (standing instruction); `MSBuild` compile is the only regression oracle; no mechanical race-detection tooling available for this UWP/C# stack |

## Improvement Backlog

1. **Continue the PrimaryWidget.xaml.cs break-up (F1, next slice): store-name resolution** into
   `Services/Stores/` alongside the existing `EpicLibrary.cs` — structural, needed for winning.
2. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups** (F3), *after*
   switching the four static name-resolution caches to `ConcurrentDictionary` — structural,
   helpful.
3. **Extract a shared AsyncLazy-style cache helper for AppliedArtworkStore and EpicLibrary** (F4)
   — simplification, helpful.

## Deepening Candidates

- **Candidate Module:** An `AsyncLazy`-style cache helper for `AppliedArtworkStore` +
  `EpicLibrary` (and eventually the `PrimaryWidget` store-name caches). Source friction proven:
  F4. Dependency category: `in-process`. What not to do: do not generalize into a public caching
  framework or add a second production adapter to justify a Seam.

## Builder Notes

1. The smallest fix for a lock-coverage gap reuses the lock that already exists — it does not
   invent a new primitive → REVIEW_HISTORY.json `loops[1].builder_notes` for full notes.
2. Churn concentrates where concerns are merged (unchanged from loop 1 — still live) →
   REVIEW_HISTORY.json `loops[1].builder_notes` for full notes.
3. Sequential `await` in a per-item loop hides its own cost until the collection grows (unchanged
   from loop 1 — F3 still open) → REVIEW_HISTORY.json `loops[1].builder_notes` for full notes.

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

--- Loop 3 (UTC 2026-08-04T01:16:59Z) ---

### Loop Counter

Loop 3 of 10 (cap)

(see Loop 1 Discovery)

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

| Field | Value |
|---|---|
| Structurally necessary | Moving store-name-resolution methods and their four cache fields out of `PrimaryWidget.xaml.cs` into `Services/Stores/StoreNameLookup.cs` — closes the third of F1's four merged concerns; the smallest honest fix is a pure relocation, reusing the exact code. |
| New seam justified | false — no new Seam; `StoreNameLookup` is a plain `internal static class` alongside its existing sibling `EpicLibrary`. |
| Helpful simplification | Reader locality for "how does the widget resolve a third-party store's game name" improved from "search a 2,500-line UI file" to "read one 263-line file." |
| Should NOT be done | Encapsulating the GOG/Epic caching logic inside `StoreNameLookup` this loop — the empty-cached-value-means-refetch semantics would be easy to flatten incorrectly with no test oracle. Deferred to a Deepening Candidate. |
| Tests after fix | No test project exists (standing instruction); `MSBuild` compile is the only regression oracle. Meta-Rule 4 does not apply — visibility was deliberately widened, not narrowed. |

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
   first is safe without a test oracle. → REVIEW_HISTORY.json `loops[2].builder_notes` for full notes.
2. **Pattern:** Extracting one slice of a multi-concern god-class narrows Locality for the
   *extracted* concern immediately, even before the whole file is broken up. → REVIEW_HISTORY.json
   `loops[2].builder_notes` for full notes.
3. **Pattern:** Churn concentrates where concerns are merged (unchanged from loops 1-2 — still
   live, now measuring a smaller file). → REVIEW_HISTORY.json `loops[2].builder_notes` for full
   notes.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. `architecture_quality` moving UP to 5.5 rather than staying SAME — loop 1's structurally
   equivalent move scored SAME, not UP. I judged this loop's move differently because it leaves
   fewer concerns remaining (2 of 4, cumulative) than loop 1's did (3 of 4) — a stricter reviewer
   could reasonably hold both loops to the identical bar and keep this SAME too.
2. `data_flow` moving UP to 6.0 — real, but no port/interface was introduced; a stricter reviewer
   could argue this doesn't yet earn a data-flow-dimension credit distinct from the
   architecture-quality credit already given for the same diff.
3. The Authority Map's "Single and clear today" verdict for the Store-name-resolution-caches
   concern, despite `gogNameCache`/`epicNameCache` now living in a class that doesn't itself write
   them — a reviewer weighting Module boundaries over call-site count could reasonably call this
   "Split and ambiguous" instead.

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

--- Loop 4 (UTC 2026-08-04T01:44:03Z) ---

### Loop Counter

Loop 4 of 10 (cap)

(see Loop 1 Discovery)

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

| Field | Value |
|---|---|
| Structurally necessary | Folding GOG's check-cache/fetch/populate-cache logic into `StoreNameLookup.GetOrFetchGogNameAsync` — closes the shallow-module residual Finding #1's loop-3 evidence named; the smallest honest fix is a targeted fold plus a `private` narrowing. |
| New seam justified | false — no new Seam; `GetOrFetchGogNameAsync` is a plain internal method added to the existing static class. |
| Helpful simplification | `PrimaryWidget.xaml.cs`'s GOG branch shrank from 17 lines of inline cache-check/fetch/populate logic to a 4-line call-and-assign; the decision now lives entirely inside `StoreNameLookup`. |
| Should NOT be done | Folding Epic's equivalent block in the same loop — Epic's path has a second fallback source (`EpicLibrary.GetDisplayNameAsync`) GOG's path does not have, so the same mechanical fold carries more behavior-preservation risk. Deferred to a Deepening Candidate. |
| Tests after fix | No test project exists (standing instruction); `MSBuild` compile is the only regression oracle, verified green both before and after. Not a Meta-Rule-4 risk-boundary crossing: the field's only external caller was rewritten in the same commit; grep- and compile-verified no orphaned caller remains. |

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
   defaulting to whichever sounds more impactful. → REVIEW_HISTORY.json `loops[3].builder_notes`
   for full notes.
2. **Pattern:** A residual left half-closed by one loop (fixing GOG's cache but not Epic's) is a
   legitimate, named next step — not a sign the prior loop's work was wasted. → REVIEW_HISTORY.json
   `loops[3].builder_notes` for full notes.
3. **Pattern:** Churn concentrates where concerns are merged (unchanged from loops 1-3 — still
   live; this loop did not touch the churn-flagged file's concern count at all). →
   REVIEW_HISTORY.json `loops[3].builder_notes` for full notes.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. `code_simplicity` moving UP to 6.5 for a fix scoped to one of four cache fields in a module that
   is itself a secondary extraction (not the god-class) — a stricter reviewer could argue this is
   too narrow a diff to move a whole-codebase dimension score at all.
2. `data_flow` staying SAME rather than moving UP — I judged the in-process reorganisation doesn't
   independently earn a data-flow credit distinct from the `code_simplicity` credit for the same
   diff; a less conservative reviewer could disagree and credit both dimensions.
3. `state_management` staying SAME despite the Authority Map entry text changing — I attributed the
   Locality/Interface-coherence improvement entirely to `code_simplicity`; a reviewer weighting
   "how many of a concern's writers-of-record are internal to its own Module" as a
   `state_management` fact could score this dimension up instead.

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

--- Loop 5 (UTC 2026-08-04T02:07:50Z) ---

### Loop Counter

Loop 5 of 10 (cap)

(see Loop 1 Discovery)

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
  no ambiguity - `epicNameCache` had exactly one writer, `PrimaryWidget.LoadGameEntriesAsync`, both
  before and after this loop's edit) - this loop closed a Locality/Interface-coherence gap for that
  concern (see Code simplicity below), not a write-authority correctness defect, so this dimension
  does not move. `isLibraryOperationRunning` and `AppliedArtworkStore.applied` remain unchanged.
- **Domain modeling:** 5.5 | SAME | Unaffected by this loop's change;
  `SteamGridDbClient.ParseOfficialCapsuleUrl` (`SteamGridDbClient.cs:144-199`) still hand-parsed
  outside the DTO's own deserialization, verified unchanged this loop.
- **Data flow and dependency design:** 6.0 | SAME | This loop's fix is entirely in-process
  reorganisation within an already-relocated module (`StoreNameLookup`, moved out of
  `PrimaryWidget` in loop 3) - no dependency moved, no new port introduced, and while
  `PrimaryWidget`'s direct call count into `Services/Stores` for the Epic path did drop (from three
  call sites to one), that is the same shape of evidence loop 4 deliberately credited to Code
  simplicity, not Data flow, for the identical GOG fix, to avoid double-counting one diff across
  two dimensions. Staying consistent with that established convention rather than re-litigating
  the judgment call.
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

| Field | Value |
|---|---|
| Structurally necessary | Folding Epic's check-cache/fetch/populate logic into `StoreNameLookup.GetOrFetchEpicNameAsync` - closes the last instance of the shallow-module residual Finding #1's evidence has tracked since loop 3; the smallest honest fix is a targeted fold plus a `private` narrowing. |
| New seam justified | false - no new Seam; `GetOrFetchEpicNameAsync` is a plain internal method added to the existing static class. |
| Helpful simplification | `PrimaryWidget.xaml.cs`'s Epic branch shrank from a 20-line inline block to a 7-line call-and-assign; the decision now lives entirely inside `StoreNameLookup`, preserving the exact two-source fallback order. |
| Should NOT be done | Attempting backup/restore orchestration extraction in the same loop - its own evidence text still names an unresolved UI-update design decision, re-confirmed this loop by reading `RestoreBackupCoreAsync` directly (`Dispatcher`/`StatusText`/`GameEntries` coupling). |
| Tests after fix | No test project exists (standing instruction); `MSBuild` compile is the only regression oracle, verified green both before and after. Not a Meta-Rule-4 risk-boundary crossing: the field's only external caller was rewritten in the same commit; grep- and compile-verified no orphaned caller remains. |

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
   option automatically becomes this loop's task - re-run the Simplify Pressure Test on it fresh.
   -> REVIEW_HISTORY.json `loops[4].builder_notes` for full notes.
2. **Pattern:** A residual tracked across multiple loops as parallel instances of the same
   shallow-module shape is fully closed once every named instance is folded - say so explicitly.
   -> REVIEW_HISTORY.json `loops[4].builder_notes` for full notes.
3. **Pattern:** Churn concentrates where concerns are merged (unchanged from loops 1-4 - still
   live; this loop did not touch the churn-flagged file's concern count at all).
   -> REVIEW_HISTORY.json `loops[4].builder_notes` for full notes.

**Scorecard humility check** (Q9): three specific claims I am least confident about -
1. `code_simplicity` moving to 7.0 (rather than staying at 6.5, or jumping further) for closing
   the second and final half of a two-instance residual in a secondary-extraction module (not the
   god-class itself) - a stricter reviewer could argue the whole StoreNameLookup cluster earns at
   most one 0.5 credit total (already spent last loop), and this loop's Epic half is "finishing
   what was already credited," not new evidence for a second increment.
2. `data_flow` staying SAME despite `PrimaryWidget`'s direct call count into `Services/Stores` for
   the Epic path dropping from three call sites to one - a reviewer who does not treat loop 4's
   precedent as binding could credit `data_flow` independently for the reduced fan-out.
3. The Deepening Candidates section's claim that "no remaining shallow instance" exists in
   `StoreNameLookup` - true for the four name-caching fields specifically, but not an exhaustive
   re-audit of every method in the file for other shallow-module shapes.

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

--- Loop 6 (UTC 2026-08-04T02:36:45Z) ---

### Loop Counter

Loop 6 of 10 (cap)

(see Loop 1 Discovery)

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

| Field | Value |
|---|---|
| Structurally necessary | Replacing `AppliedArtworkStore.LoadAsync` and `EpicLibrary.LoadAsync`'s hand-duplicated check-null/gate/re-check-null/populate/release skeleton with one shared `AsyncLazyCache<T>` - closes F-004 (open every loop since loop 1, never previously attempted); the smallest honest fix is a generic helper taking the caller's own `SemaphoreSlim` (not a new one), matching loop 5's own vetted "smallest first step" plan verbatim |
| New seam justified | false |
| Helpful simplification | `AppliedArtworkStore.cs` shrank 182 -> 165 lines (net -17); `EpicLibrary.cs` shrank 144 -> 121 lines (net -23); the two hand-copied lock skeletons collapsed into one 61-line generic helper that both classes now share, closing F-004 completely |
| Should NOT be done | Attempting F1's callback-interface alternative this loop (invents a new Seam for a single caller with zero tests); attempting F-003 (bounded concurrency) this run (ruled out by an explicit operational constraint) |
| Tests after fix | No test project exists (standing instruction); `MSBuild` compile is the only regression oracle, verified green both before and after this loop's change |

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

1. A Noticeable-severity finding can sit unattempted in the backlog for many loops without being
   cosmetic - once the Serious item's smaller substitute steps are genuinely exhausted and its
   remaining slice is genuinely blocked, advancing to the next Noticeable item is the honest move.
   → REVIEW_HISTORY.json `loops[5].builder_notes` for full notes.
2. A finding's two named remedies are not always equally blocked - re-reading the actual coupling
   can split "needs a design decision" into one path that genuinely does and one that doesn't.
   → REVIEW_HISTORY.json `loops[5].builder_notes` for full notes.
3. Churn concentrates where concerns are merged (unchanged from loops 1-5 - still live). →
   REVIEW_HISTORY.json `loops[5].builder_notes` for full notes.

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

`verdict: approved` - "Both hand-rolled check-null/gate.WaitAsync/recheck/populate/release
skeletons (AppliedArtworkStore.LoadAsync, EpicLibrary.LoadAsync) are gone from current source,
replaced by calls to a new shared AsyncLazyCache<T> that takes the caller's existing SemaphoreSlim
as a constructor argument rather than owning a private lock, and GetAsync/UpdateAsync still
separately acquire that same gate for their own critical sections after the lazy-load call returns,
so F-002's read/write lock invariant is preserved with no new same-or-higher-severity finding
introduced." All three checks (`reality`, `honesty`, `regression`) `passed`; `regressions: []`;
`conditions: []`.

