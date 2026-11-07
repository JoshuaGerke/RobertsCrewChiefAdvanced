using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CrewChiefV4.GameState;
using MathNet.Numerics;
using Newtonsoft.Json.Linq;

namespace CrewChiefV4.AiTeam
{
    /// <summary>Two-Hz immutable race evidence. Existing readers and safety directors retain their own cadence.</summary>
    public sealed class RaceReader
    {
        private sealed class TyreObservation
        {
            public string Identity;
            public TyreType Compound;
            public float ChangedGameTime = -1;
        }
        private sealed class Snapshot
        {
            public int Session, Lap, Revision;
            public SessionType Type;
            public DateTime SourceUtc;
            public float SimTime;
            public string Track;
            public JObject Data, Delta;
        }
        private readonly Dictionary<string, TyreObservation> tyres = new Dictionary<string, TyreObservation>();
        private readonly Dictionary<string, DateTime> lastSpoken = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, RadioVariantCursor> variants = new Dictionary<string, RadioVariantCursor>();
        private Snapshot snapshot;
        private ObservedRaceCall pending;
        private DateTime lastAggregate = DateTime.MinValue, lastWeatherSample = DateTime.MinValue;
        private Conditions.ConditionsSample weatherBaseline;
        private int session = int.MinValue, revision;
        private SessionType type;

        public void Reset()
        {
            tyres.Clear();
            lastSpoken.Clear();
            variants.Clear();
            pending = null;
            snapshot = null;
            lastAggregate = lastWeatherSample = DateTime.MinValue;
            weatherBaseline = null;
            session = int.MinValue;
            revision = 0;
        }

        public bool IsDue(DateTime now) => now < lastAggregate || now - lastAggregate >= TimeSpan.FromMilliseconds(500);

