# Example historical gap to investigate

Use this pattern when a timeline moment is underrepresented.

## Candidate theme

- the military government over Palestinians inside the 1949 lines (1948-1966)

## What to check

- existing event coverage in the chapter TSVs (`../content/events_1950_1966.tsv`)
- which villages it reached (`scope: region:<place>`) or every family (`national`)
- source-backed records for permits, curfews, land confiscation and present absentees
- the month it belongs in

## Good output

- a new row in the chapter's TSVs, converted with `tools/tsv_to_events.py`
- a source-backed `title`, `text`, and option logic
- a `status: draft` entry until a person has checked every claim against its source, then `fact-checked`
- a clear relation to what it meant for a family and its village
