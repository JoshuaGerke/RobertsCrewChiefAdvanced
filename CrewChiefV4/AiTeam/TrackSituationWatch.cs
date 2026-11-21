using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Practice/qualifying traffic, based on physical route separation and measured closing speed.
    /// Never uses classification/best-lap deltas. Side/clear warnings remain with Sara.</summary>
    public sealed class TrackSituationWatch
    {
        private static readonly string[] CatchingPhrasesDe = {
            "Sehr langsamer Wagen vor dir. Du läufst gleich auf ihn auf.",
            "Achtung, langsamer Verkehr direkt voraus. Behalte ihn im Blick.",
            "Vor dir rollt ein Wagen deutlich langsamer. Du bist gleich dran.",
            "Langsames Auto voraus, du kommst schnell ran. Früh entscheiden, wo du vorbei willst.",
            "Achtung, du holst stark auf einen langsamen Wagen auf.",
            "Vor dir rollt einer gemütlich. Rechtzeitig Platz suchen."
        };
        private static readonly string[] CatchingPhrasesEn = {
            "Very slow car ahead. You'll be on him shortly.",
            "Careful, slow traffic right ahead. Keep an eye on him.",
            "Car ahead is rolling much slower. You're on him shortly.",
            "Slow car ahead, you're closing fast. Decide early where to pass.",
            "Careful, you're closing quickly on a slow car.",
            "Someone's cruising ahead. Find your space in time."
        };
        private static readonly string[] CatchingPhrasesEs = {
            "Coche muy lento delante. Lo vas a alcanzar enseguida.",
            "Atención, tráfico lento justo delante. No lo pierdas de vista.",
            "Delante va un coche mucho más lento. Lo tienes encima enseguida.",
            "Coche lento delante, te acercas rápido. Decide pronto por dónde pasar.",
            "Cuidado, te acercas rápido a un coche lento.",
            "Delante va uno rodando tranquilo. Busca hueco a tiempo."
        };
        internal static string[] CatchingPhrases => RadioLanguage.Pick(CatchingPhrasesDe, CatchingPhrasesEn, CatchingPhrasesEs);
        private static readonly string[] StoppedPhrasesDe = {
            "Achtung, vor dir steht ein Wagen.",
            "Stehendes Fahrzeug voraus. Bleib aufmerksam.",
            "Vorsicht, der Wagen direkt vor dir steht.",
            "Stehendes Auto voraus. Weite Linie, Tempo raus.",
            "Achtung, da steht einer auf der Strecke.",
            "Vorsicht, Wagen steht. Rechtzeitig ausweichen."
        };
        private static readonly string[] StoppedPhrasesEn = {
            "Careful, there's a stopped car ahead.",
            "Stationary car ahead. Stay alert.",
            "Watch out, the car right ahead of you is stopped.",
            "Stopped car ahead. Go wide, ease off.",
            "Heads up, someone's stopped on track.",
            "Careful, car stopped. Move over in time."
        };
        private static readonly string[] StoppedPhrasesEs = {
            "Atención, hay un coche parado delante.",
            "Vehículo detenido delante. Mantente atento.",
            "Cuidado, el coche justo delante de ti está parado.",
            "Coche parado delante. Trazada abierta, afloja.",
            "Atención, hay uno parado en pista.",
            "Cuidado, coche parado. Esquiva a tiempo."
        };
        internal static string[] StoppedPhrases => RadioLanguage.Pick(StoppedPhrasesDe, StoppedPhrasesEn, StoppedPhrasesEs);
        private static readonly string[] ApproachingPhrasesDe = {
            "Schneller Wagen von hinten.",
            "Schneller Verkehr hinter dir.",
            "Von hinten kommt ein schnellerer Wagen.",
            "Im Spiegel: schneller Wagen kommt.",
            "Schneller Wagen hinter dir, Linie halten.",
            "Achtung hinten, da kommt einer mit mehr Tempo."
        };
        private static readonly string[] ApproachingPhrasesEn = {
            "Fast car coming from behind.",
            "Fast traffic behind you.",
            "Faster car closing from behind.",
            "In your mirrors: quick car coming.",
            "Fast car behind, hold your line.",
            "Heads up behind, someone with more pace is coming."
        };
        private static readonly string[] ApproachingPhrasesEs = {
            "Coche rápido por detrás.",
            "Tráfico rápido detrás de ti.",
            "Se acerca un coche más rápido por detrás.",
            "En los espejos: viene un coche rápido.",
            "Coche rápido detrás, mantén la trazada.",
            "Atención detrás, viene uno con más ritmo."
        };
        internal static string[] ApproachingPhrases => RadioLanguage.Pick(ApproachingPhrasesDe, ApproachingPhrasesEn, ApproachingPhrasesEs);
        internal static string ClosingAheadPhrase => ClosingAheadPhrases[0];
        private static readonly string[] ClosingAheadDe = { "Du holst auf den Wagen vor dir auf.", "Der Wagen vor dir kommt näher.", "Du läufst auf den Vordermann auf.", "Vorne wird es enger, du bist schneller." };
        private static readonly string[] ClosingAheadEn = { "You're catching the car ahead.", "The car ahead is coming back to you.", "You're closing on the car in front.", "It's getting closer up front, you're quicker." };
        private static readonly string[] ClosingAheadEs = { "Estás alcanzando al coche de delante.", "El coche de delante se acerca.", "Te estás echando encima del de delante.", "Delante se cierra el hueco, vas más rápido." };
        /// <summary>Vorbereitete Sätze (Cache) für das gewöhnliche Auflaufen im Training/Quali, vier Formulierungen.</summary>
        internal static string[] ClosingAheadPhrases => RadioLanguage.Pick(ClosingAheadDe, ClosingAheadEn, ClosingAheadEs);
        public const double AlertSeconds=6;
        private readonly Dictionary<string,DateTime> last=new Dictionary<string,DateTime>();
        private DateTime lastAny=DateTime.MinValue, lastTick=DateTime.MinValue;
        internal static readonly TimeSpan RepeatAfter=TimeSpan.FromSeconds(90), SameCarAfterAttempt=TimeSpan.FromSeconds(20);
        private int session=int.MinValue,variant,epoch;
        private SessionType type;
        private bool wasNewSession;
        private string track,pendingKey;
        private ObservedRaceCall pending;
        public void Reset() {epoch++;last.Clear();lastAny=lastTick=DateTime.MinValue;session=int.MinValue;track=null;wasNewSession=false;pending=null;pendingKey=null;}
        private static bool Supported(SessionType t) {return t==SessionType.Practice || t==SessionType.Qualify;}
        private static bool Usable(TrackCarPosition c)
        {return !c.InPits && c.SpatiallyConsistent && c.RouteGapM.HasValue && c.SpeedMps.HasValue && c.ClosingMps.HasValue;}
        public static bool MotionPlausible(TrackCarPosition car,double ownSpeed,bool ahead)
        {
            if(car==null || !car.SpeedMps.HasValue || !car.ClosingMps.HasValue) return false;
            double expected=ahead?ownSpeed-car.SpeedMps.Value:car.SpeedMps.Value-ownSpeed;
            return Math.Abs(car.ClosingMps.Value-expected)<=Math.Max(5,ownSpeed*.15);
        }
        public Func<string,bool> CoveredAhead;
        private static bool Relevant(TrackCarPosition car,double ownSpeed,bool ahead,double upperSeconds, bool ordinaryClosing = false)
        {
            if(!Usable(car) || ownSpeed<5 || car.ClosingMps<(ahead?8:3) || !MotionPlausible(car,ownSpeed,ahead) || Math.Abs(car.RouteGapM.Value)<15) return false;
            if(ahead ? car.RouteGapM<=0 : car.RouteGapM>=0) return false;
            double seconds=Math.Abs(car.RouteGapM.Value)/car.ClosingMps.Value;
            if(seconds<.5 || seconds>upperSeconds) return false;
            if(!ahead) return car.SpeedMps>=ownSpeed+3 && car.SpeedMps>=ownSpeed*1.1 &&
                (!car.TrackIntervalSeconds.HasValue || car.TrackIntervalSeconds.Value <= upperSeconds);
            // A normal braking car or a small pace difference is not slow traffic.
            if (ordinaryClosing) return car.SpeedMps >= 30 && ownSpeed >= 45 && car.ClosingMps >= 12 &&
                car.SpeedMps <= ownSpeed * .8 && seconds >= 3 && seconds <= upperSeconds && !car.OnOutLap && !car.OnInLap;
            return car.SpeedMps<=1 || car.SpeedMps<=ownSpeed*.6 &&
                ownSpeed-car.SpeedMps>=8 && (car.OnOutLap || car.OnInLap || car.SpeedMps<=5);
        }
        public ObservedRaceCall Evaluate(TrackAwareness observer,GameStateData state,DateTime now,
            double rearAlertSeconds = AlertSeconds)
        {
            var s=observer==null?null:observer.Snapshot;
            if(state==null || s==null) return null;
            if(session!=s.SessionId || type!=s.SessionType || track!=s.Track || now<lastTick || s.IsNewSession && !wasNewSession)
            {Reset();session=s.SessionId;type=s.SessionType;track=s.Track;}
            lastTick=now;wasNewSession=s.IsNewSession;
            if(pending!=null)
            {
                if(pending.DeliveryStartedUtc!=DateTime.MinValue)
                {last[pendingKey]=lastAny=pending.DeliveryStartedUtc;pending=null;}
                else if(!pending.IsValid(state,now))
                {
                    // LMU 21.11. (Quali): „Sehr langsamer Wagen vor dir … Ahmad Dwaik“ wurde beim Abspielen verworfen und zehn
                    // Sekunden später als „langsamer Verkehr direkt voraus … Ahmad Dwaik“ wiederholt. Ein angefangener Versuch
                    // sperrt dasselbe Auto für SameCarAfterAttempt.
                    if(pending.Attempted && pendingKey!=null) last[pendingKey]=now-RepeatAfter+SameCarAfterAttempt;
                    pending=null;
                }
            }
            // Remove obsolete identities without retaining an unbounded opponent history.
            if(last.Count>512) last.Clear();
            if(!Supported(s.SessionType) || now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1) ||
                state.SessionData.SessionId!=s.SessionId || state.SessionData.SessionType!=s.SessionType ||
                state.SessionData.SessionPhase!=SessionPhase.Green || state.PitData.InPitlane || !s.PlayerSpeedMps.HasValue ||
                pending!=null || now<lastAny || now-lastAny<TimeSpan.FromSeconds(4)) return null;
            var candidates=s.Cars.Where(c=>(c.RouteGapM<=0 || CoveredAhead==null || !CoveredAhead(c.Key)) &&
                (Relevant(c,s.PlayerSpeedMps.Value,c.RouteGapM>0,c.RouteGapM>0 ? AlertSeconds : rearAlertSeconds) ||
                    state.SessionData.SessionType == SessionType.Practice && Relevant(c,s.PlayerSpeedMps.Value,true,AlertSeconds,true)))
                .OrderBy(c=>Math.Abs(c.RouteGapM.Value)/c.ClosingMps.Value);
            foreach(var car in candidates)
            {
                bool ordinaryClosing = car.RouteGapM > 0 && !Relevant(car,s.PlayerSpeedMps.Value,true,AlertSeconds);
                string topic=ordinaryClosing ? "traffic-closing-ahead" : car.RouteGapM>0?"traffic-catching":"traffic-approaching";
                string key=car.Id+"|"+topic;DateTime previous;
                if(last.TryGetValue(key,out previous) && (now<previous || now-previous<RepeatAfter)) continue;
                string[] phrases=topic=="traffic-approaching"?ApproachingPhrases:car.SpeedMps<=1?StoppedPhrases:CatchingPhrases;
                // LMU 18.11. (Training): LMP2 überholte Rob, angesagt wurde nur „schneller Wagen“. Andere Klasse jetzt mit Klasse.
                bool otherClass=MulticlassTraffic.OtherClass(car) && (topic=="traffic-approaching" || topic=="traffic-closing-ahead");
                string text=otherClass ? (topic=="traffic-approaching" ? MulticlassTraffic.Rear(car.ClassLabel,variant++) : MulticlassTraffic.Ahead(car.ClassLabel,variant++)) :
                    ordinaryClosing ? RadioVariants.Pick("closing-ahead",ClosingAheadPhrases) : phrases[variant++%phrases.Length];
                int id=car.Id;string opponentKey=car.Key;bool stopped=topic=="traffic-catching" && car.SpeedMps<=1;int callEpoch=epoch;
                pending=new ObservedRaceCall {
                    Topic=topic,Text=text,Evidence="practice_quali;continuous_route_positions_1.5s;speed_vs_route_consistent;intercept_within_" +
                        (topic=="traffic-approaching" ? rearAlertSeconds : AlertSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture) + "s;"+
                        (ordinaryClosing ? "ahead_closing_at_least_12mps_in_practice" : topic=="traffic-catching"?"slow_ahead_closing_at_least_8mps":"faster_rear_closing_at_least_3mps"),
                    Member=TeamMember.Ben,Priority=4,Position=-1,SessionId=s.SessionId,SessionType=s.SessionType,
                    // AC 20.11.: „Welches Fahrzeug meinte die GT3-Meldung?“ – Klasse UND Name, wenn der Name erlaubt ist.
                    OpponentNameClip=string.IsNullOrWhiteSpace(car.Name)?null:RaceTrafficWatch.NameClipText(car.Name),
                    DetectedUtc=now,ExpiresUtc=now.AddSeconds(3),
                    TimedValidity=(gs,utc)=>callEpoch==System.Threading.Volatile.Read(ref epoch) && StillRelevant(observer,gs,opponentKey,id,topic,utc,CoveredAhead,rearAlertSeconds) &&
                        (!stopped || gs.OpponentData.ContainsKey(opponentKey) && gs.OpponentData[opponentKey].Speed<=1)
                };
                pendingKey=key;LiveDiagnostics.Decision(topic,"detected",pending.Evidence,text,pending.EventId);return pending;
            }
            return null;
        }
        public static bool StillRelevant(TrackAwareness observer,GameStateData gs,string key,int id,string topic,DateTime now,Func<string,bool> covered=null,double rearAlertSeconds=AlertSeconds)
        {
            var s=observer==null?null:observer.Snapshot;
            if(s==null || gs==null || !Supported(s.SessionType) || now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1) ||
                gs.SessionData.SessionId!=s.SessionId || gs.SessionData.SessionType!=s.SessionType ||
                gs.SessionData.SessionPhase!=SessionPhase.Green || gs.PitData.InPitlane || !s.PlayerSpeedMps.HasValue) return false;
            var car=s.Cars.FirstOrDefault(c=>c.Id==id && c.Key==key);OpponentData live;
            if(car==null || !gs.OpponentData.TryGetValue(key,out live) || live==null || !live.IsActive || live.InPits ||
                RaceReader.Identity("",live)!=car.NativeIdentity) return false;
            bool ahead = topic != "traffic-approaching";
            if(ahead && covered!=null && covered(key))return false;
            return Relevant(car,s.PlayerSpeedMps.Value,ahead,ahead ? AlertSeconds : rearAlertSeconds,topic=="traffic-closing-ahead");
        }
    }
}
