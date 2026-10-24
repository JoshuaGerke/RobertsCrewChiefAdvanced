using CrewChiefV4.Events;
using GameOverlay.Drawing;
using GameOverlay.Windows;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
namespace CrewChiefV4.Overlay
{

    public enum RenderMode
    {
        CONSOLE, CHART, ALL
    }
    public enum ChartRenderMode
    {
        SINGLE, STACKED
    }
    public enum SeriesMode
    {
        LAST_LAP, BEST_LAP, OPPONENT_BEST_LAP
    }
    public class CrewChiefOverlayWindow
    {
        #region overlay settings

        private readonly Boolean iRacingDiskTelemetryLogginEnabled = UserSettings.GetUserSettings().getBoolean("iracing_enable_disk_based_telemetry");
        public class CrewChiefOverlaySettings : OverlaySettings
        {
            public int trackMapSize = 150;
            public int windowWidth = 800;
            [JsonIgnore]
            public int chartHeight = 170;
            public int maxDisplayLines = 22;
            public float chartAlpha = 0.784313738f;
            public bool antiAliasCharts = false;

        }
        #endregion
        static readonly string overlayFileName = "crewchief_overlay.json";
        private readonly GraphicsWindow overlayWindow;
        private Font font;
        private Font fontBold;
        private SolidBrush fontBrush;
        private SolidBrush backgroundBrush;
        private SolidBrush transparentBrush;
        private int cachedFirstVisibleChar = -1;
        private static List<string> cachedVisibleLines = new List<string>();
        public static CrewChiefOverlaySettings settings = null;
        public static ColorScheme colorScheme = null;
        public static ColorScheme colorSchemeTransparent = null;
        public static Boolean createNewImage = true;

        private Boolean cleared = true;
        public bool inputsEnabled = false;
        public static bool keepWindowActiveBackUpOnClose = false;
        public static int windowHeightBackUpOnClose = -1;

        Dictionary<string, OverlayElement> overlayElements = new Dictionary<string, OverlayElement>();

        private string tileBarName = "overlay_titlebar";
        private string displayModeBoxName = "display_mode";
        private string chartModeBoxName = "chart_mode";
        private string overlayChartBoxName = "chart_container";
        private string subscriptionModeBoxName = "subscription_mode";
        private string availableSubscriptionBoxName = "available_subscription";
        private string consoleBoxName = "console";
        private string zoomAndPanBoxName = "zoom_pan";

        private OverlayElement titleBar = null;
        private OverlayElement consoleControlBox = null;
        private OverlayElement displayModeControlBox = null;
        private OverlayElement chartModeControlBox = null;
        private OverlayElement subscriptionModeControlBox = null;
        private OverlayElement availableSubscriptionControlBox = null;
        private OverlayElement chartBox = null;
        private OverlayElement zoomAndPanControlBox = null;
        private OverlayElement startAppButton = null;

        private bool showBestLap = false;
        private bool showLastLap = false;
        private bool showOpponentBestLap = false;

        private GameEnum cachedGameEnum = GameEnum.UNKNOWN;

        private int maxDisplayLines = 0;
        private float consoleBoxHeight = 0;
        float messuredFontHeight = 0;

        private SynchronizationContext mainThreadContext = null;
        public CrewChiefOverlayWindow()
        {
            mainThreadContext = SynchronizationContext.Current;
            // initialize a new Graphics object
            // GraphicsWindow will do the remaining initialization            
            settings = OverlaySettings.loadOverlaySetttings<CrewChiefOverlaySettings>(overlayFileName);
            if (settings.colorSchemes == null || settings.colorSchemes.Count == 0)
            {
                settings.colorSchemes = new List<ColorScheme>() { OverlaySettings.defaultCrewChiefColorScheme, OverlaySettings.windowsGrayColorScheme, OverlaySettings.transparentColorScheme };
            }
            colorScheme = settings.colorSchemes.FirstOrDefault(s => s.name == settings.activeColorScheme);
            colorSchemeTransparent = OverlaySettings.transparentColorScheme;
            if (colorScheme == null)
            {
                colorScheme = OverlaySettings.defaultCrewChiefColorScheme;
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
                Title = "CrewChief Overlay",
                ClassName = "CrewChief_Overlay",
            };

            overlayWindow.SetupGraphics += overlayWindow_SetupGraphics;
            overlayWindow.DestroyGraphics += overlayWindow_DestroyGraphics;
            overlayWindow.DrawGraphics += overlayWindow_DrawGraphics;
        }

        public void Dispose()
        {
            // you do not need to dispose the Graphics surface
            overlayWindow.Dispose();
        }

        public void Initialize() { }

        public void Run()
        {
            // creates the window and setups the graphics
            overlayWindow.StartThread();
        }
        #region overlayWindow events
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
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error parsing " + settings.fontName + ": " + ex.Message);
            }
            fontBrush = gfx.CreateSolidBrush(colorScheme.fontColor);
            backgroundBrush = gfx.CreateSolidBrush(colorScheme.backgroundColor);
            transparentBrush = gfx.CreateSolidBrush(Color.Transparent);

