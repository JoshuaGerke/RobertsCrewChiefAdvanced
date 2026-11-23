using System;
using System.Collections.Generic;
using System.Linq;

namespace CrewChiefV4.AiTeam.Field
{
    /// <summary>
    /// Zusammenfassung in der Box nach Training und Quali (Rob 21.11.2026: „Wo bleibt die Zusammenfassung nach dem Practice …
    /// Setz um, was wir trainiert haben“; nach der Quali: „wie viel ich verloren habe auf die vorderen drei, wie weit weg von den
    /// Top 5 oder Top 1“). Nur Gemessenes aus dem Lagebild und dem Spritgedächtnis; gestrichene Zeiten werden als solche genannt.
    /// </summary>
    internal static class SessionDebrief
    {
        internal const int Variants = 5;

        private static string Gap(float s) => RadioLanguage.Num(s, "0.0") + RadioLanguage.T(" Sekunden", " seconds", " segundos");

        /// <summary>
        /// RaceRoom Hockenheim 23.11. (Rob nach dem Ziel): „Die ersten drei mit Namen, die Zeitabstände … die Höchstgeschwindigkeit auf
        /// den Geraden kein einziges Mal erwähnt.“ Rennbilanz nach der Zielflagge: Podium mit Namen und Abständen (nur wenn gleiche
        /// Runde und gemessen), eigener Platz gegen den Startplatz, Überholmanöver auf der Strecke, schnellste Rennrunde mit Namen,
        /// die eigene Bestzeit mit Rang und das Höchsttempo gegen das schnellste Auto im Feld.
        /// </summary>
        internal static string ComposeRace(FieldView v, string driverName, IReadOnlyList<ChronicleEntry> chronicle, int variant)
        {
            chronicle = chronicle ?? new List<ChronicleEntry>();
            int startPlace = chronicle.Where(e => e.Kind == ChronicleKind.Grid && e.Position > 0).Select(e => e.Position).FirstOrDefault();
            int passesMade = chronicle.Count(e => e.Kind == ChronicleKind.PositionGained && e.Detail == ChronicleRecorder.OnTrack);
            int passesLost = chronicle.Count(e => e.Kind == ChronicleKind.PositionLost && e.Detail == ChronicleRecorder.OnTrack);
            if (v?.Player == null || v.Session != FieldSession.Race || v.Cars == null) return null;
            var p = v.Player;
            int vv = Math.Abs(variant) % Variants;
            string name = string.IsNullOrWhiteSpace(driverName) ? "Rob" : driverName.Trim();
            var cls = v.Cars.Where(c => c.ClassId == p.ClassId).ToList();
            int Own(FieldCarView c) => c.PlaceClass > 0 ? c.PlaceClass : c.Place;
            int place = Own(p);
            var parts = new List<string>();
            parts.Add(RadioLanguage.Pick(
                new[] { name + ", das Rennen in Zahlen.", "So, " + name + ", die Rennbilanz.", name + ", kurz zum Rennen.", name + ", hier die Auswertung vom Rennen.", "Rennen vorbei, " + name + ". Die Bilanz." },
                new[] { name + ", the race in numbers.", "Right, " + name + ", race review.", name + ", quick word on the race.", name + ", here's the race debrief.", "Race over, " + name + ". The summary." },
                new[] { name + ", la carrera en cifras.", "Bien, " + name + ", balance de la carrera.", name + ", un momento sobre la carrera.", name + ", aquí el resumen de la carrera.", "Carrera terminada, " + name + ". El balance." })[vv]);

            var podium = cls.Where(c => Own(c) >= 1 && Own(c) <= 3).OrderBy(Own).ToList();
            var winner = podium.FirstOrDefault(c => Own(c) == 1);
            if (winner != null && !string.IsNullOrWhiteSpace(winner.Name))
            {
                string Behind(FieldCarView c)
                {
                    if (string.IsNullOrWhiteSpace(c.Name)) return null;
                    bool measured = c.CompletedLaps == winner.CompletedLaps && !float.IsNaN(c.GapS) && !float.IsNaN(winner.GapS) && winner.GapS - c.GapS > 0 &&
                        winner.GapS - c.GapS < 120;
                    return measured ? c.Name + RadioLanguage.T(" mit " , " at ", " a ") + Gap(winner.GapS - c.GapS) : c.Name;
                }
                if (winner.IsPlayer)
                    parts.Add(RadioLanguage.T("Du hast gewonnen!", "You won it!", "¡Has ganado!"));
                else
                    parts.Add(RadioLanguage.T("Gewonnen hat " + winner.Name + ".", "The win went to " + winner.Name + ".", "Ganó " + winner.Name + "."));
                var rest = podium.Where(c => Own(c) > 1).Select(c => c.IsPlayer ? RadioLanguage.T("du", "you", "tú") + (Behind(c) is string b && b.Contains(" ") ? b.Substring(b.IndexOf(' ')) : "") : Behind(c))
                    .Where(x => x != null).ToList();
                if (rest.Count > 0)
                    parts.Add(RadioLanguage.T("Dahinter " + string.Join(", dann ", rest) + ".", "Behind: " + string.Join(", then ", rest) + ".", "Detrás: " + string.Join(", luego ", rest) + "."));
            }

            if (place > 0)
            {
                string own = RadioLanguage.T("Du bist Platz " + place, "You finished P" + place, "Terminaste " + place + "º");
                if (startPlace > 0 && startPlace != place)
                {
                    int d = startPlace - place;
                    own += d > 0
                        ? RadioLanguage.T(", von " + startPlace + " gestartet, " + d + (d == 1 ? " Platz" : " Plätze") + " gewonnen", ", from P" + startPlace + ", " + d + (d == 1 ? " place" : " places") + " gained", ", saliendo " + startPlace + "º, " + d + (d == 1 ? " puesto" : " puestos") + " ganados")
                        : RadioLanguage.T(", von " + startPlace + " gestartet", ", from P" + startPlace, ", saliendo " + startPlace + "º");
                }
                else if (startPlace == place) own += RadioLanguage.T(", wie gestartet", ", where you started", ", donde saliste");
                if (passesMade > 0 || passesLost > 0)
                    own += RadioLanguage.T(". Auf der Strecke " + passesMade + " Überholmanöver, " + passesLost + " Mal überholt worden",
                        ". On track " + passesMade + " passes made, passed " + passesLost + " times",
                        ". En pista " + passesMade + " adelantamientos, te adelantaron " + passesLost + " veces");
                parts.Add(own + ".");
            }

            var laps = cls.Where(c => !float.IsNaN(c.BestLap) && c.BestLap > 0).OrderBy(c => c.BestLap).ToList();
            if (laps.Count > 0)
            {
                var top = laps[0];
                if (top.IsPlayer)
                    parts.Add(RadioLanguage.T("Die schnellste Rennrunde gehört dir: " + LocalAnswers.LapTime(top.BestLap) + ".", "Fastest race lap is yours: " + LocalAnswers.LapTime(top.BestLap) + ".", "La vuelta rápida es tuya: " + LocalAnswers.LapTime(top.BestLap) + "."));
                else
                {
                    if (!string.IsNullOrWhiteSpace(top.Name))
                        parts.Add(RadioLanguage.T("Schnellste Rennrunde: " + top.Name + ", " + LocalAnswers.LapTime(top.BestLap) + ".", "Fastest race lap: " + top.Name + ", " + LocalAnswers.LapTime(top.BestLap) + ".", "Vuelta rápida: " + top.Name + ", " + LocalAnswers.LapTime(top.BestLap) + "."));
                    if (!float.IsNaN(p.BestLap) && p.BestLap > 0)
                    {
                        int rank = laps.Count(c => !c.IsPlayer && c.BestLap < p.BestLap) + 1;
                        parts.Add(RadioLanguage.T("Deine beste: " + LocalAnswers.LapTime(p.BestLap) + ", Rang " + rank + ".", "Your best: " + LocalAnswers.LapTime(p.BestLap) + ", P" + rank + ".", "Tu mejor: " + LocalAnswers.LapTime(p.BestLap) + ", puesto " + rank + "."));
                    }
                }
            }

            var speeds = cls.Where(c => !float.IsNaN(c.TopSpeed) && c.TopSpeed > 10).OrderByDescending(c => c.TopSpeed).ToList();
            int speedRank = speeds.FindIndex(c => c.IsPlayer) + 1;
            if (speedRank > 0 && speeds.Count >= 3)
            {
                string kmh = RadioLanguage.Num(p.TopSpeed * 3.6f, "0");
                var fast = speeds[0];
                if (speedRank == 1)
                    parts.Add(RadioLanguage.T("Höchsttempo: " + kmh + " km/h, das schnellste Auto im Feld.", "Top speed: " + kmh + " km/h, fastest car in the field.", "Velocidad punta: " + kmh + " km/h, el más rápido del grupo."));
                else if (!string.IsNullOrWhiteSpace(fast.Name))
                {
                    string theirs = RadioLanguage.Num(fast.TopSpeed * 3.6f, "0");
                    parts.Add(RadioLanguage.T("Höchsttempo: du " + kmh + " km/h, Rang " + speedRank + ". Am schnellsten war " + fast.Name + " mit " + theirs + ".",
                        "Top speed: you " + kmh + " km/h, P" + speedRank + ". Quickest was " + fast.Name + " at " + theirs + ".",
                        "Velocidad punta: tú " + kmh + " km/h, puesto " + speedRank + ". El más rápido fue " + fast.Name + " con " + theirs + "."));
                }
            }
            string story = Story(chronicle, vv);
            if (story != null) parts.Insert(Math.Min(parts.Count, 3), story);
            bool good = place > 0 && (place <= 3 || startPlace > 0 && place < startPlace);
            parts.Add((good ? CloseGood() : CloseTough())[(vv + 2) % Variants]);
            return string.Join(" ", parts);
        }

