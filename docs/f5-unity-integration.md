# The family game on screen (F5)

The sim side is in `docs/palestine-model.md`: seven chapters load with `ContentLoader.LoadChapter(readFile, chapter)`,
a game starts with `GameSession.New(content, seed, village)`, and the family loop fires national, nearby (25 km) and
village-fate events, with the era's currency (`Family.CurrencyAt`) and Arabic dates (`PlayerText.Date(d, arabic)`).
This page lists how Unity shows it, step by step; each step is testable on its own.

1. **Sim plugin.** `unity/Assets/Plugins/StoryOfTheLand/StoryOfTheLand.Sim.dll` is built by `tools/build_sim_for_unity.py`
   (save version 3, with the `village` line).
2. **Content.** `content/palestine/` (model, places, fates, currency, figures, credits, `events/<chapter>.yaml` for all
   seven chapters) ships through the StreamingAssets content sync and its manifest/bundle:
   `ContentManifest.OptionalFolders` ships `palestine/` and `palestine/events/`, and `GameInputs.Chapter(id)` loads a
   chapter from the disk or the web bundle. `tools/figures_to_yaml.py` and `tools/credits_to_yaml.py` (stdlib, `--check`)
   write `palestine/figures.yaml` and `palestine/credits.yaml`.
3. **New game.** The start screen's New game opens a chapter picker (seven chapters, Arabic and English titles, years)
   and then a village picker (`Content.Places`, Arabic names, searchable, grouped by region: inside the 1948 lines,
   West Bank, Gaza): `FamilyPickerPresenter`, then `GameRoot.StartFamily`. Continue loads the family autosave,
   `saves/family/autosave.txt`, its chapter on the first line (`FamilyGame.SaveText`). The automated runs (smoke,
   `-golden`, `-shot`, the profiles, `-nostart`, the EditMode tests) play `GameRoot.AutomatedChapter` (`1947_1949`) for a
   family of `GameRoot.AutomatedVillage` (Tantura) with the golden seed and touch no save. `-golden` plays the family
   goldens of `content/palestine/goldens.yaml` (`GoldenCheck`). `-family <chapter>:<village>` opens a family game at once
   without touching the save (the launch check).
4. **HUD.** The top bar shows the ten family resources (Land, Home, Family, Documents, Livelihood, Memory, Sumud,
   Community, Safety, Solidarity) with their Arabic names, and the era's currency beside Livelihood
   (`PlayerText.Currency(content, state, arabic)`, tooltip from `Family.CurrencyAt`: name, symbol, subunit, note). The
   date line uses `PlayerText.Date(d, arabic)`. `HudPresenter`, with `.family` on the root (`StoryOfTheLand.uss`).
5. **Event cards.** In Arabic mode a card shows `title_ar`/`text_ar` and its choices' Arabic labels; after a choice the
   outcome text (`outcome_ar`/`outcome`). Text is filled with `Family.Fill(...)` (it fills `{village_ar}`/`{village_en}`).
   A card tagged `sensitive` opens with the content note from `docs/CONTENT_POLICY.md` rule 9 before its text. After the
   text come the people the event names (a link each to the People screen) and its sources, a short list of links that
   open in the browser. `EventCardPresenter.RenderFamily`, `FamilyGame` (texts, outcome, Why).
6. **The village on the map.** The camera frames the family's village at start (`GameRoot.FrameVillage`); the village is
   marked; places show `name_ar` in Arabic mode (`content/map` rows, see `audits/map_names_ar.md`). Villages whose sourced
   fate has happened by the current date show it (depopulated / destroyed / built over) as a map state
   (`MapLabelsPresenter`: `home`, `SetFates`, `name_ar`).
7. **Figures.** `content/palestine/figures.yaml` (sourced, factual bios; `audits/figures_sources.md`) shows as the People
   screen (`PeoplePresenter`), each figure in the ornamented frame with its portrait, and as a link on the cards in
   `related_events`. Name, years, role and bio in the UI language.
8. **Narration.** Each chapter opens with `narration/scripts/intro_<chapter>`, rendered to
   `StreamingAssets/narration/intro_<chapter>.<lang>[.habibi].ogg`. `Narrator` (a MonoBehaviour GameRoot adds in Play mode
   and players): `GameRoot.StartFamily` plays the chapter's intro in the UI's language, Arabic `.ar.habibi.ogg` first, then
   `.ar.ogg`; a missing file plays nothing. The 1947_1949 chapter plays `intro_1947` (`Narrator.ScriptId`). N and Settings'
   Narration switch (`narration` in settings.txt) mute it. The files are rendered by `narration/` (README) and copied into
   `unity/Assets/StreamingAssets/narration/`. Credits disclose the synthetic voices.
9. **Credits.** A Credits screen (Settings) shows `docs/credits.md` in the UI language (`CreditsPresenter`).
10. **Verification.** EditMode tests for each step (`FamilyGameTests`); a Mac build; screenshots in Arabic and English of:
    start screen, chapter picker, village picker, the HUD in 1948 / 1967 / 2023, a sensitive card with its note, an
    outcome, the People screen, Credits (`-runTests -testPlatform EditMode -testFilter FamilyCaptures` with
    `STORY_F5_DIR` set, 1600x900). An Arabic-scholar agent reviews every Arabic screenshot
    (`.claude/agents/arabic-reviewer.md`) and every finding is fixed before a step is called done.
