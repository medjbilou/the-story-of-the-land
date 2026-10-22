# Changelog

All notable changes to The Story of the Land (حكاية الأرض). The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/). Every pull request that changes the game (`sim/`, `content/`, `unity/`)
adds a line under **Unreleased**; CI's `Changelog` job checks this (see `CONTRIBUTING.md`).

## [Unreleased]

### Added
- The family game: the player follows a Palestinian family and its village through the history of the land, with ten
  resources (Land, Home, Family, Documents, Livelihood, Memory, Sumud, Community, Safety, Solidarity) in a
  deterministic sim (`docs/palestine-model.md`).
- Seven audited chapters, from 1880 to October 2026, converted to event YAML by `tools/tsv_to_events.py`, every fact
  with its source; the places, the 1948 village fates, the currencies and the historical figures from
  `tools/places_to_yaml.py`, `tools/currency_to_yaml.py` and `tools/figures_to_yaml.py`.
- Every chapter and every People biography told from the Palestinian side: what came before each event, who acted, no
  violence told as a "response", "Zionist" for the movement, its militias and the Israeli government and army, Israeli
  state and army statements as claims, the genocide findings attributed to who made them.
- Event cards for 2023-2026: "Before 7 October" (`gz_2023_10_before_october`) with the blockade and the 2023 killings
  that preceded the attack, which has its own card (`gz_2023_10_attack`); five West Bank cards (the killing from October
  2023, Walid Daqqa and the prisons, "Iron Wall" emptying the Jenin, Tulkarm and Nur Shams camps, Masafer Yatta and the
  herders, E1), with choices for three; and Sharon's 1971 demolitions in the Gaza camps (`e1971_07_gaza_camps`).
- Arabic interface and Noto Naskh Arabic font, Arabic place names on the map, and pre-rendered Arabic and English
  narration.
- The village fates on the 3D map as a time layer, a chapter picker and a village picker on the start screen, and a
  People screen with AI-made ink-and-wash portraits, or a symbolic still life where no face is known.
- The map changes with the occupation from June 1967 (display only; the sim and the goldens do not depend on it): the
  occupied West Bank and Gaza washed in the Israeli side's blue, Oslo II's Areas A, B and C from 28 September 1995,
  Gaza's fence (1994) and its access-restricted area (September 2000), the wall from June 2002 (OCHA's January 2018
  route, built parts solid from 2018), and Israeli settlements as dots from their founding year, the 25 emptied in
  August 2005 gone from then, with their built-up areas from 2011; drawn from the true shapes by the terrain
  (`OccupationLayers`, `StoryOfTheLandEra.hlsl`), with a legend in English and Arabic. Data: OCHA, the Palestinian
  Ministry of Planning, the Gaza Housing Council, Peace Now (via HDX) and Wikidata (`tools/occupation_digitise.py`,
  `tools/occupation_bake.py`).
- The destruction of Gaza on the map from October 2023: the areas UNOSAT's satellite images record as damaged (grey) and
  destroyed (dark) appear by each image's date, with smoke over the areas the images of each month's span record, none in
  the months of the November 2023 truce, the January-March 2025 ceasefire or after 10 October 2025 (`GazaDamage`,
  `tools/gaza_damage_bake.py`: counts per area of about 300 m, UNOSAT's totals of 11 October 2025; no raw points).
- Footage on the event cards: `content/footage.tsv` lists candidate clips (Al Jazeera, Middle East Eye, the UN, Archive
  Genocide and others); a card shows "Watch footage" only for a clip a person has watched and signed off
  (`tools/footage_to_yaml.py`, `docs/footage-review.md`). After the content note, a publisher's page opens in the browser
  and an Archive Genocide video or photo plays in the desktop game's viewer over the card (streamed, never stored; the web
  player opens it in a new tab), with its caption and credit, and the music holds while it plays.
- Music, AI-generated with Google Lyria: a main theme on the start screen, a theme for each era and a war theme in the
  war months, crossfaded and lowered under the narration, with a Music volume setting (`MusicPlayer`).
- The Credits screen (Settings), from `docs/credits.md` through `tools/credits_to_yaml.py`, ending with "Created by
  Mohamed Jbilou", and one licence and attribution notice, `THIRD-PARTY-NOTICES.md`, shipped in every build.
- The pause menu, tabbed settings (display, graphics, audio, language, accessibility, controls), graphics presets and
  detailed village kits (`docs/PLAN.md`).
- End turn waits until every card of the month is answered, and says why. The mouse wheel zooms toward the point under
  the pointer.
- The Unity 6000.6.6f1 project; the macOS player builds on Mono (`Build.MacMono`).
- The content policy, which allows the disclosed People-screen portraits (`docs/CONTENT_POLICY.md`).

### Fixed
- In a short window (1366x657) the panels under the top bar start below the raised date plate, not under it, and the
  card's backdrop still dims the plate evenly; the toast and the border caption mirror exactly in Arabic; at 130% text
  the ten meters keep their room before New Game (`HudPresenter.PlateHang`).
