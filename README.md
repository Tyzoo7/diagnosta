# Diagnosta – PC Checker

[![CI](https://github.com/Tyzoo7/diagnosta/actions/workflows/ci.yml/badge.svg)](https://github.com/Tyzoo7/diagnosta/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**English** · [Deutsch](README.de.md)

Diagnosta is a Windows diagnostics tool for the terminal. It shows the health of a PC
in one place and explains the results in plain language, so people with no technical
background can understand them too.

> **Status: early development (v0.6).** Menu, system details, drives, battery, network, startup apps and running programs are in place.
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

- [x] Arrow-key menu with language choice (German/English)
- [x] System overview: Windows version, uptime and pending restart; CPU load and base clock; RAM usage and modules; GPU memory and driver, with plain-language hints
- [x] Drive overview: SSD/HDD, model, free space, warning when nearly full
- [x] Drive health: healthy / warning / failing, as reported by Windows (no admin rights needed)
- [ ] Storage analysis: largest folders and files
- [ ] Cleanup suggestions: temp files, update cache, recycle bin
- [x] Battery wear on laptops
- [x] Network adapters: link speed, IP address, router, DNS (no internet access, no location prompt)
- [x] Startup programs, as listed in Task Manager
- [x] Running programs using the most memory and CPU
- [ ] Crash history: blue screens and unexpected shutdowns
- [x] Support report: one HTML file to attach to a ticket, anonymised by default

## Support report

**Create support report** in the menu (or `diagnosta --report`) saves one HTML file to the
desktop, ready to attach to a ticket or e-mail. Findings come first, then every detail.

- **Anonymised by default:** the PC's own IP address is shortened (`192.168.1.xxx`) and drive
  names are left out. User and computer names are never read in the first place.
- **Safe to open:** all values are HTML-encoded, and a Content-Security-Policy blocks scripts and
  any outside requests. The file contains no JavaScript and loads nothing from the internet.

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
│ C:      Windows · NVMe SSD · Samsung SSD 980 PRO 1TB                             │
│         █████████████████░░░░░░░░░░░░░ 412 GB free of 931 GB (56 % used)         │
│ Health  Samsung SSD 980 PRO 1TB  ✓ Healthy                                       │
╰──────────────────────────────────────────────────────────────────────────────────╯
╭─ Findings ───────────────────────────────────────────────────────────────────────╮
│ ! This PC has not been restarted for 23 days. A restart finishes pending updates │
│   and often fixes slowness.                                                      │
│ i Fast Startup is on, so "Shut down" does not really restart Windows. Use        │
│   "Restart" instead.                                                             │
╰──────────────────────────────────────────────────────────────────────────────────╯
```

Everything is read with built-in Windows interfaces (registry, kernel32, DXGI, storage IOCTLs, WMI).
No admin rights and no extra packages are needed. Graphics memory above 4 GB is shown
correctly, unlike tools based on WMI. Drive serial numbers are never read.

## Download

Get the latest `Diagnosta-vX.Y.Z-win-x64.exe` from the
[Releases page](https://github.com/Tyzoo7/diagnosta/releases). It is a single file of
about 14 MB and needs no installation or .NET runtime. It runs on 64-bit Windows 10 and 11.

1. Open a terminal in the download folder and run `.\Diagnosta-v0.5.0-win-x64.exe`
   (use the name of the version you downloaded). Double-clicking also works: the window
   stays open until you press Esc or close it.
2. Optionally, check that the file is genuine by comparing
   `Get-FileHash .\Diagnosta-v0.5.0-win-x64.exe` with the value in `SHA256SUMS.txt`
   from the same release. Releases made while the repository is public can also be
   checked with `gh attestation verify` (see [SECURITY.md](SECURITY.md)).

The exe is not code-signed yet, so Windows SmartScreen may warn the first time you run it.
Versions below 1.0 are marked as pre-releases.

Every CI run also builds a test exe (**Actions → CI → Artifacts**), for trying out
changes before they are released.

## Usage

Started without options in a terminal (or by double-click), Diagnosta asks for the language
and then shows a menu: pick the full PC check or a single area with the arrow keys and
confirm with Enter.

```text
diagnosta                 Open the menu
diagnosta --full          Print the full PC check right away (also used when output is redirected)
diagnosta --report        Save an anonymised support report (HTML) to the desktop
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
2. Open **Actions → Release → Run workflow** and enter the same version, e.g. `0.5.0`.
3. The workflow checks the input (format, matches `<Version>`, not released before),
   runs the tests, builds the exe, and publishes a GitHub release with the exe and
   `SHA256SUMS.txt`. GitHub creates the tag `v0.5.0` on the exact commit that was built.
   Once the repository is public, the workflow also adds a build provenance attestation.

## Project structure

```text
src/Diagnosta/            The application
  Checks/                 Turns raw values into plain-language findings
  Cli/                    Command-line parsing
  SystemInfo/             Reads system information from Windows
  Localization/           Translations (Strings.resx = English, Strings.de.resx = German)
  Ui/                     Console output
tests/Diagnosta.Tests/    Automated tests (xUnit)
.github/workflows/        CI (build and test on Windows), Release, CodeQL
```

## Security

Please report security problems privately, not as a public issue. See
[SECURITY.md](SECURITY.md) for how, and for what Diagnosta does and does not do.

## License

[MIT](LICENSE) © Tyzoo7