        public bool Observe(RaceDataContext context, DriverWorkload workload)
        {
            if (context?.GameState == null) return false;
            var gs = context.GameState;
            var s = gs.SessionData;
            DateTime now = context.NowUtc;
            DateTime source = gs.SourceTelemetryUtc == DateTime.MinValue ? context.LastTelemetryUtc : gs.SourceTelemetryUtc;
            if (now < source || now - source > TimeSpan.FromSeconds(1)) return false;
            string track = s.TrackDefinition?.name ?? gs.trackName;
            var prior = Volatile.Read(ref snapshot);
            if (session != s.SessionId || type != s.SessionType || prior != null && prior.Track != track ||
                now < lastAggregate || lastAggregate != DateTime.MinValue && now - lastAggregate > TimeSpan.FromSeconds(2))
            {
                Reset();
                prior = null;
                session = s.SessionId;
                type = s.SessionType;
            }
            if (now - lastAggregate < TimeSpan.FromMilliseconds(500)) return false;
            lastAggregate = now;
            if (prior != null && prior.Session == session && prior.Type == type &&
                s.SessionPhase == SessionPhase.Green && s.SessionRunningTime == prior.SimTime) return false;
            if (pending?.DeliveryStartedUtc > DateTime.MinValue)
            {
                lastSpoken[pending.Topic] = pending.DeliveryStartedUtc;
                pending = null;
            }
            if (pending != null && !pending.IsValid(gs, now)) pending = null;

            var own = RecentOwn(context.Memory?.GetLaps(5));
            var driverLoad = workload?.Describe(now);
            var data = new JObject {
                ["schema"] = 1, ["revision"] = ++revision, ["gemessen_utc"] = source.ToString("o"),
                ["session_id"] = session, ["session_typ"] = type.ToString(), ["runde"] = s.CompletedLaps,
                ["sektor"] = s.SectorNumber, ["phase"] = s.SessionPhase.ToString(),
                ["fahrerbelastung"] = driverLoad,
                ["eigene_letzte_saubere_runden_s"] = new JArray(own.Select(l => Math.Round(l.LapTime, 3))),
                ["interpretation"] = "Messwerte und Vergleiche. Eine Ursache oder Reifen-Gripgrenze folgt daraus nicht automatisch."
            };
            double? mean = own.Length >= 3 ? (double?)own.Skip(Math.Max(0, own.Length - 3)).Average(l => l.LapTime) : null;
            data["eigene_3_runden_mittel_s"] = Number(mean);
            data["pace_regression"] = PaceRegression(own);
            data["reifenverschleiss_regression"] = WearRegression(own);
            var balance = FuelEstimate.From(context);
            data["spritprojektion"] = balance == null ? null : new JObject {
                ["verbrauch_l_runde"] = Math.Round(balance.PerLap, 3),
                ["restrunden"] = Math.Round(balance.LapsRemaining, 2),
                ["restrunden_geschaetzt"] = balance.EstimatedFromTime,
                ["fehlmenge_ohne_reserve_l"] = Math.Round(balance.AdditionalWithoutReserve, 2),
                ["reserve_l"] = Math.Round(balance.ReserveLitres, 2), ["quelle"] = balance.Source
            };
            ObserveTyres(gs);
            var frontRival = Rival(context, true, own, mean);
            var rearRival = Rival(context, false, own, mean);
            data["klassengegner_vorne"] = frontRival;
            data["klassengegner_hinten"] = rearRival;
            data["wetterverlauf"] = Weather(gs, now);
            data["spielvorhersage"] = WeatherWatch.ForecastData(gs.Conditions, now);
            var next = new Snapshot { Session = session, Type = type, Lap = s.CompletedLaps, Track = track,
                SourceUtc = source, Data = data, Revision = revision, SimTime = s.SessionRunningTime };
            next.Delta = new JObject();
            foreach (var field in data.Properties())
                if (prior == null || prior.Session != next.Session || !JToken.DeepEquals(prior.Data[field.Name], field.Value))
                    next.Delta[field.Name] = field.Value.DeepClone();
            Volatile.Write(ref snapshot, next);
            if (LiveDiagnostics.Enabled)
                LiveDiagnostics.Emit("race.reader", LiveDiagnostics.Data("reader_revision", revision,
                    "source_utc", source.ToString("o"), "workload_mode", workload?.Mode.ToString(),
                    "workload_index", (int?)driverLoad?["belastungsindex"],
                    "input_hz", (double?)driverLoad?["gemessene_abtastrate_hz"],
                    "pace_avg_s", mean,
                    "front_pace_delta_s", (double?)frontRival?["eigener_tempovorteil_s_pro_runde"],
                    "rear_pace_delta_s", (double?)rearRival?["eigener_tempovorteil_s_pro_runde"]));
            return true;
        }

        internal static LapRecord[] RecentOwn(IReadOnlyList<LapRecord> laps)
        {
            if (laps == null) return new LapRecord[0];
            var window = new List<LapRecord>();
            for (int i = laps.Count - 1; i >= 0 && window.Count < 5; i--)
            {
                var lap = laps[i];
                if (!lap.Valid || lap.InPitLap || !Good(lap.LapTime, 10, 3600) ||
                    window.Count > 0 && window.Last().LapNumber != lap.LapNumber + 1) break;
                window.Add(lap);
            }
            window.Reverse();
            return window.ToArray();
        }

        internal static JObject PaceRegression(IReadOnlyList<LapRecord> laps)
        {
            if (laps.Count < 4) return null;
            double[] x = laps.Select(l => (double)l.LapNumber).ToArray();
            double[] y = laps.Select(l => (double)l.LapTime).ToArray();
            var fit = Fit.Line(x, y);
            double residual = Math.Sqrt(x.Select((v, i) => Math.Pow(y[i] - fit.Item1 - fit.Item2 * v, 2)).Average());
            return new JObject { ["runden"] = laps.Count, ["sekunden_pro_runde"] = Math.Round(fit.Item2, 3),
                ["reststreuung_s"] = Math.Round(residual, 3), ["ursache_bestaetigt"] = false };
        }

