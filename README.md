# Diagnosta – PC Checker

[![CI](https://github.com/Tyzoo7/diagnosta/actions/workflows/ci.yml/badge.svg)](https://github.com/Tyzoo7/diagnosta/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**English** · [Deutsch](README.de.md)

Diagnosta is a Windows diagnostics tool for the terminal. It shows the health of a PC
in one place and explains the results in plain language, so people with no technical
background can understand them too.

> **Status: early development (v0.5).** The system overview and drive overview are in place.
> More checks are being added one by one.

## Why another diagnostics tool?

There are great specialised tools: WizTree for disk space, CrystalDiskInfo for drive
health, HWiNFO for hardware. In IT support, though, you usually need **a quick overview
of everything** and a way to **pass it on**. Diagnosta is built for that:

- **One tool instead of five.** The most important checks, side by side.
- **Plain language.** "Your SSD has used 12% of its lifespan, everything is fine" instead of raw values.
- **A report to share.** One click creates an HTML file you can attach to a support ticket.
- **German and English.** The tool uses your Windows language automatically.

## Planned for v1.0

- [x] System overview: CPU, RAM, GPU, Windows version, uptime, with plain-language hints
- [x] Drive overview: SSD/HDD, model, free space, warning when nearly full
- [ ] Drive health (wear, errors)
- [ ] Storage analysis: largest folders and files
- [ ] Cleanup suggestions: temp files, update cache, recycle bin
- [ ] Startup programs
- [ ] Crash history: blue screens and unexpected shutdowns
- [ ] Report export (HTML), anonymised by default

## Principles

- **Read-only by default.** Nothing is deleted without explicit confirmation, and deleted files go to the recycle bin.
- **No network access, no telemetry.** Diagnosta never sends data anywhere.
- **Admin rights only when needed.** When they are needed, the tool explains why.
- **Few dependencies, pinned versions.** This keeps the attack surface small.

## What it shows today

```text
╭──────────────────────────────────────────────────────────────────────────────────╮
│ !  2 findings – details below.                                                   │
╰──────────────────────────────────────────────────────────────────────────────────╯
╭─ System ─────────────────────────────────────────────────────────────────────────╮
│ Windows        Windows 11 Pro 24H2 (Build 26100.2314)                            │
│ Processor      AMD Ryzen 5 7600X 6-Core Processor (6 cores, 12 threads)          │
│ Memory (RAM)   12 GB of 32 GB in use (38 %)                                      │
│                ███████████░░░░░░░░░░░░░░░░░░░                                    │
│ Graphics       NVIDIA GeForce RTX 4070 (12 GB dedicated memory)                  │
│ Since restart  23 days, 4 h                                                      │
╰──────────────────────────────────────────────────────────────────────────────────╯
╭─ Drives ─────────────────────────────────────────────────────────────────────────╮
│ C:  Windows · NVMe SSD · Samsung SSD 980 PRO 1TB                                 │
│     █████████████████░░░░░░░░░░░░░ 412 GB free of 931 GB (56 % used)             │
╰──────────────────────────────────────────────────────────────────────────────────╯
╭─ Findings ───────────────────────────────────────────────────────────────────────╮
│ ! This PC has not been restarted for 23 days. A restart finishes pending updates │
│   and often fixes slowness.                                                      │
│ i Fast Startup is on, so "Shut down" does not really restart Windows. Use        │
│   "Restart" instead.                                                             │
╰──────────────────────────────────────────────────────────────────────────────────╯
```

Everything is read with built-in Windows interfaces (registry, kernel32, DXGI, storage IOCTLs).
No admin rights and no extra packages are needed. Graphics memory above 4 GB is shown
correctly, unlike tools based on WMI. Drive serial numbers are never read.

## Download

Get the latest `Diagnosta-vX.Y.Z-win-x64.exe` from the
[Releases page](https://github.com/Tyzoo7/diagnosta/releases). It is a single file of
about 14 MB and needs no installation or .NET runtime.

1. Open a terminal in the download folder and run `.\Diagnosta-v0.5.0-win-x64.exe`
   (use the name of the version you downloaded). Double-clicking also works: the window
   stays open until you press Esc or close it.
2. Optionally, check that the file is genuine by comparing
   `Get-FileHash .\Diagnosta-v0.5.0-win-x64.exe` with the value in `SHA256SUMS.txt`
   from the same release.

The exe is not code-signed yet, so Windows SmartScreen may warn the first time you run it.
Versions below 1.0 are marked as pre-releases.

Every CI run also builds a test exe (**Actions → CI → Artifacts**), for trying out
changes before they are released.

## Usage

```text
diagnosta                 Start Diagnosta
diagnosta --lang de       Start in German (de) or English (en)
diagnosta --help          Show all options
diagnosta --version       Show the version
```

## Building from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/Tyzoo7/diagnosta.git
cd diagnosta
dotnet test                          # build and run all tests
dotnet run --project src/Diagnosta   # start the app
```

## Releasing

1. Raise `<Version>` in `src/Diagnosta/Diagnosta.csproj` in a pull request and merge it.
2. Tag the merged commit on `main` and push the tag:
   ```bash
   git tag v0.5.0
   git push origin v0.5.0
   ```
3. The **Release** workflow checks that the tag matches the version, runs the tests,
   builds the exe and publishes a GitHub release with the exe and `SHA256SUMS.txt`.

## Project structure

```text
src/Diagnosta/            The application
  Checks/                 Turns raw values into plain-language findings
  Cli/                    Command-line parsing
  SystemInfo/             Reads system information from Windows
  Localization/           Translations (Strings.resx = English, Strings.de.resx = German)
  Ui/                     Console output
tests/Diagnosta.Tests/    Automated tests (xUnit)
.github/workflows/        CI: builds and tests on Windows for every change
```

## License

[MIT](LICENSE) © Tyzoo7
