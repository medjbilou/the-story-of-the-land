#!/usr/bin/env python3
# /// script
# requires-python = ">=3.10"
# ///
"""Show every place a record id is read (#346): `uv run scripts/id_references.py <id> [--root DIR]`.

Runs `git grep -n -w` for the id over the files git does not ignore in content/, sim/ and unity/Assets and prints one line per
hit (file:line: text), grouped as content, sim, tests and unity. Code reads some ids by name, which a YAML-only search
misses. Prints `no references` and exits 0 when there are none; a git error prints git's message and exits 1.
Read-only, offline."""
import argparse
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
AREAS = [("content", "content/"), ("sim", "sim/StoryOfTheLand.Sim/"), ("tests", "sim/StoryOfTheLand.Sim.Tests/"), ("unity", "unity/Assets/")]


def hits(root: Path, record_id: str) -> list[tuple[str, str]]:
    """(area, "file:line: text") per hit; a path belongs to the first area whose prefix matches."""
    r = subprocess.run(["git", "-C", str(root), "grep", "--untracked", "-n", "-w", "-F", "-e", record_id, "--", "content", "sim", "unity/Assets"],
                       capture_output=True, text=True)
    if r.returncode > 1:   # 1 means no match; anything above is a git error, never "no references"
        raise SystemExit(r.stderr.strip() or "git grep failed")
    out = r.stdout
    rows = []
    for line in out.splitlines():
        path = line.split(":", 1)[0]
        area = next((a for a, prefix in sorted(AREAS, key=lambda x: -len(x[1])) if path.startswith(prefix)), "other")
        rows.append((area, line))
    return rows


def render(rows: list[tuple[str, str]]) -> str:
    if not rows:
        return "no references"
    lines = []
    for area in [a for a, _ in AREAS] + ["other"]:
        mine = [text for a, text in rows if a == area]
        if mine:
            lines += [f"== {area} ({len(mine)})"] + mine
    return "\n".join(lines)


def main(argv: list[str] | None = None) -> int:
    p = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    p.add_argument("id")
    p.add_argument("--root", type=Path, default=ROOT)
    a = p.parse_args(argv)
    print(render(hits(a.root, a.id)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
