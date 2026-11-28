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
        /// <summary>P1.2 (Rob 25.11.): nur Gefahren etwa 3 bis 8 Sekunden vor Rob, Zeitlücke statt fester Meter. RaceRoom Laguna
        /// Seca 27.11.: Warnungen bei 700–1000 m, das Auto fuhr nach 3–11 s weiter oder stand in der Garage.</summary>
        internal const float MinAheadS = 3f, MaxAheadS = 8f;
        internal static readonly TimeSpan Confirm = TimeSpan.FromMilliseconds(300);
        /// <summary>Stehend erst nach 2 s: RaceRoom-KI hält vor dem Sprung in die Garage kurz auf der Strecke an.</summary>
        internal static readonly TimeSpan ConfirmStopped = TimeSpan.FromSeconds(2);
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
        private readonly List<float> garage = new List<float>();

        internal void Reset() { called.Clear(); garage.Clear(); times.Clear(); lastCall = DateTime.MinValue; session = int.MinValue; }

        /// <summary>Fahrzeit bis zum Wagen: gemessene Zeitlücke, sonst Meter durch Robs Tempo (mindestens 15 m/s).</summary>
        internal static float AheadSeconds(FieldCarView c, float playerSpeed) =>
            !float.IsNaN(c.GapS) && c.GapS > 0 ? c.GapS : float.IsNaN(c.GapM) ? float.NaN : c.GapM / Math.Max(15f, playerSpeed);

        internal static bool Hazardous(FieldCarView c, DateTime now, float playerSpeed = 0)
        {
            if (c == null || c.IsPlayer || c.InPit || c.Motion != CarMotion.Stopped && c.Motion != CarMotion.Spun) return false;
            TimeSpan confirm = c.Motion == CarMotion.Stopped ? ConfirmStopped : Confirm;
            if (c.MotionSinceUtc == default(DateTime) || now < c.MotionSinceUtc || now - c.MotionSinceUtc < confirm) return false;
            float s = AheadSeconds(c, playerSpeed);
            return !float.IsNaN(c.GapM) && c.GapM >= NearM && c.GapM <= FarM && !float.IsNaN(s) && s >= MinAheadS && s <= MaxAheadS;
        }

        /// <summary>RaceRoom 27.11.: KI-Autos in der Garage stehen alle auf derselben Streckenposition (Laguna 3032 m), Tempo 0, nicht
        /// in der Box. Zwei oder mehr stehende Wagen auf demselben Meter sind die Garage, kein Unfall; der Platz gilt dann für die
        /// ganze Sitzung.</summary>
        internal static List<float> GarageSpots(FieldView view)
        {
            var parked = view.Cars.Where(c => c != null && !c.IsPlayer && !c.InPit && c.Speed < .05f && !float.IsNaN(c.LapDistance)).ToList();
            var spots = new List<float>();
            foreach (var c in parked)
                if (parked.Any(o => !ReferenceEquals(o, c) && Math.Abs(o.LapDistance - c.LapDistance) < .5f) && !spots.Any(p => Math.Abs(p - c.LapDistance) < 3))
                    spots.Add(c.LapDistance);
            return spots;
        }

        /// <summary>Nächster gedrehter oder stehender Wagen 3–8 s vor Rob (höchstens 1000 m), sonst null.</summary>
        internal static Hazard Nearest(FieldView view, DateTime now, IList<float> garage = null)
        {
            if (view == null || view.Cars == null || view.Player == null || !view.Fresh(now, 2) || view.InMenus || view.Paused || view.InReplay) return null;
            float speed = view.Player.Speed;
            var car = view.Cars.Where(c => Hazardous(c, now, speed) && (garage == null || !garage.Any(p => Math.Abs(p - c.LapDistance) < 3)))
                .OrderBy(c => c.GapM).FirstOrDefault();
            return car == null ? null : new Hazard { Name = car.Name, GapM = car.GapM, LapDistance = car.LapDistance, Stopped = car.Motion == CarMotion.Stopped };
        }

        /// <summary>Der gemeldete Wagen steht, dreht oder schleicht noch vor Rob (sonst ist die Warnung überholt).</summary>
        internal static bool StillThere(FieldView view, string name, DateTime now, float lapDistance = float.NaN)
        {
            var c = view?.ByName(name);
            // Ein Sprung der Streckenposition heißt Garage oder Zurücksetzen, nicht mehr dieselbe Gefahr.
            if (c != null && !float.IsNaN(lapDistance) && !float.IsNaN(c.LapDistance) && Math.Abs(c.LapDistance - lapDistance) > 30) return false;
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
            if (gs.SessionData.SessionId != session) { called.Clear(); garage.Clear(); times.Clear(); lastCall = DateTime.MinValue; session = gs.SessionData.SessionId; }
            if (view?.Cars != null && view.Fresh(now, 2))
                foreach (float spot in GarageSpots(view))
                    if (!garage.Any(p => Math.Abs(p - spot) < 3)) { garage.Add(spot); LiveDiagnostics.Decision("warning-hazard-far", "skipped", "garage_spot_" + (int)spot + "m"); }
            // P1.5 (LMU 25.11.): dasselbe stehende Auto kam jede Runde neu. Je Vorfall einmal und höchstens eine Erinnerung;
            // fährt der Wagen wieder normal, beginnt ein neuer Vorfall.
            foreach (var name in times.Keys.ToList())
            {
                var c = view?.ByName(name);
                if (c != null && c.Motion == CarMotion.Normal) times.Remove(name);
            }
            if (gs.PitData.InPitlane || gs.SessionData.SessionPhase != SessionPhase.Green) return null;
            if (!(gs.PositionAndMotionData.CarSpeed >= 15)) return null; // Rob steht oder rollt: keine Warnung nach vorn
            if (lastCall != DateTime.MinValue && now >= lastCall && now - lastCall < AnyAgain) return null;
            var hazard = Nearest(view, now, garage);
            if (hazard == null || string.IsNullOrWhiteSpace(hazard.Name)) return null;
            if (called.TryGetValue(hazard.Name, out var at) && now >= at && now - at < SameCarAgain) return null;
            times.TryGetValue(hazard.Name, out int count);
            if (count >= MaxCallsPerIncident) { LiveDiagnostics.Decision("warning-hazard-far", "skipped", "same_incident_already_called_twice", hazard.Name); return null; }
            times[hazard.Name] = count + 1;
            called[hazard.Name] = now; lastCall = now;
            return hazard;
        }

        internal const int MaxCallsPerIncident = 2;
        private readonly Dictionary<string, int> times = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }
}
