using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Rob 23.11.: „Muss sicher funktionieren“. Ganze Rennen über 15, 30, 60 und 90 Minuten mit 14 Autos, Verkehr, ruhigen
    /// Phasen und zwei Zweikämpfen, alle zehn Berichte eingeschaltet, Funkmenge Normal. Geprüft wird über Funkplan und Rennkopf:
    /// Jedes Thema kommt nach dem Start, nie mit größerer Lücke als 1,5 × Grundabstand (Technik im Zweikampf höchstens zwei
    /// Minuten länger), Vorder- und Hintermann am häufigsten, und im Sprint kommt die Technik nur ein- bis zweimal.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class RoutineRaceSimulationTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 23, 19, 0, 0, DateTimeKind.Utc);
        private const double Lap = 139.0, SettleSeconds = 60;

        /// <summary>
        /// Was die fünf Sims, die Rob fährt, für den Funkplan unterschiedlich liefern (Quelle: Crew-Chief-Mapper und
        /// Fähigkeitsmatrix): AMS2, RaceRoom und LMU geben den Abstand zum Vordermann im Einklassenrennen selbst, AC EVO und
        /// iRacing nur abgeleitet über Streckenposition; iRacing liefert keine Schadenswerte. AMS2 mit serverseitig
        /// abgeschaltetem Verbrauch und Verschleiß ist ein eigener Fall (Spa 23.11.).
        /// </summary>
        public sealed class SimProfile
        {
            public string Name; public bool DerivedGaps, Damage = true, FrozenFuelAndWear;
            public override string ToString() => Name;
        }
        internal static readonly SimProfile AMS2 = new SimProfile { Name = "AMS2" }, R3E = new SimProfile { Name = "RaceRoom" },
            LMU = new SimProfile { Name = "LMU" }, ACE = new SimProfile { Name = "AC EVO", DerivedGaps = true },
            IRacing = new SimProfile { Name = "iRacing", DerivedGaps = true, Damage = false },
            Ams2ServerOff = new SimProfile { Name = "AMS2 Verbrauch/Verschleiß aus", FrozenFuelAndWear = true };
        private static IEnumerable<TestCaseData> Sims()
        {
            foreach (var sim in new[] { AMS2, R3E, LMU, ACE, IRacing })
                foreach (int minutes in new[] { 15, 30, 60, 90 })
                    yield return new TestCaseData(sim, minutes).SetName("Ganzes_Rennen_" + sim.Name.Replace(" ", "_") + "_" + minutes + "_min");
        }

        internal sealed class RaceRun
        {
            public int Minutes;
            public RaceSpan Span;
            public readonly Dictionary<string, List<double>> Spoken = AutoRadioPlan.Topics.ToDictionary(k => k, k => new List<double>());
            public readonly List<string> Texts = new List<string>();
            public string Report() => "Rennen " + Minutes + " min (" + Span + ")\n" + string.Join("\n", AutoRadioPlan.Topics.Select(k => k + ": " + Spoken[k].Count +
                "x, erste " + (Spoken[k].Count > 0 ? Spoken[k][0].ToString("0") : "-") + " s, größte Lücke " +
                (Spoken[k].Count > 1 ? Spoken[k].Zip(Spoken[k].Skip(1), (a, b) => b - a).Max().ToString("0") : "-") + " s, Grundabstand " +
                AutoRadioPlan.NominalSeconds(Span, k, RadioAmount.Normal) + " s"));
        }

        private static bool Battle(double t, double race) => t >= race * .30 && t < race * .45 || t >= race * .70 && t < race * .78;
        private static bool Calm(double t, double race) => t >= race * .15 && t < race * .30 || t >= race * .55 && t < race * .70;

        private static GameStateData Frame(double t, double race, CarData.CarClass cls, SimProfile sim = null)
        {
            sim = sim ?? AMS2;
            int completed = 1 + (int)(t / Lap);
            var gs = new GameStateData(T.AddSeconds(t).Ticks);
            var s = gs.SessionData;
            s.SessionId = 23; s.SessionType = SessionType.Race; s.SessionPhase = SessionPhase.Green;
            s.SessionHasFixedTime = true; s.SessionTotalRunTime = (float)race; s.SessionTimeRemaining = (float)Math.Max(1, race - t);
            s.CompletedLaps = completed; s.SessionLapsRemaining = -1;
            s.ClassPosition = 3; s.OverallPosition = 3; s.NumCarsOverall = 14; s.NumCarsInPlayerClass = 14;
            s.LapTimePrevious = (float)(Lap + (completed % 3) * .2); s.PlayerLapTimeSessionBest = (float)Lap; s.PreviousLapWasValid = true; s.CurrentLapIsValid = true;
            s.TrackDefinition = new TrackDefinition("Spa", 7004f);
            s.IsNewLap = t > 1 && (t % Lap) < 1;
            gs.SourceTelemetryUtc = T.AddSeconds(t);
            gs.carClass = cls;
            gs.PositionAndMotionData.CarSpeed = 60; gs.PositionAndMotionData.DistanceRoundTrack = (float)((t % Lap) / Lap * 7004);
            gs.FuelData.FuelUseActive = true; gs.FuelData.FuelLeft = sim.FrozenFuelAndWear ? 60f : (float)(100 - t / Lap * 2.5);
            var w = gs.TyreData; w.TyreWearActive = true;
            w.FrontLeftPercentWear = w.FrontRightPercentWear = w.RearLeftPercentWear = w.RearRightPercentWear = sim.FrozenFuelAndWear ? 0f : (float)(t / Lap * 1.5);
            gs.CarDamageData.DamageEnabled = sim.Damage;
            gs.Conditions.UpdateLiveSample(T.AddSeconds(t), completed, 1, 18, 24, 0, 0, 0, 0, 0, false, CrewChiefV4.Events.ConditionsMonitor.TrackStatus.UNKNOWN);
            for (int p = 1; p <= 14; p++)
            {
                if (p == 3) continue;
                string key = "car" + p;
                gs.OpponentData[key] = new OpponentData { DriverRawName = "Fahrer " + p, ClassPosition = p, OverallPosition = p, CarClass = cls,
                    CompletedLaps = completed, DistanceRoundTrack = (float)(((t % Lap) / Lap * 7004 + (3 - p) * 90 + 7004) % 7004), Speed = 60 };
            }
            s.OpponentKeyInFront = "car2"; s.OpponentKeyBehind = "car4"; s.LapsDeltaFront = s.LapsDeltaBehind = 0;
            s.TimeDeltaFrontIsDerived = s.TimeDeltaBehindIsDerived = sim.DerivedGaps;
            s.TimeDeltaFront = Battle(t, race) ? 0.6f : Calm(t, race) ? 4.5f : 1.8f; s.TimeDeltaBehind = Calm(t, race) ? 5.2f : 2.1f;
            return gs;
        }

        internal static RaceRun Run(int minutes, RadioAmount amount = RadioAmount.Normal, SimProfile sim = null)
        {
            sim = sim ?? AMS2;
            var truth = new DataTruth(); DataTruth.Use(truth);
            var tracks = sim.DerivedGaps ? new TrackAwareness() : null;
            double race = minutes * 60.0;
            var prefs = new AiRadioPreferences { Automatic = true };
            var cls = new CarData.CarClass();
            var schedule = new AiRadioSchedule(7) { StartCalm = true, Amount = amount };
            var memory = new AiRaceMemory();
            var run = new RaceRun { Minutes = minutes };
            GameStateData previous = null;
            double busyUntil = -1;
            bool settled = false;
            try
            {
                for (int i = 0; i <= race * 2; i++)
                {
                    double t = i * .5;
                    var now = T.AddSeconds(t);
                    var gs = Frame(t, race, cls, sim);
                    truth.Observe(gs, now);
                    tracks?.Observe(gs, now);
                    memory.Update(previous, gs, now);
                    previous = gs;
                    bool battle = Battle(t, race);
                    RaceMind.Publish(new RaceSituation { Utc = now, SessionId = 23, Phase = t > race * .9 ? RacePhase.Final : RacePhase.Middle,
                        Attention = battle ? RaceAttention.Battle : Calm(t, race) ? RaceAttention.Calm : RaceAttention.Traffic, Focus = RaceFocus.Rhythm,
                        Tension = battle ? .85 : Calm(t, race) ? .15 : .45 });
                    schedule.Observe(gs, now, prefs);
                    run.Span = schedule.Span;
                    if (!settled && t >= SettleSeconds) { settled = true; schedule.StartCalm = false; schedule.StaggerAfterStart(now, prefs); }
                    if (t < busyUntil) continue;
                    var ctx = new RaceDataContext { GameState = gs, NowUtc = now, LastTelemetryUtc = now, Memory = memory, Tracks = tracks };
                    var call = schedule.Take(ctx, prefs, false, null, true);
                    if (call == null) continue;
                    var situation = RaceMind.Latest;
                    if (RaceMind.Judge(situation, call, now, out _) != MindVerdict.Admit) { call.ReleaseForRetry(now, "held"); continue; }
                    call.RefreshBeforeSpeak(ctx);
                    var bundled = schedule.BundledTopics.ToArray();
                    call.MarkPlaybackStarted(now); call.MarkPlaybackFinished(now.AddSeconds(5), false);
                    memory.AddRadio(call.Member, call.Text, now);
                    busyUntil = t + 5;
                    run.Texts.Add(t.ToString("0") + " " + call.Topic + (bundled.Length > 0 ? "+" + string.Join("+", bundled) : "") + ": " + call.Text);
                    run.Spoken[call.Topic].Add(t);
                    foreach (var b in bundled) run.Spoken[b].Add(t);
                }
            }
            finally { RaceMind.Publish(null); DataTruth.Use(new DataTruth()); }
            return run;
        }

        [TestCaseSource(nameof(Sims))]
        public void Every_report_comes_on_its_own_over_a_whole_race(SimProfile sim, int minutes)
        {
            var run = Run(minutes, RadioAmount.Normal, sim);
            string report = sim + ", " + run.Report();
            TestContext.WriteLine(report);
            TestContext.WriteLine(string.Join("\n", run.Texts.Take(40)));
            double race = minutes * 60.0;
            foreach (var topic in AutoRadioPlan.Topics)
            {
                var times = run.Spoken[topic];
                // Ohne Schadenswerte (iRacing) sagt Mik das ehrlich; das ist kein eigener Bericht im Takt.
                if (topic == "vehicle" && !sim.Damage) { Assert.LessOrEqual(times.Count, race / 300 + 2, report); continue; }
                int nominal = AutoRadioPlan.NominalSeconds(run.Span, topic, RadioAmount.Normal);
                bool technical = AiRadioSchedule.IsTechnical(topic) || topic == "pace";
                double allowedGap = 1.5 * nominal + (technical ? AiRadioSchedule.TenseTechnicalGuaranteeSeconds + 30 : 45);
                Assert.IsNotEmpty(times, topic + " kam nie\n" + report);
                // Nach dem Start kommt jedes Thema einmal, gestaffelt im Abstand von 40 s.
                Assert.LessOrEqual(times[0], SettleSeconds + 15 + 10 * 40 + 60, topic + " kam zu spät zum ersten Mal\n" + report);
                for (int k = 1; k < times.Count; k++)
                    Assert.LessOrEqual(times[k] - times[k - 1], allowedGap, topic + " Lücke bei " + times[k - 1] + " s\n" + report);
                if (race - times.Last() > allowedGap + 30)
                    Assert.Fail(topic + " verstummte ab " + times.Last() + " s\n" + report);
                // Kein Dauerfeuer: nie öfter als im halben Grundabstand.
                Assert.LessOrEqual(times.Count, race / (nominal * .5) + 2, topic + " zu oft\n" + report);
            }
            // Rob: Vorder- und Hintermann sind das Spannendste und kommen am häufigsten.
            foreach (var topic in new[] { "fuel", "tyres", "weather", "stint", "vehicle", "pace", "status" })
            {
                Assert.Greater(run.Spoken["ahead"].Count, run.Spoken[topic].Count, "Vordermann öfter als " + topic + "\n" + report);
                Assert.Greater(run.Spoken["behind"].Count, run.Spoken[topic].Count, "Hintermann öfter als " + topic + "\n" + report);
            }
            if (minutes <= 15)
                foreach (var topic in new[] { "weather", "fuel", "tyres", "stint", "vehicle" })
                    Assert.That(run.Spoken[topic].Count, Is.InRange(1, 2), "Sprint: " + topic + " nur ein- bis zweimal\n" + report);
        }

        [Test]
        public void More_and_much_more_info_shorten_every_interval_but_never_below_thirty_seconds()
        {
            foreach (var span in new[] { RaceSpan.Practice, RaceSpan.Sprint, RaceSpan.Short, RaceSpan.Medium, RaceSpan.Long })
                foreach (var topic in AutoRadioPlan.Topics)
                {
                    int normal = AutoRadioPlan.NominalSeconds(span, topic, RadioAmount.Normal);
                    int more = AutoRadioPlan.NominalSeconds(span, topic, RadioAmount.More);
                    int much = AutoRadioPlan.NominalSeconds(span, topic, RadioAmount.MuchMore);
                    Assert.Greater(normal, 0, span + " " + topic);
                    Assert.LessOrEqual(more, normal, span + " " + topic);
                    Assert.LessOrEqual(much, more, span + " " + topic);
                    Assert.GreaterOrEqual(much, AutoRadioPlan.MinimumSeconds, span + " " + topic);
                }
            Assert.AreEqual(RaceSpan.Sprint, AutoRadioPlan.Span(15));
            Assert.AreEqual(RaceSpan.Short, AutoRadioPlan.Span(30));
            Assert.AreEqual(RaceSpan.Medium, AutoRadioPlan.Span(60));
            Assert.AreEqual(RaceSpan.Long, AutoRadioPlan.Span(61));
        }

        [Test]
        public void Calm_brings_technical_reports_sooner_and_tension_holds_them_back()
        {
            int calm = AutoRadioPlan.Seconds(RaceSpan.Medium, "tyres", RadioAmount.Normal, .1, null);
            int medium = AutoRadioPlan.Seconds(RaceSpan.Medium, "tyres", RadioAmount.Normal, .45, null);
            int tense = AutoRadioPlan.Seconds(RaceSpan.Medium, "tyres", RadioAmount.Normal, .9, null);
            Assert.Less(calm, medium); Assert.Less(medium, tense);
            // Vorder- und Hintermann ändert die Spannung hier nicht; im Zweikampf halbiert sie der Funkplan.
            Assert.AreEqual(AutoRadioPlan.Seconds(RaceSpan.Medium, "ahead", RadioAmount.Normal, .1, null),
                AutoRadioPlan.Seconds(RaceSpan.Medium, "ahead", RadioAmount.Normal, .9, null));
        }

        [Test]
        public void Much_more_info_gives_clearly_more_reports_in_the_same_race()
        {
            var normal = Run(30);
            var much = Run(30, RadioAmount.MuchMore);
            int Total(RaceRun r) => r.Spoken.Values.Sum(v => v.Count);
            TestContext.WriteLine(normal.Report() + "\n\n" + much.Report());
            Assert.Greater(Total(much), Total(normal) * 1.3, normal.Report() + "\n\n" + much.Report());
        }
    
        [Test]
        public void Server_side_fuel_and_wear_off_are_said_once_then_the_surroundings_keep_coming()
        {
            var run = Run(30, RadioAmount.Normal, Ams2ServerOff);
            string report = run.Report();
            TestContext.WriteLine(report + "\n" + string.Join("\n", run.Texts.Where(x => x.Contains("fuel") || x.Contains("tyres"))));
            Assert.LessOrEqual(run.Spoken["fuel"].Count, 3, report);
            Assert.LessOrEqual(run.Spoken["tyres"].Count, 2, report);
            Assert.IsFalse(run.Texts.Any(x => x.Contains("Runden") && x.Contains(" fuel")), "keine Reichweite aus stehendem Tank\n" + report);
            Assert.GreaterOrEqual(run.Spoken["ahead"].Count, 12, report);
            Assert.GreaterOrEqual(run.Spoken["behind"].Count, 12, report);
        }
    }
}
