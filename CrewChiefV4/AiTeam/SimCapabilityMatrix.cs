using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Ein Eintrag der Fähigkeitsmatrix: Ist ein Datenfeld im RRCA-Adapter dieser Simulation gemappt?</summary>
    internal sealed class SimCapability
    {
        public string Sim, Field, Scope, Unit, Evidence;
        public bool Mapped;
        public DataOrigin Origin;
        /// <summary>Wie alt der Wert höchstens sein darf, bevor er als veraltet gilt (0 = Ereignis, gilt bis zum nächsten).</summary>
        public int MaxAgeMs;
        /// <summary>Tatsächlich gemessene native Rate; null = noch nicht gemessen (RRCA 2.0 verlangt Messung statt Annahme).</summary>
        public double? MeasuredHz;
    }

    /// <summary>
    /// RRCA 2.0 (2.2) Fähigkeitsmatrix der sechs Kernsimulationen. Grundlage ist ausschließlich der Quellcode der
    /// Crew-Chief-Adapter (Datei:Zeile im Feld <see cref="SimCapability.Evidence"/>): „gemappt“ heißt, der Adapter schreibt das
    /// Feld in den Spielzustand. Das ist KEIN Nachweis über das vollständige native SDK und keine gemessene Frequenz; die
    /// Raten bleiben leer, bis sie in einer Fahrt gemessen sind. LMU läuft über den rF2-Mapper mit LMU-Puffern.
    /// Fahrdynamik mit 100 Hz (eigener Abtaster) gibt es für AMS2, R3E, LMU, AC und AC EVO; iRacing liefert 60 Hz und keine
    /// Raddrehzahlen (Blockieren/Durchdrehen dort nicht messbar).
    /// </summary>
    internal static class SimCapabilityMatrix
    {
        internal static readonly string[] Sims = { "AMS2", "R3E", "LMU", "AC", "ACE", "IRACING", "ACC" };
        internal static readonly SimCapability[] All =
        {
            new SimCapability { Sim = "AMS2", Field = "weltposition_xyz", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:831", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "AMS2", Field = "ausrichtung", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:1355", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "rad", MaxAgeMs = 200 },
            new SimCapability { Sim = "AMS2", Field = "tempo", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:477", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m/s", MaxAgeMs = 200 },
            new SimCapability { Sim = "AMS2", Field = "rundenfortschritt", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:208", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "AMS2", Field = "fahreridentitaet", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:422", Scope = "Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Text", MaxAgeMs = 5000 },
            new SimCapability { Sim = "AMS2", Field = "klassenposition", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:586", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Platz", MaxAgeMs = 1000 },
            new SimCapability { Sim = "AMS2", Field = "rundenzeiten", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:588", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "AMS2", Field = "sektoren", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:613", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "AMS2", Field = "boxenstatus", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:205", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "AMS2", Field = "kraftstoff", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:1102", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "l", MaxAgeMs = 1000 },
            new SimCapability { Sim = "AMS2", Field = "reifenverschleiss", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:1140", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "%", MaxAgeMs = 2000 },
            new SimCapability { Sim = "AMS2", Field = "reifentemperatur", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:1135", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "AMS2", Field = "bremstemperatur", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:23", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "AMS2", Field = "schaden", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:1086", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "Stufe", MaxAgeMs = 2000 },
            new SimCapability { Sim = "AMS2", Field = "kollision", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:88", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "AMS2", Field = "flaggen", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:712", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 1000 },
            new SimCapability { Sim = "AMS2", Field = "strafen", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:607", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "AMS2", Field = "streckenbegrenzung_rundengueltigkeit", Mapped = true, Evidence = "AMS2/AMS2GameStateMapper.cs:626", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "AMS2", Field = "vorfallpunkte", Mapped = false, Evidence = "AMS2/AMS2GameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Punkte", MaxAgeMs = 0 },
            new SimCapability { Sim = "R3E", Field = "weltposition_xyz", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:288", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "R3E", Field = "ausrichtung", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:1485", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "rad", MaxAgeMs = 200 },
            new SimCapability { Sim = "R3E", Field = "tempo", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:441", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m/s", MaxAgeMs = 200 },
            new SimCapability { Sim = "R3E", Field = "rundenfortschritt", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:391", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "R3E", Field = "fahreridentitaet", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:273", Scope = "Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Text", MaxAgeMs = 5000 },
            new SimCapability { Sim = "R3E", Field = "klassenposition", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:587", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Platz", MaxAgeMs = 1000 },
            new SimCapability { Sim = "R3E", Field = "rundenzeiten", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:614", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "R3E", Field = "sektoren", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:624", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "R3E", Field = "boxenstatus", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:326", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "R3E", Field = "kraftstoff", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:1291", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "l", MaxAgeMs = 1000 },
            new SimCapability { Sim = "R3E", Field = "reifenverschleiss", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:1373", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "%", MaxAgeMs = 2000 },
            new SimCapability { Sim = "R3E", Field = "reifentemperatur", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:1367", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "R3E", Field = "bremstemperatur", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:412", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "R3E", Field = "schaden", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:1212", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "Stufe", MaxAgeMs = 2000 },
            new SimCapability { Sim = "R3E", Field = "kollision", Mapped = false, Evidence = "R3E/R3EGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "R3E", Field = "flaggen", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:668", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 1000 },
            new SimCapability { Sim = "R3E", Field = "strafen", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:1298", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "R3E", Field = "streckenbegrenzung_rundengueltigkeit", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:621", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "R3E", Field = "vorfallpunkte", Mapped = true, Evidence = "R3E/R3EGameStateMapper.cs:768", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Punkte", MaxAgeMs = 0 },
            new SimCapability { Sim = "LMU", Field = "weltposition_xyz", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:860", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "LMU", Field = "ausrichtung", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:862", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "rad", MaxAgeMs = 200 },
            new SimCapability { Sim = "LMU", Field = "tempo", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:858", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m/s", MaxAgeMs = 200 },
            new SimCapability { Sim = "LMU", Field = "rundenfortschritt", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:859", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "LMU", Field = "fahreridentitaet", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:731", Scope = "Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Text", MaxAgeMs = 5000 },
            new SimCapability { Sim = "LMU", Field = "klassenposition", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:527", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Platz", MaxAgeMs = 1000 },
            new SimCapability { Sim = "LMU", Field = "rundenzeiten", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:1522", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "LMU", Field = "sektoren", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:1808", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "LMU", Field = "boxenstatus", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:927", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "LMU", Field = "kraftstoff", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:1959", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "l", MaxAgeMs = 1000 },
            new SimCapability { Sim = "LMU", Field = "reifenverschleiss", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:1272", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "%", MaxAgeMs = 2000 },
            new SimCapability { Sim = "LMU", Field = "reifentemperatur", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:1267", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "LMU", Field = "bremstemperatur", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:730", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "LMU", Field = "schaden", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:1206", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "Stufe", MaxAgeMs = 2000 },
            new SimCapability { Sim = "LMU", Field = "kollision", Mapped = false, Evidence = "RF2/RF2GameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "LMU", Field = "flaggen", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:2074", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 1000 },
            new SimCapability { Sim = "LMU", Field = "strafen", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:786", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "LMU", Field = "streckenbegrenzung_rundengueltigkeit", Mapped = true, Evidence = "RF2/RF2GameStateMapper.cs:2130", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "LMU", Field = "vorfallpunkte", Mapped = false, Evidence = "RF2/RF2GameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Punkte", MaxAgeMs = 0 },
            new SimCapability { Sim = "ACE", Field = "weltposition_xyz", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1513", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "ACE", Field = "ausrichtung", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1946", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "rad", MaxAgeMs = 200 },
            new SimCapability { Sim = "ACE", Field = "tempo", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1709", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m/s", MaxAgeMs = 200 },
            new SimCapability { Sim = "ACE", Field = "rundenfortschritt", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:913", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "ACE", Field = "fahreridentitaet", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1109", Scope = "Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Text", MaxAgeMs = 5000 },
            new SimCapability { Sim = "ACE", Field = "klassenposition", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1297", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Platz", MaxAgeMs = 1000 },
            new SimCapability { Sim = "ACE", Field = "rundenzeiten", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1330", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "ACE", Field = "sektoren", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1321", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "ACE", Field = "boxenstatus", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1041", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "ACE", Field = "kraftstoff", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1690", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "l", MaxAgeMs = 1000 },
            new SimCapability { Sim = "ACE", Field = "reifenverschleiss", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1886", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "%", MaxAgeMs = 2000 },
            new SimCapability { Sim = "ACE", Field = "reifentemperatur", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1826", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "ACE", Field = "bremstemperatur", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:975", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "ACE", Field = "schaden", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1786", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "Stufe", MaxAgeMs = 2000 },
            new SimCapability { Sim = "ACE", Field = "kollision", Mapped = false, Evidence = "ACE/ACEGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "ACE", Field = "flaggen", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1434", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 1000 },
            new SimCapability { Sim = "ACE", Field = "strafen", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:1315", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "ACE", Field = "streckenbegrenzung_rundengueltigkeit", Mapped = true, Evidence = "ACE/ACEGameStateMapper.cs:961", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "ACE", Field = "vorfallpunkte", Mapped = false, Evidence = "ACE/ACEGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Punkte", MaxAgeMs = 0 },
            new SimCapability { Sim = "IRACING", Field = "weltposition_xyz", Mapped = false, Evidence = "iRacing/iRacingGameStateMapper.cs", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "IRACING", Field = "ausrichtung", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:865", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "rad", MaxAgeMs = 200 },
            new SimCapability { Sim = "IRACING", Field = "tempo", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:260", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m/s", MaxAgeMs = 200 },
            new SimCapability { Sim = "IRACING", Field = "rundenfortschritt", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:102", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "IRACING", Field = "fahreridentitaet", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:187", Scope = "Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Text", MaxAgeMs = 5000 },
            new SimCapability { Sim = "IRACING", Field = "klassenposition", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:376", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Platz", MaxAgeMs = 1000 },
            new SimCapability { Sim = "IRACING", Field = "rundenzeiten", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:408", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "IRACING", Field = "sektoren", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:415", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "IRACING", Field = "boxenstatus", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:245", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "IRACING", Field = "kraftstoff", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:1321", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "l", MaxAgeMs = 1000 },
            new SimCapability { Sim = "IRACING", Field = "reifenverschleiss", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:1391", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "%", MaxAgeMs = 2000 },
            new SimCapability { Sim = "IRACING", Field = "reifentemperatur", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:1376", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "IRACING", Field = "bremstemperatur", Mapped = false, Evidence = "iRacing/iRacingGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "IRACING", Field = "schaden", Mapped = false, Evidence = "iRacing/iRacingGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "Stufe", MaxAgeMs = 2000 },
            new SimCapability { Sim = "IRACING", Field = "kollision", Mapped = false, Evidence = "iRacing/iRacingGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "IRACING", Field = "flaggen", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:925", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 1000 },
            new SimCapability { Sim = "IRACING", Field = "strafen", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:445", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "IRACING", Field = "streckenbegrenzung_rundengueltigkeit", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:825", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "IRACING", Field = "vorfallpunkte", Mapped = true, Evidence = "iRacing/iRacingGameStateMapper.cs:238", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Punkte", MaxAgeMs = 0 },
            new SimCapability { Sim = "ACC", Field = "weltposition_xyz", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:888", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "ACC", Field = "ausrichtung", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:1356", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "rad", MaxAgeMs = 200 },
            new SimCapability { Sim = "ACC", Field = "tempo", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:1142", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m/s", MaxAgeMs = 200 },
            new SimCapability { Sim = "ACC", Field = "rundenfortschritt", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:247", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "ACC", Field = "fahreridentitaet", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:417", Scope = "Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Text", MaxAgeMs = 5000 },
            new SimCapability { Sim = "ACC", Field = "klassenposition", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:657", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Platz", MaxAgeMs = 1000 },
            new SimCapability { Sim = "ACC", Field = "rundenzeiten", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:685", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "ACC", Field = "sektoren", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:676", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "ACC", Field = "boxenstatus", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:399", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "ACC", Field = "kraftstoff", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:1064", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "l", MaxAgeMs = 1000 },
            new SimCapability { Sim = "ACC", Field = "reifenverschleiss", Mapped = false, Evidence = "ACC/ACCGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "%", MaxAgeMs = 2000 },
            new SimCapability { Sim = "ACC", Field = "reifentemperatur", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:1242", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "ACC", Field = "bremstemperatur", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:42", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "ACC", Field = "schaden", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:1190", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "Stufe", MaxAgeMs = 2000 },
            new SimCapability { Sim = "ACC", Field = "kollision", Mapped = false, Evidence = "ACC/ACCGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "ACC", Field = "flaggen", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:799", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 1000 },
            new SimCapability { Sim = "ACC", Field = "strafen", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:670", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "ACC", Field = "streckenbegrenzung_rundengueltigkeit", Mapped = true, Evidence = "ACC/ACCGameStateMapper.cs:299", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "ACC", Field = "vorfallpunkte", Mapped = false, Evidence = "ACC/ACCGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Punkte", MaxAgeMs = 0 },
            new SimCapability { Sim = "AC", Field = "weltposition_xyz", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1950", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "AC", Field = "ausrichtung", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1933", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "rad", MaxAgeMs = 200 },
            new SimCapability { Sim = "AC", Field = "tempo", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1697", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m/s", MaxAgeMs = 200 },
            new SimCapability { Sim = "AC", Field = "rundenfortschritt", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1698", Scope = "Spieler+Gegner", Origin = DataOrigin.Measured, Unit = "m", MaxAgeMs = 500 },
            new SimCapability { Sim = "AC", Field = "fahreridentitaet", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:2149", Scope = "Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Text", MaxAgeMs = 5000 },
            new SimCapability { Sim = "AC", Field = "klassenposition", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1017", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Platz", MaxAgeMs = 1000 },
            new SimCapability { Sim = "AC", Field = "rundenzeiten", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1322", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "AC", Field = "sektoren", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1358", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "s", MaxAgeMs = 0 },
            new SimCapability { Sim = "AC", Field = "boxenstatus", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1036", Scope = "Spieler+Gegner", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 1000 },
            new SimCapability { Sim = "AC", Field = "kraftstoff", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1678", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "l", MaxAgeMs = 1000 },
            new SimCapability { Sim = "AC", Field = "reifenverschleiss", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1871", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "%", MaxAgeMs = 2000 },
            new SimCapability { Sim = "AC", Field = "reifentemperatur", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1811", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "AC", Field = "bremstemperatur", Mapped = false, Evidence = "ACS/ACSGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "°C", MaxAgeMs = 2000 },
            new SimCapability { Sim = "AC", Field = "schaden", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1772", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "Stufe", MaxAgeMs = 2000 },
            new SimCapability { Sim = "AC", Field = "kollision", Mapped = false, Evidence = "ACS/ACSGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "AC", Field = "flaggen", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1429", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 1000 },
            new SimCapability { Sim = "AC", Field = "strafen", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1693", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Ereignis", MaxAgeMs = 0 },
            new SimCapability { Sim = "AC", Field = "streckenbegrenzung_rundengueltigkeit", Mapped = true, Evidence = "ACS/ACSGameStateMapper.cs:1381", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "bool", MaxAgeMs = 0 },
            new SimCapability { Sim = "AC", Field = "vorfallpunkte", Mapped = false, Evidence = "ACS/ACSGameStateMapper.cs", Scope = "Spieler", Origin = DataOrigin.SimulatorReportedEvent, Unit = "Punkte", MaxAgeMs = 0 },
            new SimCapability { Sim = "AMS2", Field = "fahrdynamik_100hz", Mapped = true, Evidence = "AiTeam/Dynamics/DynamicsSampler.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "m/s², rad/s", MaxAgeMs = 50 },
            new SimCapability { Sim = "R3E", Field = "fahrdynamik_100hz", Mapped = true, Evidence = "AiTeam/Dynamics/DynamicsSampler.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "m/s², rad/s", MaxAgeMs = 50 },
            new SimCapability { Sim = "LMU", Field = "fahrdynamik_100hz", Mapped = true, Evidence = "AiTeam/Dynamics/DynamicsSampler.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "m/s², rad/s", MaxAgeMs = 50 },
            new SimCapability { Sim = "AC", Field = "fahrdynamik_100hz", Mapped = true, Evidence = "AiTeam/Dynamics/DynamicsSampler.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "m/s², rad/s", MaxAgeMs = 50 },
            new SimCapability { Sim = "ACE", Field = "fahrdynamik_100hz", Mapped = true, Evidence = "AiTeam/Dynamics/DynamicsSampler.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "m/s², rad/s", MaxAgeMs = 50 },
            new SimCapability { Sim = "IRACING", Field = "fahrdynamik_100hz", Mapped = true, Evidence = "AiTeam/Dynamics/DynamicsSampler.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "m/s², rad/s", MaxAgeMs = 50 },
            new SimCapability { Sim = "ACC", Field = "fahrdynamik_100hz", Mapped = false, Evidence = "AiTeam/Dynamics/DynamicsSampler.cs", Scope = "Spieler", Origin = DataOrigin.Measured, Unit = "m/s², rad/s", MaxAgeMs = 50 },
        };

        /// <summary>Simulationskennung aus dem Spiel; null, wenn keine der sechs Kernsimulationen.</summary>
        internal static string SimOf(GameEnum game)
        {
            switch (game)
            {
                case GameEnum.AMS2: return "AMS2";
                case GameEnum.RACE_ROOM: return "R3E";
                case GameEnum.LMU: return "LMU";
                case GameEnum.ASSETTO_32BIT:
                case GameEnum.ASSETTO_64BIT:
                case GameEnum.ASSETTO_128CARS: return "AC";
                case GameEnum.ASSETTO_EVO: return "ACE";
                case GameEnum.IRACING: return "IRACING";
                case GameEnum.ACC: return "ACC";
                default: return null;
            }
        }

        internal static IEnumerable<SimCapability> For(string sim) => All.Where(c => c.Sim == sim);

        internal static SimCapability Get(string sim, string field) => All.FirstOrDefault(c => c.Sim == sim && c.Field == field);

        /// <summary>Maschinenlesbare Matrix (für Werkzeug und Export).</summary>
        internal static JObject Describe(string sim = null)
        {
            var o = new JObject { ["grundlage"] = "Adapter-Quellcode (Datei:Zeile); gemappt heißt nicht, dass das native SDK vollständig geprüft ist. Frequenzen null = nicht gemessen." };
            foreach (var s in sim == null ? Sims : new[] { sim })
                o[s] = new JArray(For(s).Select(c => new JObject {
                    ["feld"] = c.Field, ["gemappt"] = c.Mapped, ["herkunft"] = c.Origin.ToString(), ["bezug"] = c.Scope, ["einheit"] = c.Unit,
                    ["max_alter_ms"] = c.MaxAgeMs, ["gemessen_hz"] = c.MeasuredHz.HasValue ? (JToken)c.MeasuredHz.Value : null, ["beleg"] = c.Evidence }));
            return o;
        }
    }
}
