using System;
using System.IO;
using System.Net;
using CrewChiefV4;
using CrewChiefV4.Audio;
using FluentAssertions;
using NUnit.Framework;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TestSoundPackVersionsHelper
    {
        private string sampleXML = @"<?xml version=""1.0"" encoding=""utf-8""?>
<!-- this is the final, 'gitlab' auto update data - the app will check this one last. From this update file, the MSI
will be pulled from GitLab and the assets from our VPS, falling back to thecrewchief.org. -->
<item>
	<title>Crew Chief 4.18.4.0 updated 08 June 2024</title>
	<version>4.18.4.1</version>
	<url>https://gitlab.com/mr_belowski/CrewChiefV4/-/blob/main/CrewChiefV4_installer/Installs/CrewChiefV4-4.18.4.0.msi</url>
	<changelog>https://mr_belowski.gitlab.io/CrewChiefV4/change_log_for_auto_updated.html</changelog>


	<!-- if a sound pack download fails, we retry replacing this part of its URL: -->
	<retry_replace>http://167.235.144.28/</retry_replace>
	<!-- fall back to the website ftp: -->
	<retry_replace_with>https://thecrewchief.org/downloads/</retry_replace_with>
  <retry_replace_git>https://thecrewchief.org/downloads/</retry_replace_git>
  <retry_replace_with_git>https://gitlab.com/mr_belowski/CrewChiefV4/-/blob/main/CrewChiefV4_installer/Soundpacks/</retry_replace_with_git>



  <!-- current implementation where all the sound pack update information is in this document -->
	<sounds language=""en"">
		<voice-messages latest-version=""189"">
			<update-pack url=""http://167.235.144.28/base_sound_pack.zip"" updates-from-version=""-1""/>
			<update-pack url=""http://167.235.144.28/update_sound_pack.zip"" updates-from-version=""0""/>
			<update-pack url=""http://167.235.144.28/update_2_sound_pack.zip"" updates-from-version=""122""/>
			<update-pack url=""http://167.235.144.28/update_3_sound_pack.zip"" updates-from-version=""146""/>
			<update-pack url=""http://167.235.144.28/update_4_sound_pack.zip"" updates-from-version=""151""/>
			<update-pack url=""http://167.235.144.28/update_5_sound_pack.zip"" updates-from-version=""156""/>
			<update-pack url=""http://167.235.144.28/update_6_sound_pack.zip"" updates-from-version=""157""/>
			<update-pack url=""http://167.235.144.28/update_7_sound_pack.zip"" updates-from-version=""158""/>
			<update-pack url=""http://167.235.144.28/update_8_sound_pack.zip"" updates-from-version=""160""/>
			<update-pack url=""http://167.235.144.28/update_9_sound_pack.zip"" updates-from-version=""161""/>
			<update-pack url=""http://167.235.144.28/update_10_sound_pack.zip"" updates-from-version=""162""/>
			<update-pack url=""http://167.235.144.28/update_11_sound_pack.zip"" updates-from-version=""163""/>
			<update-pack url=""http://167.235.144.28/update_12_sound_pack.zip"" updates-from-version=""164""/>
			<update-pack url=""http://167.235.144.28/update_13_sound_pack.zip"" updates-from-version=""165""/>
			<update-pack url=""http://167.235.144.28/update_14_sound_pack.zip"" updates-from-version=""167""/>
			<update-pack url=""http://167.235.144.28/update_15_sound_pack.zip"" updates-from-version=""168""/>
			<update-pack url=""http://167.235.144.28/update_16_sound_pack.zip"" updates-from-version=""169""/>
			<update-pack url=""http://167.235.144.28/update_17_sound_pack.zip"" updates-from-version=""170""/>
			<update-pack url=""http://167.235.144.28/update_18_sound_pack.zip"" updates-from-version=""171""/>
			<update-pack url=""http://167.235.144.28/update_19_sound_pack.zip"" updates-from-version=""172""/>
			<update-pack url=""http://167.235.144.28/update_20_sound_pack.zip"" updates-from-version=""173""/>
			<update-pack url=""http://167.235.144.28/update_21_sound_pack.zip"" updates-from-version=""174""/>
			<update-pack url=""http://167.235.144.28/update_22_sound_pack.zip"" updates-from-version=""175""/>
			<update-pack url=""http://167.235.144.28/update_23_sound_pack.zip"" updates-from-version=""176""/>
			<update-pack url=""http://167.235.144.28/update_24_sound_pack.zip"" updates-from-version=""177""/>
			<update-pack url=""http://167.235.144.28/update_25_sound_pack.zip"" updates-from-version=""178""/>
			<update-pack url=""http://167.235.144.28/update_26_sound_pack.zip"" updates-from-version=""179""/>
			<update-pack url=""http://167.235.144.28/update_27_sound_pack.zip"" updates-from-version=""180""/>
			<update-pack url=""http://167.235.144.28/update_28_sound_pack.zip"" updates-from-version=""181""/>
			<update-pack url=""http://167.235.144.28/update_29_sound_pack.zip"" updates-from-version=""182""/>
			<update-pack url=""http://167.235.144.28/update_30_sound_pack.zip"" updates-from-version=""183""/>
			<update-pack url=""http://167.235.144.28/update_31_sound_pack.zip"" updates-from-version=""184""/>
			<update-pack url=""http://167.235.144.28/update_32_sound_pack.zip"" updates-from-version=""185""/>
			<update-pack url=""http://167.235.144.28/update_33_sound_pack.zip"" updates-from-version=""186""/>
			<update-pack url=""http://167.235.144.28/update_34_sound_pack.zip"" updates-from-version=""187""/>
			<update-pack url=""https://thecrewchief.org/downloads/update_35_sound_pack.zip"" updates-from-version=""188""/>
			<update-pack url=""https://thecrewchief.org/downloads/update_36_sound_pack.zip"" updates-from-version=""188""/>
			<update-pack url=""https://thecrewchief.org/downloads/update_37_sound_pack.zip"" updates-from-version=""188""/>
		</voice-messages>
		<driver-names latest-version=""142"">
			<update-pack url=""http://167.235.144.28/base_driver_names.zip"" updates-from-version=""-1""/>
			<update-pack url=""http://167.235.144.28/update_driver_names.zip"" updates-from-version=""0""/>
			<update-pack url=""http://167.235.144.28/update_2_driver_names.zip"" updates-from-version=""130""/>
			<update-pack url=""http://167.235.144.28/update_3_driver_names.zip"" updates-from-version=""131""/>
			<update-pack url=""http://167.235.144.28/update_4_driver_names.zip"" updates-from-version=""133""/>
			<update-pack url=""http://167.235.144.28/update_5_driver_names.zip"" updates-from-version=""134""/>
			<update-pack url=""http://167.235.144.28/update_6_driver_names.zip"" updates-from-version=""135""/>
			<update-pack url=""http://167.235.144.28/update_7_driver_names.zip"" updates-from-version=""136""/>
			<update-pack url=""http://167.235.144.28/update_8_driver_names.zip"" updates-from-version=""137""/>
			<update-pack url=""http://167.235.144.28/update_9_driver_names.zip"" updates-from-version=""138""/>
			<update-pack url=""http://167.235.144.28/update_10_driver_names.zip"" updates-from-version=""139""/>
			<update-pack url=""http://167.235.144.28/update_11_driver_names.zip"" updates-from-version=""140""/>
			<update-pack url=""https://thecrewchief.org/downloads/update_12_driver_names.zip"" updates-from-version=""141""/>
		</driver-names>
		<personalisations latest-version=""148"">
			<update-pack url=""http://167.235.144.28/personalisations.zip"" updates-from-version=""-1""/>
			<update-pack url=""http://167.235.144.28/update_personalisations.zip"" updates-from-version=""0""/>
			<update-pack url=""http://167.235.144.28/update_2_personalisations.zip"" updates-from-version=""121""/>
			<update-pack url=""http://167.235.144.28/update_3_personalisations.zip"" updates-from-version=""129""/>
			<update-pack url=""http://167.235.144.28/update_4_personalisations.zip"" updates-from-version=""130""/>
			<update-pack url=""http://167.235.144.28/update_5_personalisations.zip"" updates-from-version=""132""/>
			<update-pack url=""http://167.235.144.28/update_6_personalisations.zip"" updates-from-version=""133""/>
			<update-pack url=""http://167.235.144.28/update_7_personalisations.zip"" updates-from-version=""134""/>
			<update-pack url=""http://167.235.144.28/update_8_personalisations.zip"" updates-from-version=""136""/>
			<update-pack url=""http://167.235.144.28/update_9_personalisations.zip"" updates-from-version=""137""/>
			<update-pack url=""http://167.235.144.28/update_10_personalisations.zip"" updates-from-version=""138""/>
			<update-pack url=""http://167.235.144.28/update_11_personalisations.zip"" updates-from-version=""139""/>
			<update-pack url=""http://167.235.144.28/update_12_personalisations.zip"" updates-from-version=""140""/>
			<update-pack url=""http://167.235.144.28/update_13_personalisations.zip"" updates-from-version=""141""/>
			<update-pack url=""http://167.235.144.28/update_14_personalisations.zip"" updates-from-version=""142""/>
			<update-pack url=""http://167.235.144.28/update_15_personalisations.zip"" updates-from-version=""143""/>
			<update-pack url=""http://167.235.144.28/update_16_personalisations.zip"" updates-from-version=""144""/>
			<update-pack url=""http://167.235.144.28/update_17_personalisations.zip"" updates-from-version=""145""/>
			<update-pack url=""http://167.235.144.28/update_18_personalisations.zip"" updates-from-version=""146""/>
			<update-pack url=""https://thecrewchief.org/downloads/update_19_personalisations.zip"" updates-from-version=""147""/>
		</personalisations>
	</sounds>


	<!--legacy sound pack stuff for 4.1.2.2 and earlier. I've given up updating this, it will be forever at 146 now.-->
	<soundpackversion>146</soundpackversion>
	<basesoundpackurl>http://167.235.144.28/base_sound_pack.zip</basesoundpackurl>
	<!-- sound pack update from base to version 121 will use this update pack -->
	<updatesoundpackurl>http://167.235.144.28/update_sound_pack.zip</updatesoundpackurl>
	<!-- sound pack update from version 121 upwards will use this update pack -->
	<update2soundpackurl>http://167.235.144.28/update_2_sound_pack.zip</update2soundpackurl>
	<drivernamesversion>100</drivernamesversion>
	<basedrivernamesurl>http://167.235.144.28/base_driver_names.zip</basedrivernamesurl>
	<updatedrivernamesurl>http://167.235.144.28/update_driver_names.zip</updatedrivernamesurl>
	<personalisationsversion>129</personalisationsversion>
	<basepersonalisationsurl>http://167.235.144.28/personalisations.zip</basepersonalisationsurl>
	<updatepersonalisationsurl>http://167.235.144.28/update_personalisations.zip</updatepersonalisationsurl>
	<update2personalisationsurl>http://167.235.144.28/update_2_personalisations.zip</update2personalisationsurl>



	<!-- legacy sound pack stuff for 4.9.3.6 and earlier. Again, this will not be updated and will (eventually) be removed -->
	<soundpack language=""en"">
		<soundpackversion>150</soundpackversion>
		<basesoundpackurl>http://167.235.144.28/base_sound_pack.zip</basesoundpackurl>
		<!-- sound pack update from base to version 121 will use this update pack -->
		<updatesoundpackurl>http://167.235.144.28/update_sound_pack.zip</updatesoundpackurl>
		<!-- sound pack update from version 121 upwards will use this update pack -->
		<update2soundpackurl>http://167.235.144.28/update_2_sound_pack.zip</update2soundpackurl>
		<update3soundpackurl>http://167.235.144.28/update_3_sound_pack.zip</update3soundpackurl>

		<drivernamesversion>131</drivernamesversion>
		<basedrivernamesurl>http://167.235.144.28/base_driver_names.zip</basedrivernamesurl>
		<updatedrivernamesurl>http://167.235.144.28/update_driver_names.zip</updatedrivernamesurl>
		<update2drivernamesurl>https://thecrewchief.org/downloads/update_2_driver_names.zip</update2drivernamesurl>

		<personalisationsversion>130</personalisationsversion>
		<basepersonalisationsurl>http://167.235.144.28/personalisations.zip</basepersonalisationsurl>
		<updatepersonalisationsurl>http://167.235.144.28/update_personalisations.zip</updatepersonalisationsurl>
		<update2personalisationsurl>http://167.235.144.28/update_2_personalisations.zip</update2personalisationsurl>
		<update3personalisationsurl>http://167.235.144.28/update_3_personalisations.zip</update3personalisationsurl>
	</soundpack>
</item>
";
        [Test]
        public void Test_XMLparses()
        {
            bool gotSoundpackUpdateData = SoundPackVersionsHelper.parseUpdateData(sampleXML, "en");
            gotSoundpackUpdateData.Should().BeTrue();
            SoundPackVersionsHelper.personalisationUpdatePacks.Count.Should().BeGreaterThan(0);
        }
        [Test]
        public void Test_XMLfilesParse()
        {
            foreach (string folder in new String[]{"primary", "secondary", "gitlab"})
            {
                string filePath = Path.Combine(TestDataFiles.getDefaultFileLocation(null), "../auto_update_data_files", folder, "auto_update_data.xml");
                if (File.Exists(filePath))
                {
                    string xml = File.ReadAllText(filePath);
                    Log.Commentary($"Checking {filePath}");
                    bool gotSoundpackUpdateData = SoundPackVersionsHelper.parseUpdateData(xml, "en");
                    gotSoundpackUpdateData.Should().BeTrue();
                    SoundPackVersionsHelper.personalisationUpdatePacks.Count.Should().BeGreaterThan(0);
                }
            }
        }
        
        [Test]
        public void Test_XMLcontents()
        {
            bool gotSoundpackUpdateData = SoundPackVersionsHelper.parseUpdateData(sampleXML, "en");
            gotSoundpackUpdateData.Should().BeTrue();
            SoundPackVersionsHelper.personalisationUpdatePacks.Count.Should().BeGreaterThan(0);

            SoundPackVersionsHelper.personalisationUpdatePacks[19].url.Should().Contain("https://thecrewchief.org/downloads/update_19_personalisations.zip");
            SoundPackVersionsHelper.retryReplaceGit.Should().Contain("https://thecrewchief.org/downloads/");
        }
        [Test]
        public void Test_UrlRetry()
        {
            bool gotSoundpackUpdateData = SoundPackVersionsHelper.parseUpdateData(sampleXML, "en");
            gotSoundpackUpdateData.Should().BeTrue();
            SoundPackVersionsHelper.personalisationUpdatePacks.Count.Should().BeGreaterThan(0);

            SoundPackVersionsHelper.personalisationUpdatePacks[19].url.Should().Contain("https://thecrewchief.org/downloads/update_19_personalisations.zip");
            string urlRetry = SoundPackVersionsHelper.replaceUrlToRetry(SoundPackVersionsHelper.personalisationUpdatePacks[19].url);

            urlRetry.Should().Contain("https://gitlab.com/mr_belowski/CrewChiefV4/-/blob/main/CrewChiefV4_installer/Soundpacks/");
        }
        [Test]
        public void Test_UrlRetryDownload()
        {
            bool gotSoundpackUpdateData = SoundPackVersionsHelper.parseUpdateData(sampleXML, "en");
            gotSoundpackUpdateData.Should().BeTrue();
            SoundPackVersionsHelper.personalisationUpdatePacks.Count.Should().BeGreaterThan(0);

            SoundPackVersionsHelper.personalisationUpdatePacks[19].url.Should().Contain("https://thecrewchief.org/downloads/update_19_personalisations.zip");
            string urlRetry = SoundPackVersionsHelper.replaceUrlToRetry(SoundPackVersionsHelper.personalisationUpdatePacks[19].url);

            urlRetry.Should().Contain("https://gitlab.com/mr_belowski/CrewChiefV4/-/blob/main/CrewChiefV4_installer/Soundpacks/");

            string filePath = Path.Combine(Path.GetTempPath(), "update_19_personalisations.zip");
            if (File.Exists(filePath))
                File.Delete(filePath);
            using (WebClient client = new WebClient())
            {
                client.DownloadFile(urlRetry, filePath);
            }
            File.Exists(filePath).Should().BeTrue();
            File.Delete(filePath);
        }
    }
}
