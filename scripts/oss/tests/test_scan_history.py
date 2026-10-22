import subprocess

import scan_history
from conftest import commit_all, git

FAKE = "sk_" + "cd" * 24


def new_repo(tmp_path):
    root = tmp_path / "r"
    root.mkdir()
    git(root, "init", "-q", "-b", "main")
    return root


def write(root, rel, text):
    p = root / rel
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(text, encoding="utf-8")


def test_a_key_added_then_deleted_is_still_reported_by_type(tmp_path, capsys):
    root = new_repo(tmp_path)
    write(root, "promo/k.py", f'K = "{FAKE}"\n')
    c1 = commit_all(root, "add tool")
    write(root, "promo/k.py", "K = None\n")
    commit_all(root, "remove key")
    assert (c1, "promo/k.py", "elevenlabs-key") in scan_history.scan(root, ["--all"])
    assert scan_history.main(["--repo", str(root)]) == 0
    out = capsys.readouterr().out
    assert f"{c1}  promo/k.py  elevenlabs-key  OPEN" in out and FAKE not in out
    assert "1 open secret-shape findings" in out and "rotated" in out


def test_accepted_rows_are_shown_as_accepted(tmp_path, capsys):
    root = new_repo(tmp_path)
    write(root, "promo/k.py", f'K = "{FAKE}"\n')
    c1 = commit_all(root, "add tool")
    write(root, "docs/oss/history-accepted.tsv",
          f"# commit\tpath\tkind\taction\tdate\n{c1}\tpromo/k.py\televenlabs-key\trevoked at provider\t2026-10-11\n")
    commit_all(root, "record rotation")
    scan_history.main(["--repo", str(root)])
    out = capsys.readouterr().out
    assert f"{c1}  promo/k.py  elevenlabs-key  accepted" in out and "0 open secret-shape findings" in out


def test_content_added_in_a_merge_resolution_is_read(tmp_path):
    root = new_repo(tmp_path)
    write(root, "a.txt", "base\n")
    commit_all(root, "base")
    git(root, "checkout", "-q", "-b", "side")
    write(root, "a.txt", "side\n")
    commit_all(root, "side")
    git(root, "checkout", "-q", "main")
    write(root, "a.txt", "main\n")
    commit_all(root, "main")
    r = subprocess.run(["git", "-C", str(root), "merge", "-q", "side"], capture_output=True)
    assert r.returncode != 0  # conflict
    write(root, "a.txt", f'resolved "{FAKE}"\n')
    merge = commit_all(root, "merge side")
    assert (merge, "a.txt", "elevenlabs-key") in scan_history.scan(root, ["main"])


def test_commit_messages_and_identities_are_scanned(tmp_path):
    root = new_repo(tmp_path)
    write(root, "a.txt", "x\n")
    c1 = commit_all(root, "contact someone" + "@" + "gmail.com for access")
    write(root, "b.txt", "y\n")
    git(root, "add", "-A")
    git(root, "-c", "user.name=t", "-c", "user.email=" + "person" + "@" + "corp.com", "commit", "-qm", "b")
    c2 = git(root, "rev-parse", "HEAD").strip()[:10]
    hits = scan_history.scan(root, ["--all"])
    assert (c1, "(message)", "personal-email") in hits and (c2, "(identity)", "personal-email") in hits


def test_ref_and_all_flags_parse(tmp_path, capsys):
    root = new_repo(tmp_path)
    write(root, "a.txt", "fine\n")
    commit_all(root, "feat: fine")
    assert scan_history.main(["--repo", str(root), "--ref", "main"]) == 0
    assert scan_history.main(["--repo", str(root), "--all"]) == 0
    assert "no findings" in capsys.readouterr().out
