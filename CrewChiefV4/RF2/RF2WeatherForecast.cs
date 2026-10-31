using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using Newtonsoft.Json.Linq;

using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using CrewChiefV4.LMU;

using static CrewChiefV4.LMU.LMU_REST_API_classes;

namespace CrewChiefV4.rFactor2
{
    /// <summary>
    /// rF2 and LMU don't put the weather forecast in shared memory but both
    /// expose it via their REST API (rF2 needs "WebUI port" set in player.JSON).
    /// Each session type (PRACTICE, QUALIFY, RACE) has 5 forecast nodes that
    /// split the session into equal fifths (same interpretation as TinyPedal).
    /// The forecast is converted to rainLevelIn10Mins / rainLevelIn30Mins so
    /// ConditionsMonitor can announce it the same way as ACC's.
    /// </summary>
    public class RF2WeatherForecast
    {
        private readonly bool enabled = UserSettings.GetUserSettings().
            getBoolean("enable_rf2_lmu_weather_forecast");

        // Only warn about rain if the forecast node's rain chance is at least this
        private const float minRainChance = 50f;

        private static readonly TimeSpan retryInterval = TimeSpan.FromSeconds(30);
        // The forecast may be edited (e.g. by a server admin) so re-read it occasionally
        // Der kurze Abruf ist nur für den Wetterfunk des KI-Teams nötig
        private static TimeSpan refreshInterval { get { return AiTeam.AiTeamService.Instance.Settings.Enabled ? TimeSpan.FromSeconds(30) : TimeSpan.FromMinutes(5); } }
        private static TimeSpan maxForecastAge { get { return AiTeam.AiTeamService.Instance.Settings.Enabled ? TimeSpan.FromSeconds(90) : TimeSpan.FromMinutes(10); } }

        public class ForecastNode
        {
            public int sky;
            public float rainChance;
            public float temperature;
            public ConditionsMonitor.RainLevel rainLevel;
        }

        private sealed class CachedForecast
        {
            public List<ForecastNode> Nodes;
            public DateTime FetchedUtc;
            public int Epoch;
        }
        private volatile CachedForecast forecast; // immutable snapshot published by the fetch task
        private int forecastEpoch, mappedSessionId = int.MinValue;
        private volatile string forecastSessionType;
        private DateTime nextFetchDue = DateTime.MinValue;
        private Task fetchTask;

        /// <summary>
        /// Set cgs.Conditions forecast rain levels from the REST API forecast.
        /// If no forecast is available the forecast levels are set to the current
        /// rain level so ConditionsMonitor has nothing to announce.
        /// </summary>
        public void update(GameStateData cgs, int rF2SessionType, float raining, double currentET, double endET)
        {
            var conditions = cgs.Conditions;
            conditions.RainForecastAvailable=false;
            conditions.RainForecastSource=null;
            conditions.RainChanceIn10Mins=conditions.RainChanceIn30Mins=-1;
            conditions.ForecastAirTempIn10Mins=conditions.ForecastAirTempIn30Mins=float.NaN;
            conditions.rainLevelNow = ConditionsMonitor.getRainLevel(raining);
            conditions.rainLevelIn10Mins = conditions.rainLevelNow;
            conditions.rainLevelIn30Mins = conditions.rainLevelNow;
            if (!enabled)
            {
                return;
            }

            var sessionType = getForecastSessionType(rF2SessionType);
            if (sessionType != forecastSessionType || cgs.SessionData.IsNewSession || mappedSessionId != cgs.SessionData.SessionId)
            {   // New session type, the old forecast doesn't apply
                forecast = null;
                Interlocked.Increment(ref forecastEpoch);
                mappedSessionId=cgs.SessionData.SessionId;
                forecastSessionType = sessionType;
                nextFetchDue = DateTime.MinValue;
            }
            if (cgs.Now > nextFetchDue && (fetchTask == null || fetchTask.IsCompleted))
            {
                nextFetchDue = cgs.Now.Add(forecast == null ? retryInterval : refreshInterval);
                int epoch=Volatile.Read(ref forecastEpoch);
                fetchTask = Task.Run(() => fetch(sessionType,epoch));
            }

            var cached = forecast;
            if (cached == null || cached.Epoch != Volatile.Read(ref forecastEpoch) || endET <= 0 || double.IsNaN(endET) || double.IsInfinity(endET) ||
                double.IsNaN(currentET) || double.IsInfinity(currentET) || cgs.Now < cached.FetchedUtc || cgs.Now-cached.FetchedUtc > maxForecastAge)
            {   // No forecast or not a timed session
                return;
            }
            var nodes=cached.Nodes;
            conditions.rainLevelIn10Mins = getRainLevelAt(nodes, currentET + 10 * 60, endET);
            conditions.rainLevelIn30Mins = getRainLevelAt(nodes, currentET + 30 * 60, endET);
            conditions.RainForecastAvailable=true;
            conditions.RainForecastReadUtc=cached.FetchedUtc;
            conditions.RainForecastSource=Game.RF2_64BIT ? "rFactor 2 lokale Spiel-/Server-REST-Vorhersage" : "Le Mans Ultimate lokale Spiel-/Server-REST-Vorhersage";
            conditions.RainChanceIn10Mins=getNodeAt(nodes,currentET+10*60,endET).rainChance;
            conditions.RainChanceIn30Mins=getNodeAt(nodes,currentET+30*60,endET).rainChance;
            conditions.ForecastAirTempIn10Mins=getNodeAt(nodes,currentET+10*60,endET).temperature;
            conditions.ForecastAirTempIn30Mins=getNodeAt(nodes,currentET+30*60,endET).temperature;
        }

