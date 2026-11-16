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
        internal bool Green;
        public int Position;
        public float FuelAtEnd = -1;
        public float FuelUsed = -1;          // -1 = unbekannt (z. B. nachgetankt)
        public float[] TyreWear;             // FL, FR, RL, RR in %, null = vom Spiel nicht geliefert
        public float[] PeakTyreTemps;        // FL, FR, RL, RR in °C, null = nicht geliefert
        public float[] SectorTimes;
        public string FrontIdentity, BehindIdentity;
        public float GapAhead = -1;
        public RaceGapKind GapAheadKind = RaceGapKind.Unknown, GapBehindKind = RaceGapKind.Unknown;
        public float GapBehind = -1;
        public bool InPitLap;
        public DateTime RecordedUtc;
    }

    public class RaceEventRecord
    {
        public string Topic,SubjectKey,Source;
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
        private float previousFuel = -1;
        private int fuelStartLap = -1;
        private float segmentStartFuel = -1;
        private float segmentMetres;
        private DateTime segmentLastStamp;
        private bool pitDuringLap;
        private bool invalidDuringLap;
        private bool greenDuringLap;
        private SessionType lastSessionType = SessionType.Unavailable;
        private int lastRecordedLap = -1;

        public DateTime LastTickUtc { get; private set; } = DateTime.MinValue;
        public int SessionId { get; private set; } = int.MinValue;
        public int RecordedInvalidLaps { get; private set; }

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
                RecordedInvalidLaps = 0;
                fuelAtLapStart = -1;
                previousFuel = -1;
                fuelStartLap = -1;
                segmentStartFuel = -1;
                segmentMetres = 0;
                pitDuringLap = false;
                invalidDuringLap = false;
                greenDuringLap = false;
            }
        }

        /// <summary>Ein Telemetrie-Tick. Liefert true, wenn gerade eine Runde abgeschlossen wurde.</summary>
        public bool Update(GameStateData previous, GameStateData current, DateTime nowUtc, RaceGapMeasurement front = null, RaceGapMeasurement rear = null)
        {
            if (current == null)
            {
                return false;
            }
            bool lapCompleted = false;
            lock (sync)
            {
                var priorSample = LastTickUtc;
                LastTickUtc = current.SourceTelemetryUtc == DateTime.MinValue ? nowUtc : current.SourceTelemetryUtc;
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
                    RecordedInvalidLaps = 0;
                    // erst ab der ersten Überfahrt messen, sonst zählen Einführungsrunde und Startaufstellung mit
                    fuelAtLapStart = -1;
                    previousFuel = -1;
                    fuelStartLap = -1;
                    pitDuringLap = false;
                    invalidDuringLap = false;
                    greenDuringLap = false;
                }
                bool inPit = current.PitData.InPitlane || current.PitData.IsInGarage;
                bool green = current.SessionData.SessionPhase == SessionPhase.Green && !current.FlagData.isLocalYellow &&
                    !inPit && !current.PitData.OnInLap && !current.PitData.OnOutLap &&
                    LastTickUtc <= nowUtc && nowUtc-LastTickUtc <= TimeSpan.FromSeconds(1);
                if (!green || priorSample != DateTime.MinValue &&
                    (LastTickUtc < priorSample || LastTickUtc-priorSample > TimeSpan.FromSeconds(2))) greenDuringLap = false;
                if (inPit)
                {
                    pitDuringLap = true;
                }
                float fuel = current.FuelData.FuelLeft;
                bool fuelAvailable = current.FuelData.FuelUseActive && fuel >= 0 &&
                    !float.IsInfinity(fuel) && fuel <= 10000;
                // A rise can mark a refuel or reset even if the final tank level is below
                // the start level. This interval is no longer a measured full lap.
                if (!fuelAvailable || inPit || (previousFuel >= 0 && fuel > previousFuel) ||
                    (previous != null && current.SessionData.CompletedLaps < previous.SessionData.CompletedLaps))
                {
                    fuelAtLapStart = -1;
                }
                ObserveFuelSegment(newSession ? null : previous, current, nowUtc, fuelAvailable && !inPit);
                previousFuel = fuelAvailable ? fuel : -1;
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
                        Green = greenDuringLap,
                        Position = current.SessionData.ClassPosition,
                        GapAhead = front == null ? current.SessionData.TimeDeltaFront : front.IsRaceGap ? front.Seconds : -1,
                        GapBehind = rear == null ? current.SessionData.TimeDeltaBehind : rear.IsRaceGap ? rear.Seconds : -1,
                        GapAheadKind = front == null ? RaceGapKind.Unknown : front.Kind,
                        GapBehindKind = rear == null ? RaceGapKind.Unknown : rear.Kind,
                        InPitLap = pitDuringLap,
                        RecordedUtc = nowUtc,
                    };
                    var frontCar = RaceDataTools.Neighbour(current, true);
                    var rearCar = RaceDataTools.Neighbour(current, false);
                    if (frontCar != null) record.FrontIdentity = RaceReader.Identity(current.OpponentData.First(p => ReferenceEquals(p.Value, frontCar)).Key, frontCar);
                    if (rearCar != null) record.BehindIdentity = RaceReader.Identity(current.OpponentData.First(p => ReferenceEquals(p.Value, rearCar)).Key, rearCar);
                    var sectors = current.SessionData.PlayerLapData.LastOrDefault(l => l.LapNumber == record.LapNumber &&
                        l.IsValid && l.LapTime > 0 && Math.Abs(l.LapTime - record.LapTime) < .1f && !l.hasMissingSectors);
                    if (!current.SessionData.SectorTimingEstimated && sectors != null &&
                        sectors.SectorTimes.Length == 3 && sectors.SectorTimes.All(v => v > 0 && v < 3600))
                        record.SectorTimes = (float[])sectors.SectorTimes.Clone();
                    if (fuelAvailable)
                    {
                        record.FuelAtEnd = fuel;
                        if (fuelAtLapStart > fuel && fuelStartLap == record.LapNumber - 1 && !pitDuringLap)
                        {
                            record.FuelUsed = fuelAtLapStart - fuel;
                        }
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
                    if (!record.Valid && record.LapTime > 0) RecordedInvalidLaps++;
                    laps.Add(record);
                    if (laps.Count > MaxLaps)
                    {
                        laps.RemoveAt(0);
                    }
                    pitDuringLap = inPit;
                    invalidDuringLap = false;
                }
                // A start-line crossing may START the first timed lap without completing
                // a counted lap. End the outlap/garage accumulation at that boundary too.
                if (current.SessionData.IsNewLap && (previous == null || !previous.SessionData.IsNewLap ||
                    current.SessionData.CompletedLaps != previous.SessionData.CompletedLaps))
                {
                    pitDuringLap = inPit;
                    invalidDuringLap = false;
                    greenDuringLap = green;
                    fuelAtLapStart = fuelAvailable && !inPit ? fuel : -1;
                    fuelStartLap = current.SessionData.CompletedLaps;
                }
                if (!current.SessionData.CurrentLapIsValid) invalidDuringLap = true;
            }
            return lapCompleted;
        }

        // Hochrechnung wie ein Renningenieur: verbrauchter Sprit je gefahrenem Meter, mal Rundenlänge.
        // Ob die Runde für die Zeitnahme gültig ist (Outlap, Tracklimits), spielt für den Verbrauch keine Rolle.
        // Unterbrochen wird nur durch Boxengasse, Nachtanken, Reset/Teleport, Sitzungs- oder Autowechsel und alte Daten.
        internal const float ProvisionalMinimumMetres = 300f;
        internal const float ProvisionalMinimumLitres = 0.05f;
        private const float FuelNoiseLitres = 0.05f;

        private void ObserveFuelSegment(GameStateData previous, GameStateData current, DateTime now, bool onTrack)
        {
            var session = current.SessionData;
            bool movingSession = session.SessionPhase == SessionPhase.Green || session.SessionPhase == SessionPhase.FullCourseYellow;
            DateTime stamp = current.SourceTelemetryUtc == DateTime.MinValue ? current.Now : current.SourceTelemetryUtc;
            segmentLastStamp = stamp;
            bool fresh = stamp != DateTime.MinValue && now >= stamp && now - stamp <= TimeSpan.FromSeconds(1);
            float trackLength = session.TrackDefinition?.trackLength ?? 0;
            float distance = current.PositionAndMotionData.DistanceRoundTrack;
            float speed = current.PositionAndMotionData.CarSpeed;
            bool good = onTrack && movingSession && fresh && trackLength > 0 && !float.IsInfinity(trackLength) &&
                distance >= 0 && distance < trackLength && speed >= 0 && speed <= 150;
            if (!good)
            {
                segmentStartFuel = -1;
                segmentMetres = 0;
                return;
            }

            float fuel = current.FuelData.FuelLeft;
            if (previous != null && segmentStartFuel >= 0 && previousFuel >= 0 && fuel <= previousFuel + FuelNoiseLitres &&
                string.Equals(previous.carName, current.carName, StringComparison.Ordinal) &&
                string.Equals(previous.SessionData.TrackDefinition?.name, session.TrackDefinition?.name, StringComparison.Ordinal))
            {
                DateTime previousStamp = previous.SourceTelemetryUtc == DateTime.MinValue ? previous.Now : previous.SourceTelemetryUtc;
                double seconds = (stamp - previousStamp).TotalSeconds;
                float delta = distance - previous.PositionAndMotionData.DistanceRoundTrack;
                if (delta < -trackLength * 0.5f) delta += trackLength; // Start/Ziel überquert: Segment läuft weiter
                if (seconds == 0 && delta == 0 && previousFuel == fuel) return;
                float maxSpeed = Math.Max(speed, previous.PositionAndMotionData.CarSpeed);
                if (seconds > 0 && seconds <= 1 && delta >= 0 && delta <= maxSpeed * seconds * 1.5 + 5)
                {
                    segmentMetres += delta;
                    return;
                }
                if (seconds >= 0 && seconds <= 1 && Math.Abs(delta) <= 2) return; // Stillstand oder leichtes Zurückrollen
            }
            segmentStartFuel = fuel;
            segmentMetres = 0;
        }

        internal float ProvisionalFuelPerLap(GameStateData state)
        {
            float metres;
            return ProvisionalFuelPerLap(state, out metres);
        }

        /// <summary>Geschätzter Verbrauch pro Runde seit der letzten Unterbrechung; <paramref name="metres"/> ist die zugrunde liegende Strecke.</summary>
        internal float ProvisionalFuelPerLap(GameStateData state, out float metres)
        {
            lock (sync)
            {
                metres = 0;
                if (!state.FuelData.FuelUseActive || state.PitData.InPitlane || state.PitData.IsInGarage) return -1;
                DateTime stamp = state.SourceTelemetryUtc == DateTime.MinValue ? state.Now : state.SourceTelemetryUtc;
                if (state.SessionData.SessionId != SessionId || state.SessionData.SessionType != lastSessionType ||
                    stamp != segmentLastStamp || segmentStartFuel < 0 || segmentMetres <= 0)
                    return -1;

                float trackLength = state.SessionData.TrackDefinition?.trackLength ?? 0;
                float used = segmentStartFuel - state.FuelData.FuelLeft;
                metres = segmentMetres;
                if (segmentMetres < Math.Max(ProvisionalMinimumMetres, trackLength * 0.05f) || !(used >= ProvisionalMinimumLitres)) return -1;
                return used / segmentMetres * trackLength;
            }
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

        public void AddObservation(string text,DateTime nowUtc,string topic=null,string subjectKey=null,string source=null)
        {
            if(string.IsNullOrWhiteSpace(text))return;
            lock(sync){AddEventLocked(nowUtc,text);var item=events[events.Count-1];item.Topic=topic;item.SubjectKey=subjectKey;item.Source=source;}
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

        public bool HasRadioSince(TimeSpan maxAge, DateTime nowUtc)
        {
            lock (sync)
            {
                for (int i = radio.Count - 1; i >= 0; i--)
                {
                    var timestamp = radio[i].TimeUtc;
                    if (timestamp <= nowUtc && nowUtc - timestamp <= maxAge) return true;
                }
                return false;
            }
        }

        public List<RaceEventRecord> GetRadio(TimeSpan maxAge, DateTime nowUtc)
        {
            lock (sync)
            {
                return radio.Where(e => nowUtc - e.TimeUtc <= maxAge).ToList();
            }
        }

        /// <summary>Durchschnitt aus vollständigen Tankmessungen, unabhängig von der Zeitnahmegültigkeit.</summary>
        public float AverageFuelPerLap(int lastLaps = 5)
        {
            return AverageFuelPerLap(out _, lastLaps);
        }

        internal float AverageFuelPerLap(out int sampleCount, int lastLaps = 5)
        {
            lock (sync)
            {
                float total = 0;
                sampleCount = 0;
                for (int i = laps.Count - 1; i >= 0 && sampleCount < lastLaps; i--)
                {
                    var lap = laps[i];
                    if (!lap.InPitLap && lap.FuelUsed > 0 && !float.IsInfinity(lap.FuelUsed))
                    {
                        total += lap.FuelUsed;
                        sampleCount++;
                    }
                }
                return sampleCount == 0 ? -1 : total / sampleCount;
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
                    var before=window[i-1];var after=window[i];
                    if(before.InPitLap || after.InPitLap || !before.Valid || !after.Valid || after.LapNumber!=before.LapNumber+1 ||
                        before.TyreWear.Length!=4 || after.TyreWear.Length!=4 ||
                        Enumerable.Range(0,4).Any(w=>float.IsNaN(before.TyreWear[w]) || float.IsInfinity(before.TyreWear[w]) ||
                            float.IsNaN(after.TyreWear[w]) || float.IsInfinity(after.TyreWear[w]) || before.TyreWear[w]<0 ||
                            after.TyreWear[w]>100 || after.TyreWear[w]<before.TyreWear[w]))
                    {
                        continue; // Missing laps, pit work and fresh tyres cannot establish a comparable wear rate.
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
