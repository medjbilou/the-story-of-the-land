import pin_actions

SHA = "a" * 40


def test_tags_are_replaced_by_sha_with_the_tag_kept_as_a_comment():
    text = "steps:\n  - uses: actions/checkout@v4\n  - uses: astral-sh/setup-uv@v6\n    with:\n      version: x\n"
    out = pin_actions.pin_text(text, lambda repo, ref: SHA)
    assert f"  - uses: actions/checkout@{SHA} # v4\n" in out
    assert f"  - uses: astral-sh/setup-uv@{SHA} # v6\n" in out


def test_already_pinned_lines_are_left_alone():
    text = f"  - uses: actions/checkout@{SHA} # v4\n"
    assert pin_actions.pin_text(text, lambda repo, ref: (_ for _ in ()).throw(AssertionError)) == text
