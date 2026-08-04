### Discovery (see Loop 1 Discovery)

Resumed from loop 14's `CONTINUE` at commit `69bc15b`. Working tree was clean at dispatch. Both ground-truth
gates re-run fresh before touching anything, per dispatch instructions:

- `powershell -NoProfile -ExecutionPolicy Bypass -File ./run-tests.ps1` — **131 passed, 0 failed** before this
  loop's fix, **138 passed, 0 failed** after (7 new tests: 5 in `ArtworkSignatureTests.cs`, 2 boundary tests
  in `ArtworkDownloaderTests.cs`).
- `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
  SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo` — exit
  0 before and after.
- `git log 69bc15b..HEAD` was empty before this loop's own edits; HEAD matched loop 14's commit exactly.
- Selected lens: Generic (C#/.NET / UWP-hosted WinUI stack).

**This is the cap loop (loop_cap = 15).** Per the protocol, Steps 1-3 execute in full exactly like any
CONTINUE loop; only the terminal system flag differs (`HALT_LOOP_CAP` in place of `CONTINUE`).

**Blind-critic ordering note**: this loop's independent scorecard below was re-derived from direct source
reads (`StoreNameLookup.cs`, `AppliedArtworkStore.cs`, `AsyncLazyCache.cs`, `ArtworkDownloader.cs`,
`ArtworkSignature.cs`, `TileImage.cs`, `PrimaryWidget.xaml.cs:332-611` all read fresh this loop) before
`CURRENT_REVIEW.md`'s prior verdict/scorecard and `REVIEW_HISTORY.md`'s tail were consulted for
delta/oscillation bookkeeping, per the dispatch's blind-critic ordering instruction.

**Dispatch framing for this loop**: loop 14 queued two backlog items — Priority 1 (add
`ArtworkSignatureTests.cs`, extract the `officialArtworkFloor` gate) and Priority 2 (extend F-009's
`AsyncLazyCache<T>` fix to `StoreNameLookup`'s three remaining per-key caches), with loop 14 itself flagging
that Priority 2 needed its own fresh Simplify Pressure Test rather than blind application, since the three
remaining caches are per-key maps, not the single-value shape `AsyncLazyCache<T>` models. Both were
genuinely investigated this loop. Priority 1 passed SPT cleanly and was built. Priority 2 did **not** pass a
fresh SPT as literally queued — see Finding F-010 — and was not built.

### Loop Counter

Loop 15 of 15

### System Flag

[STATE: HALT_LOOP_CAP]

---

## Contest Verdict

**Promising, but architecturally immature.**

Both gates green before and after (131/131 tests before, 138/138 after; MSBuild exit 0). This is the cap
loop of a 15-loop run: it closed `ArtworkSignature.cs`'s last named test gap (0/3 -> 3/3 members tested) and
extracted the `officialArtworkFloor` comparison into a second tested predicate, narrowing F-007 without
resolving it. A fresh Simplify Pressure Test on the standing extend-F-009-to-three-more-caches backlog item
found it does not cleanly pass as literally queued (F-010, new this loop), so it was not built. No scorecard
dimension moved this loop — an honest all-SAME loop, the third of this run's last five (11, 13, 15) to land
real work without moving a single dimension.

## Scorecard (1-10)

- **Architecture quality:** 7.0 | SAME | Re-read `LoadGameEntriesAsync` (`PrimaryWidget.xaml.cs:332-611`)
  directly this loop before any other work, confirmed byte-identical since loop 9's commit `05501e0` via
  `git diff --stat 05501e0 HEAD -- PrimaryWidget.xaml.cs` (empty). This loop's diff touches only
  `ArtworkDownloader.cs` and three test files, none of which are `PrimaryWidget.xaml.cs` — confirmed via
  `git diff --stat`.
- **State management and runtime ownership:** 7.0 | SAME | `AppliedArtworkStore.cs` and `FixLog.cs`
  production code confirmed byte-identical to HEAD this loop (`git diff --stat` shows only
  `ArtworkDownloader.cs` touched in production code). This loop's fix (`ArtworkSignature` test coverage,
  `ArtworkDownloader` predicate extraction) touches no mutable runtime state.
- **Domain modeling:** 5.5 | SAME | Not re-litigated this loop — no new evidence surfaced. Confirmed via
  `git diff --stat 85b5279 HEAD -- SteamGridDbGame.cs ArtworkSource.cs` (empty) that no source change exists
  to reopen loop 11's SPT-rejected construction-time-invariant question.
- **Data flow and dependency design:** 6.5 | SAME | Direct read of `StoreNameLookup.cs` this loop confirms
  `gogNameCache`/`epicNameCache`/`nameMatchCache` (`:29-34`) remain unlocked per-key `Dictionary` state,
  unchanged from loop 14. Genuinely investigated (not skipped) whether loop 14's queued "extend F-009's fix
  to the three remaining caches" backlog item now passes a fresh Simplify Pressure Test, per this loop's
  dispatch instructions: it does not, as literally specified — see Finding F-010 (new this loop). No
  source-level change against this residual landed this loop; the residual narrows in understanding (a
  correctly-scoped alternative is now named) but not in source. Score holds at 6.5, not credited UP for an
  investigation that produced a rejection, not a fix — per G8, no score increase without structural proof,
  and there is none here.
- **Framework / platform best practices:** 6.0 | SAME | Not re-litigated this loop — no new evidence
  surfaced. Confirmed via `git diff --stat 85b5279 HEAD -- SteamGridDbClient.cs` (empty) that no source
  change exists to reopen loop 11's SPT-rejected `DataContractJsonSerializer`/`Windows.Data.Json` split
  question.
- **Concurrency and runtime safety:** 6.5 | SAME | F-003's sequential per-game awaits
  (`PrimaryWidget.xaml.cs:562,584,593,602`) unaffected — `git diff --stat` confirms `PrimaryWidget.xaml.cs`
  untouched this loop. This loop's fix adds no new lock, no new `Task`, no new concurrent caller anywhere —
  pure test additions plus one predicate extraction in `ArtworkDownloader.cs`, confirmed via direct read of
  the diff.
- **Code simplicity and clarity:** 8.5 | SAME | This loop's production diff (`ArtworkDownloader.cs`,
  +14/-1) extracts one small named predicate mirroring the file's own `PassesColourAndLayoutGate` precedent
  exactly — real but not large enough to move this dimension on its own, and per this run's established
  anti-double-counting convention (loops 11, 13, 14) the credit for a small extraction paired with new
  tests goes to `test_strategy`, not simplicity. No ceremony added; the new `TestImages.cs` helper
  (`SolidColorPngAsync`) follows the file's existing one-purpose-per-factory-method pattern.
