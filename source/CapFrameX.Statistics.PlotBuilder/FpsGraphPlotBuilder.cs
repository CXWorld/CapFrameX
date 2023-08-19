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
            Reset();

            if (session == null) return;

            plotModel.Axes.Add(AxisDefinitions[EPlotAxis.XAXIS]);
            plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISFPS]);

            var frametimes = session.GetFrametimeTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod);
            double average = frametimes.Count * 1000 / frametimes.Sum();
            double yMin, yMax;

            plotModel.Series.Clear();

            var rawFpsPoints = session.GetFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod, EFilterMode.None);
            IList<Point> gpuActiveFpsPoints = new List<Point>();

            if (filterMode is EFilterMode.RawPlusAverage)
            {
                var avgFpsPoints = session.GetFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod, EFilterMode.TimeIntervalAverage);

                SetRawFPS(plotModel, rawFpsPoints);
                SetLoadCharts(plotModel, plotSettings, session);
                SetFpsChart(plotModel, avgFpsPoints, rawFpsPoints, gpuActiveFpsPoints,  average, 3, OxyColor.FromRgb(241, 125, 32), filterMode, plotSettings);

                yMin = rawFpsPoints.Min(pnt => pnt.Y);
                yMax = rawFpsPoints.Max(pnt => pnt.Y);
            }
            else
            {
                var fpsPoints = session.GetFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod, filterMode);
                

                if (plotSettings.ShowGpuActiveCharts)
                    gpuActiveFpsPoints = session.GetGpuActiveFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod, filterMode);

                if (filterMode == EFilterMode.TimeIntervalAverage)
                SetLoadCharts(plotModel, plotSettings, session);

                SetFpsChart(plotModel, fpsPoints, rawFpsPoints, gpuActiveFpsPoints, average, filterMode is EFilterMode.None ? 1.5 : 3, Constants.FpsColor, filterMode, plotSettings);


                if (filterMode is EFilterMode.None)
                    SetLoadCharts(plotModel, plotSettings, session);


                if (plotSettings.ShowGpuActiveCharts)
                {
                    yMin = Math.Min(fpsPoints.Min(pnt => pnt.Y), gpuActiveFpsPoints.Min(pnt => pnt.Y));
                    yMax = Math.Max(fpsPoints.Max(pnt => pnt.Y), gpuActiveFpsPoints.Max(pnt => pnt.Y));
                }
                else
                {
                    yMin = fpsPoints.Min(pnt => pnt.Y);
                    yMax = fpsPoints.Max(pnt => pnt.Y);
                }

            }


            if (plotSettings.ShowThresholds)
            {
                SetThresholdChart(plotModel, plotSettings, rawFpsPoints, out double yMinStuttering);
                yMin = Math.Min(yMinStuttering, yMin);
            }

            UpdateYAxisMinMaxBorders(yMin, yMax, average);

            var stutteringValue = 1000 / (frametimes.Average() * plotSettings.StutteringFactor);
            var lowFPSValue = plotSettings.LowFPSThreshold;

            SetAggregationSeparators(session, plotModel, plotSettings.ShowAggregationSeparators);

            onFinishAction?.Invoke(plotModel);
            plotModel.InvalidatePlot(true);
        }

        private void SetLoadCharts(PlotModel plotModel, IPlotSettings plotSettings, ISession session)
        {
            if (plotSettings.IsAnyPercentageGraphVisible && session.HasValidSensorData())
            {
                plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISPERCENTAGE]);

                if (plotSettings.ShowGpuLoad)
                    SetGPULoadChart(plotModel, session.GetGPULoadPointTimeWindow());
                if (plotSettings.ShowCpuLoad)
                    SetCPULoadChart(plotModel, session.GetCPULoadPointTimeWindow());
                if (plotSettings.ShowCpuMaxThreadLoad)
                    SetCPUMaxThreadLoadChart(plotModel, session.GetCPUMaxThreadLoadPointTimeWindow());
                if (plotSettings.ShowGpuPowerLimit)
                    SetGpuPowerLimitChart(plotModel, session.GetGpuPowerLimitPointTimeWindow());
            }
        }

        private void SetFpsChart(PlotModel plotModel, IList<Point> fpsPoints, IList<Point> rawfpsPoints, IList<Point> gpuActiveFpsPoints, double average, double stroke, OxyColor color, EFilterMode filtermode, IPlotSettings plotSettings)
        {
            if (fpsPoints == null || !fpsPoints.Any())
                return;

            int count = fpsPoints.Count;
            var fpsDataPoints = fpsPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
            var gpuActiveFpsDataPoints = gpuActiveFpsPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));

            // Filter mode = Raw+Average -> filtered average FPS
            // Filter mode = None -> Raw inverted frametimes
            var fpsSeries = new LineSeries
            {
                Title = "FPS",
                StrokeThickness = stroke,
                LegendStrokeThickness = 4,
                Color = color,
                EdgeRenderingMode = filtermode == EFilterMode.None ? EdgeRenderingMode.PreferSpeed : EdgeRenderingMode.PreferGeometricAccuracy,
                InterpolationAlgorithm = filtermode == EFilterMode.None ? null : InterpolationAlgorithms.CanonicalSpline
            };

            var gpuActiveFpsSeries = new LineSeries
            {
                Title = "Gpu Active FPS",
                StrokeThickness = stroke,
                LegendStrokeThickness = 4,
                Color = Constants.GpuActiveFpsColor,
                EdgeRenderingMode = filtermode == EFilterMode.None ? EdgeRenderingMode.PreferSpeed : EdgeRenderingMode.PreferGeometricAccuracy,
                InterpolationAlgorithm = filtermode == EFilterMode.None ? null : InterpolationAlgorithms.CanonicalSpline
            };


            fpsSeries.Points.AddRange(fpsDataPoints);
            plotModel.Series.Add(fpsSeries);


            if (plotSettings.ShowGpuActiveCharts)
            {
                gpuActiveFpsSeries.Points.AddRange(gpuActiveFpsDataPoints);
                plotModel.Series.Add(gpuActiveFpsSeries);
            }


            var averageDataPoints = fpsPoints.Select(pnt => new DataPoint(pnt.X, average));

            var averageSeries = new LineSeries
            {
                Title = "Avg FPS",
                StrokeThickness = 2,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromAColor(200, Constants.FpsAverageColor)
            };

            averageSeries.Points.AddRange(averageDataPoints);
            plotModel.Series.Add(averageSeries);


            UpdateAxis(EPlotAxis.XAXIS, (axis) =>
            {
                axis.Minimum = rawfpsPoints.First().X;
                axis.Maximum = rawfpsPoints.Last().X;
            });

            plotModel.InvalidatePlot(true);
        }

        private void SetRawFPS(PlotModel plotModel, IList<Point> fpsPoints)
        {
            // Only used when filter mode = Raw+Average
            var fpsSeries = new LineSeries
            {
                Title = "Raw FPS",
                StrokeThickness = 1.5,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromAColor(200, Constants.FpsColor),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };
            var points = fpsPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
            fpsSeries.Points.AddRange(points);
            plotModel.Series.Add(fpsSeries);

            plotModel.InvalidatePlot(true);
        }

        private void SetThresholdChart(PlotModel plotModel, IPlotSettings plotSettings, IList<Point> fpspoints, out double yMin)
        {
            var stuttering = new List<double>();
            var lowFPS = new List<double>();

            var movingAverage = _frametimesStatisticProvider.GetMovingAverage(fpspoints.Select(pnt => 1000 / pnt.Y).ToList());

            for (int i = 0; i < fpspoints.Count; i++)
            {
                stuttering.Add(1000 / movingAverage[i] / plotSettings.StutteringFactor);
                lowFPS.Add(plotSettings.LowFPSThreshold);
            }

            var stutteringSeries = new LineSeries
            {
                Title = "Stuttering",
                StrokeThickness = 2,
                LegendStrokeThickness = 4,
                LineStyle = LineStyle.Dash,
                Color = OxyColor.FromAColor(180, OxyColors.Red),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            var lowFPSSeries = new LineSeries
            {
                Title = "LowFPS",
                StrokeThickness = 3,
                LegendStrokeThickness = 4,
                LineStyle = LineStyle.LongDash,
                Color = OxyColor.FromAColor(180, OxyColor.FromRgb(255, 180, 0)),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            stutteringSeries.Points.AddRange(stuttering.Select((y, i) => new DataPoint(fpspoints[i].X, y)));
            lowFPSSeries.Points.AddRange(lowFPS.Select((y, i) => new DataPoint(fpspoints[i].X, y)));

            plotModel.Series.Add(stutteringSeries);
            plotModel.Series.Add(lowFPSSeries);

            yMin = Math.Min(stuttering.Min(), plotSettings.LowFPSThreshold);
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
            });
        }

    }
}
