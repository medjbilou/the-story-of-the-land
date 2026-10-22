# Reviewing footage clips

The owner decided on 22 October 2026 to use a map and checked clips. No video file goes into the game. A clip is one of
two kinds:

- **A publisher's page** (`kind` `page`), such as Al Jazeera, Middle East Eye, the UN or UNRWA, or the publisher's
  official channel. It always opens in the browser.
- **A direct file from Archive Genocide** (`kind` `video` or `photo`, at `media.archivegenocide.com`). The desktop game
  (macOS, Windows, Linux) plays it in a viewer over the card. A video streams into the viewer, with play/pause, a scrub
  bar, mute, close (Esc too), the caption in the player's language and the credit "Archive Genocide · the archive ·
  original post", each a link. The game's music pauses while a video is open. Nothing is written to disk. If the file does
  not load, the viewer says "This clip could not be loaded. Open it in the browser." and offers the browser.
  The web player cannot play these files: `media.archivegenocide.com` sends no `Access-Control-Allow-Origin` header, so a
  page cannot read them. In the web player the file opens in a new tab instead.

An event card shows a **Watch footage** («شاهد المقطع») button only for clips a person has watched and signed off.
Pressing it first shows the content note "Content note: this clip shows killing and violence." («تنبيه: يحتوي المقطع على
مشاهد قتل وعنف.») with **Open** and **Cancel**. Nothing plays or opens before Open.

Archive Genocide's data is CC BY 4.0: each clip carries the credit "Archive Genocide" with a link to archivegenocide.com
(`THIRD-PARTY-NOTICES.md`). The footage itself belongs to its authors. The game streams it and never ships or caches a
copy. Its captions are testimony: no figure from them goes into the game.

The list is `../content/footage.tsv` (repo root `content/`). It has one row per clip, tab-separated, with a header row.
Lines starting with `#` are comments. The candidates were found and their links opened by AI agents on 22 October 2026.
Nobody has watched them yet, so none of them shows in the game until you sign it off. An AI never fills `checked_by` or
`checked_on` (CONTENT_POLICY.md section 2, rules 5 and 7).

## Columns

| Column | What it holds |
|---|---|
| `event_id` | The card the clip belongs to, an id from `content/events_*.tsv` |
| `url` | The clip's page on the publisher's site or its official channel; for an Archive Genocide file, the archive's event (`#focus=`) |
| `publisher` | Who published it; for an Archive Genocide file, the archive the clip came from (a Telegram channel, for example) |
| `title_en`, `title_ar` | The publisher's title, and its other-language version |
| `date` | When it was published (YYYY-MM-DD, or YYYY-MM or YYYY) |
| `duration_s` | Length in seconds; blank where no page showed it |
| `description_en`, `description_ar` | What the clip shows. These carry no casualty figure: the card holds the sourced tolls |
| `kind` | `page`, `video` or `photo` |
| `media_url` | An Archive Genocide file's address (blank for a page) |
| `poster_url` | The file's still, where the archive has one |
| `ag_entry_id` | The clip's id in the archive |
| `post_url` | The original post the archive took the clip from, where it recorded one |
| `checked_by` | **You**: your GitHub handle, once you have watched the clip |
| `checked_on` | **You**: the day you watched it, YYYY-MM-DD |
| `notes` | The kind of clip, how graphic it looked from its page, how the link was verified, and any doubt |

## How to check a clip

1. Read the row's `notes` first. Some clips are marked graphic or doubtful.
2. Open the `url` (for a file, the `media_url`) and watch the **whole** clip, not just the start. For a file, also open
   the `post_url` and check who posted it first.
3. Check these four things:
   - It is the publisher named in `publisher`, on its own site or official channel. It is never a re-upload, and never
     Israeli media. An Archive Genocide file comes from the archive named in `publisher`; its caption is testimony.
   - It shows the event of `event_id`, or a report about that event, at the place and time the card describes.
   - Nothing in it breaks the content policy as the game would present it. For example: it must not glorify an attack
     on civilians, show a death that the card does not cover, or present a contested cause (such as al-Ahli, 17 October
     2023) as settled when the card keeps it open. A publisher's own title may differ from the game's wording; the clip
     still has to fit the card.
   - The `title_ar` and `description_ar` cells read correctly, and the description matches what you saw.
4. If the clip passes, fill `checked_by` with your GitHub handle and `checked_on` with today's date (YYYY-MM-DD). If it
   fails, delete the row, or leave both cells blank and add the reason to `notes`.
5. A card shows a button for every signed-off clip, so sign off no more than the best three for each card.

## Putting signed-off clips into the game

From `game/`:

```sh
python3 tools/footage_to_yaml.py           # writes content/palestine/footage.yaml with the signed-off clips only
python3 tools/footage_to_yaml.py --check   # fails if the YAML is stale or a row is malformed
uv run pytest -q tools/tests/test_footage_to_yaml.py
```

The converter checks every row, signed off or not. It needs an existing event id, an https address, a publisher and both
titles, a valid date, whole seconds and a kind. A `video` or `photo` row needs its `media_url`, and a `page` row has no
file columns. It also checks that `checked_by` and `checked_on` are filled together, as a
GitHub handle and a day. The game reads `content/palestine/footage.yaml`, and each card shows a button for each of its
clips there. Commit both files.

## Withdrawing a clip

If a page goes down, its content changes, or you change your mind, clear the row's `checked_by` and `checked_on` (or
delete the row) and run the converter again. The button disappears from that card.
