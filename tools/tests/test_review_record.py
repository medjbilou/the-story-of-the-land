"""tools/review_record.py: the shape of a person's sign-off (#375), the Python side of StoryOfTheLand.Sim.ReviewRecord."""
import datetime

import pytest

import review_record as rr


@pytest.mark.parametrize("handle, ok", [
    ("jdoe", True), ("a", True), ("jdoe-2", True), ("JDoe3", True), ("a" * 39, True),
    ("", False), ("a" * 40, False), ("jdoe@example.org", False), ("@jdoe", False), ("jdoe-", False), ("-jdoe", False),
    ("j--doe", False), ("j_doe", False), ("j.doe", False), ("J Doe", False), ("dependabot[bot]", False),
    (None, False), (123, False),
])
def test_the_handle_shape_is_githubs(handle, ok):
    assert rr.is_handle(handle) is ok


@pytest.mark.parametrize("row, field", [
    ({}, None),
    ({"reviewed_by": "jdoe", "reviewed_on": "2026-10-15"}, None),
    ({"reviewed_by": "jdoe", "reviewed_on": datetime.date(2026, 10, 15)}, None),
    ({"reviewed_by": "jdoe"}, "reviewed_on"),
    ({"reviewed_on": "2026-10-15"}, "reviewed_by"),
    ({"reviewed_by": None, "reviewed_on": "2026-10-15"}, "reviewed_by"),
    ({"reviewed_by": "jdoe", "reviewed_on": None}, "reviewed_on"),
    ({"reviewed_by": "jdoe", "reviewed_on": "2026-13-01"}, "reviewed_on"),
    ({"reviewed_by": "jdoe", "reviewed_on": "2026-10-15T10:00"}, "reviewed_on"),
])
def test_problem_names_the_field(row, field):
    got = rr.problem(row)
    assert (got[0] if got else None) == field


# ---- the shared cases: every reader agrees on them (tools/tests/data/review_record_cases.json) ---------------------------

import json  # noqa: E402
from pathlib import Path  # noqa: E402

import review_queue  # noqa: E402
import yaml12  # noqa: E402

CASES = json.loads((Path(__file__).parent / "data" / "review_record_cases.json").read_text(encoding="utf-8"))["cases"]


def row_text(case, indent="    "):
    lines = [f"{indent}status: fact-checked"]
    for key in ("by", "on"):
        if case[key] is not None:
            lines.append(f"{indent}reviewed_{key}: {case[key]}".rstrip())
    return "\n".join(lines) + "\n"


@pytest.mark.parametrize("case", CASES, ids=lambda c: f"{c['by']}|{c['on']}")
def test_the_rule_agrees_with_the_shared_cases(case):
    row = yaml12.load("rows:\n  - id: a\n" + row_text(case))["rows"][0]
    got = rr.problem(row)
    assert (got[0] if got else None) == case["field"]


@pytest.mark.parametrize("case", CASES, ids=lambda c: f"{c['by']}|{c['on']}")
def test_the_queue_agrees_with_the_shared_cases(case):
    """The queue reads raw YAML scalars, not the loader's types: it must still take a row out exactly when the rule says the
    sign-off is whole."""
    entries = review_queue.scan("rows:\n  - id: a\n" + row_text(case), "content/x.yaml")
    whole = case["field"] is None and case["by"] is not None
    assert [e.kind for e in entries] == ["signed" if whole else "status"]
