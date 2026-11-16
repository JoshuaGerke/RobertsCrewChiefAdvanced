# RRCA Intern – Interlagos und Masterauftrag c931ad4

Basis ist das lokal vorhandene Quellarchiv zu `c931ad49c0f9b077d58bafbb57d82365cc4163ca`. Der Import ohne Git-Historie beginnt im getrennten Entwicklungsordner `development/RRCA-c931ad4-Interlagos` bei `e42194b`, Branch `intern-c931ad4-interlagos`. Der genaue Paketstand steht in `VERSION.txt`.

## Interlagos

- **Disqualifikation**. Eine bestätigte Meldung bleibt beim Boxenwechsel und einer Rangänderung zustellbar und wird unmittelbar vor der Ausgabe geprüft. Sechs lokale Varianten pro Funksprache in `Rennfunk.de.json`, `Rennfunk.en.json` und `Rennfunk.es.json`. Mik verwendet den eingestellten Funknamen. Innerhalb eines App-Laufs bleiben die letzten drei vollständig gespielten Varianten gesperrt. Vorschau über „Team & Stimmen → Rennfunk im Stand anhören → Disqualifikation“.
- **Bestzeit**. Die eigene Bestzeit bleibt in derselben Runde bis zu 22 Sekunden gültig. Sie kann optionale Zeitmeldungen anderer verdrängen. Der Interlagos-Wortlaut „Gegenüberstellung“ wird bei passender Frage lokal verarbeitet.
- **Sprit**. Nach drei vollständig gemessenen Runden entsteht ein eigener kurzer Verbrauchsbericht. In Interlagos hatte die Aufnahme valide Spritmessungen gezeigt, aber mehrere geplante Berichte erreichten den Audiotreiber nicht.

## Masterauftrag in Arbeitsreihenfolge

1. **Telemetrie**. `TelemetrySourceClock`, `CrewChief.cs` und `R3EGameStateMapper` verarbeiten frische RaceRoom-Rennframes mit einem Zielintervall von 20 ms und reichen validiertes Gas, Bremse und Kupplung samt Quellzeit weiter. Schnellere Ortsstichproben in RaceRoom und iRacing nutzen für das Schließtempo ein getrenntes Fenster. Doppelte und alte Quellframes werden verworfen.
2. **Stint-Gedächtnis**. `RaceReader` hat bis zu fünf Abstandsrunden für denselben Gegner und dieselbe Messmethode. `RaceFeedbackWatch` korrigiert den Sitzungstyp der Konstanz- und Verteidigungsmeldungen. `FuelWatch` registriert ein belegtes Sparziel nach vollständig abgespieltem Rat. Ben wertet zwei grüne, vollständig gemessene Folgerunden aus. Boxenbesuche brechen den Vergleich ab. Historische Referenzen nutzen weiter die bestehenden Speicher.
3. **Streckenkarte**. `NativeIncidentWatch`, `RaceTrafficWatch` und `LiveWarningDispatcher` gleichen Gefahren anhand der Fahrzeugidentität ab. Der bekannte Abschnitt setzt einen fertigen Audioclip und eine eindeutige, vor der Wiedergabe erneut geprüfte Landmarke voraus. Sonst wird die allgemeine Warnung verwendet. Ben kann den letzten gültigen Sektor des direkten Rivalen mit dessen drei vorherigen vergleichbaren Runden vergleichen. Zeitverlust über 0,5 s liefert keine Aussage über die Ursache.
4. **Relevanz**. `RaceFieldTools` vergleicht dieselben beiden Rennrunden der führenden Gegner derselben Klasse. `AiRadioSchedule` und `RaceRadioDirector` warten mit Routinezahlen bei gemessenem Abstand unter einer Sekunde und in den sicher bekannten letzten zwei Runden.
5. **Funk und Lautstärke**. `RadioGainSampleProvider` verteilt Änderungen über rund zehn Millisekunden, auch über mehrere kurze Audioblöcke. Der kurze Gegnersektor-Funk prüft die Fahrbelastung vor der Ausgabe. Saras priorisierter Audiopfad ist unverändert.
6. **Diagnose**. Unter „KI & Funk → Fahrer, Telemetrie & Diagnose“ erscheinen Datenquelle, Routinefunk und aktives Sparziel. `RrcaForm` übernimmt die Daten per vorhandenem UI-Dispatcher im 200-ms-Takt und sendet nur Abweichungen. Beschriftungen in DE, EN und ES.

