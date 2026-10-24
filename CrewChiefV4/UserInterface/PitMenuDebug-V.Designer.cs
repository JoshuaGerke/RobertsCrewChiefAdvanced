#if PIT_MANAGER_DEBUG
namespace CrewChiefV4.UserInterface
{
    partial class PitMenuDebug_V
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PitMenuDebug_V));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.richTextBox1 = new System.Windows.Forms.RichTextBox();
            this.checkedListBoxMenuCategories = new System.Windows.Forms.CheckedListBox();
            this.listBoxMenuValues = new System.Windows.Forms.ListBox();
            this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
            this.comboBoxMFDs = new System.Windows.Forms.ComboBox();
            this.textBoxFuel = new System.Windows.Forms.TextBox();
            this.labelFuelEntry = new System.Windows.Forms.Label();
            this.tableLayoutPanel3 = new System.Windows.Forms.TableLayoutPanel();
            this.buttonDown = new System.Windows.Forms.Button();
            this.buttonRight = new System.Windows.Forms.Button();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonLeft = new System.Windows.Forms.Button();
            this.buttonUp = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.tableLayoutPanel2.SuspendLayout();
            this.tableLayoutPanel3.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 46.166F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 24.93702F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 28.89697F));
            this.tableLayoutPanel1.Controls.Add(this.richTextBox1, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.checkedListBoxMenuCategories, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.listBoxMenuValues, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tableLayoutPanel3, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 200F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 645F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(934, 1049);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // richTextBox1
            // 
            this.tableLayoutPanel1.SetColumnSpan(this.richTextBox1, 3);
            this.richTextBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.richTextBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.1F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.richTextBox1.Location = new System.Drawing.Point(3, 407);
            this.richTextBox1.Name = "richTextBox1";
            this.richTextBox1.ReadOnly = true;
            this.richTextBox1.Size = new System.Drawing.Size(928, 639);
            this.richTextBox1.TabIndex = 0;
            this.richTextBox1.Text = resources.GetString("richTextBox1.Text");
            this.richTextBox1.WordWrap = false;
            // 
            // checkedListBoxMenuCategories
            // 
            this.checkedListBoxMenuCategories.Font = new System.Drawing.Font("Courier New", 10.125F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.checkedListBoxMenuCategories.Items.AddRange(new object[] {
            "FUEL:",
            "R TIRES:",
            "F TIRES:",
            "RT TIRES:",
            "LF TIRES:",
            "FR TIRE:",
            "FL TIRE:",
            "RR TIRE:",
            "RL TIRE:",
            "FR PRESS:",
            "FL PRESS:",
            "RR PRESS:",
            "RL PRESS:",
            "etc."});
            this.checkedListBoxMenuCategories.Location = new System.Drawing.Point(434, 3);
            this.checkedListBoxMenuCategories.Name = "checkedListBoxMenuCategories";
            this.tableLayoutPanel1.SetRowSpan(this.checkedListBoxMenuCategories, 2);
            this.checkedListBoxMenuCategories.Size = new System.Drawing.Size(226, 382);
            this.checkedListBoxMenuCategories.TabIndex = 1;
            this.checkedListBoxMenuCategories.SelectedIndexChanged += new System.EventHandler(this.checkedListBoxMenuCategories_SelectedIndexChanged);
            // 
            // listBoxMenuValues
            // 
            this.listBoxMenuValues.Dock = System.Windows.Forms.DockStyle.Fill;
            this.listBoxMenuValues.Font = new System.Drawing.Font("Courier New", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.listBoxMenuValues.FormattingEnabled = true;
            this.listBoxMenuValues.ItemHeight = 27;
            this.listBoxMenuValues.Items.AddRange(new object[] {
            "25",
            "SOFT",
            "SOFT",
            "SOFT",
            "SOFT",
            "etc."});
            this.listBoxMenuValues.Location = new System.Drawing.Point(666, 3);
            this.listBoxMenuValues.MinimumSize = new System.Drawing.Size(151, 401);
            this.listBoxMenuValues.Name = "listBoxMenuValues";
            this.tableLayoutPanel1.SetRowSpan(this.listBoxMenuValues, 2);
            this.listBoxMenuValues.Size = new System.Drawing.Size(265, 401);
            this.listBoxMenuValues.TabIndex = 2;
            // 
            // tableLayoutPanel2
            // 
            this.tableLayoutPanel2.ColumnCount = 2;
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 164F));
            this.tableLayoutPanel2.Controls.Add(this.comboBoxMFDs, 0, 1);
            this.tableLayoutPanel2.Controls.Add(this.textBoxFuel, 1, 0);
            this.tableLayoutPanel2.Controls.Add(this.labelFuelEntry, 0, 0);
            this.tableLayoutPanel2.Location = new System.Drawing.Point(3, 203);
            this.tableLayoutPanel2.Name = "tableLayoutPanel2";
            this.tableLayoutPanel2.RowCount = 3;
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel2.Size = new System.Drawing.Size(424, 192);
            this.tableLayoutPanel2.TabIndex = 4;
            // 
            // comboBoxMFDs
            // 
            this.tableLayoutPanel2.SetColumnSpan(this.comboBoxMFDs, 2);
            this.comboBoxMFDs.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.comboBoxMFDs.FormattingEnabled = true;
            this.comboBoxMFDs.Items.AddRange(new object[] {
            "MFDA Standard (Sectors etc.)",
            "MFDB Pit Menu",
            "MFDC Vehicle Status (Tyres etc.)",
            "MFDD Driving Aids",
            "MFDE Extra Info (RPM, temps)",
            "MFDF Race Info (Clock, leader etc.)",
            "MFDG Standings (Race position)",
            "MFDH Penalties"});
            this.comboBoxMFDs.Location = new System.Drawing.Point(30, 110);
            this.comboBoxMFDs.Margin = new System.Windows.Forms.Padding(30, 24, 30, 24);
            this.comboBoxMFDs.Name = "comboBoxMFDs";
            this.comboBoxMFDs.Size = new System.Drawing.Size(364, 30);
            this.comboBoxMFDs.TabIndex = 4;
            this.comboBoxMFDs.SelectedIndexChanged += new System.EventHandler(this.comboBoxMFDs_SelectedIndexChanged);
            // 
            // textBoxFuel
            // 
            this.textBoxFuel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxFuel.Location = new System.Drawing.Point(263, 30);
            this.textBoxFuel.Name = "textBoxFuel";
            this.textBoxFuel.Size = new System.Drawing.Size(158, 26);
            this.textBoxFuel.TabIndex = 5;
            this.textBoxFuel.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.textBoxFuel_KeyPress);
            // 
            // labelFuelEntry
            // 
            this.labelFuelEntry.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelFuelEntry.AutoSize = true;
            this.labelFuelEntry.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelFuelEntry.Location = new System.Drawing.Point(118, 30);
            this.labelFuelEntry.Name = "labelFuelEntry";
            this.labelFuelEntry.Size = new System.Drawing.Size(139, 25);
            this.labelFuelEntry.TabIndex = 6;
            this.labelFuelEntry.Text = "Enter fuel level";
            // 
            // tableLayoutPanel3
            // 
            this.tableLayoutPanel3.ColumnCount = 3;
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel3.Controls.Add(this.buttonDown, 1, 2);
            this.tableLayoutPanel3.Controls.Add(this.buttonRight, 2, 1);
            this.tableLayoutPanel3.Controls.Add(this.buttonRefresh, 1, 1);
            this.tableLayoutPanel3.Controls.Add(this.buttonLeft, 0, 1);
            this.tableLayoutPanel3.Controls.Add(this.buttonUp, 1, 0);
            this.tableLayoutPanel3.Location = new System.Drawing.Point(3, 3);
            this.tableLayoutPanel3.Name = "tableLayoutPanel3";
            this.tableLayoutPanel3.RowCount = 3;
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel3.Size = new System.Drawing.Size(424, 194);
            this.tableLayoutPanel3.TabIndex = 5;
            // 
            // buttonDown
            // 
            this.buttonDown.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonDown.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.875F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonDown.Location = new System.Drawing.Point(144, 131);
            this.buttonDown.Name = "buttonDown";
            this.buttonDown.Size = new System.Drawing.Size(135, 60);
            this.buttonDown.TabIndex = 7;
            this.buttonDown.Text = "⇓";
            this.buttonDown.UseVisualStyleBackColor = true;
            this.buttonDown.Click += new System.EventHandler(this.buttonDown_Click);
            // 
            // buttonRight
            // 
            this.buttonRight.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonRight.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.875F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonRight.Location = new System.Drawing.Point(285, 67);
            this.buttonRight.Name = "buttonRight";
            this.buttonRight.Size = new System.Drawing.Size(136, 58);
            this.buttonRight.TabIndex = 5;
            this.buttonRight.Text = "⇒";
            this.buttonRight.UseVisualStyleBackColor = true;
            this.buttonRight.Click += new System.EventHandler(this.buttonRight_Click);
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.875F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonRefresh.Location = new System.Drawing.Point(143, 66);
            this.buttonRefresh.Margin = new System.Windows.Forms.Padding(2);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(136, 59);
            this.buttonRefresh.TabIndex = 5;
            this.buttonRefresh.Text = "Refresh";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // buttonLeft
            // 
            this.buttonLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonLeft.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.875F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonLeft.Location = new System.Drawing.Point(3, 67);
            this.buttonLeft.Name = "buttonLeft";
            this.buttonLeft.Size = new System.Drawing.Size(135, 58);
            this.buttonLeft.TabIndex = 3;
            this.buttonLeft.Text = "⇐ ";
            this.buttonLeft.UseVisualStyleBackColor = true;
            this.buttonLeft.Click += new System.EventHandler(this.buttonLeft_Click);
            // 
            // buttonUp
            // 
            this.buttonUp.Dock = System.Windows.Forms.DockStyle.Fill;
            this.buttonUp.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.875F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonUp.Location = new System.Drawing.Point(144, 3);
            this.buttonUp.Name = "buttonUp";
            this.buttonUp.Size = new System.Drawing.Size(135, 58);
            this.buttonUp.TabIndex = 0;
            this.buttonUp.Text = "⇑ ";
            this.buttonUp.UseVisualStyleBackColor = true;
            this.buttonUp.Click += new System.EventHandler(this.buttonUp_Click);
            // 
            // PitMenuDebug_V
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(934, 1049);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "PitMenuDebug_V";
            this.Text = "PitMenuDebug_V";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel2.ResumeLayout(false);
            this.tableLayoutPanel2.PerformLayout();
            this.tableLayoutPanel3.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel3;
        private System.Windows.Forms.Button buttonDown;
        private System.Windows.Forms.Button buttonRight;
        private System.Windows.Forms.Button buttonLeft;
        private System.Windows.Forms.Button buttonUp;
        public System.Windows.Forms.RichTextBox richTextBox1;
        public System.Windows.Forms.CheckedListBox checkedListBoxMenuCategories;
        public System.Windows.Forms.ListBox listBoxMenuValues;
        public System.Windows.Forms.ComboBox comboBoxMFDs;
        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.TextBox textBoxFuel;
        public System.Windows.Forms.Label labelFuelEntry;
    }
}
#endif