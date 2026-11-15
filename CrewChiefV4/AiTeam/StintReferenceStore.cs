using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Completed own stints only. Disk activity belongs to the memory initialization and writer threads.</summary>
    internal sealed class StintReferenceStore : IDisposable
    {
        private const int Capacity = 96;
        private static readonly string[] Wheels = { "vorn links", "vorn rechts", "hinten links", "hinten rechts" };
        private static readonly string[] ReferenceKeys = { "simulator", "fahrzeug", "strecke", "layout", "klasse",
            "letzte_runde_utc", "gueltige_runden", "sprit_messrunden", "gemessener_sprit_pro_runde_l",
            "persoenliche_bestzeit_s", "sauberer_rundenschnitt_s", "staerkste_thermische_belastung_rad", "temperaturspitze_c" };
        private readonly object sync = new object();
        private readonly List<JObject> episodes = new List<JObject>();
        private readonly string path;
        private Task writer;
        private bool writable = true, writing, disposed;
        private long revision;
        private RaceFactContext current;
        private string session;
        private int laps, fuelLaps, wheel;
        private float best, peak;
        private double time, fuel;
        private DateTime end;

        internal StintReferenceStore(string folder)
        {
            path = Path.Combine(folder, "stint-referenzen-v1.json");
            try
            {
                if (!File.Exists(path)) return;
                if (new FileInfo(path).Length > 256 * 1024) { writable = false; return; }
                var data = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                if ((int?)data["schema"] != 1 || !(data["stints"] is JArray items) || items.Count > Capacity ||
                    !items.All(ValidEpisode)) { writable = false; return; }
                episodes.AddRange(items.Cast<JObject>());
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException ||
                e is ArgumentException || e is FormatException || e is InvalidCastException || e is OverflowException)
            { writable = false; }
        }

        private static bool ValidEpisode(JToken item)
        {
            if (!(item is JObject o)) return false;
            return new[] { "simulator", "fahrzeug", "strecke", "layout", "klasse" }.All(key =>
                o[key]?.Type == JTokenType.String && RaceFactContext.Label((string)o[key]) == (string)o[key]) &&
                Guid.TryParseExact((string)o["session_uid"], "N", out _) && (int?)o["gueltige_runden"] >= 4 &&
                Between(o, "persoenliche_bestzeit_s", 5, 7200) && Between(o, "sauberer_rundenschnitt_s", 5, 7200) &&
                (o["gemessener_sprit_pro_runde_l"]?.Type == JTokenType.Null || Between(o,"gemessener_sprit_pro_runde_l",.001,250)) &&
                (o["staerkste_thermische_belastung_rad"]?.Type == JTokenType.Null ||
                    Wheels.Contains((string)o["staerkste_thermische_belastung_rad"]) && Between(o,"temperaturspitze_c",1,250)) &&
                o["letzte_runde_utc"]?.Type == JTokenType.Date;
        }

        private static bool Between(JObject o, string key, double min, double max) =>
            (o[key]?.Type == JTokenType.Float || o[key]?.Type == JTokenType.Integer) && (double)o[key] >= min && (double)o[key] <= max;

        // Inputs are accepted only after RaceFactStore has admitted the lap and its identity.
        internal void Observe(RaceFactContext context, string sessionUid, LapRecord lap)
        {
            lock (sync)
            {
                if (disposed) return;
                if (session != sessionUid || !context.SameVehicleTrack(current)) Seal();
                current = context; session = sessionUid;
                laps++;
                time += lap.LapTime;
                if (laps == 1 || lap.LapTime < best) best = lap.LapTime;
                if (lap.FuelUsed >= .001f && lap.FuelUsed <= 250 && RaceFactContext.Finite(lap.FuelAtEnd) && lap.FuelAtEnd >= 0)
                { fuel += lap.FuelUsed; fuelLaps++; }
                if (lap.PeakTyreTemps?.Length == Wheels.Length)
                    for (int i = 0; i < Wheels.Length; i++)
                        if (lap.PeakTyreTemps[i] > Math.Max(1,peak) && lap.PeakTyreTemps[i] <= 250)
                        { peak = lap.PeakTyreTemps[i]; wheel = i; }
                end = lap.RecordedUtc;
            }
        }

        internal void EndStint() { lock (sync) if (!disposed) Seal(); }

        private void Seal()
        {
            if (laps >= 4)
            {
                episodes.Add(new JObject { ["simulator"]=current.Game, ["fahrzeug"]=current.Car, ["strecke"]=current.Track,
                    ["layout"]=current.Layout, ["klasse"]=current.CarClass, ["session_uid"]=session,
                    ["letzte_runde_utc"]=end, ["gueltige_runden"]=laps, ["sprit_messrunden"]=fuelLaps,
                    ["gemessener_sprit_pro_runde_l"]=fuelLaps>0?(JToken)Math.Round(fuel/fuelLaps,3):JValue.CreateNull(),
                    ["persoenliche_bestzeit_s"]=Math.Round(best,3), ["sauberer_rundenschnitt_s"]=Math.Round(time/laps,3),
                    ["staerkste_thermische_belastung_rad"]=peak>0?Wheels[wheel]:null,
                    ["temperaturspitze_c"]=peak>0?(JToken)Math.Round(peak,1):JValue.CreateNull() });
                if (episodes.Count > Capacity) episodes.RemoveAt(0);
                revision++;
                if (writable && !writing) { writing = true; writer = Task.Run((Action)Write); }
            }
            laps = fuelLaps = 0; time = fuel = best = peak = 0; current = null;
        }

        internal JObject Previous(RaceFactContext context, string currentSessionUid, int measuredLaps, DateTime now)
        {
            if (context == null || !context.IdentityKnown || currentSessionUid == null || measuredLaps >= 3) return null;
            lock (sync)
            {
                var match = episodes.LastOrDefault(o => (string)o["session_uid"] != currentSessionUid &&
                    context.SameVehicleTrack(new RaceFactContext((string)o["simulator"], (string)o["fahrzeug"], (string)o["strecke"],
                        (string)o["layout"], (string)o["klasse"])) && (DateTime)o["letzte_runde_utc"] <= now &&
                    now - (DateTime)o["letzte_runde_utc"] <= RaceFactStore.MaxAge);
                if (match == null) return null;
                var result = new JObject();
                foreach (var key in ReferenceKeys)
                    if (match[key] != null) result[key] = match[key].DeepClone();
                result["historisch"] = true;
                result["hinweis"] = "Abgeschlossener Stint einer früheren Sitzung. Keine aktuelle Verbrauchs- oder Reichweitenmessung.";
                return result;
            }
        }

        private void Write()
        {
            while (true)
            {
                JObject data;
                long version;
                lock (sync)
                {
                    version = revision;
                    data = new JObject { ["schema"]=1, ["stints"]=new JArray(episodes.Select(o=>o.DeepClone())) };
                }
                try
                {
                    string pending = path + ".pending";
                    byte[] bytes = Encoding.UTF8.GetBytes(data.ToString(Formatting.None));
                    using (var file = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
                    { file.Write(bytes,0,bytes.Length); file.Flush(true); }
                    if (File.Exists(path)) File.Replace(pending,path,path+".bak"); else File.Move(pending,path);
                    lock (sync) { if (version == revision) { writing=false; return; } }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                { lock (sync) writing=false; return; }
            }
        }

        public void Dispose()
        {
            Task task;
            lock (sync) { disposed=true; task=writer; }
            task?.Wait(TimeSpan.FromSeconds(2));
        }
    }
}
