### Discovery (see Loop 7 Discovery refresh)

Resumed from loop 13's `CONTINUE` at commit `6e83b10`. Working tree was clean at dispatch. Both ground-truth
gates re-run fresh before touching anything, per dispatch instructions:

- `powershell -NoProfile -ExecutionPolicy Bypass -File ./run-tests.ps1` — **131 passed, 0 failed** before this
  loop's fix, **131 passed, 0 failed** after (no test file changed - this loop's fix targets a network-bound
  method with no seam, matching `StoreNameLookup.cs`'s standing test boundary).
- `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
  SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo` — exit
  0 before and after.
- `git log 6e83b10..HEAD` was empty before this loop's own edits; HEAD matched loop 13's commit exactly.
- Selected lens: Generic (C#/.NET / UWP-hosted WinUI stack).

**Dispatch framing for this loop**: `test_strategy` (9.5) and `simplicity` (8.5) have absorbed every loop's
Priority 1 since loop 11; the other seven dimensions sat flat at 5.5-7.5 with `data_flow` (6.0) and
`architecture_quality` (7.0) never once the target of a fix. This loop's mandate was to genuinely investigate
those two before defaulting to loop 13's queued test-coverage item. Both were investigated fresh from source
this loop (see Findings F-009 and F-001 below); `data_flow` yielded a real, source-backed fix that passes the
Simplify Pressure Test, `architecture_quality` did not (re-confirmed, see Finding F-001).

**Blind-critic ordering note**: this loop's independent scorecard below was re-derived from direct source reads
(`StoreNameLookup.cs`, `EpicLibrary.cs`, `AppliedArtworkStore.cs`, `AsyncLazyCache.cs`, `PrimaryWidget.xaml.cs`
all read fresh this loop) before `CURRENT_REVIEW.md`'s prior verdict/scorecard and `REVIEW_HISTORY.md`'s tail
were consulted for delta/oscillation bookkeeping, per the dispatch's blind-critic ordering instruction.

### Loop Counter

Loop 14 of 15

### System Flag

[STATE: CONTINUE]

---

## Contest Verdict

**Promising, but architecturally immature.**

Both gates green before and after (131/131 tests, MSBuild exit 0). This loop's dispatch named a real pattern:
eight of the last nine loops moved only `test_strategy`/`simplicity`, while `data_flow` and
`architecture_quality` sat unmoved since loop 3 and loop 7 respectively. Both got a genuine investigation this
loop rather than a repeat of the standing text. `architecture_quality` held: `LoadGameEntriesAsync`'s
image-resolution block (`:480-538`) and network-resolution block (`:551-629`) were read fresh looking for a
slice separable the way `ManifestEntryIdentity` was in loop 9 - neither qualifies, both are I/O-heavy with no
decision logic ahead of the fetch, the same shape that has correctly kept `ArtworkDownloader.DownloadArtworkAsync`
itself out of every extraction this run. `data_flow` did not hold: `StoreNameLookup.cs`'s `ubisoftGameLookupCache`
field and `LoadUbisoftGameListAsync` method - a fourth static cache, previously uncounted in the standing
"three unlocked caches" scorecard text - hand-rolled an unlocked, unsynchronized duplicate of the codebase's
own `AsyncLazyCache<T>` (the exact generic type `EpicLibrary.cs` and `AppliedArtworkStore.cs` already use for
this identical shape, since loop 6 closed F-004 for those two). Replaced it with a call into that existing,
already-tested type. This is the first loop in this run's history to land a source-level change against
`data_flow`'s own named residual rather than holding it flat for eleven consecutive loops. `data_flow` moves
6.0 -> 6.5, structurally proven by the diff; the anchor is still far from met (three per-key caches and
`EpicLibrary`'s ambient environment-variable fallback remain unlocked/ambient), so the residual narrows, it
does not close.

## Scorecard (1-10)

- **Architecture quality:** 7.0 | SAME | Re-read `LoadGameEntriesAsync` (`PrimaryWidget.xaml.cs:332-611`)
  directly this loop, confirmed byte-identical since loop 9's commit `05501e0` via `git diff --stat 05501e0
  HEAD -- PrimaryWidget.xaml.cs` (empty) - unchanged for five loops running (9 through 14). This loop's
  dispatch asked for a genuine re-investigation, not a rubber-stamp: read the image-resolution block (`:480-538`,
  `StorageFolder.GetFolderFromPathAsync`/`ArtworkFiles.HasBackupAsync`/`CreateThumbnailAsync`) and the
  network-resolution block (`:551-629`, the SteamGridDB call plus the GOG/Epic/Ubisoft fallbacks) looking for a
  slice separable the way `ManifestEntryIdentity` was in loop 9. Neither qualifies: both blocks are I/O-heavy
  with no decision logic ahead of the fetch - extracting either into its own method would relocate I/O, not add
  testability or reduce coupling, the same reasoning that has correctly kept `ArtworkDownloader.DownloadArtworkAsync`
  out of every extraction this run (fails Simplify Pressure Test Q2, smallest honest fix - see Finding F-001).
  This loop's actual fix targeted a different, smaller cluster (`StoreNameLookup.cs`) and does not touch this
  file - confirmed via `git diff --stat` (only `StoreNameLookup.cs` in this loop's diff).
- **State management and runtime ownership:** 7.0 | SAME | `AppliedArtworkStore.cs` and `FixLog.cs` production
  code confirmed byte-identical to HEAD this loop (`git diff --stat` shows only `StoreNameLookup.cs` touched).
  `StoreNameLookup`'s caches are shared static state, not per-instance runtime/presentation state; this run has
  consistently scored that class's caches under `data_flow` since the loop-3 file split, not `state_management`,
  and this loop's fix follows that convention.
- **Domain modeling:** 5.5 | SAME | Not re-litigated this loop per this run's standing finding (loop 11
  SPT-rejected a construction-time-invariant fix on the wire DTOs, citing `SteamGridDbGame.cs:36-39`'s own doc
  comment on the `DataContractJsonSerializer` platform constraint). Confirmed via `git diff --stat 85b5279 HEAD
  -- SteamGridDbGame.cs ArtworkSource.cs` (empty) that no new evidence exists this loop to reopen that question.
- **Data flow and dependency design:** 6.5 | UP | `StoreNameLookup.cs`'s `ubisoftGameLookupCache` field and
  `LoadUbisoftGameListAsync` method (old `:33`, `:228-291`) - a fourth static cache, hand-rolling its own
  unlocked check-null/fetch/populate lazy-load, never previously counted in this run's standing "three unlocked
  caches" scorecard text (`gogNameCache`/`epicNameCache`/`nameMatchCache`, `:27-31`, per every loop back to
  loop 3) - are gone from current source. Replaced with a `private static readonly
  AsyncLazyCache<Dictionary<string,string>>` backed by a new `gate` `SemaphoreSlim` (`StoreNameLookup.cs:40-42`),
  the same shared, locked, 32-caller-stress-tested abstraction (`Services/AsyncLazyCache.cs`, unchanged)
  `EpicLibrary.cs` (`:43-44`) and `AppliedArtworkStore.cs` (`:34-35`) already use for the identical
  single-value-loaded-once shape (see Finding F-009). Structural proof: `git diff -- SteamGridDB.Xbox/Services/
  Stores/StoreNameLookup.cs` (21 insertions, 23 deletions, net -2 lines) at this loop's commit; direct read of
  the post-diff file confirms `ubisoftGameLookupCache` and the old `LoadUbisoftGameListAsync` are both gone
  (`grep -c` zero hits for both symbols). First loop in this run's history to land a source-level change against
  `data_flow`'s own named residual rather than holding it flat (unchanged loop 3 through loop 13, eleven
  loops). Residual narrows, does not close: `gogNameCache`/`epicNameCache`/`nameMatchCache` (`:29-30,34`) remain
  unlocked per-key dictionaries - a different shape `AsyncLazyCache<T>` does not fit (it caches one value, not
  a per-key map) - and `EpicLibrary.cs`'s ambient `Environment.GetEnvironmentVariable("ProgramData")` fallback
  (`:30-32`) is untouched. 7-anchor ("ambient state reachable from multiple modules" limited to one or two
  documented cases) is not met: at least four ambient/unlocked concerns remain.
- **Framework / platform best practices:** 6.0 | SAME | Not re-litigated this loop per this run's standing
  finding (loop 11 SPT-rejected treating the `DataContractJsonSerializer` / `Windows.Data.Json` split in
  `SteamGridDbClient.cs` as a framework-idiom violation, citing the class's own doc comment at `:137-141`).
  Confirmed via `git diff --stat 85b5279 HEAD -- SteamGridDbClient.cs` (empty) that no new evidence exists this
  loop.
- **Concurrency and runtime safety:** 6.5 | SAME | F-003's sequential per-game awaits
  (`PrimaryWidget.xaml.cs:562,584,593,602`) unaffected - `git diff --stat` confirms `PrimaryWidget.xaml.cs`
  untouched this loop. This loop's `StoreNameLookup.cs` fix adds a lock to a cache that was already safe under
  the current single-caller sequential access pattern (no behavior change, no new concurrent caller introduced,
  no change to request count/order/timing) - it does not touch F-003's blocked parallelization question, so
  concurrency's own residual is unaffected. Scored entirely under `data_flow`, per this run's anti-double-
  counting convention.
- **Code simplicity and clarity:** 8.5 | SAME | This loop's diff (`StoreNameLookup.cs`, 21 insertions/23
  deletions, net -2 lines) removes a duplicate hand-rolled cache implementation - a real subtractive
  simplification - but per this run's established anti-double-counting convention (explicit in loop 11-13's own
  scorecard reasoning) the credit is scored entirely under `data_flow`, the dimension whose own named residual
  this fix directly narrows. No ceremony added; net line count dropped.
- **Test strategy and regression resistance:** 9.5 | SAME | Unaffected this loop - `git diff --stat` confirms
  `ArtworkDownloader.cs`, `ArtworkSignature.cs`, and `ArtworkDownloaderTests.cs` all untouched. The 9-anchor is
  still met (Authority-Map cross-check passes for every concern; the same one narrow gap remains, unchanged from
  loop 13). Loop 13's queued residual (`ArtworkSignatureTests.cs` + the `officialArtworkFloor` gate extraction)
  was deliberately deferred this loop in favor of the `data_flow` investigation the dispatch asked for - it
  remains Priority 1 in this loop's backlog, carried forward unresolved, not attempted, not abandoned.
- **Overall implementation credibility:** 7.5 | SAME | Consistent with this run's anti-double-counting
  convention: this loop's fix is credited entirely to `data_flow`. `PrimaryWidget.xaml.cs`'s 1,950 lines remain
  unverified by anything but inspection outside the small tested slices. `StoreNameLookup.cs`'s network-bound
  methods (including the now-fixed `GetUbisoftGameNameAsync`) remain untested by direct test - network-bound, no
  seam, consistent with `TESTING.md`'s documented boundary - so this fix's correctness rests on the two green
  gates plus direct source inspection (grep-confirmed no orphaned caller of the removed symbols), not a new
  assertion.

## Authority Map

Re-emitted this loop per G24 (mandatory whenever `test_strategy >= 9`) and because this loop's Priority-1
finding (F-009) touches the third concern's ownership.

- **Concern:** Applied-artwork record (which SteamGridDB artwork ID was written to each tile).
  - **Owner:** `AppliedArtworkStore` (`Services/Artwork/AppliedArtworkStore.cs`).
  - **Allowed writers:** `SetAsync`, `ClearAsync` - both funnel through the private `UpdateAsync`, gated by the
    shared `SemaphoreSlim gate`.
  - **Readers:** `GetAsync` (also gated).
  - **Persistence seam:** `applied-artwork.json` in `RecordFolder` (defaults to `ApplicationData.Current.
    LocalFolder`).
  - **Async mutation entry points:** `SetAsync`, `ClearAsync`.
  - **Verdict:** Single and clear. Direct test: `AppliedArtworkStoreTests.cs`. Unaffected this loop.

- **Concern:** Fix-run diagnostic log (what happened during the last "fix library" pass).
  - **Owner:** `FixLog` (`Services/Artwork/FixLog.cs`).
  - **Allowed writers:** `Start`, `Write` - called from `PrimaryWidget` and `ArtworkDownloader.
    FindOfficialLookalikeAsync`.
  - **Readers:** none in-process; `SaveAsync` writes to disk for the user to inspect externally.
  - **Persistence seam:** `last-fix.log` / `last-load.log` in `LogFolder`.
  - **Async mutation entry points:** `SaveAsync`.
  - **Verdict:** Single and clear. Direct test: `FixLogTests.cs`. Unaffected this loop.

- **Concern:** Store-name lookup caches (GOG/Epic/Ubisoft names, SteamGridDB name-match results) and the
  artwork download/selection gate.
  - **Owner:** `StoreNameLookup` (`Services/Stores/StoreNameLookup.cs`) and `ArtworkDownloader` (`Services/
    Artwork/ArtworkDownloader.cs`).
  - **Allowed writers:** `StoreNameLookup.GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/
    `FindGameByNameAsync`/`GetUbisoftGameNameAsync` (this loop: now routes through the shared
    `AsyncLazyCache<T>` instead of a hand-rolled unlocked lazy-init - see Finding F-009); `ArtworkDownloader`
    holds no mutable state of its own but is the sole gate deciding which downloaded candidate becomes the
    tile.
  - **Readers:** the same four `StoreNameLookup` writers (read-through cache).
  - **Persistence seam:** none - in-memory only, process lifetime.
  - **Async mutation entry points:** the four `StoreNameLookup` writers; `ArtworkDownloader.
    DownloadArtworkAsync`/`DownloadBestTileFillingImageAsync`/`FindOfficialLookalikeAsync`.
  - **Verdict:** Single and clear ownership. One of four `StoreNameLookup` caches moved from unlocked to
    gated this loop (`ubisoftGameListCache`); three remain unlocked per-key dictionaries
    (`gogNameCache`/`epicNameCache`/`nameMatchCache`) - a different shape, not a drop-in fix, queued as
    backlog item 2. Test gap unchanged from loop 13: `ArtworkDownloaderTests.cs` directly tests the
    replacement gate's colour/layout decision; the three async entry points, `StoreNameLookup`'s network
    calls, and `ArtworkSignature.cs` remain untested (Priority 1, F-007).

## Strengths That Matter

- `ArtworkSource`'s private-constructor-plus-factory-method design (`Services/SteamGridDB/
  ArtworkSource.cs:15-51`) still makes "neither a platform-ID nor a game-ID" unrepresentable - unaffected this
  loop, confirmed via `git diff --stat`.
- This loop closed a residual the standing scorecard text had named unchanged for eleven consecutive loops
  (`data_flow`'s unlocked static caches, loop 3 through loop 13) by finding a fourth instance of the same smell
  the standing text had never counted, and fixing it by pure reuse of an in-house type rather than inventing
  anything new - the smallest honest fix available, not a bigger consolidation project.
- `AsyncLazyCache<T>`'s original design (taking the caller's own lock as a constructor argument rather than
  owning a private one, per its own doc comment at `Services/AsyncLazyCache.cs:12-15`) let this loop's fix reach
  a third call site with zero changes to the shared type itself and zero new test infrastructure - the
  abstraction earned its keep a second time since loop 6.

## Findings

### Finding #1: StoreNameLookup.LoadUbisoftGameListAsync hand-rolled an unlocked duplicate of the codebase's own AsyncLazyCache&lt;T&gt;

**Why it matters** - A fourth static cache in this file duplicated, by hand, the exact load-once-and-remember
job the codebase already extracted into a generic, tested type, so a future contributor debugging or extending
Ubisoft lookup has two different caching idioms to learn for the identical problem, and the untested copy
stayed unlocked while both of its siblings, EpicLibrary and AppliedArtworkStore, already moved to the shared,
locked, 32-caller-stress-tested abstraction.

**What is wrong** - `StoreNameLookup.cs`'s `ubisoftGameLookupCache` field (old `:33`) and
`LoadUbisoftGameListAsync` (old `:228-291`) implemented their own check-null/fetch/populate lazy-load by hand,
unlike `EpicLibrary.cs`'s `nameCache` (`:43-44`) and `AppliedArtworkStore.cs`'s `appliedCache` (`:34-35`), both
of which already call the shared `Services/AsyncLazyCache.cs` (`AsyncLazyCache<T>`, introduced loop 6 to close
F-004 for this exact shape). Unlike EpicLibrary's and AppliedArtworkStore's caches, StoreNameLookup's hand-rolled
copy took no lock at all - the file's own doc comment (`:19-22`) states every cache here is shared across the
whole process, yet this one specific cache had zero synchronization, silently relying on its only caller
(`PrimaryWidget.LoadGameEntriesAsync`'s single sequential `foreach`) never reaching it concurrently. Prior
loops' F-004 investigation (loops 3-5) explicitly checked this file and noted it used "a simpler unlocked
lazy-init, not the locked skeleton" being consolidated at the time - correctly distinguishing it from F-004's
Claim - but no loop since `AsyncLazyCache<T>` was introduced (loop 6) asked whether it could replace this cache
too. Fixed this loop: replaced both with a `private static readonly AsyncLazyCache<Dictionary<string,string>>`
backed by a new `gate` `SemaphoreSlim` (`StoreNameLookup.cs:40-42`), preserving the original's do-not-cache-a-
failed-or-empty-fetch semantics (the loader returns `null` on failure/empty, matching `AsyncLazyCache<T>`'s
"null means not loaded yet" contract).

**Evidence**
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:33 (pre-fix)`
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:228-291 (pre-fix)`
- `SteamGridDB.Xbox/Services/Stores/EpicLibrary.cs:43-44`
- `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:34-35`
- `SteamGridDB.Xbox/Services/AsyncLazyCache.cs`

**Architectural test failed** - Deletion test - the old hand-rolled lazy-load's complexity vanishes entirely
when deleted, because `AsyncLazyCache<T>` already provides identical, already-tested behavior; nothing
reappears at the one call site. Proof the old code was pure duplicate complexity, not load-bearing complexity.

**Dependency category** - n/a (not a Coupling & Leakage finding; a duplicate in-process abstraction, not a
domain/framework or domain/persistence leak).

**Leverage impact** - One caller (`GetUbisoftGameNameAsync`); the fix reduces the number of competing caching
idioms in this file from two to one, so any future cache added to this file has one idiom to copy, not two.

**Locality impact** - Change scoped entirely to `StoreNameLookup.cs` - no other file's caching logic touched.

**Metric signal** - `StoreNameLookup.cs`: 317 -> 315 lines (net -2); 4 static caches, 1 of 4 unlocked before
this fix, 0 of 4 after.

**Why this weakens submission** - Two different idioms solving the identical load-once-and-remember problem
inside one 317-line file work against Locality: a maintainer fixing or extending Ubisoft lookup must first work
out which of the two patterns applies, and the unlocked one is the pattern most likely to get copied forward
into a new cache by a maintainer who does not know the locked idiom is the house style.

**Severity** - Noticeable weakness

**ADR conflicts** - none

**Minimal correction path** - Replace `ubisoftGameLookupCache` and `LoadUbisoftGameListAsync` with a `private
static readonly AsyncLazyCache<Dictionary<string,string>>` backed by a new `gate` `SemaphoreSlim`, matching
EpicLibrary's and AppliedArtworkStore's own idiom exactly; the loader returns `null` on a failed or empty fetch
so the do-not-cache-a-miss semantics documented in the original code are preserved. No new type needed -
`AsyncLazyCache<T>` already exists and is stress-tested (`AsyncLazyCacheTests.cs`, 32 concurrent callers). Done
this loop.

**Blast radius** - Change (this loop's actual diff): `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`.
Avoid: `SteamGridDB.Xbox/Services/Stores/EpicLibrary.cs`, `SteamGridDB.Xbox/Services/Artwork/
AppliedArtworkStore.cs`, `SteamGridDB.Xbox/Services/AsyncLazyCache.cs`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

---

### Finding #2: PrimaryWidget.xaml.cs still merges UI event handling, image decode, network resolution, and bulk-operation orchestration behind zero Interface boundary

**Why it matters** - The largest file in the repo across every prior loop's Discovery continues to bundle
several structurally distinct concerns with no Module boundary between most of them, so a change to any one
risks disturbing the others.

**What is wrong** - Re-read `LoadGameEntriesAsync` directly this loop (`PrimaryWidget.xaml.cs:332-611`), the
first fresh re-investigation of this file's separability since loop 8 rather than a re-confirmation of the
standing text. Read the image-resolution block (`:480-538`: `imageFilePath`/`imageFolder` computation,
`ArtworkFiles.HasBackupAsync`, `CreateThumbnailAsync`) and the network-resolution block (`:551-629`: the
SteamGridDB lookup plus the GOG/Epic/Ubisoft/name-search fallbacks) specifically looking for a slice separable
the way `ManifestEntryIdentity` was in loop 9 (a pure computation pulled out of the loop body). Neither
qualifies: both blocks are I/O-heavy (`StorageFolder` reads, HTTP calls) with no decision logic sitting ahead of
the fetch to extract - the same shape that has correctly kept `ArtworkDownloader.DownloadArtworkAsync` itself
out of every extraction attempted this run (loop 13's Finding #2: "it has no decision logic to extract, only the
fetch"). Pulling either block into its own method would relocate I/O verbatim, not add testability or reduce
coupling, so it fails Simplify Pressure Test Q2 (smallest honest fix that actually fixes ambiguity) the same way
a bare method-extraction-for-its-own-sake would. This loop's own diff touches only `StoreNameLookup.cs`, so
none of this changed; confirmed via `git diff --stat 05501e0 HEAD -- PrimaryWidget.xaml.cs` (empty) that the
file has been byte-identical for five loops running (9 through 14).

**Evidence**
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:332-611`
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:480-538`
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:551-629`

**Architectural test failed** - n/a - different category (ownership/coupling sprawl for what remains; the
candidate extractions were rejected via Simplify Pressure Test Q2, not an architectural test on an existing
Module).

**Dependency category** - n/a

**Leverage impact** - Unaffected this loop.

**Locality impact** - Unaffected this loop.

**Metric signal** - `PrimaryWidget.xaml.cs`: 1,950 lines, unchanged.

**Why this weakens submission** - Ownership of the concerns still merged in `PrimaryWidget` remains untraceable
from any single Module besides the UI class itself.

**Severity** - Serious deduction

**ADR conflicts** - none

**Minimal correction path** - No further split is queued this loop: both candidate blocks are I/O-only with no
separable decision logic, re-confirmed by direct read this loop rather than inherited from the standing text.
Consistent with this run's discipline since loop 8: no next slice is proposed without first re-verifying against
current source and passing Simplify Pressure Test.

**Blast radius** - Change (only if a future loop finds a genuinely separable slice, with a fresh SPT first):
`PrimaryWidget.xaml.cs`. Avoid: `Services/Artwork/*`, `Services/Stores/*`, `Services/SteamGridDB/*`,
`Services/Library/*`.

---

### Finding #3: ArtworkDownloader's fetch/orchestration entry points and StoreNameLookup's four writers remain untested

**Why it matters** - `DownloadBestTileFillingImageAsync` decides which candidate image the widget applies as
artwork when auto-selecting; a wrong crop, a broken tile-fill check, or an inverted layout-quality guard would
ship visibly bad artwork with no test catching it. This is the sole remaining Authority-Map test-coverage gap,
unchanged since loop 13 (deliberately deferred this loop - see Discovery).

**What is wrong** - Unaffected by this loop's fix. `git diff --stat` confirms `ArtworkDownloader.cs`,
`ArtworkSignature.cs`, and `ArtworkDownloaderTests.cs` are all untouched. `DownloadArtworkAsync` (`:40`) still
calls a private static `sharedHttpClient` directly with no seam; `DownloadBestTileFillingImageAsync` and
`FindOfficialLookalikeAsync` still orchestrate that network call, so those three async entry points remain
untested, as do `StoreNameLookup`'s four network-bound writer methods and `ArtworkSignature`'s
`ColourMatch`/`LayoutMatch`/`CreateAsync` (zero test file exists for `ArtworkSignature.cs`).

**Evidence**
- `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs:35,40,71,122`
- `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` (four network-bound writers)
- `SteamGridDB.Xbox/Services/Artwork/ArtworkSignature.cs` (zero test file)

**Architectural test failed** - Interface-as-test-surface - the remaining orchestration/fetch surface still
reaches past its Interface into a live, non-injectable network call.

**Dependency category** - `true-external`

**Leverage impact** - One call site cluster (`ArtworkDownloader`'s gate), the function every automatic artwork
pick and manual apply goes through after ranking.

**Locality impact** - Unaffected this loop.

**Metric signal** - Unchanged from loop 13: `ArtworkDownloader` 1 of 3 async entry points has a directly-tested
internal decision; `StoreNameLookup` 1 of 5 methods tested; `ArtworkSignature` 0 of 3 members tested.

**Why this weakens submission** - A source-level mutation in the download-loop's fallback-candidate capture, the
`MaxCandidates`/`chosenIndex + 1` loop boundaries, `StoreNameLookup`'s four writers, or `ArtworkSignature`'s
`ColourMatch`/`LayoutMatch` computations would pass the entire suite undetected today.

**Severity** - Noticeable weakness

**ADR conflicts** - none

**Minimal correction path** - Continue the zero-seam idiom loop 13 proved out: add `ArtworkSignatureTests.cs`
testing `ColourMatch`/`LayoutMatch`/`CreateAsync` directly against `TestImages.cs` fixtures, and extract the
`officialArtworkFloor` early-exit gate (`ArtworkDownloader.cs:146`) as a second small tested predicate.

**Blast radius** - Change (next loop): `SteamGridDB.Xbox/Services/Artwork/ArtworkDownloader.cs`,
`SteamGridDB.Xbox.Tests/ArtworkSignatureTests.cs` (new). Avoid: `SteamGridDB.Xbox/Services/Stores/*`,
`SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

## Simplification Check

- **Structurally necessary:** Replacing `StoreNameLookup`'s hand-rolled, unlocked `ubisoftGameLookupCache`/
  `LoadUbisoftGameListAsync` with the shared `AsyncLazyCache<T>` - passes the Deletion test (the old hand-rolled
  logic's complexity vanishes entirely when deleted; `AsyncLazyCache<T>` already provides identical,
  already-tested behavior, so nothing reappears at the one call site).
- **New seam justified:** false - `AsyncLazyCache<T>` is an existing, already-proven generic type reused at one
  more call site, not a new Seam. Nothing new is swapped at runtime.
- **Helpful simplification:** `StoreNameLookup.cs` shrank 317 -> 315 lines (net -2) while removing one of two
  competing caching idioms in the file; the removed idiom was also the one unlocked copy among the file's four
  static caches.
- **Should NOT be done:** Attempting the same fix on `gogNameCache`/`epicNameCache`/`nameMatchCache` this loop -
  they are per-key dictionaries, not `AsyncLazyCache<T>`'s single-value shape, so the fix is not a drop-in reuse
  and needs its own Simplify Pressure Test pass (queued, not built - see Improvement Backlog item 2). Also not
  attempted: `LoadGameEntriesAsync`'s further split (re-investigated fresh this loop, still fails SPT - see
  Finding #2, no separable I/O-free decision slice exists in either candidate block). Also not attempted: forcing
  a `domain_modeling` or `framework_idioms` finding, or building `ArtworkSignatureTests.cs` (deliberately
  deferred to next loop's Priority 1, F-007) - this loop's genuine-investigation mandate covered `data_flow` and
  `architecture_quality`, and `data_flow` yielded the real Simplify-Pressure-Test-passing fix first.
- **Tests after fix:** No test file exists for `StoreNameLookup.cs`'s network-bound methods before or after this
  fix - network-bound, no seam, consistent with `TESTING.md`'s documented boundary and this file's standing
  practice (`StoreNameLookupTests.cs` covers only the pure `NormaliseGameName`). No old tests existed to delete
  under Replace-don't-layer since none existed for the replaced method. `AsyncLazyCache<T>` itself is unchanged
  and already covered by `AsyncLazyCacheTests.cs` (pre-existing, 4 tests including a 32-concurrent-caller stress
  test) - the new call site inherits that coverage's guarantee without needing a new test file, since the
  Interface being reused, not deepened, is already tested at its own boundary.

## Improvement Backlog

1. **Add `ArtworkSignatureTests.cs` and extract the `officialArtworkFloor` gate as a second tested predicate
   (F-007, carried forward from loop 13, deliberately deferred this loop)** - the zero-seam idiom loop 13 proved
   out, applied to the two remaining pure-computation slices of the artwork download/selection gate.
   - Why it matters: `ArtworkSignature.ColourMatch`/`LayoutMatch` are pure, network-free and currently have zero
     test coverage. The `officialArtworkFloor` early-exit (`ArtworkDownloader.cs:146`) is the same shape as the
     gate loop 13 extracted.
   - Score impact: `test_strategy`'s residual narrows further; does not by itself reach 10.
2. **Extend F-009's fix to StoreNameLookup's three remaining unlocked per-key caches
   (`gogNameCache`/`epicNameCache`/`nameMatchCache`)** - a new slice of the same `data_flow` residual, not yet
   built.
   - Why it matters: these three caches remain unlocked, unlike the now-fixed fourth. Unlike
     `ubisoftGameLookupCache`, they are per-key dictionaries, not `AsyncLazyCache<T>`'s single-value shape - the
     fix (mirroring `AppliedArtworkStore.GetAsync`/`UpdateAsync`'s idiom of explicitly re-taking a shared gate
     around every dictionary read/write, not just the lazy load) is a bigger, riskier change touching every read/
     write call site in the file. Re-run Simplify Pressure Test fresh before building - do not assume it passes
     just because this loop's narrower fix did.
   - Score impact: `data_flow` residual narrows further toward the 7-anchor if it lands cleanly.
3. **Bound concurrency in `LoadGameEntriesAsync`'s per-game SteamGridDB lookups (F-003)** - blocked for the
   duration of this run by the standing operational constraint.
   - Why it matters: removes load latency scaling linearly with library size, whenever the constraint is
     lifted.
   - Score impact: Concurrency +0.5, Framework idioms +0.5 once verified (future run).

## Deepening Candidates

- **Candidate Module:** `StoreNameLookup`'s three remaining per-key caches (`gogNameCache`/`epicNameCache`/
  `nameMatchCache`).
- **Source friction proven:** Finding F-009 - this loop found that the file's fourth cache duplicated an
  existing, already-tested abstraction and fixed it by pure reuse; the same file's other three caches share the
  same "unlocked shared static state" smell but a different shape (per-key, not single-value), so they need a
  different remedy, not the same one.
- **Why the current Interface is shallow or misplaced:** n/a in the Deletion-test sense - these are not
  pass-through wrappers. The friction is Ownership & State: shared static dictionaries with zero synchronization
  discipline, unlike `AppliedArtworkStore`'s idiom of gating every read and write, not just the initial load.
- **Behavior to move behind the deeper Interface:** n/a - not a Seam question. The candidate change is adding a
  gate around each `TryGetValue`/assignment pair, mirroring `AppliedArtworkStore.GetAsync`/`UpdateAsync`'s
  existing pattern in this same codebase.
- **Dependency category:** `in-process` (the locking itself is pure synchronization, no I/O change).
- **Test surface after the change:** No new test surface - these methods remain network-bound with no seam,
  matching the file's standing test boundary. The change is a safety property, not a testable behavior change.
- **Smallest first step:** Wrap `gogNameCache`'s `TryGetValue`/assignment in `GetOrFetchGogNameAsync` with the
  new `gate` first (one method, smallest blast radius), prove the pattern, then repeat for `epicNameCache` and
  `nameMatchCache`.
- **What not to do:** Do not introduce `ConcurrentDictionary` as a drop-in replacement - `ConcurrentDictionary`
  makes individual operations thread-safe but does not make the check-then-act `TryGetValue`-then-assign
  sequence atomic; the `gate`-around-every-access idiom `AppliedArtworkStore` already uses is the codebase's own
  proven answer to this exact problem, not a new one to invent.

## Builder Notes

1. **Pattern:** A scorecard residual can sit unchanged for many loops not because it is unfixable, but because
   every loop re-confirms the SAME evidence (the same three named items) without asking whether a DIFFERENT,
   previously-uncounted instance of the same smell exists nearby with an already-available fix.
   - How to recognize: a standing scorecard note names a fixed, specific list ("the three unlocked caches") that
     has not changed in wording across many loops - that specificity is a signal nobody has re-swept the same
     file for a fourth instance since the list was first written.
   - Smallest coding rule: when a residual has gone unchanged for 3+ loops, re-grep the same file(s) for the same
     smell class from scratch, ignoring the standing list's exact membership, before accepting the residual as
     stable.
   - Stack example: C# - `StoreNameLookup.cs`'s standing "three unlocked caches" note (loop 3 through loop 13)
     never counted `ubisoftGameLookupCache`, a fourth static cache in the same file with the identical smell,
     because the original list was written once (loop 3, before `AsyncLazyCache<T>` existed) and re-cited
     verbatim ever since.

2. **Pattern:** When a codebase already has a generic, tested type for a shape ("load a value once and
   remember it"), a hand-rolled duplicate of that exact shape elsewhere in the codebase is a Deletion-test pass
   waiting to be noticed - deleting the duplicate and reusing the existing type does not require inventing
   anything, proving fresh friction, or passing the Unified Seam Policy (no new Seam is created).
   - How to recognize: a private field that is `null` until first use, plus a method that checks `if (field !=
     null) return`, populates it, and returns - the exact shape of any "get-or-load-once" cache, especially when
     a sibling type in the same directory already solves the identical shape via a shared abstraction.
   - Smallest coding rule: before accepting a hand-rolled lazy-init as "just how this file does it," grep the
     directory/module for an existing generic type with the same shape; if one exists and is tested, reuse it -
     no SPT gate applies since nothing new is being introduced.
   - Stack example: C# - `StoreNameLookup.ubisoftGameLookupCache` (a nullable `Dictionary<string,string>` field
     plus a check-then-populate method) was line-for-line the shape `AsyncLazyCache<T>` already generalizes and
     `EpicLibrary`/`AppliedArtworkStore` already reuse; the fix was a field-type change and a loader-method
     rename, not a new abstraction.

3. **Pattern:** Adding a brand-new, self-contained lock where none existed before is a smaller risk than moving
   or restructuring an existing one, but it is still worth recording as risk-boundary evidence: the change alters
   the concurrency contract of the code it wraps, even when the current call pattern happens to make the old,
   unlocked version safe by accident.
   - How to recognize: introducing a `SemaphoreSlim`/lock/mutex that is acquired and released only within one
     self-contained method or type, with no other lock acquired inside that same critical section.
   - Smallest coding rule: confirm (by direct read, since this stack has no TSAN tooling) that the new lock's
     critical section acquires no other lock, so no lock-ordering/deadlock hazard is introduced - then the change
     is purely additive safety, not a behavior change, and can be recorded as `reasoning_only` risk-boundary
     evidence with that specific justification rather than skipped as "not risky enough to record."
   - Stack example: C# - the new `gate` `SemaphoreSlim` in `StoreNameLookup.cs` is acquired/released only inside
     `AsyncLazyCache<T>.GetOrLoadAsync`'s existing, unchanged, already-stress-tested critical section - grep-
     confirmed no other lock is taken inside that scope.

**Scorecard humility check** (Q9): three specific claims I am least confident about -
1. Crediting `data_flow` with a full 0.5 UP for a 21-insertion/23-deletion single-file diff that fixes one of
   four caches - a stricter reviewer could argue this is the same magnitude as loop 11-13's small extractions,
   which this run consistently held flat and credited to a single dimension without moving the scorecard number
   itself. I judged the distinguishing factor to be that this is the *first* loop in the run's eleven-loop
   history to make *any* source-level change against `data_flow`'s own specifically-named residual (as opposed
   to a test-coverage addition credited there by convention), following the precedent set by loop 6's original
   `AsyncLazyCache<T>` introduction (which did move `concurrency` and `simplicity` UP for a comparable-shape
   fix) - but a reviewer who reads this run's more recent, tighter anti-double-counting convention (loops 11-13)
   as having superseded loop 6's precedent could reasonably keep this SAME instead.
2. Classifying Finding #1's architectural test as "Deletion test" rather than "n/a" - the old
   `LoadUbisoftGameListAsync` was never a "Module" in the sense the Deletion test usually evaluates (it was not
   wrapping or forwarding to another Module); I judged that "its complexity vanishes when deleted and replaced by
   an already-existing type" is a legitimate Deletion-test application, but a stricter reader could argue the
   Deletion test is reserved for pass-through wrappers specifically, not any code later found to duplicate
   something else.
3. Declining to build backlog item 2 (locking the three remaining per-key caches) this loop, on the grounds that
   it needs its own fresh Simplify Pressure Test pass rather than inheriting this loop's - a less conservative
   reviewer might argue the pattern is proven enough (the `gate`-around-every-access idiom already exists in
   `AppliedArtworkStore`) that building all four caches' fixes in one loop would have been the honest "smallest
   complete fix" rather than an artificially split one.

## Final Judge Narrative

Place, not win. Ground truth was clean going in (both gates green, zero source drift since loop 13's commit)
and clean coming out (131/131 tests, MSBuild exit 0). This loop's dispatch named a real pattern - eight of the
last nine loops moved only `test_strategy`/`simplicity` - and asked for a genuine investigation of the
structural dimensions before defaulting to the queued test-coverage item. Both `architecture_quality` and
`data_flow` got that investigation. `architecture_quality` held, for a documented reason: the two candidate
blocks inside `LoadGameEntriesAsync` are both I/O-only with no decision logic ahead of the fetch, the same shape
that has correctly protected `ArtworkDownloader.DownloadArtworkAsync` from extraction all run - re-confirmed
fresh this loop, not inherited. `data_flow` did not hold: a fourth static cache in `StoreNameLookup.cs`, never
counted in the standing "three unlocked caches" text, hand-rolled an unlocked duplicate of the codebase's own
`AsyncLazyCache<T>`. Fixed by pure reuse - no new type, no new Seam, net -2 lines - closing the first
source-level gap this run's `data_flow` scorecard has closed in eleven loops. The score moves 6.0 to 6.5 on
that structural proof; it does not reach the 7-anchor, since three more caches and one ambient environment-
variable fallback remain untouched, honestly queued as backlog item 2 rather than folded into this loop's
smaller, more defensible fix. Runtime ownership and concurrency are unaffected and exactly as trustworthy as
loop 13 left them - this loop's diff touches one production file, adds one new self-contained lock whose
critical section contains no other lock (recorded as risk-boundary evidence), and `git diff --stat` confirms
every other production file byte-identical since loop 13's commit. Tests neither improved nor regressed this
loop - the fixed method remains outside the test suite's network boundary by design, unchanged before and
after. Future work risks nothing new from overengineering - this loop's fix added zero new abstraction and
explicitly declined to extend the same fix to three riskier, differently-shaped caches without a fresh SPT
pass. Backlog is not empty (loop 13's queued test-coverage item returns to Priority 1; the new cache-locking
slice and F-003 follow), so `CONTINUE`.

## Loop 14 Result

Replaced `StoreNameLookup`'s hand-rolled, unlocked `ubisoftGameLookupCache` field and `LoadUbisoftGameListAsync`
method (`SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`, 21 insertions/23 deletions, net -2 lines) with a
call into the existing, already-tested `AsyncLazyCache<Dictionary<string,string>>` (`Services/AsyncLazyCache.cs`,
unchanged), matching `EpicLibrary.cs`'s and `AppliedArtworkStore.cs`'s existing idiom exactly - closing finding
F-009 (new this loop, resolved this loop). No other production file changed.

**What proves the change is honest:** `run-tests.ps1`: 131 passed before, 131 passed after (no test file
touched or added - `GetUbisoftGameNameAsync` remains outside the test suite's network boundary, consistent with
`TESTING.md`). MSBuild: exit 0, both runs (x64 Debug, `AppxBundle=Never`). `git diff --stat -- SteamGridDB.Xbox/
Services/Stores/StoreNameLookup.cs` confirms the only file touched (21 insertions, 23 deletions). Direct read of
the post-diff file confirms: `ubisoftGameLookupCache` and the old `LoadUbisoftGameListAsync` are both gone
(`grep -c` zero hits for both symbols in the file); `GetUbisoftGameNameAsync`'s only external caller
(`PrimaryWidget.xaml.cs:602`) is unchanged and grep-confirmed to reference no removed symbol, so no orphaned
caller remains.

**Risk boundary evidence (Meta-Rule 4):** this fix crosses a `lock_ordering` boundary (a new lock introduced
where none existed before). `{"boundary_kind": "lock_ordering", "verification": "reasoning_only", "detail":
"GetUbisoftGameNameAsync previously read/wrote ubisoftGameLookupCache with zero synchronization, relying
entirely on its single caller (PrimaryWidget.LoadGameEntriesAsync's sequential foreach) never invoking it
concurrently. This loop introduces one new, self-contained SemaphoreSlim (gate, StoreNameLookup.cs:40) used only
within AsyncLazyCache<T>.GetOrLoadAsync's acquire/check/populate/release sequence for this one cache - no other
lock is acquired within that scope (grep-confirmed: gate is referenced only at StoreNameLookup.cs:40-42 and
inside AsyncLazyCache.cs's own GetOrLoadAsync body, Services/AsyncLazyCache.cs:44,57), so no lock-ordering
hazard (deadlock via inconsistent acquisition order) is introduced - the change only adds safety where none
existed, it does not remove or reorder any existing synchronization. AsyncLazyCache<T> itself is unchanged and
already stress-tested under 32 concurrent callers (AsyncLazyCacheTests.cs, pre-existing, unaffected by this
loop's diff). No thread-sanitizer or concurrency stress-test tooling exists for this C#/UWP stack in this
environment; verified instead by direct inspection of the one new lock's scope.", "mechanically_testable":
false}`

**Targeted finding status:** `resolved` - F-009's Claim (a fourth static cache hand-rolling an unlocked
duplicate of `AsyncLazyCache<T>`) is fully gone from current source; the old field and method no longer exist,
replaced entirely (not left as a parallel shallow copy) by a call into the existing shared type.

**Unintended scorecard regression:** none. Eight of nine dimensions held SAME with fresh structural
re-derivation this loop; `data_flow` moved UP on structural proof (see Scorecard). No dimension regressed.

## Loop 14 Implementation Review

`verdict: approved` - "The diff genuinely replaces StoreNameLookup's hand-rolled, unlocked
ubisoftGameLookupCache/LoadUbisoftGameListAsync with the existing, already-tested AsyncLazyCache<T>, matching
EpicLibrary.cs:41-44 and AppliedArtworkStore.cs:30-35's identical idiom exactly, with no orphaned callers,
preserved null-means-retry semantics, and no second lock inside the new gate's critical section." All three
checks (`reality`, `honesty`, `regression`) `passed`; `conditions: []`; `regressions: []`.
