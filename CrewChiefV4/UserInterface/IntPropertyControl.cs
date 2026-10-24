using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace CrewChiefV4
{
    public partial class IntPropertyControl : UserControl
    {
        public bool changeRequiresRestart;
        public String propertyId;
        public int originalValue;
        public int defaultValue;
        public String label;
        private PropertiesForm parent;
        internal PropertyFilter filter = null;

        public IntPropertyControl(String propertyId, String label, int value, int defaultValue, String helpText, String filterText,
            String categoryText, bool changeRequiresRestart, PropertiesForm parent)
        {
            InitializeComponent();
            this.parent = parent;
            this.label = label;
            this.propertyId = propertyId;
            this.label1.Text = label;
            this.originalValue = value;
            this.textBox1.Text = value.ToString();
            this.defaultValue = defaultValue;
            this.toolTip1.SetToolTip(this.textBox1, helpText);
            this.toolTip1.SetToolTip(this.label1, helpText);

            this.changeRequiresRestart = changeRequiresRestart;

            // This has to be initialized last.
            this.filter = new PropertyFilter(filterText, categoryText, changeRequiresRestart, propertyId, this.label, helpText);
        }

        public int getValue()
        {
            int newVal;
            if (int.TryParse(this.textBox1.Text, out newVal))
            {
                originalValue = newVal;
                return newVal;
            }
            else
            {
                return originalValue;
            }
        }

        public void initValue(int value)
        {
            this.textBox1.Text = value.ToString();
            this.originalValue = value;
            this.filter.propertyChanged = false;
            checkDefault();
        }
        private void checkDefault()
        {
            if (this.filter != null)
            {
                this.filter.propertyNotDefault = this.textBox1.Text != this.defaultValue.ToString();
            }
        }

        public void resetToDefault()
        {
            this.textBox1.Text = defaultValue.ToString();
        }

        private void textChanged(object sender, EventArgs e)
        {
            if (this.textBox1.Text != originalValue.ToString())
            {
                if (this.filter != null)  // Filter is (conveniently) null during initialization.
                    this.filter.propertyChanged = true;
            }
            else
            {
                if (this.filter != null)
                    this.filter.propertyChanged = false;
            }
            checkDefault();

            if (this.filter != null)
                parent.updateChangedState(this.filter.propertyChanged, this.propertyId, this.changeRequiresRestart);
       }

        public void onSave()
        {
            this.filter.propertyChanged = false;
            this.originalValue = this.getValue();
        }

        internal void ReApplyTheme()
        {
            if (DarkModeForms.DarkModeCS.IsDarkModeCSEnabled)
                this.BackColor = this.label1.BackColor = this.parent.BackColor;
        }
    }
}
