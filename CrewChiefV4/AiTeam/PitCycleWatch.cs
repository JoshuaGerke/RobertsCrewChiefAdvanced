using System;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Compares the same class rival before the first entry and after both observed stops.</summary>
    internal sealed class PitCycleWatch
    {
        private sealed class Timing
        {
            internal string Key, Identity;
            internal RaceGapKind Kind;
            internal DateTime Since, At;
            private float minimum, maximum;
            internal float SignedGap;
            internal int Samples;
            internal bool Stable => Samples >= 3 && At - Since >= TimeSpan.FromSeconds(.6);

            internal void Add(string key, string identity, RaceGapMeasurement gap, bool ahead, DateTime now)
            {
                float signed = ahead ? -gap.Seconds : gap.Seconds;
                if (Samples == 0 || key != Key || identity != Identity || Kind != gap.Kind ||
                    Math.Sign(signed) != Math.Sign(SignedGap) || now - At > TimeSpan.FromSeconds(1) ||
                    Math.Max(maximum,signed) - Math.Min(minimum,signed) > .45f)
                {
                    Key=key; Identity=identity; Kind=gap.Kind; Since=now; Samples=0;
                    minimum=maximum=signed;
                }
                minimum=Math.Min(minimum,signed);
                maximum=Math.Max(maximum,signed);
                Samples++; SignedGap=signed; At=now;
            }
        }

        private sealed class Visit
        {
            internal int EntryLap = -1, ExitLap = -1;
            internal bool Completed, Abandoned;
            private bool stopped;
            private DateTime stoppedSince;
            internal void Observe(bool inPits, float speed, int lap, DateTime now)
            {
                if (Completed) { if (inPits) Abandoned=true; return; }
                if (!inPits)
                {
                    if (EntryLap >= 0) { Completed=stopped; Abandoned=!stopped; ExitLap=lap; }
                    return;
                }
                if (EntryLap < 0) EntryLap=lap;
                if (!(speed >= 0 && speed <= 1)) stoppedSince=DateTime.MinValue;
                else if (stoppedSince == DateTime.MinValue) stoppedSince=now;
                else if (now - stoppedSince >= TimeSpan.FromSeconds(1)) stopped=true;
            }
        }

        private sealed class Cycle
        {
            internal Timing Before;
            internal readonly Timing After = new Timing();
            internal readonly Visit Own = new Visit(), Rival = new Visit();
        }

        internal sealed class MergeForecast
        {
            internal readonly int Session;
            internal readonly DateTime Utc;
            internal readonly string Status;
            internal readonly double PitSeconds, RelativeLossSeconds, HorizonSeconds;
            internal readonly int KnownCars;
            internal MergeForecast(int session, DateTime utc, string status, double pitSeconds,
                double relativeLossSeconds, double horizonSeconds, int knownCars)
            {
                Session=session; Utc=utc; Status=status; PitSeconds=pitSeconds; RelativeLossSeconds=relativeLossSeconds;
                HorizonSeconds=horizonSeconds; KnownCars=knownCars;
            }
            internal JObject Describe() => new JObject {
                ["lage"] = Status, ["geschätzt"] = true, ["gemessene_boxendauer_s"] = Math.Round(PitSeconds, 1),
                ["geschaetzter_zeitverlust_s"] = Math.Round(RelativeLossSeconds, 1),
                ["bis_ausfahrt_s"] = Math.Round(HorizonSeconds, 1), ["gegner_mit_prognose"] = KnownCars
            };
        }

        private sealed class LaneRun
        {
            internal double Entry, Exit;
            internal float EntryTime, Seconds;
            internal DateTime StoppedSince;
            internal bool Stationary;
        }

        private LaneRun runningVisit, measuredVisit;
        private MergeForecast forecast;
        private DateTime lastMergeSample, lastForecastUtc;
        private long lastMergeCallTicks;

        internal bool MergeReportDue(DateTime now) =>
            now.Ticks-Interlocked.Read(ref lastMergeCallTicks) >= TimeSpan.FromSeconds(90).Ticks;
        internal void NoteMergeReport(DateTime now) => Interlocked.Exchange(ref lastMergeCallTicks, now.Ticks);
        private bool? lastPitLane;
        private ObservedRaceCall mergeCall;
        private readonly RadioVariantCursor mergeVariants = new RadioVariantCursor();

        private Timing front = new Timing(), rear = new Timing();
        private Cycle cycle;
        private DateTime lastSource;
        private int session = int.MinValue;
        private readonly RadioVariantCursor variants = new RadioVariantCursor();

        internal void Reset()
        {
            ResetComparison();
            runningVisit = measuredVisit = null;
            lastMergeSample = lastForecastUtc = DateTime.MinValue;
            Interlocked.Exchange(ref lastMergeCallTicks, 0);
            lastPitLane = null;
            mergeCall = null;
            Volatile.Write(ref forecast, null);
        }

        private void ResetComparison()
        { front=new Timing(); rear=new Timing(); cycle=null; lastSource=DateTime.MinValue; session=int.MinValue; }

        internal MergeForecast ReadForecast(GameStateData state, DateTime now)
        {
            var value = Volatile.Read(ref forecast);
            return value != null && state != null && IsRacing(state) && !state.PitData.InPitlane &&
                value.Session == state.SessionData.SessionId && now >= value.Utc && now-value.Utc <= TimeSpan.FromSeconds(1)
                ? value : null;
        }

        internal void ObservePitExit(RaceDataContext context, GameStateData previous)
        {
            var state = context.GameState;
            var s = state.SessionData;
            DateTime now = context.NowUtc;
            var map = context.Tracks?.Snapshot;
            var distance = map?.PlayerDistanceM;
            if (previous == null || !IsRacing(state) || map == null || !map.IsCurrent(context) || !distance.HasValue ||
                !TrackAwareness.InRange(s.SessionRunningTime, 0, 200000).HasValue)
            {
                runningVisit = null;
                lastPitLane = null;
                Volatile.Write(ref forecast, null);
                return;
            }
            if (map.SourceUtc <= lastMergeSample) return;
            if (lastMergeSample != DateTime.MinValue && map.SourceUtc-lastMergeSample > TimeSpan.FromSeconds(1))
            { runningVisit = null; lastPitLane = null; }
            lastMergeSample = map.SourceUtc;
            bool? wasInPit = lastPitLane;
            lastPitLane = state.PitData.InPitlane;
            if (state.PitData.InPitlane)
            {
                Volatile.Write(ref forecast, null);
                if (wasInPit == false && !previous.PitData.IsInGarage && !state.PitData.IsInGarage)
                    runningVisit = new LaneRun { Entry=distance.Value, EntryTime=s.SessionRunningTime };
                if (runningVisit == null) return;
                if (map.PlayerSpeedMps <= 1)
                {
                    if (runningVisit.StoppedSince == DateTime.MinValue) runningVisit.StoppedSince = now;
                    else if (now-runningVisit.StoppedSince >= TimeSpan.FromSeconds(1)) runningVisit.Stationary = true;
                }
                else runningVisit.StoppedSince = DateTime.MinValue;
                return;
            }
            if (wasInPit == true && runningVisit != null)
            {
                runningVisit.Exit = distance.Value;
                runningVisit.Seconds = s.SessionRunningTime - runningVisit.EntryTime;
                double segment = (runningVisit.Exit-runningVisit.Entry+map.LengthM.GetValueOrDefault()) % map.LengthM.GetValueOrDefault();
                if (runningVisit.Stationary && runningVisit.Seconds >= 3 && runningVisit.Seconds <= 90 &&
                    segment >= 20 && segment <= Math.Min(1800, map.LengthM.GetValueOrDefault() / 2))
                    measuredVisit = runningVisit;
                runningVisit = null;
            }

            var plan = measuredVisit;
            if (plan == null || !map.LengthM.HasValue || map.PlayerSpeedMps.GetValueOrDefault() < 10)
            { Volatile.Write(ref forecast, null); return; }
            double length = map.LengthM.Value;
            double approach = (plan.Entry-distance.Value+length) % length;
            if (approach > 450)
            { Volatile.Write(ref forecast, null); return; }
            if (now-lastForecastUtc < TimeSpan.FromMilliseconds(500)) return;
            lastForecastUtc = now;
            var clean = context.Memory?.GetLaps(5).Where(l => l.Green && l.Valid && !l.InPitLap && l.LapTime > 15).Reverse().Take(3).ToArray();
            if (clean == null || clean.Length < 2)
            { Volatile.Write(ref forecast, null); return; }
            double referenceSpeed = length / clean.Average(l => l.LapTime);
            double horizon = plan.Seconds + approach / ((map.PlayerSpeedMps.Value+referenceSpeed) / 2);
            double section = (plan.Exit-plan.Entry+length)%length;
            double loss = plan.Seconds-section/referenceSpeed;
            if (loss < 1 || horizon > 100)
            { Volatile.Write(ref forecast, null); return; }
            string status = RaceTrafficWatch.ForecastMerge(context, map, plan.Exit, horizon, out int known);
            Volatile.Write(ref forecast, new MergeForecast(s.SessionId, now, status, plan.Seconds, loss, horizon, known));
        }

        internal ObservedRaceCall TakeMergeCall(RaceDataContext context)
        {
            var state = context.GameState;
            var now = context.NowUtc;
            if (mergeCall?.DeliveryStartedUtc > DateTime.MinValue)
            { NoteMergeReport(mergeCall.DeliveryStartedUtc); mergeCall = null; }
            var outcome = ReadForecast(state, now);
            if (outcome == null || outcome.Status == "unknown" || !MergeReportDue(now) ||
                !state.PitData.HasRequestedPitStop && !state.PitData.IsApproachingPitlane && state.PitData.PitWindow != PitWindow.Open)
                return null;
            if (mergeCall == null || !mergeCall.IsValid(state, now))
            {
                const string group = "pit-exit-forecast";
                var catalog = RadioPhraseCatalog.Current;
                int variant = mergeVariants.Select(group, catalog.Count(group));
                string message = outcome.Status == "traffic"
                    ? RadioLanguage.T("An der Boxenausfahrt könnte Verkehr sein.", "There may be traffic at pit exit.", "Puede haber tráfico al salir de boxes.")
                    : RadioLanguage.T("An der Boxenausfahrt sieht es nach einer freien Lücke aus.", "Pit exit looks likely to be clear.", "La salida de boxes parece despejada.");
                mergeCall = new ObservedRaceCall { Topic=group, Text=catalog.Format(group, variant, message), Member=TeamMember.Mik,
                    Evidence="measured_prior_pit_visit;bounded_estimated_traffic", Priority=3, Position=-1,
                    SessionId=state.SessionData.SessionId, SessionType=state.SessionData.SessionType, DetectedUtc=now, ExpiresUtc=now.AddSeconds(8),
                    TimedValidity=(live, at) => ReadForecast(live, at)?.Status == outcome.Status };
                mergeCall.BindVariant(mergeVariants, group, variant);
            }
            return mergeCall.TryReserve(now) ? mergeCall : null;
        }

        private static bool IsRacing(GameStateData gs) => gs.SessionData.SessionType==SessionType.Race &&
            gs.SessionData.SessionPhase==SessionPhase.Green && !gs.FlagData.isLocalYellow && !gs.PitData.IsInGarage;

        private static bool ValidTiming(RaceGapMeasurement gap) =>
            (gap.Kind==RaceGapKind.SimulatorTiming || gap.Kind==RaceGapKind.InterpolatedTiming) && gap.Seconds>=.05f && gap.Seconds<=60;

        internal ObservedRaceCall Observe(RaceDataContext context, RaceGapMeasurement ahead, RaceGapMeasurement behind)
        {
            var gs=context.GameState;
            var s=gs.SessionData;
            DateTime now=context.NowUtc;
            DateTime source=gs.SourceTelemetryUtc==DateTime.MinValue?context.LastTelemetryUtc:gs.SourceTelemetryUtc;
            if (session!=s.SessionId || !IsRacing(gs) || source==DateTime.MinValue || source>now ||
                now-source>TimeSpan.FromSeconds(1) || lastSource!=DateTime.MinValue && source-lastSource>TimeSpan.FromSeconds(1))
                ResetComparison();
            if (!IsRacing(gs) || source==DateTime.MinValue || source>now || now-source>TimeSpan.FromSeconds(1) ||
                source<=lastSource) return null;
            session=s.SessionId;
            lastSource=source;

            if (cycle==null)
            {
                Timing baseline=StartingWindow(gs,front,now)?front:null;
                if (StartingWindow(gs,rear,now) && (baseline==null || Math.Abs(rear.SignedGap)<Math.Abs(baseline.SignedGap))) baseline=rear;
                if (baseline==null)
                {
                    UpdateBaseline(gs,front,ahead,true,now);
                    UpdateBaseline(gs,rear,behind,false,now);
                    return null;
                }
                cycle=new Cycle { Before=baseline };
                front=new Timing(); rear=new Timing();
            }

            var candidate=cycle;
            var original=candidate.Before;
            if (!FindRival(gs,original,out var rival) || now-original.At>TimeSpan.FromMinutes(10))
            { cycle=null; return null; }
            candidate.Own.Observe(gs.PitData.InPitlane,gs.PositionAndMotionData.CarSpeed,s.CompletedLaps,now);
            candidate.Rival.Observe(rival.InPits,rival.Speed,s.CompletedLaps,now);
            int first=Math.Max(0, Math.Min(candidate.Own.EntryLap<0?int.MaxValue:candidate.Own.EntryLap,
                candidate.Rival.EntryLap<0?int.MaxValue:candidate.Rival.EntryLap));
            if (candidate.Own.Abandoned || candidate.Rival.Abandoned || s.CompletedLaps-first>4 ||
                candidate.Own.EntryLap>=0 && candidate.Rival.EntryLap>=0 && Math.Abs(candidate.Own.EntryLap-candidate.Rival.EntryLap)>3 ||
                candidate.Own.Completed && candidate.Rival.Completed && Math.Abs(candidate.Own.ExitLap-candidate.Rival.ExitLap)>3)
            { cycle=null; return null; }
            if (!candidate.Own.Completed || !candidate.Rival.Completed || !(gs.PositionAndMotionData.CarSpeed>=5) || !(rival.Speed>=5))
                return null;

            bool rivalAhead=rival.ClassPosition==s.ClassPosition-1;
            bool rivalBehind=rival.ClassPosition==s.ClassPosition+1;
            var measured=rivalAhead?ahead:behind;
            if (!(rivalAhead || rivalBehind) || !ReferenceEquals(RaceDataTools.Neighbour(gs,rivalAhead),rival) ||
                !ValidTiming(measured) || measured.Kind!=original.Kind)
            { candidate.After.Samples=0; return null; }
            candidate.After.Add(original.Key,original.Identity,measured,rivalAhead,now);
            if (!candidate.After.Stable) return null;
            cycle=null;
            return Compose(context,original,rival,measured,rivalAhead);
        }

        private static bool FindRival(GameStateData gs, Timing timing, out OpponentData rival) =>
            gs.OpponentData.TryGetValue(timing.Key,out rival) && rival!=null && rival.IsActive &&
            RaceReader.Identity(timing.Key,rival)==timing.Identity && gs.carClass!=null && rival.CarClass!=null &&
            CarData.IsCarClassEqual(gs.carClass,rival.CarClass,false);

        private static bool StartingWindow(GameStateData gs, Timing timing, DateTime now) =>
            timing.Stable && now-timing.At<=TimeSpan.FromSeconds(1) && Math.Abs(timing.SignedGap)<=3.5f &&
            FindRival(gs,timing,out var rival) && (gs.PitData.InPitlane || rival.InPits);

        private static void UpdateBaseline(GameStateData gs, Timing timing, RaceGapMeasurement gap, bool ahead, DateTime now)
        {
            var rival=RaceDataTools.Neighbour(gs,ahead);
            string key=ahead?gs.SessionData.OpponentKeyInFront:gs.SessionData.OpponentKeyBehind;
            if (key==null || rival==null || rival.InPits || gs.PitData.InPitlane || !(rival.Speed>=5) ||
                !(gs.PositionAndMotionData.CarSpeed>=5) || !ValidTiming(gap) || gap.Seconds>3.5f ||
                !gs.OpponentData.TryGetValue(key,out var mapped) || !ReferenceEquals(rival,mapped))
            { timing.Samples=0; return; }
            timing.Add(key,RaceReader.Identity(key,rival),gap,ahead,now);
        }

        private ObservedRaceCall Compose(RaceDataContext context, Timing before, OpponentData rival, RaceGapMeasurement gap, bool ahead)
        {
            var state=context.GameState;
            var s=state.SessionData;
            bool previouslyAhead=before.SignedGap>0;
            string rank=previouslyAhead==!ahead ? ahead ? RadioLanguage.T("Der Platz hinter ihm bleibt.","We stay behind him.","Seguimos detrás de él.") :
                    RadioLanguage.T("Den Platz vor ihm haben wir gehalten.","We kept the place ahead of him.","Mantuvimos la posición por delante de él.") :
                ahead ? RadioLanguage.T("Wir haben den Platz an ihn verloren.","We lost the place to him.","Hemos perdido la posición con él.") :
                    RadioLanguage.T("Wir sind in der Wertung an ihm vorbei.","We're past him in the order.","Le hemos pasado en la clasificación.");
            float difference=(ahead?-gap.Seconds:gap.Seconds)-before.SignedGap;
            string estimate=gap.IsEstimated?RadioLanguage.T("etwa ","about ","unos "):"";
            string interval=RadioLanguage.T("Jetzt ","Now ","Ahora ")+estimate+GermanRadioText.Interval(gap.Seconds)+
                (ahead?RadioLanguage.T(" hinter ihm."," behind him."," por detrás de él."):RadioLanguage.T(" vor ihm."," ahead of him."," por delante de él."));
            string change=Math.Abs(difference)<.2f?RadioLanguage.T("Der Nettoabstand ist praktisch gleich.","The net gap is practically the same.","La diferencia neta es prácticamente la misma.") :
                RadioLanguage.T("Netto ","Net ","Netos ")+estimate+GermanRadioText.Interval(Math.Abs(difference))+
                (difference>0?RadioLanguage.T(" gewonnen."," gained."," ganados."):RadioLanguage.T(" verloren."," lost."," perdidos."));
            string name=context.SendDriverNames && rival.CanUseName?RadioPhraseCatalog.SafeRivalName(rival.DriverRawName):"";
            var catalog=RadioPhraseCatalog.Current;
            const string topic="pit-undercut-overcut-result";
            int index=variants.Select(topic,catalog.Count(topic));
            var call=new ObservedRaceCall { Topic=topic,Text=catalog.Format(topic,index,name.Length>0?name:RadioLanguage.T("Unser Klassengegner","Our class rival","Nuestro rival de clase"),rank,interval,change),
                Evidence="both_pit_visits_with_confirmed_standstill_and_stable_same_rival_timing", Priority=4, Member=TeamMember.Ben,
                Position=-1,SessionId=s.SessionId,SessionType=s.SessionType, FieldNamesAllowed=name.Length>0,
                DetectedUtc=context.NowUtc,ExpiresUtc=context.NowUtc.AddSeconds(12),
                TimedValidity=(live,at)=>{
                    if (!IsRacing(live) || live.PitData.InPitlane || !FindRival(live,before,out var car) || car.InPits ||
                        !ReferenceEquals(RaceDataTools.Neighbour(live,ahead),car)) return false;
                    var current=RaceDataTools.ReadGap(new RaceDataContext { GameState=live,NowUtc=at, Tracks=context.Tracks,
                        LastTelemetryUtc=live.SourceTelemetryUtc==DateTime.MinValue?live.Now:live.SourceTelemetryUtc },car,ahead);
                    return current.Kind==gap.Kind && Math.Abs(current.Seconds-gap.Seconds)<=.45f;
                } };
            call.BindVariant(variants,topic,index);
            return call;
        }
    }
}
