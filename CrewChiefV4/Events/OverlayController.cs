using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CrewChiefV4.Audio;
using CrewChiefV4.GameState;
using CrewChiefV4.Overlay;

namespace CrewChiefV4.Events
{
    // WIP control class for interpreting SRE command and whatever to manage the overlay
    // Just a collection of ideas at the moment

    class OverlayController : AbstractEvent
    {
        public static bool shown = UserSettings.GetUserSettings().getBoolean("enable_overlay_window");
        public static RenderMode mode = RenderMode.CONSOLE;
        public static ChartRenderMode chartRenderMode = ChartRenderMode.STACKED;
        public static SectorToShow sectorToShow = SectorToShow.ALL;
        private readonly Boolean consoleOverlayEnabled = UserSettings.GetUserSettings().getBoolean("enable_overlay_window");
        private readonly Boolean iRacingDiskTelemetryLogginEnabled = UserSettings.GetUserSettings().getBoolean("iracing_enable_disk_based_telemetry");
        private readonly Boolean automaticDiskTelemetryRecordingEnabled = UserSettings.GetUserSettings().getBoolean("enable_automatic_disk_telemetry_recording");
        iRSDKSharp.iRacingDiskSDK iRacingDiskTelemetry = null;
        private bool isLoggingDiskData = false;
        public static float x_min = -1;
        public static float x_max = -1;
        public static float clampXMaxTo = -1;

        public static int histogramZoomLevel = 1;

        public static Boolean showMap = false;
        public static float mapXSizeScale = 1;

        public OverlayController(AudioPlayer audioPlayer)
        {
            this.audioPlayer = audioPlayer;
        }

        public override void clearState()
        {
            OverlayDataSource.clearData();
        }

        override protected void triggerInternal(GameStateData previousGameState, GameStateData currentGameState)
        {
            if(!consoleOverlayEnabled)
            {
                return;
            }
            if (Game.IRACING)
            {
                if (currentGameState == null)
                {
                    return;
                }
                var rawData = currentGameState.rawGameData as CrewChiefV4.iRacing.iRacingSharedMemoryReader.iRacingStructWrapper;
                if (iRacingDiskTelemetryLogginEnabled)
                {
                    if (iRacingDiskTelemetry == null)
                    {
                        iRacingDiskTelemetry = new iRSDKSharp.iRacingDiskSDK();
                        if(automaticDiskTelemetryRecordingEnabled)
                        {
                            iRSDKSharp.iRacingSDK.BroadcastMessage(iRSDKSharp.BroadcastMessageTypes.TelemCommand, (int)iRSDKSharp.TelemCommandModeTypes.Start, 0);
                        }                        
                    }
                    if(currentGameState.PitData.JumpedToPits)
                    {
                        iRSDKSharp.iRacingSDK.BroadcastMessage(iRSDKSharp.BroadcastMessageTypes.TelemCommand, (int)iRSDKSharp.TelemCommandModeTypes.Stop, 0);
                    }
                    if (rawData.data.Telemetry.IsDiskLoggingActive && !isLoggingDiskData)
                    {
                        isLoggingDiskData = rawData.data.Telemetry.IsDiskLoggingActive;
                    }
                    else if (!rawData.data.Telemetry.IsDiskLoggingActive && isLoggingDiskData)
                    {
                        iRacingDiskTelemetry.ReadFileData(rawData.data.SessionId, rawData.data.SubSessionId);
                        isLoggingDiskData = false;
                    }
                    if (iRacingDiskTelemetry.iRacingDiskDataReady.WaitOne(0))
                    {
                        if (iRacingDiskTelemetry.hasNewLapData)
                        {
                            OverlayDataSource.addIRacingDiskData(iRacingDiskTelemetry, currentGameState);
                            iRacingDiskTelemetry.ClearData();
                            if (automaticDiskTelemetryRecordingEnabled)
                            {
                                iRSDKSharp.iRacingSDK.BroadcastMessage(iRSDKSharp.BroadcastMessageTypes.TelemCommand, (int)iRSDKSharp.TelemCommandModeTypes.Restart, 0);
                            }
                        }
                    }
                }
                // special case for iRacing because of data alignment issues. Probably need a similar workaround for AC and ACC
                OverlayDataSource.addIRacingData(previousGameState, currentGameState);
            }
            else
            {
                OverlayDataSource.addGameData(previousGameState, currentGameState);
            }
        }

