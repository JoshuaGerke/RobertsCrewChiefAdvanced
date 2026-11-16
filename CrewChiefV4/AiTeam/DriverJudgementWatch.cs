using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Der Renningenieur beurteilt, was der Fahrer tut (Rob 16.11.2026): gute Überholmanöver, Verteidigung gegen Schnellere und
    /// konstante Runden lobt er und spornt an; nach Einschlägen, Kontakten oder mehreren Platzverlusten zeigt er Mitgefühl oder
    /// fragt nach („Rob, was ist los? …“). Grundlage sind nur Messwerte aus dem Rundengedächtnis und der Telemetrie.
    /// Die Sätze wechseln zufällig ohne direkte Wiederholung. Mitten im Zweikampf, unter Gelb und in den letzten Runden hält der
    /// Rennkopf (RaceMind, Themen „judge-…“) die Meldungen zurück bzw. lässt sie verfallen.
    /// </summary>
    internal static class DriverJudgement
    {
        internal const int ConsistencyLaps = 5;
        internal const float ConsistencySpread = .5f;
        internal const float DefenceGap = 1f;

        private static bool Clean(LapRecord l) => l != null && l.Valid && !l.InPitLap && l.LapTime > 0 && l.LapTime < 3600;

        /// <summary>Streuung (schnellste bis langsamste) der letzten <paramref name="count"/> sauberen, zusammenhängenden Runden; -1 wenn nicht belegt.</summary>
        internal static float Spread(IList<LapRecord> laps, int count, out float average)
        {
            average = -1;
            if (laps == null || laps.Count < count) return -1;
            var last = laps.Skip(laps.Count - count).ToList();
            if (!last.All(Clean)) return -1;
            for (int i = 1; i < last.Count; i++) if (last[i].LapNumber != last[i - 1].LapNumber + 1) return -1;
            average = last.Average(l => l.LapTime);
            return last.Max(l => l.LapTime) - last.Min(l => l.LapTime);
        }

        /// <summary>Wie viele Runden in Folge derselbe Hintermann unter einer Sekunde dran war, ohne dass Rob den Platz verloren hat.</summary>
        internal static int DefendedLaps(IList<LapRecord> laps, out string attacker)
        {
            attacker = null;
            if (laps == null || laps.Count == 0) return 0;
            var last = laps[laps.Count - 1];
            if (!Clean(last) || string.IsNullOrEmpty(last.BehindIdentity) || !(last.GapBehind > 0 && last.GapBehind < DefenceGap)) return 0;
            attacker = last.BehindIdentity;
            int n = 0;
            for (int i = laps.Count - 1; i >= 0; i--)
            {
                var l = laps[i];
                if (l.InPitLap || l.BehindIdentity != attacker || !(l.GapBehind > 0 && l.GapBehind < DefenceGap) || l.Position != last.Position) break;
                if (i < laps.Count - 1 && l.LapNumber != laps[i + 1].LapNumber - 1) break;
                n++;
            }
            return n;
        }

        /// <summary>Plätze, die in den letzten <paramref name="lapsBack"/> Runden ohne eigenen Boxenstopp verloren gingen (positiv = verloren).</summary>
        internal static int PositionsLost(IList<LapRecord> laps, int lapsBack)
        {
            if (laps == null || laps.Count <= lapsBack) return 0;
            var now = laps[laps.Count - 1];
            var then = laps[laps.Count - 1 - lapsBack];
            if (now.Position <= 0 || then.Position <= 0 || now.LapNumber - then.LapNumber != lapsBack) return 0;
            for (int i = laps.Count - lapsBack; i < laps.Count; i++) if (laps[i].InPitLap) return 0;
            return now.Position - then.Position;
        }

        /// <summary>Setzt den Namen ein oder lässt die Anrede sauber weg (dann Satzanfang großschreiben, auch nach ¿ und ¡).</summary>
        internal static string WithName(string text, string name)
        {
            if (!string.IsNullOrWhiteSpace(name)) return text.Replace("{n}", name);
            bool atStart = text.StartsWith("{n}, ", StringComparison.Ordinal);
            string t = text.Replace(", {n}", "").Replace("{n}, ", "").Replace(" {n}", "").Replace("{n}", "");
            if (!atStart) return t;
            for (int i = 0; i < t.Length; i++)
                if (char.IsLetter(t[i])) return t.Substring(0, i) + char.ToUpper(t[i], RadioLanguage.Culture) + t.Substring(i + 1);
            return t;
        }

        private static string Pick(int variant, string name, params string[][] options)
        {
            var o = options[((variant % options.Length) + options.Length) % options.Length];
            return WithName(RadioLanguage.T(o[0], o[1], o[2]), name);
        }

        /// <summary>Lob nach einem sauberen, gehaltenen Überholmanöver; mit Ansporn zum Nächsten, wenn der Abstand bekannt ist.</summary>
        internal static string Overtake(string name, string passed, bool hardFought, bool stronger, float gapToNext, int variant)
        {
            string p = string.IsNullOrWhiteSpace(passed) ? null : passed;
            string praise;
            if (stronger && p != null)
                praise = Pick(variant, name,
                    new[] { p + " ist eigentlich schneller unterwegs, und du bist trotzdem vorbei. Stark, {n}!", p + " has the quicker car on paper and you still got by. Great job, {n}!", p + " va más rápido sobre el papel y aun así lo has pasado. ¡Muy bien, {n}!" },
                    new[] { "Klasse Manöver, {n}. Gegen " + p + " ist das kein Geschenk.", "Lovely move, {n}. Nothing comes free against " + p + ".", "Gran maniobra, {n}. Contra " + p + " nada es gratis." },
                    new[] { "Das war mutig und sauber zugleich. " + p + " hat mehr Tempo, aber du hast den Moment genutzt.", "Brave and clean at the same time. " + p + " has more pace, but you took your moment.", "Valiente y limpio a la vez. " + p + " tiene más ritmo, pero aprovechaste el momento." });
            else if (hardFought)
                praise = Pick(variant, name,
                    new[] { "Geduld zahlt sich aus, {n}! Lange dran gearbeitet, jetzt bist du vorbei.", "Patience pays off, {n}! You worked on that for a while and now you're through.", "¡La paciencia tiene premio, {n}! Mucho trabajo y por fin estás delante." },
                    new[] { "Den hast du dir verdient. Runde um Runde Druck gemacht, und dann sauber vorbei" + (p != null ? " an " + p : "") + ".", "You earned that one. Pressure lap after lap, then clean past" + (p != null ? " " + p : "") + ".", "Te lo has ganado. Presión vuelta tras vuelta y luego limpio" + (p != null ? " ante " + p : "") + "." },
                    new[] { "Ja! Genau so, {n}. Aufgebaut, abgewartet, zugeschlagen.", "Yes! That's it, {n}. Set it up, waited, struck.", "¡Sí! Así se hace, {n}. Preparado, paciente y al ataque." });
            else
                praise = Pick(variant, name,
                    new[] { "Sauber gemacht, {n}, ohne Berührung vorbei.", "Nicely done, {n}, past without contact.", "Bien hecho, {n}, adelantamiento sin contacto." },
                    new[] { "Schönes Manöver" + (p != null ? " gegen " + p : "") + ". So will ich das sehen.", "Nice move" + (p != null ? " on " + p : "") + ". That's what I like to see.", "Bonita maniobra" + (p != null ? " sobre " + p : "") + ". Así me gusta." },
                    new[] { "Gut gemacht, {n}. Sauber und mit Übersicht.", "Good job, {n}. Clean and with your head up.", "Buen trabajo, {n}. Limpio y con cabeza." });
            string push;
            int v = ((variant / 3) % 3 + 3) % 3; // Lob und Ansporn wechseln unabhängig voneinander
            if (gapToNext > 0 && gapToNext < 4f)
            {
                string g = GermanRadioText.Interval(gapToNext);
                push = v == 0 ? RadioLanguage.T("Der Nächste ist " + g + " vor dir, hol ihn dir.", "The next one is " + g + " up the road, go get him.", "El siguiente está a " + g + ", ve a por él.")
                    : v == 1 ? RadioLanguage.T(g + " zum Nächsten. Weiter so!", g + " to the next car. Keep it up!", g + " al siguiente. ¡Sigue así!")
                    : RadioLanguage.T("Jetzt den Schwung mitnehmen, vorne sind es " + g + ".", "Carry that momentum now, it's " + g + " to the car ahead.", "Aprovecha el impulso, delante hay " + g + ".");
            }
            else
                push = v == 0 ? RadioLanguage.T("Jetzt den Platz festigen und deinen Rhythmus finden.", "Now settle into the position and find your rhythm.", "Ahora consolida la posición y busca tu ritmo.")
                    : v == 1 ? RadioLanguage.T("Nicht nachlassen, weiter Druck machen.", "Don't ease off, keep the pressure on.", "No aflojes, sigue apretando.")
                    : RadioLanguage.T("Erst mal Luft nach hinten schaffen.", "First build a little gap behind.", "Primero abre un poco de hueco detrás.");
            return praise + " " + push;
        }

        /// <summary>Lob für eine Verteidigung über mehrere Runden gegen einen nachweislich schnelleren Gegner.</summary>
        internal static string Defence(string name, string attacker, int laps, int variant)
        {
            string a = string.IsNullOrWhiteSpace(attacker) ? null : attacker;
            string who = a ?? RadioLanguage.T("der Hintermann", "the car behind", "el de detrás");
            string whoCap = char.ToUpper(who[0], RadioLanguage.Culture) + who.Substring(1);
            string l = laps.ToString(RadioLanguage.Culture);
            return Pick(variant, name,
                new[] { "Stark verteidigt, {n}! " + whoCap + " hat mehr Tempo, aber du lässt keine Lücke.", "Great defending, {n}! " + whoCap + " has more pace, but you're not leaving a gap.", "¡Gran defensa, {n}! " + whoCap + " tiene más ritmo, pero no dejas hueco." },
                new[] { "Seit " + l + " Runden hältst du " + who + " hinter dir. Ruhig bleiben, saubere Linien, genau so.", "You've kept " + who + " behind for " + l + " laps. Stay calm, clean lines, just like that.", "Llevas " + l + " vueltas con " + who + " detrás. Calma, líneas limpias, así." },
                new[] { l + " Runden unter Druck und kein Fehler. Das ist richtig gute Arbeit, {n}.", l + " laps under pressure and not a single mistake. Really good work, {n}.", l + " vueltas bajo presión y ni un error. Muy buen trabajo, {n}." },
                new[] { whoCap + " ist schneller, aber du machst die Tür zu. Weiter so, früh die Linie wählen und dabei bleiben.", whoCap + " is quicker, but you're closing the door. Keep picking your line early and stick to it.", whoCap + " es más rápido, pero cierras la puerta. Sigue eligiendo la línea pronto y mantenla." },
                new[] { "Gute Verteidigung, {n}. Genau so kostet ihn jede Runde hinter dir Zeit.", "Good defending, {n}. Every lap stuck behind you costs him time.", "Buena defensa, {n}. Cada vuelta detrás de ti le cuesta tiempo." });
        }

        /// <summary>Lob für gleichmäßige Runden.</summary>
        internal static string Consistency(string name, int laps, float spread, int variant)
        {
            string s = RadioLanguage.Num(spread, "0.0") + RadioLanguage.T(" Sekunden", " seconds", " segundos");
            if (spread < .05f) s = RadioLanguage.T("unter einem Zehntel", "under a tenth", "menos de una décima");
            string l = laps.ToString(RadioLanguage.Culture);
            return Pick(variant, name,
                new[] { "Starke Konstanz, {n}: " + l + " Runden innerhalb von " + s + ". Wie ein Uhrwerk.", "Great consistency, {n}: " + l + " laps within " + s + ". Like clockwork.", "Gran regularidad, {n}: " + l + " vueltas en " + s + ". Como un reloj." },
                new[] { "Deine letzten " + l + " Runden liegen nur " + s + " auseinander. Genau dieser Rhythmus bringt uns nach vorne.", "Your last " + l + " laps are only " + s + " apart. That rhythm is what moves us forward.", "Tus últimas " + l + " vueltas están a solo " + s + ". Ese ritmo es el que nos hace avanzar." },
                new[] { "Sehr sauber gefahren, {n}. " + l + " Runden, Streuung nur " + s + ".", "Very tidy driving, {n}. " + l + " laps, spread only " + s + ".", "Muy limpio, {n}. " + l + " vueltas, solo " + s + " de diferencia." },
                new[] { "Das ist richtig gleichmäßig. " + s + " Unterschied über " + l + " Runden, mach genau so weiter.", "That's really consistent. " + s + " across " + l + " laps, keep doing exactly that.", "Muy constante. " + s + " de diferencia en " + l + " vueltas, sigue exactamente así." },
                new[] { "Wir sehen nur " + s + " Schwankung über " + l + " Runden. Top, {n}, so fährt man Rennen.", "We're seeing just " + s + " of variation over " + l + " laps. Top stuff, {n}, that's how you race.", "Solo " + s + " de variación en " + l + " vueltas. Genial, {n}, así se corre." });
        }

        /// <summary>Nachfragen und Aufbauen nach mehreren Platzverlusten.</summary>
        internal static string Slide(string name, int lost, int laps, int variant)
        {
            string p = lost.ToString(RadioLanguage.Culture), l = laps.ToString(RadioLanguage.Culture);
            return Pick(variant, name,
                new[] { "{n}, was ist los? Du hast in den letzten " + l + " Runden " + p + " Plätze verloren. Komm wieder in die Spur und konzentrier dich.", "{n}, what's going on? You've lost " + p + " places in the last " + l + " laps. Get back on track and focus.", "{n}, ¿qué pasa? Has perdido " + p + " posiciones en las últimas " + l + " vueltas. Vuelve a tu sitio y concéntrate." },
                new[] { "Kopf hoch, {n}. Das waren " + p + " Plätze in " + l + " Runden. Durchatmen und zurück in deinen Rhythmus.", "Chin up, {n}. That's " + p + " places in " + l + " laps. Breathe and get back into your rhythm.", "Ánimo, {n}. Son " + p + " posiciones en " + l + " vueltas. Respira y vuelve a tu ritmo." },
                new[] { "Alles okay bei dir? Wir verlieren gerade Plätze, " + p + " in " + l + " Runden. Bremspunkte sauber treffen, dann kommt das Tempo zurück.", "Everything okay? We're dropping places, " + p + " in " + l + " laps. Hit your braking points cleanly and the pace will come back.", "¿Todo bien? Estamos perdiendo posiciones, " + p + " en " + l + " vueltas. Clava las frenadas y el ritmo volverá." },
                new[] { "Nicht verrückt machen lassen, {n}. " + p + " Plätze sind weg, aber das Rennen ist noch nicht vorbei. Eine Kurve nach der anderen.", "Don't let it get to you, {n}. " + p + " places gone, but the race isn't over. One corner at a time.", "Que no te afecte, {n}. Se han ido " + p + " posiciones, pero la carrera no ha terminado. Curva a curva." },
                new[] { "{n}, bleib bei dir. Lieber eine saubere Runde als zu viel wollen, dann holen wir uns die Plätze zurück.", "{n}, stay with yourself. A clean lap beats trying too hard, then we'll win those places back.", "{n}, céntrate en lo tuyo. Mejor una vuelta limpia que querer demasiado, luego recuperamos las posiciones." });
        }

        /// <summary>Mitgefühl nach einem Einschlag oder Kontakt, sobald das Auto wieder fährt; beim zweiten Treffer kurz hintereinander deutlicher.</summary>
        internal static string Impact(string name, bool repeated, int variant)
        {
            if (repeated)
                return Pick(variant, name,
                    new[] { "{n}, das ist schon der zweite Treffer. Ein bisschen Tempo raus, wir brauchen das Auto im Ziel.", "{n}, that's the second hit already. Ease off a touch, we need the car at the finish.", "{n}, ya es el segundo golpe. Levanta un poco, necesitamos el coche en la meta." },
                    new[] { "Noch einer. Ganz ruhig jetzt, {n}, erst mal ein paar saubere Runden.", "Another one. Calm down now, {n}, just a few clean laps first.", "Otro más. Tranquilo ahora, {n}, primero unas vueltas limpias." },
                    new[] { "Zweimal kurz hintereinander, das ist ein Zeichen. Atme durch und fahr wieder mit Abstand.", "Twice in a short time, that's a sign. Take a breath and drive with some margin again.", "Dos veces en poco tiempo, es una señal. Respira y vuelve a conducir con margen." });
            return Pick(variant, name,
                new[] { "Alles okay, {n}? Das war ein harter Moment. Durchatmen, das Auto fährt noch.", "You okay, {n}? That was a hard moment. Breathe, the car is still going.", "¿Todo bien, {n}? Ha sido un momento duro. Respira, el coche sigue rodando." },
                new[] { "Das war unglücklich. Abhaken, Kopf frei machen, nächste Kurve.", "That was unlucky. Let it go, clear your head, next corner.", "Mala suerte. Olvídalo, cabeza despejada, siguiente curva." },
                new[] { "Kurz durchschütteln und weiter, {n}. Wir behalten das Auto im Blick.", "Shake it off and carry on, {n}. We're keeping an eye on the car.", "Sacúdetelo y sigue, {n}. Vigilamos el coche." },
                new[] { "Ärgerlich, ich weiß. Jetzt nicht nachlegen wollen, einfach sauber weiterfahren.", "Annoying, I know. Don't try to make it up all at once, just drive clean.", "Fastidia, lo sé. No intentes recuperarlo todo de golpe, conduce limpio." },
                new[] { "Bist du okay? Konzentrier dich auf die nächste Runde, der Rest kommt von allein.", "Are you okay? Focus on the next lap, the rest will follow.", "¿Estás bien? Céntrate en la próxima vuelta, lo demás llegará." });
        }
    }

    internal sealed class DriverJudgementWatch
    {
        private static readonly TimeSpan PraiseGap = TimeSpan.FromSeconds(120), AnyGap = TimeSpan.FromSeconds(60);
        private readonly Random random;
        private int session = int.MinValue, lastPosition = -1, lastConsistencyLap = -100, lastSlideLap = -100, lastDefenceLap = -100;
        private int overtakeCount, spokenCount;
        private string defendedAttacker;
        private DateTime lastPraise = DateTime.MinValue, lastAny = DateTime.MinValue, lastImpact = DateTime.MinValue, lastImpactCall = DateTime.MinValue;
        private readonly Dictionary<string, int> lastVariant = new Dictionary<string, int>(StringComparer.Ordinal);
        private Pending overtake;
        private DateTime impactUtc = DateTime.MinValue;
        private bool impactRepeated;
        private int lastIncidents = -1;
        private readonly Queue<KeyValuePair<DateTime, float>> damage = new Queue<KeyValuePair<DateTime, float>>();
        private ObservedRaceCall call;

        private sealed class Pending
        {
            public DateTime Utc; public int Position, Incidents; public float Damage; public string PassedName, PassedIdentity; public bool Stronger;
        }

        internal DriverJudgementWatch(int? seed = null) { random = seed.HasValue ? new Random(seed.Value) : new Random(); }

        internal void Reset()
        {
            session = int.MinValue; lastPosition = -1; lastConsistencyLap = lastSlideLap = lastDefenceLap = -100; overtakeCount = spokenCount = 0;
            defendedAttacker = null; overtake = null; impactUtc = DateTime.MinValue; impactRepeated = false; lastIncidents = -1; damage.Clear();
            lastPraise = lastAny = lastImpact = lastImpactCall = DateTime.MinValue; lastVariant.Clear(); call = null;
        }

        /// <summary>Zufällige Variante ohne direkte Wiederholung derselben Formulierung.</summary>
        private int Variant(string group, int count)
        {
            int v = random.Next(count);
            if (count > 1 && lastVariant.TryGetValue(group, out int last) && v == last) v = (v + 1 + random.Next(count - 1)) % count;
            lastVariant[group] = v;
            return v;
        }

        /// <summary>Mal mit Namen, mal ohne – wie am echten Funk.</summary>
        private string Name(string radioName) => spokenCount % 2 == 0 ? radioName : null;

        /// <summary>Schadenswerte, die bei Einschlag und Kontakt steigen (Aero, Aufhängung); -1 ohne Daten.</summary>
        internal static float ImpactDamage(CarDamageData d)
        {
            if (d == null || !d.DamageEnabled) return -1;
            var values = new List<float> { d.AeroDamagePercent };
            if (d.SuspensionDamagePercent != null) values.AddRange(d.SuspensionDamagePercent);
            var known = values.Where(x => !float.IsNaN(x) && x >= 0 && x <= 100).ToList();
            return known.Count > 0 ? known.Sum() : -1;
        }

        private static int Incidents(SessionData s) => s != null && s.CurrentIncidentCount >= 0 && s.CurrentIncidentCount < 10000 ? s.CurrentIncidentCount : -1;

        internal void Observe(GameStateData gs, IList<LapRecord> laps, bool lapCompleted, string radioName, bool names, DateTime now)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            if (s.SessionId != session) { Reset(); session = s.SessionId; }
            if (call != null && (call.DeliveryStartedUtc != DateTime.MinValue || !call.IsValid(gs, now)))
            {
                if (call.DeliveryStartedUtc != DateTime.MinValue) spokenCount++;
                call = null;
            }
            bool race = s.SessionType == SessionType.Race;
            bool green = s.SessionPhase == SessionPhase.Green && !gs.PitData.InPitlane;
            ObserveImpact(gs, now, green);
            if (race) ObserveOvertake(gs, laps, names, now, green);
            if (call != null || !green) return;
            string name = Name(radioName);

            // Mitgefühl nach einem Einschlag: erst wenn das Auto wieder rollt, nicht im Moment selbst.
            if (impactUtc != DateTime.MinValue)
            {
                if (now - impactUtc > TimeSpan.FromSeconds(30)) impactUtc = DateTime.MinValue;
                else if (now - impactUtc >= TimeSpan.FromSeconds(8) && gs.PositionAndMotionData.CarSpeed > 25 && now - lastImpactCall > TimeSpan.FromSeconds(90))
                {
                    Offer(gs, now, "judge-impact", DriverJudgement.Impact(name, impactRepeated, Variant(impactRepeated ? "impact2" : "impact", impactRepeated ? 3 : 5)),
                        "damage_or_incident_increase_then_moving_again", 5, TimeSpan.FromSeconds(20));
                    lastImpactCall = now; impactUtc = DateTime.MinValue;
                    return;
                }
            }

            if (overtake != null && race && now - lastAny >= AnyGap && now - lastPraise >= TimeSpan.FromSeconds(60) && now - overtake.Utc >= TimeSpan.FromSeconds(15))
            {
                var o = overtake; overtake = null;
                bool clean = s.ClassPosition > 0 && s.ClassPosition <= o.Position && (o.Incidents < 0 || Incidents(s) == o.Incidents) &&
                    (o.Damage < 0 || ImpactDamage(gs.CarDamageData) - o.Damage < 1f) && now - o.Utc < TimeSpan.FromSeconds(45);
                bool hardFought = laps != null && o.PassedIdentity != null && laps.Reverse().Take(3)
                    .Any(l => l.FrontIdentity == o.PassedIdentity && l.GapAhead > 0 && l.GapAhead < 1.2f);
                // Nicht jedes Überholen braucht ein Lob – nur hart erarbeitete, gegen Schnellere oder jedes dritte saubere.
                if (clean && (hardFought || o.Stronger || ++overtakeCount % 3 == 0))
                {
                    float next = s.TimeDeltaFront > 0 ? s.TimeDeltaFront : -1;
                    Offer(gs, now, "judge-overtake", DriverJudgement.Overtake(name, o.PassedName, hardFought, o.Stronger, next, Variant("overtake", 9)),
                        "position_gain_held_without_new_damage_or_incidents", 4, TimeSpan.FromSeconds(25));
                    lastPraise = now;
                    return;
                }
            }

            if (!lapCompleted || laps == null || laps.Count == 0) return;
            int lap = laps[laps.Count - 1].LapNumber;
            if (race && s.CompletedLaps >= 3 && lap - lastSlideLap >= 4)
            {
                int lost = DriverJudgement.PositionsLost(laps, 2);
                if (lost >= 2)
                {
                    lastSlideLap = lap;
                    Offer(gs, now, "judge-slide", DriverJudgement.Slide(name, lost, 2, Variant("slide", 5)), "lap_memory_positions_lost_without_own_stop", 4, TimeSpan.FromSeconds(30));
                    return;
                }
            }
            if (now - lastPraise < PraiseGap || now - lastAny < AnyGap) return;
            if (race)
            {
                int defended = DriverJudgement.DefendedLaps(laps, out string attacker);
                bool again = attacker != defendedAttacker || lap - lastDefenceLap >= 4;
                if (defended >= 2 && again && AttackerFaster(gs, attacker, out string attackerName))
                {
                    lastDefenceLap = lap; defendedAttacker = attacker;
                    Offer(gs, now, "judge-defence", DriverJudgement.Defence(name, names ? attackerName : null, defended, Variant("defence", 5)),
                        "lap_memory_same_faster_car_within_one_second_position_kept", 4, TimeSpan.FromSeconds(30));
                    lastPraise = now;
                    return;
                }
            }
            if ((race || s.SessionType == SessionType.Practice) && lap - lastConsistencyLap >= 6)
            {
                float spread = DriverJudgement.Spread(laps, DriverJudgement.ConsistencyLaps, out _);
                if (spread >= 0 && spread <= DriverJudgement.ConsistencySpread)
                {
                    lastConsistencyLap = lap;
                    Offer(gs, now, "judge-consistency", DriverJudgement.Consistency(name, DriverJudgement.ConsistencyLaps, spread, Variant("consistency", 5)),
                        "five_clean_consecutive_laps_within_half_a_second", 3, TimeSpan.FromSeconds(30));
                    lastPraise = now;
                }
            }
        }

        private static bool AttackerFaster(GameStateData gs, string identity, out string name)
        {
            name = null;
            var hit = gs.OpponentData.FirstOrDefault(p => p.Value != null && RaceReader.Identity(p.Key, p.Value) == identity);
            var op = hit.Value;
            if (op == null) return false;
            if (op.CanUseName) name = RadioPhraseCatalog.SafeRivalName(op.DriverRawName);
            float mine = gs.SessionData.PlayerLapTimeSessionBest;
            return op.CurrentBestLapTime > 0 && mine > 0 && op.CurrentBestLapTime < mine - .1f;
        }

        private void ObserveImpact(GameStateData gs, DateTime now, bool green)
        {
            var s = gs.SessionData;
            int incidents = Incidents(s);
            bool hit = false;
            if (incidents >= 0 && lastIncidents >= 0 && incidents - lastIncidents >= 2) hit = true; // Kontaktwertung, nicht nur Streckenverlassen
            lastIncidents = incidents;
            float d = ImpactDamage(gs.CarDamageData);
            while (damage.Count > 0 && now - damage.Peek().Key > TimeSpan.FromSeconds(3)) damage.Dequeue();
            if (d >= 0)
            {
                if (damage.Count > 0 && d - damage.Min(p => p.Value) >= 2f) hit = true;
                damage.Enqueue(new KeyValuePair<DateTime, float>(now, d));
            }
            if (!hit || !green || s.SessionType != SessionType.Race && s.SessionType != SessionType.Practice && s.SessionType != SessionType.Qualify) return;
            if (now - lastImpact < TimeSpan.FromSeconds(10)) { lastImpact = now; return; } // derselbe Abflug
            impactRepeated = lastImpact != DateTime.MinValue && now - lastImpact < TimeSpan.FromMinutes(4);
            lastImpact = now; impactUtc = now; overtake = null; damage.Clear();
        }

        private void ObserveOvertake(GameStateData gs, IList<LapRecord> laps, bool names, DateTime now, bool green)
        {
            var s = gs.SessionData;
            int pos = s.ClassPosition;
            if (pos <= 0) { lastPosition = -1; return; }
            int before = lastPosition; lastPosition = pos;
            if (before <= 0) return;
            if (overtake != null && pos > overtake.Position) overtake = null; // Platz gleich wieder verloren
            if (pos != before - 1 || !green || s.CompletedLaps < 1) return;
            var passed = RaceDataTools.Neighbour(gs, false);
            if (passed == null || passed.InPits || passed.isEnteringPits()) return; // Platz durch Boxenstopp des Gegners ist kein Überholmanöver
            var key = gs.OpponentData.FirstOrDefault(p => ReferenceEquals(p.Value, passed)).Key;
            float mine = s.PlayerLapTimeSessionBest;
            overtake = new Pending
            {
                Utc = now, Position = pos, Incidents = Incidents(s), Damage = ImpactDamage(gs.CarDamageData),
                PassedName = names && passed.CanUseName ? RadioPhraseCatalog.SafeRivalName(passed.DriverRawName) : null,
                PassedIdentity = key == null ? null : RaceReader.Identity(key, passed),
                Stronger = passed.CurrentBestLapTime > 0 && mine > 0 && passed.CurrentBestLapTime < mine - .1f
            };
        }

        private void Offer(GameStateData gs, DateTime now, string topic, string text, string evidence, int priority, TimeSpan life)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            lastAny = now;
            call = new ObservedRaceCall
            {
                Topic = topic, Text = text.Trim(), Evidence = evidence, Member = TeamMember.Ben, Priority = priority,
                Position = -1, SessionId = gs.SessionData.SessionId, SessionType = gs.SessionData.SessionType, DetectedUtc = now, ExpiresUtc = now.Add(life),
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
    }
}
