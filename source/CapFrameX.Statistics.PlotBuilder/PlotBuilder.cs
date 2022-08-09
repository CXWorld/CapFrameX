using CapFrameX.Data.Session.Contracts;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Legends;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Statistics.PlotBuilder
{
    public abstract class PlotBuilder
    {
        protected readonly IFrametimeStatisticProviderOptions _frametimeStatisticProviderOptions;
        protected readonly IStatisticProvider _frametimesStatisticProvider;

        public PlotModel PlotModel { get; protected set; } = new PlotModel
        {
            PlotMargins = new OxyThickness(50, 0, 50, 35),
            PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204)
        };

        protected Dictionary<EPlotAxis, LinearAxis> AxisDefinitions { get; set; }
            = new Dictionary<EPlotAxis, LinearAxis>() {
            { EPlotAxis.YAXISPERCENTAGE, new LinearAxis()
                {
                    Key = EPlotAxis.YAXISPERCENTAGE.GetDescription(),
                    Position = AxisPosition.Right,
                    Title = "Percentage [%]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.None,
                    MajorStep = 10,
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    Minimum = 0,
                    Maximum = 101,
                    AbsoluteMaximum = 101,
                    AbsoluteMinimum = 0,
                    AxisTitleDistance = 15
                }
            },
            { EPlotAxis.YAXISPOWER, new LinearAxis()
                {
                    Key = EPlotAxis.YAXISPOWER.GetDescription(),
                    Position = AxisPosition.Right,
                    Title = "Power [W]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.None,
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    Minimum = 0,
                    AbsoluteMinimum = 0,
                    AxisTitleDistance = 15
                }
            },
            { EPlotAxis.YAXISCLOCK, new LinearAxis()
                {
                    Key = EPlotAxis.YAXISCLOCK.GetDescription(),
                    Position = AxisPosition.Right,
                    Title = "Clock [MHz]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.None,
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    Minimum = 0,
                    AbsoluteMinimum = 0,
                    AxisTitleDistance = 20
                }
            },
            { EPlotAxis.YAXISTEMPERATURE, new LinearAxis()
                {
                    Key = EPlotAxis.YAXISTEMPERATURE.GetDescription(),
                    Position = AxisPosition.Right,
                    Title = "Temperature [°C]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.None,
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    Minimum = 0,
                    AbsoluteMinimum = 0,
                    AxisTitleDistance = 15
                }
            },
            { EPlotAxis.YAXISVOLTAGE, new LinearAxis()
                {
                    Key = EPlotAxis.YAXISVOLTAGE.GetDescription(),
                    Position = AxisPosition.Right,
                    Title = "Voltage [V]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.None,
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    Minimum = 0,
                    AbsoluteMinimum = 0,
                    AxisTitleDistance = 20
                }
            },
            { EPlotAxis.YAXISDATA, new LinearAxis()
                {
                    Key = EPlotAxis.YAXISDATA.GetDescription(),
                    Position = AxisPosition.Right,
                    Title = "Data [GB]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.None,
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    Minimum = 0,
                    AbsoluteMinimum = 0,
                    AxisTitleDistance = 20
                }
            },
            {
                EPlotAxis.XAXIS, new LinearAxis()
                {
                    Key = EPlotAxis.XAXIS.GetDescription(),
                    Position = AxisPosition.Bottom,
                    Title = "Recording time [s]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineThickness = 1,
                    MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    AxisTitleDistance = 10
                }
            },
            {
                EPlotAxis.YAXISFPS, new LinearAxis()
                {
                    Key = EPlotAxis.YAXISFPS.GetDescription(),
                    Position = AxisPosition.Left,
                    Title = "FPS [1/s]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineThickness = 1,
                    MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    AbsoluteMinimum = 0,
                    AxisTitleDistance = 15
                }
            },
            {
                EPlotAxis.YAXISFRAMETIMES, new LinearAxis()
                {
                    Key = EPlotAxis.YAXISFRAMETIMES.GetDescription(),
                    Position = AxisPosition.Left,
                    Title = "Frametime [ms]",
                    FontSize = 13,
                    MajorGridlineStyle = LineStyle.Solid,
                    MajorGridlineThickness = 1,
                    MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
                    MinorTickSize = 0,
                    MajorTickSize = 0,
                    AbsoluteMinimum = 0,
                    AxisTitleDistance = 15
                }
            }
        };

        public void Reset()
        {
            PlotModel.Series.Clear();
            PlotModel.Axes.Clear();
            PlotModel.InvalidatePlot(true);
        }

        public void UpdateAxis(EPlotAxis axisType, Action<Axis> action)
        {
            var axis = PlotModel.GetAxisOrDefault(axisType.GetDescription(), null);
            if (axis != null)
            {
                action(axis);
                PlotModel.InvalidatePlot(false);
            }
        }

        public PlotBuilder(IFrametimeStatisticProviderOptions options, IStatisticProvider frametimeStatisticProvider)
        {
            _frametimeStatisticProviderOptions = options;
            _frametimesStatisticProvider = frametimeStatisticProvider;

            PlotModel.Legends?.Add(new Legend()
            {
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.BottomCenter,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendMaxHeight = 25
            });
        }

        protected void SetGPULoadChart(PlotModel plotModel, IList<Point> points)
        {
            var series = new LineSeries
            {
                Title = "GPU load",
                StrokeThickness = 2,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromArgb(175, 32, 141, 228),
                YAxisKey = EPlotAxis.YAXISPERCENTAGE.GetDescription()
            };

            series.Points.AddRange(points.Select(p => new DataPoint(p.X, p.Y)));
            plotModel.Series.Add(series);
        }
        protected void SetCPULoadChart(PlotModel plotModel, IList<Point> points)
        {
            var series = new LineSeries
            {
                Title = "CPU total load",
                StrokeThickness = 2,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromArgb(175, 241, 125, 32),
                YAxisKey = EPlotAxis.YAXISPERCENTAGE.GetDescription()
            };

            series.Points.AddRange(points.Select(p => new DataPoint(p.X, p.Y)));
            plotModel.Series.Add(series);
        }
        protected void SetCPUMaxThreadLoadChart(PlotModel plotModel, IList<Point> points)
        {
            var series = new LineSeries
            {
                Title = "CPU max thread load",
                StrokeThickness = 2,
                LegendStrokeThickness = 4,
                Color = OxyColor.FromArgb(175, 250, 25, 30),
                YAxisKey = EPlotAxis.YAXISPERCENTAGE.GetDescription()
            };

            series.Points.AddRange(points.Select(p => new DataPoint(p.X, p.Y)));
            plotModel.Series.Add(series);
        }

        protected void SetGpuPowerLimitChart(PlotModel plotModel, IList<Point> points)
        {
            var series = new LineSeries
            {
                Title = "GPU power limit",
                LineStyle = LineStyle.None,
                MarkerType = MarkerType.Square,
                MarkerSize = 3,
                MarkerFill = OxyColor.FromArgb(255, 228, 32, 141),
                YAxisKey = EPlotAxis.YAXISPERCENTAGE.GetDescription()
            };

            series.Points.AddRange(points.Select(p => new DataPoint(p.X, p.Y)));
            plotModel.Series.Add(series);
        }

        public void SetAggregationSeparators(ISession session, PlotModel plotModel, bool showSeparators)
        {

            plotModel.Annotations.Clear();

            if (!showSeparators || session.Runs.Count < 2)
                return;

            // get start points of each run and end point of last run
            var sessionTimes = new List<double>();
            foreach (var singlesession in session.Runs)
            {
                sessionTimes.Add(singlesession.CaptureData.TimeInSeconds.FirstOrDefault());
            }
            sessionTimes.Add(session.Runs.LastOrDefault().CaptureData.TimeInSeconds.LastOrDefault());

            // draw and label separators 
            var sceneNumber = 1;
            foreach (var time in sessionTimes)
            {

                LineAnnotation Line = new LineAnnotation()
                {
                    StrokeThickness = 2,
                    Color = OxyColors.Gray,
                    Type = LineAnnotationType.Vertical,
                    Text = sceneNumber < sessionTimes.Count ? $"Scene {sceneNumber}" + Environment.NewLine + $"({Math.Round(time, 2)}s)" : string.Empty,
                    FontWeight = FontWeights.Bold,
                    FontSize = 14,
                    TextColor = OxyColor.FromRgb(150, 150, 150),
                    TextOrientation = AnnotationTextOrientation.Horizontal,
                    TextVerticalAlignment = VerticalAlignment.Middle,
                    TextPadding = -65,
                    TextMargin = 15,
                    X = time
                };

                plotModel.Annotations.Add(Line);
                sceneNumber++;
            }
        }
    }
}