        /// <summary>Kleiner Rennbericht aus der Chronik: der Zweikampf des Rennens oder das letzte gelungene Manöver, dazu Kontakte und Strafen.</summary>
        internal static string Story(IReadOnlyList<ChronicleEntry> chronicle, int variant)
        {
            if (chronicle == null || chronicle.Count == 0) return null;
            int vv = Math.Abs(variant) % Variants;
            var swaps = chronicle.Where(e => (e.Kind == ChronicleKind.PositionGained || e.Kind == ChronicleKind.PositionLost) && e.Detail == ChronicleRecorder.OnTrack &&
                e.Subject != null).ToList();
            var duel = swaps.GroupBy(e => e.SubjectKey ?? e.Subject).Select(g => new { Name = g.Last().Subject, Count = g.Count() })
                .Where(g => g.Count >= 2).OrderByDescending(g => g.Count).FirstOrDefault();
            var parts = new List<string>();
            if (duel != null)
            {
                string n = duel.Name, c = duel.Count.ToString();
                parts.Add(RadioLanguage.Pick(
                    new[] { "Dein Zweikampf des Rennens: " + n + ", " + c + " Platzwechsel.", "Das Duell mit " + n + " war das Highlight, " + c + " Mal ging es hin und her.", n + " und du, " + c + " Platzwechsel, richtig Racing.", "Mit " + n + " hast du " + c + " Mal die Plätze getauscht, starker Kampf.", "Das Rennen hieß " + n + " gegen dich: " + c + " Platzwechsel." },
                    new[] { "Your duel of the race: " + n + ", " + c + " position swaps.", "The fight with " + n + " was the highlight, " + c + " swaps back and forth.", n + " and you, " + c + " swaps, proper racing.", "You traded places with " + n + " " + c + " times, great battle.", "The race was " + n + " against you: " + c + " position swaps." },
                    new[] { "Tu duelo de la carrera: " + n + ", " + c + " cambios de posición.", "La pelea con " + n + " fue lo mejor, " + c + " cambios de posición.", n + " y tú, " + c + " cambios, carreras de verdad.", "Con " + n + " te cambiaste el puesto " + c + " veces, gran pelea.", "La carrera fue " + n + " contra ti: " + c + " cambios de posición." })[vv]);
            }
            else
            {
                var pass = chronicle.LastOrDefault(e => e.Kind == ChronicleKind.PositionGained && e.Detail == ChronicleRecorder.OnTrack && e.Subject != null);
                if (pass != null)
                {
                    string n = pass.Subject, l = (pass.Lap + 1).ToString();
                    parts.Add(RadioLanguage.Pick(
                        new[] { "Starker Moment: das Manöver gegen " + n + " in Runde " + l + ".", "Gut gemacht war der Angriff auf " + n + " in Runde " + l + ".", "Hängen bleibt dein Überholmanöver an " + n + ", Runde " + l + ".", "In Runde " + l + " hast du " + n + " sauber geschnappt.", "Bester Moment: Runde " + l + ", vorbei an " + n + "." },
                        new[] { "Strong moment: the move on " + n + " on lap " + l + ".", "Well done on the attack on " + n + " on lap " + l + ".", "What sticks is your pass on " + n + ", lap " + l + ".", "On lap " + l + " you picked off " + n + " cleanly.", "Best moment: lap " + l + ", past " + n + "." },
                        new[] { "Gran momento: la maniobra sobre " + n + " en la vuelta " + l + ".", "Bien hecho el ataque a " + n + " en la vuelta " + l + ".", "Queda tu adelantamiento a " + n + ", vuelta " + l + ".", "En la vuelta " + l + " superaste limpio a " + n + ".", "Mejor momento: vuelta " + l + ", por delante de " + n + "." })[vv]);
                }
            }
            int contacts = chronicle.Count(e => e.Kind == ChronicleKind.Contact);
            int penalties = chronicle.Count(e => e.Kind == ChronicleKind.Penalty);
            if (contacts > 0 || penalties > 0)
            {
                var bits = new List<string>();
                if (contacts > 0) bits.Add(contacts + RadioLanguage.T(contacts == 1 ? " Kontakt" : " Kontakte", contacts == 1 ? " contact" : " contacts", contacts == 1 ? " contacto" : " contactos"));
                if (penalties > 0) bits.Add(penalties + RadioLanguage.T(penalties == 1 ? " Strafe" : " Strafen", penalties == 1 ? " penalty" : " penalties", penalties == 1 ? " sanción" : " sanciones"));
                parts.Add(RadioLanguage.T("Dazu ", "Also ", "Además, ") + string.Join(RadioLanguage.T(" und ", " and ", " y "), bits) + ".");
            }
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }

        /// <summary>Motivation fürs nächste Rennen nach einem guten Ergebnis (Podium oder Plätze gewonnen), fünf Formulierungen.</summary>
        internal static string[] CloseGood() => RadioLanguage.Pick(
            new[] { "Stark gefahren. Genau so gehen wir ins nächste Rennen.", "Das war ein gutes Rennen, darauf bauen wir nächstes Mal auf.", "Mehr davon! Im nächsten Rennen holen wir uns den nächsten Schritt.", "Gute Arbeit heute. Nimm das Gefühl mit ins nächste Rennen.", "Starke Leistung. Nächstes Mal greifen wir wieder so an." },
            new[] { "Strong drive. That's how we go into the next race.", "That was a good race, we build on it next time.", "More of that! Next race we take the next step.", "Good work today. Take that feeling into the next race.", "Strong performance. Next time we attack just like that." },
            new[] { "Gran carrera. Así vamos a la próxima.", "Fue una buena carrera, la próxima construimos sobre esto.", "¡Más de esto! En la próxima damos el siguiente paso.", "Buen trabajo hoy. Llévate esa sensación a la próxima carrera.", "Gran actuación. La próxima vez atacamos igual." });

        /// <summary>Motivation fürs nächste Rennen nach einem schwierigen Rennen, fünf Formulierungen.</summary>
        internal static string[] CloseTough() => RadioLanguage.Pick(
            new[] { "Kopf hoch, im nächsten Rennen holen wir das zurück.", "Heute war es zäh, aber wir haben die Daten. Nächstes Mal sitzt es.", "Abhaken, daraus lernen, und im nächsten Rennen wieder angreifen.", "Durchgezogen bis ins Ziel, das zählt. Nächstes Mal geht mehr.", "Nicht unser Tag, aber du bist dran geblieben. Das nächste Rennen gehört uns." },
            new[] { "Chin up, we'll get it back next race.", "Tough one today, but we've got the data. Next time it clicks.", "Tick it off, learn from it, and attack again next race.", "You pushed through to the flag, that counts. More next time.", "Not our day, but you kept at it. The next race is ours." },
            new[] { "Ánimo, en la próxima carrera lo recuperamos.", "Hoy costó, pero tenemos los datos. La próxima saldrá.", "Olvídalo, aprende de ello y a atacar en la próxima carrera.", "Llegaste hasta la bandera, eso cuenta. La próxima habrá más.", "No fue nuestro día, pero no aflojaste. La próxima carrera es nuestra." });

