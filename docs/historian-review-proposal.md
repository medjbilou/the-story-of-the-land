# Historian review: proposed process

**PROPOSED.** Nothing on this page is in force. It is a proposal for issue #29
and needs the owner's decisions in [section 6](#6-owner-decisions) before any of it is built. Until then the process is
the one in [HISTORIAN_REVIEW.md](HISTORIAN_REVIEW.md): reviews are recorded in PR comments and YAML comments, and no
event's `status` changes.

## 0. Where this starts from

The planned pipeline:

- `draft` → `fact-checked` → `historian-reviewed`, then `sensitivity-reviewed` for sensitive tags.
- **Release builds include only fully reviewed content.**

The game's perspective is Palestinian ([CONTENT_POLICY.md, section 1](CONTENT_POLICY.md#1-the-perspective-is-palestinian-and-it-says-so)),
and reviewers check facts against sources, not a viewpoint (section 1 below).

What the code does today:

- `ContentLoader.cs` accepts the four status values and rejects any other key on an event, so a review cannot be
  recorded as a new YAML field without a schema change.
- `RealContentTests.EveryHistoricalRecordIsSourcedAndFactChecked` requires every non-sensitive event to be exactly
  `fact-checked` and every sensitive one to be exactly `sensitivity-reviewed`. Setting `historian-reviewed` fails it.
- `ReleaseGate.cs`, `StoryOfTheLand.Cli --release-gate`, the `-shareable` build, `tools/package_release.py` and
  `scripts/oss/release_gate.sh` check sensitivity review only.
- The game's content is the chapter files `../content/*.tsv`, converted by `tools/tsv_to_events.py` into
  `content/palestine/events/`. Every converted event is `draft`: the converter never raises a review status
  ([palestine-model.md](palestine-model.md)). The Arabic of each chapter has an `arabic-reviewer` (AI) audit in
  `../audits/`; no human review of any chapter is recorded yet.

No bar for how many events must be reviewed before a release is set yet (section 6).

## 1. Who qualifies

No names here. The owner chooses reviewers (section 6). A reviewer should meet all of the following:

- **Field.** Academic training in history, or a closely related field with published historical work: the history of
  Palestine and the Palestinians, the Mandate, the Nakba and the refugees, the occupation, or the political, legal or
  social history of the region.
- **Period.** Their own work covers the chapter they review (late Ottoman Palestine to today), or a part of it that
  matches the events they review. A reviewer of the 1948 massacres knows the 1948 record; a reviewer of Gaza since 2023
  knows the UN, rights-group and Ministry of Health counts.
- **Sources.** Reads Arabic, the game's first language, and checks the Arabic and English text. Reading Hebrew helps for
  Israeli archival records and is not required.
- **Track record.** At least one of: peer-reviewed articles or a monograph on the period; a doctorate on it; a
  professional role in an archive that holds the period's records. A popular-history byline alone is not enough, and a
  GitHub label or a self-description is never taken as a credential.
- **Independence.** Has not drafted, sourced or fact-checked the events they review. Is not paid by, and does not
  advise, a party, campaign or organisation with a stake in how a card reads.
- **Conflicts of interest**, declared in writing before the first review and kept with the reviewer record (section 3):
  a family tie to a person the game shows; a current role in a body whose own history is under review (for example an
  official history department reviewing its own operations); a commercial interest in the game; a public dispute with
  an author the card cites. A declared conflict does not disqualify by itself. The maintainer decides, and the
  reviewer leaves the affected events to someone else when the maintainer asks.

