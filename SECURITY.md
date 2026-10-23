# Security policy

## Supported versions

Only the latest release receives fixes. Please update before reporting a problem.

## Reporting a vulnerability

Please **do not** open a public issue for security problems.

Report them privately instead: on the repository page, open **Security → Report a
vulnerability**. Only the maintainer can see these reports.

Please include:

- the Diagnosta version (`Diagnosta.exe --version`) and your Windows version
- what you did, what happened, and what you expected
- if possible, the smallest steps to reproduce it

You will get an answer within 14 days. This is a hobby project maintained by one person,
so a fix may take longer; you will be kept informed and credited unless you prefer not to be.

## What Diagnosta does and does not do

Knowing this helps to judge whether something is a vulnerability:

- It only **reads** information. It changes no settings, files or registry values.
- It runs **without administrator rights** and never asks for them.
- It makes **no network connections**: no updates, telemetry or uploads.
- It never reads user names, computer names, serial numbers or MAC addresses.
- The only file it writes is the support report, and only when you ask for it.
  Reports are anonymised by default: the PC's own IP addresses are shortened and
  drive names are left out.

## Verifying a download

Download Diagnosta only from the
[Releases page](https://github.com/Tyzoo7/diagnosta/releases). Every release is built
by GitHub Actions, not on a personal PC, and cannot be changed after publishing.

- Compare `Get-FileHash .\Diagnosta-vX.Y.Z-win-x64.exe` with `SHA256SUMS.txt` from the
  same release.
- Releases made while the repository is public also carry a build provenance
  attestation. With the [GitHub CLI](https://cli.github.com/) you can check that the
  exe was built from this repository:
  `gh attestation verify .\Diagnosta-vX.Y.Z-win-x64.exe --repo Tyzoo7/diagnosta`