## Offene Abnahme

- Native Frequenz, Latenz und CPU-/Frametime-Auswirkung in einer Live-Session messen. Der 20-ms-Poll ist ein Ziel für RaceRoom. Die übrigen nativen Adapter müssen feldweise geprüft werden. Der optionale SimHub-UDP-Fallback ist offen.
- Weitere Mikro-Missionen und die gemeinsame Stint-Timeline für alle fünf Speicherpfade ausbauen.
- Die belastbare Live-Prognose für freie Boxenausfahrt oder Pulk und ein passender Abschnitt zum Überrundenlassen sind offen.
- Reifen-/Bremsdaten, Sektorverlust und Gegnerdruck weiter als belegten Zusammenhang verknüpfen. Automatische Übergaben zwischen Ben und Mik sind offen.
- Mit dem Recorder Start, Bestzeit, Spritfunk, ein stehendes Fahrzeug, Saras Vorrang und die Diagnosezeile prüfen. Die DQ-Sätze lassen sich mit Miks Stimme im Stand einzeln anhören.


## Build- und Testnachweis

- Windows-Release von CrewChiefV4 und UnitTest als AMD64 gebaut. 0 Buildfehler und 194 Warnungen im vollständigen x64-Lauf. PE-Architektur beider Dateien geprüft.
- 1.099 KI-Tests in isoliertem Datenordner bestanden. JavaScript-Syntax, Diagnose-Element, Disqualifikationsauswahl und alle neun neuen Übersetzungsschlüssel geprüft.
- Die importierte Lösung definiert keine Konfiguration namens Release|x64. Der Runtime-Build verwendet ihre vorhandene Release-Konfiguration und das x64-Plattformziel. Beim Versuch, die komplette Projektmappe zu bauen, fehlten auch Abhängigkeiten von mitgelieferten Hilfsprojekten.
- Die automatische Prüfung ist keine Audio- oder Telemetrieabnahme im Simulator. Die bisherige Installation wurde nicht für die Prüfung gestartet oder überschrieben.

## Fortsetzung – Paket 1

- Native Zielkadenz von 20 ms für iRacing, AMS2 Shared Memory, RF2, LMU, RaceRoom, AC, ACC, AC EVO und PMR bei frischer Quelle. Aufwendiges Mapping und Diagnose können die Rate senken. Die Messung im Simulator steht pro Adapter aus.
- ACC und AC EVO stempeln Grafik und Physik getrennt. Pausierte oder ersetzte Physikdaten bekommen keine neue Paketzeit. PMR speichert Empfangszeiten pro Fahrzeug und kanalweise getrennt.
- Die pedalseitigen Messwerte weisen bei den genannten Simulatoren Quelle, nativen Feldnamen und UTC aus, sofern ein belegter Zeitstempel vorliegt. Pausen, fehlende Felder und alte Gegnerpositionen werden nicht künstlich aufgefüllt.
- Optional empfängt der bestehende PMR-Decoder weitergeleitete native PMR-Pakete ausschließlich auf localhost. `RRCAPmrLoopbackPort=0` in `CrewChiefV4.exe.config` belässt den nativen Multicast. Ein optionaler Wert wie 7578 entspricht dem Weiterleitungsziel `127.0.0.1:7578`. Es werden nur bekannte PMR-Paketversionen dekodiert. Die Weiterleitung stellt keine zusätzlichen Datenfelder bereit. Abschnitt „Project Motor Racing“ in den offiziellen SimHub-Release-Notes. https://www.simhubdash.com/download-2/
- x64-App und Tests gebaut. 1.105 KI-Tests grün, einschließlich Quellzeit, Namen/Identitäten und Portvalidierung. Live-Raten, echte SimHub-Weiterleitung und zusätzliche SDK-Felder für die einzelnen Sims bleiben in der Fahrabnahme.

