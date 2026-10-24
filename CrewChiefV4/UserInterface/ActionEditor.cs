using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CrewChiefV4;

namespace CrewChiefV4
{
    public partial class ActionEditor : Form
    {
        ControllerConfiguration.ControllerConfigurationData controllerConfigurationData = new ControllerConfiguration.ControllerConfigurationData();
        private Boolean hasChanges = false;

        public struct ActionUiEntry
        {
            public string uiText;
            public string resolvedUiText;

            public ActionUiEntry(string uiText, string resolvedUiText)
            {
                this.uiText = uiText;
                this.resolvedUiText = resolvedUiText;
            }

            public override string ToString()
            {
                return uiText;
            }
        }

        public ActionEditor(Form parent)
        {
            StartPosition = FormStartPosition.CenterParent;
            InitializeComponent();

            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                _ = new DarkModeForms.DarkModeCS(this);

            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.SuspendLayout();
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            labelCurrentActions.Text = Configuration.getUIString("current_available_actions");
            labelAdditionalActions.Text = Configuration.getUIString("additional_available_actions");
            buttonSave.Text = Configuration.getUIString("save_and_restart");
            exitButton.Text = Configuration.getUIString("exit_without_saving");
            buttonReset.Text = Configuration.getUIString("reload_actions");
            reloadData();

            this.KeyPreview = true;
            this.KeyDown += this.ActionEditor_KeyDown;

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void ActionEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                this.Close();
        }

        private void reloadData()
        {
            controllerConfigurationData = ControllerConfiguration.getControllerConfigurationDataFromFile(ControllerConfiguration.getUserControllerConfigurationDataFileLocation());
            // Need to initialize si we can show correct Ui text to the user
            foreach (ControllerConfiguration.ButtonAssignment ba in controllerConfigurationData.buttonAssignments)
            {
                ba.Initialize();
            }
            refreshLists();
        }

        private void refreshLists()
        {
            listBoxCurrentlyAvailableActions.Items.Clear();
            listBoxAdditionalAvailableActions.Items.Clear();

            var uiAvailableActionList = new List<ActionEditor.ActionUiEntry>();
            foreach (ControllerConfiguration.ButtonAssignment ba in controllerConfigurationData.buttonAssignments.Where(ba => ba.availableAction))
            {
#if DEBUG
                // I know those asserts are annoying.  They are here to understand why do we have dupes.
                Debug.Assert(!uiAvailableActionList.Contains(new ActionEditor.ActionUiEntry(Utilities.Strings.FirstLetterToUpper(ba.resolvedUiText), ba.resolvedUiText)));
#endif
                uiAvailableActionList.Add(new ActionEditor.ActionUiEntry(Utilities.Strings.FirstLetterToUpper(ba.resolvedUiText), ba.resolvedUiText));
            }

            var uiAdditionalActionList = new List<ActionEditor.ActionUiEntry>();
            foreach (ControllerConfiguration.ButtonAssignment ba in controllerConfigurationData.buttonAssignments.Where(ba => !ba.availableAction))
            {
#if DEBUG
                Debug.Assert(!uiAdditionalActionList.Contains(new ActionEditor.ActionUiEntry(Utilities.Strings.FirstLetterToUpper(ba.resolvedUiText), ba.resolvedUiText)));
#endif
                uiAdditionalActionList.Add(new ActionEditor.ActionUiEntry(Utilities.Strings.FirstLetterToUpper(ba.resolvedUiText), ba.resolvedUiText));
            }

            uiAvailableActionList = uiAvailableActionList.OrderBy(x => x.uiText).ToList();
            uiAdditionalActionList = uiAdditionalActionList.OrderBy(x => x.uiText).ToList();

            foreach (var action in uiAvailableActionList)
            {
                listBoxCurrentlyAvailableActions.Items.Add(action);
            }

            foreach (var action in uiAdditionalActionList)
            {
                // Get the text phrase for the action
                var _action = action;
                try
                {
                    var phrase = Configuration.getSpeechRecognitionPhrases(action.uiText);
                    if (!String.IsNullOrEmpty(phrase[0]))
                    {
                        _action.uiText = Utilities.Strings.FirstLetterToUpper(phrase[0]);
                    }
                }
                catch (Exception ex)
                {
                    // It already has the phrase
                }
                // Final tidying, replace some words
                foreach (var word in new Dictionary<string, string>
                         {
                             { " i ", " I " },
                             { " tire ", " tyre " },
                             { " tires", " tyres" },
                             { " eye rating", " iRating" },
                             { " sof", " SOF" },
                             { "SOFt", "soft" },  // Stet that one
                             })
                {
                    _action.uiText = _action.uiText.Replace(word.Key, word.Value);
                }
                listBoxAdditionalAvailableActions.Items.Add(_action);
            }
        }
        private void buttonRemoveAction_Click(object sender, EventArgs e)
        {
            bool hasChangedThisClick = false;
            foreach (var item in listBoxCurrentlyAvailableActions.SelectedItems)
            {
                ControllerConfiguration.ButtonAssignment ba = controllerConfigurationData.buttonAssignments.FirstOrDefault(ba1 => ba1.resolvedUiText == ((ActionEditor.ActionUiEntry)item).resolvedUiText);
                if(ba != null)
                {
                    ba.availableAction = false;
                    hasChanges = true;
                    hasChangedThisClick = true;
                }
            }
            if (hasChangedThisClick)
            {
                refreshLists();
            }  
        }

        private void buttonAddAction_Click(object sender, EventArgs e)
        {
            bool hasChangedThisClick = false;
            foreach (var item in listBoxAdditionalAvailableActions.SelectedItems)
            {
                ControllerConfiguration.ButtonAssignment ba = controllerConfigurationData.buttonAssignments.FirstOrDefault(ba1 => ba1.resolvedUiText == ((ActionEditor.ActionUiEntry)item).resolvedUiText);
                if (ba != null)
                {
                    ba.availableAction = true;
                    hasChanges = true;
                    hasChangedThisClick = true;
                }
            }
            if (hasChangedThisClick)
            {
                refreshLists();
            }  
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            ControllerConfiguration.saveControllerConfigurationDataFile(controllerConfigurationData);
            hasChanges = false;
            if (Utilities.RestartApp()) // Why not (app_restart:true)?
            {
                MainWindow.instance.Close(); //to turn off current app
            }
        }

        private void ActionEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (hasChanges)
            {
                var warningMessage = string.Format(Utilities.Strings.NewlinesInLongString(Configuration.getUIString("save_prop_changes_warning")),
                    Path.GetFileNameWithoutExtension(UserSettings.GetUserSettings().getString("current_settings_profile")));
                if (CrewChief.Debug.RunningUnderDebugger)
                {
                    warningMessage = "You have unsaved changes. Click 'Yes' to save these changes (you will need to manually restart the application). Click 'No' to discard these changes";
                }
                if (MessageBox.Show(warningMessage, Configuration.getUIString("save_changes"), MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    ControllerConfiguration.saveControllerConfigurationDataFile(controllerConfigurationData);
                    if (Utilities.RestartApp(app_restart:true))
                    {
                        MainWindow.instance.Close(); //to turn off current app
                    }
                }
            }
        }

        private void exitButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void buttonReset_Click(object sender, EventArgs e)
        {
            reloadData();
            hasChanges = false;
        }
    }
}
