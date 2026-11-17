using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Virtuelle Energie in LMU (Rob 17.11.2026, Phase B): In Klassen mit Energie-Limit begrenzt nicht der Sprit, sondern die
    /// virtuelle Energie die Reichweite. Crew Chief liest den Stand bereits über seinen bestehenden LMU-Datenweg
    /// (<see cref="LMU.VirtualEnergy"/>, 0–100 %, sonst „nicht genutzt“). Hier misst die Crew den Verbrauch pro sauberer Runde,
    /// damit Mik Reichweite und begrenzenden Faktor (Energie oder Sprit) nennen kann – nur mit gemessenen Runden.
    /// </summary>
    internal sealed class VirtualEnergyWatch
    {
        private static VirtualEnergyWatch current = new VirtualEnergyWatch();
        internal static VirtualEnergyWatch Current => current;
        internal static void Use(VirtualEnergyWatch watch) { current = watch ?? new VirtualEnergyWatch(); }

        private readonly object gate = new object();
        private readonly List<float> perLap = new List<float>();
        private int session = int.MinValue, lapAtStart = -1;
        private float atStart = -1, level = -1;
        private bool startClean;

        internal void Reset() { lock (gate) { perLap.Clear(); session = int.MinValue; lapAtStart = -1; atStart = level = -1; startClean = false; } }

        /// <summary>Aktueller Stand in Prozent oder -1, wenn die Simulation keine virtuelle Energie nutzt.</summary>
        internal float Level { get { lock (gate) return level; } }

        /// <summary>Gemessener Durchschnitt pro Runde (Prozentpunkte) aus bis zu drei sauberen Runden; -1 ohne Messung.</summary>
        internal float PerLap { get { lock (gate) return perLap.Count == 0 ? -1 : perLap.Skip(Math.Max(0, perLap.Count - 3)).Average(); } }

        /// <summary>Runden, die die Energie bei gleichem Verbrauch noch reicht; -1 ohne Messung.</summary>
        internal float LapsLeft
        {
            get
            {
                float l = Level, p = PerLap;
                return l >= 0 && p > .05f ? l / p : -1;
            }
        }

        internal static float Read() { int ve = LMU.VirtualEnergy.Read(); return ve >= 0 && ve <= 100 ? ve : -1; }

        /// <summary>Wird je Takt mit dem gelesenen Stand gefüttert (für Tests direkt aufrufbar).</summary>
        internal void Observe(GameStateData gs, float energy)
        {
            if (gs == null) return;
            var s = gs.SessionData;
            lock (gate)
            {
                if (s.SessionId != session) { perLap.Clear(); session = s.SessionId; lapAtStart = -1; atStart = -1; startClean = false; }
                if (energy < 0 || energy > 100) { level = -1; lapAtStart = -1; atStart = -1; return; }
                if (level >= 0 && energy > level + .5f) { lapAtStart = -1; atStart = -1; } // nachgeladen (Box): neue Messung beginnen
                level = energy;
                int lap = s.CompletedLaps;
                bool clean = s.SessionPhase == SessionPhase.Green && !gs.PitData.InPitlane && !gs.PitData.OnOutLap && !gs.PitData.OnInLap;
                if (lapAtStart < 0) { lapAtStart = lap; atStart = energy; startClean = clean; return; }
                if (lap == lapAtStart) { if (!clean) startClean = false; return; }
                if (lap == lapAtStart + 1 && startClean && clean && s.PreviousLapWasValid)
                {
                    float used = atStart - energy;
                    if (used > .05f && used < 50f) { perLap.Add(used); if (perLap.Count > 10) perLap.RemoveAt(0); }
                }
                lapAtStart = lap; atStart = energy; startClean = clean;
            }
        }

        /// <summary>Kurzer Funksatz: Stand, Verbrauch, Reichweite und – wenn Sprit knapper ist – der begrenzende Faktor.</summary>
        internal static string Report(float level, float perLap, float fuelLaps, int variant)
        {
            if (level < 0) return null;
            string pct = RadioLanguage.Num(level, "0");
            if (!(perLap > .05f))
                return RadioLanguage.T("Virtuelle Energie " + pct + " Prozent, Verbrauch messen wir noch.",
                    "Virtual energy " + pct + " percent, still measuring consumption.",
                    "Energía virtual " + pct + " por ciento, aún medimos el consumo.");
            float laps = level / perLap;
            string l = RadioLanguage.Num(laps, "0.0"), p = RadioLanguage.Num(perLap, "0.0");
            string text = RadioVariants.At(variant,
                RadioLanguage.T("Virtuelle Energie " + pct + " Prozent, " + p + " pro Runde – das reicht für etwa " + l + " Runden.",
                    "Virtual energy " + pct + " percent, " + p + " a lap – that's about " + l + " laps.",
                    "Energía virtual " + pct + " por ciento, " + p + " por vuelta: da para unas " + l + " vueltas."),
                RadioLanguage.T("Energie reicht noch etwa " + l + " Runden: " + pct + " Prozent übrig, " + p + " pro Runde.",
                    "Energy lasts about " + l + " more laps: " + pct + " percent left, " + p + " a lap.",
                    "La energía dura unas " + l + " vueltas más: queda un " + pct + " por ciento, " + p + " por vuelta."),
                RadioLanguage.T("Energie-Stand: " + pct + " Prozent. Bei " + p + " pro Runde sind das rund " + l + " Runden.",
                    "Energy status: " + pct + " percent. At " + p + " a lap that's around " + l + " laps.",
                    "Estado de energía: " + pct + " por ciento. A " + p + " por vuelta son unas " + l + " vueltas."),
                RadioLanguage.T("Virtuelle Energie: " + pct + " Prozent im Speicher, Reichweite etwa " + l + " Runden bei " + p + " je Runde.",
                    "Virtual energy: " + pct + " percent in store, range about " + l + " laps at " + p + " per lap.",
                    "Energía virtual: " + pct + " por ciento disponible, autonomía de unas " + l + " vueltas a " + p + " por vuelta."));
            if (fuelLaps > 0 && Math.Abs(fuelLaps - laps) >= .5f)
                text += " " + (laps < fuelLaps
                    ? RadioLanguage.T("Die Energie ist knapper als der Sprit.", "Energy is tighter than fuel.", "La energía es más justa que el combustible.")
                    : RadioLanguage.T("Hier ist der Sprit knapper als die Energie.", "Here fuel is tighter than energy.", "Aquí el combustible es más justo que la energía."));
            return text;
        }
    }
}
