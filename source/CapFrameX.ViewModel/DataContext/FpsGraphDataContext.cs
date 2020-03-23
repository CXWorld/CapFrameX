using CapFrameX.Contracts.Configuration;
using CapFrameX.Statistics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Commands;
using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Collections.Generic;
using System.Globalization;
using CapFrameX.Data;
using System.Windows.Threading;

namespace CapFrameX.ViewModel.DataContext
{
	public class FpsGraphDataContext : GraphDataContextBase
	{
		public PlotModel FpsModel { get => PlotModel; }

		public ICommand CopyFpsValuesCommand { get; }
		public ICommand CopyFpsPointsCommand { get; }

		public FpsGraphDataContext(IRecordDataServer recordDataServer,
			IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider) :
			base(recordDataServer, appConfiguration, frametimesStatisticProvider)
		{
			CopyFpsValuesCommand = new DelegateCommand(OnCopyFpsValues);
			CopyFpsPointsCommand = new DelegateCommand(OnCopyFpsPoints);
		}



		public void BuildPlotmodel(VisibleGraphs visibleGraphs, Action<PlotModel> onFinishAction = null)
		{
			var plotModel = PlotModel;
			Dispatcher.CurrentDispatcher.Invoke(() =>
			{
				Reset();
				plotModel.Axes.Add(AxisDefinitions[EPlotAxis.XAXIS]);
				plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISFPS]);

				SetFpsChart(plotModel, RecordDataServer.GetFpsPointTimeWindow());

				if (visibleGraphs.GpuLoad || visibleGraphs.CpuLoad || visibleGraphs.CpuMaxThreadLoad)
					plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISPERCENTAGE]);

				if (visibleGraphs.GpuLoad)
					SetGPULoadChart(plotModel, RecordDataServer.GetGPULoadPointTimeWindow());
				if (visibleGraphs.CpuLoad)
					SetCPULoadChart(plotModel, RecordDataServer.GetCPULoadPointTimeWindow());
				if (visibleGraphs.CpuMaxThreadLoad)
					SetCPUMaxThreadLoadChart(plotModel, RecordDataServer.GetCPUMaxThreadLoadPointTimeWindow());

				onFinishAction?.Invoke(plotModel);
				plotModel.InvalidatePlot(true);
			});

		}

		public void SetFpsChart(PlotModel plotModel, IList<Point> fpsPoints)
		{
			if (fpsPoints == null || !fpsPoints.Any())
				return;

			int count = fpsPoints.Count;
			var fpsDataPoints = fpsPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
			var yMin = fpsPoints.Min(pnt => pnt.Y);
			var yMax = fpsPoints.Max(pnt => pnt.Y);
			var frametimes = RecordDataServer.GetFrametimeTimeWindow();
			double average = frametimes.Count * 1000 / frametimes.Sum();
			var averageDataPoints = fpsPoints.Select(pnt => new DataPoint(pnt.X, average));

			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				plotModel.Series.Clear();

				var fpsSeries = new LineSeries { Title = "FPS", StrokeThickness = 1, LegendStrokeThickness = 4, Color = ColorRessource.FpsStroke };
				var averageSeries = new LineSeries { Title = "Average FPS", StrokeThickness = 2, LegendStrokeThickness = 4, Color = ColorRessource.FpsAverageStroke };

				fpsSeries.Points.AddRange(fpsDataPoints);
				averageSeries.Points.AddRange(averageDataPoints);


				UpdateAxis(EPlotAxis.XAXIS, (axis) =>
				{
					axis.Minimum = fpsPoints.First().X;
					axis.Maximum = fpsPoints.Last().X;
				});

				UpdateAxis(EPlotAxis.YAXISFPS, (axis) =>
				{
					axis.Minimum = yMin - (yMax - yMin) / 6;
					axis.Maximum = yMax + (yMax - yMin) / 6;
				});



				plotModel.Series.Add(fpsSeries);
				plotModel.Series.Add(averageSeries);

				plotModel.InvalidatePlot(true);
			}));
		}

		private void OnCopyFpsValues()
		{
			if (RecordSession == null)
				return;

			var fps = RecordDataServer.GetFpsTimeWindow();
			StringBuilder builder = new StringBuilder();

			foreach (var framerate in fps)
			{
				builder.Append(framerate.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyFpsPoints()
		{
			if (RecordSession == null)
				return;

			var fpsPoints = RecordDataServer.GetFpsPointTimeWindow();
			StringBuilder builder = new StringBuilder();

			for (int i = 0; i < fpsPoints.Count; i++)
			{
				builder.Append(fpsPoints[i].X.ToString(CultureInfo.InvariantCulture) + "\t" +
					fpsPoints[i].Y.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}


	}
}
