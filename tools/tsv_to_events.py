#!/usr/bin/env python3
"""Convert our audited chapters (../content/events_*.tsv + choices_*.tsv) into the game's event YAML (F4).

Stdlib only. Run from anywhere:
    python3 game/tools/tsv_to_events.py           # write game/content/palestine/events/<chapter>.yaml
    python3 game/tools/tsv_to_events.py --check   # exit 1 if a file is stale or does not round-trip to the TSV rows

One file per chapter, a top-level `events:` list; each entry is one event of content/schema/event.schema.json with the
F4 extensions (docs/palestine-model.md): title_ar, text_ar, scope, place, event-level effects, options' text_ar, outcome,
outcome_ar, and the tag `sensitive`. Effects `Land:+5` become `{ target: land, delta: 0.05 }` (points / 100, since the
sim's indicators run 0..1). An event with no choice gets one option, `continue`, with no effects of its own. A fate
template (scope fate:*) has no date in the TSV; it gets the chapter's whole window and fires on the family's village fate.
Sources get `checked:` = the TSV's "Compiled" date, the day the URLs were opened.
`day:` is the day of the month the English text first dates in the event's own month ("On 9 April 1948"), read from the
text so the two cannot disagree; the sim orders a month's cards by it (an event with none, a month-wide one, first).
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

GAME = Path(__file__).resolve().parent.parent
TSV_DIR = GAME.parent / "content"
OUT_DIR = GAME / "content" / "palestine" / "events"
CHAPTERS = ["1880_1946", "1947_1949", "1950_1966", "1967_1987", "1987_2000", "2000_2023", "2023_2026"]
RESOURCES = ["Land", "Home", "Family", "Documents", "Livelihood", "Memory", "Sumud", "Community", "Safety", "Solidarity"]
EVENT_COLS = ["id", "year", "month", "scope", "place_id", "title_ar", "title_en", "text_ar", "text_en", "effects", "sources", "sensitive"]
CHOICE_COLS = ["event_id", "choice_id", "label_ar", "label_en", "effects", "outcome_ar", "outcome_en"]
CONTINUE = ("continue", "Continue", "متابعة")   # the option of an event with no choice
SCOPE = re.compile(r"^(national|region:[a-z0-9_]+|fate:(depopulated|destroyed|built_over))$")
ID = re.compile(r"^[a-z][a-z0-9_]*$")


class Bad(Exception):
    pass


# ---- TSV --------------------------------------------------------------------------------------------------------------

def read_tsv(path: Path, cols: list[str]) -> tuple[str | None, list[dict]]:
    compiled, rows = None, []
    for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        if line.startswith("#"):
            m = re.search(r"Compiled (\d{4}-\d{2}-\d{2})", line)
            compiled = compiled or (m and m.group(1))
            continue
        if not line.strip():
            continue
        cells = line.split("\t")
        if len(cells) != len(cols):
            raise Bad(f"{path.name}:{n}: {len(cells)} columns, expected {len(cols)}")
        rows.append(dict(zip(cols, cells)))
    return compiled, rows


def parse_effects(s: str, where: str) -> list[tuple[str, float]]:
    out = []
    for part in filter(None, s.split(";")):
        m = re.fullmatch(r"([A-Za-z]+):([+-]\d+)", part)
        if not m or m.group(1) not in RESOURCES:
            raise Bad(f"{where}: bad effect '{part}' (Resource:+n over {', '.join(RESOURCES)})")
        out.append((m.group(1).lower(), int(m.group(2)) / 100))
    return out


def format_effects(fx: list[dict]) -> str:
    out = []
    for e in fx:
        points = round(e["delta"] * 100)
        if abs(e["delta"] * 100 - points) > 1e-9:
            raise Bad(f"delta {e['delta']} is not a whole number of points")
        out.append(f"{e['target'].capitalize()}:{points:+d}")
    return ";".join(out)


MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November",
          "December"]
DAY = re.compile(r"\b(\d{1,2})(?:[–-]\d{1,2})?(?: and \d{1,2})? (" + "|".join(MONTHS) + r")\b")


def day_of(text: str, month: int) -> int | None:
    """The first day of `month` the text names ("9 April", "9–10 April"), or None."""
    for d, name in DAY.findall(text):
        if MONTHS.index(name) + 1 == month:
            return int(d)
    return None


def month_after(year: int, month: int) -> str:
    return f"{year + month // 12:04d}-{month % 12 + 1:02d}-01"


# ---- TSV -> event dicts -> TSV ------------------------------------------------------------------------------------------

def to_events(events: list[dict], choices: list[dict], checked: str, chapter: str) -> list[dict]:
    dated = [(int(e["year"]), int(e["month"])) for e in events if e["year"]]
    if not dated:
        raise Bad(f"{chapter}: no dated event, so no chapter window")
    window = (f"{min(dated)[0]:04d}-{min(dated)[1]:02d}-01", month_after(*max(dated)))
    by_event: dict[str, list[dict]] = {}
    for c in choices:
        by_event.setdefault(c["event_id"], []).append(c)
    ids = [e["id"] for e in events]
    for eid in by_event:
        if eid not in ids:
            raise Bad(f"choices_{chapter}: choice for unknown event '{eid}'")
    out = []
    for e in events:
        w = f"events_{chapter} {e['id']}"
        if not ID.match(e["id"]) or ids.count(e["id"]) > 1:
            raise Bad(f"{w}: id must be unique and match {ID.pattern}")
        if not SCOPE.match(e["scope"]):
            raise Bad(f"{w}: bad scope '{e['scope']}'")
        if e["place_id"] and not ID.match(e["place_id"]):
            raise Bad(f"{w}: bad place_id '{e['place_id']}'")
        if e["sensitive"] not in ("0", "1"):
            raise Bad(f"{w}: sensitive must be 0 or 1")
        fate = e["scope"].startswith("fate:")
        if fate != (e["year"] == "" and e["month"] == ""):
            raise Bad(f"{w}: a fate template has no date and every other event has one")
        if fate:
            start, end = window
        else:
            y, m = int(e["year"]), int(e["month"])
            start, end = f"{y:04d}-{m:02d}-01", month_after(y, m)
        options = []
        for c in by_event.get(e["id"], []):
            if not ID.match(c["choice_id"]) or c["choice_id"] == CONTINUE[0]:
                raise Bad(f"choices_{chapter} {e['id']}.{c['choice_id']}: bad or reserved choice id")
            options.append({"id": c["choice_id"], "text": c["label_en"], "text_ar": c["label_ar"],
                            "outcome": c["outcome_en"], "outcome_ar": c["outcome_ar"],
                            "effects": parse_effects(c["effects"], f"choices_{chapter} {e['id']}.{c['choice_id']}")})
        if not options:
            options.append({"id": CONTINUE[0], "text": CONTINUE[1], "text_ar": CONTINUE[2], "effects": []})
        urls = e["sources"].split(" ")
        if not all(u.startswith("https://") for u in urls):
            raise Bad(f"{w}: every source must be an https:// URL")
        out.append({"id": e["id"], "title": e["title_en"], "title_ar": e["title_ar"], "text": e["text_en"],
                    "text_ar": e["text_ar"], "scope": e["scope"], "place": e["place_id"],
                    "tags": ["sensitive"] if e["sensitive"] == "1" else [], "from": start, "to": end,
                    "day": None if fate else day_of(e["text_en"], int(e["month"])),
                    "effects": parse_effects(e["effects"], w), "options": options,
                    "sources": [{"url": u, "checked": checked} for u in urls]})
    return out


def to_rows(events: list[dict]) -> tuple[list[dict], list[dict]]:
    """The inverse of to_events, from parsed YAML: the TSV rows the file says it holds."""
    erows, crows = [], []
    for e in events:
        fate = e["scope"].startswith("fate:")
        erows.append({"id": e["id"], "year": "" if fate else str(int(e["from"][:4])),
                      "month": "" if fate else str(int(e["from"][5:7])), "scope": e["scope"],
                      "place_id": e.get("place", ""), "title_ar": e["title_ar"], "title_en": e["title"],
                      "text_ar": e["text_ar"], "text_en": e["text"], "effects": format_effects(e.get("effects", [])),
                      "sources": " ".join(s["url"] for s in e["sources"]),
                      "sensitive": "1" if "sensitive" in e.get("tags", []) else "0"})
        for o in e["options"]:
            if o["id"] == CONTINUE[0] and "outcome" not in o:
                continue
            crows.append({"event_id": e["id"], "choice_id": o["id"], "label_ar": o["text_ar"], "label_en": o["text"],
                          "effects": format_effects(o.get("effects", [])), "outcome_ar": o["outcome_ar"],
                          "outcome_en": o["outcome"]})
    return erows, crows


# ---- YAML out (block style; free text as double-quoted JSON strings, which YAML 1.2 reads the same) --------------------

def q(s: str) -> str:
    # JSON escapes C0 controls, '"' and '\'; YAML also wants C1 controls and U+2028/9 escaped.
    return re.sub(r"[\x7f-\x9f  ]", lambda m: f"\\u{ord(m.group()):04x}", json.dumps(s, ensure_ascii=False))


def emit(chapter: str, events: list[dict]) -> str:
    L = [f"# Generated by tools/tsv_to_events.py from ../content/events_{chapter}.tsv and choices_{chapter}.tsv. Do not edit:",
         "# fix the TSV and rerun. Licence: CC BY-SA 4.0. Effects are DESIGN VALUES (TSV points / 100), not facts.",
         "# Each entry follows content/schema/event.schema.json with the F4 extensions (docs/palestine-model.md).",
         "events:"]

    def fx(effects, pad):
        if effects:
            L.append(f"{pad}effects:")
            L.extend(f"{pad}  - {{ target: {t}, delta: {d!r} }}" for t, d in effects)

    for e in events:
        L.append(f"  - id: {e['id']}")
        for k in ("title", "title_ar", "text", "text_ar", "scope"):
            L.append(f"    {k}: {q(e[k])}")
        if e["place"]:
            L.append(f"    place: {e['place']}")
        if e["tags"]:
            L.append(f"    tags: [{', '.join(e['tags'])}]")
        L.append(f"    from: {e['from']}")
        L.append(f"    to: {e['to']}")
        if e.get("day"):
            L.append(f"    day: {e['day']}")
        fx(e["effects"], "    ")
        L.append("    options:")
        for o in e["options"]:
            L.append(f"      - id: {o['id']}")
            for k in ("text", "text_ar", "outcome", "outcome_ar"):
                if k in o:
                    L.append(f"        {k}: {q(o[k])}")
            fx(o["effects"], "        ")
        L.append("    sources:")
        L.extend(f"      - {{ url: {q(s['url'])}, checked: {s['checked']} }}" for s in e["sources"])
    return "\n".join(L) + "\n"


# ---- YAML in: exactly the subset emit() writes (block maps and lists, flow maps and lists, JSON strings, plain scalars) -

def scalar(v: str):
    if v.startswith('"'):
        return json.loads(v)
    if re.fullmatch(r"-?\d+(\.\d+)?([eE][-+]?\d+)?", v):
        return float(v)
    return v


def flow(v: str):
    if v.startswith("["):
        return [scalar(x.strip()) for x in v[1:-1].split(",") if x.strip()]
    out, rest, dec = {}, v[1:-1].strip(), json.JSONDecoder()
    while rest:
        key, rest = rest.split(":", 1)
        rest = rest.lstrip()
        if rest.startswith('"'):
            val, end = dec.raw_decode(rest)
        else:
            end = rest.find(",") if "," in rest else len(rest)
            val = scalar(rest[:end].strip())
        out[key.strip()] = val
        rest = rest[end:].lstrip().lstrip(",").strip()
    return out


def parse_yaml(text: str):
    lines = [(len(l) - len(l.lstrip(" ")), l.strip()) for l in text.splitlines() if l.strip() and not l.lstrip().startswith("#")]

    def value(v):
        return flow(v) if v[:1] in "{[" else scalar(v)

    def block(i, ind):
        if lines[i][1].startswith("- "):
            seq = []
            while i < len(lines) and lines[i][0] == ind and lines[i][1].startswith("- "):
                body = lines[i][1][2:]
                if not re.match(r"[a-z_]+:( |$)", body):
                    seq.append(value(body)); i += 1
                else:   # a mapping that starts on the dash line: re-read it two columns deeper
                    lines[i] = (ind + 2, body)
                    item, i = block(i, ind + 2)
                    seq.append(item)
            return seq, i
        m = {}
        while i < len(lines) and lines[i][0] == ind and not lines[i][1].startswith("- "):
            key, _, v = lines[i][1].partition(":")
            v = v.strip()
            if key in m:
                raise Bad(f"duplicate key '{key}'")
            if v:
                m[key] = value(v); i += 1
            else:
                m[key], i = block(i + 1, lines[i + 1][0])
        return m, i

    doc, i = block(0, lines[0][0])
    if i != len(lines):
        raise Bad(f"unparsed line: {lines[i][1][:60]}")
    for e in doc["events"]:   # effects come back as dicts
        for holder in [e] + e["options"]:
            holder["effects"] = [{"target": x["target"], "delta": x["delta"]} for x in holder.get("effects", [])]
    return doc["events"]


# ---- main -------------------------------------------------------------------------------------------------------------

def convert(chapter: str) -> tuple[str, list[dict], list[dict]]:
    checked, events = read_tsv(TSV_DIR / f"events_{chapter}.tsv", EVENT_COLS)
    _, choices = read_tsv(TSV_DIR / f"choices_{chapter}.tsv", CHOICE_COLS)
    if not checked:
        raise Bad(f"events_{chapter}.tsv: no 'Compiled YYYY-MM-DD' in its header, so no checked date for the sources")
    return emit(chapter, to_events(events, choices, checked, chapter)), events, choices


def check(chapter: str, text: str, events: list[dict], choices: list[dict]) -> list[str]:
    path = OUT_DIR / f"{chapter}.yaml"
    if not path.exists():
        return [f"{path.name}: missing; run tools/tsv_to_events.py"]
    on_disk = path.read_text(encoding="utf-8")
    errors = [] if on_disk == text else [f"{path.name}: stale (differs from a fresh conversion); run tools/tsv_to_events.py"]
    erows, crows = to_rows(parse_yaml(on_disk))
    for kind, want, got, key in (("event", events, erows, "id"), ("choice", choices, crows, "choice_id")):
        if len(want) != len(got):
            errors.append(f"{path.name}: {len(got)} {kind} rows, the TSV has {len(want)}")
        if kind == "choice":   # the YAML groups choices under their event; compare in that order
            order = [e["id"] for e in events]
            want = sorted(want, key=lambda c: order.index(c["event_id"]))
        for w, g in zip(want, got):
            errors += [f"{path.name}: {kind} {w[key]}.{c}: TSV {w[c]!r} != YAML {g[c]!r}" for c in w if w[c] != g[c]]
    return errors


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--check", action="store_true", help="fail on a stale file or a round-trip mismatch; write nothing")
    args = ap.parse_args()
    errors, total = [], 0
    for chapter in CHAPTERS:
        try:
            text, events, choices = convert(chapter)
            total += len(events)
            if args.check:
                errors += check(chapter, text, events, choices)
            else:
                OUT_DIR.mkdir(parents=True, exist_ok=True)
                (OUT_DIR / f"{chapter}.yaml").write_text(text, encoding="utf-8")
                print(f"{chapter}: {len(events)} events, {len(choices)} choices")
        except Bad as e:
            errors.append(str(e))
    for e in errors:
        print("ERROR", e, file=sys.stderr)
    if args.check and not errors:
        print(f"OK: {len(CHAPTERS)} chapters, {total} events round-trip")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
