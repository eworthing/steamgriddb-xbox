### Loop Counter
Loop 9 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

This loop wrote an independent scorecard from current source first (fresh direct reads of `StoreNameLookup.cs`, `JsonRead.cs`, `GamePlatform.cs`, `GameEntry.cs`, `GridImageItem.cs`, `SteamGridDbClient.cs`, `ArtworkDownloader.cs`, `FixLog.cs`, `App.xaml.cs`, the app csproj's target platform, plus two independently-briefed cold helper sweeps — one exhaustive session-guard trace of `PrimaryWidget.xaml.cs`, one `Services/`/`Models/` sweep), before reading `CURRENT_REVIEW.md`/`REVIEW_HISTORY.md` for delta basis. Finding F-010 (`StoreNameLookup` bypassing the `JsonRead` helper, tracked since loop 6) was implemented, verified by build + full test suite + an independent implementation review approved on first pass, net -9 lines. The mandatory Adversarial Pass on `domain_modeling`'s accepted residual tested a smaller counter-proposal than loop 8's own (private-setter encapsulation instead of a discriminated union) and found a fresh, concrete reason it still fails Simplify Pressure Test (blast radius into `PrimaryWidget`'s one construction site). A cold helper sweep surfaced a new, real, Noticeable-severity finding: F-013, a third instance of the entry-update-loop duplication pattern across three `PrimaryWidget.xaml.cs` methods, with observed drift between the copies.

## Scorecard (1-10)

