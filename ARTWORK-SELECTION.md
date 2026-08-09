# Artwork selection — where the quality ceiling actually is

Analysed 2026-08-03 against `30d3354`. **§4.1–§4.2, §4.6 and the console-badge vocabulary are now
implemented** — see [Status](#status). The remaining proposals are unchanged.

Three evidence sources, all reproducible:

- **The real library.** The Steam entries in the local Xbox app manifests, queried live against
  SteamGridDB. The first pass covered 134 games / 890 square (512²/1024²) candidates / 1577 icon
  candidates; a later refresh brought it to 153 games. The full library is Steam + 3 Epic + 1
  `ubi`; the capitalised `Gog`, `Ubisoft` and `CustomLibraryManagement` manifests each contain
  **zero** `gameCache` entries, so every statistic below is the Steam library unless stated
  otherwise.
- **13 other SteamGridDB clients**, cloned and read: the five official/community libraries and
  eight applications listed on the SteamGridDB docs page.
- **Three rounds of per-game grading** against side-by-side renders of every changed pick, with
  Valve's official capsule shown as a reference. 38 verdicts in total. Every threshold and every
  reverted idea below traces to one of them.

## Status

| Proposal | State |
| --- | --- |
| §4.1 official-artwork gate | **implemented** — 12 graded better, 0 worse |
| §4.2 `Width`/`Mime` deserialised, dead `Score` retired | **implemented** — resolution tie-break graded 3–1 |
| §4.2a PNG-over-JPEG tie-break | **tried and reverted** — graded 2 better / 7 worse |
| §4.6 request-side filters | **implemented** — no behaviour change, by design |
| console-store badge vocabulary | **implemented** — found by grading, not by analysis |
| §4.6b bare PlayStation console names | **implemented** — found in use; 5 candidates demoted, 1 pick moved |
| §4.6c corner-badge pixel check | **implemented** — 25 renderings, 603 of 4741 flagged, near-miss band human-reviewed |
| §4.3 icon fallback | **implemented** — but two of its three ideas graded worse; see below |
| §4.5 failures reported as misses | **implemented** |
| §4.4 JPEG written to `.png` | **implemented** — transcoded on save |
| §4.9 picker rescue fetch, edition-mismatch bug | **implemented** |
| §4.9 portrait crop for grid-less games | **implemented** — content-placed crop, won 23 of 34 |
| §4.7 record the applied artwork ID | **implemented** |
| §4.8 corner gate at 9% reach | outstanding, no evidence calls for it |

Net effect on the library: **18 of 150 picks change**, none graded worse.

---

## 1. How selection works today

`FixLibraryAsync` (`PrimaryWidget.xaml.cs:935`) per game:

1. `GET /grids/{platform}/{id}?dimensions=512x512,1024x1024` — one page, no other filters (`:1007`).
2. If page 1 contains no `alternate`/`white_logo`/`blurred` grid, re-request with
   `styles=` those three (the "rescue" call, `:1009-1012`).
3. `RankGrids` (`:1586`) — a lexicographic sort, five keys, stable so ties keep API order:

   | # | Key | Effect |
   | --- | --- | --- |
   | 1 | mockup vocabulary in notes/tags, **or** edition mismatch vs the game name | demote |
   | 2 | language is set and not `en` | demote |
   | 3 | style in `alternate`/`white_logo`/`blurred` | promote |
   | 4 | "official"/"offical"/store domain in notes/tags | promote |
   | 5 | `Score` descending | — |

4. `DownloadBestTileFillingImageAsync` (`:1309`) downloads up to the top 5 in order and returns
   the first whose corners are opaque (`ImageFillsTileAsync`, `:1355`), else the first downloaded.
5. If there are no grids at all, fall back to `icons.OrderByDescending(i => i.Score).First()`
   (`:1043`).

Separately, `GetGameByPlatformIdAsync` (`:578`) is already called per game to resolve the name.
That call matters — see §4.1.

History: `f228cbe` moved ranking client-side, `4a638d4` added the style tier, `02ee6c2` added
metadata/language/pixel layers, `595eb5b` and `8f07813` tuned the vocabulary from per-game grading,
`dfa22fb` reverted the press-kit boost. `fd90da6` and `30d3354` then fixed the whole-codebase
review findings — including the Epic ID bug that §3.4 was written about.

---

## 2. What the library data says

### 2.1 Three of the five ranking keys are dead or near-dead

| Key | Fires on | Note |
| --- | --- | --- |
| `Score` descending | **0 of 890 candidates** | every `score`, `upvotes` and `downvotes` in the pool is `0` |
| language | 5 of 890 | 885 `en`, 3 `ja`, 1 `fa`, 1 `zh` |
| notes/tags vocabulary | 279 of 890 have notes at all | the `tags` field **does not exist** in v2 grid responses — 0 of 890 |

`Score` is not a sampling artefact. The maintained .NET client annotates it directly:

> `[JsonProperty("score"), Obsolete("This property is marked as obsolete by API developer and left
> only for backwards compatibility. This property will always return false")]`
> — `craftersmine/SteamGridDB.NET`, `SteamGridDbObject.cs:26`

`upvotes`, `downvotes` and `lock` carry the same annotation. So `.ThenByDescending(r => r.Grid.Score)`
at `:1594` never reorders anything, and the icon fallback at `:1043` is just "the first icon the
API returned".

`GridMetadata` (`:1514`) concatenates `Notes` and `Tags`; the `Tags` half is always null, so the
vocabulary regexes see notes only, and 69% of candidates have no notes. **Further regex tuning is
working on 31% of the pool.** That is the ceiling the last three commits were pushing against.

### 2.2 Two thirds of picks are decided by API order alone

Replaying `RankGrids` over the real library:

```
games with square grids                      131
  only one candidate (nothing to rank)        21
  >1 candidate, winner tied with others       84   <- 64%, decided by API order
  ranking moved the pick off API order        27
```

For 84 games every ranking key ties across the top group and the stable sort falls through to
SteamGridDB's own ordering. Inside those tied sets: 33 span more than one style, 31 pick a JPEG
while a PNG is available, 6 have a higher-resolution candidate available.

These numbers survive `fd90da6`'s `RankedGrid` refactor. That change hoisted the metadata scan out
of the sort keys, but the key sequence and each key's polarity are unchanged — the new
`IsForeignLanguage` (`:1557`) is the exact negation of the old inline language key, and `Select`
preserves input order ahead of the stable sorts. Ordering is identical, so the replay above still
describes current behaviour.

### 2.3 The corner-transparency gate reaches ~9% of candidates

Measured over the 410 images the downloader would actually consider (top 4 per game):

```
candidates measured                410
non-PNG (jpeg)                     165
actually carrying an alpha channel  38   <- 9%
```

`ImageFillsTileAsync` can only reject an image that has alpha. Most PNGs in the pool are fully
opaque, so the gate is inert for 91% of candidates — not just the JPEG half that `02ee6c2`
already noted.

### 2.4 The icon fallback is in worse shape than the grid path

1577 icon candidates across the library:

```
mimes    image/png 826    image/vnd.microsoft.icon 751
styles   official 820     custom 757
dims     0x0 751 (every .ico reports 0x0)    512² 382    256² 236    1024² 156    128² 52
```

Three problems, all on the path taken by Make Way, Star Trek: Starfleet Academy and Star Trek:
Starfleet Command Gold Edition — the three games with no square grid at all:

- **48% of icon candidates are `.ico` files**, and `DownloadAndReplaceImageCoreAsync` writes the
  bytes to the game's `.png` path. A `.ico` renamed to `.png` is a very different proposition from
  a JPEG renamed to `.png`.
- **There is an `official` icon style** (820 of 1577) and the fallback does not ask for it, even
  though "official" is exactly the signal the grid ranker spends four regexes trying to infer from
  free text.
- The fallback sorts by `Score` (`:1043`), which is always 0, so it takes whatever the API returned
  first.

Note while touching this: `GetSquareIconsByPlatformIdAsync` (`SteamGridDbClient.cs:231`) accepts a
`dimensions` parameter and then discards it, hardcoding `{128, 256, 512, 1024}` on the next line.
Harmless today because no caller passes one, but it will silently ignore the argument if one ever
does.

### 2.5 The pool is already clean of the flags other clients filter on

`nsfw`, `humor`, `epilepsy` and `lock` are false for all 890 candidates — the API applies
`nsfw=false`/`humor=false` by default. Nothing to gain, but also no risk in being explicit.

Composition, for reference: styles `alternate` 704, `white_logo` 92, `no_logo` 87, `material` 6,
`blurred` 1. Mimes `image/png` 579, `image/jpeg` 311. Dimensions `1024²` 741, `512²` 149.

### 2.6 The picker now displays the dead score

`30d3354`'s sibling commit added a thumbnail tooltip (`GridImageItem.Description`,
`PrimaryWidget.xaml:449`) reading `Style / Author / Score`, described as making "the data behind the
ordering visible when picking artwork by hand". Style and author are genuinely useful. **`Score` is
`0` for every artwork in the library** (§2.1), so the tooltip currently tells the user that every
candidate scored zero — and implies that is a real signal being ranked on. Dropping it from the
tooltip should happen with §4.2.

