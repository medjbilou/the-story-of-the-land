import hashlib
import zlib
from pathlib import Path

import numpy as np
import pytest

import heightmap_codec as hc

MAP = Path(__file__).resolve().parents[2] / "unity" / "Assets" / "StreamingAssets" / "map"
# SHA-256 of the raw heightmap.r16 (8,396,802 bytes) shipped before issue #44 packed it; the packed file must decode to it.
RAW_SHA256 = "b6882a77420efa90217cc65a9c353147569094cf16656d4d53c30abe0f41ac9e"


def test_round_trip_is_exact_including_wraparound_deltas():
    rng = np.random.default_rng(44)
    for shape in [(1, 1), (3, 3), (5, 7), (64, 64)]:
        raw = rng.integers(0, 65536, size=shape, dtype=np.uint32).astype("<u2")
        raw.flat[0], raw.flat[-1] = 65535, 0          # the largest jumps, both ways
        packed = hc.encode(raw)
        assert np.array_equal(hc.decode(packed, shape[1], shape[0]), raw)


def test_layout_is_row_deltas_split_into_low_then_high_byte_planes():
    raw = np.array([[1, 3], [0x0102, 0x0101]], dtype="<u2")
    planes = zlib.decompress(hc.encode(raw))
    # deltas per row (mod 65536): [1, 2], [0x0102, 0xFFFF]
    assert planes == bytes([1, 2, 0x02, 0xFF]) + bytes([0, 0, 0x01, 0xFF])


def test_decode_rejects_a_stream_of_the_wrong_size_or_a_corrupt_one():
    packed = hc.encode(np.zeros((3, 3), dtype="<u2"))
    with pytest.raises(ValueError, match="18 bytes after inflating, expected 8"):
        hc.decode(packed, 2, 2)
    with pytest.raises(ValueError):
        hc.decode(packed[:-4] + b"\0\0\0\0", 3, 3)      # adler-32 no longer matches


def test_write_and_read_use_the_packed_file(tmp_path):
    raw = np.arange(12, dtype="<u2").reshape(3, 4) * 5000
    hc.write(tmp_path, raw)
    assert [p.name for p in tmp_path.iterdir()] == [hc.FILE]
    assert np.array_equal(hc.read(tmp_path, {"width": 4, "height": 3}), raw)


def test_the_shipped_heightmap_decodes_to_the_old_raw_file_byte_for_byte():
    import json
    meta = json.loads((MAP / "heightmap.json").read_text())
    raw = hc.read(MAP, meta)
    assert hashlib.sha256(raw.astype("<u2").tobytes()).hexdigest() == RAW_SHA256
    assert not (MAP / "heightmap.r16").exists(), "the raw file is replaced, not kept beside the packed one"
    assert (MAP / hc.FILE).stat().st_size < 0.55 * raw.nbytes


def test_decode_rejects_trailing_bytes_and_a_truncated_stream_like_the_game():
    packed = hc.encode(np.arange(9, dtype="<u2").reshape(3, 3))
    with pytest.raises(ValueError, match="after the zlib stream"):
        hc.decode(packed + b"xx", 3, 3)
    with pytest.raises(ValueError):
        hc.decode(packed[:-5], 3, 3)