- **Test strategy and regression resistance:** 9.5 | SAME | Added `SteamGridDB.Xbox.Tests/
  ArtworkSignatureTests.cs` (5 tests: `CreateAsync` null-on-undecodable, `ColourMatch` self-match and
  disjoint-colour, `LayoutMatch` self-match and flat-image-guard) and 2 boundary tests for the new
  `ArtworkDownloader.ChosenAlreadyMatchesOfficialArt` predicate (138 total, up from 131). Independently
  mutation-verified: flipped `ChosenAlreadyMatchesOfficialArt`'s `>=` to `>` (failed exactly the new
  floor-boundary test, reverted); neutered `ArtworkSignature.cs`'s `if (deviation <= 0) deviation = 1;`
  guard (failed exactly the new flat-image `LayoutMatch` test, reverted). A third mutation was tried and
  honestly found NOT caught: removing `ColourHistogram`'s `histogram[i] /= magnitude` normalization line
  (`ArtworkSignature.cs:70`) left all 138 tests green — recorded as Finding F-007's residual, not hidden.
  The 9-anchor is still met (Authority-Map cross-check passes for every concern); this closes
  `ArtworkSignature.cs`'s prior zero-test-file gap entirely (0/3 -> 3/3 members tested).
- **Overall implementation credibility:** 7.5 | SAME | Consistent with this run's established
  anti-double-counting convention (loops 11, 13, 14 all held this dimension flat for test-only/pure-
  extraction fixes): this loop's fix is credited entirely to `test_strategy`. `PrimaryWidget.xaml.cs`'s
  1,950 lines remain unverified by anything but inspection outside the small tested slices.
  `StoreNameLookup`'s network-bound methods remain untested by direct test — unchanged this loop.

## Authority Map

Re-emitted this loop per G24 (mandatory whenever `test_strategy >= 9`) and because this loop's Priority-1
work touches the third concern's test coverage directly.

- **Concern:** Applied-artwork record (which SteamGridDB artwork ID was written to each tile).
  - **Owner:** `AppliedArtworkStore` (`Services/Artwork/AppliedArtworkStore.cs`).
  - **Allowed writers:** `SetAsync`, `ClearAsync` — both funnel through the private `UpdateAsync`, gated by
    the shared `SemaphoreSlim gate`.
  - **Readers:** `GetAsync` (also gated).
  - **Persistence seam:** `applied-artwork.json` in `RecordFolder` (defaults to `ApplicationData.Current.
    LocalFolder`).
  - **Async mutation entry points:** `SetAsync`, `ClearAsync`.
  - **Verdict:** Single and clear. Direct test: `AppliedArtworkStoreTests.cs`. Unaffected this loop.

- **Concern:** Fix-run diagnostic log (what happened during the last "fix library" pass).
  - **Owner:** `FixLog` (`Services/Artwork/FixLog.cs`).
  - **Allowed writers:** `Start`, `Write` — called from `PrimaryWidget` and `ArtworkDownloader.
    FindOfficialLookalikeAsync`.
  - **Readers:** none in-process; `SaveAsync` writes to disk for the user to inspect externally.
  - **Persistence seam:** `last-fix.log` / `last-load.log` in `LogFolder`.
  - **Async mutation entry points:** `SaveAsync`.
  - **Verdict:** Single and clear. Direct test: `FixLogTests.cs`. Unaffected this loop.

