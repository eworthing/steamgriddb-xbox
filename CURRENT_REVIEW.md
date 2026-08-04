### Discovery (see Loop 7 Discovery refresh)

No drift since loop 8's commit (`71224de`): `git status --porcelain` was clean at this loop's Step 1, and
`git log 71224de..HEAD` was empty before this loop's own edits. Both ground-truth gates re-run fresh this
loop, independent of loop 8's cached numbers:

- `powershell -NoProfile -File ./run-tests.ps1` — **105 passed, 0 failed** before this loop's fix, **114
  passed, 0 failed** after (9 new tests added; see Loop 9 Result).
- `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
  SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo` — exit
  0 before and after.
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`: 1,978 lines at Step 1 (matches loop 8's post-fix figure exactly
  - `wc -l` re-verified fresh this loop, not carried), 1,950 lines after this loop's fix (-28).

### Loop Counter

Loop 9 of 10 (cap)

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Re-derivation from current source (both gates green before and after this loop's change: 105→114 tests,
MSBuild exit 0) found this loop's queued Priority 1 - the "corrected, narrower" field-extraction slice of
`LoadGameEntriesAsync` that loop 8 proposed after downgrading loop 7's flawed two-phase split - genuinely
survives a fresh Simplify Pressure Test this time. Reading the full method confirmed the platform-specific
identifier/name-derivation block (`gameName` default, `externalPlatformId`, `epicCatalogItemId`, including
Epic's tricky colon-split parsing) has zero data dependency on the image-decode/backup-check block it sits
next to in either direction, so it extracted into a new pure static module with no reordering of any
surrounding code. This is a real, narrow, verified win - distinct from loop 8's accessor-swap fix, which
touched no ownership boundary. A parallel independent read of the rest of the codebase (state management,
domain modeling, data flow, framework idioms, concurrency, test coverage - partly delegated to two read-only
helper passes, fully re-derived and verified here) found no other live defect, but did surface one
previously-uncredited test-coverage gap (`ArtworkDownloader`/`TileImage`, Finding #4) and confirmed two
suspected concurrency smells (`StoreNameLookup`'s unlocked static caches; `ArtworkFiles.ApplyAsync`'s
check-then-act backup logic) are not live hazards today - no concurrent call path exists anywhere in the
codebase to exploit either.

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
  `LoadGameEntriesAsync`'s fully sequential `foreach` (`PrimaryWidget.xaml.cs:562,584,593,602`) - no
  concurrent call path exists today. Not a live finding; noted as a landmine for F-003's eventual remedy
  (see Finding #3), not scored as a separate defect.
- **Code simplicity and clarity:** 8.5 | UP | `PrimaryWidget.xaml.cs:540-546` replaced a 38-line inline
  nested `if`/`else` block (`git diff` hunk: -38/+11 lines) with 4 lines calling
  `ManifestEntryIdentity.Derive` - a genuine, verified simplification a reader can now either trust via the
  function's tested contract or inspect separately, rather than parsing inline branching mid-entry-loop.
- **Test strategy and regression resistance:** 8.0 | SAME | The two previously-disclosed gap categories
  (`PrimaryWidget`'s architecturally-untestable shell seams; F-005's `RankGrids` mutation gap) are joined by
  a third, newly-surfaced gap this loop (Finding #4/F-007: `ArtworkDownloader`'s three entry points and two
  of `TileImage`'s three public methods have zero coverage - confirmed by directory listing and grep of
  `TileImageTests.cs`'s four `Fact` methods, which only exercise `EnsurePngAsync`). This loop's own fix
  closes a small piece of the first gap category (`ManifestEntryIdentityTests.cs` directly tests the Epic
  colon-split boundary, previously untestable-by-construction) but the newly-found gap keeps the ceiling at
  8 rather than moving it - a wash: F-007's gap pre-existed this loop and was simply not previously named,
  not a regression this loop introduced.
- **Overall implementation credibility:** 7.5 | SAME | This loop's diff (a genuine, reviewer-approved,
  gate-verified extraction with matching tests - 114/114 tests, MSBuild exit 0, both before and after) is
  credited to `architecture_quality` (+0.5) and `simplicity` (+0.5) rather than here, per the established
  convention (loop 8 credited its own accessor-swap-only fix to credibility precisely because it was *not*
  a concern relocation; this loop's fix *is* one, so the inverse applies). `PrimaryWidget.xaml.cs`'s
  remaining 1,950 lines are still unverified by anything but inspection and a green compile outside this
  loop's tested slice.

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
8). This run's standing operational constraint continues to rule out attempting this finding: parallelising
these round-trips would change observable request count/order/timing against third-party APIs without a
behavioral oracle, and the test suite still does not cover network calls. **New this loop**: if this
constraint is ever lifted, the remedy must also add locking to `StoreNameLookup`'s three static caches
(`gogNameCache`/`epicNameCache`/`nameMatchCache`, `StoreNameLookup.cs:27-32`) - their current
check-then-await-then-write shape is safe only because calls are strictly sequential today; bounding
concurrency without also synchronizing these would introduce a live race where none exists now.

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

**Locality impact** — The fix is new tests only; no production code change needed - both `TileImage` and
`ArtworkDownloader` are already pure/WinRT-testable, proven by `TileImageTests.cs`'s existing `EnsurePngAsync`
coverage using the same infrastructure.

**Metric signal** — 3 of 3 `ArtworkDownloader` entry points untested (0% file coverage); 2 of 3 public
`TileImage` methods untested (`FillsTileAsync`, `CropPortraitToTileAsync`).

**Why this weakens submission** — A source-level mutation in the tile-fill gate or the crop-window
selection (e.g., flipping a comparison in `BestVerticalCropAsync`'s window-scoring, or inverting
`FillsTileAsync`'s threshold) would pass the entire 114-test suite undetected - the same category of gap
method.md Step 8 requires naming before `test_strategy` can score above 8.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Add `ArtworkDownloaderTests.cs` exercising
`DownloadBestTileFillingImageAsync`'s ranking-to-selection gate with constructed `IBuffer` fixtures
(matching `TestImages.cs`'s existing pattern); add `FillsTileAsync`/`CropPortraitToTileAsync` cases to
`TileImageTests.cs` using the same WinRT-buffer construction `EnsurePngAsync`'s tests already use. No
production code changes required.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/ArtworkDownloaderTests.cs` (new),
`SteamGridDB.Xbox.Tests/TileImageTests.cs` (new cases). Avoid:
`SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs`, `SteamGridDB.Xbox/Services/Artwork/TileImage.cs`,
everything else.

