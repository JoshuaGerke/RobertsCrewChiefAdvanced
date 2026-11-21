using System;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Funkregie: Sicherheit vor Pflicht vor Ereignis vor Daten, geprüft an Fällen aus den Aufnahmen vom 21.11.</summary>
    [TestFixture, Category("AiTeam")]
    public class RadioDirectorPolicyTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 15, 36, 14, DateTimeKind.Utc);

        [Test]
        public void Topics_are_ranked_safety_mandatory_event_data()
        {
            Assert.AreEqual(RadioRank.Safety, RadioDirectorPolicy.Classify("race-hazard-ahead"));
            Assert.AreEqual(RadioRank.Safety, RadioDirectorPolicy.Classify("warning-rc-yellow-2"));
            Assert.AreEqual(RadioRank.Mandatory, RadioDirectorPolicy.Classify("warning-penalty"));
            Assert.AreEqual(RadioRank.Mandatory, RadioDirectorPolicy.Classify("warning-flag-5"));
            Assert.AreEqual(RadioRank.Mandatory, RadioDirectorPolicy.Classify("pit-limit"));
            Assert.AreEqual(RadioRank.Event, RadioDirectorPolicy.Classify("position-loss"));
            Assert.AreEqual(RadioRank.Event, RadioDirectorPolicy.Classify("own-lap"));
            Assert.AreEqual(RadioRank.Data, RadioDirectorPolicy.Classify("race-summary"));
            Assert.AreEqual(RadioRank.Data, RadioDirectorPolicy.Classify("status"));
        }

        [Test]
        public void Data_waits_after_a_safety_call()
        {
            // 15:36:11 Zwischenstand, 15:36:14 „Gelb in Sektor 2“, 15:36:18 der Zwischenstand noch einmal
            var s = new RadioSituation { LastSafetyUtc = T, Now = T.AddSeconds(4) };
            Assert.AreEqual(RadioVerdict.Hold, RadioDirectorPolicy.Decide("race-summary", s, out var why));
            Assert.AreEqual("data_waits_after_safety_call", why);
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("race-hazard-ahead", s, out _), "Sicherheit wartet nie");
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("position-loss", s, out _), "Ereignis wartet nicht");
            s.Now = T.AddSeconds(7);
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("race-summary", s, out _));
        }

        [Test]
        public void Start_phase_and_player_out_drop_what_does_not_belong()
        {
            var start = new RadioSituation { StartPhase = true, Now = T };
            Assert.AreEqual(RadioVerdict.Drop, RadioDirectorPolicy.Decide("position-loss", start, out _));
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("race-hazard-ahead", start, out _));
            start.SameHazardNamed = true;
            Assert.AreEqual(RadioVerdict.Drop, RadioDirectorPolicy.Decide("warning-rc-yellow-1", start, out var why));
            Assert.AreEqual("race_start_same_hazard_named", why);
            var outOfRace = new RadioSituation { PlayerOut = true, Now = T };
            Assert.AreEqual(RadioVerdict.Drop, RadioDirectorPolicy.Decide("pit-limit", outOfRace, out _));
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("warning-flag-5", outOfRace, out _));
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("warning-penalty", outOfRace, out _));
        }

        [Test]
        public void All_clear_does_not_interrupt()
        {
            // 15:33:05: „Gelb vorbei.“ (bisher Stufe 10) schnitt „Alles klar? Ganz ruhig …“ (Stufe 6) ab.
            Assert.AreEqual(6, FlagWatch.Priority(FlagWatch.Phrases[1]));
            Assert.AreEqual(6, FlagWatch.Priority(FlagWatch.Phrases[7]));
            Assert.AreEqual(10, FlagWatch.Priority(FlagWatch.Phrases[0]));
            Assert.AreEqual(10, FlagWatch.Priority(FlagWatch.Phrases[5]));
            Assert.IsFalse(CrewSpeechPolicy.CanInterruptForHazard(true, false, false, 6, FlagWatch.Priority(FlagWatch.Phrases[1])));
            Assert.IsTrue(CrewSpeechPolicy.CanInterruptForHazard(true, false, false, 6, FlagWatch.Priority(FlagWatch.Phrases[0])));
        }

        [Test]
        public void Pit_exit_hint_waits_after_the_pit_limit_reminder()
        {
            // RaceRoom Mantorp 15:27:16 Boxenlimit beim Losfahren, 15:27:19 schon der Ausfahrt-Hinweis hinterher.
            var s = new RadioSituation { LastPitLimitUtc = T, Now = T.AddSeconds(3) };
            Assert.AreEqual(RadioVerdict.Hold, RadioDirectorPolicy.Decide("pit-exit", s, out var why));
            Assert.AreEqual("pit_exit_waits_after_pit_limit", why);
            s.Now = T.AddSeconds(8);
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("pit-exit", s, out _));
            s.Now = T.AddSeconds(3);
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("pit-limit", s, out _), "Das Limit selbst wartet nie.");
            Assert.AreEqual(RadioVerdict.Send, RadioDirectorPolicy.Decide("pit-exit", new RadioSituation { Now = T }, out _));
        }
    }
}
