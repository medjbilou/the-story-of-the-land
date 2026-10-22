from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8")


def test_decisions_record_d2_decided():
    text = read("docs/oss/DECISIONS.md")
    assert "- [x] D2" in text and "inbound = outbound" in text


def test_security_policy_uses_private_reporting_only():
    text = read("SECURITY.md")
    assert "security/advisories/new" in text and "@" not in text
    assert "revoked at its provider; history is not rewritten" in text


def test_contributing_links_security_and_untrusted_review():
    text = read("CONTRIBUTING.md")
    assert "(SECURITY.md)" in text and "docs/UNTRUSTED_PR_REVIEW.md" in text


def test_contributing_has_no_cla_and_names_every_sensitive_tag():
    text = read("CONTRIBUTING.md")
    assert "There is no CLA and no sign-off" in text
    for tag in ("holocaust", "terror", "massacre", "war-casualties", "displacement"):
        assert f"`{tag}`" in text, tag


def test_untrusted_pr_review_forbids_running_pr_code_and_warns_about_github_diffs():
    text = read("docs/UNTRUSTED_PR_REVIEW.md")
    assert "--network none" in text and "Assets/Editor" in text and "Directory.Build.props" in text
    assert "Any diff under `.github/` can make every required check green" in text


def test_contributing_names_every_ci_command_and_the_unity_gate():
    text = read("CONTRIBUTING.md")
    for cmd in ("dotnet test sim/StoryOfTheLand.sln",
                "uv run --with pytest==9.1.1 --with rasterio==1.5.2 --with numpy==2.5.3 --with pyyaml==6.0.3 "
                "--with pillow==12.3.0 --with requests==2.32.5 --with pyproj==3.8.0 pytest tools/tests -q",
                "uv run --with pytest==9.1.1 --with pyyaml==6.0.3 --with requests==2.32.5 pytest scripts/oss/tests -q",
                "uv run scripts/oss/check_repo.py",
                "-buildTarget OSXUniversal -runTests -testPlatform EditMode", "## What CI runs", "CHANGELOG.md"):
        assert cmd in text, cmd


def test_check_script_runs_every_ci_command():
    # scripts/check.sh (#263) promises to run what CI runs; a pin changed in ci.yml must change there too.
    import yaml
    ci = yaml.safe_load(read(".github/workflows/ci.yml"))
    script = read("scripts/check.sh")
    runs = [s["run"] for job in ci["jobs"].values() for s in job["steps"] if "run" in s]
    assert len(runs) == 6, runs
    for cmd in runs:
        # The changelog job (and the reviewed_by guard in it) passes the PR's SHAs; the script passes its own base ref.
        cmd = cmd.split(" --base ")[0]
        assert cmd in script, cmd
    assert f'UV_PYTHON:-{ci["env"]["UV_PYTHON"]}' in script


def test_changelog_has_unreleased_on_top():
    text = read("CHANGELOG.md")
    assert text.index("## [Unreleased]") == text.index("## [")


def test_historical_accuracy_form_requires_a_source():
    import yaml
    form = yaml.safe_load(read(".github/ISSUE_TEMPLATE/historical_accuracy.yml"))
    body = {b.get("id"): b for b in form["body"]}
    assert body["sources"]["validations"]["required"] is True
    assert {"where", "wrong", "sources"} <= set(body)
    cfg = yaml.safe_load(read(".github/ISSUE_TEMPLATE/config.yml"))
    assert cfg["blank_issues_enabled"] is False
    assert any("security/advisories/new" in c["url"] for c in cfg["contact_links"])


def test_task_form_has_every_field_and_all_are_required():
    import yaml
    form = yaml.safe_load(read(".github/ISSUE_TEMPLATE/task.yml"))
    fields = {b["id"]: b for b in form["body"] if b["type"] != "markdown"}
    assert set(fields) == {"goal", "files", "done", "sources", "signoff", "unity"}
    assert all(f["validations"]["required"] is True for f in fields.values())
    assert fields["unity"]["type"] == "dropdown"


