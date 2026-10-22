"""tools/footage_to_yaml.py: only clips the owner signed off reach footage.yaml, and a malformed row stops the conversion."""
import json

import pytest

import footage_to_yaml as fy
from places_to_yaml import Bad

EVENTS = {"e1948_04_deir_yassin", "gz_2024_01_hind"}


def row(**kw) -> dict:
    r = {"event_id": "gz_2024_01_hind", "url": "https://www.aljazeera.com/video/x", "publisher": "Al Jazeera English",
         "title_en": "Hind Rajab", "title_ar": "هند رجب", "date": "2024-02-10", "duration_s": "120",
         "description_en": "A report.", "description_ar": "تقرير.", "kind": "page", "media_url": "", "poster_url": "",
         "ag_entry_id": "", "post_url": "", "checked_by": "", "checked_on": "", "notes": "n"}
    r.update(kw)
    return r


def test_only_signed_off_rows_ship_without_their_notes():
    clips = fy.to_clips([row(), row(url="https://www.aljazeera.com/video/y", checked_by="jdoe", checked_on="2026-10-22")], EVENTS)
    assert [c["url"] for c in clips] == ["https://www.aljazeera.com/video/y"]
    c = clips[0]
    assert (c["title"], c["title_ar"], c["description"], c["duration_s"]) == ("Hind Rajab", "هند رجب", "A report.", 120)
    assert (c["checked_by"], c["checked_on"]) == ("jdoe", "2026-10-22")
    assert "notes" not in c


def test_no_signed_off_row_gives_an_empty_list():
    assert fy.emit(fy.to_clips([row()], EVENTS)).endswith("footage: []\n")


def test_the_yaml_reads_back_with_json():
    text = fy.emit(fy.to_clips([row(checked_by="jdoe", checked_on="2026-10-22", duration_s="")], EVENTS))
    lines = [l for l in text.splitlines() if l.startswith("  - ")]
    back = json.loads(lines[0][4:])
    assert back["event_id"] == "gz_2024_01_hind" and "duration_s" not in back


@pytest.mark.parametrize("bad", [
    {"event_id": "e1999_nope"},
    {"url": "http://example.org/v"},
    {"title_ar": " "},
    {"date": "10/02/2024"},
    {"duration_s": "2:00"},
    {"checked_by": "jdoe"},                                         # half a sign-off
    {"checked_on": "2026-10-22"},
    {"checked_by": "arabic-reviewer (AI)", "checked_on": "2026-10-22"},   # not a person's handle
    {"checked_by": "jdoe", "checked_on": "22 Oct 2026"},
    {"kind": "film"},
    {"kind": "video"},                                              # a file clip without its file
    {"media_url": "https://media.archivegenocide.com/a.mp4"},       # a page with a file
    {"kind": "video", "media_url": "http://media.archivegenocide.com/a.mp4"},
    {"kind": "photo", "media_url": "https://media.archivegenocide.com/a.jpg", "ag_entry_id": "ev-1"},
])
def test_a_malformed_row_stops_the_conversion(bad):
    with pytest.raises(Bad):
        fy.to_clips([row(**bad)], EVENTS)


AG = dict(url="https://archivegenocide.com/nodes/#focus=ev-0c46bb56f2", publisher="Eye on Palestine (Telegram, Apr-Dec 2025)",
          kind="video", media_url="https://media.archivegenocide.com/x/a.mp4", poster_url="https://media.archivegenocide.com/x/a.jpg",
          ag_entry_id="113419", post_url="https://x.com/p/status/1", checked_by="jdoe", checked_on="2026-10-22")


def test_an_archive_file_ships_its_media_and_a_page_ships_none():
    clips = fy.to_clips([row(**AG), row(url="https://www.aljazeera.com/video/y", checked_by="jdoe", checked_on="2026-10-22")], EVENTS)
    assert {k: clips[0][k] for k in ("kind", "media_url", "poster_url", "ag_entry_id", "post_url")} == \
        {k: AG[k] for k in ("kind", "media_url", "poster_url", "ag_entry_id", "post_url")}
    assert clips[1]["kind"] == "page" and not any(k in clips[1] for k in fy.FILE_COLS)


def test_two_files_of_one_archive_event_are_two_clips():
    other = dict(AG, media_url="https://media.archivegenocide.com/x/b.mp4", ag_entry_id="113420")
    assert len(fy.to_clips([row(**AG), row(**other)], EVENTS)) == 2


def test_the_same_clip_twice_on_one_event_is_refused():
    with pytest.raises(Bad):
        fy.to_clips([row(), row()], EVENTS)


@pytest.mark.skipif(not fy.TSV.exists(), reason="the footage list (../content/footage.tsv) is outside this repository")
def test_the_repo_list_converts_and_is_current():
    rs = fy.rows()
    assert rs and all(not r["checked_by"] or r["checked_on"] for r in rs)
    clips = fy.to_clips(rs, fy.event_ids())
    assert fy.OUT.read_text(encoding="utf-8") == fy.emit(clips), "run tools/footage_to_yaml.py"
