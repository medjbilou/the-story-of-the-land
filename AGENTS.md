# AGENTS.md

This repository is The Story of the Land (حكاية الأرض). It tells the history of Palestine from the Palestinian perspective: the player is a Palestinian family and its village, followed across generations. It is a content-heavy history project with strict sourcing and review rules.

## Mission

Help the project stay historically grounded, readable, and technically consistent. Do not treat this as a generic game repo or a casual writing sandbox. The bar is higher because the game presents real people, dates, institutions, war, and political stakes.

## Core rules

- The game design is [`docs/palestine-model.md`](docs/palestine-model.md) and the plan is [`docs/PLAN.md`](docs/PLAN.md). Follow them. Anything they leave open needs an owner decision before it is built: do not invent values or rules for it.
- Every historical fact must be sourced. If a fact is not supported by a cited source, do not add it.
- No invented quotes. Paraphrase, and never put words in a real person’s mouth.
- Use the Palestinian perspective and terminology set in `docs/CONTENT_POLICY.md`, section 1. It is a stated design decision and is not reopened in content work.
- No Israeli media as a source (`docs/CONTENT_POLICY.md`, section 2, rule 1). A statement by the Israeli government, army, police or security services is an attributed claim, never a fact.
- Every casualty figure names its counter and date. Where counters differ, show each one by name; never sum across counters (section 2, rule 5).
- Sensitive events must remain tagged and reviewed appropriately. Do not bypass the project’s release and review gates.
- Preserve the project’s deterministic simulation rules. Game mechanics belong in the sim, not in presentation-only code.
- If a change touches `content/`, `sim/`, or `unity/`, align with the repo’s tests, changelog, and review conventions.

## What to read first

Before writing or editing content, review:

- `docs/PLAN.md` (the plan)
- `README.md`
- `CONTRIBUTING.md`
- `docs/CONTENT_POLICY.md`
- `docs/WRITING_EVENTS.md`
- `docs/WRITING_GUIDE.md`
- `../.claude/agents/arabic-reviewer.md` (the Arabic audit every Arabic string passes)
- `docs/palestine-model.md` (how the chapter files in `../content/` become `content/palestine/`)

## Content-writing rules

- The chapter files are `../content/events_*.tsv` and `../content/choices_*.tsv`. Edit them there, then run `python3 tools/tsv_to_events.py && python3 tools/tsv_to_events.py --check`; never hand-edit `content/palestine/events/`.
- Arabic first: write `title_ar` and `text_ar` in clear Modern Standard Arabic, then the English with the same facts, figures and dates. Use the Palestinian Levantine month names (كانون الثاني، شباط، آذار، نيسان، أيار، حزيران، تموز، آب، أيلول، تشرين الأول، تشرين الثاني، كانون الأول) and Western digits.
- Arab places keep their Arabic names; Israeli operation names stand in quotation marks, attributed to Israel.
- Keep wording concrete and credible. Prefer clear historical description over melodrama.
- Show English dates as `15 May 1948` style, not shorthand or invented formatting.
- Prefer short, grounded event text over rhetorical filler.
- Treat AI-generated prose as a draft, not a source of truth. A human must verify factual claims.

## Tone and style

- Direct, specific, and historically grounded.
- Calm, not sensational.
- Explain consequence and trade-off, not just crisis.
- Write for the player as the family living through the event, not as a spectator of a cinematic script.
- Favor clean factual precision over generic historical voiceover language.

## Typical workflow for a task

1. Understand the exact historical question or gap.
2. Check the relevant files in `content/` and existing event patterns.
3. Gather the source or citation for the fact before drafting.
4. Add or edit the event or narrative text in the repo’s expected format.
5. Run the validation commands below.
6. Keep the PR and change focused.

## Validation commands

- `dotnet test sim/StoryOfTheLand.sln` loads every chapter in `content/palestine/` and must pass with 0 warnings.
- `uv run scripts/oss/check_repo.py` runs the repository policy checks that CI runs.
- If you change `sim/`, `content/` or `unity/`, add a line under `## [Unreleased]` in `CHANGELOG.md`.
- If the PR finishes a part of `docs/PLAN.md`, update the Status table in `README.md` (`CONTRIBUTING.md`, rule 9).
- `scripts/check.sh` runs every check CI runs in one command: the changelog rule, the reviewed_by guard, the policy checker tests, `check_repo.py`, the rule IDs, the sim build, the sim tests and the tools tests. It stops at the first failure.

## Helpers (they write or report, they are not checks)

