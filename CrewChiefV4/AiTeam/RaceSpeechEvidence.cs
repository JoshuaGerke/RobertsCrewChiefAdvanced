using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Immutable facts at request time. This guards changed telemetry, not the linguistic truth
    /// of arbitrary LLM text. Urgent events remain local; cloud commentary expires after twelve seconds.</summary>
    public sealed class RaceSpeechEvidence
    {
        private readonly DateTime capturedUtc;
        private readonly int sessionId, completedLaps;
        private readonly JObject facts;
        private static readonly HashSet<string> metadata = new HashSet<string> {
            "quelle", "datenalter_s", "veraltet", "snapshot_utc", "datenalter_hinweis", "snapshot_aktuell", "messung_utc", "messalter_s", "rohwert",
            "nicht_verfuegbar", "verfuegbar", "streckenposition"
        };

        public RaceSpeechEvidence(RaceDataContext context)
        {
            capturedUtc=context == null ? DateTime.MinValue : context.NowUtc;
            if(context==null || context.GameState==null || context.TelemetryAgeSeconds<0 || context.TelemetryAgeSeconds>1) return;
            sessionId=context.GameState.SessionData.SessionId;
            completedLaps=context.GameState.SessionData.CompletedLaps;
            facts=Facts(context);
        }

        public bool IsCurrent(RaceDataContext context)
        {
            if(facts==null || context==null || context.GameState==null || context.NowUtc<capturedUtc ||
                (context.NowUtc-capturedUtc).TotalSeconds>12 || context.TelemetryAgeSeconds<0 || context.TelemetryAgeSeconds>1 ||
                context.GameState.SessionData.SessionId!=sessionId || context.GameState.SessionData.CompletedLaps!=completedLaps) return false;
            return Same(facts,Facts(context),"");
        }

        private static JObject Facts(RaceDataContext context)
        {
            // No locks on race memory, no I/O or cloud calls on the audio thread.
            var c=new RaceDataContext { GameState=context.GameState, NowUtc=context.NowUtc, LastTelemetryUtc=context.LastTelemetryUtc,
                SendDriverNames=true, GameName=context.GameName, Tracks=context.Tracks };
            return new JObject {
                ["race"]=RaceDataTools.Execute("get_race_state",c),
                ["tyres"]=RaceDataTools.Execute("get_tyre_status",c),
                ["fuel"]=RaceDataTools.Execute("get_fuel_status",c),
                ["opponents"]=RaceDataTools.Execute("get_opponents_nearby",c),
                ["pit"]=RaceDataTools.Execute("get_pit_info",c),
                ["damage"]=RaceDataTools.Execute("get_damage_and_engine",c),
                ["weather"]=RaceDataTools.Execute("get_track_conditions",c),
                ["laps"]=RaceDataTools.Execute("get_lap_times",c)
            };
        }

        private static bool Same(JToken before,JToken after,string path)
        {
            if(before==null || after==null) return before==after;
            if(before.Type!=after.Type) return false;
            var obj=before as JObject;
            if(obj!=null)
            {
                var current=(JObject)after;
                var names=obj.Properties().Select(p=>p.Name).Where(n=>!metadata.Contains(n)).ToArray();
                if(!names.OrderBy(n=>n).SequenceEqual(current.Properties().Select(p=>p.Name).Where(n=>!metadata.Contains(n)).OrderBy(n=>n))) return false;
                return names.All(n=>Same(obj[n],current[n],path+"/"+n));
            }
            var array=before as JArray;
            if(array!=null) return array.Count==((JArray)after).Count && array.Select((v,i)=>Same(v,after[i],path)).All(x=>x);
            if(before.Type==JTokenType.Float || before.Type==JTokenType.Integer)
            {
                double a=(double)before,b=(double)after;
                if(double.IsNaN(a)||double.IsInfinity(a)||double.IsNaN(b)||double.IsInfinity(b))return false;
                double tolerance=path.Contains("abstand")?.5:path.Contains("tankinhalt")?.3:
                    path.Contains("schaden_prozent") || path.Contains("/messwerte/damage.")?.05:
                    path.Contains("temperatur")?2:path.Contains("verschleiss")?.25:
                    path.Contains("druck_kpa") || path.Contains("_pressure/")?1:path.Contains("druck_psi")?.15:path.EndsWith("/restzeit_s")?12:0;
                return Math.Abs(a-b)<=tolerance;
            }
            return JToken.DeepEquals(before,after);
        }
    }
}
