"""usage: GEMINI_API_KEY=... python3 tools/people_portraits/gen.py <id> [<id>...]  -- one picture per figure (spec.py) in the game's style via Gemini image, to out/<id>.png."""
import base64, json, os, sys, urllib.request, pathlib, time
here = pathlib.Path(__file__).parent
sys.path.insert(0, str(here)); from spec import SPEC
key = os.environ["GEMINI_API_KEY"]
BASE = ("Square illustration for an educational history game. Style: hand-drawn ink and engraving-like linework with "
        "soft watercolor wash, limited palette of deep navy blue, warm muted gold and cream paper; plain cream paper "
        "background with nothing behind the subject (no branches, no wreath, no pattern). No weapons, no flags, no logos, "
        "no text or lettering, no frame, no heroic lighting. ")
KIND = {"p": "Head and shoulders portrait, three-quarter view, calm neutral documentary expression. Subject: ",
        "s": "A quiet still life or place, with no people in it. Subject: "}
(here / "out").mkdir(exist_ok=True)
for fid in sys.argv[1:]:
    kind, subject = SPEC[fid]
    body = {"contents": [{"parts": [{"text": BASE + KIND[kind] + subject + "."}]}],
            "generationConfig": {"responseModalities": ["IMAGE"], "imageConfig": {"aspectRatio": "1:1"}}}
    for attempt in range(3):
        try:
            req = urllib.request.Request("https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image:generateContent",
                                         json.dumps(body).encode(), {"Content-Type": "application/json", "x-goog-api-key": key})
            d = json.load(urllib.request.urlopen(req, timeout=240))
            parts = d["candidates"][0]["content"]["parts"]
            img = next(p for p in parts if "inlineData" in p)
            (here / "out" / f"{fid}.png").write_bytes(base64.b64decode(img["inlineData"]["data"])); print("ok", fid); break
        except Exception as e:
            print("retry", fid, type(e).__name__, str(e)[:120]); time.sleep(5)
    else:
        print("FAIL", fid)
