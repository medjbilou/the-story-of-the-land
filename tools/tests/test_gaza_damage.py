"""The destruction of Gaza as UNOSAT's satellite images record it (content/map/gaza_damage_2023.yaml,
tools/gaza_damage_bake.py): per-cell counts, no raw points, matching UNOSAT's own published totals."""
import base64
import io
import json
import re
from pathlib import Path

import numpy as np
from PIL import Image

import borders_bake as bb
import gaza_damage_bake as gd

ROOT = Path(__file__).resolve().parents[2]
TEXT = (ROOT / "content" / "map" / "gaza_damage_2023.yaml").read_text(encoding="utf-8")
DATES, CELLS, TOTALS = gd.from_file(TEXT)


def same_bake(a: str, b: str) -> bool:
    """Two bakes match when their text matches and every embedded PNG holds the same pixels: zlib builds differ between
    machines (the Mac and CI's Linux), so the PNG bytes may differ while the image is the same."""
    la, lb = a.splitlines(), b.splitlines()
    if len(la) != len(lb):
        return False
    for x, y in zip(la, lb):
        if x == y:
            continue
        kx, _, vx = x.partition(": ")
        ky, _, vy = y.partition(": ")
        if kx != ky or not (vx.startswith("iVBOR") and vy.startswith("iVBOR")):
            return False
        px = np.asarray(Image.open(io.BytesIO(base64.b64decode(vx))))
        py = np.asarray(Image.open(io.BytesIO(base64.b64decode(vy))))
        if px.shape != py.shape or not np.array_equal(px, py):
            return False
    return True
GAZA = next(bb.rings(f["geometry"]) for f in json.loads((ROOT / "content" / "map" / "occupation_1967.geojson").read_text(encoding="utf-8"))["features"]
            if f["properties"].get("id") == "gaza")


def test_the_file_is_its_cells_rendered_again():
    assert same_bake(gd.render(DATES, CELLS, TOTALS), TEXT), "rerun uv run tools/gaza_damage_bake.py"


def test_the_totals_are_unosats_published_ones_and_every_structure_counts_once():
    assert {k: TOTALS[k] for k in gd.PUBLISHED} == gd.PUBLISHED   # 11 October 2025: 123,464 destroyed, 17,116 severe, ...
    assert sum(sum(d) for d, _ in CELLS.values()) == TOTALS["ever_damaged"] == sum(gd.PUBLISHED.values()) + 35   # 35 later 'no visible damage'
    assert sum(sum(x) for _, x in CELLS.values()) == TOTALS["ever_destroyed"] >= gd.PUBLISHED["destroyed"]
    for d, x in CELLS.values():
        assert all(a >= 0 for a in d + x) and sum(x) <= sum(d)


def test_the_images_are_unosats_fourteen_dates_from_the_war_on():
    assert len(DATES) == gd.IMAGES == 14 and DATES == sorted(DATES)
    assert DATES[0] == "2023-10-15" and DATES[-1] == "2025-10-11" and DATES[0] >= gd.WAR_FROM


def test_the_cells_lie_in_the_gaza_strip():
    g = gd.GRID
    lat1 = g["lat0"] + g["ny"] * g["dlat"]
    inside = [bb.contains(GAZA, g["lon0"] + (i + 0.5) * g["dlon"], lat1 - (j + 0.5) * g["dlat"]) for i, j in CELLS]
    assert sum(inside) / len(inside) > 0.97, "cells straddling the outline may centre just outside it, no more"


def test_the_texture_holds_each_cells_half_way_images():
    px = np.array(Image.open(io.BytesIO(base64.b64decode(re.search(r"^tex: (\S+)$", TEXT, re.M)[1]))))
    assert px.shape == (gd.GRID["ny"], gd.GRID["nx"], 4)
    for (i, j), (d, x) in list(CELLS.items())[::97]:
        assert px[j, i, 0] == gd.half_by(d) and px[j, i, 1] == gd.half_by(x)
    assert (px[..., 0] > 0).sum() == len(CELLS)


def test_half_by_is_the_image_by_which_half_are_recorded():
    assert gd.half_by([0] * 14) == 0
    assert gd.half_by([1, 0, 1] + [0] * 11) == 1
    assert gd.half_by([1, 0, 3] + [0] * 11) == 3
    assert gd.parse_runs(gd.runs([0, 2, 0, 5] + [0] * 10)) == [0, 2, 0, 5] + [0] * 10
