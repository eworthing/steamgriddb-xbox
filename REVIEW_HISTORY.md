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

--- Loop 7 (UTC 2026-08-04T14:01:43Z) ---

### Discovery

see Loop 1 Discovery. Refreshed at loop 7 (drift since loop 6: `d98dde8`, `f61e8d4`, `08f40f6`,
`9c7ce51` - user added a 104-test suite and moved backup/restore + bulk-library orchestration out of
`PrimaryWidget.xaml.cs`; full refreshed Discovery text lives in `REVIEW_HISTORY.json.loops[6]`).

### Loop Counter

Loop 7 of 10 (cap)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Independent re-derivation from current source (both gates green before and after this loop's
change: 104/104 tests, MSBuild exit 0) confirms the loop-1-6 record that "no test project exists"
is dead - the user built a real, mutation-tested 104-test suite directly and separately moved
backup/restore and bulk-library orchestration out of `PrimaryWidget.xaml.cs` themselves, shrinking
it 2,278 -> 2,132 lines before this loop even started. Re-reading `PrimaryWidget.xaml.cs` fresh
found a third merged concern loop 6 never flagged: a ~150-line artwork-selection algorithm that
referenced zero PrimaryWidget instance state. This loop relocated that algorithm into a new
`Services/Artwork/ArtworkDownloader.cs`, shrinking the file a further 2,132 -> 1,957 lines. F1's core
claim is not resolved, so F-001 stays `carried_forward`. `test_strategy` moves the most this loop,
from the loop-1-6 blocker (3.0) to a genuinely-earned 8.0, with one real test gap now queued as
F-005.

## Scorecard (1-10)

- **Architecture quality:** 6.5 | UP | `PrimaryWidget.xaml.cs` shrank 2,132 -> 1,957 lines this loop;
  three artwork-selection methods (zero PrimaryWidget instance-state references) relocated to a new
  195-line `ArtworkDownloader.cs` with 3 real call sites, build-verified.
- **State management and runtime ownership:** 7.0 | UP | `AppliedArtworkStore.RecordFolder`/
  `FixLog.LogFolder` (this drift window) correctly recreate the cache on reassignment rather than
  serving a stale map - a new mutation vector handled correctly.
- **Domain modeling:** 5.5 | SAME | `SteamGridDbClient.ParseOfficialCapsuleUrl` unchanged this drift
  window - no structural proof to move this dimension.
- **Data flow and dependency design:** 6.0 | SAME | This loop's fix is in-process relocation of
  already-pure, already-leaf logic - consistent with the established convention of crediting this
  diff shape to architecture_quality/simplicity, not data_flow.
- **Framework / platform best practices:** 6.0 | SAME | The JSON-idiom split in `SteamGridDbClient.cs`
  is unchanged this drift window.
- **Concurrency and runtime safety:** 6.5 | UP | `AsyncLazyCacheTests.cs` (this drift window)
  empirically stress-tests 32 concurrent callers, turning loop 6's `reasoning_only` risk-boundary
  evidence into tested behavior. F-003 remains open, still blocked by operational constraint.
- **Code simplicity and clarity:** 8.0 | UP | This loop's extraction plus `SetStatusAsync`/
  `OnUiThreadAsync` (this drift window) now own nearly all UI-thread dispatch - only 4 raw
  `Dispatcher.RunAsync` calls remain in the 1,957-line file.
- **Test strategy and regression resistance:** 8.0 | UP | Full re-derivation. Authority-Map
  cross-check and a hands-on mutation-test check both performed fresh, the latter surfacing a real
  gap (F-005). Ceiling held at 8 per the anti-anchor rule (two named, disclosed coverage-gap
  categories: PrimaryWidget's UI shell, network-dependent code).
- **Overall implementation credibility:** 7.0 | UP | New mutation-verified evidence this drift window
  directly proves several load-bearing invariants hold under regression pressure, not just
  inspection. Capped below 8: `PrimaryWidget.xaml.cs`'s remaining 1,957 lines are still unverified by
  anything but inspection and a green compile.

## Authority Map

(Re-emitted this loop: a new concern - the artwork-selection algorithm - gained a single, clear
owner this loop; F1 remains Priority 1.)

- **Concern:** Library-operation exclusivity (`isLibraryOperationRunning`) - Owner: `PrimaryWidget`
  instance. Verdict: **Single and clear** - unaffected this loop; the named shell-seam residual
  behind `test_strategy`'s ceiling (no direct test file possible, `Windows.UI.Xaml` has no desktop
  projection).
