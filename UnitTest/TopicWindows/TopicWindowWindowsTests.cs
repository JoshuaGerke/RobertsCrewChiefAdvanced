using System;
using System.Windows.Forms;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Threading;
using CrewChiefV4.UserInterface.TopicWindows;
using NUnit.Framework;

namespace CrewChiefV4.UnitTest.TopicWindows
{
    [TestFixture]
    [Apartment(ApartmentState.STA)]
    [NonParallelizable]
    public class TopicWindowWindowsTests
    {
        private CultureInfo previousCulture;

        [SetUp]
        public void SetUp()
        {
            previousCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            TopicWindowFuel.ResetData();
        }

        [TearDown]
        public void TearDown()
        {
            TopicWindowFuel.ResetData();
            Thread.CurrentThread.CurrentCulture = previousCulture;
        }

        [Test]
        public void CanCreate_TopicWindowBrakes()
        {
            using (var form = new TopicWindowBrakes())
            {
                Assert.IsNotNull(form);
                Assert.AreEqual("Brakes", form.Text);
                Assert.IsTrue(form is Form);
            }
        }

        [Test]
        public void CanCreate_TopicWindowFuel()
        {
            using (var form = new TopicWindowFuel())
            {
                Assert.IsNotNull(form);
                Assert.AreEqual("Fuel", form.Text);
                Assert.IsTrue(form is Form);
            }
        }

        [Test]
        public void CanCreate_TopicWindowTyres()
        {
            using (var form = new TopicWindowTyres())
            {
                Assert.IsNotNull(form);
                Assert.AreEqual("Tyres", form.Text);
                Assert.IsTrue(form is Form);
            }
        }
        [Test]
        public void Fuel_DisplaysSuppliedValuesAndPitTarget()
        {
            TopicWindowFuel.SetFuelData(42.5f, 100, 5f, 2.5f, 7f);
            using (var form = new TopicWindowFuel())
            {
                Assert.AreEqual("42.5 L", Field<TextBox>(form, "textBoxFuelLevel").Text);
                Assert.AreEqual("100", Field<TextBox>(form, "textBoxFuelCapacity").Text);
                Assert.AreEqual("5 L/lap", Field<TextBox>(form, "textBoxFuelPerLap").Text);
                Assert.AreEqual("2.5 L/min", Field<TextBox>(form, "textBoxFuelPerMinute").Text);
                Assert.AreEqual("7 to pit", Field<TextBox>(form, "textBoxFuelLaps").Text);
                var bar = Field<FuelLevelBar>(form, "fuelLevelBar");
                Assert.AreEqual(42.5f, bar.Level);
                Assert.AreEqual(100f, bar.Capacity);
                Assert.AreEqual(5f, bar.ConsumptionPerLap);
                Assert.AreEqual(7f, bar.TargetLaps);
                Assert.AreEqual("Pit", bar.TargetName);
                Assert.IsTrue(Field<TextBox>(form, "textBoxFuelLevel").ReadOnly);
                Assert.IsTrue(Field<TextBox>(form, "textBoxFuelCapacity").ReadOnly);
            }
        }

        [Test]
        public void Fuel_UpdatesExistingWindowAndUsesFinishTarget()
        {
            using (var form = new TopicWindowFuel())
            {
                TopicWindowFuel.SetFuelData(30f, 80, 4f, 2f, 6f, true);
                Render(form);
                Assert.AreEqual("30 L", Field<TextBox>(form, "textBoxFuelLevel").Text);
                Assert.AreEqual("6 to finish", Field<TextBox>(form, "textBoxFuelLaps").Text);
                Assert.AreEqual("Finish", Field<FuelLevelBar>(form, "fuelLevelBar").TargetName);
            }
        }

        [TestCase(9f, true)]
        [TestCase(8.5f, false)]
        [TestCase(7f, false)]
        public void Fuel_HighlightsOnlyInsufficientFuel(float targetLaps, bool insufficient)
        {
            TopicWindowFuel.SetFuelData(42.5f, 100, 5f, 2.5f, targetLaps);
            using (var form = new TopicWindowFuel())
            {
                Assert.AreEqual(insufficient ? Color.Firebrick : SystemColors.WindowText,
                    Field<TextBox>(form, "textBoxFuelLaps").ForeColor);
            }
        }

