### Loop Counter
Loop 5 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
**Functionally solid, but structurally compromised.**

This loop ran a genuinely independent re-derivation from source (full re-read of `PrimaryWidget.xaml.cs`, all Services/Models files, and the three prior-audit docs), plus two fresh-eyes helper sweeps briefed on the codebase's history but not its prior conclusions. One of those sweeps found a fifth instance of the recurring "stale async completion mutates shared picker UI state" defect — in `DownloadAndReplaceImageAsync`, a fresh top-level-reachable method none of the four prior fixes (F-001, F-005, F-006, F-007) touched, and one loop 4's own "exhaustive" 32-method sweep should have caught but did not. This loop closes that specific gap and, for the first time, backs the closure with two independent verification passes rather than one self-directed sweep.

## Scorecard (1-10)

- Architecture quality: 7.5 | DOWN | `PrimaryWidget.xaml.cs` is now 2031 lines (2020 pre-fix; +11 from this loop's own guard clause), still carrying Findings F-002/F-003's duplicated ceremony (still open, current lines 742-858 and 1571-1865) plus this loop's newly-found-and-now-fixed F-008. Moved DOWN, distinct from the state_management/concurrency deductions below: this is a judgment about the *enforcement mechanism* for "who may still write to the picker UI," not about the live hazard itself. That mechanism is a manually-repeated convention (a `session != field; return;` idiom copy-pasted at each async method) with no single Module owning it — architecture_quality's own 9-anchor language is explicit that a convention-enforced property, as opposed to one "enforced by source," does not clear the bar. This loop's fifth instance, found despite two consecutive loops each claiming an exhaustive sweep, is fresh, source-backed evidence that the convention is not reliably self-auditing. (Loop 4's own Scorecard humility check flagged this exact ambiguity — whether the recurring gap is an architecture_quality concern, not purely state_management/concurrency — as arguable; this loop resolves it in the stricter direction on the strength of a fifth confirmed instance.)
- State management and runtime ownership: 6.5 | DOWN | F-005/F-006/F-007's own fixes independently re-verified this loop as still holding at their own call sites (re-read in full; unchanged). But this loop's fresh full-file re-read plus an independent helper sweep found a fifth instance of the identical shape in `DownloadAndReplaceImageAsync` (`PrimaryWidget.xaml.cs:1523-1555` pre-fix) — reached from `GridImage_Click`, a fresh top-level entry point none of the four prior fixes touch, not nested inside an already-fixed callee the way F-007 was. Moved DOWN, mirroring loop 4's own precedent for this exact situation: the code did not regress since loop 4, but this loop's own investigation is the basis for judging loop 4's "exhaustive, nothing beyond F-007" claim as having under-priced the residual — two consecutive loops now have made a completeness claim that a subsequent loop's fresh read falsified. Fixed this loop; see Loop 5 Result.
- Domain modeling: 8.5 | SAME | `Models/GameEntry.cs` and `Models/GamePlatform.cs` re-read in full this loop, unchanged. `GameEntry.cs`'s `OfficialCapsuleUrl`/`SteamGridDbGameId`/`HasSteamGridDBMatch` three-independently-settable-properties case (the sole construction site, `PrimaryWidget.xaml.cs:650-664`, unaffected by this loop's edit) remains the only known concern, still no live harm, not promoted to a Finding. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME):** explicit clean — re-read both files in full; nothing beyond the already-tracked case.
- Data flow and dependency design: 7.5 | SAME | `Services/*` re-confirmed zero `Windows.UI.Xaml` imports by grep this loop. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME, with a falsified candidate):** a helper sub-agent surfaced `SteamGridDbClient.CapsuleParseNotes` (`SteamGridDbClient.cs:47-55`, an unsynchronized static `List<string>` with a check-then-add `Count < 5` guard) as a candidate race. Independently traced this loop: `NoteCapsuleParse`'s only call chain is `ParseOfficialCapsuleUrl` ← `GetGameByPlatformIdAsync` ← `LoadGameEntriesAsync`'s sequential per-entry `foreach` (never itself reentrant — one `await` completes before the next iteration starts); `CapsuleParseNotes`'s only read site is `FixLibraryAsync` (`PrimaryWidget.xaml.cs:951`). Both `LoadGameEntriesAsync` and `FixLibraryAsync` are reachable only through the same `isLibraryOperationRunning` gate (`TryBeginLibraryOperation`/`EndLibraryOperation`) that already falsified an equivalent "unsynchronized dictionaries" candidate in loop 4 — write and read paths can never overlap. Falsified; same shape and same conclusion as loop 4's own falsified candidate.
- Framework / platform best practices: 8.0 | SAME | `SteamGridDbClient.cs:136-142`'s `DataContractJsonSerializer`/`Windows.Data.Json` split re-verified this loop, unchanged. `SteamGridDbClient.cs:273-298`'s `BuildUrl` helper (consolidating what `CODE-REVIEW.md` documents as a six-times-duplicated URL builder, now a single method) re-verified fixed and holding. Deduction unchanged: `PrimaryWidget.xaml.cs:1571-1865` (Finding F-002, still open) — the four-times duplicated `DoubleAnimation`/`Storyboard` ceremony. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME):** F-002 is this dimension's named candidate; outranked again this loop by F-008's higher severity, fourth loop running.
- Concurrency and runtime safety: 6.0 | DOWN | Same evidence as state_management. F-008 (Serious deduction, same tier as F-005/F-006/F-007 — cannot write artwork to the wrong game, since `CurrentSelectedGame` is read into `DownloadAndReplaceImageCoreAsync`'s `game` parameter by value before any await in the method, so the actual write always lands on the game selected at click time; only the panel's own in-memory display, status text, and selected-game field can be corrupted for an unrelated live session) is this loop's fifth discovery of the identical async-population-without-a-recheck shape. Moved DOWN, not SAME: mirrors loop 4's own reasoning for this exact recurrence pattern — the residual was larger than either loop 4's or this loop's starting confidence assumed, because a genuinely fresh, top-level-reachable method was still missed by a prior "exhaustive" sweep. Fixed this loop, backed by two independent verification passes (a fresh full-file re-read by this loop plus an independent helper sweep that specifically re-tested loop 4's completeness claim and found the gap) — the strongest verification basis of any of the five fixes so far.
- Code simplicity and clarity: 7.0 | SAME | `PrimaryWidget.xaml.cs:742-858` and `:1571-1865` (Findings F-003, F-002 — still open, ~225 lines of 3-4x duplicated ceremony, re-confirmed this loop at current line numbers) unchanged against otherwise-minimal Modules. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME):** a helper sub-agent swept `Services/Artwork`, `Services/Library`, `Services/SteamGridDB`, `Services/Stores` and `Models/` this loop across Reuse/Simplification/Altitude/Efficiency angles and found nothing beyond the already-tracked `GameEntry` parallel-fields case (domain_modeling territory) and the falsified `CapsuleParseNotes` candidate (data_flow territory, above) — every file it read reported an explicit clean.
- Test strategy and regression resistance: 6.5 | SAME | `PrimaryWidget.xaml.cs` remains untestable outside an app container — re-confirmed this loop via both `.csproj` target frameworks (`SteamGridDB.Xbox.csproj`: `TargetPlatformIdentifier=UAP`; `SteamGridDB.Xbox.Tests.csproj`: `TargetFramework=net8.0-windows10.0.19041.0`, a desktop projection that cannot compile a UWP page). This loop's own investigation found a *fifth* independent concurrency/state-ownership defect (F-008) on that exact untestable surface via source reasoning alone, in a location two prior "exhaustive" sweeps did not catch — re-demonstrating, not further disproving, the anchor's own disqualifying language. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME, the lowest-scored dimension on the board):** `TileImageTests.cs` re-walked (Finding F-004, re-verified this loop, still no case at the exact `alpha == 64` or `transparentCorners == 2` boundary) — the only candidate this dimension has ever had, Cosmetic severity and off-primary-flow, does not win Priority 1: the dimension's ceiling is set by `PrimaryWidget.xaml.cs`'s structural untestability, not by what `TileImage.cs`'s test file contains.
- Overall implementation credibility: 7.5 | SAME | The `gridPanelSessionId`/`searchPanelSessionId` field comments continue the codebase's documented-rationale discipline; this loop's own `DownloadAndReplaceImageAsync` fix follows the identical idiom and comment style. Deduction unchanged: `TESTING.md:47-56`'s framing of the untested UWP surface continues to undersell the surface's real risk, now demonstrated a fifth time by this loop's own F-008 discovery. **Stalled-Dimension Sweep (loop 5, 4th consecutive SAME):** re-read `TESTING.md` in full this loop against current `.csproj` files; framing verbatim unchanged from loop 1's own citation and re-confirmed accurate.

