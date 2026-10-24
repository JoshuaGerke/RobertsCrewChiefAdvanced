using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CrewChiefV4.commands;
using System.IO;
using System.Threading;
using static CrewChiefV4.ControllerConfiguration;

namespace CrewChiefV4
{

    public partial class MacroEditor : Form
    {
        public static List<String> builtInKeyMappings = new List<String>();
        MacroContainer macroContainer = null;
        List<GameDefinition> availableMacroGames = null;
        ControllerConfiguration controllerConfiguration;
        private readonly List<Guid> controllerGuids = new List<Guid>();

        private Thread assignButtonThread = null;
        bool isAssigningButton = false;

        public MacroEditor(Form parent, ControllerConfiguration controllerConfiguration)
        {
            this.controllerConfiguration = controllerConfiguration;
            StartPosition = FormStartPosition.CenterParent;
            InitializeComponent();

            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                _ = new DarkModeForms.DarkModeCS(this);

            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.SuspendLayout();
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            //Global
            groupBoxGlobalOptins.Text = Configuration.getUIString("global_macro_settings");
            labelGame.Text = Configuration.getUIString("game");

            groupBoxAvailableMacros.Text = Configuration.getUIString("available_macros");
            buttonAddNewMacro.Text = Configuration.getUIString("add_or_edit_macro");
            buttonDeleteSelectedMacro.Text = Configuration.getUIString("delete_selected_macro");
            radioButtonViewOnly.Text = Configuration.getUIString("macro_view_only");
            radioButtonEditSelectedMacro.Text = Configuration.getUIString("edit_selected_macro");
            radioButtonAddNewMacro.Text = Configuration.getUIString("create_new_macro");

            groupBoxGlobalMacroVoiceTrigger.Text = Configuration.getUIString("macro_voice_trigger");
            radioButtonRegularVoiceTrigger.Text = Configuration.getUIString("regular_macro_voice_command");
            radioButtonIntegerVoiceTrigger.Text = Configuration.getUIString("integer_macro_voice_command");
            labelMacroEditMode.Text = Configuration.getUIString("macro_edit_mode");

            labelConfirmationMessage.Text = Configuration.getUIString("confirmation_message");
            buttonSelectConfirmationMessage.Text = Configuration.getUIString("select_confirmation_message");
            labelGlobalMacroDescription.Text = Configuration.getUIString("macro_description");

            //Game Specific
            groupBoxGameSettings.Text = Configuration.getUIString("game_specific_settings");

            labelActionSequence.Text = Configuration.getUIString("action_sequence");
            labelKeyPressTime.Text = Configuration.getUIString("keypress_time");
            labelWaitBetweenEachCommand.Text = Configuration.getUIString("keypress_wait_time");
            buttonAddActionSequence.Text = Configuration.getUIString("add_action_sequence");


            labelGameMacroDescription.Text = Configuration.getUIString("macro_description");


            groupAvailableActions.Text = Configuration.getUIString("available_actions");
            radioButtonRegularKeyAction.Text = Configuration.getUIString("regular_key_action");
            radioButtonMultipleKeyAction.Text = Configuration.getUIString("multiple_key_action");
            radioButtonMultipleVoiceTrigger.Text = Configuration.getUIString("voice_trigger_action");
            radioButtonMultipleFuelAction.Text = Configuration.getUIString("multiple_fuel_action");
            radioButtonWaitAction.Text = Configuration.getUIString("wait_action");
            radioButtonFreeTextAction.Text = Configuration.getUIString("free_text_action");
            radioButtonAdvancedEditAction.Text = Configuration.getUIString("advanced_edit_action");
            this.macroEditorTooltip.SetToolTip(this.radioButtonAdvancedEditAction, Configuration.getUIString("advanced_edit_tooltip"));

            radioButtonModifierAndKey.Text = Configuration.getUIString("modifier_and_key");
            buttonAddSelectedKeyToSequence.Text = Configuration.getUIString("add_key_to_sequence");
            buttonUndoLastAction.Text = Configuration.getUIString("undo_last_action");
            labelActionKeys.Text = Configuration.getUIString("actions_keys");
            lableModifierKeys.Text = Configuration.getUIString("modifier_keys");
            // Load
            buttonLoadUserMacroSettings.Text = Configuration.getUIString("load_user_macro_settings");
            buttonLoadDefaultMacroSettings.Text = Configuration.getUIString("load_default_macro_settings");

            deleteAssignmentButton.Text = Configuration.getUIString("delete_assignment");
            addAssignmentButton.Text = Configuration.getUIString("assign_control");
            currentAssignmentLabel.Text = Configuration.getUIString("no_control_assigned");
            autoExecuteStartMacro.Text = Configuration.getUIString("auto_execute_start_chat");
            autoExecuteEndMacro.Text = Configuration.getUIString("auto_execute_end_chat");

            radioButtonRegularKeyAction.Checked = true;
            radioButtonViewOnly.Checked = true;

            if (builtInKeyMappings.Count <= 0)
            {
                foreach (KeyPresser.KeyCode value in Enum.GetValues(typeof(KeyPresser.KeyCode)))
                {
                    builtInKeyMappings.Add(value.ToString());
                }
            }
            if (comboBoxKeySelection.Items.Count <= 0)
            {
                comboBoxKeySelection.Items.AddRange(builtInKeyMappings.ToArray());
            }
            if (comboBoxModifierKeySelection.Items.Count <= 0)
            {
                comboBoxModifierKeySelection.Items.AddRange(builtInKeyMappings.ToArray());
            }
            macroContainer = MacroManager.loadCommands(MacroManager.getMacrosFileLocation());

            availableMacroGames = GameDefinition.getAllAvailableGameDefinitions(true).Where(
                gd => gd.gameEnum != GameEnum.PCARS2_NETWORK).ToList();
            var items = from name in availableMacroGames orderby name.friendlyName ascending select name;
            availableMacroGames = items.ToList();
            listBoxGames.Items.Clear();
            foreach (var mapping in availableMacroGames)
            {
                listBoxGames.Items.Add(mapping.macroEditorName);
            }
            // try to select the CME game matching the main window game
            var selectionIndexFromPosition = Math.Min(MainWindow.instance.gameDefinitionList.SelectedIndex,
                listBoxGames.Items.Count-1);
            if (MainWindow.instance.gameDefinitionList.SelectedItem == null)
            {   // No game has been selected, select the first one
                MainWindow.instance.gameDefinitionList.SelectedIndex = 0;
            }
            var selectionIndexFromName = listBoxGames.Items.IndexOf(MainWindow.instance.gameDefinitionList.SelectedItem);

            if (selectionIndexFromName != -1)
            {
                listBoxGames.SetSelected(selectionIndexFromName, true);
            }
            else if (selectionIndexFromPosition != -1)
            {
                listBoxGames.SetSelected(selectionIndexFromPosition, true);
            }
            updateMacroList();
            listBoxGames.Select();

            updateControllersList();
            updateAssignmentElements(true);

            this.KeyPreview = true;
            this.KeyDown += this.MacroEditor_KeyDown;

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void MacroEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                this.Close();
        }

