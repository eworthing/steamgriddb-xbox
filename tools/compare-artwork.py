"""Side-by-side of what this fork picked against what upstream 1.4.0.0 would have picked.

    python tools/compare-artwork.py            # build the report from the last "Re-fix all games" run
    python tools/compare-artwork.py --refresh  # ignore the cached API responses and re-query
    python tools/compare-artwork.py --out X    # write somewhere other than the default temp folder

Needs STEAMGRIDDB_API_KEY set (the same variable the widget reads) and Pillow for thumbnails.

WHERE EACH SIDE COMES FROM
--------------------------
The dev side is not simulated. `last-fix.log` records, per game, the candidate count, the top of
the ranked order, every gate and badge decision, and the artwork id that was actually written; the
id is cross-checked against `applied-artwork.json`, which is what the picker reads back to mark a
tile "in use". So the left-hand column is a recording of a real run.

The prod side is a replay of upstream/master (`fdd334e`, 1.4.0.0 - the commit this fork was taken
from), transcribed from its source rather than guessed at:

  * library load   GET /games/{platformString}/{externalPlatformId} decides HasSteamGridDBMatch,
                   and `FixLibraryAsync` only visits games where that matched. externalPlatformId
                   is the entry id after the first ':', except Epic, where upstream takes parts[2] -
                   the catalog item id, not the appName. That is why its Epic games never resolve.
  * fix            GET /grids/{platformString}/{xboxPlatformId}?dimensions=512x512,1024x1024
                   then grids.OrderByDescending(g => g.Score).First().
                   xboxPlatformId is always the entry id after the first ':', so Epic keeps the
                   whole "namespace:catalog:appName" tail.
                   With no grids: GET /icons/...?dimensions=128,256,512,1024, same First().
  * platform names steam, gog, egs, uplay, bnet, origin. Anything else is skipped outright, and
                   first-party Xbox games are not in ThirdPartyLibraries at all, so upstream never
                   enumerates them.

`Score` is 0 for every artwork the API returns - it is retired, and the maintained .NET client
marks it [Obsolete]. `OrderByDescending` is a stable sort, so a constant key leaves the input order
alone and upstream's pick is exactly `data[0]`. Check 1 re-proves that against every candidate in
every pool rather than taking it on trust.

WHAT THE CHECKS ARE FOR
-----------------------
Every number in the report depends on assumptions that could rot: the API could reorder a pool
between the run and the replay, a game name could join to the wrong store id, the ranking could
have been changed in the source without this script noticing. Checks 1-6 fail loudly on each of
those, and their results are written into the report so a stale one cannot look authoritative.
Check 4 is the strongest: it re-implements ArtworkRanker.RankGrids from scratch and requires it to
reproduce the top 5 the widget logged, for every game.
"""
import argparse
import base64
import hashlib
import io
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import webbrowser
from concurrent.futures import ThreadPoolExecutor

try:
    from PIL import Image
except ImportError:                                                    # pragma: no cover
    sys.exit("Pillow is required for the thumbnails: python -m pip install pillow")

WIDGET_STATE = os.path.join(os.environ["LOCALAPPDATA"],
                            r"Packages\eworthing.SteamGridDBforXbox.Dev_y5pmx8xz2g8jm\LocalState")
XBOX_LIBRARIES = os.path.join(os.environ["LOCALAPPDATA"],
                              r"Packages\Microsoft.GamingApp_8wekyb3d8bbwe\LocalState\ThirdPartyLibraries")
DEFAULT_OUT = os.path.join(os.environ.get("TEMP", "."), "steamgriddb-artwork-compare")

# Longest edge of every embedded image. 320 keeps a 250-game report near 18 MB and still
# fills the click-to-zoom view; raise it with --thumb-size when a run needs a closer look.
TILE_PX = 320

# ---- upstream 1.4.0.0, transcribed from GamePlatform.cs and SteamGridDbClient.cs ----------------
UPSTREAM_PLATFORM = {"steam": "steam", "gog": "gog", "epic": "egs",
                     "ubi": "uplay", "bnet": "bnet", "ea": "origin"}
UPSTREAM_GRID_DIMS = "dimensions=512x512,1024x1024"
UPSTREAM_ICON_DIMS = "dimensions=128,256,512,1024"

# ---- this fork, from SteamGridDbClient.BuildUrl and PrimaryWidget.GetTitleBearingGridsAsync ------
FORK_FILTERS = "types=static&nsfw=false&humor=false&epilepsy=false"
FORK_GRID_DIMS = "dimensions=512x512,1024x1024"
FORK_PORTRAIT_DIMS = "dimensions=600x900,342x482,660x930"
TEXT_BEARING = ("alternate", "white_logo", "blurred")

# ---- ArtworkRanker.cs, verbatim -----------------------------------------------------------------
DEMOTED = re.compile(r"\b(case|box|jewel|spine|cartridge|mock-?ups?|physical|ps1|ps2|psp|retro|custom|wallpapers?|iisu|game icons|wallhaven|artstation|deviantart)\b", re.I)
CONSOLE = re.compile(r"\b(playstation hits|ps hits|playstation ?[1-5]|ps ?[45] ?(dashboard |store )?icon|ps ?[45] ?square|nintendo switch|switch ?2? ?icon|dashboard icon|xbox one|xbox series)\b", re.I)
SOUNDTRACK = re.compile(r"\b(vinyl|soundtrack|ost|album cover|album art)\b", re.I)
BOOSTED = re.compile(r"\b(official|offical)\b|xbox\.com|playstation\.com|nintendo\.com|microsoft\.com", re.I)
EDITION = re.compile(r"\b(deluxe|goty|game of the year|definitive|ultimate|premium|collector'?s?|complete|anniversary|remaster(ed)?|enhanced|legendary|gold)\b", re.I)
XREF = re.compile(r"\[>[^\]]*\]\s*\([^)]*\)")
MDLINK = re.compile(r"\[([^\]]*)\]\s*\([^)]*\)")
BAREURL = re.compile(r"https?://(?:www\.)?([^/\s)\]]+)\S*")