## Fortsetzung – Paket 2

- Im Lagebild stehen bis zu fünf eigene Runden mit Messzeit und belegtem Verbrauch, die beobachteten Runden der direkten Klassengegner und die Abstandsreihe mit identischer Fahrzeugidentität und Messmethode. Eine passende Vorstint-Referenz erscheint separat.
- Nach einem vollständig gesprochenen Hinweis auf den direkten Vordermann prüft RaceFeedbackWatch zwei volle grüne Runden mit demselben Rivalen und derselben Abstandsmethode. Ein Gegnerwechsel, ein Boxenbesuch oder eine Messlücke beendet oder verzögert die Auswertung. Die Runde mit dem laufenden Hinweis zählt nicht.
- AMD64-Build ohne Fehler. 1.107 KI-Tests bestanden.

## Fortsetzung – Paket 3

- PitCycleWatch misst Einfahrt, Halt und Ausfahrt eines tatsächlichen Boxenbesuchs. Danach kann Mik beim nächsten Anlauf mit frischen Gegnerpositionen und stabilen Rundenzeiten eine geschätzte Verkehrslage melden. Ohne vollständige Abdeckung gibt es keine Freimeldung. Ein Drive-through ohne Halt liefert keine Stoppdauer.
- Analysewerkzeug get_pit_info enthält Boxenlagedaten, gemessene Durchfahrtsdauer des bisherigen Stopps und geschätzten Zeitverlust. Abweichende Servicearbeit, Wetterwechsel und unbekannte Gegner lassen die Schätzung unsicher. Geprüft gegen auflaufenden Verkehr, freie Linie, fehlende Fahrzeuge und alte Positionsdaten.
- AMD64-Build ohne Fehler, 1.110 KI-Tests bestanden. Reale Boxenstopps pro Simulator und Überrundung durch schnelle Gruppen bleiben in der Live-Abnahme.

## Fortsetzung – Paket 4

- RaceReader verknüpft eine anhaltende und fahrzeugspezifische Temperaturwarnung mit einem frischen Sektorverlust oder einem zuverlässig schrumpfenden Abstand nach hinten. Die beiden Messbefunde bleiben getrennt. Der zusätzliche Mik-Funk ist kurz, drehzahl- und zonenunabhängig, benötigt ruhiges Fahrfenster. Die normale Temperaturwarnung bleibt sofort verfügbar.
- RaceRadioDirector unterscheidet die Rennphasen OpeningLaps, StintMiddle, PitWindow, Battle und FinalLaps. Routinezahlen bleiben in engem Duell und am Rennende zurückgestellt.
- AMD64-Build ohne Fehler. 1.111 KI-Tests bestanden.

## Fortsetzung – Paket 5

- Mik kann nach vollständig abgespielter Temperaturwarnung an Ben übergeben. Ben wartet mindestens zwei Sekunden und spricht nur, wenn das Boxenfenster offen, die Temperaturwarnung noch aktuell und eine freie Lücke weiter wahrscheinlich ist. Ben prüft die Bedingungen erneut vor Wiedergabe. Er nennt die Option, ohne einen Stopp zu befehlen.
- Ein Cooldown gilt für beide möglichen Boxenausfahrtsmeldungen. Saras Funkpriorität und akute Gefahrenmeldungen werden nicht umgeordnet.
- AMD64-Build ohne Fehler. 1.112 KI-Tests bestanden.

## Fortsetzung – Paket 6 und aktueller Prüfstand

