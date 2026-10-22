# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///
"""Pin every `uses: owner/repo@ref` in .github/workflows/*.yml to the ref's full commit SHA, keeping the ref as a
trailing comment. Resolves with a read-only `gh api` GET. A tag can be moved by its owner; a SHA cannot (no_human
5f99b7af3: unpinned third-party downloads in the release build).

Usage: uv run scripts/oss/pin_actions.py"""
from __future__ import annotations

import re
import subprocess
import sys
from pathlib import Path
from typing import Callable

ROOT = Path(__file__).resolve().parents[2]
USES = re.compile(r"^(?P<lead>\s*-?\s*uses:\s*)(?P<action>[\w.-]+/[\w./-]+)@(?P<ref>[^\s#]+)(?P<rest>.*)$")


def gh_resolve(action: str, ref: str) -> str:
    repo = "/".join(action.split("/")[:2])
    out = subprocess.run(["gh", "api", f"repos/{repo}/commits/{ref}", "--jq", ".sha"], capture_output=True,
                         text=True, check=True).stdout.strip()
    if not re.fullmatch(r"[0-9a-f]{40}", out):
        raise SystemExit(f"could not resolve {action}@{ref}: {out!r}")
    return out


def pin_text(text: str, resolve: Callable[[str, str], str]) -> str:
    lines = []
    for line in text.splitlines(keepends=True):
        m = USES.match(line.rstrip("\n"))
        if m and not re.fullmatch(r"[0-9a-f]{40}", m["ref"]):
            sha = resolve(m["action"], m["ref"])
            line = f"{m['lead']}{m['action']}@{sha} # {m['ref']}\n"
        lines.append(line)
    return "".join(lines)


def main() -> int:
    for wf in sorted((ROOT / ".github/workflows").glob("*.yml")):
        before = wf.read_text(encoding="utf-8")
        after = pin_text(before, gh_resolve)
        if after != before:
            wf.write_text(after, encoding="utf-8")
            print(f"pinned {wf.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
