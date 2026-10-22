import json

import numpy as np
from PIL import Image

import hexgrid as hg
import preview_map as pm
import heightmap_codec as hc


def test_preview_renders_expected_size_and_marks_settlement(tmp_path):
    n = 201
    elev = np.zeros((n, n), dtype="float32"); elev[:, n // 2:] = 300.0
    meta = {"width": n, "height": n, "lon0": 35.0, "lon1": 35.5, "lat0": 32.2, "lat1": 32.7,
            "elev_min_m": 0.0, "elev_max_m": 300.0,
            "attribution": "dem-credit"}
    (tmp_path / "heightmap.json").write_text(json.dumps(meta))
    hc.write(tmp_path, np.round(elev / 300.0 * 65535).astype("<u2"))
    cell = 0.01
    hg.write_precip(np.full((52, 52), 500.0), 35.0 - cell, 32.7 + cell, cell, tmp_path, {"source": "synthetic", "attribution": "chelsa-credit"})
    (tmp_path / "precip_stations.json").write_text(json.dumps({"attribution": "ims-credit", "stations": []}))
    hexes = hg.build(tmp_path, 5.0, precip_dir=tmp_path)
    towns = [{"id": "x", "name": "Testville", "lon": 35.4, "lat": 32.5, "type": "town"}]
    out = tmp_path / "preview.png"
    pm.render(tmp_path, hexes, towns, out, width_px=400)
    img = Image.open(out)
    assert img.width == 400
    km_w = 0.5 * hg.km_per_deg_lon(meta); km_h = 0.5 * hg.KM_PER_DEG_LAT
    assert abs(img.height - round(400 * km_h / km_w)) <= 1
    x, y = pm.lonlat_to_px(35.4, 32.5, meta, img.size)
    assert img.getpixel((round(x), round(y)))[:3] == pm.DOT_RGB
