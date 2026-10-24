using System.Drawing;
using System.Windows.Forms;

namespace CrewChiefV4.UserInterface.TopicWindows
{
    public partial class TopicWindowFuel
    {
        // Keep layout helpers out of the Designer file.
        private static TableLayoutPanel CreateResourceLayout(FuelLevelBar bar, TextBox level,
            TextBox perLap, TextBox perMinute, TextBox capacityOrRatio, TextBox laps, string capacityLabel, TextBox timeLeft = null)
        {
            TableLayoutPanel panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(8), ColumnCount = 4, RowCount = 4
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22F));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            AddField(panel, "Level", level, 0, 0);
            AddField(panel, capacityLabel, capacityOrRatio, 2, 0);
            bar.Dock = DockStyle.Fill;
            panel.Controls.Add(bar, 0, 1);
            panel.SetColumnSpan(bar, 4);
            AddField(panel, "Consumption/lap", perLap, 0, 2);
            AddField(panel, "Consumption/min", perMinute, 2, 2);
            AddField(panel, "Laps to target", laps, 0, 3);
            if (timeLeft != null)
            {
                AddField(panel, "Time left", timeLeft, 2, 3);
            }
            else
            {
                panel.SetColumnSpan(laps, 3);
            }
            panel.FontChanged += (sender, args) => SizeResourceRows(panel);
            SizeResourceRows(panel);
            return panel;
        }

        private static void SizeResourceRows(TableLayoutPanel panel)
        {
            int lineHeight = panel.Font.Height + 10;
            panel.RowStyles[0].Height = lineHeight;
            panel.RowStyles[1].SizeType = SizeType.Percent;
            panel.RowStyles[1].Height = 100F;
            panel.RowStyles[2].Height = lineHeight;
            panel.RowStyles[3].Height = lineHeight;
        }

        private static void AddField(TableLayoutPanel panel, string caption, TextBox value, int column, int row)
        {
            panel.Controls.Add(new Label { Text = caption, AutoSize = true,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, column, row);
            value.ReadOnly = true;
            value.TabStop = false;
            value.Text = "—";
            // A compact, font-scaled field stays narrow when the window is enlarged.
            string exemplar = caption == "Time left" ? "000:00:00" :
                caption == "Laps to target" ? "000.0 to finish" : "0000.00 L/min";
            value.Tag = exemplar;
            value.FontChanged += (sender, args) =>
            {
                value.Width = TextRenderer.MeasureText(exemplar, value.Font).Width + 24;
            };
            value.Width = TextRenderer.MeasureText(exemplar, value.Font).Width + 24;
            value.Anchor = AnchorStyles.Left;
            panel.Controls.Add(value, column + 1, row);
        }
    }
}
