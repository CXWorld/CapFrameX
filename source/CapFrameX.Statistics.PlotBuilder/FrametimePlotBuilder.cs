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
    public class FrametimePlotBuilder : PlotBuilder
    {
        public FrametimePlotBuilder(IFrametimeStatisticProviderOptions options, IStatisticProvider frametimeStatisticProvider) : base(options, frametimeStatisticProvider) { }

        public void BuildPlotmodel(ISession session, IPlotSettings plotSettings, double startTime, double endTime, ERemoveOutlierMethod eRemoveOutlinerMethod, Action<PlotModel> onFinishAction = null)
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
            plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISFRAMETIMES]);

            var frametimepoints = session.GetFrametimePointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod);
            var displaytimespoints = session.GetDisplayChangeTimePointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod);

            IList<Point> GpuActiveTimePoints = new List<Point>();
            IList<Point> CpuActiveTimePoints = new List<Point>();

            if (plotSettings.ShowGpuActiveCharts)
                GpuActiveTimePoints = session.GetGpuActiveTimePointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod);

            if (plotSettings.ShowCpuActiveCharts)
                CpuActiveTimePoints = session.GetCpuActiveTimePointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod);

            SetFrametimeChart(plotModel, frametimepoints, displaytimespoints, GpuActiveTimePoints, CpuActiveTimePoints, plotSettings);

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

            // Draw display times graph
            if (plotSettings.ShowDisplayTimes)
            {
                SetDisplayTimeChart(plotModel, displaytimespoints);
            }

            // Draw PC latency graph
            if (plotSettings.ShowPcLatency)
            {
                SetPcLatencyChart(plotModel, GetRenderablePoints(session.GetPcLatencyPointTimeWindow(), startTime, endTime));
            }

            // Draw Animation Error graph
            if (plotSettings.ShowAnimationError)
            {
                SetAnimationErrorChart(plotModel, GetRenderablePoints(session.GetAnimationErrorPointTimeWindow(), startTime, endTime));
            }

            SetAggregationSeparators(session, plotModel, plotSettings.ShowAggregationSeparators);

            onFinishAction?.Invoke(plotModel);
            plotModel.InvalidatePlot(true);
        }

        private void SetFrametimeChart(PlotModel plotModel, IList<Point> frametimePoints, IList<Point> displaytimespoints,
            IList<Point> GpuActiveTimePoints, IList<Point> CpuActiveTimePoints, IPlotSettings plotSettings)
        {
            if (frametimePoints == null || !frametimePoints.Any()) return;

            var movingAverageSourcePoints = plotSettings.ShowDisplayTimes && displaytimespoints.Any()
                ? displaytimespoints
                : frametimePoints;
            var movingAverageValues = _frametimesStatisticProvider.GetMovingAverage(
                movingAverageSourcePoints.Select(point => point.Y).ToList());
            var movingAveragePoints = movingAverageValues
                .Select((value, index) => new Point(movingAverageSourcePoints[index].X, value))
                .ToList();

            plotModel.Series.Clear();

            var frametimeSeries = new LineSeries
            {
                Title = "Frame Times",
                StrokeThickness = 1.5,
                LegendStrokeThickness = 4,
                Color = Constants.FrametimeColor,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            var GpuActiveTimeSeries = new LineSeries
            {
                Title = "GPU-Busy Times",
                StrokeThickness = 1.5,
                LegendStrokeThickness = 4,
                Color = Constants.GpuActiveTimeColor,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            var CpuActiveTimeSeries = new LineSeries
            {
                Title = "CPU-Busy Times",
                StrokeThickness = 1.5,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromArgb(255, 100, 149, 237),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            var movingAverageSeries = new LineSeries
            {
                Title = "Moving Average",
                StrokeThickness = 3,
                LegendStrokeThickness = 4,
                Color = Constants.FrametimeMovingAverageColor,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

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
                Title = "Low FPS",
                StrokeThickness = 3,
                LegendStrokeThickness = 4,
                LineStyle = LineStyle.LongDash,
                Color = OxyColor.FromAColor(180, OxyColor.FromRgb(255, 180, 0)),
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            frametimeSeries.Points.AddRange(GetRenderablePoints(frametimePoints)
                .Select(point => new DataPoint(point.X, point.Y)));
            movingAverageSeries.Points.AddRange(GetRenderablePoints(movingAveragePoints)
                .Select(point => new DataPoint(point.X, point.Y)));

            if (plotSettings.ShowGpuActiveCharts)
                GpuActiveTimeSeries.Points.AddRange(GetRenderablePoints(GpuActiveTimePoints)
                    .Select(point => new DataPoint(point.X, point.Y)));

            if (plotSettings.ShowCpuActiveCharts)
                CpuActiveTimeSeries.Points.AddRange(GetRenderablePoints(CpuActiveTimePoints)
                    .Select(point => new DataPoint(point.X, point.Y)));

            UpdateAxis(EPlotAxis.XAXIS, (axis) =>
            {
                axis.Minimum = frametimePoints.First().X;
                axis.Maximum = frametimePoints.Last().X;
            }, false);

            plotModel.Series.Add(frametimeSeries);
            plotModel.Series.Add(movingAverageSeries);

            if (plotSettings.ShowGpuActiveCharts)
                plotModel.Series.Add(GpuActiveTimeSeries);

            if (plotSettings.ShowCpuActiveCharts)
                plotModel.Series.Add(CpuActiveTimeSeries);

            if (plotSettings.ShowThresholds)
            {
                var stutteringPoints = movingAveragePoints
                    .Select(point => new Point(point.X, point.Y * plotSettings.StutteringFactor))
                    .ToList();
                stutteringSeries.Points.AddRange(GetRenderablePoints(stutteringPoints)
                    .Select(point => new DataPoint(point.X, point.Y)));

                double lowFpsThreshold = 1000 / plotSettings.LowFPSThreshold;
                lowFPSSeries.Points.Add(new DataPoint(movingAverageSourcePoints.First().X, lowFpsThreshold));
                lowFPSSeries.Points.Add(new DataPoint(movingAverageSourcePoints.Last().X, lowFpsThreshold));

                plotModel.Series.Add(stutteringSeries);
                plotModel.Series.Add(lowFPSSeries);
            }
        }
    }
}
