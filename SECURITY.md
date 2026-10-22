# Security policy

## Supported versions

The Story of the Land is pre-release and ships from `main`. Only the latest commit on `main` is supported.

## Reporting a vulnerability

Do not open a public issue for a security problem. Use GitHub's private vulnerability reporting: open the repository's
**Security** tab and choose **Report a vulnerability**
(the repository's `/security/advisories/new` page). Only you and the maintainers can see it.

Please include what the issue is and what an attacker gets from it, the steps to reproduce, the commit SHA you
tested, and your OS and browser (for the WebGL build) or Unity and .NET versions.

Conduct reports use the same private form: start the title with "Conduct:" (see CODE_OF_CONDUCT.md). Only you and
the maintainer can read it.

## What to expect

Acknowledgement within 5 business days, and an assessment with a rough fix timeline within 15. This is a
single-maintainer project: those are targets, not a contract. There is no bug bounty. You are credited in the advisory
and the changelog unless you ask otherwise.

## Scope

In scope:

- The WebGL and desktop builds, including save files (`SaveGame`) and the page flags they read (`?golden`, `?smoke`).
- The content loader: a crafted file under `content/` or `StreamingAssets/` that crashes or hangs the game or escapes
  its folder.
- The repository's CI and scripts (`.github/`, `scripts/`, `tools/`): anything that lets a pull request reach a secret,
  write to the repository, or run code with more than read access.
- A credential, token or personal data committed to this repository.

Out of scope:

- Vulnerabilities in Unity, .NET, browsers or third-party packages with no exploitable path through this project
  (report them to their maintainers).
- Running a build or a tool you modified yourself.

## Credentials

This repository holds no credentials and never should. A tool that needs one must read it from an environment
variable, never from a file in the repository. If you find one in this repository or its history, report it privately as above. It will be
revoked at its provider; history is not rewritten.
