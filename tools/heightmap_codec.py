"""The packed heightmap file, heightmap.r16z: lossless, about half the size of the raw 16-bit grid (issue #44).

The game reads it at boot (HeightmapCodec.cs decodes the same layout); every tool that needs the elevations reads it here.
Layout: one zlib stream (RFC 1950, so it carries an Adler-32 of its contents) of the grid's row deltas split into byte
planes. Each row (row 0 = north) is replaced by its differences d[x] = h[x] - h[x-1] mod 65536 (d[0] = h[0]); the low
bytes of all deltas come first, then all the high bytes. Smooth terrain gives small deltas, so the high-byte plane is
mostly 0x00/0xFF and deflate packs both planes far better than the raw grid (8.4 MB -> about 4.1 MB). Decoding is an
inflate, an interleave and a running sum per row, and returns the original grid bit for bit."""
import zlib
from pathlib import Path

import numpy as np

FILE = "heightmap.r16z"


def encode(raw: np.ndarray) -> bytes:
    """Packs an (h, w) uint16 grid."""
    grid = np.asarray(raw, dtype="<u2")
    if grid.ndim != 2:
        raise ValueError(f"a heightmap is a 2-D grid, got shape {grid.shape}")
    delta = grid.copy()
    delta[:, 1:] = grid[:, 1:] - grid[:, :-1]          # uint16 arithmetic wraps mod 65536
    planes = delta.astype("<u2").view(np.uint8).reshape(-1, 2).T   # row 0: low bytes, row 1: high bytes
    return zlib.compress(planes.tobytes(), 9)


def decode(data: bytes, width: int, height: int) -> np.ndarray:
    """Unpacks to an (height, width) little-endian uint16 grid; ValueError on a corrupt stream or the wrong size."""
    d = zlib.decompressobj()
    try:
        planes = d.decompress(data)
    except zlib.error as e:
        raise ValueError(f"{FILE}: corrupt stream ({e})") from e
    if not d.eof:
        raise ValueError(f"{FILE}: the zlib stream ends early")
    if d.unused_data:
        raise ValueError(f"{FILE}: {len(d.unused_data)} bytes after the zlib stream")   # the game rejects them too
    n = width * height
    if len(planes) != 2 * n:
        raise ValueError(f"{FILE}: {len(planes)} bytes after inflating, expected {2 * n}")
    b = np.frombuffer(planes, dtype=np.uint8)
    delta = (b[:n].astype(np.uint16) | (b[n:].astype(np.uint16) << 8)).reshape(height, width)
    return np.cumsum(delta, axis=1, dtype=np.uint16).astype("<u2")   # the running sum wraps like the encoder


def write(map_dir: Path, raw: np.ndarray) -> Path:
    path = Path(map_dir) / FILE
    path.write_bytes(encode(raw))
    return path


def read(map_dir: Path, meta: dict) -> np.ndarray:
    """The grid of map_dir's heightmap, sized by its heightmap.json `meta`."""
    return decode((Path(map_dir) / FILE).read_bytes(), meta["width"], meta["height"])
