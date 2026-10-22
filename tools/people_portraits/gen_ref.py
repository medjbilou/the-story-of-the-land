"""usage: GEMINI_API_KEY=... python3 tools/people_portraits/gen_ref.py <refdir> <id> [<id>...]  -- redraws <refdir>/<id>.jpg (a
reference photo of the person) as a portrait in the game's style (gen.py's BASE), to out/<id>.png. The photo is a likeness
reference only: the drawing is new, and ASSETS.yaml records the reference's source."""
import base64, json, os, sys, urllib.request, pathlib, time
here = pathlib.Path(__file__).parent
BASE = ("Square illustration for an educational history game. Style: hand-drawn ink and engraving-like linework with "
        "soft watercolor wash, limited palette of deep navy blue, warm muted gold and cream paper; plain cream paper "
        "background with nothing behind the subject (no branches, no wreath, no pattern). No weapons, no flags, no logos, "
        "no text or lettering, no frame, no heroic lighting. ")
ASK = ("Redraw the person in this reference photo as a new head and shoulders portrait in exactly that style, three-quarter "
       "view, calm neutral documentary expression. Keep the person's likeness (face shape, hair, facial hair, glasses) but "
       "leave out everything else in the photo: background, other people, text, logos, watermarks, microphones, weapons.")
key = os.environ["GEMINI_API_KEY"]; ref = pathlib.Path(sys.argv[1]); (here / "out").mkdir(exist_ok=True)
for fid in sys.argv[2:]:
    img = base64.b64encode((ref / f"{fid}.jpg").read_bytes()).decode()
    body = {"contents": [{"parts": [{"inlineData": {"mimeType": "image/jpeg", "data": img}}, {"text": BASE + ASK}]}],
            "generationConfig": {"responseModalities": ["IMAGE"], "imageConfig": {"aspectRatio": "1:1"}}}
    for attempt in range(2):
        try:
            req = urllib.request.Request("https://generativelanguage.googleapis.com/v1beta/models/gemini-3-pro-image:generateContent",
                                         json.dumps(body).encode(), {"Content-Type": "application/json", "x-goog-api-key": key})
            parts = json.load(urllib.request.urlopen(req, timeout=240))["candidates"][0]["content"]["parts"]
            out = next(p for p in parts if "inlineData" in p)
            (here / "out" / f"{fid}.png").write_bytes(base64.b64decode(out["inlineData"]["data"])); print("ok", fid, flush=True); break
        except Exception as e:
            print("retry", fid, type(e).__name__, str(e)[:120], flush=True); time.sleep(5)
    else:
        print("FAIL", fid)
