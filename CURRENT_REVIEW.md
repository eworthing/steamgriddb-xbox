### Loop Counter
Loop 8 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

This loop wrote an independent scorecard from current source first (full re-read of the Priority-1 target region in `PrimaryWidget.xaml.cs`, `StoreNameLookup.cs`, `JsonRead.cs`, `AppliedArtworkStore.cs`, `GameEntry.cs`, `GridImageItem.cs`, `GamePlatform.cs`, `App.xaml.cs`, `MainPage.xaml.cs`, the mandatory doc-vs-code grep, and a fresh `new GameEntry(` construction-site grep across the whole tree), then two fresh-eyes helper sweeps briefed on the codebase's history but not its prior conclusions - one a cold Reuse/Simplification/Altitude/Efficiency/security sweep of `Services/` and `Models/`, the other a test-strategy mutation-check plus domain-modeling audit - only after which `CURRENT_REVIEW.md`/`REVIEW_HISTORY.md` were read for delta basis. This loop's Priority-1 finding (F-003, the three-times-duplicated confirmation-dialog ceremony, queued seven loops) was implemented, verified by build + full test suite + independent implementation review, and along the way caught and fixed its own transient regression (a stale doc comment left orphaned above the new helper by the extraction) before it ever reached a diff review. The two stalled dimensions with the longest SAME-streaks on the board - `domain_modeling` (seven consecutive loops, tied) and `framework_idioms` (seven consecutive loops) - both got the Residual Accounting Pass they were overdue for: `domain_modeling` promotes to 9.5 with a fully adversarially-tested accepted residual; `framework_idioms` promotes to a genuine 10 after eight cumulative loops of scrutiny (including this loop's own fresh sweeps) named zero remaining source-backed candidates. A cold helper sweep also surfaced a new, low-severity finding (F-012): `GamePlatformHelper`'s two independent switch statements over `GamePlatform` have no shared source of truth.

## Scorecard (1-10)

- Architecture quality: 7.5 | SAME | `Services/` modules re-confirmed (via this loop's own reads and the cold helper sweep) each still a deep, single-responsibility Module with a real Interface - no new Module-graph-level concern surfaced. `PrimaryWidget.xaml.cs` is now 2010 lines (2032 pre-fix; this loop's own F-003 fix is net -22 lines) and F-003's dialog ceremony is now resolved (see Findings), but the class itself remains one large single-Module orchestrator handling library loading, dialog confirmation, panel animation, grid selection and search in one file. **Stalled-Dimension Sweep (loop 8, 3rd consecutive SAME):** ran the Residual Accounting Pass explicitly this loop rather than repeating the prior note verbatim. The 9-anchor ("Module graph enforced by source, not convention... deletion tests leave little pass-through structure") is judged NOT met while `PrimaryWidget.xaml.cs` remains one large Module covering five distinct concerns with no further internal Module boundaries - this is a genuine, not cosmetic, gap. It is not promoted to a valid backlog item this loop: splitting `PrimaryWidget.xaml.cs` into several owned sub-Modules would be a large, high-risk restructuring of an untestable UWP page (no automated regression net), and the incremental, behavior-preserving extractions actually available (F-002, F-003, both now resolved; F-010's JsonRead routing) are already tracked as `simplicity`/`data_flow` items rather than Module-graph items - a bigger split has no proven friction beyond what those smaller fixes already address, so it fails Simplify Pressure Test Q2 (not the smallest honest fix for a benefit that isn't proven). `residual_blocker_kind: "structural_anchor_unmet"`.
- State management and runtime ownership: 7.0 | SAME | F-001/F-005 through F-009's eight guarded call sites spot-checked this loop (via grep for `SlidePanelAsync`/session-guard lines) and confirmed still holding, unaffected by this loop's F-003 edit (a disjoint region of the file). No fresh exhaustive completeness sweep was run this loop (that work was loop 7's, closed with zero new instances after cross-verification); this loop's own investigation focus was elsewhere (F-003, plus the Residual Accounting/Adversarial passes on `domain_modeling`/`framework_idioms`). Held at SAME rather than moved UP again: G8 requires structural proof of *fresh* completion evidence to move up, and re-asserting last loop's already-credited completeness sweep without new work would be anchoring, not re-deriving. Not moved DOWN either: no regression, no new hazard found.
- Domain modeling: 9.5 | UP | **Residual Accounting Pass run explicitly this loop** (dimension was SAME 7 consecutive loops - the longest-tied stall on the board alongside `framework_idioms`). 9-anchor ("Domain types prove most invariants by construction... one or two parallel-fields cases remain but are documented") judged MET: independently re-confirmed via a fresh, whole-tree `grep -rn "new GameEntry"` (this loop, not carried from a prior loop's claim) that `PrimaryWidget.xaml.cs:651-665` remains the sole construction site for `GameEntry`, and that its `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` three-independently-settable-properties case is documented via XML doc comments on each property (`GameEntry.cs:128-145`) - matching the anchor's own "one or two parallel-fields cases remain but are documented" language nearly verbatim. `GridImageItem.cs` and `GamePlatform.cs` re-read in full this loop (directly and via helper): no comparable concern, no impossible-state risk (their fields are flat external-API display data with no domain invariant to violate). **Adversarial Pass** (mandatory before accepting the residual): smallest possible fix considered - collapse `HasSteamGridDBMatch` into a computed property derived from `SteamGridDbGameId > 0 || OfficialCapsuleUrl != null` (verified via a fresh grep for `.HasSteamGridDBMatch =`/`.SteamGridDbGameId =`/`.OfficialCapsuleUrl =` outside the construction site: zero hits, confirming all three are write-once, so no `PropertyChanged` notification would be lost). **SPT-rejected on Q1**: this fix removes one symptom but does not enforce the actual invariant (mutual exclusivity of the platform-ID-match path vs. the name-match path's own field) - a caller could still set `SteamGridDbGameId` and `OfficialCapsuleUrl` both non-default, an impossible state under current business logic, representable either way. The fix that would actually close the gap - a discriminated `MatchResult` replacing all three properties via a smart constructor - is the same factory-method rewrite already rejected on Q2 as ceremony disproportionate to a Cosmetic, never-yet-harmful concern on a mutable, XAML-bound MVVM type. Residual holds. `residual_blocking_10`: "`GameEntry.cs:113-145`'s `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` parallel-fields case, sole construction site `PrimaryWidget.xaml.cs:651-665`." `residual_disposition: "accepted"`. `residual_rationale_or_backlog_ref`: "Fails Simplify Pressure Test (Adversarial Pass, this loop): the only fix that closes the real invariant is a discriminated-union/smart-constructor rewrite of a mutable, XAML-data-bound MVVM type for a Cosmetic-severity, no-live-harm concern - ceremony disproportionate to the fix size, eight loops running. No ADR exists in this repo; disposition rests on the SPT-Q2/ceremony branch of the Residual Accounting Pass, not a framework or ADR carve-out."
- Data flow and dependency design: 7.0 | SAME | `Services/*` re-confirmed zero `Windows.UI.Xaml` imports (re-grepped this loop). Finding F-010 (`StoreNameLookup.GetGogGameNameAsync`/`GetEpicGameNameAsync` bypassing `JsonRead`) independently re-confirmed unchanged this loop by a full fresh read of `StoreNameLookup.cs` (untouched by this loop's F-003 edit, which is confined to `PrimaryWidget.xaml.cs`) and by the cold helper sweep, which reached the identical finding without seeing loop 6/7's text. Queued residual - 9-anchor not fully met while F-010 (Noticeable, SPT-passing, well-scoped) remains open; not promoted, correctly stays a backlog item rather than an accepted residual since a real, actionable, not-yet-implemented fix exists. Outranked this loop by F-003 on stall (seven consecutive SAME loops on `simplicity` vs. F-010's own one-loop stall on `data_flow`); Priority 1 for loop 9.
- Framework / platform best practices: 10.0 | UP | **Residual Accounting Pass run explicitly this loop** (dimension was SAME 7 consecutive loops, tied with `domain_modeling` for the longest stall on the board). 9-anchor ("Stack idiomatic in primary surfaces... one or two non-idiomatic carve-outs documented") judged met, and this loop found **no remaining candidate to even carve out**: `SteamGridDbClient.cs:273-301`'s `BuildUrl` and its `DataContractJsonSerializer`/`Windows.Data.Json` split re-verified fixed and holding; `App.xaml.cs` and `MainPage.xaml.cs` read in full for the first time this loop (not previously cited in any prior loop's evidence) - both are minimal, idiomatic UWP/Xbox-Game-Bar-widget boilerplate (`Suspending` deferral pattern used correctly, `Frame` navigation, the one `//TODO` matches README's own documented EA-App limitation, not doc rot); `StoreNameLookup.cs`/`AppliedArtworkStore.cs`/`JsonRead.cs` re-read directly this loop, idiomatic throughout (`StorageFile`/`StorageFolder` disposal, `SemaphoreSlim`-gated `AsyncLazyCache<T>` reuse). The cold helper sweep's one candidate under this dimension - `SteamGridDbClient.DeserializeJson<T>` constructing a fresh `DataContractJsonSerializer` per call instead of caching one per `T` - was investigated and rejected as a true micro-optimization (Ignore-list): the cost is sub-millisecond reflection dwarfed by the network round-trip the same call already pays (50-500ms), it recurs once per HTTP response rather than N× within one logical pass (not the D1 shape), and there is no more-idiomatic built-in being bypassed. Per the Score Anchors' own rule ("No source-backed residual can be named -> set score to 10"), holding this at 8/9 any longer would itself be under-scoring against the rubric's own text, not conservatism.
- Concurrency and runtime safety: 6.5 | SAME | F-011 (`LoadGameEntriesAsync`'s sequential per-entry network calls, `PrimaryWidget.xaml.cs:455-679`) independently re-confirmed unchanged this loop by direct read - that region sits above this loop's F-003 edit point (743+) and is byte-identical. Still blocked by the STANDING USER CONSTRAINT and the unlocked-`StoreNameLookup`-cache prerequisite (also re-confirmed this loop: `gogNameCache`/`epicNameCache`/`nameMatchCache` remain plain unlocked `Dictionary`s). No new concurrency hazard found; no fresh completeness sweep run this loop (see state_management). SAME, not UP or DOWN - no fresh structural evidence in either direction.
- Code simplicity and clarity: 8.0 | UP | Finding F-003 (three near-identical `ContentDialog`-construction-plus-guard-and-run bodies, `FixLibraryButton_Click`/`RestoreChangesButton_Click`/`RevertDefaultsButton_Click`) fixed this loop: all three call sites now delegate to one shared private `ConfirmAndRunAsync(title, content, primaryButtonText, secondaryButtonText, shouldRun, action)` helper (`PrimaryWidget.xaml.cs:740-800`), each collapsing from a ~35-45-line hand-built `ContentDialog`+guard body to a 7-9 line parameterized call; net -22 lines in the file (140 lines changed: 59 insertions, 81 deletions). Verified behavior-preserving: every call site carries its original literal title/content/button-text values, the same `Style`/`PrimaryButtonStyle`/`SecondaryButtonStyle`/`CloseButtonStyle` resource assignments (secondary style now conditional on a non-empty secondary text, matching the pre-fix behavior where only `FixLibraryButton_Click`'s dialog set it), the same `XamlRoot` API-contract check, and the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` short-circuit ordering (`shouldRun(result)` evaluated before `TryBeginLibraryOperation()`, matching the original `&&`/`||` short-circuit semantics exactly). Stall broken after seven consecutive SAME loops (1-7) - the longest-running simplicity stall this run has seen, one tier past F-002's own five-loop stall. `PrimaryWidget.xaml.cs` no longer carries any of the leaf-module duplication tracked since loop 1 (F-002 and F-003 both now resolved); the leaf-module duplication sweep this loop (direct read + cold helper sweep of `Services/`/`Models/`) found the two-switch `GamePlatformHelper` concern (F-012, new, Cosmetic) as the only remaining candidate anywhere in the codebase.
- Test strategy and regression resistance: 6.5 | SAME | **Stalled-Dimension Sweep (loop 8, 7th consecutive SAME, lowest-scored dimension on the board):** re-applied the mutation-test mental model (method.md Step 8) fresh this loop rather than repeating "still can't test it." Named mutation: deleting the guard `if (session != gridPanelSessionId) { return; }` in `HideGridPanelAsync` (`PrimaryWidget.xaml.cs:1632`, unaffected by this loop's edit) would not be caught by any test - `PrimaryWidget.xaml.cs` carries zero test coverage of any kind (structurally excluded from `SteamGridDB.Xbox.Tests`, confirmed via `TESTING.md` and the `.csproj` target-framework mismatch, re-verified this loop). This mutation sits **on a primary flow** (the exact reentrancy-guard idiom that took nine rounds of hand-verification across F-001/F-005 through F-009), not an off-path helper - per method.md Step 8's own branch logic this means the 9-anchor is genuinely NOT met (not merely under-credited), so no Residual-Accounting promotion applies here the way it did for `domain_modeling`/`framework_idioms` above. Considered, and re-rejected, extracting the guard comparison into a standalone testable "SessionGuard" type (loop 6's own rejected candidate, re-tested this loop): fails SPT Q1 - the ambiguity that matters is *placement* (is the check correctly positioned after every hazardous `await`?), which a unit test of an extracted int-comparison could never exercise; the real test surface remains `PrimaryWidget.xaml.cs` itself, structurally untestable. `residual_blocker_kind: "structural_anchor_unmet"`; the blocker is a genuine platform/build-system constraint (UWP page type has no desktop test projection), not an unaddressed choice - everything extractable from this file already has been (`ManifestEntryIdentity`, `GameImages`, `OperationReport`, `JsonRead` were all pulled out of it specifically for this reason, per `TESTING.md`'s own account).
- Overall implementation credibility: 9.0 | UP | F-003's fix independently reviewed by a fresh-eyes subagent briefed cold on the diff and the targeted finding only, returning `approved` with all three checks (reality, honesty, regression) passed on the first pass. Distinct fresh evidence this loop, not a repeat of prior loops' pattern: this loop's own Step 1 investigation caught and corrected its own transient defect before it ever reached the reviewer or a commit - the `ConfirmAndRunAsync` extraction initially left a stale `/// <summary>... Handles fix library button click...` doc comment orphaned above the new helper (describing the wrong method after the extraction moved `FixLibraryButton_Click` below it); this was caught by re-diffing the change before rebuilding, fixed, and re-verified (build + test both re-run clean after the fix) - the loop's own verification discipline is catching its own mistakes, not just the codebase's. Structural proof for the UP move beyond F-003: the mandatory doc-vs-code grep (`LEGACY|TEMPORARY|DEPRECATED|DO NOT|...`) run fresh this loop found exactly three hits, all genuine and accurate (no doc rot); the Residual Accounting Pass and Adversarial Pass were both run with real, falsifiable reasoning (not rubber-stamped) on `domain_modeling`'s residual, correctly promoting one dimension while correctly declining to promote `test_strategy` for a structurally distinct reason - evidence the review methodology discriminates rather than defaults to the generous read.

## Authority Map
Not re-emitted in full this loop: no state_management/concurrency-relevant authority changed (F-003 is a dialog-and-guard-wrapping extraction with zero mutable-state implications; F-010/F-011/F-012 remain unimplemented). See loop 7's Authority Map (`REVIEW_HISTORY.md`) for the still-current picker-panel and library-load authority maps, both re-confirmed unaffected by this loop's edit via direct grep of the guarded call sites.

## Strengths That Matter
- This loop's own review methodology caught and fixed its own defect (the orphaned doc comment described above) before it reached the implementation reviewer or a commit - a concrete instance of the loop's verification discipline working as designed, not just asserted.
- The `domain_modeling` Adversarial Pass (this loop) demonstrates the review process can correctly *reject* its own proposed simplification when the smaller fix doesn't actually close the real invariant, rather than taking the first available subtractive-looking option - the SPT Q1 rejection of the "computed `HasSteamGridDBMatch`" idea is reasoned through to a concrete counter-scenario (both fields settable simultaneously), not asserted.
- `AsyncLazyCacheTests.cs`'s 32-concurrent-caller test (`Loads_once_however_many_callers_arrive_together`) remains genuine concurrency verification under real `Task.Run` parallelism, re-confirmed present and unaffected this loop.

## Findings

### Finding #1 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations had to be kept in sync by hand - the same style-resource assignment, the same `XamlRoot` API-contract check, the same `TryBeginLibraryOperation`/`EndLibraryOperation` guard wiring - and nothing enforced that a future fourth operation (or an edit to one of the three) followed the same shape.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (pre-fix lines `743-787`, `789-823`, `825-859`) each built a `ContentDialog` with the same four style-resource lookups and the same `Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent` `XamlRoot` check, called `ShowAsync`, branched on the result, and wrapped the actual operation in the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` pattern.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:743-787` (pre-fix, `FixLibraryButton_Click`), `:789-823` (pre-fix, `RestoreChangesButton_Click`), `:825-859` (pre-fix, `RevertDefaultsButton_Click`)

**Architectural test failed** — Shallow module (each Click handler's Interface ≈ its Implementation; no reuse across the three near-identical bodies)

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action instead of re-deriving the whole confirm-guard-run shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as Finding F-002 (resolved loop 7), in the same file; this cluster alone accounted for roughly 105 of `PrimaryWidget.xaml.cs`'s lines being ceremony repeated 3x rather than owned once. Seven loops queued before being fixed this loop, matching F-002's own five-loop stall pattern one tier higher.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extracted a private `ConfirmAndRunAsync(string title, string content, string primaryButtonText, string secondaryButtonText, Func<ContentDialogResult, bool> shouldRun, Func<ContentDialogResult, Task> action)` that owns dialog construction, the `XamlRoot` check, and the `TryBeginLibraryOperation`/`EndLibraryOperation` wrapping; each of the three handlers now calls it naming its own title/content/action. The action receives the dialog result so `FixLibraryButton_Click` (the one caller with a secondary button) can still branch on Primary vs Secondary.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml` (markup unchanged), `SteamGridDB.Xbox/Services/**`.

**Status this loop: implemented — see Loop 8 Result below.**

### Finding #2 (stable_id F-010): StoreNameLookup's GOG and Epic name-fetch methods bypass the JsonRead helper that exists to prevent a documented JSON null-vs-missing bug

**Why it matters** — The codebase already shipped a bug once (documented in `PrimaryWidget.xaml.cs:470-478` and `JsonRead.cs:13-16`) where a JSON member that was present-but-null was mishandled by raw `Windows.Data.Json` calls; `JsonRead.cs` exists specifically to make that class of bug impossible. Two of `StoreNameLookup`'s three network-backed name-lookup methods still use the pattern that caused it.

**What is wrong** — `GetGogGameNameAsync` (`StoreNameLookup.cs:67-74`) reads `gameData.ContainsKey("_embedded") && gameData.GetNamedObject("_embedded").ContainsKey("product")`, then `gameData.GetNamedObject("_embedded").GetNamedObject("product")`, then `product.GetNamedString("title")` - all raw `Windows.Data.Json` calls. `GetEpicGameNameAsync` (`:188-190`) does the same for `gameData.GetNamedString("title")`. `GetNamedObject`/`GetNamedString` throw `InvalidOperationException` when the member is present but JSON `null`, which `ContainsKey` cannot distinguish from a normal value - the exact ambiguity `JsonRead.Object`/`JsonRead.String` were written to resolve, and which `SteamGridDbClient.cs:155-181` and `EpicLibrary.cs` both correctly use instead. The outer broad `catch (Exception ex)` in both methods means a null-title response would not crash - it would silently degrade to "name not found" - so there is no live crash risk today; the harm is a maintained inconsistency next to the exact helper built to remove it.

**Evidence** — `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:67-74` (`GetGogGameNameAsync`), `:188-190` (`GetEpicGameNameAsync`), `SteamGridDB.Xbox/Services/JsonRead.cs:1-17`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:470-478`, `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs:155-181`

**Architectural test failed** — n/a - a Reuse/consistency finding, not a Seam/Module-boundary finding

**Dependency category** — n/a

**Leverage impact** — None directly - `JsonRead` already exists and is already used by two other call sites; this brings the remaining two into line.

**Locality impact** — Contained to `StoreNameLookup.cs`'s two methods; `JsonRead.cs` itself untouched.

**Metric signal, if any** — none

**Why this weakens submission** — Credibility/consistency deduction, not a live defect: the established, documented, purpose-built helper for this exact class of JSON-parsing bug exists in the same file's own dependency graph and is used by two of the three sibling call sites plus `SteamGridDbClient.cs` and `EpicLibrary.cs`, yet two methods opt out with no stated reason.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Replace the raw `ContainsKey`/`GetNamedObject`/`GetNamedString` chains in both methods with `JsonRead.Object(gameData, "_embedded")` → `JsonRead.Object(embedded, "product")` → `JsonRead.String(product, "title")` (GOG) and `JsonRead.String(gameData, "title")` (Epic), matching `SteamGridDbClient.cs`'s established pattern. Behavior-preserving per the five properties (call count, ordering, payload, error handling, observable result).

**Blast radius** — Change: `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`. Avoid: `SteamGridDB.Xbox/Services/JsonRead.cs`, `SteamGridDB.Xbox/Services/SteamGridDB/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

### Finding #3 (stable_id F-012): GamePlatformHelper's Xbox-folder-name and SteamGridDB-API-string mappings are two independent switch statements over GamePlatform with no shared source of truth

**Why it matters** — Adding, renaming, or removing a platform requires updating both switches by hand; nothing enforces they stay in sync, and each has a silent (non-crashing) default fallback, so a future skew would degrade silently - a new platform's names would not resolve to SteamGridDB, or vice versa - rather than fail loudly.

**What is wrong** — `GamePlatformHelper.FromXboxDirectory` (`GamePlatform.cs:22-46`) maps Xbox `ThirdPartyLibraries` folder-name strings to `GamePlatform`; `GamePlatformHelper.GamePlatformToSGDBApiString` (`GamePlatform.cs:48-67`) maps `GamePlatform` back to SteamGridDB's own API platform strings. Both switch over the same 8-case enum but are independently authored with no shared table; the six platform cases both switches cover (Steam/GOG/Epic/Ubisoft/BattleNet/EA) are each asserted twice.

**Evidence** — `SteamGridDB.Xbox/Models/GamePlatform.cs:22-46` (`FromXboxDirectory`), `:48-67` (`GamePlatformToSGDBApiString`)

**Architectural test failed** — n/a - a Reuse/consistency finding (duplicate abstraction smell), not a Seam/Module-boundary finding

**Dependency category** — n/a

**Leverage impact** — None currently; a consolidated metadata table would let a future platform addition touch one place.

**Locality impact** — Contained to `GamePlatform.cs`'s two static methods.

**Metric signal, if any** — none

**Why this weakens submission** — A real but currently-latent duplicate-abstraction risk (Simplicity dimension's own "duplicate abstractions" smoke item), surfaced by an independent, cold helper sweep this loop; not yet manifesting live harm since the six shared cases are currently correctly mirrored in both switches.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Introduce a single static table of `(GamePlatform, xboxFolderName, alternateXboxFolderName, sgdbApiString)` entries that both `FromXboxDirectory` and `GamePlatformToSGDBApiString` query, replacing both switch bodies with a lookup; `Custom`'s special-cased folder name and lack of an SGDB string stay expressed as `null`/absent in the table rather than as a code-path difference.

**Blast radius** — Change: `SteamGridDB.Xbox/Models/GamePlatform.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` (both call sites unchanged, same signatures), `SteamGridDB.Xbox/Services/**`.

### Finding #4 (stable_id F-011): LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — A user whose library has many GOG/Epic games without a direct SteamGridDB match pays the full network latency of the GOG, Epic (or Ubisoft/community-database) endpoint plus the SteamGridDB name-search endpoint for every one of those games, one after another, before the library list finishes loading - which happens on every widget open, not once.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs:455-679`) awaits, in strict sequence for each manifest entry: `sgdbClient.GetGameByPlatformIdAsync` (`:581`), then when unmatched one of `StoreNameLookup.GetOrFetchGogNameAsync` (`:603`) / `GetOrFetchEpicNameAsync` (`:612`) / `GetUbisoftGameNameAsync` (`:621`), then `StoreNameLookup.FindGameByNameAsync` (`:641`) - each entry's network calls fully complete before the next entry's loop iteration starts any of its own. The awaits are independent across entries; this is a sequential-independent-effects shape.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:455-679`, `:581`, `:603`, `:612`, `:641`

**Architectural test failed** — n/a - efficiency/D2, not a Seam/Module-boundary finding

**Dependency category** — n/a

**Leverage impact** — None currently - no seam exists to batch or parallelize through.

**Locality impact** — Contained to `LoadGameEntriesAsync`'s own per-entry loop body and, if fixed, `StoreNameLookup`'s three dictionaries' thread-safety.

**Metric signal, if any** — none

**Why this weakens submission** — Hot path (the library reloads on every widget open) doing per-item network I/O one item at a time where nothing in the current design requires that ordering.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Not implemented this loop - blocked by the STANDING USER CONSTRAINT (no behavioural oracle for per-game network-call ordering/concurrency changes) and by `StoreNameLookup`'s three unlocked caches, which would need real thread-safety added first.

**Blast radius** — Change: none this loop (not attempted). Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` (both not attempted this loop).

### Finding #5 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation at either threshold (`<` to `<=`) would ship silently.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) treats a pixel transparent when `alpha < 64` (`:250`) and rejects the image when `transparentCorners < 2` (`:263`). Untested at either exact boundary. Re-read this loop, unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — Minor, off-primary-flow gap; Cosmetic per the anchor's own carve-out.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases at the exact documented boundaries.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs`.

## Simplification Check
- Structurally necessary: Finding F-003's `ConfirmAndRunAsync` extraction resolves a real, evidenced leaf-module duplication (three near-identical `ContentDialog`/guard-and-run bodies) - passes the Shallow module test (Interface ≈ Implementation three times over; now one Interface, one Implementation, three thin callers).
- New seam justified: No. Considered and rejected: an `IConfirmationCoordinator`/`DialogService` protocol for `ConfirmAndRunAsync` fails the Unified Seam Policy's two-adapter rule immediately (no second Adapter exists or is proposed; private in-process UI glue with one production caller-family), and no single-Adapter policy/failure/platform-isolation justification applies either. A plain private helper method is the correct, and only defensible, shape - re-affirms loop 6's own Deepening Candidate guidance for F-002's identical shape.
- Helpful simplification: F-003's fix is net -22 lines in `PrimaryWidget.xaml.cs` (140 lines changed: 59 insertions, 81 deletions) - genuinely subtractive.
- Should NOT be done: Do not introduce an `IConfirmationCoordinator`/`DialogService` protocol (see above). Also re-confirmed this loop: do not attempt F-011's fix without first adding real locking to `StoreNameLookup`'s three caches; do not collapse `GameEntry.HasSteamGridDBMatch` into a computed property (Adversarial Pass this loop: fails SPT Q1, does not close the real invariant).
- Tests after fix: None added or deleted - `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface and cannot be unit-tested in the current infra, matching every prior fix to this file since loop 1. Verification is: full build (exit 0) + full test suite (138/138 unchanged) both re-run after the change, an independent fresh-eyes implementation review (separate subagent, read-only, verdict `approved`, all three checks passed), plus a line-by-line diff confirming every call site's title/content/button-text literal is unchanged and the `TryBeginLibraryOperation`/`EndLibraryOperation` short-circuit ordering is preserved exactly.

## Improvement Backlog
1. **Route StoreNameLookup's GOG and Epic name-fetch methods through the existing JsonRead helper (Finding F-010).**
   - why it matters: removes a documented-bug-class inconsistency next to the exact helper built to prevent it; small, mechanical, behavior-preserving per the five properties named in the finding.
   - score impact: `data_flow +0.5`
   - simplification
   - helpful

2. **Consolidate GamePlatformHelper's two independent switch statements into one shared platform-metadata table (Finding F-012).**
   - why it matters: removes a latent duplicate-abstraction/skew risk before a future platform addition can be silently mishandled by one switch and not the other.
   - score impact: `simplicity +0.5`
   - simplification
   - helpful

3. **Add bounded concurrency to LoadGameEntriesAsync's per-entry name/match resolution, after first adding real locking to StoreNameLookup's caches (Finding F-011).**
   - why it matters: removes a real, linearly-scaling latency cost on the primary library-load hot path - but ranked last and not actionable yet: blocked by the STANDING USER CONSTRAINT (no behavioural oracle for per-game network-call ordering/concurrency changes) and by the unlocked-cache thread-safety prerequisite named in this finding's own remedy.
   - score impact: `concurrency +0.5`
   - structural
   - helpful

**Priority-1 accounting**: F-010 is Priority 1 for loop 9 as the highest-merit **actionable** candidate (data_flow, one-loop stall, well-scoped, small, mechanical remedy already fully specified). F-011 ranks *above* F-010 on pure distance-to-target (`concurrency` at 6.5 is jointly the lowest-scored non-`test_strategy` dimension on the board) but remains blocked by the STANDING USER CONSTRAINT and the `StoreNameLookup` cache-locking prerequisite - named explicitly here per Backlog Prioritization criterion 0 rather than silently demoted, and kept in the numbered list at Priority 3 rather than escalated to `user_decision`, since F-010 and F-012 remain fully actionable next-loop picks. F-012 (new this loop, Cosmetic severity) ranks last on both distance-to-target (`simplicity` is now the least-distant non-9.5 dimension on the board after this loop's F-003 fix) and severity.

## Deepening Candidates
1. **Candidate Module**: `LoadGameEntriesAsync`'s per-entry name/match resolution (Finding F-011). Carried forward unchanged from loop 7 - re-confirmed this loop that the source region (`PrimaryWidget.xaml.cs:455-679`) and its prerequisite (`StoreNameLookup`'s three unlocked caches, `StoreNameLookup.cs:29-34`) are both byte-identical to loop 7's own citation.
   - Source friction proven: Finding F-011 - a sequential-independent-effects loop shape on a hot path.
   - Why the current Interface is shallow or misplaced: not a shallow-Interface problem in the classic sense - the loop body is correctly owned by `LoadGameEntriesAsync` - but the current shape forces every caller to pay for strictly sequential I/O with no seam to batch or bound concurrency through.
   - What behavior should move behind the deeper Interface: per-entry name/match resolution, restructured to run with bounded concurrency once `StoreNameLookup`'s caches are made thread-safe.
   - Dependency category: `true-external`
   - Test surface after the change: none achievable without solving the same untestable-file problem every other `PrimaryWidget.xaml.cs` finding has; `StoreNameLookup`'s own logic, once thread-safe, could gain a dedicated concurrency test (matching `AsyncLazyCacheTests.cs`'s own 32-concurrent-caller pattern).
   - Smallest first step: add real locking (a `SemaphoreSlim` per cache, matching `AppliedArtworkStore`'s own established pattern) to `StoreNameLookup`'s `gogNameCache`/`epicNameCache`/`nameMatchCache` and `SteamGridDbClient.CapsuleParseNotes` BEFORE attempting any concurrency change to the calling loop.
   - What not to do: do not wrap the per-entry loop in `Task.WhenAll` before the caches are locked. Do not attempt the network-ordering half of this fix at all until a behavioural oracle exists, per the STANDING USER CONSTRAINT.

## Builder Notes

1. **Pattern: running the Residual Accounting Pass explicitly, dimension by dimension, on the multi-loop-stalled dimensions - rather than re-writing the same "still can't fix it" note - can surface a legitimate promotion the prior loops' framing never tested for.**
   - How to recognize: a dimension sits SAME for many loops with a note that re-confirms the same known gap each time, without ever asking "does the 9-anchor's own text actually require more than what's already true, and if not, why is this still sub-9.5?"
   - Smallest coding rule: when a dimension crosses 3 consecutive SAME loops (Stalled-Dimension Sweep trigger), read the dimension's own 9-anchor text fresh and ask the Residual Accounting Pass's own question 1 explicitly ("is the 9-anchor met?") before writing another "explicit clean" note. If met, the pass's own branches force a real decision: promote to 9.5-accepted (with a genuine Adversarial Pass, not a rubber stamp), promote to 10 (if no residual can be named), or keep sub-9.5 only with a named `structural_anchor_unmet` blocker.
   - Stack example: `domain_modeling` and `framework_idioms` had each been SAME for 7 straight loops with essentially the same "explicit clean" note reused each time; applying the pass this loop found the 9-anchor genuinely met on both, with `framework_idioms` having accumulated zero nameable residual across 8 cumulative loops of scrutiny - a fact none of loops 1-7 stated outright because none of them asked the question in those terms.

2. **Pattern: a helper-surfaced candidate finding that looks structurally similar to a *tracked* finding (same class, different file) is not automatically a duplicate - verify the actual code shape before merging or dismissing.**
   - How to recognize: `GamePlatformHelper`'s two-switch skew (F-012) could be mistaken for "the same kind of thing" as `StoreNameLookup`'s bypassed-JsonRead inconsistency (F-010) since both are "two call sites doing the same job slightly differently" - but F-010 is a live, if latent, correctness-adjacent inconsistency (bypassing a bug-preventing helper) while F-012 is a pure maintainability/DRY concern with a currently-100%-correct pair of switches and no bug-class precedent behind it. Filing them as one finding would have hidden F-012's genuinely lower severity.
   - Smallest coding rule: before merging two helper-surfaced candidates under one finding, re-derive each one's own Consequence (not just its Claim) independently - if the actual harm mechanisms differ (data-correctness-precedent vs. pure-maintainability), keep them separate findings even if the surface pattern rhymes.
   - Stack example: F-010 cites a documented historical bug class (`PrimaryWidget.xaml.cs:470-478`) that already shipped once; F-012 has no such precedent and is rated Cosmetic rather than Noticeable specifically because of that difference.

**Scorecard humility check** — two calls here are the ones most likely to be argued the other way: (1) `framework_idioms` moving straight from 8.0 to a full 10 in one loop, skipping the intermediate 9.5-with-accepted-residual step, is an unusually large jump; a stricter reader could argue an extra loop of scrutiny should have been spent before certifying a perfect score, even though the rubric's own text ("no source-backed residual can be named -> set score to 10") does not provide for an intermediate "not quite sure yet" score. This is recorded rather than smoothed over. (2) `state_management`/`concurrency` held at SAME rather than re-run through a fresh exhaustive completeness sweep this loop is a deliberate scope choice (this loop's investigation budget went to F-003 plus the two newly-run Residual Accounting Passes) - a stricter reading could ask whether skipping the sweep for one loop already represents under-scrutiny of the dimension with the multi-loop history of hiding one more instance per sweep. Recorded rather than asserted away.

## Final Judge Narrative
Place, not win, this loop. The headline result is structural bookkeeping finally catching up with itself: two dimensions (`domain_modeling`, `framework_idioms`) had each sat at the same score for seven consecutive loops with a "confirmed unchanged, no live harm" note that never actually applied the Score Anchors' own promotion rule - this loop ran the Residual Accounting Pass explicitly on both, with a real Adversarial Pass on `domain_modeling`'s residual (not a rubber stamp; the "just compute HasSteamGridDBMatch" shortcut was proposed, tested against a concrete counter-scenario, and correctly rejected) and an honest accounting of `framework_idioms`'s eight cumulative loops of zero remaining candidates. This is not score inflation - both moves are backed by fresh, source-cited reasoning that a stricter reviewer can check and, if they disagree with the `framework_idioms` jump to 10 specifically, argue against on its own terms (see humility check). The loop's own implementation work (F-003, collapsing the three-times-duplicated confirmation-dialog ceremony into one shared helper) is real, net-subtractive (-22 lines), verified by build + full test suite + an independent implementation review that returned `approved` on first pass, and the loop caught and fixed its own transient regression (an orphaned doc comment) before that review ever saw it - genuine, demonstrated self-correction rather than an assertion of care. `architecture_quality` and `test_strategy` both got the same Stalled-Dimension Sweep discipline applied but reached the opposite conclusion from `domain_modeling`/`framework_idioms`: their 9-anchors are genuinely NOT met (a large single-Module orchestrator; zero test coverage on a primary-flow reentrancy idiom), so they correctly stay capped rather than promoted, with `structural_anchor_unmet` blockers named for each rather than a vague "still working on it." `simplicity` is now clean of every leaf-module-duplication finding tracked since loop 1 - the only remaining candidate anywhere in the codebase is F-012, freshly surfaced and Cosmetic. Future work risks over-engineering only if F-010's eventual fix reaches for anything beyond the three-line `JsonRead` substitution already fully specified, or if F-011's eventual fix attempts to parallelize the network loop before adding real locks to `StoreNameLookup`'s caches - both explicitly warned against above.

## Loop 8 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (140 lines changed: 59 insertions, 81 deletions, net -22 lines): replaced the three near-identical `ContentDialog`-construction-plus-guard-and-run bodies in `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` with a single shared `ConfirmAndRunAsync(string title, string content, string primaryButtonText, string secondaryButtonText, Func<ContentDialogResult, bool> shouldRun, Func<ContentDialogResult, Task> action)` private helper (`PrimaryWidget.xaml.cs:740-800`); each of the three call sites now delegates to it with its own original title/content/button-text values and a `shouldRun`/`action` pair matching its original branching logic exactly (`FixLibraryButton_Click`'s `action` still branches on `result == ContentDialogResult.Secondary` to choose the `refixCustomised` argument to `FixLibraryAsync`, since it receives the dialog result). During implementation, an initial version of the extraction left the original `FixLibraryButton_Click` doc comment ("Handles fix library button click...") orphaned directly above the new `ConfirmAndRunAsync` method, now describing the wrong method - caught by re-diffing the change before the final build, fixed by removing the stale comment (the file's other two handlers, `RestoreChangesButton_Click`/`RevertDefaultsButton_Click`, never had doc comments, so `FixLibraryButton_Click` losing its own bare-summary comment matches the file's existing asymmetric convention rather than introducing a new one). Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change (both the initial and the corrected version). The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected, since `PrimaryWidget.xaml.cs` is not part of the test-linked `Services/**` surface. An independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on Finding F-003 and the diff only) returned verdict `approved` with all three checks (reality, honesty, regression) passed on the first pass. Finding F1 (stable_id F-003) is **resolved**: verified by a line-by-line diff confirming every call site's title/content/button-text literal is unchanged from the pre-fix code, the `Style`/`PrimaryButtonStyle`/`SecondaryButtonStyle`/`CloseButtonStyle` resource assignments are preserved (secondary style now correctly conditional on a non-empty secondary button text), the `XamlRoot` API-contract check is unchanged, and the `shouldRun(result) || !TryBeginLibraryOperation()` short-circuit ordering exactly preserves each handler's original `&&`/`||` semantics. This loop additionally re-verified Findings F-001/F-005 through F-009's guarded call sites unaffected (disjoint region of the file), independently re-confirmed F-010 and F-011 unchanged via direct reads, ran the mandatory doc-vs-code grep (three hits, all genuine, no doc rot), ran a fresh whole-tree `GameEntry` construction-site grep (still the sole site), and ran the Residual Accounting Pass + Adversarial Pass on `domain_modeling` (promoted to 9.5, accepted residual) and `framework_idioms` (promoted to 10, no residual). An independent Services/Models helper sweep surfaced a new Finding F-012 (`GamePlatformHelper`'s two independent switch statements), queued to the backlog rather than implemented this loop. No unintended scorecard regression: the change touches no network call, no ranking/selection logic, and no file outside the one named. Findings F-004, F-010, F-011 and F-012 are carried forward to the Improvement Backlog / Findings for future loops.

## Loop 8 Implementation Review
Verdict: **approved**. Reason: the three duplicated `ContentDialog`-construction-plus-guard-and-run bodies are now genuinely consolidated into one private `ConfirmAndRunAsync` helper (only one `new ContentDialog` remains in the file), the extraction is behavior-preserving call-site-by-call-site, introduces no new Seam/protocol, and the missing direct tests are not a new gap - `PrimaryWidget.xaml.cs` was already structurally excluded from the test project before this diff. Checks: reality passed, honesty passed, regression passed. Regressions: none. Conditions: none.
