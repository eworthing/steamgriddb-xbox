### Loop Counter
Loop 6 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

This loop ran an independent re-derivation from source (full re-read of `PrimaryWidget.xaml.cs`, plus fresh reads of `ArtworkDownloader.cs`, `ArtworkRanker.cs`, `SteamGridDbClient.cs`, `ArtworkFiles.cs`, `AppliedArtworkStore.cs`, `FixLog.cs`, `TileImage.cs`, `StoreNameLookup.cs`, `AsyncLazyCache.cs`, `JsonRead.cs`, `GameEntry.cs`, `GamePlatform.cs`, `GridImageItem.cs`, `TESTING.md`), plus two fresh-eyes helper sweeps briefed on the codebase's history but not its prior conclusions — one explicitly tasked with re-testing whether the "stale async completion mutates shared picker UI state" defect class (five prior instances: F-001, F-005, F-006, F-007, F-008) had a sixth instance, the other sweeping `Services/` and `Models/` cold for Reuse/Simplification/Altitude/Efficiency findings. The first sweep found a sixth and seventh instance — `HideGridPanelAsync` and `HideSearchPanelAsync` — in the panel-close path rather than the already-fixed open/populate/download paths, reachable from the ungated `CloseGridPanel_Click`/`CloseSearchPanel_Click` buttons. This loop closes that gap (both methods, one finding, one fix) and independently re-confirmed all five prior fixes still hold. The second sweep found a genuine, if minor, new finding: two of `StoreNameLookup`'s three network-backed name lookups bypass the `JsonRead` helper that exists specifically to prevent the JSON null-vs-missing bug class documented in this same codebase's history.

## Scorecard (1-10)