- Die Diagnose zeigt bei frischen Daten beobachtete Eingangsrate, Funkfreigabe, Sparziel, Aufholphase und verfügbare Boxenausfahrt-Prognose. Übergabe weiter über den UI-Dispatcher mit 200 ms Mindestintervall und Prüfung auf tatsächlich geänderten Inhalt. Rendering, Frischesperre und Textschlüssel in DE, EN und ES separat geprüft.
- Finaler Lauf der x64-App und Testprojekte: 0 Kompilierfehler, 196 Warnungen. Alle 1.112 KI-Tests bestanden. In der importierten Lösung gibt es keine eigene Release|x64-Konfiguration. Verwendet wird Release mit x64-Plattformziel. Die vollständige Projektmappe benötigt zusätzliche Abhängigkeiten für ältere Hilfsprojekte.
- Fahrabnahme offen: tatsächliche Aktualisierungsrate und Audiolatenz je Simulator, PMR-Weiterleitung mit realen Paketen, Boxenausfahrt bei unterschiedlichen Servicezeiten und Funkübergabe im Rennen. Die Prognose braucht mindestens einen belegten Stopp, gültige Runden und frische Feldpositionen.
- Erweiterungen offen: zusätzlich verfügbare SDK-Felder pro Simulator bis zur Quelle prüfen, weitere Handlungsempfehlungen als Missionen verfolgen und einen geeigneten Streckenabschnitt zum Überrundenlassen mit ausreichender räumlicher Evidenz berechnen.

## Claude P1 – Telemetrie-Messung RaceRoom (16.11.2026)

- Neuer, abschaltbarer Messmodus „Telemetrie-Messung einschalten (RaceRoom)“ unter „Fahrer, Telemetrie & Diagnose“. Ein eigener Lesefaden öffnet das RaceRoom-Shared-Memory nur lesend, tastet etwa jede Millisekunde ab und berührt weder Mapper, Hauptschleife, Funk noch Saras Spotter. Standard: aus; nach Neustart wieder aus.
- Gemessen werden 129 Kanäle (Spieler, Sitzung, eigener Eintrag in der Fahrerliste, 14 Kanäle je Gegnerfahrzeug, gezählt über den Fahrzeug-Slot). Je Kanal: vorhanden/konstant/fehlt (-1), Wertebereich, Änderungen pro Sekunde, Anteil der Quellbilder mit Änderung, Intervall p50/p95/max, Änderungen ohne neues Quellbild. Dazu die Bildrate der Quelle, der Physik-Takt, die eigene Abtastung, Lesezeit und die CPU-Last von Crew Chief.
- Pausen, Menüs und Wiederholungen werden getrennt gezählt und verlängern keine Intervalle. Gegner, die die Liste kurz verlassen, erzeugen keine Scheinlücken.
- Offsets werden aus RaceRoomData.cs berechnet (PackedLayout, auch für generische Felder) und beim Start gegen den Windows-Marshaller sowie gegen die Kopfwerte des Spiels (Version, AllDriversOffset, DriverDataSize) geprüft. Bei Abweichung misst die Probe nicht und meldet den Grund.
- Bericht alle 30 s und beim Ausschalten in `RCCA-LiveDiagnostics\Telemetrie-Probe`: CSV (Semikolon, Dezimalkomma), JSON, Zusammenfassung.txt. Keine Fahrernamen, keine Schlüssel. Während der Messung wird die Windows-Zeitauflösung für diesen Prozess auf 1 ms gesetzt und danach zurückgesetzt.
- Tests: `UnitTest/AiTeam/TelemetryProbeTests.cs` (7 Tests: Layout gegen Marshaller, Werte-Rundlauf über StructureToPtr, Raten bei 62,5 Hz Quelle und langsamerer Gegnerkarte, Pause, Gegnerwechsel, Histogramm, Bericht ohne Namen).
- **Ungeprüft**: Build und Tests (in der Cloud nicht ausführbar), echte Raten in RaceRoom, CPU-Wirkung. Abnahme durch Rob: bauen, Tests, je eine kurze Fahrt offline und online mit Gegnern, Bericht zurückgeben.

## Claude P1b – Telemetrie-Messung auch für AMS2 (16.11.2026)

- Die Messung ist jetzt spielunabhängig (ProbeAnalyzer) und erkennt selbst, ob RaceRoom oder AMS2 läuft. Stehen bei einem Spiel 5 s lang keine neuen Bilder an (im Menü 15 s), wird das nächste Spiel geprüft. Je Spiel ein eigener Bericht (`R3E_…`, `AMS2_…`).
- AMS2: Shared Memory „$pcars2$“, Offsets direkt vom Marshaller (Standard-Ausrichtung). Nur vollständig geschriebene Bilder zählen (Sequenznummer gerade und vorher/nachher gleich); verworfene Versuche stehen im Bericht. 131 Kanäle, darunter ABS-Eingriff, Reifen (Lauffläche, Karkasse, Luft), Wetter und 12 Kanäle je Gegner (Weltposition, Streckenmeter, Platz, Tempo, Gierwinkel, Boxenmodus, Rundenzeiten).
- Tests: drei weitere AMS2-Tests (Layout gegen den vorhandenen Reader, Werte-Rundlauf über StructureToPtr, Raten mit Pause und langsamerer Gegnerkarte). Ungeprüft: Verhalten im echten AMS2.

