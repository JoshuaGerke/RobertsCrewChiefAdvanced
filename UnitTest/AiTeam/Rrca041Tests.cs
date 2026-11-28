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

        [Test]
        public void Games_with_part_damage_keep_their_report()
        {
            var d = new CarDamageData { DamageEnabled = true, OverallAeroDamage = DamageLevel.MAJOR, OverallEngineDamage = DamageLevel.NONE, OverallTransmissionDamage = DamageLevel.NONE };
            Assert.IsFalse(RepairPicture.Applies(d));
            StringAssert.Contains("Aero", IncidentWatch.DamageReport(d));
        }
    }
}
