# RRCA – Rob's Race Crew Advanced

**Die Erweiterung für Crew Chief 4.0 mit echter KI.**

[![Downloads](https://img.shields.io/github/downloads/JoshuaGerke/RobertsCrewChiefAdvanced/total?label=Downloads&color=b9ef35)](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/latest)

[English](README.md) · **Deutsch** · [Español](README_ES.md)

> **Neu in Alpha 0.41: iRacing im Mittelpunkt, Sara in Echtzeit bis 200 Meter.**
>
> - **Sara meldet Gefahren bis 200 m vor und hinter dir, dafür sofort.** Dreher, stehende und langsame Autos kommen sofort, meist in unter einer Sekunde, auch hinter dir. Die Fernwarnung bis 1000 m ist weg. Ob ein Auto zu langsam ist, misst Sara am üblichen Tempo genau an dieser Stelle. Gemeinsames Bremsen im Pulk gilt nicht mehr als Dreher. In iRacing zählt zusätzlich die Meldung „neben der Strecke“. Ein Abwürger am Start kommt 2 s nach dem Losfahren.
> - **Schaden in iRacing im Detail.** iRacing liefert keine Bauteilwerte. Mik sagt deshalb Pflicht- oder optionale Reparatur, die Wucht des Einschlags in g und ob es Mauer oder Auto war. In der Box nennt er die Reparaturzeit und die freien Fast Repairs. Nach dem Treffer misst er, wie viel Zeit du pro Runde verlierst, und sagt, ob sich die Reparatur bis zum Ende lohnt. Fehlt auf der Geraden Tempo, sagt er auch das. Der Schadensbefund nach Grün fehlte bisher ganz.
> - **Reifen in iRacing.** iRacing misst Reifen nur beim Halt in der Box. Genau dann sagt Mik das Reifenbild: Schnitt vorn und hinten, auffällige Verteilung innen, Mitte und außen mit Deutung (Druck, Sturz) und den meistverschlissenen Reifen. Auf Nachfrage nennt die Crew diese Werte als „Stand letzter Boxenhalt“, nie als aktuell.
> - **Boxenstopp in iRacing.** Beim Einfahren bestätigt Mik den eingestellten Service (Reifen, Liter, Fast Repair). Stehst du falsch, sagt er „zu weit vorn“ und Ähnliches.
> - **Vorfallpunkte und Verwarnungen nach Dritteln.** Mit Limit kommen Warnungen bei einem Drittel, zwei Dritteln und kurz vor der Strafe: „Rob, jetzt kannst du dir nicht mehr viel erlauben …“. Ohne Limit gibt es Hinweise nach jedem Renndrittel, auch zu Tracklimits, aber nur wenn etwas dazukam.
> - **Boxenlimit erst beim Losfahren.** Nicht mehr beim Joinen und nicht, solange du in der Box stehst.
> - **Weniger Doppeltes.** Begrüßung einmal, Meatball und Schwarz einmal, Dreher einmal, die Rivalen-Bestzeit nur die neueste. Ben wiederholt Miks Vorfallpunkte nicht. Nach einer Disqualifikation kommen nur noch die Meldung und die Bilanz.
> - **Richtige Zahlen.**
>   - Platz und Rückstand gelten in deiner Klasse, auch im Mehrklassenfeld.
>   - Kein „Platz 1000“ mehr, kein „1000 Autos“ im Feld.
>   - Quali-Rang nur aus einer vollständigen Zeitenliste.
>   - Die Startrunde mit Zwischenfall ist kein Bezug.
>   - Streckentemperatur nur gemessen, der Streckenname ohne Anhängsel.
>   - Gestrichene Runden zählen in der Bilanz mit.
> - **Sprit.** Der Verbrauch aus Training und Quali ist der Startwert im Rennen. Hochgerechnet wird erst ab einer halben Runde, und Tanken in der Garage zählt nicht als Verbrauch.
> - **Diagnose.** Die Aufzeichnung läuft nach 128 MB weiter. Verworfene Warnungen stehen im Gesprächslog, und jede Gefahrmeldung zeigt, seit wann das Auto auffällig war.
>
> **Seit Alpha 0.40: Sara startet auch auf Englisch und Spanisch.**
>
> - **Sara-Stimme in EN und ES.** Mit englischem oder spanischem Funk blieb RRCA bei „Saras gewählte Originalstimme ist vorbereitet, aber noch nicht in der Audioengine geladen“ stehen, auch nach einem Neustart. Die Startprüfung kennt jetzt die Sprache des Sara-Pakets. Danke an kamil-malinski für Fehlerbericht und Lösung.
>
> **Seit Alpha 0.39: Fehlerbehebungen nach dem RaceRoom-Rennen in Hockenheim.**
>
> - **Deine Bestzeit als Referenz.** Fährst du im Training oder Qualifying los, sagt Ben deine absolute Bestzeit für Wagen und Strecke, mit Sprit an Bord, Strecken- und Lufttemperatur: „Rob, deine absolute Bestzeit hier auf der Strecke liegt bisher bei … Das ist deine Referenz.“ Vier abwechselnde Fassungen. Funkst du gerade, wartet die Ansage bis zu zwei Minuten und kommt sonst beim nächsten Losfahren.
> - **Bestzeiten-Datenbank für RaceRoom.** RaceRoom meldet keinen Autonamen, deshalb blieb die Datenbank dort leer. Jetzt dient das Automodell der Sim als Schlüssel.
> - **Richtiger Startplatz.** Der Platz in der Startaufstellung gilt, nicht der erste Wert beim Anlegen der Sitzung. „Seit dem Start zwei Plätze verloren“ bei unverändertem Platz fällt weg.
> - **Ben dreht nichts um.** Eine frei formulierte Ansage darf die Richtung nicht umkehren („fällt zurück“ statt „holt auf“), keine Ursache erfinden (etwa einen Reifenschaden), beim Platzverlust nicht jubeln und kein „du bist schneller“ ohne Beleg behaupten. Sonst kommt der feste Satz.
> - **Richtiger Vergleich im Training.** Bestzeiten anderer vergleicht Ben mit deiner besten Runde von heute, auch aus der Hotlap davor.
> - **Verteidigung nach hinten kommt an.** „… schließt auf“ wartet länger auf freien Funk und ist wichtiger eingestuft. Ob der Abstand noch stimmt, wird beim Sprechen geprüft.
> - **Der erste Topspeed-Vergleich kommt.** Verfällt er ungehört, wird er mit frischen Werten neu angesetzt.
> - **Kein „der macht dir Platz“** mehr bei klassenfremdem Verkehr, das weiß keiner vorher.
>
> **Seit Alpha 0.38: Fehlerbehebungen nach dem LMU-Rennen in Algarve.**
>
> - **Sara hat Vorrang.** Gelb, Unfälle, stehende und langsame Wagen unterbrechen Ben und Mik sofort. Antworten auf deine Fragen unterbricht sie nur bei echter Gefahr.
> - **Zwei Stufen für alle Ansagen.** Wichtige Meldungen kommen sofort, alles andere stellt sich an und kommt, sobald Luft ist. Die Wartezeit hinter laufendem Funk zählt nicht als Alter, eine Meldung entfällt nur, wenn ihr Inhalt wirklich veraltet ist.
> - **Gelb mit Namen**: „Gelb, vorne ist ein Unfall“, „Gelb, vorne steht ein Wagen“ oder „Gelb, vorne ist ein langsames Fahrzeug“, jeweils mit dem Fahrernamen. Kontakte anderer Fahrer ohne Folgen meldet Sara nicht mehr.
> - **Ausfälle** meldet Sara auch unter Gelb.
> - **Sektor-Bestzeiten im Rennen** vergleicht Ben mit dem Sektor der schnellsten Runde deiner Klasse, mit Namen. Unplausible Gewinne aus der Startrunde fallen weg.
> - **Topspeed kommt wirklich zweimal.** Fällt die Meldung aus, kommt sie später wieder. Genannt werden nur Wagen, die noch fahren und einen Namen haben.
> - **Bens große Zusammenfassung**: einmal in Rennen bis 30 Minuten, zweimal bis 60 Minuten, dreimal in längeren Rennen.
> - **Die Kämpfe bleiben der Kern**: Vordermann, Hintermann, Abstand zur Spitze und Bestzeitvergleiche kommen wie bisher.
>
> **Seit Alpha 0.37: Bestzeiten, Topspeed und klare Zuständigkeiten.**
>
> - **Jede Bestzeit im Feld wird angesagt**, mit Namen und dem Abstand zu deiner Bestzeit, im Training, im Qualifying und im Rennen, auch in der Startrunde und in der letzten Runde. Die Meldung wartet, bis Luft ist, und verfällt erst, wenn jemand schneller war. Hast du im Rennen noch keine gewertete Runde, vergleicht Ben mit deiner Bestzeit von heute oder deiner letzten Runde.
> - **Topspeed zweimal im Rennen**: im ersten Drittel und im letzten Viertel nennt Ben die drei Schnellsten auf der Geraden und deinen Wert mit Platz.
> - **Deine Bestzeit zählt, wenn die Sim sie zählt.** Übernimmt RaceRoom eine Runde als Bestzeit, die Crew Chief als ungültig führt, kommt trotzdem die Ansage.
> - **Ben antwortet auf alles Allgemeine**: Rennlage, Abstände, Restzeit, Bestzeiten, Strategie. Mik sagt nur noch Sprit, Verbrauch, Reifen, Technik und Schäden an, kurz und ohne Diskussion.
> - **Sprit im Training sofort**: eine erste Schätzung nach wenigen hundert Metern, danach der Verbrauch der ersten vollen Runde, auch wenn die Runde gestrichen war. Fragst du nach Sprit für eine Renndauer, rechnet die Crew es in Litern vor.
> - **Sektor-Bestzeiten im Training und Qualifying** kommen sofort und kurz, bei deiner schnellsten Runde gibt es ein kurzes Lob.
> - **Rennleitung für alle Sims**: Verwarnungen und Strafen kommentiert Ben in eigenen Worten, mit Namen.
> - **Überrundete** werden nur noch gemeldet, wenn du wirklich aufholst, nicht bei gleichbleibendem Abstand.
> - **Vor dem Start** hält die Crew das Rennen nicht mehr für beendet, und die Restzeit nennt sie als aktuellen Wert.
>
> **Seit Alpha 0.36: Fehlerbehebungen.**
>
> - **Richtige Klasse im Multiclass-Rennen** (LMU, AMS2, iRacing). Die Rennbilanz nennt den Sieger deiner Klasse statt des Gesamtsiegers. Klassenplatz und Topspeed der Klassenspitze stimmen.
> - **FCY und Rennunterbrechung in LMU.** Unter FCY oder bei Rennunterbrechung gilt nicht mehr das halbe Feld als „langsam“ oder „steht“.
> - **Sprit ehrlich benannt.** Fragst du nach dem Sprit für eine Distanz ohne eigene Literangabe, sagt die Crew, woher der Verbrauch stammt: gemessen, hochgerechnet oder aus Training und Quali geschätzt. Nur deine eigene Zahl heißt „dein Wert“.
> - **Ben verändert keine Zahlen.** Eine frei formulierte Zweikampfansage kann aus „12 Sekunden“ nicht mehr „2 Sekunden“ machen.
> - **Kein Name geht verloren.** Kommt Saras Sammelansage für Boxenstopps oder Ausfälle nicht durch, kommen die Namen noch einmal. Bei sehr vielen Meldungen fällt zuerst die unwichtigste weg, nie ein Ausfall.
> - **Diagnose.** Die Antwortzeit auf deine Fragen misst nur noch die Antwort selbst, nicht eine andere Meldung dazwischen.
>
> **Seit Alpha 0.35: Die Crew funkt nach Lage.**
>
> - **Keine Funkstufen mehr.** Ben, Mik und Sara entscheiden selbst, wann eine Info kommt: Ereignisse sofort, Vorder- und Hintermann am häufigsten, Technik, wenn Luft ist.
> - **Spotter per Funk.** „Spotter ein“ und „Spotter aus“ wirken sofort und gelten bis zum Neustart, auch beim Wechsel vom Qualifying ins Rennen.
> - **Nichts geht mehr verloren.** Eine Meldung, die du nie gehört hast, gilt nicht als gesagt und kommt beim nächsten Anlauf. Reifen und Fahrzeugzustand gehen mit einem verworfenen Bericht nicht mehr unter.
> - **Sektorbestzeiten im Rennen**, auch mitten im Zweikampf.
> - **Ehrlichere Ansagen.** „Schließt auf“ nur, wenn der Hintermann wirklich näher kommt; „zurückgeholt“ nur nach einem echten Platzverlust. Im Rennen heißt eine schnellere Runde „beste Rennrunde“, die Startrunde ist kein Maßstab mehr.
> - **Rundenansagen zuerst.** „Noch 2 Runden“ und „Letzte Runde“ kommen vor der Rundenzeit.
> - **Mik notiert auf Zuruf** („Schreib das auf“).
> - **Stabil.** Ein schneller Stopp und Neustart zwischen Quali und Rennen bringt Crew Chief nicht mehr zum Absturz, der Spottermodus wird gespeichert.
>
> **Seit Alpha 0.32: Die Crew fiebert mit.** Ben kommentiert Überholmanöver und Zweikämpfe mit Gefühl und der Vorgeschichte des Duells, jedes Mal anders (kommt die KI-Antwort nicht binnen 2,5 Sekunden, spricht er den festen Satz). Sara meldet Boxenstopps, Ausfälle und Disqualifikationen im ganzen Feld, mehrere in einem Satz, auch Dreher und Unfälle hinter dir. Ben sagt im Zeitrennen 15, 10, 5 und 2 Minuten vor Schluss die Restzeit mit deinem Platz an. Nach der Zielflagge nennt Mik auch deine Boxenstopps.
>
> **Funkbefehle während der Fahrt.** Funktaste drücken und sagen:
>
> | Sag | Was passiert |
> |---|---|
> | **„Spotter aus“** / **„Spotter ein“** | Nur Saras Seitenansagen (links, rechts, frei). Gelb, Unfälle, langsame Autos und Safety Car meldet sie immer. |
>
> Das gilt sofort und bis du RRCA neu startest, auch beim Wechsel vom Qualifying ins Rennen und im Spottermodus Realistisch. Eine Funkmenge gibt es nicht mehr: Die Crew funkt immer nach Lage. Ereignisse kommen sofort, Vorder- und Hintermann am häufigsten, im Zweikampf noch öfter, und die Technik dann, wenn Luft ist.

RRCA ist eine kostenlose, inoffizielle Erweiterung für [Crew Chief V4](https://gitlab.com/mr_belowski/CrewChiefV4). Sie gibt dir eine Boxencrew aus drei Stimmen, mit der du im Rennen sprechen kannst:

- **Ben** beobachtet das Rennen und kümmert sich um die Strategie.
- **Mik** ist dein Renningenieur für Auto, Reifen und Sprit.
- **Sara** ist deine Spotterin und warnt dich vor Autos neben dir. Sie läuft direkt auf deinem PC und wartet nie auf das Internet.

Du drückst einen Knopf am Lenkrad, fragst in normalen Worten, und die Crew antwortet mit den Live-Daten aus deiner Simulation. Liefert das Spiel einen Wert nicht, sagt die Crew das, statt etwas zu erfinden.

> **Testversion.** Die Crew spricht **Deutsch, Englisch oder Spanisch**, du wählst. Das Programmfenster gibt es in denselben drei Sprachen, es richtet sich nach deiner Windows-Sprache. Ansagen können falsch, verspätet oder ausbleiben. Nur für Rennsimulationen.

> **Schwerpunkt Simulationen.** Aktuell wird RRCA für **Automobilista 2**, **RaceRoom** und **Le Mans Ultimate** optimiert. Danach folgen **Assetto Corsa EVO** und **iRacing**. Andere Simulationen, die Crew Chief unterstützt, funktionieren auch, sind aber noch weniger ausgefeilt.

**[Aktuelle Version herunterladen](https://github.com/JoshuaGerke/RobertsCrewChiefAdvanced/releases/latest)** · Im Download liegt außerdem eine bebilderte Schritt-für-Schritt-Anleitung: `ANLEITUNG_RRCA_DE.pdf`.

RRCA ist kostenlos. Wenn es dein Rennen besser gemacht hat und du einen Fünfer übrig hast, kannst du uns [auf Ko-fi einen Kaffee ausgeben](https://ko-fi.com/robertscrewchiefadvanced).

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
- Danach braucht ein typisches Rennen etwa 3.000 bis 10.000 Zeichen, je nachdem, wie viel du mit der Crew sprichst und wie viel im Rennen passiert.

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

**g) Funkberichte.** Eine Funkmenge stellst du nicht ein, die Crew funkt immer nach Lage. Unter **KI & Funk** → **Regelmäßige Funkberichte** schaltest du Themen nur an oder aus. Den Takt bestimmt die Crew: In kurzen Rennen bis 15 Minuten kommt jedes Technikthema ein- bis zweimal, in längeren Rennen öfter. Vorder- und Hintermann kommen am häufigsten, im Zweikampf doppelt so oft. In ruhigen Phasen kommen Reifen, Schaden, Wetter und Streckentemperatur; im Zweikampf wartet die Technik. Ereignisse wie Schaden, Strafe, Gelb oder Bestzeit kommen immer sofort.

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
