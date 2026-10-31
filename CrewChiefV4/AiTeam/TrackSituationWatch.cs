using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Non-safety traffic observations. No side/clear calls, no cloud, no inferred overtakes.</summary>
    public sealed class TrackSituationWatch
    {
        internal static readonly string[] CatchingPhrases = {
            "Du kommst auf langsameren Verkehr auf. Behalte das Auto vor dir im Blick.",
            "Vor dir wird es enger. Du holst auf das Fahrzeug vor dir auf.",
            "Du läufst auf Verkehr auf. Lass dir Platz für deinen Rhythmus."
        };
        internal static readonly string[] ApproachingPhrases = {
            "Von hinten kommt ein schnelleres Auto näher. Bleib berechenbar.",
            "Schnellerer Verkehr nähert sich von hinten. Behalte deinen Rhythmus."
        };
        private readonly Dictionary<string,DateTime> last=new Dictionary<string,DateTime>();
        private DateTime lastAny=DateTime.MinValue;
        private int session=int.MinValue, variant;
        private SessionType type;
        public void Reset() {last.Clear();lastAny=DateTime.MinValue;session=int.MinValue;variant=0;}

        public ObservedRaceCall Evaluate(TrackAwareness observer,GameStateData state,DateTime now)
        {
            var s=observer==null ? null : observer.Snapshot;
            if(state==null || s==null || now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1))return null;
            if(session!=s.SessionId || type!=s.SessionType) {Reset();session=s.SessionId;type=s.SessionType;}
            if(state.SessionData.SessionPhase!=SessionPhase.Green || state.PitData.InPitlane || !s.PlayerSpeedMps.HasValue || s.PlayerSpeedMps<15 ||
                now<lastAny || now-lastAny<TimeSpan.FromSeconds(35))return null;
            // Physical neighbours are deliberately independent of the timing/classification neighbour.
            var ahead=s.Cars.Where(c=>Usable(c) && c.RouteGapM>=45 && c.RouteGapM<=250 && c.ClosingMps>=2 &&
                c.SpeedMps<state.PositionAndMotionData.CarSpeed-1).OrderBy(c=>c.RouteGapM).FirstOrDefault();
            var behind=s.Cars.Where(c=>Usable(c) && c.RouteGapM<= -45 && c.RouteGapM>= -250 && c.ClosingMps>=3 &&
                c.SpeedMps>state.PositionAndMotionData.CarSpeed+1 &&
                (s.SessionType!=SessionType.Race || (c.ClassComparisonKnown && !c.SameClass) || c.LapOffset>0)).OrderByDescending(c=>c.RouteGapM).FirstOrDefault();
            string topic, text; TrackCarPosition car;
            if(ahead!=null && ahead.RouteGapM/ahead.ClosingMps<=25)
            {
                topic="traffic-catching";car=ahead;
                text=CatchingPhrases[variant++%CatchingPhrases.Length];
            }
            else if(behind!=null && -behind.RouteGapM/behind.ClosingMps<=20)
            {
                topic="traffic-approaching";car=behind;
                text=ApproachingPhrases[variant++%ApproachingPhrases.Length];
            }
            else return null;
            DateTime previous;
            if(last.TryGetValue(topic,out previous) && now-previous<TimeSpan.FromSeconds(90))return null;
            last[topic]=lastAny=now;
            int id=car.Id;string key=car.Key;
            var call=new ObservedRaceCall {
                Topic=topic,Text=text,Evidence="same_car_continuous_route_positions_at_least_1.5s; no_pass_or_clear_claim",
                Member=TeamMember.Ben,Priority=3,Position=-1,SessionId=s.SessionId,SessionType=s.SessionType,
                DetectedUtc=now,ExpiresUtc=now.AddSeconds(5),
                AdditionalValidity=gs=>StillRelevant(observer,gs,key,id,topic,DateTime.UtcNow)
            };
            LiveDiagnostics.Decision(topic,"detected",call.Evidence,text);
            return call;
        }
        private static bool Usable(TrackCarPosition c)
        {return !c.InPits && c.SpatiallyConsistent && c.RouteGapM.HasValue && c.SpeedMps.HasValue && c.ClosingMps.HasValue;}
        public static bool StillRelevant(TrackAwareness observer,GameStateData gs,string key,int id,string topic,DateTime now)
        {
            var s=observer==null ? null : observer.Snapshot;
            if(s==null || gs==null || now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1) || gs.SessionData.SessionId!=s.SessionId ||
                gs.SessionData.SessionType!=s.SessionType || gs.PitData.InPitlane)return false;
            var car=s.Cars.FirstOrDefault(c=>c.Id==id && c.Key==key);
            OpponentData live;
            if(car==null || !Usable(car) || !gs.OpponentData.TryGetValue(key,out live) || !live.IsActive || live.InPits)return false;
            return topic=="traffic-catching" ? car.RouteGapM>=25 && car.RouteGapM<=280 && car.ClosingMps>=1 &&
                live.Speed<gs.PositionAndMotionData.CarSpeed-1 :
                car.RouteGapM<= -25 && car.RouteGapM>= -280 && car.ClosingMps>=2 && live.Speed>gs.PositionAndMotionData.CarSpeed+1;
        }
    }
}
