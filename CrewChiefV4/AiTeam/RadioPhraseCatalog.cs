using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    internal sealed class RadioVariantCursor
    {
        private int heard;
        public int Heard => Volatile.Read(ref heard);
        public void OnPlaybackStarted() => Interlocked.Increment(ref heard);
    }

    /// <summary>Runtime text can only insert the facts declared for that situation.</summary>
    public sealed class RadioPhraseCatalog
    {
        public const string FileName = "Rennfunk.de.json";
        private const int MaxFileBytes = 64 * 1024;
        private static readonly Regex Slot = new Regex(@"\{([a-z]+)\}", RegexOptions.CultureInvariant);
        // Required slots first, optional slots after |. Values use the same order at the call site.
        private static readonly Dictionary<string, string> Contracts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["pass-first"] = "position|driver", ["pass-repeat"] = "position", ["pit-rival"] = "opponent",
            ["pit-gain"] = "opponent position", ["fuel-tank"] = "litres", ["fuel-range"] = "litres laps",
            ["finish-podium"] = "", ["finish-other"] = "", ["lap-best"] = "time",
            ["pace-front-faster"] = "delta opponent", ["pace-front-slower"] = "delta opponent",
            ["pace-rear-faster"] = "delta opponent", ["pace-rear-slower"] = "delta opponent",
            ["gap-front-close"] = "|opponent rival target", ["gap-front-increasing"] = "|opponent rival target",
            ["gap-front-decreasing"] = "|opponent rival target", ["gap-front-steady"] = "|opponent rival target",
            ["gap-rear-close"] = "|opponent rival target", ["gap-rear-increasing"] = "|opponent rival target",
            ["gap-rear-decreasing"] = "|opponent rival target", ["gap-rear-steady"] = "|opponent rival target"
        };
        private static readonly Lazy<RadioPhraseCatalog> Stock = new Lazy<RadioPhraseCatalog>(LoadStock);
        private static RadioPhraseCatalog current;
        private readonly Dictionary<string, string[]> groups;
        public static RadioPhraseCatalog Current => Volatile.Read(ref current) ?? Stock.Value;
        public static string EditablePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
        public static bool UsingCustomFile { get; private set; }
        public static bool CustomFileInvalid { get; private set; }

        private RadioPhraseCatalog(Dictionary<string, string[]> groups) { this.groups = groups; }

        private static RadioPhraseCatalog LoadStock()
        {
            using (var stream = typeof(RadioPhraseCatalog).Assembly.GetManifestResourceStream("RRCA.Rennfunk.de.json"))
            using (var reader = new StreamReader(stream)) return Parse(reader, null);
        }

        internal static void LoadForApplication()
        {
            var loaded = Stock.Value;
            if (DataFiles.IsolatedRoot == null && File.Exists(EditablePath))
            {
                try
                {
                    using (var file = new FileStream(EditablePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        if (file.Length > MaxFileBytes) throw new InvalidDataException();
                        using (var reader = new StreamReader(file)) loaded = Parse(reader, loaded);
                    }
                    UsingCustomFile = true;
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is UnauthorizedAccessException || e is JsonException)
                {
                    CustomFileInvalid = true;
                }
            }
            Volatile.Write(ref current, loaded);
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

        internal static string SafeRivalName(string text) => new string((text ?? "").Where(c =>
            char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '\'').Take(60).ToArray()).Trim();

        public int Count(string group) => groups[group].Length;

        public string Preview(string situation, int variant, string driverName)
        {
            switch (situation)
            {
                case "pass": return Format("pass-repeat", variant, "7") + " Du bist knapp hinter Müller.";
                case "gap": return Format("gap-front-decreasing", variant, "Müller", "Müller", "Müller") + " Nach vorn 0,8 Sekunden.";
                case "pit": return Format("pit-rival", variant, "Müller");
                case "pit-gain": return Format("pit-gain", variant, "Müller", "7");
                case "fuel": return Format("fuel-range", variant, "18", "8");
                case "pace-front-faster": return Format("pace-front-faster", variant, "0,4", "Müller");
                case "lap-best": return Format("lap-best", variant, "2:06,073");
                case "finish": return "Im Ziel. Platz 3. " + Format("finish-podium", variant);
                case "pass-first": return Format("pass-first", variant, "7", SafeRivalName(driverName));
                default: return null;
            }
        }

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
