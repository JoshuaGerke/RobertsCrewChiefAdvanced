# RRCA – Rob's Race Crew Advanced

Kostenlose, inoffizielle Erweiterung für [Crew Chief V4](https://gitlab.com/mr_belowski/CrewChiefV4). Drei Stimmen begleiten dich im Rennen:

- **Ben** beobachtet das Rennen und kümmert sich um Strategie.
- **Mik** ist dein Renningenieur für Fahrzeug, Reifen und Sprit.
- **Sara** warnt dich als Spotterin vor Autos neben dir. Sie läuft lokal und wartet auf keine Cloud.

Du sprichst die Crew per Funktaste an und bekommst Antworten aus den Renndaten deiner Simulation. Fehlen Daten, erfindet die Crew keine.

**[Download der aktuellen Version](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/tag/aktuell)**

Die ZIP komplett in einen neuen Ordner entpacken und dort `CrewChiefV4.exe` starten. Eine bebilderte Anleitung liegt im Paket: `ANLEITUNG_RRCA_DE.pdf` (englisch `GUIDE_RRCA_EN.pdf`, spanisch `GUIA_RRCA_ES.pdf`).

## Was du brauchst

- Windows 64 Bit (getestet unter Windows 11), .NET Framework 4.8 und die Microsoft Edge WebView2 Runtime
- eine Rennsimulation, die Crew Chief unterstützt
- eigene Konten bei [OpenRouter](https://openrouter.ai) (KI und Spracherkennung) und [ElevenLabs](https://elevenlabs.io) (Stimmen). RRCA selbst ist kostenlos, die Nutzung dieser Dienste kann Geld kosten.
- ein Mikrofon und eine Taste am Lenkrad für den Funk

## Stand

Testversion. Der Funk spricht derzeit Deutsch, die Oberfläche gibt es auf Deutsch, Englisch und Spanisch. Falsche, verspätete oder fehlende Ansagen sind möglich. Nur für Rennsimulationen, nicht für echte Fahrzeuge.

RRCA nutzt eigene Ordner für Profile und Sounds und lässt eine vorhandene Crew-Chief-Installation unberührt. Die klassischen englischen Sprachbefehle von Crew Chief laufen über die Spracherkennung von Windows.

## Daten und Schlüssel

Deine Fragen, die Sprachaufnahme der Funktaste und die dafür nötigen Renndaten gehen an die Anbieter, die du einträgst. Deine API-Schlüssel werden verschlüsselt in deinem Windows-Profil gespeichert und nie mit dem Paket weitergegeben. Welche Daten wo liegen, steht in [README_DE.md](README_DE.md).

## Fehler melden

Bitte als [Issue](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/issues) oder im [Thread auf virtualracing.org](https://forum.virtualracing.org/threads/rrca-erweiterung-freier-ki-boxenfunk-fuer-crew-chief-v4.146346/). Wichtig sind Version (`VERSION.txt` im Paket), Simulation, Strecke, Uhrzeit und die Meldung, die du gehört hast. Niemals API-Schlüssel posten. Fragen zu RRCA bitte nicht an das Crew-Chief-Team richten.

## Selbst bauen

Mit Visual Studio 2022 oder den Build Tools:

```
msbuild CrewChiefV4/CrewChiefV4.csproj -restore -p:Configuration=Release
./build/package.ps1 -OutDir dist/RRCA -PublicDistribution
```

## Lizenz und Dank

RRCA baut auf Crew Chief V4 von Britton IT Ltd auf und steht wie Crew Chief unter der [MIT-Lizenz](LICENSE). Danke an das Crew-Chief-Team für diese Grundlage. RRCA ist kein Produkt des Crew-Chief-Teams und wird von ihm nicht unterstützt. Die Soundpakete von Crew Chief haben eigene Rechte und sind nicht enthalten. Hinweise zu weiteren Komponenten: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) und [HINWEISE_UND_DANK.md](HINWEISE_UND_DANK.md).

Ein Projekt von Robert und Joshua Gerke.

---

## English

RRCA is a free, unofficial extension for Crew Chief V4. Ben (strategy), Mik (car and engineering) and Sara (spotter, runs locally) answer over push-to-talk using your sim's live data. Radio is German for now, the interface is available in German, English and Spanish. You need your own OpenRouter and ElevenLabs accounts; RRCA itself is free. Download the [latest version](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/tag/aktuell) and see `GUIDE_RRCA_EN.pdf` and [README_EN.md](README_EN.md). Based on Crew Chief V4 by Britton IT Ltd, MIT licence. Not affiliated with the Crew Chief team.