- **Architecture quality**: 7.5 | SAME | `Services/` modules re-confirmed via this loop's own direct reads plus a cold helper sweep of the rest of `Services/`/`Models/` — each remains a deep, single-responsibility Module with a real Interface. A second, independently-briefed helper produced an exhaustive method inventory of `PrimaryWidget.xaml.cs` (46 methods) and independently confirmed the same 5-concern split loop 8 named, with no internal sub-Module boundary beyond the already-extracted `SlidePanelAsync`/`ConfirmAndRunAsync` helpers. **Stalled-Dimension Sweep (loop 9, 4th consecutive SAME)**: 9-anchor re-judged NOT met against this loop's own fresh evidence. Not promoted to a backlog item: `TESTING.md` documents the bulk-operation loops staying in the widget as a deliberate choice, so splitting would not gain testability — fails SPT Q5 (product improves) and Q2 (smallest honest fix for an unproven benefit).
- **State management and runtime ownership**: 7.0 | SAME | A cold, independently-briefed helper sweep this loop traced every async method in `PrimaryWidget.xaml.cs` with an await followed by a state mutation (12 total, not just the 6 already-known guarded sites) and reconfirmed, with zero prior knowledge of the finding history, all 6 previously-fixed session-guard sites hold and the 6 unguarded sites are each correctly unguarded by design. Held SAME rather than moved UP again: re-confirmation is not new structural proof per G8. The dimension's own 9-anchor also requires state "separated by Module, not by convention" — `PrimaryWidget` still mixes runtime/presentation state in one class, the same fact capping architecture_quality. Not DOWN: the helper's flagged multi-writer fields (`lastFocusedButton`, `searchPanelSessionId`) were traced and confirmed single-UI-thread-only with documented intent — smoke, not a finding.
- **Domain modeling**: 9.5 | SAME | **Adversarial Pass re-run this loop** (mandatory) against a genuinely smaller counter-proposal than loop 8's own: private setters on `OfficialCapsuleUrl`/`SteamGridDbGameId` plus factory-style methods on `GameEntry`, instead of a full discriminated-union rewrite. Traced the mechanics: C# object-initializer syntax (used at the sole construction site, `PrimaryWidget.xaml.cs:651-665`) requires public setters for every property set within the same block, so this fix would force splitting the one construction call into two steps — a real blast-radius expansion into `PrimaryWidget`'s single riskiest, most-carefully-handled construction region. **SPT-rejected on Q5** (product improves): the benefit doesn't justify that blast radius for a Cosmetic, zero-live-harm gap. Re-confirmed independently (fresh grep, zero hits) that the three fields remain write-once at construction.
- **Data flow and dependency design**: 7.5 | UP | Finding F-010 resolved this loop: `StoreNameLookup.cs`'s `GetGogGameNameAsync`/`GetEpicGameNameAsync` now route through `JsonRead.Object`/`JsonRead.String` instead of raw `Windows.Data.Json` calls, matching `SteamGridDbClient.cs`/`EpicLibrary.cs`'s established pattern (git diff: 4 insertions, 13 deletions, net -9 lines). This closes the concrete "reuse/consistency" finding data_flow's own proof has cited for 3 consecutive loops. Not promoted past 7.5: the 9-anchor's "one or two ambient-context dependencies" allowance is exceeded — five separate static-mutable-state instances (`StoreNameLookup`'s 3 caches, `SteamGridDbClient.CapsuleParseNotes`, `FixLog`'s 3 static fields) exist without a consolidated ownership story, a freshly-named reason the 9-anchor isn't yet met. Not backlog-worthy on its own: locking without parallelizing delivers no verifiable change; correctly captured as F-011's own prerequisite.
- **Framework / platform best practices**: 10.0 | SAME | **G6 re-verification this loop**: independently checked the app project's target platform (`TargetPlatformIdentifier=UAP`, a genuine legacy UWP `AppContainerExe`) — confirming `SteamGridDbClient.cs`'s `DataContractJsonSerializer`/`Windows.Data.Json` split remains the period-appropriate, idiomatic choice, not a dated pattern with a better alternative available. `StoreNameLookup.cs` is now MORE idiomatic after this loop's own F-010 fix. Could not name a source-backed, behavior-preserving framework improvement: `Debug.WriteLine` usage (flagged by the cold sweep) is a reasonable, low-ceremony choice at this project's scale — a full logging framework would be over-engineering.
- **Concurrency and runtime safety**: 6.5 | SAME | F-011 independently re-confirmed unchanged this loop by direct read — untouched by this loop's F-010 edit. Still blocked by the STANDING USER CONSTRAINT and the unlocked-cache prerequisite, now slightly extended: a cold `Services/` sweep independently found the same "unlocked but safe only by convention" pattern in `FixLog`'s static fields and `SteamGridDbClient.CapsuleParseNotes`, not just `StoreNameLookup`'s three caches. No new concurrency hazard found (nothing is concurrent today); no new session-guard instance found either.
- **Code simplicity and clarity**: 8.0 | SAME | Leaf-module-duplication sweep this loop surfaced a genuine new instance: `ReplaceImageCoreAsync`, `RestoreAllChangesAsync`'s per-entry block, and `RestoreBackupCoreAsync` each independently dispatch-and-foreach with observed drift (`RestoreAllChangesAsync` omits the `HasBackup` write). Not fixed this loop (F-010 was this loop's one refactor); its mere discovery does not move the score down — the source didn't change by being found. Held SAME: F-013 is real evidence the 9-anchor isn't yet met, correctly routed to the backlog rather than changing this loop's score.
- **Test strategy and regression resistance**: 6.5 | SAME | **Stalled-Dimension Sweep (loop 9, 9th consecutive SAME — the single most score-stalled dimension across this run)**: re-applied the mutation-test mental model with a NEW mutation site this loop: swapping `||` for `&&` in `ConfirmAndRunAsync`'s guard (`PrimaryWidget.xaml.cs:787`) would silently change when a destructive bulk operation runs, with no test catching it — a primary-flow gap, so the 9-anchor is genuinely NOT met. F-004 re-confirmed to sit off that primary flow — implementing it would not move this score, which is why it has correctly lost every priority contest for 9 straight loops. The blocker remains a genuine, permanent platform constraint (`Windows.UI.Xaml` has no desktop projection).
- **Overall implementation credibility**: 9.5 | UP | Fresh cross-loop-independent re-verification this loop of two dimensions loop 8 promoted in the same loop it promoted them (`domain_modeling`, `framework_idioms`) — both re-tested from a different angle in a different loop, and both held: stronger evidence than same-loop self-assessment. F-010's fix is real, net-subtractive, verified by build + full test suite + independent review approved on first pass. Two independently-briefed cold sweeps this loop each ran their own doc-vs-code grep and found zero doc-rot. F-013 surfaced by a genuinely cold sweep and independently confirmed by direct reads — the review process still finds real things.