**Balance.** The perspective is fixed by [CONTENT_POLICY.md, section 1](CONTENT_POLICY.md#1-the-perspective-is-palestinian-and-it-says-so):
the story is told from the Palestinian perspective, Israeli state and army statements appear only as attributed claims,
Israeli media is not a source, and contested tolls are shown as ranges with every counter named (section 2). Within
that, the panel should not be one person or one school. Aim for reviewers from more than one institution and more than
one sub-field (political, legal, social and oral history), so that a card on a village's fate is not checked only by a
military historian, and so that a disputed toll is seen by someone who knows every count of it.

What a reviewer does not do: sign off the sensitivity review (that stays with the maintainer), certify game-balance
values, or re-open the perspective.

## 2. What a reviewer receives

### 2.1 The packet

One Markdown file per event, produced from a fixed commit, plus the event's YAML at that commit. Markdown because a
reviewer should not need git or YAML to read it. Each packet holds:

1. **Header:** event id, file, full commit SHA, the date the packet was made, current `status`, tags, and whether the
   tags need sensitivity review (`sensitive`, and the inherited `holocaust`, `terror`, `massacre`, `war-casualties`).
2. **Dates:** the event window as `from` and `to`, in words ("shown from 15 October 1948; gone by 23 October 1948"),
   with the note that `from` can be a game turn before the event and `to` is exclusive.
3. **Text as the player sees it:** title, body, every choice and its outcome, in Arabic first and then English, side by
   side, with any `{village_ar}` / `{village_en}` placeholder of a fate template shown as written.
4. **Options:** for each, its text, whether it is marked `historical: true`, whether it is `recommended`, and its
   effects in plain words, for example "Land −0.03, Sumud +0.02 (resources run 0 to 1)". Names come from
   `content/palestine/model.yaml`. The packet says that these numbers are design values and are not under review.
5. **Requirements:** any `requires` line, in words (for example "only after `second_truce` was resolved with `comply`").
6. **Sources:** every URL in the row's `sources` column, with the file's "Compiled" date and its age in days, and the
   matching passage in `../reports/` or `../research_notes/`, if any.
7. **The chapter's audit:** the findings for this row in `../audits/`, verbatim, including every "corrected" note.
8. **Open questions and flagged contradictions** for this event, from a hand-kept list (section 2.3), each with its
   issue link and state: open, fixed (with the PR), or waiting for the maintainer.
9. **A claim list to fill in:** one row per checkable claim (a date, a number, a name, a place, an act), each with
   the field it comes from and an empty verdict column. The person who makes the packet drafts the list; the reviewer
   may add, split or strike claims.

### 2.2 The script

Extend `scripts/event_timeline.py`, which already loads every event, its era, anchor, status and tags. Add:

```
uv run scripts/event_timeline.py --packet <id> [<id> ...] [--out DIR]
```

It writes `<DIR>/<id>.md` for each id (default directory: `build/review-packets/`, not committed). It needs, beyond
what the script reads now: `content/palestine/model.yaml` for resource names, the chapter TSVs and audits,
`source_report.py`'s age calculation (import it rather than copy it), and the open-questions file. It reads only and
writes only into `--out`. A test under `scripts/oss/tests/` builds a packet for one real event and checks that every
section is present and that the Arabic and English text both appear.

Not proposed here: a PDF, or any network fetch. The reviewer opens the source URLs themselves.

### 2.3 Open questions file

`docs/reviews/historian/open-questions.yaml`, kept by hand, one entry per event and field:

```yaml
- event: e1982_09_sabra_shatila
  field: text_ar
  question: "The research notes mark Bayan Nuwayhed al-Hout's 1,390 named victims UNVERIFIED; add it only with an opened source."
  link: <issue URL>
  state: open                     # open | fixed | waiting-for-maintainer
  fixed_by: null                  # PR URL when fixed
```

It is seeded from the corrections and **UNVERIFIED** marks in `../audits/` and `../research_notes/` and updated by the PR
that fixes each one. It lives outside `content/`, so editing it changes no content hash and no golden.

## 3. How a review is recorded

### 3.1 Files

All under `docs/reviews/historian/`, outside `content/` for the same reason as above. Under the folder rule in
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) that text would be CC BY 4.0; the reviewer has to agree to that (section 6).

- `reviewers.yaml`: one entry per reviewer. `id` (a stable short id such as `r1`), `public_name` (or `null` if the
  reviewer asked not to be named), `role` (`historian`), `expertise` (the scope the maintainer confirmed),
  `conflicts` (the declaration, or "none declared"), `since`. No email, phone or address: contact details stay out of
  the repository.
- `<event_id>.yaml`: one file per event, holding every review of it, newest last. A file per event keeps each review
  next to nothing else, gives a clean diff and lets a second reviewer add a record without touching the first.

### 3.2 Fields of one review