- **Concern:** Store-name lookup caches (GOG/Epic/Ubisoft names, SteamGridDB name-match results), the
  artwork download/selection gate, and the image-comparison signature it relies on.
  - **Owner:** `StoreNameLookup` (`Services/Stores/StoreNameLookup.cs`), `ArtworkDownloader` (`Services/
    Artwork/ArtworkDownloader.cs`), and `ArtworkSignature` (`Services/Artwork/ArtworkSignature.cs`).
  - **Allowed writers:** `StoreNameLookup.GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/
    `FindGameByNameAsync`/`GetUbisoftGameNameAsync`.
  - **Readers:** the same four `StoreNameLookup` writers (read-through cache).
  - **Persistence seam:** none — in-memory only, process lifetime.
  - **Async mutation entry points:** the four `StoreNameLookup` writers; `ArtworkDownloader.
    DownloadArtworkAsync`/`DownloadBestTileFillingImageAsync`/`FindOfficialLookalikeAsync`.
  - **Verdict:** Single and clear ownership. This loop closed `ArtworkSignature.cs`'s test gap entirely
    (`ArtworkSignatureTests.cs`, new) and added a second tested predicate to `ArtworkDownloader`
    (`ChosenAlreadyMatchesOfficialArt`). Three of four `StoreNameLookup` caches
    (`gogNameCache`/`epicNameCache`/`nameMatchCache`) remain unlocked per-key dictionaries — investigated
    fresh this loop (Finding F-010), the queued fix does not cleanly pass SPT as specified. Test gap
    narrowed: `ArtworkDownloader`'s three async entry points and `StoreNameLookup`'s four network-bound
    writers remain untested (Priority 1, F-007, narrowed not resolved).

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design (`Services/SteamGridDB/
  ArtworkSource.cs:15-51`) still makes "neither a platform-ID nor a game-ID" unrepresentable — unaffected
  this loop, confirmed via `git diff --stat`.
- This loop's own SPT investigation of the standing extend-F-009-to-three-caches backlog item found a real,
  honestly-reported problem with the previously-queued plan (it would either serialize four independent
  stores or hand-copy a lock idiom a third time) rather than building it on the strength of loop 14's
  precedent alone — the correct call given this loop's explicit instruction to test that plan fresh rather
  than apply it blindly.
- The mutation-verification discipline this run established (loop 10 onward) caught a real gap this loop
  even inside code the loop itself just wrote: `ColourHistogram`'s normalization step passed all 138 tests
  when neutered, and that honest negative result is recorded as a Finding rather than omitted.

## Findings

### Finding #1: ArtworkDownloader's fetch/orchestration entry points, StoreNameLookup's four writers, and ArtworkSignature's normalization step remain untested

**Why it matters** — `DownloadBestTileFillingImageAsync` decides which candidate image the widget applies
as artwork when auto-selecting. `ArtworkSignature.cs`'s `ColourMatch`/`LayoutMatch` now have direct tests
(closed this loop), but the async orchestration around them (the network fetch, the fallback-candidate
capture, `StoreNameLookup`'s four store-lookup writers) remains untested, and a specific mutation this loop
confirmed uncaught — removing `ColourHistogram`'s magnitude normalization — would silently change what
"same colour" means without any test noticing.

**What is wrong** — This loop added `SteamGridDB.Xbox.Tests/ArtworkSignatureTests.cs` (5 tests covering
`CreateAsync`/`ColourMatch`/`LayoutMatch`) and extracted plus tested `ArtworkDownloader.
ChosenAlreadyMatchesOfficialArt` (2 boundary tests), closing `ArtworkSignature.cs`'s prior zero-test-file
gap and the `officialArtworkFloor` mutation gap loop 14's backlog named. Independently verified
mutation-sensitive: flipping `ChosenAlreadyMatchesOfficialArt`'s `>=` to `>` failed exactly the new
floor-boundary test; neutering `ArtworkSignature.cs`'s `if (deviation <= 0) deviation = 1;` guard failed
exactly the new flat-image `LayoutMatch` test. A third mutation was also tried and found NOT caught:
removing `ColourHistogram`'s `histogram[i] /= magnitude` normalization (`ArtworkSignature.cs:70`) left all
138 tests green, because the disjoint-colour test's zero result holds with or without normalization
(disjoint one-hot buckets dot to zero either way) and the self-match test's `> 0.99` threshold is satisfied
by a much larger unnormalized value. `ArtworkDownloader.cs`'s three async entry points
(`DownloadArtworkAsync`, `DownloadBestTileFillingImageAsync`, `FindOfficialLookalikeAsync`) remain untested
as orchestration (network-bound, no seam — unchanged), as do `StoreNameLookup`'s four writers (unchanged,
per `TESTING.md`'s documented network boundary).

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/ArtworkSignature.cs:64-72`
- `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs:40,71,122`
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`
- `SteamGridDB.Xbox.Tests/ArtworkSignatureTests.cs`

**Architectural test failed** — Interface-as-test-surface — the remaining orchestration/fetch surface still
reaches past its Interface into a live, non-injectable network call.

**Dependency category** — `true-external`

**Leverage impact** — One call-site cluster (`ArtworkDownloader`'s replacement gate plus the signature
comparison it calls), the function every automatic artwork pick goes through after ranking.

**Locality impact** — Unaffected outside the newly-tested pure comparison slice; the remaining gap sits
entirely in the async orchestration layer.

**Metric signal** — `ArtworkSignature.cs`: 0 of 3 members tested before this loop, 3 of 3 after.
`ArtworkDownloader`: 1 of 2 pure predicates untested before (`officialArtworkFloor`), 0 of 2 after.
`ArtworkDownloader`'s 3 async entry points and `StoreNameLookup`'s 4 writers: still 0 tested, unchanged.

**Why this weakens submission** — A source-level mutation in the download loop's fallback-candidate
capture, the `MaxCandidates`/`chosenIndex + 1` loop boundaries, `StoreNameLookup`'s four writers, or
`ColourHistogram`'s normalization step would pass the entire suite undetected today.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Add a direct test for `ColourHistogram`'s normalization (e.g. two images with
the same palette in different proportions should score below 1.0, which only holds when histograms are
normalized to unit length before the dot product) — needs no new fixture beyond what
`ArtworkSignatureTests.cs` already has. Separately, `ArtworkDownloader`'s three async entry points and
`StoreNameLookup`'s four writers remain blocked by `TESTING.md`'s documented network boundary; no seam has
been proven to justify one (see F-003's standing operational constraint).

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/ArtworkSignatureTests.cs`. Avoid:
`SteamGridDB.Xbox/Services/Stores/`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