def test_task_and_feature_forms_ask_the_same_unity_question():
    import yaml
    forms = {}
    for name in ("task", "feature_request"):
        form = yaml.safe_load(read(f".github/ISSUE_TEMPLATE/{name}.yml"))
        field = {b["id"]: b for b in form["body"] if b["type"] != "markdown"}["unity"]
        assert field["type"] == "dropdown" and field["validations"]["required"] is True, name
        forms[name] = field["attributes"]["options"]
    assert forms["task"] == forms["feature_request"]
    assert read("docs/dev-setup.md").count("needs-unity") >= 1 and "needs-unity" in read("CONTRIBUTING.md")


def test_content_policy_states_the_owner_rules_and_only_real_mechanisms():
    text = read("docs/CONTENT_POLICY.md")
    for s in ("## 1. The perspective is Palestinian", "a Palestinian family and village", "Every fact is sourced",
              "No invented quotes", "How to dispute a fact", "No Israeli media as a source", "74,480", "72,835",
              "`arabic-reviewer`", "native Palestinian Arabic speaker", "content note", "`sensitive`",
              "narration is synthetic", "Wikipedia"):
        assert s in text, s
    # The content note the policy quotes is the one the game shows (unity/Assets/Game/EventCard.cs).
    card = ROOT.parent / "unity/Assets/Game/EventCard.cs"
    if card.exists():
        note = "تنبيه: يتناول هذا الحدث القتل والعنف."
        assert note in text and note in card.read_text(encoding="utf-8")
    assert "DRAFT stamp" not in text and "Release builds include only fully reviewed content" not in text


def test_code_of_conduct_has_a_private_contact_and_the_history_section():
    text = read("CODE_OF_CONDUCT.md")
    assert "[INSERT" not in text and "getnohuman" not in text
    # Fix round 2: the owner's profile has no public email, so that channel is dead; private reporting works today.
    assert "profile" not in text.lower().replace("profile of", "")
    assert "security/advisories/new" in text and 'title with "Conduct:"' in text and "only to you and the repository's administrators" in text
    assert "## Historical content and political discussion" in text
    assert "Contributor Covenant" in text and "version 2.1" in text


def test_pr_template_has_evidence_and_no_cla_item():
    text = read(".github/pull_request_template.md")
    assert "## Evidence" in text and "THIRD-PARTY-NOTICES.md" in text and "ASSETS.yaml" in text
    assert "CLA" not in text and "contributors/" not in text


def test_pr_template_asks_for_ai_disclosure_issue_link_and_goldens():
    # Issue #264: the checklist pointed at a disclosure "above" that nothing asked for.
    text = read(".github/pull_request_template.md")
    assert "## AI assistance" in text and "disclosed under \"AI assistance\"" in text
    assert "Fixes #" in text and "Refs #" in text and "only if this PR completes the issue" in text
    assert "content/palestine/goldens.yaml" in text and "docs/determinism.md" in text
    assert "## [Unreleased]" in text


def test_pr_template_asks_for_the_status_table():
    text = read(".github/pull_request_template.md")
    assert "Status table" in text and "README.md" in text and "docs/PLAN.md" in text and "README.he.md" not in text


def test_content_policy_claims_no_mechanism_that_does_not_exist():
    # Review I1: living-people exclusion, share-alike refusal and a claim verifier have no code behind them.
    text = " ".join(read("docs/CONTENT_POLICY.md").split())
    for phrase in ("excluded from release builds", "share-alike sources are refused", "A separate verifier",
                   "any mismatch blocks the text", "marked AI-generated in `ASSETS.yaml`"):
        assert phrase not in text, phrase


