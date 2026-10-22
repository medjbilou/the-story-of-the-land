"""Text files check out with LF on every platform (#405). Git for Windows defaults to core.autocrlf=true, which checked
a data file and its Unity asset out with CRLF. The import and Unity then wrote them back with LF, so the import's test
failed and the files showed as modified. .gitattributes pins LF;
these tests read it through git, so they fail wherever the rule is missing, not only on Windows."""
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
FIGURE = "unity/Assets/Art/Collection/figure-arab-villager-v1/figure-arab-villager-v1_basecolor.jpg"


def git(*args: str) -> str:
    return subprocess.run(["git", "-C", str(ROOT), *args], capture_output=True, check=True, text=True).stdout


def test_text_files_check_out_with_lf_whatever_core_autocrlf_says():
    files = ["unity/Assets/UI/Fonts/SOURCE.txt", "unity/Assets/UI/StoryOfTheLandPanel.asset", f"{FIGURE}.meta",
             "unity/Assets/Materials/Terrain.mat", "content/palestine/model.yaml", "sim/StoryOfTheLand.sln"]
    out = git("check-attr", "eol", "--", *files)
    assert out.splitlines() == [f"{f}: eol: lf" for f in files]


def test_no_tracked_file_is_stored_with_crlf():
    # With `text=auto` git would convert a CRLF blob on the next commit that touches it; none should be waiting.
    crlf = [line.split("\t", 1)[1] for line in git("ls-files", "--eol").splitlines() if line.startswith("i/crlf")]
    assert crlf == []


def test_images_stay_binary():
    eol = git("ls-files", "--eol", "--", FIGURE).split()
    assert eol[0] == "i/-text"