# =================================================================================================
# HTTP, cached so that re-running after a dev run only pays for what actually changed
# =================================================================================================

class Api:
    def __init__(self, cache_dir, refresh=False):
        self.cache = cache_dir
        self.refresh = refresh
        self.key = os.environ.get("STEAMGRIDDB_API_KEY")
        if not self.key:
            sys.exit("STEAMGRIDDB_API_KEY is not set - the widget reads the same variable.")
        os.makedirs(os.path.join(cache_dir, "api"), exist_ok=True)
        os.makedirs(os.path.join(cache_dir, "img"), exist_ok=True)

    def _path(self, kind, key):
        # Hashed, because a full query string plus a long temp path overruns MAX_PATH on Windows
        stem = re.sub(r"[^A-Za-z0-9._-]", "_", key)[:60]
        return os.path.join(self.cache, kind,
                            f"{stem}-{hashlib.sha1(key.encode()).hexdigest()[:12]}"
                            + (".json" if kind == "api" else ".bin"))

    def get(self, path, params=""):
        """Returns {"data": parsed json or None, "error": str or None}."""
        url = f"https://www.steamgriddb.com/api/v2/{path}" + (f"?{params}" if params else "")
        cp = self._path("api", url.split("/api/v2/", 1)[1])
        if os.path.exists(cp) and not self.refresh:
            return json.load(open(cp, encoding="utf-8"))
        out = {"data": None, "error": "request failed"}
        for attempt in range(4):
            req = urllib.request.Request(url, headers={
                "Authorization": "Bearer " + self.key,
                "User-Agent": "steamgriddb-xbox-compare/1.0",
                "Accept": "application/json"})
            try:
                with urllib.request.urlopen(req, timeout=45) as resp:
                    out = {"data": json.loads(resp.read().decode("utf-8")), "error": None}
                    break
            except urllib.error.HTTPError as ex:
                if ex.code == 429:                       # what upstream does not do: back off politely
                    time.sleep(float(ex.headers.get("Retry-After") or 5) + attempt)
                    out = {"data": None, "error": "rate limited"}
                    continue
                out = {"data": None, "error": f"http {ex.code}"}
                break
            except Exception as ex:
                out = {"data": None, "error": str(ex)}
                time.sleep(1 + attempt)
        json.dump(out, open(cp, "w", encoding="utf-8"))
        return out

    def rows(self, path, params=""):
        """The artwork list, [] when the game genuinely has none, None when the request failed.

        The same distinction the fork draws in ArtworkSource - conflating them is how a throttled
        run comes to look like a library with no artwork available."""
        r = self.get(path, params)
        d = r["data"]
        if d and d.get("success") and isinstance(d.get("data"), list):
            return d["data"]
        return None if (r["error"] or not d or not d.get("success")) else []

    def image(self, url):
        cp = self._path("img", url)
        if os.path.exists(cp) and not self.refresh:
            return open(cp, "rb").read()
        for attempt in range(3):
            try:
                req = urllib.request.Request(url, headers={"User-Agent": "steamgriddb-xbox-compare/1.0"})
                with urllib.request.urlopen(req, timeout=60) as resp:
                    data = resp.read()
                open(cp, "wb").write(data)
                return data
            except Exception:
                time.sleep(1 + attempt)
        return None


def thumbnail(raw, size=320, backdrop=(24, 24, 27)):
    """A small JPEG data URI. Transparency is flattened onto the tile backdrop rather than white,
    so an image with alpha corners looks the way it would on a tile rather than cut out."""
    if not raw:
        return None
    try:
        im = Image.open(io.BytesIO(raw) if isinstance(raw, bytes) else raw)
        im = im.convert("RGBA")
        flat = Image.new("RGB", im.size, backdrop)
        flat.paste(im, mask=im.split()[3])
        flat.thumbnail((size, size), Image.LANCZOS)
        buf = io.BytesIO()
        flat.save(buf, "JPEG", quality=80, optimize=True)
        return "data:image/jpeg;base64," + base64.b64encode(buf.getvalue()).decode()
    except Exception:
        return None


# =================================================================================================
# ArtworkRanker.RankGrids, re-implemented so check 4 can hold the real one to account
# =================================================================================================

def grid_metadata(candidate):
    text = (candidate.get("notes") or "") + " "        # v2 responses carry no tags field at all
    text = XREF.sub(" ", text)
    text = MDLINK.sub(r"\1", text)
    return BAREURL.sub(r" \1 ", text)


def is_demoted(metadata, game_name):
    if DEMOTED.search(metadata) or CONSOLE.search(metadata) or SOUNDTRACK.search(metadata):
        return True
    if not game_name:
        return False                                    # an unresolved name is not evidence
    return any(m.group(0).lower() not in game_name.lower() for m in EDITION.finditer(metadata))


def rank_grids(pool, game_name):
    def key(indexed):
        i, c = indexed
        meta = grid_metadata(c)
        return (1 if is_demoted(meta, game_name) else 0,
                1 if (c.get("language") and c["language"] != "en") else 0,
                0 if c.get("style") in TEXT_BEARING else 1,
                0 if BOOSTED.search(meta) else 1,
                -(c.get("width") or 0),
                i)                                      # index last: ties keep the API's order
    return [c for _, c in sorted(enumerate(pool or []), key=key)]


# =================================================================================================
# Reading what the widget and the Xbox app left on disk
# =================================================================================================

def read_json(path):
    return json.load(open(path, encoding="utf-8-sig"))