## Authority Map
Empty this loop — no authority/ownership finding is Priority 1 (F-010 is a data-flow/JSON-parsing consistency fix). See loop 7/8 in `REVIEW_HISTORY.md` for the last full Authority Map.

## Strengths That Matter
- The `domain_modeling` Adversarial Pass this loop tested a materially different, smaller counter-proposal (private-setter encapsulation) than loop 8's own (discriminated-union rewrite) and reached a fresh, concrete rejection reason (blast radius into `PrimaryWidget`'s single construction site) rather than reusing loop 8's reasoning — the Adversarial Pass is doing its job, not rubber-stamping.
- An independently-briefed cold helper sweep of `PrimaryWidget.xaml.cs`, told to trace the session-guard pattern generically rather than checking a list of known-fixed sites, reconfirmed all 6 prior fixes hold with zero prior knowledge of the finding history — the strongest form of completeness evidence this run has produced.
- F-013 was found by a helper sweep with no knowledge of this run's finding history, then independently re-verified by direct reads of all three cited method bodies — a genuinely new, real finding surfacing 9 loops into this run's own repeated scrutiny of the same file.

## Findings

### Finding #1 (stable_id F-010): StoreNameLookup's GOG and Epic name-fetch methods bypass the JsonRead helper that exists to prevent a documented JSON null-vs-missing bug

**Why it matters** — The codebase already shipped a bug once (documented in `PrimaryWidget.xaml.cs`'s manifest-parsing comment and `JsonRead.cs`'s own docstring) where a JSON member that was present-but-null was mishandled by raw `Windows.Data.Json` calls; `JsonRead.cs` exists specifically to make that class of bug impossible. Two of `StoreNameLookup`'s three network-backed name-lookup methods used the pattern that caused it.

**What is wrong** — `GetGogGameNameAsync` and `GetEpicGameNameAsync` used raw `ContainsKey`/`GetNamedObject`/`GetNamedString` `Windows.Data.Json` calls instead of the existing `JsonRead.Object`/`JsonRead.String` helper that `SteamGridDbClient.cs` and `EpicLibrary.cs` already use consistently.

**Evidence** — `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:67-77` (pre-fix, `GetGogGameNameAsync`), `:186-191` (pre-fix, `GetEpicGameNameAsync`), `SteamGridDB.Xbox/Services/JsonRead.cs:1-17`, `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs:155-181`

**Architectural test failed** — n/a — a Reuse/consistency finding

**Dependency category** — n/a

**Leverage impact** — None directly - `JsonRead` already exists and is already used by two other call sites; this brings the remaining two into line.

**Locality impact** — Contained to `StoreNameLookup.cs`'s two methods; `JsonRead.cs` itself untouched.

**Metric signal, if any** — none

**Why this weakens submission** — Credibility/consistency deduction, not a live defect (the outer catch prevented a crash even before the fix): the established, documented, purpose-built helper for this exact class of JSON-parsing bug existed in the same file's own dependency graph and was used by two of three sibling call sites plus `SteamGridDbClient.cs` and `EpicLibrary.cs`, yet two methods opted out with no stated reason.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Replaced the raw `ContainsKey`/`GetNamedObject`/`GetNamedString` chains in both methods with `JsonRead.Object(gameData, "_embedded") -> JsonRead.Object(embedded, "product") -> JsonRead.String(product, "title")` (GOG) and `JsonRead.String(gameData, "title")` (Epic), matching `SteamGridDbClient.cs`'s established pattern.

