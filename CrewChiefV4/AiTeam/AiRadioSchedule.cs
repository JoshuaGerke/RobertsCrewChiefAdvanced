using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    public sealed class RadioOption
    {
        public string Id, Label, Hint;
        public int DefaultSeconds, Minimum;
        public bool CanDisable = true;
        public string SettingKey { get { return "ai_team_radio_" + Id + "_seconds"; } }
    }

    /// <summary>Seconds, never Crew Chief's probabilistic frequency scale. Zero disables optional topics.</summary>
    public sealed class AiRadioPreferences
    {
        public static readonly RadioOption[] Options = {
            new RadioOption { Id="status", Label="Rennstatus: Position und Abstände", DefaultSeconds=120, Minimum=30, Hint="Aktueller Überblick; nur belegte Fortschritte und Pace." },
            new RadioOption { Id="weather", Label="Wetter, Luft- und Streckentemperatur", DefaultSeconds=300, Minimum=30, Hint="Nur verfügbare Messwerte; keine Wettervorhersage." },
            new RadioOption { Id="fuel", Label="Sprit und gemessene Reichweite", DefaultSeconds=240, Minimum=30, Hint="Reichweite erst mit sauber gemessenem Verbrauch." },
            new RadioOption { Id="tyres", Label="Reifenverschleiß", DefaultSeconds=240, Minimum=30, Hint="Eigene Reifen; keine erfundenen Gegnerwerte." },
            new RadioOption { Id="pace", Label="Eigene Rundenzeiten und Konstanz", DefaultSeconds=180, Minimum=30, Hint="Drei zusammenhängende gültige Runden ohne Box." },
            new RadioOption { Id="ahead", Label="Vordermann: Name und Abstand", DefaultSeconds=120, Minimum=30, Hint="Gegner der eigenen Klasse; Abstand muss bekannt sein." },
            new RadioOption { Id="behind", Label="Hintermann: Name und Abstand", DefaultSeconds=120, Minimum=30, Hint="Kein Ersatz für Saras unmittelbare Seitenwarnungen." },
            new RadioOption { Id="stint", Label="Stintentwicklung: Verbrauch und Reifen", DefaultSeconds=300, Minimum=30, Hint="Gemessene Entwicklung ohne erfundene Strategie." },
            new RadioOption { Id="vehicle", Label="Fahrzeugzustand: Schäden und Technik", DefaultSeconds=300, Minimum=30, Hint="Motor, Bremsen, Aufhängung, Getriebe, Aero und Reifen, soweit verfügbar." },
            new RadioOption { Id="progress", Label="Rennfortschritt und Restdistanz", DefaultSeconds=180, Minimum=30, Hint="Restzeit oder bestätigte Rundenzahl, passend zum Rennformat." },
        };
        private readonly Dictionary<string, int> seconds = new Dictionary<string, int>();
        private readonly Dictionary<string, int> minus = new Dictionary<string, int>();
        private readonly Dictionary<string, int> plus = new Dictionary<string, int>();
        public AiRadioPreferences() { foreach (var o in Options) { seconds[o.Id]=o.DefaultSeconds; minus[o.Id]=7; plus[o.Id]=8; } }
        public int Minus(string id) { return minus[id]; }
        public int Plus(string id) { return plus[id]; }
        public void SetTolerance(string id, int earlier, int later)
        {
            int bound=Math.Min(300,(seconds[id]>0?seconds[id]:120)/2);
            minus[id]=Math.Max(0,Math.Min(bound,earlier)); plus[id]=Math.Max(0,Math.Min(bound,later));
        }
        public int NextDelaySeconds(string id, Random random)
        {
            if(seconds[id]==0)return 0;
            int delay=seconds[id]+random.Next(-minus[id],plus[id]+1);
            return ApplyDriverScale(id,delay,DriverMemory.RoutineIntervalScale(id));
        }
        internal static int ApplyDriverScale(string id,int delay,double scale)
        {
            if(delay==0 || double.IsNaN(scale) || double.IsInfinity(scale) || scale<=0 || scale==1)return delay;
            return (int)Math.Max(Options.Single(o=>o.Id==id).Minimum,Math.Min(3600,Math.Round(delay*scale)));
        }
        public int this[string id] { get { return seconds[id]; } }
        public void Set(string id, int value)
        {
            var o = Options.Single(x => x.Id == id);
            seconds[id] = value == 0 && o.CanDisable ? 0 : Math.Max(o.Minimum, Math.Min(3600, value));
            SetTolerance(id, minus[id], plus[id]);
        }
        public static AiRadioPreferences Load()
        {
            var p = new AiRadioPreferences();
            foreach (var o in Options)
                try {
                    var u=UserSettings.GetUserSettings(); p.Set(o.Id,u.getInt(o.SettingKey));
                    p.SetTolerance(o.Id,u.getInt(o.SettingKey+"_minus"),u.getInt(o.SettingKey+"_plus"));
                } catch (Exception) { }
            return p;
        }
        public void Save()
        {
            var user = UserSettings.GetUserSettings();
            foreach (var o in Options) {
                user.setProperty(o.SettingKey,this[o.Id]);user.setProperty(o.SettingKey+"_minus",Minus(o.Id));user.setProperty(o.SettingKey+"_plus",Plus(o.Id));
            }
            user.saveUserSettings();
        }
    }

    /// <summary>One bounded due-time entry per topic; generate from current data only when a channel slot is available.</summary>
    public sealed class AiRadioSchedule
    {
        private readonly Dictionary<string, DateTime> due = new Dictionary<string, DateTime>();
        public const int RoutineSeparationSeconds = 20, QuietAfterRadioSeconds = 8;
        private DateTime lastDeferral=DateTime.MinValue;
        private DateTime lastReport = DateTime.MinValue;
        private ObservedRaceCall outstanding,lastRoutine;
        private readonly RadioVariantCursor fuelVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor aheadVariants = new RadioVariantCursor();
        private readonly RadioVariantCursor behindVariants = new RadioVariantCursor();
        private int tyreReports; // Rob 16.11.: Verschleißbericht technisch, menschlich und Achsvergleich im Wechsel
        /// <summary>
        /// Rob 16.11.: Verbrauch, Wetter, Streckentemperatur und Reifen kamen zusammen mehrmals pro Minute. Technische
        /// Routinemeldungen teilen sich deshalb ein gemeinsames Budget: nach einer technischen Meldung kommt die nächste
        /// eines anderen Themas frühestens nach 3–4 Minuten. Die eingestellten Abstände je Thema bleiben die Untergrenze;
        /// Warnungen (Sprit knapp, Reifen am Ende, Schaden) laufen über eigene Wächter und sind davon nicht betroffen. 0 schaltet ab.
        /// </summary>
        public int TechnicalSpacingSeconds = 180;
        private DateTime lastTechnicalUtc = DateTime.MinValue;
        private string lastTechnicalTopic;
        private int technicalGapSeconds;
        internal static bool IsTechnical(string topic) =>
            topic == "fuel" || topic == "weather" || topic == "tyres" || topic == "stint" || topic == "vehicle";
        internal bool TechnicalBudgetBlocks(string topic, DateTime now) =>
            IsTechnical(topic) && lastTechnicalTopic != null && topic != lastTechnicalTopic &&
            now - lastTechnicalUtc < TimeSpan.FromSeconds(technicalGapSeconds);
        private bool interruptionHandled;
        private readonly Dictionary<string,string> lastDeliveredText=new Dictionary<string,string>();
        private readonly Dictionary<string,DateTime> lastDeliveredUtc=new Dictionary<string,DateTime>();
        private string lastFuelMeasurement;
        private string outstandingFuelMeasurement;
        private readonly StintRadioMemory facts = new StintRadioMemory();
        private StintRadioMemory.Report outstandingFacts;
        public static bool OnConfirmedQualifyingPushLap(GameStateData gs)
        {
            return gs!=null && gs.SessionData.SessionType==SessionType.Qualify && gs.SessionData.CompletedLaps>0 &&
                !gs.PitData.OnOutLap && !gs.PitData.OnInLap && !gs.PitData.InPitlane &&
                !TrackAwareness.InRange(gs.PositionAndMotionData.CarSpeed,0,4.99).HasValue;
        }
        public static bool BusyForRoutine(GameStateData gs)
        {return gs != null && gs.FlagData.isLocalYellow || OnConfirmedQualifyingPushLap(gs) || gs!=null && gs.SessionData.SessionType==SessionType.HotLap &&
            gs.SessionData.CurrentLapIsValid && !gs.PitData.OnOutLap && !gs.PitData.InPitlane || gs!=null && TrackAwareness.InRange(gs.ControlData.BrakePedal,.2,1).HasValue;}
        private bool UnchangedRoutine(string topic,string text,DateTime now)
        {
            if(topic=="status" || topic=="fuel" || topic=="progress") return false;
            string old;DateTime when;
            return lastDeliveredText.TryGetValue(topic,out old) && old==text && lastDeliveredUtc.TryGetValue(topic,out when) &&
                now>=when && now-when<TimeSpan.FromMinutes(10);
        }
        private int session = int.MinValue;
        private readonly Random random;
        public AiRadioSchedule(int? seed=null) { random=seed.HasValue?new Random(seed.Value):new Random(); }
        public void Reset() { facts.Clear(); outstandingFacts = null; due.Clear(); lastDeliveredText.Clear(); lastDeliveredUtc.Clear(); outstanding = lastRoutine = null; lastFuelMeasurement = outstandingFuelMeasurement = null; interruptionHandled=false; lastReport = lastDeferral = lastTechnicalUtc = DateTime.MinValue; lastTechnicalTopic = null; technicalGapSeconds = 0; session = int.MinValue; }
        public void Observe(GameStateData gs, DateTime now, AiRadioPreferences prefs)
        {
            if (session != gs.SessionData.SessionId) { Reset(); session = gs.SessionData.SessionId; }
            foreach (var option in AiRadioPreferences.Options)
                if (!due.ContainsKey(option.Id)) due[option.Id] = now.AddSeconds(prefs.NextDelaySeconds(option.Id, random));
        }
        internal bool StatusIsOverdue(DateTime now, AiRadioPreferences prefs) => prefs["status"] > 0 &&
            (outstanding?.Topic == "status" || due.TryGetValue("status", out var next) && now - next >= TimeSpan.FromSeconds(20));
        public ObservedRaceCall Take(RaceDataContext ctx, AiRadioPreferences prefs, bool busy, Func<TeamMember,bool> memberEnabled = null, bool filterUnchanged = true,string busyReason = null)
        {
            var gs = ctx.GameState; DateTime now = ctx.NowUtc;
            if(lastRoutine!=null && lastRoutine.PlaybackInterrupted && !interruptionHandled)
            {
                due[lastRoutine.Topic]=now.AddSeconds(30);
                lastDeliveredText.Remove(lastRoutine.Topic);lastDeliveredUtc.Remove(lastRoutine.Topic);
                facts.Forget(lastRoutine.Topic);
                if (lastRoutine.Topic == "fuel") lastFuelMeasurement = null;
                interruptionHandled=true;
                LiveDiagnostics.Decision(lastRoutine.Topic,"deferred","routine_interrupted_recompose_after_30s");
            }
            if (outstanding != null)
            {
                if (outstanding.RaceStatus && outstanding.WasDeferred && !outstanding.IsReserved && !outstanding.DeliveryFailed &&
                    outstanding.DeliveryStartedUtc == DateTime.MinValue)
                    outstanding.RefreshBeforeSpeak(ctx);
                if (outstanding.DeliveryStartedUtc != DateTime.MinValue)
                {
                    lastRoutine=outstanding;interruptionHandled=false;
                    lastReport = outstanding.DeliveryStartedUtc;
                    lastDeliveredText[outstanding.Topic]=outstanding.Text;
                    lastDeliveredUtc[outstanding.Topic]=outstanding.DeliveryStartedUtc;
                    facts.Remember(outstanding.Topic, outstandingFacts, outstanding.DeliveryStartedUtc);
                    if (outstanding.Topic == "fuel") lastFuelMeasurement = outstandingFuelMeasurement;
                    if (IsTechnical(outstanding.Topic) && TechnicalSpacingSeconds > 0)
                    {
                        lastTechnicalUtc = outstanding.DeliveryStartedUtc; lastTechnicalTopic = outstanding.Topic;
                        technicalGapSeconds = TechnicalSpacingSeconds + random.Next(0, 61);
                    }
                    due[outstanding.Topic] = lastReport.AddSeconds(prefs.NextDelaySeconds(outstanding.Topic, random));
                    outstanding = null;
                }
                else if (!outstanding.IsValid(gs, now) || prefs[outstanding.Topic] == 0 ||
                    (memberEnabled != null && !memberEnabled(outstanding.Member)))
                {
                    LiveDiagnostics.Decision(outstanding.Topic,"suppressed","report_expired_or_disabled",outstanding.Evidence);
                    // Retry with a fresh snapshot later, without a tight TTS-error loop.
                    due[outstanding.Topic] = now.AddSeconds(outstanding.Topic == "status" ? 5 : 30);
                    outstanding = null;
                }
            }
            bool statusOverdue = gs?.SessionData.SessionType == SessionType.Race && StatusIsOverdue(now, prefs);
            int quietSeconds = statusOverdue ? 4 : QuietAfterRadioSeconds;
            bool recentRadio = ctx.Memory != null && ctx.Memory.HasRadioSince(TimeSpan.FromSeconds(quietSeconds), now);
            bool racingQuiet = !busy && !recentRadio && RaceRadioDirector.SuppressBackgroundStatistics(ctx);
            if (gs == null || busy || recentRadio || racingQuiet || BusyForRoutine(gs) || ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > 1 ||
                (gs.SessionData.SessionType != SessionType.Race && !SessionLapObserver.Supported(gs.SessionData.SessionType)) || gs.SessionData.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane ||
                now - lastReport < TimeSpan.FromSeconds(statusOverdue ? 8 : RoutineSeparationSeconds))
            {
                if(due.Any(p=>prefs[p.Key]>0 && p.Value<=now) && now-lastDeferral>=TimeSpan.FromSeconds(15))
                {
                    string reason=busy?(busyReason??"channel_busy"):recentRadio?"recent_radio":racingQuiet?"close_battle_or_final_laps":gs!=null && gs.FlagData.isLocalYellow?"local_yellow":BusyForRoutine(gs)?"braking_or_qualifying_push":
                        ctx.TelemetryAgeSeconds<0 || ctx.TelemetryAgeSeconds>1?"telemetry_stale":"phase_pit_or_routine_spacing";
                    foreach(var item in due.Where(p=>prefs[p.Key]>0 && p.Value<=now))
                        LiveDiagnostics.Emit("routine.state",LiveDiagnostics.Data("topic",item.Key,"reason",reason,"due_utc",item.Value.ToString("o"),
                            "late_s",Math.Round((now-item.Value).TotalSeconds,3),"interval_s",prefs[item.Key]));
                    LiveDiagnostics.Decision("routine","deferred",reason);
                    DriverMemory.RememberObservation("routine","deferred",reason,null,gs==null?int.MinValue:gs.SessionData.SessionId);
                    lastDeferral=now;
                }
                return null;
            }
            if (outstanding != null) return outstanding.TryReserve(now) ? outstanding : null;
            foreach (string topic in due.Where(kv => prefs[kv.Key] > 0 && kv.Value <= now && (!statusOverdue || kv.Key == "status"))
                .OrderBy(kv => kv.Key == "status" && gs.SessionData.SessionType == SessionType.Race ? 0 : 1)
                .ThenBy(kv => kv.Value).Select(kv => kv.Key).ToArray())
            {
                // Technik-Budget: das Thema bleibt fällig und kommt, sobald die gemeinsame Pause vorbei ist.
                if (TechnicalBudgetBlocks(topic, now)) continue;
                due[topic] = now.AddSeconds(prefs.NextDelaySeconds(topic, random));
                TeamMember member=topic == "weather" || topic == "tyres" || topic == "pace" || topic == "vehicle" ? TeamMember.Mik : TeamMember.Ben;
                if (memberEnabled!=null && !memberEnabled(member)) continue;
                var cursor = topic == "fuel" ? fuelVariants : topic == "ahead" ? aheadVariants : topic == "behind" ? behindVariants : null;
                string gapGroup = topic == "ahead" ? "gap-front-measured" : topic == "behind" ? "gap-rear-measured" : null;
                int gapVariant = gapGroup == null ? 0 : cursor.Select(gapGroup, RadioPhraseCatalog.Current.Count(gapGroup));
                string variantGroup = topic == "fuel" ? FuelGroup(ctx) : gapGroup;
                int index = topic == "fuel" ? cursor.Select(variantGroup, RadioPhraseCatalog.Current.Count(variantGroup)) : topic == "tyres" ? tyreReports++ : gapVariant;
                string text = Compose(topic, ctx, gapGroup == null ? index : gapVariant * 2 + (cursor.Heard & 1), out var template);
                if (string.IsNullOrEmpty(text)) { LiveDiagnostics.Decision(topic, "suppressed", "insufficient_evidence"); continue; }
                string fuelMeasurement = topic == "fuel" ? FuelMeasurementKey(ctx) : null;
                if (fuelMeasurement != null && fuelMeasurement == lastFuelMeasurement &&
                    lastDeliveredUtc.TryGetValue(topic, out var lastFuel) && now - lastFuel < TimeSpan.FromMinutes(10) &&
                    FuelUnchangedAcrossLaps(ctx))
                {
                    LiveDiagnostics.Decision(topic, "suppressed", "unchanged_fuel_measurement");
                    continue;
                }
                var measurement = StintRadioMemory.Capture(topic, ctx);
                if (filterUnchanged && (measurement != null ? facts.Unchanged(topic, measurement, now) : UnchangedRoutine(topic, text, now)))
                {
                    LiveDiagnostics.Decision(topic, "suppressed", "unchanged_routine_since_last_delivery");
                    continue;
                }
                string trend = facts.Trend(topic, measurement, now);
                if (trend != null) text += " " + trend;
                outstandingFacts = measurement;
                outstandingFuelMeasurement = fuelMeasurement;
                int overall = gs.SessionData.OverallPosition;
                bool raceStatus = topic == "status" && gs.SessionData.SessionType == SessionType.Race;
                bool overallSpoken = raceStatus && RaceRadioLanguage.SeparateClass(gs.SessionData) &&
                    overall > 0 && overall != gs.SessionData.ClassPosition;
                outstanding = new ObservedRaceCall { Topic=topic, Text=text, Evidence="configured_interval_current_measurements", Priority=raceStatus ? 3 : 1, VariantCursor=cursor,
                    ReferenceTemplate=template, RaceStatus=raceStatus,
                    Member=topic == "weather" || topic == "tyres" || topic == "pace" || topic == "vehicle" ? TeamMember.Mik : TeamMember.Ben,
                    AdditionalValidity=live=>(raceStatus ? !live.FlagData.isLocalYellow : !BusyForRoutine(live)) &&
                        (!overallSpoken || live.SessionData.OverallPosition == overall),
                    Position=topic == "status" ? gs.SessionData.ClassPosition : -1,
                    SessionId=gs.SessionData.SessionId, SessionType=gs.SessionData.SessionType, DetectedUtc=now, ExpiresUtc=now.AddSeconds(raceStatus ? 10 : 6) };
                if (gs.SessionData.SessionType == SessionType.Race && (topic == "ahead" || topic == "behind"))
                    outstanding.IncludeNeighbour(ctx, topic == "ahead", false);
                if (variantGroup != null && template != null) outstanding.BindVariant(cursor, variantGroup, index);
                LiveDiagnostics.Decision(topic,"detected",outstanding.Evidence,text,outstanding.EventId);
                return outstanding.TryReserve(now) ? outstanding : null;
            }
            return null;
        }
        public void NoteRadio(DateTime now) { lastReport = now; }

        private static string FuelMeasurementKey(RaceDataContext context)
        {
            float perLap = context.Memory?.AverageFuelPerLap() ?? -1;
            float litres = context.GameState.FuelData.FuelLeft;
            return Number(litres) + (Good(perLap, .1, 100) ? "|" + Number(litres / perLap) : "");
        }
        /// <summary>Kurze, abwechselnde Formulierung der geschätzten Hochrechnung; immer als Schätzung erkennbar.</summary>
        internal static string FuelEstimateLine(float perLap, float laps, int variant)
        {
            string l = Number(perLap), r = ((int)Math.Floor(laps)).ToString(RadioLanguage.Culture);
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T("Grob geschätzt " + l + " Liter pro Runde, das wird mit jeder Kurve genauer.",
                    "Rough estimate " + l + " litres a lap, it sharpens every corner.", "Estimación aproximada: " + l + " litros por vuelta, se afina en cada curva.");
                case 1: return RadioLanguage.T("Hochgerechnet etwa " + l + " Liter pro Runde, noch eine Schätzung.",
                    "Projected about " + l + " litres a lap, still an estimate.", "Proyectado unos " + l + " litros por vuelta, aún es una estimación.");
                case 2: return RadioLanguage.T("Erste Schätzung: rund " + l + " Liter pro Runde, reicht damit für etwa " + r + " Runden.",
                    "First estimate: around " + l + " litres a lap, good for about " + r + " laps.", "Primera estimación: unos " + l + " litros por vuelta, da para unas " + r + " vueltas.");
                default: return RadioLanguage.T("Nach den bisherigen Metern geschätzt " + l + " Liter pro Runde, gemessen ist es noch nicht.",
                    "Going by the distance so far, an estimated " + l + " litres a lap, not measured yet.", "Por los metros recorridos, estimados " + l + " litros por vuelta, aún sin medir.");
            }
        }
        private static string FuelGroup(RaceDataContext ctx) =>
            Good(ctx.Memory?.AverageFuelPerLap() ?? -1, .1, 100) ? "fuel-range" : "fuel-tank";

        private static bool FuelUnchangedAcrossLaps(RaceDataContext context)
        {
            var laps = context.Memory?.GetLaps(3);
            float litres = context.GameState.FuelData.FuelLeft;
            return laps?.Count == 3 && laps[2].LapNumber == context.GameState.SessionData.CompletedLaps &&
                laps[0].LapNumber == laps[2].LapNumber - 2 &&
                laps.All(lap => lap.Valid && !lap.InPitLap && Math.Abs(lap.FuelAtEnd - litres) < .05f);
        }
        // Telemetry thread only, after a native report actually starts. Detection alone never postpones a routine.
        public void NoteRelatedReport(string topic,DateTime delivered,AiRadioPreferences prefs)
        {
            if(topic!="ahead" && topic!="behind" || prefs==null)return;
            due[topic]=delivered.AddSeconds(prefs.NextDelaySeconds(topic,random));
            lastReport=delivered;
            if(outstanding!=null && outstanding.Topic==topic && outstanding.DeliveryStartedUtc==DateTime.MinValue) {
                outstanding.ExpiresUtc=delivered.AddTicks(-1);outstanding=null;
            }
        }
        private static string Number(double n) { return n.ToString("0.0", RadioLanguage.Culture); }
        private static string Capitalize(string s) { return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }
        private static bool Good(double n, double min, double max) { return !double.IsNaN(n) && !double.IsInfinity(n) && n >= min && n <= max; }
        public static string VehicleReport(GameStateData gs, bool brief = false)
        {
            var parts=new List<string>();var d=gs.CarDamageData;
            string damage=IncidentWatch.DamageReport(d, brief);if(!string.IsNullOrWhiteSpace(damage))parts.Add(damage);
            var e=gs.EngineData;
            bool water = Good(e.EngineWaterTemp,1,200), oil = Good(e.EngineOilTemp,1,250);
            if (brief && water && oil)
                parts.Add(RadioLanguage.T("Wasser ", "Water ", "Agua ") + Math.Round(e.EngineWaterTemp) + RadioLanguage.T(", Öl ", ", oil ", ", aceite ") + Math.Round(e.EngineOilTemp) + RadioLanguage.T(" Grad.", " degrees.", " grados."));
            else
            {
                if(water)parts.Add(RadioLanguage.T("Wasser ", "Water ", "Agua ")+Number(e.EngineWaterTemp)+RadioLanguage.T(" Grad.", " degrees.", " grados."));
                if(oil)parts.Add(RadioLanguage.T("Öl ", "Oil ", "Aceite ")+Number(e.EngineOilTemp)+RadioLanguage.T(" Grad.", " degrees.", " grados."));
            }
            var t=gs.TyreData;float[] brakes={t.LeftFrontBrakeTemp,t.RightFrontBrakeTemp,t.LeftRearBrakeTemp,t.RightRearBrakeTemp};
            if(brakes.All(b=>Good(b,1,1500)))parts.Add(brief ? RadioLanguage.T("Bremsen bis ", "Brakes up to ", "Frenos hasta ") + Math.Round(brakes.Max()) + RadioLanguage.T(" Grad.", " degrees.", " grados.") :
                RadioLanguage.T("Höchste Bremstemperatur ", "Peak brake temperature ", "Temperatura máxima de frenos ")+Number(brakes.Max())+RadioLanguage.T(" Grad.", " degrees.", " grados."));
            if(t.TyreWearActive && (!brief || parts.Count == 0)) { float[] wear={t.FrontLeftPercentWear,t.FrontRightPercentWear,t.RearLeftPercentWear,t.RearRightPercentWear};
                if(wear.All(w=>Good(w,0,100)))parts.Add(RadioLanguage.T("Höchster Reifenverschleiß ", "Highest tyre wear ", "Desgaste máximo de neumáticos ")+Number(wear.Max())+RadioLanguage.T(" Prozent.", " percent.", " por ciento.")); }
            return parts.Count>0?string.Join(" ",parts):null;
        }

        public static string Compose(string topic, RaceDataContext ctx, int variant = 0) => Compose(topic, ctx, variant, out _);

        private static string Compose(string topic, RaceDataContext ctx, int variant, out string template)
        {
            template = null;
            var gs = ctx.GameState;
            if (topic == "ahead" || topic == "behind")
            {
                bool ahead = topic == "ahead"; var op = RaceDataTools.Neighbour(gs, ahead);
                if (gs.SessionData.SessionType != SessionType.Race)
                {
                    if (op == null || !op.IsActive || ctx.Tracks != null && !ctx.Tracks.OpponentFresh(ctx, op) ||
                        !Good(op.CurrentBestLapTime,10,3600) || !Good(gs.SessionData.PlayerLapTimeSessionBest,10,3600)) return null;
                    float difference = Math.Abs(op.CurrentBestLapTime-gs.SessionData.PlayerLapTimeSessionBest);
                    string qualifyingName = ctx.SendDriverNames && op.CanUseName ? RadioPhraseCatalog.SafeRivalName(op.DriverRawName) : "";
                    return (qualifyingName.Length == 0 ? RadioLanguage.T("In der Zeitenliste " + (ahead ? "vor" : "hinter") + " dir: ",
                            (ahead ? "Ahead of you" : "Behind you") + " on the timesheet: ",
                            (ahead ? "Por delante" : "Por detrás") + " en la tabla de tiempos: ") : qualifyingName + ": ") + LocalAnswers.LapTime(op.CurrentBestLapTime) +
                        RadioLanguage.T(". Die Bestzeiten liegen ", ". Best laps are ", ". Los mejores tiempos están a ") + Number(difference) +
                        RadioLanguage.T(" Sekunden auseinander.", " seconds apart.", " segundos.");
                }
                var measurement = RaceDataTools.ReadGap(ctx, op, ahead);
                float gap = measurement.Seconds;
                if (op == null || !op.IsActive || !measurement.IsRaceGap || !Good(gap, 0.05, 300)) return null;
                string name = ctx.SendDriverNames && op.CanUseName ? RadioPhraseCatalog.SafeRivalName(op.DriverRawName) : "";
                string duration = (measurement.IsEstimated ? RadioLanguage.T("ungefähr ", "about ", "unos ") : "") + Number(gap) + RadioLanguage.T(" Sekunden", " seconds", " segundos");
                bool separate = RaceRadioLanguage.SeparateClass(gs.SessionData);
                if (!string.IsNullOrWhiteSpace(name)) template = RadioPhraseCatalog.Current.ReferenceFor(
                    ahead ? "gap-front-measured" : "gap-rear-measured", variant / 2);
                return RaceRadioLanguage.MeasuredGap(ahead, name, duration, separate, variant / 2);
            }
            if (topic == "weather")
            {
                var c = gs.Conditions.LatestConditions; if (c == null) return null;
                var parts = new List<string>();
                if (Good(c.AmbientTemperature, -30, 60)) parts.Add(RadioLanguage.T("Luft ", "air ", "aire ") + Number(c.AmbientTemperature) + RadioLanguage.T(" Grad", " degrees", " grados"));
                if (Good(c.TrackTemperature, -30, 85)) parts.Add(RadioLanguage.T("Strecke ", "track ", "pista ") + Number(c.TrackTemperature) + RadioLanguage.T(" Grad", " degrees", " grados"));
                if (Good(c.RainDensity, .01, 1)) parts.Add(RadioLanguage.T("Regen wird gemeldet", "rain reported", "se informa de lluvia"));
                return parts.Count > 0 ? Capitalize(string.Join(", ", parts) + ".") : null;
            }
            if (topic == "fuel")
            {
                if (!gs.FuelData.FuelUseActive || !Good(gs.FuelData.FuelLeft, .1, 1000)) return null;
                float perLap = ctx.Memory == null ? -1 : ctx.Memory.AverageFuelPerLap();
                string litres = Number(gs.FuelData.FuelLeft);
                var catalog = RadioPhraseCatalog.Current;
                if (!Good(perLap, .1, 100))
                {
                    template=catalog.ReferenceFor("fuel-tank",variant);
                    string tank = catalog.Format("fuel-tank", variant, litres);
                    // Noch keine volle gemessene Runde: ausdrücklich geschätzte Hochrechnung aus gefahrener Strecke (Rob 16.11.).
                    float estimate = ctx.Memory == null ? -1 : ctx.Memory.ProvisionalFuelPerLap(gs);
                    return Good(estimate, .1, 100) ? tank + " " + FuelEstimateLine(estimate, gs.FuelData.FuelLeft / estimate, variant) : tank;
                }
                string laps = Number(gs.FuelData.FuelLeft / perLap);
                template=catalog.ReferenceFor("fuel-range",variant);
                string verdict = FuelEstimate.From(ctx)?.Verdict();
                return catalog.Format("fuel-range", variant, litres, laps) + (verdict == null ? "" : " " + verdict);
            }
            if (topic == "tyres")
            {
                var t = gs.TyreData; if (!t.TyreWearActive) return null;
                float[] wear = { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear };
                if (!wear.All(w => Good(w, 0, 100))) return null;
                // Rob 16.11.: abwechselnd technisch und menschlich, mit fundierter Hochrechnung (nur kalibrierte Simulationen).
                // Ohne messbaren Verschleiß (manche Sims melden dauerhaft 0) gibt es nichts zu berichten.
                return TyreWearAdvisor.Report(wear, ctx.Memory?.AverageWearPerLap(3), TyreWearAdvisor.Calibrated(Game.gameType),
                    AiPersonas.CleanForRadio(ctx.DriverName ?? "", 30, false), variant);
            }
            if (topic == "pace")
            {
                var laps = ctx.Memory == null ? new LapRecord[0] : ctx.Memory.GetLaps(3).ToArray();
                if (laps.Length < 3 || !laps.All(l => l.Valid && !l.InPitLap && Good(l.LapTime, 20, 1200)) ||
                    laps[1].LapNumber != laps[0].LapNumber + 1 || laps[2].LapNumber != laps[1].LapNumber + 1) return null;
                return RadioLanguage.T("Die letzten drei sauberen Runden im Mittel ", "Last three clean laps average ", "Media de las últimas tres vueltas limpias: ") + LocalAnswers.LapTime(laps.Average(l => l.LapTime)) +
                    RadioLanguage.T(". Die Runden liegen innerhalb von ", ". All within ", ". Todas dentro de ") + Number(laps.Max(l => l.LapTime) - laps.Min(l => l.LapTime)) + RadioLanguage.T(" Sekunden.", " seconds.", " segundos.");
            }
            if (topic == "status") {
                if (gs.SessionData.SessionType == SessionType.Race)
                {
                    var session = gs.SessionData;
                    if (session.ClassPosition <= 0) return null;
                    string rank = RaceRadioLanguage.Rank(session, session.ClassPosition);
                    if (RaceRadioLanguage.SeparateClass(session) && session.OverallPosition > 0 && session.OverallPosition != session.ClassPosition)
                        rank += RadioLanguage.T(", gesamt Platz " + session.OverallPosition, ", P" + session.OverallPosition + " overall", ", P" + session.OverallPosition + " en la general");
                    int start = session.SessionStartClassPosition;
                    int change = start > 0 ? start - session.ClassPosition : 0;
                    if (change == 0) return rank + ". " + Compose("progress", ctx);
                    int count = Math.Abs(change);
                    return rank + RadioLanguage.T(". Seit dem Start " + count + (count == 1 ? " Platz" : " Plätze") + (change > 0 ? " gutgemacht." : " verloren."),
                        ". " + count + (count == 1 ? " place " : " places ") + (change > 0 ? "gained" : "lost") + " since the start.",
                        ". " + count + (count == 1 ? " posición " : " posiciones ") + (change > 0 ? (count == 1 ? "ganada" : "ganadas") : (count == 1 ? "perdida" : "perdidas")) + " desde la salida.");
                }
                var s=gs.SessionData;
                return Good(s.PlayerLapTimeSessionBest,10,3600) ? RadioLanguage.T("Deine bisherige Bestzeit: ", "Your best time so far: ", "Tu mejor tiempo hasta ahora: ") + LocalAnswers.LapTime(s.PlayerLapTimeSessionBest) + "." +
                    (s.ClassPosition>0 ? RadioLanguage.T(" Aktuell Platz "+s.ClassPosition+" in der Zeitenliste.", " Currently P"+s.ClassPosition+" on the timesheet.", " Ahora mismo P"+s.ClassPosition+" en la tabla de tiempos.") : "") : null;
            }
            if (topic == "vehicle") return VehicleReport(gs, brief: true);
            if (topic == "progress") {
                var s=gs.SessionData;
                if(s.SessionHasFixedTime && Good(s.SessionTimeRemaining,1,200000)) {
                    double minutes=Math.Ceiling(s.SessionTimeRemaining/60);
                    return RadioLanguage.T("Noch etwa "+minutes+(minutes==1?" Minute":" Minuten")+" auf der Uhr.",
                        "About "+minutes+(minutes==1?" minute":" minutes")+" left on the clock.",
                        minutes==1?"Queda más o menos 1 minuto en el reloj.":"Quedan unos "+minutes+" minutos en el reloj.");
                }
                if(!s.SessionHasFixedTime && s.SessionNumberOfLaps>0 && s.SessionLapsRemaining>0 && s.SessionLapsRemaining<10000)
                    return RadioLanguage.T("Noch "+s.SessionLapsRemaining+(s.SessionLapsRemaining==1?" Runde.":" Runden."),
                        s.SessionLapsRemaining+(s.SessionLapsRemaining==1?" lap to go.":" laps to go."),
                        s.SessionLapsRemaining==1?"Queda 1 vuelta.":"Quedan "+s.SessionLapsRemaining+" vueltas.");
                return null;
            }
            if(topic == "stint") {
                if(ctx.Memory==null)return null;
                float fuel=ctx.Memory.AverageFuelPerLap();var wear=ctx.Memory.AverageWearPerLap();var parts=new List<string>();
                if(Good(fuel,.1,100))parts.Add(RadioLanguage.T("Gemessener Verbrauch ", "Measured consumption ", "Consumo medido ")+Number(fuel)+RadioLanguage.T(" Liter pro Runde", " litres per lap", " litros por vuelta"));
                if(wear!=null && wear.Length==4 && wear.All(w=>Good(w,0,100)) && wear.Max()>=0.1)parts.Add(RadioLanguage.T("Der Reifen mit dem höchsten Verschleiß verliert ", "Most worn tyre loses ", "El neumático más gastado pierde ") + Number(wear.Max()) + RadioLanguage.T(" Prozentpunkte pro Runde", " percentage points per lap", " puntos porcentuales por vuelta"));
                return parts.Count>0?string.Join(". ",parts)+".":null;
            }
            return null;
        }
    }
}