def parse_fix_log(path):
    """last-fix.log into one record per game. The shapes come from the FixLog.Write call sites."""
    lines = open(path, encoding="utf-8-sig").read().splitlines()
    if not lines:
        sys.exit(f"{path} is empty - run 'Re-fix all games' in the widget first.")
    games, cur = [], None
    for line in lines[1:]:
        if not line.startswith("  "):
            m = re.match(r"^(.*) capsule=(.*)$", line)
            if m:
                cur = {"name": m.group(1), "capsule": m.group(2), "notes": [], "ranked": [],
                       "applied": None, "kind": None, "poolCount": 0, "appliedNote": ""}
                games.append(cur)
            continue
        if cur is None:
            continue
        body = line.strip()
        m = re.match(r"^(\d+) (square|portrait) candidates, ranked: (.*)$", body)
        if m:
            cur["poolCount"], cur["kind"] = int(m.group(1)), m.group(2)
            cur["ranked"] = [int(x) for x in m.group(3).split(", ")]
            continue
        m = re.match(r"^applied (\d+)(.*)$", body)
        if m:
            cur["applied"], cur["appliedNote"] = int(m.group(1)), m.group(2).strip()
            continue
        cur["notes"].append(body)
    return lines[0], [g for g in games if g["applied"] is not None]


def read_manifests():
    """Every ThirdPartyLibraries entry, keyed the way game-matches.json keys it, carrying both ids
    upstream derives from the raw entry id."""
    out = {}
    for folder in sorted(os.listdir(XBOX_LIBRARIES)):
        manifest = os.path.join(XBOX_LIBRARIES, folder, folder + ".manifest")
        if not os.path.isfile(manifest):
            continue
        cache = read_json(manifest).get("gameCache") or {}
        for key, entry in cache.items():
            if not isinstance(entry, dict):
                continue                                # gameCache also holds a scalar "version"
            entry_id = entry.get("id") or key
            parts = entry_id.split(":")
            tail = entry_id[entry_id.index(":") + 1:] if ":" in entry_id else entry_id
            lower = folder.lower()
            out[f"{ {'ubi': 'ubisoft'}.get(lower, lower) }/{parts[-1].lower()}".replace(" ", "")] = {
                "platform": UPSTREAM_PLATFORM.get(lower),
                "xboxId": tail,
                # upstream's Epic branch takes parts[2], the catalog item id, as the external id
                "externalId": parts[2] if (lower == "epic" and len(parts) >= 3) else tail,
                "entryId": entry_id,
            }
    return out


def applied_by_key(state):
    """applied-artwork.json is keyed by image path; turn that into the platform/id keys everything
    else uses. First-party tiles live under the Xbox app's image cache and are matched through
    xbox-tiles.json instead of their filename."""
    applied = read_json(os.path.join(state, "applied-artwork.json"))
    tiles = read_json(os.path.join(state, "xbox-tiles.json"))
    tile_to_store = {t: store.lower() for store, ts in tiles.items() for t in ts}
    out, paths = {}, {}
    for path, artwork_id in applied.items():
        base = os.path.basename(path)
        m = re.match(r"^([a-z]+)_(.+)\.png$", base)
        if m:
            folder = {"ubi": "ubisoft"}.get(m.group(1), m.group(1))
            key = f"{folder}/{m.group(2).split('_')[-1].lower()}"
        else:
            store = tile_to_store.get(base)
            key = f"xbox/{store}" if store else None
        if key:
            out.setdefault(key, set()).add(artwork_id)
            paths.setdefault(key, []).append(path)
    return out, paths


# =================================================================================================
# The two replays
# =================================================================================================

def upstream_pick(api, key, manifests):
    """What 1.4.0.0 ends up with for this game."""
    entry = manifests.get(key)
    if entry is None:
        return {"outcome": "out-of-scope",
                "why": "first-party Xbox game - upstream only walks ThirdPartyLibraries"}
    platform = entry["platform"]
    base = {k: entry[k] for k in ("entryId", "platform", "xboxId", "externalId")}
    if platform is None:
        return dict(base, outcome="out-of-scope", why="platform has no SteamGridDB mapping upstream")

    lookup = api.get(f"games/{platform}/{urllib.parse.quote(entry['externalId'], safe='')}")["data"]
    if not (lookup and lookup.get("success") and lookup.get("data")):
        return dict(base, outcome="not-eligible",
                    why=f"games/{platform}/{entry['externalId']} matches no SteamGridDB game, so "
                        f"upstream never lists it as fixable")

    seg = f"{platform}/{urllib.parse.quote(entry['xboxId'], safe='')}"
    grids = api.rows(f"grids/{seg}", UPSTREAM_GRID_DIMS)
    if grids:
        return dict(base, outcome="grid", request=f"grids/{seg}?{UPSTREAM_GRID_DIMS}",
                    poolCount=len(grids), pick=grids[0], pool=grids)
    icons = api.rows(f"icons/{seg}", UPSTREAM_ICON_DIMS)
    if icons:
        return dict(base, outcome="icon", request=f"icons/{seg}?{UPSTREAM_ICON_DIMS}",
                    poolCount=len(icons), pick=icons[0], pool=icons)
    return dict(base, outcome="nothing", request=f"grids/{seg}?{UPSTREAM_GRID_DIMS}")


def fork_segment(game):
    """ArtworkSource: a SteamGridDB game id for anything matched by name, the store's own id
    otherwise."""
    gid = (game.get("match") or {}).get("id") or 0
    if gid:
        return f"game/{gid}"
    platform, pid = game["key"].split("/", 1)
    sgdb = {"epic": "egs", "ubisoft": "uplay", "ea": "origin"}.get(platform, platform)
    return f"{sgdb}/{urllib.parse.quote(pid, safe='')}"


def fork_pool(api, game):
    """The candidates the widget ranked, rescue call included: when page one carries no
    title-bearing style at all, GetTitleBearingGridsAsync re-asks with styles= and uses that
    instead."""
    seg = fork_segment(game)
    if game["kind"] == "portrait":
        return api.rows(f"grids/{seg}", f"{FORK_PORTRAIT_DIMS}&{FORK_FILTERS}")
    pool = api.rows(f"grids/{seg}", f"{FORK_GRID_DIMS}&{FORK_FILTERS}")
    if not pool or any(c.get("style") in TEXT_BEARING for c in pool):
        return pool
    rescue = api.rows(f"grids/{seg}",
                      f"{FORK_GRID_DIMS}&styles={','.join(TEXT_BEARING)}&{FORK_FILTERS}")
    return rescue if rescue else pool


