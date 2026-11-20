using System;
using System.Collections.Generic;
using System.Globalization;
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
        public RaceReader Reader;
        internal PitCycleWatch.MergeForecast PitForecast;
        internal PitStopTimer.Measurement LastPitStop;
        /// <summary>Gemessener eigener Boxenverlust dieser Session (Median) und Anzahl Messungen; -1 ohne Messung.</summary>
        internal float PitLossSeconds = -1;
        /// <summary>RRCA 2.0 (4.2): Ereignisgedächtnis der Crew (was schon gemeldet wurde).</summary>
        internal RaceEventLog EventLog;
        internal int PitLossSamples;

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
            "get_race_reader", "get_driver_history", "get_data_capabilities", "get_telemetry_values", "get_race_state", "get_tyre_status", "get_fuel_status", "get_lap_times", "get_opponents_nearby",
            "get_track_situation", "get_race_field", "get_opponent", "get_pit_info", "get_damage_and_engine", "get_track_conditions", "get_stint_analysis", "get_recent_events", "get_corner_analysis", "get_race_pace_review",
            "get_strategy_odds", "get_lmu_native", "get_race_chronicle",
        };

        public static JArray BuildToolDefinitions()
        {
            var tools = new JArray();
            tools.Add(Tool("get_driver_history", "Belegte historische eigene Runden aus dauerhaftem lokalem Faktengedächtnis. Nur passender Spiel-/Fahrzeug-/Strecken-/Reifen-/Bedingungskontext, Quelle und UTC. Kein aktueller Fahrzeugzustand, keine vollständige persönliche Rekordliste, unbekannte Verbrauchseinstellung; niemals aktuelle Reichweite daraus ableiten."));
            tools.Add(Tool("get_data_capabilities", "Nachweis der im aktuellen Adapter angekommenen Daten: Gegner/Zeiten/Sektoren, räumliche Beobachtung, Fahrzeug, Wetter und bestätigte Spiel-/Servervorhersage. Vor Behauptungen über fehlende Daten verwenden."));
            tools.Add(Tool("get_telemetry_values", "Alle aktuell als Messwerte erfassten Details mit exaktem Rohwert, Wert, Einheit, Quelle, Feld, Komponente, UTC, Messalter und Status. Fehlend/veraltet/ungültig nie als Null interpretieren. Schaden ist API-Zustand, kein bewiesener Prozentverlust an Leistung."));
            tools.Add(Tool("get_race_state", "Session, Strecke, Auto, Position, Runden/Restzeit, Flagge, eigene Streckenposition/Start-Ziel-Distanz und im Rennen die Lageeinschätzung des Rennkopfs."));
            tools.Add(Tool("get_track_situation", "Räumliche Verkehrslage: nächste Fahrzeuge auf der Strecke, Weltkoordinaten, Abstand in Metern entlang der Runde, Überrundungsbezug und über mehrere Samples gemessene Annäherung. Keine Seitenfreigabe oder Überholgarantie."));
            tools.Add(Tool("get_tyre_status", "Reifentemperaturen (innen/mitte/außen), Drücke, Verschleiß, Reifentyp, Bremsentemperaturen, blockierende/durchdrehende Räder."));
            tools.Add(Tool("get_fuel_status", "Tankinhalt, Verbrauch pro Runde (gemessen), Reichweite, Crew-Chief-Rechnung für benötigten Sprit bis Rennende."));
            tools.Add(Tool("get_lap_times", "Eigene abgeschlossene Sektoren der laufenden Runde, letzte Rundenzeiten, persönliche Bestzeit und Session-Bestzeit."));
            tools.Add(Tool("get_race_pace_review", "Nach dem Rennen: Klassen-Top-Drei mit Name, sauberer Vergleichs-Rundenbasis, Durchschnitt, Streuung und belegten Sektordifferenzen."));
            tools.Add(Tool("get_opponents_nearby", "Gegner direkt vor und hinter uns: Abstand, letzte/beste Runde, Pace-Trend, Boxenstopps, Reifen. Dazu der Führende."));
            tools.Add(Tool("get_pit_info", "Boxenfenster, Pflichtstopp, eigene Stopps, Boxengasse, Gegner die gerade stoppen."));
            tools.Add(Tool("get_lmu_native", "Nur LMU: Werte direkt aus LMUs nativer Schnittstelle – ob die laufende Runde gestrichen ist, Tracklimit-Punkte und Schwelle zur Strafe, Abstände des Spiels, virtuelle Energie, Fahrhilfen, das ganze Feld mit offenen Strafen, Stopps, Boxenstatus, Runden zurück, sowie die letzten Meldungen der Rennleitung (Strafen, Tracklimits, Vorfälle). Für Fragen zu Strafen, Tracklimits, Rundengültigkeit, Überrundung und Rennlage."));
            tools.Add(Tool("get_race_chronicle", "Rennchronik: alle belegten Ereignisse dieses Rennens in zeitlicher Folge – Startplatz, Platzwechsel mit Gegnernamen (auf der Strecke oder durch Boxenstopp), eigene Runden und Bestzeiten, Fehler, Kontakte, Gelb mit Betroffenem, Dreher, Strafen, Boxenstopps, Ausfälle, Serverabgänge, letzte Runde. Für Rückblicke, Rivalen, Zusammenfassungen und 'was ist passiert'."));
            tools.Add(Tool("get_damage_and_engine", "Schäden (Aero, Motor, Getriebe, Aufhängung, Bremsen), Motortemperaturen und Warnungen."));
            tools.Add(Tool("get_track_conditions", "Aktuelle Luft-/Streckentemperatur, Regen, Wind, gemeldete Nässe und bestätigte Spiel-/Servervorhersagen mit Quelle, Alter und Wahrscheinlichkeit. Fehlende Prognose bleibt unbekannt."));
            tools.Add(Tool("get_race_reader", "Verdichtetes Live-Lagebild mit Fahrerbelastung, geprüften Drei-Runden- und Sektorvergleichen, Spritbilanz, Verschleißregression und Wetterverlauf. Ursachen nicht aus Messreihen erfinden.",
                new JObject { ["type"] = "object", ["properties"] = new JObject {
                    ["after_revision"] = new JObject { ["type"] = "integer", ["minimum"] = 0 }
                }, ["additionalProperties"] = false }));
            tools.Add(Tool("get_stint_analysis", "Runde-für-Runde-Verlauf dieses Stints: Rundenzeit, Spritverbrauch, Reifenverschleiß, Reifentemperaturspitzen, Abstände. Für Trends und Prognosen."));
            tools.Add(Tool("get_corner_analysis", "Kurve für Kurve: letzte gültige Runde gegen die eigene Bestrunde – Zeitverlust, Bremspunkt, Scheitel- und Ausgangstempo, Vollgaspunkt. Für Fahrercoaching (\"wo verliere ich Zeit\", \"wie fahre ich Kurve X\")."));
            tools.Add(Tool("get_strategy_odds", "Rennen: Wahrscheinlichkeiten für Durchfahren, Stopp jetzt, in einer oder in zwei Runden – reicht der Sprit, kommst du vor dem Hintermann raus (nur mit gemessenem Boxenverlust). Monte Carlo nur aus Messwerten, mit Empfehlung."));
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
            if (toolName == "get_race_reader")
            {
                try
                {
                    if ((arguments ?? "").Length > 2048) return new JObject { ["fehler"] = "Zu große Werkzeugargumente." };
                    var args = JObject.Parse(string.IsNullOrWhiteSpace(arguments) ? "{}" : arguments);
                    if (args.Properties().Any(p => p.Name != "after_revision") ||
                        args["after_revision"] != null && (args["after_revision"].Type != JTokenType.Integer ||
                            (long)args["after_revision"] < 0 || (long)args["after_revision"] > int.MaxValue))
                        return new JObject { ["fehler"] = "Ungültige Revision." };
                    return ctx?.Reader?.Read(ctx, (int?)args["after_revision"]) ?? new JObject { ["verfuegbar"] = false };
                }
                catch (Exception e) when (e is Newtonsoft.Json.JsonException || e is OverflowException)
                { return new JObject { ["fehler"] = "Ungültige Werkzeugargumente." }; }
            }
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
                    case "get_race_reader": data = ctx.Reader?.Read(ctx) ?? new JObject { ["verfuegbar"] = false }; break;
                    case "get_driver_history": data = DriverMemory.HistoricalFacts(ctx); break;
                    case "get_data_capabilities": data = TelemetryCapabilities.Describe(ctx); break;
                    case "get_race_state": data = RaceState(ctx, missing); break;
                    case "get_telemetry_values": data = new JObject { ["messwerte"]=TelemetryMeasurementTools.Read(ctx),
                        ["vollstaendig_fuer_alle_simulatorfelder"]=false }; break;
                    case "get_track_situation": data = TrackAwareness.Read(ctx); break;
                    case "get_tyre_status": data = Tyres(ctx, missing); break;
                    case "get_fuel_status": data = Fuel(ctx, missing); break;
                    case "get_lap_times": data = LapTimes(ctx, missing); break;
                    case "get_race_pace_review": data = RacePaceReview(ctx); break;
                    case "get_strategy_odds": data = StrategyOddsTool(ctx, missing); break;
                    case "get_opponents_nearby": data = Opponents(ctx, missing); break;
                    case "get_pit_info": data = Pit(ctx, missing); break;
                    case "get_damage_and_engine": data = Damage(ctx, missing); break;
                    case "get_lmu_native":
                        var native = LMUNativeSnapshot.Fresh(ctx.NowUtc == DateTime.MinValue ? DateTime.UtcNow : ctx.NowUtc);
                        data = native != null ? native.ToJson(ctx.NowUtc == DateTime.MinValue ? DateTime.UtcNow : ctx.NowUtc, LMURaceControlStream.Recent)
                            : new JObject { ["verfuegbar_nativ"] = false, ["grund"] = "Nur in Le Mans Ultimate mit nativem Abbild LMU_Data; gerade kein frisches Bild.",
                                ["rennleitung_zuletzt"] = new JArray(LMURaceControlStream.Recent.Select(t => (JToken)t)) };
                        break;
                    case "get_track_conditions": data = Conditions(ctx, missing); break;
                    case "get_stint_analysis": data = Stint(ctx, missing); break;
                    case "get_recent_events": data = Events(ctx, missing); break;
                    case "get_race_chronicle":
                        data = RaceChronicle.Current.ToJson(ctx.NowUtc == DateTime.MinValue ? DateTime.UtcNow : ctx.NowUtc, 60, ctx.SendDriverNames);
                        break;
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
                ["disqualifikation_bestaetigt"] = s.IsDisqualified,
                ["ausfall_bestaetigt"] = s.IsDNF,
                ["in_boxengasse"] = gs.PitData.InPitlane,
                ["outlap"] = gs.PitData.OnOutLap,
                ["streckenposition"] = TrackAwareness.Player(gs),
                ["letzte_runde_des_rennens"] = LapWatch.OnLastLap(s),
                ["restrunden_geschaetzt"] = LapWatch.LapsAreEstimated(s),
                ["letzte_runde_geschaetzt"] = LapWatch.LapsAreEstimated(s) && LapWatch.LapsLeft(s) == 1,
            };
            if (IncidentPointsWatch.Known(s))
            {
                o["vorfallpunkte"] = s.CurrentIncidentCount;
                o["vorfallpunkte_limit"] = s.MaxIncidentCount;
                o["vorfallpunkte_frei"] = s.MaxIncidentCount - s.CurrentIncidentCount;
                o["vorfallpunkte_hinweis"] = "Vom Spiel gemeldet. Bei Erreichen des Limits droht Disqualifikation.";
            }
            // Einschätzung des Renningenieur-Kopfs: Phase, Lage, Fokus, Gefahren, Chancen, Trends – damit Antworten die Rennlage kennen.
            var mind = RaceMind.Latest;
            if (mind != null && mind.SessionId == s.SessionId && mind.Fresh(ctx.NowUtc)) o["rennkopf"] = mind.Describe();
            var field = RaceInsightWatch.Latest(s.SessionId);
            if (field != null && s.SessionType == SessionType.Race) o["feld_erkenntnisse"] = field;
            var dynamics = DynamicsSampler.Facts(s.CompletedLaps);
            if (dynamics != null) o["fahrdynamik"] = dynamics;
            int startMode, startPhase;
            if (RaceSituationWatch.RaceRoomStart(gs, ctx.NowUtc, out startMode, out startPhase) && (startMode >= 0 || startPhase >= 0))
                o["raceroom_startgruppe"] = new JObject {
                    ["startart"] = startMode == 0 ? "stehend" : startMode == 1 ? "fliegend" : startMode == 2 ? "formation" : null,
                    ["eigene_phase"] = startPhase == 0 ? "wartet" : startPhase == 1 ? "startkorridor" : startPhase == 2 ? "gruen" : null,
                    ["quelle"] = "RaceRoom Shared Memory 3.6; eigene Phase nur bei eindeutigem Fahrerslot",
                    ["hinweis"] = "Startphase der eigenen Gruppe; allgemeines Session-Gruen kann bei gestaffelten Starts frueher kommen."
                };
            int lapsLeft = LapWatch.LapsLeft(s);
            if (s.SessionHasFixedTime)
            {
                // Negative values can also be the adapter's unknown sentinel, not proof that time expired.
                o["restzeit_s"] = Num(s.SessionTimeRemaining, 0, 0, 200000, "restzeit_s", missing);
                // LMU 18.11.: Das Modell rechnete 377 s in „fünf Minuten achtzehn“ um. Fertig formatiert wie die Uhr im Spiel.
                if (Finite(s.SessionTimeRemaining) && s.SessionTimeRemaining >= 0 && s.SessionTimeRemaining <= 200000)
                    o["restzeit_uhr"] = ((int)s.SessionTimeRemaining / 60).ToString(CultureInfo.InvariantCulture) + ":" + ((int)s.SessionTimeRemaining % 60).ToString("00", CultureInfo.InvariantCulture);
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
            // Rob 16.11.: „welche Temperatur meine Reifen am besten haben“ – Crew Chief kennt Grenzwerte je Klasse/Reifentyp.
            if (TyreGripWatch.Window(ctx.GameState, out float windowLow, out float windowHigh))
                o["temperaturfenster_c"] = new JObject { ["von"] = Math.Round(windowLow), ["bis"] = Math.Round(windowHigh),
                    ["quelle"] = TyreGripWatch.IsRaceSlick(t.FrontLeftTyreType)
                        ? "Robs Richtwert für Rennreifen: ab 70 °C in Ordnung, ab 80 °C gut, 85–90 °C sehr gut; kein Herstellerwert"
                        : "Crew-Chief-Richtwert für Klasse und Reifentyp (Spitzentemperatur), kein Herstellerwert" };
            else missing.Add("temperaturfenster_fuer_diesen_reifentyp");
            var hotCorners = TyreGripWatch.HotCorners(TyreGripWatch.Temps(ctx.GameState), windowHigh);
            if (hotCorners.Count > 0)
                o["ueberhitzt"] = new JObject { ["ecken"] = TyreGripWatch.Corners(hotCorners),
                    ["folge"] = "Weniger Grip und Traktion an diesen Ecken, bis sie abkühlen; Grund: über dem Fenster oder mindestens 12 °C heißer als die anderen." };
            if (t.DirtPickupEmulationActive && t.TyreDirtPickupStatus.hasValueAtLevel(TyreDirtPickupState.MAJOR))
                o["schmutz"] = "Das Spiel meldet deutlich Schmutz auf den Reifen: weniger Grip, bis sie sauber sind.";
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
            // Datenwahrheit je Kanal: Das Sprachmodell darf eingefrorene Werte nicht als aktuell ausgeben.
            var truth = DataTruth.Current;
            o["datenstand"] = new JObject
            {
                ["temperatur"] = truth.Describe(SimChannel.TyreTemperature), ["druck"] = truth.Describe(SimChannel.TyrePressure),
                ["verschleiss"] = truth.Describe(SimChannel.TyreWear),
                ["hinweis"] = "„eingefroren“ heißt: Die Simulation aktualisiert diese Werte während der Fahrt nicht (oft erst beim Boxenstopp). Dann nur als Stand vom letzten Stopp nennen, nie als aktuell."
            };
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

        /// <summary>C6: Messwerte für den Monte-Carlo-Kern sammeln. Was nicht gemessen ist, bleibt draußen und steht in „fehlt“.</summary>
        internal static StrategyInputs StrategyInputsFrom(RaceDataContext ctx, List<string> missing)
        {
            var gs = ctx.GameState;
            if (gs.SessionData.SessionType != SessionType.Race) { missing.Add("nur_im_rennen"); return null; }
            var estimate = FuelEstimate.From(ctx);
            if (estimate == null || estimate.PerLap <= 0) { missing.Add("verbrauch_oder_restdistanz (mindestens zwei saubere Runden nötig)"); return null; }
            var laps = ctx.Memory.GetLaps();
            var fuelSamples = laps.Where(l => !l.InPitLap && l.FuelUsed > 0 && l.FuelUsed < 1000).Reverse().Take(5).Select(l => (double)l.FuelUsed).ToList();
            double perLap = estimate.PerLap, left = gs.FuelData.FuelLeft;
            var i = new StrategyInputs
            {
                FuelLaps = left / perLap,
                // Streuung der Reichweite aus der Streuung des Verbrauchs (Fehlerfortpflanzung), mindestens eine Zehntelrunde.
                FuelLapsSpread = Math.Max(0.1, left * StrategyOdds.Spread(fuelSamples) / (perLap * perLap)),
                LapsToGo = estimate.LapsRemaining,
                LapsToGoEstimated = estimate.EstimatedFromTime,
                LapsToGoSpread = estimate.EstimatedFromTime ? 0.5 : 0,
            };
            float length = gs.SessionData.TrackDefinition?.trackLength ?? 0, distance = gs.PositionAndMotionData.DistanceRoundTrack;
            if (Finite(length) && length > 0 && Finite(distance) && distance >= 0 && distance < length)
                i.LapFractionToPitEntry = Math.Max(0.05, 1 - distance / length);
            if (ctx.PitLossSeconds > 0)
            {
                i.PitLoss = ctx.PitLossSeconds;
                i.PitLossSpread = Math.Max(1.0, ctx.PitLossSeconds * (ctx.PitLossSamples >= 2 ? 0.05 : 0.1));
            }
            else missing.Add("boxenverlust (erst nach dem ersten eigenen Stopp gemessen)");
            // Hintermann: derselbe Gegner über mindestens drei Runden ohne Boxenrunde.
            var recent = Enumerable.Reverse(laps).Take(6).ToList();
            string rear = recent.FirstOrDefault()?.BehindIdentity;
            var same = recent.TakeWhile(l => rear != null && l.BehindIdentity == rear && !l.InPitLap && l.GapBehind > 0 && l.GapBehind < 60).Reverse().ToList();
            if (same.Count >= 3)
            {
                var gains = same.Zip(same.Skip(1), (a, b) => (double)(a.GapBehind - b.GapBehind)).ToList();
                i.GapBehind = same.Last().GapBehind;
                i.RearGainPerLap = gains.Average();
                i.RearGainSpread = Math.Max(0.1, StrategyOdds.Spread(gains));
            }
            else missing.Add("hintermann (derselbe Gegner über mindestens drei Runden)");
            return i;
        }

        private static JObject StrategyOddsTool(RaceDataContext ctx, List<string> missing)
        {
            var inputs = StrategyInputsFrom(ctx, missing);
            if (inputs == null) return new JObject { ["berechnet"] = false };
            var o = StrategyOdds.Describe(StrategyOdds.Evaluate(inputs), inputs);
            o["berechnet"] = true;
            return o;
        }

        private static JObject Fuel(RaceDataContext ctx, List<string> missing)
        {
            var gs = ctx.GameState;
            var f = gs.FuelData;
            var o = new JObject();
            var energy = VirtualEnergyWatch.Current;
            if (energy.Level >= 0)
                o["virtuelle_energie"] = new JObject
                {
                    ["stand_prozent"] = Math.Round(energy.Level, 1),
                    ["verbrauch_pro_runde_prozent"] = energy.PerLap > 0 ? (JToken)Math.Round(energy.PerLap, 2) : null,
                    ["reicht_fuer_runden"] = energy.LapsLeft > 0 ? (JToken)Math.Round(energy.LapsLeft, 1) : null,
                    ["hinweis"] = "LMU-Klassen mit Energie-Limit: Die Reichweite begrenzt, was zuerst leer ist – Energie oder Sprit. Verbrauch nur aus gemessenen sauberen Runden."
                };
            if (!f.FuelUseActive || !Finite(f.FuelLeft) || f.FuelLeft < 0 || f.FuelLeft > 10000)
            {
                missing.Add("tankinhalt");
                return o;
            }
            o["tankinhalt_l"] = Math.Round(f.FuelLeft, 1);
            if (Finite(f.FuelCapacity) && f.FuelCapacity > 0 && f.FuelCapacity <= 10000)
            {
                o["tankgroesse_l"] = Math.Round(f.FuelCapacity, 1);
            }
            float perLap = FuelEstimate.Consumption(ctx.Memory, gs, out bool provisional, out _);
            if (Finite(perLap) && perLap > 0)
            {
                o["verbrauch_pro_runde_l"] = Math.Round(perLap, 2);
                o["reichweite_runden"] = Math.Round(f.FuelLeft / perLap, 1);
                o["verbrauch_basis"] = provisional ? "Vorläufige Schätzung aus Tankdifferenz und gefahrener Strecke" :
                    "Mittel der letzten gemessenen Runden ohne Boxenstopp";
                o["verbrauch_vorlaeufig"] = provisional;
            }
            else
            {
                missing.Add("verbrauch_pro_runde (Hochrechnung beginnt etwa 300 m nach der Boxenausfahrt; in der Boxengasse und direkt nach dem Tanken gibt es noch keine)");
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

        internal static JObject NativeSectorBriefing(RaceDataContext ctx)
        {
            var result = new JObject();
            if (ctx.GameState == null || ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > StaleAfterSeconds)
                return result;
            var values = ctx.GameState.Measurements;
            foreach (string scope in new[] { "personal", "leader", "class_leader" })
            {
                var splits = new JArray();
                for (int i = 1; i <= 3; i++)
                {
                    bool current = values.TryGetValue("timing.sector." + scope + "." + i, out var time) &&
                        time.StatusAt(ctx.NowUtc) == "gueltig";
                    splits.Add(current ? new JValue(Math.Round(time.Value.Value, 3)) : JValue.CreateNull());
                }
                if (splits.Any(t => t.Type != JTokenType.Null))
                    result[scope + "_einzelsektoren_s"] = splits;
            }
            if (result.HasValues)
                result["hinweis"] = "Native Einzelbestwerte. Führende sind Referenzen. Feld-Sektorbestzeit und vollständige Rundenzeit sind daraus nicht ableitbar.";
            return result;
        }

        private static JObject LapTimes(RaceDataContext ctx, List<string> missing)
        {
            var s = ctx.GameState.SessionData;
            var o = new JObject();
            var refs = TelemetryMeasurementTools.Read(ctx, "timing.sector.");
            if (refs.HasValues)
            {
                o["native_sektorreferenzen"] = refs;
                o["sektorreferenzen_hinweis"] = "Führender und Klassenführender sind Referenzen. Daraus folgt kein Einzelsektorrekord des gesamten Feldes.";
            }
            if (ctx.GameState.Measurements.TryGetValue("timing.lap.delta_personal", out var delta))
                o["native_live_differenz_zur_eigenen_bestzeit"] = TelemetryMeasurementTools.Describe(delta, ctx.NowUtc);
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
                    if (!s.SectorTimingEstimated && lap.SectorTimes != null && lap.SectorTimes.Length > 0 && lap.SectorTimes.All(st => st > 0))
                    {
                        lapObj["sektoren_s"] = new JArray(lap.SectorTimes.Select(st => Math.Round(st, 3)));
                    }
                    arr.Add(lapObj);
                }
                o["letzte_runden"] = arr;
            }
            o["persoenliche_bestzeit_s"] = Num(s.PlayerLapTimeSessionBest, 3, 1, 3600, "persoenliche_bestzeit", missing);
            o["klassen_bestzeit_session_s"] = Num(s.PlayerClassSessionBestLapTime, 3, 1, 3600, "klassen_bestzeit", missing);
            if (!s.SectorTimingEstimated && s.PlayerBestSector1Time > 0 && s.PlayerBestSector2Time > 0 && s.PlayerBestSector3Time > 0)
            {
                o["beste_sektoren_s"] = new JArray(Math.Round(s.PlayerBestSector1Time, 3), Math.Round(s.PlayerBestSector2Time, 3), Math.Round(s.PlayerBestSector3Time, 3));
            }
            else
            {
                missing.Add("beste_sektorzeiten");
            }
            o["aktuelle_runde_gueltig"] = s.CurrentLapIsValid;
            var liveSectors = CurrentSectors(ctx);
            if (liveSectors != null) o["laufende_runde"] = liveSectors;
            return o;
        }

        internal static JObject RacePaceReview(RaceDataContext ctx)
        {
            var gs = ctx.GameState;
            var s = gs.SessionData;
            var results = new JArray();
            var review = new JObject { ["bezugsgruppe"] = "klasse",
                ["vergleichsbasis"] = "Gleiche Rundennummern, gültige Runden nach der Startrunde, ohne Ein- und Ausrunden. Keine Angleichung von Reifen oder Verkehr.",
                ["fahrer"] = results };
            if (s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Finished ||
                ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > StaleAfterSeconds ||
                gs.carClass == null || s.ClassPosition < 1 || s.IsDNF || s.IsDisqualified)
            {
                review["verfuegbar"] = false;
                return review;
            }

            review["verfuegbar"] = true;
            var own = ComparableRaceLaps(s.PlayerLapData);
            for (int rank = 1; rank <= 3; rank++)
            {
                if (rank == s.ClassPosition || (s.NumCarsInPlayerClass > 0 && rank > s.NumCarsInPlayerClass)) continue;
                var rivals = gs.OpponentData.Values.Where(o => o != null && o.IsActive && o.ClassPosition == rank &&
                    CarData.IsCarClassEqual(gs.carClass, o.CarClass, false)).Take(2).ToArray();
                var comparison = new JObject { ["klassenplatz"] = rank, ["vergleichbare_runden"] = 0 };
                results.Add(comparison);
                if (rivals.Length != 1) continue;
                var rival = rivals[0];
                if (ctx.SendDriverNames && rival.CanUseName)
                {
                    var name = RadioPhraseCatalog.SafeRivalName(rival.DriverRawName);
                    if (name.Length != 0) comparison["name"] = name;
                }
                if (ValidLap(rival.CurrentBestLapTime)) comparison["beste_runde_s"] = Math.Round(rival.CurrentBestLapTime, 3);
                var theirs = ComparableRaceLaps(rival.OpponentLapData);
                var matched = own.Keys.Intersect(theirs.Keys).OrderByDescending(lap => lap).Take(6).ToArray();
                comparison["vergleichbare_runden"] = matched.Length;
                if (matched.Length < 3) continue;
                var ourTimes = matched.Select(lap => (double)own[lap].LapTime).ToArray();
                var theirTimes = matched.Select(lap => (double)theirs[lap].LapTime).ToArray();
                double ours = ourTimes.Average(), other = theirTimes.Average();
                comparison["eigener_schnitt_s"] = Math.Round(ours, 3);
                comparison["gegner_schnitt_s"] = Math.Round(other, 3);
                comparison["rueckstand_s_pro_runde"] = Math.Round(ours - other, 3);
                comparison["eigene_streuung_s"] = Spread(ourTimes, ours);
                comparison["gegner_streuung_s"] = Spread(theirTimes, other);
                comparison["runden"] = new JArray(matched.Reverse());
                if (!s.SectorTimingEstimated && matched.All(lap => CoherentSectors(own[lap]) && CoherentSectors(theirs[lap])))
                    comparison["sektorrueckstand_s"] = new JArray(Enumerable.Range(0, 3).Select(sector =>
                        Math.Round(matched.Average(lap => own[lap].SectorTimes[sector] - theirs[lap].SectorTimes[sector]), 3)));
            }
            return review;
        }

        internal static string FinalLapTactic(RaceDataContext ctx)
        {
            var gs = ctx?.GameState;
            if (gs == null || !LapWatch.OnLastLap(gs.SessionData) || gs.FlagData.isLocalYellow ||
                gs.PitData.InPitlane || ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > 1 ||
                gs.SessionData.ClassPosition <= 1) return null;
            var rival = Neighbour(gs, true);
            var gap = ReadGap(ctx, rival, true);
            if (rival == null || gap.Kind != RaceGapKind.SimulatorTiming || gap.Seconds <= .05f) return null;
            var own = ComparableRaceLaps(gs.SessionData.PlayerLapData);
            var theirs = ComparableRaceLaps(rival.OpponentLapData);
            var matching = own.Keys.Intersect(theirs.Keys).OrderByDescending(lap => lap).Take(3).ToArray();
            if (matching.Length < 3) return null;
            double gain = matching.Average(lap => theirs[lap].LapTime - own[lap].LapTime);
            if (gain >= .2 && gap.Seconds <= Math.Min(1.2, gain + .25))
                return "Du hast die Pace für einen Angriff. Wenn die Lücke aufgeht, nutz sie.";
            return "Sichere deine Position bis zur Linie.";
        }

        private static Dictionary<int, LapData> ComparableRaceLaps(IEnumerable<LapData> laps) => laps
            .Where(lap => lap != null && lap.LapNumber > 1 && lap.IsValid && !lap.InLap && !lap.OutLap && ValidLap(lap.LapTime))
            .GroupBy(lap => lap.LapNumber).Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single());

        private static bool CoherentSectors(LapData lap) => lap.SectorTimes.Length == 3 && lap.SectorTimes.All(ValidLap) &&
            Math.Abs(lap.SectorTimes.Sum() - lap.LapTime) <= .20f;

        private static double Spread(double[] times, double mean) =>
            Math.Round(Math.Sqrt(times.Average(value => (value - mean) * (value - mean))), 3);

        internal static PreparedRadioDetail RacePaceRadio(RaceDataContext ctx)
        {
            if (ctx?.GameState == null) return null;
            var review = RacePaceReview(ctx);
            var reference = ((JArray)review["fahrer"]).OfType<JObject>().FirstOrDefault(row => row["rueckstand_s_pro_runde"] != null);
            if (reference == null) return null;
            var spoken = CultureInfo.GetCultureInfo("de-DE");
            int rank = (int)reference["klassenplatz"];
            string name = (string)reference["name"] ?? "Platz " + rank + " in deiner Klasse";
            var gs = ctx.GameState;
            var rivals = gs.OpponentData.Where(p => p.Value != null && p.Value.IsActive && p.Value.ClassPosition == rank &&
                CarData.IsCarClassEqual(gs.carClass, p.Value.CarClass, false)).Take(2).ToArray();
            if (rivals.Length != 1) return null;
            var rival = rivals[0];
            string identity = RaceReader.Identity(rival.Key, rival.Value);
            double delta = (double)reference["rueckstand_s_pro_runde"];
            int count = (int)reference["vergleichbare_runden"];
            string pace = Math.Abs(delta) < .05 ? "fast genauso schnell wie du" :
                "rund " + GermanRadioText.Interval(Math.Abs(delta)) + " pro Runde " + (delta > 0 ? "schneller" : "langsamer");
            return new PreparedRadioDetail {
                Text = name + " war über " + count + " vergleichbare Runden " + pace + ".",
                StillCurrent = (live, _) => live != null && live.OpponentData.TryGetValue(rival.Key, out var current) &&
                    current != null && current.IsActive && current.ClassPosition == rank && RaceReader.Identity(rival.Key, current) == identity
            };
        }

        internal static JObject CurrentSectors(RaceDataContext ctx)
        {
            var gs = ctx.GameState;
            DateTime source = gs.SourceTelemetryUtc == DateTime.MinValue ? ctx.LastTelemetryUtc : gs.SourceTelemetryUtc;
            if (source == DateTime.MinValue || ctx.NowUtc < source || ctx.NowUtc - source > TimeSpan.FromSeconds(1))
                return null;
            var session = gs.SessionData;
            var lap = session.PlayerLapData.LastOrDefault();
            if (lap == null || lap.LapTime > 0 || lap.LapNumber < 1 || lap.LapNumber != session.CompletedLaps + 1 ||
                session.SectorTimingEstimated || session.SectorNumber < 2 || session.SectorNumber > 3 || gs.PitData.InPitlane)
                return null;

            var times = lap.SectorTimes;
            var sectors = new JArray();
            for (int i = 0; i < session.SectorNumber - 1 && i < times.Length; i++)
            {
                var value = SectorValue(times[i]);
                if (value == null) continue;
                var entry = new JObject { ["sektor"] = i + 1, ["zeit_s"] = value };
                // Rob 17.11. (Bathurst): Auf „Fällt dir nichts an meiner Sektorzeit auf?“ nannte Mik nur Sektor 1, weil die
                // Bestmarke der Simulation den gerade gefahrenen Sektor schon enthielt. Deshalb hier der Vergleich mit der
                // eigenen Bestmarke VOR dieser Runde und mit den Bestwerten der Klasse.
                int sector = i;
                var earlier = session.PlayerLapData.Where(l => l != lap && l.IsValid && !l.InLap && !l.OutLap && l.SectorTimes != null &&
                    l.SectorTimes.Length > sector && l.SectorTimes[sector] > 1 && l.SectorTimes[sector] < 3600).Select(l => l.SectorTimes[sector]).ToList();
                if (earlier.Count > 0)
                {
                    float before = earlier.Min();
                    entry["eigene_bestmarke_vor_dieser_runde_s"] = Math.Round(before, 3);
                    entry["differenz_zur_bestmarke_s"] = Math.Round(times[i] - before, 3);
                    entry["neue_persoenliche_bestzeit"] = times[i] < before - .0005f;
                }
                else entry["neue_persoenliche_bestzeit"] = null;
                var field = gs.OpponentData.Values.Where(o => o != null && gs.carClass != null && o.CarClass != null &&
                        CarData.IsCarClassEqual(gs.carClass, o.CarClass, false))
                    .Select(o => sector == 0 ? o.bestSector1Time : sector == 1 ? o.bestSector2Time : o.bestSector3Time)
                    .Where(t => t > 1 && t < 3600).ToList();
                if (field.Count > 0)
                {
                    entry["beste_klassenzeit_gegner_s"] = Math.Round(field.Min(), 3);
                    entry["schneller_als_alle_gegner_lila"] = times[i] < field.Min() - .0005f;
                }
                sectors.Add(entry);
            }
            return sectors.Count == 0 ? null : new JObject {
                ["runde"] = lap.LapNumber,
                ["bisher_gueltig"] = lap.IsValid && session.CurrentLapIsValid,
                ["boxenrunde"] = lap.OutLap || lap.InLap,
                ["abgeschlossene_sektoren"] = sectors
            };
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
            int stops = OpponentStopLog.Stops(op, out bool stoppedNow);
            if (stops > 0 || stoppedNow)
            {
                d["stillstaende_auf_strecke"] = stops;
                d["steht_gerade_auf_strecke"] = stoppedNow;
            }
            if (ctx.SendDriverNames && !string.IsNullOrEmpty(op.DriverRawName))
            {
                d["name"] = op.DriverRawName;
            }
            if (op.CarClass != null)
            {
                d["klasse"] = op.CarClass.getClassIdentifier();
            }
            if (ctx.Tracks != null && !ctx.Tracks.OpponentFresh(ctx, op))
            {
                d.Remove("letzte_runde_s");
                d.Remove("beste_runde_s");
                d["gegnerdaten_aktuell"] = false;
                return d;
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
            if (!s.SectorTimingEstimated)
            {
                d["beste_einzelsektoren_s"] = new JArray(SectorValue(op.bestSector1Time), SectorValue(op.bestSector2Time), SectorValue(op.bestSector3Time));
                d["sektoren_hinweis"] = "Einzelsektorzeiten in Sekunden, nicht kumulativ. Einzelbestwerte können aus verschiedenen Runden stammen; unbekannt = null. Historie ist auf drei zuletzt erfasste Runden begrenzt.";
                d["sektorrunden"] = new JArray(op.OpponentLapData.OrderByDescending(l => l.LapNumber).Take(3).Select(DescribeSectorLap));
            }
            var ams2 = Ams2ParticipantBestTimes(op, ctx);
            if (ams2 != null) d["ams2_simulator_bestwerte"] = ams2;
            return d;
        }

        private static JObject Ams2ParticipantBestTimes(OpponentData opponent, RaceDataContext ctx)
        {
            var state = ctx?.GameState;
            var wrapper = state?.rawGameData as CrewChiefV4.AMS2.AMS2SharedMemoryReader.AMS2StructWrapper;
            if (wrapper == null || !wrapper.sdk14LayoutSelected || wrapper.data.mVersion != 14 || opponent == null ||
                !opponent.IsActive || string.IsNullOrWhiteSpace(opponent.DriverRawName) || state.SourceTelemetryUtc == DateTime.MinValue ||
                ctx.NowUtc < state.SourceTelemetryUtc || ctx.NowUtc - state.SourceTelemetryUtc > TimeSpan.FromSeconds(1)) return null;
            var raw = wrapper.data;
            if (raw.mParticipantData == null || raw.mNumParticipants < 1 || raw.mNumParticipants > 64 ||
                raw.mNumParticipants > raw.mParticipantData.Length) return null;
            int index = -1;
            for (int i = 0; i < raw.mNumParticipants; i++)
            {
                var p = raw.mParticipantData[i];
                if (!p.mIsActive || !string.Equals(CrewChiefV4.AMS2.StructHelper.getNameFromBytes(p.mName), opponent.DriverRawName, StringComparison.Ordinal)) continue;
                if (index >= 0) return null; // Duplicate names cannot identify a unique SDK slot.
                index = i;
            }
            if (index < 0 || opponent.OverallPosition <= 0 || raw.mParticipantData[index].mRacePosition != (uint)opponent.OverallPosition ||
                string.Equals(state.SessionData.DriverRawName, opponent.DriverRawName, StringComparison.Ordinal)) return null;
            JToken sector1 = Ams2Time(raw.mFastestSector1Times, index), sector2 = Ams2Time(raw.mFastestSector2Times, index),
                sector3 = Ams2Time(raw.mFastestSector3Times, index), best = Ams2Time(raw.mFastestLapTimes, index), last = Ams2Time(raw.mLastLapTimes, index);
            if (sector1.Type == JTokenType.Null && sector2.Type == JTokenType.Null && sector3.Type == JTokenType.Null &&
                best.Type == JTokenType.Null && last.Type == JTokenType.Null) return null;
            return new JObject {
                ["quelle"] = "AMS2 Shared Memory SDK v14 / Teilnehmerindex",
                ["zeitpunkt_utc"] = state.SourceTelemetryUtc.ToString("o"),
                ["beste_einzelsektoren_s"] = new JArray(sector1, sector2, sector3),
                ["beste_runde_s"] = best, ["letzte_runde_s"] = last,
                ["hinweis"] = "Simulatorwerte; Einzelbestsektoren koennen aus verschiedenen Runden stammen. Gueltigkeit und Bedingungen der letzten Runde sind hier nicht belegt. Kein belegter aktueller Pace-Trend."
            };
        }

        private static JToken Ams2Time(float[] values, int index)
        {
            if (values == null || index < 0 || index >= values.Length || float.IsNaN(values[index]) ||
                float.IsInfinity(values[index]) || values[index] <= 0 || values[index] >= 3600) return JValue.CreateNull();
            return new JValue(Math.Round(values[index], 3));
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
            if (ctx.PitForecast != null) o["boxenausfahrt_prognose"] = ctx.PitForecast.Describe();
            else missing.Add("boxenausfahrt_prognose (keine frische Messgrundlage)");
            if (ctx.LastPitStop != null && ctx.GameState != null && ctx.LastPitStop.SessionId == ctx.GameState.SessionData.SessionId)
                o["letzter_boxenbesuch"] = ctx.LastPitStop.Describe();
            return o;
        }

        /// <summary>Karosserie und Räder, wie rF2/LMU sie melden (LMU 20.11.: „Es ist alles ablesbar“). null ohne solche Werte.</summary>
        internal static JObject Body(CarDamageData d)
        {
            if (d == null || d.DentSeverity == null && d.PartsDetached == null && d.WheelDetached == null && d.WheelFlat == null && d.LastImpactMagnitude < 0) return null;
            var o = new JObject();
            if (d.DentSeverity != null && d.DentSeverity.Length == 8)
            {
                o["dellen"] = new JArray(d.DentSeverity.Select(v => (JToken)v));
                o["dellen_stellen_verbeult"] = d.DentSeverity.Count(v => v > 0);
                o["dellen_stellen_stark"] = d.DentSeverity.Count(v => v >= 2);
                o["dellen_skala"] = "8 Stellen rund ums Auto, 0 keine, 1 leicht, 2 stark. Die Lage der einzelnen Stellen ist nicht bestätigt: nur Anzahl und Stärke nennen.";
            }
            if (d.PartsDetached != null) o["teile_abgerissen"] = d.PartsDetached.Value;
            var corners = new[] { "VL", "VR", "HL", "HR" };
            if (d.WheelDetached != null && d.WheelDetached.Length == 4)
                o["rad_ab"] = new JArray(Enumerable.Range(0, 4).Where(i => d.WheelDetached[i]).Select(i => (JToken)corners[i]));
            if (d.WheelFlat != null && d.WheelFlat.Length == 4)
                o["reifen_platt"] = new JArray(Enumerable.Range(0, 4).Where(i => d.WheelFlat[i]).Select(i => (JToken)corners[i]));
            if (d.LastImpactMagnitude >= 0)
            {
                o["letzter_einschlag_staerke"] = Math.Round(d.LastImpactMagnitude);
                o["letzter_einschlag"] = d.LastImpactMagnitude <= 0 ? "keiner" : d.LastImpactMagnitude < 1500 ? "leicht" : d.LastImpactMagnitude < 4000 ? "mittel" : "schwer";
            }
            o["hinweis"] = "Motor und Getriebe meldet das Spiel hier nicht als Schadenswert; beurteile sie über Temperaturen und Warnungen, nenne sie nicht pauschal unbekannt.";
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
            var body = Body(d);
            if (body != null) o["karosserie"] = body;
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

        internal static JArray RecentRaceActions(RaceDataContext ctx)
        {
            if (ctx?.Memory == null || ctx.GameState?.SessionData.SessionType != SessionType.Race ||
                ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > 1) return new JArray();
            return new JArray(ctx.Memory.GetEvents(TimeSpan.FromSeconds(45), ctx.NowUtc)
                .Where(e => e.TimeUtc <= ctx.NowUtc && (e.Topic == "confirmed-on-track-pass" || e.Topic == "confirmed-position-loss"))
                .Reverse().Take(8).Reverse().Select(e => new JObject {
                    ["typ"] = e.Topic,
                    ["sekunden_zurueck"] = (int)(ctx.NowUtc - e.TimeUtc).TotalSeconds,
                    ["befund"] = ctx.SendDriverNames || e.Topic != "confirmed-on-track-pass" ? e.Text :
                        "Ein belegter Platzgewinn auf der Strecke."
                }));
        }

        private static JObject Events(RaceDataContext ctx, List<string> missing)
        {
            var o = new JObject();
            if (ctx.Memory == null)
            {
                missing.Add("ereignisse");
                return o;
            }
            var events = ctx.Memory.GetEvents(TimeSpan.FromMinutes(5), ctx.NowUtc)
                .Where(e => ctx.SendDriverNames || e.Topic != "confirmed-on-track-pass").ToList();
            o["ereignisse"] = new JArray(events.Select(e => "vor " + (int)(ctx.NowUtc - e.TimeUtc).TotalSeconds + " s: " + e.Text));
            o["strukturierte_ereignisse"]=new JArray(events.Where(e=>e.Topic!=null).Select(e=>new JObject {
                ["typ"]=e.Topic,["gegner_id"]=e.SubjectKey,["quelle"]=e.Source,["erkannt_utc"]=e.TimeUtc.ToString("o"),["text"]=e.Text }));
            var radio = ctx.Memory.GetRadio(TimeSpan.FromMinutes(5), ctx.NowUtc);
            o["letzte_funksprueche"] = new JArray(radio.Select(e => "vor " + (int)(ctx.NowUtc - e.TimeUtc).TotalSeconds + " s: " + e.Text));
            // RRCA 2.0 (6.3): bereits gemeldete Vorfälle nicht als neue Information erzählen.
            if (ctx.EventLog != null && ctx.GameState != null)
            {
                o["gemeldete_crew_ereignisse"] = ctx.EventLog.DescribeReported(ctx.GameState.SessionData.SessionId, ctx.NowUtc);
                o["hinweis_gemeldet"] = "Diese Vorfälle hat die Crew schon gesagt. Nur neue Zusammenhänge ergänzen, nicht als Neuigkeit wiederholen.";
            }
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
            var previousStint = DriverMemory.PreviousStint(ctx);
            if (previousStint != null) o["vorherige_sitzung_referenz"] = previousStint;
            o["streckenlage"] = TrackAwareness.Read(ctx);
            if (ctx.Reader != null) o["rennbeobachtung"] = ctx.Reader.Read(ctx);
            var s = ctx.GameState.SessionData;
            var gapAhead = ReadGap(ctx, Neighbour(ctx.GameState, true), true);
            var gapBehind = ReadGap(ctx, Neighbour(ctx.GameState, false), false);
            if (gapAhead.Kind == RaceGapKind.SimulatorTiming) o["abstand_vorne_s"] = Math.Round(gapAhead.Seconds, 3);
            else if (gapAhead.IsEstimated) o["abstand_vorne_ungefaehr_s"] = Math.Round(gapAhead.Seconds, 1);
            o["abstand_vorne_messung"] = gapAhead.Describe(ctx);
            if (gapBehind.Kind == RaceGapKind.SimulatorTiming) o["abstand_hinten_s"] = Math.Round(gapBehind.Seconds, 3);
            else if (gapBehind.IsEstimated) o["abstand_hinten_ungefaehr_s"] = Math.Round(gapBehind.Seconds, 1);
            o["abstand_hinten_messung"] = gapBehind.Describe(ctx);
            int startLatched = RaceStartLatch.Position(s);
            if (startLatched > 0 && s.ClassPosition > 0)
            {
                o["startplatz"] = startLatched;
                o["plaetze_gewonnen_seit_start"] = startLatched - s.ClassPosition;
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
            if(context == null || context.GameState == null) return new RaceGapMeasurement();
            if (context.Tracks != null && !context.Tracks.OpponentFresh(context, op)) return new RaceGapMeasurement();
            DateTime source=context.GameState.SourceTelemetryUtc==DateTime.MinValue ? context.LastTelemetryUtc : context.GameState.SourceTelemetryUtc;
            if(source==DateTime.MinValue || context.NowUtc<source || context.NowUtc-source>TimeSpan.FromSeconds(1))
                return new RaceGapMeasurement();
            var value=ReadGap(context.GameState,op,ahead);
            if(context.Tracks != null && context.GameState != null && context.GameState.SessionData.SessionType == SessionType.Race)
            {
                if (ahead ? context.GameState.SessionData.TimeDeltaFrontIsDerived : context.GameState.SessionData.TimeDeltaBehindIsDerived)
                    return context.Tracks.Gaps.Read(context.GameState,op,ahead,context.NowUtc);
                // P3 (Rob 16.11.): Liefert das Spiel keinen Zeitabstand (RaceRoom oft in Runde 1), zählt die Zeitdifferenz beider Autos
                // am selben Streckenpunkt – gemessen ab den ersten Metern – vor der groben Schätzung aus Streckenmetern und eigenem Tempo.
                if (!value.HasValue || value.Kind == RaceGapKind.TrackDistanceEstimate)
                {
                    var tracked = context.Tracks.Gaps.Read(context.GameState,op,ahead,context.NowUtc);
                    if (tracked.HasValue) return tracked;
                }
            }
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
            // A mapper may retain the former neighbour during its overtake debounce.
            // Never attach that car's interval/lap difference to a classification fallback.
            string mappedKey=ahead?s.OpponentKeyInFront:s.OpponentKeyBehind;
            if(mappedKey!=null && !ReferenceEquals(op,Lookup(gs,mappedKey))) return new RaceGapMeasurement();
            // A lapped car's distance must not become a same-lap seconds gap.
            if ((ahead ? s.LapsDeltaFront : s.LapsDeltaBehind) != 0) return new RaceGapMeasurement();
            float mapped = ahead ? s.TimeDeltaFront : s.TimeDeltaBehind;
            if (Finite(mapped) && mapped >= 0 && mapped <= 3600)
                return new RaceGapMeasurement(mapped, (ahead ? s.TimeDeltaFrontIsDerived : s.TimeDeltaBehindIsDerived) ? RaceGapKind.DerivedTiming : RaceGapKind.SimulatorTiming,gs.SourceTelemetryUtc);
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
            return DriverMemory.PromptBlock(question,context==null?null:context.GameName,track)+DriverMemory.FactPromptBlock(context);
        }
    }
}