        internal static JObject WearRegression(IReadOnlyList<LapRecord> laps)
        {
            if (laps.Count < 4 || laps.Any(l => l.TyreWear == null || l.TyreWear.Length != 4 ||
                l.TyreWear.Any(w => !Good(w, 0, 100)))) return null;
            var slopes = new JArray();
            for (int wheel = 0; wheel < 4; wheel++)
            {
                double[] x = laps.Select(l => (double)l.LapNumber).ToArray();
                double[] y = laps.Select(l => (double)l.TyreWear[wheel]).ToArray();
                if (y.Zip(y.Skip(1), (a, b) => b < a - .01).Any(reset => reset)) return null;
                var fit = Fit.Line(x, y);
                if (fit.Item2 < 0) return null;
                slopes.Add(Math.Round(fit.Item2, 3));
            }
            return new JObject { ["prozentpunkte_pro_runde_vl_vr_hl_hr"] = slopes,
                ["runden"] = laps.Count, ["gripklippe_berechenbar"] = false };
        }

        private void ObserveTyres(GameStateData gs)
        {
            foreach (var pair in gs.OpponentData.Take(TrackAwareness.MaxCars))
            {
                var op = pair.Value;
                if (op == null || !op.IsActive) continue;
                string identity = Identity(pair.Key, op);
                if (!tyres.TryGetValue(pair.Key, out var item) || item.Identity != identity)
                {
                    tyres[pair.Key] = new TyreObservation { Identity = identity, Compound = op.CurrentTyres };
                    continue;
                }
                if (KnownTyre(item.Compound) && KnownTyre(op.CurrentTyres) && item.Compound != op.CurrentTyres)
                    item.ChangedGameTime = gs.SessionData.SessionRunningTime;
                item.Compound = op.CurrentTyres;
            }
            foreach (string key in tyres.Keys.Where(k => !gs.OpponentData.ContainsKey(k) || !gs.OpponentData[k].IsActive).ToArray())
                tyres.Remove(key);
        }

        private JObject Rival(RaceDataContext context, bool ahead, LapRecord[] own, double? ownMean)
        {
            var gs = context.GameState;
            var op = RaceDataTools.Neighbour(gs, ahead);
            if (op == null || !op.IsActive) return null;
            string key = gs.OpponentData.FirstOrDefault(p => ReferenceEquals(p.Value, op)).Key;
            if (key == null || op.CarClass == null || gs.carClass == null ||
                !CarData.IsCarClassEqual(gs.carClass, op.CarClass, false)) return null;
            var laps = op.OpponentLapData.Where(l => l.IsValid && !l.InLap && !l.OutLap && !l.hasMissingSectors &&
                Good(l.LapTime, 10, 3600)).OrderByDescending(l => l.LapNumber).Take(3).Reverse().ToArray();
            bool comparable = ownMean.HasValue && laps.Length == 3 &&
                laps[1].LapNumber == laps[0].LapNumber + 1 && laps[2].LapNumber == laps[1].LapNumber + 1 &&
                Good(laps[2].GameTimeAtLapStart, .001, gs.SessionData.SessionRunningTime) &&
                gs.SessionData.SessionRunningTime - laps[2].GameTimeAtLapStart <= ownMean.Value * 2 &&
                own.Skip(Math.Max(0, own.Length - 3)).All(l => (ahead ? l.FrontIdentity : l.BehindIdentity) == Identity(key, op));
            if (comparable && tyres.TryGetValue(key, out var observedTyre) && observedTyre.ChangedGameTime >= 0)
                comparable = laps.All(l => l.GameTimeAtLapStart >= observedTyre.ChangedGameTime);
            double? paceAdvantage = comparable ? (double?)(laps.Average(l => l.LapTime) - own.Skip(Math.Max(0, own.Length - 3)).Average(l => l.LapTime)) : null;
            var ownRecent = own.Skip(Math.Max(0, own.Length - 3)).ToArray();
            var sectors = new JArray();
            for (int sector = 0; sector < 3; sector++)
            {
                bool available = comparable && ownRecent.All(l => l.SectorTimes != null && Good(l.SectorTimes[sector], .1, 3600)) &&
                    laps.All(l => Good(l.SectorTimes[sector], .1, 3600));
                sectors.Add(available ? (JToken)Math.Round(laps.Average(l => l.SectorTimes[sector]) -
                    ownRecent.Average(l => l.SectorTimes[sector]), 3) : null);
            }
            var gap = RaceDataTools.ReadGap(context, op, ahead);
            var result = new JObject { ["gegner_id"] = key, ["name"] = RadioPhraseCatalog.SafeRivalName(op.DriverRawName),
                ["klassenplatz"] = op.ClassPosition, ["gesamtplatz"] = op.OverallPosition,
                ["vergleichbare_runden"] = comparable ? 3 : 0, ["eigener_tempovorteil_s_pro_runde"] = Number(paceAdvantage),
                ["eigener_sektorvorteil_s"] = sectors, ["abstand"] = gap.Describe(context),
                ["gemeldete_mischung"] = KnownTyre(op.CurrentTyres) ? op.CurrentTyres.ToString() : null };
            result["abstandsverlauf"] = GapHistory(ownRecent, key, op, ahead);
            if (tyres.TryGetValue(key, out var tyre) && tyre.ChangedGameTime >= 0)
                result["beobachteter_mischungswechsel_vor_spielsekunden"] = Math.Round(gs.SessionData.SessionRunningTime - tyre.ChangedGameTime, 1);
            MaybeTyreCall(context, op, key, ownRecent, laps);
            MaybePaceCall(context, op, key, ahead, paceAdvantage, laps.LastOrDefault()?.LapNumber ?? -1);
            return result;
        }

