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
        internal bool TechnicalBudgetBlocks(string topic, DateTime now, AiRadioPreferences prefs = null) =>
            IsTechnical(topic) && lastTechnicalTopic != null && topic != lastTechnicalTopic &&
            now - lastTechnicalUtc < TimeSpan.FromSeconds(technicalGapSeconds) && !OverdueByItsInterval(topic, now, prefs);

        /// <summary>
        /// Abstand zwischen zwei Technikberichten: drei Viertel des kürzesten eingestellten Technik-Intervalls, mindestens 60 s,
        /// höchstens <see cref="TechnicalSpacingSeconds"/>. Mit den Standardwerten (4–5 Minuten) bleibt es bei 3–4 Minuten (Rob 16.11.);
        /// stellt Rob Sprit auf 2 Minuten (Monza 22.11.), sind es 90 s – sonst kam von fünf Technikthemen fast nichts.
        /// </summary>
        internal int TechnicalSpacingFor(AiRadioPreferences prefs)
        {
            if (TechnicalSpacingSeconds <= 0) return 0;
            int shortest = prefs == null ? 0 : AiRadioPreferences.Options.Where(o => IsTechnical(o.Id) && prefs[o.Id] > 0).Select(o => prefs[o.Id]).DefaultIfEmpty(0).Min();
            if (shortest <= 0) return TechnicalSpacingSeconds;
            return Math.Max(Math.Min(60, TechnicalSpacingSeconds), Math.Min(TechnicalSpacingSeconds, (int)Math.Round(shortest * .75)));
        }

        /// <summary>
        /// Fairness: Ein Thema, das schon ein ganzes eigenes Intervall (mindestens 60 s) zu spät ist, wartet nicht länger auf das
        /// gemeinsame Technik-Budget. Monza 22.11.: Wetter 7, Stint 7 und Sprit 8½ Minuten zu spät, nie gesprochen.
        /// </summary>
        internal bool OverdueByItsInterval(string topic, DateTime now, AiRadioPreferences prefs) =>
            prefs != null && prefs[topic] > 0 && due.TryGetValue(topic, out var at) && now - at >= TimeSpan.FromSeconds(Math.Max(60, prefs[topic]));

        /// <summary>
        /// Welcher fällige Bericht zuerst? Nicht der zuerst fällige, sondern der dringendste: Verspätung im Verhältnis zum eigenen
        /// Intervall, plus ein kleiner Vorrang für das, was im Rennen mehr zählt (Abstände, Stand, Fortschritt, Sprit vor Wetter).
        /// Ein 5-Minuten-Thema, das 5 Minuten zu spät ist, kommt so vor einem 2-Minuten-Thema, das 10 s zu spät ist.
        /// </summary>
        internal static double Urgency(string topic, double lateSeconds, int intervalSeconds)
        {
            double ratio = intervalSeconds > 0 ? Math.Max(0, lateSeconds) / intervalSeconds : 0;
            return ratio + RaceWeight(topic);
        }

        internal static double RaceWeight(string topic)
        {
            switch (topic)
            {
                case "ahead": case "behind": return .5;
                case "status": return .4;
                case "progress": case "fuel": return .3;
                case "tyres": case "pace": case "vehicle": return .2;
                case "stint": return .1;
                default: return 0;
            }
        }

        /// <summary>Rangfolge über alle Funksprüche; fällige Routine meldet sich hier an, damit andere ihr den Vortritt lassen.</summary>
        internal RadioArbiter Arbiter { get; } = new RadioArbiter();
        private bool interruptionHandled;
        /// <summary>
        /// LMU 21.11.: Von zehn fälligen Zwischenständen kam einer – jeder wartete im Zweikampf oder Verkehr, verfiel nach
        /// zehn Sekunden und wurde neu angesetzt. Rob: eingestellte Berichte kommen, nur eben in der nächsten ruhigen Lücke.
        /// Ab dieser Verspätung (der erste Stand nach dem Start sofort) wartet ein Bericht nicht mehr auf Zweikampf-Ruhe oder
        /// eine Hintergrund-Pause, sondern nur noch auf freien Funk, kein Bremsen und kein Gelb.
        /// </summary>
        internal const int GuaranteeLateSeconds = 30;
        private readonly Dictionary<string, DateTime> overdueSince = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        private DateTime outstandingSince = DateTime.MinValue;
        private bool firstStatusPending;

        /// <summary>Abstand zum direkten Vorder- oder Hintermann: Robs eigener Kampf, im Zweikampf nicht zurückgehalten.</summary>
        internal static bool FightGap(string topic) => topic == "ahead" || topic == "behind";

        /// <summary>Abstand im Zweikampf: halbes Intervall, frühestens nach 30 s.</summary>
        internal static double BattleGapDelay(double delay) => delay <= 0 ? delay : Math.Max(30, delay / 2);

        internal bool Guaranteed(string topic, DateTime now) =>
            topic != null && overdueSince.TryGetValue(topic, out var since) && now >= since &&
            (now - since >= TimeSpan.FromSeconds(GuaranteeLateSeconds) || topic == "status" && firstStatusPending);
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
        /// <summary>
        /// Was einen fertigen Routinebericht wirklich ungültig macht: Gelb und eine laufende Qualifying- oder Hotlap-Runde.
        /// Bremsen ist nur ein Grund zu warten (der Funk wartet ohnehin die Bremszone ab), kein Grund, den fertigen Bericht
        /// wegzuwerfen. (LMU 22.11.: Sprit 0 von 13, Reifen 1 von 9, Stint 0 von 7 Berichten im Rennen – sie wurden als
        /// zugesagte Berichte freigegeben und in derselben Sekunde im Zweikampf als „Hintergrundstatistik“ wieder verworfen;
        /// das behebt die Zustellung im Dienst, diese Regel verhindert dasselbe beim Bremsen.)
        /// </summary>
        public static bool InvalidatesRoutine(GameStateData gs)
        {return gs != null && gs.FlagData.isLocalYellow || OnConfirmedQualifyingPushLap(gs) || gs!=null && gs.SessionData.SessionType==SessionType.HotLap &&
            gs.SessionData.CurrentLapIsValid && !gs.PitData.OnOutLap && !gs.PitData.InPitlane;}
        /// <summary>
        /// LMU 22.11.: Ein Bericht lebte 6 s (Zwischenstand 10 s), der Rennkopf hält in 2-s-Schritten an, nach einem Sicherheitsfunk
        /// warten Daten 6 s – er verfiel fast immer, bevor er an der Reihe war. Jetzt darf er so lange auf die Lücke warten.
        /// </summary>
        internal const int RoutineLifetimeSeconds = 20, GuaranteedLifetimeSeconds = 30;
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
        internal bool SameTimesheetAgain(string topic, string text, DateTime now) =>
            lastDeliveredText.TryGetValue(topic, out var old) && old == text && lastDeliveredUtc.TryGetValue(topic, out var when) &&
            now >= when && now - when < TimeSpan.FromMinutes(20);
        private WeatherReading lastWeather, outstandingWeather;
        private int progressReports, vehicleReports;
        // Rob 17.11.: jede Routine-Meldung mit 4–6 Formulierungen; Zähler je Thema ohne eigenen Katalog-Cursor.
        private readonly Dictionary<string, int> topicTurns = new Dictionary<string, int>(StringComparer.Ordinal);
        private int weatherVariant;
        private int session = int.MinValue;
        private readonly Random random;
        public AiRadioSchedule(int? seed=null) { random=seed.HasValue?new Random(seed.Value):new Random(); }

        /// <summary>Startphase des Rennens: keine Routine, bis sich das Feld beruhigt hat (siehe <see cref="RaceRhythm"/>).</summary>
        internal bool StartCalm;
        /// <summary>Rennlänge; verlängert oder verkürzt die eingestellten Abstände je Thema.</summary>
        internal RaceLength Length;

        private int Delay(AiRadioPreferences prefs, string topic)
        {
            int delay = prefs.NextDelaySeconds(topic, random);
            return AiRadioPreferences.ApplyDriverScale(topic, delay, RaceRhythm.Scale(Length, topic));
        }

        /// <summary>
        /// Nach der Startphase die fälligen Themen nacheinander einplanen statt alle auf einmal: Status zuerst, dann
        /// Vorder- und Hintermann, Rennfortschritt, Pace und zuletzt die technischen Themen.
        /// </summary>
        internal void StaggerAfterStart(DateTime settled, AiRadioPreferences prefs)
        {
            string[] order = { "status", "ahead", "behind", "progress", "pace", "fuel", "tyres", "vehicle", "stint", "weather" };
            DateTime slot = settled + RaceRhythm.FirstRoutineAfterSettle;
            foreach (string topic in order)
            {
                if (prefs[topic] == 0 || !due.TryGetValue(topic, out var at) || at > slot) continue;
                due[topic] = slot;
                slot += RaceRhythm.RoutineStep;
            }
            overdueSince.Clear();
            firstStatusPending = prefs["status"] > 0;
            LiveDiagnostics.Decision("routine", "staggered", "after_start_phase_" + Length.ToString().ToLowerInvariant());
        }
        internal static TeamMember MemberFor(string topic) =>
            topic == "weather" || topic == "tyres" || topic == "pace" || topic == "vehicle" ? TeamMember.Mik : TeamMember.Ben;
        public void Reset() { Arbiter.Reset(); facts.Clear(); outstandingFacts = null; due.Clear(); lastDeliveredText.Clear(); lastDeliveredUtc.Clear(); outstanding = lastRoutine = null; lastFuelMeasurement = outstandingFuelMeasurement = null; interruptionHandled=false; lastReport = lastDeferral = lastTechnicalUtc = DateTime.MinValue; lastTechnicalTopic = null; technicalGapSeconds = 0; lastWeather = outstandingWeather = null; weatherVariant = 0; session = int.MinValue; lastFuelVerdict = null; overdueSince.Clear(); outstandingSince = DateTime.MinValue; firstStatusPending = false; }
        public void Observe(GameStateData gs, DateTime now, AiRadioPreferences prefs)
        {
            if (session != gs.SessionData.SessionId) { Reset(); session = gs.SessionData.SessionId; }
            foreach (var option in AiRadioPreferences.Options)
                if (!due.ContainsKey(option.Id)) due[option.Id] = now.AddSeconds(Delay(prefs, option.Id));
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
                    if (outstanding.Topic == "weather" && outstandingWeather != null) lastWeather = outstandingWeather;
                    if (IsTechnical(outstanding.Topic) && TechnicalSpacingSeconds > 0)
                    {
                        lastTechnicalUtc = outstanding.DeliveryStartedUtc; lastTechnicalTopic = outstanding.Topic;
                        int spacing = TechnicalSpacingFor(prefs);
                        technicalGapSeconds = spacing + random.Next(0, spacing / 3 + 1);
                    }
                    double next = Delay(prefs, outstanding.Topic);
                    if (FightGap(outstanding.Topic) && RaceRadioDirector.SuppressBackgroundStatistics(ctx)) next = BattleGapDelay(next);
                    due[outstanding.Topic] = lastReport.AddSeconds(next);
                    overdueSince.Remove(outstanding.Topic);
                    Arbiter.Done(outstanding.Topic);
                    if (outstanding.Topic == "status") firstStatusPending = false;
                    outstanding = null;
                }
                else if (!outstanding.IsValid(gs, now) || prefs[outstanding.Topic] == 0 ||
                    (memberEnabled != null && !memberEnabled(outstanding.Member)))
                {
                    LiveDiagnostics.Decision(outstanding.Topic,"suppressed","report_expired_or_disabled",outstanding.Evidence);
                    // Mit frischen Zahlen neu ansetzen, ohne enge Schleife bei Sprachfehlern. Ein weiter eingestellter Bericht
                    // behält seine Verspätung und kommt dadurch spätestens als zugesagter Bericht.
                    bool keep = prefs[outstanding.Topic] > 0 && (memberEnabled == null || memberEnabled(outstanding.Member)) &&
                        outstandingSince != DateTime.MinValue;
                    // LMU 22.11.: Ein verfallener, weiter eingestellter Bericht kommt nach 5 s mit frischen Zahlen wieder (bisher 30 s –
                    // dazwischen lag oft die nächste Bremszone, und er verfiel erneut).
                    due[outstanding.Topic] = now.AddSeconds(outstanding.Topic == "status" || keep ? 5 : 30);
                    if (keep) overdueSince[outstanding.Topic] = outstandingSince; else overdueSince.Remove(outstanding.Topic);
                    outstanding = null;
                }
            }
            foreach (var item in due.Where(p => prefs[p.Key] > 0 && p.Value <= now && !overdueSince.ContainsKey(p.Key)).ToArray())
                overdueSince[item.Key] = item.Value;
            if (outstanding != null) outstanding.Guaranteed = Guaranteed(outstanding.Topic, now);
            bool race = gs?.SessionData.SessionType == SessionType.Race;
            bool statusOverdue = race && StatusIsOverdue(now, prefs);
            bool promised = race && (outstanding?.Guaranteed == true || outstanding == null && due.Any(p => prefs[p.Key] > 0 && p.Value <= now && Guaranteed(p.Key, now)));
            // Monza 22.11.: 8 s Funkstille gab es im Rennen nur 40 % der Zeit (Median zwischen zwei Funksprüchen 4,4 s). Wer schon
            // 20 s wartet, braucht nur noch eine kurze Lücke – die anderen lassen ihm ohnehin den Vortritt.
            bool claimed = race && due.Any(p => prefs[p.Key] > 0 && now - p.Value >= TimeSpan.FromSeconds(RadioArbiter.RoutineClaimSeconds));
            int quietSeconds = statusOverdue || promised || claimed ? 4 : QuietAfterRadioSeconds;
            bool recentRadio = ctx.Memory != null && ctx.Memory.HasRadioSince(TimeSpan.FromSeconds(quietSeconds), now);
            bool battleQuiet = RaceRadioDirector.SuppressBackgroundStatistics(ctx);
            // RaceRoom Monza 22.11.: Im Zweikampf und in den letzten Runden blieb auch der Abstand zum Vorder- und Hintermann
            // stumm (18 Berichte, 3 gespielt) – genau dann will Rob ihn hören. Er ist kein Hintergrund, sondern sein eigener Kampf.
            bool gapDue = race && due.Any(p => prefs[p.Key] > 0 && p.Value <= now && FightGap(p.Key));
            bool racingQuiet = !busy && !recentRadio && !promised && battleQuiet && !gapDue;
            // Rob 23.11.: Ein Bericht, der schon 20 s wartet, beansprucht den nächsten freien Moment – Kommentar, Lob und
            // Einordnungen lassen ihm den Vortritt (siehe RadioArbiter). Nur, wenn ihn allein der volle Funk aufhält, nicht Box,
            // Gelb, Start oder Zweikampf-Ruhe.
            if (gs != null && !StartCalm && gs.SessionData.SessionPhase == SessionPhase.Green && !gs.PitData.InPitlane && !gs.FlagData.isLocalYellow &&
                ctx.TelemetryAgeSeconds >= 0 && ctx.TelemetryAgeSeconds <= 1)
                foreach (var claim in due.Where(d => prefs[d.Key] > 0 && now - d.Value >= TimeSpan.FromSeconds(RadioArbiter.RoutineClaimSeconds)).ToArray())
                    if ((!battleQuiet || Guaranteed(claim.Key, now) || FightGap(claim.Key)) && !TechnicalBudgetBlocks(claim.Key, now, prefs) &&
                        (memberEnabled == null || memberEnabled(MemberFor(claim.Key))))
                        Arbiter.Note(claim.Key, RadioArbiter.Tier(claim.Key, 1), claim.Value, now);
            if (gs == null || busy || StartCalm || recentRadio || racingQuiet || BusyForRoutine(gs) || ctx.TelemetryAgeSeconds < 0 || ctx.TelemetryAgeSeconds > 1 ||
                (gs.SessionData.SessionType != SessionType.Race && !SessionLapObserver.Supported(gs.SessionData.SessionType)) || gs.SessionData.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane ||
                now - lastReport < TimeSpan.FromSeconds(statusOverdue || promised ? 8 : RoutineSeparationSeconds))
            {
                if(due.Any(p=>prefs[p.Key]>0 && p.Value<=now) && now-lastDeferral>=TimeSpan.FromSeconds(15))
                {
                    string reason=busy?(busyReason??"channel_busy"):StartCalm?"race_start_phase":recentRadio?"recent_radio":racingQuiet?"close_battle_or_final_laps":gs!=null && gs.FlagData.isLocalYellow?"local_yellow":BusyForRoutine(gs)?"braking_or_qualifying_push":
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
            foreach (string topic in due.Where(kv => prefs[kv.Key] > 0 && kv.Value <= now && (!statusOverdue || kv.Key == "status") &&
                    (!battleQuiet || Guaranteed(kv.Key, now) || FightGap(kv.Key)))
                .OrderBy(kv => kv.Key == "status" && gs.SessionData.SessionType == SessionType.Race ? 0 : 1)
                .ThenByDescending(kv => Urgency(kv.Key, (now - kv.Value).TotalSeconds, prefs[kv.Key]))
                .ThenBy(kv => kv.Value).Select(kv => kv.Key).ToArray())
            {
                // Technik-Budget: das Thema bleibt fällig und kommt, sobald die gemeinsame Pause vorbei ist.
                if (TechnicalBudgetBlocks(topic, now, prefs)) continue;
                bool promisedTopic = Guaranteed(topic, now);
                overdueSince.TryGetValue(topic, out var since);
                overdueSince.Remove(topic);
                double delay = Delay(prefs, topic);
                // Rob (Monza 22.11.): „Das muss häufiger kommen, wenn ich im Zweikampf bin.“ Im Zweikampf kommt der nächste
                // Abstandsbericht nach der halben Zeit, frühestens nach 30 s.
                if (battleQuiet && FightGap(topic)) delay = BattleGapDelay(delay);
                // Bathurst 17.11.: Restzeit im Training alle 3–4 Minuten ist zu oft. Außerhalb des Rennens höchstens alle 6 Minuten,
                // in den letzten 10 Minuten wieder im eingestellten Takt.
                var sd = ctx.GameState?.SessionData;
                if (topic == "progress" && sd != null && sd.SessionType != SessionType.Race &&
                    !(sd.SessionHasFixedTime && sd.SessionTimeRemaining > 0 && sd.SessionTimeRemaining < 600)) delay = Math.Max(delay, PracticeProgressSeconds);
                due[topic] = now.AddSeconds(delay);
                Arbiter.Done(topic); // kommt gleich als fertiger Bericht wieder (mit seiner Wartezeit) oder entfällt
                TeamMember member=MemberFor(topic);
                if (memberEnabled!=null && !memberEnabled(member)) continue;
                var cursor = topic == "fuel" ? fuelVariants : topic == "ahead" ? aheadVariants : topic == "behind" ? behindVariants : null;
                string gapGroup = topic == "ahead" ? "gap-front-measured" : topic == "behind" ? "gap-rear-measured" : null;
                int gapVariant = gapGroup == null ? 0 : cursor.Select(gapGroup, RadioPhraseCatalog.Current.Count(gapGroup));
                string variantGroup = topic == "fuel" ? FuelGroup(ctx) : gapGroup;
                int index = topic == "fuel" ? cursor.Select(variantGroup, RadioPhraseCatalog.Current.Count(variantGroup)) : topic == "tyres" ? tyreReports++ : topic == "progress" ? progressReports++ : topic == "vehicle" ? vehicleReports++ : gapGroup == null ? NextTurn(topic) : gapVariant;
                int composeVariant = gapGroup == null ? index : gapVariant * 2 + (cursor.Heard & 1);
                string text = Compose(topic, ctx, composeVariant, out var template);
                if (string.IsNullOrEmpty(text)) { LiveDiagnostics.Decision(topic, "suppressed", "insufficient_evidence"); continue; }
                if (topic == "fuel")
                {
                    // Bathurst 17.11.: „So reicht es nicht bis ins Ziel“ dreimal – das Urteil nur, wenn es sich geändert hat.
                    string verdict = FuelEstimate.From(ctx)?.Verdict();
                    if (verdict != null && verdict == lastFuelVerdict && text.Contains(" " + verdict)) text = text.Replace(" " + verdict, "");
                    else if (verdict != null) lastFuelVerdict = verdict;
                }
                // Wetter wie ein Ingenieur (Rob 17.11., Bathurst: zweimal „Luft 21,2 Grad, Strecke 25,4 Grad“ in sechs Minuten):
                // nur bei spürbarer Änderung erneut, und dann mit der Veränderung statt nackter Zahlen.
                if (topic == "weather")
                {
                    var reading = WeatherReading.From(gs);
                    if (reading != null && lastWeather != null && !reading.ChangedFrom(lastWeather))
                    {
                        LiveDiagnostics.Decision(topic, "suppressed", "weather_unchanged");
                        continue;
                    }
                    if (reading != null && lastWeather != null) text = reading.ChangeText(lastWeather, weatherVariant++) ?? text;
                    outstandingWeather = reading;
                }
                string fuelMeasurement = topic == "fuel" ? FuelMeasurementKey(ctx) : null;
                if (fuelMeasurement != null && fuelMeasurement == lastFuelMeasurement &&
                    lastDeliveredUtc.TryGetValue(topic, out var lastFuel) && now - lastFuel < TimeSpan.FromMinutes(10) &&
                    FuelUnchangedAcrossLaps(ctx))
                {
                    LiveDiagnostics.Decision(topic, "suppressed", "unchanged_fuel_measurement");
                    continue;
                }
                var measurement = StintRadioMemory.Capture(topic, ctx);
                // Rob 17.11. (Bathurst): „Rafa Rivera: 2:03,178 …“ kam zweimal, obwohl sich keine Zeit geändert hatte. Die
                // Zeitenliste außerhalb des Rennens ändert sich nur mit neuen Bestzeiten – gleicher Satz heißt gleiche Lage.
                bool timesheet = (topic == "ahead" || topic == "behind") && gs.SessionData.SessionType != SessionType.Race;
                if (timesheet && SameTimesheetAgain(topic, text, now))
                {
                    LiveDiagnostics.Decision(topic, "suppressed", "unchanged_timesheet");
                    continue;
                }
                if (filterUnchanged && !timesheet && (measurement != null ? facts.Unchanged(topic, measurement, now) : UnchangedRoutine(topic, text, now)))
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
                    ReferenceTemplate=template, RaceStatus=raceStatus, StatusVariant=composeVariant,
                    Member=topic == "weather" || topic == "tyres" || topic == "pace" || topic == "vehicle" ? TeamMember.Mik : TeamMember.Ben,
                    AdditionalValidity=live=>(raceStatus ? !live.FlagData.isLocalYellow : !InvalidatesRoutine(live)) &&
                        (!overallSpoken || live.SessionData.OverallPosition == overall),
                    Position=topic == "status" ? gs.SessionData.ClassPosition : -1,
                    SessionId=gs.SessionData.SessionId, SessionType=gs.SessionData.SessionType, DetectedUtc=now, ExpiresUtc=now.AddSeconds(promisedTopic ? GuaranteedLifetimeSeconds : RoutineLifetimeSeconds),
                    Guaranteed=promisedTopic };
                outstandingSince = since == default(DateTime) ? now : since;
                Arbiter.Note(topic, RadioArbiter.Tier(topic, 1), outstandingSince, now);
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
            due[topic]=delivered.AddSeconds(Delay(prefs,topic));
            lastReport=delivered;
            if(outstanding!=null && outstanding.Topic==topic && outstanding.DeliveryStartedUtc==DateTime.MinValue) {
                outstanding.ExpiresUtc=delivered.AddTicks(-1);outstanding=null;
            }
        }
        private static string Number(double n) { return n.ToString("0.0", RadioLanguage.Culture); }
        private string lastFuelVerdict;
        /// <summary>Über diesem Abstand (s) ist ein Gegner im Rennen weder Zweikampf noch Ziel – keine Abstandsmeldung.</summary>
        internal const float MaxRelevantGap = 20f;
        /// <summary>Restzeit außerhalb des Rennens höchstens so oft (Sekunden).</summary>
        internal const double PracticeProgressSeconds = 360;

        private int NextTurn(string topic) { topicTurns.TryGetValue(topic, out int n); topicTurns[topic] = n + 1; return n; }

        /// <summary>Wählt Formulierung Nummer <paramref name="variant"/> (zyklisch) aus mehreren gleichwertigen.</summary>
        internal static string Of(int variant, params string[] options) => options[((variant % options.Length) + options.Length) % options.Length];

        private static string Capitalize(string s) { return s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s.Substring(1); }
        private static bool Good(double n, double min, double max) { return !double.IsNaN(n) && !double.IsInfinity(n) && n >= min && n <= max; }
        /// <summary>
        /// Routine-Technikcheck wie ein Ingenieur (Rob 17.11.: „zu viele technische Details“): Ist alles im grünen Bereich,
        /// genügt ein Satz ohne Zahlen. Nur was auffällt – Schaden ab 1 %, Wasser/Öl ab 90 % der Grenze des Fahrzeugs (darüber: „über der Grenze“,
        /// RaceRoom 20.11. sagte bei 115,9 zu 100 Grad noch „nah an der Grenze“),
        /// Bremsen über der Heiß-Schwelle – wird mit Wert genannt. Ohne beurteilbare Werte bleibt der bisherige Bericht.
        /// </summary>
        internal static string VehicleCheck(GameStateData gs, int variant)
        {
            var e = gs.EngineData; var t = gs.TyreData; var cls = gs.carClass;
            bool? clean = NumericDamageWatch.NothingNotable(gs.CarDamageData);
            bool water = Good(e.EngineWaterTemp, 1, 200), oil = Good(e.EngineOilTemp, 1, 250);
            float waterLimit = cls != null ? cls.maxSafeWaterTemp : -1, oilLimit = cls != null ? cls.maxSafeOilTemp : -1;
            if (clean == null || !water && !oil || water && !(waterLimit > 0) || oil && !(oilLimit > 0)) return null;
            var issues = new List<string>();
            if (clean == false) { string damage = IncidentWatch.DamageReport(gs.CarDamageData, true); if (!string.IsNullOrWhiteSpace(damage)) issues.Add(damage.TrimEnd('.')); }
            if (water && e.EngineWaterTemp >= .9f * waterLimit)
                issues.Add(e.EngineWaterTemp > waterLimit
                    ? RadioLanguage.T("Wasser " + Number(e.EngineWaterTemp) + " Grad, über der Grenze von " + Math.Round(waterLimit),
                        "water " + Number(e.EngineWaterTemp) + " degrees, above the " + Math.Round(waterLimit) + " limit",
                        "agua " + Number(e.EngineWaterTemp) + " grados, por encima del límite de " + Math.Round(waterLimit))
                    : RadioLanguage.T("Wasser " + Number(e.EngineWaterTemp) + " Grad, nah an der Grenze von " + Math.Round(waterLimit),
                        "water " + Number(e.EngineWaterTemp) + " degrees, close to the " + Math.Round(waterLimit) + " limit",
                        "agua " + Number(e.EngineWaterTemp) + " grados, cerca del límite de " + Math.Round(waterLimit)));
            if (oil && e.EngineOilTemp >= .9f * oilLimit)
                issues.Add(e.EngineOilTemp > oilLimit
                    ? RadioLanguage.T("Öl " + Number(e.EngineOilTemp) + " Grad, über der Grenze von " + Math.Round(oilLimit),
                        "oil " + Number(e.EngineOilTemp) + " degrees, above the " + Math.Round(oilLimit) + " limit",
                        "aceite " + Number(e.EngineOilTemp) + " grados, por encima del límite de " + Math.Round(oilLimit))
                    : RadioLanguage.T("Öl " + Number(e.EngineOilTemp) + " Grad, nah an der Grenze von " + Math.Round(oilLimit),
                        "oil " + Number(e.EngineOilTemp) + " degrees, close to the " + Math.Round(oilLimit) + " limit",
                        "aceite " + Number(e.EngineOilTemp) + " grados, cerca del límite de " + Math.Round(oilLimit)));
            float[] brakes = { t.LeftFrontBrakeTemp, t.RightFrontBrakeTemp, t.LeftRearBrakeTemp, t.RightRearBrakeTemp };
            float hot = cls != null ? cls.maxHotBrakeTemp : -1;
            bool brakesKnown = hot > 0 && brakes.All(b => Good(b, 1, 1500));
            if (brakesKnown && brakes.Max() > hot)
                issues.Add(RadioLanguage.T("Bremsen sehr heiß, bis " + Math.Round(brakes.Max()) + " Grad", "brakes very hot, up to " + Math.Round(brakes.Max()) + " degrees",
                    "frenos muy calientes, hasta " + Math.Round(brakes.Max()) + " grados"));
            if (issues.Count > 0) return Capitalize(string.Join(". ", issues.Select(Capitalize))) + ".";
            string what = brakesKnown ? RadioLanguage.T("Motor und Bremsen", "engine and brakes", "motor y frenos") : RadioLanguage.T("Motor", "engine", "motor");
            switch (((variant % 5) + 5) % 5)
            {
                case 3: return RadioLanguage.T("Alles stabil am Auto, keine Auffälligkeiten.", "All stable on the car, nothing unusual.", "Todo estable en el coche, nada raro.");
                case 4: return RadioLanguage.T("Kurzer Blick auf die Technik: nichts Auffälliges, alles im Rahmen.", "Quick look at the systems: nothing unusual, all within range.", "Vistazo técnico: nada fuera de lo normal, todo en rango.");
                case 1: return RadioLanguage.T("Technik-Check: alles im grünen Bereich.", "Tech check: everything in the green.", "Chequeo técnico: todo en verde.");
                case 2: return RadioLanguage.T("Vom Auto gibt's nichts zu melden: kein Schaden, Temperaturen passen.", "Nothing to report on the car: no damage, temperatures fine.", "Nada que reportar del coche: sin daños, temperaturas bien.");
                default: return RadioLanguage.T("Auto ist in Ordnung: kein nennenswerter Schaden, " + what + " im grünen Bereich.",
                    "Car is fine: no significant damage, " + what + " in the green.", "El coche está bien: sin daños relevantes, " + what + " en verde.");
            }
        }

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

        /// <summary>Einmalige Meldung, wenn die Sim Verbrauch oder Verschleiß nicht bewegt (z. B. serverseitig ausgeschaltet).</summary>
        internal static string FrozenNotice(SimChannel c, string litres)
        {
            if (c == SimChannel.Fuel)
                return RadioLanguage.T("Der Tank steht seit über einer Minute Fahrt bei " + litres + " Litern. Verbrauch ist hier offenbar aus, zum Sprit melde ich mich erst wieder, wenn er sich bewegt.",
                    "Fuel has been stuck at " + litres + " litres for over a minute of driving. Consumption seems to be off here, I'll only report fuel again once it moves.",
                    "El depósito lleva más de un minuto en " + litres + " litros. El consumo parece desactivado, no volveré a hablar de combustible hasta que se mueva.");
            return RadioLanguage.T("Die Reifen zeigen keinen Verschleiß. Der ist hier offenbar aus, dazu melde ich mich erst wieder, wenn sich etwas tut.",
                "The tyres show no wear. It seems to be off here, I'll only report tyres again once something changes.",
                "Los neumáticos no muestran desgaste. Parece desactivado, no volveré a hablar de ellos hasta que cambie algo.");
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
                    string lap = LocalAnswers.LapTime(op.CurrentBestLapTime), diff = Number(difference);
                    bool named = qualifyingName.Length > 0;
                    string who = named ? qualifyingName : (ahead ? RadioLanguage.T("Der Nächste vor dir", "The next car ahead", "El siguiente por delante") : RadioLanguage.T("Der Nächste hinter dir", "The next car behind", "El siguiente por detrás"));
                    return Of(variant / 2,
                        (!named ? RadioLanguage.T("In der Zeitenliste " + (ahead ? "vor" : "hinter") + " dir: ",
                            (ahead ? "Ahead of you" : "Behind you") + " on the timesheet: ",
                            (ahead ? "Por delante" : "Por detrás") + " en la tabla de tiempos: ") : qualifyingName + ": ") + lap +
                        RadioLanguage.T(". Die Bestzeiten liegen ", ". Best laps are ", ". Los mejores tiempos están a ") + diff +
                        RadioLanguage.T(" Sekunden auseinander.", " seconds apart.", " segundos."),
                        who + RadioLanguage.T(" steht bei " + lap + ", " + diff + " Sekunden " + (ahead ? "schneller" : "langsamer") + " als du.",
                            " is on " + lap + ", " + diff + " seconds " + (ahead ? "quicker" : "slower") + " than you.",
                            " marca " + lap + ", " + diff + " segundos más " + (ahead ? "rápido" : "lento") + " que tú."),
                        RadioLanguage.T("Zeitenliste: ", "Timesheet: ", "Tabla de tiempos: ") + who + RadioLanguage.T(" mit ", " with ", " con ") + lap +
                            RadioLanguage.T(", Abstand " + diff + " Sekunden.", ", gap " + diff + " seconds.", ", diferencia " + diff + " segundos."),
                        ahead ? RadioLanguage.T("Bis zum Nächsten vor dir in der Zeitenliste fehlen " + diff + " Sekunden" + (named ? ", " + qualifyingName : "") + " mit " + lap + ".",
                                "You're " + diff + " seconds off the next car up the timesheet" + (named ? ", " + qualifyingName : "") + " on " + lap + ".",
                                "Te faltan " + diff + " segundos para el siguiente en la tabla" + (named ? ", " + qualifyingName : "") + ", con " + lap + ".")
                              : RadioLanguage.T("Dein Polster nach hinten in der Zeitenliste: " + diff + " Sekunden" + (named ? " auf " + qualifyingName : "") + ", " + lap + ".",
                                "Your cushion behind on the timesheet: " + diff + " seconds" + (named ? " to " + qualifyingName : "") + ", " + lap + ".",
                                "Tu margen por detrás en la tabla: " + diff + " segundos" + (named ? " sobre " + qualifyingName : "") + ", " + lap + "."));
                }
                var measurement = RaceDataTools.ReadGap(ctx, op, ahead);
                float gap = measurement.Seconds;
                if (op == null || !op.IsActive || !measurement.IsRaceGap || !Good(gap, 0.05, MaxRelevantGap)) return null;
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
                if (parts.Count == 0) return null;
                string w = string.Join(", ", parts);
                return Of(variant, Capitalize(w + "."), RadioLanguage.T("Wetter-Update: ", "Weather update: ", "Parte del tiempo: ") + w + ".",
                    RadioLanguage.T("Zu den Bedingungen: ", "Conditions: ", "Condiciones: ") + w + ".", RadioLanguage.T("Kurz zum Wetter – ", "Quick weather note – ", "Apunte del tiempo: ") + w + ".",
                    RadioLanguage.T("Bedingungen aktuell: ", "Current conditions: ", "Condiciones ahora: ") + w + ".");
            }
            if (topic == "fuel")
            {
                if (!gs.FuelData.FuelUseActive || !Good(gs.FuelData.FuelLeft, .1, 1000)) return null;
                // AMS2 Spa 23.11.: Verbrauch serverseitig aus, der Tank stand bei 12,5 l. Einmal kurz sagen, danach nichts mehr zum Sprit.
                if (DataTruth.Current.State(SimChannel.Fuel) == ChannelState.Stale)
                    return DataTruth.Current.ClaimFrozenNotice(SimChannel.Fuel) ? FrozenNotice(SimChannel.Fuel, Number(gs.FuelData.FuelLeft)) : null;
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
                // LMU mit Energie-Limit: Energie gehört in dieselbe Meldung, inklusive was zuerst knapp wird.
                var energy = VirtualEnergyWatch.Current;
                string energyLine = energy.Level >= 0 ? VirtualEnergyWatch.Report(energy.Level, energy.PerLap, gs.FuelData.FuelLeft / perLap, variant) : null;
                return catalog.Format("fuel-range", variant, litres, laps) + (verdict == null ? "" : " " + verdict) + (energyLine == null ? "" : " " + energyLine);
            }
            if (topic == "tyres")
            {
                var t = gs.TyreData; if (!t.TyreWearActive) return null;
                // Datenwahrheit: eingefrorene (z. B. erst nach dem Stopp aktualisierte) oder nicht gelieferte Werte nicht als aktuell melden.
                float[] wear = { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear };
                if (!DataTruth.Current.MaySpeakAsCurrent(SimChannel.TyreWear))
                {
                    // Steht der Verschleiß praktisch bei null still, ist er ausgeschaltet (AMS2 Spa 23.11.): einmal sagen. Steht er bei
                    // einem echten Wert still (Sims, die erst beim Stopp aktualisieren), bleibt es beim Schweigen.
                    bool off = wear.All(w => !float.IsNaN(w) && w >= 0 && w < 1);
                    return off && DataTruth.Current.ClaimFrozenNotice(SimChannel.TyreWear) ? FrozenNotice(SimChannel.TyreWear, null) : null;
                }
                if (!wear.All(w => Good(w, 0, 100))) return null;
                // Rob 16.11.: abwechselnd technisch und menschlich, mit fundierter Hochrechnung (nur kalibrierte Simulationen).
                // Ohne messbaren Verschleiß (manche Sims melden dauerhaft 0) gibt es nichts zu berichten.
                float[] rate = null, cautious = null;
                if (ctx.Memory != null && ctx.Memory.WearRates(5, out var median, out var careful, out int samples) && samples >= 2) { rate = median; cautious = samples >= 3 ? careful : null; }
                return TyreWearAdvisor.Report(wear, rate ?? ctx.Memory?.AverageWearPerLap(3), cautious, TyreWearAdvisor.Calibrated(Game.gameType),
                    AiPersonas.CleanForRadio(ctx.DriverName ?? "", 30, false), variant);
            }
            if (topic == "pace")
            {
                var laps = ctx.Memory == null ? new LapRecord[0] : ctx.Memory.GetLaps(3).ToArray();
                // Bathurst 17.11.: „im Mittel 2:34,483 … innerhalb von 83,4 Sekunden“ – FCY- und Unfallrunden gehören nicht in
                // einen Tempo-Schnitt. Nur Runden, die zusammenpassen (höchstens 4 s Spanne) – eine FCY- oder Unfallrunde fällt so heraus.
                if (laps.Length < 3 || !laps.All(l => l.Valid && !l.InPitLap && Good(l.LapTime, 20, 1200)) ||
                    laps[1].LapNumber != laps[0].LapNumber + 1 || laps[2].LapNumber != laps[1].LapNumber + 1 ||
                    laps.Max(l => l.LapTime) - laps.Min(l => l.LapTime) > 4) return null;
                string avg = LocalAnswers.LapTime(laps.Average(l => l.LapTime)), spread = Number(laps.Max(l => l.LapTime) - laps.Min(l => l.LapTime));
                return Of(variant,
                    RadioLanguage.T("Die letzten drei sauberen Runden im Mittel ", "Last three clean laps average ", "Media de las últimas tres vueltas limpias: ") + avg +
                    RadioLanguage.T(". Die Runden liegen innerhalb von ", ". All within ", ". Todas dentro de ") + spread + RadioLanguage.T(" Sekunden.", " seconds.", " segundos."),
                    RadioLanguage.T("Dein Schnitt über drei saubere Runden: " + avg + ", Spanne " + spread + " Sekunden.", "Your average over three clean laps: " + avg + ", spread " + spread + " seconds.", "Tu media en tres vueltas limpias: " + avg + ", margen de " + spread + " segundos."),
                    RadioLanguage.T("Konstanz-Check: " + spread + " Sekunden zwischen schnellster und langsamster der letzten drei Runden, Schnitt " + avg + ".", "Consistency check: " + spread + " seconds between your fastest and slowest of the last three, average " + avg + ".", "Control de constancia: " + spread + " segundos entre la más rápida y la más lenta de las tres últimas, media " + avg + "."),
                    RadioLanguage.T("Rundenzeiten zuletzt im Mittel " + avg + ", alle drei innerhalb von " + spread + " Sekunden.", "Recent laps average " + avg + ", all three within " + spread + " seconds.", "Últimas vueltas de media " + avg + ", las tres dentro de " + spread + " segundos."),
                    RadioLanguage.T("Tempo-Check: " + avg + " im Schnitt der letzten drei Runden, Spanne " + spread + " Sekunden.", "Pace check: " + avg + " average over the last three, spread " + spread + " seconds.", "Ritmo: " + avg + " de media en las tres últimas, margen de " + spread + " segundos."));
            }
            if (topic == "status") {
                if (gs.SessionData.SessionType == SessionType.Race)
                {
                    var session = gs.SessionData;
                    if (session.ClassPosition <= 0) return null;
                    string rank = RaceRadioLanguage.Rank(session, session.ClassPosition);
                    if (RaceRadioLanguage.SeparateClass(session) && session.OverallPosition > 0 && session.OverallPosition != session.ClassPosition)
                        rank += RadioLanguage.T(", gesamt Platz " + session.OverallPosition, ", P" + session.OverallPosition + " overall", ", P" + session.OverallPosition + " en la general");
                    int start = RaceStartLatch.Position(session);
                    int change = start > 0 ? start - session.ClassPosition : 0;
                    if (change == 0) return rank + ". " + Compose("progress", ctx);
                    int count = Math.Abs(change);
                    bool gained = change > 0;
                    return Of(variant,
                        rank + RadioLanguage.T(". Seit dem Start " + count + (count == 1 ? " Platz" : " Plätze") + (gained ? " gutgemacht." : " verloren."),
                            ". " + count + (count == 1 ? " place " : " places ") + (gained ? "gained" : "lost") + " since the start.",
                            ". " + count + (count == 1 ? " posición " : " posiciones ") + (gained ? (count == 1 ? "ganada" : "ganadas") : (count == 1 ? "perdida" : "perdidas")) + " desde la salida."),
                        RadioLanguage.T("Stand: " + rank + ", " + count + (count == 1 ? " Platz " : " Plätze ") + (gained ? "besser als am Start." : "hinter deinem Startplatz."),
                            "Status: " + rank + ", " + count + (count == 1 ? " place " : " places ") + (gained ? "up on the start." : "down on the start."),
                            "Situación: " + rank + ", " + count + (count == 1 ? " puesto " : " puestos ") + (gained ? "mejor que en la salida." : "por detrás de tu posición de salida.")),
                        RadioLanguage.T("Zwischenstand " + rank + ". Gegenüber dem Start " + (gained ? "plus " : "minus ") + count + ".",
                            "Running order: " + rank + ". Versus the start " + (gained ? "plus " : "minus ") + count + ".",
                            "Clasificación: " + rank + ". Respecto a la salida, " + (gained ? "más " : "menos ") + count + "."),
                        rank + RadioLanguage.T(gained ? " – seit dem Start " + count + (count == 1 ? " Platz" : " Plätze") + " nach vorn gefahren." : " – seit dem Start " + count + (count == 1 ? " Platz" : " Plätze") + " abgegeben.",
                            gained ? " – you've moved up " + count + " since the start." : " – " + count + " given away since the start.",
                            gained ? ": has remontado " + count + " desde la salida." : ": has cedido " + count + " desde la salida."));
                }
                var s=gs.SessionData;
                if (!Good(s.PlayerLapTimeSessionBest,10,3600)) return null;
                string best = LocalAnswers.LapTime(s.PlayerLapTimeSessionBest); bool pos = s.ClassPosition > 0; string p = s.ClassPosition.ToString(RadioLanguage.Culture);
                return Of(variant,
                    RadioLanguage.T("Deine bisherige Bestzeit: ", "Your best time so far: ", "Tu mejor tiempo hasta ahora: ") + best + "." +
                        (pos ? RadioLanguage.T(" Aktuell Platz "+p+" in der Zeitenliste.", " Currently P"+p+" on the timesheet.", " Ahora mismo P"+p+" en la tabla de tiempos.") : ""),
                    RadioLanguage.T("Bestzeit bisher " + best + (pos ? ", damit Platz " + p : "") + ".", "Best so far " + best + (pos ? ", that's P" + p : "") + ".", "Mejor tiempo hasta ahora " + best + (pos ? ", eso es P" + p : "") + "."),
                    pos ? RadioLanguage.T("Zeitenliste: Platz " + p + " mit " + best + ".", "Timesheet: P" + p + " with " + best + ".", "Tabla de tiempos: P" + p + " con " + best + ".")
                        : RadioLanguage.T("Deine Marke: " + best + ".", "Your benchmark: " + best + ".", "Tu marca: " + best + "."),
                    RadioLanguage.T("Deine Marke steht bei " + best + (pos ? " – das ist Platz " + p : "") + ".", "Your mark stands at " + best + (pos ? " – that's P" + p : "") + ".", "Tu marca está en " + best + (pos ? ": eso es P" + p : "") + "."));
            }
            if (topic == "vehicle") return VehicleCheck(gs, variant) ?? VehicleReport(gs, brief: true);
            if (topic == "progress") {
                var s=gs.SessionData;
                if(s.SessionHasFixedTime && Good(s.SessionTimeRemaining,60,200000)) {
                    // LMU 18.11.: „noch 29 Minuten“ bei 28:10 auf der Anzeige. Abrunden wie die Uhr im Spiel.
                    double minutes=Math.Floor(s.SessionTimeRemaining/60);
                    string m=minutes+(minutes==1?" Minute":" Minuten"), me=minutes+(minutes==1?" minute":" minutes"), ms=minutes+(minutes==1?" minuto":" minutos");
                    switch(((variant%5)+5)%5) {
                        case 1: return RadioLanguage.T("Uhr läuft: noch "+m+".", "Clock check: "+me+" to go.", "Reloj: quedan "+ms+".");
                        case 2: return RadioLanguage.T("Noch "+m+" in dieser Sitzung.", me+" left in this session.", "Quedan "+ms+" en esta sesión.");
                        case 3: return RadioLanguage.T("Restzeit: "+m+".", "Time remaining: "+me+".", "Tiempo restante: "+ms+".");
                        case 4: return RadioLanguage.T(m+" bleiben dir noch.", me+" still to use.", "Te quedan "+ms+".");
                    }
                    return RadioLanguage.T("Noch etwa "+minutes+(minutes==1?" Minute":" Minuten")+" auf der Uhr.",
                        "About "+minutes+(minutes==1?" minute":" minutes")+" left on the clock.",
                        minutes==1?"Queda más o menos 1 minuto en el reloj.":"Quedan unos "+minutes+" minutos en el reloj.");
                }
                if(!s.SessionHasFixedTime && s.SessionNumberOfLaps>0 && s.SessionLapsRemaining>0 && s.SessionLapsRemaining<10000)
                {
                    int n=s.SessionLapsRemaining; bool one=n==1;
                    return Of(variant,
                        RadioLanguage.T("Noch "+n+(one?" Runde.":" Runden."), n+(one?" lap to go.":" laps to go."), one?"Queda 1 vuelta.":"Quedan "+n+" vueltas."),
                        RadioLanguage.T(n+(one?" Runde":" Runden")+" bis zur Flagge.", n+(one?" lap":" laps")+" to the flag.", n+(one?" vuelta":" vueltas")+" hasta la bandera."),
                        RadioLanguage.T("Rundenstand: noch "+n+".", "Lap count: "+n+" remaining.", "Vueltas: quedan "+n+"."),
                        RadioLanguage.T("Noch "+n+(one?" Runde":" Runden")+" zu fahren.", "Still "+n+(one?" lap":" laps")+" to run.", "Aún "+(one?"queda 1 vuelta":"quedan "+n+" vueltas")+" por rodar."));
                }
                return null;
            }
            if(topic == "stint") {
                if(ctx.Memory==null)return null;
                float fuel=ctx.Memory.AverageFuelPerLap();var wear=ctx.Memory.AverageWearPerLap();var parts=new List<string>();
                if(Good(fuel,.1,100))parts.Add(RadioLanguage.T("Gemessener Verbrauch ", "Measured consumption ", "Consumo medido ")+Number(fuel)+RadioLanguage.T(" Liter pro Runde", " litres per lap", " litros por vuelta"));
                if(wear!=null && wear.Length==4 && wear.All(w=>Good(w,0,100)) && wear.Max()>=0.1)parts.Add(RadioLanguage.T("Der Reifen mit dem höchsten Verschleiß verliert ", "Most worn tyre loses ", "El neumático más gastado pierde ") + Number(wear.Max()) + RadioLanguage.T(" Prozentpunkte pro Runde", " percentage points per lap", " puntos porcentuales por vuelta"));
                if(parts.Count==0)return null;
                string joined=string.Join(". ",parts)+".";
                string lower=char.ToLower(joined[0], RadioLanguage.Culture)+joined.Substring(1);
                return Of(variant, joined, RadioLanguage.T("Stint-Zwischenstand: ","Stint update: ","Resumen del stint: ")+lower,
                    RadioLanguage.T("Aus dem Stint bisher: ","From the stint so far: ","Del stint hasta ahora: ")+lower,
                    RadioLanguage.T("Zahlen zum Stint – ","Stint numbers – ","Datos del stint: ")+lower);
            }
            return null;
        }
    }

    /// <summary>Wetterstand für die Routine: Luft, Strecke, Regen – und ob sich seit der letzten Meldung etwas getan hat.</summary>
    internal sealed class WeatherReading
    {
        internal float Air = float.NaN, Track = float.NaN; internal bool Rain;

        internal static WeatherReading From(GameStateData gs)
        {
            var c = gs?.Conditions?.LatestConditions; if (c == null) return null;
            var r = new WeatherReading();
            if (c.AmbientTemperature >= -30 && c.AmbientTemperature <= 60) r.Air = c.AmbientTemperature;
            if (c.TrackTemperature >= -30 && c.TrackTemperature <= 85) r.Track = c.TrackTemperature;
            r.Rain = c.RainDensity >= .01 && c.RainDensity <= 1;
            return float.IsNaN(r.Air) && float.IsNaN(r.Track) && !r.Rain ? null : r;
        }

        /// <summary>Spürbar: Strecke ±1,5 °C, Luft ±1 °C oder Regen beginnt/endet.</summary>
        internal bool ChangedFrom(WeatherReading old) =>
            old == null || Rain != old.Rain ||
            !float.IsNaN(Track) && !float.IsNaN(old.Track) && Math.Abs(Track - old.Track) >= 1.5f ||
            !float.IsNaN(Air) && !float.IsNaN(old.Air) && Math.Abs(Air - old.Air) >= 1f ||
            float.IsNaN(Track) != float.IsNaN(old.Track);

        private static string N(float v) => RadioLanguage.Num(v, "0.0");

        /// <summary>Meldung mit Richtung der Änderung; null wenn nichts Sinnvolles zu sagen ist.</summary>
        internal string ChangeText(WeatherReading old, int variant)
        {
            if (Rain != old.Rain)
                return Rain ? AiRadioSchedule.Of(variant, RadioLanguage.T("Es wird Regen gemeldet. Vorsicht, die Strecke verliert Grip.", "Rain is being reported. Careful, the track is losing grip.", "Se informa de lluvia. Cuidado, la pista pierde agarre."),
                        RadioLanguage.T("Regen kommt. Bremspunkte etwas früher, der Grip lässt nach.", "Rain's arriving. Brake a bit earlier, grip is dropping.", "Llega la lluvia. Frena algo antes, el agarre baja."),
                        RadioLanguage.T("Achtung, Regen gemeldet. Weiße Linien und Randsteine meiden.", "Heads up, rain reported. Stay off white lines and kerbs.", "Atención, lluvia. Evita líneas blancas y pianos."),
                        RadioLanguage.T("Es fängt an zu regnen. Ruhig bleiben, sanft mit den Pedalen.", "It's starting to rain. Stay calm, smooth on the pedals.", "Empieza a llover. Tranquilo, suave con los pedales."))
                    : AiRadioSchedule.Of(variant, RadioLanguage.T("Kein Regen mehr gemeldet. Die Strecke trocknet ab.", "No more rain reported. The track is drying.", "Ya no se informa de lluvia. La pista se está secando."),
                        RadioLanguage.T("Der Regen hat aufgehört. Die Ideallinie wird zuerst trocken.", "Rain has stopped. The racing line will dry first.", "Ha parado de llover. La trazada se secará primero."),
                        RadioLanguage.T("Kein Regen mehr. Grip kommt Runde für Runde zurück.", "No more rain. Grip will come back lap by lap.", "Ya no llueve. El agarre volverá vuelta a vuelta."),
                        RadioLanguage.T("Trocken von oben. Abseits der Linie bleibt es noch eine Weile rutschig.", "Dry from above now. Off-line stays slippery for a while.", "Ya no cae agua. Fuera de la trazada seguirá resbaladizo un rato."));
            if (float.IsNaN(Track) || float.IsNaN(old.Track)) return null;
            float d = Track - old.Track;
            if (Math.Abs(d) < 1.5f) return null;
            string delta = N(Math.Abs(d));
            bool warmer = d > 0;
            switch (((variant % 4) + 4) % 4)
            {
                case 3: return RadioLanguage.T("Asphalt jetzt " + N(Track) + " Grad, " + (warmer ? "plus " : "minus ") + delta + ". " + (warmer ? "Auf den Reifendruck achten." : "Die Reifen kühlen schneller ab."),
                    "Tarmac now " + N(Track) + " degrees, " + (warmer ? "up " : "down ") + delta + ". " + (warmer ? "Keep an eye on tyre pressures." : "Tyres will cool faster."),
                    "Asfalto ahora a " + N(Track) + " grados, " + (warmer ? "sube " : "baja ") + delta + ". " + (warmer ? "Ojo a las presiones." : "Los neumáticos se enfriarán antes."));
                case 0: return RadioLanguage.T("Die Strecke ist " + delta + " Grad " + (warmer ? "wärmer" : "kühler") + " geworden, jetzt " + N(Track) + " Grad.",
                    "The track has got " + delta + " degrees " + (warmer ? "warmer" : "cooler") + ", now " + N(Track) + " degrees.",
                    "La pista está " + delta + " grados más " + (warmer ? "caliente" : "fría") + ", ahora " + N(Track) + " grados.");
                case 1: return RadioLanguage.T("Streckentemperatur " + (warmer ? "steigt" : "fällt") + ": " + N(Track) + " Grad, " + delta + " " + (warmer ? "mehr" : "weniger") + " als vorhin.",
                    "Track temperature " + (warmer ? "rising" : "dropping") + ": " + N(Track) + " degrees, " + delta + " " + (warmer ? "more" : "less") + " than before.",
                    "Temperatura de pista " + (warmer ? "subiendo" : "bajando") + ": " + N(Track) + " grados, " + delta + " " + (warmer ? "más" : "menos") + " que antes.");
                default: return RadioLanguage.T("Info zur Strecke: jetzt " + N(Track) + " Grad" + (float.IsNaN(Air) ? "" : ", Luft " + N(Air)) + ". " + (warmer ? "Die Reifen werden schneller warm." : "Die Reifen brauchen etwas länger auf Temperatur."),
                    "Track info: now " + N(Track) + " degrees" + (float.IsNaN(Air) ? "" : ", air " + N(Air)) + ". " + (warmer ? "Tyres will warm up quicker." : "Tyres need a little longer to come in."),
                    "Info de pista: ahora " + N(Track) + " grados" + (float.IsNaN(Air) ? "" : ", aire " + N(Air)) + ". " + (warmer ? "Los neumáticos se calentarán antes." : "Los neumáticos tardan algo más en coger temperatura."));
            }
        }
    }
}
