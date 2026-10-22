# Developer setup (verified 2026-10-09 on macOS 27.0.1, arm64)

- .NET SDK 10.0.401: `brew install dotnet`
- Unity Hub 3.22.2: `brew install --cask unity-hub`. Sign in and activate a Personal license.
- Unity Editor 6000.3.25f1 (6.3 LTS), changeset e1dba0a9aba4, installed through the Hub CLI:
  `"/Applications/Unity Hub.app/Contents/MacOS/Unity Hub" -- --headless install -v 6000.3.25f1 -c e1dba0a9aba4 -a arm64 -m webgl -m mac-il2cpp`
  Modules: Web Build Support, Mac Build Support (IL2CPP).
- `export UNITY="/Applications/Unity/Hub/Editor/6000.3.25f1/Unity.app/Contents/MacOS/Unity"`
- Python tools: `uv` (0.12.5 verified). Dependencies are declared inline in each tools/*.py script.
- ffmpeg (Homebrew) is used for flythrough videos.

## After cloning
- Run `uv run tools/build_sim_for_unity.py` before opening `unity/`. The sim DLLs are build output and are git-ignored.
  The helper works on Windows, macOS and Linux; with Python already installed, `python tools/build_sim_for_unity.py`
  also works without `uv` (use `python3` if that is your interpreter's command).
  `tools/build_sim_for_unity.sh` is a thin wrapper that runs the same Python script, for older plans and scripts.

## Without Unity (Linux, a container, a sandbox)

Unity, a Unity licence and macOS are only needed for `unity/`. The sim, the content and the policy checks run headless, also in the dev container (`.devcontainer/README.md`, for a local container or a Codespace). These are the commands CI runs (`.github/workflows/ci.yml`), with the .NET SDK 10.0.401 and `uv`, and the helpers CI does not run (CI job "none"):

| Check | Command | CI job |
|---|---|---|
| Sim and content tests (0 warnings) | `dotnet test sim/StoryOfTheLand.sln -c Release` | Sim (dotnet test) |
| Repository policy | `uv run scripts/oss/check_repo.py` | Repo policy |
| Policy checker tests | `uv run --with pytest==9.1.1 --with pyyaml==6.0.3 --with requests==2.32.5 pytest scripts/oss/tests -q` | Repo policy |
| Data tool tests (one runs the sim's release gate, so they need the .NET SDK too) | `uv run --with pytest==9.1.1 --with rasterio==1.5.2 --with numpy==2.5.3 --with pyyaml==6.0.3 --with pillow==12.3.0 --with requests==2.32.5 --with pyproj==3.8.0 pytest tools/tests -q` | Tools (pytest) |
| Rule IDs: every rule has a citing test, every cited ID exists (also run by the policy checker tests) | `uv run scripts/oss/rule_ids.py` | Repo policy |
| Every content source with its check date and age (`--older-than DAYS`) | `uv run scripts/source_report.py` | none (helper) |
| Dead relative links in Markdown files (also run by the policy checker tests) | `uv run scripts/oss/md_links.py` | Repo policy |
| Every script this table names exists (also run by the policy checker tests) | `uv run scripts/oss/dev_setup_commands.py` | Repo policy |
| Every place a record id is read, in content and code | `uv run scripts/id_references.py <id>` | none (helper) |
| Changelog line | `uv run scripts/oss/changelog_check.py --base origin/main` | Changelog |
| Only a person signs a row off (`reviewed_by` and `reviewed_on`) | `uv run scripts/oss/reviewed_by_guard.py --base origin/main` | Changelog |

`scripts/check.sh` runs all the checks in one go, cheapest first, plus a sim build with warnings as errors. It stops at the first failure and shows the end of its output. It takes the base ref as its argument (default `origin/main`); set `CHECK_NO_CHANGELOG=1` if the PR has the `no-changelog` label.

The label `needs-unity` marks an issue you cannot finish without Unity. Its acceptance needs the Unity editor, a player build or the WebGL check. The label `area: unity` is different: it says which code the issue touches, not what you must have to finish it. An issue without `needs-unity` can be finished with the commands above.

Not possible without Unity and not in CI: the Unity EditMode tests, player builds, `tools/web_check.py` and the visual profiles below. If your change touches `unity/`, or `sim/` code that Unity compiles, say in the PR that you could not run them. The maintainer runs them (`CONTRIBUTING.md`, "Unity EditMode tests are not in CI").

## Builds and browser checks (M3)
- Web build: `"$UNITY" -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -buildTarget WebGL -executeMethod StoryOfTheLand.Game.EditorTools.Build.Web -compression disabled -logFile "$PWD/unity-build-web.log"`
  (`-compression` is `disabled`, `gzip` or `brotli`; output in `unity/Builds/Web-<compression>/`, about 2 minutes).
- macOS IL2CPP build: `"$UNITY" -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -buildTarget OSXUniversal -executeMethod StoryOfTheLand.Game.EditorTools.Build.Mac -logFile "$PWD/unity-build-mac.log"`
  (output `unity/Builds/Mac/TheStoryOfTheLand.app`).
- Browser check: `uv run tools/web_check.py unity/Builds/Web-disabled --query "golden&smoke=5"` with the three full `--expect`
  values pinned in `docs/determinism.md` (both goldens and the smoke line).
  It uses Chrome for Testing from the Playwright cache (`~/Library/Caches/ms-playwright/chromium-1228` on this Mac;
  elsewhere `npx playwright install chromium`).
- Page flags: `?golden` (golden check), `?smoke=N` (play N turns through the UI; bare `?smoke` = 5, `?smoke=0` = off),
  `?profile` (terrain profile), `?camlog` (log the camera distance), `?bootlog` (each boot phase's timing) and `?navlog`
  (every UI pointer and submit event, screen toggle and map click, with its frame), `?vprofile` (the visual frame-time
  profile per feature), `?shot` (screenshots at the promo poses as `SHOT` console lines) and `?kitscale=N`, `?housescale=N`, `?treescale=N` (the settlement kits' symbolic scale for city blocks, houses and trees, clamped to 5–200; defaults 60, 100, 100). In the editor, `MapCapture.Kits` takes `-kitScale`, `-houseScale` and `-treeScale`. Desktop players take `-golden`,
  `-smoke N`, `-profile`, `-camlog`, `-bootlog`, `-navlog`, `-vprofile` and `-shot`. The console also shows `GAME OK <date> turn <n> hash <h>` at start and `TURN <n> <date> hash <h>` per turn.
- First card in normal mode (#909): `?firstcard` keeps the start screen on, presses New game and the picker's default
  chapter and village at once, and logs `FIRST CARD OK event=<id> wait=<ms> ms budget=<ms> ms`, or `FIRST CARD FAIL`
  after the budget. `tools/web_check.py <build> --startscreen` runs it on a fresh profile under the machine lock's
  usual wrapper (`uv run tools/machine_lock.py --exclusive -- uv run tools/web_check.py <build> --startscreen`), and again
  with `--query lang=ar`.
- Visual checks (docs/visual-v1.md): measure only under the machine lock, which also waits for any open Unity editor:
  `uv run tools/machine_lock.py --exclusive --wait-unity -- uv run tools/web_check.py <build> --query vprofile --expect "PROFILE visual done" --timeout 1800 --stall 120`
  (headless), `... -- uv run tools/headed.py profile --build <build> --dpr 1` (headed; `--dpr 2` is the
  fill-rate stress proxy) and `... -- uv run tools/headed.py boot --build <build> --runs 3 --baseline <ms>
  --baseline-first-frame <ms>` (the boot budget: two spans, default allowance max(300 ms, 50%)); all of them fix the
  viewport to 1920x1080 over CDP and fail on another `res=`; wrap Unity
  batch commands in `uv run tools/machine_lock.py --exclusive -- ...`; `tools/art_checkpoint.sh [--no-build] <name>` for the
  owner's side-by-side images.
- Quality tiers: `uv run tools/machine_lock.py --exclusive -- "$UNITY" -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -executeMethod StoryOfTheLand.Game.EditorTools.QualityTiers.Apply`
  writes the decisions in `QualityTierValues` to the URP assets (docs/visual-v1.md "Tier decisions"). The built macOS
  player (`unity/Builds/Mac/TheStoryOfTheLand.app`) does not run in the background (`runInBackground` is off): its `-vprofile`
  waits at `GAME OK` until that player's window is in front (`open -a unity/Builds/Mac/TheStoryOfTheLand.app` brings it
  forward). Never script, `open -a` or AppleScript the Unity editor itself.
- A player build rewrites some URP asset and player-setting fields as build-time caches; drop them afterwards with
  `git checkout -- unity/Assets/Settings unity/ProjectSettings/ProjectSettings.asset`. That restores the index, so stage
  intended settings changes first (`QualityTiers.RestoreAll` writes them), and never run it while another agent shares the
  worktree.
- Play a build locally: `python3 -m http.server 8000 --directory unity/Builds/Web-disabled`, then open http://localhost:8000/.
  A gzip or Brotli build needs `Content-Encoding`; serve it with `tools/web_check.py` (usage in its header).
