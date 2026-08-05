### Loop Counter
Loop 8 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
Good app, but not top-tier yet

This loop's own independent re-derivation (a full-file cold sweep of PrimaryWidget.xaml.cs via one helper, a full leaf-module Services/Models sweep via a second, plus direct reads of every write/read site of `lastFocusedButton`, StoreNameLookup.cs in full, GamePlatform.cs, GameEntry.cs, App.xaml.cs, MainPage.xaml.cs and Converters/) confirmed F-019 as the correct Priority 1 and resolved it: `lastFocusedButton` is now split into two panel-scoped fields with the one legitimate cross-panel handoff made an explicit, named assignment. `state_management` and `credibility` both move to 10 this loop on the strength of that fix plus a comprehensive fresh sweep finding no further residual on either dimension. `architecture_quality` and `data_flow` remain flat (7.5) and F-011 remains genuinely blocked by the standing user constraint, which is what keeps this short of a top-tier verdict.

## Scorecard (1-10)

- Architecture quality: 7.5 | SAME | Fresh direct read this loop via an independently-briefed helper's full (pre-fix) read of PrimaryWidget.xaml.cs found no new module-graph or Seam-level finding: the same set of concerns co-located in one file persists, and no extraction candidate beyond what has already landed passes SPT without a multi-file redesign disproportionate to one loop's blast radius. This loop's own fix (F-019) is a field-level restructuring within the existing class, not a module-graph change. Stalled-Dimension Sweep (8 consecutive loops SAME): explicit clean.
- State management and runtime ownership: 10 | UP | F-019 resolved this loop - `lastFocusedButton`'s triple-role ambiguity is gone. `gridPanelFocusRestoreTarget` is now written only at EditGameImage_Click:1291 (its own open handler), HideGridPanelAsync:1701-1702 (its own close handler, clears), and SearchResult_Click:1840 (the one explicit, documented handoff); `searchPanelFocusRestoreTarget` only at SearchGameImage_Click:1732 (its own open handler), SearchResult_Click:1841 (handoff, clears), and HideSearchPanelAsync:1940-1943 (its own close handler, clears) - independently grep-verified this loop, all 6 write sites classified with zero unaccounted writes. Combined with F-018's fix (loop 6) independently re-holding, and `libraryOperationGuard`'s 7 call sites re-confirmed clean, no further state-ownership residual could be named after a comprehensive fresh census. No nameable residual means the honest score is 10.
- Domain modeling: 9.5 | SAME | Direct re-read this loop of GameEntry.cs:95-155 re-confirms the parallel-fields case (`HasSteamGridDBMatch`/`OfficialCapsuleUrl`/`SteamGridDbGameId`, lines 113-145) unchanged; this loop's own edit did not touch GameEntry.cs. Residual accepted (readonly-struct-with-factories still fails SPT Q2 - two-way XAML data binding needs a mutable wrapper).
- Data flow and dependency design: 7.5 | SAME | Ambient-state census re-confirmed this loop via direct read of StoreNameLookup.cs in full and a helper sweep of AppliedArtworkStore.cs/SteamGridDbClient.cs/FixLog.cs: 7 process-lifetime (static) instances, unchanged in count. No consolidation candidate passes SPT Q2. Stalled-Dimension Sweep: explicit clean.
- Framework / platform best practices: 9.5 | SAME | App.xaml.cs:120's `//TODO: Load state from previously suspended application` re-confirmed present this loop via direct grep (unchanged since loop 1). Residual accepted (fails SPT Q5 comparatively against this loop's actual pick).
- Concurrency and runtime safety: 9.5 | SAME | F-018's fix (loop 6) independently re-verified this loop by direct reads of HideGridPanelAsync/HideSearchPanelAsync at their post-fix locations. This loop's own F-019 fix touches no await boundary and introduces no new concurrency surface. Held at 9.5: `PopulateGridSelectionPanelAsync`'s own trailing `var _ = Dispatcher.RunAsync(...)` (PrimaryWidget.xaml.cs:1516-1519, current line numbers) remains an unstructured, undawaited Task with no error handling - re-confirmed unchanged. Residual accepted (SPT-rejected on Q5).
- Code simplicity and clarity: 9.5 | SAME | Fresh leaf-module duplication sweep this loop (helper-run, four angles, 24 files in Services/+Models/ read in full plus PrimaryWidget.xaml.cs): Reuse/Altitude/Efficiency clean; Simplification re-confirmed F-020 (StoreNameLookup.cs's GetOrFetchGogNameAsync/GetOrFetchEpicNameAsync, lines 102-131/244-277, identical double-checked-locking skeleton) as the sole remaining candidate, unchanged from loop 7. This loop's own F-019 fix is itself simplicity-positive: `HideSearchPanelAsync`'s `restoreFocus` boolean parameter became fully unnecessary once the handoff was made explicit and was deleted along with it. Residual queued (F-020).
- Test strategy and regression resistance: 8.0 | SAME | `GridImage_Click`'s stale-session guard (now at PrimaryWidget.xaml.cs:1552) remains untested, re-confirmed by grep across `SteamGridDB.Xbox.Tests\` for `SessionId` - zero hits, same platform-binding carve-out TESTING.md documents. This loop's own fix touches PrimaryWidget.xaml.cs only, which has no desktop test projection - 167/167 tests pass unchanged before and after. Mutation-test mental model re-run this loop on the new code: deleting `gridPanelFocusRestoreTarget = null;` at HideGridPanelAsync:1702 would go uncaught, same untestable-surface class as the already-named gap, not a new finding.
- Overall implementation credibility: 10 | UP | F-019's own gap - the one place this file's otherwise-careful inline-comment culture had a blind spot - is fully closed this loop: the new field-declaration comment (PrimaryWidget.xaml.cs:60-66) makes an exhaustive, falsifiable claim that this loop independently verified true by grep (6 write sites, all classified, zero unaccounted for). Combined with a comprehensive fresh sweep covering essentially the entire first-party source tree finding zero further honesty-leak candidates, no nameable residual remains.

## Authority Map
(Re-emitted this loop: an authority/state-ownership finding, F-019, was Priority 1.)

- Concern: Library-wide operation vs. single-game write mutual exclusion
  - Owner: `PrimaryWidget.libraryOperationGuard` (`LibraryOperationGuard` instance)
  - Allowed writers: `TryBeginLibraryOperation`/`EndLibraryOperation` (all 7 call sites, re-confirmed unchanged this loop)
  - Readers: `IsLibraryOperationBlocking` (`EditGameImage_Click`, `SearchGameImage_Click`)
  - Persistence seam: none
  - Async mutation entry points: every `TryBeginLibraryOperation` call site
  - Verdict: Single and clear

- Concern: Grid-picker and search-panel close-and-teardown mutual exclusion (F-018, resolved loop 6)
  - Owner: `PrimaryWidget.gridPanelCloseGuard` / `searchPanelCloseGuard` (`LibraryOperationGuard` instances)
  - Allowed writers: `HideGridPanelAsync` / `HideSearchPanelAsync` (own `TryBegin`/`End`)
  - Readers: none
  - Persistence seam: none
  - Async mutation entry points: `CloseGridPanel_Click`, `DownloadAndReplaceImageAsync`'s auto-close, `CloseSearchPanel_Click`, `SearchResult_Click`
  - Verdict: Single and clear

- Concern: Grid-panel focus-restoration target (F-019, resolved loop 8)
  - Owner: `PrimaryWidget.gridPanelFocusRestoreTarget` (Button field, own to the grid panel)
  - Allowed writers: `EditGameImage_Click:1291` (own open handler), `SearchResult_Click:1840` (explicit handoff from `searchPanelFocusRestoreTarget`), `HideGridPanelAsync:1701-1702` (own close handler, restores then clears)
  - Readers: `HideGridPanelAsync:1701-1702`
  - Persistence seam: none
  - Async mutation entry points: `EditGameImage_Click`, `SearchResult_Click`, `HideGridPanelAsync`
  - Verdict: Single and clear

- Concern: Search-panel focus-restoration target (F-019, resolved loop 8)
  - Owner: `PrimaryWidget.searchPanelFocusRestoreTarget` (Button field, own to the search panel)
  - Allowed writers: `SearchGameImage_Click:1732` (own open handler), `SearchResult_Click:1841` (explicit handoff, clears), `HideSearchPanelAsync:1940-1943` (own close handler)
  - Readers: `HideSearchPanelAsync:1940-1943`
  - Persistence seam: none
  - Async mutation entry points: `SearchGameImage_Click`, `SearchResult_Click`, `HideSearchPanelAsync`
  - Verdict: Single and clear

## Strengths That Matter
- ArtworkRanker/ArtworkDownloader/ArtworkSignature/TileImage remain a genuinely deep, pure, well-tested pipeline - re-confirmed this loop's own independent leaf-module duplication sweep (Reuse/Altitude/Efficiency clean across all four files) with no structural issues and no domain-policy leakage.
- This loop's own F-019 fix eliminates the actual multi-writer ambiguity, not just documents it: `gridPanelFocusRestoreTarget` and `searchPanelFocusRestoreTarget` are now written only by their own panel's own open handler, their own panel's own close handler, or one explicit, named handoff assignment (PrimaryWidget.xaml.cs:1840-1841) - independently grep-verified this loop (all 6 total write sites classified, zero unaccounted-for writes) and confirmed by a cold implementation reviewer.
- `HideSearchPanelAsync`'s `restoreFocus` boolean parameter - a control-flow flag whose only purpose was suppressing the normal cleanup for one caller - became fully unnecessary once the handoff was made explicit, and was deleted along with it: net simplification, not just net safety.

## Findings

### Finding #1: lastFocusedButton is written by both EditGameImage_Click and SearchGameImage_Click with no single owner, and is deliberately left uncleared across the SearchResult_Click -> grid-panel handoff

**Why it matters** — PrimaryWidget's own Edit and Search entry points can both reach the underlying game-row list while the other panel's ~250ms slide-up animation is still in flight; if the not-yet-covering panel's row buttons are reached during that window, the two panels' close handlers race for the same field, and whichever panel closes second finds `lastFocusedButton` already cleared by the other and silently skips its own focus restoration.

**What is wrong** — `lastFocusedButton` (PrimaryWidget.xaml.cs:59, pre-fix) was a single Button field written by `EditGameImage_Click` (line 1282) and `SearchGameImage_Click` (line 1723), and read/cleared by both `HideGridPanelAsync` (lines 1692-1693) and `HideSearchPanelAsync` (lines 1924-1927). `SearchResult_Click` (line 1829) explicitly closed the search panel with `HideSearchPanelAsync(false)` specifically so `lastFocusedButton` was NOT cleared, carrying the original search-opening button's value forward - a genuine, deliberate cross-panel handoff, undocumented at the field's own declaration.

**Evidence** — PrimaryWidget.xaml.cs:59, 1282, 1692-1693, 1723, 1828-1830, 1924-1927 (all pre-fix)

**Architectural test failed** — n/a — state-ownership finding, not an abstraction-removal candidate

**Dependency category** — n/a (not a Coupling & Leakage finding)

**Leverage impact** — Callers (the four Show/Hide-adjacent handlers) could not tell from the field's own declaration which of three roles it was playing at a given moment.

**Locality impact** — Contained to PrimaryWidget.xaml.cs; no other file read or wrote this field.

**Metric signal, if any** — none

**Why this weakens submission** — A real, if narrow, single-owner ambiguity on a mutable field driving primary-flow focus restoration, undocumented at its declaration - the naive fix (split into two panel-scoped fields) would have silently broken the intentional handoff, so it needed a documented redesign. Rated Noticeable rather than Serious since the worst reachable consequence was a missed focus restoration (UX only).

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Fixed this loop: split `lastFocusedButton` into two panel-scoped fields, `gridPanelFocusRestoreTarget` and `searchPanelFocusRestoreTarget`. The one legitimate cross-panel case - `SearchResult_Click`'s handoff - is now an explicit, named, two-line assignment instead of an implicit "skip the clear" boolean parameter, which let `HideSearchPanelAsync`'s own `restoreFocus` parameter be deleted entirely.

**Blast radius** — Change: SteamGridDB.Xbox/PrimaryWidget.xaml.cs. Avoid: no XAML changes needed.

### Finding #2: StoreNameLookup's GOG and Epic name-fetch methods independently reimplement the same double-checked-locking skeleton

**Why it matters** — Any future fix or refinement to the check-then-populate discipline has to be applied by hand in both places; nothing enforces they stay identical.

**What is wrong** — `GetOrFetchGogNameAsync` (StoreNameLookup.cs:102-131) and `GetOrFetchEpicNameAsync` (StoreNameLookup.cs:244-277) each independently implement the identical shape: check cache unlocked, await the cache's own gate, re-check under the gate, fetch, cache only a non-empty result, release. `FindGameByNameAsync` (144-190) is a related but distinct third shape (int cacheable-zero, must not cache a network failure) - genuinely different enough to stay out of scope.

**Evidence** — StoreNameLookup.cs:102-131, 244-277

**Architectural test failed** — Shallow module

**Dependency category** — n/a

**Leverage impact** — Each new per-key string cache with the same "skip empty-string misses" semantics pays the double-checked-locking boilerplate tax again by hand.

**Locality impact** — Contained to StoreNameLookup.cs.

**Metric signal, if any** — none

**Why this weakens submission** — Synchronized maintenance across two behavior-bearing sites implementing the identical check-then-populate skeleton - reduces Locality without adding any offsetting clarity.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Extract a small private `GetOrFetchNameAsync(Dictionary<string,string> cache, SemaphoreSlim gate, string key, Func<Task<string>> fetch)` helper used by the two GOG/Epic methods only; leave `FindGameByNameAsync`'s distinct logic untouched.

**Blast radius** — Change: StoreNameLookup.cs. Avoid: `FindGameByNameAsync` (out of scope), EpicLibrary.cs, StoreNameLookupTests.cs.

### Finding #3: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (PrimaryWidget.xaml.cs:411-781, unaffected by this loop's edit - shifted from loop 7's cited 402-705 by this loop's own +8-line field-declaration insertion well above this range) awaits each entry's SteamGridDB platform-ID lookup, store name-fetch and SteamGridDB name search in strict sequence, even though the awaits are independent across entries.

**Evidence** — PrimaryWidget.xaml.cs:411-781

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None currently actionable - see blocker.

**Locality impact** — Would be contained to PrimaryWidget.xaml.cs if unblocked.

**Metric signal, if any** — none

**Why this weakens submission** — A real, linearly-scaling latency cost, but BLOCKED: the recorded standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft) means this loop cannot land a change altering observable call ordering/concurrency/count without a product decision this loop cannot make.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — BLOCKED this loop; named for continuity rather than escalated to a user-decision halt, since F-019 (resolved) and F-020 (queued) fill this loop's actionable capacity.

