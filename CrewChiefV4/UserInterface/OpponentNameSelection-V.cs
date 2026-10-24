using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using CrewChiefV4.UserInterface.Models;
using CrewChiefV4.UserInterface.VMs;

namespace CrewChiefV4.UserInterface
{
    public partial class OpponentNameSelection_V : Form
    {
        private readonly OpponentNameSelection_VM vm;
        private readonly OpponentNameSelection model;
        private readonly IMainWindow mwi;
        private string OpponentName;
        public OpponentNameSelection_V(IMainWindow _mwi, string opponentName)
        {
            if (!string.IsNullOrEmpty(opponentName))
            {
                mwi = _mwi;
                StartPosition = FormStartPosition.CenterParent;
                InitializeComponent();

                if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                    _ = new DarkModeForms.DarkModeCS(this);

                this.SuspendLayout();
                vm = new OpponentNameSelection_VM(this);
                model = new OpponentNameSelection(vm);
                this.Text = Configuration.getUIString("dialog_title_opponent_name_selection");
                labelOpponentName.Text = Configuration.getUIString("opponent_name");
                labelDriverName.Text = Configuration.getUIString("driver_name");
                labelOtherDriverName.Text = Configuration.getUIString("other_driver_name");
                buttonNameSelect.Text = Configuration.getUIString("select");
                buttonNoneOfTheAbove.Text = Configuration.getUIString("none_of_the_above");
                toolTipNoneOfTheAbove.SetToolTip(buttonNoneOfTheAbove,
                    Configuration.getUIString("none_of_the_above_tooltip"));
                labelOpponentNameEntry.Text = opponentName;
                // Set the Play button icon size
                System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MyName_V));
                var img = (System.Drawing.Image)(resources.GetObject("buttonPlayName.Image"));
                buttonPlayName.Image = new Bitmap(img, new Size(12, 12));

                OpponentName = opponentName;
                model.NameEntry(opponentName);
                this.ResumeLayout(false);
            }
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
        public void selectedDriverName(string wavFileName)
        {
            model.NewDriverName(OpponentName, wavFileName.ToLower());
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
        private void buttonPlayName_Click(object sender, EventArgs e)
        {
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
            string name = string.Empty;
            if (listBoxDriverNames.SelectedIndex != -1)
            {
                name = listBoxDriverNames.SelectedItem.ToString();
                model.SelectedDriverName(name);
            }
            else if (listBoxOtherDriverNames.SelectedIndex != -1)
            {
                name = listBoxOtherDriverNames.SelectedItem.ToString();
                model.SelectedDriverName(name);
            }
            this.Close();
        }

        private void listBoxDriverNames_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxDriverNames.SelectedIndex != -1)
            {   // listBoxOtherDriverNames cleared this one
                listBoxOtherDriverNames.SelectedIndex = -1;
            }
            buttonPlayName.Enabled = true;
            buttonNameSelect.Enabled = true;
        }

        private void listBoxOtherDriverNames_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxOtherDriverNames.SelectedIndex != -1)
            {   // listBoxDriverNames cleared this one
                listBoxDriverNames.SelectedIndex = -1;
            }
            buttonPlayName.Enabled = true;
            buttonNameSelect.Enabled = true;
        }

        private void listBoxDriverNames_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            buttonPlayName_Click(sender, e);
        }

        private void listBoxOtherDriverNames_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            buttonPlayName_Click(sender, e);
        }

        private void buttonNoneOfTheAbove_Click(object sender, EventArgs e)
        {
            selectedDriverName(string.Empty);
            this.Close();
        }
    }
}