        private static JObject GapHistory(LapRecord[] laps, string key, OpponentData rival, bool ahead)
        {
            if (laps.Length != 3 || laps.Any(l => (ahead ? l.FrontIdentity : l.BehindIdentity) != Identity(key, rival))) return null;
            var kinds = laps.Select(l => ahead ? l.GapAheadKind : l.GapBehindKind).ToArray();
            double[] values = laps.Select(l => (double)(ahead ? l.GapAhead : l.GapBehind)).ToArray();
            if (kinds.Any(k => k != RaceGapKind.SimulatorTiming && k != RaceGapKind.InterpolatedTiming) ||
                kinds.Any(k => k != kinds[0]) || values.Any(v => !Good(v, .05, 300))) return null;
            double first = values[1] - values[0], second = values[2] - values[1];
            string trend = first < -.1 && second < -.1 ? "schrumpft" : first > .1 && second > .1 ? "waechst" :
                Math.Abs(first) <= .1 && Math.Abs(second) <= .1 ? "stabil" : "wechselnd";
            return new JObject { ["runden"] = 3, ["typ"] = trend,
                ["werte_s"] = new JArray(values.Select(v => Math.Round(v, 2))),
                ["messmethode"] = kinds[0].ToString(), ["sektorursache_bestaetigt"] = false };
        }

        private void MaybePaceCall(RaceDataContext context, OpponentData op, string key, bool ahead, double? advantage, int opponentLap)
        {
            var gs = context.GameState;
            string topic = ahead ? "reader-pace-front" : "reader-pace-rear";
            if (!advantage.HasValue || Math.Abs(advantage.Value) < .35 || Math.Abs(advantage.Value) > 10 || pending != null ||
                gs.SessionData.SessionType != SessionType.Race || gs.SessionData.SessionPhase != SessionPhase.Green || gs.PitData.InPitlane ||
                lastSpoken.TryGetValue(topic, out var last) && context.NowUtc - last < TimeSpan.FromSeconds(90)) return;
            string identity = Identity(key, op);
            int lap = gs.SessionData.CompletedLaps;
            string amount = Math.Abs(advantage.Value).ToString("0.0", CultureInfo.GetCultureInfo("de-DE"));
            string group = (ahead ? "pace-front-" : "pace-rear-") + (advantage.Value > 0 ? "faster" : "slower");
            string name = context.SendDriverNames ? RadioPhraseCatalog.SafeRivalName(op.DriverRawName) : "";
            if (!variants.TryGetValue(topic, out var cursor)) variants[topic] = cursor = new RadioVariantCursor();
            string text = name.Length > 0 ? RadioPhraseCatalog.Current.Format(group, cursor.Heard, amount, name) :
                advantage.Value > 0 ? "Unser Rundenschnitt ist " + amount + " Sekunden schneller als beim Klassenrivalen " + (ahead ? "vor" : "hinter") + " dir." :
                "Der Klassenrivale " + (ahead ? "vor" : "hinter") + " dir fährt im Schnitt " + amount + " Sekunden schneller.";
            pending = new ObservedRaceCall { Topic = topic, Text = text, VariantCursor = cursor,
                FieldNamesAllowed = name.Length > 0, ReferenceTemplate = "RRCA/" + group,
                Member = TeamMember.Mik, Priority = 3, Position = -1, SessionId = session, SessionType = type,
                CompletedLaps = lap, DetectedUtc = context.NowUtc, ExpiresUtc = context.NowUtc.AddSeconds(10),
                Evidence = "three_valid_consecutive_laps_same_identified_class_rival;mean_difference=" + advantage.Value.ToString("F3", CultureInfo.InvariantCulture),
                AdditionalValidity = live => live.OpponentData.TryGetValue(key, out var current) && Identity(key, current) == identity &&
                    !current.InPits && current.IsActive && (RaceDataTools.Neighbour(live, ahead) == current) &&
                    current.OpponentLapData.Where(l => Good(l.LapTime, 10, 3600)).Select(l => l.LapNumber).DefaultIfEmpty(-1).Max() == opponentLap };
            LiveDiagnostics.Decision(topic, "detected", pending.Evidence);
        }

