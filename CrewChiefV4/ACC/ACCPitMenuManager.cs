using CrewChiefV4.Audio;
using CrewChiefV4.commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WindowsInput.Native;

namespace CrewChiefV4.ACC
{
    // functions to fart around with ACC's pit menu
    class ACCPitMenuManager
    {
        // hard-coded keys to fall back on if we don't find the menu navigation macro definitions
        private static VirtualKeyCode upKey = VirtualKeyCode.UP;
        private static VirtualKeyCode downKey = VirtualKeyCode.DOWN;
        private static VirtualKeyCode leftKey = VirtualKeyCode.LEFT;
        private static VirtualKeyCode rightKey = VirtualKeyCode.RIGHT;
        private static VirtualKeyCode pitMenuKey = VirtualKeyCode.VK_P;

        // lazily initialised
        private static ExecutableCommandMacro pitMenuMacro;
        private static ExecutableCommandMacro closePitMenuMacro;
        private static ExecutableCommandMacro menuUpMacro;
        private static ExecutableCommandMacro menuDownMacro;
        private static ExecutableCommandMacro menuRightMacro;
        private static ExecutableCommandMacro menuLeftMacro;

        private const String PIT_MENU_MACRO_NAME = "show acc pit menu";
        private const String CLOSE_PIT_MENU_MACRO_NAME = "close acc pit menu";
        private const String PIT_MENU_UP_MACRO_NAME = "pit menu up";
        private const String PIT_MENU_DOWN_MACRO_NAME = "pit menu down";
        private const String PIT_MENU_RIGHT_MACRO_NAME = "pit menu right";
        private const String PIT_MENU_LEFT_MACRO_NAME = "pit menu left";

        private const string folderConfirmChangeTyres = "mandatory_pit_stops/confirm_change_tyres";
        private const string folderConfirmWetTyres = "mandatory_pit_stops/confirm_wet_tyres";
        private const string folderConfirmDryTyres = "mandatory_pit_stops/confirm_dry_tyres";
        private const string folderConfirmNoTyres = "mandatory_pit_stops/confirm_change_no_tyres";
        private const string folderConfirmNoRefuelling = "mandatory_pit_stops/confirm_no_refuelling";

        private const string folderCantDoThat= "mandatory_pit_stops/cant_do_that";

