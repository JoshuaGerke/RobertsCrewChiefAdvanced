using System;
using System.Drawing;
using System.Windows.Forms;

using WebSocketSharp;

namespace CrewChiefV4
{
    /// The main window is a set of nested tableLayoutPanels. These classes show and hide parts of it.
    /// The layout is as follows:
    /// tableLayoutPanelMain is 5 rows by 2 columns. Column 2 tableLayoutPanelTestSounds is usually hidden (that maybe a dumb design, that should be part of row 2).
    /// * Row 0 is the top row with the menu strip.
    /// * Row 1 tableLayoutPanelTop has 6 columns and 2 rows.
    ///   * Column 0 groupBoxTrace is the trace window, usually hidden by setting its width to 0.
    ///   * Column 1 tableLayoutPanel2
    ///     * tableLayoutPanelToDockGameDefinitionList
    ///     *   groupBoxGame
    ///     * the Start button
    ///     * Fuel multiplier
    ///   * Column 2 tableLayoutPanelMessagesVolume is the messages volume.
    ///   * Column 3 tableLayoutPanelBackgroundVolume is the background volume and tableLayoutPanelSpeechrecognitionDevice.
    ///   * Column 4 tableLayoutPanelActorsSRmode has 4 rows
    ///     * Row 0 buttonMyName.
    ///     * Row 1 chiefNameBox.
    ///     * Row 2 spotterNameBox.
    ///     * Row 3 comboBoxSpeechRecognitionModes (not used).
    ///   * Column 5 tableLayoutPanelButtons has 3 buttons: propertiesButton, buttonVRWindowSettings and buttonEditCommandMacros.
    /// * Row 2 groupBoxConsoleWindow. Usually this spans over tableLayoutPanelMain column 2 (sound test window) and row 3 (sound update).
    /// * Row 3 groupBoxSoundUpdateEtc is the sound update window.
    /// * Row 4 tableLayoutPanelBottom is 4 columns.
    ///   * Column 0 is groupBoxAvailableControllers.
    ///   * Column 1 is groupBoxAssignedActions.
    ///   * Column 2 is tableLayoutPanelSrModeAndBackgroundDevice
    ///     * Row 0 groupBoxVoiceRecognitionMode. This can span over rows 1 and 2.
    ///     * Row 1 backgroundAudioDeviceLabel if shown.
    ///     * Row 2 backgroundAudioDeviceBox if shown.
    ///   * Column 3 is groupBoxUpdates and donateLink.

    /// <summary>
    /// Control the main window layout, showing or hiding parts of it.
    /// </summary>
    public class MainWindowLayout
    {
        private MainWindow mw;

        public MainWindowLayout(MainWindow _mw)
        {
            mw = _mw;
            if (CrewChief.Debug.ShowMainLayout)
            {
                mw.borders(mw, BorderStyle.None, TableLayoutPanelCellBorderStyle.Single);
            }
            hideTraceWindow();
            hideFuelMultiplier();
            hideSoundUpdate();
            hideSoundTest();
            hideBackgroundDeviceWindow();
        }

        // The console window may be half height and/or
        // half width by changing the row and column span it uses.

        /// <summary>
        /// Show the sound pack update buttons.
        /// </summary>
        internal void showSoundUpdate()
        {
            mw.SuspendLayout();
            mw.groupBoxSoundUpdateEtc.Visible = true;
            mw.tableLayoutPanelMain.SetRowSpan(mw.groupBoxConsoleWindow, 1);
            mw.buttonSoundPackUpdate.ForeColor = Color.Green;
            mw.buttonSoundPackUpdate.Text = Configuration.getUIString("an_updated_sound_pack_available_press_to_download");
            mw.ResumeLayout();
        }

        /// <summary>
        /// Hide the sound pack update buttons.
        /// </summary>
        internal void hideSoundUpdate()
        {
            mw.SuspendLayout();
            mw.groupBoxSoundUpdateEtc.Visible = false;
            mw.tableLayoutPanelMain.SetRowSpan(mw.groupBoxConsoleWindow, 2);
            mw.buttonSoundPackUpdate.ForeColor = Color.Gray;
            mw.buttonSoundPackUpdate.Text = Configuration.getUIString("sound_packs_are_up_to_date");
            mw.ResumeLayout();
        }

