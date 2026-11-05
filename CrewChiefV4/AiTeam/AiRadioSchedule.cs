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
            new RadioOption { Id="fuel", Label="Sprit und gemessene Reichweite", DefaultSeconds=120, Minimum=30, Hint="Reichweite erst mit sauber gemessenem Verbrauch." },
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
        { return seconds[id]==0?0:seconds[id]+random.Next(-minus[id],plus[id]+1); }
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
        private bool interruptionHandled;
        private readonly Dictionary<string,string> lastDeliveredText=new Dictionary<string,string>();
        private readonly Dictionary<string,DateTime> lastDeliveredUtc=new Dictionary<string,DateTime>();
        public static bool OnConfirmedQualifyingPushLap(GameStateData gs)
        {
            return gs!=null && gs.SessionData.SessionType==SessionType.Qualify && gs.SessionData.CompletedLaps>0 &&
                !gs.PitData.OnOutLap && !gs.PitData.OnInLap && !gs.PitData.InPitlane &&
                !TrackAwareness.InRange(gs.PositionAndMotionData.CarSpeed,0,4.99).HasValue;
        }
        public static bool BusyForRoutine(GameStateData gs)
        {return OnConfirmedQualifyingPushLap(gs) || gs!=null && gs.SessionData.SessionType==SessionType.HotLap &&
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
        public void Reset() { due.Clear(); lastDeliveredText.Clear(); lastDeliveredUtc.Clear(); outstanding = lastRoutine = null; interruptionHandled=false; lastReport = lastDeferral = DateTime.MinValue; session = int.MinValue; }
        public void Observe(GameStateData gs, DateTime now, AiRadioPreferences prefs)
        {
            if (session != gs.SessionData.SessionId) { Reset(); session = gs.SessionData.SessionId; }
            foreach (var option in AiRadioPreferences.Options)
                if (!due.ContainsKey(option.Id)) due[option.Id] = now.AddSeconds(prefs.NextDelaySeconds(option.Id, random));
        }
        public ObservedRaceCall Take(RaceDataContext ctx, AiRadioPreferences prefs, bool busy, Func<TeamMember,bool> memberEnabled = null, bool filterUnchanged = true,string busyReason = null)
        {
            var gs = ctx.GameState; DateTime now = ctx.NowUtc;
            if(lastRoutine!=null && lastRoutine.PlaybackInterrupted && !interruptionHandled)
            {
                due[lastRoutine.Topic]=now.AddSeconds(30);
                lastDeliveredText.Remove(lastRoutine.Topic);lastDeliveredUtc.Remove(lastRoutine.Topic);
                interruptionHandled=true;
                LiveDiagnostics.Decision(lastRoutine.Topic,"deferred","routine_interrupted_recompose_after_30s");
            }
            if (outstanding != null)
            {
                if (outstanding.DeliveryStartedUtc != DateTime.MinValue)
                {
                    lastRoutine=outstanding;interruptionHandled=false;
                    lastReport = outstanding.DeliveryStartedUtc;
                    lastDeliveredText[outstanding.Topic]=outstanding.Text;
                    lastDeliveredUtc[outstanding.Topic]=outstanding.DeliveryStartedUtc;
                    due[outstanding.Topic] = lastReport.AddSeconds(prefs.NextDelaySeconds(outstanding.Topic, random));
                    outstanding = null;
                }
                else if (!outstanding.IsValid(gs, now) || prefs[outstanding.Topic] == 0 ||
                    (memberEnabled != null && !memberEnabled(outstanding.Member)))
                {
                    LiveDiagnostics.Decision(outstanding.Topic,"suppressed","report_expired_or_disabled",outstanding.Evidence);
                    // Retry with a fresh snapshot later, without a tight TTS-error loop.
                    due[outstanding.Topic] = now.AddSeconds(30);
                    outstanding = null;
                }
            }
            bool recentRadio = ctx.Memory != null && ctx.Memory.GetRadio(TimeSpan.FromSeconds(QuietAfterRadioSeconds), now).Any(r => r.TimeUtc <= now);
            if (gs == null || busy || recentRadio || BusyForRoutine(gs) || ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > 1 ||
                (gs.SessionData.SessionType != SessionType.Race && !SessionLapObserver.Supported(gs.SessionData.SessionType)) || gs.SessionData.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane ||
                now - lastReport < TimeSpan.FromSeconds(RoutineSeparationSeconds))
            {
                if(due.Any(p=>prefs[p.Key]>0 && p.Value<=now) && now-lastDeferral>=TimeSpan.FromSeconds(15))
                {
                    string reason=busy?(busyReason??"channel_busy"):recentRadio?"recent_radio":BusyForRoutine(gs)?"braking_or_qualifying_push":
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
            foreach (string topic in due.Where(kv => prefs[kv.Key] > 0 && kv.Value <= now)
                .OrderBy(kv => kv.Value).Select(kv => kv.Key).ToArray())
            {
                due[topic] = now.AddSeconds(prefs.NextDelaySeconds(topic, random));
                TeamMember member=topic == "weather" || topic == "tyres" || topic == "pace" || topic == "vehicle" ? TeamMember.Mik : TeamMember.Ben;
                if (memberEnabled!=null && !memberEnabled(member)) continue;
                string text = Compose(topic, ctx);
                if (string.IsNullOrEmpty(text)) { LiveDiagnostics.Decision(topic, "suppressed", "insufficient_evidence"); continue; }
                if(filterUnchanged && UnchangedRoutine(topic,text,now)) { LiveDiagnostics.Decision(topic,"suppressed","unchanged_routine_since_last_delivery"); continue; }
                outstanding = new ObservedRaceCall { Topic=topic, Text=text, Evidence="configured_interval_current_measurements", Priority=1,
                    Member=topic == "weather" || topic == "tyres" || topic == "pace" || topic == "vehicle" ? TeamMember.Mik : TeamMember.Ben,
                    AdditionalValidity=live=>!BusyForRoutine(live),
                    Position=topic == "status" ? gs.SessionData.ClassPosition : -1,
                    SessionId=gs.SessionData.SessionId, SessionType=gs.SessionData.SessionType, DetectedUtc=now, ExpiresUtc=now.AddSeconds(6) };
                LiveDiagnostics.Decision(topic,"detected",outstanding.Evidence,text);
                return outstanding.TryReserve(now) ? outstanding : null;
            }
            return null;
        }
        public void NoteRadio(DateTime now) { lastReport = now; }
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
        private static string Number(double n) { return n.ToString("0.0", CultureInfo.GetCultureInfo("de-DE")); }
        private static bool Good(double n, double min, double max) { return !double.IsNaN(n) && !double.IsInfinity(n) && n >= min && n <= max; }
        public static string VehicleReport(GameStateData gs)
        {
            var parts=new List<string>();var d=gs.CarDamageData;
            string damage=IncidentWatch.DamageReport(d);if(!string.IsNullOrWhiteSpace(damage))parts.Add(damage);
            var e=gs.EngineData;
            if(Good(e.EngineWaterTemp,1,200))parts.Add("Wasser "+Number(e.EngineWaterTemp)+" Grad.");
            if(Good(e.EngineOilTemp,1,250))parts.Add("Öl "+Number(e.EngineOilTemp)+" Grad.");
            var t=gs.TyreData;float[] brakes={t.LeftFrontBrakeTemp,t.RightFrontBrakeTemp,t.LeftRearBrakeTemp,t.RightRearBrakeTemp};
            if(brakes.All(b=>Good(b,1,1500)))parts.Add("Höchste Bremstemperatur "+Number(brakes.Max())+" Grad.");
            if(t.TyreWearActive) { float[] wear={t.FrontLeftPercentWear,t.FrontRightPercentWear,t.RearLeftPercentWear,t.RearRightPercentWear};
                if(wear.All(w=>Good(w,0,100)))parts.Add("Höchster Reifenverschleiß "+Number(wear.Max())+" Prozent."); }
            return parts.Count>0?string.Join(" ",parts):null;
        }

        public static string Compose(string topic, RaceDataContext ctx)
        {
            var gs = ctx.GameState;
            if (topic == "ahead" || topic == "behind")
            {
                bool ahead = topic == "ahead"; var op = RaceDataTools.Neighbour(gs, ahead);
                if (gs.SessionData.SessionType != SessionType.Race)
                {
                    if (op == null || !op.IsActive || !Good(op.CurrentBestLapTime,10,3600) || !Good(gs.SessionData.PlayerLapTimeSessionBest,10,3600)) return null;
                    float difference = Math.Abs(op.CurrentBestLapTime-gs.SessionData.PlayerLapTimeSessionBest);
                    return "In der Zeitenliste " + (ahead ? "vor" : "hinter") + " dir: " + LocalAnswers.LapTime(op.CurrentBestLapTime) +
                        ". Die Bestzeiten liegen " + Number(difference) + " Sekunden auseinander.";
                }
                var measurement = RaceDataTools.ReadGap(ctx, op, ahead);
                float gap = measurement.Seconds;
                if (op == null || !op.IsActive || !measurement.IsRaceGap || !Good(gap, 0.05, 300)) return null;
                string name = ctx.SendDriverNames ? new string((op.DriverRawName ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-').Take(60).ToArray()) : "";
                return (ahead ? "Vor dir " : "Hinter dir ") + (name.Length > 0 ? name + ", " : "") +
                    (measurement.IsEstimated ? "ungefähr " : "") + Number(gap) + " Sekunden Abstand.";
            }
            if (topic == "weather")
            {
                var c = gs.Conditions.LatestConditions; if (c == null) return null;
                var parts = new List<string>();
                if (Good(c.AmbientTemperature, -30, 60)) parts.Add("Luft " + Number(c.AmbientTemperature) + " Grad");
                if (Good(c.TrackTemperature, -30, 85)) parts.Add("Strecke " + Number(c.TrackTemperature) + " Grad");
                if (Good(c.RainDensity, .01, 1)) parts.Add("Regen wird gemeldet");
                return parts.Count > 0 ? string.Join(", ", parts) + "." : null;
            }
            if (topic == "fuel")
            {
                if (!gs.FuelData.FuelUseActive || !Good(gs.FuelData.FuelLeft, .1, 1000)) return null;
                float perLap = ctx.Memory == null ? -1 : ctx.Memory.AverageFuelPerLap();
                string text = "Noch " + Number(gs.FuelData.FuelLeft) + " Liter im Tank.";
                if (Good(perLap, .1, 100)) text += " Das reicht bei dem Verbrauch für etwa " + Number(gs.FuelData.FuelLeft / perLap) + " Runden Reichweite.";
                return text;
            }
            if (topic == "tyres")
            {
                var t = gs.TyreData; if (!t.TyreWearActive) return null;
                float[] wear = { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear };
                if (!wear.All(w => Good(w, 0, 100))) return null;
                int worst = Array.IndexOf(wear, wear.Max());
                return "Am stärksten abgenutzt ist der Reifen " + new[] { "vorne links", "vorne rechts", "hinten links", "hinten rechts" }[worst] +
                    ": " + Number(wear[worst]) + " Prozent Verschleiß.";
            }
            if (topic == "pace")
            {
                var laps = ctx.Memory == null ? new LapRecord[0] : ctx.Memory.GetLaps(3).ToArray();
                if (laps.Length < 3 || !laps.All(l => l.Valid && !l.InPitLap && Good(l.LapTime, 20, 1200)) ||
                    laps[1].LapNumber != laps[0].LapNumber + 1 || laps[2].LapNumber != laps[1].LapNumber + 1) return null;
                return "Die letzten drei sauberen Runden im Mittel " + LocalAnswers.LapTime(laps.Average(l => l.LapTime)) +
                    ". Die Runden liegen innerhalb von " + Number(laps.Max(l => l.LapTime) - laps.Min(l => l.LapTime)) + " Sekunden.";
            }
            if (topic == "status") {
                if (gs.SessionData.SessionType == SessionType.Race) return ActiveRaceObserver.Summary(gs,ctx.Memory==null?null:ctx.Memory.GetLaps(3),gs.SessionData.SessionStartClassPosition,ctx.DriverName);
                var s=gs.SessionData;
                return Good(s.PlayerLapTimeSessionBest,10,3600) ? "Deine bisherige Bestzeit: " + LocalAnswers.LapTime(s.PlayerLapTimeSessionBest) + "." + (s.ClassPosition>0 ? " Aktuell Platz "+s.ClassPosition+" in der Zeitenliste." : "") : null;
            }
            if (topic == "vehicle") return VehicleReport(gs);
            if (topic == "progress") {
                var s=gs.SessionData;
                if(s.SessionHasFixedTime && Good(s.SessionTimeRemaining,1,200000)) return "Noch etwa "+Math.Ceiling(s.SessionTimeRemaining/60)+(Math.Ceiling(s.SessionTimeRemaining/60)==1?" Minute":" Minuten")+" auf der Uhr.";
                if(!s.SessionHasFixedTime && s.SessionNumberOfLaps>0 && s.SessionLapsRemaining>0 && s.SessionLapsRemaining<10000)
                    return "Noch "+s.SessionLapsRemaining+(s.SessionLapsRemaining==1?" Runde.":" Runden.");
                return null;
            }
            if(topic == "stint") {
                if(ctx.Memory==null)return null;
                float fuel=ctx.Memory.AverageFuelPerLap();var wear=ctx.Memory.AverageWearPerLap();var parts=new List<string>();
                if(Good(fuel,.1,100))parts.Add("Gemessener Verbrauch "+Number(fuel)+" Liter pro Runde");
                if(wear!=null && wear.Length==4 && wear.All(w=>Good(w,0,100)))parts.Add("höchster gemessener Reifenabbau "+Number(wear.Max())+" Prozentpunkte pro Runde");
                return parts.Count>0?string.Join(". ",parts)+".":null;
            }
            return null;
        }
    }
}
