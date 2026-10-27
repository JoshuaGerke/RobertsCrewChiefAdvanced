using CrewChiefV4;
using CrewChiefV4.Audio;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using CrewChiefV4.NumberProcessing;
using CrewChiefV4.PitManager;

using FluentAssertions;

using NUnit.Framework;

using System;
using System.Collections.Generic;

namespace UnitTest.Misc.OpponentSpeechCommands
{
    public enum gameList
    {
        AllGames,
        HasSoftTyres,
        LicenceGames,
        HasPitManager,
        iRacingR3E,
        Rally,
        NeedsR3ERatingData,
        ACC,
        Iracing,
        LMU,
        RF2,
        R3E,
    }
    [TestFixture]
    public class TestOpponentSpeechCommands
    {
        static AudioPlayer audioPlayer;
        static GameStateData currentGameState;
        private Func<QueuedMessage, bool> previousMessageFilter;
        private float previousSoundPackVersion;
        [SetUp] // nunit
        public void Init()
        {
            // This fixture exercises the legacy voice path, independently of AI fixture order.
            previousMessageFilter = AudioPlayer.messageFilter;
            AudioPlayer.messageFilter = null;
            previousSoundPackVersion = SoundPackVersionsHelper.currentSoundPackVersion;
            // This fixture supplies the legacy numbers/23 fragment set. Do not inherit the installed soundpack format.
            SoundPackVersionsHelper.currentSoundPackVersion = 106;
            sInit();
        }
        public void sInit()
        {
            SubtitleManager.enableSubtitles = true;
            Log.setLogLevel(Log.LogType.Exception);
            // Doesn't help, static initialisation UIStrings = LoadUIStrings(); already happened // currentActiveProfile = new UserProfileSettings();
            UserSettings.GetUserSettings().setProperty("use_naudio", false); // trips Debug.Assert(!MainWindow.instance.InvokeRequired); in AudioPlayer
            if (audioPlayer == null)
            {
                audioPlayer = new AudioPlayer();
            }
            CrewChief.gameDefinition = new GameDefinition();
            CrewChief.eventsList.Clear();
            CrewChief.eventsList.Add("Opponents", new Opponents(audioPlayer));
            CrewChief.eventsList.Add("WatchedOpponents", new WatchedOpponents(audioPlayer));
            CrewChief.enableDriverNames = true;
            currentGameState = new GameStateData(10);
            CrewChief.currentGameState = currentGameState;
            NumberReaderFactory.NewNumberReader(); // so ConsoleCapture doesn't include "Using NumberReaderEn for soundPackLanguage en"
            CarData.loadCarClassData();
            GameOrVoipVolume.VolumeControlEnabled = true;

            # region Fake opponent data
            // "leading_driver" is 1st place
            // "second_place_driver" is 2nd place
            // "player" is 3rd place
            // "fourth_place_driver" is 4th place
            DriverNameHelper.lowerCaseRawNameToUsableName.Clear();
            SoundCache.availableDriverNames.Clear();
            List<string> driverNames = new List<string> { "leading_driver", "second_place_driver", "player", "fourth_place_driver" };
            for (var i = 0; i < driverNames.Count; i++)
            {
                var name = driverNames[i];
                DriverNameHelper.lowerCaseRawNameToUsableName.Add(name, name);
                SoundCache.availableDriverNames.Add(name);
                currentGameState.OpponentData.Add(name, new OpponentData
                {
                    ClassPosition = i + 1,
                    OverallPosition = i + 1,
                    DriverRawName = name,
                    CurrentTyres = TyreType.Soft,
                    LastLapTime = 23f,
                    CurrentBestLapTime = 23f,
                    iRating = 1500,
                    LicensLevel = new Tuple<string, float>("a", 999f),
                    Speed = 100f,
                    InPits = true,
                    DistanceRoundTrack = i * 100f,
                    r3eUserId = i
                });
            }
            currentGameState.SessionData.ClassPosition = driverNames.Count - 1;
            currentGameState.SessionData.NumCarsOverall = driverNames.Count;
            currentGameState.SessionData.NumCarsInPlayerClass = driverNames.Count;
            currentGameState.SessionData.PlayerCarNr = "99";
            #endregion Fake opponent data

            currentGameState.SessionData.TrackDefinition = TrackData.getTrackDefinition(
                "Some Track",
                5000
                );

            for (int i = 0; i < 100; i++)
            {
                SoundCache.availableSounds.Add($"numbers/{i}");
            }
            SoundCache.availableSounds.Add("numbers/hundred");
            SoundCache.availableSounds.Add("numbers/point");
            SoundCache.availableSounds.Add("numbers/minutes");
            SoundCache.availableSounds.Add("numbers/seconds");
            SoundCache.availableSounds.Add("licence/a_licence");
            SoundCache.availableSounds.Add("opponents/rating_intro");
            SoundCache.availableSounds.Add("acknowledge/no_data");
            SoundCache.availableSounds.Add("watched_opponents/acknowledge_we_will_watch");
            SoundCache.availableSounds.Add("watched_opponents/acknowledge_stop_watching_all");
            SoundCache.availableSounds.Add("watched_opponents/acknowledge_stop_watching_driver_name");

            ConsoleCapture.Clear();
        }

        [TearDown] // nunit
        public void Cleanup()
        {
            AudioPlayer.messageFilter = previousMessageFilter;
            SoundPackVersionsHelper.currentSoundPackVersion = previousSoundPackVersion;
            audioPlayer?.Dispose();
        }

        static readonly GameEnum[] allGames =
        {
            GameEnum.ASSETTO_64BIT,
            GameEnum.ACC,
            GameEnum.IRACING,
            GameEnum.LMU,
            GameEnum.RACE_ROOM,
            GameEnum.RF2_64BIT,
        };
        static readonly GameEnum[] hasSoftTyres =
        {
            GameEnum.IRACING,
            GameEnum.LMU,
            GameEnum.RACE_ROOM,
            GameEnum.RF2_64BIT,
        };
        static readonly GameEnum[] licenseGames =
        {
            GameEnum.IRACING,
            GameEnum.RACE_ROOM
        };
        static readonly GameEnum[] NeedsR3ERatingData =
        {
            GameEnum.RACE_ROOM,
        };
        static readonly GameEnum[] HasPitManager =
        {
            //handled separately GameEnum.ACC,
            GameEnum.IRACING,
            GameEnum.LMU,
            //handled separately GameEnum.RACE_ROOM,
            GameEnum.RF2_64BIT,
        };

        static readonly Dictionary<gameList, GameEnum[]> gameListToEnums = new Dictionary<gameList, GameEnum[]>
        {
            { gameList.AllGames, allGames },
            { gameList.HasSoftTyres, hasSoftTyres },
            { gameList.LicenceGames, licenseGames },
            { gameList.HasPitManager, HasPitManager },
            { gameList.iRacingR3E, new GameEnum[]{GameEnum.IRACING, GameEnum.RACE_ROOM } },
            { gameList.Rally, new GameEnum[]{GameEnum.ASSETTO_64BIT_RALLY} },
            { gameList.NeedsR3ERatingData, NeedsR3ERatingData },
            { gameList.ACC, new GameEnum[]{} },
            { gameList.Iracing, new GameEnum[]{GameEnum.IRACING } },
            { gameList.LMU, new GameEnum[]{GameEnum.LMU } },
            { gameList.RF2, new GameEnum[]{GameEnum.RF2_64BIT } },
            { gameList.R3E, new GameEnum[]{GameEnum.RACE_ROOM } },
        };
        #region testCases
        [TestCase("how good is leading_driver", "leading_driver", gameList.NeedsR3ERatingData, false)]
        [TestCase("what's my car number", "opponents/car_number", gameList.AllGames)]
        [TestCase("what tires am i on", "tyre_monitor/slick", gameList.AllGames)]
        [TestCase("who's behind in the race", "fourth_place_driver", gameList.NeedsR3ERatingData, false)] // TBD test fails on the second game??
        //Doesn't work when all tests are run [TestCase("who's behind on track", "fourth_place_driver", gameList.AllGames, false)]
        [TestCase("who's in front in the race", "second_place_driver", gameList.NeedsR3ERatingData, false)] // TBD test fails on the second game??
        [TestCase("who's two positions in front", "leading_driver", gameList.NeedsR3ERatingData, false)] // TBD test fails on the second game??
        //Doesn't work when all tests are run [TestCase("who's in front on the track", "second_place_driver", gameList.AllGames, false)]
        //Doesn't work when all tests are run [TestCase("who's leading", "leading_driver", gameList.AllGames, false)]
        [TestCase("how long since the car ahead pitted", "numbers/minutes", gameList.AllGames, false)]
        [TestCase("how long since the car behind pitted", "numbers/minutes", gameList.AllGames, false)]

