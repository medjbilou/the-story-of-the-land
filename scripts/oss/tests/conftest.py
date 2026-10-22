import subprocess
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))


def git(root: Path, *args: str) -> str:
    return subprocess.run(["git", "-C", str(root), *args], capture_output=True, text=True, check=True).stdout


def commit_all(root: Path, msg: str) -> str:
    git(root, "add", "-A")
    git(root, "-c", "user.name=t", "-c", "user.email=t@users.noreply.github.com", "commit", "-qm", msg)
    return git(root, "rev-parse", "HEAD").strip()[:10]


@pytest.fixture
def make_repo(tmp_path):
    """A throwaway git repo with the given files tracked. Values are bytes or str; a str starting with '->' is a
    symlink target."""
    def _make(files: dict[str, bytes | str], untracked: dict[str, str] | None = None) -> Path:
        root = tmp_path / "repo"
        root.mkdir()
        git(root, "init", "-q", "-b", "main")
        for rel, data in files.items():
            p = root / rel
            p.parent.mkdir(parents=True, exist_ok=True)
            if isinstance(data, str) and data.startswith("->"):
                p.symlink_to(data[2:])
            elif isinstance(data, bytes):
                p.write_bytes(data)
            else:
                p.write_text(data, encoding="utf-8")
        git(root, "add", "-A")
        for rel, data in (untracked or {}).items():
            (root / rel).parent.mkdir(parents=True, exist_ok=True)
            (root / rel).write_text(data, encoding="utf-8")
        return root
    return _make
