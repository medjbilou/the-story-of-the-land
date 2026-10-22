import changelog_check as cc


def test_user_visible_change_without_changelog_fails_and_names_the_paths():
    ok, msg = cc.verdict(["content/events/x.yaml", "README.md"], skip_label=False)
    assert not ok and "content/events/x.yaml" in msg and "## [Unreleased]" in msg and "no-changelog" in msg


def test_changelog_entry_satisfies_it():
    assert cc.verdict(["sim/StoryOfTheLand.Sim/Rng.cs", "CHANGELOG.md"], skip_label=False)[0]


def test_label_satisfies_it():
    assert cc.verdict(["unity/Assets/Scripts/UI/GameRoot.cs"], skip_label=True)[0]


def test_tests_docs_tools_and_ci_need_no_entry():
    for p in ("unity/Assets/Tests/EditMode/MapTests.cs", "sim/StoryOfTheLand.Sim.Tests/CoreTests.cs", "docs/x.md",
              "tools/hexgrid.py", ".github/workflows/ci.yml", "scripts/oss/check_repo.py"):
        assert cc.verdict([p], skip_label=False)[0], p


def test_every_user_visible_root_is_named():
    for p in ("content/palestine/model.yaml", "sim/StoryOfTheLand.Sim/Rng.cs",
              "unity/Assets/UI/Game.uxml", "unity/ProjectSettings/ProjectSettings.asset", "unity/Packages/manifest.json"):
        assert cc.user_visible([p]) == [p], p
