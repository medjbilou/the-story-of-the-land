"""The occupation's map layers: the true shapes (content/map/occupation_1967.geojson, oslo_areas_1995.geojson;
tools/occupation_digitise.py from OCHA, the Palestinian Ministry of Planning, Peace Now and Wikidata) and the layer the
terrain draws from them (content/map/occupation_layers.yaml, tools/occupation_bake.py)."""
import base64
import io
import json
import re
from pathlib import Path

import numpy as np
import pytest
from PIL import Image

import borders_bake as bb
import occupation_bake as ob

ROOT = Path(__file__).resolve().parents[2]
MAP = ROOT / "content" / "map"
SOURCE = json.loads((MAP / "occupation_1967.geojson").read_text(encoding="utf-8"))
OSLO = json.loads((MAP / "oslo_areas_1995.geojson").read_text(encoding="utf-8"))
TEXT = (MAP / "occupation_layers.yaml").read_text(encoding="utf-8")


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
FEATS = SOURCE["features"]
TERR = {f["properties"]["id"]: bb.rings(f["geometry"]) for f in FEATS if f["properties"]["kind"] == "territory"}
POINTS = [f for f in FEATS if f["properties"]["kind"] == "settlement"]
# PCBS, "Number of Israeli Settlements in the West Bank by Year of Establishment" (2021; Israeli Settlements and Land Grab
# Database 2022), https://www.pcbs.gov.ps/Portals/_Rainbow/Documents/SETT5E-2021.html, opened 2026-10-22: 151 settlements.
PCBS_BY_YEAR = {1967: 1, 1968: 5, 1969: 5, 1970: 4, 1971: 4, 1972: 4, 1973: 3, 1974: 1, 1975: 6, 1976: 1, 1977: 16, 1978: 9,
                1979: 5, 1980: 10, 1981: 13, 1982: 12, 1983: 14, 1984: 12, 1985: 3, 1986: 3, 1987: 1, 1989: 4, 1990: 4, 1991: 1,
                1994: 1, 1996: 2, 1997: 2, 1998: 1, 1999: 1, 2013: 1, 2018: 1}
EVACUATED = {"Bedolah", "Bnei Atzmon", "Dugit", "Elei Sinai", "Gadid", "Gan Or", "Ganei Tal", "Kfar Darom", "Kfar Yam", "Katif",
             "Kerem Atzmona", "Morag", "Netzarim", "Netzer Hazani", "Neve Dekalim", "Nisanit", "Pe'at Sadeh", "Rafiah Yam",
             "Shirat HaYam", "Slav", "Tel Katifa", "Ganim", "Kadim", "Homesh", "Sa-Nur"}


def tex(name: str) -> np.ndarray:
    m = re.search(rf"^{name}: (\S+)$", TEXT, re.M)
    return np.array(Image.open(io.BytesIO(base64.b64decode(m[1]))))


def texel(lon: float, lat: float) -> tuple[int, int]:
    """(row, column) of the texel holding lon/lat; row 0 is the north edge."""
    g = ob.GRID
    return int((ob.lat1() - lat) / g["dlat"]), int((lon - g["lon0"]) / g["dlon"])


def scalar(key: str) -> str:
    return re.search(rf"^{key}: (\S+)", TEXT, re.M)[1]


def test_the_bake_is_up_to_date_and_deterministic():
    text = ob.build()
    assert same_bake(text, TEXT), "rerun uv run tools/occupation_bake.py"
    assert ob.build() == text


def test_each_layer_has_its_sourced_date_and_they_come_in_order():
    keys = [k for k, _, _ in ob.DATES]
    assert keys == ["occupied_from", "fence_from", "oslo_from", "ara_from", "wall_from", "evacuated_on", "footprints_from", "wall_mapped_from"]
    days = [scalar(k) for k in keys]
    assert days == sorted(days) and days[0] == "1967-06-05" and scalar("oslo_from") == "1995-09-28"
    for _, _, why in ob.DATES:
        assert re.search(r"https?://|chapter event e\d{4}_", why) or "Peace Now" in why or "OCHA" in why, why


