"""What the current pipeline would put on the tile for games that are NOT in this library.

    python tools/badge-survey.py                     # 300 games, seed 1
    python tools/badge-survey.py --games 600         # a bigger sweep
    python tools/badge-survey.py --seed 2            # a different sample from the same pool
    python tools/badge-survey.py --refresh           # ignore the cache and re-query

Needs STEAMGRIDDB_API_KEY set (the same variable the widget reads) and Pillow.

WHY THIS EXISTS
---------------
Every storefront badge and mockup template the pipeline knows about was found the same way: a human
looked at a tile and said "that one is wrong". ARTWORK-SELECTION.md is explicit that adding a
rendering is a measurement, not a tuning exercise, and that the measurement starts from a reported
upload. So the bottleneck is not analysis, it is *eyes on covers this library has never produced*.

This builds that: a broad sample of games from SteamGridDB's own index, run through the current
selection logic end to end, rendered as a contact sheet. Anything that still shows a badge or a
template in that sheet is by construction a case the shipped pipeline misses.

WHAT IS FAITHFUL, AND WHAT IS NOT
---------------------------------
Faithful, because it is the same code or a direct port of it:

  * ranking          rank_grids/is_demoted/grid_metadata are IMPORTED from compare-artwork.py, whose
                     check 4 already holds them to the widget's own logged order. Not re-typed here,
                     because a second transcription of ArtworkRanker is a second thing to drift.
  * request filters  types=static&nsfw=false&humor=false&epilepsy=false and the 512/1024 dimensions,
                     plus the styles= rescue call when page 1 carries no text-bearing style.
  * badge check      the reference table is PARSED OUT OF BadgeOverlay.cs at run time, so a rendering
                     added to the app is picked up here without touching this file.
  * download walk    ArtworkDownloader.DownloadBestTileFillingImageAsync: up to 8 candidates in rank
                     order, badge check skips, tile-fill check accepts, first downloaded is the
                     fallback, then the official-artwork gate.

Not faithful, and deliberately:

  * game source      the widget walks the Xbox app's manifests; this samples SteamSpy's owner
                     ranking instead, addressed as /grids/steam/{appid} exactly as the widget would.
                     The candidate pool for a game is the same set of uploads either way.

                     The ranking matters: a sweep of SteamGridDB's autocomplete instead drew games
                     like "Super Alpaca Bros." and "Last Days Motel", and 10 of 12 had no square
                     artwork at all. Badges are composited by uploaders working through games people
                     own, so a sample that misses those games misses the badges too.

  * scope            every game must be sold by a store the app supports. SteamGridDB's autocomplete
                     returns a `types` list per game - the stores it is linked to - and that is
                     filtered against GamePlatform.cs's own table (steam, gog, egs, uplay, bnet,
                     origin). Mach Rider comes back `["eshop"]` and is dropped; a GOG or Epic
                     exclusive is kept, because the Xbox app lists those and this widget fixes them.

                     This matters more than it looks. The first run of this tool had no such filter,
                     and two of the three covers it surfaced were for games that were never released
                     on PC at all - a PS2 title and an NES one. Both were real templates. Neither
                     could ever reach a tile.
  * resampling       Pillow LANCZOS where the app uses WIC Fant. The badge measure has ~5 points of
                     slack below its limit and ~5 above it before the nearest clean candidate, so a
                     resampler difference cannot flip a verdict, but the printed distance can move by
                     a point or so. That is why the sheet prints the distance rather than just a flag.

READING THE SHEET
-----------------
Each card is the image the pipeline would actually write, with the badge distance under it. Three
bands matter:

    flagged      <= 10.0   already caught - these never reach a tile, and are shown only with --all
    near miss    10 - 25   the band where an unfitted rendering would sit. Worth the closest look.
    clear        > 25      nothing in the table resembles this corner

Click a card to flag it. "Copy flagged" puts a JSON list of {id, name, url} on the clipboard, which
is the input a fitting pass needs: grow the group from a reported upload, confirm by eye, average.
"""
import argparse
import importlib.util
import json
import io
import os
import random
import re
import sys
import urllib.parse
from concurrent.futures import ThreadPoolExecutor

try:
    from PIL import Image