## Authority Map
Re-emitted this loop: an authority-related finding, F-008, is Priority 1.

- Concern: **Selected game for the artwork picker (`CurrentSelectedGame`) - write path**
  - Owner: `PrimaryWidget`
  - Allowed writers: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (nulls on close)
  - Readers: `LoadGridSelectionPanelAsync`, `PopulateGridSelectionPanelAsync`, `GridImage_Click`, `DownloadAndReplaceImageAsync` (reads once, by value, before its own await), `ShowSearchPanelAsync`
  - Persistence seam: none (in-memory UI field)
  - Async mutation entry points: `EditGameImage_Click`, `SearchGameImage_Click`, `HideGridPanelAsync` (indirectly, via a stale `DownloadAndReplaceImageAsync` completion, pre-fix)
  - Verdict: **Single and clear** - `CurrentSelectedGame`'s own write sites were never the problem; F-008's hazard was `HideGridPanelAsync` being invoked from a stale context that no longer owned the panel, not a second writer to the field itself.

- Concern: **Grid picker panel display contents (`GridImagesView.Items` / `GridPanelStatus` / `GridLoadingRing` / panel visibility during population and post-download teardown)**
  - Owner: `PrimaryWidget` (`LoadGridSelectionAsync` / `PopulateGridSelectionPanelAsync` / `DownloadAndReplaceImageAsync` / `HideGridPanelAsync`)
  - Allowed writers: `LoadGridSelectionAsync` (Clear, status text, loading ring), `PopulateGridSelectionPanelAsync` (Add - guarded since loop 4/F-007), `DownloadAndReplaceImageAsync` (status text, triggers `HideGridPanelAsync` - guarded since this loop/F-008)
  - Readers: user (visually), `GridImage_Click` (via `SessionId` comparison)
  - Persistence seam: none
  - Async mutation entry points: `LoadGridSelectionAsync` (one invocation per Edit/Search-result click), `DownloadAndReplaceImageAsync` (one invocation per tile click)
  - Verdict: **Split and ambiguous (pre-fix)** - see Finding F-008. `GridImage_Click`'s own session check (established loop 1, F-001) sits before calling `DownloadAndReplaceImageAsync`, not inside it; this loop's fix adds the equivalent re-check inside `DownloadAndReplaceImageAsync` itself, after its own await and before the status-text write and the `HideGridPanelAsync` call. Re-audit next loop once the fix has a full loop's scrutiny, matching the cadence applied to F-005/F-006/F-007.

