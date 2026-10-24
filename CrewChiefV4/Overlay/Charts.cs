using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms.DataVisualization.Charting;
using System.Linq;
using CrewChiefV4.Events;

namespace CrewChiefV4.Overlay
{
    public class ChartContainer
    {
        public string subscriptionId;
        public byte[] data;
        public ChartContainer(string subscriptionId, byte[] data)
        {
            this.subscriptionId = subscriptionId;
            this.data = data;
        }
    }

    public class Charts
    {
        private static Color[] colours = new Color[] {
            Color.Green, Color.Red, Color.Purple, Color.Blue, Color.Cyan, Color.Orange, Color.Yellow,
            Color.BlueViolet, Color.Brown, Color.DarkGreen, Color.Magenta, Color.LimeGreen, Color.SlateGray,
            Color.HotPink, Color.DarkViolet, Color.PeachPuff, Color.YellowGreen, Color.RosyBrown, Color.SandyBrown
        };

        private static HashSet<Tuple<OverlaySubscription, SeriesMode>> activeSubscriptions = new HashSet<Tuple<OverlaySubscription, SeriesMode>>();

        private static float y_min;
        private static float y_max;
        private static float x_min;
        private static float x_max;

        public static bool hasHistogramSeriesSubs()
        {
            foreach (Tuple<OverlaySubscription, SeriesMode> activeSubscription in activeSubscriptions)
            {
                if (activeSubscription.Item1.dataSeriesType == DataSeriesType.HISTOGRAM)
                {
                    return true;

                }
            }
            return false;
        }
 
        public static bool hasTimeSeriesSubs()
        {
            foreach (Tuple<OverlaySubscription, SeriesMode> activeSubscription in activeSubscriptions)
            {
                if (activeSubscription.Item1.dataSeriesType == DataSeriesType.TIMESERIES)
                {
                    return true;

                }
            }
            return false;
        }

        public static List<ChartContainer> createOverlayChart(int width, int height, Boolean antiAliasing)
        {
            List<ChartContainer> chartContainers = new List<ChartContainer>();
            foreach (DataSeriesType type in Enum.GetValues(typeof(DataSeriesType)))
            {
                ChartContainer container = createTypedOverlayChart(width, height, antiAliasing, type);
                if (container != null)
                {
                    chartContainers.Add(container);
                }
            }
            return chartContainers;
        }

