using System;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>Art der Session, unabhängig von der Sim.</summary>
    internal enum FieldSession { Unknown, Practice, Qualify, Warmup, Race }

    /// <summary>Phase der Session, unabhängig von der Sim.</summary>
    internal enum FieldPhase { Unknown, Garage, Gridwalk, Formation, Countdown, Green, Checkered }

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
        public bool CurrentLapValid = true;
        public float LapTimeCurrent = float.NaN;
        /// <summary>Kumuliert wie in der Sim: S1, S1+S2, Rundenzeit. Unbekannt = NaN.</summary>
        public float BestS1 = float.NaN, BestS12 = float.NaN, BestLap = float.NaN;
        public float LastLap = float.NaN;
        /// <summary>0 = fährt, 1 = im Ziel, 2 = DNF, 3 = DQ, 4 = DNS, -1 = unbekannt.</summary>
        public int FinishStatus = -1;
        public int PenaltyType = -1, PenaltyReason = -1;
        /// <summary>Offene Strafen in Sekunden (RaceRoom: Slow-down = Zeit, die noch zurückzugeben ist). -1 = keine.</summary>
        public float SlowDown = -1, DriveThrough = -1, StopAndGo = -1, TimeDeduction = -1;
        public bool IsPlayer;

        public FieldCarFrame Copy() => (FieldCarFrame)MemberwiseClone();
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
        public bool InMenus, Paused, InReplay, PlayerInGarage;
        public double SimTime = double.NaN;
        public int NumCars;
        /// <summary>Plätze, die der Fahrer laut Sim zurückgeben muss (RaceRoom), -1 = unbekannt.</summary>
        public int PositionsToGiveBack = -1;
        /// <summary>Layout passte zur eigenen Struktur (Kopf der Sim gegen eigene Rechnung geprüft).</summary>
        public bool LayoutVerified;
        public FieldCarFrame[] Cars = new FieldCarFrame[0];
        public int PlayerIndex = -1;

        public FieldCarFrame Player => PlayerIndex >= 0 && PlayerIndex < Cars.Length ? Cars[PlayerIndex] : null;
    }
}