- Concern: **Search results panel display contents (`SearchResultsListView.Items` / `SearchPanelStatus` / `SearchLoadingRing` during a name search)**
  - Owner: `PrimaryWidget` (`PerformGameSearchAsync` / `ShowSearchPanelAsync`)
  - Allowed writers: `PerformGameSearchAsync` (Clear, Add, status text, loading ring), `ShowSearchPanelAsync` (Clear, header/box text)
  - Readers: user (visually), `SearchResult_Click`
  - Persistence seam: none
  - Async mutation entry points: `PerformGameSearchAsync`, `ShowSearchPanelAsync`
  - Verdict: **Single and clear** - re-verified this loop: `PerformGameSearchAsync` has exactly one `await` in its whole body and the existing session check sits immediately after it and before every mutation; no equivalent "download and auto-close" tail exists on the search flow the way it does on the grid flow, so F-008's shape has no search-side counterpart.

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
  - Verdict: **Single and clear** - unchanged since loop 2; not re-walked line-by-line this loop, carried forward as background context.

- Concern: **Store-name / capsule-parse / fix-log ambient state (`StoreNameLookup`'s three dictionaries, `SteamGridDbClient.CapsuleParseNotes`, `FixLog`'s fields)**
  - Owner: `StoreNameLookup` / `SteamGridDbClient` / `FixLog` (static Modules)
  - Allowed writers: `GetOrFetchGogNameAsync`, `GetOrFetchEpicNameAsync`, `FindGameByNameAsync` (unlocked dictionaries), `NoteCapsuleParse` (unlocked list), `FixLog.Start`/`Write` (unlocked list+fields)
  - Readers: same methods, `FixLibraryAsync` (reads `CapsuleParseNotes`)
  - Persistence seam: none
  - Async mutation entry points: `LoadGameEntriesAsync`'s per-entry loop; `FixLibraryAsync`
  - Verdict: **Single and clear** - independently re-traced this loop for `CapsuleParseNotes` specifically (see data_flow proof above): reachable only through the same gated entry points sharing `isLibraryOperationRunning`, so it can never run concurrently with itself or with `FixLog`/`StoreNameLookup`'s equivalents. A helper-surfaced candidate on this exact field was evaluated and falsified.

## Strengths That Matter
- `ArtworkDownloader.cs`'s `DownloadBestTileFillingImageAsync` / `FindOfficialLookalikeAsync` / `PassesColourAndLayoutGate` / `ChosenAlreadyMatchesOfficialArt` split (`ArtworkDownloader.cs:71-220`, re-read in full this loop) is a genuinely deep Module: the five-step selection-and-veto pipeline is documented with the specific graded incident (`officialArtworkFloor`'s doc comment cites "Mad Max at 0.51") that calibrated each threshold, the two gate predicates are extracted as pure, independently-testable functions (`ArtworkDownloaderTests.cs` exercises them directly), and no caller has to understand the pipeline's internals to use it.
- `ArtworkRanker.RankGrids` (`ArtworkRanker.cs:189-205`, re-read this loop) computes each grid's ranking signals exactly once via the private `RankedGrid` wrapper (`:151-182`), rather than recomputing `GridMetadata`'s three regex passes per sort-key access - a genuine fix for `CODE-REVIEW.md`'s finding #10 ("recomputed three times per grid on every ranking pass"), independently re-verified this loop as still holding.
- `StoreNameLookup`'s Ubisoft cache (`StoreNameLookup.cs:40-42`, re-read this loop) now uses the shared `AsyncLazyCache<T>` instead of a fourth hand-rolled check-then-populate implementation, and the doc comment at `:36-39` explains why: "the exact shape `EpicLibrary`'s and `AppliedArtworkStore`'s own caches have, so this uses their same `AsyncLazyCache<T>` instead of a fourth hand-rolled copy of the same check-then-populate logic" - a real Locality win, not a renamed wrapper.

## Findings

