"""Id references (#346): hits grouped by area, whole-word matching, no hits, and the real repo."""
import importlib.util

import pytest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SPEC = importlib.util.spec_from_file_location('id_references', ROOT / 'scripts/id_references.py')
m = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(m)


def test_groups_hits_by_area_and_matches_whole_words(make_repo):
    root = make_repo({
        'content/model.yaml': '- { id: morale }\n- { id: morale_extra }\n',
        'sim/StoryOfTheLand.Sim/A.cs': 'Shift(c, s, "morale", 1);\n',
        'sim/StoryOfTheLand.Sim.Tests/B.cs': '// morale\n',
        'unity/Assets/Scripts/Hud.cs': 'var m = "morale";\n',
        'docs/x.md': 'morale\n',
    })
    rows = m.hits(root, 'morale')
    assert sorted(a for a, _ in rows) == ['content', 'sim', 'tests', 'unity']
    out = m.render(rows)
    assert '== content (1)' in out and 'content/model.yaml:1:' in out and 'morale_extra' not in out
    assert 'docs/x.md' not in out


def test_no_references(make_repo, capsys):
    root = make_repo({'content/model.yaml': 'x\n'})
    assert m.main(['nothing_here', '--root', str(root)]) == 0
    assert capsys.readouterr().out.strip() == 'no references'


def test_a_git_error_is_an_error_not_no_references(tmp_path, monkeypatch, capsys):
    """`no references` tells an agent a record is safe to delete, so a failed `git grep` must never print it."""
    monkeypatch.setenv('GIT_CEILING_DIRECTORIES', str(tmp_path.parent))   # tmp_path is not inside any checkout
    with pytest.raises(SystemExit) as e:
        m.main(['morale', '--root', str(tmp_path)])
    assert e.value.code not in (0, None)
    assert 'not a git repository' in str(e.value.code)
    assert 'no references' not in capsys.readouterr().out


def test_real_repo_finds_a_code_named_indicator():
    areas = {a for a, _ in m.hits(ROOT, 'livelihood')}   # PlayerText.Livelihood reads it by name
    assert {'content', 'sim'} <= areas