        private void listBoxGames_SelectedIndexChanged(object sender, EventArgs e)
        {
            textBoxActionSequence.Text = "";
            textBoxDescription.Text = "";
            comboBoxKeySelection.SelectedIndex = -1;
            comboBoxModifierKeySelection.SelectedIndex = -1;
            if (listBoxGames.SelectedIndex != -1)
            {
                buttonAddActionSequence.Text = Configuration.getUIString("add_action_sequence") + " " + listBoxGames.Items[listBoxGames.SelectedIndex].ToString();
            }
            if (listBoxAvailableMacros.SelectedIndex != -1)
            {
                listBoxAvailableMacros.SetSelected(listBoxAvailableMacros.SelectedIndex, true);
            }
            var currentSelectedGame = availableMacroGames[listBoxGames.SelectedIndex];
            radioButtonRf2HwControl.Visible = radioButtonRf2HwControl.Enabled = false; // Not yet wired into RF2 PM    currentSelectedGame == GameDefinition.rfactor2_64bit;
            radioButtonDosCommand.Visible = radioButtonDosCommand.Enabled = UserSettings.GetUserSettings().getBoolean("enable_running_dos_commands_from_command_macros");
            this.macroEditorTooltip.SetToolTip(this.radioButtonDosCommand, Configuration.getUIString("dos_command_tooltip"));
        }

        private void listBoxAvailableMacros_SelectedIndexChanged(object sender, EventArgs e)
        {
            textBoxActionSequence.Text = "";
            textBoxDescription.Text = "";
            textBoxConfirmationMessage.Text = "";
            textBoxKeyPressTime.Text = "";
            textBoxWaitBetweenEachCommand.Text = "60";
            textBoxGameMacroDescription.Text = "";
            textBoxAddNewMacro.Text = "";
            textBoxVoiceTriggers.Text = "";
            if (listBoxGames.SelectedIndex == -1)
            {
                listBoxGames.SelectedIndex = 0;
            }
            if (listBoxAvailableMacros.SelectedIndex == -1)
            {
                return;
            }
            var currentSelectedGame = availableMacroGames[listBoxGames.SelectedIndex];
            var macro = macroContainer.macros.FirstOrDefault(m => m.name == listBoxAvailableMacros.Items[listBoxAvailableMacros.SelectedIndex].ToString());
            if (macro != null)
            {
                textBoxDescription.Text = macro.description;
                if (macro.integerVariableVoiceTrigger != null)
                {
                    textBoxVoiceTriggers.Text = macro.integerVariableVoiceTrigger;
                    textBoxVoiceTriggers.Multiline = false;
                    radioButtonIntegerVoiceTrigger.Checked = true;
                    radioButtonMultipleVoiceTrigger.Enabled = true;
                }
                else
                {
                    textBoxVoiceTriggers.Lines = macro.voiceTriggers;
                    textBoxVoiceTriggers.Multiline = true;
                    radioButtonRegularVoiceTrigger.Checked = true;
                    radioButtonMultipleVoiceTrigger.Enabled = false;
                }
                textBoxConfirmationMessage.Text = macro.confirmationMessage;
                textBoxAddNewMacro.Text = macro.name;
                if (macro.commandSets != null)
                {
                    var macroForGame = macro.commandSets.FirstOrDefault(cs => cs.gameDefinition == currentSelectedGame.gameEnum.ToString());
                    if (macroForGame != null)
                    {
                        textBoxActionSequence.Lines = macroForGame.actionSequence;
                        var match = textBoxActionSequence.Lines.ToList().Where(s => s.Contains($"{{{MacroManager.FREE_TEXT_IDENTIFIER}}}"));
                        if (match != null)
                        {
                            autoExecuteStartMacro.Enabled = true;
                            autoExecuteEndMacro.Enabled = true;
                            autoExecuteStartMacro.Checked = macroForGame.autoExecuteStartChatMacro;
                            autoExecuteEndMacro.Checked = macroForGame.autoExecuteEndChatMacro;                           
                        }
                        textBoxKeyPressTime.Text = macroForGame.keyPressTime == null ? "" : macroForGame.keyPressTime.Value.ToString();
                        textBoxWaitBetweenEachCommand.Text = macroForGame.waitBetweenEachCommand.ToString();
                        textBoxGameMacroDescription.Text = macroForGame.description;
                    }
                }

            }
            updateAssignmentElements(true);
        }

