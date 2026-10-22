# The Story of the Land — plan

The game tells the history of Palestine from the Palestinian perspective: a family, its village, and the land. Its
content is the Palestinian story researched in `../reports`, `../research_notes` and `../content`. Licences: see
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

## Engine and tooling

Engine (`sim/` deterministic step, Rng, SaveGame, Cause log, EventSystem), map pipeline and data, heightmap and terrain
shaders, settlement kits and the CC0 models (including the Arab village houses), UI Toolkit theme (`StoryOfTheLand.uss`,
`StoryOfTheLandTheme.tss`, ornamented frames, EB Garamond), camera, tilt-shift, minimap, `-shot` capture mode, tests and
golden harness, `tools/` (content validation, bakes).

## The game

| What | Where |
|---|---|
| The player: a Palestinian family and its village community | `docs/palestine-model.md` |
| Events: the audited chapters, converted to the event schema | `../content/events_*.tsv` → `content/palestine/events/` |
| The goal: keep the claim alive (memory, documents, sumud, return) | `docs/palestine-model.md`, "Still to do" |
| Family resources: Land, Home, Family, Documents, Livelihood, Memory, Sumud, Community, Safety, Solidarity | `content/palestine/model.yaml` |
| Narration (Habibi/Chatterbox) and openly licensed images | `unity/Assets/StreamingAssets/narration/`, `ASSETS.yaml` |
| Arabic interface: `strings.ar.yaml` and Noto Naskh Arabic (OFL), right to left | `unity/Assets/UI/` |
| The 1948 village fates as a time layer on the map | `../content/village_fates.tsv` → `content/palestine/fates.yaml` |

## Milestones

Code comments and tests cite these by label.

1. F1 — the engine on Unity 6000.6.6f1 (`GetHashCode` keys the per-object caches; no Rider or Unity-MCP packages;
   `Build.MacMono`).
2. F2 — the game's name, the credits screen and the licence notices.
3. F3 — one boot path: the start screen and every automated run start the family game, and a build ships only the
   content and media in `ContentManifest`.
4. F4 — family/village model in `sim/`, converter from our TSV chapters to the event YAML, tests.
5. F5 — Arabic UI strings and font, village fates on the 3D map, narration hook (`docs/f5-unity-integration.md`).
6. F6 — polish to the standard of big strategy games (owner's goal, 2026-10-19):
   - Graphics and textures: higher-detail terrain, materials and Palestinian village kits (stone houses, olive and
     citrus groves, terraces), lighting and post-processing tuned per quality tier; no AI art, no real people.
   - Pause menu (Esc): resume, save, load, settings, credits, main menu, quit; Arabic and English.
   - Settings menu: display (resolution, window mode, vsync, frame cap), graphics presets and individual toggles,
     audio (master, narration, effects), language, accessibility (text size, reduced motion), controls; applied
     live and saved.
   - Performance: smooth at the highest frame rate the machine allows; measured with the frame-profile tool
     (`-vprofile`), budgets per tier, no GC spikes in the turn loop.
   - Status of the UI part (F6-UI, 2026-10-20): the pause menu is `PausePresenter` (Escape or a controller's Start; save
     and load in three slots beside the family autosave, `SaveStore.Slot`, same format; Settings, How to play, Credits;
     Main menu and Quit after a confirm, the game saved first; no Quit on the web). Settings is tabbed (`SettingsPresenter`:
     Display, Graphics, Audio, Language, Accessibility, Controls), every row applied at once and kept in settings.txt. The
     top bar is one row, icon first, from 1800 logical px (16:9 1920x1080 and 16:10). Tests: `PauseMenuTests`,
     `SettingsTabsTests`; captures: `-testFilter F6Captures` with `STORY_F6_DIR`; Arabic audit:
     `../audits/arabic_f6_ui.md`. Enter (and the keypad's) ends the turn when nothing is open and nothing has the focus
     (`GameRoot.EndTurnKey`). Graphics: the presets Automatic, Low, Medium (High's tier, hard shadows, 0.9 render scale,
     fast edge smoothing) and High, and the rendering side's knobs (`GraphicsQuality`: shadows, render scale, edge
     smoothing, terrain detail, haze); no Ultra, bloom or scenery density (nothing above High to offer; bloom priced and
     dropped). Reduce motion (Accessibility) stills the water and the trains (`GraphicsQuality.ReducedMotion`); the camera
     has no easing to calm. Left out on purpose: an effects volume (add when the game has sound effects).
7. F7 — the Palestinian-perspective writing of every chapter and biography, music, the occupation and Gaza's
   destruction on the map, and checked footage.
