# The family model

How the sim carries The Story of the Land. The player is a Palestinian family and its village. This note maps each
sim concept onto the family model and lists the sim files that hold it. Content: `../content/*.tsv` (the audited
chapters) becomes `content/palestine/` through `tools/tsv_to_events.py`.

## Concept map

| Sim concept | Where | Family model |
|---|---|---|
| Indicators, 0..1, with a `base` (`model.yaml`) | `Content.Indicators`, `EventSystem` | The ten family resources in `content/palestine/model.yaml`: `land`, `home`, `family`, `documents`, `livelihood`, `memory`, `sumud`, `community`, `safety`, `solidarity`. A TSV effect of *n* points is a delta of *n*/100. Every base is 0.5 (a design value) until each chapter has its own start values. `LoadChapter` reads indicators only: any other key of a family `model.yaml` is an unknown-key error. |
| Events: window `[from, to)`, `anchor`, `tags`, `conditions`, `requires` | `GameEvent`, `EventSystem.Trigger` | One TSV event row. `year`/`month` become the window of that month. Every event is `soft` and every review status is `draft`: the converter never raises a review status for an agent. |
| Options | `EventOption` | One family choice (`choices_*.tsv`): its label, its effects and its outcome text. An event with no choice gets one option, `continue` ("Continue" / "متابعة"), with no effects of its own. |
| Option effects `{ target, delta }` | `EventSystem.Resolve` | The choice's effects, on the family resources. The event's own effects (see below) apply first, then the option's. |
| Cause log (`Cause`, "Why did this happen?") | `Cause.cs`, `PlayerText` | Each effect writes a line such as "Land -3.0% (The Partition Resolution)". |
| Chapters | `ContentLoader.LoadChapter`, `Content.Eras` | Each chapter is its own campaign, loaded by `ContentLoader.LoadChapter(root, "<chapter>")`. The loader makes the chapter one era, from its first dated month to the end of its last (the window the converter gives fate templates), in the `family` loop with one-month turns. No `eras/<chapter>.yaml` is needed. |
| Unanswered cards | `EventSystem.LetPass` | A card the family leaves unanswered still happens at the month's end: its own effects apply, no option's, and it is recorded with option `""`. |
| Sensitive tags and the review gate (R-GATE-001) | `EventRules.SensitiveTags`, `tools/package_release.py` | `sensitive: 1` (a massacre or killings) becomes the tag `sensitive`. That tag is a sensitive tag, so these events go through the sensitivity-review gate. Our rule, "no choice rewards harming civilians", is enforced in the audits, not by the sim. |

## What the event schema could not hold, and the extension chosen

Each extension is optional. The schema (`content/schema/event.schema.json`) and the
loader types (`GameEvent`, `EventOption`) change together, and `SchemaTests` keeps them equal.

| Our column | Extension |
|---|---|
| `title_ar`, `text_ar`, `label_ar`, `outcome_ar` | `title_ar`, `text_ar` on the event, and `text_ar`, `outcome_ar` on the option. |
| `outcome_en` | `outcome` on the option: what happened to the family after the choice. |
| `scope` | `scope`: `national`, `region:<place_id>` or `fate:depopulated\|destroyed\|built_over`. It is checked against the same pattern in the schema and in `EventRules`. |
| `place_id` | `place`, a places.tsv id. |
| event-level `effects` | `effects` on the event, with the same shape as an option's. They are checked like option effects and applied in `Resolve` before the option's, always in full. |
| `sensitive` | The tag `sensitive` in the existing `tags` list. The schema needs no new field. |
| One file per chapter | `content/palestine/events/<chapter>.yaml` holds a top-level `events:` list. The event schema applies to each entry, not to the file. That is why these files have no `$schema` first line. |
| Fate templates: no year or month | Their window is the whole chapter: from the first day of the earliest dated month to the end of the latest one. The `fate:*` scope holds them back (see below). `{village_en}` and `{village_ar}` are allowed only in a `fate:*` event. |
| Sources: URLs only | The schema needs a `checked` date. The converter uses the TSV header's "Compiled YYYY-MM-DD", the day the URLs were opened. |

