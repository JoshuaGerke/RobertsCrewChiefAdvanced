using System;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using CrewChiefV4.GameState;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class OpponentNameMatchingTests
    {
        private static OpponentData Car(string name, string number) => new OpponentData {
            DriverRawName = name, CarNumber = number, OverallPosition = 25, IsActive = true };

        [TestCase("Konrad Testanov")]
        [TestCase("Testanov")]
        [TestCase("K. Testanov")]
        [TestCase("KTESTANOV")]
        public void Exact_initial_and_surname_aliases_use_actual_field_identity(string query)
        {
            var car = Car("Konrad Testanov", "12");
            CollectionAssert.AreEqual(new[] { car }, OpponentNameMatching.Find(query, new[] { car }));
            CollectionAssert.AreEqual(new[] { car }, OpponentNameMatching.InQuestion("Welche Rundenzeit fährt " + query + "?", new[] { car }));
        }

        [Test]
        public void Same_initial_and_surname_remain_ambiguous()
        {
            var cars = new[] { Car("Konrad Testanov", "12"), Car("Kai Testanov", "14") };
            Assert.AreEqual(2, OpponentNameMatching.Find("K. Testanov", cars).Count);
            Assert.AreEqual(2, OpponentNameMatching.InQuestion("Was fährt K. Testanov?", cars).Count);
            Assert.AreEqual(0, OpponentNameMatching.Find("K. A.", cars).Count);
        }

        [TestCase("K. Testarnoff")]
        [TestCase("Testarnolf")]
        public void Speech_recognition_similarity_only_offers_a_candidate(string query)
        {
            var car = Car("Konrad Testanov", "12");
            Assert.IsEmpty(OpponentNameMatching.Find(query, new[] { car }));
            CollectionAssert.Contains(OpponentNameMatching.Suggest(query, new[] { car }), car);
        }

        [Test]
        public void Opponent_tool_never_confirms_similar_name_or_leaks_names_without_permission()
        {
            DateTime now = DateTime.UtcNow;
            var gs = new GameStateData(now.Ticks);
            gs.OpponentData["driver"] = Car("Konrad Testanov", "12");
            var ctx = new RaceDataContext { GameState = gs, NowUtc = now, LastTelemetryUtc = now, SendDriverNames = true };
            var found = RaceFieldTools.Read("get_opponent", ctx, "{\"name\":\"K. Testarnoff\"}");
            Assert.IsFalse((bool)found["eindeutig"]);
            Assert.AreEqual(0, (int)found["trefferzahl"]);
            Assert.AreEqual(1, ((JArray)found["moegliche_zuordnungen"]).Count);
            Assert.IsFalse((bool)found["zuordnung_bestaetigt"]);
            ctx.SendDriverNames = false;
            var blocked = RaceFieldTools.Read("get_opponent", ctx, "{\"name\":\"K. Testanov\"}");
            StringAssert.DoesNotContain("Konrad Testanov", blocked.ToString());
        }
    }
}
