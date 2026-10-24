using System;
using System.Collections.Generic;

using CrewChiefV4.ACC;
using CrewChiefV4.Audio;
using CrewChiefV4.Events;
using CrewChiefV4.GameState;
using CrewChiefV4.R3E;

namespace CrewChiefV4.PitManager
{
    public class PitManagerVoiceCmds : AbstractEvent
    {
        #region Private Fields

        private static PitManager pmh;

        const float DEFAULT_FUEL = 10f; // Mainly some artificial minimum amount that keeps unit tests happy
        private static float fuelCapacity = DEFAULT_FUEL;
        private static float currentFuel = DEFAULT_FUEL;
        // In the car (in real time)
        private static bool inCar;

        public static Boolean tyresAutoCleared;

        #endregion Private Fields

        #region Public Constructors

        public PitManagerVoiceCmds(AudioPlayer audioPlayer)
        {
            pmh = new PitManager();
            this.audioPlayer = audioPlayer;
            fuelCapacity = DEFAULT_FUEL;
            currentFuel = DEFAULT_FUEL;
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// I think this is a list of the sessions when Pit Manager should be active.
        /// Player may want to use it in practice and qually
        /// </summary>
        public override List<SessionType> applicableSessionTypes
        {
            get
            {
                return new List<SessionType> {
                    SessionType.Practice,
                    SessionType.Qualify,
                    SessionType.PrivateQualify,
                    SessionType.Race,
                    SessionType.LonePractice };
            }
        }
        /// <summary>
        /// I think this is a list of the subset of phases of sessions when
        /// Pit Manager should be active.
        /// </summary>
        public override List<SessionPhase> applicableSessionPhases
        {
            get
            {
                return new List<SessionPhase> {
                    SessionPhase.Garage,
                    SessionPhase.Formation,
                    SessionPhase.Green,
                    SessionPhase.Countdown,
                    SessionPhase.FullCourseYellow };
            }
        }

        #endregion Public Properties

        #region Public Methods

        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            return PitManager.IsPitManagerCommand(voiceMessage) != null ? SpeechCommands.ID.PIT_STOP_FIX_NONE : SpeechCommands.ID.NO_COMMAND;
        }
        /// <summary>
        /// Respond to a voice command
        /// </summary>
        /// <param name="voiceMessage"></param>
        public override void respond(String voiceMessage)
        {
            respond(voiceMessage, HandlesEvent(voiceMessage));
        }
        public override void respond(String voiceMessage, SpeechCommands.ID Xcmd)
        {
            var cmd = PitManager.IsPitManagerCommand(voiceMessage);
            if (cmd != null)
            {
                if (inCar)
                {
                    Log.Debug(() => "Pit Manager voice command " + cmd.Item2.SpeechRecognitionPhrases[0]);
                    switch (CrewChief.gameDefinition.gameEnum)
                    {
                        case GameEnum.RF2_64BIT:
                        case GameEnum.LMU:
                            pmh.EventHandler(cmd.Item1, voiceMessage);
                            break;
                        case GameEnum.RACE_ROOM:
                            // Hacky, but it seems to work
                            if (audioPlayer != null) // Unittesting
                            {
                                R3EPitMenuManager.processVoiceCommand(voiceMessage, audioPlayer);
                            }
                            else
                            {
                                R3EPitMenuManager.processVoiceCommand(voiceMessage, MainWindow.instance.crewChief.speechRecogniser.crewChief.audioPlayer);
                            }
                            break;
                        case GameEnum.ACC:
                            // Hacky, but it seems to work
                            if (audioPlayer != null) // Unittesting
                            {
                                ACCPitMenuManager.processVoiceCommand(voiceMessage, audioPlayer);
                            }
                            else
                            {
                                ACCPitMenuManager.processVoiceCommand(voiceMessage, MainWindow.instance.crewChief.speechRecogniser.crewChief.audioPlayer);
                            }
                            break;
                    }
                }
                else
                {
                    PitManagerResponseHandlers.PMrh_CantDoThat(); // tbd
                    Log.Commentary("Not in car received Pit Manager voice command " + cmd.Item2.SpeechRecognitionPhrases[0]);
                }
            }
        }

        /// <summary>
        /// reinitialise any state held by the event subtype
        /// </summary>
        public override void clearState()
        {
            fuelCapacity = DEFAULT_FUEL;
            currentFuel = DEFAULT_FUEL;
            FuelVoiceCommand.Given = false;
            pmh.EventHandlerInit();
        }

        /// <summary>
        /// Cleardown the event subtype
        /// </summary>
        public override void teardownState()
        {
            fuelCapacity = DEFAULT_FUEL;
            currentFuel = DEFAULT_FUEL;
            pmh.EventHandler(PitManagerEvent.Teardown, "");
        }

