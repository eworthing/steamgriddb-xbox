### Loop Counter
Loop 7 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

This loop ran an independent re-derivation from source (full re-read of `PrimaryWidget.xaml.cs`, plus fresh reads of every `Services/` and `Models/` file, `TESTING.md`, and the full test suite), plus two fresh-eyes helper sweeps briefed on the codebase's history but not its prior conclusions - one explicitly tasked with re-testing, method-by-method, whether the six-loop-running "stale async completion mutates shared picker UI state" defect class (F-001, F-005 through F-009) had a further instance anywhere in `PrimaryWidget.xaml.cs`, the other sweeping `Services/` and `Models/` cold. The first sweep, and my own independent trace of the same candidates, both converged on the same conclusion: `LoadGridSelectionAsync`'s and `ShowSearchPanelAsync`'s own unguarded post-await mutations are real but not exploitable (traced in full below), closing the three-loop-running completeness question with zero new instances found. This loop's Priority-1 finding (F-002, the four-times-duplicated panel slide animation, queued five loops) was implemented, verified by build + full test suite + independent implementation review. The second sweep independently re-confirmed F-010 (`StoreNameLookup` bypassing `JsonRead`) and surfaced a genuine new finding (F-011): `LoadGameEntriesAsync` resolves each unmatched game's name and SteamGridDB match sequentially rather than concurrently - a real structural-waste finding blocked from implementation this loop by the STANDING USER CONSTRAINT and a genuine new thread-safety risk a naive fix would introduce.

## Scorecard (1-10)

