using CrewChiefV4.Events;
using CrewChiefV4.SharedMemory;
using GameOverlay.Drawing;
using GameOverlay.PInvoke;
using GameOverlay.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Cursor = System.Windows.Forms.Cursor;
using Point = System.Windows.Point;

namespace CrewChiefV4.Overlay
{
    public class SubtitleOverlay
    {
        #region overlay settings
        public class SubtitleOverlaySettings : OverlaySettings
        {
            public int maxSubtitlehistory = 4;
            public int windowWidth = 640;
            public new int fontSize = 16;
            public new bool fontBold = true;
            public new int windowX = (Screen.PrimaryScreen.Bounds.Width / 2) - 350;
            public new int windowY = 50;
            public new string activeColorScheme = "CrewChief";
            public bool capitalLetter = false;
            public bool hideControlsOnStartup = false;
            [JsonConverter(typeof(StringEnumConverter))]
            public DisplayVoices displayVoices = DisplayVoices.All;
            [JsonConverter(typeof(StringEnumConverter))]
            public DisplayMode displayMode = DisplayMode.AlwaysOn;
            public int displayTime = 4;

        }
        #endregion
        private readonly GraphicsWindow overlayWindow;
        private Font font;
        private Font fontBold;
        private Font fontControls;
        private SolidBrush fontBrush;
        private SolidBrush backgroundBrush;
        private SolidBrush transparentBrush;
        public static SubtitleOverlaySettings settings = null;
        public static ColorScheme colorScheme = null;
        public static ColorScheme defaultColorScheme = null;
        public static ColorScheme colorSchemeTransparent = null;

        public static bool shown = UserSettings.GetUserSettings().getBoolean("enable_subtitle_overlay");
        public static readonly bool likelyShownInVR = UserSettings.GetUserSettings().getBoolean("enable_vr_overlay_windows");
        private Boolean cleared = true;
        public bool inputsEnabled = false;
        public bool keepWindowActiveBackUpOnClose = false;
        public int windowHeightBackUpOnClose = -1;

        private Dictionary<string, OverlayElement> overlayElements = new Dictionary<string, OverlayElement>();
        static readonly string overlayFileName = "subtitle_overlay.json";

        private string tileBarName = "overlay_titlebar";
        private string subtitleOverlayName = "subtitle_overlay";
        private string displayModeBoxName = "display_mode";

        private OverlayElement titleBar = null;
        private OverlayElement subtitleElement = null;
        private OverlayElement displayModeControlBox = null;

        private int maxDisplayLines = 0;
        private float messuredFontHeight = 0;
        private float subtitleTextBoxHeight = 0;
        private bool shiftKeyReleased = false;
        private IntPtr forgroundWindow = IntPtr.Zero;

        private LinkedList<OverlayElement> linkedTabStopElements = new LinkedList<OverlayElement>();
        private LinkedListNode<OverlayElement> listNodeTabStopElement;
        private static string initialSubtitle = "";

        public enum DisplayVoices : int { All = 0, ChiefOnly, SpotterOnly, YouOnly, ChiefAndSpotter, YouAndChief, YouAndSpotter }

        public enum DisplayMode : int { AlwaysOn = 0, Movie }

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern bool GetKeyboardState(byte[] lpKeyState);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        static extern IntPtr GetKeyboardLayout(int idThread);

        [DllImport("user32.dll")]
        static extern int ToUnicodeEx(uint wVirtKey, uint wScanCode, byte[] lpKeyState, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff, int cchBuff, uint wFlags, IntPtr dwhkl);
        // enable user to move the window
        public SubtitleOverlay()
        {
            // initialize a new Graphics object
            // GraphicsWindow will do the remaining initialization
            settings = OverlaySettings.loadOverlaySetttings<SubtitleOverlaySettings>(overlayFileName);
            if (settings.colorSchemes == null || settings.colorSchemes.Count == 0)
            {
                settings.colorSchemes = new List<ColorScheme>() { OverlaySettings.defaultCrewChiefColorScheme, OverlaySettings.windowsGrayColorScheme, OverlaySettings.transparentColorScheme };
            }
            colorScheme = settings.colorSchemes.FirstOrDefault(s => s.name == settings.activeColorScheme);
            defaultColorScheme = OverlaySettings.defaultCrewChiefColorScheme;
            colorSchemeTransparent = OverlaySettings.transparentColorScheme;
            if (colorScheme == null)
            {
                colorScheme = defaultColorScheme;
            }
            var graphics = new Graphics
            {
                PerPrimitiveAntiAliasing = true,
                TextAntiAliasing = settings.textAntiAliasing,
                UseMultiThreadedFactories = false,
                VSync = settings.vSync,
                WindowHandle = IntPtr.Zero
            };

            // it is important to set the window to visible (and topmost) if you want to see it!
            overlayWindow = new GraphicsWindow(graphics)
            {
                IsTopmost = true,
                IsVisible = true,
                IsAppWindow = UserSettings.GetUserSettings().getBoolean("make_overlay_app_window"),
                FPS = settings.windowFPS,
                X = settings.windowX,
                Y = settings.windowY,
                Width = settings.windowWidth,
                Height = settings.windowHeight,
                Title = "CrewChief Subtitles",
                ClassName = "CrewChief_Subtitles",
            };

            initialSubtitle = settings.capitalLetter ? "Use CTRL + SHIFT to show/hide settings and title bar".ToUpper() :
                        "Use CTRL + SHIFT to show/hide settings and title bar";

            overlayWindow.SetupGraphics += overlayWindow_SetupGraphics;
            overlayWindow.DestroyGraphics += overlayWindow_DestroyGraphics;
            overlayWindow.DrawGraphics += overlayWindow_DrawGraphics;
        }
        public void Dispose()
        {
            // you do not need to dispose the Graphics surface
            overlayWindow.Dispose();
        }