## Claude – Erkenntnisse aus Robs AMS2-Fahrt 16.11.2026 (Bathurst, Training)

Grundlage: Diagnoseprotokoll 16:04–16:16 und Telemetrie-Messung (AMS2 liefert 118 Bilder/s, RRCA nutzte 48).

- **Sprit-Hochrechnung ab den ersten Metern:** Bisher blockiert durch (1) Sperre bei „ungültiger Runde“ – AMS2 meldet die Outlap als ungültig –, (2) Mindeststrecke 25 % der Runde, (3) Neustart an jeder Ziellinie. Jetzt: Verbrauch je Meter seit Boxenausfahrt mal Rundenlänge, ab 300 m und 0,05 l, unabhängig von der Rundengültigkeit, über die Ziellinie hinweg; Stillstand und leichtes Zurückrollen unterbrechen nicht. Neu beginnt sie nur nach Boxengasse, Nachtanken, Reset/Teleport, Auto- oder Streckenwechsel und alten Daten. Im Funk immer als Schätzung (vier Formulierungen), gemessene volle Runden haben Vorrang. Die bisherige Schwelle im Test VerifiedV06 wurde bewusst auf 300 m angepasst (Robs ausdrücklicher Wunsch).
- **AMS2-Gegnerzeiten:** Bestzeit und letzte Runde werden aus mFastestLapTimes/mLastLapTimes übernommen, wenn Crew Chief sie selbst nicht gesehen hat (z. B. vor dem Verbinden gefahren). Eigene Beobachtungen werden nie ersetzt, es entsteht kein Rundenereignis.
- **Boxenanforderung:** Die vorhandene Limit-Erinnerung bei Anmeldung beginnt jetzt mit einer kurzen Bestätigung (vier Varianten, ohne „diese/nächste Runde“).
- **Weniger Boxenlimit-Wiederholung:** Wurde vor der Boxengasse bereits erinnert (Anmeldung/Anfahrt), entfällt die Ansage beim Einfahren. Die Erinnerung beim Losfahren aus der Box bleibt.
- **Boxenzeiten (PitStopTimer):** Einfahrt bis Halt, Standzeit, Ausfahrt und Gesamtzeit in allen Sitzungsarten, gemessen mit der Sitzungsuhr. Ben meldet sie einmal nach der Ausfahrt; für Fragen im Analysewerkzeug „letzter_boxenbesuch“. Durchfahrt ohne Halt wird so benannt, Garage/Zurücksetzen zählt nicht.
- Tests: FuelProjectionTests (6), PitAndFieldFindingsTests (4). Offen: Zeitüberschreitung „keine Antwort von der Auswertung“ (16:12) – Ursache im Protokoll noch nicht untersucht.
- **Sektoren (Robs Wünsche 14.–16.11.):** Sektorbestzeiten mit drei Nachkommastellen, jede zweite Meldung mit persönlicher Anrede („Rob, deine Bestzeit in Sektor 2 …“) und – wenn die Runde noch läuft und gültig ist – ein kurzer Ansporn aus der neuen Kataloggruppe „own-sector-best-push“ (6 Varianten je Sprache). Im Rennen seltener und nie im engen Zweikampf (<1 s). Neu: erste Sektorzeit ohne Referenz einmal je Sitzung, sonst „knapp an der Bestmarke“ (bis 0,15 s) höchstens alle 3 Minuten, nur Training/Qualifying.
- **AMS2 nach Neustart mitten in der Sitzung:** eigene Bestzeit und Bestsektoren aus mPersonalFastest* übernommen, wenn unbekannt (vorher fehlten alle Referenzen, daher keine Sektormeldungen).
- **Box:** Bestätigung beginnt jetzt u. a. mit „Box, Box, Rob.“; Bens Boxenmeldung nennt die nachgetankte Menge.