**Blast radius** — Change: none (blocked). Avoid: PrimaryWidget.xaml.cs (no change while blocked).

## Simplification Check
- Structurally necessary: Splitting `lastFocusedButton` into `gridPanelFocusRestoreTarget`/`searchPanelFocusRestoreTarget` - removes a real multi-writer ambiguity current source could not otherwise prove race-safe during the narrow interactive-during-slide window.
- New seam justified: false (no port/adapter added)
- Helpful simplification: `HideSearchPanelAsync`'s `restoreFocus` boolean parameter is deleted entirely - it existed only to suppress the normal cleanup for `SearchResult_Click`'s handoff, and the handoff is now an explicit two-line assignment that makes the parameter's job unnecessary.
- Should NOT be done: Merging `FindGameByNameAsync` into the same helper as the GOG/Epic methods (F-020, queued) - risks a fake-simplification behavior drift. Modeling the focus-restore targets as an enum-tagged single field instead of two plain fields - would add a costume layer for what two plain Button fields plus one documented handoff already express honestly.
- Tests after fix: No tests existed on `lastFocusedButton` before (PrimaryWidget.xaml.cs has no desktop test projection) and none exist on the split fields after - consistent with every other PrimaryWidget.xaml.cs-only fix in this project's history. Verification: full build (exit 0) and full test suite (167 passed/0 failed, both before and after) re-run; independent implementation review returned verdict approved with all three checks passed.

