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

            SetFrametimeChart(plotModel, frametimepoints, plotSettings);

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


            SetAggregationSeparators(session, plotModel, plotSettings.ShowAggregationSeparators);


            onFinishAction?.Invoke(plotModel);
            plotModel.InvalidatePlot(true);
        }

        private void SetFrametimeChart(PlotModel plotModel, IList<Point> frametimePoints, IPlotSettings plotSettings)
        {
            if (frametimePoints == null || !frametimePoints.Any())
                return;

            int count = frametimePoints.Count;
            var frametimeDataPoints = frametimePoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
            var yMin = frametimePoints.Min(pnt => pnt.Y);
            var yMax = frametimePoints.Max(pnt => pnt.Y);
            var movingAverage = _frametimesStatisticProvider.GetMovingAverage(frametimePoints.Select(pnt => pnt.Y).ToList());

            var stuttering = new List<double>();
            var lowFPS = new List<double>();

            for (int i = 0; i < count; i++)
            {
                stuttering.Add(movingAverage[i] * plotSettings.StutteringFactor);
                lowFPS.Add(1000 / plotSettings.LowFPSThreshold);
            }


            plotModel.Series.Clear();

            var frametimeSeries = new LineSeries
            {
                Title = "Frametimes",
                StrokeThickness = 1.5,
                LegendStrokeThickness = 4,
                Color = Constants.FrametimeColor,
                EdgeRenderingMode = EdgeRenderingMode.PreferSpeed
            };

            var movingAverageSeries = new LineSeries
            {
                Title = "Moving average",
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
                Title = "LowFPS",
                StrokeThickness = 3,
                LegendStrokeThickness = 4,
                LineStyle = LineStyle.LongDash,
                Color = OxyColor.FromAColor(180, OxyColor.FromRgb(255, 180, 0)),
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


            if (plotSettings.ShowThresholds)
            { 
                stutteringSeries.Points.AddRange(stuttering.Select((y, i) => new DataPoint(frametimePoints[i].X, y)));
                lowFPSSeries.Points.AddRange(lowFPS.Select((y, i) => new DataPoint(frametimePoints[i].X, y)));

                plotModel.Series.Add(stutteringSeries);
                plotModel.Series.Add(lowFPSSeries);

            }

            plotModel.InvalidatePlot(true);
        }
    }
}
