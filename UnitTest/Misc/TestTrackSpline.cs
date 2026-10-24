using CrewChiefV4;
using CrewChiefV4.ACE.aceData;
using CrewChiefV4.GameState;
using CrewChiefV4.TrackSpline;

using NUnit.Framework;

namespace UnitTest.Misc
{
    [TestFixture]
    public class UnitTestTrackSpline
    {
        [Test]
        public void TestReadExistingFile()
        {
            GameStateData currentGameState = new GameStateData(10);
            currentGameState.SessionData = new SessionData();
            currentGameState.SessionData.TrackDefinition = TrackData.getTrackDefinition("Brands Hatch - GP", 1234);
            CrewChief.gameDefinition.gameEnum = GameEnum.ASSETTO_EVO;
            TrackSplineManager.Start(currentGameState);
            aceVec3 pos = new aceVec3(0, 0, 0);
            var splineLength = TrackSplineManager.Project(pos, 20);
        }
    }
}