        //Doesn't work when all tests are run [TestCase("who's in pea four", "fourth_place_driver", gameList.AllGames, false)]

        [TestCase("what tires is the leader on", "tyre_monitor/softs", gameList.HasSoftTyres)]
        [TestCase("what tire is the leader on", "tyre_monitor/softs", gameList.HasSoftTyres)]
        // needs work in compareWithLegacy  [TestCase("who's in position two", "Cmnt: Using mapped drivername second_place_driver for raw driver name second_place_driver", GameEnum.RF2_64BIT)]

        [TestCase("what's second_place_driver last lap time", "numbers/23", gameList.AllGames)]  //note: not second_place_driver's, that's handled with magic elsewhere
        [TestCase("what's second_place_driver best lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's second_place_driver eye rating", "1500", gameList.AllGames)]
        [TestCase("what's second_place_driver license class", "999", gameList.LicenceGames)]
        [TestCase("what's the driver in position two's last lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the driver in position two's best lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the driver in position two's eye rating", "1500", gameList.AllGames)]
        [TestCase("what's the driver in position two's license class", "999", gameList.LicenceGames)]
        [TestCase("what's the driver in pea one's last lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the driver in pea one's best lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the driver in pea one's eye rating", "1500", gameList.AllGames)]
        [TestCase("what's the driver in pea one's license class", "999", gameList.LicenceGames)]
        [TestCase("what's the leader's last lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the leader's best lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the leader's eye rating", "1500", gameList.AllGames)]
        [TestCase("what's the leader's license class", "999", gameList.LicenceGames)]
        [TestCase("what's the car in front's last lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the car in front's best lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the car in front's eye rating", "1500", gameList.AllGames)]
        [TestCase("what's the car in front's license class", "999", gameList.LicenceGames)]
        [TestCase("what's the car in front's last lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the car in front's best lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the car in front's eye rating", "1500", gameList.AllGames)]
        [TestCase("what's the car in front's license class", "999", gameList.LicenceGames)]
        [TestCase("what's the car in front's last lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the car in front's best lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the car in front's eye rating", "1500", gameList.AllGames)]
        [TestCase("what's the car in front's license class", "999", gameList.LicenceGames)]
        [TestCase("what's the car behind's last lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the car behind's best lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the car behind's eye rating", "1500", gameList.AllGames)]
        [TestCase("what's the car behind's license class", "999", gameList.LicenceGames)]
        [TestCase("what's the car behind's last lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the car behind's best lap time", "numbers/23", gameList.AllGames)]
        [TestCase("what's the car behind's eye rating", "1500", gameList.AllGames)]
        [TestCase("what's the car behind's license class", "999", gameList.LicenceGames)]
        #endregion testCases
        public void Test_voice_getEventForAction_Opponents(string voiceMessage, string shouldContain, gameList gamesList, bool legacyWorks = true)
        {
            var gameEnums = gameListToEnums[gamesList];
            ConsoleCapture.Clear();
            foreach (var gameEnum in gameEnums)
            {
                //ConsoleCapture.Clear();
                CrewChief.gameDefinition.gameEnum = gameEnum;
                Game.game = CrewChief.gameDefinition.gameEnum;
                List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
                eventResult[0].abstractEvent.trigger(currentGameState, currentGameState);
                ConsoleCapture.Clear();
                Log.Commentary($"Testing gameEnum {gameEnum}");
                audioPlayer.purgeQueues();
                eventResult[0].abstractEvent.respond(voiceMessage);
                if (shouldContain.StartsWith("numbers/", StringComparison.Ordinal))
                {
                    // Time fragments are resolved into the audio queue, not printed as sound paths.
                    // Verify the actual response, independently of diagnostic log verbosity.
                    var response = audioPlayer.getFirstWaitingImmediateMessage(SoundType.OTHER);
                    Assert.IsNotNull(response);
                    CollectionAssert.Contains(response.messageFolders, shouldContain);
                }
                else ConsoleCapture.Contents.Should().Contain(shouldContain);
            }
        }

        // WatchedOpponents
        //TBD [TestCase("stop monitoring all", "watched_opponents/acknowledge_stop_watching_all", gameList.AllGames)]
        [TestCase("watch the car ahead", "watched_opponents/acknowledge_we_will_watch", gameList.AllGames)]
        [TestCase("watch the car behind", "watched_opponents/acknowledge_we_will_watch", gameList.AllGames)]
        [TestCase("monitor rival leading_driver", "watched_opponents/acknowledge_we_will_watch", gameList.AllGames)]
        [TestCase("monitor team mate second_place_driver", "watched_opponents/acknowledge_we_will_watch", gameList.AllGames)]
        [TestCase("rival leading_driver", "watched_opponents/acknowledge_we_will_watch", gameList.AllGames)]
        [TestCase("team mate second_place_driver", "watched_opponents/acknowledge_we_will_watch", gameList.AllGames)]
        [TestCase("stop monitoring rival leading_driver", "watched_opponents/acknowledge_stop_watching_driver_name", gameList.AllGames)]
        [TestCase("stop monitoring team mate second_place_driver", "watched_opponents/acknowledge_stop_watching_driver_name", gameList.AllGames)]
        public void Test_voice_getEventForActionWatchedOpponents(string voiceMessage, string shouldContain, gameList gamesList, bool legacyWorks = true)
        {
            var gameEnums = gameListToEnums[gamesList];
            ConsoleCapture.Clear();
            foreach (var gameEnum in gameEnums)
            {
                CrewChief.gameDefinition.gameEnum = gameEnum;
                Game.game = CrewChief.gameDefinition.gameEnum;
                List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
                eventResult[0].abstractEvent.trigger(currentGameState, currentGameState);
                ConsoleCapture.Clear();
                Log.Commentary($"Testing gameEnum {gameEnum}");
                audioPlayer.purgeQueues();
                eventResult[0].abstractEvent.respond(voiceMessage);
                ConsoleCapture.Contents.Should().Contain(shouldContain);
            }
        }

        // Mega test
        // Set up all the event handlers and fake data
        // Read in all voice commands and expected handlers from json file  Couldn't get this to work so used loads of TestCases instead
        // Feed in all voice commands 
        // Check that the right handler picks it up
        //var events = getEventsForSpeech(recognisedText);
        //if (events != null)
        //{
        //    //test that only one event picked it up
        //    //test that the event is the expected one
        //    foreach (EventResult eventResult in events)
        //    {
        //        if (eventResult.abstractEvent != null)
        //        {
        //            eventResult.abstractEvent.respond(recognisedText, eventResult.cmd);
        //            //check that the expected sound was played?
        //            //test that nothing blew up?
        //        }
        //    }
        //}
        //else ERROR $"Speech command {recognisedText} not handled"
        #region testCases
        [TestCase("how good is leading_driver", "Opponents")]
        [TestCase("watch the car ahead", "WatchedOpponents")]
        [TestCase("What's my gap in front", "Timings")]
        //breaking test  [TestCase("watch the car ahad", "WatchedOpponents", gameList.AllGames)]
        //... list of all commands

