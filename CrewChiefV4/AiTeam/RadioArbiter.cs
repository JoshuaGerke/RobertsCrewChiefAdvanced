using System;
using System.Collections.Generic;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Rangstufen am Kommandostand, wie im echten Rennfunk: Wer darf zuerst, wenn mehrere Meldungen gleichzeitig warten?
    /// <list type="number">
    /// <item><b>Sicherheit</b>: Gefahr voraus, Full Course Yellow, Safety Car, Rot, Blau, Verkehr von hinten. Sofort, vor allem anderen.</item>
    /// <item><b>Antwort</b>: Rob hat gefragt – die Antwort kommt vor jeder Ansage der Crew.</item>
    /// <item><b>Pflicht</b>: Box, Strafe, Schwarz, Schaden, Motor, Tracklimits. Muss ankommen, darf kurz warten.</item>
    /// <item><b>Eigenes Rennen</b>: Platz gewonnen oder verloren, Startfazit, eigene Runde und Bestzeitkurs, Bestzeit eines Gegners.
    /// Nachrichten, die schnell alt werden.</item>
    /// <item><b>Zweikampf</b>: Abstand zum Vorder- und Hintermann. Zahlen, die nur jetzt stimmen.</item>
    /// <item><b>Routine</b>: Stand, Rennfortschritt, Pace, Sprit, Reifen, Stint, Fahrzeug, Wetter. Darf warten, wird beim Sprechen
    /// frisch zusammengesetzt, und rückt mit jeder Sekunde Wartezeit nach vorn.</item>
    /// <item><b>Farbe</b>: Lob, Coaching, Einordnungen. Nur, wenn Platz ist; sonst entfällt sie.</item>
    /// </list>
    /// </summary>
    internal enum RadioTier { Safety = 0, DriverReply = 1, Critical = 2, OwnRace = 3, Fight = 4, Routine = 5, Colour = 6 }

    /// <summary>
    /// Eine Rangfolge für alle Funksprüche (Rob 23.11.: „Es muss eine grundsätzliche Priorisierung her … wenn eine gewisse Zeit
    /// vergangen ist, muss der Spruch nicht gleich kommen, sondern warten. Oder ganz wegfallen.“).
    ///
    /// RaceRoom Monza 22.11.: Von etwa 56 eingestellten Routineberichten kamen 11; Wetter, Sprit, Pace und Stint nie. Die Wächter
    /// werden je Takt in fester Reihenfolge gefragt, wer zuerst etwas hat, spricht – die Routine stand ganz hinten und wartete
    /// zusätzlich auf 8 s Funkstille (im Rennen nur 40 % der Zeit frei). So kam sie praktisch nie dran.
    ///
    /// Die Regeln:
    /// <list type="bullet">
    /// <item><b>Rang plus Wartezeit</b>: Jede wartende Meldung hat einen Wert aus ihrer Rangstufe. Routine gewinnt mit jeder Sekunde
    /// Wartezeit dazu (Altern), bis knapp über den Zweikampf – nie über das eigene Rennen, Pflicht oder Sicherheit. Damit kann
    /// keine Meldung ewig verhungern, und trotzdem schiebt sich nie eine Statistik vor eine Gefahr.</item>
    /// <item><b>Vortritt</b>: Wartet etwas Höherrangiges, lässt eine niedrigere Meldung ihm den nächsten freien Moment.</item>
    /// <item><b>Haltbarkeit</b>: Nachrichten (eigenes Rennen, Farbe, fertig gesprochene Abstände) verfallen nach ihrer Haltbarkeit –
    /// ein „Platz gewonnen“ nach einer halben Minute ist keine Nachricht mehr. Routine verfällt nicht, sie wird beim Sprechen
    /// mit aktuellen Werten neu gebildet.</item>
    /// <item><b>Kein Stillstand</b>: Ein Eintrag gilt nur, solange sein Wächter ihn weiter anbietet (alle paar Sekunden). Kann er
    /// selbst nicht senden, verschwindet er nach kurzer Zeit, und niemand wartet auf ihn.</item>
    /// </list>
    /// </summary>
    internal sealed class RadioArbiter
    {
        /// <summary>Ein Eintrag, der so lange nicht erneut angeboten wurde, gilt als aufgegeben.</summary>
        internal static readonly TimeSpan Forget = TimeSpan.FromSeconds(4);
        /// <summary>Ab dieser Verspätung beansprucht ein fälliger Routinebericht den nächsten freien Moment.</summary>
        internal const int RoutineClaimSeconds = 20;
        /// <summary>Altern der Routine: Punkte je Sekunde Wartezeit und Obergrenze.</summary>
        internal const double RoutineAgingPerSecond = 5, RoutineAgingCap = 250;
        internal const double FightAgingPerSecond = 3, FightAgingCap = 90;

        internal static int BaseScore(RadioTier tier)
        {
            switch (tier)
            {
                case RadioTier.Safety: return 1000;
                case RadioTier.DriverReply: return 900;
                case RadioTier.Critical: return 800;
                case RadioTier.OwnRace: return 600;
                case RadioTier.Fight: return 500;
                case RadioTier.Routine: return 300;
                default: return 200;
            }
        }

        /// <summary>Wert einer Meldung, die seit waitedSeconds wartet. Routine 300 → höchstens 550, Zweikampf 500 → höchstens 590.</summary>
        internal static double Score(RadioTier tier, double waitedSeconds)
        {
            double w = Math.Max(0, waitedSeconds);
            if (tier == RadioTier.Routine) return BaseScore(tier) + Math.Min(RoutineAgingCap, w * RoutineAgingPerSecond);
            if (tier == RadioTier.Fight) return BaseScore(tier) + Math.Min(FightAgingCap, w * FightAgingPerSecond);
            return BaseScore(tier);
        }

        /// <summary>
        /// Wie lange eine fertige Nachricht dieser Stufe gilt. Sicherheit und Pflicht prüfen ihre Gültigkeit selbst (die Gefahr
        /// ist noch da, die Box ist noch offen) und kennen deshalb keine feste Grenze; Routine wird frisch gebildet.
        /// </summary>
        internal static TimeSpan? ShelfLife(RadioTier tier)
        {
            switch (tier)
            {
                case RadioTier.OwnRace: return TimeSpan.FromSeconds(30);
                case RadioTier.Fight: return TimeSpan.FromSeconds(15);
                case RadioTier.Colour: return TimeSpan.FromSeconds(20);
                default: return null;
            }
        }

        /// <summary>Ist diese Nachricht zu alt, um sie noch zu sprechen? Neu gebildete Meldungen (canRefresh) altern nicht.</summary>
        internal static bool Stale(RadioTier tier, DateTime detectedUtc, DateTime now, bool canRefresh)
        {
            var life = ShelfLife(tier);
            return life.HasValue && !canRefresh && detectedUtc != DateTime.MinValue && now >= detectedUtc && now - detectedUtc > life.Value;
        }

        private static readonly HashSet<string> RoutineTopics = new HashSet<string>(StringComparer.Ordinal)
            { "status", "weather", "fuel", "tyres", "pace", "stint", "vehicle", "progress", "race-summary", "focus-brief", "endgame-brief",
              "quali-fuel", "qualifying-result", "first-lap-fuel", "fuel-baseline", "fuel-early" };

        private static readonly HashSet<string> OwnPace = new HashSet<string>(StringComparer.Ordinal)
            { "lap-on-pace", "own-best-pace", "sector-best", "personal-best-sector", "personal-best-record", "near-best", "personal-best-near" };

        /// <summary>Einer der eingestellten regelmäßigen Berichte (Rennstatus, Wetter, Sprit, Reifen, Pace, Vorder-/Hintermann …).</summary>
        internal static bool Scheduled(string topic) => topic != null && AiRadioPreferences.Options.Any(o => o.Id == topic);

        /// <summary>Rangstufe einer Meldung aus Thema und Wächter-Priorität.</summary>
        internal static RadioTier Tier(string topic, int priority)
        {
            string t = topic ?? "";
            if (t == "reply" || t == "driver-reply") return RadioTier.DriverReply;
            if (t == "ahead" || t == "behind" || t == "native-gap-front" || t == "native-gap-behind" || t == "gap-trend" ||
                t == "battle-sector" || t == "rival-sector-loss") return RadioTier.Fight;
            if (RoutineTopics.Contains(t)) return RadioTier.Routine;
            // Warnungen zum eigenen Auto und Regelverstöße: müssen ankommen, sind aber keine Gefahr auf der Strecke.
            if (t == "incident-points" || t == "tyre-wear-warning" || t == "fuel-provisional-short" || t == "warning-cut" ||
                t == "warning-invalid-lap" || t == "warning-numeric-damage" || t.StartsWith("engine-", StringComparison.Ordinal) ||
                t.StartsWith("warning-temperature-", StringComparison.Ordinal)) return RadioTier.Critical;
            var rank = RadioDirectorPolicy.Classify(t);
            var load = RadioLoad.Classify(t, priority);
            if (load == RadioClass.Safety) return rank == RadioRank.Mandatory ? RadioTier.Critical : RadioTier.Safety;
            if (rank == RadioRank.Mandatory) return RadioTier.Critical;
            if (RadioLoad.OwnPositionChange(t) || RadioLoad.OwnFight(t) || OwnPace.Contains(t)) return RadioTier.OwnRace;
            switch (load)
            {
                case RadioClass.Event: return RadioTier.OwnRace;
                case RadioClass.Data: return RadioTier.Routine;
                default: return RadioTier.Colour;
            }
        }

        private sealed class Entry { public RadioTier Tier; public DateTime Since, Seen; }
        private readonly object gate = new object();
        private readonly Dictionary<string, Entry> waiting = new Dictionary<string, Entry>(StringComparer.Ordinal);

        /// <summary>Eine Meldung wartet (oder wird gerade angeboten). since = seit wann sie fällig ist.</summary>
        internal void Note(string topic, RadioTier tier, DateTime since, DateTime now)
        {
            if (string.IsNullOrEmpty(topic)) return;
            lock (gate)
            {
                if (waiting.TryGetValue(topic, out var e) && now - e.Seen <= Forget)
                {
                    e.Tier = tier; e.Seen = now;
                    if (since < e.Since) e.Since = since;
                }
                else waiting[topic] = new Entry { Tier = tier, Since = since > now ? now : since, Seen = now };
            }
        }

        /// <summary>Gesprochen oder verworfen: Die Meldung wartet nicht mehr.</summary>
        internal void Done(string topic)
        {
            if (string.IsNullOrEmpty(topic)) return;
            lock (gate) waiting.Remove(topic);
        }

        internal void Reset() { lock (gate) waiting.Clear(); }

        /// <summary>Seit wann diese Meldung wartet (oder now, wenn sie nicht wartet).</summary>
        internal DateTime WaitingSince(string topic, DateTime now)
        {
            lock (gate) return topic != null && waiting.TryGetValue(topic, out var e) && now - e.Seen <= Forget ? e.Since : now;
        }

        internal int Count(DateTime now) { lock (gate) { Trim(now); return waiting.Count; } }

        private void Trim(DateTime now)
        {
            foreach (var k in waiting.Where(p => now - p.Value.Seen > Forget || p.Value.Seen > now.AddSeconds(1)).Select(p => p.Key).ToArray()) waiting.Remove(k);
        }

        /// <summary>
        /// Soll diese Meldung jetzt einer wartenden Meldung einer anderen Stufe mit höherem Wert den Vortritt lassen? Sicherheit
        /// lässt nie den Vortritt; bei gleichem Wert geht, wer gerade dran ist.
        /// </summary>
        internal bool ShouldYield(string topic, RadioTier tier, DateTime since, DateTime now, out string yieldTo)
        {
            yieldTo = null;
            if (tier == RadioTier.Safety) return false;
            double mine = Score(tier, (now - since).TotalSeconds);
            lock (gate)
            {
                Trim(now);
                double best = mine;
                foreach (var p in waiting)
                {
                    // Gleiche Stufe ordnet ihr Wächter selbst (die Routine hat ihre eigene Dringlichkeit, nur ein Bericht ist
                    // gleichzeitig unterwegs) – sonst warteten zwei Routineberichte aufeinander.
                    // Die eingestellten Berichte (auch Vorder-/Hintermann) kommen aus einem Plan mit je einem Bericht unterwegs: sie
                    // warten nie aufeinander.
                    if (string.Equals(p.Key, topic, StringComparison.Ordinal) || p.Value.Tier == tier || Scheduled(p.Key) && Scheduled(topic)) continue;
                    double other = Score(p.Value.Tier, (now - p.Value.Since).TotalSeconds);
                    if (other > best) { best = other; yieldTo = p.Key; }
                }
            }
            return yieldTo != null;
        }

        /// <summary>Die nächste Meldung, die nach dieser Rangfolge drankommt (für Diagnose und Tests).</summary>
        internal string Next(DateTime now)
        {
            lock (gate)
            {
                Trim(now);
                return waiting.OrderByDescending(p => Score(p.Value.Tier, (now - p.Value.Since).TotalSeconds)).ThenBy(p => p.Value.Since)
                    .Select(p => p.Key).FirstOrDefault();
            }
        }
    }
}
