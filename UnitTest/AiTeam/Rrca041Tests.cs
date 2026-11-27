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

        [Test]
        public void Games_with_part_damage_keep_their_report()
        {
            var d = new CarDamageData { DamageEnabled = true, OverallAeroDamage = DamageLevel.MAJOR, OverallEngineDamage = DamageLevel.NONE, OverallTransmissionDamage = DamageLevel.NONE };
            Assert.IsFalse(RepairPicture.Applies(d));
            StringAssert.Contains("Aero", IncidentWatch.DamageReport(d));
        }
    }
}
