# Licences and third-party notices

The Story of the Land (حكاية الأرض), by Mohamed Jbilou. This file is the project's one licence and attribution
notice: which licence covers each part of the repository, and the notices third-party material requires. The licence texts are in
[`LICENSE`](LICENSE) and [`LICENSES/`](LICENSES/); the data-source attribution texts are also in [`NOTICE`](NOTICE).
Per-file provenance is in [`ASSETS.yaml`](ASSETS.yaml).

## Third-party works

Parts of the code, art and map data are third-party work, modified:

- **Copyright:** Copyright (c) 2026 Eyal Golan.
- **Code:** MIT License ([`LICENSE`](LICENSE)). Modified.
- **Art, interface and shaders:** Creative Commons Attribution 4.0 International (CC BY 4.0),
  https://creativecommons.org/licenses/by/4.0/ ([`LICENSES/CC-BY-4.0.txt`](LICENSES/CC-BY-4.0.txt)). Modified.
- **Content and map data:** Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0),
  https://creativecommons.org/licenses/by-sa/4.0/ ([`LICENSES/CC-BY-SA-4.0.txt`](LICENSES/CC-BY-SA-4.0.txt)).
  Modified. Material derived from it, including the map data and the localities, stays under CC BY-SA 4.0.

Other third-party material:

