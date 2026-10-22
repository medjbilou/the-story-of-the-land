#!/usr/bin/env python3
"""The web player's narration: an MP3 copy of every intro in unity/Assets/StreamingAssets/narration (Unity's web player
loads no Ogg Vorbis; Narrator.Files). Needs ffmpeg with libmp3lame. Same sample rate and channels as the .ogg, VBR
quality 5, no metadata. Run again after any intro is rendered anew.
    python3 tools/narration_web.py           # write the missing or older .mp3 files
    python3 tools/narration_web.py --check   # exit 1 when an .ogg has no .mp3
"""
import subprocess
import sys
from pathlib import Path

DIR = Path(__file__).resolve().parents[1] / "unity/Assets/StreamingAssets/narration"


def main() -> int:
    check = "--check" in sys.argv
    missing = []
    for ogg in sorted(DIR.glob("*.ogg")):
        mp3 = ogg.with_suffix(".mp3")
        if mp3.exists() and mp3.stat().st_mtime >= ogg.stat().st_mtime:
            continue
        if check:
            missing.append(mp3.name)
            continue
        subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", str(ogg), "-map_metadata", "-1", "-codec:a", "libmp3lame",
                        "-q:a", "5", str(mp3)], check=True)
        print("wrote", mp3.name)
    for m in missing:
        print("ERROR missing or older than its .ogg:", m, file=sys.stderr)
    return 1 if missing else 0


if __name__ == "__main__":
    sys.exit(main())