        [Test]
        public void Fuel_UnknownDataDisplaysPlaceholders()
        {
            TopicWindowFuel.SetFuelData(float.NaN, 0, -1f, float.PositiveInfinity);
            using (var form = new TopicWindowFuel())
            {
                foreach (string name in new[] { "textBoxFuelLevel", "textBoxFuelCapacity",
                    "textBoxFuelPerLap", "textBoxFuelPerMinute", "textBoxFuelLaps" })
                {
                    Assert.AreEqual("—", Field<TextBox>(form, name).Text, name);
                }
                Assert.IsNull(Field<FuelLevelBar>(form, "fuelLevelBar").TargetLaps);
            }
        }

        [Test]
        public void Fuel_VirtualEnergyIsHiddenAndCollapsedByDefault()
        {
            using (var form = new TopicWindowFuel())
            {
                form.Show();
                Assert.AreEqual("Fuel", form.Text);
                Assert.IsFalse(Field<GroupBox>(form, "groupBoxVirtualEnergy").Visible);
                var layout = Field<TableLayoutPanel>(form, "layout");
                Assert.AreEqual(SizeType.Absolute, layout.RowStyles[1].SizeType);
                Assert.AreEqual(0f, layout.RowStyles[1].Height);
            }
        }

        [Test]
        public void Fuel_VirtualEnergyDisplaysPercentagesAndFuelRatio()
        {
            TopicWindowFuel.SetVirtualEnergyData(true, 65f, 100, 8f, 4f, 7f, true);
            using (var form = new TopicWindowFuel())
            {
                form.Show();
                Assert.AreEqual("Fuel and virtual energy", form.Text);
                Assert.IsTrue(Field<GroupBox>(form, "groupBoxVirtualEnergy").Visible);
                Assert.AreEqual("65%", Field<TextBox>(form, "textBoxVirtualEnergyLevel").Text);
                Assert.AreEqual("8%/lap", Field<TextBox>(form, "textBoxVirtualEnergyPerLap").Text);
                Assert.AreEqual("4%/min", Field<TextBox>(form, "textBoxVirtualEnergyPerMinute").Text);
                Assert.AreEqual("100", Field<TextBox>(form, "textBoxFuelRatio").Text);
                Assert.AreEqual("7 to finish", Field<TextBox>(form, "textBoxVirtualEnergyLaps").Text);
                var bar = Field<FuelLevelBar>(form, "virtualEnergyLevelBar");
                Assert.AreEqual(100f, bar.Capacity);
                Assert.AreEqual(65f, bar.Level);
                Assert.AreEqual(8f, bar.ConsumptionPerLap);
                Assert.AreEqual("Finish", bar.TargetName);
            }
        }

        [Test]
        public void Fuel_ChangingCarHidesVirtualEnergyAndRestoresWindowHeight()
        {
            using (var form = new TopicWindowFuel())
            {
                form.Show();
                int fuelOnlyHeight = form.Height;
                TopicWindowFuel.SetVirtualEnergyData(true, 65f, 100, 8f, 4f, 7f);
                Render(form);
                Assert.AreEqual("Fuel and virtual energy", form.Text);
                Assert.IsTrue(Field<GroupBox>(form, "groupBoxVirtualEnergy").Visible);
                Assert.Greater(form.Height, fuelOnlyHeight);
                int energyHeight = form.Height;
                Render(form);
                Assert.AreEqual(energyHeight, form.Height, "Repeated refresh must not resize the window.");
                TopicWindowFuel.SetVirtualEnergyData(false, 65f, 100, 8f, 4f, 7f);
                Render(form);
                Assert.AreEqual("Fuel", form.Text);
                Assert.IsFalse(Field<GroupBox>(form, "groupBoxVirtualEnergy").Visible);
                Assert.AreEqual(fuelOnlyHeight, form.Height);
                Assert.AreEqual(0f, Field<TableLayoutPanel>(form, "layout").RowStyles[1].Height);
                Assert.AreEqual("—", Field<TextBox>(form, "textBoxVirtualEnergyLevel").Text);
            }
        }

