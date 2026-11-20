using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Session-end feedback: checkered is provisional; only Finished confirms the result.</summary>
    public sealed class QualifyingResultWatch
    {
        private ObservedRaceCall pending;
        private bool seenQualifying, provisionalDelivered, finalDelivered, unknownFinalDelivered;
        private bool playerFinished;
        private int session=int.MinValue;
        private SessionType sessionType=SessionType.Unavailable;
        private readonly RadioVariantCursor variants = new RadioVariantCursor();
        private DateTime lastTick, retryAfter;
        // LMU/rF2 springen bei Ablauf der Quali-Uhr direkt auf „beendet“, obwohl die laufende Runde noch zählt (Quali 20.11.:
        // „Quali beendet, Platz 8“ bei 1222 von 3000 m, Rob kam 42 s später ins Ziel und war Zehnter).
        private SessionPhase lastPhase = SessionPhase.Unavailable;
        private int lastLapCount = -1, finishHoldLap = -1;
        private DateTime lastLapChangeUtc, finishHoldSince, finishCrossedUtc;
        internal static readonly TimeSpan FinishSettle = TimeSpan.FromSeconds(2);
        public void Reset()
        {pending=null;seenQualifying=provisionalDelivered=finalDelivered=unknownFinalDelivered=playerFinished=false;session=int.MinValue;sessionType=SessionType.Unavailable;lastTick=retryAfter=DateTime.MinValue;
         lastPhase=SessionPhase.Unavailable;lastLapCount=finishHoldLap=-1;lastLapChangeUtc=finishHoldSince=finishCrossedUtc=DateTime.MinValue;}

        /// <summary>Der Fahrer ist auf einer laufenden, gültigen Runde unterwegs (nicht Box, nicht Out-Lap).</summary>
        internal static bool OnRunningLap(GameStateData gs) => !gs.PitData.InPitlane && !gs.PitData.OnOutLap &&
            gs.SessionData.CurrentLapIsValid && gs.PositionAndMotionData.CarSpeed > 5;

        /// <summary>Längste Wartezeit auf die letzte Runde nach Ablauf der Uhr.</summary>
        internal static TimeSpan FinishHoldLimit(SessionData s)
        {
            double lap = s.PlayerLapTimeSessionBest > 0 ? s.PlayerLapTimeSessionBest : s.LapTimePrevious;
            double seconds = lap > 0 ? lap * 1.5 : 150;
            return TimeSpan.FromSeconds(Math.Max(60, Math.Min(300, seconds)));
        }

        /// <summary>true = Ergebnis noch zurückhalten, weil die letzte Runde nach Ablauf der Uhr noch läuft.</summary>
        private bool HoldForRunningLap(GameStateData gs, DateTime now)
        {
            var s = gs.SessionData;
            bool justEnded = s.SessionPhase == SessionPhase.Finished && (lastPhase == SessionPhase.Green || lastPhase == SessionPhase.Checkered);
            bool lapJustChanged = s.IsNewLap || lastLapChangeUtc != DateTime.MinValue && now - lastLapChangeUtc < TimeSpan.FromSeconds(5);
            if (justEnded && !playerFinished && OnRunningLap(gs) && !lapJustChanged && s.CompletedLaps >= 0)
            {
                finishHoldLap = s.CompletedLaps;
                finishHoldSince = now;
                finishCrossedUtc = DateTime.MinValue;
                LiveDiagnostics.Decision("qualifying-result", "held", "clock_expired_player_on_running_lap", null, null);
            }
            if (finishHoldLap < 0 || s.SessionPhase != SessionPhase.Finished) return false;
            if (s.CompletedLaps > finishHoldLap)
            {
                if (finishCrossedUtc == DateTime.MinValue) finishCrossedUtc = now;
                // Kurz warten, bis die Wertung die neue Runde eingerechnet hat.
                if (now - finishCrossedUtc < FinishSettle) return true;
                finishHoldLap = -1;
                return false;
            }
            if (gs.PitData.InPitlane || !s.CurrentLapIsValid || now - finishHoldSince > FinishHoldLimit(s))
            {
                finishHoldLap = -1;
                return false;
            }
            return true;
        }
        internal static bool Supported(SessionType type) => type==SessionType.Qualify || type==SessionType.PrivateQualify;
        private static int ResultPosition(SessionData s) => s.ClassPosition>0 && s.ClassPosition<=TrackAwareness.MaxCars?s.ClassPosition:0;
        public void Observe(GameStateData gs,DateTime now,string driver,GameStateData previous=null)
        {
            if(gs==null || !Supported(gs.SessionData.SessionType)) {Reset();return;}
            var s=gs.SessionData;
            if(!seenQualifying || session!=s.SessionId || sessionType!=s.SessionType || now<lastTick || s.SessionPhase==SessionPhase.Green && (provisionalDelivered || finalDelivered || unknownFinalDelivered))
            {Reset();seenQualifying=true;session=s.SessionId;sessionType=s.SessionType;}
            lastTick=now;
            if (lastLapCount >= 0 && s.CompletedLaps != lastLapCount) lastLapChangeUtc = now;
            lastLapCount = s.CompletedLaps;
            bool holdFinish = HoldForRunningLap(gs, now);
            lastPhase = s.SessionPhase;
            if(pending!=null && pending.DeliveryStartedUtc!=DateTime.MinValue)
            {if(pending.Evidence!="checkered_qualifying_provisional") {if(pending.Position>0)finalDelivered=true;else unknownFinalDelivered=true;}else provisionalDelivered=true;pending=null;}
            var before = previous?.SessionData;
            if (before != null && before.SessionId == s.SessionId && before.SessionType == s.SessionType &&
                before.SessionPhase == SessionPhase.Checkered && s.SessionPhase == SessionPhase.Checkered &&
                s.IsNewLap && s.CompletedLaps == before.CompletedLaps + 1 && !gs.PitData.InPitlane)
            {
                playerFinished = true;
                pending = null;
                retryAfter = DateTime.MinValue;
            }
            if (holdFinish) { pending = null; return; }
            bool officialFinish = s.SessionPhase==SessionPhase.Finished;
            if (!officialFinish && !playerFinished && s.SessionPhase == SessionPhase.Checkered &&
                !gs.PitData.InPitlane && !gs.PitData.OnOutLap && s.CurrentLapIsValid &&
                gs.PositionAndMotionData.CarSpeed > 5)
            {
                pending = null;
                return;
            }
            bool final=officialFinish || playerFinished;
            if(!final && s.SessionPhase!=SessionPhase.Checkered || finalDelivered || !final && provisionalDelivered || final && unknownFinalDelivered && ResultPosition(s)==0) {pending=null;return;}
            if(pending!=null && pending.IsValid(gs,now))return;
            if(pending!=null && (pending.DeliveryFailed || now>pending.ExpiresUtc))
            {pending=null;retryAfter=now.AddSeconds(15);return;}
            if(now<retryAfter)return;
            int position=ResultPosition(s);
            bool separateClass=RaceRadioLanguage.SeparateClass(s);
            bool useTemplate = !final || officialFinish;
            string group = Group(position, final);
            int variant = useTemplate ? variants.Select(group, RadioPhraseCatalog.Current.Count(group)) : 0;
            string text=final && !officialFinish
                ? AiPersonas.CleanForRadio(driver ?? "Rob", 40, false) + RadioVariants.Pick("quali-done",
                        RadioLanguage.T(", deine Quali ist durch. ", ", your qualifying is done. ", ", tu clasificación ha terminado. "),
                        RadioLanguage.T(", Quali abgeschlossen. ", ", qualifying complete. ", ", clasificación completada. "),
                        RadioLanguage.T(", das war's mit der Quali. ", ", that's qualifying done. ", ", se acabó la clasificación. "),
                        RadioLanguage.T(", Quali ist vorbei. ", ", qualifying is over. ", ", terminó la clasificación. ")) +
                    (position > 0 ? RadioLanguage.T("Du stehst aktuell auf ", "You're currently ", "Ahora mismo vas ") + RaceRadioLanguage.Rank(s, position) + "." :
                        RadioLanguage.T("Die Platzierung steht noch aus.", "The result isn't in yet.", "La posición aún no está confirmada."))
                : Compose(driver,position,final,variant,separateClass);
            pending=new ObservedRaceCall {Topic="qualifying-result",Text=text,Member=TeamMember.Ben,Priority=4,
                Position=position>0?position:-1,SessionId=s.SessionId,SessionType=s.SessionType,
                AllowPitlane=true,AllowLapFinish=true,DetectedUtc=now,ExpiresUtc=now.AddSeconds(20),
                Evidence=officialFinish?"confirmed_qualifying_finished":playerFinished?"confirmed_player_qualifying_crossing":"checkered_qualifying_provisional",
                AdditionalValidity=live=>live.SessionData.SessionPhase==(officialFinish?SessionPhase.Finished:SessionPhase.Checkered) &&
                    ResultPosition(live.SessionData)==position && RaceRadioLanguage.SeparateClass(live.SessionData)==separateClass};
            if (useTemplate) pending.BindVariant(variants, group, variant);
            LiveDiagnostics.Decision(pending.Topic,"detected",pending.Evidence,text,pending.EventId);
        }
        public ObservedRaceCall Take(GameStateData gs,DateTime now,bool busy,bool enabled)
        {return pending!=null && !busy && enabled && pending.IsValid(gs,now) && pending.TryReserve(now)?pending:null;}
        private static string Group(int position,bool final) => position<=0 || position>TrackAwareness.MaxCars?"quali-unknown":
            !final?"quali-provisional":position==1?"quali-first":position<=3?"quali-top3":"quali-field";
        public static string Compose(string driver,int position,bool final,int variant=0,bool separateClass=false)
        {
            string name=AiPersonas.CleanForRadio(driver??"Rob",40,false);
            if(string.IsNullOrWhiteSpace(name))name="Rob";
            string prefix=name+", "+(final?RadioVariants.Pick("quali-final",
                    RadioLanguage.T("Quali beendet. ","qualifying is over. ","la clasificación ha terminado. "),
                    RadioLanguage.T("Quali abgeschlossen. ","qualifying complete. ","clasificación completada. "),
                    RadioLanguage.T("die Quali ist durch. ","qualifying is done. ","la clasificación ha acabado. "),
                    RadioLanguage.T("Ende der Quali. ","end of qualifying. ","fin de la clasificación. ")):
                RadioVariants.Pick("quali-time-up",
                    RadioLanguage.T("die Quali-Zeit ist vorbei. ","qualifying time is up. ","se acabó el tiempo de clasificación. "),
                    RadioLanguage.T("die Uhr der Quali ist abgelaufen. ","the qualifying clock has run out. ","el reloj de la clasificación ha llegado a cero. "),
                    RadioLanguage.T("Quali-Zeit abgelaufen. ","qualifying time has expired. ","tiempo de clasificación agotado. "),
                    RadioLanguage.T("keine Zeit mehr auf der Quali-Uhr. ","no time left on the qualifying clock. ","no queda tiempo en la clasificación. ")));
            string group=Group(position,final);
            return prefix+(group=="quali-unknown"?RadioPhraseCatalog.Current.Format(group,variant):
                RadioPhraseCatalog.Current.Format(group,variant,position+(separateClass?RadioLanguage.T(" in deiner Klasse"," in class"," en tu clase"):"")));
        }
    }
}