        private void MaybeTyreCall(RaceDataContext context, OpponentData rival, string key, LapRecord[] own, LapData[] opponent)
        {
            var gs = context.GameState;
            string topic = "reader-wet-compound";
            if (pending != null || !tyres.TryGetValue(key, out var observed) || observed.ChangedGameTime < 0 ||
                rival.CurrentTyres != TyreType.Intermediate && rival.CurrentTyres != TyreType.Wet ||
                !KnownTyre(gs.TyreData.FrontLeftTyreType) || !gs.TyreData.HasMatchedTyreTypes ||
                gs.TyreData.FrontLeftTyreType == TyreType.Wet || gs.TyreData.FrontLeftTyreType == TyreType.Intermediate ||
                !WetNow(gs, context.NowUtc) ||
                gs.SessionData.SessionType != SessionType.Race || gs.SessionData.SessionPhase != SessionPhase.Green ||
                own.Length < 3 || own.Any(l => l.SectorTimes == null || !Good(l.SectorTimes[0], .1, 3600)) ||
                opponent.Length < 2 || lastSpoken.TryGetValue(topic, out var last) && context.NowUtc - last < TimeSpan.FromSeconds(90)) return;
            var afterChange = opponent.Skip(Math.Max(0, opponent.Length - 2)).ToArray();
            double ownSector = own.Average(l => l.SectorTimes[0]);
            if (afterChange.Any(l => l.GameTimeAtLapStart < observed.ChangedGameTime ||
                !Good(l.SectorTimes[0], .1, 3600) || l.SectorTimes[0] >= ownSector - .3)) return;
            string name = context.SendDriverNames ? RadioPhraseCatalog.SafeRivalName(rival.DriverRawName) : "";
            string compound = rival.CurrentTyres == TyreType.Intermediate ? "Intermediates" : "Regenreifen";
            TyreType measuredCompound = rival.CurrentTyres;
            TyreType ownCompound = gs.TyreData.FrontLeftTyreType;
            string identity = Identity(key, rival);
            pending = new ObservedRaceCall { Topic = topic,
                Text = (name.Length > 0 ? name : "Der Klassenrivale") + " auf " + compound + ". Seine ersten Sektoren sind schneller als unsere.",
                Member = TeamMember.Ben, Priority = 4, Position = -1, SessionId = session, SessionType = type,
                CompletedLaps = gs.SessionData.CompletedLaps, FieldNamesAllowed = name.Length > 0,
                DetectedUtc = context.NowUtc, ExpiresUtc = context.NowUtc.AddSeconds(10),
                Evidence = "observed_compound_change;two_valid_postchange_sector1_comparisons;wet_weather",
                AdditionalValidity = live => live.OpponentData.TryGetValue(key, out var op) && op.IsActive && !op.InPits &&
                    Identity(key, op) == identity && op.CurrentTyres == measuredCompound &&
                    live.TyreData.HasMatchedTyreTypes && live.TyreData.FrontLeftTyreType == ownCompound,
                TimedValidity = (live, utc) => WetNow(live, utc) };
            LiveDiagnostics.Decision(topic, "detected", pending.Evidence);
        }

