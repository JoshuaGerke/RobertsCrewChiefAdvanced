#if PIT_MANAGER_DEBUG
using System;
using System.Windows.Forms;

using CrewChiefV4.UserInterface.Models;
using CrewChiefV4.UserInterface.VMs;
using static CrewChiefV4.UserInterface.Models.PitMenuDebug;

namespace CrewChiefV4.UserInterface
{
    public partial class PitMenuDebug_V : Form
    {
        private readonly PitMenuDebug_VM vm;
        private readonly PitMenuDebug model;

        public PitMenuDebug_V()
        {
            StartPosition = FormStartPosition.CenterParent;
            InitializeComponent();

            vm = new PitMenuDebug_VM(this);
            this.Show();
            model = new PitMenuDebug(vm);
            this.SuspendLayout();
            comboBoxMFDs.SelectedIndex = 1;
            this.ResumeLayout(false);
        }

        private void buttonUp_Click(object sender, EventArgs e)
        {
            model.MenuButton(DirectionButton.UP);
        }

        private void buttonLeft_Click(object sender, EventArgs e)
        {
            model.MenuButton(DirectionButton.LEFT);
        }

        private void buttonRight_Click(object sender, EventArgs e)
        {
            model.MenuButton(DirectionButton.RIGHT);
        }

        private void buttonDown_Click(object sender, EventArgs e)
        {
            model.MenuButton(DirectionButton.DOWN);
        }

        private void buttonNextMFD_Click(object sender, EventArgs e)
        {

        }

        private void comboBoxMFDs_SelectedIndexChanged(object sender, EventArgs e)
        {
            string mfd = comboBoxMFDs.Text.Substring(0, 4);
            model.SwitchMFD(mfd);
        }

        private void checkedListBoxMenuCategories_SelectedIndexChanged(object sender, EventArgs e)
        {
            string pitCategory = checkedListBoxMenuCategories.Text.Substring(0);
            model.SelectPitCategory(pitCategory);
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            model.FillMenuChoices();
        }

        private void textBoxFuel_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                model.SetFuel(textBoxFuel.Text);
            }
        }
    }
}
#endif