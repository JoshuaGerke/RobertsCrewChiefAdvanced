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

        // ---------- Wetter nur bei Änderung ----------

        private static WeatherReading W(float air, float track, bool rain = false) => new WeatherReading { Air = air, Track = track, Rain = rain };

        [Test]
        public void Weather_is_repeated_only_after_a_noticeable_change_and_names_the_direction()
        {
            Assert.IsFalse(W(21.2f, 25.4f).ChangedFrom(W(21.2f, 25.4f)), "Bathurst: gleiche Werte, keine zweite Meldung");
            Assert.IsFalse(W(21.6f, 26.4f).ChangedFrom(W(21.2f, 25.4f)));
            Assert.IsTrue(W(21.2f, 27.0f).ChangedFrom(W(21.2f, 25.4f)));
            Assert.IsTrue(W(22.3f, 25.4f).ChangedFrom(W(21.2f, 25.4f)));
            Assert.IsTrue(W(21.2f, 25.4f, true).ChangedFrom(W(21.2f, 25.4f)));
            StringAssert.Contains("wärmer", W(21.2f, 27.4f).ChangeText(W(21.2f, 25.4f), 0));
            StringAssert.Contains("kühler", W(21.2f, 23.4f).ChangeText(W(21.2f, 25.4f), 0));
            StringAssert.Contains("Regen", W(21.2f, 25.4f, true).ChangeText(W(21.2f, 25.4f), 0));
            Assert.AreEqual(3, Enumerable.Range(0, 3).Select(v => W(21.2f, 27.4f).ChangeText(W(21.2f, 25.4f), v)).Distinct().Count());
        }

        [Test]
        public void Podium_within_reach_is_called_with_the_measured_gap()
        {
            var gs = Race(4, 8, 91f); gs.SessionData.TimeDeltaFront = 2.1f; gs.SessionData.TimeDeltaBehind = 4f;
            Rival(gs, "Keller", 3, 90.8f); Rival(gs, "Wurz", 2, 90.9f); Rival(gs, "Mayer", 5, 91f);
            var watch = new RaceInsightWatch(1) { Gap = (a, b) => 5f };
            watch.Observe(gs, new RaceSituation { Phase = RacePhase.Middle, FrontRate = .3f }, "Rob", true, T, true);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("insight-target", call.Topic);
            StringAssert.Contains("2,1 Sekunden", call.Text);
            StringAssert.Contains("Podium", call.Text);
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var texts = Enumerable.Range(0, 3).SelectMany(v => new[] { RaceInsightWatch.TargetText(v == 1 ? null : "Rob", 3, 2.1f, .3f, v), RaceInsightWatch.TargetText("Rob", 1, 1.2f, 0, v) }).ToArray();
                Assert.AreEqual(6, texts.Distinct().Count(), lang);
                foreach (var t in texts) Assert.IsFalse(char.IsLower(t.TrimStart('¡', '¿')[0]), t);
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Neighbour_pit_stop_comes_with_the_tactical_consequence_in_races_only()
        {
            StringAssert.Contains("freie Bahn", NativeFieldEvents.PitConsequence("car_in_front_is_pitting", SessionType.Race, 0));
            StringAssert.Contains("Undercut", NativeFieldEvents.PitConsequence("car_behind_is_pitting", SessionType.Race, 0));
            Assert.AreEqual("", NativeFieldEvents.PitConsequence("car_in_front_is_pitting", SessionType.Practice, 0));
            Assert.AreEqual("", NativeFieldEvents.PitConsequence("leader_is_pitting", SessionType.Race, 0));
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                Assert.AreEqual(3, Enumerable.Range(0, 3).Select(v => NativeFieldEvents.PitConsequence("car_behind_is_pitting", SessionType.Race, v)).Distinct().Count());
                Assert.AreEqual(3, Enumerable.Range(0, 3).Select(v => NativeFieldEvents.PitConsequence("car_in_front_is_pitting", SessionType.Race, v)).Distinct().Count());
            }
            RadioLanguage.Current = RadioLanguage.German;
        }
    }
}
