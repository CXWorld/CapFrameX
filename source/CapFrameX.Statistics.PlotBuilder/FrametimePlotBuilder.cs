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
            Reset();
            if (session == null)
            {
                return;
            }

            plotModel.Axes.Add(AxisDefinitions[EPlotAxis.XAXIS]);
            plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISFRAMETIMES]);

            var frametimepoints = session.GetFrametimePointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod);
            var frametimes = session.GetFrametimeTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod);

            SetFrametimeChart(plotModel, frametimepoints);

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

            var stutteringValue = frametimes.Average() * plotSettings.StutteringFactor;
            var lowFPSValue = 1000 / plotSettings.LowFPSThreshold;


            SetAnnotations(session, frametimes, plotModel, plotSettings.ShowAggregationSeparators, plotSettings.ShowThresholds, stutteringValue, lowFPSValue);


            onFinishAction?.Invoke(plotModel);
            plotModel.InvalidatePlot(true);
        }

        private void SetFrametimeChart(PlotModel plotModel, IList<Point> frametimePoints)
        {
            if (frametimePoints == null || !frametimePoints.Any())
                return;

            int count = frametimePoints.Count;
            var frametimeDataPoints = frametimePoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
            var yMin = frametimePoints.Min(pnt => pnt.Y);
            var yMax = frametimePoints.Max(pnt => pnt.Y);
            var movingAverage = _frametimesStatisticProvider.GetMovingAverage(frametimePoints.Select(pnt => pnt.Y).ToList());

            plotModel.Series.Clear();

            var frametimeSeries = new LineSeries
            {
                Title = "Frametimes",
                StrokeThickness = 1,
                LegendStrokeThickness = 4,
                Color = Constants.FrametimeStroke,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            var movingAverageSeries = new LineSeries
            {
                Title = "Moving average",
                StrokeThickness = 2,
                LegendStrokeThickness = 4,
                Color = Constants.FrametimeMovingAverageStroke,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };


            frametimeSeries.Points.AddRange(frametimeDataPoints);
            movingAverageSeries.Points.AddRange(movingAverage.Select((y, i) => new DataPoint(frametimePoints[i].X, y)));

            UpdateAxis(EPlotAxis.XAXIS, (axis) =>
            {
                axis.Minimum = frametimePoints.First().X;
                axis.Maximum = frametimePoints.Last().X;
            });

            plotModel.Series.Add(frametimeSeries);
            plotModel.Series.Add(movingAverageSeries);

            plotModel.InvalidatePlot(true);
        }
    }
}
