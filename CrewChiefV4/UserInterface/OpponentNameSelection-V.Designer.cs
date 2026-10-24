namespace CrewChiefV4.UserInterface
{
    partial class OpponentNameSelection_V
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OpponentNameSelection_V));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.labelDriverName = new System.Windows.Forms.Label();
            this.listBoxOtherDriverNames = new System.Windows.Forms.ListBox();
            this.labelOtherDriverName = new System.Windows.Forms.Label();
            this.listBoxDriverNames = new System.Windows.Forms.ListBox();
            this.buttonNameSelect = new System.Windows.Forms.Button();
            this.labelOpponentName = new System.Windows.Forms.Label();
            this.labelOpponentNameEntry = new System.Windows.Forms.Label();
            this.buttonNoneOfTheAbove = new System.Windows.Forms.Button();
            this.buttonPlayName = new System.Windows.Forms.Button();
            this.toolTipNoneOfTheAbove = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 20F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 30F));
            this.tableLayoutPanel1.Controls.Add(this.labelDriverName, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.listBoxOtherDriverNames, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.labelOtherDriverName, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.listBoxDriverNames, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.buttonNameSelect, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.labelOpponentName, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.labelOpponentNameEntry, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.buttonNoneOfTheAbove, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.buttonPlayName, 0, 3);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(13, 14);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 10F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 65F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 15F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(676, 333);
            this.tableLayoutPanel1.TabIndex = 2;
            // 
            // labelDriverName
            // 
            this.labelDriverName.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelDriverName.AutoSize = true;
            this.labelDriverName.Location = new System.Drawing.Point(3, 33);
            this.labelDriverName.Name = "labelDriverName";
            this.labelDriverName.Size = new System.Drawing.Size(332, 33);
            this.labelDriverName.TabIndex = 4;
            this.labelDriverName.Text = "Driver name";
            this.labelDriverName.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // listBoxOtherDriverNames
            // 
            this.listBoxOtherDriverNames.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.SetColumnSpan(this.listBoxOtherDriverNames, 2);
            this.listBoxOtherDriverNames.Enabled = false;
            this.listBoxOtherDriverNames.FormattingEnabled = true;
            this.listBoxOtherDriverNames.ItemHeight = 20;
            this.listBoxOtherDriverNames.Location = new System.Drawing.Point(353, 69);
            this.listBoxOtherDriverNames.Margin = new System.Windows.Forms.Padding(15, 3, 15, 3);
            this.listBoxOtherDriverNames.Name = "listBoxOtherDriverNames";
            this.listBoxOtherDriverNames.Size = new System.Drawing.Size(308, 204);
            this.listBoxOtherDriverNames.TabIndex = 7;
            this.listBoxOtherDriverNames.SelectedIndexChanged += new System.EventHandler(this.listBoxOtherDriverNames_SelectedIndexChanged);
            this.listBoxOtherDriverNames.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listBoxOtherDriverNames_MouseDoubleClick);
            // 
            // labelOtherDriverName
            // 
            this.labelOtherDriverName.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelOtherDriverName.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.labelOtherDriverName, 2);
            this.labelOtherDriverName.Location = new System.Drawing.Point(340, 33);
            this.labelOtherDriverName.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelOtherDriverName.Name = "labelOtherDriverName";
            this.labelOtherDriverName.Size = new System.Drawing.Size(334, 33);
            this.labelOtherDriverName.TabIndex = 8;
            this.labelOtherDriverName.Text = "Other possible driver names";
            this.labelOtherDriverName.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // listBoxDriverNames
            // 
            this.listBoxDriverNames.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxDriverNames.Enabled = false;
            this.listBoxDriverNames.FormattingEnabled = true;
            this.listBoxDriverNames.ItemHeight = 20;
            this.listBoxDriverNames.Location = new System.Drawing.Point(15, 69);
            this.listBoxDriverNames.Margin = new System.Windows.Forms.Padding(15, 3, 15, 3);
            this.listBoxDriverNames.Name = "listBoxDriverNames";
            this.listBoxDriverNames.Size = new System.Drawing.Size(308, 204);
            this.listBoxDriverNames.TabIndex = 2;
            this.listBoxDriverNames.SelectedIndexChanged += new System.EventHandler(this.listBoxDriverNames_SelectedIndexChanged);
            this.listBoxDriverNames.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listBoxDriverNames_MouseDoubleClick);
            // 
            // buttonNameSelect
            // 
            this.buttonNameSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonNameSelect.Enabled = false;
            this.buttonNameSelect.Location = new System.Drawing.Point(353, 285);
            this.buttonNameSelect.Margin = new System.Windows.Forms.Padding(15, 3, 15, 3);
            this.buttonNameSelect.Name = "buttonNameSelect";
            this.buttonNameSelect.Size = new System.Drawing.Size(105, 45);
            this.buttonNameSelect.TabIndex = 6;
            this.buttonNameSelect.Text = "Select";
            this.buttonNameSelect.UseVisualStyleBackColor = true;
            this.buttonNameSelect.Click += new System.EventHandler(this.buttonNameSelect_Click);
            // 
            // labelOpponentName
            // 
            this.labelOpponentName.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelOpponentName.AutoSize = true;
            this.labelOpponentName.Location = new System.Drawing.Point(212, 6);
            this.labelOpponentName.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelOpponentName.Name = "labelOpponentName";
            this.labelOpponentName.Size = new System.Drawing.Size(124, 20);
            this.labelOpponentName.TabIndex = 9;
            this.labelOpponentName.Text = "Opponent name";
            // 
            // labelOpponentNameEntry
            // 
            this.labelOpponentNameEntry.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.labelOpponentNameEntry.AutoSize = true;
            this.labelOpponentNameEntry.Location = new System.Drawing.Point(340, 6);
            this.labelOpponentNameEntry.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelOpponentNameEntry.Name = "labelOpponentNameEntry";
            this.labelOpponentNameEntry.Size = new System.Drawing.Size(67, 20);
            this.labelOpponentNameEntry.TabIndex = 10;
            this.labelOpponentNameEntry.Text = "<name>";
            // 
            // buttonNoneOfTheAbove
            // 
            this.buttonNoneOfTheAbove.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonNoneOfTheAbove.Location = new System.Drawing.Point(488, 285);
            this.buttonNoneOfTheAbove.Margin = new System.Windows.Forms.Padding(15, 3, 15, 3);
            this.buttonNoneOfTheAbove.Name = "buttonNoneOfTheAbove";
            this.buttonNoneOfTheAbove.Size = new System.Drawing.Size(173, 45);
            this.buttonNoneOfTheAbove.TabIndex = 11;
            this.buttonNoneOfTheAbove.Text = "None of the above";
            this.buttonNoneOfTheAbove.UseVisualStyleBackColor = true;
            this.buttonNoneOfTheAbove.Click += new System.EventHandler(this.buttonNoneOfTheAbove_Click);
            // 
            // buttonPlayName
            // 
            this.buttonPlayName.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonPlayName.Enabled = false;
            this.buttonPlayName.Image = ((System.Drawing.Image)(resources.GetObject("buttonPlayName.Image")));
            this.buttonPlayName.Location = new System.Drawing.Point(15, 285);
            this.buttonPlayName.Margin = new System.Windows.Forms.Padding(15, 3, 15, 3);
            this.buttonPlayName.Name = "buttonPlayName";
            this.buttonPlayName.Size = new System.Drawing.Size(308, 45);
            this.buttonPlayName.TabIndex = 5;
            this.buttonPlayName.UseVisualStyleBackColor = true;
            this.buttonPlayName.Click += new System.EventHandler(this.buttonPlayName_Click);
            // 
            // OpponentNameSelection_V
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(711, 374);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "OpponentNameSelection_V";
            this.Text = "Opponent Name Selection";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.ListBox listBoxDriverNames;
        private System.Windows.Forms.Label labelDriverName;
        private System.Windows.Forms.Button buttonPlayName;
        private System.Windows.Forms.Button buttonNameSelect;
        private System.Windows.Forms.ListBox listBoxOtherDriverNames;
        private System.Windows.Forms.Label labelOtherDriverName;
        private System.Windows.Forms.Label labelOpponentName;
        private System.Windows.Forms.Label labelOpponentNameEntry;
        private System.Windows.Forms.Button buttonNoneOfTheAbove;
        private System.Windows.Forms.ToolTip toolTipNoneOfTheAbove;
    }
}