        public static List<ChartContainer> createStackedCharts(int width, int height, Boolean antiAliasing)
        {
            Color ForeColor = Color.FromArgb(CrewChiefOverlayWindow.colorScheme.fontColor.ToARGB());
            Color BackColor = Color.FromArgb(CrewChiefOverlayWindow.colorScheme.backgroundColor.ToARGB());
            List<ChartContainer> charts = new List<ChartContainer>();
            // Display a single empty chart if there are no data to display instead of just a blank screen.
            if (activeSubscriptions.Count == 0)
            {
                charts.Add(createTypedOverlayChart(width, height, antiAliasing, null));
                return charts;
            }
            HashSet<Tuple<OverlaySubscription, SeriesMode>> addedSubscriptions = new HashSet<Tuple<OverlaySubscription, SeriesMode>>();
            foreach (var subscription in activeSubscriptions)
            {
                Boolean isHistogram = false;
                string histogramXLabel = "";
                if (subscription.Item1.dataSeriesType == DataSeriesType.HISTOGRAM)
                {
                    histogramXLabel = subscription.Item1.histogramXLabel == null ? subscription.Item1.labels[0] : subscription.Item1.histogramXLabel;
                    isHistogram = true;
                }
                if (addedSubscriptions.Contains(subscription))
                {
                    continue;
                }
                addedSubscriptions.Add(subscription);

                Charts.y_min = float.MaxValue;
                Charts.y_max = float.MinValue;
                ChartArea chartArea1 = new ChartArea();
                chartArea1.BackColor = ForeColor;
                chartArea1.AxisX.LabelStyle.ForeColor = ForeColor;
                chartArea1.AxisY.LabelStyle.ForeColor = ForeColor;
                chartArea1.AxisX.MajorGrid.LineColor = BackColor;
                chartArea1.AxisY.MajorGrid.LineColor = BackColor;
                chartArea1.AxisX.Minimum = 0;

                Legend legend1 = new Legend();
                legend1.ForeColor = ForeColor;
                legend1.BackColor = BackColor;
                Chart chart1 = new Chart();
                ((System.ComponentModel.ISupportInitialize)(chart1)).BeginInit();
                chart1.BackColor = BackColor;
                chart1.ForeColor = ForeColor;
                chartArea1.Name = "ChartArea1";
                chart1.ChartAreas.Add(chartArea1);
                chart1.Dock = System.Windows.Forms.DockStyle.Fill;
                legend1.Name = "Legend1";
                chart1.Legends.Add(legend1);
                chart1.Location = new Point(0, 50);
                chart1.Name = "chart1";
                chart1.Size = new Size(width, height);
                chart1.TabIndex = 0;
                chart1.Text = "chart1";
                chart1.AntiAliasing = antiAliasing ? AntiAliasingStyles.All : AntiAliasingStyles.Text;
                ((System.ComponentModel.ISupportInitialize)(chart1)).EndInit();
                chart1.Series.Clear();

                string yAxisFormat = null;
                List<Series> seriesList = new List<Series>();
                seriesList.AddRange(createSeriesSet(subscription));
                yAxisFormat = subscription.Item1.yAxisFormat;

                //find matching best/last lap subscription 
                foreach (var sub in activeSubscriptions.Where(s => s.Item1.fieldName == subscription.Item1.fieldName && s.Item2 != subscription.Item2))
                {
                    if (addedSubscriptions.Contains(sub))
                    {
                        continue;
                    }
                    seriesList.AddRange(createSeriesSet(sub));
                    addedSubscriptions.Add(sub);
                }

                if (yAxisFormat == null)
                {
                    yAxisFormat = getYAxisFormat();
                }
                chartArea1.AxisY.LabelStyle.Format = yAxisFormat;
                if (isHistogram)
                {
                    chartArea1.AxisX.Title = histogramXLabel;
                    chartArea1.AxisX.LabelStyle.Format = "F3";
                    chartArea1.AxisY.LabelStyle.Format = "P";   // convert to percentage
                    double xmin = Double.Parse(x_min.ToString("G1"));
                    double xmax = Double.Parse(x_max.ToString("G1"));
                    // ensure a symmetrical range for charts crossing 0
                    if (xmin < 0 && xmax > 0)
                    {
                        double maxAndMin = Math.Max(xmax, xmin * 1);
                        chartArea1.AxisX.Minimum = maxAndMin * -1;
                        chartArea1.AxisX.Maximum = maxAndMin;
                        chart1.Annotations.Add(createZeroAnnotation(chartArea1));
                    }
                    else
                    {
                        chartArea1.AxisX.Minimum = xmin;
                        chartArea1.AxisX.Maximum = xmax;
                    }
                }
                else
                {
                    chartArea1.AxisX.Title = OverlayDataSource.xAxisType == X_AXIS_TYPE.DISTANCE ? "Distance (m)" : "Time (s)";
                    chartArea1.AxisX.LabelStyle.Format = "F0";
                    chartArea1.AxisX.Minimum = x_min;
                    chartArea1.AxisX.Maximum = x_max;
                }
                chartArea1.AxisX.TitleForeColor = ForeColor;
                if (y_max <= y_min)
                {
                    Console.WriteLine("Data series is incomplete");
                    y_max = y_min + 1;
                }
                chartArea1.AxisY.Maximum = y_max;
                chartArea1.AxisY.Minimum = y_min;
                int colourIndex = 0;
                foreach (Series series in seriesList)
                {
                    try
                    {
                        if (!chart1.Series.Contains(series))
                        {
                            if (series.Color == Color.White)
                            {
                                series.Color = colours[colourIndex];
                                colourIndex++;
                            }
                            chart1.Series.Add(series);
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Exception(e, "Unable to add series " + series.Name + ": ");
                    }
                }
                chartArea1.InnerPlotPosition = new ElementPosition(7, 10, 70, 60);
                chart1.Legends[0].Position = new ElementPosition(80, 0, 20, 100);
                if (!isHistogram)
                {
                    if (OverlayDataSource.sector1End > 0)
                        chart1.Annotations.Add(createSectorAnnotation(chartArea1, 1, OverlayDataSource.sector1End));
                    if (OverlayDataSource.sector2End > 0)
                        chart1.Annotations.Add(createSectorAnnotation(chartArea1, 2, OverlayDataSource.sector2End));
                }
                chart1.Invalidate();
                charts.Add(new ChartContainer(subscription.Item2.ToString() + " " + subscription.Item1.id, GetByteArrayForChart(chart1)));
            }            
            return charts;
        }

        private static ChartContainer createTypedOverlayChart(int width, int height, Boolean antiAliasing, DataSeriesType? type)
        {
            Color ForeColor = Color.FromArgb(CrewChiefOverlayWindow.colorScheme.fontColor.ToARGB());
            Color BackColor = Color.FromArgb(CrewChiefOverlayWindow.colorScheme.backgroundColor.ToARGB());
            Charts.y_min = float.MaxValue;
            Charts.y_max = float.MinValue;
            ChartArea chartArea1 = new ChartArea();
            chartArea1.BackColor = ForeColor;
            chartArea1.AxisX.LabelStyle.ForeColor = ForeColor;
            chartArea1.AxisY.LabelStyle.ForeColor = ForeColor;
            chartArea1.AxisX.MajorGrid.LineColor = BackColor;
            chartArea1.AxisY.MajorGrid.LineColor = BackColor;
            chartArea1.AxisX.Minimum = 0;
            Legend legend1 = new Legend();
            legend1.ForeColor = ForeColor;
            legend1.BackColor = BackColor;
            Chart chart1 = new Chart();
            ((System.ComponentModel.ISupportInitialize)(chart1)).BeginInit();
            chart1.BackColor = BackColor;
            chart1.ForeColor = ForeColor;
            chartArea1.Name = "ChartArea1";
            chart1.ChartAreas.Add(chartArea1);
            chart1.Dock = System.Windows.Forms.DockStyle.Fill;
            legend1.Name = "Legend1";
            chart1.Legends.Add(legend1);
            chart1.Location = new Point(0, 50);
            chart1.Name = "chart1";
            chart1.Size = new Size(width, height);
            chart1.TabIndex = 0;
            chart1.Text = "chart1";
            chart1.AntiAliasing = antiAliasing ? AntiAliasingStyles.All : AntiAliasingStyles.Text;
            ((System.ComponentModel.ISupportInitialize)(chart1)).EndInit();
            chart1.Series.Clear();

            List<Series> seriesList = new List<Series>();
            string yAxisFormat = null;
            string compoundId = "";
            Boolean addedFirst = false;
            Boolean isHistogram = false;
            string histogramXLabel = "";
            foreach (Tuple<OverlaySubscription, SeriesMode> subscription in Charts.activeSubscriptions)
            {
                if (subscription != null && (type == null || subscription.Item1.dataSeriesType == type.Value))
                {
                    if (subscription.Item1.dataSeriesType == DataSeriesType.HISTOGRAM)
                    {
                        isHistogram = true;
                        histogramXLabel = subscription.Item1.histogramXLabel == null ? subscription.Item1.labels[0] : subscription.Item1.histogramXLabel;
                    }
                    seriesList.AddRange(createSeriesSet(subscription));
                    if (addedFirst)
                    {
                        compoundId += ":";
                    }
                    compoundId += subscription.Item2.ToString() + " " + subscription.Item1.id;
                    yAxisFormat = subscription.Item1.yAxisFormat;
                    addedFirst = true;
                }
            }
            if (!addedFirst && type != null)
            {
                return null;
            }

            if (y_max <= y_min)
            {
                Console.WriteLine("Data series is incomplete");
                y_max = y_min + 1;
            }
            chartArea1.AxisY.Maximum = y_max;
            chartArea1.AxisY.Minimum = y_min;

            if (yAxisFormat == null)
            {
                yAxisFormat = getYAxisFormat();
            }
            chartArea1.AxisY.LabelStyle.Format = yAxisFormat;
            chartArea1.AxisX.LabelStyle.Format = "F0";
            if (isHistogram)
            {
                chartArea1.AxisX.Title = histogramXLabel;
                chartArea1.AxisX.LabelStyle.Format = "F3";
                chartArea1.AxisY.LabelStyle.Format = "P";   // convert to percentage
                double xmin = Double.Parse(x_min.ToString("G1"));
                double xmax = Double.Parse(x_max.ToString("G1"));
                // ensure a symmetrical range for charts crossing 0
                if (xmin < 0 && xmax > 0)
                {
                    double maxAndMin = Math.Max(xmax, xmin * 1);
                    chartArea1.AxisX.Minimum = maxAndMin * -1;
                    chartArea1.AxisX.Maximum = maxAndMin;
                    chart1.Annotations.Add(createZeroAnnotation(chartArea1));
                }
                else
                {
                    chartArea1.AxisX.Minimum = xmin;
                    chartArea1.AxisX.Maximum = xmax;
                }
            }
            else
            {
                chartArea1.AxisX.Title = OverlayDataSource.xAxisType == X_AXIS_TYPE.DISTANCE ? "Distance (m)" : "Time (s)";
                chartArea1.AxisX.LabelStyle.Format = "F0";
                chartArea1.AxisX.Minimum = x_min;
                chartArea1.AxisX.Maximum = x_max;
            }
            chartArea1.AxisX.TitleForeColor = ForeColor;
            int colourIndex = 0;
            foreach (Series series in seriesList)
            {
                try
                {
                    if (!chart1.Series.Contains(series))
                    {
                        if (series.Color == Color.White)
                        {
                            series.Color = colours[colourIndex];
                            colourIndex++;
                        }
                        chart1.Series.Add(series);
                    }
                }
                catch (Exception e)
                {
                    Log.Exception(e, "Unable to add series " + series.Name + ": ");
                }
            }
            chartArea1.InnerPlotPosition = new ElementPosition(7, 10, 70, 60);
            chart1.Legends[0].Position = new ElementPosition(80, 0, 20, 100);
            if (!isHistogram)
            {
                if (OverlayDataSource.sector1End > 0)
                    chart1.Annotations.Add(createSectorAnnotation(chartArea1, 1, OverlayDataSource.sector1End));
                if (OverlayDataSource.sector2End > 0)
                    chart1.Annotations.Add(createSectorAnnotation(chartArea1, 2, OverlayDataSource.sector2End));
            }
            chart1.Invalidate();
            return new ChartContainer(compoundId, GetByteArrayForChart(chart1));
        }

        private static VerticalLineAnnotation createSectorAnnotation(ChartArea chartArea, int sectorNumber, float sectorPoint)
        {
            var sectorAnnotation = new VerticalLineAnnotation();
            sectorAnnotation.AxisX = chartArea.AxisX;
            sectorAnnotation.Name = "S" + sectorNumber;
            sectorAnnotation.X = sectorPoint;
            sectorAnnotation.LineColor = Color.Gray;
            sectorAnnotation.LineDashStyle = ChartDashStyle.Dash;
            sectorAnnotation.LineWidth = 2;
            sectorAnnotation.IsInfinitive = true;
            sectorAnnotation.ClipToChartArea = chartArea.Name;
            return sectorAnnotation;
        }

        private static VerticalLineAnnotation createZeroAnnotation(ChartArea chartArea)
        {
            var zeroAnnotation = new VerticalLineAnnotation();
            zeroAnnotation.AxisX = chartArea.AxisX;
            zeroAnnotation.Name = "0";
            zeroAnnotation.X = 0.0f;
            zeroAnnotation.LineColor = Color.Black;
            zeroAnnotation.LineDashStyle = ChartDashStyle.Dash;
            zeroAnnotation.LineWidth = 1;
            zeroAnnotation.IsInfinitive = true;
            zeroAnnotation.ClipToChartArea = chartArea.Name;
            return zeroAnnotation;
        }

        private static string getYAxisFormat()
        {
            if (y_max > 1000)
            {
                return "F0";
            }
            else if (y_max > 100)
            {
                return "F1";
            }
            else if (y_max > 1)
            {
                return "F2";
            }
            else if (y_max > 0.1)
            {
                return "F3";
            }
            else if (y_max > 0.01)
            {
                return "F4";
            }
            else
            {
                return "F5";
            }
        }

        public static void clearSeries()
        {
            Charts.activeSubscriptions.Clear();
        }

        public static void addSeries(Tuple<OverlaySubscription, SeriesMode> subscription)
        {
            Charts.activeSubscriptions.Add(subscription);
        }

        public static void removeSeries(Tuple<OverlaySubscription, SeriesMode> subscription)
        {
            Charts.activeSubscriptions.Remove(subscription);
        }

        private static List<Series> createSeriesSet(Tuple<OverlaySubscription, SeriesMode> overlaySubscription)
        {
            if (overlaySubscription.Item1.isGroup)
            {
                List<Series> groupSeries = new List<Series>();
                foreach (string id in overlaySubscription.Item1.groupMemberIds)
                {
                    OverlaySubscription subscription = OverlayDataSource.getOverlaySubscriptionForId(id);
                    if (subscription != null)
                    {
                        groupSeries.AddRange(createSeriesSet(new Tuple<OverlaySubscription, SeriesMode>(subscription, overlaySubscription.Item2)));
                    }
                }
                return groupSeries;
            }
            else
            {
                return createSeries(overlaySubscription);
            }
        }

        public static ChartContainer createWorldPositionSeries(SeriesMode seriesMode, int height)
        {
            List<DataPoint> dataPoints = OverlayDataSource.getWorldPositions(seriesMode);
            if (dataPoints != null)
            {
                var series = new Series
                {
                    Name = "Map",
                    IsVisibleInLegend = false,
                    IsXValueIndexed = false,
                    ChartType = SeriesChartType.Spline,
                    Color = Color.Red
                };
                float xmin = 0;
                float xmax = 0;
                float ymin = 0;
                float ymax = 0;
                foreach (DataPoint dataPoint in dataPoints)
                {
                    float[] coordinates = (float[]) dataPoint.datum;
                    float x = coordinates[0];
                    float y = coordinates[2];
                    float distanceRoundTrack = dataPoint.distanceRoundTrack;
                    series.Points.AddXY(x, y);
                    if (x < xmin)
                    {
                        xmin = x;
                    }
                    if (y < ymin)
                    {
                        ymin = y;
                    }
                    if (x > xmax)
                    {
                        xmax = x;
                    }
                    if (y > ymax)
                    {
                        ymax = y;
                    }
                    if (distanceRoundTrack < OverlayController.x_min || distanceRoundTrack > OverlayController.x_max)
                    {
                        series.Points[series.Points.Count - 1].Color = Color.Gray;
                    }
                }
                Color ForeColor = Color.FromArgb(CrewChiefOverlayWindow.colorScheme.fontColor.ToARGB());
                Color BackColor = Color.FromArgb(CrewChiefOverlayWindow.colorScheme.backgroundColor.ToARGB());
                ChartArea chartArea = new ChartArea();
                chartArea.BackColor = ForeColor;
                chartArea.AxisX.LabelStyle.ForeColor = ForeColor;
                chartArea.AxisY.LabelStyle.ForeColor = ForeColor;
                chartArea.AxisX.MajorGrid.LineColor = BackColor;
                chartArea.AxisY.MajorGrid.LineColor = BackColor;

                float xrange = xmax - xmin;
                float yrange = ymax - ymin;
                OverlayController.mapXSizeScale = xrange / yrange;
                chartArea.AxisY.Maximum = ymax + 10;
                chartArea.AxisY.Minimum = ymin - 10;
                chartArea.AxisX.Minimum = xmin - 10;
                chartArea.AxisX.Maximum = xmax + 10;
                chartArea.AxisY.Enabled = AxisEnabled.False;
                chartArea.AxisX.Enabled = AxisEnabled.False;

                Chart chart = new Chart();
                ((System.ComponentModel.ISupportInitialize)(chart)).BeginInit();
                chart.BackColor = BackColor;
                chart.ForeColor = ForeColor;
                chartArea.Name = "Map";
                chart.ChartAreas.Add(chartArea);
                chart.Dock = System.Windows.Forms.DockStyle.Fill;
                chart.Name = "Map";
                chart.Size = new Size((int) (height * OverlayController.mapXSizeScale), height);
                chart.TabIndex = 0;
                chart.Text = "Map";
                ((System.ComponentModel.ISupportInitialize)(chart)).EndInit();
                chart.Series.Clear();
                chart.Series.Add(series);
                return new ChartContainer("Map", GetByteArrayForChart(chart));
            }
            return null;
        }

        private static List<Series> createSeries(Tuple<OverlaySubscription, SeriesMode> overlaySubscription)
        {
            List<Series> seriesList = new List<Series>();
            try
            {
                List<Tuple<float, float[]>> data = OverlayDataSource.getDataForLap(overlaySubscription, OverlayController.sectorToShow);

                // set up the axes from the first data point if we have one
                if (data.Count > 0)
                {
                    int bestLapColourIndex = 0;
                    int lastLapColourIndex = 0;
                    int opponentBestLapColourIndex = 0;
                    String laptimeString = "--:--:---";
                    switch (overlaySubscription.Item2)
                    {
                        case SeriesMode.LAST_LAP:
                            laptimeString = OverlayDataSource.getLapTimeForLastLapString();
                            break;
                        case SeriesMode.BEST_LAP:
                            laptimeString = OverlayDataSource.getLapTimeForBestLapString();
                            break;
                        case SeriesMode.OPPONENT_BEST_LAP:
                            if (OverlayDataSource.bestOpponentLap > 0 && OverlayDataSource.bestOpponentLap < 10000)
                            {
                                laptimeString = OverlayDataSource.bestOpponentLapDriverName + "\n" + TimeSpan.FromSeconds(OverlayDataSource.bestOpponentLap).ToString(@"mm\:ss\.fff");
                            }
                            break;
                    }
                    OverlayDataSource.getLapTimeForBestLapString();
                    Boolean addedLaptime = false;
                    if (overlaySubscription.Item1.dataSeriesType == DataSeriesType.HISTOGRAM)
                    {
                        x_min = float.MaxValue;
                    }
                    x_max = float.MinValue;
                    for (int i = 0; i < data[0].Item2.Length; i++)
                    {
                        string name;
                        if (!addedLaptime)
                        {
                            name = overlaySubscription.Item1.labels[0] + " " + Configuration.getUIString("chart_label_" + overlaySubscription.Item2) + ",\n" + laptimeString;
                            addedLaptime = true;
                        }
                        else
                        {
                            name = overlaySubscription.Item1.labels[0] + " " + Configuration.getUIString("chart_label_" + overlaySubscription.Item2);
                        }
                        var series = new Series
                        {
                            Name = name,
                            IsVisibleInLegend = true,
                            IsXValueIndexed = false,
                            ChartType = SeriesChartType.Line,
                            /* white ==> needs to be auto-set*/
                            Color = Color.White,
                        };
                        if (overlaySubscription.Item2 == SeriesMode.LAST_LAP && overlaySubscription.Item1.coloursLastLap_Internal.Count() > i)
                        {
                            try
                            {
                                series.Color = Color.FromName(overlaySubscription.Item1.coloursLastLap_Internal[lastLapColourIndex]);
                            }
                            catch (Exception e) {Log.Exception(e);}
                            lastLapColourIndex++;
                        }
                        else if (overlaySubscription.Item2 == SeriesMode.BEST_LAP && overlaySubscription.Item1.coloursBestLap_Internal.Count() > i)
                        {
                            try
                            {
                                series.Color = Color.FromName(overlaySubscription.Item1.coloursBestLap_Internal[bestLapColourIndex]);
                            }
                            catch (Exception e) {Log.Exception(e);}
                            bestLapColourIndex++;
                        }
                        else if (overlaySubscription.Item2 == SeriesMode.OPPONENT_BEST_LAP && overlaySubscription.Item1.coloursOpponentBestLap_Internal.Count() > i)
                        {
                            try
                            {
                                series.Color = Color.FromName(overlaySubscription.Item1.coloursOpponentBestLap_Internal[opponentBestLapColourIndex]);
                            }
                            catch (Exception e) {Log.Exception(e);}
                            opponentBestLapColourIndex++;
                        }
                        bool autoScaleMin = true;
                        bool autoScaleMax = true;
                        if (overlaySubscription.Item1.yAxisMinScaling == YAxisScaling.MANUAL)
                        {
                            if (overlaySubscription.Item1.yMin < y_min)
                            {
                                y_min = overlaySubscription.Item1.yMin;
                            }
                            autoScaleMin = false;
                        }
                        if (overlaySubscription.Item1.yAxisMaxScaling == YAxisScaling.MANUAL)
                        {
                            if (overlaySubscription.Item1.yMax > y_max)
                            {
                                y_max = overlaySubscription.Item1.yMax;
                            }
                            autoScaleMax = false;
                        }
                        bool gotXMin = false;
                        foreach (Tuple<float, float[]> overlayDataPoint in data)
                        {
                            if (overlayDataPoint.Item1 > x_max)
                            {
                                x_max = overlayDataPoint.Item1;
                            }
                            if (overlaySubscription.Item1.dataSeriesType == DataSeriesType.HISTOGRAM)
                            {
                                if (overlayDataPoint.Item1 < x_min)
                                {
                                    x_min = overlayDataPoint.Item1;
                                }
                            }
                            else if (!gotXMin)
                            {
                                x_min = overlayDataPoint.Item1;
                                // special case for when we're at the start of the range - ensure the first X point is actually 0
                                if (x_min < 10)
                                {
                                    x_min = 0;
                                }
                                gotXMin = true;
                            }
                            if (autoScaleMax && overlayDataPoint.Item2[i] > y_max)
                            {
                                y_max = overlayDataPoint.Item2[i];
                            }
                            if (autoScaleMin && overlayDataPoint.Item2[i] < y_min)
                            {
                                y_min = overlayDataPoint.Item2[i];
                            }
                            series.Points.AddXY((float)overlayDataPoint.Item1, overlayDataPoint.Item2[i]);
                        }
                        seriesList.Add(series);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Exception(e, "Error creating series for " + overlaySubscription.Item1.fieldName + ", " + overlaySubscription.Item2 + ": ");
            }
            return seriesList;
        }

        // convert the chart obj to a rendered BMP in a byte[]
        private static byte[] GetByteArrayForChart(Chart chart)
        {
            MemoryStream stream = new MemoryStream();
            chart.SaveImage(stream, ChartImageFormat.Bmp);
            byte[] bytes = stream.ToArray();
            stream.Dispose();
            return bytes;
        }
    }
}