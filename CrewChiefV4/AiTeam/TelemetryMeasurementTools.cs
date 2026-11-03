using System;
using System.Linq;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    public static class TelemetryMeasurementTools
    {
        public static JObject Describe(TelemetryMeasurement m,DateTime now,bool paused=false)
        {
            if(m==null)return new JObject { ["status"]="fehlt",["wert"]=null };
            string status=m.StatusAt(now,paused);
            return new JObject {
                ["wert"]=status=="gueltig" && m.Value.HasValue?(JToken)m.Value.Value:null,
                ["rohwert"]=m.RawValue.HasValue?(JToken)m.RawValue.Value:null,
                ["einheit"]=m.Unit,["roheinheit"]=m.RawUnit,["umrechnung"]=m.Conversion,
                ["quelle"]=m.Source,["feld"]=m.Field,["komponente"]=m.Component,["status"]=status,
                ["messung_utc"]=m.SampleUtc==DateTime.MinValue?null:m.SampleUtc.ToString("o"),
                ["messalter_s"]=m.SampleUtc==DateTime.MinValue?null:(JToken)Math.Round((now-m.SampleUtc).TotalSeconds,6)
            };
        }
        public static JObject Read(RaceDataContext ctx,string prefix=null)
        {
            var values=new JObject();
            if(ctx==null || ctx.GameState==null)return values;
            foreach(var p in ctx.GameState.Measurements.Where(p=>prefix==null || p.Key.StartsWith(prefix,StringComparison.Ordinal)).Take(256))
                values[p.Key]=Describe(p.Value,ctx.NowUtc);
            return values;
        }
        public static JToken Number(RaceDataContext ctx,string id,float fallback)
        {
            TelemetryMeasurement m;
            if(ctx.GameState.Measurements.TryGetValue(id,out m))
                return m.StatusAt(ctx.NowUtc)=="gueltig" && m.Value.HasValue?(JToken)Math.Round(m.Value.Value,4):null;
            return ctx.TelemetryAgeSeconds>=0 && ctx.TelemetryAgeSeconds<=RaceDataTools.StaleAfterSeconds &&
                !float.IsNaN(fallback) && !float.IsInfinity(fallback) && fallback>=0 && fallback<=100?(JToken)Math.Round(fallback,4):null;
        }
    }
}
