using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Befunde aus dem LMU-Test vom 18.11.2026 (Interlagos, Multiclass, GT3 gegen LMP2 und Hypercar). Die Situationen sind
    /// den Aufnahmen nachgebildet; die Werte (Restzeit 377 s, 2,5 l pro Runde, 1:36,9, Rad hinten rechts) stammen aus dem Log.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class LmuRaceFindingsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 18, 21, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { wasDeterministic = RadioVariants.Deterministic; RadioLanguage.Current = RadioLanguage.German; RadioVariants.Deterministic = true; }

        private bool wasDeterministic;

        [TearDown]
        public void Restore() { RadioLanguage.Current = RadioLanguage.German; RadioVariants.Deterministic = wasDeterministic; MulticlassTraffic.LastRearClassCallUtc = DateTime.MinValue; }

        private static GameStateData State(SessionType type = SessionType.Race)
        {
            var gs = new GameStateData(T.Ticks);
            var s = gs.SessionData;
            s.SessionId = 9; s.SessionType = type; s.SessionPhase = SessionPhase.Green; s.ClassPosition = 12; s.OverallPosition = 23; s.CompletedLaps = 1;
            s.TrackDefinition = new TrackDefinition("Interlagos", 4273);
            gs.PositionAndMotionData.CarSpeed = 50; gs.PositionAndMotionData.DistanceRoundTrack = 1000;
            gs.TyreData.TyreWearActive = true;
            return gs;
        }

        // ---------- Multiclass ----------

        [Test]
        public void Other_class_traffic_names_the_class_in_every_language()
        {
            Assert.AreEqual("LMP2", MulticlassTraffic.Label(new CarData.CarClass { carClassEnum = CarData.CarClassEnum.LMP2 }));
            Assert.AreEqual("Hypercar", MulticlassTraffic.Label(new CarData.CarClass { carClassEnum = CarData.CarClassEnum.HYPER_CAR_RACE }));
            Assert.AreEqual("GT3", MulticlassTraffic.Label(new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 }));
            Assert.IsNull(MulticlassTraffic.Label(null));
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                foreach (Func<string, int, string> make in new Func<string, int, string>[] { MulticlassTraffic.Rear, MulticlassTraffic.RearGroup, MulticlassTraffic.Ahead })
                {
                    var lines = Enumerable.Range(0, 5).Select(v => make("LMP2", v)).ToList();
                    Assert.AreEqual(5, lines.Distinct().Count(), lang + ": fünf Formulierungen");
                    Assert.IsTrue(lines.All(l => l.Contains("LMP2")), lang + ": Klasse wird genannt");
                    Assert.IsTrue(Enumerable.Range(0, 5).Select(v => make(null, v)).All(l => !l.Contains("  ") && char.IsUpper(l[0])), lang + ": ohne Klasse sauber");
                }
            }
            RadioLanguage.Current = RadioLanguage.German;
            StringAssert.Contains("lass ihn durch", MulticlassTraffic.Rear("LMP2", 0).ToLowerInvariant());
            StringAssert.Contains("Kolonne", MulticlassTraffic.RearGroup("LMP2", 0));
            CollectionAssert.Contains(MulticlassTraffic.PreparedPhrases.ToList(), MulticlassTraffic.Rear("Hypercar", 3), "als Originalclip vorbereitet");
            Assert.IsTrue(CrewSpeechPolicy.IsPreparedPhrase(TeamMember.Ben, MulticlassTraffic.RearGroup("LMP2", 2)));
        }

        [Test]
        public void Blue_flag_is_not_repeated_right_after_the_class_call()
        {
            MulticlassTraffic.LastRearClassCallUtc = T;
            Assert.IsTrue(FlagWatch.BlueCoveredByClassCall(T.AddSeconds(8)), "gerade „LMP2 von hinten, lass ihn durch“ gesagt");
            Assert.IsFalse(FlagWatch.BlueCoveredByClassCall(T.AddSeconds(25)), "später wieder normale blaue Flagge");
            MulticlassTraffic.LastRearClassCallUtc = DateTime.MinValue;
        }

        // ---------- Restzeit ----------

        [Test]
        public void Remaining_time_matches_the_clock_on_screen()
        {
            Assert.AreEqual("6 Minuten 17", LocalAnswers.TimeLeftText(377.4), "LMU-Quali: Anzeige 6:17, gesagt wurde 5:18");
            Assert.AreEqual("28 Minuten", LocalAnswers.TimeLeftText(1690), "Training: Anzeige 28:10, gesagt wurde 29");
            Assert.AreEqual("eine Minute 5", LocalAnswers.TimeLeftText(65));
            Assert.AreEqual("45 Sekunden", LocalAnswers.TimeLeftText(45.9));
            Assert.IsTrue(LocalAnswers.AskedTimeLeft("wie viel zeit bleibt mir noch?"));
            Assert.IsTrue(LocalAnswers.AskedTimeLeft("how much time left?"));
            Assert.IsTrue(LocalAnswers.AskedTimeLeft("¿cuánto tiempo queda?"));
            Assert.IsFalse(LocalAnswers.AskedTimeLeft("wie viel zeit verliere ich pro runde?"), "Rundenfrage ist keine Restzeit");
            var gs = State(SessionType.Qualify);
            gs.SessionData.SessionHasFixedTime = true; gs.SessionData.SessionTimeRemaining = 377.4f;
            Assert.AreEqual("Noch 6 Minuten 17.", LocalAnswers.TryAnswer("Wie viel Zeit bleibt mir noch?", gs, null));
        }

        // ---------- Sprit für eine genannte Renndistanz ----------

        [Test]
        public void Fuel_for_a_thirty_minute_race_is_worked_out_from_the_driver_figures()
        {
            string rob = "Nein, geh von den zweieinhalb Litern aus und rechne hoch, was das bei 30 Minuten bedeutet, wie viel ich sicher mitnehmen soll.";
            Assert.IsTrue(RaceFuelPlan.AsksFuelForDistance(rob));
            RaceFuelPlan.Distance(rob, out double seconds, out int laps);
            Assert.AreEqual(1800, seconds, .01);
            Assert.AreEqual(2.5f, RaceFuelPlan.StatedPerLap(rob), .001f);
            Assert.IsTrue(RaceFuelPlan.Compute(1800, -1, 96.9f, 2.5f, out int raceLaps, out double needed, out double reserve));
            Assert.AreEqual(20, raceLaps, "19 angefangene Runden plus eine nach Ablauf");
            Assert.AreEqual(50, needed, .01);
            Assert.AreEqual(53, reserve, .01);
            var gs = State(SessionType.Qualify); gs.SessionData.PlayerLapTimeSessionBest = 96.9f;
            string answer = RaceFuelPlan.Answer(rob, gs, null);
            StringAssert.Contains("20 Runden", answer);
            StringAssert.Contains("53 Liter", answer);
            StringAssert.Contains("dein Wert", answer, "Herkunft des Verbrauchs wird genannt");
            Assert.IsTrue(RaceFuelPlan.Compute(-1, 12, -1, 2.7f, out raceLaps, out needed, out reserve));
            Assert.AreEqual(12, raceLaps);
            Assert.IsFalse(RaceFuelPlan.AsksFuelForDistance("Wie viel Sprit habe ich?"), "Tankstand ist keine Planung");
        }

        // ---------- Schaden, Box, Aufgabe ----------

        [Test]
        public void Lost_wheel_or_destroyed_tyre_is_recognised()
        {
            var gs = State();
            gs.TyreData.RightRearAttached = false;
            Assert.AreEqual(3, PitDamageWatch.LostCorner(gs, null, -1, out bool gone));
            Assert.IsTrue(gone);
            gs = State();
            gs.TyreData.FrontLeftPercentWear = gs.TyreData.FrontRightPercentWear = gs.TyreData.RearLeftPercentWear = 3;
            gs.TyreData.RearRightPercentWear = 100;
            Assert.AreEqual(3, PitDamageWatch.LostCorner(gs, new float[] { 2, 2, 3, 3 }, .3, out gone), "21:08:29: hinten rechts von 3 auf 100 Prozent");
            Assert.IsFalse(gone);
            Assert.AreEqual(-1, PitDamageWatch.LostCorner(gs, new float[] { 2, 2, 3, 99 }, .3, out _), "schon vorher voll: kein neuer Befund");
            Assert.AreEqual(-1, PitDamageWatch.LostCorner(gs, new float[] { 2, 2, 3, 3 }, 10, out _), "zu alter Vergleich");
            string urgent = PitDamageWatch.Urgent("Rob", 3, true, 0);
            StringAssert.Contains("Rad hinten rechts ist weg", urgent);
            StringAssert.Contains("Box", urgent);
            StringAssert.DoesNotContain("komisch", urgent);
        }

        [Test]
        public void Damaged_car_is_called_in_supported_through_the_stop_and_back_out()
        {
            var watch = new PitDamageWatch();
            var gs = State();
            Func<TeamMember, bool> all = _ => true;
            watch.Observe(gs, null, "Rob", T);
            Assert.IsNull(watch.Take(gs, T, false, all));
            gs.TyreData.RightRearAttached = false;
            watch.Observe(gs, null, "Rob", T.AddSeconds(1));
            var call = watch.Take(gs, T.AddSeconds(1), false, all);
            Assert.IsNotNull(call, "Rad verloren");
            Assert.AreEqual("pit-urgent-damage", call.Topic);
            Assert.AreEqual(TeamMember.Mik, call.Member);
            Assert.GreaterOrEqual(call.Priority, 8);
            call.MarkPlaybackStarted(T.AddSeconds(1)); call.MarkPlaybackFinished(T.AddSeconds(4), false);
            watch.Observe(gs, null, "Rob", T.AddSeconds(20));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(20), false, all), "Erinnerung frühestens nach 40 s");
            watch.Observe(gs, null, "Rob", T.AddSeconds(45));
            call = watch.Take(gs, T.AddSeconds(45), false, all);
            Assert.IsNotNull(call, "Erinnerung");
            call.MarkPlaybackStarted(T.AddSeconds(45)); call.MarkPlaybackFinished(T.AddSeconds(47), false);

            gs.PitData.InPitlane = true; gs.PositionAndMotionData.CarSpeed = 16;
            watch.Observe(gs, null, "Rob", T.AddSeconds(60));
            call = watch.Take(gs, T.AddSeconds(60), false, all);
            Assert.IsNotNull(call, "Zuspruch an der Boxeneinfahrt");
            Assert.AreEqual("pit-arrival-support", call.Topic);
            Assert.IsTrue(call.IsValid(gs, T.AddSeconds(60)), "gilt in der Boxengasse");
            call.MarkPlaybackStarted(T.AddSeconds(60)); call.MarkPlaybackFinished(T.AddSeconds(63), false);

            gs.PositionAndMotionData.CarSpeed = 0; gs.TyreData.RightRearAttached = true;
            watch.Observe(gs, null, "Rob", T.AddSeconds(70));
            watch.Observe(gs, null, "Rob", T.AddSeconds(101));
            call = watch.Take(gs, T.AddSeconds(101), false, all);
            Assert.IsNotNull(call, "Standzeit-Kommentar nach 30 s");
            Assert.AreEqual("pit-repair-wait", call.Topic);
            StringAssert.Contains("31 Sekunden", call.Text);
            call.MarkPlaybackStarted(T.AddSeconds(101)); call.MarkPlaybackFinished(T.AddSeconds(105), false);

            gs.PitData.InPitlane = false; gs.PositionAndMotionData.CarSpeed = 30;
            watch.Observe(gs, null, "Rob", T.AddSeconds(180));
            watch.Observe(gs, null, "Rob", T.AddSeconds(184));
            call = watch.Take(gs, T.AddSeconds(184), false, all);
            Assert.IsNotNull(call, "Infos und Zuspruch nach dem Wiedereinsetzen");
            Assert.AreEqual("pit-rejoin", call.Topic);
            StringAssert.Contains("Klassenplatz 12", call.Text);
            StringAssert.Contains("Freie Bahn", call.Text);
        }

        [Test]
        public void Retirement_gets_a_few_good_words()
        {
            var watch = new PitDamageWatch();
            var gs = State(); gs.PitData.InPitlane = true; gs.PositionAndMotionData.CarSpeed = 0;
            watch.Observe(gs, null, "Rob", T);
            gs.SessionData.IsDNF = true;
            watch.Observe(gs, null, "Rob", T.AddSeconds(1));
            var call = watch.Take(gs, T.AddSeconds(1), false, _ => true);
            Assert.IsNotNull(call);
            Assert.AreEqual("race-retired", call.Topic);
            Assert.AreEqual(TeamMember.Ben, call.Member);
            StringAssert.StartsWith("Rob, ", call.Text);
            gs.SessionData.SessionPhase = SessionPhase.Finished;
            Assert.IsTrue(call.IsValid(gs, T.AddSeconds(2)), "auch nach der Zielflagge noch gültig");
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.AreEqual(6, Enumerable.Range(0, 6).Select(v => PitDamageWatch.Retired("Rob", v)).Distinct().Count(), lang);
                Assert.IsTrue(Enumerable.Range(0, 6).All(v => char.IsUpper(PitDamageWatch.Retired(null, v)[0])), lang + ": ohne Namen groß");
                Assert.AreEqual(5, Enumerable.Range(0, 5).Select(v => PitDamageWatch.Arrival("Rob", v)).Distinct().Count(), lang);
                Assert.AreEqual(4, Enumerable.Range(0, 4).Select(v => PitDamageWatch.CrewReady(v)).Distinct().Count(), lang);
            }
        }

        [Test]
        public void Serious_damage_advice_says_box_and_is_not_repeated_word_for_word()
        {
            var d = new CarDamageData { DamageEnabled = true, OverallAeroDamage = DamageLevel.MAJOR, OverallEngineDamage = DamageLevel.NONE, OverallTransmissionDamage = DamageLevel.NONE };
            string major = IncidentWatch.DamageReport(d);
            StringAssert.Contains("komm an die Box", major);
            StringAssert.DoesNotContain("komisch", major);
            d.OverallAeroDamage = DamageLevel.DESTROYED;
            StringAssert.Contains("Komm sofort an die Box", IncidentWatch.DamageReport(d));
        }

        [Test]
        public void No_backmarker_call_on_the_way_into_the_pits()
        {
            var gs = State();
            gs.PositionAndMotionData.DistanceRoundTrack = 3900;
            Assert.IsFalse(PitDamageWatch.HeadingToPits(gs), "kein Stopp angemeldet");
            gs.PitData.HasRequestedPitStop = true;
            Assert.IsTrue(PitDamageWatch.HeadingToPits(gs), "LMU 21:17: Stopp angemeldet, kurz vor der Einfahrt");
            gs.PositionAndMotionData.DistanceRoundTrack = 1200;
            Assert.IsFalse(PitDamageWatch.HeadingToPits(gs), "noch weit weg von der Box");
            gs.PitData.InPitlane = true; gs.PositionAndMotionData.DistanceRoundTrack = 3900;
            Assert.IsFalse(PitDamageWatch.HeadingToPits(gs), "schon in der Gasse – eigene Regeln");
        }

        // ---------- Strafen, Überholen, Namen ----------

        [Test]
        public void Stop_and_go_from_the_game_message_is_named_with_its_reason()
        {
            string text = IncidentWatch.DetailedPenalty(PenatiesData.DetailedPenaltyType.STOP_AND_GO, PenatiesData.DetailedPenaltyCause.SPEEDING_IN_PITLANE);
            StringAssert.Contains("Stop-and-Go", text);
            StringAssert.Contains("Boxengasse", text);
            RadioLanguage.Current = RadioLanguage.English;
            StringAssert.Contains("drive-through", IncidentWatch.DetailedPenalty(PenatiesData.DetailedPenaltyType.DRIVE_THROUGH, PenatiesData.DetailedPenaltyCause.NONE));
        }

        [Test]
        public void Position_gained_without_passing_on_track_is_no_overtake()
        {
            var gs = State();
            var near = new OpponentData { CompletedLaps = 1, DistanceRoundTrack = 900, Speed = 45 };
            Assert.IsTrue(DriverJudgementWatch.PassedOnTrack(gs, near), "100 m hinter uns: echtes Überholen");
            var far = new OpponentData { CompletedLaps = 1, DistanceRoundTrack = 3500, Speed = 45 };
            Assert.IsFalse(DriverJudgementWatch.PassedOnTrack(gs, far), "LMU 21:10: Gegner weit weg, Rob hatte niemanden überholt");
            var parked = new OpponentData { CompletedLaps = 1, DistanceRoundTrack = 950, Speed = 0 };
            Assert.IsFalse(DriverJudgementWatch.PassedOnTrack(gs, parked), "stehendes Auto ist kein Überholmanöver");
        }

        [Test]
        public void Mis_decoded_names_and_online_tags_are_cleaned_for_radio()
        {
            Assert.AreEqual("Autódromo José Carlos Pace", RadioPhraseCatalog.RepairEncoding("AutÃ³dromo JosÃ© Carlos Pace"));
            Assert.AreEqual("Autódromo José Carlos Pace", SessionGreetingDirector.SpokenTrack("AutÃ³dromo JosÃ© Carlos Pace"));
            Assert.AreEqual("Sebring School Circuit", RadioPhraseCatalog.RepairEncoding("Sebring School Circuit"));
            Assert.AreEqual("Antonino Sammito", RadioPhraseCatalog.SafeRivalName("Antonino Sammito4797"));
            Assert.AreEqual("Car 33", RadioPhraseCatalog.SafeRivalName("Car 33"));
            Assert.AreEqual("Online Alessandro", RadioPhraseCatalog.SafeRivalName("Online Alessandro"));
        }

        // ---------- Qualifying ----------

        [Test]
        public void Flying_lap_window_keeps_room_for_the_lap_start_call()
        {
            var gs = State(SessionType.Qualify);
            gs.PitData.OnOutLap = true; gs.PositionAndMotionData.DistanceRoundTrack = 3600;
            Assert.IsTrue(SessionLapObserver.FlyingLapWindow(gs), "letztes Viertel der Aufwärmrunde");
            gs.PositionAndMotionData.DistanceRoundTrack = 1500;
            Assert.IsFalse(SessionLapObserver.FlyingLapWindow(gs));
            gs.SessionData.SessionType = SessionType.Race; gs.PositionAndMotionData.DistanceRoundTrack = 3600;
            Assert.IsFalse(SessionLapObserver.FlyingLapWindow(gs), "nicht im Rennen");
        }

        [Test]
        public void Gap_below_one_second_is_spoken_with_tenths()
        {
            string text = SessionLapObserver.OutlapGapText(-1, false, .3, true, 1);
            StringAssert.Contains("0,3 Sekunden", text);
            StringAssert.DoesNotContain(" 0 Sekunden", text);
        }
    }
}
