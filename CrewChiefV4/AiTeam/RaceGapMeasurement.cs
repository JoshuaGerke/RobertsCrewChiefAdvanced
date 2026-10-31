using System;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    public enum RaceGapKind { Unknown, SimulatorTiming, BestLapDifference, TrackDistanceEstimate }

    /// <summary>A timing-list difference is not traffic proximity; a distance estimate is not a timing measurement.</summary>
    public sealed class RaceGapMeasurement
    {
        public float Seconds { get; private set; }
        public RaceGapKind Kind { get; private set; }
        public bool HasValue { get { return Kind != RaceGapKind.Unknown; } }
        public bool IsEstimated { get { return Kind == RaceGapKind.TrackDistanceEstimate; } }
        public bool IsRaceGap { get { return Kind == RaceGapKind.SimulatorTiming || IsEstimated; } }

        public RaceGapMeasurement(float seconds = -1, RaceGapKind kind = RaceGapKind.Unknown)
        {
            bool valid = !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0 && seconds <= 3600;
            Kind = valid ? kind : RaceGapKind.Unknown;
            Seconds = Kind == RaceGapKind.Unknown ? -1 : seconds;
        }

        public JObject Describe(RaceDataContext context)
        {
            return new JObject {
                ["sekunden"] = HasValue ? (JToken)Math.Round(Seconds, IsEstimated ? 1 : 3) : null,
                ["einheit"] = "s",
                ["typ"] = Kind == RaceGapKind.SimulatorTiming ? "rennabstand_aus_spieladapter" :
                    Kind == RaceGapKind.BestLapDifference ? "differenz_persoenlicher_bestzeiten" :
                    IsEstimated ? "naeherung_aus_streckendistanz_und_rundentempo" : "unbekannt",
                ["geschaetzt"] = IsEstimated,
                // The mapped value may itself be derived by the simulator adapter. Freshness is not an accuracy guarantee.
                ["fuer_praezise_taktik_geeignet"] = false,
                ["snapshot_aktuell"] = context != null && context.TelemetryAgeSeconds >= 0 && context.TelemetryAgeSeconds <= 1,
                ["snapshot_utc"] = context == null || context.LastTelemetryUtc == DateTime.MinValue ? null : context.LastTelemetryUtc.ToString("o"),
                ["hinweis"] = IsEstimated ? "Nur eine Naeherung, kein gemessener Timing-Abstand. Nicht fuer sichere Angriffspunkte oder exakte Prognosen verwenden." :
                    Kind == RaceGapKind.BestLapDifference ? "Zeitenlistenvergleich, keine raeumliche Naehe auf der Strecke." :
                    Kind == RaceGapKind.SimulatorTiming ? "Abstand aus dem Spieladapter; kann dort berechnet sein. Keine garantierte Messgenauigkeit oder feldweise Aktualitaet." : null
            };
        }
    }
}