        private void updateAssignmentElements(bool updateSelectedController)
        {
            if (listBoxAvailableMacros == null ||
                listBoxAvailableMacros.Items.Count == 0 ||
                listBoxAvailableMacros.SelectedIndex < 0)
            {
                return;
            }
            Macro currentMacro = macroContainer.macros.FirstOrDefault(mc => mc.name == listBoxAvailableMacros.Items[listBoxAvailableMacros.SelectedIndex].ToString());
            this.addAssignmentButton.Enabled = currentMacro != null && this.controllersList.SelectedIndex > -1 && ((MainWindow.ControllerUiEntry)this.controllersList.Items[this.controllersList.SelectedIndex]).isConnected;
            if (currentMacro != null && currentMacro.buttonTriggers != null && currentMacro.buttonTriggers.Length > 0
                && currentMacro.buttonTriggers[0].deviceId != null && currentMacro.buttonTriggers[0].deviceId.Length > 0)
            {
                Guid assignedGuid;
                bool hasGuid = Guid.TryParse(currentMacro.buttonTriggers[0].deviceId, out assignedGuid);
                string controllerName = hasGuid
                    ? ControllerNames.GetControllerDisplayName(assignedGuid, currentMacro.buttonTriggers[0].description)
                    : currentMacro.buttonTriggers[0].description;
                string buttonName = hasGuid
                    ? ControllerNames.GetControllerButtonNameOrNumber(assignedGuid, currentMacro.buttonTriggers[0].buttonIndex)
                    : null;
                string buttonDescription = string.IsNullOrWhiteSpace(buttonName)
                    ? "Button " + currentMacro.buttonTriggers[0].buttonIndex
                    : buttonName;
                this.currentAssignmentLabel.Text = "Assigned to " + controllerName + ", " + buttonDescription;
                this.deleteAssignmentButton.Enabled = true;
                if (updateSelectedController)
                {
                    int index = hasGuid ? controllerGuids.IndexOf(assignedGuid) : -1;
                    if (index < 0)
                    {
                        for (int i = 0; i < this.controllersList.Items.Count; i++)
                        {
                            if (this.controllersList.Items[i].ToString().Equals(controllerName))
                            {
                                index = i;
                                break;
                            }
                        }
                    }
                    if (index >= 0)
                    {
                        this.controllersList.SelectedIndex = index;
                    }
                }
            }
            else
            {
                this.currentAssignmentLabel.Text = Configuration.getUIString("no_control_assigned");
                this.deleteAssignmentButton.Enabled = false;
                if (updateSelectedController)
                {
                    this.controllersList.SelectedIndex = -1;
                }
            }
        }