- Architecture quality: 7.5 | SAME | `Services/` modules (`ArtworkDownloader.cs`, `ArtworkRanker.cs`, `TileImage.cs`, `ArtworkSignature.cs`, `JsonRead.cs`, `AsyncLazyCache.cs`, `AppliedArtworkStore.cs`, `FixLog.cs`, `ManifestEntryIdentity.cs`, `EpicLibrary.cs`) re-read in full this loop, independently confirmed each still a deep, single-responsibility Module with a real Interface. `PrimaryWidget.xaml.cs` (now 2033 lines, down from 2058 pre-fix) still carries Finding F-003's three-times-duplicated dialog ceremony (open). Held at SAME rather than moved to reflect F-002's fix: this dimension's own 9-anchor language is about Module graph, Seams, and deletion-test pass-through structure, not shallow-module duplication within a single already-owned class - that concern is priced under simplicity below, matching loop 5/6's own explicit non-double-counting discipline for this exact pair of dimensions.
- State management and runtime ownership: 7.0 | UP | F-001/F-005 through F-009's own fixes independently re-verified this loop as still holding at their own call sites (fresh full-file read; all eight guarded call sites unchanged in substance). This loop's own investigation - an exhaustive, table-based enumeration of every async method in `PrimaryWidget.xaml.cs` with a mutation after its own await, cross-verified independently by a helper sweep using the identical enumeration discipline - found **zero** new exploitable instances, the first time in three loops (4→5→6 each found one) that a specifically-adversarial completeness sweep came back clean. The one candidate both traces surfaced (`LoadGridSelectionAsync:1315-1317`, an unguarded `GridLoadingRing.IsActive`/`Items.Clear()`/status-text mutation after `await ShowGridPanelAsync()`) was traced end-to-end and found NOT exploitable: `ShowGridPanelAsync`'s `Task.Delay` is a fixed 250ms in every call, which guarantees an earlier-clicked session always reaches this line before a later-clicked session does (chronological click order equals chronological arrival order at this line, since both sessions wait the identical duration from their own distinct start times), so the mutation is self-correcting rather than corruption-causing - full reasoning trace against current source, not assumption. Moved UP: this is genuine structural completion evidence (an exhaustive audit closing a 3-loop-running open question), not a code change, matching the same evidentiary weight the DOWN moves in loops 5/6 were given for the inverse finding.
- Domain modeling: 8.5 | SAME | `GameEntry.cs`, `GamePlatform.cs`, `GridImageItem.cs` and `ManifestEntryIdentity.cs` re-read in full this loop. `GameEntry.cs`'s `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` three-independently-settable-properties case (sole construction site `PrimaryWidget.xaml.cs:650-664`, unaffected by this loop's edits) remains the only known concern - freely settable, no smart constructor, no invariant enforced at construction; still no live harm. **Stalled-Dimension Sweep (loop 7, 6th consecutive SAME):** explicit clean - re-read `GameEntry.cs` in full, independently confirmed via helper; the concern's own severity (Cosmetic - no live harm, would require a factory-method rewrite of the sole construction site to fix, which SPT Q2 rejects as ceremony-for-the-fix-size) keeps it from ever winning Priority 1 against real Noticeable-or-worse candidates.
- Data flow and dependency design: 7.0 | SAME | `Services/*` re-confirmed zero `Windows.UI.Xaml` imports. Finding F-010 (`StoreNameLookup.GetGogGameNameAsync`/`GetEpicGameNameAsync` bypassing `JsonRead`) independently re-confirmed unchanged this loop by both a direct read of `StoreNameLookup.cs:54-86`/`175-201` (file untouched by this loop's F-002 edit) and a second independent helper sweep, which reached the identical finding cold. Not implemented this loop (F-002 outranked it on stall - five consecutive loops SAME vs. F-010's one); queued Priority 2 for loop 8.
- Framework / platform best practices: 8.0 | SAME | `SteamGridDbClient.cs:273-301`'s `BuildUrl` helper re-verified fixed and holding; its `DataContractJsonSerializer`/`Windows.Data.Json` split (`:136-142`, `:148-227`) re-read this loop and confirmed well-justified (`DataContractJsonSerializer` for typed responses, `JsonObject` for the one dynamic per-language-keyed document walk DCJS cannot express) rather than a smell. **Stalled-Dimension Sweep (loop 7, 6th consecutive SAME):** independently re-derived this loop rather than carried forward - F-002's now-fixed duplicated Storyboard/DoubleAnimation ceremony was NOT actually a framework_idioms concern on reflection: `DoubleAnimation`+`Storyboard` directly against a `TranslateTransform` IS the idiomatic UWP animation approach (there is no higher-level platform affordance being skipped), the defect was duplication (simplicity), not idiom violation - correcting prior loops' categorization of F-002 under this dimension. Swept `ArtworkDownloader.cs`, `ArtworkFiles.cs`, `TileImage.cs`, `EpicLibrary.cs`, `AsyncLazyCache.cs` fresh this loop: consistent async/await usage, correct `StorageFile`/`StorageFolder` disposal patterns, no fresh framework_idioms candidate found.
- Concurrency and runtime safety: 6.5 | UP | Same completed-audit evidence as state_management: eight guarded call sites (F-001/F-005 through F-009) re-verified holding; an exhaustive cross-verified sweep of every post-await mutation in `PrimaryWidget.xaml.cs` found zero new reentrancy hazards, the first clean result in three loops. Moved UP, not to the full state_management magnitude: a genuine NEW concurrency-dimension finding surfaced this loop (F-011) - `LoadGameEntriesAsync`'s per-manifest-entry loop (`PrimaryWidget.xaml.cs:455-679`) awaits each entry's network calls (SteamGridDB, then GOG/Epic/Ubisoft, then a SteamGridDB name-search) in strict sequence with no shared cross-entry state forcing that order, a textbook D2 sequential-independent-effects shape per the efficiency lens, on the hot path that runs every widget open. Not previously priced into this dimension (freshly surfaced, not a regression). Queued, not implemented - blocked by the STANDING USER CONSTRAINT (no behavioural oracle for per-game network-call ordering changes) and by a real thread-safety risk: `StoreNameLookup`'s `gogNameCache`/`epicNameCache`/`nameMatchCache` and `SteamGridDbClient.CapsuleParseNotes` are currently unlocked `Dictionary`s/`List`, correct only because today's caller is single-threaded per load - naive parallelization would introduce a genuine new data race.
- Code simplicity and clarity: 7.5 | UP | Finding F-002 (four near-identical `DoubleAnimation`/`Storyboard` bodies, `ShowGridPanelAsync`/`HideGridPanelAsync`/`ShowSearchPanelAsync`/`HideSearchPanelAsync`) fixed this loop: all four call sites now delegate to one shared private `SlidePanelAsync(TranslateTransform, from, to, durationMs, EasingMode)` helper (`PrimaryWidget.xaml.cs:1569-1600`), each collapsing from a ~20-line hand-built `Storyboard` to a one-line call; net -26 lines in the file (92 lines changed: 33 insertions, 59 deletions). Verified behavior-preserving: every call site carries its original literal From/To/Duration/EasingMode values, and the loop-6 session guards in `HideGridPanelAsync`/`HideSearchPanelAsync` are textually unchanged, just repositioned after the new call. Stall broken after five consecutive SAME loops (2-6). Finding F-003 (three-times-duplicated `ContentDialog`/confirm-guard-run ceremony, `PrimaryWidget.xaml.cs:743-859`) remains open and is the sole remaining simplicity candidate - now the correctly-highest-stall item, Priority 1 for loop 8.
- Test strategy and regression resistance: 6.5 | SAME | `PrimaryWidget.xaml.cs` remains untestable outside an app container - re-confirmed via both `.csproj` target frameworks (`UAP` vs `net8.0-windows10.0.19041.0` desktop projection), matching `TESTING.md:49-56`'s own documented reasoning, re-read in full this loop and still accurate; this loop's own F-002 fix (private-helper extraction inside that same untestable class) verified instead by full build + full test suite (138/138 unchanged) + independent implementation review, matching the established precedent for every fix to this file since loop 1. **Stalled-Dimension Sweep (loop 7, 6th consecutive SAME, lowest-scored dimension on the board):** `TileImageTests.cs` re-checked (Finding F-004, still no case at the exact `alpha == 64` or `transparentCorners == 2` boundary, `TileImage.cs:250`/`:263` re-read unchanged) - the only candidate on this dimension, Cosmetic severity and off-primary-flow, does not win Priority 1: the dimension's ceiling is set by `PrimaryWidget.xaml.cs`'s structural untestability, not by what `TileImage.cs`'s test file contains.
- Overall implementation credibility: 8.5 | UP | Second consecutive loop in which a targeted, adversarially-framed re-test sweep (this time paired with an independent same-loop cross-verification, not just a generic re-read) is the methodology that produced the loop's headline result - this time a genuine zero-new-instances finding rather than another instance, which is itself evidence the methodology is honest rather than just good at finding bugs. Structural proof for the UP move: this loop's own F-002 fix, independently reviewed by a fresh-eyes subagent briefed cold on the diff and the targeted finding only, returning `approved` with all three checks (reality, honesty, regression) passed; plus my own independent re-derivation of the fingerprint-hash algorithm from the canonical spec in `output-format-state-schemas.md`, which reproduced three of loop 6's own stored hashes byte-for-byte on unchanged findings (F-004, F-010) before I had seen those exact values - a same-loop cross-check the registry's own audit trail did not have before.

