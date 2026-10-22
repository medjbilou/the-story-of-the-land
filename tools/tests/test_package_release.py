"""tools/package_release.py: the itch.io zip is made only after the owner's sensitivity review of every sensitive family
card (spec 5.5; M3 rereview). The gate is Build.ShareableRefusal's, run on the family chapters inside the build."""
import json
import re
import shutil
import zipfile
from pathlib import Path

import package_release
import yaml12

REPO = Path(package_release.REPO)
EVENTS = REPO / "content/palestine/events"


def sensitive_cards():
    """The ids of the real cards that carry a sensitive tag, read straight from the chapters."""
    return sorted(e["id"] for f in EVENTS.glob("*.yaml") for e in yaml12.load(f.read_text(encoding="utf-8"))["events"]
                  if {t.lower() for t in e.get("tags") or []} & package_release.SENSITIVE_TAGS)


def photo_manifest(build, photos=(), shareable=True):
    """The event-photos.json Build.Web writes (Build.PhotoManifestJson): its list is empty."""
    (build / package_release.PHOTO_MANIFEST).write_text(json.dumps({"version": 1, "shareable": shareable, "photos": list(photos)}))


def fake_build(tmp_path, reviewed):
    """A build folder with index.html, the real family chapters copied into StreamingAssets/content and the photo
    manifest; reviewed gives every sensitive card the owner's sign-off (status: sensitivity-reviewed)."""
    build = tmp_path / "Web-brotli"
    (build / "StreamingAssets").mkdir(parents=True)
    (build / "index.html").write_text("<html></html>")
    events = build / "StreamingAssets" / "content" / "palestine" / "events"
    shutil.copytree(EVENTS, events)
    if reviewed:
        for f in events.glob("*.yaml"):
            f.write_text(re.sub(r"(?m)^(    tags: .*)$", r"\1\n    status: sensitivity-reviewed", f.read_text(encoding="utf-8")),
                         encoding="utf-8")
    photo_manifest(build)
    return build


def test_the_real_chapters_have_sensitive_cards():
    cards = sensitive_cards()
    assert "e1948_04_deir_yassin" in cards and "gz_2023_10_ahli" in cards


def test_packaging_is_refused_until_every_sensitive_card_is_reviewed(tmp_path, capsys):
    build, zip_path = fake_build(tmp_path, reviewed=False), tmp_path / "out.zip"
    assert package_release.main(["--build", str(build), "--zip", str(zip_path)]) == 1
    assert not zip_path.exists()
    out = capsys.readouterr().out
    assert "PACKAGE REFUSED" in out
    for e in sensitive_cards():
        assert e in out


def test_packaging_writes_the_zip_once_all_are_reviewed(tmp_path, capsys):
    build, zip_path = fake_build(tmp_path, reviewed=True), tmp_path / "out.zip"
    assert package_release.main(["--build", str(build), "--zip", str(zip_path)]) == 0
    with zipfile.ZipFile(zip_path) as z:
        names = z.namelist()
    assert "index.html" in names                                       # at the zip root, as itch.io needs
    assert "StreamingAssets/content/palestine/events/1947_1949.yaml" in names
    out = capsys.readouterr().out
    assert "RELEASE GATE OK: 7 chapters" in out and "PACKAGED" in out


def chapter(build, text):
    (build / "StreamingAssets/content/palestine/events/test.yaml").write_text(text, encoding="utf-8")


def test_any_sensitive_tag_in_any_case_needs_the_review_and_other_cards_do_not(tmp_path, capsys):
    build = fake_build(tmp_path, reviewed=True)
    chapter(build, "events:\n  - { id: a, tags: [Massacre] }\n  - { id: b, tags: [displacement], status: fact-checked }\n"
                   "  - { id: c, tags: [land], status: draft }\n  - { id: d }\n")
    out = refused(build, tmp_path, capsys)
    assert "for: a, b (" in out


def test_a_build_without_family_chapters_is_refused(tmp_path, capsys):
    build = fake_build(tmp_path, reviewed=True)
    shutil.rmtree(build / "StreamingAssets/content/palestine/events")
    assert "no family chapters" in refused(build, tmp_path, capsys)


def test_an_unreadable_chapter_is_refused(tmp_path, capsys):
    build = fake_build(tmp_path, reviewed=True)
    chapter(build, "events: {")
    assert "cannot read test.yaml" in refused(build, tmp_path, capsys)


