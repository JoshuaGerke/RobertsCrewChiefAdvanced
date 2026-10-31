using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Bounded, read-only access to the complete mapped field, including cars outside our neighbourhood.</summary>
    public static class RaceFieldTools
    {
        public static JObject Parameters(bool lookup)
        {
            var properties = lookup ? new JObject
            {
                ["name"] = new JObject { ["type"] = "string", ["maxLength"] = 100 },
                ["car_number"] = new JObject { ["type"] = "string", ["maxLength"] = 16 },
                ["overall_position"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 256 },
            } : new JObject
            {
                ["offset"] = new JObject { ["type"] = "integer", ["minimum"] = 0, ["maximum"] = 256 },
                ["limit"] = new JObject { ["type"] = "integer", ["minimum"] = 1, ["maximum"] = 20 },
            };
            return new JObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
        }

        public static JObject Read(string tool, RaceDataContext ctx, string rawArguments)
        {
            JObject args;
            try
            {
                if ((rawArguments ?? "").Length > 2048) return Error("Argumente zu lang.");
                args = JObject.Parse(string.IsNullOrWhiteSpace(rawArguments) ? "{}" : rawArguments);
            }
            catch (Exception) { return Error("Argumente müssen ein JSON-Objekt sein."); }
            bool lookup = tool == "get_opponent";
            var schema = (JObject)Parameters(lookup)["properties"];
            foreach (var p in args.Properties())
            {
                var rule = schema[p.Name];
                if (rule == null) return Error("Unbekanntes Argument.");
                if ((string)rule["type"] == "integer")
                {
                    if (p.Value.Type != JTokenType.Integer || !long.TryParse(p.Value.ToString(), out long n) ||
                        n < (int)rule["minimum"] || n > (int)rule["maximum"]) return Error("Ganzzahl außerhalb des erlaubten Bereichs.");
                }
                else if (p.Value.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)p.Value) ||
                    ((string)p.Value).Length > (int)rule["maxLength"]) return Error("Ungültiger Suchtext.");
            }
            if (lookup && args.Count != 1) return Error("Genau einen Fahrer über name, car_number oder overall_position wählen.");
            if (ctx == null || ctx.GameState == null) return Error("Keine Telemetrie.");
            var result = new JObject
            {
                ["quelle"] = "Crew-Chief-GameState / " + ctx.GameName,
                ["zeitpunkt_utc"] = ctx.LastTelemetryUtc.ToString("o"),
                ["datenalter_s"] = ctx.TelemetryAgeSeconds > 9999 ? (JToken)null : Math.Round(ctx.TelemetryAgeSeconds, 3),
                ["veraltet"] = ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > RaceDataTools.StaleAfterSeconds,
                ["umfang"] = "Vom Spieladapter erfasste Gegner; eigener Fahrer separat in get_race_state. Keine Garantie für ein vollständiges Starterfeld.",
                ["nicht_verfuegbar"] = new JArray("Gegner-Reifenverschleiß", "Gegner-Reifentemperatur", "kurvenweiser Gegnervergleich"),
            };
            var field = ctx.GameState.OpponentData.Values.Where(o => o != null).OrderBy(o => o.OverallPosition > 0 ? o.OverallPosition : int.MaxValue).ToList();
            result["erfasste_gegner"] = field.Count;
            IEnumerable<OpponentData> selected = field;
            if (lookup)
            {
                if (args["name"] != null)
                {
                    if (!ctx.SendDriverNames) return Error("Namensfreigabe deaktiviert. Startnummer oder Gesamtposition verwenden.");
                    string name = Normalize((string)args["name"]);
                    selected = field.Where(o => Normalize(o.DriverRawName) == name).ToList();
                    if (!selected.Any()) selected = field.Where(o => Normalize(o.DriverRawName).EndsWith(" " + name, StringComparison.Ordinal));
                }
                else if (args["car_number"] != null) selected = field.Where(o => o.CarNumber == (string)args["car_number"]);
                else selected = field.Where(o => o.OverallPosition == (int)args["overall_position"]);
                var matches = selected.ToList();
                result["eindeutig"] = matches.Count == 1;
                result["trefferzahl"] = matches.Count;
                result["fahrer"] = new JArray(matches.Take(20).Select(o => Describe(o, ctx)));
                if (matches.Count != 1) result["hinweis"] = matches.Count == 0 ? "Fahrer nicht gefunden; keine eigenen Werte einsetzen." : "Mehrdeutig. Fahrer über Startnummer oder Gesamtposition klären.";
            }
            else
            {
                int offset = (int?)args["offset"] ?? 0, limit = (int?)args["limit"] ?? 20;
                result["fahrer"] = new JArray(selected.Skip(offset).Take(limit).Select(o => Describe(o, ctx)));
                result["naechster_offset"] = offset + limit < field.Count ? (JToken)(offset + limit) : null;
            }
            return result;
        }

        private static JObject Describe(OpponentData o, RaceDataContext ctx)
        {
            var result = RaceDataTools.Describe(o, ctx, ctx.GameState.SessionData);
            result["position_gesamt"] = o.OverallPosition > 0 ? (JToken)o.OverallPosition : null;
            result["startnummer"] = string.IsNullOrEmpty(o.CarNumber) || o.CarNumber == "-1" ? null : o.CarNumber;
            result["aktiv"] = o.IsActive;
            result["streckenposition"] = TrackAwareness.OpponentPosition(ctx.GameState,o);
            result["runden_absolviert"] = o.CompletedLaps;
            var lap = o.OpponentLapData.LastOrDefault(l => l.LapTime > 0);
            result["letzte_runde_gueltig"] = lap != null ? (JToken)lap.IsValid : null;
            return result;
        }

        public static string Normalize(string value)
        {
            string decomposed = (value ?? "").Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in decomposed)
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
            return string.Join(" ", sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }

        public static List<OpponentData> NamedInQuestion(string question, GameStateData gs)
        {
            string q = " " + Normalize(question) + " ";
            var field = gs.OpponentData.Values.Where(o => !string.IsNullOrWhiteSpace(o.DriverRawName)).ToList();
            var full = field.Where(o => q.Contains(" " + Normalize(o.DriverRawName) + " ")).ToList();
            if (full.Count > 0) return full;
            return field.Where(o => { string surname = Normalize(o.DriverRawName).Split(' ').Last(); return surname.Length >= 3 && q.Contains(" " + surname + " "); }).ToList();
        }

        private static JObject Error(string text) { return new JObject { ["fehler"] = text, ["verfuegbar"] = false }; }
    }
}
