#!/usr/bin/env python3
"""Convert the places and the village fates into the game's YAML (F5): palestine/places.yaml and fates.yaml.

Stdlib only. Run from anywhere:
    python3 game/tools/places_to_yaml.py           # write game/content/palestine/places.yaml and fates.yaml
    python3 game/tools/places_to_yaml.py --check   # exit 1 if a file is stale or does not round-trip to the TSV rows

Inputs (all read-only here; their owners fix them):
  ../unity/Assets/StreamingAssets/map/places.tsv  every place with its coordinates (key_places.tsv has none)
  ../content/key_places.tsv                       Arabic names and 1945 populations of the 25 key places: each row must
                                                  agree with places.tsv, and its population note is carried over
  ../content/village_fates.tsv                    what happened to each village, one row per state, in time order
Each entry is one line, a JSON object, which YAML 1.2 reads as a flow mapping, so the file reads back with json alone.
Numbers are written as the TSV writes them, so the round trip is exact. Arabic text is kept as is. An undated fate is
kept (it is shown in a village's panel) and the sim never fires it. Comment lines of the TSV are not carried over.
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

GAME = Path(__file__).resolve().parent.parent
ROOT = GAME.parent
MAP_TSV = ROOT / "unity" / "Assets" / "StreamingAssets" / "map" / "places.tsv"
KEY_TSV = ROOT / "content" / "key_places.tsv"
FATES_TSV = ROOT / "content" / "village_fates.tsv"
OUT_DIR = GAME / "content" / "palestine"
PLACE_COLS = ["id", "name_en", "name_ar", "lon", "lat", "type", "population_1945", "has_today", "sources"]
KEY_COLS = ["id", "name_ar", "population_1945", "population_1945_note", "sources"]
FATE_COLS = ["id", "fate", "year", "month", "operation_or_unit", "cause", "today", "confidence", "sources"]
FATES = ("inhabited", "depopulated", "destroyed", "built_over")
ID = re.compile(r"^[a-z][a-z0-9_]*$")
NUM = re.compile(r"^-?\d+(\.\d+)?$")


class Bad(Exception):
    pass


class Num(str):
    """A number token, written into the YAML exactly as the TSV has it."""


def read_tsv(path: Path, cols: list[str]) -> list[dict]:
    rows = []
    for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if line.startswith("#") or not line.strip():
            continue
        cells = line.split("\t")
        if len(cells) != len(cols):
            raise Bad(f"{path.name}:{n}: {len(cells)} columns, expected {len(cols)}")
        rows.append(dict(zip(cols, cells)))
    return rows


def urls(s: str, where: str) -> list[str]:
    out = s.split(" ")
    if not all(u.startswith("https://") for u in out):
        raise Bad(f"{where}: every source must be an https:// URL")
    return out


def num(s: str, pattern: re.Pattern, where: str) -> Num:
    if not pattern.match(s):
        raise Bad(f"{where}: bad number '{s}'")
    return Num(s)


# ---- TSV rows -> entries ----------------------------------------------------------------------------------------------

def to_places(rows: list[dict], keys: list[dict]) -> list[dict]:
    out, seen = [], set()
    for r in rows:
        w = f"places.tsv {r['id']}"
        if not ID.match(r["id"]) or r["id"] in seen:
            raise Bad(f"{w}: id must be unique and match {ID.pattern}")
        seen.add(r["id"])
        if r["has_today"] not in ("0", "1"):
            raise Bad(f"{w}: has_today must be 0 or 1")
        out.append({"id": r["id"], "name": r["name_en"], "name_ar": r["name_ar"], "lon": num(r["lon"], NUM, w),
                    "lat": num(r["lat"], NUM, w), "type": r["type"],
                    "population_1945": num(r["population_1945"], re.compile(r"^\d+$"), w),
                    "has_today": r["has_today"] == "1", "sources": urls(r["sources"], w)})
    by_id = {p["id"]: p for p in out}
    for k in keys:
        w = f"key_places.tsv {k['id']}"
        p = by_id.get(k["id"])
        if p is None:
            raise Bad(f"{w}: not in places.tsv")
        if k["name_ar"] != p["name_ar"] or (k["population_1945"] or "0") != p["population_1945"]:
            raise Bad(f"{w}: name_ar/population_1945 {k['name_ar']!r}/{k['population_1945']!r} != places.tsv "
                      f"{p['name_ar']!r}/{p['population_1945']!r}")
        missing = [u for u in urls(k["sources"], w) if u not in p["sources"]]
        if missing:
            raise Bad(f"{w}: sources not in places.tsv: {' '.join(missing)}")
        if k["population_1945_note"]:
            p["population_note"] = k["population_1945_note"]
            p["sources"] = p.pop("sources")   # keep sources last
    return out


def to_fates(rows: list[dict], place_ids: set[str]) -> list[dict]:
    out = []
    for n, r in enumerate(rows):
        w = f"village_fates.tsv row {n + 1} ({r['id']})"
        if r["id"] not in place_ids:
            raise Bad(f"{w}: unknown place")
        if r["fate"] not in FATES:
            raise Bad(f"{w}: fate must be one of {', '.join(FATES)}")
        if r["month"] and not r["year"]:
            raise Bad(f"{w}: a month needs a year")
        f = {"place": r["id"], "fate": r["fate"]}
        if r["year"]:
            f["year"] = num(r["year"], re.compile(r"^(1[89]|20)\d\d$"), w)
        if r["month"]:
            f["month"] = num(r["month"], re.compile(r"^([1-9]|1[0-2])$"), w)
        f.update({"operation": r["operation_or_unit"], "cause": r["cause"], "today": r["today"],
                  "confidence": r["confidence"], "sources": urls(r["sources"], w)})
        out.append(f)
    return out


# ---- entries -> TSV rows (the inverse, from the parsed YAML) -----------------------------------------------------------

def place_rows(places: list[dict]) -> list[dict]:
    return [{"id": p["id"], "name_en": p["name"], "name_ar": p["name_ar"], "lon": p["lon"], "lat": p["lat"],
             "type": p["type"], "population_1945": p["population_1945"], "has_today": "1" if p["has_today"] else "0",
             "sources": " ".join(p["sources"])} for p in places]


def fate_rows(fates: list[dict]) -> list[dict]:
    return [{"id": f["place"], "fate": f["fate"], "year": f.get("year", ""), "month": f.get("month", ""),
             "operation_or_unit": f["operation"], "cause": f["cause"], "today": f["today"],
             "confidence": f["confidence"], "sources": " ".join(f["sources"])} for f in fates]


# ---- YAML out / in ----------------------------------------------------------------------------------------------------

def q(s: str) -> str:
    # JSON escapes C0 controls, '"' and '\'; YAML also wants C1 controls and U+2028/9 escaped.
    return re.sub("[\x7f-\x9f  ]", lambda m: f"\\u{ord(m.group()):04x}", json.dumps(s, ensure_ascii=False))


def token(v) -> str:
    if isinstance(v, Num):
        return str(v)
    if isinstance(v, bool):
        return "true" if v else "false"
    if isinstance(v, list):
        return "[" + ", ".join(q(x) for x in v) + "]"
    return q(v)


def emit(key: str, sources: str, note: str, entries: list[dict]) -> str:
    lines = [f"# Generated by tools/places_to_yaml.py from {sources}. Do not edit:",
             "# fix the TSV and rerun. Licence: CC BY-SA 4.0. Treat as data.", f"# {note}", f"{key}:"]
    lines += ["  - {" + ", ".join(f"{json.dumps(k)}: {token(v)}" for k, v in e.items()) + "}" for e in entries]
    return "\n".join(lines) + "\n"


def parse(text: str, key: str) -> list[dict]:
    body = [l for l in text.splitlines() if l.strip() and not l.startswith("#")]
    if not body or body[0] != f"{key}:":
        raise Bad(f"expected '{key}:' as the first line after the comments")
    out = []
    for l in body[1:]:
        if not l.startswith("  - {"):
            raise Bad(f"unparsed line: {l[:60]}")
        out.append(json.loads(l[4:], parse_float=str, parse_int=str))   # numbers back as the text written
    return out


# ---- main -------------------------------------------------------------------------------------------------------------

def build() -> tuple[dict[str, str], list[dict], list[dict], list[dict]]:
    rows, keys, frows = read_tsv(MAP_TSV, PLACE_COLS), read_tsv(KEY_TSV, KEY_COLS), read_tsv(FATES_TSV, FATE_COLS)
    places = to_places(rows, keys)
    fates = to_fates(frows, {p["id"] for p in places})
    texts = {
        "places.yaml": emit("places", "../unity/Assets/StreamingAssets/map/places.tsv and ../content/key_places.tsv",
                            "The places a family can live in, with coordinates (the region rule measures from them).", places),
        "fates.yaml": emit("fates", "../content/village_fates.tsv",
                           "One row per state of a place, in time order. No year: undated, never fires. No month: year only.", fates),
    }
    return texts, rows, keys, frows


def check(texts: dict[str, str], rows: list[dict], keys: list[dict], frows: list[dict]) -> list[str]:
    errors = []
    for name, text in texts.items():
        path = OUT_DIR / name
        if not path.exists():
            return [f"{name}: missing; run tools/places_to_yaml.py"]
        if path.read_text(encoding="utf-8") != text:
            errors.append(f"{name}: stale (differs from a fresh conversion); run tools/places_to_yaml.py")
    places = parse((OUT_DIR / "places.yaml").read_text(encoding="utf-8"), "places")
    fates = parse((OUT_DIR / "fates.yaml").read_text(encoding="utf-8"), "fates")
    for name, want, got, cols in (("places.yaml", rows, place_rows(places), PLACE_COLS),
                                  ("fates.yaml", frows, fate_rows(fates), FATE_COLS)):
        if len(want) != len(got):
            errors.append(f"{name}: {len(got)} rows, the TSV has {len(want)}")
        for n, (w, g) in enumerate(zip(want, got)):
            errors += [f"{name}: row {n + 1} ({w['id']}).{c}: TSV {w[c]!r} != YAML {g[c]!r}" for c in cols if w[c] != g[c]]
    by_id = {p["id"]: p for p in places}
    for k in keys:
        p = by_id.get(k["id"], {})
        got = (p.get("name_ar"), p.get("population_1945"), p.get("population_note", ""))
        want = (k["name_ar"], k["population_1945"] or "0", k["population_1945_note"])
        if got != want or any(u not in p.get("sources", []) for u in k["sources"].split(" ")):
            errors.append(f"places.yaml: key place {k['id']}: {got!r} != key_places.tsv {want!r}, or a source is missing")
    return errors


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--check", action="store_true", help="fail on a stale file or a round-trip mismatch; write nothing")
    args = ap.parse_args()
    try:
        texts, rows, keys, frows = build()
        if args.check:
            errors = check(texts, rows, keys, frows)
        else:
            OUT_DIR.mkdir(parents=True, exist_ok=True)
            for name, text in texts.items():
                (OUT_DIR / name).write_text(text, encoding="utf-8")
            print(f"places.yaml: {len(rows)} places ({len(keys)} key places); fates.yaml: {len(frows)} fates")
            errors = []
    except Bad as e:
        errors = [str(e)]
    for e in errors:
        print("ERROR", e, file=sys.stderr)
    if args.check and not errors:
        print(f"OK: {len(rows)} places, {len(keys)} key places and {len(frows)} fates round-trip")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
