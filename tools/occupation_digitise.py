# /// script
# requires-python = ">=3.11"
# dependencies = ["pyshp==2.3.1", "shapely==2.2.0", "pyproj==3.8.0", "requests==2.32.5", "numpy==2.5.3"]
# ///
"""The occupation's map layers from open data, as true shapes with their dates: content/map/occupation_1967.geojson (CC BY,
CC BY-IGO and CC0 sources) and content/map/oslo_areas_1995.geojson (the Oslo areas, whose HDX licence is 'Other': owner-
verify). tools/occupation_bake.py turns both into the layer the terrain draws (content/map/occupation_layers.yaml).

Usage: uv run tools/occupation_digitise.py --raw <dir>   (a directory outside the repository: the HDX zips are downloaded
into it when missing and checked by SHA-256 (SOURCES), and Wikidata is queried live; delete the directory afterwards)

What each feature is (properties.kind), in the projection-free lon/lat of every map tool (WGS84, 5 decimals, about 1 m):
  territory         the West Bank (the union of the 342 village boundaries of the Palestinian Ministry of Planning, gaps
                    under 60 m closed, holes filled) and the Gaza Strip (the union of its 33 municipal boundaries): the lands
                    occupied in June 1967, whose outline is the 1949 line (the Green Line)
  barrier           the West Bank wall, by status (constructed, under construction, planned: the source's 'Projected') as
                    OCHA mapped it in January 2018
  ara               the access-restricted bands along Gaza's fence (100, 300 and 1,000 m) as OCHA mapped them in October 2023
  settlement_area   a settlement's built-up area as Peace Now mapped it from 2011 aerial photos (via OCHA)
  settlement        a settlement's point: each built-up area's representative point, with its founding year from Wikidata
                    (P571, the earliest value in 1967-2026, of a settlement item within NEAR_M of the area, or
                    NAMED_M under the same name) or, with none,
                    2011 (the photos: it stood by then); Wikidata's other settlements inside the West Bank; and the 21 Gaza
                    settlements and the four in the north of the West Bank emptied in August 2005 (EVACUATED_2005)
  oslo_area         (oslo_areas_1995.geojson) Area A (with H1) and Area B (with the nature reserve: open question for the
                    owner), as the Palestinian Ministry of Planning mapped them (HDX dataset date 2004)
Shapes are simplified (SIMPLIFY_M) in UTM 36N, so every line keeps within that distance of its source.
"""
import argparse
import hashlib
import json
import re
import sys
import zipfile
from pathlib import Path

import numpy as np
import pyproj
import requests
import shapefile
import shapely
from shapely import ops
from shapely.geometry import LineString, MultiPolygon, Point, Polygon, mapping, shape
from shapely.validation import make_valid