        private static bool WetNow(GameStateData state, DateTime now)
        {
            var weather = state.Conditions.LatestConditions;
            return weather != null && WeatherWatch.Fresh(now, weather.Time) &&
                (weather.TrackWetness >= CrewChiefV4.Events.ConditionsMonitor.TrackWetness.LightlyWet || Good(weather.RainDensity, .01, 1));
        }

        private JObject Weather(GameStateData gs, DateTime now)
        {
            var sample = gs.Conditions.LatestConditions;
            if (sample == null || !WeatherWatch.Fresh(now, sample.Time)) return null;
            if (weatherBaseline == null || sample.Time < lastWeatherSample || sample.Time - weatherBaseline.Time > TimeSpan.FromMinutes(3))
                weatherBaseline = sample;
            lastWeatherSample = sample.Time;
            double elapsed = (sample.Time - weatherBaseline.Time).TotalSeconds;
            return new JObject { ["zeitfenster_s"] = Math.Round(elapsed, 1),
                ["luft_delta_c"] = elapsed >= 30 && Good(sample.AmbientTemperature, -40, 70) && Good(weatherBaseline.AmbientTemperature, -40, 70) ?
                    (JToken)Math.Round(sample.AmbientTemperature - weatherBaseline.AmbientTemperature, 2) : null,
                ["strecke_delta_c"] = elapsed >= 30 && Good(sample.TrackTemperature, -40, 90) && Good(weatherBaseline.TrackTemperature, -40, 90) ?
                    (JToken)Math.Round(sample.TrackTemperature - weatherBaseline.TrackTemperature, 2) : null,
                ["ursache_fuer_rundenzeit_bestaetigt"] = false };
        }

        public ObservedRaceCall Take(GameStateData state, DateTime now, bool busy, Func<TeamMember, bool> enabled = null)
        {
            if (pending == null || busy || !pending.IsValid(state, now) || enabled != null && !enabled(pending.Member)) return null;
            return pending.TryReserve(now) ? pending : null;
        }

        public JObject Read(RaceDataContext context, int? afterRevision = null)
        {
            var current = Volatile.Read(ref snapshot);
            if (current == null || context?.GameState == null || context.TelemetryAgeSeconds < 0 || context.TelemetryAgeSeconds > 1 || context.GameState.SessionData.SessionId != current.Session ||
                context.GameState.SessionData.SessionType != current.Type || (context.GameState.SessionData.TrackDefinition?.name ?? context.GameState.trackName) != current.Track || context.NowUtc < current.SourceUtc ||
                context.NowUtc - current.SourceUtc > TimeSpan.FromSeconds(1))
                return new JObject { ["verfuegbar"] = false, ["grund"] = "Kein frisches verdichtetes Lagebild." };
            bool delta = afterRevision.HasValue && afterRevision.Value >= current.Revision - 1 && afterRevision.Value <= current.Revision;
            var copy = (JObject)(afterRevision == current.Revision ? new JObject() : delta ? current.Delta : current.Data).DeepClone();
            copy["revision"] = current.Revision;
            copy["vollstaendiger_snapshot"] = !delta;
            if (!context.SendDriverNames)
                foreach (var key in new[] { "klassengegner_vorne", "klassengegner_hinten" })
                    if (copy[key] is JObject rival) { rival.Remove("name"); rival.Remove("gegner_id"); }
            copy["verfuegbar"] = true;
            return copy;
        }

        internal static string Identity(string key, OpponentData op) => key + "|" + op.DriverRawName + "|" + op.CarNumber + "|" + op.CostId;
        private static bool KnownTyre(TyreType tyre) => tyre != TyreType.Unknown_Race && tyre != TyreType.Uninitialized;
        private static bool Good(double value, double low, double high) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= low && value <= high;
        private static JToken Number(double? value) => value.HasValue ? (JToken)Math.Round(value.Value, 3) : null;
    }

}