        // created from speech_recognition_config.txt 

        [TestCase("how's my tire wear", "TyreMonitor")]
        [TestCase("what's my tire wear", "TyreMonitor")]
        [TestCase("tire wear", "TyreMonitor")]
        [TestCase("how are my tires", "TyreMonitor")]
        [TestCase("how's my transmission", "xx")]
        [TestCase("how's my gearbox", "xx")]
        [TestCase("how's my aero", "xx")]
        [TestCase("how's my bodywork", "xx")]
        [TestCase("how's my engine", "xx")]
        [TestCase("how's my suspension", "xx")]
        [TestCase("how's my brakes", "xx")]
        [TestCase("how are my brakes", "xx")]
        [TestCase("how's my fuel", "xx")]
        [TestCase("how's my fuel level", "xx")]
        [TestCase("how's my battery", "xx")]
        [TestCase("how's my battery level", "xx")]
        [TestCase("how's my energy level", "xx")]
        [TestCase("how's my pace", "xx")]
        [TestCase("how's my self gap", "xx")]
        [TestCase("how's my personal pace", "xx")]
        [TestCase("how's my delta best", "xx")]
        [TestCase("how are my tire temps", "TyreMonitor")]
        [TestCase("how are my tire temperatures", "TyreMonitor")]
        [TestCase("what are my tire temps", "TyreMonitor")]
        [TestCase("what are my tire temperatures", "TyreMonitor")]
        [TestCase("tire temps", "TyreMonitor")]
        [TestCase("tire temperatures", "TyreMonitor")]
        [TestCase("what are my tire pressures", "TyreMonitor")]
        [TestCase("tire pressures", "TyreMonitor")]
        [TestCase("how are my brake temps", "TyreMonitor")]
        [TestCase("how are my brake temperatures", "TyreMonitor")]
        [TestCase("what are my brake temps", "TyreMonitor")]
        [TestCase("what are my brake temperatures", "TyreMonitor")]
        [TestCase("brake temps", "TyreMonitor")]
        [TestCase("brake temperatures", "TyreMonitor")]
        [TestCase("how are my engine temps", "EngineMonitor")]
        [TestCase("how are my engine temperatures", "EngineMonitor")]
        [TestCase("whats my water temp", "EngineMonitor")]
        [TestCase("whats my water temperature", "EngineMonitor")]
        [TestCase("whats my oil temp", "EngineMonitor")]
        [TestCase("whats my oil temperature", "EngineMonitor")]
        [TestCase("what are my engine temps", "EngineMonitor")]
        [TestCase("what are my engine temperatures", "EngineMonitor")]
        [TestCase("what's the gap ahead", "Timings")]
        [TestCase("what's the gap in front", "Timings")]
        [TestCase("what's my gap ahead", "Timings")]
        [TestCase("whats my gap in front", "Timings")]
        [TestCase("gap in front", "Timings")]
        [TestCase("gap ahead", "Timings")]
        [TestCase("what's the gap behind", "Timings")]
        [TestCase("what's my gap behind", "Timings")]
        [TestCase("gap behind", "Timings")]
        [TestCase("what's the gap behind on track", "Timings")]
        [TestCase("what's the gap ahead on track", "Timings")]
        //tbd asserts [TestCase("what's the gap to the leader", "Timings")]
        [TestCase("what lap is the leader on", "Timings")]
        [TestCase("what's my last lap time", "LapTimes")]
        [TestCase("last lap time", "LapTimes")]
        [TestCase("lap time", "LapTimes")]
        [TestCase("what's my lap time", "LapTimes")]
        [TestCase("what's my best lap time", "LapTimes")]
        [TestCase("best lap time", "LapTimes")]
        [TestCase("best lap", "LapTimes")]
        [TestCase("what's the fastest lap", "LapTimes")]
        [TestCase("what's the fastest lap time", "LapTimes")]
        [TestCase("what's my position", "Position")]
        [TestCase("what's my fuel level", "Fuel")]
        [TestCase("what's my fuel usage", "Fuel")]
        [TestCase("what's my fuel consumption", "Fuel")]
        [TestCase("what's my fuel use", "Fuel")]
        [TestCase("whats my eye rating", "xx", gameList.Iracing)]
        [TestCase("what's my licence class", "xx", gameList.Iracing)]
        [TestCase("what's my expected finish position", "Position")]
        [TestCase("what's my expected position", "Position")]
        [TestCase("what tires am i on", "Opponents")]
        [TestCase("what tire am i on", "Opponents")]
        [TestCase("what tire type am i on", "Opponents")]
        [TestCase("is my pit box occupied", "xx")]
        [TestCase("is my pit stall occupied", "xx")]
        [TestCase("how much fuel to the end", "Fuel")]
        [TestCase("how much fuel do we need", "Fuel")]

        [TestCase("use a risky fuel strategy", "Fuel")]
        [TestCase("use a safe fuel strategy", "Fuel")]
        [TestCase("reset the fuel strategy", "Fuel")]

        [TestCase("how long will the tires last", "TyreMonitor")]
        [TestCase("how long on these tires", "TyreMonitor")]
        [TestCase("how long will these tires last", "TyreMonitor")]
        [TestCase("what's the pitlane speed limit", "xx")]
        [TestCase("what's the pit speed limit", "xx")]
        [TestCase("pit speed limit", "xx")]
        [TestCase("how many laps since I pitted", "xx")]
        [TestCase("how long since I pitted", "xx")]

        [TestCase("where should I line up", "xx")]

        [TestCase("calculate fuel for", "Fuel")]
        [TestCase("how much fuel do i need for", "Fuel")]
        [TestCase("how much fuel for", "Fuel")]

        [TestCase("give me tyre pace differences", "TyreMonitor")]
        [TestCase("what are the tire speeds", "TyreMonitor")]
        [TestCase("whats the difference between tires", "TyreMonitor")]
        [TestCase("compare tire compounds", "TyreMonitor")]


        [TestCase("keep quiet", "xx")]
        [TestCase("shut up", "xx")]
        [TestCase("I know what I'm doing", "xx")]
        [TestCase("leave me alone", "xx")]
        [TestCase("minimal talking", "xx")]
        [TestCase("keep me updated", "xx")]
        [TestCase("keep me informed", "xx")]
        [TestCase("keep me posted", "xx")]
        [TestCase("tell me the gaps", "xx")]
        [TestCase("tell me the deltas", "xx")]
        [TestCase("give me the deltas", "xx")]
        [TestCase("time deltas every lap", "xx")]
        [TestCase("don't tell me the gaps", "xx")]
        [TestCase("don't tell me the deltas", "xx")]
        [TestCase("don't give me the deltas", "xx")]
        [TestCase("no more deltas", "xx")]
        [TestCase("no more gaps", "xx")]
        [TestCase("give me yellows", "xx")]
        [TestCase("tell me yellows", "xx")]
        [TestCase("give me incident updates", "xx")]
        [TestCase("give me yellow flag updates", "xx")]
        [TestCase("no more yellows", "xx")]
        [TestCase("stop incident updates", "xx")]
        [TestCase("don't give me yellows", "xx")]
        [TestCase("don't tell me yellows", "xx")]
        [TestCase("this is the formation lap", "xx")]
        [TestCase("formation lap", "xx")]
        [TestCase("rolling start", "xx")]
        [TestCase("standing start", "xx")]
        [TestCase("no formation lap", "xx")]
        [TestCase("talk to me anywhere", "xx")]
        [TestCase("messages at any point", "xx")]
        [TestCase("don't talk in the corners", "xx")]
        [TestCase("no messages in the corners", "xx")]
        [TestCase("don't talk in the braking zones", "xx")]
        [TestCase("no messages in the braking zones", "xx")]
        // Handled separately
        //[TestCase("spot", "xx")]
        //[TestCase("start spotting", "xx")]
        //[TestCase("enabled spotter", "xx")]
        //[TestCase("don't spot", "xx")]