ROOT = Path(__file__).resolve().parents[1]
MAP = ROOT / "content" / "map"
OUT = MAP / "occupation_1967.geojson"
OUT_OSLO = MAP / "oslo_areas_1995.geojson"
CHECKED = "2026-10-22"
HDX = "https://data.humdata.org/dataset/"
SOURCES = {
    "barrier": {"title": "West Bank Separation Barrier, January 2018", "by": "OCHA occupied Palestinian territory (oPt)",
                "page": HDX + "west-bank-barrier", "licence": "CC-BY-4.0", "date": "2018-01",
                "url": HDX + "e782d4c9-d0ce-412c-ad1e-6771be2969b0/resource/95470fef-0d9d-4967-bbd6-cf247b80d578/download/barrier_jan2018.zip",
                "sha256": "b7b59776327d661122101a65b1d50422e40a6345f428b43e52580bc3396625a3", "shp": "Barrier_Jan2018.shp"},
    "ara": {"title": "Gaza Strip Buffer Area (closed and access restricted areas)", "by": "OCHA occupied Palestinian territory (oPt)",
            "page": HDX + "gaza-strip-closed-and-access-restricted-areas", "licence": "CC-BY-4.0", "date": "2023-10-17",
            "url": HDX + "26e56b0e-f838-45e7-9efe-2598a5324a56/resource/f083c03e-b746-4500-8504-0403d9ee3633/download/buffers4.zip",
            "sha256": "50d34dddf64f17e4c115d30801c99c199f47128af8a0a24f0235871ab3f279bd",
            "shp": "Closed_and_access_restricted_areas_GazaStrip_cleaned4.shp"},
    "settlements": {"title": "State of Palestine Settlements (built-up areas, Peace Now, from 2011 aerial photos)",
                    "by": "Peace Now and OCHA oPt", "page": HDX + "state-of-palestine-settlements", "licence": "CC-BY-IGO-3.0",
                    "date": "2011", "url": HDX + "0d6d3717-afc1-4f6b-a003-c7e458298928/resource/05d6421b-e796-4940-ae9e-a55858e96664/download/settlements_peacenow.zip",
                    "sha256": "61bb80c00697312b5b4fed405f5ed970d2a4aa061c43c2a1b8705b8408ec91c4", "shp": "Settlements_Buildup_PeaceNow.shp"},
    "villages": {"title": "State of Palestine - Village boundary in the West Bank", "by": "Palestinian Authority, Ministry of Planning (via OCHA oPt)",
                 "page": HDX + "state-of-palestine-village-boundary-in-the-west-bank", "licence": "CC-BY-4.0", "date": "2019-07-24",
                 "url": HDX + "8e88b6e5-c8e8-45b3-afa1-268530aadae1/resource/70c36308-91ed-4e21-9a25-e0aefb4e756f/download/villageboundary.zip",
                 "sha256": "e69271d46eac8ae7e3a3497f9537bad9be55093fa401780434dcef0165b4ec8f", "shp": "VillageBoundary.shp"},
    "gaza": {"title": "State of Palestine - Gaza Strip Municipal Boundaries", "by": "Gaza Housing Council (via OCHA oPt)",
             "page": HDX + "gaza-stripmunicipal-boundaries", "licence": "CC-BY-4.0", "date": "2019-07-18",
             "url": HDX + "6b79f7ab-9e13-48e5-9f58-dc45b4a9222c/resource/f131956a-8cc6-424c-a831-5b8b0d6956b9/download/gazastrip_municipalboundaries.zip",
             "sha256": "3f17760a0c9392d52df0463f995e0ff994773aa21e19b9cf3c6ba97fcd00849f", "shp": "GazaStrip_MunicipalBoundaries.shp"},
    "oslo": {"title": "State of Palestine - Oslo Agreement in the West Bank", "by": "Palestinian Authority, Ministry of Planning (via OCHA oPt)",
             "page": HDX + "state-of-palestine-other-0-0-0-0-0", "licence": "unknown", "date": "2004",
             "licence_note": "HDX 'Other': the legacy HumanitarianResponse.info terms (data.humdata.org/about/license/legacy_hrinfo), which redirected to a page without them on 2026-10-22",
             "url": HDX + "d3a843df-a640-4e9d-b867-8ca974c2aa74/resource/86f2707a-dc64-46af-bd41-b31e95611b45/download/osloagreement.zip",
             "sha256": "7dc96c808cd6e1b95c7e05c4491890453144889998f9c76f4857a8a0ef5ac999", "shp": "OsloAgreement.shp"},
}
WIKIDATA = {"title": "Wikidata: items that are an Israeli settlement (Q582706) and the settlements emptied in 2005", "by": "Wikidata contributors",
            "page": "https://query.wikidata.org/", "licence": "CC0-1.0"}
