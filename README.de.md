# Diagnosta – PC Checker

[![CI](https://github.com/Tyzoo7/diagnosta/actions/workflows/ci.yml/badge.svg)](https://github.com/Tyzoo7/diagnosta/actions/workflows/ci.yml)
[![Lizenz: MIT](https://img.shields.io/badge/Lizenz-MIT-blue.svg)](LICENSE)

[English](README.md) · **Deutsch**

Diagnosta ist ein Diagnose-Tool für Windows, das im Terminal läuft. Es zeigt den Zustand
eines PCs an einem Ort und erklärt die Ergebnisse in verständlicher Sprache, sodass
auch Menschen ohne Technikwissen sie verstehen.

> **Status: frühe Entwicklung (v0.4).** Systemüberblick und Laufwerksübersicht sind fertig.
> Weitere Prüfungen kommen Schritt für Schritt dazu.

## Warum noch ein Diagnose-Tool?

Es gibt großartige Spezial-Tools: WizTree für Speicherplatz, CrystalDiskInfo für die
Laufwerksgesundheit, HWiNFO für Hardware. Im IT-Support braucht man aber meistens
**einen schnellen Überblick über alles** und eine Möglichkeit, ihn **weiterzugeben**.
Genau dafür ist Diagnosta gebaut:

- **Ein Tool statt fünf.** Die wichtigsten Prüfungen auf einen Blick.
- **Verständliche Sprache.** „Deine SSD hat 12 % ihrer Lebensdauer verbraucht, alles in Ordnung“ statt roher Messwerte.
- **Ein Bericht zum Weitergeben.** Ein Klick erstellt eine HTML-Datei, die du an ein Support-Ticket anhängen kannst.
- **Deutsch und Englisch.** Das Tool nutzt automatisch deine Windows-Sprache.

## Geplant für v1.0

- [x] Systemüberblick: CPU, RAM, Grafikkarte, Windows-Version, Laufzeit, mit verständlichen Hinweisen
- [x] Laufwerksübersicht: SSD/HDD, Modell, freier Platz, Warnung wenn fast voll
- [ ] Laufwerksgesundheit (Verschleiß, Fehler)
- [ ] Speicheranalyse: größte Ordner und Dateien
- [ ] Aufräum-Vorschläge: Temp-Dateien, Update-Cache, Papierkorb
- [ ] Autostart-Programme
- [ ] Absturz-Verlauf: Bluescreens und unerwartete Neustarts
- [ ] Bericht-Export (HTML), standardmäßig anonymisiert

## Grundsätze

- **Standardmäßig nur lesen.** Gelöscht wird nichts ohne ausdrückliche Bestätigung, und gelöschte Dateien landen im Papierkorb.
- **Kein Netzwerkzugriff, keine Telemetrie.** Diagnosta sendet nie Daten irgendwohin.
- **Admin-Rechte nur, wenn nötig.** Wenn sie nötig sind, erklärt das Tool, warum.
- **Wenige Abhängigkeiten, feste Versionen.** So bleibt die Angriffsfläche klein.

## Was es heute zeigt

```text
╭──────────────────────────────────────────────────────────────────────────────────╮
│ !  2 Hinweise – Details siehe unten.                                             │
╰──────────────────────────────────────────────────────────────────────────────────╯
╭─ System ─────────────────────────────────────────────────────────────────────────╮
│ Windows                Windows 11 Pro 24H2 (Build 26100.2314)                    │
│ Prozessor              AMD Ryzen 5 7600X 6-Core Processor (6 Kerne, 12 Threads)  │
│ Arbeitsspeicher (RAM)  12 GB von 32 GB belegt (38 %)                             │
│                        ███████████░░░░░░░░░░░░░░░░░░░                            │
│ Grafik                 NVIDIA GeForce RTX 4070 (12 GB eigener Speicher)          │
│ Seit Neustart          23 Tage, 4 Std.                                           │
╰──────────────────────────────────────────────────────────────────────────────────╯
╭─ Laufwerke ──────────────────────────────────────────────────────────────────────╮
│ C:  Windows · NVMe SSD · Samsung SSD 980 PRO 1TB                                 │
│     █████████████████░░░░░░░░░░░░░ 412 GB frei von 931 GB (56 % belegt)          │
╰──────────────────────────────────────────────────────────────────────────────────╯
╭─ Hinweise ───────────────────────────────────────────────────────────────────────╮
│ ! Dieser PC wurde seit 23 Tagen nicht neu gestartet. Ein Neustart schließt       │
│   ausstehende Updates ab und behebt oft Langsamkeit.                             │
│ i Schnellstart ist aktiv, deshalb startet „Herunterfahren“ Windows nicht         │
│   wirklich neu. Nutze stattdessen „Neu starten“.                                 │
╰──────────────────────────────────────────────────────────────────────────────────╯
```

Alle Werte werden über Windows-eigene Schnittstellen gelesen (Registry, kernel32, DXGI,
Speicher-IOCTLs). Dafür braucht es weder Admin-Rechte noch zusätzliche Pakete. Grafikspeicher
über 4 GB wird korrekt angezeigt, anders als bei Tools, die auf WMI basieren.
Seriennummern von Laufwerken werden nie ausgelesen.

## Ausprobieren

Ein offizielles Release gibt es noch nicht. Jeder CI-Lauf baut eine eigenständige
`Diagnosta.exe` (etwa 14 MB, ohne .NET-Installation lauffähig):

1. Öffne **Actions → CI**, wähle den neuesten grünen Lauf und lade unter *Artifacts*
   **Diagnosta-win-x64** herunter.
2. Entpacke die Datei, öffne ein Terminal in diesem Ordner und starte `.\Diagnosta.exe`.
   Ein Doppelklick funktioniert auch: Das Fenster bleibt offen, bis du eine Taste drückst.
3. Optional kannst du `Get-FileHash .\Diagnosta.exe` mit dem SHA-256 in der Zusammenfassung des Laufs vergleichen.

Die .exe ist noch nicht code-signiert, deshalb warnt Windows SmartScreen beim ersten Start eventuell.

## Verwendung

```text
diagnosta                 Diagnosta starten
diagnosta --lang de       Auf Deutsch (de) oder Englisch (en) starten
diagnosta --help          Alle Optionen anzeigen
diagnosta --version       Version anzeigen
```

## Aus dem Quellcode bauen

Benötigt das [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
git clone https://github.com/Tyzoo7/diagnosta.git
cd diagnosta
dotnet test                          # bauen und alle Tests ausführen
dotnet run --project src/Diagnosta   # Programm starten
```

## Projektstruktur

```text
src/Diagnosta/            Das Programm
  Checks/                 Macht aus Rohwerten verständliche Hinweise
  Cli/                    Auswertung der Kommandozeile
  SystemInfo/             Liest Systeminformationen aus Windows
  Localization/           Übersetzungen (Strings.resx = Englisch, Strings.de.resx = Deutsch)
  Ui/                     Ausgabe in der Konsole
tests/Diagnosta.Tests/    Automatische Tests (xUnit)
.github/workflows/        CI: baut und testet bei jeder Änderung auf Windows
```

## Lizenz

[MIT](LICENSE) © Tyzoo7
