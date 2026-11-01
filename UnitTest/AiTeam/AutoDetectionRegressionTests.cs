using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using CrewChiefV4;
using NUnit.Framework;

namespace UnitTest.AiTeamTests
{
    [TestFixture, Category("AiTeam"), NonParallelizable, Apartment(ApartmentState.STA)]
    public class AutoDetectionRegressionTests
    {
        private static GameDefinition D(string name,string process,params string[] alternatives)
        { return new GameDefinition { friendlyName=name, processName=process, alternativeProcessNames=alternatives }; }

        [TestCase("RRRE64")] [TestCase("rrre64.exe")] [TestCase("RRRE")] [TestCase("RRRE.EXE")]
        public void RaceRoom_primary_and_alternative_processes_are_recognized(string process)
        { Assert.IsTrue(AutoGameDetection.Matches(GameDefinition.raceRoom,process)); }

        [TestCase("AMS2AVX")] [TestCase("ams2avx.exe")] [TestCase("AMS2")] [TestCase("AMS2.exe")]
        public void AMS2_primary_and_alternative_processes_are_recognized(string process)
        { Assert.IsTrue(AutoGameDetection.Matches(GameDefinition.ams2,process)); }

        [TestCase("iRacingService64")] [TestCase("iRacingUI")] [TestCase("RRRE64-helper")]
        [TestCase("")] [TestCase(null)]
        public void Services_launchers_and_near_matches_are_not_the_simulator(string process)
        { Assert.IsFalse(AutoGameDetection.Matches(D("iRacing","iRacingSim64DX11"),process)); }

        [Test]
        public void Single_running_sim_is_detected_while_RRCA_is_in_front()
        {
            var sim=D("RaceRoom","RRRE64");
            Assert.AreSame(sim,AutoGameDetection.Select(new[]{sim},new[]{"CrewChiefV4","RRRE64"},"CrewChiefV4",null,false));
        }

        [Test]
        public void Already_running_game_is_detected_without_a_foreground_change()
        {
            var sim=D("AMS2","AMS2AVX","AMS2");
            Assert.AreSame(sim,AutoGameDetection.Select(new[]{sim},new[]{"AMS2","explorer"},null,sim,false));
        }

        [Test]
        public void Two_different_background_sims_are_ambiguous_even_with_an_old_selection()
        {
            var a=D("AMS2","AMS2AVX");var b=D("RaceRoom","RRRE64");
            Assert.IsNull(AutoGameDetection.Select(new[]{a,b},new[]{"AMS2AVX","RRRE64"},"CrewChiefV4",a,false));
            Assert.AreSame(b,AutoGameDetection.Select(new[]{a,b},new[]{"AMS2AVX","RRRE64"},"RRRE64",a,false));
        }

        [Test]
        public void Alt_tab_to_unrelated_app_never_changes_a_running_crew()
        {
            var a=D("AMS2","AMS2AVX");var b=D("RaceRoom","RRRE64");
            Assert.IsNull(AutoGameDetection.Select(new[]{a,b},new[]{"AMS2AVX","RRRE64"},"explorer",a,true));
        }

        [Test]
        public void Shared_process_variant_keeps_the_explicit_selection()
        {
            var a=D("variant A","acs");var b=D("variant B","acs");
            Assert.AreSame(b,AutoGameDetection.Select(new[]{a,b},new[]{"acs"},null,b,false));
        }

        [Test]
        public void A_foreground_process_must_still_exist_in_the_snapshot()
        {
            var sim=D("RaceRoom","RRRE64");
            Assert.IsNull(AutoGameDetection.Select(new[]{sim},new[]{"explorer"},"RRRE64",sim,false));
        }

        [Test]
        public void Simulators_filtered_out_by_the_user_are_not_detected()
        { Assert.IsNull(AutoGameDetection.Select(new GameDefinition[0],new[]{"RRRE64"},"RRRE64",null,false)); }

        [Test]
        public void Alternative_process_counts_as_still_running_for_exit_monitoring()
        {
            var sim=D("RaceRoom","RRRE64","RRRE");
            Assert.IsTrue(new[]{"CrewChiefV4","RRRE"}.Any(p=>AutoGameDetection.Matches(sim,p)));
        }

        [Test]
        public void Manual_stop_suppresses_only_the_stopped_game()
        {
            var a=D("AMS2","AMS2AVX");var b=D("RaceRoom","RRRE64");
            Assert.IsNull(AutoGameDetection.Select(new[]{a,b},new[]{"AMS2AVX"},"AMS2AVX",a,false,a));
            Assert.AreSame(b,AutoGameDetection.Select(new[]{a,b},new[]{"AMS2AVX","RRRE64"},"RRRE64",a,false,a));
        }

        [TestCase(false)] [TestCase(true)]
        public void Toggle_applies_to_timer_and_saved_setting_without_stopping_running_crew(bool running)
        {
            var main=(MainWindow)FormatterServices.GetUninitializedObject(typeof(MainWindow));
            var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
            var timer=new System.Windows.Forms.Timer();
            var button=new Button();
            var previousMain=MainWindow.instance;
            MainWindow.instance=main;
            typeof(MainWindow).GetField("startApplicationButton",flags).SetValue(main,button);
            typeof(MainWindow).GetField("autoDetectTimer",flags).SetValue(main,timer);
            typeof(MainWindow).GetField("_isAppRunning",flags).SetValue(main,running);
            var settingsType=typeof(MainWindow).Assembly.GetType("CrewChiefV4.UserSettings");
            var settings=settingsType.GetMethod("GetUserSettings").Invoke(null,null);
            var get=settingsType.GetMethod("getBoolean");bool before=(bool)get.Invoke(settings,new object[]{"enable_auto_detect"});
            try
            {
                Assert.IsFalse((bool)settingsType.GetField("initFailed").GetValue(settings),
                    "The Windows test host must be allowed to save its own .NET configuration.");
                var buttonType=typeof(MainWindow).GetNestedType("StartButton",BindingFlags.NonPublic);
                var state=Activator.CreateInstance(buttonType,flags,null,new object[0],null);
                typeof(MainWindow).GetField("startButton",flags).SetValue(main,state);
                buttonType.GetMethod("update",flags).Invoke(state,new object[]{running});
                string textBefore=button.Text;
                main.SetAutoDetectEnabled(true);
                Assert.IsTrue(timer.Enabled);Assert.IsTrue((bool)get.Invoke(settings,new object[]{"enable_auto_detect"}));
                Assert.AreEqual(running,main.IsAppRunning);
                main.SetAutoDetectEnabled(false);
                Assert.IsFalse(timer.Enabled);Assert.IsFalse((bool)get.Invoke(settings,new object[]{"enable_auto_detect"}));
                Assert.AreEqual(running,main.IsAppRunning);
                if(running) Assert.AreEqual(textBefore,button.Text,"Stop remains available while the crew runs.");
            }
            finally
            {
                MainWindow.instance=previousMain;button.Dispose();timer.Dispose();settingsType.GetMethod("setProperty").Invoke(settings,new object[]{"enable_auto_detect",before});
                settingsType.GetMethod("saveUserSettings").Invoke(settings,null);
            }
        }
    }
}
