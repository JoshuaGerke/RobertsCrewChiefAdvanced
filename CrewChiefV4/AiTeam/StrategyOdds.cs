using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Messwerte für die Stopp-Rechnung. NaN heißt: nicht gemessen, wird nicht geraten.</summary>
    internal sealed class StrategyInputs
    {
        /// <summary>Restrunden bis zum Ziel und deren Unsicherheit (bei Zeitrennen geschätzt).</summary>
        public double LapsToGo = double.NaN, LapsToGoSpread;
        /// <summary>Reichweite des Tanks in Runden und deren Streuung aus dem gemessenen Verbrauch.</summary>
        public double FuelLaps = double.NaN, FuelLapsSpread;
        /// <summary>Anteil der laufenden Runde bis zur Boxeneinfahrt (1 = ganze Runde).</summary>
        public double LapFractionToPitEntry = 1;
        /// <summary>Gemessener Boxenverlust in Sekunden und dessen Streuung.</summary>
        public double PitLoss = double.NaN, PitLossSpread;
        /// <summary>Abstand zum Auto dahinter in Sekunden und wie viel es pro Runde aufholt (positiv = es wird enger).</summary>
        public double GapBehind = double.NaN, RearGainPerLap, RearGainSpread;
        public bool LapsToGoEstimated;
    }

    internal sealed class StrategyOption
    {
        public int StopInLaps;
        /// <summary>Wahrscheinlichkeit, dass der Sprit bis zur Boxeneinfahrt reicht.</summary>
        public double FuelReaches;
        /// <summary>Wahrscheinlichkeit, vor dem Auto dahinter wieder rauszukommen; NaN ohne Messung.</summary>
        public double AheadOfRearCar = double.NaN;
    }

    internal sealed class StrategyResult
    {
        public double NoStopReachesFinish = double.NaN;
        public readonly List<StrategyOption> Options = new List<StrategyOption>();
        public string Recommendation;
        public int Runs;
    }

    /// <summary>
    /// C6 Strategie-Wahrscheinlichkeiten: kleiner Monte-Carlo-Kern für „Stopp jetzt, in einer oder in zwei Runden“ und
    /// „reicht der Sprit bis ins Ziel“. Jede Option wird mit denselben Zufallszahlen gerechnet, damit Unterschiede zwischen
    /// den Optionen nicht aus dem Zufall kommen. Nur gemessene Werte gehen ein; was fehlt, bleibt offen.
    /// Ben nennt die Werte nur gerundet (5-%-Schritte) und mit Empfehlung.
    /// </summary>
    internal static class StrategyOdds
    {
        internal const int Runs = 4000;
        internal const double Safe = 0.9;

        internal static StrategyResult Evaluate(StrategyInputs i, int seed = 20261118)
        {
            var result = new StrategyResult { Runs = Runs };
            if (i == null || !Finite(i.FuelLaps) || i.FuelLaps < 0) return result;
            var rng = new Random(seed);
            bool finishKnown = Finite(i.LapsToGo) && i.LapsToGo > 0;
            bool rearKnown = Finite(i.PitLoss) && i.PitLoss > 0 && Finite(i.GapBehind) && i.GapBehind >= 0;
            int maxStop = finishKnown ? Math.Max(0, Math.Min(2, (int)Math.Floor(i.LapsToGo) - 1)) : 2;
            int[] fuelOk = new int[maxStop + 1], ahead = new int[maxStop + 1];
            int noStop = 0;
            for (int run = 0; run < Runs; run++)
            {
                // Gemeinsame Zufallszahlen: ein Satz Ziehungen je Lauf für alle Optionen.
                double fuel = i.FuelLaps + Math.Max(0, i.FuelLapsSpread) * Gauss(rng);
                double toGo = finishKnown ? i.LapsToGo + Math.Max(0, i.LapsToGoSpread) * Gauss(rng) : double.NaN;
                double loss = rearKnown ? Math.Max(0, i.PitLoss + Math.Max(0, i.PitLossSpread) * Gauss(rng)) : double.NaN;
                double gain = i.RearGainPerLap + Math.Max(0, i.RearGainSpread) * Gauss(rng);
                if (finishKnown && fuel >= toGo) noStop++;
                for (int k = 0; k <= maxStop; k++)
                {
                    if (fuel >= k + i.LapFractionToPitEntry) fuelOk[k]++;
                    if (rearKnown && i.GapBehind - k * gain > loss) ahead[k]++;
                }
            }
            if (finishKnown) result.NoStopReachesFinish = noStop / (double)Runs;
            for (int k = 0; k <= maxStop; k++)
                result.Options.Add(new StrategyOption { StopInLaps = k, FuelReaches = fuelOk[k] / (double)Runs,
                    AheadOfRearCar = rearKnown ? ahead[k] / (double)Runs : double.NaN });
            result.Recommendation = Recommend(result);
            return result;
        }

        /// <summary>Durchfahren, wenn es sicher reicht; sonst der Stopp mit sicherem Sprit, der am ehesten vor dem Hintermann
        /// landet, bei Gleichstand oder ohne Messung der späteste sichere.</summary>
        internal static string Recommend(StrategyResult r)
        {
            if (Finite(r.NoStopReachesFinish) && r.NoStopReachesFinish >= Safe) return "durchfahren";
            var safe = r.Options.Where(o => o.FuelReaches >= Safe).ToList();
            if (safe.Count == 0) return r.Options.Count > 0 ? "sofort_rein" : null;
            var best = safe.OrderByDescending(o => Finite(o.AheadOfRearCar) ? Round5(o.AheadOfRearCar) : 0).ThenByDescending(o => o.StopInLaps).First();
            return best.StopInLaps == 0 ? "stopp_jetzt" : "stopp_in_" + best.StopInLaps;
        }

        /// <summary>Auf 5 % gerundet, als ganze Prozentzahl.</summary>
        internal static int Round5(double p) => (int)(Math.Round(Math.Max(0, Math.Min(1, p)) * 20) * 5);

        internal static JObject Describe(StrategyResult r, StrategyInputs i)
        {
            var o = new JObject { ["laeufe"] = r.Runs, ["methode"] = "Monte Carlo mit gleichen Zufallszahlen je Option, nur Messwerte" };
            if (Finite(r.NoStopReachesFinish)) o["durchfahren_reicht_prozent"] = Round5(r.NoStopReachesFinish);
            var options = new JArray();
            foreach (var opt in r.Options)
            {
                var j = new JObject { ["stopp_in_runden"] = opt.StopInLaps, ["sprit_reicht_bis_box_prozent"] = Round5(opt.FuelReaches) };
                if (Finite(opt.AheadOfRearCar)) j["vor_dem_hintermann_prozent"] = Round5(opt.AheadOfRearCar);
                options.Add(j);
            }
            o["optionen"] = options;
            if (r.Recommendation != null) o["empfehlung"] = r.Recommendation;
            if (i != null && i.LapsToGoEstimated) o["restrunden_geschaetzt"] = true;
            o["hinweis"] = "Nur gerundet und mit Empfehlung nennen (z. B. „etwa 80 Prozent“). Fehlende Größen sind nicht gemessen, nicht schätzen.";
            return o;
        }

        private static double Gauss(Random rng)
        {
            double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        /// <summary>Streuung (Standardabweichung) einer Stichprobe; 0 bei weniger als zwei Werten.</summary>
        internal static double Spread(IList<double> values)
        {
            if (values == null || values.Count < 2) return 0;
            double mean = values.Average();
            return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1));
        }
    }
}