---

## 3. What the other 13 clients do

Cloned and read: `node-steamgriddb`, `tauri-steamgriddb`, `SteamGridDB.NET`, `steamgriddb_api`,
`Steam-Art-Manager`, `clear`, `steamtinkerlaunch`, `SteamGridDBMetadata`, `UWPHook`, `GameHub`,
`steamgrid`, `steam-buddy`, `steam-rom-manager`. (`ZebcoWeb/python-steamgriddb` is gone from
GitHub.)

**None of them rank candidates client-side.** Every one either pushes filters into the request and
takes `data[0]`, or renders a picker and lets a human choose — `steamtinkerlaunch` literally
`jq -r ".data[$i].data[0].url"`, `steam-buddy` and `clear` map the response straight to
`{thumb, url}`, `UWPHook` and the Playnite extension hand the array to a UI. This project's ranking
layer is past the state of the art in that ecosystem. The transferable learnings are about *where
the art comes from*, *what the API will tell you if you ask*, and *what happens when things go
wrong*.

### 3.1 SteamGridDB is treated as a fallback source, not the source

`boppreh/steamgrid` — the most-used bulk artwork tool — orders its providers
(`download.go:359-405`):

```
official Steam CDN  ->  SteamGridDB  ->  IGDB  ->  Google image search
```

