namespace CrewChiefV4.UserInterface.TopicWindows
{
    partial class TopicWindowFuel
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (refreshTimer != null) refreshTimer.Dispose();
                if (components != null) components.Dispose();
                if (object.ReferenceEquals(_instance, this)) { _instance = null; JustOpened = false; }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(TopicWindowFuel));
            this.layout = new System.Windows.Forms.TableLayoutPanel();
            this.labelFuelUseDisabled = new System.Windows.Forms.Label();
            this.labelFuelUseDisabled.Text = "Fuel consumption turned off in game";
            this.labelFuelUseDisabled.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelFuelUseDisabled.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.labelFuelUseDisabled.Visible = false;
            this.groupBoxFuel = new System.Windows.Forms.GroupBox();
            this.groupBoxVirtualEnergy = new System.Windows.Forms.GroupBox();
            this.fuelLevelBar = new FuelLevelBar();
            this.virtualEnergyLevelBar = new FuelLevelBar();
            this.textBoxTimeLeft = new System.Windows.Forms.TextBox();
            this.textBoxFuelLevel = new System.Windows.Forms.TextBox();
            this.textBoxFuelPerLap = new System.Windows.Forms.TextBox();
            this.textBoxFuelPerMinute = new System.Windows.Forms.TextBox();
            this.textBoxFuelCapacity = new System.Windows.Forms.TextBox();
            this.textBoxFuelLaps = new System.Windows.Forms.TextBox();
            this.textBoxVirtualEnergyLevel = new System.Windows.Forms.TextBox();
            this.textBoxVirtualEnergyPerLap = new System.Windows.Forms.TextBox();
            this.textBoxVirtualEnergyPerMinute = new System.Windows.Forms.TextBox();
            this.textBoxFuelRatio = new System.Windows.Forms.TextBox();
            this.textBoxVirtualEnergyLaps = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            this.layout.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layout.Padding = new System.Windows.Forms.Padding(8);
            this.layout.ColumnCount = 1;
            this.layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.layout.RowCount = 3;
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 50F));
            this.layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            this.groupBoxFuel.Text = "Fuel";
            this.groupBoxVirtualEnergy.Text = "Virtual energy";
            this.groupBoxVirtualEnergy.Visible = false;
            this.groupBoxFuel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupBoxVirtualEnergy.Dock = System.Windows.Forms.DockStyle.Fill;
            this.layout.Controls.Add(this.groupBoxFuel, 0, 0);
            this.layout.Controls.Add(this.groupBoxVirtualEnergy, 0, 1);
            this.groupBoxFuel.Controls.Add(CreateResourceLayout(fuelLevelBar,
                textBoxFuelLevel, textBoxFuelPerLap, textBoxFuelPerMinute,
                textBoxFuelCapacity, textBoxFuelLaps, "Capacity (L)", textBoxTimeLeft));
            this.groupBoxVirtualEnergy.Controls.Add(CreateResourceLayout(virtualEnergyLevelBar,
                textBoxVirtualEnergyLevel, textBoxVirtualEnergyPerLap, textBoxVirtualEnergyPerMinute,
                textBoxFuelRatio, textBoxVirtualEnergyLaps, "Fuel ratio"));
            this.layout.Controls.Add(new System.Windows.Forms.Label {
                Text = "Ticks: level after further laps. Orange marker: pit / finish. Red: insufficient.",
                AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft }, 0, 2);
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(650, 500);
            this.MinimumSize = new System.Drawing.Size(610, 530);
            this.Controls.Add(this.layout);
            this.Controls.Add(this.labelFuelUseDisabled);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "TopicWindowFuel";
            this.Text = "Fuel and virtual energy";
            this.ResumeLayout(false);
        }
        #endregion

        private System.Windows.Forms.Label labelFuelUseDisabled;
        private System.Windows.Forms.TableLayoutPanel layout;
        private System.Windows.Forms.GroupBox groupBoxFuel;
        private System.Windows.Forms.GroupBox groupBoxVirtualEnergy;
        private FuelLevelBar fuelLevelBar;
        private FuelLevelBar virtualEnergyLevelBar;
        private System.Windows.Forms.TextBox textBoxTimeLeft;
        private System.Windows.Forms.TextBox textBoxFuelLevel;
        private System.Windows.Forms.TextBox textBoxFuelPerLap;
        private System.Windows.Forms.TextBox textBoxFuelPerMinute;
        private System.Windows.Forms.TextBox textBoxFuelCapacity;
        private System.Windows.Forms.TextBox textBoxFuelLaps;
        private System.Windows.Forms.TextBox textBoxVirtualEnergyLevel;
        private System.Windows.Forms.TextBox textBoxVirtualEnergyPerLap;
        private System.Windows.Forms.TextBox textBoxVirtualEnergyPerMinute;
        private System.Windows.Forms.TextBox textBoxFuelRatio;
        private System.Windows.Forms.TextBox textBoxVirtualEnergyLaps;
    }
}
