using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Session-end feedback: checkered is provisional; only Finished confirms the result.</summary>
    public sealed class QualifyingResultWatch
    {
        private ObservedRaceCall pending;
        private bool seenQualifying, provisionalDelivered, finalDelivered;
        private int session=int.MinValue, variant;
        private DateTime lastTick, retryAfter;
        public void Reset()
        {pending=null;seenQualifying=provisionalDelivered=finalDelivered=false;session=int.MinValue;lastTick=retryAfter=DateTime.MinValue;}
        public void Observe(GameStateData gs,DateTime now,string driver)
        {
            if(gs==null || gs.SessionData.SessionType!=SessionType.Qualify) {Reset();return;}
            var s=gs.SessionData;
            if(!seenQualifying || session!=s.SessionId || now<lastTick || s.SessionPhase==SessionPhase.Green && (provisionalDelivered || finalDelivered))
            {Reset();seenQualifying=true;session=s.SessionId;}
            lastTick=now;
            if(pending!=null && pending.DeliveryStartedUtc!=DateTime.MinValue)
            {if(pending.Evidence=="confirmed_qualifying_finished") finalDelivered=true;else provisionalDelivered=true;pending=null;}
            bool final=s.SessionPhase==SessionPhase.Finished;
            if(!final && s.SessionPhase!=SessionPhase.Checkered || finalDelivered || !final && provisionalDelivered) {pending=null;return;}
            if(pending!=null && pending.IsValid(gs,now))return;
            if(pending!=null && (pending.DeliveryFailed || now>pending.ExpiresUtc))
            {pending=null;retryAfter=now.AddSeconds(15);return;}
            if(now<retryAfter)return;
            int position=s.ClassPosition<=TrackAwareness.MaxCars?s.ClassPosition:0;
            string text=Compose(driver,position,final,variant++);
            pending=new ObservedRaceCall {Topic="qualifying-result",Text=text,Member=TeamMember.Ben,Priority=4,
                Position=position>0?position:-1,SessionId=s.SessionId,SessionType=s.SessionType,
                AllowPitlane=true,AllowLapFinish=true,DetectedUtc=now,ExpiresUtc=now.AddSeconds(20),
                Evidence=final?"confirmed_qualifying_finished":"checkered_qualifying_provisional",
                AdditionalValidity=live=>live.SessionData.SessionPhase==(final?SessionPhase.Finished:SessionPhase.Checkered)};
            LiveDiagnostics.Decision(pending.Topic,"detected",pending.Evidence,text);
        }
        public ObservedRaceCall Take(GameStateData gs,DateTime now,bool busy,bool enabled)
        {return pending!=null && !busy && enabled && pending.IsValid(gs,now) && pending.TryReserve(now)?pending:null;}
        public static string Compose(string driver,int position,bool final,int variant=0)
        {
            string name=AiPersonas.CleanForRadio(driver??"Rob",40,false);
            string prefix=name+", "+(final?RadioLanguage.T("die Qualifikation ist beendet. ","qualifying is over. ","la clasificación ha terminado. "):
                RadioLanguage.T("die Quali-Zeit ist vorbei. ","qualifying time is up. ","se acabó el tiempo de clasificación. "));
            if(position<=0 || position>TrackAwareness.MaxCars)return prefix+RadioLanguage.T("Dein Platz ist noch nicht bestätigt.","Your position isn't confirmed yet.","Tu posición aún no está confirmada.");
            if(!final)return prefix+RadioLanguage.T("Aktuell Platz "+position+". Das Ergebnis ist noch vorläufig.","Currently P"+position+". Result is still provisional.","Ahora mismo P"+position+". El resultado aún es provisional.");
            bool even=variant%2==0;
            string encouragement=position==1?(even?RadioLanguage.T("Ganz vorne! Stark gemacht.","Right at the front! Great job.","¡En lo más alto! Gran trabajo."):
                    RadioLanguage.T("Die Bestzeit steht. Sehr gut gemacht.","Fastest time is yours. Very well done.","El mejor tiempo es tuyo. Muy bien hecho.")):
                position<=3?(even?RadioLanguage.T("Unter den ersten drei, stark gemacht.","Top three, great job.","Entre los tres primeros, gran trabajo."):
                    RadioLanguage.T("Top drei. Das ist eine gute Ausgangslage.","Top three. That's a good starting spot.","Top tres. Es una buena posición de salida.")):
                position<=10?(even?RadioLanguage.T("Unter den ersten zehn. Jetzt konzentrieren wir uns aufs Rennen.","Top ten. Now we focus on the race.","Entre los diez primeros. Ahora a centrarnos en la carrera."):
                    RadioLanguage.T("Jetzt richten wir den Fokus aufs Rennen.","Now we turn our focus to the race.","Ahora el foco está en la carrera.")):
                even?RadioLanguage.T("Jetzt richten wir uns fürs Rennen ein.","Now we get set up for the race.","Ahora nos preparamos para la carrera."):
                    RadioLanguage.T("Jetzt ruhig bleiben und aufs Rennen konzentrieren.","Stay calm now and focus on the race.","Ahora con calma y a centrarse en la carrera.");
            return prefix+RadioLanguage.T("Platz ","P","P")+position+". "+encouragement;
        }
    }
}
