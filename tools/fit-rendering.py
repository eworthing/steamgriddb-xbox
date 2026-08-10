"""Grow a group around a reported upload, and fit a BadgeOverlay rendering from it.

    python tools/fit-rendering.py seeds.json                 # find the group around each seed
    python tools/fit-rendering.py seeds.json --fit           # and emit the C# for groups that hold
    python tools/fit-rendering.py seeds.json --games 1200    # sweep harder

`seeds.json` is what the badge-survey sheet's "Copy flagged" button produces, unedited.

THE METHOD, WHICH IS NOT NEGOTIABLE
-----------------------------------
BadgeOverlay.cs states it plainly: adding a rendering is a measurement, not a tuning exercise.
Grow the group from a reported upload, confirm the members by eye, average them, keep the pixels
whose spread across *different games* is lowest. A group whose members do not then sit far below
the limit is not one rendering and must be split.

Two failures are on record from skipping the "different games" part, and both looked fine in every
statistic: a LEGO 2K Drive reference whose "overlay" was the LEGO brick logo - the game's own title
art - and a Bloons TD 6 one fitted across two uploads of the same cover, one badged and one not, so
its mask locked onto the artwork they shared and it flagged the clean upload. A storefront badge is
by definition something that appears on unrelated games. This tool therefore refuses to fit a group
that does not span enough distinct games, and prints the member names so the franchise check stays
a human one.

WHAT IT DOES
------------
1. Sweeps games and downloads EVERY candidate in each ranked pool, not just the pick. A badged
   upload usually loses on rank; the survey sheet only ever shows the winner.
2. Reduces each to the same 16x16 corner of a 64-square that BadgeOverlay measures, and reports
   each seed's nearest neighbours with their distances. The group boundary should show up as a gap
   in that list. If it does not, there is no group.
3. With --fit: averages the group, measures per-pixel spread across distinct games, keeps the
   low-spread pixels, and re-measures the result against the whole corpus - flags, worst flagged,
   nearest unflagged. Emits a paste-ready C# block only when the margin clears the limit.

The corner cache is keyed by artwork URL and kept on disk, so a second run costs nothing.
"""
import argparse
import importlib.util
import json
import io
import os
import random
import re
import statistics
import sys
from concurrent.futures import ThreadPoolExecutor

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

# Per-channel tolerance for calling a pixel "the same in both". Wide enough to survive JPEG
# recompression and a resampler difference, far tighter than any two pieces of artwork agree by luck.
OVERLAP_TOLERANCE = 12

# Pixels of the 16x16 corner that must be near-identical before two covers are worth reporting as
# possible group members. Measured, not guessed: at 24 the report filled with hundreds of unrelated
# covers that merely share a dark corner, and Red Dead Redemption's list admitted three uploads of
# "The friends of Ringo Ishikawa" at 46-59. Real groups sit far above that - the Nintendo Switch
# spine links No More Heroes, Cuphead and Street Fighter 6 at 96, 83 and 115, then falls to 27.
NEIGHBOUR_REPORT = 40

# A rendering must flag more than one game, and those games must not be one franchise. Two is the
# floor BadgeOverlay.cs states; four is used here because a fit from two members has no spread to
# measure - every pixel looks constant when there are only two samples.
MIN_MEMBERS = 4

# Pixels kept from the fit: those whose across-game spread is lowest. The count is capped so a
# rendering stays a badge rather than a whole corner, matching the 90-160 px the shipped ones use.
MAX_RENDERING_PIXELS = 150
MAX_CHANNEL_SPREAD = 12.0