## Authority Map
Re-emitted this loop: significant state_management/concurrency movement this loop is directly tied to authority/ownership evidence (the completed reentrancy audit), so this substantiates the UP moves above even though F-002 (this loop's Priority 1) is not itself an authority finding.

- Concern: **Grid picker panel display contents (`GridImagesView.Items` / `GridPanelStatus` / `GridLoadingRing` / panel visibility during population and close) and search results panel (`SearchResultsListView.Items` / `SearchPanelStatus` / `SearchLoadingRing`)**
  - Owner: `PrimaryWidget` (`LoadGridSelectionAsync` / `PopulateGridSelectionPanelAsync` / `DownloadAndReplaceImageAsync` / `HideGridPanelAsync` / `PerformGameSearchAsync` / `ShowSearchPanelAsync` / `HideSearchPanelAsync`)
  - Allowed writers: `LoadGridSelectionAsync` (guarded since loop 2/F-005 at the population branch; its own pre-population loading-ring/clear/status lines at `:1315-1317` are unguarded but traced this loop as non-exploitable), `PopulateGridSelectionPanelAsync` (guarded since loop 4/F-007), `DownloadAndReplaceImageAsync` (guarded since loop 5/F-008), `HideGridPanelAsync` (guarded since loop 6/F-009, now delegates its animation to the shared `SlidePanelAsync` helper as of this loop), `PerformGameSearchAsync` (guarded since loop 3/F-006), `ShowSearchPanelAsync` (its own post-await focus-only mutations at `:1824-1834` traced this loop as non-exploitable), `HideSearchPanelAsync` (guarded since loop 6/F-009, now delegates to `SlidePanelAsync`)
  - Readers: user (visually), `GridImage_Click` (via `SessionId` comparison), `SearchResult_Click`
  - Persistence seam: none
  - Async mutation entry points: `LoadGridSelectionAsync`, `PopulateGridSelectionPanelAsync`, `DownloadAndReplaceImageAsync`, `HideGridPanelAsync`, `PerformGameSearchAsync`, `ShowSearchPanelAsync`, `HideSearchPanelAsync`
  - Verdict: **Single and clear** - this loop closes out a three-loop-running open completeness question (loops 4, 5 and 6 each found one further unguarded instance of this exact hazard class). This loop's own exhaustive, cross-verified enumeration of every post-await mutation in the file found zero further instances; the two unguarded-but-safe candidates found are documented above with their own non-exploitability trace rather than silently omitted.

- Concern: **Library load's per-entry name/match resolution (`LoadGameEntriesAsync`'s sequential network calls)**
  - Owner: `PrimaryWidget.LoadGameEntriesAsync`, delegating to `StoreNameLookup` / `SteamGridDbClient`
  - Allowed writers: `LoadGameEntriesAsync`'s own `foreach` loop (`tmpGameList`, a loop-local accumulator - safe); `StoreNameLookup`'s three caches (`gogNameCache`/`epicNameCache`/`nameMatchCache`) and `SteamGridDbClient.CapsuleParseNotes`, all unlocked
  - Readers: same methods on the next lookup for the same key
  - Persistence seam: none
  - Async mutation entry points: `LoadGameEntriesAsync`'s per-entry loop (called once per `PrimaryWidget_Loaded` / `RefreshButton_Click`, itself gated by `isLibraryOperationRunning` so only one load runs at a time)
  - Verdict: **Single and clear today** - the unlocked caches are safe under the current single-threaded-per-load design, but that safety is a load-bearing assumption, not an enforced invariant. See Finding F-011: this is also why parallelizing the sequential-await shape identified this loop is not a safe drop-in fix - it would need real locking added to these caches first.

## Strengths That Matter
- This loop's own verification methodology held up under an independent same-loop cross-check for the first time: a helper sweep briefed to independently enumerate and trace every post-await mutation in `PrimaryWidget.xaml.cs` reached the identical UNGUARDED-BUT-SAFE conclusion on the same two candidates (`LoadGridSelectionAsync:1315-1317`, `ShowSearchPanelAsync`'s focus-only tail) via the same reasoning, without having seen my own trace first - genuine independent convergence, not one analysis rubber-stamping the other.
- `ArtworkDownloader.cs`'s `DownloadBestTileFillingImageAsync` / `FindOfficialLookalikeAsync` / `PassesColourAndLayoutGate` / `ChosenAlreadyMatchesOfficialArt` split (`ArtworkDownloader.cs:71-220`, re-read in full this loop) remains a genuinely deep Module: the five-step selection-and-veto pipeline is documented with the specific graded incident (`officialArtworkFloor`'s doc comment cites "Mad Max at 0.51") that calibrated each threshold, and both gate predicates are extracted as pure, independently mutation-tested functions (`ArtworkDownloaderTests.cs` asserts the exact `>=` vs `>` boundary at each threshold).
- `AsyncLazyCacheTests.cs`'s 32-concurrent-caller test (`Loads_once_however_many_callers_arrive_together`) is genuine concurrency verification, not a timing-hack sleep - it proves the single-load guarantee `AsyncLazyCache<T>` makes to `StoreNameLookup`, `EpicLibrary` and `AppliedArtworkStore` under real `Task.Run` parallelism.

## Findings