```yaml
event: e1982_09_sabra_shatila
reviews:
  - reviewer: r1
    reviewed_on: 2026-11-20
    commit: <full SHA the packet was made from>
    fingerprint: <sha256 of the reviewed fields, section 3.4>
    scope: "All claims in text and options; not the effects"
    claims:
      - claim: "The UN General Assembly condemned the massacre as an act of genocide on 16 December 1982"
        field: text_ar
        sources: ["<URL from the row's sources column>"]
        verdict: supported        # supported | supported-after-change | not-supported | unable-to-verify | out-of-scope
        comment: ""
    requested_changes:
      - field: text
        change: "..."
        resolved_by: <PR URL or null>
    outcome: accepted             # accepted | changes-requested | unable-to-verify
    dissent: []                   # section 4
    signoff: <URL of the reviewer's own sign-off comment>
    accepted_by_maintainer: <PR URL that merged this record>
```

`outcome: accepted` means every in-scope claim is `supported` or `supported-after-change`, every requested change has
`resolved_by`, and `dissent` is empty or the owner has ruled that it does not block (section 6, decision 8).

### 3.3 Sign-off

The sign-off is the reviewer's own act. The reviewer posts it from their own account as a comment on the PR that adds
the record: "I reviewed `<event>` at `<SHA>`; outcome `<outcome>`; the record in this PR is mine." The `signoff` field
holds that comment's URL. A reviewer without a GitHub account sends the same statement to the maintainer, who commits
it with a note of how and when it arrived and keeps the original. The claim list may be drafted by a contributor or an
AI agent; the verdicts, comments and sign-off may not. A code-owner approval is not a historian sign-off.

### 3.4 The fingerprint and `status: historian-reviewed`

The fingerprint is a SHA-256 over a canonical form (sorted keys, LF line ends) of the fields a historian checks:
`title`, `title_ar`, `text`, `text_ar`, `from`, `to`, `scope`, `place`, `tags`, `requires`, each option's `id`, `text`,
`text_ar`, `outcome` and `outcome_ar`, and the list of source URLs. It leaves out `effects`, `checked` dates and YAML
comments, which are design values or bookkeeping. The packet script
prints it, so the packet and the record carry the same value.

An event may carry `status: historian-reviewed` only when all of these hold:

1. it is not sensitive (sensitive events stay at `sensitivity-reviewed`; see below);
2. its file in `docs/reviews/historian/` has at least one review with `outcome: accepted`, a `signoff` URL, and an
   `accepted_by_maintainer` PR;
3. that review's `fingerprint` equals the fingerprint of the event as it is now;
4. the reviewer is in `reviewers.yaml`.

A sensitive event has one status field and needs `sensitivity-reviewed` for the existing gate. Rather than a fifth
status or a second YAML field (both schema changes), its historian review is shown by the same file and the same
fingerprint check, and its status stays `sensitivity-reviewed`. The owner may prefer the pipeline's order, where
sensitivity review comes after historian review and therefore implies it (section 6, decision 7).

**How it is checked.** A new `scripts/oss/historian_reviews.py`, run by `check_repo.py` in CI:

- fails if an event is `historian-reviewed` and conditions 1–4 do not hold;
- fails if a record's YAML is malformed, names an unknown reviewer or event, or lacks `signoff`;
- prints, as information, every sensitive event and whether a matching accepted review exists.

`RealContentTests` changes to allow `historian-reviewed` for non-sensitive events. The release gate gets one more step
in `release_gate.sh` only when the owner decides that review gates release (section 6, decision 6); until then the CI
check keeps the status honest and nothing more.

**When the text changes later.** Any edit that changes the fingerprint (new wording, a moved date, a changed place, a new or removed source) makes the review stale. The CI check then fails, so the PR that makes the edit must
do one of two things: set the event back to `fact-checked`, or add a new accepted review at the new fingerprint. Old
records stay in the file; they apply to the commit they name and to nothing later. Effect and balance changes do not
invalidate the review, but the packet for any later review lists them. A sensitive event whose text or choices change
also returns to the maintainer for sensitivity review.

## 4. Disagreements