        private static readonly List<SpeechCommands.ID> Commands = new List<SpeechCommands.ID>
        {
            SpeechCommands.ID.CHART_COMMAND_ADD,
            SpeechCommands.ID.CHART_COMMAND_PAN_LEFT,
            SpeechCommands.ID.CHART_COMMAND_PAN_RIGHT,
            SpeechCommands.ID.CHART_COMMAND_REMOVE,
            SpeechCommands.ID.CHART_COMMAND_RESET_ZOOM,
            SpeechCommands.ID.CHART_COMMAND_SHOW_ALL_SECTORS,
            SpeechCommands.ID.CHART_COMMAND_SHOW_LAST_LAP,
            SpeechCommands.ID.CHART_COMMAND_SHOW_NEXT_LAP,
            SpeechCommands.ID.CHART_COMMAND_SHOW_PREVIOUS_LAP,
            SpeechCommands.ID.CHART_COMMAND_SHOW_SECTOR_1,
            SpeechCommands.ID.CHART_COMMAND_SHOW_SECTOR_2,
            SpeechCommands.ID.CHART_COMMAND_SHOW_SECTOR_3,
            SpeechCommands.ID.CHART_COMMAND_ZOOM_IN,
            SpeechCommands.ID.CHART_COMMAND_ZOOM_OUT,
            SpeechCommands.ID.CLEAR_CHART,
            SpeechCommands.ID.CLEAR_DATA,
            SpeechCommands.ID.HIDE_CHART,
            SpeechCommands.ID.HIDE_CONSOLE,
            SpeechCommands.ID.HIDE_OVERLAY,
            SpeechCommands.ID.REFRESH_CHART,
            SpeechCommands.ID.SHOW_All_OVERLAYS,
            SpeechCommands.ID.SHOW_CHART,
            SpeechCommands.ID.SHOW_CONSOLE,
            SpeechCommands.ID.SHOW_DISTANCE,
            SpeechCommands.ID.SHOW_OVERLAY,
            SpeechCommands.ID.SHOW_SINGLE_CHART,
            SpeechCommands.ID.SHOW_STACKED_CHARTS,
            SpeechCommands.ID.SHOW_TIME,
        };
        public override SpeechCommands.ID HandlesEvent(String voiceMessage)
        {
            return SpeechCommands.SpeechToCommand(Commands, voiceMessage);
        }

