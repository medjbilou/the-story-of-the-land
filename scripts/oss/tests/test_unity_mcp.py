"""Unity-MCP (com.ivanmurzak.unity.mcp) is an AI-agent bridge for the Unity editor. The project does not use it or its
OpenUPM registry, because it cannot be downloaded here (README.md, "Unity 6.6 notes"; THIRD-PARTY-NOTICES.md). These tests keep it out, and keep the guards that still matter without it."""
import json
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
PKG = "com.ivanmurzak.unity.mcp"
OPENUPM = "https://package.openupm.com"


def manifest() -> dict:
    return json.loads((ROOT / "unity/Packages/manifest.json").read_text(encoding="utf-8"))


def lock() -> dict:
    return json.loads((ROOT / "unity/Packages/packages-lock.json").read_text(encoding="utf-8"))["dependencies"]


def test_the_plugin_and_its_registry_are_absent():
    assert PKG not in manifest()["dependencies"]
    assert not [r for r in manifest().get("scopedRegistries", []) if r.get("url") == OPENUPM]
    assert not [n for n, d in lock().items() if d.get("url") == OPENUPM or n.startswith(("com.ivanmurzak", "extensions.unity"))]


def test_notices_record_the_plugin_as_unused():
    notices = (ROOT / "THIRD-PARTY-NOTICES.md").read_text(encoding="utf-8")
    assert "Unity-MCP" in notices and "are not used: they are not in\n`unity/Packages/manifest.json`" in notices


def test_project_settings_do_not_commit_the_plugin_gate_defines():
    # The plugin's resolver downloads its NuGet DLLs, then adds UNITY_MCP_READY and UNITY_MCP_DEPS_<n> on each
    # machine. Committed, the defines reach a fresh clone before the DLLs do, and the plugin fails to compile
    # (error CS0234 'System.Text.Json', ...), which stops batch mode. Leave that local change uncommitted: this reads
    # the committed file, so a contributor's own working copy (which has the defines) still passes.
    text = subprocess.run(["git", "-C", str(ROOT), "show", "HEAD:unity/ProjectSettings/ProjectSettings.asset"],
                          capture_output=True, check=True, text=True).stdout
    assert "UNITY_MCP_" not in text


def test_downloads_and_generated_agent_files_are_ignored():
    # The NuGet DLLs are third-party binaries fetched on first open; .mcp.json can carry the local server's bearer
    # token; the generated skills change with every plugin version. UserSettings/ (the plugin's per-user config and
    # local token) was already ignored.
    ignore = (ROOT / ".gitignore").read_text(encoding="utf-8").splitlines()
    for rule in ("unity/Assets/Plugins/NuGet/", "unity/Assets/Plugins/NuGet.meta", ".mcp.json", "unity/.claude/",
                 "unity/UserSettings/", "unity/Library/", "unity/Assets/packages-merged-link/",
                 "unity/Assets/packages-merged-link.meta", "unity/.agent/", "unity/.agents/", "unity/.cline/",
                 "unity/.codex/", "unity/.cursor/", "unity/.gemini/", "unity/.github/", "unity/.junie/",
                 "unity/.kilocode/", "unity/.opencode/", "unity/.roo/", "unity/opencode.json"):
        assert rule in ignore, rule


CI_OFF = "-CI false -GITHUB_ACTIONS false -TF_BUILD false"


def test_every_documented_batch_unity_command_turns_the_plugins_ci_mode_off():
    # The plugin treats CI, GITHUB_ACTIONS or TF_BUILD = true as CI: its resolver then skips its NuGet download but still
    # adds its gate defines, so a fresh clone fails to compile and the batch run hangs (fix rounds 1 and 2). The plugin
    # reads each key from the command line before the environment, so these three arguments win. Any line that starts
    # Unity in batch mode on the project, in any form, must carry them.
    files = [f for f in subprocess.run(["git", "-C", str(ROOT), "ls-files", "-z", "*.md", "tools/*.sh", "scripts/*.sh",
                                        "unity/Assets/Editor/*.cs"],
                                       capture_output=True, check=True, text=True).stdout.split("\0") if f]
    files = [f for f in files if (ROOT / f).exists()]
    seen = 0
    for rel in files:
        for n, line in enumerate((ROOT / rel).read_text(encoding="utf-8").splitlines(), 1):
            if "-batchmode" in line and "-projectPath" in line:
                seen += 1
                assert CI_OFF in line, f"{rel}:{n}"
    assert seen >= 17


def test_ci_section_says_a_unity_job_must_turn_ci_mode_off():
    text = (ROOT / "CONTRIBUTING.md").read_text(encoding="utf-8").split("## What CI runs", 1)[1].split("\n## ", 1)[0]
    assert CI_OFF in text and "GITHUB_ACTIONS" in text and "TF_BUILD" in text


def test_docs_name_all_three_ci_variables():
    for rel in ("CONTRIBUTING.md", "BUILDING.md"):
        text = (ROOT / rel).read_text(encoding="utf-8")
        for fact in ("GITHUB_ACTIONS", "TF_BUILD", CI_OFF):
            assert fact in text, (rel, fact)


def test_unity_mcp_feature_request_draft_is_kept_for_the_owner():
    text = (ROOT / "docs/unity-mcp-feature-request.md").read_text(encoding="utf-8")
    assert "versionDefines" in text and "ProjectSettings.asset" in text and "not filed" in text
