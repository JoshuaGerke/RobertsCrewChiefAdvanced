using System;
using System.Drawing;
using System.Windows.Forms;

namespace CrewChiefV4.UserInterface.TopicWindows
{
    public partial class TopicWindowFuel : Form
    {
        private static TopicWindowFuel _instance;
        private readonly Timer refreshTimer;
        private bool? displayedVirtualEnergy;
        private static readonly object dataLock = new object();
        private static ResourceData fuel = new ResourceData();
        private static ResourceData virtualEnergy = new ResourceData();
        private static bool virtualEnergyAvailable;
        private static bool virtualEnergySeen;
        private static float timeLeftSeconds = float.NaN;
        private static bool fuelUseActive = true;
        public static bool JustOpened { get; private set; }

        private sealed class ResourceData
        {
            public float Level = float.NaN;
            public float PerLap = float.NaN;
            public float PerMinute = float.NaN;
            public int CapacityOrRatio;
            public float? TargetLaps;
            public bool TargetIsRaceFinish;
        }

        public TopicWindowFuel()
        {
            InitializeComponent();
            _instance = this;
            JustOpened = true;
            // A WinForms timer renders the latest snapshot on the UI thread without queuing telemetry callbacks.
            refreshTimer = new Timer { Interval = 200 };
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
            RenderData();
        }

        /// <summary>Supply litres and consumption estimates. Null target means no pit/finish estimate.</summary>
        public static void SetFuelData(float level, int capacity, float litresPerLap,
            float litresPerMinute, float? lapsToTarget = null, bool targetIsRaceFinish = false)
        {
            lock (dataLock)
            {
                fuel = new ResourceData { Level = level, CapacityOrRatio = capacity,
                    PerLap = litresPerLap, PerMinute = litresPerMinute,
                    TargetLaps = lapsToTarget, TargetIsRaceFinish = targetIsRaceFinish };
            }
        }

        /// <summary>VE values are percentages from 0 to 100; consumption is percentage points per lap/minute.</summary>
        public static void SetVirtualEnergyData(bool available, float level, int fuelRatio,
            float percentPerLap, float percentPerMinute, float? lapsToTarget = null,
            bool targetIsRaceFinish = false)
        {
            lock (dataLock)
            {
                if (available && level > 0 && level <= 100)
                {
                    virtualEnergySeen = true;
                }
                virtualEnergyAvailable = available && virtualEnergySeen;
                virtualEnergy = new ResourceData { Level = level, CapacityOrRatio = fuelRatio,
                    PerLap = percentPerLap, PerMinute = percentPerMinute,
                    TargetLaps = lapsToTarget, TargetIsRaceFinish = targetIsRaceFinish };
            }
        }

        public static void SetFuelUseActive(bool active)
        {
            lock (dataLock)
            {
                fuelUseActive = active;
            }
        }

        public static void SetTimeLeft(float seconds)
        {
            lock (dataLock)
            {
                timeLeftSeconds = seconds;
            }
        }

        /// <summary>Call at a new session or car change so old estimates cannot carry over.</summary>
        public static void ResetData()
        {
            lock (dataLock)
            {
                fuel = new ResourceData();
                virtualEnergy = new ResourceData();
                virtualEnergyAvailable = false;
                virtualEnergySeen = false;
                timeLeftSeconds = float.NaN;
                fuelUseActive = true;
            }
        }

        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            if (Visible) RenderData();
        }

