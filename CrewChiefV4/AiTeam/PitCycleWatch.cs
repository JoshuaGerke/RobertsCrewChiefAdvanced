using System;
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

        private Timing front = new Timing(), rear = new Timing();
        private Cycle cycle;
        private DateTime lastSource;
        private int session = int.MinValue;
        private readonly RadioVariantCursor variants = new RadioVariantCursor();

        internal void Reset()
        { front=new Timing(); rear=new Timing(); cycle=null; lastSource=DateTime.MinValue; session=int.MinValue; }

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
                Reset();
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
