using System;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>LMU 21.11.: zehn Zwischenstände fällig, einer gesprochen. Eingestellte Berichte müssen kommen.</summary>
    [TestFixture, Category("AiTeam")]
    public class RoutineGuaranteeTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 21, 21, 30, 0, DateTimeKind.Utc);

        private static GameStateData Race(bool closingLaps)
        {
            var s = new GameStateData(T.Ticks);
            s.SessionData.SessionId = 7; s.SessionData.SessionType = SessionType.Race;
            s.SessionData.SessionPhase = SessionPhase.Green; s.SessionData.CompletedLaps = 4;
            s.SessionData.SessionLapsRemaining = closingLaps ? 2 : 10;
            s.SessionData.ClassPosition = 13; s.SessionData.OverallPosition = 13;
            s.FuelData.FuelUseActive = true; s.FuelData.FuelLeft = 40; s.PositionAndMotionData.CarSpeed = 40;
            s.EngineData.EngineWaterTemp = 90;
            return s;
        }

        private static RaceDataContext At(GameStateData s, double seconds) =>
            new RaceDataContext { GameState = s, NowUtc = T.AddSeconds(seconds), LastTelemetryUtc = T.AddSeconds(seconds), Memory = new AiRaceMemory() };

        private static AiRadioPreferences Only(string topic)
        {
            var p = new AiRadioPreferences();
            foreach (var o in AiRadioPreferences.Options) p.Set(o.Id, 0);
            p.Set(topic, 30); p.SetTolerance(topic, 0, 0);
            return p;
        }

        [Test]
        public void A_report_kept_back_by_close_racing_comes_once_it_is_thirty_seconds_late()
        {
            var s = Race(true); var p = Only("vehicle"); var q = new AiRadioSchedule(1);
            q.Observe(s, T, p);
            Assert.IsTrue(RaceRadioDirector.SuppressBackgroundStatistics(At(s, 30)));
            Assert.IsNull(q.Take(At(s, 30), p, false));
            Assert.IsNull(q.Take(At(s, 59), p, false));
            var call = q.Take(At(s, 61), p, false);
            Assert.IsNotNull(call);
            Assert.IsTrue(call.Guaranteed);
        }

        [Test]
        public void An_expired_report_keeps_its_delay_and_returns_as_a_promised_report()
        {
            var s = Race(false); var p = Only("vehicle"); var q = new AiRadioSchedule(1);
            q.Observe(s, T, p);
            var first = q.Take(At(s, 30), p, false);
            Assert.IsNotNull(first);
            Assert.IsFalse(first.Guaranteed);
            first.ReleaseForRetry(T.AddSeconds(31));
            // LMU 22.11.: Der Bericht wartet bis zu 20 s auf seine Lücke (bisher 6 s) – ein Fehlversuch wirft ihn nicht weg.
            var waiting = q.Take(At(s, 40), p, false);
            Assert.IsNotNull(waiting, "wartet weiter auf die Lücke");
            Assert.AreEqual(first.EventId, waiting.EventId, "derselbe Bericht, kein neuer");
            waiting.ReleaseForRetry(T.AddSeconds(41));
            Assert.IsNull(q.Take(At(s, 51), p, false), "nach 20 s verfallen");
            var again = q.Take(At(s, 71), p, false);
            Assert.IsNotNull(again);
            Assert.IsTrue(again.Guaranteed, "die Wartezeit seit der ersten Fälligkeit zählt weiter");
        }

        [Test]
        public void The_first_status_after_the_start_is_promised_at_once()
        {
            var s = Race(true); var p = Only("status"); var q = new AiRadioSchedule(1);
            q.Observe(s, T, p);
            q.StaggerAfterStart(T, p);
            double due = Math.Max(30, RaceRhythm.FirstRoutineAfterSettle.TotalSeconds);
            var first = q.Take(At(s, due), p, false);
            Assert.IsNotNull(first, "der erste Stand wartet nicht auf Zweikampf-Ruhe");
            Assert.IsTrue(first.Guaranteed);
            first.MarkPlaybackStarted(T.AddSeconds(due));
            first.MarkPlaybackFinished(T.AddSeconds(due + 3), false);
            Assert.IsNull(q.Take(At(s, due + 31), p, false), "der nächste Stand wartet wieder auf eine ruhige Lücke");
        }

        [Test]
        public void A_disabled_report_never_becomes_a_promise()
        {
            var s = Race(true); var p = Only("vehicle"); var q = new AiRadioSchedule(1);
            q.Observe(s, T, p);
            Assert.IsNull(q.Take(At(s, 30), p, false));
            p.Set("vehicle", 0);
            Assert.IsNull(q.Take(At(s, 90), p, false));
        }

        private static RaceSituation Situation(RaceAttention attention, RacePhase phase = RacePhase.Middle) =>
            new RaceSituation { Utc = T, SessionId = 7, Phase = phase, Attention = attention, Focus = RaceFocus.Defend };

        private static ObservedRaceCall Status(bool promised) => new ObservedRaceCall { Topic = "status", Text = "Platz 13.", Priority = 3,
            SessionId = 7, SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(10), Guaranteed = promised };

        [TestCase(true)] [TestCase(false)]
        public void Race_mind_lets_a_promised_report_through_battle_and_traffic(bool battle)
        {
            var attention = battle ? RaceAttention.Battle : RaceAttention.Traffic;
            Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(Situation(attention), Status(false), T, out _));
            Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(Situation(attention), Status(true), T, out _));
        }

        [Test]
        public void A_promised_report_still_waits_under_yellow_and_in_the_pits()
        {
            Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(Situation(RaceAttention.Caution), Status(true), T, out _));
            Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(Situation(RaceAttention.Pit), Status(true), T, out _));
            Assert.AreEqual(MindVerdict.Drop, RaceMind.Judge(Situation(RaceAttention.Open, RacePhase.LastLap), Status(true), T, out _));
        }
    }
}