## Claude – Rennkopf und Erkenntnisse aus Robs RaceRoom-Rennen 16.11.2026 (Mantorp Park)

Ausgangslage: Disqualifikation bei 30/30 Vorfallpunkten, obwohl RaceRoom den Zähler lieferte (Messung bestätigt). Weitere Befunde: unklare letzte Runde im Zeitrennen, „vorgerückt“ bei Platzverlust, gleichförmige Meldungen, Zeitüberschreitungen bei Fragen.

- **Vorfallpunkte (IncidentPointsWatch):** RaceRoom/iRacing-Zähler gegen das Sitzungslimit. Mik meldet gestaffelt beim Anstieg: erster Punkt, Hälfte, drei Viertel, ab „noch 3“ jeder Punkt (Priorität 7), „noch 1“ deutlich. Beim Verbinden mitten im Rennen nur Ausgangsstand. Im Analysewerkzeug get_race_state: vorfallpunkte, _limit, _frei.
- **Letzte Runde im Zeitrennen (LapWatch.PlanTimedFinish):** Bei Ablauf der Uhr entscheidet, wer zuerst über die Linie kommt (Führender oder wir, Rundenanteil), plus Zusatzrunden des Formats. Ansage „Die Uhr ist abgelaufen, Rob. Das ist deine letzte Runde!“ oder „Noch diese Runde, dann kommt die letzte“; LapsLeft liefert danach 1 bzw. 2 statt „unbekannt“. Ohne eindeutigen Führenden bleibt die bisherige Ansage.
- **Platzmeldungen:** „vorgerückt“ nur, wenn die Position besser ist als die zuletzt gemeldete; sonst neutrale Standmeldung. Verlust und Gewinn ohne Überholnachweis in je fünf Varianten, teils mit Namen. Steht der überholte Klassengegner direkt hinter uns (<120 m, beide in Fahrt), gilt es als Überholmanöver und bekommt die lebendigen Überhol-Sätze.
- **Rennkopf – Zwischenresümee (RaceSummaryWatch):** 1–3 je Rennen nach Renndauer, zufällig verteilt, nie in den letzten zwei Runden, nur wenn kein Auto innerhalb von 5 s ist (gemessen oder über Streckenmeter), keine Gelbphase, nicht in der Box. Inhalt: Platz und Bilanz seit Start, Abstände mit Trend über die letzten Runden, Sprit-Urteil, Vorfallpunkte wenn knapp. Schlusssatz nach Lage: angreifen, verteidigen, sicher fahren, führen oder gleichmäßig – je vier Varianten in DE/EN/ES.
- **Stehende Autos:** gleiche Stelle → einmal melden, einmal kurz erinnern, dann still; fährt das Auto wieder, gilt es neu.
- **Gegner-Bestzeiten:** Satz und Ansporn drehen jetzt auch ohne Wiedergabe-Rückmeldung weiter (vorher immer „Da geht noch was, bleib dran.“).
- **Erste gezeitete Runde:** kurzer Vergleich mit der aktuellen Klassenbestzeit (mit Namen, wenn erlaubt und eindeutig), drei Varianten.
- **Begrüßung im Training/Hotlap:** Fehlt die Standbegrüßung, kommt einmal eine kurze Begrüßung auf der Strecke (nie im Rennen, nicht unter Gelb/Überlappung).
- **P4 Antworttempo:** Wird die Zeit knapp (unter 4,5 s Rest), keine weitere Werkzeugrunde – das Modell muss mit der Lagebeschreibung antworten. Bei Zeitüberschreitung lokale Antwort oder kurze Lage aus Messwerten statt „keine Antwort von der Auswertung“.
- Tests: RaceMindTests (9). Offen: Name-Abgleich der Spracherkennung mit dem Fahrerfeld; tieferer Umbau P4 (eine Anfrage, schnelles Modell) nach Messung im Rennen.
- **Nachtrag (gleicher Tag):** Startplatz-Frage („Welche Position zum Start?“) wird lokal beantwortet (vor dem Start: „Du startest von Platz …“, im Rennen mit Bilanz); kurze Positionsfrage ohne „mein“ gilt als eigene. Bei Tracklimit-Zweifel bleibt die Platzmeldung neutral (kein „Sauber“).
- **Rennkopf Teil 2 – Abstandstrend (GapTrendWatch):** Je Runde der gemessene Klassenabstand zu Vorder- und Hintermann (nur derselbe Gegner, keine Box-Runden, keine reinen Streckenmeter-Schätzungen). Nach drei Runden gleichgerichtetem Trend (höchstens ein kleiner Ausreißer) rechnet Ben hoch: „Du holst 3 Zehntel pro Runde auf Keller. In etwa 5 Runden bist du dran.“ Reicht es bis zur Flagge nicht, sagt er das und nennt das nötige Tempo; kommt der Hintermann bis zum Ende nicht heran, beruhigt er. Nicht im Zweikampf (<1 s), nicht in der letzten Runde, je Gegner höchstens alle vier Runden, insgesamt höchstens alle zwei Runden.
- **Stillstände der Gegner (OpponentStopLog):** Rob fragte „Der steht ziemlich oft draußen, der Sergi Reche“ – das Analysewerkzeug kannte nur „in der Box“. Jetzt je Gegner: stillstaende_auf_strecke, steht_gerade_auf_strecke (unter 1 m/s für 3 s außerhalb der Box, nur wenn er vorher in Fahrt gemessen wurde).

