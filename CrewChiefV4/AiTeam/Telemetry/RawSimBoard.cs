using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam.Telemetry
{
    /// <summary>
    /// Rohdatentafel (Roadmap Etappe 3): Jedes Feld, das die offizielle Schnittstelle der laufenden Sim in Crew Chiefs eigener
    /// Strukturdefinition liefert, landet hier mit Wert, Zeitpunkt der letzten Änderung und Zustand, auch wenn noch niemand
    /// in der Crew es nutzt. Die Felder werden über die Strukturen selbst gefunden, es gibt keine Feldliste je Sim.
    /// Je Wagen-Liste wird nur der eigene Wagen gelesen (über das Spieler-Kennzeichen der jeweiligen Struktur).
    /// Abtastung alle 500 ms, damit die Tafel neben der Fahrphysik nichts kostet.
    /// Zustand je Feld: live (in den letzten 5 s geändert), langsam (in der letzten Minute), eingefroren (hat sich schon
    /// geändert, steht aber seit mindestens 60 s Fahrt still), konstant (seit 30 s Fahrt nie geändert), ungueltig (nie ein
    /// gültiger Zahlenwert), ruht (steht, aber ohne Fahrt dazwischen, etwa in der Box), beobachtet (noch zu kurz gesehen).
    /// </summary>
    internal sealed class RawSimBoard
    {
        internal static RawSimBoard Current { get; } = new RawSimBoard();

        internal const int MaxFields = 4000, MaxDepth = 6, SmallArray = 8, LargeArray = 16;
        internal static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(500);
        internal const double LiveSeconds = 5, SlowSeconds = 60, FrozenDrivingSeconds = 60, ConstantDrivingSeconds = 30;

        private sealed class Node
        {
            public string Path;
            public int Parent = -1;
            public Func<object, object, object> FromParent; // (parentValue, root) -> value
            public bool Leaf, Text, Enum;
            public Type EnumType;
        }

        private sealed class Field
        {
            public string Path;
            public bool Text, Enum;
            public Type EnumType;
            public double Number = double.NaN;
            public string Value;
            public bool EverValid, Seen, HadSample;
            public DateTime FirstUtc, ChangeUtc = DateTime.MinValue;
            public int Changes;
            public double Min = double.PositiveInfinity, Max = double.NegativeInfinity;
            public double DrivingSinceChange, DrivingTotal;
        }

        private readonly object gate = new object();
        private Type planType;
        private Node[] plan = new Node[0];
        private Field[] fields = new Field[0];
        private object[] scratch = new object[0];
        private string sim;
        private DateTime lastSample = DateTime.MinValue, lastSummary = DateTime.MinValue;
        private int samples;

        internal string Sim { get { lock (gate) return sim; } }
        internal int FieldCount { get { lock (gate) return fields.Length; } }

        internal void Reset()
        {
            lock (gate)
            {
                planType = null; plan = new Node[0]; fields = new Field[0]; scratch = new object[0];
                sim = null; lastSample = lastSummary = DateTime.MinValue; samples = 0;
            }
        }

        /// <summary>
        /// Wurzel der Tafel: Crew Chiefs Struktur-Hülle; bei iRacing liegen die Werte hinter der Eigenschaft Telemetry.
        /// </summary>
        internal static object Unwrap(object raw)
        {
            if (raw == null) return null;
            try
            {
                var data = raw.GetType().GetField("data", BindingFlags.Public | BindingFlags.Instance)?.GetValue(raw);
                if (data != null && data.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance).Length == 0)
                {
                    var telemetry = data.GetType().GetProperty("Telemetry", BindingFlags.Public | BindingFlags.Instance);
                    var value = telemetry?.GetValue(data);
                    if (value != null) return value;
                }
            }
            catch (Exception) { }
            return raw;
        }

        /// <summary>Ein Rohbild der Sim (Crew Chiefs Struktur-Hülle) übernehmen; höchstens alle 500 ms.</summary>
        internal bool Observe(object raw, string simName, bool driving, DateTime now)
        {
            raw = Unwrap(raw);
            if (raw == null) return false;
            lock (gate)
            {
                if (lastSample != DateTime.MinValue && now >= lastSample && now - lastSample < SampleInterval) return false;
                double dt = lastSample == DateTime.MinValue || now < lastSample ? 0 : Math.Min(5, (now - lastSample).TotalSeconds);
                lastSample = now;
                if (raw.GetType() != planType || simName != sim)
                {
                    BuildPlan(raw);
                    sim = simName;
                    samples = 0;
                    dt = 0;
                }
                for (int i = 0; i < plan.Length; i++)
                {
                    var n = plan[i];
                    object parent = n.Parent < 0 ? raw : scratch[n.Parent];
                    object v = null;
                    if (n.Parent < 0 || parent != null)
                    {
                        try { v = n.FromParent(parent, raw); } catch (Exception) { v = null; }
                    }
                    scratch[i] = v;
                }
                int f = 0;
                for (int i = 0; i < plan.Length; i++)
                {
                    if (!plan[i].Leaf) continue;
                    Update(fields[f++], scratch[i], driving, dt, now);
                }
                Array.Clear(scratch, 0, scratch.Length);
                samples++;
                if (LiveDiagnostics.Enabled && (lastSummary == DateTime.MinValue || now - lastSummary >= TimeSpan.FromMinutes(2)) && samples > 20)
                {
                    lastSummary = now;
                    var counts = CountsLocked(now);
                    LiveDiagnostics.Emit("telemetry.raw", LiveDiagnostics.Data("sim", sim, "fields", fields.Length,
                        "live", Count(counts, "live"), "slow", Count(counts, "langsam"), "frozen", Count(counts, "eingefroren"),
                        "constant", Count(counts, "konstant"), "invalid", Count(counts, "ungueltig")));
                }
                return true;
            }
        }

        private static int Count(Dictionary<string, int> counts, string key) => counts.TryGetValue(key, out int n) ? n : 0;

        private static void Update(Field field, object raw, bool driving, double dt, DateTime now)
        {
            if (!field.Seen) { field.Seen = true; field.FirstUtc = now; }
            if (driving) { field.DrivingSinceChange += dt; field.DrivingTotal += dt; }
            if (field.Text)
            {
                string text = raw as string;
                if (raw is byte[] bytes) text = Decode(bytes);
                else if (raw is char[] chars) text = new string(chars).Split('\0')[0].Trim();
                if (string.IsNullOrEmpty(text)) text = null;
                if (text != null) field.EverValid = true;
                if (field.HadSample && !string.Equals(text, field.Value, StringComparison.Ordinal))
                {
                    field.Changes++;
                    field.ChangeUtc = now;
                    field.DrivingSinceChange = 0;
                }
                field.Value = text;
                field.HadSample = true;
                return;
            }
            double number = ToNumber(raw);
            bool valid = !double.IsNaN(number) && !double.IsInfinity(number);
            if (!valid) { field.Number = double.NaN; return; }
            if (field.EverValid && number != field.Number)
            {
                field.Changes++;
                field.ChangeUtc = now;
                field.DrivingSinceChange = 0;
            }
            field.EverValid = true;
            field.Number = number;
            if (number < field.Min) field.Min = number;
            if (number > field.Max) field.Max = number;
        }

        internal static double ToNumber(object v)
        {
            if (v == null) return double.NaN;
            switch (v)
            {
                case double d: return d;
                case float f: return f;
                case int i: return i;
                case uint ui: return ui;
                case short s: return s;
                case ushort us: return us;
                case long l: return l;
                case ulong ul: return ul;
                case byte b: return b;
                case sbyte sb: return sb;
                case bool bo: return bo ? 1 : 0;
                case char c: return c;
            }
            if (v is Enum) { try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch (Exception) { return double.NaN; } }
            return double.NaN;
        }

        internal static string Decode(byte[] bytes)
        {
            if (bytes == null) return null;
            int end = Array.IndexOf(bytes, (byte)0);
            if (end < 0) end = bytes.Length;
            if (end == 0) return null;
            string s;
            try { s = Encoding.UTF8.GetString(bytes, 0, Math.Min(end, 128)); } catch (Exception) { return null; }
            s = new string(s.Where(ch => !char.IsControl(ch)).ToArray()).Trim();
            return s.Length == 0 ? null : s;
        }

        // ---------- Plan: Felder über die Strukturen finden ----------

        private static readonly string[] SkipParts = { "VersionUpdate", "Expansion", "Unused", "Reserved", "Padding", "ticks", "Ticks" };
        private static readonly string[] TextParts = { "Name", "String", "Message", "Track", "Car", "Driver", "Class", "Layout", "Team",
            "Category", "Choice", "Compound", "Description", "Model", "Series", "Session", "Status", "Tyre", "Tire" };
        private static readonly string[] ViewedIndexNames = { "mViewedParticipantIndex", "PlayerCarIdx", "playerCarIndex", "PlayerCarIndex" };

        private void BuildPlan(object raw)
        {
            planType = raw.GetType();
            var nodes = new List<Node>();
            var resolvers = new Resolvers(raw);
            Walk(raw.GetType(), raw, "", -1, 0, nodes, resolvers, new HashSet<Type>());
            plan = nodes.ToArray();
            scratch = new object[plan.Length];
            fields = plan.Where(n => n.Leaf).Select(n => new Field { Path = n.Path, Text = n.Text, Enum = n.Enum, EnumType = n.EnumType }).ToArray();
        }

        private static bool Skip(string name) => SkipParts.Any(p => name.IndexOf(p, StringComparison.Ordinal) >= 0);
        private static bool TextLike(string name) => TextParts.Any(p => name.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0);
        private static bool Scalar(Type t) => t.IsPrimitive || t.IsEnum;

        private static IEnumerable<FieldInfo> Fields(Type t) =>
            t.GetFields(BindingFlags.Public | BindingFlags.Instance).Where(f => !f.IsDefined(typeof(ObsoleteAttribute), false) && !Skip(f.Name));

        private static int Leaves(List<Node> nodes) => nodes.Count(n => n.Leaf);

        private static void Walk(Type type, object sample, string path, int parent, int depth, List<Node> nodes, Resolvers resolvers, HashSet<Type> stack)
        {
            if (depth > MaxDepth || Leaves(nodes) >= MaxFields || !stack.Add(type)) return;
            try
            {
                foreach (var fi in Fields(type))
                {
                    if (Leaves(nodes) >= MaxFields) return;
                    var info = fi;
                    var ft = fi.FieldType;
                    string p = path.Length == 0 ? fi.Name : path + "." + fi.Name;
                    object child = null;
                    if (sample != null) { try { child = fi.GetValue(sample); } catch (Exception) { child = null; } }
                    Func<object, object, object> get = (o, root) => info.GetValue(o);
                    if (Scalar(ft)) { nodes.Add(new Node { Path = p, Parent = parent, FromParent = get, Leaf = true, Enum = ft.IsEnum, EnumType = ft.IsEnum ? ft : null }); continue; }
                    if (ft == typeof(string)) { nodes.Add(new Node { Path = p, Parent = parent, FromParent = get, Leaf = true, Text = true }); continue; }
                    if (ft.IsArray)
                    {
                        var et = ft.GetElementType();
                        var array = child as Array;
                        if (et == typeof(byte) || et == typeof(sbyte) || et == typeof(char))
                        {
                            if (TextLike(fi.Name)) nodes.Add(new Node { Path = p, Parent = parent, FromParent = get, Leaf = true, Text = true });
                            else if (array != null && array.Length <= 4) AddSmallArray(et, array, p, parent, depth, get, nodes, resolvers, stack);
                            continue;
                        }
                        if (array == null || array.Rank != 1) continue;
                        if (array.Length <= SmallArray) { AddSmallArray(et, array, p, parent, depth, get, nodes, resolvers, stack); continue; }
                        var resolve = resolvers.For(p, et, array.Length);
                        if (resolve == null) continue;
                        nodes.Add(new Node { Path = p, Parent = parent, FromParent = get });
                        int arrayNode = nodes.Count - 1;
                        Func<object, object, object> mine = (a, root) =>
                        {
                            var arr = a as Array; int k = resolve(root);
                            return arr != null && k >= 0 && k < arr.Length ? arr.GetValue(k) : null;
                        };
                        string ep = p + "[ich]";
                        object elementSample = null;
                        try { int k = resolve(resolvers.Root); if (k >= 0 && k < array.Length) elementSample = array.GetValue(k); else elementSample = array.GetValue(0); }
                        catch (Exception) { elementSample = null; }
                        if (Scalar(et)) nodes.Add(new Node { Path = ep, Parent = arrayNode, FromParent = mine, Leaf = true, Enum = et.IsEnum, EnumType = et.IsEnum ? et : null });
                        else if (!et.IsArray)
                        {
                            nodes.Add(new Node { Path = ep, Parent = arrayNode, FromParent = mine });
                            Walk(et, elementSample, ep, nodes.Count - 1, depth + 1, nodes, resolvers, stack);
                        }
                        continue;
                    }
                    if ((ft.IsValueType || ft.IsClass) && ft.Namespace != null && !ft.Namespace.StartsWith("System", StringComparison.Ordinal))
                    {
                        nodes.Add(new Node { Path = p, Parent = parent, FromParent = get });
                        Walk(ft, child, p, nodes.Count - 1, depth + 1, nodes, resolvers, stack);
                    }
                }
            }
            finally { stack.Remove(type); }
        }

        private static void AddSmallArray(Type et, Array array, string p, int parent, int depth, Func<object, object, object> get,
            List<Node> nodes, Resolvers resolvers, HashSet<Type> stack)
        {
            nodes.Add(new Node { Path = p, Parent = parent, FromParent = get });
            int arrayNode = nodes.Count - 1;
            for (int i = 0; i < array.Length; i++)
            {
                int index = i;
                Func<object, object, object> at = (a, root) => { var arr = a as Array; return arr != null && index < arr.Length ? arr.GetValue(index) : null; };
                string ep = p + "[" + i + "]";
                if (Scalar(et)) nodes.Add(new Node { Path = ep, Parent = arrayNode, FromParent = at, Leaf = true, Enum = et.IsEnum, EnumType = et.IsEnum ? et : null });
                else if (!et.IsArray && et != typeof(string))
                {
                    nodes.Add(new Node { Path = ep, Parent = arrayNode, FromParent = at });
                    object sample = null;
                    try { sample = array.GetValue(i); } catch (Exception) { sample = null; }
                    Walk(et, sample, ep, nodes.Count - 1, depth + 1, nodes, resolvers, stack);
                }
            }
        }

        /// <summary>Findet den eigenen Wagen in den Wagen-Listen der Struktur.</summary>
        private sealed class Resolvers
        {
            internal readonly object Root;
            private readonly List<Func<object, int>> viewedIndex = new List<Func<object, int>>();
            private Func<object, int> playerId;

            internal Resolvers(object root)
            {
                Root = root;
                Scan(root?.GetType(), (o) => o, 0);
            }

            // Spieler-Index-Felder (AMS2, iRacing) und die Wagen-Liste mit Spieler-Kennzeichen und ID (rF2/LMU) bis Tiefe 3 suchen.
            private void Scan(Type t, Func<object, object> get, int depth)
            {
                if (t == null || depth > 3) return;
                foreach (var fi in Fields(t))
                {
                    var info = fi;
                    var ft = fi.FieldType;
                    Func<object, object> g = o => { var parent = get(o); return parent == null ? null : info.GetValue(parent); };
                    if ((ft == typeof(int) || ft == typeof(uint) || ft == typeof(short)) && ViewedIndexNames.Contains(fi.Name))
                        viewedIndex.Add(o => { try { return Convert.ToInt32(g(o), CultureInfo.InvariantCulture); } catch (Exception) { return -1; } });
                    else if (ft.IsArray && !Scalar(ft.GetElementType()) && playerId == null)
                    {
                        var et = ft.GetElementType();
                        var isPlayer = et.GetField("mIsPlayer"); var id = et.GetField("mID");
                        if (isPlayer != null && id != null)
                            playerId = o =>
                            {
                                if (!(g(o) is Array arr)) return int.MinValue;
                                foreach (var e in arr)
                                    if (e != null && ToNumber(isPlayer.GetValue(e)) != 0) return (int)ToNumber(id.GetValue(e));
                                return int.MinValue;
                            };
                    }
                    else if (!ft.IsArray && !Scalar(ft) && ft != typeof(string) && ft.Namespace != null && !ft.Namespace.StartsWith("System", StringComparison.Ordinal))
                        Scan(ft, g, depth + 1);
                }
            }

            internal Func<object, int> For(string path, Type elementType, int length)
            {
                var isPlayer = elementType.GetField("mIsPlayer");
                if (isPlayer != null && !Scalar(elementType))
                {
                    Func<object, object> arrayAt = ArrayGetter(path);
                    return root =>
                    {
                        if (!(arrayAt(root) is Array arr)) return -1;
                        for (int i = 0; i < arr.Length; i++) { var e = arr.GetValue(i); if (e != null && ToNumber(isPlayer.GetValue(e)) != 0) return i; }
                        return -1;
                    };
                }
                var id = elementType.GetField("mID");
                if (id != null && playerId != null && !Scalar(elementType))
                {
                    Func<object, object> arrayAt = ArrayGetter(path);
                    var pid = playerId;
                    return root =>
                    {
                        int want = pid(root);
                        if (want == int.MinValue || !(arrayAt(root) is Array arr)) return -1;
                        for (int i = 0; i < arr.Length; i++) { var e = arr.GetValue(i); if (e != null && (int)ToNumber(id.GetValue(e)) == want) return i; }
                        return -1;
                    };
                }
                var driverInfo = elementType.GetField("DriverInfo");
                var slot = driverInfo?.FieldType.GetField("SlotId");
                var vehicleInfo = Root?.GetType().GetField("data")?.FieldType.GetField("VehicleInfo") ?? Root?.GetType().GetField("VehicleInfo");
                var ownSlot = vehicleInfo?.FieldType.GetField("SlotId");
                if (slot != null && ownSlot != null)
                {
                    Func<object, object> arrayAt = ArrayGetter(path);
                    return root =>
                    {
                        object data = root.GetType().GetField("data")?.GetValue(root) ?? root;
                        var vi = vehicleInfo.DeclaringType.IsInstanceOfType(data) ? vehicleInfo.GetValue(data) : null;
                        if (vi == null || !(arrayAt(root) is Array arr)) return -1;
                        double want = ToNumber(ownSlot.GetValue(vi));
                        if (want < 0) return -1;
                        for (int i = 0; i < arr.Length; i++)
                        {
                            var e = arr.GetValue(i); if (e == null) continue;
                            var di = driverInfo.GetValue(e);
                            if (di != null && ToNumber(slot.GetValue(di)) == want) return i;
                        }
                        return -1;
                    };
                }
                if (viewedIndex.Count > 0 && length >= LargeArray)
                {
                    var first = viewedIndex[0];
                    return root => first(root);
                }
                return null;
            }

            // Getter für ein Feld über seinen Punktpfad ab der Wurzel (nur Feldnamen, keine Indizes).
            private Func<object, object> ArrayGetter(string path)
            {
                var names = path.Split('.');
                return root =>
                {
                    object o = root;
                    foreach (var n in names)
                    {
                        if (o == null) return null;
                        var fi = o.GetType().GetField(n, BindingFlags.Public | BindingFlags.Instance);
                        if (fi == null) return null;
                        o = fi.GetValue(o);
                    }
                    return o;
                };
            }
        }

        // ---------- Abfrage ----------

        private string StatusLocked(Field f, DateTime now)
        {
            if (!f.EverValid) return f.Text ? "leer" : "ungueltig";
            if (f.Changes == 0) return f.DrivingTotal >= ConstantDrivingSeconds ? "konstant" : "beobachtet";
            double since = (now - f.ChangeUtc).TotalSeconds;
            if (since <= LiveSeconds) return "live";
            if (since <= SlowSeconds) return "langsam";
            return f.DrivingSinceChange >= FrozenDrivingSeconds ? "eingefroren" : "ruht";
        }

        private Dictionary<string, int> CountsLocked(DateTime now)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var f in fields)
            {
                string s = StatusLocked(f, now);
                counts[s] = counts.TryGetValue(s, out int n) ? n + 1 : 1;
            }
            return counts;
        }

        internal string Status(string path, DateTime now)
        {
            lock (gate)
            {
                var f = fields.FirstOrDefault(x => x.Path == path);
                return f == null ? null : StatusLocked(f, now);
            }
        }

        internal double Number(string path)
        {
            lock (gate) { var f = fields.FirstOrDefault(x => x.Path == path); return f == null ? double.NaN : f.Number; }
        }

        private JObject Row(Field f, DateTime now)
        {
            var o = new JObject { ["feld"] = f.Path };
            if (f.Text) o["wert"] = f.Value;
            else if (double.IsNaN(f.Number)) o["wert"] = null;
            else
            {
                o["wert"] = Math.Round(f.Number, 4);
                if (f.Enum && f.EnumType != null)
                {
                    try { o["bedeutung"] = System.Enum.ToObject(f.EnumType, (long)f.Number).ToString(); } catch (Exception) { }
                }
            }
            o["status"] = StatusLocked(f, now);
            if (f.ChangeUtc != DateTime.MinValue) o["letzte_aenderung_vor_s"] = Math.Round(Math.Max(0, (now - f.ChangeUtc).TotalSeconds), 1);
            double minutes = Math.Max(1.0 / 60, (now - f.FirstUtc).TotalMinutes);
            o["aenderungen_pro_min"] = Math.Round(f.Changes / minutes, 1);
            if (!f.Text && f.EverValid && f.Changes > 0) { o["min"] = Math.Round(f.Min, 4); o["max"] = Math.Round(f.Max, 4); }
            return o;
        }

        internal JObject Summary(DateTime now)
        {
            lock (gate)
            {
                if (fields.Length == 0) return new JObject { ["verfuegbar"] = false };
                var counts = CountsLocked(now);
                var o = new JObject { ["verfuegbar"] = true, ["sim"] = sim, ["felder"] = fields.Length, ["abtastungen"] = samples };
                var c = new JObject();
                foreach (var p in counts.OrderBy(p => p.Key)) c[p.Key] = p.Value;
                o["zustaende"] = c;
                return o;
            }
        }

        /// <summary>Felder suchen. Mehrere Suchwörter (Leerzeichen) müssen alle im Feldpfad vorkommen.</summary>
        internal JObject Describe(string search, bool liveOnly, int max, DateTime now)
        {
            lock (gate)
            {
                if (fields.Length == 0)
                    return new JObject { ["verfuegbar"] = false, ["grund"] = "Noch kein Rohbild der Sim gelesen (Sim nicht aktiv oder Crew Chief nicht gestartet)." };
                var terms = (search ?? "").Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
                var hits = fields.Where(f => terms.All(t => f.Path.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0));
                if (liveOnly) hits = hits.Where(f => { string s = StatusLocked(f, now); return s == "live" || s == "langsam"; });
                var list = hits.ToList();
                int limit = Math.Max(1, Math.Min(150, max));
                var o = Summary(now);
                o["suche"] = search ?? "";
                o["treffer"] = list.Count;
                o["felder_liste"] = new JArray(list.OrderBy(f => Rank(StatusLocked(f, now))).ThenBy(f => f.Path, StringComparer.Ordinal)
                    .Take(limit).Select(f => Row(f, now)));
                if (list.Count > limit) o["gekuerzt"] = true;
                o["hinweis"] = "Rohfelder aus der offiziellen Schnittstelle der Sim, ohne Umrechnung. Einheiten stehen nicht im Feld; " +
                    "Feldnamen und Bedeutung nur mit Vorsicht deuten. „eingefroren“ heißt: hat sich früher bewegt, steht aber seit mindestens " +
                    "60 s Fahrt still. [ich] = eigener Wagen.";
                return o;
            }
        }

        private static int Rank(string status)
        {
            switch (status)
            {
                case "live": return 0;
                case "langsam": return 1;
                case "eingefroren": return 2;
                case "ruht": return 3;
                case "konstant": return 4;
                case "beobachtet": return 5;
                default: return 6;
            }
        }

        internal static JObject ToolParameters() => new JObject
        {
            ["type"] = "object",
            ["properties"] = new JObject
            {
                ["suche"] = new JObject { ["type"] = "string", ["description"] = "Suchwörter im Feldnamen, z. B. \"tyre wear\", \"fuel\", \"brake temp\", \"energy\", \"pit\". Leer = alle." },
                ["nur_live"] = new JObject { ["type"] = "boolean", ["description"] = "Nur Felder, die sich in der letzten Minute bewegt haben." },
                ["max"] = new JObject { ["type"] = "integer", ["description"] = "Höchstzahl Felder (1 bis 150, Standard 60)." }
            },
            ["additionalProperties"] = false
        };

        internal static JObject Tool(string rawArguments, DateTime now)
        {
            JObject args;
            try { args = JObject.Parse(string.IsNullOrWhiteSpace(rawArguments) ? "{}" : rawArguments); }
            catch (Exception) { return new JObject { ["fehler"] = "Argumente müssen ein JSON-Objekt sein." }; }
            string search = args.Value<string>("suche");
            bool liveOnly = args["nur_live"]?.Type == JTokenType.Boolean && (bool)args["nur_live"];
            int max = args["max"]?.Type == JTokenType.Integer ? (int)args["max"] : 60;
            return Current.Describe(search, liveOnly, max, now);
        }
    }
}
