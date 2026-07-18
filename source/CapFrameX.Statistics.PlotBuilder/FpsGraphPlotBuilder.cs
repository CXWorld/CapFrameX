using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Statistics.PlotBuilder
{
    public class FpsGraphPlotBuilder : PlotBuilder
    {
        public FpsGraphPlotBuilder(IFrametimeStatisticProviderOptions options, IStatisticProvider frametimeStatisticProvider) : base(options, frametimeStatisticProvider) { }

        public void BuildPlotmodel(ISession session, IPlotSettings plotSettings, double startTime, double endTime, ERemoveOutlierMethod eRemoveOutlinerMethod, EFilterMode filterMode, Action<PlotModel> onFinishAction = null)
        {
            var plotModel = PlotModel;
            Reset(false);

            if (session == null)
            {
                // Reset(false) skipped the redraw; render the cleared model so no
                // stale chart from a previously selected record stays on screen.
                plotModel.InvalidatePlot(true);
                return;
            }

            plotModel.Axes.Add(AxisDefinitions[EPlotAxis.XAXIS]);
            plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISFPS]);

            var useDisplayTimes = plotSettings.ShowDisplayTimes;
            var timingValues = useDisplayTimes
                ? session.GetDisplayChangeTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod)
                : session.GetFrametimeTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod);

            // PresentMon does not expose display-change data for every graphics API.
            // Match the current capture pipeline behavior and fall back to presents.
            if (useDisplayTimes && timingValues.Count == 0)
            {
                useDisplayTimes = false;
                timingValues = session.GetFrametimeTimeWindow(startTime, endTime,
                    _frametimeStatisticProviderOptions, eRemoveOutlinerMethod);
            }

            if (timingValues.Count == 0)
            {
                plotModel.InvalidatePlot(true);
                return;
            }

            double average = timingValues.Count * 1000 / timingValues.Sum();
            double yMin, yMax;

            plotModel.Series.Clear();

            var rawFpsPoints = GetFpsPoints(session, useDisplayTimes, startTime, endTime,
                eRemoveOutlinerMethod, EFilterMode.None);
            IList<Point> gpuActiveFpsPoints = new List<Point>();

            if (filterMode is EFilterMode.RawPlusAverage)
            {
                var avgFpsPoints = GetFpsPoints(session, useDisplayTimes, startTime, endTime,
                    eRemoveOutlinerMethod, EFilterMode.TimeIntervalAverage);

                if (rawFpsPoints.Count == 0 || avgFpsPoints.Count == 0)
                {
                    plotModel.InvalidatePlot(true);
                    return;
                }

                //if (plotSettings.ShowGpuActiveCharts)
                //    gpuActiveFpsPoints = session.GetGpuActiveFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod, filterMode);

                SetRawFPS(plotModel, rawFpsPoints, useDisplayTimes);
                SetLoadCharts(plotModel, plotSettings, session, startTime, endTime);
                SetFpsChart(plotModel, avgFpsPoints, rawFpsPoints, gpuActiveFpsPoints, average, 3,
                    OxyColor.FromRgb(241, 125, 32), filterMode, plotSettings, useDisplayTimes);

                yMin = rawFpsPoints.Min(pnt => pnt.Y);
                yMax = rawFpsPoints.Max(pnt => pnt.Y);
            }
            else
            {
                var fpsPoints = GetFpsPoints(session, useDisplayTimes, startTime, endTime,
                    eRemoveOutlinerMethod, filterMode);
                if (fpsPoints.Count == 0)
                {
                    plotModel.InvalidatePlot(true);
                    return;
                }

                //if (plotSettings.ShowGpuActiveCharts)
                //    gpuActiveFpsPoints = session.GetGpuActiveFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod, filterMode);

                if (filterMode == EFilterMode.TimeIntervalAverage)
                    SetLoadCharts(plotModel, plotSettings, session, startTime, endTime);

                SetFpsChart(plotModel, fpsPoints, rawFpsPoints, gpuActiveFpsPoints, average,
                    filterMode is EFilterMode.None ? 1.5 : 3, Constants.FpsColor, filterMode,
                    plotSettings, useDisplayTimes);


                if (filterMode is EFilterMode.None)
                    SetLoadCharts(plotModel, plotSettings, session, startTime, endTime);

                yMin = fpsPoints.Min(pnt => pnt.Y);
                yMax = fpsPoints.Max(pnt => pnt.Y);

                //if (plotSettings.ShowGpuActiveCharts)
                //{
                //    yMin = Math.Min(fpsPoints.Min(pnt => pnt.Y), gpuActiveFpsPoints.Min(pnt => pnt.Y));
                //    yMax = Math.Max(fpsPoints.Max(pnt => pnt.Y), gpuActiveFpsPoints.Max(pnt => pnt.Y));
                //}
                //else
                //{
                //    yMin = fpsPoints.Min(pnt => pnt.Y);
                //    yMax = fpsPoints.Max(pnt => pnt.Y);
                //}
            }

            if (plotSettings.ShowThresholds)
            {
                SetThresholdChart(plotModel, plotSettings, rawFpsPoints);
                yMin = Math.Min(plotSettings.LowFPSThreshold, yMin);
            }

            UpdateYAxisMinMaxBorders(yMin, yMax, average);
            SetAggregationSeparators(session, plotModel, plotSettings.ShowAggregationSeparators);

            onFinishAction?.Invoke(plotModel);
            plotModel.InvalidatePlot(true);
        }

        private IList<Point> GetFpsPoints(ISession session, bool useDisplayTimes, double startTime, double endTime,
            ERemoveOutlierMethod removeOutlierMethod, EFilterMode filterMode)
        {
            return useDisplayTimes
                ? session.GetDisplayFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions,
                    removeOutlierMethod, filterMode)
                : session.GetFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions,
                    removeOutlierMethod, filterMode);
        }

        private void SetLoadCharts(PlotModel plotModel, IPlotSettings plotSettings, ISession session,
            double startTime, double endTime)
        {
            if (plotSettings.IsAnyPercentageGraphVisible && session.HasValidSensorData())
            {
                plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISPERCENTAGE]);

                if (plotSettings.ShowGpuLoad)
                    SetGPULoadChart(plotModel, GetRenderablePoints(session.GetGPULoadPointTimeWindow(), startTime, endTime));
                if (plotSettings.ShowCpuLoad)
                    SetCPULoadChart(plotModel, GetRenderablePoints(session.GetCPULoadPointTimeWindow(), startTime, endTime));
                if (plotSettings.ShowCpuMaxThreadLoad)
                    SetCPUMaxThreadLoadChart(plotModel, GetRenderablePoints(session.GetCPUMaxThreadLoadPointTimeWindow(), startTime, endTime));
                if (plotSettings.ShowGpuPowerLimit)
                    SetGpuPowerLimitChart(plotModel, GetRenderablePoints(session.GetGpuPowerLimitPointTimeWindow(), startTime, endTime));
            }
        }

        private void SetFpsChart(PlotModel plotModel, IList<Point> fpsPoints, IList<Point> rawfpsPoints,
            IList<Point> gpuActiveFpsPoints, double average, double stroke, OxyColor color,
            EFilterMode filtermode, IPlotSettings plotSettings, bool useDisplayTimes)
        {
            if (fpsPoints == null || !fpsPoints.Any())
                return;

            // Filter mode = Raw+Average -> filtered average FPS
            // Filter mode = None -> Raw inverted frametimes
            var fpsSeries = new LineSeries
            {
                Title = useDisplayTimes ? "Display FPS" : "FPS",
                StrokeThickness = stroke,
                LegendStrokeThickness = 4,
                Color = color,
                EdgeRenderingMode = filtermode == EFilterMode.None ? EdgeRenderingMode.PreferSpeed : EdgeRenderingMode.PreferGeometricAccuracy,
                InterpolationAlgorithm = filtermode == EFilterMode.None ? null : InterpolationAlgorithms.CanonicalSpline
            };

            //var gpuActiveFpsSeries = new LineSeries
            //{
            //    Title = "GPU-Busy FPS",
            //    StrokeThickness = stroke,
            //    LegendStrokeThickness = 4,
            //    Color = Constants.GpuActiveFpsColor,
            //    EdgeRenderingMode = filtermode == EFilterMode.None ? EdgeRenderingMode.PreferSpeed : EdgeRenderingMode.PreferGeometricAccuracy,
            //    InterpolationAlgorithm = filtermode == EFilterMode.None ? null : InterpolationAlgorithms.CanonicalSpline
            //};


            fpsSeries.Points.AddRange(GetRenderablePoints(fpsPoints)
                .Select(point => new DataPoint(point.X, point.Y)));
            plotModel.Series.Add(fpsSeries);


            //if (plotSettings.ShowGpuActiveCharts)
            //{
            //    gpuActiveFpsSeries.Points.AddRange(gpuActiveFpsDataPoints);
            //    plotModel.Series.Add(gpuActiveFpsSeries);
            //}

            var averageSeries = new LineSeries
            {
                Title = useDisplayTimes ? "Avg Display FPS" : "Avg FPS",
                StrokeThickness = 2,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromAColor(200, Constants.FpsAverageColor)
            };

            averageSeries.Points.Add(new DataPoint(fpsPoints.First().X, average));
            averageSeries.Points.Add(new DataPoint(fpsPoints.Last().X, average));
            plotModel.Series.Add(averageSeries);


            UpdateAxis(EPlotAxis.XAXIS, (axis) =>
            {
                var axisPoints = rawfpsPoints != null && rawfpsPoints.Count > 0
                    ? rawfpsPoints
                    : fpsPoints;
                axis.Minimum = axisPoints.First().X;
                axis.Maximum = axisPoints.Last().X;
            }, false);
        }

        private void SetRawFPS(PlotModel plotModel, IList<Point> fpsPoints, bool useDisplayTimes)
        {
            // Only used when filter mode = Raw+Average
            var fpsSeries = new LineSeries
            {
                Title = useDisplayTimes ? "Raw Display FPS" : "Raw FPS",
                StrokeThickness = 1.5,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromAColor(200, Constants.FpsColor),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };
            var points = GetRenderablePoints(fpsPoints).Select(pnt => new DataPoint(pnt.X, pnt.Y));
            fpsSeries.Points.AddRange(points);
            plotModel.Series.Add(fpsSeries);
        }

        private void SetThresholdChart(PlotModel plotModel, IPlotSettings plotSettings, IList<Point> fpspoints)
        {
            var lowFPSSeries = new LineSeries
            {
                Title = "LowFPS",
                StrokeThickness = 3,
                LegendStrokeThickness = 4,
                LineStyle = LineStyle.LongDash,
                Color = OxyColor.FromAColor(180, OxyColor.FromRgb(255, 180, 0)),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            lowFPSSeries.Points.Add(new DataPoint(fpspoints.First().X, plotSettings.LowFPSThreshold));
            lowFPSSeries.Points.Add(new DataPoint(fpspoints.Last().X, plotSettings.LowFPSThreshold));
            plotModel.Series.Add(lowFPSSeries);
        }

        private void UpdateYAxisMinMaxBorders(double yMin, double yMax, double average)
        {
            UpdateAxis(EPlotAxis.YAXISFPS, (axis) =>
            {
                var axisMinimum = yMin - (yMax - yMin) / 6;
                var axisMaximum = yMax + (yMax - yMin) / 6;

                // min range of y-axis
                if (average - axisMinimum < 5)
                    axis.Minimum = average - 5;
                else
                    axis.Minimum = axisMinimum;

                if (axis.Minimum < 0)
                    axis.Minimum = 0;

                if (axisMaximum - average < 5)
                    axis.Maximum = average + 5;
                else
                    axis.Maximum = axisMaximum;
            }, false);
        }

    }
}
