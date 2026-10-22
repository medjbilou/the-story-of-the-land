"""dev_setup_commands: a script named in the dev-setup table must exist; the repo itself is clean."""
import importlib.util
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SPEC = importlib.util.spec_from_file_location('dev_setup_commands', ROOT / 'scripts/oss/dev_setup_commands.py')
m = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(m)


def test_missing_script_is_reported_with_its_line(make_repo, capsys):
    root = make_repo({
        'docs/dev-setup.md': '# Setup\n| a | `uv run scripts/here.py [x]` | none |\n| b | `uv run scripts/gone.py` | none |\n'
                             'plain scripts/not_in_backticks.py\n| c | `bash tools/run.sh` | none |\n'
                             '| d | `uv run sim/tools/x.py` | `foo-scripts/a.py` |\n',  # inside a longer path: not reported
        'scripts/here.py': 'x\n',
    })
    assert m.missing(root) == [(3, 'scripts/gone.py'), (5, 'tools/run.sh')]
    assert m.main(['--root', str(root)]) == 1
    assert 'docs/dev-setup.md:3: scripts/gone.py does not exist' in capsys.readouterr().out


def test_clean_returns_zero(make_repo):
    root = make_repo({'docs/dev-setup.md': '`uv run scripts/a.py`\n', 'scripts/a.py': 'x\n'})
    assert m.main(['--root', str(root)]) == 0


def test_this_repo_is_clean():
    assert m.missing(ROOT) == []


def test_a_script_with_no_row_is_reported(make_repo, capsys):
    root = make_repo({'docs/dev-setup.md': '`uv run scripts/a.py`\nSee scripts/c.sh for the rest.\n',  # prose only: not a name
                      'scripts/a.py': 'x\n', 'scripts/b.sh': 'x\n', 'scripts/c.sh': 'x\n', 'scripts/notes.txt': 'x\n'})
    assert m.undocumented(root) == ['scripts/b.sh', 'scripts/c.sh']
    assert m.main(['--root', str(root)]) == 1
    assert 'scripts/b.sh is not named in docs/dev-setup.md' in capsys.readouterr().out


def test_a_directory_named_like_a_script_is_not_a_script(make_repo):
    root = make_repo({'docs/dev-setup.md': '`uv run scripts/a.py`\n', 'scripts/a.py': 'x\n', 'scripts/d.py/inner.txt': 'x\n'})
    assert m.undocumented(root) == []


def test_a_longer_name_does_not_name_the_script_inside_it(make_repo):
    root = make_repo({'docs/dev-setup.md': '`uv run scripts/a.py` and `scripts/e.py.old`\n', 'scripts/a.py': 'x\n',
                      'scripts/e.py': 'x\n'})
    assert m.undocumented(root) == ['scripts/e.py']


def test_this_repo_documents_every_script():
    assert m.undocumented(ROOT) == []
