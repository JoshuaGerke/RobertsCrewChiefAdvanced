namespace CrewChiefV4.UserInterface
{
    partial class OpponentNames_V
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OpponentNames_V));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.buttonEditGuessedNames = new System.Windows.Forms.Button();
            this.listBoxOpponentNames = new System.Windows.Forms.ListBox();
            this.labelOpponentNames = new System.Windows.Forms.Label();
            this.buttonEditOpponentNames = new System.Windows.Forms.Button();
            this.buttonDeleteGuessedNames = new System.Windows.Forms.Button();
            this.contextMenuStripOpponentNames = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.playToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.findAnotherSoundToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolTipEditOpponentNamesFile = new System.Windows.Forms.ToolTip(this.components);
            this.toolTipEditGuessedNamesFile = new System.Windows.Forms.ToolTip(this.components);
            this.toolTipNamesList = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.contextMenuStripOpponentNames.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Controls.Add(this.buttonEditGuessedNames, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.listBoxOpponentNames, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.labelOpponentNames, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.buttonEditOpponentNames, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.buttonDeleteGuessedNames, 1, 2);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(19, 22);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 12.65823F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 29.11392F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 29.11392F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 29.11392F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(664, 388);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // buttonEditGuessedNames
            // 
            this.buttonEditGuessedNames.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonEditGuessedNames.Location = new System.Drawing.Point(392, 64);
            this.buttonEditGuessedNames.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonEditGuessedNames.Name = "buttonEditGuessedNames";
            this.buttonEditGuessedNames.Size = new System.Drawing.Size(211, 82);
            this.buttonEditGuessedNames.TabIndex = 14;
            this.buttonEditGuessedNames.Text = "EditGuessedNamesFile";
            this.buttonEditGuessedNames.UseVisualStyleBackColor = true;
            this.buttonEditGuessedNames.MouseClick += new System.Windows.Forms.MouseEventHandler(this.buttonEditGuessedNames_Click);
            // 
            // listBoxOpponentNames
            // 
            this.listBoxOpponentNames.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxOpponentNames.FormattingEnabled = true;
            this.listBoxOpponentNames.ItemHeight = 25;
            this.listBoxOpponentNames.Location = new System.Drawing.Point(3, 51);
            this.listBoxOpponentNames.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.listBoxOpponentNames.Name = "listBoxOpponentNames";
            this.tableLayoutPanel1.SetRowSpan(this.listBoxOpponentNames, 3);
            this.listBoxOpponentNames.ScrollAlwaysVisible = true;
            this.listBoxOpponentNames.Size = new System.Drawing.Size(326, 329);
            this.listBoxOpponentNames.TabIndex = 12;
            this.listBoxOpponentNames.DoubleClick += new System.EventHandler(this.listBoxOpponentNames_MouseDoubleClick);
            this.listBoxOpponentNames.MouseDown += new System.Windows.Forms.MouseEventHandler(this.listBoxOpponentNames_MouseDown);
            // 
            // labelOpponentNames
            // 
            this.labelOpponentNames.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelOpponentNames.AutoSize = true;
            this.labelOpponentNames.Location = new System.Drawing.Point(3, 0);
            this.labelOpponentNames.Name = "labelOpponentNames";
            this.labelOpponentNames.Size = new System.Drawing.Size(326, 49);
            this.labelOpponentNames.TabIndex = 11;
            this.labelOpponentNames.Text = "GuessedOpponentNames";
            this.labelOpponentNames.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            // 
            // buttonEditOpponentNames
            // 
            this.buttonEditOpponentNames.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonEditOpponentNames.Location = new System.Drawing.Point(392, 289);
            this.buttonEditOpponentNames.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.buttonEditOpponentNames.Name = "buttonEditOpponentNames";
            this.buttonEditOpponentNames.Size = new System.Drawing.Size(211, 82);
            this.buttonEditOpponentNames.TabIndex = 13;
            this.buttonEditOpponentNames.Text = "EditOpponentNamesFile";
            this.buttonEditOpponentNames.UseVisualStyleBackColor = true;
            this.buttonEditOpponentNames.Click += new System.EventHandler(this.buttonEditNames_Click);
            // 
            // buttonDeleteGuessedNames
            // 
            this.buttonDeleteGuessedNames.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonDeleteGuessedNames.BackColor = System.Drawing.SystemColors.Control;
            this.buttonDeleteGuessedNames.Enabled = false;
            this.buttonDeleteGuessedNames.Location = new System.Drawing.Point(394, 176);
            this.buttonDeleteGuessedNames.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.buttonDeleteGuessedNames.Name = "buttonDeleteGuessedNames";
            this.buttonDeleteGuessedNames.Size = new System.Drawing.Size(207, 82);
            this.buttonDeleteGuessedNames.TabIndex = 15;
            this.buttonDeleteGuessedNames.Text = "DeleteGuessedNamesFile";
            this.buttonDeleteGuessedNames.UseVisualStyleBackColor = true;
            this.buttonDeleteGuessedNames.Visible = false;
            this.buttonDeleteGuessedNames.Click += new System.EventHandler(this.buttonDeleteGuessedNames_Click);
            // 
            // contextMenuStripOpponentNames
            // 
            this.contextMenuStripOpponentNames.ImageScalingSize = new System.Drawing.Size(32, 32);
            this.contextMenuStripOpponentNames.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.playToolStripMenuItem,
            this.findAnotherSoundToolStripMenuItem});
            this.contextMenuStripOpponentNames.Name = "contextMenuStripOpponentNames";
            this.contextMenuStripOpponentNames.Size = new System.Drawing.Size(298, 80);
            // 
            // playToolStripMenuItem
            // 
            this.playToolStripMenuItem.Name = "playToolStripMenuItem";
            this.playToolStripMenuItem.Size = new System.Drawing.Size(297, 38);
            this.playToolStripMenuItem.Text = "Play";
            this.playToolStripMenuItem.Click += new System.EventHandler(this.playToolStripMenuItem_Click);
            // 
            // findAnotherSoundToolStripMenuItem
            // 
            this.findAnotherSoundToolStripMenuItem.Name = "findAnotherSoundToolStripMenuItem";
            this.findAnotherSoundToolStripMenuItem.Size = new System.Drawing.Size(297, 38);
            this.findAnotherSoundToolStripMenuItem.Text = "Find another sound";
            this.findAnotherSoundToolStripMenuItem.Click += new System.EventHandler(this.ignoreToolStripMenuItem_Click);
            // 
            // OpponentNames_V
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(709, 428);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.Name = "OpponentNames_V";
            this.Text = "Opponent Names";
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.OpponentNames_V_FormClosed);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.contextMenuStripOpponentNames.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label labelOpponentNames;
        private System.Windows.Forms.ListBox listBoxOpponentNames;
        private System.Windows.Forms.Button buttonEditGuessedNames;
        private System.Windows.Forms.Button buttonEditOpponentNames;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripOpponentNames;
        private System.Windows.Forms.ToolStripMenuItem playToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem findAnotherSoundToolStripMenuItem;
        private System.Windows.Forms.Button buttonDeleteGuessedNames;
        private System.Windows.Forms.ToolTip toolTipEditOpponentNamesFile;
        private System.Windows.Forms.ToolTip toolTipEditGuessedNamesFile;
        private System.Windows.Forms.ToolTip toolTipNamesList;
    }
}