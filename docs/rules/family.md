# Rules of the family game

The rules of the family game (docs/palestine-model.md) that have IDs. An ID is never reused.

## Saves

R-SAVE-001: A save names the ruleset it was played under (`ruleset family-0.1`, save schema 3, with the family's
village). The game refuses a save made under another ruleset: a save of an older ruleset (`ruleset legacy` or
`ruleset gdd-0.1`, or a schema 1 or 2 save) is refused as made under earlier rules. The refusal carries a code
(`older_rules`) the UI translates, and the sim's reason does not itself ask for a new game: the player reads that once (N4).

R-SAVE-002: A save that names a ruleset this build does not know, or a newer save schema, was made by a newer version of
the game and is refused with the code `newer_version` (N2).

## Release gate

R-GATE-001: An event with a sensitive tag (`holocaust`, `terror`, `massacre`, `war-casualties`, `displacement` or
`sensitive`) must have `status: sensitivity-reviewed` before a shareable build. The release gate lists every such event
of every chapter whose status is not reviewed, and `Build -shareable` refuses the build (Q-GATE-1(c), owner ruling of
18 October 2026).
