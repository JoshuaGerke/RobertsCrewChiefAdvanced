using System;
using System.Collections.Generic;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// LMU 22.11. (Bahrain Paddock Circuit, Training – Qualifying – Rennen über 22 Minuten): Gelb kam nie, ein Dreher und ein
    /// stehendes Auto voraus wurden nicht gemeldet, Überholmanöver blieben ohne Reaktion, eingestellte Routineberichte kamen
    /// kaum, „Virtuelle Energie 0 Prozent“ bei 19 Litern, und „…, Sarah, …“ ging an Ben oder Mik.
    /// Zeitpunkte und Werte stammen aus der Aufnahme dieses Tages.
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class LmuBahrainFindingsTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 22, 11, 1, 20, DateTimeKind.Utc);

        [SetUp]
        public void Setup() { RadioLanguage.Current = RadioLanguage.German; }

        [TearDown]
        public void TearDown() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData Race(int sector, params int[] yellowSectors)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 4; s.SessionData.SessionType = SessionType.Race; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.CompletedLaps = 11; s.SessionData.Flag = FlagEnum.GREEN; s.SessionData.SectorNumber = sector;
            s.PositionAndMotionData.CarSpeed = 50;
            foreach (int y in yellowSectors) s.FlagData.sectorFlags[y - 1] = FlagEnum.YELLOW;
            return s;
        }

        // ---------- Gelb aus den Sektorflaggen (rF2, LMU, rF1, GTR2, ACC, R3E) ----------

        [Test]
        public void Lmu_sector_yellow_ahead_is_called_short_and_its_lifting_too()
        {
            var watch = new FlagWatch();
            CollectionAssert.IsEmpty(watch.Evaluate(Race(1), Race(1, 2), T), "erst bestätigen (1,5 s), kein Flackern");
            CollectionAssert.IsEmpty(watch.Evaluate(Race(1, 2), Race(1, 2), T.AddSeconds(1)));
            CollectionAssert.AreEqual(new[] { "Gelb voraus." }, watch.Evaluate(Race(1, 2), Race(1, 2), T.AddSeconds(1.6)), "nächster Sektor gelb");
            Assert.IsTrue(FlagWatch.StillRelevant("Gelb voraus.", Race(2, 2)), "Rob ist jetzt im gelben Sektor");
            CollectionAssert.IsEmpty(watch.Evaluate(Race(2, 2), Race(2, 2), T.AddSeconds(5)), "nur einmal");
            CollectionAssert.IsEmpty(watch.Evaluate(Race(2, 2), Race(2), T.AddSeconds(12)), "grün muss 2 s stehen");
            CollectionAssert.AreEqual(new[] { "Gelb aufgehoben." }, watch.Evaluate(Race(2), Race(2), T.AddSeconds(14.5)));
            Assert.IsTrue(FlagWatch.StillRelevant("Gelb aufgehoben.", Race(2)));
            Assert.IsFalse(FlagWatch.StillRelevant("Gelb aufgehoben.", Race(2, 3)), "wieder Gelb voraus: kein „aufgehoben“");
        }

        [Test]
        public void Driving_out_of_a_yellow_sector_is_not_called_lifted()
        {
            var watch = new FlagWatch();
            watch.Evaluate(Race(1), Race(1, 2), T);
            CollectionAssert.AreEqual(new[] { "Gelb voraus." }, watch.Evaluate(Race(1, 2), Race(2, 2), T.AddSeconds(2)));
            // Rob fährt in Sektor 3, Sektor 2 ist noch gelb – nichts zu sagen.
            for (double t = 3; t <= 20; t += 1) CollectionAssert.IsEmpty(watch.Evaluate(Race(3, 2), Race(3, 2), T.AddSeconds(t)), "t=" + t);
        }

        [Test]
        public void Yellow_behind_rob_is_not_called()
        {
            var watch = new FlagWatch();
            // Rob in Sektor 2, Gelb in Sektor 1 (hinter ihm, erst in einer Runde wieder dran).
            for (double t = 0; t <= 5; t += .5) CollectionAssert.IsEmpty(watch.Evaluate(Race(2, 1), Race(2, 1), T.AddSeconds(t)));
            Assert.AreEqual(-1, FlagWatch.YellowSectorAhead(Race(2, 1)));
            Assert.AreEqual(0, FlagWatch.YellowSectorAhead(Race(3, 1)), "aus Sektor 3 ist Sektor 1 der nächste");
        }

        [Test]
        public void Local_yellow_from_the_sim_is_not_doubled_by_the_sector()
        {
            // ACC/RaceRoom setzen zusätzlich lokales Gelb: dann reicht „Gelb! Nicht überholen.“
            var watch = new FlagWatch();
            var a = Race(2, 2); a.FlagData.isLocalYellow = true;
            CollectionAssert.AreEqual(new[] { FlagWatch.Phrases[0] }, watch.Evaluate(Race(2), a, T));
            CollectionAssert.IsEmpty(watch.Evaluate(a, a, T.AddSeconds(2)));
        }

        [Test]
        public void Saras_flag_lines_are_short()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                for (int i = 0; i < 3; i++) Assert.LessOrEqual(FlagWatch.Phrases[i].Split(' ').Length, 3, lang + ": " + FlagWatch.Phrases[i]);
            }
            RadioLanguage.Current = RadioLanguage.German;
            Assert.AreEqual(FlagWatch.Priority(FlagWatch.Phrases[1]), FlagWatch.Priority("Gelb vorbei."), "alter Satz wird weiter erkannt");
            Assert.AreEqual("gelb_sektor", FlagWatch.Kind("Gelb im Sektor."));
        }

        // ---------- Dreher oder stehendes Auto weiter voraus ----------

        private static FieldView Field(params FieldCarView[] others)
        {
            var player = new FieldCarView { IsPlayer = true, Name = "Robert Gerke", Motion = CarMotion.Normal, LapDistance = 2218 };
            var cars = new List<FieldCarView> { player };
            cars.AddRange(others);
            return new FieldView { Utc = T, Sim = "LMU", Track = "Bahrain Paddock Circuit", TrackLength = 3810.8f, Player = player, Cars = cars };
        }

        private static FieldCarView Car(string name, float gapM, CarMotion motion, double since = 2, bool pit = false) =>
            new FieldCarView { Name = name, GapM = gapM, GapS = gapM / 50f, Motion = motion, MotionSinceUtc = T.AddSeconds(-since), InPit = pit, LapDistance = 2218 + gapM };

        [Test]
        public void Spin_and_stopped_car_far_ahead_reach_sara_early()
        {
            // 11:01:35: udo scharny gedreht 959 m vor Rob, kurz davor stehend; die Nahbereichswarnung prüfte nur das Auto direkt vor Rob.
            var watch = new YellowAheadWatch();
            var gs = Race(2);
            var hazard = watch.Take(Field(Car("steve dionne", 380, CarMotion.Normal), Car("udo scharny", 959, CarMotion.Spun)), gs, T);
            Assert.IsNotNull(hazard);
            Assert.AreEqual("udo scharny", hazard.Name);
            Assert.IsFalse(hazard.Stopped);
            Assert.IsNull(watch.Take(Field(Car("udo scharny", 700, CarMotion.Stopped)), gs, T.AddSeconds(20)), "derselbe Wagen nicht gleich wieder");
            Assert.IsNotNull(new YellowAheadWatch().Take(Field(Car("udo scharny", 700, CarMotion.Stopped)), gs, T), "stehend");
            Assert.IsTrue(YellowAheadWatch.StillThere(Field(Car("udo scharny", 600, CarMotion.Slow)), "udo scharny", T));
            Assert.IsFalse(YellowAheadWatch.StillThere(Field(Car("udo scharny", 600, CarMotion.Normal)), "udo scharny", T), "fährt wieder: Warnung überholt");
        }

        [Test]
        public void Far_hazard_ignores_near_cars_cars_in_the_pits_and_rob_standing()
        {
            var gs = Race(2);
            Assert.IsNull(new YellowAheadWatch().Take(Field(Car("a", 150, CarMotion.Stopped)), gs, T), "Nahbereich hat eigene Warnung");
            Assert.IsNull(new YellowAheadWatch().Take(Field(Car("a", 1500, CarMotion.Stopped)), gs, T), "zu weit");
            Assert.IsNull(new YellowAheadWatch().Take(Field(Car("a", 600, CarMotion.Stopped, pit: true)), gs, T), "Box");
            Assert.IsNull(new YellowAheadWatch().Take(Field(Car("a", 600, CarMotion.Slow)), gs, T), "langsam allein ist keine Gefahr voraus");
            Assert.IsNull(new YellowAheadWatch().Take(Field(Car("a", 600, CarMotion.Spun, since: .1)), gs, T), "kurz bestätigen");
            var standing = Race(2); standing.PositionAndMotionData.CarSpeed = 3;
            Assert.IsNull(new YellowAheadWatch().Take(Field(Car("a", 600, CarMotion.Stopped)), standing, T));
            var pitLane = Race(2); pitLane.PitData.InPitlane = true;
            Assert.IsNull(new YellowAheadWatch().Take(Field(Car("a", 600, CarMotion.Stopped)), pitLane, T));
            var stale = Field(Car("a", 600, CarMotion.Stopped)); stale.Utc = T.AddSeconds(-5);
            Assert.IsNull(new YellowAheadWatch().Take(stale, gs, T), "altes Lagebild");
        }

        [Test]
        public void Far_hazard_says_yellow_only_when_the_sim_shows_yellow()
        {
            Assert.AreEqual("Gelb voraus, Kurve 4. Auto steht.", YellowAheadWatch.Text(true, "Kurve 4", 640, true));
            Assert.AreEqual("Achtung voraus, Kurve 4: Dreher.", YellowAheadWatch.Text(false, "Kurve 4", 640, false));
            Assert.AreEqual("Gelb voraus, in 600 Metern. Dreher.", YellowAheadWatch.Text(true, null, 640, false), "ohne Kurvennamen die Meter");
            Assert.IsTrue(YellowAheadWatch.SimShowsYellow(Race(2, 3)), "gelber Sektor irgendwo");
            Assert.IsFalse(YellowAheadWatch.SimShowsYellow(Race(2)));
            foreach (var lang in new[] { RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                StringAssert.DoesNotContain("  ", YellowAheadWatch.Text(true, null, 400, true));
                StringAssert.DoesNotContain("Gelb", YellowAheadWatch.Text(true, null, 400, true));
            }
        }

        [Test]
        public void Far_hazard_is_a_safety_call_with_priority()
        {
            Assert.AreEqual(RadioRank.Safety, RadioDirectorPolicy.Classify("warning-hazard-far"));
            Assert.AreEqual(RadioClass.Safety, RadioLoad.Classify("warning-hazard-far", 7));
            Assert.IsTrue(RaceRhythm.StartEssential("warning-hazard-far"), "auch in der Startphase");
        }

        // ---------- Eingestellte Routineberichte ----------

        [Test]
        public void A_finished_routine_report_is_not_thrown_away_for_braking()
        {
            var gs = Race(2); gs.ControlData.BrakePedal = .8f;
            Assert.IsTrue(AiRadioSchedule.BusyForRoutine(gs), "beim Bremsen startet kein neuer Bericht");
            Assert.IsFalse(AiRadioSchedule.InvalidatesRoutine(gs), "ein fertiger Bericht wartet die Bremszone ab");
            gs.FlagData.isLocalYellow = true;
            Assert.IsTrue(AiRadioSchedule.InvalidatesRoutine(gs), "unter Gelb gilt er nicht mehr");
            Assert.GreaterOrEqual(AiRadioSchedule.RoutineLifetimeSeconds, 20, "länger als Rennkopf-Halten und Datenpause nach Sicherheitsfunk");
            Assert.Greater(AiRadioSchedule.GuaranteedLifetimeSeconds, AiRadioSchedule.RoutineLifetimeSeconds);
        }

        // ---------- Platzmeldung ----------

        [Test]
        public void Own_position_change_lives_long_enough_to_get_through_the_queue()
        {
            // 10:56:49 Valgo geht durch: die Meldung wartete hinter „Durchatmen“ und „Blick nach hinten“ und verfiel nach 8 s.
            Assert.GreaterOrEqual(ActiveRaceObserver.PositionCallSeconds, 15);
            var load = new RadioLoad(); load.Update(1, T);
            Assert.IsTrue(load.Allows("position", 2, T, out _), "Platzverlust trotz Zweikampf-Anspannung");
        }

        // ---------- Virtuelle Energie ----------

        [Test]
        public void Zero_virtual_energy_while_driving_with_fuel_is_not_reported()
        {
            // Rennen 22.11.: „Virtuelle Energie 0 Prozent, 2,0 pro Runde – das reicht für etwa 0,0 Runden“ bei 19–34 Litern.
            Assert.IsNull(VirtualEnergyWatch.Report(0, 2f, 9.7f, 0));
            var ve = new VirtualEnergyWatch();
            var practice = Race(1); practice.SessionData.SessionType = SessionType.Practice; practice.SessionData.SessionId = -1;
            practice.SessionData.PreviousLapWasValid = true;
            practice.SessionData.CompletedLaps = 3; ve.Observe(practice, 96f);
            practice.SessionData.CompletedLaps = 4; ve.Observe(practice, 93.5f);
            Assert.AreEqual(2.5f, ve.PerLap, .01f);
            var race = Race(1); race.SessionData.SessionId = -1; // LMU: dieselbe Sitzungsnummer in allen Sitzungen
            ve.Observe(race, 0f);
            Assert.AreEqual(-1f, ve.Level, "0 % = Energie nicht genutzt");
            Assert.AreEqual(-1f, ve.PerLap, "Trainingsverbrauch gilt nicht im Rennen");
        }

        // ---------- Sara mit Namen mitten im Satz ----------

        [Test]
        public void Sara_named_later_in_the_sentence_gets_the_question()
        {
            Assert.AreEqual(TeamMember.Sara, TeamRouter.Route("Die Gelbphasen kriegst du aber auch mit, ja, Sarah, und meldest die, oder?", TeamMember.Mik).Member);
            Assert.AreEqual(TeamMember.Sara, TeamRouter.Route("Ich rede von Sarah, nicht von dir. Die soll das melden.", TeamMember.Ben).Member);
            Assert.AreEqual(TeamMember.Sara, TeamRouter.Route("machen. Ich spreche mit Sarah. Das war die Idee.", TeamMember.Ben).Member);
            Assert.AreEqual(TeamMember.Mik, TeamRouter.Route("Kannst du das bitte prüfen, Mik?", TeamMember.Ben).Member);
            Assert.AreNotEqual(TeamMember.Mik, TeamRouter.Route("Wie weit ist Mike Rockenfeller vor mir?", TeamMember.Ben).Member, "Gegnername ist keine Anrede");
            Assert.IsNull(TeamRouter.AddressedLater("Frag mal Ben und Sara."), "zwei Namen ohne Anrede: wie bisher");
        }
    }
}
