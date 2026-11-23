using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam.Strategy
{
    /// <summary>
    /// Strategie im Funk (Testkanal B): An jeder Ziellinie im Rennen rechnet die Crew das Strategiebild neu. Gesprochen wird nur,
    /// wenn sich die Lage ändert, nicht jede Runde:
    ///  - Ben: der Plan steht oder kippt (durchfahren, Energie sparen, Reifen schonen, ein Stopp mit Fenster, zwei Stopps,
    ///    Pflichtstopp), das Boxenfenster öffnet sich, und die letzte Runde im Fenster, wenn Sprit oder Energie hart begrenzen.
    ///  - Mik: das eigene Tempo baut über mehrere saubere Runden messbar ab.
    /// Ein Plan muss zwei Linien in Folge gleich bleiben, bevor Ben ihn sagt, damit eine Verkehrsrunde ihn nicht kippen lässt.
    /// Der reine Spritmangel bleibt bei der festen Sprit-Warnung; hier kommt er nur im Plan mit Fenster vor.
    /// </summary>
    internal sealed class StrategyWatch
    {
        private static readonly object latestGate = new object();
        private static StrategyPicture latest;
        private static int latestSession = int.MinValue;
        private static DateTime latestUtc = DateTime.MinValue;

        private int session = int.MinValue, lastCallLap = -100, lastFadeLap = -100, stopsAtOpen = -1, lastWindowLastSpoken = -1, stableCount;
        private string spokenPlan, candidatePlan;
        private ObservedRaceCall call;
        private Action commit;

        internal DateTime LastPaceFadeUtc { get; private set; } = DateTime.MinValue;

        internal void Reset()
        {
            session = int.MinValue; lastCallLap = lastFadeLap = -100; stopsAtOpen = lastWindowLastSpoken = -1; stableCount = 0;
            spokenPlan = candidatePlan = null; call = null; commit = null; LastPaceFadeUtc = DateTime.MinValue;
        }

        internal static void Publish(StrategyPicture p, int sessionId, DateTime now)
        {
            lock (latestGate) { latest = p; latestSession = sessionId; latestUtc = now; }
        }

        internal static void ClearLatest() { lock (latestGate) { latest = null; latestSession = int.MinValue; } }

        /// <summary>Das zuletzt gerechnete Strategiebild dieser Session als Werkzeugantwort, höchstens 5 Minuten alt.</summary>
        internal static JObject Latest(int sessionId, DateTime now)
        {
            lock (latestGate)
            {
                if (latest == null || latestSession != sessionId || now < latestUtc || now - latestUtc > TimeSpan.FromMinutes(5)) return null;
                var o = latest.ToJson();
                o["gerechnet_vor_s"] = Math.Round((now - latestUtc).TotalSeconds, 0);
                return o;
            }
        }

        // ---------- Eingaben aus der laufenden Session ----------

        internal static StrategyInput FromContext(RaceDataContext ctx)
        {
            var gs = ctx?.GameState;
            if (gs == null) return null;
            var s = gs.SessionData;
            var i = new StrategyInput { Lap = s.CompletedLaps, RefuelAllowed = gs.PitData.IsRefuellingAllowed, StopsMade = gs.PitData.NumPitStops,
                MandatoryStopPending = gs.PitData.HasMandatoryPitStop && !gs.PitData.MandatoryPitStopCompleted };
            var estimate = FuelEstimate.From(ctx);
            if (estimate != null)
            {
                i.LapsToGo = estimate.LapsRemaining;
                i.LapsToGoEstimated = estimate.EstimatedFromTime;
                i.FuelPerLap = estimate.PerLap;
                i.FuelProvisional = estimate.ProvisionalConsumption;
            }
            else
            {
                int left = LapWatch.LapsLeft(s);
                if (left > 0) { i.LapsToGo = left; i.LapsToGoEstimated = s.SessionHasFixedTime; }
            }
            if (gs.FuelData.FuelUseActive)
            {
                i.FuelLeft = gs.FuelData.FuelLeft;
                i.FuelCapacity = gs.FuelData.FuelCapacity;
            }
            var laps = ctx.Memory?.GetLaps() ?? new List<LapRecord>();
            var fuel = laps.Where(l => !l.InPitLap && l.Valid && l.FuelUsed > 0 && l.FuelUsed < 1000).Reverse().Take(5).Select(l => (double)l.FuelUsed).ToList();
            if (fuel.Count >= 2) i.FuelPerLapMeasured = StrategyPicture.Median(fuel);

            var energy = VirtualEnergyWatch.Current;
            if (energy != null && energy.Level >= 0 && energy.PerLap > 0) { i.EnergyLeft = energy.Level; i.EnergyPerLap = energy.PerLap; }

            var t = gs.TyreData;
            if (t.TyreWearActive && DataTruth.Current.MaySpeakAsCurrent(SimChannel.TyreWear) && ctx.Memory != null)
            {
                i.Wear = new[] { t.FrontLeftPercentWear, t.FrontRightPercentWear, t.RearLeftPercentWear, t.RearRightPercentWear };
                i.WearPerLap = ctx.Memory.WearRates(5, out var median, out _, out int samples) && samples >= 2 ? median : ctx.Memory.AverageWearPerLap(3);
                i.WearCalibrated = TyreWearAdvisor.Calibrated(Game.gameType);
            }
            i.StintLaps = StintLaps(laps);
            if (ctx.PitLossSeconds > 0) i.PitLoss = ctx.PitLossSeconds;
            return i;
        }

        /// <summary>Saubere Runden seit dem letzten Boxenbesuch, ohne die erste Rennrunde.</summary>
        internal static List<KeyValuePair<int, double>> StintLaps(IList<LapRecord> laps)
        {
            var result = new List<KeyValuePair<int, double>>();
            if (laps == null) return result;
            for (int k = laps.Count - 1; k >= 0 && result.Count < 12; k--)
            {
                var l = laps[k];
                if (l.InPitLap) break;
                if (!l.Valid || l.LapNumber <= 1 || l.LapTime < 10 || l.LapTime > 3600) continue;
                result.Add(new KeyValuePair<int, double>(l.LapNumber, l.LapTime));
            }
            result.Reverse();
            return result;
        }

        // ---------- Beobachten und Funk ----------

        internal void Observe(RaceDataContext ctx, bool lapCompleted, DateTime now)
        {
            var gs = ctx?.GameState;
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (!lapCompleted || s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Green) return;
            StrategyPicture p;
            try { p = StrategyPicture.Compute(FromContext(ctx)); }
            catch (Exception e) { LiveDiagnostics.Decision("strategy-picture", "error", e.GetType().Name); return; }
            Publish(p, s.SessionId, now);
            if (LiveDiagnostics.Enabled)
                LiveDiagnostics.Emit("strategy.picture", LiveDiagnostics.Data("lap", p.Lap, "plan", p.Plan, "reason", p.PlanReason, "limit", p.Limit,
                    "range_laps", Round(p.RangeLaps), "laps_to_go", Round(p.LapsToGo), "fuel_checked", p.FuelChecked,
                    "window_first", p.WindowFirstLap, "window_last", p.WindowLastLap, "pace_trend", p.PaceTrend, "pace_slope", Round(p.PaceSlope)));
            if (gs.PitData.InPitlane || s.CompletedLaps < 2) return;
            if (!(p.LapsToGo > 1.5)) return; // letzte Runde: nichts mehr zu planen

            if (p.Plan == candidatePlan) stableCount++; else { candidatePlan = p.Plan; stableCount = 1; }
            int stops = gs.PitData.NumPitStops;
            if (stopsAtOpen >= 0 && stops != stopsAtOpen) { stopsAtOpen = -1; lastWindowLastSpoken = -1; }

            string topic = null, text = null, evidence = null;
            TeamMember member = TeamMember.Ben;
            int priority = 4;
            Action onSpoken = null;
            bool stopPlan = p.Plan == "ein_stopp" || p.Plan == "zwei_stopps" || p.Plan == "pflichtstopp";

            if (stopPlan && p.HardLimit && p.WindowLastLap == p.Lap + 1 && lastWindowLastSpoken != p.WindowLastLap)
            {
                topic = "strategy-window-last"; priority = 6;
                text = StrategyRadio.WindowLast(p);
                evidence = "measured_range_ends_this_lap;limit=" + p.Limit;
                int last = p.WindowLastLap;
                onSpoken = () => { lastWindowLastSpoken = last; };
            }
            else if ((p.Plan == "ein_stopp" || p.Plan == "pflichtstopp") && p.WindowKnown && p.WindowFirstLap <= p.Lap + 1 && p.WindowLastLap > p.Lap + 1 &&
                stopsAtOpen < 0 && s.CompletedLaps - lastCallLap >= 1)
            {
                topic = "strategy-window-open";
                text = StrategyRadio.WindowOpen(p);
                evidence = "one_stop_reaches_finish_from_now;window=" + p.WindowFirstLap + "-" + p.WindowLastLap;
                onSpoken = () => { stopsAtOpen = stops; spokenPlan = p.Plan; };
            }
            else if (stableCount >= 2 && p.Plan != spokenPlan && WorthSaying(p, spokenPlan) && s.CompletedLaps - lastCallLap >= 2)
            {
                topic = "strategy-plan";
                text = StrategyRadio.Plan(p, spokenPlan);
                evidence = "plan_stable_two_lines;plan=" + p.Plan + ";limit=" + p.Limit;
                string plan = p.Plan;
                onSpoken = () => { spokenPlan = plan; };
            }
            else if (p.PaceTrend == "baut_ab" && p.PaceLaps >= 5 && s.CompletedLaps - lastFadeLap >= 6 && s.CompletedLaps - lastCallLap >= 1)
            {
                topic = "strategy-pace-fade"; member = TeamMember.Mik; priority = 3;
                text = StrategyRadio.PaceFade(p);
                evidence = "theil_sen_slope_clean_stint_laps;slope=" + Round(p.PaceSlope) + ";laps=" + p.PaceLaps;
                onSpoken = () => { lastFadeLap = s.CompletedLaps; LastPaceFadeUtc = DateTime.UtcNow; };
            }
            if (topic == null || text == null) return;
            int lap = s.CompletedLaps;
            var spoken = onSpoken;
            commit = () => { lastCallLap = lap; spoken?.Invoke(); };
            call = new ObservedRaceCall
            {
                Topic = topic, Text = text, Evidence = evidence, Member = member, Priority = priority, Position = -1,
                SessionId = s.SessionId, SessionType = s.SessionType, CompletedLaps = lap, DetectedUtc = now, ExpiresUtc = now.AddSeconds(40),
                AdditionalValidity = live => !live.PitData.InPitlane
            };
            LiveDiagnostics.Decision(topic, "detected", evidence, text, call.EventId);
        }

        /// <summary>Ein neuer Plan ist nur dann eine Meldung wert, wenn er etwas zu tun oder eine Entwarnung bedeutet.</summary>
        internal static bool WorthSaying(StrategyPicture p, string previous)
        {
            switch (p.Plan)
            {
                case "ein_stopp": return p.WindowKnown || p.WindowLastLap > 0;
                case "zwei_stopps": return p.WindowLastLap > 0;
                case "pflichtstopp": return p.WindowLastLap > 0;
                case "sparen": return p.PlanReason == "energie" && !double.IsNaN(p.EnergyTargetPerLap); // reiner Spritmangel: feste Sprit-Warnung
                case "reifen_schonen": return !double.IsNaN(p.TyreLaps);
                case "durchfahren":
                    // Entwarnung nach einem Stopp-Plan, sonst nur bei knapper Reserve.
                    return previous != null && previous != "durchfahren" || !double.IsNaN(p.Margin) && p.Margin < 3;
                default: return false;
            }
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool benEnabled, bool mikEnabled)
        {
            if (call != null && call.DeliveryStartedUtc != DateTime.MinValue)
            {
                commit?.Invoke();
                commit = null;
                call = null;
                return null;
            }
            if (call != null && !call.IsValid(gs, now)) { call = null; commit = null; }
            if (busy || call == null) return null;
            if (call.Member == TeamMember.Mik ? !mikEnabled : !benEnabled) return null;
            return call.TryReserve(now) ? call : null;
        }

        private static double Round(double v) => double.IsNaN(v) || double.IsInfinity(v) ? double.NaN : Math.Round(v, 3);
    }

    /// <summary>Die Sätze zum Strategiebild, je Anlass mindestens vier Formulierungen in Deutsch, Englisch und Spanisch.</summary>
    internal static class StrategyRadio
    {
        private static string Laps(double v) => RadioLanguage.Num(Math.Max(0, v), v < 10 ? "0.0" : "0");

        private static string LimitName(string limit) =>
            limit == "energie" ? RadioLanguage.T("die Energie", "the energy", "la energía")
            : limit == "reifen" ? RadioLanguage.T("die Reifen", "the tyres", "los neumáticos")
            : RadioLanguage.T("der Sprit", "the fuel", "el combustible");

        /// <summary>„begrenzt durch …“: im Deutschen mit Akkusativ.</summary>
        private static string LimitBy(string limit) =>
            limit == "energie" ? RadioLanguage.T("die Energie", "the energy", "la energía")
            : limit == "reifen" ? RadioLanguage.T("die Reifen", "the tyres", "los neumáticos")
            : RadioLanguage.T("den Sprit", "the fuel", "el combustible");

        private static string LimitNameCap(string limit)
        {
            string s = LimitName(limit);
            return char.ToUpper(s[0]) + s.Substring(1);
        }

        private static readonly string[] De = { "vorne links", "vorne rechts", "hinten links", "hinten rechts" };
        private static readonly string[] En = { "front left", "front right", "rear left", "rear right" };
        private static readonly string[] Es = { "delantero izquierdo", "delantero derecho", "trasero izquierdo", "trasero derecho" };
        private static string Corner(int i) => i < 0 || i > 3 ? RadioLanguage.T("ein Reifen", "one tyre", "un neumático") : RadioLanguage.Pick(De, En, Es)[i];

        internal static string Plan(StrategyPicture p, string previous)
        {
            switch (p.Plan)
            {
                case "durchfahren":
                {
                    string m = Laps(p.Margin);
                    if (previous != null && previous != "durchfahren")
                        return RadioVariants.Pick("strategy-plan-clear",
                            RadioLanguage.T("Entwarnung bei der Strategie: Wir kommen jetzt ohne Stopp ins Ziel, mit etwa " + m + " Runden Luft.",
                                "Strategy all clear: we now make the finish without a stop, about " + m + " laps in hand.",
                                "Tranquilo con la estrategia: ahora llegamos a meta sin parar, con unas " + m + " vueltas de margen."),
                            RadioLanguage.T("Neue Lage: Kein Stopp mehr nötig. Reserve rund " + m + " Runden.",
                                "New picture: no stop needed any more. Margin around " + m + " laps.",
                                "Nueva situación: ya no hace falta parar. Margen de unas " + m + " vueltas."),
                            RadioLanguage.T("Gute Nachricht, wir fahren durch. Es bleiben etwa " + m + " Runden Reserve.",
                                "Good news, we run to the end. About " + m + " laps of margin left.",
                                "Buena noticia, vamos hasta el final. Quedan unas " + m + " vueltas de margen."),
                            RadioLanguage.T("Die Rechnung hat sich gedreht: Durchfahren geht, mit gut " + m + " Runden Luft.",
                                "The numbers have turned: we can run to the end, with a good " + m + " laps spare.",
                                "Las cuentas han cambiado: podemos llegar sin parar, con unas " + m + " vueltas de sobra."));
                    return RadioVariants.Pick("strategy-plan-through",
                        RadioLanguage.T("Strategie: Wir kommen ohne Stopp ins Ziel, aber nur mit etwa " + m + " Runden Luft.",
                            "Strategy: we make the finish without a stop, but only with about " + m + " laps in hand.",
                            "Estrategia: llegamos a meta sin parar, pero solo con unas " + m + " vueltas de margen."),
                        RadioLanguage.T("Kein Stopp nötig. " + LimitNameCap(p.Limit) + " reicht, Reserve etwa " + m + " Runden.",
                            "No stop needed. " + LimitNameCap(p.Limit) + " lasts, margin about " + m + " laps.",
                            "No hace falta parar. " + LimitNameCap(p.Limit) + " alcanza, margen de unas " + m + " vueltas."),
                        RadioLanguage.T("Wir fahren durch. Die Reserve ist knapp, rund " + m + " Runden.",
                            "We run to the end. The margin is tight, around " + m + " laps.",
                            "Vamos hasta el final. El margen es justo, unas " + m + " vueltas."),
                        RadioLanguage.T("Plan steht: durchfahren. Luft sind etwa " + m + " Runden, also sauber bleiben.",
                            "Plan is set: no stop. About " + m + " laps of margin, so keep it clean.",
                            "El plan está claro: sin parar. Hay unas " + m + " vueltas de margen, así que sin errores."));
                }
                case "sparen":
                {
                    string target = RadioLanguage.Num(p.EnergyTargetPerLap, "0.0"), save = RadioLanguage.Num(Math.Max(0, p.EnergySavePerLap), "0.0");
                    return RadioVariants.Pick("strategy-plan-energy-save",
                        RadioLanguage.T("Mit der Energie wird es knapp. Ziel sind " + target + " Prozent pro Runde, das sind etwa " + save + " weniger als jetzt.",
                            "Energy is getting tight. Target is " + target + " percent per lap, about " + save + " less than now.",
                            "La energía va justa. El objetivo es " + target + " por ciento por vuelta, unos " + save + " menos que ahora."),
                        RadioLanguage.T("Energie sparen: Bis ins Ziel brauchen wir " + target + " Prozent pro Runde. Früher vom Gas, dann reicht es.",
                            "Save energy: to the finish we need " + target + " percent per lap. Lift a bit earlier and it works.",
                            "Ahorra energía: hasta meta necesitamos " + target + " por ciento por vuelta. Levanta un poco antes y llega."),
                        RadioLanguage.T("Die Energie begrenzt uns. Wenn du pro Runde " + save + " Prozent weniger brauchst, kommen wir durch.",
                            "Energy is the limit. If you use " + save + " percent less per lap, we make it.",
                            "La energía nos limita. Si gastas " + save + " por ciento menos por vuelta, llegamos."),
                        RadioLanguage.T("Strategie: durchfahren mit Energiesparen. Zielwert " + target + " Prozent pro Runde.",
                            "Strategy: no stop, with energy saving. Target " + target + " percent per lap.",
                            "Estrategia: sin parar, ahorrando energía. Objetivo " + target + " por ciento por vuelta."));
                }
                case "reifen_schonen":
                {
                    string corner = Corner(p.TyreCorner), tyre = RadioLanguage.Num(p.TyreLaps, "0"), toGo = RadioLanguage.Num(Math.Floor(p.LapsToGo), "0");
                    return RadioVariants.Pick("strategy-plan-tyres",
                        RadioLanguage.T("Sprit reicht, aber die Reifen werden das Limit. " + corner + " hält bei diesem Abbau noch etwa " + tyre + " Runden, bis ins Ziel sind es " + toGo + ".",
                            "Fuel is fine, but the tyres will be the limit. " + corner + " lasts about " + tyre + " more laps at this rate, " + toGo + " to go.",
                            "El combustible llega, pero los neumáticos serán el límite. El " + corner + " aguanta unas " + tyre + " vueltas a este ritmo, quedan " + toGo + "."),
                        RadioLanguage.T("Reifen schonen: " + corner + " reicht noch rund " + tyre + " Runden, wir brauchen " + toGo + ".",
                            "Look after the tyres: " + corner + " has around " + tyre + " laps left, we need " + toGo + ".",
                            "Cuida los neumáticos: al " + corner + " le quedan unas " + tyre + " vueltas, necesitamos " + toGo + "."),
                        RadioLanguage.T("Ohne Stopp geht es nur, wenn du die Reifen etwas schonst. " + corner + " ist der kritische, noch etwa " + tyre + " Runden.",
                            "No stop only works if you go a bit easier on the tyres. " + corner + " is the critical one, about " + tyre + " laps left.",
                            "Sin parar solo sale si cuidas un poco los neumáticos. El " + corner + " es el crítico, quedan unas " + tyre + " vueltas."),
                        RadioLanguage.T("Strategie: durchfahren, aber mit Reifenmanagement. " + corner + " hat bei diesem Tempo etwa " + tyre + " Runden, Ziel ist in " + toGo + ".",
                            "Strategy: no stop, but manage the tyres. " + corner + " has about " + tyre + " laps at this pace, the finish is in " + toGo + ".",
                            "Estrategia: sin parar, pero gestionando neumáticos. El " + corner + " tiene unas " + tyre + " vueltas a este ritmo, la meta está a " + toGo + "."));
                }
                case "ein_stopp":
                case "pflichtstopp":
                {
                    string a = p.WindowFirstLap.ToString(), b = p.WindowLastLap.ToString(), limit = LimitName(p.PlanReason ?? p.Limit), by = LimitBy(p.PlanReason ?? p.Limit);
                    if (p.Plan == "pflichtstopp")
                        return RadioVariants.Pick("strategy-plan-mandatory",
                            RadioLanguage.T("Der Pflichtstopp steht noch aus. Spätestens am Ende von Runde " + b + " solltest du rein.",
                                "The mandatory stop is still to do. Come in at the end of lap " + b + " at the latest.",
                                "Falta la parada obligatoria. Entra como muy tarde al final de la vuelta " + b + "."),
                            RadioLanguage.T("Denk an den Pflichtstopp. Das Fenster reicht bis Runde " + b + ".",
                                "Remember the mandatory stop. The window runs to lap " + b + ".",
                                "Recuerda la parada obligatoria. La ventana llega hasta la vuelta " + b + "."),
                            RadioLanguage.T("Pflichtstopp offen: Wir können ihn bis Ende Runde " + b + " legen.",
                                "Mandatory stop open: we can take it up to the end of lap " + b + ".",
                                "Parada obligatoria pendiente: podemos hacerla hasta el final de la vuelta " + b + "."),
                            RadioLanguage.T("Strategie: Pflichtstopp, spätestens Runde " + b + ".",
                                "Strategy: mandatory stop, lap " + b + " at the latest.",
                                "Estrategia: parada obligatoria, como muy tarde en la vuelta " + b + "."));
                    if (p.WindowFirstLap == p.WindowLastLap)
                        return RadioVariants.Pick("strategy-plan-stop-fixed",
                            RadioLanguage.T("Strategie: ein Stopp, und zwar am Ende von Runde " + b + ". Vorher reicht ein Stopp nicht, später reicht " + limit + " nicht.",
                                "Strategy: one stop, at the end of lap " + b + ". Earlier one stop isn't enough, later " + limit + " won't last.",
                                "Estrategia: una parada, al final de la vuelta " + b + ". Antes no basta una parada, después no alcanza " + limit + "."),
                            RadioLanguage.T("Ein Stopp, genau am Ende von Runde " + b + ".",
                                "One stop, exactly at the end of lap " + b + ".",
                                "Una parada, justo al final de la vuelta " + b + "."),
                            RadioLanguage.T("Der Stopp muss am Ende von Runde " + b + " kommen, das Fenster ist nur diese eine Runde.",
                                "The stop has to be at the end of lap " + b + ", the window is just that one lap.",
                                "La parada tiene que ser al final de la vuelta " + b + ", la ventana es solo esa vuelta."),
                            RadioLanguage.T("Plan: Box am Ende von Runde " + b + ". Begrenzt durch " + by + ".",
                                "Plan: box at the end of lap " + b + ". Limited by " + limit + ".",
                                "Plan: a boxes al final de la vuelta " + b + ". Limitados por " + limit + "."));
                    return RadioVariants.Pick("strategy-plan-stop",
                        RadioLanguage.T("Strategie: ein Stopp. Das Fenster geht von Runde " + a + " bis " + b + ", begrenzt durch " + by + ".",
                            "Strategy: one stop. The window runs from lap " + a + " to " + b + ", limited by " + limit + ".",
                            "Estrategia: una parada. La ventana va de la vuelta " + a + " a la " + b + ", limitados por " + limit + "."),
                        RadioLanguage.T("Wir planen einen Stopp, irgendwann zwischen Runde " + a + " und " + b + ".",
                            "We're planning one stop, somewhere between lap " + a + " and " + b + ".",
                            "Planeamos una parada, entre la vuelta " + a + " y la " + b + "."),
                        RadioLanguage.T("Ohne Stopp reicht " + limit + " nicht. Ein Stopp genügt, Fenster Runde " + a + " bis " + b + ".",
                            "Without a stop " + limit + " won't last. One stop is enough, window lap " + a + " to " + b + ".",
                            "Sin parar no alcanza " + limit + ". Basta una parada, ventana de la vuelta " + a + " a la " + b + "."),
                        RadioLanguage.T("Plan: ein Boxenstopp. Frühestens Ende Runde " + a + ", spätestens Ende Runde " + b + ".",
                            "Plan: one pit stop. End of lap " + a + " at the earliest, end of lap " + b + " at the latest.",
                            "Plan: una parada. Como pronto al final de la vuelta " + a + ", como tarde al final de la " + b + "."));
                }
                case "zwei_stopps":
                {
                    string b = p.WindowLastLap.ToString();
                    return RadioVariants.Pick("strategy-plan-two",
                        RadioLanguage.T("So reicht ein Stopp nicht bis ins Ziel, wir brauchen zwei. Der erste spätestens am Ende von Runde " + b + ".",
                            "One stop won't get us to the finish like this, we need two. The first one at the end of lap " + b + " at the latest.",
                            "Así una parada no basta para llegar, necesitamos dos. La primera como muy tarde al final de la vuelta " + b + "."),
                        RadioLanguage.T("Strategie: zwei Stopps. Erster Stopp bis Runde " + b + ".",
                            "Strategy: two stops. First stop by lap " + b + ".",
                            "Estrategia: dos paradas. La primera antes de la vuelta " + b + "."),
                        RadioLanguage.T("Ein Tank reicht nicht bis ins Ziel, wir müssen zweimal rein. Erster Stopp spätestens Runde " + b + ".",
                            "One tank won't reach the finish, we have to come in twice. First stop lap " + b + " at the latest.",
                            "Un depósito no llega a meta, tenemos que entrar dos veces. Primera parada como tarde en la vuelta " + b + "."),
                        RadioLanguage.T("Rechnung sagt zwei Stopps. Der erste muss bis Ende Runde " + b + " passieren.",
                            "The numbers say two stops. The first has to happen by the end of lap " + b + ".",
                            "Las cuentas dicen dos paradas. La primera tiene que ser antes del final de la vuelta " + b + "."));
                }
                default: return null;
            }
        }

        internal static string WindowOpen(StrategyPicture p)
        {
            string b = p.WindowLastLap.ToString();
            return RadioVariants.Pick("strategy-window-open",
                RadioLanguage.T("Das Boxenfenster ist offen: Ab jetzt reicht ein Stopp bis ins Ziel. Spätestens am Ende von Runde " + b + ".",
                    "The pit window is open: from now on one stop gets us to the finish. End of lap " + b + " at the latest.",
                    "La ventana de boxes está abierta: desde ahora una parada nos lleva a meta. Como muy tarde al final de la vuelta " + b + "."),
                RadioLanguage.T("Fenster offen. Wann du reinkommst, entscheiden wir nach Verkehr, spätestens Runde " + b + ".",
                    "Window open. We'll pick the moment by traffic, lap " + b + " at the latest.",
                    "Ventana abierta. Elegimos el momento según el tráfico, como tarde en la vuelta " + b + "."),
                RadioLanguage.T("Ab jetzt sind wir im Boxenfenster. Letzte Möglichkeit ist Ende Runde " + b + ".",
                    "We're in the pit window from now. Last chance is the end of lap " + b + ".",
                    "Desde ahora estamos en la ventana de boxes. La última opción es el final de la vuelta " + b + "."),
                RadioLanguage.T("Boxenfenster auf. Ein Stopp genügt jetzt, Zeit bis Runde " + b + ".",
                    "Pit window is open. One stop is enough now, we have until lap " + b + ".",
                    "Ventana de boxes abierta. Ahora basta una parada, tenemos hasta la vuelta " + b + "."));
        }

        internal static string WindowLast(StrategyPicture p)
        {
            string b = p.WindowLastLap.ToString(), limit = LimitName(p.Limit);
            return RadioVariants.Pick("strategy-window-last",
                RadioLanguage.T("Das ist die letzte Runde im Fenster. Am Ende von Runde " + b + " musst du rein, danach reicht " + limit + " nicht mehr.",
                    "This is the last lap in the window. You have to come in at the end of lap " + b + ", after that " + limit + " won't last.",
                    "Es la última vuelta de la ventana. Tienes que entrar al final de la vuelta " + b + ", después no alcanza " + limit + "."),
                RadioLanguage.T("Box am Ende dieser Runde. Länger reicht " + limit + " nicht.",
                    "Box at the end of this lap. " + LimitNameCap(p.Limit) + " won't last longer.",
                    "A boxes al final de esta vuelta. " + LimitNameCap(p.Limit) + " no da para más."),
                RadioLanguage.T("Diese Runde noch, dann rein. Runde " + b + " ist die letzte, die " + limit + " hergibt.",
                    "This lap, then in. Lap " + b + " is the last one " + limit + " allows.",
                    "Esta vuelta y dentro. La vuelta " + b + " es la última que permite " + limit + "."),
                RadioLanguage.T("Letzte Gelegenheit: Ende Runde " + b + " an die Box.",
                    "Last chance: pit at the end of lap " + b + ".",
                    "Última oportunidad: a boxes al final de la vuelta " + b + "."));
        }

        internal static string PaceFade(StrategyPicture p)
        {
            double tenths = Math.Round(Math.Abs(p.PaceSlope) * 10);
            string amount = tenths >= 1
                ? RadioLanguage.T(tenths == 1 ? "ein Zehntel" : RadioLanguage.Num(tenths, "0") + " Zehntel",
                    tenths == 1 ? "a tenth" : RadioLanguage.Num(tenths, "0") + " tenths",
                    tenths == 1 ? "una décima" : RadioLanguage.Num(tenths, "0") + " décimas")
                : RadioLanguage.T("knapp ein Zehntel", "just under a tenth", "casi una décima");
            string n = p.PaceLaps.ToString();
            return RadioVariants.Pick("strategy-pace-fade",
                RadioLanguage.T("Deine Rundenzeiten werden seit " + n + " Runden langsamer, etwa " + amount + " pro Runde.",
                    "Your lap times have been dropping off for " + n + " laps, about " + amount + " per lap.",
                    "Tus tiempos llevan " + n + " vueltas empeorando, unas " + amount + " por vuelta."),
                RadioLanguage.T("Info zum Tempo: Über die letzten " + n + " Runden verlierst du rund " + amount + " pro Runde.",
                    "Pace info: over the last " + n + " laps you're losing around " + amount + " per lap.",
                    "Info de ritmo: en las últimas " + n + " vueltas pierdes unas " + amount + " por vuelta."),
                RadioLanguage.T("Die Zeiten gehen langsam hoch, " + amount + " pro Runde seit " + n + " Runden.",
                    "Times are creeping up, " + amount + " per lap over " + n + " laps.",
                    "Los tiempos van subiendo, " + amount + " por vuelta en " + n + " vueltas."),
                RadioLanguage.T("Messung im Stint: pro Runde etwa " + amount + " langsamer, über " + n + " saubere Runden.",
                    "Stint measurement: about " + amount + " slower per lap, over " + n + " clean laps.",
                    "Medición del stint: unas " + amount + " más lento por vuelta, en " + n + " vueltas limpias."));
        }
    }
}