There are two caveats:
- The ids `fate_depopulated`, `fate_destroyed` and `fate_built_over` appear in both 1947_1949 and 1967_1987. This is fine while each chapter loads alone. A combined load would refuse the duplicate ids, so rename them in the TSV first (for example `fate1967_*`).
- Their YAML tooling (`tools/yaml12.py`) needs PyYAML, so it is not stdlib-only. The schema check therefore runs in the C# `SchemaTests`, using their own validator. The Python `--check` covers fidelity: the file must equal a fresh conversion, and converting the file back must give the TSV rows exactly.

## The family's game

One chapter, a chosen village, one month a turn. It follows the sim's patterns: `Simulation.Step` is state in, state out
on a clone; every change writes a `Cause`; the village is in the state hash and the save.

- Content: `tools/places_to_yaml.py` (stdlib only, `--check` as in `tsv_to_events.py`) writes `content/palestine/places.yaml`
  and `fates.yaml`. `key_places.tsv` has no coordinates, so the places come from the map's place list
  `../unity/Assets/StreamingAssets/map/places.tsv` (296 places, with Arabic names); each `key_places.tsv` row must agree with it
  (Arabic name, 1945 population, sources), and its population note is carried over. Each entry is one JSON line, which YAML
  reads as a flow mapping; numbers are written as the TSV writes them, so the round trip is exact. `village_fates.tsv` becomes
  455 fates, undated ones included. TSV comment lines are not carried over.
- `Content.cs`: `GameEvent` has `TitleAr`, `TextAr`, `Scope`, `Place` and `Effects`; `EventOption` has `TextAr`, `Outcome` and
  `OutcomeAr`. `Content.Places`, `Content.Fates`, `IsFamily`, and the loop `EraSystems.Family` (`family`), which `eras.yaml`
  cannot set. Its ruleset is `family-0.1`.
- `EventRules.cs`: `sensitive` is one of the `SensitiveTags`. `Validate` checks `scope`, and checks event-level effects with
  the same function as option effects. A `fate:*` event may use `{village_en}` and `{village_ar}`.
- `ContentLoader.cs`: `LoadChapter(readFile | contentRoot, chapter)` reads `palestine/model.yaml` (indicators only),
  `palestine/events/<chapter>.yaml`, `places.yaml` and `fates.yaml`, and checks them (unique ids, a name, coordinates inside
  Mandate Palestine, known fate kinds, a month only with a year, https sources), and checks that every event's `place` and
  `region:` place exists. It runs `EventRules.Validate`, then `BuildWindows`, builds the chapter's era (above), sets
  `Rules.Start`, and sets `Content.Hash` over the files it read (`Hashed`).
- `Family.cs`: the `Place` and `VillageFate` types, and the rules. `Reaches`: national events reach every family;
  `region:<place>` events reach a village within 25 km, on a flat measure (111.2 km a degree of latitude, 94.6 a degree of
  longitude, no trigonometry); a `fate:<fate>` template reaches the family in the month its own village has a dated fate of
  that kind. A fate with a year and no month counts from December; an undated fate never fires.
  `Fill(content, state, text)` gives `{village_en}` / `{village_ar}` the village's names. `NewGame` and the monthly `Step`.
- `EventSystem.cs`: `Trigger` filters with `Family.Reaches` and has no per-turn cap in a chapter (June 1967 brings five
  cards to an Imwas family). `Resolve` applies the event's effects, then the option's. `LetPass` resolves the unanswered cards.
- `GameState.cs`: the family's fields, among them `Village`. The hash writes `village=<id>` only when it is set; the rest of
  the hash text is in `docs/determinism.md`.
- `SaveGame.cs`: schema 3 (`ruleset family-0.1`). After `ruleset`, a `village <id>` line. A family save must name a village.
  A save with an older schema or ruleset is refused as made under older rules (R-SAVE-001), a newer one as made by a newer
  version (R-SAVE-002). `GameSession.Load` replays a family save with `New(content, seed, village)` and refuses a village
  the content does not know.
- `Simulation.cs` / `GameSession.cs`: `NewGame(content, seed, village)` and `GameSession.New(content, seed, village)`. `Step`
  on a chapter runs `Family.Step`: the family's answers (`ChooseEventOption` is the only order; any other is rejected), the
  unanswered cards (`EventSystem.LetPass`), one month, the chapter's end (`era_ended`), then the next month's events. There
  is no RNG draw: the seed is kept in the state, and nothing draws from it yet.
