using FuzzySharp;
using CrewChiefV4.Audio;
using CrewChiefV4.UserInterface.VMs;
using System.Linq;
using System.IO;
using System.Media;
using System.Collections.Generic;

namespace CrewChiefV4.UserInterface.Models
{
    /// <summary>
    /// Model module of OpponentNameSelection dialog MVVM
    /// </summary>
    internal class OpponentNameSelection
    {
        private readonly OpponentNameSelection_VM viewModel;
        public OpponentNameSelection(OpponentNameSelection_VM _viewModel)
        {
            viewModel = _viewModel;
        }
        /// <summary>
        /// Fuzzy match the name against all the full personalisations and
        /// also the list of opponent names that can have personalisation stubs
        /// applied to them
        /// </summary>
        /// <param name="name">Driver's name</param>
        public void NameEntry(string name)
        {
            // Create 2 lists of candidate names:
            // 1) Just driver names
            int exactDriverNameMatch = -1;
            var driverNames = SoundCache.availableDriverNamesForUI;
            // This needs to work on CLEANED UP names, not "jim_britton!!!"
            name = DriverNameHelper.validateAndCleanUpName(name);
            string[] useAvailableDriverNames = driverNames.Where(w => w.Length > name.Length / 2).ToArray();
            var matches = Process.ExtractTop(name, useAvailableDriverNames.ToArray(), limit: 10);
            var driverNamesList = new string[matches.Count()];
            var index = 0;
            foreach (var match in matches)
            {
                driverNamesList[index] = match.Value;
                if (match.Score == 100)
                {
                    exactDriverNameMatch = index;
                }
                index++;
            }
            viewModel.fillDriverNames(driverNamesList);
            if (exactDriverNameMatch != -1)
            {
                viewModel.selectDriverName(exactDriverNameMatch);
            }

            // 2) other less likely fuzzy matches on the driver names
            if (exactDriverNameMatch == -1)
            {
                var oddDriverNames = new HashSet<string>(SoundCache.availableDriverNamesForUI);
                // Remove any driver names that are already offered
                foreach (var match in matches)
                {
                    if (oddDriverNames.Contains(match.Value))
                    {
                        oddDriverNames.Remove(match.Value);
                    }
                }
                string[] otherDriverNames = oddDriverNames.ToArray();
                var res = DriverNameHelper.PhonixFuzzyMatches(name, otherDriverNames, 10);
                if (res.matched)
                {
                    viewModel.fillOtherDriverNames(res.driverNameMatches.ToArray());
                }
            }
        }
        public void PlayRandomPersonalisation(string name)
        {
            DirectoryInfo personalisationsDirectory = new DirectoryInfo(Path.Combine(AudioPlayer.soundFilesPath, "personalisations", name, "prefixes_and_suffixes", "bad_luck"));
            if (personalisationsDirectory.Exists)
            {
                FileInfo[] files = personalisationsDirectory.GetFiles();
                var wavFile = files[Utilities.random.Next(0, files.Length)];
                Log.Info(() => $"Playing {wavFile.FullName}");
                SoundPlayer simpleSound = new SoundPlayer(wavFile.FullName);
                simpleSound.Play();
            }
        }
        public void PlayRandomDriverName(string name)
        {
            PlayDriverName(name);
        }
        public static void PlayDriverName(string name)
        { 
            //DirectoryInfo soundsDirectory = new DirectoryInfo(AudioPlayer.soundFilesPath);
            DirectoryInfo driverNamesDirectory = new DirectoryInfo(Path.Combine(AudioPlayer.soundFilesPath, "driver_names"));
            //SoundCache.createCompositePrefixesAndSuffixes(soundsDirectory, personalisationsDirectory, name);
            if (driverNamesDirectory.Exists)
            {
                string wavFile = driverNamesDirectory.FullName;
                name += ".wav";
                string wavFilePath = Path.Combine(wavFile, name);
                if (File.Exists(wavFilePath))
                {
                    Log.Info(() => $"Playing {wavFile}\\{name}");
                    SoundPlayer simpleSound = new SoundPlayer(wavFilePath);
                    simpleSound.Play();
                }
            }
        }
        public void SelectedDriverName(string name)
        {
            viewModel.selectedDriverName(name);
        }
        public void NewDriverName(string opponentName, string wavFileName)
        {
            OpponentNames.NewDriverName(opponentName, wavFileName);
        }
    }
}