        public override void respond(String voiceMessage)
        {
            respond(voiceMessage, HandlesEvent(voiceMessage));
        }
        public override void respond(String voiceMessage, SpeechCommands.ID cmd)
        {
            switch (cmd)
            {
                case SpeechCommands.ID.CHART_COMMAND_ADD:
                case SpeechCommands.ID.CHART_COMMAND_REMOVE:
                {
                    foreach (OverlaySubscription overlaySubscription in OverlayDataSource.getOverlaySubscriptions())
                    {
                        foreach (string voiceCommand in overlaySubscription.getVoiceCommands())
                        {
                            if (voiceMessage == voiceCommand)
                            {
                                // nasty...
                                SeriesMode seriesMode = SeriesMode.LAST_LAP;
                                Boolean isAdd = true;
                                foreach (string bestFragment in SpeechRecogniser.CHART_COMMAND_BEST_LAP)
                                {
                                    if (voiceMessage.Contains(bestFragment))
                                    {
                                        seriesMode = SeriesMode.BEST_LAP;
                                    }
                                }
                                foreach (string bestFragment in SpeechRecogniser.CHART_COMMAND_OPPONENT_BEST_LAP)
                                {
                                    if (voiceMessage.Contains(bestFragment))
                                    {
                                        seriesMode = SeriesMode.OPPONENT_BEST_LAP;
                                    }
                                }
                                foreach (string removeFragment in SpeechRecogniser.CHART_COMMAND_REMOVE)
                                {
                                    if (voiceMessage.Contains(removeFragment))
                                    {
                                        isAdd = false;
                                    }
                                }
                                if (isAdd)
                                {
                                    addChartData(overlaySubscription, seriesMode);
                                }
                                else
                                {
                                    removeChartData(overlaySubscription, seriesMode);
                                }
                                return;
                            }
                        }
                    }

                    break;
                }
                case SpeechCommands.ID.HIDE_OVERLAY:
                    hideOverlay();
                    break;
                case SpeechCommands.ID.SHOW_All_OVERLAYS:
                    showAll();
                    refreshChart();
                    break;
                case SpeechCommands.ID.SHOW_OVERLAY:
                    showOverlay();
                    break;
                case SpeechCommands.ID.SHOW_CONSOLE:
                    showConsole();
                    break;
                case SpeechCommands.ID.SHOW_CHART:
                    showChart();
                    break;
                case SpeechCommands.ID.CLEAR_CHART:
                    clearChart();
                    x_max = -1;
                    x_min = -1;
                    sectorToShow = SectorToShow.ALL;
                    showMap = false;
                    break;
                case SpeechCommands.ID.SHOW_STACKED_CHARTS:
                    showStackedCharts();
                    break;
                case SpeechCommands.ID.SHOW_SINGLE_CHART:
                    showSingleChart();
                    break;
                case SpeechCommands.ID.REFRESH_CHART:
                    refreshChart();
                    break;
                case SpeechCommands.ID.CLEAR_DATA:
                    clearChart();
                    OverlayDataSource.clearData();
                    x_max = -1;
                    x_min = -1;
                    sectorToShow = SectorToShow.ALL;
                    showMap = false;
                    break;
                case SpeechCommands.ID.SHOW_TIME:
                    OverlayDataSource.xAxisType = X_AXIS_TYPE.TIME;
                    refreshChart();
                    break;
                case SpeechCommands.ID.SHOW_DISTANCE:
                    OverlayDataSource.xAxisType = X_AXIS_TYPE.DISTANCE;
                    refreshChart();
                    break;
                case SpeechCommands.ID.HIDE_CHART:
                {
                    if (OverlayController.mode == RenderMode.ALL || OverlayController.mode == RenderMode.CONSOLE)
                    {
                        showConsole();
                    }
                    else
                    {
                        hideOverlay();
                    }

                    break;
                }
                case SpeechCommands.ID.HIDE_CONSOLE:
                {
                    if (OverlayController.mode == RenderMode.ALL || OverlayController.mode == RenderMode.CHART)
                    {
                        showChart();
                    }
                    else
                    {
                        hideOverlay();
                    }

                    break;
                }
                case SpeechCommands.ID.CHART_COMMAND_SHOW_ALL_SECTORS:
                    showMap = false;
                    showSector(SectorToShow.ALL);
                    break;
                case SpeechCommands.ID.CHART_COMMAND_SHOW_SECTOR_1:
                    showMap = true;
                    showSector(SectorToShow.SECTOR_1);
                    break;
                case SpeechCommands.ID.CHART_COMMAND_SHOW_SECTOR_2:
                    showMap = true;
                    showSector(SectorToShow.SECTOR_2);
                    break;
                case SpeechCommands.ID.CHART_COMMAND_SHOW_SECTOR_3:
                    showMap = true;
                    showSector(SectorToShow.SECTOR_3);
                    break;
                case SpeechCommands.ID.CHART_COMMAND_ZOOM_IN:
                    zoomIn();
                    break;
                case SpeechCommands.ID.CHART_COMMAND_ZOOM_OUT:
                    zoomOut();
                    break;
                case SpeechCommands.ID.CHART_COMMAND_RESET_ZOOM:
                    resetZoom();
                    break;
                case SpeechCommands.ID.CHART_COMMAND_PAN_RIGHT:
                    panRight();
                    break;
                case SpeechCommands.ID.CHART_COMMAND_PAN_LEFT:
                    panLeft();
                    break;
                case SpeechCommands.ID.CHART_COMMAND_SHOW_LAST_LAP:
                    showLastLap();
                    break;
                case SpeechCommands.ID.CHART_COMMAND_SHOW_NEXT_LAP:
                    showNextLap();
                    break;
                case SpeechCommands.ID.CHART_COMMAND_SHOW_PREVIOUS_LAP:
                    showPreviousLap();
                    break;
            }
        }

