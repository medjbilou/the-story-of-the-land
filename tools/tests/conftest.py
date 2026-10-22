import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import pytest


def pytest_addoption(parser):
    parser.addoption("--headed", action="store_true", help="also run the headed-browser checks (opens Chrome for Testing)")


def pytest_configure(config):
    config.addinivalue_line("markers", "headed: needs a real headed browser; runs only with --headed")


def pytest_collection_modifyitems(config, items):
    if config.getoption("--headed"):
        return
    skip = pytest.mark.skip(reason="headed browser check: run with --headed")
    for item in items:
        if "headed" in item.keywords:
            item.add_marker(skip)
