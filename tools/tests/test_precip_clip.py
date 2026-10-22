import json

import numpy as np
import pytest
import rasterio
from rasterio.transform import from_origin

import hexgrid as hg
import precip_clip as pc


def make_chelsa_like(path, scale, offset, nodata=65535):
    """1 x 1 degree uint16 GeoTIFF over lon 34-35, lat 31-32 at 0.1 deg, with embedded scale/offset like CHELSA."""
    raw = np.arange(100, dtype="uint16").reshape(10, 10) * 10 + 1000   # row 0 = north
    raw[0, 0] = nodata
    with rasterio.open(path, "w", driver="GTiff", height=10, width=10, count=1, dtype="uint16", nodata=nodata,
                       crs="EPSG:4326", transform=from_origin(34.0, 32.0, 0.1, 0.1)) as ds:
        ds.write(raw, 1)
        ds.scales = (scale,); ds.offsets = (offset,)
    return raw


@pytest.mark.parametrize("scale", [0.1, 1.0])
def test_clip_applies_embedded_scale_and_keeps_north_up(tmp_path, scale):
    src = tmp_path / "bio12.tif"
    raw = make_chelsa_like(src, scale, 0.0)
    bounds = {"lon0": 34.25, "lon1": 34.55, "lat0": 31.55, "lat1": 31.95}
    meta = pc.clip(str(src), bounds, tmp_path / "out", source_url="file://x", retrieved="2026-10-10")
    pr = hg.load_precip(tmp_path / "out")
    # window covers the bounds plus a one-cell margin: cols 1..6 (lon 34.1-34.7), rows 0..5 (lat 32.0-31.4)
    assert meta["lon0"] == pytest.approx(34.1) and meta["lat1"] == pytest.approx(32.0)
    assert pr["mm"].shape == (meta["height"], meta["width"]) == (6, 6)
    assert pr["mm"][0, 0] == pytest.approx(raw[0, 1] * scale)          # north-west cell of the clip
    assert pr["mm"][5, 5] == pytest.approx(raw[5, 6] * scale)
    assert meta["scale_applied"] == scale and meta["units"] == "mm/year"


def test_clip_maps_nodata_to_nan_and_writes_sidecar(tmp_path):
    src = tmp_path / "bio12.tif"
    make_chelsa_like(src, 0.1, 0.0)
    bounds = {"lon0": 34.0, "lon1": 34.3, "lat0": 31.7, "lat1": 32.0}
    pc.clip(str(src), bounds, tmp_path, source_url="file://x", retrieved="2026-10-10")
    pr = hg.load_precip(tmp_path)
    assert np.isnan(pr["mm"][0, 0])
    side = json.loads((tmp_path / "precip_bio12.json").read_text())
    for k in ("source_url", "licence", "licence_url", "attribution", "units", "retrieved", "period", "lon0", "lat1",
              "cell_deg", "width", "height"):
        assert k in side, k
    assert "Karger" in side["attribution"] and side["licence"] == "CC0-1.0"
