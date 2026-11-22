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