**Reviewer against the sources.** The reviewer says a cited source is wrong or does not support the claim and gives a
checkable passage. That becomes a **Historical accuracy** issue under
[CONTENT_POLICY.md, section 3](CONTENT_POLICY.md#3-how-to-dispute-a-fact). Where reputable counters disagree, the row
shows each one, named (section 2, rule 5); otherwise the maintainer picks the sourced value and records why. If the value changes, the reviewer rechecks the changed claim
(`supported-after-change`).

**Reviewer against the perspective.** Section 1 of the content policy is not up for review. A comment that the card
takes the Palestinian view or uses Palestinian terms is recorded as `out-of-scope`, does not block, and is not answered
as a dispute. The line is between perspective and fact: "the term is one-sided" is perspective; "this date is wrong",
"this number is not in the source", "this toll does not name its counter" and "this omission makes the sentence false"
are facts and are in scope. The rules in section 2 of the policy (sourcing, no Israeli media, attributed claims, named
counters, no invented quotes, no reward for harm to civilians) are in scope: a reviewer who finds a breach reports it.

**Between reviewers.** Both records stay in the event file. The event cannot reach `historian-reviewed` while any
in-scope claim has a `not-supported` verdict from a reviewer that is not resolved. The maintainer may change the
value, cut the claim from the card, or ask a further reviewer; the further review is added, not substituted.

**Reviewer against the maintainer.** On a fact, the maintainer's decision under section 3 of the policy closes the
dispute for the game. If the reviewer still disagrees, the record keeps it:

```yaml
    dissent:
      - claim: "..."
        reviewer_position: "..."
        maintainer_ruling: <issue URL>
```

Whether a recorded dissent blocks `historian-reviewed` is decision 8 below. This proposal recommends that it does: a
status that says a historian checked the card should not stand over that historian's written objection. On design
values (effects, which option is `recommended`), the reviewer may flag a value as implausible; the maintainer decides,
and the flag goes in the PR, not in the review record.

**Escalation.** Issue, then the maintainer. There is no outside body. The owner's decision is final for the game; a
dissent stays public in the record unless the reviewer asked not to be named, in which case it is kept under their
reviewer id.

## 5. The first batch

Chosen by risk: a contested toll the policy names (section 2, rule 5), a sensitive row, or a correction already recorded
in `../audits/`. Each is a row of a chapter file in `../content/`.

| # | Event | Why |
|---|---|---|
| 1 | `e1948_04_deir_yassin` | A contested toll with an early unsubstantiated figure (254) the policy says to show as such, if at all. |
| 2 | `e1948_05_tantura` | A contested toll and a contested historiography (the Katz thesis and its aftermath). |
| 3 | `e1948_07_lydda_ramle`, `e1948_07_lydda_march` | Contested tolls across two rows that must not be summed across counters. |
| 4 | `e1948_10_dawayima` | A massacre with several counts. |
| 5 | `e1956_10_kafr_qasim` | A massacre with a 1957 military trial; the notes still mark the "one-piastre fine" **UNVERIFIED**. |
| 6 | `e1982_09_sabra_shatila` | Four counters (the Kahan Commission's figure, a Lebanese army prosecutor, the Palestinian Red Crescent, Kapeliouk). |
| 7 | `e2002_04_jenin` | Corrected on 2026-10-19 (`../audits/arabic_2000_2023.md`); an early unsubstantiated "200+" the policy names. |

Every one is sensitive, so each also goes to the maintainer's sensitivity review. The order and the size of the batch
are owner decisions (section 6, decision 5).

## 6. Owner decisions

Each of these is for the owner. Nothing above is built until they are made.

1. **Recruitment.** Who approaches reviewers, and how (the "help wanted" label on #29 alone, or direct approaches by
   the owner). How many for the first batch: one, or two so that no card rests on one person.
2. **Payment.** Volunteer or an honorarium, and from what budget. If paid, the independence rule in section 1 needs a
   line saying the fee does not depend on the outcome.
3. **Naming and credit.** Whether reviewers appear by name on the contributors page (the `historian` category since
   #430) and in the game's credits, by handle, or not at all; and the wording beside the name, for example "reviewed
   these events against their sources", so that credit is not read as endorsement of the game's perspective.
4. **Licence of review records.** Whether review text is committed under CC BY 4.0 (the `docs/` rule) and how the
   reviewer agrees to that, or kept out of the repository with only a link.
5. **Timeline.** When the first batch goes out, and whether to send it in the order of section 5 or chapter by
   chapter.
6. **Whether review gates release.** Proposed: the CI consistency check from the start; a release-gate step once the
   first batch is reviewed; every event `historian-reviewed` before a public release.
7. **Status for sensitive events.** Keep `sensitivity-reviewed` and show the historian review through the record
   (proposed), or follow the pipeline's order so sensitivity review implies historian review.
8. **Whether a recorded dissent blocks `historian-reviewed`.** Proposed: it does.
9. **Reviewers per sensitive event.** One, or two for events tagged `sensitive` (or an inherited `holocaust`, `terror`,
   `massacre` or `war-casualties` tag).
