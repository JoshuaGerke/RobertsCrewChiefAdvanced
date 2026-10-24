namespace CrewChiefV4.UserInterface
{
    partial class SpeechWizard_V
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
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.buttonRestartCrewChief = new System.Windows.Forms.Button();
            this.textBoxStatus = new System.Windows.Forms.TextBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.radioButtonMicrosoftSRE = new System.Windows.Forms.RadioButton();
            this.checkBoxNaudio = new System.Windows.Forms.CheckBox();
            this.checkBoxFreeDictation = new System.Windows.Forms.CheckBox();
            this.radioButtonSystemSRE = new System.Windows.Forms.RadioButton();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.textBoxSpeechRecognitionCountry = new System.Windows.Forms.TextBox();
            this.labelSpeechRecognitionCountry = new System.Windows.Forms.Label();
            this.labelUIlanguage = new System.Windows.Forms.Label();
            this.textBoxUILanguage = new System.Windows.Forms.TextBox();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.label1 = new System.Windows.Forms.Label();
            this.buttonHelp = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tableLayoutPanel1.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Inset;
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 48.98447F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 51.01553F));
            this.tableLayoutPanel1.Controls.Add(this.textBoxStatus, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel3, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.buttonRestartCrewChief, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.buttonHelp, 1, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(-6, 3);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 66F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(839, 542);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // buttonRestartCrewChief
            // 
            this.buttonRestartCrewChief.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonRestartCrewChief.AutoSize = true;
            this.buttonRestartCrewChief.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.buttonRestartCrewChief.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.buttonRestartCrewChief.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.875F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonRestartCrewChief.Location = new System.Drawing.Point(480, 166);
            this.buttonRestartCrewChief.Name = "buttonRestartCrewChief";
            this.buttonRestartCrewChief.Size = new System.Drawing.Size(288, 41);
            this.buttonRestartCrewChief.TabIndex = 0;
            this.buttonRestartCrewChief.Text = "(Re)Start Crew Chief";
            this.buttonRestartCrewChief.UseVisualStyleBackColor = true;
            this.buttonRestartCrewChief.Click += new System.EventHandler(this.buttonRestartCrewChief_Click);
            // 
            // textBoxStatus
            // 
            this.textBoxStatus.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxStatus.Location = new System.Drawing.Point(5, 73);
            this.textBoxStatus.Multiline = true;
            this.textBoxStatus.Name = "textBoxStatus";
            this.textBoxStatus.ReadOnly = true;
            this.textBoxStatus.Size = new System.Drawing.Size(402, 228);
            this.textBoxStatus.TabIndex = 1;
            this.textBoxStatus.Text = "No activity so far";
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.Controls.Add(this.radioButtonMicrosoftSRE, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.checkBoxNaudio, 0, 2);
            this.tableLayoutPanel2.Controls.Add(this.checkBoxFreeDictation, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this.radioButtonSystemSRE, 0, 0);
            this.tableLayoutPanel2.Location = new System.Drawing.Point(5, 309);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 4;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 16.47059F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 32.94118F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(339, 228);
            this.tableLayoutPanel2.TabIndex = 2;
            // 
            // radioButtonMicrosoftSRE
            // 
            this.radioButtonMicrosoftSRE.AutoSize = true;
            this.radioButtonMicrosoftSRE.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.radioButtonMicrosoftSRE.Location = new System.Drawing.Point(3, 40);
            this.radioButtonMicrosoftSRE.Name = "radioButtonMicrosoftSRE";
            this.radioButtonMicrosoftSRE.Size = new System.Drawing.Size(204, 29);
            this.radioButtonMicrosoftSRE.TabIndex = 1;
            this.radioButtonMicrosoftSRE.TabStop = true;
            this.radioButtonMicrosoftSRE.Text = "Microsoft Speech";
            this.radioButtonMicrosoftSRE.UseVisualStyleBackColor = true;
            this.radioButtonMicrosoftSRE.CheckedChanged += new System.EventHandler(this.radioButtonMicrosoftSRE_CheckedChanged);
            // 
            // checkBoxNaudio
            // 
            this.checkBoxNaudio.AutoSize = true;
            this.checkBoxNaudio.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkBoxNaudio.Location = new System.Drawing.Point(3, 115);
            this.checkBoxNaudio.Name = "checkBoxNaudio";
            this.checkBoxNaudio.Size = new System.Drawing.Size(309, 29);
            this.checkBoxNaudio.TabIndex = 2;
            this.checkBoxNaudio.Text = "Use nAudio for speech input";
            this.checkBoxNaudio.UseVisualStyleBackColor = true;
            this.checkBoxNaudio.CheckedChanged += new System.EventHandler(this.checkBoxNaudio_CheckedChanged);
            // 
            // checkBoxFreeDictation
            // 
            this.checkBoxFreeDictation.AutoSize = true;
            this.checkBoxFreeDictation.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkBoxFreeDictation.Location = new System.Drawing.Point(2, 171);
            this.checkBoxFreeDictation.Margin = new System.Windows.Forms.Padding(2);
            this.checkBoxFreeDictation.Name = "checkBoxFreeDictation";
            this.checkBoxFreeDictation.Size = new System.Drawing.Size(313, 29);
            this.checkBoxFreeDictation.TabIndex = 3;
            this.checkBoxFreeDictation.Text = "Enable free dictation for chat";
            this.checkBoxFreeDictation.UseVisualStyleBackColor = true;
            this.checkBoxFreeDictation.CheckedChanged += new System.EventHandler(this.checkBoxFreeDictation_CheckedChanged);
            // 
            // radioButtonSystemSRE
            // 
            this.radioButtonSystemSRE.AutoSize = true;
            this.radioButtonSystemSRE.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.radioButtonSystemSRE.Location = new System.Drawing.Point(3, 3);
            this.radioButtonSystemSRE.Name = "radioButtonSystemSRE";
            this.radioButtonSystemSRE.Size = new System.Drawing.Size(294, 29);
            this.radioButtonSystemSRE.TabIndex = 1;
            this.radioButtonSystemSRE.TabStop = true;
            this.radioButtonSystemSRE.Text = "Windows (System) Speech";
            this.radioButtonSystemSRE.UseVisualStyleBackColor = true;
            this.radioButtonSystemSRE.CheckedChanged += new System.EventHandler(this.radioButtonSystem_CheckedChanged);
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel3.ColumnCount = 1;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.Controls.Add(this.textBoxSpeechRecognitionCountry, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this.labelSpeechRecognitionCountry, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this.labelUIlanguage, 0, 2);
            this.tableLayoutPanel3.Controls.Add(this.textBoxUILanguage, 0, 3);
            this.tableLayoutPanel3.Location = new System.Drawing.Point(414, 308);
            this.tableLayoutPanel3.Margin = new System.Windows.Forms.Padding(2);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 4;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(421, 230);
            this.tableLayoutPanel3.TabIndex = 3;
            // 
            // textBoxSpeechRecognitionCountry
            // 
            this.textBoxSpeechRecognitionCountry.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxSpeechRecognitionCountry.Location = new System.Drawing.Point(2, 59);
            this.textBoxSpeechRecognitionCountry.Margin = new System.Windows.Forms.Padding(2);
            this.textBoxSpeechRecognitionCountry.Name = "textBoxSpeechRecognitionCountry";
            this.textBoxSpeechRecognitionCountry.Size = new System.Drawing.Size(128, 30);
            this.textBoxSpeechRecognitionCountry.TabIndex = 3;
            this.textBoxSpeechRecognitionCountry.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBoxSpeechRecognitionCountry_KeyDown);
            // 
            // labelSpeechRecognitionCountry
            // 
            this.labelSpeechRecognitionCountry.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelSpeechRecognitionCountry.AutoSize = true;
            this.labelSpeechRecognitionCountry.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelSpeechRecognitionCountry.Location = new System.Drawing.Point(2, 32);
            this.labelSpeechRecognitionCountry.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelSpeechRecognitionCountry.Name = "labelSpeechRecognitionCountry";
            this.labelSpeechRecognitionCountry.Size = new System.Drawing.Size(274, 25);
            this.labelSpeechRecognitionCountry.TabIndex = 0;
            this.labelSpeechRecognitionCountry.Text = "Speech recognition country";
            // 
            // labelUIlanguage
            // 
            this.labelUIlanguage.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.labelUIlanguage.AutoSize = true;
            this.labelUIlanguage.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelUIlanguage.Location = new System.Drawing.Point(2, 146);
            this.labelUIlanguage.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelUIlanguage.Name = "labelUIlanguage";
            this.labelUIlanguage.Size = new System.Drawing.Size(127, 25);
            this.labelUIlanguage.TabIndex = 1;
            this.labelUIlanguage.Text = "UI language";
            // 
            // textBoxUILanguage
            // 
            this.textBoxUILanguage.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.125F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBoxUILanguage.Location = new System.Drawing.Point(2, 173);
            this.textBoxUILanguage.Margin = new System.Windows.Forms.Padding(2);
            this.textBoxUILanguage.Name = "textBoxUILanguage";
            this.textBoxUILanguage.Size = new System.Drawing.Size(128, 30);
            this.textBoxUILanguage.TabIndex = 2;
            this.textBoxUILanguage.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBoxUILanguage_KeyDown);
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(5, 2);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(402, 58);
            this.label1.TabIndex = 4;
            this.label1.Text = "Set Voice recognition mode in the main window first";
            // 
            // buttonHelp
            // 
            this.buttonHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonHelp.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonHelp.Location = new System.Drawing.Point(473, 5);
            this.buttonHelp.Name = "buttonHelp";
            this.buttonHelp.Size = new System.Drawing.Size(361, 51);
            this.buttonHelp.TabIndex = 5;
            this.buttonHelp.Text = "Speech Recognition Help";
            this.buttonHelp.UseVisualStyleBackColor = true;
            this.buttonHelp.Click += new System.EventHandler(this.buttonHelp_Click);
            // 
            // SpeechWizard_V
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(845, 547);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "SpeechWizard_V";
            this.Text = "Speech Wizard";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.SpeechWizard_V_FormClosed);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        internal System.Windows.Forms.Button buttonRestartCrewChief;
        public System.Windows.Forms.TextBox textBoxStatus;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        public System.Windows.Forms.CheckBox checkBoxNaudio;
        private System.Windows.Forms.CheckBox checkBoxFreeDictation;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        internal System.Windows.Forms.TextBox textBoxSpeechRecognitionCountry;
        private System.Windows.Forms.Label labelSpeechRecognitionCountry;
        private System.Windows.Forms.Label labelUIlanguage;
        internal System.Windows.Forms.TextBox textBoxUILanguage;
        private System.Windows.Forms.ToolTip toolTip1;
        public System.Windows.Forms.RadioButton radioButtonMicrosoftSRE;
        public System.Windows.Forms.RadioButton radioButtonSystemSRE;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button buttonHelp;
    }
}