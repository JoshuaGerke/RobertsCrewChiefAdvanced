using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Sprit-Hochrechnung ab den ersten Metern (Robs Wunsch 16.11.2026). Nachgestellt nach seiner AMS2-Fahrt in Bathurst:
    /// Boxenausfahrt, Outlap als „ungültig“ gemeldet, Ziellinie ohne neuen Rundenzähler, Einfahrt und Nachtanken.
    /// </summary>
    [TestFixture, Category("AiTeam")]
    public class FuelProjectionTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 16, 15, 7, 20, DateTimeKind.Utc);
        private static readonly TrackDefinition Bathurst = new TrackDefinition("Bathurst:Bathurst_2020", 6205.47f);
        private const float LitresPerMetre = 2.9f / 6205.47f;

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private sealed class Drive
        {
            internal readonly AiRaceMemory Memory = new AiRaceMemory();
            internal GameStateData Last;
            internal DateTime Now = T;
            internal float Fuel = 30.9f, Distance = 330f;

            internal GameStateData Tick(float metres, bool inPit = false, bool lapValid = false, float refuel = 0)
            {
                Now = Now.AddMilliseconds(100);
                Distance += metres;
                bool crossed = Distance >= Bathurst.trackLength;
                if (crossed) Distance -= Bathurst.trackLength;
                if (!inPit) Fuel -= metres * LitresPerMetre;
                Fuel += refuel;
                var s = new GameStateData(Now.Ticks);
                s.Now = Now;
                s.SourceTelemetryUtc = Now;
                s.SessionData.SessionId = 3;
                s.SessionData.SessionType = SessionType.Practice;
                s.SessionData.SessionPhase = SessionPhase.Green;
                s.SessionData.TrackDefinition = Bathurst;
                s.SessionData.CompletedLaps = 6; // AMS2 zählte auf der Outlap nicht weiter
                s.SessionData.IsNewLap = crossed;
                s.SessionData.CurrentLapIsValid = lapValid;
                s.PitData.InPitlane = inPit;
                s.FuelData.FuelUseActive = true;
                s.FuelData.FuelLeft = Fuel;
                s.PositionAndMotionData.DistanceRoundTrack = Distance;
                s.PositionAndMotionData.CarSpeed = metres * 10;
                s.carName = "Porsche 992 GT3 R";
                Memory.Update(Last, s, Now);
                Last = s;
                return s;
            }

            internal float Estimate() { return Memory.ProvisionalFuelPerLap(Last); }
        }

        [Test]
        public void Outlap_counts_although_the_game_calls_it_invalid()
        {
            var d = new Drive();
            for (int i = 0; i < 60; i++) d.Tick(4.5f);      // 270 m: noch zu wenig
            Assert.AreEqual(-1, d.Estimate());
            for (int i = 0; i < 20; i++) d.Tick(4.5f);      // 360 m
            Assert.AreEqual(2.9, d.Estimate(), 0.05);
            StringAssert.Contains("Erste Schätzung", LocalAnswers.TryAnswer("wie viel Sprit haben wir noch?", d.Last, d.Memory));
        }

        [Test]
        public void Finish_line_does_not_restart_the_estimate()
        {
            var d = new Drive();
            for (int i = 0; i < 1400; i++) d.Tick(4.5f, lapValid: i > 1300); // über Start/Ziel hinweg
            float metres;
            Assert.AreEqual(2.9, d.Memory.ProvisionalFuelPerLap(d.Last, out metres), 0.02);
            Assert.Greater(metres, 6000);
        }

        [Test]
        public void Standing_still_or_rolling_back_keeps_the_estimate()
        {
            var d = new Drive();
            for (int i = 0; i < 100; i++) d.Tick(4.5f);
            for (int i = 0; i < 30; i++) d.Tick(0f);
            d.Tick(-0.5f);
            for (int i = 0; i < 5; i++) d.Tick(4.5f);
            Assert.AreEqual(2.9, d.Estimate(), 0.05);
        }

        [Test]
        public void Pit_lane_and_refuel_start_a_new_estimate()
        {
            var d = new Drive();
            for (int i = 0; i < 200; i++) d.Tick(4.5f);
            Assert.Greater(d.Estimate(), 0);
            d.Tick(2f, inPit: true);
            Assert.AreEqual(-1, d.Estimate());
            d.Tick(0f, inPit: true, refuel: 12.8f);
            for (int i = 0; i < 30; i++) d.Tick(4.5f);
            Assert.AreEqual(-1, d.Estimate(), "nach dem Tanken erst wieder ab rund 300 m");
            for (int i = 0; i < 50; i++) d.Tick(4.5f);
            Assert.AreEqual(2.9, d.Estimate(), 0.05);
        }

        [Test]
        public void Teleport_or_reset_starts_a_new_estimate()
        {
            var d = new Drive();
            for (int i = 0; i < 200; i++) d.Tick(4.5f);
            d.Tick(1500f);
            Assert.AreEqual(-1, d.Estimate());
        }

        [Test]
        public void Measured_full_laps_take_precedence_and_are_not_mixed_with_the_estimate()
        {
            var d = new Drive();
            for (int i = 0; i < 100; i++) d.Tick(4.5f);
            bool provisional; int laps;
            float perLap = FuelEstimate.Consumption(d.Memory, d.Last, out provisional, out laps);
            Assert.IsTrue(provisional);
            Assert.AreEqual(0, laps);
            Assert.AreEqual(2.9, perLap, 0.05);
        }
    }
}
