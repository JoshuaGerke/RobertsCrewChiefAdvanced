using System;
using CrewChiefV4.Audio;
using CrewChiefV4.Events;
using FluentAssertions;
using NUnit.Framework;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TestIrLicense
    {
        [Test]
        [TestCase("a", "licence/a_licence")]
        [TestCase("wc", "licence/pro_licence")]
        [TestCase("x", null)]
        public void TestGetLicenseFolder(string licenseID, string folder)
        {
            var license = Ratings.GetLicenseFolder(licenseID);
            license.Should().Be(folder);
        }
        [Test]
        [TestCase("b", 25.11f, "licence/b_licence")]
        [TestCase("r", 51.22f, "licence/rookie_licence")]
        [TestCase("x", 7.99f, null)]
        public void TestLicenceLevelMessage(string licenseID, float licenseNum, string folder)
        {
            //CrewChief.gameDefinition = null;
            //GlobalResources.Dispose();
            //PlaybackModerator.Dispose();
            //MainWindow hwnd = new MainWindow();
            SoundCache.availableSounds.Add(folder);
            Tuple<String, float> licenseLevel = new Tuple<String, float>(licenseID, licenseNum);
            var msg = Ratings.LicenseLevelMessage(licenseLevel);
            if (folder != null)
            {
                msg.messageFolders[0].Should().Be(folder);
                msg.messageFolders[1].Should().Be($"numbers/{(int)licenseNum}");
            }
            else
            {
                //msg.Should().BeNull(); doesn't compile
                AssertionExtensions.Should(msg).BeNull();
            }
        }
    }
}
