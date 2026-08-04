### Discovery (see Loop 7 Discovery refresh)

Resumed from loop 11's `CONTINUE` at commit `85b5279`. Working tree was clean at dispatch. This loop
independently re-ran both ground-truth gates fresh before touching anything:

- `powershell -NoProfile -ExecutionPolicy Bypass -File ./run-tests.ps1` — **121 passed, 0 failed** before
  this loop's fix, **125 passed, 0 failed** after (4 new tests added; see Loop 12 Result).
- `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
  SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo` — exit
  0 before and after.
- `git log 85b5279..HEAD` was empty before this loop's own edits; `PrimaryWidget.xaml.cs` measured 1,950
  lines via `wc -l` this loop, matching every prior loop's figure exactly; confirmed unchanged since loop 9's
  commit `05501e0` via `git diff --stat 05501e0 HEAD -- PrimaryWidget.xaml.cs` (empty).
- Selected lens: Generic (C#/.NET / UWP-hosted WinUI stack).

**Process note on this loop's Step 1 ordering**: this loop's dispatch read `CURRENT_REVIEW.md` in full
(Discovery plus prior verdict/scorecard together, in one file read) before the independent scorecard was
drafted, rather than Discovery-only first. To compensate, every score below was re-derived from a fresh
direct read of current source this loop (file:line citations, `git diff --stat` confirmations, a fresh
mutation-verification pass on the new test) rather than from memory of the prior review's text, and the
prior scorecard was consulted only afterward for delta/oscillation bookkeeping, consistent with the
Anchor-to-source discipline in `method.md` Step 1.

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
below as a refined Deepening Candidate for next loop rather than built opportunistically inside this loop's
test-only fix.

## Scorecard (1-10)

- **Architecture quality:** 7.0 | SAME | `PrimaryWidget.xaml.cs` confirmed byte-identical since loop 9's
  commit `05501e0` via `git diff --stat 05501e0 HEAD` (empty); re-read `LoadGameEntriesAsync` (`:332-611`)
  directly this loop and confirmed the same merge of image decode (`:520-522`), backup check (`:516`), and
  per-game network resolution (`:562-609`) persists inside the nested `foreach` at `:436`. `ArtworkDownloader.
  cs` re-read fresh this loop (unaffected by this loop's test-only diff).
- **State management and runtime ownership:** 7.0 | SAME | `AppliedArtworkStore.cs` and `FixLog.cs`
  production code confirmed byte-identical to HEAD this loop (`git diff --stat` shows only
  `SteamGridDB.Xbox.Tests/FixLogTests.cs` added; zero production files touched). `FixLog.cs` read fresh this
  loop: `Start`/`Write`/`SaveAsync` remain the same three-member shape, one clear writer per member.
- **Domain modeling:** 5.5 | SAME | Not re-litigated this loop per this run's explicit prior finding
  (loop 11 already investigated and SPT-rejected a construction-time-invariant fix on the wire DTOs, citing
  `SteamGridDbGame.cs:36-39`'s own doc comment on the `DataContractJsonSerializer` platform constraint).
  Confirmed via `git diff --stat 85b5279 HEAD -- SteamGridDB.Xbox/Services/SteamGridDB/Models/
  SteamGridDbGame.cs SteamGridDB.Xbox/Services/Artwork/ArtworkSource.cs` (empty) that no new evidence exists
  this loop to reopen that question.
- **Data flow and dependency design:** 6.0 | SAME | `StoreNameLookup.cs` read fresh this loop: the three
  unlocked static caches (`gogNameCache`/`epicNameCache`/`nameMatchCache`, `:27-32`) confirmed unchanged.
  `EpicLibrary.cs`'s ambient `Environment.GetEnvironmentVariable` fallback and `AppliedArtworkStore.cs`'s
  ambient `ApplicationData.Current` default confirmed untouched via `git diff --stat` (zero production files
  changed this loop).
- **Framework / platform best practices:** 6.0 | SAME | Not re-litigated this loop per this run's explicit
  prior finding (loop 11 already investigated and SPT-rejected treating the `DataContractJsonSerializer` /
  `Windows.Data.Json` split in `SteamGridDbClient.cs` as a framework-idiom violation, citing the class's own
  doc comment at `:137-141`). Confirmed via `git diff --stat 85b5279 HEAD -- SteamGridDB.Xbox/Services/
  SteamGridDB/SteamGridDbClient.cs` (empty) that no new evidence exists this loop.
- **Concurrency and runtime safety:** 6.5 | SAME | F-003's sequential per-game awaits (`PrimaryWidget.
  xaml.cs:562,584,593,602`) re-confirmed at identical line numbers via this loop's own direct read (see
  Finding #3). Re-grepped codebase-wide for `Task.WhenAll`/`Parallel.*`/`Task.Run` this loop, zero hits, same
  as every prior loop.
- **Code simplicity and clarity:** 8.5 | SAME | This loop's diff is one new test file only
  (`SteamGridDB.Xbox.Tests/FixLogTests.cs`, 106 lines) — no simplification, no new ceremony, no new
  abstraction. Follows the existing `TempFolder` + settable-static-folder-property pattern
  `AppliedArtworkStoreTests.cs` already establishes exactly (same shape: `using var temp = new TempFolder();
  FixLog.LogFolder = temp.Folder;`).
- **Test strategy and regression resistance:** 9.5 | UP (from 8.5) | Closed F-008: added `FixLogTests.cs`
  (4 tests) covering all three of `FixLog`'s members (`Start`, `Write`, `SaveAsync`). Independently verified
  mutation-sensitive, not just asserted: (1) removed `FixLog.cs:49`'s `lines.Clear();`, re-ran the suite, got
  exactly 1 failure (`Starting_a_new_run_discards_the_previous_runs_lines`), reverted; (2) removed `FixLog.
  cs:48`'s `fileName = file;` assignment, re-ran, got exactly 3 failures (every test asserting on the
  file-name parameter), reverted; (3) no-op'd `FixLog.cs:58`'s `lines.Add(line);`, re-ran, got exactly 2
  failures (the two tests asserting on `Write`-appended content), reverted; 125/125 re-confirmed green before
  each subsequent mutation and after the final revert (`git diff --stat` on `FixLog.cs` empty). This closes
  the second of the two Authority-Map gaps loop 11 named as the sole reason `test_strategy` held at 8.5
  ("With `ArtworkDownloader` (Finding #2) and `FixLog` (Finding #4) both open and neither an accepted
  residual, the 9-anchor's 'at most one named gap' bar is not met" — loop 11's own words). With `FixLog`
  closed, exactly one Authority-Map gap remains (`ArtworkDownloader`/`StoreNameLookup`'s network-bound
  entry points, Finding #2, `true-external`, queued in the Improvement Backlog as Priority 1) — the 9-anchor's
  "at most one gap" bar is now met, and per `architecture-rubric-scoring.md`'s 9.5+ Threshold, a *queued*
  residual (not only an accepted one) is a valid disposition for 9.5 (only `HALT_SUCCESS` requires
  `accepted`). Also freshly confirmed via `TESTING.md`'s "What is not covered" section (read fresh this loop)
  that the Authority Map's scope (Services/Artwork, Services/Stores; excluding `PrimaryWidget.xaml.cs`'s
  UI-bound state and network calls) is the project's own documented, reasoned test-coverage boundary, not an
  arbitrary loop-invented one — stronger evidence for anchor-completeness than any prior loop had. Mandatory
  mutation-test mental-model check (`method.md` Step 8): a nameable mutation the current suite would NOT
  catch is `ArtworkDownloader.cs:179`'s `candidateLayout < chosenLayout` comparison (flipping the operator
  would silently invert the official-artwork replacement gate's layout-quality guard) — zero test file exists
  for `ArtworkDownloader.cs`, so this is the named residual, not a 10.
- **Overall implementation credibility:** 7.5 | SAME | Consistent with this run's established
  anti-double-counting convention (explicit in loop 11's own scorecard reasoning): this loop's fix is
  credited entirely to `test_strategy` rather than double-counted here. `PrimaryWidget.xaml.cs`'s 1,950
  lines remain unverified by anything but inspection outside the small tested slices, and
  `ArtworkDownloader`'s untested network-bound entry points (Finding #2) are the same category of
  unverified-but-inspected code that `FixLog` was before this loop. Holding `credibility` steady while
  `test_strategy` absorbs the credit for a single act of rigor is the same discipline loop 11 applied when
  the roles were reversed.

## Authority Map

Re-emitted this loop per G24 (mandatory whenever `test_strategy >= 9`). Scope confirmed this loop against
`TESTING.md`'s own "What is not covered" section (read fresh) — `PrimaryWidget.xaml.cs`'s UI-bound state is
excluded by documented project design (no desktop projection for `Windows.UI.Xaml`), and network calls are
excluded by documented project design ("A test that did that would be grading their uptime"), which is why
neither widens this Authority Map's scope.

- **Concern:** Applied-artwork record (which SteamGridDB artwork ID was written to each tile).
  - **Owner:** `AppliedArtworkStore` (`Services/Artwork/AppliedArtworkStore.cs`).
  - **Allowed writers:** `SetAsync`, `ClearAsync` — both funnel through the private `UpdateAsync`, gated by
    the shared `SemaphoreSlim gate`.
  - **Readers:** `GetAsync` (also gated, per F-002's read/write lock invariant, resolved loop 2).
  - **Persistence seam:** `applied-artwork.json` in `RecordFolder` (defaults to `ApplicationData.Current.
    LocalFolder`).
  - **Async mutation entry points:** `SetAsync`, `ClearAsync`.
  - **Verdict:** Single and clear. Direct test: `AppliedArtworkStoreTests.cs`.

- **Concern:** Fix-run diagnostic log (what happened during the last "fix library" pass).
  - **Owner:** `FixLog` (`Services/Artwork/FixLog.cs`).
  - **Allowed writers:** `Start` (resets the in-memory `lines` list), `Write` (appends) — called from
    `PrimaryWidget` and `ArtworkDownloader.FindOfficialLookalikeAsync`.
  - **Readers:** none in-process; `SaveAsync` writes to disk for the user to inspect externally.
  - **Persistence seam:** `last-fix.log` / `last-load.log` in `LogFolder` (defaults to `ApplicationData.
    Current.LocalFolder`).
  - **Async mutation entry points:** `SaveAsync` (the only async member; `Start`/`Write` are synchronous).
  - **Verdict:** Single and clear. **Direct test: `FixLogTests.cs` (new this loop)** — was "no direct test
    file" through loop 11; closed this loop.

- **Concern:** Store-name lookup caches (GOG/Epic names, SteamGridDB name-match results) and the artwork
  download/selection gate.
  - **Owner:** `StoreNameLookup` (`Services/Stores/StoreNameLookup.cs`) and `ArtworkDownloader` (`Services/
    Artwork/ArtworkDownloader.cs`).
  - **Allowed writers:** `StoreNameLookup.GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/
    `FindGameByNameAsync`/`LoadUbisoftGameListAsync` (each owns its own cache slot); `ArtworkDownloader`
    holds no mutable state of its own but is the sole gate deciding which downloaded candidate becomes the
    tile.
  - **Readers:** the same four `StoreNameLookup` writers (read-through cache).
  - **Persistence seam:** none — in-memory only, process lifetime; `ArtworkDownloader` has no persistence
    seam of its own.
  - **Async mutation entry points:** the four `StoreNameLookup` writers; `ArtworkDownloader.
    DownloadArtworkAsync`/`DownloadBestTileFillingImageAsync`/`FindOfficialLookalikeAsync`.
  - **Verdict:** Single and clear ownership, but **the sole remaining test gap**: `StoreNameLookupTests.cs`
    covers only the pure `NormaliseGameName` helper; `ArtworkDownloaderTests.cs` does not exist. Both are
    blocked by the same cause — `true-external` network calls with no injectable seam (Finding #2) — and are
    treated as one Authority-Map gap, not two, consistent with loop 11's cross-referencing.

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design (`Services/SteamGridDB/
  ArtworkSource.cs:15-51`) still makes "neither a platform-ID nor a game-ID" unrepresentable — unaffected
  this loop, confirmed via `git diff --stat`.
- `FixLog.LogFolder`'s settable-static-property pattern (matching `AppliedArtworkStore.RecordFolder`) let
  this loop's fix reach full coverage with zero production risk and zero new test infrastructure — the same
  shape already proven twice in this run (`AppliedArtworkStoreTests.cs`, now `FixLogTests.cs`) is exactly the
  idiom the Deepening Candidate below proposes reusing for `ArtworkDownloader`'s network seam, rather than
  introducing a new interface/protocol shape the codebase does not otherwise use.
- The mutation-verification technique established in loop 10 and refined in loop 11 (flip/no-op the exact
  production line, confirm exactly the expected test(s) react, revert) scaled to a third distinct case this
  loop — a stateful three-member static class rather than a pure algorithm — and caught all three targeted
  mutations precisely (1, 3, and 2 failures respectively, matching the exact set of assertions that exercise
  each mutated line).

## Findings

### Finding #1: PrimaryWidget.xaml.cs still merges UI event handling, image decode, network resolution, and bulk-operation orchestration behind zero Interface boundary

**Why it matters** — The largest file in the repo across every prior loop's Discovery continues to bundle
several structurally distinct concerns with no Module boundary between most of them, so a change to any one
risks disturbing the others.

**What is wrong** — Re-read `LoadGameEntriesAsync` directly this loop (`PrimaryWidget.xaml.cs:332-611`).
The nested `foreach` over `gameCache` entries (`:436`) still interleaves image decode (`:520-522`), the
backup check (`:516`), and per-game SteamGridDB/store name resolution (`:562,584,593,602`) inside one
sequential per-entry block. This loop's own diff touches only `SteamGridDB.Xbox.Tests/FixLogTests.cs` (new
file), so none of this changed; confirmed via `git diff --stat 05501e0 HEAD -- PrimaryWidget.xaml.cs`
(empty) that the file has been byte-identical for three loops running (9, 10, 11, 12).

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

### Finding #2: ArtworkDownloader's three entry points and StoreNameLookup's four network-bound writers remain untested because each calls the network directly with no injectable seam

**Why it matters** — `DownloadBestTileFillingImageAsync` decides which candidate image the widget applies as
artwork when auto-selecting; a wrong crop, a broken tile-fill check, or an inverted layout-quality guard
would ship visibly bad artwork with no test catching it. This is now the sole remaining test-coverage gap in
the Authority Map.

**What is wrong** — Read `ArtworkDownloader.cs` fresh this loop and confirmed unchanged since loop 11:
`DownloadArtworkAsync` (`:40`) calls a private static `sharedHttpClient` (`:35`) directly with no seam to
inject a fixture through, and `DownloadBestTileFillingImageAsync`/`FindOfficialLookalikeAsync` both call
`DownloadArtworkAsync` internally. `StoreNameLookup.cs`'s four writer methods (`GetOrFetchGogNameAsync`,
`GetOrFetchEpicNameAsync`, `FindGameByNameAsync`, `LoadUbisoftGameListAsync`) are in the same position — each
calls a store's HTTP API directly. `TESTING.md`'s own "What is not covered" section (read fresh this loop)
names this exact boundary as intentional: "Anything over the network... A test that did that would be
grading their uptime." Per this loop's dispatch instructions, evaluated whether loop 11's queued
Deepening Candidate (an injectable HTTP-fetch seam) survives the Unified Seam Policy: a bare `IArtworkFetcher`
interface with one production conformer would fail the two-adapter rule unless a genuinely behavior-faithful
fake (not a recording stub) is built in the *same* change — but the codebase's own established idiom for
exactly this situation is not a new interface at all. `FixLog.LogFolder` and `AppliedArtworkStore.
RecordFolder` are both settable static properties that default to the real dependency
(`ApplicationData.Current.LocalFolder`) and are swapped for a test double via simple assignment, with no
protocol/interface ceremony. The same shape applied to `ArtworkDownloader` — a settable static
`Func<string, Task<IBuffer>> Fetcher` property defaulting to the real `sharedHttpClient` call — would satisfy
the two-adapter rule (prod HTTP call + a test fake returning canned `IBuffer` bytes per URL, built from the
existing `TestImages.cs` fixture helpers) without introducing a shape the codebase does not already use
elsewhere. This is a narrower, idiom-matched refinement of loop 11's more open-ended "delegate or interface"
proposal, not yet built this loop (see Deepening Candidate).

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs:35,40,71,122,179`
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:86,114,203,228`
- `TESTING.md:57-59` ("What is not covered" — network)
- `SteamGridDB.Xbox/Services/Artwork/FixLog.cs:35-39`, `SteamGridDB.Xbox/Services/Artwork/
  AppliedArtworkStore.cs:47-53` (the settable-static-property idiom this loop proposes reusing)

**Architectural test failed** — Interface-as-test-surface — tests cannot stay at `ArtworkDownloader`'s
current Interface because the Implementation reaches past it into a live, non-injectable network call.

**Dependency category** — `true-external`

**Leverage impact** — One call site cluster (`ArtworkDownloader`'s gate), but it is the function every
automatic artwork pick and manual apply goes through after ranking.

**Locality impact** — A seam change here would be scoped to `ArtworkDownloader.cs` alone (one new static
property plus threading it through three internal calls); `StoreNameLookup`'s four writers are a separate,
smaller follow-up using the identical idiom, not required for this Finding's minimal correction path.

**Metric signal** — `ArtworkDownloader`: 0 of 3 entry points tested. `StoreNameLookup`: 1 of 5 methods
tested (`NormaliseGameName` only). Both unchanged this loop.

**Why this weakens submission** — A source-level mutation anywhere in the tile-fill gate, the crop-window
selection's consumer, or the official-artwork replacement gate's layout comparison (`ArtworkDownloader.
cs:179`) would pass the entire suite undetected today.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Add a settable static `internal static Func<string, Task<IBuffer>> Fetcher`
property to `ArtworkDownloader`, defaulting to a lambda wrapping the existing `sharedHttpClient.GetAsync`
call; route `DownloadArtworkAsync` through it. Add `ArtworkDownloaderTests.cs` that sets `Fetcher` to a fake
returning constructed `IBuffer` fixtures (reusing `TestImages.cs`) per URL, exercising
`DownloadBestTileFillingImageAsync`'s tile-fill gate and `FindOfficialLookalikeAsync`'s official-artwork
veto (including the layout-comparison mutation named above). Not attempted this loop — the shape is now
resolved (matching the codebase's own idiom rather than introducing a new one), but building it deserves its
own tiebreak-free loop with its own fresh SPT pass on the actual diff, not bolted on inside this loop's
`FixLog`-scoped fix.

**Blast radius** — Change (this loop's actual diff): `SteamGridDB.Xbox.Tests/FixLogTests.cs` only. Avoid:
`SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs`, `SteamGridDB.Xbox/Services/Stores/
StoreNameLookup.cs`, everything else.

---

### Finding #3: Library load issues one sequential SteamGridDB round-trip per game with no bounded concurrency

**Why it matters** — Load time scales linearly with library size and network latency on the widget's
primary open path.

**What is wrong** — Read `LoadGameEntriesAsync` fresh this loop (see Finding #1): the `gameCache` `foreach`
still awaits `sgdbClient.GetGameByPlatformIdAsync` (`:562`) and the GOG/Epic/Ubisoft name fallbacks
(`:584,593,602`) one game at a time, unchanged from every prior loop. The standing operational constraint
continues to rule out attempting this: parallelising these round-trips would change observable request
count/order/timing against third-party APIs without a behavioral oracle, and the test suite still does not
cover network calls. `StoreNameLookup`'s three static caches remain unlocked, re-confirmed safe only because
every call path remains the single sequential `foreach`.

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
through loop 12.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Blocked for the duration of this run by the standing operational constraint,
unchanged. Any eventual remedy must also add locking to `StoreNameLookup`'s three static caches.

**Blast radius** — Change: `PrimaryWidget.xaml.cs` (`LoadGameEntriesAsync`), `Services/Stores/
StoreNameLookup.cs`. Avoid: `Services/Artwork/*`, `Services/SteamGridDB/*`.

### Finding #4: FixLog had zero direct test coverage — resolved this loop

**Why it matters** — `FixLog` is the widget's only diagnostic trail for artwork-selection runs; the file's
own doc comment records a real incident where the official-artwork gate silently failed across an entire
library and was found only by manually diffing artwork IDs on disk. If `FixLog` itself broke, that
diagnostic capability would disappear silently, exactly the failure mode it exists to catch for everything
else.

**What is wrong** — Surfaced loop 11 while building the Authority Map cross-check that `test_strategy`
approaching 9.0 requires. Before this loop: `Start` (`:46`), `Write` (`:56`), `SaveAsync` (`:64`) had zero
test coverage, direct or indirect (no `FixLogTests.cs` existed). **Resolved this loop**: added `FixLogTests.
cs` (4 tests) covering all three members, independently verified mutation-sensitive against three separate
production lines (`:48`, `:49`, `:58`) — see Loop 12 Result for the full mutation-verification detail.

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/FixLog.cs:46,48,49,56,58,64`
- `SteamGridDB.Xbox.Tests/FixLogTests.cs` (new this loop)

**Architectural test failed** — n/a — different category (missing-test gap per method.md Step 8's
mutation-test check).

**Dependency category** — n/a

**Leverage impact** — One call site cluster (`FixLog`'s three members), but every artwork-fix and
library-load run writes through it.

**Locality impact** — The fix was new tests only; no production code change.

**Metric signal** — 3 of 3 `FixLog` members now tested (was 0 of 3 through loop 11).

**Why this weakens submission** — Resolved: a source-level mutation in `Start`/`Write`/`SaveAsync` would
now be caught by the new tests (independently verified — see Loop 12 Result).

**Severity** — Noticeable weakness (as discovered; resolved this loop)

**ADR conflicts** — none

**Minimal correction path** — Done: `FixLogTests.cs` added using the `TempFolder` + `LogFolder`-assignment
pattern `AppliedArtworkStoreTests.cs` already establishes.

**Blast radius** — Change (this loop's actual diff): `SteamGridDB.Xbox.Tests/FixLogTests.cs` (new). No
production files touched.

## Simplification Check

- **Structurally necessary:** Adding one new test file (`FixLogTests.cs`, 106 lines, 4 tests). No Module
  removed or restructured, no Seam introduced — a pure test addition. Unified Seam Policy does not apply to
  this loop's actual diff.
- **New seam justified:** false — no protocol/port/abstraction introduced this loop. The seam analysed for
  `ArtworkDownloader` (Finding #2 / Deepening Candidate) is deferred to next loop, not built now; its shape
  is refined (a settable static delegate property, matching `FixLog.LogFolder`/`AppliedArtworkStore.
  RecordFolder`) rather than left as an open "interface vs delegate" choice.
- **Helpful simplification:** None — this loop's fix is a test-coverage addition, not a simplification.
  `simplicity` correctly held SAME.
- **Should NOT be done:** Building the `ArtworkDownloader` HTTP seam this loop without a fresh Step 2 SPT
  pass of its own on the actual diff — the shape is now resolved by this loop's Unified Seam Policy analysis,
  but the change itself (new property, three call-site threads, new test file with a hand-built fake) is a
  distinct, non-trivial diff that deserves its own loop rather than being appended to this loop's
  `FixLog`-scoped fix. Also not attempted: forcing a `domain_modeling` or `framework_idioms` finding — no new
  evidence surfaced this loop to reopen loop 11's SPT-rejection of both. Also not attempted: any further
  slice of F-001 — no new evidence surfaced this loop reopening that question.
- **Tests after fix:** No prior test exercised `FixLog.Start`, `FixLog.Write`, or `FixLog.SaveAsync`. Four
  new tests added at the class's existing public Interface, following the file's existing
  `TempFolder`-plus-settable-static-property construction pattern (`AppliedArtworkStoreTests.cs`). Verified
  mutation-sensitive directly rather than merely asserted: three separate production lines mutated in turn
  (one removed statement, one no-op'd statement, one omitted assignment), exactly the expected tests failed
  each time, then reverted.

## Improvement Backlog

1. **Add a settable static `Fetcher` delegate to `ArtworkDownloader` and cover it with
   `ArtworkDownloaderTests.cs` (F-007, remaining half)** — closes the last Authority-Map test-coverage gap.
   - Why it matters: this loop's Unified Seam Policy analysis resolved the shape ambiguity loop 11 left open
     — a settable static delegate matching the codebase's own `RecordFolder`/`LogFolder` idiom, not a new
     interface — so the next loop can execute directly rather than re-deriving the design.
   - Score impact: Test strategy's residual closes entirely (9.5 -> 10 becomes reachable once no source-backed
     residual remains); credibility may follow if the change is small and reviewer-approved cleanly.
2. **Bound concurrency in `LoadGameEntriesAsync`'s per-game SteamGridDB lookups (F-003)** — blocked for the
   duration of this run by the standing operational constraint.
   - Why it matters: removes load latency scaling linearly with library size, whenever the constraint is
     lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

- **Candidate Module:** `ArtworkDownloader.DownloadArtworkAsync` (and its `sharedHttpClient` dependency).
- **Source friction proven:** Loop 11 attempted to test `DownloadBestTileFillingImageAsync`'s
  ranking-to-selection gate with constructed `IBuffer` fixtures and found it unreachable without a live
  network round-trip, because `DownloadArtworkAsync` (`:40`) calls a private static `sharedHttpClient`
  (`:35`) directly with no seam to substitute a fixture through — see Finding #2.
- **Why the current Interface is shallow or misplaced:** `DownloadArtworkAsync`'s Interface (a URL in, an
  `IBuffer` out) already looks like the right shape for a seam, but its Implementation is inseparable from a
  live `HttpClient` — the two-adapter rule cannot be satisfied today because there is no injection point.
- **Behavior to move behind the deeper Interface:** The network fetch itself. This loop's addition to loop
  11's proposal: use a settable static `internal static Func<string, Task<IBuffer>> Fetcher` property
  defaulting to the real HTTP call, exactly matching `FixLog.LogFolder`'s and `AppliedArtworkStore.
  RecordFolder`'s existing shape, rather than a new named interface — the codebase has zero precedent for
  interface-based injection and two precedents for settable-static-property injection.
- **Dependency category:** `true-external`
- **Test surface after the change:** New `ArtworkDownloaderTests.cs` exercising
  `DownloadBestTileFillingImageAsync`'s tile-fill gate and `FindOfficialLookalikeAsync`'s official-artwork
  veto (including the layout-comparison guard at `:179`) against a fake `Fetcher` returning constructed
  `IBuffer` fixtures, closing Finding #2 entirely.
- **Smallest first step:** Add the `Fetcher` property and route `DownloadArtworkAsync` through it — the
  production `HttpClient`-backed default and a test fake enter in the same change, satisfying the
  two-adapter rule in one step.
- **What not to do:** Do not introduce a general-purpose `IHttpClient` wrapper/interface across the whole
  codebase — `StoreNameLookup` and `SteamGridDbClient` have their own `HttpClient` usage with no proven
  friction; scope the seam to `ArtworkDownloader`'s three entry points only. Do not build a recording-only
  stub that just logs calls — the fake must return real per-URL bytes so the tile-fill/layout logic actually
  executes against it (Unified Seam Policy's behavior-faithful requirement).

## Builder Notes

1. **Pattern:** When a prior loop leaves a seam proposal open-ended ("a delegate or a narrow interface"),
   check whether the codebase already has a working idiom for the exact same problem shape (external
   dependency needs a test substitute) before inventing a new one. A settable static property defaulting to
   the real implementation is a full Seam (satisfies the two-adapter rule once a real fake is added) with far
   less ceremony than a named interface, and this codebase already uses it twice.
   - How to recognize: a Deepening Candidate proposes "`Func<T>` or a narrow `IFoo`" as alternatives without
     picking one, and the surrounding codebase has an existing settable-static-property pattern for a
     structurally identical problem (ambient platform dependency needing a test double).
   - Smallest coding rule: grep the codebase for existing test-injection patterns (`internal static.*{ get;
     set; }` properties defaulting to a real ambient value) before designing a new seam shape; match the
     existing idiom unless it demonstrably cannot fit.
   - Stack example: C# - `FixLog.LogFolder` and `AppliedArtworkStore.RecordFolder` are both `internal static
     StorageFolder` properties defaulting to `ApplicationData.Current.LocalFolder`; `ArtworkDownloader.
     Fetcher` (proposed) would be the same shape for `HttpClient` instead of `StorageFolder`.

2. **Pattern:** A stateful three-member static class (reset / append / flush-to-disk) needs a distinct
   mutation per member to prove real coverage, not one test per public method — the reset-discipline and the
   append-ordering are separate hazards from the same class.
   - How to recognize: a class shaped like `Start()`/`Write()`/`SaveAsync()` — one method establishes a
     fresh run, one accumulates state, one persists it — where a bug in any one is independently invisible to
     tests targeting only the others.
   - Smallest coding rule: write one test asserting the reset method actually discards a *previous* run's
     state (not just that a fresh run works), one asserting the accumulator preserves order and multiplicity,
     and one asserting the persist step honors any per-call configuration (here, the file name). Verify each
     by mutating only that member's line and confirming only the matching test(s) fail.
   - Stack example: C# - removing `FixLog.cs:49`'s `lines.Clear()` was caught only by the test that starts a
     second run and checks the first run's line is absent, not by the test that only checks header content of
     a single run.

3. **Pattern:** Re-litigating a long-held score without new source evidence just re-derives the same
   conclusion at the cost of a loop's investigation budget. When a prior loop's SPT-rejection cited a specific
   doc comment or source fact as the reason, confirming the cited file is still byte-identical (`git diff
   --stat` against the SHA that rejection was based on) is sufficient re-verification; it does not need a
   fresh line-by-line re-read every loop.
   - How to recognize: a scorecard dimension has been SAME for 2+ consecutive loops with an explicit,
     source-cited SPT-rejection already on record, and this loop's diff does not touch the cited files.
   - Smallest coding rule: `git diff --stat <sha-of-last-real-investigation> HEAD -- <cited files>`; empty
     output is sufficient proof the SPT-rejection still holds without re-deriving it from scratch.
   - Stack example: C# - `SteamGridDbGame.cs`/`ArtworkSource.cs`/`SteamGridDbClient.cs` confirmed empty-diff
     since loop 11's `85b5279`, so `domain_modeling`/`framework_idioms` held SAME on citation rather than a
     full re-read this loop.

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. Promoting `test_strategy` to 9.5 (not holding at 9.0) on a *queued* rather than *accepted* residual — a
   stricter reviewer could argue that `ArtworkDownloader`'s gate is reachable from the primary
   automatic-artwork-selection flow (closer to the severity anchors' "primary user flow" language than a
   typical Noticeable-weakness residual), and that 9.5 should be reserved for residuals further along toward
   closure than "named and queued, not yet designed in code." I judged `architecture-rubric-scoring.md`'s
   explicit statement that queued residuals are compatible with 9.5 (only `HALT_SUCCESS` requires accepted)
   as controlling; a stricter reader could land at 9.0 instead.
2. My conclusion that the `Fetcher`-property seam design passes the Unified Seam Policy — I verified the
   *shape* matches an existing successful idiom in this codebase, but I have not built the fake, so I cannot
   yet confirm it is genuinely behavior-faithful (correctly simulating failure/timeout paths, not just
   success-path bytes) rather than a disguised recording stub. That confirmation is next loop's job.
3. Treating `TESTING.md`'s "What is not covered" section as authoritative proof the Authority Map's scope is
   complete — I did not independently re-derive every mutable-state owner in the codebase from a fresh Step 2
   walk this loop (I relied on this loop's own broader `grep` for static mutable fields plus this document);
   a type I did not check with either method could still hold an uncredited gap.

## Final Judge Narrative

Place, not win. Ground truth was clean going in (both gates green, zero source drift since loop 11's commit)
and clean coming out (125/125 tests, MSBuild exit 0). This loop closed the queued Priority 1 (F-008) with
four new mutation-verified tests on `FixLog` and zero production code change — the same idiom
(`TempFolder` + settable-static-folder-property) that already worked for `AppliedArtworkStore`, applied a
second time with the same discipline. That closure is not merely incremental: loop 11 stated in its own
scorecard reasoning that having two open Authority-Map gaps instead of "at most one" was the *entire* reason
`test_strategy` held at 8.5 rather than crossing 9.0. With `FixLog` closed, exactly one gap remains, named,
evidenced, and queued — which the rubric's own 9.5+ Threshold section treats as sufficient for 9.5. This is a
genuine structurally-proven UP, not a manufactured one to satisfy a stagnation counter: the same conclusion
follows mechanically from loop 11's own stated criterion regardless of any external pressure, and the loop
was honest above about where a stricter reader could reasonably disagree (Scorecard humility check item 1).
Separately, this loop discharged its dispatch instruction to hold loop 11's queued `ArtworkDownloader` HTTP
seam hard against the Unified Seam Policy before it could be selected as a future Priority 1: it survives,
but only in a specific idiom-matched shape (a settable static delegate, not a new interface), which is now
recorded precisely enough that next loop can execute rather than re-derive. Runtime ownership and concurrency
are unaffected and exactly as trustworthy as loop 11 left them — this loop's diff touches only test code, and
`git diff --stat` confirms every production file byte-identical since loop 11's commit. Tests reduce
regressions more than last loop measured on this specific class: `FixLog`'s silent-failure risk (the same
failure mode its own doc comment records happening for real, once, undetected until a manual disk diff) is
now caught by three independently mutation-verified assertions. Future work risks nothing new from
overengineering — this loop's fix added zero production abstraction, and the one abstraction it resolves the
shape of (the HTTP-fetch seam) is deferred, not built opportunistically. Backlog is not empty
(`ArtworkDownloader` seam, F-003), so `CONTINUE`.

## Loop 12 Result

Added one new test file, `SteamGridDB.Xbox.Tests/FixLogTests.cs` (106 lines, 4 tests:
`Writes_the_header_and_every_line_to_disk_in_order`, `Starting_a_new_run_discards_the_previous_runs_lines`,
`Saves_under_the_file_name_given_to_start`, `Defaults_to_last_fix_log_when_no_file_name_is_given`), closing
finding F-008 (stable_id `F-008`, "FixLog has zero direct test coverage"). No production code changed in the
final diff (`git diff --stat` shows only the new test file).

**What proves the change is honest:** `run-tests.ps1`: 121 passed before, 125 passed after (delta is exactly
the 4 new tests). MSBuild: exit 0, both runs. Mutation-sensitivity independently verified three times, not
just asserted: (1) temporarily removed `FixLog.cs:49`'s `lines.Clear();`, re-ran the full suite, got exactly
1 failure (`Starting_a_new_run_discards_the_previous_runs_lines`), reverted; (2) temporarily removed
`FixLog.cs:48`'s `fileName = file;` assignment, re-ran, got exactly 3 failures (every test that asserts on
the file-name parameter: `Writes_the_header_and_every_line_to_disk_in_order`,
`Saves_under_the_file_name_given_to_start`, `Starting_a_new_run_discards_the_previous_runs_lines`), reverted;
(3) temporarily no-op'd `FixLog.cs:56-59`'s `Write` body (removed `lines.Add(line);`), re-ran, got exactly 2
failures (`Writes_the_header_and_every_line_to_disk_in_order`,
`Starting_a_new_run_discards_the_previous_runs_lines`), reverted; 125/125 re-confirmed green after each
revert, and `git diff --stat -- SteamGridDB.Xbox/Services/Artwork/FixLog.cs` confirmed empty (byte-identical
to HEAD) before the implementation review and commit.

**Risk boundary evidence (Meta-Rule 4):** none — this fix crosses no isolation/Sendable/conditional-
compilation/cross-file-visibility/lock-ordering boundary. It is a pure test-only addition; no production
type, visibility, or concurrency primitive changed.

**Targeted finding status:** `resolved` — F-008's Claim (zero direct test coverage on `FixLog`) no longer
holds: all three members now have direct, mutation-verified tests.

**Unintended scorecard regression:** none. `test_strategy` moved UP (8.5 -> 9.5) with structural proof
(the new test file, the three mutation verifications, and loop 11's own stated "at most one gap" criterion
now being met). No other dimension changed — this loop's diff touches only test code, and every production
file is confirmed byte-identical to HEAD via `git diff --stat`.
