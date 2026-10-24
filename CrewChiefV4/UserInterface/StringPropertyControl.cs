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
    public partial class StringPropertyControl : UserControl
    {
        public bool changeRequiresRestart;
        public String propertyId;
        public String defaultValue;
        public String originalValue;
        public String label;
        private PropertiesForm parent;
        internal PropertyFilter filter = null;
        public bool changed = false;

        public StringPropertyControl(String propertyId, String label, String currentValue, String defaultValue, String helpText, String filterText, 
            String categoryText, bool changeRequiresRestart, PropertiesForm parent)
        {
            InitializeComponent();
            this.parent = parent;
            this.label = label;
            this.propertyId = propertyId;
            this.label1.Text = label;
            this.originalValue = currentValue;
            this.textBox1.Text = currentValue;
            this.defaultValue = defaultValue;
            this.toolTip1.SetToolTip(this.textBox1, helpText);
            this.toolTip1.SetToolTip(this.label1, helpText);

            this.changeRequiresRestart = changeRequiresRestart;

            // This has to be initialized last.
            this.filter = new PropertyFilter(filterText, categoryText, changeRequiresRestart, propertyId, this.label, helpText);
        }

        public String getValue()
        {
            return this.textBox1.Text;
        }

        public void initValue(String value)
        {
            this.textBox1.Text = value;
            this.originalValue = value;
            this.filter.propertyChanged = false;
            checkDefault();
        }
        private void checkDefault()
        {
            if (this.filter != null)
            {
                this.filter.propertyNotDefault = this.defaultValue != this.textBox1.Text;
            }
        }

        public void resetToDefault()
        {
            this.textBox1.Text = defaultValue;
        }

        private void textChanged(object sender, EventArgs e)
        {
            if (this.textBox1.Text != originalValue)
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
