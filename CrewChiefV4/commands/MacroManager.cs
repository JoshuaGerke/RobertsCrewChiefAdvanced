using CrewChiefV4.Audio;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using static CrewChiefV4.Utilities;

namespace CrewChiefV4.commands
{
    class MacroManager
    {
        // these are the macro names used to identify certain macros which have special hard-coded behaviours. Not ideal...
        public const String REQUEST_PIT_IDENTIFIER = "request pit";
        public const String CANCEL_REQUEST_PIT_IDENTIFIER = "cancel pit request";

        public const String MULTIPLE_PRESS_IDENTIFIER = "MULTIPLE";
        public const String FREE_TEXT_IDENTIFIER = "FREE_TEXT";
        public const String HOLD_TIME_IDENTIFIER = "HOLD_TIME";   // used to override the key hold time on a per-key basis
        public const String MULTIPLE_PRESS_FROM_VOICE_TRIGGER_IDENTIFIER = "VOICE_TRIGGER";
        public const String WAIT_IDENTIFIER = "WAIT";
        public const String APPLICABLE_SESSION_TYPES_IDENTIFIER = "SESSION_TYPES";
        public const String EXECUTE_DOS_COMMAND_IDENTIFIER = "DOS_COMMAND";
        public const String RF2_HWCONTROL_IDENTIFIER = "RF2_HWCONTROL";

        public static readonly Boolean enablePitExitPositionEstimates = UserSettings.GetUserSettings().getBoolean("enable_pit_exit_position_estimates");

        public static Boolean stopped = false;

        public static string GENERIC_MACRO_GAME_NAME = "ANY";

        // make all the macros available so the events can press buttons as they see fit:
        public static Dictionary<string, ExecutableCommandMacro> macros = new Dictionary<string, ExecutableCommandMacro>();

        public static Dictionary<string, ExecutableCommandMacro> voiceTriggeredMacros = new Dictionary<string, ExecutableCommandMacro>();

        public static int MAX_FUEL_RESET_COUNT = 150;

        public static void stop()
        {
            stopped = true;
            KeyPresser.releasePressedKey();
        }

        // converter 
        public static MacroContainer convertAssignmentToKey(MacroContainer macroContainer)
        {
            foreach(var assignment in macroContainer.assignments)
            {
                foreach(var keyBinding in assignment.keyBindings)
                {
                    foreach(var macro in macroContainer.macros)
                    {
                        foreach(CommandSet cs in macro.commandSets)
                        {
                            List<string> convertedActions = new List<string>();
                            foreach (var action in cs.actionSequence)
                            {
                                if(action.Contains(keyBinding.action))                                
                                {
                                    string convertesAction = action.Replace(keyBinding.action, keyBinding.key);
                                    convertedActions.Add(convertesAction);
                                }
                                else
                                {
                                    convertedActions.Add(action);
                                }
                            }
                            cs.actionSequence = convertedActions.ToArray();
                        }
                    }
                }
            }
            macroContainer.assignments = null;
            return macroContainer;
        }

        // checks if the game definition selected matches the game definition from the command set.
        public static bool isCommandSetForCurrentGame(string gameDefinitionFromCommandSet)
        {
            return gameDefinitionFromCommandSet != null &&
                ((gameDefinitionFromCommandSet.Equals(GENERIC_MACRO_GAME_NAME, StringComparison.InvariantCultureIgnoreCase) && !Game.NONE) ||
                  gameDefinitionFromCommandSet.Equals(CrewChief.gameDefinition.gameEnum.ToString(), StringComparison.InvariantCultureIgnoreCase));
        }