        public static void processVoiceCommand(string recognisedText, AudioPlayer audioPlayer, bool allowDidntUnderstandResponse = true)
        {
            // usually, the pit menu goes 'limiter', 'strategy', 'fuel to add', 'change tyres', 'change brakes', 'suspension', 'bodywork'.
            // Sometimes 'strategy' is not in the list.
            // If 'change tyres' is selected another set of items are inserted between it and 'change brakes' - 'tyre set', 'compound', 'pressures', 
            // 'LF pressure', 'RF pressure', 'LR pressure', 'RR pressure'.
            // If 'change brakes' is selected 2 items are inserted between it and 'suspension' - 'front brake' and 'rear brake'.
            // If compound is wet the tyre set item is greyed out and pressing 'down' skips over it.
            //
            // Some items wrap - cursor location, toggles, tyre set, selected brakes. Others don't - compound, pressure, fuel to add.
            // Pressure varies between 20.4 and 35 (but this my be class dependent).
            //
            // All this means that the correct number of presses to get to an item below 'change tyres' can vary in awkward ways.
            // Fortunately tyre-sets is available in all sessions, even if it's unlimited (if and only if change tyres is selected and the tyre set is dry).
            // Changing tyre-set in the menu updates the corresponding value in shared memory, so we use this to orient ourselves in the
            // menu and get it into a known state before making changes. The logic is too complex for macros, so this (like the R3E menu manager)
            // is a bunch of hard-coded nasty tightly coupled to the quirks of the pit menu
            bool recognised = false;
            if (CrewChief.currentGameState == null)
            {
                Console.WriteLine("Can't send ACC pit menu command because we have no game state");
            }
            else
            {
                if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_CHANGE_TYRES))
                {
                    mashKeysToPutPitMenuInKnownState();
                    recognised = true;
                    audioPlayer.playMessageImmediately(new QueuedMessage(folderConfirmChangeTyres, 0));
                }
                if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_DRY_TYRES))
                {
                    mashKeysToPutPitMenuInKnownState();
                    recognised = true;
                    audioPlayer.playMessageImmediately(new QueuedMessage(folderConfirmDryTyres, 0));
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_CLEAR_TYRES))
                {
                    dontChangeTyres();
                    recognised = true;
                    audioPlayer.playMessageImmediately(new QueuedMessage(folderConfirmNoTyres, 0));
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_WET_TYRES))
                {
                    selectWets();
                    recognised = true;
                    audioPlayer.playMessageImmediately(new QueuedMessage(folderConfirmWetTyres, 0));
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_SELECT_TYRE_SET))
                {
                    foreach (string command in SpeechRecogniser.PIT_STOP_SELECT_TYRE_SET)
                    {
                        if (recognisedText.StartsWith(command))
                        {
                            recognisedText = recognisedText.Substring(command.Length);
                            break;
                        }
                    }
                    int requestedSet = extractInt(recognisedText) - 1;
                    if (requestedSet >= 0 && requestedSet < 50)
                    {
                        mashKeysToPutPitMenuInKnownState(false);
                        if (selectTyreSet(requestedSet, getAvailableTyreSetsCount()))
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                        }
                        else
                        {
                            audioPlayer.playMessageImmediately(new QueuedMessage(folderCantDoThat, 0));
                        }
                        recognised = true;
                    }
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_SELECT_LEAST_USED_TYRE_SET))
                {
                    mashKeysToPutPitMenuInKnownState(false);
                    if (selectLeastUsedTyreSet(getAvailableTyreSetsCount()))
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    }
                    else
                    {
                        audioPlayer.playMessageImmediately(new QueuedMessage(folderCantDoThat, 0));
                    }
                    recognised = true;
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_DONT_REFUEL))
                {
                    clearFuel();
                    recognised = true;
                    audioPlayer.playMessageImmediately(new QueuedMessage(folderConfirmNoRefuelling, 0));
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_CHANGE_TYRE_PRESSURE))
                {
                    foreach (string command in SpeechRecogniser.PIT_STOP_CHANGE_TYRE_PRESSURE)
                    {
                        if (recognisedText.StartsWith(command))
                        {
                            recognisedText = recognisedText.Substring(command.Length);
                            break;
                        }
                    }
                    changeAllPressuresTo(parsePressureRequest(recognisedText));
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    recognised = true;
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_CHANGE_FRONT_PRESSURES))
                {
                    foreach (string command in SpeechRecogniser.PIT_STOP_CHANGE_FRONT_PRESSURES)
                    {
                        if (recognisedText.StartsWith(command))
                        {
                            recognisedText = recognisedText.Substring(command.Length);
                            break;
                        }
                    }
                    changeFrontPressuresTo(parsePressureRequest(recognisedText));
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    recognised = true;
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_CHANGE_REAR_PRESSURES))
                {
                    foreach (string command in SpeechRecogniser.PIT_STOP_CHANGE_REAR_PRESSURES)
                    {
                        if (recognisedText.StartsWith(command))
                        {
                            recognisedText = recognisedText.Substring(command.Length);
                            break;
                        }
                    }
                    changeRearPressuresTo(parsePressureRequest(recognisedText));
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    recognised = true;
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_CHANGE_FRONT_LEFT_TYRE_PRESSURE))
                {
                    foreach (string command in SpeechRecogniser.PIT_STOP_CHANGE_FRONT_LEFT_TYRE_PRESSURE)
                    {
                        if (recognisedText.StartsWith(command))
                        {
                            recognisedText = recognisedText.Substring(command.Length);
                            break;
                        }
                    }
                    changeLFPressuresTo(parsePressureRequest(recognisedText));
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    recognised = true;
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_CHANGE_FRONT_RIGHT_TYRE_PRESSURE))
                {
                    foreach (string command in SpeechRecogniser.PIT_STOP_CHANGE_FRONT_RIGHT_TYRE_PRESSURE)
                    {
                        if (recognisedText.StartsWith(command))
                        {
                            recognisedText = recognisedText.Substring(command.Length);
                            break;
                        }
                    }
                    changeRFPressuresTo(parsePressureRequest(recognisedText));
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    recognised = true;
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_CHANGE_REAR_LEFT_TYRE_PRESSURE))
                {
                    foreach (string command in SpeechRecogniser.PIT_STOP_CHANGE_REAR_LEFT_TYRE_PRESSURE)
                    {
                        if (recognisedText.StartsWith(command))
                        {
                            recognisedText = recognisedText.Substring(command.Length);
                            break;
                        }
                    }
                    changeLRPressuresTo(parsePressureRequest(recognisedText));
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    recognised = true;
                }
                else if (SpeechRecogniser.ResultContains(recognisedText, SpeechRecogniser.PIT_STOP_CHANGE_REAR_RIGHT_TYRE_PRESSURE))
                {
                    foreach (string command in SpeechRecogniser.PIT_STOP_CHANGE_REAR_RIGHT_TYRE_PRESSURE)
                    {
                        if (recognisedText.StartsWith(command))
                        {
                            recognisedText = recognisedText.Substring(command.Length);
                            break;
                        }
                    }
                    changeRRPressuresTo(parsePressureRequest(recognisedText));
                    audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderAcknowlegeOK, 0));
                    recognised = true;
                }
                // TODO: wait a few seconds and close the pit menu here? We can't know if it was open when the command was made
                // so this might be annoying
            }
            if (!recognised && allowDidntUnderstandResponse)
            {
                audioPlayer.playMessageImmediately(new QueuedMessage(AudioPlayer.folderDidntUnderstand, 0));
            }
        }

        private static bool isStrategyOptionShown()
        {
            // maybe get this value from some clue in shared memory, for now just assume it's show.
            return true;
        }

        private static float parsePressureRequest(string phrase)
        {
            float parsedAmount;
            string[] beforeAndAfterPoint = phrase.Split(new String[] { SpeechRecogniser.POINT[0] }, StringSplitOptions.None);
            if (beforeAndAfterPoint.Length == 2)
            {
                parsedAmount = float.Parse(extractInt(beforeAndAfterPoint[0]) + "." + extractInt(beforeAndAfterPoint[1]));
            }
            else
            {
                parsedAmount =(float) extractInt(beforeAndAfterPoint[0]);
            }
            Console.WriteLine("Parsed pressure message quantity from " + phrase + " to " + parsedAmount);
            return parsedAmount;
        }

        private static int extractInt(String commandFragment)
        {
            foreach (KeyValuePair<String[], int> entry in SpeechRecogniser.numberToNumber)
            {
                foreach (String numberStr in entry.Key)
                {
                    if (commandFragment.Trim() == numberStr)
                    {
                        return entry.Value;
                    }
                }
            }
            return 0;
        }

        private static void mashKeysToPutPitMenuInKnownState(bool returnToTop = true)
        {
            // keep track of this
            int currentSelectedTyreSet = CrewChief.currentGameState.TyreData.selectedSet;
            // mash keys until this changes
            bool gotMenuInKnownState = false;
            // go to the pit menu to put the cursor at the top
            moveCursorToTopOfPitMenu();

            // now go down 3 or 4 times and press right. If change tyres is selected this will change the selected tyre set
            int itemsToReachTyreSets = isStrategyOptionShown() ? 4 : 3;
            for (int i = 0; i < itemsToReachTyreSets; i++)
            {
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            }
            sendKeyPressOrMacro(getMenuRightMacro(), rightKey);
            // now wait a moment to ensure we have a new game update
            Thread.Sleep(CrewChief.timeInterval * 2);
            if (currentSelectedTyreSet != CrewChief.currentGameState.TyreData.selectedSet)
            {
                gotMenuInKnownState = true;
                // yay, we know where we are
            }
            else
            {
                // 2 possibilities here. Either we don't have the change-tyres checkbox selected and we've just 
                // selected 'change brakes', or we do have change-tyres selected and we're on the tyre set option
                // but the game's ignoring us

                // on the happy path, we either changed the brakes checkbox or the tyre type. A left press will either
                // reset the brake option or select dry tyres
                sendKeyPressOrMacro(getMenuLeftMacro(), leftKey);

                // now go up - we'll either be on tyre set or change tyres
                sendKeyPressOrMacro(getMenuUpMacro(), upKey);
                sendKeyPressOrMacro(getMenuRightMacro(), rightKey);
                // now see if that change tyres
                Thread.Sleep(CrewChief.timeInterval * 2);
                if (currentSelectedTyreSet != CrewChief.currentGameState.TyreData.selectedSet)
                {
                    gotMenuInKnownState = true;
                    // yay, we know where we are
                }
                else
                {
                    // this last press must have enabled tyre change. We're on drys at this point so down-and-right must be what we want
                    sendKeyPressOrMacro(getMenuDownMacro(), downKey);
                    sendKeyPressOrMacro(getMenuRightMacro(), rightKey);
                    Thread.Sleep(CrewChief.timeInterval * 2);
                    if (currentSelectedTyreSet != CrewChief.currentGameState.TyreData.selectedSet)
                    {
                        gotMenuInKnownState = true;
                        // yay, we know where we are
                    }
                    else
                    {
                        // one more possibility, we're now on the tyre type option with wets selected - change to dry and try again
                        sendKeyPressOrMacro(getMenuLeftMacro(), leftKey);
                        sendKeyPressOrMacro(getMenuUpMacro(), upKey);
                        sendKeyPressOrMacro(getMenuRightMacro(), rightKey);
                        Thread.Sleep(CrewChief.timeInterval * 2);
                        if (currentSelectedTyreSet != CrewChief.currentGameState.TyreData.selectedSet)
                        {
                            gotMenuInKnownState = true;
                            // yay, we know where we are
                        }
                    }
                }
            }
            if (gotMenuInKnownState)
            {
                // put the tyre set back to where it was
                sendKeyPressOrMacro(getMenuLeftMacro(), leftKey);
                // put the cursor back to the top
                if (returnToTop)
                {
                    moveCursorToTopOfPitMenu();
                }
            }
        }

        private static void sendKeyPressOrMacro(ExecutableCommandMacro macro, VirtualKeyCode fallbackKeyCode, int sleepTime = 200)
        {
            if (macro != null)
            {
                // suppress macro confirmation messages, and run the macro on the caller's thread:
                macro.execute(null, true, false);
            }
            else
            {
                // we didn't find a required key press macro so just press the most likely key anyway
                KeyPresser.SendKeyPress(new Tuple<VirtualKeyCode?, VirtualKeyCode>(null, fallbackKeyCode), 100);
            }
            Thread.Sleep(sleepTime);
        }

        private static void moveCursorToTopOfPitMenu()
        {
            sendKeyPressOrMacro(getPitMenuMacro(), pitMenuKey);
            // additional pause - sometimes this specific key is ignored or it takes a while to complete the action
            Thread.Sleep(300);
        }
 
        // assumes we're already on the available tyres sets option
        private static bool selectTyreSet(int requestedTyreSet, int totalAvailableSets)
        {
            // TODO: the currently fitted tyre set isn't available to be selected. We don't know what the currently fitted set is,
            // so we need to be quite careful here
            if (requestedTyreSet <= totalAvailableSets)
            {
                int currentTyreSet = CrewChief.currentGameState.TyreData.selectedSet;
                if (requestedTyreSet == currentTyreSet)
                {
                    Console.WriteLine("Tyre set " + (requestedTyreSet + 1) + " is already selected");
                    return true;
                }
                bool increase = requestedTyreSet > currentTyreSet;
                string directionString = increase ? "right" : "left";
                int presses = Math.Abs(requestedTyreSet - currentTyreSet);
                Console.WriteLine("Selecting set " + (requestedTyreSet + 1) + ", current set = " + (currentTyreSet + 1) + " sending " + presses + " " + directionString + " presses");
                for (int i = 0; i < presses; i++)
                {
                    if (increase)
                    {
                        sendKeyPressOrMacro(getMenuRightMacro(), rightKey);
                    }
                    else
                    {
                        sendKeyPressOrMacro(getMenuLeftMacro(), leftKey);
                    }
                }
                return true;
            }
            Console.WriteLine("Requested tyre set " + (requestedTyreSet + 1) + " but only " + (totalAvailableSets + 1) + " are available");
            return false;
        }
 
        // assumes we're already on the available tyres sets option
        private static bool selectLeastUsedTyreSet(int totalAvailableSets)
        {
            if (totalAvailableSets > 0)
            {
                int leastLaps = int.MaxValue;
                int bestSet = -1;
                for (int availableSet = 0; availableSet < totalAvailableSets; availableSet++)
                {
                    if (CrewChief.currentGameState.TyreData.lapsPerSet[availableSet] < leastLaps)
                    {
                        leastLaps = CrewChief.currentGameState.TyreData.lapsPerSet[availableSet];
                        bestSet = availableSet;
                    }
                }
                if (bestSet != -1)
                {
                    Console.WriteLine("Best tyre set is " + (bestSet + 1) + " with " + leastLaps + " laps use");
                    return selectTyreSet(bestSet, totalAvailableSets);
                }
                else
                {
                    Console.WriteLine("Unable to derive the best tyre set");
                    return false;
                }
            }
            else
            {
                Console.WriteLine("Unable to get the list of available sets");
                return false;
            }
        }

        // assumes we're already on the available tyres sets option
        private static int getAvailableTyreSetsCount()
        {
            int pressCount = 0;
            int selectedSet = CrewChief.currentGameState.TyreData.selectedSet;
            int lastTyreSet = selectedSet;
            int totalAvailableSets = 50;
            // if selectedSet > 25, increase otherwise decrease
            bool decrease = selectedSet < 25;
            while (pressCount < 25)
            {
                if (decrease)
                {
                    sendKeyPressOrMacro(getMenuLeftMacro(), leftKey);
                }
                else
                {
                    sendKeyPressOrMacro(getMenuRightMacro(), rightKey);
                }
                pressCount++;
                Thread.Sleep(200);
                selectedSet = CrewChief.currentGameState.TyreData.selectedSet;

                // the selected set should wrap. If it's not changed after pressing left / right then we're probably not where we think we are in the menu
                if (selectedSet == lastTyreSet)
                {
                    break;
                }
                // if we've wrapped, then we can stop looking
                if (decrease && selectedSet > lastTyreSet)
                {
                    totalAvailableSets = selectedSet + 1;
                    break;
                }
                else if (!decrease && selectedSet < lastTyreSet)
                {
                    totalAvailableSets = lastTyreSet + 1;
                    break;
                }
                lastTyreSet = selectedSet;
            }
            // now reselect the original tyre set
            for (int i=0; i<pressCount; i++)
            {
                if (decrease)
                {
                    sendKeyPressOrMacro(getMenuRightMacro(), rightKey);
                }
                else
                {
                    sendKeyPressOrMacro(getMenuLeftMacro(), leftKey);
                }
                Thread.Sleep(200);
            }
            List<string> availableSetsInfo = new List<string>();
            for (int availableSet = 0; availableSet < totalAvailableSets; availableSet++)
            {
                availableSetsInfo.Add((availableSet + 1) + " (" + CrewChief.currentGameState.TyreData.lapsPerSet[availableSet] + " laps use)");
            }
            Console.WriteLine("Available tyre sets are " + String.Join(", ", availableSetsInfo));
            return totalAvailableSets;
        }

        private static void selectWets()
        {
            mashKeysToPutPitMenuInKnownState();
            sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            if (isStrategyOptionShown())
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            sendKeyPressOrMacro(getMenuRightMacro(), rightKey);
        }

        private static void selectDrys()
        {
            mashKeysToPutPitMenuInKnownState();
        }

        private static void dontChangeTyres()
        {
            mashKeysToPutPitMenuInKnownState();
            sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            if (isStrategyOptionShown())
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            sendKeyPressOrMacro(getMenuRightMacro(), rightKey);
        }

        private static void clearFuel()
        {
            moveCursorToTopOfPitMenu();
            sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            if (isStrategyOptionShown())
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            for (int i = 0; i < 100; i++)
            {
                sendKeyPressOrMacro(getMenuLeftMacro(), leftKey, 50);
            }
        }

        private static void changeAllPressuresTo(float targetPressure)
        {
            mashKeysToPutPitMenuInKnownState();
            int pressesToReachTyrePressureOption = isStrategyOptionShown() ? 6 : 5;
            for (int i = 0; i < pressesToReachTyrePressureOption; i++)
            {
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            }
            changePressureTo(targetPressure, CrewChief.currentGameState.TyreData.ACCFrontLeftPressureMFD);
        }

        private static void changeFrontPressuresTo(float targetPressure)
        {
            mashKeysToPutPitMenuInKnownState();
            int pressesToReachTyrePressureOption = isStrategyOptionShown() ? 7 : 6;
            for (int i = 0; i < pressesToReachTyrePressureOption; i++)
            {
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            }
            changePressureTo(targetPressure, CrewChief.currentGameState.TyreData.ACCFrontLeftPressureMFD);
            sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            changePressureTo(targetPressure, CrewChief.currentGameState.TyreData.ACCFrontRightPressureMFD);
        }

        private static void changeRearPressuresTo(float targetPressure)
        {
            mashKeysToPutPitMenuInKnownState();
            int pressesToReachTyrePressureOption = isStrategyOptionShown() ? 9 : 8;
            for (int i = 0; i < pressesToReachTyrePressureOption; i++)
            {
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            }
            changePressureTo(targetPressure, CrewChief.currentGameState.TyreData.ACCRearLeftPressureMFD);
            sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            changePressureTo(targetPressure, CrewChief.currentGameState.TyreData.ACCRearRightPressureMFD);
        }

        private static void changeLFPressuresTo(float targetPressure)
        {
            mashKeysToPutPitMenuInKnownState();
            int pressesToReachTyrePressureOption = isStrategyOptionShown() ? 7 : 6;
            for (int i = 0; i < pressesToReachTyrePressureOption; i++)
            {
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            }
            changePressureTo(targetPressure, CrewChief.currentGameState.TyreData.ACCFrontLeftPressureMFD);
        }

        private static void changeRFPressuresTo(float targetPressure)
        {
            mashKeysToPutPitMenuInKnownState();
            int pressesToReachTyrePressureOption = isStrategyOptionShown() ? 8 : 7;
            for (int i = 0; i < pressesToReachTyrePressureOption; i++)
            {
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            }
            changePressureTo(targetPressure, CrewChief.currentGameState.TyreData.ACCFrontRightPressureMFD);
        }

        private static void changeLRPressuresTo(float targetPressure)
        {
            mashKeysToPutPitMenuInKnownState();
            int pressesToReachTyrePressureOption = isStrategyOptionShown() ? 9 : 8;
            for (int i = 0; i < pressesToReachTyrePressureOption; i++)
            {
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            }
            changePressureTo(targetPressure, CrewChief.currentGameState.TyreData.ACCRearLeftPressureMFD);
        }

        private static void changeRRPressuresTo(float targetPressure)
        {
            mashKeysToPutPitMenuInKnownState();
            int pressesToReachTyrePressureOption = isStrategyOptionShown() ? 10 : 9;
            for (int i = 0; i < pressesToReachTyrePressureOption; i++)
            {
                sendKeyPressOrMacro(getMenuDownMacro(), downKey);
            }
            changePressureTo(targetPressure, CrewChief.currentGameState.TyreData.ACCRearRightPressureMFD);
        }

        private static void changePressureTo(float targetPressure, float currentPressure)
        {
            if (targetPressure <= 0 || currentPressure <= 0)
            {
                Console.WriteLine("Unable to adjust pressure, target = " + targetPressure + " current = " + currentPressure);
            }
            else
            {
                int diff = (int)((targetPressure - currentPressure) * 10f);
                int presses = Math.Abs(diff);
                // steps of 0.1 psi
                Console.WriteLine("Current MFD pressure = " + currentPressure + " target = " + targetPressure + " key press count " + presses);
                for (int i = 0; i < presses; i++)
                {
                    if (diff > 0)
                    {
                        sendKeyPressOrMacro(getMenuRightMacro(), rightKey);
                    }
                    else
                    {
                        sendKeyPressOrMacro(getMenuLeftMacro(), leftKey);
                    }
                }
            }
        }

        private static ExecutableCommandMacro getPitMenuMacro()
        {
            if (ACCPitMenuManager.pitMenuMacro == null)
            {
                MacroManager.macros.TryGetValue(PIT_MENU_MACRO_NAME, out ACCPitMenuManager.pitMenuMacro);
            }
            return ACCPitMenuManager.pitMenuMacro;
        }

        private static ExecutableCommandMacro getClosePitMenuMacro()
        {
            if (ACCPitMenuManager.closePitMenuMacro == null)
            {
                MacroManager.macros.TryGetValue(CLOSE_PIT_MENU_MACRO_NAME, out ACCPitMenuManager.closePitMenuMacro);
            }
            return ACCPitMenuManager.closePitMenuMacro;
        }

        private static ExecutableCommandMacro getMenuUpMacro()
        {
            if (ACCPitMenuManager.menuUpMacro == null)
            {
                MacroManager.macros.TryGetValue(PIT_MENU_UP_MACRO_NAME, out ACCPitMenuManager.menuUpMacro);
            }
            return ACCPitMenuManager.menuUpMacro;
        }

        private static ExecutableCommandMacro getMenuDownMacro()
        {
            if (ACCPitMenuManager.menuDownMacro == null)
            {
                MacroManager.macros.TryGetValue(PIT_MENU_DOWN_MACRO_NAME, out ACCPitMenuManager.menuDownMacro);
            }
            return ACCPitMenuManager.menuDownMacro;
        }

        private static ExecutableCommandMacro getMenuRightMacro()
        {
            if (ACCPitMenuManager.menuRightMacro == null)
            {
                MacroManager.macros.TryGetValue(PIT_MENU_RIGHT_MACRO_NAME, out ACCPitMenuManager.menuRightMacro);
            }
            return ACCPitMenuManager.menuRightMacro;
        }

        private static ExecutableCommandMacro getMenuLeftMacro()
        {
            if (ACCPitMenuManager.menuLeftMacro == null)
            {
                MacroManager.macros.TryGetValue(PIT_MENU_LEFT_MACRO_NAME, out ACCPitMenuManager.menuLeftMacro);
            }
            return ACCPitMenuManager.menuLeftMacro;
        }
    }
}
