using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Strategy;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Testkanal B, AMS2 Spa 23.11.: Befunde aus dem Testrennen.</summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class AmsSpaFindingsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 23, 18, 40, 0, DateTimeKind.Utc);

        [Test]
        public void Section_reference_is_the_lowest_own_speed_not_the_entry_speed()
        {
            // Bremszone vor La Source: Einfahrt 75 m/s, Scheitel 18 m/s im selben 100-m-Abschnitt.
            float reference = 75;
            foreach (float v in new[] { 70f, 55f, 34f, 18f, 22f }) reference = RaceTrafficWatch.CellReference(reference, v);
            Assert.AreEqual(18f, reference);
            Assert.IsFalse(RaceTrafficWatch.SuddenSlowdown(new List<KeyValuePair<DateTime, float>>
            {
                new KeyValuePair<DateTime, float>(T.AddSeconds(-2.5), 72), new KeyValuePair<DateTime, float>(T.AddSeconds(-1), 40)
            }, 20, T, reference, 70), "normales Anbremsen in der Haarnadel ist kein Dreher");
            Assert.IsTrue(RaceTrafficWatch.SuddenSlowdown(new List<KeyValuePair<DateTime, float>>
            {
                new KeyValuePair<DateTime, float>(T.AddSeconds(-2.5), 72), new KeyValuePair<DateTime, float>(T.AddSeconds(-1), 40)
            }, 5, T, reference, 70), "Einbruch auf 5 m/s bleibt ein Dreher");
        }

        [Test]
        public void Background_call_waits_in_traffic_only_a_few_seconds()
        {
            var call = new ObservedRaceCall { Topic = "reader-pace-rear", Text = "x", Priority = 3, SessionId = 5, SessionType = SessionType.Race,
                DetectedUtc = T, ExpiresUtc = T.AddSeconds(10) };
            RaceSituation At(DateTime now, RaceAttention a) => new RaceSituation { Utc = now, SessionId = 5, Phase = RacePhase.Middle, Attention = a, Focus = RaceFocus.Rhythm };
            Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(At(T.AddSeconds(2), RaceAttention.Traffic), call, T.AddSeconds(2), out _));
            Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(At(T.AddSeconds(7), RaceAttention.Traffic), call, T.AddSeconds(7), out _),
                "Rob: „Ich höre nichts von euch“ – Verkehr hält nicht ewig auf");
            Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(At(T.AddSeconds(7), RaceAttention.Battle), call, T.AddSeconds(7), out _), "Zweikampf wartet weiter");
        }

        [Test]
        public void Configured_reports_are_not_held_by_traffic_and_only_briefly_by_a_battle()
        {
            RaceSituation At(DateTime now, RaceAttention a) => new RaceSituation { Utc = now, SessionId = 5, Phase = RacePhase.Middle, Attention = a, Focus = RaceFocus.Rhythm };
            foreach (var topic in new[] { "status", "ahead", "behind", "fuel", "tyres", "weather" })
            {
                var call = new ObservedRaceCall { Topic = topic, Text = "x", Priority = 1, SessionId = 5, SessionType = SessionType.Race,
                    Evidence = "configured_interval_current_measurements", DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };
                Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(At(T, RaceAttention.Traffic), call, T, out _), topic + ": Verkehr hält nicht auf");
                Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(At(T.AddSeconds(3), RaceAttention.Battle), call, T.AddSeconds(3), out _), topic + ": Zweikampf kurz");
                Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(At(T.AddSeconds(9), RaceAttention.Battle), call, T.AddSeconds(9), out _), topic + ": dann doch");
                Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(At(T.AddSeconds(9), RaceAttention.Caution), call, T.AddSeconds(9), out _), topic + ": Gelb wartet");
                Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(new RaceSituation { Utc = T, SessionId = 5, Phase = RacePhase.Final, Attention = RaceAttention.Open },
                    call, T, out _), topic + ": auch in der Schlussphase");
            }
        }

        [Test]
        public void Questions_about_penalties_validity_closeness_and_corrections_use_the_data()
        {
            foreach (var q in new[] { "Dann notieren, sehr genau rechnen.", "Das ist falsch, Kozmy fährt 2:17 und ich 2:19.",
                "Das müsste automatisch kommen.", "Gute Ansage, öfter bitte." })
                Assert.IsTrue(AiTeamService.NeedsTools(q, null), q);
            // Diese Werte stehen im Lagebild: die schnelle Antwort ohne Werkzeugrunde reicht.
            foreach (var q in new[] { "45 Sekunden Zeitstrafe?", "Ist diese Runde gültig?", "JJ ist plötzlich ganz nah, vorne.", "Wie viel Sprit habe ich noch?",
                "Wie groß ist der Abstand nach vorne?", "Welche Position?", "Völlig okay so?", "Noch mal bitte?" })
                Assert.IsFalse(AiTeamService.NeedsTools(q, null), q);
            StringAssert.Contains("gib nur nach", AiTeamService.LiveAnswerRules);
        }

        [Test]
        public void Briefing_carries_penalty_lap_validity_location_and_stopped_cars()
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green; gs.SessionData.CurrentLapIsValid = true;
            gs.SessionData.TrackDefinition = new TrackDefinition("Spa", 7004f);
            gs.PositionAndMotionData.DistanceRoundTrack = 6700;
            gs.OpponentData["jj"] = new OpponentData { DriverRawName = "JJ", Speed = 2f, DistanceRoundTrack = 6900, ClassPosition = 2 };
            gs.OpponentData["k"] = new OpponentData { DriverRawName = "Kozmy", Speed = 60f, DistanceRoundTrack = 1200, ClassPosition = 1 };
            var o = new Newtonsoft.Json.Linq.JObject();
            RaceDataTools.AddLiveVerdicts(o, new RaceDataContext { GameState = gs, NowUtc = T, LastTelemetryUtc = T });
            Assert.AreEqual(0, (int)o["strafen"]["offene_strafen"]);
            Assert.IsTrue((bool)o["aktuelle_runde_gueltig"]);
            Assert.AreEqual(6700, (int)o["eigene_streckenposition"]["rundenmeter"]);
            var stopped = (Newtonsoft.Json.Linq.JArray)o["feld_stehend_oder_sehr_langsam"];
            Assert.AreEqual(1, stopped.Count);
            Assert.AreEqual(200, (int)stopped[0]["meter_vor_dir"]);
        }

        [Test]
        public void A_single_slow_last_lap_is_not_a_pace_fade()
        {
            double[] spa = { 139.00, 139.45, 139.07, 139.35, 139.95 };
            var input = new StrategyInput { Lap = 6, LapsToGo = 4, FuelLeft = 12.5, FuelPerLap = double.NaN,
                StintLaps = spa.Select((t, k) => new KeyValuePair<int, double>(k + 2, t)).ToList() };
            Assert.AreEqual("stabil", StrategyPicture.Compute(input).PaceTrend);
            double[] fading = { 139.0, 139.2, 139.4, 139.6, 139.8, 140.0 };
            input.StintLaps = fading.Select((t, k) => new KeyValuePair<int, double>(k + 2, t)).ToList();
            Assert.AreEqual("baut_ab", StrategyPicture.Compute(input).PaceTrend, "ein echter Abbau bleibt einer");
        }

        [Test]
        public void Closing_lap_call_waits_for_the_own_line_not_the_leaders()
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 7; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.ClassPosition = 2; s.SessionData.SessionNumberOfLaps = 10; s.SessionData.SessionLapsRemaining = 4; s.SessionData.CompletedLaps = 6;
            s.carClass = new CarData.CarClass(); s.PositionAndMotionData.CarSpeed = 60;
            var observer = new ActiveRaceObserver();
            ObservedRaceCall Run(double from, double to)
            {
                ObservedRaceCall got = null;
                for (double t = from; t <= to; t += 0.5)
                {
                    observer.Observe(s, T.AddSeconds(t), "Rob", 0);
                    var c = observer.Take(s, T.AddSeconds(t), false, false, false, 0, null, "Rob");
                    if (c != null && c.Topic == "race-end") got = c;
                }
                return got;
            }
            Assert.IsNull(Run(0, 2));
            // Der Führende quert die Linie: Restrunden 3, Rob ist noch 14 s davor.
            s.SessionData.SessionLapsRemaining = 3;
            Assert.IsNull(Run(2.5, 16), "nicht schon beim Führenden");
            s.SessionData.CompletedLaps = 7;
            Assert.IsNotNull(Run(16.5, 17), "an Robs eigener Linie");
            // Führt Rob selbst und stellt die Sim die Restrunden einen Tick später um, kommt die Ansage trotzdem.
            s.SessionData.CompletedLaps = 8;
            Assert.IsNull(Run(17.5, 18));
            s.SessionData.SessionLapsRemaining = 2;
            var next = Run(18.5, 19);
            Assert.IsNotNull(next); Assert.AreEqual("race-end", next.Topic);
        }

        [Test]
        public void Switched_off_fuel_and_wear_are_said_once_then_silence()
        {
            var truth = new DataTruth();
            bool wasDeterministic = RadioVariants.Deterministic;
            try
            {
                DataTruth.Use(truth);
                var gs = new GameStateData(T.Ticks);
                gs.SessionData.SessionId = 9; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
                gs.PositionAndMotionData.CarSpeed = 60;
                gs.FuelData.FuelUseActive = true; gs.FuelData.FuelLeft = 12.5f;
                var t = gs.TyreData; t.TyreWearActive = true;
                t.FrontLeftPercentWear = t.FrontRightPercentWear = t.RearLeftPercentWear = t.RearRightPercentWear = 0.00041f;
                for (int i = 0; i <= 200; i++) truth.Observe(gs, T.AddSeconds(i));
                Assert.AreEqual(ChannelState.Stale, truth.State(SimChannel.Fuel));
                Assert.IsTrue(truth.ClaimFrozenNotice(SimChannel.Fuel), "einmal sagen");
                Assert.IsFalse(truth.ClaimFrozenNotice(SimChannel.Fuel), "danach still");
                Assert.AreEqual(-1f, FuelEstimate.Consumption(new AiRaceMemory(), gs, out _, out _), "kein Verbrauch aus Training oder Quali");
                StringAssert.Contains("12,5", AiRadioSchedule.FrozenNotice(SimChannel.Fuel, "12,5"));
                Assert.IsTrue(truth.ClaimFrozenNotice(SimChannel.TyreWear));
                // Neue Session: die Notiz darf wieder einmal kommen.
                gs.SessionData.SessionId = 10; truth.Observe(gs, T.AddSeconds(201));
                Assert.AreNotEqual(ChannelState.Stale, truth.State(SimChannel.Fuel));
            }
            finally { DataTruth.Use(new DataTruth()); RadioVariants.Deterministic = wasDeterministic; }
        }

        [Test]
        public void Configured_reports_are_not_dropped_as_the_same_sentence()
        {
            var memory = new CrewMemory();
            ObservedRaceCall Report(string topic, string text, int stated = 0) => new ObservedRaceCall { Topic = topic, Text = text, Priority = 1,
                Member = TeamMember.Ben, SessionId = 5, StatedPosition = stated, Evidence = "configured_interval_current_measurements",
                DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };
            var status = Report("status", "Platz 3. Seit dem Start 1 Platz gutgemacht.", 3);
            memory.Remember(status, T);
            Assert.IsFalse(memory.SaidRecently(Report("status", "Platz 3. Seit dem Start 1 Platz gutgemacht.", 3), T.AddSeconds(120)), "Rennstatus alle 120 s");
            Assert.IsFalse(memory.NothingNew(Report("status", "P3, weiter so.", 3), T.AddSeconds(60), out _), "gleicher Platz im Bericht");
            Assert.IsTrue(memory.NothingNew(Report("status", "P3, weiter so.", 3), T.AddSeconds(5), out _), "Mosport 16.11.: nicht im selben Atemzug");
            memory.Remember(Report("vehicle", "Auto in Ordnung, keine Schäden."), T);
            Assert.IsFalse(memory.SaidRecently(Report("vehicle", "Auto in Ordnung, keine Schäden."), T.AddSeconds(200)));
            // Ein anderer Wächter mit demselben Satz bleibt gesperrt.
            Assert.IsTrue(memory.SaidRecently(new ObservedRaceCall { Topic = "race-status", Text = "Platz 3. Seit dem Start 1 Platz gutgemacht.", Priority = 2,
                Member = TeamMember.Ben, SessionId = 5, DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) }, T.AddSeconds(30)));
        }

        [TestCase(true), TestCase(false)]
        public void Configured_reports_come_at_roberts_intervals_in_a_steady_race(bool active)
        {
            // Robs Cockpit 23.11.: Rennstatus 120 s, Wetter 300 s, Rennfortschritt 180 s, je −7/+8 s.
            var prefs = new AiRadioPreferences();
            foreach (var o in AiRadioPreferences.Options) prefs.Set(o.Id, 0);
            prefs.Set("status", 120); prefs.Set("weather", 300); prefs.Set("progress", 180);
            foreach (var id in new[] { "status", "weather", "progress" }) prefs.SetTolerance(id, 7, 8);
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 5; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.ClassPosition = 3; gs.SessionData.SessionNumberOfLaps = 30; gs.SessionData.SessionLapsRemaining = 20; gs.SessionData.CompletedLaps = 10;
            gs.carClass = new CarData.CarClass(); gs.PositionAndMotionData.CarSpeed = 60;
            gs.Conditions.UpdateLiveSample(T, 10, 1, 18, 24, 0, 0, 0, 0, 0, false, CrewChiefV4.Events.ConditionsMonitor.TrackStatus.UNKNOWN);
            var schedule = new AiRadioSchedule(3);
            var heard = new Dictionary<string, int>();
            for (int t = 0; t <= 1200; t++)
            {
                var now = T.AddSeconds(t);
                schedule.Observe(gs, now, prefs);
                var call = schedule.Take(new RaceDataContext { GameState = gs, NowUtc = now, LastTelemetryUtc = now }, prefs, false, null, !active);
                if (call == null) continue;
                call.MarkPlaybackStarted(now); call.MarkPlaybackFinished(now.AddSeconds(3), false);
                heard[call.Topic] = (heard.TryGetValue(call.Topic, out int n) ? n : 0) + 1;
            }
            int Heard(string topic) => heard.TryGetValue(topic, out int n) ? n : 0;
            Assert.GreaterOrEqual(Heard("status"), 9, "Rennstatus alle 113–128 s");
            Assert.GreaterOrEqual(Heard("progress"), 6, "Rennfortschritt alle 173–188 s");
            if (active) Assert.GreaterOrEqual(Heard("weather"), 3, "Aktiv: Wetter auch unverändert alle 293–308 s");
            else Assert.LessOrEqual(Heard("weather"), 1, "Normal: unverändertes Wetter wird gebündelt");
        }
    }
}