## Claude – P6 Renningenieur-Kopf (RaceMind), 16.11.2026 abends

Robs Kernwunsch: eine zentrale Stelle, die die Rennlage zusammenführt und bewertet und entscheidet, was jetzt wirklich wichtig ist.

- **RaceMind (neu, zentral):** pro Tick ein Lagebericht (RaceSituation, threadsicher veröffentlicht):
  - Rennphase: vor dem Start, Startrunde, früh, Mitte, spät, Schlussphase (2 Runden), letzte Runde, beendet.
  - Lage ums Auto: Boxengasse, Gelbphase, Zweikampf (<1 s oder Spotter-Überlappung, 3 s Nachlauf), Verkehr (<2,5 s oder Auto in 2,5 s Fahrstrecke, 2 s Nachlauf), freie Fahrt, ruhig (nichts innerhalb 5 s).
  - Trends: rundenweise Abstandsmessung zu demselben Vorder-/Hintermann (gemeinsame Quelle für Abstandstrend und Zwischenresümee), Hochrechnung „dran in Runden“.
  - Gefahren (Gelb, Hintermann dran/holt auf, Vorfallpunkte knapp, Sprit reicht nicht) und Chancen (Vordermann in Reichweite/einholbar/in der Box).
  - Fokus: sicher ins Ziel > verteidigen/angreifen > nach Hause bringen > Rhythmus.
- **Zentrale Freigabe in QueueLocalRaceCall:** zulassen, zurückhalten (2 s, zählt nicht als Zustellversuch, ObservedRaceCall.HoldBack) oder verwerfen. Gesteuert werden nur bekannte Info-Meldungen (Gegner-/Feldnachrichten, Statistik, Routinestatus, Kurven-Coaching, Begrüßung, Boxenzeit, leichte Wetterinfos, Zwischenresümee, Abstandstrend, eigene Sektor-Motivation). Sicherheit, Warnungen, Box, Vorfallpunkte, Platzwechsel, eigene Runden, Taktik und alles Unbekannte gehen unverändert durch (Bestandsschutz). Ohne frische Lage oder außerhalb des Rennens wird nichts blockiert.
  - Schlussphase/letzte Runde: Hintergrundinfos und Zwischenresümee verfallen; Hochrechnung in der letzten Runde verfällt.
  - Startrunde, Zweikampf, Gelb, Verkehr: Hintergrund wartet; Zwischenresümee nur in ruhiger Minute.
  - Fokus Angriff/Verteidigung: Nachrichten über andere Autos verfallen.