It only asks SteamGridDB when Valve's own art is missing. This is not one project's quirk:
`PhilipK/steamgriddb_api`, a *SteamGridDB client library*, ships a `SteamStaticUrls` struct as a
first-class part of its public API (`steam_static.rs`), pre-formatting header, capsule, hero and
logo URLs off `cdn.cloudflare.steamstatic.com`. The ecosystem consensus is that Valve's own assets
outrank community uploads.

Our app treats SteamGridDB as the sole source.

### 3.2 The API will hand you Valve's official assets — on a call we already make

`node-steamgriddb/src/index.ts:96` lists `platformdata` among the multi-value query params, and
nothing in the ecosystem uses it. Passing it to the *game lookup* returns Steam's own store asset
manifest:

```
GET /games/steam/1145350?platformdata=steam

data.verified                     true
data.types                        ["steam"]
data.external_platform_data.steam[0].id                              "1145350"
                                 ...metadata.library_capsule_full    {"image":{"english":"2ba1…/library_capsule.jpg"},
                                                                      "image2x":{"english":"2ba1…/library_capsule_2x.jpg"}}
                                 ...metadata.library_hero_full       {…}
                                 ...metadata.library_logo_full       {…}
                                 ...metadata.header_image_full       {"english":"91ac…/header.jpg"}
                                 ...metadata.logo_position           {"pinned_position":"BottomLeft", …}
                                 ...metadata.store_asset_mtime       1758926015
```

Serve those paths from `https://shared.steamstatic.com/store_item_assets/steam/apps/{appid}/{path}`
(the `shared.cloudflare.steamstatic.com` host 301s there; the old
`cdn.cloudflare.steamstatic.com/steam/apps/…` host 404s on the hashed form).

Two consequences:

- **No extra request.** `GetGameByPlatformIdAsync` (`:578`) already runs per game to resolve the
  name. Adding `?platformdata=steam` and deserialising two more fields costs nothing.
- **It resolves across stores.** `GET /games/egs/Sugar?platformdata=steam` returns Rocket League
  *with Steam appid 252950 attached*. So an Epic or GOG game that SteamGridDB has linked to Steam
  still yields Valve's official capsule.

Coverage over the 134 local games:

```
verified = true                134 / 134
library_capsule_full present   130 / 134
header_image_full present      132 / 134
library_logo_full present      128 / 134
neither capsule nor header       2       (Call of Duty: Modern Warfare II, Override)
```

Better than the 127/134 obtainable by blind-probing `library_600x900_2x.jpg`, and it needs no 404
probing at all — the field's presence *is* the availability check.

### 3.3 Request parameters we are not using

The full v2 filter set, per the official client: `styles`, `dimensions`, `mimes`, `types`,
`platformdata` (comma-joined) and `nsfw`, `humor`, `epilepsy`, `oneoftag`, `page` (single).

We send `dimensions` and sometimes `styles`. Everyone else sends more:

| Project | Sends |
| --- | --- |
| `steamgrid` | `styles`, `types=static` (default), `nsfw`, `humor`, `dimensions` |
| `steam-rom-manager` | `types`, `nsfw=false`, `humor=false`, `dimensions`, `styles` |
| `Steam-Art-Manager` | all of the above **plus `mimes`** and `epilepsy` |
| `steamtinkerlaunch`, `UWPHook`, Playnite `SGDBMetadata` | `styles`, `dimensions`, `types`, `nsfw`, `humor` |

Verified against the API with this library's IDs — all accepted. `&mimes=image/png` cuts one game's
23 candidates to 19; `&types=static` is accepted; `&page=1` returns 0 results, so a single page
covers every game here (largest pool is 25).

Enumerated mime values worth knowing (`steamgriddb_api/src/query_parameters.rs:445-483`): grids are
`image/png` / `image/jpeg` / `image/webp`; **icons are `image/png` / `image/vnd.microsoft.icon`**.
Steam Art Manager's shipped capsule defaults are `nsfw: false`, `epilepsy: false`, `humor: true`,
`untagged: true` (`Defaults.ts:25-56`).

### 3.4 Non-Steam platform IDs — the Epic fix is confirmed, Ubisoft still will not match

The Playnite extension maps platform IDs for Steam only, and has the other cases commented out with
an explicit rationale (`SGDBMetadataProvider.cs:47-79`):

> `// check for platform "steam""origin""egs""bnet""uplay"`
> `// only steam is reliable enough on sgdb`
> `// most games are not linked to other platforms`

It also skips ID matching for Steam entries whose ID exceeds int32 (Steam *mods* use larger
generated IDs) and for names ending in `" Demo"`, falling back to name search in both cases.
**Neither of those two cases is handled here** — worth keeping in mind if a Steam mod or demo ever
turns up matched to the wrong SteamGridDB entry.

