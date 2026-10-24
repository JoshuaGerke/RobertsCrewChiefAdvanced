namespace CrewChiefV4.UserInterface.VMs
{
    /// <summary>
    /// View Model module of SpeechWizard dialog MVVM
    /// </summary>
    internal class SpeechWizard_VM
    {
        private readonly SpeechWizard_V view;

        public SpeechWizard_VM(SpeechWizard_V _view)
        {
            view = _view;
        }

        public void setButtonRestartCrewChiefText(string text)
        {
            view.buttonRestartCrewChief.Text = text;
        }

        public string textBoxSpeechRecognitionCountryText
        {
            get => view.textBoxSpeechRecognitionCountry.Text;
            set => view.textBoxSpeechRecognitionCountry.Text = value;
        }

        public string textBoxUILanguageText
        {
            get => view.textBoxUILanguage.Text;
            set => view.textBoxUILanguage.Text = value;
        }
    }
}