### Finding #1 (stable_id F-008): DownloadAndReplaceImageAsync's post-download UI mutations are not re-checked against a superseded picker session

**Why it matters** — A user who clicks a grid tile to download artwork, then opens the artwork picker again for a different game before that download completes, can have the new session's picker panel unexpectedly closed, its tile list cleared, and `CurrentSelectedGame` nulled out by the stale download's completion — even though that stale download has nothing to do with the new session.

**What is wrong** — `DownloadAndReplaceImageAsync(GridImageItem gridItem)` (`PrimaryWidget.xaml.cs:1523-1555` pre-fix) is reached from `GridImage_Click`, which checks `gridItem.SessionId == gridPanelSessionId` before calling it — but that check does not cover an await *inside* `DownloadAndReplaceImageAsync` itself: `bool success = await DownloadAndReplaceImageCoreAsync(CurrentSelectedGame, gridItem.Url, true, gridItem.Id)` (line 1531 pre-fix) is a genuine suspension point (network download, file write, `AppliedArtworkStore` update), and nothing re-checked the session after it returned. If a newer `LoadGridSelectionAsync` call (a different Edit/Search click) started and bumped `gridPanelSessionId` while this await was pending, the resumed method still wrote `GridPanelStatus.Text` for the live session's panel and, on success, awaited `Task.Delay(250)` then called `HideGridPanelAsync()` — which collapses the panel, clears `GridImagesView.Items`, and nulls `CurrentSelectedGame`, all for a session no longer live.

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1523-1555` (pre-fix, no session re-check after its own await), `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1512-1518` (`GridImage_Click`'s own session check sits before calling `DownloadAndReplaceImageAsync`, not inside it), `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1586-1613` (pre-fix; `HideGridPanelAsync` collapses the panel, clears `GridImagesView.Items`, and nulls `CurrentSelectedGame` unconditionally)

**Architectural test failed** — n/a - different category (state-ownership/reentrancy defect, matching Findings F-001/F-005/F-006/F-007's own categorization)

**Dependency category** — n/a (not a Coupling & Leakage finding)

**Leverage impact** — None - correctness fix inside `DownloadAndReplaceImageAsync`'s own body, not a change to any caller-facing Interface; `GridImage_Click`'s call site is unchanged.

**Locality impact** — Fix stays entirely inside `DownloadAndReplaceImageAsync` (one guard clause reusing the existing `gridItem.SessionId` field and the `gridPanelSessionId` field); no other Module's behavior changes, and no network call is added, removed, or reordered.

**Metric signal, if any** — none

**Why this weakens submission** — This is the same "stale authority remains alive" hazard class Findings F-001, F-005, F-006 and F-007 already closed, found a fifth time — in a fresh top-level-reachable method none of the four prior fixes touched, proving the codebase's remaining untouched async handlers cannot be assumed safe merely because a sibling method nearby was already fixed, and that two consecutive loops' "exhaustive sweep" claims were each incomplete. Severity mirrors F-005/F-006/F-007: `CurrentSelectedGame` is read into `DownloadAndReplaceImageCoreAsync`'s `game` parameter by value before any await, so artwork can never be written to the wrong game — only the panel's own in-memory display, status text and selected-game field can be corrupted for an unrelated live session.

**Severity** — Serious deduction

**ADR conflicts** — none

**Minimal correction path** — Add the same guard clause already used at the other four sites (`if (gridItem.SessionId != gridPanelSessionId) { return; }`) immediately after the `DownloadAndReplaceImageCoreAsync` await and before any further mutation of `GridPanelStatus.Text` or the call to `HideGridPanelAsync`. Reuses the existing `gridItem.SessionId` field and the `gridPanelSessionId` field — no new field, no new type. This runs after the only await that matters in the method and before every further mutation, so it changes no network call, no call count, ordering, payload, or error handling elsewhere in the flow.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/Models/**`, `SteamGridDB.Xbox/Services/**`, `SteamGridDB.Xbox/PrimaryWidget.xaml`.

**Status this loop: implemented — see Loop 5 Result below.**

### Finding #2 (stable_id F-002): Grid and search panel slide animations duplicate the same Storyboard ceremony four times

**Why it matters** — A future change to the panel's slide timing or easing (or a bug in it) has to be made and verified in four places instead of one, and the four copies have already begun to drift (200ms hide vs 250ms show, independently re-derived each time).

