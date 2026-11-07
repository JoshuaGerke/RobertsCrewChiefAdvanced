using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    // A bounded, non-blocking hand-off BEFORE native voice suppression. No audio/network work here.
    public static class NativeTrafficBridge
    {
        public sealed class Detection
        {
            public int SessionId;
            public SessionType Type;
            public DateTime Utc;
            public bool Behind;
            public string[] Names;
        }
        private static readonly ConcurrentQueue<Detection> queue = new ConcurrentQueue<Detection>();
        public static volatile bool Enabled;
        public static void Publish(GameStateData gs, IEnumerable<string> names, bool behind)
        {
            if (!Enabled || gs == null || gs.SessionData.SessionType != SessionType.Race || names == null) return;
            var copy = names.Where(n=>!string.IsNullOrWhiteSpace(n) && n.Length<=128).Take(16).ToArray();
            if (copy.Length == 0) return;
            queue.Enqueue(new Detection { SessionId=gs.SessionData.SessionId, Type=gs.SessionData.SessionType,
                Utc=DateTime.UtcNow, Behind=behind, Names=copy });
            while (queue.Count > 32) { Detection unused; queue.TryDequeue(out unused); }
        }
        public static bool TryTake(out Detection detection) { return queue.TryDequeue(out detection); }
        public static void Clear() { Detection unused; while (queue.TryDequeue(out unused)) { } }
    }

    /// <summary>Local race traffic, independent of LLMs and optional reporting timers.
    /// Own previously observed speed at the SAME route section distinguishes slow cars from normal corner braking.
    /// Side/clear calls remain exclusively with the existing Sara spotter.</summary>
    public sealed class RaceTrafficWatch
    {
        public static readonly string[] StoppedPhrases = {
            "Vorsicht, stehendes Fahrzeug voraus.", "Achtung, vor dir steht ein Wagen.", "Stehender Wagen voraus, bleib aufmerksam."
        };
        public static readonly string[] SlowPhrases = {
            "Achtung, sehr langsames Fahrzeug voraus.", "Vor dir fährt ein Wagen deutlich langsamer. Bleib aufmerksam.",
            "Vorsicht, langsamer Verkehr direkt voraus."
        };
        public static readonly string[] RearPhrases = {
            "Schnelleres Fahrzeug von hinten. Bleib berechenbar.", "Von hinten kommt schneller Verkehr. Behalte deine Linie bei.",
            "Ein schnellerer Wagen läuft von hinten auf. Bleib berechenbar."
        };
        public static readonly string[] RearGroupPhrases = {
            "Mehrere schnellere Fahrzeuge kommen von hinten. Bleib berechenbar.",
            "Schnellere Gruppe von hinten. Behalte deine Linie bei."
        };
        public static string DistancePhrase(int metres,bool stopped) => "Vorsicht, in etwa "+metres+" Metern "+
            (stopped?"steht ein Fahrzeug.":"ein sehr langsames Fahrzeug.");
        public static readonly string[] DistancePhrases=Enumerable.Range(1,12).SelectMany(i=>new[]{DistancePhrase(i*20,true),DistancePhrase(i*20,false)}).ToArray();
        public static IEnumerable<string> PreparedPhrases { get { return DistancePhrases.Concat(StoppedPhrases).Concat(SlowPhrases).Concat(RearPhrases).Concat(RearGroupPhrases); } }
        private sealed class Reference { public float Speed; public int Lap; public DateTime Utc; }
        private sealed class Facts { public DateTime Utc; public int Epoch, Session; public TrackCarPosition[] Cars; }
        private Facts facts;
        private readonly Dictionary<int,Reference> reference = new Dictionary<int,Reference>();
        private readonly Dictionary<string,DateTime> reported = new Dictionary<string,DateTime>();
        private readonly Dictionary<string,DateTime> nativeRear = new Dictionary<string,DateTime>();
        private TrackAwareness tracks;
        private ObservedRaceCall pending;
        private string pendingKey, track;
        private int session=int.MinValue, variant, epoch;
        private DateTime lastTick, lastRadio;
        public void Reset()
        {
            reference.Clear(); reported.Clear(); nativeRear.Clear(); pending=null; tracks=null;
            session=int.MinValue; track=null; lastTick=lastRadio=DateTime.MinValue; epoch++;
            Volatile.Write(ref facts,null);
        }
        public void Observe(TrackAwareness observer, GameStateData gs, DateTime now)
        {
            if (gs == null || observer == null || observer.Snapshot == null) return;
            var s=observer.Snapshot;
            if (session!=s.SessionId || track!=s.Track || now<lastTick ||
                lastTick!=DateTime.MinValue && now-lastTick>TimeSpan.FromSeconds(2))
            { Reset(); session=s.SessionId; track=s.Track; }
            tracks=observer; lastTick=now;
            if (pending!=null && pending.DeliveryStartedUtc!=DateTime.MinValue)
            { reported[pendingKey]=lastRadio=pending.DeliveryStartedUtc; pending=null; }
            if (pending!=null && !pending.IsValid(gs,now)) pending=null;
            NativeTrafficBridge.Detection d;
            while (NativeTrafficBridge.TryTake(out d))
                if (d.SessionId==s.SessionId && d.Type==s.SessionType && d.Behind && now>=d.Utc && now-d.Utc<TimeSpan.FromSeconds(3))
                    foreach (string name in d.Names) nativeRear[name]=d.Utc;
            foreach (string name in nativeRear.Where(p=>now-p.Value>TimeSpan.FromSeconds(3)).Select(p=>p.Key).ToArray()) nativeRear.Remove(name);
            if (s.SessionType!=SessionType.Race || gs.SessionData.SessionPhase!=SessionPhase.Green ||
                gs.PitData.InPitlane || !s.PlayerSpeedMps.HasValue || now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1))
            { Volatile.Write(ref facts,null); return; }
            if (s.PlayerDistanceM.HasValue && s.PlayerSpeedMps>=5 && !gs.PenaltiesData.IsOffRacingSurface)
            {
                int cell=(int)(s.PlayerDistanceM.Value/100);
                Reference old;
                // Retain the last lap, not a same-tick reference which merely duplicates the player speed.
                if (!reference.TryGetValue(cell,out old) || old.Lap<gs.SessionData.CompletedLaps)
                    reference[cell]=new Reference {Speed=(float)s.PlayerSpeedMps.Value,Lap=gs.SessionData.CompletedLaps,Utc=now};
                if (reference.Count>2000) reference.Clear();
            }
            var candidates=s.Cars.Where(c=>Relevant(c,gs,s,now)).OrderBy(c=>c.RouteGapM>0?0:1)
                .ThenBy(c=>Math.Abs(c.RouteGapM.Value)/Math.Max(1,c.ClosingMps??1)).ToArray();
            Volatile.Write(ref facts,new Facts {Utc=now,Epoch=epoch,Session=s.SessionId,Cars=candidates});
            if (pending!=null || now-lastRadio<TimeSpan.FromSeconds(3)) return;
            foreach (var car in candidates)
            {
                bool rear=car.RouteGapM<0, stopped=!rear && car.SpeedMps<=1;
                string key=car.Id+"|"+(rear?"rear":"hazard"); DateTime previous;
                if (reported.TryGetValue(key,out previous) && now-previous<TimeSpan.FromSeconds(60)) continue;
                bool group=rear && candidates.Count(c=>c.RouteGapM<0)>1;
                var phrases=rear?(group?RearGroupPhrases:RearPhrases):stopped?StoppedPhrases:SlowPhrases;
                int id=car.Id, generation=epoch; string opponentKey=car.Key;
                pending=new ObservedRaceCall {
                    Topic=rear?"race-traffic-rear":"race-hazard-ahead", Text=phrases[variant++%phrases.Length],
                    Member=TeamMember.Ben, Priority=7, Position=-1, SessionId=s.SessionId, SessionType=s.SessionType,
                    DetectedUtc=now, ExpiresUtc=now.AddSeconds(3),
                    Evidence="local_route_motion;opponent="+opponentKey+";source="+(NativeConfirmed(car,now)?"native_multiclass_and_positions":"measured_positions"),
                    TimedValidity=(live,utc)=>generation==epoch && ValidNow(live,utc,opponentKey,id,stopped,group)
                };
                if(!rear) pending.PreparedDetail=(live,at)=>generation==epoch?DistanceDetail(live,at,opponentKey,id,stopped):null;
                pendingKey=key; LiveDiagnostics.Decision(pending.Topic,"detected",pending.Evidence,pending.Text); break;
            }
            if (reported.Count>512) reported.Clear();
        }
        private PreparedRadioDetail DistanceDetail(GameStateData gs,DateTime now,string key,int id,bool stopped)
        {
            if(!ValidNow(gs,now,key,id,stopped,false))return null;
            var sample=Volatile.Read(ref facts);
            if(sample==null || now<sample.Utc || now-sample.Utc>TimeSpan.FromMilliseconds(500))return null;
            var car=sample.Cars.FirstOrDefault(c=>c.Id==id && c.Key==key);
            if(car==null || !car.RouteGapM.HasValue || car.RouteGapM<40 || car.RouteGapM>240)return null;
            double measured=car.RouteGapM.Value;
            int rounded=(int)Math.Round(measured/20,MidpointRounding.AwayFromZero)*20;
            return new PreparedRadioDetail {Text=DistancePhrase(rounded,stopped),StillCurrent=(live,at)=>{
                if(!ValidNow(live,at,key,id,stopped,false))return false;
                var fresh=Volatile.Read(ref facts);
                if(fresh==null || at<fresh.Utc || at-fresh.Utc>TimeSpan.FromMilliseconds(500))return false;
                var same=fresh.Cars.FirstOrDefault(c=>c.Id==id && c.Key==key);
                return same!=null && same.RouteGapM.HasValue && Math.Abs(same.RouteGapM.Value-measured)<=15;
            }};
        }
        private bool NativeConfirmed(TrackCarPosition car,DateTime now)
        {
            DateTime utc;
            return (nativeRear.TryGetValue(car.Key,out utc) || car.NativeName!=null && nativeRear.TryGetValue(car.NativeName,out utc)) &&
                now>=utc && now-utc<TimeSpan.FromSeconds(3);
        }
        public Func<string,bool> CoveredAhead;
        private bool Relevant(TrackCarPosition c,GameStateData gs,TrackPositionSnapshot s,DateTime now)
        {
            if (c.InPits || !c.SpatiallyConsistent || !c.RouteGapM.HasValue || !c.SpeedMps.HasValue ||
                !s.PlayerSpeedMps.HasValue || s.PlayerSpeedMps<5 || Math.Abs(c.RouteGapM.Value)<15) return false;
            bool rear=c.RouteGapM<0;
            if(!rear && CoveredAhead!=null && CoveredAhead(c.Key))return false;
            if (rear && NativeConfirmed(c,now) && c.SpeedMps>=s.PlayerSpeedMps+3 && Math.Abs(c.RouteGapM.Value)<=500) return true;
            if (!c.ClosingMps.HasValue || !TrackSituationWatch.MotionPlausible(c,s.PlayerSpeedMps.Value,!rear)) return false;
            double closing=c.ClosingMps.Value;
            if (closing<(rear?3:8)) return false;
            double intercept=Math.Abs(c.RouteGapM.Value)/closing;
            if (intercept<.5 || intercept>(rear?10:7)) return false;
            if (rear) return c.ClassComparisonKnown && (!c.SameClass || c.LapOffset>0) && c.SpeedMps>s.PlayerSpeedMps*1.08;
            if (c.SpeedMps<=5) return true;
            Reference normal;
            return c.LapDistanceM.HasValue && reference.TryGetValue((int)(c.LapDistanceM.Value/100),out normal) &&
                normal.Lap<gs.SessionData.CompletedLaps && now>=normal.Utc && now-normal.Utc<TimeSpan.FromMinutes(10) &&
                normal.Speed>=15 && c.SpeedMps<normal.Speed*.45 && c.SpeedMps<s.PlayerSpeedMps*.6;
        }
        private bool ValidNow(GameStateData gs,DateTime now,string key,int id,bool stopped,bool group)
        {
            var s=tracks==null?null:tracks.Snapshot;
            var current=Volatile.Read(ref facts);
            if (gs==null || s==null || s.SessionId!=gs.SessionData.SessionId || s.SessionType!=SessionType.Race ||
                gs.SessionData.SessionType!=SessionType.Race || gs.SessionData.SessionPhase!=SessionPhase.Green ||
                gs.PitData.InPitlane || now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1) || current==null ||
                current.Epoch!=epoch || current.Session!=s.SessionId || now<current.Utc || now-current.Utc>TimeSpan.FromSeconds(1)) return false;
            var car=current.Cars.FirstOrDefault(c=>c.Id==id && c.Key==key);
            OpponentData live;
            return car!=null && gs.OpponentData.TryGetValue(key,out live) && live.IsActive && !live.InPits && live.DriverRawName==car.NativeName && live.CarNumber==car.NativeNumber && live.CostId==car.NativeCarId &&
                (!stopped || live.Speed<=1 && car.SpeedMps<=1) && (!group || current.Cars.Count(c=>c.RouteGapM<0)>1);
        }
        public ObservedRaceCall Take(GameStateData gs,DateTime now,bool busy,bool memberEnabled)
        {
            if (pending==null || busy || !memberEnabled || !pending.IsValid(gs,now)) return null;
            return pending.TryReserve(now)?pending:null;
        }
    }
}
