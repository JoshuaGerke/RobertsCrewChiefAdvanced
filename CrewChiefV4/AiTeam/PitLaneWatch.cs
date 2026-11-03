using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Measured speed reminders on pit approach, departure and overspeed.
    /// No guessed limits or assumed speed-marker coordinates.</summary>
    public sealed class PitLaneWatch
    {
        public static readonly string[] UnknownLimitPhrases={
            "Achte auf das ausgeschilderte Boxenlimit.",
            "Denk ans Tempolimit in der Boxengasse.",
            "Boxenlimit beachten. Schau auf die Beschilderung."
        };
        public static bool Known(float value)
        {return !float.IsNaN(value) && !float.IsInfinity(value) && value>0 && value<56;}
        public static IEnumerable<string> Phrases(float limit)
        {
            if(!Known(limit)) return UnknownLimitPhrases;
            string number=Math.Round(limit*3.6f).ToString(CultureInfo.InvariantCulture);
            return new[]{"Achte aufs Boxenlimit: "+number+" Kilometer pro Stunde.",
                "Denk ans Tempo in der Box: "+number+" Kilometer pro Stunde.",
                "Boxenlimit beachten: "+number+" Kilometer pro Stunde."};
        }
        public static string[] SpeedPhrases(float limit)
        {
            if(!Known(limit)) return CrewSpeechPolicy.PitSpeedPhrases;
            string number=Math.Round(limit*3.6f).ToString(CultureInfo.InvariantCulture);
            return new[]{"Zu schnell für die Box. Hier gelten "+number+" Kilometer pro Stunde.",
                "Tempo runter, Boxenlimit: "+number+" Kilometer pro Stunde."};
        }
        public static bool IsPreparedPhrase(string text)
        {
            if(UnknownLimitPhrases.Contains(text)) return true;
            if(text==null || !text.EndsWith(" Kilometer pro Stunde.",StringComparison.Ordinal)) return false;
            int colon=text.LastIndexOf(": "),number;
            if(text.StartsWith("Zu schnell für die Box. Hier gelten ",StringComparison.Ordinal)) colon="Zu schnell für die Box. Hier gelten ".Length-2;
            if(colon<0 || !int.TryParse(text.Substring(colon+2,text.Length-colon-2-" Kilometer pro Stunde.".Length),out number) || number<1 || number>201) return false;
            return Phrases(number/3.6f).Concat(SpeedPhrases(number/3.6f)).Contains(text);
        }
        private int session=int.MinValue,variant,epoch;
        private SessionType type=SessionType.Unavailable;
        private string track;
        private float reportedLimit=-1,lastSpeed,lastSessionSeconds=float.NaN;
        private bool initialized,wasInPits,approachHandled,departureHandled,wasNewSession,lastPitRequest;
        private DateTime speedingSince=DateTime.MinValue,lastWarning=DateTime.MinValue,lastProgress=DateTime.MinValue,lastTick=DateTime.MinValue,lastReminder=DateTime.MinValue;
        private ObservedRaceCall limitCall,speedCall;
        public void Reset()
        {epoch++;session=int.MinValue;type=SessionType.Unavailable;track=null;reportedLimit=-1;lastSpeed=0;initialized=wasInPits=approachHandled=departureHandled=wasNewSession=lastPitRequest=false;
            speedingSince=lastWarning=lastProgress=lastTick=lastReminder=DateTime.MinValue;lastSessionSeconds=float.NaN;limitCall=speedCall=null;}
        private static bool Speeding(GameStateData gs,float limit)
        {return gs!=null && gs.PitData.InPitlane && Known(gs.PitData.PitSpeedLimit) && Math.Abs(gs.PitData.PitSpeedLimit-limit)<.1f && gs.PositionAndMotionData.CarSpeed>limit+.5f && gs.PositionAndMotionData.CarSpeed<=180;}
        public static bool Approaching(GameStateData gs)
        {
            if(gs==null || gs.PitData.InPitlane || gs.PitData.JumpedToPits || (!TrackAwareness.InRange(gs.PositionAndMotionData.CarSpeed,2,180).HasValue)) return false;
            if(gs.PitData.IsApproachingPitlane) return true;
            if(!gs.PitData.HasRequestedPitStop && !gs.PitData.OnInLap) return false;
            var t=gs.SessionData.TrackDefinition;
            if(t==null || t.trackLength<100 || float.IsNaN(t.trackLength) || float.IsInfinity(t.trackLength)) return false;
            // The generic trackLength-300 classification anchor is NOT a pit-speed marker.
            double anchor=t.pitApproachPoint!=null && t.pitApproachPoint.Length>=3?t.pitApproachPoint[0]:t.iracingPitEntranceDistanceRoundTrack;
            var point=TrackAwareness.ValidDistance(anchor,t.trackLength);
            var own=TrackAwareness.ValidDistance(gs.PositionAndMotionData.DistanceRoundTrack,t.trackLength);
            if(!point.HasValue || !own.HasValue) return false;
            double ahead=(point.Value-own.Value+t.trackLength)%t.trackLength;
            double lead=Math.Max(80,Math.Min(650,gs.PositionAndMotionData.CarSpeed*8));
            return ahead<=lead;
        }
        public void Observe(GameStateData gs,DateTime now)
        {
            if(gs==null) return;
            var s=gs.SessionData;string currentTrack=s.TrackDefinition==null?gs.trackName:s.TrackDefinition.name;
            if(session!=s.SessionId || type!=s.SessionType || track!=currentTrack || s.IsNewSession && !wasNewSession || now<lastTick)
            {Reset();session=s.SessionId;type=s.SessionType;track=currentTrack;}
            if(float.IsNaN(lastSessionSeconds) || s.SessionRunningTime>lastSessionSeconds) lastProgress=now;
            lastSessionSeconds=s.SessionRunningTime;lastTick=now;wasNewSession=s.IsNewSession;
            if(limitCall!=null && limitCall.DeliveryStartedUtc!=DateTime.MinValue) lastReminder=limitCall.DeliveryStartedUtc;
            if(now-lastProgress>TimeSpan.FromSeconds(1.5)) {speedingSince=DateTime.MinValue;limitCall=speedCall=null;return;}
            bool inPits=gs.PitData.InPitlane,approach=Approaching(gs);
            float limit=gs.PitData.PitSpeedLimit,speed=gs.PositionAndMotionData.CarSpeed;
            if(gs.PitData.JumpedToPits)
            {
                limitCall=speedCall=null;speedingSince=DateTime.MinValue;
                wasInPits=inPits;lastSpeed=speed;initialized=true;return;
            }
            if(!inPits && !approach)
            {
                approachHandled=false;departureHandled=false;
                if(!gs.PitData.HasRequestedPitStop) {reportedLimit=-1;limitCall=null;}
            }
            bool requestRaised=gs.PitData.HasRequestedPitStop && !lastPitRequest && !inPits && TrackAwareness.InRange(speed,2,180).HasValue;
            lastPitRequest=gs.PitData.HasRequestedPitStop;
            bool changedLimit=Known(limit) && Math.Abs(reportedLimit-limit)>.1f;
            if((requestRaised || gs.PitData.HasRequestedPitStop && changedLimit) && !inPits && !approach && TrackAwareness.InRange(speed,2,180).HasValue)
            {
                reportedLimit=Known(limit)?limit:-1;
                limitCall=Reminder(gs,now,limit,7,true,"pit_request_reminder;speed_marker_position_not_known",true);
            }
            if(approach && (!approachHandled || Known(limit) && Math.Abs(reportedLimit-limit)>.1f))
            {
                approachHandled=true;reportedLimit=Known(limit)?limit:-1;
                if(changedLimit || (limitCall==null || !limitCall.IsValid(gs,now)) &&
                    (lastReminder==DateTime.MinValue || now-lastReminder>=TimeSpan.FromSeconds(20)))
                    limitCall=Reminder(gs,now,limit,7,true,"confirmed_pit_approach_before_lane_entry");
            }
            if(inPits)
            {
                bool entered=initialized && !wasInPits;
                bool leavingBox=speed>=2 && (!initialized || lastSpeed<2) && !departureHandled;
                bool recentlyReminded=lastReminder!=DateTime.MinValue && now-lastReminder<TimeSpan.FromSeconds(20);
                bool newLimit=Known(limit) && Math.Abs(reportedLimit-limit)>.1f;
                // One current reminder covers entry/departure. Never restart it every tick.
                if(newLimit || entered && !recentlyReminded || leavingBox && limitCall==null && !recentlyReminded)
                {
                    if(leavingBox) departureHandled=true;
                    limitCall=Reminder(gs,now,limit,(leavingBox || entered)?7:3,false,leavingBox?"moving_from_pit_box":"measured_pit_lane_limit");
                }
                if(Known(limit)) reportedLimit=limit;
                if(leavingBox) departureHandled=true;
            }
            if(!inPits) {speedingSince=DateTime.MinValue;speedCall=null;}
            else if(!Speeding(gs,limit)) {speedingSince=DateTime.MinValue;speedCall=null;}
            else
            {
                if(speedingSince==DateTime.MinValue) speedingSince=now;
                if(now-speedingSince>=TimeSpan.FromMilliseconds(200) && now-lastWarning>=TimeSpan.FromSeconds(15))
                {
                    lastWarning=now;
                    speedCall=Call(gs,now,"pit-speed",SpeedPhrases(limit)[variant++%SpeedPhrases(limit).Length],7,
                        live=>Speeding(live,limit),"measured_pit_lane_limit_and_speed");
                }
            }
            wasInPits=inPits;lastSpeed=speed;initialized=true;
        }
        private ObservedRaceCall Reminder(GameStateData gs,DateTime now,float limit,int priority,bool approach,string evidence,bool request=false)
        {
            var phrases=Phrases(limit).ToArray();
            return Call(gs,now,"pit-limit",phrases[variant++%phrases.Length],priority,
                live=>(approach?Approaching(live) || request && live.PitData.HasRequestedPitStop && !live.PitData.InPitlane:live.PitData.InPitlane) &&
                    (Known(limit)?Known(live.PitData.PitSpeedLimit) && Math.Abs(live.PitData.PitSpeedLimit-limit)<.1f:!Known(live.PitData.PitSpeedLimit)),evidence);
        }
        private ObservedRaceCall Call(GameStateData gs,DateTime now,string topic,string text,int priority,Func<GameStateData,bool> valid,string evidence)
        {
            int callEpoch=epoch;
            LiveDiagnostics.Decision(topic,"detected",evidence,text);
            return new ObservedRaceCall {Topic=topic,Text=text,Evidence=evidence,Member=TeamMember.Mik,Priority=priority,
                Position=-1,SessionId=gs.SessionData.SessionId,SessionType=gs.SessionData.SessionType,DetectedUtc=now,
                ExpiresUtc=now.AddSeconds(priority>=7?5:10),AllowPitlane=true,AdditionalValidity=valid,
                TimedValidity=(live,utc)=>callEpoch==System.Threading.Volatile.Read(ref epoch) && utc>=lastProgress && utc-lastProgress<=TimeSpan.FromSeconds(1.5)};
        }
        public ObservedRaceCall Take(GameStateData gs,DateTime now,bool busy,int minimumPriority=0)
        {
            if(speedCall!=null && (!speedCall.IsValid(gs,now) || speedCall.DeliveryStartedUtc!=DateTime.MinValue)) speedCall=null;
            if(limitCall!=null && (!limitCall.IsValid(gs,now) || limitCall.DeliveryStartedUtc!=DateTime.MinValue)) limitCall=null;
            if(busy) return null;
            var candidate=speedCall??limitCall;
            return candidate!=null && candidate.Priority>=minimumPriority && candidate.TryReserve(now)?candidate:null;
        }
    }
}