        public static float getFuelCapacity()
        {
            return fuelCapacity;
        }
        public static float getCurrentFuel()
        {
            return currentFuel;
        }

        public static bool isOnTrack()
        {
            return inCar;
        }

        public static void startOfRace()
        {
            CrewChief.HandleEvent("PitManagerVoiceCmds","pitstop clear tyres");
            tyresAutoCleared = true;
    }
    #endregion Public Methods

    #region Protected Methods

    /// <summary>
    /// This is called on each 'tick' - the event subtype should
    /// place its logic in here including calls to audioPlayer.queueClip
    /// </summary>
    /// <param name="previousGameState"></param>
    /// <param name="currentGameState"></param>
    protected override void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            inCar = currentGameState.inCar;
            if (!previousGameState.inCar && currentGameState.inCar)
            {
                pmh.EventHandlerInit();
                Log.Debug(() => $"Car name {currentGameState.carName}");
                Log.Debug(() => $"Track name {currentGameState.trackName}");
            }

            fuelCapacity = currentGameState.FuelData.FuelCapacity;
            currentFuel = currentGameState.FuelData.FuelLeft;
            if (inCar
#pragma warning disable S2589
                && (previousGameState != null
#pragma warning restore S2589
                    && currentGameState.SessionData.SessionType == SessionType.Race
                    && currentGameState.SessionData.SessionRunningTime > 15
                    && !previousGameState.PitData.IsInGarage
                    && !currentGameState.PitData.JumpedToPits))
            {
                if (!previousGameState.PitData.InPitlane
                    && currentGameState.PitData.InPitlane)
                {
                    Log.Commentary("Entered pit lane");
                    if (UserSettings.GetUserSettings().getBoolean("rf2_enable_auto_fuel_to_end_of_race"))
                    {
                        if (FuelVoiceCommand.Given)
                        {
                            Log.Warning("'rF2 auto refuelling' ignored as a pitstop fuel voice command has been given");
                            FuelVoiceCommand.Given = false;  // auto refuel next pitstop
                        }
                        else
                        {
                            PitManagerEventHandlers_RF2.EH_FuelToEnd(null);
                        }
                    }
                    if(UserSettings.GetUserSettings().getBoolean("lmu_enable_auto_fuel_to_end_of_race"))
                    {
                        if (FuelVoiceCommand.Given)
                        {
                            Log.Warning("'LMU auto refuelling' ignored as a pitstop fuel voice command has been given");
                            FuelVoiceCommand.Given = false;  // auto refuel next pitstop
                        }
                        else
                        {
                            PitManagerEventHandlers_LMU.EH_FuelToEnd(null);
                        }
                    }
                }
                else if (previousGameState.PitData.InPitlane
                    && !currentGameState.PitData.InPitlane)
                {
                    Log.Commentary("Left pit lane");
                }
            }
        }

