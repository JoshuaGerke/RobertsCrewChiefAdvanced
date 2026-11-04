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
        public string DriverName = "Rob";
        public bool SendDriverNames = true;
        /// <summary>Ergebnis von Fuel.GetAdditionalFuelToEndOfRace (Crew-Chief-Rechnung), float.MaxValue = unbekannt.</summary>
        public float CrewChiefAdditionalFuelNeeded = float.MaxValue;
        public CornerCoach Corners;
        public TrackAwareness Tracks;

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
            "get_data_capabilities", "get_telemetry_values", "get_race_state", "get_tyre_status", "get_fuel_status", "get_lap_times", "get_opponents_nearby",
            "get_track_situation", "get_race_field", "get_opponent", "get_pit_info", "get_damage_and_engine", "get_track_conditions", "get_stint_analysis", "get_recent_events", "get_corner_analysis",
        };

        public static JArray BuildToolDefinitions()
        {
            var tools = new JArray();
            tools.Add(Tool("get_data_capabilities", "Nachweis der im aktuellen Adapter angekommenen Daten: Gegner/Zeiten/Sektoren, räumliche Beobachtung, Fahrzeug, Wetter und bestätigte Spiel-/Servervorhersage. Vor Behauptungen über fehlende Daten verwenden."));
            tools.Add(Tool("get_telemetry_values", "Alle aktuell als Messwerte erfassten Details mit exaktem Rohwert, Wert, Einheit, Quelle, Feld, Komponente, UTC, Messalter und Status. Fehlend/veraltet/ungültig nie als Null interpretieren. Schaden ist API-Zustand, kein bewiesener Prozentverlust an Leistung."));
            tools.Add(Tool("get_race_state", "Session, Strecke, Auto, Position, Runden/Restzeit, Flagge und eigene Streckenposition/Start-Ziel-Distanz."));
            tools.Add(Tool("get_track_situation", "Räumliche Verkehrslage: nächste Fahrzeuge auf der Strecke, Weltkoordinaten, Abstand in Metern entlang der Runde, Überrundungsbezug und über mehrere Samples gemessene Annäherung. Keine Seitenfreigabe oder Überholgarantie."));
            tools.Add(Tool("get_tyre_status", "Reifentemperaturen (innen/mitte/außen), Drücke, Verschleiß, Reifentyp, Bremsentemperaturen, blockierende/durchdrehende Räder."));
            tools.Add(Tool("get_fuel_status", "Tankinhalt, Verbrauch pro Runde (gemessen), Reichweite, Crew-Chief-Rechnung für benötigten Sprit bis Rennende."));
            tools.Add(Tool("get_lap_times", "Eigene letzte Rundenzeiten und Sektorzeiten, persönliche Bestzeit, Session-Bestzeit."));
            tools.Add(Tool("get_opponents_nearby", "Gegner direkt vor und hinter uns: Abstand, letzte/beste Runde, Pace-Trend, Boxenstopps, Reifen. Dazu der Führende."));
            tools.Add(Tool("get_pit_info", "Boxenfenster, Pflichtstopp, eigene Stopps, Boxengasse, Gegner die gerade stoppen."));
            tools.Add(Tool("get_damage_and_engine", "Schäden (Aero, Motor, Getriebe, Aufhängung, Bremsen), Motortemperaturen und Warnungen."));
            tools.Add(Tool("get_track_conditions", "Aktuelle Luft-/Streckentemperatur, Regen, Wind, gemeldete Nässe und bestätigte Spiel-/Servervorhersagen mit Quelle, Alter und Wahrscheinlichkeit. Fehlende Prognose bleibt unbekannt."));
            tools.Add(Tool("get_stint_analysis", "Runde-für-Runde-Verlauf dieses Stints: Rundenzeit, Spritverbrauch, Reifenverschleiß, Reifentemperaturspitzen, Abstände. Für Trends und Prognosen."));
            tools.Add(Tool("get_corner_analysis", "Kurve für Kurve: letzte gültige Runde gegen die eigene Bestrunde – Zeitverlust, Bremspunkt, Scheitel- und Ausgangstempo, Vollgaspunkt. Für Fahrercoaching (\"wo verliere ich Zeit\", \"wie fahre ich Kurve X\")."));
            tools.Add(Tool("get_recent_events", "Was in den letzten Minuten passiert ist (Positionswechsel, Gegner an der Box, Flaggen, Kontakte) und was das Team zuletzt gefunkt hat."));
            tools.Add(new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = "save_note",
                    ["description"] = "Dauerhafte Notiz für alle künftigen Rennen speichern, wenn der Fahrer sagt, dass ihr euch etwas merken sollt " +
                        "(Vorlieben, Korrekturen, Wünsche). Kurz und eigenständig verständlich formulieren.",
                    ["parameters"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject { ["notiz"] = new JObject { ["type"] = "string" } },
                        ["required"] = new JArray("notiz"),
                        ["additionalProperties"] = false,
                    },
                },
            });
            tools.Add(Tool("get_race_field", "Alle erfassten Gegner, seitenweise mit Name, Gesamt-/Klassenplatz, Startnummer, Rundenzeiten und Boxenstatus.", RaceFieldTools.Parameters(false)));
            tools.Add(Tool("get_opponent", "Gezielt einen Gegner aus dem gesamten Feld abfragen. Genau ein Suchmerkmal: Name, Startnummer oder Gesamtplatz. Mehrdeutigkeit niemals raten.", RaceFieldTools.Parameters(true)));
            return tools;
        }

        private static JObject Tool(string name, string description, JObject parameters = null)
        {
            return new JObject
            {
                ["type"] = "function",
                ["function"] = new JObject
                {
                    ["name"] = name,
                    ["description"] = description,
                    ["parameters"] = parameters ?? new JObject { ["type"] = "object", ["properties"] = new JObject(), ["additionalProperties"] = false },
                },
            };
        }

        public static JObject Execute(string toolName, RaceDataContext ctx, string arguments)
        {
            if (toolName == "get_race_field" || toolName == "get_opponent") return RaceFieldTools.Read(toolName, ctx, arguments);
            try
            {
                if ((arguments ?? "").Length > 2048 || JObject.Parse(string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments).Count != 0)
                    return new JObject { ["fehler"] = "Dieses Werkzeug akzeptiert keine Argumente." };
            }
            catch (Exception) { return new JObject { ["fehler"] = "Ungültige Werkzeugargumente." }; }
            return Execute(toolName, ctx);
        }

        public static JObject Execute(string toolName, RaceDataContext ctx)
        {
            if (toolName == "get_race_field" || toolName == "get_opponent") return RaceFieldTools.Read(toolName, ctx, "{}");
            var missing = new List<string>();
            JObject data;
            if (ctx == null || ctx.GameState == null)
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
                    case "get_data_capabilities": data = TelemetryCapabilities.Describe(ctx); break;
                    case "get_race_state": data = RaceState(ctx, missing); break;
                    case "get_telemetry_values": data = new JObject { ["messwerte"]=TelemetryMeasurementTools.Read(ctx),
                        ["vollstaendig_fuer_alle_simulatorfelder"]=false }; break;
                    case "get_track_situation": data = TrackAwareness.Read(ctx); break;
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
                ["snapshot_utc"] = ctx.LastTelemetryUtc == DateTime.MinValue ? null : ctx.LastTelemetryUtc.ToString("o"),
                ["zeitbasis"] = "Uebernahme des GameState; einzelne Adapterfelder koennen seltener aktualisiert werden.",
                ["datenalter_s"] = ctx.TelemetryAgeSeconds > 9999 ? (JToken)null : Math.Round(ctx.TelemetryAgeSeconds, 3),
                ["veraltet"] = ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > StaleAfterSeconds,
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
                ["runden_bedeutung"] = "Absolviert ist Vergangenheit; Restdistanz separat unter runden_verbleibend. Null ist unbekannt, nicht null Runden.",
                ["flagge"] = s.Flag.ToString(),
                ["in_boxengasse"] = gs.PitData.InPitlane,
                ["outlap"] = gs.PitData.OnOutLap,
                ["streckenposition"] = TrackAwareness.Player(gs),
                ["letzte_runde_des_rennens"] = LapWatch.OnLastLap(s),
                ["restrunden_geschaetzt"] = LapWatch.LapsAreEstimated(s),
                ["letzte_runde_geschaetzt"] = LapWatch.LapsAreEstimated(s) && LapWatch.LapsLeft(s) == 1,
            };
            int lapsLeft = LapWatch.LapsLeft(s);
            if (s.SessionHasFixedTime)
            {
                // Negative values can also be the adapter's unknown sentinel, not proof that time expired.
                o["restzeit_s"] = Num(s.SessionTimeRemaining, 0, 0, 200000, "restzeit_s", missing);
                o["uhr_abgelaufen"] = s.SessionTimeRemaining == 0 && Finite(s.SessionTotalRunTime) &&
                    s.SessionTotalRunTime > 0 && Finite(s.SessionRunningTime) && s.SessionRunningTime >= s.SessionTotalRunTime;
                if (lapsLeft > 0)
                {
                    o["runden_verbleibend_inkl_aktueller_geschaetzt"] = lapsLeft;
                }
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
                o["verschleiss_prozent"] = CheckedArray(new[] { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear }, 1, 0, 100, "reifenverschleiss", missing);
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
                o["bremsentemperatur_c"] = CheckedArray(brakes, 0, 1, 1500, "bremsentemperatur", missing);
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
            if (!f.FuelUseActive || !Finite(f.FuelLeft) || f.FuelLeft <= 0 || f.FuelLeft > 10000)
            {
                missing.Add("tankinhalt");
                return o;
            }
            o["tankinhalt_l"] = Math.Round(f.FuelLeft, 1);
            if (Finite(f.FuelCapacity) && f.FuelCapacity > 0 && f.FuelCapacity <= 10000)
            {
                o["tankgroesse_l"] = Math.Round(f.FuelCapacity, 1);
            }
            float perLap = ctx.Memory != null ? ctx.Memory.AverageFuelPerLap() : -1;
            if (Finite(perLap) && perLap > 0)
            {
                o["verbrauch_pro_runde_l"] = Math.Round(perLap, 2);
                o["reichweite_runden"] = Math.Round(f.FuelLeft / perLap, 1);
                o["verbrauch_basis"] = "Mittel der letzten gemessenen Runden ohne Boxenstopp";
            }
            else
            {
                missing.Add("verbrauch_pro_runde (noch keine volle gemessene Runde)");
            }
            var estimate=FuelEstimate.From(ctx);
            if (estimate != null)
            {
                o["zusaetzlich_benoetigt_bis_rennende_l"] = Math.Round(estimate.AdditionalWithoutReserve, 2);
                o["reserve_ziel_l"] = Math.Round(estimate.ReserveLitres, 2);
                o["fehlmenge_mit_reserve_l"] = Math.Round(estimate.AdditionalWithoutReserve+estimate.ReserveLitres, 2);
                o["zusaetzlich_hinweis"] = estimate.Source + ". Negativer Bedarf ohne Reserve bedeutet: rechnerisch ausreichend. Fehlende Reserve ist KEIN bestätigter Spritmangel.";
                o["zeitrennen_rest_distanz_geschaetzt"] = estimate.EstimatedFromTime;
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
            OpponentData ahead = Neighbour(gs, true);
            OpponentData behind = Neighbour(gs, false);
            if (ahead != null)
            {
                var a = Describe(ahead, ctx, s);
                AddGap(a, ctx, ahead, true, missing);
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
                AddGap(b, ctx, behind, false, missing);
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
            var leaders = gs.OpponentData.Values.Where(op => op.ClassPosition == 1 && EligibleNeighbour(gs, op)).Take(2).ToArray();
            var leader = leaders.Length == 1 ? leaders[0] : null;
            if (leader != null && s.ClassPosition != 1)
            {
                o["klassenfuehrender"] = Describe(leader, ctx, s);
            }
            return o;
        }

        private static void AddGap(JObject target, RaceDataContext ctx, OpponentData opponent, bool ahead, List<string> missing)
        {
            var gap = ReadGap(ctx, opponent, ahead);
            target["abstand_messung"] = gap.Describe(ctx);
            // Only an adapter timing value occupies the precise racing-gap field.
            target["abstand_s"] = gap.Kind == RaceGapKind.SimulatorTiming ? (JToken)Math.Round(gap.Seconds, 3) : null;
            if (gap.IsEstimated) target["abstand_ungefaehr_s"] = Math.Round(gap.Seconds, 1);
            if (gap.Kind == RaceGapKind.BestLapDifference) target["bestzeitdifferenz_s"] = Math.Round(gap.Seconds, 3);
            if (!gap.HasValue) missing.Add(ahead ? "abstand_vorne" : "abstand_hinten");
        }

        internal static JObject Describe(OpponentData op, RaceDataContext ctx, SessionData s)
        {
            var d = new JObject
            {
                ["position_klasse"] = op.ClassPosition,
                ["letzte_runde_s"] = op.LastLapTime > 0 && op.LastLapTime < 3600 ? (JToken)Math.Round(op.LastLapTime, 3) : null,
                ["beste_runde_s"] = op.CurrentBestLapTime > 0 && op.CurrentBestLapTime < 3600 ? (JToken)Math.Round(op.CurrentBestLapTime, 3) : null,
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
            var theirs = op.OpponentLapData.Where(l => l.LapTime > 0 && l.IsValid && !l.InLap && !l.OutLap).Reverse().Take(3).ToList();
            var ours = s.PlayerLapData.Where(l => l.LapTime > 0 && l.IsValid && !l.InLap && !l.OutLap).Reverse().Take(3).ToList();
            if (theirs.Count >= 2 && ours.Count >= 2)
            {
                double delta = ours.Average(l => l.LapTime) - theirs.Average(l => l.LapTime);
                d["pace_unterschied_s_pro_runde"] = Math.Round(delta, 2);
                d["pace_hinweis"] = "positiv = er ist schneller als wir, negativ = wir sind schneller (Mittel letzte Runden)";
            }
            // Sector availability is independent of a three-lap pace comparison or our own lap count.
            d["beste_einzelsektoren_s"] = new JArray(SectorValue(op.bestSector1Time), SectorValue(op.bestSector2Time), SectorValue(op.bestSector3Time));
            d["sektoren_hinweis"] = "Einzelsektorzeiten in Sekunden, nicht kumulativ. Einzelbestwerte können aus verschiedenen Runden stammen; unbekannt = null. Historie ist auf drei zuletzt erfasste Runden begrenzt.";
            d["sektorrunden"] = new JArray(op.OpponentLapData.OrderByDescending(l => l.LapNumber).Take(3).Select(DescribeSectorLap));
            return d;
        }

        private static JObject DescribeSectorLap(LapData lap)
        {
            var sectors = Enumerable.Range(0, 3).Select(i => lap.SectorTimes != null && i < lap.SectorTimes.Length
                ? SectorValue(lap.SectorTimes[i]) : null).ToArray();
            bool complete = sectors.All(s => s != null && s.Type != JTokenType.Null);
            double? difference = complete && ValidLap(lap.LapTime) ? (double?)(sectors.Sum(s => (double)s) - lap.LapTime) : null;
            // Plausibility only: this tolerance does not claim millisecond accuracy or turn estimates into measurements.
            bool? consistent = difference.HasValue ? (bool?)(Math.Abs(difference.Value) <= .15) : null;
            bool comparable = lap.IsValid && !lap.InLap && !lap.OutLap && consistent == true;
            return new JObject {
                ["runde"] = lap.LapNumber, ["gueltig"] = lap.IsValid, ["boxenrunde"] = lap.InLap || lap.OutLap,
                ["rundenzeit_s"] = SectorValue(lap.LapTime), ["sektoren_s"] = new JArray(sectors),
                ["sektoren_vollstaendig"] = complete,
                ["sektorsumme_abweichung_s"] = difference.HasValue ? (JToken)Math.Round(difference.Value, 3) : null,
                ["sektorsumme_plausibel"] = consistent.HasValue ? (JToken)consistent.Value : null,
                ["fuer_pacevergleich_geeignet"] = comparable,
                ["qualitaet"] = !complete ? "unvollstaendig" : !consistent.HasValue ? "rundenzeit_fehlt" :
                    !consistent.Value ? "sektorsumme_widerspricht_rundenzeit" : !lap.IsValid ? "ungueltige_runde" :
                    lap.InLap || lap.OutLap ? "boxenrunde" : "plausibel_keine_genauigkeitsgarantie",
                ["hinweis"] = "Adapterhistorie, keine neuen Messwerte. Summentoleranz 0,15 s prueft Plausibilitaet. Nur geeignete Runden vergleichen.",
                ["sektorende_simulationszeit_s"] = new JArray(Enumerable.Range(0,3).Select(i => lap.GameTimeAtSectorEnd != null && i < lap.GameTimeAtSectorEnd.Length ? SectorValue(lap.GameTimeAtSectorEnd[i], 200000) : null))
            };
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
            o["boxenlimit_verfuegbar"]=PitLaneWatch.Known(p.PitSpeedLimit);
            o["boxenanfahrt_bestaetigt"]=PitLaneWatch.Approaching(ctx.GameState);
            o["boxenstopp_angemeldet"]=p.HasRequestedPitStop;
            if (PitLaneWatch.Known(p.PitSpeedLimit))
            {
                o["boxenlimit_ms"] = Math.Round(p.PitSpeedLimit, 1);
                o["boxenlimit_kmh"] = Math.Round(p.PitSpeedLimit*3.6f);
                o["boxenlimit_quelle"] = "aktueller Simulatoradapter, keine Annahme einer bestimmten Markierung";
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
                    ["aufhaengung"] = CornerDamage(d.SuspensionDamageStatus),
                    ["bremsen"] = CornerDamage(d.BrakeDamageStatus),
                };
                o["skala"] = "UNKNOWN, NONE, TRIVIAL, MINOR, MAJOR, DESTROYED";
                o["hinweis"] = "Aero ist ein Gesamtwert. Bremsen-/Aufhängungszahlen sind gemeldete API-Zustände und kein bewiesener Prozentverlust an Bremskraft oder Funktion. Fehlend ist kein gesunder Nullwert.";
            }
            else
            {
                missing.Add("schadensmodell");
            }
            var brakePercent=new JArray();var suspensionPercent=new JArray();
            for(int i=0;i<4;i++)
            {
                brakePercent.Add(TelemetryMeasurementTools.Number(ctx,"damage.brake."+TelemetryMeasurements.Corners[i],d.BrakeDamagePercent[i]));
                suspensionPercent.Add(TelemetryMeasurementTools.Number(ctx,"damage.suspension."+TelemetryMeasurements.Corners[i],d.SuspensionDamagePercent[i]));
            }
            o["schaden_prozent"]=new JObject {
                ["aero"]=TelemetryMeasurementTools.Number(ctx,"damage.aero",d.AeroDamagePercent),
                ["motor"]=TelemetryMeasurementTools.Number(ctx,"damage.engine",d.EngineDamagePercent),
                ["getriebe"]=TelemetryMeasurementTools.Number(ctx,"damage.transmission",d.TransmissionDamagePercent),
                ["bremsen"]=brakePercent,["aufhaengung"]=suspensionPercent,["reihenfolge"]="VL, VR, HL, HR"
            };
            o["messwerte"]=TelemetryMeasurementTools.Read(ctx);
            if(!d.DamageEnabled && !ctx.GameState.Measurements.Keys.Any(k=>k.StartsWith("damage.",StringComparison.Ordinal)))missing.Add("schaden_prozent");
            if (e.EngineWaterTemp > 1)
            {
                o["wassertemperatur_c"] = Num(e.EngineWaterTemp, 0, 1, 250, "wassertemperatur", missing);
            }
            else
            {
                missing.Add("wassertemperatur");
            }
            if (e.EngineOilTemp > 1)
            {
                o["oeltemperatur_c"] = Num(e.EngineOilTemp, 0, 1, 300, "oeltemperatur", missing);
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

        private static JObject CornerDamage(CrewChiefV4.Events.CornerData corners)
        {
            var o = new JObject();
            foreach (var level in new[] { DamageLevel.MINOR, DamageLevel.MAJOR, DamageLevel.DESTROYED })
            {
                string where = IncidentWatch.CornerText(corners, level);
                if (where != null)
                {
                    o[level.ToString()] = where;
                }
            }
            return o;
        }

        private static JObject Conditions(RaceDataContext ctx, List<string> missing)
        {
            var c = ctx.GameState.Conditions.LatestConditions;
            var o = new JObject { ["vorhersage"]=WeatherWatch.ForecastData(ctx.GameState.Conditions,ctx.NowUtc) };
            if (c == null)
            {
                missing.Add("wetterdaten");
                return o;
            }
            o["messzeit_utc"] = DateTime.SpecifyKind(c.Time,DateTimeKind.Utc).ToString("o");
            o["messung_veraltet"] = !WeatherWatch.Fresh(ctx.NowUtc,c.Time);
            o["streckennaesse"] = c.TrackWetness==CrewChiefV4.Events.ConditionsMonitor.TrackWetness.UNKNOWN ? null : c.TrackWetness.ToString();
            o["streckenstatus"] = c.TrackStatus==CrewChiefV4.Events.ConditionsMonitor.TrackStatus.UNKNOWN ? null : c.TrackStatus.ToString();
            o["luft_c"] = Num(c.AmbientTemperature, 1, -40, 70, "lufttemperatur", missing);
            o["strecke_c"] = Num(c.TrackTemperature, 1, -40, 90, "streckentemperatur", missing);
            o["regen_0_bis_1"] = Num(c.RainDensity, 2, 0, 1, "regen", missing);
            if (c.RainDensity < 0)
            {
                missing.Add("regen");
            }
            if (c.WindSpeed > 0)
            {
                o["wind_ms"] = Num(c.WindSpeed, 1, 0, 150, "wind", missing);
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
                if (l.GapAhead > 0) lo[l.GapAheadKind == RaceGapKind.SimulatorTiming ? "abstand_vorne_s" : "abstand_vorne_ungefaehr_s"] = Math.Round(l.GapAhead, 2);
                if (l.GapBehind > 0) lo[l.GapBehindKind == RaceGapKind.SimulatorTiming ? "abstand_hinten_s" : "abstand_hinten_ungefaehr_s"] = Math.Round(l.GapBehind, 2);
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
            o["strukturierte_ereignisse"]=new JArray(events.Where(e=>e.Topic!=null).Select(e=>new JObject {
                ["typ"]=e.Topic,["gegner_id"]=e.SubjectKey,["quelle"]=e.Source,["erkannt_utc"]=e.TimeUtc.ToString("o"),["text"]=e.Text }));
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
            if (ctx == null || ctx.GameState == null)
            {
                return o;
            }
            o["reifen"] = WithoutMeta(Execute("get_tyre_status", ctx));
            o["sprit"] = WithoutMeta(Execute("get_fuel_status", ctx));
            o["gegner"] = WithoutMeta(Execute("get_opponents_nearby", ctx));
            o["box"] = WithoutMeta(Execute("get_pit_info", ctx));
            o["streckenlage"] = TrackAwareness.Read(ctx);
            var s = ctx.GameState.SessionData;
            var gapAhead = ReadGap(ctx, Neighbour(ctx.GameState, true), true);
            var gapBehind = ReadGap(ctx, Neighbour(ctx.GameState, false), false);
            if (gapAhead.Kind == RaceGapKind.SimulatorTiming) o["abstand_vorne_s"] = Math.Round(gapAhead.Seconds, 3);
            else if (gapAhead.IsEstimated) o["abstand_vorne_ungefaehr_s"] = Math.Round(gapAhead.Seconds, 1);
            o["abstand_vorne_messung"] = gapAhead.Describe(ctx);
            if (gapBehind.Kind == RaceGapKind.SimulatorTiming) o["abstand_hinten_s"] = Math.Round(gapBehind.Seconds, 3);
            else if (gapBehind.IsEstimated) o["abstand_hinten_ungefaehr_s"] = Math.Round(gapBehind.Seconds, 1);
            o["abstand_hinten_messung"] = gapBehind.Describe(ctx);
            if (s.SessionStartClassPosition > 0 && s.ClassPosition > 0)
            {
                o["startplatz"] = s.SessionStartClassPosition;
                o["plaetze_gewonnen_seit_start"] = s.SessionStartClassPosition - s.ClassPosition;
            }
            if (ValidLap(s.LapTimePrevious)) o["letzte_runde_s"] = Math.Round(s.LapTimePrevious, 3);
            if (ctx.GameState.FuelData.FuelUseActive && Finite(ctx.GameState.FuelData.FuelLeft) && ctx.GameState.FuelData.FuelLeft > 0 && ctx.GameState.FuelData.FuelLeft <= 10000)
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

        /// <summary>Auto direkt vor bzw. hinter uns: Crew Chiefs Zuordnung, sonst über die Klassenposition.</summary>
        public static OpponentData Neighbour(GameStateData gs, bool ahead)
        {
            if (gs == null)
            {
                return null;
            }
            var s = gs.SessionData;
            if (s.ClassPosition <= 0) return null;
            int pos = s.ClassPosition + (ahead ? -1 : 1);
            if (pos < 1) return null;
            var op = Lookup(gs, ahead ? s.OpponentKeyInFront : s.OpponentKeyBehind);
            if (EligibleNeighbour(gs, op) && op.ClassPosition == pos) return op;
            var matches = gs.OpponentData.Values.Where(o => EligibleNeighbour(gs, o) && o.ClassPosition == pos).Take(2).ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private static bool EligibleNeighbour(GameStateData gs, OpponentData op)
        {
            bool confirmedSingleClass = CrewChief.forceSingleClass ||
                (GameStateData.NumberOfClasses == 1 && gs.SessionData.NumCarsOverall > 0 &&
                 gs.SessionData.NumCarsOverall == gs.SessionData.NumCarsInPlayerClass);
            return op != null && op.IsActive && (confirmedSingleClass ||
                (op.CarClass != null && gs.carClass != null && CarData.IsCarClassEqual(gs.carClass, op.CarClass, false)));
        }

        /// <summary>Legacy numeric view. New callers must inspect ReadGap.Kind before interpreting seconds.</summary>
        public static float GapSeconds(GameStateData gs, OpponentData op, bool ahead)
        {
            return ReadGap(gs, op, ahead).Seconds;
        }

        public static RaceGapMeasurement ReadGap(RaceDataContext context, OpponentData op, bool ahead)
        {
            if(context == null) return new RaceGapMeasurement();
            var value=ReadGap(context.GameState,op,ahead);
            if(context.Tracks != null && context.GameState != null && context.GameState.SessionData.SessionType == SessionType.Race &&
                (ahead ? context.GameState.SessionData.TimeDeltaFrontIsDerived : context.GameState.SessionData.TimeDeltaBehindIsDerived))
                return context.Tracks.Gaps.Read(context.GameState,op,ahead,context.NowUtc);
            return value;
        }

        public static RaceGapMeasurement ReadGap(GameStateData gs, OpponentData op, bool ahead)
        {
            if (gs == null || op == null || !ReferenceEquals(op, Neighbour(gs, ahead))) return new RaceGapMeasurement();
            var s = gs.SessionData;
            if (s.SessionType != SessionType.Race)
            {
                if (!SessionLapObserver.Supported(s.SessionType) || !ValidLap(s.PlayerLapTimeSessionBest) || !ValidLap(op.CurrentBestLapTime))
                    return new RaceGapMeasurement();
                return new RaceGapMeasurement(Math.Abs(s.PlayerLapTimeSessionBest - op.CurrentBestLapTime), RaceGapKind.BestLapDifference);
            }
            // A lapped car's distance must not become a same-lap seconds gap.
            if ((ahead ? s.LapsDeltaFront : s.LapsDeltaBehind) != 0) return new RaceGapMeasurement();
            float mapped = ahead ? s.TimeDeltaFront : s.TimeDeltaBehind;
            if (Finite(mapped) && mapped >= 0 && mapped <= 3600)
                return new RaceGapMeasurement(mapped, (ahead ? s.TimeDeltaFrontIsDerived : s.TimeDeltaBehindIsDerived) ? RaceGapKind.DerivedTiming : RaceGapKind.SimulatorTiming);
            float len = s.TrackDefinition != null ? s.TrackDefinition.trackLength : 0;
            if (!Finite(len) || len <= 0 || !Finite(gs.PositionAndMotionData.DistanceRoundTrack) || !Finite(op.DistanceRoundTrack) ||
                gs.PositionAndMotionData.DistanceRoundTrack < 0 || op.DistanceRoundTrack < 0 ||
                gs.PositionAndMotionData.DistanceRoundTrack > len || op.DistanceRoundTrack > len ||
                s.CompletedLaps < 0 || op.CompletedLaps < 0) return new RaceGapMeasurement();
            float own = s.CompletedLaps * len + gs.PositionAndMotionData.DistanceRoundTrack;
            float other = op.CompletedLaps * len + op.DistanceRoundTrack;
            float distance = ahead ? other - own : own - other;
            if (!Finite(distance) || distance <= 0 || distance > len) return new RaceGapMeasurement();
            float lap = ValidLap(s.LapTimePrevious) ? s.LapTimePrevious : s.PlayerLapTimeSessionBest;
            float speed = ValidLap(lap) ? len / lap : gs.PositionAndMotionData.CarSpeed;
            return Finite(speed) && speed > 5 ? new RaceGapMeasurement(distance / speed, RaceGapKind.TrackDistanceEstimate) : new RaceGapMeasurement();
        }

        private static bool Finite(float number) { return !float.IsNaN(number) && !float.IsInfinity(number); }
        private static bool ValidLap(float seconds) { return Finite(seconds) && seconds >= 1 && seconds < 3600; }

        private static OpponentData Lookup(GameStateData gs, string key)
        {
            OpponentData op;
            return key != null && gs.OpponentData.TryGetValue(key, out op) ? op : null;
        }

        private static JToken SectorValue(float value, float maximum = 3600)
        {
            return Finite(value) && value > 0 && value < maximum ? (JToken)Math.Round(value, 3) : null;
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

        private static JArray CheckedArray(float[] values, int decimals, float min, float max, string name, List<string> missing)
        {
            return new JArray(values.Select((v,i) => Num(v, decimals, min, max, name + "_" + new[] { "VL", "VR", "HL", "HR" }[i], missing)));
        }

        private static JArray Round4(float[] values, int decimals)
        {
            return new JArray(values.Select(v => Math.Round(v, decimals)));
        }
    }

    /// <summary>Notizen des Fahrers, die über alle Rennen erhalten bleiben ("merk dir das").</summary>
    public static class DriverNotes
    {
        public static string Path { get { return DriverMemory.NotesPath; } }

        public static void Add(string note)
        { if (!DriverMemory.TryAddNote(note)) throw new System.IO.IOException("Fahrergedächtnis ist nicht verfügbar oder ausgeschaltet."); }

        public static string PromptBlock(string question = null, RaceDataContext context = null)
        {
            var state=context==null?null:context.GameState;
            string track=state==null?null:state.SessionData.TrackDefinition==null?state.trackName:state.SessionData.TrackDefinition.name;
            return DriverMemory.PromptBlock(question,context==null?null:context.GameName,track);
        }
    }
}