`fd90da6` fixed the Epic ID parsing (last segment, not the catalog item ID) and removed
`XboxPlatformId` so the wrong identifier can no longer reach a fetch. Testing the local non-Steam
entries against the API confirms that was the right call, and bounds what it buys:

| Lookup | Result |
| --- | --- |
| `games/egs/Sugar` | Rocket League — 36 square grids |
| `games/egs/Fortnite` | Fortnite — 50 square grids |
| `games/egs/dc9d2e59…` (opaque GUID appName) | miss |
| `games/uplay/5266` | miss |
| `games/ubi/5266` | miss |

So two of the three local Epic entries now work. The third has a GUID for an appName and cannot be
matched by ID. The Ubisoft entry misses under both `uplay` and `ubi` — it needs name search, which
is the fallback Playnite settled on.

### 3.5 Failure handling everyone else has and we do not

`steam-rom-manager` wraps every provider request in a retry layer (`x-request-wrapper.ts:32-55`):
3 retries, and on **HTTP 429 it reads `Retry-After` and reschedules** rather than failing.
`steamtinkerlaunch` passes `--tries="${SGDBRETRIES}"` to wget.

`SteamGridDbClient.GetAsync` (`SteamGridDbClient.cs:309`) still logs a non-200 to `Debug` and
returns `null`; the wrappers then convert that to an empty list. `FixLibraryAsync` sees no grids,
falls to the icon branch, sees no icons, and increments `notFoundCount` (`:1057`).

`30d3354` improved the accounting — unsupported platforms are now counted and reported separately
as `skipped (unsupported platform)` (`:998`, `:1078`). That closes the progress-arithmetic half of
the problem. The half that remains is the one that matters for artwork quality: **a rate limit or
transient 5xx is still reported as "had no artwork in the database"** (`:1074`). Nothing
distinguishes "SteamGridDB does not have art for this game" from "we were throttled".

### 3.6 Two more habits worth copying

- **Record which artwork was chosen.** Steam Art Manager caches the selected grid per app
  (`CacheController.cacheSelectedGrid` → `userSelectedGrids[appId][type]`) alongside a cached copy
  of the original asset. We keep the original as `.bak` but never record the chosen grid's ID, so
  "Re-fix all games" is not idempotent — if SteamGridDB's ordering shifts, picks silently move, and
  a graded run cannot be reproduced afterwards.
- **Fuzzy-rank name search results.** `steamgrid` runs `fuzzy.Sort(searchResults, game.Name)`
  (`download.go:198`) and `steam-rom-manager` uses `fuzzysort` before choosing. Our manual search
  appends `/search/autocomplete/` results in API order, so "hades" lists Hades, Hades' Star, Hades
  Ultimate Fighting Ball in whatever order the server chose. The response also carries `verified`
  and `types` per result, neither of which we surface.
- **`steam-rom-manager` drops candidates whose URL ends in `?`** (`steamgriddb.worker.ts:47`,
  comment: "Nintendo Sucks") — malformed CDN URLs. Not present in this library, one-line insurance.
- **`steamgrid` requests one dimension at a time**, HQ then LQ, because requesting both "will give
  us scrambled results with no indicator which result has which size" (`download.go:147`). We hit
  exactly this — we ask for `512x512,1024x1024` together and cannot prefer the 1024, because
  `width`/`height` are not deserialised. Deserialising is the cheaper fix than a second request.

---

## 4. Proposals, in order of expected gain

None of these were addressed by `fd90da6` or `30d3354`; those commits fixed correctness, memory and
accounting, and explicitly left ranking behaviour unchanged.

### 4.1 Rank against Valve's official capsule, fetched via `platformdata` — IMPLEMENTED

Shipped as `FindOfficialLookalikeAsync`. What grading changed from the design below:

- **It is a gate, not a ranking key.** Ranking by similarity outright would have moved 73 of 106
  picks, including several already graded good. Walking the ranked list and vetoing was both safer
  and cheaper — 17 of 20 replacements sit at rank 2, so it fits inside the existing download loop
  rather than needing a thumbnail for all 890 candidates up front.
- **The margin condition is load-bearing.** A first implementation kept only the floor and dropped
  the ceiling. That let Hi-Fi RUSH move on a similarity gain of 0.57 → 0.616, which is inside the
  measure's own noise, and it graded worse. Restoring the ceiling removed 5 of the 6 problem cases
  in that round on its own.
- **A second, structural measure was needed.** Marvel Rivals graded as "closer to Valve but only in
  colour, not in actual image" — a failure a colour histogram cannot see. `ArtworkSignature` now
  carries a contrast-normalised luma grid alongside the histogram, and a replacement must not
  regress it. That measure scores Marvel Rivals −0.02 → −0.01 (no relationship) and Hi-Fi RUSH
  +0.60 → +0.53 (the original was structurally better), rejecting both independently.
- **Replacements must not themselves be demoted.** Without this the gate picked the "PlayStation
  Hits" upload for LEGO Worlds — it scores highly precisely because it *is* the real cover, with a
  storefront banner across it. With the guard, LEGO Worlds keeps its current art.
