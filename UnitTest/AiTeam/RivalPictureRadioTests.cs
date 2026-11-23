using System;
using System.Linq;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.AiTeam.Field;
using CrewChiefV4.GameState;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    /// <summary>
    /// Rob 23.11.2026: „Vordermann und Hintermann sehr oft, vereinzelt die Spitze (Topspeed, Bestzeiten im Vergleich zu meiner),
    /// am Anfang der Vergleich aus der Datenbank. Immer 4–6 Phrasen.“
    /// </summary>
    [TestFixture, Category("AiTeam"), NonParallelizable]
    public class RivalPictureRadioTests
    {
        private static readonly DateTime T = new DateTime(2026, 11, 23, 21, 0, 0, DateTimeKind.Utc);
        private bool wasDeterministic;

        [SetUp]
        public void SetUp()
        {
            RadioLanguage.Current = RadioLanguage.German;
            wasDeterministic = RadioVariants.Deterministic;
            PhraseMemory.Clock = () => T;
            PhraseMemory.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            RadioVariants.Deterministic = wasDeterministic;
            RadioLanguage.Current = RadioLanguage.German;
            PhraseMemory.Clock = () => DateTime.UtcNow;
            PhraseMemory.Clear();
        }

        private static GameStateData Race(out OpponentData front, out OpponentData rear)
        {
            var gs = new GameStateData(T.Ticks);
            var s = gs.SessionData;
            s.SessionId = 9; s.SessionType = SessionType.Race; s.SessionPhase = SessionPhase.Green;
            s.ClassPosition = 5; s.OverallPosition = 5; s.CompletedLaps = 6;
            s.TrackDefinition = new TrackDefinition("Monza", 5793f);
            gs.carClass = new CarData.CarClass { carClassEnum = CarData.CarClassEnum.GT3 };
            front = new OpponentData { DriverRawName = "Krobber", CarNumber = "12", IsActive = true, ClassPosition = 4, OverallPosition = 4, CarClass = gs.carClass };
            rear = new OpponentData { DriverRawName = "Valgo", CarNumber = "31", IsActive = true, ClassPosition = 6, OverallPosition = 6, CarClass = gs.carClass };
            gs.OpponentData["krobber"] = front; gs.OpponentData["valgo"] = rear;
            s.OpponentKeyInFront = "krobber"; s.OpponentKeyBehind = "valgo";
            gs.Now = T; gs.SourceTelemetryUtc = T;
            return gs;
        }

        private static FieldView View(float mineKmh, float frontKmh, float rearKmh, float leaderKmh = float.NaN)
        {
            var me = new FieldCarView { Name = "Rob", IsPlayer = true, ClassId = 1, PlaceClass = 5, TopSpeed = mineKmh / 3.6f };
            var cars = new[]
            {
                me,
                new FieldCarView { Name = "Krobber", ClassId = 1, PlaceClass = 4, TopSpeed = frontKmh / 3.6f, GapS = 0.9f },
                new FieldCarView { Name = "Valgo", ClassId = 1, PlaceClass = 6, TopSpeed = rearKmh / 3.6f, GapS = -1.2f },
                new FieldCarView { Name = "Lead", ClassId = 1, PlaceClass = 1, TopSpeed = leaderKmh / 3.6f, GapS = 30f },
            };
            return new FieldView { Player = me, Cars = cars };
        }

        [Test]
        public void Every_top_speed_call_has_five_wordings_in_every_language()
        {
            RadioVariants.Deterministic = false;
            foreach (var language in new[] { RadioLanguage.German, RadioLanguage.English, RadioLanguage.Spanish })
            {
                RadioLanguage.Current = language;
                foreach (bool ahead in new[] { true, false })
                    foreach (bool faster in new[] { true, false })
                    {
                        var seen = Enumerable.Range(0, 80).Select(_ => TopSpeedWatch.RivalText(ahead, faster, "Krobber", "7")).Distinct().ToList();
                        Assert.That(seen.Count, Is.InRange(4, 6), language + " ahead=" + ahead + " faster=" + faster);
                        Assert.IsTrue(seen.All(t => t.Contains("Krobber") && t.Contains("7 km/h")), language + " ahead=" + ahead);
                    }
                foreach (bool own in new[] { true, false })
                {
                    var seen = Enumerable.Range(0, 80).Select(_ => TopSpeedWatch.FieldText(own, "Lead", "291", "285", "6")).Distinct().Count();
                    Assert.That(seen, Is.InRange(4, 6), language + " field own=" + own);
                }
            }
        }

        [Test]
        public void A_clearly_faster_car_ahead_is_called_with_name_and_difference()
        {
            var gs = Race(out _, out _);
            var watch = new TopSpeedWatch();
            watch.Evaluate(gs, View(281, 289, 283), true, T);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("rival-topspeed", call.Topic);
            Assert.AreEqual(TeamMember.Ben, call.Member);
            StringAssert.Contains("Krobber", call.Text);
            StringAssert.Contains("8 km/h", call.Text);
            StringAssert.Contains("schneller", call.Text);
        }

        [Test]
        public void The_bigger_difference_wins_and_small_ones_stay_quiet()
        {
            var gs = Race(out _, out _);
            var watch = new TopSpeedWatch();
            watch.Evaluate(gs, View(281, 283, 281), true, T);
            Assert.IsNull(watch.Take(gs, T, false, true), "2 km/h sind kein Thema, weder beim Gegner noch an der Klassenspitze");

            gs.SessionData.CompletedLaps = 7;
            watch = new TopSpeedWatch();
            watch.Evaluate(gs, View(281, 287, 271), true, T);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            StringAssert.Contains("Valgo", call.Text, "10 km/h hinten wiegen mehr als 6 vorn");
            StringAssert.Contains("10 km/h", call.Text);
        }

        [Test]
        public void No_call_without_a_speakable_name_early_in_the_race_or_too_often()
        {
            var gs = Race(out var front, out _);
            var watch = new TopSpeedWatch();
            watch.Evaluate(gs, View(281, 290, 281), false, T);
            Assert.IsNull(watch.Take(gs, T, false, true), "ohne Namen kein Satz");

            gs.SessionData.CompletedLaps = 2;
            watch.Evaluate(gs, View(281, 290, 281), true, T);
            Assert.IsNull(watch.Take(gs, T, false, true), "am Start zählt der Windschatten im Pulk");

            gs.SessionData.CompletedLaps = 6;
            watch.Evaluate(gs, View(281, 290, 281), true, T);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            call.MarkPlaybackStarted(T);
            gs.SessionData.CompletedLaps = 8;
            watch.Evaluate(gs, View(281, 290, 281), true, T.AddMinutes(3));
            var again = watch.Take(gs, T.AddMinutes(3), false, true);
            Assert.IsTrue(again == null || again.Topic != "rival-topspeed", "derselbe Vergleich erst nach " + TopSpeedWatch.RivalEveryLaps + " Runden");
        }

        [Test]
        public void Now_and_then_the_class_top_speed_is_named()
        {
            var gs = Race(out _, out _);
            gs.OpponentData["lead"] = new OpponentData { DriverRawName = "Lead", IsActive = true, ClassPosition = 1, CarClass = gs.carClass };
            var watch = new TopSpeedWatch();
            watch.Evaluate(gs, View(281, 283, 280, 292), true, T);
            var call = watch.Take(gs, T, false, true);
            Assert.IsNotNull(call);
            Assert.AreEqual("field-topspeed", call.Topic);
            StringAssert.Contains("Lead", call.Text);
            StringAssert.Contains("292", call.Text);

            watch = new TopSpeedWatch();
            watch.Evaluate(gs, View(295, 293, 292, 292), true, T);
            var own = watch.Take(gs, T, false, true);
            Assert.IsNotNull(own);
            StringAssert.Contains("295", own.Text);
            StringAssert.DoesNotContain("Lead", own.Text);
        }

        [Test]
        public void Field_best_lap_is_compared_with_the_own_best_in_five_ways()
        {
            RadioVariants.Deterministic = false;
            var seen = Enumerable.Range(0, 80).Select(_ => SessionLapObserver.GapAndPush(100f, 101.3f, 0))
                .Select(t => t.Substring(0, t.IndexOf("1,3", StringComparison.Ordinal) + 3)).Distinct().Count();
            Assert.That(seen, Is.InRange(4, 6));
        }

        [Test]
        public void Session_start_reference_from_the_database_has_six_openings()
        {
            RadioVariants.Deterministic = false;
            var pb = new PersonalBest { LapSeconds = 77.271f, DateUtc = DateTime.UtcNow.AddDays(-30), FuelAtStart = -1, TrackC = float.NaN, AirC = float.NaN };
            var openings = Enumerable.Range(0, 120).Select(_ => PersonalBestWatch.Briefing(pb, -1, float.NaN, "Rob", 0))
                .Select(t => t.Substring(0, t.IndexOf("1:17,271", StringComparison.Ordinal))).Distinct().Count();
            Assert.That(openings, Is.InRange(4, 6));
        }
    }
}
