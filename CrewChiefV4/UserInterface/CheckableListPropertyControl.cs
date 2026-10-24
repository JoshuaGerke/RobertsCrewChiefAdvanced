using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CrewChiefV4
{
    public partial class CheckableListPropertyControl : UserControl
    {
        public bool changeRequiresRestart;
        public string propertyId;
        public List<string> availableValues;
        public List<string> defaultValues;
        public List<string> originalValues;
        public string label;
        private PropertiesForm parent;
        internal PropertyFilter filter = null;
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
        public CheckableListPropertyControl(
            string propertyId,
            string label,
            List<string> currentValues,
            List<string> defaultValues,
            string helpText,
            string filterText,
            string categoryText,
            bool changeRequiresRestart,
            List<string> availableValues,
            PropertiesForm parent)
        {
            InitializeComponent();
            this.parent = parent;
            this.label = label;
            this.propertyId = propertyId;
            label1.Text = label;
            this.availableValues = availableValues;
            this.defaultValues = defaultValues;
            originalValues = new List<string>(currentValues);
            this.changeRequiresRestart = changeRequiresRestart;

            initDropdown(currentValues, availableValues);
            UpdateDropdownText();

            toolTip1.SetToolTip(checkedListBox1, helpText);
            toolTip1.SetToolTip(label1, helpText);
            toolTip1.SetToolTip(dropdownTextBox, helpText);
            toolTip1.SetToolTip(dropdownButton, helpText);

            filter = new PropertyFilter(filterText, categoryText, changeRequiresRestart, propertyId, this.label, helpText);
        }

        private void initDropdown(List<string> currentValues, List<string> availableValues)
        {
            checkedListBox1.Items.Clear();
            foreach (string value in availableValues)
            {
                checkedListBox1.Items.Add(value, currentValues.Contains(value));
            }
        }

        public List<string> GetSelectedValues()
        {
            var selected = new List<string>();
            foreach (var item in checkedListBox1.CheckedItems)
            {
                selected.Add(item.ToString());
            }
            return selected;
        }

        public void initValues(List<string> values)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
            {
                checkedListBox1.SetItemChecked(i, values.Contains(checkedListBox1.Items[i].ToString()));
            }
            originalValues = new List<string>(values);
            filter.propertyChanged = false;
            CheckDefault();
        }

        private void CheckDefault()
        {
            if (filter != null)
            {
                bool notDefault = !GetSelectedValues().SequenceEqual(defaultValues);
                filter.propertyNotDefault = notDefault;
            }
        }

        private void CheckedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (IsHandleCreated)
            {
                BeginInvoke((Action)(() =>
                {
                    bool changed = !GetSelectedValues().SequenceEqual(originalValues);
                    if (filter != null)
                    {
                        filter.propertyChanged = changed;
                        CheckDefault();
                        parent.updateChangedState(filter.propertyChanged,
                            propertyId, changeRequiresRestart);
                    }
                    UpdateDropdownText();
                }));
            }
        }


        public void resetToDefault()
        {
            initDropdown(availableValues, availableValues);
        }

        public void onSave()
        {
            filter.propertyChanged = false;
            originalValues = GetSelectedValues();
            string originalValuesString = string.Join(",", originalValues);
            UserSettings.GetUserSettings().setProperty(propertyId, originalValuesString);
        }

        private ToolStripDropDown dropdown;
        private CheckedListBox checkedListBox1;
        private TextBox dropdownTextBox;
        private Button dropdownButton;
        private Label label1;
        private ToolTip toolTip1;

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            label1 = new Label();
            dropdownTextBox = new TextBox();
            dropdownButton = new Button();
            checkedListBox1 = new CheckedListBox();
            toolTip1 = new ToolTip(components);
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(6, 6);
            label1.Margin = new Padding(4, 0, 4, 0);
            label1.Name = "label1";
            label1.Size = new Size(51, 20);
            label1.TabIndex = 0;
            // 
            // dropdownTextBox
            // 
            dropdownTextBox.Location = new Point(7, 35);
            dropdownTextBox.Name = "dropdownTextBox";
            dropdownTextBox.Size = new Size(360, 21);
            dropdownTextBox.BackColor = Color.White;
            dropdownTextBox.ReadOnly = true;
            dropdownTextBox.TabIndex = 1;
            dropdownTextBox.Click += DropdownTextBox_Click;
            // 
            // dropdownButton
            // 
            dropdownButton.Location = new Point(366, 33);
            dropdownButton.Name = "dropdownButton";
            dropdownButton.Size = new Size(28, 31);
            dropdownButton.TabIndex = 2;
            dropdownButton.Text = "v";
            dropdownButton.Click += DropdownButton_Click;
            // 
            // checkedListBox1
            // 
            checkedListBox1.FormattingEnabled = true;
            checkedListBox1.Location = new Point(0, 0);
            checkedListBox1.Name = "checkedListBox1";
            checkedListBox1.Size = new Size(355, 500); // Deeper dropdown
            checkedListBox1.TabIndex = 4;
            checkedListBox1.ItemCheck += CheckedListBox1_ItemCheck;
            checkedListBox1.LostFocus += CheckedListBox1_LostFocus;
            // 
            // CheckableListPropertyControl
            // 
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
			Controls.Add(label1);
            Controls.Add(dropdownTextBox);
            Controls.Add(dropdownButton);
            //this.Margin = new Padding(4, 5, 4, 5);
            Name = "CheckableListPropertyControl";
            Size = new Size(435, 71);
            ResumeLayout(false);
            PerformLayout();
        }

        private void ShowDropdownPanel()
        {
            if (dropdown == null)
            {
                ToolStripControlHost host = new ToolStripControlHost(checkedListBox1)
                {
                    Margin = Padding.Empty,
                    Padding = Padding.Empty,
                    AutoSize = false,
                    Size = checkedListBox1.Size
                };
                dropdown = new ToolStripDropDown
                {
                    Padding = Padding.Empty,
                    AutoClose = true
                };
                dropdown.Items.Add(host);
                dropdown.Closed += (s, e) => UpdateDropdownText();
            }
            else
            {
                dropdown.Close();
                dropdown = null;
                return;
            }

            Point screenLocation = PointToScreen(new Point(dropdownTextBox.Left, dropdownTextBox.Bottom));
            dropdown.Show(screenLocation);
            checkedListBox1.Focus();
        }

        private void DropdownTextBox_Click(object sender, EventArgs e)
        {
            ShowDropdownPanel();
        }

        private void DropdownButton_Click(object sender, EventArgs e)
        {
            ShowDropdownPanel();
        }

        private void CheckedListBox1_LostFocus(object sender, EventArgs e)
        {
            if (dropdown != null && dropdown.Visible)
                dropdown.Close();
        }

        private void UpdateDropdownText()
        {
            var selected = GetSelectedValues();
            dropdownTextBox.Text = selected.Count > 0 ? string.Join(", ", selected) : "";
        }
    }
}
