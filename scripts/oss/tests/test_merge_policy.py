import json
import os
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
GH = ROOT / "scripts/oss/github"


def load(name: str) -> dict:
    return json.loads((GH / name).read_text())


def rule(ruleset: dict, kind: str) -> dict | None:
    return next((r for r in ruleset["rules"] if r["type"] == kind), None)


def test_squash_only_no_auto_merge_branches_deleted_discussions_on():
    s = load("repo-settings.json")
    assert s["allow_squash_merge"] is True
    assert s["allow_merge_commit"] is False and s["allow_rebase_merge"] is False
    assert s["allow_auto_merge"] is False  # no_human's live setting was true despite its docs
    assert s["delete_branch_on_merge"] is True and s["has_discussions"] is True


def test_history_ruleset_is_the_live_one_and_nobody_bypasses_it():
    # Review N2: force-push and deletion stay blocked for everyone, the owner included (owner ruling 1).
    r = load("ruleset-protect-main.json")
    assert r["name"] == "protect-main" and r["enforcement"] == "active"
    assert r["conditions"]["ref_name"]["include"] == ["~DEFAULT_BRANCH"]
    assert {x["type"] for x in r["rules"]} == {"non_fast_forward", "deletion"}
    assert r["bypass_actors"] == []


def test_review_ruleset_requires_code_owner_review_squash_and_checks():
    r = load("ruleset-main-review.json")
    assert r["name"] == "main-review" and r["enforcement"] == "active"
    assert r["conditions"]["ref_name"]["include"] == ["~DEFAULT_BRANCH"]
    assert {x["type"] for x in r["rules"]} == {"pull_request", "required_status_checks"}
    pr = rule(r, "pull_request")["parameters"]
    assert pr["require_code_owner_review"] and pr["dismiss_stale_reviews_on_push"] and pr["require_last_push_approval"]
    assert pr["required_approving_review_count"] == 1 and pr["required_review_thread_resolution"]
    assert pr["allowed_merge_methods"] == ["squash"]
    assert rule(r, "required_status_checks")["parameters"]["strict_required_status_checks_policy"] is True
    # Each check must come from the GitHub Actions app (id 15368), so a status posted by anything else can't satisfy it.
    assert {c["integration_id"] for c in rule(r, "required_status_checks")["parameters"]["required_status_checks"]} == {15368}


def test_no_history_enforcement():
    # Owner ruling 2026-10-11: no signed-commit or linear-history requirement.
    for name in ("ruleset-protect-main.json", "ruleset-main-review.json"):
        r = load(name)
        assert rule(r, "required_signatures") is None and rule(r, "required_linear_history") is None


def test_only_the_review_ruleset_has_the_admin_bypass():
    # Decision D12: the maintainer keeps merging locally and pushing; everyone else goes through review and checks.
    assert load("ruleset-main-review.json")["bypass_actors"] == [
        {"actor_id": 5, "actor_type": "RepositoryRole", "bypass_mode": "always"}]


def test_actions_are_pinned_and_allow_listed():
    p = load("actions-permissions.json")
    assert p["sha_pinning_required"] is True and p["allowed_actions"] == "selected" and p["enabled"] is True
    s = load("actions-selected.json")
    assert s["patterns_allowed"] == ["actions/*", "astral-sh/setup-uv@*"]
    assert s["github_owned_allowed"] is False and s["verified_allowed"] is False
    text = (ROOT / "scripts/oss/apply_github_settings.sh").read_text()
    assert 'actions/permissions" --input' in text and "actions/permissions/selected-actions" in text


def test_apply_script_looks_rulesets_up_by_name():
    text = (ROOT / "scripts/oss/apply_github_settings.sh").read_text()
    assert "put_ruleset protect-main" in text and "put_ruleset main-review" in text
    assert "24834491" not in text


def test_no_classic_branch_protection_file():
    assert not (GH / "branch-protection.json").exists()
    assert "branches/main/protection\" --input" not in (ROOT / "scripts/oss/apply_github_settings.sh").read_text()


def test_labels_include_the_ones_ci_forms_and_readme_use():
    names = {l.split("\t")[0] for l in (GH / "labels.tsv").read_text().splitlines() if l}
    assert {"no-changelog", "historical-accuracy", "bug", "enhancement", "spam", "good first issue"} <= names


def test_apply_script_refuses_without_a_terminal_and_calls_nothing(tmp_path):
    marker = tmp_path / "gh-called"
    fake = tmp_path / "gh"
    fake.write_text(f"#!/bin/sh\ntouch {marker}\n")
    fake.chmod(0o755)
    env = dict(os.environ, PATH=f"{tmp_path}:{os.environ['PATH']}")
    r = subprocess.run(["bash", str(ROOT / "scripts/oss/apply_github_settings.sh"), "example/the-story-of-the-land"],
                       stdin=subprocess.DEVNULL, capture_output=True, text=True, env=env)
    assert r.returncode == 2 and "interactive terminal" in r.stdout
    assert not marker.exists()