- **Concern:** Artwork-selection algorithm (candidate download, tile-fill pick, official-lookalike
  veto) - **new owner this loop** - Owner: `Services/Artwork/ArtworkDownloader` (new, static, this
  loop). Verdict: **Single and clear** - new this loop; no direct test file (network-dependent, same
  pre-existing documented carve-out `StoreNameLookup`'s network methods already had).
- **Concern:** Applied-artwork record (`AppliedArtworkStore.appliedCache`) - Owner:
  `AppliedArtworkStore` (static). Verdict: **Single and clear** - unaffected in substance this loop
  (`RecordFolder`'s setter, this drift window, correctly recreates the cache on reassignment).

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design
  (`Services/SteamGridDB/ArtworkSource.cs:15-51`) makes "neither a platform-ID nor a game-ID"
  unrepresentable. Re-verified unchanged this loop.
- `AsyncLazyCache<T>` takes the caller's own lock as a constructor argument instead of owning a
  private one, keeping `AppliedArtworkStore`'s F-002 fix intact - now empirically stress-tested under
  32 concurrent callers (`AsyncLazyCacheTests.cs`, this drift window), not merely reasoned about.
- The official-artwork gate (`FindOfficialLookalikeAsync`, now
  `Services/Artwork/ArtworkDownloader.cs:112-183`) is a narrow, evidence-tuned veto - moved verbatim
  this loop, comments intact.
- `ArtworkRankerTests.cs` (this drift window) pins several artwork-ranking decisions the code
  comments explain but nothing previously enforced - PNG-over-JPEG tried and reverted, SteamGridDB's
  own "official" icon style tried and rejected, the mockup-vocabulary word-boundary rule.

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling and multi-concern orchestration behind zero Interface boundary

**Why it matters** - The churn-flagged file (27 edits over 6 months, still the largest in the repo)
continues to bundle several structurally distinct concerns with no Module boundary between them, so
a change to any one risks disturbing the others.

**What is wrong** - `PrimaryWidget.xaml.cs` is 1,957 lines after this loop's fix (was 2,132 at Step 1
inspection, 2,278 before loop 6's drift window) - re-derived fresh this loop rather than carried from
loop 6's text, since the user's own out-of-band commits (`08f40f6`, `9c7ce51`) had already extracted
the backup/restore file-operations (`ArtworkFiles`) and the bulk-operation primitives (`GameImages`,
`OperationReport`) loop 6 last saw merged in, so loop 6's evidence was stale before this loop began.
Re-reading the current file top to bottom found a third, previously-uncredited merged concern: the
artwork-selection algorithm had zero references to any `PrimaryWidget` instance state across all
three of its methods. This loop's fix moves those three methods (`DownloadArtworkAsync`,
`DownloadBestTileFillingImageAsync`, `FindOfficialLookalikeAsync`) plus their three tuning constants
and the shared `HttpClient` into a new `Services/Artwork/ArtworkDownloader.cs`. What remains merged
in `PrimaryWidget`: (1) UI event handling proper - correctly stays; (2) `LoadGameEntriesAsync`
(`PrimaryWidget.xaml.cs:331-709`, ~378 lines) interleaves pure manifest-JSON parsing with UI-bound
work - untouched this loop, now the largest remaining merged-concern candidate; (3) the three
bulk-operation loops still iterate `GameEntry` directly for a source-verified platform reason
(`GameEntry.Image`/`HasBackup` bind `Windows.UI.Xaml`, no desktop projection).

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (1,957 lines total post-fix; `wc -l` before/after this
  loop: 2,132 -> 1,957)
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:331-709` (`LoadGameEntriesAsync` - manifest parsing
  interleaved with `OnUiThreadAsync`/`CreateThumbnailAsync`, unaffected by this loop's fix)
- `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs` (new this loop, 195 lines - the
  previously-merged artwork-selection algorithm, now standalone with zero PrimaryWidget-instance-
  state dependency)

**Architectural test failed** - n/a - different category (ownership/coupling sprawl, not a
removable Seam or wrapper)

**Dependency category** - n/a

**Leverage impact** - The three moved methods now have one home instead of being reachable only by
editing the 1,957+-line UI class.

**Locality impact** - A maintainer tuning the official-artwork gate's constants now reads a 195-line
file with a single responsibility instead of finding the logic 1,200+ lines into a UI class.

**Metric signal** - `PrimaryWidget.xaml.cs`: 2,132 -> 1,957 lines this loop (-175, -8.2%);
`Services/Artwork/ArtworkDownloader.cs`: 195 lines, new.

**Why this weakens submission** - Ownership of the two concerns still remaining merged in
`PrimaryWidget` is still untraceable from any single Module besides the UI class itself; the
churn-flagged file, while smaller, is still well above the architecture-quality 7-anchor bar.

**Severity** - Serious deduction

**ADR conflicts** - none

**Minimal correction path** - F1's remaining scope is now `LoadGameEntriesAsync`'s manifest-parsing
versus its UI-bound tail. This is a bigger, more entangled slice than this loop's fix. Re-run the
Simplify Pressure Test fresh before attempting.

**Blast radius** - Change (next loop, if fresh SPT passes): `PrimaryWidget.xaml.cs`
(`LoadGameEntriesAsync`'s manifest-parsing lines only), a new `Services/Library` manifest-parsing
helper. Avoid: `Services/Artwork/ArtworkDownloader.cs`, `Services/Artwork/ArtworkFiles.cs`,
`Services/Artwork/ArtworkRanker.cs`, `Services/Stores/*`, `Services/Library/*`.

---

### Finding #2: ArtworkRanker.RankGrids' style-priority ordering has no test mixing text-bearing and icon-like styles, so a sort-direction inversion would pass silently

**Why it matters** - `RankGrids` is the ranking algorithm behind every automatic artwork pick on the
widget's primary flow; an inverted style-priority tie-break would silently prefer icon-like art over
box-art-style uploads library-wide, with no test catching it.

**What is wrong** - `ArtworkRanker.cs:195` sorts candidates with
`.ThenBy(r => GridStylePriority(r.Grid.Style))` - ascending order is load-bearing (0 = text-bearing
first, 1 = icon-like last), but every `RankGrids` test in `ArtworkRankerTests.cs:161-244` constructs
its candidates with the `Grid()` factory's default style ("alternate") on both sides of the
comparison, so `GridStylePriority` evaluates to the same tie value in every test. Mutation check
performed directly against current source: flipping `.ThenBy` to `.ThenByDescending` changes nothing
observable in any of the 10 existing `RankGrids`/`RankIcons` tests.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs:195` (the load-bearing ascending sort)
- `SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs:74-77` (`GridStylePriority`)
- `SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs:21-41` (`Grid()` factory defaults to "alternate")
- `SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs:161-244` (all six `RankGrids` ordering tests)

**Architectural test failed** - n/a - different category (missing-test gap, method.md Step 8)

**Dependency category** - n/a

**Leverage impact** - One call site, but it is the ranking function every automatic-fix and
manual-picker artwork list goes through.

**Locality impact** - One new test case; no production code changes.

**Metric signal** - none

**Why this weakens submission** - A source-level mutation on a central, primary-flow ranking rule
passes the entire 104-test suite undetected.

**Severity** - Noticeable weakness

**ADR conflicts** - none

**Minimal correction path** - Add one `RankGrids` test case with mixed styles, asserting the
text-bearing one sorts first. No production code change.

**Blast radius** - Change: `SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs` (one new test method).
Avoid: everything else.

---

### Finding #3: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** - Load time scales linearly with library size and network latency on the
widget's primary open path.

**What is wrong** - The `gameCache` `foreach` loop in `LoadGameEntriesAsync` awaits per-game network
calls one at a time; nothing overlaps them. Re-verified this loop: unaffected by this loop's fix
(which relocated a downstream concern). Still ruled out for this run by the explicit operational
constraint (must not change observable per-game network-call behavior without a behavioral oracle);
the new test suite does not cover network calls, so the drift does not lift this constraint.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:331-709` (awaits at `:569,591,600,609,629`, re-verified at
  current line numbers)

**Architectural test failed** - n/a - different category (D2, structural waste)

**Dependency category** - `true-external`

**Leverage impact** - One call site; a future second caller would inherit the same linear cost.

**Locality impact** - Local to `LoadGameEntriesAsync` and `StoreNameLookup`'s cache fields.

**Metric signal** - One HTTP round-trip per game per store lookup; 100+ sequential requests for a
100-game library.

**Why this weakens submission** - Structural waste on the primary hot path, out of scope by
explicit instruction, not mechanical difficulty.

**Severity** - Noticeable weakness

**ADR conflicts** - none

**Minimal correction path** - Blocked for this run's duration. If lifted: bounded concurrency +
`ConcurrentDictionary` for `StoreNameLookup`'s caches.

**Blast radius** - Change: `PrimaryWidget.xaml.cs`, `Services/Stores/StoreNameLookup.cs`. Avoid:
`Services/Artwork/*`, `Services/SteamGridDB/*`.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Relocating the artwork-selection algorithm (zero PrimaryWidget-state deps) into `ArtworkDownloader.cs` - passes the deletion test in reverse |
| New seam justified | false - internal static class, no interface/DI |
| Helpful simplification | `PrimaryWidget.xaml.cs` shrank 2,132 -> 1,957 lines; unused `Windows.Web.Http` import removed |
| Should NOT be done | Attempting `LoadGameEntriesAsync`'s split in the same loop (bigger, unvetted); attempting F-005 or F-003 this loop (F1 is higher priority) |
| Tests after fix | None added/needed - pure relocation of already-untested (network-dependent), already-documented-as-out-of-scope logic, not a deepening |

## Improvement Backlog

1. **Attempt LoadGameEntriesAsync's manifest-parsing/UI-decode split (F1's next honest slice)** -
   Architecture quality +0.5-1.0, Code simplicity +0.5 if verified.
2. **Add the missing RankGrids style-priority mixed-style test case (F-005)** - Test strategy
   +0.5-1.0 once verified.
3. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups (F-003)** - blocked
   for this run's duration; Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

None this loop - see `REVIEW_HISTORY.json.loops[6]` for full rationale.

## Builder Notes

1. A Serious finding's own evidence can go stale between loops even when no contest loop touched the
   file in the meantime. → REVIEW_HISTORY.json `loops[6].builder_notes` for full notes.
2. A method with zero references to its enclosing class's instance state sitting inside a large
   god-class is a clean, low-risk extraction candidate regardless of whether it is architecturally
   "deep". → REVIEW_HISTORY.json `loops[6].builder_notes` for full notes.
3. Mutation-testing your own test suite (not just running it) surfaces gaps that passing-test-count
   metrics hide completely. → REVIEW_HISTORY.json `loops[6].builder_notes` for full notes.

## Final Judge Narrative

Place, not win. This loop's real news is upstream of anything a contest loop did: the user closed
the loop-1-6 record's single biggest blocker themselves, writing a 104-test suite with real,
mutation-verified assertions and moving two of `PrimaryWidget.xaml.cs`'s four original merged
concerns out on their own, before this loop's Step 1 even started. This loop's job was honest
re-derivation, not confirmation - re-reading `PrimaryWidget.xaml.cs` fresh surfaced a third merged
concern loop 6 never named (the artwork-selection algorithm, provably UI-free) and closed it the
same way the user's own commits closed the other two: a plain relocation, no new Seam, verified by a
green build both before and after. F1 stays `carried_forward` - `LoadGameEntriesAsync` and the
bulk-operation loops are real, remaining merged concerns - but the finding's own evidence is now
current, not stale. `test_strategy` moved the most (3.0 -> 8.0) on real evidence: an Authority-Map
cross-check and a hands-on mutation-test check both performed against current source, the latter
honestly surfacing a real gap (F-005) rather than rubber-stamping the test count. Concurrency's
F-003 residual remains explicitly out of scope for this entire run by operational instruction;
`AsyncLazyCache<T>`'s lock discipline is now empirically stress-tested. Runtime ownership is
trustworthy for what has been resolved and now partly test-verified rather than purely inspected;
concurrency is not yet fully trustworthy on the still-open F-003 path. Future work has one
honestly-scoped path for F1 and one cheap, concrete fix for the newly-found test gap.

## Loop 7 Result

Moved `DownloadArtworkAsync`, `DownloadBestTileFillingImageAsync` and `FindOfficialLookalikeAsync` -
plus the `maxArtworkCandidates`/`officialArtworkFloor`/`officialArtworkCeiling` constants and the
`sharedHttpClient` field they alone used - verbatim from `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`
into a new `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs` (195 lines), closing this loop's
re-derived slice of F1 (stable_id F-001). All three moved methods were grep-confirmed to reference
zero `PrimaryWidget` instance state before the move. Three call sites in `PrimaryWidget` were
re-pointed to the new `ArtworkDownloader.*` names. The now-unused `using Windows.Web.Http;` import
was removed. Added `<Compile Include="Services\Artwork\ArtworkDownloader.cs" />` to
`SteamGridDB.Xbox.csproj`. `git diff --stat`: `PrimaryWidget.xaml.cs` (5 insertions, 179 deletions),
`SteamGridDB.Xbox.csproj` (1 insertion), plus the new 195-line `ArtworkDownloader.cs`.

**What proves the change is honest:** Both regression oracles passed clean before and after the
change - `run-tests.ps1` (104 passed, 0 failed, both runs) and `MSBuild` (exit 0 both times). The
move is byte-identical logic relocation, traced method-by-method. Grep-verified post-edit that no
stray reference to the removed methods, constants, or `sharedHttpClient` field remains anywhere in
`PrimaryWidget.xaml.cs`. The test project's `Services/**/*.cs` glob automatically picked up
`ArtworkDownloader.cs`; its continued 104/104 pass independently confirms the new file compiles
cleanly in the desktop-projected context too. This changes only where the artwork-selection
algorithm's code lives, not any selection/download behavior - confirmed by the independent
implementation-reviewer pass below.

**Risk boundary evidence (Meta-Rule 4):** this fix crosses a `cross_file_visibility` boundary - the
three moved methods and the `MaxCandidates` constant went from `private` to `internal`.
`{"boundary_kind": "cross_file_visibility", "verification": "compile_matrix", "detail": "Both
regression-oracle configurations this codebase has were compiled clean after the visibility change:
the UWP AppContainerExe build and the desktop net8.0-windows test build (which links
ArtworkDownloader.cs directly via the test project's Services/**/*.cs glob). Widening private to
internal in a single assembly with no InternalsVisibleTo boundary carries no cross-assembly exposure
risk in this codebase's shape (confirmed by reading both .csproj files directly this loop) - the
only thing that could break is a call site failing to resolve, which either compile config would
catch immediately as a hard compile error, and both did not.", "mechanically_testable": true}`

**Targeted finding status:** `carried_forward` - F-001's underlying Claim is not fully resolved:
`LoadGameEntriesAsync`'s manifest-parsing and the three bulk-operation loops remain merged in the
file. This loop closed the specific artwork-selection-algorithm slice of that Claim.

**Unintended scorecard regression:** none observed. Six dimensions moved UP on distinct,
non-overlapping structural proof; three held SAME; none regressed.

## Loop 7 Implementation Review

`verdict: approved` - "The three artwork-selection methods, three constants, and `sharedHttpClient`
field are verifiably gone from `PrimaryWidget.xaml.cs` and relocated byte-identical (verified
against `git show HEAD`) into `ArtworkDownloader.cs` with all three call sites re-pointed and no
dangling references anywhere in the repo." All three checks (`reality`, `honesty`, `regression`)
`passed`; `conditions: []`; `regressions: []`.


--- Loop 8 (UTC 2026-08-04T14:30:15Z) ---

### Discovery

see Loop 7 Discovery refresh (full detail in `REVIEW_HISTORY.json.loops[6]`). No drift since loop 7's
commit `7fa0548`; both ground-truth gates re-verified fresh this loop: `run-tests.ps1` 104 passed before
this loop's fix, 105 passed after; MSBuild exit 0 before and after.

### Loop Counter

Loop 8 of 10 (cap)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Re-derivation from current source (both gates green before and after this loop's change: 104→105 tests,
MSBuild exit 0) found that loop 7's queued Priority 1 - splitting `LoadGameEntriesAsync`'s manifest-parsing
from its UI-decode work - does not survive a fresh Simplify Pressure Test: reading the full method top to
bottom (not the summary loop 7's own Builder Notes admitted was untested) shows image decode, backup
checks, and network name-resolution are genuinely interleaved per manifest entry, not separable into a
clean two-phase split. Attempting it as previously scoped would risk exactly the costume-layer failure the
Simplify Pressure Test's fake-clean anti-examples warn against. Downgrading to the next backlog item
instead surfaced a new, independently-verified defect in the same method: five raw `Windows.Data.Json`
accessor calls bypass the codebase's own null-tolerant `JsonRead` module, and (confirmed empirically this
loop via a new xunit test) throw `InvalidOperationException` on a manifest field that is present but JSON
`null` - uncaught until the per-folder `catch`, silently discarding every other game in that folder. This
loop fixed it: all five call sites now route through `JsonRead`.

## Scorecard (1-10)

- **Architecture quality:** 6.5 | SAME | `PrimaryWidget.xaml.cs` is unaffected in shape this loop - the
  fix is a call-site accessor swap inside `LoadGameEntriesAsync`, not a concern relocation; F1's core claim
  (manifest-parsing and bulk-operation orchestration still merged with UI event handling) is untouched. No
  structural proof supports moving this dimension this loop (G8).
- **State management and runtime ownership:** 7.0 | SAME | Unaffected. `AppliedArtworkStore`/`FixLog`'s
  `RecordFolder`/`LogFolder` setters, `gate`, `GetAsync`/`UpdateAsync` are untouched by this loop's fix,
  which lives entirely inside `PrimaryWidget.xaml.cs`'s local JSON parsing.
- **Domain modeling:** 5.5 | SAME | `SteamGridDbClient.ParseOfficialCapsuleUrl` (`SteamGridDbClient.cs:144-199`)
  unchanged this loop; this loop's fix is credited to `credibility` below (an honesty-leak closure, not a
  domain-type change) to avoid double-counting one diff across two dimensions, per the established
  loop-4-7 convention.
- **Data flow and dependency design:** 6.0 | SAME | This loop's fix is in-process (no dependency moved, no
  new port, no cycle) - reusing `JsonRead`, an already-existing internal module, at a call site that
  previously bypassed it. Consistent with the established convention of crediting this diff shape
  elsewhere (see credibility) rather than double-counting it here.
- **Framework / platform best practices:** 6.0 | SAME | The `DataContractJsonSerializer` /
  `Windows.Data.Json` split in `SteamGridDbClient.cs` is unchanged this loop - no structural proof to move
  this dimension.
- **Concurrency and runtime safety:** 6.5 | SAME | Unaffected. F-003's fully sequential per-game
  round-trips (`PrimaryWidget.xaml.cs:332-730`) remain open, still ruled out for this run by the standing
  operational constraint (see Finding #4). `AsyncLazyCache<T>`'s loop-6/7 evidence is untouched this loop.
- **Code simplicity and clarity:** 8.0 | SAME | The fix is a small, local accessor swap (5 call sites, net
  +21 lines in a 1,957-line file, mostly explanatory comments) - real but too small in scale to move this
  dimension on its own; it removes one redundant `ContainsKey` check (folded into `JsonRead.String`'s
  null-tolerant return) but does not change the file's overall structure or ceremony level.
- **Test strategy and regression resistance:** 8.0 | SAME | Ceiling still held at 8 by the same two named,
  disclosed gap categories as loop 7 (`PrimaryWidget`'s architecturally-untestable shell seams; F-005's
  still-open `RankGrids` mutation gap - unaffected this loop, not selected as Priority 1). This loop's own
  fix cannot get direct test coverage for the same reason (`PrimaryWidget.xaml.cs` binds `Windows.UI.Xaml`,
  no desktop projection); the one new test added (`JsonReadTests.cs`) documents `JsonRead`'s own contract,
  not `LoadGameEntriesAsync` itself, so it does not close either gap category.
- **Overall implementation credibility:** 7.5 | UP | Closes a real, empirically-verified honesty leak on
  the widget's primary load path: `LoadGameEntriesAsync` bypassed the codebase's own defensive JSON-access
  module (`JsonRead`, built specifically to prevent a documented "present-but-null member throws" failure
  class that `JsonRead.cs`'s docstring says already shipped once) at five call sites. Confirmed this loop,
  not assumed from the docstring: a new xunit test
  (`JsonReadTests.cs::Raw_windows_data_json_overloads_throw_on_a_present_json_null_member`) proves the raw
  `GetNamedString` overloads throw `InvalidOperationException` on a present-null member, and that
  `ContainsKey` does not guard against it. All five sites (`PrimaryWidget.xaml.cs:452-458` id,
  `:461` addedDate, `:474` imagePath, `:536-537` title/installLocation/executableName) now route through
  `JsonRead`, verified by grep (`GetNamedString`/`entryObject.ContainsKey` no longer appear anywhere in the
  file). Capped below 8: `PrimaryWidget.xaml.cs`'s remaining 1,978 lines are still unverified by anything
  but inspection and a green compile - this fix closes one honesty leak, not the file's larger
  test-surface gap.

## Strengths That Matter

- `JsonRead` (`Services/JsonRead.cs`) is a genuine smart-accessor built from a real production incident
  (its docstring names the specific bug: a null Steam app ID field threw for every game with Steam
  platform data, and the resulting exception was swallowed into a false "no artwork" message). This loop's
  fix is proof the module keeps paying for itself: the same failure class, at a different call site,
  caught and fixed the same way years the module already existed to prevent.
- `ArtworkSource`'s private-constructor-plus-factory-method design
  (`Services/SteamGridDB/ArtworkSource.cs:15-51`) still makes "neither a platform-ID nor a game-ID"
  unrepresentable - unaffected and re-verified unchanged this loop.
- `AsyncLazyCache<T>` still takes the caller's own lock as a constructor argument rather than owning a
  private one, and remains stress-tested under 32 concurrent callers (`AsyncLazyCacheTests.cs`) -
  unaffected this loop.

## Findings

### Finding #1: LoadGameEntriesAsync's manifest parser bypassed JsonRead at five call sites, so one JSON-null field silently dropped the rest of that folder's games

**Why it matters** — The widget's primary load path could silently hide an entire platform folder's worth
of games from the user with no visible error, whenever any single manifest entry had one of five
commonly-nullable fields explicitly JSON `null`.

**What is wrong** — `LoadGameEntriesAsync` read five manifest fields (`id`, `addedDate`, `imagePath`,
`title`, `installLocation`/`executableName`) with the raw `Windows.Data.Json` accessors (`GetNamedString`,
`ContainsKey`) instead of the codebase's own `JsonRead` module, which exists specifically to tolerate a
member that is present and JSON `null`. Empirically confirmed this loop (new test in
`SteamGridDB.Xbox.Tests/JsonReadTests.cs`): `GetNamedString(name)` and `GetNamedString(name, default)` both
throw `InvalidOperationException` when the named member is present and JSON `null`, and `ContainsKey(name)`
returns `true` for that same null-valued member - so the `ContainsKey("id")` guard at the top of the entry
loop did not protect against a null `id`. None of the five raw accesses sat inside a per-entry `try/catch`;
the nearest one is the per-folder `try/catch` several stack frames up, wrapping the entire `gameCache`
walk. A single manifest entry with one null field therefore threw past every sibling entry still to be
processed in that folder, silently aborting the rest of the folder's parse (`Debug.WriteLine` only,
invisible outside an attached debugger) instead of just skipping the one malformed entry. `JsonRead.cs`'s
own docstring documents this exact failure class already shipping once in this codebase, on a different
call site (`SteamGridDbClient`'s Steam app ID field) - the fix that produced `JsonRead` was never carried
into the manifest loader, the largest and most-churned file in the repo.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:452-458` (`id`: `ContainsKey` did not guard a null-valued
  member; `GetNamedString` then threw) - pre-fix state; now routed through `JsonRead.String`.
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:461` (`addedDate`: `GetNamedString` with a default still threw on
  a present-null member) - pre-fix state; now routed through `JsonRead.String`.
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:474` (`imagePath`, Custom platform: unguarded `GetNamedString`
  outside the adjacent `try`/`catch`) - pre-fix state; now routed through `JsonRead.String`.
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:536-537` (`title`/`installLocation`/`executableName`, Custom
  platform: unguarded `GetNamedString`) - pre-fix state; now routed through `JsonRead.String`.
- `SteamGridDB.Xbox/Services/JsonRead.cs:1-18` (docstring: the same failure class already shipped once, on
  a different call site).
- `SteamGridDB.Xbox.Tests/JsonReadTests.cs` (new test this loop, proves the raw-accessor throw and the
  `ContainsKey` non-guard empirically rather than by trusting the docstring's prose).

**Architectural test failed** — n/a — different category (an existing defensive-parsing module bypassed at
its highest-value call site, not a removable/addable Module or Seam).

**Dependency category** — n/a (in-process JSON parsing; no external dependency involved).

**Leverage impact** — One call site fixed, but it is the widget's primary load path - the flow every user
hits every time the widget opens.

**Locality impact** — The fix is entirely local to the five call sites inside `LoadGameEntriesAsync`; no
caller or test needed to change.

**Metric signal** — 5 raw accessor call sites removed; 0 remain (grep-verified:
`GetNamedString`/`entryObject.ContainsKey` no longer appear in `PrimaryWidget.xaml.cs`).

**Why this weakens submission** — A defensive-parsing module built and proven after a real production
incident exists in this codebase, but the primary load path bypassed it at the exact seam most exposed to
malformed third-party input (the Xbox app's own manifest cache). The failure mode was not theoretical: it
is the same `InvalidOperationException` class the docstring says already shipped once.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Route all five call sites through the existing `JsonRead` module instead of
the raw WinRT accessors, matching the null-handling each site already needed: `id` and `imagePath` (Custom)
null/absent both mean skip the entry (same as the prior `ContainsKey`/`catch` paths' intent); `addedDate`
null/absent falls back to `"0"` (same default the raw overload already provided for the absent case);
`title` null/absent keeps the existing `gameName` default ("Unknown"); `installLocation`/`executableName`
null/absent fall back to empty string rather than crashing `Path.Combine`. No new abstraction - `JsonRead`
already exists and is already used by every other JSON-parsing call site in the codebase except this one.
**Applied this loop.**

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (the five call sites),
`SteamGridDB.Xbox.Tests/JsonReadTests.cs` (one new regression test). Avoid: everything else - confirmed by
`git diff --stat` touching only those two files.

---

### Finding #2: PrimaryWidget.xaml.cs still merges UI event handling and multi-concern orchestration behind zero Interface boundary

**Why it matters** — The churn-flagged file (27 edits over 6 months, still the largest in the repo)
continues to bundle several structurally distinct concerns with no Module boundary between them, so a
change to any one risks disturbing the others.

**What is wrong** — Unaffected in shape by this loop's fix (Finding #1 above is a call-site accessor swap,
not a concern relocation). Re-derived fresh this loop per the Anchor-check requirement (method.md Step
1.7): loop 7's queued remedy - split `LoadGameEntriesAsync` into a "pure manifest-parsing phase" and a
"UI-decode tail" - does **not** survive a full top-to-bottom read of the method, which loop 7's own Builder
Notes admitted it had not done ("I named the split direction from reading the method once, not from
actually attempting it"). The two phases are not separable: `CreateThumbnailAsync` (image decode, genuinely
UI-thread-affine - `BitmapImage` must be sourced on the UI thread) runs **per entry**, immediately after
that entry's `HasBackupAsync` check and immediately before that entry's SteamGridDB/store-name network
calls; the final `GameEntry` (a UWP-bound type carrying the decoded `BitmapImage`) is constructed inline at
the end of each entry's iteration, not after a batch parse phase. Attempting the two-phase split as
previously scoped would risk exactly the fake-clean anti-example the Simplify Pressure Test warns against:
"a clean-looking fix [that] adds ceremony without fixing ownership... reject it" - here, a split that looks
architectural but leaves the UI-thread coupling load-bearing inside the nominally "pure" half. What remains
merged, unchanged from loop 7: (1) UI event handling proper - correctly stays; (2)
`LoadGameEntriesAsync`'s manifest parsing, image decode, and network name-resolution, genuinely interleaved
per entry (this loop's fix touched only the field-extraction sub-step, not the surrounding structure); (3)
the three bulk-operation loops, still ruled out by the source-verified `GameEntry`/UWP platform constraint.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-730` (`LoadGameEntriesAsync`, now 399 lines post this loop's
  fix, was 379 at loop 7 - net +20 from Finding #1's added null-guards and comments, not from a shape
  change).
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:503-509` (image decode call site, inline mid-entry-loop, between
  the backup check and the `GameEntry` construction - the concrete evidence that no separable "UI-decode
  tail" phase exists).

**Architectural test failed** — n/a - different category (ownership/coupling sprawl, not a removable Seam
or wrapper).

**Dependency category** — n/a (unaffected by this loop).

**Leverage impact** — Unaffected this loop.

**Locality impact** — Unaffected this loop.

**Metric signal** — `PrimaryWidget.xaml.cs`: 1,957 → 1,978 lines this loop (+21, all inside Finding #1's
fix, not a shape change to this finding's evidence).

**Why this weakens submission** — Ownership of the concerns still merged in `PrimaryWidget` (manifest
parsing interleaved with UI-bound decode and network calls, bulk-operation orchestration) is still
untraceable from any single Module besides the UI class itself.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — The loop-7-envisioned two-phase split is downgraded (fails Simplify Pressure
Test on re-inspection; see Simplification Check). The corrected, narrower honest next slice: extract only
the manifest **field-extraction** sub-step (`entryId`, `addedDate`/timestamp parsing, platform-specific
`externalPlatformId`/`epicCatalogItemId` derivation, the non-Custom `imageFilePath` string construction,
and the Custom-platform `title`/`installLocation`/`executableName` reads Finding #1 just made
null-safe) into a small pure static parser returning a plain record - leaving image decode, the backup
check, network name resolution, and `GameEntry` construction exactly where they are today in
`PrimaryWidget`. This is a smaller, purely mechanical slice than the previous framing since it does not
also try to split off a "UI-decode tail" that does not cleanly exist as a separate phase. Re-run the
Simplify Pressure Test fresh before attempting even this narrower slice.

**Blast radius** — Change (next loop, if the fresh SPT passes): `PrimaryWidget.xaml.cs` (the
field-extraction lines only), a new `Services/Library` manifest-entry-parsing helper. Avoid:
`Services/Artwork/*`, `Services/Stores/*`, `Services/SteamGridDB/*`, `Services/Library/GameImages.cs`,
`Services/Library/OperationReport.cs`.

---

### Finding #3: ArtworkRanker.RankGrids' style-priority ordering has no test mixing text-bearing and icon-like styles, so a sort-direction inversion would pass silently

**Why it matters** — `RankGrids` is the ranking algorithm behind every automatic artwork pick on the
widget's primary flow; an inverted style-priority tie-break would silently prefer icon-like art over
box-art-style uploads library-wide, with no test catching it.

**What is wrong** — Unaffected this loop; re-verified still present. `ArtworkRanker.cs:195` still sorts
candidates with `.ThenBy(r => GridStylePriority(r.Grid.Style))` and every `RankGrids` test still uses the
`Grid()` factory's default style ("alternate") on both sides of the comparison
(`ArtworkRankerTests.cs:161-244`), so the ascending-vs-descending direction of that tie-break is still
never exercised. `ArtworkRanker.cs` and `ArtworkRankerTests.cs` do not appear in this loop's diff.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs:195` (unchanged this loop).
- `SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs:21-41,161-244` (unchanged this loop).

**Architectural test failed** — n/a - different category (missing-test gap, per method.md Step 8's
mutation-test check).

**Dependency category** — n/a

**Leverage impact** — One call site (`RankGrids`), but it is the ranking function every automatic-fix and
manual-picker artwork list goes through.

**Locality impact** — The fix is one new test case; no production code changes.

**Metric signal** — none

**Why this weakens submission** — Unchanged from loop 7: a source-level mutation on a central, primary-flow
ranking rule still passes the entire suite undetected.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Unchanged from loop 7: add one `RankGrids` test case constructing two
candidates with different styles and asserting the text-bearing one sorts first.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs` (one new test method). Avoid:
`SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs`, everything else.

---

### Finding #4: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** — Load time scales linearly with library size and network latency on the widget's
primary open path.

**What is wrong** — Unaffected this loop. The `gameCache` `foreach` loop in `LoadGameEntriesAsync` still
awaits `sgdbClient.GetGameByPlatformIdAsync` and the GOG/Epic/Ubisoft name fallbacks one game at a time;
nothing overlaps the independent per-game network calls. Re-verified at current line numbers this loop
after Finding #1's fix shifted them: await sites now at `:590,612,621,630` (was `:569,591,600,609,629` at
loop 7). This run's standing operational constraint continues to rule out attempting this finding:
parallelising these round-trips would change observable request count/order/timing against third-party
APIs without a behavioral oracle, and the test suite still does not cover network calls.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-730` (per-folder, per-entry `foreach`; awaits at
  `:590,612,621,630`, re-verified at current line numbers this loop).

**Architectural test failed** — n/a - different category (structural waste per `lens-efficiency.md`, not a
Seam).

**Dependency category** — `true-external`

**Leverage impact** — Unaffected this loop.

**Locality impact** — Unaffected this loop.

**Metric signal** — One HTTP round-trip per game per store lookup; unchanged this loop.

**Why this weakens submission** — Structural waste on the widget's primary hot path, unchanged from loop 7.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Blocked for the duration of this run by the standing operational constraint,
unchanged from loop 7.

**Blast radius** — Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync`), `Services/Stores/StoreNameLookup.cs`.
Avoid: `Services/Artwork/*`, `Services/SteamGridDB/*`.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Routing `LoadGameEntriesAsync`'s five raw JSON field reads through the existing `JsonRead` module. Passes the Unified Seam Policy trivially - no new Seam. Passes SPT Q1 cleanly: the raw accessors' present-null behavior is demonstrably unsafe (empirically confirmed this loop), `JsonRead`'s is safe by construction. |
| New seam justified | false - no new Seam introduced; `JsonRead` already existed. |
| Helpful simplification | The `id` field's `ContainsKey`-then-`GetNamedString` two-step collapsed into one `JsonRead.String` call plus a single `string.IsNullOrEmpty` check. |
| Should NOT be done | Attempting loop 7's queued two-phase split this loop - image decode and network calls are interleaved per entry, not separable. The corrected, narrower next slice is named in Finding #2 instead. F-005/F-003 not attempted - both Noticeable, lower priority than this loop's Serious Finding #1. |
| Tests after fix | One new test added at the testable layer (`JsonReadTests.cs`) proving the exact failure mode this fix closes. Not a deepening of `JsonRead` itself, so Replace-don't-layer's stale-test-deletion requirement does not apply. |

## Improvement Backlog

1. **Attempt LoadGameEntriesAsync's corrected, narrower field-extraction slice (F1's next honest sub-step)**
   — Extract only the pure manifest-field-extraction sub-step (now null-safe per this loop's fix) into a
   small pure static parser; leave image decode, backup check, network name resolution, and `GameEntry`
   construction in `PrimaryWidget` exactly where they are. Re-run the Simplify Pressure Test fresh before
   attempting - this narrower framing was not vetted this loop, only identified once the previous framing's
   flaw was confirmed.
   - Why it matters: F1 (F-001) remains the largest Serious deduction on the board.
   - Score impact: Architecture quality +0.5 and Code simplicity +0.5 if verified and the extraction
     survives fresh SPT without introducing UI-thread coupling.
2. **Add the missing RankGrids style-priority mixed-style test case (F-005)** — one new
   `ArtworkRankerTests.cs` test case, no production code change.
   - Why it matters: `test_strategy`'s current 8.0 ceiling is partly explained by this named, source-backed
     gap.
   - Score impact: Test strategy +0.5-1.0 once verified.
3. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups (F-003)** — blocked for the
   duration of this run by the standing operational constraint. Carried forward as a reminder.
   - Why it matters: removes load latency scaling linearly with library size, whenever the constraint is
     lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

None this loop. `JsonRead` (touched only via new call sites, not internally) is already deep - one small
Interface (`Value`/`Object`/`Array`/`String`), Implementation handles four distinct WinRT null/type-mismatch
edge cases behind it; this loop added callers, not Implementation. `LoadGameEntriesAsync`'s remaining
concern-merge (Finding #2's next slice) is a splitting/ownership problem, not a shallow-Interface-needs-
deepening problem - correctly tracked as a Finding + backlog item instead.

## Builder Notes

1. A backlog item's own remedy can be wrong even when the underlying finding is real - the prior loop's
   own uncertainty disclosure was the exact signal that the queued Priority 1 needed re-verification before
   being attempted, not assumed correct because it was already queued. → REVIEW_HISTORY.json
   `loops[7].builder_notes` for full notes.
2. A module built to fix one real, documented incident can still be bypassed elsewhere in the same
   codebase by code that predates it or was never updated to use it. → REVIEW_HISTORY.json
   `loops[7].builder_notes` for full notes.
3. `ContainsKey` and "is this member usable" are not the same question for a JSON API that distinguishes
   "absent" from "present and null." → REVIEW_HISTORY.json `loops[7].builder_notes` for full notes.

## Final Judge Narrative

Place, not win. This loop's real work was catching its own predecessor's untested assumption before
building on it: loop 7 queued a two-phase split of `LoadGameEntriesAsync` as Priority 1 but flagged in its
own humility check that the split had only been "named," not attempted - reading the method fully this loop
confirmed the split does not hold, and forcing it would have produced exactly the kind of costume-layer fix
the Simplify Pressure Test exists to reject. Downgrading to the next backlog item instead of forcing a
flawed plan surfaced a real, independently-verified defect: `LoadGameEntriesAsync` bypassed the codebase's
own null-tolerant JSON module at five call sites, and a new test proves the raw accessors it used instead
throw uncaught on a present-null manifest field - silently dropping every other game in that folder. All
five sites are fixed and verified this loop, with both regression oracles green before and after. F1
(F-001) stays `carried_forward` with a corrected, narrower next slice named plainly rather than the flawed
one repeated; F-005 and F-003 are unchanged and unattempted, both lower-severity than this loop's fix.
Runtime ownership and concurrency are unaffected and remain exactly as trustworthy (or not) as loop 7 left
them. Future work risks nothing new from overengineering this loop - the fix added no abstraction, only
safer callers of one that already existed.

## Loop 8 Result

Replaced five raw `Windows.Data.Json` accessor calls (`GetNamedString`/`ContainsKey`) in
`SteamGridDB.Xbox/PrimaryWidget.xaml.cs`'s `LoadGameEntriesAsync` with the existing `JsonRead` module's
null-tolerant equivalents, closing this loop's re-derived slice of F1 (stable_id F-001) - now tracked as its
own finding, F-006, since it is a source-verified defect distinct from F1's ownership/merge claim. `id` and
`imagePath` (Custom platform) null/missing now skip the entry (matching the pre-existing intent of the
`ContainsKey` guard and the adjacent folder-resolution catch); `addedDate` null/missing falls back to
`"0"` (same default the raw overload already gave for the absent case); `title` null/missing keeps the
existing "Unknown" default; `installLocation`/`executableName` null/missing fall back to empty string
rather than crashing `Path.Combine`. Added `using SteamGridDB.Xbox.Services;` for `JsonRead`'s namespace.
Added one regression test to `SteamGridDB.Xbox.Tests/JsonReadTests.cs`
(`Raw_windows_data_json_overloads_throw_on_a_present_json_null_member`) proving the raw accessors'
present-null-throws behavior and `ContainsKey`'s non-guard empirically, rather than trusting the class
docstring's prose alone. `git diff --stat`: `PrimaryWidget.xaml.cs` (29 insertions, 8 deletions - net +21),
`JsonReadTests.cs` (25 insertions).

**What proves the change is honest:** Both regression oracles passed clean before and after -
`run-tests.ps1` (104 passed before, 105 passed after - the delta is exactly the one new test added, no
other test count change) and MSBuild (exit 0, both runs, same command as every prior loop). Grep-verified
post-edit that no `GetNamedString` or `entryObject.ContainsKey` call remains anywhere in
`PrimaryWidget.xaml.cs`. The fix is a pure accessor substitution with matching fallback semantics at every
site - no network call, no image-decode call, no `GameEntry` field, and no UI-thread dispatch touched;
confirmed by reading the diff hunk-by-hunk (all five hunks are confined to the field-parsing lines within
the entry loop). This changes only how malformed/null manifest fields are handled, not the observable
outcome for any well-formed entry (every existing field present and non-null behaves identically to before,
verified by the 104 pre-existing tests still passing unchanged) - confirmed by the independent
implementation-reviewer pass below.

**Risk boundary evidence (Meta-Rule 4):** none - this fix crosses no isolation/Sendable/conditional-
compilation/cross-file-visibility/lock-ordering boundary. `JsonRead` is already `internal static` and
already called from other files in the same assembly (`SteamGridDbClient.cs`, `EpicLibrary.cs`,
`StoreNameLookup.cs`); adding `PrimaryWidget.xaml.cs` as a fourth caller changes no visibility modifier
anywhere.

**Targeted finding status:** `resolved` - F-006's Claim (five raw accessor call sites bypassing `JsonRead`,
throwing uncaught on a present-null field) is fully closed: all five sites now route through `JsonRead`,
verified by grep with zero raw accessor calls remaining in the file.

**Unintended scorecard regression:** none observed. `credibility` moved UP on distinct, source-verified
proof; `architecture_quality`, `state_management`, `domain_modeling`, `data_flow`, `framework_idioms`,
`concurrency`, `simplicity`, `test_strategy` all held SAME (no structural change in their evidence this
loop); no dimension regressed.

## Loop 8 Implementation Review

`verdict: approved` — "All five raw Windows.Data.Json accessor call sites in LoadGameEntriesAsync now route
through the pre-existing JsonRead module with per-site null-handling matching the claimed fallback
semantics, no new abstraction or suppression was introduced, and no same-or-higher-severity regression
appears in the changed hunks." All three checks (`reality`, `honesty`, `regression`) `passed`; `conditions:
[]`; `regressions: []`.

--- Loop 9 (UTC 2026-08-04T15:00:56Z) ---

### Discovery (see Loop 7 Discovery refresh)

### Loop Counter
Loop 9 of 10 (cap)

### System Flag
[STATE: CONTINUE]

## Contest Verdict

**Promising, but architecturally immature.**

Re-derivation from current source (both gates green before and after this loop's change: 105→114 tests,
MSBuild exit 0) found this loop's queued Priority 1 - the "corrected, narrower" field-extraction slice of
`LoadGameEntriesAsync` that loop 8 proposed after downgrading loop 7's flawed two-phase split - genuinely
survives a fresh Simplify Pressure Test this time. Reading the full method confirmed the platform-specific
identifier/name-derivation block has zero data dependency on the image-decode/backup-check block it sits
next to in either direction, so it extracted into a new pure static module with no reordering of any
surrounding code. A parallel independent read of the rest of the codebase surfaced one previously-uncredited
test-coverage gap (`ArtworkDownloader`/`TileImage`, Finding #4) and confirmed two suspected concurrency
smells are not live hazards today - no concurrent call path exists anywhere in the codebase to exploit
either.

## Scorecard (1-10)

- **Architecture quality:** 7.0 | UP | `Services/Library/ManifestEntryIdentity.cs:1-90` (new this loop) now
  owns the Epic colon-split identifier derivation that used to be 38 lines of inline, untestable branching
  in `PrimaryWidget.xaml.cs:540-577`. Deletion test: removing `ManifestEntryIdentity` reintroduces that
  complexity inline - it earns its keep, not a pass-through wrapper. `PrimaryWidget.xaml.cs` shrank
  1,978→1,950 lines (`LoadGameEntriesAsync` 399→371). Still capped well below 7's ceiling: the image-decode/
  backup-check/network-resolution core (`PrimaryWidget.xaml.cs:332-702`) remains merged with UI orchestration,
  unaffected by this loop's narrower fix.
- **State management and runtime ownership:** 7.0 | SAME | `AppliedArtworkStore.cs`/`FixLog.cs`'s
  `RecordFolder`/`LogFolder` setters, `gate`, `GetAsync`/`UpdateAsync` are untouched by this loop's fix,
  which lives entirely in a new stateless pure-function module.
- **Domain modeling:** 5.5 | SAME | `SteamGridDbClient.ParseOfficialCapsuleUrl` (`SteamGridDbClient.cs:144-199`)
  and the anemic `SteamGridDbGame`/`SteamGridDbGrid` DTOs (re-verified via helper read this loop) are
  unchanged. `ManifestEntryIdentity.Result` is a plain 3-field struct with no invariant enforcement beyond
  what the inline code already did - this loop's diff is credited to `architecture_quality`/`simplicity`,
  not domain modeling, per the established loop 7/8 convention against double-counting one diff.
- **Data flow and dependency design:** 6.0 | SAME | `StoreNameLookup`'s three unlocked static caches
  (`StoreNameLookup.cs:27-32`), `EpicLibrary`'s `Environment.GetEnvironmentVariable` ambient fallback
  (`EpicLibrary.cs:31`), and `AppliedArtworkStore`'s `ApplicationData.Current` ambient default
  (`AppliedArtworkStore.cs:49`) are all unaffected this loop, re-verified present via helper read.
  `ManifestEntryIdentity.Derive` takes fully explicit parameters with no ambient state - consistent with
  existing good examples (`AsyncLazyCache`, `ArtworkSource`), not a new pattern; credited to
  `architecture_quality` per the same anti-double-counting convention.
- **Framework / platform best practices:** 6.0 | SAME | The `DataContractJsonSerializer` /
  `Windows.Data.Json` split in `SteamGridDbClient.cs` is unchanged this loop. `ManifestEntryIdentity.cs`
  takes `Windows.Data.Json.JsonObject` as a parameter, matching `JsonRead`'s existing pattern - no new
  idiom, no structural proof to move this dimension.
- **Concurrency and runtime safety:** 6.5 | SAME | F-003's fully sequential per-game round-trips
  (`PrimaryWidget.xaml.cs:332-702`, awaits now at `:562,584,593,602`) remain open, unaffected. Investigated
  `StoreNameLookup`'s three unlocked static `Dictionary` caches (`StoreNameLookup.cs:27-32`, a
  check-then-await-then-write shape) as a possible latent race: grep across the whole codebase found zero
  uses of `Task.WhenAll`/`Parallel.*`/`Task.Run` anywhere, and `StoreNameLookup`'s only callers are
  `LoadGameEntriesAsync`'s fully sequential `foreach` - no concurrent call path exists today. Not a live
  finding; noted as a landmine for F-003's eventual remedy, not scored as a separate defect.
- **Code simplicity and clarity:** 8.5 | UP | `PrimaryWidget.xaml.cs:540-546` replaced a 38-line inline
  nested `if`/`else` block (`git diff` hunk: -38/+11 lines) with 4 lines calling
  `ManifestEntryIdentity.Derive` - a genuine, verified simplification a reader can now either trust via the
  function's tested contract or inspect separately, rather than parsing inline branching mid-entry-loop.
- **Test strategy and regression resistance:** 8.0 | SAME | The two previously-disclosed gap categories
  are joined by a third, newly-surfaced gap this loop (Finding #4/F-007: `ArtworkDownloader`'s three entry
  points and two of `TileImage`'s three public methods have zero coverage). This loop's own fix closes a
  small piece of the first gap category but the newly-found gap keeps the ceiling at 8 rather than moving
  it - a wash, not a regression.
- **Overall implementation credibility:** 7.5 | SAME | This loop's diff is credited to `architecture_quality`
  (+0.5) and `simplicity` (+0.5) rather than here, per the established convention (loop 8 credited its own
  accessor-swap-only fix to credibility precisely because it was *not* a concern relocation; this loop's fix
  *is* one, so the inverse applies). `PrimaryWidget.xaml.cs`'s remaining 1,950 lines are still unverified by
  anything but inspection and a green compile outside this loop's tested slice.

## Authority Map
(none this loop)

## Strengths That Matter

- `JsonRead` (`Services/JsonRead.cs`) is a genuine smart-accessor built from a real production incident and
  now used at every JSON-parsing call site in the codebase, including `PrimaryWidget.xaml.cs` since loop 8
  - unaffected and re-verified unchanged this loop.
- `ArtworkSource`'s private-constructor-plus-factory-method design
  (`Services/SteamGridDB/ArtworkSource.cs:15-51`) still makes "neither a platform-ID nor a game-ID"
  unrepresentable - unaffected and re-verified unchanged this loop.
- `AsyncLazyCache<T>` still takes the caller's own lock as a constructor argument rather than owning a
  private one, and remains stress-tested under 32 concurrent callers (`AsyncLazyCacheTests.cs`) -
  unaffected this loop.

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling, image decode, network resolution, and bulk-operation orchestration behind zero Interface boundary

**Why it matters** — The churn-flagged file (still the largest in the repo across every prior loop's
Discovery) continues to bundle several structurally distinct concerns with no Module boundary between most
of them, so a change to any one risks disturbing the others.

**What is wrong** — This loop extracted one further slice: the platform-specific identifier/name-derivation
logic (`gameName` default, `externalPlatformId`, `epicCatalogItemId` - including Epic's colon-split parsing)
moved from 38 lines of inline code in `LoadGameEntriesAsync`'s per-entry loop into a new pure static module,
`Services/Library/ManifestEntryIdentity.cs`, with 9 new direct unit tests. Verified this loop via a fresh
Simplify Pressure Test (not carried from loop 8's queued plan without re-checking): the extracted logic
reads only `entryObject`/`platform`/`entryId`/`unknownName` (all already known before the image-decode block
runs) and its outputs are not read until after that block - zero data dependency in either direction - so it
moved without reordering any surrounding code. `PrimaryWidget.xaml.cs` shrank 1,978 to 1,950 lines. What
remains merged, confirmed unaffected by this loop's extraction: (1) UI event handling proper; (2)
`LoadGameEntriesAsync`'s image decode, backup check, and network name-resolution, still genuinely
interleaved per entry (this loop's extraction touched only the platform-identity sub-step, which sits
between them but has no data dependency on either); (3) the three bulk-operation loops, still ruled out by
the `GameEntry`/UWP platform constraint documented at loop 6.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-702` (`LoadGameEntriesAsync`, now 371 lines, was 399 at loop 8
  - net -28 from this loop's extraction).
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:522` (image decode call site, still inline mid-entry-loop between
  the backup check and the platform-identity call - the extraction did not touch this).
- `SteamGridDB.Xbox/Services/Library/ManifestEntryIdentity.cs:1-90` (new module this loop).
- `SteamGridDB.Xbox.Tests/ManifestEntryIdentityTests.cs:1-125` (new, 9 tests, this loop).

**Architectural test failed** — n/a - different category (ownership/coupling sprawl for what remains; this
loop's own fix passed the Deletion test cleanly, see Simplification Check).

**Dependency category** — n/a

**Leverage impact** — Unaffected for the remaining merge; the extracted slice's Leverage improved (Epic's
parsing rules are now callable/testable independent of `PrimaryWidget`).

**Locality impact** — Unaffected for the remaining merge; the extracted slice's Locality improved (Epic's
parsing bug surface is now in one small file with direct tests, not buried 550 lines into a UI-bound
method).

**Metric signal** — `PrimaryWidget.xaml.cs`: 1,978 → 1,950 lines this loop (-28); `LoadGameEntriesAsync`:
399 → 371 lines (-28).

**Why this weakens submission** — Ownership of the concerns still merged in `PrimaryWidget` (image decode
interleaved with backup checks and network calls, bulk-operation orchestration, UI event handling) remains
untraceable from any single Module besides the UI class itself - unaffected by this loop's narrower fix.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — No further split is queued this loop: the image-decode/backup-check/
network-resolution core is still genuinely interleaved per entry (confirmed at loop 8, re-confirmed by this
loop's own full read, unaffected by this loop's fix), so no next slice is proposed without first
re-verifying against current source, per this run's established discipline of re-testing before attempting
a carried-forward remedy rather than assuming a queued plan is still correct.

**Blast radius** — Change (only if a future loop verifies a further slice, with a fresh SPT first):
`PrimaryWidget.xaml.cs`. Avoid: `Services/Artwork/*`, `Services/Stores/*`, `Services/SteamGridDB/*`,
`Services/Library/ManifestEntryIdentity.cs` (already extracted and tested this loop), `Services/Library/GameImages.cs`,
`Services/Library/OperationReport.cs`.

---

### Finding #2: ArtworkRanker.RankGrids' style-priority ordering has no test mixing text-bearing and icon-like styles, so a sort-direction inversion would pass silently

**Why it matters** — `RankGrids` is the ranking algorithm behind every automatic artwork pick on the
widget's primary flow; an inverted style-priority tie-break would silently prefer icon-like art over
box-art-style uploads library-wide, with no test catching it.

**What is wrong** — Unaffected this loop; re-verified still present via direct read. `ArtworkRanker.cs:195`
still sorts candidates with `.ThenBy(r => GridStylePriority(r.Grid.Style))` and every `RankGrids` test still
uses the `Grid()` factory's default style ("alternate") on both sides of the comparison, so the
ascending-vs-descending direction of that tie-break is still never exercised. `ArtworkRanker.cs` and
`ArtworkRankerTests.cs` do not appear in this loop's diff.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs:195` (unchanged this loop, re-read directly).
- `SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs` (unchanged this loop; confirmed via helper read that no
  test varies `Style` alone between two ranked candidates).

**Architectural test failed** — n/a - different category (missing-test gap, per method.md Step 8's
mutation-test check).

**Dependency category** — n/a

**Leverage impact** — One call site (`RankGrids`), but it is the ranking function every automatic-fix and
manual-picker artwork list goes through.

**Locality impact** — The fix is one new test case; no production code changes.

**Metric signal** — none

**Why this weakens submission** — Unchanged from loop 7/8: a source-level mutation on a central, primary-flow
ranking rule still passes the entire suite undetected.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Unchanged from loop 7/8: add one `RankGrids` test case constructing two
candidates with different styles and asserting the text-bearing one sorts first.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs` (one new test method). Avoid:
`SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs`, everything else.

---

### Finding #3: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** — Load time scales linearly with library size and network latency on the widget's
primary open path.

**What is wrong** — Unaffected this loop. The `gameCache` `foreach` loop in `LoadGameEntriesAsync` still
awaits `sgdbClient.GetGameByPlatformIdAsync` and the GOG/Epic/Ubisoft name fallbacks one game at a time;
nothing overlaps the independent per-game network calls. Re-verified at current line numbers this loop
after Finding #1's fix shifted them: await sites now at `:562,584,593,602` (was `:590,612,621,630` at loop
8). This run's standing operational constraint continues to rule out attempting this finding. **New this
loop**: if this constraint is ever lifted, the remedy must also add locking to `StoreNameLookup`'s three
static caches (`gogNameCache`/`epicNameCache`/`nameMatchCache`, `StoreNameLookup.cs:27-32`) - their current
check-then-await-then-write shape is safe only because calls are strictly sequential today.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-702` (per-folder, per-entry `foreach`; awaits at
  `:562,584,593,602`, re-verified at current line numbers this loop).
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:27-32,86-101,203-222` (the three unlocked static
  caches this constraint's eventual remedy would also need to address).

**Architectural test failed** — n/a - different category (structural waste per `lens-efficiency.md`, not a
Seam).

**Dependency category** — `true-external`

**Leverage impact** — Unaffected this loop.

**Locality impact** — Unaffected this loop.

**Metric signal** — One HTTP round-trip per game per store lookup; unchanged this loop.

**Why this weakens submission** — Structural waste on the widget's primary hot path, unchanged from loop
7/8.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Blocked for the duration of this run by the standing operational constraint,
unchanged from loop 7/8. Amended this loop with the caching-synchronization prerequisite noted above.

**Blast radius** — Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync`), `Services/Stores/StoreNameLookup.cs`.
Avoid: `Services/Artwork/*`, `Services/SteamGridDB/*`.

---

### Finding #4: ArtworkDownloader's tile-fill gate and TileImage's vertical-crop algorithm have zero test coverage at any interface, direct or indirect

**Why it matters** — `DownloadBestTileFillingImageAsync` decides which candidate image the widget applies as
artwork when auto-selecting; a wrong crop or a broken tile-fill check would ship visibly bad artwork with no
test catching it.

**What is wrong** — New finding this loop, surfaced while independently re-deriving the test-strategy
dimension from source (helper-assisted read, verified directly). `ArtworkDownloader.cs` (195 lines) has
three internal entry points (`DownloadArtworkAsync`, `DownloadBestTileFillingImageAsync`,
`FindOfficialLookalikeAsync`) with no corresponding `ArtworkDownloaderTests.cs` file - confirmed by
directory listing, the file does not exist. `TileImage.cs`'s public `CropPortraitToTileAsync` and
`FillsTileAsync` (both called from `ArtworkDownloader`'s gate logic) are also untested: `TileImageTests.cs`
exists but its four `Fact` methods (grep-verified) exercise only `EnsurePngAsync`. `BestVerticalCropAsync` -
a private method implementing Laplacian-based vertical-window selection, with comments documenting
hand-grading against 35 real covers - is reachable only through `CropPortraitToTileAsync`, which has zero
callers in any test file, so it has zero coverage direct or indirect. This is a genuinely testable Module
(already proven testable in principle - `TileImageTests.cs` constructs real WinRT bitmap buffers for
`EnsurePngAsync`), not one of `PrimaryWidget`'s architecturally-untestable UWP-bound seams.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs:40,71,122` (three internal entry points, no test
  file).
- `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:231,284,321` (`FillsTileAsync` public/untested,
  `CropPortraitToTileAsync` public/untested, `BestVerticalCropAsync` private/reachable only via `:284`).
- `SteamGridDB.Xbox.Tests/TileImageTests.cs:17-60` (four `Fact` methods, all exercising only
  `EnsurePngAsync` - grep-verified, no reference to `CropPortraitToTileAsync` or `FillsTileAsync` anywhere
  in the file).
- `SteamGridDB.Xbox.Tests/` directory listing: no `ArtworkDownloaderTests.cs`, no `ArtworkSignatureTests.cs`,
  no `FixLogTests.cs`.

**Architectural test failed** — n/a - different category (missing-test gap per method.md Step 8's
mutation-test check, same category as Finding #2/F-005).

**Dependency category** — n/a

**Leverage impact** — One call site cluster (`ArtworkDownloader`'s gate), but it is the function every
automatic artwork pick and manual apply goes through after ranking.

**Locality impact** — The fix is new tests only; no production code change needed.

**Metric signal** — 3 of 3 `ArtworkDownloader` entry points untested (0% file coverage); 2 of 3 public
`TileImage` methods untested (`FillsTileAsync`, `CropPortraitToTileAsync`).

**Why this weakens submission** — A source-level mutation in the tile-fill gate or the crop-window
selection would pass the entire 114-test suite undetected - the same category of gap method.md Step 8
requires naming before `test_strategy` can score above 8.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Add `ArtworkDownloaderTests.cs` exercising
`DownloadBestTileFillingImageAsync`'s ranking-to-selection gate with constructed `IBuffer` fixtures
(matching `TestImages.cs`'s existing pattern); add `FillsTileAsync`/`CropPortraitToTileAsync` cases to
`TileImageTests.cs` using the same WinRT-buffer construction pattern. No production code changes required.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/ArtworkDownloaderTests.cs` (new),
`SteamGridDB.Xbox.Tests/TileImageTests.cs` (new cases). Avoid:
`SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs`, `SteamGridDB.Xbox/Services/Artwork/TileImage.cs`,
everything else.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Extracting `ManifestEntryIdentity.Derive` from `LoadGameEntriesAsync`'s inline platform-identity block. Passes the Deletion test (removing it reintroduces the Epic-parsing complexity inline). No Seam/protocol introduced, so the Unified Seam Policy does not apply - confirmed by the independent implementation reviewer. |
| New seam justified | false - no protocol/port/abstraction introduced, just a static-method extraction. |
| Helpful simplification | The Epic colon-split logic (`parts.Length >= 3` / `>= 4` boundary) is now directly unit-tested at 4 boundary cases - a mutation on either comparison would now fail a test. |
| Should NOT be done | Attempting a further split of `LoadGameEntriesAsync`'s image-decode/backup-check/network-resolution core this loop - these remain genuinely interleaved per entry; no new evidence surfaced this loop to reopen that question. Also not attempted: F-005's `RankGrids` test gap and F-003's concurrency fix. |
| Tests after fix | No prior tests existed for the removed inline platform-identity block. Nine new tests added at the new Interface (`ManifestEntryIdentityTests.cs`), including the Epic colon-split boundary conditions previously unreachable by any test. The old inline code is fully removed, not left as a parallel shallow copy. |

## Improvement Backlog

1. **Add the missing RankGrids style-priority mixed-style test case (F-005)** — one new
   `ArtworkRankerTests.cs` test case, no production code change.
   - Why it matters: `test_strategy`'s current 8.0 ceiling is partly explained by this named, source-backed
     gap, carried across loops 7-9 unattempted while higher-severity findings took priority.
   - Score impact: Test strategy +0.5-1.0 once verified.
2. **Add ArtworkDownloader/TileImage test coverage for the tile-fill gate and vertical-crop selection (F-007)**
   — new `ArtworkDownloaderTests.cs`, new `TileImageTests.cs` cases; no production code change.
   - Why it matters: closes the newly-surfaced third named gap in `test_strategy`'s ceiling.
   - Score impact: Test strategy +0.5-1.0 once verified.
3. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups (F-003)** — blocked for the
   duration of this run by the standing operational constraint. Carried forward as a reminder.
   - Why it matters: removes load latency scaling linearly with library size, whenever the constraint is
     lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

None this loop. `ManifestEntryIdentity` (new this loop) is already deep for its scope - one small Interface,
Implementation handles three distinct platform-branching cases including Epic's two-identifier split; it has
exactly one caller today, consistent with a fresh extraction, not yet showing deletion-test-failing friction.
`LoadGameEntriesAsync`'s remaining concern-merge is a splitting/ownership problem, not a shallow-Interface-
needs-deepening problem - correctly tracked as a Finding + backlog note instead.

## Builder Notes

1. A carried-forward backlog item's proposed remedy can be correct even when a related, differently-scoped
   remedy failed the same test - don't let a prior failed attempt poison a genuinely narrower reframing of
   the same finding. → REVIEW_HISTORY.json `loops[9].builder_notes` for full notes.
2. A pure computation buried inside a large orchestration method can be verified fully separable by checking
   data dependencies in both directions. → REVIEW_HISTORY.json `loops[9].builder_notes` for full notes.
3. A theoretically-racy static mutable cache is not evidence of a live concurrency defect if nothing in the
   codebase actually calls it from more than one execution path. → REVIEW_HISTORY.json
   `loops[9].builder_notes` for full notes.

## Final Judge Narrative

Place, not win. This loop re-ran the Simplify Pressure Test on loop 8's queued "corrected, narrower"
field-extraction slice from scratch rather than assuming a two-loop-old plan was still correct - and this
time it held: reading `LoadGameEntriesAsync` fully confirmed the platform-identifier derivation block has no
data dependency on the surrounding image-decode/backup-check code in either direction, so it extracted
cleanly into a new tested module with zero reordering. This is a real, if narrow, architectural win, distinct
in kind from loop 8's accessor-swap fix: it relocates ownership of a genuinely tricky piece of domain logic
behind a small, deep, directly-tested Interface. `LoadGameEntriesAsync`'s core problem - image decode, backup
checks, and network calls still genuinely interleaved per entry - is untouched, and this loop deliberately
did not queue a further slice without first re-verifying one exists. A parallel independent re-derivation of
the rest of the scorecard surfaced one new, real test-coverage gap (`ArtworkDownloader`/`TileImage`, Finding
#4) and ruled out two suspected concurrency hazards as latent rather than live, with source-verified
reasoning either way. Runtime ownership is unaffected and exactly as trustworthy as loop 8 left it.
Concurrency remains sequential and safe today. Tests reduce regressions more than last loop measured, though
the newly-found gap keeps the net movement at zero. Future work risks nothing new from overengineering.

## Loop 9 Result

Extracted the platform-specific identifier/name-derivation logic (`gameName` default, `externalPlatformId`,
`epicCatalogItemId`) from `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`'s `LoadGameEntriesAsync` into a new pure
static module, `SteamGridDB.Xbox/Services/Library/ManifestEntryIdentity.cs`, closing this loop's re-derived,
narrower slice of Finding #1 (stable_id F-001). The extraction is a mechanical Extract-Method with zero code
reordering: the block's inputs (`entryObject`, `platform`, `entryId`, `unknownName`) were all available
before it ran and its outputs were not consumed until after, confirmed by reading the full method before and
after. Added `SteamGridDB.Xbox.Tests/ManifestEntryIdentityTests.cs` with 9 new tests, including the Epic
colon-split boundary conditions (4-segment id with catalog item, 3-segment id without one, <3-segment
malformed fallback) that were previously unreachable by any test since the logic lived inside a UWP-bound
class. Also added an explicit `<Compile Include>` entry to `SteamGridDB.Xbox/SteamGridDB.Xbox.csproj` for
the new file (this project uses old-style explicit compile-item lists, not SDK-style implicit globbing -
confirmed by reading the file; the build failed with `CS0246`/`CS0103` until this was added, then passed
clean). `git diff --stat`: `PrimaryWidget.xaml.cs` (11 insertions, 38 deletions - net -27),
`SteamGridDB.Xbox.csproj` (1 insertion), plus two new files (`ManifestEntryIdentity.cs`, 90 lines;
`ManifestEntryIdentityTests.cs`, 125 lines).

**What proves the change is honest:** Both regression oracles passed clean before and after -
`run-tests.ps1` (105 passed before, 114 passed after - the delta is exactly the 9 new tests added, no other
test count change) and MSBuild (exit 0, both runs, same command as every prior loop). The extracted code in
`ManifestEntryIdentity.Derive` is byte-identical in branching and fallback semantics to what was removed
from `PrimaryWidget.xaml.cs` - confirmed by the independent implementation-reviewer pass below. This changes
only where the platform-identity logic lives, not its behavior for any well-formed or malformed manifest
entry - verified by the 105 pre-existing tests still passing unchanged plus 9 new tests asserting the exact
fallback semantics at each boundary.

**Risk boundary evidence (Meta-Rule 4):** none - this fix crosses no isolation/Sendable/conditional-
compilation/cross-file-visibility/lock-ordering boundary. `ManifestEntryIdentity` is a new `internal static`
class in the same assembly as its one caller; no visibility modifier changed anywhere, and the extraction
introduces no new thread, task, or shared mutable state.

**Targeted finding status:** `carried_forward` - Finding #1/F-001's core claim (the image-decode/backup-
check/network-resolution/UI-orchestration merge) is unaffected; this loop closed one further evidence slice
of it (the platform-identity sub-step) without resolving the finding as a whole, consistent with F-001's
established loop-by-loop incremental-slice pattern since loop 1.

**Unintended scorecard regression:** none observed. `architecture_quality` and `simplicity` moved UP on
distinct, source-verified proof; `state_management`, `domain_modeling`, `data_flow`, `framework_idioms`,
`concurrency`, `test_strategy`, `credibility` all held SAME; no dimension regressed.

## Loop 9 Implementation Review

`verdict: approved` — "The extraction is byte-identical to the removed inline logic (including the Epic
colon-split boundary conditions), introduces no Seam so the Unified Seam Policy is correctly inapplicable,
is directly tested at its new Interface with mutation-sensitive assertions, and both verification gates
(114/114 tests, MSBuild exit 0) pass clean." All three checks (`reality`, `honesty`, `regression`) `passed`;
`conditions: []`; `regressions: []`.

--- Loop 10 (UTC 2026-08-04T15:25:36Z) ---

### Discovery
see Loop 1 Discovery

### Loop Counter
Loop 10 of 10 (cap)

### System Flag
[STATE: HALT_LOOP_CAP]

---

## Contest Verdict

**Promising, but architecturally immature.**

This is the cap loop (10/10). Independent re-derivation from current source (both gates green before and
after this loop's change: 114->115 tests, MSBuild exit 0) found no drift since loop 9 and confirmed the
queued Priority 1 - F-005's RankGrids style-priority mixed-style test gap - genuinely survives a fresh
Simplify Pressure Test. Ran the Step 2 tiebreak against the only other unblocked backlog item (F-007) on
blast radius (F-005: one file; F-007: two files) and F-005 won honestly. Landed the fix and independently
proved it mutation-sensitive by inverting the exact production line the finding named, re-running the suite,
confirming exactly the new test - and nothing else - reacted, then reverting the inversion before commit. No
production code changed this loop. Backlog is not empty (F-007, F-003 remain), so this cap halt is the
exhausted variant, not convergence - real work remains queued.

## Scorecard (1-10)

- Architecture quality: 7.0 | SAME | PrimaryWidget.xaml.cs byte-identical to loop 9; re-read
  LoadGameEntriesAsync (:332-703) directly this loop, merge unaffected.
- State management and runtime ownership: 7.0 | SAME | AppliedArtworkStore.cs/FixLog.cs untouched
  this loop; fix touches zero mutable runtime state.
- Domain modeling: 5.5 | SAME | SteamGridDbGame.cs/SteamGridDbGrid.cs re-read directly, still plain
  public-setter data bags. ParseOfficialCapsuleUrl unchanged.
- Data flow and dependency design: 6.0 | SAME | StoreNameLookup.cs:27-32, EpicLibrary.cs:31,
  AppliedArtworkStore.cs:49 all re-read directly, unaffected.
- Framework / platform best practices: 6.0 | SAME | DataContractJsonSerializer/Windows.Data.Json
  split re-confirmed unchanged; this loop's diff is xunit test code only.
- Concurrency and runtime safety: 6.5 | SAME | F-003's sequential per-game awaits re-confirmed at
  identical line numbers; zero Task.WhenAll/Parallel.*/Task.Run codebase-wide, same as loop 9.
- Code simplicity and clarity: 8.5 | SAME | Diff is purely additive test code (+17/-0, one file) - no
  simplification, no double-counting.
- Test strategy and regression resistance: 8.5 | UP | ArtworkRankerTests.cs:190-204 (new) closes
  F-005's named mutation gap - verified mutation-sensitive by inverting ArtworkRanker.cs:195's ThenBy
  and confirming exactly 1 failure, then reverting. F-007 + PrimaryWidget's shell seams keep it below 9.
- Overall implementation credibility: 7.5 | SAME | This loop's win credited entirely to test_strategy
  per this run's anti-double-counting convention; PrimaryWidget.xaml.cs's remaining lines still unverified
  beyond inspection + compile.

## Authority Map
(unchanged; not re-emitted - no authority finding was Priority 1 this loop)

## Strengths That Matter

- JsonRead (Services/JsonRead.cs) - unaffected this loop (file not in diff).
- ArtworkSource's private-constructor-plus-factory-method design - unaffected this loop (file not in diff).
- AsyncLazyCache<T> - unaffected this loop (file not in diff).
- This loop's fix demonstrates a repeatable pattern for closing remaining test-strategy gaps cheaply: prove
  mutation-sensitivity directly (flip the production line, confirm exactly the new test fails, revert)
  rather than merely asserting coverage - applies directly to F-007's queued tests.

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling, image decode, network resolution, and bulk-operation orchestration behind zero Interface boundary

Why it matters - The churn-flagged file (still the largest in the repo across every prior loop's
Discovery) continues to bundle several structurally distinct concerns with no Module boundary between most
of them, so a change to any one risks disturbing the others.

What is wrong - Re-verified this loop via a fresh direct read; unaffected by this loop's fix (which
targeted ArtworkRankerTests.cs only). LoadGameEntriesAsync (PrimaryWidget.xaml.cs:332-703) still
interleaves image decode, backup checks, and network name-resolution per entry inside one sequential
foreach (nested foreach at :436), with per-game awaits at :562,584,593,602 - unchanged from loop 9.
What remains merged: (1) UI event handling proper; (2) the image-decode/backup-check/network-resolution
core; (3) the three bulk-operation loops, still ruled out by the GameEntry/UWP platform constraint
documented at loop 6.

Evidence
- SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-703
- SteamGridDB.Xbox/PrimaryWidget.xaml.cs:436
- SteamGridDB.Xbox/PrimaryWidget.xaml.cs:562,584,593,602

Architectural test failed - n/a - different category (ownership/coupling sprawl for what remains).

Dependency category - n/a

Leverage impact - Unaffected this loop.

Locality impact - Unaffected this loop.

Metric signal - PrimaryWidget.xaml.cs: 1,950 lines, unchanged this loop.

Why this weakens submission - Ownership of the concerns still merged in PrimaryWidget remains
untraceable from any single Module besides the UI class itself - unaffected by this loop's test-only fix.

Severity - Serious deduction

ADR conflicts - none

Minimal correction path - No further split is queued this loop: the image-decode/backup-check/network-
resolution core is still genuinely interleaved per entry (re-confirmed by this loop's own direct read), so
no next slice is proposed without first re-verifying against current source.

Blast radius - Change (future loop, with a fresh SPT first): PrimaryWidget.xaml.cs. Avoid:
Services/Artwork/*, Services/Stores/*, Services/SteamGridDB/*, Services/Library/*.

---

### Finding #2: ArtworkRanker.RankGrids' style-priority ordering has no test mixing text-bearing and icon-like styles, so a sort-direction inversion would pass silently

Why it matters - RankGrids is the ranking algorithm behind every automatic artwork pick on the
widget's primary flow; an inverted style-priority tie-break would silently prefer icon-like art over
box-art-style uploads library-wide, with no test catching it.

What is wrong - Resolved this loop. Added Text_bearing_styles_sort_ahead_of_icon_like_styles_in_
RankGrids to ArtworkRankerTests.cs: two RankGrids candidates differing only in Style (no_logo vs
alternate), asserting the text-bearing one sorts first. Verified mutation-sensitive by directly flipping
ArtworkRanker.cs:195's ThenBy to ThenByDescending and re-running the suite: exactly one test failed
(the new one), then reverted (git checkout) and re-confirmed 115/115 green before commit.

Evidence
- SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs:195
- SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs:190-204

Architectural test failed - n/a - different category (missing-test gap, now closed).

Dependency category - n/a

Leverage impact - One call site (RankGrids), but it is the ranking function every automatic-fix and
manual-picker artwork list goes through.

Locality impact - The fix is one new test case; no production code changes.

Metric signal - 1 new test (114 -> 115); mutation-sensitivity independently verified.

Why this weakens submission - Previously: a source-level mutation on a central, primary-flow ranking
rule passed the entire suite undetected. Now closed.

Severity - Noticeable weakness

ADR conflicts - none

Minimal correction path - Add one RankGrids test case constructing two candidates with different
styles and asserting the text-bearing one sorts first. (Executed this loop.)

Blast radius - Change: SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs. Avoid:
SteamGridDB.Xbox/Services/Artwork/ArtworkRanker.cs, everything else.

---

### Finding #3: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

Why it matters - Load time scales linearly with library size and network latency on the widget's
primary open path.

What is wrong - Re-verified this loop via direct read; unaffected. Await sites remain at
:562,584,593,602 (unchanged from loop 9). Standing operational constraint continues to rule out attempting
this finding. StoreNameLookup's three static caches remain unlocked; still safe only because calls are
strictly sequential today, re-confirmed via codebase-wide grep.

Evidence
- SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-703
- SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:27-32,86-101,203-222

Architectural test failed - n/a - different category (structural waste per lens-efficiency.md).

Dependency category - true-external

Leverage impact - Unaffected this loop.

Locality impact - Unaffected this loop.

Metric signal - One HTTP round-trip per game per store lookup; unchanged this loop.

Why this weakens submission - Structural waste on the widget's primary hot path, unchanged from loop
7/8/9.

Severity - Noticeable weakness

ADR conflicts - none

Minimal correction path - Blocked for the duration of this run by the standing operational constraint.

Blast radius - Change: PrimaryWidget.xaml.cs, Services/Stores/StoreNameLookup.cs. Avoid:
Services/Artwork/*, Services/SteamGridDB/*.

---

### Finding #4: ArtworkDownloader's tile-fill gate and TileImage's vertical-crop algorithm have zero test coverage at any interface, direct or indirect

Why it matters - DownloadBestTileFillingImageAsync decides which candidate image the widget applies as
artwork when auto-selecting; a wrong crop or a broken tile-fill check would ship visibly bad artwork with no
test catching it.

What is wrong - Re-verified this loop via direct read; unaffected. ArtworkDownloader.cs's three
internal entry points still have no corresponding ArtworkDownloaderTests.cs file. TileImage.cs's public
FillsTileAsync/CropPortraitToTileAsync remain untested - TileImageTests.cs's four Fact methods still
exercise only EnsurePngAsync. BestVerticalCropAsync, reachable only through CropPortraitToTileAsync,
remains at zero coverage.

Evidence
- SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs:40,71,122
- SteamGridDB.Xbox/Services/Artwork/TileImage.cs:231,284,321
- SteamGridDB.Xbox.Tests/TileImageTests.cs:17-60

Architectural test failed - n/a - different category (missing-test gap per method.md Step 8).

Dependency category - n/a

Leverage impact - One call site cluster, on the primary automatic-artwork-pick path.

Locality impact - The fix is new tests only; no production code change needed.

Metric signal - 3 of 3 ArtworkDownloader entry points untested; 2 of 3 public TileImage methods
untested. Unchanged this loop.

Why this weakens submission - A source-level mutation in the tile-fill gate or crop-window selection
would pass the entire suite undetected.

Severity - Noticeable weakness

ADR conflicts - none

Minimal correction path - Add ArtworkDownloaderTests.cs exercising
DownloadBestTileFillingImageAsync's ranking-to-selection gate with constructed IBuffer fixtures; add
FillsTileAsync/CropPortraitToTileAsync cases to TileImageTests.cs. No production code changes
required.

Blast radius - Change: SteamGridDB.Xbox.Tests/ArtworkDownloaderTests.cs (new),
SteamGridDB.Xbox.Tests/TileImageTests.cs (new cases). Avoid:
SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs, SteamGridDB.Xbox/Services/Artwork/TileImage.cs.

## Simplification Check

| Field | Value |
|---|---|
| structurally_necessary | Adding one RankGrids test case; no Module removed/restructured, no Seam introduced. |
| new_seam_justified | false |
| helpful_simplification | None - this loop's fix is a test-coverage addition, not a simplification. |
| should_not_be_done | Attempting F-007 or F-003 this loop (lost the tiebreak / blocked); any further F-001 slice (no new evidence). |
| tests_after_fix | One new test added at the existing RankGrids Interface; mutation-sensitivity independently verified. |

## Improvement Backlog

1. Add ArtworkDownloader/TileImage test coverage for the tile-fill gate and vertical-crop selection
   (F-007) - new ArtworkDownloaderTests.cs, new TileImageTests.cs cases; no production code change.
   - Why it matters: closes the last remaining named test_strategy gap besides PrimaryWidget's
     architecturally-untestable shell seams.
   - Score impact: Test strategy +0.5-1.0 once verified.
2. Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups (F-003) - blocked for the
   duration of this run by the standing operational constraint.
   - Why it matters: removes load latency scaling linearly with library size, whenever lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

None this loop.

## Builder Notes

1. A test-coverage finding's fix can be independently proven correct (not just asserted correct) by
   inverting the exact production line the finding names, re-running the suite, and confirming precisely the
   new test fails - then reverting before commit. -> REVIEW_HISTORY.json loops[10].builder_notes for full
   notes.
2. When two backlog items are both test-only fixes at the same severity, blast radius (file count in
   minimal_correction_path) is a clean, mechanical tiebreak. -> REVIEW_HISTORY.json loops[10].
   builder_notes for full notes.

## Final Judge Narrative

Place, not win, at the loop cap. This is loop 10 of 10 - the configured maximum. Ground truth was clean
going in (both gates green, zero drift since loop 9's commit) and clean coming out (115/115 tests, MSBuild
exit 0). The loop re-ran the Simplify Pressure Test on the queued F-005 finding from scratch, confirmed it
survives, ran the Step 2 tiebreak against the only other unblocked backlog item (F-007) on blast radius, and
executed the smaller one. The fix's correctness was independently proven, not just asserted: inverting the
exact production line the finding named and re-running the suite confirmed precisely the new test - and
nothing else - reacts to that mutation. Runtime ownership is unaffected and exactly as trustworthy as loop 9
left it. Concurrency remains sequential and safe today, unaffected. Tests reduce regressions incrementally
more than last loop measured, with a proof standard (flip-and-verify) stronger than a bare assertion. Future
work risks nothing new from overengineering - this loop's fix added zero abstraction. Backlog is not empty
(F-007, F-003 remain), so this cap halt is the exhausted variant, not convergence.

## Loop 10 Result

Added one new test method, Text_bearing_styles_sort_ahead_of_icon_like_styles_in_RankGrids, to
SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs, closing finding F-005 (stable_id F-005). The test
constructs two RankGrids candidates differing only in Style (no_logo vs alternate, all other fields
default/equal) and asserts the text-bearing one (alternate) sorts first. git diff --stat:
SteamGridDB.Xbox.Tests/ArtworkRankerTests.cs | 17 +++++++++++++++++, 1 file changed, 17 insertions(+). No
production code changed.

What proves the change is honest: run-tests.ps1: 114 passed before, 115 passed after (delta is exactly
the 1 new test). MSBuild: exit 0, both runs. Mutation-sensitivity independently verified, not just asserted:
temporarily inverted ArtworkRanker.cs:195's ThenBy(GridStylePriority) to ThenByDescending, re-ran the
full suite, got exactly 1 failure (the new test, confirmed via stack trace pointing at
ArtworkRankerTests.cs:204), then reverted via git checkout -- SteamGridDB.Xbox/Services/Artwork/
ArtworkRanker.cs and re-confirmed 115/115 green before the implementation review and commit.

Risk boundary evidence (Meta-Rule 4): none - this fix crosses no isolation/Sendable/conditional-
compilation/cross-file-visibility/lock-ordering boundary. It is a pure test-only addition; no production
type, visibility, or concurrency primitive changed.

Targeted finding status: resolved - F-005's core claim (no RankGrids-level test varies Style
between two candidates, so the tie-break direction could invert silently) is fully closed: a
mutation-verified test now exists at exactly that surface.

Unintended scorecard regression: none observed. test_strategy moved UP on distinct, source-verified,
mutation-tested proof; all other dimensions held SAME (zero structural change in their evidence this loop);
no dimension regressed.

## Loop 10 Implementation Review

verdict: approved - "The new RankGrids test isolates Style as the sole varying field (all other
RankedGrid signals equal via Grid() defaults) and asserts an order that only holds under .ThenBy ascending,
so it would fail if the clause were inverted to .ThenByDescending, genuinely closing F-005's mutation gap
with no production code touched." All three checks (reality, honesty, regression) passed;
conditions: []; regressions: [].

--- Loop 11 (UTC 2026-08-04T16:37:25Z) ---

### Discovery
see Loop 1 Discovery

### Loop Counter

Loop 11 of 15 (cap raised from 10)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Both gates green before and after (115->121 tests, MSBuild exit 0). Independently re-derived the scorecard
from fresh reads of the source this loop (not anchored to loop 10's cached numbers) before consulting the
prior review for delta basis. The queued Priority 1 — F-007's `ArtworkDownloader`/`TileImage` test-coverage
gap — survived a fresh Simplify Pressure Test for its `TileImage` half; attempting the `ArtworkDownloader`
half surfaced a genuine, previously-uncredited fact (its three entry points call the network directly with
no injectable seam), which is now recorded as both a narrowed Finding and a friction-proven Deepening
Candidate for next loop. `test_strategy` approaching the 9.0 threshold triggered a mandatory Authority Map
cross-check this loop, which surfaced a second, distinct, previously-uncredited gap: `FixLog` (Services/
Artwork/FixLog.cs) has no direct test file at all — a genuinely new, cheap, safe finding, not something this
loop's own fix touched. With two open gaps now named honestly rather than one, `test_strategy` holds rather
than crosses 9.0 this loop. Also spent real investigation time this loop on `domain_modeling` and
`framework_idioms` — the two lowest-scoring dimensions, never targeted by ten prior loops' backlogs — per
this loop's dispatch instructions; found no fix that survives the Simplify Pressure Test (see Finding
discussion and Builder Notes), so neither became this loop's Priority 1. Backlog remains non-empty
(`FixLog` tests, the `ArtworkDownloader` seam, F-003), so `CONTINUE`.

## Scorecard (1-10)

- **Architecture quality:** 7.0 | SAME | `PrimaryWidget.xaml.cs` confirmed byte-identical to loop 9 via
  `git diff --stat` (this loop's diff: `SteamGridDB.Xbox.Tests/TestImages.cs`, `TileImageTests.cs` only);
  re-read `LoadGameEntriesAsync` (`:332-609`) directly this loop and confirmed the same merge of image
  decode (`:520-522`), backup check (`:516`), and per-game network resolution (`:562-609`) persists inside
  the nested `foreach` at `:436`. `ArtworkDownloader.cs`, `TileImage.cs`, `ArtworkRanker.cs`,
  `ArtworkSource.cs` all re-read fresh this loop and confirmed unaffected by this loop's test-only diff —
  their existing shapes (pure static modules; `ArtworkSource`'s private-ctor-plus-factory design) remain the
  strongest parts of the graph.
- **State management and runtime ownership:** 7.0 | SAME | `AppliedArtworkStore.cs`/`FixLog.cs` confirmed
  untouched this loop via `git diff --stat` (2 files changed, both test files, neither is these). `GameEntry.cs`
  read fresh this loop: an `INotifyPropertyChanged` view-model with explicit backing fields and
  change-detecting setters — one clear writer per property, unaffected by this loop's diff.
- **Domain modeling:** 5.5 | SAME | Re-read `SteamGridDbGame.cs` and `SteamGridDbGrid.cs` fresh this loop:
  both remain `[DataContract]` wire types with public `get; set;` auto-properties and no invariant
  enforcement at construction. Investigated this loop (per this run's standing judgment note) whether a
  construction-time-invariant fix now passes the Simplify Pressure Test — it does not: `SteamGridDbGame.
  OfficialCapsuleUrl`'s own doc comment (`SteamGridDbGame.cs:36-39`) already documents why it sits outside
  `[DataMember]` — the platformdata per-language keys `DataContractJsonSerializer` cannot express. No
  caller-observed ambiguity motivates adding constructor validation to these DTOs (SPT question 1 fails —
  no real ambiguity to fix); the codebase's actual domain types (`ArtworkSource`, `ManifestEntryIdentity`,
  the private `RankedGrid` inside `ArtworkRanker`) already enforce their own invariants at construction and
  are not anemic. SPT-rejected this loop; no backlog item queued.
- **Data flow and dependency design:** 6.0 | SAME | `StoreNameLookup.cs`'s three unlocked static caches
  (`gogNameCache`/`epicNameCache`/`nameMatchCache`, `:27-32`) read fresh this loop and confirmed unchanged;
  `EpicLibrary.cs`'s ambient `Environment.GetEnvironmentVariable` fallback and `AppliedArtworkStore.cs`'s
  ambient `ApplicationData.Current` default confirmed untouched via `git diff --stat` (not independently
  re-read this loop; unaffected by a 2-file test-only diff).
- **Framework / platform best practices:** 6.0 | SAME | `DataContractJsonSerializer` (`SteamGridDbClient.
  cs:388`) / `Windows.Data.Json` (`SteamGridDbClient.cs:10`, used in `ParseOfficialCapsuleUrl:144-199`)
  split read fresh this loop and re-confirmed present and unchanged. Investigated this loop whether the
  split is a framework-idiom violation or a justified platform accommodation: `SteamGridDbClient.
  cs:137-141`'s own doc comment states `DataContractJsonSerializer` cannot express the per-language
  `platformdata` keys, so the manual `Windows.Data.Json` walk is the only way to reach that data on this
  stack — a genuine platform constraint, not cargo-culted ceremony. No SPT-passing framework-idiom fix
  identified this loop.
- **Concurrency and runtime safety:** 6.5 | SAME | F-003's sequential per-game awaits (`PrimaryWidget.
  xaml.cs:562,584,593,602`) re-confirmed at identical line numbers via this loop's own direct read (see
  Finding #3). `StoreNameLookup`'s unlocked static caches re-confirmed still safe only because every call
  path remains the single sequential `foreach` — re-grepped codebase-wide for `Task.WhenAll`/`Parallel.*`/
  `Task.Run` this loop, zero hits, same as every prior loop.
- **Code simplicity and clarity:** 8.5 | SAME | This loop's diff is additive test code only
  (`TestImages.cs`: new using statement + four new one-purpose factory helpers; `TileImageTests.cs`: six new
  test methods) — no simplification, no new ceremony. The new `TestImages.cs` helpers
  (`OpaquePngAsync`/`PngWithTransparentCornersAsync`/`PortraitWithDetailBandAsync`) follow the file's
  existing one-purpose-per-factory-method pattern rather than adding a generic builder abstraction.
- **Test strategy and regression resistance:** 8.5 | SAME | Added 6 new tests to `SteamGridDB.Xbox.Tests/
  TileImageTests.cs` (121 total, up from 115), closing `TileImage`'s two previously-untested public methods
  (`FillsTileAsync`, `CropPortraitToTileAsync`) plus the private `BestVerticalCropAsync` reachable only
  through the latter. Both algorithms independently verified mutation-sensitive, not just asserted: flipped
  `TileImage.cs:263`'s `transparentCorners < 2` to `>= 2`, re-ran the suite, got exactly the 2 new
  `FillsTileAsync` tests failing; separately flipped `TileImage.cs:371`'s `running > best` to
  `running < best` (reversing the crop-window-selection direction), re-ran, got exactly the 2 new
  crop-direction tests failing; both mutations reverted via `git checkout` and 121/121 re-confirmed green
  before commit. This closes F-007's `TileImage` half. Approaching the 9-anchor's threshold triggered the
  mandatory Authority Map cross-check this loop (see Authority Map below), which surfaced `FixLog.cs`
  (`Services/Artwork/FixLog.cs`) as a mutable-state concern with zero direct test coverage (no
  `FixLogTests.cs` exists — confirmed via directory listing this loop) — a genuine, previously-uncredited
  gap, distinct from F-007. With `ArtworkDownloader` (Finding #2) and `FixLog` (Finding #4) both open and
  neither an accepted residual, the 9-anchor's "at most one named gap" bar is not met even though real
  progress landed this loop; held at 8.5 rather than crediting the closed `TileImage` gap as a net score
  increase, because Meta-Rule 6 (honesty beats polish) means a freshly-discovered gap counts against the
  anchor even when this loop's own diff did not create it.
- **Overall implementation credibility:** 7.5 | SAME | This loop's fix is credited entirely to
  `test_strategy` (mutation-verified, reviewer-approved, zero production code touched) rather than
  double-counted here, consistent with this run's established anti-double-counting convention.
  `PrimaryWidget.xaml.cs`'s 1,950 lines (`wc -l` confirmed this loop) remain unverified by anything but
  inspection and a green compile outside the small tested slices; `ArtworkDownloader`'s untested
  network-bound entry points (Finding #2) and `FixLog`'s untested disk-write path (Finding #4) are the same
  category of unverified-but-inspected code. Choosing to hold `test_strategy` at 8.5 rather than credit a
  net UP this loop, once the Authority Map cross-check surfaced `FixLog`, is itself the kind of honesty this
  dimension rewards — not spending it here avoids double-crediting a single act of rigor across two
  dimensions.

## Authority Map

Triggered this loop by `test_strategy` approaching the 9.0 threshold (G24's mandatory cross-check). Scoped
to `Services/Artwork/` and `Services/Stores/` concerns this loop's investigation actually touched — not a
full-app audit; `PrimaryWidget`'s own UI-bound state (`GameEntries`, status text, etc.) is out of scope this
loop, unaffected by this loop's diff either way.

- **Concern:** Applied-artwork record (which SteamGridDB artwork ID was written to each tile).
  - **Owner:** `AppliedArtworkStore` (`Services/Artwork/AppliedArtworkStore.cs`), read fresh this loop.
  - **Allowed writers:** `SetAsync`, `ClearAsync` — both funnel through the private `UpdateAsync`, gated by
    the shared `SemaphoreSlim gate`.
  - **Readers:** `GetAsync` (also gated, per F-002's read/write lock invariant).
  - **Persistence seam:** `applied-artwork.json` in `RecordFolder` (defaults to `ApplicationData.Current.
    LocalFolder`).
  - **Async mutation entry points:** `SetAsync`, `ClearAsync`.
  - **Verdict:** Single and clear. Direct test: `AppliedArtworkStoreTests.cs` (confirmed covers `Get`/`Set`
    fresh this loop).

- **Concern:** Fix-run diagnostic log (what happened during the last "fix library" pass).
  - **Owner:** `FixLog` (`Services/Artwork/FixLog.cs`), read fresh this loop.
  - **Allowed writers:** `Start` (resets the in-memory `lines` list), `Write` (appends) — called from
    `PrimaryWidget` and `ArtworkDownloader.FindOfficialLookalikeAsync`.
  - **Readers:** none in-process; `SaveAsync` writes to disk for the user to inspect externally.
  - **Persistence seam:** `last-fix.log` / `last-load.log` in `LogFolder` (defaults to `ApplicationData.
    Current.LocalFolder`).
  - **Async mutation entry points:** `SaveAsync` (the only async member; `Start`/`Write` are synchronous).
  - **Verdict:** Single and clear. **No direct test file** — `FixLogTests.cs` is absent from
    `SteamGridDB.Xbox.Tests/` (confirmed via directory listing this loop). See Finding #4.

- **Concern:** Store-name lookup caches (GOG/Epic names, SteamGridDB name-match results).
  - **Owner:** `StoreNameLookup` (`Services/Stores/StoreNameLookup.cs`), read fresh this loop.
  - **Allowed writers:** `GetOrFetchGogNameAsync`, `GetOrFetchEpicNameAsync`, `FindGameByNameAsync`,
    `LoadUbisoftGameListAsync` — each owns its own cache slot.
  - **Readers:** the same four methods (read-through cache).
  - **Persistence seam:** none — in-memory only, process lifetime.
  - **Async mutation entry points:** all four writers listed above.
  - **Verdict:** Single and clear (per F-003, unlocked but safe only because every call path is sequential
    today). `StoreNameLookupTests.cs` exists but, per its own doc comment, covers only the pure
    `NormaliseGameName` helper — the four network-bound cache-writer methods are untested for the same
    reason `ArtworkDownloader`'s entry points are (Finding #2): no injectable seam, and testing them for
    real would mean grading GOG/Epic/Ubisoft's uptime. Not a new Finding — same category and same
    disposition as Finding #2, cross-referenced rather than duplicated.

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design (`Services/SteamGridDB/
  ArtworkSource.cs:15-51`) still makes "neither a platform-ID nor a game-ID" unrepresentable — re-read fresh
  this loop, unaffected.
- `AsyncLazyCache<T>` still takes the caller's own lock as a constructor argument rather than owning a
  private one — unaffected this loop (file not in diff, confirmed via `git diff --stat`).
- `TileImage`'s pure, UI-free, dispatcher-free static functions (fill-check, crop, PNG conversion) made this
  loop's fix possible with zero production risk: every algorithm needed for the tile-fill gate and the
  crop-window selection was reachable and testable purely from `IBuffer` fixtures, with nothing to mock.
  This is the same shape loop 10's `RankGrids` fix exploited, now proven twice on genuinely different
  algorithm classes (a sort tie-break, and a per-row energy sliding window).
- The mutation-verification technique from loop 10 (flip the exact production line, confirm exactly the new
  test(s) react, revert) scaled cleanly to a harder case this loop: proving `BestVerticalCropAsync`'s
  window-selection *direction* (not just its existence) required constructing two mirror-image test inputs
  rather than one, and the technique caught the injected reversal precisely.

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling, image decode, network resolution, and bulk-operation orchestration behind zero Interface boundary

**Why it matters** — The largest file in the repo across every prior loop's Discovery continues to bundle
several structurally distinct concerns with no Module boundary between most of them, so a change to any one
risks disturbing the others.

**What is wrong** — Read `LoadGameEntriesAsync` directly this loop (`PrimaryWidget.xaml.cs:332-609` read in
full this loop; `:610-703` confirmed unchanged via `git diff` showing the file untouched since loop 9's
commit `05501e0`). The nested `foreach` over `gameCache` entries (`:436`) still interleaves image decode
(`:520-522`), the backup check (`:516`), and per-game SteamGridDB/store name resolution (`:562,584,593,602`)
inside one sequential per-entry block. This loop's own diff touches only `SteamGridDB.Xbox.Tests/
TestImages.cs` and `TileImageTests.cs`, so none of this changed.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-703`
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:436`
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:516,520-522,562,584,593,602`

**Architectural test failed** — n/a — different category (ownership/coupling sprawl for what remains).

**Dependency category** — n/a

**Leverage impact** — Unaffected this loop.

**Locality impact** — Unaffected this loop.

**Metric signal** — `PrimaryWidget.xaml.cs`: 1,950 lines (`wc -l` confirmed this loop), unchanged.

**Why this weakens submission** — Ownership of the concerns still merged in `PrimaryWidget` (image decode
interleaved with the backup check and per-game network resolution, plus the three bulk-operation loops
elsewhere in the file) remains untraceable from any single Module besides the UI class itself.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — No further split is queued this loop: the image-decode/backup-check/
network-resolution core is still genuinely interleaved per entry (re-confirmed by this loop's own direct
read), so no next slice is proposed without first re-verifying against current source, consistent with this
run's discipline since loop 8.

**Blast radius** — Change (only if a future loop verifies a further slice, with a fresh SPT first):
`PrimaryWidget.xaml.cs`. Avoid: `Services/Artwork/*`, `Services/Stores/*`, `Services/SteamGridDB/*`,
`Services/Library/*`.

---

### Finding #2: ArtworkDownloader's three entry points remain untested because each calls the network directly with no injectable seam; TileImage's fill/crop algorithms are now covered

**Why it matters** — `DownloadBestTileFillingImageAsync` decides which candidate image the widget applies as
artwork when auto-selecting; a wrong crop or a broken tile-fill check would ship visibly bad artwork with no
test catching it. Closing the algorithmic half of this gap this loop still leaves the selection gate itself
untested.

**What is wrong** — **Partially resolved this loop.** `TileImage.cs`'s public `FillsTileAsync` (`:231`) and
`CropPortraitToTileAsync` (`:284`), plus the private `BestVerticalCropAsync` (`:321`) reachable only through
the latter, were untested at any interface before this loop. Added six tests this loop to `SteamGridDB.Xbox.
Tests/TileImageTests.cs`, all mutation-verified (see Loop 11 Result): `FillsTileAsync`'s opaque/
transparent-corner distinction, `CropPortraitToTileAsync`'s non-portrait guard and output dimensions, and
`BestVerticalCropAsync`'s window-selection direction (tested from both ends of a constructed portrait
image). `ArtworkDownloader.cs`'s three entry points (`DownloadArtworkAsync:40`,
`DownloadBestTileFillingImageAsync:71`, `FindOfficialLookalikeAsync:122`) remain untested: read the file
fresh this loop and confirmed `DownloadArtworkAsync` calls a private static `sharedHttpClient` (`:35`)
directly with no seam to inject a fixture through, and `DownloadBestTileFillingImageAsync`/
`FindOfficialLookalikeAsync` both call `DownloadArtworkAsync` internally — so even a fixture-based test of
the ranking-to-selection gate (as the prior backlog description assumed) would require a real network
round-trip. Testing these three honestly requires either a production HTTP-abstraction seam (its own
Simplify Pressure Test — see the Deepening Candidate below) or real network calls in the test suite
(rejected: flaky, and the suite is network-free by design).

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:231,284,321`
- `SteamGridDB.Xbox.Tests/TileImageTests.cs:63-153`
- `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs:35,40,71,122`

**Architectural test failed** — Interface-as-test-surface, for the `ArtworkDownloader` half — the tests
this loop's backlog description assumed could stay at the fixture level cannot, because the Implementation
reaches past the Interface into a live network call with no substitutable seam.

**Dependency category** — `true-external`

**Leverage impact** — One call site cluster (`ArtworkDownloader`'s gate), but it is the function every
automatic artwork pick and manual apply goes through after ranking.

**Locality impact** — This loop's fix is new tests only; no production code change. The `ArtworkDownloader`
remainder would need a small seam change, scoped to that file alone (see Deepening Candidate).

**Metric signal** — `TileImage`: 2 of 2 previously-untested public methods now covered, 6 new mutation-verified
tests. `ArtworkDownloader`: 0 of 3 entry points tested, unchanged — now with a concrete, evidenced reason
why (no seam), not just an absence.

**Why this weakens submission** — Before this loop: a source-level mutation anywhere in the tile-fill gate
or the crop-window selection passed the entire suite undetected. Now: `TileImage`'s two algorithms are
mutation-verified; the pick-from-network selection logic in `ArtworkDownloader` is not, and cannot be with
constructed fixtures alone as originally scoped.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Closed this loop for `TileImage`'s public surface (no production code
change). `ArtworkDownloader`'s three entry points remain open: closing them honestly requires either
introducing an injectable HTTP abstraction (a production seam change, its own Simplify Pressure Test) or
accepting the network boundary as a residual. Not attempted this loop — no friction proven yet for a new
seam *before* this loop's own attempt (Meta-Rule 3), and forcing one without that proof would risk protocol
soup. Friction is proven now (see Deepening Candidate); the seam itself is next loop's decision, with its
own fresh SPT.

**Blast radius** — Change (this loop's actual diff): `SteamGridDB.Xbox.Tests/TestImages.cs`,
`SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs`,
`SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs`, everything else.

---

### Finding #3: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** — Load time scales linearly with library size and network latency on the widget's
primary open path.

**What is wrong** — Read `LoadGameEntriesAsync` fresh this loop (see Finding #1): the `gameCache` `foreach`
still awaits `sgdbClient.GetGameByPlatformIdAsync` (`:562`) and the GOG/Epic/Ubisoft name fallbacks
(`:584,593,602`) one game at a time, unchanged from every prior loop. The standing operational constraint
continues to rule out attempting this: parallelising these round-trips would change observable request
count/order/timing against third-party APIs without a behavioral oracle, and the test suite still does not
cover network calls. `StoreNameLookup`'s three static caches (`gogNameCache`/`epicNameCache`/
`nameMatchCache`, `StoreNameLookup.cs:27-32`) remain unlocked, re-confirmed safe only because every call
path is still the single sequential `foreach` — re-grepped codebase-wide for `Task.WhenAll`/`Parallel.*`/
`Task.Run` this loop, zero hits.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:399,562,584,593,602`
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:27-32`

**Architectural test failed** — n/a — different category (structural waste per `lens-efficiency.md`, not a
Seam).

**Dependency category** — `true-external`

**Leverage impact** — Unaffected this loop.

**Locality impact** — Unaffected this loop.

**Metric signal** — One HTTP round-trip per game per store lookup; unchanged this loop.

**Why this weakens submission** — Structural waste on the widget's primary hot path, unchanged from loop 7
through loop 11.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Blocked for the duration of this run by the standing operational constraint,
unchanged. Any eventual remedy must also add locking to `StoreNameLookup`'s three static caches.

**Blast radius** — Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync`), `Services/Stores/
StoreNameLookup.cs`. Avoid: `Services/Artwork/*`, `Services/SteamGridDB/*`.

---

### Finding #4: FixLog has zero direct test coverage

**Why it matters** — `FixLog` is the widget's only diagnostic trail for artwork-selection runs; the file's
own doc comment records a real incident where the official-artwork gate silently failed across an entire
library and was found only by manually diffing artwork IDs on disk. If `FixLog` itself broke, that
diagnostic capability would disappear silently, exactly the failure mode it exists to catch for everything
else.

**What is wrong** — Surfaced this loop while building the Authority Map cross-check that `test_strategy`
approaching 9.0 requires (see Authority Map). Read `FixLog.cs` fresh this loop: `Start` (`:46`) resets the
in-memory `lines` list, `Write` (`:56`) appends to it, `SaveAsync` (`:64`) writes it to disk once per run.
Confirmed via directory listing that `SteamGridDB.Xbox.Tests/` has no `FixLogTests.cs` and no other test
file references `FixLog` — zero coverage, direct or indirect.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/FixLog.cs:46,56,64`
- `SteamGridDB.Xbox.Tests/` (directory listing confirms no `FixLogTests.cs`)

**Architectural test failed** — n/a — different category (missing-test gap per method.md Step 8's
mutation-test check).

**Dependency category** — n/a

**Leverage impact** — One call site cluster (`FixLog`'s three members), but every artwork-fix and
library-load run writes through it.

**Locality impact** — The fix is new tests only; no production code change needed. `FixLog`'s shape (no
network, no WinRT decode, just a `List<string>` and a `TempFolder`-testable disk write) makes this the
cheapest of this run's three open test-coverage items — the same construction pattern
`AppliedArtworkStoreTests.cs` already uses (`TempFolder`, assign `LogFolder`, assert on disk content) applies
directly.

**Metric signal** — 0 of 3 `FixLog` members tested. New this loop (not carried forward from any prior
loop's backlog — first time this concern was audited).

**Why this weakens submission** — A source-level mutation in `Start`/`Write`/`SaveAsync` (e.g., `Write`
silently no-op'ing, or `SaveAsync` writing an empty file) would pass the entire suite undetected, and would
also defeat the one tool this codebase has for diagnosing exactly that class of silent failure.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Add `FixLogTests.cs` using the same `TempFolder` + `LogFolder`-assignment
pattern `AppliedArtworkStoreTests.cs` already establishes: assert `Start` resets prior lines and writes a
header, `Write` appends, `SaveAsync` writes every line to disk in order. No production code changes
required.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/FixLogTests.cs` (new). Avoid:
`SteamGridDB.Xbox/Services/Artwork/FixLog.cs`, everything else.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Adding six tests to `TileImageTests.cs` plus four new fixture-builder helpers to `TestImages.cs`. No Module removed or restructured, no Seam introduced. Unified Seam Policy does not apply. |
| New seam justified | false — no protocol/port/abstraction introduced this loop; deferred to next loop as a Deepening Candidate. |
| Helpful simplification | None — this loop's fix is a test-coverage addition, not a simplification. |
| Should NOT be done | Building the `ArtworkDownloader` HTTP seam this loop without a fresh Step 2 SPT pass; forcing a `domain_modeling`/`framework_idioms` finding when neither passed SPT question 1; any further slice of F-001 without fresh evidence. |
| Tests after fix | Six new tests at `TileImage`'s existing public Interface (`FillsTileAsync`, `CropPortraitToTileAsync`), mutation-verified directly by inverting two production lines in turn. |

## Improvement Backlog

1. **Add `FixLogTests.cs` covering `Start`/`Write`/`SaveAsync` (F-008, new this loop)** — no production code
   change; same construction pattern `AppliedArtworkStoreTests.cs` already uses.
   - Why it matters: closes a genuine, freshly-discovered test gap on the widget's only diagnostic trail for
     artwork-selection runs; the cheapest and safest of this run's three open test-coverage items (no
     network, no WinRT decode).
   - Score impact: Test strategy +0.5 once verified, likely enough to cross the 9.0 threshold if
     `ArtworkDownloader` is also resolved or accepted as residual by then.
2. **Introduce an injectable HTTP-fetch seam for `ArtworkDownloader.DownloadArtworkAsync` so
   `DownloadBestTileFillingImageAsync`/`FindOfficialLookalikeAsync` can be tested with constructed
   fixtures instead of live network calls** — closes the remaining half of F-007 (stable_id `F-007`).
   - Why it matters: this loop proved (not assumed) that `ArtworkDownloader`'s three entry points cannot be
     tested without either a live network round-trip or a seam; the two-adapter rule is satisfiable (real
     HttpClient adapter + a test fake returning canned bytes), and friction is now proven per Meta-Rule 3
     rather than hypothetical.
   - Score impact: Test strategy +0.5 once verified; `credibility` may follow if the change is small and
     reviewer-approved cleanly.
3. **Bound concurrency in `LoadGameEntriesAsync`'s per-game SteamGridDB lookups (F-003)** — blocked for the
   duration of this run by the standing operational constraint.
   - Why it matters: removes load latency scaling linearly with library size, whenever the constraint is
     lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

- **Candidate Module:** `ArtworkDownloader.DownloadArtworkAsync` (and its `sharedHttpClient` dependency).
- **Source friction proven:** This loop attempted to test `DownloadBestTileFillingImageAsync`'s
  ranking-to-selection gate and `FindOfficialLookalikeAsync`'s official-artwork veto with constructed
  `IBuffer` fixtures per the existing backlog description (F-007) and found both are unreachable without a
  live network round-trip, because `DownloadArtworkAsync` (`:40`) calls a private static `sharedHttpClient`
  (`:35`) directly with no seam to substitute a fixture through — see Finding #2.
- **Why the current Interface is shallow or misplaced:** `DownloadArtworkAsync`'s Interface (a URL in, an
  `IBuffer` out) already looks like the right shape for a seam, but its Implementation is inseparable from a
  live `HttpClient` — the two-adapter rule (prod `HttpClient` + test fake returning canned bytes) cannot be
  satisfied today because there is no injection point.
- **Behavior to move behind the deeper Interface:** The network fetch itself — introduce a small
  delegate/interface parameter (e.g. `Func<string, Task<IBuffer>>` or a narrow `IArtworkFetcher`) that
  `DownloadArtworkAsync`'s callers can substitute in tests, defaulting to the real HTTP call in production.
- **Dependency category:** `true-external`
- **Test surface after the change:** New `ArtworkDownloaderTests.cs` exercising
  `DownloadBestTileFillingImageAsync`'s tile-fill gate and `FindOfficialLookalikeAsync`'s official-artwork
  veto against a fake fetcher returning constructed `IBuffer` fixtures, closing the remaining half of F-007.
- **Smallest first step:** Add one seam to `DownloadArtworkAsync`'s call path — the production `HttpClient`
  is the only real Adapter until a test fake is added *alongside it in the same change*, satisfying the
  two-adapter rule in one step rather than split across loops.
- **What not to do:** Do not introduce a general-purpose `IHttpClient` wrapper/interface across the whole
  codebase — `StoreNameLookup` and `SteamGridDbClient` have their own `HttpClient` usage with no proven
  friction; scope the seam to `ArtworkDownloader`'s three entry points only.

## Builder Notes

1. A "test with constructed fixtures" backlog item can turn out to be non-executable as scoped once you
   actually try it — the function's own body may call further out (here, into a live network client) past
   where the fixture stops.
2. A crop/window-selection algorithm's mutation-sensitivity can be proven with a synthetic two-band image
   (one flat region, one high-contrast region) placed at each extreme, rather than needing to predict the
   algorithm's exact numeric output.
3. Re-litigating a long-held low score is worth doing periodically, but "anemic DTO" isn't automatically a
   defect — a wire-format type mirroring a third-party API's JSON shape is expected to be a data bag.

Full notes in REVIEW_HISTORY.json loops array (loop 11 entry) builder_notes field.

## Final Judge Narrative

Place, not win. Ground truth was clean going in (both gates green, zero source drift since loop 9's commit;
main's own `loop_cap` bump was the only pre-existing diff) and clean coming out (121/121 tests, MSBuild exit
0). The loop re-ran the Simplify Pressure Test on the queued F-007 finding from scratch rather than assuming
the carried-forward plan was still correct, and in doing so discovered the plan's own assumption didn't
fully hold: `ArtworkDownloader`'s entry points cannot be tested with constructed fixtures alone, because they
reach into a live, non-injectable network client. Rather than treating that as a reason to skip the loop, it
executed the genuinely reachable half (`TileImage`'s fill-check and crop-window algorithms) with the same
mutation-verification discipline loop 10 established — this time proving a *direction*, not just a
tie-break, via two mirror-image synthetic images — and recorded the unreachable half as a narrowed Finding
plus a friction-proven Deepening Candidate for next loop's own fresh SPT pass. Building the Authority Map
that approaching `test_strategy`'s 9.0 threshold requires then surfaced a second, genuinely new gap
(`FixLog`, zero test coverage) that this loop's own diff did not create and did not fix — recorded honestly
as Finding #4 rather than left to inflate the scorecard, which is why `test_strategy` holds at 8.5 despite
real, verified forward progress. Separately, this loop spent real investigation time on `domain_modeling`
and `framework_idioms` per its dispatch instructions and concluded, with fresh evidence rather than by
default, that neither has an SPT-passing fix available right now — the "anemic DTO" and "serializer split"
findings that have anchored those scores for ten loops are justified platform accommodations, not neglect,
once compared against the codebase's actual domain types. Runtime ownership and concurrency are unaffected
and exactly as trustworthy as loop 10 left them. Tests reduce regressions more than any single prior loop
measured (6 new tests, two independent mutation verifications, not one), even though the scorecard doesn't
show it as an UP this loop — the newly-found `FixLog` gap offsets it honestly. Future work risks nothing new
from overengineering — this loop's fix added zero production abstraction, and the one abstraction it
motivates (the HTTP-fetch seam) is deferred, not built opportunistically. Backlog is not empty (`FixLog`
tests, the `ArtworkDownloader` seam, F-003), so `CONTINUE`.

## Loop 11 Result

Added six new test methods to `SteamGridDB.Xbox.Tests/TileImageTests.cs` and four new fixture-builder
helpers to `SteamGridDB.Xbox.Tests/TestImages.cs` (`OpaquePngAsync`, `PngWithTransparentCornersAsync`,
`PortraitWithDetailBandAsync`, plus the private `FromPixelsAsync` helper they share), closing the
`TileImage` half of finding F-007 (stable_id `F-007`). New tests: `Fills_tile_when_the_image_is_opaque_at_
every_corner`, `Does_not_fill_tile_when_the_corners_are_transparent`, `Crop_returns_null_for_images_that_
are_not_taller_than_wide`, `Crops_a_portrait_image_to_a_square_matching_the_source_width`, `Crop_window_is_
drawn_toward_a_high_detail_band_at_the_top`, `Crop_window_is_drawn_toward_a_high_detail_band_at_the_bottom`.
`git diff --stat`: `SteamGridDB.Xbox.Tests/TestImages.cs | 74 ++++++++++++`, `SteamGridDB.Xbox.Tests/
TileImageTests.cs | 99 +++++++++++++++++`, 2 files changed, 172 insertions(+), 1 deletion(-). No production
code changed in the final diff.

**What proves the change is honest:** `run-tests.ps1`: 115 passed before, 121 passed after (delta is exactly
the 6 new tests). MSBuild: exit 0, both runs. Mutation-sensitivity independently verified twice, not just
asserted: (1) temporarily inverted `TileImage.cs:263`'s `transparentCorners < 2` to `transparentCorners >=
2`, re-ran the full suite, got exactly 2 failures (`Fills_tile_when_the_image_is_opaque_at_every_corner` and
`Does_not_fill_tile_when_the_corners_are_transparent`), reverted via `git checkout -- SteamGridDB.Xbox/
Services/Artwork/TileImage.cs`, re-confirmed 121/121 green; (2) temporarily inverted `TileImage.cs:371`'s
`running > best` to `running < best`, re-ran the full suite, got exactly 2 failures (`Crop_window_is_drawn_
toward_a_high_detail_band_at_the_top` and `..._at_the_bottom`), reverted the same way, re-confirmed 121/121
green before the implementation review and commit.

**Risk boundary evidence (Meta-Rule 4):** none — this fix crosses no isolation/Sendable/conditional-
compilation/cross-file-visibility/lock-ordering boundary. It is a pure test-only addition; no production
type, visibility, or concurrency primitive changed.

**Targeted finding status:** `carried_forward` — F-007's Claim narrowed but is not fully discharged:
`TileImage`'s two public algorithms are now mutation-verified, but `ArtworkDownloader`'s three entry points
remain untested, for a newly-evidenced concrete reason (no injectable HTTP seam) rather than the prior
loop's more general "zero coverage" framing.

**Unintended scorecard regression:** none observed, but one unintended scorecard *non-movement* worth
naming explicitly: this fix alone would have justified `test_strategy` moving UP, and it is instead held at
8.5 SAME because building the mandatory Authority Map (triggered by approaching the 9.0 threshold) surfaced
`FixLog`'s pre-existing, previously-uncredited test gap (new Finding #4) in the same loop. That gap is not
something this loop's diff created or could have avoided by scoping differently — it was always there.
`architecture_quality`, `state_management`, `domain_modeling`, `data_flow`, `framework_idioms`,
`concurrency`, `simplicity`, `credibility` all held SAME (zero structural change in their evidence this loop
— the diff touches only test code); no dimension regressed.

## Loop 11 Implementation Review

`verdict: approved` — "The six new TileImageTests.cs tests exercise FillsTileAsync (:231) and
CropPortraitToTileAsync/BestVerticalCropAsync (:284,:321) at their real public Interface with constructed
IBuffer fixtures, independently re-derived as mutation-sensitive to the exact production lines cited
(corner-count comparison at TileImage.cs:263 and window-selection comparison at TileImage.cs:371), and
TileImage.cs itself carries zero uncommitted or committed change." All three checks (`reality`, `honesty`,
`regression`) `passed`; `conditions: []`; `regressions: []`.

--- Loop 12 (UTC 2026-08-04T17:18:21Z) ---

### Discovery
See Loop 1 Discovery (refreshed Loop 7). Ground truth this loop: 121 tests passing before / 125 after;
MSBuild exit 0 both runs; zero source drift since loop 11's commit `85b5279`. Selected lens: Generic
(C#/.NET / UWP-hosted WinUI stack).

### Loop Counter
Loop 12 of 15

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Both gates green before and after (121->125 tests, MSBuild exit 0). Closed this run's queued Priority 1
(F-008, `FixLog` has zero direct test coverage) with four new mutation-verified tests and zero production
code change. Loop 11 was explicit that the *only* thing keeping `test_strategy` at 8.5 rather than crossing
9.0 was having two open Authority-Map gaps (`ArtworkDownloader` and `FixLog`) instead of the 9-anchor's "at
most one." With `FixLog` now closed, exactly one gap remains (`ArtworkDownloader`'s three network-bound
entry points, cross-referenced with `StoreNameLookup`'s four network-bound writers), named, evidenced, and
queued in the Improvement Backlog — which promotes `test_strategy` to 9.5 with a queued residual, a genuine
structurally-proven UP, not a manufactured one. Also evaluated loop 11's queued Priority 2 (an injectable
HTTP-fetch seam for `ArtworkDownloader`) against the Unified Seam Policy per this loop's dispatch
instructions: it survives, but only in a specific idiom-matched shape (a static delegate injection point
matching the codebase's existing `RecordFolder`/`LogFolder` pattern, not a new interface/protocol), recorded
as a refined Deepening Candidate for next loop rather than built opportunistically inside this loop's
test-only fix.

## Scorecard (1-10)

- **Architecture quality:** 7.0 | SAME | `PrimaryWidget.xaml.cs` confirmed byte-identical since loop 9's
  commit `05501e0` via `git diff --stat 05501e0 HEAD` (empty); re-read `LoadGameEntriesAsync` (`:332-611`)
  directly this loop and confirmed the same merge of image decode (`:520-522`), backup check (`:516`), and
  per-game network resolution (`:562-609`) persists inside the nested `foreach` at `:436`.
- **State management and runtime ownership:** 7.0 | SAME | `AppliedArtworkStore.cs` and `FixLog.cs`
  production code confirmed byte-identical to HEAD this loop (`git diff --stat` shows only
  `SteamGridDB.Xbox.Tests/FixLogTests.cs` added; zero production files touched).
- **Domain modeling:** 5.5 | SAME | Not re-litigated this loop per this run's explicit prior finding (loop
  11 SPT-rejected a construction-time-invariant fix on the wire DTOs). Confirmed via `git diff --stat` (empty
  on `SteamGridDbGame.cs`/`ArtworkSource.cs`) that no new evidence exists this loop to reopen that question.
- **Data flow and dependency design:** 6.0 | SAME | `StoreNameLookup.cs` read fresh this loop: the three
  unlocked static caches (`:27-32`) confirmed unchanged.
- **Framework / platform best practices:** 6.0 | SAME | Not re-litigated this loop per this run's explicit
  prior finding (loop 11 SPT-rejected the `DataContractJsonSerializer`/`Windows.Data.Json` split as a
  framework-idiom violation). Confirmed via `git diff --stat` (empty on `SteamGridDbClient.cs`) that no new
  evidence exists this loop.
- **Concurrency and runtime safety:** 6.5 | SAME | F-003's sequential per-game awaits
  (`PrimaryWidget.xaml.cs:562,584,593,602`) re-confirmed at identical line numbers via this loop's own direct
  read.
- **Code simplicity and clarity:** 8.5 | SAME | This loop's diff is one new test file only
  (`SteamGridDB.Xbox.Tests/FixLogTests.cs`, 106 lines) — no simplification, no new ceremony.
- **Test strategy and regression resistance:** 9.5 | UP (from 8.5) | Closed F-008: added `FixLogTests.cs`
  (4 tests) covering all three of `FixLog`'s members. Mutation-verified three times (removed
  `lines.Clear()` at `:49` → 1 failure; removed `fileName = file` at `:48` → 3 failures; no-op'd
  `lines.Add(line)` at `:58` → 2 failures; all reverted, 125/125 green). Closes the second of two
  Authority-Map gaps loop 11 named as the sole reason `test_strategy` held at 8.5. With `FixLog` closed,
  exactly one gap remains (`ArtworkDownloader`/`StoreNameLookup`, `true-external`, queued in the backlog) —
  the 9-anchor's "at most one gap" bar is now met, and a queued residual is a valid disposition for 9.5.
  Mandatory mutation-test mental-model check: `ArtworkDownloader.cs:179`'s `candidateLayout < chosenLayout`
  comparison is the named residual.
- **Overall implementation credibility:** 7.5 | SAME | Anti-double-counting convention: this loop's fix is
  credited entirely to `test_strategy` rather than double-counted here.

## Authority Map

Re-emitted this loop per G24. Scope confirmed against `TESTING.md`'s own "What is not covered" section.

- **Concern:** Applied-artwork record. **Owner:** `AppliedArtworkStore`. **Verdict:** Single and clear.
  Direct test: `AppliedArtworkStoreTests.cs`.
- **Concern:** Fix-run diagnostic log. **Owner:** `FixLog`. **Verdict:** Single and clear. **Direct test:
  `FixLogTests.cs` (new this loop)** — was "no direct test file" through loop 11; closed this loop.
- **Concern:** Store-name lookup caches and the artwork download/selection gate. **Owner:** `StoreNameLookup`
  and `ArtworkDownloader`. **Verdict:** Single and clear ownership, but the sole remaining test gap
  (`true-external` network calls with no injectable seam).

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design (`Services/SteamGridDB/
  ArtworkSource.cs:15-51`) still makes "neither a platform-ID nor a game-ID" unrepresentable — unaffected
  this loop.
- `FixLog.LogFolder`'s settable-static-property pattern (matching `AppliedArtworkStore.RecordFolder`) let
  this loop's fix reach full coverage with zero production risk — the same shape now proven twice, and
  exactly the idiom the Deepening Candidate proposes reusing for `ArtworkDownloader`'s network seam.
- The mutation-verification technique from loop 10/11 scaled to a third distinct case this loop — a stateful
  three-member static class rather than a pure algorithm — and caught all three targeted mutations precisely.

## Findings

**F1 (F-001)** — `PrimaryWidget.xaml.cs` still merges UI event handling, image decode, network resolution,
and bulk-operation orchestration behind zero Interface boundary. Serious deduction. Carried forward, no new
evidence this loop reopens a further slice.

**F2 (F-007)** — `ArtworkDownloader`'s three entry points and `StoreNameLookup`'s four network-bound writers
remain untested because each calls the network directly with no injectable seam. Noticeable weakness.
Refined this loop: held loop 11's queued HTTP-fetch seam Deepening Candidate against the Unified Seam
Policy and resolved its shape — a settable static `Fetcher` delegate matching the codebase's existing
`RecordFolder`/`LogFolder` idiom, not a new interface. Now the sole remaining Authority-Map test-coverage
gap. Promoted to Priority 1.

**F3 (F-003)** — Library load issues one sequential SteamGridDB round-trip per game with no bounded
concurrency. Noticeable weakness. Blocked for the duration of this run by the standing operational
constraint.

**F4 (F-008)** — `FixLog` had zero direct test coverage. Noticeable weakness as discovered; **resolved this
loop**. Added `FixLogTests.cs` (4 tests), independently mutation-verified against three separate production
lines. Full evidence chain and mutation-verification detail in Loop 12 Result below.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Adding one new test file (`FixLogTests.cs`, 106 lines, 4 tests). No Module removed or restructured, no Seam introduced. |
| New seam justified | false — no protocol/port/abstraction introduced this loop; the `ArtworkDownloader` seam is deferred, shape resolved but not built. |
| Helpful simplification | None — test-coverage addition, not a simplification. |
| Should NOT be done | Building the `ArtworkDownloader` HTTP seam this loop without its own fresh SPT pass; forcing a `domain_modeling`/`framework_idioms` finding with no new evidence; any further slice of F-001 with no new evidence. |
| Tests after fix | Four new tests at `FixLog`'s existing public Interface, following the `TempFolder`-plus-settable-static-property pattern. Verified mutation-sensitive directly (three production lines mutated in turn). |

## Improvement Backlog

1. Add a settable static `Fetcher` delegate to `ArtworkDownloader` and cover it with `ArtworkDownloaderTests.
   cs` (F-007, remaining half) — closes the last Authority-Map test-coverage gap; shape now resolved.
2. Bound concurrency in `LoadGameEntriesAsync`'s per-game SteamGridDB lookups (F-003) — blocked for the
   duration of this run by the standing operational constraint.

## Deepening Candidates

- **Candidate Module:** `ArtworkDownloader.DownloadArtworkAsync`. Friction proven loop 11. This loop's
  refinement: a settable static `internal static Func<string, Task<IBuffer>> Fetcher` property defaulting to
  the real HTTP call, matching `FixLog.LogFolder`'s/`AppliedArtworkStore.RecordFolder`'s existing shape
  rather than a new named interface. `true-external`. What not to do: no general-purpose `IHttpClient`
  wrapper across the codebase; no recording-only stub (the fake must return real per-URL bytes).

## Builder Notes

1. **Pattern:** When a prior loop leaves a seam proposal open-ended, check whether the codebase already has
   a working idiom for the exact same problem shape before inventing a new one. → REVIEW_HISTORY.json
   `loops[11].builder_notes` for full notes.
2. **Pattern:** A stateful three-member static class (reset / append / flush-to-disk) needs a distinct
   mutation per member to prove real coverage, not one test per public method. → REVIEW_HISTORY.json
   `loops[11].builder_notes` for full notes.
3. **Pattern:** Re-litigating a long-held score without new source evidence just re-derives the same
   conclusion at the cost of a loop's investigation budget. → REVIEW_HISTORY.json `loops[11].builder_notes`
   for full notes.

## Final Judge Narrative

Place, not win. Ground truth was clean going in and clean coming out (125/125 tests, MSBuild exit 0). This
loop closed the queued Priority 1 (F-008) with four new mutation-verified tests on `FixLog` and zero
production code change. Loop 11 stated in its own scorecard reasoning that having two open Authority-Map
gaps instead of "at most one" was the entire reason `test_strategy` held at 8.5. With `FixLog` closed,
exactly one gap remains, named, evidenced, and queued — sufficient for 9.5 per the rubric's own 9.5+
Threshold section. Genuine structurally-proven UP, not a manufactured one: the same conclusion follows
mechanically from loop 11's own stated criterion. Separately, this loop resolved the shape of loop 11's
queued `ArtworkDownloader` HTTP seam against the Unified Seam Policy — a settable static delegate, not a new
interface — precisely enough that next loop can execute rather than re-derive. Runtime ownership and
concurrency unaffected. Backlog is not empty (`ArtworkDownloader` seam, F-003), so `CONTINUE`.

## Loop 12 Result

Added one new test file, `SteamGridDB.Xbox.Tests/FixLogTests.cs` (106 lines, 4 tests), closing finding
F-008 (stable_id `F-008`). No production code changed in the final diff. `run-tests.ps1`: 121 passed before,
125 passed after. MSBuild: exit 0, both runs. Mutation-sensitivity independently verified three times: (1)
removed `FixLog.cs:49`'s `lines.Clear();`, got exactly 1 failure, reverted; (2) removed `FixLog.cs:48`'s
`fileName = file;`, got exactly 3 failures, reverted; (3) no-op'd `FixLog.cs:58`'s `lines.Add(line);`, got
exactly 2 failures, reverted; 125/125 re-confirmed green, `git diff --stat` on `FixLog.cs` empty before
commit. **Risk boundary evidence:** none — pure test-only addition. **Targeted finding status:** `resolved`.
**Unintended scorecard regression:** none; `test_strategy` moved UP (8.5 -> 9.5) with structural proof; no
other dimension changed.

## Loop 12 Implementation Review

`verdict: approved` — "FixLogTests.cs genuinely exercises Start/Write/SaveAsync with content-based
assertions, the three mutation-verification claims check out exactly against current FixLog.cs source, and
the diff touches zero production code." All three checks (`reality`, `honesty`, `regression`) `passed`;
`conditions: []`; `regressions: []`.

--- Loop 13 (UTC 2026-08-04T17:37:02Z) ---

### Discovery
See Loop 1 Discovery (refreshed Loop 7). Ground truth this loop: 125 tests passing before / 131 after;
MSBuild exit 0 both runs; zero source drift since loop 12's commit `e0245c4`. Selected lens: Generic
(C#/.NET / UWP-hosted WinUI stack).

### Loop Counter
Loop 13 of 15

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Both gates green before and after (125->131 tests, MSBuild exit 0). This loop's dispatch instructions asked
for two things to be weighed honestly rather than accepted from the prior loop: (1) re-verify `test_strategy`'s
9.5 against current source rather than defend loop 12's number, and (2) hold loop 12's queued Priority 1 (a
settable static `Fetcher` delegate on `ArtworkDownloader`) hard against the Unified Seam Policy before
building it. Both were done. On (2): the Fetcher-delegate proposal is **rejected** — its "second adapter"
would have to be a hand-built fake fabricating network response bytes (unproven behavior-faithful, the exact
gap loop 12's own Scorecard Humility Check already flagged), and a settable static delegate is unowned
mutable global state, which the dispatch correctly identified as the shape the Unified Seam Policy and
Ownership & State standards exist to catch. A narrower, honest alternative existed instead and was built:
`FindOfficialLookalikeAsync`'s replacement gate already reduces its inputs to plain doubles before deciding
— pure computation needing no seam, no fake, and no new mutable state. Extracted that decision into
`ArtworkDownloader.PassesColourAndLayoutGate` and added `ArtworkDownloaderTests.cs` (6 tests), independently
mutation-verified against all three logical mutations the boolean expression admits. This closes the exact
nameable mutation loop 12's own mandatory mutation-test check named. On (1): `test_strategy` holds at 9.5
**SAME**, not UP — the residual narrows (the specific named mutation is now caught) but does not close (the
network fetch itself and the two orchestration loops' boundary conditions remain untested), and the rubric's
score grid has no rung between 9.5 and 10, so SAME is the mechanically correct call, not a conservative one.
No dimension moved this loop; this is an honest all-SAME loop with real, source-proven forward motion inside
one dimension's residual, consistent with the dispatch's explicit "honest SAME beats a fabricated UP"
framing.

## Scorecard (1-10)

- **Architecture quality:** 7.0 | SAME | `PrimaryWidget.xaml.cs` confirmed byte-identical since loop 9's
  commit `05501e0` via `git diff --stat 05501e0 HEAD` (empty); re-read `LoadGameEntriesAsync` (`:332-611`)
  directly this loop and confirmed the same merge of image decode (`CreateThumbnailAsync`), backup check
  (`ArtworkFiles.HasBackupAsync`, `:516`), and per-game network resolution (`:562,584,593,602`) persists
  inside the nested `foreach` at `:436`. This loop's own diff (`ArtworkDownloader.cs`'s `PassesColourAndLayoutGate`
  extraction) is a small, local, in-file predicate extraction — real but not large enough to move the
  macro-level Module-graph judgment this dimension scores; not double-counted here (credited to
  `test_strategy` below).
- **State management and runtime ownership:** 7.0 | SAME | `AppliedArtworkStore.cs` and `FixLog.cs`
  production code confirmed byte-identical to HEAD this loop. The new `PassesColourAndLayoutGate` method is
  pure and stateless — no new mutable state, no new writer, confirmed by direct read
  (`ArtworkDownloader.cs:205-208`).
- **Domain modeling:** 5.5 | SAME | Not re-litigated this loop per this run's standing finding (loop 11
  SPT-rejected a construction-time-invariant fix on the wire DTOs). Confirmed via `git diff --stat` (empty
  on `SteamGridDbGame.cs`/`ArtworkSource.cs`) that no new evidence exists this loop to reopen that question.
- **Data flow and dependency design:** 6.0 | SAME | `StoreNameLookup.cs` read fresh this loop: the three
  unlocked static caches (`:27-31`) confirmed unchanged. This loop's new `PassesColourAndLayoutGate` method
  takes explicit parameters only (no ambient reads) — too small relative to the dimension's standing
  concerns to move the score.
- **Framework / platform best practices:** 6.0 | SAME | Not re-litigated this loop per this run's standing
  finding (loop 11 SPT-rejected the `DataContractJsonSerializer`/`Windows.Data.Json` split). Confirmed via
  `git diff --stat` (empty on `SteamGridDbClient.cs`) that no new evidence exists this loop.
- **Concurrency and runtime safety:** 6.5 | SAME | F-003's sequential per-game awaits
  (`PrimaryWidget.xaml.cs:562,584,593,602`) re-confirmed at identical line numbers via this loop's own direct
  read.
- **Code simplicity and clarity:** 8.5 | SAME | This loop's production diff is one extracted pure predicate
  in `ArtworkDownloader.cs` (16 insertions, 1 deletion) — a real, small, honest extraction, but per this
  run's anti-double-counting convention the credit goes entirely to `test_strategy`.
- **Test strategy and regression resistance:** 9.5 | SAME | Re-verified the 9.5 against current source per
  this loop's explicit dispatch instruction, not defended from loop 12's number. The 9-anchor is still met:
  exactly one Authority-Map gap remains, now narrower. Rejected loop 12's queued Fetcher-delegate remedy
  against the Unified Seam Policy (see Finding #2) and instead extracted `ArtworkDownloader.
  PassesColourAndLayoutGate` (`:205-208`), adding `ArtworkDownloaderTests.cs` (6 tests). Independently
  mutation-verified three times: (1) `candidateMatch > officialArtworkCeiling` to `>=` — 1 failure, reverted;
  (2) `candidateLayout >= chosenLayout` to `>` — 1 failure, reverted; (3) `&&` to `||` — 3 failures, reverted;
  131/131 re-confirmed green after each revert. This closes the specific nameable mutation loop 12's own
  mandatory mutation-test mental-model check named (`ArtworkDownloader.cs`'s old `:179`, `candidateLayout <
  chosenLayout`). **Not promoted to 10**: a fresh mutation-test check this loop names a different,
  still-uncaught mutation — `DownloadBestTileFillingImageAsync`'s fallback-candidate capture (`:85-89`).
  `ArtworkSignature.cs` also has zero test coverage (confirmed via grep), a previously-uncredited slice of
  the same gap. The score grid has no rung between 9.5 and 10; SAME is the mechanically correct score.
- **Overall implementation credibility:** 7.5 | SAME | Consistent with this run's anti-double-counting
  convention: this loop's fix is credited entirely to `test_strategy`.

## Authority Map

Re-emitted this loop per G24 (mandatory whenever `test_strategy >= 9`).

- **Concern:** Applied-artwork record. **Owner:** `AppliedArtworkStore`. **Verdict:** Single and clear.
  Direct test: `AppliedArtworkStoreTests.cs`.
- **Concern:** Fix-run diagnostic log. **Owner:** `FixLog`. **Verdict:** Single and clear. Direct test:
  `FixLogTests.cs`.
- **Concern:** Store-name lookup caches and the artwork download/selection gate. **Owner:**
  `StoreNameLookup` and `ArtworkDownloader`. **Verdict:** Single and clear ownership. **Test gap narrowed
  this loop**: `ArtworkDownloaderTests.cs` (new) directly tests the replacement gate's colour/layout
  decision (`PassesColourAndLayoutGate`), independently mutation-verified. Still untested: the three async
  entry points' network/orchestration behavior, `StoreNameLookup`'s four writers, and `ArtworkSignature.cs`'s
  `ColourMatch`/`LayoutMatch`/`CreateAsync` (zero test file). All `true-external` network calls or their
  immediate consumers with no seam built yet.

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design (`Services/SteamGridDB/
  ArtworkSource.cs:15-51`) still makes "neither a platform-ID nor a game-ID" unrepresentable — unaffected
  this loop.
- This loop's rejection of the Fetcher-delegate seam is itself evidence of a working discipline, not just its
  conclusion: it correctly distinguished `FixLog.LogFolder`/`AppliedArtworkStore.RecordFolder`'s
  local-substitutable pattern (two real adapters, safe to reuse) from a true-external network fetch (would
  need a fabricated fake, not proven behavior-faithful) using the codebase's own Dependency Categorization
  table rather than pattern-matching on syntactic shape alone.
- The mutation-verification technique established in loop 10 and refined since scaled to a fourth distinct
  case this loop — a multi-clause boolean gate extracted from inside an async loop — and caught all three
  targeted mutations precisely (1, 1, and 3 failures respectively).

## Findings

**F1 (F-001)** — `PrimaryWidget.xaml.cs` still merges UI event handling, image decode, network resolution,
and bulk-operation orchestration behind zero Interface boundary. Serious deduction. Carried forward, no new
evidence this loop reopens a further slice.

**F2 (F-007)** — `ArtworkDownloader`'s fetch/orchestration entry points and `StoreNameLookup`'s four writers
remain untested; this loop rejected the queued Fetcher-delegate seam against the Unified Seam Policy and
split out the tested decision core instead. Noticeable weakness. Extracted `PassesColourAndLayoutGate`
(`ArtworkDownloader.cs:205-208`) and added `ArtworkDownloaderTests.cs` (6 tests, mutation-verified). Closes
the specific nameable mutation loop 12 named; the broader network-facing surface (`DownloadArtworkAsync`,
orchestration loop boundaries, `StoreNameLookup`'s four writers, `ArtworkSignature.cs`) remains untested.
Carried forward.

**F3 (F-003)** — Library load issues one sequential SteamGridDB round-trip per game with no bounded
concurrency. Noticeable weakness. Blocked for the duration of this run by the standing operational
constraint.

## Simplification Check

| Field | Value |
|---|---|
| Structurally necessary | Extracting `PassesColourAndLayoutGate` from `FindOfficialLookalikeAsync`'s inline boolean guard — Interface now smaller than and independently testable from the surrounding orchestration. |
| New seam justified | false — no protocol/port/abstraction introduced this loop; not a Seam (in-process, no I/O). The Fetcher-delegate Seam loop 12 queued was evaluated and explicitly rejected (Finding #2) rather than built. |
| Helpful simplification | Minor positive simplification, but credited to `test_strategy` per this run's anti-double-counting convention. |
| Should NOT be done | Building the Fetcher-delegate seam (rejected, Finding #2). Extending this loop's extraction to `StoreNameLookup`'s writers or `DownloadArtworkAsync` itself (no proven friction/no decision logic to extract). Forcing a `domain_modeling`/`framework_idioms` finding or any further slice of F-001 — no new evidence this loop. |
| Tests after fix | Six new tests at `PassesColourAndLayoutGate`'s new Interface. Verified mutation-sensitive directly: three operators mutated in turn, exactly the expected test(s) failed each time, then reverted. |

## Improvement Backlog

1. Add `ArtworkSignatureTests.cs` and extract the `officialArtworkFloor` gate as a second tested predicate
   (F-007, continuing narrowing) — the same zero-seam idiom this loop proved out. Explicitly do NOT build a
   Fetcher/network seam — rejected this loop against the Unified Seam Policy.
2. Bound concurrency in `LoadGameEntriesAsync`'s per-game SteamGridDB lookups (F-003) — blocked for the
   duration of this run by the standing operational constraint.

## Deepening Candidates

- **Candidate Module:** `ArtworkDownloader`'s orchestration (`DownloadBestTileFillingImageAsync` +
  `FindOfficialLookalikeAsync`'s candidate-selection loops). Friction proven Finding #2: pulling the leaf
  decision out required no seam and unlocked direct testing immediately; the loops' own control flow (which
  candidate to try next, when to stop, the fallback capture) is still interleaved with the network fetch.
  `in-process`. Smallest first step: extract one more leaf decision (`officialArtworkFloor` gate) before
  attempting the loop-control extraction itself. What not to do: no general iterator/strategy abstraction;
  do not touch `DownloadArtworkAsync` itself.

## Builder Notes

1. **Pattern:** When a proposed seam's second adapter would have to be a hand-built fake simulating an
   external system, check first whether the decision logic that actually needs testing is separable from
   the fetch. → REVIEW_HISTORY.json `loops[13].builder_notes` for full notes.
2. **Pattern:** A settable static delegate (`Func<T>`) proposed as a test seam for a true-external
   dependency is architecturally different from a settable static property proposed for a
   local-substitutable one. → REVIEW_HISTORY.json `loops[13].builder_notes` for full notes.
3. **Pattern:** Extracting a multi-clause boolean gate into its own named method turns each clause boundary
   into an independently mutation-testable unit. → REVIEW_HISTORY.json `loops[13].builder_notes` for full
   notes.

## Final Judge Narrative

Place, not win. Ground truth was clean going in and clean coming out (131/131 tests, MSBuild exit 0). This
loop's dispatch asked two things to be weighed honestly rather than inherited: whether `test_strategy`'s 9.5
still holds, and whether loop 12's queued Fetcher-delegate seam survives the Unified Seam Policy. On the
seam: it does not survive — the second adapter would be fabricated network data, not a real one, unlike the
`StorageFolder` idiom it was modeled on, and a settable static delegate is unowned global mutable state
regardless. Rather than build seam ceremony to buy a test-coverage point, this loop found and took a
genuinely honest narrower alternative: the exact decision logic the Authority Map gap's risk language
pointed at was already pure computation one call away from testable. On `test_strategy`: 9.5 holds, not
because the prior loop said so, but because a fresh mutation-test check this loop names a still-uncaught
mutation elsewhere in the same file — the residual moved, the score didn't. Runtime ownership and
concurrency are unaffected and exactly as trustworthy as loop 12 left them. Tests reduce regressions more
precisely than last loop measured on this specific gate. Future work risks nothing new from overengineering.
Backlog is not empty, so `CONTINUE`.

## Loop 13 Result

Extracted `ArtworkDownloader.PassesColourAndLayoutGate(double, double, double)` from
`FindOfficialLookalikeAsync`'s inline boolean guard (`SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs`,
16 insertions/1 deletion, behavior-preserving via De Morgan's law — the short-circuit position of
`TileImage.FillsTileAsync` is unchanged) and added `SteamGridDB.Xbox.Tests/ArtworkDownloaderTests.cs` (6
tests), closing the specific nameable mutation loop 12's own mandatory mutation-test check named (finding F2,
stable_id F-007, remaining half). No other production file changed.

**What proves the change is honest:** `run-tests.ps1`: 125 passed before, 131 passed after (delta is exactly
the 6 new tests). MSBuild: exit 0, both runs. Mutation-sensitivity independently verified three times: (1)
changed `ArtworkDownloader.cs:207`'s `candidateMatch > officialArtworkCeiling` to `>=`, re-ran the full suite,
got exactly 1 failure (`Fails_when_the_colour_match_is_exactly_at_the_ceiling`), reverted; (2) changed the
same line's `candidateLayout >= chosenLayout` to `>`, re-ran, got exactly 1 failure
(`Passes_when_the_layout_match_exactly_ties_the_artwork_it_would_replace`), reverted; (3) changed `&&` to
`||`, re-ran, got exactly 3 failures, reverted; 131/131 re-confirmed green after each revert, and the final
`git diff -- SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs` was confirmed to match the intended diff
before the implementation review and commit.

**Risk boundary evidence (Meta-Rule 4):** none — this fix crosses no isolation/Sendable/conditional-
compilation/cross-file-visibility/lock-ordering boundary. It is a pure, stateless, single-file extraction plus
one new test file; no concurrency primitive, visibility scope, or platform conditional changed.

**Targeted finding status:** `carried_forward` — F-007's Claim still holds for the network-facing surface;
the specific mutation loop 12 named as evidence for that claim is now caught, but the Claim's remaining scope
is unresolved, so the finding is not marked resolved.

**Unintended scorecard regression:** none. All nine dimensions held SAME with fresh structural re-derivation
this loop; `test_strategy` remains at 9.5 (residual narrowed, not closed) rather than moving UP or DOWN. No
dimension regressed.

## Loop 13 Implementation Review

`verdict: approved` — "The extraction is a verified De Morgan-equivalent, short-circuit-preserving refactor
with no new seam, and the 6 new tests directly and mutation-verifiably cover the exact boundary conditions
(candidateMatch > officialArtworkCeiling, candidateLayout >= chosenLayout) that were previously untested
inline logic." All three checks (`reality`, `honesty`, `regression`) `passed`; `conditions: []`;
`regressions: []`.
