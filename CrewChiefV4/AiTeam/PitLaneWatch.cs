using System;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Measured pit-speed attention. Does not depend on the muted legacy chief voice.</summary>
    public sealed class PitLaneWatch
    {
        private int session=int.MinValue;
        private SessionType type=SessionType.Unavailable;
        private float reportedLimit=-1;
        private DateTime speedingSince=DateTime.MinValue,lastWarning=DateTime.MinValue;
        private ObservedRaceCall limitCall,speedCall;
        private int variant;
        private float lastSessionSeconds=float.NaN;
        private DateTime lastProgress=DateTime.MinValue;
        public void Reset()
        { session=int.MinValue;type=SessionType.Unavailable;reportedLimit=-1;speedingSince=lastWarning=lastProgress=DateTime.MinValue;lastSessionSeconds=float.NaN;limitCall=speedCall=null; }
        private static bool Known(float value)
        { return !float.IsNaN(value) && !float.IsInfinity(value) && value>0 && value<56; }
        private static bool Speeding(GameStateData gs,float limit)
        { return gs!=null && gs.PitData.InPitlane && Known(gs.PitData.PitSpeedLimit) && Math.Abs(gs.PitData.PitSpeedLimit-limit)<.1f && gs.PositionAndMotionData.CarSpeed>limit+.5f && gs.PositionAndMotionData.CarSpeed<=180; }
        public void Observe(GameStateData gs,DateTime now)
        {
            if(gs==null)return;
            var s=gs.SessionData;
            if(session!=s.SessionId || type!=s.SessionType || s.IsNewSession) { Reset();session=s.SessionId;type=s.SessionType; }
            if(float.IsNaN(lastSessionSeconds) || s.SessionRunningTime>lastSessionSeconds)lastProgress=now;
            lastSessionSeconds=s.SessionRunningTime;
            if(now-lastProgress>TimeSpan.FromSeconds(1.5)) { speedingSince=DateTime.MinValue;speedCall=null;return; }
            float limit=gs.PitData.PitSpeedLimit;
            if(!gs.PitData.InPitlane || !Known(limit)) { reportedLimit=-1;speedingSince=DateTime.MinValue;limitCall=speedCall=null;return; }
            if(Math.Abs(reportedLimit-limit)>.1f)
            {
                reportedLimit=limit;
                limitCall=Call(gs,now,"pit-limit","Boxenlimit: "+Math.Round(limit*3.6f)+" Kilometer pro Stunde.",3,
                    live=>live.PitData.InPitlane && Math.Abs(live.PitData.PitSpeedLimit-limit)<.1f);
            }
            if(!Speeding(gs,limit)) { speedingSince=DateTime.MinValue;speedCall=null;return; }
            if(speedingSince==DateTime.MinValue)speedingSince=now;
            if(now-speedingSince>=TimeSpan.FromMilliseconds(200) && now-lastWarning>=TimeSpan.FromSeconds(15))
            {
                lastWarning=now;
                speedCall=Call(gs,now,"pit-speed",CrewSpeechPolicy.PitSpeedPhrases[variant++%CrewSpeechPolicy.PitSpeedPhrases.Length],7,
                    live=>Speeding(live,limit));
            }
        }
        private static ObservedRaceCall Call(GameStateData gs,DateTime now,string topic,string text,int priority,Func<GameStateData,bool> valid)
        {
            LiveDiagnostics.Decision(topic,"detected","measured_pit_lane_limit_and_speed",text);
            return new ObservedRaceCall { Topic=topic,Text=text,Evidence="measured_pit_lane_limit_and_speed",Member=TeamMember.Mik,Priority=priority,
                Position=-1,SessionId=gs.SessionData.SessionId,SessionType=gs.SessionData.SessionType,DetectedUtc=now,
                ExpiresUtc=now.AddSeconds(priority>=7?5:10),AllowPitlane=true,AdditionalValidity=valid };
        }
        public ObservedRaceCall Take(GameStateData gs,DateTime now,bool busy,int minimumPriority=0)
        {
            if(speedCall!=null && (!speedCall.IsValid(gs,now) || speedCall.DeliveryStartedUtc!=DateTime.MinValue))speedCall=null;
            if(limitCall!=null && (!limitCall.IsValid(gs,now) || limitCall.DeliveryStartedUtc!=DateTime.MinValue))limitCall=null;
            if(busy)return null;
            var candidate=speedCall??limitCall;
            return candidate!=null && candidate.Priority>=minimumPriority && candidate.TryReserve(now)?candidate:null;
        }
    }
}