        // This is called immediately after initialising the speech recogniser in MainWindow
        public static void initialise(AudioPlayer audioPlayer, SpeechRecogniser speechRecogniser, ControllerConfiguration controllerConfiguration)
        {
            MacroManager.stopped = false;
            MacroManager.macros.Clear();
            MacroManager.voiceTriggeredMacros.Clear();
            if (UserSettings.GetUserSettings().getBoolean("enable_command_macros"))
            {
                // load the json:
                MacroContainer macroContainer = loadCommands(getMacrosFileLocation());
                MacroContainer defaultMacroContainer = loadCommands(getMacrosFileLocation(true));
                if (macroContainer.macros == null)
                {
                    macroContainer.macros = defaultMacroContainer.macros;
                }
                else
                {
                    if (mergeNewCommandSetsFromDefault(macroContainer, defaultMacroContainer))
                    {
                        saveCommands(macroContainer);
                    }
                }
                controllerConfiguration.clearMacroButtonAssignmments();
                // if it's valid, load the command sets:
                foreach (Macro macro in macroContainer.macros)
                {
                    Boolean hasCommandForCurrentGame = false;
                    // eagerly load the key bindings for each macro:
                    if (macro.commandSets != null)
                    {
                        foreach (CommandSet commandSet in macro.commandSets)
                        {
                            if (isCommandSetForCurrentGame(commandSet.gameDefinition))
                            {
                                // this does the conversion from key characters to key enums and stores the result to save us doing it every time
                                if (!commandSet.loadActionItems())
                                {
                                    Console.WriteLine("Macro \"" + macro.name + "\" failed to load - some actionItems didn't parse succesfully");
                                }
                                else
                                {
                                    hasCommandForCurrentGame = true;
                                }
                                break;
                            }
                        }
                    }
                    if (hasCommandForCurrentGame)
                    {
                        // make this macro globally visible:
                        ExecutableCommandMacro commandMacro = new ExecutableCommandMacro(audioPlayer, macro);
                        macros.Add(macro.name, commandMacro);
                        // if there's a voice command, load it into the recogniser:
                        if (macro.voiceTriggers != null && macro.voiceTriggers.Length > 0)
                        {
                            foreach (String voiceTrigger in macro.voiceTriggers)
                            {
                                if (voiceTriggeredMacros.ContainsKey(voiceTrigger))
                                {
                                    Console.WriteLine("Voice trigger " + voiceTrigger + " has already been allocated to a different command");
                                }
                                else
                                {
                                    voiceTriggeredMacros.Add(voiceTrigger, commandMacro);
                                }
                            }
                        }
                        else if (macro.integerVariableVoiceTrigger != null && macro.integerVariableVoiceTrigger.Length > 0)
                        {
                            if (voiceTriggeredMacros.ContainsKey(macro.integerVariableVoiceTrigger))
                            {
                                Console.WriteLine("Voice trigger " + macro.integerVariableVoiceTrigger + " has already been allocated to a different command");
                            }
                            else
                            {
                                voiceTriggeredMacros.Add(macro.integerVariableVoiceTrigger, commandMacro);
                            }
                        }
                        if (macro.buttonTriggers != null && macro.buttonTriggers.Length > 0)
                        {
                            // load each button assignment
                            foreach (ButtonTrigger buttonTrigger in macro.buttonTriggers)
                            {
                                ControllerConfiguration.ButtonAssignment buttonAssignment = new ControllerConfiguration.ButtonAssignment();
                                buttonAssignment.executableCommandMacro = commandMacro;
                                buttonAssignment.buttonIndex = buttonTrigger.buttonIndex;
                                buttonAssignment.deviceGuid = buttonTrigger.deviceId;
                                controllerConfiguration.addControllerObjectToButtonAssignment(buttonAssignment);
                                controllerConfiguration.addMacroButtonAssignment(buttonAssignment);
                                controllerConfiguration.addControllerIfNecessary(buttonTrigger.description, buttonTrigger.deviceId);
                            }
                        }
                    }
                }
            }
            else
            {
                Console.WriteLine("Command macros are disabled");
            }
        }

