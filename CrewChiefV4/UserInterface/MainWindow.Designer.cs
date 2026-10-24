using System.Windows.Forms;
namespace CrewChiefV4
{
    partial class MainWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.forceVersionCheckButton = new System.Windows.Forms.Button();
            this.buttonActionSelect = new System.Windows.Forms.ListBox();
            this.controllersList = new System.Windows.Forms.ListBox();
            this.assignButtonToAction = new System.Windows.Forms.Button();
            this.deleteAssigmentButton = new System.Windows.Forms.Button();
            this.propertiesButton = new System.Windows.Forms.Button();
            this.messagesAudioDeviceBox = new System.Windows.Forms.ComboBox();
            this.speechRecognitionDeviceBox = new System.Windows.Forms.ComboBox();
            this.backgroundAudioDeviceBox = new System.Windows.Forms.ComboBox();
            this.filenameTextbox = new System.Windows.Forms.TextBox();
            this.filenameLabel = new System.Windows.Forms.Label();
            this.recordSession = new System.Windows.Forms.CheckBox();
            this.playbackInterval = new System.Windows.Forms.TextBox();
            this.app_version = new System.Windows.Forms.Label();
            this.soundPackProgressBar = new System.Windows.Forms.ProgressBar();
            this.downloadSoundPackButton = new System.Windows.Forms.Button();
            this.downloadDriverNamesButton = new System.Windows.Forms.Button();
            this.downloadPersonalisationsButton = new System.Windows.Forms.Button();
            this.driverNamesProgressBar = new System.Windows.Forms.ProgressBar();
            this.personalisationsProgressBar = new System.Windows.Forms.ProgressBar();
            this.spotterNameLabel = new System.Windows.Forms.Label();
            this.messagesAudioDeviceLabel = new System.Windows.Forms.Label();
            this.speechRecognitionDeviceLabel = new System.Windows.Forms.Label();
            this.backgroundAudioDeviceLabel = new System.Windows.Forms.Label();
            this.spotterNameBox = new System.Windows.Forms.ComboBox();
            this.donateLink = new System.Windows.Forms.LinkLabel();
            this.smokeTestTextBox = new System.Windows.Forms.TextBox();
            this.buttonSmokeTest = new System.Windows.Forms.Button();
            this.chiefNameLabel = new System.Windows.Forms.Label();
            this.chiefNameBox = new System.Windows.Forms.ComboBox();
            this.mainWindowTooltip = new System.Windows.Forms.ToolTip(this.components);
            this.codriverNameLabel = new System.Windows.Forms.Label();
            this.codriverNameBox = new System.Windows.Forms.ComboBox();
            this.codriverStyleLabel = new System.Windows.Forms.Label();
            this.codriverStyleBox = new System.Windows.Forms.ComboBox();
            this.scanControllers = new System.Windows.Forms.Button();
            this.buttonEditCommandMacros = new System.Windows.Forms.Button();
            this.AddRemoveActions = new System.Windows.Forms.Button();
            this.buttonVRWindowSettings = new System.Windows.Forms.Button();
            this.buttonMyName = new System.Windows.Forms.Button();
            this.autoDetectTimer = new System.Windows.Forms.Timer(this.components);
            this.groupBoxGame = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelToDockGameDefinitionList = new System.Windows.Forms.TableLayoutPanel();
            this.gameDefinitionList = new System.Windows.Forms.ComboBox();
            this.tableLayoutPanelBottom = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxAssignedActions = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelActions = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxAvailableControllers = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelControllers = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelUpdatesAndDonate = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxUpdates = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelUpdates = new System.Windows.Forms.TableLayoutPanel();
            this.buttonSoundPackUpdate = new System.Windows.Forms.Button();
            this.groupBoxConsoleWindow = new System.Windows.Forms.GroupBox();
            this.consoleTextBox = new System.Windows.Forms.RichTextBox();
            this.tableLayoutPanelTop = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelSpeechrecognitionDevice = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelBackgroundVolume = new System.Windows.Forms.TableLayoutPanel();
            this.backgroundVolumeSliderLabel = new System.Windows.Forms.Label();
            this.backgroundVolumeSlider = new System.Windows.Forms.TrackBar();
            this.groupBoxTrace = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelTrace = new System.Windows.Forms.TableLayoutPanel();
            this.labelPlaybackSpeed = new System.Windows.Forms.Label();
            this.tableLayoutPanelMessagesVolume = new System.Windows.Forms.TableLayoutPanel();
            this.messagesVolumeSliderLabel = new System.Windows.Forms.Label();
            this.messagesVolumeSlider = new System.Windows.Forms.TrackBar();
            this.tableLayoutPanelActorsSRmode = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelBackgroundDevice = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelButtons = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelMessagesDevice = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelGameStart = new System.Windows.Forms.TableLayoutPanel();
            this.startApplicationButton = new System.Windows.Forms.Button();
            this.tableLayoutPanelFuelMultiplier = new System.Windows.Forms.TableLayoutPanel();
            this.numericUpDownfuelMultiplier = new System.Windows.Forms.NumericUpDown();
            this.fuelMultiplierLabel = new System.Windows.Forms.Label();
            this.comboBoxSpeechRecognitionModes = new System.Windows.Forms.ComboBox();
            this.labelSpeechRecognitionMode = new System.Windows.Forms.Label();
            this.tableLayoutPanelMain = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxSoundTest = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelTestSounds = new System.Windows.Forms.TableLayoutPanel();
            this.groupBoxSoundUpdateEtc = new System.Windows.Forms.GroupBox();
            this.tableLayoutPanelActors = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelSrModeAndBackgroundDevice = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanelVoiceRecognitionModes = new System.Windows.Forms.TableLayoutPanel();
            this.toggleButton = new System.Windows.Forms.RadioButton();
            this.alwaysOnButton = new System.Windows.Forms.RadioButton();
            this.holdButton = new System.Windows.Forms.RadioButton();
            this.voiceDisableButton = new System.Windows.Forms.RadioButton();
            this.triggerWordButton = new System.Windows.Forms.RadioButton();
            this.listenIfNotPressedButton = new System.Windows.Forms.RadioButton();
            this.groupBoxVoiceRecognitionMode = new System.Windows.Forms.GroupBox();
            this.groupBoxGame.SuspendLayout();
            this.tableLayoutPanelToDockGameDefinitionList.SuspendLayout();
            this.tableLayoutPanelBottom.SuspendLayout();
            this.groupBoxAssignedActions.SuspendLayout();
            this.tableLayoutPanelActions.SuspendLayout();
            this.groupBoxAvailableControllers.SuspendLayout();
            this.tableLayoutPanelControllers.SuspendLayout();
            this.tableLayoutPanelUpdatesAndDonate.SuspendLayout();
            this.groupBoxUpdates.SuspendLayout();
            this.tableLayoutPanelUpdates.SuspendLayout();
            this.groupBoxConsoleWindow.SuspendLayout();
            this.tableLayoutPanelTop.SuspendLayout();
            this.tableLayoutPanelSpeechrecognitionDevice.SuspendLayout();
            this.tableLayoutPanelBackgroundVolume.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.backgroundVolumeSlider)).BeginInit();
            this.groupBoxTrace.SuspendLayout();
            this.tableLayoutPanelTrace.SuspendLayout();
            this.tableLayoutPanelMessagesVolume.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.messagesVolumeSlider)).BeginInit();
            this.tableLayoutPanelActorsSRmode.SuspendLayout();
            this.tableLayoutPanelBackgroundDevice.SuspendLayout();
            this.tableLayoutPanelButtons.SuspendLayout();
            this.tableLayoutPanelMessagesDevice.SuspendLayout();
            this.tableLayoutPanelGameStart.SuspendLayout();
            this.tableLayoutPanelFuelMultiplier.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownfuelMultiplier)).BeginInit();
            this.tableLayoutPanelMain.SuspendLayout();
            this.groupBoxSoundTest.SuspendLayout();
            this.tableLayoutPanelTestSounds.SuspendLayout();
            this.groupBoxSoundUpdateEtc.SuspendLayout();
            this.tableLayoutPanelActors.SuspendLayout();
            this.tableLayoutPanelSrModeAndBackgroundDevice.SuspendLayout();
            this.tableLayoutPanelVoiceRecognitionModes.SuspendLayout();
            this.groupBoxVoiceRecognitionMode.SuspendLayout();
            this.SuspendLayout();
            // 
            // forceVersionCheckButton
            // 
            this.forceVersionCheckButton.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.forceVersionCheckButton.AutoSize = true;
            this.forceVersionCheckButton.Location = new System.Drawing.Point(22, 29);
            this.forceVersionCheckButton.Margin = new System.Windows.Forms.Padding(20, 3, 20, 3);
            this.forceVersionCheckButton.Name = "forceVersionCheckButton";
            this.forceVersionCheckButton.Size = new System.Drawing.Size(121, 30);
            this.forceVersionCheckButton.TabIndex = 290;
            this.forceVersionCheckButton.Text = "check_for_updates";
            this.forceVersionCheckButton.UseVisualStyleBackColor = true;
            this.forceVersionCheckButton.Click += new System.EventHandler(this.forceVersionCheckButtonClicked);
            // 
            // buttonActionSelect
            // 
            this.buttonActionSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelActions.SetColumnSpan(this.buttonActionSelect, 3);
            this.buttonActionSelect.FormattingEnabled = true;
            this.buttonActionSelect.HorizontalScrollbar = true;
            this.buttonActionSelect.Location = new System.Drawing.Point(3, 3);
            this.buttonActionSelect.Name = "buttonActionSelect";
            this.buttonActionSelect.ScrollAlwaysVisible = true;
            this.buttonActionSelect.Size = new System.Drawing.Size(536, 121);
            this.buttonActionSelect.TabIndex = 230;
            this.buttonActionSelect.SelectedIndexChanged += new System.EventHandler(this.buttonActionSelect_SelectedIndexChanged);
            // 
            // controllersList
            // 
            this.controllersList.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.controllersList.FormattingEnabled = true;
            this.controllersList.HorizontalScrollbar = true;
            this.controllersList.Location = new System.Drawing.Point(3, 3);
            this.controllersList.Name = "controllersList";
            this.controllersList.ScrollAlwaysVisible = true;
            this.controllersList.Size = new System.Drawing.Size(213, 121);
            this.controllersList.TabIndex = 210;
            this.controllersList.SelectedIndexChanged += new System.EventHandler(this.controllersList_SelectedIndexChanged);
            // 
            // assignButtonToAction
            // 
            this.assignButtonToAction.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.assignButtonToAction.Location = new System.Drawing.Point(3, 137);
            this.assignButtonToAction.Name = "assignButtonToAction";
            this.assignButtonToAction.Size = new System.Drawing.Size(174, 26);
            this.assignButtonToAction.TabIndex = 240;
            this.assignButtonToAction.Text = "assign_control";
            this.assignButtonToAction.UseVisualStyleBackColor = true;
            this.assignButtonToAction.Click += new System.EventHandler(this.assignButtonToActionClick);
            // 
            // deleteAssigmentButton
            // 
            this.deleteAssigmentButton.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.deleteAssigmentButton.Location = new System.Drawing.Point(183, 137);
            this.deleteAssigmentButton.Name = "deleteAssigmentButton";
            this.deleteAssigmentButton.Size = new System.Drawing.Size(174, 23);
            this.deleteAssigmentButton.TabIndex = 250;
            this.deleteAssigmentButton.Text = "delete_assignment";
            this.deleteAssigmentButton.UseVisualStyleBackColor = true;
            this.deleteAssigmentButton.Click += new System.EventHandler(this.deleteAssignmentButtonClicked);
            // 
            // propertiesButton
            // 
            this.propertiesButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertiesButton.Location = new System.Drawing.Point(3, 3);
            this.propertiesButton.Name = "propertiesButton";
            this.propertiesButton.Size = new System.Drawing.Size(110, 35);
            this.propertiesButton.TabIndex = 110;
            this.propertiesButton.Text = "properties";
            this.propertiesButton.UseVisualStyleBackColor = true;
            this.propertiesButton.Click += new System.EventHandler(this.editPropertiesButtonClicked);
            // 
            // messagesAudioDeviceBox
            // 
            this.messagesAudioDeviceBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.messagesAudioDeviceBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.messagesAudioDeviceBox.DropDownWidth = 300;
            this.messagesAudioDeviceBox.Enabled = false;
            this.messagesAudioDeviceBox.IntegralHeight = false;
            this.messagesAudioDeviceBox.Location = new System.Drawing.Point(26, 35);
            this.messagesAudioDeviceBox.MaxDropDownItems = 10;
            this.messagesAudioDeviceBox.Name = "messagesAudioDeviceBox";
            this.messagesAudioDeviceBox.Size = new System.Drawing.Size(140, 21);
            this.messagesAudioDeviceBox.TabIndex = 150;
            this.messagesAudioDeviceBox.Visible = false;
            // 
            // speechRecognitionDeviceBox
            // 
            this.speechRecognitionDeviceBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.speechRecognitionDeviceBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.speechRecognitionDeviceBox.DropDownWidth = 300;
            this.speechRecognitionDeviceBox.Enabled = false;
            this.speechRecognitionDeviceBox.IntegralHeight = false;
            this.speechRecognitionDeviceBox.Location = new System.Drawing.Point(26, 35);
            this.speechRecognitionDeviceBox.MaxDropDownItems = 10;
            this.speechRecognitionDeviceBox.Name = "speechRecognitionDeviceBox";
            this.speechRecognitionDeviceBox.Size = new System.Drawing.Size(140, 21);
            this.speechRecognitionDeviceBox.TabIndex = 140;
            this.speechRecognitionDeviceBox.Visible = false;
            // 
            // backgroundAudioDeviceBox
            // 
            this.backgroundAudioDeviceBox.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.backgroundAudioDeviceBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.backgroundAudioDeviceBox.DropDownWidth = 300;
            this.backgroundAudioDeviceBox.Enabled = false;
            this.backgroundAudioDeviceBox.IntegralHeight = false;
            this.backgroundAudioDeviceBox.Location = new System.Drawing.Point(24, 16);
            this.backgroundAudioDeviceBox.Margin = new System.Windows.Forms.Padding(10, 0, 10, 0);
            this.backgroundAudioDeviceBox.MaxDropDownItems = 10;
            this.backgroundAudioDeviceBox.Name = "backgroundAudioDeviceBox";
            this.backgroundAudioDeviceBox.Size = new System.Drawing.Size(178, 21);
            this.backgroundAudioDeviceBox.TabIndex = 160;
            this.backgroundAudioDeviceBox.Visible = false;
            // 
            // filenameTextbox
            // 
            this.filenameTextbox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.filenameTextbox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.filenameTextbox.Location = new System.Drawing.Point(3, 47);
            this.filenameTextbox.Name = "filenameTextbox";
            this.filenameTextbox.Size = new System.Drawing.Size(183, 13);
            this.filenameTextbox.TabIndex = 20;
            // 
            // filenameLabel
            // 
            this.filenameLabel.AutoSize = true;
            this.filenameLabel.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.filenameLabel.Location = new System.Drawing.Point(3, 31);
            this.filenameLabel.Name = "filenameLabel";
            this.filenameLabel.Size = new System.Drawing.Size(183, 13);
            this.filenameLabel.TabIndex = 19;
            this.filenameLabel.Text = "File &name to run";
            // 
            // recordSession
            // 
            this.recordSession.AutoSize = true;
            this.recordSession.Dock = System.Windows.Forms.DockStyle.Fill;
            this.recordSession.Location = new System.Drawing.Point(3, 3);
            this.recordSession.Name = "recordSession";
            this.recordSession.Size = new System.Drawing.Size(183, 18);
            this.recordSession.TabIndex = 10;
            this.recordSession.Text = "&Record";
            this.mainWindowTooltip.SetToolTip(this.recordSession, "Record all the data provided by the game");
            this.recordSession.UseVisualStyleBackColor = true;
            this.recordSession.CheckedChanged += new System.EventHandler(this.recordSession_CheckedChanged);
            // 
            // playbackInterval
            // 
            this.playbackInterval.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.playbackInterval.Dock = System.Windows.Forms.DockStyle.Left;
            this.playbackInterval.Location = new System.Drawing.Point(3, 87);
            this.playbackInterval.Name = "playbackInterval";
            this.playbackInterval.Size = new System.Drawing.Size(54, 13);
            this.playbackInterval.TabIndex = 30;
            this.playbackInterval.TextChanged += new System.EventHandler(this.playbackIntervalChanged);
            // 
            // app_version
            // 
            this.app_version.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.app_version.AutoSize = true;
            this.app_version.Location = new System.Drawing.Point(5, 2);
            this.app_version.Name = "app_version";
            this.app_version.Size = new System.Drawing.Size(155, 24);
            this.app_version.TabIndex = 193;
            this.app_version.Text = "app_version";
            this.app_version.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // soundPackProgressBar
            // 
            this.soundPackProgressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.soundPackProgressBar.Location = new System.Drawing.Point(126, 44);
            this.soundPackProgressBar.Name = "soundPackProgressBar";
            this.soundPackProgressBar.Size = new System.Drawing.Size(228, 35);
            this.soundPackProgressBar.TabIndex = 191;
            // 
            // downloadSoundPackButton
            // 
            this.downloadSoundPackButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.downloadSoundPackButton.Enabled = false;
            this.downloadSoundPackButton.Location = new System.Drawing.Point(126, 3);
            this.downloadSoundPackButton.Name = "downloadSoundPackButton";
            this.downloadSoundPackButton.Size = new System.Drawing.Size(228, 35);
            this.downloadSoundPackButton.TabIndex = 170;
            this.downloadSoundPackButton.Text = "sound_pack_is_up_to_date";
            this.downloadSoundPackButton.UseVisualStyleBackColor = true;
            this.downloadSoundPackButton.Click += new System.EventHandler(this.downloadSoundPackButtonPress);
            // 
            // downloadDriverNamesButton
            // 
            this.downloadDriverNamesButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.downloadDriverNamesButton.Enabled = false;
            this.downloadDriverNamesButton.Location = new System.Drawing.Point(360, 3);
            this.downloadDriverNamesButton.Name = "downloadDriverNamesButton";
            this.downloadDriverNamesButton.Size = new System.Drawing.Size(244, 35);
            this.downloadDriverNamesButton.TabIndex = 180;
            this.downloadDriverNamesButton.Text = "driver_names_are_up_to_date";
            this.downloadDriverNamesButton.UseVisualStyleBackColor = true;
            this.downloadDriverNamesButton.Click += new System.EventHandler(this.downloadDriverNamesButtonPress);
            // 
            // downloadPersonalisationsButton
            // 
            this.downloadPersonalisationsButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.downloadPersonalisationsButton.Enabled = false;
            this.downloadPersonalisationsButton.Location = new System.Drawing.Point(610, 3);
            this.downloadPersonalisationsButton.Name = "downloadPersonalisationsButton";
            this.downloadPersonalisationsButton.Size = new System.Drawing.Size(220, 35);
            this.downloadPersonalisationsButton.TabIndex = 190;
            this.downloadPersonalisationsButton.Text = "personalisations_are_up_to_date";
            this.downloadPersonalisationsButton.UseVisualStyleBackColor = true;
            this.downloadPersonalisationsButton.Click += new System.EventHandler(this.downloadPersonalisationsButtonPress);
            // 
            // driverNamesProgressBar
            // 
            this.driverNamesProgressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.driverNamesProgressBar.Location = new System.Drawing.Point(360, 44);
            this.driverNamesProgressBar.Name = "driverNamesProgressBar";
            this.driverNamesProgressBar.Size = new System.Drawing.Size(244, 35);
            this.driverNamesProgressBar.TabIndex = 0;
            // 
            // personalisationsProgressBar
            // 
            this.personalisationsProgressBar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.personalisationsProgressBar.Location = new System.Drawing.Point(610, 44);
            this.personalisationsProgressBar.Name = "personalisationsProgressBar";
            this.personalisationsProgressBar.Size = new System.Drawing.Size(220, 35);
            this.personalisationsProgressBar.TabIndex = 192;
            // 
            // spotterNameLabel
            // 
            this.spotterNameLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.spotterNameLabel.AutoSize = true;
            this.spotterNameLabel.Location = new System.Drawing.Point(14, 60);
            this.spotterNameLabel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.spotterNameLabel.Name = "spotterNameLabel";
            this.spotterNameLabel.Size = new System.Drawing.Size(99, 21);
            this.spotterNameLabel.TabIndex = 99;
            this.spotterNameLabel.Text = "spotter_name_label";
            this.spotterNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.mainWindowTooltip.SetToolTip(this.spotterNameLabel, "spotter_name_tooltip");
            // 
            // messagesAudioDeviceLabel
            // 
            this.messagesAudioDeviceLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.messagesAudioDeviceLabel.AutoSize = true;
            this.messagesAudioDeviceLabel.Location = new System.Drawing.Point(20, 19);
            this.messagesAudioDeviceLabel.Name = "messagesAudioDeviceLabel";
            this.messagesAudioDeviceLabel.Size = new System.Drawing.Size(152, 13);
            this.messagesAudioDeviceLabel.TabIndex = 149;
            this.messagesAudioDeviceLabel.Text = "messages_audio_device_label";
            this.messagesAudioDeviceLabel.Visible = false;
            // 
            // speechRecognitionDeviceLabel
            // 
            this.speechRecognitionDeviceLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.speechRecognitionDeviceLabel.AutoSize = true;
            this.speechRecognitionDeviceLabel.Location = new System.Drawing.Point(13, 19);
            this.speechRecognitionDeviceLabel.Name = "speechRecognitionDeviceLabel";
            this.speechRecognitionDeviceLabel.Size = new System.Drawing.Size(166, 13);
            this.speechRecognitionDeviceLabel.TabIndex = 139;
            this.speechRecognitionDeviceLabel.Text = "speech_recognition_device_label";
            this.speechRecognitionDeviceLabel.Visible = false;
            // 
            // backgroundAudioDeviceLabel
            // 
            this.backgroundAudioDeviceLabel.AutoSize = true;
            this.backgroundAudioDeviceLabel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.backgroundAudioDeviceLabel.Location = new System.Drawing.Point(3, 0);
            this.backgroundAudioDeviceLabel.Name = "backgroundAudioDeviceLabel";
            this.backgroundAudioDeviceLabel.Size = new System.Drawing.Size(220, 16);
            this.backgroundAudioDeviceLabel.TabIndex = 159;
            this.backgroundAudioDeviceLabel.Text = "background_audio_device_label";
            this.backgroundAudioDeviceLabel.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.backgroundAudioDeviceLabel.Visible = false;
            // 
            // spotterNameBox
            // 
            this.spotterNameBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.spotterNameBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.spotterNameBox.IntegralHeight = false;
            this.spotterNameBox.Location = new System.Drawing.Point(119, 60);
            this.spotterNameBox.MaxDropDownItems = 5;
            this.spotterNameBox.Name = "spotterNameBox";
            this.spotterNameBox.Size = new System.Drawing.Size(121, 21);
            this.spotterNameBox.TabIndex = 100;
            this.mainWindowTooltip.SetToolTip(this.spotterNameBox, "spotter_name_tooltip");
            // 
            // donateLink
            // 
            this.donateLink.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.donateLink.Location = new System.Drawing.Point(3, 138);
            this.donateLink.Name = "donateLink";
            this.donateLink.Size = new System.Drawing.Size(167, 37);
            this.donateLink.TabIndex = 270;
            this.donateLink.TabStop = true;
            this.donateLink.Text = "donate_link_text";
            this.donateLink.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.donateLink.Click += new System.EventHandler(this.internetPanHandler);
            // 
            // smokeTestTextBox
            // 
            this.smokeTestTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.smokeTestTextBox.Location = new System.Drawing.Point(3, 3);
            this.smokeTestTextBox.MaxLength = 99999999;
            this.smokeTestTextBox.Multiline = true;
            this.smokeTestTextBox.Name = "smokeTestTextBox";
            this.smokeTestTextBox.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.smokeTestTextBox.Size = new System.Drawing.Size(289, 162);
            this.smokeTestTextBox.TabIndex = 502;
            this.smokeTestTextBox.Visible = false;
            // 
            // buttonSmokeTest
            // 
            this.buttonSmokeTest.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonSmokeTest.Location = new System.Drawing.Point(153, 171);
            this.buttonSmokeTest.Name = "buttonSmokeTest";
            this.buttonSmokeTest.Size = new System.Drawing.Size(139, 23);
            this.buttonSmokeTest.TabIndex = 501;
            this.buttonSmokeTest.Text = "Test Sounds";
            this.buttonSmokeTest.UseVisualStyleBackColor = true;
            this.buttonSmokeTest.Visible = false;
            this.buttonSmokeTest.Click += new System.EventHandler(this.playSmokeTestSounds);
            // 
            // chiefNameLabel
            // 
            this.chiefNameLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.chiefNameLabel.AutoSize = true;
            this.chiefNameLabel.Location = new System.Drawing.Point(23, 36);
            this.chiefNameLabel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 0);
            this.chiefNameLabel.Name = "chiefNameLabel";
            this.chiefNameLabel.Size = new System.Drawing.Size(90, 21);
            this.chiefNameLabel.TabIndex = 94;
            this.chiefNameLabel.Text = "chief_name_label";
            this.chiefNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.mainWindowTooltip.SetToolTip(this.chiefNameLabel, "chief_name_tooltip");
            // 
            // chiefNameBox
            // 
            this.chiefNameBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.chiefNameBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.chiefNameBox.IntegralHeight = false;
            this.chiefNameBox.Location = new System.Drawing.Point(119, 36);
            this.chiefNameBox.MaxDropDownItems = 5;
            this.chiefNameBox.Name = "chiefNameBox";
            this.chiefNameBox.Size = new System.Drawing.Size(121, 21);
            this.chiefNameBox.TabIndex = 95;
            this.mainWindowTooltip.SetToolTip(this.chiefNameBox, "chief_name_tooltip");
            // 
            // codriverNameLabel
            // 
            this.codriverNameLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.codriverNameLabel.AutoSize = true;
            this.codriverNameLabel.Location = new System.Drawing.Point(1030, 0);
            this.codriverNameLabel.Name = "codriverNameLabel";
            this.codriverNameLabel.Size = new System.Drawing.Size(105, 41);
            this.codriverNameLabel.TabIndex = 94;
            this.codriverNameLabel.Text = "codriver_name_label";
            this.codriverNameLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.mainWindowTooltip.SetToolTip(this.codriverNameLabel, "codriver_name_tooltip");
            this.codriverNameLabel.Visible = false;
            // 
            // codriverNameBox
            // 
            this.codriverNameBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.codriverNameBox.IntegralHeight = false;
            this.codriverNameBox.Location = new System.Drawing.Point(0, 0);
            this.codriverNameBox.MaxDropDownItems = 5;
            this.codriverNameBox.Name = "codriverNameBox";
            this.codriverNameBox.Size = new System.Drawing.Size(121, 21);
            this.codriverNameBox.TabIndex = 95;
            this.mainWindowTooltip.SetToolTip(this.codriverNameBox, "codriver_name_tooltip");
            this.codriverNameBox.Visible = false;
            // 
            // codriverStyleLabel
            // 
            this.codriverStyleLabel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.codriverStyleLabel.AutoSize = true;
            this.codriverStyleLabel.Location = new System.Drawing.Point(1035, 41);
            this.codriverStyleLabel.Name = "codriverStyleLabel";
            this.codriverStyleLabel.Size = new System.Drawing.Size(100, 41);
            this.codriverStyleLabel.TabIndex = 94;
            this.codriverStyleLabel.Text = "codriver_style_label";
            this.codriverStyleLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.codriverStyleLabel.Visible = false;
            // 
            // codriverStyleBox
            // 
            this.codriverStyleBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.codriverStyleBox.IntegralHeight = false;
            this.codriverStyleBox.Location = new System.Drawing.Point(0, 0);
            this.codriverStyleBox.MaxDropDownItems = 6;
            this.codriverStyleBox.Name = "codriverStyleBox";
            this.codriverStyleBox.Size = new System.Drawing.Size(106, 21);
            this.codriverStyleBox.TabIndex = 95;
            this.codriverStyleBox.Visible = false;
            // 
            // scanControllers
            // 
            this.scanControllers.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.scanControllers.Location = new System.Drawing.Point(3, 134);
            this.scanControllers.Name = "scanControllers";
            this.scanControllers.Size = new System.Drawing.Size(213, 26);
            this.scanControllers.TabIndex = 215;
            this.scanControllers.Text = "scan_for_controllers";
            this.scanControllers.Click += new System.EventHandler(this.ScanControllers_Click);
            // 
            // buttonEditCommandMacros
            // 
            this.buttonEditCommandMacros.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonEditCommandMacros.Location = new System.Drawing.Point(3, 85);
            this.buttonEditCommandMacros.Name = "buttonEditCommandMacros";
            this.buttonEditCommandMacros.Size = new System.Drawing.Size(110, 35);
            this.buttonEditCommandMacros.TabIndex = 114;
            this.buttonEditCommandMacros.Text = "edit_macro_commands";
            this.buttonEditCommandMacros.UseVisualStyleBackColor = true;
            this.buttonEditCommandMacros.Click += new System.EventHandler(this.editCommandMacroButtonClicked);
            // 
            // AddRemoveActions
            // 
            this.AddRemoveActions.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.AddRemoveActions.Location = new System.Drawing.Point(363, 137);
            this.AddRemoveActions.Name = "AddRemoveActions";
            this.AddRemoveActions.Size = new System.Drawing.Size(176, 22);
            this.AddRemoveActions.TabIndex = 233;
            this.AddRemoveActions.Text = "add_remove_actions";
            this.AddRemoveActions.UseVisualStyleBackColor = true;
            this.AddRemoveActions.Click += new System.EventHandler(this.AddRemoveActions_Click);
            // 
            // buttonVRWindowSettings
            // 
            this.buttonVRWindowSettings.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonVRWindowSettings.Enabled = false;
            this.buttonVRWindowSettings.Location = new System.Drawing.Point(3, 44);
            this.buttonVRWindowSettings.Name = "buttonVRWindowSettings";
            this.buttonVRWindowSettings.Size = new System.Drawing.Size(110, 35);
            this.buttonVRWindowSettings.TabIndex = 112;
            this.buttonVRWindowSettings.Text = "vr_window_settings";
            this.buttonVRWindowSettings.UseVisualStyleBackColor = true;
            this.buttonVRWindowSettings.Click += new System.EventHandler(this.buttonVRWindowSettings_Click);
            // 
            // buttonMyName
            // 
            this.buttonMyName.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)));
            this.buttonMyName.AutoSize = true;
            this.buttonMyName.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanelActorsSRmode.SetColumnSpan(this.buttonMyName, 2);
            this.buttonMyName.Location = new System.Drawing.Point(86, 2);
            this.buttonMyName.Margin = new System.Windows.Forms.Padding(2);
            this.buttonMyName.Name = "buttonMyName";
            this.buttonMyName.Size = new System.Drawing.Size(60, 29);
            this.buttonMyName.TabIndex = 503;
            this.buttonMyName.Text = "My name";
            this.buttonMyName.UseVisualStyleBackColor = true;
            this.buttonMyName.Click += new System.EventHandler(this.buttonMyName_Click);
            // 
            // autoDetectTimer
            // 
            this.autoDetectTimer.Interval = 3000;
            this.autoDetectTimer.Tick += new System.EventHandler(this.autoDetectTimer_Tick);
            // 
            // groupBoxGame
            // 
            this.groupBoxGame.AutoSize = true;
            this.groupBoxGame.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.groupBoxGame.CausesValidation = false;
            this.tableLayoutPanelGameStart.SetColumnSpan(this.groupBoxGame, 2);
            this.groupBoxGame.Controls.Add(this.tableLayoutPanelToDockGameDefinitionList);
            this.groupBoxGame.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxGame.Location = new System.Drawing.Point(1, 1);
            this.groupBoxGame.Margin = new System.Windows.Forms.Padding(1);
            this.groupBoxGame.Name = "groupBoxGame";
            this.groupBoxGame.Padding = new System.Windows.Forms.Padding(1);
            this.groupBoxGame.Size = new System.Drawing.Size(189, 42);
            this.groupBoxGame.TabIndex = 203;
            this.groupBoxGame.TabStop = false;
            this.groupBoxGame.Text = "Game";
            // 
            // tableLayoutPanelToDockGameDefinitionList
            // 
            this.tableLayoutPanelToDockGameDefinitionList.ColumnCount = 1;
            this.tableLayoutPanelToDockGameDefinitionList.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelToDockGameDefinitionList.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelToDockGameDefinitionList.Controls.Add(this.gameDefinitionList, 0, 0);
            this.tableLayoutPanelToDockGameDefinitionList.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelToDockGameDefinitionList.Location = new System.Drawing.Point(1, 14);
            this.tableLayoutPanelToDockGameDefinitionList.Name = "tableLayoutPanelToDockGameDefinitionList";
            this.tableLayoutPanelToDockGameDefinitionList.RowCount = 1;
            this.tableLayoutPanelToDockGameDefinitionList.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelToDockGameDefinitionList.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 27F));
            this.tableLayoutPanelToDockGameDefinitionList.Size = new System.Drawing.Size(187, 27);
            this.tableLayoutPanelToDockGameDefinitionList.TabIndex = 0;
            // 
            // gameDefinitionList
            // 
            this.gameDefinitionList.DropDownWidth = 180;
            this.gameDefinitionList.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.gameDefinitionList.FormattingEnabled = true;
            this.gameDefinitionList.Items.AddRange(new object[] {
            "Assetto Corsa (32 bit)",
            "Assetto Corsa (64 bit)",
            "Automobilista",
            "etc."});
            this.gameDefinitionList.Location = new System.Drawing.Point(3, 3);
            this.gameDefinitionList.Name = "gameDefinitionList";
            this.gameDefinitionList.Size = new System.Drawing.Size(181, 24);
            this.gameDefinitionList.TabIndex = 0;
            this.gameDefinitionList.SelectedValueChanged += new System.EventHandler(this.updateSelectedGameDefinition);
            // 
            // tableLayoutPanelBottom
            // 
            this.tableLayoutPanelBottom.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelBottom.ColumnCount = 4;
            this.tableLayoutPanelMain.SetColumnSpan(this.tableLayoutPanelBottom, 2);
            this.tableLayoutPanelBottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 19.83348F));
            this.tableLayoutPanelBottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 48.03855F));
            this.tableLayoutPanelBottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 16.6454F));
            this.tableLayoutPanelBottom.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15.48257F));
            this.tableLayoutPanelBottom.Controls.Add(this.groupBoxAssignedActions, 1, 0);
            this.tableLayoutPanelBottom.Controls.Add(this.groupBoxAvailableControllers, 0, 0);
            this.tableLayoutPanelBottom.Controls.Add(this.tableLayoutPanelUpdatesAndDonate, 3, 0);
            this.tableLayoutPanelBottom.Controls.Add(this.tableLayoutPanelSrModeAndBackgroundDevice, 2, 0);
            this.tableLayoutPanelBottom.Location = new System.Drawing.Point(4, 495);
            this.tableLayoutPanelBottom.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelBottom.Name = "tableLayoutPanelBottom";
            this.tableLayoutPanelBottom.RowCount = 1;
            this.tableLayoutPanelBottom.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelBottom.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 188F));
            this.tableLayoutPanelBottom.Size = new System.Drawing.Size(1146, 188);
            this.tableLayoutPanelBottom.TabIndex = 2;
            // 
            // groupBoxAssignedActions
            // 
            this.groupBoxAssignedActions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxAssignedActions.Controls.Add(this.tableLayoutPanelActions);
            this.groupBoxAssignedActions.Location = new System.Drawing.Point(229, 2);
            this.groupBoxAssignedActions.Margin = new System.Windows.Forms.Padding(2);
            this.groupBoxAssignedActions.Name = "groupBoxAssignedActions";
            this.groupBoxAssignedActions.Padding = new System.Windows.Forms.Padding(2);
            this.groupBoxAssignedActions.Size = new System.Drawing.Size(546, 184);
            this.groupBoxAssignedActions.TabIndex = 202;
            this.groupBoxAssignedActions.TabStop = false;
            this.groupBoxAssignedActions.Text = "Assigned Actions";
            // 
            // tableLayoutPanelActions
            // 
            this.tableLayoutPanelActions.ColumnCount = 3;
            this.tableLayoutPanelActions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanelActions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanelActions.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanelActions.Controls.Add(this.buttonActionSelect, 0, 0);
            this.tableLayoutPanelActions.Controls.Add(this.AddRemoveActions, 2, 1);
            this.tableLayoutPanelActions.Controls.Add(this.assignButtonToAction, 0, 1);
            this.tableLayoutPanelActions.Controls.Add(this.deleteAssigmentButton, 1, 1);
            this.tableLayoutPanelActions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelActions.Location = new System.Drawing.Point(2, 15);
            this.tableLayoutPanelActions.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelActions.Name = "tableLayoutPanelActions";
            this.tableLayoutPanelActions.RowCount = 2;
            this.tableLayoutPanelActions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 80.45977F));
            this.tableLayoutPanelActions.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 19.54023F));
            this.tableLayoutPanelActions.Size = new System.Drawing.Size(542, 167);
            this.tableLayoutPanelActions.TabIndex = 206;
            // 
            // groupBoxAvailableControllers
            // 
            this.groupBoxAvailableControllers.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBoxAvailableControllers.Controls.Add(this.tableLayoutPanelControllers);
            this.groupBoxAvailableControllers.Location = new System.Drawing.Point(2, 2);
            this.groupBoxAvailableControllers.Margin = new System.Windows.Forms.Padding(2);
            this.groupBoxAvailableControllers.Name = "groupBoxAvailableControllers";
            this.groupBoxAvailableControllers.Padding = new System.Windows.Forms.Padding(2);
            this.groupBoxAvailableControllers.Size = new System.Drawing.Size(223, 184);
            this.groupBoxAvailableControllers.TabIndex = 201;
            this.groupBoxAvailableControllers.TabStop = false;
            this.groupBoxAvailableControllers.Text = "Available Controllers";
            // 
            // tableLayoutPanelControllers
            // 
            this.tableLayoutPanelControllers.ColumnCount = 1;
            this.tableLayoutPanelControllers.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelControllers.Controls.Add(this.controllersList, 0, 0);
            this.tableLayoutPanelControllers.Controls.Add(this.scanControllers, 0, 1);
            this.tableLayoutPanelControllers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelControllers.Location = new System.Drawing.Point(2, 15);
            this.tableLayoutPanelControllers.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelControllers.Name = "tableLayoutPanelControllers";
            this.tableLayoutPanelControllers.RowCount = 2;
            this.tableLayoutPanelControllers.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 78.91566F));
            this.tableLayoutPanelControllers.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 21.08434F));
            this.tableLayoutPanelControllers.Size = new System.Drawing.Size(219, 167);
            this.tableLayoutPanelControllers.TabIndex = 205;
            // 
            // tableLayoutPanelUpdatesAndDonate
            // 
            this.tableLayoutPanelUpdatesAndDonate.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelUpdatesAndDonate.ColumnCount = 1;
            this.tableLayoutPanelUpdatesAndDonate.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelUpdatesAndDonate.Controls.Add(this.groupBoxUpdates, 0, 0);
            this.tableLayoutPanelUpdatesAndDonate.Controls.Add(this.donateLink, 0, 1);
            this.tableLayoutPanelUpdatesAndDonate.Location = new System.Drawing.Point(970, 3);
            this.tableLayoutPanelUpdatesAndDonate.Margin = new System.Windows.Forms.Padding(3, 3, 3, 10);
            this.tableLayoutPanelUpdatesAndDonate.Name = "tableLayoutPanelUpdatesAndDonate";
            this.tableLayoutPanelUpdatesAndDonate.RowCount = 2;
            this.tableLayoutPanelUpdatesAndDonate.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 79.37063F));
            this.tableLayoutPanelUpdatesAndDonate.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20.62937F));
            this.tableLayoutPanelUpdatesAndDonate.Size = new System.Drawing.Size(173, 175);
            this.tableLayoutPanelUpdatesAndDonate.TabIndex = 261;
            // 
            // groupBoxUpdates
            // 
            this.groupBoxUpdates.AutoSize = true;
            this.groupBoxUpdates.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.groupBoxUpdates.CausesValidation = false;
            this.groupBoxUpdates.Controls.Add(this.tableLayoutPanelUpdates);
            this.groupBoxUpdates.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxUpdates.Location = new System.Drawing.Point(2, 2);
            this.groupBoxUpdates.Margin = new System.Windows.Forms.Padding(2);
            this.groupBoxUpdates.Name = "groupBoxUpdates";
            this.groupBoxUpdates.Padding = new System.Windows.Forms.Padding(2);
            this.groupBoxUpdates.Size = new System.Drawing.Size(169, 134);
            this.groupBoxUpdates.TabIndex = 202;
            this.groupBoxUpdates.TabStop = false;
            this.groupBoxUpdates.Text = "Updates";
            // 
            // tableLayoutPanelUpdates
            // 
            this.tableLayoutPanelUpdates.AutoSize = true;
            this.tableLayoutPanelUpdates.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanelUpdates.ColumnCount = 1;
            this.tableLayoutPanelUpdates.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelUpdates.Controls.Add(this.buttonSoundPackUpdate, 0, 2);
            this.tableLayoutPanelUpdates.Controls.Add(this.app_version, 0, 0);
            this.tableLayoutPanelUpdates.Controls.Add(this.forceVersionCheckButton, 0, 1);
            this.tableLayoutPanelUpdates.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelUpdates.Location = new System.Drawing.Point(2, 15);
            this.tableLayoutPanelUpdates.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelUpdates.Name = "tableLayoutPanelUpdates";
            this.tableLayoutPanelUpdates.Padding = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelUpdates.RowCount = 1;
            this.tableLayoutPanelUpdates.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 21.73907F));
            this.tableLayoutPanelUpdates.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 32.49275F));
            this.tableLayoutPanelUpdates.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 45.76818F));
            this.tableLayoutPanelUpdates.Size = new System.Drawing.Size(165, 117);
            this.tableLayoutPanelUpdates.TabIndex = 0;
            // 
            // buttonSoundPackUpdate
            // 
            this.buttonSoundPackUpdate.AutoSize = true;
            this.buttonSoundPackUpdate.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.buttonSoundPackUpdate.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonSoundPackUpdate.Location = new System.Drawing.Point(5, 65);
            this.buttonSoundPackUpdate.Name = "buttonSoundPackUpdate";
            this.buttonSoundPackUpdate.Size = new System.Drawing.Size(155, 47);
            this.buttonSoundPackUpdate.TabIndex = 291;
            this.buttonSoundPackUpdate.Text = "Sound pack update";
            this.buttonSoundPackUpdate.UseVisualStyleBackColor = true;
            this.buttonSoundPackUpdate.Click += new System.EventHandler(this.buttonSoundPackUpdate_Click);
            // 
            // groupBoxConsoleWindow
            // 
            this.groupBoxConsoleWindow.Controls.Add(this.consoleTextBox);
            this.groupBoxConsoleWindow.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxConsoleWindow.Location = new System.Drawing.Point(4, 161);
            this.groupBoxConsoleWindow.Margin = new System.Windows.Forms.Padding(2);
            this.groupBoxConsoleWindow.Name = "groupBoxConsoleWindow";
            this.groupBoxConsoleWindow.Padding = new System.Windows.Forms.Padding(2);
            this.groupBoxConsoleWindow.Size = new System.Drawing.Size(837, 219);
            this.groupBoxConsoleWindow.TabIndex = 1;
            this.groupBoxConsoleWindow.TabStop = false;
            this.groupBoxConsoleWindow.Text = "Console Window";
            // 
            // consoleTextBox
            // 
            this.consoleTextBox.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.consoleTextBox.Dock = System.Windows.Forms.DockStyle.Fill;
            this.consoleTextBox.Location = new System.Drawing.Point(2, 15);
            this.consoleTextBox.MaxLength = 99999999;
            this.consoleTextBox.Name = "consoleTextBox";
            this.consoleTextBox.ReadOnly = true;
            this.consoleTextBox.Size = new System.Drawing.Size(833, 202);
            this.consoleTextBox.TabIndex = 200;
            this.consoleTextBox.Text = "";
            // 
            // tableLayoutPanelTop
            // 
            this.tableLayoutPanelTop.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanelTop.ColumnCount = 6;
            this.tableLayoutPanelMain.SetColumnSpan(this.tableLayoutPanelTop, 2);
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17.24138F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17.24138F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17.24138F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17.24138F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20.68966F));
            this.tableLayoutPanelTop.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 10.34483F));
            this.tableLayoutPanelTop.Controls.Add(this.tableLayoutPanelSpeechrecognitionDevice, 3, 1);
            this.tableLayoutPanelTop.Controls.Add(this.tableLayoutPanelBackgroundVolume, 3, 0);
            this.tableLayoutPanelTop.Controls.Add(this.groupBoxTrace, 0, 0);
            this.tableLayoutPanelTop.Controls.Add(this.tableLayoutPanelMessagesVolume, 2, 0);
            this.tableLayoutPanelTop.Controls.Add(this.tableLayoutPanelActorsSRmode, 4, 0);
            this.tableLayoutPanelTop.Controls.Add(this.tableLayoutPanelButtons, 5, 0);
            this.tableLayoutPanelTop.Controls.Add(this.tableLayoutPanelMessagesDevice, 2, 1);
            this.tableLayoutPanelTop.Controls.Add(this.tableLayoutPanelGameStart, 1, 0);
            this.tableLayoutPanelTop.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelTop.Location = new System.Drawing.Point(4, 26);
            this.tableLayoutPanelTop.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelTop.MinimumSize = new System.Drawing.Size(800, 80);
            this.tableLayoutPanelTop.Name = "tableLayoutPanelTop";
            this.tableLayoutPanelTop.Padding = new System.Windows.Forms.Padding(1);
            this.tableLayoutPanelTop.RowCount = 2;
            this.tableLayoutPanelTop.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 45.85635F));
            this.tableLayoutPanelTop.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 54.14365F));
            this.tableLayoutPanelTop.Size = new System.Drawing.Size(1146, 129);
            this.tableLayoutPanelTop.TabIndex = 0;
            // 
            // tableLayoutPanelSpeechrecognitionDevice
            // 
            this.tableLayoutPanelSpeechrecognitionDevice.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelSpeechrecognitionDevice.ColumnCount = 1;
            this.tableLayoutPanelSpeechrecognitionDevice.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelSpeechrecognitionDevice.Controls.Add(this.speechRecognitionDeviceLabel, 0, 0);
            this.tableLayoutPanelSpeechrecognitionDevice.Controls.Add(this.speechRecognitionDeviceBox, 0, 1);
            this.tableLayoutPanelSpeechrecognitionDevice.Location = new System.Drawing.Point(594, 61);
            this.tableLayoutPanelSpeechrecognitionDevice.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelSpeechrecognitionDevice.Name = "tableLayoutPanelSpeechrecognitionDevice";
            this.tableLayoutPanelSpeechrecognitionDevice.RowCount = 2;
            this.tableLayoutPanelSpeechrecognitionDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelSpeechrecognitionDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelSpeechrecognitionDevice.Size = new System.Drawing.Size(193, 65);
            this.tableLayoutPanelSpeechrecognitionDevice.TabIndex = 207;
            // 
            // tableLayoutPanelBackgroundVolume
            // 
            this.tableLayoutPanelBackgroundVolume.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelBackgroundVolume.ColumnCount = 1;
            this.tableLayoutPanelBackgroundVolume.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelBackgroundVolume.Controls.Add(this.backgroundVolumeSliderLabel, 0, 0);
            this.tableLayoutPanelBackgroundVolume.Controls.Add(this.backgroundVolumeSlider, 0, 1);
            this.tableLayoutPanelBackgroundVolume.Location = new System.Drawing.Point(594, 3);
            this.tableLayoutPanelBackgroundVolume.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelBackgroundVolume.Name = "tableLayoutPanelBackgroundVolume";
            this.tableLayoutPanelBackgroundVolume.RowCount = 2;
            this.tableLayoutPanelBackgroundVolume.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelBackgroundVolume.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tableLayoutPanelBackgroundVolume.Size = new System.Drawing.Size(193, 54);
            this.tableLayoutPanelBackgroundVolume.TabIndex = 42;
            // 
            // backgroundVolumeSliderLabel
            // 
            this.backgroundVolumeSliderLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.backgroundVolumeSliderLabel.AutoSize = true;
            this.backgroundVolumeSliderLabel.Location = new System.Drawing.Point(44, 7);
            this.backgroundVolumeSliderLabel.Name = "backgroundVolumeSliderLabel";
            this.backgroundVolumeSliderLabel.Size = new System.Drawing.Size(104, 13);
            this.backgroundVolumeSliderLabel.TabIndex = 69;
            this.backgroundVolumeSliderLabel.Text = "background_volume";
            // 
            // backgroundVolumeSlider
            // 
            this.backgroundVolumeSlider.Dock = System.Windows.Forms.DockStyle.Fill;
            this.backgroundVolumeSlider.Location = new System.Drawing.Point(13, 23);
            this.backgroundVolumeSlider.Margin = new System.Windows.Forms.Padding(13, 3, 13, 3);
            this.backgroundVolumeSlider.Maximum = 100;
            this.backgroundVolumeSlider.Name = "backgroundVolumeSlider";
            this.backgroundVolumeSlider.Size = new System.Drawing.Size(167, 28);
            this.backgroundVolumeSlider.TabIndex = 70;
            this.backgroundVolumeSlider.TickFrequency = 10;
            this.backgroundVolumeSlider.Scroll += new System.EventHandler(this.backgroundVolumeSlider_Scroll);
            // 
            // groupBoxTrace
            // 
            this.groupBoxTrace.Controls.Add(this.tableLayoutPanelTrace);
            this.groupBoxTrace.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxTrace.Location = new System.Drawing.Point(3, 3);
            this.groupBoxTrace.Margin = new System.Windows.Forms.Padding(2);
            this.groupBoxTrace.Name = "groupBoxTrace";
            this.groupBoxTrace.Padding = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelTop.SetRowSpan(this.groupBoxTrace, 2);
            this.groupBoxTrace.Size = new System.Drawing.Size(193, 123);
            this.groupBoxTrace.TabIndex = 208;
            this.groupBoxTrace.TabStop = false;
            this.groupBoxTrace.Text = "Trace";
            // 
            // tableLayoutPanelTrace
            // 
            this.tableLayoutPanelTrace.ColumnCount = 1;
            this.tableLayoutPanelTrace.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTrace.Controls.Add(this.recordSession, 0, 0);
            this.tableLayoutPanelTrace.Controls.Add(this.filenameLabel, 0, 1);
            this.tableLayoutPanelTrace.Controls.Add(this.filenameTextbox, 0, 2);
            this.tableLayoutPanelTrace.Controls.Add(this.labelPlaybackSpeed, 0, 3);
            this.tableLayoutPanelTrace.Controls.Add(this.playbackInterval, 0, 4);
            this.tableLayoutPanelTrace.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelTrace.Location = new System.Drawing.Point(2, 15);
            this.tableLayoutPanelTrace.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelTrace.Name = "tableLayoutPanelTrace";
            this.tableLayoutPanelTrace.RowCount = 5;
            this.tableLayoutPanelTrace.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 23.07692F));
            this.tableLayoutPanelTrace.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 19.23077F));
            this.tableLayoutPanelTrace.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 19.23077F));
            this.tableLayoutPanelTrace.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 19.23077F));
            this.tableLayoutPanelTrace.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 19.23077F));
            this.tableLayoutPanelTrace.Size = new System.Drawing.Size(189, 106);
            this.tableLayoutPanelTrace.TabIndex = 0;
            // 
            // labelPlaybackSpeed
            // 
            this.labelPlaybackSpeed.AutoSize = true;
            this.labelPlaybackSpeed.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.labelPlaybackSpeed.Location = new System.Drawing.Point(2, 71);
            this.labelPlaybackSpeed.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelPlaybackSpeed.Name = "labelPlaybackSpeed";
            this.labelPlaybackSpeed.Size = new System.Drawing.Size(185, 13);
            this.labelPlaybackSpeed.TabIndex = 21;
            this.labelPlaybackSpeed.Text = "Playback speed";
            // 
            // tableLayoutPanelMessagesVolume
            // 
            this.tableLayoutPanelMessagesVolume.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelMessagesVolume.ColumnCount = 1;
            this.tableLayoutPanelMessagesVolume.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelMessagesVolume.Controls.Add(this.messagesVolumeSliderLabel, 0, 0);
            this.tableLayoutPanelMessagesVolume.Controls.Add(this.messagesVolumeSlider, 0, 1);
            this.tableLayoutPanelMessagesVolume.Location = new System.Drawing.Point(397, 3);
            this.tableLayoutPanelMessagesVolume.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelMessagesVolume.Name = "tableLayoutPanelMessagesVolume";
            this.tableLayoutPanelMessagesVolume.RowCount = 2;
            this.tableLayoutPanelMessagesVolume.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelMessagesVolume.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            this.tableLayoutPanelMessagesVolume.Size = new System.Drawing.Size(193, 54);
            this.tableLayoutPanelMessagesVolume.TabIndex = 41;
            // 
            // messagesVolumeSliderLabel
            // 
            this.messagesVolumeSliderLabel.Anchor = System.Windows.Forms.AnchorStyles.Bottom;
            this.messagesVolumeSliderLabel.AutoSize = true;
            this.messagesVolumeSliderLabel.Location = new System.Drawing.Point(49, 7);
            this.messagesVolumeSliderLabel.Name = "messagesVolumeSliderLabel";
            this.messagesVolumeSliderLabel.Size = new System.Drawing.Size(94, 13);
            this.messagesVolumeSliderLabel.TabIndex = 59;
            this.messagesVolumeSliderLabel.Text = "messages_volume";
            // 
            // messagesVolumeSlider
            // 
            this.messagesVolumeSlider.Dock = System.Windows.Forms.DockStyle.Fill;
            this.messagesVolumeSlider.Location = new System.Drawing.Point(13, 23);
            this.messagesVolumeSlider.Margin = new System.Windows.Forms.Padding(13, 3, 13, 3);
            this.messagesVolumeSlider.Maximum = 100;
            this.messagesVolumeSlider.Name = "messagesVolumeSlider";
            this.messagesVolumeSlider.Size = new System.Drawing.Size(167, 28);
            this.messagesVolumeSlider.TabIndex = 60;
            this.messagesVolumeSlider.TickFrequency = 10;
            this.messagesVolumeSlider.Scroll += new System.EventHandler(this.messagesVolumeSlider_Scroll);
            // 
            // tableLayoutPanelActorsSRmode
            // 
            this.tableLayoutPanelActorsSRmode.ColumnCount = 2;
            this.tableLayoutPanelActorsSRmode.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelActorsSRmode.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelActorsSRmode.Controls.Add(this.chiefNameLabel, 0, 1);
            this.tableLayoutPanelActorsSRmode.Controls.Add(this.spotterNameLabel, 0, 2);
            this.tableLayoutPanelActorsSRmode.Controls.Add(this.buttonMyName, 0, 0);
            this.tableLayoutPanelActorsSRmode.Controls.Add(this.chiefNameBox, 1, 1);
            this.tableLayoutPanelActorsSRmode.Controls.Add(this.spotterNameBox, 1, 2);
            this.tableLayoutPanelActorsSRmode.Controls.Add(this.tableLayoutPanelBackgroundDevice, 0, 3);
            this.tableLayoutPanelActorsSRmode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelActorsSRmode.Location = new System.Drawing.Point(791, 3);
            this.tableLayoutPanelActorsSRmode.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelActorsSRmode.Name = "tableLayoutPanelActorsSRmode";
            this.tableLayoutPanelActorsSRmode.RowCount = 4;
            this.tableLayoutPanelTop.SetRowSpan(this.tableLayoutPanelActorsSRmode, 2);
            this.tableLayoutPanelActorsSRmode.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40.7582F));
            this.tableLayoutPanelActorsSRmode.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 29.6209F));
            this.tableLayoutPanelActorsSRmode.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 29.6209F));
            this.tableLayoutPanelActorsSRmode.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            this.tableLayoutPanelActorsSRmode.Size = new System.Drawing.Size(232, 123);
            this.tableLayoutPanelActorsSRmode.TabIndex = 204;
            // 
            // tableLayoutPanelBackgroundDevice
            // 
            this.tableLayoutPanelBackgroundDevice.ColumnCount = 1;
            this.tableLayoutPanelActorsSRmode.SetColumnSpan(this.tableLayoutPanelBackgroundDevice, 2);
            this.tableLayoutPanelBackgroundDevice.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelBackgroundDevice.Controls.Add(this.backgroundAudioDeviceLabel, 0, 0);
            this.tableLayoutPanelBackgroundDevice.Controls.Add(this.backgroundAudioDeviceBox, 0, 1);
            this.tableLayoutPanelBackgroundDevice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelBackgroundDevice.Location = new System.Drawing.Point(3, 84);
            this.tableLayoutPanelBackgroundDevice.MinimumSize = new System.Drawing.Size(0, 40);
            this.tableLayoutPanelBackgroundDevice.Name = "tableLayoutPanelBackgroundDevice";
            this.tableLayoutPanelBackgroundDevice.RowCount = 2;
            this.tableLayoutPanelBackgroundDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanelBackgroundDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanelBackgroundDevice.Size = new System.Drawing.Size(226, 40);
            this.tableLayoutPanelBackgroundDevice.TabIndex = 504;
            // 
            // tableLayoutPanelButtons
            // 
            this.tableLayoutPanelButtons.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelButtons.ColumnCount = 1;
            this.tableLayoutPanelButtons.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelButtons.Controls.Add(this.propertiesButton, 0, 0);
            this.tableLayoutPanelButtons.Controls.Add(this.buttonVRWindowSettings, 0, 1);
            this.tableLayoutPanelButtons.Controls.Add(this.buttonEditCommandMacros, 0, 2);
            this.tableLayoutPanelButtons.Location = new System.Drawing.Point(1027, 3);
            this.tableLayoutPanelButtons.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelButtons.Name = "tableLayoutPanelButtons";
            this.tableLayoutPanelButtons.RowCount = 3;
            this.tableLayoutPanelTop.SetRowSpan(this.tableLayoutPanelButtons, 2);
            this.tableLayoutPanelButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanelButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanelButtons.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanelButtons.Size = new System.Drawing.Size(116, 123);
            this.tableLayoutPanelButtons.TabIndex = 205;
            // 
            // tableLayoutPanelMessagesDevice
            // 
            this.tableLayoutPanelMessagesDevice.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanelMessagesDevice.ColumnCount = 1;
            this.tableLayoutPanelMessagesDevice.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelMessagesDevice.Controls.Add(this.messagesAudioDeviceLabel, 0, 0);
            this.tableLayoutPanelMessagesDevice.Controls.Add(this.messagesAudioDeviceBox, 0, 1);
            this.tableLayoutPanelMessagesDevice.Location = new System.Drawing.Point(397, 61);
            this.tableLayoutPanelMessagesDevice.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelMessagesDevice.Name = "tableLayoutPanelMessagesDevice";
            this.tableLayoutPanelMessagesDevice.RowCount = 2;
            this.tableLayoutPanelMessagesDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelMessagesDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelMessagesDevice.Size = new System.Drawing.Size(193, 65);
            this.tableLayoutPanelMessagesDevice.TabIndex = 206;
            // 
            // tableLayoutPanelGameStart
            // 
            this.tableLayoutPanelGameStart.ColumnCount = 2;
            this.tableLayoutPanelGameStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            this.tableLayoutPanelGameStart.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            this.tableLayoutPanelGameStart.Controls.Add(this.startApplicationButton, 0, 1);
            this.tableLayoutPanelGameStart.Controls.Add(this.groupBoxGame, 0, 0);
            this.tableLayoutPanelGameStart.Controls.Add(this.tableLayoutPanelFuelMultiplier, 0, 2);
            this.tableLayoutPanelGameStart.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelGameStart.Location = new System.Drawing.Point(201, 4);
            this.tableLayoutPanelGameStart.Name = "tableLayoutPanelGameStart";
            this.tableLayoutPanelGameStart.RowCount = 3;
            this.tableLayoutPanelTop.SetRowSpan(this.tableLayoutPanelGameStart, 2);
            this.tableLayoutPanelGameStart.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 37F));
            this.tableLayoutPanelGameStart.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 48F));
            this.tableLayoutPanelGameStart.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanelGameStart.Size = new System.Drawing.Size(191, 121);
            this.tableLayoutPanelGameStart.TabIndex = 209;
            // 
            // startApplicationButton
            // 
            this.startApplicationButton.AutoSize = true;
            this.startApplicationButton.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanelGameStart.SetColumnSpan(this.startApplicationButton, 2);
            this.startApplicationButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.startApplicationButton.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.startApplicationButton.Location = new System.Drawing.Point(13, 49);
            this.startApplicationButton.Margin = new System.Windows.Forms.Padding(13, 5, 13, 5);
            this.startApplicationButton.MaximumSize = new System.Drawing.Size(180, 60);
            this.startApplicationButton.MinimumSize = new System.Drawing.Size(90, 20);
            this.startApplicationButton.Name = "startApplicationButton";
            this.startApplicationButton.Size = new System.Drawing.Size(165, 48);
            this.startApplicationButton.TabIndex = 40;
            this.startApplicationButton.Text = "start_application";
            this.startApplicationButton.UseVisualStyleBackColor = true;
            this.startApplicationButton.Click += new System.EventHandler(this.startApplicationButton_Click);
            // 
            // tableLayoutPanelFuelMultiplier
            // 
            this.tableLayoutPanelFuelMultiplier.ColumnCount = 2;
            this.tableLayoutPanelGameStart.SetColumnSpan(this.tableLayoutPanelFuelMultiplier, 2);
            this.tableLayoutPanelFuelMultiplier.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelFuelMultiplier.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelFuelMultiplier.Controls.Add(this.numericUpDownfuelMultiplier, 1, 0);
            this.tableLayoutPanelFuelMultiplier.Controls.Add(this.fuelMultiplierLabel, 0, 0);
            this.tableLayoutPanelFuelMultiplier.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelFuelMultiplier.Location = new System.Drawing.Point(3, 105);
            this.tableLayoutPanelFuelMultiplier.Name = "tableLayoutPanelFuelMultiplier";
            this.tableLayoutPanelFuelMultiplier.RowCount = 1;
            this.tableLayoutPanelFuelMultiplier.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelFuelMultiplier.Size = new System.Drawing.Size(185, 13);
            this.tableLayoutPanelFuelMultiplier.TabIndex = 204;
            // 
            // numericUpDownfuelMultiplier
            // 
            this.numericUpDownfuelMultiplier.AutoSize = true;
            this.numericUpDownfuelMultiplier.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.numericUpDownfuelMultiplier.Font = new System.Drawing.Font("Microsoft Sans Serif", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.numericUpDownfuelMultiplier.Location = new System.Drawing.Point(92, 0);
            this.numericUpDownfuelMultiplier.Margin = new System.Windows.Forms.Padding(0);
            this.numericUpDownfuelMultiplier.Maximum = new decimal(new int[] {
            7,
            0,
            0,
            0});
            this.numericUpDownfuelMultiplier.Name = "numericUpDownfuelMultiplier";
            this.numericUpDownfuelMultiplier.Size = new System.Drawing.Size(29, 16);
            this.numericUpDownfuelMultiplier.TabIndex = 201;
            this.numericUpDownfuelMultiplier.Value = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownfuelMultiplier.ValueChanged += new System.EventHandler(this.numericUpDownfuelMultiplier_ValueChanged);
            // 
            // fuelMultiplierLabel
            // 
            this.fuelMultiplierLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.fuelMultiplierLabel.AutoSize = true;
            this.fuelMultiplierLabel.Location = new System.Drawing.Point(9, 0);
            this.fuelMultiplierLabel.Margin = new System.Windows.Forms.Padding(4, 0, 0, 1);
            this.fuelMultiplierLabel.Name = "fuelMultiplierLabel";
            this.fuelMultiplierLabel.Size = new System.Drawing.Size(83, 12);
            this.fuelMultiplierLabel.TabIndex = 505;
            this.fuelMultiplierLabel.Text = "fuel_multiplier  X";
            this.fuelMultiplierLabel.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            // 
            // comboBoxSpeechRecognitionModes
            // 
            this.comboBoxSpeechRecognitionModes.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxSpeechRecognitionModes.FormattingEnabled = true;
            this.comboBoxSpeechRecognitionModes.Items.AddRange(new object[] {
            "Disabled",
            "Hold button",
            "Press and release button",
            "Always on",
            "etc."});
            this.comboBoxSpeechRecognitionModes.Location = new System.Drawing.Point(835, 43);
            this.comboBoxSpeechRecognitionModes.Margin = new System.Windows.Forms.Padding(2);
            this.comboBoxSpeechRecognitionModes.Name = "comboBoxSpeechRecognitionModes";
            this.comboBoxSpeechRecognitionModes.Size = new System.Drawing.Size(166, 21);
            this.comboBoxSpeechRecognitionModes.TabIndex = 1;
            this.comboBoxSpeechRecognitionModes.Visible = false;
            // 
            // labelSpeechRecognitionMode
            // 
            this.labelSpeechRecognitionMode.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelSpeechRecognitionMode.AutoSize = true;
            this.labelSpeechRecognitionMode.Location = new System.Drawing.Point(873, 0);
            this.labelSpeechRecognitionMode.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelSpeechRecognitionMode.Name = "labelSpeechRecognitionMode";
            this.labelSpeechRecognitionMode.Size = new System.Drawing.Size(128, 41);
            this.labelSpeechRecognitionMode.TabIndex = 504;
            this.labelSpeechRecognitionMode.Text = "Speech recognition mode";
            this.labelSpeechRecognitionMode.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.labelSpeechRecognitionMode.Visible = false;
            // 
            // tableLayoutPanelMain
            // 
            this.tableLayoutPanelMain.AutoSize = true;
            this.tableLayoutPanelMain.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanelMain.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Outset;
            this.tableLayoutPanelMain.ColumnCount = 2;
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelMain.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 307F));
            this.tableLayoutPanelMain.Controls.Add(this.groupBoxSoundTest, 1, 2);
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanelBottom, 0, 4);
            this.tableLayoutPanelMain.Controls.Add(this.tableLayoutPanelTop, 0, 1);
            this.tableLayoutPanelMain.Controls.Add(this.groupBoxConsoleWindow, 0, 2);
            this.tableLayoutPanelMain.Controls.Add(this.groupBoxSoundUpdateEtc, 0, 3);
            this.tableLayoutPanelMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelMain.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanelMain.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanelMain.MinimumSize = new System.Drawing.Size(800, 450);
            this.tableLayoutPanelMain.Name = "tableLayoutPanelMain";
            this.tableLayoutPanelMain.RowCount = 5;
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20.40265F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 34.1063F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.4509F));
            this.tableLayoutPanelMain.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 29.04015F));
            this.tableLayoutPanelMain.Size = new System.Drawing.Size(1154, 687);
            this.tableLayoutPanelMain.TabIndex = 505;
            // 
            // groupBoxSoundTest
            // 
            this.groupBoxSoundTest.Controls.Add(this.tableLayoutPanelTestSounds);
            this.groupBoxSoundTest.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSoundTest.Location = new System.Drawing.Point(848, 162);
            this.groupBoxSoundTest.Name = "groupBoxSoundTest";
            this.groupBoxSoundTest.Size = new System.Drawing.Size(301, 217);
            this.groupBoxSoundTest.TabIndex = 4;
            this.groupBoxSoundTest.TabStop = false;
            this.groupBoxSoundTest.Text = "Sound test";
            // 
            // tableLayoutPanelTestSounds
            // 
            this.tableLayoutPanelTestSounds.ColumnCount = 1;
            this.tableLayoutPanelTestSounds.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTestSounds.Controls.Add(this.buttonSmokeTest, 0, 1);
            this.tableLayoutPanelTestSounds.Controls.Add(this.smokeTestTextBox, 0, 0);
            this.tableLayoutPanelTestSounds.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelTestSounds.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanelTestSounds.Name = "tableLayoutPanelTestSounds";
            this.tableLayoutPanelTestSounds.RowCount = 2;
            this.tableLayoutPanelTestSounds.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelTestSounds.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 30F));
            this.tableLayoutPanelTestSounds.Size = new System.Drawing.Size(295, 198);
            this.tableLayoutPanelTestSounds.TabIndex = 4;
            // 
            // groupBoxSoundUpdateEtc
            // 
            this.tableLayoutPanelMain.SetColumnSpan(this.groupBoxSoundUpdateEtc, 2);
            this.groupBoxSoundUpdateEtc.Controls.Add(this.tableLayoutPanelActors);
            this.groupBoxSoundUpdateEtc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxSoundUpdateEtc.Location = new System.Drawing.Point(5, 387);
            this.groupBoxSoundUpdateEtc.Name = "groupBoxSoundUpdateEtc";
            this.groupBoxSoundUpdateEtc.Size = new System.Drawing.Size(1144, 101);
            this.groupBoxSoundUpdateEtc.TabIndex = 3;
            this.groupBoxSoundUpdateEtc.TabStop = false;
            this.groupBoxSoundUpdateEtc.Text = "Sound pack status";
            // 
            // tableLayoutPanelActors
            // 
            this.tableLayoutPanelActors.ColumnCount = 6;
            this.tableLayoutPanelActors.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 123F));
            this.tableLayoutPanelActors.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 48.3945F));
            this.tableLayoutPanelActors.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 51.6055F));
            this.tableLayoutPanelActors.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 226F));
            this.tableLayoutPanelActors.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 170F));
            this.tableLayoutPanelActors.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 134F));
            this.tableLayoutPanelActors.Controls.Add(this.comboBoxSpeechRecognitionModes, 4, 1);
            this.tableLayoutPanelActors.Controls.Add(this.labelSpeechRecognitionMode, 4, 0);
            this.tableLayoutPanelActors.Controls.Add(this.downloadSoundPackButton, 1, 0);
            this.tableLayoutPanelActors.Controls.Add(this.downloadDriverNamesButton, 2, 0);
            this.tableLayoutPanelActors.Controls.Add(this.downloadPersonalisationsButton, 3, 0);
            this.tableLayoutPanelActors.Controls.Add(this.driverNamesProgressBar, 2, 1);
            this.tableLayoutPanelActors.Controls.Add(this.personalisationsProgressBar, 3, 1);
            this.tableLayoutPanelActors.Controls.Add(this.soundPackProgressBar, 1, 1);
            this.tableLayoutPanelActors.Controls.Add(this.codriverStyleLabel, 5, 1);
            this.tableLayoutPanelActors.Controls.Add(this.codriverNameLabel, 5, 0);
            this.tableLayoutPanelActors.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelActors.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanelActors.Name = "tableLayoutPanelActors";
            this.tableLayoutPanelActors.RowCount = 2;
            this.tableLayoutPanelActors.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelActors.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanelActors.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 13F));
            this.tableLayoutPanelActors.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 13F));
            this.tableLayoutPanelActors.Size = new System.Drawing.Size(1138, 82);
            this.tableLayoutPanelActors.TabIndex = 0;
            // 
            // tableLayoutPanelSrModeAndBackgroundDevice
            // 
            this.tableLayoutPanelSrModeAndBackgroundDevice.ColumnCount = 1;
            this.tableLayoutPanelSrModeAndBackgroundDevice.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelSrModeAndBackgroundDevice.Controls.Add(this.groupBoxVoiceRecognitionMode, 0, 0);
            this.tableLayoutPanelSrModeAndBackgroundDevice.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelSrModeAndBackgroundDevice.Location = new System.Drawing.Point(780, 3);
            this.tableLayoutPanelSrModeAndBackgroundDevice.Margin = new System.Windows.Forms.Padding(3, 3, 3, 10);
            this.tableLayoutPanelSrModeAndBackgroundDevice.Name = "tableLayoutPanelSrModeAndBackgroundDevice";
            this.tableLayoutPanelSrModeAndBackgroundDevice.RowCount = 3;
            this.tableLayoutPanelSrModeAndBackgroundDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 81.63265F));
            this.tableLayoutPanelSrModeAndBackgroundDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 8.163265F));
            this.tableLayoutPanelSrModeAndBackgroundDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10.20408F));
            this.tableLayoutPanelSrModeAndBackgroundDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelSrModeAndBackgroundDevice.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanelSrModeAndBackgroundDevice.Size = new System.Drawing.Size(184, 175);
            this.tableLayoutPanelSrModeAndBackgroundDevice.TabIndex = 262;
            // 
            // tableLayoutPanelVoiceRecognitionModes
            // 
            this.tableLayoutPanelVoiceRecognitionModes.ColumnCount = 1;
            this.tableLayoutPanelVoiceRecognitionModes.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanelVoiceRecognitionModes.Controls.Add(this.listenIfNotPressedButton, 0, 5);
            this.tableLayoutPanelVoiceRecognitionModes.Controls.Add(this.triggerWordButton, 0, 4);
            this.tableLayoutPanelVoiceRecognitionModes.Controls.Add(this.voiceDisableButton, 0, 0);
            this.tableLayoutPanelVoiceRecognitionModes.Controls.Add(this.holdButton, 0, 1);
            this.tableLayoutPanelVoiceRecognitionModes.Controls.Add(this.alwaysOnButton, 0, 3);
            this.tableLayoutPanelVoiceRecognitionModes.Controls.Add(this.toggleButton, 0, 2);
            this.tableLayoutPanelVoiceRecognitionModes.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanelVoiceRecognitionModes.Location = new System.Drawing.Point(3, 16);
            this.tableLayoutPanelVoiceRecognitionModes.Name = "tableLayoutPanelVoiceRecognitionModes";
            this.tableLayoutPanelVoiceRecognitionModes.RowCount = 6;
            this.tableLayoutPanelVoiceRecognitionModes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelVoiceRecognitionModes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelVoiceRecognitionModes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelVoiceRecognitionModes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelVoiceRecognitionModes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelVoiceRecognitionModes.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.66667F));
            this.tableLayoutPanelVoiceRecognitionModes.Size = new System.Drawing.Size(172, 117);
            this.tableLayoutPanelVoiceRecognitionModes.TabIndex = 0;
            // 
            // toggleButton
            // 
            this.toggleButton.AutoSize = true;
            this.toggleButton.Location = new System.Drawing.Point(0, 38);
            this.toggleButton.Margin = new System.Windows.Forms.Padding(0);
            this.toggleButton.Name = "toggleButton";
            this.toggleButton.Size = new System.Drawing.Size(90, 17);
            this.toggleButton.TabIndex = 2;
            this.toggleButton.TabStop = true;
            this.toggleButton.Text = "toggle_button";
            this.mainWindowTooltip.SetToolTip(this.toggleButton, "voice_recognition_toggle_button_help");
            this.toggleButton.UseVisualStyleBackColor = true;
            this.toggleButton.CheckedChanged += new System.EventHandler(this.toggleButton_CheckedChanged);
            // 
            // alwaysOnButton
            // 
            this.alwaysOnButton.AutoSize = true;
            this.alwaysOnButton.Location = new System.Drawing.Point(0, 57);
            this.alwaysOnButton.Margin = new System.Windows.Forms.Padding(0);
            this.alwaysOnButton.Name = "alwaysOnButton";
            this.alwaysOnButton.Size = new System.Drawing.Size(75, 17);
            this.alwaysOnButton.TabIndex = 3;
            this.alwaysOnButton.TabStop = true;
            this.alwaysOnButton.Text = "always_on";
            this.mainWindowTooltip.SetToolTip(this.alwaysOnButton, "voice_recognition_always_on_help");
            this.alwaysOnButton.UseVisualStyleBackColor = true;
            this.alwaysOnButton.CheckedChanged += new System.EventHandler(this.alwaysOnButton_CheckedChanged);
            // 
            // holdButton
            // 
            this.holdButton.AutoSize = true;
            this.holdButton.Location = new System.Drawing.Point(0, 19);
            this.holdButton.Margin = new System.Windows.Forms.Padding(0);
            this.holdButton.Name = "holdButton";
            this.holdButton.Size = new System.Drawing.Size(81, 17);
            this.holdButton.TabIndex = 1;
            this.holdButton.TabStop = true;
            this.holdButton.Text = "hold_button";
            this.mainWindowTooltip.SetToolTip(this.holdButton, "voice_recognition_hold_button_help");
            this.holdButton.UseVisualStyleBackColor = true;
            this.holdButton.CheckedChanged += new System.EventHandler(this.holdButton_CheckedChanged);
            // 
            // voiceDisableButton
            // 
            this.voiceDisableButton.AutoSize = true;
            this.voiceDisableButton.Location = new System.Drawing.Point(0, 0);
            this.voiceDisableButton.Margin = new System.Windows.Forms.Padding(0);
            this.voiceDisableButton.Name = "voiceDisableButton";
            this.voiceDisableButton.Size = new System.Drawing.Size(64, 17);
            this.voiceDisableButton.TabIndex = 0;
            this.voiceDisableButton.TabStop = true;
            this.voiceDisableButton.Text = "disabled";
            this.mainWindowTooltip.SetToolTip(this.voiceDisableButton, "voice_recognition_disabled_help");
            this.voiceDisableButton.UseVisualStyleBackColor = true;
            this.voiceDisableButton.CheckedChanged += new System.EventHandler(this.voiceDisableButton_CheckedChanged);
            // 
            // triggerWordButton
            // 
            this.triggerWordButton.AutoSize = true;
            this.triggerWordButton.Location = new System.Drawing.Point(0, 76);
            this.triggerWordButton.Margin = new System.Windows.Forms.Padding(0);
            this.triggerWordButton.Name = "triggerWordButton";
            this.triggerWordButton.Size = new System.Drawing.Size(172, 17);
            this.triggerWordButton.TabIndex = 4;
            this.triggerWordButton.TabStop = true;
            this.triggerWordButton.Text = "trigger_word (\"trigger_word_for_always_on_sre\")";
            this.mainWindowTooltip.SetToolTip(this.triggerWordButton, "voice_recognition_trigger_word_help");
            this.triggerWordButton.UseVisualStyleBackColor = true;
            this.triggerWordButton.CheckedChanged += new System.EventHandler(this.triggerWordButton_CheckedChanged);
            // 
            // listenIfNotPressedButton
            // 
            this.listenIfNotPressedButton.AutoSize = true;
            this.listenIfNotPressedButton.Location = new System.Drawing.Point(0, 95);
            this.listenIfNotPressedButton.Margin = new System.Windows.Forms.Padding(0);
            this.listenIfNotPressedButton.Name = "listenIfNotPressedButton";
            this.listenIfNotPressedButton.Size = new System.Drawing.Size(172, 17);
            this.listenIfNotPressedButton.TabIndex = 5;
            this.listenIfNotPressedButton.TabStop = true;
            this.listenIfNotPressedButton.Text = "voice_recognition_listen_if_not_pressed";
            this.listenIfNotPressedButton.UseVisualStyleBackColor = true;
            this.listenIfNotPressedButton.CheckedChanged += new System.EventHandler(this.listenIfNotPressed_CheckedChanged);
            // 
            // groupBoxVoiceRecognitionMode
            // 
            this.groupBoxVoiceRecognitionMode.AutoSize = true;
            this.groupBoxVoiceRecognitionMode.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.groupBoxVoiceRecognitionMode.Controls.Add(this.tableLayoutPanelVoiceRecognitionModes);
            this.groupBoxVoiceRecognitionMode.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxVoiceRecognitionMode.Location = new System.Drawing.Point(3, 3);
            this.groupBoxVoiceRecognitionMode.Name = "groupBoxVoiceRecognitionMode";
            this.groupBoxVoiceRecognitionMode.Size = new System.Drawing.Size(178, 136);
            this.groupBoxVoiceRecognitionMode.TabIndex = 260;
            this.groupBoxVoiceRecognitionMode.TabStop = false;
            this.groupBoxVoiceRecognitionMode.Text = "voice_recognition_mode";
            this.mainWindowTooltip.SetToolTip(this.groupBoxVoiceRecognitionMode, "voice_recognition_mode_help");
            // 
            // MainWindow
            // 
            this.mainWindowTooltip.ShowAlways = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1154, 687);
            this.Controls.Add(this.tableLayoutPanelMain);
            this.Controls.Add(this.codriverNameBox);
            this.Controls.Add(this.codriverStyleBox);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "MainWindow";
            this.Text = "Crew Chief V4";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.stopApp);
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.groupBoxGame.ResumeLayout(false);
            this.tableLayoutPanelToDockGameDefinitionList.ResumeLayout(false);
            this.tableLayoutPanelBottom.ResumeLayout(false);
            this.groupBoxAssignedActions.ResumeLayout(false);
            this.tableLayoutPanelActions.ResumeLayout(false);
            this.groupBoxAvailableControllers.ResumeLayout(false);
            this.tableLayoutPanelControllers.ResumeLayout(false);
            this.tableLayoutPanelUpdatesAndDonate.ResumeLayout(false);
            this.tableLayoutPanelUpdatesAndDonate.PerformLayout();
            this.groupBoxUpdates.ResumeLayout(false);
            this.groupBoxUpdates.PerformLayout();
            this.tableLayoutPanelUpdates.ResumeLayout(false);
            this.tableLayoutPanelUpdates.PerformLayout();
            this.groupBoxConsoleWindow.ResumeLayout(false);
            this.tableLayoutPanelTop.ResumeLayout(false);
            this.tableLayoutPanelSpeechrecognitionDevice.ResumeLayout(false);
            this.tableLayoutPanelSpeechrecognitionDevice.PerformLayout();
            this.tableLayoutPanelBackgroundVolume.ResumeLayout(false);
            this.tableLayoutPanelBackgroundVolume.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.backgroundVolumeSlider)).EndInit();
            this.groupBoxTrace.ResumeLayout(false);
            this.tableLayoutPanelTrace.ResumeLayout(false);
            this.tableLayoutPanelTrace.PerformLayout();
            this.tableLayoutPanelMessagesVolume.ResumeLayout(false);
            this.tableLayoutPanelMessagesVolume.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.messagesVolumeSlider)).EndInit();
            this.tableLayoutPanelActorsSRmode.ResumeLayout(false);
            this.tableLayoutPanelActorsSRmode.PerformLayout();
            this.tableLayoutPanelBackgroundDevice.ResumeLayout(false);
            this.tableLayoutPanelBackgroundDevice.PerformLayout();
            this.tableLayoutPanelButtons.ResumeLayout(false);
            this.tableLayoutPanelMessagesDevice.ResumeLayout(false);
            this.tableLayoutPanelMessagesDevice.PerformLayout();
            this.tableLayoutPanelGameStart.ResumeLayout(false);
            this.tableLayoutPanelGameStart.PerformLayout();
            this.tableLayoutPanelFuelMultiplier.ResumeLayout(false);
            this.tableLayoutPanelFuelMultiplier.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownfuelMultiplier)).EndInit();
            this.tableLayoutPanelMain.ResumeLayout(false);
            this.groupBoxSoundTest.ResumeLayout(false);
            this.tableLayoutPanelTestSounds.ResumeLayout(false);
            this.tableLayoutPanelTestSounds.PerformLayout();
            this.groupBoxSoundUpdateEtc.ResumeLayout(false);
            this.tableLayoutPanelActors.ResumeLayout(false);
            this.tableLayoutPanelActors.PerformLayout();
            this.tableLayoutPanelSrModeAndBackgroundDevice.ResumeLayout(false);
            this.tableLayoutPanelSrModeAndBackgroundDevice.PerformLayout();
            this.tableLayoutPanelVoiceRecognitionModes.ResumeLayout(false);
            this.tableLayoutPanelVoiceRecognitionModes.PerformLayout();
            this.groupBoxVoiceRecognitionMode.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        public System.Windows.Forms.CheckBox recordSession;
        private System.Windows.Forms.Button forceVersionCheckButton;
        private System.Windows.Forms.ListBox buttonActionSelect;
        private System.Windows.Forms.ListBox controllersList;
        private System.Windows.Forms.Button assignButtonToAction;
        private System.Windows.Forms.Button deleteAssigmentButton;
        private System.Windows.Forms.Button propertiesButton;
        internal System.Windows.Forms.ComboBox speechRecognitionDeviceBox;
        private System.Windows.Forms.ComboBox messagesAudioDeviceBox;
        internal System.Windows.Forms.ComboBox backgroundAudioDeviceBox;
        public System.Windows.Forms.TextBox filenameTextbox;
        private System.Windows.Forms.Label filenameLabel;
        private System.Windows.Forms.TextBox playbackInterval;
        private System.Windows.Forms.Label app_version;
        private System.Windows.Forms.ProgressBar soundPackProgressBar;
        private System.Windows.Forms.Button downloadSoundPackButton;
        private System.Windows.Forms.Button downloadDriverNamesButton;
        private System.Windows.Forms.Button downloadPersonalisationsButton;
        private System.Windows.Forms.ProgressBar driverNamesProgressBar;
        private System.Windows.Forms.ProgressBar personalisationsProgressBar;
        internal System.Windows.Forms.Label spotterNameLabel;
        private System.Windows.Forms.Label messagesAudioDeviceLabel;
        internal System.Windows.Forms.Label speechRecognitionDeviceLabel;
        internal System.Windows.Forms.Label backgroundAudioDeviceLabel;
        internal System.Windows.Forms.ComboBox spotterNameBox;
        private System.Windows.Forms.LinkLabel donateLink;
        private System.Windows.Forms.TextBox smokeTestTextBox;
        private System.Windows.Forms.Button buttonSmokeTest;
        internal System.Windows.Forms.Label chiefNameLabel;
        internal System.Windows.Forms.ComboBox chiefNameBox;
        internal System.Windows.Forms.Label codriverNameLabel;
        internal System.Windows.Forms.ComboBox codriverNameBox;
        internal System.Windows.Forms.Label codriverStyleLabel;
        internal System.Windows.Forms.ComboBox codriverStyleBox;
        internal System.Windows.Forms.ToolTip mainWindowTooltip;
        private System.Windows.Forms.Button scanControllers;
        private System.Windows.Forms.Button buttonEditCommandMacros;
        private Button AddRemoveActions;
        public Button buttonVRWindowSettings;
        public Button buttonMyName;
        private Timer autoDetectTimer;
        private GroupBox groupBoxGame;
        public ComboBox gameDefinitionList;
        private TableLayoutPanel tableLayoutPanelBottom;
        private GroupBox groupBoxUpdates;
        private TableLayoutPanel tableLayoutPanelUpdates;
        private GroupBox groupBoxAssignedActions;
        private GroupBox groupBoxAvailableControllers;
        internal GroupBox groupBoxConsoleWindow;
        public RichTextBox consoleTextBox;
        internal TableLayoutPanel tableLayoutPanelTop;
        private TableLayoutPanel tableLayoutPanelBackgroundVolume;
        private Label backgroundVolumeSliderLabel;
        public Button startApplicationButton;
        private TableLayoutPanel tableLayoutPanelMessagesVolume;
        private Label messagesVolumeSliderLabel;
        private TrackBar messagesVolumeSlider;
        private TrackBar backgroundVolumeSlider;
        internal TableLayoutPanel tableLayoutPanelMain;
        private TableLayoutPanel tableLayoutPanelActions;
        private TableLayoutPanel tableLayoutPanelControllers;
        private TableLayoutPanel tableLayoutPanelButtons;
        internal TableLayoutPanel tableLayoutPanelActorsSRmode;
        private TableLayoutPanel tableLayoutPanelSpeechrecognitionDevice;
        private TableLayoutPanel tableLayoutPanelMessagesDevice;
        private ComboBox comboBoxSpeechRecognitionModes;
        private Label labelSpeechRecognitionMode;
        private TableLayoutPanel tableLayoutPanelActors;
        internal GroupBox groupBoxSoundUpdateEtc;
        internal TableLayoutPanel tableLayoutPanelTestSounds;
        internal Button buttonSoundPackUpdate;
        internal GroupBox groupBoxTrace;
        private TableLayoutPanel tableLayoutPanelTrace;
        private Label labelPlaybackSpeed;
        private TableLayoutPanel tableLayoutPanelUpdatesAndDonate;
        private TableLayoutPanel tableLayoutPanelGameStart;
        private Label fuelMultiplierLabel;
        internal GroupBox groupBoxSoundTest;
        private TableLayoutPanel tableLayoutPanelBackgroundDevice;
        public NumericUpDown numericUpDownfuelMultiplier;
        internal TableLayoutPanel tableLayoutPanelFuelMultiplier;
        public MainMenuStrip menuStrip;
        private TableLayoutPanel tableLayoutPanelToDockGameDefinitionList;
        internal TableLayoutPanel tableLayoutPanelSrModeAndBackgroundDevice;
        internal GroupBox groupBoxVoiceRecognitionMode;
        private TableLayoutPanel tableLayoutPanelVoiceRecognitionModes;
        private RadioButton listenIfNotPressedButton;
        private RadioButton triggerWordButton;
        private RadioButton voiceDisableButton;
        private RadioButton holdButton;
        private RadioButton alwaysOnButton;
        private RadioButton toggleButton;
    }
}