def test_the_territories_are_the_west_bank_and_gaza_at_their_known_size():
    meta = SOURCE["storyoftheland"]
    assert meta["west_bank_km2"] == pytest.approx(5655, rel=0.01)   # 5,655 km2 with East Jerusalem
    assert meta["gaza_km2"] == pytest.approx(365, rel=0.01)
    assert bb.contains(TERR["west_bank"], 35.2620, 32.2211)        # Nablus
    assert bb.contains(TERR["west_bank"], 35.2353, 31.7767)        # the Old City of al-Quds (East Jerusalem)
    assert not bb.contains(TERR["west_bank"], 35.2000, 31.7800)    # West Jerusalem
    assert bb.contains(TERR["gaza"], 34.4667, 31.5000)             # Gaza City
    assert not bb.contains(TERR["gaza"], 34.6000, 31.5200)         # Sderot


def test_the_wall_keeps_ochas_lengths_by_status():
    km = {}
    for f in FEATS:
        if f["properties"]["kind"] == "barrier":
            for part in ob.lines(f["geometry"]):
                p = np.array(part)
                d = np.hypot(np.diff(p[:, 0]) * 94.6, np.diff(p[:, 1]) * 110.57).sum()
                km[f["properties"]["status"]] = km.get(f["properties"]["status"], 0.0) + d
    # OCHA's January 2018 file: 464 km constructed, 51 under construction, 196 planned (its Shape_Leng, in UTM metres)
    assert km["constructed"] == pytest.approx(464, rel=0.03)
    assert km["under_construction"] == pytest.approx(51, rel=0.05)
    assert km["planned"] == pytest.approx(196, rel=0.03)


def test_settlements_are_dated_inside_the_occupied_territory():
    for f in POINTS:
        p = f["properties"]
        lon, lat = f["geometry"]["coordinates"]
        assert 1967 <= p["from_year"] <= 2026, p
        assert p["basis"] in ("wikidata", "aerial_photos_2011"), p
        assert p["basis"] == "wikidata" or p["from_year"] == 2011, p
        inside = bb.contains(TERR["west_bank"], lon, lat) or bb.contains(TERR["gaza"], lon, lat)
        assert inside or p["until"], f"{p['name']} lies outside the West Bank and Gaza"
    gone = {f["properties"]["name"] for f in POINTS if f["properties"]["until"]}
    assert gone == EVACUATED, "the 21 Gaza settlements and the four in the northern West Bank, emptied in August 2005"
    assert {f["properties"]["until"] for f in POINTS if f["properties"]["until"]} == {"2005-08-22"}


def test_the_founding_years_follow_the_pcbs_count_of_settlements_by_year():
    """Wikidata's years (the points the 2011 photos date are left out) against the Palestinian Central Bureau of Statistics'
    count by year of establishment: within 10% at every five years to 2002."""
    dated = [f["properties"]["from_year"] for f in POINTS if f["properties"]["basis"] == "wikidata" and not f["properties"]["until"]]
    for y in range(1972, 2003, 5):
        ours = sum(1 for d in dated if d <= y)
        pcbs = sum(n for k, n in PCBS_BY_YEAR.items() if k <= y)
        assert ours == pytest.approx(pcbs, rel=0.10), (y, ours, pcbs)


def test_the_oslo_areas_are_a_and_b_with_their_shares():
    def km2(area):
        tot = 0.0
        for f in OSLO["features"]:
            if f["properties"]["area"] != area:
                continue
            polys = [f["geometry"]["coordinates"]] if f["geometry"]["type"] == "Polygon" else f["geometry"]["coordinates"]
            for poly in polys:
                for k, ring in enumerate(poly):
                    p = np.array(ring)
                    a = 0.5 * np.sum(p[:-1, 0] * p[1:, 1] - p[1:, 0] * p[:-1, 1]) * 94.6 * 110.57
                    tot += abs(a) if k == 0 else -abs(a)
        return tot
    wb = SOURCE["storyoftheland"]["west_bank_km2"]
    assert km2("A") / wb == pytest.approx(0.18, abs=0.02)   # Oslo II: Area A about 18%, B about 22% (e1995_09_oslo2)
    assert km2("B") / wb == pytest.approx(0.21, abs=0.02)


