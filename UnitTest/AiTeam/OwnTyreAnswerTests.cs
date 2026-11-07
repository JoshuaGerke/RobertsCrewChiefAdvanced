using System;
using CrewChiefV4.AiTeam;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class OwnTyreAnswerTests
    {
        [TestCase("Was machen meine Reifen?")]
        [TestCase("Sind meine Reifen bedenklich oder geht's?")]
        public void Actual_short_own_tyre_questions_use_local_facts(string question)
        {
            Assert.IsTrue(OwnTyreAnswer.Matches(question));
            DateTime now = DateTime.UtcNow;
            var gs = new GameStateData(now.Ticks) { SourceTelemetryUtc = now };
            gs.TyreData.TyreWearActive = true;
            gs.TyreData.FrontLeftPercentWear = 20;
            gs.TyreData.FrontRightPercentWear = 21;
            gs.TyreData.RearLeftPercentWear = 27;
            gs.TyreData.RearRightPercentWear = 29;
            string text = LocalAnswers.TryAnswer(question, gs, new AiRaceMemory());
            StringAssert.Contains("Hinten rechts", text);
            StringAssert.Contains("29 Prozent", text);
            StringAssert.DoesNotContain("unbedenklich", text);
            StringAssert.DoesNotContain("70 Prozent", text);
        }

        [TestCase("Was machen die Reifen von Testfahrer?")]
        [TestCase("Reifendruck vorne links?")]
        [TestCase("Reichen meine Reifen bis zum Ende?")]
        public void Rival_pressure_and_strategy_questions_are_not_hijacked(string question)
        { Assert.IsFalse(OwnTyreAnswer.Matches(question)); }

        [Test]
        public void Stale_and_unknown_values_never_become_an_all_clear()
        {
            DateTime now = DateTime.UtcNow;
            var gs = new GameStateData(now.Ticks) { SourceTelemetryUtc = now.AddSeconds(-3) };
            StringAssert.Contains("frische", OwnTyreAnswer.Answer(gs, now));
            gs.SourceTelemetryUtc = now;
            StringAssert.Contains("fehlen", OwnTyreAnswer.Answer(gs, now));
        }

        [Test]
        public void Worn_out_status_is_not_hidden_by_a_temperature_summary()
        {
            DateTime now = DateTime.UtcNow;
            var gs = new GameStateData(now.Ticks) { SourceTelemetryUtc = now };
            gs.TyreData.TyreWearActive = true;
            gs.TyreData.TyreConditionStatus.cornersForEachStatus[TyreCondition.WORN_OUT] = CornerData.Corners.REAR_RIGHT;
            StringAssert.Contains("abgefahren", OwnTyreAnswer.Answer(gs, now));
        }
    }
}