---

### Finding #2: PrimaryWidget.xaml.cs still merges UI event handling, image decode, network resolution, and bulk-operation orchestration behind zero Interface boundary

**Why it matters** — The largest file in the repo across every prior loop's Discovery continues to bundle
several structurally distinct concerns with no Module boundary between most of them, so a change to any one
risks disturbing the others.

**What is wrong** — Re-read `LoadGameEntriesAsync` directly this loop (`PrimaryWidget.xaml.cs:332-611`, the
first 280 lines read in full this loop rather than trusted from git diff alone), confirming the same merge
loop 14 found: the image-resolution block (`:480-538`: `imageFilePath`/`imageFolder` computation,
`ArtworkFiles.HasBackupAsync`, `CreateThumbnailAsync`) and the network-resolution block (`:551-629`: the
SteamGridDB lookup plus the GOG/Epic/Ubisoft/name-search fallbacks) are both still I/O-heavy with no
decision logic ahead of the fetch to extract — the same shape that has correctly kept `ArtworkDownloader.
DownloadArtworkAsync` itself out of every extraction attempted this run. This loop's own diff touches
`ArtworkDownloader.cs`, `ArtworkDownloaderTests.cs`, `TestImages.cs`, and the new
`ArtworkSignatureTests.cs` only; confirmed via `git diff --stat 05501e0 HEAD -- PrimaryWidget.xaml.cs`
(empty) that the file has been byte-identical for six loops running (9 through 15).

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-611`
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:480-538`
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:551-629`

**Architectural test failed** — n/a — different category (ownership/coupling sprawl for what remains; the
candidate extractions were rejected via Simplify Pressure Test Q2, not an architectural test on an existing
Module).

**Dependency category** — n/a

**Leverage impact** — Unaffected this loop.

**Locality impact** — Unaffected this loop.

**Metric signal** — `PrimaryWidget.xaml.cs`: 1,950 lines, unchanged.

**Why this weakens submission** — Ownership of the concerns still merged in `PrimaryWidget` remains
untraceable from any single Module besides the UI class itself.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — No further split is queued this loop: both candidate blocks are I/O-only with
no separable decision logic, re-confirmed by direct read this loop rather than inherited from the standing
text. Consistent with this run's discipline since loop 8: no next slice is proposed without first
re-verifying against current source and passing Simplify Pressure Test.

**Blast radius** — Change (only if a future loop finds a genuinely separable slice, with a fresh SPT first):
`PrimaryWidget.xaml.cs`. Avoid: `Services/Artwork/*`, `Services/Stores/*`, `Services/SteamGridDB/*`,
`Services/Library/*`.

---

### Finding #3: StoreNameLookup's three remaining per-key caches stay unlocked; the queued single-shared-semaphore fix would either duplicate a hand-rolled lock idiom a third time or serialize four logically-independent stores against each other

**Why it matters** — `gogNameCache`/`epicNameCache`/`nameMatchCache` are shared static `Dictionary` state
with zero synchronization, currently safe only because the single sequential per-game `foreach` never calls
them concurrently (the same accidental-safety shape F-009 fixed for the Ubisoft cache). Loop 14 queued
mirroring `AppliedArtworkStore`'s gate-around-every-access idiom as the next step without re-testing it, and
this loop's fresh Simplify Pressure Test finds that literal plan does not cleanly pass.

**What is wrong** — Direct read of current `StoreNameLookup.cs` this loop (`gogNameCache`/`epicNameCache`/
`nameMatchCache`, `:29-34`) confirms all three remain plain unlocked `Dictionary` fields read/written via
bare `TryGetValue`/assignment in `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/`FindGameByNameAsync`.
Two ways to apply loop 14's queued remedy were considered: (a) wrap each method's cache access with the
same `gate` `SemaphoreSlim` loop 14 introduced for the Ubisoft cache — but `gate` is one lock shared by four
logically-independent stores (GOG, Epic, name-match, Ubisoft), so holding it for a GOG network fetch would
block an unrelated Epic lookup even though today's single-sequential-caller pattern makes this invisible;
that is an artificial bottleneck engineered into the design for no live caller currently exercising it, not
a currently-needed fix. (b) give each cache its own dedicated semaphore — avoids the cross-store
bottleneck, but is three more hand-copies of the check-then-populate-under-lock shape
(`AppliedArtworkStore.GetAsync`/`UpdateAsync` already has one, `AsyncLazyCache<T>` is a fourth in-house
implementation of the same lock discipline for a different shape) — this is the exact
hand-rolled-duplicate-of-a-shape-the-codebase-already-generalizes smell F-009 fixed, reintroduced three
more times instead of extracted once. Neither literal option is the smallest honest fix as queued; a
shared, extracted per-key lock wrapper (used by both `StoreNameLookup`'s three caches and
`AppliedArtworkStore`'s one) is the more defensible shape but is a bigger, un-friction-proven redesign, not
this loop's "extend F-009" framing.

**Evidence**
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:29-34`
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:95-110`
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:212-231`
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:123-155`
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:63-84`

**Architectural test failed** — n/a (a Simplify-Pressure-Test rejection of a queued candidate fix, not an
architectural test on an existing Module).

**Dependency category** — n/a

**Leverage impact** — Three call sites in one file; a naive per-cache lock copy would triple this loop's
Ubisoft-cache lock pattern rather than reduce the number of locking idioms in the codebase.

**Locality impact** — Confined to `StoreNameLookup.cs` if attempted narrowly; a proper extraction would
also touch `AppliedArtworkStore.cs`.

**Metric signal** — 3 of 4 `StoreNameLookup` static caches remain unlocked (unchanged this loop);
`AppliedArtworkStore.cs` independently hand-rolls the same gate-around-every-access idiom this backlog item
would copy a third and fourth time.

**Why this weakens submission** — Confirms `data_flow`'s residual narrows only as far as loop 14 already
took it; attempting the queued remedy literally as described would trade one smell (unlocked per-key state)
for another (duplicated lock idiom or unnecessary cross-store serialization), which is why it was not built
this loop despite being available.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Do not copy `AppliedArtworkStore`'s gate-around-every-access idiom three more
times by hand. If pursued, extract a small in-process type generalizing "gate-guarded dictionary" (used by
`AppliedArtworkStore`'s one map AND `StoreNameLookup`'s three), giving each cache/map its own semaphore
instance rather than sharing one — this is a two-real-call-site internal reuse, not a Seam requiring Unified
Seam Policy justification (nothing is injected or swapped at runtime). Re-run Simplify Pressure Test on that
narrower, correctly-scoped proposal before building; do not build the single-shared-gate or
three-more-hand-copies version this backlog item originally named.

**Blast radius** — Change: `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`,
`SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs` (only if the shared extraction is built). Avoid:
`SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs`.

## Simplification Check

- **Structurally necessary:** Extracting `ArtworkDownloader.ChosenAlreadyMatchesOfficialArt` from the
  inline `chosenMatch >= officialArtworkFloor` check — passes the same test loop 13's
  `PassesColourAndLayoutGate` extraction did: the comparison needed a test surface that async orchestration
  code cannot cheaply provide, and the extraction is a direct mirror of an already-accepted pattern in the
  same file.
- **New seam justified:** false — no new Seam is introduced; a pure predicate extraction and new test file
  only.
- **Helpful simplification:** None claimed as simplification — this loop's work is purely additive (new
  test file, new test fixture helper, one small predicate extraction) and is credited entirely to
  `test_strategy` per this run's anti-double-counting convention.
- **Should NOT be done:** Building F-010's queued fix (locking `StoreNameLookup`'s three remaining per-key
  caches) this loop, on the strength of loop 14's precedent alone — this loop's fresh SPT investigation
  found the literal plan does not pass (see Finding F-010); building it anyway would have landed either
  unnecessary cross-store lock contention or a third hand-copy of an existing lock idiom. Also not
  attempted: further splitting `LoadGameEntriesAsync` (re-investigated fresh this loop, still fails SPT —
  see Finding F-001), or re-litigating `domain_modeling` / `framework_idioms` without new evidence (none
  surfaced this loop; both remain flat since loop 11's SPT rejection).
- **Tests after fix:** New test file `SteamGridDB.Xbox.Tests/ArtworkSignatureTests.cs` (5 tests) covers
  `ArtworkSignature.CreateAsync`/`ColourMatch`/`LayoutMatch` at their real public Interface — no old tests
  existed to delete since `ArtworkSignature.cs` had zero test coverage before this loop. Two new boundary
  tests in `ArtworkDownloaderTests.cs` cover the newly-extracted `ChosenAlreadyMatchesOfficialArt`
  predicate, mirroring `PassesColourAndLayoutGate`'s existing test shape in the same file (Replace-don't-
  layer satisfied: the old inline comparison had no test to delete, so there is nothing left at a
  shallower level).

## Improvement Backlog

1. **Add a direct test for `ColourHistogram`'s magnitude-normalization step (F-007, narrowed this loop)** —
   this loop's own mutation testing found removing the `/= magnitude` normalization line in
   `ArtworkSignature.cs`'s `ColourHistogram` is currently uncaught by any test — needs no new fixture beyond
   what `ArtworkSignatureTests.cs` already has (e.g. compare two images with the same palette in different
   proportions, which only scores below 1.0 when normalized).
   - Why it matters: closes the last named mutation gap inside code this loop already touched.
   - Score impact: `test_strategy`'s residual narrows further; does not by itself reach 10 (`ArtworkDownloader`'s
     async orchestration and `StoreNameLookup`'s writers remain the network-boundary gap).
2. **Extract a shared gate-guarded-dictionary type for `StoreNameLookup`'s three remaining per-key caches
   and `AppliedArtworkStore`'s map (F-010, new this loop, refines loop 14's backlog item)** — these three
   caches remain unlocked; a fresh SPT this loop rejected the literal "mirror `AppliedArtworkStore`'s idiom
   by hand three more times" plan (duplicate lock idiom) and the "reuse the single shared gate" plan
   (unneeded cross-store serialization). The correctly-scoped version is a small internal type with
   per-cache semaphores, reused at `StoreNameLookup`'s three call sites AND `AppliedArtworkStore`'s one —
   bigger blast radius than item 1, needs its own SPT pass before building.
   - Score impact: `data_flow` residual narrows further toward the 7-anchor if it lands cleanly.
3. **Bound concurrency in `LoadGameEntriesAsync`'s per-game SteamGridDB lookups (F-003)** — blocked for the
   duration of this run by the standing operational constraint.
   - Why it matters: removes load latency scaling linearly with library size, whenever the constraint is
     lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

- **Candidate Module:** A shared gate-guarded-dictionary type generalizing `StoreNameLookup`'s three
  remaining per-key caches and `AppliedArtworkStore`'s one map.
- **Source friction proven:** Finding F-010 — this loop's fresh SPT investigation of extending F-009's fix
  found both literal options (one shared lock across four stores, or three more hand-copies of
  `AppliedArtworkStore`'s idiom) fail Simplify Pressure Test Q3 (avoid duplicate layers); a shared internal
  type reused at both `StoreNameLookup` and `AppliedArtworkStore`'s call sites is the version that would not
  duplicate anything, but that friction (a real second call site for internal reuse, distinct from a Seam)
  was only proven this loop.
- **Why the current Interface is shallow or misplaced:** n/a in the Deletion-test sense — not pass-through
  wrappers. The friction is Ownership & State: shared static dictionaries with zero synchronization
  discipline, and `AppliedArtworkStore`'s own hand-rolled gate-around-every-access idiom already exists once
  with no shared abstraction backing it.
- **Behavior to move behind the deeper Interface:** n/a — not a Seam question (internal reuse, nothing
  injected or swapped at runtime). The candidate change is a small generic type owning one `SemaphoreSlim`
  plus one `Dictionary`, exposing gated Get/Set, reused at `StoreNameLookup`'s three sites and
  `AppliedArtworkStore`'s one.
- **Dependency category:** `in-process`.
- **Test surface after the change:** `StoreNameLookup`'s three call sites remain network-bound with no seam
  (unaffected). `AppliedArtworkStore`'s existing tests (`AppliedArtworkStoreTests.cs`) would need to keep
  passing unchanged against the new shared type — a genuine Replace-don't-layer test if built.
- **Smallest first step:** Prototype the shared type against `AppliedArtworkStore` alone first (its existing
  tests are the oracle for correctness), then wire `StoreNameLookup`'s `gogNameCache` to it as the first of
  the three per-key caches, proving the pattern before repeating for `epicNameCache` and `nameMatchCache`.
- **What not to do:** Do not build this by copying `AppliedArtworkStore`'s gate-around-every-access idiom
  into `StoreNameLookup` by hand three times — that is the exact duplicate-hand-rolled-lock smell F-009
  fixed, reintroduced. Do not share one `SemaphoreSlim` across all three caches plus the Ubisoft cache —
  that serializes four logically-independent stores for no live benefit today.

## Builder Notes

1. **Pattern:** A backlog item queued by a prior loop as "extend fix X to the remaining N instances" can
   look like a drop-in repeat of the fix that just worked, when the remaining instances are actually a
   different shape that the same remedy does not fit.
   - How to recognize: the prior loop's fix consolidated a single-value lazy-load into an existing generic
     type; the remaining instances are per-key dictionaries, not single values — the generic type that fit
     the first shape (`AsyncLazyCache<T>`) has no equivalent for the second shape (a locked per-key map), so
     "just do the same thing again" silently changes what "the same thing" means.
   - Smallest coding rule: before building a queued "extend the fix" backlog item, re-read the actual data
     shape of the remaining instances (single value vs. collection, one caller vs. several) and re-run the
     Simplify Pressure Test against the specific mechanism the extension would require — do not assume the
     prior loop's SPT pass transfers.
   - Stack example: C# — loop 14 fixed `StoreNameLookup.ubisoftGameLookupCache` (a single
     `Dictionary<string,string>` loaded once) by reusing `AsyncLazyCache<T>`. The three remaining caches
     (`gogNameCache`/`epicNameCache`/`nameMatchCache`) are per-key dictionaries with many independent
     reads/writes, not one load — `AsyncLazyCache<T>` does not generalize to that shape, and the naive "lock
     it like the others" plan either shares one lock across unrelated stores or hand-copies a different
     existing pattern (`AppliedArtworkStore`'s) three more times.

2. **Pattern:** A single shared lock protecting several logically-independent resources is invisible as a
   cost when there is only ever one caller at a time, and becomes a real, silent bottleneck the moment
   concurrency is introduced — the exact bug a later loop would have to rediscover from scratch if it were
   built now without comment.
   - How to recognize: one `SemaphoreSlim`/mutex field, multiple unrelated cached values or subsystems
     reaching for it (four different third-party stores in this codebase's case) — ask whether operations
     on resource A ever need to wait behind an in-flight operation on unrelated resource B, and whether that
     answer changes once the code's one sequential caller is replaced with several concurrent ones.
   - Smallest coding rule: give logically-independent resources their own lock instances, even if it means
     one more field per resource, rather than reusing a single shared one for convenience — the cost is a
     few extra `SemaphoreSlim` allocations; the alternative is a hidden coupling that only shows up as a
     performance regression later, far from the line that introduced it.
   - Stack example: C# — reusing `StoreNameLookup.cs`'s single `gate` `SemaphoreSlim` (introduced in loop
     14 for the Ubisoft cache) to also guard the GOG, Epic, and name-match caches would make a slow GOG
     network fetch block an unrelated Epic lookup, even though the two share nothing but the same file.

3. **Pattern:** Mutation testing a brand-new test file can surface a real gap even inside the code the same
   loop just wrote — "I added tests for X" is not the same claim as "every mutation of X is now caught," and
   the two should not be conflated in the scorecard.
   - How to recognize: a numeric comparison or normalization step feeding into a fixed-threshold decision
     (here: `ColourMatch`'s histogram normalization feeding into the 0.60/0.85 official-artwork thresholds)
     where a test asserts a specific value (0.0, > 0.99) that would still hold even if the normalization
     step were silently removed, because the test's fixture happens to make normalized and unnormalized
     results indistinguishable at that specific assertion.
   - Smallest coding rule: after writing a new test file, deliberately try to break each mechanism the
     tests are supposed to protect (not just the mechanisms the tests obviously target) by hand-editing the
     source and re-running — a test suite that would pass with a mutation still live is not proof against
     that mutation, no matter how many assertions surround it.
   - Stack example: C# — `ArtworkSignatureTests.cs`'s disjoint-colour test (`Assert.Equal(0.0, ...)`) and
     self-match test (`Assert.True(... > 0.99)`) both still pass with `ColourHistogram`'s magnitude
     normalization deleted entirely, because a zero dot product stays zero regardless of scaling and a
     large unnormalized self-dot-product still clears 0.99 — the test file closes `ArtworkSignature.cs`'s
     zero-coverage gap without closing every mutation gap inside it.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. Scoring `data_flow` as SAME (not DOWN, not UP) after Finding F-010's SPT rejection — a stricter reviewer
   could argue that spending real loop effort investigating and rejecting a fix, without landing anything,
   deserves neither UP (no structural proof, correctly held per G8) nor necessarily a routine SAME — a
   confirmed-harder-than-thought residual is arguably a different fact than an untouched one, even if the
   number is mechanically identical. I judged SAME is correct because no source changed and the rubric has
   no "residual got harder" gradation, but a stricter reader could want that distinction surfaced somewhere
   other than prose.
2. Assigning Finding F-010 a NEW stable_id rather than treating it as a continuation of F-009 (resolved
   last loop) — the fuzzy-match rules (title cosine similarity, or same-file/same-severity/nearby-lines) are
   a close call given both findings share `StoreNameLookup.cs` and a "hand-rolled duplicate lock" framing; a
   stricter application of the title-similarity test could argue this should have reopened F-009 rather
   than minting F-010.
3. Declining to build the F-010 "shared gate-guarded-dictionary type" deepening candidate this loop, on the
   grounds it is a bigger, un-friction-proven redesign than this loop's mandate — a less conservative
   reviewer might argue the friction IS already proven (two failed literal alternatives were characterized
   in detail this loop) and that building the smallest version (just `AppliedArtworkStore` +
   `gogNameCache`) this loop, rather than only naming it as a Deepening Candidate, would have been the more
   decisive "smallest complete fix."

## Final Judge Narrative

Place, not win, and the cap loop should say so plainly rather than round up. Ground truth was clean going in
(131/131 tests, MSBuild exit 0, zero source drift since loop 14's commit) and clean coming out (138/138
tests, MSBuild exit 0). This loop closed `ArtworkSignature.cs`'s last named test gap and extracted a second
tested predicate in `ArtworkDownloader.cs`, both real, reviewer-approved, mutation-verified work — but it
moved zero scorecard dimensions, because `test_strategy` was already sitting at its 9.5 ceiling and this
run's own anti-double-counting convention (established loops 11, 13, 14) correctly declines to credit a
small pure extraction to simplicity or credibility on top of that. This is the third of the run's last five
loops (11, 13, 15) to land real work while moving nothing on the scorecard, and the pattern this run's
dispatch asked to be named plainly holds: `test_strategy` and `simplicity` have absorbed the large majority
of this run's fixing energy, while `domain_modeling` and `framework_idioms` have not moved once in 15 loops
and `architecture_quality` has been flat for six loops running. This loop's own investigation of the
standing `data_flow` backlog item (extending F-009's cache fix to three more caches) is honest, not evasive,
about why it did not build that item: a fresh Simplify Pressure Test found the literal plan would trade one
smell for another, and a smaller, lower-risk, already-well-scoped test-coverage win was available and taken
instead — which is itself the shape of the imbalance, made concrete in this loop's own choice. Runtime
ownership and concurrency are exactly as trustworthy as loop 14 left them; this loop's diff touches no
mutable state and crosses no risk boundary (confirmed: pure additions and one pure-function extraction, no
isolation/concurrency/visibility change). Tests genuinely improved this loop, and improved honestly — a
mutation was tried and found NOT caught (`ColourHistogram`'s normalization), and that negative result is
recorded as a Finding rather than smoothed over. Future work risks nothing new from overengineering this
loop; it does risk more of the same imbalance if the next invocation defaults to whichever backlog item is
cheapest and safest, the same choice this loop made, rather than deliberately spending a loop on the two
dimensions that have never moved.

## Loop 15 Result

Extracted `ArtworkDownloader`'s inline `chosenMatch >= officialArtworkFloor` comparison into a named, tested
predicate `ChosenAlreadyMatchesOfficialArt` (`SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs`,
+14/-1), mirroring the file's existing `PassesColourAndLayoutGate` pattern. Added
`SteamGridDB.Xbox.Tests/ArtworkSignatureTests.cs` (new file, 5 tests) covering `ArtworkSignature.
CreateAsync`/`ColourMatch`/`LayoutMatch` — previously zero test coverage. Added 2 boundary tests for the new
predicate to `ArtworkDownloaderTests.cs`, and one new fixture helper (`SolidColorPngAsync`) to
`TestImages.cs`. Closes finding F-007's `ArtworkSignature.cs` slice (0/3 -> 3/3 tested) and its
`officialArtworkFloor` mutation gap; narrows but does not resolve F-007 overall (`ArtworkDownloader`'s async
orchestration and `StoreNameLookup`'s writers remain untested by design). No production behavior change
anywhere — `ChosenAlreadyMatchesOfficialArt` is a pure refactor of an existing comparison, not a new
decision.

**What proves the change is honest:** `run-tests.ps1`: 131 passed before, 138 passed after (7 new tests, 0
removed, 0 failed). MSBuild: exit 0, both runs (x64 Debug, `AppxBundle=Never`). `git diff --stat` confirms
exactly 4 files touched: `ArtworkDownloader.cs` (production), `ArtworkDownloaderTests.cs`, `TestImages.cs`,
and the new `ArtworkSignatureTests.cs` (all test files). Independently mutation-verified 3 distinct
assertions by hand-editing source, re-running, and reverting: (1) `ChosenAlreadyMatchesOfficialArt`'s `>=`
flipped to `>` — exactly 1 test failed (the new floor-boundary test), reverted, 138/138 green; (2)
`ArtworkSignature.cs`'s `if (deviation <= 0) deviation = 1;` guard neutered — exactly 1 test failed (the new
flat-image `LayoutMatch` test), reverted, 138/138 green; (3) `ColourHistogram`'s `/= magnitude`
normalization removed — 0 tests failed (honestly reported as an uncaught gap, not concealed), reverted,
138/138 green. `git status --short` confirms the working tree matches exactly the 4 predicted files after
the final revert, nothing else touched.

**Risk boundary evidence (Meta-Rule 4):** none — this loop's diff is a pure-function extraction (no isolation,
Sendable-equivalent, conditional-compilation, cross-file-visibility, or lock-ordering change) plus new test
files. `risk_boundary_evidence` is null.

**Targeted finding status:** `carried_forward` — F-007's Claim (untested async/orchestration surfaces
around the artwork-comparison gate) narrows (`ArtworkSignature.cs`'s slice is now fully closed) but is not
fully gone from current source: `ArtworkDownloader`'s three async entry points and `StoreNameLookup`'s four
writers remain untested by design.

**Unintended scorecard regression:** none. All nine dimensions held SAME with fresh structural
re-derivation this loop; no dimension regressed.

## Loop 15 Implementation Review

`verdict: approved` — "Reality: `officialArtworkFloor`'s inline `chosenMatch >= officialArtworkFloor`
comparison (`ArtworkDownloader.cs:146`, single call site confirmed by grep) now routes through named,
directly-tested `ChosenAlreadyMatchesOfficialArt`, and `ArtworkSignature.cs`'s `ColourMatch`/`LayoutMatch`/
`CreateAsync` now have direct tests in the new `ArtworkSignatureTests.cs`; Honesty: the extraction mirrors
the file's own `PassesColourAndLayoutGate` precedent exactly (same internal static shape, same doc-comment
style), `SolidColorPngAsync` is a minimal reuse of the existing `FromPixelsAsync` fixture helper consistent
with `OpaquePngAsync`/`PngWithTransparentCornersAsync`, no new seam/protocol was introduced so Unified Seam
Policy is not triggered, and the disclosed mutation-coverage gap (unnormalized `ColourHistogram`) is
verified plausible by hand: the disjoint-colour test's `Assert.Equal(0.0,...)` holds regardless of
normalization because red/blue occupy non-overlapping histogram buckets so the dot product is exactly zero
either way, and the self-match test's `>0.99` threshold is trivially satisfied by an unnormalized
sum-of-squares that is far larger than 1; Regression: the two new boundary tests (0.60 true / 0.59 false)
correctly bracket the `>=` operator so a boundary-flip mutation would be caught, and the diff crosses no
risk boundary — both `ArtworkDownloader.cs` changes stay internal static within the same file with no
isolation/concurrency/visibility change, confirmed by reading the diff." All three checks (`reality`,
`honesty`, `regression`) `passed`; `conditions: []`; `regressions: []`.
