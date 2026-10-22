"""#45: dimensions/count checks are not a gameplay-provenance verdict."""
import importlib.util
from pathlib import Path
from PIL import Image
ROOT = Path(__file__).resolve().parents[2]


def module():
    spec = importlib.util.spec_from_file_location('store_screenshots', ROOT / 'tools/store_screenshots.py')
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m


def shots(tmp_path, count=5, size=(1920, 1080)):
    paths = []
    for i in range(count):
        p = tmp_path / f'{i}.png'
        Image.new('RGB', size).save(p)
        paths.append(p)
    return paths


def test_five_widescreen_images_pass_and_four_fail(tmp_path):
    m = module()
    paths = shots(tmp_path)
    assert m.validate(paths) == []
    assert any('at least 5' in e for e in m.validate(paths[:4]))


def test_smaller_readme_shot_and_wrong_ratio_fail(tmp_path):
    m = module()
    paths = shots(tmp_path)
    Image.new('RGB', (1568, 752)).save(paths[0])  # the README build shots (docs/images/build-m3-*.jpg)
    errors = m.validate(paths)
    assert any('1920 x 1080' in e and '0.png' in e for e in errors)
    Image.new('RGB', (1920, 1200)).save(paths[0])
    assert any('16:9' in e for e in m.validate(paths))


def test_duplicate_paths_do_not_meet_count_and_bad_file_is_named(tmp_path):
    m = module()
    paths = shots(tmp_path)
    assert any('duplicate' in e for e in m.validate([paths[0]] * 5))
    paths[0].write_text('not an image')
    assert any('cannot read' in e and '0.png' in e for e in m.validate(paths))


def test_cli_pass_is_explicitly_limited(tmp_path, capsys):
    m = module()
    assert m.main([str(p) for p in shots(tmp_path)]) == 0
    out = capsys.readouterr().out
    assert 'DIMENSIONS OK' in out
    assert 'not gameplay provenance' in out
    assert m.main([]) == 1