## Simplification Check

- **Structurally necessary:** Extracting `ManifestEntryIdentity.Derive` from `LoadGameEntriesAsync`'s inline
  platform-identity block. Passes the Deletion test (removing it reintroduces the Epic-parsing complexity
  inline - it earns its keep). No Seam/protocol introduced (a plain static method + struct), so the Unified
  Seam Policy does not apply - confirmed by the independent implementation reviewer.
- **New seam justified:** false - no protocol/port/abstraction introduced, just a static-method extraction.
- **Helpful simplification:** The Epic colon-split logic (`parts.Length >= 3` / `>= 4` boundary) is now
  directly unit-tested at 4 boundary cases (4-segment, 3-segment, <3-segment, non-Epic) - a mutation on
  either comparison would now fail a test, closing a mutation-test gap method.md Step 8 would otherwise
  flag.
- **Should NOT be done:** Attempting a further split of `LoadGameEntriesAsync`'s image-decode/backup-check/
  network-resolution core this loop - loop 8 already confirmed (and this loop re-confirmed by reading the
  current method top to bottom) that these remain genuinely interleaved per entry; no new evidence surfaced
  this loop to reopen that question, so no further slice is queued without first re-verifying against
  current source. Also not attempted this loop: F-005's `RankGrids` test gap and F-003's concurrency fix -
  both lower severity/priority than this loop's fix, and F-003 remains blocked by the standing operational
  constraint.
- **Tests after fix:** No prior tests existed for the removed inline platform-identity block (it lived
  inside `PrimaryWidget.xaml.cs`, architecturally untestable in place). Nine new tests added at the new
  Interface (`ManifestEntryIdentityTests.cs`), including the Epic colon-split boundary conditions
  (3-segment vs 4-segment vs <3-segment) previously unreachable by any test. This is a deepening (a
  previously-inline, untestable block is now a tested Module), so Replace-don't-layer's requirement is
  satisfied: the old inline code is fully removed (not left as a parallel shallow copy), and all new
  assertions target the new Interface directly.

## Improvement Backlog

1. **Add the missing RankGrids style-priority mixed-style test case (F-005)** — one new
   `ArtworkRankerTests.cs` test case, no production code change.
   - Why it matters: `test_strategy`'s current 8.0 ceiling is partly explained by this named, source-backed
     gap, carried across loops 7-9 unattempted while higher-severity findings took priority.
   - Score impact: Test strategy +0.5-1.0 once verified.
2. **Add ArtworkDownloader/TileImage test coverage for the tile-fill gate and vertical-crop selection (F-007)**
   — new `ArtworkDownloaderTests.cs`, new `TileImageTests.cs` cases; no production code change.
   - Why it matters: closes the newly-surfaced third named gap in `test_strategy`'s ceiling; the tile-fill
     gate sits on the widget's primary automatic-artwork-pick path.
   - Score impact: Test strategy +0.5-1.0 once verified.
