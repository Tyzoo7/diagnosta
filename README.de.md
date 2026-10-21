# Diagnosta – PC Checker

[![CI](https://github.com/Tyzoo7/diagnosta/actions/workflows/ci.yml/badge.svg)](https://github.com/Tyzoo7/diagnosta/actions/workflows/ci.yml)
[![Lizenz: MIT](https://img.shields.io/badge/Lizenz-MIT-blue.svg)](LICENSE)

[English](README.md) · **Deutsch**

Diagnosta ist ein Diagnose-Tool für Windows, das im Terminal läuft. Es zeigt den Zustand
eines PCs an einem Ort und erklärt die Ergebnisse in verständlicher Sprache, sodass
auch Menschen ohne Technikwissen sie verstehen.

> **Status: frühe Entwicklung (v0.1).** Das Fundament steht: Sprachunterstützung,
> Kommandozeile, Tests und CI. **PC-Prüfungen gibt es noch keine.** Sie kommen Schritt für Schritt dazu.

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

- [ ] Systemüberblick: CPU, RAM, Grafikkarte, Windows-Version, Laufzeit
- [ ] Laufwerksgesundheit (SMART)
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
  Cli/                    Auswertung der Kommandozeile
  Localization/           Übersetzungen (Strings.resx = Englisch, Strings.de.resx = Deutsch)
  Ui/                     Ausgabe in der Konsole
tests/Diagnosta.Tests/    Automatische Tests (xUnit)
.github/workflows/        CI: baut und testet bei jeder Änderung auf Windows
```

## Lizenz

[MIT](LICENSE) © Tyzoo7
