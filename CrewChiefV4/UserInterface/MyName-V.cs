using CrewChiefV4.UserInterface.Models;
using CrewChiefV4.UserInterface.VMs;

using System;
using System.Drawing;
using System.Windows.Forms;

namespace CrewChiefV4.UserInterface
{
    /// <summary>
    /// View module of MyName dialog MVVM
    /// </summary>
    public partial class MyName_V : Form
    {
        private readonly MyName_VM vm;
        private readonly MyName model;
        private readonly MainWindow mwi;
        public MyName_V(MainWindow _mwi, string oldName)
        {
            mwi = _mwi;
            
            StartPosition = FormStartPosition.CenterParent;
            InitializeComponent();

            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                _ = new DarkModeForms.DarkModeCS(this);

            vm = new MyName_VM(this);
            model = new MyName(vm);
            this.SuspendLayout();
            this.Text = Configuration.getUIString("selecting_a_name");
            labelEnterYourName.Text = Configuration.getUIStringWrapped("enter_your_name");
            toolTipMyName.SetToolTip(labelEnterYourName, Configuration.getUIString("enter_your_name_help"));
            textBoxMyName.Text = oldName;
            toolTipMyName.SetToolTip(textBoxMyName, Configuration.getUIString("enter_your_name_help"));
            labelFullPersonalisation.Text = Configuration.getUIString("full_personalisation");
            toolTipMyName.SetToolTip(labelFullPersonalisation, Configuration.getUIString("full_personalisation_help"));
            toolTipMyName.SetToolTip(listBoxPersonalisations, Configuration.getUIString("full_personalisation_help"));
            labelDriverName.Text = Configuration.getUIString("driver_name");
            toolTipMyName.SetToolTip(labelDriverName, Configuration.getUIString("driver_name_help"));
            toolTipMyName.SetToolTip(listBoxDriverNames, Configuration.getUIString("driver_name_help"));
            labelOtherDriverName.Text = Configuration.getUIString("other_driver_name");
            toolTipMyName.SetToolTip(labelOtherDriverName, Configuration.getUIString("other_driver_name_help"));
            toolTipMyName.SetToolTip(listBoxOtherDriverNames, Configuration.getUIString("other_driver_name_help"));
            //buttonPlayName.Text = Configuration.getUIString("play_name_sample");
            buttonNameSelect.Text = Configuration.getUIString("select");
            buttonNoName.Text = Configuration.getUIString("no_name");
            toolTipMyName.SetToolTip(buttonNoName, Configuration.getUIString("no_name_help"));
            toolTipMyName.SetToolTip(buttonPlayName, Configuration.getUIString("play_name_sample_help"));
            


            // Set the Play and Search button icon sizes
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MyName_V));
            var img = (System.Drawing.Image)(resources.GetObject("buttonPlayName.Image"));
            buttonPlayName.Image = new Bitmap(img, new Size(12, 12));
            img = (System.Drawing.Image)(resources.GetObject("buttonSearch.Image"));
            buttonSearch.Image = new Bitmap(img, new Size(10, 10));

