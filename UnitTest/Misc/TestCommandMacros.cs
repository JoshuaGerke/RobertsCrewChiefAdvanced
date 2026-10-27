using CrewChiefV4;
using CrewChiefV4.Audio;
using CrewChiefV4.commands;

using NUnit.Framework;

using System;
using System.Diagnostics;
using System.Threading;

namespace UnitTest.Misc
{
    [TestFixture]
    public class TestCommandMacros
    {
        AudioPlayer audioPlayer;
        [SetUp] // nunit
        public void Init()
        {
            Log.setLogLevel(Log.LogType.Exception);
            // Doesn't help, static initialisation UIStrings = LoadUIStrings(); already happened // currentActiveProfile = new UserProfileSettings();
            UserSettings.GetUserSettings().setProperty("use_naudio", false); // trips Debug.Assert(!MainWindow.instance.InvokeRequired); in AudioPlayer
            if (audioPlayer == null)
            {
                audioPlayer = new AudioPlayer();
            }
            CrewChief.gameDefinition = new GameDefinition();
            ConsoleCapture.Clear();
            audioPlayer.purgeQueues();
        }
        [TearDown] // nunit
        public void Cleanup()
        {
            audioPlayer?.Dispose();
        }
        [Test, Explicit("Manueller Sprach-/Tastaturtest: öffnet Notepad und benötigt eingerichtete Controller und Spracherkennung.")]
        public void Test_voice_macro_R3E_Manual_Fuel()
        {
            CrewChief.eventsList.Clear();
            CrewChief.gameDefinition.gameEnum = GameEnum.RACE_ROOM;
            Game.game = CrewChief.gameDefinition.gameEnum;
            CrewChief.gameDefinition.processName = "Notepad"; // Write macro keys to notepad
            Process.Start("notepad.exe");
            CrewChief.rf2ChatTransceiver = new Rf2ChatTransceiver();
            var sr = new SpeechRecogniser(null);
            sr.initialiseSpeechEngine();
            sr.loadSRECommands();
            var controllerConfiguration = new ControllerConfiguration(null);
            MacroManager.initialise(audioPlayer, sr, controllerConfiguration);
            sr.loadMacroVoiceTriggers(MacroManager.voiceTriggeredMacros);
            string voiceMessage = "can you hear me";    // Check that the speech engine is working
            sr.TracePlayback(voiceMessage);
            Thread.Sleep(1000); // Give the speech engine time to process the command

            // Only seems to work when in debug mode, not run?
            // Now test the voice macro for adding fuel
            voiceMessage = "add fuel twenty-three litres";
            sr.TracePlayback(voiceMessage);
            Thread.Sleep(1000); // Give the speech engine time to process the command
            //ConsoleCapture.Contents.Should().Contain("acknowledge/OK");
        }
    }
}
