# Diagnosta – PC Checker

[![CI](https://github.com/Tyzoo7/diagnosta/actions/workflows/ci.yml/badge.svg)](https://github.com/Tyzoo7/diagnosta/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

**English** · [Deutsch](README.de.md)

Diagnosta is a Windows diagnostics tool for the terminal. It shows the health of a PC
in one place and explains the results in plain language, so people with no technical
background can understand them too.

> **Status: early development (v0.1).** The foundation is in place: language support,
> command line, tests and CI. There are **no PC checks yet**. They are being added one by one.

## Why another diagnostics tool?

There are great specialised tools: WizTree for disk space, CrystalDiskInfo for drive
health, HWiNFO for hardware. In IT support, though, you usually need **a quick overview
of everything** and a way to **pass it on**. Diagnosta is built for that:

- **One tool instead of five.** The most important checks, side by side.
- **Plain language.** "Your SSD has used 12% of its lifespan, everything is fine" instead of raw values.
- **A report to share.** One click creates an HTML file you can attach to a support ticket.
- **German and English.** The tool uses your Windows language automatically.

## Planned for v1.0

- [ ] System overview: CPU, RAM, GPU, Windows version, uptime
- [ ] Drive health (SMART)
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

## Project structure

```text
src/Diagnosta/            The application
  Cli/                    Command-line parsing
  Localization/           Translations (Strings.resx = English, Strings.de.resx = German)
  Ui/                     Console output
tests/Diagnosta.Tests/    Automated tests (xUnit)
.github/workflows/        CI: builds and tests on Windows for every change
```

## License

[MIT](LICENSE) © Tyzoo7