- **The floor is 0.60, not 0.50.** At 0.50, Mad Max sat on a 0.51 match while four candidates above
  0.85 went untouched, and graded "both wrong". 0.60 fixes it and changes one other game (Totally
  Accurate Battlegrounds, 0.59 → 0.87), both improving on colour and layout together.

Final graded result: 12 better, 4 about the same, 0 worse.

The original analysis follows, since the numbers still describe why the approach was chosen.

### 4.1a The original case for the gate

The 64% of games decided by API order cannot be improved by more vocabulary: the metadata is not
there. The official store capsule is a hard signal, available for 130 of 134 games, and — per §3.2 —
obtainable from a request the app already makes.

Implementation: add `?platformdata=steam` to `GetGameByPlatformIdAsync` (`:578`), deserialise
`external_platform_data.steam[0].metadata.library_capsule_full.image2x.english`, fetch that one
image per game, centre-crop both it and each candidate to square, compare coarse colour signatures.

Tested over the 106 Steam games with ≥2 candidates and an available capsule:

- Similarity spreads well: p10 `0.267`, median `0.825`, p90 `0.983`.
- Ranking purely by similarity would change **73 of 106** picks — too aggressive to ship blind.
- A **veto**, applied only when today's pick is visually unrelated to the official art and an
  alternative clearly is not, changes **10 of 106**:

| Game | today's pick | best alternative |
| --- | --- | --- |
| SteamVR | 0.023 | 0.992 |
| Lossless Scaling | 0.047 | 0.992 |
| SUPERHOT | 0.081 | 0.970 |
| BioShock Infinite | 0.105 | 0.949 |
| Wallpaper Engine | 0.132 | 0.943 |
| Prince of Persia: The Lost Crown | 0.156 | 0.932 |
| BeamNG.drive | 0.162 | 0.984 |
| LEGO Worlds | 0.429 | 0.927 |
| A Short Hike | 0.434 | 0.994 |
| Star Wars: Battlefront II | 0.465 | 0.936 |

Gate: pick `< 0.50` and some top-6 alternative `> 0.85`. Loosening to `< 0.60` / `> 0.90` adds one
game, so the set is not threshold-sensitive. These are exactly the "no metadata to work with" cases
the last commit deferred — BeamNG.drive is named in `8f07813`.

Cost: one image fetch per game from Valve's CDN (not the SGDB quota), plus decoding candidates the
app already downloads. Applies to any game SteamGridDB has linked to Steam, including the two Epic
entries that `fd90da6` just made matchable.

Caveat: a 4×4×4 colour histogram was enough to demonstrate the separation, but it is coarse. If the
graded result is mixed, a perceptual hash over a downscaled luma image is the next step, not a
threshold tweak.

### 4.2 Deserialise `width`, `height`, `mime` — then retire the dead `Score` key — IMPLEMENTED

`Width`, `Height` and `Mime` are now on `SteamGridDbGrid`; `Score` is retained with a comment saying
it is always 0 and must never be ranked on. `RankGrids` ends on `.ThenByDescending(r => r.Grid.Width)`,
and the picker tooltip shows the size instead of the dead score.

`nsfw`, `humor` and `epilepsy` were deliberately **not** added: §4.6 filters them at request time, so
deserialising them would add three permanently-false fields.

**The PNG-over-JPEG tie-break was tried and reverted.** It moved 26 picks and graded 2 better against
7 worse — Among Us, Elden Ring, Terraria and Shadow of War among the losses. Two lessons worth
keeping: format says nothing about whether the art is the game's real cover, and the justification
given here ("decodable by the corner gate") was actually backwards — PNGs are the only images that
*can* have transparent corners, so preferring them surfaces more candidates the gate then rejects.
The `.png` filename problem is real but belongs to §4.4, at the download, not the ranking.

The resolution tie-break graded 3 better / 1 worse in isolation, and the one loss (The Walking Dead:
Saints & Sinners) was subsequently fixed by the §4.1 gate.

### 4.3 Fix the icon fallback — IMPLEMENTED, but almost none of the plan survived

The proposal was three changes: prefer `styles=official`, reject `image/vnd.microsoft.icon`, and
sort by size. Grading 108 games killed two of the three.

| Deciding key | n | today better | proposed better | same | verdict |
| --- | --- | --- | --- | --- | --- |
| format — PNG over `.ico` | 83 | 30 | 29 | 23 | no signal |
| style — `official` over `custom` | 17 | 8 | 3 | 6 | **actively worse** |
| size — larger first | 8 | 1 | 6 | 1 | the only winner |

- **Preferring PNG is worthless**, exactly as it was for grids (§4.2). Two independent gradings now
  say format does not predict whether artwork is good.
- **`official` is a trap.** SteamGridDB's official icon is frequently the small platform icon:
  `Unrailed!` 128px official against a 512px custom, `Wolfenstein II` the same, `A Building Full of
  Cats` 256 against 512. A label was outranking size.
- **No whole-list ordering beat the API's own order.** Sorting by size, by format, by style, or any
  pairing, all landed at 37–39 correct out of 77 decisive verdicts. Coin flips.

