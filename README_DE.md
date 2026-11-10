# RRCA – KI-Crew für Crew Chief · v0.9 Alpha 3

Inoffizielle, kostenlose Testfassung für Rennsimulationen. Ben beobachtet Strategie und Renngeschehen, Mik das Fahrzeug, Sara den unmittelbaren Verkehr neben dem Auto. Grundlage ist Crew Chief V4 von Britton IT Ltd. Keine Verbindung oder Partnerschaft mit den genannten Entwicklern, Simulationen oder Sprachanbietern.

**Alpha: Fehler und verspätete oder ausbleibende Ansagen sind möglich. Kein System für reale Fahrzeuge oder sicherheitskritische Entscheidungen.** Der Fahrer bleibt verantwortlich. Kostenloser Download bedeutet nicht kostenlose KI-/Stimmen-APIs. Diese Fassung ist ein Testkandidat, keine abschließend rechtlich freigegebene öffentliche Veröffentlichung; offene Punkte stehen in `FREEWARE_PRUEFUNG.md`.

## Installation und erster Start

1. ZIP in einen neuen, beschreibbaren Ordner entpacken. Eine funktionierende Crew-Chief-/RRCA-Installation vorher separat sichern und unverändert lassen. `CrewChiefV4.exe` starten. Windows x64, .NET Framework 4.8 und Microsoft Edge WebView2 Runtime werden benötigt; nichts davon wird heimlich installiert.
2. Die **öffentliche Test-ZIP** setzt `RRCAStandaloneData=true` in `CrewChiefV4.exe.config`: eigene Profile und eigener Soundordner. Kein automatischer Import des Originalprofils. Details unten. Nicht über die Originalinstallation entpacken.
3. Falls nötig, Crew-Chief-Grundsounds mit der eingebauten Soundverwaltung in den neuen Soundordner laden. Sie sind aus Rechte- und Größen­gründen nicht in dieser ZIP. Die Soundverwaltung kann eigene Downloads benötigen.
4. Unter **Team & Stimmen → Dienste & Schlüssel** eigene OpenRouter- und ElevenLabs-Schlüssel speichern. Die Anbieter und ausgewählten Modelle müssen die verwendeten Schnittstellen unterstützen. Verbindung testen. Kontingente und Preise beim jeweiligen Anbieter prüfen.
5. **Stimmenliste laden**, drei deutsch geeignete, für das eigene Konto zulässige Stimmen wählen und im Stand mit **Probe** anhören. Es werden keine persönlichen Stimmen-IDs als neue Standardauswahl mitgeliefert. **Funkclips vorbereiten**, danach **Sara als Spotter erzeugen**. Erst wenn abgeschlossen, App vollständig schließen und wieder öffnen. Falls angezeigt, **Beheben und neu starten** im Stand verwenden. Saras Pack muss zur tatsächlich gewählten Stimme, zum Modell und Funkfilter passen.
6. Mikrofon wählen. Im **Crew-Chief-Fenster** die Controller-Aktion **KI-Team anfunken** zuordnen. Gedrückt halten, sprechen, loslassen. Alternativ im Stand die Textfrage im Cockpit testen. Keine Eingaben während der Fahrt vornehmen.
7. Unter **KI & Funk** eigenen vollständigen Namen und optionalen Funknamen eintragen und bei gestoppter Crew übernehmen. Spiel wählen oder **Spiel automatisch erkennen** einschalten. Die Sim benötigt ihre jeweilige Shared-Memory-/UDP-/Plugin-Konfiguration. Erkennung eines Prozesses bedeutet noch nicht gültige Fahrdaten.

Keine automatische Windows-Ersatzstimme: Ist die gewählte Stimme nicht verfügbar, kann die Ansage ausbleiben. Saras lokale Clips benötigen nach erfolgreicher Vorbereitung keine laufende Cloud-Verbindung. Ein Startblock erklärt, wenn ihr Pack fehlt oder nicht geladen ist. Ben/Mik können einzeln deaktiviert werden; **Nur Sara** schaltet beide aus. Sara auszuschalten entfernt ihre Seitenwarnungen.

## Bedienung und Sprache

Oben **Deutsch / English / Español** auswählen. Das übersetzt die neue RRCA-Oberfläche und die mit **?** erreichbaren Kurzinfos. Die Wahl wird im lokalen WebView2-Profil gespeichert. **Gesprochener Rennfunk, Sara-Clips und die ältere Crew-Chief-Oberfläche werden dadurch nicht automatisch englisch oder spanisch.** Technische Fremdanbieterfehler können in ihrer ursprünglichen Sprache erscheinen.