        [TestCase("who's in front in the race", "Opponents")]
        [TestCase("who's ahead in the race", "Opponents")]
        [TestCase("who's in front", "Opponents")]
        [TestCase("who's ahead", "Opponents")]
        [TestCase("who's two positions in front", "Opponents")]
        [TestCase("who's two positions ahead", "Opponents")]
        [TestCase("who's he fighting", "Opponents")]
        [TestCase("who's behind in the race", "Opponents")]
        [TestCase("who's behind", "Opponents")]
        [TestCase("who's in front on track", "Opponents")]
        [TestCase("who's ahead on track", "Opponents")]
        [TestCase("who's behind on track", "Opponents")]
        [TestCase("who's leading", "Opponents")]
        [TestCase("who's in the lead", "Opponents")]
        [TestCase("who is leading", "Opponents")]
        [TestCase("who is in the lead", "Opponents")]
        [TestCase("who's the leader", "Opponents")]

        [TestCase("how long since the car in front pitted", "Opponents")]
        [TestCase("how long since the car ahead pitted", "Opponents")]
        [TestCase("how long since the car behind pitted", "Opponents")]

        [TestCase("what's my car number", "Opponents")]

        [TestCase("mark the driver ahead as dirty", "Timings")]
        [TestCase("mark the driver behind as dirty", "Timings")]

        [TestCase("where should I attack", "Timings")]
        [TestCase("where am I faster", "Timings")]
        [TestCase("where can I attack", "Timings")]
        [TestCase("where should I defend", "Timings")]
        [TestCase("where am I slower", "Timings")]
        [TestCase("where is he faster", "Timings")]
        [TestCase("where will he attack", "Timings")]

        [TestCase("how long's left", "xx")]
        [TestCase("how many laps left", "xx")]
        [TestCase("how many laps to go", "xx")]
        [TestCase("what lap is it", "xx")]
        [TestCase("what lap am I on", "xx")]
        [TestCase("what's the time", "xx")]
        [TestCase("what time is it", "xx")]
        //[TestCase("repeat last message", "xx")]
        //[TestCase("say again", "xx")]
        [TestCase("have I served my penalty", "Penalties")]
        [TestCase("do I have a penalty", "Penalties")]
        [TestCase("do I still have a penalty", "Penalties")]
        [TestCase("do I have to pit", "PitStops")]
        [TestCase("do I need to pit", "PitStops")]
        [TestCase("do I have a mandatory pit stop", "PitStops")]
        [TestCase("do I have a mandatory stop", "PitStops")]
        [TestCase("do I have to make a pit stop", "PitStops")]
        [TestCase("what are my sector times", "LapTimes")]
        [TestCase("what's my last sector time", "LapTimes")]
        [TestCase("what's the air temp", "xx")]
        [TestCase("what's the air temperature", "xx")]
        [TestCase("what's the track temp", "xx")]
        [TestCase("what's the track temperature", "xx")]
        [TestCase("can you hear me", "xx")]

        [TestCase("what's my brake bias", "xx")]

        [TestCase("read corner names", "xx")]
        [TestCase("corner names", "xx")]
        [TestCase("tell me the corner names", "xx")]

        [TestCase("damage report", "xx")]
        [TestCase("how's my car", "xx")]
        [TestCase("is my car ok", "xx")]
        [TestCase("car status", "xx")]
        [TestCase("session status", "xx")]
        [TestCase("race status", "xx")]
        [TestCase("full update", "xx")]
        [TestCase("full status", "xx")]
        [TestCase("update me", "xx")]

        [TestCase("start pace notes", "xx")]
        [TestCase("stop pace notes", "xx")]

        [TestCase("is the car ahead in my class", "MulticlassWarnings")]
        [TestCase("is the car ahead my class", "MulticlassWarnings")]
        [TestCase("is the car ahead the same class as me", "MulticlassWarnings")]
        [TestCase("is the car in front in my class", "MulticlassWarnings")]
        [TestCase("is the car in front my class", "MulticlassWarnings")]
        [TestCase("is the car in front the same class as me", "MulticlassWarnings")]
        [TestCase("is the car behind in my class", "MulticlassWarnings")]
        [TestCase("is the car behind my class", "MulticlassWarnings")]
        [TestCase("is the car behind the same class as me", "MulticlassWarnings")]
        [TestCase("what class is the car ahead", "MulticlassWarnings")]
        [TestCase("what class is the guy ahead", "MulticlassWarnings")]
        [TestCase("what class is the car in front", "MulticlassWarnings")]
        [TestCase("what class is the guy in front", "MulticlassWarnings")]
        [TestCase("what class is the car behind", "MulticlassWarnings")]
        [TestCase("what class is the guy behind", "MulticlassWarnings")]

        // Handled separately
        //[TestCase("clarify", "xx")]
        //[TestCase("tell me more", "xx")]
        //[TestCase("more information", "xx")]
        //[TestCase("more info", "xx")]

        //[TestCase("yeah", "xx")]
        //[TestCase("yes", "xx")]
        //[TestCase("fine", "xx")]
        //[TestCase("I'm OK", "xx")]
        //[TestCase("I'm fine", "xx")]
        //[TestCase("yes I'm fine", "xx")]
        //[TestCase("yeah I'm fine", "xx")]

        [TestCase("where will i be after a stop", "Strategy")]
        [TestCase("estimate pit exit positions", "Strategy")]
        [TestCase("what will happen if i pit", "Strategy")]
        [TestCase("time this stop", "Strategy")]
        [TestCase("practice pitstop", "Strategy")]
        [TestCase("time this pitstop", "Strategy")]
        [TestCase("pitstop benchmark", "Strategy")]

        [TestCase("set alarm to two pm", "AlarmClock")]
        [TestCase("alarm me at three am", "AlarmClock")]
        [TestCase("clear alarm clock", "AlarmClock")]
        [TestCase("clear alarms", "AlarmClock")]

        [TestCase("enable cut track warnings", "xx")]
        [TestCase("play cut track warnings", "xx")]
        [TestCase("warn about cuts", "xx")]
        [TestCase("no cut track warnings", "xx")]
        [TestCase("no more cut warnings", "xx")]
        [TestCase("no more cut track warnings", "xx")]

        [TestCase("how's my left front camber", "TyreMonitor")]
        [TestCase("how's my right front camber", "TyreMonitor")]
        [TestCase("how's my left rear camber", "TyreMonitor")]
        [TestCase("how's my right rear camber", "TyreMonitor")]
        [TestCase("how's my front camber", "TyreMonitor")]
        [TestCase("how's my rear camber", "TyreMonitor")]

        [TestCase("how are my front pressures", "TyreMonitor")]
        [TestCase("how are my front tire pressures", "TyreMonitor")]
        [TestCase("how are my rear pressures", "TyreMonitor")]
        [TestCase("how are my rear tire pressures", "TyreMonitor")]
        [TestCase("how are my pressures", "TyreMonitor")]
        [TestCase("how are my tire pressures", "TyreMonitor")]

