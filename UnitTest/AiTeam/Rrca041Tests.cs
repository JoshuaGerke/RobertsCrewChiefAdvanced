using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Befunde RaceRoom Laguna Seca und iRacing Snetterton/Montreal vom 27.11.2026 (Arbeitsmappe 0.41).</summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class Rrca041Tests
    {
        [SetUp] public void Setup() { RadioLanguage.Current = RadioLanguage.German; }

        private static CarDamageData IRacingDamage(bool required, bool optional, float g = -1, string kind = null)
        {
            return new CarDamageData { DamageEnabled = true, RepairRequired = required, RepairOptional = optional, ImpactPeakG = g, ImpactKind = kind };
        }

        // I1: iRacing liefert keine Bauteile, aber Pflicht-/optionale Reparatur, Art und Wucht.
        [Test]
        public void IRacing_heavy_wall_hit_names_force_kind_and_mandatory_repair()
        {
            var d = IRacingDamage(true, false, 71.4f, "wall");
            string report = IncidentWatch.DamageReport(d);
            StringAssert.Contains("Sehr harter Einschlag in die Begrenzung, rund 71 g", report);
            StringAssert.Contains("Pflichtreparatur nötig", report);
            StringAssert.Contains("Box", report);
            StringAssert.DoesNotContain("Schadenswerte", report);
            Assert.IsTrue(IncidentWatch.IsSerious(d));
            Assert.IsTrue(PitDamageWatch.Major(d));
        }

        [Test]
        public void IRacing_optional_only_is_not_serious_and_says_car_runs()
        {
            var d = IRacingDamage(false, true, 8f, "car");
            string report = IncidentWatch.DamageReport(d, true);
            StringAssert.Contains("Leichter Einschlag mit einem anderen Auto", report);
            StringAssert.Contains("Nur optionale Reparatur", report);
            Assert.IsFalse(IncidentWatch.IsSerious(d));
            Assert.AreEqual(false, NumericDamageWatch.NothingNotable(d));
        }

        [Test]
        public void IRacing_without_repairs_reports_clean_and_routine_sees_nothing_notable()
        {
            var d = IRacingDamage(false, false);
            Assert.AreEqual("Kein Reparaturbedarf gemeldet.", IncidentWatch.DamageReport(d, true));
            Assert.AreEqual(true, NumericDamageWatch.NothingNotable(d));
        }

        [Test]
        public void IRacing_incident_flags_map_to_kind()
        {
            Assert.AreEqual("offtrack", RepairPicture.IncidentKind(0x2));
            Assert.AreEqual("wall", RepairPicture.IncidentKind(0x5));
            Assert.AreEqual("car", RepairPicture.IncidentKind(0x0308));
            Assert.IsNull(RepairPicture.IncidentKind(0));
            Assert.Greater(RepairPicture.KindRank("wall"), RepairPicture.KindRank("offtrack"));
        }

        [Test]
        public void IRacing_pit_repair_time_says_mandatory_and_optional_part()
        {
            var d = IRacingDamage(true, true);
            d.RepairSecondsRequired = 44.6f; d.RepairSecondsOptional = 70;
            Assert.AreEqual("Reparatur: 45 Sekunden Pflicht, dazu 1:10 Minuten optional. Den optionalen Teil kannst du abbrechen, wenn du früher raus willst.",
                RepairPicture.PitTime(d));
            d.RepairSecondsRequired = -1; d.RepairSecondsOptional = -1;
            Assert.IsNull(RepairPicture.PitTime(d));
        }

        [Test]
        public void Damage_question_uses_tools()
        {
            Assert.IsTrue(AiTeamService.NeedsTools("Und was sagst du, alles kaputt oder was siehst du?", null));
        }

        // R1: RaceRoom-Garage (gestapelte KI-Autos, Tempo 0, nicht in der Box) ist keine Gefahr; Sprung der Position beendet die Warnung.
        private static readonly DateTime T = new DateTime(2026, 11, 27, 21, 0, 0, DateTimeKind.Utc);
        private static CrewChiefV4.AiTeam.Field.FieldView Field(double at, params CrewChiefV4.AiTeam.Field.FieldCarView[] others)
        {
            var player = new CrewChiefV4.AiTeam.Field.FieldCarView { IsPlayer = true, Name = "Robert Gerke", Motion = CrewChiefV4.AiTeam.Field.CarMotion.Normal, LapDistance = 2700, Speed = 50 };
            var cars = new System.Collections.Generic.List<CrewChiefV4.AiTeam.Field.FieldCarView> { player };
            cars.AddRange(others);
            foreach (var c in others) c.MotionSinceUtc = T.AddSeconds(at - 10);
            return new CrewChiefV4.AiTeam.Field.FieldView { Utc = T.AddSeconds(at), Sim = "RaceRoom", Track = "Laguna Seca", TrackLength = 3585.5f, Player = player, Cars = cars };
        }
        private static CrewChiefV4.AiTeam.Field.FieldCarView Parked(string name, float lap, float gapM) => new CrewChiefV4.AiTeam.Field.FieldCarView
        { Name = name, LapDistance = lap, GapM = gapM, GapS = gapM / 50f, Speed = 0, Motion = CrewChiefV4.AiTeam.Field.CarMotion.Stopped, MotionSinceUtc = T.AddSeconds(-10) };
        private static GameStateData RaceRoomQuali()
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 7; s.SessionData.SessionType = SessionType.Qualify; s.SessionData.SessionPhase = SessionPhase.Green;
            s.PositionAndMotionData.CarSpeed = 50;
            return s;
        }

        [Test]
        public void Stacked_garage_cars_are_no_hazard_and_the_spot_stays_known()
        {
            var watch = new YellowAheadWatch();
            var gs = RaceRoomQuali();
            Assert.IsNull(watch.Take(Field(0, Parked("Alexander Santos", 3032f, 330), Parked("Whate Verfool", 3032.1f, 330)), gs, T), "Garage");
            Assert.IsNull(watch.Take(Field(30, Parked("Alex Fernandez", 3032f, 330)), gs, T.AddSeconds(30)), "später ein einzelnes Auto am selben Platz");
            Assert.IsNotNull(watch.Take(Field(60, Parked("Sven Lutz", 1244f, 330)), gs, T.AddSeconds(60)), "echtes stehendes Auto");
        }

        [Test]
        public void Warning_ends_when_the_car_jumps_away()
        {
            Assert.IsTrue(YellowAheadWatch.StillThere(Field(0, Parked("claudio ciao", 2054f, 330)), "claudio ciao", T, 2054f));
            Assert.IsFalse(YellowAheadWatch.StillThere(Field(0, Parked("claudio ciao", 3032f, 330)), "claudio ciao", T, 2054f), "in die Garage gesprungen");
        }

        // R2: gleiche Zeit mit 0,4 ms Unterschied ist kein Rückstand; im Mehrklassenfeld zählt die eigene Klasse.
        private static CrewChiefV4.AiTeam.Field.FieldView Timesheet(params CrewChiefV4.AiTeam.Field.FieldCarView[] others)
        {
            var player = new CrewChiefV4.AiTeam.Field.FieldCarView { IsPlayer = true, Name = "Robert Gerke", ClassId = 1, BestLap = 83.88257f, FastestAnyLap = 83.88257f };
            var cars = new System.Collections.Generic.List<CrewChiefV4.AiTeam.Field.FieldCarView> { player };
            cars.AddRange(others);
            return new CrewChiefV4.AiTeam.Field.FieldView { Utc = T, Sim = "RaceRoom", Track = "Laguna Seca", TrackLength = 3585.5f, Player = player, Cars = cars,
                Session = CrewChiefV4.AiTeam.Field.FieldSession.Practice };
        }
        private static CrewChiefV4.AiTeam.Field.FieldCarView Timed(string name, int cls, float best) =>
            new CrewChiefV4.AiTeam.Field.FieldCarView { Name = name, ClassId = cls, BestLap = best };

        [Test]
        public void Same_lap_within_5_ms_is_rank_one_without_gap()
        {
            var v = Timesheet(Timed("Echo", 1, 83.88214f), Timed("Wolfgang Kuck", 1, 87.343f));
            Assert.AreEqual(1, CrewChiefV4.AiTeam.Field.FieldFacts.RankOf(CrewChiefV4.AiTeam.Field.FieldFacts.Timesheet(v, true), 83.88257f));
            string text = CrewChiefV4.AiTeam.Field.SessionDebrief.Compose(v, "Rob", 2.0f, 4, 0);
            StringAssert.Contains("Rang 1.", text);
            StringAssert.DoesNotContain("Rückstand", text);
        }

        [Test]
        public void Multiclass_debrief_ranks_in_own_class()
        {
            var v = Timesheet(Timed("Tobias Maesgen", 2, 79.0f), Timed("Wolfgang Kuck", 1, 87.343f));
            string text = CrewChiefV4.AiTeam.Field.SessionDebrief.Compose(v, "Rob", 2.0f, 4, 0);
            StringAssert.Contains("Rang 1 in deiner Klasse", text);
            StringAssert.DoesNotContain("Rückstand", text);
            Assert.AreEqual(2, CrewChiefV4.AiTeam.Field.FieldFacts.RankOf(CrewChiefV4.AiTeam.Field.FieldFacts.Timesheet(v, false), 83.88257f), "gesamt");
        }

        // R3: Verbrauch aus dem Training gilt in der Quali vom Start weg.
        private static GameStateData Session(SessionType type, int id)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = id; s.SessionData.SessionType = type; s.SessionData.SessionPhase = SessionPhase.Green;
            s.SessionData.TrackDefinition = new TrackDefinition("WeatherTech Raceway Laguna Seca - Modern", 3585.5f);
            s.carName = "r3e-model-13572";
            return s;
        }

        [Test]
        public void Practice_fuel_is_the_baseline_for_quali()
        {
            try
            {
                FuelCarry.Reset();
                var practice = Session(SessionType.Practice, 1);
                FuelCarry.Remember(practice, 2.01f, 4);
                Assert.AreEqual(-1, FuelCarry.FromEarlierSessions(practice, out _, out _), "die laufende Session zählt nicht als frühere");
                var quali = Session(SessionType.Qualify, 2);
                Assert.AreEqual(2.01f, FuelCarry.FromEarlierSessions(quali, out int laps, out SessionType from), 1e-5);
                Assert.AreEqual(4, laps); Assert.AreEqual(SessionType.Practice, from);
                FuelCarry.Remember(quali, 2.05f, 2);
                Assert.AreEqual(2.01f, FuelCarry.FromEarlierSessions(quali, out _, out _), 1e-5, "eigene Quali-Runden bleiben außen vor");
                StringAssert.Contains("aus dem Training", SessionLapObserver.CarriedFuelText("2,0 Liter", 4, SessionType.Practice));
            }
            finally { FuelCarry.Reset(); }
        }

        // Rob 28.11.: Boxenlimit nicht beim Joinen in der Box, erst beim Losfahren aus der eigenen Box.
        [Test]
        public void Pit_limit_waits_until_rob_drives_off()
        {
            var s = Session(SessionType.Practice, 3);
            s.PitData.InPitlane = true; s.PitData.PitSpeedLimit = 60 / 3.6f; s.PositionAndMotionData.CarSpeed = 0; s.SessionData.SessionRunningTime = 10;
            var watch = new PitLaneWatch();
            watch.Observe(s, T); Assert.IsNull(watch.Take(s, T, false), "gerade gejoint, steht in der Box");
            s.SessionData.SessionType = SessionType.LonePractice; s.SessionData.SessionRunningTime = 11;
            watch.Observe(s, T.AddSeconds(1)); Assert.IsNull(watch.Take(s, T.AddSeconds(1), false), "Sitzungsart wechselt, Rob steht weiter");
            s.PositionAndMotionData.CarSpeed = 4; s.SessionData.SessionRunningTime = 12;
            watch.Observe(s, T.AddSeconds(2)); var call = watch.Take(s, T.AddSeconds(2), false);
            Assert.IsNotNull(call, "losgefahren"); StringAssert.Contains("60", call.Text);
            call.MarkPlaybackStarted(T.AddSeconds(2));
            s.SessionData.SessionRunningTime = 13; watch.Observe(s, T.AddSeconds(3)); Assert.IsNull(watch.Take(s, T.AddSeconds(3), false), "einmal");
        }

        [Test]
        public void Repair_time_is_said_once_while_standing_in_the_pit()
        {
            var s = Session(SessionType.Practice, 4);
            s.PitData.InPitlane = true; s.PositionAndMotionData.CarSpeed = 0;
            s.CarDamageData = IRacingDamage(true, true); s.CarDamageData.RepairSecondsRequired = 45; s.CarDamageData.RepairSecondsOptional = 70;
            var watch = new PitDamageWatch();
            watch.Observe(s, null, "Rob", T);
            var call = watch.Take(s, T, false, m => true);
            Assert.IsNotNull(call); Assert.AreEqual("pit-repair-time", call.Topic); StringAssert.Contains("45 Sekunden Pflicht", call.Text);
            call.MarkPlaybackStarted(T);
            s.CarDamageData.RepairSecondsRequired = 40;
            watch.Observe(s, null, "Rob", T.AddSeconds(5)); Assert.IsNull(watch.Take(s, T.AddSeconds(5), false, m => true), "einmal pro Stopp");
        }

        private static GameStateData Driving(float kmh, SessionType type = SessionType.Practice)
        {
            var s = Session(type, 5);
            s.PositionAndMotionData.CarSpeed = kmh / 3.6f;
            s.CarDamageData = IRacingDamage(false, false);
            return s;
        }

        // I1 im Ablauf: „Ich schau nach dem Auto“ wird nach 4 s mit dem iRacing-Befund eingelöst.
        [Test]
        public void IRacing_contact_promise_is_followed_by_the_repair_picture()
        {
            var watch = new IncidentWatch();
            var before = Driving(150);
            var hit = Driving(10);
            hit.CarDamageData = IRacingDamage(true, false, 71.4f, "wall");
            hit.CarDamageData.LastImpactTime = 442.9f;
            var first = watch.Evaluate(before, hit, T, false);
            Assert.AreEqual(1, first.Count, "Kontakt-Satz");
            watch.Evaluate(hit, hit, T.AddSeconds(4.5), false);
            var a = watch.TakeDamageAssessment();
            Assert.IsNotNull(a, "Befund kommt von selbst");
            Assert.IsTrue(a.Serious);
            StringAssert.Contains("71 g", a.LocalReport);
        }

        // I4: eingerollte schwarze Flagge für 0,5 s ist weder Slow-down-Strafe noch „abgesessen“.
        [Test]
        public void Brief_furled_flag_is_neither_penalty_nor_served()
        {
            var watch = new IncidentWatch();
            var quiet = Driving(150);
            var furled = Driving(150); furled.PenaltiesData.HasSlowDown = true; furled.PenaltiesData.NumOutstandingPenalties = 1;
            Assert.AreEqual(0, watch.Evaluate(quiet, furled, T, false).Count, "nicht sofort");
            var gone = Driving(150);
            var after = watch.Evaluate(furled, gone, T.AddSeconds(.5), false);
            Assert.AreEqual(0, after.Count, "kein „Strafe abgesessen“");
            Assert.AreEqual(0, watch.Evaluate(quiet, furled, T.AddSeconds(10), false).Count);
            var still = watch.Evaluate(furled, furled, T.AddSeconds(12.1), false);
            Assert.AreEqual(1, still.Count, "steht 2 s an: Slow-down gilt");
            StringAssert.Contains("Slow-down", still[0].Value);
            Assert.AreEqual(0, watch.Evaluate(furled, furled, T.AddSeconds(13), false).Count, "einmal");
        }

        [Test]
        public void Meatball_from_game_message_and_session_flag_is_said_once()
        {
            var gs = Driving(150);
            var o = new LiveWarningDispatcher();
            o.Offer("warning-rc-meatball", "Technik-Flagge. Rein in die Box, sobald es geht.", TeamMember.Ben, 8, gs, T, live => true, "race_control:iracing_session_flags:meatball");
            o.Offer("warning-rc-pen-meatball", "Rob, Meatball-Flagge: Das Auto muss zur Reparatur an die Box.", TeamMember.Ben, 9, gs, T, live => true, "native:cc");
            var first = o.Take(gs, T, false, m => true);
            Assert.IsNotNull(first);
            first.MarkPlaybackStarted(T);
            o.Observe(gs, T.AddSeconds(1));
            Assert.IsNull(o.Take(gs, T.AddSeconds(1), false, m => true), "zweite Meldung zum selben Meatball");
        }

        [Test]
        public void Greeting_is_not_repeated_after_a_rejoin_on_the_same_track()
        {
            Assert.IsTrue(AiTeamService.GreetedMomentsAgo("montreal", SessionType.Practice, T, "montreal", SessionType.Practice, T.AddSeconds(12)));
            Assert.IsFalse(AiTeamService.GreetedMomentsAgo("montreal", SessionType.Practice, T, "montreal", SessionType.Qualify, T.AddSeconds(12)), "neue Sitzungsart");
            Assert.IsFalse(AiTeamService.GreetedMomentsAgo("montreal", SessionType.Practice, T, "snetterton 300", SessionType.Practice, T.AddSeconds(12)), "andere Strecke");
            Assert.IsFalse(AiTeamService.GreetedMomentsAgo("montreal", SessionType.Practice, T, "montreal", SessionType.Practice, T.AddMinutes(4)), "später neu");
        }

        [Test]
        public void Same_stopped_car_is_called_at_most_twice_per_incident()
        {
            var watch = new YellowAheadWatch();
            var gs = RaceRoomQuali();
            Assert.IsNotNull(watch.Take(Field(0, Parked("Mani Racing", 1244f, 330)), gs, T));
            Assert.IsNotNull(watch.Take(Field(100, Parked("Mani Racing", 1244f, 330)), gs, T.AddSeconds(100)), "eine Erinnerung");
            Assert.IsNull(watch.Take(Field(200, Parked("Mani Racing", 1244f, 330)), gs, T.AddSeconds(200)), "nicht jede Runde");
            var moving = Parked("Mani Racing", 1300f, -200); moving.Motion = CrewChiefV4.AiTeam.Field.CarMotion.Normal; moving.Speed = 40;
            watch.Take(Field(250, moving), gs, T.AddSeconds(250));
            Assert.IsNotNull(watch.Take(Field(400, Parked("Mani Racing", 2000f, 330)), gs, T.AddSeconds(400)), "neuer Vorfall nach Weiterfahrt");
        }

        [Test]
        public void Games_with_part_damage_keep_their_report()
        {
            var d = new CarDamageData { DamageEnabled = true, OverallAeroDamage = DamageLevel.MAJOR, OverallEngineDamage = DamageLevel.NONE, OverallTransmissionDamage = DamageLevel.NONE };
            Assert.IsFalse(RepairPicture.Applies(d));
            StringAssert.Contains("Aero", IncidentWatch.DamageReport(d));
        }
    }
}