        [Test]
        public void Fuel_ResetClearsOldSessionValuesAndVirtualEnergy()
        {
            TopicWindowFuel.SetFuelData(42.5f, 100, 5f, 2.5f, 7f);
            TopicWindowFuel.SetVirtualEnergyData(true, 65f, 100, 8f, 4f, 7f);
            using (var form = new TopicWindowFuel())
            {
                form.Show();
                TopicWindowFuel.ResetData();
                Render(form);
                Assert.AreEqual("—", Field<TextBox>(form, "textBoxFuelLevel").Text);
                Assert.AreEqual("—", Field<TextBox>(form, "textBoxFuelLaps").Text);
                Assert.AreEqual("Fuel", form.Text);
                Assert.IsFalse(Field<GroupBox>(form, "groupBoxVirtualEnergy").Visible);
            }
        }

        [Test]
        public void Fuel_ReopenedWindowUsesLatestSnapshot()
        {
            TopicWindowFuel.SetFuelData(42.5f, 100, 5f, 2.5f);
            using (var form = new TopicWindowFuel()) { }
            TopicWindowFuel.SetFuelData(20f, 100, 5f, 2.5f);
            using (var form = new TopicWindowFuel())
            {
                Assert.AreEqual("20 L", Field<TextBox>(form, "textBoxFuelLevel").Text);
            }
        }

        [TestCase(5f)]
        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void FuelLevelBar_CanPaintWithMissingOrValidConsumption(float consumption)
        {
            using (var bar = new FuelLevelBar { Size = new Size(416, 100),
                Level = 42.5f, Capacity = 100f, ConsumptionPerLap = consumption, TargetLaps = 7f })
            using (var bitmap = new Bitmap(bar.Width, bar.Height))
            {
                Assert.DoesNotThrow(() => bar.DrawToBitmap(bitmap, bar.ClientRectangle));
            }
        }

        [Test]
        public void FuelLevelBar_LapTickMovesWithCurrentLevel()
        {
            using (var bar = new FuelLevelBar { Size = new Size(416, 100),
                Level = 42.5f, Capacity = 100f, ConsumptionPerLap = 5f })
            using (var before = new Bitmap(bar.Width, bar.Height))
            using (var after = new Bitmap(bar.Width, bar.Height))
            {
                // With a 400px bar and 100L capacity, lap 1 is at 37.5L => x=158.
                // After burning another litre, it moves to 36.5L => x=154.
                int y = bar.Font.Height + 12;
                bar.DrawToBitmap(before, bar.ClientRectangle);
                bar.Level = 41.5f;
                bar.DrawToBitmap(after, bar.ClientRectangle);
                Assert.Greater(before.GetPixel(158, y).R, before.GetPixel(154, y).R);
                Assert.Greater(after.GetPixel(154, y).R, after.GetPixel(158, y).R);
            }
        }

        // Manual previews are skipped by Run All. Select an individual case to run or debug it.
        // ShowDialog keeps the window responsive until it is closed.
        [TestCase(false, false, TestName = "Show_TopicWindowFuel_FuelOnly")]
        [TestCase(true, false, TestName = "Show_TopicWindowFuel_WithVirtualEnergy")]
        [TestCase(true, true, TestName = "Show_TopicWindowFuel_InsufficientForFinish")]
        [Explicit("Manual visual inspection; close the window to finish")]
        [Category("ManualUI")]
        public void Show_TopicWindowFuel(bool virtualEnergyAvailable, bool insufficient)
        {
            float targetLaps = insufficient ? 9f : 7f;
            TopicWindowFuel.SetTimeLeft(7f * 120f);
            TopicWindowFuel.SetFuelData(42.5f, 100, 5f, 2.5f,
                targetLaps, targetIsRaceFinish: insufficient);
            TopicWindowFuel.SetVirtualEnergyData(virtualEnergyAvailable, 65f, 100,
                8f, 4f, targetLaps, targetIsRaceFinish: insufficient);

            using (var form = new TopicWindowFuel())
            {
                form.StartPosition = FormStartPosition.CenterScreen;
                form.ShowDialog();
            }
            // The fixture's TearDown resets the static fuel/VE snapshots.
        }

        [Test]
        [Explicit("Manual visual inspection; close the window to finish")]
        [Category("ManualUI")]
        public void Show_TopicWindowBrakes()
        {
            using (var form = new TopicWindowBrakes())
            {
                form.StartPosition = FormStartPosition.CenterScreen;
                form.ShowDialog();
            }
        }