## Improvement Backlog
1. F-020 — Extract a shared `GetOrFetchNameAsync` helper for StoreNameLookup's GOG and Epic double-checked-locking duplication. Kind: simplification. Rank: helpful. Why it matters: sole remaining actionable Noticeable finding this loop's investigation found (F-011 is blocked); resolving it clears simplicity's last queued residual. Score impact: simplicity +0.5.
2. F-011 — Parallelize LoadGameEntriesAsync's per-entry network resolution. Kind: structural. Rank: needed for winning. Why it matters: BLOCKED by the standing user constraint on per-game network-call ordering/concurrency; named for continuity, not actionable this loop. Score impact: concurrency +0.5.

## Deepening Candidates
None. No friction proven this loop beyond what Findings #1-#3 already cover; no Module shows Interface-shallower-than-Implementation drift worth a new deepening candidate.

## Builder Notes

**Pattern 1** — An implicit "skip the normal cleanup" boolean parameter is often really an unwritten ownership handoff between two owners - making the handoff an explicit, named assignment removes the ambiguity and often lets the parameter itself disappear.
- How to recognize: The old `HideSearchPanelAsync(bool restoreFocus = true)` signature - a boolean whose only job was "don't do the normal cleanup step this one time, because something later needs the value."
- Smallest coding rule: When a shared field is read/cleared by two different owners' close paths and one owner needs to hand its value to the other, give each owner its own field and write the handoff as one explicit assignment at the handoff site.
- Stack example: C#: `HideSearchPanelAsync(bool restoreFocus = true)` lost its parameter entirely once `gridPanelFocusRestoreTarget`/`searchPanelFocusRestoreTarget` became separate fields.

