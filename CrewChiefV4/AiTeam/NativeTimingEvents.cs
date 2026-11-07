using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    public enum NativeTimingRelation { ClassAhead, ClassBehind, BehindOnTrack }
    public enum NativeTimingTrend { Close, Increasing, Decreasing, Steady }

    /// <summary>Value snapshot of an existing Timings decision, before a DELAYED sound callback is queued.
    /// NativeSeconds explains that decision; it is never evidence of a fresh official timing measurement.</summary>
    public sealed class NativeTimingFact
    {
        public const string Source = "Timings.native_gap_decision";
        public readonly string Key, Name, CarNumber, PlayerClass, OpponentClass, Track;
        public readonly int Session, PlayerLap, OpponentLap, SignedLapDelta, PlayerPosition, OpponentPosition, CostId;
        public readonly SessionType Type;
        public readonly DateTime Utc, SourceUtc, SessionStart;
        public readonly NativeTimingRelation Relation;
        public readonly NativeTimingTrend Trend;
        public readonly float NativeSeconds;
        public readonly bool PlayerInPits, OpponentInPits, OpponentEnteringPits, PlayerOnOutLap, OpponentOnOutLap;
        public string Topic { get { return Relation == NativeTimingRelation.ClassAhead ? "native-gap-front" :
            Relation == NativeTimingRelation.ClassBehind ? "native-gap-behind" : "native-gap-track"; } }

        private NativeTimingFact(GameStateData gs, string key, OpponentData op, NativeTimingRelation relation,
            NativeTimingTrend trend, float seconds, int lapDelta)
        {
            Key = key; Name = op.DriverRawName; CarNumber = op.CarNumber; CostId = op.CostId;
            PlayerClass = ClassId(gs.carClass); OpponentClass = ClassId(op.CarClass);
            Track = gs.SessionData.TrackDefinition == null ? gs.trackName : gs.SessionData.TrackDefinition.name;
            Session = gs.SessionData.SessionId; Type = gs.SessionData.SessionType; SessionStart = gs.SessionData.SessionStartTime;
            Utc = gs.Now; SourceUtc = gs.SourceTelemetryUtc == DateTime.MinValue ? gs.Now : gs.SourceTelemetryUtc;
            PlayerLap = gs.SessionData.CompletedLaps; OpponentLap = op.CompletedLaps;
            PlayerPosition = gs.SessionData.ClassPosition; OpponentPosition = op.ClassPosition;
            Relation = relation; Trend = trend; NativeSeconds = seconds; SignedLapDelta = lapDelta;
            PlayerInPits = gs.PitData.InPitlane; OpponentInPits = op.InPits; OpponentEnteringPits = op.isEnteringPits();
            PlayerOnOutLap = gs.PitData.OnOutLap; OpponentOnOutLap = op.isOnOutLap();
        }
        internal static string ClassId(CarData.CarClass carClass)
        { return carClass == null ? null : carClass.getClassIdentifier(); }
        public static NativeTimingFact Capture(GameStateData gs, string opponentKey, NativeTimingRelation relation,
            NativeTimingTrend trend, float nativeSeconds, int signedLapDelta)
        {
            OpponentData op;
            if (gs == null || opponentKey == null || !Enum.IsDefined(typeof(NativeTimingRelation), relation) ||
                !Enum.IsDefined(typeof(NativeTimingTrend), trend) || !gs.OpponentData.TryGetValue(opponentKey, out op) ||
                op == null || string.IsNullOrWhiteSpace(op.DriverRawName) || !op.IsActive ||
                float.IsNaN(nativeSeconds) || float.IsInfinity(nativeSeconds) || nativeSeconds <= .05f || nativeSeconds > 3600)
                return null;
            var fact = new NativeTimingFact(gs, opponentKey, op, relation, trend, nativeSeconds, signedLapDelta);
            return NativeTimingEvents.StillCurrent(fact, gs, gs.Now) ? fact : null;
        }
    }

    /// <summary>A bounded semantic handoff, not another gap/trend detector. Capture can run on the native event
    /// thread; Observe/Take/Reset have one telemetry-thread owner. No delayed callbacks or sound paths are used.</summary>
    public sealed class NativeTimingEvents
    {
        public const int MaximumIncoming = 32, MaximumPending = 16, MaximumSeen = 128;
        public static readonly TimeSpan MaximumAge = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan DecisionLifetime = TimeSpan.FromSeconds(8);
        private readonly ConcurrentQueue<NativeTimingFact> incoming = new ConcurrentQueue<NativeTimingFact>();
        private sealed class Entry
        {
            public NativeTimingFact Fact;
            public bool Names;
            public ObservedRaceCall Call;
        }
        private readonly List<Entry> pending = new List<Entry>();
        private readonly Dictionary<string, DateTime> seen = new Dictionary<string, DateTime>();
        private DateTime lastRadio = DateTime.MinValue;
        private readonly RadioVariantCursor[,] variants = CreateVariants();

        private static RadioVariantCursor[,] CreateVariants()
        {
            var cursors = new RadioVariantCursor[3, 4];
            for (int relation = 0; relation < 3; relation++)
                for (int trend = 0; trend < 4; trend++) cursors[relation, trend] = new RadioVariantCursor();
            return cursors;
        }
        public int IncomingCount { get { return incoming.Count; } }
        public int PendingCount { get { return pending.Count; } }

        public void Capture(NativeTimingFact fact)
        {
            if (fact == null) return;
            incoming.Enqueue(fact);
            NativeTimingFact unused;
            while (incoming.Count > MaximumIncoming) incoming.TryDequeue(out unused);
        }
        public void Reset()
        {
            NativeTimingFact unused;
            while (incoming.TryDequeue(out unused)) { }
            pending.Clear(); seen.Clear(); lastRadio = DateTime.MinValue;
        }
        private static string SafeName(string name)
        { return new string((name ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '\'').Take(60).ToArray()).Trim(); }
        private static bool Fresh(DateTime source, DateTime now)
        { return source != DateTime.MinValue && now >= source && now - source <= MaximumAge; }

        public static bool StillCurrent(NativeTimingFact fact, GameStateData gs)
        { return gs != null && StillCurrent(fact, gs, gs.Now); }
        public static bool StillCurrent(NativeTimingFact fact, GameStateData gs, DateTime now)
        {
            if (fact == null || gs == null || now < fact.Utc || now - fact.Utc > DecisionLifetime ||
                !Fresh(fact.SourceUtc, fact.Utc) || !Fresh(gs.Now, now) ||
                !Fresh(gs.SourceTelemetryUtc == DateTime.MinValue ? gs.Now : gs.SourceTelemetryUtc, now)) return false;
            var s = gs.SessionData;
            if (s.SessionId != fact.Session || s.SessionType != fact.Type || s.SessionStartTime != fact.SessionStart ||
                s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Green || s.IsNewSession ||
                GameStateData.onManualFormationLap || gs.PitData.InPitlane || gs.PitData.OnInLap ||
                Events.PitStops.isPittingThisLap || gs.PitData.OnOutLap != fact.PlayerOnOutLap ||
                gs.FlagData.currentLapIsFCY || gs.FlagData.isLocalYellow || s.Flag == FlagEnum.BLUE ||
                s.CompletedLaps != fact.PlayerLap || s.ClassPosition != fact.PlayerPosition ||
                fact.PlayerInPits || fact.OpponentInPits || fact.OpponentEnteringPits ||
                string.IsNullOrWhiteSpace(fact.PlayerClass) || string.IsNullOrWhiteSpace(fact.OpponentClass) ||
                NativeTimingFact.ClassId(gs.carClass) != fact.PlayerClass ||
                (s.TrackDefinition == null ? gs.trackName : s.TrackDefinition.name) != fact.Track) return false;
            OpponentData op;
            if (!gs.OpponentData.TryGetValue(fact.Key, out op) || op == null || !op.IsActive ||
                op.DriverRawName != fact.Name || op.CarNumber != fact.CarNumber || op.CostId != fact.CostId ||
                op.ClassPosition != fact.OpponentPosition || op.CompletedLaps != fact.OpponentLap ||
                op.InPits || op.isEnteringPits() || op.isOnOutLap() != fact.OpponentOnOutLap ||
                NativeTimingFact.ClassId(op.CarClass) != fact.OpponentClass ||
                !CarData.IsCarClassEqual(gs.carClass, op.CarClass, false)) return false;
            if (fact.Relation == NativeTimingRelation.ClassAhead)
                return s.ClassPosition > 1 && s.OpponentKeyInFront == fact.Key && op.ClassPosition == s.ClassPosition - 1 &&
                    fact.SignedLapDelta == 0 && s.LapsDeltaFront == 0 && NativeGapUnchanged(fact, s.TimeDeltaFront);
            if (fact.Relation == NativeTimingRelation.ClassBehind)
                return s.OpponentKeyBehind == fact.Key && op.ClassPosition == s.ClassPosition + 1 &&
                    fact.SignedLapDelta == 0 && s.LapsDeltaBehind == 0 && NativeGapUnchanged(fact, s.TimeDeltaBehind);
            // This is a separate relation, never silently relabelled as the class rival behind.
            return s.TrackDefinition != null && fact.Key != s.OpponentKeyBehind &&
                fact.Key == gs.getOpponentKeyBehindOnTrack(false, gs.carClass);
        }
        private static bool NativeGapUnchanged(NativeTimingFact fact, float current)
        {
            // Revalidate the original decision, without constructing a second trend window.
            return !float.IsNaN(current) && !float.IsInfinity(current) && current > .05f &&
                Math.Abs(current - fact.NativeSeconds) <= .5f;
        }

        public void Observe(GameStateData gs, DateTime now, AiRaceMemory memory, bool names)
        {
            foreach (string id in seen.Where(p => now < p.Value || now - p.Value > TimeSpan.FromSeconds(30)).Select(p => p.Key).ToArray()) seen.Remove(id);
            NativeTimingFact fact;
            while (incoming.TryDequeue(out fact))
            {
                if (!StillCurrent(fact, gs, now)) continue;
                string id = fact.Topic + "|" + fact.Key + "|" + fact.Name + "|" + fact.Trend + "|" + fact.PlayerLap + "|" + fact.OpponentLap;
                if (seen.ContainsKey(id)) continue;
                if (seen.Count >= MaximumSeen) seen.Remove(seen.OrderBy(p => p.Value).First().Key);
                seen[id] = now;
                string observation = Text(fact, names, 0, new RaceGapMeasurement());
                if (memory != null && observation != null)
                    memory.AddObservation(observation, now, fact.Topic, fact.Key, NativeTimingFact.Source);
                // GetRelativeDelta has no exported passage timestamps/quality. Store its native observation,
                // but do not turn that track estimate into an official gap or a second traffic warning.
                if (fact.Relation == NativeTimingRelation.BehindOnTrack)
                {
                    LiveDiagnostics.Decision(fact.Topic, "stored", "native_track_gap_without_passage_provenance", fact.Key);
                    continue;
                }
                if (pending.Count >= MaximumPending) pending.RemoveAt(0);
                pending.RemoveAll(e => e.Fact.Topic == fact.Topic && e.Call == null);
                pending.Add(new Entry { Fact = fact, Names = names });
                LiveDiagnostics.Decision(fact.Topic, "detected", "native_semantic_handoff", fact.Key);
            }
        }

        public ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool enabled, bool minimal)
        { return Take(new RaceDataContext { GameState = gs, NowUtc = now, LastTelemetryUtc = gs == null ? DateTime.MinValue :
            gs.SourceTelemetryUtc == DateTime.MinValue ? gs.Now : gs.SourceTelemetryUtc }, busy, enabled, minimal); }
        public ObservedRaceCall Take(RaceDataContext context, bool busy, bool enabled, bool minimal)
        {
            if (context == null) return null;
            GameStateData gs = context.GameState; DateTime now = context.NowUtc;
            foreach (Entry entry in pending.ToArray())
            {
                if (entry.Call != null && entry.Call.DeliveryStartedUtc != DateTime.MinValue)
                { lastRadio = entry.Call.DeliveryStartedUtc; pending.Remove(entry); }
                else if (!StillCurrent(entry.Fact, gs, now) || entry.Call != null && !entry.Call.IsValid(gs, now)) pending.Remove(entry);
            }
            if (!enabled || minimal) { pending.Clear(); return null; }
            if (!Fresh(context.LastTelemetryUtc, now) || busy || AiRadioSchedule.BusyForRoutine(gs) ||
                now < lastRadio || now - lastRadio < TimeSpan.FromSeconds(8)) return null;
            if (pending.Any(e => e.Call != null && !e.Call.CanReserve(now))) return null;
            foreach (Entry entry in pending.ToArray())
            {
                if (entry.Call == null)
                {
                    NativeTimingFact fact = entry.Fact;
                    RaceGapMeasurement measurement = ReadMeasurement(context, fact);
                    if (fact.Trend == NativeTimingTrend.Steady && !measurement.HasValue) { pending.Remove(entry); continue; }
                    var cursor = variants[(int)fact.Relation, (int)fact.Trend];
                    string text = Text(fact, entry.Names, cursor.Heard, measurement, RaceRadioLanguage.SeparateClass(gs.SessionData));
                    if (text == null) { pending.Remove(entry); continue; }
                    TrackAwareness tracks = context.Tracks;
                    entry.Call = new ObservedRaceCall {
                        Topic = fact.Topic, Text = text, Evidence = NativeTimingFact.Source + ";opponent=" + fact.Key + ";relation=" + fact.Relation + ";trend=" + fact.Trend,
                        SessionId = fact.Session, SessionType = fact.Type, CompletedLaps = fact.PlayerLap,
                        Position = fact.PlayerPosition, Member = TeamMember.Ben, Priority = 3, VariantCursor = cursor,
                        DetectedUtc = fact.Utc, ExpiresUtc = fact.Utc.Add(DecisionLifetime),
                        TimedValidity = (live, when) => StillCurrent(fact, live, when) && !AiRadioSchedule.BusyForRoutine(live) &&
                            MeasurementStillCurrent(fact, measurement, live, when, tracks)
                    };
                }
                if (entry.Call.IsValid(gs, now) && entry.Call.TryReserve(now)) return entry.Call;
            }
            return null;
        }
        private static RaceGapMeasurement ReadMeasurement(RaceDataContext context, NativeTimingFact fact)
        {
            OpponentData op;
            if (!StillCurrent(fact, context.GameState, context.NowUtc) || fact.Relation == NativeTimingRelation.BehindOnTrack ||
                !context.GameState.OpponentData.TryGetValue(fact.Key, out op)) return new RaceGapMeasurement();
            var value = RaceDataTools.ReadGap(context, op, fact.Relation == NativeTimingRelation.ClassAhead);
            bool official = value.Kind == RaceGapKind.SimulatorTiming;
            bool samePoint = value.Kind == RaceGapKind.InterpolatedTiming && context.Tracks != null && Fresh(value.MeasuredUtc, context.NowUtc);
            return (official || samePoint) && value.Seconds > .05f && value.Seconds <= 300 ? value : new RaceGapMeasurement();
        }
        private static bool MeasurementStillCurrent(NativeTimingFact fact, RaceGapMeasurement original, GameStateData gs, DateTime now, TrackAwareness tracks)
        {
            if (!original.HasValue) return true; // Qualitative native trend does not claim a number.
            var live = ReadMeasurement(new RaceDataContext { GameState = gs, NowUtc = now, LastTelemetryUtc = gs.Now, Tracks = tracks }, fact);
            return live.HasValue && live.Kind == original.Kind && Math.Abs(live.Seconds - original.Seconds) <= .5f;
        }
        private static string Text(NativeTimingFact fact, bool names, int variant, RaceGapMeasurement measurement, bool separateClass = false)
        {
            string name = names ? SafeName(fact.Name) : "";
            bool ahead = fact.Relation == NativeTimingRelation.ClassAhead;
            string text;
            if (fact.Relation == NativeTimingRelation.BehindOnTrack)
                return "Nativ erkannter Abstand zum Wagen hinter uns auf der Strecke " +
                    (fact.Trend == NativeTimingTrend.Decreasing ? "wird kleiner" : fact.Trend == NativeTimingTrend.Increasing ? "wächst" :
                    fact.Trend == NativeTimingTrend.Close ? "bleibt klein" : "bleibt ähnlich") + ". Kein bestätigter Klassenabstand.";
            string interval = measurement.HasValue ? (measurement.IsEstimated ? "ungefähr " : "") +
                measurement.Seconds.ToString("0.0", CultureInfo.GetCultureInfo("de-DE")) + " Sekunden" : null;
            bool gapDiffers = measurement.HasValue && fact.NativeSeconds > .05f &&
                Math.Abs(measurement.Seconds - fact.NativeSeconds) > Math.Max(.25f, measurement.Seconds * .20f);
            // The native trend may describe a different timing source. Never combine it with
            // a conflicting passage interval or call a distant class car "dicht dran".
            if (separateClass || gapDiffers || fact.Trend == NativeTimingTrend.Close && measurement.HasValue && measurement.Seconds > 2)
                return interval == null ? null : RaceRadioLanguage.MeasuredGap(ahead, name, interval, separateClass, variant);
            string group = (ahead ? "gap-front-" : "gap-rear-") + fact.Trend.ToString().ToLowerInvariant();
            string subject = name.Length > 0 ? name : ahead ? "Der Vordermann" : "Der Hintermann";
            string dative = name.Length > 0 ? name : ahead ? "dem Vordermann" : "dem Hintermann";
            string target = name.Length > 0 ? name : ahead ? "den Vordermann" : "den Hintermann";
            text = RadioPhraseCatalog.Current.Format(group, variant, subject, dative, target);
            if (text.Length > 0) text = char.ToUpper(text[0], CultureInfo.GetCultureInfo("de-DE")) + text.Substring(1);
            if (interval != null)
                text = (ahead ? "Nach vorn " : "Nach hinten ") + interval + ". " + text;
            return text;
        }
    }
}
