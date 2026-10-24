using FuzzySharp;
using CrewChiefV4.Audio;
using CrewChiefV4.UserInterface.VMs;
using System.Linq;
using System.IO;
using System.Media;
using System.Collections.Generic;
using WebSocketSharp;

namespace CrewChiefV4.UserInterface.Models
{
    /// <summary>
    /// Model module of MyName dialog MVVM
    /// </summary>
    internal class MyName
    {
        private readonly MyName_VM viewModel;
        private const string NO_NAME_SELECTED = "NO_NAME_SELECTED";

        public static string myName
        {
            get {
                string name = UserSettings.GetUserSettings().getString("PERSONALISATION_NAME");
                if (name == NO_NAME_SELECTED)
                {
                    return string.Empty;
                }
                else
                {
                    return name;
                }
            }

            set
            {
                string name;
                if (value == string.Empty)
                {
                    name = NO_NAME_SELECTED;
                }
                else
                {

                    name = value;
                }
                UserSettings.GetUserSettings().setProperty("PERSONALISATION_NAME", name);
            }
        }

        public MyName(MyName_VM _viewModel)
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
            // 1) Full personalisations
            int exactPersonalisationMatch = -1;
            string[] availablePersonalisations = MainWindow.instance.crewChief.audioPlayer.personalisationsArray.
                Where(w => w.Length > name.Length / 2).ToArray();
            // get fuzzy and phonic matches on the available personalisations
            var matches = Process.ExtractTop(name, availablePersonalisations, limit: 10);
            var phonicMatches = DriverNameHelper.PhonixFuzzyMatches(name, availablePersonalisations, 10);
            List<string> fuzzyAndPhonicPersonalisationMatches = new List<string>();
            int index = 0;
            foreach (var match in matches)
            {
                if (!AudioPlayer.NO_PERSONALISATION_SELECTED.Equals(match.Value))
                {
                    fuzzyAndPhonicPersonalisationMatches.Add(match.Value);
                    if (match.Score == 100)
                    {
                        exactPersonalisationMatch = index;
                    }
                    index++;
                }
            }
            fuzzyAndPhonicPersonalisationMatches.AddRange(phonicMatches.driverNameMatches.Where(
                w => w != null && !AudioPlayer.NO_PERSONALISATION_SELECTED.Equals(w) && !fuzzyAndPhonicPersonalisationMatches.Contains(w)));
            // Send it to the VM
            viewModel.fillPersonalisations(fuzzyAndPhonicPersonalisationMatches.ToArray());
            if (exactPersonalisationMatch != -1)
            {
                viewModel.selectPersonalisation(exactPersonalisationMatch);
            }

            // 2) Just driver names
            int exactDriverNameMatch = -1;
            var driverNames = SoundCache.availableDriverNamesForUI;
            // Remove any personalisations that are duplicated in drivers
            foreach (var match in matches)
            {
                if (driverNames.Contains(match.Value))
                {
                    driverNames.Remove(match.Value);
                }
            }
            string[] useAvailableDriverNames = driverNames.Where(w => w.Length > name.Length / 2).ToArray();
            matches = Process.ExtractTop(name, useAvailableDriverNames.ToArray(), limit: 10);
            var driverNamesList = new string[matches.Count()];
            index = 0;
            foreach (var match in matches)
            {
                driverNamesList[index] = match.Value;
                if (exactPersonalisationMatch == -1 && match.Score == 100)
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

            // 3) other less likely fuzzy matches on the driver names
            if (exactPersonalisationMatch == -1 &&
                exactDriverNameMatch == -1)
            {
                var oddDriverNames = SoundCache.availableDriverNamesForUI;
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
                if (!File.Exists(wavFilePath))
                {
                    wavFilePath = Path.Combine(wavFile, "AI-generated", name);
                }
                if (File.Exists(wavFilePath))
                {
                    Log.Info(() => $"Playing {wavFile}\\{name}");
                    SoundPlayer simpleSound = new SoundPlayer(wavFilePath);
                    simpleSound.Play();
                }
            }
        }
        public void SelectPersonalisation(string name)
        {
            if (!myName.Equals(name))
            {
                if (!name.IsNullOrEmpty())
                {
                    Log.Info(() => $"My name '{name}' selected");
                }
                else
                {
                    Log.Warning("Didn't select a name");
                    //name = NO_NAME_SELECTED;
                }
                myName = name;
                UserSettings.GetUserSettings().saveUserSettings();
                if (!UserSettings.GetUserSettings().getBoolean("FIRST_RUN"))
                {
                    viewModel.doRestart();
                }
            }
        }
        public void SelectDriverName(string name)
        {
            UserSettings.GetUserSettings().setProperty("allow_composite_personalisations", true);
            SelectPersonalisation(name);
        }
    }
}
