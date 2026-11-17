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
    /// </list>
    /// </summary>
    internal sealed class RaceInsightWatch
    {
        internal const float BattleGap = 1.0f, ApproachGap = 5f, FreeAir = 6f;
        private readonly Random random;
        private int session = int.MinValue, lastInsightLap = -100, fastestStreak, lastFastestLap = -100, lastFreeAirLap = -100, spoken;
        private readonly Dictionary<string, int> lastBattleLap = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>(StringComparer.Ordinal);
        private ObservedRaceCall call;
        /// <summary>Abstand zwischen zwei Gegnern; austauschbar für Tests.</summary>
        internal Func<OpponentData, OpponentData, float> Gap = GapBetween;

        internal RaceInsightWatch(int? seed = null) { random = seed.HasValue ? new Random(seed.Value) : new Random(); }

        internal void Reset()
        {
            session = int.MinValue; lastInsightLap = lastFastestLap = lastFreeAirLap = -100; fastestStreak = spoken = 0;
            lastBattleLap.Clear(); lastVariant.Clear(); call = null;
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

        private static string Name(OpponentData o, bool names) => names && o != null && o.CanUseName ? RadioPhraseCatalog.SafeRivalName(o.DriverRawName) : null;

        /// <summary>Nach jeder abgeschlossenen Rennrunde aufrufen.</summary>
        internal void Observe(GameStateData gs, RaceSituation mind, string driver, bool names, DateTime now, bool lapCompleted)
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

            // 2. Schnellster der Klasse zwei Runden in Folge.
            if (fastestStreak >= 2 && lap - lastFastestLap >= 8)
            {
                lastFastestLap = lap;
                Offer(gs, now, lap, "insight-fastest", Fastest(n, fastestStreak, Variant("fastest", 4)), "rob_last_laps_fastest_in_class", 3);
                return;
            }

            // 3. Freie Fahrt mitten im Rennen.
            float ahead = s.TimeDeltaFront, behind = s.TimeDeltaBehind;
            bool middle = mind == null || mind.Phase == RacePhase.Middle;
            bool pullingAway = mind == null || mind.RearRate <= 0;
            if (middle && lap - lastFreeAirLap >= 15 && (ahead <= 0 || ahead >= FreeAir) && behind >= FreeAir && pullingAway && s.ClassPosition > 0)
            {
                lastFreeAirLap = lap;
                Offer(gs, now, lap, "insight-free-air", FreeAirText(n, ahead, behind, Variant("free", 4)), "gaps_both_ways_at_least_six_seconds", 2);
            }
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
