# Reviewing an outside pull request safely

An outside pull request is untrusted input aimed at a machine that holds the maintainer's credentials (GitHub, Unity,
ElevenLabs, AI agents). In this repository, opening or testing a pull request **runs its code**:

- **Unity** compiles and runs every script under `unity/Assets/Editor/` (and `[InitializeOnLoad]` code anywhere in
  `Assets/`) as soon as the project is opened or imported. Opening the PR's branch in the editor is running it.
- **`dotnet build` / `dotnet test`** run MSBuild targets from every `.csproj` and from `Directory.Build.props`.
- **`uv run`** installs the dependencies a script declares inline and runs the script. `pytest` runs `conftest.py`
  at import.

So: **read the diff before running anything, and never run a pull request's code on your own machine before the review
verdict.** CI runs the PR's code on GitHub's machines with a read-only token and no secrets; that is where it belongs.

**Any diff under `.github/` can make every required check green.** CI runs the workflow file from the pull request
itself, so a PR that edits `.github/workflows/ci.yml` can turn `Changelog`, `Repo policy` or any other check into a job
that always passes. Green checks on such a PR mean nothing; read the `.github/` diff first, and your own review is
the real gate.

## The profile

```sh
# 1. A throwaway checkout of only the pull request, in an empty directory:
rm -rf /tmp/sc-review && mkdir -p /tmp/sc-review && cd /tmp/sc-review
git clone --depth 50 <this repository's URL> repo
cd repo && git fetch origin pull/<PR#>/head:pr && git checkout pr

# 2. Read the diff first, especially build and editor surfaces:
git diff origin/main...pr --stat
git diff origin/main...pr -- '*.csproj' 'sim/Directory.Build.props' 'unity/Assets/Editor/**' '.github/**' 'scripts/**' 'tools/**'

# 3. To run anything, use two containers. Stage 1 restores packages into a writable copy of the checkout: it has the
#    network, and only the checkout (read-only) and a work folder. (A restore also runs MSBuild targets from the PR,
#    which is why it too sees no credentials.)
mkdir -p "$HOME/sc-review/cache"        # under $HOME: Docker Desktop and colima share $HOME, not /tmp
docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
  --cap-drop ALL --security-opt no-new-privileges -e NUGET_PACKAGES=/cache/nuget \
  -v "$HOME/sc-review/cache":/cache -v "$PWD":/src:ro -w /tmp mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -c 'rm -rf /cache/work && cp -a /src /cache/work && cd /cache/work && dotnet restore sim/StoryOfTheLand.sln'

# Stage 2 runs the tests with NO network, as you (not root), on that writable copy (the checkout stays read-only):
docker run --rm --network none --user "$(id -u):$(id -g)" -e HOME=/tmp -e DOTNET_CLI_HOME=/tmp -e DOTNET_NOLOGO=1 \
  --cap-drop ALL --security-opt no-new-privileges -e NUGET_PACKAGES=/cache/nuget \
  -v "$HOME/sc-review/cache":/cache -w /cache/work mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet test sim/StoryOfTheLand.sln --no-restore
```

Rules:

1. **No credential volumes.** The container gets the checkout only: no `~/.config/gh`, no `~/.claude`, no Unity
   licence, no `.env` file.
2. **No network while the PR's code runs** (`--network none` in Stage 2; Stage 1 only restores). An injected payload's
   first move is usually to phone home. Run as your own user, never root: a root container also changes what the tests see.
3. **AI review only on a throwaway credential.** If you ask an AI agent to review the PR, run it inside the container
   with a separate, low-limit key made for that review, never your own session. Treat its verdict as text you read,
   never as a command you run.
4. **Never open the PR's `unity/` folder in your Unity editor** until the diff has been read and approved. Unity edit-mode
   tests for an outside PR are run by the maintainer after merge-readiness, on a branch the maintainer has read.
5. **No AI agent and no secret in CI for pull requests**, and never `pull_request_target` with a checkout of the PR's code.

This protects the review, not the judgement: merging is still a human decision on what was read.
