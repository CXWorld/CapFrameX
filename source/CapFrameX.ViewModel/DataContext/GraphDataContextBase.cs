using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Windows;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Extensions;
using CapFrameX.Statistics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Mvvm;

namespace CapFrameX.ViewModel.DataContext
{
	public class GraphDataContextBase : BindableBase
	{
		public const int SCALE_RESOLUTION = 200;

		protected PlotModel PlotModel { get; set; } = new PlotModel
		{
			PlotMargins = new OxyThickness(35, 0, 35, 35),
			PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
			LegendPlacement = LegendPlacement.Outside,
			LegendPosition = LegendPosition.BottomCenter,
			LegendOrientation = LegendOrientation.Horizontal,
			LegendMaxHeight = 25
		};

		protected IAppConfiguration AppConfiguration { get; }

		protected IRecordDataServer RecordDataServer { get; }

		protected IStatisticProvider FrametimesStatisticProvider { get; }

		protected Dictionary<EPlotAxis, LinearAxis> AxisDefinitions { get; set; } = new Dictionary<EPlotAxis, LinearAxis>() {
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
					AbsoluteMinimum = 0
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
					MajorTickSize = 0
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
					MajorTickSize = 0
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
					MajorTickSize = 0
				}
			}
		};

		public ISession RecordSession
		{
			get => RecordDataServer.CurrentSession;
			set
			{
				RecordDataServer.CurrentSession = value;
			}
		}

		public void Reset()
		{
			PlotModel.Series.Clear();
			PlotModel.Axes.Clear();
			PlotModel.InvalidatePlot(true);
		}

		public void UpdateAxis(EPlotAxis axisType, Action<Axis> action)
		{
			var axis = PlotModel.GetAxisOrDefault(axisType.GetDescription(), null);
			if(axis != null)
			{
				action(axis);
				PlotModel.InvalidatePlot(false);
			}
		}

		public GraphDataContextBase(IRecordDataServer recordDataServer,
			IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider)
		{
			RecordDataServer = recordDataServer;
			AppConfiguration = appConfiguration;
			FrametimesStatisticProvider = frametimesStatisticProvider;
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
	}

	public class VisibleGraphs
	{
		public bool GpuLoad { get; private set; }
		public bool CpuLoad { get; private set; }
		public bool CpuMaxThreadLoad { get; private set; }

		public bool IsAnyGraphVisible => GpuLoad || CpuLoad || CpuMaxThreadLoad;

		public VisibleGraphs(bool gpuLoad, bool cpuLoad, bool cpuMaxThreadLoad)
		{
			GpuLoad = gpuLoad;
			CpuLoad = cpuLoad;
			CpuMaxThreadLoad = cpuMaxThreadLoad;
		}
	}

	public enum EPlotAxis
	{
		XAXIS,
		YAXISFRAMETIMES,
		YAXISFPS,
		YAXISPERCENTAGE
	}
}
