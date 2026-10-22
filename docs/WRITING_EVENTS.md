# Writing an event

An event is one entry of a chapter file, `content/palestine/events/<chapter>.yaml`. The game shows it to a family as a
card with one or more choices; each choice moves the family's resources. Nobody writes these files by hand: a chapter is
written and audited as two TSV files at the project root (`../content/events_<chapter>.tsv` and `choices_<chapter>.tsv`),
and `tools/tsv_to_events.py` converts them. A correction is made in the TSV row, never in the generated YAML
(`python3 tools/tsv_to_events.py --check` fails on a hand edit). This page says what the converted file holds and what
the loader refuses. Everything here was checked against the loader (`sim/StoryOfTheLand.Sim/ContentLoader.cs`,
`EventRules.cs`, `Content.cs`, `ReleaseGate.cs`); if the code and this page disagree, the code wins and the page has a
bug (please open an issue).

Read [docs/CONTENT_POLICY.md](CONTENT_POLICY.md) first. It is the contract for sources, quotes and sensitive topics.
[palestine-model.md](palestine-model.md) maps each TSV column onto the fields below.

## The shortest path

1. Find your sources and write down, for each fact, a URL and the date you opened it.
2. Add or fix the row in the chapter's TSV files, then run `python3 tools/tsv_to_events.py`.
3. Run `dotnet test sim/StoryOfTheLand.sln` (see "Running the tests").
4. Add a line to `CHANGELOG.md` under `## [Unreleased]`, and open a pull request.

## Fields

The loader is strict. An unknown key is an error, so a typo such as `titel:` fails loudly instead of being ignored.
A key written with no value (`tags:`) is an error for lists. The fields follow `content/schema/event.schema.json`, and
`SchemaTests` keeps the schema equal to the loader types.

| Field | Required | What it is |
|---|---|---|
| `id` | yes | Unique in the chapter. Lowercase letters, digits and underscores, starting with a letter. |
| `title` | yes | The card title. May contain `{village_en}` in a `fate:*` event. |
| `text` | yes | The card body. Same placeholder rule as `title`. |
| `title_ar` | no | The Arabic title. May contain `{village_ar}` in a `fate:*` event. |
| `text_ar` | no | The Arabic body. |
| `scope` | no (empty means national) | Whose family the event reaches: `national`, `region:<place id>` or `fate:depopulated`, `fate:destroyed` or `fate:built_over`. |
| `place` | no | The `places.yaml` id of where the event happened. |
| `effects` | no | The event's own `{ target, delta }` list, applied when it resolves, before the chosen option's. |
| `tags` | no | A list of labels. Some are sensitive. See below. |
| `from` | yes | First day of the window, `YYYY-MM-DD`. |
| `to` | no | Day the window ends (exclusive). Must be after `from`. |
| `day` | no | The day of the `from` month the event happened, where its text dates it. A turn's cards come in date order, an event with no `day` (a month-wide one) first. `tools/tsv_to_events.py` reads it from the English text ("On 9 April 1948"). |
| `options` | yes | One or more choices. See below. |
| `sources` | yes by the schema | A list of `{ url, checked }`. See below. |
| `status` | no (default `draft`) | The review status. See below. |

### Placeholders

Never type a family member's or a real person's name into a text. The only placeholders are `{village_en}` and
`{village_ar}`, and only in a `fate:*` event: the game replaces them with the name of the family's own village. Any other
`{...}`, or any stray `{` or `}`, is an error.

Write plain description. Do not put words in anyone's mouth: no invented quotes
([CONTENT_POLICY.md](CONTENT_POLICY.md), section 2).

### Scope and window

A month is one turn. An event reaches the family in the month its window `[from, to)` overlaps, if its scope lets it:
a `national` event reaches every family, a `region:<place>` event a family whose village is within 25 km of that place,
and a `fate:<kind>` card a family whose own village has a dated fate of that kind that month (`places.yaml`,
`fates.yaml`). Every event that reaches the family in a month comes that month. A card the family leaves unanswered
still happens at the month's end: its own `effects` apply, no option's.

### tags and the sensitive review

Tags are free-form, but these are sensitive (checked without regard to case): `sensitive` (a massacre or killings; the
card opens with a content note), and the inherited `holocaust`, `terror`, `massacre`, `war-casualties`, `displacement`.
An event with any of them needs `status: sensitivity-reviewed` before any build is shared. That status is set by the
maintainer, not by you. `Build -shareable` and `tools/package_release.py` refuse to release while any sensitive event
of any chapter is not `sensitivity-reviewed` (R-GATE-001). Our rule that no choice rewards harming civilians is checked
in the audits, not by the sim.

### options

Each option has:

| Field | Required | What it is |
|---|---|---|
| `id` | yes | Unique within the event. Same shape as an event id. |
| `text` | yes in practice | What the family's button says. |
| `text_ar` | no | The Arabic button text. |
| `outcome` | no | What happened to the family after this choice (no historical claim). |
| `outcome_ar` | no | The Arabic outcome. |
| `effects` | no | A list of `{ target, delta }`. |

An event needs at least one option. An event with no choice in the TSV gets one option, `continue`.

### effects

`target` is a family resource id from `content/palestine/model.yaml`: `land`, `home`, `family`, `documents`,
`livelihood`, `memory`, `sumud`, `community`, `safety`, `solidarity`. They live between 0 and 1, so a delta of `0.03` is
three points of the scale (a TSV effect of *n* points is *n*/100). `delta` must be a finite number. Any other target is
an error. The effects are design values, not facts.

