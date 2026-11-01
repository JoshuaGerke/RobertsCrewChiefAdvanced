using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Class intervals from both cars passing the SAME 5 m timing point.
    /// Passage times are interpolated between actual samples, never distance/current speed.
    /// One telemetry-thread writer, immutable published results for UI/audio/tool readers.</summary>
    public sealed class RaceGapTracker
    {
        public const int MaximumPointsPerCar=2400;
        private const double Spacing=5;
        private sealed class History
        {
            public string Identity;
            public double Progress;
            public DateTime Utc;
            public readonly Dictionary<long,long> Points=new Dictionary<long,long>();
            public readonly Queue<long> Order=new Queue<long>();
        }
        private sealed class Snapshot
        {
            public int Session; public string Track; public DateTime Utc;
            public readonly Dictionary<string,RaceGapMeasurement> Values=new Dictionary<string,RaceGapMeasurement>();
            public string AheadKey, BehindKey, AheadIdentity, BehindIdentity;
        }
        private readonly Dictionary<string,History> cars=new Dictionary<string,History>();
        private History own;
        private int session=int.MinValue;
        private string track;
        private double length;
        private DateTime lastTick;
        private Snapshot snapshot;
        public void Reset()
        { cars.Clear(); own=null; session=int.MinValue; track=null; lastTick=DateTime.MinValue; Volatile.Write(ref snapshot,null); }
        private static bool Good(double x) { return !double.IsNaN(x) && !double.IsInfinity(x); }
        private static History Add(History h,string identity,double progress,DateTime now)
        {
            if (h==null || h.Identity!=identity || now<h.Utc || now-h.Utc>TimeSpan.FromSeconds(.75) ||
                progress<h.Progress-.1 || progress-h.Progress>180*(now-h.Utc).TotalSeconds+15)
                h=new History {Identity=identity,Progress=progress,Utc=now};
            double advance=progress-h.Progress;
            // Repeated polls are not new per-car measurements. Keep the last distinct
            // position time so a network packet interpolates over its actual observed interval.
            if (Math.Abs(advance)<.001) return h;
            if (advance>.001 && now>h.Utc)
            {
                long first=(long)Math.Floor(h.Progress/Spacing)+1,last=(long)Math.Floor(progress/Spacing);
                for(long key=first;key<=last;key++)
                {
                    double fraction=(key*Spacing-h.Progress)/advance;
                    h.Points[key]=h.Utc.Ticks+(long)((now.Ticks-h.Utc.Ticks)*fraction); h.Order.Enqueue(key);
                }
            }
            h.Progress=progress; h.Utc=now;
            while (h.Order.Count>0 && (h.Order.Count>MaximumPointsPerCar || now.Ticks-h.Points[h.Order.Peek()]>TimeSpan.FromSeconds(120).Ticks))
                h.Points.Remove(h.Order.Dequeue());
            return h;
        }
        public void Observe(GameStateData gs,DateTime now)
        {
            var s=gs==null?null:gs.SessionData;
            if (s==null || s.SessionType!=SessionType.Race || s.SessionPhase!=SessionPhase.Green || gs.PitData.InPitlane ||
                s.TrackDefinition==null || !Good(s.TrackDefinition.trackLength) || s.TrackDefinition.trackLength<100)
            { Reset(); return; }
            string currentTrack=s.TrackDefinition.name;
            if (s.SessionId!=session || currentTrack!=track || length!=s.TrackDefinition.trackLength || now<lastTick ||
                lastTick!=DateTime.MinValue && now-lastTick>TimeSpan.FromSeconds(.75))
            { Reset(); session=s.SessionId;track=currentTrack;length=s.TrackDefinition.trackLength; }
            if (now==lastTick) return;
            lastTick=now;
            var publish=new Snapshot {Session=session,Track=track,Utc=now};
            double d=gs.PositionAndMotionData.DistanceRoundTrack;
            if (!Good(d) || d<0 || d>length || s.CompletedLaps<0) {Reset();return;}
            own=Add(own,"player",s.CompletedLaps*length+d,now);
            var retained=new HashSet<string>();
            foreach(var pair in gs.OpponentData.Where(p=>p.Value!=null && p.Value.IsActive).Take(TrackAwareness.MaxCars))
            {
                var c=pair.Value;
                if (c.InPits || c.CompletedLaps<0 || !Good(c.DistanceRoundTrack) || c.DistanceRoundTrack<0 || c.DistanceRoundTrack>length)
                {cars.Remove(pair.Key);continue;}
                retained.Add(pair.Key); History h; cars.TryGetValue(pair.Key,out h);
                cars[pair.Key]=Add(h,(c.DriverRawName??"")+"|"+(c.CarNumber??""),c.CompletedLaps*length+c.DistanceRoundTrack,now);
            }
            foreach(string key in cars.Keys.Where(k=>!retained.Contains(k)).ToArray()) cars.Remove(key);
            foreach(bool ahead in new[]{true,false})
            {
                var c=RaceDataTools.Neighbour(gs,ahead); if(c==null)continue;
                var pair=gs.OpponentData.FirstOrDefault(p=>ReferenceEquals(p.Value,c));
                History h;if(pair.Key==null || !cars.TryGetValue(pair.Key,out h))continue;
                double separation=h.Progress-own.Progress;
                if (ahead?separation<=0:separation>=0)continue;
                if(Math.Abs(separation)>=length)continue; // A lap difference is never a seconds interval.
                var follower=ahead?own:h;
                long point=(long)Math.Floor(follower.Progress/Spacing);
                // Only a recent common point; never search arbitrarily far back and call it live.
                for(int back=0;back<3;back++,point--)
                {
                    long a,b;
                    if(!own.Points.TryGetValue(point,out a) || !h.Points.TryGetValue(point,out b))continue;
                    long measured=Math.Max(a,b);
                    if(now.Ticks-measured>TimeSpan.FromSeconds(1).Ticks || measured>now.Ticks)break;
                    double delta=Math.Abs(a-b)/(double)TimeSpan.TicksPerSecond;
                    if(delta>300)break;
                    publish.Values[pair.Key]=new RaceGapMeasurement((float)delta,RaceGapKind.InterpolatedTiming,new DateTime(measured,DateTimeKind.Utc));
                    if(ahead) {publish.AheadKey=pair.Key;publish.AheadIdentity=h.Identity;}
                    else {publish.BehindKey=pair.Key;publish.BehindIdentity=h.Identity;}
                    break;
                }
            }
            Volatile.Write(ref snapshot,publish);
        }
        public RaceGapMeasurement Read(GameStateData gs,OpponentData car,bool ahead,DateTime now)
        {
            var s=Volatile.Read(ref snapshot);
            if(s==null || gs==null || car==null || gs.SessionData.SessionType!=SessionType.Race || gs.SessionData.SessionPhase!=SessionPhase.Green || gs.PitData.InPitlane ||
                gs.SessionData.SessionId!=s.Session || gs.SessionData.TrackDefinition==null || gs.SessionData.TrackDefinition.name!=s.Track ||
                now<s.Utc || now-s.Utc>TimeSpan.FromSeconds(1) || !ReferenceEquals(car,RaceDataTools.Neighbour(gs,ahead)))return new RaceGapMeasurement();
            string key=ahead?s.AheadKey:s.BehindKey; OpponentData live; RaceGapMeasurement measurement;
            if ((ahead?s.AheadIdentity:s.BehindIdentity)!=((car.DriverRawName??"")+"|"+(car.CarNumber??"")))return new RaceGapMeasurement();
            if(key==null || !gs.OpponentData.TryGetValue(key,out live) || !ReferenceEquals(car,live) || !s.Values.TryGetValue(key,out measurement))return new RaceGapMeasurement();
            return now>=measurement.MeasuredUtc && now-measurement.MeasuredUtc<=TimeSpan.FromSeconds(1)?measurement:new RaceGapMeasurement();
        }
    }
}