def locate_applied(api, game, upstream):
    """Metadata for the artwork the widget wrote. Upstream's square pool is a superset of the
    fork's - same dimensions, fewer filters - so a square pick is normally already there."""
    want = game["applied"]
    for c in (upstream.get("pool") or []):
        if c["id"] == want:
            return c
    for c in (game.get("pool") or []):
        if c["id"] == want:
            return c
    seg = fork_segment(game)
    for path, params in ((f"grids/{seg}", f"{FORK_GRID_DIMS}&{FORK_FILTERS}"),
                         (f"grids/{seg}", f"{FORK_GRID_DIMS}&styles={','.join(TEXT_BEARING)}&{FORK_FILTERS}"),
                         (f"grids/{seg}", f"{FORK_PORTRAIT_DIMS}&{FORK_FILTERS}"),
                         (f"icons/{seg}", f"{UPSTREAM_ICON_DIMS}&{FORK_FILTERS}")):
        for c in (api.rows(path, params) or []):
            if c["id"] == want:
                return c
    return None


CAUSES = {
    "ranking": ("Ranking", "the ranking keys moved the pick off the order SteamGridDB returned"),
    "gate": ("Official-art gate", "the top-ranked art did not resemble Valve's capsule and something below it did"),
    "badge": ("Storefront badge", "a higher-ranked candidate had a store badge composited into its corner"),
    "portrait": ("Portrait crop", "no square art exists; upstream falls back to an icon or to nothing"),
    "unreachable": ("Never listed", "upstream filters the game out before any artwork request happens"),
    "filters": ("Request filters", "upstream's first result is animated or flagged, so the fork never sees it"),
    "download": ("Download loop", "the top-ranked candidate could not be downloaded or failed the tile-fill check"),
}


def attribute(game, upstream):
    """Which single mechanism accounts for the two versions disagreeing."""
    upstream_id = (upstream.get("pick") or {}).get("id")
    if upstream_id == game["applied"]:
        return None
    if upstream["outcome"] in ("not-eligible", "out-of-scope"):
        return "unreachable"
    if upstream["outcome"] in ("nothing", "icon"):
        return "portrait"

    pool = game.get("pool") or []
    ranked = [c["id"] for c in rank_grids(pool, game["name"])]
    notes = " | ".join(game["notes"])
    if upstream_id not in {c["id"] for c in pool}:
        return "filters"
    if "storefront badge in the corner - skipped" in notes and game["applied"] != (ranked[0] if ranked else None):
        return "badge"
    if re.search(r"REPLACED \d+", notes) and game["applied"] != (ranked[0] if ranked else None):
        return "gate"
    if ranked and game["applied"] == ranked[0]:
        return "ranking"
    return "download"


# =================================================================================================
# Checks
# =================================================================================================

def run_checks(games, manifests, state, fix_log_header):
    results = []

    def check(title, ok, detail, meaning):
        results.append({"title": title, "ok": bool(ok), "detail": detail, "meaning": meaning})

    scores, votes, n = set(), set(), 0
    for g in games:
        for c in (g["upstream"].get("pool") or []):
            scores.add(c.get("score"))
            votes.add((c.get("upvotes"), c.get("downvotes")))
            n += 1
    check("Upstream's ranking really is a no-op",
          scores <= {0} and votes <= {(0, 0)},
          f"{n} candidates across every pool upstream would see; distinct scores {sorted(scores)}, "
          f"distinct vote pairs {sorted(votes)}",
          "If a score were ever non-zero, upstream's OrderByDescending would reorder and its pick "
          "would not be data[0] - every right-hand column would be wrong.")

    drift = [g["name"] for g in games
             if g["kind"] == "square" and g["upstream"]["outcome"] == "grid"
             and (set(g["ranked"]) | {int(x) for x in re.findall(r"\b(\d{4,7})\b", " ".join(g["notes"]))})
             - {c["id"] for c in g["upstream"]["pool"]}]
    check("The candidate pools have not moved since the run",
          not drift,
          f"every artwork id the widget logged is still in upstream's pool for all "
          f"{sum(1 for g in games if g['kind'] == 'square' and g['upstream']['outcome'] == 'grid')} "
          f"square games" if not drift else f"{len(drift)} games drifted: {drift[:6]}",
          "The dev run and this replay happen at different times. If SteamGridDB reordered or "
          "removed uploads in between, the two columns would not be comparable.")

    sizes = [(g["name"], len(g["pool"] or []), g["poolCount"]) for g in games
             if g["pool"] is not None and len(g["pool"]) != g["poolCount"]]
    check("The pool is still the size the widget logged",
          not sizes, f"{len(games)} games checked" if not sizes else f"{len(sizes)} differ: {sizes[:6]}",
          "A second, independent test of the same thing: the log records how many candidates the "
          "widget ranked, so a changed count means the pool moved under us.")

    key_applied, _ = applied_by_key(state)
    disagree = [(g["name"], g["applied"], sorted(key_applied[g["key"]]))
                for g in games if g["key"] in key_applied and g["applied"] not in key_applied[g["key"]]]
    confirmed = sum(1 for g in games if g["applied"] in key_applied.get(g["key"], set()))
    check("Every logged pick matches the artwork recorded on disk",
          not disagree, f"{confirmed} of {len(games)} confirmed against applied-artwork.json"
          if not disagree else f"{len(disagree)} contradict it: {disagree[:4]}",
          "The log says what the run intended; applied-artwork.json is what the picker reads back "
          "as 'in use'. Agreement means the left-hand column is the tile actually on the machine.")

    mismatch = [(g["name"], [c["id"] for c in rank_grids(g["pool"], g["name"])][:5], g["ranked"])
                for g in games if g["pool"] and
                [c["id"] for c in rank_grids(g["pool"], g["name"])][:5] != g["ranked"]]
    check("A from-scratch ranker reproduces the widget's ranking",
          not mismatch, f"{len(games)} games, top 5 identical every time"
          if not mismatch else f"{len(mismatch)} differ: {mismatch[:3]}",
          "ArtworkRanker.cs is re-implemented here from its source and required to reproduce what "
          "the widget logged. It fails whenever the two have drifted apart - including, expectedly, "
          "in the window between changing the ranker and re-running the widget, when the log on disk "
          "still records the old behaviour. Outside that window a failure means the copy above is "
          "stale, and the 'why it moved' attribution cannot be trusted.")

    installed = set(manifests)
    covered = {g["key"] for g in games}
    load_log = os.path.join(state, "last-load.log")
    excused = 0
    if os.path.exists(load_log):
        text = open(load_log, encoding="utf-8-sig").read()
        excused = len(re.findall(r"^not shown ", text, re.M)) + len(re.findall(r"matched=False", text))
    gap = sorted(installed - covered)
    bnet = [k for k in gap if k.startswith("bnet/")]
    check("Every installed game the run skipped has a recorded reason",
          len(gap) <= excused + len(bnet),
          f"{len(installed)} third-party entries installed, {len(covered & installed)} in the run, "
          f"{len(gap)} not - against {excused} recorded in last-load.log as having no tile on disk "
          f"or no SteamGridDB match, plus {len(bnet)} Battle.net",
          "Games with no tile file, no SteamGridDB match, or a Battle.net entry are out of both "
          "versions' reach. First-party Xbox games are excluded from bulk fixes by FixEligibility, "
          "by design, so they are absent from both columns.")

    return results


