using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Gefahr weiter voraus, für alle Sims mit Lagebild (LMU/rF2, AC, ACE, AMS2, iRacing, RaceRoom).
    /// LMU 22.11., 12:01 (Rob): „Eben gab es eine Gelbphase und ein stehendes Auto vor mir. Du hättest alle anderen überstimmen
    /// müssen.“ udo scharny drehte sich 1500 m vor Rob, stand, drehte sich wieder; Rob kam auf 550 m heran. Das Lagebild sah es
    /// die ganze Zeit (Dreher/steht), die Nahbereichswarnung prüft aber nur bis etwa 400–600 m und lehnte deshalb ab
    /// („field_picture_normal“ für das Auto direkt vor Rob). Sara meldet so etwas jetzt früh und kurz:
    /// „Gelb voraus, Kurve 4. Auto steht.“ – „Gelb“ nur, wenn die Sim wirklich Gelb zeigt, sonst „Achtung, Kurve 4: Dreher.“
    /// </summary>
    internal sealed class YellowAheadWatch
    {
        /// <summary>Näher als das übernimmt die Nahbereichswarnung (Abstand, Ausweichen).</summary>
        internal const float NearM = 250f;
        internal const float FarM = 1000f;
        /// <summary>Höchstens so viele Sekunden Fahrzeit entfernt (Feldtempo).</summary>
        internal const float MaxAheadS = 22f;
        internal static readonly TimeSpan Confirm = TimeSpan.FromMilliseconds(300);
        internal static readonly TimeSpan SameCarAgain = TimeSpan.FromSeconds(90);
        internal static readonly TimeSpan AnyAgain = TimeSpan.FromSeconds(12);

        internal sealed class Hazard
        {
            public string Name;
            public float GapM, LapDistance;
            public bool Stopped;
        }

        private readonly Dictionary<string, DateTime> called = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private DateTime lastCall = DateTime.MinValue;
        private int session = int.MinValue;

        internal void Reset() { called.Clear(); lastCall = DateTime.MinValue; session = int.MinValue; }

        internal static bool Hazardous(FieldCarView c, DateTime now) =>
            c != null && !c.IsPlayer && !c.InPit && (c.Motion == CarMotion.Stopped || c.Motion == CarMotion.Spun) &&
            c.MotionSinceUtc != default(DateTime) && now >= c.MotionSinceUtc && now - c.MotionSinceUtc >= Confirm &&
            !float.IsNaN(c.GapM) && c.GapM >= NearM && c.GapM <= FarM && (float.IsNaN(c.GapS) || c.GapS <= MaxAheadS);

        /// <summary>Nächster gedrehter oder stehender Wagen zwischen 250 und 1000 m vor Rob, sonst null.</summary>
        internal static Hazard Nearest(FieldView view, DateTime now)
        {
            if (view == null || view.Cars == null || view.Player == null || !view.Fresh(now, 2) || view.InMenus || view.Paused || view.InReplay) return null;
            var car = view.Cars.Where(c => Hazardous(c, now)).OrderBy(c => c.GapM).FirstOrDefault();
            return car == null ? null : new Hazard { Name = car.Name, GapM = car.GapM, LapDistance = car.LapDistance, Stopped = car.Motion == CarMotion.Stopped };
        }

        /// <summary>Der gemeldete Wagen steht, dreht oder schleicht noch vor Rob (sonst ist die Warnung überholt).</summary>
        internal static bool StillThere(FieldView view, string name, DateTime now)
        {
            var c = view?.ByName(name);
            return c != null && view.Fresh(now, 2) && !c.InPit &&
                (c.Motion == CarMotion.Stopped || c.Motion == CarMotion.Spun || c.Motion == CarMotion.Slow) &&
                !float.IsNaN(c.GapM) && c.GapM > 30 && c.GapM <= FarM + 200;
        }

        /// <summary>Zeigt die Sim Gelb (eigene Flagge, lokales Gelb oder ein gelber Sektor)?</summary>
        internal static bool SimShowsYellow(GameStateData gs) =>
            gs != null && (gs.FlagData.isLocalYellow || gs.SessionData.Flag == FlagEnum.YELLOW || gs.SessionData.Flag == FlagEnum.DOUBLE_YELLOW ||
                FlagWatch.AnySectorYellow(gs));

        /// <summary>Kurz wie eine Spotterin: Ort zuerst (Kurve oder Meter), dann was dort ist.</summary>
        internal static string Text(bool yellow, string section, float gapM, bool stopped)
        {
            int metres = Math.Max(100, (int)(Math.Round(gapM / 100.0) * 100));
            bool named = !string.IsNullOrWhiteSpace(section);
            string where = named ? section : RadioLanguage.T("in " + metres + " Metern", "in " + metres + " metres", "a " + metres + " metros");
            string what = stopped ? RadioLanguage.T("Auto steht.", "Car stopped.", "Coche parado.") : RadioLanguage.T("Dreher.", "Car spun.", "Trompo.");
            return yellow
                ? RadioLanguage.T("Gelb voraus, " + where + ". ", "Yellow ahead, " + where + ". ", "Amarilla delante, " + where + ". ") + what
                : RadioLanguage.T("Achtung voraus, " + where + ": ", "Careful ahead, " + where + ": ", "Atención delante, " + where + ": ") + what;
        }

        /// <summary>Neue Gefahr voraus, höchstens alle 12 s und je Wagen alle 90 s; sonst null.</summary>
        internal Hazard Take(FieldView view, GameStateData gs, DateTime now)
        {
            if (gs == null) return null;
            if (gs.SessionData.SessionId != session) { called.Clear(); lastCall = DateTime.MinValue; session = gs.SessionData.SessionId; }
            if (gs.PitData.InPitlane || gs.SessionData.SessionPhase != SessionPhase.Green) return null;
            if (!(gs.PositionAndMotionData.CarSpeed >= 15)) return null; // Rob steht oder rollt: keine Warnung nach vorn
            if (lastCall != DateTime.MinValue && now >= lastCall && now - lastCall < AnyAgain) return null;
            var hazard = Nearest(view, now);
            if (hazard == null || string.IsNullOrWhiteSpace(hazard.Name)) return null;
            if (called.TryGetValue(hazard.Name, out var at) && now >= at && now - at < SameCarAgain) return null;
            called[hazard.Name] = now; lastCall = now;
            return hazard;
        }
    }
}
