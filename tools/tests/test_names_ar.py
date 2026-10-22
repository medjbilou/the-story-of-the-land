"""Checks content/map/names_ar.yaml, the Arabic names of the places the map draws (audits/map_names_ar.md): one row per id
of each listed file, every non-empty name sourced and written in plain Arabic, each file's inline name_ar (and the
localities' today_name_ar) equal to it, and no Hebrew in the map's content (F5 step 6)."""
import re
from pathlib import Path

import pytest
import yaml12

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
DOC = yaml12.load((CONTENT / "map" / "names_ar.yaml").read_text(encoding="utf-8"))
FILES = {"map/settlements_1948.yaml": "settlements", "map/locations_1948.yaml": "locations", "map/localities_1948.yaml": "places"}
ROWS = [(f, r) for f in FILES for r in DOC[f]]
ARABIC = re.compile(r"[ء-غف-ي /\-]+")   # letters, space, slash, hyphen: no diacritics, marks or Latin


def source_rows(f):
    return yaml12.load((CONTENT / f).read_text(encoding="utf-8"))[FILES[f]]


@pytest.mark.parametrize("f", FILES)
def test_one_row_per_id_in_file_order(f):
    assert [r["id"] for r in DOC[f]] == [r["id"] for r in source_rows(f)]


@pytest.mark.parametrize("f,r", ROWS, ids=lambda x: x if isinstance(x, str) else x["id"])
def test_a_name_is_plain_arabic_and_sourced_or_empty_with_a_reason(f, r):
    for name, sources in ((r["name_ar"], r.get("name_ar_sources")), (r.get("today_name_ar", ""), r.get("today_name_ar_sources"))):
        if not name:
            continue
        assert ARABIC.fullmatch(name), name
        assert sources and all(s["url"].startswith("https://") and str(s["checked"]) == "2026-10-19" for s in sources)
    if r["name_ar"]:
        assert r["name_ar_review"] == "agent-checked"
    else:
        assert r["name_ar_note"] and "name_ar_sources" not in r, "an empty name says why"


@pytest.mark.parametrize("f", FILES)
def test_each_files_inline_name_ar_is_the_sidecars(f):
    """The map's loaders read name_ar from each file's own rows; names_ar.yaml keeps the sources. An empty name is no key."""
    rows = source_rows(f)
    assert {p["id"]: p.get("name_ar", "") for p in rows} == {r["id"]: r["name_ar"] for r in DOC[f]}
    assert all(p.get("name_ar", None) != "" for p in rows), "an empty name is left out, not written as ''"
    if f == "map/localities_1948.yaml":
        assert {p["id"]: p.get("today_name_ar", "") for p in rows} == {r["id"]: r.get("today_name_ar", "") for r in DOC[f]}
        assert all(("today" in p) == ("today_name_ar" in p) for p in rows), "an Arabic today name sits beside each today"


HEBREW = re.compile(r"[\u0590-\u05FF]")


def test_the_map_content_has_no_hebrew():
    """F5 step 6: the map's files carry no Hebrew, no *_he key and no Hebrew citation text."""
    files = [f for f in sorted((CONTENT / "map").iterdir()) if f.suffix in {".yaml", ".tsv", ".json", ".geojson"}]
    for f in files:
        text = f.read_text(encoding="utf-8")
        assert not HEBREW.search(text) and not re.search(r"\\u05[0-9a-fA-F]{2}", text), f.name   # also a JSON escape
        assert not re.search(r"(^|[\s{,\"])[a-z_]*_he(_[a-z]+)?[\"']?\s*:", text, re.M), f.name


def test_the_1948_names_the_brief_fixes():
    by = {(f, r["id"]): r["name_ar"] for f, r in ROWS}
    assert by["map/localities_1948.yaml", "al_majdal"] == "المجدل"
    assert by["map/settlements_1948.yaml", "petah_tikva"] == "ملبس"
    assert by["map/settlements_1948.yaml", "rishon_lezion"] == "عيون قارة"
    assert by["map/settlements_1948.yaml", "beersheba"] == "بئر السبع"


def test_a_renamed_name_or_a_tooltip_is_sourced():
    """2026-10-20 (audits/place_names_palestinian.md): a row whose Palestinian English name or tooltip line (tip, tip_ar,
    today_tip, today_tip_ar) is new cites a Palestinian source for it, read that day."""
    for f in FILES:
        own = {p["id"]: p for p in source_rows(f)}
        for r in DOC[f]:
            p = own[r["id"]]
            srcs = r.get("name_sources", []) + r.get("tip_sources", [])
            if any(p.get(k) for k in ("tip", "tip_ar", "today_tip", "today_tip_ar")) or r.get("name_sources"):
                assert srcs and all(s["url"].startswith("https://") and str(s["checked"]) == "2026-10-20" for s in srcs), r["id"]
                assert r["name"] == p["name"], "the sidecar copies the file's name"
            assert not p.get("tip_ar") or p.get("tip"), "an Arabic tip stands beside an English one"
            assert not (p.get("today_tip") or p.get("today_tip_ar")) or "today" in p, "today's own wording needs a today"
