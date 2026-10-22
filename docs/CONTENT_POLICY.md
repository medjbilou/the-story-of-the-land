# Historical content policy

The Story of the Land (حكاية الأرض) puts real people, real places, real killings and real losses on screen. This page is
the contract for every word, number and picture in the game: the chapter files (`content/events_*.tsv`,
`content/choices_*.tsv`, `content/village_fates.tsv` and `content/key_places.tsv` at the repository root), the
narration scripts, the UI strings and the memorial. It binds the maintainer, contributors and AI agents alike. It is
written from the accuracy guardrails in `reports/Palestine game research dossier.md` (the table "Accuracy guardrails:
the traps a writer is most likely to fall into", which is part of this policy) and from the practice recorded in
`audits/*.md`. Paths in this page are from the repository root.

## 1. The perspective is Palestinian, and it says so

The player is a Palestinian family and village, followed across generations, from late Ottoman Palestine to today.
Narration and terminology use the Palestinian framing: the Nakba, the Naksa, the occupation, the settlements, the
right of return, and the wall "which Palestinians call the Apartheid Wall". Arab places keep their Arabic names; Hebrew
names are used only for Zionist colonies, Israeli settlements, people and bodies, or where the text explains a
renaming. Israeli operation names stand in quotation marks, attributed to Israel, beside the Palestinian names. The
Zionist and Israeli state narrative is not adopted: its statements appear only as attributed claims (section 2, rule 1).

The perspective is not a licence to bend a fact. Precision strengthens the Palestinian story; an inflated or unsourced
figure weakens it. So contested tolls are shown as ranges and competing accounts are shown
side by side, each attributed by name (section 2, rule 5).

This is a design decision by the maintainer. Issues and pull requests that ask to change the perspective will be closed
with a link to this section. That is not a judgement on the person asking, and it is not up for vote. A derivative project can
make a different choice under its own name (see [TRADEMARK.md](../TRADEMARK.md)).

## 2. What holds regardless of perspective

These rules protect the game and the people in it. They are not a viewpoint:

1. **Every fact is sourced.** Every date, number, name and quotation in an event, choice or village-fate row comes from
   the URLs in its `sources` column, or from `reports/` and `research_notes/`, and every URL was opened when the row was
   written or changed. A figure that cannot be traced to an opened source stays out until one is found, and claims the
   notes mark **UNVERIFIED** stay out (the dossier's guardrail on unsourced "facts"). Prefer the primary record: the UN
   (OCHA, OHCHR, the Commissions of Inquiry, UNRWA, UNISPAL), the ICJ, the Palestinian Central Bureau of Statistics and
   Ministry of Health, and rights groups such as Al-Haq, PCHR, B'Tselem, HRW and Amnesty. Wikipedia may be used for an
   overview; where it is the source, the row cites it and the notes mark it for upgrade to a primary source, and that
   is why the content is CC BY-SA 4.0.
   - **No Israeli media as a source.** Israeli newspapers, broadcasters and news sites are not cited. B'Tselem and other
     Israeli human-rights groups are cited by name as such, and Israeli historians by name and work.
   - **Israeli state and army statements are claims.** A figure or statement from the Israeli government, army,
     police or security services is labelled as a claim ("زعم الجيش", "an Israeli government report claimed"), never
     stated as fact. Where the claim's own document cannot be fetched, it may be cited through a non-Israeli report of
     it.
2. **No invented quotes.** No words are attributed to a real person or document unless they are sourced verbatim.
   Event text describes; it does not put speech in anyone's mouth. Quotations are short: a phrase used as a term
   (up to about five words) or at most about two short sentences. A quotation from a non-Arabic text uses its official
   Arabic text or a standard published Arabic translation; where none exists, it is marked as the game's own
   translation (`audits/arabic_1880_1946.md`, "Quotations").
3. **Choices never reward harm to civilians.** No choice, outcome or effect grants a resource or a score for killing,
   expelling or harming civilians, from any side. The `effects` column holds design values, not facts, and a choice
   on a killing rewards nothing violent.
4. **Resistance and civilian harm are told truthfully.** Resistance, armed and unarmed, is named with its organisation,
   its acts and its cost. Attacks on civilians are stated plainly by any side, with their source and toll: neither
   glorified nor erased. Text about an attack on Israeli civilians uses no glorifying vocabulary (for example no
   "استشهادي" or "عملية" for a bombing), names the act, and gives the toll. «استُشهد» / «شهيد» is used, as Palestinians
   use it, for Palestinians killed by the occupation and for those killed resisting the occupying state's forces, never
   for a person killed while attacking civilians (owner ruling, 22 October 2026). Text about 7 October 2023 neither denies
   the killings nor repeats claims shown to be false (the "40 beheaded babies" claim), and keeps the Commission of
   Inquiry's finding that at least 14 civilians were likely killed by Israeli fire. Contested causes stay contested
   (al-Ahli, 17 October 2023). Findings are attributed to who made them: the ICJ ordered provisional measures and has
   not ruled on genocide; the UN Commission of Inquiry, Amnesty, B'Tselem and others concluded genocide; apartheid
   findings belong to B'Tselem, HRW, Amnesty and UN human-rights experts, not to "the UN" or the ICJ.
5. **Every casualty figure names its counter.**
   - Each toll says who counted it and to what date: "according to OCHA", "B'Tselem counted", "the Ministry of Health
     recorded".
   - Where counters differ, the row shows them source against source, each named (for example PCHR 8 against HRW 9).
     Contested tolls (Tantura, Deir Yassin, Lydda, Sabra and Shatila, Jenin 2002, the villages) are given as a range
     with every counter named. Early figures that were never substantiated (Deir Yassin's 254, Jenin's "200+") are
     shown as unsubstantiated, if at all.
   - Tolls are compared like with like: civilians with civilians, all dead with all dead, the same period with the
     same period. They are never added together across counters.
   - Gaza: **74,480** is the number of people recorded killed (Ministry of Health, 19 October 2026); **72,835** is the
     number identified by name (to 7 May 2026, the Tech For Palestine dataset the memorial uses). They are different
     counts and are never merged, summed or swapped. 72,835 is never shown as the death toll.
   - Archive Genocide (archivegenocide.com) is used only for captions and links to footage. Figures in its captions
     and casualty fields are testimony, never game figures, and its fields are never summed. No clip is shown in the
     game until a person has viewed it and checked its provenance (`audits/archivegenocide_links_2023_2026.md`).
6. **AI-assisted writing and AI narration are disclosed.** Text drafted with AI is a draft until every factual claim
   in it has been checked against its sources. The narration is synthetic: it is made offline with local text-to-speech
   models from a fully diacritized script, and the game says so in its credits and in its README.
   No real person's voice is cloned without a written release that covers synthetic generation, scope, withdrawal,
   credit and payment. Nothing is generated live while you play. The People screen's portraits are AI-made ink-and-wash
   drawings (owner decision, 21 October 2026): drawings, never photographs or footage, with no weapons, flags or
   wreaths; a symbolic still life stands in where no face is known or where a picture of a killed child would intrude.
   They are disclosed in `docs/ai-disclosure.md` and the credits. No other AI-generated likeness of a real person is
   used. The game's music is AI-generated with Google Lyria (owner decision, 22 October 2026) and disclosed the same
   way, in `docs/ai-disclosure.md`, the credits and the README.
7. **A sign-off names a person.** Each audit in `audits/` records its reviewer and date. An AI reviewer is named as
   one (for example `arabic-reviewer` (AI)). A human sign-off, such as the native editor's read in rule 8, is recorded
   by that person, with their name and the day; an AI agent never records or changes a human sign-off.
8. **Arabic quality is a release gate.** Every Arabic string (event and choice text, UI strings, narration scripts,
   captions, memorial labels) passes the `arabic-reviewer` audit (`.claude/agents/arabic-reviewer.md`) with zero
   blockers and zero majors, and the findings and their fixes are recorded in `audits/`. Narration sent to TTS is
   fully diacritized. After the audit, a native Palestinian Arabic speaker with editorial experience reads the text;
   no chapter is released before that read.
9. **Sensitive events carry a content note.** A row about a massacre or killings has `sensitive` set to `1`. The
   game then shows a content note, "تنبيه: يتناول هذا الحدث القتل والعنف." ("This event deals with killing and
   violence."), at the top of the event card, before the event text. When in doubt, a row is marked sensitive.

## 3. How to dispute a fact

Facts are open to correction. The perspective (section 1) is not.

1. Open an issue with the **Historical accuracy** form. Name the file, row and field (for example
   `content/events_2000_2023.tsv`, `e2002_04_jenin`, `text_en`), say what is wrong, and give at least one source the
   reader can check. A source is a published book or article, an official record, a UN or rights-group report, or a
   reputable reference work. A link to a social-media post is not a source, and neither is an Israeli media report
   (section 2, rule 1).
2. The maintainer opens the source, checks it against the row's current sources and answers in the issue. When
   reputable counters disagree, the row shows both, each named (section 2, rule 5), and the issue records why.
3. A correction lands as a pull request that changes the text in both languages **and** the `sources` column, with
   the decision recorded in the chapter's audit file in `audits/`. Changed Arabic goes through the `arabic-reviewer`
   audit again (section 2, rule 8).
4. The maintainer's decision closes the dispute. Re-opening the same dispute without a new source is off-topic under
   [CODE_OF_CONDUCT.md](../CODE_OF_CONDUCT.md).

## 4. What contributors must not add

- Content without sources, with a source that was not opened, or with a source that does not support the claim.
- Israeli media as a source, or an Israeli government, army or police statement stated as fact rather than as a claim.
- One number for a contested toll, a toll without its counter, a sum across counters, the 72,835 named people
  presented as the death toll, or a figure taken from Archive Genocide captions or fields.
- Quotes, speeches or letters that are not sourced verbatim, and long quotations.
- Mechanics or text that reward or celebrate violence against civilians, from any side, or that erase it.
- Zionist framing presented as neutral: for example "Judea and Samaria", "security fence", "terrorists" or
  "infiltrators" for Palestinians, or Hebrew renamings of Arab places, outside a quotation or an explained renaming.
- Apocryphal anecdotes as fact (the dossier's guardrails list known ones).
- Footage from the archive before a person has checked its provenance, AI-generated likenesses of real people other
  than the disclosed People-screen drawings (section 2, rule 6), and undisclosed synthetic voices.
- Hate speech, slurs or dehumanising language about any group, Jews and Israelis included, in content, code comments,
  issues or commits.
