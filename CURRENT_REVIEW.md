### Loop Counter
Loop 4 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

Loop 3's own Builder Note said fixing one instance of a defect class is not the same as auditing the class. This loop finally ran that audit for real: every `private async void`/`private async Task` method in `PrimaryWidget.xaml.cs` (32 methods, enumerated by grep, not sampled) was checked for the awaited-then-mutate-without-a-recheck shape. It found a fourth instance - nested inside `PopulateGridSelectionPanelAsync`, a method loop 2's own fix (`LoadGridSelectionAsync`, Finding F-005) already calls - proving the entry-point-level session checks loops 1-3 built do not protect a callee with its own await. This loop closes that specific gap and, for the first time across four loops, backs the fix with a genuinely exhaustive sweep of the file.

## Scorecard (1-10)

- Architecture quality: 8.0 | SAME | `ArtworkDownloader.cs:71-100` (re-read this loop, unchanged) still hides a five-step selection+veto pipeline behind two methods. `PrimaryWidget.xaml.cs` is now 2020 lines (2008 pre-fix; +12 from this loop's own guard clause) still carrying Findings F-002/F-003's duplicated ceremony (still open, current lines 742-858 and 1560-1613/1752-1854) plus this loop's newly-found-and-now-fixed F-007 (an ownership-arbitration gap nested inside `PopulateGridSelectionPanelAsync`'s own await, one level deeper than any of F-001/F-005/F-006). **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** F-007 is this dimension's named candidate, routed through Findings below.
- State management and runtime ownership: 7.0 | DOWN | F-005's (loop 2) and F-006's (loop 3) own fixes independently re-verified this loop as still holding at their own call sites - neither regressed. But this loop's first-ever exhaustive enumeration of the file's 32 `private async` methods found a fourth instance of the identical shape inside `PopulateGridSelectionPanelAsync` (`PrimaryWidget.xaml.cs:1428-1489` pre-fix) - and this one is qualitatively different from F-005/F-006: it lives *inside* a method that a prior loop's own fix already calls, proving the caller-level check does not protect a callee with its own suspension point. That is new information, not "one more sibling missed" (the framing loops 2-3 used for their own SAME calls) - it shows loop 3's SAME assessment under-priced the true residual, because neither loop 2 nor loop 3 checked inside its own fix's callees for the same shape. Moved DOWN rather than held SAME: the code did not regress since loop 3, but this loop's own more rigorous investigation is the basis for judging loop 3's confidence level as having been too high, which is a legitimate basis for a downward correction distinct from "the code got worse." Fixed this loop; see Loop 4 Result.
- Domain modeling: 8.5 | SAME | `Models/GamePlatform.cs` discriminated enum + single translation seam (`GamePlatformHelper`) read in full this loop, unchanged. `GameEntry.cs:113-145` (re-read this loop) still leaves `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` as three independently-settable properties expressing one derived fact; the sole construction site (`PrimaryWidget.xaml.cs:661-663`, unaffected by this loop's edit since it sits above the insertion point) still sets all three together - re-verified this loop, still no live harm, not promoted to a Finding. **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** walked `Models/` in full (`GameEntry.cs`, `GamePlatform.cs`, `GridImageItem.cs` - 3 types) - explicit clean; no impossible state representable, no new parallel-fields case beyond the one already tracked above.
- Data flow and dependency design: 7.5 | SAME | `Services/*` re-confirmed zero `Windows.UI.Xaml` imports by grep this loop (the two hits under `Services/` - `ArtworkFiles.cs:23`, `GameImages.cs:15` - remain doc-comment prose). **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** independently traced `StoreNameLookup`'s three unlocked dictionaries (`gogNameCache:29`, `epicNameCache:30`, `nameMatchCache:34`) and `FixLog`'s unlocked fields to their only call sites - both reachable exclusively through `LoadGameEntriesAsync`/`FixLibraryAsync`, both gated by the single `isLibraryOperationRunning` flag (`PrimaryWidget.xaml.cs:144/724/773/809/845`) - re-confirmed unreachable concurrently, same conclusion as loops 2-3. This walk explicitly evaluated and falsified a helper-surfaced candidate this loop ("unsynchronized static dictionaries," flagged without a reachability trace) - compliance-is-not-clearance cuts both ways: a shape that looks dangerous still needs a call-graph trace before it is a finding, and this one does not clear it.
- Framework / platform best practices: 8.0 | SAME | `SteamGridDbClient.cs:136-142`'s deliberate `DataContractJsonSerializer` + `Windows.Data.Json` split re-verified this loop (doc comment still cites the concrete JSON shape - `external_platform_data.steam[0].metadata.library_capsule_full.image2x.{language}` - the serializer cannot express). Deduction unchanged: `PrimaryWidget.xaml.cs:1560-1613`/`:1752-1854` (Finding F-002, still open, line numbers shifted +12 by this loop's own fix) - the four-times duplicated `DoubleAnimation`/`Storyboard` ceremony WinUI's resource/style system exists to avoid. **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** F-002 is this dimension's named candidate; still queued (Priority 2 this loop), not implemented, third loop running.
- Concurrency and runtime safety: 6.5 | DOWN | Same evidence as state_management. F-007 (Serious deduction, same tier as F-005/F-006 - cannot write artwork to the wrong game, since a clicked stale tile still only feeds `LoadGridSelectionByGameIdAsync`/`DownloadAndReplaceImageAsync`, both gated on `gridPanelSessionId` at click time; it can only corrupt the grid panel's own in-memory display and artwork ranking) is this loop's fourth discovery of the identical async-population-without-a-recheck shape, and the first found via a genuinely complete sweep rather than incidental discovery while fixing something else. Moved DOWN, not held SAME: loop 3's own SAME reasoning ("one Serious gap closed, one of the same shape newly found: net wash") assumed each loop's fix-plus-find pair was a complete accounting of that loop's residual; this loop's fix proves that assumption undercounted at loop 3 (and, by the same logic, at loop 2) - the residual was larger than either loop's own net-wash accounting captured, because neither loop checked inside its own fix's callees. Fixed this loop, with a full-file sweep providing the strongest evidence yet that no further instance remains (see Loop 4 Result and Builder Notes).
- Code simplicity and clarity: 7.0 | SAME | `PrimaryWidget.xaml.cs:742-858` and `:1560-1613`/`:1752-1854` (Findings F-003, F-002 - still open, ~225 lines of 3-4x duplicated ceremony, re-confirmed this loop at current line numbers) unchanged against otherwise-minimal Modules. **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** a helper sub-agent swept `Services/Artwork`, `Services/Library`, `Services/SteamGridDB`, `Services/Stores` and `Models/` this loop across all four angles (Reuse/Simplification/Altitude/Efficiency per method.md Step 6) and found nothing beyond the already-known `StoreNameLookup` cache asymmetry (data_flow territory) and `GameEntry`'s parallel-fields case (domain_modeling territory) - neither new, the deduction remains concentrated entirely in `PrimaryWidget.xaml.cs`'s two known clusters.
- Test strategy and regression resistance: 6.5 | SAME | `PrimaryWidget.xaml.cs` remains untestable outside an app container - re-confirmed this loop via both `.csproj` target frameworks (`SteamGridDB.Xbox.csproj`: `TargetPlatformIdentifier=UAP`; `SteamGridDB.Xbox.Tests.csproj`: `TargetFramework=net8.0-windows10.0.19041.0`, a desktop projection that cannot compile a UWP page). This loop's own investigation found a *fourth* independent concurrency/state-ownership defect (F-007) on that exact untestable surface via manual source reasoning alone, nested one level deeper than any of the first three - re-demonstrating, not further disproving, the anchor's own disqualifying language; the untestability was already fully priced into 6.5, so a fourth, deeper confirming instance holds the score rather than moving it further. **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME - the lowest-scored dimension on the board):** walked `TileImageTests.cs` in full (Finding F-004, re-verified this loop, still no case at the exact `alpha == 64` or `transparentCorners == 2` boundary) - the only candidate this dimension has ever had across four loops, and it does not win Priority 1: it is Cosmetic severity and explicitly off-primary-flow per the rubric's own anchor carve-out, so fixing it would not proportionally advance this dimension's anchor, which is capped by `PrimaryWidget.xaml.cs`'s structural untestability regardless of what `TileImage.cs`'s test file contains.
- Overall implementation credibility: 7.5 | SAME | The `gridPanelSessionId`/`searchPanelSessionId` field comments continue the codebase's documented-rationale discipline; this loop's own `PopulateGridSelectionPanelAsync` fix follows the identical comment style. Deduction unchanged: `TESTING.md:47-56`'s framing of the untested UWP surface ("What they *do* to the UI is not [covered]") continues to undersell the surface's real risk, now demonstrated a fourth time by this loop's own F-007 discovery, nested deeper than any prior instance. **Stalled-Dimension Sweep (loop 4, 3rd consecutive SAME):** re-read `TESTING.md` in full this loop; the quoted framing is verbatim unchanged from loop 1's own citation, still accurate but still under-describing the risk this loop's own finding demonstrates.

