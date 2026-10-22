"""Unity references that only fail inside Unity (a C# compile passes them): shader #include targets, Shader.Find
names, and the UXML stylesheets and custom element types. A rename that misses one breaks a shader or a screen at run
time, not at compile time."""
import json
import re
from pathlib import Path

UNITY = Path(__file__).resolve().parents[2] / "unity"
ASSETS = UNITY / "Assets"
INCLUDE = re.compile(r'#include(?:_with_pragmas)?\s+"([^"]+)"')


def files(*patterns: str) -> list[Path]:
    return sorted(p for pattern in patterns for p in ASSETS.rglob(pattern))


def test_every_shader_include_resolves():
    missing = []
    for src in files("*.shader", "*.hlsl", "*.cginc", "*.compute"):
        for target in INCLUDE.findall(src.read_text(encoding="utf-8", errors="replace")):
            if target.startswith("Packages/"):
                continue   # Unity's packages, not in the repository
            path = UNITY / target if target.startswith("Assets/") else src.parent / target
            if not path.is_file():
                missing.append(f"{src.relative_to(UNITY)}: {target}")
    assert missing == []


def test_every_shader_graph_custom_function_file_exists():
    guids = {p.read_text(encoding="utf-8").split("guid: ", 1)[1].split()[0]: p for p in files("*.hlsl.meta")}
    for graph in files("*.shadergraph"):
        for guid in re.findall(r'"m_FunctionSource":\s*"([0-9a-f]{32})"', graph.read_text(encoding="utf-8")):
            assert guid in guids, f"{graph.name}: no .hlsl with guid {guid}"


def declared_shaders() -> set[str]:
    names = {m for src in files("*.shader") for m in re.findall(r'^\s*Shader\s+"([^"]+)"', src.read_text(encoding="utf-8"), re.M)}
    for graph in files("*.shadergraph"):
        m = re.search(r'"m_Path":\s*"([^"]*)"', graph.read_text(encoding="utf-8"))   # the graph's own menu path
        names.add(f"{m.group(1)}/{graph.stem}" if m and m.group(1) else graph.stem)
    return names


def test_every_shader_find_names_a_shader_in_the_repository():
    declared = declared_shaders()
    found = {n for src in files("*.cs") for n in re.findall(r'Shader\.Find\("([^"]+)"\)', src.read_text(encoding="utf-8"))}
    unity_builtin = {n for n in found if n.startswith(("Universal Render Pipeline/", "Hidden/", "Sprites/", "UI/", "Unlit/"))}
    assert found - unity_builtin <= declared, sorted(found - unity_builtin - declared)


def test_uxml_stylesheets_and_custom_elements_resolve():
    sources = "\n".join(p.read_text(encoding="utf-8") for p in files("*.cs"))
    for uxml in files("*.uxml"):
        text = uxml.read_text(encoding="utf-8")
        for src in re.findall(r'<Style src="([^"]+)"', text):
            path = UNITY / src.split("?")[0].replace("project://database/", "") if src.startswith("project://") else uxml.parent / src
            assert path.is_file(), f"{uxml.name}: {src}"
        for full in sorted(set(re.findall(r"<((?:[A-Z]\w*\.)+[A-Z]\w*)[\s/>]", text))):
            if full.startswith(("UnityEngine.", "UnityEditor.")):
                continue
            namespace, name = full.rsplit(".", 1)
            assert re.search(rf"namespace {re.escape(namespace)}\b[\s\S]*\bclass {name}\b", sources), f"{uxml.name}: {full}"


def test_asmdef_references_name_assemblies_that_exist():
    defs = {json.loads(p.read_text(encoding="utf-8"))["name"]: json.loads(p.read_text(encoding="utf-8")) for p in files("*.asmdef")}
    for name, d in defs.items():
        for ref in d.get("references", []):
            assert ref in defs or not ref.startswith("StoryOfTheLand"), f"{name}: {ref}"
        for dll in d.get("precompiledReferences", []):   # the plugin DLLs are build output, so only their names are checked
            assert dll in ("StoryOfTheLand.Sim.dll", "YamlDotNet.dll", "nunit.framework.dll"), f"{name}: {dll}"


def test_every_uss_url_names_a_file_in_the_project():
    for uss in files("*.uss", "*.tss"):
        for target in re.findall(r'url\("project://database/([^"?]+)', uss.read_text(encoding="utf-8")):
            assert (UNITY / target).is_file(), f"{uss.name}: {target}"


def test_the_arabic_credits_are_set_in_amiri_bold_and_regular():
    uss = (ASSETS / "UI/StoryOfTheLand.uss").read_text(encoding="utf-8")
    heading = uss.index(".lang-ar .credits-heading {")
    assert "Fonts/Amiri/Amiri-Bold.ttf" in uss[heading:uss.index("}", heading)]
    body = uss.index(".lang-ar .credits-text, .lang-ar .credits-item {")
    rule = uss[body:uss.index("}", body)]
    assert "Fonts/Amiri/Amiri-Regular.ttf" in rule and "-unity-text-generator: advanced" in rule
    assert heading > uss.index(".lang-ar .section-title,"), "after the .lang-ar block, so it wins"
