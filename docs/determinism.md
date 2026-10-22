# Determinism

The simulation must produce the same state, bit for bit, on .NET, Unity Mono and IL2CPP. Saves, replays and the
"Why did this happen?" report depend on this.

## How it is kept

- **One RNG:** SplitMix64 (`StoryOfTheLand.Sim.Rng`). Its whole state is one `ulong` in `GameState.RngState`. The family game
  draws nothing yet, so the seed only travels in the state and the save.
- **Reference vectors:** `DeterminismTests.SplitMix64MatchesTheReferenceVectors` checks the first three outputs for
  seeds 0 and 1234567. The expected values come from Sebastiano Vigna's reference `splitmix64.c`
  (https://prng.di.unimi.it/splitmix64.c), compiled and run separately. They were not copied from this implementation.
- **No trigonometry in the sim:** the sim calls only `Math.Abs`, `Math.Min` and `Math.Max`. The region rule
  (`Family.Near`) measures on a flat grid (111.2 km a degree of latitude, 94.6 a degree of longitude) for the same reason.
- **Canonical hash:** `GameState.Hash()` is SHA-256 over a canonical text. Keys are sorted ordinally. Doubles are
  written as IEEE bit patterns, with -0.0 written as 0.0. Culture is invariant. The text also carries a fixed block of fields at
  constant values (`GameState.Hash`); they are part of the hash format, so changing them moves every golden and save
  hash.

## Golden playthroughs

The family goldens are pinned in `content/palestine/goldens.yaml`, one `family-<chapter>-<village>: <hash>` line each.
A golden plays that chapter for a family of that village with the golden seed (`GoldenRun.Seed`, 1948), every card
answered with its first choice, to the chapter's end, and pins the final `GameState.Hash()`:

| Golden | Ends | Hash |
|---|---|---|
| `family-1947_1949-tantura` | the chapter's end | `159d72dad8dcf3ef990a6cf68c6ef28f869c0ed655bc9e5ee37cc06462bee325` |

`FamilyTests.TheFamilyGoldensHold` plays every line on .NET (`dotnet test sim/StoryOfTheLand.sln`) and, when one fails,
prints the new hash and the final state as `GameState.Describe()` writes it: the same writer as `Hash()`, one line per
section, every number as a plain value beside its IEEE bits, so a diff shows what moved. `GoldenCheck` plays the same
runs in Unity (Mono in `unity/Assets/Tests/EditMode/GoldenCheckTests.cs`, and the macOS and WebGL IL2CPP players with
`-golden` or `?golden`) and prints `GOLDEN OK <name> <hash>` per line, then `BOOT OK`. `ContentSync` ships the file with
the chapters, and the WebGL loader fetches it before the check. `tools/player_golden_check.py` (the CI player check,
which reads the file at the release tag), `tools/release_build.py`, `tools/art_checkpoint.sh` and `docs/RELEASING.md`
derive their required OK lines from it. Run `uv run tools/build_sim_for_unity.py` first so Unity has the current DLLs.

The browser smoke run (`?smoke=5`) plays five months of the default family game through the UI's own buttons, answering
each card with its first choice, and prints `SMOKE OK 5 turns 1948-04-01 <hash>`. Check every WebGL build with every
golden and the smoke line with:

```
golden_expect=()
while read -r name hash; do
  [[ -z "$name" || "$name" == \#* ]] && continue
  golden_expect+=(--expect "GOLDEN OK ${name%:} $hash")
done < content/palestine/goldens.yaml
uv run tools/web_check.py unity/Builds/Web-<compression> --query "golden&smoke=5" "${golden_expect[@]}" \
  --expect "SMOKE OK 5 turns 1948-04-01"
```

Re-check the players with:

```
uv run tools/machine_lock.py --exclusive --wait-unity -- "$UNITY" -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -projectPath unity -runTests -testPlatform EditMode -testFilter GoldenCheckTests -testResults "$PWD/unity-editmode.xml" -logFile "$PWD/unity-editmode.log"
uv run tools/machine_lock.py --exclusive --wait-unity -- "$UNITY" -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -buildTarget OSXUniversal -executeMethod StoryOfTheLand.Game.EditorTools.Build.Mac -logFile unity-build-mac.log
uv run tools/machine_lock.py --exclusive -- "unity/Builds/Mac/TheStoryOfTheLand.app/Contents/MacOS/The Story of the Land" -batchmode -nographics -golden -logFile "$PWD/unity-golden-mac.log"; echo exit=$?
```

The player needs an absolute `-logFile` path: with a relative one it wrote no log file in the check run. The editor
commands are fine with relative paths.

**Cause text is derived, not hashed.** The "Why did this happen?" lines (`Cause.Message`, `Cause.Delta`) are output
computed from the hashed state; replaying a save's commands regenerates them. Wording changes therefore do not move the
goldens.

**A content change can move the content hash without moving a golden.** `Content.Hash` covers every file a chapter
reads (`palestine/model.yaml`, the chapter, `places.yaml`, `fates.yaml`, `currency.yaml`). A change that does not reach
the family's state (a source URL, a place another family lives in) moves only the content hash. A save made before it
loads with the "different game content" warning (`GameSession.Load`) and replays to the same state.

**Any change to a golden hash is a deliberate rules or content change.** Such a change could be to the family loop,
a chapter's events or effects, the places and fates, or the hash format. When it happens, update
`content/palestine/goldens.yaml` and the table above in the same commit, add a row to the history below, and say in the
commit message why the hash moved. If a hash changes and you did not mean to change rules or content, treat it as a
determinism bug.

## Rebaseline history

| Change | Golden | New hash (first 8) | Why |
|---|---|---|---|
| First golden | `family-1947_1949-tantura` | `c568906d` | the first family golden |
| Playtest 3 | `family-1947_1949-tantura` | `159d72da` | a turn's cards come in date order (an event's `day`, read from its text by tools/tsv_to_events.py), not id order: the golden answers them in the new order, so the clamped resources land differently. With id order kept the old hash holds |