# Review I1: the build's own photo manifest is what is checked; it lists no photographs.
def refused(build, tmp_path, capsys):
    zip_path = tmp_path / "out.zip"
    assert package_release.main(["--build", str(build), "--zip", str(zip_path)]) == 1
    assert not zip_path.exists()
    out = capsys.readouterr().out
    assert "PACKAGE REFUSED" in out
    return out


def test_a_build_without_the_photo_manifest_is_refused(tmp_path, capsys):
    build = fake_build(tmp_path, reviewed=True)
    (build / package_release.PHOTO_MANIFEST).unlink()
    assert "event-photos.json" in refused(build, tmp_path, capsys)


def test_an_unreviewed_sensitive_photograph_would_be_refused(tmp_path, capsys):
    build = fake_build(tmp_path, reviewed=True)
    photo_manifest(build, [{"event_id": "x_sensitive", "sensitive": True, "sensitivity_reviewed": False},
                           {"event_id": "y_plain", "sensitive": False, "sensitivity_reviewed": False}])
    out = refused(build, tmp_path, capsys)
    assert "x_sensitive" in out and "y_plain" not in out             # not sensitive: nothing to review


def test_a_dev_build_is_refused(tmp_path, capsys):
    build = fake_build(tmp_path, reviewed=True)
    photo_manifest(build, shareable=False)
    assert "-shareable" in refused(build, tmp_path, capsys)


def test_a_malformed_photo_manifest_is_refused(tmp_path, capsys):
    build = fake_build(tmp_path, reviewed=True)
    for bad in ["{", '{"shareable": true}', '{"shareable": "yes", "photos": []}', '{"shareable": true, "photos": [{"event_id": "x"}]}']:
        (build / package_release.PHOTO_MANIFEST).write_text(bad)
        assert "not a valid photo manifest" in refused(build, tmp_path, capsys)


# Issue #437: Build.Web names the files under Build/ by their content hash, so the names differ from build to build and
# index.html is the only place that says which files belong to this build.
HASHED = ["0a1b2c3d4e5f60718293a4b5c6d7e8f9.loader.js", "11112222333344445555666677778888.framework.js.br",
          "9999aaaabbbbccccddddeeeeffff0000.data.br", "abcdefabcdefabcdefabcdefabcdefab.wasm.br"]


def hashed_build(tmp_path):
    """fake_build with a Build/ folder of hashed names and an index.html that names them, as the template writes it."""
    build = fake_build(tmp_path, reviewed=True)
    loader, framework, data, code = HASHED
    (build / "index.html").write_text(
        f'<html><script src="Build/{loader}"></script><script>var config = {{\n  dataUrl: "Build/{data}",\n'
        f'  frameworkUrl: "Build/{framework}",\n  codeUrl: "Build/{code}",\n  streamingAssetsUrl: "StreamingAssets",\n}};'
        "</script></html>")
    (build / "Build").mkdir()
    for name in HASHED:
        (build / "Build" / name).write_bytes(b"\x00")
    return build


def test_the_build_files_are_read_from_index_html(tmp_path):
    assert package_release.build_files(hashed_build(tmp_path)) == sorted(HASHED)
    assert package_release.build_files(fake_build(tmp_path / "plain", reviewed=True)) == []


def test_the_zip_carries_the_hashed_files_index_html_names(tmp_path, capsys):
    build, zip_path = hashed_build(tmp_path), tmp_path / "out.zip"
    assert package_release.main(["--build", str(build), "--zip", str(zip_path)]) == 0
    with zipfile.ZipFile(zip_path) as z:
        assert sorted(n for n in z.namelist() if n.startswith("Build/")) == sorted("Build/" + n for n in HASHED)


def test_files_of_an_earlier_build_are_left_out_of_the_zip(tmp_path, capsys):
    """A second build into the same folder leaves the earlier build's files beside the new ones under other names."""
    build, zip_path = hashed_build(tmp_path), tmp_path / "out.zip"
    (build / "Build" / "ffffffffffffffffffffffffffffffff.data.br").write_bytes(b"old")
    assert package_release.main(["--build", str(build), "--zip", str(zip_path)]) == 0
    with zipfile.ZipFile(zip_path) as z:
        assert sorted(n for n in z.namelist() if n.startswith("Build/")) == sorted("Build/" + n for n in HASHED)
    assert "LEFT OUT Build/ffffffffffffffffffffffffffffffff.data.br" in capsys.readouterr().out


def test_a_build_missing_a_file_index_html_names_is_refused(tmp_path, capsys):
    build = hashed_build(tmp_path)
    (build / "Build" / HASHED[3]).unlink()
    assert "Build/" + HASHED[3] in refused(build, tmp_path, capsys)