        [TestCase("how's my left front camber right now", "TyreMonitor")]
        [TestCase("how's my right front camber right now", "TyreMonitor")]
        [TestCase("how's my left rear camber right now", "TyreMonitor")]
        [TestCase("how's my right rear camber right now", "TyreMonitor")]
        [TestCase("how's my front camber right now", "TyreMonitor")]
        [TestCase("how's my rear camber right now", "TyreMonitor")]

        [TestCase("how are my front pressures right now", "TyreMonitor")]
        [TestCase("how are my front tire pressures right now", "TyreMonitor")]
        [TestCase("how are my rear pressures right now", "TyreMonitor")]
        [TestCase("how are my rear tire pressures right now", "TyreMonitor")]
        [TestCase("how are my pressures right now", "TyreMonitor")]
        [TestCase("how are my tire pressures right now", "TyreMonitor")]

        [TestCase("what are my left front surface temps", "TyreMonitor")]
        [TestCase("what are my right front surface temps", "TyreMonitor")]
        [TestCase("what are my left rear surface temps", "TyreMonitor")]
        [TestCase("what are my right rear surface temps", "TyreMonitor")]

        [TestCase("what are the pit actions", "PitStops")]
        [TestCase("what's the pitstop plan", "PitStops")]

        [TestCase("stop complaining", "xx")]
        [TestCase("stop grumbling", "xx")]
        [TestCase("fuck off", "xx")]

        [TestCase("what's my rank", "xx")]
        [TestCase("what's my rating", "xx")]
        [TestCase("what's my reputation", "xx")]

        [TestCase("watch the car ahead", "WatchedOpponents")]
        [TestCase("watch the car behind", "WatchedOpponents")]
        [TestCase("cancel monitored drivers", "WatchedOpponents")]
        [TestCase("stop monitoring drivers", "WatchedOpponents")]
        [TestCase("stop monitoring all", "WatchedOpponents")]


        [TestCase("pitstop add", "xx", gameList.HasPitManager)]
        [TestCase("box, add", "xx", gameList.HasPitManager)]
        [TestCase("pitstop tearoff", "xx", gameList.Iracing)]
        [TestCase("box, tearoff", "xx", gameList.Iracing)]
        [TestCase("pitstop windscreen", "xx", gameList.Iracing)]
        [TestCase("box, windscreen", "xx", gameList.Iracing)]
        [TestCase("pitstop fast repair", "xx", gameList.Iracing)]
        [TestCase("box, fast repair", "xx", gameList.Iracing)]
        [TestCase("pitstop repair", "xx", gameList.Iracing)]
        [TestCase("box, repair", "xx", gameList.Iracing)]
        [TestCase("pitstop clear all", "xx", gameList.HasPitManager)]
        [TestCase("box, clear all", "xx", gameList.HasPitManager)]
        [TestCase("pitstop clear tyres", "xx", gameList.HasPitManager)]
        [TestCase("box, clear tyres", "xx", gameList.HasPitManager)]
        [TestCase("pitstop don't change tyres", "xx", gameList.HasPitManager)]
        [TestCase("box, don't change tyres", "xx", gameList.HasPitManager)]
        [TestCase("pitstop clear tearoff", "xx", gameList.Iracing)]
        [TestCase("box, clear tearoff", "xx", gameList.Iracing)]
        [TestCase("pitstop clear windscreen", "xx", gameList.Iracing)]
        [TestCase("box, clear windscreen", "xx", gameList.Iracing)]
        [TestCase("pitstop clear fast repair", "xx", gameList.Iracing)]
        [TestCase("box, clear fast repair", "xx", gameList.Iracing)]
        [TestCase("pitstop clear fuel", "xx", gameList.HasPitManager)]
        [TestCase("box, clear fuel", "xx", gameList.HasPitManager)]