3. **Bound concurrency in LoadGameEntriesAsync's per-game SteamGridDB lookups (F-003)** — blocked for the
   duration of this run by the standing operational constraint. Carried forward as a reminder; this loop
   added the caching-synchronization prerequisite (see Finding #3) to whatever eventually attempts it.
   - Why it matters: removes load latency scaling linearly with library size, whenever the constraint is
     lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

None this loop. `ManifestEntryIdentity` (new this loop) is already deep for its scope - one small Interface
(`Derive`, 4 params in / 3-field struct out), Implementation handles three distinct platform-branching cases
including Epic's two-identifier split; it has exactly one caller today, consistent with a fresh extraction,
not yet showing deletion-test-failing friction that would justify further deepening. `JsonRead` and
`AsyncLazyCache<T>` remain unaffected and already deep from prior loops. `LoadGameEntriesAsync`'s remaining
concern-merge (Finding #1's residue) is a splitting/ownership problem, not a shallow-Interface-needs-
deepening problem - correctly tracked as a Finding + backlog note instead.

## Builder Notes

1. **Pattern:** A carried-forward backlog item's proposed remedy can be correct even when a related,
   differently-scoped remedy failed the same test - don't let a prior failed attempt poison a genuinely
   narrower reframing of the same finding.
   - How to recognize: a backlog item explicitly framed as "the corrected, narrower version of X" after X
     failed the Simplify Pressure Test - the narrower version deserves its own fresh SPT pass, not
     inherited rejection.
   - Smallest coding rule: before rejecting a queued remedy because "something like this failed before,"
     re-read the queued remedy's actual scope and re-run SPT on that scope specifically, not the scope of
     the prior failed attempt.
   - Stack example: C# - loop 7's two-phase parse/UI-decode split failed because image decode is interleaved
     per-entry; loop 9's narrower field-extraction-only slice (no image-decode phase claimed) had zero
     dependency on that interleaving and passed cleanly on a fresh read.

2. **Pattern:** A pure computation buried inside a large orchestration method can be verified fully
   separable by checking data dependencies in both directions - not just "does the later code need this,"
   but "does the earlier code the extraction sits next to need anything the extraction produces."
   - How to recognize: an inline block bracketed by side-effecting code (I/O calls, `continue` statements,
     mutable counters) on either side - check whether the block's inputs are all available before the
     surrounding code runs, and whether its outputs are needed by nothing until after.
   - Smallest coding rule: trace every local variable the candidate block reads and writes; if none of its
     reads come from the surrounding side-effecting code and none of its writes feed back into it, the block
     is safe to extract without reordering.
   - Stack example: C# - `ManifestEntryIdentity.Derive`'s inputs (`entryObject`, `platform`, `entryId`,
     `unknownName`) were all set before the image-decode block ran, and its outputs (`gameName`,
     `externalPlatformId`, `epicCatalogItemId`) were not read until after - so it moved out with zero
     reordering of the surrounding image-decode/backup-check code.

3. **Pattern:** A theoretically-racy static mutable cache is not evidence of a live concurrency defect if
   nothing in the codebase actually calls it from more than one execution path.
   - How to recognize: unlocked static `Dictionary` writes with a check-then-fetch-then-write shape (a
     classic TOCTOU pattern in isolation) - before flagging as a finding, grep the whole codebase for
     `Task.WhenAll`, `Parallel.*`, `Task.Run`, and any other concurrent-dispatch mechanism; if none exists
     and every caller is reached through a single sequential loop, the hazard is latent, not live.
   - Smallest coding rule: a concurrency finding needs two things, not one - an unsynchronized shared write
     AND a source-verified concurrent call path. Missing the second downgrades it from a finding to a
     landmine note on whichever finding would introduce the concurrent path.
   - Stack example: C# - `StoreNameLookup`'s `gogNameCache`/`epicNameCache`/`nameMatchCache` are unlocked
     static `Dictionary`s with a `TryGetValue`-then-`await`-then-write shape, but their only caller is
     `LoadGameEntriesAsync`'s single sequential `foreach` - confirmed via grep across the whole codebase
     that no `Task.WhenAll`/`Parallel`/`Task.Run` exists anywhere, so this is not a live race today.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. `architecture_quality`'s +0.5 magnitude (6.5→7.0) for extracting one narrow, single-caller pure function
   - a stricter reviewer could argue this is too small a win to move a whole-codebase architecture dimension,
   especially since `LoadGameEntriesAsync`'s core interleaving problem (Finding #1's real substance) is
   completely untouched by this loop.
