using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Copies an existing native stopped-in-landmark decision; does not infer a collision.</summary>
    public sealed class NativeIncidentWatch
    {
        private sealed class Subject {public string Key,Name;}
        private sealed class Detection {public int Session;public SessionType Type;public DateTime Utc;public string Corner;public Subject[] Cars;}
        private static readonly string[] PhrasesDe={"Vorsicht, Vorfall voraus. Dort steht oder rollt ein Wagen.",
            "Sehr langsames Fahrzeug beim Vorfall voraus. Bleib aufmerksam.","Achtung, stehender oder sehr langsamer Wagen voraus."};
        private static readonly string[] PhrasesEn={"Careful, incident ahead. There's a car stopped or rolling there.",
            "Very slow car at the incident ahead. Stay alert.","Watch out, stopped or very slow car ahead."};
        private static readonly string[] PhrasesEs={"Cuidado, incidente delante. Hay un coche parado o rodando despacio.",
            "Vehículo muy lento en el incidente de delante. Mantente atento.","Atención, coche parado o muy lento delante."};
        public static string[] Phrases => RadioLanguage.Pick(PhrasesDe,PhrasesEn,PhrasesEs);
        private static readonly ConcurrentQueue<Detection> incoming=new ConcurrentQueue<Detection>();
        public static volatile bool Enabled;
        private readonly List<Detection> active=new List<Detection>();
        private readonly Dictionary<string,DateTime> recorded=new Dictionary<string,DateTime>();
        private string[] covered=new string[0];
        private int variant;
        public static void Publish(GameStateData gs,string corner,IEnumerable<OpponentData> opponents)
        {
            if(!Enabled || gs==null || string.IsNullOrWhiteSpace(corner))return;
            var cars=(opponents??Enumerable.Empty<OpponentData>()).Where(o=>o!=null).Take(16).Select(o=>new Subject {
                Key=gs.OpponentData.Where(p=>p.Value==o).Select(p=>p.Key).FirstOrDefault(),Name=o.DriverRawName}).Where(c=>c.Key!=null).ToArray();
            if(cars.Length==0)return;
            incoming.Enqueue(new Detection {Session=gs.SessionData.SessionId,Type=gs.SessionData.SessionType,Utc=gs.Now,Corner=corner,Cars=cars});
            Detection unused;while(incoming.Count>32)incoming.TryDequeue(out unused);
        }
        public void Reset(){Detection unused;while(incoming.TryDequeue(out unused)){}active.Clear();recorded.Clear();Volatile.Write(ref covered,new string[0]);}
        private static bool Good(float v,float min,float max){return !float.IsNaN(v) && !float.IsInfinity(v) && v>=min && v<=max;}
        public static bool IsAhead(GameStateData gs,string key,string expectedName,string corner,DateTime now)
        {
            if(gs==null || gs.PitData.InPitlane || gs.SessionData.TrackDefinition==null ||
                gs.SessionData.SessionType!=SessionType.Race && !SessionLapObserver.Supported(gs.SessionData.SessionType) ||
                gs.SessionData.SessionPhase!=SessionPhase.Green && gs.SessionData.SessionPhase!=SessionPhase.FullCourseYellow)return false;
            DateTime source=gs.SourceTelemetryUtc==DateTime.MinValue?gs.Now:gs.SourceTelemetryUtc;
            if(now<source || now-source>TimeSpan.FromSeconds(1))return false;
            OpponentData op;if(!gs.OpponentData.TryGetValue(key,out op) || !op.IsActive || op.DriverRawName!=expectedName || op.InPits ||
                op.stoppedInLandmark!=corner || gs.SessionData.stoppedInLandmark==corner || !Good(op.Speed,0,5))return false;
            float length=gs.SessionData.TrackDefinition.trackLength,own=gs.PositionAndMotionData.DistanceRoundTrack;
            if(!Good(length,100,100000) || !Good(own,0,length-.001f) || !Good(op.DistanceRoundTrack,0,length-.001f))return false;
            float ahead=(op.DistanceRoundTrack-own+length)%length;
            float speed=gs.PositionAndMotionData.CarSpeed;
            return ahead>=15 && ahead<=1000 && (ahead<=200 || Good(speed,5,180) && ahead/speed<=10);
        }
        public bool CoversAhead(string key){return Volatile.Read(ref covered).Contains(key);}
        public void Observe(GameStateData gs,DateTime now,LiveWarningDispatcher warnings,AiRaceMemory memory,bool names)
        {
            if(!Enabled || gs==null){Volatile.Write(ref covered,new string[0]);return;}
            Detection d;
            foreach(string old in recorded.Where(p=>now-p.Value>TimeSpan.FromMinutes(2)).Select(p=>p.Key).ToArray())recorded.Remove(old);
            while(incoming.TryDequeue(out d))
            {
                if(d.Session!=gs.SessionData.SessionId || d.Type!=gs.SessionData.SessionType || now<d.Utc || now-d.Utc>TimeSpan.FromSeconds(2))continue;
                if(active.Count>=16)active.RemoveAt(0);active.Add(d);
                foreach(var car in d.Cars)
                {
                    string id=car.Key+"|"+d.Corner;
                    if(recorded.ContainsKey(id))continue;
                    if(recorded.Count>=128)recorded.Remove(recorded.OrderBy(p=>p.Value).First().Key);
                    recorded[id]=now;
                    string who=names?new string((car.Name??"").Where(c=>char.IsLetterOrDigit(c)||c==' '||c=='-').Take(60).ToArray()):"Ein Gegner";
                    if(memory!=null)memory.AddObservation(who+": Crew Chief hat einen Stillstand im Streckenabschnitt erkannt.",now,"native-stopped-landmark",car.Key,"FlagsMonitor;heuristic_not_collision");
                }
            }
            active.RemoveAll(a=>a.Session!=gs.SessionData.SessionId || a.Type!=gs.SessionData.SessionType || now<a.Utc || now-a.Utc>TimeSpan.FromSeconds(15));
            var keys=new List<string>();
            foreach(var item in active)foreach(var car in item.Cars)
            {
                var captured=car;var location=item.Corner;int session=item.Session;var type=item.Type;DateTime deadline=item.Utc.AddSeconds(15);
                if(!IsAhead(gs,car.Key,car.Name,location,now))continue;
                keys.Add(car.Key);
                warnings.Offer("warning-native-incident-"+car.Key,Phrases[variant++%Phrases.Length],TeamMember.Ben,7,gs,now,
                    live=>live.SessionData.SessionId==session && live.SessionData.SessionType==type &&
                        live.OpponentData.ContainsKey(captured.Key) && live.OpponentData[captured.Key].DriverRawName==captured.Name &&
                        !live.OpponentData[captured.Key].InPits && live.OpponentData[captured.Key].stoppedInLandmark==location,
                    "FlagsMonitor_stopped_in_landmark;not_proven_collision",-1,
                    (live,utc)=>utc<=deadline && IsAhead(live,captured.Key,captured.Name,location,utc));
            }
            Volatile.Write(ref covered,keys.Distinct().ToArray());
        }
    }
}