        internal void showSoundTest()
        {
            mw.SuspendLayout();
            mw.tableLayoutPanelTestSounds.Visible = true;
            mw.groupBoxSoundTest.Visible = true;
            mw.tableLayoutPanelMain.ColumnCount = 2;
            mw.tableLayoutPanelMain.SetColumnSpan(mw.groupBoxConsoleWindow, 1);
            mw.ResumeLayout();
        }

        internal void hideSoundTest()
        {
            mw.SuspendLayout();
            mw.groupBoxSoundTest.Visible = false;
            mw.tableLayoutPanelMain.ColumnCount = 1;
            mw.ResumeLayout();
        }

        private int saveBackgroundVolumeWidth = 20;

        /// <summary>
        /// Hide or show the trace window in tableLayoutPanelTop.
        /// </summary>
        internal void showTraceWindow()
        {
            mw.groupBoxTrace.Visible = true;
            // Hide the background volume column.  Maybe
            saveBackgroundVolumeWidth = (int)mw.tableLayoutPanelTop.ColumnStyles[3].Width;
            //mw.tableLayoutPanelTop.ColumnStyles[3].Width = 0;
            //mw.tableLayoutPanelTop.ColumnStyles[3].SizeType = SizeType.Percent;
            mw.tableLayoutPanelTop.ColumnStyles[0].Width = 20;
            mw.tableLayoutPanelTop.ColumnStyles[0].SizeType = SizeType.Percent;
        }

        internal void hideTraceWindow()
        {
            mw.groupBoxTrace.Visible = false;
            mw.tableLayoutPanelTop.ColumnStyles[3].Width = saveBackgroundVolumeWidth;
            mw.tableLayoutPanelTop.ColumnStyles[3].SizeType = SizeType.Percent;
            mw.tableLayoutPanelTop.ColumnStyles[0].Width = 0;
            mw.tableLayoutPanelTop.ColumnStyles[0].SizeType = SizeType.Percent;
        }

        /// <summary>
        /// Hide or show the speech input device selector in tableLayoutPanelSrModeAndBackgroundDevice.
        /// </summary>
        internal void showInputDeviceSelector()
        {
            mw.speechRecognitionDeviceBox.Enabled = true;
            mw.speechRecognitionDeviceBox.Visible = true;
            mw.speechRecognitionDeviceLabel.Visible = true;
            mw.speechRecognitionDeviceLabel.Size = new System.Drawing.Size(166, 13);
            mw.speechRecognitionDeviceLabel.Text = Configuration.getUIString("speech_recognition_device_label");
        }

        /// <summary>
        /// If not use_naudio_for_speech_recognition show the Windows
        /// default speech input device
        /// </summary>
        /// <param name="deviceName"></param>
        internal void refreshDefaultSpeechRecognitionDevice(string deviceName)
        {
            mw.speechRecognitionDeviceBox.Items.Clear();
            if (deviceName.IsNullOrEmpty())
            {
                deviceName = Configuration.getUIString("no_speech_recognition_device");
            }
            mw.speechRecognitionDeviceBox.Items.Add(deviceName);
            mw.speechRecognitionDeviceBox.SelectedIndex = 0;
            mw.sizeComboBox(mw.speechRecognitionDeviceBox);
        }

        internal void hideInputDeviceSelector()
        {
            mw.speechRecognitionDeviceBox.Enabled = true;
            mw.speechRecognitionDeviceBox.Visible = true;
            mw.speechRecognitionDeviceLabel.Size = new System.Drawing.Size(166, 50);
            mw.speechRecognitionDeviceLabel.Visible = true;
            mw.speechRecognitionDeviceLabel.Enabled = true;
            mw.speechRecognitionDeviceLabel.Text = Configuration.getUIString("speech_recognition_device_label");
            mw.mainWindowTooltip.SetToolTip(mw.speechRecognitionDeviceBox,
                Configuration.getUIStringStrict("no_speech_recognition_tooltip"));
            mw.mainWindowTooltip.SetToolTip(mw.speechRecognitionDeviceLabel,
                Configuration.getUIStringStrict("no_speech_recognition_tooltip"));
        }