**Blast radius** — Change: `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`. Avoid: `SteamGridDB.Xbox/Services/JsonRead.cs`, `SteamGridDB.Xbox/Services/SteamGridDB/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

**Status this loop: implemented — see Loop 9 Result below.**

### Finding #2 (stable_id F-013): ReplaceImageCoreAsync, RestoreAllChangesAsync and RestoreBackupCoreAsync duplicate the same UI-thread entry-update loop three times

**Why it matters** — Any future change to what happens when a game's tile image is replaced (a new derived property, a new visual state, a new field to keep in sync) has to be found and added in three places by hand, and the three copies have already drifted: `RestoreAllChangesAsync` omits the `HasBackup` write and the per-call status-text update that the other two include.

**What is wrong** — `ReplaceImageCoreAsync` (`PrimaryWidget.xaml.cs:1169-1182`), `RestoreAllChangesAsync`'s per-entry block (`:1101-1108`) and `RestoreBackupCoreAsync` (`:1924-1937`) each dispatch to the UI thread via `OnUiThreadAsync` and `foreach` over `EntriesSharingImage(game)`, writing `entry.Image` and `entry.ImageFileName` in all three, `entry.HasBackup` in two of three, and `StatusText.Text` conditionally in two of three.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1169-1182` (`ReplaceImageCoreAsync`), `:1101-1108` (`RestoreAllChangesAsync`), `:1924-1937` (`RestoreBackupCoreAsync`)

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own image/backup-flag values instead of re-deriving the whole dispatch-and-foreach shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication class as Finding F-002 and F-003 (both resolved), in the same file — a third, smaller instance that survived two prior rounds of exactly this kind of sweep because no prior loop's helper looked at these specific three methods together; the observed drift between the three copies is concrete evidence unsynchronized duplication already costs correctness attention, not just line count.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private helper (e.g. `UpdateEntriesSharingImage(GameEntry game, BitmapImage image, string imageFileName, bool? hasBackup)`) that owns the `OnUiThreadAsync` dispatch and the `EntriesSharingImage(game)` foreach, writing `Image`/`ImageFileName` always and `HasBackup` only when a value is supplied; each call site keeps its own status-text/counter logic outside the helper.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #3 (stable_id F-011): LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — A user whose library has many GOG/Epic games without a direct SteamGridDB match pays the full network latency of the GOG, Epic (or Ubisoft/community-database) endpoint plus the SteamGridDB name-search endpoint for every one of those games, one after another, before the library list finishes loading.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs:455-679`) awaits, in strict sequence for each manifest entry, several independent network calls. The awaits are independent across entries; this is a sequential-independent-effects shape.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:455-679`, `:581`, `:603`, `:612`, `:641`

**Architectural test failed** — n/a — different category (efficiency/D2)

**Dependency category** — n/a

**Leverage impact** — None currently - no seam exists to batch or parallelize through.

**Locality impact** — Contained to `LoadGameEntriesAsync`'s own per-entry loop body and, if fixed, `StoreNameLookup`'s/`FixLog`'s/`SteamGridDbClient`'s static caches' thread-safety.

**Metric signal, if any** — none

**Why this weakens submission** — Hot path (the library reloads on every widget open) doing per-item network I/O one item at a time where nothing in the current design requires that ordering.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Not implemented this loop — blocked by the STANDING USER CONSTRAINT and by StoreNameLookup's three unlocked caches (plus FixLog's static fields and SteamGridDbClient.CapsuleParseNotes, all confirmed unlocked by an independent Services/ sweep this loop), which would need real thread-safety added first.

**Blast radius** — Change: none this loop. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` (both not attempted this loop).

### Finding #4 (stable_id F-012): GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding, renaming, or removing a platform requires updating both switches by hand; nothing enforces they stay in sync, and each has a silent default fallback, so a future skew would degrade silently.

**What is wrong** — `GamePlatformHelper.FromXboxDirectory` (`GamePlatform.cs:22-46`) and `GamePlatformHelper.GamePlatformToSGDBApiString` (`:48-67`) both switch over the same 8-case enum but are independently authored with no shared table.

**Evidence** — `SteamGridDB.Xbox/Models/GamePlatform.cs:22-46`, `:48-67`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None currently; a consolidated metadata table would let a future platform addition touch one place.

**Locality impact** — Contained to `GamePlatform.cs`'s two static methods.

**Metric signal, if any** — none

**Why this weakens submission** — A real but currently-latent duplicate-abstraction risk; not yet manifesting live harm since the six shared cases are currently correctly mirrored in both switches.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Introduce a single static table both methods query, replacing both switch bodies with a lookup.

**Blast radius** — Change: `SteamGridDB.Xbox/Models/GamePlatform.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (both call sites unchanged), `SteamGridDB.Xbox/Services/**`.