## Authority Map
Re-emitted this loop: an authority-related finding, F-007, is Priority 1.

- Concern: **Selected game for the artwork picker (`CurrentSelectedGame`) - write path**
  - Owner: `PrimaryWidget`
  - Allowed writers: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (nulls on close)
  - Readers: `LoadGridSelectionPanelAsync`, `PopulateGridSelectionPanelAsync`, `GridImage_Click`, `DownloadAndReplaceImageAsync`, `ShowSearchPanelAsync`
  - Persistence seam: none (in-memory UI field)
  - Async mutation entry points: `EditGameImage_Click`, `SearchGameImage_Click`
  - Verdict: **Single and clear**

- Concern: **Grid picker panel display contents (`GridImagesView.Items` / `GridPanelStatus` / `GridLoadingRing` during population)**
  - Owner: `PrimaryWidget` (`LoadGridSelectionAsync` / `PopulateGridSelectionPanelAsync`)
  - Allowed writers: `LoadGridSelectionAsync` (Clear, status text, loading ring), `PopulateGridSelectionPanelAsync` (Add) - pre-fix: no re-check of its own await
  - Readers: user (visually), `GridImage_Click` (via `SessionId` comparison)
  - Persistence seam: none
  - Async mutation entry points: `LoadGridSelectionAsync` (one invocation per Edit/Search-result click; multiple can be in flight concurrently)
  - Verdict: **Split and ambiguous (pre-fix)** - see Finding F-007. `LoadGridSelectionAsync`'s own guard (loop 2, F-005) checks immediately after its network awaits but does not cover `PopulateGridSelectionPanelAsync`'s own internal await (`AppliedArtworkStore.GetAsync`); this loop's fix adds the equivalent check inside `PopulateGridSelectionPanelAsync` itself, closing the nested gap. Re-audit next loop once the fix has a full loop's scrutiny.

