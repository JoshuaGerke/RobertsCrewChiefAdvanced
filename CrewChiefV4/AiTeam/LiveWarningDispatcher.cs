using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Local replacement for native semantic warnings, independent of the simulator adapter.
    /// A warning is acknowledged at playback start, never at recognition or enqueue.</summary>
    public sealed class LiveWarningDispatcher
    {
        private static readonly string[] OilPhrasesDe={
            "Die Öltemperatur ist zu hoch. Schone den Motor und prüfe die Anzeige.",
            "Vorsicht, das Motoröl ist zu heiß. Nimm Belastung aus dem Motor.",
            "Achtung, zu hohe Öltemperatur. Schone den Motor."
        };
        private static readonly string[] OilPhrasesEn={
            "Oil temperature is too high. Look after the engine and check the gauge.",
            "Careful, the engine oil is too hot. Take some load off the engine.",
            "Watch out, oil temperature too high. Look after the engine."
        };
        private static readonly string[] OilPhrasesEs={
            "La temperatura del aceite es demasiado alta. Cuida el motor y revisa el indicador.",
            "Cuidado, el aceite del motor está demasiado caliente. Quítale carga al motor.",
            "Atención, temperatura de aceite demasiado alta. Cuida el motor."
        };
        public static string[] OilPhrases => RadioLanguage.Pick(OilPhrasesDe,OilPhrasesEn,OilPhrasesEs);
        private static readonly string[] WaterPhrasesDe={
            "Die Wassertemperatur ist zu hoch. Schone den Motor und prüfe die Kühlung.",
            "Vorsicht, das Kühlwasser ist zu heiß. Nimm Belastung aus dem Motor.",
            "Achtung, zu hohe Wassertemperatur. Schone den Motor."
        };
        private static readonly string[] WaterPhrasesEn={
            "Water temperature is too high. Look after the engine and check the cooling.",
            "Careful, the coolant is too hot. Take some load off the engine.",
            "Watch out, water temperature too high. Look after the engine."
        };
        private static readonly string[] WaterPhrasesEs={
            "La temperatura del agua es demasiado alta. Cuida el motor y revisa la refrigeración.",
            "Cuidado, el refrigerante está demasiado caliente. Quítale carga al motor.",
            "Atención, temperatura de agua demasiado alta. Cuida el motor."
        };
        public static string[] WaterPhrases => RadioLanguage.Pick(WaterPhrasesDe,WaterPhrasesEn,WaterPhrasesEs);
        private static readonly string[] BothPhrasesDe={
            "Öl und Kühlwasser sind zu heiß. Schone den Motor und prüfe die Kühlung.",
            "Vorsicht, Öl- und Wassertemperatur sind zu hoch. Nimm Belastung aus dem Motor."
        };
        private static readonly string[] BothPhrasesEn={
            "Oil and coolant are too hot. Look after the engine and check the cooling.",
            "Careful, oil and water temperatures are too high. Take some load off the engine."
        };
        private static readonly string[] BothPhrasesEs={
            "El aceite y el refrigerante están demasiado calientes. Cuida el motor y revisa la refrigeración.",
            "Cuidado, las temperaturas de aceite y agua son demasiado altas. Quítale carga al motor."
        };
        public static string[] BothPhrases => RadioLanguage.Pick(BothPhrasesDe,BothPhrasesEn,BothPhrasesEs);
        private static readonly string[] LimitPhrasesDe={
            "Tracklimit-Vorwarnung. Lass etwas mehr Platz zum Streckenrand.",
            "Die Rennleitung warnt wegen Tracklimits. Bleib sauber innerhalb der Strecke.",
            "Pass auf die Limits auf. Das ist eine Vorwarnung."
        };
        private static readonly string[] LimitPhrasesEn={
            "Track limits warning. Give yourself a bit more margin to the edge.",
            "Race control is warning about track limits. Keep it clean inside the lines.",
            "Watch the limits. That's a warning."
        };
        private static readonly string[] LimitPhrasesEs={
            "Aviso de límites de pista. Deja algo más de margen al borde.",
            "Dirección de carrera avisa por límites de pista. Mantente limpio dentro de la pista.",
            "Cuidado con los límites. Es un aviso previo."
        };
        public static string[] LimitPhrases => RadioLanguage.Pick(LimitPhrasesDe,LimitPhrasesEn,LimitPhrasesEs);
        public static IEnumerable<string> MikPhrases {get{return OilPhrases.Concat(WaterPhrases).Concat(BothPhrases).Concat(LimitPhrases);}}
        private sealed class Native
        {
            public string Kind, Source;public int Session,Lap;public SessionType Type;
            public DateTime Utc;public float Oil,Water;
        }
        private sealed class Pending
        {
            public string Topic,Text,Evidence;public TeamMember Member;public int Priority,Session,Lap=-1;
            public SessionType Type;public Func<GameStateData,bool> Relevant;
            public Func<GameStateData,DateTime,bool> TimedRelevant;
            public ObservedRaceCall Call;public DateTime CapturedUtc,RetryAfter;
            public Action<DateTime> Delivered;
        }
        private readonly ConcurrentQueue<Native> native=new ConcurrentQueue<Native>();
        private readonly Dictionary<string,Pending> pending=new Dictionary<string,Pending>();
        private readonly Dictionary<string,DateTime> delivered=new Dictionary<string,DateTime>();
        private int variant;
        public void Reset(){Native n;while(native.TryDequeue(out n)){}pending.Clear();delivered.Clear();}

        public static string Classify(IEnumerable<string> folders)
        {
            foreach(string raw in folders??new string[0])
            {
                string f=(raw??"").Replace('\\','/').Trim('/');
                if(f=="engine_monitor/hot_oil_and_water")return "both";
                if(f=="engine_monitor/hot_oil")return "oil";
                if(f=="engine_monitor/hot_water")return "water";
                if(f=="penalties/possible_track_limits_warning")return "limits";
            }
            return null;
        }
        public bool Capture(QueuedMessage message,GameStateData gs,DateTime now)
        {
            if(message==null || gs==null)return false;
            string kind=Classify(message.messageFolders);
            if(kind==null)return false;
            native.Enqueue(new Native {Kind=kind,Source=message.messageName,Session=gs.SessionData.SessionId,
                Type=gs.SessionData.SessionType,Lap=gs.SessionData.CompletedLaps,Utc=now,
                Oil=gs.EngineData.EngineOilTemp,Water=gs.EngineData.EngineWaterTemp});
            Native unused;while(native.Count>32)native.TryDequeue(out unused);
            LiveDiagnostics.Decision("native-warning","captured",kind,message.messageName);
            return true;
        }
        public void Offer(string topic,string text,TeamMember member,int priority,GameStateData gs,DateTime now,
            Func<GameStateData,bool> relevant,string evidence,int lap=-1,Func<GameStateData,DateTime,bool> timed=null,Action<DateTime> onDelivered=null)
        {
            if(gs==null || string.IsNullOrWhiteSpace(text) || relevant==null || !relevant(gs))return;
            DateTime last;
            if(delivered.TryGetValue(topic,out last) && now>=last && now-last<TimeSpan.FromSeconds(topic=="warning-tracklimit"?15:30))return;
            Pending old;
            if(pending.TryGetValue(topic,out old))
            {
                if(old.Session==gs.SessionData.SessionId && old.Type==gs.SessionData.SessionType &&
                    (old.Lap<0 || old.Lap==gs.SessionData.CompletedLaps) && old.Relevant(gs))return;
                pending.Remove(topic);
            }
            if(topic=="warning-engine-both"){pending.Remove("warning-engine-oil");pending.Remove("warning-engine-water");}
            if((topic=="warning-engine-oil" || topic=="warning-engine-water") && pending.TryGetValue("warning-engine-both",out old) && old.Relevant(gs))return;
            if(topic=="warning-invalid-lap")pending.Remove("warning-tracklimit");
            if(topic=="warning-tracklimit" && pending.TryGetValue("warning-invalid-lap",out old) && old.Relevant(gs))return;
            if(pending.Count>=16){LiveDiagnostics.Decision(topic,"suppressed","warning_capacity");return;}
            pending[topic]=new Pending {Topic=topic,Text=text,Member=member,Priority=priority,Session=gs.SessionData.SessionId,
                Type=gs.SessionData.SessionType,Lap=lap,Relevant=relevant,TimedRelevant=timed,CapturedUtc=now,Evidence=evidence,Delivered=onDelivered};
            LiveDiagnostics.Decision(topic,"detected",evidence,text);
        }
        private static bool Hot(GameStateData gs,bool oil,float captured)
        {
            float value=oil?gs.EngineData.EngineOilTemp:gs.EngineData.EngineWaterTemp;
            float limit=gs.carClass==null?0:oil?gs.carClass.maxSafeOilTemp:gs.carClass.maxSafeWaterTemp;
            return !float.IsNaN(value) && !float.IsInfinity(value) && value>1 &&
                (limit>0?value>limit:value>=captured-2);
        }
        public void Observe(GameStateData gs,DateTime now)
        {
            if(gs==null)return;
            Native n;
            while(native.TryDequeue(out n))
            {
                if(n.Session!=gs.SessionData.SessionId || n.Type!=gs.SessionData.SessionType || now<n.Utc || now-n.Utc>TimeSpan.FromSeconds(2))continue;
                var captured=n;
                if(n.Kind=="limits")
                {
                    int lap=n.Lap;
                    Offer("warning-tracklimit",LimitPhrases[variant++%LimitPhrases.Length],TeamMember.Mik,7,gs,now,
                        live=>!live.PitData.InPitlane && live.SessionData.CompletedLaps==lap,
                        "native:"+n.Source+";captured="+n.Utc.ToString("o"),lap);
                }
                else
                {
                    bool oil=n.Kind=="oil" || n.Kind=="both",water=n.Kind=="water" || n.Kind=="both";
                    var phrases=n.Kind=="both"?BothPhrases:oil?OilPhrases:WaterPhrases;
                    Offer("warning-engine-"+n.Kind,phrases[variant++%phrases.Length],TeamMember.Mik,9,gs,now,
                        live=>!live.PitData.InPitlane && (!oil || Hot(live,true,captured.Oil)) && (!water || Hot(live,false,captured.Water)),
                        "native:"+n.Source+";captured="+n.Utc.ToString("o"));
                }
            }
            foreach(var pair in pending.ToArray())
            {
                var p=pair.Value;
                if(p.Call!=null && p.Call.DeliveryStartedUtc!=DateTime.MinValue)
                {delivered[p.Topic]=p.Call.DeliveryStartedUtc;if(p.Delivered!=null)p.Delivered(p.Call.DeliveryStartedUtc);pending.Remove(pair.Key);continue;}
                if(gs.SessionData.SessionId!=p.Session || gs.SessionData.SessionType!=p.Type ||
                    p.Lap>=0 && gs.SessionData.CompletedLaps!=p.Lap || !p.Relevant(gs) || p.TimedRelevant!=null && !p.TimedRelevant(gs,now))
                {LiveDiagnostics.Decision(p.Topic,"suppressed","warning_no_longer_current",p.Evidence);pending.Remove(pair.Key);continue;}
                if(p.Call!=null && (p.Call.DeliveryFailed || now>p.Call.ExpiresUtc))
                {
                    // Renew only from the CURRENT state. A lifted yellow or next-lap invalidity never renews.
                    LiveDiagnostics.Decision(p.Topic,"deferred","still_current_warning_retry",p.Evidence);
                    p.Call=null;p.RetryAfter=now.AddSeconds(1);
                }
            }
        }
        public ObservedRaceCall Take(GameStateData gs,DateTime now,bool busy,Func<TeamMember,bool> enabled)
        {
            if(busy || gs==null)return null;
            foreach(var p in pending.Values.OrderByDescending(p=>p.Priority).ThenBy(p=>p.CapturedUtc))
            {
                if(now<p.RetryAfter || !enabled(p.Member))continue;
                // Mehrere stehende Autos am selben Unfall sind ein Vorfall, keine Ansagekette
                if(p.Topic.StartsWith("warning-native-incident-",StringComparison.Ordinal) &&
                    delivered.Any(d=>d.Key.StartsWith("warning-native-incident-",StringComparison.Ordinal) && now-d.Value<TimeSpan.FromSeconds(10)))continue;
                if(p.Call==null)p.Call=new ObservedRaceCall {Topic=p.Topic,Text=p.Text,Member=p.Member,Priority=p.Priority,
                    Position=-1,SessionId=p.Session,SessionType=p.Type,CompletedLaps=p.Lap,DetectedUtc=now,
                    ExpiresUtc=now.AddSeconds(8),AdditionalValidity=p.Relevant,TimedValidity=p.TimedRelevant,Evidence=p.Evidence};
                if(p.Call.IsValid(gs,now) && p.Call.TryReserve(now))return p.Call;
            }
            return null;
        }
        public DateTime LastDelivered(string topic)
        {DateTime utc;return delivered.TryGetValue(topic,out utc)?utc:DateTime.MinValue;}
    }
}