**What is wrong** — `ShowGridPanelAsync`, `HideGridPanelAsync`, `ShowSearchPanelAsync` and `HideSearchPanelAsync` (current lines `1571-1592`, `1597-1624`, `1763-1831`, `1836-1865`) each hand-build a `DoubleAnimation` + `Storyboard` against a `TranslateTransform`, set From/To/Duration/EasingFunction, call `storyboard.Begin()`, then await `Task.Delay` matching the duration. The only real variation across the four is which transform, which direction, and 250ms vs 200ms. Re-confirmed unchanged this loop (line numbers shifted +11 by this loop's own `DownloadAndReplaceImageAsync` guard clause, which lands above all four bodies).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:1571-1592`, `:1597-1624`, `:1763-1831`, `:1836-1865`

**Architectural test failed** — Shallow module (each Show/Hide method's Interface ≈ its Implementation; no reuse across the four near-identical bodies)

**Dependency category** — n/a

**Leverage impact** — One call site to read/change instead of four; callers learn less, not more.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`; no other file's behavior changes.

**Metric signal, if any** — none

**Why this weakens submission** — Four near-identical bodies in the single largest, most-churned file in the codebase (`PrimaryWidget.xaml.cs`, 2031 LOC) is exactly the leaf-module duplication the Simplicity dimension's 9-anchor requires be swept for; it currently is not swept away. Four loops queued without action, correctly outranked each time by a higher-severity finding.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a private `SlidePanelAsync(FrameworkElement transform, double from, double to, int durationMs, EasingMode mode)` helper that builds the `DoubleAnimation`/`Storyboard` once and awaits `Task.Delay(durationMs)`; each of the four call sites becomes a one-line call. No new Seam, no new file - the four bodies collapse into the one Module (`PrimaryWidget`) that already owns them.

**Blast radius** — Change: `SteamGridDB.Xbox/PrimaryWidget.xaml.cs`. Avoid: `SteamGridDB.Xbox/PrimaryWidget.xaml` (markup unchanged), `SteamGridDB.Xbox/Services/**`.

### Finding #3 (stable_id F-003): Fix/Restore/Revert confirmation dialogs duplicate the same ContentDialog construction and guard-and-run ceremony three times

**Why it matters** — Each of the three destructive-operation confirmations has to be kept in sync by hand — the same style-resource assignment, the same `XamlRoot` API-contract check, the same `TryBeginLibraryOperation`/`EndLibraryOperation` guard wiring — and nothing enforces that a future fourth operation (or an edit to one of the three) follows the same shape.

**What is wrong** — `FixLibraryButton_Click`, `RestoreChangesButton_Click` and `RevertDefaultsButton_Click` (current lines `742-786`, `788-822`, `824-858`) each build a `ContentDialog` with the same four style-resource lookups and the same `Windows.Foundation.Metadata.ApiInformation.IsApiContractPresent` `XamlRoot` check, call `ShowAsync`, branch on the result, and wrap the actual operation in the same `TryBeginLibraryOperation`/try/finally/`EndLibraryOperation` pattern. Re-confirmed unchanged this loop (these three methods sit above this loop's own insertion point, so line numbers did not shift).

**Evidence** — `SteamGridDB.Xbox/PrimaryWidget.xaml.cs:742-786`, `:788-822`, `:824-858`

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Three call sites drop to naming their own title/content/action instead of re-deriving the whole confirm-guard-run shape.

**Locality impact** — Fully contained inside `PrimaryWidget.xaml.cs`.

**Metric signal, if any** — none

**Why this weakens submission** — Same leaf-module-duplication concern as Finding F-002, in the same file; combined the two clusters account for roughly 225 of `PrimaryWidget.xaml.cs`'s 2031 lines being ceremony repeated 3-4x rather than owned once. Four loops queued without action, correctly outranked each time by a higher-severity finding.

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
- Structurally necessary: Finding F-008's session-liveness guard closes a real, evidenced display-corruption path (unrelated live session's panel closed, tiles cleared, selected game nulled) caused by a stale `DownloadAndReplaceImageAsync` completion (no architectural test in the deletion/seam sense applies - this is a state-ownership fix, matching Findings F-001/F-005/F-006/F-007's own categorization).
- New seam justified: No new Seam introduced. Considered and rejected again this loop, now with a fifth data point: a shared `SessionGuard`/`PickerSessionToken` type wrapping `gridPanelSessionId`/`searchPanelSessionId`. Fails **Q1 (fixes real ambiguity)**: the actual failure mode across all five instances was never "the check is hard to write" (the check is a one-line comparison every time) - it was "a specific method's own suspension point was never given the check at all." A wrapper type does not change whether a future async method remembers to call it; forgetting to invoke `guard.IsCurrent(id)` is exactly as easy as forgetting to write `if (id != field) return;`. Also fails **Q3 (duplicate layer)** on the same textual-cost grounds loop 4 already established (`session != gridPanelSessionId`, 30 characters, is not shorter or clearer than a wrapper call). Unified Seam Policy does not apply (no new Seam - a plain value-type deduplication question). A second candidate mechanism was also considered and rejected: a `PopulateIfCurrentAsync(int sessionId, Func<Task> populate)` wrapper that checks once at entry. This fails for a different reason - `DownloadAndReplaceImageAsync` needs the check positioned *after* its own await and *before* a specific subset of its remaining statements (the `HideGridPanelAsync` tail), not at method entry; a single entry-check wrapper cannot express "guard this later segment, not the whole method" without becoming a multi-checkpoint coordinator, which is architecture costume for a five-instance, 1-3-line idiom.
- Helpful simplification: none this loop (Findings F-002/F-003 remain queued, not implemented, fourth loop running).
- Should NOT be done: Do not build a shared session-guard type or population-wrapper (see above). Also re-confirmed this loop: do not add a per-row reentrancy guard to `RestoreBackup_Click`/`RestoreBackupCoreAsync` — unaffected by this loop's edit, still self-correcting via `ArtworkFiles.RestoreOriginalAsync`'s race-tolerant backup-first ordering (re-read this loop, unchanged), still a materially smaller blast radius than F-001/F-005/F-006/F-007/F-008's shape.
- Tests after fix: None added or deleted - `PrimaryWidget.xaml.cs` is outside the test-linked `Services/**` surface and cannot be unit-tested in the current infra, matching Findings F-001/F-005/F-006/F-007's fixes. Verification is: full build (exit 0) + full test suite (138/138 unchanged) both re-run after the change, an independent fresh-eyes implementation review (separate subagent, read-only, verdict `approved`), plus a manual trace confirming the guard sits after the method's only await that matters and before every subsequent mutation (`GridPanelStatus.Text` write, `HideGridPanelAsync` call). This is the `reasoning_only` evidence path (Meta-Rule 4) for the local-UI-ownership invariant; the no-network-call-touched half is directly inspectable (the guard sits strictly after the only network/file-write call in the method, and does not alter that call's arguments, count, or error handling).

## Improvement Backlog
1. **Collapse the four-times-duplicated panel slide animation (Finding F-002).**
   - why it matters: removes ~120 lines of repeated ceremony from the largest, most-churned file; the two Show/Hide pairs have already drifted (200ms vs 250ms) once, showing the duplication is actively costing consistency, and it has now been queued unfixed for four full loops.
   - score impact: simplicity +0.5; framework_idioms +0.5
   - simplification
   - helpful

2. **Collapse the three-times-duplicated confirmation-dialog ceremony (Finding F-003).**
   - why it matters: removes ~105 more lines of repeated ceremony from the same file; combined with F-002 this is the largest remaining simplicity gap in the codebase, also queued unfixed for four full loops.
   - score impact: simplicity +0.5
   - simplification
   - helpful

**Priority-1 accounting**: F-008 (loop_local F1) is Priority 1 on severity (Serious deduction, the only finding at that severity this loop - F-002/F-003 are Noticeable, F-004 is Cosmetic) and this is the fourth consecutive loop that reasoning has correctly outranked F-002/F-003 despite their Stall (4 consecutive SAME loops on the dimensions they target). Named explicitly per the Backlog Prioritization Pass's actionability/stall criteria: this is not proximity bias or an accident of where the loop happened to look - F-008 was independently surfaced by a dedicated fresh-eyes helper sweep specifically tasked with re-testing loop 4's own "exhaustive, nothing beyond F-007" completeness claim, found in a method (`DownloadAndReplaceImageAsync`) none of the four prior fixes touch, and verified by this loop's own full-file re-read before being accepted as a finding. A Serious-severity, source-proven, safely-fixable defect on a primary user flow beats a Noticeable-severity, safely-fixable, four-loop-stalled simplification item on Backlog Prioritization criterion 3 (Severity) regardless of criterion 2 (Stall)'s tie-breaking role - Stall only breaks near-ties, it does not override a clear severity gap. If no further Serious-or-worse finding surfaces next loop, F-002 is the correctly-queued next pick (Priority 1 above) and should not be deferred a fifth time without a comparable severity justification.

## Deepening Candidates
1. **Candidate Module**: `PrimaryWidget`'s panel slide-animation ceremony (the four bodies in Finding F-002).
   - Source friction proven: Finding F-002 - four near-identical `DoubleAnimation`/`Storyboard` bodies, one already-observed drift (200ms vs 250ms) between the pairs, re-confirmed unchanged this loop.
   - Why the current Interface is shallow or misplaced: there is no Interface at all - each Show/Hide method inlines its own animation construction; Interface ≈ Implementation four times over (Shallow module test).
   - What behavior should move behind the deeper Interface: the `DoubleAnimation` + `Storyboard` construction, `Begin()`, and matching `Task.Delay`, parameterized by transform/from/to/duration/easing.
   - Dependency category: `in-process`
   - Test surface after the change: none - `PrimaryWidget.xaml.cs` is UWP-page-bound and untestable in the current infra either before or after this extraction; verified by build + manual trace, same as Findings F-001, F-005, F-006, F-007 and F-008's fixes.
   - Smallest first step: extract `private async Task SlidePanelAsync(FrameworkElement transform, double from, double to, int durationMs, EasingMode mode)` and replace all four call sites.
   - What not to do: do not introduce an `IAnimator`/`IPanelController` protocol or a separate animation-coordinator class - this is private in-process UI glue with one production caller-family; Unified Seam Policy's two-adapter rule would fail immediately, and single-Adapter policy/failure/platform-isolation justification doesn't apply either. Inline extraction only.

## Builder Notes

1. **Pattern: an "exhaustive sweep" claim from a prior loop is a lead, not proof - it needs re-testing by someone who did not write it, not just re-reading by someone who trusts it.**
   - How to recognize: a prior loop's own text says "exhaustive," "no further instance," or "checked all N methods." Treat that claim as itself a candidate finding to falsify, not as settled fact - assign a fresh pass (ideally an independently-briefed second reader) to re-derive the same completeness claim from scratch rather than spot-checking the specific instance the prior loop already fixed.
   - Smallest coding rule: for a defect class that has recurred 2+ times, the audit method itself (not just the fix) is suspect until a *second, differently-scoped* sweep also comes up empty. One "exhaustive" sweep finding nothing further is weak evidence; two independent sweeps finding nothing further is real evidence.
   - Stack example: loop 4's own 32-method enumeration concluded "no further instance was found" after checking `PopulateGridSelectionPanelAsync`'s siblings; this loop's independently-briefed helper sweep, tasked specifically with re-testing that claim rather than assuming it, found `DownloadAndReplaceImageAsync` had the identical unguarded-await shape loop 4's own methodology should have caught.

2. **Pattern: a session/generation check protects only the mutations reachable after its own check-point in its own stack frame - and a *caller's* check never protects a *callee's* own suspension point, no matter how many call-sites deep.**
   - How to recognize: when method A checks `session != field` and then calls method B, and B itself contains an `await`, the check in A does NOT protect anything B does after B's own await - not even indirectly, through a chain of calls. Grep every method reached from an already-guarded caller for its own awaits followed by mutations, treating each one as a fresh, ungraded surface regardless of how many "protected" callers lead to it.
   - Smallest coding rule: every method with its own await that mutates shared UI/session state needs its own check, even when every path that reaches it is already guarded elsewhere.
   - Stack example: `GridImage_Click`'s guard (loop 1, F-001) sits before it calls `DownloadAndReplaceImageAsync`; `DownloadAndReplaceImageAsync`'s own `await DownloadAndReplaceImageCoreAsync(...)` was left unguarded until this loop, even though its only caller was already "fixed" four loops ago.

3. **Pattern: a helper-surfaced "unsynchronized shared state" candidate looks identical whether it is a live hazard or an already-gated non-issue - the difference is a call-graph trace to the write site's and read site's actual entry points, not the shape of the state itself.**
   - How to recognize: a `static` field/collection with no lock, written from one method and read from another. Before promoting it to a finding, trace both the writer's and the reader's *own* entry points back to whatever exclusivity gate the codebase already has (here, `isLibraryOperationRunning`). If both trace to the same mutually-exclusive gate, the "unsynchronized" state was never reachable concurrently in the first place.
   - Smallest coding rule: compliance-is-not-clearance cuts both ways - a shape that looks dangerous still needs a reachability trace before it becomes a finding, exactly as much as code that "follows a rule" still needs independent judgment before it's cleared.
   - Stack example: this loop's helper surfaced `SteamGridDbClient.CapsuleParseNotes` (an unsynchronized static `List<string>`) as a race candidate; tracing its writer (`NoteCapsuleParse`, reachable only through `LoadGameEntriesAsync`'s sequential loop) and its reader (`FixLibraryAsync`) back to their shared `isLibraryOperationRunning` gate showed they can never run concurrently - the same conclusion loop 4 reached for `StoreNameLookup`'s dictionaries via the identical trace.

**Stalled-Dimension Sweep (loop 5 - 4th consecutive SAME on the six dimensions below; triggers on any dimension below 9.5 with `delta == SAME` for 3+ consecutive loops per `REVIEW_HISTORY.json`):**
- `domain_modeling` — explicit clean: re-read `GameEntry.cs`/`GamePlatform.cs` in full this loop; only known concern (`GameEntry`'s 3-property parallel-fields case) unchanged, not promotable.
- `data_flow` — explicit clean (with a falsified candidate): traced `SteamGridDbClient.CapsuleParseNotes`'s writer and reader entry points to the shared `isLibraryOperationRunning` gate this loop (see data_flow proof above); a helper-surfaced candidate rejected on the same reachability grounds as loop 4's equivalent finding.
- `framework_idioms` — named candidate (carried): Finding F-002 remains the only candidate, outranked again this loop, fourth loop running.
- `simplicity` — explicit clean beyond the tracked items: swept `Services/Artwork`, `Services/Library`, `Services/SteamGridDB`, `Services/Stores`, `Models/` across all four angles this loop; nothing beyond F-002/F-003 (already tracked) and the falsified/clean items above.
- `test_strategy` — named candidate (carried, evaluated, does not win Priority 1): Finding F-004 re-walked, still the only candidate, Cosmetic/off-primary-flow.
- `credibility` — explicit clean: re-read `TESTING.md` in full against current `.csproj` files this loop; framing unchanged, still accurate, still under-describing risk (5th confirming instance this loop).

**Scorecard humility check** — three claims here are the ones most likely to be argued the other way: (1) the DOWN move for `architecture_quality` (8.0→7.5) - a reviewer could argue this double-counts the same evidence already priced into the state_management/concurrency deductions, since loop 1's own humility check flagged exactly this double-counting risk and loop 4 explicitly chose *not* to move this dimension for the analogous reason; this loop takes the stricter reading loop 4 itself flagged as arguable, but a reasonable judge could hold the line loop 4 held instead. (2) The claim that `DownloadAndReplaceImageAsync`'s fix is "the strongest verification basis of any of the five fixes so far" (concurrency proof) - this rests on one helper sweep finding what a second helper sweep initially concluded was absent (that second sweep's own first-pass verdict was "loop 4's claim is correct" before it caught F-008 on closer inspection), which is a thinner margin of confidence than the confident framing suggests. (3) `RestoreBackup_Click`'s missing per-row guard, again walked and again explicitly rejected as a Finding this loop on smaller-blast-radius grounds identical to loop 4's own reasoning - four loops running without re-examining whether "self-correcting via a caught exception" is still the right bar as more of the file's other async methods get guards added around it.

## Final Judge Narrative
Place, not win, this loop. Runtime ownership is again not more trustworthy by a plain net-wash accounting - it is, on this loop's own honest reassessment, less trustworthy than the prior score implied: a fifth instance of the identical shape lived in a fresh top-level method two consecutive "exhaustive" sweeps did not catch. This loop closes that specific gap and, for the first time, backs the fix with two independently-scoped verification passes rather than one self-directed sweep - one helper explicitly re-tested loop 4's own completeness claim and found the gap it missed. State_management, concurrency and (newly, this loop) architecture_quality all moved DOWN, not because the code regressed since loop 4, but because this loop's own investigation is the basis for judging the prior confidence level as having under-priced the residual - a legitimate basis for downward correction distinct from "the code got worse." Simplification did not happen this loop on the F-002/F-003 axis - both remain queued for a fourth full loop, correctly outranked again by F-008's higher severity, not by proximity bias (F-008 is a materially distinct defect shape in a fresh method, found via independently-briefed re-verification, not incidental discovery in the same neighborhood). The systemic shared-guard-mechanism and population-wrapper alternatives were both evaluated this loop and rejected on SPT grounds - neither would have changed whether this specific gap was caught, since the actual failure mode is a missed call site, not verbose syntax. Tests do not, and structurally cannot, reduce regression risk on `PrimaryWidget.xaml.cs`; this loop's only regression evidence is full build + full suite (138/138 unchanged), an independent fresh-eyes implementation review (verdict `approved`), and a manual trace of the new guard's placement relative to its one await that matters. Future work risks over-engineering only if F-002/F-003's eventual extraction reaches for a coordinator abstraction instead of a private helper method, or if a future loop still tries to wrap the five hand-rolled session-check idioms in a shared type for what remains a set of small, honestly-duplicated 1-3 line idioms whose actual failure mode a wrapper type cannot fix - unchanged guidance from loop 1's own Deepening Candidate, now re-affirmed a fifth time against a fresh-method instance too.

## Loop 5 Result
Changed `SteamGridDB.Xbox/PrimaryWidget.xaml.cs` only (11 insertions, 0 deletions): added a session re-check inside `DownloadAndReplaceImageAsync`, immediately after its own await (`DownloadAndReplaceImageCoreAsync`) and before any further mutation of `GridPanelStatus.Text` or the call to `HideGridPanelAsync`: `if (gridItem.SessionId != gridPanelSessionId) { return; }`. Reuses the existing `gridItem.SessionId` field (already populated by `PopulateGridSelectionPanelAsync` when the tile was created) and the existing `gridPanelSessionId` field - no new field, no new type, no new parameter. This method's only network/file-write call (`DownloadAndReplaceImageCoreAsync`) is unchanged - same arguments, same call count, same ordering, same error handling - so no network call count, ordering, payload, or error-handling behavior changes anywhere in the flow; the fix is purely a local UI-mutation skip for a session no longer live. Full build (`msbuild SteamGridDB.Xbox.sln /p:AppxBundle=Never`) exits 0 both before and after the change. The full test suite (`run-tests.ps1`) reports 138 passed / 0 failed / 0 skipped both before and after - unchanged, as expected, since `PrimaryWidget.xaml.cs` is not part of the test-linked `Services/**` surface. An independent fresh-eyes implementation review (separate subagent, read-only, briefed cold on the targeted finding and the diff only) returned verdict `approved` with all three checks (reality, honesty, regression) passed. Finding F1 (stable_id F-008) is **resolved**: the specific display-corruption path (a stale, superseded picker session's download completion closing a live, unrelated session's panel) is closed by construction, verified by direct inspection of the diff (the guard sits textually immediately after the method's only await that matters and before every subsequent mutation) and by re-reading the final source. This loop additionally re-verified Findings F-001/F-005/F-006/F-007's fixes are still holding at their own call sites (no regression), and independently traced a helper-surfaced `SteamGridDbClient.CapsuleParseNotes` candidate to a falsified conclusion (see Scorecard `data_flow` proof and Builder Note 3). No unintended scorecard regression: the change touches no network call, no ranking/selection logic beyond skipping a stale-session UI update, and no file outside the one named. Findings F-002, F-003 and F-004 are carried forward, unchanged in substance (evidence line numbers corrected against current source for F-002 only, shifted +11 by this loop's own insertion), to the Improvement Backlog / Findings for future loops.
