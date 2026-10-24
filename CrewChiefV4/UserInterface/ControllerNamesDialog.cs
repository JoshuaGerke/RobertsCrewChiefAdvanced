using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace CrewChiefV4.UserInterface
{
    public sealed class ControllerNamesDialog : Form
    {
        private sealed class ControllerItem
        {
            public DeviceInstance Device { get; set; }
            public string DisplayName { get; set; }
            public int NamedButtonCount { get; set; }
            public override string ToString() { return DisplayName; }
        }

        private readonly DirectInput directInput = new DirectInput();
        private Joystick joystick;
        private bool[] previousButtons = new bool[0];
        private readonly Dictionary<int, DataGridViewRow> buttonRows = new Dictionary<int, DataGridViewRow>();

        private readonly ComboBox controllerCombo = new ComboBox();
        private readonly Label guidValue = new Label();
        private readonly TextBox controllerNameText = new TextBox();
        private readonly DataGridView grid = new DataGridView();
        private readonly Label statusLabel = new Label();
        private readonly Button saveButton = new Button();
        private readonly Timer timer = new Timer();

        public ControllerNamesDialog()
        {
            Text = "Crew Chief Controller Names";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(720, 620);
            MinimumSize = new Size(620, 480);
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            initialiseControls();
            loadControllers();

            timer.Interval = 30;
            timer.Tick += timer_Tick;
            timer.Start();

            FormClosed += ControllerNamesDialog_FormClosed;
        }

        private void initialiseControls()
        {
            AutoScaleMode = AutoScaleMode.Font;
            Text = "Controller names";
            Padding = new Padding(12);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 6,
                AutoSize = false,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label controllerLabel = makeFieldLabel("Controller:");
            Label guidLabel = makeFieldLabel("GUID:");
            Label nameLabel = makeFieldLabel("Name:");

            controllerCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            controllerCombo.Dock = DockStyle.Fill;
            controllerCombo.Margin = new Padding(6, 3, 0, 3);
            controllerCombo.SelectedIndexChanged += controllerCombo_SelectedIndexChanged;

            guidValue.AutoSize = true;
            guidValue.Dock = DockStyle.Fill;
            guidValue.TextAlign = ContentAlignment.MiddleLeft;
            guidValue.Margin = new Padding(6, 5, 0, 5);

            controllerNameText.Dock = DockStyle.Fill;
            controllerNameText.Margin = new Padding(6, 3, 0, 3);

            var instructions = new Label
            {
                AutoSize = true,
                MaximumSize = new Size(0, 0),
                Text = "Press each controller button once. New buttons will be added to the list. Existing names can be edited.",
                Margin = new Padding(0, 8, 0, 8)
            };

            grid.Dock = DockStyle.Fill;
            grid.Margin = new Padding(0);
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = true;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            grid.Columns.Add("Button", "Button");
            grid.Columns.Add("Name", "Name");
            grid.Columns["Button"].ReadOnly = true;
            grid.Columns["Button"].Width = 90;
            grid.Columns["Button"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns["Name"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 3, 4, 3);
            grid.RowTemplate.Height = 28;

            // WinForms can recalculate the header during DPI/font autoscaling.
            // Set the final height after the form has completed its initial layout.
            Load += (sender, args) => grid.ColumnHeadersHeight = 40;

            saveButton.Text = "Save";
            saveButton.AutoSize = true;
            saveButton.MinimumSize = new Size(100, 0);
            saveButton.Margin = new Padding(12, 0, 0, 0);
            saveButton.Click += saveButton_Click;

            statusLabel.AutoSize = true;
            statusLabel.Dock = DockStyle.Fill;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Margin = Padding.Empty;

            var bottomPanel = new TableLayoutPanel
            {
                AutoSize = true,
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Margin = new Padding(0, 10, 0, 0),
                Padding = Padding.Empty
            };
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            bottomPanel.Controls.Add(statusLabel, 0, 0);
            bottomPanel.Controls.Add(saveButton, 1, 0);

            layout.Controls.Add(controllerLabel, 0, 0);
            layout.Controls.Add(controllerCombo, 1, 0);
            layout.Controls.Add(guidLabel, 0, 1);
            layout.Controls.Add(guidValue, 1, 1);
            layout.Controls.Add(nameLabel, 0, 2);
            layout.Controls.Add(controllerNameText, 1, 2);
            layout.Controls.Add(instructions, 0, 3);
            layout.SetColumnSpan(instructions, 2);
            layout.Controls.Add(grid, 0, 4);
            layout.SetColumnSpan(grid, 2);
            layout.Controls.Add(bottomPanel, 0, 5);
            layout.SetColumnSpan(bottomPanel, 2);

            Controls.Add(layout);
            AcceptButton = saveButton;
        }

        private static Label makeFieldLabel(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 5, 0, 5)
            };
        }

        private void loadControllers()
        {
            try
            {
                var controllerItems = new List<ControllerItem>();

                foreach (DeviceInstance device in directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly))
                {
                    ControllerNames.ControllerNamesData config =
                        ControllerNames.GetControllerNames(device.InstanceGuid);

                    controllerItems.Add(new ControllerItem
                    {
                        Device = device,
                        DisplayName = ControllerNames.GetControllerDisplayName(device.InstanceGuid, device.InstanceName),
                        NamedButtonCount = config != null && config.buttons != null
                            ? config.buttons.Count
                            : 0
                    });
                }

                foreach (ControllerItem item in controllerItems
                    .OrderByDescending(item => item.NamedButtonCount > 0)
                    .ThenBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase))
                {
                    controllerCombo.Items.Add(item);
                }

                if (controllerCombo.Items.Count > 0)
                {
                    controllerCombo.SelectedIndex = 0;
                }
                else
                {
                    statusLabel.Text = "No controllers found.";
                    saveButton.Enabled = false;
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Unable to enumerate controllers: ");
                statusLabel.Text = "Unable to enumerate controllers: " + e.Message;
                saveButton.Enabled = false;
            }
        }

        private ControllerItem selectedController
        {
            get { return controllerCombo.SelectedItem as ControllerItem; }
        }

        private void controllerCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            closeJoystick();
            grid.Rows.Clear();
            buttonRows.Clear();
            previousButtons = new bool[0];

            ControllerItem item = selectedController;
            if (item == null)
            {
                return;
            }

            guidValue.Text = item.Device.InstanceGuid.ToString();
            ControllerNames.ControllerNamesData config = ControllerNames.GetControllerNames(item.Device.InstanceGuid);
            controllerNameText.Text = config != null && !string.IsNullOrWhiteSpace(config.name)
                ? config.name
                : item.Device.InstanceName;

            if (config != null && config.buttons != null)
            {
                foreach (KeyValuePair<int, string> button in config.buttons.OrderBy(b => b.Key))
                {
                    addButtonRow(button.Key, button.Value);
                }
            }

            try
            {
                joystick = new Joystick(directInput, item.Device.InstanceGuid);
                joystick.Properties.BufferSize = 128;
                joystick.Acquire();
                previousButtons = joystick.GetCurrentState().Buttons.ToArray();
                statusLabel.Text = config == null
                    ? "Press buttons on the selected controller."
                    : "Existing names loaded. Press any missed buttons to add them.";
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Unable to acquire controller: ");
                statusLabel.Text = "Unable to acquire controller: " + ex.Message;
                closeJoystick();
            }
        }

        private void addButtonRow(int buttonIndex, string name)
        {
            int rowIndex = grid.Rows.Add(buttonIndex, name ?? string.Empty);
            buttonRows[buttonIndex] = grid.Rows[rowIndex];
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            if (joystick == null)
            {
                return;
            }

            try
            {
                bool[] buttons = joystick.GetCurrentState().Buttons.ToArray();
                for (int i = 0; i < buttons.Length; i++)
                {
                    bool wasPressed = i < previousButtons.Length && previousButtons[i];
                    if (buttons[i] && !wasPressed && !buttonRows.ContainsKey(i))
                    {
                        addButtonRow(i, string.Empty);
                        DataGridViewRow row = buttonRows[i];
                        grid.CurrentCell = row.Cells["Name"];
                        grid.BeginEdit(true);
                        statusLabel.Text = "Button " + i + " detected. Enter its name.";
                    }
                }
                previousButtons = buttons;
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Controller read failed: " + ex.Message;
            }
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            ControllerItem item = selectedController;
            if (item == null)
            {
                return;
            }

            string controllerName = controllerNameText.Text.Trim();
            if (controllerName.Length == 0)
            {
                MessageBox.Show(this, "Enter a controller name.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var buttons = new Dictionary<int, string>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                int buttonIndex;
                if (row.Cells["Button"].Value != null
                    && int.TryParse(row.Cells["Button"].Value.ToString(), out buttonIndex))
                {
                    string name = Convert.ToString(row.Cells["Name"].Value).Trim();
                    if (name.Length > 0)
                    {
                        buttons[buttonIndex] = name;
                    }
                }
            }

            var config = new ControllerNames.ControllerNamesData
            {
                name = controllerName,
                guid = item.Device.InstanceGuid,
                buttons = buttons
            };

            string filename = ControllerNames.FindControllerNamesFile(item.Device.InstanceGuid);
            if (filename == null)
            {
                filename = getNewFileName(item.Device.InstanceName, item.Device.InstanceGuid);
            }

            try
            {
                ControllerNames.SaveControllerNames(filename, config);
                item.DisplayName = controllerName;
                controllerCombo.Refresh();
                statusLabel.Text = "Saved " + filename;
            }
            catch (Exception ex)
            {
                Log.Exception(ex, "Unable to save controller names: ");
                MessageBox.Show(this, ex.Message, "Unable to save controller names", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string getNewFileName(string controllerName, Guid guid)
        {
            string safeName = controllerName;
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                safeName = safeName.Replace(invalid, '_');
            }
            safeName = safeName.Trim();
            if (safeName.Length == 0)
            {
                safeName = "Controller";
            }

            string filename = Path.Combine(DataFiles.ControllerButtonNamesFolder, safeName + ".json");
            if (!File.Exists(filename))
            {
                return filename;
            }
            return Path.Combine(DataFiles.ControllerButtonNamesFolder, safeName + "_" + guid.ToString("N") + ".json");
        }

        private void closeJoystick()
        {
            if (joystick == null)
            {
                return;
            }
            try { joystick.Unacquire(); } catch { }
            try { joystick.Dispose(); } catch { }
            joystick = null;
        }

        private void ControllerNamesDialog_FormClosed(object sender, FormClosedEventArgs e)
        {
            timer.Stop();
            closeJoystick();
            directInput.Dispose();
        }
    }
}
