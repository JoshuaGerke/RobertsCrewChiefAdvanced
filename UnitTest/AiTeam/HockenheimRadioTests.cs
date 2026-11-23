using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// RaceRoom Hockenheim 23.11. (Tatus F4, 25 Autos, 15 Minuten): Rob war fast die ganze Zeit im Pulk, die Crew blieb still.
    /// Diese Tests sichern die Antworten darauf: Dreher hinter Rob, Saras Feldnachrichten, Funkstille als Ausgleich,
    /// eigene Ergebnisse trotz Anspannung, Rennbilanz nach dem Ziel und Begrüßung/Abschluss, die nie blockiert werden.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class HockenheimRadioTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 23, 20, 38, 0, DateTimeKind.Utc);
        private string wasLanguage;

        [SetUp]
        public void SetUp() { wasLanguage = RadioLanguage.Current; RadioLanguage.Current = RadioLanguage.German; }

        [TearDown]
        public void TearDown() { RadioLanguage.Current = wasLanguage; }

        private static FieldCarView Car(string name, float gapM, float gapS, CarMotion motion = CarMotion.Normal, int lapDelta = 0, bool inPit = false) =>
            new FieldCarView { Name = name, GapM = gapM, GapS = gapS, Motion = motion, MotionSinceUtc = T.AddSeconds(-1), LapDelta = lapDelta, InPit = inPit };

        private static FieldView View(params FieldCarView[] cars)
        {
            var player = new FieldCarView { Name = "Rob", IsPlayer = true, GapM = 0, GapS = 0 };
            return new FieldView { Utc = T, Session = FieldSession.Race, Cars = cars.Concat(new[] { player }).ToList(), Player = player };
        }

        private static GameStateData Race()
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 7; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            return s;
        }

        [Test]
        public void Spin_right_behind_rob_is_found_with_name()
        {
            var view = View(Car("Iban Garcia", -40, -.9f, CarMotion.Spun), Car("Walter Becker", 80, 2.8f, CarMotion.Spun));
            var incident = RearIncidentWatch.Nearest(view, T);
            Assert.IsNotNull(incident, "Rob 21:38: „Hinter mir hat sich jemand gedreht.“");
            Assert.AreEqual("Iban Garcia", incident.Name, "nur hinter Rob, nicht der Wagen voraus");
            Assert.IsFalse(incident.Stopped);
        }

        [Test]
        public void Rear_watch_ignores_what_does_not_concern_rob()
        {
            Assert.IsNull(RearIncidentWatch.Nearest(View(Car("A", -40, -.9f, CarMotion.Spun, lapDelta: -1)), T), "überrundet");
            Assert.IsNull(RearIncidentWatch.Nearest(View(Car("A", -40, -.9f, CarMotion.Spun, inPit: true)), T), "Box");
            Assert.IsNull(RearIncidentWatch.Nearest(View(Car("A", -600, -6f, CarMotion.Spun)), T), "zu weit hinten");
            Assert.IsNull(RearIncidentWatch.Nearest(View(Car("A", -40, -.9f, CarMotion.Normal)), T), "fährt normal");
            var fresh = Car("A", -40, -.9f, CarMotion.Spun); fresh.MotionSinceUtc = T.AddMilliseconds(-100);
            Assert.IsNull(RearIncidentWatch.Nearest(View(fresh), T), "noch nicht 300 ms bestätigt");
        }

        [Test]
        public void Rear_watch_does_not_repeat_itself()
        {
            var watch = new RearIncidentWatch();
            var gs = Race();
            var view = View(Car("Iban Garcia", -40, -.9f, CarMotion.Spun));
            Assert.IsNotNull(watch.Take(view, gs, T));
            view.Utc = T.AddSeconds(5);
            Assert.IsNull(watch.Take(view, gs, T.AddSeconds(5)), "höchstens alle 12 s");
            view.Utc = T.AddSeconds(30);
            Assert.IsNull(watch.Take(view, gs, T.AddSeconds(30)), "derselbe Wagen höchstens alle 90 s");
            var other = View(Car("Luca Berger", -60, -1.2f, CarMotion.Stopped)); other.Utc = T.AddSeconds(30);
            var call = watch.Take(other, gs, T.AddSeconds(30));
            Assert.IsNotNull(call, "ein anderer Wagen nach 12 s");
            Assert.IsTrue(call.Stopped);
        }

        [Test]
        public void Rear_incident_phrases_vary_in_every_language()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var watch = new RearIncidentWatch();
                var spun = Enumerable.Range(0, 5).Select(_ => watch.Text("Iban Garcia", false)).Distinct().ToList();
                var stopped = Enumerable.Range(0, 4).Select(_ => watch.Text("Iban Garcia", true)).Distinct().ToList();
                Assert.GreaterOrEqual(spun.Count, 4, lang);
                Assert.GreaterOrEqual(stopped.Count, 4, lang);
                Assert.IsTrue(spun.All(t => t.Contains("Iban Garcia")), lang);
            }
        }

        [Test]
        public void Sara_says_pit_retire_and_pit_exit_of_near_rivals()
        {
            foreach (var t in new[] { "field-retired", "field-left", "field-dq", "field-pit", "field-pit-exit" })
                Assert.IsTrue(NativeFieldEvents.SaraNews(t), t);
            Assert.IsFalse(NativeFieldEvents.SaraNews("field-tyres"));
            Assert.IsTrue(NativeFieldEvents.NearRival(7, 4), "drei Plätze dahinter");
            Assert.IsTrue(NativeFieldEvents.NearRival(1, 12), "der Führende immer");
            Assert.IsFalse(NativeFieldEvents.NearRival(9, 4), "fünf Plätze weg");
            Assert.IsFalse(NativeFieldEvents.NearRival(4, 4), "nicht Rob selbst");
        }

        [Test]
        public void Long_silence_lowers_the_bar_in_a_fight()
        {
            Assert.AreEqual(0, RadioLoad.SilenceRelief(15, 1), 1e-9, "Normal: erst ab 20 s Stille");
            Assert.AreEqual(.2, RadioLoad.SilenceRelief(30, 1), 1e-9);
            Assert.AreEqual(.5, RadioLoad.SilenceRelief(200, 1), 1e-9, "höchstens 0,5");
            Assert.Greater(RadioLoad.SilenceRelief(15, -1), 0, "Viel mehr Infos: schon nach 8 s");
            Assert.AreEqual(0, RadioLoad.SilenceRelief(30, 2), 1e-9, "Ruhig: erst nach 35 s");

            var load = new RadioLoad { DensityOverride = 1 };
            load.Update(.7, T);
            Assert.IsFalse(load.Allows("tyre-wear", 3, T, out _), "direkt im Pulk wartet die Technik");
            load.Update(.7, T.AddSeconds(40));
            Assert.IsTrue(load.SilenceEarned(T.AddSeconds(40)));
            Assert.IsTrue(load.Allows("tyre-wear", 3, T.AddSeconds(40), out string why), "nach 40 s Funkstille darf sie kommen: " + why);
        }

        [Test]
        public void Fight_information_counts_as_race_event()
        {
            foreach (var t in new[] { "battle-sector", "gap-trend", "reader-pace-front", "rear-pressure", "insight-battle-ahead" })
                Assert.IsTrue(RadioLoad.FightContext(t), t);
            foreach (var t in new[] { "story-free", "tyre-wear", "weather", "field-tyres" })
                Assert.IsFalse(RadioLoad.FightContext(t), t);
            var load = new RadioLoad { DensityOverride = 1 };
            load.Update(.9, T);
            Assert.IsTrue(load.Allows("gap-trend", 3, T, out _), "Näherkommen gehört in den Zweikampf");
            Assert.IsFalse(load.Allows("tyre-wear", 3, T, out _));
        }

        [Test]
        public void Own_results_and_fastest_lap_pass_full_tension()
        {
            var load = new RadioLoad { DensityOverride = 1 };
            load.Update(1, T);
            foreach (var t in new[] { "own-lap", "first-lap-fuel", "field-fastest", "field-fastest-overall" })
                Assert.IsTrue(load.Allows(t, 3, T, out string why), t + ": " + why);
            Assert.IsTrue(RaceRhythm.StartEssential("field-fastest"), "Rob: Name und schnellste Runde, auch in der Startphase");
        }

        [Test]
        public void Greeting_start_and_debrief_are_never_blocked_by_the_radio_logic()
        {
            var load = new RadioLoad { DensityOverride = 2 };
            load.Update(1, T);
            for (int i = 0; i < 6; i++) load.NoteSpoken("status", "Platz drei, Abstand nach vorn zwei Komma acht Sekunden, nach hinten null Komma vier.", T.AddSeconds(i));
            foreach (var t in new[] { "greeting", "race-start", "session-debrief" })
                Assert.IsTrue(load.Allows(t, 3, T.AddSeconds(6), out string why), t + ": " + why);
            foreach (RacePhase phase in Enum.GetValues(typeof(RacePhase)))
                foreach (RaceAttention attention in Enum.GetValues(typeof(RaceAttention)))
                {
                    var x = new RaceSituation { Utc = T, SessionId = 7, Phase = phase, Attention = attention };
                    foreach (var t in new[] { "greeting", "race-start", "session-debrief" })
                    {
                        var call = new ObservedRaceCall { Topic = t, Text = t, Priority = 3, SessionId = 7, SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };
                        Assert.AreNotEqual(MindVerdict.Drop, RaceMind.Judge(x, call, T, out string reason), t + " " + phase + " " + attention + ": " + reason);
                    }
                }
        }


        private static IEnumerable<TestCaseData> PackSims()
        {
            foreach (var sim in new[] { RoutineRaceSimulationTests.AMS2, RoutineRaceSimulationTests.R3E, RoutineRaceSimulationTests.LMU,
                RoutineRaceSimulationTests.ACE, RoutineRaceSimulationTests.IRacing })
                foreach (int minutes in new[] { 15, 30 })
                    yield return new TestCaseData(sim, minutes).SetName("Pulkrennen_" + sim.Name.Replace(" ", "_") + "_" + minutes + "_min");
        }

        /// <summary>
        /// Hockenheim 23.11.: 72 % der Zeit Zweikampf, im Rennen kam fast kein Routinebericht. Im Pulk kommen alle Berichte
        /// trotzdem, Vorder- und Hintermann am häufigsten.
        /// </summary>
        [TestCaseSource(nameof(PackSims))]
        public void Pack_race_still_brings_every_report(RoutineRaceSimulationTests.SimProfile sim, int minutes)
        {
            RoutineRaceSimulationTests.PackRace = true;
            try
            {
                var run = RoutineRaceSimulationTests.Run(minutes, RadioAmount.Normal, sim);
                string report = sim + ", Pulk, " + run.Report();
                TestContext.WriteLine(report);
                foreach (var topic in AutoRadioPlan.Topics)
                {
                    if (topic == "vehicle" && !sim.Damage) continue;
                    Assert.IsNotEmpty(run.Spoken[topic], topic + " kam im Pulk nie\n" + report);
                }
                Assert.GreaterOrEqual(run.Spoken["ahead"].Count, minutes / 5, "Vordermann im Pulk\n" + report);
                Assert.GreaterOrEqual(run.Spoken["behind"].Count, minutes / 5, "Hintermann im Pulk\n" + report);
            }
            finally { RoutineRaceSimulationTests.PackRace = false; }
        }


        [Test]
        public void Several_pit_stops_become_one_sentence_with_all_names()
        {
            string pit = NativeFieldEvents.BundleText("pit", new[] { "Walter Becker", "Iban Garcia", "Luca Berger" }, 0);
            StringAssert.Contains("Walter Becker, Iban Garcia und Luca Berger", pit);
            StringAssert.Contains("Box", pit);
            Assert.AreEqual(4, Enumerable.Range(0, 4).Select(i => NativeFieldEvents.BundleText("pit-exit", new[] { "A", "B" }, i)).Distinct().Count());
            Assert.AreEqual(4, Enumerable.Range(0, 4).Select(i => NativeFieldEvents.BundleText("out", new[] { "A", "B" }, i)).Distinct().Count());
            RadioLanguage.Current = RadioLanguage.English;
            StringAssert.Contains("A and B", NativeFieldEvents.BundleText("pit", new[] { "A", "B" }, 0));
        }

        [Test]
        public void Passer_is_named_from_the_track_when_positions_update_out_of_step()
        {
            var cls = new CarData.CarClass();
            var gs = Race();
            gs.carClass = cls;
            gs.SessionData.TrackDefinition = new TrackDefinition("Hockenheimring", 4574f);
            gs.SessionData.CompletedLaps = 2; gs.SessionData.ClassPosition = 4;
            gs.PositionAndMotionData.DistanceRoundTrack = 2119; gs.PositionAndMotionData.CarSpeed = 17;
            gs.OpponentData["garcia"] = new OpponentData { DriverRawName = "Iban Garcia", ClassPosition = 3, CarClass = cls, CompletedLaps = 2, DistanceRoundTrack = 2122, Speed = 17, IsActive = true };
            gs.OpponentData["becker"] = new OpponentData { DriverRawName = "Walter Becker", ClassPosition = 2, CarClass = cls, CompletedLaps = 2, DistanceRoundTrack = 2176, Speed = 30, IsActive = true };
            Assert.AreEqual("garcia", ActiveRaceObserver.PasserJustAhead(gs, 4), "21:35:08: Garcia 3 m vor Rob, jetzt einen Platz besser");
            gs.OpponentData["garcia"].DistanceRoundTrack = 2300;
            Assert.IsNull(ActiveRaceObserver.PasserJustAhead(gs, 4), "180 m voraus ist kein Überholer");
        }

        [Test]
        public void Time_rob_talks_or_waits_for_an_answer_is_credited_to_queued_calls()
        {
            DriverRadioGate.Reset();
            try
            {
                long at = DriverRadioGate.BusyTicks(T);
                DriverRadioGate.Pressed(T);
                DriverRadioGate.Released(T.AddSeconds(4));
                Assert.AreEqual(4 + DriverRadioGate.AfterRelease.TotalSeconds, DriverRadioGate.Credit(at, T.AddSeconds(10)).TotalSeconds, .01);
                long at2 = DriverRadioGate.BusyTicks(T.AddSeconds(10));
                DriverRadioGate.AnswerStarted(T.AddSeconds(10));
                Assert.AreEqual(5, DriverRadioGate.Credit(at2, T.AddSeconds(15)).TotalSeconds, .01, "Antwort läuft");
                DriverRadioGate.AnswerEnded(T.AddSeconds(20));
                Assert.AreEqual(10, DriverRadioGate.Credit(at2, T.AddSeconds(60)).TotalSeconds, .01);
                DriverRadioGate.AnswerStarted(T.AddSeconds(60));
                Assert.AreEqual(DriverRadioGate.MaxCredit, DriverRadioGate.Credit(at, T.AddSeconds(200)), "höchstens 25 s");
            }
            finally { DriverRadioGate.Reset(); }
        }

        [Test]
        public void Questions_about_incidents_and_sectors_look_at_the_field()
        {
            Assert.IsTrue(AiTeamService.NeedsTools("Doch eben gerade hinter mir hat sich jemand gedreht. Habt ihr das wahrgenommen?", null));
            Assert.IsTrue(AiTeamService.NeedsTools("Auch keine Unfälle, richtig?", null));
            Assert.IsTrue(AiTeamService.NeedsTools("Wie sehen denn meine Sektorenzeiten aus zu den vorderen Führenden?", null));
            Assert.IsTrue(AiTeamService.NeedsTools("Did someone spin behind me?", null));
            Assert.IsFalse(AiTeamService.NeedsTools("Mein Spritverbrauch.", null));
        }

        private static FieldView Finish()
        {
            var cars = new List<FieldCarView>
            {
                new FieldCarView { Name = "Juan Ayuso", PlaceClass = 1, Place = 1, CompletedLaps = 9, GapS = 9.4f, BestLap = 103.317f, TopSpeed = 61f },
                new FieldCarView { Name = "Walter Becker", PlaceClass = 2, Place = 2, CompletedLaps = 9, GapS = 2.6f, BestLap = 103.9f, TopSpeed = 60.2f },
                new FieldCarView { Name = "Rob", IsPlayer = true, PlaceClass = 3, Place = 3, CompletedLaps = 9, GapS = 0, BestLap = 104.148f, TopSpeed = 60.5f },
                new FieldCarView { Name = "Iban Garcia", PlaceClass = 4, Place = 4, CompletedLaps = 9, GapS = -4.9f, BestLap = 104.6f, TopSpeed = 59.8f },
            };
            return new FieldView { Utc = T, Session = FieldSession.Race, Cars = cars, Player = cars[2] };
        }

        private static List<ChronicleEntry> Chronicle() => new List<ChronicleEntry>
        {
            new ChronicleEntry { Kind = ChronicleKind.Grid, Position = 4 },
            new ChronicleEntry { Kind = ChronicleKind.PositionLost, Detail = ChronicleRecorder.OnTrack, Subject = "Iban Garcia", SubjectKey = "g", Lap = 1 },
            new ChronicleEntry { Kind = ChronicleKind.PositionGained, Detail = ChronicleRecorder.OnTrack, Subject = "Iban Garcia", SubjectKey = "g", Lap = 2 },
        };

        [Test]
        public void Race_debrief_names_podium_gaps_start_duel_fastest_lap_and_top_speed()
        {
            string text = SessionDebrief.ComposeRace(Finish(), "Rob", Chronicle(), 0);
            StringAssert.Contains("Gewonnen hat Juan Ayuso", text);
            StringAssert.Contains("Walter Becker mit 6,8 Sekunden", text, "Abstand zum Sieger aus dem Lagebild");
            StringAssert.Contains("Platz 3", text);
            StringAssert.Contains("von 4 gestartet, 1 Platz gewonnen", text);
            StringAssert.Contains("Iban Garcia", text, "kleiner Rennbericht: der Zweikampf des Rennens");
            StringAssert.Contains("Schnellste Rennrunde: Juan Ayuso, 1:43,317", text);
            StringAssert.Contains("Rang 3", text);
            StringAssert.Contains("Höchsttempo: du 218 km/h, Rang 2. Am schnellsten war Juan Ayuso mit 220", text);
        }

        [Test]
        public void Race_debrief_has_five_intros_and_five_closings_per_mood_in_each_language()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var texts = Enumerable.Range(0, 5).Select(i => SessionDebrief.ComposeRace(Finish(), "Rob", Chronicle(), i)).ToList();
                Assert.AreEqual(5, texts.Distinct().Count(), lang);
                Assert.AreEqual(5, SessionDebrief.CloseGood().Distinct().Count(), lang);
                Assert.AreEqual(5, SessionDebrief.CloseTough().Distinct().Count(), lang);
                Assert.IsTrue(texts.Any(t => SessionDebrief.CloseGood().Any(t.EndsWith)), lang + ": Platz gewonnen = gute Motivation");
                Assert.AreEqual(5, Enumerable.Range(0, 5).Select(i => SessionDebrief.Story(Chronicle(), i)).Distinct().Count(), lang + ": Rennbericht variiert");
            }
        }

        [Test]
        public void Race_debrief_after_a_tough_race_motivates_for_the_next_one()
        {
            var view = Finish();
            view.Player.PlaceClass = view.Player.Place = 5;
            var chronicle = Chronicle(); chronicle[0].Position = 2;
            string text = SessionDebrief.ComposeRace(view, "Rob", chronicle, 1);
            Assert.IsTrue(SessionDebrief.CloseTough().Any(text.EndsWith), text);
            StringAssert.Contains("Platz 5, von 2 gestartet", text);
            StringAssert.DoesNotContain("gewonnen,", text);
        }

        [Test]
        public void Race_debrief_without_measured_gaps_still_names_the_podium()
        {
            var view = Finish();
            foreach (var c in view.Cars) c.GapS = float.NaN;
            string text = SessionDebrief.ComposeRace(view, "Rob", new List<ChronicleEntry>(), 0);
            StringAssert.Contains("Juan Ayuso", text);
            StringAssert.Contains("Walter Becker", text);
            StringAssert.DoesNotContain(" mit NaN", text);
            StringAssert.DoesNotContain("gestartet", text, "ohne Startplatz kein Vergleich");
            Assert.IsNull(SessionDebrief.ComposeRace(new FieldView { Session = FieldSession.Practice, Cars = view.Cars, Player = view.Player }, "Rob", null, 0));
        }
    }
}
