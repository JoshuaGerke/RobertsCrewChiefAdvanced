using System;
using System.Collections.Generic;
using System.Linq;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;

using iRSDKSharp;

namespace CrewChiefV4.Events
{
    enum PressureUnit
    {
        PSI,
        KPA
    }

    class IRacingBroadcastMessageEvent : AbstractEvent
    {
        private PressureUnit pressureUnit = UserSettings.GetUserSettings().getBoolean("iracing_pit_tyre_pressure_in_psi") ? PressureUnit.PSI : PressureUnit.KPA;

        public static readonly Boolean autoFuelToEnd = UserSettings.GetUserSettings().getBoolean("iracing_enable_auto_fuel_to_end_of_race");

        private int lastColdFLPressure = -1;
        private int lastColdFRPressure = -1;
        private int lastColdRLPressure = -1;
        private int lastColdRRPressure = -1;

        private float fuelCapacity = -1;
        private float currentFuel = -1;

        public IRacingBroadcastMessageEvent(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
            this.lastColdFLPressure = -1;
            this.lastColdFRPressure = -1;
            this.lastColdRLPressure = -1;
            this.lastColdRRPressure = -1;
            this.fuelCapacity = -1;
            this.currentFuel = -1;
        }

        public override void clearState()
        {
            this.lastColdFLPressure = -1;
            this.lastColdFRPressure = -1;
            this.lastColdRLPressure = -1;
            this.lastColdRRPressure = -1;
            this.fuelCapacity = -1;
            this.currentFuel = -1;
        }

        public override List<SessionPhase> applicableSessionPhases
        {
            get { return new List<SessionPhase> { SessionPhase.Green, SessionPhase.Countdown, SessionPhase.FullCourseYellow }; }
        }

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            if (!Game.IRACING)
            {
                return;
            }

            lastColdFLPressure = (int)currentGameState.TyreData.FrontLeftPressure;
            lastColdFRPressure = (int)currentGameState.TyreData.FrontRightPressure;
            lastColdRLPressure = (int)currentGameState.TyreData.RearLeftPressure;
            lastColdRRPressure = (int)currentGameState.TyreData.RearRightPressure;

