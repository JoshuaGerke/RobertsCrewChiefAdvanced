# Robert's Crew Chief Advanced – Hinweise, Lizenz und Dank

## Was das ist

Robert's Crew Chief Advanced (RCCA) ist eine **inoffizielle, nicht kommerzielle** Erweiterung von Crew Chief V4. Wir verkaufen nichts, nehmen kein Geld und verfolgen keine geschäftlichen Ziele. RCCA ist kein Produkt des Crew-Chief-Teams und wird von ihm weder geprüft noch unterstützt. Fragen und Fehler zu RCCA bitte an uns (GitHub-Issues), nicht an das Crew-Chief-Team.

## Grundlage und Dank

RCCA baut vollständig auf **Crew Chief V4** auf:

- Copyright © 2019–2024 Britton IT Ltd, veröffentlicht unter der MIT-Lizenz
- Website: https://thecrewchief.org
- Quellcode: https://gitlab.com/mr_belowski/CrewChiefV4

Crew Chief ist über viele Jahre und tausende Stunden freiwilliger Arbeit entstanden. Ohne diese Arbeit gäbe es RCCA nicht. Unser großer Dank gilt Jim Britton (mr_belowski), der Crew Chief geschaffen hat und ihm seine Stimme gibt, und allen Mitwirkenden, darunter Tony Whitley, The Iron Wolf, Sparten, Sam Halliday, Vytautas Leonavičius, Morten Roslev, Dan Allongo, Marcel Hild und Steven White, sowie allen, die Strecken, Fahrernamen, Übersetzungen und Fehlerberichte beigetragen haben.

Wer Crew Chief unterstützen möchte, findet auf https://thecrewchief.org den Weg dorthin.

## Was RCCA ergänzt

- Das KI-Team (Ben, Mik, Sara) im Ordner `CrewChiefV4/AiTeam`: Funk auf Deutsch per Funktaste, Auswertung der Telemetrie, Sprachausgabe, deutsche Spotter-Ansagen
- Wenige Anbindungsstellen im Originalcode (Menü, Einstellungen, Spotter-Stimme, Rundenschätzung)

Spotter, Ansagen und alle übrigen Funktionen von Crew Chief bleiben unverändert erhalten.

## Lizenz

- Der Originalcode von Crew Chief steht unter der MIT-Lizenz. Der vollständige Lizenztext liegt jeder Version als `LICENSE_CrewChief_MIT.txt` bei.
- Die Ergänzungen von RCCA stehen unter derselben MIT-Lizenz.
- Mitgelieferte Bibliotheken haben eigene Lizenzen, die Texte liegen bei (`Newtonsoft_Json_licence.txt`, `SharpDX_licence.txt`, `openvr_licence.txt`, `zlib_licence.txt`).
- Die Sprachdateien von Crew Chief (Sound-Packs) sind **nicht** enthalten. Sie haben eigene Rechte und werden wie gewohnt über den Download in der App von den Crew-Chief-Servern geladen.

## Dienste von Drittanbietern

Das KI-Team nutzt auf Wunsch zwei externe Dienste, jeweils mit eigenem Konto und eigenen Kosten des Nutzers:

- **OpenRouter** (https://openrouter.ai) für Spracherkennung und Antworten. Dorthin gehen die Aufnahme beim Drücken der Funktaste und die Renndaten, die für die Antwort nötig sind.
- **ElevenLabs** (https://elevenlabs.io) für die Stimmen. Dorthin gehen die Texte der Funksprüche.

Es gelten die Nutzungs- und Datenschutzbedingungen dieser Anbieter. Die Schlüssel werden nur lokal und verschlüsselt für den jeweiligen Windows-Benutzer gespeichert und nie weitergegeben. Ohne Schlüssel funktioniert Crew Chief wie gewohnt, nur ohne KI-Team.

## Haftung

RCCA wird ohne jede Gewähr bereitgestellt (siehe MIT-Lizenz). Die Auswertungen des KI-Teams können falsch sein. Was am Auto gemacht wird und ob es an die Box geht, entscheidet immer der Fahrer.
