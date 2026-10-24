// Snipped from WatchedOpponents.cs for comparison.
// Before sharing the one in Opponents.cs
private string getOpponentKey(String voiceMessage, GameStateData currentGameState)
        {
            if (currentGameState == null)
            {
                return null;
            }
            string opponentKey = null;
            if (voiceMessage.Contains(SpeechRecogniser.THE_LEADER))
            {
                if (currentGameState.SessionData.ClassPosition > 1)
                {
                    opponentKey = currentGameState.getOpponentKeyAtClassPosition(1, currentGameState.carClass);
                }
                else if (currentGameState.SessionData.ClassPosition == 1)
                {
                    // we asked for the leader but we're the leader, warn here?
                }
            }
            else if ((voiceMessage.Contains(SpeechRecogniser.THE_CAR_AHEAD) || voiceMessage.Contains(SpeechRecogniser.THE_GUY_AHEAD) ||
                voiceMessage.Contains(SpeechRecogniser.THE_GUY_IN_FRONT) || voiceMessage.Contains(SpeechRecogniser.THE_CAR_IN_FRONT)) && currentGameState.SessionData.ClassPosition > 1)
            {
                opponentKey = currentGameState.getOpponentKeyInFront(currentGameState.carClass);
            }
            else if ((voiceMessage.Contains(SpeechRecogniser.THE_CAR_BEHIND) || voiceMessage.Contains(SpeechRecogniser.THE_GUY_BEHIND)) &&
                            !currentGameState.isLast())
            {
                opponentKey = currentGameState.getOpponentKeyBehind(currentGameState.carClass);
            }
            else if (voiceMessage.Contains(SpeechRecogniser.POSITION_LONG) || voiceMessage.Contains(SpeechRecogniser.POSITION_SHORT))
            {
                int position = 0;
                Boolean found = false;
                foreach (KeyValuePair<String[], int> entry in SpeechRecogniser.racePositionNumberToNumber)
                {
                    foreach (String numberStr in entry.Key)
                    {
                        if (voiceMessage.EndsWith(" " + numberStr))
                        {
                            position = entry.Value;
                            found = true;
                            break;
                        }
                    }
                    if (found)
                    {
                        break;
                    }
                }
                if (position != currentGameState.SessionData.ClassPosition)
                {
                    opponentKey = currentGameState.getOpponentKeyAtClassPosition(position, currentGameState.carClass);
                }
                else
                {
                    // we asked for the car at a position but we're in that position
                }
            }
            else if (voiceMessage.Contains(SpeechRecogniser.CAR_NUMBER))
            {
                String carNumber = "-1";
                Boolean found = false;
                foreach (KeyValuePair<String[], String> entry in SpeechRecogniser.carNumberToNumber)
                {
                    foreach (String numberStr in entry.Key)
                    {
                        if (voiceMessage.EndsWith(" " + numberStr))
                        {
                            carNumber = entry.Value;
                            found = true;
                            break;
                        }
                    }
                    if (found)
                    {
                        break;
                    }
                }
                if (carNumber != "-1" && carNumber != currentGameState.SessionData.PlayerCarNr)
                {
                    opponentKey = currentGameState.getOpponentKeyForCarNumber(carNumber);
                }
            }
            else
            {
                foreach (KeyValuePair<string, OpponentData> entry in currentGameState.OpponentData)
                {
                    String usableDriverNameForSRE = DriverNameHelper.getUsableDriverNameForSRE(entry.Value.DriverRawName);
                    // check for full username match so we're not triggering on substrings within other words
                    if (usableDriverNameForSRE != null
                        && (voiceMessage.Contains(" " + usableDriverNameForSRE + " ") || voiceMessage.EndsWith(" " + usableDriverNameForSRE)))
                    {
                        opponentKey = entry.Key;
                        break;
                    }
                }
            }
            return opponentKey;
        }

