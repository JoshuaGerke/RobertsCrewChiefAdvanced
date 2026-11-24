using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrewChiefV4.GameState;

namespace CrewChiefV4
{
    /// <summary>
    /// LMU Sebring 24.11.: Beim Einstieg in eine schon beendete Qualifikation brach Crew Chief mit „Fatal error when running“
    /// (NullReferenceException in runGame) ab, und die Crew war für das ganze Rennen weg. Ein einzelner fehlerhafter Tick
    /// darf die Sitzung nicht beenden: Der Fehler wird mit Stack und den leeren Feldern des Spielzustands protokolliert,
    /// der nächste Tick läuft normal. Auch wenn der Fehler bleibt, läuft Crew Chief weiter (die Crew liest ihren Zustand
    /// selbst); das Protokoll meldet ihn dann alle 100 Ticks.
    /// </summary>
    internal static class TickGuard
    {
        private static int consecutive, total;
        private static readonly HashSet<string> reported = new HashSet<string>();

        internal static int Consecutive => consecutive;

        internal static void Ok() { consecutive = 0; }

        internal static void Reset() { consecutive = 0; total = 0; lock (reported) reported.Clear(); }

        /// <summary>Protokolliert den Fehler und gibt true zurück: weiterlaufen.</summary>
        internal static bool Survive(Exception e, GameStateData current, GameStateData previous)
        {
            consecutive++;
            total++;
            string key = e.GetType().Name + "|" + (e.StackTrace ?? "");
            bool first;
            lock (reported) first = reported.Add(key);
            if (first || total % 100 == 0)
            {
                try
                {
                    Console.WriteLine("RRCA Tick-Schutz: Fehler im Spielzyklus abgefangen (" + total + " gesamt, " + consecutive +
                        " hintereinander). Crew Chief läuft weiter.\n" + e + "\nLeere Felder: " + NullFields(current, previous));
                }
                catch (Exception) { }
            }
            return true;
        }

        /// <summary>Welche Teile des Spielzustands waren leer? Damit lässt sich die Stelle ohne Zeilennummern finden.</summary>
        internal static string NullFields(GameStateData current, GameStateData previous)
        {
            var parts = new List<string>();
            if (current == null) parts.Add("current");
            else parts.AddRange(Nulls("current", current).Concat(Nulls("current.SessionData", current.SessionData)));
            if (previous == null) parts.Add("previous");
            else parts.AddRange(Nulls("previous", previous).Concat(Nulls("previous.SessionData", previous.SessionData)));
            if (current != null)
                parts.Add("phase=" + current.SessionData?.SessionType + "/" + current.SessionData?.SessionPhase + " new=" + current.SessionData?.IsNewSession);
            return parts.Count == 0 ? "keine" : string.Join(", ", parts);
        }

        private static IEnumerable<string> Nulls(string prefix, object o)
        {
            if (o == null) return new[] { prefix };
            try
            {
                return o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => !f.FieldType.IsValueType && f.FieldType != typeof(string))
                    .Where(f => { try { return f.GetValue(o) == null; } catch (Exception) { return false; } })
                    .Select(f => prefix + "." + f.Name).ToList();
            }
            catch (Exception) { return new string[0]; }
        }
    }
}
