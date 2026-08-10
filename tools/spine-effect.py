"""What would a geometric spine check actually change? Picks moved, rendered for grading.

    python tools/spine-effect.py --games 400

The obvious way to judge a detector is its precision, and for this one that reads badly: 64% on the
covers a human confirmed. But precision is the wrong measure here, because a flag is not a verdict.
ArtworkDownloader SKIPS a flagged candidate and takes the next one, and if every candidate in reach
is flagged it writes the best-ranked anyway. So a false positive costs "rank 2 instead of rank 1",
and on a single-candidate game it costs nothing at all.

The measure that decides it is the one ARTWORK-SELECTION.md uses everywhere else: how many picks
move, and are the ones that move better. This runs the download walk twice over the same games -
once as the app does it today, once with the spine check added as one more skip - and renders every
game where the two disagree, side by side, for grading.

Both arms skip the badge check and the tile-fill check identically, so the only difference is the
spine check. The official-artwork gate is left out of both: it is applied after this point in the
real walk, it costs a capsule fetch per game, and running neither arm through it keeps the
comparison a clean A/B on the one thing being tested.
"""
import argparse
import base64
import importlib.util
import io
import json
import os
import random
import re
import sys
from concurrent.futures import ThreadPoolExecutor

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

SIZE = 96
GRADIENT = 34
MARGIN = (4, 15)
SPINE_BAR = 0.90


def load_survey():
    spec = importlib.util.spec_from_file_location("badge_survey", os.path.join(HERE, "badge-survey.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def spine_score(im):
    """Strongest straight full-height vertical edge near either side, as a fraction of rows."""
    px = im.convert("RGB").resize((SIZE, SIZE), Image.LANCZOS).load()
    best = 0.0
    for side in (0, 1):
        for offset in range(*MARGIN):
            x = offset if side == 0 else SIZE - 1 - offset
            rows = sum(1 for y in range(SIZE)
                       if (abs(px[x - 1, y][0] - px[x, y][0]) + abs(px[x - 1, y][1] - px[x, y][1])
                           + abs(px[x - 1, y][2] - px[x, y][2])) / 3 >= GRADIENT)
            best = max(best, rows / SIZE)
    return best


def walk(api, survey, module, game, badges):
    """The download walk, both arms at once.

    Returns (baseline, with_spine) as (rank, artwork) pairs, or None when the game has no artwork.
    Decoding happens once per candidate and both arms read the same decisions, so the arms cannot
    diverge for any reason other than the spine check itself."""
    renderings, scaled, corner, limit = badges
    pool = survey.candidate_pool(api, module, game)
    if not pool:
        return None
    ranked = module.rank_grids(pool, game["name"])[:survey.MAX_CANDIDATES]

    baseline = with_spine = None
    fallback = None
    for rank, candidate in enumerate(ranked):
        raw = api.image(candidate["url"])
        if not raw:
            continue
        try:
            im = Image.open(io.BytesIO(raw))
            im.load()
        except Exception:
            continue

        if fallback is None:
            fallback = (rank, candidate)
        if survey.badge_distance(im, renderings, scaled, corner)[0] <= limit or not survey.fills_tile(im):
            continue

        if baseline is None:
            baseline = (rank, candidate)
        if with_spine is None and spine_score(im) < SPINE_BAR:
            with_spine = (rank, candidate)
        if baseline and with_spine:
            break

    return (baseline or fallback, with_spine or baseline or fallback)


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")

    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--games", type=int, default=400)
    parser.add_argument("--seed", type=int, default=7)
    parser.add_argument("--pages", type=int, nargs="+", default=[0, 1, 2, 3, 4, 5])
    parser.add_argument("--workers", type=int, default=8)
    args = parser.parse_args()

    survey = load_survey()
    module = survey.load_shared()
    badges = survey.load_renderings()
    api = module.Api(os.path.join(survey.DEFAULT_OUT, "cache"))

    mine = survey.local_names(module)
    pool = survey.steamspy_games(args.pages)
    pool.update(survey.autocomplete_games(api, survey.TERMS, args.workers))
    games = sorted((g for g in pool.values() if survey.normalise(g["name"]) not in mine),
                   key=lambda g: g["segment"])
    random.Random(args.seed).shuffle(games)
    games = games[:args.games]
    print(f"walking {len(games)} games twice")

    moved, examined = [], 0
    with ThreadPoolExecutor(max_workers=args.workers) as executor:
        for game, result in zip(games, executor.map(
                lambda g: walk(api, survey, module, g, badges), games)):
            if not result:
                continue
            examined += 1
            before, after = result
            if before and after and before[1]["id"] != after[1]["id"]:
                moved.append((game["name"], before, after))

    print(f"\n{examined} games with artwork")
    print(f"{len(moved)} picks move ({100 * len(moved) / max(examined, 1):.1f}%)")

    cards = []
    for name, before, after in moved:
        b = module.thumbnail(api.image(before[1]["url"]), size=300) or ""
        a = module.thumbnail(api.image(after[1]["url"]), size=300) or ""
        cards.append(
            f'<div class="pair"><h3>{survey.escape(name)}</h3><div class="two">'
            f'<figure><img src="{b}"><figcaption>now &mdash; rank {before[0] + 1}, id {before[1]["id"]}</figcaption></figure>'
            f'<figure><img src="{a}"><figcaption>with spine check &mdash; rank {after[0] + 1}, id {after[1]["id"]}</figcaption></figure>'
            f'</div><div class="vote"><label><input type="radio" name="v{before[1]["id"]}" value="better">better</label>'
            f'<label><input type="radio" name="v{before[1]["id"]}" value="same">same</label>'
            f'<label><input type="radio" name="v{before[1]["id"]}" value="worse">worse</label></div></div>')

    out = os.path.join(survey.DEFAULT_OUT, "spine-effect.html")
    open(out, "w", encoding="utf-8").write(f"""<!doctype html><meta charset="utf-8"><title>Spine check effect</title>
<style>:root{{color-scheme:dark}}body{{background:#111114;color:#e6e6e9;font:14px "Segoe UI",system-ui;padding:20px;max-width:1100px;margin:0 auto}}
h1{{font-size:18px}}p{{color:#9a9aa4}}.pair{{background:#1a1a20;border-radius:10px;padding:14px;margin-bottom:16px}}
h3{{margin:0 0 10px;font-size:14px}}.two{{display:grid;grid-template-columns:1fr 1fr;gap:14px}}
figure{{margin:0}}figure img{{width:100%;border-radius:8px;background:#000}}
figcaption{{color:#8f8f99;font-size:11px;margin-top:5px}}
.vote{{margin-top:10px;display:flex;gap:16px}}.vote label{{color:#c9c9d1;font-size:13px;cursor:pointer}}
#tally{{position:sticky;bottom:0;background:#16161c;padding:10px;border-radius:8px;margin-top:10px}}</style>
<h1>{len(moved)} of {examined} picks move when the spine check is added</h1>
<p>Left is what the app writes today. Right is what it would write with the geometric spine check
as one more skip. Grade each pair &mdash; the question is not whether the check is accurate, it is
whether the picks it moves end up better.</p>
<div>{''.join(cards)}</div>
<div id="tally">nothing graded yet</div>
<script>document.addEventListener('change',()=>{{const v=[...document.querySelectorAll('input:checked')].map(i=>i.value);
const c=x=>v.filter(y=>y===x).length;document.getElementById('tally').textContent=
`${{c('better')}} better / ${{c('same')}} same / ${{c('worse')}} worse  (of {len(moved)})`}})</script>""")
    print(out)
    return out


if __name__ == "__main__":
    main()
