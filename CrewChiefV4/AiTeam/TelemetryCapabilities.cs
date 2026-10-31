using System;
using System.Linq;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Evidence of what reached the mapped data layer, not a claim that the complete simulator SDK was audited.</summary>
    public static class TelemetryCapabilities
    {
        private static bool Good(float value,float low,float high)
        { return !float.IsNaN(value) && !float.IsInfinity(value) && value>=low && value<=high; }
        public static JObject Describe(RaceDataContext ctx)
        {
            if(ctx==null || ctx.GameState==null)return new JObject { ["verfuegbar"]=false };
            var gs=ctx.GameState;var s=gs.SessionData;
            var cars=gs.OpponentData.Values.Where(o=>o!=null).Take(256).ToArray();
            var c=gs.Conditions.LatestConditions;
            var spatial=ctx.Tracks==null?null:ctx.Tracks.Snapshot;
            int sectors=cars.Count(o=>o.OpponentLapData.Skip(Math.Max(0,o.OpponentLapData.Count-3)).Any(l=>l.SectorTimes.Any(t=>Good(t,.001f,3600))));
            return new JObject {
                ["bedeutung"]="Nachweis aktuell im Adapter-/GameState angekommenen Daten. Fehlend bedeutet hier nicht gemappt oder nicht geliefert, keine Behauptung über das gesamte SDK.",
                ["vollstaendiger_roh_sdk_audit"]=false,
                ["eigene_position"]=s.ClassPosition>0,
                ["eigene_bestzeit"]=Good(s.PlayerLapTimeSessionBest,.001f,3600),
                ["klassenbestzeit"]=Good(s.PlayerClassSessionBestLapTime,.001f,3600),
                ["erfasste_gegner"]=cars.Length,
                ["gegner_mit_letzter_rundenzeit"]=cars.Count(o=>Good(o.LastLapTime,.001f,3600)),
                ["gegner_mit_bestzeit"]=cars.Count(o=>Good(o.CurrentBestLapTime,.001f,3600)),
                ["gegner_mit_erfassten_sektoren"]=sectors,
                ["gegner_mit_namen"]=ctx.SendDriverNames?cars.Count(o=>!string.IsNullOrWhiteSpace(o.DriverRawName)):(JToken)null,
                ["raeumliche_beobachtung_frisch"]=spatial!=null && spatial.IsCurrent(ctx),
                ["tankinhalt"]=gs.FuelData.FuelUseActive && Good(gs.FuelData.FuelLeft,0,10000),
                ["schadensmodell_aktiv"]=gs.CarDamageData.DamageEnabled,
                ["aufhaengungsstatus_gemappt"]=gs.CarDamageData.SuspensionDamageStatus!=null && gs.CarDamageData.SuspensionDamageStatus.cornersForEachStatus.Count>0,
                ["bremsenschadensstatus_gemappt"]=gs.CarDamageData.BrakeDamageStatus!=null && gs.CarDamageData.BrakeDamageStatus.cornersForEachStatus.Count>0,
                ["motor_wassertemperatur"]=Good(gs.EngineData.EngineWaterTemp,1,250),
                ["motor_oeltemperatur"]=Good(gs.EngineData.EngineOilTemp,1,300),
                ["reifenverschleiss_aktiv"]=gs.TyreData.TyreWearActive,
                ["aktuelles_wetter_frisch"]=c!=null && WeatherWatch.Fresh(ctx.NowUtc,c.Time),
                ["spiel_server_vorhersage"]=WeatherWatch.ForecastData(gs.Conditions,ctx.NowUtc),
                ["lokaler_spotter_frisch"]=ctx.Spotter.IsFresh(ctx.NowUtc),
                ["ereignislogik_vorhanden"]=new JArray("Position/Überholbeleg","Runden/Bestzeiten","Strafen/Tracklimits","Schäden","Flaggen","Verkehr/Annäherung","Wetter/Vorhersage","Boxengeschwindigkeit"),
                ["hinweise"]=new JArray("Die registrierte Ereignislogik ist keine Garantie, dass ein Teammitglied im Profil eingeschaltet ist.",
                    "Gegner-Reifen und Prognosen nicht aus verfügbaren Namen/Koordinaten erfinden.","Historische Erinnerungen ersetzen keine aktuellen Messwerte.")
            };
        }
    }
}
