using System;
using System.Windows.Forms;

namespace CrewChiefV4
{
    partial class PropertiesForm
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
            this.saveButton = new System.Windows.Forms.Button();
            this.propertiesFlowLayoutPanel = new System.Windows.Forms.FlowLayoutPanel();
            this.searchTextBox = new System.Windows.Forms.TextBox();
            this.mainTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.headerTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.gameFilterLabel = new System.Windows.Forms.Label();
            this.filterBox = new System.Windows.Forms.ComboBox();
            this.showCommonCheckbox = new System.Windows.Forms.CheckBox();
            this.showChangedCheckbox = new System.Windows.Forms.CheckBox();
            this.showNotDefaultCheckbox = new System.Windows.Forms.CheckBox();
            this.categoriesLabel = new System.Windows.Forms.Label();
            this.categoriesBox = new System.Windows.Forms.ComboBox();
            this.buttonsTableLayoutPanel = new System.Windows.Forms.TableLayoutPanel();
            this.exitButton = new System.Windows.Forms.Button();
            this.restoreButton = new System.Windows.Forms.Button();
            this.searchBoxTooltip = new System.Windows.Forms.ToolTip(this.components);
            this.userProfileSettingsGroupBox = new System.Windows.Forms.GroupBox();
            this.userProfileGroupBox = new System.Windows.Forms.GroupBox();
            this.copySettingsFromCurrentSelectionCheckBox = new System.Windows.Forms.CheckBox();
            this.createNewProfileButton = new System.Windows.Forms.Button();
            this.profilesLabel = new System.Windows.Forms.Label();
            this.activateProfileButton = new System.Windows.Forms.Button();
            this.profileSelectionComboBox = new System.Windows.Forms.ComboBox();
            this.activateNewProfileCheckBox = new System.Windows.Forms.CheckBox();
            this.activateProfileButtonTooltip = new System.Windows.Forms.ToolTip(this.components);
            this.showCommonCheckboxTooltip = new System.Windows.Forms.ToolTip(this.components);
            this.showChangedCheckboxTooltip = new System.Windows.Forms.ToolTip(this.components);
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.toolStripMenuItemResetToDefault = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemCopyPropertyToClipboard = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemCopyTooltipToClipboard = new System.Windows.Forms.ToolStripMenuItem();
            this.mainTableLayoutPanel.SuspendLayout();
            this.headerTableLayoutPanel.SuspendLayout();
            this.buttonsTableLayoutPanel.SuspendLayout();
            this.userProfileSettingsGroupBox.SuspendLayout();
            this.userProfileGroupBox.SuspendLayout();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // saveButton
            // 
            this.saveButton.Dock = System.Windows.Forms.DockStyle.Top;
            this.saveButton.Location = new System.Drawing.Point(3, 3);
            this.saveButton.Name = "saveButton";
            this.saveButton.Size = new System.Drawing.Size(159, 40);
            this.saveButton.TabIndex = 1;
            this.saveButton.Text = "save_and_restart";
            this.saveButton.UseVisualStyleBackColor = true;
            this.saveButton.Click += new System.EventHandler(this.saveButton_Click);
            // 
            // propertiesFlowLayoutPanel
            // 
            this.propertiesFlowLayoutPanel.AutoScroll = true;
            this.propertiesFlowLayoutPanel.AutoSize = true;
            this.propertiesFlowLayoutPanel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.propertiesFlowLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.propertiesFlowLayoutPanel.Location = new System.Drawing.Point(3, 35);
            this.propertiesFlowLayoutPanel.Name = "propertiesFlowLayoutPanel";
            this.propertiesFlowLayoutPanel.Size = new System.Drawing.Size(994, 608);
            this.propertiesFlowLayoutPanel.TabIndex = 0;
            this.propertiesFlowLayoutPanel.TabStop = true;
            // 
            // searchTextBox
            // 
            this.searchTextBox.Margin = new System.Windows.Forms.Padding(5, 3, 0, 3);
            this.searchTextBox.Name = "searchTextBox";
            this.searchTextBox.Size = new System.Drawing.Size(180, 20);
            this.searchTextBox.TabIndex = 15;
            this.searchTextBox.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            this.searchTextBox.Enter += new System.EventHandler(this.SearchTextBox_GotFocus);
            this.searchTextBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.SearchTextBox_KeyDown);
            this.searchTextBox.Leave += new System.EventHandler(this.SearchTextBox_LostFocus);
            // 
            // mainTableLayoutPanel
            // 
            this.mainTableLayoutPanel.ColumnCount = 1;
            this.mainTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.mainTableLayoutPanel.Controls.Add(this.headerTableLayoutPanel, 0, 0);
            this.mainTableLayoutPanel.Controls.Add(this.propertiesFlowLayoutPanel, 0, 1);
            this.mainTableLayoutPanel.Controls.Add(this.buttonsTableLayoutPanel, 0, 2);
            this.mainTableLayoutPanel.Location = new System.Drawing.Point(6, 19);
            this.mainTableLayoutPanel.Name = "mainTableLayoutPanel";
            this.mainTableLayoutPanel.RowCount = 3;
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 4.55192F));
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 87.33997F));
            this.mainTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 8F));
            this.mainTableLayoutPanel.Size = new System.Drawing.Size(1000, 703);
            this.mainTableLayoutPanel.TabIndex = 0;
            // 
            // headerTableLayoutPanel
            // 
            this.headerTableLayoutPanel.ColumnCount = 8;
            this.headerTableLayoutPanel.Controls.Add(this.gameFilterLabel, 0, 0);
            this.headerTableLayoutPanel.Controls.Add(this.filterBox, 1, 0);
            this.headerTableLayoutPanel.Controls.Add(this.showCommonCheckbox, 2, 0);
            this.headerTableLayoutPanel.Controls.Add(this.categoriesLabel, 3, 0);
            this.headerTableLayoutPanel.Controls.Add(this.categoriesBox, 4, 0);
            this.headerTableLayoutPanel.Controls.Add(this.showChangedCheckbox, 5, 0);
            this.headerTableLayoutPanel.Controls.Add(this.showNotDefaultCheckbox, 6, 0);
            this.headerTableLayoutPanel.Controls.Add(this.searchTextBox, 7, 0);
            this.headerTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.headerTableLayoutPanel.Location = new System.Drawing.Point(0, 0);
            this.headerTableLayoutPanel.Margin = new System.Windows.Forms.Padding(0);
            this.headerTableLayoutPanel.Name = "headerTableLayoutPanel";
            this.headerTableLayoutPanel.RowCount = 1;
            this.headerTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.headerTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
            this.headerTableLayoutPanel.Size = new System.Drawing.Size(1000, 32);
            this.headerTableLayoutPanel.TabIndex = 0;
            // 
            // gameFilterLabel
            // 
            this.gameFilterLabel.Location = new System.Drawing.Point(3, 0);
            this.gameFilterLabel.Name = "gameFilterLabel";
            this.gameFilterLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.gameFilterLabel.Size = new System.Drawing.Size(60, 23);
            this.gameFilterLabel.TabIndex = 9;
            this.gameFilterLabel.Text = "game_filter_label";
            this.gameFilterLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // filterBox
            // 
            this.filterBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.filterBox.Location = new System.Drawing.Point(66, 3);
            this.filterBox.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
            this.filterBox.MaximumSize = new System.Drawing.Size(153, 0);
            this.filterBox.MinimumSize = new System.Drawing.Size(153, 0);
            this.filterBox.Name = "filterBox";
            this.filterBox.Size = new System.Drawing.Size(153, 21);
            this.filterBox.TabIndex = 10;
            this.filterBox.SelectedValueChanged += new System.EventHandler(this.FilterBox_SelectedValueChanged);
            // 
            // showCommonCheckbox
            // 
            this.showCommonCheckbox.CheckAlign = System.Drawing.ContentAlignment.TopLeft;
            this.showCommonCheckbox.Checked = true;
            this.showCommonCheckbox.CheckState = System.Windows.Forms.CheckState.Checked;
            this.showCommonCheckbox.Margin = new System.Windows.Forms.Padding(3, 4, 0, 0);
            this.showCommonCheckbox.Name = "showCommonCheckbox";
            this.showCommonCheckbox.Size = new System.Drawing.Size(100, 20);
            this.showCommonCheckbox.TabIndex = 11;
            this.showCommonCheckbox.Text = "show_common_props_label";
            this.showCommonCheckbox.CheckedChanged += new System.EventHandler(this.ShowCommonCheckbox_CheckedChanged);
            // 
            // categoriesLabel
            // 
            this.categoriesLabel.Name = "categoriesLabel";
            this.categoriesLabel.Padding = new System.Windows.Forms.Padding(0, 4, 0, 0);
            this.categoriesLabel.Size = new System.Drawing.Size(89, 20);
            this.categoriesLabel.TabIndex = 12;
            this.categoriesLabel.Text = "category_filter_label";
            this.categoriesLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // categoriesBox
            // 
            this.categoriesBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.categoriesBox.Margin = new System.Windows.Forms.Padding(0, 3, 3, 3);
            this.categoriesBox.MaximumSize = new System.Drawing.Size(153, 0);
            this.categoriesBox.MinimumSize = new System.Drawing.Size(153, 0);
            this.categoriesBox.Name = "categoriesBox";
            this.categoriesBox.Size = new System.Drawing.Size(153, 21);
            this.categoriesBox.TabIndex = 13;
            this.categoriesBox.SelectedValueChanged += new System.EventHandler(this.CategoriesBox_SelectedValueChanged);
            // 
            // showChangedCheckbox
            // 
            this.showChangedCheckbox.CheckAlign = System.Drawing.ContentAlignment.TopLeft;
            this.showChangedCheckbox.Checked = false;
            this.showChangedCheckbox.CheckState = System.Windows.Forms.CheckState.Unchecked;
            this.showChangedCheckbox.Location = new System.Drawing.Point(286, 5);
            this.showChangedCheckbox.Margin = new System.Windows.Forms.Padding(7, 4, 3, 0);
            this.showChangedCheckbox.Name = "showChangedCheckbox";
            this.showChangedCheckbox.Size = new System.Drawing.Size(101, 20);
            this.showChangedCheckbox.TabIndex = 14;
            this.showChangedCheckbox.Text = "show_changed_props_label";
            this.showChangedCheckbox.CheckedChanged += new System.EventHandler(this.ShowChangedCheckbox_CheckedChanged);
            // 
            // showNotDefaultCheckbox
            // 
            this.showNotDefaultCheckbox.CheckAlign = System.Drawing.ContentAlignment.TopLeft;
            this.showNotDefaultCheckbox.Checked = false;
            this.showNotDefaultCheckbox.CheckState = System.Windows.Forms.CheckState.Unchecked;
            this.showNotDefaultCheckbox.Margin = new System.Windows.Forms.Padding(3, 4, 3, 0);
            this.showNotDefaultCheckbox.Name = "showNotDefaultCheckbox";
            this.showNotDefaultCheckbox.Size = new System.Drawing.Size(110, 20);
            this.showNotDefaultCheckbox.TabIndex = 14;
            this.showNotDefaultCheckbox.Text = "show_non_default_props_label";
            this.showNotDefaultCheckbox.CheckedChanged += new System.EventHandler(this.showNotDefaultCheckbox_CheckedChanged);
            // 
            // buttonsTableLayoutPanel
            // 
            this.buttonsTableLayoutPanel.ColumnCount = 4;
            this.buttonsTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17F));
            this.buttonsTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17F));
            this.buttonsTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 49F));
            this.buttonsTableLayoutPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 17F));
            this.buttonsTableLayoutPanel.Controls.Add(this.saveButton, 0, 0);
            this.buttonsTableLayoutPanel.Controls.Add(this.exitButton, 1, 0);
            this.buttonsTableLayoutPanel.Controls.Add(this.restoreButton, 3, 0);
            this.buttonsTableLayoutPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonsTableLayoutPanel.Location = new System.Drawing.Point(0, 646);
            this.buttonsTableLayoutPanel.Margin = new System.Windows.Forms.Padding(0);
            this.buttonsTableLayoutPanel.Name = "buttonsTableLayoutPanel";
            this.buttonsTableLayoutPanel.RowCount = 1;
            this.buttonsTableLayoutPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.buttonsTableLayoutPanel.Size = new System.Drawing.Size(1000, 57);
            this.buttonsTableLayoutPanel.TabIndex = 0;
            // 
            // exitButton
            // 
            this.exitButton.Dock = System.Windows.Forms.DockStyle.Top;
            this.exitButton.Location = new System.Drawing.Point(168, 3);
            this.exitButton.Name = "exitButton";
            this.exitButton.Size = new System.Drawing.Size(159, 40);
            this.exitButton.TabIndex = 0;
            this.exitButton.Text = "exit_without_saving";
            this.exitButton.UseVisualStyleBackColor = true;
            this.exitButton.Click += new System.EventHandler(this.exitButton_Click);
            // 
            // restoreButton
            // 
            this.restoreButton.Dock = System.Windows.Forms.DockStyle.Top;
            this.restoreButton.Location = new System.Drawing.Point(810, 3);
            this.restoreButton.Name = "restoreButton";
            this.restoreButton.Size = new System.Drawing.Size(162, 40);
            this.restoreButton.TabIndex = 2;
            this.restoreButton.Text = "restore_default_settings";
            this.restoreButton.UseVisualStyleBackColor = true;
            this.restoreButton.Click += new System.EventHandler(this.restoreButton_Click);
            // 
            // searchBoxTooltip
            // 
            this.searchBoxTooltip.AutoPopDelay = 5000;
            this.searchBoxTooltip.InitialDelay = 250;
            this.searchBoxTooltip.IsBalloon = true;
            this.searchBoxTooltip.ReshowDelay = 100;
            // 
            // userProfileSettingsGroupBox
            // 
            this.userProfileSettingsGroupBox.Controls.Add(this.mainTableLayoutPanel);
            this.userProfileSettingsGroupBox.Location = new System.Drawing.Point(12, 84);
            this.userProfileSettingsGroupBox.Name = "userProfileSettingsGroupBox";
            this.userProfileSettingsGroupBox.Size = new System.Drawing.Size(1000, 731);
            this.userProfileSettingsGroupBox.TabIndex = 1;
            this.userProfileSettingsGroupBox.TabStop = false;
            this.userProfileSettingsGroupBox.Text = "user_profile_settings";
            // 
            // userProfileGroupBox
            // 
            this.userProfileGroupBox.Controls.Add(this.activateNewProfileCheckBox);
            this.userProfileGroupBox.Controls.Add(this.copySettingsFromCurrentSelectionCheckBox);
            this.userProfileGroupBox.Controls.Add(this.createNewProfileButton);
            this.userProfileGroupBox.Controls.Add(this.profilesLabel);
            this.userProfileGroupBox.Controls.Add(this.activateProfileButton);
            this.userProfileGroupBox.Controls.Add(this.profileSelectionComboBox);
            this.userProfileGroupBox.Location = new System.Drawing.Point(12, 13);
            this.userProfileGroupBox.Name = "userProfileGroupBox";
            this.userProfileGroupBox.Size = new System.Drawing.Size(1000, 65);
            this.userProfileGroupBox.TabIndex = 2;
            this.userProfileGroupBox.TabStop = false;
            this.userProfileGroupBox.Text = "user_profile";
            // 
            // copySettingsFromCurrentSelectionCheckBox
            // 
            this.copySettingsFromCurrentSelectionCheckBox.AutoSize = true;
            this.copySettingsFromCurrentSelectionCheckBox.Location = new System.Drawing.Point(639, 17);
            this.copySettingsFromCurrentSelectionCheckBox.Name = "copySettingsFromCurrentSelectionCheckBox";
            this.copySettingsFromCurrentSelectionCheckBox.Size = new System.Drawing.Size(156, 17);
            this.copySettingsFromCurrentSelectionCheckBox.TabIndex = 7;
            this.copySettingsFromCurrentSelectionCheckBox.Text = "copy_settings_from_current";
            this.copySettingsFromCurrentSelectionCheckBox.UseVisualStyleBackColor = true;
            // 
            // createNewProfileButton
            // 
            this.createNewProfileButton.Location = new System.Drawing.Point(491, 16);
            this.createNewProfileButton.Name = "createNewProfileButton";
            this.createNewProfileButton.Size = new System.Drawing.Size(142, 40);
            this.createNewProfileButton.TabIndex = 6;
            this.createNewProfileButton.Text = "create_new_profile";
            this.createNewProfileButton.UseVisualStyleBackColor = true;
            this.createNewProfileButton.Click += new System.EventHandler(this.createNewProfileButton_Click);
            // 
            // profilesLabel
            // 
            this.profilesLabel.AutoSize = true;
            this.profilesLabel.Location = new System.Drawing.Point(15, 28);
            this.profilesLabel.Name = "profilesLabel";
            this.profilesLabel.Size = new System.Drawing.Size(40, 13);
            this.profilesLabel.TabIndex = 0;
            this.profilesLabel.Text = "profiles";
            // 
            // activateProfileButton
            // 
            this.activateProfileButton.Location = new System.Drawing.Point(290, 16);
            this.activateProfileButton.Name = "activateProfileButton";
            this.activateProfileButton.Size = new System.Drawing.Size(159, 40);
            this.activateProfileButton.TabIndex = 2;
            this.activateProfileButton.Text = "activate_profile";
            this.activateProfileButton.UseVisualStyleBackColor = true;
            this.activateProfileButton.Click += new System.EventHandler(this.activateProfileButton_Click);
            // 
            // profileSelectionComboBox
            // 
            this.profileSelectionComboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.profileSelectionComboBox.FormattingEnabled = true;
            this.profileSelectionComboBox.Location = new System.Drawing.Point(72, 25);
            this.profileSelectionComboBox.Name = "profileSelectionComboBox";
            this.profileSelectionComboBox.Size = new System.Drawing.Size(203, 21);
            this.profileSelectionComboBox.TabIndex = 1;
            this.profileSelectionComboBox.SelectedValueChanged += new System.EventHandler(this.profileSelectionComboBox_SelectedValueChanged);
            // 
            // activateNewProfileCheckBox
            // 
            this.activateNewProfileCheckBox.AutoSize = true;
            this.activateNewProfileCheckBox.Location = new System.Drawing.Point(639, 40);
            this.activateNewProfileCheckBox.Name = "activateNewProfileCheckBox";
            this.activateNewProfileCheckBox.Size = new System.Drawing.Size(124, 17);
            this.activateNewProfileCheckBox.TabIndex = 8;
            this.activateNewProfileCheckBox.Text = "activate_new_profile";
            this.activateNewProfileCheckBox.UseVisualStyleBackColor = true;
            // 
            // activateProfileButtonTooltip
            // 
            this.activateProfileButtonTooltip.AutoPopDelay = 5000;
            this.activateProfileButtonTooltip.InitialDelay = 700;
            this.activateProfileButtonTooltip.IsBalloon = true;
            this.activateProfileButtonTooltip.ReshowDelay = 100;
            // 
            // showCommonCheckboxTooltip
            // 
            this.showCommonCheckboxTooltip.AutoPopDelay = 5000;
            this.showCommonCheckboxTooltip.InitialDelay = 700;
            this.showCommonCheckboxTooltip.IsBalloon = true;
            this.showCommonCheckboxTooltip.ReshowDelay = 100;
            // 
            // showChangedCheckboxTooltip
            // 
            this.showChangedCheckboxTooltip.AutoPopDelay = 5000;
            this.showChangedCheckboxTooltip.InitialDelay = 700;
            this.showChangedCheckboxTooltip.IsBalloon = true;
            this.showChangedCheckboxTooltip.ReshowDelay = 100;
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.ImageScalingSize = new System.Drawing.Size(24, 24);
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemResetToDefault,
            this.toolStripMenuItemCopyPropertyToClipboard,
            this.toolStripMenuItemCopyTooltipToClipboard});
            this.contextMenuStrip.Name = "contextMenuStrip";
            this.contextMenuStrip.Size = new System.Drawing.Size(303, 100);
            this.contextMenuStrip.Click += new System.EventHandler(this.contextMenuStrip_Click);
            this.contextMenuStrip.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(this.contextMenuStrip_ItemClicked);
            // 
            // toolStripMenuItemResetToDefault
            // 
            this.toolStripMenuItemResetToDefault.Name = "toolStripMenuItemResetToDefault";
            this.toolStripMenuItemResetToDefault.Size = new System.Drawing.Size(302, 32);
            this.toolStripMenuItemResetToDefault.Text = "Reset to default";
            // 
            // toolStripMenuItemCopyPropertyToClipboard
            // 
            this.toolStripMenuItemCopyPropertyToClipboard.Name = "toolStripMenuItemCopyPropertyToClipboard";
            this.toolStripMenuItemCopyPropertyToClipboard.Size = new System.Drawing.Size(302, 32);
            this.toolStripMenuItemCopyPropertyToClipboard.Text = "Copy property name to clipboard";
            // 
            // toolStripMenuItemCopyTooltipToClipboard
            // 
            this.toolStripMenuItemCopyTooltipToClipboard.Name = "toolStripMenuItemCopyTooltipToClipboard";
            this.toolStripMenuItemCopyTooltipToClipboard.Size = new System.Drawing.Size(302, 32);
            this.toolStripMenuItemCopyTooltipToClipboard.Text = "Copy tooltip to clipboard";
            // PropertiesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoScroll = true;
            this.Controls.Add(this.userProfileGroupBox);
            this.Controls.Add(this.userProfileSettingsGroupBox);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PropertiesForm";
            this.Text = "properties_form";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.properties_FormClosing);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.PropertiesForm_KeyDown);
            this.mainTableLayoutPanel.ResumeLayout(false);
            this.mainTableLayoutPanel.PerformLayout();
            this.headerTableLayoutPanel.ResumeLayout(false);
            this.headerTableLayoutPanel.PerformLayout();
            this.buttonsTableLayoutPanel.ResumeLayout(false);
            this.userProfileSettingsGroupBox.ResumeLayout(false);
            this.userProfileGroupBox.ResumeLayout(false);
            this.userProfileGroupBox.PerformLayout();
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button saveButton;
        private System.Windows.Forms.FlowLayoutPanel propertiesFlowLayoutPanel;
        private System.Windows.Forms.TextBox searchTextBox;
        private System.Windows.Forms.TableLayoutPanel mainTableLayoutPanel;
        private System.Windows.Forms.TableLayoutPanel headerTableLayoutPanel;
        private System.Windows.Forms.TableLayoutPanel buttonsTableLayoutPanel;
        private System.Windows.Forms.ToolTip searchBoxTooltip;
        private System.Windows.Forms.Button exitButton;
        private System.Windows.Forms.Button restoreButton;
        private System.Windows.Forms.Label gameFilterLabel;
        private System.Windows.Forms.ComboBox filterBox;
        private System.Windows.Forms.CheckBox showCommonCheckbox;
        private System.Windows.Forms.CheckBox showChangedCheckbox;
        private System.Windows.Forms.CheckBox showNotDefaultCheckbox;
        private System.Windows.Forms.Label categoriesLabel;
        private System.Windows.Forms.ComboBox categoriesBox;
        private GroupBox userProfileSettingsGroupBox;
        private GroupBox userProfileGroupBox;
        private ComboBox profileSelectionComboBox;
        private Label profilesLabel;
        private Button activateProfileButton;
        private CheckBox copySettingsFromCurrentSelectionCheckBox;
        private Button createNewProfileButton;
        private CheckBox activateNewProfileCheckBox;
        private System.Windows.Forms.ToolTip activateProfileButtonTooltip;
        private System.Windows.Forms.ToolTip showCommonCheckboxTooltip;
        private System.Windows.Forms.ToolTip showChangedCheckboxTooltip;
        private ContextMenuStrip contextMenuStrip;
        private ToolStripMenuItem toolStripMenuItemResetToDefault;
        private ToolStripMenuItem toolStripMenuItemCopyPropertyToClipboard;
        private ToolStripMenuItem toolStripMenuItemCopyTooltipToClipboard;
    }
}