def load_survey():
    spec = importlib.util.spec_from_file_location(
        "badge_survey", os.path.join(HERE, "badge-survey.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def corner_of(raw, scaled, corner):
    """The BadgeOverlay corner as a flat [r,g,b,...] list, or None when undecodable."""
    try:
        im = Image.open(io.BytesIO(raw))
        im.load()
    except Exception:
        return None
    px = im.convert("RGB").resize((scaled, scaled), Image.LANCZOS).load()
    return [c for y in range(corner) for x in range(corner) for c in px[x, y]]


def distance(a, b, mask=None):
    """Mean per-channel difference over the whole corner, or over mask's pixel indices."""
    if mask is None:
        return sum(abs(x - y) for x, y in zip(a, b)) / len(a)
    total = sum(abs(a[i * 3 + c] - b[i * 3 + c]) for i in mask for c in range(3))
    return total / (len(mask) * 3)


def overlap(a, b, tolerance=OVERLAP_TOLERANCE):
    """How many of the corner's pixels are near-identical in both - the group-finding measure.

    Mean distance is the WRONG measure for finding a group and this tool used it at first, which
    made every sweep report "no group" for covers that plainly shared an overlay. A spine occupies
    maybe a quarter of the corner; the other three quarters are different artwork, and averaging
    over all of it buries the signal. Cuphead and No More Heroes carry the identical Nintendo Switch
    spine and measured 70.1 apart - past every threshold - while sharing 96 of 256 pixels exactly.

    Counting near-exact pixels instead is the measure that matches what a composited overlay
    actually is: bit-for-bit identical wherever it appears, whatever is underneath. Two unrelated
    covers score near zero on it - the same pair's control against FINAL FANTASY VII scores 5."""
    return sum(1 for i in range(len(a) // 3)
               if abs(a[i * 3] - b[i * 3]) < tolerance
               and abs(a[i * 3 + 1] - b[i * 3 + 1]) < tolerance
               and abs(a[i * 3 + 2] - b[i * 3 + 2]) < tolerance)


# =================================================================================================
# The corpus - every candidate, not every pick
# =================================================================================================

def build_corpus(api, survey, module, games, scaled, corner, workers, cache_path):
    """{url: {"corner": [...], "game": name, "id": artwork id}} over every candidate in every pool."""
    cache = {}
    if os.path.exists(cache_path):
        cache = json.load(open(cache_path, encoding="utf-8"))
        print(f"  {len(cache)} corners already cached")

    def one(game):
        # The WHOLE pool, not the top 8 the downloader would reach. A badged upload usually loses
        # on rank - that is the point of the ranking - so a corpus cut at the download's horizon is
        # a corpus with the badges filtered out of it.
        pool = survey.candidate_pool(api, module, game) or []
        out = []
        for candidate in pool:
            url = candidate.get("url")
            if not url or url in cache:
                continue
            c = corner_of(api.image(url), scaled, corner)
            if c:
                out.append((url, {"corner": c, "game": game["name"], "id": candidate["id"]}))
        return out

    done = 0
    with ThreadPoolExecutor(max_workers=workers) as executor:
        for results in executor.map(one, games):
            cache.update(results)
            done += 1
            if done % 50 == 0:
                print(f"  {done}/{len(games)} games, {len(cache)} corners")

    json.dump(cache, open(cache_path, "w", encoding="utf-8"))
    return cache


# =================================================================================================
# Reporting the neighbourhood
# =================================================================================================

def neighbours(seed_corner, corpus, floor=NEIGHBOUR_REPORT):
    """Corpus entries sharing at least `floor` near-identical pixels with the seed, best first."""
    found = []
    for url, entry in corpus.items():
        shared = overlap(seed_corner, entry["corner"])
        if shared >= floor:
            found.append((shared, entry["game"], entry["id"], url))
    return sorted(found, reverse=True)


def report(seed, found):
    print(f"\n=== {seed['game']} (artwork {seed['id']}) " + "=" * 30)
    if not found:
        print(f"  nothing in the corpus shares {NEIGHBOUR_REPORT} pixels with this corner "
              "- no group, nothing to fit")
        return
    games = {g for _, g, _, _ in found}
    print(f"  {len(found)} candidates sharing >= {NEIGHBOUR_REPORT} px, across {len(games)} distinct games")
    previous = None
    for shared, game, artwork_id, _ in found[:40]:
        gap = "   <-- gap" if previous is not None and previous - shared > 12 else ""
        print(f"    {shared:>4} px  {artwork_id:>7}  {game[:48]}{gap}")
        previous = shared


# =================================================================================================
# Fitting
# =================================================================================================

def fit(members, corpus, limit):
    """Average the members, keep the pixels whose spread across distinct games is lowest.

    Returns (packed, stats) or (None, reason). The spread is measured per game rather than per
    upload: two uploads of the same cover agree on the artwork as well as the badge, which is
    exactly how the Bloons TD 6 reference came to mask the wrong pixels."""
    by_game = {}
    for entry in members:
        by_game.setdefault(entry["game"], entry["corner"])
    corners = list(by_game.values())

    if len(corners) < MIN_MEMBERS:
        return None, f"only {len(corners)} distinct games - needs {MIN_MEMBERS}"

    pixels = len(corners[0]) // 3
    mean, keep = [], []
    for i in range(pixels):
        channels = [[c[i * 3 + ch] for c in corners] for ch in range(3)]
        spread = max(statistics.pstdev(ch) for ch in channels)
        mean.append([round(statistics.fmean(ch)) for ch in channels])
        keep.append((spread, i))

    keep.sort()
    chosen = [i for spread, i in keep
              if spread <= MAX_CHANNEL_SPREAD][:MAX_RENDERING_PIXELS]
    if len(chosen) < 32:
        return None, (f"only {len(chosen)} pixels are constant across those games "
                      "- the members do not share one composited overlay")

    chosen.sort()
    packed = [(i << 24) | (mean[i][0] << 16) | (mean[i][1] << 8) | mean[i][2] for i in chosen]

    # Re-measure against the whole corpus, the way BadgeOverlay's own comments are stated
    scored = sorted((distance(mean_flat(mean), e["corner"], chosen), e["game"])
                    for e in corpus.values())
    flagged = [s for s in scored if s[0] <= limit]
    unflagged = [s for s in scored if s[0] > limit]
    return packed, {
        "pixels": len(chosen),
        "games": len({g for _, g in flagged}),
        "flags": len(flagged),
        "worst": flagged[-1][0] if flagged else 0.0,
        "nearest": unflagged[0][0] if unflagged else float("inf"),
        "members": sorted(by_game),
        "flaggedGames": sorted({g for _, g in flagged}),
    }


def mean_flat(mean):
    return [c for pixel in mean for c in pixel]


def emit(name, packed, stats):
    rows = []
    for i in range(0, len(packed), 6):
        rows.append("                " + ", ".join(f"0x{v:08X}" for v in packed[i:i + 6]) + ",")
    return (f"            // {name} - {stats['pixels']} px, {stats['flags']} flags across "
            f"{stats['games']} games, worst {stats['worst']:.1f}, nearest {stats['nearest']:.1f}\n"
            "            new uint[]\n            {\n" + "\n".join(rows) + "\n            },")


# =================================================================================================

def main():
    # Game names carry characters cp1252 cannot encode, and Windows picks cp1252 the moment stdout
    # is a file rather than a console - so redirecting a run to a log killed it mid-report on
    # "Disney-Pixar Toy Story 3". Replace rather than raise: a mangled name in a report is a
    # cosmetic problem, losing the run is not.
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("seeds", help="the sheet's Copy flagged JSON")
    parser.add_argument("--games", type=int, default=800, help="games to sweep for candidates")
    parser.add_argument("--seed", type=int, default=1)
    parser.add_argument("--pages", type=int, nargs="+", default=[0, 1, 2, 3])
    parser.add_argument("--terms", help="file of search terms, one per line, replacing the default")
    parser.add_argument("--any-store", action="store_true",
                        help="drop the supported-store filter, admitting games never released on "
                             "PC. Off by default: those cannot appear in the Xbox app.")
    parser.add_argument("--fit", action="store_true", help="emit C# for groups that hold up")
    parser.add_argument("--member-limit", type=int, default=64,
                        help="near-identical pixels (of 256) before a neighbour counts as a member; "
                             "a quarter of the corner, which is above the coincidence floor")
    parser.add_argument("--workers", type=int, default=8)
    args = parser.parse_args()

    survey = load_survey()
    module = survey.load_shared()
    renderings, scaled, corner, limit = survey.load_renderings()
    print(f"{len(renderings)} existing renderings, limit {limit}")

    out = survey.DEFAULT_OUT
    os.makedirs(out, exist_ok=True)
    api = module.Api(os.path.join(out, "cache"))

    seeds = json.load(open(args.seeds, encoding="utf-8"))
    for s in seeds:
        s["corner"] = corner_of(api.image(s["url"]), scaled, corner)
        if s["corner"] is None:
            sys.exit(f"seed {s['id']} ({s['game']}) could not be decoded")
    print(f"{len(seeds)} seeds")

    mine = survey.local_names(module)

    # Scoped to the stores GamePlatform.cs supports, same as the survey. A rendering fitted against
    # console-exclusive art would be measured on covers that can never reach a tile here, and its
    # "nearest unflagged" margin - the number that decides whether it is safe to ship - would then
    # describe the wrong corpus entirely.
    terms = survey.TERMS
    if args.terms:
        terms = [t.strip() for t in open(args.terms, encoding="utf-8") if t.strip()]
        print(f"{len(terms)} search terms from {os.path.basename(args.terms)}")

    pool = survey.steamspy_games(args.pages)
    pool.update(survey.autocomplete_games(api, terms, args.workers, args.any_store))
    if args.any_store:
        print("  SCOPE FILTER OFF - corpus includes games never released on PC")
    games = sorted((g for g in pool.values()
                    if re.sub(r"[^a-z0-9]", "", g["name"].lower()) not in mine),
                   key=lambda g: g["segment"])
    random.Random(args.seed).shuffle(games)
    games = games[:args.games]
    print(f"sweeping every candidate of {len(games)} games")

    corpus = build_corpus(api, survey, module, games, scaled, corner,
                          args.workers, os.path.join(out, "corners.json"))
    print(f"corpus: {len(corpus)} candidates across "
          f"{len({e['game'] for e in corpus.values()})} games")

    for s in seeds:
        found = neighbours(s["corner"], corpus)
        report(s, found)
        if not args.fit:
            continue
        members = [corpus[url] for shared, _, _, url in found if shared >= args.member_limit]
        packed, stats = fit(members, corpus, limit)
        if packed is None:
            print(f"  NOT FITTED: {stats}")
            continue
        print(f"\n  fitted from {len(stats['members'])} games: {', '.join(stats['members'][:8])}")
        print(f"  {stats['flags']} flags across {stats['games']} games, "
              f"worst {stats['worst']:.1f}, nearest unflagged {stats['nearest']:.1f}")
        if stats["nearest"] - stats["worst"] < 5:
            print("  REJECTED: no margin between flagged and clean - this is not one rendering")
            continue
        print("\n" + emit(re.sub(r"[^A-Za-z0-9]", "", s["game"])[:20], packed, stats))


if __name__ == "__main__":
    main()