### Finding #5 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation at either threshold would ship silently.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) thresholds at `alpha < 64` and `transparentCorners < 2` are untested at their exact boundary. Re-read this loop, unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — Minor, off-primary-flow gap; Cosmetic per the anchor's own carve-out — re-confirmed this loop against a fresh primary-flow mutation (the `ConfirmAndRunAsync` guard) that IS the actual blocker capping test_strategy below 9.5, not this boundary gap.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases at the exact documented boundaries.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs` (test-only addition).

## Simplification Check
- Structurally necessary: Finding F-010's `JsonRead` substitution resolves a real, evidenced consistency gap with a call-site substitution to an existing helper — `JsonRead.cs` already is the seam; this loop brought the last two holdout call sites into line with it.
- New seam justified: No — no new port/adapter added.
- Helpful simplification: F-010's fix is net -9 lines in `StoreNameLookup.cs` (4 insertions, 13 deletions) — genuinely subtractive, and more consistent with the rest of the codebase.
- Should NOT be done: Do not extract F-013's entry-update loop until it is actually implemented (queued for loop 10). When implemented, keep the shared helper narrow (Image/ImageFileName/HasBackup only). Re-confirmed this loop: do not touch `GameEntry`'s match-state construction (Adversarial Pass rejected the smaller encapsulation fix on blast-radius grounds). Do not attempt F-011's fix without first adding real locking to every static cache this loop's sweep identified.
- Tests after fix: None added or deleted — `StoreNameLookup`'s two fixed methods make live network calls and are deliberately untested per `TESTING.md`, confirmed by direct read this loop and independently by the reviewer. Verification: full build + full test suite (138/138 unchanged) + independent implementation review (approved, first pass) + manual trace of `JsonRead`'s null-propagation semantics against every pre-fix failure path.

## Improvement Backlog
1. **Extract a shared entry-update helper for ReplaceImageCoreAsync/RestoreAllChangesAsync/RestoreBackupCoreAsync (Finding F-013).**
   - why it matters: removes a third instance of the leaf-module-duplication class F-002/F-003 already fixed, with concrete evidence (a missing `HasBackup` write) that unsynchronized duplication already costs correctness attention.
   - score impact: `simplicity +0.5`
   - simplification / helpful

2. **Consolidate GamePlatformHelper's two independent switch statements into one shared platform-metadata table (Finding F-012).**
   - why it matters: removes a latent duplicate-abstraction/skew risk before a future platform addition can be silently mishandled.
   - score impact: `simplicity +0.5`
   - simplification / helpful

3. **Add bounded concurrency to LoadGameEntriesAsync's per-entry name/match resolution, after first adding real locking to every static cache (Finding F-011).**
   - why it matters: removes a real, linearly-scaling latency cost on the primary library-load hot path — but not actionable yet: blocked by the STANDING USER CONSTRAINT and the unlocked-cache thread-safety prerequisite.
   - score impact: `concurrency +0.5`
   - structural / helpful

**Priority-1 accounting**: F-010 was Priority 1 this loop as the longest-tracked, fully actionable Noticeable-severity candidate (open since loop 6, re-confirmed unchanged across loops 6-8). F-011 ranks higher on merit (concurrency is tied-lowest-scored dimension) but remains blocked by the STANDING USER CONSTRAINT and the unlocked-cache prerequisite — named here per criterion 0, not silently demoted. For loop 10, F-013 (new this loop) is Priority 1: it is the highest-merit fully actionable candidate, ahead of F-012 (Cosmetic) and F-011 (blocked).