# =================================================================================================
# Report
# =================================================================================================

def tidy_notes(notes):
    if not notes:
        return None
    s = MDLINK.sub(r"\1", notes)
    s = re.sub(r"https?://\S+", "", s)
    s = re.sub(r"[*_`>#|\\]+", " ", s)
    s = re.sub(r"\s+", " ", s).strip(" -:")
    return (s[:110].rstrip() + "…") if len(s) > 110 else (s or None)


CSS = """
:root{--bg:#f6f7f9;--panel:#fff;--ink:#16181d;--muted:#666e7a;--line:#e2e5ea;--dev:#107c10;
 --prod:#8a6a00;--chip:#eef0f4;--ok:#107c10;--bad:#c42b1c;
 --shadow:0 1px 2px rgba(0,0,0,.06),0 8px 24px rgba(0,0,0,.04)}
@media (prefers-color-scheme:dark){:root{--bg:#111316;--panel:#191c21;--ink:#e8eaee;--muted:#98a1ad;
 --line:#282d35;--dev:#5ec75e;--prod:#e0b53a;--chip:#232830;--ok:#5ec75e;--bad:#ff6b5e;
 --shadow:0 1px 2px rgba(0,0,0,.4)}}
*{box-sizing:border-box}
body{margin:0;background:var(--bg);color:var(--ink);font:15px/1.5 "Segoe UI Variable Text","Segoe UI",system-ui,sans-serif}
header{padding:28px 28px 0;max-width:1560px;margin:0 auto}
h1{font-size:26px;margin:0 0 6px;letter-spacing:-.01em}
.sub{color:var(--muted);max-width:80ch;margin:0 0 16px}
.sub code{background:var(--chip);padding:1px 5px;border-radius:4px;font-size:12.5px}
.stats{display:flex;flex-wrap:wrap;gap:10px;margin-bottom:14px}
.stat{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:10px 14px;box-shadow:var(--shadow)}
.stat b{display:block;font-size:22px;line-height:1.2}
.stat span{color:var(--muted);font-size:12.5px}
.audit{background:var(--panel);border:1px solid var(--line);border-radius:10px;padding:12px 16px;
 margin-bottom:18px;box-shadow:var(--shadow)}
.audit>summary{cursor:pointer;font-size:13.5px}
.audit ul{list-style:none;margin:12px 0 0;padding:0}
.audit li{padding:9px 0;border-top:1px solid var(--line)}
.audit li:first-child{border-top:0}
.audit .v{font-weight:600;font-family:ui-monospace,Consolas,monospace;font-size:11.5px;
 padding:1px 7px;border-radius:999px;margin-right:8px}
.audit .pass{background:var(--ok);color:#fff}.audit .fail{background:var(--bad);color:#fff}
.audit .d{color:var(--muted);font-size:12.5px;margin:4px 0 0 0}
.audit .m{color:var(--muted);font-size:12.5px;margin:3px 0 0 0;font-style:italic;opacity:.85}
.filters{position:sticky;top:0;z-index:5;background:var(--bg);padding:10px 0 14px;
 border-bottom:1px solid var(--line);display:flex;flex-wrap:wrap;gap:8px}
button.f{background:var(--chip);border:1px solid transparent;color:var(--ink);border-radius:999px;
 padding:7px 14px;font:inherit;font-size:13.5px;cursor:pointer}
button.f:hover{border-color:var(--line)}
button.f[aria-pressed=true]{background:var(--ink);color:var(--bg)}
button.f i{font-style:normal;opacity:.6;margin-left:6px}
main{max-width:1560px;margin:0 auto;padding:16px 28px 60px}
.blurb{color:var(--muted);max-width:90ch;margin:4px 0 20px;font-size:13.5px}
.grid{display:grid;gap:18px;grid-template-columns:repeat(auto-fill,minmax(min(620px,100%),1fr))}
.card{background:var(--panel);border:1px solid var(--line);border-radius:12px;overflow:hidden;box-shadow:var(--shadow)}
.card h2{font-size:15.5px;margin:0;padding:12px 14px 10px;display:flex;gap:8px;align-items:center}
.card h2 .txt{flex:1;min-width:0}
.card h2 em{display:block;font-style:normal;color:var(--muted);font-weight:400;font-size:11.5px;
 font-family:ui-monospace,Consolas,monospace;margin-top:2px}
.side.ref>h3{color:var(--muted)}
.pair{display:grid;grid-template-columns:1fr 1fr 1fr;gap:1px;background:var(--line)}
@media (max-width:560px){.pair{grid-template-columns:1fr}}
.side{background:var(--panel);padding:10px 12px 12px}
.side>h3{margin:0 0 8px;font-size:11px;letter-spacing:.08em;text-transform:uppercase;color:var(--muted)}
.side.dev>h3{color:var(--dev)}.side.prod>h3{color:var(--prod)}
.shot{aspect-ratio:1;background:#18181b;border-radius:8px;overflow:hidden;display:grid;place-items:center}
.shot img{width:100%;height:100%;object-fit:contain;display:block;cursor:zoom-in}
#lb{position:fixed;inset:0;background:rgba(0,0,0,.9);display:none;place-items:center;z-index:50;
 padding:24px;cursor:zoom-out}
#lb.on{display:grid}
#lb img{max-width:min(96vw,900px);max-height:92vh;object-fit:contain;border-radius:8px}
.shot.none{background:var(--chip);color:var(--muted);font-size:12.5px;text-align:center;padding:14px;line-height:1.45}
.meta{margin:8px 0 0;font-size:12px;color:var(--muted);word-break:break-word}
.meta b{color:var(--ink);font-weight:600}
.notes{margin-top:4px;font-style:italic;opacity:.85}
.tags{display:flex;flex-wrap:wrap;gap:6px;padding:10px 14px 0}
.tag{font-size:11.5px;background:var(--chip);border-radius:999px;padding:3px 9px;color:var(--muted)}
.tag.hi{background:var(--dev);color:#fff}
details.log{padding:10px 14px 14px}
details.log>summary{cursor:pointer;font-size:12.5px;color:var(--muted)}
pre{margin:8px 0 0;font:12px/1.55 ui-monospace,Consolas,monospace;white-space:pre-wrap;
 background:var(--chip);padding:10px;border-radius:8px;color:var(--ink)}
.empty{color:var(--muted);padding:40px 0}
"""