            titleBar = overlayElements[tileBarName] = new OverlayHeader(gfx, "CrewChief Overlay", fontBold, new Rect(0, 0, overlayWindow.Width, 20), colorScheme, overlayWindow, OnEnableUserInput, OnButtonClosed, OnSavePosition);
            titleBar.AddChildElement(new ElementCheckBox(gfx, "Enable input", font, new Rect(202, 3, 14, 14), colorScheme));
            titleBar.AddChildElement(new ElementButton(gfx, "ButtonClose", font, new Rect(overlayWindow.Width - 18, 3, 14, 14), colorScheme));
            titleBar.AddChildElement(new ElementButton(gfx, "Save window position", font, new Rect(overlayWindow.Width - 160, 3, 130, 14), colorScheme));

            maxDisplayLines = settings.maxDisplayLines == -1 || settings.maxDisplayLines > 17 ? 17 : settings.maxDisplayLines;
            messuredFontHeight = font.MeasureString("Hello World").Height;
            consoleBoxHeight = messuredFontHeight * (maxDisplayLines);
            consoleControlBox = overlayElements[consoleBoxName] = new ElementTextBox(gfx, consoleBoxName, font, new Rect(0, 20, overlayWindow.Width, consoleBoxHeight),
                colorScheme, initialEnableState: false);
            consoleControlBox.OnElementDraw += OnUpdateConsole;

            displayModeControlBox = overlayElements[displayModeBoxName] = new ElementGroupBox(gfx, displayModeBoxName, font, new Rect(0, 20, overlayWindow.Width, 22),
                colorScheme, initialEnableState: false);
            int offsetX = (int)displayModeControlBox.rectangle.X + 2;
            int offsetY = (int)displayModeControlBox.rectangle.Y + 2;

            startAppButton = displayModeControlBox.AddChildElement(new ElementButton(gfx, "Start App", font, new Rect(4, 2, 150, 16), colorScheme));
            startAppButton.OnElementLMButtonClicked += OnStartApplication;

            var lastChild = displayModeControlBox.AddChildElement(new ElementRadioButton(gfx, "Show Telemetry Chart(s)", font, new Rect(offsetX + 200, 4, 14, 14), colorScheme,            
                isChecked: OverlayController.mode == RenderMode.CHART));
            lastChild.OnElementLMButtonClicked += OnShowCharts;

            lastChild = displayModeControlBox.AddChildElement(new ElementRadioButton(gfx, "Show Console", font, new Rect(offsetX + 400, 4, 14, 14), colorScheme,
                isChecked: OverlayController.mode == RenderMode.CONSOLE));
            lastChild.OnElementLMButtonClicked += OnShowConsole;

            lastChild = displayModeControlBox.AddChildElement(new ElementRadioButton(gfx, "Show Both", font, new Rect(offsetX + 600, 4, 14, 14), colorScheme,
                isChecked: OverlayController.mode == RenderMode.ALL));
            lastChild.OnElementLMButtonClicked += OnShowAll;
            chartModeControlBox = overlayElements[chartModeBoxName] = new ElementGroupBox(gfx, chartModeBoxName, font, new Rect(0, 0, overlayWindow.Width, 20),
                colorScheme, initialEnableState: false);
            offsetX = (int)chartModeControlBox.rectangle.Left + 2;
            offsetY = (int)chartModeControlBox.rectangle.Top + 2;

            lastChild = chartModeControlBox.AddChildElement(new ElementRadioButton(gfx, "Single Telemetry Chart", font, new Rect(offsetX, 2, 14, 14), colorScheme,
                isChecked: OverlayController.chartRenderMode == ChartRenderMode.SINGLE));
            lastChild.OnElementLMButtonClicked += OnShowSingleChart;

            lastChild = chartModeControlBox.AddChildElement(new ElementRadioButton(gfx, "Stacked Telemetry Charts", font, new Rect(offsetX + 200, 2, 14, 14), colorScheme,
                isChecked: OverlayController.chartRenderMode == ChartRenderMode.STACKED));
            lastChild.OnElementLMButtonClicked += OnShowStackedCharts;

            subscriptionModeControlBox = overlayElements[subscriptionModeBoxName] = new ElementGroupBox(gfx, subscriptionModeBoxName, font, new Rect(0, 0, overlayWindow.Width, 38),
                colorScheme, initialEnableState: false);
            offsetX = (int)subscriptionModeControlBox.rectangle.Left + 2;
            offsetY = (int)subscriptionModeControlBox.rectangle.Top + 2;

            lastChild = subscriptionModeControlBox.AddChildElement(new ElementCheckBox(gfx, "Show Last Lap", font, new Rect(offsetX, 2, 14, 14), colorScheme, initialEnabled: true));
            lastChild.OnElementLMButtonClicked += OnShowLastLap;
            lastChild = subscriptionModeControlBox.AddChildElement(new ElementCheckBox(gfx, "Show Best Lap", font, new Rect(offsetX + 200, 2, 14, 14), colorScheme));
            lastChild.OnElementLMButtonClicked += OnShowBestLap;
            lastChild = subscriptionModeControlBox.AddChildElement(new ElementCheckBox(gfx, "Show Opponent Best Lap", font, new Rect(offsetX + 400, 2, 14, 14), colorScheme));
            lastChild.OnElementLMButtonClicked += OnShowOpponentBestLap;