            if (textBoxMyName.Text.Length > 0)
            {
                // Run the model
                model.NameEntry(textBoxMyName.Text);
            }
            this.ResumeLayout(false);
        }

        private void textBoxMyName_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return && textBoxMyName.Text.Length > 0)
            {
                // Run the model
                model.NameEntry(textBoxMyName.Text);
                e.SuppressKeyPress = true;  // Prevent the error beep
            }
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }

        private void buttonSearch_Click(object sender, EventArgs e)
        {
            if (textBoxMyName.Text.Length > 0)
            {
                // Run the model
                model.NameEntry(textBoxMyName.Text);
            }
        }

        public void fillPersonalisations(string[] names)
        {
            listBoxPersonalisations.Enabled = true;
            listBoxPersonalisations.Items.Clear();
            foreach (var name in names)
            {
                listBoxPersonalisations.Items.Add(name);
            }
        }
        public void selectPersonalisation(int index)
        {
            listBoxPersonalisations.SelectedIndex = index;
        }
        public void fillDriverNames(string[] names)
        {
            listBoxDriverNames.Enabled = true;
            listBoxDriverNames.Items.Clear();
            foreach (var name in names)
            {
                listBoxDriverNames.Items.Add(name);
            }
            // May not be any other names
            listBoxOtherDriverNames.Items.Clear();
            listBoxOtherDriverNames.Enabled = false;
        }
        public void selectDriverName(int index)
        {
            listBoxDriverNames.SelectedIndex = index;
        }
        public void fillOtherDriverNames(string[] names)
        {
            listBoxOtherDriverNames.Enabled = true;
            listBoxOtherDriverNames.Items.Clear();
            foreach (var name in names)
            {
                listBoxOtherDriverNames.Items.Add(name);
            }
        }
        public void doRestart()
        {
            mwi.doRestart(Configuration.getUIString("the_application_must_be_restarted_to_load_the_new_sounds"),
                Configuration.getUIString("load_new_sounds"));
        }

        private void buttonPlayName_Click(object sender, EventArgs e)
        {
            if (listBoxPersonalisations.SelectedIndex != -1)
            {
                model.PlayRandomPersonalisation(listBoxPersonalisations.SelectedItem.ToString());
            }
            if (listBoxDriverNames.SelectedIndex != -1)
            {
                model.PlayRandomDriverName(listBoxDriverNames.SelectedItem.ToString());
            }
            if (listBoxOtherDriverNames.SelectedIndex != -1)
            {
                model.PlayRandomDriverName(listBoxOtherDriverNames.SelectedItem.ToString());
            }
        }

        private void buttonNameSelect_Click(object sender, EventArgs e)
        {
            string name = null;
            if (listBoxPersonalisations.SelectedIndex != -1)
            {
                name = listBoxPersonalisations.SelectedItem.ToString();
                model.SelectPersonalisation(name);
            }
            else if (listBoxDriverNames.SelectedIndex != -1)
            {
                name = listBoxDriverNames.SelectedItem.ToString();
                model.SelectDriverName(name);
            }
            else if (listBoxOtherDriverNames.SelectedIndex != -1)
            {
                name = listBoxOtherDriverNames.SelectedItem.ToString();
                model.SelectDriverName(name);
            }
            mwi.SetButtonMyNameText();
            this.Close();
        }

        private void listBoxPersonalisations_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxPersonalisations.SelectedIndex != -1)
            {   // listBoxDriverNames cleared this one
                listBoxDriverNames.SelectedIndex = -1;
                listBoxOtherDriverNames.SelectedIndex = -1;
            }

            buttonPlayName.Enabled = true;
            buttonNameSelect.Enabled = true;
        }

        private void listBoxDriverNames_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxDriverNames.SelectedIndex != -1)
            {   // listBoxPersonalisations cleared this one
                listBoxPersonalisations.SelectedIndex = -1;
                listBoxOtherDriverNames.SelectedIndex = -1;
            }
            buttonPlayName.Enabled = true;
            buttonNameSelect.Enabled = true;
        }

        private void listBoxOtherDriverNames_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxOtherDriverNames.SelectedIndex != -1)
            {   // listBoxPersonalisations cleared this one
                listBoxPersonalisations.SelectedIndex = -1;
                listBoxDriverNames.SelectedIndex = -1;
            }
            buttonPlayName.Enabled = true;
            buttonNameSelect.Enabled = true;
        }

        private void listBoxPersonalisations_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            buttonPlayName_Click(sender, e);
        }

        private void listBoxDriverNames_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            buttonPlayName_Click(sender, e);
        }

        private void listBoxOtherDriverNames_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            buttonPlayName_Click(sender, e);
        }

        private void buttonNoName_Click(object sender, EventArgs e)
        {
            model.SelectDriverName(string.Empty);
            mwi.SetButtonMyNameText();
            this.Close();
        }

        private void MyName_V_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
            }
        }
    }
}
