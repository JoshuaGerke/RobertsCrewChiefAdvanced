using System;

namespace CrewChiefV4.AiTeam
{
    /// <summary>
    /// Momentaufnahme der Crew-Chief-Spotterlogik (NoisyCartesianCoordinateSpotter).
    /// Sara verwendet ausschließlich diese lokalen, deterministischen Daten.
    /// </summary>
    public struct SpotterSnapshot
    {
        /// <summary>Wie alt die letzte Spotter-Auswertung höchstens sein darf, um daraus "frei" abzuleiten.</summary>
        public static readonly TimeSpan MaxAgeForClearance = TimeSpan.FromMilliseconds(600);

        public bool SpotterRunning;
        public int CarsLeft;
        public int CarsRight;
        public DateTime EvaluatedUtc;

        public static SpotterSnapshot None
        {
            get { return new SpotterSnapshot { SpotterRunning = false, EvaluatedUtc = DateTime.MinValue }; }
        }

        public bool IsFresh(DateTime nowUtc)
        {
            return SpotterRunning && EvaluatedUtc != DateTime.MinValue && nowUtc - EvaluatedUtc <= MaxAgeForClearance;
        }

        public bool HasOverlap
        {
            get { return CarsLeft > 0 || CarsRight > 0; }
        }
    }
}
