---
name: storyoftheland-historical-content
description: 'Research, draft, and review historical events and political writing for The Story of the Land. Use for source-checking claims, event drafting, content gaps and chapter narrative work.'
argument-hint: 'event topic / date range / source question / historical gap'
user-invocable: true
disable-model-invocation: false
---

# Historical Content

## When to use

- drafting or revising event text in the chapter TSVs (`../content/events_*.tsv`, `choices_*.tsv`)
- finding historical gaps in a chapter's timeline
- checking whether a claim is source-backed and suitable for the repo
- editing player-facing writing for historical clarity and tone

## Core repo rules

Before drafting, read these first:

- [README.md](../../../README.md)
- [CONTRIBUTING.md](../../../CONTRIBUTING.md)
- [docs/CONTENT_POLICY.md](../../../docs/CONTENT_POLICY.md)
- [docs/WRITING_EVENTS.md](../../../docs/WRITING_EVENTS.md)
- [docs/WRITING_GUIDE.md](../../../docs/WRITING_GUIDE.md)

## Mandatory workflow

1. Identify the event, date range, or gap.
2. Check the existing events of the chapter (`content/palestine/events/<chapter>.yaml`, generated from the TSVs) and the places and fates (`content/palestine/places.yaml`, `fates.yaml`).
3. Verify the factual claim with a source before drafting.
4. Draft only in the repo’s historical framing and event schema.
5. Keep the text grounded, specific, and readable in a game UI.
6. Leave `status: draft` on anything an AI drafted. Only a person who has checked every claim against its source sets `fact-checked`.
7. Validate with `dotnet test sim/StoryOfTheLand.sln` when editing repo content.

## Historical writing standards

- No invented quotes or verbatim speeches without direct source support.
- Never type a family member's name; a fate card names the family's village only through `{village_en}` / `{village_ar}`.
- Prefer concrete social, legal and military context over generic historical mood-setting.
- Keep war and atrocity language precise and sensitive.
- Favor what the event meant for a family and its village over dramatic filler.

## Research checklist

- Is the claim connected to a real date and a real event?
- Is the wording supported by a cited source or a source packet?
- Does it fit the historical period and the place?
- Does it avoid unsupported generalization or invented drama?
- Does it follow the project's Palestinian perspective and source policy (`docs/CONTENT_POLICY.md`, sections 1-2)?

## Skill bundle contents

This skill package is intentionally structured like a real agent skill bundle, not a single markdown note.

- `scripts/` contains quick helper commands for checking source coverage and event completeness
- `templates/` contains YAML and checklist scaffolds for drafts
- `examples/` contains small examples of useful historical gaps and workflow patterns
- `references/` contains the supporting checklists and repo reference docs

## References

- [Historical writing guide](./references/historical-writing-checklist.md)
- [Content policy](../../../docs/CONTENT_POLICY.md)
- [Event schema](../../../docs/WRITING_EVENTS.md)
- [Event draft template](./templates/event-draft.yaml)
- [Source coverage script](./scripts/check_source_coverage.py)
