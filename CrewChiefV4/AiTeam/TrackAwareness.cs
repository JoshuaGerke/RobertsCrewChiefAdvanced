using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Copied values only: publishing a snapshot never shares mutable GameState/coordinate arrays.</summary>
    public sealed class TrackCarPosition
    {
        public readonly int Id, OverallPosition, ClassPosition;
        public readonly string Key, Name;
        public readonly bool InPits, SameClass, ClassComparisonKnown, SpatiallyConsistent;
        public readonly double? X, Z, LapDistanceM, RouteGapM, WorldDistanceM, SpeedMps, ClosingMps;
        public readonly int? LapOffset;
        internal TrackCarPosition(int id, string key, OpponentData car, GameStateData own, double? x, double? z,
            double? distance, double? gap, double? worldDistance, double? closing, int? lapOffset, bool consistent)
        {
            Id=id; Key=key; Name=car.CanUseName ? SafeName(car.DriverRawName) : null;
            OverallPosition=car.OverallPosition; ClassPosition=car.ClassPosition; InPits=car.InPits;
            ClassComparisonKnown=own.carClass!=null && own.carClass.carClassEnum!=CarData.CarClassEnum.UNKNOWN_RACE &&
                own.carClass.carClassEnum!=CarData.CarClassEnum.USER_CREATED && car.CarClass.carClassEnum!=CarData.CarClassEnum.UNKNOWN_RACE &&
                car.CarClass.carClassEnum!=CarData.CarClassEnum.USER_CREATED;
            SameClass=ClassComparisonKnown && CarData.IsCarClassEqual(own.carClass,car.CarClass,false);
            X=x; Z=z; LapDistanceM=distance; RouteGapM=gap; WorldDistanceM=worldDistance;
            SpeedMps=TrackAwareness.InRange(car.Speed,0,180); ClosingMps=closing; LapOffset=lapOffset;
            SpatiallyConsistent=consistent;
        }
        private static string SafeName(string name)
        { return name==null ? null : new string(name.Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='-'||c=='\'').Take(60).ToArray()); }
    }

    public sealed class TrackPositionSnapshot
    {
        public readonly DateTime Utc;
        public readonly int SessionId, MappedCars;
        public readonly SessionType SessionType;
        public readonly string Track, CurrentSection, NextSection;
        public readonly double? LengthM, PlayerX, PlayerZ, PlayerDistanceM, PlayerSpeedMps, ToNextSectionM;
        public readonly bool PlayerInPits, IsNewSession;
        public readonly ReadOnlyCollection<TrackCarPosition> Cars;
        internal TrackPositionSnapshot(GameStateData state, DateTime utc, List<TrackCarPosition> cars)
        {
            Utc=utc; SessionId=state.SessionData.SessionId; SessionType=state.SessionData.SessionType;
            IsNewSession=state.SessionData.IsNewSession;
            var track=state.SessionData.TrackDefinition;
            Track=track==null ? state.trackName : track.name;
            LengthM=track==null ? null : TrackAwareness.InRange(track.trackLength,100,100000);
            PlayerDistanceM=TrackAwareness.ValidDistance(state.PositionAndMotionData.DistanceRoundTrack,LengthM);
            PlayerSpeedMps=TrackAwareness.InRange(state.PositionAndMotionData.CarSpeed,0,180);
            PlayerInPits=state.PitData.InPitlane;
            double? x,z; TrackAwareness.Horizontal(state.PositionAndMotionData.WorldPosition,out x,out z); PlayerX=x; PlayerZ=z;
            Cars=cars.AsReadOnly(); MappedCars=state.OpponentData.Count;
            double best=double.MaxValue;
            if(LengthM.HasValue && PlayerDistanceM.HasValue && track.trackLandmarks!=null)
                foreach(var section in track.trackLandmarks.Take(512))
                {
                    if(section.isStraight || section.landmarkNames==null || section.landmarkNames.Count==0 ||
                        string.IsNullOrWhiteSpace(section.landmarkNames[0])) continue;
                    var start=TrackAwareness.ValidDistance(section.distanceRoundLapStart,LengthM);
                    var end=TrackAwareness.ValidDistance(section.distanceRoundLapEnd,LengthM);
                    if(!start.HasValue || !end.HasValue || start==end) continue;
                    string name=CornerCoach.FriendlyName(section.landmarkNames[0],0);
                    bool inside=start<end ? PlayerDistanceM>=start && PlayerDistanceM<end : PlayerDistanceM>=start || PlayerDistanceM<end;
                    if(inside) CurrentSection=name;
                    double ahead=(start.Value-PlayerDistanceM.Value+LengthM.Value)%LengthM.Value;
                    if(ahead<best) {best=ahead;NextSection=name;ToNextSectionM=ahead;}
                }
        }
        public bool IsCurrent(RaceDataContext context)
        {
            if(context==null || context.GameState==null || context.NowUtc<Utc || (context.NowUtc-Utc).TotalSeconds>1 ||
                context.TelemetryAgeSeconds<0 || context.TelemetryAgeSeconds>1) return false;
            var s=context.GameState.SessionData;
            string track=s.TrackDefinition==null ? context.GameState.trackName : s.TrackDefinition.name;
            double? length=s.TrackDefinition==null ? null : TrackAwareness.InRange(s.TrackDefinition.trackLength,100,100000);
            return s.SessionId==SessionId && s.SessionType==SessionType && track==Track && length==LengthM;
        }
    }

    /// <summary>20 Hz maximum, bounded history, no I/O/JSON/network on the observation path.
    /// Route distance and horizontal world distance are distinct measurements, never timing gaps.</summary>
    public sealed class TrackAwareness
    {
        public const int MaxCars=256, MaxSamples=42;
        private sealed class Sample { public DateTime Utc; public double Gap; }
        private sealed class History
        {
            public int Id; public string Identity; public double? LastDistance;
            public readonly Queue<Sample> Samples=new Queue<Sample>();
        }
        private readonly Dictionary<string,History> history=new Dictionary<string,History>();
        private TrackPositionSnapshot latest;
        private int nextId;
        public TrackPositionSnapshot Snapshot { get { return Volatile.Read(ref latest); } }
        public void Reset() { history.Clear(); nextId=0; Volatile.Write(ref latest,null); }

        public bool Observe(GameStateData state, DateTime now)
        {
            if(state==null) {Reset();return false;}
            var prior=Snapshot; var session=state.SessionData;
            string track=session.TrackDefinition==null ? state.trackName : session.TrackDefinition.name;
            var currentLength=session.TrackDefinition==null ? null : InRange(session.TrackDefinition.trackLength,100,100000);
            if(prior!=null && (prior.SessionId!=session.SessionId || prior.SessionType!=session.SessionType || prior.Track!=track ||
                prior.LengthM!=currentLength || (session.IsNewSession && !prior.IsNewSession) || now<prior.Utc)) Reset();
            prior=Snapshot;
            if(prior!=null && now-prior.Utc<TimeSpan.FromMilliseconds(50))return false;
            if(prior!=null && now-prior.Utc>TimeSpan.FromSeconds(1))history.Clear();
            var length=session.TrackDefinition==null ? null : InRange(session.TrackDefinition.trackLength,100,100000);
            var ownDistance=ValidDistance(state.PositionAndMotionData.DistanceRoundTrack,length);
            double? ownX,ownZ;Horizontal(state.PositionAndMotionData.WorldPosition,out ownX,out ownZ);
            bool ownJump=prior!=null && prior.PlayerDistanceM.HasValue && ownDistance.HasValue && length.HasValue &&
                Math.Abs(Wrap(ownDistance.Value-prior.PlayerDistanceM.Value,length.Value))>180*(now-prior.Utc).TotalSeconds+15;
            if(ownJump)history.Clear();
            var cars=new List<TrackCarPosition>();var retained=new HashSet<string>();
            foreach(var pair in state.OpponentData.Where(p=>p.Value!=null && p.Value.IsActive).Take(MaxCars))
            {
                var car=pair.Value; if(car==null || !car.IsActive)continue;
                retained.Add(pair.Key);
                var distance=ValidDistance(car.DistanceRoundTrack,length);
                double? gap=distance.HasValue && ownDistance.HasValue ? (double?)Wrap(distance.Value-ownDistance.Value,length.Value) : null;
                double? x,z;Horizontal(car.WorldPosition,out x,out z);
                double? worldDistance=x.HasValue && ownX.HasValue && !(x==0 && z==0 && ownX==0 && ownZ==0)
                    ? (double?)Math.Sqrt(Math.Pow(x.Value-ownX.Value,2)+Math.Pow(z.Value-ownZ.Value,2)) : null;
                bool consistent=gap.HasValue && (!worldDistance.HasValue || worldDistance<=Math.Abs(gap.Value)+20);
                string identity=(car.DriverRawName??"")+"|"+(car.CarNumber??"");
                History h;
                if(!history.TryGetValue(pair.Key,out h) || h.Identity!=identity)
                    history[pair.Key]=h=new History{Id=++nextId,Identity=identity};
                double? closing=null;
                if(gap.HasValue && consistent && !car.InPits && !state.PitData.InPitlane && !ownJump)
                {
                    var previous=h.Samples.Count==0 ? null : h.Samples.Last();
                    double seconds=previous==null ? 0 : (now-previous.Utc).TotalSeconds;
                    bool jumped=h.LastDistance.HasValue && distance.HasValue && length.HasValue &&
                        Math.Abs(Wrap(distance.Value-h.LastDistance.Value,length.Value))>180*seconds+15;
                    if(previous!=null && (seconds<=0 || seconds>.75 || jumped || Math.Abs(gap.Value-previous.Gap)>180*seconds+15))h.Samples.Clear();
                    h.Samples.Enqueue(new Sample{Utc=now,Gap=gap.Value});
                    while(h.Samples.Count>MaxSamples || (h.Samples.Count>1 && now-h.Samples.Peek().Utc>TimeSpan.FromSeconds(3)))h.Samples.Dequeue();
                    var first=h.Samples.Peek(); double span=(now-first.Utc).TotalSeconds;
                    if(h.Samples.Count>=4 && span>=1.5 && Math.Sign(first.Gap)==Math.Sign(gap.Value))
                    {
                        // Positive means the separation is shrinking, both ahead and behind.
                        double meanTime=h.Samples.Average(p=>(p.Utc-first.Utc).TotalSeconds);
                        double meanGap=h.Samples.Average(p=>Math.Abs(p.Gap));
                        double covariance=0,variance=0;
                        foreach(var sample in h.Samples)
                        {
                            double t=(sample.Utc-first.Utc).TotalSeconds-meanTime;
                            covariance+=t*(Math.Abs(sample.Gap)-meanGap);variance+=t*t;
                        }
                        double slope=covariance/variance;
                        double residual=Math.Sqrt(h.Samples.Average(p=>Math.Pow(Math.Abs(p.Gap)-meanGap-slope*((p.Utc-first.Utc).TotalSeconds-meanTime),2)));
                        if(Math.Abs(slope)<=90 && residual<=3)closing=-slope;
                    }
                }
                else h.Samples.Clear();
                h.LastDistance=distance;
                int? lapOffset=null;
                if(gap.HasValue && session.CompletedLaps>=0 && car.CompletedLaps>=0 && session.SessionType==SessionType.Race)
                    lapOffset=(int)Math.Round(((car.CompletedLaps-session.CompletedLaps)*length.Value+distance.Value-ownDistance.Value-gap.Value)/length.Value);
                cars.Add(new TrackCarPosition(h.Id,pair.Key,car,state,x,z,distance,gap,worldDistance,closing,lapOffset,consistent));
            }
            foreach(string key in history.Keys.Where(k=>!retained.Contains(k)).ToArray())history.Remove(key);
            Volatile.Write(ref latest,new TrackPositionSnapshot(state,now,cars));
            return true;
        }

        internal static double? InRange(double n,double min,double max)
        { return double.IsNaN(n)||double.IsInfinity(n)||n<min||n>max ? null : (double?)n; }
        internal static double? ValidDistance(double distance,double? length)
        { return !length.HasValue ? null : InRange(distance,0,length.Value); }
        internal static double Wrap(double delta,double length)
        { if(delta>length/2)delta-=length;else if(delta< -length/2)delta+=length;return delta; }
        internal static void Horizontal(float[] point,out double? x,out double? z)
        {
            x=z=null; if(point==null || (point.Length!=2 && point.Length!=3))return;
            var px=InRange(point[0],-1000000,1000000);var pz=InRange(point[point.Length==3 ? 2 : 1],-1000000,1000000);
            if(px.HasValue && pz.HasValue){x=px;z=pz;}
        }

        public static JObject Player(GameStateData state)
        {
            if(state==null)return new JObject{["verfuegbar"]=false};
            var s=new TrackPositionSnapshot(state,DateTime.MinValue,new List<TrackCarPosition>());
            return Player(s,state.PitData.OnOutLap);
        }
        public static JObject OpponentPosition(GameStateData own,OpponentData car)
        {
            var track=own.SessionData.TrackDefinition;
            double? length=track==null ? null : InRange(track.trackLength,100,100000);
            double? distance=ValidDistance(car.DistanceRoundTrack,length);
            double? x,z;Horizontal(car.WorldPosition,out x,out z);
            return new JObject {
                ["meter_ab_start_ziel"]=Number(distance),["welt_x_m"]=Number(x),["welt_z_m"]=Number(z),
                ["quelle"]="Spieladapter, Momentaufnahme in Metern; keine Zeitabstands- oder Freigabemessung"
            };
        }
        private static JToken Number(double? value,int digits=1) {return value.HasValue ? (JToken)Math.Round(value.Value,digits) : JValue.CreateNull();}
        private static JObject Player(TrackPositionSnapshot s,bool outLap)
        {
            return new JObject {
                ["verfuegbar"]=s.PlayerDistanceM.HasValue || s.PlayerX.HasValue,
                ["quelle"]="gemappte Spielposition; Snapshot, keine Vorhersage",
                ["einheit"]="Meter",["achsen"]="Weltebene X/Z; kein Links-/Rechtsnachweis",
                ["streckenlaenge_m"]=Number(s.LengthM),["meter_ab_start_ziel"]=Number(s.PlayerDistanceM),
                ["meter_bis_start_ziel"]=Number(s.LengthM.HasValue && s.PlayerDistanceM.HasValue ? s.LengthM-s.PlayerDistanceM : null),
                ["rundenanteil"]=Number(s.LengthM.HasValue && s.PlayerDistanceM.HasValue ? s.PlayerDistanceM/s.LengthM : null,4),
                ["welt_x_m"]=Number(s.PlayerX),["welt_z_m"]=Number(s.PlayerZ),["outlap"]=outLap,
                ["abschnitt"]=s.CurrentSection,["naechster_abschnitt"]=s.NextSection,["bis_naechster_abschnitt_m"]=Number(s.ToNextSectionM)
            };
        }
        public static JObject Read(RaceDataContext context)
        {
            var s=context==null || context.Tracks==null ? null : context.Tracks.Snapshot;
            if(s==null || !s.IsCurrent(context))return new JObject{["verfuegbar"]=false,["grund"]="Keine frische räumliche Beobachtung; keine freie Strecke behaupten."};
            var ordered=s.Cars.OrderBy(c=>c.RouteGapM.HasValue && c.SpatiallyConsistent ? Math.Abs(c.RouteGapM.Value) : double.MaxValue).Take(12);
            return new JObject {
                ["verfuegbar"]=true,["snapshot_utc"]=s.Utc.ToString("o"),["datenalter_s"]=Math.Round((context.NowUtc-s.Utc).TotalSeconds,3),
                ["quelle"]="Crew-Chief-GameState / "+context.GameName,["eigene_position"]=Player(s,context.GameState.PitData.OnOutLap),
                ["erfasste_gegner"]=s.MappedCars,["beobachtete_gegner"]=s.Cars.Count,["ausgabe_begrenzt_auf"]=12,
                ["hinweis"]="Physische Streckenlage enthält auch andere Klassen und Überrundungen. Positiver Streckenversatz bedeutet voraus, negativer dahinter (kürzester Rundenteil). Meterabstand entlang der Runde ist kein Zeitabstand, kein Beweis für ein Überholmanöver und keine Freigabe. Weltpositionen belegen weder Grip noch Gegnerreifen oder Kurvenvorteile.",
                ["fahrzeuge"]=new JArray(ordered.Select(c=>new JObject {
                    ["id"]=c.Id,["name"]=context.SendDriverNames ? c.Name : null,["position_gesamt"]=c.OverallPosition>0 ? (JToken)c.OverallPosition : null,
                    ["position_klasse"]=c.ClassPosition>0 ? (JToken)c.ClassPosition : null,
                    ["gleiche_klasse"]=c.ClassComparisonKnown ? (JToken)c.SameClass : null,["in_boxengasse"]=c.InPits,
                    ["welt_x_m"]=Number(c.X),["welt_z_m"]=Number(c.Z),["meter_ab_start_ziel"]=Number(c.LapDistanceM),
                    ["versatz_entlang_strecke_m"]=Number(c.SpatiallyConsistent ? c.RouteGapM : null),["luftlinie_horizontal_m"]=Number(c.WorldDistanceM),
                    ["rundenversatz"]=c.LapOffset.HasValue ? (JToken)c.LapOffset.Value : null,["tempo_m_s"]=Number(c.SpeedMps),
                    ["annaeherung_m_s"]=Number(c.ClosingMps),["positionsdaten_konsistent"]=c.SpatiallyConsistent
                }))
            };
        }
    }
}