JS = r"""
const DATA = window.__DATA__, IMG = window.__IMG__, CAUSES = window.__CAUSES__;
const grid = document.getElementById('grid'), count = document.getElementById('count');
const GROUPS = {
  changed: g => g.cause !== null,
  ranking: g => g.cause === 'ranking',
  gate: g => g.cause === 'gate',
  badge: g => g.cause === 'badge',
  portrait: g => g.cause === 'portrait',
  unreachable: g => g.cause === 'unreachable',
  identical: g => g.cause === null,
  all: () => true,
};
const BLURB = {
  changed: 'Every game where the two versions land on different artwork, one card per game, labelled with the single mechanism that accounts for the difference.',
  ranking: 'The ranking keys — mockup and edition vocabulary, console-store badges, language, style tier, official-artwork wording, then resolution — moved rank 1 off the order SteamGridDB returned, and that is the whole difference. Upstream takes the API order as-is.',
  gate: 'The top-ranked artwork did not resemble Valve’s own store capsule and a candidate below it clearly did. The replacement has to win on colour and on layout, and must not itself be demoted.',
  badge: 'A higher-ranked candidate had a storefront badge composited into its corner. The notes fields on these are empty, so only the pixel check finds them.',
  portrait: 'No square artwork exists at all. Upstream falls back to an icon — sometimes a .ico written into a .png — or to nothing. This fork crops portrait box art instead.',
  unreachable: 'Upstream never applies anything here: its Epic id handling, its uplay and origin platform strings, or an empty response filter the game out before any artwork request happens. The right-hand tile is what the Xbox app already had.',
  identical: 'Both versions choose the same artwork. The ranking layer only earns its keep on the rest.',
  all: 'The whole run, most-interesting mechanisms first.',
};
let active = 'changed';
const esc = s => String(s).replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const src = k => (k && IMG[k]) || null;

function shot(uri, fallback){
  return uri ? `<div class="shot"><img loading="lazy" src="${uri}" alt=""></div>`
             : `<div class="shot none">${fallback}</div>`;
}
function meta(a){
  if(!a) return '';
  const bits = [`<b>#${a.id}</b>`, a.style, a.w ? `${a.w}×${a.h}` : null, a.mime,
                a.lang && a.lang !== 'en' ? `lang ${a.lang}` : null].filter(Boolean);
  return `<p class="meta">${bits.join(' · ')}${a.author ? `<br>by ${esc(a.author)}` : ''}` +
         `${a.notes ? `<span class="notes"><br>“${esc(a.notes)}”</span>` : ''}</p>`;
}
function prodSide(g){
  if(g.prod) return shot(src(g.prod.img), 'thumbnail unavailable') + meta(g.prod);
  return shot(src(g.original), 'no artwork, and no backup tile to show') +
    `<p class="meta">${g.original ? '<b>The Xbox app’s own tile, left untouched.</b><br>' : ''}${esc(g.prodWhy || '')}</p>`;
}
function card(g){
  const c = g.cause ? CAUSES[g.cause] : null;
  const tags = (c ? `<span class="tag hi" title="${esc(c[1])}">${esc(c[0])}</span>` : '<span class="tag">Same pick as upstream</span>')
    + (g.rankAlsoMoved && g.cause !== 'ranking' ? '<span class="tag">ranking had already moved rank 1</span>' : '');
  const log = [
    g.devPool ? `this fork: ${g.devPool} ${g.kind} candidates, ranked ${g.devRanked.join(', ')}` : null,
    ...g.log.map(l => '  ' + l),
    `  applied ${g.applied}${g.appliedNote ? ' ' + g.appliedNote : ''}`,
    '',
    g.prodRequest ? `upstream: GET ${g.prodRequest}` : `upstream: no request — ${g.prodWhy || ''}`,
    g.prodTop.length ? `  ${g.prodPool} in the response, order ${g.prodTop.join(', ')}` : null,
    g.prod ? `  takes data[0] → ${g.prod.id}` : null,
  ].filter(x => x !== null).join('\n');
  const reference = g.capsule
    ? shot(src(g.capsule), '') + '<p class="meta">Valve’s own store cover — the yardstick the gate measures against.</p>'
    : '<div class="shot none">No official Steam capsule for this game, so the gate could not run.</div>';
  return `<article class="card">
    <h2><span class="txt">${esc(g.name)}<em>${esc(g.key)}</em></span></h2>
    <div class="tags">${tags}</div>
    <div class="pair">
      <div class="side ref"><h3>Valve’s capsule</h3>${reference}</div>
      <div class="side dev"><h3>This fork</h3>${shot(g.dev && src(g.dev.img), 'thumbnail unavailable')}${meta(g.dev)}</div>
      <div class="side prod"><h3>Upstream 1.4.0.0</h3>${prodSide(g)}</div>
    </div>
    <details class="log"><summary>Decision log</summary><pre>${esc(log)}</pre></details>
  </article>`;
}
function render(){
  const list = DATA.games.filter(GROUPS[active]);
  count.textContent = `${list.length} game${list.length === 1 ? '' : 's'}`;
  document.getElementById('blurb').textContent = BLURB[active] || '';
  grid.innerHTML = list.length ? list.map(card).join('') : '<p class="empty">Nothing in this group.</p>';
  window.scrollTo({top: 0});
}
const lb = document.getElementById('lb');
grid.addEventListener('click', e => {
  if (e.target.tagName === 'IMG' && e.target.closest('.shot')) {
    lb.firstElementChild.src = e.target.src;
    lb.classList.add('on');
  }
});
lb.addEventListener('click', () => lb.classList.remove('on'));
addEventListener('keydown', e => { if (e.key === 'Escape') lb.classList.remove('on'); });
document.querySelectorAll('button.f').forEach(b => b.addEventListener('click', () => {
  active = b.dataset.g;
  document.querySelectorAll('button.f').forEach(x => x.setAttribute('aria-pressed', x === b));
  render();
}));
render();
"""


