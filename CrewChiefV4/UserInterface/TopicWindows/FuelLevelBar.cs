using System;
using System.Drawing;
using System.Windows.Forms;

namespace CrewChiefV4.UserInterface.TopicWindows
{
    /// <summary>A read-only level bar with ticks showing the level after each further lap.</summary>
    public class FuelLevelBar : Control
    {
        private float level = float.NaN;
        private float capacity = float.NaN;
        private float consumptionPerLap = float.NaN;
        private float? targetLaps;
        private string targetName = "Pit";

        public FuelLevelBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            TabStop = false;
            Size = new Size(400, 88);
        }

        public float Level { get { return level; } set { level = value; Invalidate(); } }
        public float Capacity { get { return capacity; } set { capacity = value; Invalidate(); } }
        public float ConsumptionPerLap { get { return consumptionPerLap; } set { consumptionPerLap = value; Invalidate(); } }
        public float? TargetLaps { get { return targetLaps; } set { targetLaps = value; Invalidate(); } }
        public string TargetName { get { return targetName; } set { targetName = value ?? "Pit"; Invalidate(); } }

        internal static bool IsKnown(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            int textHeight = Font.Height;
            Rectangle bar = new Rectangle(8, textHeight + 8, Width - 16,
                Math.Min(2 * textHeight, Math.Max(8, Height - 3 * textHeight - 22)));
            if (bar.Width <= 0 || Height < textHeight + 20)
            {
                return;
            }
            Color textColor = Enabled ? ForeColor : SystemColors.GrayText;
            using (Brush background = new SolidBrush(SystemColors.ControlLight))
                e.Graphics.FillRectangle(background, bar);
            e.Graphics.DrawRectangle(SystemPens.ControlDark, bar);
            DrawText(e.Graphics, "Empty", new Rectangle(bar.Left, bar.Bottom + textHeight + 4, bar.Width / 2, textHeight), textColor, false);
            DrawText(e.Graphics, "Full", new Rectangle(bar.Left + bar.Width / 2, bar.Bottom + textHeight + 4, bar.Width / 2, textHeight), textColor, true);
            if (!IsKnown(level) || !IsKnown(capacity) || capacity <= 0)
            {
                DrawText(e.Graphics, "Level / capacity unavailable", bar, textColor, false);
                return;
            }

            double shownLevel = Math.Min(level, capacity);
            int fillWidth = (int)Math.Round(bar.Width * shownLevel / capacity);
            bool targetKnown = targetLaps.HasValue && IsKnown(targetLaps.Value);
            bool consumptionKnown = IsKnown(consumptionPerLap) && consumptionPerLap > 0;
            double remainingAtTarget = targetKnown && consumptionKnown
                ? level - (double)targetLaps.Value * consumptionPerLap : double.NaN;
            Color fillColor = !Enabled ? SystemColors.ControlDark :
                remainingAtTarget < 0 ? Color.Firebrick : Color.SeaGreen;
            using (Brush fill = new SolidBrush(fillColor))
                e.Graphics.FillRectangle(fill, bar.Left + 1, bar.Top + 1, Math.Max(0, fillWidth - 1), bar.Height - 1);
            if (!Enabled || !consumptionKnown) return;

            DrawLapMarkers(e.Graphics, bar, shownLevel, textColor);
            if (targetKnown)
            {
                int x = bar.Left + (int)Math.Round(bar.Width * Math.Max(0, Math.Min(capacity, remainingAtTarget)) / capacity);
                // Keep the target orange even when short; a white outline separates it from the red fill.
                using (Pen outline = new Pen(Color.White, 7))
                    e.Graphics.DrawLine(outline, x, bar.Top - 4, x, bar.Bottom);
                using (Pen pen = new Pen(Color.DarkOrange, 3))
                    e.Graphics.DrawLine(pen, x, bar.Top - 4, x, bar.Bottom);
                string caption = targetName + (remainingAtTarget < 0 ? " — short" : "") + " (" + targetLaps.Value.ToString("0.#") + " laps)";
                DrawText(e.Graphics, caption, new Rectangle(bar.Left, 0, bar.Width, textHeight + 4), textColor, true);
            }
        }

        private void DrawLapMarkers(Graphics graphics, Rectangle bar, double shownLevel, Color textColor)
        {
            double lapCount = Math.Ceiling(shownLevel / consumptionPerLap) - 1;
            if (lapCount < 1) return;
            double spacing = bar.Width * (double)consumptionPerLap / capacity;
            // At sub-pixel spacing, sample ticks to keep painting bounded by the bar width.
            double tickStep = Math.Max(1, Math.Ceiling(2 / spacing));
            int labelWidth = TextRenderer.MeasureText(lapCount.ToString("0"), Font).Width + 10;
            double labelStep = Math.Max(tickStep, Math.Ceiling(labelWidth / spacing / tickStep) * tickStep);
            using (Pen tick = new Pen(Color.FromArgb(210, Color.White)))
            {
                for (double lap = tickStep; lap <= lapCount; lap += tickStep)
                {
                    double remaining = shownLevel - lap * consumptionPerLap;
                    if (remaining > capacity) continue;
                    int x = bar.Left + (int)Math.Round(bar.Width * remaining / capacity);
                    graphics.DrawLine(tick, x, bar.Top + 1, x, bar.Bottom - 1);
                }
            }
            for (double lap = labelStep; lap <= lapCount; lap += labelStep)
            {
                double remaining = shownLevel - lap * consumptionPerLap;
                if (remaining > capacity) continue;
                int x = bar.Left + (int)Math.Round(bar.Width * remaining / capacity);
                Rectangle label = new Rectangle(Math.Max(bar.Left, Math.Min(bar.Right - labelWidth, x - labelWidth / 2)), bar.Bottom + 2, labelWidth, Font.Height);
                TextRenderer.DrawText(graphics, lap.ToString("0"), Font, label, textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
            }
        }

        private void DrawText(Graphics graphics, string text, Rectangle bounds, Color color, bool right)
        {
            TextRenderer.DrawText(graphics, text, Font, bounds, color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                (right ? TextFormatFlags.Right : TextFormatFlags.Left));
        }
    }
}
