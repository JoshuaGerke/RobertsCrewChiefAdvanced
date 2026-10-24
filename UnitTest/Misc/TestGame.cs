using NUnit.Framework;

using CrewChiefV4;
namespace UnitTest.Misc
{
    [TestFixture]
    public class TestGame
    {
        [Test]
        public void TestMethod1()
        {
            Game.game = GameEnum.AMS2;
            Assert.IsTrue(Game.AMS2);
            Assert.IsFalse(Game.RF2_64BIT);

            Game.game = GameEnum.RF2_64BIT;
            Assert.IsTrue(Game.RF2_64BIT);
            Assert.IsFalse(Game.AMS2);
        }

        [Test]
        public void TestMethodAB()
        {
            Game.game = GameEnum.AMS2;
            Assert.AreEqual(Game.AMS2, Game.game == GameEnum.AMS2);
            Assert.AreNotEqual(Game.AMS2, Game.game == GameEnum.RF2_64BIT);
        }

        [Test]
        public void ShowReplacements()
        {
            CrewChief.gameDefinition.gameEnum = GameEnum.IRACING;
            Game.game = GameEnum.IRACING;
            
            if (CrewChief.gameDefinition.gameEnum == GameEnum.PCARS2
                || (CrewChief.gameDefinition.gameEnum == GameEnum.RF2_64BIT || CrewChief.gameDefinition.gameEnum == GameEnum.IRACING)
                || CrewChief.gameDefinition.gameEnum == GameEnum.AMS2
                || CrewChief.gameDefinition.gameEnum == GameEnum.PCARS3)
            {}
            else
            {
                Assert.Fail();
            }

            // becomes (using a regex substitution)
            if (Game.PCARS2
                || (Game.RF2_64BIT || Game.IRACING)
                || Game.AMS2
                || Game.PCARS3)
            {}
            else
            {
                Assert.Fail();
            }

            // becomes (manually)
            if (Game.PCARS2 || Game.RF2_64BIT || Game.IRACING || Game.AMS2 || Game.PCARS3)
            {}

            // and using a regex
            if (CrewChief.gameDefinition.gameEnum != GameEnum.RF2_64BIT && CrewChief.gameDefinition.gameEnum != GameEnum.IRACING)
            {}

            // becomes
            if (!Game.RF2_64BIT && !Game.IRACING)
            {}

            // or
            if (!(Game.RF2_64BIT || Game.IRACING))
            {}

            // can even replace
            CrewChief.gameDefinition.gameEnum = GameEnum.ASSETTO_32BIT;
            Game.game = GameEnum.ASSETTO_32BIT;
            if (CrewChief.gameDefinition.gameEnum == GameEnum.ASSETTO_32BIT ||
                CrewChief.gameDefinition.gameEnum == GameEnum.ASSETTO_64BIT)
            {}
            else
            {
                Assert.Fail();
            }

            // with
            if (Game.ASSETTO1)
            {}
            else
            {
                Assert.Fail();
            }
            CrewChief.gameDefinition.gameEnum = GameEnum.ASSETTO_64BIT;
            Game.game = GameEnum.ASSETTO_64BIT;
            if (CrewChief.gameDefinition.gameEnum != GameEnum.ASSETTO_32BIT &&
                CrewChief.gameDefinition.gameEnum != GameEnum.ASSETTO_64BIT)
            {
                Assert.Fail();
            }

            // with
            if (!Game.ASSETTO1)
            {
                Assert.Fail();
            }

            // similarly, the inspiration for this change
            if (Game.RF2_LMU)
            {}

            switch (CrewChief.gameDefinition.gameEnum)
            {
                case GameEnum.PCARS_64BIT:
                case GameEnum.PCARS_32BIT:
                    break;
            }

            //becomes
            switch (Game.game)
            {
                case GameEnum.PCARS_64BIT:
                case GameEnum.PCARS_32BIT:
                    break;
            }
        }
    }
}