SPARQL = "https://query.wikidata.org/sparql"
# The 21 Gaza settlements and the four in the northern West Bank emptied in August 2005 (all 21 named by Al Jazeera, 16 Sep
# 2025, "Mapping the 21 illegal settlements Israel had in Gaza 20 years ago"; the four by its timeline of 14 Aug 2005), by
# their Wikidata items (several are not typed Q582706 there).
EVACUATED_2005 = {
    "Q486555": "Bedolah", "Q2910044": "Bnei Atzmon", "Q2909990": "Dugit", "Q917089": "Elei Sinai", "Q2909386": "Gadid",
    "Q2909281": "Gan Or", "Q1425179": "Ganei Tal", "Q599665": "Kfar Darom", "Q2909483": "Kfar Yam", "Q785793": "Katif",
    "Q6393775": "Kerem Atzmona", "Q2777077": "Morag", "Q991346": "Netzarim", "Q2909782": "Netzer Hazani", "Q249046": "Neve Dekalim",
    "Q2909935": "Nisanit", "Q2910018": "Pe'at Sadeh", "Q425527": "Rafiah Yam", "Q2909110": "Shirat HaYam", "Q2909059": "Slav",
    "Q2909775": "Tel Katifa",
    "Q2909325": "Ganim", "Q2891037": "Kadim", "Q2909708": "Homesh", "Q1759887": "Sa-Nur",
}
EVACUATED_ON = "2005-08-22"   # Al Jazeera (16 Sep 2025): "All 21 settlements in Gaza are emptied" on 22 August 2005
FIRST_YEAR, LAST_YEAR = 1967, 2026
PHOTO_YEAR = 2011
NEAR_M = 400.0       # a Wikidata settlement point within this of a built-up area dates it
NAMED_M = 5000.0     # ... or within this when the two names agree (Wikidata's point is often off the houses)
CLOSE_M = 30.0       # village boundaries: gaps under twice this are closed before the union
SIMPLIFY_M = {"territory": 15.0, "barrier": 10.0, "ara": 5.0, "settlement_area": 8.0, "oslo_area": 10.0}
MIN_AREA_M2 = {"settlement_area": 2000.0, "oslo_area": 5000.0}
BARRIER_STATUS = {"Constructed": "constructed", "Under Construction": "under_construction", "Projected": "planned"}
OSLO_CLASS = {"H1": "A", "Nature Reserve": "B"}   # the rest (C, H2, East Jerusalem, no man's land): no Oslo area of their own
# The source's CLASS reads "A" on two records. OBJECTID 31 (982 km2) holds the cities (Nablus, Jenin, Ramallah, Bethlehem,
# Jericho...): Area A. OBJECTID 2 (1,035 km2, 212 parts) holds the villages around them (Beit Ummar...) and with the nature
# reserve (167 km2) makes about 21% of the West Bank: Area B (Oslo II: A about 18%, B about 22%; e1995_09_oslo2).
OSLO_BY_ID = {31: "A", 2: "B"}
UTM = pyproj.CRS("EPSG:32636")
TO_UTM = pyproj.Transformer.from_crs("EPSG:4326", UTM, always_xy=True)
TO_LL = pyproj.Transformer.from_crs(UTM, "EPSG:4326", always_xy=True)


def fetch(raw: Path) -> dict:
    """{key: unzipped shapefile path}: each source downloaded into raw/<key>/ when missing, checked by SHA-256."""
    out = {}
    for key, s in SOURCES.items():
        d = raw / key
        d.mkdir(parents=True, exist_ok=True)
        z = d / Path(s["url"]).name
        if not z.exists():
            r = requests.get(s["url"], timeout=120)
            r.raise_for_status()
            z.write_bytes(r.content)
        got = hashlib.sha256(z.read_bytes()).hexdigest()
        if got != s["sha256"]:
            raise SystemExit(f"{z}: SHA-256 {got}, expected {s['sha256']} (the dataset changed: check it, then update SOURCES)")
        with zipfile.ZipFile(z) as zf:
            for m in zf.namelist():   # flat members only: nothing outside d
                if "/" in m.strip("/") or m.startswith(("/", "..")):
                    raise SystemExit(f"{z}: unexpected member {m}")
            zf.extractall(d)
        out[key] = d / s["shp"]
    return out


def read(path: Path) -> list:
    """[(record dict, shapely geometry in UTM 36N)]: the shapefile's .prj says whether it is in UTM 36N or WGS84."""
    utm = "UTM_Zone_36N" in path.with_suffix(".prj").read_text()
    rows = []
    with shapefile.Reader(str(path)) as r:
        for sr in r.iterShapeRecords():
            g = shape(sr.shape.__geo_interface__)
            if not utm:
                g = shapely.transform(g, lambda c: np.column_stack(TO_UTM.transform(c[:, 0], c[:, 1])))
            rows.append((sr.record.as_dict(), make_valid(g) if g.geom_type in ("Polygon", "MultiPolygon") else g))
    return rows


def polygons(g) -> list:
    if g.is_empty:
        return []
    if g.geom_type == "Polygon":
        return [g]
    if g.geom_type in ("MultiPolygon", "GeometryCollection"):
        return [p for x in g.geoms for p in polygons(x)]
    return []


def outline(geoms: list, close_m: float):
    """The largest polygon of the union, its gaps under 2 close_m closed and its holes filled."""
    u = ops.unary_union(geoms).buffer(close_m, join_style="mitre").buffer(-close_m, join_style="mitre")
    biggest = max(polygons(u), key=lambda p: p.area)
    return Polygon(biggest.exterior)


def lonlat(g):
    """UTM -> lon/lat rounded to 5 decimals (about 1 m)."""
    g = shapely.transform(g, lambda c: np.column_stack(TO_LL.transform(c[:, 0], c[:, 1])))
    return json.loads(json.dumps(mapping(g)), parse_float=lambda s: round(float(s), 5))


def clean(kind: str, g):
    """Simplified (topology kept) and, for areas, small parts dropped."""
    g = g.simplify(SIMPLIFY_M[kind], preserve_topology=True)
    if kind in MIN_AREA_M2:
        parts = [p for p in polygons(g) if p.area >= MIN_AREA_M2[kind]]
        g = MultiPolygon(parts) if len(parts) > 1 else (parts[0] if parts else Polygon())
    return g


