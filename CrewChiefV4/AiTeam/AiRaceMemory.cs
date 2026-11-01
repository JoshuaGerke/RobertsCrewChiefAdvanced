using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    public class LapRecord
    {
        public int LapNumber;
        public float LapTime = -1;
        public bool Valid;
        public int Position;
        public float FuelAtEnd = -1;
        public float FuelUsed = -1;          // -1 = unbekannt (z. B. nachgetankt)
        public float[] TyreWear;             // FL, FR, RL, RR in %, null = vom Spiel nicht geliefert
        public float[] PeakTyreTemps;        // FL, FR, RL, RR in °C, null = nicht geliefert
        public float GapAhead = -1;
        public RaceGapKind GapAheadKind = RaceGapKind.Unknown, GapBehindKind = RaceGapKind.Unknown;
        public float GapBehind = -1;
        public bool InPitLap;
        public DateTime RecordedUtc;
    }

    public class RaceEventRecord
    {
        public DateTime TimeUtc;
        public string Text;
    }

    /// <summary>
    /// Gemeinsames Renngedächtnis für Ben und Mik. Wird pro Telemetrie-Tick von
    /// <see cref="CrewChiefV4.Events.AiTeamEvent"/> gefüttert (billig, ohne KI) und beim
    /// Sessionwechsel geleert. Sara nutzt es nicht – sie arbeitet nur mit Live-Daten.
    /// </summary>
    public class AiRaceMemory
    {
        public const int MaxLaps = 60;
        public const int MaxEvents = 40;
        public const int MaxRadio = 20;

        private readonly object sync = new object();
        private readonly List<LapRecord> laps = new List<LapRecord>();
        private readonly List<RaceEventRecord> events = new List<RaceEventRecord>();
        private readonly List<RaceEventRecord> radio = new List<RaceEventRecord>();
        private float fuelAtLapStart = -1;
        private bool pitDuringLap;
        private bool invalidDuringLap;
        private SessionType lastSessionType = SessionType.Unavailable;
        private int lastRecordedLap = -1;

        public DateTime LastTickUtc { get; private set; } = DateTime.MinValue;
        public int SessionId { get; private set; } = int.MinValue;

        public void Clear()
        {
            lock (sync)
            {
                laps.Clear();
                events.Clear();
                radio.Clear();
                SessionId = int.MinValue;
                LastTickUtc = DateTime.MinValue;
                lastRecordedLap = -1;
                fuelAtLapStart = -1;
                pitDuringLap = false;
                invalidDuringLap = false;
            }
        }

        /// <summary>Ein Telemetrie-Tick. Liefert true, wenn gerade eine Runde abgeschlossen wurde.</summary>
        public bool Update(GameStateData previous, GameStateData current, DateTime nowUtc, RaceGapMeasurement front = null, RaceGapMeasurement rear = null)
        {
            if (current == null)
            {
                return false;
            }
            LastTickUtc = nowUtc;
            bool lapCompleted = false;
            lock (sync)
            {
                bool newSession = current.SessionData.SessionId != SessionId || current.SessionData.SessionType != lastSessionType ||
                    (current.SessionData.IsNewSession && (previous == null || !previous.SessionData.IsNewSession));
                if (newSession)
                {
                    SessionId = current.SessionData.SessionId;
                    lastSessionType = current.SessionData.SessionType;
                    laps.Clear();
                    events.Clear();
                    radio.Clear();
                    lastRecordedLap = -1;
                    // erst ab der ersten Überfahrt messen, sonst zählen Einführungsrunde und Startaufstellung mit
                    fuelAtLapStart = -1;
                    pitDuringLap = false;
                invalidDuringLap = false;
                }
                if (current.PitData.InPitlane)
                {
                    pitDuringLap = true;
                }
                if (previous != null && !newSession)
                {
                    TrackEvents(previous, current, nowUtc);
                }
                if (current.SessionData.IsNewLap && current.SessionData.CompletedLaps > lastRecordedLap && current.SessionData.CompletedLaps > 0)
                {
                    lapCompleted = true;
                    lastRecordedLap = current.SessionData.CompletedLaps;
                    var record = new LapRecord
                    {
                        LapNumber = current.SessionData.CompletedLaps,
                        LapTime = current.SessionData.LapTimePrevious,
                        Valid = current.SessionData.PreviousLapWasValid && !invalidDuringLap,
                        Position = current.SessionData.ClassPosition,
                        GapAhead = front == null ? current.SessionData.TimeDeltaFront : front.IsRaceGap ? front.Seconds : -1,
                        GapBehind = rear == null ? current.SessionData.TimeDeltaBehind : rear.IsRaceGap ? rear.Seconds : -1,
                        GapAheadKind = front == null ? RaceGapKind.Unknown : front.Kind,
                        GapBehindKind = rear == null ? RaceGapKind.Unknown : rear.Kind,
                        InPitLap = pitDuringLap,
                        RecordedUtc = nowUtc,
                    };
                    if (current.FuelData.FuelUseActive && current.FuelData.FuelLeft > 0)
                    {
                        record.FuelAtEnd = current.FuelData.FuelLeft;
                        if (fuelAtLapStart > 0 && fuelAtLapStart >= current.FuelData.FuelLeft && !pitDuringLap)
                        {
                            record.FuelUsed = fuelAtLapStart - current.FuelData.FuelLeft;
                        }
                        fuelAtLapStart = current.FuelData.FuelLeft;
                    }
                    var t = current.TyreData;
                    if (t.TyreWearActive)
                    {
                        record.TyreWear = new[] { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear };
                    }
                    float[] peaks = { t.PeakFrontLeftTemperatureForLap, t.PeakFrontRightTemperatureForLap, t.PeakRearLeftTemperatureForLap, t.PeakRearRightTemperatureForLap };
                    if (peaks.Any(p => p > 1))
                    {
                        record.PeakTyreTemps = peaks;
                    }
                    laps.Add(record);
                    if (laps.Count > MaxLaps)
                    {
                        laps.RemoveAt(0);
                    }
                    pitDuringLap = current.PitData.InPitlane;
                    invalidDuringLap = false;
                }
                if (!current.SessionData.CurrentLapIsValid) invalidDuringLap = true;
            }
            return lapCompleted;
        }

        private void TrackEvents(GameStateData previous, GameStateData current, DateTime nowUtc)
        {
            var p = previous.PitData;
            var c = current.PitData;
            if (!p.CarInFrontIsPitting && c.CarInFrontIsPitting)
            {
                AddEventLocked(nowUtc, "Das Auto direkt vor uns fährt an die Box.");
            }
            if (!p.CarBehindIsPitting && c.CarBehindIsPitting)
            {
                AddEventLocked(nowUtc, "Das Auto direkt hinter uns fährt an die Box.");
            }
            if (!p.LeaderIsPitting && c.LeaderIsPitting)
            {
                AddEventLocked(nowUtc, "Der Führende fährt an die Box.");
            }
            if (previous.SessionData.ClassPosition > 0 && current.SessionData.ClassPosition > 0 &&
                previous.SessionData.ClassPosition != current.SessionData.ClassPosition)
            {
                AddEventLocked(nowUtc, "Position in der Klasse: P" + previous.SessionData.ClassPosition + " → P" + current.SessionData.ClassPosition + ".");
            }
            if (previous.SessionData.Flag != current.SessionData.Flag)
            {
                AddEventLocked(nowUtc, "Flagge: " + current.SessionData.Flag + ".");
            }
            if (!p.InPitlane && c.InPitlane)
            {
                AddEventLocked(nowUtc, "Wir sind in der Boxengasse.");
            }
            if (previous.CarDamageData.LastImpactTime != current.CarDamageData.LastImpactTime && current.CarDamageData.LastImpactTime > 0)
            {
                AddEventLocked(nowUtc, "Es gab einen Kontakt/Einschlag.");
            }
        }

        private void AddEventLocked(DateTime nowUtc, string text)
        {
            events.Add(new RaceEventRecord { TimeUtc = nowUtc, Text = text });
            if (events.Count > MaxEvents)
            {
                events.RemoveAt(0);
            }
        }

        public void AddRadio(TeamMember member, string text, DateTime nowUtc)
        {
            lock (sync)
            {
                radio.Add(new RaceEventRecord { TimeUtc = nowUtc, Text = TeamRouter.DisplayName(member) + ": " + text });
                if (radio.Count > MaxRadio)
                {
                    radio.RemoveAt(0);
                }
            }
        }

        public List<LapRecord> GetLaps(int max = MaxLaps)
        {
            lock (sync)
            {
                return laps.Skip(Math.Max(0, laps.Count - max)).ToList();
            }
        }

        public List<RaceEventRecord> GetEvents(TimeSpan maxAge, DateTime nowUtc)
        {
            lock (sync)
            {
                return events.Where(e => nowUtc - e.TimeUtc <= maxAge).ToList();
            }
        }

        public List<RaceEventRecord> GetRadio(TimeSpan maxAge, DateTime nowUtc)
        {
            lock (sync)
            {
                return radio.Where(e => nowUtc - e.TimeUtc <= maxAge).ToList();
            }
        }

        /// <summary>Durchschnittsverbrauch der letzten gültigen Runden ohne Boxenstopp, -1 wenn unbekannt.</summary>
        public float AverageFuelPerLap(int lastLaps = 5)
        {
            lock (sync)
            {
                var usable = laps.Where(l => l.Valid && l.FuelUsed > 0 && !float.IsNaN(l.FuelUsed) && !float.IsInfinity(l.FuelUsed) && !l.InPitLap).Reverse().Take(lastLaps).ToList();
                return usable.Count == 0 ? -1 : usable.Average(l => l.FuelUsed);
            }
        }

        /// <summary>Reifenverschleiß pro Runde (Mittel der letzten Runden), null wenn das Spiel keinen Verschleiß liefert.</summary>
        public float[] AverageWearPerLap(int lastLaps = 5)
        {
            lock (sync)
            {
                var withWear = laps.Where(l => l.TyreWear != null).ToList();
                if (withWear.Count < 2)
                {
                    return null;
                }
                var window = withWear.Skip(Math.Max(0, withWear.Count - (lastLaps + 1))).ToList();
                var result = new float[4];
                int n = 0;
                for (int i = 1; i < window.Count; i++)
                {
                    if (window[i].InPitLap)
                    {
                        continue; // Reifenwechsel verfälscht den Wert
                    }
                    for (int w = 0; w < 4; w++)
                    {
                        result[w] += window[i].TyreWear[w] - window[i - 1].TyreWear[w];
                    }
                    n++;
                }
                if (n == 0)
                {
                    return null;
                }
                for (int w = 0; w < 4; w++)
                {
                    result[w] /= n;
                }
                return result;
            }
        }
    }
}