            lastChild = subscriptionModeControlBox.AddChildElement(new ElementRadioButton(gfx, "Show Full Lap", font, new Rect(offsetX, 20, 14, 14), colorScheme, 
                OverlayController.sectorToShow == SectorToShow.ALL, "0"));
            lastChild.OnElementLMButtonClicked += OnSetSectorOrLap;
            lastChild = subscriptionModeControlBox.AddChildElement(new ElementRadioButton(gfx, "Show Sector 1", font, new Rect(offsetX + 200, 20, 14, 14), colorScheme,
                OverlayController.sectorToShow == SectorToShow.SECTOR_1, "1"));
            lastChild.OnElementLMButtonClicked += OnSetSectorOrLap;
            lastChild = subscriptionModeControlBox.AddChildElement(new ElementRadioButton(gfx, "Show Sector 2", font, new Rect(offsetX + 400, 20, 14, 14), colorScheme,
                OverlayController.sectorToShow == SectorToShow.SECTOR_2, "2"));
            lastChild.OnElementLMButtonClicked += OnSetSectorOrLap;
            lastChild = subscriptionModeControlBox.AddChildElement(new ElementRadioButton(gfx, "Show Sector 3", font, new Rect(offsetX + 600, 20, 14, 14), colorScheme, 
                OverlayController.sectorToShow == SectorToShow.SECTOR_3, "3"));
            lastChild.OnElementLMButtonClicked += OnSetSectorOrLap;

            zoomAndPanControlBox  = overlayElements[zoomAndPanBoxName] = new ElementGroupBox(gfx, zoomAndPanBoxName, font, new Rect(0, 0, overlayWindow.Width, 22),
                colorScheme, initialEnableState: false);
            lastChild = zoomAndPanControlBox.AddChildElement(new ElementButton(gfx, "<< Previous Lap", font, new Rect(4, 2, 110, 16), colorScheme));
            lastChild.OnElementLMButtonClicked += ShowPreviousLap;
            lastChild = zoomAndPanControlBox.AddChildElement(new ElementButton(gfx, "Next Lap >>", font, new Rect(118, 2, 110, 16), colorScheme));
            lastChild.OnElementLMButtonClicked += ShowNextLap;
            lastChild = zoomAndPanControlBox.AddChildElement(new ElementButton(gfx, "Zoom In", font, new Rect(232, 2, 110, 16), colorScheme));
            lastChild.OnElementLMButtonClicked += OnZoomIn;
            lastChild = zoomAndPanControlBox.AddChildElement(new ElementButton(gfx, "Zoom Out", font, new Rect(346, 2, 110, 16), colorScheme));
            lastChild.OnElementLMButtonClicked += OnZoomOut;
            lastChild = zoomAndPanControlBox.AddChildElement(new ElementButton(gfx, "<< Pan Left", font, new Rect(460, 2, 110, 16), colorScheme));
            lastChild.OnElementLMButtonClicked += OnPanLeft;
            lastChild = zoomAndPanControlBox.AddChildElement(new ElementButton(gfx, "Pan Right >>", font, new Rect(574, 2, 110, 16), colorScheme));
            lastChild.OnElementLMButtonClicked += OnPanRight;
            lastChild = zoomAndPanControlBox.AddChildElement(new ElementButton(gfx, "Reset", font, new Rect(688, 2, 108, 16), colorScheme));
            lastChild.OnElementLMButtonClicked += OnReset;

            availableSubscriptionControlBox = overlayElements[availableSubscriptionBoxName] = new ElementGroupBox(gfx, availableSubscriptionBoxName, font, new Rect(0, 0, overlayWindow.Width, 0),
                colorScheme, initialEnableState: false);

            chartBox = overlayElements[overlayChartBoxName] = new ElementGroupBox(gfx, overlayChartBoxName, font, new Rect(0, 0, settings.windowWidth, 0),
                colorSchemeTransparent, initialEnableState: true, outlined: false);
            overlayWindow.Resize(settings.windowWidth, (int)titleBar.rectangle.Bottom);