        public static string getForecastSessionType(int rF2SessionType)
        {
            // 0 test day, 1-4 practice, 5-8 qualifying, 9 warmup, 10-13 race
            if (rF2SessionType >= 9)
            {
                return "RACE";
            }
            if (rF2SessionType >= 5)
            {
                return "QUALIFY";
            }
            return "PRACTICE";
        }

        public static ConditionsMonitor.RainLevel getRainLevelAt(List<ForecastNode> nodes, double sessionTime, double endET)
        {
            var node=getNodeAt(nodes,sessionTime,endET);
            return node==null ? ConditionsMonitor.RainLevel.NONE : node.rainLevel;
        }

        /// <summary>
        /// Sky type:
        /// 0 Clear, 1 Light Clouds, 2 Partially Cloudy, 3 Mostly Cloudy, 4 Overcast,
        /// 5 Cloudy & Drizzle, 6 Cloudy & Light Rain, 7 Overcast & Light Rain,
        /// 8 Overcast & Rain, 9 Overcast & Heavy Rain, 10 Overcast & Storm
        /// </summary>
        private static ConditionsMonitor.RainLevel skyToRainLevel(int sky, float rainChance)
        {
            if (rainChance < minRainChance)
            {
                return ConditionsMonitor.RainLevel.NONE;
            }
            switch (sky)
            {
                case 5:
                    return ConditionsMonitor.RainLevel.DRIZZLE;
                case 6:
                case 7:
                    return ConditionsMonitor.RainLevel.LIGHT;
                case 8:
                    return ConditionsMonitor.RainLevel.MID;
                case 9:
                    return ConditionsMonitor.RainLevel.HEAVY;
                case 10:
                    return ConditionsMonitor.RainLevel.STORM;
                default:
                    return ConditionsMonitor.RainLevel.NONE;
            }
        }

        private static ForecastNode getNodeAt(List<ForecastNode> nodes,double sessionTime,double endET)
        {
            if(nodes==null || nodes.Count==0 || endET<=0 || double.IsNaN(endET) || double.IsInfinity(endET) || double.IsNaN(sessionTime) || double.IsInfinity(sessionTime))return null;
            var fraction=Math.Min(Math.Max(sessionTime/endET,0),1);
            return nodes[Math.Min((int)(fraction*nodes.Count),nodes.Count-1)];
        }

        private void fetch(string sessionType,int epoch)
        {
            try
            {
                var nodes = parse(new RestAPI().get<JObject>(SessionsWeather.endPoint), sessionType);
                if (nodes == null || sessionType != forecastSessionType || epoch != Volatile.Read(ref forecastEpoch))
                {   // No forecast or session changed while reading
                    return;
                }
                if (forecast == null || !forecast.Nodes.Select(n => n.rainLevel).SequenceEqual(nodes.Select(n => n.rainLevel)))
                {
                    Log.Info($"Weather forecast for {sessionType}: " + string.Join(", ", nodes.Select(n =>
                        $"sky {n.sky} rain chance {n.rainChance}% {n.temperature}C => {n.rainLevel}")));
                }
                forecast = new CachedForecast { Nodes=nodes,FetchedUtc=DateTime.UtcNow,Epoch=epoch };
            }
            catch (Exception e)
            {
                Log.Warning("Could not read weather forecast: " + e.Message);
            }
        }

        /// <summary>
        /// Parse the rest/sessions/weather response for one session type.
        /// </summary>
        /// <returns>The forecast nodes or null if they're not available</returns>
        public static List<ForecastNode> parse(JObject response, string sessionType)
        {
            var session = response?[sessionType];
            if (session == null)
            {
                return null;
            }
            var nodes = new List<ForecastNode>();
            foreach (var nodeName in SessionsWeather.nodeNames)
            {
                var node = session[nodeName];
                if (node == null)
                {
                    Log.Warning($"Weather forecast node {nodeName} missing for {sessionType}");
                    return null;
                }
                var skyValue = node["WNV_SKY"]?["currentValue"]?.Value<int?>();
                var chanceValue = node["WNV_RAIN_CHANCE"]?["currentValue"]?.Value<float?>();
                if(!skyValue.HasValue || skyValue.Value<0 || skyValue.Value>10 || !chanceValue.HasValue ||
                    float.IsNaN(chanceValue.Value) || float.IsInfinity(chanceValue.Value) || chanceValue.Value<0 || chanceValue.Value>100)return null;
                int sky=skyValue.Value;
                float rainChance=chanceValue.Value;
                nodes.Add(new ForecastNode
                {
                    sky = sky,
                    rainChance = rainChance,
                    temperature = node["WNV_TEMPERATURE"]?["currentValue"]?.Value<float>() ?? float.NaN,
                    rainLevel = skyToRainLevel(sky, rainChance),
                });
            }
            return nodes;
        }
    }
}
