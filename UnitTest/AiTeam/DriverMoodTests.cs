using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>C7 Rennstimmung: nur aus Fakten der letzten Runden, steuert den Ton und die Fahrtechnik-Kritik.</summary>
    [TestFixture, Category("AiTeam")]
    public class DriverMoodTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 18, 8, 0, 0, DateTimeKind.Utc);

        private static List<LapRecord> Laps(params (int pos, float time, bool valid, float behind)[] laps) =>
            laps.Select((l, i) => new LapRecord { LapNumber = i + 1, Position = l.pos, LapTime = l.time, Valid = l.valid, GapBehind = l.behind }).ToList();

        [Test]
        public void Too_few_laps_stay_neutral()
        {
            Assert.AreEqual(DriverMoodState.Neutral, DriverMood.Assess(Laps((5, 90, true, 3), (5, 90.1f, true, 3)), false, out _));
            Assert.AreEqual(DriverMoodState.Neutral, DriverMood.Assess(null, false, out _));
        }

        [Test]
        public void Lost_places_or_invalid_laps_mean_frustration()
        {
            Assert.AreEqual(DriverMoodState.Frustration, DriverMood.Assess(Laps((4, 90, true, 3), (5, 91, true, 3), (6, 91, true, 3), (6, 90.5f, true, 3)), false, out string why));
            StringAssert.Contains("plaetze_verloren_2", why);
            Assert.AreEqual(DriverMoodState.Frustration, DriverMood.Assess(Laps((5, 90, false, 3), (5, 92, true, 3), (5, 91, false, 3)), false, out why));
            StringAssert.Contains("ungueltige_runden", why);
        }

        [Test]
        public void Close_car_behind_or_tight_incidents_mean_pressure()
        {
            Assert.AreEqual(DriverMoodState.Pressure, DriverMood.Assess(Laps((5, 90, true, 2), (5, 90.2f, true, .8f), (5, 90.1f, true, .6f)), false, out _));
            Assert.AreEqual(DriverMoodState.Pressure, DriverMood.Assess(Laps((5, 90, true, 3), (5, 92, true, 3), (5, 91, true, 3)), true, out _));
        }

        [Test]
        public void Three_consistent_valid_laps_mean_flow()
        {
            Assert.AreEqual(DriverMoodState.Flow, DriverMood.Assess(Laps((5, 90.1f, true, 3), (5, 90.3f, true, 4), (4, 90.0f, true, 2)), false, out _));
            Assert.AreEqual(DriverMoodState.Neutral, DriverMood.Assess(Laps((5, 90.1f, true, 3), (5, 91.3f, true, 4), (5, 90.0f, true, 2)), false, out _), "zu viel Streuung");
        }

        private static RaceSituation Situation(DriverMoodState mood) =>
            new RaceSituation { Utc = T, SessionId = 5, Phase = RacePhase.Middle, Attention = RaceAttention.Calm, Focus = RaceFocus.Rhythm, Mood = mood };

        private static MindVerdict Judge(DriverMoodState mood, string topic, int priority = 3) =>
            RaceMind.Judge(Situation(mood), new ObservedRaceCall { Topic = topic, Text = topic, Priority = priority, SessionId = 5, SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) }, T, out _);

        [Test]
        public void Criticism_stays_only_its_timing_follows_the_mood()
        {
            Assert.AreEqual(MindVerdict.Admit, Judge(DriverMoodState.Frustration, "insight-lockup"), "Rob 18.11.: Kritik bleibt drin");
            Assert.AreEqual(MindVerdict.Admit, Judge(DriverMoodState.Flow, "corner-coaching"), "auch im Fluss");
            Assert.AreEqual(MindVerdict.Hold, Judge(DriverMoodState.Pressure, "insight-wheelspin"), "unter Druck wartet Kritik, bis Luft ist");
            Assert.AreEqual(MindVerdict.Admit, Judge(DriverMoodState.Neutral, "insight-lockup"));
            Assert.AreEqual(MindVerdict.Admit, Judge(DriverMoodState.Pressure, "insight-flatspot"), "Bremsplatten ist Fahrzeugzustand, keine Kritik");
            Assert.AreEqual(MindVerdict.Admit, Judge(DriverMoodState.Frustration, "race-hazard-ahead", 7), "Sicherheit geht immer durch");
        }

        [Test]
        public void Frustrated_driver_gets_one_point_at_a_time_softly_introduced()
        {
            var mind = new RaceMind();
            try
            {
                RaceMind.Publish(Situation(DriverMoodState.Frustration));
                ObservedRaceCall Call(string topic) => new ObservedRaceCall { Topic = topic, Text = topic, Priority = 3, SessionId = 5, SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };
                Assert.AreEqual(MindVerdict.Admit, mind.Judge(Call("insight-lockup"), T, out _));
                Assert.AreEqual(MindVerdict.Drop, mind.Judge(Call("insight-wheelspin"), T, out string why), "nicht gleich der nächste Fehler");
                StringAssert.Contains("one_point", why);
                Assert.AreEqual(MindVerdict.Admit, mind.Judge(Call("judge-dropped"), T, out _), "Nachfragen kommt durch");
            }
            finally { RaceMind.Publish(null); }
            foreach (var lang in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = lang;
                var soft = Enumerable.Range(0, 20).Select(_ => DriverMood.Softener()).Distinct().ToList();
                Assert.GreaterOrEqual(soft.Count, 1);
            }
            RadioLanguage.Current = RadioLanguage.German;
        }

        [Test]
        public void Quick_position_loss_after_a_spin_is_still_asked_about_near_other_cars()
        {
            var battle = Situation(DriverMoodState.Neutral); battle.Attention = RaceAttention.Battle;
            ObservedRaceCall Call(string topic) => new ObservedRaceCall { Topic = topic, Text = topic, Priority = 5, SessionId = 5, SessionType = SessionType.Race, DetectedUtc = T, ExpiresUtc = T.AddSeconds(30) };
            Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(battle, Call("judge-dropped"), T, out _));
            Assert.AreEqual(MindVerdict.Admit, RaceMind.Judge(battle, Call("judge-spin"), T, out _));
            battle.Attention = RaceAttention.Caution;
            Assert.AreEqual(MindVerdict.Hold, RaceMind.Judge(battle, Call("judge-dropped"), T, out _), "unter Gelb wartet es");
        }

        [Test]
        public void Mood_and_tone_reach_the_language_model()
        {
            var o = Situation(DriverMoodState.Frustration).Describe();
            Assert.AreEqual("frustriert", (string)o["fahrerstimmung"]);
            StringAssert.Contains("aufbauend", (string)o["ton"]);
        }
    }
}
