using CrewChiefV4.UserInterface.VMs;
using System.IO;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.Globalization;

namespace CrewChiefV4.UserInterface.Models
{
    /// <summary>
    /// Model module of OpponentNames dialog MVVM
    /// </summary>
    internal class OpponentNames
    {
        private static OpponentNames inst = null;
        private readonly OpponentNames_VM viewModel;
        public OpponentNames(OpponentNames_VM _viewModel)
        {
            viewModel = _viewModel;
            LoadGuessedOpponentNames();
            generateOpponentNamePairs();
            inst = this;
        }

        private void generateOpponentNamePairs()
        {
            List<string> availableGuessedOpponentNames = new List<string>();
            TextInfo myTI = new CultureInfo("en-GB", false).TextInfo;
            foreach (var opponentName in guessedOpponentNames)
            {

                // var oppName = myTI.ToTitleCase(opponentName.Key);
                // var wavFile = char.ToUpper(opponentName.Value[0]) + opponentName.Value.Substring(1);
                availableGuessedOpponentNames.Add($"{opponentName.Key}:{opponentName.Value}");
            }
            viewModel.fillGuessedOpponentNames(availableGuessedOpponentNames);
        }

        Dictionary<string, string> guessedOpponentNames = new Dictionary<string, string>();
        private void LoadGuessedOpponentNames()
        {
            guessedOpponentNames = DriverNameHelper.getGuessedDriverNames();
        }
        public void PlayOpponentDriverName(string name)
        {
            if (guessedOpponentNames.ContainsKey(name))
            {
                MyName.PlayDriverName(guessedOpponentNames[name]);
            }
        }
        public static void NewDriverName(string opponentName, string wavFileName)
        {
            inst.guessedOpponentNames[opponentName] = wavFileName;
            inst.generateOpponentNamePairs();
            DriverNameHelper.writeGuessedDriverNames(inst.guessedOpponentNames);
        }
        public void DeleteOpponentDriverName(string name)
        {
            guessedOpponentNames[name] = null;
            // Now save 
            DriverNameHelper.writeGuessedDriverNames(guessedOpponentNames);
            generateOpponentNamePairs();
        }
        public void EditGuessedNames()
        {
            DriverNameHelper.EditGuessedNames();
            LoadGuessedOpponentNames();
            generateOpponentNamePairs();
        }
        public void DeleteGuessedNames()
        {
            guessedOpponentNames.Clear();
            DriverNameHelper.writeGuessedDriverNames(guessedOpponentNames);
            generateOpponentNamePairs();
        }
        public void EditNames()
        {
            DriverNameHelper.EditNames();
        }
        public void FormClose()
        {
            DriverNameHelper.ReadDriverNameMappings();
        }
    }
}
