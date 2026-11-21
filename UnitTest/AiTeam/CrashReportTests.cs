using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>LMU 21.11., 21:38: Abflug mit Einschlag, Rad hinten rechts ab, danach Aufgabe.</summary>
    [TestFixture, Category("AiTeam")]
    public class CrashReportTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 21, 38, 20, DateTimeKind.Utc);

        private static GameStateData Race(int position = 12)
        {
            var gs = new GameStateData(T.Ticks);
            var s = gs.SessionData;
            s.SessionId = 9; s.SessionType = SessionType.Race; s.SessionPhase = SessionPhase.Green;
            s.ClassPosition = position; s.OverallPosition = position; s.CompletedLaps = 6;
            s.TrackDefinition = new TrackDefinition("Algarve", 4635);
            gs.carClass = new CarData.CarClass();
            gs.PositionAndMotionData.CarSpeed = 50; gs.PositionAndMotionData.DistanceRoundTrack = 1000;
            gs.TyreData.TyreWearActive = true;
            return gs;
        }

        private static CarDamageData WheelOff(bool aero)
        {
            var d = new CarDamageData { DamageEnabled = true, OverallAeroDamage = aero ? DamageLevel.MAJOR : DamageLevel.NONE,
                OverallEngineDamage = DamageLevel.NONE, OverallTransmissionDamage = DamageLevel.NONE };
            d.WheelDetached = new[] { false, false, false, true };
            d.WheelFlat = new bool[4];
            return d;
        }

        [Test]
        public void After_the_urgent_box_call_the_assessment_only_adds_what_is_new()
        {
            string full = IncidentWatch.DamageReport(WheelOff(true));
            StringAssert.Contains("Rad hinten rechts", full);
            string extra = IncidentWatch.DamageReport(WheelOff(true), wheelsAlreadySaid: true);
            Assert.IsNotNull(extra);
            StringAssert.Contains("Aero", extra);
            StringAssert.DoesNotContain("Rad", extra);
            StringAssert.DoesNotContain("Box", extra);
            Assert.IsNull(IncidentWatch.DamageReport(WheelOff(false), wheelsAlreadySaid: true), "nur das Rad: alles schon gesagt");
        }

        [Test]
        public void An_unspoken_urgent_call_merges_into_the_assessment()
        {
            var watch = new PitDamageWatch(); var gs = Race(); Func<TeamMember, bool> all = _ => true;
            watch.Observe(gs, null, "Rob", T);
            gs.TyreData.RightRearAttached = false;
            watch.Observe(gs, null, "Rob", T.AddSeconds(3));
            Assert.IsTrue(watch.AbsorbUrgent(T.AddSeconds(4)), "Bewertung kommt, Boxenruf noch nicht gesprochen");
            Assert.IsNull(watch.Take(gs, T.AddSeconds(4), false, all));
            Assert.IsFalse(watch.UrgentJustSpoken(T.AddSeconds(4)));
        }

        [Test]
        public void A_spoken_urgent_call_is_not_repeated_by_the_assessment()
        {
            var watch = new PitDamageWatch(); var gs = Race(); Func<TeamMember, bool> all = _ => true;
            watch.Observe(gs, null, "Rob", T);
            gs.TyreData.RightRearAttached = false;
            watch.Observe(gs, null, "Rob", T.AddSeconds(3));
            var urgent = watch.Take(gs, T.AddSeconds(3), false, all);
            Assert.IsNotNull(urgent);
            urgent.MarkPlaybackStarted(T.AddSeconds(3));
            watch.Observe(gs, null, "Rob", T.AddSeconds(4));
            Assert.IsFalse(watch.AbsorbUrgent(T.AddSeconds(7)));
            Assert.IsTrue(watch.UrgentJustSpoken(T.AddSeconds(7)));
            Assert.IsFalse(watch.UrgentJustSpoken(T.AddSeconds(40)));
        }

        [Test]
        public void A_damage_report_that_already_said_box_replaces_the_first_urgent_call()
        {
            var watch = new PitDamageWatch(); var gs = Race(); Func<TeamMember, bool> all = _ => true;
            watch.Observe(gs, null, "Rob", T);
            watch.NoteDamageReportStarted(T.AddSeconds(1));
            gs.TyreData.RightRearAttached = false;
            watch.Observe(gs, null, "Rob", T.AddSeconds(3));
            Assert.IsNull(watch.Take(gs, T.AddSeconds(3), false, all), "Rad und Box sind gerade gesagt");
            watch.Observe(gs, null, "Rob", T.AddSeconds(50));
            var reminder = watch.Take(gs, T.AddSeconds(50), false, all);
            Assert.IsNotNull(reminder, "die Erinnerung zur Box bleibt");
            Assert.AreEqual("pit-urgent-damage", reminder.Topic);
        }

        [Test]
        public void Losing_places_after_heavy_damage_gets_no_we_will_win_them_back()
        {
            var gs = Race(13);
            gs.CarDamageData = WheelOff(true);
            var watch = new DriverJudgementWatch(1);
            watch.Observe(gs, null, false, "Rob", true, T);
            gs.SessionData.ClassPosition = 17; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(4));
            gs.SessionData.ClassPosition = 21; watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(10));
            watch.Observe(gs, null, false, "Rob", true, T.AddSeconds(16));
            var call = watch.Take(gs, T.AddSeconds(16), false, true);
            Assert.IsTrue(call == null || call.Topic != "judge-dropped");
        }

        private static GameStateData Moving(float mps, bool offTrack)
        {
            var gs = Race(); gs.PositionAndMotionData.CarSpeed = mps; gs.PenaltiesData.IsOffRacingSurface = offTrack; return gs;
        }

        [Test]
        public void One_driver_check_per_incident()
        {
            var watch = new IncidentWatch();
            var a = Moving(25, true); var b = Moving(25, true); var c = Moving(2, true);
            Assert.IsEmpty(watch.Evaluate(Moving(25, false), a, T, false));
            var off = watch.Evaluate(a, b, T.AddSeconds(2.5), false);
            Assert.AreEqual(1, off.Count, "Abflug gemeldet");
            Assert.IsEmpty(watch.Evaluate(b, c, T.AddSeconds(3), false), "stehend direkt danach: keine zweite Nachfrage");
            Assert.IsTrue(watch.RecentCheck(T.AddSeconds(3)));
            Assert.IsFalse(watch.RecentCheck(T.AddSeconds(15)));
        }

        [Test]
        public void A_stop_without_an_earlier_check_is_still_asked_about()
        {
            var watch = new IncidentWatch();
            var a = Moving(25, false); var b = Moving(2, false);
            watch.Evaluate(Moving(25, false), a, T, false);
            Assert.AreEqual(1, watch.Evaluate(a, b, T.AddSeconds(1), false).Count);
        }
    }
}
