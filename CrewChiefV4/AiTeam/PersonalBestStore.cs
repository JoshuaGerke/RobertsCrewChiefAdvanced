using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CrewChiefV4.GameState;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Allzeit-Bestzeit für eine Kombination aus Simulator, Fahrzeug, Strecke und Layout – mit den Bedingungen der Runde.</summary>
    internal sealed class PersonalBest
    {
        public string Game, Car, Track, Layout, CarClass, Compound, SessionType;
        public float LapSeconds, FuelAtStart = -1, TrackC = float.NaN, AirC = float.NaN, Rain = -1;
        public float[] Sectors;      // Sektoren der Bestrunde (3 oder 4, wie das Spiel sie liefert)
        public float[] BestSectors;  // beste je gefahrene Einzelsektoren (theoretische Bestrunde), Rob 16.11.
        public DateTime DateUtc;
        public int Improvements;

        internal string Key => Key(Game, Car, Track, Layout);
        internal static string Key(string game, string car, string track, string layout) =>
            (game ?? "") + "|" + (car ?? "") + "|" + (track ?? "") + "|" + (layout ?? "");

        internal JObject ToJson() => new JObject
        {
            ["simulator"] = Game, ["fahrzeug"] = Car, ["strecke"] = Track, ["layout"] = Layout, ["klasse"] = CarClass,
            ["bestzeit_s"] = Math.Round(LapSeconds, 3),
            ["sektoren_s"] = GoodSectors(Sectors) ? new JArray(Sectors.Select(x => Math.Round(x, 3))) : null,
            ["beste_einzelsektoren_s"] = GoodSectors(BestSectors) ? new JArray(BestSectors.Select(x => Math.Round(x, 3))) : null,
            ["sprit_beim_rundenstart_l"] = FuelAtStart >= 0 ? (JToken)Math.Round(FuelAtStart, 1) : null,
            ["reifen"] = Compound,
            ["streckentemperatur_c"] = Finite(TrackC) ? (JToken)Math.Round(TrackC, 1) : null,
            ["lufttemperatur_c"] = Finite(AirC) ? (JToken)Math.Round(AirC, 1) : null,
            ["regen_0_bis_1"] = Rain >= 0 ? (JToken)Math.Round(Rain, 2) : null,
            ["sitzung"] = SessionType, ["datum_utc"] = DateUtc.ToString("o", CultureInfo.InvariantCulture),
            ["verbesserungen"] = Improvements
        };

        internal static PersonalBest FromJson(JObject o)
        {
            var b = new PersonalBest
            {
                Game = (string)o["simulator"], Car = (string)o["fahrzeug"], Track = (string)o["strecke"], Layout = (string)o["layout"],
                CarClass = (string)o["klasse"], Compound = (string)o["reifen"], SessionType = (string)o["sitzung"],
                LapSeconds = (float?)o["bestzeit_s"] ?? -1, FuelAtStart = (float?)o["sprit_beim_rundenstart_l"] ?? -1,
                TrackC = (float?)o["streckentemperatur_c"] ?? float.NaN, AirC = (float?)o["lufttemperatur_c"] ?? float.NaN,
                Rain = (float?)o["regen_0_bis_1"] ?? -1, Improvements = (int?)o["verbesserungen"] ?? 0,
                Sectors = o["sektoren_s"] is JArray a && (a.Count == 3 || a.Count == 4) ? a.Select(x => (float)x).ToArray() : null,
                BestSectors = o["beste_einzelsektoren_s"] is JArray bs && (bs.Count == 3 || bs.Count == 4) ? bs.Select(x => (float)x).ToArray() : null
            };
            DateTime.TryParse((string)o["datum_utc"], CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out b.DateUtc);
            return b.LapSeconds > 10 && b.LapSeconds < 3600 && !string.IsNullOrWhiteSpace(b.Track) && !string.IsNullOrWhiteSpace(b.Car) ? b : null;
        }

        internal static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
        internal static bool GoodSectors(float[] s) => s != null && (s.Length == 3 || s.Length == 4) && s.All(x => x > 0 && x < 1000);
    }

    /// <summary>
    /// Robs Bestzeiten-Datenbank (Wunsch 16.11.2026): je Fahrzeug, Strecke und Layout die schnellste jemals gültige eigene Runde
    /// mit Sprit beim Rundenstart, Reifen, Strecken- und Lufttemperatur, Regen, Sitzung und Datum.
    /// Für jede Simulation eine eigene Datei („Bestzeiten\&lt;Simulation&gt;.json“ im Fahrergedächtnis), damit nichts verwechselt wird;
    /// zusätzlich steht der Simulator in jedem Eintrag und ist Teil des Schlüssels. Lokal, wird nirgends hochgeladen.
    /// Gezählt werden nur gültige Runden ohne Boxengasse mit vom Spiel gemeldeter Zeit. Ein-/ausschaltbar und löschbar in der App.
    /// </summary>
    internal sealed class PersonalBestStore
    {
        internal const int Capacity = 2000;
        private readonly object sync = new object();
        private readonly Dictionary<string, PersonalBest> bests = new Dictionary<string, PersonalBest>(StringComparer.Ordinal);
        private readonly string path;
        internal readonly string Game;
        private bool writable = true;
        private int pendingWrite;

        private static readonly object registry = new object();
        private static readonly Dictionary<string, PersonalBestStore> stores = new Dictionary<string, PersonalBestStore>(StringComparer.OrdinalIgnoreCase);
        private static int enabledState = -1;

        /// <summary>Ordner der Bestzeiten-Datenbank im Fahrergedächtnis (bleibt bei Updates erhalten).</summary>
        internal static string Folder => Path.Combine(DriverMemory.Folder, "Bestzeiten");
        private static string SettingsPath => Path.Combine(Folder, "einstellungen.json");

        /// <summary>Bestzeiten speichern und kommentieren – Standard an; die Wahl liegt im Fahrergedächtnis, nicht in den Crew-Chief-Einstellungen.</summary>
        internal static bool Enabled
        {
            get
            {
                int state = Volatile.Read(ref enabledState);
                if (state >= 0) return state == 1;
                bool on = true;
                try { if (File.Exists(SettingsPath)) on = (bool?)JObject.Parse(File.ReadAllText(SettingsPath, Encoding.UTF8))["aktiv"] ?? true; }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException || e is ArgumentException) { }
                Volatile.Write(ref enabledState, on ? 1 : 0);
                return on;
            }
            set
            {
                Volatile.Write(ref enabledState, value ? 1 : 0);
                try { Directory.CreateDirectory(Folder); File.WriteAllText(SettingsPath, new JObject { ["aktiv"] = value }.ToString(), new UTF8Encoding(false)); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException) { }
            }
        }

        internal static string FileFor(string game)
        {
            var name = new StringBuilder();
            foreach (char c in (game ?? "unbekannt").Trim()) name.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            string n = name.ToString().Trim('_');
            return (n.Length == 0 ? "unbekannt" : n.Length > 60 ? n.Substring(0, 60) : n) + ".json";
        }

        /// <summary>Datenbank der jeweiligen Simulation (null, wenn ausgeschaltet oder der Ordner nicht nutzbar ist).</summary>
        internal static PersonalBestStore For(string game)
        {
            if (string.IsNullOrWhiteSpace(game) || !Enabled) return null;
            lock (registry)
            {
                if (stores.TryGetValue(game, out var s)) return s;
                try { s = new PersonalBestStore(Folder, game); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException) { return null; }
                stores[game] = s;
                return s;
            }
        }

        /// <summary>Übersicht für die App: Simulation und Anzahl gespeicherter Kombinationen.</summary>
        internal static List<KeyValuePair<string, int>> Overview()
        {
            var list = new List<KeyValuePair<string, int>>();
            try
            {
                if (!Directory.Exists(Folder)) return list;
                foreach (var file in Directory.GetFiles(Folder, "*.json").Where(f => !f.EndsWith("einstellungen.json", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        if (new FileInfo(file).Length > 4 * 1024 * 1024) continue;
                        var data = JObject.Parse(File.ReadAllText(file, Encoding.UTF8));
                        list.Add(new KeyValuePair<string, int>((string)data["simulation"] ?? Path.GetFileNameWithoutExtension(file), (data["bestzeiten"] as JArray)?.Count ?? 0));
                    }
                    catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException) { }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            return list.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<KeyValuePair<string, int>> overviewCache;
        private static DateTime overviewUtc = DateTime.MinValue;
        /// <summary>Übersicht höchstens alle 5 s neu von der Platte lesen (die App fragt oft).</summary>
        internal static List<KeyValuePair<string, int>> OverviewCached()
        {
            lock (registry)
            {
                if (overviewCache != null && DateTime.UtcNow - overviewUtc < TimeSpan.FromSeconds(5)) return overviewCache;
            }
            var fresh = Overview();
            lock (registry) { overviewCache = fresh; overviewUtc = DateTime.UtcNow; }
            return fresh;
        }

        /// <summary>Löscht alle Bestzeiten (game == null) oder die einer Simulation. Die Einstellung bleibt erhalten.</summary>
        internal static int Clear(string game = null)
        {
            int removed = 0;
            lock (registry)
            {
                if (game == null) stores.Clear(); else stores.Remove(game);
                try
                {
                    if (!Directory.Exists(Folder)) return 0;
                    foreach (var file in Directory.GetFiles(Folder, "*.json"))
                    {
                        if (file.EndsWith("einstellungen.json", StringComparison.OrdinalIgnoreCase)) continue;
                        if (game != null && !string.Equals(Path.GetFileName(file), FileFor(game), StringComparison.OrdinalIgnoreCase)) continue;
                        File.Delete(file); removed++;
                    }
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
                overviewCache = null;
            }
            return removed;
        }

        internal PersonalBestStore(string folder, string game)
        {
            Game = game;
            if (folder == null) { writable = false; return; }
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, FileFor(game));
            try
            {
                if (!File.Exists(path)) return;
                if (new FileInfo(path).Length > 4 * 1024 * 1024) { writable = false; return; }
                var data = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                if ((int?)data["schema"] != 1 || !(data["bestzeiten"] is JArray items)) { writable = false; return; }
                foreach (var item in items.OfType<JObject>().Take(Capacity))
                {
                    var b = PersonalBest.FromJson(item);
                    // Nur Einträge dieser Simulation – eine falsch einsortierte Zeile wird nie übernommen.
                    if (b != null && string.Equals(RaceFactContext.Label(b.Game), RaceFactContext.Label(game), StringComparison.Ordinal)) bests[b.Key] = b;
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException || e is FormatException ||
                e is InvalidCastException || e is OverflowException || e is ArgumentException)
            { writable = false; } // Eine kaputte Datei wird nie überschrieben.
        }

        internal PersonalBest Find(string game, string car, string track, string layout)
        {
            if (!string.Equals(RaceFactContext.Label(game), RaceFactContext.Label(Game), StringComparison.Ordinal)) return null;
            lock (sync) return bests.TryGetValue(PersonalBest.Key(game, car, track, layout), out var b) ? b : null;
        }

        internal int Count { get { lock (sync) return bests.Count; } }

        /// <summary>Bietet eine gültige Runde an. Liefert true bei neuer Allzeit-Bestzeit; previous ist die alte (oder null).</summary>
        internal bool Offer(PersonalBest lap, out PersonalBest previous)
        {
            previous = null;
            if (lap == null || !(lap.LapSeconds > 10 && lap.LapSeconds < 3600) || string.IsNullOrWhiteSpace(lap.Track) || string.IsNullOrWhiteSpace(lap.Car)) return false;
            if (!string.Equals(RaceFactContext.Label(lap.Game), RaceFactContext.Label(Game), StringComparison.Ordinal)) return false; // nie in die Datenbank einer anderen Simulation
            lock (sync)
            {
                bests.TryGetValue(lap.Key, out previous);
                // Beste Einzelsektoren immer mitführen – auch aus Runden, die insgesamt nicht schneller waren.
                if (previous != null && PersonalBest.GoodSectors(lap.Sectors))
                {
                    var merged = PersonalBest.GoodSectors(previous.BestSectors) && previous.BestSectors.Length == lap.Sectors.Length
                        ? previous.BestSectors.Zip(lap.Sectors, (a, b) => Math.Min(a, b)).ToArray() : (float[])lap.Sectors.Clone();
                    bool sectorGain = !PersonalBest.GoodSectors(previous.BestSectors) || merged.Where((v, i) => v < previous.BestSectors[i] - .0005f).Any();
                    previous.BestSectors = merged;
                    if (lap.LapSeconds >= previous.LapSeconds - .0005f) { if (sectorGain) Save(); return false; }
                    lap.BestSectors = merged;
                }
                else if (previous != null && lap.LapSeconds >= previous.LapSeconds - .0005f) return false;
                if (lap.BestSectors == null && PersonalBest.GoodSectors(lap.Sectors)) lap.BestSectors = (float[])lap.Sectors.Clone();
                if (previous == null && bests.Count >= Capacity) return false;
                lap.Improvements = previous == null ? 0 : previous.Improvements + 1;
                bests[lap.Key] = lap;
            }
            Save();
            return true;
        }

        private void Save()
        {
            if (!writable || path == null) return;
            if (Interlocked.Exchange(ref pendingWrite, 1) == 1) return;
            Task.Run(() =>
            {
                try
                {
                    Interlocked.Exchange(ref pendingWrite, 0);
                    JArray items;
                    lock (sync) items = new JArray(bests.Values.OrderBy(b => b.Game).ThenBy(b => b.Track).ThenBy(b => b.Car).Select(b => b.ToJson()));
                    var data = new JObject { ["schema"] = 1, ["simulation"] = Game,
                        ["hinweis"] = "Robs Allzeit-Bestzeiten in dieser Simulation je Fahrzeug, Strecke und Layout. Lokal, wird nicht hochgeladen.", ["bestzeiten"] = items };
                    string tmp = path + ".tmp";
                    File.WriteAllText(tmp, data.ToString(Formatting.Indented), new UTF8Encoding(false));
                    if (File.Exists(path)) File.Replace(tmp, path, null); else File.Move(tmp, path);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { writable = false; }
            });
        }

        /// <summary>Baut den Eintrag für die gerade beendete Runde aus Spielzustand und Rundenaufzeichnung.</summary>
        internal static PersonalBest FromLap(string game, GameStateData gs, LapRecord lap, DateTime now)
        {
            if (gs == null || lap == null || !lap.Valid || lap.InPitLap || !(lap.LapTime > 10 && lap.LapTime < 3600)) return null;
            var ctx = RaceFactContext.FromGameState(game, gs, now);
            if (!ctx.IdentityKnown) return null;
            var c = gs.Conditions.LatestConditions;
            bool weather = c != null && c.Time != DateTime.MinValue;
            var tyres = gs.TyreData;
            string compound = !string.IsNullOrWhiteSpace(tyres.TyreTypeName) ? tyres.TyreTypeName :
                tyres.FrontLeftTyreType != TyreType.Unknown_Race && tyres.FrontLeftTyreType != TyreType.Uninitialized ? tyres.FrontLeftTyreType.ToString() : null;
            return new PersonalBest
            {
                Game = ctx.Game, Car = ctx.Car, Track = ctx.Track, Layout = ctx.Layout, CarClass = ctx.CarClass, Compound = compound,
                SessionType = gs.SessionData.SessionType.ToString(), LapSeconds = lap.LapTime, DateUtc = now,
                FuelAtStart = lap.FuelAtEnd >= 0 && lap.FuelUsed >= 0 ? lap.FuelAtEnd + lap.FuelUsed : -1,
                TrackC = weather && c.TrackTemperature > -30 && c.TrackTemperature < 90 ? c.TrackTemperature : float.NaN,
                AirC = weather && c.AmbientTemperature > -30 && c.AmbientTemperature < 60 ? c.AmbientTemperature : float.NaN,
                Rain = weather && c.RainDensity >= 0 && c.RainDensity <= 1 ? c.RainDensity : -1,
                Sectors = PersonalBest.GoodSectors(lap.SectorTimes) ? (float[])lap.SectorTimes.Clone() : null
            };
        }
    }
}
