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
            public double? InputRateHz;
        }
        private readonly Dictionary<string, TyreObservation> tyres = new Dictionary<string, TyreObservation>();
        private readonly Dictionary<string, DateTime> lastSpoken = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, RadioVariantCursor> variants = new Dictionary<string, RadioVariantCursor>();
        private Snapshot snapshot;
        private ObservedRaceCall pending;
        private string lastSectorLoss;
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
            lastSectorLoss = null;
            snapshot = null;
            lastAggregate = lastWeatherSample = DateTime.MinValue;
            weatherBaseline = null;
            session = int.MinValue;
            revision = 0;
        }

        public bool IsDue(DateTime now) => now < lastAggregate || now - lastAggregate >= TimeSpan.FromMilliseconds(500);

        public bool Observe(RaceDataContext context, DriverWorkload workload, ThermalWatch thermalWatch = null)
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
                ["stintverlauf"] = RaceFactStore.DescribeLiveLaps(own),
                ["interpretation"] = "Messwerte und Vergleiche. Eine Ursache oder Reifen-Gripgrenze folgt daraus nicht automatisch."
            };
            double? mean = own.Length >= 3 ? (double?)own.Skip(Math.Max(0, own.Length - 3)).Average(l => l.LapTime) : null;
            data["eigene_3_runden_mittel_s"] = Number(mean);
            data["pace_regression"] = PaceRegression(own);
            data["klassenfeld_pace"] = RaceFieldTools.TopClassPace(gs, own);
            data["reifenverschleiss_regression"] = WearRegression(own);
            var balance = FuelEstimate.From(context);
            data["spritprojektion"] = balance == null ? null : new JObject {
                ["verbrauch_l_runde"] = Math.Round(balance.PerLap, 3),
                ["verbrauch_vorlaeufig"] = balance.ProvisionalConsumption,
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
            var thermal = ThermalContext(context, own, rearRival, thermalWatch);
            data["temperatur_und_rennlage"] = thermal;
            if (thermal != null && pending == null) MaybeThermalCall(context, thermal, thermalWatch);
            data["funkphase"] = RaceRadioDirector.BackgroundPhase(context);
            data["wetterverlauf"] = Weather(gs, now);
            data["spielvorhersage"] = WeatherWatch.ForecastData(gs.Conditions, now);
            var next = new Snapshot { Session = session, Type = type, Lap = s.CompletedLaps, Track = track,
                SourceUtc = source, Data = data, Revision = revision, SimTime = s.SessionRunningTime,
                InputRateHz = (double?)driverLoad?["gemessene_abtastrate_hz"] };
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

        internal static JObject ThermalContext(RaceDataContext context, LapRecord[] own, JObject rear, ThermalWatch watch)
        {
            var state = context.GameState;
            int heat = watch?.SustainedHeat(state, context.NowUtc) ?? 0;
            if (heat == 0 || state.SessionData.SessionType != SessionType.Race) return null;
            bool closing = (string)rear?["abstandsverlauf"]?["typ"] == "schrumpft" &&
                (double?)rear?["abstand"]?["sekunden"] is double gap && gap > .05 && gap <= 2.5;
            var s = state.SessionData;
            var recent = s.PlayerLapData.LastOrDefault();
            int? sector = null;
            double? loss = null;
            if (recent != null && recent.IsValid && !recent.InLap && !recent.OutLap && !s.SectorTimingEstimated)
            {
                for (int split = 2; split >= 0; split--)
                {
                    if (recent.GameTimeAtSectorEnd.Length <= split || recent.SectorTimes.Length <= split) continue;
                    float finished = recent.GameTimeAtSectorEnd[split];
                    float measured = recent.SectorTimes[split];
                    double age = s.SessionRunningTime - finished;
                    if (finished <= 0 || !Good(age, 0, 3) || !Good(measured, .1, 3600)) continue;
                    var baseline = own.Where(l => l.Green && l.LapNumber < recent.LapNumber && l.SectorTimes != null &&
                        l.SectorTimes.Length > split && Good(l.SectorTimes[split], .1, 3600)).Reverse().Take(3).ToArray();
                    if (baseline.Length == 3)
                    {
                        var delta = measured - baseline.Average(l => (double)l.SectorTimes[split]);
                        if (delta > .5) { sector = split+1; loss = delta; }
                    }
                    break;
                }
            }
            if (!closing && !loss.HasValue) return null;
            return new JObject { ["reifen_an_warnschwelle"] = (heat & 1) != 0, ["bremsen_an_warnschwelle"] = (heat & 2) != 0,
                ["hintermann_holt_auf"] = closing, ["sektor"] = sector, ["sektorverlust_s"] = Number(loss),
                ["aktueller_abschnitt"] = context.Tracks?.Snapshot?.IsCurrent(context) == true ? context.Tracks.Snapshot.CurrentSection : null,
                ["ursache_bestaetigt"] = false };
        }

        private void MaybeThermalCall(RaceDataContext context, JObject thermal, ThermalWatch watch)
        {
            const string topic = "reader-temperature-context";
            if (lastSpoken.TryGetValue(topic, out var last) && context.NowUtc-last < TimeSpan.FromMinutes(2)) return;
            int heat = watch.SustainedHeat(context.GameState, context.NowUtc);
            if (heat == 0) return;
            string temperature = heat == 3 ? RadioLanguage.T("Reifen und Bremsen sind heiß.", "Tyres and brakes are hot.", "Los neumáticos y frenos van calientes.") :
                heat == 1 ? RadioLanguage.T("Die Reifen sind zu heiß.", "The tyres are too hot.", "Los neumáticos van demasiado calientes.") :
                    RadioLanguage.T("Die Bremsen sind zu heiß.", "The brakes are too hot.", "Los frenos van demasiado calientes.");
            int? sector = (int?)thermal["sektor"];
            double? loss = (double?)thermal["sektorverlust_s"];
            bool closing = (bool)thermal["hintermann_holt_auf"];
            string gapTrend = sector.HasValue && loss.HasValue ?
                RadioLanguage.T("In Sektor " + sector + " hast du " + GermanRadioText.Interval((float)loss.Value) + " verloren.",
                    "You lost " + GermanRadioText.Interval((float)loss.Value) + " in sector " + sector + ".",
                    "Has perdido " + GermanRadioText.Interval((float)loss.Value) + " en el sector " + sector + ".") :
                RadioLanguage.T("Der Hintermann kommt näher.", "The car behind is closing.", "El coche de detrás se acerca.");
            var state = context.GameState.SessionData;
            var catalog = RadioPhraseCatalog.Current;
            if (!variants.TryGetValue(topic, out var cursor)) variants[topic] = cursor = new RadioVariantCursor();
            int variant = cursor.Select(topic, catalog.Count(topic));
            string rearKey = state.OpponentKeyBehind;
            pending = new ObservedRaceCall {Topic=topic, Text=catalog.Format(topic, variant, temperature, gapTrend),
                Member=TeamMember.Mik, Priority=4, Position=-1, SessionId=state.SessionId, SessionType=state.SessionType,
                CompletedLaps=state.CompletedLaps, DetectedUtc=context.NowUtc, ExpiresUtc=context.NowUtc.AddSeconds(8),
                Evidence="sustained_temperature_limit_and_measured_race_context;no_cause_inferred",
                TimedValidity=(live, at) => (watch.SustainedHeat(live, at) & heat) == heat &&
                    (!closing || sector.HasValue || live.SessionData.OpponentKeyBehind == rearKey) };
            pending.BindVariant(cursor, topic, variant);
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
            if (op == null || !op.IsActive || context.Tracks != null && !context.Tracks.OpponentFresh(context, op)) return null;
            string key = gs.OpponentData.FirstOrDefault(p => ReferenceEquals(p.Value, op)).Key;
            if (key == null || op.CarClass == null || gs.carClass == null ||
                !CarData.IsCarClassEqual(gs.carClass, op.CarClass, false)) return null;
            var recentRivalLaps = op.OpponentLapData.Where(l => l.IsValid && !l.InLap && !l.OutLap && !l.hasMissingSectors &&
                Good(l.LapTime, 10, 3600)).OrderByDescending(l => l.LapNumber).Take(5).Reverse().ToArray();
            var laps = recentRivalLaps.Skip(Math.Max(0, recentRivalLaps.Length - 3)).ToArray();
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
            if (ahead) MaybeSectorLossCall(context, op, key, gap);
            var result = new JObject { ["gegner_id"] = key, ["name"] = RadioPhraseCatalog.SafeRivalName(op.DriverRawName),
                ["klassenplatz"] = op.ClassPosition, ["gesamtplatz"] = op.OverallPosition,
                ["vergleichbare_runden"] = comparable ? 3 : 0, ["eigener_tempovorteil_s_pro_runde"] = Number(paceAdvantage),
                ["eigener_sektorvorteil_s"] = sectors, ["abstand"] = gap.Describe(context),
                ["gemeldete_mischung"] = KnownTyre(op.CurrentTyres) ? op.CurrentTyres.ToString() : null };
            result["letzte_renn_runden"] = new JArray(recentRivalLaps.Select(l =>
                new JObject { ["runde"] = l.LapNumber, ["zeit_s"] = Math.Round(l.LapTime, 3) }));
            result["abstandsverlauf"] = GapHistory(own, key, op, ahead);
            if (tyres.TryGetValue(key, out var tyre) && tyre.ChangedGameTime >= 0)
                result["beobachteter_mischungswechsel_vor_spielsekunden"] = Math.Round(gs.SessionData.SessionRunningTime - tyre.ChangedGameTime, 1);
            MaybeTyreCall(context, op, key, ownRecent, laps);
            MaybePaceCall(context, op, key, ahead, paceAdvantage, laps.LastOrDefault()?.LapNumber ?? -1);
            return result;
        }

        internal static JObject GapHistory(LapRecord[] laps, string key, OpponentData rival, bool ahead)
        {
            if (laps.Length < 3) return null;
            string identity = Identity(key, rival);
            RaceGapKind Kind(LapRecord lap) => ahead ? lap.GapAheadKind : lap.GapBehindKind;
            double Gap(LapRecord lap) => ahead ? lap.GapAhead : lap.GapBehind;
            var kind = Kind(laps[laps.Length - 1]);
            if (kind != RaceGapKind.SimulatorTiming && kind != RaceGapKind.InterpolatedTiming) return null;
            var series = laps.Reverse().Take(5).TakeWhile(lap =>
                (ahead ? lap.FrontIdentity : lap.BehindIdentity) == identity && Kind(lap) == kind &&
                Good(Gap(lap), .05, 300)).Reverse().ToArray();
            if (series.Length < 3) return null;
            var values = series.Select(Gap).ToArray();
            var changes = values.Zip(values.Skip(1), (earlier, later) => later - earlier).ToArray();
            string trend = changes.All(c => c < -.1) ? "schrumpft" : changes.All(c => c > .1) ? "waechst" :
                changes.All(c => Math.Abs(c) <= .1) ? "stabil" : "wechselnd";
            return new JObject { ["runden"] = series.Length, ["typ"] = trend,
                ["rundennummern"] = new JArray(series.Select(lap => lap.LapNumber)),
                ["werte_s"] = new JArray(values.Select(v => Math.Round(v, 2))),
                ["messmethode"] = kind.ToString(), ["sektorursache_bestaetigt"] = false };
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
            int index = cursor.Select(group, RadioPhraseCatalog.Current.Count(group));
            string text = name.Length > 0 ? RadioPhraseCatalog.Current.Format(group, index, amount, name) :
                advantage.Value > 0 ? "Unser Rundenschnitt ist " + amount + " Sekunden schneller als beim Klassenrivalen " + (ahead ? "vor" : "hinter") + " dir." :
                "Der Klassenrivale " + (ahead ? "vor" : "hinter") + " dir fährt im Schnitt " + amount + " Sekunden schneller.";
            pending = new ObservedRaceCall { Topic = topic, Text = text, VariantCursor = cursor,
                FieldNamesAllowed = name.Length > 0, ReferenceTemplate = "RRCA/anonymous-" + group,
                Member = TeamMember.Mik, Priority = 3, Position = -1, SessionId = session, SessionType = type,
                CompletedLaps = lap, DetectedUtc = context.NowUtc, ExpiresUtc = context.NowUtc.AddSeconds(10),
                Evidence = "three_valid_consecutive_laps_same_identified_class_rival;mean_difference=" + advantage.Value.ToString("F3", CultureInfo.InvariantCulture),
                AdditionalValidity = live => live.OpponentData.TryGetValue(key, out var current) && Identity(key, current) == identity &&
                    !current.InPits && current.IsActive && (RaceDataTools.Neighbour(live, ahead) == current) &&
                    current.OpponentLapData.Where(l => Good(l.LapTime, 10, 3600)).Select(l => l.LapNumber).DefaultIfEmpty(-1).Max() == opponentLap };
            if (name.Length > 0) pending.BindVariant(cursor, group, index);
            if (context.Tracks != null)
                pending.TimedValidity = (live, at) => context.Tracks.OpponentFresh(new RaceDataContext { GameState=live, NowUtc=at,
                    LastTelemetryUtc=live.SourceTelemetryUtc == DateTime.MinValue ? live.Now : live.SourceTelemetryUtc },
                    RaceDataTools.Neighbour(live, ahead));
            LiveDiagnostics.Decision(topic, "detected", pending.Evidence);
        }

        internal static bool TryRecentSectorLoss(OpponentData rival, float gameTime, out LapData lap, out int sector, out double loss)
        {
            lap = null; sector = -1; loss = 0;
            var laps = rival.OpponentLapData;
            if (laps.Count < 4 || !Good(gameTime, .01, 200000)) return false;
            for (int i = laps.Count - 1; i >= Math.Max(3, laps.Count - 2); i--)
            {
                var current = laps[i];
                if (current == null || !current.IsValid || current.InLap || current.OutLap || current.SectorTimes.Length != 3 ||
                    current.GameTimeAtSectorEnd.Length != 3) continue;
                for (int split = 2; split >= 0; split--)
                {
                    float ended = current.GameTimeAtSectorEnd[split];
                    float measured = current.SectorTimes[split];
                    if (!Good(ended, .01, gameTime) || gameTime - ended > 4 || !Good(measured, .1, 3600)) continue;
                    // Use the opponent's own preceding three clean, consecutive laps in that same sector.
                    double sum = 0;
                    for (int back = 1; back <= 3; back++)
                    {
                        var prior = laps[i - back];
                        if (prior == null || prior.LapNumber != current.LapNumber - back || !prior.IsValid || prior.InLap ||
                            prior.OutLap || prior.hasMissingSectors || !Good(prior.LapTime, 10, 3600) ||
                            prior.SectorTimes.Length != 3 || !Good(prior.SectorTimes[split], .1, 3600)) return false;
                        sum += prior.SectorTimes[split];
                    }
                    loss = measured - sum / 3;
                    if (loss <= .5) return false;
                    lap = current; sector = split + 1; return true;
                }
            }
            return false;
        }

        private static bool SectorTacticalGap(RaceGapMeasurement gap) =>
            (gap.Kind == RaceGapKind.SimulatorTiming || gap.Kind == RaceGapKind.InterpolatedTiming) && Good(gap.Seconds, .1, 4);

        private void MaybeSectorLossCall(RaceDataContext context, OpponentData rival, string key, RaceGapMeasurement gap)
        {
            const string topic = "rival-sector-loss";
            var gs = context.GameState;
            var state = gs.SessionData;
            if (state.SessionType != SessionType.Race || state.SessionPhase != SessionPhase.Green || !state.CurrentLapIsValid ||
                state.SectorTimingEstimated || gs.FlagData.isLocalYellow || gs.PitData.InPitlane || rival.InPits ||
                rival.isEnteringPits() || !SectorTacticalGap(gap) ||
                pending != null && (pending.IsReserved || pending.Priority >= 4) ||
                lastSpoken.TryGetValue(topic, out var last) && context.NowUtc - last < TimeSpan.FromSeconds(30) ||
                !TryRecentSectorLoss(rival, state.SessionRunningTime, out var lap, out int sector, out double loss)) return;
            string identity = Identity(key, rival);
            string sample = identity + "|" + lap.LapNumber + "|" + sector;
            if (lastSectorLoss == sample) return;
            lastSectorLoss = sample;
            string name = context.SendDriverNames && rival.CanUseName ? RadioPhraseCatalog.SafeRivalName(rival.DriverRawName) : "";
            string driver = name.Length > 0 ? name : RadioLanguage.T("Der Vordermann", "The car ahead", "El coche de delante");
            var catalog = RadioPhraseCatalog.Current;
            if (!variants.TryGetValue(topic, out var cursor)) variants[topic] = cursor = new RadioVariantCursor();
            int variant = cursor.Select(topic, catalog.Count(topic));
            pending = new ObservedRaceCall {
                Topic=topic, Text=catalog.Format(topic, variant, driver, sector.ToString(CultureInfo.InvariantCulture), GermanRadioText.Interval((float)loss)),
                Member=TeamMember.Ben, Priority=4, Position=-1, SessionId=state.SessionId, SessionType=state.SessionType,
                CompletedLaps=state.CompletedLaps, DetectedUtc=context.NowUtc, ExpiresUtc=context.NowUtc.AddSeconds(7),
                FieldNamesAllowed=name.Length > 0, ChaseOpponentIdentity=identity,
                Evidence="fresh_rival_sector_vs_own_three_clean_sectors;no_cause_inferred",
                TimedValidity=(live, at) => {
                    var next = RaceDataTools.Neighbour(live, true);
                    if (next == null || next.InPits || next.isEnteringPits() || live.FlagData.isLocalYellow ||
                        Identity(key, next) != identity || live.SessionData.OpponentKeyInFront != key) return false;
                    var liveContext = new RaceDataContext { GameState=live, NowUtc=at, Tracks=context.Tracks,
                        LastTelemetryUtc=live.SourceTelemetryUtc == DateTime.MinValue ? live.Now : live.SourceTelemetryUtc };
                    var fresh = RaceDataTools.ReadGap(liveContext, next, true);
                    return SectorTacticalGap(fresh);
                }
            };
            pending.BindVariant(cursor, topic, variant);
            LiveDiagnostics.Decision(topic, "detected", pending.Evidence, pending.Text, pending.EventId);
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

        internal double? InputRate(GameStateData state, DateTime now)
        {
            var sample = Volatile.Read(ref snapshot);
            return sample != null && state != null && state.SessionData.SessionId == sample.Session &&
                state.SessionData.SessionType == sample.Type && now >= sample.SourceUtc &&
                now - sample.SourceUtc <= TimeSpan.FromSeconds(1) ? sample.InputRateHz : null;
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
            if (!delta)
            {
                var historical = DriverMemory.PreviousStint(context);
                if (historical != null) copy["vorherige_sitzung_referenz"] = historical;
            }
            copy["verfuegbar"] = true;
            return copy;
        }

        internal static string Identity(string key, OpponentData op) => key + "|" + op.DriverRawName + "|" + op.CarNumber + "|" + op.CostId + "|" + op.r3eUserId + "|" + op.r3eSlotId;
        private static bool KnownTyre(TyreType tyre) => tyre != TyreType.Unknown_Race && tyre != TyreType.Uninitialized;
        private static bool Good(double value, double low, double high) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= low && value <= high;
        private static JToken Number(double? value) => value.HasValue ? (JToken)Math.Round(value.Value, 3) : null;
    }

}
