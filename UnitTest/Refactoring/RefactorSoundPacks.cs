using CrewChiefV4;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;

using static CrewChiefV4.MainWindow;
using static CrewChiefV4.VirtualReality.TrackedDevices.Controller;

namespace UnitTest.Refactoring
{
    [TestClass]
    public class RefactorSoundPacks
    {
        [TestMethod]
        public void TestButtonUiStringsAreAvailable()
        {
            // set up the sound pack buttons
            new CrewChiefV4.UserSettings();
            CrewChief.gameDefinition = null;
            var hwnd = new MainWindow();
            var ccObj = hwnd.crewChief;
            MainWindow.instance = hwnd;
            MainWindow.instance.crewChief = ccObj;
            MainWindow.instance.initialiseSoundPackButtons();
            List<SoundButton> soundButtons = new List<SoundButton>
            {
                MainWindow.instance.soundPackButton,
                MainWindow.instance.driverNamesButton,
                MainWindow.instance.personalisationsButton
            };

            foreach (SoundButton soundButton in soundButtons)
            {
                var uiStringName = $"extracting_{soundButton.packName}";
                var uiText = Configuration.getUIString(uiStringName);   // will return uiStringName if not found
                Assert.AreNotEqual(uiStringName, uiText, $"{uiStringName} not found");

                uiStringName = $"{soundButton.packName}_are_up_to_date";
                uiText = Configuration.getUIString(uiStringName);
                Assert.AreNotEqual(uiStringName, uiText, $"{uiStringName} not found");

                uiStringName = $"no_{soundButton.packName}_detected_press_to_download";
                uiText = Configuration.getUIString(uiStringName);
                Assert.AreNotEqual(uiStringName, uiText, $"{uiStringName} not found");

                uiStringName = $"updated_{soundButton.packName}_available_press_to_download";
                uiText = Configuration.getUIString(uiStringName);
                Assert.AreNotEqual(uiStringName, uiText, $"{uiStringName} not found");

                uiStringName = $"error_downloading_{soundButton.packName}";
                uiText = Configuration.getUIString(uiStringName);
                Assert.AreNotEqual(uiStringName, uiText, $"{uiStringName} not found");

                uiStringName = $"unable_to_download_{soundButton.packName}";
                uiText = Configuration.getUIString(uiStringName);
                Assert.AreNotEqual(uiStringName, uiText, $"{uiStringName} not found");

                uiStringName = $"unknown_{soundButton.packName}_language_text";
                uiText = Configuration.getUIString(uiStringName);
                Assert.AreNotEqual(uiStringName, uiText, $"{uiStringName} not found");

                uiStringName = $"unknown_{soundButton.packName}_language_title";
                uiText = Configuration.getUIString(uiStringName);
                Assert.AreNotEqual(uiStringName, uiText, $"{uiStringName} not found");

                uiStringName = $"downloading_{soundButton.packName}";
                uiText = Configuration.getUIString(uiStringName);
                Assert.AreNotEqual(uiStringName, uiText, $"{uiStringName} not found");
            }
            MainWindow.instance.crewChief.audioPlayer.Dispose();
        }
    }
}