### Finding #1 (stable_id F-002): Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to the panel's slide timing or easing (or a bug in it) had to be made and verified in four places instead of one, and the four copies had already begun to drift (200ms hide vs 250ms show, independently re-derived each time).

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` (pre-fix lines `1571-1592`, `1597-1640`, `1779-1847`, `1852-1891`) each hand-built a `DoubleAnimation` + `Storyboard` against a `TranslateTransform`, set From/To/Duration/EasingFunction, called `storyboard.Begin()`, then awaited `Task.Delay` matching the duration - four near-identical bodies in the single largest, most-churned file in the codebase.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1571-1592` (pre-fix, `ShowGridPanelAsync`), `:1597-1640` (pre-fix, `HideGridPanelAsync`), `:1779-1847` (pre-fix, `ShowSearchPanelAsync`), `:1852-1891` (pre-fix, `HideSearchPanelAsync`)

**Architectural test failed** — Shallow module (each Show/Hide method's Interface ≈ its Implementation; no reuse across the four near-identical bodies)

**Dependency category** — n/a

**Leverage impact** — One call site to read/change instead of four; callers learn less, not more.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`; no other file's behavior changes.

**Metric signal, if any** — none

**Why this weakens submission** — Four near-identical bodies in `PrimaryWidget.xaml.cs` is exactly the leaf-module duplication the Simplicity dimension's 9-anchor requires be swept for; queued five full loops before being fixed this loop.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(TranslateTransform transform, double from, double to, int durationMs, EasingMode mode)` helper that builds the `DoubleAnimation`/`Storyboard` once and awaits `Task.Delay(durationMs)`; each of the four call sites becomes a one-line call. No new Seam, no new file - the four bodies collapse into the one Module (`PrimaryWidget`) that already owns them.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml` (markup unchanged), `SteamGridDB.Xbox/Services/**`.

**Status this loop: implemented — see Loop 7 Result below.**

### Finding #2 (stable_id F-011): LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — A user whose library has many GOG/Epic games without a direct SteamGridDB match pays the full network latency of the GOG, Epic (or Ubisoft/community-database) endpoint plus the SteamGridDB name-search endpoint for every one of those games, one after another, before the library list finishes loading - which happens on every widget open, not once.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (`PrimaryWidget.xaml.cs:455-679`) awaits, in strict sequence for each manifest entry: `sgdbClient.GetGameByPlatformIdAsync` (`:581`), then when unmatched one of `StoreNameLookup.GetOrFetchGogNameAsync` (`:603`) / `GetOrFetchEpicNameAsync` (`:612`) / `GetUbisoftGameNameAsync` (`:621`), then `StoreNameLookup.FindGameByNameAsync` (`:641`) - each entry's network calls fully complete before the next entry's loop iteration starts any of its own. None of these calls read or write state another entry's iteration also touches, so the awaits are independent across entries and the loop body is a textbook sequential-independent-effects shape (efficiency lens, D2).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:455-679` (per-manifest-entry `foreach` loop), `:581` (`sgdbClient.GetGameByPlatformIdAsync`), `:603` (`StoreNameLookup.GetOrFetchGogNameAsync`), `:612` (`StoreNameLookup.GetOrFetchEpicNameAsync`), `:641` (`StoreNameLookup.FindGameByNameAsync`)

**Architectural test failed** — n/a - different category (efficiency/D2, not a Seam/Module-boundary finding)

**Dependency category** — n/a (not a Coupling & Leakage finding)

**Leverage impact** — None currently - no seam exists to batch or parallelize through; a fix would need one.

**Locality impact** — Contained to `LoadGameEntriesAsync`'s own per-entry loop body and, if fixed, `StoreNameLookup`'s three dictionaries' thread-safety; no other file's behavior changes.

**Metric signal, if any** — none

**Why this weakens submission** — This is a hot path (the library reloads on every widget open, per `StoreNameLookup`'s own class doc comment) doing per-item network I/O one item at a time where nothing in the current design requires that ordering - the structural cost scales linearly with the count of unmatched games in a user's library, and nothing amortizes it.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Not implemented this loop - blocked by the STANDING USER CONSTRAINT (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft). A safe fix would need bounded concurrency (never unbounded fan-out), and - because `StoreNameLookup`'s `gogNameCache`/`epicNameCache`/`nameMatchCache` dictionaries and `SteamGridDbClient.CapsuleParseNotes` list are currently unlocked, correct only because today's caller is single-threaded per load - would need real thread-safety added to those shared caches first, which is itself a non-trivial, behaviour-affecting change this loop declines to attempt without a broader design pass.

**Blast radius** — Change: none this loop (not attempted). Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`, `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs` (both not attempted this loop).

### Finding #3 (stable_id F-010): StoreNameLookup's GOG and Epic name-fetch methods bypass the JsonRead helper that exists to prevent a documented JSON null-vs-missing bug

**Why it matters** — The codebase already shipped a bug once (documented in `PrimaryWidget.xaml.cs:470-478` and `JsonRead.cs:13-16`) where a JSON member that was present-but-null was mishandled by raw `Windows.Data.Json` calls; `JsonRead.cs` exists specifically to make that class of bug impossible. Two of `StoreNameLookup`'s three network-backed name-lookup methods still use the pattern that caused it.

**What is wrong** — `GetGogGameNameAsync` (`StoreNameLookup.cs:67-74`) reads `gameData.ContainsKey("_embedded") && gameData.GetNamedObject("_embedded").ContainsKey("product")`, then `gameData.GetNamedObject("_embedded").GetNamedObject("product")` (calling `GetNamedObject("_embedded")` twice), then `product.GetNamedString("title")` - all raw `Windows.Data.Json` calls. `GetEpicGameNameAsync` (`:188-190`) does the same for `gameData.GetNamedString("title")`. `GetNamedObject`/`GetNamedString` throw `InvalidOperationException` when the member is present but JSON `null`, which `ContainsKey` cannot distinguish from a normal value - the exact ambiguity `JsonRead.Object`/`JsonRead.String` were written to resolve, and which `SteamGridDbClient.cs:155-181` and `EpicLibrary.cs` both correctly use instead. The outer broad `catch (Exception ex)` in both methods means a null-title response would not crash - it would silently degrade to "name not found" - so there is no live crash risk today; the harm is a maintained inconsistency next to the exact helper built to remove it.

**Evidence** — `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:67-74` (`GetGogGameNameAsync`), `:188-190` (`GetEpicGameNameAsync`), `SteamGridDB.Xbox/Services/JsonRead.cs:1-17` (the helper and its own docstring naming the bug class), `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:470-478` (the manifest-parsing comment citing the same bug class having shipped once already), `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs:155-181` (the established correct pattern)

**Architectural test failed** — n/a - a Reuse/consistency finding, not a Seam/Module-boundary finding

**Dependency category** — n/a

**Leverage impact** — None directly - `JsonRead` already exists and is already used by two other call sites; this brings the remaining two into line rather than adding anything.

**Locality impact** — Contained to `StoreNameLookup.cs`'s two methods; no other file's behavior changes; `JsonRead.cs` itself is untouched.

**Metric signal, if any** — none

**Why this weakens submission** — Credibility/consistency deduction, not a live defect: the established, documented, purpose-built helper for this exact class of JSON-parsing bug exists in the same file's own dependency graph and is used by two of the three sibling call sites plus `SteamGridDbClient.cs` and `EpicLibrary.cs`, yet two methods opt out with no stated reason.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Replace the raw `ContainsKey`/`GetNamedObject`/`GetNamedString` chains in both methods with `JsonRead.Object(gameData, "_embedded")` → `JsonRead.Object(embedded, "product")` → `JsonRead.String(product, "title")` (GOG) and `JsonRead.String(gameData, "title")` (Epic), matching `SteamGridDbClient.cs`'s established pattern. Behavior-preserving: for every currently-well-formed response the observable result is identical; for a hypothetical null-valued "title", both the old (exception caught by the outer `catch`) and new (`JsonRead` returns `null` directly) paths converge on the same observable `null` return to the caller - no network call, call count, ordering, payload, or error-handling behavior visible to any caller changes.

**Blast radius** — Change: `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`. Avoid: `SteamGridDB.Xbox/Services/JsonRead.cs`, `SteamGridDB.Xbox/Services/SteamGridDB/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

### Finding #4 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations has to be kept in sync by hand - the same style-resource assignment, the same `XamlRoot` API-contract check, the same `TryBeginLibraryOperation`/`EndLibraryOperation` guard wiring - and nothing enforces that a future fourth operation (or an edit to one of the three) follows the same shape.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (current lines `743-787`, `789-823`, `825-859` - shifted by one line this loop's own using-directive addition; unaffected by this loop's F-002 edit, which lands far below at line 1568+) each build a `ContentDialog` with the same four style-resource lookups and the same `Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent` `XamlRoot` check, call `ShowAsync`, branch on the result, and wrap the actual operation in the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` pattern.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:743-787`, `:789-823`, `:825-859`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action instead of re-deriving the whole confirm-guard-run shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as Finding F-002 (now fixed), in the same file; this cluster alone accounts for roughly 105 of `PrimaryWidget.xaml.cs`'s remaining lines being ceremony repeated 3x rather than owned once. Six loops queued without action, correctly outranked each time by a higher-severity or higher-stall finding.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `ConfirmAndRunAsync(string title, string content, string primaryText, string secondaryText, Func<ContentDialogResult, bool> shouldRun, Func<Task> action)` (or the smallest signature that covers the 2-button and 3-button cases) that owns dialog construction, the `XamlRoot` check, and the `TryBeginLibraryOperation`/`EndLibraryOperation` wrapping; each handler becomes a short call naming its own title/content/action.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #5 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation at either threshold (`<` to `<=`) would ship silently: the corner-transparency gate that keeps case-mockup art off tiles would become off-by-one permissive or strict with no test failing.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) treats a pixel transparent when `alpha < 64` (`:250`) and rejects the image when 2 or more of its 4 corners are transparent (`transparentCorners < 2`, `:263`). `TileImageTests` exercises fully-opaque and fully-transparent corners but not alpha exactly at 64 or a candidate with exactly 2 transparent corners. Re-read this loop (`TileImage.cs`/`TileImageTests.cs` unaffected by this loop's edits); gap re-confirmed unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — A minor, off-primary-flow gap (per the Test strategy dimension's own anchor language, an off-path helper boundary is Cosmetic on its own) but worth naming before it is mistaken for full coverage.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases: a corner at exactly alpha 63/64, and an image with exactly 2 (not 0, not 4) transparent corners, asserting the documented boundary.

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs` (test-only addition, no production change).

## Simplification Check
- Structurally necessary: Finding F-002's `SlidePanelAsync` extraction resolves a real, evidenced leaf-module duplication (four near-identical `DoubleAnimation`/`Storyboard` bodies, one already-observed drift between the Show/Hide pairs) - passes the Shallow module test (Interface ≈ Implementation four times over; now one Interface, one Implementation, four thin callers).
- New seam justified: No. Considered and rejected: introducing an `IAnimator`/`IPanelController` protocol for `SlidePanelAsync` fails the Unified Seam Policy's two-adapter rule immediately (no second Adapter exists or is proposed, private in-process UI glue with one production caller-family), and no single-Adapter policy/failure/platform-isolation justification applies either. A plain private helper method is the correct, and only defensible, shape.
- Helpful simplification: F-002's fix is net -26 lines in `PrimaryWidget.xaml.cs` (92 lines changed: 33 insertions, 59 deletions) - genuinely subtractive, not ceremony-for-ceremony's-sake.
- Should NOT be done: Do not introduce an `IAnimator`/`IPanelController` protocol or a separate animation-coordinator class for `SlidePanelAsync` (see above; re-affirms loop 6's own Deepening Candidate guidance for this exact extraction). Also re-confirmed this loop: do not attempt F-011's fix (parallelize `LoadGameEntriesAsync`'s per-entry network calls) without first adding real locking to `StoreNameLookup`'s three caches - a naive `Task.WhenAll` wrap over the current unlocked `Dictionary`s would trade a latency problem for a data-race problem, and the STANDING USER CONSTRAINT blocks the network-ordering half of the change regardless until a behavioural oracle exists.
- Tests after fix: None added or deleted - `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface and cannot be unit-tested in the current infra, matching every prior fix to this file since loop 1. Verification is: full build (exit 0) + full test suite (138/138 unchanged) both re-run after the change, an independent fresh-eyes implementation review (separate subagent, read-only, verdict `approved`, all three checks passed), plus a line-by-line diff confirming every call site's From/To/Duration/EasingMode literal is unchanged from the pre-fix code and the loop-6 session guards in `HideGridPanelAsync`/`HideSearchPanelAsync` are textually unchanged, only repositioned after the new helper call.

## Improvement Backlog
1. **Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003).**
   - why it matters: removes ~105 more lines of repeated ceremony from the same file; the sole remaining simplicity candidate, and now the highest-stall item on the board (six consecutive loops SAME on the dimension it targets, the same stall level F-002 itself just broke).
   - score impact: `simplicity +0.5`
   - simplification
   - helpful

2. **Route StoreNameLookup's GOG and Epic name-fetch methods through the existing JsonRead helper (Finding F-010).**
   - why it matters: removes a documented-bug-class inconsistency next to the exact helper built to prevent it; small, mechanical, behavior-preserving per the five properties named in the finding.
   - score impact: `data_flow +0.5`
   - simplification
   - helpful

3. **Add bounded concurrency to LoadGameEntriesAsync's per-entry name/match resolution, after first adding real locking to StoreNameLookup's caches (Finding F-011).**
   - why it matters: removes a real, linearly-scaling latency cost on the primary library-load hot path - but ranked last and not actionable yet: blocked by the STANDING USER CONSTRAINT (no behavioural oracle for per-game network-call ordering/concurrency changes) and by the unlocked-cache thread-safety prerequisite named in this finding's own remedy.
   - score impact: `concurrency +0.5`
   - structural
   - helpful

**Priority-1 accounting**: F-003 is Priority 1 for the next loop on Stall (six consecutive loops SAME on `simplicity`, the same criterion that made F-002 Priority 1 this loop) and criterion-4 subtractive-fix preference. F-010 (data_flow, found loop 6, one loop's stall) is Priority 2 on lower stall despite comparable severity. F-011 (concurrency-mapped efficiency finding, sequential per-game network calls in `LoadGameEntriesAsync`) ranks on merit above both by distance-to-target (`concurrency` is the lowest-scored non-test dimension on the board) but is not actionable this loop: blocked by the STANDING USER CONSTRAINT - parallelizing per-game GOG/Epic/SteamGridDB calls changes per-game network-call ordering/concurrency with no behavioral oracle - and by a genuine new correctness risk naive parallelization would introduce (`StoreNameLookup`'s currently-unlocked `gogNameCache`/`epicNameCache`/`nameMatchCache` would race under concurrent writes). Named explicitly per the Backlog Prioritization Pass's actionability criterion (criterion 0); queued at Priority 3 rather than escalated to `user_decision`, since F-003 and F-010 remain fully actionable next-loop picks.

## Deepening Candidates
1. **Candidate Module**: `PrimaryWidget`'s confirmation-dialog ceremony (the three bodies in Finding F-003).
   - Source friction proven: Finding F-003 - three near-identical `ContentDialog`-construction-plus-guard-and-run bodies, re-confirmed unchanged this loop (line numbers refreshed for this loop's own +1-line using-directive insertion, which sits above all three).
   - Why the current Interface is shallow or misplaced: there is no Interface at all - each Click handler inlines its own dialog construction, `XamlRoot` check, and `TryBeginLibraryOperation`/`EndLibraryOperation` wrapping; Interface ≈ Implementation three times over (Shallow module test), the identical shape F-002 had before this loop's fix.
   - What behavior should move behind the deeper Interface: `ContentDialog` construction (title/content/button text/style resources), the `XamlRoot` API-contract check, `ShowAsync`, and the `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` guard wrapping, parameterized by title/content/button text/the action to run.
   - Dependency category: `in-process`
   - Test surface after the change: none - `PrimaryWidget.xaml.cs` is UWP-page-bound and untestable in the current infra either before or after this extraction; verified by build + manual trace, same as F-002's own fix this loop.
   - Smallest first step: extract a private `ConfirmAndRunAsync(string title, string content, string primaryText, string secondaryText, Func<ContentDialogResult, bool> shouldRun, Func<Task> action)` and replace all three call sites.
   - What not to do: do not introduce a `DialogService`/`IConfirmationCoordinator` protocol - this is private in-process UI glue with one production caller-family; the Unified Seam Policy's two-adapter rule fails immediately, and single-Adapter policy/failure/platform-isolation justification doesn't apply either. Inline extraction only, mirroring F-002's own fix this loop.

2. **Candidate Module**: `LoadGameEntriesAsync`'s per-entry name/match resolution (Finding F-011).
   - Source friction proven: Finding F-011 - a sequential-independent-effects loop shape on a hot path, surfaced fresh this loop by an independent helper sweep applying the always-included efficiency lens's D2 detector.
   - Why the current Interface is shallow or misplaced: not a shallow-Interface problem in the classic sense - the loop body is correctly owned by `LoadGameEntriesAsync` - but the current shape forces every caller of the library-load path to pay for strictly sequential I/O with no seam to batch or bound concurrency through.
   - What behavior should move behind the deeper Interface: per-entry name/match resolution (the GOG/Epic/Ubisoft lookup plus the SteamGridDB name-search fallback), restructured to run with bounded concurrency once `StoreNameLookup`'s caches are made thread-safe.
   - Dependency category: `true-external`
   - Test surface after the change: none achievable without first solving the same untestable-file problem every other `PrimaryWidget.xaml.cs` finding has; the underlying `StoreNameLookup` logic itself, once thread-safe, could gain a dedicated concurrency test (matching `AsyncLazyCacheTests.cs`'s own 32-concurrent-caller pattern) even though the calling loop cannot be tested.
   - Smallest first step: add real locking (a `SemaphoreSlim` per cache, matching `AppliedArtworkStore`'s own established pattern) to `StoreNameLookup`'s `gogNameCache`/`epicNameCache`/`nameMatchCache` and `SteamGridDbClient.CapsuleParseNotes` BEFORE attempting any concurrency change to the calling loop - this is a prerequisite, not the fix itself, and is blast-radius-contained to `StoreNameLookup.cs`/`SteamGridDbClient.cs` with no behavior change to any network call.
   - What not to do: do not wrap the per-entry loop in `Task.WhenAll` (or any concurrent form) before the caches are locked - this would trade a latency problem for a silent data-race problem. Do not attempt the network-ordering half of this fix at all until a behavioural oracle exists, per the STANDING USER CONSTRAINT.

## Builder Notes

1. **Pattern: an adversarially-framed completeness sweep converging with an independent second sweep on the SAME non-finding is itself the strongest evidence a defect class is actually closed - stronger than either sweep alone, and stronger than three prior loops each finding one more instance.**
   - How to recognize: after a recurring defect class has been fixed several times running, the natural next question is "is that really the last one?" A single re-sweep answering "yes" is weak evidence - it could just be a less thorough look. Two independently-briefed sweeps reaching the same conclusion via the same reasoning, on the same specific candidate sites, is much stronger: two different lookers had to independently fail to find a counterexample.
   - Smallest coding rule: when closing out a recurring-defect audit, don't stop at one sweep even if it comes back clean - budget for two independent passes on completeness questions specifically (not on every review question - this is expensive and should be reserved for a defect class with a multi-loop track record of hiding).
   - Stack example: this loop's own trace of `LoadGridSelectionAsync:1315-1317` and the helper's trace of the same lines both independently constructed the same click-timing argument (`ShowGridPanelAsync`'s fixed 250ms `Task.Delay` guarantees earlier-clicked sessions always reach that line first) before either saw the other's reasoning - a coincidence of correct analysis, not one analysis copying the other.

2. **Pattern: not every duplicated code block that a Simplify Pressure Test wants collapsed belongs to the scorecard dimension a prior loop filed it under - re-derive the dimension mapping fresh each time a finding is scored, not just the score.**
   - How to recognize: a finding gets filed under a dimension once (often the dimension that happened to be lowest/most-stalled at the time) and then every subsequent loop's scorecard note inherits that categorization without re-testing whether it actually fits the dimension's own anchor language.
   - Smallest coding rule: before writing "Finding FN, priced under dimension D," read D's own 9-anchor language and ask whether the finding's underlying defect actually matches it. A shallow-module duplication of framework-idiomatic code (used correctly, just four times) is a simplicity finding, not a framework_idioms finding - the framework is being used exactly right, four times.
   - Stack example: Finding F-002 had been implicitly counted toward `framework_idioms`'s Stalled-Dimension Sweep for several loops; re-reading `framework_idioms`'s actual 9-anchor language this loop ("Stack idioms used naturally... Framework affordances used where they fit") shows F-002 never actually violated it - correcting the categorization rather than carrying it forward unexamined.

3. **Pattern: a structural-waste finding (slow, not wrong) can still be correctly blocked by a behavioral-preservation constraint even when the finding itself never touches the constrained surface directly - the FIX, not the finding, is what has to clear the bar.**
   - How to recognize: it's tempting to read a network-call-ordering constraint as only applying to findings that are themselves about network calls. But a finding whose defect is purely structural (a sequential loop shape) can still have its only honest fix be a change that crosses the constrained surface.
   - Smallest coding rule: when a finding's remedy would change call ordering, concurrency, or timing against a constrained third-party surface, name that explicitly as the blocker in the finding's own minimal correction path - even if the finding's "what is wrong" never mentions the constraint by name - so a future loop implementing it doesn't have to re-discover the blocker from scratch.
   - Stack example: Finding F-011's "what is wrong" is purely about loop shape (sequential awaits with no ordering dependency); its minimal correction path is the section that names the STANDING USER CONSTRAINT and the unlocked-cache thread-safety prerequisite - both facts a future implementing loop needs before it can safely act.

**Scorecard humility check** — three claims here are the ones most likely to be argued the other way: (1) The claim that `LoadGridSelectionAsync:1315-1317` and `ShowSearchPanelAsync`'s focus-only tail are genuinely safe rather than merely unexploited-so-far rests on an argument about `ShowGridPanelAsync`'s `Task.Delay` being a reliably fixed 250ms in wall-clock terms; `Task.Delay`'s actual OS-timer-resolution jitter is not zero, and a sufficiently adversarial timing scenario (heavy UI-thread congestion skewing the two delays' relative completion order) is not rigorously ruled out, only argued to be very unlikely given the observed code shape - a reviewer could reasonably want this demoted from "safe" to "very low probability, not fully closed." (2) Moving `concurrency` UP by a full point (5.5→6.5) while a brand-new Noticeable finding (F-011) landed in the same dimension the same loop is an unusual combination; a stricter reading could argue the new finding should have capped the UP move at +0.5 rather than allowing the audit-completion credit to net out to a full point after F-011's modest pull-down - this is recorded explicitly rather than smoothed over. (3) Recategorizing F-002 out of `framework_idioms`'s Stalled-Dimension Sweep this loop (see Builder Notes item 2) is a correction of prior loops' categorization, not new evidence about current source - a stricter reading of Anchor-to-source discipline could ask whether re-categorizing a finding's dimension counts as "re-deriving from source" or as second-guessing a settled editorial call; this loop's position is that the dimension anchor language IS source (the rubric), so re-reading it counts, but the call is close enough to flag.

## Final Judge Narrative
Place, not win, this loop. This loop's headline result is a negative one, and a rare one for this codebase: an exhaustive, cross-verified sweep for a sixth instance of the "stale async completion mutates shared picker UI state" defect class found none, closing a completeness question that loops 4, 5 and 6 each answered by finding one more instance. Two unguarded candidates were found and traced to genuine safety rather than silently passed over - the click-timing argument for `LoadGridSelectionAsync`'s own unguarded lines is recorded in full, not asserted. State_management and concurrency both moved UP on that structural completion evidence, with concurrency's move tempered by a genuine new finding (F-011, sequential per-game network calls on the library-load hot path) that the same fresh-eyes sweep surfaced independently. This loop's own implementation work (F-002, collapsing the four-times-duplicated panel-slide animation into one shared helper) is real, net-subtractive (-26 lines), and verified by build + full test suite + an independent implementation review that returned `approved` on first pass. Simplicity's five-loop stall broke as a direct result. Runtime ownership is more trustworthy this loop by real evidence, not by the passage of time; concurrency is more trustworthy on the reentrancy axis specifically, but F-011 is an honest reminder that "trustworthy" does not mean "exhaustively efficient" - a hot path can be race-free and still slow. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; this loop's only regression evidence for the F-002 fix is full build + full suite + independent review + a line-by-line literal-value diff. Future work risks over-engineering only if F-003's eventual extraction (next loop's Priority 1, the same shape as F-002) reaches for a coordinator/service abstraction instead of a private helper method, or if F-011's eventual fix attempts to parallelize the network loop before adding real locks to `StoreNameLookup`'s caches - both explicitly warned against in this loop's own Deepening Candidates.

## Loop 7 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (92 lines changed: 33 insertions, 59 deletions, net -26 lines): replaced the four near-identical `DoubleAnimation`/`Storyboard` construction bodies in `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` with a single shared `SlidePanelAsync(TranslateTransform, from, to, durationMs, EasingMode)` private helper (`PrimaryWidget.xaml.cs:1569-1600`); each of the four call sites now delegates to it with its own original From/To/Duration/Easing arguments. The five prior session-guard fixes (F-001, F-005 through F-009) are untouched in substance - `HideGridPanelAsync`'s and `HideSearchPanelAsync`'s session captures and rechecks sit exactly where they did before, just after a call to the new helper instead of after inline animation code. Added one using directive (`Windows.UI.Xaml.Media`, for `TranslateTransform`) at the top of the file. Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected, since `PrimaryWidget.xaml.cs` is not part of the test-linked `Services/**` surface. An independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on Finding F-002 and the diff only) returned verdict `approved` with all three checks (reality, honesty, regression) passed. Finding F1 (stable_id F-002) is **resolved**: verified by a line-by-line diff confirming every call site's From/To/Duration/EasingMode literal value is unchanged from the pre-fix code and the loop-6 session guards in `HideGridPanelAsync`/`HideSearchPanelAsync` are textually unchanged, only repositioned after the new helper call. This loop additionally re-verified Findings F-001/F-005 through F-009's fixes are still holding at their own call sites (no regression), completed a three-loop-running audit of the panel-state reentrancy hazard class with zero new instances found (two candidates traced to genuine safety, not silently passed over), and an independent helper sweep of `Services/`/`Models/` re-confirmed Finding F-010 and surfaced a new Finding F-011 (sequential per-game network calls in `LoadGameEntriesAsync`), both queued to the backlog rather than implemented this loop. No unintended scorecard regression: the change touches no network call, no ranking/selection logic, and no file outside the one named. Findings F-003, F-004, F-010 and F-011 are carried forward to the Improvement Backlog / Findings for future loops.
