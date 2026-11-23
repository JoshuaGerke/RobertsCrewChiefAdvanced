using System;
using System.Collections.Generic;
using System.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Welche Art von Ansage: Sicherheit geht immer, Rennereignisse fast immer, Daten nur mit Luft.</summary>
    internal enum RadioClass { Safety, Event, Coaching, Encouragement, Data }

    /// <summary>
    /// Funklast (Rob 18.11.2026): Balance zwischen „das Rennen kommentieren“ und „zwischendurch Daten und Technik ansagen“,
    /// ohne in heiklen Momenten und engen Zweikämpfen zu viel zu reden. Wie ein Renningenieur am Kommandostand:
    /// <list type="number">
    /// <item><b>Anspannung</b> (0–1) aus Messwerten: Abstand nach vorn/hinten, Auto daneben, Zweikampf, Verkehr, Gelb,
    /// Start, letzte Runde, Fahrer unter Druck. Sie klingt nach einem Zweikampf langsam ab (Halbwertszeit 8 s), damit nach
    /// der Schlacht nicht sofort Zahlen kommen.</item>
    /// <item><b>Sprechzeit</b>: Wie viele Sekunden die Crew in der letzten Minute schon gefunkt hat. Daten haben ein eigenes,
    /// kleines Budget und brauchen Abstand zur letzten Ansage.</item>
    /// <item><b>Rangfolge</b>: Sicherheit &gt; Rennereignis (Platz, eigene Runde, Nachfrage) &gt; Lob/Ansporn &gt; Coaching &gt;
    /// Daten. Je höher die Anspannung, desto weiter oben muss eine Ansage stehen, um durchzukommen.</item>
    /// </list>
    /// Was wartet, verfällt nach seiner eigenen Lebensdauer – veraltete Zahlen werden nicht nachgeholt.
    /// Die Grenzen sind bewusst großzügig (Bathurst und Monza 17.11. waren gut): sie schneiden nur Überfluss ab.
    /// </summary>
    internal sealed class RadioLoad
    {
        internal static readonly TimeSpan Window = TimeSpan.FromSeconds(60), DataGap = TimeSpan.FromSeconds(8), HalfLife = TimeSpan.FromSeconds(8),
            SummaryGap = TimeSpan.FromSeconds(3);
        /// <summary>Höchste Anspannung, bei der eine Klasse noch sprechen darf.</summary>
        internal const double EventLimit = .95, EncouragementLimit = .8, CoachingLimit = .6, DataLimit = .55;
        /// <summary>Sprechzeit je Minute: insgesamt (ohne Sicherheit) und für Daten.</summary>
        internal const double AirtimePerMinute = 35, DataAirtimePerMinute = 18;

        private readonly object gate = new object();
        private readonly List<KeyValuePair<DateTime, double>> spoken = new List<KeyValuePair<DateTime, double>>();
        private readonly List<KeyValuePair<DateTime, double>> spokenData = new List<KeyValuePair<DateTime, double>>();
        private DateTime lastSpoken = DateTime.MinValue, lastTension = DateTime.MinValue, quietSince = DateTime.MinValue;
        private double tension;

        internal void Reset() { lock (gate) { spoken.Clear(); spokenData.Clear(); lastSpoken = lastTension = quietSince = DateTime.MinValue; tension = 0; } }

        /// <summary>
        /// RaceRoom Hockenheim 23.11. (25 Autos, 15 Minuten): Rob war 72 % der Zeit im Zweikampf, die Anspannung stand fast
        /// durchgehend auf 0,8 bis 1. Sektorvergleich, Näherkommen, Wetter, Strecke und Reifen warteten, bis sie verfielen –
        /// Rob: „Es fehlt mir die eigenständige Entscheidung, was wichtig ist.“ Wie ein Renningenieur am Kommandostand: Je länger
        /// der Funk still war, desto eher ist auch im Pulk Platz für die nächste Information. Ab einer Funkstille (Funkmenge
        /// Normal 20 s, Mehr Infos 12 s, Viel mehr Infos 8 s, Ruhig 35 s) sinkt die Hürde um 0,02 je Sekunde, höchstens um 0,5.
        /// Auto daneben, Kurven und Bremszonen halten die Meldung weiter auf (Fahrerbelastung und Spotter).
        /// </summary>
        internal static double SilenceRelief(double silentSeconds, int density)
        {
            double start = density < 0 ? 8 : density == 0 ? 12 : density == 2 ? 35 : 20;
            return Math.Max(0, Math.Min(.5, (silentSeconds - start) * .02));
        }

        /// <summary>Sekunden seit dem letzten Funkspruch (bzw. seit Beginn der Messung).</summary>
        internal double SilentSeconds(DateTime now)
        {
            lock (gate)
            {
                DateTime since = lastSpoken > quietSince ? lastSpoken : quietSince;
                return since == DateTime.MinValue || now < since ? 0 : (now - since).TotalSeconds;
            }
        }

        /// <summary>Die Funkstille ist so lang, dass die Crew auch im Zweikampf etwas sagen darf (Hürde gesenkt).</summary>
        internal bool SilenceEarned(DateTime now) => SilenceRelief(SilentSeconds(now), Density) > 0;

        /// <summary>
        /// Informationen über Robs eigenen Kampf (Rob 23.11.: „Sektorenvergleiche zum Vordermann, gute Rennaktionen, Zweikämpfe,
        /// Näherkommen, Weiterwegfahren vom Hinteren“): Sektorvergleich mit dem direkten Gegner, Abstandsverlauf, Tempovergleich,
        /// Zielansage, Zweikampf voraus und der Rennverlauf mit Vorder- und Hintermann. Sie sind im Zweikampf das Wichtigste und
        /// zählen als Rennereignis, nicht als Statistik.
        /// </summary>
        internal static bool FightContext(string topic) =>
            topic == "battle-sector" || topic == "rival-sector-loss" || topic == "gap-trend" || topic == "reader-pace-front" ||
            topic == "reader-pace-rear" || topic == "insight-target" || topic == "insight-battle-ahead" || topic == "focus-brief" ||
            topic == "rear-pressure" || topic == "race-rear-pressure" || topic == "rival-topspeed" ||
            topic == "story-duel-front" || topic == "story-duel-rear" || topic == "story-kept-ahead" || topic == "story-kept-behind";

        /// <summary>Anspannung aus einem Lagebild ohne Glättung (0 = ruhig, 1 = höchste Konzentration).</summary>
        internal static double RawTension(float gapAhead, float gapBehind, bool overlap, RaceAttention attention, RacePhase phase, DriverMoodState mood)
        {
            if (overlap) return 1;
            double t = Math.Max(Gap(gapAhead), Gap(gapBehind));
            switch (attention)
            {
                case RaceAttention.Battle: t = Math.Max(t, .8); break;
                case RaceAttention.Caution: t = Math.Max(t, .7); break;
                case RaceAttention.Traffic: t = Math.Max(t, .6); break;
            }
            if (phase == RacePhase.Start) t = Math.Max(t, .9);
            else if (phase == RacePhase.LastLap) t = Math.Max(t, .7);
            else if (phase == RacePhase.Final) t = Math.Min(1, t + .1);
            if (mood == DriverMoodState.Pressure) t = Math.Min(1, t + .15);
            return t;
        }

        private static double Gap(float g) => !(g > 0) ? 0 : g < .5f ? 1 : g < 1f ? .8 : g < 1.5f ? .55 : g < 2.5f ? .3 : 0;

        /// <summary>Neuer Messwert: steigt sofort, fällt mit Halbwertszeit (nach dem Zweikampf erst durchatmen).</summary>
        internal double Update(double raw, DateTime now)
        {
            lock (gate)
            {
                double decayed = tension;
                if (lastTension != DateTime.MinValue && now > lastTension)
                    decayed = tension * Math.Pow(.5, (now - lastTension).TotalSeconds / HalfLife.TotalSeconds);
                tension = Math.Max(Math.Max(0, Math.Min(1, raw)), decayed);
                lastTension = now;
                if (quietSince == DateTime.MinValue) quietSince = now;
                return tension;
            }
        }

        internal double Tension { get { lock (gate) return tension; } }

        /// <summary>Etwa 2,6 Wörter pro Sekunde am Funk, plus Ein- und Ausklang.</summary>
        internal static double EstimateSeconds(string text) =>
            string.IsNullOrWhiteSpace(text) ? 0 : .6 + text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length / 2.6;

        internal void NoteSpoken(string topic, string text, DateTime now)
        {
            double seconds = EstimateSeconds(text);
            if (seconds <= 0) return;
            lock (gate)
            {
                spoken.Add(new KeyValuePair<DateTime, double>(now, seconds));
                if (Classify(topic, 3) == RadioClass.Data) spokenData.Add(new KeyValuePair<DateTime, double>(now, seconds));
                lastSpoken = now;
                Trim(now);
            }
        }

        private void Trim(DateTime now)
        {
            spoken.RemoveAll(p => p.Key > now || now - p.Key > Window);
            spokenData.RemoveAll(p => p.Key > now || now - p.Key > Window);
        }

        internal double Airtime(DateTime now) { lock (gate) { Trim(now); return spoken.Sum(p => p.Value); } }
        internal double DataAirtime(DateTime now) { lock (gate) { Trim(now); return spokenData.Sum(p => p.Value); } }

        internal static RadioClass Classify(string topic, int priority)
        {
            string t = topic ?? "";
            if (priority >= 6 || t.StartsWith("warning-", StringComparison.Ordinal) || t == "race-hazard-ahead" || t == "race-traffic-rear" ||
                t == "race-disqualified" || t == "incident-points" || t.StartsWith("pit-", StringComparison.Ordinal) && t != "pit-stop-time")
                return RadioClass.Safety;
            if (DriverMood.IsCoaching(t)) return RadioClass.Coaching;
            if (t.StartsWith("judge-", StringComparison.Ordinal) || t == "personal-best-record" || t == "personal-best-sector" ||
                t == "sector-best" || t == "near-best" || t == "own-best-pace" || t == "lap-on-pace" || t == "personal-best-near")
                return t == "judge-dropped" || t == "judge-spin" || t == "judge-impact" ? RadioClass.Event : RadioClass.Encouragement;
            // Rennverlauf (Rob 23.11.): „freigefahren“, „abgesetzt“ und die kurze Reaktion eines Kollegen sind Ton, keine Daten;
            // Zweikampfdauer und „wie angekündigt“ gehören zum eigenen Rennen (Ereignis).
            if (t == "story-free" || t == "story-escaped" || t == "story-followup") return RadioClass.Encouragement;
            // Rob (Monza 22.11.): „Sofort eine Rundenzeit, wenn jemand eine Bestrunde gefahren hat.“ Die Klassen- bzw. Feldbestzeit
            // ist ein Rennereignis, keine Statistik – beide Meldungen warteten als Daten im Dauerzweikampf, bis sie verfielen.
            if (t == "field-fastest" || t == "field-fastest-overall") return RadioClass.Event;
            if (t.StartsWith("fuel-", StringComparison.Ordinal) || t.StartsWith("tyre-", StringComparison.Ordinal) || t.StartsWith("engine-", StringComparison.Ordinal) ||
                t.StartsWith("field-", StringComparison.Ordinal) || t.StartsWith("reader-", StringComparison.Ordinal) || t.StartsWith("weather-", StringComparison.Ordinal) ||
                t == "stint-consistency" || t == "status" || t == "summary" || t == "race-summary" || t == "gap-trend" || t == "rival-best" ||
                t == "rival-sector-loss" || t == "pit-stop-time" || t == "personal-best-brief" || t == "insight-dirty-tyres" || t == "insight-flatspot" ||
                t == "track-welcome" || t == "focus-brief")
                return RadioClass.Data;
            return RadioClass.Event; // Platz, eigene Runde, Überholen, Verkehr: das Rennen selbst
        }

        /// <summary>
        /// Rob hat selbst überholt oder einen Platz verloren (Platzmeldung und die Bewertung des Manövers).
        /// LMU 22.11.: „Ich hatte gerade Dubus überholt – keine Reaktion.“ Direkt nach dem Manöver ist der Gegner unter 0,5 s
        /// hinter Rob, die Anspannung steht auf 1 – die Platzmeldung wartete 26-mal in 2-s-Schritten, bis sie verfiel. Genau
        /// dann will Rob sie hören. Diese Meldungen gehen durch die Funklast; Seite an Seite (Überlappung) warten sie weiter.
        /// </summary>
        internal static bool OwnPositionChange(string topic) =>
            topic == "position" || topic == "position-loss" || topic == "judge-overtake" || topic == "judge-retake" ||
            topic == "judge-overtake-rough" || topic == "judge-overtake-countered";

        /// <summary>
        /// Robs eigener Kampf, über den er gerade jetzt Bescheid wissen will: das Startfazit nach der halben ersten Runde und die
        /// Abstände zum direkten Vorder- und Hintermann. LMU Bahrain 22.11.: Das Startfazit verfiel nach 8 s, die Abstände nach vorn
        /// wurden viermal wegen hoher Anspannung gehalten – sie kamen genau dann nicht, wenn es eng wurde. Wie Platzwechsel gehen sie
        /// an Anspannung und Sprechzeit vorbei; Seite an Seite (Überlappung) warten sie weiter.
        /// </summary>
        /// RaceRoom Monza 22.11.: Auch die eingestellten Abstandsberichte zum Vorder- und Hintermann („ahead“/„behind“) gehören dazu –
        /// 18 Berichte erkannt, 3 gespielt, Rob: „Das muss häufiger kommen, wenn ich im Zweikampf bin.“
        internal static bool OwnFight(string topic) =>
            topic == "start-verdict" || topic == "native-gap-front" || topic == "native-gap-behind" || topic == "ahead" || topic == "behind";

        /// <summary>
        /// RaceRoom Hockenheim 23.11.: „Neue persönliche Bestzeit 1:45,073“ wartete 11-mal, der Verbrauch der ersten Runde 15-mal
        /// auf zu hohe Anspannung und verfiel – im Pulk lag die Anspannung fast durchgehend bei 1. Das eigene Rundenergebnis, der
        /// erste gemessene Verbrauch und die schnellste Runde eines Gegners (Rob: „mit Namen und Zeit“) sind kurze Rennereignisse
        /// und gehen an der Anspannung vorbei; Seite an Seite warten sie weiter.
        /// Begrüßung, Startspruch und Rennbilanz gehören ebenfalls dazu: Die Funklast darf sie nie blockieren.
        /// </summary>
        internal static bool OwnResult(string topic) =>
            topic == "own-lap" || topic == "race-lap" || topic == "first-lap-fuel" || topic == "fuel-baseline" ||
            topic == "field-fastest" || topic == "field-fastest-overall" ||
            topic == "greeting" || topic == "race-start" || topic == "session-debrief";

        /// <summary>Darf diese Ansage jetzt sprechen? Sonst wartet sie (Hold) und verfällt, wenn ihre Zeit um ist.</summary>
        internal bool Allows(string topic, int priority, DateTime now, out string reason)
        {
            reason = null;
            var c = Classify(topic, priority);
            if (c == RadioClass.Safety || OwnPositionChange(topic) || OwnFight(topic) || OwnResult(topic) || NativeFieldEvents.SaraNews(topic)) return true;
            if (FightContext(topic)) c = RadioClass.Event;
            double t = Math.Max(0, Tension - SilenceRelief(SilentSeconds(now), Density));
            switch (c)
            {
                case RadioClass.Event:
                    if (t > EventLimit) { reason = "load_tension_too_high_for_event"; return false; }
                    return true;
                case RadioClass.Encouragement:
                    if (t > EncouragementLimit) { reason = "load_tension_praise_waits"; return false; }
                    break;
                case RadioClass.Coaching:
                    if (t > CoachingLimit) { reason = "load_tension_coaching_waits"; return false; }
                    break;
                case RadioClass.Data:
                    // Funkdichte des Fahrers („mehr Funk“ / „weniger Funk“): verschiebt nur die Datengrenzen.
                    int density = Density;
                    // -1 = „viel mehr Infos“ (Rob 23.11.).
                    double limit = density < 0 ? DataLimit + .15 : density == 0 ? DataLimit + .1 : density == 2 ? DataLimit - .1 : DataLimit;
                    double budget = density < 0 ? DataAirtimePerMinute * 2 : density == 0 ? DataAirtimePerMinute * 1.5 : density == 2 ? DataAirtimePerMinute * .6 : DataAirtimePerMinute;
                    TimeSpan gap = density < 0 ? TimeSpan.FromSeconds(5) : density == 0 ? TimeSpan.FromSeconds(6) : density == 2 ? TimeSpan.FromSeconds(12) : DataGap;
                    // LMU Bahrain 22.11.: Der Zwischenstand wartete 13-mal auf 8 s Funkstille und kam nur 2 von 16 Mal. Er entsteht
                    // ohnehin nur in einer ruhigen Minute; ein kurzer Atemzug nach der letzten Ansage genügt.
                    if (topic == "race-summary" && gap > SummaryGap) gap = SummaryGap;
                    if (t > limit) { reason = "load_tension_data_waits"; return false; }
                    if (DataAirtime(now) >= budget) { reason = "load_data_budget_used"; return false; }
                    lock (gate) if (lastSpoken != DateTime.MinValue && now >= lastSpoken && now - lastSpoken < gap) { reason = "load_data_needs_a_gap"; return false; }
                    break;
            }
            if (Airtime(now) >= AirtimePerMinute) { reason = "load_airtime_per_minute_used"; return false; }
            return true;
        }

        /// <summary>Funkdichte; in Tests direkt setzbar, sonst aus den gespeicherten Funkwünschen des Fahrers.</summary>
        internal int? DensityOverride;
        private int Density => DensityOverride ?? AmountDensity(AutoRadioPlan.CurrentAmount, DriverMemory.RadioDensity);

        /// <summary>Funkmenge aus Cockpit oder Funkbefehl hat Vorrang; bei Normal gilt der ältere gespeicherte Funkwunsch.</summary>
        internal static int AmountDensity(RadioAmount amount, int saved) =>
            amount == RadioAmount.MuchMore ? -1 : amount == RadioAmount.More ? 0 : saved;

        internal static string Level(double tension) => tension >= .75 ? "hoch" : tension >= .4 ? "mittel" : "niedrig";
    }
}
