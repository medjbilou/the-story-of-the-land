#!/usr/bin/env python3
"""Every script named in the dev-setup table exists (#359): `uv run scripts/oss/dev_setup_commands.py [--root DIR]`.

Reads docs/dev-setup.md, takes each backticked `scripts/...` or `tools/...` path ending in .py or .sh, and checks that
the file is there. One line per missing file (line number and path). Also lists each top-level scripts/*.py or scripts/*.sh that no backticked path names. Exit 1 if any."""
import argparse
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PATH = re.compile(r"(?<![\w/.-])((?:scripts|tools)/[\w./-]+\.(?:py|sh))(?![\w.-])")


def missing(root: Path) -> list[tuple[int, str]]:
    doc = root / "docs/dev-setup.md"
    bad = []
    for n, line in enumerate(doc.read_text(encoding="utf-8").splitlines(), 1):
        for span in re.findall(r"`([^`]+)`", line):
            for p in PATH.findall(span):
                if not (root / p).is_file():
                    bad.append((n, p))
    return bad


def named(root: Path) -> set[str]:
    """Every `scripts/...` or `tools/...` path inside a backticked span of docs/dev-setup.md, as missing() reads them."""
    text = (root / "docs/dev-setup.md").read_text(encoding="utf-8")
    return {p for span in re.findall(r"`([^`]+)`", text) for p in PATH.findall(span)}


def undocumented(root: Path) -> list[str]:
    """Top-level scripts/*.py and scripts/*.sh that no backticked path in docs/dev-setup.md names."""
    names = named(root)
    scripts = sorted(p for p in (root / "scripts").iterdir() if p.is_file() and p.suffix in (".py", ".sh"))
    return [f"scripts/{p.name}" for p in scripts if f"scripts/{p.name}" not in names]


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--root", type=Path, default=ROOT)
    args = ap.parse_args(argv)
    bad = missing(args.root)
    for n, p in bad:
        print(f"docs/dev-setup.md:{n}: {p} does not exist")
    loose = undocumented(args.root)
    for p in loose:
        print(f"{p} is not named in docs/dev-setup.md")
    return 1 if bad or loose else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