        [Test]
        [Explicit("Manual visual inspection; close the window to finish")]
        [Category("ManualUI")]
        public void Show_TopicWindowTyres()
        {
            using (var form = new TopicWindowTyres())
            {
                form.StartPosition = FormStartPosition.CenterScreen;
                // The tyre setters require a visible form, so populate it after opening.
                form.Shown += (sender, args) =>
                {
                    TopicWindowTyres.SetFLTemp(85f);
                    TopicWindowTyres.SetFRTemp(90f);
                    TopicWindowTyres.SetRLTemp(80f);
                    TopicWindowTyres.SetRRTemp(82f);
                };
                form.ShowDialog();
            }
        }

        [Test]
        public void Fuel_VirtualEnergyRequiresPositiveReadingAndStaysVisibleAtZero()
        {
            TopicWindowFuel.SetVirtualEnergyData(true, 0f, 100, 8f, 4f);
            using (var form = new TopicWindowFuel())
            {
                form.Show();
                Assert.IsFalse(Field<GroupBox>(form, "groupBoxVirtualEnergy").Visible);
                TopicWindowFuel.SetVirtualEnergyData(true, 50f, 100, 8f, 4f);
                Render(form);
                Assert.IsTrue(Field<GroupBox>(form, "groupBoxVirtualEnergy").Visible);
                TopicWindowFuel.SetVirtualEnergyData(true, 0f, 100, 8f, 4f);
                Render(form);
                Assert.IsTrue(Field<GroupBox>(form, "groupBoxVirtualEnergy").Visible);
                Assert.AreEqual("0%", Field<TextBox>(form, "textBoxVirtualEnergyLevel").Text);
                TopicWindowFuel.ResetData();
                TopicWindowFuel.SetVirtualEnergyData(true, 0f, 100, 8f, 4f);
                Render(form);
                Assert.IsFalse(Field<GroupBox>(form, "groupBoxVirtualEnergy").Visible);
            }
        }

        [Test]
        public void Fuel_TimeLeftDisplaysHoursMinutesSecondsAndResets()
        {
            TopicWindowFuel.SetTimeLeft(3661f);
            using (var form = new TopicWindowFuel())
            {
                Assert.AreEqual("01:01:01", Field<TextBox>(form, "textBoxTimeLeft").Text);
                TopicWindowFuel.SetTimeLeft(0f);
                Render(form);
                Assert.AreEqual("00:00:00", Field<TextBox>(form, "textBoxTimeLeft").Text);
                TopicWindowFuel.ResetData();
                Render(form);
                Assert.AreEqual("—", Field<TextBox>(form, "textBoxTimeLeft").Text);
            }
        }

        [Test]
        public void Fuel_DisabledConsumptionShowsMessageAndRestoresDisplayWhenEnabled()
        {
            TopicWindowFuel.SetFuelData(42.5f, 100, 5f, 2.5f, 7f);
            TopicWindowFuel.SetFuelUseActive(false);
            using (var form = new TopicWindowFuel())
            {
                form.Show();
                Assert.IsFalse(Field<TableLayoutPanel>(form, "layout").Visible);
                var message = Field<Label>(form, "labelFuelUseDisabled");
                Assert.IsTrue(message.Visible);
                Assert.AreEqual("Fuel consumption turned off in game", message.Text);
                TopicWindowFuel.SetFuelUseActive(true);
                Render(form);
                Assert.IsTrue(Field<TableLayoutPanel>(form, "layout").Visible);
                Assert.IsFalse(message.Visible);
                Assert.AreEqual("42.5 L", Field<TextBox>(form, "textBoxFuelLevel").Text);
            }
        }

        [Test]
        [Explicit("Manual visual inspection; close the window to finish")]
        [Category("ManualUI")]
        public void Show_TopicWindowFuel_ConsumptionDisabled()
        {
            TopicWindowFuel.SetFuelUseActive(false);
            using (var form = new TopicWindowFuel())
            {
                form.StartPosition = FormStartPosition.CenterScreen;
                form.ShowDialog();
            }
        }

        private static T Field<T>(TopicWindowFuel form, string name) where T : class
        {
            var field = typeof(TopicWindowFuel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "Missing fuel window field: " + name);
            return (T)field.GetValue(form);
        }

        private static void Render(TopicWindowFuel form)
        {
            // Render deterministically without sleeps or waiting for the UI timer.
            var method = typeof(TopicWindowFuel).GetMethod("RenderData", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            method.Invoke(form, null);
        }
    }
}