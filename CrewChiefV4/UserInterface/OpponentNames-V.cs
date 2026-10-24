using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml.Linq;

using CrewChiefV4.UserInterface.Models;
using CrewChiefV4.UserInterface.VMs;

namespace CrewChiefV4.UserInterface
{
    public partial class OpponentNames_V : Form
    {
        private readonly OpponentNames_VM vm;
        private readonly OpponentNames model;
        private readonly IMainWindow mwi;
        public OpponentNames_V(IMainWindow _mwi)
        {
            mwi = _mwi;
            StartPosition = FormStartPosition.CenterParent;
            InitializeComponent();

            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                _ = new DarkModeForms.DarkModeCS(this);

            this.SuspendLayout();
            this.Text = Configuration.getUIString("dialog_title_opponent_names");
            labelOpponentNames.Text = Configuration.getUIString("guessed_opponent_names");
            buttonEditGuessedNames.Text = Configuration.getUIString("edit_guessed_names_file");
            buttonDeleteGuessedNames.Text = Configuration.getUIString("delete_additional_names_file");
            buttonEditOpponentNames.Text = Configuration.getUIString("edit_opponent_names_file");
            toolTipEditGuessedNamesFile.SetToolTip(buttonEditGuessedNames, 
                Configuration.getUIString("edit_guessed_names_file_tooltip"));
            toolTipEditOpponentNamesFile.SetToolTip(buttonEditOpponentNames,
                Configuration.getUIString("edit_opponent_names_file_tooltip"));
            toolTipNamesList.SetToolTip(listBoxOpponentNames,
                Configuration.getUIString("opponent_names_list_tooltip"));

            vm = new OpponentNames_VM(this);
            model = new OpponentNames(vm);
            this.ResumeLayout(false);
        }
        public void fillGuessedOpponentNames(List <string> availableGuessedOpponentNames)
        {
            listBoxOpponentNames.Items.Clear();
            foreach (var name in availableGuessedOpponentNames)
            {
                listBoxOpponentNames.Items.Add(name);
            }
        }

        private void listBoxOpponentNames_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                listBoxOpponentNames.SelectedIndex = listBoxOpponentNames.IndexFromPoint(e.X, e.Y);
                contextMenuStripOpponentNames.Show(Cursor.Position);
            }
        }

        private void buttonEditNames_Click(object sender, EventArgs e)
        {
            model.EditNames();
        }

        private void buttonEditGuessedNames_Click(object sender, MouseEventArgs e)
        {
            model.EditGuessedNames();
        }

        private void buttonDeleteGuessedNames_Click(object sender, EventArgs e)
        {
            model.DeleteGuessedNames();
        }

        private void listBoxOpponentNames_MouseDoubleClick(object sender, EventArgs e)
        {
            model.PlayOpponentDriverName(listBoxOpponentNames.SelectedItem.ToString().Split(':')[0]);
        }

        private void playToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBoxOpponentNames.SelectedItem != null)
            {
                model.PlayOpponentDriverName(listBoxOpponentNames.SelectedItem.ToString().Split(':')[0]);
            }
        }

        private void ignoreToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (listBoxOpponentNames.SelectedItem != null)
            {
                var name = listBoxOpponentNames.SelectedItem.ToString().Split(':')[0];
                var win = new OpponentNameSelection_V(mwi, name);
                win.ShowDialog(this);
                listBoxOpponentNames.Refresh();
            }
        }

        private void OpponentNames_V_FormClosed(object sender, FormClosedEventArgs e)
        {
            model.FormClose();
        }
    }
}
