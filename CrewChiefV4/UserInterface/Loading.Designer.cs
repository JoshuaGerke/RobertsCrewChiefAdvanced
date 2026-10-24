using System;

namespace CrewChiefV4.UserInterface
{
    partial class Loading
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
            this.pictureBox = new System.Windows.Forms.PictureBox();
            this.label = new System.Windows.Forms.Label();
            this.SuspendLayout();
            this.pictureBox.Size = new System.Drawing.Size(400, 400);
            this.ClientSize = new System.Drawing.Size(400, 400);
            try
            {
                this.pictureBox.Load(Loading.splashImagePath);
                this.pictureBox.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
                this.Controls.Add(this.pictureBox);
            }
            catch (Exception)
            {
                this.label.AutoSize = true;
                this.label.Font = new System.Drawing.Font("Comic Sans MS", 16F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Italic))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
                this.label.Location = new System.Drawing.Point(67, 106);
                this.label.Size = new System.Drawing.Size(458, 110);
                this.label.TabIndex = 0;
                this.label.Text = "Launching Crew Chief...";
                // 
                // Loading
                // 
                this.AutoScaleDimensions = new System.Drawing.SizeF(11F, 24F);
                this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
                this.ClientSize = new System.Drawing.Size(800, 450);
                this.Controls.Add(this.label);

            }
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox;
        private System.Windows.Forms.Label label;
    }
}