def build_report(games, checks, header, out_path, api):
    images = {}

    def keep(key, uri):
        if uri and key not in images:
            images[key] = uri
        return key if uri else None

    def art(a):
        if not a:
            return None
        return {"id": a["id"], "style": a.get("style"), "w": a.get("width"), "h": a.get("height"),
                "mime": (a.get("mime") or "").replace("image/", ""),
                "author": (a.get("author") or {}).get("name"),
                "notes": tidy_notes(a.get("notes")), "lang": a.get("language"),
                "img": keep(f"a{a['id']}", thumbnail(api.image(a["thumb"]), TILE_PX) if a.get("thumb") else None)}

    order = ["gate", "badge", "portrait", "unreachable", "filters", "download", "ranking", None]
    records = []
    for g in games:
        up = g["upstream"]
        records.append({
            "name": g["name"], "key": g["key"], "kind": g["kind"], "cause": g["cause"],
            "rankAlsoMoved": g["rankAlsoMoved"],
            "dev": art(g["devArt"]), "prod": art(up.get("pick")),
            "prodWhy": up.get("why") or ("The request comes back with no square art and no icons."
                                         if up["outcome"] == "nothing" else None),
            "prodRequest": up.get("request"), "prodPool": up.get("poolCount"),
            "prodTop": [c["id"] for c in (up.get("pool") or [])][:6],
            "devPool": g["poolCount"], "devRanked": g["ranked"],
            "applied": g["applied"], "appliedNote": g["appliedNote"], "log": g["notes"],
            "original": keep(f"o{g['key']}", g.get("originalTile")),
            "capsule": keep(f"c{g['key']}",
                            thumbnail(api.image(g["capsule"]), int(TILE_PX * 1.4)) if g["capsule"] != "none" else None),
        })
    records.sort(key=lambda r: (order.index(r["cause"]), r["name"].lower()))

    counts = {
        "changed": sum(1 for r in records if r["cause"]),
        "ranking": sum(1 for r in records if r["cause"] == "ranking"),
        "gate": sum(1 for r in records if r["cause"] == "gate"),
        "badge": sum(1 for r in records if r["cause"] == "badge"),
        "portrait": sum(1 for r in records if r["cause"] == "portrait"),
        "unreachable": sum(1 for r in records if r["cause"] == "unreachable"),
        "identical": sum(1 for r in records if not r["cause"]),
        "all": len(records),
    }
    labels = [("changed", "Picks that moved"), ("ranking", "Ranking"), ("gate", "Official-art gate"),
              ("badge", "Storefront badge"), ("portrait", "Portrait crop"),
              ("unreachable", "Never listed"), ("identical", "Identical"), ("all", "Everything")]
    buttons = "".join(
        f'<button class="f" data-g="{gid}" aria-pressed="{"true" if gid == "changed" else "false"}">'
        f'{label}<i>{counts[gid]}</i></button>' for gid, label in labels if gid in counts)

    audit = "".join(
        f'<li><span class="v {"pass" if c["ok"] else "fail"}">{"PASS" if c["ok"] else "FAIL"}</span>'
        f'<b>{c["title"]}</b><p class="d">{c["detail"]}</p><p class="m">{c["meaning"]}</p></li>'
        for c in checks)
    failed = sum(1 for c in checks if not c["ok"])
    audit_head = ("All 6 accuracy checks pass" if not failed
                  else f"{failed} of {len(checks)} accuracy checks FAILED - read before trusting this")

    also = sum(1 for r in records if r["rankAlsoMoved"])
    page = f"""<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Artwork selection - this fork vs upstream 1.4.0.0</title>
<style>{CSS}</style></head><body>
<header>
  <h1>Artwork selection &mdash; this fork vs upstream&nbsp;1.4.0.0</h1>
  <p class="sub"><b>Left</b> is what the widget actually applied, read back from
  <code>{header}</code> and cross-checked against <code>applied-artwork.json</code>. Click any image for a full-size look.
  <b>Right</b> is upstream <code>fdd334e</code> replayed: its own eligibility rule, its own
  <code>xboxPlatformId</code> handling, its exact requests, and <code>data[0]</code> &mdash; because
  <code>OrderByDescending(g =&gt; g.Score)</code> sorts on a key the API retired and never returns
  anything but&nbsp;0.</p>
  <div class="stats">
    <div class="stat"><b>{counts['all']}</b><span>games in the run</span></div>
    <div class="stat"><b>{counts['changed']}</b><span>picks that moved</span></div>
    <div class="stat"><b>{counts['identical']}</b><span>identical either way</span></div>
    <div class="stat"><b>{counts['ranking']}</b><span>ranking alone</span></div>
    <div class="stat"><b>{counts['gate']}</b><span>official-art gate</span></div>
    <div class="stat"><b>{counts['badge']}</b><span>storefront badge</span></div>
    <div class="stat"><b>{counts['portrait'] + counts['unreachable']}</b><span>upstream leaves an icon or the original</span></div>
    <div class="stat"><b>{also}</b><span>where ranking moved rank&nbsp;1 too</span></div>
  </div>
  <details class="audit"><summary>{audit_head} &mdash; what each one rules out</summary>
    <ul>{audit}</ul></details>
  <div class="filters">{buttons}<span class="tag" id="count"></span></div>
</header>
<main><p class="blurb" id="blurb"></p><div class="grid" id="grid"></div></main>
<div id="lb"><img alt="full size"></div>
<script>
window.__DATA__ = {json.dumps({"games": records}, ensure_ascii=False, separators=(",", ":"))};
window.__IMG__ = {json.dumps(images, separators=(",", ":"))};
window.__CAUSES__ = {json.dumps(CAUSES, ensure_ascii=False)};
</script>
<script>{JS}</script>
</body></html>
"""
    open(out_path, "w", encoding="utf-8").write(page)
    return counts, images


