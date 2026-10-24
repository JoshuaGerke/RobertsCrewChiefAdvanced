using CrewChiefV4.VirtualReality;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Valve.VR;

namespace CrewChiefV4
{
    public partial class VROverlaySettings : Form
    {
        public static object instanceLock = new object();
        VROverlayConfiguration _Config = null;
        private bool loadingSettings = true;

        public VROverlaySettings(VROverlayConfiguration config)
        {
            _Config = config;
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainWindow));
            this.SuspendLayout();
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));

            StartPosition = FormStartPosition.CenterParent;
            InitializeComponent();

            labelAvailableWindows.Text = Configuration.getUIString("available_windows_label");
            buttonSaveSettings.Text = Configuration.getUIString("save_changes");
            buttonSaveSettings.Enabled = false;
            checkBoxEnabled.Text = Configuration.getUIString("enable_in_vr");
            groupBoxPosition.Text = Configuration.getUIString("vr_overlay_position");
            labelPositionX.Text = Configuration.getUIString("vr_position_x");
            labelPositionY.Text = Configuration.getUIString("vr_position_y");
            labelPositionZ.Text = Configuration.getUIString("vr_position_z");

            groupBoxRotation.Text = Configuration.getUIString("vr_overlay_rotation");
            labelRotationX.Text = Configuration.getUIString("vr_rotation_x");
            labelRotationY.Text = Configuration.getUIString("vr_rotation_y");
            labelRotationZ.Text = Configuration.getUIString("vr_rotation_z");

            groupBoxScaleTransCurve.Text = Configuration.getUIString("vr_overlay_scale_trans_curve");
            labelSale.Text = Configuration.getUIString("vr_scale");
            labelTransparency.Text = Configuration.getUIString("vr_transparency");
            labelCurvature.Text = Configuration.getUIString("vr_curvature");

            checkBoxEnableGazeing.Text = Configuration.getUIString("vr_enable_gazing");
            labelGazeScale.Text = Configuration.getUIString("vr_gaze_scale");
            labelGazeTransparency.Text = Configuration.getUIString("vr_gaze_transparency");

            checkBoxForceTopMostWindow.Text = Configuration.getUIString("vr_force_topmost_window");

            labelTrackingSpace.Text = Configuration.getUIString("vr_tracking_space");
            comboBoxTrackingSpace.Items.Add(Configuration.getUIString("vr_tracking_space_seated"));
            comboBoxTrackingSpace.Items.Add(Configuration.getUIString("vr_tracking_space_standing"));
            comboBoxTrackingSpace.Items.Add(Configuration.getUIString("vr_tracking_space_followhead"));

            labelToggleKey.Text = Configuration.getUIString("vr_toggle_key");

            groupBoxTrackingUniverse.Text = Configuration.getUIString("vr_tracking_universe_overwrite");
            buttonReCenter.Text = Configuration.getUIString("vr_recenter_pose");

            checkBoxTransparentBackground.Text = Configuration.getUIString("vr_transparent_background");
            label1.Text = Configuration.getUIString("vr_transparent_color");
            lblTransparentTolerance.Text = Configuration.getUIString("vr_transparent_tolerance");

            lblTransparentColor.Enabled = false;
            txtTransparentColor.Enabled = false;
            numericTransparencyTolerance.Enabled = false;
            txtTransparentColor.ValidatingType = typeof(HexColorValidator);

            // if the Config contains hotkeys, then overwrite the default
            if (_Config.HotKeys?.Count > 0)
            {
                HotKeyMapping.HotKeys = _Config.HotKeys;
            }

            SetToolTips();

            foreach (var key in Enum.GetValues(typeof(Keys)))
            {
                comboBoxToggleVirtualKeys.Items.Add(new VirtualKeyMap((Keys)key));
                comboBoxModifierKeys.Items.Add(new VirtualKeyMap((Keys)key));
            }

            comboBoxSetTrackingSpace.DropDownStyle = ComboBoxStyle.DropDownList;
            var trackingSpaces = Enum.GetValues(typeof(ETrackingUniverseOrigin));
            foreach (var trackingSpace in trackingSpaces)
            {
                comboBoxSetTrackingSpace.Items.Add(new TrackingUniverseMap((ETrackingUniverseOrigin)trackingSpace));
            }
            List<TrackingUniverseMap> trackList = comboBoxSetTrackingSpace.Items.OfType<TrackingUniverseMap>().ToList();
            var currentSpace = trackList.FirstOrDefault(t => t.ToString() == OpenVR.Compositor.GetTrackingSpace().ToString());
            comboBoxSetTrackingSpace.SelectedItem = currentSpace;

            updateWindowList();

            this.KeyPreview = true;
            this.KeyDown += VROverlaySettings_KeyDown;
            this.Shown += (s, e) => BringToFront();

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void VROverlaySettings_KeyDown(object sender, KeyEventArgs e)
        {
            if (txtTransparentColor.Focused)
            {
                return;
            }

            var hotKey = HotKeyMapping.Find(e);
            if (hotKey != null)
            {
                hotKey.Invoke(this, e);
                return;
            }

            if (e.KeyCode == Keys.Escape)
                this.Close();
        }

        void updateWindowList()
        {
            List<IntPtr> windows = new List<IntPtr>();
            // hack! screens dont contain a hwnd so make one up and hope it dont collide with an existing hwnd
            IntPtr scrVirtWnd = IntPtr.Zero;
            foreach (var screen in Screen.AllScreens)
            {
                windows.Add(scrVirtWnd);
                scrVirtWnd = IntPtr.Subtract(scrVirtWnd, 1);
            }
            windows.AddRange(Win32Stuff.FindWindows());
            VROverlayWindow[] currentItems = listBoxWindows.Items.OfType<VROverlayWindow>().ToArray();
            var newWindows = windows.Where(wnd => !currentItems.Any(cu => cu.hWnd == wnd));
            var removedWindows = currentItems.Where(ws => !windows.Any(wnd => wnd == ws.hWnd));
            var showSettingsAsOverlay = UserSettings.GetUserSettings().getBoolean("show_vr_settings_as_overlay");
            foreach (var wnd in newWindows)
            {
                bool isDisplay = false;
                string windowName = Win32Stuff.GetWindowText(wnd);

                if (wnd == IntPtr.Subtract(IntPtr.Zero, 1) || wnd == IntPtr.Zero)
                {
                    windowName = Screen.AllScreens[Math.Abs((int)wnd)].DeviceName;
                    isDisplay = true;
                }

                var existingWindowFromConfig = _Config.Windows == null ? null : _Config.Windows.FirstOrDefault(s => s.Name == windowName);
                if (existingWindowFromConfig != null)
                {
                    bool isVRForm = existingWindowFromConfig.Text == this.Text;
                    LoadingBlock(() =>
                    {
                        // always enable this window in VR, unless opted out.                                                
                        if (isVRForm)
                        {
                            existingWindowFromConfig.enabled = showSettingsAsOverlay;
                        }

                        listBoxWindows.Items.Add(new VROverlayWindow(wnd, existingWindowFromConfig) { isDisplay = isDisplay });
                    });

                    if (isVRForm)
                    {
                        // move the cursor over this form so its easy to find in VR
                        MoveCursor();
                    }
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(windowName))
                    {
                        var scr = new VROverlayWindow(windowName, wnd, isDisplay: isDisplay, enabled: windowName == this.Text && showSettingsAsOverlay, wasEnabled: windowName == this.Text && showSettingsAsOverlay);
                        listBoxWindows.Items.Add(scr);
                        if (windowName == this.Text)
                        {
                            // move the cursor over this form so its easy to find in VR
                            MoveCursor();
                        }
                    }
                }
            }

            foreach (var rm in removedWindows)
            {
                if (rm == listBoxWindows.SelectedItem)
                {
                    listBoxWindows.SelectedIndex = listBoxWindows.Items.Count == 1 ? -1 : 0;
                }
                rm.Dispose();
                listBoxWindows.Items.Remove(rm);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            foreach (var window in listBoxWindows.Items.OfType<VROverlayWindow>().Where(w => w.IsSelected))
            {
                window.IsSelected = false;
            }
            base.OnClosed(e);
        }

        private void VROverlay_Load(object sender, EventArgs e)
        {
            LoadingBlock(() =>
            {
                // updated a bit faster if form is showing
                timer5sec.Interval = 1000;
                if (listBoxWindows.Items.Count > 0)
                    listBoxWindows.SelectedIndex = 0;
            });
        }

        // call this in a seperate thread to ensure this window gets removed from the list, this is needed as listBoxWindows hold a handle to this window for 5 sec after we close it.
        // resulting in overlay creation error if user opens this window again within the 5 sec of closing it.
        private void PostCloseUpdateWindows()
        {
            Thread.Sleep(200);
            lock (instanceLock)
            {
                updateWindowList();
            }
        }

        private void VROverlayController_FormClosing(object sender, FormClosingEventArgs e)
        {

            //Cursor.Clip = default(System.Drawing.Rectangle);

            new Thread(PostCloseUpdateWindows).Start();
            // limit update rate to 5 sec interval
            timer5sec.Interval = 5000;
        }

        private void timer5sec_Tick(object sender, EventArgs e)
        {
            lock (instanceLock)
            {
                updateWindowList();
            }
        }

        private void listBoxWindows_SelectedIndexChanged(object sender, EventArgs e)
        {
            lock (instanceLock)
            {
                if (listBoxWindows.SelectedIndex != -1)
                {
                    // Let child controls know that this is not value set via UI interaction.
                    LoadingBlock(() =>
                    {
                        foreach (var w in listBoxWindows.Items.OfType<VROverlayWindow>().Where(w => w.IsSelected))
                        {
                            w.IsSelected = false;
                        }

                        var window = ((VROverlayWindow)listBoxWindows.SelectedItem);

                        window.IsSelected = true;
                        checkBoxEnabled.Checked = window.enabled;
                        checkBoxEnableGazeing.Checked = window.gazeEnabled;

                        checkBoxForceTopMostWindow.Checked = window.forceTopMost;

                        trackBarPositionX.Value = (int)(window.positionX * 100);
                        trackBarPositionY.Value = (int)(window.positionY * 100);
                        trackBarPositionZ.Value = (int)(window.positionZ * 100);

                        textBoxLeftRight.Text = window.positionX.ToString("0.000");
                        textBoxUpDown.Text = window.positionY.ToString("0.000");
                        textBoxDistance.Text = window.positionZ.ToString("0.000");

                        trackBarRotationX.Value = (int)(window.rotationX * MathUtil.Rad2Deg);
                        trackBarRotationY.Value = (int)(window.rotationY * MathUtil.Rad2Deg);
                        trackBarRotationZ.Value = (int)(window.rotationZ * MathUtil.Rad2Deg);

                        textBoxRotationX.Text = trackBarRotationX.Value.ToString("0.0");
                        textBoxRotationY.Text = trackBarRotationY.Value.ToString("0.0");
                        textBoxRotationZ.Text = trackBarRotationZ.Value.ToString("0.0");

                        trackBarScale.Value = (int)(window.scale * 100);
                        trackBarTransparency.Value = (int)(window.transparency * 100);
                        trackBarCurvature.Value = (int)(window.curvature * 100);

                        textBoxScale.Text = window.scale.ToString("0.00");
                        textBoxTransparency.Text = window.transparency.ToString("0.00");
                        textBoxCurvature.Text = window.curvature.ToString("0.00");

                        trackBarGazeScale.Value = (int)(window.gazeScale * 10);
                        trackBarGazeTransparency.Value = (int)(window.gazeTransparency * 100);

                        textBoxGazeScale.Text = window.gazeScale.ToString("0.0");
                        textBoxGazeTransparency.Text = window.gazeTransparency.ToString("0.00");

                        comboBoxTrackingSpace.SelectedIndex = (int)window.trackingSpace;

                        checkBoxTransparentBackground.Checked = window.Chromakey;
                        txtTransparentColor.Text = window.ChromakeyColor;
                        numericTransparencyTolerance.Value = (decimal)window.ChromakeyTolerance;
                        lblTransparentColor.BackColor = System.Drawing.ColorTranslator.FromHtml(window.ChromakeyColor);

                        comboBoxToggleVirtualKeys.SelectedIndex = window.toggleVKeyCode == -1
                            ? 0
                            : comboBoxToggleVirtualKeys.FindString(((Keys)window.toggleVKeyCode).ToString());

                        comboBoxModifierKeys.SelectedIndex = window.modifierVKeyCode == -1
                            ? 0
                            : comboBoxModifierKeys.FindString(((Keys)window.modifierVKeyCode).ToString());

                    });
                }
            }
        }

        private void ResetPosition()
        {
            if (listBoxWindows.SelectedIndex != -1)
            {
                trackBarPositionX.Value = 0;
                trackBarPositionY.Value = 0;
                trackBarPositionZ.Value = -100;
                trackBarRotationX.Value = 0;
                trackBarRotationY.Value = 0;
                trackBarRotationZ.Value = 0;
                trackBarScale.Value = 100;
            }
        }

        private void LoadingBlock(Action action)
        {
            try
            {
                this.loadingSettings = true;
                action();
            }
            finally
            {
                this.loadingSettings = false;
            }
        }

        private void SyncronizedUpdate(Action<VROverlayWindow> selectedItemAction)
        {
            lock (instanceLock)
            {
                if (listBoxWindows.SelectedIndex != -1)
                {
                    if (!loadingSettings)
                        buttonSaveSettings.Enabled = true;

                    selectedItemAction(((VROverlayWindow)listBoxWindows.SelectedItem));
                }
            }
        }

        private void trackBarPositionX_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.positionX = (trackBarPositionX.Value * 0.01f);
                textBoxLeftRight.Text = window.positionX.ToString("0.000");
            });
        }

        private void trackBarPositionY_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.positionY = (trackBarPositionY.Value * 0.01f);
                textBoxUpDown.Text = window.positionY.ToString("0.000");
            });
        }

        private void trackBarPositionZ_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.positionZ = (trackBarPositionZ.Value * 0.01f);
                textBoxDistance.Text = window.positionZ.ToString("0.000");
            });
        }

        private void buttonSaveSettings_Click(object sender, EventArgs e)
        {
            lock (instanceLock)
            {
                this.buttonSaveSettings.Enabled = false;

                // find the windows that the user interacted with
                // also try to match with an existing instance from the config 
                if (_Config.Windows != null)
                {
                    var currWasEnabled = listBoxWindows.Items.OfType<VROverlayWindow>()
                                                             .Where(s => s.wasEnabled)
                                                             .Select(currentWindow => new { currentWindow, windowFromConfig = _Config.Windows.FirstOrDefault(w => w.Text == currentWindow.Text) })
                                                             .ToList();

                    foreach (var pair in currWasEnabled)
                    {
                        // add new windows
                        if (pair.windowFromConfig == null)
                        {
                            _Config.Windows.Add(pair.currentWindow);
                        }
                        else
                        {
                            // otherwise update existing
                            Debug.Assert(pair.currentWindow.Name == pair.windowFromConfig.Name);
                            pair.windowFromConfig.Copy(pair.currentWindow);
                        }
                    }
                }

                _Config.Save();
            }
        }

        private void checkBoxEnabled_CheckedChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.enabled = checkBoxEnabled.Checked;

                if (window.enabled && window.vrOverlayHandle == 0)
                    window.CreateOverlay();
                if (!window.enabled)
                    window.shouldDraw = false;

                window.SetOverlayEnabled(window.enabled);
                window.SetOverlayCurvature();
                window.SetOverlayTransparency();

                // If window was ever enabled, keep it in the settings.
                window.wasEnabled = window.wasEnabled || window.enabled;
            });
        }

        private void trackBarRotationX_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.rotationX = (trackBarRotationX.Value * MathUtil.Deg2Rad);
                textBoxRotationX.Text = trackBarRotationX.Value.ToString("0.0");
            });
        }

        private void trackBarRotationY_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.rotationY = (trackBarRotationY.Value * MathUtil.Deg2Rad);
                textBoxRotationY.Text = trackBarRotationY.Value.ToString("0.0");
            });
        }

        private void trackBarRotationZ_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.rotationZ = (trackBarRotationZ.Value * MathUtil.Deg2Rad);
                textBoxRotationZ.Text = trackBarRotationZ.Value.ToString("0.0");
            });
        }

        private void trackBarScale_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.scale = (trackBarScale.Value * 0.01f);
                textBoxScale.Text = window.scale.ToString("0.00");
            });
        }

        private void trackBarTransparency_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.transparency = (trackBarTransparency.Value * 0.01f);
                textBoxTransparency.Text = window.transparency.ToString("0.00");
                window.SetOverlayTransparency();
            });
        }

        private void trackBarCurvature_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.curvature = (trackBarCurvature.Value * 0.01f);
                textBoxCurvature.Text = window.curvature.ToString("0.00");
                window.SetOverlayCurvature();
            });
        }
        private void MoveCursor()
        {
            this.Cursor = new Cursor(Cursor.Current.Handle);
            Cursor.Position = new System.Drawing.Point(this.Location.X + 50, this.Location.Y + 50);
            //Cursor.Clip = new System.Drawing.Rectangle(this.Location, this.Size);
        }

        private void checkBoxEnableGazeing_CheckedChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window => window.gazeEnabled = checkBoxEnableGazeing.Checked);
        }

        private void trackBarGazeScale_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.gazeScale = (trackBarGazeScale.Value * 0.1f);
                textBoxGazeScale.Text = window.gazeScale.ToString("0.00");
            });

        }

        private void trackBarGazeTransparency_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.gazeTransparency = (trackBarGazeTransparency.Value * 0.01f);
                textBoxGazeTransparency.Text = window.gazeTransparency.ToString("0.00");
            });
        }

        private void checkBoxForceTopMostWindow_CheckedChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window => window.forceTopMost = checkBoxForceTopMostWindow.Checked);
        }

        private void comboBoxTrackingSpace_SelectedIndexChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.trackingSpace = comboBoxTrackingSpace.SelectedIndex != -1
                                        ? (TrackingSpace)comboBoxTrackingSpace.SelectedIndex
                                        : TrackingSpace.Seated;
            });
        }

        private void comboBoxVirtualKeys_SelectedIndexChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.toggleVKeyCode = comboBoxToggleVirtualKeys.SelectedIndex != -1 || comboBoxToggleVirtualKeys.SelectedIndex == 0
                                            ? (int)((VirtualKeyMap)comboBoxToggleVirtualKeys.SelectedItem).keyCode
                                            : -1;
            });
        }

        private void comboBoxModifierKeys_SelectedIndexChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.modifierVKeyCode = comboBoxModifierKeys.SelectedIndex != -1 || comboBoxModifierKeys.SelectedIndex == 0
                                            ? (int)((VirtualKeyMap)comboBoxModifierKeys.SelectedItem).keyCode
                                            : -1;
            });
        }

        private void checkBoxTransparentBackground_CheckedChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                window.Chromakey = checkBoxTransparentBackground.Checked;
                lblTransparentColor.Enabled = checkBoxTransparentBackground.Checked;
                txtTransparentColor.Enabled = checkBoxTransparentBackground.Checked;
                numericTransparencyTolerance.Enabled = checkBoxTransparentBackground.Checked;
            });

        }

        private void txtTransparentColor_TextChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                // JB: when updating the colour text in the UI also update the ChromakeyColor field value if the input data is a valid hex colour
                if (HexColorValidator.TryParse(txtTransparentColor.Text, out HexColorValidator hex))
                {
                    lblTransparentColor.BackColor = hex.Color;
                    window.ChromakeyColor = txtTransparentColor.Text;
                }
                else
                {
                    lblTransparentColor.BackColor = System.Drawing.Color.Transparent;
                    window.ChromakeyColor = "#000000";
                }
            });
        }

        private void txtTransparentColor_TypeValidationCompleted(object sender, TypeValidationEventArgs e)
        {
            SyncronizedUpdate(window =>
            {
                if (e.IsValidInput)
                {
                    lblTransparentColor.BackColor = ((HexColorValidator)e.ReturnValue).Color;
                }
                else
                {
                    lblTransparentColor.BackColor = System.Drawing.Color.Transparent;
                }
            });
        }

        private void numericTransparencyTolerance_ValueChanged(object sender, EventArgs e)
        {
            SyncronizedUpdate(window => window.ChromakeyTolerance = (float)numericTransparencyTolerance.Value);
        }

        private void comboBoxSetTrackingSpace_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxSetTrackingSpace.SelectedIndex == -1)
                return;
            lock (instanceLock)
            {
                OpenVR.Compositor.SetTrackingSpace(((TrackingUniverseMap)comboBoxSetTrackingSpace.SelectedItem).eTrackingUniverse);
            }
        }

        private void buttonReCenter_Click(object sender, EventArgs e)
        {
            OpenVR.Chaperone.ResetZeroPose(((TrackingUniverseMap)comboBoxSetTrackingSpace.SelectedItem).eTrackingUniverse);
        }

        private void SetToolTips()
        {
            HotKeyMapping.SetToolTips(new[]
            {
                nameof(HotKeyMapping.Functions.INCREMENT_X_POSITION),
                nameof(HotKeyMapping.Functions.DECREMENT_X_POSITION),
                nameof(HotKeyMapping.Functions.INCREMENT_Y_POSITION),
                nameof(HotKeyMapping.Functions.DECREMENT_Y_POSITION),
                nameof(HotKeyMapping.Functions.INCREMENT_Z_POSITION),
                nameof(HotKeyMapping.Functions.DECREMENT_Z_POSITION)
            }, toolTip1, groupBoxPosition);

            HotKeyMapping.SetToolTips(new[]
            {
                nameof(HotKeyMapping.Functions.INCREMENT_X_ROTATION),
                nameof(HotKeyMapping.Functions.DECREMENT_X_ROTATION),
                nameof(HotKeyMapping.Functions.INCREMENT_Y_ROTATION),
                nameof(HotKeyMapping.Functions.DECREMENT_Y_ROTATION),
                nameof(HotKeyMapping.Functions.INCREMENT_Z_ROTATION),
                nameof(HotKeyMapping.Functions.DECREMENT_Z_ROTATION)
            }, toolTip1, groupBoxRotation);

            HotKeyMapping.SetToolTips(new[]
            {
                nameof(HotKeyMapping.Functions.INCREMENT_SCALE),
                nameof(HotKeyMapping.Functions.DECREMENT_SCALE),
                nameof(HotKeyMapping.Functions.INCREMENT_TRANSPARENCY),
                nameof(HotKeyMapping.Functions.DECREMENT_TRANSPARENCY)
            }, toolTip1, groupBoxScaleTransCurve);

            HotKeyMapping.SetToolTips(new[]
            {
                nameof(HotKeyMapping.Functions.NEXT_VISIBLE_OVERLAY),
                nameof(HotKeyMapping.Functions.PREVIOUS_VISIBLE_OVERLAY)
            }, toolTip1, listBoxWindows);
        }

        public class HexColorValidator
        {
            public System.Drawing.Color Color { get; set; }
            // the following function is needed to used to verify the input
            public static HexColorValidator Parse(string str)
            {
                if (str[0] == '#' && str.Length == 7 && str.Skip(1).All(char.IsLetterOrDigit))
                    return new HexColorValidator { Color = System.Drawing.ColorTranslator.FromHtml(str) };

                throw new FormatException("Invalid Format");
            }

            public static bool TryParse(string str, out HexColorValidator hex)
            {
                try
                {
                    hex = Parse(str);
                    return true;
                }
                catch (Exception)
                {
                    hex = null;
                    return false;
                }
            }
        }

        private class TrackingUniverseMap
        {
            public ETrackingUniverseOrigin eTrackingUniverse;
            public TrackingUniverseMap(ETrackingUniverseOrigin eTrackingUniverse)
            {
                this.eTrackingUniverse = eTrackingUniverse;
            }
            public override string ToString()
            {
                return eTrackingUniverse.ToString();
            }
        }

        private class VirtualKeyMap
        {
            public Keys keyCode;
            public VirtualKeyMap(Keys keyCode)
            {
                this.keyCode = keyCode;
            }

            public override string ToString()
            {
                return keyCode.ToString();
            }
        }

        [DebuggerDisplay("Id:{Id} Key: {Key} Modifier: {Modifier}")]
        public class HotKeyMapping
        {
            public string Id { get; set; }
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public Keys Key { get; set; } = Keys.None;
            [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
            public Keys Modifier { get; set; } = Keys.None;

            public static List<HotKeyMapping> HotKeys { get; set; } = new List<HotKeyMapping>();

            static HotKeyMapping()
            {
            }

            public static void SetToolTips(IEnumerable<string> ids, ToolTip toolTip, Control target)
            {
                var positionHotKeys = ids.Where(id => HotKeys.Any(m => m.Id == id)).ToArray();

                if (positionHotKeys.Length > 0)
                {
                    toolTip.SetToolTip(target, BuildHelpText(positionHotKeys));
                }
            }

            public static string BuildHelpText(Func<VROverlaySettings, KeyEventArgs, bool> func)
            {
                return BuildHelpText(func.Method.Name);
            }

            public static string BuildHelpText(params string[] ids)
            {
                return ids
                    .Select(Find)
                    .Aggregate(new StringBuilder(), (s, m) => s.AppendLine(m.GetHelpText()))
                    .ToString();
            }

            public static HotKeyMapping Find(Func<VROverlaySettings, KeyEventArgs, bool> func)
            {
                return Find(func.Method.Name);
            }

            public static HotKeyMapping Find(string id)
            {
                return HotKeys.FirstOrDefault(m => m.Id == id);
            }

            public static HotKeyMapping Find(KeyEventArgs e)
            {
                return HotKeys.FirstOrDefault(m => m.Key == e.KeyCode && e.Modifiers == m.Modifier);
            }

            public string GetHelpText()
            {
                string mod = Modifier != Keys.None ? $"{Modifier} + " : string.Empty;
                return $"{Configuration.getUIString("vr_" + Id.ToLower())}\t\t{mod}{Key}";
            }

            public bool Invoke(VROverlaySettings window, KeyEventArgs e)
            {
                bool result = Functions.Invoke(Id, window, e);
                e.Handled = result;
                e.SuppressKeyPress = result;
                return result;
            }

            public bool IsValid()
            {
                return Functions.All().Any(f => f == Id);
            }

            public static IEnumerable<HotKeyMapping> Default()
            {
                yield return new HotKeyMapping { Id = nameof(Functions.DECREMENT_X_POSITION), Key = Keys.A, };
                yield return new HotKeyMapping { Id = nameof(Functions.INCREMENT_X_POSITION), Key = Keys.D, };
                yield return new HotKeyMapping { Id = nameof(Functions.INCREMENT_Y_POSITION), Key = Keys.W, };
                yield return new HotKeyMapping { Id = nameof(Functions.DECREMENT_Y_POSITION), Key = Keys.S, };
                yield return new HotKeyMapping { Id = nameof(Functions.DECREMENT_Z_POSITION), Key = Keys.Q, };
                yield return new HotKeyMapping { Id = nameof(Functions.INCREMENT_Z_POSITION), Key = Keys.Z, };
                yield return new HotKeyMapping { Id = nameof(Functions.DECREMENT_X_ROTATION), Key = Keys.W, Modifier = Keys.Shift, };
                yield return new HotKeyMapping { Id = nameof(Functions.INCREMENT_X_ROTATION), Key = Keys.S, Modifier = Keys.Shift, };
                yield return new HotKeyMapping { Id = nameof(Functions.DECREMENT_Y_ROTATION), Key = Keys.A, Modifier = Keys.Shift, };
                yield return new HotKeyMapping { Id = nameof(Functions.INCREMENT_Y_ROTATION), Key = Keys.D, Modifier = Keys.Shift, };
                yield return new HotKeyMapping { Id = nameof(Functions.DECREMENT_Z_ROTATION), Key = Keys.Q, Modifier = Keys.Shift, };
                yield return new HotKeyMapping { Id = nameof(Functions.INCREMENT_Z_ROTATION), Key = Keys.Z, Modifier = Keys.Shift, };
                yield return new HotKeyMapping { Id = nameof(Functions.INCREMENT_SCALE), Key = Keys.Oemplus, };
                yield return new HotKeyMapping { Id = nameof(Functions.DECREMENT_SCALE), Key = Keys.OemMinus };
                yield return new HotKeyMapping { Id = nameof(Functions.DECREMENT_TRANSPARENCY), Key = Keys.Oemcomma, };
                yield return new HotKeyMapping { Id = nameof(Functions.INCREMENT_TRANSPARENCY), Key = Keys.OemPeriod, };
                yield return new HotKeyMapping { Id = nameof(Functions.RESET_OVERLAY_POSITION_AND_ROTATION), Key = Keys.R, Modifier = Keys.Control, };
                yield return new HotKeyMapping { Id = nameof(Functions.PREVIOUS_VISIBLE_OVERLAY), Key = Keys.OemCloseBrackets, };
                yield return new HotKeyMapping { Id = nameof(Functions.PREVIOUS_VISIBLE_OVERLAY), Key = Keys.Space, Modifier = Keys.Control, };
                yield return new HotKeyMapping { Id = nameof(Functions.NEXT_VISIBLE_OVERLAY), Key = Keys.OemOpenBrackets, };
                yield return new HotKeyMapping { Id = nameof(Functions.NEXT_VISIBLE_OVERLAY), Key = Keys.Space, };
            }

            public class Functions
            {
                static string[] ValidFunctions = null;
                static Functions()
                {
                    ValidFunctions = typeof(Functions)
                                       .GetMethods()
                                       .Where(m =>
                                       {
                                           var prams = m.GetParameters();
                                           return m.ReturnType == typeof(bool)
                                               && m.IsPublic
                                               && prams.Length == 2
                                               && prams[0].ParameterType == typeof(VROverlaySettings)
                                               && prams[1].ParameterType == typeof(KeyEventArgs);
                                       })
                                       .Select(m => m.Name).ToArray();
                }

                public static bool Invoke(string key, VROverlaySettings window, KeyEventArgs e)
                {
                    return (bool)typeof(Functions).GetMethod(key).Invoke(null, new object[] { window, e });
                }

                public static IEnumerable<string> All()
                {
                    return ValidFunctions;
                }

                private static bool Add(TrackBar bar, int valueToAdd)
                {
                    var value = bar.Value + valueToAdd;
                    if (value <= bar.Maximum && value >= bar.Minimum)
                    {
                        bar.Value = value;
                    }
                    return true;
                }

                public static bool DECREMENT_X_POSITION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarPositionX, -1);
                public static bool INCREMENT_X_POSITION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarPositionX, 1);
                public static bool DECREMENT_Y_POSITION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarPositionY, -1);
                public static bool INCREMENT_Y_POSITION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarPositionY, 1);
                public static bool DECREMENT_Z_POSITION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarPositionZ, -1);
                public static bool INCREMENT_Z_POSITION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarPositionZ, 1);
                public static bool DECREMENT_X_ROTATION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarRotationX, -1);
                public static bool INCREMENT_X_ROTATION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarRotationX, 1);
                public static bool DECREMENT_Y_ROTATION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarRotationY, -1);
                public static bool INCREMENT_Y_ROTATION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarRotationY, 1);
                public static bool DECREMENT_Z_ROTATION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarRotationZ, -1);
                public static bool INCREMENT_Z_ROTATION(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarRotationZ, 1);
                public static bool DECREMENT_SCALE(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarScale, -1);
                public static bool INCREMENT_SCALE(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarScale, 1);
                public static bool DECREMENT_TRANSPARENCY(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarTransparency, - 1);
                public static bool INCREMENT_TRANSPARENCY(VROverlaySettings window, KeyEventArgs e) => Add(window.trackBarTransparency, 1);
                public static bool RESET_OVERLAY_POSITION_AND_ROTATION(VROverlaySettings window, KeyEventArgs e) { window.ResetPosition(); return true; }
                public static bool PREVIOUS_VISIBLE_OVERLAY(VROverlaySettings window, KeyEventArgs e)
                {
                    var next = window.listBoxWindows.SelectedItem == null
                        ? window.listBoxWindows.Items.OfType<VROverlayWindow>().FirstOrDefault(w => w.enabled)
                        : window.listBoxWindows.Items.OfType<VROverlayWindow>()
                                                  .Cyclic(2)
                                                  .SkipWhile(w => !w.IsSelected)
                                                  .Skip(1)
                                                  .FirstOrDefault(w => w.enabled);

                    if (next != null)
                    {
                        window.listBoxWindows.SelectedItem = next;
                        return true;
                    }
                    return false;
                }

                public static bool NEXT_VISIBLE_OVERLAY(VROverlaySettings window, KeyEventArgs e)
                {
                    var next = window.listBoxWindows.SelectedItem == null
                        ? window.listBoxWindows.Items.OfType<VROverlayWindow>().FirstOrDefault(w => w.enabled)
                        : window.listBoxWindows.Items.OfType<VROverlayWindow>()
                                                  .Reverse()
                                                  .Cyclic(2)
                                                  .SkipWhile(w => !w.IsSelected)
                                                  .Skip(1)
                                                  .FirstOrDefault(w => w.enabled);

                    if (next != null)
                    {
                        window.listBoxWindows.SelectedItem = next;
                        return true;
                    }
                    return false;
                }
            }
        }
    }
}
