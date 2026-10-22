import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]


def test_mit_licence_keeps_the_copyright_lines():
    text = (ROOT / "LICENSE").read_text(encoding="utf-8")
    assert "MIT License" in text and "Copyright (c) 2026 Mohamed Jbilou and The Story of the Land contributors" in text
    assert len(re.findall(r"^Copyright \(c\) 2026 ", text, re.M)) == 2  # the MIT notices must ship with every copy


def third_party_creator() -> str:
    """The second copyright holder in LICENSE: the work this game is inspired by (credited, never described)."""
    return re.findall(r"^Copyright \(c\) 2026 (.+)$", (ROOT / "LICENSE").read_text(encoding="utf-8"), re.M)[1]


def test_notices_carry_the_third_party_attribution_and_nothing_of_the_history():
    text = (ROOT / "THIRD-PARTY-NOTICES.md").read_text(encoding="utf-8")
    for s in (f"Copyright (c) 2026 {third_party_creator()}.",
              "https://creativecommons.org/licenses/by/4.0/", "https://creativecommons.org/licenses/by-sa/4.0/",
              "Modified.", "stays under CC BY-SA 4.0"):
        assert s in text, s
    for s in ("fork", "began as", "based on", "inspired by", "upstream", third_party_creator().replace(" ", "").lower()):
        assert s not in text.lower(), s   # no history, and no link to the creator's pages


def test_licence_texts_are_the_real_ones():
    for name, marker in (("CC-BY-4.0", "Attribution 4.0 International"),
                         ("CC-BY-SA-4.0", "Attribution-ShareAlike 4.0 International"),
                         ("CC0-1.0", "CC0 1.0 Universal")):
        assert marker in (ROOT / f"LICENSES/{name}.txt").read_text(encoding="utf-8"), name
    yaml_dot_net = (ROOT / "LICENSES/YamlDotNet-MIT.txt").read_text(encoding="utf-8")
    assert "Antoine Aubry" in yaml_dot_net and "Permission is hereby granted" in yaml_dot_net


def test_licensing_table_covers_every_top_level_area():
    text = (ROOT / "THIRD-PARTY-NOTICES.md").read_text(encoding="utf-8")
    for d in ("sim/", "tools/", "scripts/", ".github/", "unity/Assets/", "content/", "docs/", "ASSETS.yaml",
              "unity/Assets/Settings/", "unity/Packages/"):
        assert f"`{d}" in text, d


def test_licensing_puts_unity_template_files_under_the_companion_licence():
    text = (ROOT / "THIRD-PARTY-NOTICES.md").read_text(encoding="utf-8")
    row = next(l for l in text.splitlines() if l.startswith("| Unity template-derived files"))
    assert "Unity Companion License" in row and "unity/Assets/Settings/" in row and "not MIT" in row


def test_licensing_points_at_decisions_and_assets():
    text = (ROOT / "THIRD-PARTY-NOTICES.md").read_text(encoding="utf-8")
    assert "docs/oss/DECISIONS.md" in text and "ASSETS.yaml" in text and "published before the checks" in text
    assert not (ROOT / "LICENSING.md").exists(), "one notices file: THIRD-PARTY-NOTICES.md"


def test_notice_carries_the_required_attributions():
    text = (ROOT / "NOTICE").read_text(encoding="utf-8")
    assert "produced using Copernicus WorldDEM-30 (c) DLR e.V. 2010-2014" in text
    for s in ("CHELSA V2.1", "Israel Meteorological Service", "SIL Open Font License", "Wikipedia", "YamlDotNet"):
        assert s in text, s


