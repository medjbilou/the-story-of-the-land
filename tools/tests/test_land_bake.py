import numpy as np
import pytest
from PIL import Image

import land_bake as lb


def test_the_hash_is_deterministic_uniform_and_seeded():
    i = np.arange(-500, 500)
    h = lb.hash01(i, i * 3, 7)
    assert np.array_equal(h, lb.hash01(i, i * 3, 7)) and not np.array_equal(h, lb.hash01(i, i * 3, 8))
    assert h.min() >= 0.0 and h.max() < 1.0 and abs(h.mean() - 0.5) < 0.05


def test_the_painted_land_is_weighted_by_the_masks_and_holds_every_crop():
    x, y = np.meshgrid(np.arange(0, 20000, 50.0), np.arange(0, 20000, 50.0))
    zero, one = np.zeros_like(x), np.ones_like(x)
    _, w = lb.paint_samples(x, y, zero, zero, zero, zero + 50.0, zero)
    assert w.max() == 0.0                                    # no fields, not wet: the biome's ground
    c, w = lb.paint_samples(x, y, one, zero, zero, zero + 50.0, zero)
    assert w.mean() > 0.8 and np.isfinite(c).all()          # farmed: parcels nearly everywhere
    # Each crop covers a share of the ground (its colour, give or take the rows and the parcel's shade).
    for name, share in (("green", 0.10), ("ochre", 0.08), ("plough", 0.06), ("fallow", 0.04)):
        ref = lb.linear(lb.SRGB[name])
        near = np.linalg.norm(c / np.maximum(c.mean(axis=-1, keepdims=True), 1e-6) - ref / ref.mean(), axis=-1) < 0.06
        assert near.mean() > share, name


@pytest.mark.parametrize("name, size_m, step_m", [("HEDGE_HALF_M", (8000, 2000), 5.0), ("TRACK_HALF_M", (36000, 4000), 14.0)])
def test_field_lines_are_wide_enough_for_the_texture(name, size_m, step_m, monkeypatch):
    # Land round 2: round 1's 18 m hedges and 30 m tracks were averaged away in the texture's 79 x 105 m texel. A line's
    # own samples are those that change when its half width is 0; runs of them along east-west rows cross the lines at
    # their angles (and the blocks' wobble stretches them by up to about 20% either way), so the median run measures the
    # line across: at least 1.5 texels of the texel's 79 m (east-west) side; across its 105 m north-south side that is about
    # 1.2 texels, as only east-west rows are sampled. (Tracks lie only on some 5.2 km block edges, so they need the larger
    # ground.)
    x, y = np.meshgrid(np.arange(0, size_m[0], step_m), np.arange(0, size_m[1], step_m))
    zero, one = np.zeros_like(x), np.ones_like(x)
    with_lines, _ = lb.paint_samples(x, y, one, zero, zero, zero + 50.0, zero)
    monkeypatch.setattr(lb, name, 0.0)
    without, _ = lb.paint_samples(x, y, one, zero, zero, zero + 50.0, zero)
    line = np.abs(with_lines - without).max(axis=-1) > 1e-6
    runs = []
    for row in line:
        edges = np.flatnonzero(np.diff(np.concatenate([[0], row.astype(np.int8), [0]])))
        runs += [(b - a) * step_m for a, b in zip(edges[::2], edges[1::2]) if a > 0 and b < row.size]
    assert line.mean() > 0.002 and len(runs) > 100
    assert np.median(runs) >= 1.5 * 79.0, f"{name}: lines {np.median(runs):.0f} m across, under 1.5 texels"


def test_empty_texels_take_the_neighbouring_land_colour():
    rgb = np.zeros((8, 8, 3)); filled = np.zeros((8, 8), dtype=bool)
    rgb[:, :2] = (0.5, 0.4, 0.2); filled[:, :2] = True
    out = lb.dilate_colour(rgb, filled, passes=8)
    assert np.allclose(out, (0.5, 0.4, 0.2)), "no black spreads into the mips"


def test_the_land_reaches_into_the_water_and_avoids_the_rock():
    x, y = np.meshgrid(np.arange(0, 5000, 50.0), np.arange(0, 5000, 50.0))
    zero, one = np.zeros_like(x), np.ones_like(x)
    _, w_flat = lb.paint_samples(x, y, zero, zero, one, zero + 400.0, zero)
    _, w_cliff = lb.paint_samples(x, y, zero, zero, one, zero + 400.0, zero + 1.0)   # rise 1 per metre: rock
    assert w_flat.mean() > 0.5 and w_cliff.max() < 0.01


@pytest.fixture(scope="module")
def baked():
    return lb.bake()


def test_the_committed_texture_is_the_bake(baked):
    # Review M11: the committed PNG must be what tools/land_bake.py makes from today's inputs (about 20 s).
    assert np.array_equal(np.asarray(Image.open(lb.OUT)), baked), "landuse.png is stale: run uv run tools/land_bake.py"


def test_the_committed_texture_has_its_size_and_follows_the_ground():
    land = np.asarray(Image.open(lb.OUT))
    assert land.shape == (lb.COLOUR_H, lb.COLOUR_W, 4)

    def weight(lon, lat):
        r, c = int((33.35 - lat) / 3.9 * land.shape[0]), int((lon - 34.2) / 1.7 * land.shape[1])
        return land[r - 6:r + 7, c - 6:c + 7, 3].mean() / 255
    assert weight(34.84, 31.92) > 0.8 and weight(35.30, 32.62) > 0.8    # the coastal plain and the Jezreel valley farmed
    assert weight(34.90, 30.60) < 0.02 and weight(35.50, 31.50) < 0.02  # the central Negev and the Dead Sea untouched
    assert weight(35.15, 31.78) > 0.4                                   # garrigue and terraces on the Judean hills
    assert land[..., :3][land[..., 3] == 0].min(axis=0).max() > 0, "the empty texels hold land colour, not black"