What shipped is the one rule the data supports: keep the API's order, and among icons of the same
*kind* — same format, same style — take the largest. That moved 14 picks on the graded set, 6 onto
preferred artwork and 1 onto rejected. `RankIcons` also replaces the sort on the retired `Score`,
which was sorting on a constant.

**This changes no tile in the library today.** All three games that actually fall back to an icon
keep their pick: Make Way's two icons already had the better one first, and both Star Trek entries
have exactly one icon. The value is in the picker ordering and in not carrying a dead sort.

Also fixed here: the discarded `dimensions` parameter noted in §2.4.

### 4.4 Stop writing JPEG bytes into a `.png` file — IMPLEMENTED

Transcoded on save (`EnsurePngAsync`), rather than filtered at request time or preferred in ranking.

Both of the other options are ruled out by grading. Preferring PNG in the ranking graded 2–7 against
for grids and 30–29 for icons — format is not a quality signal. Requesting `mimes=image/png` would
have discarded 35% of the candidate pool to fix a naming problem, and would have taken the artwork
graded best for several games with it.

Converting sidesteps the question: artwork is chosen on merit, then the bytes are made to match the
name the Xbox app owns. Roughly 45% of picks and about half of all icons needed it. Windows imaging
sniffs content, which is why mislabelled files have worked so far - that is luck, not a contract,
and the mismatched bytes also flowed into the `.bak` and `.new` siblings. Conversion failure falls
back to writing the original bytes: a mislabelled tile that renders beats no tile.

### 4.5 Distinguish "no artwork" from "the request failed" — IMPLEMENTED

The artwork fetches now return `null` when the *request* failed and an empty list only when
SteamGridDB genuinely has nothing, and `FixLibraryAsync` counts the former as an error rather than
reporting "had no artwork in the database".

This was the quiet one. Before it, a throttled run of 130 games was indistinguishable from a library
with no artwork available - which would have silently corrupted every grading round in this
document. Worth doing before any further comparison, not after.

Retry with `Retry-After` on 429, as `steam-rom-manager` does (§3.5), is still outstanding.

### 4.6 Send the filters everyone else sends — IMPLEMENTED

`types=static`, `nsfw=false`, `humor=false`, `epilepsy=false` now go on every request from `BuildUrl`.
Verified accepted on both the grids and icons endpoints with identical result counts, which is the
point: zero change today, and an animated or flagged upload appearing later cannot silently become a
tile.

### 4.6a Console-store badge vocabulary — IMPLEMENTED

Not in the original analysis. It came out of grading: three of six flagged picks in one round had a
storefront badge burned into otherwise-correct art — a Steam logo, a "PlayStation Hits" banner.

The similarity gate *promotes* these, because the art underneath really is the official cover. The
existing mockup vocabulary misses them for the same reason — they are not mockups, they are branded
reissues. But the labels are sitting in the notes: 26 candidates across the library carry terms like
`PlayStation Hits`, `Switch Icon`, `PS5 dashboard icon`, `Xbox One`. Two of them
(`Official - Nintendo Switch`) were being *boosted* as official artwork.

`consoleBadgeGridMetadata` now demotes them, and `IsDemotedGrid` is consulted by the gate as well as
the ranking. `greatest hits` is deliberately excluded — one upload advertises being the *non*-Hits
version. Xbox terms are included at the user's request, despite this project targeting the native
Xbox look.

### 4.6b The PlayStation half of that vocabulary was missing — IMPLEMENTED

Found in use, not by analysis: Far Cry 6 shipped a tile with a **PS4 spine down its left edge**.

The Xbox names went in bare (`xbox one`, `xbox series`); the PlayStation ones only went in as
badge-shaped phrasings (`playstation hits`, `ps5 dashboard icon`, `ps4 square`). One uploader had
posted the same Far Cry 6 cover four times, once per console, and the notes said so exactly —
`Playstation 4`, `Playstation 5`, `Xbox One`, `Xbox Series S/X`. Half the batch was demoted and half
of it was not, so `682497` won rank 1 of 23 on a game whose own metadata named the problem.

`playstation ?[1-5]` closes it. Replayed over the whole library (191 games, 187 rankable):

```
candidates newly demoted     5   Far Cry 6 x2, Forza Horizon 5, Fallout 4, Far Cry 3
picks that move              1   Far Cry 6 only - the other four were already losing on rank
```

Two things this case says that the earlier rounds did not:

- **The similarity gate could not have caught it, twice over.** Far Cry 6 is a Ubisoft entry matched
  by name, so it has no Steam link and `capsule=none` — but even with a capsule the gate would have
  *approved* the pick, because a badged cover is the real cover and scores ~0.9 against it. §4.6a
  said this in the abstract; this is the first tile that shipped because of it.
- **A vocabulary is only as good as its symmetry.** The hole was not a missing idea, it was one
  console family written in a narrower form than the other in the same regex.

