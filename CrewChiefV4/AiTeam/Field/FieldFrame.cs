using System;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>Art der Session, unabhängig von der Sim.</summary>
    internal enum FieldSession { Unknown, Practice, Qualify, Warmup, Race }

    /// <summary>Phase der Session, unabhängig von der Sim. FullCourseYellow = Neutralisierung (FCY/SC), Stopped = Rennunterbrechung.</summary>
    internal enum FieldPhase { Unknown, Garage, Gridwalk, Formation, Countdown, Green, Checkered, FullCourseYellow, Stopped }

    /// <summary>
    /// Klassen für Sims, die je Fahrer nur den Klassennamen liefern (LMU, AMS2). Gleicher Name = gleiche Klasse; den Klassenplatz
    /// zählen wir aus dem Gesamtplatz. Ohne Klassennamen bleibt alles wie bisher (eine Klasse, Klassenplatz = Gesamtplatz).
    /// </summary>
    internal static class FieldClasses
    {
        /// <summary>Feste Nummer für einen Klassennamen (Groß-/Kleinschreibung egal); -1 ohne Namen.</summary>
        internal static int IdOf(string className)
        {
            string n = className?.Trim();
            if (string.IsNullOrEmpty(n)) return -1;
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in n.ToUpperInvariant()) { h ^= ch; h *= 16777619; }
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>Klassenplatz = 1 + Wagen derselben Klasse mit besserem Gesamtplatz. Wagen ohne Klasse behalten den Gesamtplatz.</summary>
        internal static void RankWithinClass(FieldCarFrame[] cars)
        {
            if (cars == null) return;
            foreach (var c in cars)
            {
                if (c == null) continue;
                if (c.ClassId < 0 || c.Place <= 0) { c.PlaceClass = c.Place; continue; }
                int ahead = 0;
                foreach (var o in cars)
                    if (o != null && !ReferenceEquals(o, c) && o.ClassId == c.ClassId && o.Place > 0 && o.Place < c.Place) ahead++;
                c.PlaceClass = ahead + 1;
            }
        }
    }

    /// <summary>
    /// Ein Fahrer in einem Rohbild direkt aus dem Speicher der Sim (nicht aus Crew Chiefs Spielzustand).
    /// Zeiten in Sekunden, Strecken in Metern, Tempo in m/s. Unbekannt = NaN bzw. -1.
    /// </summary>
    internal sealed class FieldCarFrame
    {
        public int Slot = -1;
        public string Name;
        public int ClassId = -1;
        public int Place, PlaceClass;
        public float LapDistance = float.NaN;
        public int CompletedLaps;
        public float Speed = float.NaN;
        public float X = float.NaN, Y = float.NaN, Z = float.NaN;
        public bool InPit;
        /// <summary>iRacing `CarIdxTrackSurface` = neben der Strecke (Rob 28.11.: Abflüge sofort sehen, nicht erst am Tempo).</summary>
        public bool OffTrack;
        public bool CurrentLapValid = true;
        public float LapTimeCurrent = float.NaN;
        /// <summary>Kumuliert wie in der Sim: S1, S1+S2, Rundenzeit. Unbekannt = NaN.</summary>
        public float BestS1 = float.NaN, BestS12 = float.NaN, BestLap = float.NaN;
        public float LastLap = float.NaN;
        /// <summary>Beste Einzelsektoren, wenn die Sim sie getrennt meldet (AMS2); sonst NaN und aus den kumulierten Werten gerechnet.</summary>
        public float SplitBest1 = float.NaN, SplitBest2 = float.NaN, SplitBest3 = float.NaN;
        /// <summary>0 = fährt, 1 = im Ziel, 2 = DNF, 3 = DQ, 4 = DNS, -1 = unbekannt.</summary>
        public int FinishStatus = -1;
        public int PenaltyType = -1, PenaltyReason = -1;
        /// <summary>Offene Strafen in Sekunden (RaceRoom: Slow-down = Zeit, die noch zurückzugeben ist). -1 = keine.</summary>
        public float SlowDown = -1, DriveThrough = -1, StopAndGo = -1, TimeDeduction = -1;
        public bool IsPlayer;
        /// <summary>Streckenmeter/Position zwischen zwei Wertungsbildern aus Tempo und Zeit fortgeschrieben.</summary>
        public bool Extrapolated;
        /// <summary>IR4: iRacings eigene Zeitschätzung bis zur aktuellen Streckenstelle (`CarIdxEstTime`, Grundlage der Relativanzeige).</summary>
        public float EstTime = float.NaN;
        /// <summary>IR5: verbrauchte Fast Repairs (`CarIdxFastRepairsUsed`), -1 = unbekannt.</summary>
        public int FastRepairsUsed = -1;

        public FieldCarFrame Copy() => (FieldCarFrame)MemberwiseClone();
    }

    /// <summary>Eine kommende Session des Events (iRacing meldet den ganzen Ablauf). Minuten bzw. Runden, -1 = unbekannt.</summary>
    internal sealed class FieldUpcoming
    {
        public FieldSession Session;
        public int Minutes = -1, Laps = -1;
    }

    /// <summary>Ein vollständiges Rohbild des Feldes, so wie die Sim es gerade meldet.</summary>
    internal sealed class FieldFrame
    {
        public DateTime Utc;
        public string Sim;
        public string Track, Layout;
        public float TrackLength = float.NaN;
        public FieldSession Session;
        public FieldPhase Phase;
        /// <summary>Rohwert der Sim für Session-Art/Iteration (nur Diagnose).</summary>
        public int RawSession = -1, SessionIteration = -1;
        public bool TimedSession, LapsSession;
        public float TimeRemaining = float.NaN, TimeDuration = float.NaN;
        public int SessionLaps = -1;
        /// <summary>Renndauer der Rennen 1–3 im Event (Minuten bzw. Runden), -1 = nicht vorhanden.</summary>
        public int[] RaceMinutes = { -1, -1, -1 }, RaceLaps = { -1, -1, -1 };
        /// <summary>Kommende Sessions nach der laufenden, wenn die Sim sie meldet; leer = unbekannt.</summary>
        public FieldUpcoming[] Upcoming = new FieldUpcoming[0];
        public bool InMenus, Paused, InReplay, PlayerInGarage;
        /// <summary>Luft- und Streckentemperatur in °C, NaN = von der Sim nicht gemeldet (RaceRoom).</summary>
        public float AirTempC = float.NaN, TrackTempC = float.NaN;
        public double SimTime = double.NaN;
        public int NumCars;
        /// <summary>Plätze, die der Fahrer laut Sim zurückgeben muss (RaceRoom), -1 = unbekannt.</summary>
        public int PositionsToGiveBack = -1;
        /// <summary>Layout passte zur eigenen Struktur (Kopf der Sim gegen eigene Rechnung geprüft).</summary>
        public bool LayoutVerified;
        public FieldCarFrame[] Cars = new FieldCarFrame[0];
        public int PlayerIndex = -1;
        /// <summary>Eigenes Automodell als Schlüssel, wenn Crew Chief keinen Autonamen liefert (RaceRoom: „r3e-model-1234“), sonst null.</summary>
        public string PlayerCarModel;

        public FieldCarFrame Player => PlayerIndex >= 0 && PlayerIndex < Cars.Length ? Cars[PlayerIndex] : null;
    }
}
