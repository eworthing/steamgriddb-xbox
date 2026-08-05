### Loop Counter
Loop 2 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Promising, but architecturally immature.**

Runtime ownership took a real step forward this loop: F-014's guard-scope fix (landed loop 1, commit 9b2c4cb) is now visible in current source with no remaining unguarded single-game write path, and state management is credited for it this loop per this project's own established scoring convention. This loop's own refactor (F-013) collapsed the third instance of a duplicated UI-thread entry-update block. The artwork pipeline remains genuinely deep and well-tested. What keeps this short of contest-grade: `PrimaryWidget.xaml.cs` still spans five concerns in one Module, `StoreNameLookup`'s cache-locking inconsistency (F-015) is now confirmed to span all three of its hand-rolled caches (not just two), and the untestable-by-platform-constraint surface (`PrimaryWidget.xaml.cs`) is exactly where every Serious finding this run has lived.

**Prior-audit adopt-or-falsify** (run after this loop's independent scorecard draft, per blind-critic ordering): CODE-REVIEW.md, TESTING.md and ARTWORK-SELECTION.md were adopted/falsified against current source in loop 1 with no open claim dropped; this loop re-confirmed no new prior-audit claim changed status (no edits landed in any of the three source areas those docs cover beyond `PrimaryWidget.xaml.cs`'s entry-update blocks, already inside loop 1's reviewed scope).

## Scorecard (1-10)

- Architecture quality: **7.5** | SAME | Direct re-read this loop of `PrimaryWidget.xaml.cs` in full (2051 lines pre-fix) plus two independently-briefed helper sweeps (Services/Artwork+Library; Services/Stores+SteamGridDB+Models) converge on the same picture as loop 1: `Services/*` remains deep, single-responsibility Modules (`ArtworkRanker.RankGrids`, `TileImage.WithDecoderAsync` genuinely hide implementation behind small Interfaces). `PrimaryWidget.xaml.cs` still spans five concerns (library load/manifest parsing, bulk fix/restore/revert, grid picker, search panel, single-game ops) in one Module; this loop's own fix (F-013) added a private helper inside that same Module rather than reducing its scope. No structural proof of a move either direction.
- State management and runtime ownership: **9.5** | UP | `residual_disposition: accepted` | Structural proof for the UP: `git log 05d06a9..9b2c4cbbeaa7ff019160901b30d51352045d9f2f` shows loop 1's own commit, which this loop's direct re-read of current source confirms landed — `GridImage_Click` (:1504-1522) and `RestoreBackup_Click` (:1891-1911) now both wrap their single-game write in `TryBeginLibraryOperation()`/`EndLibraryOperation()`, closing F-014's gap; no other write path to `GameEntries` or a game's image file bypasses the guard (traced every caller of `ReplaceImageCoreAsync`, `RestoreBackupCoreAsync`, `RestoreAllChangesAsync`, `LoadGameEntriesAsync`). The 9-anchor ("one owner per mutable concern, including process lifetime; writers explicit") is met. Residual blocking 10: `SteamGridDbClient.CapsuleParseNotes` (`SteamGridDbClient.cs:47`) is declared `public static readonly List<string>` — the `readonly` only pins the reference, not the contents, so any external caller can mutate or clear it, unlike every other cross-file mutable-state instance in this codebase (`isLibraryOperationRunning`, the picker session IDs, `AppliedArtworkStore`'s cache) which all keep their writer private. Cosmetic for contest (diagnostic-only list, capped at 5 entries, one actual reader) — accepted, not queued, since promoting it to the backlog this loop would rank below F-013/F-015 on severity and distance-to-target.
- Domain modeling: **9.5** | SAME | `residual_disposition: accepted` | Adversarial Pass re-run: `GameEntry`'s parallel-fields case (`HasSteamGridDBMatch`/`OfficialCapsuleUrl`/`SteamGridDbGameId`, `GameEntry.cs:113-145`, constructed at `PrimaryWidget.xaml.cs:654-668`) re-confirmed live in current source. Smallest candidate fix (readonly-struct-with-factories mirroring `ManifestEntryIdentity.Result`) still fails Simplify Pressure Test Q5 on call-site blast radius (construction site + downstream reads across the file) for a Cosmetic-severity, zero-live-harm concern on a mutable, XAML-data-bound MVVM type.
- Data flow and dependency design: **7.5** | SAME | Direct re-read of `StoreNameLookup.cs` this loop (all 306 lines) plus independent helper confirmation: three static `Dictionary` caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`, :29-34) remain process-lifetime ambient state, exceeding the 9-anchor's "one or two ambient-context dependencies" allowance — this loop's own re-read found `nameMatchCache` (:34, :117-149) has the identical unsynchronized check-then-populate shape as the two caches F-015 already named, which the prior loop's evidence did not cite. `SteamGridDbClient.CapsuleParseNotes` (`SteamGridDbClient.cs:47`) is a fourth instance of the same class. Locking a cache changes its concurrency-safety story, not its data-flow shape, so this count (not F-015's eventual fix) is what keeps the score here.
- Framework / platform best practices: **9.5** | SAME | `residual_disposition: accepted` | `App.xaml.cs:120`'s `//TODO: Load state from previously suspended application` (dead Visual-Studio-template scaffolding on the documented fallback-only `OnLaunched` path) re-confirmed unchanged; not touched by this loop's edits.
- Concurrency and runtime safety: **6.5** | SAME | F-011 (sequential per-entry network calls) re-confirmed unchanged, still blocked by the standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency against SteamGridDB/GOG/Epic/Ubisoft). F-015 re-derived this loop with broadened evidence (`nameMatchCache` added, see data_flow) — still latent, not live, since the sole caller path (`LoadGameEntriesAsync`'s per-entry loop) awaits sequentially. Not fixed this loop (outranked by F-013 per the Backlog Prioritization Pass's item-deferral criterion — see Priority-1 accounting below).
- Code simplicity and clarity: **8.0** | SAME | F-013 (`ReplaceImageCoreAsync`/`RestoreAllChangesAsync`/`RestoreBackupCoreAsync`'s triplicated UI-thread entry-update loop) re-confirmed present at Step 1, at its pre-fix line ranges (`PrimaryWidget.xaml.cs:1101-1114`, `:1169-1188`, `:1962-1981`) — this score reflects source as read at Step 1, before this loop's own Step 2/3 fix; per this project's established convention (see F-014's loop-1-to-loop-2 precedent), the credit for closing F-013 lands in loop 3's Step-1 scorecard against this loop's own commit SHA.
- Test strategy and regression resistance: **6.5** | SAME | `PrimaryWidget.xaml.cs` carries zero direct or indirect test coverage — confirmed again this loop (no test file under `SteamGridDB.Xbox.Tests/` references `PrimaryWidget`; `run-tests.ps1` output unchanged at 138/138 before and after this loop's edit). Mutation-test mental model re-applied: a mutation flipping `GridImage_Click`'s `if (!TryBeginLibraryOperation()) { return; }` guard would silently invert which clicks are allowed to write, and no test in this repo would catch it — this is the same primary-flow gap named in loop 1, unchanged this loop (this loop's own fix, `UpdateSharedEntriesAsync`, sits on the identical untested surface). `Services/*` remains comprehensively covered per the independent helper sweep (12 of 13 reviewed Service files: "Comprehensive"; `ArtworkDownloader.cs`'s network+orchestration combination is the sole, already-known, structurally-untestable exception). Permanent platform constraint (WinUI `Page`, no desktop projection for `Windows.UI.Xaml` types in this repo's xunit infra), not an unaddressed choice — TESTING.md documents the same boundary.
- Overall implementation credibility: **9.5** | SAME | `residual_disposition: queued` (F-015) | Adversarial Pass re-run on the residual (targeted mechanism-level tracing, not blanket re-derivation): this loop's own direct re-read of `StoreNameLookup.cs` strengthened rather than weakened the residual's case — the same gate-reuse fix (`SemaphoreSlim`-backed `AsyncLazyCache<T>`, already used by the file's own Ubisoft cache) still applies cleanly to all three unsynchronized caches, confirming F-015 is exactly the "local, subtractive-fixable" class the 9-anchor's "few honesty leaks" language already accounts for, not a new leak category. F-015's broadened scope (three caches, not two) is additional evidence for the existing queued residual, not a fresh leak.

## Strengths That Matter
- `ArtworkRanker`/`ArtworkDownloader`/`ArtworkSignature`/`TileImage` remain a genuinely deep, pure, well-tested pipeline (no I/O beyond the download itself, no UI) with documented calibration history (`ArtworkDownloader.cs`'s colour-match floor/ceiling) and real Leverage: `GetTitleBearingGridsAsync` (`PrimaryWidget.xaml.cs:1387-1399`, unaffected by this loop) serves both the auto-fixer and the manual picker from one Interface.
- `AppliedArtworkStore` and `StoreNameLookup`'s own Ubisoft cache correctly gate concurrent access to a shared `Dictionary` behind one `SemaphoreSlim`-backed `AsyncLazyCache<T>` — the exact primitive this loop's re-derivation of F-015 confirms the file's other three mutable-state instances (`gogNameCache`, `epicNameCache`, `nameMatchCache`) still do not reuse, so the fix is a known-good pattern away, not a new design.
- This loop's own fix (`UpdateSharedEntriesAsync`, `PrimaryWidget.xaml.cs`) collapsed three independently-drifting UI-thread entry-update blocks into one Interface without adding a new Seam or ceremony layer — matching this codebase's own established "third instance triggers extraction" convention already used for F-002 (four copies) and F-003 (three copies).

## Findings

### Finding #1: ReplaceImageCoreAsync, RestoreAllChangesAsync and RestoreBackupCoreAsync duplicate the same UI-thread entry-update loop three times

**Why it matters** — A future edit to how a tile is refreshed after a write has three call sites to find and keep in sync, and the field-list differences between the three copies (whether `HasBackup` is touched, whether a status message is shown) are not documented as intentional versus oversight.

**What is wrong** — `ReplaceImageCoreAsync` (`PrimaryWidget.xaml.cs:1169-1188` pre-fix), `RestoreAllChangesAsync`'s per-entry block (`:1101-1114` pre-fix) and `RestoreBackupCoreAsync` (`:1962-1981` pre-fix) each independently dispatch to the UI thread and `foreach` over `EntriesSharingImage(game)`, with no shared helper.

**Evidence** — `PrimaryWidget.xaml.cs:1101-1114`; `:1169-1188`; `:1962-1981` (all pre-fix line numbers, as read at Step 1).

**Architectural test failed** — Shallow module (three near-identical Implementations with no shared Interface).

**Leverage impact** — Callers previously had to independently verify each of three copies stayed correct; a shared helper lets one Interface carry the invariant.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal** — none.

**Why this weakens submission** — A leaf-duplication pattern already collapsed twice in this file (F-002 at four copies, F-003 at three) recurring a third time — and being deferred across loops 9, 10 and this run's loop 1 in favor of higher-severity findings — reduces confidence the codebase's own collapse-on-third-instance idiom was being applied consistently until this loop.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — Extract a private `UpdateSharedEntriesAsync(GameEntry game, string imageFileName, BitmapImage image, bool? hasBackup, string statusText)` helper; delete the three independent blocks. `hasBackup: null` and `statusText: null` mean "leave untouched" for the one call site (`RestoreAllChangesAsync`) that doesn't know or doesn't always set that value.

**Blast radius** — Change: `PrimaryWidget.xaml.cs`. Avoid: `Services/**`, `PrimaryWidget.xaml` (markup unchanged).

---

### Finding #2: StoreNameLookup's three hand-rolled caches perform unsynchronized check-then-populate writes, unlike the file's own Ubisoft cache three lines below

**Why it matters** — The type's own doc comment says every cache here is "shared across the whole process," but only the Ubisoft cache (via `AsyncLazyCache<T>`) actually protects that shared state from a concurrent race; a future caller or a future fix to F-011 (blocked below) would silently inherit an unsynchronized write.

**What is wrong** — `gogNameCache`/`epicNameCache` (`StoreNameLookup.cs:29-30`) are plain `Dictionary<string,string>` read/written by `GetOrFetchGogNameAsync` (:89-104) / `GetOrFetchEpicNameAsync` (:203-222) with a bare check-then-populate and no lock — this was known as of loop 1. This loop's own direct re-read of the full file additionally confirms `nameMatchCache` (:34) has the identical shape, populated by `FindGameByNameAsync` (:117-149) with the same unsynchronized check-then-populate. All three sit three lines from `ubisoftGameListCache` (:40-42), which solves the identical shape via `AsyncLazyCache<T>`'s `SemaphoreSlim` gate. No comment explains why three of the file's four caches are exempt from the pattern the fourth uses.

**Evidence** — `StoreNameLookup.cs:29-30`; `:34`; `:89-104`; `:117-149`; `:203-222`; `:40-42`; `AsyncLazyCache.cs:19-60`.

**Architectural test failed** — n/a (concurrency-primitive inconsistency, not a Seam/Module-boundary defect).

**Dependency category** — `in-process`.

**Leverage impact** — A caller gets no concurrent-access-safety guarantee from the Interface alone; safety today depends on the sole caller (`LoadGameEntriesAsync`'s per-entry loop) happening to await sequentially.

**Locality impact** — Fully contained inside `StoreNameLookup.cs`; the fix reuses the file's own existing gate idiom.

**Metric signal** — none.

**Why this weakens submission** — Not currently reachable as a live race (F-011's own evidence: the sole caller awaits sequentially), so latent rather than Serious — but a real structural inconsistency spanning three of the file's four mutable-state fields, not two as previously scoped, in a codebase whose own doc comments elsewhere go out of their way to prevent exactly this (see `AppliedArtworkStore.cs`'s explicit shared-gate rationale).

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — Wrap `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/`FindGameByNameAsync`'s check-then-populate bodies in the same `SemaphoreSlim` gate `ubisoftGameListCache` already uses (or convert each to its own `AsyncLazyCache`-style guarded dictionary), matching `AppliedArtworkStore`'s own shared-gate pattern.

**Blast radius** — Change: `StoreNameLookup.cs`. Avoid: `PrimaryWidget.xaml.cs`, `EpicLibrary.cs`.

---

### Finding #3: GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding or renaming a platform requires remembering to update both switches; nothing fails to compile if one is forgotten (both silently default to `Unknown`/`null`).

**What is wrong** — `FromXboxDirectory` (`GamePlatform.cs:22-46`) and `GamePlatformToSGDBApiString` (:48-67) independently switch over the same platform cases with no shared table. Re-read in full this loop: `FromXboxDirectory` additionally handles legacy folder-name aliases (`"ubi"`/`"ubisoft"`, `"bnet"`/`"battlenet"`) that have no analogue in the reverse mapping, so a naive single dictionary would need to model the alias fan-in explicitly rather than being a trivial two-way fold.

**Evidence** — `GamePlatform.cs:22-46`; `:48-67`.

**Architectural test failed** — n/a.

**Leverage impact** — Each new platform pays this asserted-twice tax; small today, compounds at scale.

**Locality impact** — Contained to `GamePlatform.cs`.

**Metric signal** — none.

**Why this weakens submission** — Minor, but the enum's own home module holds two independent interpretations of itself with no compiler-enforced link.

**Severity** — Cosmetic for contest.

**ADR conflicts** — none.

**Minimal correction path** — Fold both mappings into one table keyed by `GamePlatform`, with a separate small alias list for `FromXboxDirectory`'s legacy folder names.

**Blast radius** — Change: `GamePlatform.cs`. Avoid: `PrimaryWidget.xaml.cs` (call sites unchanged).

---

### Finding #4: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop awaits each entry's SteamGridDB platform-ID lookup, store name-fetch and SteamGridDB name search in strict sequence, even though the awaits are independent across entries.

**Evidence** — `PrimaryWidget.xaml.cs:421-683` (loop bounds re-confirmed this loop at their current, F-014-fix-adjusted line numbers; the sequential-await shape inside is unchanged).

**Architectural test failed** — n/a.

**Leverage impact** — None currently actionable — see blocker.

**Locality impact** — Would be contained to `PrimaryWidget.xaml.cs` and F-015's cache-locking prerequisite, if unblocked.

**Metric signal** — none.

**Why this weakens submission** — A real, linearly-scaling latency cost, but **BLOCKED**: the recorded standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft) means this loop cannot land a change altering observable call ordering/concurrency/count against those services without a product decision this loop cannot make.

**Severity** — Noticeable weakness.

**ADR conflicts** — none.

**Minimal correction path** — BLOCKED this loop; named for continuity (Backlog Prioritization Pass criterion 0) rather than escalated to a user-decision halt, since two other unblocked items filled this loop's backlog.

**Blast radius** — Change: none this loop. Avoid: `PrimaryWidget.xaml.cs` (no change while blocked).

## Simplification Check
- Structurally necessary: Extracting `UpdateSharedEntriesAsync` — a Shallow-module fix (three near-identical Implementations collapsed to one Interface), not a Deletion/Seam-category fix.
- New seam justified: false — `UpdateSharedEntriesAsync` is a private helper method inside the existing `PrimaryWidget` Module, not a new Interface exposed to any second caller class.
- Helpful simplification: Removes the third duplicated instance of a pattern this codebase has already collapsed twice (F-002 at four copies, F-003 at three).
- Should NOT be done: A public/protected version of the helper, or a separate static utility class for it — neither adds Leverage since the only three callers are private methods in this same file; either would be ceremony without reducing ambiguity (fails Simplify Pressure Test Q2).
- Tests after fix: None added or deleted — `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface (WinUI `Page`, no desktop projection for `Windows.UI.Xaml` types in this repo's xunit infra). Verification: full build (exit 0) + full test suite (138/138 unchanged) both re-run before and after the change, independent fresh-eyes implementation review, and a manual line-by-line diff confirming each of the three call sites preserves its exact pre-fix field-write and status-text behavior (including the two intentional differences: `RestoreAllChangesAsync` never touches `HasBackup` or sets `StatusText` from this block; `RestoreBackupCoreAsync` always sets `HasBackup = false`).

## Improvement Backlog
1. **[F-013]** Extract a shared entry-update helper for `ReplaceImageCoreAsync`/`RestoreAllChangesAsync`/`RestoreBackupCoreAsync` — simplification, helpful. Removes the third instance of the leaf-duplication class F-002/F-003 already fixed, and ends a three-loop deferral. Score impact: `simplicity +0.5`.
2. **[F-015]** Gate `StoreNameLookup`'s three hand-rolled caches (`gogNameCache`, `epicNameCache`, `nameMatchCache`) the same way its own Ubisoft cache already is — structural, helpful. Closes a latent unsynchronized-write inconsistency now confirmed to span three fields, not two, and removes credibility's own queued residual. Score impact: `concurrency +0.5; credibility +0.5`.
3. **[F-011]** Parallelize `LoadGameEntriesAsync`'s per-entry network resolution — efficiency, needed for winning once unblocked. **BLOCKED** by the standing user constraint on per-game network-call ordering/concurrency; named for continuity, not actionable this loop. Score impact: `concurrency +1.0`.

**Priority-1 accounting**: F-013 has been open across loops 9, 10 and this run's loop 1 (three consecutive occurrences, `status: open`, each time outranked by a higher-severity Serious finding — most recently F-014, resolved this run's loop 1). Per the Backlog Prioritization Pass's item-deferral criterion (criterion 2, ranked above severity), an item deferred three or more times against a renewable supply of higher-severity findings should have the tie broken toward it once that supply clears — F-014 is now resolved and no higher-severity unblocked candidate exists this loop (F-011 is blocked; F-015, though equally Noticeable in severity, has only one prior occurrence and is not yet a deferral case). F-013 is Priority 1.

## Deepening Candidates
None this loop. `LoadGameEntriesAsync`'s manifest-parsing/game-identity-resolution block (`PrimaryWidget.xaml.cs:354-724`, mostly untouched by UI dispatch except at its start and end) was investigated as a candidate Module extraction — it would gain a testable Interface outside the WinUI `Page`, directly addressing `test_strategy`'s permanent-constraint blocker. It fails the Simplify Pressure Test's Q2 (smallest honest fix) this loop: `CreateThumbnailAsync`, called per-entry inside the loop, requires `Dispatcher`-thread affinity, and `GameEntry` itself carries `BitmapImage`/`StorageFolder` WinRT-typed properties — extracting the loop cleanly would require first splitting `GameEntry` into a pure DTO plus a UI-bound wrapper, a larger, simultaneous redesign this loop's Simplify Pressure Test does not clear on its own. Matches this and prior loops' own conclusion on the same candidate (see Final Judge Narrative).

## Builder Notes

**A near-identical block appearing a third time in one file is this codebase's own established threshold for "extract it" — and letting it sit past that threshold has a cost independent of the block itself.** It collapsed F-002 at four copies and F-003 at three. F-013 was the third instance of this exact shape, correctly identified at loop 9, and sat un-actioned for three loops because a higher-severity finding kept winning Priority 1 each time. The lesson isn't about the duplication — it's that a correctly-deferred item needs its own escalation rule, or "correctly deferred" repeats until the higher-severity supply runs out on its own, which is not the same as the deferred item being handled.

**Distinguish "an item lost every priority contest so far" from "an item isn't worth doing."** Both look identical from inside a single loop — a lower-priority item just sitting in the backlog. The Backlog Prioritization Pass's item-deferral criterion exists because only cross-loop history (three-plus occurrences, all `open`, never selected) can tell them apart; a single loop's own judgment, applied fresh each time, will keep making the locally-correct call and never notice the pattern.

**When extending an existing finding's evidence (F-015 here), re-read the whole file, not just the lines the finding already cites.** The prior evidence for F-015 named two of `StoreNameLookup`'s four mutable-state fields; a full re-read this loop (required regardless, since `StoreNameLookup.cs` was in this loop's Step-1 scope) found a third field with the identical defect shape that the original evidence simply hadn't looked at. Re-deriving a finding from source, not from its own prior evidence list, is how a scoped defect gets its true scope.

## Final Judge Narrative
Place, not win, yet. This loop credited a real prior improvement (F-014, now visibly closed in source) and made one of its own (F-013), ending a three-loop deferral rather than adding a fourth. Runtime ownership is now trustworthy for every traced write path in `PrimaryWidget.xaml.cs` — a genuine, source-proven state_management gain, not a cosmetic one. Simplification helped this loop: the fix collapsed three drifting copies into one Interface with no new Seam and no ceremony, and preserved every field-level behavioral difference between the three original call sites. Concurrency remains trustworthy for what actually executes today (nothing runs off the UI thread), though F-015's re-derivation this loop shows the latent inconsistency is wider than previously scoped (three caches, not two) — worth closing before any future change (including an eventual F-011 fix, still blocked) makes cache access genuinely concurrent. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs` itself: this loop's own fix, like every fix in this file, was verified by direct reading, an independent implementation review, and unchanged build/test evidence — not by a failing test turning green. Future work still risks over-engineering if it tries to extract `PrimaryWidget`'s orchestration into a testable Module wholesale; this loop re-examined that specific candidate (`LoadGameEntriesAsync`'s manifest-parsing block) independently and reached the same conclusion prior loops did, for a source-backed reason (`GameEntry`'s WinRT-typed properties, `CreateThumbnailAsync`'s dispatcher affinity) rather than by anchoring to the prior verdict.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) `state_management` at 9.5 with `CapsuleParseNotes` as the accepted residual — a stricter reading could argue a `public` mutable collection with zero current external writers outside its own class is not yet a live enough concern to be the residual blocking 10, versus simply awarding 10 with no residual named; 9.5 was chosen because the encapsulation gap is real and source-backed even though unexploited. (2) `data_flow` held at 7.5 rather than moving down for finding a third unsynchronized cache (`nameMatchCache`) this loop — the count of ambient-state instances grew in the evidence but not in the codebase (it was already there, just uncited), so SAME was judged the honest delta per the Delta Derivation Guardrail, but a reviewer could reasonably read "found a worse instance of a known class" as a DOWN signal. (3) `test_strategy` held at 6.5 rather than nudged up slightly for `Services/*`'s comprehensive coverage (per this loop's independent helper sweep, 12 of 13 files "Comprehensive") — the score stays capped by the untested `PrimaryWidget.xaml.cs` surface per the rubric's own anti-anchor ("shell seams... score ceiling is 8 regardless"), but exactly how far below that ceiling 6.5 sits versus 7.0 is a judgment call.

## Loop 2 Result
Extracted a private `UpdateSharedEntriesAsync(GameEntry game, string imageFileName, BitmapImage image, bool? hasBackup, string statusText)` helper in `PrimaryWidget.xaml.cs` (added after `EntriesSharingImage`, :337-365) and replaced the three independent UI-thread-dispatch/foreach/status-text blocks in `RestoreAllChangesAsync`, `ReplaceImageCoreAsync` and `RestoreBackupCoreAsync` with calls to it, preserving each call site's exact prior behavior via the `bool? hasBackup` (null = leave untouched) and `string statusText` (null = leave untouched) parameters: `RestoreAllChangesAsync` passes `(null, null)` (never touched `HasBackup` or set `StatusText` from this block originally); `ReplaceImageCoreAsync` passes `(backupExists, <conditional message or null>)`; `RestoreBackupCoreAsync` passes `(false, <conditional message or null>)`. Full build (msbuild, exit 0) and full test suite (run-tests.ps1, 138 passed / 0 failed / 0 skipped) both re-run after the change, unchanged from before — expected, since `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface. No network/file-write call, ordering, or payload changed anywhere; the diff is confined to how already-computed values are written to already-owned UI state. Independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on the targeted finding and the diff only) returned verdict **approved** with all three checks (reality, honesty, regression) passed. Finding F-013 (stable_id F-013) is **resolved**. No unintended scorecard regression observed.
