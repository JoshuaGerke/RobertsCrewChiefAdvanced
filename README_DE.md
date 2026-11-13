# RRCA – Rob's Race Crew Advanced

[English](README.md) · **Deutsch** · [Español](README_ES.md)

RRCA ist eine kostenlose, inoffizielle Erweiterung für [Crew Chief V4](https://gitlab.com/mr_belowski/CrewChiefV4). Sie gibt dir eine Boxencrew aus drei Stimmen, mit der du im Rennen sprechen kannst:

- **Ben** beobachtet das Rennen und kümmert sich um die Strategie.
- **Mik** ist dein Renningenieur für Auto, Reifen und Sprit.
- **Sara** ist deine Spotterin und warnt dich vor Autos neben dir. Sie läuft direkt auf deinem PC und wartet nie auf das Internet.

Du drückst einen Knopf am Lenkrad, fragst in normalen Worten, und die Crew antwortet mit den Live-Daten aus deiner Simulation. Liefert das Spiel einen Wert nicht, sagt die Crew das, statt etwas zu erfinden.

> **Testversion.** Die Crew spricht **Deutsch, Englisch oder Spanisch**, du wählst. Das Programmfenster gibt es in denselben drei Sprachen, es richtet sich nach deiner Windows-Sprache. Ansagen können falsch, verspätet oder ausbleiben. Nur für Rennsimulationen.

**[Aktuelle Version herunterladen](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/latest)** · Im Download liegt außerdem eine bebilderte Schritt-für-Schritt-Anleitung: `ANLEITUNG_RRCA_DE.pdf`.

---

## Inhalt

1. [Was du brauchst](#1-was-du-brauchst)
2. [Was sind OpenRouter und ElevenLabs?](#2-was-sind-openrouter-und-elevenlabs)
3. [Was kostet das?](#3-was-kostet-das)
4. [OpenRouter-Schlüssel anlegen](#4-openrouter-schlüssel-anlegen)
5. [ElevenLabs-Schlüssel anlegen](#5-elevenlabs-schlüssel-anlegen)
6. [RRCA installieren](#6-rrca-installieren)
7. [RRCA einrichten](#7-rrca-einrichten)
8. [Der erste Test](#8-der-erste-test)
9. [Probleme und Lösungen](#9-probleme-und-lösungen)
10. [Daten und Datenschutz](#10-daten-und-datenschutz)
11. [Updates, Deinstallieren und Fehler melden](#11-updates-deinstallieren-und-fehler-melden)
12. [Technische Details](#12-technische-details)
13. [Lizenz und Dank](#13-lizenz-und-dank)

---

## 1. Was du brauchst

- Einen Windows-PC mit 64 Bit (getestet unter Windows 11). Windows 10 und 11 haben die zwei Microsoft-Bausteine, die RRCA braucht, normalerweise schon: **.NET Framework 4.8** und die **Microsoft Edge WebView2 Runtime**. Fehlt einer, sagt RRCA Bescheid. Lade ihn dann nur von microsoft.com.
- Eine Rennsimulation, die Crew Chief unterstützt, zum Beispiel RaceRoom, Automobilista 2, Assetto Corsa Competizione, iRacing, rFactor 2 oder Le Mans Ultimate.
- Ein **Mikrofon** (das vom Headset reicht) und einen **freien Knopf am Lenkrad** zum Sprechen mit der Crew.
- Eine **Internetverbindung** während der Fahrt.
- Zwei Online-Konten: **OpenRouter** und **ElevenLabs**. Beide werden unten Schritt für Schritt erklärt.

Du brauchst **keine** Programmierkenntnisse, kein Python, kein SimHub und sonst nichts.

## 2. Was sind OpenRouter und ElevenLabs?

RRCA selbst ist kostenlos. Damit es deine Fragen versteht und mit natürlichen Stimmen spricht, nutzt es zwei Online-Dienste. Du eröffnest bei beiden ein Konto, lädst einen kleinen Betrag auf, und RRCA nutzt dieses Guthaben. Das Geld geht an diese Dienste, nicht an uns.

| Dienst | Was er für dich tut | Webseite |
|---|---|---|
| **OpenRouter** | Das „Gehirn“. Es macht aus deiner gesprochenen Frage Text und lässt Ben und Mik eine passende Antwort überlegen. OpenRouter ist ein Marktplatz, über den man mit einem Konto und einem Guthaben viele KI-Modelle nutzen kann. | [openrouter.ai](https://openrouter.ai) |
| **ElevenLabs** | Die „Stimmen“. Es macht aus den Antworten der Crew natürliche Sprache, damit Ben, Mik und Sara wie echte Menschen klingen. | [elevenlabs.io](https://elevenlabs.io) |

Jeder Dienst gibt dir einen **API-Schlüssel**. Das ist ein langes Passwort aus Buchstaben und Zahlen, das du einmal in RRCA einfügst, damit RRCA dein Konto nutzen darf. Behandle ihn wie ein Passwort: niemals in einem Forum posten oder auf einem Screenshot zeigen.

## 3. Was kostet das?

Preise Stand **November 2026**. Preise ändern sich, bitte vor dem Bezahlen auf den Webseiten der Anbieter nachsehen. Abgerechnet wird in US-Dollar.

**OpenRouter** (Guthaben im Voraus, Abrechnung nach Verbrauch)
- Du kaufst Guthaben im Voraus, zum Beispiel 10 US-Dollar. Bei Kartenzahlung schlägt OpenRouter 5,5 % Gebühr auf (mindestens 0,80 USD).
- RRCA nutzt für Funkantworten standardmäßig ein schnelles, günstiges Modell und ein stärkeres nur für längere Auswertungen, wenn du in der Box stehst. Die Spracherkennung kostet etwa 0,0045 USD pro Minute Sprache.
- Grobe Schätzung: **Ein 30-Minuten-Rennen kostet meist ein paar Cent, selten mehr als 0,25 USD.** Das hängt davon ab, wie viel du mit der Crew sprichst.
- OpenRouter kann ungenutztes Guthaben nach einem Jahr verfallen lassen.

**ElevenLabs** (Monatsabo oder nach Verbrauch)
- **Free:** 0 USD, etwa 10.000 Zeichen pro Monat. Das reicht zum Ausprobieren, aber nicht für regelmäßige Rennen, und nicht jede Stimme ist im kostenlosen Tarif nutzbar.
- **Starter:** etwa 6 USD pro Monat (der erste Monat kostete beim Schreiben 1 USD), etwa 75.000 Zeichen pro Monat. **Für den Anfang empfohlen.**
- Für Vielfahrer gibt es größere Tarife.
- Einmalig bei der Einrichtung bereitet RRCA die Standardansagen der Crew und Saras Spotter-Ansagen vor. Das braucht einmalig etwa 15.000 bis 25.000 Zeichen.
- Danach braucht ein typisches Rennen etwa 3.000 bis 10.000 Zeichen, je nachdem, wie gesprächig du den Funk einstellst.

**So behältst du die Kosten im Griff**
- Setz bei jedem Schlüssel ein **Ausgabenlimit** (unten erklärt).
- Fang klein an, zum Beispiel mit 10 USD bei OpenRouter und dem Starter-Tarif bei ElevenLabs.
- In RRCA begrenzt die Einstellung **Maximale KI-Aufrufe je Session** (Standard 300), wie oft Ben und Mik in einer Session die KI fragen dürfen.
- Beide Webseiten zeigen genau, was du verbraucht hast.

Die Zahlen sind Schätzungen, keine Garantie. Deine echten Kosten stehen in deinen Konten.

## 4. OpenRouter-Schlüssel anlegen

1. Geh auf [openrouter.ai](https://openrouter.ai) und klick oben rechts auf **Sign in**. Du kannst dich mit einem Google-Konto oder mit deiner E-Mail-Adresse anmelden.
2. Öffne **Credits** ([openrouter.ai/settings/credits](https://openrouter.ai/settings/credits)) und klick auf **Add Credits**. Bezahl einen kleinen Betrag, zum Beispiel 10 USD, per Karte.
3. Öffne **Keys** ([openrouter.ai/settings/keys](https://openrouter.ai/settings/keys)) und klick auf **Create Key**.
   - Name: zum Beispiel `RRCA`.
   - **Credit limit:** trag einen Betrag ein, zum Beispiel `5`. Der Schlüssel hört dann auf zu funktionieren, wenn dieser Betrag verbraucht ist.
4. Klick auf **Create**. Der Schlüssel wird **nur einmal** angezeigt. Er beginnt mit `sk-or-`. Kopier ihn sofort (markieren, dann Strg+C) und bewahr ihn sicher auf, zum Beispiel in einem Passwort-Manager.

## 5. ElevenLabs-Schlüssel anlegen

1. Geh auf [elevenlabs.io](https://elevenlabs.io) und klick auf **Sign up**. Bestätige deine E-Mail-Adresse.
2. Wähl unter **Pricing** oder in deinen Kontoeinstellungen einen Tarif. **Starter** ist die sichere Wahl (siehe Kosten oben).
3. Stimmen aussuchen: Öffne die **Voice Library** (manchmal „Voices“ → „Explore“). Filtere nach deiner Funksprache (zum Beispiel **German**) und hör dir einige an. Such dir drei aus, je eine für Ben, Mik und Sara (für Sara passt eine Frauenstimme), und klick jeweils auf **Add to my voices**. Nur Stimmen unter „My voices“ erscheinen später in RRCA.
4. Schlüssel anlegen: Öffne in deinem Konto **API Keys** (meist unter **Developers** oder im Profilmenü) und klick auf **Create API key**.
   - Name: zum Beispiel `RRCA`.
   - Berechtigungen: **Text to Speech: Access** und **Voices: Read** reichen. Alles andere kann aus bleiben.
   - Wenn angeboten, ein **Credit limit** setzen.
5. Kopier den Schlüssel (er beginnt mit `sk_`). Auch er wird meist **nur einmal** angezeigt.

## 6. RRCA installieren

1. Lade `RobertsCrewChiefAdvanced.zip` von der [Download-Seite](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/latest).
2. Rechtsklick auf die ZIP → **Alle extrahieren…** und einen neuen Ordner wählen, zum Beispiel `C:\Simracing\RRCA`. Nicht in einen bestehenden Crew-Chief-Ordner entpacken.
3. Im neuen Ordner **CrewChiefV4.exe** doppelt anklicken. Zeigt Windows die blaue Meldung „Der Computer wurde durch Windows geschützt“, klick auf **Weitere Informationen** → **Trotzdem ausführen**. Das kommt, weil das Programm neu und nicht signiert ist.
4. Das RRCA-Fenster öffnet sich. Das klassische Crew-Chief-Fenster liegt minimiert dahinter in der Taskleiste, beide gehören zum selben Programm.

**Du nutzt schon Crew Chief?** Kein Problem. RRCA hat eigene Profile und Sounds und verändert deinen bisherigen Crew Chief nicht. Lass nicht beide gleichzeitig laufen.

## 7. RRCA einrichten

Mach das alles bei geschlossenem Spiel oder wenn du in der Box stehst, nie während der Fahrt.

**a) Sprachen wählen.** Oben rechts unter **Oberflächensprache** stellst du das Fenster auf Deutsch, English oder Español. Die Sprache der Crew stellst du getrennt unter **KI & Funk** → **Funksprache** ein (Automatisch nimmt die Windows-Sprache). Wechselst du die Funksprache später, **Funkclips vorbereiten** und **Sara als Spotter erzeugen** erneut ausführen (Schritt c) und RRCA neu starten.

**b) Schlüssel eintragen.** Geh auf **Team & Stimmen** → **Dienste & Schlüssel**.
- Füg den OpenRouter-Schlüssel in das Feld **OpenRouter (Ben, Mik, Spracherkennung)** ein (ins Feld klicken, Strg+V) und klick auf **Speichern**.
- Füg den ElevenLabs-Schlüssel in das Feld **ElevenLabs (Stimmen)** ein und klick auf **Speichern**.
- Klick auf **Verbindung testen**. RRCA zeigt an, ob die Verbindung klappt.

**c) Der Crew ihre Stimmen geben.** Weiter unter **Team & Stimmen**:
1. Klick auf **Stimmenliste laden**. Deine Stimmen aus „My voices“ bei ElevenLabs erscheinen.
2. Wähl eine Stimme für Ben, Mik und Sara und hör sie dir mit **Probe** an.
3. Klick auf **Funkclips vorbereiten**. Das erzeugt einmalig die Standardansagen und dauert ein paar Minuten.
4. Klick auf **Sara als Spotter erzeugen** und warte, bis es fertig ist.
5. **RRCA komplett schließen und neu starten.** Saras Warnungen funktionieren erst nach diesem Neustart.

**d) Mikrofon und Funktaste.**
1. Unter **Team & Stimmen** → **Geräte & Funktaste** dein Mikrofon auswählen.
2. Klick auf **Crew-Chief-Fenster**, damit das klassische Fenster nach vorne kommt.
3. Dort in der Controller-Liste dein Lenkrad wählen, die Aktion **„KI-Team anfunken“** markieren und auf **Assign** (Zuweisen) klicken. Dann den Knopf am Lenkrad drücken, den du nutzen willst. Siehst du die Aktion nicht, zuerst auf **Add/Remove Actions** klicken.
4. Zurück ins RRCA-Fenster wechseln. Das klassische Fenster kannst du wieder minimieren.

**e) Dein Name.** Unter **KI & Funk** → **Dein Fahrerprofil** deinen Namen und, wenn du willst, einen kurzen Funknamen eintragen (zum Beispiel „Rob“). Unten auf **Übernehmen** klicken.

**f) Deine Simulation.** Auf der Seite **Cockpit** schaltest du **Spiel automatisch erkennen** ein. RRCA startet die Crew dann selbst, sobald dein Spiel läuft. Alternativ das Spiel aus der Liste wählen und auf **Start** klicken. In den Einstellungen des Spiels muss die Telemetrie bzw. Shared Memory eingeschaltet sein; die Crew-Chief-Dokumentation erklärt das für jedes Spiel.

## 8. Der erste Test

1. Spiel starten und in der Box ins Auto setzen.
2. RRCA zeigt dein Spiel als verbunden an, und die Crew begrüßt dich.
3. Funktaste gedrückt halten, etwas Einfaches fragen, zum Beispiel *„Wie viel Sprit habe ich?“*, und loslassen.
4. Mik sollte innerhalb weniger Sekunden antworten.
5. Fahr danach erst ein kurzes Training, bevor du ins erste Rennen gehst.

## 9. Probleme und Lösungen

**Die Crew sagt nichts.**
Prüf, ob RRCA dein Spiel als verbunden zeigt, ob im Spiel die Telemetrie an ist und ob **Verbindung testen** bei deinen Schlüsseln geklappt hat. Schau nach deinem Guthaben bei OpenRouter und ElevenLabs.

**Sara warnt mich nicht.**
**Sara als Spotter erzeugen** nochmal ausführen, warten bis es fertig ist und RRCA neu starten. Ein roter Hinweis im Fenster erklärt, was fehlt.

**Die Stimmenliste ist leer.**
Bei ElevenLabs Stimmen zu **My voices** hinzufügen, dann **Stimmenliste laden** erneut klicken. Prüf, ob der Schlüssel die Berechtigung **Voices: Read** hat.

**Meine Frage wird nicht verstanden.**
Halt die Taste kurz gedrückt, bevor du sprichst, und lass sie erst los, wenn du fertig bist. Prüf das gewählte Mikrofon.

**Plötzlich ist Funkstille.**
Dein Guthaben oder deine Monatszeichen sind vielleicht aufgebraucht, oder das Limit des Schlüssels ist erreicht. Schau auf beiden Webseiten nach.

**Die klassischen englischen Sprachbefehle von Crew Chief gehen nicht.**
Diese alten Befehle nutzen die Spracherkennung von Windows. Installiere in Windows Englisch mit Spracherkennung (Einstellungen → Zeit und Sprache → Sprache und Region). Ben, Mik und Sara brauchen das nicht.

## 10. Daten und Datenschutz

- Wenn du mit der Crew sprichst, gehen deine gesprochene Frage, die dafür nötigen Renndaten und, bei eingeschaltetem Fahrergedächtnis, passende Notizen an OpenRouter und das dort genutzte KI-Modell. Der zu sprechende Text geht an ElevenLabs. Es gelten die Regeln der Anbieter: [OpenRouter-Datenschutz](https://openrouter.ai/privacy), [ElevenLabs-Bedingungen](https://elevenlabs.io/terms-of-use-eu).
- Das Mikrofon nimmt nur auf, solange du die Funktaste hältst. Es werden keine vollständigen Aufnahmen gespeichert.
- Beim Start fragt RRCA bei GitHub nach, ob es eine neuere Version gibt. Dabei werden keine persönlichen Daten übertragen.
- Deine Schlüssel werden verschlüsselt für deinen Windows-Benutzer gespeichert (Windows-DPAPI) und sind nie im Download enthalten.
- Die Option **Fahrernamen an KI übermitteln** ist bei neuen Profilen aus.
- Das Fahrergedächtnis speichert Notizen und Wünsche nur auf deinem PC. Es trainiert kein KI-Modell.

## 11. Updates, Deinstallieren und Fehler melden

- **Update:** Gibt es eine neue Version, zeigt RRCA oben einen Knopf **Update auf …**. Draufklicken und bestätigen: RRCA lädt und prüft das Update, startet neu und behält Schlüssel, Profile und Gedächtnis. Alternativ die neue ZIP herunterladen und bei geschlossenem RRCA über deinen RRCA-Ordner entpacken.
- **Nicht** den normalen Crew-Chief-Installer oder dessen Updates über den RRCA-Ordner installieren. Einen normalen Crew Chief, falls vorhanden, in seinem eigenen Ordner lassen.
- **Deinstallieren:** RRCA schließen und den RRCA-Ordner löschen. Deine persönlichen Daten (siehe Tabelle unten) bleiben, bis du sie selbst löschst.
- **Fehler melden:** ein [Issue auf GitHub](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/issues) anlegen oder im [Thread auf virtualracing.org](https://forum.virtualracing.org/threads/rrca-erweiterung-freier-ki-boxenfunk-fuer-crew-chief-v4.146346/) schreiben. Bitte Version (`VERSION.txt` im RRCA-Ordner), Spiel, Strecke, Uhrzeit und das Gehörte angeben. **Niemals deine Schlüssel posten.** Bitte das Crew-Chief-Team nicht wegen RRCA anschreiben.

## 12. Technische Details

### Wo RRCA Daten speichert

„Dokumente“ ist dein Windows-Dokumente-Ordner (er kann auf OneDrive umgeleitet sein). Jeder Ordner entsteht erst, wenn die zugehörige Funktion benutzt wird.

| Was | Wo |
|---|---|
| Profile | `Dokumente\RRCA\CrewChiefV4\Profiles` |
| Controller-Einstellungen | `…\Profiles\ControllerData` |
| Protokolle | `Dokumente\RRCA\CrewChiefV4\DebugLogs` |
| KI-Funkprotokoll, wenn eingeschaltet | `Dokumente\RRCA\CrewChiefV4\AiTeam\Logs` |
| Laufzeitdaten und Sounds | `%LOCALAPPDATA%\RobertsCrewChiefAdvanced` und `…\Sounds` |
| Saras Spotterpaket | `…\Sounds\voice\spotter_Sara` |
| RRCA-Fenster (Sprache, Cache) | `%LOCALAPPDATA%\RCCA\WebView2` |
| .NET-Einstellungen | `%LOCALAPPDATA%\Britton_IT_Ltd\…\user.config` |
| Schlüssel | `%APPDATA%\RobertsCrewChiefAdvanced\api_key.bin` und `elevenlabs_key.bin` |
| Vorbereitete Stimmen-Clips | `%APPDATA%\RobertsCrewChiefAdvanced\stimmen-cache` |
| Fahrergedächtnis, wenn eingeschaltet | `Dokumente\RRCA\Fahrergedaechtnis` |
| Diagnoserecorder, wenn eingeschaltet | `Dokumente\CrewChief20\RCCA-LiveDiagnostics` |

Der Diagnoserecorder ist auf 128 MB pro Aufnahme und 512 MB insgesamt begrenzt. Er speichert nie Schlüssel oder Mikrofonaufnahmen, kann aber Namen und Funktexte enthalten. Prüf ihn deshalb, bevor du ihn weitergibst.

### Selbst bauen

Mit Visual Studio 2022 oder den Build Tools:

```
msbuild CrewChiefV4/CrewChiefV4.csproj -restore -p:Configuration=Release
./build/package.ps1 -OutDir dist/RRCA -PublicDistribution
```

## 13. Lizenz und Dank

RRCA ist eine inoffizielle Erweiterung auf Basis von Crew Chief V4 von Britton IT Ltd und steht wie Crew Chief unter der [MIT-Lizenz](LICENSE). Vielen Dank an das Crew-Chief-Team für diese Grundlage. RRCA ist kein Produkt des Crew-Chief-Teams und wird von ihm nicht unterstützt. Die Soundpakete von Crew Chief haben eigene Rechte und sind nicht enthalten. Weitere Komponenten: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [HINWEISE_UND_DANK.md](HINWEISE_UND_DANK.md).

Ein Projekt von Robert und Joshua Gerke.