            foreach (var element in overlayElements)
            {
                element.Value.initialize();
            }
            overlayWindow.OnWindowMessage += overlayWindow_OnWindowMessage;
            Microsoft.VisualBasic.Interaction.AppActivate(System.Diagnostics.Process.GetCurrentProcess().Id);
        }


        private void overlayWindow_OnWindowMessage(object sender, OverlayWindowsMessage e)
        {
            try
            {
                foreach (var element in overlayElements)
                {
                    element.Value.OnWindowMessage(e.WindowMessage, e.wParam, e.lParam);
                }
            }
            catch
            {
                // Swollow
            }

        }
        private void overlayWindow_DrawGraphics(object sender, DrawGraphicsEventArgs e)
        {
            if (!OverlayController.shown)
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
            int thisFrameWindowHeight = 0;
            if (keepWindowActiveBackUpOnClose)
            {
                inputsEnabled = keepWindowActiveBackUpOnClose;
                overlayWindow.ActivateWindow();
                keepWindowActiveBackUpOnClose = false;
            }
            if(CrewChief.gameDefinition != null)
            {
                populateControlBox(gfx, cachedGameEnum != CrewChief.gameDefinition.gameEnum);
                cachedGameEnum = CrewChief.gameDefinition.gameEnum;
            }

            // you do not need to call BeginScene() or EndScene()

            gfx.ClearScene(Color.Transparent);

            titleBar.updateInputs(overlayWindow.X, overlayWindow.Y, inputsEnabled);

            foreach (var elements in overlayElements.Where(el => el.Value.elementEnabled && el.Value != chartBox))
            {
                thisFrameWindowHeight += (int)elements.Value.rectangle.Height;
            }

            if (OverlayController.mode == RenderMode.CHART || OverlayController.mode == RenderMode.ALL)
            {
                if (CrewChiefOverlayWindow.createNewImage)
                {
                    try
                    {
                        CreateNewImages(gfx);
                    }
                    catch (Exception ge)
                    {
                        Console.WriteLine(ge.Message);
                    }
                    CrewChiefOverlayWindow.createNewImage = false;
                }
                int rectHeight;
                if (OverlayController.showMap && chartBox.children.Count > 1)
                {
                    rectHeight = ((chartBox.children.Count - 1) * settings.chartHeight) + settings.trackMapSize;
                }
                else
                {
                    rectHeight = chartBox.children.Count * settings.chartHeight;
                }
                chartBox.rectangle = new Rect(0, thisFrameWindowHeight, settings.windowWidth, rectHeight);
            }
            else
            {
                chartBox.rectangle = new Rect(0, 0, settings.windowWidth, 0);
            }

            thisFrameWindowHeight += (int)chartBox.rectangle.Height;
            if (thisFrameWindowHeight != overlayWindow.Height)
            {
                overlayWindow.Resize(settings.windowWidth, thisFrameWindowHeight);
            }
            foreach (var elements in overlayElements.Where(el => el.Value.elementEnabled && el.Value != chartBox))
            {
                elements.Value.drawElement();
            }

            if (OverlayController.mode == RenderMode.CHART || OverlayController.mode == RenderMode.ALL)
            {
                chartBox.drawElement();
            }
            consoleControlBox.elementEnabled = OverlayController.mode != RenderMode.CHART;
            /*if (inputsEnabled)
            {
                gfx.DrawRectangle(fontBrush, 0, 0, overlayWindow.Width, overlayWindow.Height, 2);
            }*/
            //gfx.DrawImage(cursorImage, Cursor.Position.X - overlayWindow.X, Cursor.Position.Y - overlayWindow.Y);
        }
        private void overlayWindow_DestroyGraphics(object sender, DestroyGraphicsEventArgs e)
        {
            // you may want to dispose any brushes, fonts or images
        }
        #endregion
        /*public void CreateNewImages(Graphics gfx)
        { 
            int combinedImageHeight = 0;
            if (OverlayController.chartRenderMode == ChartRenderMode.SINGLE)
            {
                if (chartBox.children.Count > 2)
                {
                    foreach (ElementImage child in chartBox.children.Where(el => el.GetType() == typeof(ElementImage)))
                    {
                        child.DisposeImage();
                    }
                    chartBox.children.Clear();
                }
                ChartContainer chartContainer = Charts.createChart(settings.windowWidth, settings.chartHeight, settings.antiAliasCharts);
                if (chartBox.children.Count == 0)
                {
                    var child = chartBox.AddChildElement(new ElementImage(gfx, chartContainer.subscriptionId, font, new Rect(0, combinedImageHeight, settings.windowWidth, settings.chartHeight),
                        colorSchemeTransparent, chartContainer: chartContainer, imageAlpha: settings.chartAlpha, outlined: true));
                    child.OnElementMWheel += MouseWheelOnImage;
                    child.OnElementMMButtonClicked += MouseMButtonOnImage;
                }
                else
                {
                    ((ElementImage)chartBox.children[0]).UpdateImage(chartContainer, new GameOverlay.Drawing.Point(0, combinedImageHeight));
                }
                if (OverlayController.showMap)
                {
                    ChartContainer mapContainer = Charts.createWorldPositionSeries(SeriesMode.LAST_LAP, settings.trackMapSize);
                    if (mapContainer != null)
                    {
                        ElementImage mapImage = (ElementImage)chartBox.children.FirstOrDefault(c => c.title == "Map");
                        if (mapImage == null)
                        {
                            chartBox.AddChildElement(new ElementImage(gfx, mapContainer.subscriptionId, font, new Rect(0, (int)settings.chartHeight, 
                                OverlayController.mapXSizeScale * settings.trackMapSize, settings.trackMapSize), 
                                colorSchemeTransparent, chartContainer: mapContainer, imageAlpha: settings.chartAlpha, outlined: true));
                        }
                        else
                        {
                            mapImage.UpdateImage(mapContainer, new GameOverlay.Drawing.Point(0, (int)settings.chartHeight));
                        }
                    }
                }
            }
            else
            {
                List<ChartContainer> chartContainers = Charts.createCharts(settings.windowWidth, settings.chartHeight, settings.antiAliasCharts);
                var removedImages = chartBox.children.Where(c => !chartContainers.Any(cc => cc.subscriptionId == c.title && c.GetType() == typeof(ElementImage))).ToList();
                foreach (ElementImage img in removedImages)
                {
                    img.DisposeImage();
                    chartBox.children.Remove(img);
                }
                foreach (var chartContainer in chartContainers)
                {
                    ElementImage child = (ElementImage)chartBox.children.FirstOrDefault(c => c.title == chartContainer.subscriptionId);
                    if (child != null)
                    {
                        child.UpdateImage(chartContainer, new GameOverlay.Drawing.Point(0, combinedImageHeight));
                        combinedImageHeight += (int)settings.chartHeight;
                    }
                    else
                    {
                        var child2 = chartBox.AddChildElement(new ElementImage(gfx, chartContainer.subscriptionId, font, new Rect(0, combinedImageHeight, settings.windowWidth, settings.chartHeight),
                            colorSchemeTransparent, chartContainer: chartContainer, imageAlpha: settings.chartAlpha, outlined: true));
                        child2.OnElementMWheel += MouseWheelOnImage;
                        child2.OnElementMMButtonClicked += MouseMButtonOnImage;
                        combinedImageHeight += (int)settings.chartHeight;
                    }
                }
                if (OverlayController.showMap)
                {
                    ChartContainer mapContainer = Charts.createWorldPositionSeries(SeriesMode.LAST_LAP, settings.trackMapSize);
                    if (mapContainer != null)
                    {
                        ElementImage mapImage = (ElementImage)chartBox.children.FirstOrDefault(c => c.title == "Map");
                        if (mapImage == null)
                        {
                            chartBox.AddChildElement(new ElementImage(gfx, mapContainer.subscriptionId, font, new Rect(0, combinedImageHeight,
                                OverlayController.mapXSizeScale * settings.trackMapSize, settings.trackMapSize),
                                colorSchemeTransparent, chartContainer: mapContainer, imageAlpha: settings.chartAlpha, outlined: true));
                        }
                        else
                        {
                            mapImage.UpdateImage(mapContainer, new GameOverlay.Drawing.Point(0, combinedImageHeight));
                        }
                    }
                }
            }
        }
        */

        public void CreateNewImages(Graphics gfx)
        {
            int combinedImageHeight = 0;
            List<ChartContainer> chartContainers = new List<ChartContainer>();
            if (OverlayController.chartRenderMode == ChartRenderMode.SINGLE)
            {
                chartContainers.AddRange(Charts.createOverlayChart(settings.windowWidth, settings.chartHeight, settings.antiAliasCharts));
            }
            else
            {
                chartContainers.AddRange(Charts.createStackedCharts(settings.windowWidth, settings.chartHeight, settings.antiAliasCharts));
            }
            foreach (ElementImage child in chartBox.children.Where(el => el.GetType() == typeof(ElementImage)))
            {
                child.DisposeImage();
            }
            chartBox.children.Clear();
            foreach (var chartContainer in chartContainers)
            {
                var child = chartBox.AddChildElement(new ElementImage(gfx, chartContainer.subscriptionId, font, new Rect(0, combinedImageHeight, settings.windowWidth, settings.chartHeight),
                    colorSchemeTransparent, chartContainer: chartContainer, imageAlpha: settings.chartAlpha, outlined: true));
                child.OnElementMWheel += MouseWheelOnImage;
                child.OnElementMMButtonClicked += MouseMButtonOnImage;
                combinedImageHeight += (int)settings.chartHeight;
            }
            if (OverlayController.showMap && Charts.hasTimeSeriesSubs())
            {
                ChartContainer mapContainer = Charts.createWorldPositionSeries(SeriesMode.LAST_LAP, settings.trackMapSize);
                if (mapContainer != null)
                {
                    ElementImage mapImage = (ElementImage)chartBox.children.FirstOrDefault(c => c.title == "Map");
                    if (mapImage == null)
                    {
                        chartBox.AddChildElement(new ElementImage(gfx, mapContainer.subscriptionId, font, new Rect(0, combinedImageHeight,
                            OverlayController.mapXSizeScale * settings.trackMapSize, settings.trackMapSize),
                            colorSchemeTransparent, chartContainer: mapContainer, imageAlpha: settings.chartAlpha, outlined: true));
                    }
                    else
                    {
                        mapImage.UpdateImage(mapContainer, new GameOverlay.Drawing.Point(0, combinedImageHeight));
                    }
                }
            }
        }

        private void populateControlBox(Graphics graphics, bool gamedefinitionChanged = false)
        {
            lock (availableSubscriptionControlBox.children)
            {
                if (gamedefinitionChanged)
                {
                    availableSubscriptionControlBox.children.Clear();
                }
                if (availableSubscriptionControlBox.children.Count <= 0)
                {
                    int elementHeight = 17;
                    int offsetX = ((int)availableSubscriptionControlBox.rectangle.Left + 2);
                    int offsetY = 2;
                    foreach (OverlaySubscription overlaySubscription in OverlayDataSource.getOverlaySubscriptions())
                    {
                        if (overlaySubscription.isDiskData && !iRacingDiskTelemetryLogginEnabled)
                        {
                            continue;
                        }
                        var sub = availableSubscriptionControlBox.AddChildElement(new ElementCheckBox(graphics, overlaySubscription.voiceCommandFragment_Internal, font, 
                            new Rect(offsetX, offsetY, 14, 14), colorScheme, overlaySubscription.id));
                        sub.OnElementLMButtonClicked += OnSubscribe;

                        int count = (int)Math.Floor((double)settings.windowWidth / 200);
                        if (availableSubscriptionControlBox.children.Count % count == 0)
                        {
                            offsetY += (int)elementHeight;
                            offsetX = (int)availableSubscriptionControlBox.rectangle.Left + 2;
                        }
                        else
                        {
                            offsetX += 200;
                        }
                    }
                    if (availableSubscriptionControlBox.children.Count > 0)
                    {
                        availableSubscriptionControlBox.rectangle.Height = offsetY + elementHeight;
                        availableSubscriptionControlBox.rectangle.Y = chartModeControlBox.rectangle.Bottom;
                        subscriptionModeControlBox.rectangle.Y = availableSubscriptionControlBox.rectangle.Bottom;
                        zoomAndPanControlBox.rectangle.Y = subscriptionModeControlBox.rectangle.Bottom;
                    }                        
                    else
                    {
                        availableSubscriptionControlBox.rectangle.Height = 0;
                        subscriptionModeControlBox.rectangle.Y = chartModeControlBox.rectangle.Bottom;
                        zoomAndPanControlBox.rectangle.Y = subscriptionModeControlBox.rectangle.Bottom;
                    }
                }
            }
        }

        private void UpdateElementsPosition()
        {
            if (consoleControlBox.elementEnabled && inputsEnabled)
            {
                consoleControlBox.rectangle.Y = displayModeControlBox.rectangle.Bottom;
            }
            else
            {
                consoleControlBox.rectangle.Y = titleBar.rectangle.Bottom;
            }
            if (chartModeControlBox.elementEnabled && consoleControlBox.elementEnabled)
            {
                chartModeControlBox.rectangle.Y = consoleControlBox.rectangle.Bottom;
            }
            else
            {
                chartModeControlBox.rectangle.Y = displayModeControlBox.rectangle.Bottom;
            }
            if(availableSubscriptionControlBox.children.Count > 0)
            {
                availableSubscriptionControlBox.rectangle.Y = chartModeControlBox.rectangle.Bottom;
                subscriptionModeControlBox.rectangle.Y = availableSubscriptionControlBox.rectangle.Bottom;
                zoomAndPanControlBox.rectangle.Y = subscriptionModeControlBox.rectangle.Bottom;
            }
            else
            {
                subscriptionModeControlBox.rectangle.Y = chartModeControlBox.rectangle.Bottom;
                zoomAndPanControlBox.rectangle.Y = subscriptionModeControlBox.rectangle.Bottom;
                availableSubscriptionControlBox.rectangle.Y = zoomAndPanControlBox.rectangle.Bottom;
            }
        }

        #region overlay element events
        public void OnStartApplication(object sender, OverlayElementClicked e)
        {
            if (sender.GetType() == typeof(ElementButton))
            {
                lock (MainWindow.instanceLock)
                {
                    if (MainWindow.instance != null)
                    {
                        mainThreadContext.Post(delegate
                        {
                            MainWindow.instance.startApplicationButton_Click(this, new EventArgs());
                        }, null);
                    }
                }
            }
            else
            {
                if (startAppButton.title == "Start App")
                {
                    startAppButton.title = "Stop App";
                }
                else
                {
                    startAppButton.title = "Start App";
                }
            }
        }
        private void OnButtonClosed(object sender, OverlayElementClicked e)
        {
            this.overlayWindow.DeActivateWindow();            
            Events.OverlayController.shown = false;
        }
        private void OnUpdateConsole(object sender, OverlayElementDrawUpdate e)
        {
            lock (MainWindow.instance.consoleTextBox)
            {
                RichTextBox textBox = MainWindow.instance.consoleTextBox;
                int firstVisibleChar = textBox.GetCharIndexFromPosition(new System.Drawing.Point(0, 0));
                if (cachedFirstVisibleChar != firstVisibleChar)
                {
                    cachedFirstVisibleChar = firstVisibleChar;
                    int firstVisibleLine = textBox.GetLineFromCharIndex(firstVisibleChar);
                    int lastVisibleChar = textBox.GetCharIndexFromPosition(new System.Drawing.Point(0, textBox.ClientSize.Height - textBox.Font.Height)); // Skip Last line(Caret)
                    int lastVisibleLine = textBox.GetLineFromCharIndex(lastVisibleChar) + 1;
                    int count = (lastVisibleLine) - firstVisibleLine;
                    try
                    {
                        if (count > 0 && (firstVisibleLine + count) <= textBox.Lines.Length - 1)
                        {
                            List<string> visibleLines = textBox.Lines.ToList().GetRange(firstVisibleLine, count);
                            cachedVisibleLines = visibleLines.Skip((visibleLines.Count) - maxDisplayLines).ToList();
                            //string lineLen = cachedVisibleLines.Aggregate("", (max, cur) => max.Length > cur.Length ? max : cur);                                                                                    
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in ConsoleOverlay: {ex.Message}");
                    }
                }
            }
            var lineOffsetY = e.rect.Top;
            foreach (var line in cachedVisibleLines)
            {
                e.graphics.DrawText(font, fontBrush, e.rect.Left, lineOffsetY, line);
                lineOffsetY += messuredFontHeight;
            }
        }
        private void OnShowSingleChart(object sender, OverlayElementClicked e)
        {
            OverlayController.chartRenderMode = ChartRenderMode.SINGLE;
            CrewChiefOverlayWindow.createNewImage = true;
        }
        private void OnShowStackedCharts(object sender, OverlayElementClicked e)
        {
            OverlayController.chartRenderMode = ChartRenderMode.STACKED;
            CrewChiefOverlayWindow.createNewImage = true;
        }
        private void OnShowCharts(object sender, OverlayElementClicked e)
        {
            OverlayController.mode = RenderMode.CHART;
            consoleControlBox.elementEnabled = false;
            if (inputsEnabled)
            {
                chartModeControlBox.elementEnabled = true;
                subscriptionModeControlBox.elementEnabled = true;
                availableSubscriptionControlBox.elementEnabled = true;
                zoomAndPanControlBox.elementEnabled = true;
            }
            CrewChiefOverlayWindow.createNewImage = true;
            UpdateElementsPosition();
        }
        private void OnShowConsole(object sender, OverlayElementClicked e)
        {
            OverlayController.mode = RenderMode.CONSOLE;
            chartModeControlBox.elementEnabled = false;
            subscriptionModeControlBox.elementEnabled = false;
            availableSubscriptionControlBox.elementEnabled = false;
            zoomAndPanControlBox.elementEnabled = false;
            consoleControlBox.elementEnabled = true;
            UpdateElementsPosition();
        }
        private void OnShowAll(object sender, OverlayElementClicked e)
        {
            OverlayController.mode = RenderMode.ALL;
            consoleControlBox.elementEnabled = true;
            if (inputsEnabled)
            {
                chartModeControlBox.elementEnabled = true;
                subscriptionModeControlBox.elementEnabled = true;
                availableSubscriptionControlBox.elementEnabled = true;
                zoomAndPanControlBox.elementEnabled = true;
            }
            CrewChiefOverlayWindow.createNewImage = true;
            UpdateElementsPosition();
        }

        private void OnSubscribe(object sender, OverlayElementClicked e)
        {
            foreach (OverlaySubscription overlaySubscription in OverlayDataSource.getOverlaySubscriptions())
            {
                if (overlaySubscription.id == e.costumTextId)
                {
                    CrewChiefOverlayWindow.createNewImage = true;
                    if (!OverlayController.shown)
                    {
                        OverlayController.mode = RenderMode.CHART;
                        OverlayController.shown = true;
                    }
                    else if (OverlayController.mode == RenderMode.CONSOLE)
                    {
                        OverlayController.mode = RenderMode.ALL;
                    }
                    if (e.enabled)
                    {
                        if (showLastLap)
                            Charts.addSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.LAST_LAP));
                        if (showBestLap)
                            Charts.addSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.BEST_LAP));
                        if (showOpponentBestLap && overlaySubscription.opponentDataFieldname != null)
                        {
                            Charts.addSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.OPPONENT_BEST_LAP));
                        }
                    }
                    else
                    {
                        if (showLastLap)
                            Charts.removeSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.LAST_LAP));
                        if (showBestLap)
                            Charts.removeSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.BEST_LAP));
                        if (showOpponentBestLap && overlaySubscription.opponentDataFieldname != null)
                        {
                            Charts.removeSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.OPPONENT_BEST_LAP));
                        }
                    }
                }
            }
        }

        // enable user inputs plan is to add the available subscriptins for the current selected/played game here.
        private void OnEnableUserInput(object sender, OverlayElementClicked e)
        {
            inputsEnabled = e.enabled;
            if (inputsEnabled)
            {
                populateControlBox(e.graphics);
                displayModeControlBox.elementEnabled = true;
                if (OverlayController.mode == RenderMode.CHART || OverlayController.mode == RenderMode.ALL)
                {
                    chartModeControlBox.elementEnabled = true;
                    subscriptionModeControlBox.elementEnabled = true;
                    availableSubscriptionControlBox.elementEnabled = true;
                    zoomAndPanControlBox.elementEnabled = true;
                }

                this.overlayWindow.ActivateWindow();
            }
            else
            {
                displayModeControlBox.elementEnabled = false;
                chartModeControlBox.elementEnabled = false;
                subscriptionModeControlBox.elementEnabled = false;
                availableSubscriptionControlBox.elementEnabled = false;
                zoomAndPanControlBox.elementEnabled = false;
                this.overlayWindow.DeActivateWindow();
            }
            UpdateElementsPosition();
        }

        private void OnShowBestLap(object sender, OverlayElementClicked e)
        {
            showBestLap = !showBestLap;
            foreach (ElementCheckBox control in availableSubscriptionControlBox.children.Where(c => c.GetType() == typeof(ElementCheckBox)))
            {
                var overlaySubscription = OverlayDataSource.getOverlaySubscriptionForId(control.subscriptionDataField);
                if (control.enabled && overlaySubscription != null && showBestLap)
                {
                    Charts.addSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.BEST_LAP));
                }
                else if (overlaySubscription != null && !showBestLap && control.enabled)
                {
                    Charts.removeSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.BEST_LAP));
                }
            }
            CrewChiefOverlayWindow.createNewImage = true;
        }
        private void OnShowLastLap(object sender, OverlayElementClicked e)
        {
            showLastLap = !showLastLap;
            foreach (ElementCheckBox control in availableSubscriptionControlBox.children.Where(c => c.GetType() == typeof(ElementCheckBox)))
            {
                var overlaySubscription = OverlayDataSource.getOverlaySubscriptionForId(control.subscriptionDataField);
                if (control.enabled && overlaySubscription != null && showLastLap)
                {
                    Charts.addSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.LAST_LAP));
                }
                else if (overlaySubscription != null && !showLastLap && control.enabled)
                {
                    Charts.removeSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.LAST_LAP));
                }
            }
            CrewChiefOverlayWindow.createNewImage = true;
        }
        private void OnShowOpponentBestLap(object sender, OverlayElementClicked e)
        {
            showOpponentBestLap = !showOpponentBestLap;
            foreach (ElementCheckBox control in availableSubscriptionControlBox.children.Where(c => c.GetType() == typeof(ElementCheckBox)))
            {
                var overlaySubscription = OverlayDataSource.getOverlaySubscriptionForId(control.subscriptionDataField);
                if (overlaySubscription == null || overlaySubscription.opponentDataFieldname == null)
                {
                    continue;
                }
                if (control.enabled && showOpponentBestLap)
                {
                    Charts.addSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.OPPONENT_BEST_LAP));
                }
                else if (!showOpponentBestLap && control.enabled)
                {
                    Charts.removeSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, SeriesMode.OPPONENT_BEST_LAP));
                }
            }
            CrewChiefOverlayWindow.createNewImage = true;
        }
        private void MouseWheelOnImage(object sender, OverlayElementMouseWheel e)
        {
            if (e.UpDown < 0)
            {
                OverlayController.zoomOut();
            }
            else
            {
                OverlayController.zoomIn();
            }
        }
        private void OnZoomIn(object sender, OverlayElementClicked e)
        {
            OverlayController.zoomIn();
        }
        private void OnZoomOut(object sender, OverlayElementClicked e)
        {
            OverlayController.zoomOut();
        }
        private void OnPanLeft(object sender, OverlayElementClicked e)
        {
            OverlayController.panLeft();
        }
        private void OnPanRight(object sender, OverlayElementClicked e)
        {
            OverlayController.panRight();
        }
        private void ShowNextLap(object sender, OverlayElementClicked e)
        {
            OverlayController.showNextLap();
        }
        private void ShowPreviousLap(object sender, OverlayElementClicked e)
        {
            OverlayController.showPreviousLap();
        }
        private void OnReset(object sender, OverlayElementClicked e)
        {
            OverlayController.resetZoom();
            foreach (ElementRadioButton rb in subscriptionModeControlBox.children.Where(dm => dm.GetType() == typeof(ElementRadioButton)))
            {
                if (rb.title == "Show Full Lap")
                {
                    rb.enabled = true;
                }
                else
                {
                    rb.enabled = false;
                }
            }
        }
        private void MouseMButtonOnImage(object sender, OverlayElementClicked e)
        {
            OnReset(sender, null);
        }
        private void OnSetSectorOrLap(object sender, OverlayElementClicked e)
        {
            if (e.costumTextId == "0")
            {
                OverlayController.showSector(SectorToShow.ALL);
            }
            if (e.costumTextId == "1")
            {
                OverlayController.showSector(SectorToShow.SECTOR_1);
            }
            if (e.costumTextId == "2")
            {
                OverlayController.showSector(SectorToShow.SECTOR_2);
            }
            if (e.costumTextId == "3")
            {
                OverlayController.showSector(SectorToShow.SECTOR_3);
            }
        }
        private void OnSavePosition(object sender, OverlayElementClicked e)
        {
            settings.windowX = overlayWindow.X;
            settings.windowY = overlayWindow.Y;
            OverlaySettings.saveOverlaySetttings(overlayFileName, settings);
        }
        #endregion
    }

}

