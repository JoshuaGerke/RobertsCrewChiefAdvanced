using System;
using System.Collections.Generic;
using System.Threading;

/// <summary>
/// A set of events that the driver or crew chief want
/// * Input from voice, button or CC(e.g.fuel calculation or Strategy Manager)
/// * Execution via game-specific functions
/// * Each has speech output.If the game doesn’t have the function then say so,
///   or if it’s not available (e.g.aero on non-aero car)
/// </summary>
namespace CrewChiefV4.PitManager
{
    using PME = PitManagerEvent;  // shorthand
    using SRE = SpeechRecogniser;

    /// <summary>
    /// All the events that Pit Manager handles
    /// Not all events can be handled by all games or all cars
    /// </summary>

    public enum PitManagerEvent
    {
        Initialise,
        Teardown,
        PrepareToUseMenu,

        // Fuel
        FuelAddXlitres,
        FuelFillToXlitres,
        FuelNone,
        FuelFillToEnd,
        Refuel,
        FuelRatio,
        VirtualEnergy,

        // Tyres
        TyreChangeAll,
        TyreChangeNone,
        TyreChangeFront,
        TyreChangeRear,
        TyreChangeLeft,
        TyreChangeRight,
        TyreChangeLF,
        TyreChangeRF,
        TyreChangeLR,
        TyreChangeRR,

        TyreCompoundDry,
        TyreCompoundHard,
        TyreCompoundMedium,
        TyreCompoundSoft,
        TyreCompoundUsedHard,
        TyreCompoundUsedMedium,
        TyreCompoundUsedSoft,
        TyreCompoundSupersoft,
        TyreCompoundUltrasoft,
        TyreCompoundHypersoft,
        TyreCompoundPrime,
        TyreCompoundOption,
        TyreCompoundAlternate,
        TyreCompoundIntermediate,
        TyreCompoundWet,
        TyreCompoundMonsoon,
        TyreCompoundNext,

        TyreSet,
        LeastUsedTyreSet,

        TyrePressure,
        TyrePressureFront,
        TyrePressureRear,
        TyrePressureLF,
        TyrePressureRF,
        TyrePressureLR,
        TyrePressureRR,

        // Repairs
        RepairFast,
        RepairNone,
        RepairAllAero,
        RepairAeroNone,
        RepairFrontAero,
        RepairRearAero,
        RepairRearNone,
        RepairSuspension,
        RepairSuspensionNone,
        RepairBody,
        RepairAll,

        // Penalties
        PenaltyServe,
        PenaltyServeNone,

        // Misc
        TearOff,
        TearOffNone,
        ClearAll,

        // Are these pit menu items?,
        HowManyIncidentPoints,
        WhatsTheIncidentLimit,
        WhatsMyIrating,
        WhatsMyLicenseClass,
        WhatsTheSof,
        WhatsThePitActions,

        // rF2 MFD
        DisplaySectors,
        DisplayPitMenu,
        DisplayTyres,
        DisplayTemps,
        DisplayRaceInfo,
        DisplayStandings,
        DisplayPenalties,
        DisplayNext,

        // TBD
        AeroFrontPlusMinusX,
        AeroRearPlusMinusX,
        AeroFrontSetToX,
        AeroRearSetToX,

        GrillePlusMinusX,
        GrilleSetToX,
        WedgePlusMinusX,
        WedgeSetToX,
        TrackBarPlusMinusX,
        TrackBarSetToX,
        RubberLF,
        RubberRF,
        RubberLR,
        RubberRR,
        FenderL,
        FenderR,
        FlipUpL,
        FlipUpR,
    }

    public class PitManager
    {
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        // This gets very messy but you don't need to worry about it
        // It allows each game to set up a dictionary PM_event_dict containing
        // PitManagerEvent, actionHandler fn, responseHandler fn, SpeechRecognitionPhrases
        // for all the events handled by the game.
        // There's probably a neater way of doing it but it's beyond my C# skills.
        public struct PitManagerEventTableEntry
        {
            public delegate bool PitManagerEventAction_Delegate(string voiceMessage);

            public delegate bool PitManagerEventResponse_Delegate();

            public PitManagerEventAction_Delegate PitManagerEventAction;
            public PitManagerEventResponse_Delegate PitManagerEventResponse;
            public String[] SpeechRecognitionPhrases;
        }

        internal class GamePitManagerDict : Dictionary<PME, PitManagerEventTableEntry>
        {
        }

        //-------------------------------------------------------------------------

        // Dictionary of games and their event dicts
        private readonly Dictionary<GameEnum, GamePitManagerDict>
            games_dict = new Dictionary<GameEnum, GamePitManagerDict>
        {
          {GameEnum.RF2_64BIT,  PitManagerEventHandlers_RF2.PM_event_dict_RF2},
          {GameEnum.IRACING,    PitManagerEventHandlers_iRacing.PM_event_dict_iRacing},
          {GameEnum.RACE_ROOM,  PitManagerEventHandlers_R3E.PM_event_dict_R3E},
          {GameEnum.ACC,        PitManagerEventHandlers_ACC.PM_event_dict_ACC},
          {GameEnum.LMU,        PitManagerEventHandlers_LMU.PM_event_dict_LMU}
        };

        private static readonly Object myLock = new Object();

        private static Thread executeThread;

        /// <summary>
        /// Used to initialise PM event handler the first time a command is
        /// issued in a session
        /// </summary>
        private static bool initialised;

