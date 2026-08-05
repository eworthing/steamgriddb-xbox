### Loop Counter
Loop 9 of 10 (cap)

### System Flag
[STATE: CONTINUE]

---

## Contest Verdict
Good app, but not top-tier yet

This loop resolved F-020 (StoreNameLookup's GOG/Epic double-checked-locking duplication) by consolidating both methods' identical bodies into one shared private helper, with zero PrimaryWidget.xaml.cs/XAML edits. The louder work this loop was verification: per the run's own instruction, `state_management` and `credibility` (both raised to 10 last loop) were re-tested against current source by two independent, blind adversarial helper passes plus a direct field-by-field census, and both held on fresh evidence with zero counter-examples found. `simplicity` also reaches 10 this loop on a comprehensive fresh leaf-module sweep finding nothing beyond F-020. `architecture_quality` and `data_flow` remain flat (7.5) and F-011 remains genuinely blocked by the standing user constraint, which is what keeps this short of a top-tier verdict.

## Scorecard (1-10)

- Architecture quality: 7.5 | SAME | Fresh direct reads this loop (PrimaryWidget.xaml.cs in full again, plus SteamGridDbClient.cs, GameImages.cs, OperationReport.cs, LibraryOperationGuard.cs, AppliedArtworkStore.cs, FixLog.cs, JsonRead.cs, AsyncLazyCache.cs, ArtworkDownloader.cs, ArtworkRanker.cs, ArtworkSignature.cs, all read directly this loop) plus two independent helper sweeps found no new module-graph or Seam-level finding: PrimaryWidget.xaml.cs still co-locates UI event handling, network orchestration, file I/O/backup-restore, artwork-selection invocation, panel/search navigation, bulk-operation loops, and library-operation guarding in one class, and no extraction candidate passes SPT without a multi-file redesign disproportionate to one loop's blast radius. This loop's own fix (F-020) is confined to StoreNameLookup.cs's internal implementation, not a module-graph change. Stalled-Dimension Sweep (9 consecutive loops SAME, per loop 8's own count of 8 plus this loop): explicit clean.
- State management and runtime ownership: 10 | SAME | Extra-scrutiny re-verification this loop (per the run's own instruction to adversarially re-test loop 8's two same-loop 10s): two independent, blind helper passes (one explicitly briefed to try to break the claim) plus my own direct field-by-field census of every PrimaryWidget.xaml.cs mutable field re-derived every write/read site and found zero counter-examples. `gridPanelFocusRestoreTarget`'s 3 write sites (EditGameImage_Click:1291, HideGridPanelAsync:1701-1702, SearchResult_Click:1840) and `searchPanelFocusRestoreTarget`'s 3 (SearchGameImage_Click:1732, SearchResult_Click:1841, HideSearchPanelAsync:1940-1943) remain exhaustively classified against the field's own doc comment. `searchPanelSessionId`'s two increment sites (PerformGameSearchAsync:1765, ShowSearchPanelAsync:1856) remain a documented, semantically-justified invalidation-counter pattern (bumped on any event that should discard a stale in-flight search), not an ownership ambiguity. Every `LibraryOperationGuard` TryBegin/End pair remains balanced across every early return and exception path. This loop's own F-020 fix touches no PrimaryWidget.xaml.cs state at all (confined to StoreNameLookup.cs). No nameable residual survived genuine adversarial pressure.
- Domain modeling: 9.5 | SAME | Direct re-read this loop of GameEntry.cs in full re-confirms the parallel-fields case (`HasSteamGridDBMatch`/`OfficialCapsuleUrl`/`SteamGridDbGameId`, lines 113-145) unchanged; this loop's own edit did not touch GameEntry.cs. Adversarial Pass on this accepted residual (mandatory this loop): considered a genuinely new alternative to the previously-rejected readonly-struct proposal - a factory-method/enum design (`SteamGridDbMatchKind` discriminator plus `SetMatchedByPlatform`/`SetMatchedByName` methods replacing the three public setters) that would make the true correlation between the three fields enforced rather than merely conventional. Rejected on SPT Q2: the single flat object-initializer at the one construction site (`LoadGameEntriesAsync`) would have to become a two-step construct-then-conditionally-call-one-of-two-setters protocol, trading today's permissiveness for a comparably-sized new failure class (forgetting to call the right setter, or calling one twice), not a genuinely smaller fix. Residual accepted.
- Data flow and dependency design: 7.5 | SAME | Direct re-reads this loop of StoreNameLookup.cs (post-fix), SteamGridDbClient.cs, FixLog.cs, AppliedArtworkStore.cs, AsyncLazyCache.cs in full: the same scattered process-lifetime static caches persist (StoreNameLookup's `gogNameCache`/`epicNameCache`/`nameMatchCache`/`ubisoftGameListCache`, SteamGridDbClient's `capsuleParseNotes`, FixLog's `lines`/`fileName`/`logFolder`, AppliedArtworkStore's `appliedCache`/`recordFolder`) - this loop's own fix consolidated duplicate CODE across two of these caches' accessor methods but did not consolidate the STATE itself (still separate Dictionary instances). No consolidation candidate passes SPT Q2 for a single-instance widget with no multi-instance/test-injection need. Stalled-Dimension Sweep: explicit clean.
- Framework / platform best practices: 9.5 | SAME | App.xaml.cs:120's `//TODO: Load state from previously suspended application` re-confirmed present this loop via direct read (unchanged since loop 1). Prior-audit adopt-or-falsify pass this loop (Discovery-listed ARTWORK-SELECTION.md, dated 2026-08-03): the one open claim touching this dimension - §3.5/§4.5's note that `SteamGridDbClient` still does not distinguish a 429 rate-limit from any other failure for retry purposes - verified live in current source (`SteamGridDbClient.GetStringAsync` logs and returns null uniformly on any non-success status). Adopted as real but Cosmetic-for-contest: the failure IS already caught and correctly reported as "request failed" rather than "no artwork" (the harm §4.5 actually fixed); adding retry/backoff policy is a resilience/product feature, not an architecture ownership/seam/depth concern this rubric scores - not promoted to backlog. Adversarial Pass on the accepted TODO-comment residual: deleting it is trivially small but genuinely zero-behavioral-consequence (a stale comment on a debug/Start-menu-launch-only fallback path) - SPT-rejected on Q5, re-confirmed with fresh reasoning this loop.
- Concurrency and runtime safety: 9.5 | SAME | This loop's own F-020 fix moves `GetOrFetchGogNameAsync`'s and `GetOrFetchEpicNameAsync`'s identical `SemaphoreSlim` WaitAsync/Release pairs into one shared private helper, parameterized by which cache/gate to use - the lock objects themselves (`gogNameGate`, `epicNameGate`) are unchanged, same single-lock-per-call shape, no lock-ordering change, so this is not a Meta-Rule 4 risk-boundary crossing. Adversarial Pass on the accepted `PopulateGridSelectionPanelAsync` residual (PrimaryWidget.xaml.cs:1516-1528, the discarded `var _ = Dispatcher.RunAsync(...)`): re-tested the proposed fix (await it) this loop - `GridImagesView.UpdateLayout()`/`ContainerFromIndex(0)` do not fail on a live GridView and `Focus()` is null-guarded via `?.`, so the only observable effect of awaiting would be surfacing an essentially-unreachable exception class for a controller-focus nicety - still SPT-rejected on Q5 even as the sole remaining candidate this loop; a zero-realistic-gain fix does not become a real gain for lack of competition.
- Code simplicity and clarity: 10 | UP | F-020 resolved this loop: `GetOrFetchGogNameAsync` and `GetOrFetchEpicNameAsync` (StoreNameLookup.cs) now both delegate to one shared private `GetOrFetchNameAsync(cache, gate, key, fetch)` helper (StoreNameLookup.cs:101-151) containing the double-checked-locking skeleton once instead of twice; `FindGameByNameAsync`'s genuinely distinct int-cacheable-zero/must-not-cache-failure shape was deliberately left untouched. Comprehensive fresh leaf-module duplication sweep this loop (helper-run, four angles, 24 files/3339 lines in Services/+Models/, cross-checked by direct reads of 20 of those files plus PrimaryWidget.xaml.cs in full) found nothing else Noticeable-or-worse: the one candidate raised (AppliedArtworkStore.cs's manual JsonValueType enumeration vs JsonRead's named-lookup helpers, `LoadMapFromDiskAsync`:129-137) is a different shape solving a different problem (bulk enumeration vs single named lookup) at a single call site - not a genuine Reuse duplication, correctly not promoted. No further source-backed residual can be named after this loop's own comprehensive sweep; per the 9.5+/10 Threshold rule the honest score is 10.
- Test strategy and regression resistance: 8.0 | SAME | `GridImage_Click`'s stale-session guard (PrimaryWidget.xaml.cs:1552, unchanged - this loop's edit is confined to StoreNameLookup.cs) remains untested, re-confirmed by grep across `SteamGridDB.Xbox.Tests\` for `SessionId` - zero hits, same platform-binding carve-out TESTING.md documents. This loop's own fix touches StoreNameLookup.cs only, whose GOG/Epic methods are also untested by design (network-bound, per StoreNameLookupTests.cs's own doc comment) - 167/167 tests pass unchanged before and after. Mutation-test mental model re-run this loop on the new shared helper: deleting the "cache only if non-empty" guard inside `GetOrFetchNameAsync` would go uncaught by any test, same untestable-network-bound-surface class as the already-named primary-flow gap, not a new finding. Held at 8.0 - the primary-flow gap remains the actual blocker.
- Overall implementation credibility: 10 | SAME | Extra-scrutiny re-verification this loop: two independent adversarial helper passes plus my own direct reads found zero doc-comment-vs-code mismatches anywhere sampled (PrimaryWidget.xaml.cs's field-ownership comment, its session-guard comments, App.xaml.cs, MainPage.xaml.cs, LibraryOperationGuard.cs, StoreNameLookup.cs, SteamGridDbClient.cs, AppliedArtworkStore.cs, FixLog.cs, ArtworkDownloader.cs, ArtworkRanker.cs, ArtworkSignature.cs). This loop's own new doc comment on `GetOrFetchNameAsync` (StoreNameLookup.cs:112-115) makes a falsifiable claim - that `FindGameByNameAsync` is deliberately excluded because folding it in would risk caching a failed request the same way a genuine miss is cached - independently verified true by re-reading `FindGameByNameAsync`'s own catch block (StoreNameLookup.cs:198-202: `nameMatchCache` is written only inside the try, never inside the catch). No nameable residual survived a second consecutive loop of genuine adversarial pressure.

## Authority Map
(Not re-emitted this loop: no authority/state-ownership finding was Priority 1 - F-020 is a simplicity/leaf-module-duplication finding. See loop 8's archive for the current Authority Map, unchanged this loop.)

## Strengths That Matter
- ArtworkRanker/ArtworkDownloader/ArtworkSignature/TileImage remain a genuinely deep, pure, well-tested pipeline - re-confirmed this loop by direct reads of ArtworkSignature.cs, ArtworkRanker.cs and ArtworkDownloader.cs in full plus a helper's four-angle sweep, with no structural issues and no domain-policy leakage.
- This loop's own F-020 fix is a clean, verifiable subtraction: two 30-line hand-synchronized method bodies collapse into two 3-line delegating calls plus one 50-line shared helper, with the public interface, cache semantics, and lock behavior all provably unchanged - confirmed by a cold implementation reviewer on the first pass.
- This loop's extra-scrutiny mandate on `state_management`/`credibility` was answered with real adversarial effort, not a rubber stamp: two independently-spawned helpers, one explicitly briefed to try to break the claim, both came back with a full field-by-field census and zero counter-examples - the kind of evidence a 10 is supposed to require.

## Findings

### Finding #1: StoreNameLookup's GOG and Epic name-fetch methods independently reimplement the same double-checked-locking skeleton

**Why it matters** — Any future fix or refinement to the check-then-populate discipline has to be applied by hand in both places; nothing enforces they stay identical.

**What is wrong** — `GetOrFetchGogNameAsync` (StoreNameLookup.cs:102-131, pre-fix) and `GetOrFetchEpicNameAsync` (StoreNameLookup.cs:244-277, pre-fix) each independently implemented the identical shape: check cache unlocked, await the cache's own gate, re-check under the gate, fetch, cache only a non-empty result, release. `FindGameByNameAsync` (144-190) is a related but distinct third shape (its cached value is an int where 0 is a valid, cacheable "not found" result, and it additionally must not cache a network-failure exception) - genuinely different enough to stay out of scope.

**Evidence** — StoreNameLookup.cs:102-131, 244-277 (pre-fix line numbers)

**Architectural test failed** — Shallow module

**Dependency category** — n/a (not a Coupling & Leakage finding)

**Leverage impact** — Each new per-key string cache with the same "skip empty-string misses" semantics paid the double-checked-locking boilerplate tax again by hand.

**Locality impact** — Contained to StoreNameLookup.cs.

**Metric signal, if any** — none

**Why this weakens submission** — Synchronized maintenance across two behavior-bearing sites implementing the identical check-then-populate skeleton - reduced Locality without adding any offsetting clarity.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — Fixed this loop: extracted a small private `GetOrFetchNameAsync(Dictionary<string,string> cache, SemaphoreSlim gate, string key, Func<Task<string>> fetch)` helper used by the two GOG/Epic methods only; `FindGameByNameAsync`'s distinct logic left untouched.

**Blast radius** — Change: StoreNameLookup.cs. Avoid: `FindGameByNameAsync` (out of scope), EpicLibrary.cs, StoreNameLookupTests.cs (existing tests pass unmodified).

### Finding #2: LoadGameEntriesAsync resolves each unmatched game's name and SteamGridDB match sequentially, one network round trip at a time, on every widget open

**Why it matters** — On a library with many unmatched games, the fully-sequential per-entry network chain adds latency that scales linearly with library size.

**What is wrong** — `LoadGameEntriesAsync`'s per-entry loop (PrimaryWidget.xaml.cs:411-781, unaffected by this loop's edit - confined to StoreNameLookup.cs) awaits each entry's SteamGridDB platform-ID lookup, store name-fetch and SteamGridDB name search in strict sequence, even though the awaits are independent across entries.

**Evidence** — PrimaryWidget.xaml.cs:411-781

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — None currently actionable - see blocker.

**Locality impact** — Would be contained to PrimaryWidget.xaml.cs if unblocked.

**Metric signal, if any** — none

**Why this weakens submission** — A real, linearly-scaling latency cost, but BLOCKED: the recorded standing user constraint (no behavioural oracle for per-game network-call ordering/concurrency changes against SteamGridDB/GOG/Epic/Ubisoft) means this loop cannot land a change altering observable call ordering/concurrency/count without a product decision this loop cannot make. Re-derived fresh this loop: a pure extraction would not be blocked, but this finding's own remedy (bounded concurrency) necessarily changes network-call ordering, so it remains genuinely blocked.

**Severity** — Noticeable weakness

**ADR conflicts** — none

**Minimal correction path** — BLOCKED this loop; named for continuity per Backlog Prioritization Pass criterion 0 rather than escalated to a user-decision halt, since it is not the sole candidate this loop (F-020 filled this loop's actionable capacity).

**Blast radius** — Change: none (blocked). Avoid: PrimaryWidget.xaml.cs (no change while blocked).

### Finding #3: GameEntry's SteamGridDB-match fields (HasSteamGridDBMatch/OfficialCapsuleUrl/SteamGridDbGameId) admit a combination LoadGameEntriesAsync never actually constructs

**Why it matters** — A reader of GameEntry's type alone cannot tell that `HasSteamGridDBMatch == false` combined with `SteamGridDbGameId > 0` never happens in practice - the type represents more states than the one constructor site ever produces, so the correlation lives only in LoadGameEntriesAsync's own control flow, not in the domain model.

**What is wrong** — `HasSteamGridDBMatch` (bool), `OfficialCapsuleUrl` (nullable string) and `SteamGridDbGameId` (int) are three independently publicly-settable properties (GameEntry.cs:113-145). LoadGameEntriesAsync's construction (PrimaryWidget.xaml.cs, the single `new GameEntry { ... }` call site) only ever produces three of the four representable combinations: unmatched (`false`/`null`/`0`), matched by platform ID (`true`/possibly-non-null capsule/`0` - `SteamGridDbGameId` is never set on this path), or matched by name search (`true`/`null`/`>0`). `false` combined with a non-zero `SteamGridDbGameId` is never constructed but is fully representable by the type.

**Evidence** — SteamGridDB.Xbox/Models/GameEntry.cs:113-145; SteamGridDB.Xbox/PrimaryWidget.xaml.cs (LoadGameEntriesAsync's construction site and its two match branches)

**Architectural test failed** — n/a

**Dependency category** — n/a

**Leverage impact** — A caller reading GameEntry's type signature alone cannot recover which combinations are real without reading LoadGameEntriesAsync's control flow.

**Locality impact** — Contained to GameEntry.cs and its one construction site; no other file constructs a GameEntry.

**Metric signal, if any** — none

**Why this weakens submission** — An impossible-in-practice state remains representable, which is exactly what the domain_modeling 9-anchor ("impossible states unrepresentable") tests for - but the harm is narrow (no code path currently exercises the gap) and every proposed fix trades today's permissiveness for a comparably-sized new ceremony burden, which is why this stays an accepted residual rather than a queued backlog item.

**Severity** — Cosmetic for contest

**ADR conflicts** — none

**Minimal correction path** — Not fixed this loop (accepted residual). Two alternatives considered and both rejected on SPT Q2 (smallest honest fix): (a) a readonly struct with static factories - fails because GameEntry implements INotifyPropertyChanged and is two-way XAML-data-bound, which a readonly struct cannot back without a separate wrapper layer; (b) a `SteamGridDbMatchKind` enum discriminator plus `SetMatchedByPlatform`/`SetMatchedByName` methods replacing the three public setters (considered fresh this loop) - fails because it turns the one flat object-initializer construction site into a two-step construct-then-conditionally-call-one-setter protocol, trading today's permissiveness for a new "forgot to call the setter" failure class rather than a genuinely smaller fix.

**Blast radius** — Change: none this loop (accepted residual, not queued). If ever queued: GameEntry.cs, PrimaryWidget.xaml.cs's one construction site.

## Simplification Check
- Structurally necessary: Consolidating `GetOrFetchGogNameAsync`'s and `GetOrFetchEpicNameAsync`'s identical double-checked-locking bodies into one shared private `GetOrFetchNameAsync` helper - passes the deletion test (delete the helper and the duplicated skeleton reappears in both of its 2 callers, so it earns its keep).
- New seam justified: false (no port/adapter added - a private static helper, same file, same class)
- Helpful simplification: `GetOrFetchGogNameAsync` and `GetOrFetchEpicNameAsync` both shrink to a single delegating call each; their own doc comments now point to the shared helper's fuller explanation instead of each repeating it.
- Should NOT be done: Folding `FindGameByNameAsync` into the same helper (out of scope - its int-cacheable-zero/must-not-cache-failure semantics differ genuinely, a fake-simplification behavior-drift risk). Modeling GameEntry's SteamGridDB-match fields as a discriminated enum+factory-methods (Finding #3's residual, re-tested this loop) - trades today's permissiveness for a comparably-sized two-step-construction ceremony burden with a new class of caller error, not a net simplification.
- Tests after fix: StoreNameLookupTests.cs's existing `NormaliseGameName` tests are unaffected (untouched surface); `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync`/the new `GetOrFetchNameAsync` helper remain untested by design - network-bound, per the test file's own doc comment ("the rest of StoreNameLookup calls GOG, Epic and Ubisoft over the network, and a test that did that would be grading their uptime") - consistent with every other network-bound fix in this project's history. Verification: full build (msbuild, exit 0, both before and after) and full test suite (run-tests.ps1: 167 passed/0 failed both times) re-run; independent implementation review (separate subagent, read-only, briefed cold on this finding and the diff only) returned verdict approved with all three checks passed on the first pass.

## Improvement Backlog
1. F-011 — Parallelize LoadGameEntriesAsync's per-entry network resolution. Kind: structural. Rank: needed for winning. Why it matters: the sole remaining Noticeable-or-worse candidate this loop's investigation found; BLOCKED by the standing user constraint on per-game network-call ordering/concurrency, named for continuity per Backlog Prioritization Pass criterion 0 rather than silently dropped. Score impact: concurrency +0.5.

## Deepening Candidates
None. No friction proven this loop beyond what Findings #1-#3 already cover; no Module shows Interface-shallower-than-Implementation drift worth a new deepening candidate.

## Builder Notes

**Pattern 1** — A duplicated double-checked-locking skeleton across two methods extracts cleanly into one private helper parameterized by (cache, gate, key, fetch) - the same three-argument shape (a collection, a lock, a delegate) generalizes any "check cache, lock, re-check, populate" pattern regardless of what is being cached.
- How to recognize: Two or more methods with the identical control-flow skeleton (unlocked check -> await lock -> locked re-check -> compute -> conditionally cache -> release) differing only in which field/dictionary/gate they close over and how they compute the value on a miss.
- Smallest coding rule: Parameterize the skeleton by cache, gate, key, and a fetch delegate; leave any method whose caching RULE differs (not just its data source) unmerged - a genuinely different cacheability rule is a different shape, not the same shape with different data.
- Stack example: C#: `GetOrFetchGogNameAsync`/`GetOrFetchEpicNameAsync` now both delegate to `GetOrFetchNameAsync(Dictionary<string,string>, SemaphoreSlim, string, Func<Task<string>>)`; `FindGameByNameAsync` stayed separate because its miss (int 0) is cacheable while a failed fetch (exception) must not be - a rule difference, not just a data difference.

**Pattern 2** — Adversarially re-testing an accepted residual with a genuinely NEW proposed fix (not just re-reading the old rejection) sometimes surfaces a smaller alternative worth real consideration, even when the original rejection still holds - the value is in the fresh attempt, not just the confirmed answer.
- How to recognize: An accepted residual whose rejection rationale names one specific proposed fix (e.g. "a readonly struct fails Q2") - that rejection only proves ONE fix does not work, not that no fix exists.
- Smallest coding rule: Before re-confirming an accepted residual, name a different concrete fix than the one previously rejected and run it through the same five SPT questions from scratch; only re-use prior reasoning once a genuinely new alternative has also been tried and failed.
- Stack example: C#: GameEntry's parallel SteamGridDB-match fields were previously rejected via a readonly-struct proposal; this loop tried a factory-method/enum alternative instead and found it trades the same permissiveness for a differently-shaped ceremony burden - still rejected, but on fresh grounds.

**Pattern 3** — A perfect score (10) earned honestly under genuine adversarial pressure is worth re-testing again the next time a NEW dimension reaches it too - momentum from "the last two loops both held" is not evidence about a third dimension's own claim.
- How to recognize: Multiple scorecard dimensions reaching 10 across consecutive loops, especially when the newest one (simplicity, this loop) was not itself the subject of this loop's mandated extra-scrutiny instruction.
- Smallest coding rule: Extend whatever adversarial-pass discipline was mandated for one dimension to any OTHER dimension reaching the same ceiling in the same or a nearby loop, rather than letting the mandate's scope silently bound which claims get stress-tested.
- Stack example: C#: this loop's explicit adversarial mandate covered state_management/credibility (both already 10 from loop 8); simplicity independently reached 10 this loop too, on the strength of a comprehensive fresh sweep, not because the other two held.

**Scorecard humility check.** Three claims this loop's critic is least confident about: (1) `simplicity` moving to a full 10 this loop, the third dimension to reach a perfect score within two loops - the leaf-module sweep (helper plus direct reads of ~20 files) is real and thorough, but a stricter critic might note that "nothing found by two readers" is a weaker guarantee against a subtle three-or-more-site duplication cluster than the sweep's own four-angle framing implies, especially in generated/`obj/` XAML code-behind files neither pass opened. (2) Rejecting the async-void missing-try/catch asymmetry (`ShowGridPanelAsync`/`HideGridPanelAsync`/`ShowSearchPanelAsync`/`HideSearchPanelAsync` have no top-level catch, unlike their sibling network/file-I/O methods) as too speculative to raise as a Finding - the failure surface really is narrow (in-memory UI manipulation, no I/O), but if any of these ever does throw under a real Game Bar window-lifecycle edge case, an unhandled exception in an async void handler could crash the widget, a materially worse outcome than my "essentially inert" assessment assumes. (3) `domain_modeling`'s residual staying accepted after this loop's own Adversarial Pass considered a genuinely new factory-method alternative and rejected it on Q2 - a different critic could reasonably judge the two-step construction ceremony a net win worth the trade, in which case the residual should have been re-opened to the backlog rather than re-confirmed accepted.

## Final Judge Narrative
Place, and a genuinely re-verified one - this loop resolved F-020, the long-queued StoreNameLookup double-checked-locking duplication, by consolidating GetOrFetchGogNameAsync's and GetOrFetchEpicNameAsync's identical bodies into one shared private helper with zero public-interface change and zero XAML/PrimaryWidget.xaml.cs edits. Simplification helped: net line reduction at the two call sites, one shared implementation instead of two hand-synchronized copies, no new seam. The louder story is verification, not construction: this loop's own instruction called for genuine adversarial re-testing of loop 8's two same-loop jumps to 10 (state_management, credibility), and two independent blind helper passes plus a direct field-by-field census found zero counter-examples on either dimension after real effort to find one - both scores hold on fresh evidence, not deference. Simplicity also reaches 10 this loop on the strength of a comprehensive fresh sweep finding nothing beyond F-020. Runtime ownership and credibility are both genuinely trustworthy. Concurrency remains trustworthy; its one accepted residual (an inert discarded focus-Task) and domain_modeling's own accepted residual (GameEntry's parallel SteamGridDB-match fields) both survived a fresh Adversarial Pass this loop with newly-considered alternative fixes, not recycled reasoning. Tests reduce regression risk everywhere they can reach, but GridImage_Click's session guard stays outside that protection - PrimaryWidget.xaml.cs's own no-desktop-test-projection constraint, unchanged. architecture_quality and data_flow remain the two dimensions genuinely capping this below top-tier, both requiring a multi-file redesign disproportionate to any single loop's blast radius; F-011 stays the only backlog item, still blocked by the standing user constraint on per-game network-call concurrency. Future work risks nothing from this loop's own fix (behavior-preserving, cold-reviewed); the risk to watch is treating three same-loop-adjacent 10s (state_management/credibility last loop, simplicity this loop) as momentum rather than re-testing each on its own merits again next loop.

## Loop 9 Result
Replaced `GetOrFetchGogNameAsync`'s and `GetOrFetchEpicNameAsync`'s identical double-checked-locking bodies (StoreNameLookup.cs) with calls to one new shared private `GetOrFetchNameAsync(Dictionary<string,string> cache, SemaphoreSlim gate, string key, Func<Task<string>> fetch)` helper containing that logic once; both methods' public signatures and observable behavior are unchanged. `FindGameByNameAsync`'s distinct int-cacheable-zero/must-not-cache-failure shape was left untouched. Full build (msbuild, exit 0) and full test suite (run-tests.ps1: 167 passed/0 failed, unchanged before and after) both re-run - StoreNameLookup.cs's GOG/Epic methods have no direct tests by design (network-bound), so no new tests were possible or expected. A manual trace confirms 1:1 behavior preservation: both original methods' unlocked-check/await-gate/re-check-under-gate/fetch/cache-if-nonempty/release-in-finally sequence is byte-for-byte preserved inside the shared helper, just parameterized by which cache/gate/key/fetch-delegate to close over; the Epic path's `EpicLibrary.GetDisplayNameAsync ?? GetEpicGameNameAsync` fallback chain is reproduced in the same order. Finding F1 (stable_id F-020) is **resolved** - the duplicated skeleton no longer exists in current source. Independent implementation review (separate subagent, read-only, briefed cold on this finding and the diff only) returned verdict approved with all three checks (reality, honesty, regression) passed on the first pass. No unintended scorecard regression observed; `simplicity` moved UP as a direct consequence, and `state_management`/`credibility` (unaffected by this loop's own code change) were independently re-verified at 10 via a separate adversarial investigation pass earlier in this loop.
