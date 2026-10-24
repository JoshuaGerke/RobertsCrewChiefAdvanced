namespace CrewChiefV4.UserInterface
{
    partial class MyName_V
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MyName_V));
            this.labelEnterYourName = new System.Windows.Forms.Label();
            this.textBoxMyName = new System.Windows.Forms.TextBox();
            this.listBoxPersonalisations = new System.Windows.Forms.ListBox();
            this.listBoxDriverNames = new System.Windows.Forms.ListBox();
            this.labelFullPersonalisation = new System.Windows.Forms.Label();
            this.labelDriverName = new System.Windows.Forms.Label();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.buttonPlayName = new System.Windows.Forms.Button();
            this.buttonNameSelect = new System.Windows.Forms.Button();
            this.listBoxOtherDriverNames = new System.Windows.Forms.ListBox();
            this.labelOtherDriverName = new System.Windows.Forms.Label();
            this.buttonNoName = new System.Windows.Forms.Button();
            this.buttonSearch = new System.Windows.Forms.Button();
            this.toolTipMyName = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // labelEnterYourName
            // 
            this.labelEnterYourName.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelEnterYourName.AutoSize = true;
            this.labelEnterYourName.Location = new System.Drawing.Point(4, 0);
            this.labelEnterYourName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelEnterYourName.Name = "labelEnterYourName";
            this.labelEnterYourName.Size = new System.Drawing.Size(398, 52);
            this.labelEnterYourName.TabIndex = 0;
            this.labelEnterYourName.Text = "enter_your_name";
            this.labelEnterYourName.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // textBoxMyName
            // 
            this.textBoxMyName.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxMyName.Location = new System.Drawing.Point(410, 4);
            this.textBoxMyName.Margin = new System.Windows.Forms.Padding(4);
            this.textBoxMyName.MaxLength = 32;
            this.textBoxMyName.Name = "textBoxMyName";
            this.textBoxMyName.Size = new System.Drawing.Size(398, 31);
            this.textBoxMyName.TabIndex = 1;
            this.textBoxMyName.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBoxMyName_KeyDown);
            // 
            // listBoxPersonalisations
            // 
            this.listBoxPersonalisations.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxPersonalisations.Enabled = false;
            this.listBoxPersonalisations.FormattingEnabled = true;
            this.listBoxPersonalisations.ItemHeight = 25;
            this.listBoxPersonalisations.Location = new System.Drawing.Point(20, 88);
            this.listBoxPersonalisations.Margin = new System.Windows.Forms.Padding(20, 4, 20, 4);
            this.listBoxPersonalisations.Name = "listBoxPersonalisations";
            this.listBoxPersonalisations.Size = new System.Drawing.Size(366, 254);
            this.listBoxPersonalisations.TabIndex = 1;
            this.listBoxPersonalisations.SelectedIndexChanged += new System.EventHandler(this.listBoxPersonalisations_SelectedIndexChanged);
            this.listBoxPersonalisations.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listBoxPersonalisations_MouseDoubleClick);
            // 
            // listBoxDriverNames
            // 
            this.listBoxDriverNames.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxDriverNames.Enabled = false;
            this.listBoxDriverNames.FormattingEnabled = true;
            this.listBoxDriverNames.ItemHeight = 25;
            this.listBoxDriverNames.Location = new System.Drawing.Point(426, 88);
            this.listBoxDriverNames.Margin = new System.Windows.Forms.Padding(20, 4, 20, 4);
            this.listBoxDriverNames.Name = "listBoxDriverNames";
            this.listBoxDriverNames.Size = new System.Drawing.Size(366, 254);
            this.listBoxDriverNames.TabIndex = 2;
            this.listBoxDriverNames.SelectedIndexChanged += new System.EventHandler(this.listBoxDriverNames_SelectedIndexChanged);
            this.listBoxDriverNames.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.listBoxDriverNames_MouseDoubleClick);
            // 
            // labelFullPersonalisation
            // 
            this.labelFullPersonalisation.AutoSize = true;
            this.labelFullPersonalisation.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.labelFullPersonalisation.Location = new System.Drawing.Point(4, 59);
            this.labelFullPersonalisation.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelFullPersonalisation.Name = "labelFullPersonalisation";
            this.labelFullPersonalisation.Size = new System.Drawing.Size(398, 25);
            this.labelFullPersonalisation.TabIndex = 3;
            this.labelFullPersonalisation.Text = "full_personalisation";
            this.labelFullPersonalisation.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            // 
            // labelDriverName
            // 
            this.labelDriverName.AutoSize = true;
            this.labelDriverName.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.labelDriverName.Location = new System.Drawing.Point(410, 59);
            this.labelDriverName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDriverName.Name = "labelDriverName";
            this.labelDriverName.Size = new System.Drawing.Size(398, 25);
            this.labelDriverName.TabIndex = 4;
            this.labelDriverName.Text = "driver_name";
            this.labelDriverName.TextAlign = System.Drawing.ContentAlignment.BottomCenter;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33333F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 33.33334F));
            this.tableLayoutPanel1.Controls.Add(this.listBoxDriverNames, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.labelDriverName, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.listBoxPersonalisations, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.textBoxMyName, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.labelEnterYourName, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.labelFullPersonalisation, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.buttonPlayName, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.buttonNameSelect, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.listBoxOtherDriverNames, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.labelOtherDriverName, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.buttonNoName, 2, 3);
            this.tableLayoutPanel1.Controls.Add(this.buttonSearch, 2, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(12, 35);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(4);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 61.38119F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 38.61881F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 285F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 46F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(1220, 416);
            this.tableLayoutPanel1.TabIndex = 1;
            // 
            // buttonPlayName
            // 
            this.buttonPlayName.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonPlayName.Enabled = false;
            this.buttonPlayName.Image = ((System.Drawing.Image)(resources.GetObject("buttonPlayName.Image")));
            this.buttonPlayName.Location = new System.Drawing.Point(20, 373);
            this.buttonPlayName.Margin = new System.Windows.Forms.Padding(20, 4, 20, 4);
            this.buttonPlayName.Name = "buttonPlayName";
            this.buttonPlayName.Size = new System.Drawing.Size(366, 39);
            this.buttonPlayName.TabIndex = 5;
            this.buttonPlayName.UseVisualStyleBackColor = true;
            this.buttonPlayName.Click += new System.EventHandler(this.buttonPlayName_Click);
            // 
            // buttonNameSelect
            // 
            this.buttonNameSelect.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonNameSelect.Enabled = false;
            this.buttonNameSelect.Location = new System.Drawing.Point(426, 373);
            this.buttonNameSelect.Margin = new System.Windows.Forms.Padding(20, 4, 20, 4);
            this.buttonNameSelect.Name = "buttonNameSelect";
            this.buttonNameSelect.Size = new System.Drawing.Size(366, 39);
            this.buttonNameSelect.TabIndex = 6;
            this.buttonNameSelect.Text = "_select";
            this.buttonNameSelect.UseVisualStyleBackColor = true;
            this.buttonNameSelect.Click += new System.EventHandler(this.buttonNameSelect_Click);
            // 
            // listBoxOtherDriverNames
            // 
            this.listBoxOtherDriverNames.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listBoxOtherDriverNames.Enabled = false;
            this.listBoxOtherDriverNames.FormattingEnabled = true;
            this.listBoxOtherDriverNames.ItemHeight = 25;
            this.listBoxOtherDriverNames.Location = new System.Drawing.Point(832, 88);
            this.listBoxOtherDriverNames.Margin = new System.Windows.Forms.Padding(20, 4, 20, 4);
            this.listBoxOtherDriverNames.Name = "listBoxOtherDriverNames";
            this.listBoxOtherDriverNames.Size = new System.Drawing.Size(368, 254);
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
            this.labelOtherDriverName.Location = new System.Drawing.Point(815, 52);
            this.labelOtherDriverName.Name = "labelOtherDriverName";
            this.labelOtherDriverName.Size = new System.Drawing.Size(402, 32);
            this.labelOtherDriverName.TabIndex = 8;
            this.labelOtherDriverName.Text = "other_possible_driver_names";
            this.labelOtherDriverName.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // buttonNoName
            // 
            this.buttonNoName.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonNoName.Location = new System.Drawing.Point(815, 372);
            this.buttonNoName.Name = "buttonNoName";
            this.buttonNoName.Size = new System.Drawing.Size(402, 41);
            this.buttonNoName.TabIndex = 9;
            this.buttonNoName.UseVisualStyleBackColor = true;
            this.buttonNoName.Click += new System.EventHandler(this.buttonNoName_Click);
            // 
            // buttonSearch
            // 
            this.buttonSearch.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.buttonSearch.Image = ((System.Drawing.Image)(resources.GetObject("buttonSearch.Image")));
            this.buttonSearch.Location = new System.Drawing.Point(815, 3);
            this.buttonSearch.Name = "buttonSearch";
            this.buttonSearch.Size = new System.Drawing.Size(41, 46);
            this.buttonSearch.TabIndex = 10;
            this.buttonSearch.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.buttonSearch.UseVisualStyleBackColor = true;
            this.buttonSearch.Click += new System.EventHandler(this.buttonSearch_Click);
            // 
            // MyName_V
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 25F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1250, 490);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "MyName_V";
            this.Text = "selecting_a_name";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.MyName_V_KeyDown);
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label labelEnterYourName;
        private System.Windows.Forms.TextBox textBoxMyName;
        private System.Windows.Forms.ListBox listBoxPersonalisations;
        private System.Windows.Forms.ListBox listBoxDriverNames;
        private System.Windows.Forms.Label labelFullPersonalisation;
        private System.Windows.Forms.Label labelDriverName;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Button buttonPlayName;
        private System.Windows.Forms.Button buttonNameSelect;
        private System.Windows.Forms.ListBox listBoxOtherDriverNames;
        private System.Windows.Forms.Label labelOtherDriverName;
        private System.Windows.Forms.Button buttonNoName;
        private System.Windows.Forms.ToolTip toolTipMyName;
        private System.Windows.Forms.Button buttonSearch;
    }
}