        internal static string Compose(FieldView v, string driverName, float fuelPerLap, int fuelLaps, int variant)
        {
            if (v?.Player == null || v.Session != FieldSession.Practice && v.Session != FieldSession.Qualify && v.Session != FieldSession.Warmup) return null;
            var p = v.Player;
            int vv = Math.Abs(variant) % Variants;
            bool quali = v.Session == FieldSession.Qualify;
            string name = string.IsNullOrWhiteSpace(driverName) ? "Rob" : driverName.Trim();
            var parts = new List<string>();
            string[] intro = quali
                ? RadioLanguage.Pick(
                    new[] { name + ", kurz zur Quali.", name + ", die Quali in Zahlen.", "So, " + name + ", Quali-Bilanz.", name + ", hier die Auswertung der Quali.", name + ", schauen wir auf die Quali." },
                    new[] { name + ", quick word on qualifying.", name + ", qualifying in numbers.", "Right, " + name + ", qualifying review.", name + ", here's the qualifying debrief.", name + ", let's look at qualifying." },
                    new[] { name + ", un momento sobre la clasificación.", name + ", la clasificación en cifras.", "Bien, " + name + ", balance de la clasificación.", name + ", aquí el resumen de la clasificación.", name + ", veamos la clasificación." })
                : RadioLanguage.Pick(
                    new[] { "Sehr gut, " + name + ", das Training ist durch.", name + ", kurze Bilanz vom Training.", name + ", das Training in Zahlen.", "So, " + name + ", was wir aus dem Training mitnehmen.", name + ", Training beendet, hier die Auswertung." },
                    new[] { "Good job, " + name + ", practice is done.", name + ", quick practice review.", name + ", practice in numbers.", "Right, " + name + ", what we take from practice.", name + ", practice over, here's the debrief." },
                    new[] { "Muy bien, " + name + ", se acabó el entrenamiento.", name + ", balance rápido del entrenamiento.", name + ", el entrenamiento en cifras.", "Bien, " + name + ", lo que nos llevamos del entrenamiento.", name + ", entrenamiento terminado, aquí el resumen." });
            parts.Add(intro[vv]);

            bool valid = !float.IsNaN(p.BestLap);
            float mine = valid ? p.BestLap : p.FastestAnyLap;
            var timed = v.Cars.Where(c => !float.IsNaN(c.BestLap) && c.BestLap > 0).OrderBy(c => c.BestLap).ToList();
            // Mantorp 22.11.: eine gestrichene „Runde“ von 28 s war ein Rundenstück und hieß dann „gültig Rang 1“. Eine gestrichene Zeit
            // zählt nur, wenn sie eine ganze Runde sein kann: nicht unter der Streckenlänge bei Höchstschnitt und nicht 10 % unter der Feldbestzeit.
            if (!valid && !float.IsNaN(mine) && (!FieldPicture.PlausibleLap(mine, v.TrackLength) || timed.Count > 0 && mine < timed[0].BestLap * .9f))
                mine = float.NaN;
            if (!float.IsNaN(mine))
            {
                string t = LocalAnswers.LapTime(mine);
                int rank = timed.Count(c => !c.IsPlayer && c.BestLap < mine) + 1;
                parts.Add(valid
                    ? RadioLanguage.T("Deine Bestzeit: " + t + ", das ist Rang " + rank + ".", "Your best: " + t + ", that's P" + rank + ".", "Tu mejor vuelta: " + t + ", puesto " + rank + ".")
                    : RadioLanguage.T("Deine schnellste Runde war " + t + ", leider gestrichen. Gültig wäre das Rang " + rank + " gewesen.",
                        "Your fastest lap was " + t + ", deleted unfortunately. Valid, that would have been P" + rank + ".",
                        "Tu vuelta más rápida fue " + t + ", anulada por desgracia. Válida habría sido el puesto " + rank + "."));
                if (timed.Count > 0 && rank > 1)
                {
                    var gaps = new List<string>();
                    gaps.Add(RadioLanguage.T(Gap(mine - timed[0].BestLap) + " auf die Spitze", Gap(mine - timed[0].BestLap) + " to the top", Gap(mine - timed[0].BestLap) + " a la cabeza"));
                    if (rank > 3 && timed.Count >= 3) gaps.Add(RadioLanguage.T(Gap(mine - timed[2].BestLap) + " auf Platz 3", Gap(mine - timed[2].BestLap) + " to P3", Gap(mine - timed[2].BestLap) + " al tercero"));
                    if (rank > 5 && timed.Count >= 5) gaps.Add(RadioLanguage.T(Gap(mine - timed[4].BestLap) + " auf die Top 5", Gap(mine - timed[4].BestLap) + " to the top five", Gap(mine - timed[4].BestLap) + " al top cinco"));
                    parts.Add(RadioLanguage.T("Rückstand: ", "Gap: ", "Diferencia: ") + string.Join(", ", gaps) + ".");
                }
            }
            else parts.Add(RadioLanguage.T("Eine gezeitete Runde hatten wir nicht.", "We didn't get a timed lap.", "No tuvimos vuelta cronometrada."));

            var fastest = v.Cars.Where(c => !float.IsNaN(c.TopSpeed)).OrderByDescending(c => c.TopSpeed).ToList();
            int speedRank = fastest.FindIndex(c => c.IsPlayer) + 1;
            if (speedRank > 0 && fastest.Count >= 5)
            {
                string kmh = RadioLanguage.Num(p.TopSpeed * 3.6f, "0");
                if (speedRank <= 3)
                    parts.Add(RadioLanguage.T("Auf der Geraden bist du stark: " + kmh + " km/h, Rang " + speedRank + " im Feld. Die Zeit liegt also in den Kurven und beim Anbremsen.",
                        "You're strong on the straights: " + kmh + " km/h, P" + speedRank + " in the field. So the time is in the corners and the braking.",
                        "En recta vas fuerte: " + kmh + " km/h, el " + speedRank + " del grupo. El tiempo está en las curvas y las frenadas."));
                else if (speedRank > fastest.Count * 2 / 3)
                    parts.Add(RadioLanguage.T("Auf der Geraden fehlt Tempo: " + kmh + " km/h, nur Rang " + speedRank + ". Am Kurvenausgang früher ans Gas.",
                        "Short on top speed: " + kmh + " km/h, only P" + speedRank + ". Get on the throttle earlier on corner exit.",
                        "Falta velocidad punta: " + kmh + " km/h, solo el " + speedRank + ". Acelera antes a la salida de las curvas."));
            }
            if (fuelPerLap > 0 && fuelLaps > 0)
            {
                string l = RadioLanguage.Num(fuelPerLap, "0.0");
                parts.Add(fuelLaps >= 3
                    ? RadioLanguage.T("Verbrauch " + l + " Liter pro Runde, über " + fuelLaps + " Runden gemessen.", "Fuel " + l + " litres a lap, measured over " + fuelLaps + " laps.", "Consumo " + l + " litros por vuelta, medido en " + fuelLaps + " vueltas.")
                    : RadioLanguage.T("Verbrauch ca. " + l + " Liter pro Runde, nur grob, dafür waren es zu wenige Runden.", "Fuel roughly " + l + " litres a lap, only a rough figure from few laps.", "Consumo aprox. " + l + " litros por vuelta, solo orientativo con pocas vueltas."));
            }
            string[] close = quali
                ? RadioLanguage.Pick(
                    new[] { "Jetzt das Rennen: sauber starten, dann angreifen.", "Fürs Rennen gilt: erste Runde überleben, dann Plätze holen.", "Im Rennen zählt die Konstanz. Wir gehen das ruhig an.", "Startplatz ist gesetzt, jetzt machen wir im Rennen das Beste draus.", "Kopf frei, fürs Rennen sind wir bereit." },
                    new[] { "Now the race: clean start, then attack.", "For the race: survive lap one, then pick up places.", "In the race consistency counts. We'll take it calmly.", "The grid slot is set, now we make the most of the race.", "Clear your head, we're ready for the race." },
                    new[] { "Ahora la carrera: salida limpia y luego a atacar.", "En carrera: sobrevive a la primera vuelta y luego gana puestos.", "En carrera cuenta la constancia. Lo haremos con calma.", "La posición está hecha, ahora a sacar lo mejor en carrera.", "Cabeza despejada, estamos listos para la carrera." })
                : RadioLanguage.Pick(
                    new[] { "Setz in der nächsten Session um, was wir trainiert haben.", "Das nehmen wir mit in die nächste Session.", "Gute Basis. Jetzt daraus Zeit machen.", "Weiter so, in der nächsten Session legen wir nach.", "Wir haben unsere Daten, jetzt geht's ans Umsetzen." },
                    new[] { "Put what we practised into the next session.", "We take that into the next session.", "Good base. Now turn it into lap time.", "Keep it up, next session we push on.", "We've got our data, now let's put it to work." },
                    new[] { "Aplica en la próxima sesión lo que hemos entrenado.", "Esto nos lo llevamos a la próxima sesión.", "Buena base. Ahora a convertirlo en tiempo.", "Así se hace, en la próxima sesión apretamos más.", "Tenemos los datos, ahora toca aplicarlos." });
            parts.Add(close[(vv + 1) % close.Length]);
            return string.Join(" ", parts);
        }
    }
}
