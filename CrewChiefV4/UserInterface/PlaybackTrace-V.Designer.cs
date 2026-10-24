namespace CrewChiefV4.UserInterface
{
    partial class PlaybackTraceWindow
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
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.textBoxPlaybackFile = new System.Windows.Forms.TextBox();
            this.buttonCopyTraceFile = new System.Windows.Forms.Button();
            this.checkBoxPlayback = new System.Windows.Forms.CheckBox();
            this.buttonChooseTraceFile = new System.Windows.Forms.Button();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.labelPlaybackSpeed = new System.Windows.Forms.Label();
            this.hScrollBarPlaybackSpeed = new System.Windows.Forms.HScrollBar();
            this.textBoxPlaybackSpeed = new System.Windows.Forms.TextBox();
            this.textBoxPlaybackFiletoolTip = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel4 = new System.Windows.Forms.TableLayoutPanel();
            this.label1 = new System.Windows.Forms.Label();
            this.tableLayoutPanel1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.tableLayoutPanel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.CellBorderStyle = System.Windows.Forms.TableLayoutPanelCellBorderStyle.Inset;
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(21, 23);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 1;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(613, 326);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 1;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.Controls.Add(this.buttonCopyTraceFile, 0, 4);
            this.tableLayoutPanel2.Controls.Add(this.checkBoxPlayback, 0, 0);
            this.tableLayoutPanel2.Controls.Add(this.buttonChooseTraceFile, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.tableLayoutPanel3, 0, 3);
            this.tableLayoutPanel2.Controls.Add(this.tableLayoutPanel4, 0, 2);
            this.tableLayoutPanel2.Location = new System.Drawing.Point(5, 5);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 5;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(603, 301);
            this.tableLayoutPanel2.TabIndex = 2;
            // 
            // textBoxPlaybackFile
            // 
            this.textBoxPlaybackFile.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxPlaybackFile.Location = new System.Drawing.Point(103, 14);
            this.textBoxPlaybackFile.Margin = new System.Windows.Forms.Padding(10, 3, 10, 3);
            this.textBoxPlaybackFile.Name = "textBoxPlaybackFile";
            this.textBoxPlaybackFile.Size = new System.Drawing.Size(483, 26);
            this.textBoxPlaybackFile.TabIndex = 3;
            // 
            // buttonCopyTraceFile
            // 
            this.buttonCopyTraceFile.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.buttonCopyTraceFile.Location = new System.Drawing.Point(3, 253);
            this.buttonCopyTraceFile.Name = "buttonCopyTraceFile";
            this.buttonCopyTraceFile.Size = new System.Drawing.Size(320, 34);
            this.buttonCopyTraceFile.TabIndex = 5;
            this.buttonCopyTraceFile.Text = "Copy a trace file from somewhere";
            this.buttonCopyTraceFile.UseVisualStyleBackColor = true;
            this.buttonCopyTraceFile.Click += new System.EventHandler(this.buttonCopyTraceFile_Click);
            // 
            // checkBoxPlayback
            // 
            this.checkBoxPlayback.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.checkBoxPlayback.AutoSize = true;
            this.checkBoxPlayback.Location = new System.Drawing.Point(3, 18);
            this.checkBoxPlayback.Name = "checkBoxPlayback";
            this.checkBoxPlayback.Size = new System.Drawing.Size(98, 24);
            this.checkBoxPlayback.TabIndex = 1;
            this.checkBoxPlayback.Text = "Playback";
            this.checkBoxPlayback.UseVisualStyleBackColor = true;
            this.checkBoxPlayback.CheckedChanged += new System.EventHandler(this.checkBoxPlayback_CheckedChanged);
            // 
            // buttonChooseTraceFile
            // 
            this.buttonChooseTraceFile.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.buttonChooseTraceFile.FlatAppearance.BorderSize = 4;
            this.buttonChooseTraceFile.Location = new System.Drawing.Point(3, 72);
            this.buttonChooseTraceFile.Name = "buttonChooseTraceFile";
            this.buttonChooseTraceFile.Size = new System.Drawing.Size(320, 35);
            this.buttonChooseTraceFile.TabIndex = 2;
            this.buttonChooseTraceFile.Text = "Choose an available trace file";
            this.buttonChooseTraceFile.UseVisualStyleBackColor = true;
            this.buttonChooseTraceFile.Click += new System.EventHandler(this.buttonChooseTraceFile_Click);
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.ColumnCount = 3;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 23.12812F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 8.818636F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 67.88686F));
            this.tableLayoutPanel3.Controls.Add(this.labelPlaybackSpeed, 0, 0);
            this.tableLayoutPanel3.Controls.Add(this.hScrollBarPlaybackSpeed, 2, 0);
            this.tableLayoutPanel3.Controls.Add(this.textBoxPlaybackSpeed, 1, 0);
            this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 183);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 1;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(597, 47);
            this.tableLayoutPanel3.TabIndex = 6;
            // 
            // labelPlaybackSpeed
            // 
            this.labelPlaybackSpeed.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelPlaybackSpeed.AutoSize = true;
            this.labelPlaybackSpeed.Location = new System.Drawing.Point(15, 13);
            this.labelPlaybackSpeed.Name = "labelPlaybackSpeed";
            this.labelPlaybackSpeed.Size = new System.Drawing.Size(120, 20);
            this.labelPlaybackSpeed.TabIndex = 0;
            this.labelPlaybackSpeed.Text = "Playback speed";
            // 
            // hScrollBarPlaybackSpeed
            // 
            this.hScrollBarPlaybackSpeed.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.hScrollBarPlaybackSpeed.Location = new System.Drawing.Point(195, 6);
            this.hScrollBarPlaybackSpeed.Margin = new System.Windows.Forms.Padding(5);
            this.hScrollBarPlaybackSpeed.Maximum = 30;
            this.hScrollBarPlaybackSpeed.Name = "hScrollBarPlaybackSpeed";
            this.hScrollBarPlaybackSpeed.Padding = new System.Windows.Forms.Padding(5);
            this.hScrollBarPlaybackSpeed.Size = new System.Drawing.Size(397, 34);
            this.hScrollBarPlaybackSpeed.TabIndex = 1;
            this.hScrollBarPlaybackSpeed.Scroll += new System.Windows.Forms.ScrollEventHandler(this.hScrollBarPlaybackSpeed_Scroll);
            // 
            // textBoxPlaybackSpeed
            // 
            this.textBoxPlaybackSpeed.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.textBoxPlaybackSpeed.Location = new System.Drawing.Point(142, 10);
            this.textBoxPlaybackSpeed.Name = "textBoxPlaybackSpeed";
            this.textBoxPlaybackSpeed.ReadOnly = true;
            this.textBoxPlaybackSpeed.Size = new System.Drawing.Size(44, 26);
            this.textBoxPlaybackSpeed.TabIndex = 2;
            this.textBoxPlaybackSpeed.Text = "0";
            // 
            // tableLayoutPanel4
            // 
            this.tableLayoutPanel4.ColumnCount = 2;
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 15.60403F));
            this.tableLayoutPanel4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 84.39597F));
            this.tableLayoutPanel4.Controls.Add(this.textBoxPlaybackFile, 1, 0);
            this.tableLayoutPanel4.Controls.Add(this.label1, 0, 0);
            this.tableLayoutPanel4.Location = new System.Drawing.Point(3, 123);
            this.tableLayoutPanel4.Name = "tableLayoutPanel4";
            this.tableLayoutPanel4.RowCount = 1;
            this.tableLayoutPanel4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel4.Size = new System.Drawing.Size(596, 54);
            this.tableLayoutPanel4.TabIndex = 7;
            // 
            // label1
            // 
            this.label1.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(17, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(73, 20);
            this.label1.TabIndex = 4;
            this.label1.Text = "Trace file";
            // 
            // PlaybackTraceWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(651, 360);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PlaybackTraceWindow";
            this.Text = "Game Trace Files";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.tableLayoutPanel3.PerformLayout();
            this.tableLayoutPanel4.ResumeLayout(false);
            this.tableLayoutPanel4.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TextBox textBoxPlaybackFile;
        private System.Windows.Forms.Button buttonCopyTraceFile;
        private System.Windows.Forms.CheckBox checkBoxPlayback;
        private System.Windows.Forms.Button buttonChooseTraceFile;
        private System.Windows.Forms.ToolTip textBoxPlaybackFiletoolTip;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.Label labelPlaybackSpeed;
        private System.Windows.Forms.HScrollBar hScrollBarPlaybackSpeed;
        private System.Windows.Forms.TextBox textBoxPlaybackSpeed;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel4;
        private System.Windows.Forms.Label label1;
    }
}