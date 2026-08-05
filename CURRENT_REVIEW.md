### Loop Counter
Loop 5 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

Test strategy breaks its own 13-loop stall this loop: crediting loop 4's own fix (commit `c6fcf6e`, independently re-verified this loop via direct reads of `LibraryOperationGuard.cs` and `LibraryOperationGuardTests.cs`), `test_strategy` moves from 6.5 to 8.0 — the single most-cited gap in this project's history is closed, though the remaining untested UI-orchestration surface (documented, platform-forced) keeps it below 9. This loop's own pick, F-017, closes the second occurrence of the concurrency defect class F-015 fixed last loop: `SteamGridDbClient.CapsuleParseNotes` and `FixLog`'s static fields now gate their writes, matching `StoreNameLookup`'s established pattern — and, as a natural side effect of touching the same lines, also resolves `state_management`'s standing residual (`CapsuleParseNotes`'s public-mutable-reference exposure). What keeps this short of contest-grade: `architecture_quality` and `data_flow` remain flat (`PrimaryWidget.xaml.cs` still mixes several concerns; ambient-state count unchanged at 7 instances, now all synchronized), and F-011 remains genuinely blocked by the standing user constraint.

**Prior-audit adopt-or-falsify**: `CODE-REVIEW.md`, `TESTING.md` and `ARTWORK-SELECTION.md` were adopted/falsified against current source in loop 1 with no open claim dropped; this loop re-confirmed no new prior-audit claim changed status — `TESTING.md`'s documented Compute/Do split and network-boundary test carve-out were re-read in full and directly informed both this loop's scoring of `test_strategy` and the shape of F-017's fix and tests.

## Scorecard (1-10)

- Architecture quality: **7.5** | SAME | Direct full re-read this loop (via a dedicated helper sweep) of `PrimaryWidget.xaml.cs` (2069 lines, post loop-4 extraction) confirms the file still mixes the same real concerns: UI event handling, network orchestration, file I/O/backup-restore, artwork-selection invocation, and panel/search navigation. **Stalled-Dimension Sweep (SAME for loops 1-5 of this run, 11 of the last 12 loops overall):** explicit clean — no single extraction candidate beyond F-016 (already landed) passes SPT; a further split needs a multi-file redesign disproportionate to any one loop's blast radius. Noted as a standing shape, not actionable this loop.
- State management and runtime ownership: **9.5** | SAME | `residual_disposition: accepted` | Direct re-read this loop of the guard call sites (`TryBeginLibraryOperation`/`EndLibraryOperation`/`IsLibraryOperationBlocking`, `PrimaryWidget.xaml.cs`:213-234, plus all 7 call sites: `PrimaryWidget_Loaded`:151, `RefreshButton_Click`:764, `ConfirmAndRunAsync`:826, `GridImage_Click`:1533, `RestoreBackup_Click`:1918) — independently re-confirmed by a helper sweep — re-confirms F-014/F-016's fix holds with no unguarded write path. Residual as of Step 1 (pre-fix source): `SteamGridDbClient.CapsuleParseNotes` was still `public static readonly List<string>` — the `readonly` only pinned the reference. Adversarial Pass re-run: the residual's own smallest fix (private field + `IReadOnlyList<string>` accessor) still fails SPT Q5 comparatively against F-017 (this loop's actual, higher-severity pick) *as a standalone item* — but this loop's own F-017 fix bundles exactly that change as a side effect of touching the same lines for the concurrency gate (zero extra blast radius, same file). Per this project's established credit-lands-next-loop convention, the resolved residual is scored SAME this loop (Step 1 evaluates pre-fix source) and will show as resolved at loop 6's Step 1.
- Domain modeling: **9.5** | SAME | `residual_disposition: accepted` | Direct full re-read this loop of `GameEntry.cs` (196 lines) re-confirms the parallel-fields case (`HasSteamGridDBMatch`/`OfficialCapsuleUrl`/`SteamGridDbGameId`, lines 113-145) unchanged. Adversarial Pass re-run: readonly-struct-with-factories still fails SPT Q2 — `GameEntry` implements `INotifyPropertyChanged` and is two-way XAML-data-bound; residual holds.
- Data flow and dependency design: **7.5** | SAME | Ambient-state census re-confirmed this loop by two independent direct sweeps (mine and a helper's): `StoreNameLookup`'s three caches + `SteamGridDbClient.CapsuleParseNotes` + `FixLog`'s three fields = 7 process-lifetime instances, unchanged in count. **Stalled-Dimension Sweep (SAME for loops 1-5 of this run, 11 of the last 12 loops overall):** explicit clean — this loop's own fix (F-017) adds synchronization to two of the seven instances but does not reduce the ambient-dependency count, which is what this dimension measures (the same conclusion loop 3 reached for F-015's fix). No consolidation candidate passes SPT Q2 — single-instance widget, no multi-instance/test-injection need proven.
- Framework / platform best practices: **9.5** | SAME | `residual_disposition: accepted` | `App.xaml.cs`:120's `//TODO: Load state from previously suspended application` re-read in full this loop (independently, via helper), unchanged — matches the widget's own OnActivated path being the real launch surface, so the OnLaunched fallback's TODO is inert rather than a live gap. Adversarial Pass re-run: deleting the dead comment still passes SPT Q1-Q4 as a free, zero-risk subtractive change but fails Q5 comparatively against F-017; residual holds.
- Concurrency and runtime safety: **7.0** | SAME | Scored pre-fix (Step 1 convention). Direct re-read this loop of `SteamGridDbClient.cs` and `FixLog.cs` (both in full) re-confirms F-017's TOCTOU gap unchanged as of Step 1: `NoteCapsuleParse` (`SteamGridDbClient.cs`:49-55, pre-fix) still checks-then-adds with no lock; `FixLog`'s `Start`/`Write`/`SaveAsync` (`FixLog.cs`:46-77, pre-fix) still mutate/read `lines`/`fileName` with no synchronization. This loop's own Step 2/3 fixes both — credit lands at loop 6's Step 1, matching the established pattern. F-011 remains blocked, re-derived fresh: its own remedy (bounded concurrency) necessarily changes network-call ordering, which the standing user constraint forbids without a product decision.
- Code simplicity and clarity: **9.5** | SAME | `residual_disposition: accepted` | Mandatory leaf-module duplication sweep this loop (three parts): (a) leaf modules read directly by a helper — `GameEntry.cs`, `GamePlatform.cs`, `GridImageItem.cs`, `OperationReport.cs`, `AppliedArtworkStore.cs`, `ArtworkDownloader.cs`, `ArtworkRanker.cs`, `ArtworkSignature.cs`, `TileImage.cs`, `EpicLibrary.cs`, `App.xaml.cs`, `MainPage.xaml.cs`, plus my own direct reads of `LibraryOperationGuard.cs`, `AsyncLazyCache.cs`, `StoreNameLookup.cs`; (b) four-angle results all clean except the already-tracked `GamePlatform.cs` dual-switch (F-012) and the `gridPanelSessionId`/`searchPanelSessionId` session-token pattern, independently re-confirmed at exactly 2 instances — still below this project's own established 3-instance extraction threshold, correctly not a finding, watched; (c) no `audit_clones.py`/`audit-enum-interpretation.sh` available in this repo checkout — manual four-angle pass substituted, noted as scope limit (unchanged from prior loops). Residual: `GamePlatform.cs`'s two independent switches (F-012, Cosmetic). Adversarial Pass re-run: F-012's own smallest fix still passes SPT Q1-Q4 but fails Q5 comparatively against F-017 (this loop's actual, higher-severity pick); residual holds — but with F-016 and F-017 both now resolved and F-011 blocked, F-012 is promoted to Priority 1 for loop 6 (nothing higher-severity remains actionable).
- Test strategy and regression resistance: **8.0** | UP | Structural proof: commit `c6fcf6e` (loop 4) added `SteamGridDB.Xbox/Services/Library/LibraryOperationGuard.cs` (a plain, dependency-free class) and `SteamGridDB.Xbox.Tests/LibraryOperationGuardTests.cs` (5 direct tests, independently re-read in full this loop: `Starts_not_running`, `TryBegin_succeeds_and_marks_running_when_nothing_else_is_running`, `TryBegin_fails_and_leaves_the_guard_running_when_already_running` — the exact mutation named on every loop of this project's 13-loop history — `End_releases_the_guard_so_a_new_operation_can_begin`, `End_is_safe_to_call_when_nothing_is_running`), independently re-verified this loop as wired correctly into `PrimaryWidget.xaml.cs` at all 7 call sites (see State management proof). This is source loop 4's own Step-1 evaluation did not have (the test file did not exist until loop 4's own Step 2/3), so the UP is honest re-derivation, not anchoring. Held below 9 (not 8.5+): a helper sweep this loop named a second, still-uncovered gap on the same file — `GridImage_Click`'s stale-session guard (`gridItem.SessionId == gridPanelSessionId`, `PrimaryWidget.xaml.cs`:1531) is a primary-flow persistence-writer check with no test, reachable only through WinUI and therefore excluded by the same platform-binding carve-out `TESTING.md` documents for the rest of `PrimaryWidget`'s UI-orchestration surface — genuinely off the file's remaining scope, but real enough that claiming the 9-anchor's "every contest-relevant feature flow" bar this loop would be premature. Left at 8.0 rather than escalated further; the Authority Map cross-check required at `test_strategy >= 9` (G24) is deferred to a loop that can do it properly rather than claimed on a partial pass.
- Overall implementation credibility: **9.5** | SAME | `residual_disposition: queued` (F-017) | Scored pre-fix (Step 1 convention): F-017's gap is still present in the source this Step 1 evaluates (the codebase's own established pattern — every other cross-file mutable cache gates its writes — is silently broken by two instances), so the residual stays queued rather than accepted. It is this loop's own Priority-1 pick; the queued-to-resolved transition (like F-013's, F-014's, F-015's and F-016's before it) shows up at loop 6's Step 1, scored against this loop's own commit.

## Authority Map
(Re-emitted this loop: F-017, this loop's Priority-1 pick, is an ownership/authority concern — who may write the two ambient state surfaces it fixes.)

**Concern: `SteamGridDbClient.CapsuleParseNotes` (capsule-parse failure notes)**
- Owner: `SteamGridDbClient` (static)
- Allowed writers: `NoteCapsuleParse` (now `internal`, gated by `capsuleParseNotesGate`)
- Observers / readers: `PrimaryWidget.FixLibraryAsync` (`PrimaryWidget.xaml.cs`:969, reads `CapsuleParseNotes` to log parse failures into the fix run)
- Persistence seam: none (in-memory, process-lifetime)
- Async mutation entry points: `ParseOfficialCapsuleUrl` (synchronous; called from `GetGameByPlatformIdAsync`, itself called once per unmatched entry from `LoadGameEntriesAsync`'s per-entry loop)
- Verdict: **Single and clear** (now gated; previously unsynchronized check-then-populate, safe only by the single-threaded-per-load convention)

**Concern: `FixLog`'s run state (`lines`/`fileName`/`logFolder`)**
- Owner: `FixLog` (static)
- Allowed writers: `Start()`/`Write()` (now gated by `syncRoot`)
- Observers / readers: `SaveAsync()` (now takes a gated point-in-time snapshot before its file I/O)
- Persistence seam: `SaveAsync()` writes to a `StorageFile` (`last-fix.log` / `last-load.log`, caller-selected)
- Async mutation entry points: `LoadGameEntriesAsync`, `FixLibraryAsync`, `RestoreAllChangesAsync` (each calls `Start`/`Write`/`SaveAsync` in sequence; all three run under `PrimaryWidget`'s own library-operation guard today, so never concurrently with each other in production)
- Verdict: **Single and clear** (now gated; previously unsynchronized)

## Strengths That Matter
- `ArtworkRanker`/`ArtworkDownloader`/`ArtworkSignature`/`TileImage` remain a genuinely deep, pure, well-tested pipeline — independently re-confirmed this loop by a cold helper sweep finding no structural issues and no domain-policy leakage.
- `StoreNameLookup`'s three per-store gates (`gogNameGate`/`epicNameGate`/`nameMatchGate`, landed loop 3) are re-verified this loop as correct double-checked locking, matching `AsyncLazyCache<T>`'s already-proven shape — the exact pattern this loop's own fix (F-017) extends to the codebase's two remaining unsynchronized static caches.
- This loop's own fix required zero new architecture and zero new files in the app project beyond two small, targeted edits to already-existing classes (`SteamGridDbClient`, `FixLog`) — the smallest honest fix for a defect class this codebase has now closed in every location it was found.

## Findings

### Finding #1: SteamGridDbClient.NoteCapsuleParse and FixLog perform unsynchronized check-then-populate/append writes on static mutable collections, the same defect class F-015 fixed in StoreNameLookup

**Why it matters** — Both are safe today only by the same single-threaded-per-load convention that made `StoreNameLookup`'s three caches safe before F-015's fix. Neither guarantee is enforced by the fields themselves — a future concurrent caller (including a future unblocked F-011) would silently inherit an unsynchronized write, exactly the risk F-015 eliminated for `StoreNameLookup`.

**What is wrong** — `SteamGridDbClient.NoteCapsuleParse` (`SteamGridDbClient.cs`:49-55, pre-fix) checked `CapsuleParseNotes.Count < 5` then called `CapsuleParseNotes.Add(note)` with no lock — a TOCTOU race on a `List<string>`, which is not thread-safe for concurrent `Add` calls even individually. `FixLog`'s three static fields (`lines`/`fileName`/`logFolder`, `FixLog.cs`:24,26,28, pre-fix) were mutated by `Start()`/`Write()` and read by `SaveAsync()` with no synchronization at all.

**Evidence** — `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs`:47 (pre-fix), `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs`:49-55 (pre-fix), `SteamGridDB.Xbox/Services/Artwork/FixLog.cs`:24,26,28 (pre-fix), `SteamGridDB.Xbox/Services/Artwork/FixLog.cs`:46-77 (pre-fix), `SteamGridDB.Xbox/Services/AsyncLazyCache.cs` (the established gate pattern), `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` (F-015's own remedy, the precedent this fix follows).

**Architectural test failed** — n/a (concurrency-safety defect, not a Seam/Module-boundary question).

**Dependency category** — `in-process`.

**Leverage impact** — Before the fix, a caller got no concurrent-access-safety guarantee from either type's Interface alone; safety depended entirely on today's callers happening to be sequential.

**Locality impact** — `SteamGridDbClient.cs`'s fix is fully contained to that file; `FixLog.cs`'s fix is fully contained to that file — two independent, small remedies, not one shared abstraction.

**Metric signal** — none.

**Why this weakens submission** — Not currently reachable as a live race (`LoadGameEntriesAsync`'s loop awaits one entry at a time; `FixLog`'s callers are mutually exclusive under the library-operation guard), but the identical structural inconsistency F-015 closed in `StoreNameLookup`, found in two more locations.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — `SteamGridDbClient`: guard `NoteCapsuleParse`'s check-then-populate body with a plain `lock` (the method and its only caller chain are fully synchronous — no `await` inside the critical section, so a `SemaphoreSlim` would be ceremony `StoreNameLookup`'s own async callers need and this one doesn't). `FixLog`: guard `Start`/`Write` with a plain `lock`; have `SaveAsync` take a point-in-time snapshot under the same lock before its file I/O (a `lock` block cannot itself wrap an `await`).

**Blast radius** — Change: `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs`, `SteamGridDB.Xbox/Services/Artwork/FixLog.cs`, `SteamGridDB.Xbox.Tests/SteamGridDbClientTests.cs` (new), `SteamGridDB.Xbox.Tests/FixLogTests.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`.

---

### Finding #2: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs`:494-705, current line numbers; was 455-717 pre-loop-4-extraction) awaits each entry's SteamGridDB platform-ID lookup, store name-fetch and SteamGridDB name search in strict sequence, even though the awaits are independent across entries.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`:494-705.

**Architectural test failed** — n/a.

**Dependency category** — null.

**Leverage impact** — None currently actionable — see blocker.

**Locality impact** — Would be contained to `PrimaryWidget.xaml.cs` if unblocked.

**Metric signal** — none.

**Why this weakens submission** — A real, linearly-scaling latency cost, but BLOCKED: the recorded standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft) means this loop cannot land a change altering observable call ordering/concurrency/count against those services without a product decision this loop cannot make. Re-derived fresh this loop: a pure extraction would not be blocked, but F-011's own remedy (bounded concurrency) necessarily changes network-call ordering, so it stays genuinely blocked.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — BLOCKED this loop; named for continuity (Backlog Prioritization Pass criterion 0) rather than escalated to a user-decision halt, since F-017 filled this loop's actionable Priority-1 slot.

**Blast radius** — Change: none. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (no change while blocked).

---

### Finding #3: GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding or renaming a platform requires remembering to update both switches; nothing fails to compile if one is forgotten (both silently default to Unknown/null).

**What is wrong** — `FromXboxDirectory` (`GamePlatform.cs`:22-46) and `GamePlatformToSGDBApiString` (`GamePlatform.cs`:48-67) independently switch over the same platform cases with no shared table; `FromXboxDirectory` additionally handles legacy folder-name aliases with no analogue in the reverse mapping. Re-confirmed unchanged this loop via direct full read and an independent helper sweep.

**Evidence** — `SteamGridDB.Xbox/Models/GamePlatform.cs`:22-46, `SteamGridDB.Xbox/Models/GamePlatform.cs`:48-67.

**Architectural test failed** — n/a.

**Dependency category** — null.

**Leverage impact** — Each new platform pays this asserted-twice tax; small today, compounds at scale.

**Locality impact** — Contained to `GamePlatform.cs`.

**Metric signal** — none.

**Why this weakens submission** — Minor, but the enum's own home module holds two independent interpretations of itself with no compiler-enforced link. This loop's Adversarial Pass re-tested the smallest fix and confirmed it still fails Simplify Pressure Test Q5 against F-017 — but with the backlog otherwise clear, this becomes loop 6's Priority 1.

**Severity** — Cosmetic for contest.

**ADR conflicts** — none.

**Minimal correction path** — Fold both mappings into one table keyed by `GamePlatform`, with a separate small alias list for `FromXboxDirectory`'s legacy folder names.

**Blast radius** — Change: `SteamGridDB.Xbox/Models/GamePlatform.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (call sites unchanged).

## Simplification Check
- Structurally necessary: Gating `SteamGridDbClient.NoteCapsuleParse`'s check-then-populate body and `FixLog`'s `Start`/`Write`/`SaveAsync` — removes a real TOCTOU hazard that current source could not otherwise prove absent; matches the same remedy class F-015 already established for `StoreNameLookup`.
- New seam justified: false (no new Seam introduced — plain `lock` statements inside existing classes).
- Helpful simplification: As a side effect of touching the same lines, `CapsuleParseNotes` moves from a publicly-mutable `List<string>` reference to a `private` backing field exposed as `IReadOnlyList<string>` — resolving `state_management`'s standing accepted residual (credit lands at loop 6's Step 1, per this project's established convention).
- Should NOT be done: Fixing `FixLog`'s concurrency with a `SemaphoreSlim` to match `StoreNameLookup`'s pattern exactly — `Start`/`Write` are synchronous and never hold the lock across an `await`, so a plain `lock` is the smaller, more idiomatic primitive; reaching for `SemaphoreSlim` everywhere regardless of whether the critical section spans an `await` would be ceremony, not consistency. Extracting `GridImage_Click`'s stale-session equality check (`gridItem.SessionId == gridPanelSessionId`) into its own tested unit — a real primary-flow gap named this loop, but wrapping a single comparison operator in a class for testability is ceremony disproportionate to the risk (fails SPT Q2); left as a named, accepted scope limit on `test_strategy` rather than actioned.
- Tests after fix: No existing tests were deleted (none existed for this exact surface). Two tests added directly at the new Interface: `SteamGridDbClientTests.Concurrent_notes_are_capped_at_five_with_no_corruption` (asserts the exact mutation named above — 50 concurrent writers, cap holds at exactly 5) and `FixLogTests.Concurrent_writes_do_not_lose_or_corrupt_lines` (50 concurrent writers, all 50 lines plus the header present, none lost or corrupted). Verification: full build (msbuild, exit 0) and full test suite (run-tests.ps1, 145 passed / 0 failed / 0 skipped — 143 prior + 2 new) both re-run before and after the change; `git diff` review confirms `PrimaryWidget.xaml.cs`'s only touch point (`SteamGridDbClient.CapsuleParseNotes`'s `foreach` read at line 969) compiles unchanged against the new `IReadOnlyList<string>` return type, and no network call, ordering, or count changed anywhere.

## Improvement Backlog
1. **[F-012]** Fold `GamePlatformHelper`'s two independent switch statements into one shared table (Finding F-012) — simplification, minor. With F-016 and F-017 both resolved and F-011 blocked, nothing higher-severity remains actionable; F-012 has been deferred many loops running behind a rotating cast of higher-priority picks (Backlog Prioritization Pass criterion 2 — item deferral) and is promoted to Priority 1 for loop 6. Score impact: `simplicity +0.5`.
2. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution (Finding F-011) — structural, needed for winning. BLOCKED by the standing user constraint on per-game network-call ordering/concurrency; named for continuity, not actionable this loop. Score impact: `concurrency +1.0`.

**Priority-1 accounting**: F-017 wins Priority 1 on severity (Noticeable weakness, tied with F-011) and actionability (F-011 is blocked per criterion 0) — the only unblocked Noticeable-or-worse candidate this loop, and it closes the same defect class F-015 already established a remedy for, at zero new architectural cost. F-012 (Cosmetic) was re-tested by the Adversarial Pass and still loses on severity; it is named explicitly here, rather than silently deferred again, because with F-017 landing this loop it becomes the only remaining actionable item for loop 6.

## Deepening Candidates
None this loop. `LibraryOperationGuard` (loop 4) and this loop's `SteamGridDbClient`/`FixLog` gates are all plain, already-deep-enough classes; no caller or test currently reaches past their Interfaces. `GridImage_Click`'s stale-session check was investigated as a candidate and rejected (see Simplification Check's should-not-be-done) — friction is not proven for a single comparison operator.

## Builder Notes

**Pattern**: Credit for a fix lands at the START of the next loop's Step 1, not inside the loop that made the fix.
**How to recognize**: This loop's own scorecard shows the pattern for a fifth time: `test_strategy` moved UP crediting loop 4's own F-016 fix, while F-017 (this loop's own fix) will show up resolved at loop 6's Step 1, scored against this loop's own commit.
**Smallest coding rule**: Always score against the source as read at Step 1, before this loop's own Step 2/3 executes.
**Stack example**: C#: `test_strategy` moved to 8.0 this loop crediting `LibraryOperationGuardTests.cs` (loop 4's own fix); F-017's fix (this loop's own) will show up at loop 6's Step 1 against this loop's own commit SHA.

**Pattern**: When a Priority-1 fix already touches the exact lines a different dimension's accepted residual lives on, its smallest fix can ride along for free.
**How to recognize**: `state_management`'s residual (`CapsuleParseNotes`'s public-mutable-reference exposure) sat on the same lines F-017's concurrency gate needed to touch anyway — a private field plus a read-only accessor cost nothing extra once the file was already open for the real fix, with zero added blast radius.
**Smallest coding rule**: Before writing a fix, check whether any *other* dimension's named residual lives on the same lines. If its own smallest fix is genuinely free there, bundle it; if it needs a separate file or a separate justification, it isn't free — leave it queued.
**Stack example**: C#: `SteamGridDbClient.CapsuleParseNotes` changed from `public static readonly List<string>` to `private static readonly List<string>` plus a `public static IReadOnlyList<string>` accessor, in the same edit that added the lock — not a separate commit, not a separate SPT pass.

**Pattern**: A Cosmetic residual deferred behind a rotating cast of higher-severity findings for many loops running is not evidence the finding is wrong — it is evidence the supply of higher-severity findings hadn't run out yet.
**How to recognize**: F-012 (`GamePlatform.cs`'s dual switches) has lost the Adversarial Pass to F-014, F-015, F-016 and now F-017 in turn — never because F-012 itself changed, but because something more severe kept arriving first. With that supply now exhausted (F-011 blocked, nothing else found this loop), F-012's promotion to Priority 1 for loop 6 is the queue finally draining, not new evidence.
**Smallest coding rule**: When a residual has been SPT-rejected on Q5 against a *different* finding three or more loops running, check whether it lost to the same finding repeatedly (drop it — it's genuinely lower-value) or to a rotating cast (it's queued behind supply, not disqualified — let it surface once the supply runs out).
**Stack example**: C#: this loop's Backlog Prioritization Pass names F-012 explicitly as next loop's Priority 1 rather than silently deferring it an nth time, precisely because nothing else remains to outrank it.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) `test_strategy`'s landing at exactly 8.0 rather than 7.5 or 8.5 — a stricter reading could treat `GridImage_Click`'s untested stale-session guard as fully disqualifying the jump past 7.5 (it is, after all, a primary-flow persistence-writer check), while a more generous reading could treat `TESTING.md`'s documented platform-binding carve-out as covering it entirely and justify 8.5; 8.0 is this loop's judgment call, not a mechanically-derived number. (2) `state_management` staying `9.5 SAME` rather than acknowledging that its residual's fix already exists in the working tree at the moment this scorecard is written (Step 3 having already run) — the credit-lands-next-loop convention is well-precedented in this project, but a reader who does not accept that convention could reasonably call this scorecard stale by one loop on this specific dimension. (3) Carrying F-017's severity forward as "Noticeable weakness" unchanged from loop 4's characterization, rather than re-assessing it now that its actual remedy turned out to be two small `lock` statements with no live-reachable exploit path — a stricter read might have downgraded it to Cosmetic before spending a Priority-1 slot on it.

## Final Judge Narrative
Place, not win, yet — but a clean loop that closes what it opened. Last loop's own investigation found the concurrency defect class recurring in two more locations; this loop closes both, at zero new architectural cost, following the exact remedy pattern loop 3 already established. Separately, credit for loop 4's own test-coverage fix lands here: `test_strategy` breaks its 13-loop stall, moving from 6.5 to 8.0 on real, independently-verified structural proof — held below 9 by an honestly-named remaining gap (`GridImage_Click`'s stale-session check) rather than claimed prematurely. Runtime ownership remains trustworthy for every traced write path (F-014/F-016 hold, independently re-verified). Concurrency is more trustworthy in current source right now than last loop's own scorecard shows — this loop's fix lands the credit next loop, matching the established pattern. Simplification did not hurt this loop: zero new ceremony, zero new Seams, two plain `lock` statements sized to match their call sites (no `SemaphoreSlim` reached for where a synchronous critical section didn't need one). Two new tests directly cover the exact mutations this loop's finding named. Future work still risks over-engineering if it tries to extract `GridImage_Click`'s single-comparison stale-session check into its own tested unit before a second instance of that shape justifies it, or to unify `StoreNameLookup`'s, `SteamGridDbClient`'s and `FixLog`'s now-three differently-shaped gates into one abstraction they don't share enough behavior to earn.

## Loop 5 Result
Wrapped `SteamGridDbClient.NoteCapsuleParse`'s check-then-populate body and `FixLog`'s `Start`/`Write`/`SaveAsync` in dedicated locks (`SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs`, `SteamGridDB.Xbox/Services/Artwork/FixLog.cs`), matching the concurrency-safety pattern F-015 already established for `StoreNameLookup`'s caches; `CapsuleParseNotes` also moves from a publicly-mutable `List<string>` reference to a `private` backing field exposed as `IReadOnlyList<string>`, resolving `state_management`'s standing residual as a side effect of the same edit. Added `SteamGridDB.Xbox.Tests/SteamGridDbClientTests.cs` (1 test) and extended `SteamGridDB.Xbox.Tests/FixLogTests.cs` (1 test), both asserting the exact concurrent-write mutation the finding names. Full build (msbuild, exit 0) and full test suite (run-tests.ps1) both re-run before and after: before, 143 passed / 0 failed / 0 skipped; after, 145 passed / 0 failed / 0 skipped (143 prior + 2 new, all green, no prior test's behavior changed). `git diff` review confirms `PrimaryWidget.xaml.cs`'s sole touch point on the changed surface (`SteamGridDbClient.CapsuleParseNotes`'s `foreach` read, line 969) compiles unchanged against the new `IReadOnlyList<string>` return type — no network call, UI side effect, ordering, or count changed anywhere. Finding F-017 (stable_id F-017) is **resolved**. No unintended scorecard regression observed.

## Loop 5 Implementation Review
Verdict: **approved**. Reason: Both static-collection TOCTOU gaps are genuinely closed with correctly-scoped plain locks, the two new tests assert real concurrent-write behavior (not just no-throw), and no same-or-higher-severity regression was introduced. Checks: reality=passed, honesty=passed, regression=passed. Regressions: none. Conditions: none. Rounds: 1.

## Retired Findings (this loop)
None.
