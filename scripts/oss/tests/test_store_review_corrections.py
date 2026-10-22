"""#45: pin the post-merge review's rule attribution."""
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]


def checklist():
    # Whitespace-normalised so the checks do not depend on where the Markdown wraps.
    text = (ROOT / 'docs/RELEASING.md').read_text().split('## Store-page checklist', 1)[1]
    return ' '.join(text.split())


def test_trailer_recommendation_is_separate_from_project_rule():
    text = checklist()
    assert 'Steam requires a trailer' in text
    assert 'recommends' in text and 'mostly gameplay' in text
    assert 'real build' in text and 'stricter' in text
    assert 'https://partner.steamgames.com/doc/store/trailer' in text
    assert 'still encoding' in text
