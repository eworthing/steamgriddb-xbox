### Discovery (see Loop 7 Discovery refresh)

No drift since loop 7's commit (`7fa0548`): `git status --porcelain` was clean at this loop's Step 1, and
`git log 7fa0548..HEAD` was empty before this loop's own edits. Both ground-truth gates re-run fresh this
loop, independent of loop 7's cached numbers:

- `powershell -NoProfile -File ./run-tests.ps1` — **104 passed, 0 failed** before this loop's fix, **105
  passed, 0 failed** after (one new regression test added; see Loop 8 Result).
- `"C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
  SteamGridDB.Xbox.sln /p:Configuration=Debug /p:Platform=x64 /p:AppxBundle=Never /v:minimal /nologo` — exit
  0 before and after.
- `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`: 1,957 lines at Step 1 (matches loop 7's post-fix figure exactly
  - `wc -l` re-verified fresh this loop, not carried), 1,978 lines after this loop's fix (+21).

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

- **Structurally necessary:** Routing `LoadGameEntriesAsync`'s five raw JSON field reads through the
  existing `JsonRead` module. Passes the Unified Seam Policy trivially - no new Seam, `JsonRead` already
  exists and is already the established access pattern everywhere else in the codebase. Passes Simplify
  Pressure Test question 1 (fixes real ambiguity) cleanly: the raw accessors' behavior on a present-null
  member is demonstrably unsafe (empirically confirmed this loop), `JsonRead`'s is safe by construction.
- **New seam justified:** false - no new Seam introduced; `JsonRead` already existed.
- **Helpful simplification:** The `id` field's `ContainsKey`-then-`GetNamedString` two-step collapsed into
  one `JsonRead.String` call plus a single `string.IsNullOrEmpty` check, removing a redundant lookup.
- **Should NOT be done:** Attempting loop 7's queued two-phase `LoadGameEntriesAsync` split this loop - a
  full read of the method (this loop's own investigation) shows image decode and network calls are
  interleaved per entry, not separable into "pure parse phase" then "UI-decode tail." Forcing that split
  now would fail Simplify Pressure Test's fake-clean anti-example ("a clean-looking fix that adds ceremony
  without fixing ownership") - the corrected, narrower next slice is named in Finding #2's minimal
  correction path instead. Also not attempted this loop: F-005's `RankGrids` test gap and F-003's
  concurrency fix - both Noticeable, both lower priority than this loop's Serious-severity Finding #1.
- **Tests after fix:** No prior tests existed for the removed raw-accessor pattern (it lived inside
  `PrimaryWidget.xaml.cs`, architecturally untestable). One new test added at the layer that IS testable -
  `JsonReadTests.cs` - proving the exact failure mode this fix closes, rather than only describing it in a
  docstring. This is not a deepening of a previously-shallow Module (`JsonRead` itself is unchanged; only
  its call sites moved), so Replace-don't-layer's stale-test-deletion requirement does not apply.

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

1. **Pattern:** A backlog item's own remedy can be wrong even when the underlying finding is real - the
   prior loop's own uncertainty disclosure ("I named the split direction from reading the method once, not
   from actually attempting it") was the exact signal that the queued Priority 1 needed re-verification
   before being attempted, not assumed correct because it was already queued.
   - How to recognize: a carried-forward backlog item whose `minimal_correction_path` was written from a
     single read-through, especially one the prior loop's own Builder Notes or humility check flagged as
     uncertain.
   - Smallest coding rule: before attempting a carried-forward remedy, re-read the target method in full
     and name the exact line(s) where the remedy's assumed split boundary would fall - if no clean boundary
     exists, downgrade per Simplify Pressure Test rather than force the originally-planned shape.
   - Stack example: C# - `LoadGameEntriesAsync`'s image decode (`CreateThumbnailAsync`) sits between the
     per-entry backup check and the `GameEntry` construction, inside the same entry iteration as the
     "pure" field parsing loop 7 assumed was separable from it.

2. **Pattern:** A module built to fix one real, documented incident (`JsonRead`, built after a null-Steam-ID
   crash) can still be bypassed elsewhere in the same codebase by code that predates it or was never
   updated to use it - the module's existence does not retroactively protect call sites that do not call
   it.
   - How to recognize: grep every raw use of the underlying unsafe API (here, `Windows.Data.Json`'s
     `GetNamedString`/`GetNamedObject`/etc. called directly rather than through the wrapper) across the
     whole codebase, not just the files the wrapper was originally written for.
   - Smallest coding rule: when a defensive-access module exists specifically to prevent a documented
     failure class, treat any direct call to the raw API it wraps as a candidate defect, not just a style
     inconsistency - verify the raw call's actual failure behavior before dismissing it.
   - Stack example: C# - `SteamGridDbClient.cs`, `EpicLibrary.cs`, and `StoreNameLookup.cs` all already used
     `JsonRead`; `PrimaryWidget.xaml.cs` - the largest, most-churned file - was the one holdout.

3. **Pattern:** `ContainsKey` and "is this member usable" are not the same question for a JSON API that
   distinguishes "absent" from "present and null" - a guard written to catch "absent" silently does nothing
   for "present and null," which is often the more common malformed-data case in the wild.
   - How to recognize: a null/missing-value guard that calls `ContainsKey`/`HasKey`/`hasOwnProperty`
     immediately before reading a value with no further null check.
   - Smallest coding rule: for any JSON/dictionary API where a key can map to an explicit null, guard on
     the read result's nullness, not on key presence.
   - Stack example: C# - `Windows.Data.Json`'s `JsonObject.ContainsKey` returns `true` for a key whose value
     is `JsonValueType.Null`; only `JsonRead`'s `ValueType` check (not a `ContainsKey` call) actually
     distinguishes "present and null" from "present and usable."

**Scorecard humility check** (Q9): three specific claims I am least confident about —
1. `overall_implementation_credibility`'s +0.5 magnitude (7.0 → 7.5) for closing one honesty leak at five
   call sites in one method - a stricter reviewer could argue this is too small a fix (net +21 lines, no
   caller-visible change) to move a whole-codebase credibility dimension at all, and that it should have
   stayed SAME with the leak instead named as a residual note under domain_modeling or data_flow.
2. Downgrading Finding #2's remedy from loop 7's two-phase split to a narrower field-extraction-only slice
   - I am confident the two-phase framing does not hold (verified by reading the interleaving directly),
   but I have not attempted the narrower slice myself, so I cannot yet promise it survives its own Simplify
   Pressure Test any better; a future loop might find the field-extraction sub-step is also more entangled
   than I am describing here (e.g., if `epicCatalogItemId`'s derivation turns out to need something from
   the network-call phase I have not traced).
3. Whether Finding #1 (this loop's fix) is correctly scored as `credibility` rather than `domain_modeling` -
   the rubric has no smell explicitly named for "an existing defensive-access module bypassed at one call
   site"; a reviewer applying the domain_modeling 9-anchor's "invariants enforced at construction, not by
   convention" language could reasonably credit that dimension instead, which would change which dimension
   moved this loop without changing the underlying fix.

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