**Pattern 2** — Splitting one shared field into per-owner fields doesn't just document an existing write-ownership ambiguity, it structurally removes the race the ambiguity created - because each owner's close handler now only ever touches its own field.
- How to recognize: A single mutable field written by two independent entry points, where a narrow animation or async window lets both entry points fire before either's corresponding close handler runs.
- Smallest coding rule: When two independent flows share one mutable field for two conceptually distinct roles, prefer splitting the field over merely commenting the split - verify the one legitimate cross-flow case can become an explicit assignment.
- Stack example: C#: `gridPanelFocusRestoreTarget`/`searchPanelFocusRestoreTarget` replacing the single `lastFocusedButton` field; the one cross-panel case became a two-line handoff.

**Pattern 3** — A field's own doc comment can be written as a literal, checkable enumeration of every legitimate write site by role, not just a narrative description - and when it is, a reviewer can verify the claim by grep instead of by trust.
- How to recognize: Compare the new fields' doc comment - which enumerates "own open handler OR own close handler OR the handoff" as an exhaustive classification - against a narrative comment that just describes what a field is for.
- Smallest coding rule: When documenting a shared mutable field's ownership, write the doc comment as an exhaustive classification of write sites by role, then grep the field name and confirm every hit falls into a named role before committing.
- Stack example: C#: the new field comment's claim was independently grep-verified this loop against all 6 actual write sites - zero unaccounted for.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) `state_management` and `credibility` both jumping to a full 10 in the same loop - the evidence (a comprehensive fresh sweep of essentially the entire first-party source tree finding zero further residual on either dimension) is real, but a stricter critic might have staged one or both through 9.5 first rather than crediting a same-loop double-10, since a two-tier jump on two dimensions at once from one loop's fix is a bigger move than this project's own loop-over-loop history typically shows. (2) `domain_modeling` and `framework_idioms`'s accepted residuals were carried forward from loop 7's disposition without a fresh Adversarial Pass re-run this loop (this loop's investigation focus was PrimaryWidget.xaml.cs's focus-restoration mechanism) - both are re-confirmed present by direct grep/read, but the "no cheaper fix exists" argument itself was not re-litigated fresh. (3) Rating F-019 "Noticeable weakness" (carried from loop 7) rather than re-examining whether it should have been "Serious deduction" - the reachability chain for the described race (both panels' row buttons interactive during the other's ~250ms slide) was accepted on loop 7's own reasoning rather than independently re-verified against the live WinUI app this loop.

