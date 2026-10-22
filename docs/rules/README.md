# Rule IDs

Every rule in the game design gets a stable ID, like `R-FAM-014`: `R-`, a 2 to 6 letter area, a three digit number.
Rewording a rule keeps its ID. A removed rule's ID is not reused.

**Where an ID is written.** A rule is defined in a Markdown file in this folder (`docs/rules/*.md`, not this README), on a line that
starts with the ID:

```
R-FAM-014: A national event reaches every family.
- **R-FAM-015** A region event reaches the villages within 25 km of its place.
## R-FAM-016 Fate cards
```

**How an ID is cited.** Write the ID in a comment next to the thing that builds or checks the rule: a C# test
(`// R-FAM-014`), a YAML comment in `content/`, or the PR body ("implements R-FAM-014").
A comment in `content/` is still a content change: it moves the content hash, so an older save loads with the
"different game content" warning, and the PR needs a `CHANGELOG.md` line or the `no-changelog` label. The goldens do not move.

**What the check does.** `uv run scripts/oss/rule_ids.py` fails when:

- a rule defined here has no citing test in `sim/StoryOfTheLand.Sim.Tests` (untested),
- an ID cited in `sim/`, `unity/Assets/` or `content/` is defined nowhere (dead), or
- an ID is defined twice.

Only `sim/StoryOfTheLand.Sim.Tests` counts as a test, because CI does not run the Unity tests.
It runs in CI as one of the policy checker tests (`pytest scripts/oss/tests`) and in `scripts/check.sh`. Prose that only mentions an ID mid-line is not a definition.