            fuelCapacity = currentGameState.FuelData.FuelCapacity;
            currentFuel = currentGameState.FuelData.FuelLeft;
            if (autoFuelToEnd)
            {
                if (previousGameState != null
                    // special case: don't allow auto fuelling to trigger if we've just been given control of the car. Prevents this
                    // triggering immediately after a driver change. We also don't want this to trigger when we're not in the car because
                    // the fuel data aren't sent.
                    //
                    // The above isn't quite enough - there's some noise in the data which results in autofuelling triggering sometimes
                    // when a driver enters the car after a driver swap. Because the trigger is supposed to happen as we enter the pitlane,
                    // also check for a sane car speed (a driver swap should be with the car stationary this should block an unwanted autofuel trigger)
                    && currentGameState.PositionAndMotionData.CarSpeed > 1
                    && currentGameState.ControlData.ControlType == ControlType.Player
                    && !(previousGameState.ControlData.ControlType == ControlType.Replay && currentGameState.ControlData.ControlType == ControlType.Player)
                    && !previousGameState.PitData.InPitlane && currentGameState.PitData.InPitlane
                    && currentGameState.SessionData.SessionType == SessionType.Race && currentGameState.SessionData.SessionRunningTime > 15
                    && !previousGameState.PitData.IsInGarage && !currentGameState.PitData.JumpedToPits)
                {
                    float litresNeededFull = Fuel.GetAdditionalFuelToEndOfRace(true);

                    if (litresNeededFull == Fuel.NO_FUEL_DATA)
                    {
                        // would be good to say what we don't have data about
                        audioPlayer.playMessage(new QueuedMessage(AudioPlayer.folderNoData, 0));
                    }
                    else if (litresNeededFull <= 0)
                    {
                        ClearFuel();
                        audioPlayer.playMessage(new QueuedMessage(Fuel.folderPlentyOfFuel, 0));
                    }
                    else if (litresNeededFull > 0)
                    {
                        float litresNeeded = litresNeededFull;
                        float litresNeededNoMargin = Fuel.GetAdditionalFuelToEndOfRace(false, verbose: false);
                        if (litresNeededNoMargin > fuelCapacity - currentFuel)
                        {
                            audioPlayer.playMessage(new QueuedMessage(Fuel.folderWillNeedToStopAgain, 0, secondsDelay: 4, abstractEvent: this));
                        }
                        else
                        {
                            audioPlayer.playMessage(new QueuedMessage(AudioPlayer.folderFuelToEnd, 0));
                        }

                        int roundedLitresNeeded = (int)Math.Ceiling(litresNeeded);
                        AddFuel(roundedLitresNeeded);
                        Console.WriteLine("Auto refuel to the end of the race, adding " + roundedLitresNeeded + " liters of fuel");
                    }
                }
            }
        }
        
        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.PIT_STOP_ADD,
            SpeechCommands.ID.PIT_STOP_CHANGE_ALL_TYRES,
            SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_LEFT_TYRE,
            SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_LEFT_TYRE_PRESSURE,
            SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_RIGHT_TYRE,
            SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_RIGHT_TYRE_PRESSURE,
            SpeechCommands.ID.PIT_STOP_CHANGE_REAR_LEFT_TYRE,
            SpeechCommands.ID.PIT_STOP_CHANGE_REAR_LEFT_TYRE_PRESSURE,
            SpeechCommands.ID.PIT_STOP_CHANGE_REAR_RIGHT_TYRE,
            SpeechCommands.ID.PIT_STOP_CHANGE_REAR_RIGHT_TYRE_PRESSURE,
            SpeechCommands.ID.PIT_STOP_CHANGE_TYRE_PRESSURE,
            SpeechCommands.ID.PIT_STOP_CLEAR_ALL,
            SpeechCommands.ID.PIT_STOP_CLEAR_FAST_REPAIR,
            SpeechCommands.ID.PIT_STOP_CLEAR_FUEL,
            SpeechCommands.ID.PIT_STOP_CLEAR_TYRES,
            SpeechCommands.ID.PIT_STOP_CLEAR_WIND_SCREEN,
            SpeechCommands.ID.PIT_STOP_FAST_REPAIR,
            SpeechCommands.ID.PIT_STOP_FUEL_TO_THE_END,
            SpeechCommands.ID.PIT_STOP_TEAROFF,
        };

        private static List<SpeechCommands.ID> OvalCommands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.PIT_STOP_CHANGE_LEFT_SIDE_TYRES,
            SpeechCommands.ID.PIT_STOP_CHANGE_RIGHT_SIDE_TYRES
        };
        private static List<SpeechCommands.ID> CircuitCommands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_TYRES,
            SpeechCommands.ID.PIT_STOP_CHANGE_REAR_TYRES
        };
        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            var cmd = SpeechCommands.SpeechToCommand(Commands, voiceMessage);
            if (cmd == SpeechCommands.ID.NO_COMMAND)
            {   // Checking for these separately improves the SRE outcome by
                // ensuring that RIGHT/REAR don't end up being detected incorrectly
                cmd = GlobalBehaviourSettings.useOvalLogic ? SpeechCommands.SpeechToCommand(OvalCommands, voiceMessage) :
                    SpeechCommands.SpeechToCommand(CircuitCommands, voiceMessage);
                if (cmd == SpeechCommands.ID.NO_COMMAND)
                {   // Checking for the other commands also so we can report unhandled commands.
                    cmd = !GlobalBehaviourSettings.useOvalLogic ? SpeechCommands.SpeechToCommand(OvalCommands, voiceMessage) :
                        SpeechCommands.SpeechToCommand(CircuitCommands, voiceMessage);
                }
            }
            return cmd;
        }

        public override void respond(String voiceMessage)
        {
            respond(voiceMessage, HandlesEvent(voiceMessage));
        }
        public override void respond(String voiceMessage, SpeechCommands.ID cmd)
        {
            switch (cmd)
            {
                case SpeechCommands.ID.PIT_STOP_ADD:
                    {
                        // Could use
                        // amount = PitNumberHandling.processNumber(voiceMessage);
                        // amount = PitNumberHandling.processLitresGallons(amount, voiceMessage);
                        // instead.
                        int amount = 0;
                        foreach (KeyValuePair<String[], int> entry in SpeechRecogniser.numberToNumber)
                        {
                            foreach (String numberStr in entry.Key)
                            {
                                if (voiceMessage.Contains(" " + numberStr + " "))
                                {
                                    amount = entry.Value;
                                    break;
                                }
                            }
                        }

                        if (amount == 0)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderDidntUnderstand, 0));
                            return;
                        }

                        if (SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.LITERS))
                        {
                            AddFuel(amount);
                            audioPlayer.playMessageImmediately(new QueuedMessage("iracing_add_fuel", 0,
                                messageFragments: MessageContents(AudioPlayer.folderAcknowlegeOK, amount, amount == 1 ? Fuel.folderLitre : Fuel.folderLitres)));
                        }
                        else if (SpeechRecogniser.ResultContains(voiceMessage, SpeechRecogniser.GALLONS))
                        {
                            AddFuel(Utilities.Conversions.convertGallonsToLitres(amount));
                            audioPlayer.playMessageImmediately(new QueuedMessage("iracing_add_fuel", 0,
                                messageFragments: MessageContents(AudioPlayer.folderAcknowlegeOK, amount, amount == 1 ? Fuel.folderGallon : Fuel.folderGallons)));
                        }
                        else
                        {
                            Console.WriteLine("Got fuel request with no unit, assuming " + (Fuel.fuelReportsInGallons ? " gallons" : "litres"));
                            if (!Fuel.fuelReportsInGallons)
                            {
                                AddFuel(amount);
                                audioPlayer.playMessageImmediately(new QueuedMessage("iracing_add_fuel", 0,
                                    messageFragments: MessageContents(AudioPlayer.folderAcknowlegeOK, amount, amount == 1 ? Fuel.folderLitre : Fuel.folderLitres)));
                            }
                            else
                            {
                                AddFuel(Utilities.Conversions.convertGallonsToLitres(amount));
                                audioPlayer.playMessageImmediately(new QueuedMessage("iracing_add_fuel", 0,
                                    messageFragments: MessageContents(AudioPlayer.folderAcknowlegeOK, amount, amount == 1 ? Fuel.folderGallon : Fuel.folderGallons)));
                            }
                        }

                        return;
                    }
                case SpeechCommands.ID.PIT_STOP_FUEL_TO_THE_END:
                    {
                        float litresNeeded = Fuel.GetAdditionalFuelToEndOfRace(true);

                        if (litresNeeded == Fuel.NO_FUEL_DATA)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderNoData, 0));
                        }
                        else if (litresNeeded <= 0)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(Fuel.folderPlentyOfFuel, 0));
                        }
                        else if (litresNeeded > 0)
                        {
                            int roundedLitresNeeded = (int)Math.Ceiling(litresNeeded);
                            AddFuel(roundedLitresNeeded);

                            // don't include the margin in this calculation
                            litresNeeded = Fuel.GetAdditionalFuelToEndOfRace(false, verbose: false);
                            if (litresNeeded > fuelCapacity - currentFuel)
                            {
                                // if we have a known fuel capacity and this is less than the calculated amount of fuel we need, warn about it.
                                audioPlayer.playMessage(new QueuedMessage(Fuel.folderWillNeedToStopAgain, 0, secondsDelay: 4, abstractEvent: this));
                            }
                            else
                            {
                                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderFuelToEnd, 0));
                            }
                            return;
                        }

                        break;
                    }
                case SpeechCommands.ID.PIT_STOP_TEAROFF:
                    Tearoff();
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_FAST_REPAIR:
                    FastRepair();
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CLEAR_ALL:
                    ClearAll();
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CLEAR_TYRES:
                    ClearTires();
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CLEAR_WIND_SCREEN:
                    ClearTearoff();
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CLEAR_FAST_REPAIR:
                    ClearFastRepair();
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CLEAR_FUEL:
                    ClearFuel();
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CHANGE_TYRE_PRESSURE:
                case SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_LEFT_TYRE_PRESSURE:
                case SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_RIGHT_TYRE_PRESSURE:
                case SpeechCommands.ID.PIT_STOP_CHANGE_REAR_LEFT_TYRE_PRESSURE:
                case SpeechCommands.ID.PIT_STOP_CHANGE_REAR_RIGHT_TYRE_PRESSURE:
                    {
                        int amount = 0;
                        foreach (KeyValuePair<String[], int> entry in SpeechRecogniser.numberToNumber)
                        {
                            foreach (String numberStr in entry.Key)
                            {
                                if (voiceMessage.Contains(" " + numberStr))
                                {
                                    amount = entry.Value;
                                    break;
                                }
                            }
                        }

                        if (amount == 0)
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderDidntUnderstand, 0));
                            return;
                        }
                        else
                        {
                            if (pressureUnit == PressureUnit.PSI)
                            {
                                amount = Utilities.Conversions.convertPSItoKPA(amount);
                            }

                            switch (cmd)
                            {
                                case SpeechCommands.ID.PIT_STOP_CHANGE_TYRE_PRESSURE:
                                    ChangeTire(PitCommandModeTypes.LF, amount);
                                    ChangeTire(PitCommandModeTypes.RF, amount);
                                    ChangeTire(PitCommandModeTypes.LR, amount);
                                    ChangeTire(PitCommandModeTypes.RR, amount);
                                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                                    return;
                                case SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_LEFT_TYRE_PRESSURE:
                                    ChangeTire(PitCommandModeTypes.LF, amount);
                                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                                    return;
                                case SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_RIGHT_TYRE_PRESSURE:
                                    ChangeTire(PitCommandModeTypes.RF, amount);
                                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                                    return;
                                case SpeechCommands.ID.PIT_STOP_CHANGE_REAR_LEFT_TYRE_PRESSURE:
                                    ChangeTire(PitCommandModeTypes.LR, amount);
                                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                                    return;
                                case SpeechCommands.ID.PIT_STOP_CHANGE_REAR_RIGHT_TYRE_PRESSURE:
                                    ChangeTire(PitCommandModeTypes.RR, amount);
                                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                                    return;
                            }
                        }

                        break;
                    }
                case SpeechCommands.ID.PIT_STOP_CHANGE_ALL_TYRES:
                    ChangeTire(PitCommandModeTypes.LF, 0);
                    ChangeTire(PitCommandModeTypes.RF, 0);
                    ChangeTire(PitCommandModeTypes.LR, 0);
                    ChangeTire(PitCommandModeTypes.RR, 0);
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_LEFT_TYRE:
                    ChangeTire(PitCommandModeTypes.LF, 0);
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_RIGHT_TYRE:
                    ChangeTire(PitCommandModeTypes.RF, 0);
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CHANGE_REAR_LEFT_TYRE:
                    ChangeTire(PitCommandModeTypes.LR, 0);
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CHANGE_REAR_RIGHT_TYRE:
                    ChangeTire(PitCommandModeTypes.RR, 0);
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    return;
                case SpeechCommands.ID.PIT_STOP_CHANGE_LEFT_SIDE_TYRES:
                    if (GlobalBehaviourSettings.useOvalLogic)
                    {
                        ClearTires();
                        ChangeTire(PitCommandModeTypes.LF, 0);
                        ChangeTire(PitCommandModeTypes.LR, 0);
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("mandatory_pit_stops/cant_change_those", 0));
                        Log.Commentary("Can only change left side tyres on ovals");
                    }
                    return;
                case SpeechCommands.ID.PIT_STOP_CHANGE_RIGHT_SIDE_TYRES:
                    if (GlobalBehaviourSettings.useOvalLogic)
                    {
                        ClearTires();
                        ChangeTire(PitCommandModeTypes.RF, 0);
                        ChangeTire(PitCommandModeTypes.RR, 0);
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("mandatory_pit_stops/cant_change_those", 0));
                        Log.Commentary("Can only change right side tyres on ovals");
                    }
                    return;
                case SpeechCommands.ID.PIT_STOP_CHANGE_FRONT_TYRES:
                    if (!GlobalBehaviourSettings.useOvalLogic)
                    {
                        ClearTires();
                        ChangeTire(PitCommandModeTypes.LF, 0);
                        ChangeTire(PitCommandModeTypes.RF, 0);
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("mandatory_pit_stops/cant_change_those", 0));
                        Log.Commentary("Can only change front tyres on road circuits");
                    }
                    return;
                case SpeechCommands.ID.PIT_STOP_CHANGE_REAR_TYRES:
                    if (!GlobalBehaviourSettings.useOvalLogic)
                    {
                        ClearTires();
                        ChangeTire(PitCommandModeTypes.LR, 0);
                        ChangeTire(PitCommandModeTypes.RR, 0);
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage("mandatory_pit_stops/cant_change_those", 0));
                        Log.Commentary("Can only change rear tyres on road circuits");
                    }
                    return;
            }
        }
        /// <summary>
        /// Schedule to add the specified amount of fuel (in liters) in the next pitstop.
        /// </summary>
        /// <param name="amount">The amount of fuel (in liters) to add. Use 0 to leave at current value.</param>
        public void AddFuel(int amount)
        {
            iRacingSDK.BroadcastMessage(BroadcastMessageTypes.PitCommand, (int)PitCommandModeTypes.Fuel, amount, 0);
        }

        private void ChangeTire(PitCommandModeTypes type, int pressure)
        {
            iRacingSDK.BroadcastMessage(BroadcastMessageTypes.PitCommand, (int)type, pressure);
        }

        /// <summary>
        /// Schedule to use a windshield tear-off in the next pitstop.
        /// </summary>
        public void Tearoff()
        {
            iRacingSDK.BroadcastMessage(BroadcastMessageTypes.PitCommand, (int)PitCommandModeTypes.WS, 0);
        }

        /// <summary>
        /// Schedule to use a fast repair in the next pitstop.
        /// </summary>
        public void FastRepair()
        {
            iRacingSDK.BroadcastMessage(BroadcastMessageTypes.PitCommand, (int)PitCommandModeTypes.FastRepair, 0);
        }

        /// <summary>
        /// Clear all pit commands.
        /// </summary>
        public static void ClearAll()
        {
            iRacingSDK.BroadcastMessage(BroadcastMessageTypes.PitCommand, (int)PitCommandModeTypes.Clear, 0);
        }

        /// <summary>
        /// Clear all tire changes.
        /// </summary>
        public void ClearTires()
        {
            iRacingSDK.BroadcastMessage(BroadcastMessageTypes.PitCommand, (int)PitCommandModeTypes.ClearTires, 0);
        }

        /// <summary>
        /// Clear tearoff.
        /// </summary>
        public void ClearTearoff()
        {
            iRacingSDK.BroadcastMessage(BroadcastMessageTypes.PitCommand, (int)PitCommandModeTypes.ClearWS, 0);
        }

        /// <summary>
        /// Clear fast repair.
        /// </summary>
        public void ClearFastRepair()
        {
            iRacingSDK.BroadcastMessage(BroadcastMessageTypes.PitCommand, (int)PitCommandModeTypes.ClearFR, 0);
        }

        /// <summary>
        /// Clear clear fuel.
        /// </summary>
        public void ClearFuel()
        {
            iRacingSDK.BroadcastMessage(BroadcastMessageTypes.PitCommand, (int)PitCommandModeTypes.ClearFuel, 0);
        }
    }
}