except ImportError:                                                    # pragma: no cover
    sys.exit("Pillow is required for the thumbnails: python -m pip install pillow")

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(HERE)
BADGE_OVERLAY_CS = os.path.join(REPO, "SteamGridDB.Xbox", "Services", "Artwork", "BadgeOverlay.cs")
DEFAULT_OUT = os.path.join(os.environ.get("TEMP", "."), "steamgriddb-badge-survey")

# ---- GamePlatform.cs's own table: the stores this widget can fix a game for ---------------------
# A game sold by none of these cannot appear in the Xbox app's list, so artwork for it says nothing
# about what would ever land on a tile. Xbox and Custom are omitted deliberately - they have no
# SteamGridDB platform at all and are matched by name, so they cannot be filtered on here.
SUPPORTED_STORES = {"steam", "gog", "egs", "uplay", "bnet", "origin"}

# ---- ArtworkDownloader.cs / TileImage.cs / ArtworkSignature.cs ----------------------------------
MAX_CANDIDATES = 8
OFFICIAL_FLOOR = 0.60
OFFICIAL_CEILING = 0.85
COLOUR_GRID = 32
LAYOUT_GRID = 12

# The sample is drawn from SteamGridDB's autocomplete, swept over words that turn up in game titles.
# A fixed list rather than random strings so a run is reproducible and a second run with a new --seed
# draws from the same pool instead of a different one.
TERMS = """
war dark souls star super dead last city world legend quest space king blood night fire ice iron
steel ghost dragon knight hero lost final alien zombie robot ninja samurai pirate racing football
soccer farm sim tycoon craft mine build escape puzzle story tales chronicles saga rise fall age
empire total call duty battle field gun shoot hunt survival island forest mountain river sea ocean
sky moon sun storm thunder shadow light magic witch wizard demon angel god hell heaven soul spirit
dream nightmare memory time clock gear machine engine factory train car bike plane ship tank army
squad team club cup league master champion arena tower castle dungeon crypt tomb temple ruins secret
mystery murder detective crime prison hospital school garden house room door key gold silver diamond
crystal stone wood metal red blue green black white grey rogue like craft punk cyber neon retro pixel
adventure action horror sniper zone edge core rush drift boss hunter slayer keeper walker runner
rider fighter defender guardian ranger raider seeker rebel outlaw bounty frontier colony station
""".split()