def feature(kind: str, g, **props) -> dict:
    return {"type": "Feature", "properties": {"kind": kind, **props}, "geometry": lonlat(g)}


def wikidata() -> list:
    """[{qid, name, lon, lat, years, dissolved}] for every Q582706 item in the map's box and every EVACUATED_2005 item."""
    values = " ".join("wd:" + q for q in EVACUATED_2005)
    query = ("SELECT ?s ?sLabel ?coord ?inc ?dis WHERE { "
             "{ SERVICE wikibase:box { ?s wdt:P625 ?coord . bd:serviceParam wikibase:cornerSouthWest \"Point(34.15 31.15)\"^^geo:wktLiteral . "
             "bd:serviceParam wikibase:cornerNorthEast \"Point(35.65 32.65)\"^^geo:wktLiteral . } ?s wdt:P31 wd:Q582706 . } "
             f"UNION {{ VALUES ?s {{ {values} }} ?s wdt:P625 ?coord . }} "
             "OPTIONAL { ?s wdt:P571 ?inc } OPTIONAL { ?s wdt:P576 ?dis } "
             "SERVICE wikibase:label { bd:serviceParam wikibase:language \"en\". } }")
    r = requests.get(SPARQL, params={"query": query}, headers={"Accept": "application/sparql-results+json",
                     "User-Agent": "StoryOfTheLand-occupation-digitise/1.0 (https://github.com/; map data bake)"}, timeout=120)
    r.raise_for_status()
    items = {}
    for b in r.json()["results"]["bindings"]:
        qid = b["s"]["value"].rsplit("/", 1)[1]
        it = items.setdefault(qid, {"qid": qid, "name": b["sLabel"]["value"], "coords": set(), "years": set(), "dissolved": set()})
        m = re.match(r"Point\(([-\d.]+) ([-\d.]+)\)", b["coord"]["value"])
        if m:
            it["coords"].add((float(m[1]), float(m[2])))
        if "inc" in b and re.match(r"\d{4}-", b["inc"]["value"]):
            it["years"].add(int(b["inc"]["value"][:4]))
        if "dis" in b:
            it["dissolved"].add(b["dis"]["value"][:10])
    out = []
    for it in sorted(items.values(), key=lambda x: int(x["qid"][1:])):
        if not it["coords"]:
            continue
        lon, lat = sorted(it["coords"])[0]
        out.append({"qid": it["qid"], "name": EVACUATED_2005.get(it["qid"], it["name"]), "lon": lon, "lat": lat,
                    "years": sorted(it["years"]), "dissolved": sorted(it["dissolved"])})
    return out


def founding(years: list) -> int | None:
    """The earliest Wikidata inception in the occupation's years (a value before June 1967 is another place's or an error)."""
    ok = [y for y in years if FIRST_YEAR <= y <= LAST_YEAR]
    return min(ok) if ok else None


def key(name: str) -> str:
    """A name as both sources spell it: lower case, letters only ("Giva't Ze'ev" and "Givat Zeev" agree)."""
    return re.sub(r"[^a-z]", "", name.lower())


def settlements(areas: list, wd: list, west_bank, gaza) -> tuple[list, list]:
    """(settlement_area features, settlement point features), deterministic: areas in file order, points by area then QID."""
    pts = [(w, Point(TO_UTM.transform(w["lon"], w["lat"]))) for w in wd]
    area_feats, point_feats, used = [], [], set()
    for rec, g in areas:
        name = (rec.get("Name") or "").strip()
        near = sorted(((g.distance(p), w["qid"], w) for w, p in pts if founding(w["years"]) and (g.distance(p) <= NEAR_M
                       or (g.distance(p) <= NAMED_M and key(w["name"]) == key(name) != ""))), key=lambda t: (t[0], int(t[1][1:])))
        years = [founding(w["years"]) for _, _, w in near]
        year = min(years) if years else PHOTO_YEAR
        qids = [q for _, q, _ in near]
        used.update(qids)
        g = clean("settlement_area", g)
        if g.is_empty:
            continue
        area_feats.append(feature("settlement_area", g, name=name, gis_id=int(rec.get("GIS_ID") or 0)))
        rp = g.representative_point()
        point_feats.append(feature("settlement", rp, name=name, from_year=year, until=None,
                                   basis="wikidata" if years else "aerial_photos_2011", wikidata=qids))
    for w, p in pts:
        if w["qid"] in used:
            continue
        gone = w["qid"] in EVACUATED_2005
        y = founding(w["years"])
        if y is None or not (gone or west_bank.contains(p)):
            continue
        if gone and not (gaza.buffer(200).contains(p) or west_bank.contains(p)):
            raise SystemExit(f"{w['name']} ({w['qid']}): an evacuated settlement outside Gaza and the West Bank")
        point_feats.append(feature("settlement", p, name=w["name"], from_year=y, until=EVACUATED_ON if gone else None,
                                   basis="wikidata", wikidata=[w["qid"]]))
    missing = sorted(set(EVACUATED_2005) - {q for f in point_feats for q in f["properties"]["wikidata"]})
    if missing:
        print("evacuated settlements without a founding year or point in Wikidata (not drawn):", ", ".join(EVACUATED_2005[q] for q in missing))
    return area_feats, point_feats