## Deepening Candidates
1. **Candidate Module**: `LoadGameEntriesAsync`'s per-entry name/match resolution (Finding F-011). Smallest first step: add real locking (a `SemaphoreSlim` per cache) to `StoreNameLookup`'s, `FixLog`'s, and `SteamGridDbClient`'s static mutable state BEFORE attempting any concurrency change to the calling loop. What not to do: do not wrap the loop in `Task.WhenAll` before every static cache is locked, and do not attempt the network-ordering half of this fix at all until a behavioural oracle exists.

## Builder Notes

1. **Pattern**: An independently-briefed cold helper sweep with no knowledge of prior loops' finding history is more likely to surface a genuinely new instance of a known defect class than one primed to check whether specific known sites still hold.
   - **How to recognize**: A file has been swept for a defect class N times across N loops, always converging on the same known list, and the sweeps were each briefed with (explicitly or implicitly) the prior list to check against rather than the pattern itself in the abstract.
   - **Smallest coding rule**: When re-sweeping a heavily-reviewed file for a known defect class, brief the sweep on the pattern's shape (e.g. "find dispatch-to-UI-thread-then-mutate-fields blocks"), not on the list of instances already found — this loop's Services/Models sweep found F-013 (three loops of `PrimaryWidget.xaml.cs` review had missed it) precisely because it was not told what to look for beyond the general shape.
   - **Stack example**: F-013's three call sites (`ReplaceImageCoreAsync`, `RestoreAllChangesAsync`, `RestoreBackupCoreAsync`) all sit in `PrimaryWidget.xaml.cs`, the same file F-002/F-003's own leaf-module-duplication sweeps covered — but those sweeps were focused on dialog/animation ceremony, not the smaller entry-update pattern this loop's differently-scoped sweep caught.

2. **Pattern**: An Adversarial Pass on an accepted residual only does its job when it tests a materially different fix than what was already rejected — retesting the same rejected fix and reaching the same conclusion is confirmation, not adversarial re-examination.
   - **How to recognize**: A residual's rationale text is copied near-verbatim loop over loop, citing the same rejected fix candidate each time, without naming a different, smaller alternative that was actually traced through its own mechanics.
   - **Smallest coding rule**: Each loop's Adversarial Pass should name a fix candidate smaller than the smallest one already rejected, and trace its actual mechanical consequences (not just re-state the abstract SPT question) before concluding the residual still holds.
   - **Stack example**: Loop 8 rejected a full discriminated-union rewrite of `GameEntry`'s match state on Q1/Q2. This loop tested a smaller private-setter-encapsulation fix instead, traced that C# object-initializer syntax would force splitting `GameEntry`'s single construction call site, and rejected it on Q5 — a different question, a different mechanical reason, and a genuinely smaller candidate than loop 8 tested.

