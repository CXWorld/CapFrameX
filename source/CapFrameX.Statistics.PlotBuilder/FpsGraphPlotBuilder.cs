using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using OxyPlot.Annotations;
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



            if (filterMode is EFilterMode.RawPlusAverage)
            {
                var avgFpsPoints = session.GetFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod, EFilterMode.TimeIntervalAverage);

                SetRawFPS(plotModel, rawFpsPoints);
                SetLoadCharts(plotModel, plotSettings, session);
                SetFpsChart(plotModel, avgFpsPoints, rawFpsPoints, average, 2, OxyColor.FromRgb(241, 125, 32), filterMode);


                yMin = rawFpsPoints.Min(pnt => pnt.Y);
                yMax = rawFpsPoints.Max(pnt => pnt.Y);
            }
            else
            {
                var fpsPoints = session.GetFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod, filterMode);

                if(filterMode == EFilterMode.TimeIntervalAverage)
                    SetLoadCharts(plotModel, plotSettings, session);

                SetFpsChart(plotModel, fpsPoints, rawFpsPoints, average, filterMode is EFilterMode.None ? 1 : 2, Constants.FpsStroke, filterMode);

                if(filterMode is EFilterMode.None)
                    SetLoadCharts(plotModel, plotSettings, session);

                yMin = fpsPoints.Min(pnt => pnt.Y);
                yMax = fpsPoints.Max(pnt => pnt.Y);
            }
            UpdateYAxisMinMaxBorders(yMin, yMax, average);

            var stutteringValue = 1000 / (frametimes.Average() * plotSettings.StutteringFactor);
            var lowFPSValue = plotSettings.LowFPSThreshold;

            SetAnnotations(session, frametimes, plotModel, plotSettings.ShowAggregationSeparators, plotSettings.ShowThresholds, stutteringValue, lowFPSValue);


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

        private void SetFpsChart(PlotModel plotModel, IList<Point> fpsPoints, IList<Point> rawfpsPoints, double average, int stroke, OxyColor color, EFilterMode filtermode)
        {
            if (fpsPoints == null || !fpsPoints.Any())
                return;

            int count = fpsPoints.Count;
            var fpsDataPoints = fpsPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));

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

            fpsSeries.Points.AddRange(fpsDataPoints);
            plotModel.Series.Add(fpsSeries);

            var averageDataPoints =  fpsPoints.Select(pnt => new DataPoint(pnt.X, average));

            var averageSeries = new LineSeries
            {
                Title = "Avg FPS",
                StrokeThickness = 2,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromAColor(200, Constants.FpsAverageStroke)
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
                StrokeThickness = 1,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromAColor(200, Constants.FpsStroke),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };
            var points = fpsPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
            fpsSeries.Points.AddRange(points);
            plotModel.Series.Add(fpsSeries);

            plotModel.InvalidatePlot(true);
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
