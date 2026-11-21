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

        /// <summary>Rob 21.11.: auch auf weniger gute Manöver reagieren. Vorbei, aber mit neuem Schaden oder Vorfallpunkten.</summary>
        internal static string RoughPass(string name, string passed, int variant)
        {
            string p = string.IsNullOrWhiteSpace(passed) ? null : passed;
            string on = p != null ? " gegen " + p : "", onEn = p != null ? " on " + p : "", onEs = p != null ? " sobre " + p : "";
            return Pick(variant, name,
                new[] { "Vorbei" + on + ", aber mit Kontakt. Das nächste Mal etwas mehr Platz lassen, {n}.", "Through" + onEn + ", but with contact. Leave a bit more room next time, {n}.", "Pasado" + onEs + ", pero con contacto. La próxima, deja algo más de espacio, {n}." },
                new[] { "Der Platz ist unser, aber das war eng. Achte auf die Vorfallpunkte, {n}.", "The place is ours, but that was tight. Mind the incident points, {n}.", "El puesto es nuestro, pero ha sido justo. Ojo con los puntos de incidente, {n}." },
                new[] { "Durch, aber nicht ganz sauber. Lieber eine Kurve später und ohne Berührung.", "Through, but not quite clean. Better a corner later and without contact.", "Pasado, pero no del todo limpio. Mejor una curva más tarde y sin tocar." },
                new[] { "Das hat am Auto Spuren hinterlassen. Nächstes Manöver mit mehr Geduld, {n}.", "That left a mark on the car. More patience on the next one, {n}.", "Eso ha dejado marca en el coche. Más paciencia en la próxima, {n}." },
                new[] { "Platz gewonnen, aber mit Berührung. Sauber zählt mehr als schnell, {n}.", "Place gained, but with contact. Clean counts more than quick, {n}.", "Puesto ganado, pero con contacto. Limpio vale más que rápido, {n}." });
        }

        /// <summary>Rob 21.11.: Überholt und gleich wieder vom selben Auto zurücküberholt.</summary>
        internal static string Countered(string name, string passed, int variant)
        {
            string p = string.IsNullOrWhiteSpace(passed) ? null : passed;
            string who = p ?? "Er", whoEn = p ?? "He", whoEs = p ?? "Te";
            return p != null ? Pick(variant, name,
                new[] { p + " hat direkt gekontert. Beim nächsten Mal den Ausgang sauberer treffen, {n}.", p + " hit straight back. Get a cleaner exit next time, {n}.", p + " ha respondido enseguida. La próxima, mejor salida de curva, {n}." },
                new[] { "Gleich wieder verloren an " + p + ". Erst die Linie zumachen, dann ist der Platz sicher.", "Lost straight back to " + p + ". Close the line first, then the place is safe.", "Perdido otra vez ante " + p + ". Primero cierra la trazada y el puesto es tuyo." },
                new[] { p + " ist wieder vorbei. Kein Problem, du bist schneller, nächste Gelegenheit abwarten.", p + " is back ahead. No problem, you're quicker, wait for the next chance.", p + " vuelve a ir delante. Tranquilo, eres más rápido, espera la próxima." },
                new[] { "Das Manöver hat nicht gehalten, " + p + " kam zurück. Mehr Schwung aus der Kurve mitnehmen, {n}.", "The move didn't stick, " + p + " came back. Carry more speed out of the corner, {n}.", "La maniobra no aguantó, " + p + " volvió. Sal con más velocidad de la curva, {n}." },
                new[] { "Konter von " + p + ". Ruhig bleiben, der Zweikampf läuft noch.", "Counter from " + p + ". Stay calm, the fight's not over.", "Contraataque de " + p + ". Calma, la pelea sigue." }) : Pick(variant, name,
                new[] { "Direkt gekontert. Beim nächsten Mal den Ausgang sauberer treffen, {n}.", "Hit straight back. Get a cleaner exit next time, {n}.", "Respuesta inmediata. La próxima, mejor salida de curva, {n}." },
                new[] { "Gleich wieder verloren. Erst die Linie zumachen, dann ist der Platz sicher.", "Lost straight back. Close the line first, then the place is safe.", "Perdido otra vez. Primero cierra la trazada y el puesto es tuyo." },
                new[] { who + " ist wieder vorbei. Kein Problem, nächste Gelegenheit abwarten.", whoEn + "'s back ahead. No problem, wait for the next chance.", whoEs + " ha vuelto a pasar. Tranquilo, espera la próxima." },
                new[] { "Das Manöver hat nicht gehalten. Mehr Schwung aus der Kurve mitnehmen, {n}.", "The move didn't stick. Carry more speed out of the corner, {n}.", "La maniobra no aguantó. Sal con más velocidad de la curva, {n}." },
                new[] { "Konter bekommen. Ruhig bleiben, der Zweikampf läuft noch.", "Got countered. Stay calm, the fight's not over.", "Te han devuelto el adelantamiento. Calma, la pelea sigue." });
        }

        /// <summary>Den Platz gegen denselben Gegner zurückgeholt, der uns vorher überholt hatte.</summary>
        internal static string Retake(string name, string passed, int variant)
        {
            string p = string.IsNullOrWhiteSpace(passed) ? null : passed;
            return p != null ? Pick(variant, name,
                new[] { "Den hast du dir zurückgeholt, {n}! " + p + " ist wieder hinter dir.", "You got that one back, {n}! " + p + " is behind you again.", "¡Lo has recuperado, {n}! " + p + " vuelve a estar detrás." },
                new[] { "Revanche gelungen. " + p + " hatte dich vorhin, jetzt bist wieder du vorn, {n}.", "Payback done. " + p + " had you earlier, now you're back ahead, {n}.", "Revancha cumplida. " + p + " te pasó antes, ahora vuelves a ir delante, {n}." },
                new[] { "Platz zurück gegen " + p + ". Genau so antwortet man, {n}.", "Place back from " + p + ". That's how you answer, {n}.", "Puesto recuperado frente a " + p + ". Así se responde, {n}." },
                new[] { p + " ist wieder hinter dir. Stark gekontert, {n}!", p + " is behind you again. Strong counter, {n}!", p + " vuelve a estar detrás. ¡Gran respuesta, {n}!" },
                new[] { "Und " + p + " ist wieder eingesammelt. Jetzt sauber wegfahren, {n}.", "And " + p + " is reeled back in. Now pull clear cleanly, {n}.", "Y " + p + " otra vez cazado. Ahora escápate limpio, {n}." },
                new[] { "Zurückgeschlagen, {n}! Gegen " + p + " ist die Rechnung beglichen.", "Struck back, {n}! The score with " + p + " is settled.", "¡Devuelto, {n}! Cuentas saldadas con " + p + "." }) : Pick(variant, name,
                new[] { "Den hast du dir zurückgeholt, {n}! Der Platz ist wieder unser.", "You got that one back, {n}! The place is ours again.", "¡Lo has recuperado, {n}! El puesto vuelve a ser nuestro." },
                new[] { "Revanche gelungen, {n}. Der hatte dich vorhin, jetzt bist wieder du vorn.", "Payback done, {n}. He had you earlier, now you're back ahead.", "Revancha cumplida, {n}. Te pasó antes, ahora vuelves a ir delante." },
                new[] { "Platz zurückgeholt. Genau so antwortet man, {n}.", "Place taken back. That's how you answer, {n}.", "Puesto recuperado. Así se responde, {n}." },
                new[] { "Wieder vorbei am selben Auto. Stark gekontert, {n}!", "Back past the same car. Strong counter, {n}!", "Otra vez delante del mismo coche. ¡Gran respuesta, {n}!" },
                new[] { "Der ist wieder eingesammelt. Jetzt sauber wegfahren, {n}.", "He's reeled back in. Now pull clear cleanly, {n}.", "Otra vez cazado. Ahora escápate limpio, {n}." },
                new[] { "Zurückgeschlagen, {n}! Die Rechnung ist beglichen.", "Struck back, {n}! Score settled.", "¡Devuelto, {n}! Cuentas saldadas." });
        }

        /// <summary>Rob 18.11.: Viele Plätze in kurzer Zeit verloren – nachfragen, beruhigen, neu fokussieren.</summary>
        internal static string Dropped(string name, int lost, int variant)
        {
            string p = lost.ToString(RadioLanguage.Culture);
            return Pick(variant, name,
                new[] { "{n}, alles klar bei dir? Das waren gerade " + p + " Plätze. Konzentrier dich, die holen wir uns wieder.", "{n}, are you okay? That was " + p + " places just now. Focus, we'll get them back.", "{n}, ¿todo bien? Acabamos de perder " + p + " puestos. Concéntrate, los recuperaremos." },
                new[] { "Okay, {n}, " + p + " Plätze sind weg. Durchatmen, fokussieren, das Rennen ist noch lang.", "Okay, {n}, " + p + " places gone. Breathe, focus, there's plenty of race left.", "Vale, {n}, se han ido " + p + " puestos. Respira, céntrate, queda mucha carrera." },
                new[] { "{n}, bleib ruhig. " + p + " Plätze verloren, aber die Pace hast du. Eine saubere Runde nach der anderen.", "{n}, stay calm. " + p + " places lost, but you've got the pace. One clean lap after another.", "{n}, tranquilo. " + p + " puestos perdidos, pero tienes ritmo. Una vuelta limpia tras otra." },
                new[] { "Das hat " + p + " Plätze gekostet, {n}. Abhaken und konzentrieren, wir arbeiten uns zurück nach vorn.", "That cost us " + p + " places, {n}. Let it go and focus, we'll work our way back up.", "Eso nos ha costado " + p + " puestos, {n}. Olvídalo y céntrate, vamos a remontar." },
                new[] { "Alles okay, {n}? " + p + " Plätze in kurzer Zeit. Kopf frei machen, Bremspunkte sauber treffen, dann kommen sie zurück.", "All good, {n}? " + p + " places in a short time. Clear your head, hit your braking points, and they'll come back.", "¿Todo bien, {n}? " + p + " puestos en poco tiempo. Despeja la cabeza, clava las frenadas y volverán." },
                new[] { "{n}, nicht nervös werden. " + p + " Plätze sind aufholbar, wenn du jetzt sauber bleibst.", "{n}, don't get rattled. " + p + " places can be won back if you stay clean now.", "{n}, sin nervios. " + p + " puestos se pueden recuperar si ahora vas limpio." });
        }

        /// <summary>Nach einem Dreher oder Abflug ohne Kontakt, sobald das Auto wieder fährt.</summary>
        internal static string Spin(string name, int variant) => Pick(variant, name,
            new[] { "{n}, alles okay? Kurz sammeln, das kostet nur Zeit, nicht das Rennen.", "{n}, all okay? Gather yourself, that only costs time, not the race.", "{n}, ¿todo bien? Recupérate, eso solo cuesta tiempo, no la carrera." },
            new[] { "Passiert. Reifen erst wieder auf Temperatur bringen, dann wieder angreifen, {n}.", "It happens. Get the tyres back up to temperature first, then attack again, {n}.", "Pasa. Primero recupera la temperatura de los neumáticos y luego vuelve a atacar, {n}." },
            new[] { "Ausrutscher abhaken, {n}. Ruhig wieder reinfinden, die nächste Runde zählt.", "Put the slip behind you, {n}. Ease back in, the next lap is what counts.", "Olvida el error, {n}. Vuelve a entrar con calma, cuenta la próxima vuelta." },
            new[] { "Weiter geht's, {n}. Nicht gleich alles zurückholen wollen, erst mal Rhythmus.", "Off we go again, {n}. Don't try to get it all back at once, rhythm first.", "Seguimos, {n}. No intentes recuperarlo todo de golpe, primero el ritmo." },
            new[] { "Durchatmen. Das Auto ist heil, {n}, jetzt wieder Kurve für Kurve.", "Breathe. The car is fine, {n}, now corner by corner again.", "Respira. El coche está bien, {n}, ahora curva a curva otra vez." },
            new[] { "Kopf hoch, {n}. Jeder dreht sich mal, wichtig ist die Runde danach.", "Chin up, {n}. Everyone spins sometimes, the lap after is what matters.", "Ánimo, {n}. Todos hacemos un trompo alguna vez, lo importante es la vuelta siguiente." });

        /// <summary>Vorfallpunkte steigen schnell (iRacing u. a.): ruhig und sauber werden.</summary>
        internal static string IncidentsRising(string name, int added, int variant)
        {
            string a = added.ToString(RadioLanguage.Culture);
            return Pick(variant, name,
                new[] { "{n}, das sind " + a + " Vorfallpunkte in den letzten Minuten. Etwas ruhiger, sauber bleiben.", "{n}, that's " + a + " incident points in the last few minutes. Calm it down a bit, stay clean.", "{n}, son " + a + " puntos de incidente en los últimos minutos. Un poco más de calma, limpio." },
                new[] { "Die Vorfallpunkte werden mehr, plus " + a + ". Lieber eine Zehntel langsamer und dafür sauber, {n}.", "Incident points are adding up, plus " + a + ". Better a tenth slower and clean, {n}.", "Los puntos de incidente suben, más " + a + ". Mejor una décima más lento y limpio, {n}." },
                new[] { "Plus " + a + " Vorfallpunkte, {n}. Mehr Abstand zu den anderen und weg von den Randsteinen.", "Plus " + a + " incident points, {n}. More room to the others and off the kerbs.", "Más " + a + " puntos de incidente, {n}. Más espacio con los demás y fuera de los pianos." },
                new[] { "{n}, " + a + " neue Vorfallpunkte. Das kostet uns mehr als jede Position, also sauber fahren.", "{n}, " + a + " new incident points. That costs us more than any position, so drive clean.", "{n}, " + a + " puntos de incidente nuevos. Eso cuesta más que cualquier posición, así que limpio." },
                new[] { "Achtung bei den Vorfallpunkten, " + a + " in kurzer Zeit. Durchatmen und mit Reserve fahren, {n}.", "Watch the incident points, " + a + " in a short time. Breathe and drive with some margin, {n}.", "Cuidado con los puntos de incidente, " + a + " en poco tiempo. Respira y conduce con margen, {n}." });
        }

        /// <summary>Nach einer schwierigen Phase wieder im Tritt: das bestätigen.</summary>
        internal static string Recovered(string name, int variant) => Pick(variant, name,
            new[] { "Siehst du, {n}, wieder drin. Genau so weiter.", "See, {n}, back in it. Keep it exactly like that.", "¿Ves, {n}? Otra vez dentro. Sigue justo así." },
            new[] { "Gut gefangen, {n}. Das sieht wieder richtig gut aus.", "Well recovered, {n}. That looks really good again.", "Bien recuperado, {n}. Vuelve a tener muy buena pinta." },
            new[] { "Da ist der Rhythmus wieder. Stark, wie du das zurückgeholt hast, {n}.", "There's the rhythm again. Strong how you pulled that back, {n}.", "Ahí está el ritmo otra vez. Muy bien cómo lo has recuperado, {n}." },
            new[] { "{n}, so gefällt mir das. Ruhig, sauber, wieder auf Kurs.", "{n}, that's what I like. Calm, clean, back on track.", "{n}, así me gusta. Tranquilo, limpio, otra vez en camino." },
            new[] { "Wieder voll da, {n}. Jetzt bauen wir darauf auf.", "Fully back, {n}. Now we build on that.", "Otra vez a tope, {n}. Ahora construimos sobre eso." });

        /// <summary>Mitgefühl nach einem Einschlag oder Kontakt, sobald das Auto wieder fährt; beim zweiten Treffer kurz hintereinander deutlicher.</summary>
        internal static string Impact(string name, bool repeated, int variant)
        {
            if (repeated)
                return Pick(variant, name,
                    new[] { "{n}, das ist schon der zweite Treffer. Ein bisschen Tempo raus, wir brauchen das Auto im Ziel.", "{n}, that's the second hit already. Ease off a touch, we need the car at the finish.", "{n}, ya es el segundo golpe. Levanta un poco, necesitamos el coche en la meta." },
                    new[] { "Noch einer. Ganz ruhig jetzt, {n}, erst mal ein paar saubere Runden.", "Another one. Calm down now, {n}, just a few clean laps first.", "Otro más. Tranquilo ahora, {n}, primero unas vueltas limpias." },
                    new[] { "Wieder Kontakt, {n}. Lass den anderen jetzt mehr Platz, das Rennen ist lang genug.", "Contact again, {n}. Give the others more room now, the race is long enough.", "Otro contacto, {n}. Deja más espacio a los demás, la carrera es larga." },
                    new[] { "Das Auto hat schon genug abbekommen, {n}. Ab jetzt mit Köpfchen, nicht mit der Brechstange.", "The car has taken enough already, {n}. Use your head from here, not the crowbar.", "El coche ya ha sufrido bastante, {n}. Ahora con cabeza, no a lo bruto." },
                    new[] { "Zweimal kurz hintereinander, das ist ein Zeichen. Atme durch und fahr wieder mit Abstand.", "Twice in a short time, that's a sign. Take a breath and drive with some margin again.", "Dos veces en poco tiempo, es una señal. Respira y vuelve a conducir con margen." });
            return Pick(variant, name,
                // Die erste Frage „Alles okay?“ stellt schon die Kontaktmeldung; Ben schaut danach nach vorn.
                new[] { "Durchatmen, {n}. Das Auto fährt noch, jetzt wieder in den Rhythmus.", "Breathe, {n}. The car is still going, now back into the rhythm.", "Respira, {n}. El coche sigue rodando, ahora a recuperar el ritmo." },
                new[] { "Das war unglücklich. Abhaken, Kopf frei machen, nächste Kurve.", "That was unlucky. Let it go, clear your head, next corner.", "Mala suerte. Olvídalo, cabeza despejada, siguiente curva." },
                new[] { "Kurz durchschütteln und weiter, {n}. Wir behalten das Auto im Blick.", "Shake it off and carry on, {n}. We're keeping an eye on the car.", "Sacúdetelo y sigue, {n}. Vigilamos el coche." },
                new[] { "Ärgerlich, ich weiß. Jetzt nicht nachlegen wollen, einfach sauber weiterfahren.", "Annoying, I know. Don't try to make it up all at once, just drive clean.", "Fastidia, lo sé. No intentes recuperarlo todo de golpe, conduce limpio." },
                new[] { "Konzentrier dich auf die nächste Runde, {n}, der Rest kommt von allein.", "Focus on the next lap, {n}, the rest will follow.", "Céntrate en la próxima vuelta, {n}, lo demás llegará." });
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
        private Pending overtake, countered;
        internal static readonly TimeSpan CounterWindow = TimeSpan.FromSeconds(20);
        private DateTime impactUtc = DateTime.MinValue;
        private bool impactRepeated;
        private int lastIncidents = -1;
        // Rob 18.11.: schneller Platzverlust, Dreher, steigende Vorfallpunkte, wieder im Tritt.
        internal static readonly TimeSpan DropWindow = TimeSpan.FromSeconds(45), DropSettle = TimeSpan.FromSeconds(5), IncidentWindow = TimeSpan.FromMinutes(3);
        internal const int DropPlaces = 3, IncidentStep = 4;
        private readonly Queue<KeyValuePair<DateTime, int>> positions = new Queue<KeyValuePair<DateTime, int>>();
        private readonly Queue<KeyValuePair<DateTime, int>> incidentHistory = new Queue<KeyValuePair<DateTime, int>>();
        private DateTime lastPositionChange = DateTime.MinValue, lastDropCall = DateTime.MinValue, spinUtc = DateTime.MinValue, lastSpinCall = DateTime.MinValue, lastIncidentCall = DateTime.MinValue;
        private int frustratedLap = -100;
        private float lastImpactTime = -1;
        private bool impactBaseline;
        private DateTime impactCandidateUtc = DateTime.MinValue;
        private readonly Queue<KeyValuePair<DateTime, float>> speeds = new Queue<KeyValuePair<DateTime, float>>();
        private readonly Queue<KeyValuePair<DateTime, float>> damage = new Queue<KeyValuePair<DateTime, float>>();
        private ObservedRaceCall call;

        private sealed class Pending
        {
            public DateTime Utc; public int Position, Incidents; public float Damage; public string PassedName, PassedIdentity; public bool Stronger, Retake, Rough;
        }
        // RRCA 2.0 (4.2) Rivalengeschichte: wer uns zuletzt überholt hat. Holt Rob sich den Platz gegen denselben Gegner zurück,
        // ist das eine Geschichte („den hast du dir zurückgeholt“), nicht nur ein weiteres Überholen.
        internal static readonly TimeSpan RetakeWindow = TimeSpan.FromMinutes(10);
        private readonly Dictionary<string, DateTime> passedBy = new Dictionary<string, DateTime>(StringComparer.Ordinal);

        internal DriverJudgementWatch(int? seed = null) { random = seed.HasValue ? new Random(seed.Value) : new Random(); }

        internal void Reset()
        {
            session = int.MinValue; lastPosition = -1; lastConsistencyLap = lastSlideLap = lastDefenceLap = -100; overtakeCount = spokenCount = 0;
            defendedAttacker = null; overtake = null; passedBy.Clear(); impactUtc = DateTime.MinValue; impactRepeated = false; lastIncidents = -1; damage.Clear();
            lastImpactTime = -1; impactBaseline = false; impactCandidateUtc = DateTime.MinValue; speeds.Clear();
            lastPraise = lastAny = lastImpact = lastImpactCall = DateTime.MinValue; lastVariant.Clear(); call = null;
            positions.Clear(); incidentHistory.Clear(); frustratedLap = -100; lastGrade = null; lastGradeLap = -100;
            lastPositionChange = lastDropCall = spinUtc = lastSpinCall = lastIncidentCall = DateTime.MinValue;
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
            ObservePositions(gs, now, race && green);
            ObserveSpin(gs, now, green);
            ObserveIncidents(s, now);
            if (call != null || !green) return;
            string name = Name(radioName);

            // Viele Plätze in kurzer Zeit verloren: erst wenn sich die Reihenfolge kurz beruhigt hat.
            int dropped = race && s.CompletedLaps >= 1 ? LostInWindow(s.ClassPosition) : 0;
            // LMU 21.11.: Rad ab, Rob auf dem Weg zur Box – „Das waren gerade 8 Plätze, die holen wir uns wieder“ passte nicht.
            // Nach schwerem Schaden erklärt die Schadensmeldung den Platzverlust; Ansporn gibt es dann keinen.
            if (dropped >= DropPlaces && IncidentWatch.IsSerious(gs.CarDamageData))
            {
                LiveDiagnostics.Decision("judge-dropped", "suppressed", "serious_damage_explains_the_loss", null);
                lastDropCall = now; positions.Clear();
            }
            else if (dropped >= DropPlaces && now - lastPositionChange >= DropSettle && now - lastDropCall > TimeSpan.FromMinutes(3))
            {
                Offer(gs, now, "judge-dropped", DriverJudgement.Dropped(name, dropped, Variant("dropped", 6)), "class_position_lost_" + dropped + "_within_45s_without_own_stop", 5, TimeSpan.FromSeconds(40));
                lastDropCall = now; positions.Clear();
                if (laps != null && laps.Count > 0) lastSlideLap = laps[laps.Count - 1].LapNumber + 1; // nicht gleich noch einmal als Rundenbilanz
                return;
            }
            // Dreher oder Abflug ohne Kontakt: wenn das Auto wieder Fahrt aufnimmt.
            if (spinUtc != DateTime.MinValue)
            {
                if (now - spinUtc > TimeSpan.FromSeconds(40) || now - lastImpact < TimeSpan.FromSeconds(15)) spinUtc = DateTime.MinValue;
                else if (now - spinUtc >= TimeSpan.FromSeconds(5) && gs.PositionAndMotionData.CarSpeed > 15 && now - lastSpinCall > TimeSpan.FromMinutes(2))
                {
                    Offer(gs, now, "judge-spin", DriverJudgement.Spin(name, Variant("spin", 6)), "speed_from_above_20_to_below_3_mps_within_3s_no_impact", 5, TimeSpan.FromSeconds(25));
                    lastSpinCall = now; spinUtc = DateTime.MinValue;
                    return;
                }
            }
            // Vorfallpunkte steigen schnell (ohne den Kontakt, den schon das Mitgefühl abdeckt).
            int added = IncidentsAdded(s);
            if (added >= IncidentStep && now - lastIncidentCall > TimeSpan.FromMinutes(5) && now - lastImpactCall > TimeSpan.FromSeconds(60))
            {
                Offer(gs, now, "judge-incidents", DriverJudgement.IncidentsRising(name, added, Variant("incidents", 5)), "incident_points_plus_" + added + "_within_3_minutes", 4, TimeSpan.FromSeconds(30));
                lastIncidentCall = now; incidentHistory.Clear();
                return;
            }

            // Mitgefühl nach einem Einschlag: erst wenn das Auto wieder rollt, nicht im Moment selbst.
            if (impactUtc != DateTime.MinValue)
            {
                if (now - impactUtc > TimeSpan.FromSeconds(30)) impactUtc = DateTime.MinValue;
                else if (now - impactUtc >= TimeSpan.FromSeconds(8) && gs.PositionAndMotionData.CarSpeed > 25 && now - lastImpactCall > TimeSpan.FromSeconds(90))
                {
                    Offer(gs, now, "judge-impact", DriverJudgement.Impact(name, impactRepeated, Variant(impactRepeated ? "impact2" : "impact", 5)),
                        "damage_or_incident_increase_then_moving_again", 5, TimeSpan.FromSeconds(20));
                    lastImpactCall = now; impactUtc = DateTime.MinValue;
                    return;
                }
            }

            if (countered != null && (!race || now - countered.Utc > TimeSpan.FromSeconds(60))) countered = null;
            if (countered != null && now - lastAny >= AnyGap)
            {
                var o = countered; countered = null;
                Offer(gs, now, "judge-overtake-countered", DriverJudgement.Countered(name, o.PassedName, Variant("countered", 5)),
                    "same_rival_retook_the_place_within_20s", 4, TimeSpan.FromSeconds(20));
                return;
            }
            if (overtake != null && race && now - lastAny >= AnyGap && now - lastPraise >= TimeSpan.FromSeconds(60) && now - overtake.Utc >= TimeSpan.FromSeconds(15))
            {
                var o = overtake; overtake = null;
                bool clean = s.ClassPosition > 0 && s.ClassPosition <= o.Position && (o.Incidents < 0 || Incidents(s) == o.Incidents) &&
                    (o.Damage < 0 || ImpactDamage(gs.CarDamageData) - o.Damage < 1f) && now - o.Utc < TimeSpan.FromSeconds(45);
                bool hardFought = laps != null && o.PassedIdentity != null && laps.Reverse().Take(3)
                    .Any(l => l.FrontIdentity == o.PassedIdentity && l.GapAhead > 0 && l.GapAhead < 1.2f);
                // Platz gegen denselben Gegner zurückgeholt: immer erwähnenswert.
                if (clean && o.Retake)
                {
                    passedBy.Remove(o.PassedIdentity);
                    Offer(gs, now, "judge-retake", DriverJudgement.Retake(name, o.PassedName, Variant("retake", 6)),
                        "class_position_regained_against_the_rival_who_passed_us_held_without_new_damage", 4, TimeSpan.FromSeconds(25));
                    lastPraise = now;
                    return;
                }
                // Nicht jedes Überholen braucht ein Lob – nur hart erarbeitete, gegen Schnellere oder jedes dritte saubere.
                // Rob 21.11.: „auf gute oder weniger gute Überholmanöver reagieren“. Vorbei und gehalten, aber mit neuem Schaden
                // oder neuen Vorfallpunkten: ein ehrlicher Hinweis statt Lob.
                bool held = s.ClassPosition > 0 && s.ClassPosition <= o.Position && now - o.Utc < TimeSpan.FromSeconds(45);
                bool rough = held && (o.Rough || o.Incidents >= 0 && Incidents(s) > o.Incidents || o.Damage >= 0 && ImpactDamage(gs.CarDamageData) - o.Damage >= 1f);
                if (rough && !IncidentWatch.IsSerious(gs.CarDamageData))
                {
                    Offer(gs, now, "judge-overtake-rough", DriverJudgement.RoughPass(name, o.PassedName, Variant("overtake-rough", 5)),
                        "position_gain_with_new_damage_or_incident_points", 4, TimeSpan.FromSeconds(25));
                    lastPraise = now;
                    return;
                }
                // Rob 21.11.: Jedes saubere, gehaltene Überholmanöver bekommt eine Reaktion (höchstens eine pro Minute).
                if (clean)
                {
                    overtakeCount++;
                    float next = s.TimeDeltaFront > 0 ? s.TimeDeltaFront : -1;
                    Offer(gs, now, "judge-overtake", DriverJudgement.Overtake(name, o.PassedName, hardFought, o.Stronger, next, Variant("overtake", 9)),
                        "position_gain_held_without_new_damage_or_incidents", 4, TimeSpan.FromSeconds(25));
                    lastPraise = now;
                    return;
                }
            }

            if (!lapCompleted || laps == null || laps.Count == 0) return;
            int lap = laps[laps.Count - 1].LapNumber;
            // Wieder im Tritt nach einer frustrierenden Phase (Rennstimmung des Rennkopfs).
            var mind = RaceMind.Latest;
            if (race && mind != null && mind.SessionId == s.SessionId && mind.Fresh(now))
            {
                if (mind.Mood == DriverMoodState.Frustration) frustratedLap = lap;
                else if ((mind.Mood == DriverMoodState.Flow || mind.Mood == DriverMoodState.Neutral) && frustratedLap > 0 && lap - frustratedLap <= 6 &&
                    laps[laps.Count - 1].Valid && now - lastAny >= AnyGap)
                {
                    frustratedLap = -100;
                    Offer(gs, now, "judge-recovered", DriverJudgement.Recovered(name, Variant("recovered", 5)), "race_mind_mood_back_from_frustration_valid_lap", 3, TimeSpan.FromSeconds(30));
                    lastPraise = now;
                    return;
                }
            }
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
                    return;
                }
            }
            // Rob 20.11.: Bewertung der eigenen Runde – höchstens alle zwei Runden, gleiche Note erst nach fünf Runden wieder.
            if (race && s.CompletedLaps >= 3)
            {
                var grade = LapGrade.Of(laps, out float behindBest);
                if (grade.HasValue && (grade != lastGrade && lap - lastGradeLap >= 2 || lap - lastGradeLap >= 5))
                {
                    lastGrade = grade; lastGradeLap = lap;
                    Offer(gs, now, "judge-lap", LapGrade.Text(grade.Value, behindBest, name), "clean_lap_vs_own_best_" + grade.Value + "_" + behindBest.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture), 2, TimeSpan.FromSeconds(25));
                    if (grade.Value <= LapGrade.Grade.Good) lastPraise = now;
                }
            }
        }

        private LapGrade.Grade? lastGrade;
        private int lastGradeLap = -100;

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
            // Einschlag-Zeitpunkt, den Crew Chief je Simulation schon pflegt (LMU/rF2: Zeitpunkt des letzten Einschlags aus der
            // Telemetrie; AMS2: Kollision über Schwelle; iRacing: Reparatur nötig). Damit nicht jeder Kerb-Kontakt Mitgefühl
            // auslöst, zählt er erst mit spürbarem Tempoverlust (≥ 5 m/s binnen 2 s) oder neuem Schaden.
            float impactTime = gs.CarDamageData == null ? -1 : gs.CarDamageData.LastImpactTime;
            if (impactBaseline && impactTime > 0 && impactTime != lastImpactTime) impactCandidateUtc = now;
            lastImpactTime = impactTime; impactBaseline = true; // erster Wert ist nur Ausgangslage (Einschlag vor Sitzungsbeginn)
            float speed = gs.PositionAndMotionData.CarSpeed;
            while (speeds.Count > 0 && now - speeds.Peek().Key > TimeSpan.FromSeconds(3)) speeds.Dequeue();
            speeds.Enqueue(new KeyValuePair<DateTime, float>(now, speed));
            if (impactCandidateUtc != DateTime.MinValue)
            {
                if (now - impactCandidateUtc > TimeSpan.FromSeconds(2)) impactCandidateUtc = DateTime.MinValue;
                else if (SpeedLoss(speeds, impactCandidateUtc) >= 5f) { hit = true; impactCandidateUtc = DateTime.MinValue; }
            }
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
            // Kontakt direkt beim Überholen: kein Mitgefühl, sondern später ein ehrlicher Satz zum Manöver.
            if (overtake != null && now >= overtake.Utc && now - overtake.Utc < TimeSpan.FromSeconds(15) && !IncidentWatch.IsSerious(gs.CarDamageData))
                overtake.Rough = true;
            else { impactUtc = now; overtake = null; }
            lastImpact = now; damage.Clear(); impactCandidateUtc = DateTime.MinValue; spinUtc = DateTime.MinValue;
        }

        private void ObservePositions(GameStateData gs, DateTime now, bool racingGreen)
        {
            int pos = gs.SessionData.ClassPosition;
            if (!racingGreen || pos <= 0 || gs.PitData.InPitlane || gs.PitData.OnOutLap) { positions.Clear(); return; } // eigener Stopp ist kein Platzverlust
            if (positions.Count > 0 && positions.Last().Value != pos) lastPositionChange = now;
            while (positions.Count > 0 && (now < positions.Peek().Key || now - positions.Peek().Key > DropWindow)) positions.Dequeue();
            if (positions.Count == 0 || positions.Last().Value != pos || now - positions.Last().Key > TimeSpan.FromSeconds(1))
                positions.Enqueue(new KeyValuePair<DateTime, int>(now, pos));
        }

        /// <summary>Plätze, die im Fenster der letzten 45 s verloren gingen (aktueller Platz minus bester Platz im Fenster).</summary>
        private int LostInWindow(int pos) => pos <= 0 || positions.Count == 0 ? 0 : pos - positions.Min(p => p.Value);

        private void ObserveSpin(GameStateData gs, DateTime now, bool green)
        {
            if (!green) return;
            float speed = gs.PositionAndMotionData.CarSpeed;
            // „speeds“ hält die letzten 3 s (siehe ObserveImpact): von über 20 m/s auf unter 3 m/s ohne Box = Dreher oder Abflug.
            if (spinUtc == DateTime.MinValue && speed < 3 && speeds.Count > 0 && speeds.Max(p => p.Value) > 20 && now - lastImpact > TimeSpan.FromSeconds(15))
                spinUtc = now;
        }

        private void ObserveIncidents(SessionData s, DateTime now)
        {
            int incidents = Incidents(s);
            if (incidents < 0) return;
            while (incidentHistory.Count > 0 && (now < incidentHistory.Peek().Key || now - incidentHistory.Peek().Key > IncidentWindow)) incidentHistory.Dequeue();
            if (incidentHistory.Count == 0 || incidentHistory.Last().Value != incidents) incidentHistory.Enqueue(new KeyValuePair<DateTime, int>(now, incidents));
        }

        private int IncidentsAdded(SessionData s)
        {
            int incidents = Incidents(s);
            return incidents < 0 || incidentHistory.Count == 0 ? 0 : incidents - incidentHistory.Min(p => p.Value);
        }

        /// <summary>Höchstes Tempo kurz vor dem Einschlag minus geringstes Tempo danach (m/s).</summary>
        internal static float SpeedLoss(IEnumerable<KeyValuePair<DateTime, float>> samples, DateTime impact)
        {
            var list = samples.ToList();
            var before = list.Where(p => p.Key <= impact && impact - p.Key <= TimeSpan.FromSeconds(1)).Select(p => p.Value).ToList();
            var after = list.Where(p => p.Key >= impact).Select(p => p.Value).ToList();
            return before.Count == 0 || after.Count == 0 ? 0 : before.Max() - after.Min();
        }

        private void ObserveOvertake(GameStateData gs, IList<LapRecord> laps, bool names, DateTime now, bool green)
        {
            var s = gs.SessionData;
            int pos = s.ClassPosition;
            if (pos <= 0) { lastPosition = -1; return; }
            int before = lastPosition; lastPosition = pos;
            if (before <= 0) return;
            if (overtake != null && pos > overtake.Position)
            {
                // Platz gleich wieder verloren. Vom selben Auto kurz danach: ein Konter, auf den die Crew reagiert.
                var front = RaceDataTools.Neighbour(gs, true);
                var frontKey = front == null ? null : gs.OpponentData.FirstOrDefault(p => ReferenceEquals(p.Value, front)).Key;
                if (front != null && frontKey != null && overtake.PassedIdentity != null && RaceReader.Identity(frontKey, front) == overtake.PassedIdentity &&
                    now >= overtake.Utc && now - overtake.Utc < CounterWindow && green && !gs.PitData.InPitlane)
                    countered = overtake;
                overtake = null;
            }
            if (pos == before + 1 && green && !gs.PitData.InPitlane)
            {
                // Ein Platz verloren: Wer steht jetzt direkt vor uns? Nur ein Gegner auf der Strecke (kein eigener Stopp).
                var by = RaceDataTools.Neighbour(gs, true);
                var byKey = by == null ? null : gs.OpponentData.FirstOrDefault(p => ReferenceEquals(p.Value, by)).Key;
                if (by != null && !by.InPits && byKey != null)
                {
                    if (passedBy.Count > 32) passedBy.Clear();
                    passedBy[RaceReader.Identity(byKey, by)] = now;
                }
            }
            if (pos != before - 1 || !green || s.CompletedLaps < 1) return;
            var passed = RaceDataTools.Neighbour(gs, false);
            if (passed == null || passed.InPits || passed.isEnteringPits()) return; // Platz durch Boxenstopp des Gegners ist kein Überholmanöver
            // LMU 18.11.: „du bist trotzdem vorbei“ – Rob hatte niemanden überholt, der Gegner stand weit weg (Garage/Unfall).
            // Lob nur, wenn der Überholte jetzt wirklich auf der Strecke dicht hinter uns fährt.
            if (!PassedOnTrack(gs, passed)) return;
            var key = gs.OpponentData.FirstOrDefault(p => ReferenceEquals(p.Value, passed)).Key;
            float mine = s.PlayerLapTimeSessionBest;
            overtake = new Pending
            {
                Utc = now, Position = pos, Incidents = Incidents(s), Damage = ImpactDamage(gs.CarDamageData),
                PassedName = names && passed.CanUseName ? RadioPhraseCatalog.SafeRivalName(passed.DriverRawName) : null,
                PassedIdentity = key == null ? null : RaceReader.Identity(key, passed),
                Stronger = passed.CurrentBestLapTime > 0 && mine > 0 && passed.CurrentBestLapTime < mine - .1f
            };
            if (overtake.PassedIdentity != null && passedBy.TryGetValue(overtake.PassedIdentity, out var lostAt) && now >= lostAt && now - lostAt < RetakeWindow)
                overtake.Retake = true;
            // Kontakt kurz vor dem Platzwechsel gehört zum Manöver: statt Mitgefühl später ein Satz zum Überholen.
            if (lastImpact != DateTime.MinValue && now >= lastImpact && now - lastImpact < TimeSpan.FromSeconds(5) && !IncidentWatch.IsSerious(gs.CarDamageData))
            { overtake.Rough = true; impactUtc = DateTime.MinValue; }
        }

        internal const float PassEvidenceMetres = 300;

        /// <summary>Der Überholte liegt in der Renndistanz knapp hinter uns und fährt (kein Platzgewinn durch Ausfall oder Garage).</summary>
        internal static bool PassedOnTrack(GameStateData gs, OpponentData passed)
        {
            float length = gs?.SessionData.TrackDefinition?.trackLength ?? 0;
            if (passed == null || length <= 100 || passed.Speed < 5 || passed.DistanceRoundTrack < 0) return false;
            double own = (double)gs.SessionData.CompletedLaps * length + gs.PositionAndMotionData.DistanceRoundTrack;
            double rival = (double)passed.CompletedLaps * length + passed.DistanceRoundTrack;
            double behind = own - rival;
            return behind > 0 && behind <= PassEvidenceMetres;
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

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Bewertung der eigenen Runde im Rennen (Rob 20.11.2026: „Kommentare für sehr gute, gute, mittelmäßige, mäßige und schlechte
    /// Aktionen haben gefehlt“). Maßstab ist Robs eigene persönliche Bestzeit dieses Rennens aus sauberen Runden; Box-, Out- und
    /// ungültige Runden zählen nicht. Neue persönliche Bestzeit meldet bereits die Rundenzeit-Ansage.
    /// </summary>
    internal static class LapGrade
    {
        internal enum Grade { VeryGood, Good, Medium, Weak, Bad }

        private static bool Clean(LapRecord l) => l != null && l.Valid && !l.InPitLap && l.LapTime > 10 && l.LapTime < 3600;

        /// <summary>Note der letzten Runde gegen die bisher beste saubere Runde; null ohne Vergleich oder bei neuer Bestzeit / Ausreißer.</summary>
        internal static Grade? Of(IList<LapRecord> laps, out float delta)
        {
            delta = 0;
            if (laps == null || laps.Count < 3) return null;
            var last = laps[laps.Count - 1];
            if (!Clean(last)) return null;
            var earlier = laps.Take(laps.Count - 1).Where(Clean).ToList();
            if (earlier.Count < 2) return null;
            float best = earlier.Min(l => l.LapTime);
            delta = last.LapTime - best;
            if (delta < 0) return null;            // neue persönliche Bestzeit: eigene Ansage
            if (delta > 15) return null;           // Zwischenfall oder Verkehr: sagen die anderen Meldungen
            return delta <= .15f ? Grade.VeryGood : delta <= .5f ? Grade.Good : delta <= 1.2f ? Grade.Medium : delta <= 3f ? Grade.Weak : Grade.Bad;
        }

        internal static string Text(Grade grade, float delta, string name)
        {
            string n = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            string d = GermanRadioText.Interval(delta);
            string[] o;
            switch (grade)
            {
                case Grade.VeryGood:
                    o = new[] {
                        RadioLanguage.T("Sehr starke Runde, nur " + d + " über deiner persönlichen Bestzeit.", "Very strong lap, only " + d + " off your personal best.", "Vuelta muy fuerte, solo " + d + " por encima de tu mejor personal."),
                        RadioLanguage.T("Klasse, das war fast deine persönliche Bestzeit.", "Great, that was almost your personal best.", "Genial, casi tu mejor vuelta personal."),
                        RadioLanguage.T("Top Runde! Genau so, " + d + " an der Bestmarke.", "Top lap! Just like that, " + d + " from your mark.", "¡Vuelta top! Así, a " + d + " de tu marca."),
                        RadioLanguage.T("Das sitzt. Sauber und schnell, so wollen wir das.", "That's spot on. Clean and quick, that's what we want.", "Eso es. Limpio y rápido, así lo queremos."),
                        RadioLanguage.T("Hervorragend gefahren, die Pace ist voll da.", "Superbly driven, the pace is fully there.", "Excelente, el ritmo está ahí.") };
                    break;
                case Grade.Good:
                    o = new[] {
                        RadioLanguage.T("Gute Runde, " + d + " über deiner persönlichen Bestzeit.", "Good lap, " + d + " off your personal best.", "Buena vuelta, " + d + " por encima de tu mejor personal."),
                        RadioLanguage.T("Ordentlich, der Rhythmus stimmt.", "Solid, the rhythm is right.", "Bien, el ritmo es bueno."),
                        RadioLanguage.T("Gute Pace, nur " + d + " zur Bestmarke.", "Good pace, just " + d + " to your mark.", "Buen ritmo, solo " + d + " a tu marca."),
                        RadioLanguage.T("Saubere Runde. Weiter so.", "Clean lap. Keep it up.", "Vuelta limpia. Sigue así."),
                        RadioLanguage.T("Passt, gleichmäßig und schnell.", "Good, consistent and quick.", "Bien, constante y rápido.") };
                    break;
                case Grade.Medium:
                    o = new[] {
                        RadioLanguage.T("Ganz okay, aber " + d + " liegen noch drin.", "Okay, but there's " + d + " still in it.", "Correcto, pero aún quedan " + d + "."),
                        RadioLanguage.T("Mittelmäßige Runde, " + d + " langsamer als deine persönliche Bestzeit.", "Average lap, " + d + " slower than your personal best.", "Vuelta normal, " + d + " más lenta que tu mejor personal."),
                        RadioLanguage.T("Solide, aber nicht ganz am Limit. " + d + " fehlen.", "Solid, but not quite on the limit. " + d + " missing.", "Sólida, pero no al límite. Faltan " + d + "."),
                        RadioLanguage.T(d + " verschenkt. Konzentrier dich auf die Bremspunkte.", d + " given away. Focus on your braking points.", d + " regalados. Céntrate en las frenadas."),
                        RadioLanguage.T("Geht noch besser, da sind " + d + " drin.", "There's more to come, " + d + " in it.", "Se puede mejorar, hay " + d + ".") };
                    break;
                case Grade.Weak:
                    o = new[] {
                        RadioLanguage.T("Die war schwächer, " + d + " langsamer. Ruhig bleiben, Rhythmus finden.", "That one was weaker, " + d + " slower. Stay calm, find the rhythm.", "Esa fue más floja, " + d + " más lenta. Tranquilo, busca el ritmo."),
                        RadioLanguage.T(d + " verloren. Nicht hetzen, saubere Linien.", d + " lost. Don't rush, clean lines.", d + " perdidos. Sin prisa, trazadas limpias."),
                        RadioLanguage.T("Mäßige Runde. Hak sie ab, die nächste zählt.", "So-so lap. Let it go, the next one counts.", "Vuelta regular. Olvídala, la siguiente cuenta."),
                        RadioLanguage.T("Da ging Zeit liegen, " + d + ". Zurück zur Basis.", "Time went missing there, " + d + ". Back to basics.", "Ahí se fue tiempo, " + d + ". Vuelve a lo básico."),
                        RadioLanguage.T("Etwas zäh, " + d + " über der Bestmarke. Kopf frei, weiter.", "A bit sticky, " + d + " off your mark. Clear head, carry on.", "Algo lenta, " + d + " sobre tu marca. Cabeza fría, sigue.") };
                    break;
                default:
                    o = new[] {
                        RadioLanguage.T("Das war keine gute Runde, " + d + " verloren. Durchatmen und neu sortieren.", "That wasn't a good lap, " + d + " lost. Breathe and reset.", "No fue una buena vuelta, " + d + " perdidos. Respira y reordena."),
                        RadioLanguage.T(d + " langsamer als deine persönliche Bestzeit. Ruhig, Schritt für Schritt zurück.", d + " slower than your personal best. Easy, step by step back.", d + " más lenta que tu mejor personal. Calma, paso a paso."),
                        RadioLanguage.T("Schlechte Runde. Abhaken, die nächste wird besser.", "Bad lap. Forget it, the next will be better.", "Mala vuelta. Olvídala, la próxima será mejor."),
                        RadioLanguage.T("Viel Zeit liegen gelassen, " + d + ". Erst sauber, dann schnell.", "A lot of time dropped, " + d + ". Clean first, then quick.", "Mucho tiempo perdido, " + d + ". Primero limpio, luego rápido."),
                        RadioLanguage.T("Die ging daneben. Konzentration, saubere Runde jetzt.", "That one went wrong. Focus, clean lap now.", "Esa salió mal. Concentración, vuelta limpia ahora.") };
                    break;
            }
            string text = RadioVariants.Pick("lap-grade-" + grade, o);
            return n != null && grade <= Grade.Good ? RadioPhraseCatalog.Address(text, n) : text;
        }
    }
}
