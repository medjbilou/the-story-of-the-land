#!/usr/bin/env bash
# Owner art checkpoint: a WebGL build (skipped with --no-build when the task has just built), screenshots at the camera
# poses (PromoPose) from the player itself (?shot: the web tier with its UI and labels) and editor renders of both quality
# tiers. Re-checks every family golden in the same player run and fails if a screenshot is missing. It only captures:
# there is no side-by-side comparison against reference images.
# --borderproof adds the region borders' synthetic, labelled proof pattern (docs/visual-v1.md, "Region borders"): only
# the editor renders (-borderproof) show it, since a release WebGL build ignores &borderproof. Usage:
# tools/art_checkpoint.sh [--no-build] [--borderproof] <checkpoint-name>
set -euo pipefail
build=1; proof_query=""; proof_arg=()
while [[ "${1:-}" == --* ]]; do
  case "$1" in
    --no-build) build=0 ;;
    --borderproof) proof_query="&borderproof"; proof_arg=(-borderproof) ;;
    *) echo "art_checkpoint: unknown option $1" >&2; exit 2 ;;
  esac
  shift
done
name="${1:?usage: tools/art_checkpoint.sh [--no-build] [--borderproof] <checkpoint-name>}"
root="$(git rev-parse --show-toplevel)"
cd "$root"
: "${UNITY:?set UNITY as in docs/dev-setup.md}"
shared() { uv run tools/machine_lock.py --shared -- "$@"; }
# The build rewrites URP and player settings, and the editor capture may write QualitySettings' m_CurrentQuality; this
# script restores them from the index, so it refuses to run while any of them has unstaged changes (review M4: never
# discard an edit silently) and restores them on every exit, a failed build included (review M3).
guarded=(unity/ProjectSettings/QualitySettings.asset)
[[ $build == 1 ]] && guarded+=(unity/Assets/Settings unity/ProjectSettings/ProjectSettings.asset)
if ! git diff --quiet -- "${guarded[@]}"; then
  echo "art_checkpoint: stage your changes to ${guarded[*]} first (this script restores them from the index)" >&2
  exit 2
fi
trap 'git checkout -- "${guarded[@]}"' EXIT
if [[ $build == 1 ]]; then
  shared "$UNITY" -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -buildTarget WebGL -executeMethod StoryOfTheLand.Game.EditorTools.Build.Web -compression disabled -logFile "$root/unity-build-web-art.log"
  git checkout -- unity/Assets/Settings unity/ProjectSettings/ProjectSettings.asset
  if grep -n "Shader error" "$root/unity-build-web-art.log"; then echo "art_checkpoint: shader errors in the WebGL build" >&2; exit 1; fi
fi
[[ -f unity/Builds/Web-disabled/index.html ]] || { echo "art_checkpoint: no build at unity/Builds/Web-disabled (run without --no-build)" >&2; exit 2; }
out="captures/visual/$name"
rm -rf "$out"; mkdir -p "$out"
# The expected hashes come from the family goldens (content/palestine/goldens.yaml, docs/determinism.md), so a re-pin needs
# no edit here. A line is "name: hash", so the name loses its colon.
golden_expect=()
# (golden_name, not name: $name is the checkpoint's name, used below)
while read -r golden_name golden_hash; do
  [[ -z "$golden_name" || "$golden_name" == \#* ]] && continue
  golden_expect+=(--expect "GOLDEN OK ${golden_name%:} $golden_hash")
done < content/palestine/goldens.yaml
[[ ${#golden_expect[@]} -gt 0 ]] || { echo "art_checkpoint: no family goldens" >&2; exit 2; }
uv run tools/web_check.py unity/Builds/Web-disabled --query "golden&shot${proof_query}" "${golden_expect[@]}" --expect "SHOT done" --shots "$out" --stall 180 \
  --fail 'GOLDEN FAIL|SMOKE FAIL|BOOT FAIL|StoryOfTheLand/.*(not supported|Unsupported)|(not supported|Unsupported).*StoryOfTheLand/'
# The poses come from PromoPose.All's source, so a new pose is checked without editing this script (review M3).
poses=$(grep -o 'new PromoPose("[A-Za-z0-9_]*"' unity/Assets/Scripts/Map/PromoPose.cs | cut -d'"' -f2)
[[ -n "$poses" ]] || { echo "art_checkpoint: no PromoPose found in PromoPose.cs" >&2; exit 1; }
for pose in $poses; do
  [[ -s "$out/$pose.png" ]] || { echo "art_checkpoint: $out/$pose.png is missing" >&2; exit 1; }
done
shared "$UNITY" -batchmode -CI false -GITHUB_ACTIONS false -TF_BUILD false -quit -projectPath unity -buildTarget OSXUniversal -executeMethod MapCapture.Promo -captureDir "$root/$out/editor" ${proof_arg[@]+"${proof_arg[@]}"} -logFile "$root/unity-capture-promo.log"
git checkout -- unity/ProjectSettings/QualitySettings.asset   # SetQualityLevel may have written m_CurrentQuality (no unstaged edits: checked above)
echo "ART CHECKPOINT $name: captures in $out (captures only, no comparison)"
