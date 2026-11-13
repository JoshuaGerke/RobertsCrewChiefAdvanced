using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Local bounded weather attention, independent of optional periodic reports and cloud inference.</summary>
    public sealed class WeatherWatch
    {
        private sealed class Pending { public ObservedRaceCall Call; }
        private readonly Dictionary<string,Pending> pending=new Dictionary<string,Pending>();
        private readonly RaceRadioLanguage language=new RaceRadioLanguage();
        private int session=int.MinValue; private SessionType type=SessionType.Unavailable;
        private bool? raining; private int? wetness;
        private float? air,track,wind;
        private int forecast10=-1,forecast30=-1;
        private float chance10=-1,chance30=-1,forecastTemp10=float.NaN,forecastTemp30=float.NaN;
        private DateTime lastSample=DateTime.MinValue;
        private DateTime lastWeatherRadio=DateTime.MinValue;
        public int PendingCount { get { return pending.Count; } }
        public void Reset()
        {
            pending.Clear();session=int.MinValue;type=SessionType.Unavailable;raining=null;wetness=null;
            air=track=wind=null;forecast10=forecast30=-1;chance10=chance30=-1;lastSample=lastWeatherRadio=DateTime.MinValue;forecastTemp10=forecastTemp30=float.NaN;
        }

        public static bool Fresh(DateTime now,DateTime sample,int seconds=30)
        { return sample!=DateTime.MinValue && now>=sample && now-sample<=TimeSpan.FromSeconds(seconds); }
        private static bool Good(float value,float low,float high)
        { return !float.IsNaN(value) && !float.IsInfinity(value) && value>=low && value<=high; }
        private static bool? Rain(Conditions.ConditionsSample c)
        { return c!=null && Good(c.RainDensity,0,1) ? (bool?)(c.RainDensity>=.01f) : null; }
        private static int? Wet(Conditions.ConditionsSample c)
        {
            if(c==null)return null;
            if(c.TrackWetness>=ConditionsMonitor.TrackWetness.Dry && c.TrackWetness<=ConditionsMonitor.TrackWetness.ExtremelyWet)
                return (int)c.TrackWetness;
            switch(c.TrackStatus) {
                case ConditionsMonitor.TrackStatus.DAMP:return 3;
                case ConditionsMonitor.TrackStatus.WET:return 5;
                case ConditionsMonitor.TrackStatus.FLOODED:return 8;
                // GREEN/FAST/OPTIMUM describe grip, not a verified dry surface.
                default:return null;
            }
        }
        public static bool ForecastFresh(Conditions c,DateTime now)
        {
            return c!=null && c.RainForecastAvailable && !string.IsNullOrWhiteSpace(c.RainForecastSource) && Fresh(now,c.RainForecastReadUtc,90) &&
                ((int)c.rainLevelIn10Mins>=0 && (int)c.rainLevelIn10Mins<=5 || (int)c.rainLevelIn30Mins>=0 && (int)c.rainLevelIn30Mins<=5);
        }
        private static string RainText(int value)
        { return new[]{"kein Regen","Nieselregen","leichter Regen","mäßiger Regen","starker Regen","Sturmregen"}[value]; }
        // RainText bleibt deutsch (Daten für das Sprachmodell); gesprochen wird diese Fassung.
        private static string SpokenRainText(int value)
        { return RadioLanguage.Pick(new[]{"kein Regen","Nieselregen","leichter Regen","mäßiger Regen","starker Regen","Sturmregen"},
            new[]{"no rain","drizzle","light rain","moderate rain","heavy rain","torrential rain"},
            new[]{"sin lluvia","llovizna","lluvia ligera","lluvia moderada","lluvia fuerte","lluvia torrencial"})[value]; }
        private static string Number(float value) { return RadioLanguage.Num(value,"0.#"); }
        public static JObject ForecastData(Conditions c,DateTime now)
        {
            if(!ForecastFresh(c,now))return new JObject { ["verfuegbar"]=false,["grund"]="Keine bestätigte frische Spiel-/Servervorhersage vom Adapter verfügbar." };
            return new JObject { ["verfuegbar"]=true,["quelle"]=c.RainForecastSource,
                ["abfrage_utc"]=DateTime.SpecifyKind(c.RainForecastReadUtc,DateTimeKind.Utc).ToString("o"),
                ["abfragealter_s"]=Math.Round((now-c.RainForecastReadUtc).TotalSeconds,1),
                ["zeitbasis"]="Prognosehorizont in Spielminuten, keine garantierte reale Ankunftszeit von Regen.",
                ["in_10_spielminuten"]=ForecastPoint((int)c.rainLevelIn10Mins,c.RainChanceIn10Mins,c.ForecastAirTempIn10Mins),
                ["in_30_spielminuten"]=ForecastPoint((int)c.rainLevelIn30Mins,c.RainChanceIn30Mins,c.ForecastAirTempIn30Mins) };
        }
        private static JObject ForecastPoint(int level,float chance,float temperature)
        {
            if(level<0 || level>5)return new JObject { ["verfuegbar"]=false };
            return new JObject { ["verfuegbar"]=true,["regenstufe"]=level,["regenbeschreibung"]=level==0 && Good(chance,.001f,100)?"Regen möglich, native Regenstufe unter der Meldeschwelle":RainText(level),
                ["lufttemperatur_c"]=Good(temperature,-40,70)?(JToken)Math.Round(temperature,1):null,
                ["regenwahrscheinlichkeit_pct"]=Good(chance,0,100)?(JToken)Math.Round(chance,1):null,
                ["hinweis"]=Good(chance,0,100)?"Regenstufe enthält die Meldeschwelle des Adapters; bei weniger als 50 Prozent ist NONE keine garantierte Trockenprognose.":"Keine separate Wahrscheinlichkeit vom Adapter geliefert." };
        }

        public void Observe(GameStateData gs,DateTime now)
        {
            if(gs==null || gs.SessionData.SessionType==SessionType.Unavailable)return;
            var s=gs.SessionData;
            if(session!=s.SessionId || type!=s.SessionType || s.IsNewSession) { Reset();session=s.SessionId;type=s.SessionType; }
            ConsumeDelivered();
            var c=gs.Conditions.LatestConditions;
            if(c!=null && Fresh(now,c.Time) && c.Time!=lastSample)
            {
                lastSample=c.Time;
                bool? rain=Rain(c);int? wet=Wet(c);
                if(!raining.HasValue)raining=rain;
                else if(rain.HasValue && raining!=rain) {
                    bool value=rain.Value;
                    Add(gs,now,"rain",language.WeatherRain(value),5,"measured_rain_transition",live=>Rain(live.Conditions.LatestConditions)==value,
                        ()=>raining=value);
                }
                if(!wetness.HasValue)wetness=wet;
                else if(wet.HasValue && Math.Abs(wet.Value-wetness.Value)>=2) {
                    int value=wet.Value;
                    Add(gs,now,"wetness",language.WeatherWetness(value>wetness.Value),4,"measured_surface_wetness_transition",
                        live=>Wet(live.Conditions.LatestConditions)==value,()=>wetness=value);
                }
                Temperature(gs,now,c.AmbientTemperature,-40,70,2,"air","Luft",ref air);
                Temperature(gs,now,c.TrackTemperature,-40,90,3,"track","Strecke",ref track);
                if(Good(c.WindSpeed,.01f,150)) {
                    if(!wind.HasValue)wind=c.WindSpeed;
                    else if(Math.Abs(c.WindSpeed-wind.Value)>=4) {
                        float value=c.WindSpeed;
                        Add(gs,now,"wind",RadioLanguage.T("Der gemeldete Wind hat sich verändert: "+Number(value)+" Meter pro Sekunde.",
                            "The reported wind has changed: "+Number(value)+" metres per second.","El viento reportado ha cambiado: "+Number(value)+" metros por segundo."),2,"measured_wind_speed_change",
                            live=>live.Conditions.LatestConditions!=null && Math.Abs(live.Conditions.LatestConditions.WindSpeed-value)<1,()=>wind=value);
                    }
                }
            }
            if(ForecastFresh(gs.Conditions,now)) {
                int ten=(int)gs.Conditions.rainLevelIn10Mins, thirty=(int)gs.Conditions.rainLevelIn30Mins;
                if(ten>=0 && ten<=5)Forecast(gs,now,10,ten,gs.Conditions.RainChanceIn10Mins,gs.Conditions.ForecastAirTempIn10Mins,ref forecast10,ref chance10,ref forecastTemp10);
                if(thirty>=0 && thirty<=5)Forecast(gs,now,30,thirty,gs.Conditions.RainChanceIn30Mins,gs.Conditions.ForecastAirTempIn30Mins,ref forecast30,ref chance30,ref forecastTemp30);
            }
        }
        private void Temperature(GameStateData gs,DateTime now,float value,float low,float high,float threshold,string topic,string name,ref float? baseline)
        {
            if(!Good(value,low,high))return;
            if(!baseline.HasValue){baseline=value;return;}
            if(Math.Abs(value-baseline.Value)<threshold)return;
            string n=Number(value);
            Add(gs,now,topic,RadioLanguage.T(name+"temperatur verändert: jetzt "+n+" Grad.",(topic=="air"?"Air":"Track")+" temperature has changed: now "+n+" degrees.",
                "La temperatura "+(topic=="air"?"del aire":"de la pista")+" ha cambiado: ahora "+n+" grados."),2,"measured_temperature_change",
                live=>live.Conditions.LatestConditions!=null && Math.Abs((topic=="air"?live.Conditions.LatestConditions.AmbientTemperature:live.Conditions.LatestConditions.TrackTemperature)-value)<=.5f,
                ()=> { if(topic=="air")air=value;else track=value; });
        }
        private void Forecast(GameStateData gs,DateTime now,int minutes,int level,float chance,float temperature,ref int baseline,ref float priorChance,ref float priorTemperature)
        {
            bool first=baseline<0;
            bool knownChance=Good(chance,0,100), wasKnown=Good(priorChance,0,100);
            int current=(int)gs.Conditions.rainLevelNow;
            bool relevant=first ? (knownChance?chance>=50:level>(current>=0 && current<=5?current:0)) :
                level!=baseline || knownChance && wasKnown && (Math.Abs(chance-priorChance)>=20 || (chance>=50)!=(priorChance>=50));
            bool knownTemperature=Good(temperature,-40,70);
            relevant = relevant || !first && knownTemperature && Good(priorTemperature,-40,70) && Math.Abs(temperature-priorTemperature)>=2;
            if(first && !relevant){baseline=level;priorChance=chance;priorTemperature=temperature;return;}
            if(!Good(priorTemperature,-40,70) && knownTemperature)priorTemperature=temperature;
            if(!relevant)return;
            string rain=SpokenRainText(level),temp=knownTemperature?Number(temperature):null,pct=knownChance?Number(chance):null;
            string text=RadioLanguage.T(
                "Spielvorhersage für "+minutes+" Spielminuten: "+(knownChance?pct+" Prozent Regenwahrscheinlichkeit":rain)+"."+(knownTemperature?" Luft: "+temp+" Grad.":""),
                "Game forecast for "+minutes+" game minutes: "+(knownChance?pct+" percent chance of rain":rain)+"."+(knownTemperature?" Air: "+temp+" degrees.":""),
                "Previsión del juego para "+minutes+" minutos de juego: "+(knownChance?pct+" por ciento de probabilidad de lluvia":rain)+"."+(knownTemperature?" Aire: "+temp+" grados.":""));
            Add(gs,now,"forecast-"+minutes,text,4,"confirmed_game_server_forecast_change",
                live=>ForecastFresh(live.Conditions,live.Now) && (int)(minutes==10?live.Conditions.rainLevelIn10Mins:live.Conditions.rainLevelIn30Mins)==level &&
                    (!knownChance || Math.Abs((minutes==10?live.Conditions.RainChanceIn10Mins:live.Conditions.RainChanceIn30Mins)-chance)<1) &&
                    (!knownTemperature || Math.Abs((minutes==10?live.Conditions.ForecastAirTempIn10Mins:live.Conditions.ForecastAirTempIn30Mins)-temperature)<.5f),
                ()=> {if(minutes==10){forecast10=level;chance10=chance;forecastTemp10=temperature;}else{forecast30=level;chance30=chance;forecastTemp30=temperature;} });
        }
        private void Add(GameStateData gs,DateTime now,string topic,string text,int priority,string evidence,Func<GameStateData,bool> valid,Action observed)
        {
            Pending existing;
            if(pending.TryGetValue(topic,out existing) && existing.Call.Text==text && existing.Call.IsValid(gs,now))return;
            if(pending.Count>=7 && !pending.ContainsKey(topic))return;
            pending[topic]=new Pending { Call=new ObservedRaceCall { Topic="weather-"+topic,Text=text,Priority=priority,
                Evidence=evidence,Member=TeamMember.Mik,Position=-1,SessionId=session,SessionType=type,DetectedUtc=now,ExpiresUtc=now.AddSeconds(10),
                AllowPitlane=true,AdditionalValidity=live=>
                    (topic.StartsWith("forecast-",StringComparison.Ordinal)?ForecastFresh(live.Conditions,live.Now):
                        live.Conditions.LatestConditions!=null && Fresh(live.Now,live.Conditions.LatestConditions.Time)) && valid(live) } };
            // Observation and delivery are separate. An expired rain onset must never be recreated as a new onset minutes later.
            observed();
            LiveDiagnostics.Decision("weather-"+topic,"detected",evidence,text);
        }
        private void ConsumeDelivered()
        {
            foreach(var item in pending.Where(p=>p.Value.Call.DeliveryStartedUtc!=DateTime.MinValue).ToArray()) {
                if(item.Value.Call.DeliveryStartedUtc>lastWeatherRadio)lastWeatherRadio=item.Value.Call.DeliveryStartedUtc;
                pending.Remove(item.Key);
            }
        }
        public ObservedRaceCall Take(GameStateData gs,DateTime now,bool busy,int minimumPriority=0)
        {
            ConsumeDelivered();
            foreach(var expired in pending.Where(p=>!p.Value.Call.IsValid(gs,now)).ToArray()) {
                LiveDiagnostics.Decision(expired.Value.Call.Topic,"suppressed","weather_changed_or_expired");pending.Remove(expired.Key);
            }
            if(busy)return null;
            var selected=pending.Values.OrderByDescending(p=>p.Call.Priority).FirstOrDefault(p=>p.Call.Priority>=minimumPriority &&
                (p.Call.Priority>=5 || now-lastWeatherRadio>=TimeSpan.FromSeconds(12)) && p.Call.CanReserve(now));
            return selected!=null && selected.Call.TryReserve(now)?selected.Call:null;
        }
    }
}