        /// <summary>
        /// Hide or show the background device window in tableLayoutPanelSrModeAndBackgroundDevice.
        /// </summary>
        internal void showBackgroundDeviceWindow()
        {
            mw.backgroundAudioDeviceLabel.Visible = true;
            mw.backgroundAudioDeviceBox.Visible = true;
            mw.tableLayoutPanelSrModeAndBackgroundDevice.SetRowSpan(mw.groupBoxVoiceRecognitionMode, 1);
        }

        internal void hideBackgroundDeviceWindow()
        {
            mw.backgroundAudioDeviceLabel.Visible = false;
            mw.backgroundAudioDeviceBox.Visible = false;
            mw.tableLayoutPanelSrModeAndBackgroundDevice.SetRowSpan(mw.groupBoxVoiceRecognitionMode, 3);
        }

        /// <summary>
        /// Hide or show the Fuel Multiplier in tableLayoutPanel2.
        /// </summary>
        internal void showFuelMultiplier()
        {
            mw.tableLayoutPanelFuelMultiplier.Visible = true;
        }

        internal void hideFuelMultiplier()
        {
            mw.tableLayoutPanelFuelMultiplier.Visible = false;
        }

        /// <summary>
        /// Show the rally co-driver and style in place of the chief and spotter.
        /// </summary>
        internal void showRallyMode()
        {
            mw.chiefNameLabel.Visible = false;
            mw.chiefNameBox.Visible = false;
            mw.spotterNameLabel.Visible = false;
            mw.spotterNameBox.Visible = false;
            mw.codriverNameLabel.Visible = true;
            mw.codriverStyleLabel.Visible = true;
            mw.codriverNameBox.Visible = true;
            mw.codriverStyleBox.Visible = true;

            mw.tableLayoutPanelActorsSRmode.Controls.Remove(mw.chiefNameLabel);
            mw.tableLayoutPanelActorsSRmode.Controls.Remove(mw.spotterNameLabel);
            mw.tableLayoutPanelActorsSRmode.Controls.Remove(mw.chiefNameBox);
            mw.tableLayoutPanelActorsSRmode.Controls.Remove(mw.spotterNameBox);
            mw.tableLayoutPanelActorsSRmode.Controls.Add(mw.codriverNameLabel, 0, 1);
            mw.tableLayoutPanelActorsSRmode.Controls.Add(mw.codriverStyleLabel, 0, 2);
            mw.tableLayoutPanelActorsSRmode.Controls.Add(mw.codriverNameBox, 1, 1);
            mw.tableLayoutPanelActorsSRmode.Controls.Add(mw.codriverStyleBox, 1, 2);
        }
    }

    public partial class MainWindow // Partial class because the layout controls are private.
    {
        /// <summary>
        /// Set the width of the drop down list to the width of the longest item.
        /// </summary>
        void sizeComboBoxDropDown(ComboBox cb)
        {
            cb.DropDownWidth = 100;
            foreach (var item in cb.Items)
            {
                var size = TextRenderer.MeasureText(item.ToString(), cb.Font);
                if (size.Width > cb.DropDownWidth)
                {
                    cb.DropDownWidth = size.Width;
                }
            }
        }
        /// <summary>
        /// Set the width of the combo box to the width of the text.
        /// </summary>
        internal void sizeComboBox(ComboBox cb, int maxWidth = 200)
        {
            var size = TextRenderer.MeasureText(cb.Text, cb.Font);
            cb.Width = Math.Min(maxWidth, size.Width + TextRenderer.MeasureText("    ", cb.Font).Width);
            object longestItem = null;
            int maxLength = 0;
            foreach (var item in cb.Items)
            {
                int length = item?.ToString().Length ?? 0;
                if (length > maxLength)
                {
                    maxLength = length;
                    longestItem = item;
                }
            }
            size = TextRenderer.MeasureText(longestItem.ToString(), cb.Font);
            cb.DropDownWidth = size.Width + TextRenderer.MeasureText("    ", cb.Font).Width;
        }