def load_shared():
    """The ranker replay, the cached API client and the thumbnailer, taken from compare-artwork.py.

    Imported rather than copied. That file's check 4 re-derives ArtworkRanker.RankGrids from scratch
    and requires it to reproduce the order the widget logged for every game in the library, so what
    is imported here is a replay something already holds to account. A second transcription in this
    file would inherit none of that."""
    spec = importlib.util.spec_from_file_location(
        "compare_artwork", os.path.join(HERE, "compare-artwork.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


# =================================================================================================
# BadgeOverlay.cs, read rather than transcribed
# =================================================================================================

def load_renderings(path=BADGE_OVERLAY_CS):
    """Parses the reference table straight out of the C#.

    The alternative is a copy of 25 pixel tables in a second language, which would be wrong the
    first time a rendering is added to the app and silently wrong thereafter. The constants come
    from the same file for the same reason."""
    source = open(path, encoding="utf-8-sig").read()

    def const(name, pattern):
        m = re.search(pattern, source)
        if not m:
            sys.exit(f"could not read {name} from {os.path.basename(path)} - has it been renamed?")
        return float(m.group(1))

    scaled = int(const("ScaledSize", r"ScaledSize\s*=\s*(\d+)"))
    corner = int(const("CornerSize", r"CornerSize\s*=\s*(\d+)"))
    limit = const("badgeDistanceLimit", r"badgeDistanceLimit\s*=\s*([\d.]+)")

    body = source.split("renderings =", 1)[1].split("\n        };", 1)[0]
    out = []
    for block in re.finditer(r"//\s*(\w+)\s*-\s*(\d+)\s*px.*?new uint\[\]\s*\{(.*?)\}", body, re.S):
        name, declared, pixels = block.group(1), int(block.group(2)), block.group(3)
        packed = [int(v, 16) for v in re.findall(r"0x([0-9A-Fa-f]{8})", pixels)]
        if len(packed) != declared:
            sys.exit(f"{name}: comment says {declared} px, table has {len(packed)}")
        out.append((name, packed))

    if not out:
        sys.exit(f"no renderings parsed from {os.path.basename(path)}")
    return out, scaled, corner, limit


def badge_distance(im, renderings, scaled, corner):
    """BadgeOverlay.BadgeDistance: mean per-channel distance to the nearest rendering.

    Returns (distance, rendering name). Alpha is dropped rather than composited, which is what
    BitmapAlphaMode.Ignore does - a transparent corner keeps whatever RGB is stored under it."""
    px = im.convert("RGB").resize((scaled, scaled), Image.LANCZOS).load()

    best, best_name = float("inf"), None
    for name, rendering in renderings:
        total = 0
        for entry in rendering:
            index = entry >> 24
            r, g, b = px[index % corner, index // corner]
            total += (abs(r - ((entry >> 16) & 0xFF))
                      + abs(g - ((entry >> 8) & 0xFF))
                      + abs(b - (entry & 0xFF))) / 3.0
        mean = total / len(rendering)
        if mean < best:
            best, best_name = mean, name
    return best, best_name


def fills_tile(im):
    """TileImage.FillsTileAsync: a 6x6 block in each corner of a 32x32, transparent when over 40% of
    it is near-zero alpha, and the image fills the tile when fewer than two corners are."""
    px = im.convert("RGBA").resize((32, 32), Image.LANCZOS).load()
    transparent_corners = 0
    for cx, cy in ((0, 0), (26, 0), (0, 26), (26, 26)):
        n = sum(1 for y in range(cy, cy + 6) for x in range(cx, cx + 6) if px[x, y][3] < 64)
        if n > 14:
            transparent_corners += 1
    return transparent_corners < 2


# =================================================================================================
# ArtworkSignature.cs
# =================================================================================================

def centre_square(im, size):
    """TileImage.CentreSquarePixelsAsync: scale the short side to size, take the middle out."""
    im = im.convert("RGB")
    scale = size / min(im.size)
    w = max(size, round(im.width * scale))
    h = max(size, round(im.height * scale))
    scaled = im.resize((w, h), Image.LANCZOS)
    left, top = (w - size) // 2, (h - size) // 2
    return scaled.crop((left, top, left + size, top + size))


def signature(im):
    """(colour histogram, layout grid) - the two measures the official-artwork gate compares on."""
    colour = centre_square(im, COLOUR_GRID).tobytes()
    histogram = [0.0] * 64
    for i in range(0, len(colour), 3):
        histogram[(colour[i] // 64) * 16 + (colour[i + 1] // 64) * 4 + (colour[i + 2] // 64)] += 1
    magnitude = sum(v * v for v in histogram) ** 0.5
    if magnitude > 0:
        histogram = [v / magnitude for v in histogram]

    grid = centre_square(im, LAYOUT_GRID).tobytes()
    luma = [0.299 * grid[i] + 0.587 * grid[i + 1] + 0.114 * grid[i + 2]
            for i in range(0, len(grid), 3)]
    mean = sum(luma) / len(luma)
    deviation = (sum((v - mean) ** 2 for v in luma) / len(luma)) ** 0.5 or 1
    return histogram, [(v - mean) / deviation for v in luma]


def colour_match(a, b):
    return sum(x * y for x, y in zip(a[0], b[0]))


def layout_match(a, b):
    return sum(x * y for x, y in zip(a[1], b[1])) / len(a[1])


# =================================================================================================
# Sampling games this library has never seen
# =================================================================================================

def normalise(name):
    """Game names reduced to a comparison key, for "is this the same title" questions."""
    return re.sub(r"[^a-z0-9]", "", (name or "").lower())


def steamspy_games(pages):
    """Steam titles by owner count, 1000 per page, most-owned first.

    Page 0 is the games everyone has - and so the games most likely to already be in this library.
    Starting at page 1 keeps the sample to titles with real candidate pools while steering away from
    what the widget has already been graded on."""
    games = {}
    for page in pages:
        url = f"https://steamspy.com/api.php?request=all&page={page}"
        try:
            request = urllib.request.Request(url, headers={"User-Agent": "steamgriddb-xbox-survey/1.0"})
            with urllib.request.urlopen(request, timeout=90) as response:
                data = json.load(response)
        except Exception as ex:
            print(f"  steamspy page {page} unavailable ({ex})")
            continue
        for appid, game in data.items():
            if game.get("name"):
                games[f"steam/{appid}"] = {"id": appid, "name": game["name"],
                                           "segment": f"steam/{appid}",
                                           "lookup": f"games/steam/{appid}"}
    return games


def autocomplete_games(api, terms, workers=6, any_store=False):
    """Games SteamGridDB's autocomplete returns for the term list, deduplicated by id.

    This is what reaches the stores the owner ranking cannot see - a GOG, Epic, EA or Ubisoft
    release that never shipped on Steam is still a game the Xbox app lists and this widget fixes.

    The `types` field is the scope filter and it is free: it comes back on the same response, so a
    game linked only to eshop, ps or xbox is dropped without a second call. Games with no store
    link at all are dropped too - nothing addresses them."""
    found, dropped = {}, 0

    def one(term):
        return api.rows(f"search/autocomplete/{urllib.parse.quote(term)}") or []

    with ThreadPoolExecutor(max_workers=workers) as pool:
        for results in pool.map(one, terms):
            for game in results:
                if not (game.get("id") and game.get("name")):
                    continue
                if not any_store and not (set(game.get("types") or []) & SUPPORTED_STORES):
                    dropped += 1
                    continue
                found.setdefault(f"game/{game['id']}", {
                    "id": game["id"], "name": game["name"],
                    "segment": f"game/{game['id']}",
                    "lookup": f"games/id/{game['id']}"})
    if dropped:
        print(f"  dropped {dropped} results sold by no supported store")
    return found


def local_names(module):
    """Names already in this library, so the sample is genuinely a different set of games.

    Best effort - the run log is the cheapest source and is present whenever "Re-fix all games" has
    been used. Without it the sweep just is not filtered, which costs overlap, not correctness."""
    log = os.path.join(module.WIDGET_STATE, "last-fix.log")
    if not os.path.isfile(log):
        return set()
    try:
        _, games = module.parse_fix_log(log)
    except SystemExit:
        return set()
    return {normalise(g["name"]) for g in games}


# =================================================================================================
# The pipeline, end to end
# =================================================================================================

def candidate_pool(api, module, game):
    """The grids FixLibraryAsync would rank, including the styles= rescue call."""
    params = f"{module.FORK_GRID_DIMS}&{module.FORK_FILTERS}"
    pool = api.rows(f"grids/{game['segment']}", params)
    if pool is None:
        return None
    if not any(c.get("style") in module.TEXT_BEARING for c in pool):
        styles = ",".join(module.TEXT_BEARING)
        rescue = api.rows(f"grids/{game['segment']}", f"{params}&styles={styles}")
        if rescue:
            pool = rescue
    return pool


def official_capsule(api, game):
    """Valve's own capsule for the game, via the platformdata the game lookup already carries."""
    data = (api.get(game["lookup"], "platformdata=steam")["data"] or {}).get("data") or {}
    steam = (data.get("external_platform_data") or {}).get("steam") or []
    for entry in steam:
        capsule = ((entry.get("metadata") or {}).get("library_capsule_full") or {})
        path = (capsule.get("image2x") or capsule.get("image") or {}).get("english")
        if path:
            return f"https://shared.steamstatic.com/store_item_assets/steam/apps/{entry['id']}/{path}"
    return None


def decode(raw):
    try:
        im = Image.open(io.BytesIO(raw))
        im.load()
        return im
    except Exception:
        return None


def pick(api, module, game, badges, use_gate):
    """ArtworkDownloader.DownloadBestTileFillingImageAsync, including the official-artwork gate.

    Returns the record the sheet renders, or None when the game has nothing to show."""
    renderings, scaled, corner, limit = badges
    pool = candidate_pool(api, module, game)
    if not pool:
        return None

    ranked = module.rank_grids(pool, game["name"])
    fallback = None
    notes = []

    for position, candidate in enumerate(ranked[:MAX_CANDIDATES]):
        im = decode(api.image(candidate["url"]))
        if im is None:
            notes.append(f"{candidate['id']}: unreadable")
            continue

        if fallback is None:
            fallback = (position, candidate, im)

        distance, name = badge_distance(im, renderings, scaled, corner)
        if distance <= limit:
            notes.append(f"{candidate['id']}: badge ({name}, {distance:.1f}) - skipped")
            continue

        if not fills_tile(im):
            notes.append(f"{candidate['id']}: transparent corners - skipped")
            continue

        chosen = (position, candidate, im, distance, name)
        if use_gate:
            replacement = gate(api, module, game, ranked, position, im, badges, notes)
            if replacement:
                chosen = replacement
        return record(game, ranked, chosen, notes)

    if fallback is None:
        return None

    # Every candidate in reach was badged or did not fill the tile. The app writes the best-ranked
    # one anyway - a badged cover beats no tile - so that is what the sheet has to show.
    position, candidate, im = fallback
    distance, name = badge_distance(im, renderings, scaled, corner)
    notes.append("no candidate passed - best-ranked written as the fallback")
    return record(game, ranked, (position, candidate, im, distance, name), notes)


def gate(api, module, game, ranked, chosen_index, chosen_im, badges, notes):
    """ArtworkDownloader.FindOfficialLookalikeAsync."""
    renderings, scaled, corner, limit = badges
    url = official_capsule(api, game)
    if not url:
        return None
    capsule = decode(api.image(url))
    if capsule is None:
        return None

    official = signature(capsule)
    chosen = signature(chosen_im)
    chosen_colour = colour_match(official, chosen)
    if chosen_colour >= OFFICIAL_FLOOR:
        return None
    chosen_layout = layout_match(official, chosen)

    for position in range(chosen_index + 1, min(len(ranked), MAX_CANDIDATES)):
        candidate = ranked[position]
        if module.is_demoted(module.grid_metadata(candidate), game["name"]):
            continue
        im = decode(api.image(candidate["url"]))
        if im is None:
            continue
        distance, name = badge_distance(im, renderings, scaled, corner)
        if distance <= limit:
            continue
        candidate_signature = signature(im)
        if (colour_match(official, candidate_signature) > OFFICIAL_CEILING
                and layout_match(official, candidate_signature) >= chosen_layout
                and fills_tile(im)):
            notes.append(f"gate: {candidate['id']} replaced {ranked[chosen_index]['id']}")
            return (position, candidate, im, distance, name)
    return None


def record(game, ranked, chosen, notes):
    position, candidate, im, distance, name = chosen
    return {
        "gameId": game["id"],
        "segment": game["segment"],
        "name": game["name"],
        "artworkId": candidate["id"],
        "url": candidate["url"],
        "author": (candidate.get("author") or {}).get("name") or "",
        "style": candidate.get("style") or "",
        "notes": (candidate.get("notes") or "").strip(),
        "rank": position + 1,
        "pool": len(ranked),
        "distance": round(distance, 1),
        "nearest": name,
        "trace": notes,
    }


# =================================================================================================
# The sheet
# =================================================================================================

CARD = """<label class="card {cls}" data-d="{distance}">
  <input type="checkbox" value="{value}">
  <img loading="lazy" src="{thumb}">
  <b>{name}</b>
  <span class="meta">rank {rank}/{pool} &middot; {style} &middot; badge {distance}<i>{nearest}</i></span>
  <span class="notes">{notes}</span>
</label>"""


def build_sheet(records, out_path, api, module, limit, seed, terms_used):
    """Thumbnails are re-read from the API client's disk cache rather than carried through the run.
    300 decoded 1024-square covers is a gigabyte of RAM held for no reason; the bytes are already
    on disk from the download walk, so this costs a re-read and nothing else."""
    near = sum(1 for r in records if limit < r["distance"] <= 25)
    cards = []
    for r in sorted(records, key=lambda r: r["distance"]):
        cls = "flagged" if r["distance"] <= limit else ("near" if r["distance"] <= 25 else "clear")
        cards.append(CARD.format(
            cls=cls,
            distance=f"{r['distance']:.1f}",
            value=escape(json.dumps({"id": r["artworkId"], "game": r["name"], "url": r["url"]})),
            thumb=module.thumbnail(api.image(r["url"]), size=320) or "",
            name=escape(r["name"]),
            rank=r["rank"], pool=r["pool"], style=escape(r["style"] or "-"),
            nearest=escape(f" ({r['nearest']})") if r["distance"] <= 25 else "",
            notes=escape(r["notes"][:180] or ""),
        ))

    html = SHEET.replace("__CARDS__", "\n".join(cards))
    html = html.replace("__SUMMARY__", (
        f"{len(records)} games &middot; {near} in the 10-25 near-miss band &middot; "
        f"{sum(1 for r in records if r['distance'] <= limit)} already flagged &middot; "
        f"seed {seed} &middot; {terms_used} search terms"))
    open(out_path, "w", encoding="utf-8").write(html)
    return near


def escape(text):
    return (str(text).replace("&", "&amp;").replace("<", "&lt;")
            .replace(">", "&gt;").replace('"', "&quot;"))


SHEET = """<!doctype html><meta charset="utf-8"><title>Badge survey</title>
<style>
 :root{color-scheme:dark}
 body{background:#111114;color:#e6e6e9;font:14px/1.45 "Segoe UI",system-ui,sans-serif;margin:0;padding:20px}
 header{position:sticky;top:0;background:#111114;padding:12px 0 16px;border-bottom:1px solid #2a2a30;z-index:2}
 h1{font-size:18px;margin:0 0 6px}
 .sub{color:#9a9aa4;font-size:13px}
 .bar{margin-top:12px;display:flex;gap:8px;flex-wrap:wrap;align-items:center}
 button{background:#26262e;color:#e6e6e9;border:1px solid #3a3a44;border-radius:6px;padding:6px 12px;cursor:pointer;font:inherit}
 button:hover{background:#32323c} button.on{background:#3d6fd9;border-color:#3d6fd9}
 #grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(190px,1fr));gap:14px;margin-top:18px}
 .card{background:#1a1a20;border:2px solid transparent;border-radius:10px;padding:8px;display:flex;
       flex-direction:column;gap:4px;cursor:pointer;overflow:hidden}
 .card:has(:checked){border-color:#e04b4b;background:#2a1a1c}
 .card input{display:none}
 .card img{width:100%;aspect-ratio:1;object-fit:cover;border-radius:6px;background:#000}
 .card b{font-size:13px;font-weight:600;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
 .meta{color:#8f8f99;font-size:11px} .meta i{color:#6f6f79;font-style:normal}
 .notes{color:#6f6f79;font-size:11px;max-height:30px;overflow:hidden}
 .near .meta{color:#e0a24b} .flagged .meta{color:#e04b4b}
 body.only-near .clear,body.only-near .flagged{display:none}
 body.hide-flagged .flagged{display:none}
</style>
<header>
 <h1>What the pipeline would put on the tile &mdash; games outside this library</h1>
 <div class="sub">__SUMMARY__</div>
 <div class="sub">Click any cover carrying a storefront badge or a template. Sorted by badge distance,
  so the most likely misses are first. <b>near</b> = 10&ndash;25, the band an unfitted rendering sits in.</div>
 <div class="bar">
  <button id="near">Only the near-miss band</button>
  <button id="hide" class="on">Hide already-flagged</button>
  <button id="copy">Copy flagged</button>
  <span class="sub" id="count">0 flagged</span>
 </div>
</header>
<div id="grid">__CARDS__</div>
<script>
 const boxes = () => [...document.querySelectorAll('.card input')];
 const update = () => document.getElementById('count').textContent =
   boxes().filter(b => b.checked).length + ' flagged';
 document.getElementById('grid').addEventListener('change', update);
 document.body.classList.add('hide-flagged');
 const toggle = (id, cls) => document.getElementById(id).onclick = e => {
   document.body.classList.toggle(cls); e.target.classList.toggle('on');
 };
 toggle('near', 'only-near'); toggle('hide', 'hide-flagged');
 document.getElementById('copy').onclick = () => {
   const picked = boxes().filter(b => b.checked).map(b => JSON.parse(b.value));
   navigator.clipboard.writeText(JSON.stringify(picked, null, 2));
   document.getElementById('copy').textContent = 'Copied ' + picked.length;
   setTimeout(() => document.getElementById('copy').textContent = 'Copy flagged', 1500);
 };
</script>"""


# =================================================================================================

def main():
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--games", type=int, default=300, help="how many tiles to render")
    parser.add_argument("--seed", type=int, default=1, help="which sample to draw")
    parser.add_argument("--pages", type=int, nargs="+", default=[1, 2, 3],
                        help="steamspy owner-ranking pages; 0 is the most-owned 1000")
    parser.add_argument("--out", default=DEFAULT_OUT)
    parser.add_argument("--refresh", action="store_true", help="ignore cached API responses")
    parser.add_argument("--no-gate", action="store_true", help="skip the official-artwork gate")
    parser.add_argument("--repeat", action="store_true",
                        help="allow games from earlier sheets to be drawn again")
    parser.add_argument("--any-store", action="store_true",
                        help="drop the supported-store filter, admitting games never released on "
                             "PC. Off by default: those cannot appear in the Xbox app.")
    parser.add_argument("--workers", type=int, default=6)
    args = parser.parse_args()

    module = load_shared()
    badges = load_renderings()
    renderings, _, _, limit = badges
    print(f"{len(renderings)} renderings from BadgeOverlay.cs, limit {limit}")

    os.makedirs(args.out, exist_ok=True)
    api = module.Api(os.path.join(args.out, "cache"), refresh=args.refresh)

    mine = local_names(module)
    print(f"sourcing games" + (f", excluding {len(mine)} already in this library" if mine else ""))

    # Every SteamSpy entry has a Steam appid, so every game here shipped on PC. That is the whole
    # scope guarantee - it comes from the source, not from a filter applied afterwards.
    pool = steamspy_games(args.pages)
    print(f"  {len(pool)} Steam titles from steamspy pages {min(args.pages)}-{max(args.pages)}")
    pool.update(autocomplete_games(api, TERMS, args.workers, args.any_store))
    print(f"  {len(pool)} after the sweep of the other supported stores"
          + (" - SCOPE FILTER OFF" if args.any_store else ""))

    # Games any previous run already put in front of a human. Reviewing a sheet is the expensive
    # step here - the API calls are cheap and cached - so a second run that re-showed half the same
    # covers would be spending the only scarce resource twice.
    surveyed_path = os.path.join(args.out, "surveyed.json")
    surveyed = set()
    if os.path.exists(surveyed_path) and not args.repeat:
        surveyed = set(json.load(open(surveyed_path, encoding="utf-8")))
        print(f"  skipping {len(surveyed)} games already reviewed in an earlier sheet")

    # Sorted before shuffling so the sample depends on the seed alone, not on the order the sweep's
    # threads happened to finish in
    # Keyed by name, not by id: the same title is reachable as steam/{appid} and as game/{id}, and
    # what a human already looked at is the cover, not the address it was fetched from.
    outside = sorted((g for g in pool.values()
                      if normalise(g["name"]) not in mine and normalise(g["name"]) not in surveyed),
                     key=lambda g: g["segment"])
    random.Random(args.seed).shuffle(outside)
    print(f"  {len(outside)} in scope and not yet reviewed")

    # Drawn until the sheet is full rather than sampled once: roughly a third of games have no
    # square artwork at all, and a sheet that silently comes back a third short is a sheet whose
    # size says nothing about how much was actually looked at.
    records, drawn = [], 0
    with ThreadPoolExecutor(max_workers=args.workers) as executor:
        while len(records) < args.games and drawn < len(outside):
            batch = outside[drawn:drawn + args.workers * 8]
            drawn += len(batch)
            for r in executor.map(lambda g: pick(api, module, g, badges, not args.no_gate), batch):
                if r:
                    records.append(r)
            print(f"  {len(records)}/{args.games} tiles from {drawn} games")
    records = records[:args.games]

    json.dump(sorted(surveyed | {normalise(r["name"]) for r in records}),
              open(surveyed_path, "w", encoding="utf-8"))

    if not records:
        sys.exit("no games produced a tile - is the API key valid?")

    out_path = os.path.join(args.out, "badge-survey.html")
    near = build_sheet(records, out_path, api, module, limit, args.seed, len(TERMS))
    print(f"\n{len(records)} tiles, {near} in the near-miss band\n{out_path}")
    return out_path


if __name__ == "__main__":
    import webbrowser
    webbrowser.open(main())
