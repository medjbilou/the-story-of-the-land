"""Partial #12, ported to the Arabic UI: translation data must keep UiText's format contract."""
from pathlib import Path
import re
from collections import Counter
import yaml12
ROOT = Path(__file__).resolve().parents[2]
UI = ROOT / 'unity/Assets/UI'


def strings(name):
    return yaml12.load((UI / name).read_text(encoding='utf-8'))


def tokens(v):
    return Counter(re.findall(r'\{[^{}]+\}', v))


PLURAL = ('one', 'two', 'few', 'many', 'other')   # UiText.Plural's forms, the CLDR Arabic rule


def plural(key):
    """(base, form) for a counted key's form ("city.groups.two"), else (key, None)."""
    base, _, form = key.rpartition('.')
    return (base, form) if form in PLURAL else (key, None)


def test_arabic_keys_match_english_and_every_value_is_nonempty():
    # Arabic has every English key; its only extra keys are plural forms of an English key (UiText.Plural falls back to
    # the base key, so English needs only key and key.one).
    en, ar = strings('strings.en.yaml'), strings('strings.ar.yaml')
    assert en.keys() <= ar.keys()
    for key in ar.keys() - en.keys():
        base, form = plural(key)
        assert form and base in en, key
    assert all(isinstance(v, str) and v.strip() for v in ar.values())


def test_every_arabic_counted_key_has_all_five_forms():
    en, ar = strings('strings.en.yaml'), strings('strings.ar.yaml')
    counted = {plural(k)[0] for k in ar.keys() - en.keys()}
    assert len(counted) == 4   # event.more, hud.say_points, picker.count and skip.passed
    for base in counted:
        assert base in ar and all(f'{base}.{f}' in ar for f in PLURAL), base


def test_arabic_keeps_complete_format_tokens_and_logical_text_order():
    en, ar = strings('strings.en.yaml'), strings('strings.ar.yaml')
    for key in ar:
        base, form = plural(key)
        want = tokens(en[key] if key in en else en[base])
        if form in ('one', 'two'):
            # The count is said in words ("شهرين", not "2 شهرين"): the form may drop that one token, and no other.
            assert tokens(ar[key]) <= want and sum((want - tokens(ar[key])).values()) <= 1, key
        else:
            assert tokens(ar[key]) == want, key
        assert not any(c in ar[key] for c in '\u202a\u202b\u202c\u202d\u202e'), key


def test_an_order_refusal_shows_no_raw_id_or_exception_text():
    # #622 finding 1: the toast showed a content id ("Unknown policy 'curfew'") or the exception's message after
    # "Not done:". The kind word stays ({0}, #595); the id and the message go to the log only (SimBridge).
    for name in ('strings.en.yaml', 'strings.ar.yaml'):
        s = strings(name)
        assert re.findall(r'\{[^{}]+\}', s['order.unknown']) == ['{0}'], name
        assert re.findall(r'\{[^{}]+\}', s['order.failed']) == [], name


def test_the_top_bar_and_start_labels_are_in_sentence_case():
    # #622 finding 2: the Title Case labels match the file's sentence case, and so do the lines that name the two
    # buttons, written as the button shows them.
    en = strings('strings.en.yaml')
    assert [en[k] for k in ('hud.new_game', 'hud.end_turn', 'start.new_game')] == ['New game', 'End turn', 'New game']
    for key, value in en.items():
        assert 'End Turn' not in value and 'New Game' not in value, key
    uxml = (UI / 'Game.uxml').read_text(encoding='utf-8')
    for title in ('New Game', 'End Turn'):
        assert f'text="{title}"' not in uxml, title