        // This may be useful. Or not.
        public void MainWindowLayoutLiveDocumentation(MainWindow mw)
        {
            // grep this.tableLayoutPanel.*.Controls.Add MainWindow.Designer.cs
            // and then edit.
            // To check it's correct you _could_ clear these tableLayoutPanels and 
            // call this method to rebuild them.
            mw.tableLayoutPanelMain.Controls.Add(mw.tableLayoutPanelTop, 0, 1);
            {
                mw.tableLayoutPanelTop.Controls.Add(mw.groupBoxTrace, 0, 0);
                mw.tableLayoutPanelTop.Controls.Add(mw.tableLayoutPanelGameStart, 1, 0);
                {
                    mw.tableLayoutPanelGameStart.Controls.Add(mw.groupBoxGame, 0, 0);
                    mw.tableLayoutPanelGameStart.Controls.Add(mw.startApplicationButton, 0, 1);
                    mw.tableLayoutPanelGameStart.Controls.Add(mw.tableLayoutPanelFuelMultiplier, 0, 2);
                    {
                        mw.tableLayoutPanelFuelMultiplier.Controls.Add(mw.fuelMultiplierLabel, 0, 0);
                        mw.tableLayoutPanelFuelMultiplier.Controls.Add(mw.numericUpDownfuelMultiplier, 1, 0);
                    }
                }
                mw.tableLayoutPanelTop.Controls.Add(mw.tableLayoutPanelActorsSRmode, 4, 0);
                {
                    mw.tableLayoutPanelActorsSRmode.Controls.Add(mw.buttonMyName, 0, 0);
                    mw.tableLayoutPanelActorsSRmode.Controls.Add(mw.chiefNameBox, 1, 1);
                    mw.tableLayoutPanelActorsSRmode.Controls.Add(mw.chiefNameLabel, 0, 1);
                    mw.tableLayoutPanelActorsSRmode.Controls.Add(mw.spotterNameBox, 1, 2);
                    mw.tableLayoutPanelActorsSRmode.Controls.Add(mw.spotterNameLabel, 0, 2);
                    mw.tableLayoutPanelActorsSRmode.Controls.Add(mw.tableLayoutPanelBackgroundDevice, 0, 3);
                    {
                        mw.tableLayoutPanelBackgroundDevice.Controls.Add(mw.backgroundAudioDeviceBox, 0, 1);
                        mw.tableLayoutPanelBackgroundDevice.Controls.Add(mw.backgroundAudioDeviceLabel, 0, 0);
                    }
                }
                mw.tableLayoutPanelTop.Controls.Add(mw.tableLayoutPanelBackgroundVolume, 3, 0);
                {
                    mw.tableLayoutPanelBackgroundVolume.Controls.Add(mw.backgroundVolumeSlider, 0, 1);
                    mw.tableLayoutPanelBackgroundVolume.Controls.Add(mw.backgroundVolumeSliderLabel, 0, 0);
                }
                mw.tableLayoutPanelTop.Controls.Add(mw.tableLayoutPanelButtons, 5, 0);
                {
                    mw.tableLayoutPanelButtons.Controls.Add(mw.buttonEditCommandMacros, 0, 2);
                    mw.tableLayoutPanelButtons.Controls.Add(mw.buttonVRWindowSettings, 0, 1);
                    mw.tableLayoutPanelButtons.Controls.Add(mw.propertiesButton, 0, 0);
                }
                mw.tableLayoutPanelTop.Controls.Add(mw.tableLayoutPanelMessagesDevice, 2, 1);
                {
                    mw.tableLayoutPanelMessagesDevice.Controls.Add(mw.messagesAudioDeviceBox, 0, 1);
                    mw.tableLayoutPanelMessagesDevice.Controls.Add(mw.messagesAudioDeviceLabel, 0, 0);
                }
                mw.tableLayoutPanelTop.Controls.Add(mw.tableLayoutPanelMessagesVolume, 2, 0);
                {
                    mw.tableLayoutPanelMessagesVolume.Controls.Add(mw.messagesVolumeSlider, 0, 1);
                    mw.tableLayoutPanelMessagesVolume.Controls.Add(mw.messagesVolumeSliderLabel, 0, 0);
                }
                mw.tableLayoutPanelTop.Controls.Add(mw.tableLayoutPanelSpeechrecognitionDevice, 3, 1);
                {
                    mw.tableLayoutPanelSpeechrecognitionDevice.Controls.Add(mw.speechRecognitionDeviceBox, 0, 1);
                    mw.tableLayoutPanelSpeechrecognitionDevice.Controls.Add(mw.speechRecognitionDeviceLabel, 0, 0);
                }
            }
            mw.tableLayoutPanelMain.Controls.Add(mw.groupBoxConsoleWindow, 0, 2);
            mw.tableLayoutPanelMain.Controls.Add(mw.groupBoxSoundTest, 1, 2);
            mw.tableLayoutPanelMain.Controls.Add(mw.groupBoxSoundUpdateEtc, 0, 3);
            mw.tableLayoutPanelMain.Controls.Add(mw.tableLayoutPanelBottom, 0, 4);
            {
                mw.tableLayoutPanelBottom.Controls.Add(mw.groupBoxAssignedActions, 1, 0);
                mw.tableLayoutPanelBottom.Controls.Add(mw.groupBoxAvailableControllers, 0, 0);
                mw.tableLayoutPanelBottom.Controls.Add(mw.tableLayoutPanelSrModeAndBackgroundDevice, 2, 0);
                {
                    mw.tableLayoutPanelSrModeAndBackgroundDevice.Controls.Add(mw.groupBoxVoiceRecognitionMode, 0, 0);
                }
                mw.tableLayoutPanelBottom.Controls.Add(mw.tableLayoutPanelUpdatesAndDonate, 3, 0);
                {
                    mw.tableLayoutPanelUpdatesAndDonate.Controls.Add(mw.donateLink, 0, 1);
                    mw.tableLayoutPanelUpdatesAndDonate.Controls.Add(mw.groupBoxUpdates, 0, 0);
                }
            }



            mw.tableLayoutPanelTrace.Controls.Add(mw.filenameLabel, 0, 1);
            mw.tableLayoutPanelTrace.Controls.Add(mw.filenameTextbox, 0, 2);
            mw.tableLayoutPanelTrace.Controls.Add(mw.labelPlaybackSpeed, 0, 3);
            mw.tableLayoutPanelTrace.Controls.Add(mw.playbackInterval, 0, 4);
            mw.tableLayoutPanelTrace.Controls.Add(mw.recordSession, 0, 0);

            mw.tableLayoutPanelControllers.Controls.Add(mw.controllersList, 0, 0);
            mw.tableLayoutPanelControllers.Controls.Add(mw.scanControllers, 0, 1);

            mw.tableLayoutPanelActions.Controls.Add(mw.AddRemoveActions, 2, 1);
            mw.tableLayoutPanelActions.Controls.Add(mw.assignButtonToAction, 0, 1);
            mw.tableLayoutPanelActions.Controls.Add(mw.buttonActionSelect, 0, 0);
            mw.tableLayoutPanelActions.Controls.Add(mw.deleteAssigmentButton, 1, 1);

            mw.tableLayoutPanelActors.Controls.Add(mw.codriverNameLabel, 5, 0);
            mw.tableLayoutPanelActors.Controls.Add(mw.codriverStyleLabel, 5, 1);
            mw.tableLayoutPanelActors.Controls.Add(mw.comboBoxSpeechRecognitionModes, 4, 1);
            mw.tableLayoutPanelActors.Controls.Add(mw.downloadDriverNamesButton, 2, 0);
            mw.tableLayoutPanelActors.Controls.Add(mw.downloadPersonalisationsButton, 3, 0);
            mw.tableLayoutPanelActors.Controls.Add(mw.downloadSoundPackButton, 1, 0);
            mw.tableLayoutPanelActors.Controls.Add(mw.driverNamesProgressBar, 2, 1);
            mw.tableLayoutPanelActors.Controls.Add(mw.labelSpeechRecognitionMode, 4, 0);
            mw.tableLayoutPanelActors.Controls.Add(mw.personalisationsProgressBar, 3, 1);
            mw.tableLayoutPanelActors.Controls.Add(mw.soundPackProgressBar, 1, 1);

            mw.tableLayoutPanelTestSounds.Controls.Add(mw.buttonSmokeTest, 0, 1);
            mw.tableLayoutPanelTestSounds.Controls.Add(mw.smokeTestTextBox, 0, 0);

            mw.tableLayoutPanelUpdates.Controls.Add(mw.app_version, 0, 0);
            mw.tableLayoutPanelUpdates.Controls.Add(mw.buttonSoundPackUpdate, 0, 2);
            mw.tableLayoutPanelUpdates.Controls.Add(mw.forceVersionCheckButton, 0, 1);

            mw.tableLayoutPanelVoiceRecognitionModes.Controls.Add(mw.alwaysOnButton, 0, 3);
            mw.tableLayoutPanelVoiceRecognitionModes.Controls.Add(mw.holdButton, 0, 1);
            mw.tableLayoutPanelVoiceRecognitionModes.Controls.Add(mw.listenIfNotPressedButton, 0, 5);
            mw.tableLayoutPanelVoiceRecognitionModes.Controls.Add(mw.toggleButton, 0, 2);
            mw.tableLayoutPanelVoiceRecognitionModes.Controls.Add(mw.triggerWordButton, 0, 4);
            mw.tableLayoutPanelVoiceRecognitionModes.Controls.Add(mw.voiceDisableButton, 0, 0);
        }

