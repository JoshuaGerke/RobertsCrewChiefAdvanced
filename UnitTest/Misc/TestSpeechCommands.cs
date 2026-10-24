using CrewChiefV4;
using CrewChiefV4.Audio;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using CrewChiefV4.PitManager;

using FluentAssertions;

using NUnit.Framework;

using System;
using System.Collections.Generic;

namespace UnitTest.Misc.WillThrowExceptionsIfDebugging
{
    [TestFixture]
    public class TestSpeechCommands
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

        [Test]
        public void TestSpeechCommandsTable()
        {
            SpeechCommands.tableCheck().Should().BeTrue("speechCommands[] and controllersCommands[] check out");
        }

        [Test]
        [Ignore("Don't really work")]

        public void TestAllSpeechCommands()
        {
            // Doesn't really work as too many of the event handlers just report
            // "no data" for events they don't handle.
            // The "worst offenders" have been commented out but that means
            // the events they *should* handle won't be.
            CrewChief.gameDefinition.gameEnum = GameEnum.IRACING;
            Game.game = CrewChief.gameDefinition.gameEnum;
            CrewChief.eventsList.Clear();
            CrewChief.eventsList.Add("Timings", new Timings(audioPlayer));
            CrewChief.eventsList.Add("Position", new Position(audioPlayer));
            CrewChief.eventsList.Add("LapCounter", new LapCounter(audioPlayer, null));
            CrewChief.eventsList.Add("LapTimes", new LapTimes(audioPlayer));
            //CrewChief.eventsList.Add("Penalties", new Penalties(audioPlayer));
            //CrewChief.eventsList.Add("PitStops", new PitStops(audioPlayer));
            GlobalBehaviourSettings.enabledMessageTypes.AddRange(GlobalBehaviourSettings.defaultEnabledMessageTypes);
            CrewChief.fuelMultiplier = new CrewChief.FuelMultiplier(CrewChief.gameDefinition.fuelMultiplierType);
            CrewChief.eventsList.Add("Fuel", new Fuel(audioPlayer));
            //CrewChief.eventsList.Add("Battery", new Battery(audioPlayer));
            CrewChief.eventsList.Add("WatchedOpponents", new WatchedOpponents(audioPlayer));
            CrewChief.eventsList.Add("Strategy", new Strategy(audioPlayer));
            //CrewChief.eventsList.Add("Opponents", new Opponents(audioPlayer));
            CrewChief.eventsList.Add("RaceTime", new RaceTime(audioPlayer));
            CrewChief.eventsList.Add("TyreMonitor", new TyreMonitor(audioPlayer));
            CrewChief.eventsList.Add("EngineMonitor", new EngineMonitor(audioPlayer));
            CrewChief.eventsList.Add("DamageReporting", new DamageReporting(audioPlayer));
            CrewChief.eventsList.Add("PushNow", new PushNow(audioPlayer));
            CrewChief.eventsList.Add("FlagsMonitor", new FlagsMonitor(audioPlayer));
            CrewChief.eventsList.Add("ConditionsMonitor", new ConditionsMonitor(audioPlayer));
            CrewChief.eventsList.Add("OvertakingAidsMonitor", new OvertakingAidsMonitor(audioPlayer));
            CrewChief.eventsList.Add("FrozenOrderMonitor", new FrozenOrderMonitor(audioPlayer));
            CrewChief.eventsList.Add("IRacingBroadcastMessageEvent", new IRacingBroadcastMessageEvent(audioPlayer));
            CrewChief.eventsList.Add("MulticlassWarnings", new MulticlassWarnings(audioPlayer));
            CrewChief.eventsList.Add("DriverSwaps", new DriverSwaps(audioPlayer));
            CrewChief.eventsList.Add("CommonActions", new CommonActions(audioPlayer));
            CrewChief.eventsList.Add("OverlayController", new OverlayController(audioPlayer));
            CrewChief.eventsList.Add("VROverlayController", new VROverlayController(audioPlayer));
            CrewChief.eventsList.Add("Mqtt", new Mqtt(audioPlayer));
            CrewChief.eventsList.Add("CoDriver", new CoDriver(audioPlayer));
            CrewChief.eventsList.Add("PitManagerVoiceCmds", new PitManagerVoiceCmds(audioPlayer));

            var sessionEndMessages = new SessionEndMessages(audioPlayer);
            var alarmClock = new AlarmClock(audioPlayer);
            foreach (SpeechCommands.ID id in Enum.GetValues(typeof(SpeechCommands.ID)))
            {
                ConsoleCapture.Clear();
                foreach (AbstractEvent eventHandler in CrewChief.eventsList.Values)
                {
                    try
                    {
                        eventHandler.respond("can you hear me", id);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }

                ConsoleCapture.Contents.Should().Contain("can't be played because there is no sound for text fragment");
            }
            audioPlayer.Dispose();
        }

        [Test]
        public void TestAllControllerCommands()
        {
            CrewChief.gameDefinition.gameEnum = GameEnum.IRACING;
            Game.game = CrewChief.gameDefinition.gameEnum;
            CrewChief.eventsList.Clear();
            CrewChief.eventsList.Add("Battery", new Battery(audioPlayer));
            CrewChief.eventsList.Add("Fuel", new Fuel(audioPlayer));
            var eventHandler = new CommonActions(audioPlayer);

            foreach (var id in SpeechCommands.controllerCommands)
            {
                if (id.Key == SpeechCommands.ID.TOGGLE_RALLY_RECCE_MODE)
                {   // Not handled by CommonActions
                    continue;
                }
                ConsoleCapture.Clear();
                try
                {
                    if (!ControllerConfiguration.specialActions.ContainsKey(id.Value))
                    {
                        eventHandler.respond("", id.Key);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }

                ConsoleCapture.Contents.Should().NotContain("not handled by");
            }
        }

        [Test]
        public void TestLogCapture()
        {
            var eventHandler = new CommonActions(null);
            eventHandler.respond("", SpeechCommands.ID.NO_COMMAND);
            ConsoleCapture.Contents.Should().Contain("NO_COMMAND not handled by CommonActions");
        }

        [Test]
        public void Test_voice_RADIO_CHECK()
        {
            SpeechCommands.ID cmd = SpeechCommands.SpeechToCommand(CommonActions.Commands, "can you hear me");
            var cmdInt = (int)cmd; // Fluent assertion of enum doesn't seem to work directly
            cmdInt.Should().NotBe((int)SpeechCommands.ID.NO_COMMAND);
            cmdInt.Should().Be((int)SpeechCommands.ID.RADIO_CHECK);
            var eventHandler = new CommonActions(audioPlayer);
            eventHandler.respond("", cmd);
            ConsoleCapture.Contents.Should().Contain("acknowledge/radio_check");
        }

        [Test]
        public void Test_voice_DISABLE_MANUAL_FORMATION_LAP()
        {
            var cmd = SpeechCommands.SpeechToCommand(CommonActions.Commands, "standing start");
            var cmdInt = (int)cmd; // Fluent assertion of enum doesn't seem to work directly
            cmdInt.Should().NotBe((int)SpeechCommands.ID.NO_COMMAND);
            cmdInt.Should().Be((int)SpeechCommands.ID.DISABLE_MANUAL_FORMATION_LAP);
            var eventHandler = new CommonActions(audioPlayer);
            eventHandler.respond("", cmd);
            ConsoleCapture.Contents.Should().Contain("lap_counter/manual_formation_lap_mode_disabled");
        }
        [Test]
        [Ignore("Doesn't work when all tests are run")]
        public void Test_voice_WHATS_THE_FASTEST_LAP_TIME()
        {
            var cmd = SpeechCommands.SpeechToCommand(LapTimes.Commands, "what's the fastest lap time");
            var cmdInt = (int)cmd; // Fluent assertion of enum doesn't seem to work directly
            cmdInt.Should().NotBe((int)SpeechCommands.ID.NO_COMMAND);
            cmdInt.Should().Be((int)SpeechCommands.ID.WHATS_THE_FASTEST_LAP_TIME);

            var eventHandler = new LapTimes(audioPlayer);
            eventHandler.respond("", cmd);
            ConsoleCapture.Contents.Should().Contain("acknowledge/no_data");
        }
        [Test]
        public void Test_voice_SET_ALARM_CLOCK()
        {
            string voiceMessage = "set alarm to eight twenty-five";
            var cmd = SpeechCommands.SpeechToCommand(AlarmClock.Commands, voiceMessage);
            var cmdInt = (int)cmd; // Fluent assertion of enum doesn't seem to work directly
            cmdInt.Should().NotBe((int)SpeechCommands.ID.NO_COMMAND);
            cmdInt.Should().Be((int)SpeechCommands.ID.SET_ALARM_CLOCK);

            var eventHandler = new AlarmClock(audioPlayer);
            eventHandler.respond(voiceMessage, cmd);
            ConsoleCapture.Contents.Should().Contain("Alarm has been set to ");
            ConsoleCapture.Contents.Should().Contain("8:25:00");
        }
        [Test]
        public void Test_voice_WHATS_THE_FASTEST_LAP_TIME_fail()
        {
            var eventHandler = new LapTimes(audioPlayer);
            string eventName = "LapTimes";
            string command = "Xyzzy";
            CrewChief.eventsList.Clear();
            CrewChief.eventsList.Add(eventName, eventHandler);
            ConsoleCapture.Clear();
            CrewChief.HandleEvent(eventName, command);
            ConsoleCapture.Contents.Should().Contain($"Event {eventName} does not handle command {command}");
        }
        [Test]
        public void Test_controller_PRINT_TRACK_DATA()
        {
            //Assert.Inconclusive("CommonActions.ControllerCommands not yet implemented");
            var cmd = SpeechCommands.ControllerToCommand(CommonActions.ControllerCommands, "print_track_data");
            var cmdInt = (int)cmd; // Fluent assertion of enum doesn't seem to work directly
            cmdInt.Should().NotBe((int)SpeechCommands.ID.NO_COMMAND);
            cmdInt.Should().Be((int)SpeechCommands.ID.PRINT_TRACK_DATA);
            var eventHandler = new CommonActions(audioPlayer);
            eventHandler.respond("", cmd);
            ConsoleCapture.Contents.Should().Contain("No track data available");
        }

        [Test]
        public void Test_voice_getEventForActionVROverlayController()
        {
            CrewChief.eventsList.Clear();
            CrewChief.eventsList.Add("VROverlayController", new VROverlayController(audioPlayer));
            VROverlayController.vrUpdateThreadRunning = true;
            string voiceMessage = "show VR settings";
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("vrOverlayForm not available");
        }
        [Test]
        public void Test_voice_getEventForAction()
        {
            CrewChief.eventsList.Clear();
            CrewChief.eventsList.Add("CommonActions", new CommonActions(audioPlayer));
            string voiceMessage = "can you hear me";
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("acknowledge/radio_check");
        }
        [Test]
        public void Test_voice_getEventForAction_iRacing_pitstop_tearoff()
        {
            CrewChief.eventsList.Clear();
            CrewChief.gameDefinition.gameEnum = GameEnum.IRACING;
            Game.game = CrewChief.gameDefinition.gameEnum;
            CrewChief.eventsList.Add("IRacingBroadcastMessageEvent", new IRacingBroadcastMessageEvent(audioPlayer));
            string voiceMessage = "pitstop tearoff";
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("acknowledge/OK");
        }
        [Test]
        public void Test_voice_getEventForAction_iRacing_PIT_STOP_CHANGE_REAR_TYRES()
        {
            CrewChief.eventsList.Clear();
            CrewChief.gameDefinition.gameEnum = GameEnum.IRACING;
            Game.game = CrewChief.gameDefinition.gameEnum;
            CrewChief.eventsList.Add("IRacingBroadcastMessageEvent", new IRacingBroadcastMessageEvent(audioPlayer));

            string voiceMessage = "pitstop change rear tyres only";
            GlobalBehaviourSettings.useOvalLogic = false;
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("acknowledge/OK");

            ConsoleCapture.Clear();
            GlobalBehaviourSettings.useOvalLogic = true;
            eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult.Count.Should().NotBe(0); // eventResult is not NullEvent as IRacingBroadcastMessageEvent handles it
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("mandatory_pit_stops/cant_change_those");
        }
        [Test]
        public void Test_voice_getEventForAction_iRacing_PIT_STOP_CHANGE_RIGHT_SIDE_TYRES()
        {
            CrewChief.eventsList.Clear();
            CrewChief.gameDefinition.gameEnum = GameEnum.IRACING;
            Game.game = CrewChief.gameDefinition.gameEnum;
            CrewChief.eventsList.Add("IRacingBroadcastMessageEvent", new IRacingBroadcastMessageEvent(audioPlayer));

            string voiceMessage = "pitstop change right side tyres";
            GlobalBehaviourSettings.useOvalLogic = true;
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("acknowledge/OK");

            ConsoleCapture.Clear();
            GlobalBehaviourSettings.useOvalLogic = false;
            eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult.Count.Should().NotBe(0); // eventResult is not NullEvent as IRacingBroadcastMessageEvent handles it
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("mandatory_pit_stops/cant_change_those");
        }

        [Test]
        [Ignore("Doesn't work when all tests are run")]
        public void Test_voice_getEventForAction_rF2_DISPLAY_SECTORS()
        {
            CrewChief.eventsList.Clear();
            CrewChief.gameDefinition.gameEnum = GameEnum.RF2_64BIT;
            Game.game = CrewChief.gameDefinition.gameEnum;
            CrewChief.eventsList.Add("PitManagerVoiceCmds", new PitManagerVoiceCmds(audioPlayer));
            string voiceMessage = "display sectors";
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult[0].abstractEvent.respond(voiceMessage);
            var notInCar = ConsoleCapture.Contents.Contains("Not in car");
            var ok = ConsoleCapture.Contents.Contains("acknowledge/OK");
            (notInCar || ok).Should().BeTrue(); // Not very fluent...
        }
        [Test]
        [Ignore("Doesn't work when all tests are run")]
        public void Test_voice_getEventForAction_HOW_ARE_MY_TYRE_TEMPS()
        {
            CrewChief.eventsList.Clear();
            CrewChief.gameDefinition.gameEnum = GameEnum.RF2_64BIT; // (! iRacing)
            Game.game = CrewChief.gameDefinition.gameEnum;
            CrewChief.eventsList.Add("TyreMonitor", new TyreMonitor(audioPlayer));
            string voiceMessage = "how are my tire temps";
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            ConsoleCapture.Clear();
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("acknowledge/no_data");
            //setUp.ConsoleOutput.ToString().Should().Contain("HOW_ARE_MY_TYRE_TEMPS");
            eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            ConsoleCapture.Clear();
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("acknowledge/no_data");
            //setUp.ConsoleOutput.ToString().Should().Contain("HOW_ARE_MY_TYRE_TEMPS");
        }
        [Test]
        public void Test_controller_getEventForAction()
        {
            CrewChief.eventsList.Clear();
            CrewChief.eventsList.Add("CommonActions", new CommonActions(audioPlayer));
            string voiceMessage = "print_track_data";
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("No track data available");
        }

        [Test]
        public void Test_voice_getEventForActionLOOP()
        {
            CrewChief.eventsList.Clear();
            CrewChief.eventsList.Add("CommonActions", new CommonActions(audioPlayer));
            string voiceMessage = "can you hear me";
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("acknowledge/radio_check");
        }

        [Test] public void Test_voice_getEventForActionLOOP_iRacing_pitstop_clear_fuel()
        {
            CrewChief.eventsList.Clear();
            CrewChief.gameDefinition.gameEnum = GameEnum.IRACING;
            Game.game = CrewChief.gameDefinition.gameEnum;
            CrewChief.eventsList.Add("IRacingBroadcastMessageEvent", new IRacingBroadcastMessageEvent(audioPlayer));
            string voiceMessage = "pitstop clear fuel";
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("acknowledge/OK");
            //tbd setUp.ConsoleOutput.ToString().Should().Contain("PIT_STOP_CLEAR_FUEL");

        }
        [Test]
        public void Test_voice_getEventForAction_SET_ALARM_CLOCK()
        {
            CrewChief.eventsList.Clear();
            CrewChief.eventsList.Add("AlarmClock", new AlarmClock(audioPlayer));
            string voiceMessage = "set alarm to eight twenty-five";
            List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
            eventResult[0].abstractEvent.respond(voiceMessage);
            ConsoleCapture.Contents.Should().Contain("Alarm has been set to ");
            ConsoleCapture.Contents.Should().Contain("8:25:00");
        }
    }
}
