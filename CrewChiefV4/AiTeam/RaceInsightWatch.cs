using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Haupt-Renningenieur liest das Feld (Rob 17.11.2026, Kern für AMS2, RaceRoom, LMU): Nach jeder Runde sucht Ben
    /// Erkenntnisse, die ein Mensch am Kommandostand sehen würde – nicht nur Abstände zum direkten Nachbarn.
    /// Grundsatz aus der Recherche: Lagebild → Urteil → Sprache. Mehrere Kandidaten werden bewertet, die spezifischste und
    /// wichtigste gewinnt; jede Art hat ihr eigenes Gedächtnis, damit nichts wiederholt wird. Nur gemessene Werte aus
    /// Crew Chiefs Zeitabständen und Rundenzeiten, die in allen drei Sims gleich gepflegt werden.
    ///
    /// Erkenntnisse:
    /// <list type="bullet">
    /// <item>Zweikampf vor dir: Die zwei Autos direkt vor Rob liegen höchstens eine Sekunde auseinander und Rob ist nah dran
    /// oder kommt näher – die beiden halten sich auf, das ist seine Chance.</item>
    /// <item>Schnellster im Feld: Robs letzte zwei Runden waren jeweils die schnellsten seiner Klasse.</item>
    /// <item>Freie Fahrt: vorn und hinten mindestens sechs Sekunden Luft, hinten wachsend – Gelegenheit, Reifen und Sprit zu schonen.</item>
    /// <item>Stopp-Bilanz: Autos vor Rob mit weniger Stopps oder Autos hinter ihm mit mehr Stopps. Ist der eigene Boxenverlust
    /// gemessen (<see cref="PitLossMeter"/>), sagt Ben, wer rechnerisch nach den Stopps vorn liegt – als Rechnung gekennzeichnet.</item>
    /// </list>
    /// </summary>
    internal sealed class RaceInsightWatch
    {
        internal const float BattleGap = 1.0f, ApproachGap = 5f, FreeAir = 6f;
        private readonly Random random;
        private int session = int.MinValue, lastInsightLap = -100, fastestStreak, lastFastestLap = -100, lastFreeAirLap = -100, lastTargetLap = -100, lastStopsLap = -100, spoken;
        private string lastStopsKey;
        private readonly Dictionary<string, int> lastBattleLap = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>(StringComparer.Ordinal);
        private ObservedRaceCall call;
        /// <summary>Abstand zwischen zwei Gegnern; austauschbar für Tests.</summary>
        internal Func<OpponentData, OpponentData, float> Gap = GapBetween;
        /// <summary>Abstand zwischen Rob und einem Gegner derselben Runde; austauschbar für Tests.</summary>
        internal Func<GameStateData, OpponentData, float> GapToRob = GapToPlayer;

        internal RaceInsightWatch(int? seed = null) { random = seed.HasValue ? new Random(seed.Value) : new Random(); }

        internal void Reset()
        {
            session = int.MinValue; lastInsightLap = lastFastestLap = lastFreeAirLap = lastTargetLap = lastStopsLap = -100; fastestStreak = spoken = 0;
            lastStopsKey = null; lastBattleLap.Clear(); lastVariant.Clear(); call = null;
        }

        private int Variant(string group, int count)
        {
            int v = random.Next(count);
            if (count > 1 && lastVariant.TryGetValue(group, out int last) && v == last) v = (v + 1 + random.Next(count - 1)) % count;
            lastVariant[group] = v;
            return v;
        }

        private static bool SameClass(GameStateData gs, OpponentData o) =>
            o != null && (gs.carClass == null || o.CarClass == null || CarData.IsCarClassEqual(gs.carClass, o.CarClass, false));

        private static OpponentData AtClassPosition(GameStateData gs, int position) =>
            position < 1 ? null : gs.OpponentData.Values.FirstOrDefault(o => o != null && o.IsActive && o.ClassPosition == position && SameClass(gs, o));

        /// <summary>Abstand zwischen zwei Gegnern in Sekunden (gleiche Runde); -1 wenn nicht belegt.</summary>
        internal static float GapBetween(OpponentData front, OpponentData back)
        {
            try
            {
                if (front?.DeltaTime == null || back?.DeltaTime == null) return -1;
                var d = front.DeltaTime.GetAbsoluteTimeDeltaAllowingForLapDifferences(back.DeltaTime);
                return d.Item1 == 0 && d.Item2 > 0 && d.Item2 < 60 ? d.Item2 : -1;
            }
            catch (Exception) { return -1; }
        }

        /// <summary>Abstand Rob ↔ Gegner in Sekunden (gleiche Runde, egal ob vor oder hinter Rob); -1 wenn nicht belegt.</summary>
        internal static float GapToPlayer(GameStateData gs, OpponentData o)
        {
            try
            {
                var me = gs?.SessionData.DeltaTime;
                if (me == null || o?.DeltaTime == null) return -1;
                bool ahead = o.ClassPosition > 0 && o.ClassPosition < gs.SessionData.ClassPosition;
                var d = ahead ? o.DeltaTime.GetAbsoluteTimeDeltaAllowingForLapDifferences(me) : me.GetAbsoluteTimeDeltaAllowingForLapDifferences(o.DeltaTime);
                return d.Item1 == 0 && d.Item2 > 0 && d.Item2 < 300 ? d.Item2 : -1;
            }
            catch (Exception) { return -1; }
        }

        /// <summary>Gegner, deren Stopp-Bilanz von Robs abweicht: vor ihm mit weniger Stopps, hinter ihm mit mehr – nächstgelegene zuerst.</summary>
        internal List<KeyValuePair<OpponentData, float>> StopDifference(GameStateData gs, bool ahead, float pitLoss)
        {
            var s = gs.SessionData;
            int mine = gs.PitData.NumPitStops;
            float reach = pitLoss > 0 ? pitLoss + 10 : 30;
            return gs.OpponentData.Values
                .Where(o => o != null && o.IsActive && !o.InPits && SameClass(gs, o) && o.ClassPosition > 0 &&
                    (ahead ? o.ClassPosition < s.ClassPosition && o.NumPitStops < mine : o.ClassPosition > s.ClassPosition && o.NumPitStops > mine))
                .Select(o => new KeyValuePair<OpponentData, float>(o, GapToRob(gs, o)))
                .Where(p => p.Value > 0 && p.Value <= reach)
                .OrderBy(p => p.Value).Take(3).ToList();
        }

        private static string Name(OpponentData o, bool names) => names && o != null && o.CanUseName ? RadioPhraseCatalog.SafeRivalName(o.DriverRawName) : null;

        /// <summary>Nach jeder abgeschlossenen Rennrunde aufrufen.</summary>
        internal void Observe(GameStateData gs, RaceSituation mind, string driver, bool names, DateTime now, bool lapCompleted, float pitLoss = -1)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (call != null && (call.DeliveryStartedUtc != DateTime.MinValue || !call.IsValid(gs, now)))
            {
                if (call.DeliveryStartedUtc != DateTime.MinValue) spoken++;
                call = null;
            }
            if (!lapCompleted || s.SessionType != SessionType.Race || s.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane) return;
            int lap = s.CompletedLaps;
            UpdateFastest(gs, lap);
            Publish(gs, s, lap, names, pitLoss);
            if (call != null || lap < 2 || lap - lastInsightLap < 2) return;
            string n = spoken % 2 == 0 ? driver : null;

            // 1. Zweikampf vor dir – am spezifischsten, darum zuerst.
            var a = AtClassPosition(gs, s.ClassPosition - 1);
            var b = AtClassPosition(gs, s.ClassPosition - 2);
            float toA = s.TimeDeltaFront;
            if (a != null && b != null && !a.InPits && !b.InPits && toA > 0 && toA <= ApproachGap)
            {
                float between = Gap(b, a);
                bool closing = mind != null && mind.FrontRate >= GapTrendWatch.MinRate;
                string key = (a.DriverRawName ?? "a") + "|" + (b.DriverRawName ?? "b");
                if (between > 0 && between <= BattleGap && (closing || toA <= 2f) &&
                    (!lastBattleLap.TryGetValue(key, out int last) || lap - last >= 5))
                {
                    lastBattleLap[key] = lap;
                    Offer(gs, now, lap, "insight-battle-ahead",
                        BattleAhead(n, Name(b, names), Name(a, names), between, toA, closing ? mind.FrontRate : 0, Variant("battle", 4)),
                        "two_cars_ahead_within_one_second_and_rob_close_or_closing", 4);
                    return;
                }
            }

            // 2. Podium oder Führung in Reichweite: der Vordermann liegt auf Platz 3 oder 1, höchstens drei Sekunden weg und Rob kommt näher.
            int target = s.ClassPosition - 1;
            if ((target == 3 || target == 1) && toA > 0 && toA <= 3f && (mind == null || mind.FrontRate >= GapTrendWatch.MinRate || toA <= 1.5f) &&
                lap - lastTargetLap >= 6)
            {
                lastTargetLap = lap;
                Offer(gs, now, lap, "insight-target", TargetText(n, target, toA, mind != null && mind.FrontRate >= GapTrendWatch.MinRate ? mind.FrontRate : 0, Variant("target", 3)),
                    "car_ahead_holds_podium_or_lead_within_three_seconds", 4);
                return;
            }

            // 3. Stopp-Bilanz: Wer vor Rob hat noch weniger gestoppt, wer hinter ihm schon öfter? Mit gemessenem Boxenverlust
            //    rechnet Ben aus, wie es nach den Stopps aussieht – ausdrücklich als Rechnung.
            if (StopsInsight(gs, now, lap, n, names, pitLoss)) return;

            // 4. Schnellster der Klasse zwei Runden in Folge.
            if (fastestStreak >= 2 && lap - lastFastestLap >= 8)
            {
                lastFastestLap = lap;
                Offer(gs, now, lap, "insight-fastest", Fastest(n, fastestStreak, Variant("fastest", 4)), "rob_last_laps_fastest_in_class", 3);
                return;
            }

            // 5. Freie Fahrt mitten im Rennen.
            float ahead = s.TimeDeltaFront, behind = s.TimeDeltaBehind;
            bool middle = mind == null || mind.Phase == RacePhase.Middle;
            bool pullingAway = mind == null || mind.RearRate <= 0;
            if (middle && lap - lastFreeAirLap >= 15 && (ahead <= 0 || ahead >= FreeAir) && behind >= FreeAir && pullingAway && s.ClassPosition > 0)
            {
                lastFreeAirLap = lap;
                Offer(gs, now, lap, "insight-free-air", FreeAirText(n, ahead, behind, Variant("free", 4)), "gaps_both_ways_at_least_six_seconds", 2);
            }
        }

        private static Newtonsoft.Json.Linq.JObject latest;
        private static int latestSession = int.MinValue;

        /// <summary>Feld-Erkenntnisse der letzten Runde für Sprachfragen („Wie läuft's?“); null ohne aktuelle Daten.</summary>
        internal static Newtonsoft.Json.Linq.JObject Latest(int sessionId) =>
            System.Threading.Volatile.Read(ref latestSession) == sessionId ? System.Threading.Volatile.Read(ref latest) : null;

        private bool StopsInsight(GameStateData gs, DateTime now, int lap, string n, bool names, float pitLoss)
        {
            if (lap < 3 || gs.PitData.OnOutLap || gs.PitData.OnInLap) return false;
            var ahead = StopDifference(gs, true, pitLoss);
            var behind = ahead.Count == 0 ? StopDifference(gs, false, pitLoss) : new List<KeyValuePair<OpponentData, float>>();
            var list = ahead.Count > 0 ? ahead : behind;
            if (list.Count == 0) return false;
            string key = (ahead.Count > 0 ? "a:" : "b:") + string.Join("|", list.Select(p => (p.Key.DriverRawName ?? "?") + "#" + p.Key.NumPitStops).OrderBy(x => x, StringComparer.Ordinal)) + "/" + gs.PitData.NumPitStops;
            if (key == lastStopsKey ? lap - lastStopsLap < 12 : lap - lastStopsLap < 4) return false;
            lastStopsKey = key; lastStopsLap = lap;
            var who = list.Select(p => Name(p.Key, names) ?? "P" + p.Key.ClassPosition).ToList();
            // Rechnerisch: Liegt der Abstand unter dem Boxenverlust, dreht sich die Reihenfolge nach dem fehlenden Stopp.
            var turned = pitLoss > 0 ? list.Where(p => p.Value < pitLoss - 1.5f).Select(p => Name(p.Key, names) ?? "P" + p.Key.ClassPosition).ToList() : null;
            int variant = Variant(ahead.Count > 0 ? "stops-ahead" : "stops-behind", 3);
            string text = ahead.Count > 0 ? StopsAheadText(n, who, turned, pitLoss, variant) : StopsBehindText(n, who, turned, pitLoss, variant);
            Offer(gs, now, lap, ahead.Count > 0 ? "insight-stops-ahead" : "insight-stops-behind", text,
                (ahead.Count > 0 ? "class_rivals_ahead_with_fewer_stops" : "class_rivals_behind_with_more_stops") + (pitLoss > 0 ? "_and_measured_pit_loss" : ""), 3);
            return true;
        }

        private void Publish(GameStateData gs, SessionData s, int lap, bool names, float pitLoss)
        {
            var o = new Newtonsoft.Json.Linq.JObject { ["runde"] = lap, ["schnellste_klassenrunden_in_folge"] = fastestStreak };
            var a = AtClassPosition(gs, s.ClassPosition - 1);
            var b = AtClassPosition(gs, s.ClassPosition - 2);
            if (a != null && b != null && !a.InPits && !b.InPits)
            {
                float between = Gap(b, a);
                if (between > 0)
                    o["zweikampf_vor_dir"] = new Newtonsoft.Json.Linq.JObject
                    {
                        ["vorne"] = Name(b, names) ?? "P" + b.ClassPosition, ["dahinter"] = Name(a, names) ?? "P" + a.ClassPosition,
                        ["abstand_zwischen_beiden_s"] = Math.Round(between, 2), ["dein_abstand_s"] = s.TimeDeltaFront > 0 ? (Newtonsoft.Json.Linq.JToken)Math.Round(s.TimeDeltaFront, 2) : null,
                        ["echter_zweikampf"] = between <= BattleGap
                    };
            }
            if ((s.ClassPosition - 1 == 3 || s.ClassPosition - 1 == 1) && s.TimeDeltaFront > 0)
                o[s.ClassPosition - 1 == 1 ? "abstand_zur_fuehrung_s" : "abstand_zum_podium_s"] = Math.Round(s.TimeDeltaFront, 2);
            o["freie_fahrt"] = (s.TimeDeltaFront <= 0 || s.TimeDeltaFront >= FreeAir) && s.TimeDeltaBehind >= FreeAir;
            o["eigene_stopps"] = gs.PitData.NumPitStops;
            if (pitLoss > 0) o["gemessener_boxenverlust_s"] = Math.Round(pitLoss, 1);
            Newtonsoft.Json.Linq.JArray Stops(List<KeyValuePair<OpponentData, float>> l) => new Newtonsoft.Json.Linq.JArray(l.Select(p => new Newtonsoft.Json.Linq.JObject
            {
                ["fahrer"] = Name(p.Key, names) ?? "P" + p.Key.ClassPosition, ["stopps"] = p.Key.NumPitStops, ["abstand_s"] = Math.Round(p.Value, 1),
                ["nach_stopp_rechnerisch_getauscht"] = pitLoss > 0 ? (Newtonsoft.Json.Linq.JToken)(p.Value < pitLoss - 1.5f) : null
            }));
            var fewer = StopDifference(gs, true, pitLoss); var more = StopDifference(gs, false, pitLoss);
            if (fewer.Count > 0) o["vor_dir_mit_weniger_stopps"] = Stops(fewer);
            if (more.Count > 0) o["hinter_dir_mit_mehr_stopps"] = Stops(more);
            o["hinweis"] = "Gemessene Abstände und Rundenzeiten der letzten Runde; ein Zweikampf gilt ab höchstens 1 s zwischen den beiden. " +
                "Getauscht nach Stopp ist eine Rechnung mit Robs gemessenem Boxenverlust inklusive Standzeit, keine Gewissheit.";
            System.Threading.Volatile.Write(ref latest, o);
            System.Threading.Volatile.Write(ref latestSession, s.SessionId);
        }

        private void UpdateFastest(GameStateData gs, int lap)
        {
            float mine = gs.SessionData.LapTimePrevious;
            if (!(mine > 1) || !gs.SessionData.PreviousLapWasValid) { fastestStreak = 0; return; }
            var rivals = gs.OpponentData.Values.Where(o => o != null && o.IsActive && SameClass(gs, o) && o.LastLapTime > 1 && o.LastLapTime < 3600 && o.LastLapValid).Select(o => o.LastLapTime).ToList();
            if (rivals.Count < 3) { fastestStreak = 0; return; } // allein auf der Strecke ist „Schnellster“ keine Aussage
            fastestStreak = mine < rivals.Min() ? fastestStreak + 1 : 0;
        }

        private void Offer(GameStateData gs, DateTime now, int lap, string topic, string text, string evidence, int priority)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            lastInsightLap = lap;
            call = new ObservedRaceCall
            {
                Topic = topic, Text = text.Trim(), Evidence = evidence, Member = TeamMember.Ben, Priority = priority, Position = -1,
                SessionId = gs.SessionData.SessionId, SessionType = gs.SessionData.SessionType, DetectedUtc = now, ExpiresUtc = now.AddSeconds(35),
                AdditionalValidity = live => !live.PitData.InPitlane && !live.FlagData.isLocalYellow
            };
            LiveDiagnostics.Decision(topic, "detected", evidence, call.Text, call.EventId);
        }

        internal ObservedRaceCall Take(GameStateData gs, DateTime now, bool busy, bool benEnabled)
        {
            if (call != null && (!call.IsValid(gs, now) || call.DeliveryStartedUtc != DateTime.MinValue)) return null;
            if (busy || !benEnabled || call == null) return null;
            return call.TryReserve(now) ? call : null;
        }

        // ---------- Sprache (DE/EN/ES, selbst formuliert) ----------

        private static string Sec(float v) => GermanRadioText.Interval(v);

        internal static string BattleAhead(string driver, string first, string second, float between, float toSecond, float rate, int variant)
        {
            string n = string.IsNullOrWhiteSpace(driver) ? null : driver;
            string pair = first != null && second != null ? first + RadioLanguage.T(" und ", " and ", " y ") + second
                : RadioLanguage.T("die beiden Autos", "the two cars", "los dos coches");
            string Pair() => char.ToUpper(pair[0], RadioLanguage.Culture) + pair.Substring(1);
            string closing = rate > 0 ? RadioLanguage.T(" Du holst " + GapTrendWatch.Rate(rate) + " pro Runde auf.", " You're gaining " + GapTrendWatch.Rate(rate) + " a lap.", " Recortas " + GapTrendWatch.Rate(rate) + " por vuelta.") : "";
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T((n != null ? n + ", vor" : "Vor") + " dir kämpfen " + pair + ", nur " + Sec(between) + " auseinander. Die halten sich gegenseitig auf." + closing,
                    (n != null ? n + ", " + pair : Pair()) + " are fighting ahead of you, only " + Sec(between) + " apart. They're holding each other up." + closing,
                    (n != null ? n + ", " + pair : Pair()) + " pelean delante de ti, a solo " + Sec(between) + ". Se están frenando entre ellos." + closing);
                case 1: return RadioLanguage.T(Pair() + " liefern sich einen Zweikampf, " + Sec(between) + " Abstand. Du bist " + Sec(toSecond) + " dahinter – das ist deine Chance." + closing,
                    Pair() + " are in a battle, " + Sec(between) + " apart. You're " + Sec(toSecond) + " behind – that's your chance." + closing,
                    Pair() + " están en plena lucha, a " + Sec(between) + ". Tú estás a " + Sec(toSecond) + ", es tu oportunidad." + closing);
                case 2: return RadioLanguage.T("Da vorne wird gekämpft: " + pair + ", " + Sec(between) + " auseinander. Ruhig ranfahren, die schenken sich nichts." + closing,
                    "There's a fight up ahead: " + pair + ", " + Sec(between) + " apart. Close in calmly, they're not giving an inch." + closing,
                    "Hay pelea delante: " + pair + ", a " + Sec(between) + ". Acércate con calma, no se regalan nada." + closing);
                default: return RadioLanguage.T("Augen auf" + (n != null ? ", " + n : "") + ": " + pair + " sind im Clinch. Wenn einer einen Fehler macht, bist du da." + closing,
                    "Eyes open" + (n != null ? ", " + n : "") + ": " + pair + " are locked together. If one of them slips up, you're there." + closing,
                    "Atento" + (n != null ? ", " + n : "") + ": " + pair + " van pegados. Si uno falla, ahí estás tú." + closing);
            }
        }

        internal static string TargetText(string driver, int target, float gap, float rate, int variant)
        {
            string n = string.IsNullOrWhiteSpace(driver) ? null : driver;
            bool lead = target == 1;
            string what = lead ? RadioLanguage.T("die Führung", "the lead", "el liderato") : RadioLanguage.T("das Podium", "the podium", "el podio");
            string What = char.ToUpper(what[0], RadioLanguage.Culture) + what.Substring(1);
            string closing = rate > 0 ? RadioLanguage.T(", und du holst " + GapTrendWatch.Rate(rate) + " pro Runde auf", ", and you're gaining " + GapTrendWatch.Rate(rate) + " a lap", ", y recortas " + GapTrendWatch.Rate(rate) + " por vuelta") : "";
            switch (((variant % 3) + 3) % 3)
            {
                case 0: return RadioLanguage.T(What + " ist nur " + Sec(gap) + " entfernt" + closing + ". Bleib dran" + (n != null ? ", " + n : "") + "!",
                    What + " is only " + Sec(gap) + " away" + closing + ". Stay with it" + (n != null ? ", " + n : "") + "!",
                    (lead ? "El liderato" : "El podio") + " está a solo " + Sec(gap) + closing + ". ¡No lo sueltes" + (n != null ? ", " + n : "") + "!");
                case 1: return RadioLanguage.T((n != null ? n + ", " : "") + Sec(gap) + " bis " + (lead ? "zur Führung" : "zum Podium") + closing + ". Jetzt zählt jede Kurve.",
                    (n != null ? n + ", " : "") + Sec(gap) + " to " + what + closing + ". Every corner counts now.",
                    (n != null ? n + ", " : "") + Sec(gap) + " hasta " + (lead ? "el liderato" : "el podio") + closing + ". Ahora cuenta cada curva.");
                default: return RadioLanguage.T("Da vorne wartet " + what + ": " + Sec(gap) + closing + ". Sauber bleiben, Druck aufbauen.",
                    What + " is right there: " + Sec(gap) + closing + ". Stay clean, build the pressure.",
                    "Ahí delante está " + what + ": " + Sec(gap) + closing + ". Limpio y metiendo presión.");
            }
        }

        internal static string Fastest(string driver, int laps, int variant)
        {
            string n = string.IsNullOrWhiteSpace(driver) ? null : driver;
            string l = laps.ToString(RadioLanguage.Culture);
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T((n != null ? n + ", du" : "Du") + " warst die letzten " + l + " Runden der Schnellste in deiner Klasse. Stark!",
                    (n != null ? n + ", you" : "You") + " were the quickest in your class for the last " + l + " laps. Great stuff!",
                    (n != null ? n + ", has" : "Has") + " sido el más rápido de tu clase en las últimas " + l + " vueltas. ¡Genial!");
                case 1: return RadioLanguage.T("Niemand in deiner Klasse ist gerade schneller als du – " + l + " Runden in Folge. Genau so weiter.",
                    "Nobody in your class is quicker than you right now – " + l + " laps in a row. Keep doing exactly that.",
                    "Ahora mismo nadie de tu clase va más rápido que tú: " + l + " vueltas seguidas. Sigue exactamente así.");
                case 2: return RadioLanguage.T("Bestes Tempo im Feld" + (n != null ? ", " + n : "") + ". " + l + " Runden lang die schnellste Zeit der Klasse.",
                    "Best pace in the field" + (n != null ? ", " + n : "") + ". Fastest lap in class for " + l + " laps.",
                    "El mejor ritmo del grupo" + (n != null ? ", " + n : "") + ". La vuelta más rápida de la clase durante " + l + " vueltas.");
                default: return RadioLanguage.T("Das Tempo stimmt: " + l + " Runden hintereinander die Schnellste deiner Klasse. Das zahlt sich aus.",
                    "The pace is right: fastest in your class " + l + " laps running. That's paying off.",
                    "El ritmo es bueno: el más rápido de tu clase " + l + " vueltas seguidas. Eso se nota.");
            }
        }

        /// <summary>„A“, „A und B“, „A, B und C“ in der Funksprache.</summary>
        internal static string List(IList<string> names)
        {
            if (names == null || names.Count == 0) return "";
            if (names.Count == 1) return names[0];
            return string.Join(", ", names.Take(names.Count - 1)) + RadioLanguage.T(" und ", " and ", " y ") + names[names.Count - 1];
        }

        internal static string StopsAheadText(string driver, IList<string> who, IList<string> turned, float pitLoss, int variant)
        {
            string n = string.IsNullOrWhiteSpace(driver) ? null : driver;
            bool many = who.Count > 1;
            string list = List(who), l = pitLoss > 0 ? Sec(pitLoss) : null;
            string calc = "";
            if (pitLoss > 0 && turned != null)
                calc = turned.Count == 0
                    ? RadioLanguage.T(" Der Vorsprung ist aber größer als unser Boxenverlust von etwa " + l + " – die bleiben auch danach vorn.",
                        " But the margin is bigger than our pit loss of about " + l + " – they stay ahead even after stopping.",
                        " Pero la ventaja es mayor que nuestra pérdida en boxes de unos " + l + ": seguirán delante después de parar.")
                    : RadioLanguage.T(" Mit unserem gemessenen Boxenverlust von etwa " + l + " liegst du nach dem Stopp rechnerisch vor " + List(turned) + ".",
                        " With our measured pit loss of about " + l + ", on paper you'll be ahead of " + List(turned) + " after the stop.",
                        " Con nuestra pérdida medida en boxes de unos " + l + ", sobre el papel quedas por delante de " + List(turned) + " tras su parada.");
            switch (((variant % 3) + 3) % 3)
            {
                case 0: return RadioLanguage.T("Zur Einordnung" + (n != null ? ", " + n : "") + ": " + list + " vor dir " + (many ? "haben" : "hat") + " einmal weniger gestoppt als du." + calc,
                    "For context" + (n != null ? ", " + n : "") + ": " + list + " ahead of you " + (many ? "have" : "has") + " stopped once less than you." + calc,
                    "Para que lo sepas" + (n != null ? ", " + n : "") + ": " + list + " delante de ti " + (many ? "han" : "ha") + " parado una vez menos que tú." + calc);
                case 1: return RadioLanguage.T("Die Reihenfolge vorne täuscht: " + list + " " + (many ? "müssen" : "muss") + ", wenn die Stoppzahl gleich sein soll, noch an die Box." + calc,
                    "The order ahead is misleading: " + list + " still " + (many ? "have" : "has") + " a stop to make if the stop count evens out." + calc,
                    "El orden de delante engaña: " + list + " aún " + (many ? "tienen" : "tiene") + " que parar si el número de paradas se iguala." + calc);
                default: return RadioLanguage.T("Stopp-Bilanz: Du warst schon öfter drin als " + list + "." + calc + " Bleib auf deinem Tempo.",
                    "Stop count: you've been in more often than " + list + "." + calc + " Stick to your pace.",
                    "Cuenta de paradas: has entrado más veces que " + list + "." + calc + " Mantén tu ritmo.");
            }
        }

        internal static string StopsBehindText(string driver, IList<string> who, IList<string> turned, float pitLoss, int variant)
        {
            string n = string.IsNullOrWhiteSpace(driver) ? null : driver;
            bool many = who.Count > 1;
            string list = List(who), l = pitLoss > 0 ? Sec(pitLoss) : null;
            string calc = "";
            if (pitLoss > 0 && turned != null)
                calc = turned.Count == 0
                    ? RadioLanguage.T(" Dein Polster ist größer als unser Boxenverlust von etwa " + l + " – nach deinem Stopp bleibst du rechnerisch vorn.",
                        " Your cushion is bigger than our pit loss of about " + l + " – on paper you stay ahead after your stop.",
                        " Tu colchón es mayor que nuestra pérdida en boxes de unos " + l + ": sobre el papel sigues delante tras tu parada.")
                    : RadioLanguage.T(" Mit etwa " + l + " Boxenverlust liegt " + List(turned) + " nach deinem Stopp rechnerisch vor dir.",
                        " With about " + l + " of pit loss, " + List(turned) + " would be ahead of you after your stop on paper.",
                        " Con unos " + l + " de pérdida en boxes, " + List(turned) + " quedaría delante tras tu parada, sobre el papel.");
            switch (((variant % 3) + 3) % 3)
            {
                case 0: return RadioLanguage.T("Achtung" + (n != null ? ", " + n : "") + ": " + list + " hinter dir " + (many ? "haben" : "hat") + " schon einmal mehr gestoppt als du." + calc,
                    "Heads up" + (n != null ? ", " + n : "") + ": " + list + " behind you " + (many ? "have" : "has") + " already stopped once more than you." + calc,
                    "Atención" + (n != null ? ", " + n : "") + ": " + list + " detrás de ti ya " + (many ? "han" : "ha") + " parado una vez más que tú." + calc);
                case 1: return RadioLanguage.T("Der Abstand nach hinten täuscht: " + list + " " + (many ? "haben" : "hat") + " den Stopp schon hinter sich, du noch nicht." + calc,
                    "The gap behind is misleading: " + list + " " + (many ? "have" : "has") + " already made the stop, you haven't." + calc,
                    "La distancia por detrás engaña: " + list + " ya " + (many ? "han" : "ha") + " hecho la parada, tú todavía no." + calc);
                default: return RadioLanguage.T("Blick auf die Stopps: " + list + " war schon öfter an der Box als du." + calc,
                    "Watch the stops: " + list + (many ? " have" : " has") + " pitted more often than you." + calc,
                    "Ojo a las paradas: " + list + (many ? " han" : " ha") + " entrado en boxes más veces que tú." + calc);
            }
        }

        internal static string FreeAirText(string driver, float ahead, float behind, int variant)
        {
            string n = string.IsNullOrWhiteSpace(driver) ? null : driver;
            string front = ahead > 0 ? RadioLanguage.T("vorne " + Sec(ahead) + ", ", Sec(ahead) + " ahead, ", "delante " + Sec(ahead) + ", ") : "";
            string rear = RadioLanguage.T("hinten " + Sec(behind), Sec(behind) + " behind", "detrás " + Sec(behind));
            string gaps = front + rear;
            string Gaps() => char.ToUpper(gaps[0], RadioLanguage.Culture) + gaps.Substring(1);
            switch (((variant % 4) + 4) % 4)
            {
                case 0: return RadioLanguage.T((n != null ? n + ", du" : "Du") + " fährst gerade allein: " + gaps + ". Gute Gelegenheit, Reifen und Sprit zu schonen.",
                    (n != null ? n + ", you're" : "You're") + " running on your own: " + gaps + ". Good chance to look after tyres and fuel.",
                    (n != null ? n + ", vas" : "Vas") + " solo: " + gaps + ". Buena ocasión para cuidar neumáticos y combustible.");
                case 1: return RadioLanguage.T("Freie Fahrt. " + Gaps() + ". Saubere Runden, nichts riskieren.",
                    "Clear track. " + Gaps() + ". Clean laps, no risks.",
                    "Pista libre. " + Gaps() + ". Vueltas limpias, sin riesgos.");
                case 2: return RadioLanguage.T("Ruhige Phase: " + gaps + ". Nutz sie, um deinen Rhythmus zu festigen.",
                    "Quiet phase: " + gaps + ". Use it to settle your rhythm.",
                    "Fase tranquila: " + gaps + ". Aprovéchala para asentar tu ritmo.");
                default: return RadioLanguage.T("Viel Luft um dich herum, " + gaps + ". Hier gewinnst du am meisten mit gleichmäßigen Runden.",
                    "Plenty of space around you, " + gaps + ". Consistent laps gain you the most here.",
                    "Mucho espacio a tu alrededor, " + gaps + ". Aquí lo que más ganas son vueltas constantes.");
            }
        }
    }
}