        public static void showSector(SectorToShow sectorToShow)
        {
            x_max = -1;
            x_min = -1;
            OverlayController.sectorToShow = sectorToShow;
            refreshChart();
        }

        public static void zoomIn()
        {
            float zoomAmount = (x_max - x_min) / 4;
            if (zoomAmount > 10)
            {
                x_min = Math.Max(0, x_min + zoomAmount);
                x_max = Math.Max(x_min, x_max - zoomAmount);
                showMap = true;
            }
            if (histogramZoomLevel < 6)
            {
                histogramZoomLevel++;
            }
            refreshChart();
        }

        public static void zoomOut()
        {
            if (histogramZoomLevel > 1)
            {
                histogramZoomLevel--;
            }
            if (x_max < clampXMaxTo || x_min > 0)
            {
                float zoomAmount = (x_max - x_min) / 2;
                x_max = Math.Min(clampXMaxTo, x_max + zoomAmount);
                x_min = Math.Max(0, x_min - zoomAmount);
                if (x_min < 10)
                {
                    x_min = 0;
                }
                if (x_min == 0 && x_max == clampXMaxTo)
                {
                    showMap = false;
                }
            }
            refreshChart();
        }

        public static void panRight()
        {
            if (x_max < clampXMaxTo)
            {
                float moveAmount = (x_max - x_min) * 0.5f;
                if (x_max + moveAmount > clampXMaxTo)
                {
                    moveAmount = clampXMaxTo - x_max;
                }
                x_min += moveAmount;
                x_max += moveAmount;
                refreshChart();
            }
        }

        public static void panLeft()
        {
            if (x_min > 0)
            {
                float moveAmount = (x_max - x_min) * 0.5f;
                if (x_min - moveAmount < 0)
                {
                    moveAmount = x_min;
                }
                x_min = x_min - moveAmount;
                if (x_min < 10)
                {
                    x_min = 0;
                }
                x_max = x_max - moveAmount;
                refreshChart();
            }
        }

        public static void resetZoom()
        {
            OverlayController.sectorToShow = SectorToShow.ALL;
            x_max = -1;
            x_min = -1;
            showMap = false;
            refreshChart();
        }

        public static void showLastLap()
        {
            OverlayDataSource.countBack = 1;
            refreshChart();
        }

        public static void showPreviousLap()
        {
            OverlayDataSource.countBack++;
            refreshChart();
        }

        public static void showNextLap()
        {
            if (OverlayDataSource.countBack > 0)
            {
                OverlayDataSource.countBack--;
                refreshChart();
            }
        }

        public static void showOverlay()
        {
            OverlayController.shown = true;
        }

        public static void hideOverlay()
        {
            OverlayController.shown = false;
        }

        public static void showConsole()
        {
            OverlayController.mode = RenderMode.CONSOLE;
            OverlayController.shown = true;
        }

        public static void showChart()
        {
            OverlayController.mode = RenderMode.CHART;
            OverlayController.shown = true;
        }

        public static void showAll()
        {
            OverlayController.mode = RenderMode.ALL;
            OverlayController.shown = true;
        }

        public static void clearChart()
        {
            Charts.clearSeries();
            CrewChiefOverlayWindow.createNewImage = true;
        }

        public static void refreshChart()
        {
            CrewChiefOverlayWindow.createNewImage = true;
        }

        public static void showStackedCharts()
        {
            if(chartRenderMode == ChartRenderMode.SINGLE && OverlayController.shown)
            {
                CrewChiefOverlayWindow.createNewImage = true;
            }
            chartRenderMode = ChartRenderMode.STACKED;
        }

        public static void showSingleChart()
        {
            if (chartRenderMode == ChartRenderMode.STACKED && OverlayController.shown)
            {
                CrewChiefOverlayWindow.createNewImage = true;
            }
            chartRenderMode = ChartRenderMode.SINGLE;
        }

        private static void addChartData(OverlaySubscription overlaySubscription, SeriesMode seriesMode)
        {
            Charts.addSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, seriesMode));
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
        }

        private static void removeChartData(OverlaySubscription overlaySubscription, SeriesMode seriesMode)
        {
            Charts.removeSeries(new Tuple<OverlaySubscription, SeriesMode>(overlaySubscription, seriesMode));
            CrewChiefOverlayWindow.createNewImage = true;
        }
    }
}