- `uv run scripts/source_report.py [--older-than DAYS]` lists every content source with its check date and age.
- `uv run scripts/id_references.py <id>` lists every place a record id is read in content, sim and unity (code reads some ids by name).
- `uv run scripts/oss/md_links.py` lists relative Markdown links that point at a missing file (it also runs in the policy tests).

## Git

- Never run `git commit` or `git push` unless the user asks for it in that message.
- Stage and commit only the files the user names or the task clearly covers.

## Engineering (sim, tests, tools)

For changes under `sim/`, `tools/`, `scripts/` or `unity/`, not only content.

**Layout**
- `sim/StoryOfTheLand.Sim/` is the game rules library (netstandard2.1, so Unity can use it). `sim/StoryOfTheLand.Sim.Tests/` holds the tests (net10.0). The tests load the real chapters in `content/palestine/`.
- `unity/` only draws and takes input. Rules never live there (`CONTRIBUTING.md`).

**Checks** (run them before every PR; the exact commands are in `CONTRIBUTING.md`, rule 3)
- `dotnet test sim/StoryOfTheLand.sln`: .NET SDK 10.0.401 (`docs/dev-setup.md`); it must end with 0 warnings.
- The tools tests (`pytest tools/tests`), the policy checker tests (`pytest scripts/oss/tests`) and `uv run scripts/oss/check_repo.py`. These and the sim tests need no Unity.
- CI runs them as `Sim (dotnet test)`, `Tools (pytest)` and `Repo policy`, plus `Changelog`. Unity EditMode tests, WebGL builds and `tools/web_check.py` need Unity and are not in CI. If your change touches `unity/` or code Unity compiles and you cannot run them, say so in the PR; do not claim they passed.
- When you do run Unity: wrap the command in `uv run tools/machine_lock.py --exclusive --wait-unity -- ...` (one Unity at a time: a 16 GB Mac froze running several), never add `-nographics` to an editor run, and stop a stuck Unity by its PID. Afterwards drop the settings churn with `git checkout -- unity/Assets/Settings unity/ProjectSettings/ProjectSettings.asset`; it restores from the index, so stage intended settings changes first (`QualityTiers.Apply` and `ProjectSetup.Apply` write them), and never commit the `UNITY_MCP_*` scripting defines.

**Rule IDs** (`docs/rules/README.md`)
- A rule with an ID like `R-ECON-014` is cited by that ID in its test (`// R-ECON-014`), in YAML comments and in the PR body ("implements R-ECON-014"). Run `uv run scripts/oss/rule_ids.py`; it fails on an untested rule and on a cited ID no rule defines.

**Determinism** (`docs/determinism.md`)
- The sim must give the same state, bit for bit, on .NET, Mono and IL2CPP: no new RNG, no trigonometry, no unordered iteration into state, no culture-dependent formatting.
- `GoldenRun.Hash1950` and `Hash1952` must not move unless the change is a deliberate rules or content change. The browser smoke value also moves when the UI path changes; re-record it then. If they move, update every place the doc lists in the same commit and say why in the message and the PR.
- A content edit changes the content hash and old saves then warn; that is expected and is not a golden change.

**Pull requests**
- Branch from `main`, one focused change per PR, no force-push to `main`. Fill in `.github/pull_request_template.md`, including "AI assistance". Use "Fixes #N" only if the PR completes the issue, otherwise "Refs #N".
- Add a line under `## [Unreleased]` in `CHANGELOG.md` for any change to `sim/`, `content/` or `unity/` (tests excepted).
- Write the test first and check it fails without the change.

**Leave to the maintainer**
- The perspective in `docs/CONTENT_POLICY.md` section 1, sensitivity tags and the release gates.
- Anything that needs a design call (for example which date a rule is read at). Put the question in the PR description with the options and what each costs.

## Red flags

- Unattributed claims or vague historical language
- An Israeli media source, or an Israeli official statement stated as fact
- Zionist framing presented as neutral ("Judea and Samaria", "security fence", Hebrew renamings of Arab places)
- Fabricated quotes or supposedly verbatim speeches
- Stating a number, date or claim without a source that supports it
- Softening historical pain or atrocity into game flavor text
- Generic AI filler like “in today’s rapidly evolving landscape” or “it is important to note that”

## Files that define quality in this repo

- `docs/CONTENT_POLICY.md`: the Palestinian perspective and the factual standards
- `docs/WRITING_EVENTS.md`: required YAML structure for content
- `docs/WRITING_GUIDE.md`: tone, style and writing expectations
- `CONTRIBUTING.md`: repo workflow and validation standards
- `docs/SOURCING_FOR_CONTRIBUTORS.md`: a sourcing checklist, and what to do when a source cannot be reached

Keep the work in scope, sourced, and readable.
