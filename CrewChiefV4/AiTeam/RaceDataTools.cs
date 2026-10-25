using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Kontext eines Werkzeugaufrufs: Live-Telemetrie von Crew Chief, Renngedächtnis und Spotterstatus.
    /// </summary>
    public class RaceDataContext
    {
        public GameStateData GameState;
        public AiRaceMemory Memory;
        public SpotterSnapshot Spotter;
        public DateTime NowUtc;
        public DateTime LastTelemetryUtc;
        public string GameName;
        public bool SendDriverNames = true;
        /// <summary>Ergebnis von Fuel.GetAdditionalFuelToEndOfRace (Crew-Chief-Rechnung), float.MaxValue = unbekannt.</summary>
        public float CrewChiefAdditionalFuelNeeded = float.MaxValue;
        public CornerCoach Corners;

        public double TelemetryAgeSeconds
        {
            get { return LastTelemetryUtc == DateTime.MinValue ? double.MaxValue : (NowUtc - LastTelemetryUtc).TotalSeconds; }
        }
    }

    /// <summary>
    /// Read-only-Werkzeuge für Ben und Mik (Tool Calling). Jede Antwort nennt Quelle, Alter der Daten
    /// und welche Werte die Simulation gerade NICHT liefert. Fehlende Werte werden nie geschätzt.
    /// </summary>
    public static class RaceDataTools
    {
        public const double StaleAfterSeconds = 3.0;

        public static readonly string[] AllToolNames =
        {
            "get_race_state", "get_tyre_status", "get_fuel_status", "get_lap_times", "get_opponents_nearby",
            "get_pit_info", "get_damage_and_engine", "get_track_conditions", "get_stint_analysis", "get_recent_events", "get_corner_analysis",
        };

        public static JArray BuildToolDefinitions()
        {
            var tools = new JArray();
            tools.Add(Tool("get_race_state", "Session, Strecke, Auto, Position, Runden/Restzeit, Flagge."));
            tools.Add(Tool("get_tyre_status", "Reifentemperaturen (innen/mitte/außen), Drücke, Verschleiß, Reifentyp, Bremsentemperaturen, blockierende/durchdrehende Räder."));
            tools.Add(Tool("get_fuel_status", "Tankinhalt, Verbrauch pro Runde (gemessen), Reichweite, Crew-Chief-Rechnung für benötigten Sprit bis Rennende."));
            tools.Add(Tool("get_lap_times", "Eigene letzte Rundenzeiten und Sektorzeiten, persönliche Bestzeit, Session-Bestzeit."));
            tools.Add(Tool("get_opponents_nearby", "Gegner direkt vor und hinter uns: Abstand, letzte/beste Runde, Pace-Trend, Boxenstopps, Reifen. Dazu der Führende."));
            tools.Add(Tool("get_pit_info", "Boxenfenster, Pflichtstopp, eigene Stopps, Boxengasse, Gegner die gerade stoppen."));
            tools.Add(Tool("get_damage_and_engine", "Schäden (Aero, Motor, Getriebe, Aufhängung, Bremsen), Motortemperaturen und Warnungen."));
            tools.Add(Tool("get_track_conditions", "Luft- und Streckentemperatur, Regen, Wind."));
            tools.Add(Tool("get_stint_analysis", "Runde-für-Runde-Verlauf dieses Stints: Rundenzeit, Spritverbrauch, Reifenverschleiß, Reifentemperaturspitzen, Abstände. Für Trends und Prognosen."));
            tools.Add(Tool("get_corner_analysis", "Kurve für Kurve: letzte gültige Runde gegen die eigene Bestrunde – Zeitverlust, Bremspunkt, Scheitel- und Ausgangstempo, Vollgaspunkt. Für Fahrercoaching (\"wo verliere ich Zeit\", \"wie fahre ich Kurve X\")."));
            tools.Add(Tool("get_recent_events", "Was in den letzten Minuten passiert ist (Positionswechsel, Gegner an der Box, Flaggen, Kontakte) und was das Team zuletzt gefunkt hat."));
            return tools;
        }

        private static JObject Tool(string name, string description)
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = name,
                    ["description"] = description,
                    ["parameters"] = new JObject { ["type"] = "object", ["properties"] = new JObject(), ["additionalProperties"] = false },
                },
            };
        }

        public static JObject Execute(string toolName, RaceDataContext ctx)
        {
            var missing = new List<string>();
            JObject data;
            if (ctx.GameState == null)
            {
                return new JObject
                {
                    ["verfuegbar"] = false,
                    ["grund"] = "Keine laufende Simulation – Crew Chief empfängt gerade keine Telemetrie.",
                };
            }
            try
            {
                switch (toolName)
                {
                    case "get_race_state": data = RaceState(ctx, missing); break;
                    case "get_tyre_status": data = Tyres(ctx, missing); break;
                    case "get_fuel_status": data = Fuel(ctx, missing); break;
                    case "get_lap_times": data = LapTimes(ctx, missing); break;
                    case "get_opponents_nearby": data = Opponents(ctx, missing); break;
                    case "get_pit_info": data = Pit(ctx, missing); break;
                    case "get_damage_and_engine": data = Damage(ctx, missing); break;
                    case "get_track_conditions": data = Conditions(ctx, missing); break;
                    case "get_stint_analysis": data = Stint(ctx, missing); break;
                    case "get_recent_events": data = Events(ctx, missing); break;
                    case "get_corner_analysis": data = ctx.Corners != null ? ctx.Corners.Analyse() : new JObject { ["verfuegbar"] = false, ["grund"] = "Kurvenanalyse nicht aktiv." }; break;
                    default:
                        return new JObject { ["fehler"] = "Unbekanntes Werkzeug: " + toolName };
                }
            }
            catch (Exception e)
            {
                return new JObject { ["fehler"] = "Daten konnten nicht gelesen werden: " + e.Message };
            }
            var result = new JObject
            {
                ["quelle"] = "Crew-Chief-Telemetrie" + (string.IsNullOrEmpty(ctx.GameName) ? "" : " (" + ctx.GameName + ")"),
                ["datenalter_s"] = ctx.TelemetryAgeSeconds > 9999 ? (JToken)null : Math.Round(ctx.TelemetryAgeSeconds, 1),
                ["veraltet"] = ctx.TelemetryAgeSeconds > StaleAfterSeconds,
            };
            foreach (var property in data.Properties())
            {
                result[property.Name] = property.Value;
            }
            if (missing.Count > 0)
            {
                result["nicht_verfuegbar"] = new JArray(missing.Distinct());
            }
            return result;
        }

        // ---------- einzelne Werkzeuge ----------

        private static JObject RaceState(RaceDataContext ctx, List<string> missing)
        {
            var gs = ctx.GameState;
            var s = gs.SessionData;
            var o = new JObject
            {
                ["session"] = s.SessionType.ToString(),
                ["phase"] = s.SessionPhase.ToString(),
                ["strecke"] = Text(s.TrackDefinition != null ? s.TrackDefinition.name : gs.trackName, "strecke", missing),
                ["auto"] = Text(gs.carName, "auto", missing),
                ["fahrzeugklasse"] = Text(gs.carClass != null ? gs.carClass.getClassIdentifier() : null, "fahrzeugklasse", missing),
                ["position_klasse"] = Pos(s.ClassPosition, "position_klasse", missing),
                ["position_gesamt"] = Pos(s.OverallPosition, "position_gesamt", missing),
                ["autos_in_klasse"] = Pos(s.NumCarsInPlayerClass, "autos_in_klasse", missing),
                ["autos_gesamt"] = Pos(s.NumCarsOverall, "autos_gesamt", missing),
                ["runden_absolviert"] = s.CompletedLaps,
                ["flagge"] = s.Flag.ToString(),
                ["in_boxengasse"] = gs.PitData.InPitlane,
                ["letzte_runde_des_rennens"] = s.IsLastLap,
            };
            if (s.SessionHasFixedTime)
            {
                o["restzeit_s"] = Num(s.SessionTimeRemaining, 0, 0, 200000, "restzeit_s", missing);
            }
            else if (s.SessionNumberOfLaps > 0)
            {
                o["runden_gesamt"] = s.SessionNumberOfLaps;
                o["runden_verbleibend"] = s.SessionLapsRemaining >= 0 && s.SessionLapsRemaining < 10000 ? (JToken)s.SessionLapsRemaining : null;
            }
            else
            {
                missing.Add("renndistanz");
            }
            return o;
        }

        private static JObject Tyres(RaceDataContext ctx, List<string> missing)
        {
            var t = ctx.GameState.TyreData;
            var o = new JObject();
            o["reifentyp"] = string.IsNullOrEmpty(t.TyreTypeName) ? t.FrontLeftTyreType.ToString() : t.TyreTypeName;
            o["temperatur_c"] = Corners(missing, "reifentemperatur", 1, 250,
                Triple(t.FrontLeft_LeftTemp, t.FrontLeft_CenterTemp, t.FrontLeft_RightTemp),
                Triple(t.FrontRight_LeftTemp, t.FrontRight_CenterTemp, t.FrontRight_RightTemp),
                Triple(t.RearLeft_LeftTemp, t.RearLeft_CenterTemp, t.RearLeft_RightTemp),
                Triple(t.RearRight_LeftTemp, t.RearRight_CenterTemp, t.RearRight_RightTemp));
            if (o["temperatur_c"].Type != JTokenType.Null)
            {
                o["temperatur_hinweis"] = "Je Reifen [links, mitte, rechts] aus Fahrersicht, °C";
            }
            float[] pressures = { t.FrontLeftPressure, t.FrontRightPressure, t.RearLeftPressure, t.RearRightPressure };
            if (pressures.All(p => p > 50 && p < 500))
            {
                o["druck_kpa"] = Round4(pressures, 1);
                o["druck_psi"] = Round4(pressures.Select(p => p / 6.894f).ToArray(), 2);
            }
            else
            {
                missing.Add("reifendruck");
            }
            if (t.TyreWearActive)
            {
                o["verschleiss_prozent"] = Round4(new[] { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear }, 1);
                o["verschleiss_hinweis"] = "0 = neu, 100 = komplett abgefahren; Reihenfolge VL, VR, HL, HR";
                var perLap = ctx.Memory != null ? ctx.Memory.AverageWearPerLap() : null;
                if (perLap != null)
                {
                    o["verschleiss_pro_runde_prozent"] = Round4(perLap, 2);
                }
            }
            else
            {
                missing.Add("reifenverschleiss");
            }
            float[] brakes = { t.LeftFrontBrakeTemp, t.RightFrontBrakeTemp, t.LeftRearBrakeTemp, t.RightRearBrakeTemp };
            if (brakes.Any(b => b > 1 && b < 1500))
            {
                o["bremsentemperatur_c"] = Round4(brakes, 0);
            }
            else
            {
                missing.Add("bremsentemperatur");
            }
            o["blockiert_jetzt"] = new JArray(t.LeftFrontIsLocked, t.RightFrontIsLocked, t.LeftRearIsLocked, t.RightRearIsLocked);
            o["dreht_durch_jetzt"] = new JArray(t.LeftFrontIsSpinning, t.RightFrontIsSpinning, t.LeftRearIsSpinning, t.RightRearIsSpinning);
            o["reihenfolge"] = "VL, VR, HL, HR";
            missing.Add("setupwerte (Federn, Dämpfer, Stabis, Aero, Differential) – liefert keine unterstützte Simulation live");
            return o;
        }

        private static JObject Fuel(RaceDataContext ctx, List<string> missing)
        {
            var gs = ctx.GameState;
            var f = gs.FuelData;
            var o = new JObject();
            if (!f.FuelUseActive || f.FuelLeft <= 0)
            {
                missing.Add("tankinhalt");
                return o;
            }
            o["tankinhalt_l"] = Math.Round(f.FuelLeft, 1);
            if (f.FuelCapacity > 0)
            {
                o["tankgroesse_l"] = Math.Round(f.FuelCapacity, 1);
            }
            float perLap = ctx.Memory != null ? ctx.Memory.AverageFuelPerLap() : -1;
            if (perLap > 0)
            {
                o["verbrauch_pro_runde_l"] = Math.Round(perLap, 2);
                o["reichweite_runden"] = Math.Round(f.FuelLeft / perLap, 1);
                o["verbrauch_basis"] = "Mittel der letzten gemessenen Runden ohne Boxenstopp";
            }
            else
            {
                missing.Add("verbrauch_pro_runde (noch keine volle gemessene Runde)");
            }
            if (ctx.CrewChiefAdditionalFuelNeeded != float.MaxValue && ctx.CrewChiefAdditionalFuelNeeded > -1000)
            {
                o["zusaetzlich_benoetigt_bis_rennende_l"] = Math.Round(ctx.CrewChiefAdditionalFuelNeeded, 1);
                o["zusaetzlich_hinweis"] = "Crew-Chief-Rechnung ohne Reserve; negativ = Sprit reicht mit Überschuss";
            }
            else
            {
                missing.Add("sprit_bis_rennende");
            }
            return o;
        }

        private static JObject LapTimes(RaceDataContext ctx, List<string> missing)
        {
            var s = ctx.GameState.SessionData;
            var o = new JObject();
            var laps = s.PlayerLapData.Where(l => l.LapTime > 0).Skip(Math.Max(0, s.PlayerLapData.Count(l => l.LapTime > 0) - 8)).ToList();
            if (laps.Count == 0)
            {
                missing.Add("rundenzeiten (noch keine gezeitete Runde)");
            }
            else
            {
                var arr = new JArray();
                foreach (var lap in laps)
                {
                    var lapObj = new JObject { ["runde"] = lap.LapNumber, ["zeit_s"] = Math.Round(lap.LapTime, 3), ["gueltig"] = lap.IsValid };
                    if (lap.SectorTimes != null && lap.SectorTimes.Length > 0 && lap.SectorTimes.All(st => st > 0))
                    {
                        lapObj["sektoren_s"] = new JArray(lap.SectorTimes.Select(st => Math.Round(st, 3)));
                    }
                    arr.Add(lapObj);
                }
                o["letzte_runden"] = arr;
            }
            o["persoenliche_bestzeit_s"] = Num(s.PlayerLapTimeSessionBest, 3, 1, 3600, "persoenliche_bestzeit", missing);
            o["klassen_bestzeit_session_s"] = Num(s.PlayerClassSessionBestLapTime, 3, 1, 3600, "klassen_bestzeit", missing);
            if (s.PlayerBestSector1Time > 0 && s.PlayerBestSector2Time > 0 && s.PlayerBestSector3Time > 0)
            {
                o["beste_sektoren_s"] = new JArray(Math.Round(s.PlayerBestSector1Time, 3), Math.Round(s.PlayerBestSector2Time, 3), Math.Round(s.PlayerBestSector3Time, 3));
            }
            else
            {
                missing.Add("beste_sektorzeiten");
            }
            o["aktuelle_runde_gueltig"] = s.CurrentLapIsValid;
            missing.Add("bremspunkte/kurvenweise Telemetrie – nicht im Crew-Chief-Kern verfügbar");
            return o;
        }

        private static JObject Opponents(RaceDataContext ctx, List<string> missing)
        {
            var gs = ctx.GameState;
            var s = gs.SessionData;
            var o = new JObject();
            o["eigene_letzte_runde_s"] = Num(s.LapTimePrevious, 3, 1, 3600, "eigene_letzte_runde", missing);
            OpponentData ahead = Lookup(gs, s.OpponentKeyInFront);
            OpponentData behind = Lookup(gs, s.OpponentKeyBehind);
            if (ahead != null)
            {
                var a = Describe(ahead, ctx, s);
                a["abstand_s"] = Num(s.TimeDeltaFront, 2, 0.01f, 600, "abstand_vorne", missing);
                if (s.LapsDeltaFront > 0)
                {
                    a["runden_rueckstand_auf_ihn"] = s.LapsDeltaFront;
                }
                o["vor_uns"] = a;
            }
            else
            {
                missing.Add("auto_vor_uns");
            }
            if (behind != null)
            {
                var b = Describe(behind, ctx, s);
                b["abstand_s"] = Num(s.TimeDeltaBehind, 2, 0.01f, 600, "abstand_hinten", missing);
                if (s.LapsDeltaBehind > 0)
                {
                    b["runden_vorsprung"] = s.LapsDeltaBehind;
                }
                o["hinter_uns"] = b;
            }
            else
            {
                missing.Add("auto_hinter_uns");
            }
            var leader = gs.OpponentData.Values.FirstOrDefault(op => op.ClassPosition == 1 && op.CarClass != null && gs.carClass != null &&
                op.CarClass.getClassIdentifier() == gs.carClass.getClassIdentifier());
            if (leader != null && s.ClassPosition != 1)
            {
                o["klassenfuehrender"] = Describe(leader, ctx, s);
            }
            return o;
        }

        private static JObject Describe(OpponentData op, RaceDataContext ctx, SessionData s)
        {
            var d = new JObject
            {
                ["position_klasse"] = op.ClassPosition,
                ["letzte_runde_s"] = op.LastLapTime > 0 ? (JToken)Math.Round(op.LastLapTime, 3) : null,
                ["beste_runde_s"] = op.CurrentBestLapTime > 0 ? (JToken)Math.Round(op.CurrentBestLapTime, 3) : null,
                ["in_der_box"] = op.InPits,
                ["boxenstopps"] = op.NumPitStops,
                ["reifen"] = op.CurrentTyres.ToString(),
            };
            if (ctx.SendDriverNames && !string.IsNullOrEmpty(op.DriverRawName))
            {
                d["name"] = op.DriverRawName;
            }
            if (op.CarClass != null)
            {
                d["klasse"] = op.CarClass.getClassIdentifier();
            }
            // Pace-Trend: Durchschnitt der letzten 3 gültigen Runden im Vergleich zu unseren
            var theirs = op.OpponentLapData.Where(l => l.LapTime > 0 && l.IsValid).Reverse().Take(3).ToList();
            var ours = s.PlayerLapData.Where(l => l.LapTime > 0 && l.IsValid).Reverse().Take(3).ToList();
            if (theirs.Count >= 2 && ours.Count >= 2)
            {
                double delta = ours.Average(l => l.LapTime) - theirs.Average(l => l.LapTime);
                d["pace_unterschied_s_pro_runde"] = Math.Round(delta, 2);
                d["pace_hinweis"] = "positiv = er ist schneller als wir, negativ = wir sind schneller (Mittel letzte Runden)";
                if (op.SectorTimesComparable(s))
                {
                    d["sektoren_bester_lauf_s"] = new JArray(Math.Round(op.bestSector1Time, 3), Math.Round(op.bestSector2Time, 3), Math.Round(op.bestSector3Time, 3));
                }
            }
            return d;
        }

        private static JObject Pit(RaceDataContext ctx, List<string> missing)
        {
            var p = ctx.GameState.PitData;
            var o = new JObject
            {
                ["boxenfenster"] = p.PitWindow.ToString(),
                ["pflichtstopp"] = p.HasMandatoryPitStop,
                ["pflichtstopp_erledigt"] = p.MandatoryPitStopCompleted,
                ["eigene_stopps"] = p.NumPitStops,
                ["in_boxengasse"] = p.InPitlane,
                ["auto_vor_uns_an_der_box"] = p.CarInFrontIsPitting,
                ["auto_hinter_uns_an_der_box"] = p.CarBehindIsPitting,
                ["fuehrender_an_der_box"] = p.LeaderIsPitting,
                ["tanken_erlaubt"] = p.IsRefuellingAllowed,
            };
            if (p.PitWindowStart > 0 || p.PitWindowEnd > 0)
            {
                o["boxenfenster_start"] = p.PitWindowStart;
                o["boxenfenster_ende"] = p.PitWindowEnd;
                o["boxenfenster_hinweis"] = "Runden oder Minuten, je nach Rennformat";
            }
            if (p.PitSpeedLimit > 0)
            {
                o["boxenlimit_ms"] = Math.Round(p.PitSpeedLimit, 1);
            }
            if (p.MandatoryPitMinDurationTotal > 0)
            {
                o["mindeststandzeit_s"] = Math.Round(p.MandatoryPitMinDurationTotal, 1);
            }
            missing.Add("zeitverlust_boxenstopp (muss aus der Strecke geschätzt werden – nicht gemessen)");
            return o;
        }

        private static JObject Damage(RaceDataContext ctx, List<string> missing)
        {
            var d = ctx.GameState.CarDamageData;
            var e = ctx.GameState.EngineData;
            var o = new JObject();
            if (d.DamageEnabled)
            {
                o["schaden"] = new JObject
                {
                    ["aero"] = d.OverallAeroDamage.ToString(),
                    ["motor"] = d.OverallEngineDamage.ToString(),
                    ["getriebe"] = d.OverallTransmissionDamage.ToString(),
                };
                o["skala"] = "UNKNOWN, NONE, TRIVIAL, MINOR, MAJOR, DESTROYED";
            }
            else
            {
                missing.Add("schadensmodell");
            }
            if (e.EngineWaterTemp > 1)
            {
                o["wassertemperatur_c"] = Math.Round(e.EngineWaterTemp, 0);
            }
            else
            {
                missing.Add("wassertemperatur");
            }
            if (e.EngineOilTemp > 1)
            {
                o["oeltemperatur_c"] = Math.Round(e.EngineOilTemp, 0);
            }
            else
            {
                missing.Add("oeltemperatur");
            }
            o["warnungen"] = new JObject
            {
                ["wassertemperatur"] = e.EngineWaterTempWarning,
                ["oeldruck"] = e.EngineOilPressureWarning,
                ["benzindruck"] = e.EngineFuelPressureWarning,
                ["motor_abgewuergt"] = e.EngineStalledWarning,
            };
            return o;
        }

        private static JObject Conditions(RaceDataContext ctx, List<string> missing)
        {
            var c = ctx.GameState.Conditions.CurrentConditions;
            var o = new JObject();
            if (c == null)
            {
                missing.Add("wetterdaten");
                return o;
            }
            o["luft_c"] = Num(c.AmbientTemperature, 1, -40, 70, "lufttemperatur", missing);
            o["strecke_c"] = Num(c.TrackTemperature, 1, -40, 90, "streckentemperatur", missing);
            o["regen_0_bis_1"] = c.RainDensity >= 0 ? (JToken)Math.Round(c.RainDensity, 2) : null;
            if (c.RainDensity < 0)
            {
                missing.Add("regen");
            }
            if (c.WindSpeed > 0)
            {
                o["wind_ms"] = Math.Round(c.WindSpeed, 1);
            }
            return o;
        }

        private static JObject Stint(RaceDataContext ctx, List<string> missing)
        {
            var o = new JObject();
            var laps = ctx.Memory != null ? ctx.Memory.GetLaps(12) : new List<LapRecord>();
            if (laps.Count == 0)
            {
                missing.Add("stintverlauf (noch keine Runde aufgezeichnet)");
                return o;
            }
            var arr = new JArray();
            foreach (var l in laps)
            {
                var lo = new JObject
                {
                    ["runde"] = l.LapNumber,
                    ["zeit_s"] = l.LapTime > 0 ? (JToken)Math.Round(l.LapTime, 3) : null,
                    ["gueltig"] = l.Valid,
                    ["pos"] = l.Position,
                    ["box"] = l.InPitLap,
                };
                if (l.FuelUsed > 0) lo["sprit_l"] = Math.Round(l.FuelUsed, 2);
                if (l.TyreWear != null) lo["verschleiss_prozent"] = Round4(l.TyreWear, 1);
                if (l.PeakTyreTemps != null) lo["reifen_spitze_c"] = Round4(l.PeakTyreTemps, 0);
                if (l.GapAhead > 0) lo["abstand_vorne_s"] = Math.Round(l.GapAhead, 2);
                if (l.GapBehind > 0) lo["abstand_hinten_s"] = Math.Round(l.GapBehind, 2);
                arr.Add(lo);
            }
            o["runden"] = arr;
            o["reihenfolge_reifen"] = "VL, VR, HL, HR";
            return o;
        }

        private static JObject Events(RaceDataContext ctx, List<string> missing)
        {
            var o = new JObject();
            if (ctx.Memory == null)
            {
                missing.Add("ereignisse");
                return o;
            }
            var events = ctx.Memory.GetEvents(TimeSpan.FromMinutes(5), ctx.NowUtc);
            o["ereignisse"] = new JArray(events.Select(e => "vor " + (int)(ctx.NowUtc - e.TimeUtc).TotalSeconds + " s: " + e.Text));
            var radio = ctx.Memory.GetRadio(TimeSpan.FromMinutes(5), ctx.NowUtc);
            o["letzte_funksprueche"] = new JArray(radio.Select(e => "vor " + (int)(ctx.NowUtc - e.TimeUtc).TotalSeconds + " s: " + e.Text));
            return o;
        }

        /// <summary>
        /// Lagebild für den System-Prompt: Rennstand plus Reifen, Sprit, direkte Gegner und Box. Damit beantwortet das
        /// Modell die meisten Fragen in einem einzigen Aufruf, statt erst Werkzeuge anzufragen (spart gut eine Sekunde).
        /// </summary>
        public static JObject BuildBriefing(RaceDataContext ctx)
        {
            var o = Execute("get_race_state", ctx);
            if (ctx.GameState == null)
            {
                return o;
            }
            o["reifen"] = WithoutMeta(Execute("get_tyre_status", ctx));
            o["sprit"] = WithoutMeta(Execute("get_fuel_status", ctx));
            o["gegner"] = WithoutMeta(Execute("get_opponents_nearby", ctx));
            o["box"] = WithoutMeta(Execute("get_pit_info", ctx));
            var s = ctx.GameState.SessionData;
            if (s.TimeDeltaFront > 0) o["abstand_vorne_s"] = Math.Round(s.TimeDeltaFront, 2);
            if (s.TimeDeltaBehind > 0) o["abstand_hinten_s"] = Math.Round(s.TimeDeltaBehind, 2);
            if (s.LapTimePrevious > 0) o["letzte_runde_s"] = Math.Round(s.LapTimePrevious, 3);
            if (ctx.GameState.FuelData.FuelUseActive && ctx.GameState.FuelData.FuelLeft > 0)
            {
                o["tankinhalt_l"] = Math.Round(ctx.GameState.FuelData.FuelLeft, 1);
            }
            return o;
        }

        // ---------- Hilfsfunktionen ----------

        // Quelle/Alter stehen schon einmal oben im Lagebild
        private static JObject WithoutMeta(JObject result)
        {
            result.Remove("quelle");
            result.Remove("datenalter_s");
            result.Remove("veraltet");
            return result;
        }

        private static OpponentData Lookup(GameStateData gs, string key)
        {
            OpponentData op;
            return key != null && gs.OpponentData.TryGetValue(key, out op) ? op : null;
        }

        private static bool SectorTimesComparable(this OpponentData op, SessionData s)
        {
            return op.bestSector1Time > 0 && op.bestSector2Time > 0 && op.bestSector3Time > 0;
        }

        private static JToken Text(string value, string name, List<string> missing)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                missing.Add(name);
                return null;
            }
            return value;
        }

        private static JToken Pos(int value, string name, List<string> missing)
        {
            if (value <= 0)
            {
                missing.Add(name);
                return null;
            }
            return value;
        }

        internal static JToken Num(float value, int decimals, float min, float max, string name, List<string> missing)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max)
            {
                missing.Add(name);
                return null;
            }
            return Math.Round(value, decimals);
        }

        private static float[] Triple(float a, float b, float c)
        {
            return new[] { a, b, c };
        }

        private static JToken Corners(List<string> missing, string name, float min, float max, params float[][] corners)
        {
            if (corners.Any(c => c.Any(v => float.IsNaN(v) || v < min || v > max)))
            {
                missing.Add(name);
                return null;
            }
            return new JObject
            {
                ["VL"] = new JArray(corners[0].Select(v => Math.Round(v, 0))),
                ["VR"] = new JArray(corners[1].Select(v => Math.Round(v, 0))),
                ["HL"] = new JArray(corners[2].Select(v => Math.Round(v, 0))),
                ["HR"] = new JArray(corners[3].Select(v => Math.Round(v, 0))),
            };
        }

        private static JArray Round4(float[] values, int decimals)
        {
            return new JArray(values.Select(v => Math.Round(v, decimals)));
        }
    }
}
