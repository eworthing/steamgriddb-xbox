### Discovery (see Loop 7 Discovery refresh)

Resumed from loop 10's `HALT_LOOP_CAP` per the user's `--cap 15` re-invocation (the documented auto-resume
path). Main verified no drift before dispatch (HEAD still loop 10's commit `b042f75`, working tree clean
except main's own `loop_cap: 10 -> 15` edit to `CURRENT_REVIEW.json`) and raised the cap. This loop
independently re-ran both ground-truth gates fresh before touching anything:

- `powershell -NoProfile -ExecutionPolicy Bypass -File ./run-tests.ps1` — **115 passed, 0 failed** before
  this loop's fix, **121 passed, 0 failed** after (6 new tests added; see Loop 11 Result).
- `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
  SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo` — exit
  0 before and after.
- `git log b042f75..HEAD` was empty before this loop's own edits; `PrimaryWidget.xaml.cs` measured 1,950
  lines via `wc -l` this loop, matching every prior loop's figure exactly.
- Selected lens: Generic (C#/.NET / UWP-hosted WinUI stack).

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

- **Structurally necessary:** Adding six tests to `TileImageTests.cs` plus four new fixture-builder helpers
  to `TestImages.cs`. No Module removed or restructured, no Seam introduced — a pure test addition.
  Unified Seam Policy does not apply.
- **New seam justified:** false — no protocol/port/abstraction introduced this loop; the seam this loop's
  investigation motivates (`ArtworkDownloader`'s HTTP fetch) is deferred to next loop as a Deepening
  Candidate, not built now.
- **Helpful simplification:** None — this loop's fix is a test-coverage addition, not a simplification.
  `simplicity` correctly held SAME.
- **Should NOT be done:** Building the `ArtworkDownloader` HTTP seam this loop without a fresh Step 2 SPT
  pass of its own — friction is now proven (Finding #2), but the seam's shape (delegate parameter vs.
  interface vs. constructor injection) deserves its own tiebreak-free loop rather than being bolted on
  opportunistically inside this loop's test-only fix. Also not attempted: forcing a `domain_modeling` or
  `framework_idioms` finding despite this loop's dispatch instructions inviting one — investigated both
  fresh (see Scorecard) and found the "anemic DTO" / "serializer split" framings are justified platform
  accommodations, not neglect; no fix passed SPT question 1 (fixes real ambiguity). Also not attempted: any
  further slice of F-001 — no new evidence surfaced this loop reopening that question.
- **Tests after fix:** No prior test exercised `TileImage.FillsTileAsync`, `TileImage.
  CropPortraitToTileAsync`, or (indirectly) `TileImage.BestVerticalCropAsync`. Six new tests added at the
  existing public Interface (`FillsTileAsync`, `CropPortraitToTileAsync`), following the file's existing
  `WithDecoderAsync`-based construction pattern. Verified mutation-sensitive directly rather than merely
  asserted: two separate production lines inverted in turn, exactly the expected new tests failed each
  time, then reverted.

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

1. **Pattern:** A "test with constructed fixtures" backlog item can turn out to be non-executable as scoped
   once you actually try it — the function's own body may call further out (here, into a live network
   client) past where the fixture stops. Discovering that mid-attempt is itself a finding, not a reason to
   abandon the loop: close the reachable half, and hand the unreachable half forward with the concrete
   reason (no injectable seam) instead of the vague reason (untested).
   - How to recognize: a finding names "construct X fixtures and test Y" but Y's implementation calls a
     further, non-injectable dependency (a static `HttpClient`, ambient state) before it ever touches the
     fixture.
   - Smallest coding rule: before writing the test, trace every call the target function makes internally;
     if any of them reaches outside the process (network, disk, ambient state) with no injection point,
     that call — not the whole function — is the actual blocker. Test what's reachable; name what isn't as
     a separate, more precise finding.
   - Stack example: C# - `ArtworkDownloader.DownloadBestTileFillingImageAsync` calls `DownloadArtworkAsync`
     internally, which hits a private static `HttpClient` with no seam; the algorithm it wraps around that
     call (`TileImage.FillsTileAsync`/`CropPortraitToTileAsync`) has no such dependency and was fully
     testable.

2. **Pattern:** A crop/window-selection algorithm's mutation-sensitivity can be proven with a synthetic
   two-band image (one flat region, one high-contrast region) placed at each extreme, rather than needing to
   predict the algorithm's exact numeric output.
   - How to recognize: a private helper does a sliding-window/best-of search over derived per-row (or
     per-cell) scores, reachable only through a public wrapper.
   - Smallest coding rule: construct one input with all the signal concentrated at one end and none at the
     other (and the mirror image), then assert the *output contains the signal*, not a specific coordinate
     — this survives implementation details of exactly how the window is scored while still catching a
     reversed comparison or fixed-offset shortcut.
   - Stack example: C# - built a 64x256 portrait PNG with a flat grey band and a checkerboard band, once at
     the top and once at the bottom; asserted the cropped 64x64 output was not uniformly the flat colour in
     both cases, which caught a `running > best` -> `running < best` mutation directly.

3. **Pattern:** Re-litigating a long-held low score is worth doing periodically, but "anemic DTO" isn't
   automatically a defect — a wire-format type mirroring a third-party API's JSON shape is expected to be a
   data bag; the question is whether the codebase's actual domain types (the ones representing decisions,
   not wire data) show the same anemia.
   - How to recognize: a `domain_modeling` finding names a type whose fields are all
     `[DataMember]`/`[JsonProperty]`-decorated and 1:1 with an external API's documented response shape.
   - Smallest coding rule: before proposing smart constructors on a wire DTO, check whether the surrounding
     code already has a distinct domain type built from it that *does* enforce invariants. If so, the wire
     type's anemia is by design and adding validation to it fixes no real ambiguity — it fails the Simplify
     Pressure Test's first question.
   - Stack example: C# - `SteamGridDbGame`/`SteamGridDbGrid` stay anemic on purpose
     (`DataContractJsonSerializer` needs public setters); `ArtworkSource` right next to them has a private
     constructor and two factory methods precisely because it represents a real decision (which of two
     addressing schemes a game uses), not wire data.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. Holding `test_strategy` at 8.5 SAME rather than crediting any UP for this loop's two mutation-verified
   test additions — a more lenient reviewer could argue that discovering `FixLog`'s gap via the Authority Map
   is itself new information unrelated to what this loop's own diff achieved, and `TileImage`'s closure
   deserves credit on its own terms (e.g., 8.5->8.75, rounded to 9.0) with `FixLog` simply added as a
   separate, freshly-opened backlog item rather than used to cap the score for work this loop didn't touch.
   I judged the 9-anchor's literal "at most one gap" wording as controlling; a stricter or more lenient
   reader could reasonably land elsewhere.
2. Holding `domain_modeling` at 5.5 for an eleventh consecutive loop on the strength of "the wire DTOs are
   anemic by design, and the real domain types aren't" — I did not audit every type under `Models/` and
   `Services/*/Models/` against this bar this loop, only the ones this loop's investigation happened to
   touch (`SteamGridDbGame`, `SteamGridDbGrid`, `ArtworkSource`, `GameEntry`); a type I did not look at could
   still be a genuine anemic-domain-type finding.
3. The Deepening Candidate proposing an HTTP-fetch seam on `ArtworkDownloader` — a stricter reviewer might
   argue the two-adapter rule requires the test fake to actually get built and proven useful before the seam
   is "justified," and until next loop does that, this is a proposal backed by proven friction, not yet proof
   the resulting seam itself will be clean; if the fake never materializes usefully, this was closer to
   speculative seam design than genuine friction-driven extraction.

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