- Concern: **Search results panel display contents (`SearchResultsListView.Items` / `SearchPanelStatus` / `SearchLoadingRing` during a name search)**
  - Owner: `PrimaryWidget` (`PerformGameSearchAsync` / `ShowSearchPanelAsync`)
  - Allowed writers: `PerformGameSearchAsync` (Clear, Add, status text, loading ring), `ShowSearchPanelAsync` (Clear, header/box text)
  - Readers: user (visually), `SearchResult_Click`
  - Persistence seam: none
  - Async mutation entry points: `PerformGameSearchAsync`, `ShowSearchPanelAsync`
  - Verdict: **Single and clear** - re-verified this loop: `PerformGameSearchAsync` has exactly one `await` in its whole body (`client.SearchGameByNameAsync`), and the existing session check sits immediately after it and before every mutation - unlike the grid picker's `PopulateGridSelectionPanelAsync`, there is no second, nested await left unguarded on this flow.

- Concern: **Bulk library operation exclusivity (`isLibraryOperationRunning`)**
  - Owner: `PrimaryWidget`
  - Allowed writers: `TryBeginLibraryOperation`, `EndLibraryOperation`
  - Readers: `IsLibraryOperationBlocking`, `TryBeginLibraryOperation`
  - Persistence seam: none
  - Async mutation entry points: `PrimaryWidget_Loaded`, `RefreshButton_Click`, `FixLibraryButton_Click`, `RestoreChangesButton_Click`, `RevertDefaultsButton_Click`
  - Verdict: **Single and clear** - re-verified this loop via a fresh grep of every `TryBeginLibraryOperation`/`IsLibraryOperationBlocking`/`EndLibraryOperation` call site; unchanged. Note: `RestoreBackup_Click` and `SearchGameImage_Click`/`EditGameImage_Click` check only this bulk flag, not a per-row guard - walked this loop, see Simplification Check for why `RestoreBackup_Click`'s narrower race was not promoted to a Finding.

