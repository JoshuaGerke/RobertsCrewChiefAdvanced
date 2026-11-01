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
        internal static readonly string[] CatchingPhrases = {
            "Sehr langsamer Wagen vor dir. Du läufst gleich auf ihn auf.",
            "Achtung, langsamer Verkehr direkt voraus. Behalte ihn im Blick.",
            "Vor dir rollt ein Wagen deutlich langsamer. Du bist gleich dran."
        };
        internal static readonly string[] StoppedPhrases = {
            "Achtung, vor dir steht ein Wagen.",
            "Stehendes Fahrzeug voraus. Bleib aufmerksam.",
            "Vorsicht, der Wagen direkt vor dir steht."
        };
        internal static readonly string[] ApproachingPhrases = {
            "Ein deutlich schnellerer Wagen kommt von hinten. Bleib berechenbar.",
            "Schneller Verkehr direkt hinter dir. Behalte deine Linie bei.",
            "Von hinten kommt ein schnellerer Wagen heran. Keine plötzlichen Manöver."
        };
        public const double AlertSeconds=6;
        private readonly Dictionary<string,DateTime> last=new Dictionary<string,DateTime>();
        private DateTime lastAny=DateTime.MinValue, lastTick=DateTime.MinValue;
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
        private static bool Relevant(TrackCarPosition car,double ownSpeed,bool ahead,double upperSeconds)
        {
            if(!Usable(car) || ownSpeed<5 || car.ClosingMps<(ahead?8:3) || !MotionPlausible(car,ownSpeed,ahead) || Math.Abs(car.RouteGapM.Value)<15) return false;
            if(ahead ? car.RouteGapM<=0 : car.RouteGapM>=0) return false;
            double seconds=Math.Abs(car.RouteGapM.Value)/car.ClosingMps.Value;
            if(seconds<.5 || seconds>upperSeconds) return false;
            if(!ahead) return car.SpeedMps>=ownSpeed+3 && car.SpeedMps>=ownSpeed*1.1;
            // A normal braking car or a small pace difference is not slow traffic.
            return car.SpeedMps<=1 || car.SpeedMps<=ownSpeed*.6 &&
                ownSpeed-car.SpeedMps>=8 && (car.OnOutLap || car.OnInLap || car.SpeedMps<=5);
        }
        public ObservedRaceCall Evaluate(TrackAwareness observer,GameStateData state,DateTime now)
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
                else if(!pending.IsValid(state,now)) pending=null;
            }
            // Remove obsolete identities without retaining an unbounded opponent history.
            if(last.Count>512) last.Clear();
            if(!Supported(s.SessionType) || now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1) ||
                state.SessionData.SessionId!=s.SessionId || state.SessionData.SessionType!=s.SessionType ||
                state.SessionData.SessionPhase!=SessionPhase.Green || state.PitData.InPitlane || !s.PlayerSpeedMps.HasValue ||
                pending!=null || now<lastAny || now-lastAny<TimeSpan.FromSeconds(4)) return null;
            var candidates=s.Cars.Where(c=>Relevant(c,s.PlayerSpeedMps.Value,c.RouteGapM>0,AlertSeconds))
                .OrderBy(c=>Math.Abs(c.RouteGapM.Value)/c.ClosingMps.Value);
            foreach(var car in candidates)
            {
                string topic=car.RouteGapM>0?"traffic-catching":"traffic-approaching";
                string key=car.Id+"|"+topic;DateTime previous;
                if(last.TryGetValue(key,out previous) && (now<previous || now-previous<TimeSpan.FromSeconds(90))) continue;
                string[] phrases=topic=="traffic-approaching"?ApproachingPhrases:car.SpeedMps<=1?StoppedPhrases:CatchingPhrases;
                string text=phrases[variant++%phrases.Length];int id=car.Id;string opponentKey=car.Key;bool stopped=topic=="traffic-catching" && car.SpeedMps<=1;int callEpoch=epoch;
                pending=new ObservedRaceCall {
                    Topic=topic,Text=text,Evidence="practice_quali;continuous_route_positions_1.5s;speed_vs_route_consistent;intercept_within_6s;"+(topic=="traffic-catching"?"slow_ahead_closing_at_least_8mps":"faster_rear_closing_at_least_3mps"),
                    Member=TeamMember.Ben,Priority=4,Position=-1,SessionId=s.SessionId,SessionType=s.SessionType,
                    DetectedUtc=now,ExpiresUtc=now.AddSeconds(3),
                    TimedValidity=(gs,utc)=>callEpoch==System.Threading.Volatile.Read(ref epoch) && StillRelevant(observer,gs,opponentKey,id,topic,utc) &&
                        (!stopped || gs.OpponentData.ContainsKey(opponentKey) && gs.OpponentData[opponentKey].Speed<=1)
                };
                pendingKey=key;LiveDiagnostics.Decision(topic,"detected",pending.Evidence,text);return pending;
            }
            return null;
        }
        public static bool StillRelevant(TrackAwareness observer,GameStateData gs,string key,int id,string topic,DateTime now)
        {
            var s=observer==null?null:observer.Snapshot;
            if(s==null || gs==null || !Supported(s.SessionType) || now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1) ||
                gs.SessionData.SessionId!=s.SessionId || gs.SessionData.SessionType!=s.SessionType ||
                gs.SessionData.SessionPhase!=SessionPhase.Green || gs.PitData.InPitlane || !s.PlayerSpeedMps.HasValue) return false;
            var car=s.Cars.FirstOrDefault(c=>c.Id==id && c.Key==key);OpponentData live;
            if(car==null || !gs.OpponentData.TryGetValue(key,out live) || !live.IsActive || live.InPits) return false;
            return Relevant(car,s.PlayerSpeedMps.Value,topic=="traffic-catching",AlertSeconds);
        }
    }
}
