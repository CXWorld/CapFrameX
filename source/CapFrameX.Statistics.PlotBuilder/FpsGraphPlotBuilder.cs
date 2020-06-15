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

            SetFpsChart(plotModel, session.GetFpsPointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod, filterMode),
                session.GetFrametimeTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod));

            if (plotSettings.IsAnyGraphVisible && session.HasValidSensorData())
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

            onFinishAction?.Invoke(plotModel);
            plotModel.InvalidatePlot(true);
        }

        private void SetFpsChart(PlotModel plotModel, IList<Point> fpsPoints, IList<double> frametimes)
        {
            if (fpsPoints == null || !fpsPoints.Any())
                return;

            var avgFps = _frametimesStatisticProvider.GetFpsMetricValue(frametimes, EMetric.Average);

            int count = fpsPoints.Count;
            var fpsDataPoints = fpsPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));

            var yMin = fpsPoints.Min(pnt => pnt.Y);
            var yMax = fpsPoints.Max(pnt => pnt.Y);

            plotModel.Series.Clear();

            var fpsSeries = new LineSeries
            {
                Title = "FPS",
                StrokeThickness = 1,
                LegendStrokeThickness = 4,
                Color = Constants.FpsStroke,
                InterpolationAlgorithm = InterpolationAlgorithms.CanonicalSpline
            };

            fpsSeries.Points.AddRange(fpsDataPoints);
            plotModel.Series.Add(fpsSeries);

            double average = frametimes.Count * 1000 / frametimes.Sum();
            var averageDataPoints =  fpsPoints.Select(pnt => new DataPoint(pnt.X, average));

            var averageSeries = new LineSeries
            {
                Title = "Average FPS",
                StrokeThickness = 2,
                LegendStrokeThickness = 4,
                Color = Constants.FpsAverageStroke
            };

            averageSeries.Points.AddRange(averageDataPoints);
            plotModel.Series.Add(averageSeries);


            UpdateAxis(EPlotAxis.XAXIS, (axis) =>
            {
                axis.Minimum = 0;
                axis.Maximum = fpsPoints.Last().X;
            });

            UpdateAxis(EPlotAxis.YAXISFPS, (axis) =>
            {
                var axisMinimum = yMin - (yMax - yMin) / 6;
                var axisMaximum = yMax + (yMax - yMin) / 6;

                // min range of y-axis
                if (avgFps - axisMinimum < 15)
                    axis.Minimum = avgFps - 15;
                else
                    axis.Minimum = axisMinimum;

                if (axisMaximum - avgFps < 15)
                    axis.Maximum = avgFps + 15;
                else
                    axis.Maximum = axisMaximum;

                // center average FPS line
                var rangeAvgMin = avgFps - axis.Minimum;
                var rangeAvgMax = axis.Maximum - avgFps;

                if (rangeAvgMin > rangeAvgMax)
                    axis.Maximum += (rangeAvgMin - rangeAvgMax);
                else if (rangeAvgMin < rangeAvgMax)
                    axis.Minimum -= (rangeAvgMax - rangeAvgMin);


            });

            plotModel.InvalidatePlot(true);
        }
    }
}
