#if PIT_MANAGER_DEBUG
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using CrewChiefV4.RF2;
using PitMenuAPI;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CrewChiefV4.UserInterface.VMs
{
    internal class PitMenuDebug_VM
    {
        private readonly PitMenuDebug_V view;
        public PitMenuDebug_VM(PitMenuDebug_V _view)
        {
            view = _view;
            view.richTextBox1.Text = "";
            Serilog.Log.Logger = new LoggerConfiguration()
                .WriteTo.Sink(new RichTextBoxSink(view.richTextBox1))
                .CreateLogger();
            Log.logSink = logSink;
        }

        private static void logSink(string log)
        {
            Serilog.Log.Information(log);
        }

        public void WriteMenu(Rf2PitMenuController.PitMenuContents menuDict)
        {
            view.checkedListBoxMenuCategories.Items.Clear();
            foreach (var item in menuDict.Categories)
            {
                view.checkedListBoxMenuCategories.Items.Add(item);
            }
        }

        public void CheckmarkPitCategory(string category)
        {
            int index = -1;
            if (view.checkedListBoxMenuCategories.CheckedIndices.Count > 0)
            {
                index = view.checkedListBoxMenuCategories.CheckedIndices[0];
            }
            if (index != -1)
            {
                view.checkedListBoxMenuCategories.SetItemChecked(index, false);
            }
            index = view.checkedListBoxMenuCategories.Items.IndexOf(category);
            if (index != -1)
            {
                view.checkedListBoxMenuCategories.SetItemChecked(index, true);
            }
        }

        public void SetMfdDropdown(string display)
        {
            if (view.comboBoxMFDs.SelectedItem != null)
            {
                string currentItem = view.comboBoxMFDs.SelectedItem.ToString();
                if (!currentItem.StartsWith(display))
                {
                    foreach (var item in view.comboBoxMFDs.Items)
                    {
                        if (item.ToString().StartsWith(display))
                        {
                            view.comboBoxMFDs.SelectedItem = item;
                        }
                    }
                }
            }
        }

        public void SetChoices(List<string> choices)
        {
            view.listBoxMenuValues.DataSource = choices;
        }

        public void LitresOrGallons(bool gallons)
        {
            if (gallons)
            {
                view.labelFuelEntry.Text = "Enter fuel (in gallons)";
            }
            else
            {
                view.labelFuelEntry.Text = "Enter fuel (in litres)";
            }
        }
    }

    public class RichTextBoxSink : ILogEventSink
    {
        private readonly RichTextBox _richTextBox;
        public RichTextBoxSink(RichTextBox richTextBox)
        {
            _richTextBox = richTextBox;
        }
        public void Emit(LogEvent logEvent)
        {
            if (logEvent == null)
                throw new ArgumentNullException(nameof(logEvent));
            _richTextBox.Invoke(new Action(() =>
            {
                _richTextBox.AppendText(logEvent.RenderMessage() + Environment.NewLine);
                // autoscroll
                _richTextBox.SelectionStart = _richTextBox.Text.Length;
                _richTextBox.ScrollToCaret();
            _richTextBox.Refresh();
            }));
        }
    }

}
#endif