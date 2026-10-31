using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class RaceSpeechEvidenceTests
    {
        private static readonly DateTime T=new DateTime(2026,10,31,8,0,0,DateTimeKind.Utc);
        private static RaceDataContext Context()
        {
            var s=new GameStateData(T.Ticks);
            s.SessionData.SessionId=1;s.SessionData.SessionType=SessionType.Race;s.SessionData.SessionPhase=SessionPhase.Green;
            s.SessionData.ClassPosition=3;s.SessionData.CompletedLaps=4;
            s.SessionData.LapsDeltaFront=0;s.SessionData.TimeDeltaFront=3;
            s.carClass=new CarData.CarClass {carClassEnum=CarData.CarClassEnum.AUDI_TT_CUP};
            s.OpponentData["rival"]=new OpponentData {IsActive=true,ClassPosition=2,CarClass=s.carClass,DriverRawName="Alex"};
            return new RaceDataContext {GameState=s,NowUtc=T,LastTelemetryUtc=T};
        }
        [Test]
        public void Fresh_unchanged_facts_survive_generation()
        {var c=Context();var e=new RaceSpeechEvidence(c);c.NowUtc=c.LastTelemetryUtc=T.AddSeconds(3);Assert.IsTrue(e.IsCurrent(c));}
        [TestCase("position")] [TestCase("lap")] [TestCase("session")] [TestCase("phase")] [TestCase("pit")] [TestCase("opponent")] [TestCase("gap")]
        public void Relevant_changed_facts_reject_old_commentary(string change)
        {
            var c=Context();var e=new RaceSpeechEvidence(c);var s=c.GameState;
            switch(change) {
                case "position":s.SessionData.ClassPosition=4;break;
                case "lap":s.SessionData.CompletedLaps++;break;
                case "session":s.SessionData.SessionId++;break;
                case "phase":s.SessionData.SessionPhase=SessionPhase.Checkered;break;
                case "pit":s.PitData.InPitlane=true;break;
                case "opponent":s.OpponentData["rival"].DriverRawName="Other";break;
                case "gap":s.SessionData.TimeDeltaFront=1;break;
            }
            Assert.IsFalse(e.IsCurrent(c));
        }
        [Test]
        public void Small_gap_noise_does_not_silence_a_current_insight()
        {var c=Context();var e=new RaceSpeechEvidence(c);c.GameState.SessionData.TimeDeltaFront=3.1f;Assert.IsTrue(e.IsCurrent(c));}
        [TestCase(-1)] [TestCase(13)]
        public void Clock_rollback_or_slow_cloud_result_is_rejected(int age)
        {var c=Context();var e=new RaceSpeechEvidence(c);c.NowUtc=c.LastTelemetryUtc=T.AddSeconds(age);Assert.IsFalse(e.IsCurrent(c));}
        [Test]
        public void Stale_telemetry_and_missing_context_are_rejected()
        {var c=Context();var e=new RaceSpeechEvidence(c);c.NowUtc=T.AddSeconds(2);Assert.IsFalse(e.IsCurrent(c));Assert.IsFalse(e.IsCurrent(null));}
    }
}