        /// <summary>
        /// Show or hide the TableLayoutPanels borders.
        /// </summary>
        internal void borders(MainWindow mw, BorderStyle border, TableLayoutPanelCellBorderStyle cellBorder)
        {
            mw.tableLayoutPanelMain.BorderStyle = border;
            mw.tableLayoutPanelTop.BorderStyle = border;
            mw.tableLayoutPanelGameStart.BorderStyle = border;
            mw.tableLayoutPanelFuelMultiplier.BorderStyle = border;
            mw.tableLayoutPanelActorsSRmode.BorderStyle = border;
            mw.tableLayoutPanelBackgroundDevice.BorderStyle = border;
            mw.tableLayoutPanelBackgroundVolume.BorderStyle = border;
            mw.tableLayoutPanelButtons.BorderStyle = border;
            mw.tableLayoutPanelMessagesDevice.BorderStyle = border;
            mw.tableLayoutPanelMessagesVolume.BorderStyle = border;
            mw.tableLayoutPanelSpeechrecognitionDevice.BorderStyle = border;
            mw.tableLayoutPanelBottom.BorderStyle = border;
            mw.tableLayoutPanelSrModeAndBackgroundDevice.BorderStyle = border;
            mw.tableLayoutPanelUpdatesAndDonate.BorderStyle = border;
            mw.tableLayoutPanelTrace.BorderStyle = border;
            mw.tableLayoutPanelControllers.BorderStyle = border;
            mw.tableLayoutPanelActions.BorderStyle = border;
            mw.tableLayoutPanelActors.BorderStyle = border;
            mw.tableLayoutPanelTestSounds.BorderStyle = border;
            mw.tableLayoutPanelUpdates.BorderStyle = border;
            mw.tableLayoutPanelVoiceRecognitionModes.BorderStyle = border;

            mw.tableLayoutPanelMain.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelTop.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelGameStart.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelFuelMultiplier.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelActorsSRmode.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelBackgroundDevice.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelBackgroundVolume.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelButtons.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelMessagesDevice.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelMessagesVolume.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelSpeechrecognitionDevice.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelBottom.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelSrModeAndBackgroundDevice.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelUpdatesAndDonate.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelTrace.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelControllers.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelActions.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelActors.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelTestSounds.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelUpdates.CellBorderStyle = cellBorder;
            mw.tableLayoutPanelVoiceRecognitionModes.CellBorderStyle = cellBorder;
        }
    }
}