        private void RenderData()
        {
            ResourceData fuelSnapshot;
            ResourceData energySnapshot;
            bool available;
            float seconds;
            bool active;
            lock (dataLock)
            {
                fuelSnapshot = fuel;
                energySnapshot = virtualEnergy;
                available = virtualEnergyAvailable;
                seconds = timeLeftSeconds;
                active = fuelUseActive;
            }
            layout.Visible = active;
            labelFuelUseDisabled.Visible = !active;
            if (!active)
            {
                Text = "Fuel (WIP)";
                return;
            }
            Text = available ? "Fuel and virtual energy (WIP)" : "Fuel (WIP)";
            if (FuelLevelBar.IsKnown(seconds) && seconds < int.MaxValue)
            {
                TimeSpan time = TimeSpan.FromSeconds(Math.Floor(seconds));
                textBoxTimeLeft.Text = ((int)time.TotalHours).ToString("00") + ":" +
                    time.Minutes.ToString("00") + ":" + time.Seconds.ToString("00");
            }
            else
            {
                textBoxTimeLeft.Text = "—";
            }
            // Reapply font-based sizes after WinForms has completed automatic DPI scaling.
            foreach (GroupBox group in new[] { groupBoxFuel, groupBoxVirtualEnergy })
            {
                TableLayoutPanel panel = group.Controls[0] as TableLayoutPanel;
                if (panel != null)
                {
                    SizeResourceRows(panel);
                    foreach (Control control in panel.Controls)
                    {
                        TextBox box = control as TextBox;
                        string exemplar = box?.Tag as string;
                        if (exemplar != null)
                        {
                            box.Width = TextRenderer.MeasureText(exemplar, box.Font).Width + 24;
                        }
                    }
                }
            }
            RenderResource(fuelSnapshot, fuelLevelBar, textBoxFuelLevel, textBoxFuelPerLap,
                textBoxFuelPerMinute, textBoxFuelCapacity, textBoxFuelLaps, " L", true);
            if (displayedVirtualEnergy != available)
            {
                displayedVirtualEnergy = available;
                Text = available ? "Fuel and virtual energy (WIP)" : "Fuel (WIP)";
                layout.SuspendLayout();
                groupBoxVirtualEnergy.Visible = available;
                // Allow for the fonts actually inherited by the dynamically-created controls.
                int resourceFontHeight = Math.Max(Font.Height,
                    Math.Max(fuelLevelBar.Font.Height, virtualEnergyLevelBar.Font.Height));
                int resourceHeight = 10 * resourceFontHeight + 140;
                layout.RowStyles[0].SizeType = SizeType.Absolute;
                layout.RowStyles[0].Height = resourceHeight;
                layout.RowStyles[1].SizeType = SizeType.Absolute;
                layout.RowStyles[1].Height = available ? resourceHeight : 0;
                layout.RowStyles[2].Height = 2 * Font.Height + 8;
                int contentHeight = resourceHeight * (available ? 2 : 1) +
                    (int)layout.RowStyles[2].Height + layout.Padding.Vertical;
                MinimumSize = new Size(MinimumSize.Width, contentHeight + Height - ClientSize.Height);
                ClientSize = new Size(ClientSize.Width, contentHeight);
                layout.ResumeLayout(true);
            }
            RenderResource(available ? energySnapshot : new ResourceData(), virtualEnergyLevelBar,
                textBoxVirtualEnergyLevel, textBoxVirtualEnergyPerLap, textBoxVirtualEnergyPerMinute,
                textBoxFuelRatio, textBoxVirtualEnergyLaps, "%", false);
            JustOpened = false;
        }

        private static void RenderResource(ResourceData data, FuelLevelBar bar, TextBox level,
            TextBox perLap, TextBox perMinute, TextBox capacityOrRatio, TextBox laps,
            string unit, bool isFuel)
        {
            level.Text = Format(data.Level, unit);
            perLap.Text = Format(data.PerLap, unit + "/lap");
            perMinute.Text = Format(data.PerMinute, unit + "/min");
            capacityOrRatio.Text = data.CapacityOrRatio > 0 ? data.CapacityOrRatio.ToString() : "—";
            laps.Text = data.TargetLaps.HasValue && FuelLevelBar.IsKnown(data.TargetLaps.Value)
                ? data.TargetLaps.Value.ToString("0.#") + (data.TargetIsRaceFinish ? " to finish" : " to pit") : "—";
            bar.Level = data.Level;
            bar.Capacity = isFuel ? data.CapacityOrRatio : 100;
            bar.ConsumptionPerLap = data.PerLap;
            bar.TargetLaps = data.TargetLaps;
            bar.TargetName = data.TargetIsRaceFinish ? "Finish" : "Pit";
            bool shortOfTarget = data.TargetLaps.HasValue && FuelLevelBar.IsKnown(data.TargetLaps.Value)
                && FuelLevelBar.IsKnown(data.Level) && FuelLevelBar.IsKnown(data.PerLap)
                && data.PerLap > 0 && data.Level < (double)data.TargetLaps.Value * data.PerLap;
            laps.ForeColor = shortOfTarget ? Color.Firebrick : SystemColors.WindowText;
        }

        private static string Format(float value, string unit)
        {
            return FuelLevelBar.IsKnown(value) ? value.ToString("0.##") + unit : "—";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (ReferenceEquals(_instance, this)) { _instance = null; JustOpened = false; }
            base.OnFormClosed(e);
        }
    }
}