def collection(meta: dict, feats: list) -> str:
    doc = {"type": "FeatureCollection", "storyoftheland": meta, "features": feats}
    return json.dumps(doc, ensure_ascii=False, indent=None, separators=(",", ":")).replace("},{\"type\":\"Feature\"", "},\n{\"type\":\"Feature\"") + "\n"


def source_meta(keys: list) -> list:
    return [{"key": k, **{f: v for f, v in SOURCES[k].items() if f != "shp"}, "checked": CHECKED} for k in keys]


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--raw", type=Path, required=True, help="download directory outside the repository")
    a = ap.parse_args()
    if ROOT in a.raw.resolve().parents:
        raise SystemExit("--raw must be outside the repository (raw downloads are not committed)")
    shp = fetch(a.raw)
    west_bank = outline([g for _, g in read(shp["villages"])], CLOSE_M)
    gaza = outline([g for _, g in read(shp["gaza"])], CLOSE_M)
    feats = [feature("territory", clean("territory", west_bank), id="west_bank", name="West Bank (with East Jerusalem)"),
             feature("territory", clean("territory", gaza), id="gaza", name="Gaza Strip")]
    for rec, g in read(shp["barrier"]):
        status = BARRIER_STATUS[rec["Status"].strip()]
        feats.append(feature("barrier", clean("barrier", g), status=status, type=(rec.get("Type") or "").strip()))
    for rec, g in sorted(read(shp["ara"]), key=lambda t: t[0]["BUFF_DIST"]):
        feats.append(feature("ara", clean("ara", g), band_m=int(rec["BUFF_DIST"])))
    wd = wikidata()
    areas, points = settlements(read(shp["settlements"]), wd, west_bank, gaza)
    feats += areas + points
    meta = {"title": "The occupation's map layers, June 1967 on: the occupied territory, the wall, Gaza's fence and access-restricted area, settlements",
            "status": "draft, owner-verify",
            "sources": source_meta(["villages", "gaza", "barrier", "ara", "settlements"]) + [{**WIKIDATA, "checked": CHECKED}],
            "west_bank_km2": round(west_bank.area / 1e6, 1), "gaza_km2": round(gaza.area / 1e6, 1),
            "simplify_m": {k: v for k, v in SIMPLIFY_M.items() if k != "oslo_area"}}
    OUT.write_text(collection(meta, feats), encoding="utf-8")
    oslo = []
    for rec, g in read(shp["oslo"]):
        cls = OSLO_BY_ID.get(rec["OBJECTID"]) or OSLO_CLASS.get(rec["CLASS"].strip())
        if cls:
            g = clean("oslo_area", g)
            if not g.is_empty:
                oslo.append(feature("oslo_area", g, area=cls, source_class=rec["CLASS"].strip()))
    OUT_OSLO.write_text(collection({"title": "Oslo II areas A and B in the West Bank (Area C is the rest of the West Bank)", "status": "draft, owner-verify (licence)",
                                    "sources": source_meta(["oslo"]), "simplify_m": SIMPLIFY_M["oslo_area"]}, oslo), encoding="utf-8")
    n = {k: sum(1 for f in feats if f["properties"]["kind"] == k) for k in ("barrier", "ara", "settlement_area", "settlement")}
    print(f"wrote {OUT.relative_to(ROOT)} ({OUT.stat().st_size // 1024} KB; West Bank {meta['west_bank_km2']} km2, Gaza {meta['gaza_km2']} km2; {n}) "
          f"and {OUT_OSLO.relative_to(ROOT)} ({OUT_OSLO.stat().st_size // 1024} KB, {len(oslo)} areas)")


if __name__ == "__main__":
    sys.exit(main())
