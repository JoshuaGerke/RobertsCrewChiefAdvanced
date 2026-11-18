using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.GameState;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Last delivered facts per routine topic, independent of wording and reset with the session.</summary>
    internal sealed class StintRadioMemory
    {
        internal sealed class Report
        {
            public string Identity;
            public double[] Values, Thresholds;
            public DateTime Time;
            public int Lap;
        }

        private readonly Dictionary<string, Report> delivered = new Dictionary<string, Report>();
        internal void Clear() => delivered.Clear();
        internal void Forget(string topic) => delivered.Remove(topic);
        internal void Remember(string topic, Report report, DateTime time)
        {
            if (report == null) return;
            report.Time = time;
            delivered[topic] = report;
        }

        internal bool Unchanged(string topic, Report report, DateTime time)
        {
            if (!Comparable(topic, report, time, out var previous)) return false;
            if (report.Lap - previous.Lap >= 3 || time - previous.Time >= TimeSpan.FromMinutes(5)) return false;
            for (int i = 0; i < report.Values.Length; i++)
            {
                double old = previous.Values[i], next = report.Values[i];
                if (double.IsNaN(old) != double.IsNaN(next) || Math.Abs(next - old) >= report.Thresholds[i]) return false;
            }
            return true;
        }

        internal string Trend(string topic, Report report, DateTime time)
        {
            if (!Comparable(topic, report, time, out var previous)) return null;
            double delta = report.Values[0] - previous.Values[0];
            if (double.IsNaN(delta) || Math.Abs(delta) < report.Thresholds[0]) return null;
            if (topic == "pace") return delta < 0 ? RadioVariants.Pick("memory-pace-faster",
                RadioLanguage.T("Der Schnitt ist schneller als beim letzten Bericht.", "Average pace is quicker than at the last report.", "El ritmo medio es más rápido que en el último informe."),
                RadioLanguage.T("Seit dem letzten Bericht bist du im Schnitt schneller.", "Since the last report you're quicker on average.", "Desde el último informe vas más rápido de media."),
                RadioLanguage.T("Der Schnitt hat sich verbessert.", "The average has improved.", "La media ha mejorado."),
                RadioLanguage.T("Im Mittel schneller als zuletzt, gut so.", "Quicker on average than last time, good.", "De media más rápido que antes, bien.")) :
                RadioVariants.Pick("memory-pace-slower",
                RadioLanguage.T("Der Schnitt ist langsamer als beim letzten Bericht.", "Average pace is slower than at the last report.", "El ritmo medio es más lento que en el último informe."),
                RadioLanguage.T("Seit dem letzten Bericht bist du im Schnitt langsamer.", "Since the last report you're slower on average.", "Desde el último informe vas más lento de media."),
                RadioLanguage.T("Der Schnitt ist etwas abgefallen.", "The average has dropped off a bit.", "La media ha caído un poco."),
                RadioLanguage.T("Im Mittel langsamer als zuletzt.", "Slower on average than last time.", "De media más lento que antes."));
            if (topic == "stint") return delta < 0 ? RadioVariants.Pick("memory-fuel-less",
                RadioLanguage.T("Pro Runde verbrauchen wir weniger.", "We're using less fuel per lap.", "Gastamos menos por vuelta."),
                RadioLanguage.T("Der Verbrauch pro Runde ist gesunken.", "Fuel use per lap has come down.", "El consumo por vuelta ha bajado."),
                RadioLanguage.T("Wir brauchen jetzt weniger Sprit pro Runde.", "We need less fuel per lap now.", "Ahora necesitamos menos combustible por vuelta."),
                RadioLanguage.T("Weniger Verbrauch pro Runde als zuletzt.", "Less fuel per lap than last time.", "Menos consumo por vuelta que antes.")) :
                RadioVariants.Pick("memory-fuel-more",
                RadioLanguage.T("Der Verbrauch pro Runde ist gestiegen.", "Fuel use per lap has gone up.", "El consumo por vuelta ha subido."),
                RadioLanguage.T("Pro Runde verbrauchen wir mehr.", "We're using more fuel per lap.", "Gastamos más por vuelta."),
                RadioLanguage.T("Wir brauchen jetzt mehr Sprit pro Runde.", "We need more fuel per lap now.", "Ahora necesitamos más combustible por vuelta."),
                RadioLanguage.T("Mehr Verbrauch pro Runde als zuletzt.", "More fuel per lap than last time.", "Más consumo por vuelta que antes."));
            return null;
        }

        private bool Comparable(string topic, Report report, DateTime time, out Report previous)
        {
            previous = null;
            return report != null && delivered.TryGetValue(topic, out previous) &&
                report.Identity == previous.Identity && previous.Values.Length == report.Values.Length &&
                time >= previous.Time && time - previous.Time < TimeSpan.FromMinutes(10) && report.Lap >= previous.Lap;
        }

        private static double Valid(double value, double low, double high) => TrackAwareness.InRange(value, low, high) ?? double.NaN;

        internal static Report Capture(string topic, RaceDataContext ctx)
        {
            var gs = ctx.GameState;
            var session = gs.SessionData;
            var t = gs.TyreData;
            var e = gs.EngineData;
            Report Make(string identity, double[] values, params double[] thresholds) =>
                new Report { Identity = identity, Values = values, Thresholds = thresholds, Lap = session.CompletedLaps };

            switch (topic)
            {
                case "fuel":
                    double consumption = Valid(ctx.Memory?.AverageFuelPerLap() ?? -1, .1, 100);
                    return double.IsNaN(consumption) ? null : Make("tank",
                        new[] { Valid(gs.FuelData.FuelLeft, 0, 1000), consumption }, 1.5, .2);
                case "stint":
                    var wear = ctx.Memory?.AverageWearPerLap();
                    return Make("stint", new[] { Valid(ctx.Memory?.AverageFuelPerLap() ?? -1, .1, 100),
                        wear != null && wear.Length == 4 ? Valid(wear.Max(), 0, 100) : double.NaN }, .2, .5);
                case "tyres":
                    return Make("wear", new double[] { t.FrontLeftPercentWear, t.FrontRightPercentWear,
                        t.RearLeftPercentWear, t.RearRightPercentWear }, 2, 2, 2, 2);
                case "vehicle":
                    return Make(IncidentWatch.DamageReport(gs.CarDamageData, true), new[] {
                        Valid(e.EngineWaterTemp, 1, 200), Valid(e.EngineOilTemp, 1, 250),
                        Valid(t.LeftFrontBrakeTemp, 1, 1500), Valid(t.RightFrontBrakeTemp, 1, 1500),
                        Valid(t.LeftRearBrakeTemp, 1, 1500), Valid(t.RightRearBrakeTemp, 1, 1500) }, 5, 5, 40, 40, 40, 40);
                case "pace":
                    var laps = ctx.Memory?.GetLaps(3);
                    return laps?.Count == 3 ? Make("pace", new[] { laps.Average(l => (double)l.LapTime),
                        laps.Max(l => l.LapTime) - (double)laps.Min(l => l.LapTime) }, .2, .3) : null;
                case "ahead":
                case "behind":
                    bool front = topic == "ahead";
                    var rival = RaceDataTools.Neighbour(gs, front);
                    var gap = RaceDataTools.ReadGap(ctx, rival, front);
                    return rival == null || !gap.HasValue ? null : Make(
                        RaceReader.Identity(front ? session.OpponentKeyInFront : session.OpponentKeyBehind, rival) + "|" + gap.Kind,
                        new double[] { gap.Seconds }, .45);
                default:
                    return null;
            }
        }
    }
}
