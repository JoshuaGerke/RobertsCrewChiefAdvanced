namespace CrewChiefV4.UserInterface
{
    partial class SendLogTraceDialog
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SendLogTraceDialog));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.buttonCopyCrewChiefEmail = new System.Windows.Forms.Button();
            this.textBoxCrewChiefEmail = new System.Windows.Forms.TextBox();
            this.labelCrewChiefEmail = new System.Windows.Forms.Label();
            this.labelLogTraceFilePath = new System.Windows.Forms.Label();
            this.buttonCopyLogTraceFilePath = new System.Windows.Forms.Button();
            this.textBoxLogTraceFilePath = new System.Windows.Forms.TextBox();
            this.labelInstructions = new System.Windows.Forms.Label();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.AutoSize = true;
            this.tableLayoutPanel1.ColumnCount = 3;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 39.3401F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60.6599F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 107F));
            this.tableLayoutPanel1.Controls.Add(this.buttonCopyCrewChiefEmail, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.textBoxCrewChiefEmail, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.labelCrewChiefEmail, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.labelLogTraceFilePath, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.buttonCopyLogTraceFilePath, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.textBoxLogTraceFilePath, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.labelInstructions, 0, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 3;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 91F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(519, 172);
            this.tableLayoutPanel1.TabIndex = 0;
            // 
            // buttonCopyCrewChiefEmail
            // 
            this.buttonCopyCrewChiefEmail.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonCopyCrewChiefEmail.Location = new System.Drawing.Point(420, 137);
            this.buttonCopyCrewChiefEmail.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonCopyCrewChiefEmail.Name = "buttonCopyCrewChiefEmail";
            this.buttonCopyCrewChiefEmail.Size = new System.Drawing.Size(89, 29);
            this.buttonCopyCrewChiefEmail.TabIndex = 6;
            this.buttonCopyCrewChiefEmail.Text = "Copy";
            this.buttonCopyCrewChiefEmail.UseVisualStyleBackColor = true;
            this.buttonCopyCrewChiefEmail.Click += new System.EventHandler(this.buttonCrewChiefEmail_Click);
            // 
            // textBoxCrewChiefEmail
            // 
            this.textBoxCrewChiefEmail.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxCrewChiefEmail.Location = new System.Drawing.Point(164, 141);
            this.textBoxCrewChiefEmail.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBoxCrewChiefEmail.Name = "textBoxCrewChiefEmail";
            this.textBoxCrewChiefEmail.ReadOnly = true;
            this.textBoxCrewChiefEmail.Size = new System.Drawing.Size(245, 20);
            this.textBoxCrewChiefEmail.TabIndex = 5;
            // 
            // labelCrewChiefEmail
            // 
            this.labelCrewChiefEmail.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelCrewChiefEmail.AutoSize = true;
            this.labelCrewChiefEmail.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelCrewChiefEmail.Location = new System.Drawing.Point(60, 144);
            this.labelCrewChiefEmail.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelCrewChiefEmail.Name = "labelCrewChiefEmail";
            this.labelCrewChiefEmail.Size = new System.Drawing.Size(100, 15);
            this.labelCrewChiefEmail.TabIndex = 4;
            this.labelCrewChiefEmail.Text = "Crew Chief email";
            // 
            // labelLogTraceFilePath
            // 
            this.labelLogTraceFilePath.Anchor = System.Windows.Forms.AnchorStyles.Right;
            this.labelLogTraceFilePath.AutoSize = true;
            this.labelLogTraceFilePath.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelLogTraceFilePath.Location = new System.Drawing.Point(56, 103);
            this.labelLogTraceFilePath.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelLogTraceFilePath.Name = "labelLogTraceFilePath";
            this.labelLogTraceFilePath.Size = new System.Drawing.Size(104, 15);
            this.labelLogTraceFilePath.TabIndex = 1;
            this.labelLogTraceFilePath.Text = "Log/trace file path";
            // 
            // buttonCopyLogTraceFilePath
            // 
            this.buttonCopyLogTraceFilePath.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.buttonCopyLogTraceFilePath.Location = new System.Drawing.Point(420, 97);
            this.buttonCopyLogTraceFilePath.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.buttonCopyLogTraceFilePath.Name = "buttonCopyLogTraceFilePath";
            this.buttonCopyLogTraceFilePath.Size = new System.Drawing.Size(89, 27);
            this.buttonCopyLogTraceFilePath.TabIndex = 3;
            this.buttonCopyLogTraceFilePath.Text = "Copy";
            this.buttonCopyLogTraceFilePath.UseVisualStyleBackColor = true;
            this.buttonCopyLogTraceFilePath.Click += new System.EventHandler(this.buttonCopyLogTraceFilePath_Click);
            // 
            // textBoxLogTraceFilePath
            // 
            this.textBoxLogTraceFilePath.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.textBoxLogTraceFilePath.Location = new System.Drawing.Point(164, 101);
            this.textBoxLogTraceFilePath.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.textBoxLogTraceFilePath.Name = "textBoxLogTraceFilePath";
            this.textBoxLogTraceFilePath.ReadOnly = true;
            this.textBoxLogTraceFilePath.Size = new System.Drawing.Size(245, 20);
            this.textBoxLogTraceFilePath.TabIndex = 2;
            // 
            // labelInstructions
            // 
            this.labelInstructions.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.labelInstructions, 3);
            this.labelInstructions.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelInstructions.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.labelInstructions.Location = new System.Drawing.Point(2, 0);
            this.labelInstructions.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.labelInstructions.Name = "labelInstructions";
            this.labelInstructions.Padding = new System.Windows.Forms.Padding(7, 6, 7, 6);
            this.labelInstructions.Size = new System.Drawing.Size(515, 91);
            this.labelInstructions.TabIndex = 7;
            this.labelInstructions.Text = "Instructions";
            // 
            // SendLogTraceDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(519, 172);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(2, 2, 2, 2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SendLogTraceDialog";
            this.Text = "Send Log/Trace";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Button buttonCopyCrewChiefEmail;
        private System.Windows.Forms.TextBox textBoxCrewChiefEmail;
        private System.Windows.Forms.Label labelCrewChiefEmail;
        private System.Windows.Forms.Label labelLogTraceFilePath;
        private System.Windows.Forms.Button buttonCopyLogTraceFilePath;
        private System.Windows.Forms.TextBox textBoxLogTraceFilePath;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Label labelInstructions;
    }
}