Messages und Background sind direkt im Cockpit regelbar. Background ist Funkambiente, nicht das Spiel. Modelle, Stimmen, Mikrofon und Berichtseinstellungen im Stand ändern. Die zehn regelmäßigen Berichte ergänzen eigenständige Ereigniserkennung: Grundintervall, Minus-/Plus-Toleranz und An/Aus pro Thema. Beispiel 120 −7/+8 plant 113–128 Sekunden. Das ist keine garantierte Sendezeit: Dringender Funk, Kurvenlast, bestätigte Qualifying-Flugrunde oder fehlende Daten verschieben Meldungen. Zwischen Routineberichten liegen mindestens 20 Sekunden. Mehr Meldungen bedeuten nicht mehr vorhandene Telemetrie.

## Daten, Kosten und Privatsphäre

- KI-Fragen übermitteln relevante Rennfakten, Frage und gegebenenfalls passende Erinnerungen an den eingestellten KI-Anbieter. OpenRouter kann den ausgewählten Modellanbieter einschalten; dessen Datenschutzregeln gelten zusätzlich. [OpenRouter-Datenschutz](https://openrouter.ai/privacy).
- Push-to-Talk sendet den aufgenommenen Sprachabschnitt zur Cloud-Spracherkennung. Es wird keine vollständige Mikrofonaufnahme als Datei gespeichert. Für TTS wird der zu sprechende Text an den gewählten Stimmenanbieter gesendet.
- **Fahrernamen an KI übermitteln** ist bei neuem Profil aus. Das reduziert Gegnernamen in KI-Anfragen; eigener Funkname, eingegebene Fragen und TTS-Text können weiterhin Namen enthalten. Keine vollständige Anonymisierung versprechen.
- Schlüssel sind mit Windows-DPAPI für den aktuellen Windows-Benutzer geschützt; zur Anmeldung gehen sie an den jeweiligen Anbieter. Sie gehören nie in ZIPs, Quellcode, Screenshots oder Supportlogs. Der eigene Windows-Benutzer kann weiterhin auf seine Daten zugreifen.
- Gedächtnis speichert Texte, Wünsche und begrenzte historische Fakten lokal und bleibt bei Updates erhalten. Es trainiert keine Modellgewichte und verändert keinen Programmcode. Historische Notizen sind keine aktuellen Messwerte.
- Optionaler JSONL-Diagnoserecorder: bis 20 Hz, höchstens 128 MiB je Aufnahme und 512 MiB im Ordner, begrenzte Segmente. Keine Schlüssel, Authentifizierungsheader oder Roh-Mikrofonaufnahmen; Namen und Funktexte können enthalten sein. Vor Weitergabe prüfen. Software-Zeitstempel sind keine Messung der tatsächlich gehörten Audioverzögerung.
- Eigene Konten und zulässige Stimmen verwenden. [ElevenLabs-Bedingungen](https://elevenlabs.io/terms-of-use-eu) einschließlich Stimmen- und gegebenenfalls Integrationsbedingungen beachten. Keine API-Guthaben oder Nutzungsrechte werden mitgeliefert.

## Speicherorte der öffentlichen ZIP

`Dokumente` meint den von Windows gelieferten Dokumente-Ordner, gegebenenfalls OneDrive/umgeleitet. Ordner entstehen nur bei der jeweiligen Funktion; Entpacken allein erzeugt diese Daten nicht.

| Zeitpunkt/Funktion | Pfad |
|---|---|
| Grundinitialisierung: Profile | `Dokumente\RRCA\CrewChiefV4\Profiles` |
| Controllerdaten und Buttonnamen | `…\Profiles\ControllerData\ButtonNames` |
| Konsole/Fehler-/Debugprotokolle | `Dokumente\RRCA\CrewChiefV4\DebugLogs` |
| Begrenztes KI-Funklog, wenn aktiv | `Dokumente\RRCA\CrewChiefV4\AiTeam\Logs` |
| Laufzeitdaten und Sounddownloads | `%LOCALAPPDATA%\RobertsCrewChiefAdvanced` und `…\Sounds` |
| Sara nach Erzeugung | `…\Sounds\voice\spotter_Sara` und zugehörige Sara-Funkcheck-/Bestätigungsordner |
| Neue RRCA-Oberfläche, Sprachwahl, Browsercache | `%LOCALAPPDATA%\RCCA\WebView2` |
| .NET-Konfiguration | `%LOCALAPPDATA%\Britton_IT_Ltd\…\user.config` – Unterpfad abhängig von EXE-Pfad/Hash und Version |
| Schlüssel beim expliziten Speichern | `%APPDATA%\RobertsCrewChiefAdvanced\api_key.bin` und `elevenlabs_key.bin` |
| Vorbereitete KI-Stimmen | `%APPDATA%\RobertsCrewChiefAdvanced\stimmen-cache\*.pcm` |
| Gesprächs-/Fahrergedächtnis, wenn aktiv | `Dokumente\RRCA\Fahrergedaechtnis`: `notizen.txt`, `Gespraeche\*.jsonl`, `telemetrie-fakten-v1.json` und Sicherung |
| Diagnoserecorder nach Einschalten | `Dokumente\CrewChief20\RCCA-LiveDiagnostics\*.jsonl` |
| Optionale Kurven-/Streckendaten | `…\CrewChiefV4\Track_landmarks`, `Track_splines`, `Pace_notes`, `RF2`, `LMU`; KI-Kurvenprofile unter `%APPDATA%\RobertsCrewChiefAdvanced\kurven` |
| Sara-Erzeugung temporär | Windows-Tempordner `rcca_sara_<zufällige ID>` |

**Wenn Crew Chief bereits installiert ist:** Der öffentliche Modus liest/importiert nicht automatisch `Dokumente\CrewChiefV4` oder `%LOCALAPPDATA%\CrewChiefV4\Sounds`. Bestehende RRCA-Schlüssel, Gedächtnis und WebView-Sprachwahl desselben Windows-Benutzers bleiben hingegen bewusst auffindbar – das beweist keinen Schlüssel im ZIP. Originalprofile werden nicht migriert. Wer in den erweiterten Einstellungen ausdrücklich einen gemeinsamen Soundordner auswählt, erlaubt damit auch Sound-/Sara-Schreibzugriffe dort. Für vollständige Trennung den neuen Soundordner behalten.

Frühere private Testpakete mit `RRCAStandaloneData=false` verwenden weiter die bisherigen Crew-Chief-Pfade. Nicht mit dem öffentlichen Modus verwechseln. Die .NET- und eigenen RRCA-Datenpfade gelten unabhängig davon. Für Updates denselben **RRCA-Testordner** nutzen oder neues Paket separat entpacken; Profile/Schlüssel/Erinnerungen liegen außerhalb der ZIP. Keine automatische App-Aktualisierung durch den Crew-Chief-Installer im öffentlichen Modus; RRCA-Updates bewusst über das eigene Projekt beziehen. Soundupdates sind getrennt.

## Teststatus, Grenzen und Support


Abstände sind von Gegneridentität, Klassenbezug, Zeitpunkt und Quelle abhängig. Näherungen werden gekennzeichnet. Ein schrumpfender Abstand beweist keinen Dreher oder Reifenschaden des Gegners. Gefahrenentfernungen nur mit frischen Positionen und vorbereiteten Clips; sonst kurze Grundwarnung. Nicht jede Sim liefert Reifen-/Sektor-/Wetterdaten für Gegner oder eine Wettervorhersage.

Fehler bitte im [eigenen GitHub-Projekt](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/issues) melden: Version/Commit, Sim, Strecke, Session, genaue Uhrzeit, Erwartung/gehörte Meldung und bereinigten Logausschnitt. Keine garantierte Reaktionszeit; bitte nicht das Crew-Chief-Team für RRCA-spezifische Fehler verantwortlich machen. Beim Entfernen App schließen und nur den gewünschten RRCA-Programmordner manuell entfernen; persönliche Daten werden nicht automatisch gelöscht. Gemeinsame Ordner nie blind entfernen.

Lizenzen und Danksagung: `HINWEISE_UND_DANK.md`, `THIRD_PARTY_NOTICES.md`, `ThirdPartyLicenses`. MIT-Quellcodelizenz ersetzt keine Rechte an Sounds, Stimmen, Marken oder fremden Vorlagen. Vor öffentlicher Werbung die offenen Punkte in `FREEWARE_PRUEFUNG.md` klären.

**Sprachbefehle:** RRCA liefert Microsofts `Microsoft.Speech.dll` bewusst nicht mit. Die klassischen Sprachbefehle von Crew Chief nutzen deshalb die Spracherkennung von Windows. Dafür muss in Windows die passende Sprache mit Spracherkennung installiert sein (Einstellungen → Zeit und Sprache → Sprache und Region; für die englischen Crew-Chief-Befehle Englisch). Die KI-Crew (Ben, Mik, Sara) ist davon unabhängig: Sie hört über die eigene Funktaste und die Cloud-Spracherkennung zu.

Direkt sichtbarer App-Hinweis: kostenloses, nichtkommerzielles Privatprojekt auf Basis von Crew Chief V4; Dank an die Entwickler, MIT-Lizenz und separate Dienstkosten. Eine zusätzliche persönliche Entwicklerfreigabe wird mangels Nachweis nicht behauptet.

Im Cockpit lassen sich Ben, Mik und Sara separat regeln (0-100 %, anfangs 100 %). Die allgemeine Funklautstärke wirkt zusätzlich; 0 % schaltet die Stimme einschließlich Saras Warnungen stumm. Kein Neustart oder erneutes Erzeugen von Clips nötig.
