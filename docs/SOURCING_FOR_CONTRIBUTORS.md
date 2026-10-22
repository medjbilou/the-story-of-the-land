# Sourcing: a checklist for contributors

The rules live in `docs/CONTENT_POLICY.md` (section 1 perspective, section 2 sources) and `docs/palestine-model.md` (the chapter files' columns). This page is the short version for the moment you are looking for a source, plus what to do when you cannot reach one.

## Before you write a fact

- [ ] Every fact has a `https://` source and the day you checked it. In a chapter file (`../content/*.tsv`) that is the URLs in the row's `sources` column, opened on the file's "Compiled" date; in YAML it is `sources: - { url, checked }`.
- [ ] You opened the page and read the claim there. A search snippet or a summary is not a source.
- [ ] If the fact is a date, the source gives that date, not a nearby one. A term's `sources:` must support the date the term carries (a source for the announcement does not support the start date).
- [ ] No Israeli media as a source: not Times of Israel, the Jerusalem Post, Ynet / Yedioth, Haaretz, Maariv, Israel Hayom, i24, Arutz Sheva, Kan, Channels 12–14, Walla or +972 (`docs/CONTENT_POLICY.md` section 2, rule 1). B'Tselem and other Israeli human-rights groups are cited by name as such, and Israeli historians by name and work. If only Israeli media supports a claim, the notes mark it **UNSOURCED (Israeli media only)** and it stays out of the content.
- [ ] A statement by the Israeli government, army, police or security services is a claim: cite the statement itself, or a non-Israeli report of it, and label it as a claim in the text.
- [ ] Every casualty figure names its counter and date. When counters differ, the row shows each one, named; a contested toll is a range; counters are never summed (section 2, rule 5). Record the disagreement in the PR.
- [ ] No quotes unless verbatim and sourced. Paraphrase otherwise.
- [ ] Images: portraits are public-domain, CC0, CC BY 4.0 or CC BY-SA 4.0 photographs; a portrait cropped from a CC BY-SA 4.0 photograph is itself released under CC BY-SA 4.0 and credited in `NOTICE`, and any other licence is refused (`THIRD-PARTY-NOTICES.md`). A file whose only licence is a PD-Israel tag on Wikimedia Commons needs a decision first (D-P in `docs/oss/DECISIONS.md`); ask before using one. AI-generated likenesses of real people are never used (`docs/CONTENT_POLICY.md` section 2, rule 6).

## Where to look

- First `../reports/` and `../research_notes/`: the dossier and the notes already hold opened sources for most chapters.
- The primary record named in `docs/CONTENT_POLICY.md` section 2, rule 1: the UN (OCHA, OHCHR, the Commissions of Inquiry, UNRWA, UNISPAL), the ICJ, the Palestinian Central Bureau of Statistics and Ministry of Health, and rights groups such as Al-Haq, PCHR, B'Tselem, HRW and Amnesty.
- Academic work, such as the Institute for Palestine Studies (palestine-studies.org) and the *Journal of Palestine Studies*, and non-Israeli news agencies and outlets (AP, Reuters, AFP, Al Jazeera, Middle East Eye) for reporting.
- Israeli laws, court rulings and official reports are official records: cite them as such, and what they assert as a claim.
- Wikipedia may be used for an overview. Cite the exact article and, for a number or date, the footnote it rests on, and mark it in the notes for upgrade to a primary source.
- When you add primary or official sources to a record that cites Wikipedia, drop a Wikipedia article only if the new sources cover every line it supported. Otherwise keep it, and say in the PR which lines still rest on it.
- Knesset plenary records and laws are PDFs on `fs.knesset.gov.il`.

## When you cannot reach a source

Some sources fail for the reader, not because the fact is wrong. Seen so far:

- `fs.knesset.gov.il` PDFs and `main.knesset.gov.il` pages can return an HTML "geo-block" page to non-Israeli networks or to scripted requests, with a 200 status. If you saved a "PDF" and it opens as HTML, that is what happened.
- Long PDFs can be cut off by a fetch tool, so the end of a sitting (a final vote tally) is missing from the text you got.
- Some sites block scripted requests or return 403; the Wayback Machine (`web.archive.org`) often has the page. It too can be offline.
- Wikimedia Commons' API limits fast clients; use a descriptive User-Agent and pause between calls.

What to do, in this order:

1. Try another route to the same record: another page, the Arabic or English version, the printed page numbers, an official mirror, a Wayback copy.
2. If you still cannot read the claim, do **not** fill the gap from memory or from a nearby date. Leave the value as it is (or leave the field out) and write in the PR and in any research note (`research_notes/`): what you wanted, which URLs you tried, what each returned ("geo-block page", "text stops at item 20", "403"), and what is still open.
3. A secondary source may carry the fact. Cite it as what it is and say in the PR that the primary source was not reachable. The maintainer decides whether that is enough.
4. If a reviewer cannot open your source either, say so, and attach what you saw (for example the page text you read, with the URL and the day).

## Disclose how you worked

If an AI assistant helped, say so under "AI assistance" in the PR (`CONTRIBUTING.md`) and say which claims you checked yourself against which pages.
