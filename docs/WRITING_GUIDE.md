# Writing guide for The Story of the Land

This guide is the repo’s practical writing standard for event copy, historical cards, and narrative text. It complements the stricter rules in `docs/CONTENT_POLICY.md`, the Arabic audit in `../.claude/agents/arabic-reviewer.md` and the chapter files' columns in `docs/palestine-model.md`.

## 1. Purpose

The writing should make the player feel what a Palestinian family and its village lived through, from late Ottoman Palestine to today, without drifting into melodrama, invented speech, or unsupported certainty.

The standard is writing that helps the player understand what happened, who did it, what it cost, and what choice was before the family.

## 2. Core voice

The project voice is:

- direct
- grounded
- historically specific
- politically literate
- sober but readable

Use short, concrete sentences when possible. Favor verbs that tell the player what changed, not abstract historical summary.

Good patterns:

- Name who acted: “the army closed the road”, not “the road was closed”.
- Name who counted: “OCHA counted ten injured; the Palestinian Red Crescent said it treated about 100”.
- Attribute a claim: “the Israeli army claimed …”, “زعم الجيش …”, never the claim as fact.

## 3. Tone guidelines

### Preferred tone

- credible
- tense but precise
- matter-of-fact about loss, expulsion and violence
- clear about trade-offs and consequences

### Avoid

- inflated heroics
- generic nationalism without action
- vague political theater
- AI-flavored filler
- moralizing summaries that flatten complexity

## 4. Historical perspective

The project tells the story from the Palestinian perspective, as set out in `docs/CONTENT_POLICY.md`, section 1. That choice is settled and is not reopened in event copy. It is not a licence to bend a fact: precision strengthens the story, and an inflated or unsourced figure weakens it.

- Use the Palestinian terms: the Nakba, the Naksa, the occupation, the settlements, the right of return, and the wall "which Palestinians call the Apartheid Wall".
- Arab places keep their Arabic names. Hebrew names are used only for Zionist colonies, Israeli settlements, people and bodies, or where the text explains a renaming.
- Israeli operation names stand in quotation marks, attributed to Israel, beside the Palestinian names.
- A statement by the Israeli government, army, police or security services is a claim and is labelled as one. Israeli media is never a source (section 2, rule 1).
- A contested toll is a range with every counter named; counters are never summed (section 2, rule 5).
- Attacks on civilians are stated plainly, by any side, with their source and toll (section 2, rule 4).

The writing must also stay honest:

- name the institutions, not just the mood
- identify the real conflict or pressure
- connect the action to a sourceable fact
- avoid turning suffering into a gameplay abstraction

## 5. Event writing rules

When drafting an event card:

- write the Arabic first (`title_ar`, `text_ar`) in clear Modern Standard Arabic, then the English; both carry the same facts, figures and dates
- in Arabic, use the Palestinian Levantine month names (كانون الثاني، شباط، آذار، نيسان، أيار، حزيران، تموز، آب، أيلول، تشرين الأول، تشرين الثاني، كانون الأول), matching the row's `month` column, and Western digits
- explain the situation and the family's choice, if the row has one
- keep the sentence length readable in UI space
- avoid invented speeches or private thoughts
- write English dates in the project’s format, such as `15 May 1948`
- never show internal ids to the player

A strong event card usually does three things:

1. names the problem or situation
2. shows the source of pressure or stakes
3. frames the family’s choice

## 6. Forbidden patterns

Do not write:

- invented direct quotes
- “this unprecedented moment” without evidence
- generic AI filler or explanatory padding
- claims that are not tied to a real source
- emotionally inflated language that obscures the historical fact
- moral language that turns policy into a character judgment
- Zionist framing presented as neutral, such as "Judea and Samaria", "security fence", or "terrorists" for Palestinians, outside a quotation

## 7. Source-backed writing standard

If a sentence is not directly supported by a source, it is not ready for the repo.

The real standard is not “interesting” but “supported, readable, and historically grounded.”

## 8. Good historical writing checklist

Before finalizing a draft, ask:

- Does the text reflect the actual period and power structure?
- Is the decision understandable to a player without prior knowledge?
- Does the wording use the perspective and terms of `docs/CONTENT_POLICY.md`, section 1?
- Is every Israeli official statement labelled as a claim, and is no source Israeli media?
- Does every toll name its counter and date?
- Does it fit the row's columns and its `sources` list?
- Is it concise and legible in the game UI?
- Does it avoid invented quotes or inflated language?
- Has the Arabic passed the `arabic-reviewer` audit, recorded in `../audits/`, before the native editor's read?
