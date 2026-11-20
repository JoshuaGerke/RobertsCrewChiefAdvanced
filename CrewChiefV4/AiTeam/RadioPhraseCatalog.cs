using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    internal sealed class RadioVariantCursor
    {
        private sealed class Group
        {
            public readonly Queue<int> Recent = new Queue<int>(3);
            public int Candidate = -1;
        }
        private static readonly Random random = new Random();
        private readonly Dictionary<string, Group> groups = new Dictionary<string, Group>(StringComparer.Ordinal);
        private int heard;
        public int Heard => Volatile.Read(ref heard);
        public int Select(string name, int count)
        {
            if (count < 2) return 0;
            lock (groups)
            {
                if (!groups.TryGetValue(name, out var group)) groups[name] = group = new Group();
                if (group.Candidate >= 0 && group.Candidate < count && !group.Recent.Contains(group.Candidate))
                    return group.Candidate;
                int blocked = Math.Min(3, count - 1);
                while (group.Recent.Count > blocked) group.Recent.Dequeue();
                if (count < 5)
                {
                    int next = Heard % count;
                    while (group.Recent.Contains(next)) next = (next + 1) % count;
                    return group.Candidate = next;
                }
                if (group.Recent.Count == 0) return group.Candidate = 0;
                var available = Enumerable.Range(0, count).Where(i => !group.Recent.Contains(i)).ToArray();
                int pick;
                lock (random) pick = random.Next(available.Length);
                return group.Candidate = available[pick];
            }
        }
        public void OnPlaybackCompleted(string name = null, int index = -1)
        {
            if (name != null && index >= 0)
            {
                lock (groups)
                {
                    if (!groups.TryGetValue(name, out var group)) groups[name] = group = new Group();
                    if (group.Recent.Count == 3) group.Recent.Dequeue();
                    group.Recent.Enqueue(index);
                    group.Candidate = -1;
                }
            }
            Interlocked.Increment(ref heard);
        }
    }

    /// <summary>Runtime text can only insert the facts declared for that situation.</summary>
    public sealed class RadioPhraseCatalog
    {
        // Satzanfänge, die nach vorangestelltem Namen klein geschrieben werden (keine Substantive, keine Namen).
        private static readonly HashSet<string> LowerableStarts = new HashSet<string>(StringComparer.Ordinal)
        {
            "Du", "Wir", "Das", "Der", "Die", "Den", "Dem", "Ein", "Eine", "Einen", "Jetzt", "Und", "Noch", "Wieder", "Gut", "Sauber", "Sauberer",
            "Stark", "Starker", "Neue", "Neuer", "Nach", "In", "Ja", "Jawohl", "Sehr", "Deine", "Dein", "Da", "Von", "Mit", "Schnellste", "Persönliche",
            "Letzte", "Letzter", "Letztes", "Erste", "Erster", "Zwei", "Drei", "Kleiner", "Kleine", "Nur", "Bleib", "Halt",
            "Last", "Final", "First", "Just", "Stay", "Keep", "Última", "Último", "Primera", "Primer", "Solo", "Sigue", "Mantén",
            "You", "We", "The", "A", "And", "Another", "New", "Nice", "Good", "Great", "Strong", "Back", "Fastest", "Your", "That", "Now", "One", "Clean", "Yes",
            "Has", "Hemos", "Otra", "Otro", "Un", "Una", "El", "La", "Los", "Nueva", "Nuevo", "Bien", "Buen", "Buena", "Muy", "Ya", "Ahora", "Tu"
        };

        /// <summary>Spricht den Fahrer mit Namen an („Rob, …“). Nur bekannte Satzanfänge werden klein geschrieben; Substantive und Namen bleiben.</summary>
        internal static string Address(string line, string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0 || string.IsNullOrEmpty(line) || line.StartsWith(name + ",", StringComparison.Ordinal)) return line;
            int end = 0;
            while (end < line.Length && char.IsLetter(line[end])) end++;
            string rest = end > 0 && LowerableStarts.Contains(line.Substring(0, end)) ? char.ToLower(line[0], RadioLanguage.Culture) + line.Substring(1) : line;
            return name + ", " + rest;
        }

        public const string FileName = "Rennfunk.de.json";
        /// <summary>Katalogdatei der jeweiligen Funksprache.</summary>
        public static string FileNameFor(string language) => "Rennfunk." + language + ".json";
        private const int MaxFileBytes = 64 * 1024;
        private static readonly Regex Slot = new Regex(@"\{([a-z]+)\}", RegexOptions.CultureInvariant);
        // Required slots first, optional slots after |. Values use the same order at the call site.
        private static readonly Dictionary<string, string> Contracts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pass-first"] = "position|driver", ["pass-repeat"] = "position", ["pit-rival"] = "opponent",
            ["pit-gain"] = "opponent position", ["fuel-tank"] = "litres", ["fuel-range"] = "litres laps",
            ["finish-podium"] = "", ["finish-other"] = "", ["race-disqualified"] = "|driver", ["lap-best"] = "time",
            ["lap-first-valid"] = "time", ["lap-near-best"] = "delta",
            ["quali-sector-comparison"] = "sector time delta direction opponent",
            ["own-sector-best"] = "sector time", ["own-sector-best-push"] = "", ["own-best-pace"] = "delta",
            ["stint-consistency"] = "time|driver", ["race-defense-hold"] = "opponent interval|driver", ["fuel-saving-review"] = "average comparison", ["race-chase-review"] = "result", ["pit-exit-forecast"] = "forecast", ["crew-thermal-pit-handover"] = "",
            ["corner-improvement"] = "corner gain",
            ["pit-undercut-overcut-result"] = "opponent result interval change",
            ["race-position-loss"] = "from position",
            ["race-class-best"] = "opponent time",
            ["race-overall-best"] = "opponent time",
            ["race-rear-pressure"] = "opponent interval", ["rival-sector-loss"] = "opponent sector interval", ["reader-temperature-context"] = "heat trend",
            ["gap-front-measured"] = "interval opponent", ["gap-rear-measured"] = "interval opponent",
            ["quali-first"] = "position", ["quali-top3"] = "position", ["quali-field"] = "position",
            ["quali-provisional"] = "position", ["quali-unknown"] = "",
            ["race-last"] = "driver", ["race-closing"] = "laps driver",
            ["pace-front-faster"] = "delta opponent", ["pace-front-slower"] = "delta opponent",
            ["pace-rear-faster"] = "delta opponent", ["pace-rear-slower"] = "delta opponent",
            ["gap-front-close"] = "|opponent rival target", ["gap-front-increasing"] = "|opponent rival target",
            ["gap-front-decreasing"] = "|opponent rival target", ["gap-front-steady"] = "|opponent rival target",
            ["gap-rear-close"] = "|opponent rival target", ["gap-rear-increasing"] = "|opponent rival target",
            ["gap-rear-decreasing"] = "|opponent rival target", ["gap-rear-steady"] = "|opponent rival target"
        };
        private static readonly Lazy<RadioPhraseCatalog> Stock = new Lazy<RadioPhraseCatalog>(() => LoadStock(RadioLanguage.German));
        private static readonly Lazy<RadioPhraseCatalog> StockEn = new Lazy<RadioPhraseCatalog>(() => LoadStock(RadioLanguage.English));
        private static readonly Lazy<RadioPhraseCatalog> StockEs = new Lazy<RadioPhraseCatalog>(() => LoadStock(RadioLanguage.Spanish));
        private static RadioPhraseCatalog current, currentEn, currentEs;
        private readonly Dictionary<string, string[]> groups;
        /// <summary>Katalog der aktuellen Funksprache.</summary>
        public static RadioPhraseCatalog Current => RadioLanguage.Pick(
            Volatile.Read(ref current) ?? Stock.Value,
            Volatile.Read(ref currentEn) ?? StockEn.Value,
            Volatile.Read(ref currentEs) ?? StockEs.Value);
        public static string EditablePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
        public static bool UsingCustomFile { get; private set; }
        public static bool CustomFileInvalid { get; private set; }

        private RadioPhraseCatalog(Dictionary<string, string[]> groups) { this.groups = groups; }

        private static RadioPhraseCatalog LoadStock(string language)
        {
            using (var stream = typeof(RadioPhraseCatalog).Assembly.GetManifestResourceStream("RRCA." + FileNameFor(language)))
            using (var reader = new StreamReader(stream)) return Parse(reader, null);
        }

        internal static void LoadForApplication()
        {
            Volatile.Write(ref current, LoadWithOverride(RadioLanguage.German, Stock.Value));
            Volatile.Write(ref currentEn, LoadWithOverride(RadioLanguage.English, StockEn.Value));
            Volatile.Write(ref currentEs, LoadWithOverride(RadioLanguage.Spanish, StockEs.Value));
        }

        // Eine bearbeitete Katalogdatei neben der exe ersetzt einzelne Gruppen ihrer Sprache
        private static RadioPhraseCatalog LoadWithOverride(string language, RadioPhraseCatalog loaded)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileNameFor(language));
            if (DataFiles.IsolatedRoot == null && File.Exists(path))
            {
                try
                {
                    using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (file.Length > MaxFileBytes) throw new InvalidDataException();
                        using (var reader = new StreamReader(file)) loaded = Parse(reader, loaded);
                    }
                    if (language == RadioLanguage.German) UsingCustomFile = true;
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is JsonException)
                {
                    if (language == RadioLanguage.German) CustomFileInvalid = true;
                }
            }
            return loaded;
        }

        public static RadioPhraseCatalog ParseOverrides(TextReader reader) => Parse(reader, Stock.Value);

        private static RadioPhraseCatalog Parse(TextReader reader, RadioPhraseCatalog defaults)
        {
            using (var json = new JsonTextReader(reader) { MaxDepth = 8, DateParseHandling = DateParseHandling.None })
            {
                var data = JObject.Load(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (json.Read() || data["version"]?.Type != JTokenType.Integer || data["version"].ToString() != "1" || !(data["groups"] is JObject entries))
                    throw new InvalidDataException("Unbekanntes Rennfunk-Dateiformat.");
                var parsed = defaults == null ? new Dictionary<string, string[]>() : new Dictionary<string, string[]>(defaults.groups);
                foreach (var property in entries.Properties())
                {
                    if (!Contracts.TryGetValue(property.Name, out var contract) || !(property.Value is JArray lines) || lines.Count < 1 || lines.Count > 16)
                        throw new InvalidDataException("Ungültige Rennfunk-Gruppe: " + property.Name);
                    var required = Keys(contract.Split('|')[0]);
                    var allowed = Keys(contract);
                    var variants = new List<string>();
                    foreach (var entry in lines)
                    {
                        var line = entry.Type == JTokenType.String ? ((string)entry).Trim() : null;
                        if (string.IsNullOrEmpty(line) || line.Length > 160 || line.Any(char.IsControl))
                            throw new InvalidDataException("Ungültiger Rennfunk-Satz: " + property.Name);
                        var used = Slot.Matches(line).Cast<Match>().Select(match => match.Groups[1].Value).ToArray();
                        if (required.Except(used).Any() || used.Except(allowed).Any() || Slot.Replace(line, "").IndexOfAny(new[] { '{', '}' }) >= 0)
                            throw new InvalidDataException("Ungültige Platzhalter: " + property.Name);
                        if (!variants.Contains(line)) variants.Add(line);
                    }
                    parsed[property.Name] = variants.ToArray();
                }
                if (Contracts.Keys.Except(parsed.Keys).Any()) throw new InvalidDataException("Rennfunk-Gruppen fehlen.");
                return new RadioPhraseCatalog(parsed);
            }
        }

        private static string[] Keys(string contract) => contract.Split(new[] { ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);

        internal static string SafeRivalName(string text)
        {
            string clean = new string((RepairEncoding(text) ?? "").Where(c =>
                char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '\'').Take(60).ToArray()).Trim();
            // LMU 18.11.: „Antonino Sammito4797“ – Online-Kennungen hinter dem Namen spricht kein Ingenieur mit.
            string withoutTag = Regex.Replace(clean, @"(?<=\p{L}{2})\d{3,}$", "");
            return withoutTag.Length >= 2 ? withoutTag.Trim() : clean;
        }

        /// <summary>
        /// LMU 18.11.: „AutÃ³dromo JosÃ© Carlos Pace“ – UTF-8 aus dem Spiel als Windows-1252 gelesen. Typische Folgen (Ã, Â, Å
        /// vor einem zweiten Byte) werden zurückübersetzt; alles andere bleibt unverändert.
        /// </summary>
        internal static string RepairEncoding(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('\u00C3') < 0 && text.IndexOf('\u00C2') < 0 && text.IndexOf('\u00C5') < 0) return text;
            try
            {
                Encoding single;
                try { single = Encoding.GetEncoding(1252); } catch (Exception) { single = Encoding.GetEncoding(28591); }
                var strict = new UTF8Encoding(false, true);
                string repaired = strict.GetString(single.GetBytes(text));
                return repaired.IndexOf('\uFFFD') < 0 ? repaired : text;
            }
            catch (Exception) { return text; }
        }

        public int Count(string group) => groups[group].Length;
        public string ReferenceFor(string group, int variant)
        {
            var options=groups[group];int index=(int)((uint)variant%options.Length);
            return "RRCA/"+group+"/"+index;
        }

        public string Preview(string situation, int variant, string driverName)
        {
            switch (situation)
            {
                case "pass": return Format("pass-repeat", variant, "7") + RadioLanguage.T(" Du bist knapp hinter Müller.", " You're right behind Miller.", " Estás justo detrás de Molina.");
                case "gap": return Format("gap-front-decreasing", variant, Rival, Rival, Rival) + RadioLanguage.T(" Nach vorn 0,8 Sekunden.", " Gap ahead 0.8 seconds.", " Por delante 0,8 segundos.");
                case "pit": return Format("pit-rival", variant, Rival);
                case "pit-gain": return Format("pit-gain", variant, Rival, "7");
                case "fuel": return Format("fuel-range", variant, "18", "8");
                case "pace-front-faster": return Format("pace-front-faster", variant, RadioLanguage.Num(0.4, "0.0"), Rival);
                case "lap-best": return Format("lap-best", variant, RadioLanguage.T("2:06,073", "2:06.073", "2:06,073"));
                case "finish": return RadioLanguage.T("Im Ziel. Platz 3. ", "Chequered flag. P3. ", "Bandera a cuadros. Tercero. ") + Format("finish-podium", variant);
                case "race-disqualified": return Format("race-disqualified", variant, LapWatch.DriverAddress(driverName));
                case "pass-first": return Format("pass-first", variant, "7", SafeRivalName(driverName));
                default: return null;
            }
        }

        private static string Rival => RadioLanguage.T("Müller", "Miller", "Molina");

        public string Format(string group, int variant, params string[] values)
        {
            var options = groups[group];
            var slots = Keys(Contracts[group]);
            if (slots.Length != values.Length) throw new ArgumentException("Ungültige Werte für Rennfunk-Gruppe.", nameof(values));
            string line = options[(int)((uint)variant % options.Length)];
            return Slot.Replace(line, match => values[Array.IndexOf(slots, match.Groups[1].Value)]);
        }
    }
}