        private static GamePitManagerDict PM_event_dict;
        public PitManager()
        {
            // Use the event dict for the current game
            if (games_dict.TryGetValue(CrewChief.gameDefinition.gameEnum, out GamePitManagerDict value))
            {
                PM_event_dict = value;
            }
            else
            {
                //TBD: default handler "Pit menu control is not available in this game"
                PM_event_dict = null;
                // This is now called from createListOfAllEvents() so don't report an error
                // Log.Error($"Pit menu control is not available in this game '{Game.game.ToString()}'");
            }
        }
        public void EventHandlerInit()
        {
            initialised = false;
        }

        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        /// The Event handler
        /// The event may come from a voice command, a command macro or
        /// internally (e.g."Fill to end" Property)
        /// </summary>
        /// <param name="ev">PitManagerEvent</param>
        /// <param name="voiceMessage"> The voice command which may contain
        /// more information (numbers etc.)</param>
        /// <returns>
        /// true if event was handled
        /// </returns>
        public bool EventHandler(PitManagerEvent ev, string voiceMessage)
        {
            bool result = false;
            if (PM_event_dict == null)
            {
                return result;
            }    

            lock (myLock)
            {
                // run this in a new thread as it may take a while to complete its work
                ThreadManager.UnregisterTemporaryThread(executeThread);
                executeThread = new Thread(() => { result = HandleEvent(ev, voiceMessage); });
                executeThread.Name = "PitManager.executeThread";
                ThreadManager.RegisterTemporaryThread(executeThread);
                executeThread.Start();
            }
            return result;
        }

        /// <summary>
        /// Execute the event. Called directly for unit tests or from a thread
        /// when CC is running.
        /// </summary>
        /// <param name="ev"></param>
        /// <param name="voiceMessage">The original command which may contain
        /// more information (numbers etc.)</param>
        /// <returns></returns>
        public bool HandleEvent(PitManagerEvent ev, string voiceMessage)
        {
            bool result = false;
            if (PM_event_dict.ContainsKey(ev))
            {
                try
                {
                    if (!initialised && ev != PME.Teardown)
                    {
                        initialised = PM_event_dict[PitManagerEvent.Initialise].PitManagerEventAction.Invoke("");
                    }
                    if (initialised)
                    {
                        PM_event_dict[PitManagerEvent.PrepareToUseMenu].PitManagerEventAction.Invoke("");
                        result = PM_event_dict[ev].PitManagerEventAction.Invoke(voiceMessage);
                        if (result)
                        {
                            result = PM_event_dict[ev].PitManagerEventResponse.Invoke();
                            if (PitManagerVoiceCmds.tyresAutoCleared)
                            {   // Pit menu tyre change cleared at start of race
                                PitManagerVoiceCmds.tyresAutoCleared = false;
                                // Restore the MFD
                                PM_event_dict[PME.DisplayRaceInfo].PitManagerEventAction.Invoke(voiceMessage);
                            }
                        }
                        else
                        {
                            //TBD: default handler "Couldn't do event for this vehicle"
                            // e.g. change aero on non-aero car, option not in menu,
                            // fuel a car
                            PitManagerResponseHandlers.PMrh_CantDoThat();
                            Log.Commentary($"Pit Manager couldn't do {ev} for this vehicle");
                        }
                    }
                    // else couldn't initialise or TearDown when not started up
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Pit Manager event error");
                }
            }
            else
            {
                //TBD: default handler "Not available in this game"
                PitManagerResponseHandlers.PMrh_CantDoThat();
                Log.Commentary($"Pit Manager couldn't do {ev} in this game");
                //Alternatively event dicts for all games have all events
                //and the response handler does the warning.
            }
            return result;
        }

        public static Tuple<PME, PitManagerEventTableEntry> IsPitManagerCommand(String voiceMessage)
        {
            if (PM_event_dict != null)
            {
                // Check the Pit commands
                foreach (var cmd in PM_event_dict)
                {
                    if (cmd.Value.SpeechRecognitionPhrases != null &&
                        SRE.ResultContains(voiceMessage, cmd.Value.SpeechRecognitionPhrases))
                    {
                        return Tuple.Create(cmd.Key, cmd.Value);
                    }
                }
            }
            return null;
        }

        public static List<string[]> GetPitManagerCommands()
        {
            List<string[]> result = new List<string[]>();
            if (PM_event_dict != null)
            {
                foreach (var cmd in PM_event_dict)
                {
                    if (cmd.Value.SpeechRecognitionPhrases != null)
                    {
                        result.Add(cmd.Value.SpeechRecognitionPhrases);
                    }
                }
            }
            return result;
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        // More messy stuff to set up the dictionary
        // Again, there's probably a neater way of doing it but it's beyond my C# skills.
        /// <summary>
        /// Helper fn to create GamePitManagerDict entry
        /// - "PM_event" : (actionHandler, responseHandler)
        /// </summary>
        /// <param name="existing"></param>
        /// <param name="actionHandler"></param>
        /// <param name="responseHandler"></param>
        /// <param name="SpeechRecognitionPhrases"></param>
        /// <returns></returns>
        public static PitManagerEventTableEntry _PMet(PitManagerEventTableEntry existing,
              PitManagerEventTableEntry.PitManagerEventAction_Delegate actionHandler,
              PitManagerEventTableEntry.PitManagerEventResponse_Delegate responseHandler,
              String[] SpeechRecognitionPhrases)
        {
            existing.PitManagerEventAction = actionHandler;
            existing.PitManagerEventResponse = responseHandler;
            existing.SpeechRecognitionPhrases = SpeechRecognitionPhrases;
            return existing;
        }

        /// <summary>
        /// Shorthand
        /// </summary>
        internal static PitManagerEventTableEntry _PMeh = new PitManagerEventTableEntry();
    }
}