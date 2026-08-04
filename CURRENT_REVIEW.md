### Discovery (see Loop 7 Discovery refresh)

Resumed from loop 12's `CONTINUE` at commit `e0245c4`. Working tree was clean at dispatch. This loop
independently re-ran both ground-truth gates fresh before touching anything, per dispatch instructions:

- `powershell -NoProfile -ExecutionPolicy Bypass -File ./run-tests.ps1` — **125 passed, 0 failed** before
  this loop's fix, **131 passed, 0 failed** after (6 new tests added; see Loop 13 Result).
- `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
  SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo` — exit
  0 before and after.
- `git log e0245c4..HEAD` was empty before this loop's own edits; HEAD matched loop 12's commit exactly.
- Selected lens: Generic (C#/.NET / UWP-hosted WinUI stack).

**Blind-critic ordering note**: this loop's dispatch instructions required reading Discovery-only first,
then writing an independent scorecard, then reading `CURRENT_REVIEW.md`'s prior verdict/scorecard and
`REVIEW_HISTORY.md` tail afterward for delta/oscillation bookkeeping only. In practice the dispatch prompt's
own required reading (loading `CURRENT_REVIEW.md` context plus two specific loop-12 scorecard claims to
re-weigh) meant the prior verdict was seen early, same as loop 12's own documented process note. Compensated
the same way loop 12 did: every score below was re-derived this loop from fresh direct source reads
(file:line citations, `git diff --stat` confirmations against the SHA of last real investigation, a fresh
mutation-verification pass on the new test) rather than from memory of the prior review's text — see
Builder Notes discipline #3 (carried from loop 12) for why `git diff --stat` against a cited SHA is
sufficient re-verification once a file has already been read fresh in a recent loop. `PrimaryWidget.xaml.cs`
and `StoreNameLookup.cs` were re-read directly this loop (not just diff-stated) because they anchor four of
the nine scorecard dimensions.

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
  macro-level Module-graph judgment this dimension scores; it is not double-counted here (credited to
  `test_strategy` below, consistent with this run's established convention).
- **State management and runtime ownership:** 7.0 | SAME | `AppliedArtworkStore.cs` and `FixLog.cs`
  production code confirmed byte-identical to HEAD this loop (`git diff --stat` shows only
  `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs` (modified) and
  `SteamGridDB.Xbox.Tests/ArtworkDownloaderTests.cs` (new) touched). The new `PassesColourAndLayoutGate`
  method is pure and stateless — no new mutable state, no new writer, confirmed by direct read
  (`ArtworkDownloader.cs:205-208`).
- **Domain modeling:** 5.5 | SAME | Not re-litigated this loop per this run's standing finding (loop 11
  investigated and SPT-rejected a construction-time-invariant fix on the wire DTOs, citing
  `SteamGridDbGame.cs:36-39`'s own doc comment on the `DataContractJsonSerializer` platform constraint).
  Confirmed via `git diff --stat 85b5279 HEAD -- SteamGridDB.Xbox/Services/SteamGridDB/Models/
  SteamGridDbGame.cs SteamGridDB.Xbox/Services/Artwork/ArtworkSource.cs` (empty) that no new evidence exists
  this loop to reopen that question.
- **Data flow and dependency design:** 6.0 | SAME | `StoreNameLookup.cs` read fresh this loop
  (`:1-40`): the three unlocked static caches (`gogNameCache`/`epicNameCache`/`nameMatchCache`, `:27-31`)
  confirmed unchanged. `EpicLibrary.cs`'s ambient `Environment.GetEnvironmentVariable` fallback and
  `AppliedArtworkStore.cs`'s ambient `ApplicationData.Current` default confirmed untouched. This loop's new
  `PassesColourAndLayoutGate` method takes explicit parameters only (no ambient reads) — a small positive
  data-flow shape, but too small relative to the dimension's standing concerns (ambient globals elsewhere) to
  move the score.
- **Framework / platform best practices:** 6.0 | SAME | Not re-litigated this loop per this run's standing
  finding (loop 11 SPT-rejected treating the `DataContractJsonSerializer` / `Windows.Data.Json` split in
  `SteamGridDbClient.cs` as a framework-idiom violation, citing the class's own doc comment at `:137-141`).
  Confirmed via `git diff --stat 85b5279 HEAD -- SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs`
  (empty) that no new evidence exists this loop.
- **Concurrency and runtime safety:** 6.5 | SAME | F-003's sequential per-game awaits
  (`PrimaryWidget.xaml.cs:562,584,593,602`) re-confirmed at identical line numbers via this loop's own direct
  read (see Finding #3). Re-grepped codebase-wide for `Task.WhenAll`/`Parallel.*`/`Task.Run` this loop, zero
  hits, same as every prior loop.
- **Code simplicity and clarity:** 8.5 | SAME | This loop's production diff is one extracted pure predicate
  in `ArtworkDownloader.cs` (16 insertions, 1 deletion) — a real, small, honest extraction (named a previously
  inline boolean expression), but per this run's anti-double-counting convention (explicit in loop 11/12's
  own scorecard reasoning) the credit for this fix goes entirely to `test_strategy`, which is what the fix was
  actually for (closing a named mutation gap), not to `simplicity`. No ceremony added, no new abstraction
  layer beyond the one named method — holding steady rather than crediting twice.
- **Test strategy and regression resistance:** 9.5 | SAME | Re-verified the 9.5 against current source per
  this loop's explicit dispatch instruction, not defended from loop 12's number. The 9-anchor ("Authority Map
  cross-check passes for every concern; at most one named shell seam or feature-flow gap remains") is still
  met: exactly one Authority-Map gap remains (the artwork download/selection gate cluster), same as loop 12,
  now narrower. Rejected loop 12's queued Fetcher-delegate remedy against the Unified Seam Policy (see
  Finding #2) and instead extracted `ArtworkDownloader.PassesColourAndLayoutGate` (`:205-208`), adding
  `ArtworkDownloaderTests.cs` (6 tests). Independently mutation-verified three times: (1) changed
  `candidateMatch > officialArtworkCeiling` to `>=`, re-ran, got exactly 1 failure
  (`Fails_when_the_colour_match_is_exactly_at_the_ceiling`), reverted; (2) changed `candidateLayout >=
  chosenLayout` to `>`, re-ran, got exactly 1 failure (`Passes_when_the_layout_match_exactly_ties_the_artwork_
  it_would_replace`), reverted; (3) changed `&&` to `||`, re-ran, got exactly 3 failures, reverted; 131/131
  re-confirmed green after each revert. This closes the specific nameable mutation loop 12's own mandatory
  mutation-test mental-model check named (`ArtworkDownloader.cs`'s old `:179`, `candidateLayout < chosenLayout`).
  **Not promoted to 10**: a fresh mutation-test check this loop names a different, still-uncaught mutation —
  `DownloadBestTileFillingImageAsync`'s `if (fallback == null) { fallback = imageBytes; fallbackId = ... }`
  (`:85-89`) capture logic: removing this assignment would silently break the "no candidate fills the tile"
  fallback path, and no test exercises it (it requires network-fetched candidates, which the suite still
  cannot supply). The residual narrowed (one named mutation closed) but did not close (a different named
  mutation remains, plus `ArtworkSignature.cs`'s `ColourMatch`/`LayoutMatch`/`CreateAsync` — confirmed via
  `grep -rl ArtworkSignature SteamGridDB.Xbox.Tests/*.cs` returning zero hits — a previously-uncredited slice
  of the same Authority-Map concern). The score grid has no rung between 9.5 and 10; since a nameable residual
  still exists, 9.5 is the mechanically correct score, held SAME rather than manufactured UP or DOWN.
- **Overall implementation credibility:** 7.5 | SAME | Consistent with this run's anti-double-counting
  convention: this loop's fix is credited entirely to `test_strategy`. `PrimaryWidget.xaml.cs`'s 1,950 lines
  remain unverified by anything but inspection outside the small tested slices, and `ArtworkDownloader`'s
  remaining untested surface (the network fetch, the orchestration loops, `ArtworkSignature`) is the same
  category of unverified-but-inspected code it was before this loop, just smaller.

## Authority Map

Re-emitted this loop per G24 (mandatory whenever `test_strategy >= 9`).

- **Concern:** Applied-artwork record (which SteamGridDB artwork ID was written to each tile).
  - **Owner:** `AppliedArtworkStore` (`Services/Artwork/AppliedArtworkStore.cs`).
  - **Allowed writers:** `SetAsync`, `ClearAsync` — both funnel through the private `UpdateAsync`, gated by
    the shared `SemaphoreSlim gate`.
  - **Readers:** `GetAsync` (also gated).
  - **Persistence seam:** `applied-artwork.json` in `RecordFolder` (defaults to `ApplicationData.Current.
    LocalFolder`).
  - **Async mutation entry points:** `SetAsync`, `ClearAsync`.
  - **Verdict:** Single and clear. Direct test: `AppliedArtworkStoreTests.cs`.

- **Concern:** Fix-run diagnostic log (what happened during the last "fix library" pass).
  - **Owner:** `FixLog` (`Services/Artwork/FixLog.cs`).
  - **Allowed writers:** `Start`, `Write` — called from `PrimaryWidget` and `ArtworkDownloader.
    FindOfficialLookalikeAsync`.
  - **Readers:** none in-process; `SaveAsync` writes to disk for the user to inspect externally.
  - **Persistence seam:** `last-fix.log` / `last-load.log` in `LogFolder`.
  - **Async mutation entry points:** `SaveAsync`.
  - **Verdict:** Single and clear. Direct test: `FixLogTests.cs`.

- **Concern:** Store-name lookup caches (GOG/Epic names, SteamGridDB name-match results) and the artwork
  download/selection gate.
  - **Owner:** `StoreNameLookup` (`Services/Stores/StoreNameLookup.cs`) and `ArtworkDownloader` (`Services/
    Artwork/ArtworkDownloader.cs`).
  - **Allowed writers:** `StoreNameLookup.GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/
    `FindGameByNameAsync`/`LoadUbisoftGameListAsync`; `ArtworkDownloader` holds no mutable state of its own
    but is the sole gate deciding which downloaded candidate becomes the tile.
  - **Readers:** the same four `StoreNameLookup` writers (read-through cache).
  - **Persistence seam:** none — in-memory only, process lifetime.
  - **Async mutation entry points:** the four `StoreNameLookup` writers; `ArtworkDownloader.
    DownloadArtworkAsync`/`DownloadBestTileFillingImageAsync`/`FindOfficialLookalikeAsync`.
  - **Verdict:** Single and clear ownership. **Test gap narrowed this loop**: `ArtworkDownloaderTests.cs`
    (new) directly tests the replacement gate's colour/layout decision (`PassesColourAndLayoutGate`),
    independently mutation-verified. Still untested: the three async entry points' network/orchestration
    behavior, `StoreNameLookup`'s four writers (`StoreNameLookupTests.cs` covers only `NormaliseGameName`),
    and `ArtworkSignature.cs`'s `ColourMatch`/`LayoutMatch`/`CreateAsync` (zero test file). All `true-external`
    network calls or their immediate consumers with no seam built yet.

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design (`Services/SteamGridDB/
  ArtworkSource.cs:15-51`) still makes "neither a platform-ID nor a game-ID" unrepresentable — unaffected
  this loop, confirmed via `git diff --stat`.
- This loop's rejection of the Fetcher-delegate seam is itself evidence of a working discipline, not just its
  conclusion: it correctly distinguished `FixLog.LogFolder`/`AppliedArtworkStore.RecordFolder`'s
  local-substitutable pattern (two real adapters, safe to reuse) from a true-external network fetch (would
  need a fabricated fake, not proven behavior-faithful) using the codebase's own Dependency Categorization
  table rather than pattern-matching on syntactic shape (both are "a settable static") alone.
- The mutation-verification technique established in loop 10 and refined since scaled to a fourth distinct
  case this loop — a multi-clause boolean gate extracted from inside an async loop — and caught all three
  targeted mutations precisely (1, 1, and 3 failures respectively, matching the exact set of assertions that
  exercise each mutated operator).

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling, image decode, network resolution, and bulk-operation orchestration behind zero Interface boundary

**Why it matters** — The largest file in the repo across every prior loop's Discovery continues to bundle
several structurally distinct concerns with no Module boundary between most of them, so a change to any one
risks disturbing the others.

**What is wrong** — Re-read `LoadGameEntriesAsync` directly this loop (`PrimaryWidget.xaml.cs:332-611`). The
nested `foreach` over `gameCache` entries (`:436`) still interleaves image decode (`CreateThumbnailAsync`),
the backup check (`ArtworkFiles.HasBackupAsync`, `:516`), and per-game SteamGridDB/store name resolution
(`:562,584,593,602`) inside one sequential per-entry block. This loop's own diff touches only
`SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs` and a new test file, so none of this changed;
confirmed via `git diff --stat 05501e0 HEAD -- PrimaryWidget.xaml.cs` (empty) that the file has been
byte-identical for four loops running (9 through 13).

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-611`
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:436`
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:516,520-522,562,584,593,602`

**Architectural test failed** — n/a — different category (ownership/coupling sprawl for what remains).

**Dependency category** — n/a

**Leverage impact** — Unaffected this loop.

**Locality impact** — Unaffected this loop.

**Metric signal** — `PrimaryWidget.xaml.cs`: 1,950 lines (`wc -l` confirmed this loop), unchanged.

**Why this weakens submission** — Ownership of the concerns still merged in `PrimaryWidget` remains
untraceable from any single Module besides the UI class itself.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — No further split is queued this loop: the image-decode/backup-check/
network-resolution core is still genuinely interleaved per entry, so no next slice is proposed without first
re-verifying against current source, consistent with this run's discipline since loop 8.

**Blast radius** — Change (only if a future loop verifies a further slice, with a fresh SPT first):
`PrimaryWidget.xaml.cs`. Avoid: `Services/Artwork/*`, `Services/Stores/*`, `Services/SteamGridDB/*`,
`Services/Library/*`.

---

### Finding #2: ArtworkDownloader's fetch/orchestration entry points and StoreNameLookup's four writers remain untested; this loop rejected the queued Fetcher-delegate seam and split out the tested decision core instead

**Why it matters** — `DownloadBestTileFillingImageAsync` decides which candidate image the widget applies as
artwork when auto-selecting; a wrong crop, a broken tile-fill check, or an inverted layout-quality guard
would ship visibly bad artwork with no test catching it. This is the sole remaining Authority-Map
test-coverage gap, narrowed this loop but not closed.

**What is wrong** — Read `ArtworkDownloader.cs` fresh this loop. `DownloadArtworkAsync` (`:40`) still calls a
private static `sharedHttpClient` (`:35`) directly with no seam; `DownloadBestTileFillingImageAsync` and
`FindOfficialLookalikeAsync` still orchestrate that network call, so those three async entry points remain
untested, as do `StoreNameLookup`'s four network-bound writer methods (`:86,114,203,228`) and
`ArtworkSignature`'s `ColourMatch`/`LayoutMatch`/`CreateAsync` (zero test file exists for
`ArtworkSignature.cs` — confirmed via `grep -rl ArtworkSignature SteamGridDB.Xbox.Tests/*.cs` returning no
source hits — a previously-uncredited slice of the same gap). Held loop 12's queued Priority 1 (a settable
static `Fetcher` delegate on `ArtworkDownloader`) hard against the Unified Seam Policy per this loop's
dispatch instructions, and **rejected it**: the two-adapter rule's "second adapter" would have to be a
hand-built fake fabricating network response bytes, which loop 12's own Scorecard Humility Check already
flagged as unconfirmed to be behavior-faithful (it cannot simulate timeouts, malformed responses, or partial
reads without deliberately engineering that, and a success-path-only fake is the recording-stub shape
`architecture-rubric.md` names as failing the rule). The storage-folder idiom it was modeled on
(`FixLog.LogFolder`, `AppliedArtworkStore.RecordFolder`) is not a valid precedent: that seam swaps between
two *real* `StorageFolder` adapters over the real file system (`local-substitutable` per the codebase's own
Dependency Categorization), not a real network call versus a fabricated one (`true-external`) — categories
the rubric's own table treats with different seam strategies. A global settable static delegate is also
unowned mutable state reachable from any test or future caller, which Core Architectural Standards' Ownership
& State clause (single owner per mutable concern, no hidden control flow) scores against directly. A
narrower, honest alternative existed instead: `FindOfficialLookalikeAsync`'s replacement gate already
computed `candidateMatch` and `candidateLayout` as plain doubles before deciding — pure computation needing
no seam at all. Extracted that decision into `ArtworkDownloader.PassesColourAndLayoutGate(candidateMatch,
candidateLayout, chosenLayout)` (`:205-208`) and added `ArtworkDownloaderTests.cs` (6 tests), independently
mutation-verified against all three logical mutations the extracted boolean expression admits (colour-ceiling
boundary `>=` vs `>`, layout-tie boundary `>` vs `>=`, `&&` vs `||`) — see Loop 13 Result for the full
mutation-verification detail. This closes exactly the nameable mutation loop 12's own mandatory
mutation-test check named (`ArtworkDownloader.cs`'s old `:179`, `candidateLayout < chosenLayout`).

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs:35,40,71,122` (still-untested async entry points)
- `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs:205-208` (new tested predicate)
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:86,114,203,228`
- `SteamGridDB.Xbox/Services/Artwork/ArtworkSignature.cs` (zero test file)
- `SteamGridDB.Xbox.Tests/ArtworkDownloaderTests.cs` (new this loop)
- `SteamGridDB.Xbox/Services/Artwork/FixLog.cs:35-39`, `SteamGridDB.Xbox/Services/Artwork/
  AppliedArtworkStore.cs:47-56` (the local-substitutable idiom this loop distinguished from the rejected
  Fetcher proposal)

**Architectural test failed** — Interface-as-test-surface — the remaining orchestration/fetch surface still
reaches past its Interface into a live, non-injectable network call; the extracted decision now genuinely
satisfies this test (tests live at its Interface).

**Dependency category** — `true-external`

**Leverage impact** — One call site cluster (`ArtworkDownloader`'s gate), the function every automatic
artwork pick and manual apply goes through after ranking. The extracted predicate itself has one call site.

**Locality impact** — This loop's change was scoped to `ArtworkDownloader.cs` alone (one new method, one
call-site edit) plus one new test file — no other file touched.

**Metric signal** — `ArtworkDownloader`: 1 of 3 async entry points has a directly-tested internal decision
(was 0 of 3); the entry points themselves remain untested. `StoreNameLookup`: 1 of 5 methods tested
(`NormaliseGameName`). `ArtworkSignature`: 0 of 3 members tested. All unchanged except the new predicate.

**Why this weakens submission** — A source-level mutation in the download-loop's fallback-candidate capture
(`:85-89`), the `MaxCandidates`/`chosenIndex + 1` loop boundaries, `StoreNameLookup`'s four writers, or
`ArtworkSignature`'s `ColourMatch`/`LayoutMatch` computations would pass the entire suite undetected today.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Continue the same zero-seam idiom: add `ArtworkSignatureTests.cs` testing
`ColourMatch`/`LayoutMatch`/`CreateAsync` directly against `TestImages.cs` fixtures (`CreateAsync` takes an
`IBuffer`, not a URL, so no network is needed), and extract the `officialArtworkFloor` early-exit gate
(`ArtworkDownloader.cs:146`, `chosenMatch >= officialArtworkFloor`) as a second small tested predicate.
`DownloadArtworkAsync`'s actual HTTP call and the two orchestration loops' boundary conditions remain
untested — that residual needs either a genuinely behavior-faithful network seam, built carefully enough not
to become a recording stub, or stands as an accepted limit consistent with `TESTING.md`'s own documented
network-testing boundary ("Anything over the network... A test that did that would be grading their
uptime").

**Blast radius** — Change (this loop's actual diff): `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs`,
`SteamGridDB.Xbox.Tests/ArtworkDownloaderTests.cs` (new). Avoid: `SteamGridDB.Xbox/Services/Stores/
StoreNameLookup.cs`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, everything else.

---

### Finding #3: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** — Load time scales linearly with library size and network latency on the widget's primary
open path.

**What is wrong** — Read `LoadGameEntriesAsync` fresh this loop (see Finding #1): the `gameCache` `foreach`
still awaits `sgdbClient.GetGameByPlatformIdAsync` (`:562`) and the GOG/Epic/Ubisoft name fallbacks
(`:584,593,602`) one game at a time, unchanged from every prior loop. The standing operational constraint
continues to rule out attempting this: parallelising these round-trips would change observable request
count/order/timing against third-party APIs without a behavioral oracle, and the test suite still does not
cover network calls. `StoreNameLookup`'s three static caches (`:27-31`) remain unlocked, re-confirmed safe
only because every call path remains the single sequential `foreach`.

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:399,562,584,593,602`
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:27-31`

**Architectural test failed** — n/a — different category (structural waste per `lens-efficiency.md`, not a
Seam).

**Dependency category** — `true-external`

**Leverage impact** — Unaffected this loop.

**Locality impact** — Unaffected this loop.

**Metric signal** — One HTTP round-trip per game per store lookup; unchanged this loop.

**Why this weakens submission** — Structural waste on the widget's primary hot path, unchanged from loop 7
through loop 13.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Blocked for the duration of this run by the standing operational constraint,
unchanged. Any eventual remedy must also add locking to `StoreNameLookup`'s three static caches.

**Blast radius** — Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync`), `Services/Stores/
StoreNameLookup.cs`. Avoid: `Services/Artwork/*`, `Services/SteamGridDB/*`.

## Simplification Check

- **Structurally necessary:** Extracting `PassesColourAndLayoutGate` from `FindOfficialLookalikeAsync`'s
  inline boolean guard — passes the Shallow-module/Interface-as-test-surface tests (Interface is now smaller
  than and independently testable from the surrounding orchestration; the deletion test on the *old* inline
  form shows complexity would reappear at the one call site if inlined back, but the new form's whole point is
  that tests now live at the extracted Interface rather than requiring the caller's network dependency).
- **New seam justified:** false — no protocol/port/abstraction introduced this loop; `PassesColourAndLayoutGate`
  is a plain pure `internal static` method, not a Seam (nothing is swapped at runtime; there is exactly one
  Adapter because there is no Adapter concept here at all — in-process, no I/O, per Dependency
  Categorization's "No seam needed" row). The Unified Seam Policy does not apply to this loop's diff. The
  Fetcher-delegate Seam loop 12 queued was evaluated and explicitly rejected (see Finding #2) rather than
  built.
- **Helpful simplification:** The extraction is a minor positive simplification (names a previously-inline,
  three-clause boolean expression) but is credited to `test_strategy`, not scored as a `simplicity` gain, per
  this run's anti-double-counting convention.
- **Should NOT be done:** Building the Fetcher-delegate seam (rejected this loop, see Finding #2). Also not
  attempted: extending this loop's extraction to `StoreNameLookup`'s writers (no proven friction there yet —
  they don't reduce to plain values before deciding the way `FindOfficialLookalikeAsync` did) or to
  `DownloadArtworkAsync` itself (its only content is the network call; there is no decision logic to extract
  out from under it). Also not attempted: forcing a `domain_modeling` or `framework_idioms` finding — no new
  evidence surfaced this loop to reopen loop 11's SPT-rejection of both. Also not attempted: any further slice
  of F-001 — no new evidence surfaced this loop reopening that question.
- **Tests after fix:** No prior test exercised any decision logic inside `FindOfficialLookalikeAsync`. Six new
  tests added at `PassesColourAndLayoutGate`'s new Interface (the old inline `if` guard had no Interface of
  its own to test at — nothing to delete under Replace-don't-layer, since nothing shallow was left behind;
  the old guard *became* the new predicate rather than being duplicated alongside it). Verified
  mutation-sensitive directly: three separate operators mutated in turn (`>` to `>=`, `>=` to `>`, `&&` to
  `||`), exactly the expected test(s) failed each time, then reverted.

## Improvement Backlog

1. **Add `ArtworkSignatureTests.cs` and extract the `officialArtworkFloor` gate as a second tested predicate
   (F-007, continuing narrowing)** — the same zero-seam idiom this loop proved out, applied to the two
   remaining pure-computation slices of the artwork download/selection gate.
   - Why it matters: `ArtworkSignature.ColourMatch`/`LayoutMatch` are pure, network-free (`CreateAsync` takes
     `IBuffer`, not a URL) and currently have zero test coverage — a previously-uncredited slice of the same
     Authority-Map gap. The `officialArtworkFloor` early-exit (`ArtworkDownloader.cs:146`) is the same shape
     as the gate just extracted.
   - Score impact: `test_strategy`'s residual narrows further; does not by itself reach 10, since
     `DownloadArtworkAsync`'s network call and the orchestration loops' boundaries would still be untested.
   - Explicitly do NOT build a Fetcher/network seam on `ArtworkDownloader` or `StoreNameLookup` — rejected
     this loop against the Unified Seam Policy (Finding #2); re-litigate only with new evidence that a
     genuinely behavior-faithful fake (not a recording stub) can be built.
2. **Bound concurrency in `LoadGameEntriesAsync`'s per-game SteamGridDB lookups (F-003)** — blocked for the
   duration of this run by the standing operational constraint.
   - Why it matters: removes load latency scaling linearly with library size, whenever the constraint is
     lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

- **Candidate Module:** `ArtworkDownloader`'s orchestration (`DownloadBestTileFillingImageAsync` +
  `FindOfficialLookalikeAsync`'s candidate-selection loops).
- **Source friction proven:** Finding #2 — this loop found that pulling the leaf decision
  (`PassesColourAndLayoutGate`) out of the async loop required no seam and unlocked direct testing
  immediately. The remaining friction is that the loops' own control flow (which candidate to try next, when
  to stop, the fallback capture) is still interleaved with the network fetch inside the same `for`/`await`
  body, so *that* logic can't be tested without a real round-trip either.
- **Why the current Interface is shallow or misplaced:** `DownloadBestTileFillingImageAsync` and
  `FindOfficialLookalikeAsync`'s Interfaces each mix two responsibilities behind one signature: "fetch the
  next candidate's bytes" (I/O) and "decide whether to keep looking / which one wins" (pure, in-process, given
  already-decoded candidates). The Implementation currently earns its Depth only by doing both together.
- **Behavior to move behind the deeper Interface:** given a sequence of already-decoded candidates (signature
  + fills-tile bool per candidate, already available in-process), pick the winner — separate from fetching and
  decoding the next candidate.
- **Dependency category:** `in-process` (the decision half only) — no seam needed at all, matching this
  loop's finding that leaf decisions here require none.
- **Test surface after the change:** new tests exercising the loop-selection logic itself (the `chosenIndex +
  1` start, the `MaxCandidates` boundary, the fallback-candidate capture at `:85-89`) against constructed
  `ArtworkSignature`/bool inputs, no network.
- **Smallest first step:** extract one more leaf decision the same way this loop did (`officialArtworkFloor`
  gate, per the Improvement Backlog) before attempting the loop-control extraction itself — prove the pattern
  scales to a second case first.
- **What not to do:** do not build a general iterator/strategy abstraction around "candidate selection" — that
  is ceremony beyond what one more extracted predicate needs. Do not touch `DownloadArtworkAsync` itself (the
  actual network call), which is correctly excluded from this deepening; it has no decision logic to extract,
  only the fetch.

## Builder Notes

1. **Pattern:** When a proposed seam's "second adapter" would have to be a hand-built fake simulating an
   external system, check first whether the decision logic that actually needs testing is separable from the
   fetch. Async functions that call the network often already reduce their inputs to plain values (numbers,
   bools, structs) before deciding what to do with them — extracting from that point needs no seam, no fake,
   and no new mutable state at all.
   - How to recognize: a Finding's "why it matters" names one specific comparison/boundary (a `<`, `<=`,
     `&&`-joined condition) as the risk, not "the whole untested function" — that's a signal the decision,
     not the fetch, is what actually needs coverage.
   - Smallest coding rule: before proposing any seam, find where the function already reduces its network/IO
     result to plain values, and extract from that point forward as a pure static method.
   - Stack example: C# - `ArtworkDownloader.FindOfficialLookalikeAsync` already computed `candidateMatch`/
     `candidateLayout` as doubles before deciding; `PassesColourAndLayoutGate(double, double, double)` needed
     neither a `Fetcher` delegate nor a mock HTTP client.

2. **Pattern:** A settable static delegate (`Func<T>`) proposed as a test seam for a true-external dependency
   is architecturally different from a settable static property proposed for a local-substitutable one (e.g.
   `StorageFolder`), even though both look like "the same idiom" on the surface — the file-system case swaps
   between two *real* adapters, the network case would swap a real call for fabricated data.
   - How to recognize: before reusing an existing settable-static-property idiom for a new seam, check the
     Dependency Categorization of what's being swapped — `local-substitutable` (real thing, different
     location/instance) vs `true-external` (real remote system; the test side would have to be fabricated).
   - Smallest coding rule: only reuse the settable-static idiom directly when the test-side value can be a
     REAL instance of the same type (a temp folder, a second real client pointed at a local stand-in); when
     the test side would have to be hand-fabricated data pretending to be a response, treat it as a new
     architectural decision needing its own two-adapter analysis, not a drop-in reuse of a working pattern.
   - Stack example: C# - `FixLog.LogFolder`/`AppliedArtworkStore.RecordFolder` swap a real `StorageFolder` for
     another real `StorageFolder` (safe reuse, proven twice); a proposed `ArtworkDownloader.Fetcher` would swap
     a real `HttpClient` call for fabricated `IBuffer` bytes (not a safe reuse without further work to prove
     the fake behavior-faithful).

3. **Pattern:** Extracting a multi-clause boolean gate into its own named method turns each clause boundary
   into an independently mutation-testable unit — mutate one comparison operator at a time and confirm exactly
   the boundary-condition test(s) fail, not the whole suite.
   - How to recognize: an inline `if (a <= X || b < Y || !Z)`-shaped guard buried inside a larger async
     function, where a finding's "why it matters" names one specific sub-clause as the risk.
   - Smallest coding rule: extract the boolean expression (inverting via De Morgan's law reads more naturally
     as a "passes" predicate at the call site) into a pure static method; write one test per boundary (each
     operator's tie-breaking direction) plus one combining-failure test; mutate each operator individually and
     confirm only the matching boundary test fails.
   - Stack example: C# - `PassesColourAndLayoutGate`'s 3 mutations (`>=` for `>`, `>` for `>=`, `||` for `&&`)
     each broke exactly the test naming that boundary (1, 1, and 3 failures respectively), not the whole
     suite.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. Rejecting the Fetcher-delegate seam as failing the two-adapter rule (Finding #2) — a less strict reviewer
   could argue the codebase's own Dependency Categorization table prescribes exactly "inject port + mock
   adapter" as the strategy for `true-external` dependencies, and that my objection (the fake isn't proven
   behavior-faithful yet) is an argument for building the fake carefully, not for rejecting the seam shape
   outright. I judged the multi-writer-global-state concern plus the unproven-fidelity risk as decisive
   against attempting it *this loop*; a reviewer optimizing purely for fastest gap-closure might disagree.
2. Holding `test_strategy` at 9.5 SAME rather than treating the narrowed residual as grounds to move toward
   promotion language closer to 10 — I judged that a real, nameable, uncaught mutation still exists (the
   fallback-capture logic at `ArtworkDownloader.cs:85-89`), so 10 isn't earned; a stricter reader of the
   9-anchor's "at most one gap" language could read the narrowing itself as more consequential than I scored
   it, though the grid's quantization (9.5 is the only rung below 10) limits how much this matters in
   practice.
3. My bright line between `FixLog`/`AppliedArtworkStore`'s `StorageFolder` swap (`local-substitutable`, safe
   reuse) and the rejected `Fetcher` proposal (`true-external`, unsafe reuse) — derived from the codebase's
   own Dependency Categorization table, but a stricter reader could argue a temp directory is *also* not
   perfectly identical to production (different physical location, no app-container ACL quirks) and that I'm
   drawing a sharper category line than the rubric intends between "different real instance" and "fabricated
   instance."

## Final Judge Narrative

Place, not win. Ground truth was clean going in (both gates green, zero source drift since loop 12's commit)
and clean coming out (131/131 tests, MSBuild exit 0). This loop's dispatch asked two things to be weighed
honestly rather than inherited: whether `test_strategy`'s 9.5 still holds, and whether loop 12's queued
Fetcher-delegate seam survives the Unified Seam Policy. On the seam: it does not survive, and the reasoning is
recorded precisely enough that no future loop needs to re-litigate it without new evidence — the "second
adapter" would be fabricated network data, not a real one, unlike the `StorageFolder` idiom it was modeled on,
and a settable static delegate is unowned global mutable state regardless. Rather than build seam ceremony to
buy a test-coverage point, this loop found and took a genuinely honest narrower alternative: the exact
decision logic the Authority Map gap's risk language pointed at (the layout-comparison guard) was already pure
computation one call away from testable, and extracting it needed nothing more than a new pure method and six
tests. On `test_strategy`: 9.5 holds, not because the prior loop said so, but because a fresh mutation-test
check this loop names a still-uncaught mutation elsewhere in the same file — the residual moved, the score
didn't, and that's the honest outcome the score grid's quantization actually supports. Runtime ownership and
concurrency are unaffected and exactly as trustworthy as loop 12 left them — this loop's diff touches one
production file with a pure, stateless addition, and `git diff --stat` confirms every other production file
byte-identical since loop 12's commit. Tests reduce regressions more precisely than last loop measured on this
specific gate: the exact mutation named as the residual blocking 10 is now caught, independently
mutation-verified three times. Future work risks nothing new from overengineering — this loop's fix added zero
new abstraction beyond one named pure method, and the seam it explicitly declined to build stays declined
until new evidence changes the analysis. Backlog is not empty (continue narrowing the same gap; F-003 still
blocked), so `CONTINUE`.

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

**Targeted finding status:** `carried_forward` — F-007's Claim (`ArtworkDownloader`/`StoreNameLookup`'s
network-bound entry points and writers are untested) still holds for the network-facing surface; the specific
mutation loop 12 named as evidence for that claim is now caught, but the Claim's remaining scope (three async
entry points, four `StoreNameLookup` writers, `ArtworkSignature`) is unresolved, so the finding is not marked
resolved.

**Unintended scorecard regression:** none. All nine dimensions held SAME with fresh structural re-derivation
this loop; `test_strategy` remains at 9.5 (residual narrowed, not closed — no rung exists between 9.5 and 10
while a nameable residual remains) rather than moving UP or DOWN. No dimension regressed.

## Loop 13 Implementation Review

`verdict: approved` — "The extraction is a verified De Morgan-equivalent, short-circuit-preserving refactor
with no new seam, and the 6 new tests directly and mutation-verifiably cover the exact boundary conditions
(candidateMatch > officialArtworkCeiling, candidateLayout >= chosenLayout) that were previously untested
inline logic." All three checks (`reality`, `honesty`, `regression`) `passed`; `conditions: []`;
`regressions: []`.
