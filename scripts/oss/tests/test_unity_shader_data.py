"""ShaderData has two subshader counts. GetSerializedSubshader(i) indexes the serialized subshaders, so a loop over it must
stop at SerializedSubshaderCount. SubshaderCount can be larger: on Windows (Direct3D 12) the terrain shader reports more
subshaders than it serializes, and TerrainShaderTests logged "Serialized Subshader index is incorrect: 2" (#406). On macOS
the two counts match, so only this source check catches the mistake before a Windows run does."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
LOOP = re.compile(r"for\s*\(\s*int\s+(\w+)\s*=\s*0\s*;\s*\1\s*<\s*[\w.]+\.SubshaderCount\s*;")


def wrong_bounds(text: str) -> list[int]:
    """Line numbers of loops bounded by SubshaderCount whose next lines call GetSerializedSubshader with the loop variable."""
    found = []
    for m in LOOP.finditer(text):
        body = text[m.end():m.end() + 400]
        if re.search(r"GetSerializedSubshader\(\s*" + m.group(1) + r"\s*\)", body):
            found.append(text.count("\n", 0, m.start()) + 1)
    return found


def test_the_check_flags_the_406_loop_and_passes_the_fixed_one():
    loop = "for (int s = 0; s < data.{0}; s++)\n{{\n    var sub = data.GetSerializedSubshader(s);\n}}"
    assert wrong_bounds(loop.format("SubshaderCount")) == [1]
    assert wrong_bounds(loop.format("SerializedSubshaderCount")) == []


def test_serialized_subshader_loops_stop_at_the_serialized_count():
    bad = [f"{p.relative_to(ROOT)}:{line}" for p in sorted((ROOT / "unity/Assets").rglob("*.cs"))
           for line in wrong_bounds(p.read_text(encoding="utf-8"))]
    assert bad == [], "loop over GetSerializedSubshader with SerializedSubshaderCount, not SubshaderCount: " + ", ".join(bad)
