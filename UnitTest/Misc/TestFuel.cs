using System;

using CrewChiefV4.Audio;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using CrewChiefV4;

using NUnit.Framework;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;
using static CrewChiefV4.Events.Fuel;

namespace UnitTest.Misc
{
    [TestFixture]
    /// Nothing to do with SpeechRecogniser changes but uses the
    /// same ConsoleOutput trick 
    public class TestFuel
    {
        private StringBuilder ConsoleOutput { get; set; } = new StringBuilder();

        [SetUp] // nunit
        public void Init()
        {
            Console.SetOut(new StringWriter(this.ConsoleOutput)); // Associate StringBuilder with StdOut
            this.ConsoleOutput.Clear(); // Clear text from any previous text runs
        }

        [Test]
        // Doesn't do anything, just to test that it's possible
        public void Test_fuel_trigger()
        {
            //CrewChief.eventsList.Clear();
            //AudioPlayer audioPlayer = new AudioPlayer();
            //CrewChief.eventsList.Add("Fuel", new Fuel(audioPlayer));
            CrewChief.gameDefinition.gameEnum = GameEnum.IRACING;
            GameStateData currentGameState = new GameStateData(20);
            currentGameState.FuelData.FuelUseActive = true;
            currentGameState.SessionData.SessionRunningTime = 16;
            currentGameState.SessionData.SessionType = SessionType.Race;
            currentGameState.SessionData.SessionPhase = SessionPhase.Green;
            currentGameState.SessionData.TrackDefinition = new TrackDefinition("Unit Test Track", 999.0f);
            // TrackDefinition sets this currentGameState.SessionData.TrackDefinition.trackLengthClass = TrackData.TrackLengthClass.MEDIUM;
            currentGameState.SessionData.SessionNumberOfLaps = 10;
            currentGameState.SessionData.SessionLapsRemaining = 2;
            currentGameState.FuelData.FuelLeft = 20;

            // or currentGameState.SessionData.SessionTotalRunTime = 300;
            //    currentGameState.SessionData.SessionTimeRemaining = 40;
            GameStateData pgs = new GameStateData(20);
            //CrewChief.eventsList["Fuel"].trigger(pgs, cgs);
            var fuel = new Fuel(new AudioPlayer());
            fuel.clearState();
            // Initialisation call (??)
            fuel.trigger(pgs, currentGameState);
            // First call
            fuel.trigger(pgs, currentGameState);
            // Next lap
            pgs = currentGameState;
            currentGameState.SessionData.SessionLapsRemaining = 1;
            currentGameState.FuelData.FuelLeft = 2;
            currentGameState.SessionData.IsNewLap = true;
            fuel.trigger(pgs, currentGameState);

            // Doesn't want to work
            Console.SetOut(new StreamWriter(Console.OpenStandardOutput()));
            Console.WriteLine(this.ConsoleOutput.ToString());
        }

        [Test]
        public void Test_fuel_trigger_data()
        {
            Log.setLogLevel(Log.LogType.Exception);
            string logFileName = @"..\..\misc\fuelLog.JSON";
            CrewChief.gameDefinition.gameEnum = GameEnum.RF2_64BIT;
            GameStateData currentGameState = new GameStateData(20);
            currentGameState.FuelData.FuelUseActive = true;
            currentGameState.SessionData.TrackDefinition = new TrackDefinition("Unit Test Track", 999.0f);
            // TrackDefinition sets this currentGameState.SessionData.TrackDefinition.trackLengthClass = TrackData.TrackLengthClass.MEDIUM;
            GameStateData pgs = new GameStateData(20);
            var fuel = new Fuel(new AudioPlayer());
            fuel.clearState();
            foreach (var log in ReadFuelLogsFromJsonFile(logFileName))
            {
                do1log(log);
                if (log.IsNewSession)
                {
                    fuel.clearState();
                }
            }
            void do1log(Fuel.FuelLog log)
            {
                pgs = currentGameState;
                currentGameState.SessionData.SessionLapsRemaining = log.SessionLapsRemaining;
                currentGameState.FuelData.FuelLeft = log.FuelLeft;
                currentGameState.SessionData.IsNewLap = log.IsNewLap;
                currentGameState.SessionData.SessionTimeRemaining = log.SessionTimeRemaining;
                currentGameState.SessionData.SessionRunningTime = log.SessionRunningTime;
                currentGameState.SessionData.SessionType = log.SessionType;
                currentGameState.SessionData.SessionPhase = log.SessionPhase;
                currentGameState.SessionData.SessionNumberOfLaps = log.SessionNumberOfLaps;
                currentGameState.SessionData.LapTimePrevious = log.LapTimePrevious;
                currentGameState.SessionData.PlayerLapTimeSessionBest = log.PlayerLapTimeSessionBest;
                currentGameState.SessionData.TrackDefinition.trackLengthClass = log.trackLengthClass;
                currentGameState.SessionData.TrackDefinition.trackLength = log.trackLength;
                currentGameState.SessionData.IsNewSession = log.IsNewSession;

                fuel.trigger(pgs, currentGameState);
            }
        }

        static IEnumerable<Fuel.FuelLog> ReadFuelLogsFromJsonFile(string filePath)
        {
            Log.Commentary($"Reading {Path.GetFullPath(filePath)}");
            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath);
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Can't find fuel log");
                lines = new string[0];
            }

            // Deserialize each line to a FuelLog and yield return it
            foreach (string line in lines)
            {
                Fuel.FuelLog fuelLog = JsonConvert.DeserializeObject<Fuel.FuelLog>(line);
                yield return fuelLog;
            }
        }
    }
}