**An `MS Store` boost was considered and rejected.** `615770` is the same cover *without* a badge,
noted `Taken from the MS Store`, and boosting that phrase lands Far Cry 6 exactly on it. It was left
out because the phrase matches **one candidate in the entire library** — a rule with a sample of one
is the thing this document keeps learning not to ship. Without it Far Cry 6 ranks onto minimalist
fan art (`173475`); the correct cover is one click away in the picker.

Still out of reach of any vocabulary, and visible in the same candidate set: `173473` has
**GOLD EDITION** printed across the art with an empty notes field, and `681879` carries a PC/Windows
spine with no notes at all. Catching those needs an edge-spine detector, not more words — related to
§4.8, and unevidenced for now.

### 4.6c The badge that has no notes at all — IMPLEMENTED as a pixel check

Reported from the Xbox app: a Steam roundel burned into the corner of Rayman Legends, Risk of Rain 2,
OlliOlli World, Plants vs. Zombies GOTY, Slay the Spire, Tabletop Simulator, REMATCH and PEAK.

§4.6a and §4.6b both work by reading what the uploader wrote. **These say nothing.** One uploader has
41 covers in the test library, in a single contiguous ID block, every notes field empty, every one
carrying the same composited overlay. Two more uploaders do the same. No vocabulary can reach this,
and the official-artwork gate rates it *highly* — the art under the badge is the real cover.

Two vaguer measures were tried against the library first and are worth recording as failures:

| measure | result |
| --- | --- |
| contrast-normalised corner template correlation | recall 37%, and it scored the reported Rayman Legends case 0.57 — a miss |
| "corner is a flat colour absent from the middle" | no separation at all; clean artwork scored **higher** than badged artwork, because most cover art has a flat corner |

What works is exploiting the one property a composited overlay has: it is bit-for-bit identical no
matter what is underneath. Averaging the badged corners and measuring per-pixel spread *across
different games* separates the overlay (constant) from the art showing past it (not), which derives
the mask from the data instead of drawing it by hand. `BadgeOverlay` then measures mean per-channel
distance to that reference over those pixels only.

One reference per **rendering**, not per badge design — the hardest-won part. The dark Steam tab is
drawn by several uploaders at slightly different scales, and a reference averaged across them matches
any dark corner: one such fit claimed 226 of 861 candidates. Fitted per rendering, the same images
separate by a factor of thirty.

**Twenty-five renderings** are described: the Steam roundel tab in five, the "STEAM" case spine, a PC
corner tab and two PC spines, a "PC monitor" tab, the PlayStation 3/4/5 and Xbox One/Series/360
spines, and the Epic, Ubisoft, Wii, Play Store and Nintendo Switch badges. 603 of 4741 candidates
flagged; worst flagged 7.9, smallest per-rendering margin 14.6, limit 10.0.

**The corpus is deliberately not this developer's library** — 1000 of the most-owned Steam titles plus
a broad autocomplete sweep, 5.5× the library the first version was measured on. Its near-miss band was
then reviewed candidate by candidate by a human, which is where five of these renderings came from and
which is the only reason the two rules below exist.

### What review caught that measurement did not

**A rendering must flag more than one game, and those games must not be one franchise.** Two failures
established this, and neither was visible in any statistic:

| admitted | "overlay" | why it was wrong |
| --- | --- | --- |
| LEGO 2K Drive | the LEGO brick logo | the game's own title art |
| Bloons TD 6 | shared cover pixels | fitted across two uploads of the *same cover*, one badged and one not — so it flagged the clean one |

A later automated sweep reproduced the same failure one level up: four Half-Life 2 titles sharing
title art, two Capcom Arcade Stadiums, two Jackbox packs. A storefront badge is by definition
something that appears on unrelated games; franchise art is constant across a franchise's uploads for
a quite different reason, and nothing but the spread of games distinguishes them.

### Rejected rather than admitted at a lower bar

- A second Epic variant — flagged 215 candidates with **no margin at all**.
- The "PlayStation Hits" banner — two renderings, margins 6.9 and 1.5.
- A **Nintendo Switch** rendering on Bayonetta, present on that one game and nothing else in 4741
  candidates, so indistinguishable from Bayonetta's own art. It is the nearest unflagged candidate in
  the corpus at 17.1 — a known miss, kept as one rather than guessed at.
- **Games for Windows Live** — a single sample, which cannot be fitted at all.
- Two consistent *non-badge* templates: floppy-disk mockups with handwritten labels across 27 games,
  and a glossy rounded-icon frame across 9. Both make poor tiles, but neither is a storefront badge —
  the mockups belong to the notes vocabulary and the icon frames to the tile-fill check.

It lives in `ArtworkDownloader`, not in `ArtworkRanker`: ranking has no pixels, and the downloader
already walks candidates in rank order decoding each one. A badged candidate is skipped the way one
that fails the tile-fill check is, and the same fallback applies — if every candidate in reach carries
a badge, the best-ranked is still written, because a badged cover beats no tile. The gate consults it
too, for the same reason it consults `IsDemotedGrid`.

Effect: **8 of 189 picks move, all to rank 2**, so it costs one extra download on the games it fires
on and nothing on the rest. Two more games outside that replay's corpus also move — Stumble Guys
(725323 at rank 1) and Vampire Survivors (689258 at rank 2, which the tile-fill check had reached).

