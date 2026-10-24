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
    public partial class BooleanPropertyControl : UserControl
    {
        public bool changeRequiresRestart;
        public String propertyId;
        public String label;
        public Boolean defaultValue;
        public Boolean originalValue;
        private PropertiesForm parent;
        internal PropertyFilter filter = null;

        public BooleanPropertyControl(String propertyId, String label, Boolean value, Boolean defaultValue, String helpText, String filterText, 
            String categoryText, bool changeRequiresRestart, PropertiesForm parent)
        {
            InitializeComponent();
            this.parent = parent;
            this.label = label;
            this.propertyId = propertyId;
            this.originalValue = value;
            this.checkBox1.Text = label;
            this.checkBox1.Checked = value;
            this.defaultValue = defaultValue;
            this.toolTip1.SetToolTip(this.checkBox1, helpText);
            
            this.changeRequiresRestart = changeRequiresRestart;

            // This has to be initialized last.
            this.filter = new PropertyFilter(filterText, categoryText, changeRequiresRestart, propertyId, this.label, helpText);
        }
        public Boolean getValue()
        {
            return this.checkBox1.Checked;
        }

        public void initValue(Boolean value)
        {
            this.checkBox1.Checked = value;
            this.originalValue = value;
            this.filter.propertyChanged = false;
            checkDefault();
        }
        private void checkDefault()
        {
            if (this.filter != null)
            {
                this.filter.propertyNotDefault = this.checkBox1.Checked != this.defaultValue;
            }
        }
        public void resetToDefault()
        {
            this.checkBox1.Checked = defaultValue;
        }

        private void checkedChanged(object sender, EventArgs e)
        {
            if (this.originalValue != this.checkBox1.Checked)
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
                this.BackColor = this.checkBox1.BackColor = this.parent.BackColor;
        }
    }
}