2. Leaving Finding #1/F-001 off this loop's Improvement Backlog entirely, with no queued next slice - a
   stricter reviewer might argue I should have at least attempted the SPT on a further candidate slice
   (e.g., splitting out just the `addedDate`/timestamp parsing, or the non-Custom `imageFilePath` one-liner)
   rather than stopping after one slice; I judged those too trivial to justify a dedicated Module (a single
   `JsonRead.String` call or `Path.Combine` one-liner has no real domain logic worth protecting behind a
   tested boundary), but that judgment call could reasonably go either way.
3. Scoring the newly-found Finding #4/F-007 (ArtworkDownloader/TileImage test gap) as keeping `test_strategy`
   at a "wash" (SAME) rather than moving it down - a stricter reading could argue that surfacing a
   previously-uncredited, source-verified gap in a central artwork-selection path (not a minor helper)
   should lower the score below its apparent floor of 8.0, since the codebase's actual test coverage was
   always thinner than that number implied; I judged this a discovery correction (the codebase did not get
   worse this loop) rather than a regression, but the resulting number is identical to what a more generous
   original grading would have produced anyway.

## Final Judge Narrative

Place, not win. This loop re-ran the Simplify Pressure Test on loop 8's queued "corrected, narrower"
field-extraction slice from scratch rather than assuming a two-loop-old plan was still correct - and this
time it held: reading `LoadGameEntriesAsync` fully confirmed the platform-identifier derivation block has no
data dependency on the surrounding image-decode/backup-check code in either direction, so it extracted
cleanly into a new tested module with zero reordering. This is a real, if narrow, architectural win, distinct
in kind from loop 8's accessor-swap fix: it relocates ownership of a genuinely tricky piece of domain logic
(Epic's colon-split identifier parsing) behind a small, deep, directly-tested Interface, closing a mutation-test
gap that previously had zero coverage. `LoadGameEntriesAsync`'s core problem - image decode, backup checks,
and network calls still genuinely interleaved per entry - is untouched, and this loop deliberately did not
queue a further slice without first re-verifying one exists; forcing a plan just to keep the backlog full
would repeat the exact mistake loop 9 caught loop 8 in the act of correcting for loop 7. A parallel
independent re-derivation of the rest of the scorecard surfaced one new, real test-coverage gap
(`ArtworkDownloader`/`TileImage`, Finding #4) and ruled out two suspected concurrency hazards as latent
rather than live, with source-verified reasoning either way. Runtime ownership is unaffected and exactly as
trustworthy as loop 8 left it. Concurrency remains sequential and safe today; the one new evidence item
(`StoreNameLookup`'s unlocked caches) is a landmine for F-003's eventual remedy, not a present defect. Tests
reduce regressions more than last loop measured, though the newly-found gap keeps the net movement at zero.
Future work risks nothing new from overengineering - this loop's fix added no abstraction beyond what the
extracted logic already needed to be independently testable.

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
from `PrimaryWidget.xaml.cs` - confirmed by the independent implementation-reviewer pass below, which
compared the diff's removal against the new file side by side. This changes only where the platform-identity
logic lives, not its behavior for any well-formed or malformed manifest entry (every existing case - Custom
with/without title, GOG/Steam prefix-strip, Epic 3- and 4-segment ids - produces identical output, verified
by the 105 pre-existing tests still passing unchanged plus 9 new tests asserting the exact fallback
semantics at each boundary).

**Risk boundary evidence (Meta-Rule 4):** none - this fix crosses no isolation/Sendable/conditional-
compilation/cross-file-visibility/lock-ordering boundary. `ManifestEntryIdentity` is a new `internal static`
class in the same assembly as its one caller; no visibility modifier changed anywhere, and the extraction
introduces no new thread, task, or shared mutable state.

**Targeted finding status:** `carried_forward` - Finding #1/F-001's core claim (the image-decode/backup-
check/network-resolution/UI-orchestration merge) is unaffected; this loop closed one further evidence slice
of it (the platform-identity sub-step) without resolving the finding as a whole, consistent with F-001's
established loop-by-loop incremental-slice pattern since loop 1.

**Unintended scorecard regression:** none observed. `architecture_quality` and `simplicity` moved UP on
distinct, source-verified proof (the new module + the line-count reduction); `state_management`,
`domain_modeling`, `data_flow`, `framework_idioms`, `concurrency`, `test_strategy`, `credibility` all held
SAME (no structural change in their evidence this loop, or - for `test_strategy` - a newly-found gap
offsetting a newly-closed one); no dimension regressed.

## Loop 9 Implementation Review

`verdict: approved` — "The extraction is byte-identical to the removed inline logic (including the Epic
colon-split boundary conditions), introduces no Seam so the Unified Seam Policy is correctly inapplicable,
is directly tested at its new Interface with mutation-sensitive assertions, and both verification gates
(114/114 tests, MSBuild exit 0) pass clean." All three checks (`reality`, `honesty`, `regression`) `passed`;
`conditions: []`; `regressions: []`.