def test_conduct_reports_use_private_reporting_and_no_profile_email():
    import yaml
    sec = read("SECURITY.md")
    assert 'start the title with "Conduct:"' in sec and "profile" not in sec
    cfg = yaml.safe_load(read(".github/ISSUE_TEMPLATE/config.yml"))
    assert any("security/advisories/new" in c["url"] and "onduct" in c["about"] for c in cfg["contact_links"])
    assert "profile" not in read(".github/ISSUE_TEMPLATE/config.yml")
    d7 = next(l for l in read("docs/oss/DECISIONS.md").splitlines() if "D7" in l)
    assert d7.startswith("- [x] D7") and "dedicated contact" in d7


def test_docs_describe_only_what_is_live_today():
    # Review I2: main-review (code-owner review, squash-only) is not applied yet; Task 13 updates these when it is.
    c = read("CONTRIBUTING.md")
    assert "fresh approval" not in c and "squash-merged" not in c
    o = read(".github/CODEOWNERS")
    assert "main-review" not in o


def test_contributing_uses_the_check_that_passes_today():
    # Review I6: the full check_repo run fails until Task 14; CI's own command is what a contributor can run.
    c = read("CONTRIBUTING.md")
    # Task 14 landed the last required files, so CI and CONTRIBUTING both run the full check now.
    assert "`uv run scripts/oss/check_repo.py` (the command CI runs)" in c and "check_repo.py --only" not in c


def test_untrusted_review_profile_restores_with_network_then_runs_without():
    # Review I3: --network none cannot restore packages, and a read-only root-owned mount cannot build.
    text = read("docs/UNTRUSTED_PR_REVIEW.md")
    assert "Stage 1" in text and "Stage 2" in text and '--user "$(id -u):$(id -g)"' in text
    assert "-e HOME=/tmp" in text and "cp -a /src /cache/work" in text and text.count("--network none") >= 1
    stage1 = text.split("Stage 1")[1].split("Stage 2")[0]
    assert "--network none" not in stage1


def test_small_doc_fixes_from_review():
    assert "promo/gen.py" not in read("SECURITY.md")  # M5: it reads a file today; Task 11 changes that
    coc = read("CODE_OF_CONDUCT.md")
    assert coc.index("## Historical content and political discussion") < coc.index("## Attribution")  # M6
    assert "](docs/CONTENT_POLICY.md)" in coc
    form = read(".github/ISSUE_TEMPLATE/historical_accuracy.yml")
    assert "Read docs/CONTENT_POLICY.md in this repository first." in form  # M7
    assert "The game's Palestinian perspective" in form


def test_contributing_requires_a_provenance_row():
    text = read("CONTRIBUTING.md")
    assert "**Provenance.**" in text and "[ASSETS.yaml](ASSETS.yaml)" in text


def test_gitignore_covers_editor_licence_and_local_files():
    lines = set(read(".gitignore").splitlines())
    for rule in (".env.*", "*.alf", "Unity_v*.alf", ".claude/settings.local.json", ".pytest_cache/", ".vs/",
                 ".idea/", "unity/obj/", "unity/MemoryCaptures/"):
        assert rule in lines, rule


def test_releasing_merges_before_the_gate_and_pins_the_full_expect_values():
    text = read("docs/RELEASING.md")
    assert text.index("**Merge**") < text.index("**Run the release gate**")
    det = read("docs/determinism.md")
    import re
    for expect in re.findall(r'--expect "((?:GOLDEN|SMOKE) OK [^"]* [0-9a-f]{64})"', det):
        assert f'--expect "{expect}"' in text, expect


def test_notices_cover_the_shader_graph():
    assert "StoryOfTheLandTerrain.shadergraph" in read("THIRD-PARTY-NOTICES.md")


def test_release_gate_runs_the_tools_tests_exactly_as_ci_does():
    # Without the same --with list (pyproj), the gate skipped 12 tools tests that CI runs.
    ci = [l.strip() for l in read(".github/workflows/ci.yml").splitlines() if "pytest tools/tests" in l]
    assert len(ci) == 1 and ci[0].startswith("run: ")
    assert ci[0][len("run: "):] in read("scripts/oss/release_gate.sh")
