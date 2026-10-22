"""The README of The Story of the Land: no origin story, no Hebrew, the build instructions, links that resolve."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
HEBREW = re.compile("[%s-%s%s-%s]" % (chr(0x0590), chr(0x05FF), chr(0xFB1D), chr(0xFB4F)))  # Hebrew, presentation forms


def read(rel: str) -> str:
    return (ROOT / rel).read_text(encoding="utf-8")


def section(text: str, heading: str) -> str:
    return text.split(heading, 1)[1].split("\n## ", 1)[0]


def test_title_names_the_game_in_english_and_arabic():
    first = read("README.md").lstrip().splitlines()[0]
    assert first == "# The Story of the Land (حكاية الأرض)"


def test_readme_tells_no_origin_story_and_points_to_the_notices():
    text = read("README.md")
    head = text.split("## What is this?", 1)[0]
    assert "non-commercial" in head
    # the README names neither the third-party creator in LICENSE nor that work (owner, 2026-10-22); the legal notices
    # are in LICENSE and THIRD-PARTY-NOTICES.md
    creator = re.findall(r"^Copyright \(c\) 2026 (.+)$", read("LICENSE"), re.M)[1]
    assert creator.split()[-1] not in text and "inspired by" not in text.lower()
    for s in ("fork", "upstream", "original project", "based on"):
        assert s not in text.lower(), s
    assert "THIRD-PARTY-NOTICES.md" in section(text, "## License")


def test_no_hebrew_readme_and_no_hebrew_text():
    assert not (ROOT / "README.he.md").exists()
    for rel in ("README.md", "BUILDING.md", "CONTRIBUTING.md"):
        assert not HEBREW.search(read(rel)), rel
    assert "README.he.md" not in read("README.md")


def test_build_section_pins_unity_and_gives_the_commands():
    build = section(read("README.md"), "## Build from source")
    version = re.search(r"m_EditorVersion: (\S+)", read("unity/ProjectSettings/ProjectVersion.txt")).group(1)
    assert version in build and "unity/ProjectSettings/ProjectVersion.txt" in build and "BUILDING.md" in build
    for fact in ("uv run tools/build_sim_for_unity.py", "-executeMethod StoryOfTheLand.Game.EditorTools.Build.MacMono",
                 "StoryOfTheLand.Game.EditorTools.Build.Web", "-CI false -GITHUB_ACTIONS false -TF_BUILD false",
                 "First open in Unity", "Assets/Map.unity"):
        assert fact in build, fact


def test_tests_section_names_the_python_suites():
    tests = section(read("BUILDING.md"), "## Tests")
    assert "pytest tools/tests" in tests and "pytest scripts/oss/tests" in tests and "check_repo.py" in tests


def test_unity_mcp_is_documented_as_unused():
    assert "Unity-MCP packages are not in" in read("BUILDING.md") and "com.ivanmurzak.unity.mcp" not in read("README.md")


def test_every_local_link_and_image_exists():
    for rel in ("README.md", "BUILDING.md", "CONTRIBUTING.md"):
        text = read(rel)
        targets = re.findall(r'\]\(([^)#\s]+)', text) + re.findall(r'(?:src|href)="([^"#]+)"', text)
        local = [t for t in targets if not t.startswith(("http://", "https://", "mailto:"))]
        missing = [t for t in local if not (ROOT / t).exists()]
        assert not missing, (rel, missing)


def test_no_counts_in_building_prose():
    assert "five commands" not in read("BUILDING.md") and "five commands" not in read("README.md")


def test_images_are_web_sized():
    for p in (ROOT / "docs/images").glob("*.jpg"):
        assert p.stat().st_size < 1_000_000, p.name


def test_building_names_contributing_headings_that_exist():
    building, contributing = read("BUILDING.md"), read("CONTRIBUTING.md")
    for heading in ("Rules every change follows", "What CI runs"):
        assert f'"{heading}"' in building and f"## {heading}" in contributing, heading


def test_readme_has_no_formatting_churn():
    text = read("README.md")
    assert "\n\n\n" not in text, "double blank line"
    assert not re.search(r"\n [A-Za-z<]", text), "continuation line indented by one space"
    for block in re.findall(r"(?:^- .*\n(?:\n)?)+", text, re.M):
        assert "\n\n- " not in block, "loose list: " + block[:40]