        [TestCase("pitstop change all tyres", "xx", gameList.HasPitManager)]
        [TestCase("box, change all tyres", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change tyres", "xx", gameList.ACC)] //tbd !!
        [TestCase("box, change tyres", "xx", gameList.ACC)]
        [TestCase("pitstop change left front tyre", "xx", gameList.HasPitManager)]
        [TestCase("box, change left front tyre", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change front left tyre", "xx", gameList.HasPitManager)]
        [TestCase("box, change front left tyre", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change right front tyre", "xx", gameList.HasPitManager)]
        [TestCase("box, change right front tyre", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change front right tyre", "xx", gameList.HasPitManager)]
        [TestCase("box, change front right tyre", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change left rear tyre", "xx", gameList.HasPitManager)]
        [TestCase("box, change left rear tyre", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change rear left tyre", "xx", gameList.HasPitManager)]
        [TestCase("box, change rear left tyre", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change right rear tyre", "xx", gameList.HasPitManager)]
        [TestCase("box, change right rear tyre", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change rear right tyre", "xx", gameList.HasPitManager)]
        [TestCase("box, change rear right tyre", "xx", gameList.HasPitManager)]
        //[TestCase("pitstop change left side tyres", "xx", gameList.PitManager)]  //ovals
        //[TestCase("box, change left side tyres", "xx", gameList.PitManager)]
        //[TestCase("pitstop change right side tyres", "xx", gameList.PitManager)]
        //[TestCase("box, change right side tyres", "xx", gameList.PitManager)]

        [TestCase("pitstop change tire pressures", "xx", gameList.HasPitManager)]
        [TestCase("box, change tire pressures", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change left front tyre pressure", "xx", gameList.HasPitManager)]
        [TestCase("box, change left front tyre pressure", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change right front tyre pressure", "xx", gameList.HasPitManager)]
        [TestCase("box, change right front tyre pressure", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change left rear tyre pressure", "xx", gameList.HasPitManager)]
        [TestCase("box, change left rear tyre pressure", "xx", gameList.HasPitManager)]
        [TestCase("pitstop change right rear tyre pressure", "xx", gameList.HasPitManager)]
        [TestCase("box, change right rear tyre pressure", "xx", gameList.HasPitManager)]
        [TestCase("pitstop fuel to the end", "xx", gameList.HasPitManager)]
        [TestCase("box, fuel to the end", "xx", gameList.HasPitManager)]
        [TestCase("pitstop fuel to the end of the race", "xx", gameList.HasPitManager)]
        [TestCase("box, fuel to the end of the race", "xx", gameList.HasPitManager)]
        // LMU-specific pit commands
        [TestCase("pitstop fuel ratio", "PitManagerVoiceCmds", gameList.LMU)]
        [TestCase("box, fuel ratio", "PitManagerVoiceCmds", gameList.LMU)]
        [TestCase("pitstop virtual energy", "PitManagerVoiceCmds", gameList.LMU)]
        [TestCase("box, virtual energy", "PitManagerVoiceCmds", gameList.LMU)]
        //[TestCase("point", "xx", gameList.LMU)]

        // iRacing / R3E-specific pit commands
        [TestCase("pitstop change front tyres only", "xx", gameList.Iracing)]
        [TestCase("box, change front tyres only", "xx", gameList.Iracing)]
        [TestCase("pitstop change rear tyres only", "xx", gameList.Iracing)]
        [TestCase("box, change rear tyres only", "xx", gameList.Iracing)]

        // R3E-specific pit commands
        //Handled separately 
        //[TestCase("pitstop fix front aero only", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, fix front aero only", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop fix rear aero only", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, fix rear aero only", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop fix all aero", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, fix all aero", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop don't fix aero", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, don't fix aero", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop fix suspension", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, fix suspension", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop don't fix suspension", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, don't fix suspension", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop serve penalty", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, serve penalty", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop don't serve penalty", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, don't serve penalty", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop refuel", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, refuel", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop don't refuel", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, don't refuel", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop clear fuel", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, clear fuel", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop next tire compound", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, next tire compound", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop hard tires", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, hard tires", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop medium tires", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, medium tires", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("pitstop soft tires", "xx", gameList.NeedsR3ERatingData)]
        //[TestCase("box, soft tires", "xx", gameList.NeedsR3ERatingData)]

        [TestCase("pitstop super soft tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, super soft tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop ultra soft tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, ultra soft tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop hyper soft tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, hyper soft tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop prime tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, prime tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop primary tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, primary tyres", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop option tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, option tires", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop alternate tyres", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, alternate tires", "PitManagerVoiceCmds", gameList.RF2)]

        // LMU-specific pit commands
        [TestCase("pitstop used hard tires", "PitManagerVoiceCmds", gameList.LMU)]
        [TestCase("box, used hard tires", "PitManagerVoiceCmds", gameList.LMU)]
        [TestCase("pitstop used medium tires", "PitManagerVoiceCmds", gameList.LMU)]
        [TestCase("box, used medium tires", "PitManagerVoiceCmds", gameList.LMU)]
        [TestCase("pitstop used soft tires", "PitManagerVoiceCmds", gameList.LMU)]
        [TestCase("box, used soft tires", "PitManagerVoiceCmds", gameList.LMU)]

        // ACC specific pit commands
        [TestCase("pitstop change front pressures", "xx", gameList.ACC)]
        [TestCase("box change front pressures", "xx", gameList.ACC)]
        [TestCase("pitstop change rear pressures", "xx", gameList.ACC)]
        [TestCase("box change rear pressures", "xx", gameList.ACC)]
        [TestCase("pitstop tire set", "xx", gameList.ACC)]
        [TestCase("box tire set", "xx", gameList.ACC)]
        [TestCase("pitstop select best tires", "xx", gameList.ACC)]
        [TestCase("box select best tyres", "xx", gameList.ACC)]

        // Pit Manager/rF2-specific pit commands
        [TestCase("pitstop fix all", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, fix all", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop repair all", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, repair all", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop fix body", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, fix body", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop repair body", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, repair body", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop fix none", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, fix none", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop repair none", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, repair none", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop intermediate tyres", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, intermediate tyres", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop wet tyres", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, wet tyres", "PitManagerVoiceCmds", gameList.RF2)]
        //tbd [TestCase("pitstop dry tyres", "PitManagerVoiceCmds", gameList.RF2)]
        //tbd [TestCase("box, dry tyres", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop monsoon tyres", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, monsoon tyres", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("pitstop fill to", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("box, fill to", "PitManagerVoiceCmds", gameList.RF2)]

        [TestCase("display sectors", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("display sector times", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("M F D sectors", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("M F D sector times", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("display pit menu", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("M F D pit menu", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("display tyres", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("M F D tyres", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("display temps", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("display temperatures", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("M F D temps", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("M F D temperatures", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("display race info", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("M F D race info", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("display standings", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("M F D standings", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("display penalties", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("M F D penalties", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("display next", "PitManagerVoiceCmds", gameList.RF2)]
        [TestCase("M F D next", "PitManagerVoiceCmds", gameList.RF2)]

        [TestCase("how many incidents do i have", "xx", gameList.Iracing)]
        [TestCase("how many incident points do i have", "xx", gameList.Iracing)]
        [TestCase("what's my incident count", "xx", gameList.Iracing)]
        [TestCase("what are my incident points", "xx", gameList.Iracing)]
        [TestCase("what's the incident limit", "xx", gameList.Iracing)]
        [TestCase("what's the incident points limit", "xx", gameList.Iracing)]
        [TestCase("what's the sof", "xx", gameList.Iracing)]
        [TestCase("what is the strength of field", "xx", gameList.Iracing)]
        [TestCase("what's the strength of field", "xx", gameList.Iracing)]

        [TestCase("how many laps on tire set", "TyreMonitor")]
        [TestCase("how many laps are on tire set", "TyreMonitor")]

        [TestCase("how old are these tires", "TyreMonitor")]

        [TestCase("hide overlay", "OverlayController")]
        [TestCase("close overlay", "OverlayController")]
        [TestCase("hide console", "OverlayController")]
        [TestCase("close console", "OverlayController")]
        [TestCase("hide chart", "OverlayController")]
        [TestCase("close chart", "OverlayController")]
        [TestCase("show overlay", "OverlayController")]
        [TestCase("show console", "OverlayController")]
        [TestCase("show chart", "OverlayController")]
        [TestCase("show all overlays", "OverlayController")]
        [TestCase("new chart", "OverlayController")]
        [TestCase("clear chart", "OverlayController")]
        [TestCase("clear data", "OverlayController")]
        [TestCase("refresh chart", "OverlayController")]
        [TestCase("show stacked charts", "OverlayController")]
        [TestCase("show single chart", "OverlayController")]
        [TestCase("show time", "OverlayController")]
        [TestCase("show distance", "OverlayController")]
        [TestCase("show me", "OverlayController")]
        [TestCase("chart, remove", "OverlayController")]
        [TestCase("best lap", "LapTimes")]
        //tbd [TestCase("last lap", "LapTimes")]
        [TestCase("opponent best lap", "LapTimes")]
        //tbd [TestCase("opponent", "LapTimes")]
        [TestCase("show sector one", "OverlayController")]
        [TestCase("show sector two", "OverlayController")]
        [TestCase("show sector three", "OverlayController")]
        [TestCase("show all sectors", "OverlayController")]
        [TestCase("zoom in", "OverlayController")]
        [TestCase("zoom out", "OverlayController")]
        [TestCase("reset zoom", "OverlayController")]
        [TestCase("pan left", "OverlayController")]
        [TestCase("pan right", "OverlayController")]
        [TestCase("show last lap", "OverlayController")]
        [TestCase("show next lap", "OverlayController")]
        [TestCase("show previous lap", "OverlayController")]
        [TestCase("toggle VR overlays", "VROverlayController")]
        [TestCase("show VR settings", "VROverlayController")]
        [TestCase("hide VR settings", "VROverlayController")]

        [TestCase("show subtitles", "xx")]
        [TestCase("hide subtitles", "xx")]
        [TestCase("close subtitles", "xx")]

        [TestCase("earlier calls", "CoDriver", gameList.Rally)]
        [TestCase("more notice", "CoDriver", gameList.Rally)]
        [TestCase("later calls", "CoDriver", gameList.Rally)]
        [TestCase("corner number first", "CoDriver", gameList.Rally)]
        [TestCase("use corner number first", "CoDriver", gameList.Rally)]
        [TestCase("corner description", "CoDriver", gameList.Rally)]
        [TestCase("use corner description", "CoDriver", gameList.Rally)]
        [TestCase("corner direction first", "CoDriver", gameList.Rally)]
        [TestCase("use corner direction first", "CoDriver", gameList.Rally)]
        [TestCase("start stage reccy", "CoDriver", gameList.Rally)]
        [TestCase("start reccy", "CoDriver", gameList.Rally)]
        [TestCase("start recording pace notes", "CoDriver", gameList.Rally)]
        [TestCase("start recording stage notes", "CoDriver", gameList.Rally)]
        [TestCase("finish stage reccy", "CoDriver", gameList.Rally)]
        [TestCase("finish reccy", "CoDriver", gameList.Rally)]
        [TestCase("finish recording pace notes", "CoDriver", gameList.Rally)]
        [TestCase("finish recording stage notes", "CoDriver", gameList.Rally)]
        [TestCase("correction", "CoDriver", gameList.Rally)]
        [TestCase("earlier", "CoDriver", gameList.Rally)]
        [TestCase("sooner", "CoDriver", gameList.Rally)]
        [TestCase("later", "CoDriver", gameList.Rally)]
        [TestCase("insert", "CoDriver", gameList.Rally)]
        [TestCase("left", "CoDriver", gameList.Rally)]
        [TestCase("right", "CoDriver", gameList.Rally)]
        [TestCase("one", "CoDriver", gameList.Rally)]
        [TestCase("acute", "CoDriver", gameList.Rally)]
        [TestCase("two", "CoDriver", gameList.Rally)]
        [TestCase("kay", "CoDriver", gameList.Rally)]
        [TestCase("three", "CoDriver", gameList.Rally)]
        [TestCase("slow", "CoDriver", gameList.Rally)]
        [TestCase("four", "CoDriver", gameList.Rally)]
        [TestCase("medium", "CoDriver", gameList.Rally)]
        [TestCase("five", "CoDriver", gameList.Rally)]
        [TestCase("fast", "CoDriver", gameList.Rally)]
        [TestCase("six", "CoDriver", gameList.Rally)]
        [TestCase("slight", "CoDriver", gameList.Rally)]
        [TestCase("square", "CoDriver", gameList.Rally)]
        [TestCase("hairpin", "CoDriver", gameList.Rally)]
        [TestCase("open hairpin", "CoDriver", gameList.Rally)]
        [TestCase("flat", "CoDriver", gameList.Rally)]
        [TestCase("tightens to one", "CoDriver", gameList.Rally)]
        [TestCase("tightens one", "CoDriver", gameList.Rally)]
        [TestCase("tightens acute", "CoDriver", gameList.Rally)]
        [TestCase("tightens to acute", "CoDriver", gameList.Rally)]
        [TestCase("tightens to two", "CoDriver", gameList.Rally)]
        [TestCase("tightens two", "CoDriver", gameList.Rally)]
        [TestCase("tightens kay", "CoDriver", gameList.Rally)]
        [TestCase("tightens to kay", "CoDriver", gameList.Rally)]
        [TestCase("tightens to three", "CoDriver", gameList.Rally)]
        [TestCase("tightens three", "CoDriver", gameList.Rally)]
        [TestCase("tightens slow", "CoDriver", gameList.Rally)]
        [TestCase("tightens to slow", "CoDriver", gameList.Rally)]
        [TestCase("tightens to four", "CoDriver", gameList.Rally)]
        [TestCase("tightens four", "CoDriver", gameList.Rally)]
        [TestCase("tightens medium", "CoDriver", gameList.Rally)]
        [TestCase("tightens to medium", "CoDriver", gameList.Rally)]
        [TestCase("tightens to five", "CoDriver", gameList.Rally)]
        [TestCase("tightens five", "CoDriver", gameList.Rally)]
        [TestCase("tightens fast", "CoDriver", gameList.Rally)]
        [TestCase("tightens to fast", "CoDriver", gameList.Rally)]
        [TestCase("tightens to hairpin", "CoDriver", gameList.Rally)]
        [TestCase("tightens hairpin", "CoDriver", gameList.Rally)]
        [TestCase("cut", "CoDriver", gameList.Rally)]
        [TestCase("don't cut", "CoDriver", gameList.Rally)]
        [TestCase("tightens", "CoDriver", gameList.Rally)]
        [TestCase("tightens bad", "CoDriver", gameList.Rally)]
        [TestCase("widens", "CoDriver", gameList.Rally)]
        [TestCase("wide out", "CoDriver", gameList.Rally)]
        [TestCase("narrows", "CoDriver", gameList.Rally)]
        [TestCase("gate", "CoDriver", gameList.Rally)]
        [TestCase("through gate", "CoDriver", gameList.Rally)]
        [TestCase("opens", "CoDriver", gameList.Rally)]
        [TestCase("long", "CoDriver", gameList.Rally)]
        [TestCase("very long", "CoDriver", gameList.Rally)]
        [TestCase("bad camber", "CoDriver", gameList.Rally)]
        [TestCase("bridge", "CoDriver", gameList.Rally)]
        [TestCase("over bridge", "CoDriver", gameList.Rally)]
        [TestCase("ford", "CoDriver", gameList.Rally)]
        [TestCase("water splash", "CoDriver", gameList.Rally)]
        [TestCase("water crossing", "CoDriver", gameList.Rally)]
        [TestCase("crest", "CoDriver", gameList.Rally)]
        [TestCase("over crest", "CoDriver", gameList.Rally)]
        [TestCase("jump", "CoDriver", gameList.Rally)]
        [TestCase("over jump", "CoDriver", gameList.Rally)]
        [TestCase("big jump", "CoDriver", gameList.Rally)]
        [TestCase("over big jump", "CoDriver", gameList.Rally)]
        [TestCase("maybe", "CoDriver", gameList.Rally)]
        [TestCase("over junction", "CoDriver", gameList.Rally)]
        [TestCase("junction", "CoDriver", gameList.Rally)]
        [TestCase("past junction", "CoDriver", gameList.Rally)]
        [TestCase("through junction", "CoDriver", gameList.Rally)]
        [TestCase("caution", "CoDriver", gameList.Rally)]
        [TestCase("start stage reccy", "CoDriver", gameList.Rally)]
        [TestCase("finish stage reccy", "CoDriver", gameList.Rally)]
        //tbd [TestCase("end stage reccy", "CoDriver", gameList.Rally)]
        [TestCase("distance", "CoDriver", gameList.Rally)]
        [TestCase("onto tarmac", "CoDriver", gameList.Rally)]
        [TestCase("tarmac", "CoDriver", gameList.Rally)]
        [TestCase("onto gravel", "CoDriver", gameList.Rally)]
        [TestCase("gravel", "CoDriver", gameList.Rally)]
        [TestCase("onto snow", "CoDriver", gameList.Rally)]
        [TestCase("snow", "CoDriver", gameList.Rally)]
        [TestCase("slippy", "CoDriver", gameList.Rally)]
        [TestCase("slippery", "CoDriver", gameList.Rally)]
        [TestCase("onto concrete", "CoDriver", gameList.Rally)]
        [TestCase("concrete", "CoDriver", gameList.Rally)]
        [TestCase("ruts", "CoDriver", gameList.Rally)]
        [TestCase("deep ruts", "CoDriver", gameList.Rally)]
        [TestCase("tunnel", "CoDriver", gameList.Rally)]
        [TestCase("left entry chicane", "CoDriver", gameList.Rally)]
        [TestCase("right entry chicane", "CoDriver", gameList.Rally)]
        [TestCase("tightens then opens", "CoDriver", gameList.Rally)]
        [TestCase("opens then tightens", "CoDriver", gameList.Rally)]
        [TestCase("care", "CoDriver", gameList.Rally)]
        [TestCase("double caution", "CoDriver", gameList.Rally)]
        [TestCase("danger", "CoDriver", gameList.Rally)]
        [TestCase("triple caution", "CoDriver", gameList.Rally)]
        [TestCase("keep middle", "CoDriver", gameList.Rally)]
        [TestCase("keep center", "CoDriver", gameList.Rally)]
        [TestCase("keep left", "CoDriver", gameList.Rally)]
        [TestCase("keep right", "CoDriver", gameList.Rally)]
        [TestCase("keep in", "CoDriver", gameList.Rally)]
        [TestCase("keep out", "CoDriver", gameList.Rally)]
        [TestCase("bumps", "CoDriver", gameList.Rally)]
        [TestCase("bumpy", "CoDriver", gameList.Rally)]
        [TestCase("over rails", "CoDriver", gameList.Rally)]
        [TestCase("down hill", "CoDriver", gameList.Rally)]
        [TestCase("up hill", "CoDriver", gameList.Rally)]
        [TestCase("braking", "CoDriver", gameList.Rally)]
        [TestCase("brake", "CoDriver", gameList.Rally)]
        [TestCase("go straight", "CoDriver", gameList.Rally)]
        [TestCase("twisty", "CoDriver", gameList.Rally)]
        [TestCase("dip", "CoDriver", gameList.Rally)]
        [TestCase("plus", "CoDriver", gameList.Rally)]
        [TestCase("minus", "CoDriver", gameList.Rally)]
        [TestCase("log inside", "CoDriver", gameList.Rally)]
        [TestCase("logs inside", "CoDriver", gameList.Rally)]
        [TestCase("rock inside", "CoDriver", gameList.Rally)]
        [TestCase("rocks inside", "CoDriver", gameList.Rally)]
        [TestCase("tree inside", "CoDriver", gameList.Rally)]
        [TestCase("trees inside", "CoDriver", gameList.Rally)]
        [TestCase("log outside", "CoDriver", gameList.Rally)]
        [TestCase("logs outside", "CoDriver", gameList.Rally)]
        [TestCase("rock outside", "CoDriver", gameList.Rally)]
        [TestCase("rocks outside", "CoDriver", gameList.Rally)]
        [TestCase("tree outside", "CoDriver", gameList.Rally)]
        [TestCase("trees outside", "CoDriver", gameList.Rally)]
        [TestCase("onto loose gravel", "CoDriver", gameList.Rally)]
        [TestCase("loose gravel", "CoDriver", gameList.Rally)]


        [TestCase("crew chief volume down", "CommonActions")]
        [TestCase("crew chief volume up", "CommonActions")]
        [TestCase("game volume down", "CommonActions")]
        [TestCase("game volume up", "CommonActions")]
        [TestCase("voip volume down", "CommonActions")]
        [TestCase("voip volume up", "CommonActions")]
        #endregion testCases
        public void Test_voice_getEventForAllActionsSimple(string voiceMessage, string moduleThatHandlesIt, gameList gamesList = gameList.AllGames)
        {
            voiceMessage = voiceMessage.ToLower();
            CrewChief.eventsList.Clear();
            CrewChief.eventsList.Add("Timings", new Timings(audioPlayer));
            CrewChief.eventsList.Add("Position", new Position(audioPlayer));
            CrewChief.eventsList.Add("LapCounter", new LapCounter(audioPlayer, null));
            CrewChief.eventsList.Add("LapTimes", new LapTimes(audioPlayer));
            CrewChief.eventsList.Add("Penalties", new Penalties(audioPlayer));
            CrewChief.eventsList.Add("PitStops", new PitStops(audioPlayer));
            //GlobalBehaviourSettings.enabledMessageTypes.AddRange(GlobalBehaviourSettings.defaultEnabledMessageTypes);
            CrewChief.fuelMultiplier = new CrewChief.FuelMultiplier(CrewChief.gameDefinition.fuelMultiplierType);
            CrewChief.eventsList.Add("Fuel", new Fuel(audioPlayer));
            CrewChief.eventsList.Add("Battery", new Battery(audioPlayer));
            CrewChief.eventsList.Add("WatchedOpponents", new WatchedOpponents(audioPlayer));
            CrewChief.eventsList.Add("Strategy", new Strategy(audioPlayer));
            CrewChief.eventsList.Add("Opponents", new Opponents(audioPlayer));
            CrewChief.eventsList.Add("RaceTime", new RaceTime(audioPlayer));
            CrewChief.eventsList.Add("TyreMonitor", new TyreMonitor(audioPlayer));
            CrewChief.eventsList.Add("EngineMonitor", new EngineMonitor(audioPlayer));
            CrewChief.eventsList.Add("DamageReporting", new DamageReporting(audioPlayer));
            CrewChief.eventsList.Add("PushNow", new PushNow(audioPlayer));
            CrewChief.eventsList.Add("FlagsMonitor", new FlagsMonitor(audioPlayer));
            CrewChief.eventsList.Add("ConditionsMonitor", new ConditionsMonitor(audioPlayer));
            CrewChief.eventsList.Add("OvertakingAidsMonitor", new OvertakingAidsMonitor(audioPlayer));
            CrewChief.eventsList.Add("FrozenOrderMonitor", new FrozenOrderMonitor(audioPlayer));
            CrewChief.eventsList.Add("MulticlassWarnings", new MulticlassWarnings(audioPlayer));
            CrewChief.eventsList.Add("DriverSwaps", new DriverSwaps(audioPlayer));
            CrewChief.eventsList.Add("CommonActions", new CommonActions(audioPlayer));
            CrewChief.eventsList.Add("OverlayController", new OverlayController(audioPlayer));
            CrewChief.eventsList.Add("VROverlayController", new VROverlayController(audioPlayer));
            CrewChief.eventsList.Add("Mqtt", new Mqtt(audioPlayer));
            CrewChief.eventsList.Add("AlarmClock", new AlarmClock(audioPlayer));
            CrewChief.eventsList.Add("Ratings", new Ratings(audioPlayer));

            var gameEnums = gameListToEnums[gamesList];
            ConsoleCapture.Clear();
            foreach (var gameEnum in gameEnums)
            {
                if (CrewChief.eventsList.ContainsKey("IRacingBroadcastMessageEvent"))
                {
                    CrewChief.eventsList.Remove("IRacingBroadcastMessageEvent");
                }
                if (CrewChief.eventsList.ContainsKey("PitManagerVoiceCmds"))
                {
                    CrewChief.eventsList.Remove("PitManagerVoiceCmds");
                }
                if (CrewChief.eventsList.ContainsKey("CoDriver"))
                {
                    CrewChief.eventsList.Remove("CoDriver");
                }
                switch (gameEnum)
                {
                    case GameEnum.IRACING:
                        CrewChief.eventsList.Add("IRacingBroadcastMessageEvent", new IRacingBroadcastMessageEvent(audioPlayer));
                        break;

                    case GameEnum.RF2_64BIT:
                    case GameEnum.LMU:
                        CrewChief.eventsList.Add("PitManagerVoiceCmds", new PitManagerVoiceCmds(audioPlayer));
                        break;
                    case GameEnum.ASSETTO_64BIT_RALLY:
                    case GameEnum.DIRT:
                    case GameEnum.DIRT_2:
                    case GameEnum.RBR:
                        CrewChief.eventsList.Add("CoDriver", new CoDriver(audioPlayer));
                        break;
                }
                ConsoleCapture.Clear();
                CrewChief.gameDefinition.gameEnum = gameEnum;
                Game.game = CrewChief.gameDefinition.gameEnum;
                new PitManager();
                List<SpeechRecogniser.EventResult> eventResult = SpeechRecogniser.getEventsForAction(voiceMessage, CrewChief.eventsList);
                eventResult.Count.Should().NotBe(0, because: $"No event in {gameEnum.ToString()} to handle the voice message '{voiceMessage}'");
                //eventResult.Count.Should().Be(1, because: $"Expected exactly one event to handle the voice message '{voiceMessage}', but found {eventResult.Count}.");
                if (moduleThatHandlesIt != "xx")
                {
                    CrewChief.getEventName(eventResult[0].abstractEvent).Should().Match(moduleThatHandlesIt);
                }
                else
                {
                    Console.WriteLine($"Voice message '{voiceMessage}' handled by module '{CrewChief.getEventName(eventResult[0].abstractEvent)}' in {gameEnum.ToString()}");
                }

                eventResult[0].abstractEvent.clearState();
                eventResult[0].abstractEvent.trigger(currentGameState, currentGameState);
                eventResult[0].abstractEvent.respond(voiceMessage);
            }
        }
    }
}
