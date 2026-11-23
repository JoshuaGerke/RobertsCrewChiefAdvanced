using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam.Strategy
{
    /// <summary>Messwerte für das Strategiebild, an der Ziellinie gesammelt. NaN oder null heißt: nicht gemessen, nicht geraten.</summary>
    internal sealed class StrategyInput
    {
        /// <summary>Abgeschlossene Runden; die laufende Runde ist <see cref="Lap"/> + 1.</summary>
        public int Lap;
        /// <summary>Noch zu fahrende Runden ab der Linie (bei Zeitrennen geschätzt).</summary>
        public double LapsToGo = double.NaN;
        public bool LapsToGoEstimated;
        public double FuelLeft = double.NaN, FuelCapacity = double.NaN, FuelPerLap = double.NaN;
        /// <summary>Gegenprobe: Median des selbst gemessenen Verbrauchs der letzten sauberen Runden.</summary>
        public double FuelPerLapMeasured = double.NaN;
        public bool FuelProvisional, RefuelAllowed = true;
        /// <summary>Virtuelle Energie (LMU) in Prozent und Prozent pro Runde.</summary>
        public double EnergyLeft = double.NaN, EnergyPerLap = double.NaN;
        public float[] Wear, WearPerLap;
        public bool WearCalibrated;
        /// <summary>Saubere Runden des laufenden Stints: Rundennummer und Zeit in Sekunden, aufsteigend.</summary>
        public IList<KeyValuePair<int, double>> StintLaps = new List<KeyValuePair<int, double>>();
        public double PitLoss = double.NaN;
        public bool MandatoryStopPending;
        public int StopsMade;
    }

    /// <summary>
    /// Renn-Gesamtbild der Strategie (Robs Auftrag 23.11.2026, Testkanal B): Reichweite aus Sprit, virtueller Energie und Reifen,
    /// was davon begrenzt, ob es ohne Stopp reicht, das Boxenfenster für einen Stopp und der Tempoverlauf im Stint.
    /// Rechnet nur mit Messwerten. Passt der Verbrauch der Sim-Rechnung nicht zur eigenen Messung (mehr als 15 % Abweichung),
    /// gilt der Sprit als ungeprüft und die Crew nennt dazu keine Zahl.
    /// </summary>
    internal sealed class StrategyPicture
    {
        internal const double FuelReserveLaps = .2, MaxSaving = .08, TyreSoftMargin = .15, CheckTolerance = .15;
        internal const double FadeSlope = .07, FadeTotal = .25;

        public int Lap;
        public double LapsToGo = double.NaN;
        public bool LapsToGoEstimated;
        public double FuelLaps = double.NaN, EnergyLaps = double.NaN, TyreLaps = double.NaN;
        public double FullTankLaps = double.NaN, FullEnergyLaps = double.NaN, FreshTyreLaps = double.NaN;
        public int TyreCorner = -1;
        public bool FuelChecked;
        /// <summary>sprit, energie oder reifen.</summary>
        public string Limit;
        public double RangeLaps = double.NaN, Margin = double.NaN;
        /// <summary>durchfahren, sparen, reifen_schonen, ein_stopp, zwei_stopps, pflichtstopp, unklar.</summary>
        public string Plan = "unklar";
        public string PlanReason;
        public double FuelTargetPerLap = double.NaN, FuelSavePerLap = double.NaN;
        public double EnergyTargetPerLap = double.NaN, EnergySavePerLap = double.NaN;
        /// <summary>Stopp am Ende dieser Runden (Rundennummern); -1 ohne Fenster.</summary>
        public int WindowFirstLap = -1, WindowLastLap = -1;
        public double PaceSlope = double.NaN, PaceNoise = double.NaN;
        public int PaceLaps;
        /// <summary>baut_ab, stabil, wird_schneller oder null bei zu wenigen Runden.</summary>
        public string PaceTrend;
        public double PitLoss = double.NaN;
        public readonly List<string> Missing = new List<string>();

        internal bool WindowKnown => WindowFirstLap > 0 && WindowLastLap >= WindowFirstLap;
        internal bool HardLimit => Limit == "sprit" || Limit == "energie";

        private static bool Finite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        internal static StrategyPicture Compute(StrategyInput i)
        {
            var p = new StrategyPicture();
            if (i == null) { p.Missing.Add("eingaben"); return p; }
            p.Lap = i.Lap;
            p.LapsToGo = i.LapsToGo;
            p.LapsToGoEstimated = i.LapsToGoEstimated;
            p.PitLoss = i.PitLoss;
            bool toGoKnown = Finite(i.LapsToGo) && i.LapsToGo > 0;
            if (!toGoKnown) p.Missing.Add("restdistanz");

            // Sprit mit Gegenprobe.
            if (Finite(i.FuelLeft) && i.FuelLeft >= 0 && Finite(i.FuelPerLap) && i.FuelPerLap > 0)
            {
                p.FuelChecked = !i.FuelProvisional && (!Finite(i.FuelPerLapMeasured) || i.FuelPerLapMeasured <= 0 ||
                    Math.Abs(i.FuelPerLap - i.FuelPerLapMeasured) <= CheckTolerance * Math.Max(i.FuelPerLap, i.FuelPerLapMeasured));
                p.FuelLaps = i.FuelLeft / i.FuelPerLap;
                if (Finite(i.FuelCapacity) && i.FuelCapacity > 0) p.FullTankLaps = i.FuelCapacity / i.FuelPerLap;
                if (!p.FuelChecked) p.Missing.Add(i.FuelProvisional ? "verbrauch_vorlaeufig" : "verbrauch_gegenprobe_abweichend");
            }
            else p.Missing.Add("verbrauch");

            if (Finite(i.EnergyLeft) && i.EnergyLeft >= 0 && Finite(i.EnergyPerLap) && i.EnergyPerLap > .05)
            {
                p.EnergyLaps = i.EnergyLeft / i.EnergyPerLap;
                p.FullEnergyLaps = 100 / i.EnergyPerLap;
            }

            if (i.WearCalibrated && i.Wear != null && i.WearPerLap != null)
            {
                int corner;
                int laps = TyreWearAdvisor.LapsToEnd(i.Wear, i.WearPerLap, true, out corner);
                if (laps >= 0) { p.TyreLaps = laps; p.TyreCorner = corner; }
                int freshCorner;
                int fresh = TyreWearAdvisor.LapsToEnd(new float[4], i.WearPerLap, true, out freshCorner);
                if (fresh > 0) p.FreshTyreLaps = fresh;
            }

            // Begrenzung: das Kleinste aus Sprit (nur geprüft), Energie und Reifen.
            var limits = new List<KeyValuePair<string, double>>();
            if (Finite(p.FuelLaps) && p.FuelChecked) limits.Add(new KeyValuePair<string, double>("sprit", p.FuelLaps));
            if (Finite(p.EnergyLaps)) limits.Add(new KeyValuePair<string, double>("energie", p.EnergyLaps));
            if (Finite(p.TyreLaps)) limits.Add(new KeyValuePair<string, double>("reifen", p.TyreLaps));
            if (limits.Count > 0)
            {
                var min = limits.OrderBy(l => l.Value).First();
                p.Limit = min.Key;
                p.RangeLaps = min.Value;
            }
            Pace(i, p);
            if (!toGoKnown || limits.Count == 0) return p;
            p.Margin = p.RangeLaps - i.LapsToGo;

            // Was reicht, wenn wir nicht stoppen?
            var hard = limits.Where(l => l.Key != "reifen").ToList();
            double hardRange = hard.Count > 0 ? hard.Min(l => l.Value) : double.PositiveInfinity;
            string hardKey = hard.Count > 0 ? hard.OrderBy(l => l.Value).First().Key : null;
            bool hardReaches = hardRange >= i.LapsToGo + FuelReserveLaps;
            bool tyresReach = !Finite(p.TyreLaps) || p.TyreLaps >= i.LapsToGo;
            if (hardReaches && tyresReach)
            {
                p.Plan = i.MandatoryStopPending ? "pflichtstopp" : "durchfahren";
                p.PlanReason = i.MandatoryStopPending ? "pflichtstopp_offen" : hardKey;
            }
            else if (hardReaches && Finite(p.TyreLaps) && p.TyreLaps >= i.LapsToGo * (1 - TyreSoftMargin))
            {
                p.Plan = i.MandatoryStopPending ? "pflichtstopp" : "reifen_schonen";
                p.PlanReason = "reifen";
            }
            else if (!hardReaches && hardRange >= (i.LapsToGo + FuelReserveLaps) * (1 - MaxSaving) && tyresReach && !i.MandatoryStopPending)
            {
                p.Plan = "sparen";
                p.PlanReason = hardKey;
            }
            else
            {
                p.Plan = "ein_stopp";
                p.PlanReason = hardReaches ? "reifen" : hardKey;
            }
            if (p.Plan == "sparen" || !hardReaches)
            {
                if (Finite(p.FuelLaps) && p.FuelChecked && p.FuelLaps < i.LapsToGo + FuelReserveLaps)
                {
                    p.FuelTargetPerLap = i.FuelLeft / (i.LapsToGo + FuelReserveLaps);
                    p.FuelSavePerLap = i.FuelPerLap - p.FuelTargetPerLap;
                }
                if (Finite(p.EnergyLaps) && p.EnergyLaps < i.LapsToGo + FuelReserveLaps)
                {
                    p.EnergyTargetPerLap = i.EnergyLeft / (i.LapsToGo + FuelReserveLaps);
                    p.EnergySavePerLap = i.EnergyPerLap - p.EnergyTargetPerLap;
                }
            }

            if (p.Plan == "ein_stopp" || p.Plan == "pflichtstopp")
            {
                // Reichweite nach dem Stopp: was beim Stopp erneuert wird (Sprit nur mit Nachtanken).
                double after = double.PositiveInfinity;
                if (Finite(p.FuelLaps) && p.FuelChecked)
                    after = Math.Min(after, i.RefuelAllowed && Finite(p.FullTankLaps) ? p.FullTankLaps : Math.Max(0, p.FuelLaps));
                if (Finite(p.EnergyLaps)) after = Math.Min(after, Finite(p.FullEnergyLaps) ? p.FullEnergyLaps : p.EnergyLaps);
                if (Finite(p.FreshTyreLaps)) after = Math.Min(after, p.FreshTyreLaps);
                double now = p.RangeLaps;
                if (!hardReaches) now = Math.Min(now, hardRange);
                int earliest = Math.Max(1, (int)Math.Ceiling(i.LapsToGo + FuelReserveLaps - after));
                int latest = (int)Math.Floor(Math.Min(now, i.LapsToGo) - .1);
                if (p.Plan == "pflichtstopp") latest = Math.Max(1, Math.Min(latest, (int)Math.Floor(i.LapsToGo) - 1));
                if (double.IsPositiveInfinity(after)) earliest = 1;
                if (latest < 1)
                {
                    // Es reicht nur noch diese Runde: Stopp am Ende der laufenden Runde.
                    p.WindowFirstLap = p.WindowLastLap = i.Lap + 1;
                    if (earliest > 1 && !double.IsPositiveInfinity(after)) p.Plan = "zwei_stopps";
                }
                else if (latest >= earliest)
                {
                    p.WindowFirstLap = i.Lap + earliest;
                    p.WindowLastLap = i.Lap + latest;
                }
                else
                {
                    p.Plan = "zwei_stopps";
                    p.WindowLastLap = i.Lap + latest;
                }
            }
            return p;
        }

        // Theil-Sen: Median aller Paarsteigungen, unempfindlich gegen einzelne Verkehrs- oder Fehlerrunden.
        private static void Pace(StrategyInput i, StrategyPicture p)
        {
            var laps = i.StintLaps?.Where(l => Finite(l.Value) && l.Value > 10 && l.Value < 3600).OrderBy(l => l.Key).ToList();
            if (laps == null || laps.Count < 4) { p.Missing.Add("pace_trend (mindestens 4 saubere Runden im Stint)"); return; }
            var slopes = new List<double>();
            for (int a = 0; a < laps.Count; a++)
                for (int b = a + 1; b < laps.Count; b++)
                    if (laps[b].Key != laps[a].Key) slopes.Add((laps[b].Value - laps[a].Value) / (laps[b].Key - laps[a].Key));
            if (slopes.Count == 0) return;
            double slope = Median(slopes);
            double intercept = Median(laps.Select(l => l.Value - slope * l.Key).ToList());
            double noise = Median(laps.Select(l => Math.Abs(l.Value - (intercept + slope * l.Key))).ToList());
            int span = laps.Last().Key - laps.First().Key;
            p.PaceSlope = slope;
            p.PaceNoise = noise;
            p.PaceLaps = laps.Count;
            double total = Math.Abs(slope) * span;
            bool clear = total >= FadeTotal && total >= 1.5 * noise;
            p.PaceTrend = clear && slope >= FadeSlope ? "baut_ab" : clear && slope <= -FadeSlope ? "wird_schneller" : "stabil";
        }

        internal static double Median(IList<double> values)
        {
            var v = values.OrderBy(x => x).ToList();
            if (v.Count == 0) return double.NaN;
            return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
        }

        private static JToken N(double v, int digits) => Finite(v) ? (JToken)Math.Round(v, digits) : null;

        internal JObject ToJson()
        {
            var o = new JObject
            {
                ["runde"] = Lap,
                ["restrunden"] = N(LapsToGo, 1), ["restrunden_geschaetzt"] = LapsToGoEstimated,
                ["plan"] = Plan, ["plan_grund"] = PlanReason,
                ["begrenzt_durch"] = Limit, ["reichweite_runden"] = N(RangeLaps, 1), ["luft_runden"] = N(Margin, 1),
                ["sprit"] = new JObject
                {
                    ["reichweite_runden"] = N(FuelLaps, 1), ["voller_tank_runden"] = N(FullTankLaps, 1),
                    ["gegenprobe_ok"] = FuelChecked, ["ziel_pro_runde_l"] = N(FuelTargetPerLap, 2), ["sparen_pro_runde_l"] = N(FuelSavePerLap, 2)
                },
                ["energie"] = Finite(EnergyLaps) ? new JObject
                {
                    ["reichweite_runden"] = N(EnergyLaps, 1), ["volle_energie_runden"] = N(FullEnergyLaps, 1),
                    ["ziel_pro_runde_prozent"] = N(EnergyTargetPerLap, 2), ["sparen_pro_runde_prozent"] = N(EnergySavePerLap, 2)
                } : null,
                ["reifen"] = Finite(TyreLaps) || Finite(FreshTyreLaps) ? new JObject
                {
                    ["runden_bis_ende"] = N(TyreLaps, 0), ["kritische_ecke"] = TyreCorner >= 0 ? new[] { "VL", "VR", "HL", "HR" }[TyreCorner] : null,
                    ["neue_reifen_runden"] = N(FreshTyreLaps, 0), ["grenze"] = "90 % der normierten Verschleißgrenze"
                } : null,
                ["boxenfenster"] = WindowKnown || WindowLastLap > 0 ? new JObject
                {
                    ["fruehestens_ende_runde"] = WindowFirstLap > 0 ? (JToken)WindowFirstLap : null,
                    ["spaetestens_ende_runde"] = WindowLastLap > 0 ? (JToken)WindowLastLap : null,
                    ["boxenverlust_s"] = N(PitLoss, 1)
                } : null,
                ["tempo"] = PaceTrend == null ? null : new JObject
                {
                    ["trend"] = PaceTrend, ["sekunden_pro_runde"] = N(PaceSlope, 3), ["streuung_s"] = N(PaceNoise, 3), ["runden"] = PaceLaps,
                    ["ursache_bestaetigt"] = false
                }
            };
            if (Missing.Count > 0) o["fehlt"] = new JArray(Missing.Distinct());
            o["hinweis"] = "Nur Messwerte. „sparen“ heißt: höchstens 8 % weniger pro Runde reichen. Fenster = Stopp am Ende dieser Runden. " +
                "Tempotrend ist eine Messung, keine Ursache. Ohne Gegenprobe beim Sprit keine Spritzahlen nennen.";
            return o;
        }
    }
}
