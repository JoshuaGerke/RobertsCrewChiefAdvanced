using System;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Gefahrenmeldungen aus der LMU-Aufnahme vom 21.11.</summary>
    [TestFixture, Category("AiTeam")]
    public class HazardCallRegressionTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 21, 33, 25, DateTimeKind.Utc);

        [Test]
        public void A_parked_car_is_reminded_once_on_every_new_lap()
        {
            Assert.AreEqual(1, RaceTrafficWatch.ParkedCall(0, 0, 5), "erste Sichtung: volle Meldung");
            Assert.AreEqual(2, RaceTrafficWatch.ParkedCall(1, 5, 5), "zweite Sichtung: kurze Erinnerung");
            Assert.AreEqual(0, RaceTrafficWatch.ParkedCall(2, 5, 5), "in derselben Runde nicht noch einmal");
            Assert.AreEqual(2, RaceTrafficWatch.ParkedCall(2, 5, 6), "nächste Runde: wieder eine Erinnerung");
            Assert.AreEqual(2, RaceTrafficWatch.ParkedCall(5, 8, 9));
        }

        [Test]
        public void A_spin_far_ahead_or_not_yet_confirmed_is_not_called()
        {
            Assert.IsFalse(RaceTrafficWatch.SpinCallable(400, T.AddSeconds(-2), T), "Galygin drehte sich 400 m voraus");
            Assert.IsFalse(RaceTrafficWatch.SpinCallable(200, T, T.AddMilliseconds(200)), "noch nicht bestätigt");
            Assert.IsFalse(RaceTrafficWatch.SpinCallable(200, DateTime.MinValue, T));
            Assert.IsFalse(RaceTrafficWatch.SpinCallable(0, T.AddSeconds(-1), T));
            Assert.IsTrue(RaceTrafficWatch.SpinCallable(200, T, T.AddMilliseconds(300)));
            Assert.IsTrue(RaceTrafficWatch.SpinCallable(RaceTrafficWatch.SpinReachM, T.AddSeconds(-1), T));
        }

        [Test]
        public void Only_urgent_hazards_reach_the_priority_that_may_cut_an_answer()
        {
            int urgent = CrewSpeechPolicy.InterruptAnswerPriority;
            Assert.AreEqual(urgent, RaceTrafficWatch.HazardPriority(false, false, false, true, false, 200));
            Assert.AreEqual(urgent, RaceTrafficWatch.HazardPriority(false, false, false, false, true, 100));
            Assert.Less(RaceTrafficWatch.HazardPriority(false, false, false, false, true, 300), urgent, "stehend, aber noch weit weg");
            Assert.Less(RaceTrafficWatch.HazardPriority(false, false, true, false, true, 100), 7, "Erinnerung unterbricht nichts");
            Assert.Less(RaceTrafficWatch.HazardPriority(true, false, false, false, false, 50), urgent, "Verkehr von hinten");
            Assert.Less(RaceTrafficWatch.HazardPriority(false, true, false, false, false, 50), urgent, "schnellere Klasse");
        }

        [Test]
        public void An_answer_to_Rob_gives_way_only_to_an_urgent_hazard()
        {
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForHazard(true, false, false, 5, 7, null), "langsames Auto 200 m voraus");
            Assert.IsTrue(CrewSpeechPolicy.CanInterruptForHazard(true, false, false, 5, 8, null), "Dreher direkt voraus");
            Assert.IsTrue(CrewSpeechPolicy.CanInterruptForHazard(true, false, false, 1, 7, "status"), "Routine darf weiter weichen");
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForHazard(false, false, false, 1, 9, null), "Crew-Chief-Kern bleibt unberührt");
        }

        [Test]
        public void A_flag_is_never_dropped_because_the_same_driver_was_just_named()
        {
            var s = new RadioSituation { StartPhase = true, SameHazardNamed = true, Now = T };
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("warning-flag-yellow-sector", s, out _));
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("warning-rc-fcy", s, out _));
            Assert.AreEqual(RadioVerdict.Drop, RadioDirectorPolicy.Decide("race-hazard-ahead", s, out var reason));
            Assert.AreEqual("race_start_same_hazard_named", reason);
            Assert.IsTrue(RadioDirectorPolicy.IsFlag("warning-flag-blue"));
            Assert.IsFalse(RadioDirectorPolicy.IsFlag("warning-rc-penalty"));
            Assert.IsFalse(RadioDirectorPolicy.IsFlag("warning-rc-giveback"));
            Assert.IsFalse(RadioDirectorPolicy.IsFlag("race-hazard-ahead"));
        }

        [Test]
        public void The_retirement_call_survives_the_exclusion_it_triggers()
        {
            Assert.IsTrue(RaceExclusion.Allowed("race-retired"));
            var s = new RadioSituation { PlayerOut = true, Now = T };
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("race-retired", s, out _));
            Assert.AreEqual(RadioVerdict.Drop, RadioDirectorPolicy.Decide("status", s, out _));
        }

        [Test]
        public void Damage_and_the_start_verdict_belong_to_the_start_essentials()
        {
            Assert.IsTrue(RaceRhythm.StartEssential("start-verdict"));
            Assert.IsTrue(RaceRhythm.StartEssential("incident-damage"));
            Assert.IsTrue(RaceRhythm.StartEssential("incident-damage-heavy"));
            Assert.IsFalse(RaceRhythm.StartEssential("status"));
        }
    }
}
