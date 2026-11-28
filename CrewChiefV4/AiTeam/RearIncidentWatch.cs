using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Dreher oder Unfall direkt hinter Rob, für alle Sims mit Lagebild.
    /// RaceRoom Hockenheim 23.11. (Rob, 21:38:47): „Hinter mir hat sich jemand gedreht.“ Die Gefahrmeldungen schauen nur nach
    /// vorn; der Wagen hinter Rob (Iban Garcia, eben noch 0,9 s dahinter) drehte sich, und niemand sagte etwas. Für Rob ist das
    /// Renngeschehen: Der Druck von hinten ist weg oder der Verfolger fällt zurück. Sara sagt es kurz mit Namen, wenn ein Wagen
    /// bis etwa 4 s (höchstens 400 m) hinter Rob sich dreht oder stehen bleibt – bestätigt nach 300 ms, je Wagen höchstens alle
    /// 90 s und insgesamt höchstens alle 12 s.
    /// </summary>
    /// <remarks>
    /// Rob 28.11. (iRacing Navarra): „Max 200 Meter vor und hinter mir, dann aber Echtzeit: Dreher, langsame Fahrzeuge, Unfälle.“
    /// Lucas Raine brach 70 m hinter Rob von 37 auf 13 m/s ein, Ali Yüksel drehte sich 90 m dahinter – beides blieb ungesagt,
    /// weil hinten nur Dreher (rückwärts) und Stillstand zählten. Jetzt: bis 200 m hinter Rob (ohne Zeitgrenze), auch langsam.
    /// </remarks>
    internal sealed class RearIncidentWatch
    {
        internal const float MaxBehindM = (float)RaceTrafficWatch.HazardReachM, MinBehindM = 5f;
        internal static readonly TimeSpan Confirm = TimeSpan.FromMilliseconds(300);
        internal static readonly TimeSpan SameCarAgain = TimeSpan.FromSeconds(90);
        internal static readonly TimeSpan AnyAgain = TimeSpan.FromSeconds(12);

        internal sealed class Incident
        {
            public string Name;
            public float GapS, GapM;
            public bool Stopped, Slow;
            /// <summary>Weitere Wagen, die im selben Moment hinter Rob stehen oder sich gedreht haben (Massenunfall).</summary>
            public List<string> Others = new List<string>();
        }

        private readonly Dictionary<string, DateTime> called = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private DateTime lastCall = DateTime.MinValue;
        private int session = int.MinValue, variant;

        internal void Reset() { called.Clear(); lastCall = DateTime.MinValue; session = int.MinValue; }

        internal static bool Relevant(FieldCarView c, DateTime now) =>
            c != null && !c.IsPlayer && !c.InPit && !string.IsNullOrWhiteSpace(c.Name) && c.LapDelta == 0 &&
            (c.Motion == CarMotion.Spun || c.Motion == CarMotion.Stopped || c.Motion == CarMotion.Slow) &&
            c.MotionSinceUtc != default(DateTime) && now >= c.MotionSinceUtc && now - c.MotionSinceUtc >= Confirm &&
            !float.IsNaN(c.GapM) && c.GapM <= -MinBehindM && c.GapM >= -MaxBehindM;

        /// <summary>Nächster gedrehter oder stehender Wagen direkt hinter Rob, sonst null.</summary>
        internal static Incident Nearest(FieldView view, DateTime now)
        {
            if (view == null || view.Cars == null || view.Player == null || !view.Fresh(now, 2) || view.InMenus || view.Paused || view.InReplay) return null;
            var cars = view.Cars.Where(c => Relevant(c, now)).OrderByDescending(c => c.GapM).ToList();
            var car = cars.FirstOrDefault();
            // Massenunfall nur mit Wagen, die stehen oder sich gedreht haben; ein langsamer Wagen daneben ist kein Beteiligter.
            return car == null ? null : new Incident { Name = car.Name, GapS = car.GapS, GapM = car.GapM, Stopped = car.Motion == CarMotion.Stopped,
                Slow = car.Motion == CarMotion.Slow,
                Others = car.Motion == CarMotion.Slow ? new List<string>() :
                    cars.Skip(1).Where(c => c.Motion != CarMotion.Slow).Select(c => c.Name).Take(3).ToList() };
        }

        internal Incident Take(FieldView view, GameStateData gs, DateTime now)
        {
            if (gs == null) return null;
            if (gs.SessionData.SessionId != session) { called.Clear(); lastCall = DateTime.MinValue; session = gs.SessionData.SessionId; }
            if (gs.SessionData.SessionType != SessionType.Race || gs.PitData.InPitlane || gs.SessionData.SessionPhase != SessionPhase.Green) return null;
            if (lastCall != DateTime.MinValue && now >= lastCall && now - lastCall < AnyAgain) return null;
            var incident = Nearest(view, now);
            if (incident == null) return null;
            if (called.TryGetValue(incident.Name, out var at) && now >= at && now - at < SameCarAgain) return null;
            called[incident.Name] = now; lastCall = now;
            foreach (var other in incident.Others) called[other] = now;
            return incident;
        }

        /// <summary>
        /// LMU Sebring 23.11. (Rob, 22:29:57): „Gar keine Ansagen, dass es hinter mir einen Massencrash gab.“ Stehen mehrere Wagen
        /// zugleich hinter Rob, sagt Sara es als einen Satz mit allen Namen.
        /// </summary>
        internal string CrashText(IList<string> names)
        {
            var list = (names ?? new List<string>()).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
            int v = variant++;
            if (list.Count < 2)
                return RadioVariants.At(v,
                    RadioLanguage.T("Massenunfall hinter dir, mehrere Autos stehen.", "Big crash behind you, several cars stopped.", "Accidente múltiple detrás, varios coches parados."),
                    RadioLanguage.T("Hinter dir hat es gekracht, mehrere Autos.", "There's been a crash behind you, several cars.", "Ha habido un choque detrás, varios coches."),
                    RadioLanguage.T("Unfall hinter dir mit mehreren Autos. Du bist durch.", "Multi-car crash behind you. You're through.", "Accidente con varios coches detrás. Has pasado."));
            string joined = string.Join(", ", list.Take(list.Count - 1)) + RadioLanguage.T(" und ", " and ", " y ") + list[list.Count - 1];
            return RadioVariants.At(v,
                RadioLanguage.T("Massenunfall hinter dir: " + joined + ".", "Big crash behind you: " + joined + ".", "Accidente múltiple detrás: " + joined + "."),
                RadioLanguage.T("Hinter dir hat es gekracht, " + joined + " stehen.", "Crash behind you, " + joined + " are stopped.", "Choque detrás, " + joined + " están parados."),
                RadioLanguage.T("Unfall hinter dir mit " + joined + ". Du bist durch.", "Crash behind you involving " + joined + ". You're through.", "Accidente detrás con " + joined + ". Has pasado."),
                RadioLanguage.T(joined + " sind hinter dir verunfallt.", joined + " have crashed behind you.", joined + " han tenido un accidente detrás."));
        }

        /// <summary>Kurz wie eine Spotterin, mit Namen; ohne Namen „hinter dir“.</summary>
        internal string Text(string name, bool stopped, bool slow = false)
        {
            bool named = !string.IsNullOrWhiteSpace(name);
            string who = named ? name : RadioLanguage.T("Einer", "Someone", "Alguien");
            int v = variant++;
            if (slow && !stopped)
                return RadioVariants.At(v,
                    RadioLanguage.T(who + " hinter dir wird plötzlich langsam.", who + " behind you is suddenly slow.", who + " detrás de ti va lento de repente."),
                    RadioLanguage.T("Hinter dir: " + who + " hat Probleme, viel langsamer.", "Behind you: " + who + " has a problem, much slower.", "Detrás: " + who + " tiene problemas, mucho más lento."),
                    RadioLanguage.T(who + " hinter dir bricht ein, du gewinnst Luft.", who + " behind you is dropping away, you're gaining room.", who + " detrás de ti se descuelga, ganas aire."),
                    RadioLanguage.T("Langsamer Wagen hinter dir, " + who + ".", "Slow car behind you, " + who + ".", "Coche lento detrás de ti, " + who + "."));
            if (stopped)
                return RadioVariants.At(v,
                    RadioLanguage.T(who + " steht hinter dir.", who + " has stopped behind you.", who + " se ha parado detrás de ti."),
                    RadioLanguage.T("Hinter dir: " + who + " ist stehen geblieben.", "Behind you: " + who + " has come to a stop.", "Detrás: " + who + " se ha quedado parado."),
                    RadioLanguage.T(who + " hinter dir steht, der Druck ist weg.", who + " behind you is stopped, the pressure's off.", who + " está parado detrás, se acabó la presión."),
                    RadioLanguage.T("Unfall hinter dir, " + who + " steht.", "Incident behind you, " + who + " is stopped.", "Incidente detrás, " + who + " está parado."));
            return RadioVariants.At(v,
                RadioLanguage.T(who + " hat sich hinter dir gedreht.", who + " has spun behind you.", who + " ha hecho un trompo detrás de ti."),
                RadioLanguage.T("Dreher hinter dir, " + who + ".", "Spin behind you, " + who + ".", "Trompo detrás de ti, " + who + "."),
                RadioLanguage.T(who + " dreht sich hinter dir, du hast Luft.", who + " is spinning behind you, you've got room.", who + " hace un trompo detrás, tienes aire."),
                RadioLanguage.T("Hinter dir: " + who + " hat sich gedreht.", "Behind you: " + who + " has spun.", "Detrás: " + who + " ha hecho un trompo."),
                RadioLanguage.T(who + " ist hinter dir abgeflogen.", who + " has gone off behind you.", who + " se ha salido detrás de ti."));
        }
    }
}
