namespace CrewChiefV4
{
    partial class ActionEditor
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
            this.listBoxCurrentlyAvailableActions = new System.Windows.Forms.ListBox();
            this.listBoxAdditionalAvailableActions = new System.Windows.Forms.ListBox();
            this.buttonRemoveAction = new System.Windows.Forms.Button();
            this.buttonAddAction = new System.Windows.Forms.Button();
            this.buttonSave = new System.Windows.Forms.Button();
            this.exitButton = new System.Windows.Forms.Button();
            this.labelCurrentActions = new System.Windows.Forms.Label();
            this.labelAdditionalActions = new System.Windows.Forms.Label();
            this.buttonReset = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // listBoxAdditionalAvailableActions
            // 
            this.listBoxAdditionalAvailableActions.FormattingEnabled = true;
            this.listBoxAdditionalAvailableActions.Location = new System.Drawing.Point(8, 20);
            this.listBoxAdditionalAvailableActions.Name = "listBoxAdditionalAvailableActions";
            this.listBoxAdditionalAvailableActions.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBoxAdditionalAvailableActions.Size = new System.Drawing.Size(272, 134);
            this.listBoxAdditionalAvailableActions.TabIndex = 0;
            // 
            // buttonRemoveAction
            // 
            this.buttonRemoveAction.Location = new System.Drawing.Point(286, 95);
            this.buttonRemoveAction.Name = "buttonRemoveAction";
            this.buttonRemoveAction.Size = new System.Drawing.Size(45, 25);
            this.buttonRemoveAction.TabIndex = 3;
            this.buttonRemoveAction.Text = "<<";
            this.buttonRemoveAction.UseVisualStyleBackColor = true;
            this.buttonRemoveAction.Click += new System.EventHandler(this.buttonRemoveAction_Click);
            // 
            // buttonAddAction
            // 
            this.buttonAddAction.Location = new System.Drawing.Point(286, 54);
            this.buttonAddAction.Name = "buttonAddAction";
            this.buttonAddAction.Size = new System.Drawing.Size(45, 23);
            this.buttonAddAction.TabIndex = 2;
            this.buttonAddAction.Text = ">>";
            this.buttonAddAction.UseVisualStyleBackColor = true;
            this.buttonAddAction.Click += new System.EventHandler(this.buttonAddAction_Click);
            // 
            // listBoxCurrentlyAvailableActions
            // 
            this.listBoxCurrentlyAvailableActions.FormattingEnabled = true;
            this.listBoxCurrentlyAvailableActions.Location = new System.Drawing.Point(337, 20);
            this.listBoxCurrentlyAvailableActions.Name = "listBoxCurrentlyAvailableActions";
            this.listBoxCurrentlyAvailableActions.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.listBoxCurrentlyAvailableActions.Size = new System.Drawing.Size(272, 134);
            this.listBoxCurrentlyAvailableActions.TabIndex = 8;
            // 
            // buttonSave
            // 
            this.buttonSave.Location = new System.Drawing.Point(8, 161);
            this.buttonSave.Name = "buttonSave";
            this.buttonSave.Size = new System.Drawing.Size(133, 35);
            this.buttonSave.TabIndex = 15;
            this.buttonSave.Text = "save_and_restart";
            this.buttonSave.UseVisualStyleBackColor = true;
            this.buttonSave.Click += new System.EventHandler(this.buttonSave_Click);
            // 
            // exitButton
            // 
            this.exitButton.Location = new System.Drawing.Point(147, 161);
            this.exitButton.Name = "exitButton";
            this.exitButton.Size = new System.Drawing.Size(133, 35);
            this.exitButton.TabIndex = 10;
            this.exitButton.Text = "exit_without_saving";
            this.exitButton.UseVisualStyleBackColor = true;
            this.exitButton.Click += new System.EventHandler(this.exitButton_Click);
            // 
            // labelCurrentActions
            // 
            this.labelCurrentActions.AutoSize = true;
            this.labelCurrentActions.Location = new System.Drawing.Point(334, 4);
            this.labelCurrentActions.Name = "labelCurrentActions";
            this.labelCurrentActions.Size = new System.Drawing.Size(128, 13);
            this.labelCurrentActions.Text = "current_available_actions";
            // 
            // labelAdditionalActions
            // 
            this.labelAdditionalActions.AutoSize = true;
            this.labelAdditionalActions.Location = new System.Drawing.Point(5, 4);
            this.labelAdditionalActions.Name = "labelAdditionalActions";
            this.labelAdditionalActions.Size = new System.Drawing.Size(140, 13);
            this.labelAdditionalActions.Text = "additional_available_actions";
            // 
            // buttonReset
            // 
            this.buttonReset.Location = new System.Drawing.Point(476, 161);
            this.buttonReset.Name = "buttonReset";
            this.buttonReset.Size = new System.Drawing.Size(133, 35);
            this.buttonReset.TabIndex = 20;
            this.buttonReset.Text = "reload_actions";
            this.buttonReset.UseVisualStyleBackColor = true;
            this.buttonReset.Click += new System.EventHandler(this.buttonReset_Click);
            // 
            // ActionEditor
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(624, 208);
            this.Controls.Add(this.buttonReset);
            this.Controls.Add(this.labelAdditionalActions);
            this.Controls.Add(this.labelCurrentActions);
            this.Controls.Add(this.exitButton);
            this.Controls.Add(this.buttonSave);
            this.Controls.Add(this.buttonAddAction);
            this.Controls.Add(this.buttonRemoveAction);
            this.Controls.Add(this.listBoxAdditionalAvailableActions);
            this.Controls.Add(this.listBoxCurrentlyAvailableActions);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ActionEditor";
            this.Text = "Action Editor";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.ActionEditor_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox listBoxCurrentlyAvailableActions;
        private System.Windows.Forms.ListBox listBoxAdditionalAvailableActions;
        private System.Windows.Forms.Button buttonRemoveAction;
        private System.Windows.Forms.Button buttonAddAction;
        private System.Windows.Forms.Button buttonSave;
        private System.Windows.Forms.Button exitButton;
        private System.Windows.Forms.Label labelCurrentActions;
        private System.Windows.Forms.Label labelAdditionalActions;
        private System.Windows.Forms.Button buttonReset;
    }
}