## Final Judge Narrative
Place, and a clean one - this loop resolved F-019, the long-analyzed `lastFocusedButton` ambiguity, by splitting it into two panel-scoped fields with the one legitimate cross-panel handoff made an explicit, named assignment instead of an implicit "skip the clear" boolean parameter - which let `HideSearchPanelAsync`'s own `restoreFocus` parameter disappear entirely as dead ceremony once it was no longer needed. Simplification helped: net structural clarity gained for a net-small line-count change, with zero XAML edits and zero observable behavior change (167/167 tests pass unchanged). Runtime ownership is now genuinely trustworthy for every mutable concern this loop's comprehensive fresh sweep covered - `state_management` and `credibility` both moved to 10 this loop, a call flagged for extra scrutiny in this loop's own humility check. Concurrency remains trustworthy (F-018's fix still holds; the one remaining accepted residual is inert by its own SPT-rejected reasoning). Tests reduce regression risk everywhere they can reach, but PrimaryWidget.xaml.cs's own architectural constraint (no desktop test projection) still excludes `GridImage_Click`'s session guard from that protection, unchanged. Future work risks overengineering if F-020's extraction reaches for a generic multi-purpose cache helper instead of the narrow two-method helper already scoped in the backlog.

## Loop 8 Result
Split `lastFocusedButton` (PrimaryWidget.xaml.cs:59) into two panel-scoped fields, `gridPanelFocusRestoreTarget` and `searchPanelFocusRestoreTarget`; `EditGameImage_Click`/`HideGridPanelAsync` now own the grid field exclusively, `SearchGameImage_Click`/`HideSearchPanelAsync` own the search field exclusively, and `SearchResult_Click`'s cross-panel handoff is now an explicit two-line assignment instead of an implicit "skip the clear" boolean parameter (which was deleted from `HideSearchPanelAsync`'s signature). Full build (msbuild, exit 0) and full test suite (run-tests.ps1: 167 passed/0 failed, unchanged before and after) both re-run, confirming zero regressions on this file's only reachable test surface (there is none - no desktop test projection). Finding F-019 (stable_id F-019) is **resolved** - `lastFocusedButton` no longer exists in current source, and every write site of its two replacement fields was independently grep-verified against the new field-declaration comment's exhaustive claim. No unintended scorecard regression observed; `state_management` and `credibility` both moved UP as a direct consequence.
