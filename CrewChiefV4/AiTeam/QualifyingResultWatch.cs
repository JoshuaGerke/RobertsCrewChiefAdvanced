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
        public void Reset()
        {pending=null;seenQualifying=provisionalDelivered=finalDelivered=unknownFinalDelivered=playerFinished=false;session=int.MinValue;sessionType=SessionType.Unavailable;lastTick=retryAfter=DateTime.MinValue;}
        internal static bool Supported(SessionType type) => type==SessionType.Qualify || type==SessionType.PrivateQualify;
        private static int ResultPosition(SessionData s) => s.ClassPosition>0 && s.ClassPosition<=TrackAwareness.MaxCars?s.ClassPosition:0;
        public void Observe(GameStateData gs,DateTime now,string driver,GameStateData previous=null)
        {
            if(gs==null || !Supported(gs.SessionData.SessionType)) {Reset();return;}
            var s=gs.SessionData;
            if(!seenQualifying || session!=s.SessionId || sessionType!=s.SessionType || now<lastTick || s.SessionPhase==SessionPhase.Green && (provisionalDelivered || finalDelivered || unknownFinalDelivered))
            {Reset();seenQualifying=true;session=s.SessionId;sessionType=s.SessionType;}
            lastTick=now;
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
                ? AiPersonas.CleanForRadio(driver ?? "Rob", 40, false) + RadioLanguage.T(", deine Quali ist durch. ", ", your qualifying is done. ", ", tu clasificación ha terminado. ") +
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
            string prefix=name+", "+(final?RadioLanguage.T("Quali beendet. ","qualifying is over. ","la clasificación ha terminado. "):
                RadioLanguage.T("die Quali-Zeit ist vorbei. ","qualifying time is up. ","se acabó el tiempo de clasificación. "));
            string group=Group(position,final);
            return prefix+(group=="quali-unknown"?RadioPhraseCatalog.Current.Format(group,variant):
                RadioPhraseCatalog.Current.Format(group,variant,position+(separateClass?RadioLanguage.T(" in deiner Klasse"," in class"," en tu clase"):"")));
        }
    }
}