        #endregion Protected Methods
    }

    /// <summary>
    /// Utility class to handle pit number commands
    /// </summary>
    internal static class PitNumberHandling
    {
        #region Private Fields

        private static readonly CrewChief crewChief = MainWindow.instance != null ? MainWindow.instance.crewChief : null;

        #endregion Private Fields

        #region Public Methods

        /// <summary>
        /// Parse a non-zero number from the voice command
        /// </summary>
        /// <param name="_voiceMessage"></param>
        /// <returns>
        /// The number, 0 if the number couldn't be parsed
        /// </returns>
        public static int processNumber(string _voiceMessage)
        {
            float amount = NumberProcessing.SpokenNumberParser.Parse(_voiceMessage);

            if (amount < 1)
            {
                amount = 0;
                if (crewChief != null) crewChief.audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderDidntUnderstand, 0));
            }
            return (int)amount;
        }

        /// <summary>
        /// Parse the amount in litres by looking at the remainder of the voice
        /// command. Report the amount of fuel and the units
        /// </summary>
        /// <param name="amount"></param>
        /// The number
        /// <param name="_voiceMessage"></param>
        /// <returns>
        /// The number of litres
        /// </returns>
        public static int processLitresGallons(int amount, string _voiceMessage)
        {
            bool litres = true;
            if (SpeechRecogniser.ResultContains(_voiceMessage, SpeechRecogniser.LITERS))
            {
                litres = true;
            }
            else if (SpeechRecogniser.ResultContains(_voiceMessage, SpeechRecogniser.GALLONS))
            {
                litres = false;
            }
            else
            {
                Log.Commentary("Got fuel request with no unit, assuming " + (Fuel.fuelReportsInGallons ? " gallons" : "litres"));
                if (Fuel.fuelReportsInGallons)
                {
                    litres = false;
                }
            }

            if (litres)
            {
                if (crewChief != null) crewChief.audioPlayer.playMessageImmediately(new QueuedMessage(
                    "iracing_add_fuel", 0,  // tbd: rename
                    messageFragments: PitManagerVoiceCmds.MessageContents(
                        AudioPlayer.folderAcknowlegeOK,
                        amount,
                        amount == 1 ? Fuel.folderLitre : Fuel.folderLitres)
                    ));
            }
            else
            {
                if (crewChief != null) crewChief.audioPlayer.playMessageImmediately(new QueuedMessage(
                    "iracing_add_fuel", 0,
                    messageFragments: PitManagerVoiceCmds.MessageContents(
                        AudioPlayer.folderAcknowlegeOK,
                        amount,
                        amount == 1 ? Fuel.folderGallon : Fuel.folderGallons)
                    ));
                amount = Utilities.Conversions.convertGallonsToLitres(amount);
            }
            return amount;
        }

        #endregion Public Methods
    }

    public static class FuelVoiceCommand
    {
        /// <summary>
        /// Player has given a fuel voice command, don't auto-fuel
        /// </summary>
        public static bool Given { get; set; }
        /// <summary>
        /// Litres of fuel to be added at the pit stop
        /// </summary>
        static int _added = 0;
        public static int Added
        {
            get => _added;
            set
            {
                _added = value;
                Log.Fuel(() => $"{_added} litres of fuel to be added at the pit stop");
            }
        }
        /// <summary>
        /// Litres of fuel in the tank after the pit stop
        /// </summary>
        static int _level = 0;
        public static int Level {
            get => _level;
            set
            {
                _level = value;
                Log.Fuel(() => $"{_level} litres of fuel after the pit stop");
            }
        }
    }

    static class PitFuelling
    {
        #region Public Methods
        /// <summary>
        /// Calculate how much fuel is needed to get to the end of the race
        /// </summary>
        /// <param name="fuelCapacity"></param>
        /// <param name="currentFuel"></param>
        /// <param name="maxVE">100% Virtual Energy expressed as litres</param>
        /// <returns>
        /// +ve: Litres needed
        /// 0:   Enough fuel in car already
        /// -ve: Couldn't calculate fuel required
        /// </returns>
        public static (int litresNeeded, QueuedMessage message) fuelToEnd(float fuelCapacity, float currentFuel, float maxVE = Single.MaxValue)
        {
            QueuedMessage queuedMessage = null;
            int roundedLitresNeeded = -1;
            float additionalLitresNeeded = Fuel.GetAdditionalFuelToEndOfRace(true);

            if (additionalLitresNeeded == Fuel.NO_FUEL_DATA)
            {
                queuedMessage = new QueuedMessage(AudioPlayer.folderNoData, 0);
                Log.Fuel("Pit: couldn't calculate fuel needed");
                roundedLitresNeeded = -1;
            }
            else if (additionalLitresNeeded <= 0)
            {
                queuedMessage = new QueuedMessage(Fuel.folderPlentyOfFuel, 0);
                Log.Fuel("Pit: no fuel needed");
                roundedLitresNeeded = 0;
            }
            else if (additionalLitresNeeded > 0)
            {
                roundedLitresNeeded = (int)Math.Ceiling(additionalLitresNeeded);
                Log.Fuel(() => $"Pit: auto refuel to the end of the race, need {roundedLitresNeeded} litres of fuel");
                Log.Fuel(() => $"     including the {currentFuel} litres left in the tank");
                if (roundedLitresNeeded > fuelCapacity - currentFuel)
                {
                    // if we have a known fuel capacity and this is less than the calculated amount of fuel we need, warn about it.
                    queuedMessage = new QueuedMessage(Fuel.folderWillNeedToStopAgain, 0, secondsDelay: 4);
                    Log.Fuel(() => $"Pit: need {roundedLitresNeeded + currentFuel} but tank only holds {fuelCapacity} litres");
                    roundedLitresNeeded = (int)(fuelCapacity - currentFuel);
                }
                else if (roundedLitresNeeded > maxVE)
                {
                    // warn if effective VE is less than the calculated amount of fuel we need.
                    queuedMessage = new QueuedMessage(Fuel.folderWillNeedToStopAgain, 0, secondsDelay: 4);
                    Log.Fuel(() => $"Pit: need {roundedLitresNeeded + currentFuel} but that is > 100% Virtual Energy");
                    roundedLitresNeeded = (int)maxVE;
                }
                else
                {
                    queuedMessage = new QueuedMessage(AudioPlayer.folderFuelToEnd, 0);
                }
                FuelVoiceCommand.Added = (int)(roundedLitresNeeded - currentFuel);
                // FuelVoiceCommand.Level is set later, in SetFuelLevel
            }
            return (roundedLitresNeeded, queuedMessage);
        }

        #endregion Public Methods
    }
}