**Adding a rendering is a measurement, not a tuning exercise.** Grow the group from a reported
upload, confirm the members by eye, average them, keep the pixels whose spread across different
games is lowest. A group whose members do not then sit far below the limit is not one rendering and
must be split. Three fits were rejected this way before the dark tab came out as three renderings
rather than one.

Known limit: only the Steam tab is described. A clustering pass over the library found the rest as
their own renderings — the PlayStation and Xbox spines (Far Cry 6's set, which measures 36.09 against
the Steam references, correctly), Epic, LEGO, Play Store, Xbox 360, "PlayStation Hits" and Nintendo
Switch. None is described yet.

An uploader blocklist was considered first and reaches the same 8 picks on this library. It was
rejected: it names people rather than describing artwork, and it cannot generalise — the pixel check
found the other two uploaders on its own.

### 4.7 Record the applied artwork ID — IMPLEMENTED

`AppliedArtworkStore` keeps a path → artwork-ID map in the widget's local data, written whenever a
tile is replaced and cleared when the Xbox app's original is restored.

Once a tile is written it is just a PNG — the artwork it came from is unrecoverable from disk. That
had three costs, and the third is the one that actually hurt during this work: the picker could not
show which artwork was in use, re-fixing silently reshuffled picks whenever SteamGridDB's ordering
moved, and **every grading round in this document had to rebuild its comparison from scratch**
because nothing knew what the previous run had chosen.

The picker now marks the applied artwork with an "in use" badge. Keyed by image path, matching what
the bulk operations already deduplicate on — stale Xbox app manifests list one image under several
entries.

### 4.8 Reconsider the corner gate rather than tune it

At 9% reach it is not doing the job its comment claims. Two options:

- Accept it as a narrow PNG-only guard and say so in the comment.
- Add a flat-background measure: modal border colour, fraction of the image within tolerance of it.
  Over the library, only **4 of 131** picks exceed 60% flat background — Skyrim SE (85.7%, with a
  5.4% alternative at rank 1), SteamVR, Injustice 2, Heavy Rain. Small, and it carries the same
  false-positive risk against minimalist covers that got the padding detector rejected in `595eb5b`.
  Worth it only if those four grade as wins.

### 4.9 Loose ends

- ~~**The picker does not do the rescue fetch.**~~ **Fixed.** `LoadGridSelectionPanelAsync` now makes
  the same `styles=`-filtered follow-up as `FixLibraryAsync` when page 1 is all icon-like, so the
  picker can no longer offer a strictly worse set than auto-fix chose from.
- ~~**`IsEditionMismatch` demotes everything when the name is unknown.**~~ **Fixed** — an unresolved
  name is no longer treated as evidence of a mismatch. No effect on the current library, where every
  game resolves a name; it was demoting every edition-labelled candidate on no evidence.
- **Manual search results are unranked.** Per §3.6 — fuzzy-sort against the search term, and
  surface `verified` / `types`, both of which the autocomplete response already returns.
- ~~**3 games have no square grid at all.**~~ **Fixed.** Make Way, Star Trek: Starfleet Academy and
  Star Trek: Starfleet Command Gold Edition now take cropped portrait box art instead of an icon —
  two of the three were being handed a `.ico`. See §4.9c.

Two items from earlier revisions of this document are now **fixed** and have been removed:
`GridMetadata` calling uncompiled inline `Regex.Replace` (now uses the compiled fields, `:1518-1520`),
and `FixLibraryAsync` re-implementing the style-tier check instead of calling `GridStylePriority`
(`:1009`).

---

## Remaining order

1. **§4.4** — the `.png` filename problem, now that the ranking-level attempt at it is known not to
   work. `mimes=image/png` at request time or transcoding on save are the two live options.
3. **§4.7, §4.8, §4.9** — only if graded results justify them.

## What the grading rounds taught

Worth keeping, because none of it came from analysis:

- **Every plausible-sounding rule needs grading before it ships.** The PNG tie-break read as
  obviously correct and was 2–7 against for grids, then 30–29 for icons. "Prefer the official one"
  read as the safest rule in the document and was 3–8 against.
- **Decompose stacked changes before judging them.** Both the grid and the icon proposals bundled
  several keys and graded as a wash overall; splitting each by which key decided the pick found the
  one worth keeping and the ones to drop. An undecomposed verdict would have thrown away resolution
  along with format.
- **Thresholds need a margin, not just a level.** Two separate failures — Hi-Fi RUSH moving on a
  0.046 gain, Mad Max held back by 0.01 — were both edge effects at a hard boundary.
- **One metric is not enough.** Colour similarity and layout similarity fail in different places;
  requiring both is what makes the gate safe enough to be narrow.
- **The user's eye finds signals the data does not surface.** Store badges were invisible in every
  aggregate statistic and obvious in a side-by-side render — and once named, turned out to be
  sitting in the notes field all along.

Reproduction scripts (library fetch, rank replay, pixel measures, reference matching) are in the
session scratchpad; the cloned reference implementations are under `scratchpad/repos/`.
