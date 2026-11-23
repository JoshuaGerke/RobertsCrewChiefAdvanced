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
    internal sealed class RearIncidentWatch
    {
        internal const float MaxBehindS = 4f, MaxBehindM = 400f, MinBehindM = 5f;
        internal static readonly TimeSpan Confirm = TimeSpan.FromMilliseconds(300);
        internal static readonly TimeSpan SameCarAgain = TimeSpan.FromSeconds(90);
        internal static readonly TimeSpan AnyAgain = TimeSpan.FromSeconds(12);

        internal sealed class Incident
        {
            public string Name;
            public float GapS, GapM;
            public bool Stopped;
        }

        private readonly Dictionary<string, DateTime> called = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private DateTime lastCall = DateTime.MinValue;
        private int session = int.MinValue, variant;

        internal void Reset() { called.Clear(); lastCall = DateTime.MinValue; session = int.MinValue; }

        internal static bool Relevant(FieldCarView c, DateTime now) =>
            c != null && !c.IsPlayer && !c.InPit && !string.IsNullOrWhiteSpace(c.Name) && c.LapDelta == 0 &&
            (c.Motion == CarMotion.Spun || c.Motion == CarMotion.Stopped) &&
            c.MotionSinceUtc != default(DateTime) && now >= c.MotionSinceUtc && now - c.MotionSinceUtc >= Confirm &&
            !float.IsNaN(c.GapM) && c.GapM <= -MinBehindM && c.GapM >= -MaxBehindM &&
            (float.IsNaN(c.GapS) || c.GapS >= -MaxBehindS);

        /// <summary>Nächster gedrehter oder stehender Wagen direkt hinter Rob, sonst null.</summary>
        internal static Incident Nearest(FieldView view, DateTime now)
        {
            if (view == null || view.Cars == null || view.Player == null || !view.Fresh(now, 2) || view.InMenus || view.Paused || view.InReplay) return null;
            var car = view.Cars.Where(c => Relevant(c, now)).OrderByDescending(c => c.GapM).FirstOrDefault();
            return car == null ? null : new Incident { Name = car.Name, GapS = car.GapS, GapM = car.GapM, Stopped = car.Motion == CarMotion.Stopped };
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
            return incident;
        }

        /// <summary>Kurz wie eine Spotterin, mit Namen; ohne Namen „hinter dir“.</summary>
        internal string Text(string name, bool stopped)
        {
            bool named = !string.IsNullOrWhiteSpace(name);
            string who = named ? name : RadioLanguage.T("Einer", "Someone", "Alguien");
            int v = variant++;
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
