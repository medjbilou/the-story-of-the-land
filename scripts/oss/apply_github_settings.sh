#!/usr/bin/env bash
# OWNER ONLY. Applies scripts/oss/github/* to the GitHub repository: repo settings (squash only, no auto-merge, secret
# scanning, Discussions on), two rulesets (protect-main: force-push and deletion blocked, no bypass; main-review:
# required review and checks, admin bypass; no classic branch protection), the Actions allow-list with SHA pinning,
# private vulnerability reporting, Dependabot alerts, a read-only default workflow token, approval for outside
# contributors' workflow runs, and the labels.
# Agents never run this. It refuses without an interactive terminal and asks you to type the repository name.
# Usage: bash scripts/oss/apply_github_settings.sh OWNER/REPO
set -euo pipefail
repo="${1:?usage: apply_github_settings.sh OWNER/REPO}"
here="$(cd "$(dirname "$0")" && pwd)"

if [ ! -t 0 ]; then
  echo "refusing: this owner step needs an interactive terminal; nothing was applied"
  exit 2
fi
read -r -p "Type the repository name ($repo) to apply the settings: " typed
if [ "$typed" != "$repo" ]; then
  echo "names differ; nothing was applied"
  exit 2
fi

gh api -X PATCH "repos/$repo" --input "$here/github/repo-settings.json" >/dev/null
echo "repo settings applied"
# Two rulesets, looked up by name. protect-main (the live one, kept) blocks force-push and deletion with NO bypass:
# nobody, the owner included, can rewrite public history (owner ruling 1). main-review adds review and required checks,
# and only it carries the admin bypass (decision D12).
put_ruleset() {
  local name="$1" file="$2" id
  id="$(gh api --paginate "repos/$repo/rulesets" --jq ".[] | select(.name==\"$name\") | .id")"
  if [ -n "$id" ]; then
    gh api -X PUT "repos/$repo/rulesets/$id" --input "$file" >/dev/null
    echo "ruleset $name ($id) updated"
  else
    gh api -X POST "repos/$repo/rulesets" --input "$file" >/dev/null
    echo "ruleset $name created"
  fi
}
put_ruleset protect-main "$here/github/ruleset-protect-main.json"
put_ruleset main-review "$here/github/ruleset-main-review.json"
gh api -X PUT "repos/$repo/private-vulnerability-reporting" >/dev/null
echo "private vulnerability reporting on"
gh api -X PUT "repos/$repo/vulnerability-alerts" >/dev/null
echo "Dependabot alerts on"
gh api -X PUT "repos/$repo/actions/permissions/workflow" -f default_workflow_permissions=read \
  -F can_approve_pull_request_reviews=false >/dev/null
echo "default workflow token read-only"
if gh api -X PUT "repos/$repo/actions/permissions/fork-pr-contributor-approval" \
     -f approval_policy=all_external_contributors >/dev/null 2>&1; then
  echo "outside contributors' workflow runs need approval"
else
  echo "WARN: set it by hand: Settings > Actions > General > 'Require approval for all external contributors'"
fi
gh api -X PUT "repos/$repo/actions/permissions" --input "$here/github/actions-permissions.json" >/dev/null
# Runs after "selected" is on (GitHub refuses the list otherwise); if it fails, no workflow runs until a re-run succeeds.
gh api -X PUT "repos/$repo/actions/permissions/selected-actions" --input "$here/github/actions-selected.json" >/dev/null ||
  { echo "ERROR: the actions allow-list was not set, so every workflow is blocked; re-run this script"; exit 1; }
echo "only allow-listed actions run, pinned to a full SHA"
while IFS=$'\t' read -r name color desc; do
  [ -n "$name" ] && gh label create "$name" --repo "$repo" --color "$color" --description "$desc" --force >/dev/null
done < "$here/github/labels.tsv"
echo "labels created"

echo "--- verify ---"
gh api "repos/$repo" --jq '{allow_auto_merge, allow_merge_commit, allow_rebase_merge, allow_squash_merge, delete_branch_on_merge, has_discussions, visibility}'
for name in protect-main main-review; do
  id="$(gh api --paginate "repos/$repo/rulesets" --jq ".[] | select(.name==\"$name\") | .id")"
  gh api "repos/$repo/rulesets/$id" --jq '{name, enforcement, bypass: [.bypass_actors[] | .bypass_mode], rules: [.rules[].type], checks: [.rules[] | select(.type=="required_status_checks") | .parameters.required_status_checks[].context]}'
done
gh api "repos/$repo/actions/permissions" --jq '{allowed_actions, sha_pinning_required}'
gh api "repos/$repo/branches/main/protection" >/dev/null 2>&1 && echo "WARN: classic branch protection exists too; remove it (rulesets only)" || echo "no classic branch protection (rulesets only)"