- Architecture quality: 7.5 | SAME | `PrimaryWidget.xaml.cs` is now 2058 lines (2031 pre-fix; +27 from this loop's own two guard clauses), still carrying Findings F-002/F-003's duplicated ceremony (still open, current lines 1571-1592/1597-1640/1779-1847/1852-1891 and 742-786/788-822/824-858). Considered moving this DOWN again, mirroring loop 5's move for the same reasoning (the session-check idiom is now duplicated across 7 sites, not 5, still a manually-repeated convention with no single Module owning it) — held the line at SAME instead. Rationale: loop 5's own Scorecard humility check already flagged the DOWN move as an arguable double-count against `state_management`/`concurrency`, where the actual runtime hazard lives; a second confirmatory instance of the *same already-acknowledged* pattern (not a new kind of gap) is priced into those two dimensions below, and compounding the identical critique into a second full architecture_quality deduction risks exactly the double-count loop 5 itself named as a risk. `architecture_quality`'s own anchors ask whether Module ownership is traceable and pass-through wrappers/costume layers are absent — the repeated guard idiom is neither; it is scattered defensive code, which is what `state_management`/`concurrency` already score.
- State management and runtime ownership: 6.0 | DOWN | F-001/F-005/F-006/F-007/F-008's own fixes independently re-verified this loop as still holding at their own call sites (re-read `PrimaryWidget.xaml.cs` in full; unchanged: session checks at `LoadGridSelectionAsync:1349`→now`1349` unaffected by this loop's edits since they sit above the insertion points, `PopulateGridSelectionPanelAsync`, `PerformGameSearchAsync`, `DownloadAndReplaceImageAsync`). But an independently-briefed helper sweep — tasked specifically with re-testing whether a sixth instance existed, not just re-reading the file — found two more: `HideGridPanelAsync` (pre-fix `PrimaryWidget.xaml.cs:1597-1624`) and `HideSearchPanelAsync` (pre-fix `:1836-1865`) both mutated shared panel state (`GridSelectionPanel.Visibility`, `GridImagesView.Items`, `CurrentSelectedGame` for the grid panel; `GameSearchPanel.Visibility`, `SearchResultsListView.Items` for the search panel) after their own `await Task.Delay(...)` with no session recheck, reachable from the *ungated* `CloseGridPanel_Click`/`CloseSearchPanel_Click` handlers — not from an already-checked caller the way `DownloadAndReplaceImageAsync`'s internal await was. Moved DOWN, not SAME, mirroring loop 5's own precedent for this exact recurrence pattern: the code did not regress since loop 5, but a sixth/seventh confirmed instance — found via a sweep explicitly designed to falsify the "no more instances" assumption — is evidence the guard convention still is not self-auditing, three loops running (4→5→6). Fixed this loop; see Loop 6 Result.
- Domain modeling: 8.5 | SAME | `GameEntry.cs` and `GamePlatform.cs` re-read in full this loop (directly, and independently by a helper); `GameEntry.cs`'s `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` three-independently-settable-properties case (sole construction site `PrimaryWidget.xaml.cs:650-664`, unaffected by this loop's edits) remains the only known concern, still no live harm. **Stalled-Dimension Sweep (loop 6, 5th consecutive SAME):** explicit clean — re-read `GameEntry.cs` in full and independently via helper; only known concern unchanged, not promotable.
- Data flow and dependency design: 7.0 | DOWN | `Services/*` re-confirmed zero `Windows.UI.Xaml` imports. New finding this loop (F-010): `StoreNameLookup.GetGogGameNameAsync` (`StoreNameLookup.cs:67-74`) and `GetEpicGameNameAsync` (`:188-190`) use raw `Windows.Data.Json` `ContainsKey`/`GetNamedObject`/`GetNamedString` calls instead of the `JsonRead` helper (`Services/JsonRead.cs`) that exists in this same codebase specifically to prevent a documented bug class: `GetNamedString`/`GetNamedObject` throw `InvalidOperationException` on a present-but-JSON-null member, which `ContainsKey` alone cannot distinguish from a missing one — the exact defect `PrimaryWidget.xaml.cs:470-478`'s own comment cites as having "shipped once already" for the manifest `"id"` field, and `JsonRead.cs`'s own docstring cites for the Steam app ID. `SteamGridDbClient.cs:155-181` and `EpicLibrary.cs` both correctly route through `JsonRead`; two of `StoreNameLookup`'s three name-fetch methods (GOG, Epic) do not, while the third (Ubisoft, via `LoadUbisoftGameListFromWebAsync`) uses a different, unaffected parsing shape. Moved DOWN from SAME: this is a fresh, source-backed inconsistency independently surfaced this loop by a helper sweep cold on the codebase's history, not carried forward from a prior loop.
- Framework / platform best practices: 8.0 | SAME | `SteamGridDbClient.cs:273-301`'s `BuildUrl` helper (consolidating `CODE-REVIEW.md`'s documented six-times-duplicated URL builder) re-verified fixed and holding. `SteamGridDbClient.cs:136-142`'s `DataContractJsonSerializer`/`Windows.Data.Json` split unchanged. Deduction unchanged: Finding F-002's four-times duplicated `DoubleAnimation`/`Storyboard` ceremony, still open. **Stalled-Dimension Sweep (loop 6, 5th consecutive SAME):** F-002 remains the named candidate; outranked again this loop by F-009's higher severity — fifth loop running.
- Concurrency and runtime safety: 5.5 | DOWN | Same evidence as state_management. F-009 (Serious deduction, same tier as F-001/F-005/F-006/F-007/F-008) is this loop's sixth-and-seventh discovery of the identical async-population-without-a-recheck shape, this time in the teardown path rather than the populate path. Moved DOWN, not SAME: two new scattered-guard sites found via a targeted re-test sweep, on top of the five already known, means verifying completeness now requires reading eight call sites across the file rather than trusting a prior "exhaustive" claim — this matches the anchor's 5-6 range language ("async flows work by convention... reentrancy behavior requires reading scattered code") more closely than the 7-anchor's "isolation mostly right, some lifecycle gaps remain." Fixed this loop, verified by build + full test suite + independent implementation review (verdict `approved`).
- Code simplicity and clarity: 7.0 | SAME | `PrimaryWidget.xaml.cs:742-858` (F-003) and the four animation bodies (F-002, now at `:1571-1592`/`:1597-1640`/`:1779-1847`/`:1852-1891`, evidence lines refreshed for this loop's +27-line insertion) remain open, unchanged in substance. **Stalled-Dimension Sweep (loop 6, 5th consecutive SAME):** a helper sub-agent swept `Services/Artwork`, `Services/Library`, `Services/SteamGridDB`, `Services/Stores`, `Services/AsyncLazyCache.cs`, `Services/JsonRead.cs` and `Models/` this loop across Reuse/Simplification/Altitude/Efficiency angles; nothing beyond the already-tracked F-002/F-003 and the new F-010 (StoreNameLookup/JsonRead, priced under data_flow above, not double-counted here) — every other file reported an explicit clean.
- Test strategy and regression resistance: 6.5 | SAME | `PrimaryWidget.xaml.cs` remains untestable outside an app container — re-confirmed via both `.csproj` target frameworks (`UAP` vs `net8.0-windows10.0.19041.0` desktop projection), matching `TESTING.md:49-56`'s own documented reasoning, re-read in full this loop and still accurate. This loop's own investigation found a sixth/seventh independent concurrency/state-ownership defect (F-009) on that exact untestable surface via source reasoning and an independent helper sweep alone, once again in a location a prior loop's own confidence did not anticipate. **Stalled-Dimension Sweep (loop 6, 5th consecutive SAME, the lowest-scored dimension on the board):** `TileImageTests.cs` re-checked (Finding F-004, still no case at the exact `alpha == 64` or `transparentCorners == 2` boundary, `TileImage.cs:250`/`:263` re-read unchanged) — the only candidate on this dimension, Cosmetic severity and off-primary-flow, does not win Priority 1: the dimension's ceiling is set by `PrimaryWidget.xaml.cs`'s structural untestability (a legacy `AppContainerExe`/`UAP` target with no desktop projection, per `TESTING.md:49-56`), not by what `TileImage.cs`'s test file contains.
- Overall implementation credibility: 8.0 | UP | `gridPanelSessionId`/`searchPanelSessionId` field comments continue the codebase's documented-rationale discipline; this loop's own `HideGridPanelAsync`/`HideSearchPanelAsync` fix follows the identical idiom and comment style as the five prior fixes. Moved UP: this is the sixth consecutive loop in which every one of a growing set of prior fixes (now 7 guarded call sites across 6 loops) independently re-verified as still holding under fresh inspection, and the methodology that keeps finding the remaining gaps — an independently-briefed helper explicitly tasked with falsifying the prior loop's "no further instance" assumption, rather than a generic re-sweep — is now validated a second time in a row (loop 5 found F-008 this way; loop 6 found F-009 the same way). Structural proof for the UP move: this loop's own commit (F-009's fix, `PrimaryWidget.xaml.cs`), reviewed independently and returning `approved` with all three checks passed — a structural change loop 5 did not have.

## Authority Map
Re-emitted this loop: an authority-related finding, F-009, is Priority 1.

- Concern: **Selected game for the artwork picker (`CurrentSelectedGame`) - write path**
  - Owner: `PrimaryWidget`
  - Allowed writers: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (nulls on close, now session-guarded)
  - Readers: `LoadGridSelectionPanelAsync`, `PopulateGridSelectionPanelAsync`, `GridImage_Click`, `DownloadAndReplaceImageAsync` (reads once, by value, before its own await), `ShowSearchPanelAsync`
  - Persistence seam: none (in-memory UI field)
  - Async mutation entry points: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (previously reachable via a stale close animation with no guard; fixed this loop)
  - Verdict: **Single and clear** - `CurrentSelectedGame`'s own write sites were never multiply-owned; F-009's hazard was `HideGridPanelAsync` writing `null` to it from a stale, superseded close, not a second concurrent writer.

- Concern: **Grid picker panel display contents (`GridImagesView.Items` / `GridPanelStatus` / `GridLoadingRing` / panel visibility during population and close)**
  - Owner: `PrimaryWidget` (`LoadGridSelectionAsync` / `PopulateGridSelectionPanelAsync` / `DownloadAndReplaceImageAsync` / `HideGridPanelAsync`)
  - Allowed writers: `LoadGridSelectionAsync` (Clear, status text, loading ring), `PopulateGridSelectionPanelAsync` (Add - guarded since loop 4/F-007), `DownloadAndReplaceImageAsync` (status text, triggers `HideGridPanelAsync` - guarded since loop 5/F-008), `HideGridPanelAsync` (Visibility/Items/CurrentSelectedGame - guarded since this loop/F-009)
  - Readers: user (visually), `GridImage_Click` (via `SessionId` comparison)
  - Persistence seam: none
  - Async mutation entry points: `LoadGridSelectionAsync`, `DownloadAndReplaceImageAsync`, `HideGridPanelAsync` (one invocation per Edit/Search-result click, per tile click, per Close click)
  - Verdict: **Split and ambiguous (pre-fix)** - see Finding F-009. `CloseGridPanel_Click` calls `HideGridPanelAsync` with no session check of its own; the panel is only partially covering the screen during its own 200ms close animation, so a new Edit click on a different game can start (and show its own panel/tiles/selected game) before the stale close finishes. This loop's fix captures the session before the animation starts and rechecks it after, mirroring the four prior fixes. Re-audit next loop once the fix has a full loop's scrutiny, matching the cadence applied to F-005/F-006/F-007/F-008.

- Concern: **Search results panel display contents (`SearchResultsListView.Items` / `SearchPanelStatus` / `SearchLoadingRing` during a name search, and its own close path)**
  - Owner: `PrimaryWidget` (`PerformGameSearchAsync` / `ShowSearchPanelAsync` / `HideSearchPanelAsync`)
  - Allowed writers: `PerformGameSearchAsync` (Clear, Add, status text, loading ring - guarded since loop 3/F-006), `ShowSearchPanelAsync` (Clear, header/box text), `HideSearchPanelAsync` (Visibility/Items - guarded since this loop/F-009)
  - Readers: user (visually), `SearchResult_Click`
  - Persistence seam: none
  - Async mutation entry points: `PerformGameSearchAsync`, `ShowSearchPanelAsync`, `HideSearchPanelAsync`
  - Verdict: **Split and ambiguous (pre-fix)** - the search panel's own close path (`CloseSearchPanel_Click` → `HideSearchPanelAsync`) had the identical unguarded shape as the grid panel's; fixed this loop alongside it (same finding, same commit). `SearchResult_Click`'s call to `HideSearchPanelAsync(false)` is unaffected: nothing bumps `searchPanelSessionId` during that specific transition, so the new guard passes through as before.

- Concern: **Bulk library operation exclusivity (`isLibraryOperationRunning`)**
  - Owner: `PrimaryWidget`
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Readers: `IsLibraryOperationBlocking`, `TryBeginLibraryOperation`
  - Persistence seam: none
  - Async mutation entry points: `PrimaryWidget_Loaded`, `RefreshButton_Click`, `FixLibraryButton_Click`, `RestoreChangesButton_Click`, `RevertDefaultsButton_Click`
  - Verdict: **Single and clear** - unchanged since loop 4; not re-walked line-by-line this loop (no evidence suggests drift, and this loop's fix is unrelated to this gate), carried forward as background context.

- Concern: **Applied-artwork record (`AppliedArtworkStore`'s path->artworkId map)**
  - Owner: `AppliedArtworkStore` (static Module)
  - Allowed writers: `SetAsync`, `ClearAsync` (via `UpdateAsync`, gated by `SemaphoreSlim gate`)
  - Readers: `GetAsync` (same gate)
  - Persistence seam: `applied-artwork.json` in the widget's local data (`RecordFolder`)
  - Async mutation entry points: `ReplaceImageCoreAsync`, `RestoreBackupCoreAsync`
  - Verdict: **Single and clear** - re-read in full this loop; `GetAsync`/`UpdateAsync` both take the same `gate` before touching the shared `Dictionary` instance, confirmed unchanged.

- Concern: **Store-name / capsule-parse / fix-log ambient state (`StoreNameLookup`'s three dictionaries, `SteamGridDbClient.CapsuleParseNotes`, `FixLog`'s fields)**
  - Owner: `StoreNameLookup` / `SteamGridDbClient` / `FixLog` (static Modules)
  - Allowed writers: `GetOrFetchGogNameAsync`, `GetOrFetchEpicNameAsync`, `FindGameByNameAsync` (unlocked dictionaries), `NoteCapsuleParse` (unlocked list), `FixLog.Start`/`Write` (unlocked list+fields)
  - Readers: same methods, `FixLibraryAsync` (reads `CapsuleParseNotes`)
  - Persistence seam: none
  - Async mutation entry points: `LoadGameEntriesAsync`'s per-entry loop; `FixLibraryAsync`
  - Verdict: **Single and clear** - independently re-traced this loop by helper: writer (`NoteCapsuleParse`, via `LoadGameEntriesAsync`'s sequential loop) and reader (`FixLibraryAsync`) both reachable only through the shared `isLibraryOperationRunning` gate, so they can never run concurrently. (See also Finding F-010 for a data-correctness, not thread-safety, concern on two of this Module's own methods.)

## Strengths That Matter
- `ArtworkDownloader.cs`'s `DownloadBestTileFillingImageAsync` / `FindOfficialLookalikeAsync` / `PassesColourAndLayoutGate` / `ChosenAlreadyMatchesOfficialArt` split (`ArtworkDownloader.cs:71-220`, re-read in full this loop) remains a genuinely deep Module: the five-step selection-and-veto pipeline is documented with the specific graded incident (`officialArtworkFloor`'s doc comment cites "Mad Max at 0.51") that calibrated each threshold, the two gate predicates are extracted as pure, independently-testable functions.
- `ArtworkRanker.RankGrids` (`ArtworkRanker.cs:189-205`, re-read this loop) computes each grid's ranking signals exactly once via the private `RankedGrid` wrapper (`:151-182`) rather than recomputing `GridMetadata`'s regex passes per sort-key access, and the doc comments name specific rejected alternatives with their graded results (e.g. "moved 26 picks and graded 2 better against 7 worse") - genuine engineering discipline, not decoration.
- This loop's own verification methodology held up under a second consecutive test: an independently-briefed helper explicitly tasked with re-testing "is there a sixth instance" (not just re-reading the file) found the gap a same-loop full-file read initially missed, exactly as loop 5's equivalent methodology found F-008. Two consecutive loops now validate that a targeted, adversarially-framed sweep catches what a confident, generically-framed one does not.

## Findings

### Finding #1 (stable_id F-009): HideGridPanelAsync and HideSearchPanelAsync unconditionally mutated shared panel state after their own await, with no session recheck, reachable from ungated Close-button clicks

**Why it matters** — A user who clicks the picker's Close (X) button starts a ~200ms slide-down animation. During that animation the full-screen panel only partially covers the screen, so the game list underneath - and a different game's Edit/Search button - can become reachable before the panel fully collapses. If a new picker/search session starts during that window, the stale close call finishing afterward would collapse the new, live session's panel, clear its tiles/results, and (for the grid panel) null its selected game - not just finish closing the panel the user actually clicked to close.

**What is wrong** — `HideGridPanelAsync` (pre-fix `PrimaryWidget.xaml.cs:1597-1624`) had no session capture or check: after `await Task.Delay(200)` it unconditionally ran `GridSelectionPanel.Visibility = Visibility.Collapsed; GridImagesView.Items.Clear(); CurrentSelectedGame = null;` plus focus restoration. `HideSearchPanelAsync` (pre-fix `:1836-1865`) had the identical shape: after its own `await Task.Delay(200)` it unconditionally collapsed `GameSearchPanel` and cleared `SearchResultsListView.Items`. Both are reached from `CloseGridPanel_Click`/`CloseSearchPanel_Click`, neither of which check anything before calling in. This is the same "stale async completion mutates shared picker UI state" hazard class as Findings F-001, F-005, F-006, F-007 and F-008, this time in the panel-close/teardown path rather than the open/populate/download paths those five fixes already cover.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1597-1624` (pre-fix, `HideGridPanelAsync`, no session recheck after its own await), `:1629-1632` (pre-fix, `CloseGridPanel_Click` calls in with no guard of its own), `:1836-1865` (pre-fix, `HideSearchPanelAsync`, same shape), `:1870-1873` (pre-fix, `CloseSearchPanel_Click` calls in with no guard of its own)

**Architectural test failed** — n/a - different category (state-ownership/reentrancy defect, matching Findings F-001/F-005/F-006/F-007/F-008's own categorization)

**Dependency category** — n/a (not a Coupling & Leakage finding)

**Leverage impact** — None - correctness fix inside each method's own body; `CloseGridPanel_Click`/`CloseSearchPanel_Click`'s call sites are unchanged.

**Locality impact** — Fix stays entirely inside `HideGridPanelAsync` and `HideSearchPanelAsync` (one guard clause each, reusing the existing `gridPanelSessionId`/`searchPanelSessionId` fields); no other Module's behavior changes, and no network call is added, removed, or reordered.

**Metric signal, if any** — none

**Why this weakens submission** — A sixth and seventh instance of the identical "stale authority remains alive" hazard class, found via a sweep explicitly tasked with falsifying loop 5's own "fifth instance was the last one" implicit confidence, in the one path (panel teardown) none of the five prior fixes touched. Confirms the guard convention is not self-auditing across a third consecutive loop (4→5→6). Severity mirrors F-005/F-006/F-007/F-008: no network call or artwork-write-target is affected by this defect - only the panel's own in-memory display, item list, and (for the grid panel) selected-game field can be corrupted for an unrelated live session.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Add the same guard idiom used at the five prior sites to both methods: capture `int session = gridPanelSessionId;` (or `searchPanelSessionId`) before the animation starts, and after `await Task.Delay(...)` add `if (session != gridPanelSessionId) { return; }` (or the search equivalent) before any mutation. Reuses the existing fields - no new field, no new type.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/Models/**`, `SteamGridDB.Xbox/Services/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml`.

**Status this loop: implemented — see Loop 6 Result below.**

### Finding #2 (stable_id F-002): Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to the panel's slide timing or easing (or a bug in it) has to be made and verified in four places instead of one, and the four copies have already begun to drift (200ms hide vs 250ms show, independently re-derived each time).

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` (current lines `1571-1592`, `1597-1640`, `1779-1847`, `1852-1891` - shifted by this loop's own +27-line F-009 fix, which lands inside `HideGridPanelAsync` and `HideSearchPanelAsync`) each still hand-build a `DoubleAnimation` + `Storyboard` against a `TranslateTransform`, set From/To/Duration/EasingFunction, call `storyboard.Begin()`, then await `Task.Delay` matching the duration. This loop's own F-009 fix added a session guard around the mutation *after* each Hide method's animation, but did not touch the duplicated animation-construction ceremony itself - a deliberate scope decision (see Simplification Check below).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1571-1592`, `:1597-1640`, `:1779-1847`, `:1852-1891`

**Architectural test failed** — Shallow module (each Show/Hide method's Interface ≈ its Implementation; no reuse across the four near-identical bodies)

**Dependency category** — n/a

**Leverage impact** — One call site to read/change instead of four; callers learn less, not more.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`; no other file's behavior changes.

**Metric signal, if any** — none

**Why this weakens submission** — Four near-identical bodies in the single largest, most-churned file in the codebase (`PrimaryWidget.xaml.cs`, 2058 LOC) is exactly the leaf-module duplication the Simplicity dimension's 9-anchor requires be swept for; it currently is not swept away. Five loops queued without action, correctly outranked each time by a higher-severity finding.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(TranslateTransform transform, double from, double to, int durationMs, EasingMode mode)` helper that builds the `DoubleAnimation`/`Storyboard` once and awaits `Task.Delay(durationMs)`; each of the four call sites becomes a one-line call. No new Seam, no new file - the four bodies collapse into the one Module (`PrimaryWidget`) that already owns them.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml` (markup unchanged), `SteamGridDB.Xbox/Services/**`.

### Finding #3 (stable_id F-010): StoreNameLookup's GOG and Epic name-fetch methods bypass the JsonRead helper that exists to prevent a documented JSON null-vs-missing bug

**Why it matters** — The codebase already shipped a bug once (documented in `PrimaryWidget.xaml.cs:470-478` and `JsonRead.cs:13-16`) where a JSON member that was present-but-null was mishandled by raw `Windows.Data.Json` calls; `JsonRead.cs` exists specifically to make that class of bug impossible. Two of `StoreNameLookup`'s three network-backed name-lookup methods still use the pattern that caused it.

**What is wrong** — `GetGogGameNameAsync` (`StoreNameLookup.cs:67-74`) reads `gameData.ContainsKey("_embedded") && gameData.GetNamedObject("_embedded").ContainsKey("product")`, then `gameData.GetNamedObject("_embedded").GetNamedObject("product")` (calling `GetNamedObject("_embedded")` twice), then `product.GetNamedString("title")` - all raw `Windows.Data.Json` calls. `GetEpicGameNameAsync` (`:188-190`) does the same for `gameData.GetNamedString("title")`. `GetNamedObject`/`GetNamedString` throw `InvalidOperationException` when the member is present but JSON `null` (as opposed to absent), which `ContainsKey` cannot distinguish from a normal string value - the exact ambiguity `JsonRead.Object`/`JsonRead.String` (`Services/JsonRead.cs`) were written to resolve, and which `SteamGridDbClient.cs:155-181` and `EpicLibrary.cs` both correctly use instead. The outer broad `catch (Exception ex)` in both `GetGogGameNameAsync` and `GetEpicGameNameAsync` means a null-title response would not crash - it would silently degrade to "name not found" (the same outcome as a genuinely missing title), so there is no live crash risk today; the harm is a maintained inconsistency next to the exact helper built to remove it, and any refactor that narrows that catch would reintroduce the original bug class.

**Evidence** — `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs:67-74` (`GetGogGameNameAsync`, raw `ContainsKey`/`GetNamedObject`/`GetNamedString`), `:188-190` (`GetEpicGameNameAsync`, same pattern), `SteamGridDB.Xbox/Services/JsonRead.cs:1-17` (the helper and its own docstring naming the bug class), `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:470-478` (the manifest-parsing comment citing the same bug class having shipped once already), `SteamGridDB.Xbox/Services/SteamGridDB/SteamGridDbClient.cs:155-181` (the established correct pattern, `JsonRead.Object`/`JsonRead.String`)

**Architectural test failed** — n/a - a Reuse/consistency finding, not a Seam/Module-boundary finding

**Dependency category** — n/a

**Leverage impact** — None directly - `JsonRead` already exists and is already used by two other call sites; this brings the remaining two into line rather than adding anything.

**Locality impact** — Contained to `StoreNameLookup.cs`'s two methods; no other file's behavior changes; `JsonRead.cs` itself is untouched.

**Metric signal, if any** — none

**Why this weakens submission** — Credibility/consistency deduction, not a live defect: the established, documented, purpose-built helper for this exact class of JSON-parsing bug exists in the same file's own dependency graph and is used by two of the three sibling call sites in `StoreNameLookup.cs` itself (`GetUbisoftGameNameAsync`'s path differs structurally) plus `SteamGridDbClient.cs` and `EpicLibrary.cs`, yet two methods opt out with no stated reason.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Replace the raw `ContainsKey`/`GetNamedObject`/`GetNamedString` chains in both methods with `JsonRead.Object(gameData, "_embedded")` → `JsonRead.Object(embedded, "product")` → `JsonRead.String(product, "title")` (GOG) and `JsonRead.String(gameData, "title")` (Epic), matching `SteamGridDbClient.cs`'s established pattern. Behavior-preserving: for every currently-well-formed response the observable result (a name string or `null`) is identical; for a hypothetical null-valued `"title"`, both the old (exception caught by the outer `catch`) and new (`JsonRead` returns `null` directly) paths converge on the same observable `null` return to the caller - no network call, call count, ordering, payload, or error-handling behavior visible to any caller changes.

**Blast radius** — Change: `SteamGridDB.Xbox/Services/Stores/StoreNameLookup.cs`. Avoid: `SteamGridDB.Xbox/Services/JsonRead.cs`, `SteamGridDB.Xbox/Services/SteamGridDB/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`.

### Finding #4 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations has to be kept in sync by hand - the same style-resource assignment, the same `XamlRoot` API-contract check, the same `TryBeginLibraryOperation`/`EndLibraryOperation` guard wiring - and nothing enforces that a future fourth operation (or an edit to one of the three) follows the same shape.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (current lines `742-786`, `788-822`, `824-858` - unaffected by this loop's edits, which land far below at line 1597+) each build a `ContentDialog` with the same four style-resource lookups and the same `Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent` `XamlRoot` check, call `ShowAsync`, branch on the result, and wrap the actual operation in the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` pattern.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:742-786`, `:788-822`, `:824-858`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action instead of re-deriving the whole confirm-guard-run shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as Finding F-002, in the same file; combined the two clusters account for roughly 225 of `PrimaryWidget.xaml.cs`'s 2058 lines being ceremony repeated 3-4x rather than owned once. Five loops queued without action, correctly outranked each time by a higher-severity finding.

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
- Structurally necessary: Finding F-009's session-liveness guards close a real, evidenced display-corruption path (an unrelated live session's panel closed, tiles/results cleared, and for the grid panel its selected game nulled) caused by a stale `HideGridPanelAsync`/`HideSearchPanelAsync` completion (no architectural test in the deletion/seam sense applies - this is a state-ownership fix, matching Findings F-001/F-005/F-006/F-007/F-008's own categorization).
- New seam justified: No new Seam introduced. Considered and rejected again this loop, now with a sixth and seventh data point (a shared `SessionGuard`/`PickerSessionToken` type wrapping `gridPanelSessionId`/`searchPanelSessionId`). Fails **Q1 (fixes real ambiguity)**: the failure mode across all seven instances was never "the check is hard to write" - it was "a specific method's own suspension point was never given the check at all," including, this loop, the *teardown* path rather than a populate path. A wrapper type does not change whether a future async method remembers to call it. Also fails **Q3 (duplicate layer)** on the same textual-cost grounds established since loop 4 (`session != gridPanelSessionId`, ~25 characters, is not shorter or clearer than a wrapper call). A bigger alternative was also considered this loop given the pattern's now-seven-instance recurrence: centralizing panel open/populate/close behind a single owning orchestration method (a `SwapPanelSessionAsync`-style coordinator) that captures the session once and threads it through the whole lifecycle. Rejected: this would be a materially larger, riskier rewrite of `PrimaryWidget.xaml.cs`'s UI orchestration on a file that cannot be unit-tested (see `test_strategy`), for a defect class where every fixed instance has held across every subsequent loop's re-verification (F-001/F-005/F-006/F-007/F-008 all re-confirmed holding again this loop) - the marginal risk of a large untested rewrite outweighs the marginal benefit over the existing, proven, minimal per-site guard idiom. This is the same "coordinator ceremony" trap loop 1's own Deepening Candidate and every subsequent loop's Final Judge Narrative have warned against.
- Helpful simplification: none this loop (Findings F-002/F-003 remain queued, not implemented, fifth loop running for F-002).
- Should NOT be done: Do not build a shared session-guard type or population/close-orchestration wrapper (see above). Also re-confirmed this loop: do not add a per-row reentrancy guard to `RestoreBackup_Click`/`RestoreBackupCoreAsync` - independently re-traced this loop via helper: `ArtworkFiles.RestoreOriginalAsync`'s backup-first, rename-with-`ReplaceExisting` ordering means a concurrent second call on the same row either succeeds harmlessly or throws (caught by the outer `catch`, reported as a user-facing error) - no data loss, no corruption, materially smaller blast radius than F-001/F-005/F-006/F-007/F-008/F-009's shape.
- Tests after fix: None added or deleted - `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface and cannot be unit-tested in the current infra, matching Findings F-001/F-005/F-006/F-007/F-008's fixes. Verification is: full build (exit 0) + full test suite (138/138 unchanged) both re-run after the change, an independent fresh-eyes implementation review (separate subagent, read-only, verdict `approved`, all three checks passed), plus a manual trace confirming both guards sit after each method's only await that matters and before every subsequent mutation. This is the `reasoning_only` evidence path (Meta-Rule 4) for the local-UI-ownership invariant; the no-network-call-touched half is directly inspectable (neither guard touches, reorders, or wraps any network/file-write call - both sit entirely within the already-network-free animation/teardown tail of each method).

## Improvement Backlog
1. **Collapse the four-times-duplicated panel slide animation (Finding F-002).**
   - why it matters: removes ~120 lines of repeated ceremony from the largest, most-churned file; the two Show/Hide pairs have already drifted (200ms vs 250ms) once, showing the duplication is actively costing consistency, and it has now been queued unfixed for five full loops.
   - score impact: `simplicity +0.5; framework_idioms +0.5`
   - simplification
   - helpful

2. **Route StoreNameLookup's GOG and Epic name-fetch methods through the existing JsonRead helper (Finding F-010).**
   - why it matters: removes a documented-bug-class inconsistency next to the exact helper built to prevent it; small, mechanical, behavior-preserving per the five properties named in the finding.
   - score impact: `data_flow +0.5`
   - simplification
   - helpful

3. **Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003).**
   - why it matters: removes ~105 more lines of repeated ceremony from the same file; combined with F-002 this is the largest remaining simplicity gap in the codebase, also queued unfixed for five full loops.
   - score impact: `simplicity +0.5`
   - simplification
   - helpful

**Priority-1 accounting**: F-009 (loop_local F1) is Priority 1 on severity (Serious deduction, the only finding at that severity this loop - F-002/F-003/F-010 are Noticeable, F-004 is Cosmetic) and this is the fifth consecutive loop that severity has correctly outranked F-002/F-003 despite their Stall (5 consecutive SAME loops on the dimensions they target). Named explicitly per the Backlog Prioritization Pass's actionability/stall criteria: this is not proximity bias - F-009 was independently surfaced by a helper sweep specifically tasked with re-testing the "is there another instance" question rather than a generic re-read, found in the one code path (panel close/teardown) genuinely distinct from all five prior fixes' paths (open/populate/download), and verified by this loop's own full-file re-read plus an independent implementation review before being accepted. A Serious-severity, source-proven, safely-fixable defect on a primary user flow beats three Noticeable/Cosmetic-severity items on Backlog Prioritization criterion 3 (Severity) regardless of criterion 2 (Stall)'s tie-breaking role. If no further Serious-or-worse finding surfaces next loop, F-002 is the correctly-queued next pick (Priority 1 above) and should not be deferred a sixth time without a comparable severity justification - the pattern of "a Serious finding keeps appearing just as F-002/F-003 are about to win" is itself worth flagging honestly rather than treating as coincidence indefinitely.

## Deepening Candidates
1. **Candidate Module**: `PrimaryWidget`'s panel slide-animation ceremony (the four bodies in Finding F-002).
   - Source friction proven: Finding F-002 - four near-identical `DoubleAnimation`/`Storyboard` bodies, one already-observed drift (200ms vs 250ms) between the pairs, re-confirmed unchanged this loop (line numbers refreshed for this loop's own +27-line insertion).
   - Why the current Interface is shallow or misplaced: there is no Interface at all - each Show/Hide method inlines its own animation construction; Interface ≈ Implementation four times over (Shallow module test).
   - What behavior should move behind the deeper Interface: the `DoubleAnimation` + `Storyboard` construction, `Begin()`, and matching `Task.Delay`, parameterized by transform/from/to/duration/easing.
   - Dependency category: `in-process`
   - Test surface after the change: none - `PrimaryWidget.xaml.cs` is UWP-page-bound and untestable in the current infra either before or after this extraction; verified by build + manual trace, same as Findings F-001, F-005, F-006, F-007, F-008 and F-009's fixes.
   - Smallest first step: extract `private static async Task SlidePanelAsync(TranslateTransform transform, double from, double to, int durationMs, EasingMode mode)` and replace all four call sites.
   - What not to do: do not introduce an `IAnimator`/`IPanelController` protocol or a separate animation-coordinator class - this is private in-process UI glue with one production caller-family; Unified Seam Policy's two-adapter rule would fail immediately, and single-Adapter policy/failure/platform-isolation justification doesn't apply either. Inline extraction only.

## Builder Notes

1. **Pattern: a helper sweep framed as "does a gap still exist" finds what a helper sweep framed as "look for problems" misses - the framing of the task, not just the act of delegating it, is what surfaces a falsified assumption.**
   - How to recognize: after a prior loop closes a recurring defect class, the temptation is to spawn a generic "review this file for issues" sweep next time. That framing inherits the same blind spot the fix's author had, because nothing in the prompt asks the sweep to specifically distrust the "this was the last instance" assumption.
   - Smallest coding rule: when re-testing a completeness claim, brief the checker with the specific claim to falsify ("is there a sixth instance of X shape, reachable from an ungated entry point") rather than a generic quality pass. Two consecutive loops (5 and 6) both used this framing and both found a real gap a generic sweep of the same file might not have specifically hunted for.
   - Stack example: this loop's helper was briefed to enumerate every async method with a mutation after its own await and trace *each one's* reachability back to a gate, rather than "look for bugs in PrimaryWidget.xaml.cs" - that enumeration discipline is what caught `HideGridPanelAsync`/`HideSearchPanelAsync`, both of which "look" like simple teardown code with nothing to audit until the reachability trace is actually run.

2. **Pattern: the same defect class can hide in a teardown/close path just as easily as in a populate/open path - "this method doesn't populate anything, it just cleans up" is not evidence it is safe.**
   - How to recognize: an async method that runs an animation then unconditionally resets state (`Visibility = Collapsed`, `Items.Clear()`, nulls a selection field) reads as "closing" rather than "writing," which makes it easy to mentally exclude from a sweep focused on "does every populate step have a guard." But a stale close finishing after a new session has already opened and populated its own panel is exactly as destructive as a stale populate step overwriting a live one.
   - Smallest coding rule: audit every async method that mutates shared UI/session state after its own await, regardless of whether the mutation is additive (populate) or subtractive (teardown/reset) - the reentrancy hazard is identical in both directions.
   - Stack example: `HideGridPanelAsync` nulls `CurrentSelectedGame` and clears `GridImagesView.Items` - the same fields `PopulateGridSelectionPanelAsync` (F-007, fixed loop 4) writes to on the *populate* side. Both directions needed the same guard; only one had it before this loop.

3. **Pattern: when a fix touches network-call-adjacent code but is queued rather than implemented, name the behavior-preservation argument in the finding text now, so the loop that eventually implements it does not have to re-derive it from scratch.**
   - How to recognize: a finding's minimal correction path touches a method that makes an external network call, but the proposed fix itself only changes local parsing/plumbing around that call, not the call's arguments, count, ordering, or error surface. It is tempting to defer the behavior-preservation analysis to "whichever loop actually implements it."
   - Smallest coding rule: state explicitly which of the five behavior-preservation properties (call count, ordering, payload, error handling, observable result) the fix relies on and why, in the finding itself - not just at implementation time. A future loop implementing a two-loop-old finding should not have to re-investigate whether it is safe to attempt.
   - Stack example: Finding F-010's correction path (routing `StoreNameLookup.GetGogGameNameAsync`/`GetEpicGameNameAsync` through `JsonRead`) touches methods that call GOG's and a GitHub-hosted community database's HTTP endpoints, but the fix only changes how the already-fetched response body is parsed - the finding states explicitly that both the well-formed and hypothetical-null-title cases converge on the same observable `null`-or-name result to the caller, so the STANDING USER CONSTRAINT's "provably behaviour-preserving" carve-out applies without needing a behavioral oracle.

**Scorecard humility check** — three claims here are the ones most likely to be argued the other way: (1) holding `architecture_quality` at SAME (7.5) rather than moving it DOWN again for the second confirmatory instance of the same "manually-repeated convention" critique - a reviewer could reasonably argue two more scattered guard sites (bringing the total to seven) is independently-verified evidence the convention doesn't scale regardless of double-counting risk, and that loop 5's own DOWN move set the correct precedent this loop should have continued rather than held. (2) Treating `HideGridPanelAsync` and `HideSearchPanelAsync` as one finding (F-009) fixed in a single loop rather than two separate findings fixed one loop at a time, as F-005 through F-008 each were - a stricter reading of the one-Priority-1-finding-per-loop cadence could argue `HideSearchPanelAsync` "jumped the queue" ahead of F-002/F-003 without its own dedicated loop's worth of scrutiny, even though the fix is mechanically identical and low-risk. (3) The judgment call not to reuse F-006's `stable_id` for F-009 despite a mechanical line-proximity (`Fuzzy-match rule M2`) collision (`HideGridPanelAsync`'s evidence-line start sits within 50 lines of F-006's recorded start, and both share `test_failed: n/a` and `severity: Serious deduction`) - Title/Claim proximity (M1) clearly does not match, and the two findings describe different methods with different fixes already independently verified holding, but a strict reading of Step 1.5's "iff" wording could argue the mechanical rule should have been followed regardless of the semantic mismatch; this is documented explicitly here so a future loop or the registry validator can revisit the call.

## Final Judge Narrative
Place, not win, this loop. Runtime ownership is again not more trustworthy by a plain net-wash accounting - a sixth and seventh instance of the identical shape lived in the panel-close path, found by a sweep this loop specifically tasked with distrusting the prior loop's implicit "that was the last one" confidence. This loop closes that gap with the same minimal, proven idiom as the five prior fixes, verified by full build + full test suite (138/138 unchanged) + an independent implementation review (verdict `approved`, all three checks passed). State_management and concurrency both moved DOWN again, not because the code regressed since loop 5, but because a third consecutive loop finding more instances of the same class is itself evidence the residual was under-priced, exactly the same non-regression-basis for downward correction loop 5 established. Architecture_quality was deliberately held at SAME rather than compounding the same critique a second time, on the explicit reasoning that the concern is already priced into state_management/concurrency and a second full deduction risks double-counting loop 5 itself flagged as a risk - a genuinely close call, recorded in the humility check above. Simplification did not happen this loop on the F-002/F-003 axis - both remain queued for a fifth full loop, correctly outranked again by F-009's higher severity. A new, smaller finding (F-010, StoreNameLookup bypassing the established JsonRead helper) was surfaced by an independent Services/Models sweep and queued, not implemented, since it is lower severity than F-009. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; this loop's only regression evidence is full build + full suite + independent review + a manual trace of both new guards' placement. Future work risks over-engineering only if F-002/F-003's eventual extraction reaches for a coordinator abstraction instead of a private helper method, or if a future loop tries to wrap the now-seven hand-rolled session-check idioms in a shared type or a centralized panel-lifecycle orchestrator - both explicitly evaluated and rejected again this loop on Simplify Pressure Test grounds, unchanged guidance from loop 1's own Deepening Candidate, now re-affirmed a sixth and seventh time against fresh-method instances too.

## Loop 6 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (27 insertions, 0 deletions): added a session capture and recheck to `HideGridPanelAsync` (captures `int session = gridPanelSessionId;` before the close animation starts; after `await Task.Delay(200)` adds `if (session != gridPanelSessionId) { return; }` before the `Visibility`/`Items.Clear()`/`CurrentSelectedGame = null` mutations and focus restoration) and the identical pattern to `HideSearchPanelAsync` (`searchPanelSessionId`, guarding the `Visibility`/`Items.Clear()`/focus-restoration mutations). Both reuse the existing session fields already used by the five prior fixes - no new field, no new type, no new parameter. Neither method's own animation construction, timing, or the network/file-write calls elsewhere in the flow changed - no network call count, ordering, payload, or error-handling behavior changes anywhere; the fix is purely a local UI-mutation skip for a session no longer live. Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected, since `PrimaryWidget.xaml.cs` is not part of the test-linked `Services/**` surface. An independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on the targeted finding and the diff only) returned verdict `approved` with all three checks (reality, honesty, regression) passed. Finding F1 (stable_id F-009) is **resolved**: both display-corruption paths (a stale, superseded picker/search session's close animation completing and corrupting a live, unrelated session's panel) are closed by construction, verified by direct inspection of the diff (both guards sit textually immediately after each method's only await that matters and before every subsequent mutation) and by re-reading the final source. This loop additionally re-verified Findings F-001/F-005/F-006/F-007/F-008's fixes are still holding at their own call sites (no regression), and an independent helper sweep of `Services/`/`Models/` surfaced Finding F-010 (StoreNameLookup bypassing the JsonRead helper), queued to the backlog rather than implemented this loop (lower severity than F-009). No unintended scorecard regression: the change touches no network call, no ranking/selection logic beyond skipping a stale-session UI update, and no file outside the one named. Findings F-002, F-003, F-004 and F-010 are carried forward to the Improvement Backlog / Findings for future loops.
