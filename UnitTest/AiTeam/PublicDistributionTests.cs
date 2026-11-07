using System;
using System.IO;
using CrewChiefV4;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture,Category("AiTeam")]
    public class PublicDistributionTests
    {
        [Test] public void Public_paths_are_separate_from_original_Crew_Chief_paths()
        {
            string docs=Path.GetFullPath("test-documents"),local=Path.GetFullPath("test-local");
            Assert.AreEqual(Path.Combine(docs,"RRCA","CrewChiefV4"),DataFiles.StandaloneDocumentsPath(docs));
            Assert.AreNotEqual(Path.Combine(docs,"CrewChiefV4"),DataFiles.StandaloneDocumentsPath(docs));
            Assert.AreNotEqual(Path.Combine(local,"CrewChiefV4"),DataFiles.StandaloneLocalPath(local));
        }
        [Test] public void Fresh_driver_and_voice_defaults_are_neutral_and_do_not_enable_opponent_name_transfer()
        {
            var s=new AiTeamSettings();Assert.AreEqual("Fahrer",s.DriverName);Assert.IsFalse(s.SendDriverNames);
            Assert.IsEmpty(s.ElevenLabsVoiceBen);Assert.IsEmpty(s.ElevenLabsVoiceMik);Assert.IsEmpty(s.ElevenLabsVoiceSara);
        }
        [Test] public void First_pass_catalogue_variant_can_change_without_changing_the_fact()
        {
            string a=RaceRadioLanguage.Overtake(1,7,"Alex",false,0),b=RaceRadioLanguage.Overtake(1,7,"Alex",false,1);
            Assert.AreNotEqual(a,b);StringAssert.Contains("7",a);StringAssert.Contains("7",b);
        }
    }
}