| Material | Where | Licence | Obligation |
|---|---|---|---|
| 3D models by Tal Heuberger | `unity/Assets/Art/Settlements/Prototype/Models/`, `unity/Assets/Art/Collection/` | CC0 1.0 | None; credited with thanks |
| Copernicus DEM GLO-30 (derived heightmap, hex elevations) | `unity/Assets/StreamingAssets/map/`, `content/map/hexes.json` | Copernicus DEM licence | Keep the notice below with the data, in this file and in `NOTICE` |
| CHELSA V2.1 bio12 | `content/map/precip_bio12.*` | CC0 1.0 | None; the citation in `NOTICE` is requested |
| IMS rain-gauge normals (data.gov.il dataset 481) | `content/map/precip_stations.json` | "Other (Open)" (data.gov.il) | Attribution in `NOTICE`; terms under owner review (decision D6) |
| Archive Genocide clip index (the address, archive and original post of each footage clip the owner signed off) | `content/palestine/footage.yaml` | CC BY 4.0 (data), https://creativecommons.org/licenses/by/4.0/ | Credit "Archive Genocide" with a link to archivegenocide.com: the footage viewer shows it on every clip. The footage itself belongs to its authors: the desktop game streams it and never ships or caches a copy |
| Occupation map data: OCHA oPt (the wall, January 2018; Gaza's access-restricted area, October 2023), the Palestinian Ministry of Planning (West Bank village boundaries; the Oslo areas) and the Gaza Housing Council (municipal boundaries) via HDX; Peace Now's settlement built-up areas (2011) via OCHA; Wikidata (founding years) | `content/map/occupation_1967.geojson`, `content/map/oslo_areas_1995.geojson`, `content/map/occupation_layers.yaml` | CC BY 4.0; CC BY-IGO 3.0 (Peace Now/OCHA); CC0 (Wikidata); the Oslo areas: HDX "Other" (owner-verify) | Credit each source and say the data was changed (below); no endorsement implied |
| UNOSAT damage assessment of the Gaza Strip, 11 October 2025 (counts per cell) | `content/map/gaza_damage_2023.yaml` | CC BY-SA | Credit UNOSAT and say the data was changed (below); share-alike, as `content/` already is |
| Wikipedia text and coordinates | cited throughout `content/` | CC BY-SA 4.0 | Attribution (source URL per entry); share-alike, which is why `content/` is CC BY-SA 4.0 |
| EB Garamond | `unity/Assets/UI/Fonts/` | SIL Open Font License 1.1 | Keep `OFL.txt` with the fonts; do not sell the fonts on their own |
| Noto Naskh Arabic (Copyright 2022 The Noto Project Authors) | `unity/Assets/UI/Fonts/NotoNaskhArabic/` | SIL Open Font License 1.1 | Keep `OFL.txt` with the font; do not sell the font on its own |
| Amiri 1.003 (Copyright 2010-2022 The Amiri Project Authors), the Arabic credits' face | `unity/Assets/UI/Fonts/Amiri/` | SIL Open Font License 1.1 | Keep `OFL.txt` with the font; do not sell the font on its own |
| YamlDotNet (Copyright (c) Antoine Aubry and contributors) | `YamlDotNet.dll`, in every build | MIT ([`LICENSES/YamlDotNet-MIT.txt`](LICENSES/YamlDotNet-MIT.txt)) | Keep the licence text with the build |
| AI narration | `unity/Assets/StreamingAssets/narration/` (rendered offline; the scripts and renderers are outside this tree) | Outputs of Habibi-TTS Specialized MSA (Apache-2.0 per its model card) and Chatterbox Multilingual (MIT); the models are not shipped; the outputs' licence is under owner review (`ASSETS.yaml`, owner-verify) | Disclose as AI-generated: synthetic stock voices, no real person's voice cloned (`docs/credits.md`) |
| AI music | `unity/Assets/StreamingAssets/music/` (six tracks, generated offline) | Generated with Google Lyria 3.5; no third-party rights claimed (CC0 / no rights claimed; Lyria's output terms under owner review, `ASSETS.yaml`, owner-verify) | Disclose as AI-generated (`docs/credits.md`, `docs/ai-disclosure.md`) |

Code dependencies are fetched at build or run time under their own licences and are not vendored: NuGet packages in
`sim/**/*.csproj` (YamlDotNet MIT; test-only: xUnit Apache-2.0, Microsoft.NET.Test.Sdk MIT, xunit.runner.visualstudio
Apache-2.0), Unity packages in `unity/Packages/manifest.json` (Unity Companion License or the Unity Terms of Service),
and Python packages declared inline in `tools/*.py` and `scripts/oss/*.py`. The Unity-MCP editor package
(`com.ivanmurzak.unity.mcp`, Apache-2.0), its OpenUPM registry and the Rider package are not used: they are not in
`unity/Packages/manifest.json`, and `UnityMcpGuard` (`unity/Assets/Editor/UnityMcpGuard.cs`) keeps any such
editor-only assemblies out of a player build.

You need your own Unity licence (Unity Personal is free within Unity's eligibility terms) to open or build `unity/`.

## Required notices

The notices the licences above require, word for word. Every build carries this file in the game folder.

- **Copernicus DEM GLO-30** (Copernicus DEM licence; the elevation data is modified):
  "produced using Copernicus WorldDEM-30 (c) DLR e.V. 2010-2014 and (c) Airbus Defence and Space GmbH 2014-2018
  provided under COPERNICUS by the European Union and ESA; all rights reserved"
- **CHELSA V2.1 bio12** (1981-2010), CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/). Karger, D.N.,
  Conrad, O., Böhner, J., Kawohl, T., Kreft, H., Soria-Auza, R.W., Zimmermann, N.E., Linder, H.P. & Kessler, M. (2021)
  Climatologies at high resolution for the earth's land surface areas. EnviDat. https://doi.org/10.16904/envidat.228.v2.1 ;
  Karger et al. (2017) Scientific Data 4, 170122.
- **Rain-gauge data:** Israel Meteorological Service, meteorological data archive, data.gov.il dataset 481
  (https://data.gov.il/dataset/481), licence "Other (Open)" as stated on the dataset.
- **3D models** by Tal Heuberger: CC0 1.0 (https://creativecommons.org/publicdomain/zero/1.0/). Credited with thanks.
- **EB Garamond:** Copyright 2017 The EB Garamond Project Authors (https://github.com/octaviopardo/EBGaramond12);
  Copyright 2025 Deborah Khodanovich (RCS citation glyphs). SIL Open Font License 1.1, https://openfontlicense.org
  (`unity/Assets/UI/Fonts/OFL.txt`, shipped as `OFL-EBGaramond.txt`).
- **Noto Naskh Arabic:** Copyright 2022 The Noto Project Authors (https://github.com/notofonts/arabic). SIL Open Font
  License 1.1, https://openfontlicense.org (`unity/Assets/UI/Fonts/NotoNaskhArabic/OFL.txt`, shipped as
  `OFL-NotoNaskhArabic.txt`). Modified: line metrics only (`unity/Assets/UI/Fonts/NotoNaskhArabic/SOURCE.txt`).
- **Amiri:** Copyright 2010-2022 The Amiri Project Authors (https://github.com/aliftype/amiri). SIL Open Font License
  1.1, https://openfontlicense.org (`unity/Assets/UI/Fonts/Amiri/OFL.txt`, shipped as `OFL-Amiri.txt`). Unmodified.
- **YamlDotNet:** Copyright (c) Antoine Aubry and contributors. MIT License
  ([`LICENSES/YamlDotNet-MIT.txt`](LICENSES/YamlDotNet-MIT.txt)).
- **Narration models** (used offline; the models are not shipped, only the audio they made): Habibi-TTS Specialized
  MSA, Apache License 2.0 (https://www.apache.org/licenses/LICENSE-2.0); Chatterbox Multilingual by Resemble AI, MIT
  License. Stock synthetic voices; no real person's voice was cloned.
- **Occupation map data** (`content/map/occupation_*`, `oslo_areas_1995.geojson`; simplified, reprojected and baked
  into textures by `tools/occupation_digitise.py` and `tools/occupation_bake.py`): "West Bank Separation Barrier, January
  2018" and "Gaza Strip Buffer Area" (2023), OCHA occupied Palestinian territory, CC BY 4.0
  (https://creativecommons.org/licenses/by/4.0/), https://data.humdata.org/dataset/west-bank-barrier ,
  https://data.humdata.org/dataset/gaza-strip-closed-and-access-restricted-areas ; "State of Palestine - Village boundary
  in the West Bank", Palestinian Authority, Ministry of Planning, CC BY 4.0,
  https://data.humdata.org/dataset/state-of-palestine-village-boundary-in-the-west-bank ; "State of Palestine - Gaza Strip
  Municipal Boundaries", Gaza Housing Council, CC BY 4.0, https://data.humdata.org/dataset/gaza-stripmunicipal-boundaries ;
  "State of Palestine Settlements", Peace Now and OCHA oPt, CC BY-IGO 3.0
  (https://creativecommons.org/licenses/by/3.0/igo/), https://data.humdata.org/dataset/state-of-palestine-settlements ;
  "State of Palestine - Oslo Agreement in the West Bank", Palestinian Authority, Ministry of Planning, HDX licence
  "Other" (under owner review), https://data.humdata.org/dataset/state-of-palestine-other-0-0-0-0-0 ; founding years
  from Wikidata, CC0 1.0. None of these publishers endorses the game.
- **UNOSAT damage assessment** (`content/map/gaza_damage_2023.yaml`; aggregated to counts per cell of about 300 m and
  baked into a texture by `tools/gaza_damage_bake.py`): "UNOSAT Gaza Strip Comprehensive Damage Assessment - 11 October
  2025", United Nations Satellite Centre (UNOSAT), CC BY-SA,
  https://data.humdata.org/dataset/unosat-gaza-strip-comprehensive-damage-assessment-11-october-2025 . UNOSAT does not
  endorse the game.
- **Wikipedia:** text adapted under CC BY-SA 4.0 (https://creativecommons.org/licenses/by-sa/4.0/); every entry lists
  its source URLs.
- **Unity runtime:** used under the Unity Terms of Service. It includes the ICU data file UI Toolkit uses for
  right-to-left text: Copyright (c) 2016 and later Unicode, Inc. and others, Unicode License v3
  (https://www.unicode.org/license.txt). Unity is a trademark of Unity Technologies.

## Licences by path

Different parts of this repository are under different licences. Where a file is not listed below, the rule for its
folder applies.

| What | Paths | Licence |
|---|---|---|
| Code | `sim/`, `tools/` (Python), `scripts/`, `.github/`, `.gitignore`, `unity/Assets/Scripts/`, `unity/Assets/Editor/`, `unity/Assets/Tests/`, and everything else under `unity/Assets/` not listed below (scenes such as `Map.unity`, `WebGLTemplates/`, `link.xml`, `*.inputactions`, `.meta` files) | [MIT](LICENSE) |
| Game content (text and data) | `content/` (events, model, rules, map YAML/JSON and their baked runtime forms `map/*.tsv`), except the third-party map data below | [CC BY-SA 4.0](LICENSES/CC-BY-SA-4.0.txt). The text paraphrases its cited sources, many from Wikipedia (CC BY-SA), so share-alike applies; each entry lists its sources |
| Docs, art and UI assets | `docs/`, the root `*.md` files (except `LICENSE`), `ASSETS.yaml`, `tools/README.md`, `unity/Assets/UI/*.uxml`, `*.uss`, `strings.en.yaml`, `unity/Assets/Materials/`, `unity/Assets/Resources/Units/` (materials), `unity/Assets/Shaders/`, `unity/Assets/UI/*.asset`, `unity/Assets/WebGLTemplates/TheStoryOfTheLand/favicon.ico` | [CC BY 4.0](LICENSES/CC-BY-4.0.txt) |
| Unity template-derived files | `unity/Assets/Settings/` (URP assets, renderers, volume profiles), and any Shader Graph generated from a Unity template (for example `unity/Assets/Shaders/StoryOfTheLandTerrain.shadergraph`, from "Terrain Simple") | Created from Unity's project or graph templates and edited; under the [Unity Companion License](https://unity.com/legal/licenses/unity-companion-license) (use with Unity), not MIT |
| 3D models | `unity/Assets/Art/Settlements/Prototype/Models/` (23 single-building models by Tal Heuberger and their colour maps; the materials beside them are under the code row) | Marked [CC0 1.0](LICENSES/CC0-1.0.txt). No rights are claimed. AI-generated with Tripo Studio on a paid plan; per-file records in `ASSETS.yaml` |
| In-engine captures | `unity/Assets/UI/StartScreen/start-map.png`, `docs/images/hero-engine-*.jpg` | CC BY 4.0. They show the Copernicus-derived terrain; see below. Captures that show the CC BY-SA 4.0 map bakes of decision D13 (`start-map.png`, `hero-engine-*.jpg`) are CC BY-SA 4.0; per-file licences in `ASSETS.yaml`. |
| AI narration | `unity/Assets/StreamingAssets/narration/*.ogg` | Synthetic voices rendered offline from the project's own scripts: English with Chatterbox Multilingual V3 (Resemble AI, MIT) in its stock voice, Arabic with the Habibi-TTS Specialized MSA checkpoint (Apache-2.0 per its model card). No real person's voice was cloned. Marked [CC0 1.0](LICENSES/CC0-1.0.txt), no rights claimed, pending the owner's ruling (`ASSETS.yaml`, owner-verify); credited in `docs/credits.md` and listed in `docs/ai-disclosure.md` |
| AI music | `unity/Assets/StreamingAssets/music/` (`.ogg`, and the web player's `.mp3` copies) | AI-generated with Google Lyria 3.5 at the owner's request; no third-party rights are claimed. Marked [CC0 1.0](LICENSES/CC0-1.0.txt), no rights claimed, pending the owner's check of Lyria's output terms (`ASSETS.yaml`, owner-verify); credited in `docs/credits.md` and listed in `docs/ai-disclosure.md` |
| 3D models (The 1948 Collection, streamed) | `unity/Assets/Art/Collection/` (50 models by Tal Heuberger: people, vehicles, ships, aircraft, animals and ten whole towns, slimmed by `tools/slim_collection.py`, and their colour maps; ten of them are from #336 only and are marked CC0 on the owner's decisions of 18 and 19 October 2026 on #708, without a licence line from their author (`ASSETS.yaml`); the prefabs, controllers and materials beside them are under the code row) | Marked [CC0 1.0](LICENSES/CC0-1.0.txt). No rights are claimed. AI-generated with Tripo Studio on a paid plan; per-file records in `ASSETS.yaml` |
| Fonts | `unity/Assets/UI/Fonts/` | EB Garamond, [SIL Open Font License 1.1](unity/Assets/UI/Fonts/OFL.txt); Noto Naskh Arabic (`NotoNaskhArabic/`, Copyright 2022 The Noto Project Authors), [SIL Open Font License 1.1](unity/Assets/UI/Fonts/NotoNaskhArabic/OFL.txt); Amiri (`Amiri/`, Copyright 2010-2022 The Amiri Project Authors), [SIL Open Font License 1.1](unity/Assets/UI/Fonts/Amiri/OFL.txt) |
| Terrain heightmap | `unity/Assets/StreamingAssets/map/heightmap.*`, `content/map/hexes.json` (elevations) | Derived from Copernicus GLO-30 under the Copernicus DEM licence. Required notice: "produced using Copernicus WorldDEM-30 (c) DLR e.V. 2010-2014 and (c) Airbus Defence and Space GmbH 2014-2018 provided under COPERNICUS by the European Union and ESA; all rights reserved" |
| Derived map bakes | `unity/Assets/Resources/Map/BiomeColours.bytes` (biome colours, #529), `unity/Assets/Resources/Routes/RouteRibbons.bytes` (route ribbons, #436), `unity/Assets/Textures/Map/landuse.png` (land-use texture, `tools/land_bake.py`, #33), `unity/Assets/Resources/Map/MinimapRelief.png` (the minimap's painted relief, `tools/minimap_bake.py`, #398) | Made by the bakes from the map content (`content/map/`, CC BY-SA 4.0) and the Copernicus-derived heightmap. The hexes' biomes come from the Copernicus DEM, CHELSA V2.1 and the IMS station normals. [CC BY-SA 4.0](LICENSES/CC-BY-SA-4.0.txt), the most restrictive input, by the owner's decision D13 (`docs/oss/DECISIONS.md`); per-file records in `ASSETS.yaml` |
| Precipitation grid | `content/map/precip_bio12.*` | CHELSA V2.1, CC0 1.0 (Karger et al. 2017, 2021; see `tools/README.md`) |
| Rain-gauge data | `content/map/precip_stations.json` | Israel Meteorological Service, data.gov.il dataset 481, under the licence stated on the dataset ("Other (Open)") |
| Unity engine and packages | `unity/Packages/`, `unity/ProjectSettings/` | Configuration only. The Unity engine and its packages are under Unity's own terms and are not included. No Asset Store content is in this repository. |
| In every build | `YamlDotNet.dll` | MIT, Copyright (c) Antoine Aubry and contributors ([LICENSES/YamlDotNet-MIT.txt](LICENSES/YamlDotNet-MIT.txt)) |

Historical facts are not owned by anyone. The text describing them paraphrases the cited sources.

Still being checked by the owner: every row marked owner-verify in [ASSETS.yaml](ASSETS.yaml), among them the AI
narration's and the AI music's licence; the open decisions are in [docs/oss/DECISIONS.md](docs/oss/DECISIONS.md). Files with such a
row are published before the checks end. If a check fails, the file is removed from the current tree.

## Builds (what a WebGL or desktop build carries)

A build is a different artefact. It contains the Unity runtime, `YamlDotNet.dll`, the fonts, the map data, the
narration audio and the music. Before a build is handed to anyone, it must carry `LICENSE`, `NOTICE`, this file, `LICENSES/` and
each font's licence: `unity/Assets/UI/Fonts/OFL.txt` as `OFL-EBGaramond.txt`,
`unity/Assets/UI/Fonts/NotoNaskhArabic/OFL.txt` as `OFL-NotoNaskhArabic.txt`, `unity/Assets/UI/Fonts/Amiri/OFL.txt` as
`OFL-Amiri.txt`. `scripts/oss/package_web.sh` (WebGL) and
`tools/release_build.py` (desktop) copy them into the game folder, and each refuses to finish unless the archive lists
every one of them (see [`docs/RELEASING.md`](docs/RELEASING.md)). The in-game Credits screen points to this file.

## Third-party trademarks

Names below belong to their owners and are used only to say truthfully what this project uses, runs on or is compared
with. None of them endorses this project, and this repository ships none of their logos.

- Unity is a trademark of Unity Technologies.
- ElevenLabs is a trademark of Eleven Labs, Inc.
- Steam is a trademark of Valve Corporation.
- Wikipedia and Wikimedia Commons are trademarks of the Wikimedia Foundation.
- Copernicus is a programme of the European Union.
- GitHub is a trademark of GitHub, Inc.

If you own a mark named here and something in this repository misuses it, open an issue and it will be fixed.