        public void Run()
        {
            // creates the window and setups the graphics
            overlayWindow.StartThread();
        }
        private void overlayWindow_SetupGraphics(object sender, SetupGraphicsEventArgs e)
        {
            var gfx = e.Graphics;

            // creates a simple font with no additional style
            try
            {
                font = gfx.CreateFont(settings.fontName, settings.fontSize, settings.fontBold, settings.fontItalic);
                if (font == null)
                {
                    font = gfx.CreateFont("Arial", 12);
                }
                fontBold = gfx.CreateFont(settings.fontName, settings.fontSize, true, false);
                fontControls = gfx.CreateFont(settings.fontName, 12, false, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing " + settings.fontName + ": " + ex.Message);
            }

            fontBrush = gfx.CreateSolidBrush(colorScheme.fontColor);
            backgroundBrush = gfx.CreateSolidBrush(colorScheme.backgroundColor);
            transparentBrush = gfx.CreateSolidBrush(Color.Transparent);

            maxDisplayLines = settings.maxSubtitlehistory == -1 || settings.maxSubtitlehistory > 10 ? 10 : settings.maxSubtitlehistory;
            messuredFontHeight = font.MeasureString("Hj").Height;
            subtitleTextBoxHeight = messuredFontHeight * (maxDisplayLines);

            titleBar = overlayElements[tileBarName] = new OverlayHeader(gfx, "CrewChief Subtitles", fontBold, new Rect(0, 0, overlayWindow.Width, 20), defaultColorScheme, overlayWindow,
                OnEnableUserInput, OnButtonClosed, initialEnabled: !settings.hideControlsOnStartup);

            linkedTabStopElements.AddLast(titleBar.AddChildElement(new ElementCheckBox(gfx, "Enable input", fontControls, new Rect(135, 3, 14, 14), defaultColorScheme, initialEnabled: false)));
            linkedTabStopElements.AddLast(titleBar.AddChildElement(new ElementButton(gfx, "ButtonClose", font, new Rect(overlayWindow.Width - 18, 3, 14, 14), defaultColorScheme)));

            displayModeControlBox = overlayElements[displayModeBoxName] = new ElementGroupBox(gfx, displayModeBoxName, fontControls, new Rect(0, 20, overlayWindow.Width, 144),
                defaultColorScheme, initialEnableState: !settings.hideControlsOnStartup);

            //voicesBox.AddChildElement()

            displayModeControlBox.AddChildElement(new ElementText(gfx, "Window width", fontControls, new Rect(0, 2, 90, 17), defaultColorScheme, textAlign: TextAlign.Left | TextAlign.Center));

            var lastChild = linkedTabStopElements.AddLast(displayModeControlBox.AddChildElement(new ElementTextBox(gfx, "window_width_textbox", fontControls, new Rect(122, 2, 30, 17),
                defaultColorScheme, text: settings.windowWidth.ToString(), initialEnableState: true, acceptInput: true, maxTextLength: 4, digitsOnly: true, textAlign: TextAlign.Left | TextAlign.Center))).Value;
            lastChild.OnKeyDown += OnKeyDown;
            lastChild.OnElementDraw += OnDrawTextBox;

            displayModeControlBox.AddChildElement(new ElementText(gfx, "Max history", fontControls, new Rect(0, 22, 90, 17), defaultColorScheme, textAlign: TextAlign.Left | TextAlign.Center));
            lastChild = linkedTabStopElements.AddLast(displayModeControlBox.AddChildElement(new ElementTextBox(gfx, "max_history_textbox", fontControls, new Rect(122, 22, 30, 17),
                defaultColorScheme, text: settings.maxSubtitlehistory.ToString(), initialEnableState: true, acceptInput: true, maxTextLength: 2, digitsOnly: true, textAlign: TextAlign.Left | TextAlign.Center))).Value;
            lastChild.OnKeyDown += OnKeyDown;
            lastChild.OnElementDraw += OnDrawTextBox;

            displayModeControlBox.AddChildElement(new ElementText(gfx, "Font size", fontControls, new Rect(0, 42, 90, 17), defaultColorScheme, textAlign: TextAlign.Left | TextAlign.Center));
            lastChild = linkedTabStopElements.AddLast(displayModeControlBox.AddChildElement(new ElementTextBox(gfx, "font_size_textbox", fontControls, new Rect(122, 42, 30, 17),
                defaultColorScheme, text: settings.fontSize.ToString(), initialEnableState: true, acceptInput: true, maxTextLength: 2, digitsOnly: true, textAlign: TextAlign.Left | TextAlign.Center))).Value;
            lastChild.OnKeyDown += OnKeyDown;
            lastChild.OnElementDraw += OnDrawTextBox;

            displayModeControlBox.AddChildElement(new ElementText(gfx, "Display time (seconds)", fontControls, new Rect(0, 62, 90, 17), defaultColorScheme, textAlign: TextAlign.Left | TextAlign.Center));
            lastChild = linkedTabStopElements.AddLast(displayModeControlBox.AddChildElement(new ElementTextBox(gfx, "display_time_textbox", fontControls, new Rect(122, 62, 30, 17),
                defaultColorScheme, text: settings.displayTime.ToString(), initialEnableState: true, acceptInput: true, maxTextLength: 2, digitsOnly: true, textAlign: TextAlign.Left | TextAlign.Center))).Value;
            lastChild.OnKeyDown += OnKeyDown;
            lastChild.OnElementDraw += OnDrawTextBox;


            lastChild = linkedTabStopElements.AddLast(displayModeControlBox.AddChildElement(new ElementCheckBox(gfx, "Bold font", fontControls, new Rect(2, 82, 14, 14), defaultColorScheme, initialEnabled: settings.fontBold))).Value;
            lastChild.OnElementLMButtonClicked = OnBoldFontClicked;
            lastChild.OnEnterKeyDown += OnBoldFontClicked;

            lastChild = linkedTabStopElements.AddLast(displayModeControlBox.AddChildElement(new ElementCheckBox(gfx, "Capital letter", fontControls, new Rect(2, 102, 14, 14), defaultColorScheme, initialEnabled: settings.capitalLetter))).Value;
            lastChild.OnElementLMButtonClicked = OnCapitalLettersClicked;
            lastChild.OnEnterKeyDown += OnCapitalLettersClicked;

            lastChild = linkedTabStopElements.AddLast(displayModeControlBox.AddChildElement(new ElementCheckBox(gfx, "Hide controls on startup", fontControls, new Rect(2, 122, 14, 14), defaultColorScheme, initialEnabled: settings.hideControlsOnStartup))).Value;
            lastChild.OnElementLMButtonClicked = OnHideControlsOnStartupClicked;
            lastChild.OnEnterKeyDown += OnHideControlsOnStartupClicked;

            var listBox = displayModeControlBox.AddChildElement(new ElementGroupBox(gfx, "ColorSchemes", fontControls, new Rect(160, 0, 120, 90),
                    colorSchemeTransparent, outlined: false));

            var colorSchemesNames = settings.colorSchemes.Select(item => item.name).ToArray();

            lastChild = linkedTabStopElements.AddLast(listBox.AddChildElement(new ElementListBox(gfx, "ColorScheme", fontControls, new Rect(1, 2, 110, 77), defaultColorScheme, colorSchemesNames, settings.activeColorScheme))).Value;
            ((ElementListBox)lastChild).OnSelectedObjectChanged += OnListBoxColorSchemeSelectedObjectChange;

            listBox.AddChildElement(new ElementText(gfx, "New color name:", fontControls, new Rect(1, 82, 90, 17), defaultColorScheme, textAlign: TextAlign.Left | TextAlign.Center));
            lastChild = linkedTabStopElements.AddLast(listBox.AddChildElement(new ElementTextBox(gfx, "add_new_color_textbox", fontControls, new Rect(1, 102, 110, 17),
                defaultColorScheme, initialEnableState: true, acceptInput: true,
                maxTextLength: 13, textAlign: TextAlign.Left | TextAlign.Center))).Value;
            lastChild.OnKeyDown += OnKeyDown;
            lastChild.OnElementDraw += OnDrawTextBox;

            lastChild = linkedTabStopElements.AddLast(listBox.AddChildElement(new ElementButton(gfx, "Add new color", fontControls, new Rect(1, 122, 110, 17), defaultColorScheme))).Value;
            lastChild.OnElementLMButtonClicked += OnAddNewColor;


            var colorBox = displayModeControlBox.AddChildElement(new ElementGroupBox(gfx, "Color", fontControls, new Rect(280, 0, 120, 144),
                colorSchemeTransparent, outlined: false));

            lastChild = linkedTabStopElements.AddLast(colorBox.AddChildElement(new ElementRadioButton(gfx, "Font color", fontControls, new Rect(2, 2, 14, 14), defaultColorScheme,
                isChecked: true))).Value;
            lastChild.OnElementLMButtonClicked += OnSelectedColorClicked;
            lastChild.OnEnterKeyDown += OnSelectedColorClicked;

            lastChild = linkedTabStopElements.AddLast(colorBox.AddChildElement(new ElementRadioButton(gfx, "Background color", fontControls, new Rect(2, 22, 14, 14), defaultColorScheme,
                isChecked: false))).Value;
            lastChild.OnElementLMButtonClicked += OnSelectedColorClicked;
            lastChild.OnEnterKeyDown += OnSelectedColorClicked;

            System.Drawing.Color initialColor = System.Drawing.Color.FromArgb(colorScheme.fontColor.ToARGB());

            colorBox.AddChildElement(new ElementText(gfx, "Red", fontControls, new Rect(2, 42, 90, 17), defaultColorScheme, textAlign: TextAlign.Left | TextAlign.Center));
            lastChild = linkedTabStopElements.AddLast(colorBox.AddChildElement(new ElementTextBox(gfx, "color_red_textbox", fontControls, new Rect(85, 42, 30, 17),
                defaultColorScheme, text: initialColor.R.ToString(), initialEnableState: true, acceptInput: true,
                maxTextLength: 3, digitsOnly: true, textAlign: TextAlign.Left | TextAlign.Center))).Value;
            lastChild.OnKeyDown += OnKeyDown;
            lastChild.OnElementDraw += OnDrawTextBox;

            colorBox.AddChildElement(new ElementText(gfx, "Green", fontControls, new Rect(2, 62, 90, 17), defaultColorScheme, textAlign: TextAlign.Left | TextAlign.Center));

            lastChild = linkedTabStopElements.AddLast(colorBox.AddChildElement(new ElementTextBox(gfx, "color_green_textbox", fontControls, new Rect(85, 62, 30, 17),
                defaultColorScheme, text: initialColor.G.ToString(), initialEnableState: true, acceptInput: true,
                maxTextLength: 3, digitsOnly: true, textAlign: TextAlign.Left | TextAlign.Center))).Value;
            lastChild.OnKeyDown += OnKeyDown;
            lastChild.OnElementDraw += OnDrawTextBox;

            colorBox.AddChildElement(new ElementText(gfx, "Blue", fontControls, new Rect(2, 82, 90, 17), defaultColorScheme, textAlign: TextAlign.Left | TextAlign.Center));

            lastChild = linkedTabStopElements.AddLast(colorBox.AddChildElement(new ElementTextBox(gfx, "color_blue_textbox", fontControls, new Rect(85, 82, 30, 17),
                defaultColorScheme, text: initialColor.B.ToString(), initialEnableState: true, acceptInput: true,
                maxTextLength: 3, digitsOnly: true, textAlign: TextAlign.Left | TextAlign.Center))).Value;
            lastChild.OnKeyDown += OnKeyDown;
            lastChild.OnElementDraw += OnDrawTextBox;

            colorBox.AddChildElement(new ElementText(gfx, "Alpha", fontControls, new Rect(2, 102, 90, 17), defaultColorScheme, textAlign: TextAlign.Left | TextAlign.Center));

            lastChild = linkedTabStopElements.AddLast(colorBox.AddChildElement(new ElementTextBox(gfx, "color_alpha_textbox", fontControls, new Rect(85, 102, 30, 17),
                defaultColorScheme, text: initialColor.A.ToString(), initialEnableState: true, acceptInput: true,
                maxTextLength: 3, digitsOnly: true, textAlign: TextAlign.Left | TextAlign.Center))).Value;
            lastChild.OnKeyDown += OnKeyDown;
            lastChild.OnElementDraw += OnDrawTextBox;

            lastChild = linkedTabStopElements.AddLast(colorBox.AddChildElement(new ElementButton(gfx, "Set color", fontControls, new Rect(3, 122, 112, 17), defaultColorScheme))).Value;
            lastChild.OnElementLMButtonClicked += OnSetSelectedColorClicked;


            var voicesBox = displayModeControlBox.AddChildElement(new ElementGroupBox(gfx, "Voices", fontControls, new Rect(400, 0, 120, 144),
                colorSchemeTransparent, outlined: false));

            lastChild = linkedTabStopElements.AddLast(voicesBox.AddChildElement(new ElementRadioButton(gfx, "All", fontControls, new Rect(2, 2, 14, 14), defaultColorScheme,
                isChecked: settings.displayVoices == DisplayVoices.All, costumCommand: DisplayVoices.All.ToString()))).Value;
            lastChild.OnElementLMButtonClicked += OnDisplayVoicesClicked;
            lastChild.OnEnterKeyDown += OnDisplayVoicesClicked;

            lastChild = linkedTabStopElements.AddLast(voicesBox.AddChildElement(new ElementRadioButton(gfx, "Chief only", fontControls, new Rect(2, 22, 14, 14), defaultColorScheme,
                isChecked: settings.displayVoices == DisplayVoices.ChiefOnly, costumCommand: DisplayVoices.ChiefOnly.ToString()))).Value;
            lastChild.OnElementLMButtonClicked += OnDisplayVoicesClicked;
            lastChild.OnEnterKeyDown += OnDisplayVoicesClicked;

            lastChild = linkedTabStopElements.AddLast(voicesBox.AddChildElement(new ElementRadioButton(gfx, "Spotter only", fontControls, new Rect(2, 42, 14, 14), defaultColorScheme,
                isChecked: settings.displayVoices == DisplayVoices.SpotterOnly, costumCommand: DisplayVoices.SpotterOnly.ToString()))).Value;
            lastChild.OnElementLMButtonClicked += OnDisplayVoicesClicked;
            lastChild.OnEnterKeyDown += OnDisplayVoicesClicked;

            lastChild = linkedTabStopElements.AddLast(voicesBox.AddChildElement(new ElementRadioButton(gfx, "Chief and spotter", fontControls, new Rect(2, 62, 14, 14), defaultColorScheme,
            isChecked: settings.displayVoices == DisplayVoices.ChiefAndSpotter, costumCommand: DisplayVoices.ChiefAndSpotter.ToString()))).Value;
            lastChild.OnElementLMButtonClicked += OnDisplayVoicesClicked;
            lastChild.OnEnterKeyDown += OnDisplayVoicesClicked;

            lastChild = linkedTabStopElements.AddLast(voicesBox.AddChildElement(new ElementRadioButton(gfx, "You only", fontControls, new Rect(2, 82, 14, 14), defaultColorScheme,
                isChecked: settings.displayVoices == DisplayVoices.YouOnly, costumCommand: DisplayVoices.YouOnly.ToString()))).Value;
            lastChild.OnElementLMButtonClicked += OnDisplayVoicesClicked;
            lastChild.OnEnterKeyDown += OnDisplayVoicesClicked;

            lastChild = linkedTabStopElements.AddLast(voicesBox.AddChildElement(new ElementRadioButton(gfx, "You and chief", fontControls, new Rect(2, 102, 14, 14), defaultColorScheme,
                isChecked: settings.displayVoices == DisplayVoices.YouAndChief, costumCommand: DisplayVoices.YouAndChief.ToString()))).Value;
            lastChild.OnElementLMButtonClicked += OnDisplayVoicesClicked;
            lastChild.OnEnterKeyDown += OnDisplayVoicesClicked;

            lastChild = linkedTabStopElements.AddLast(voicesBox.AddChildElement(new ElementRadioButton(gfx, "You and spotter", fontControls, new Rect(2, 122, 14, 14), defaultColorScheme,
                isChecked: settings.displayVoices == DisplayVoices.YouAndSpotter, costumCommand: DisplayVoices.YouAndSpotter.ToString()))).Value;
            lastChild.OnElementLMButtonClicked += OnDisplayVoicesClicked;
            lastChild.OnEnterKeyDown += OnDisplayVoicesClicked;

            var modeBox = displayModeControlBox.AddChildElement(new ElementGroupBox(gfx, "Mode", fontControls, new Rect(520, 0, 120, 40),
                colorSchemeTransparent, outlined: false));

            lastChild = linkedTabStopElements.AddLast(modeBox.AddChildElement(new ElementRadioButton(gfx, "Always on", fontControls, new Rect(2, 2, 14, 14), defaultColorScheme,
                isChecked: settings.displayMode == DisplayMode.AlwaysOn, costumCommand: DisplayMode.AlwaysOn.ToString()))).Value;
            lastChild.OnElementLMButtonClicked += OnDisplayModeClicked;
            lastChild.OnEnterKeyDown += OnDisplayModeClicked;

            lastChild = linkedTabStopElements.AddLast(modeBox.AddChildElement(new ElementRadioButton(gfx, "Movie mode", fontControls, new Rect(2, 22, 14, 14), defaultColorScheme,
                isChecked: settings.displayMode == DisplayMode.Movie, costumCommand: DisplayMode.Movie.ToString()))).Value;
            lastChild.OnElementLMButtonClicked += OnDisplayModeClicked;
            lastChild.OnEnterKeyDown += OnDisplayModeClicked;

            lastChild = linkedTabStopElements.AddLast(displayModeControlBox.AddChildElement(new ElementButton(gfx, "Save settings", fontControls, new Rect(overlayWindow.Width - 116, 122, 110, 17), defaultColorScheme))).Value;
            lastChild.OnElementLMButtonClicked += OnSaveOverlaySettings;
            lastChild.OnEnterKeyDown += OnSaveOverlaySettings;

            listNodeTabStopElement = linkedTabStopElements.First;
            listNodeTabStopElement.Value.selected = true;
            subtitleElement = overlayElements[subtitleOverlayName] = new ElementTextBox(gfx, subtitleOverlayName, font, new Rect(0, displayModeControlBox.rectangle.Bottom, overlayWindow.Width, subtitleTextBoxHeight),
            colorScheme, initialEnableState: true, internalDrawBox: false);

            subtitleElement.OnElementDraw += OnPhraseUpdate;

            overlayWindow.Resize(settings.windowWidth, (int)subtitleElement.rectangle.Bottom);

            foreach (var element in overlayElements)
            {
                element.Value.initialize();
            }
            subtitleElement.rectangle.Y = titleBar.elementEnabled ? displayModeControlBox.rectangle.Bottom : 0;
            overlayWindow.Resize(overlayWindow.X, overlayWindow.Y, settings.windowWidth, (int)subtitleElement.rectangle.Bottom);
            overlayWindow.OnWindowMessage += overlayWindow_OnWindowMessage;
            //make sure overlay dont steal focus from main window.
            Microsoft.VisualBasic.Interaction.AppActivate(System.Diagnostics.Process.GetCurrentProcess().Id);
        }

        private void overlayWindow_OnWindowMessage(object sender, OverlayWindowsMessage e)
        {
            try
            {
                if (e.WindowMessage == WindowMessage.Keydown)
                {
                    bool shiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                    switch ((Keys)e.wParam)
                    {
                        case Keys.Tab:
                            {
                                if(shiftPressed)
                                {
                                    if (linkedTabStopElements.First == listNodeTabStopElement)
                                    {
                                        listNodeTabStopElement = linkedTabStopElements.Last;
                                    }
                                    else
                                    {
                                        listNodeTabStopElement = listNodeTabStopElement.Previous;
                                    }
                                }
                                else
                                {
                                    if (linkedTabStopElements.Last == listNodeTabStopElement)
                                    {
                                        listNodeTabStopElement = linkedTabStopElements.First;
                                    }
                                    else
                                    {
                                        listNodeTabStopElement = listNodeTabStopElement.Next;
                                    }
                                }
                                listNodeTabStopElement.Value.selected = true;
                                foreach (var element in linkedTabStopElements)
                                {
                                    if (element != listNodeTabStopElement.Value)
                                    {
                                        element.selected = false;
                                    }
                                }
                                return;
                            }
                    }
                }
                foreach (var element in overlayElements)
                {
                    if (element.Value.OnWindowMessage(e.WindowMessage, e.wParam, e.lParam))
                    {
                        var linkedElement = linkedTabStopElements.FirstOrDefault(el => el.mousePressed);
                        if (linkedElement != null)
                        {
                            linkedElement.selected = true;
                            listNodeTabStopElement = linkedTabStopElements.Find(linkedElement);
                            foreach (var elNode in linkedTabStopElements.Where(el => !el.mousePressed))
                            {
                                elNode.selected = false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                //Console.WriteLine(ex.Message);
                // Swollow
            }

        }

        private void DoInputHacks()
        {
            if(inputsEnabled)
            {
                Point cursor = new Point(Cursor.Position.X, Cursor.Position.Y);
                Rect overlayRect = new Rect(overlayWindow.X, overlayWindow.Y, overlayWindow.Width, overlayWindow.Height);
                if (overlayRect.Contains(cursor) && titleBar.elementEnabled && (Control.MouseButtons == MouseButtons.Left ||
                    Control.MouseButtons == MouseButtons.Right || Control.MouseButtons == MouseButtons.Middle))
                {
                    overlayWindow.ActivateWindow();
                    SetForegroundWindow(overlayWindow.Handle);
                }
            }
            if (Control.ModifierKeys != (Keys.Shift | Keys.Control))
            {
                shiftKeyReleased = true;
            }
            if (Control.ModifierKeys == (Keys.Shift | Keys.Control) && shiftKeyReleased)
            {
                titleBar.elementEnabled = !titleBar.elementEnabled;
                displayModeControlBox.elementEnabled = !displayModeControlBox.elementEnabled;
                shiftKeyReleased = false;
                if (inputsEnabled && titleBar.elementEnabled)
                {
                    overlayWindow.ActivateWindow();
                }
                else
                {
                    overlayWindow.DeActivateWindow();
                }
                subtitleElement.rectangle.Y = titleBar.elementEnabled ? displayModeControlBox.rectangle.Bottom : 0;
                overlayWindow.Resize(overlayWindow.X, overlayWindow.Y, settings.windowWidth, (int)subtitleElement.rectangle.Bottom);
            }
        }

        private void overlayWindow_DrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            DoInputHacks();
            if (!shown)
            {
                if (!cleared)
                {
                    // if we call this.overlayWindow.Hide() here, this method won't be called again so we can't
                    // unhide in this callback
                    keepWindowActiveBackUpOnClose = inputsEnabled;
                    windowHeightBackUpOnClose = overlayWindow.Height;
                    e.Graphics.ClearScene(Color.Transparent);
                    overlayWindow.Resize(0, 0);
                    cleared = true;
                }
                return;
            }
            else
            {
                if (windowHeightBackUpOnClose != -1)
                {
                    overlayWindow.Resize(settings.windowWidth, windowHeightBackUpOnClose);
                    windowHeightBackUpOnClose = -1;
                }
            }
            cleared = false;
            var gfx = e.Graphics;

            if (keepWindowActiveBackUpOnClose)
            {
                inputsEnabled = keepWindowActiveBackUpOnClose;
                //overlayWindow.ActivateWindow();
                keepWindowActiveBackUpOnClose = false;
            }

            gfx.ClearScene(Color.Transparent);

            titleBar.updateInputs(overlayWindow.X, overlayWindow.Y, inputsEnabled);

            foreach (var elements in overlayElements)
            {
                elements.Value.drawElement();
            }

            //gfx.DrawImage(cursorImage, Cursor.Position.X - overlayWindow.X, Cursor.Position.Y - overlayWindow.Y);
        }

        private void overlayWindow_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
            // you may want to dispose any brushes, fonts or images
        }
        //
        IEnumerable<string> SplitToLines(string stringToSplit, int maximumLineLength)
        {
            var words = stringToSplit.Split(' ').Concat(new[] { "" });
            return
                words
                    .Skip(1)
                    .Aggregate(
                        words.Take(1).ToList(),
                        (a, w) =>
                        {
                            var last = a.Last();
                            while (last.Length > maximumLineLength)
                            {
                                a[a.Count() - 1] = last.Substring(0, maximumLineLength);
                                last = last.Substring(maximumLineLength);
                                a.Add(last);
                            }
                            var test = last + " " + w;
                            if (test.Length > maximumLineLength)
                            {
                                a.Add(w);
                            }
                            else
                            {
                                a[a.Count() - 1] = test;
                            }
                            return a;
                        });
        }
        private void OnPhraseUpdate(object sender, OverlayElementDrawUpdate e)
        {
            var lineOffsetY = e.rect.Top;
            ElementTextBox textBox = (ElementTextBox)sender;
            //e.graphics.DrawBox2D(fontBrush, backgroundBrush, new Rectangle(textBox.rectangle),1);
            List<string> phraseLines = new List<string>();
            lock (Audio.SubtitleManager.phraseBuffer)
            {
                if(Audio.SubtitleManager.phraseBuffer.Size < 1)
                {
                    string subtitle = initialSubtitle;
                    System.Drawing.SizeF textSize = textBox.font.MeasureString(subtitle);
                    if (textSize.Width > textBox.rectangle.Width)
                    {
                        int maxStringLen = 0;
                        string messureStr = "" + subtitle[0];
                        for (; maxStringLen < subtitle.Length; maxStringLen++)
                        {
                            if (textBox.font.MeasureString(messureStr).Width < textBox.rectangle.Width)
                            {
                                messureStr += subtitle[maxStringLen];
                            }
                            else
                            {
                                break;
                            }
                        }
                        phraseLines = SplitToLines(subtitle, maxStringLen).ToList();
                        textBox.rectangle.Height = (textSize.Height) * phraseLines.Count;
                        //e.graphics.FillRectangle(backgroundBrush, new Rectangle(textBox.rectangle));
                        if (textBox.rectangle.Bottom != overlayWindow.Height)
                        {
                            overlayWindow.Resize((int)settings.windowWidth, (int)textBox.rectangle.Bottom);
                        }
                        foreach (var line in phraseLines)
                        {
                            e.graphics.DrawTextWithBackground(textBox.font, fontBrush, backgroundBrush, e.rect.Left, lineOffsetY, line, settings.windowWidth, true, likelyShownInVR || titleBar.elementEnabled);
                            lineOffsetY += (textSize.Height);
                        }
                    }
                    else
                    {
                        textBox.rectangle.Height = (textSize.Height);
                        if (textBox.rectangle.Bottom != overlayWindow.Height)
                        {
                            overlayWindow.Resize((int)settings.windowWidth, (int)textBox.rectangle.Bottom);
                        }
                        e.graphics.DrawTextWithBackground(textBox.font, fontBrush, backgroundBrush, e.rect.Left, lineOffsetY, subtitle, settings.windowWidth, true, likelyShownInVR || titleBar.elementEnabled);
                        lineOffsetY += (textSize.Height);
                    }
                    return;
                }

                var phrases = Audio.SubtitleManager.phraseBuffer.ToArray();

                //public enum PhraseVoiceType { chief = 0, spotter, you }
                int elementCount = settings.displayMode == DisplayMode.AlwaysOn ? settings.maxSubtitlehistory : 1;
                for (int i = 0; i < phrases.Length && i < elementCount; i++)
                {
                    bool showMovie = settings.displayMode == DisplayMode.Movie ? DateTime.Now < DateTime.FromFileTime(phrases[i].fileTime) + TimeSpan.FromSeconds(settings.displayTime) :
                        true;

                    bool shouldShowVoice = false;
                    if ((PhraseVoiceType)phrases[i].voiceType == PhraseVoiceType.chief && (settings.displayVoices == DisplayVoices.All ||
                        settings.displayVoices == DisplayVoices.ChiefOnly || settings.displayVoices == DisplayVoices.ChiefAndSpotter ||
                        settings.displayVoices == DisplayVoices.YouAndChief))
                    {
                        shouldShowVoice = true;
                    }
                    else if ((PhraseVoiceType)phrases[i].voiceType == PhraseVoiceType.spotter && (settings.displayVoices == DisplayVoices.All ||
                        settings.displayVoices == DisplayVoices.SpotterOnly || settings.displayVoices == DisplayVoices.ChiefAndSpotter ||
                        settings.displayVoices == DisplayVoices.YouAndSpotter))
                    {
                        shouldShowVoice = true;
                    }
                    else if ((PhraseVoiceType)phrases[i].voiceType == PhraseVoiceType.you && (settings.displayVoices == DisplayVoices.All ||
                        settings.displayVoices == DisplayVoices.YouOnly || settings.displayVoices == DisplayVoices.YouAndSpotter ||
                        settings.displayVoices == DisplayVoices.YouAndChief))
                    {
                        shouldShowVoice = true;
                    }
                    if(!shouldShowVoice || !showMovie)
                    {
                        continue;
                    }

                    string subtitle = settings.capitalLetter ? phrases[i].voiceName.ToUpper() + ": " + phrases[i].phrase.ToUpper() :
                        phrases[i].voiceName + ": " + Utilities.Strings.FirstLetterToUpper(phrases[i].phrase);
                    System.Drawing.SizeF textSize = textBox.font.MeasureString(subtitle);
                    if (textSize.Width > textBox.rectangle.Width)
                    {
                        int maxStringLen = 0;
                        string messureStr = "" + subtitle[0];
                        for (; maxStringLen < subtitle.Length; maxStringLen++)
                        {
                            if (textBox.font.MeasureString(messureStr).Width < textBox.rectangle.Width)
                            {
                                messureStr += subtitle[maxStringLen];
                            }
                            else
                            {
                                break;
                            }
                        }
                        phraseLines.AddRange(SplitToLines(subtitle, maxStringLen));
                    }
                    else
                    {
                        phraseLines.Add(subtitle);
                    }
                }
                textBox.rectangle.Height = (messuredFontHeight - 1) * phraseLines.Count;
                // only resize the width of the window if we are not showing vr windows as it will screw with scaling.
                if (textBox.rectangle.Bottom != overlayWindow.Height)
                {
                    overlayWindow.Resize((int)settings.windowWidth, (int)textBox.rectangle.Bottom);
                }
                foreach (var line in phraseLines)
                {
                    e.graphics.DrawTextWithBackground(textBox.font, fontBrush,backgroundBrush, e.rect.Left, lineOffsetY, line, settings.windowWidth, true, likelyShownInVR || titleBar.elementEnabled);
                    lineOffsetY += (messuredFontHeight - 1);
                }
            }
        }

        private void OnDrawTextBox(object sender, OverlayElementDrawUpdate e)
        {
            ElementTextBox element = (ElementTextBox)sender;
            float carretPos = 1;
            if (element.text.Length > 0)
            {
                System.Drawing.SizeF fontRect = element.font.MeasureString(element.text);
                carretPos = fontRect.Width + 2;

                if (element.textAlign.HasFlag(TextAlign.Left) && element.textAlign.HasFlag(TextAlign.Top))
                {
                    e.graphics.DrawText(element.font, element.secondaryBrush, e.rect.Left + 1, e.rect.Top, element.text);
                }
                else if (element.textAlign.HasFlag(TextAlign.Left) && element.textAlign.HasFlag(TextAlign.Center))
                {
                    float textY = ((e.rect.Height - fontRect.Height) / 2) + e.rect.Top;
                    e.graphics.DrawText(element.font, element.secondaryBrush, e.rect.Left + 1, textY, element.text);
                }
                /*else if (element.textAlign.HasFlag(TextAlign.CenterRect))
                {
                    e.graphics.DrawTextCenterInRect(font, element.secondaryBrush, e.rect, element.text);
                }*/
                //e.graphics.DrawText(element.font, fontBrush, e.rect.Left, e.rect.Top, element.text);
            }
            if(element.selected && DateTime.Now.Second % 2 == 0)
            {
                e.graphics.DrawLine(element.secondaryBrush, (int)carretPos + e.rect.Left, e.rect.Top + 1, (int)carretPos + e.rect.Left, e.rect.Bottom - 2, 2);
            }
            return;
        }
        private void OnButtonClosed(object sender, OverlayElementClicked e)
        {
            this.overlayWindow.DeActivateWindow();
            shown = false;
            //overlayWindow.Hide();
        }

        private void OnEnableUserInput(object sender, OverlayElementClicked e)
        {
            inputsEnabled = e.enabled;
            if (inputsEnabled)
            {
                forgroundWindow = GetForegroundWindow();
                this.overlayWindow.ActivateWindow();
                SetForegroundWindow(overlayWindow.Handle);
            }
            else
            {
                this.overlayWindow.DeActivateWindow();
                SetForegroundWindow(forgroundWindow);
            }
        }
        private void OnSavePosition(object sender, OverlayElementClicked e)
        {
            settings.windowX = overlayWindow.X;
            settings.windowY = overlayWindow.Y;
            OverlaySettings.saveOverlaySetttings(overlayFileName, settings);
        }
        public string KeyCodeToUnicode(Keys key)
        {
            byte[] keyboardState = new byte[255];
            bool keyboardStateStatus = GetKeyboardState(keyboardState);

            if (!keyboardStateStatus)
            {
                return "";
            }

            uint virtualKeyCode = (uint)key;
            uint scanCode = MapVirtualKey(virtualKeyCode, 0);
            IntPtr inputLocaleIdentifier = GetKeyboardLayout(Thread.CurrentThread.ManagedThreadId);

            StringBuilder result = new StringBuilder();
            ToUnicodeEx(virtualKeyCode, scanCode, keyboardState, result, 5, 0, inputLocaleIdentifier);

            return result.ToString();
        }

        private void OnKeyDown(object sender, OverlayElementKeyDown e)
        {
            ElementTextBox element = (ElementTextBox)sender;
            string text = element.text;
            if (e.key == Keys.Back)
            {
                if(text.Length > 0)
                    element.text = text.Remove(text.Length - 1);
                return;
            }
            string wChar = KeyCodeToUnicode(e.key);
            if (element.digitsOnly && !int.TryParse(wChar, out _))
            {
                return;
            }
            text = element.text + wChar;
            if (element.maxTextLength != 0)
            {
                if(text.Length <= element.maxTextLength)
                {
                    element.text = text;
                }
                return;
            }
            else if (element.font.MeasureString(text).Width < element.rectangle.Width)
            {
                element.text = text;
            }

        }
        public void OnSaveOverlaySettings(object sender, OverlayElementClicked e)
        {
            var windowWidthElement = (ElementTextBox)displayModeControlBox.children.FirstOrDefault(c => c.title == "window_width_textbox");
            if(int.TryParse(windowWidthElement.text, out int windowWidth))
            {
                // if smaller controls wont fit
                if (windowWidth >= 640)
                {
                    settings.windowWidth = windowWidth;
                }
                else
                {
                    settings.windowWidth = 640;
                }
            }
            var maxHistoryElement = (ElementTextBox)displayModeControlBox.children.FirstOrDefault(c => c.title == "max_history_textbox");
            if (int.TryParse(maxHistoryElement.text, out int maxHistory))
            {
                // max cached size
                if (maxHistory <= 10)
                {
                    settings.maxSubtitlehistory = maxHistory;
                }
                else
                {
                    settings.maxSubtitlehistory = 10;
                }
            }
            var fontSizeElement = (ElementTextBox)displayModeControlBox.children.FirstOrDefault(c => c.title == "font_size_textbox");
            if (int.TryParse(fontSizeElement.text, out int fontSize))
            {
                // max/min font size
                if (fontSize <= 92 && fontSize >= 4)
                {
                    settings.fontSize = fontSize;
                }
                else // revert to default
                {
                    settings.fontSize = 16;
                }
            }
            var displayTimeElement = (ElementTextBox)displayModeControlBox.children.FirstOrDefault(c => c.title == "display_time_textbox");
            if (int.TryParse(displayTimeElement.text, out int displayTime))
            {
                settings.displayTime = displayTime;
            }
            settings.windowX = overlayWindow.X;
            settings.windowY = overlayWindow.Y;
            OverlaySettings.saveOverlaySetttings(overlayFileName, settings);
        }
        public void OnCapitalLettersClicked(object sender, OverlayElementClicked e)
        {
            settings.capitalLetter = e.enabled;
        }
        public void OnHideControlsOnStartupClicked(object sender, OverlayElementClicked e)
        {
            settings.hideControlsOnStartup = e.enabled;
        }
        public void OnBoldFontClicked(object sender, OverlayElementClicked e)
        {
            settings.fontBold = e.enabled;
        }
        public void OnDisplayVoicesClicked(object sender, OverlayElementClicked e)
        {
            if (Enum.TryParse(e.costumTextId, out DisplayVoices result))
                settings.displayVoices = result;
        }
        public void OnDisplayModeClicked(object sender, OverlayElementClicked e)
        {
            if (Enum.TryParse(e.costumTextId, out DisplayMode result))
                settings.displayMode = result;
        }
        public void OnSelectedColorClicked(object sender, OverlayElementClicked e)
        {
            ElementRadioButton element = (ElementRadioButton)sender;
            var colorBox = displayModeControlBox.children.FirstOrDefault(el => el.title == "Color");
            var colorSchemes = displayModeControlBox.children.FirstOrDefault(el => el.title == "ColorSchemes");
            ElementListBox colorSchemesListBox = colorSchemes.children.FirstOrDefault(el => el.title == "ColorScheme") as ElementListBox;

            ColorScheme colorScheme = settings.colorSchemes.FirstOrDefault(cs => cs.name == colorSchemesListBox.GetSelectedObject());
            if (element.title == "Font color")
            {
                System.Drawing.Color selectedColor = System.Drawing.Color.FromArgb(colorScheme.fontColor.ToARGB());
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_red_textbox")).text = selectedColor.R.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_green_textbox")).text = selectedColor.G.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_blue_textbox")).text = selectedColor.B.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_alpha_textbox")).text = selectedColor.A.ToString();
            }
            else
            {
                System.Drawing.Color selectedColor = System.Drawing.Color.FromArgb(colorScheme.backgroundColor.ToARGB());
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_red_textbox")).text = selectedColor.R.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_green_textbox")).text = selectedColor.G.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_blue_textbox")).text = selectedColor.B.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_alpha_textbox")).text = selectedColor.A.ToString();
            }
        }
        public void OnSetSelectedColorClicked(object sender, OverlayElementClicked e)
        {
            var colorBox = displayModeControlBox.children.FirstOrDefault(el => el.title == "Color");
            var colorSchemes = displayModeControlBox.children.FirstOrDefault(el => el.title == "ColorSchemes");
            var colorSchemesListBox = colorSchemes.children.FirstOrDefault(el => el.title == "ColorScheme") as ElementListBox;
            if (colorBox != null)
            {
                var selecteColor = colorBox.children.FirstOrDefault(el => el.GetType() == typeof(ElementRadioButton) && ((ElementRadioButton)el).enabled);
                if(selecteColor != null)
                {
                    var red = ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_red_textbox")).text;
                    var green = ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_green_textbox")).text;
                    var blue = ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_blue_textbox")).text;
                    var alpha = ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_alpha_textbox")).text;
                    if(int.TryParse(red, out int redValue) && int.TryParse(green, out int greenValue) && int.TryParse(blue, out int blueValue) && int.TryParse(alpha, out int alphaValue))
                    {
                        if (redValue < 0 && redValue > 255)
                        {
                            redValue = 255;
                        }
                        if (greenValue < 0 && greenValue > 255)
                        {
                            greenValue = 255;
                        }
                        if (blueValue < 0 && blueValue > 255)
                        {
                            blueValue = 255;
                        }
                        if (alphaValue < 0 && alphaValue > 255)
                        {
                            alphaValue = 255;
                        }
                        if (selecteColor.title == "Font color")
                        {
                            fontBrush = e.graphics.CreateSolidBrush(new Color(redValue, greenValue, blueValue, alphaValue));
                            settings.colorSchemes.FirstOrDefault(cs => cs.name == colorSchemesListBox.GetSelectedObject()).fontColor = new Color(redValue, greenValue, blueValue, alphaValue);

                        }
                        else
                        {
                            backgroundBrush = e.graphics.CreateSolidBrush(new Color(redValue, greenValue, blueValue, alphaValue));
                            settings.colorSchemes.FirstOrDefault(cs => cs.name == colorSchemesListBox.GetSelectedObject()).backgroundColor = new Color(redValue, greenValue, blueValue, alphaValue);
                        }
                    }

                }

            }
        }
        public void OnListBoxColorSchemeSelectedObjectChange(object sender, OverlayElementClicked e)
        {
            var colorBox = displayModeControlBox.children.FirstOrDefault(el => el.title == "Color");
            var selecteColor = colorBox.children.FirstOrDefault(el => el.GetType() == typeof(ElementRadioButton) && ((ElementRadioButton)el).enabled);

            ColorScheme colorScheme = settings.colorSchemes.FirstOrDefault(cs => cs.name == e.costumTextId);
            if(colorScheme == null)
            {
                return;
            }
            if (selecteColor.title == "Font color")
            {
                System.Drawing.Color selectedColor = System.Drawing.Color.FromArgb(colorScheme.fontColor.ToARGB());
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_red_textbox")).text = selectedColor.R.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_green_textbox")).text = selectedColor.G.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_blue_textbox")).text = selectedColor.B.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_alpha_textbox")).text = selectedColor.A.ToString();
            }
            else
            {
                System.Drawing.Color selectedColor = System.Drawing.Color.FromArgb(colorScheme.backgroundColor.ToARGB());
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_red_textbox")).text = selectedColor.R.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_green_textbox")).text = selectedColor.G.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_blue_textbox")).text = selectedColor.B.ToString();
                ((ElementTextBox)colorBox.children.FirstOrDefault(el => el.title == "color_alpha_textbox")).text = selectedColor.A.ToString();
            }
            fontBrush = e.graphics.CreateSolidBrush(colorScheme.fontColor);
            backgroundBrush = e.graphics.CreateSolidBrush(colorScheme.backgroundColor);
        }
        public void OnAddNewColor(object sender, OverlayElementClicked e)
        {
            var colorSchemes = displayModeControlBox.children.FirstOrDefault(el => el.title == "ColorSchemes");
            ElementTextBox colorSchemesTextBox = colorSchemes.children.FirstOrDefault(el => el.title == "add_new_color_textbox") as ElementTextBox;
            ElementListBox colorSchemesListBox = colorSchemes.children.FirstOrDefault(el => el.title == "ColorScheme") as ElementListBox;

            if (string.IsNullOrWhiteSpace(colorSchemesTextBox.text))
                return;
            if(colorSchemesListBox.objects.FirstOrDefault(colorSchemesTextBox.text.Contains) == null)
            {
                settings.colorSchemes.Add(new ColorScheme(colorSchemesTextBox.text, new Color(0, 0, 0), new Color(255, 255, 255)));
                colorSchemesListBox.AddObject(colorSchemesTextBox.text);
                colorSchemesTextBox.text = "";
            }
        }
    }
}