def test_the_textures_hold_the_layers_where_they_are():
    t1, t2, t3 = tex("tex1"), tex("tex2"), tex("tex3")
    for t in (t1, t2, t3):
        assert t.shape == (ob.GRID["ny"], ob.GRID["nx"], 4)
    assert t1[texel(35.2620, 32.2211)][0] == 255 and t1[texel(35.2620, 32.2211)][2] == 255   # Nablus: West Bank, Area A
    assert t1[texel(34.7818, 32.0853)][0] == 0                                              # Tel Aviv: neither
    assert t1[texel(34.4667, 31.5000)][1] == 255                                            # Gaza City
    by_name = {f["properties"]["name"]: f for f in POINTS}
    for name, code in (("Netzarim", 72 + 128), ("Kfar Darom", 70 + 128)):
        lon, lat = by_name[name]["geometry"]["coordinates"]
        assert t3[texel(lon, lat)][0] == code, name
        assert t2[texel(lon, lat)][2] * ob.DIST_UNIT_M < 150, name
    lon, lat = next(f for f in FEATS if f["properties"]["kind"] == "barrier" and f["properties"]["status"] == "constructed")["geometry"]["coordinates"][0]
    assert t2[texel(lon, lat)][0] * ob.DIST_UNIT_M < 110, "the wall's distance is about 0 on the wall"
    for lon, lat in ((34.5433, 31.5583), (34.4916, 31.4850)):   # the Erez and Nahal Oz crossings (OCHA's crossings layer)
        assert t3[texel(lon, lat)][3] * ob.DIST_UNIT_M < 400, "Gaza's fence runs past the crossings"
    assert t3[texel(34.30, 31.40)][3] == 255, "no fence on the coast"
    assert (t2[..., 3] > 200).sum() > 0 and (t2[..., 3][t1[..., 1] == 0] == 0).all(), "the access-restricted area is inside Gaza"


def test_distance_and_coverage_are_exact_on_simple_shapes():
    mx, my = ob.metres_per_degree()
    seg = np.array([[1000.0, 1000.0, 5000.0, 1000.0]])
    d, who = ob.distance(seg)
    j, i = ob.GRID["ny"] - 1 - int(1000.0 / (ob.GRID["dlat"] * my)), int(3000.0 / (ob.GRID["dlon"] * mx))
    cy = (ob.GRID["ny"] - j - 0.5) * ob.GRID["dlat"] * my
    assert d[j, i] == round(abs(cy - 1000.0) / ob.DIST_UNIT_M) and who[j, i] == 0
    assert d[0, 0] == 255 and who[0, 0] == -1
    square = {"type": "Polygon", "coordinates": [[[34.2, 31.2], [34.2 + 10 * ob.GRID["dlon"], 31.2], [34.2 + 10 * ob.GRID["dlon"], 31.2 + 10 * ob.GRID["dlat"]],
                                                  [34.2, 31.2 + 10 * ob.GRID["dlat"]], [34.2, 31.2]]]}
    c = ob.coverage([square])
    assert int(c.sum()) == 100 * 255 and c[-1, 0] == 255 and c[-11, 0] == 0


def test_a_founding_year_that_does_not_fit_the_code_fails_the_bake():
    assert ob.settlement_code({"name": "x", "from_year": 1977, "until": None}) == 77
    assert ob.settlement_code({"name": "x", "from_year": 1972, "until": "2005-08-22"}) == 200
    with pytest.raises(SystemExit):
        ob.settlement_code({"name": "x", "from_year": 2030, "until": None})