# =================================================================================================

def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", default=DEFAULT_OUT, help="folder for the report and the API cache")
    ap.add_argument("--refresh", action="store_true", help="re-query instead of using the cache")
    ap.add_argument("--open", action="store_true", help="open the report when it is written")
    ap.add_argument("--thumb-size", type=int, default=TILE_PX,
                    help=f"longest edge of the embedded images (default {TILE_PX}); the "
                         f"report grows roughly with its square")
    args = ap.parse_args()
    globals()["TILE_PX"] = args.thumb_size

    os.makedirs(args.out, exist_ok=True)
    api = Api(os.path.join(args.out, "cache"), refresh=args.refresh)

    header, games = parse_fix_log(os.path.join(WIDGET_STATE, "last-fix.log"))
    matches = read_json(os.path.join(WIDGET_STATE, "game-matches.json"))
    manifests = read_manifests()
    key_applied, key_paths = applied_by_key(WIDGET_STATE)
    print(f"{len(games)} games from {header}")

    # Join each logged game to its store id. Names are unique in practice; where one is not (an
    # Xbox and a Steam copy of the same game), the artwork id on disk settles it.
    by_name = {}
    for k, v in matches.items():
        by_name.setdefault(v.get("name"), []).append(k)
    for g in games:
        keys = by_name.get(g["name"], [])
        g["key"] = (keys[0] if len(keys) == 1 else
                    next((k for k in keys if g["applied"] in key_applied.get(k, set())),
                         keys[0] if keys else None))
        g["match"] = matches.get(g["key"], {})
    unresolved = [g["name"] for g in games if not g["key"]]
    if unresolved:
        print(f"  warning: no store id for {len(unresolved)} game(s): {unresolved[:5]}")
        games = [g for g in games if g["key"]]

    print("replaying upstream and re-ranking this fork's candidates...")
    done = [0]

    def work(g):
        up = upstream_pick(api, g["key"], manifests)
        g["upstream"] = up
        g["pool"] = fork_pool(api, g)
        g["devArt"] = locate_applied(api, g, up)
        done[0] += 1
        if done[0] % 25 == 0:
            print(f"  {done[0]}/{len(games)}", flush=True)

    with ThreadPoolExecutor(max_workers=4) as pool:
        list(pool.map(work, games))

    for g in games:
        g["cause"] = attribute(g, g["upstream"])
        ranked = rank_grids(g["pool"], g["name"])
        g["rankAlsoMoved"] = bool(
            g["upstream"]["outcome"] == "grid" and ranked
            and ranked[0]["id"] != (g["upstream"].get("pick") or {}).get("id"))
        # For a game upstream never touches, show what it leaves behind: the Xbox app's own tile.
        g["originalTile"] = None
        if not g["upstream"].get("pick"):
            for p in key_paths.get(g["key"], []):
                backup = p[:-4] + ".bak"
                if os.path.exists(backup):
                    g["originalTile"] = thumbnail(open(backup, "rb").read(), TILE_PX)
                    break

    print("\nchecks")
    checks = run_checks(games, manifests, WIDGET_STATE, header)
    for c in checks:
        print(f"  {'PASS' if c['ok'] else 'FAIL'}  {c['title']} - {c['detail']}")

    report = os.path.join(args.out, "artwork-dev-vs-prod.html")
    counts, images = build_report(games, checks, header, report, api)
    print(f"\n{counts['changed']} of {counts['all']} picks moved: "
          f"{counts['ranking']} ranking, {counts['gate']} gate, {counts['badge']} badge, "
          f"{counts['portrait']} portrait-only, {counts['unreachable']} never listed")
    print(f"wrote {report} ({os.path.getsize(report) / 1e6:.1f} MB, {len(images)} images)")

    if args.open:
        webbrowser.open("file:///" + report.replace("\\", "/"))
    return 1 if any(not c["ok"] for c in checks) else 0


if __name__ == "__main__":
    sys.exit(main())
