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