- Concern: **Applied-artwork record (`AppliedArtworkStore`'s path->artworkId map)**
  - Owner: `AppliedArtworkStore` (static Module)
  - Allowed writers: `SetAsync`, `ClearAsync` (via `UpdateAsync`, gated by `SemaphoreSlim gate`)
  - Readers: `GetAsync` (same gate)
  - Persistence seam: `applied-artwork.json` in the widget's local data (`RecordFolder`)
  - Async mutation entry points: `ReplaceImageCoreAsync`, `RestoreBackupCoreAsync`
  - Verdict: **Single and clear** - unchanged since loop 2; not re-walked line-by-line this loop (no evidence suggests drift), carried forward as background context, not as a scoring basis for any dimension held this loop.

- Concern: **Store-name / capsule-parse / fix-log ambient state (`StoreNameLookup`'s three dictionaries, `SteamGridDbClient.CapsuleParseNotes`, `FixLog`'s fields)**
  - Owner: `StoreNameLookup` / `SteamGridDbClient` / `FixLog` (static Modules)
  - Allowed writers: `GetOrFetchGogNameAsync`, `GetOrFetchEpicNameAsync`, `FindGameByNameAsync` (unlocked dictionaries), `NoteCapsuleParse` (unlocked list), `FixLog.Start`/`Write` (unlocked list+fields)
  - Readers: same methods, `FixLibraryAsync` (reads `CapsuleParseNotes`)
  - Persistence seam: none
  - Async mutation entry points: `LoadGameEntriesAsync`'s per-entry loop; `FixLibraryAsync`
  - Verdict: **Single and clear** - independently re-traced this loop (see data_flow proof): both operations are reachable only through gated entry points sharing `isLibraryOperationRunning`, so they can never run concurrently with each other or themselves. This loop's trace also evaluated and falsified a helper-surfaced "unsynchronized dictionaries" candidate.

## Strengths That Matter
- `PrimaryWidget.xaml.cs`'s two session-token mechanisms (`gridPanelSessionId` at `:64-72`, `searchPanelSessionId` at `:75-80`) share one clear idiom - capture before the first await, check immediately before the destructive mutation - documented with the exact race window and failure mode each closes rather than a generic "thread safety" comment. This loop's own fix (inside `PopulateGridSelectionPanelAsync`) follows the identical idiom and comment style, verified by direct inspection rather than assumed.
- `SteamGridDbClient.cs:136-142`'s `DataContractJsonSerializer`/`Windows.Data.Json` split is justified in a doc comment citing the actual JSON shape (`external_platform_data.steam[0].metadata...{language}`) that the serializer cannot express - a real, still-true reason for the framework mismatch, re-verified this loop.
- `ArtworkFiles.RestoreOriginalAsync` (`ArtworkFiles.cs:160-191`, re-read this loop while evaluating `RestoreBackup_Click`'s missing per-row guard) is race-tolerant by construction: it locates the backup file before doing anything destructive, so a second concurrent restore attempt for the same game fails closed into a caught, reported `BackupMissing` rather than corrupting data - a real defensive property discovered by this loop's own sweep, not merely asserted.

## Findings

### Finding #1 (stable_id F-007): PopulateGridSelectionPanelAsync's own await is not re-checked against a superseded picker session

**Why it matters** — A user who edits or re-opens the artwork picker while a prior population's own local lookup is still pending can see stale, permanently unclickable tiles mixed into the live session's panel, the live session's status text overwritten by the stale one's, or the stale session's artwork ranked using whatever game is now selected instead of the game it was actually fetched for.

**What is wrong** — `PopulateGridSelectionPanelAsync` (`PrimaryWidget.xaml.cs:1428-1489` pre-fix) receives an already-session-checked call from `LoadGridSelectionAsync` - the caller re-checks `session != gridPanelSessionId` immediately after its own network awaits, per Finding F-005's loop-2 fix, before calling this method - but that check does not cover an await *inside* `PopulateGridSelectionPanelAsync` itself: `int? appliedArtworkId = await AppliedArtworkStore.GetAsync(...)` (line 1431 pre-fix) is a genuine suspension point (a semaphore wait plus, on first access, a disk read), and nothing re-checks `sessionId != gridPanelSessionId` after it returns. If a newer `LoadGridSelectionAsync` call (a different Edit/Search click) starts and bumps `gridPanelSessionId` while this await is pending, the resumed method still ranks grids/icons using `ArtworkRanker.RankGrids(grids, CurrentSelectedGame?.Name)` - reading `CurrentSelectedGame`'s now-different value - and unconditionally executes `GridImagesView.Items.Add(...)` and overwrites `GridPanelStatus.Text` for a session no longer live.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1428-1489` (pre-fix, no session re-check after its own await), `:1349-1352` (`LoadGridSelectionAsync`'s own session check, loop 2's Finding F-005, sits before calling `PopulateGridSelectionPanelAsync`, not inside it), `SteamGridDB.Xbox/Services/Artwork/AppliedArtworkStore.cs:63-84` (`GetAsync` awaits `appliedCache.GetOrLoadAsync()` and `gate.WaitAsync()`, both genuine suspension points)

**Architectural test failed** — n/a - different category (state-ownership/reentrancy defect, matching Findings F-001/F-005/F-006's own categorization)

**Dependency category** — n/a (not a Coupling & Leakage finding)

**Leverage impact** — None - correctness fix inside `PopulateGridSelectionPanelAsync`'s own body, not a change to any caller-facing Interface; `LoadGridSelectionAsync`'s call site is unchanged.

**Locality impact** — Fix stays entirely inside `PopulateGridSelectionPanelAsync` (one guard clause reusing the existing `gridPanelSessionId` field and the `sessionId` parameter already threaded through); no other Module's behavior changes, and no network call is added, removed, or reordered.

**Metric signal, if any** — none

**Why this weakens submission** — This is the same "stale authority remains alive" hazard class Findings F-001, F-005 and F-006 already closed, found a fourth time - and, unlike the first three, nested one level deeper than any prior loop audited: inside a helper method a prior loop's own fix (F-005, loop 2) already called, not at a fresh top-level entry point. It proves the entry-point-level session checks loops 1-3 built do not compose across a call boundary when the callee has its own await - a materially different fact than "one more sibling method was missed," and evidence the codebase's remaining untouched async handlers cannot be assumed safe merely because their direct caller is guarded. Severity mirrors F-005/F-006: clicking a stale tile still only feeds `LoadGridSelectionByGameIdAsync`/`DownloadAndReplaceImageAsync`, both gated on `gridPanelSessionId` at click time, so no artwork can be written to the wrong game - only the panel's own in-memory display and ranking can be corrupted.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Add the same guard clause already used at the other three sites (`if (sessionId != gridPanelSessionId) { return; }`) immediately after the `appliedArtworkId` await and before any further read of `CurrentSelectedGame` or mutation of `GridImagesView.Items`/`GridPanelStatus`. Reuses the existing `gridPanelSessionId` field and the `sessionId` parameter already threaded through - no new field, no new type. This runs after the only await in the method and before every mutation, so it changes no network call (this method makes none), no call count, ordering, payload, or error handling elsewhere in the flow.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/Models/**`, `SteamGridDB.Xbox/Services/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml`.

**Status this loop: implemented — see Loop 4 Result below.**

### Finding #2 (stable_id F-002): Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to the panel's slide timing or easing (or a bug in it) has to be made and verified in four places instead of one, and the four copies have already begun to drift (200ms hide vs 250ms show, independently re-derived each time).

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` (current lines `1560-1581`, `1586-1613`, `1752-1820`, `1825-1854`) each hand-build a `DoubleAnimation` + `Storyboard` against a `TranslateTransform`, set From/To/Duration/EasingFunction, call `storyboard.Begin()`, then await `Task.Delay` matching the duration. The only real variation across the four is which transform, which direction, and 250ms vs 200ms. Re-confirmed unchanged this loop (line numbers shifted +12 by this loop's own F-007 fix, which lands above all four bodies).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1560-1581`, `:1586-1613`, `:1752-1820`, `:1825-1854`

**Architectural test failed** — Shallow module (each Show/Hide method's Interface ≈ its Implementation; no reuse across the four near-identical bodies)

**Dependency category** — n/a

**Leverage impact** — One call site to read/change instead of four; callers learn less, not more.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`; no other file's behavior changes.

**Metric signal, if any** — none

**Why this weakens submission** — Four near-identical bodies in the single largest, most-churned file in the codebase (`PrimaryWidget.xaml.cs`, 2020 LOC) is exactly the leaf-module duplication the Simplicity dimension's 9-anchor requires be swept for; it currently is not swept away. Three loops queued without action.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(FrameworkElement transform, double from, double to, int durationMs, EasingMode mode)` helper that builds the `DoubleAnimation`/`Storyboard` once and awaits `Task.Delay(durationMs)`; each of the four call sites becomes a one-line call. No new Seam, no new file - the four bodies collapse into the one Module (`PrimaryWidget`) that already owns them.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml` (markup unchanged), `SteamGridDB.Xbox/Services/**`.

### Finding #3 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations has to be kept in sync by hand - the same style-resource assignment, the same `XamlRoot` API-contract check, the same `TryBeginLibraryOperation`/`EndLibraryOperation` guard wiring - and nothing enforces that a future fourth operation (or an edit to one of the three) follows the same shape.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (current lines `742-786`, `788-822`, `824-858`) each build a `ContentDialog` with the same four style-resource lookups and the same `Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent` `XamlRoot` check, call `ShowAsync`, branch on the result, and wrap the actual operation in the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` pattern. Re-confirmed unchanged this loop (these three methods sit above this loop's own insertion point, so line numbers did not shift).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:742-786`, `:788-822`, `:824-858`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action instead of re-deriving the whole confirm-guard-run shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as Finding F-002, in the same file; combined the two clusters account for roughly 225 of `PrimaryWidget.xaml.cs`'s 2020 lines being ceremony repeated 3-4x rather than owned once. Three loops queued without action.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `ConfirmAndRunAsync(string title, string content, string primaryText, string secondaryText, Func<ContentDialogResult, bool> shouldRun, Func<Task> action)` (or the smallest signature that covers the 2-button and 3-button cases) that owns dialog construction, the `XamlRoot` check, and the `TryBeginLibraryOperation`/`EndLibraryOperation` wrapping; each handler becomes a short call naming its own title/content/action.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml`, `SteamGridDB.Xbox/Services/**`.

### Finding #4 (stable_id F-004): TileImage.FillsTileAsync's alpha and corner-count thresholds are untested at their exact boundary values

**Why it matters** — A boundary-flip mutation at either threshold (`<` to `<=`) would ship silently: the corner-transparency gate that keeps case-mockup art off tiles would become off-by-one permissive or strict with no test failing.

**What is wrong** — `FillsTileAsync` (`TileImage.cs:231-265`) treats a pixel transparent when `alpha < 64` (`:250`) and treats a corner as transparent when more than 14 of its 36 sampled pixels are transparent, then rejects the image when 2 or more of its 4 corners are transparent (`transparentCorners < 2`, `:263`). `TileImageTests` exercises fully-opaque and fully-transparent corners but not alpha exactly at 64 or a candidate with exactly 2 transparent corners, so a mutation at either boundary is invisible to the suite. Re-read this loop (`TileImage.cs`/`TileImageTests.cs` unaffected by this loop's edits); gap re-confirmed unchanged.

**Evidence** — `SteamGridDB.Xbox/Services/Artwork/TileImage.cs:250`, `:263`

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None - test-only addition.

**Locality impact** — Contained to the test file.

**Metric signal, if any** — none

**Why this weakens submission** — A minor, off-primary-flow gap (per the Test strategy dimension's own anchor language, an off-path helper boundary is Cosmetic on its own) but worth naming before it is mistaken for full coverage.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Add two `TileImageTests` cases: a corner at exactly alpha 63/64, and an image with exactly 2 (not 0, not 4) transparent corners, asserting the documented boundary (`< 64` transparent, `< 2` corners passes).

**Blast radius** — Change: `SteamGridDB.Xbox.Tests/TileImageTests.cs`. Avoid: `SteamGridDB.Xbox/Services/Artwork/TileImage.cs` (test-only addition, no production change).

## Simplification Check
- Structurally necessary: Finding F-007's session-liveness guard closes a real, evidenced display-corruption/mis-ranking path nested inside `PopulateGridSelectionPanelAsync`'s own await (no architectural test in the deletion/seam sense applies - this is a state-ownership fix, matching Findings F-001/F-005/F-006's own categorization).
- New seam justified: No new Seam introduced. **Explicitly evaluated this loop per the standing directive to consider the shared-guard-mechanism alternative honestly**: a `SessionGuard` type wrapping `gridPanelSessionId`/`searchPanelSessionId` (now 7 total 1-3-line idiom instances across the two fields: `gridPanelSessionId` has 1 increment site + 3 check sites after this loop's fix, `searchPanelSessionId` has 2 increment sites + 1 check site). Rejected on two SPT questions: **Q2 (smallest honest fix)** - the natural fix for this loop's own new gap reuses the existing `gridPanelSessionId` field and the `sessionId` parameter already threaded through; zero new field, zero new type was needed, so a shared type would not have made this loop's own fix smaller or simpler. **Q3 (duplicate layer)** - a `SessionGuard.IsCurrent(session)` wrapper (`!gridPanelSession.IsCurrent(session)`, 36 characters) is not shorter, clearer, or more honest than the raw comparison it replaces (`session != gridPanelSessionId`, 30 characters); it adds an indirection layer over an idiom already at its simplest expressible form in C#, buying no Leverage (nothing reused isn't already trivially copy-pasteable) and no Locality (the two fields still need two separate instances either way). Unified Seam Policy does not apply either way (no new Seam - this is a plain value-type deduplication question, not a port/adapter question). Rejected again, now with a stronger data point (7 call sites, not the 2 fields' worth loop 3 had) than loop 3's own rejection, and the conclusion is unchanged: the multiplying instance count is a simplicity/duplication signal on the *surrounding* methods needing the guard (already tracked as F-002/F-003's shallow-module territory in the same file), not evidence the guard idiom itself needs abstracting.
- Helpful simplification: none this loop (Findings F-002/F-003 remain queued, not implemented, third loop running).
- Should NOT be done: Do not build the shared `SessionGuard` type (see above). Also considered and rejected this loop: adding an equivalent per-row reentrancy guard to `RestoreBackup_Click`/`RestoreBackupCoreAsync`. Walked this loop (`PrimaryWidget.xaml.cs:1867-1980` pre-fix region, unaffected by this loop's edit): `RestoreBackupCoreAsync` mutates properties on `EntriesSharingImage(game)` - a specific `GameEntry` object passed as a parameter, not an ambient "current selection" field - and `ArtworkFiles.RestoreOriginalAsync` (`ArtworkFiles.cs:160-191`, re-read this loop) is race-tolerant by construction: it locates the backup file before doing anything destructive, so a second concurrent restore attempt for the same game finds its backup already renamed away and fails closed into a caught, reported `BackupMissing` rather than corrupting data. A double-click race here degrades to a confusing status-text flash for that one row, never a wrong-game write or persistent corruption - a materially smaller blast radius than F-001/F-005/F-006/F-007's shape, and adding a guard for a self-correcting, already-caught race would be ceremony without proportional harm to justify it. Not promoted to a Finding this loop; noted here so the sweep's absence of a fifth Finding reads as a decision, not an oversight (see Scorecard humility check).
- Tests after fix: None added or deleted - `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface and cannot be unit-tested in the current infra, matching Findings F-001/F-005/F-006's fixes. Verification is: full build (exit 0) + full test suite (138/138 unchanged) both re-run after the change, plus a manual trace confirming the guard sits after the method's only await and before every subsequent mutation (`CurrentSelectedGame` read, `GridImagesView.Items.Add`, `GridPanelStatus.Text` write). This is the `reasoning_only` evidence path (Meta-Rule 4) for the local-UI-ownership invariant; the no-network-call-touched half is directly inspectable (this method makes no network call at all - grids/icons arrive pre-fetched as parameters from its caller).

## Improvement Backlog
1. **Collapse the four-times-duplicated panel slide animation (Finding F-002).**
   - why it matters: removes ~120 lines of repeated ceremony from the largest, most-churned file; the two Show/Hide pairs have already drifted (200ms vs 250ms) once, showing the duplication is actively costing consistency, and it has now been queued unfixed for three full loops.
   - score impact: `simplicity +0.5`
   - simplification
   - helpful

2. **Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003).**
   - why it matters: removes ~105 more lines of repeated ceremony from the same file; combined with F-002 this is the largest remaining simplicity gap in the codebase, also queued unfixed for three full loops.
   - score impact: `simplicity +0.5; framework_idioms +0.5`
   - simplification
   - helpful

**Priority-1 accounting**: F-007 (loop_local F1) is Priority 1 on severity (Serious deduction, the only finding at that severity this loop - F-002/F-003 are Noticeable, F-004 is Cosmetic) and on distance-to-target under a reweighted Backlog Prioritization Pass reading: criterion 1 (distance to target) nominally favors `test_strategy` (6.5, lowest score on the board), but its only candidate (F-004) is Cosmetic and off-primary-flow, so fixing it would not proportionally advance `test_strategy`'s anchor, which is capped by `PrimaryWidget.xaml.cs`'s structural untestability regardless of what `TileImage.cs`'s test file contains - a Cosmetic, off-path fix does not buy the kind of anchor progress criterion 1 rewards. F-007, by contrast, closes a Serious, source-proven gap on `state_management`/`concurrency` (7.0/6.5 pre-fix), dimensions where a fix genuinely moves the anchor. Unlike F-002/F-003, F-007 required no bias-check override for a fourth consecutive loop landing in the picker/session area: this is not proximity bias, because F-007 is a materially different defect shape than F-001/F-005/F-006 (nested inside a callee a prior fix already called, not a fresh top-level entry point), discovered via this loop's first-ever full enumeration of every `private async` method in the file (32 methods, grep-verified) rather than incidental discovery while auditing something else. The rejected alternative was F-002 (simplicity, tied for second-lowest distance-to-target after test_strategy): F-002 does not fail the Simplify Pressure Test - it remains a sound, ready fix, queued a third loop running - but Backlog Prioritization criterion 3 (severity) breaks the tie decisively in F-007's favor (Serious beats Noticeable), consistent with loops 2 and 3's own reasoning. The systemic-mechanism alternative (a shared `SessionGuard` type replacing the two hand-rolled fields) was evaluated per this loop's explicit mandate to consider it, and is rejected in the Simplification Check above on SPT Q2 and Q3, not silently passed over.

## Deepening Candidates
1. **Candidate Module**: `PrimaryWidget`'s panel slide-animation ceremony (the four bodies in Finding F-002).
   - Source friction proven: Finding F-002 - four near-identical `DoubleAnimation`/`Storyboard` bodies, one already-observed drift (200ms vs 250ms) between the pairs, re-confirmed unchanged this loop.
   - Why the current Interface is shallow or misplaced: there is no Interface at all - each Show/Hide method inlines its own animation construction; Interface ≈ Implementation four times over (Shallow module test).
   - What behavior should move behind the deeper Interface: the `DoubleAnimation` + `Storyboard` construction, `Begin()`, and matching `Task.Delay`, parameterized by transform/from/to/duration/easing.
   - Dependency category: `in-process`
   - Test surface after the change: none - `PrimaryWidget.xaml.cs` is UWP-page-bound and untestable in the current infra either before or after this extraction; verified by build + manual trace, same as Findings F-001, F-005, F-006 and F-007's fixes.
   - Smallest first step: extract `private async Task SlidePanelAsync(FrameworkElement transform, double from, double to, int durationMs, EasingMode mode)` and replace all four call sites.
   - What not to do: do not introduce an `IAnimator`/`IPanelController` protocol or a separate animation-coordinator class - this is private in-process UI glue with one production caller-family; Unified Seam Policy's two-adapter rule would fail immediately, and single-Adapter policy/failure/platform-isolation justification doesn't apply either. Inline extraction only.

## Builder Notes

1. **Pattern: a caller's session check protects only the mutations after its own check-point - not the mutations inside a callee that runs an await of its own.**
   - How to recognize: when method A checks `session != field` and then calls method B, and B itself contains an `await`, the check in A does NOT protect anything B does after B's own await. Grep every method reached from an already-guarded caller for a second, nested `await` followed by a mutation, not just the caller's own awaits.
   - Smallest coding rule: a session/generation guard protects code textually after its own check, in its own stack frame - it does not propagate through a call into a callee's own suspension points. Each method with its own await needs its own check, even when its only caller is already guarded.
   - Stack example: `LoadGridSelectionAsync`'s guard (loop 2, Finding F-005) sits before it calls `PopulateGridSelectionPanelAsync`; `PopulateGridSelectionPanelAsync`'s own `await AppliedArtworkStore.GetAsync(...)` was left unguarded until this loop, even though its only caller was already "fixed."

2. **Pattern: the same async-population-without-a-liveness-guard defect recurring across sibling methods in one file, because a fix scoped to the finding's own call site does not audit every other method - or every callee an already-fixed method reaches - with the same shape.**
   - How to recognize: once one async UI-population method is found missing a session/generation guard, enumerate every `private async (void|Task)` method in the class (a full grep, not a sample) and check each for (a) triggering by a user action not gated by "an equivalent operation is already in flight," and (b) an `await` followed by a write to an `ItemsControl`/list/status field. This loop did that enumeration for the first time (32 methods) rather than checking only the methods adjacent to the finding under investigation.
   - Smallest coding rule: fixing one instance of a defect class is not the same as auditing the class, and auditing only the methods near a known instance is not the same as auditing the whole class either.
   - Stack example: `PerformGameSearchAsync` (loop 3, Finding F-006) has exactly one await total, so no nested gap was possible there; `PopulateGridSelectionPanelAsync` has two awaits (`AppliedArtworkStore.GetAsync`, and its caller's own network awaits) - the second one was the gap this loop closed.

3. **Pattern: a registry occurrence recorded with a placeholder resolution sha (`<pending>`) at commit time, because the sha of the commit being written cannot be known before it is made, then never backfilled.**
   - How to recognize: check the prior loop's `findings_registry.json` for any `"sha": "<pending>"` on a `resolved` occurrence before writing this loop's own registry update.
   - Smallest coding rule: backfill a pending sha as part of the current loop's own registry write (it already touches the file and lands in the same commit) rather than leaving it stale indefinitely or spending a separate bookkeeping commit on it.
   - Stack example: this loop corrected F-006's loop-3 occurrence from `"sha": "<pending>"` to loop 3's actual commit sha (`3b7ab3a7117661ac3b595db3f6bab7b15c88226c`) as part of this loop's own registry write - no extra commit. This loop's own F-007 occurrence is recorded `<pending>` in turn, for loop 5 to backfill.

**Stalled-Dimension Sweep (loop 4 - fires for the first time; triggers on any dimension below 9.5 with `delta == SAME` for 3+ consecutive loops per `REVIEW_HISTORY.json`):**
- `architecture_quality` — named candidate: Finding F-007 (routed above; G11 holds, only the ID appears here).
- `domain_modeling` — explicit clean: walked `Models/` in full (`GameEntry.cs`, `GamePlatform.cs`, `GridImageItem.cs` - 3 types); only known concern (`GameEntry`'s 3-property parallel-fields case) unchanged, no live harm, not promotable.
- `data_flow` — explicit clean (with a falsified candidate): walked `StoreNameLookup.cs` + `FixLog.cs` call graphs to their only entry points; a helper-flagged "unsynchronized dictionaries" candidate traced to unreachable-concurrently (both gated by `isLibraryOperationRunning`) and rejected.
- `framework_idioms` — named candidate (carried): Finding F-002 remains the only candidate, still queued (Priority 2), not implemented, third loop running.
- `simplicity` — explicit clean: swept `Services/Artwork`, `Services/Library`, `Services/SteamGridDB`, `Services/Stores`, `Models/` across Reuse/Simplification/Altitude/Efficiency angles; nothing beyond the already-tracked F-002/F-003/data_flow/domain_modeling items.
- `test_strategy` — named candidate (carried, evaluated and does not win Priority 1): Finding F-004 walked, still the only candidate, Cosmetic/off-primary-flow, does not advance distance-to-target since the dimension's ceiling is set by untestable production code, not this test file.
- `credibility` — explicit clean: re-read `TESTING.md:47-56` in full; framing unchanged, still accurate, still under-describing risk (4th confirming instance this loop).

**Scorecard humility check** — three claims here are the ones most likely to be argued the other way: (1) the DOWN move for `state_management`/`concurrency` (7.5→7.0, 7.0→6.5) rather than SAME - a reviewer following loops 2-3's own "net wash" precedent literally (one closed, one found, in the same loop) could argue SAME is more internally consistent with prior practice, and that DOWN over-corrects for a change in *this loop's* methodology (a genuinely exhaustive sweep) rather than the code itself getting worse. (2) `architecture_quality` held at SAME rather than also moving DOWN, despite F-007 living squarely in `PrimaryWidget`'s ownership territory - a stricter reviewer could argue the recursive nature of the gap (a fix's own callee left unaudited) is itself an architecture_quality concern about module/interface boundaries not actually enforcing what they appear to, not purely a state_management/concurrency one, and that the double-counting risk loop 1's own humility check flagged wasn't avoided so much as reassigned to a different pair of dimensions. (3) `RestoreBackup_Click`'s missing per-row guard, walked and explicitly rejected as a Finding this loop on smaller-blast-radius grounds - a stricter reviewer could hold that "self-correcting via a caught exception and a confusing status flash" is still a real, source-backed reliability gap that should have been at least a Cosmetic-severity Finding rather than folded into the Simplification Check's prose, since the rubric's own Finding budget (3-5, up to 7) had room for a fifth entry this loop.

## Final Judge Narrative
Place, not win, this loop. Runtime ownership is NOT more trustworthy this loop by the plain net-wash accounting loops 2-3 used - it is, on this loop's own honest reassessment, less trustworthy than SAME implied: the fourth instance of the identical shape lived inside a callee a prior loop's own fix already called, proving entry-point-level session checks do not compose across call boundaries. This loop closes that specific gap and, for the first time across four loops, backs the fix with a genuinely exhaustive sweep (every `private async` method in the 2020-line file enumerated and checked for the shape), which is the systemic-prevention step loop 3's own Builder Note demanded but did not itself perform. Concurrency and state_management moved DOWN, not SAME, because this loop's own investigation is the basis for judging loop 3's SAME assessment as having under-priced the residual, not because the code regressed since loop 3 - nothing regressed; a deeper-nested, pre-existing hazard was found by more rigorous review. Simplification did not happen this loop on the F-002/F-003 axis - both remain queued for a third full loop, correctly outranked again by F-007's higher severity, not by proximity bias (F-007 is a materially distinct defect shape, found via exhaustive enumeration, not incidental discovery in the same neighborhood). The systemic shared-guard-mechanism alternative was evaluated per this loop's explicit mandate and rejected on SPT Q2/Q3 - the natural fix for the new gap needed zero new type. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; this loop's only regression evidence is full build + full suite (138/138 unchanged) plus a manual trace of the new guard's placement relative to its one await. Future work risks over-engineering only if F-002/F-003's eventual extraction reaches for a coordinator abstraction instead of a private helper method, or if a future loop still tries to wrap the two hand-rolled session fields in a shared type for what remains a set of small, honestly-duplicated 1-3 line idioms - unchanged guidance from loop 1's own Deepening Candidate, now re-affirmed a fourth time against a nested instance too.

## Loop 4 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (12 insertions, 0 deletions): added a session re-check inside `PopulateGridSelectionPanelAsync`, immediately after its own await (`AppliedArtworkStore.GetAsync`) and before any read of `CurrentSelectedGame` or mutation of `GridImagesView.Items`/`GridPanelStatus`: `if (sessionId != gridPanelSessionId) { return; }`. Reuses the existing `gridPanelSessionId` field and the `sessionId` parameter already threaded through from `LoadGridSelectionAsync` - no new field, no new type. This method makes no network call itself (grids/icons arrive pre-fetched as parameters from its caller), so no network call count, ordering, payload, or error handling changes anywhere in the flow - the fix is purely a local UI-mutation skip for a session no longer live. Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected, since `PrimaryWidget.xaml.cs` is not part of the test-linked `Services/**` surface. Finding F1 (stable_id F-007) is **resolved**: the specific display-corruption/mis-ranking path (a stale, superseded picker session's population landing after a newer session's own claim on the panel) is closed by construction, verified by direct inspection of the diff (the guard sits textually immediately after the method's only await and before every subsequent read/mutation) and by re-reading the final source. This loop additionally performed the first exhaustive enumeration of every `private async void`/`private async Task` method in `PrimaryWidget.xaml.cs` (32 methods via grep) and checked each for the same awaited-then-mutate-without-recheck shape; no further instance was found (`RestoreBackup_Click`/`RestoreBackupCoreAsync`'s own, narrower, self-correcting race was walked and explicitly not promoted - see Simplification Check). No unintended scorecard regression: the change touches no network call, no ranking/selection logic beyond skipping it for a stale session, and no file outside the one named. Findings F-002, F-003 and F-004 are carried forward, unchanged in substance (evidence line numbers corrected against current source for F-002 only, shifted +12 by this loop's own insertion), to the Improvement Backlog / Findings for future loops.