### sources

```yaml
sources:
  - { url: "https://en.wikipedia.org/wiki/United_Nations_Partition_Plan_for_Palestine", checked: 2026-10-19 }
```

Each URL starts with `https://` and `checked` is the day the URL was opened (the converter takes the TSV header's
"Compiled YYYY-MM-DD"). A source must support the claim it is attached to ([CONTENT_POLICY.md](CONTENT_POLICY.md),
section 2).

### status

One of `draft`, `fact-checked`, `historian-reviewed`, `sensitivity-reviewed`; anything else is an error. The converter
writes none, so every chapter event is `draft`: it never raises a review status for an agent.

- `fact-checked`: a person checked every claim against its sources.
- `historian-reviewed`: not a gate yet. How a review is done and recorded until then: [HISTORIAN_REVIEW.md](HISTORIAN_REVIEW.md).
- `sensitivity-reviewed`: set by the maintainer for events with a sensitive tag (see above).

If AI helped draft the text, a person still checks every claim before `fact-checked`
([CONTENT_POLICY.md](CONTENT_POLICY.md), section 2, rule 6).

### reviewed_by and reviewed_on

A person's sign-off beside `status`: `reviewed_by` is the reviewer's GitHub handle (never an email), `reviewed_on` the
day, `YYYY-MM-DD`. Both or neither; the loader refuses a blank or malformed value. **Only the reviewer writes them**, in a
commit of their own: never add, change or remove them yourself on someone's behalf, and an AI agent never does
(`scripts/oss/reviewed_by_guard.py` fails such a pull request; owner, 15 October 2026, #375). An agent may not edit any
field of a signed row either, until a person signs it again ("Block it in CI", owner, 15 October 2026). See
[HISTORIAN_REVIEW.md](HISTORIAN_REVIEW.md#sign-the-row-off).

## What the loader rejects

Errors are printed as `file: field: message`, the file relative to `content/palestine/`. These are the messages you are most likely to see:

| Message contains | Cause |
|---|---|
| `Property 'x' not found on type 'StoryOfTheLand.Sim.GameEvent'` | A key the loader does not know (usually a typo). Not a field of the event. |
| `must be lowercase letters, digits and underscores, starting with a letter` | An event, option, resource or place `id` of the wrong shape (for example `Continue-Works`). |
| `duplicate event id` | Another event of the chapter already uses this `id`. |
| `duplicate option id` | Two options share an `id`. |
| `unknown status` | `status` is not one of the four values. |
| `must be after from` | `to` is on or before `from`. |
| `unknown scope` | `scope` is not `national`, `region:<place>` or `fate:depopulated`, `fate:destroyed` or `fate:built_over`. |
| `unknown place '` | `place`, or the place of a `region:` scope, is not in `places.yaml`. |
| `unknown placeholder` | A `{...}` other than `{village_en}` or `{village_ar}`, or one of those outside a `fate:*` event. |
| `stray '{' or '}' outside a {placeholder}` | A brace that is not a placeholder. |
| `at least one option required` | `options` is empty. |
| `unknown target` | An effect `target` is not a family resource. |
| `must be a finite number` | An effect `delta` is not a number. |
| `must be a list` | A list field (such as `tags:`) written with no value. |
| `empty entry` | A list with an empty item (`- `). |
| `must be valid text (no lone surrogate)` | A text that is not valid Unicode. |
| `a chapter needs at least one event` | The chapter file has an empty `events:` list. |
| `required with reviewed_by` | `reviewed_by` without `reviewed_on`. |
| `must be the reviewer's GitHub handle` | `reviewed_by` is an email, an @-name, a display name or a bot account. |
| `required (the name shown to the player)` | A resource or place with no name, or a currency with no name or symbol in both languages. |
| `lon/lat outside Mandate Palestine` | A place's coordinates are wrong. |
| `at least one source, each an https:// URL` | A place, fate or currency row with no `https://` source. |
| `currencies for` | A month of the chapter with no currency, or with two, in one region (`currency.yaml`). |
| `missing file` | A chapter, `model.yaml`, `places.yaml`, `fates.yaml` or `currency.yaml` is missing. |

A full message looks like this (an effect target misspelled `lnad`):

```
events/1947_1949.yaml: events[e1947_11_partition].effects.lnad: unknown target
```

## Running the tests

From the repository root, with the .NET SDK installed ([dev-setup.md](dev-setup.md)):

```
python3 tools/tsv_to_events.py --check
dotnet test sim/StoryOfTheLand.sln
```

The sim tests load every chapter, so any error above shows up here, and they must finish with 0 warnings. They also play
the family goldens (`content/palestine/goldens.yaml`, see [determinism.md](determinism.md)): a change to a chapter's
events moves a golden's hash. Say so in your pull request; the maintainer updates the hash.

The Python checks (`uv run --with pytest==9.1.1 --with pyyaml==6.0.3 pytest scripts/oss/tests -q`, and
`uv run scripts/oss/check_repo.py`) cover repository policy. They include a test that keeps this guide's field tables
in line with the loader.

## Checklist

- [ ] the row is in the chapter's TSV, and `tools/tsv_to_events.py --check` passes
- [ ] `id` unique and lowercase; no names typed into the text; `{village_en}` / `{village_ar}` only in a `fate:*` event
- [ ] every fact has a `https://` source with the day you checked it
- [ ] no invented quotes; `sensitive` only where it applies
- [ ] `dotnet test sim/StoryOfTheLand.sln` passes
- [ ] a line in `CHANGELOG.md`