        private void textBoxKeyPressTime_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar))
                e.Handled = true;         //Just Digits
            if (e.KeyChar == (char)8)
                e.Handled = false;        //Allow Backspace
        }

        private void textBoxWaitBetweenEachCommand_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsDigit(e.KeyChar))
                e.Handled = true;         //Just Digits
            if (e.KeyChar == (char)8)
                e.Handled = false;        //Allow Backspace
        }

        private void buttonAddSelectedActionToSequence_Click(object sender, EventArgs e)
        {
            if (!radioButtonRegularKeyAction.Checked && string.IsNullOrWhiteSpace(textBoxSpecialActionParameter.Text))
            {
                MessageBox.Show(labelSpecialActionParameter.Text + " " + Configuration.getUIString("special_action_text_cant_be_empty"));

                return;
            }

            string formatedAction = "";
            List<string> currentLines = textBoxActionSequence.Lines.ToList();

            if (radioButtonWaitAction.Checked)
            {
                formatedAction = $"{{{MacroManager.WAIT_IDENTIFIER},{textBoxSpecialActionParameter.Text}}}";
            }
            else if (radioButtonFreeTextAction.Checked)
            {
                formatedAction = $"{{{MacroManager.FREE_TEXT_IDENTIFIER}}}{textBoxSpecialActionParameter.Text}";
            }
            else if (radioButtonDosCommand.Checked)
            {
                formatedAction = $"{{{MacroManager.EXECUTE_DOS_COMMAND_IDENTIFIER},{textBoxSpecialActionParameter.Text}}}";
            }
            else if (radioButtonRf2HwControl.Checked)
            {
                formatedAction = $"{{{MacroManager.RF2_HWCONTROL_IDENTIFIER},{textBoxSpecialActionParameter.Text}}}";
            }
            else if (radioButtonModifierAndKey.Checked)
            {
                if (comboBoxKeySelection.SelectedIndex != -1 && comboBoxModifierKeySelection.SelectedIndex != -1)
                {
                    formatedAction = comboBoxModifierKeySelection.Items[comboBoxModifierKeySelection.SelectedIndex].ToString() 
                        + textBoxSpecialActionParameter.Text 
                        + comboBoxKeySelection.Items[comboBoxKeySelection.SelectedIndex].ToString();
                }
                else
                {
                    MessageBox.Show(Configuration.getUIString("must_select_a_key"));
                    return;
                }
            }
            else if(comboBoxKeySelection.SelectedIndex != -1)
            {
                if (radioButtonMultipleKeyAction.Checked)
                {
                    formatedAction = $"{{{MacroManager.MULTIPLE_PRESS_IDENTIFIER},{textBoxSpecialActionParameter.Text}}}" + comboBoxKeySelection.Items[comboBoxKeySelection.SelectedIndex].ToString();
                }
                else if (radioButtonMultipleFuelAction.Checked || radioButtonMultipleVoiceTrigger.Checked)
                {
                    formatedAction = textBoxSpecialActionParameter.Text + comboBoxKeySelection.Items[comboBoxKeySelection.SelectedIndex].ToString();
                }
                else if (radioButtonRegularKeyAction.Checked)
                {
                    formatedAction = comboBoxKeySelection.Items[comboBoxKeySelection.SelectedIndex].ToString();
                }
            }
            else
            {
                MessageBox.Show(Configuration.getUIString("must_select_a_key"));
                return;
            }

            if (!string.IsNullOrWhiteSpace(formatedAction))
            {
                currentLines.Add(formatedAction);
            }
            textBoxActionSequence.Lines = currentLines.ToArray();
        }

        private void buttonSelectConfirmationMessage_Click(object sender, EventArgs e)
        {
            string soundPackLocationOverride = UserSettings.GetUserSettings().getString("override_default_sound_pack_location") + @"\voice";
            string soundPackLocation = DataFiles.VoiceFilesFolder;
            if (soundPackLocationOverride != null && soundPackLocationOverride.Length > 0)
            {
                try
                {
                    if (Directory.Exists(soundPackLocationOverride))
                    {
                        soundPackLocation = soundPackLocationOverride;
                    }
                }
                catch (Exception ee) { Log.Exception(ee); }
            }
            try
            {
                if (!Directory.Exists(soundPackLocation))
                {
                    MessageBox.Show(Configuration.getUIString("macro_please_download_soundpack"));
                    return;
                }
                else
                {
                    using (OpenFileDialog folderBrowser = new OpenFileDialog())
                    {
                        folderBrowser.ValidateNames = false;
                        folderBrowser.CheckFileExists = false;
                        folderBrowser.CheckPathExists = true;
                        folderBrowser.FileName = "Folder Selection.";
                        folderBrowser.InitialDirectory = soundPackLocation;
                        if (folderBrowser.ShowDialog() == DialogResult.OK)
                        {
                            textBoxConfirmationMessage.Text = Path.GetDirectoryName(folderBrowser.FileName).Substring(soundPackLocation.Length).TrimStart('\\');
                        }
                    }
                }
            }
            catch (Exception)
            {
                return;
            }
        }
        private void buttonAddNewMacro_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxAddNewMacro.Text) || string.IsNullOrWhiteSpace(textBoxVoiceTriggers.Text))
            {
                MessageBox.Show(Configuration.getUIString("empty_name_or_voicetrigger"));
                return;
            }

            if (canAddVoiceTrigger(radioButtonEditSelectedMacro.Checked))
            {
                List<Macro> currentMacros = macroContainer.macros.ToList();
                if (radioButtonAddNewMacro.Checked)
                {
                    Macro macro = new Macro();
                    macro.name = textBoxAddNewMacro.Text;
                    macro.description = textBoxDescription.Text;
                    if (radioButtonRegularVoiceTrigger.Checked)
                    {
                        macro.voiceTriggers = textBoxVoiceTriggers.Lines;
                        macro.integerVariableVoiceTrigger = null;
                    }
                    else
                    {
                        macro.integerVariableVoiceTrigger = textBoxVoiceTriggers.Text;
                        macro.voiceTriggers = null;
                    }
                    if (textBoxConfirmationMessage.Text.Length > 0)
                    {
                        macro.confirmationMessage = textBoxConfirmationMessage.Text.Replace('\\', '/');
                    }
                    currentMacros.Add(macro);
                    macroContainer.macros = currentMacros.ToArray();
                    radioButtonViewOnly.Checked = true;
                    saveMacroSettings();
                    updateMacroList(true);

                }
                else if (listBoxAvailableMacros.SelectedIndex != -1)
                {
                    Macro currentMacro = macroContainer.macros.FirstOrDefault(mc => mc.name == listBoxAvailableMacros.SelectedItem.ToString());
                    currentMacro.name = textBoxAddNewMacro.Text;
                    currentMacro.description = textBoxDescription.Text;
                    if (radioButtonRegularVoiceTrigger.Checked)
                    {
                        currentMacro.voiceTriggers = textBoxVoiceTriggers.Lines;
                        currentMacro.integerVariableVoiceTrigger = null;
                    }
                    else
                    {
                        currentMacro.integerVariableVoiceTrigger = textBoxVoiceTriggers.Text;
                        currentMacro.voiceTriggers = null;
                    }
                    if (textBoxConfirmationMessage.Text.Length > 0)
                    {
                        currentMacro.confirmationMessage = textBoxConfirmationMessage.Text.Replace('\\', '/');
                    }
                    radioButtonViewOnly.Checked = true;
                    saveMacroSettings();
                    updateMacroList(false);
                }
            }
        }
        private bool canAddVoiceTrigger(bool editExisting)
        {
            // also make sure we dont have any existing voice triggers with same name(s)

            if (!editExisting)
            {
                foreach (var m in macroContainer.macros)
                {
                    if (m.voiceTriggers != null)
                    {
                        foreach (var voiceTrigger in m.voiceTriggers)
                        {
                            if (textBoxVoiceTriggers.Lines.Contains(voiceTrigger))
                            {
                                MessageBox.Show(Configuration.getUIString("macro_voicetrigger_already_exists"));
                                return false;
                            }
                        }
                    }
                }
            }
            else
            {
                foreach (var m in macroContainer.macros.Where(m => m.name != textBoxAddNewMacro.Text))
                {
                    if (m.voiceTriggers != null)
                    {
                        foreach (var voiceTrigger in m.voiceTriggers)
                        {
                            if (textBoxVoiceTriggers.Lines.Contains(voiceTrigger))
                            {
                                MessageBox.Show(Configuration.getUIString("macro_voicetrigger_already_exists"));
                                return false;
                            }
                        }
                    }
                }
            }
            return true;
        }
        private void updateMacroList(bool setToEnd = false, bool retainIndex = true)
        {
            listBoxAvailableMacros.Items.Clear();
            if (macroContainer != null && macroContainer.macros != null)
            {
                foreach (var macro in macroContainer.macros)
                {
                    listBoxAvailableMacros.Items.Add(macro.name);
                }
                if (setToEnd)
                {
                    listBoxAvailableMacros.SetSelected(listBoxAvailableMacros.Items.Count - 1, true);
                }
                else
                {
                    listBoxAvailableMacros.SetSelected(0, true);
                }
            }
        }

        private void buttonAddActionSequence_Click(object sender, EventArgs e)
        {
            if(listBoxAvailableMacros.SelectedIndex == -1)
            {
                MessageBox.Show(Configuration.getUIString("must_select_a_macro"));
                return;
            }
            if (string.IsNullOrWhiteSpace(textBoxActionSequence.Text))
            {
                MessageBox.Show(Configuration.getUIString("action_sequence_cant_be_empty"));
                return;
            }
            if(string.IsNullOrWhiteSpace(textBoxWaitBetweenEachCommand.Text))
            {
                MessageBox.Show(labelWaitBetweenEachCommand.Text + " " + Configuration.getUIString("empty_wait_time_end"));
                return;
            }
            int currentSelectedMacroIndex = listBoxAvailableMacros.SelectedIndex;
            var currentSelectedGame = availableMacroGames[listBoxGames.SelectedIndex];
            Macro currentMacro = macroContainer.macros.FirstOrDefault(mc => mc.name == listBoxAvailableMacros.Items[listBoxAvailableMacros.SelectedIndex].ToString());
            if (currentMacro == null)
            {
                return;
            }
            List<CommandSet> currentCommandSets = new List<CommandSet>();
            if (currentMacro.commandSets != null)
            {
                currentCommandSets = currentMacro.commandSets.ToList();
            }
            CommandSet currentCommandSet = currentCommandSets.FirstOrDefault(cs => cs.gameDefinition == currentSelectedGame.gameEnum.ToString());
            List<string> actions = textBoxActionSequence.Lines.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            var isFreeText = actions.Where(s => s.Contains($"{{{MacroManager.FREE_TEXT_IDENTIFIER}}}"));
            if (currentCommandSet == null)
            {
                currentCommandSet = new CommandSet();
                currentCommandSet.gameDefinition = currentSelectedGame.gameEnum.ToString();
                if (!string.IsNullOrWhiteSpace(textBoxGameMacroDescription.Text))
                {
                    currentCommandSet.description = textBoxGameMacroDescription.Text;
                }
                else
                {
                    currentCommandSet.description = currentSelectedGame.friendlyName + " version";
                }
                currentCommandSet.actionSequence = actions.ToArray();
                if (string.IsNullOrWhiteSpace(textBoxKeyPressTime.Text))
                {
                    currentCommandSet.keyPressTime = null;
                }
                else
                {
                    currentCommandSet.keyPressTime = int.Parse(textBoxKeyPressTime.Text);
                }
                currentCommandSet.waitBetweenEachCommand = int.Parse(textBoxWaitBetweenEachCommand.Text);
                currentCommandSets.Add(currentCommandSet);
                currentMacro.commandSets = currentCommandSets.ToArray();
                if (isFreeText != null)
                {
                    currentCommandSet.autoExecuteStartChatMacro = autoExecuteStartMacro.Checked;
                    currentCommandSet.autoExecuteEndChatMacro = autoExecuteEndMacro.Checked;
                }
                saveMacroSettings();
                updateMacroList(false);
                listBoxAvailableMacros.SetSelected(currentSelectedMacroIndex, true);

            }
            else
            {
                currentCommandSet.gameDefinition = currentSelectedGame.gameEnum.ToString();
                currentCommandSet.actionSequence = actions.ToArray();
                if (string.IsNullOrWhiteSpace(textBoxKeyPressTime.Text))
                {
                    currentCommandSet.keyPressTime = null;
                }
                else
                {
                    currentCommandSet.keyPressTime = int.Parse(textBoxKeyPressTime.Text);
                }
                currentCommandSet.waitBetweenEachCommand = int.Parse(textBoxWaitBetweenEachCommand.Text);
                if (!string.IsNullOrWhiteSpace(textBoxGameMacroDescription.Text))
                {
                    currentCommandSet.description = textBoxGameMacroDescription.Text;
                }
                else
                {
                    currentCommandSet.description = currentSelectedGame.friendlyName + " version";
                }
                if (isFreeText != null)
                {
                    currentCommandSet.autoExecuteStartChatMacro = autoExecuteStartMacro.Checked;
                    currentCommandSet.autoExecuteEndChatMacro = autoExecuteEndMacro.Checked;
                }
                saveMacroSettings();
                updateMacroList(false);
                listBoxAvailableMacros.SetSelected(currentSelectedMacroIndex, true);
            }
        }

        private void buttonDeleteSelectedMacro_Click(object sender, EventArgs e)
        {
            if (listBoxAvailableMacros.SelectedIndex == -1)
            {
                return;
            }
            if (MessageBox.Show(Configuration.getUIString("delete_selected_macro_confirmation"), Configuration.getUIString("delete_selected_macro_confirmation_title") + " " + listBoxAvailableMacros.SelectedItem.ToString(),
                MessageBoxButtons.OKCancel) == System.Windows.Forms.DialogResult.OK)
            {
                macroContainer.macros = macroContainer.macros.Where(val => val.name != listBoxAvailableMacros.SelectedItem.ToString()).ToArray();
                saveMacroSettings();
                updateMacroList(false);
            }

        }

        private void saveMacroSettings()
        {
            MacroManager.saveCommands(macroContainer);
        }

        private void buttonLoadUserMacroSettings_Click(object sender, EventArgs e)
        {
            macroContainer = MacroManager.loadCommands(MacroManager.getMacrosFileLocation());
            updateMacroList(false);
        }

        private void buttonLoadDefaultMacroSettings_Click(object sender, EventArgs e)
        {
            macroContainer = MacroManager.loadCommands(MacroManager.getMacrosFileLocation(true));
            updateMacroList(false);
        }

        private void radioButtonAvailableActions_CheckedChanged(object sender, EventArgs e)
        {
            buttonAddSelectedKeyToSequence.Enabled = true;
            textBoxDescription.ShortcutsEnabled = false;
            comboBoxKeySelection.Enabled = true;
            comboBoxModifierKeySelection.Enabled = false;
            autoExecuteStartMacro.Enabled = false;
            autoExecuteEndMacro.Enabled = false;
            if (radioButtonRegularKeyAction.Checked)
            {
                textBoxSpecialActionParameter.Enabled = false;
                comboBoxModifierKeySelection.Enabled = false;
                textBoxSpecialActionParameter.Text = "";
                labelSpecialActionParameter.Text = "";
            }
            else if (radioButtonMultipleKeyAction.Checked)
            {
                textBoxSpecialActionParameter.Enabled = true;
                textBoxSpecialActionParameter.Text = "";
                labelSpecialActionParameter.Text = Configuration.getUIString("action_repeated_key_presses");
            }
            else if (radioButtonMultipleFuelAction.Checked)
            {
                textBoxSpecialActionParameter.Enabled = false;
                textBoxSpecialActionParameter.Text = "{MULTIPLE,Fuel}";
                labelSpecialActionParameter.Text = "";
            }
            else if (radioButtonMultipleVoiceTrigger.Checked)
            {
                textBoxSpecialActionParameter.Enabled = false;
                textBoxSpecialActionParameter.Text = "{MULTIPLE,VOICE_TRIGGER}";
                labelSpecialActionParameter.Text = "";
            }
            else if (radioButtonWaitAction.Checked)
            {
                textBoxSpecialActionParameter.Enabled = true;
                textBoxSpecialActionParameter.Text = "";
                comboBoxKeySelection.Enabled = false;
                labelSpecialActionParameter.Text = Configuration.getUIString("action_wait_time");
            }
            else if (radioButtonFreeTextAction.Checked)
            {
                textBoxSpecialActionParameter.Enabled = true;
                textBoxSpecialActionParameter.Text = "";
                comboBoxKeySelection.Enabled = false;
                autoExecuteStartMacro.Enabled = true;
                autoExecuteEndMacro.Enabled = true;
                labelSpecialActionParameter.Text = Configuration.getUIString("action_free_text");
            }
            else if(radioButtonModifierAndKey.Checked)
            {
                comboBoxModifierKeySelection.Enabled = true;
                textBoxSpecialActionParameter.Enabled = false;
                textBoxSpecialActionParameter.Text = "+";
                labelSpecialActionParameter.Text = "";

            }
            else if (radioButtonAdvancedEditAction.Checked)
            {
                textBoxSpecialActionParameter.Enabled = false;
                comboBoxKeySelection.Enabled = false;
                textBoxSpecialActionParameter.Text = "";
                labelSpecialActionParameter.Text = "";
                textBoxActionSequence.Enabled = true;
                buttonAddSelectedKeyToSequence.Enabled = false;
                textBoxDescription.ShortcutsEnabled = true;
                autoExecuteStartMacro.Enabled = true;
                autoExecuteEndMacro.Enabled = true;
            }

        }

        private void textBoxSpecialActionParameter_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (radioButtonWaitAction.Checked || radioButtonMultipleKeyAction.Checked)
            {
                if (!char.IsDigit(e.KeyChar))
                    e.Handled = true;         //Just Digits
                if (e.KeyChar == (char)8)
                    e.Handled = false;        //Allow Backspace
            }

        }

        private void radioButtonAvailableMacros_CheckedChanged(object sender, EventArgs e)
        {
            buttonDeleteSelectedMacro.Enabled = false;
            groupBoxGameSettings.Enabled = false;
            if (radioButtonViewOnly.Checked)
            {
                buttonAddNewMacro.Enabled = false;
                textBoxAddNewMacro.Enabled = false;
                groupBoxGlobalMacroVoiceTrigger.Enabled = false;
                buttonSelectConfirmationMessage.Enabled = false;
                textBoxDescription.ShortcutsEnabled = false;
                groupBoxGameSettings.Enabled = true;
            }
            else
            {
                buttonAddNewMacro.Enabled = true;
                textBoxAddNewMacro.Enabled = true;
                groupBoxGlobalMacroVoiceTrigger.Enabled = true;
                buttonSelectConfirmationMessage.Enabled = true;
                textBoxDescription.ShortcutsEnabled = true;
            }
            if (radioButtonAddNewMacro.Checked)
            {
                listBoxAvailableMacros.ClearSelected();
                listBoxAvailableMacros.Enabled = false;
                textBoxConfirmationMessage.Text = "";
            }
            else
            {
                listBoxAvailableMacros.Enabled = true;
            }
            if(radioButtonEditSelectedMacro.Checked)
            {
                buttonDeleteSelectedMacro.Enabled = true;
            }

        }

        private void textBoxDescription_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (radioButtonViewOnly.Checked)
            {
                e.Handled = true;
            }
        }

        private void textBoxActionSequence_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!radioButtonAdvancedEditAction.Checked)
            {
                e.Handled = true;
            }
        }

        private void buttonUndoLastAction_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(textBoxActionSequence.Text))
            {
                textBoxActionSequence.Lines = textBoxActionSequence.Lines.Take(textBoxActionSequence.Lines.Count() - 1).ToArray();
            }
        }

        private void controllersList_SelectedIndexChanged(object sender, EventArgs e)
        {
            updateAssignmentElements(false);
        }

        private void updateControllersList()
        {
            this.controllersList.Items.Clear();
            controllerGuids.Clear();

            var controllerEntries = new List<Tuple<ControllerConfiguration.ControllerData, bool>>();
            foreach (ControllerConfiguration.ControllerData configData in this.controllerConfiguration.controllers)
            {
                controllerEntries.Add(Tuple.Create(configData, true));
            }

            // Include previously seen controllers which are currently disconnected.
            foreach (ControllerConfiguration.ControllerData configData in this.controllerConfiguration.knownControllers)
            {
                if (!this.controllerConfiguration.controllers.Exists(c => c.guid == configData.guid))
                {
                    controllerEntries.Add(Tuple.Create(configData, false));
                }
            }

            foreach (var entry in controllerEntries
                .OrderByDescending(e =>
                {
                    ControllerNames.ControllerNamesData names = ControllerNames.GetControllerNames(e.Item1.guid);
                    return names != null && names.buttons != null &&
                        names.buttons.Values.Any(name => !string.IsNullOrWhiteSpace(name));
                })
                .ThenBy(e => ControllerNames.GetControllerDisplayName(e.Item1.guid, e.Item1.deviceName),
                    StringComparer.CurrentCultureIgnoreCase))
            {
                string name = ControllerNames.GetControllerDisplayName(entry.Item1.guid, entry.Item1.deviceName);
                this.controllersList.Items.Add(new MainWindow.ControllerUiEntry(name, isConnected: entry.Item2));
                controllerGuids.Add(entry.Item1.guid);
            }
        }

        private void addAssignment_Click(object sender, EventArgs e)
        {
            if (!this.isAssigningButton)
            {
                if (this.controllersList.SelectedIndex >= 0)
                {
                    isAssigningButton = true;
                    this.addAssignmentButton.Text = Configuration.getUIString("waiting_for_button_click_to_cancel");
                    ThreadStart assignButtonWork = assignButton;
                    ThreadManager.UnregisterTemporaryThread(assignButtonThread);
                    assignButtonThread = new Thread(assignButtonWork);
                    assignButtonThread.Name = "MacroWindow.assignButtonThread";
                    ThreadManager.RegisterTemporaryThread(assignButtonThread);
                    assignButtonThread.Start();
                }
            }
            else
            {
                isAssigningButton = false;
                controllerConfiguration.listenForAssignment = false;
                this.addAssignmentButton.Text = Configuration.getUIString("assign_control");
            }
        }

        private void assignButton()
        {
            ButtonAssignment buttonAssignment = new ButtonAssignment();
            int selectedIndex = this.controllersList.SelectedIndex;
            int controllerIndex = -1;
            if (selectedIndex >= 0 && selectedIndex < controllerGuids.Count)
            {
                Guid selectedGuid = controllerGuids[selectedIndex];
                controllerIndex = controllerConfiguration.controllers.FindIndex(c => c.guid == selectedGuid);
            }

            if (controllerIndex >= 0 &&
                controllerConfiguration.assignButton(this, controllerIndex, buttonAssignment))
            {
                Log.Commentary("Macro Editor button detected: " + buttonAssignment.controller.deviceName
                    + " (" + buttonAssignment.deviceGuid + "), button " + buttonAssignment.buttonIndex);
                // update the macro with the controller guid and button id
                isAssigningButton = false;
                Macro currentMacro = macroContainer.macros.FirstOrDefault(mc => mc.name == listBoxAvailableMacros.Items[listBoxAvailableMacros.SelectedIndex].ToString());
                if (currentMacro != null)
                {
                    if (currentMacro.buttonTriggers == null || currentMacro.buttonTriggers.Length == 0)
                    {
                        currentMacro.buttonTriggers = new ButtonTrigger[] { new ButtonTrigger() };
                    }
                    currentMacro.buttonTriggers[0].buttonIndex = buttonAssignment.buttonIndex;
                    currentMacro.buttonTriggers[0].deviceId = buttonAssignment.deviceGuid;
                    currentMacro.buttonTriggers[0].description = buttonAssignment.controller.deviceName;
                    saveMacroSettings();
                    updateAssignmentElements(true);
                }
            }
            try
            {
                this.Invoke((MethodInvoker)delegate
                {
                    this.addAssignmentButton.Text = Configuration.getUIString("assign_control");
                });
            }
            catch (Exception)
            {
                // Shutdown.
            }
        }

        private void deleteAssignment_Click(object sender, EventArgs e)
        {
            Macro currentMacro = macroContainer.macros.FirstOrDefault(mc => mc.name == listBoxAvailableMacros.Items[listBoxAvailableMacros.SelectedIndex].ToString());
            if (currentMacro != null && currentMacro.buttonTriggers != null && currentMacro.buttonTriggers.Length > 0)
            {
                bool remove = false;
                int i = 0;
                // remove the loaded button assignment for this macro. Here we match on macro name, which is kind of nasty. Note that if we move button assignments
                // to be in the per-game element this won't work
                for (; i < this.controllerConfiguration.buttonAssignments.Count; i++)
                {
                    if (this.controllerConfiguration.buttonAssignments[i].executableCommandMacro != null
                        && this.controllerConfiguration.buttonAssignments[i].executableCommandMacro.macro.name == currentMacro.name)
                    {
                        remove = true;
                        break;
                    }
                }
                if (remove)
                {
                    this.controllerConfiguration.buttonAssignments.RemoveAt(i);
                }
                currentMacro.buttonTriggers = null;
                saveMacroSettings();
                updateAssignmentElements(true);
            }
        }

        private void MacroEditor_Load(object sender, EventArgs e)
        {

        }

        private void radioButtonDosCommand_CheckedChanged(object sender, EventArgs e)
        {
            textBoxSpecialActionParameter.Enabled = true;
            textBoxSpecialActionParameter.Text = "";
            comboBoxKeySelection.Enabled = false;
            textBoxSpecialActionParameter.Text = "";
            labelSpecialActionParameter.Text = Configuration.getUIString("dos_command_action");
        }

        private void radioButtonRf2HwControl_CheckedChanged(object sender, EventArgs e)
        {
            textBoxSpecialActionParameter.Enabled = true;
            textBoxSpecialActionParameter.Text = "";
            comboBoxKeySelection.Enabled = false;
            textBoxSpecialActionParameter.Text = "";
            labelSpecialActionParameter.Text = Configuration.getUIString("rf2_hw_control_string_action");
        }
    }
}
