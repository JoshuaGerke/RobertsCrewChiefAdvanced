using System.Windows.Forms;

using CrewChiefV4.UserInterface.VMs;

namespace CrewChiefV4.UserInterface.Models
{
    /// <summary>
    /// Model module of SpeechWizard dialog MVVM
    /// </summary>
    internal class SpeechWizard
    {
        private readonly SpeechWizard_VM viewModel;
        public static bool Active { get; set; }

        public SpeechWizard(SpeechWizard_VM _viewModel)
        {
            viewModel = _viewModel;
        }

        internal void Init(bool IsAppRunning)
        {
            if (startButton != startButtonState.Changed)
            {
                startButton = startButtonState.None;
                startButton = IsAppRunning ? startButtonState.Started : startButtonState.Waiting;
            }
        }

        internal void ShutDown()
        {
            if (startButton == startButtonState.Changed)
            {
                buttonRestartCrewChief_Click();
            }
        }

        private enum startButtonState
        {
            None,
            Waiting,
            Started,
            Changed
        }

        private static startButtonState _startButton = startButtonState.None;

        private startButtonState startButton
        {
            get
            {
                return _startButton;
            }
            set
            {
                if (_startButton != value)
                {
                    _startButton = value;
                    string text = null;
                    switch (_startButton)
                    {
                        case startButtonState.Waiting:
                            text ="Start Crew Chief";
                            break;
                        case startButtonState.Started:
                            text = "Stop Crew Chief";
                            break;
                        case startButtonState.Changed:
                            text = CrewChief.Debug.RunningUnderDebugger ? "Stop the debugger" :"Restart Crew Chief";
                            break;
                    }
                    viewModel.setButtonRestartCrewChiefText(text);
                }
            }
        }

        internal void buttonRestartCrewChief_Click()
        {
            switch (startButton)
            {
                case startButtonState.Waiting:
                    startButton = startButtonState.Started;
                    MainWindow.instance.startApplicationButton_Click(null, null);
                    MainWindow.instance.Activate();
                    if (string.IsNullOrEmpty(viewModel.textBoxSpeechRecognitionCountryText))
                    {
                        if (MainWindow.instance.crewChief.speechRecogniser.cultureInfo != null)
                        {
                            viewModel.textBoxSpeechRecognitionCountryText = MainWindow.instance.crewChief.speechRecogniser.cultureInfo.IetfLanguageTag;
                        }
                    }
                    if (string.IsNullOrEmpty(viewModel.textBoxUILanguageText))
                    {
                        if (MainWindow.instance.crewChief.speechRecogniser.cultureInfo != null)
                        {
                            viewModel.textBoxUILanguageText = MainWindow.instance.crewChief.speechRecogniser.cultureInfo.IetfLanguageTag.Split('-')[1];
                        }
                    }
                    break;
                case startButtonState.Started:
                    startButton = startButtonState.Waiting;
                    MainWindow.instance.startApplicationButton_Click(null, null);
                    MainWindow.instance.Activate();
                    break;
                case startButtonState.Changed:
                    startButton = startButtonState.None;  // So we don't loop round.
                    UserSettings.GetUserSettings().saveUserSettings();
                    if (Utilities.RestartApp(app_restart: true,
                            removeSkipUpdates: true,
                            removeProfile: true,
                            removeGame: true))
                    {
                        MainWindow.instance.Close(); //to turn off current app
                    }
                    break;
            }
        }

        internal void radioButtonMicrosoftSRE_CheckedChanged()
        {
            if (UserSettings.GetUserSettings().getBoolean("prefer_system_sre"))
            {
                UserSettings.GetUserSettings().setProperty("prefer_system_sre", false);
                UserSettings.GetUserSettings().saveUserSettings();
                startButton = startButtonState.Changed;
            }
        }

        internal void radioButtonSystem_CheckedChanged()
        {
            if (!UserSettings.GetUserSettings().getBoolean("prefer_system_sre"))
            {
                UserSettings.GetUserSettings().setProperty("prefer_system_sre", true);
                UserSettings.GetUserSettings().saveUserSettings();
                startButton = startButtonState.Changed;
            }
        }

        internal void checkBoxNaudio_CheckedChanged(bool value)
        {
            if (UserSettings.GetUserSettings().getBoolean("use_naudio_for_speech_recognition") != value)
            {
                UserSettings.GetUserSettings().setProperty("use_naudio_for_speech_recognition", value);
                UserSettings.GetUserSettings().saveUserSettings();
                startButton = startButtonState.Changed;
            }
        }

        internal void checkBoxFreeDictation_CheckedChanged(bool value)
        {
            if (UserSettings.GetUserSettings().getBoolean("use_free_dictation_for_chat") != value)
            {
                UserSettings.GetUserSettings().setProperty("use_free_dictation_for_chat", value);
                UserSettings.GetUserSettings().saveUserSettings();
                startButton = startButtonState.Changed;
            }
        }

        internal void textBoxUILanguage_KeyDown(string value)
        {
            if (UserSettings.GetUserSettings().getString("ui_language") != value)
            {
                UserSettings.GetUserSettings().setProperty("ui_language", value);
                UserSettings.GetUserSettings().saveUserSettings();
                startButton = startButtonState.Changed;
            }
        }

        internal void textBoxSpeechRecognitionCountry_KeyDown(string value)
        {
            if (UserSettings.GetUserSettings().getString("speech_recognition_country") != value)
            {
                UserSettings.GetUserSettings().setProperty("speech_recognition_country", value);
                UserSettings.GetUserSettings().saveUserSettings();
                startButton = startButtonState.Changed;
            }
        }
    }
}
