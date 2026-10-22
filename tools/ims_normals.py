# /// script
# requires-python = ">=3.11"
# dependencies = ["numpy==2.5.3", "requests==2.32.5"]
# ///
"""Israel Meteorological Service (IMS) rain-gauge normals for 1981-2010 -> content/map/precip_stations.json.

Usage: uv run tools/ims_normals.py   (run after tools/precip_clip.py; keeps only gauges inside the clip)
Source: the IMS official data archive on data.gov.il (dataset "481", publisher IMS, licence as stated
on the dataset: "Other (Open)", id other-open, no licence URL given), read through the CKAN datastore API because
the direct CSV downloads are blocked (HTTP 403). Raw API pages are cached in tools/.cache/ims/ (not committed).
Rules (IMS data guide, "-_-2021.pdf" in the same dataset):
  - rain year = 1 August .. 31 July, labelled here by the year it ends;
  - monthly rain codes 0 (normal), 7/8 (partly interpolated by IMS), 9 (trace) are used; 5 (doubtful) is not;
  - a rain year counts only if October..May are all present; yearly storage-gauge totals (rain storage gauges) are used as given,
    with their time_obs taken as the rain-year label;
  - gauges within 1 km of each other are one site (renumbered or replaced instruments); the lowest station number wins
    where two report the same year;
  - a site needs at least 20 rain years in 1981-2010 (CHELSA bio12's period)."""
import datetime
import json
import math
from pathlib import Path

import numpy as np
import requests

from geo import KM_PER_DEG_LAT, KM_PER_DEG_LON_EQ
import hexgrid as hg

ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / "tools" / ".cache" / "ims"
API = "https://data.gov.il/api/3/action/datastore_search"
DATASET = "https://data.gov.il/dataset/481"
RES_STATIONS = "83841660-b9c4-4ecc-a403-d435b3e8c92f"
RES_MONTHLY = "53a21a1a-b083-44c0-a3ce-19fdc01eebcd"
RES_YEARLY = "c141f389-e1c8-45b9-b041-ad9565a38fbc"
GUIDE = f"{DATASET}/resource/097db308-5952-424d-b34b-676c281d0e78"
OK_CODES = {0, 7, 8, 9}
RAINY_MONTHS = (10, 11, 12, 1, 2, 3, 4, 5)
FIRST, LAST, MIN_YEARS, MERGE_KM = 1981, 2010, 20, 1.0
ATTRIBUTION = ("Rain-gauge data: Israel Meteorological Service, meteorological data archive, "
               "data.gov.il dataset 481 (https://data.gov.il/dataset/481)")


def resource_url(res: str) -> str:
    return f"{DATASET}/resource/{res}"


def monthly_to_rain_years(records: list[dict], required=RAINY_MONTHS, ok_codes=OK_CODES) -> dict:
    """{stn: {rain_year: total mm}} from IMS monthly records; a year needs every `required` month with a usable code."""
    months: dict = {}
    for r in records:
        if r["rain_code"] in ok_codes and r["rain_ttl"] is not None:
            ry = int(r["year"]) + (1 if int(r["month"]) >= 8 else 0)
            months.setdefault((int(r["stn_num"]), ry), {})[int(r["month"])] = float(r["rain_ttl"])
    out: dict = {}
    for (stn, ry), m in sorted(months.items()):
        if all(k in m for k in required):
            out.setdefault(stn, {})[ry] = round(sum(m[k] for k in sorted(m)), 1)
    return out


def yearly_to_rain_years(records: list[dict], ok_codes=OK_CODES) -> dict:
    out: dict = {}
    for r in records:
        if r["rain_code"] in ok_codes and r["rain_ttl"] is not None:
            out.setdefault(int(r["stn_num"]), {}).setdefault(int(r["time_obs"]), float(r["rain_ttl"]))
    return out


