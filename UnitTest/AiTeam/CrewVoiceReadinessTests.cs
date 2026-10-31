using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using CrewChiefV4.AiTeam;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam")]
    public class CrewVoiceReadinessTests
    {
        private static AiTeamSettings Settings()
        { return new AiTeamSettings { Enabled=true, BenEnabled=true, MikEnabled=true, SaraEnabled=true, ReplaceCrewChiefVoice=true, TtsProvider=AiTtsProvider.ELEVENLABS }; }

        private static CrewVoiceReadiness.Result Check(AiTeamSettings s, bool pack=true, string selected="Sara", string folder="spotter_Sara/car_left", string key=null, bool spotting=true)
        { return CrewVoiceReadiness.Evaluate(s,pack,selected,folder,key ?? CrewVoiceReadiness.ExpectedSaraVoiceKey(s),spotting); }

        [Test]
        public void Prepared_loaded_original_sara_is_ready_without_any_api_key()
        { var s=Settings();Assert.IsTrue(Check(s).Ready);Assert.AreEqual("ready",Check(s).Reason); }

        [TestCase(false,"Sara","spotter_Sara/car_left","pack_missing_or_mismatched")]
        [TestCase(true,"Jim (default)","spotter/car_left","spotter_not_selected")]
        [TestCase(true,"Sara","spotter/car_left","restart_required")]
        public void Missing_or_different_local_spotter_cannot_claim_selected_voice_is_ready(bool pack,string selected,string folder,string reason)
        { var result=Check(Settings(),pack,selected,folder);Assert.IsFalse(result.Ready);Assert.AreEqual(reason,result.Reason); }

        [Test]
        public void Replaced_pack_on_disk_does_not_claim_old_loaded_clips_have_changed()
        { var s=Settings();string loaded=CrewVoiceReadiness.ExpectedSaraVoiceKey(s);s.ElevenLabsVoiceSara="another-selected-voice";var result=Check(s,key:loaded);Assert.IsFalse(result.Ready);Assert.AreEqual("restart_required",result.Reason); }

        [Test]
        public void Unrecorded_loaded_identity_requires_restart()
        { var s=Settings();Assert.AreEqual("restart_required",CrewVoiceReadiness.Evaluate(s,true,"Sara","spotter_Sara/car_left",null,true).Reason); }

        [Test]
        public void Windows_system_voice_is_not_accepted_as_the_requested_natural_crew()
        { var s=Settings();s.TtsProvider=AiTtsProvider.WINDOWS;Assert.AreEqual("windows_voice_selected",Check(s).Reason); }

        [Test]
        public void Enabled_legacy_chief_cannot_claim_an_exclusive_three_voice_configuration()
        { var s=Settings();s.ReplaceCrewChiefVoice=false;Assert.AreEqual("legacy_chief_enabled",Check(s).Reason); }

        [Test]
        public void Disabled_sara_does_not_require_a_pack_or_spotting()
        { var s=Settings();s.SaraEnabled=false;Assert.IsTrue(Check(s,false,"Jim (default)","spotter/car_left",spotting:false).Ready); }

        [Test]
        public void Explicitly_disabled_team_does_not_block_the_original_application()
        { var s=Settings();s.Enabled=false;s.TtsProvider=AiTtsProvider.WINDOWS;Assert.IsTrue(Check(s,false).Ready); }

        [Test]
        public void All_roles_disabled_do_not_block_the_original_application()
        { var s=Settings();s.BenEnabled=s.MikEnabled=s.SaraEnabled=false;Assert.IsTrue(Check(s,false).Ready); }

        [Test]
        public void Enabled_sara_requires_enabled_local_spotter()
        { Assert.AreEqual("spotter_disabled",Check(Settings(),spotting:false).Reason); }

        [Test]
        public void Sara_requires_a_backend_that_can_interrupt_engineer_speech()
        { var s=Settings();Assert.AreEqual("interruptible_audio_required",CrewVoiceReadiness.Evaluate(s,true,"Sara","spotter_Sara/car_left",CrewVoiceReadiness.ExpectedSaraVoiceKey(s),true,false).Reason); }

        [Test]
        public void Identity_includes_provider_model_voice_and_radio_filter()
        {
            var s=Settings();string original=CrewVoiceReadiness.ExpectedSaraVoiceKey(s);
            s.ElevenLabsModel="other-model";Assert.AreNotEqual(original,CrewVoiceReadiness.ExpectedSaraVoiceKey(s));
            s=Settings();s.ElevenLabsVoiceSara="other-voice";Assert.AreNotEqual(original,CrewVoiceReadiness.ExpectedSaraVoiceKey(s));
            s=Settings();s.RadioEffect=!s.RadioEffect;Assert.AreNotEqual(original,CrewVoiceReadiness.ExpectedSaraVoiceKey(s));
            s=Settings();s.TtsProvider=AiTtsProvider.OPENAI_COMPATIBLE;Assert.AreNotEqual(original,CrewVoiceReadiness.ExpectedSaraVoiceKey(s));
        }

        [Test]
        public async Task New_program_folder_restores_exclusive_sara_from_the_valid_shared_pack()
        {
            var settings = Settings();
            string voiceFolder = await Pack(CrewVoiceReadiness.ExpectedSaraVoiceKey(settings));
            var properties = new Dictionary<string, object> {
                { "spotter_name", "Jim (default)" }, { "enable_spotter", false }, { "use_naudio", false }
            };
            int saves = 0;
            Assert.IsTrue(CrewVoiceReadiness.RestoreStartupSelection(settings, voiceFolder,
                (string)properties["spotter_name"], (bool)properties["enable_spotter"], (bool)properties["use_naudio"],
                (key, value) => properties[key] = value, () => saves++));
            Assert.AreEqual("Sara", properties["spotter_name"]);
            Assert.AreEqual(true, properties["enable_spotter"]);
            Assert.AreEqual(true, properties["use_naudio"]);
            Assert.AreEqual(1, saves);
            // At the next startup those same persisted settings need neither writes nor another repair/restart.
            Assert.IsFalse(CrewVoiceReadiness.RestoreStartupSelection(settings, voiceFolder,
                (string)properties["spotter_name"], (bool)properties["enable_spotter"], (bool)properties["use_naudio"],
                (key, value) => Assert.Fail("Already-correct setting was overwritten"), () => Assert.Fail("Unnecessary save")));
            // Selecting Sara must not falsely claim that the old Jim audio was already reloaded.
            Assert.AreEqual("restart_required", CrewVoiceReadiness.Evaluate(settings, true, "Sara", "spotter/car_left", "", true).Reason);
        }

        [TestCase("team")] [TestCase("sara")] [TestCase("legacy")] [TestCase("windows")]
        public async Task Startup_restoration_respects_explicitly_disabled_sara_or_other_voice_mode(string disabled)
        {
            var settings = Settings();
            string voiceFolder = await Pack(CrewVoiceReadiness.ExpectedSaraVoiceKey(settings));
            if (disabled == "team") settings.Enabled = false;
            if (disabled == "sara") settings.SaraEnabled = false;
            if (disabled == "legacy") settings.ReplaceCrewChiefVoice = false;
            if (disabled == "windows") settings.TtsProvider = AiTtsProvider.WINDOWS;
            Assert.IsFalse(CrewVoiceReadiness.RestoreStartupSelection(settings, voiceFolder, "Jim (default)", false, false,
                (key, value) => Assert.Fail("Must not change explicit configuration"), () => Assert.Fail("Must not save")));
        }

        [TestCase("missing")] [TestCase("mismatched")] [TestCase("corrupt")]
        public async Task Startup_restoration_does_not_select_an_unusable_sara_pack(string failure)
        {
            var settings = Settings();
            string voiceFolder = await Pack(CrewVoiceReadiness.ExpectedSaraVoiceKey(settings));
            if (failure == "missing") voiceFolder = Path.Combine(voiceFolder, "nonexistent");
            if (failure == "mismatched") settings.ElevenLabsVoiceSara = "different-chosen-voice";
            if (failure == "corrupt") {
                string clip = Path.Combine(voiceFolder, "spotter_Sara", "car_left", "1.wav");
                byte[] bytes = File.ReadAllBytes(clip); bytes[100] ^= 1; File.WriteAllBytes(clip, bytes);
            }
            Assert.IsFalse(CrewVoiceReadiness.RestoreStartupSelection(settings, voiceFolder, "Jim (default)", false, false,
                (key, value) => Assert.Fail("Must not select an invalid pack"), () => Assert.Fail("Must not save")));
        }

        [Test]
        public async Task Startup_restoration_changes_only_required_audio_settings_and_saves_once()
        {
            var settings = Settings(); string voiceFolder = await Pack(CrewVoiceReadiness.ExpectedSaraVoiceKey(settings));
            var changed = new List<string>(); int saves = 0;
            Assert.IsTrue(CrewVoiceReadiness.RestoreStartupSelection(settings, voiceFolder, "Sara", true, false,
                (key, value) => { changed.Add(key); Assert.AreEqual(true, value); }, () => saves++));
            CollectionAssert.AreEqual(new [] { "use_naudio" }, changed); Assert.AreEqual(1, saves);
        }

        private static async Task<string> Pack(string voiceKey = "test|selected-original")
        {
            string folder=Path.Combine(Path.GetTempPath(),"rrca_voice_integrity_"+Guid.NewGuid().ToString("N"));
            // Synthetic local audio only. Retained for inspection; never reads or modifies the driver's sounds.
            await SaraSpotterPack.GenerateAsync(folder,voiceKey,text => {
                byte[] pcm=new byte[4800];for(int i=0;i<pcm.Length;i+=2){pcm[i]=100;pcm[i+1]=10;}return Task.FromResult(pcm);
            },null);
            TestContext.WriteLine("Retained synthetic pack: "+folder);return folder;
        }

        private static string CopyWithout(string source, string omittedRelative)
        {
            string target=Path.Combine(Path.GetTempPath(),"rrca_voice_variant_"+Guid.NewGuid().ToString("N"));
            foreach(string file in Directory.GetFiles(source,"*",SearchOption.AllDirectories))
            {
                string relative=file.Substring(source.Length+1);
                if(relative.Replace('\\','/')==omittedRelative)continue;
                string copy=Path.Combine(target,relative);Directory.CreateDirectory(Path.GetDirectoryName(copy));File.WriteAllBytes(copy,File.ReadAllBytes(file));
            }
            return target;
        }

        [Test]
        public async Task New_pack_contains_manifest_and_rejects_same_length_audio_tampering()
        {
            string folder=await Pack();Assert.IsTrue(SaraSpotterPack.IsGenerated(folder,"test|selected-original"));
            Assert.IsTrue(File.Exists(Path.Combine(folder,"spotter_Sara",SaraSpotterPack.ManifestFile)));
            string path=Path.Combine(folder,"spotter_Sara","car_left","1.wav");byte[] data=File.ReadAllBytes(path);data[100]^=1;File.WriteAllBytes(path,data);
            Assert.IsFalse(SaraSpotterPack.IsGenerated(folder,"test|selected-original"));
        }

        [Test]
        public async Task Complete_legacy_pack_is_accepted_but_corrupt_wave_is_not()
        {
            string original=await Pack();string folder=CopyWithout(original,"spotter_Sara/"+SaraSpotterPack.ManifestFile);
            Assert.IsTrue(SaraSpotterPack.IsGenerated(folder,"test|selected-original"));
            string path=Path.Combine(folder,"spotter_Sara","car_right","1.wav");byte[] data=File.ReadAllBytes(path);data[0]=0;File.WriteAllBytes(path,data);
            Assert.IsFalse(SaraSpotterPack.IsGenerated(folder,"test|selected-original"));
        }

        [Test]
        public async Task Malformed_manifest_or_different_identity_fails_closed()
        {
            string folder=await Pack();Assert.IsFalse(SaraSpotterPack.IsGenerated(folder,"test|another-voice"));
            File.WriteAllText(Path.Combine(folder,"spotter_Sara",SaraSpotterPack.ManifestFile),"{broken");
            Assert.IsFalse(SaraSpotterPack.IsGenerated(folder,"test|selected-original"));
        }

        [Test]
        public async Task Missing_clip_is_not_a_complete_spotter_pack()
        {
            string original=await Pack();string folder=CopyWithout(original,"spotter_Sara/clear_left/1.wav");
            Assert.IsFalse(SaraSpotterPack.IsGenerated(folder,"test|selected-original"));
        }

        [Test]
        public async Task Empty_new_render_preserves_the_previously_valid_original_pack()
        {
            string folder=await Pack();
            Assert.ThrowsAsync<InvalidDataException>(async () => await SaraSpotterPack.GenerateAsync(folder,"test|replacement",text => Task.FromResult(new byte[0]),null));
            Assert.IsTrue(SaraSpotterPack.IsGenerated(folder,"test|selected-original"));
            Assert.IsFalse(SaraSpotterPack.IsGenerated(folder,"test|replacement"));
        }

        [Test]
        public void Unavailable_pack_path_never_throws_or_claims_ready()
        { Assert.AreEqual("",SaraSpotterPack.ReadVoiceKey(null));Assert.IsFalse(SaraSpotterPack.IsGenerated(null,"voice")); }
    }
}