def test_every_repo_path_licensing_names_exists():
    """Every backticked repo path in THIRD-PARTY-NOTICES.md names something tracked, so the table never licenses a file that is
    gone (the decision clips' old label, tools/event_videos/ai-bug.png, was deleted in #551).
    What counts as a repo path: a backticked token with a "/" whose first segment is a tracked top-level entry
    (`unity/...`, `tools/...`, `.github/`). Skipped as not repo paths: URLs, placeholders such as `<name>`, and tokens
    with no "/" or a relative first segment (`*.md`, `.meta`, `link.xml`, `NotoNaskhArabic/`, `08`), which name a kind of
    file or a file inside a folder the row names. A token with * or ? must match at least one tracked file; any other
    token must be a tracked file or the start of one's path (a folder, or `docs/images/build-` for its build-*.jpg files)."""
    import fnmatch
    import re
    import subprocess
    out = subprocess.run(["git", "ls-files", "--cached", "--others", "--exclude-standard"], cwd=ROOT, capture_output=True,
                         text=True, check=True).stdout
    tracked = [p for p in out.splitlines() if (ROOT / p).exists()]  # the working tree: deleted files do not count
    top = {p.split("/", 1)[0] for p in tracked}
    text = (ROOT / "THIRD-PARTY-NOTICES.md").read_text(encoding="utf-8")
    missing = []
    for token in sorted(set(re.findall(r"`([^`\n]+)`", text))):
        if "://" in token or "<" in token or "/" not in token or token.split("/", 1)[0] not in top:
            continue
        if any(c in token for c in "*?"):
            ok = any(fnmatch.fnmatchcase(p, token) for p in tracked)
        else:
            ok = any(p == token or p.startswith(token) for p in tracked)
        if not ok:
            missing.append(token)
    assert missing == [], f"THIRD-PARTY-NOTICES.md names paths that are not in the repository: {missing}"


def test_the_owner_is_credited_without_an_email_address():
    owner = "Mohamed Jbilou"
    assert "Copyright (c) 2026 Mohamed Jbilou" in (ROOT / "LICENSE").read_text(encoding="utf-8")
    for rel in ("NOTICE", "THIRD-PARTY-NOTICES.md"):
        assert f"The Story of the Land (حكاية الأرض), by {owner}" in (ROOT / rel).read_text(encoding="utf-8"), rel
    assert f"By {owner}." in (ROOT / "README.md").read_text(encoding="utf-8")
    credits = (ROOT / "docs/credits.md").read_text(encoding="utf-8")
    assert f"Created by {owner}." in credits and "من صنع محمد جبيلو." in credits
    for rel in ("LICENSE", "NOTICE", "THIRD-PARTY-NOTICES.md", "README.md", "docs/credits.md"):
        assert "@" not in (ROOT / rel).read_text(encoding="utf-8"), rel


def test_the_credits_screen_shows_the_game_and_its_sources_and_the_notices_carry_the_rest():
    """The owner's ruling (2026-10-20): the Credits screen names the creator, the sources, the Wikipedia licence, the
    narration and the game's licences; every other notice moves, word for word, into THIRD-PARTY-NOTICES.md."""
    credits = (ROOT / "docs/credits.md").read_text(encoding="utf-8").split("\n---\n", 1)[1]
    for s in ("### Sources", "### المصادر", "El-Daheeh", "palquest.org", "TechForPalestine", "archivegenocide.com",
              "Adalah", "Palestine Remembered", "Village Statistics 1945",
              "Much of the text is adapted from Wikipedia (CC BY-SA 4.0).", "No real person's voice",
              "All other licences and notices: THIRD-PARTY-NOTICES.md in the game folder."):
        assert s in credits, s
    for gone in ("Heuberger", "Garamond", "Noto", "Copernicus", "CHELSA", "Meteorological", "YamlDotNet",
                 "Unity", "MIT", "CC BY 4.0", "inspired"):
        assert gone not in credits, gone
    notices = " ".join((ROOT / "THIRD-PARTY-NOTICES.md").read_text(encoding="utf-8").split())
    for s in ("produced using Copernicus WorldDEM-30 (c) DLR e.V. 2010-2014 and (c) Airbus Defence and Space GmbH "
              "2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved",
              "Copyright 2017 The EB Garamond Project Authors", "Copyright 2022 The Noto Project Authors",
              "SIL Open Font License 1.1", "Copyright (c) Antoine Aubry and contributors. MIT License",
              "Habibi-TTS Specialized MSA, Apache License 2.0", "**3D models** by Tal Heuberger: CC0 1.0",
              "**CHELSA V2.1 bio12** (1981-2010), CC0 1.0", "data.gov.il dataset 481", 'licence "Other (Open)"'):
        assert s in notices, s