def normals(annual: dict, stations: dict, first=FIRST, last=LAST, min_years=MIN_YEARS, merge_km=MERGE_KM) -> list[dict]:
    kx = KM_PER_DEG_LON_EQ * math.cos(math.radians(31.4))
    sites: list[dict] = []
    for stn in sorted(annual):
        if stn not in stations:
            continue
        years = {y: v for y, v in annual[stn].items() if first <= y <= last}
        if not years:
            continue
        m = stations[stn]
        lon, lat = float(m["stn_long"]), float(m["stn_lat"])
        site = next((s for s in sites if math.hypot((s["lon"] - lon) * kx, (s["lat"] - lat) * KM_PER_DEG_LAT) <= merge_km), None)
        if site is None:
            site = {"ids": [], "name": m["stn_name"].strip(), "lon": lon, "lat": lat, "by_year": {}}
            sites.append(site)
        site["ids"].append(stn)
        for y, v in sorted(years.items()):
            site["by_year"].setdefault(y, v)
    out = []
    for s in sites:
        if len(s["by_year"]) >= min_years:
            out.append({"ids": s["ids"], "name": s["name"], "lon": round(s["lon"], 4), "lat": round(s["lat"], 4),
                        "mm": round(float(np.mean(list(s["by_year"].values()))), 1), "years": len(s["by_year"])})
    return out


def fetch_all(res: str, cache: Path = CACHE, page: int = 32000) -> list[dict]:
    path = cache / f"{res}.json"
    if path.exists():
        return json.loads(path.read_text())
    records, offset = [], 0
    while True:
        r = requests.get(API, params={"resource_id": res, "limit": page, "offset": offset}, timeout=600,
                         headers={"User-Agent": "the-story-of-the-land-map-tools"})
        r.raise_for_status()
        batch = r.json()["result"]["records"]
        records += batch
        if len(batch) < page:
            break
        offset += page
    cache.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(records))
    return records


def main() -> None:
    stations = {int(r["stn_num"]): r for r in fetch_all(RES_STATIONS)}
    monthly = monthly_to_rain_years(fetch_all(RES_MONTHLY))
    yearly = yearly_to_rain_years(fetch_all(RES_YEARLY))
    annual = {k: dict(v) for k, v in monthly.items()}
    for stn, ys in yearly.items():
        for y, v in ys.items():
            annual.setdefault(stn, {}).setdefault(y, v)
    clip = json.loads((hg.PRECIP_DIR / f"{hg.PRECIP_NAME}.json").read_text())
    lon1 = clip["lon0"] + clip["width"] * clip["cell_deg"]; lat0 = clip["lat1"] - clip["height"] * clip["cell_deg"]
    sites = [s for s in normals(annual, stations) if clip["lon0"] <= s["lon"] <= lon1 and lat0 <= s["lat"] <= clip["lat1"]]
    for s in sites:
        s["source_url"] = resource_url(RES_MONTHLY if any(i in monthly for i in s["ids"]) else RES_YEARLY)
    doc = {"source": "Israel Meteorological Service rain gauges, annual normals 1981-2010",
           "source_urls": [DATASET, resource_url(RES_STATIONS), resource_url(RES_MONTHLY), resource_url(RES_YEARLY), GUIDE],
           "licence": "Other (Open) (data.gov.il licence id other-open; no licence URL on the dataset page)",
           "attribution": ATTRIBUTION, "retrieved": datetime.date.today().isoformat(), "period": f"{FIRST}-{LAST}",
           "units": "mm/year",
           "selection": (f"rain year Aug-Jul labelled by ending year; monthly codes {sorted(OK_CODES)} used; Oct-May required; "
                         f"storage-gauge yearly totals as given; gauges within {MERGE_KM} km merged (lowest id first); "
                         f">= {MIN_YEARS} rain years in {FIRST}-{LAST}; inside the CHELSA clip"),
           "method": ("used by tools/hexgrid.py: ratio = station mm / CHELSA bio12 mm (bilinear at the gauge); IDW (power 2, "
                      "km) of log(ratio) to every clip cell; corrected = CHELSA x exp(IDW log ratio); desert = corrected "
                      "hex mean < 200 mm"),
           "stations": sites}
    out = hg.PRECIP_DIR / hg.STATIONS_NAME
    head = json.dumps({k: v for k, v in doc.items() if k != "stations"}, indent=1, ensure_ascii=False)[:-2]
    rows = ",\n  ".join(json.dumps(s, ensure_ascii=False) for s in sites)
    out.write_text(f'{head},\n "stations": [\n  {rows}\n ]\n}}\n')   # one gauge per line
    print(f"wrote {out}: {len(sites)} sites")


if __name__ == "__main__":
    main()
