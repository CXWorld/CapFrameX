using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using OxyPlot.Annotations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CapFrameX.Statistics.PlotBuilder
{
    public class FrametimeDistributionPlotBuilder : PlotBuilder
    {
        public FrametimeDistributionPlotBuilder(IFrametimeStatisticProviderOptions options, IStatisticProvider frametimeStatisticProvider) : base(options, frametimeStatisticProvider) { }

        public void BuildPlotmodel(ISession session, IPlotSettings plotSettings, double startTime, double endTime, ERemoveOutlierMethod eRemoveOutlinerMethod, Action<PlotModel> onFinishAction = null)
        {
            var plotModel = PlotModel;
            Reset();

            if (session == null)
            {
                return;
            }

            plotModel.Axes.Add(AxisDefinitions[EPlotAxis.XAXISFRAMETIMES]);
            plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISDISTRIBUTION]);


            var frametimeDistributionPoints = session.GetFrametimeDistributionPoints(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod);


            IList<Point> GpuActiveTimePoints = new List<Point>();


            SetFrametimeDistributionChart(plotModel, frametimeDistributionPoints, plotSettings);


            SetAggregationSeparators(session, plotModel, plotSettings.ShowAggregationSeparators);


            UpdateYAxisMaxBorder(frametimeDistributionPoints.Max(p => p.Y));

            // draw refresh rate separators
            plotModel.Annotations.Clear();
            var refreshRates = new List<int> { 30, 60, 90, 120, 144, 240 };

            foreach (var rate in refreshRates)
            {

                LineAnnotation Line = new LineAnnotation()
                {
                    StrokeThickness = 2,
                    Color = OxyColors.Gray,
                    Type = LineAnnotationType.Vertical,
                    Text = rate + " Hz",
                    FontWeight = FontWeights.Bold,
                    FontSize = 10,
                    TextColor = OxyColor.FromRgb(150, 150, 150),
                    TextOrientation = AnnotationTextOrientation.Horizontal,
                    TextHorizontalAlignment = HorizontalAlignment.Left,
                    TextVerticalAlignment = VerticalAlignment.Middle,
                    TextPadding = -40,
                    TextMargin = 5,
                    X = 1000.0 / rate
                };
                plotModel.Annotations.Add(Line); ;
            }


            onFinishAction?.Invoke(plotModel);
            plotModel.InvalidatePlot(true);
        }

        private void SetFrametimeDistributionChart(PlotModel plotModel, IList<Point> frametimeDistributionPoints, IPlotSettings plotSettings)
        {
            if (frametimeDistributionPoints == null || !frametimeDistributionPoints.Any())
                return;

            int count = frametimeDistributionPoints.Count;
            var distributionDataPoints = frametimeDistributionPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));

            plotModel.Series.Clear();

            var distributionSeries = new LineSeries
            {
                Title = "Distribution",
                StrokeThickness = 3,
                LegendStrokeThickness = 4,
                Color = Constants.FrametimeColor,
                EdgeRenderingMode = EdgeRenderingMode.PreferGeometricAccuracy
            };


            distributionSeries.Points.AddRange(distributionDataPoints);



            UpdateAxis(EPlotAxis.XAXISFRAMETIMES, (axis) =>
            {
                axis.Minimum = frametimeDistributionPoints.First().X - 1;
                axis.Maximum = frametimeDistributionPoints.Last().X + 1;
            });
            plotModel.Series.Add(distributionSeries);

            plotModel.InvalidatePlot(true);
        }

        private void UpdateYAxisMaxBorder(double yMax)
        {
            UpdateAxis(EPlotAxis.YAXISDISTRIBUTION, (axis) =>
             {

                 axis.Minimum = 0;
                 axis.Maximum = yMax * 1.2;

                 double stepSize = 0;
                 if (yMax <= 5)
                     stepSize = 0.5;
                 else if (yMax <= 10)
                     stepSize = 1;
                 else if (yMax <= 20)
                     stepSize = 2;
                 else if (yMax <= 50)
                     stepSize = 5;
                 else
                     stepSize = 10;

                 axis.MajorStep = stepSize;
             });
        }
    }
}