        // users may remove a macro from their own macros file - at this point we don't want to copy missing macro
        // from the default to the user's file. However, we may add a game-specific command set to a default macro.
        // In this case we want to copy that command set to the user's version if he hasn't removed that macro.
        private static Boolean mergeNewCommandSetsFromDefault(MacroContainer userMacroContainer, MacroContainer defaultMacroContainer)
        {
            Boolean addedAny = false;

            // before adding any missing command sets to the user macros, check for cases where multiple macros have the same name.
            // There's currently only one of these ("Get out of car") - we don't want to modify these macros because they may have been
            // configured to have a single command set per game. Not exactly what was intended but will work - if we add command sets to
            // these it'll make the duplication much worse
            HashSet<string> macroNames = new HashSet<string>();
            HashSet<string> repeatedMacros = new HashSet<string>();
            foreach (var userMacro in userMacroContainer.macros)
            {
                if (macroNames.Contains(userMacro.name))
                {
                    repeatedMacros.Add(userMacro.name);
                }
                macroNames.Add(userMacro.name);
            }
            foreach (var userMacro in userMacroContainer.macros)
            {
                if (repeatedMacros.Contains(userMacro.name))
                {
                    continue;
                }
                Boolean added = false;
                HashSet<String> userMacroGameDefinitions = new HashSet<String>();
                // temporary list to which we'll add missing command sets:
                List<CommandSet> userMacroCommandSetsList = new List<CommandSet>();
                if (userMacro.commandSets != null)
                {
                    foreach (var userMacroCommandSet in userMacro.commandSets)
                    {
                        userMacroGameDefinitions.Add(userMacroCommandSet.gameDefinition);
                        userMacroCommandSetsList.Add(userMacroCommandSet);
                    }
                }
                foreach (var defaultMacro in defaultMacroContainer.macros)
                {
                    if (userMacro.name == defaultMacro.name)
                    {
                        if (defaultMacro.commandSets != null)
                        {
                            foreach (var defaultMacroCommandSet in defaultMacro.commandSets)
                            {
                                if (!userMacroGameDefinitions.Contains(defaultMacroCommandSet.gameDefinition))
                                {
                                    // this macro exists in the user set and the default set, but the default set
                                    // has a CommandSet for a game that's not in the user's set - add it
                                    addedAny = true;
                                    added = true;
                                    userMacroCommandSetsList.Add(defaultMacroCommandSet);
                                }
                            }
                        }
                        break;
                    }
                }
                if (added)
                {
                    // we've added a command set from the default to this user macro (or temporary list)
                    userMacro.commandSets = userMacroCommandSetsList.ToArray();
                }
            }
            return addedAny;
        }

        // file loading boilerplate - needs refactoring
        public static MacroContainer loadCommands(String filename)
        {
            if (filename != null)
            {
                try
                {                    
                    MacroContainer macroContainer = JsonConvert.DeserializeObject<MacroContainer>(getFileContents(filename));
                    // Conver any existing user created command macros to new "assignment" format
                    if (macroContainer != null)
                    {
                        if (macroContainer.assignments != null)
                        {
                            macroContainer = convertAssignmentToKey(macroContainer);
                            saveCommands(macroContainer);
                        }
                        return macroContainer;
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Error pasing " + filename + ": ");
                }
            }
            return new MacroContainer();
        }

        public static void saveCommands(MacroContainer macroContainer)
        {
            String fileName = DataFiles.saved_command_macros;
            if (DataFiles.MakeFolders() == null)
            {
                JsonFiles.WriteFile(fileName, macroContainer);
            }         
        }

        private static String getFileContents(String fullFilePath)
        {
            StringBuilder jsonString = new StringBuilder();
            StreamReader file = null;
            try
            {
                file = new StreamReader(fullFilePath);
                String line;
                while ((line = file.ReadLine()) != null)
                {
                    if (!line.Trim().StartsWith("#"))
                    {
                        jsonString.AppendLine(line);
                    }
                }
                return jsonString.ToString();
            }
            catch (Exception e)
            {
                Log.Exception(e, "Error reading file " + fullFilePath + ": ");
            }
            finally
            {
                if (file != null)
                {
                    file.Close();
                }
            }
            return null;
        }

        public static String getMacrosFileLocation(bool forceDefault = false)
        {
            return DataFiles.GetFileLocation(DataFiles.saved_command_macros,
                DataFiles.default_saved_command_macros,
                "command macros",
                forceDefault);
        }
    }
}
