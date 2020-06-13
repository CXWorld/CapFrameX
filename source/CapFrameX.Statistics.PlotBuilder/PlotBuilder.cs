using CapFrameX.Extensions.NetStandard;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using OxyPlot.Axes;
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
			PlotMargins = new OxyThickness(35, 0, 35, 35),
			PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
			LegendPlacement = LegendPlacement.Outside,
			LegendPosition = LegendPosition.BottomCenter,
			LegendOrientation = LegendOrientation.Horizontal,
			LegendMaxHeight = 25
		};

		protected Dictionary<EPlotAxis, LinearAxis> AxisDefinitions { get; set; } 
			= new Dictionary<EPlotAxis, LinearAxis>() {
			{ EPlotAxis.YAXISPERCENTAGE, new LinearAxis()
				{
					Key = EPlotAxis.YAXISPERCENTAGE.GetDescription(),
					Position = AxisPosition.Right,
					Title = "Percentage [%]",
					MajorGridlineStyle = LineStyle.None,
					MajorStep = 25,
					MinorTickSize = 0,
					MajorTickSize = 0,
					Minimum = 0,
					Maximum = 100,
					AbsoluteMaximum = 100,
					AbsoluteMinimum = 0,
					AxisTitleDistance = 10
				}
			},
			{
				EPlotAxis.XAXIS, new LinearAxis()
				{
					Key = EPlotAxis.XAXIS.GetDescription(),
					Position = AxisPosition.Bottom,
					Title = "Recording time [s]",
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
					MajorGridlineStyle = LineStyle.Solid,
					MajorGridlineThickness = 1,
					MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
					MinorTickSize = 0,
					MajorTickSize = 0,
					AxisTitleDistance = 10
				}
			},
			{
				EPlotAxis.YAXISFRAMETIMES, new LinearAxis()
				{
					Key = EPlotAxis.YAXISFRAMETIMES.GetDescription(),
					Position = AxisPosition.Left,
					Title = "Frametime [ms]",
					MajorGridlineStyle = LineStyle.Solid,
					MajorGridlineThickness = 1,
					MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
					MinorTickSize = 0,
					MajorTickSize = 0,
					AxisTitleDistance = 10
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
		}

		protected void SetGPULoadChart(PlotModel plotModel, IList<Point> points)
		{
			var series = new LineSeries
			{
				Title = "GPU load",
				StrokeThickness = 2,
				LegendStrokeThickness = 4,
				Color = OxyColor.FromArgb(180, 32, 141, 228),
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
				Color = OxyColor.FromArgb(180, 241, 125, 32),
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
				Color = OxyColor.FromArgb(180, 250, 25, 30),
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
				MarkerFill = OxyColor.FromArgb(100, 228, 32, 141),
				YAxisKey = EPlotAxis.YAXISPERCENTAGE.GetDescription()
			};

			series.Points.AddRange(points.Select(p => new DataPoint(p.X, p.Y)));
			plotModel.Series.Add(series);
		}
	}
}