- Tests: `PalestineContentTests.cs`, `SchemaTests.cs` and `FamilyTests.cs`: national events reach every family; region
  events only nearby villages (Haifa about 23 km from Tantura, Jaffa about 65 km); a Tantura family gets `fate_depopulated`
  in May 1948, its choice applies and the Cause log names it, and Tantura's undated fates never fire; Imwas gets
  `fate1967_depopulated` in June 1967 and its year-only `fate1967_destroyed` in December; the Gaza chapter opens on
  `gz_2023_10_attack` for Khan Yunis; the save round-trips with the village; the same seed and village give the same hash.

Known limits: two dated fates of the same kind for one village in one chapter fire the card once (event ids fire once).
Imwas's Arabic name in the map's `places.tsv` carries bidi marks (U+200F, U+200E), which `{village_ar}` passes on.

## Currency: the money the family used

The family's money comes from the date and the village.

- Content: `../content/currency.tsv` (24 rows, sourced) gives each currency a period (`from_*` inclusive, `to_*` exclusive,
  blank = still in use) and a region: `national` (every family, before the 1948 split), `inside_1948`, `west_bank`, `gaza`.
  A change dated after the 1st of a month counts from the next month, so a turn shows the money in force when its month began
  (the occupation of 5–7 June 1967 shows from July). An Israeli currency says so in its name, one wording a tier: "(imposed by the occupying state)"
  inside the 1949 lines, "(imposed by the occupation)" in the West Bank and Gaza from 1967, "(under occupation)" from the
  Paris Protocol (May 1994). Where the name or subunit changed inside a period, the row is split by date (the "Palestine
  Pound" notes of 1948-1952, mil then prutah from 1949, the Israeli pound from 1952, the agora from 1960). `../content/place_regions.tsv`
  lists the 108 places outside the 1949 lines (98 west_bank, 10 gaza), from Wikidata's "located in" chain read by hand; every
  other place is `inside_1948`. A refugee family's currency still follows its village: where it fled to is not modelled yet.
- `tools/currency_to_yaml.py` (stdlib only, `--check`; reuses the helpers of `places_to_yaml.py`) writes
  `content/palestine/currency.yaml`: a `currencies:` list and a `regions:` list. It refuses overlapping periods in one region or
  with a national period.
- `Family.cs`: `Currency`, `PlaceRegion`, `Family.Region(c, village)` and `Family.CurrencyAt(c, village, date)` /
  `CurrencyAt(c, state)`. `Content.Currencies`, `Content.PlaceRegions`.
- `ContentLoader.cs`: `LoadChapter` reads `currency.yaml` (required; it is in `Content.Hash`) and checks it: known
  regions, names and symbols in both languages, https sources, known places, and exactly one currency for every month of the
  chapter in every region.
- `PlayerText.Currency(c, state, arabic)` ("Palestine pound, £P" / "الجنيه الفلسطيني، ج.ف") and `PlayerText.Livelihood(c, state, arabic)`
  ("Livelihood 50% · Palestine pound, £P" / "الرزق 50% · الجنيه الفلسطيني، ج.ف", the resource's `name_ar` from
  `palestine/model.yaml`). The sign is left out where it only repeats a word of the name ("الليرة العثمانية"). The HUD shows
  it beside Livelihood each turn (`HudPresenter`).
- Tests: `CurrencyTests.cs`: Tantura in May 1948 (Palestine pound), Jenin in June 1967 (Jordanian dinar; the occupation's lira
  from July), Gaza in June 1956 (Egyptian pound; the imposed lira in January 1957), Khan Yunis in October 2023 (the shekel,
  under occupation); a whole chapter's Cause log and currency text for three villages never shows "IL" or "Foreign currency";
  `currency.yaml` read by the sim gives back both TSVs exactly; the loader refuses a month with no currency and a bad row.

## Still to do

1. Objectives: the "keep the claim alive" goals (memory, documents, sumud, return), tied to resource thresholds.
2. Per-chapter start values for the family resources (every base is 0.5).

## Running it

```
python3 tools/tsv_to_events.py          # writes content/palestine/events/*.yaml
python3 tools/tsv_to_events.py --check  # exit 1 on a stale file or a round-trip mismatch
python3 tools/places_to_yaml.py         # writes content/palestine/places.yaml and fates.yaml
python3 tools/places_to_yaml.py --check
python3 tools/currency_to_yaml.py       # writes content/palestine/currency.yaml
python3 tools/currency_to_yaml.py --check
nice -n 10 dotnet test sim/StoryOfTheLand.Sim.Tests
```
