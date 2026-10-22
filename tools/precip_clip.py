# /// script
# requires-python = ">=3.11"
# dependencies = ["rasterio==1.5.2", "numpy==2.5.3"]
# ///
"""CHELSA V2.1 bio12 (mean annual precipitation, 1981-2010, 30 arc-sec) -> small clip over the map bounds.

Usage: uv run tools/precip_clip.py
Reads only the window covering the heightmap bounds (plus one cell) from the remote GeoTIFF via GDAL /vsicurl/,
applies the file's embedded scale/offset (the file specification lists bio12 as scale 0.1, offset 0; the current
copy on the chelsa02 bucket is stored in whole mm with scale 1.0 embedded: both give the same mm), and writes
content/map/precip_bio12.u16 + precip_bio12.json (source, licence, attribution, units, georeference, retrieval date).
Sources checked 2026-10-10:
  data:     https://os.unil.cloud.switch.ch/chelsa02/chelsa/global/bioclim/bio12/1981-2010/CHELSA_bio12_1981-2010_V.2.1.tif
            (bucket linked from EnviDat "CHELSA V2.1 (current)", doi:10.16904/envidat.228)
  licence:  CC0 1.0 (EnviDat package chelsa-climatologies, license_id CC0-1.0)
  spec:     CHELSA technical specification v1.2, section 7.1: bio12 "annual precipitation amount", kg m-2, scale 0.1, offset 0
"""
import datetime
import json
import math
from pathlib import Path

import numpy as np
import rasterio
from rasterio.windows import Window

import hexgrid as hg

ROOT = Path(__file__).resolve().parents[1]
SOURCE_URL = ("https://os.unil.cloud.switch.ch/chelsa02/chelsa/global/bioclim/bio12/1981-2010/"
              "CHELSA_bio12_1981-2010_V.2.1.tif")
SPEC_URL = ("https://www.envidat.ch/dataset/2adb0c83-4653-4337-af28-f75c63ab7c74/resource/"
            "61fff0a7-6abb-45f7-a5f6-48f6e2c24851/download/chelsa_file_specification.pdf")
LICENCE, LICENCE_URL = "CC0-1.0", "https://creativecommons.org/publicdomain/zero/1.0/"
ATTRIBUTION = ("Precipitation: CHELSA V2.1 bio12 (1981-2010). Karger, D.N., Conrad, O., Böhner, J., Kawohl, T., Kreft, H., "
               "Soria-Auza, R.W., Zimmermann, N.E., Linder, H.P. & Kessler, M. (2021) Climatologies at high resolution for "
               "the earth's land surface areas. EnviDat. https://doi.org/10.16904/envidat.228.v2.1 ; Karger et al. (2017) "
               "Scientific Data 4, 170122. https://doi.org/10.1038/sdata.2017.122")


def window_for(transform, b: dict, margin: int = 1) -> Window:
    """Pixel window covering lon0..lon1 x lat0..lat1 plus `margin` cells (north-up grid)."""
    col0 = math.floor((b["lon0"] - transform.c) / transform.a + 1e-9) - margin
    col1 = math.ceil((b["lon1"] - transform.c) / transform.a - 1e-9) + margin
    row0 = math.floor((transform.f - b["lat1"]) / -transform.e + 1e-9) - margin
    row1 = math.ceil((transform.f - b["lat0"]) / -transform.e - 1e-9) + margin
    return Window(col0, row0, col1 - col0, row1 - row0)


def clip(src_path: str, b: dict, out_dir: Path, source_url: str = SOURCE_URL, retrieved: str | None = None) -> dict:
    with rasterio.open(src_path) as src:
        win = window_for(src.transform, b).intersection(Window(0, 0, src.width, src.height))
        raw = src.read(1, window=win)
        scale, offset = src.scales[0], src.offsets[0]
        nodata = src.nodata
        t = src.window_transform(win)
    mm = raw.astype("float64") * scale + offset
    if nodata is not None:
        mm[raw == nodata] = np.nan
    extra = {"source": "CHELSA V2.1 bio12, mean annual precipitation 1981-2010, 30 arc-sec",
             "source_url": source_url, "spec_url": SPEC_URL, "period": "1981-2010",
             "licence": LICENCE, "licence_url": LICENCE_URL, "attribution": ATTRIBUTION,
             "scale_applied": scale, "offset_applied": offset,
             "retrieved": retrieved or datetime.date.today().isoformat()}
    return hg.write_precip(mm, t.c, t.f, t.a, out_dir, extra)


def main() -> None:
    b = json.loads((hg.MAP_DIR / "heightmap.json").read_text())
    meta = clip("/vsicurl/" + SOURCE_URL, b, hg.PRECIP_DIR)
    pr = hg.load_precip(hg.PRECIP_DIR)
    print(f"wrote {hg.PRECIP_DIR / (hg.PRECIP_NAME + '.u16')}: {meta['width']}x{meta['height']} cells, "
          f"{np.nanmin(pr['mm']):.0f}..{np.nanmax(pr['mm']):.0f} mm/year, scale {meta['scale_applied']}")


if __name__ == "__main__":
    main()
