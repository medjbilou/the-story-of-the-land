# /// script
# requires-python = ">=3.11"
# dependencies = []
# ///
"""Per-pull-request changelog rule: if the PR changes a user-visible path, it must touch CHANGELOG.md or carry the
`no-changelog` label. Looks at the PR's own diff only, never at earlier history.

Usage: uv run scripts/oss/changelog_check.py --base <base-sha> [--head HEAD] [--skip-label true|false]"""
from __future__ import annotations

import argparse
import subprocess
import sys

USER_VISIBLE = ("content/", "sim/StoryOfTheLand.Sim/", "unity/Assets/", "unity/ProjectSettings/",
                "unity/Packages/")
NOT_USER_VISIBLE = ("unity/Assets/Tests/",)


def user_visible(paths: list[str]) -> list[str]:
    return [p for p in paths if p.startswith(USER_VISIBLE) and not p.startswith(NOT_USER_VISIBLE)]


def verdict(paths: list[str], skip_label: bool) -> tuple[bool, str]:
    visible = user_visible(paths)
    if not visible:
        return True, "No user-visible change; no CHANGELOG entry needed."
    if "CHANGELOG.md" in paths:
        return True, "CHANGELOG.md is updated."
    if skip_label:
        return True, "The no-changelog label is set."
    listed = "\n".join(f"  {p}" for p in visible)
    return False, ("These user-visible paths changed without a CHANGELOG.md entry:\n" + listed +
                   "\nAdd a line under ## [Unreleased] in CHANGELOG.md, or ask a maintainer for the no-changelog label.")


def changed_paths(base: str, head: str) -> list[str]:
    out = subprocess.run(["git", "diff", "--name-only", f"{base}...{head}"], capture_output=True, text=True, check=True)
    return [p for p in out.stdout.splitlines() if p]


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", required=True)
    ap.add_argument("--head", default="HEAD")
    ap.add_argument("--skip-label", default="false")
    a = ap.parse_args(argv)
    ok, msg = verdict(changed_paths(a.base, a.head), a.skip_label.lower() == "true")
    print(msg)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
