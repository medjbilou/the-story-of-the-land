#!/usr/bin/env python3
"""Convert the footage list into the game's YAML: palestine/footage.yaml, the event card's "Watch footage" clips.

Stdlib only. Run from anywhere:
    python3 game/tools/footage_to_yaml.py           # write game/content/palestine/footage.yaml
    python3 game/tools/footage_to_yaml.py --check   # exit 1 if the file is stale or a row is malformed

Input (read-only here): ../content/footage.tsv, one candidate clip a row (docs/footage-review.md). Every row is checked:
an event id that exists in the chapter files (../content/events_*.tsv), an https URL, the publisher and both titles, a
date as YYYY, YYYY-MM or YYYY-MM-DD, whole seconds or blank, a kind, and a sign-off that is whole or absent. A clip is
either a publisher's page (kind page: it opens in the browser) or a direct file from Archive Genocide (kind video or photo,
with its media_url, and its poster, entry id and original post where the archive has them: the desktop game streams it). Only a row the owner
has watched and signed off (checked_by, their GitHub handle, and checked_on, the day: CONTENT_POLICY.md section 2 rule 5)
is written out; the others stay candidates. The notes are for the reviewer and are not carried over. The file has one
list, `footage:`, one JSON line a clip (as figures_to_yaml.py writes them), so it reads back with json alone.
"""
from __future__ import annotations

import argparse
import json
import re
import sys

from figures_to_yaml import event_ids
from places_to_yaml import OUT_DIR, ROOT, Bad, read_tsv
from review_record import is_day, is_handle

TSV = ROOT / "content" / "footage.tsv"
OUT = OUT_DIR / "footage.yaml"
COLS = ["event_id", "url", "publisher", "title_en", "title_ar", "date", "duration_s", "description_en", "description_ar",
        "kind", "media_url", "poster_url", "ag_entry_id", "post_url", "checked_by", "checked_on", "notes"]
KINDS = ("page", "video", "photo")
FILE_COLS = ("media_url", "poster_url", "ag_entry_id", "post_url")
WHEN = re.compile(r"^(1[89]|20)\d\d(-(0[1-9]|1[0-2])(-(0[1-9]|[12]\d|3[01]))?)?$")
# TSV column -> YAML key (the UI's English fields carry no suffix, as in figures.yaml). The others keep their TSV name.
RENAME = {"title_en": "title", "description_en": "description"}
SHIPPED = ["event_id", "url", "publisher", "title_en", "title_ar", "date", "duration_s", "description_en", "description_ar",
           "kind", "media_url", "poster_url", "ag_entry_id", "post_url", "checked_by", "checked_on"]


def rows() -> list[dict]:
    """The TSV's clip rows (its header row left out)."""
    return [r for r in read_tsv(TSV, COLS) if r["event_id"] != "event_id"]


def signed(r: dict, where: str) -> bool:
    """True for a row the owner signed off; Bad for half a sign-off or a malformed one."""
    by, on = r["checked_by"].strip(), r["checked_on"].strip()
    if not by and not on:
        return False
    if not by or not on:
        raise Bad(f"{where}: checked_by and checked_on go together (both or neither)")
    if not is_handle(by):
        raise Bad(f"{where}: checked_by must be the reviewer's GitHub handle")
    if not is_day(on):
        raise Bad(f"{where}: checked_on must be the day of the sign-off, YYYY-MM-DD")
    return True


def to_clips(rs: list[dict], events: set[str]) -> list[dict]:
    """Every row validated; the signed-off ones as YAML entries, in the TSV's order."""
    out, seen = [], set()
    for n, r in enumerate(rs, 1):
        w = f"footage.tsv row {n} ({r['event_id']})"
        if r["event_id"] not in events:
            raise Bad(f"{w}: unknown event id")
        if (r["event_id"], r["url"], r["media_url"]) in seen:
            raise Bad(f"{w}: the same clip twice")
        seen.add((r["event_id"], r["url"], r["media_url"]))
        if not r["url"].startswith("https://") or " " in r["url"]:
            raise Bad(f"{w}: url must be one https:// address")
        for k in ("publisher", "title_en", "title_ar"):
            if not r[k].strip():
                raise Bad(f"{w}: {k} is empty")
        if not WHEN.match(r["date"]):
            raise Bad(f"{w}: date must be YYYY, YYYY-MM or YYYY-MM-DD")
        if r["duration_s"] and not r["duration_s"].isdigit():
            raise Bad(f"{w}: duration_s must be whole seconds, or blank")
        if r["kind"] not in KINDS:
            raise Bad(f"{w}: kind must be one of {', '.join(KINDS)}")
        if r["kind"] == "page" and any(r[k] for k in FILE_COLS):
            raise Bad(f"{w}: a page clip has no {', '.join(FILE_COLS)}")
        if r["kind"] != "page" and not r["media_url"]:
            raise Bad(f"{w}: a {r['kind']} clip needs its media_url")
        for k in ("media_url", "poster_url", "post_url"):
            if r[k] and (not r[k].startswith("https://") or " " in r[k]):
                raise Bad(f"{w}: {k} must be one https:// address, or blank")
        if r["ag_entry_id"] and not r["ag_entry_id"].isdigit():
            raise Bad(f"{w}: ag_entry_id must be the archive's numeric entry id, or blank")
        if not signed(r, w):
            continue
        e = {}
        for k in SHIPPED:
            if k == "duration_s":
                if r[k]:
                    e[k] = int(r[k])
            elif k in FILE_COLS:
                if r[k]:
                    e[k] = r[k]
            else:
                e[RENAME.get(k, k)] = r[k]
        out.append(e)
    return out


def emit(clips: list[dict]) -> str:
    head = ["# Generated by tools/footage_to_yaml.py from ../content/footage.tsv. Do not edit: fix the TSV and rerun.",
            "# The event card's \"Watch footage\" clips: only those the owner watched and signed off (checked_by, checked_on).",
            "# After the content note a page opens in the browser, and an Archive Genocide video or photo (media_url) plays in the",
            "# desktop game, streamed and never stored. No video is shipped. docs/footage-review.md."]
    if not clips:
        return "\n".join(head + ["footage: []"]) + "\n"
    return "\n".join(head + ["footage:"] + ["  - " + json.dumps(c, ensure_ascii=False) for c in clips]) + "\n"


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--check", action="store_true", help="fail on a stale file or a malformed row; write nothing")
    args = ap.parse_args()
    rs: list[dict] = []
    clips: list[dict] = []
    try:
        rs = rows()
        clips = to_clips(rs, event_ids())
        text = emit(clips)
        if args.check:
            errors = [] if OUT.exists() and OUT.read_text(encoding="utf-8") == text else \
                ["footage.yaml: stale or missing; run tools/footage_to_yaml.py"]
        else:
            OUT.write_text(text, encoding="utf-8")
            errors = []
    except Bad as e:
        errors = [str(e)]
    for e in errors:
        print("ERROR", e, file=sys.stderr)
    if not errors:
        print(("OK: " if args.check else "footage.yaml: ") + f"{len(clips)} signed-off clips of {len(rs)} listed")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
