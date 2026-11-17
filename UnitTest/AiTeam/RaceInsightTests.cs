using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>Haupt-Renningenieur liest das Feld (Rob 17.11.2026): Zweikampf vor dir, Schnellster der Klasse, freie Fahrt.</summary>
    [TestFixture, Category("AiTeam")]
    public class RaceInsightTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 17, 20, 0, 0, DateTimeKind.Utc);

        [SetUp]
        public void German() { RadioLanguage.Current = RadioLanguage.German; }

        private static GameStateData Race(int position, int lap, float myLap)
        {
            var gs = new GameStateData(T.Ticks);
            gs.SessionData.SessionId = 21; gs.SessionData.SessionType = SessionType.Race; gs.SessionData.SessionPhase = SessionPhase.Green;
            gs.SessionData.ClassPosition = position; gs.SessionData.OverallPosition = position; gs.SessionData.CompletedLaps = lap;
            gs.SessionData.LapTimePrevious = myLap; gs.SessionData.PreviousLapWasValid = true;
            gs.carClass = new CarData.CarClass();
            return gs;
        }

        private static OpponentData Rival(GameStateData gs, string name, int position, float lastLap)
        {
            var o = new OpponentData { DriverRawName = name, ClassPosition = position, OverallPosition = position, CarClass = gs.carClass, LastLapTime = lastLap, LastLapValid = true };
            gs.OpponentData[name] = o;
            return o;
        }

        [Test]
        public void Two_cars_fighting_ahead_are_called_as_an_opportunity()
        {
            var gs = Race(5, 6, 90.5f); gs.SessionData.TimeDeltaFront = 1.6f; gs.SessionData.TimeDeltaBehind = 3f;
            Rival(gs, "Keller", 4, 90.8f); Rival(gs, "Wurz", 3, 90.9f); Rival(gs, "Mayer", 6, 91f);
            var watch = new RaceInsightWatch(1) { Gap = (front, back) => .6f };
            watch.Observe(gs, null, "Rob", true, T, true);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-battle-ahead", call.Topic);
            StringAssert.Contains("Wurz", call.Text); StringAssert.Contains("Keller", call.Text);

            var far = Race(5, 6, 90.5f); far.SessionData.TimeDeltaFront = 4.5f;
            Rival(far, "Keller", 4, 90.8f); Rival(far, "Wurz", 3, 90.9f); Rival(far, "Mayer", 6, 91f);
            var quiet = new RaceInsightWatch(1) { Gap = (front, back) => .6f };
            quiet.Observe(far, new RaceSituation { FrontRate = 0 }, "Rob", true, T, true);
            Assert.IsNull(quiet.Take(far, T, false, true), "weit weg und nicht näherkommend: keine Chance zu melden");
        }

        [Test]
        public void Two_fastest_laps_in_class_in_a_row_are_praised()
        {
            var watch = new RaceInsightWatch(1) { Gap = (a, b) => -1 };
            var gs = Race(1, 2, 89f); Rival(gs, "A", 2, 90f); Rival(gs, "B", 3, 90.5f); Rival(gs, "C", 4, 91f);
            watch.Observe(gs, null, "Rob", true, T, true);
            Assert.IsNull(watch.Take(gs, T, false, true), "eine Runde ist noch kein Trend");
            gs = Race(1, 3, 89.1f); Rival(gs, "A", 2, 90f); Rival(gs, "B", 3, 90.5f); Rival(gs, "C", 4, 91f);
            watch.Observe(gs, null, "Rob", true, T.AddSeconds(90), true);
            var call = watch.Take(gs, T.AddSeconds(90), false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-fastest", call.Topic);
            StringAssert.Contains("2", call.Text);
        }

        [Test]
        public void Free_air_both_ways_suggests_saving_tyres_and_fuel()
        {
            var gs = Race(4, 6, 92f); gs.SessionData.TimeDeltaFront = 8f; gs.SessionData.TimeDeltaBehind = 7f;
            Rival(gs, "A", 3, 90f); Rival(gs, "B", 2, 90.5f); Rival(gs, "C", 5, 91f);
            var watch = new RaceInsightWatch(1) { Gap = (a, b) => 8f };
            watch.Observe(gs, new RaceSituation { Phase = RacePhase.Middle, RearRate = -.1f }, "Rob", true, T, true);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-free-air", call.Topic);
            StringAssert.Contains("8,0 Sekunden", call.Text);
        }

        [Test]
        public void Insight_texts_vary_and_exist_in_three_languages()
        {
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var all = Enumerable.Range(0, 4).SelectMany(v => new[] {
                    RaceInsightWatch.BattleAhead(v % 2 == 0 ? "Rob" : null, "Wurz", "Keller", .6f, 1.6f, v % 2 == 0 ? .3f : 0, v),
                    RaceInsightWatch.BattleAhead("Rob", null, null, .6f, 1.6f, 0, v),
                    RaceInsightWatch.Fastest(v % 2 == 0 ? "Rob" : null, 3, v),
                    RaceInsightWatch.FreeAirText(v % 2 == 0 ? "Rob" : null, v == 1 ? -1 : 8f, 7f, v) }).ToArray();
                Assert.GreaterOrEqual(all.Distinct().Count(), 14, lang);
                foreach (var t in all) { Assert.IsFalse(char.IsLower(t[0]), t); StringAssert.DoesNotContain("  ", t); }
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Race_mind_keeps_insights_out_of_battles_and_the_last_laps()
        {
            var call = new ObservedRaceCall { Topic = "insight-battle-ahead", Text = "x", Priority = 4, SessionId = 5, SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };
            Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(new RaceSituation { Utc = T, SessionId = 5, Phase = RacePhase.Middle, Attention = RaceAttention.Battle }, call, T, out _));
            Assert.AreEqual(MindVerdict.Drop, RaceMind.Judge(new RaceSituation { Utc = T, SessionId = 5, Phase = RacePhase.LastLap, Attention = RaceAttention.Calm }, call, T, out _));
        }
    }
}