- **Zwischenresümee:** Schlusssatz folgt dem Fokus des Rennkopfs. **KI-Auswertung:** get_race_state enthält „rennkopf“ (Phase, Lage, Fokus, Gefahren, Chancen, Trends); Persona-Hinweis ergänzt. **Doppelmeldung:** KI-Trendbericht entfällt, wenn Ben denselben Trend in den letzten 4 Runden schon gemeldet hat.
- Diagnose: LiveDiagnostics „race-mind/state“ bei jeder Lageänderung, „held“/„suppressed“ mit Grund „race_mind:…“.
- Tests: RaceMindTests (Phase, Lage mit Nachlauf, Fokus, Freigabe-Matrix, Sicherheit nie blockiert, HoldBack ohne Versuchsverbrauch, Beschreibung).

## Claude – Erkenntnisse aus Robs AMS2-Qualifying und -Rennen in Mosport, 16.11.2026 abends

Robs Funk-Anmerkungen an die KI (Live-Diagnose ausgewertet):
- **„Hinten links … ziemlich heiß … dir fehlt Traktion und Grip zurzeit. Warte, bis sie sich abgekühlt haben. Bitte einbauen.“** und **„auch bei dreckigen Reifen, wenn der Schmutz physikalisch mitberechnet wird“** → TyreGripWatch (Mik): überhitzte Ecken (über dem Crew-Chief-Fenster für Klasse/Reifentyp +3 °C oder ≥12 °C heißer als die anderen drei und ≥85 °C, 4 s Dauer) mit Grip-/Traktionsfolge und Abkühlhinweis in vier Varianten; einmal je Stint „Reifen im Fenster, jetzt kannst du pushen“; Schmutz gemessen (RaceRoom/rF2-Status) oder nach einem Ausflug neben die Strecke ausdrücklich als Vermutung. Sehr breite Sammelfenster (unbekannter Reifentyp, z. B. 60–117 °C) werden nicht als Zahl genannt.
- **„Welche Temperatur meine Reifen am besten Fenster haben“** (Antwort war „Zieltemperatur fehlt“) → Reifenwerkzeug und lokale Reifenantwort nennen das Crew-Chief-Richtwertfenster, überhitzte Ecken und Schmutz.
- **„Ich hab zu viel Sprit für Quali … das habt ihr vergessen“** → QualiFuelWatch (Ben): nach der ersten gemessenen Runde einmal je Qualifying, wenn der Sprit mindestens drei Runden länger reicht als die Restzeit braucht (Runden und Liter, ohne erfundenen Zeitverlust).
- **„Ihr hättet schneller reagieren müssen, da stand ein Auto fast auf der Strecke“** → Auswertung: Das Auto stand neben der Fahrbahn; AMS2 lieferte für diesen Wagen Streckenmeter, die nicht zur Weltposition passten (räumlich inkonsistent) – solche Autos werden bewusst nicht als Gefahr gemeldet. Offen: sichere Erkennung „Auto neben der Strecke voraus“ aus Weltposition und Fahrtrichtung.
- Fehler behoben: „Sektor 1 nur 0,000 Sekunden über deiner Bestmarke“ (gleiche Zeit wird nicht gemeldet); „Rob, Eine Runde noch“ (Namensanrede mit richtiger Kleinschreibung); Ausfahrt aus der Garage zu Sitzungsbeginn wurde als Boxenstopp gemeldet (jetzt nur nach mindestens 5 s auf der Strecke); „Für eine vollständige Schadensbewertung fehlen Messwerte“ ersetzt durch eine ehrliche Aussage mit Handlungshinweis.
- Nach dem Dreher kamen „Platz 5“, „Platz 6“, „Platz 7“, „von 7 auf 9“ und „Platz 9, seit dem Start 3 verloren“ in Sekunden → Rennkopf: einzelner Platzverlust wartet 2,5 s, bis die Wertung steht (Folgeverluste ersetzen ihn); Status/Zusammenfassung innerhalb 20 s nach einer Platzmeldung entfällt.
- Tests: RaceMindTests (Reifen heiß/dreckig, Quali-Sprit, Platzmeldungen nach Dreher), PitAndFieldFindingsTests (Garagenausfahrt).