3. **Pattern**: A dimension can be the most score-stalled item on the board while having zero legitimate backlog-worthy candidates, when its one remaining open finding sits off the primary flow that actually sets the score's ceiling.
   - **How to recognize**: A dimension has been SAME for many loops, one Cosmetic-severity finding under it has also sat open for just as many loops, and every loop's own reasoning says the same thing: the finding is real but doesn't move the number.
   - **Smallest coding rule**: Before assuming a stalled dimension's oldest open finding deserves Priority-1 attention purely because of its own stall count, check whether fixing it would actually move the SCORE (per method.md's mutation-test primary-flow/off-primary-flow branch) — if not, its own stall is a red herring; the dimension's real ceiling is set elsewhere.
   - **Stack example**: test_strategy has been SAME for 9 straight loops and F-004 has sat open just as long, but F-004 (a `TileImage` boundary test) is off the primary flow that caps test_strategy (`PrimaryWidget.xaml.cs`'s untestable reentrancy-guard idiom) — implementing it would not move the score, which is exactly why it has correctly lost every priority contest since loop 1.

### Scorecard humility check
Three scorecard claims this loop's critic is least confident about:
1. `credibility` at 9.5 vs 9.0 (`CURRENT_REVIEW.json` scorecard) — the cross-loop-re-verification argument (loop 9 independently re-testing loop 8's own same-loop promotions) is real evidence, but it is also exactly the kind of reasoning that could be used to justify a promotion almost every loop going forward; a stricter reviewer could reasonably hold this at 9.0 and ask for a second independent loop's re-verification before crediting it.
2. `domain_modeling`'s residual rationale (Adversarial Pass, SPT-rejected on Q5) — the blast-radius argument against the private-setter fix is concrete, but "would need to split one construction call site into two steps" is a real but modest cost; a reviewer who weighs the invariant-safety benefit more heavily than the construction-site-churn cost could reasonably re-open this finding.
3. `data_flow` at 7.5 — the "five static-mutable-state instances exceed the anchor's one-or-two allowance" reasoning is freshly named this loop and has not been tested against a counter-argument (e.g., that all five are documented, intentional, process-lifetime caches for a single-process widget, which might itself satisfy "documented" even at five instances rather than one or two).

## Final Judge Narrative
Place, not win, this loop. This loop's headline result is a genuinely new finding nine loops into repeated scrutiny of the same file: an independently-briefed cold helper sweep, told to look for a general shape rather than a known list, surfaced F-013 — a third, smaller instance of the leaf-module-duplication class F-002 and F-003 already fixed, with concrete evidence of drift between the copies. This loop's own implementation work (F-010, routing `StoreNameLookup`'s last two holdout JSON-reading call sites through the established `JsonRead` helper) is real, net-subtractive (-9 lines), verified by build + full test suite + an independent implementation review that returned `approved` on first pass. The mandatory Adversarial Pass on `domain_modeling`'s accepted residual tested a materially smaller fix than loop 8's own and found a fresh, concrete reason it still fails — genuine re-examination, not rubber-stamping. Runtime ownership is more trustworthy this loop by fresh, independent completeness evidence, though the score itself holds rather than climbs, since re-confirmation of already-credited evidence is not new structural proof. Concurrency's own blocker widened slightly in scope (`FixLog` and `SteamGridDbClient.CapsuleParseNotes` join `StoreNameLookup`'s caches as unlocked-but-safe-by-convention state) without changing its disposition. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; test_strategy's own 9-loop stall is now confirmed to be a genuine structural ceiling rather than an unaddressed choice, since its one remaining candidate (F-004) provably would not move the score even if implemented. Future work risks over-engineering only if F-013's eventual extraction reaches for a shared status-text abstraction beyond the narrow Image/ImageFileName/HasBackup fields, or if F-011's eventual fix attempts to parallelize the network loop before locking every static cache this loop's sweep identified.

## Loop 9 Result
Changed `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` only (4 insertions, 13 deletions, net -9 lines): replaced the raw `Windows.Data.Json` `ContainsKey`/`GetNamedObject`/`GetNamedString` call chains in `GetGogGameNameAsync` and `GetEpicGameNameAsync` with calls to the existing `JsonRead.Object`/`JsonRead.String` helper, matching `SteamGridDbClient.cs`'s and `EpicLibrary.cs`'s already-established pattern. Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after — unchanged, as expected, since these two methods make live network calls and are deliberately untested per `TESTING.md`. Manually traced `JsonRead.Object`/`JsonRead.String`'s null-propagation semantics against every pre-fix failure path (member absent, member present-but-JSON-null, member present-but-wrong-type) and confirmed each now falls through to the same `null` return as before. An independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on Finding F-010 and the diff only) returned verdict `approved` with all three checks (reality, honesty, regression) passed on the first pass, and independently re-ran the full test suite confirming 138/0/0. Finding F1 (stable_id F-010) is **resolved**. This loop also surfaced a new Finding F-013 (a third instance of the entry-update-loop leaf-module-duplication class in `PrimaryWidget.xaml.cs`, queued for loop 10) and independently re-verified Findings F-011/F-012/F-004 unchanged. No unintended scorecard regression: the change touches no network call ordering, no ranking/selection logic, and no file outside the one named.

## Loop 9 Implementation Review
Verdict: **approved**. Reason: both methods now route entirely through `JsonRead.Object`/`JsonRead.String` with no raw `ContainsKey`/`GetNamedObject`/`GetNamedString` remaining, every failure path traced by hand converges to the same `null` return as before, only `StoreNameLookup.cs` changed, and the full suite still passes 138/0/0. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.
