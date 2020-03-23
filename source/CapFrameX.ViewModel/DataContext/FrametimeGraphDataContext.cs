using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Statistics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CapFrameX.ViewModel.DataContext
{
	public class FrametimeGraphDataContext : GraphDataContextBase
	{
		public ICommand CopyFrametimeValuesCommand { get; }

		public ICommand CopyFrametimePointsCommand { get; }

		public PlotModel FrametimeModel { get => PlotModel; }

		public FrametimeGraphDataContext(IRecordDataServer recordDataServer, IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider) :
			base(recordDataServer, appConfiguration, frametimesStatisticProvider)
		{
			CopyFrametimeValuesCommand = new DelegateCommand(OnCopyFrametimeValues);
			CopyFrametimePointsCommand = new DelegateCommand(OnCopyFrametimePoints);
		}

		public void BuildPlotmodel(VisibleGraphs visibleGraphs, Action<PlotModel> onFinishAction = null)
		{
			var plotModel = PlotModel;
			Dispatcher.CurrentDispatcher.Invoke(() =>
			{
				Reset();
				plotModel.Axes.Add(AxisDefinitions[EPlotAxis.XAXIS]);
				plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISFRAMETIMES]);

				SetFrametimeChart(plotModel, RecordDataServer.GetFrametimePointTimeWindow());

				if (visibleGraphs.IsAnyGraphVisible)
					plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISPERCENTAGE]);

				if (visibleGraphs.GpuLoad)
					SetGPULoadChart(plotModel, RecordDataServer.GetGPULoadPointTimeWindow());
				if(visibleGraphs.CpuLoad)
					SetCPULoadChart(plotModel, RecordDataServer.GetCPULoadPointTimeWindow());
				if (visibleGraphs.CpuMaxThreadLoad)
					SetCPUMaxThreadLoadChart(plotModel, RecordDataServer.GetCPUMaxThreadLoadPointTimeWindow());

				onFinishAction?.Invoke(plotModel);
				plotModel.InvalidatePlot(true);
			});
		}

		private void SetFrametimeChart(PlotModel plotModel, IList<Point> frametimePoints)
		{
			if (frametimePoints == null || !frametimePoints.Any())
				return;

			int count = frametimePoints.Count;
			var frametimeDataPoints = frametimePoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
			var yMin = frametimePoints.Min(pnt => pnt.Y);
			var yMax = frametimePoints.Max(pnt => pnt.Y);
			var movingAverage = FrametimesStatisticProvider
				.GetMovingAverage(frametimePoints.Select(pnt => pnt.Y)
				.ToList(), AppConfiguration.MovingAverageWindowSize);
			var frametimeSeries = new LineSeries
			{
				Title = "Frametimes",
				StrokeThickness = 1,
				LegendStrokeThickness = 4,
				Color = ColorRessource.FrametimeStroke
			};
			var movingAverageSeries = new LineSeries
			{
				Title = "Moving average",
				StrokeThickness = 2,
				LegendStrokeThickness = 4,
				Color = ColorRessource.FrametimeMovingAverageStroke
			};

			frametimeSeries.Points.AddRange(frametimeDataPoints);
			movingAverageSeries.Points.AddRange(movingAverage.Select((y, i) => new DataPoint(frametimePoints[i].X, y)));

			UpdateAxis(EPlotAxis.XAXIS, (axis) =>
			{
				axis.Minimum = frametimePoints.First().X;
				axis.Maximum = frametimePoints.Last().X;
			});
			//var yAxis = FrametimeModel.GetAxisOrDefault("yAxis", null);


			//yAxis.Minimum = yMin - (yMax - yMin) / 6;
			//yAxis.Maximum = yMax + (yMax - yMin) / 6;

			plotModel.Series.Add(frametimeSeries);
			plotModel.Series.Add(movingAverageSeries);
		}

		private void OnCopyFrametimeValues()
		{
			if (RecordSession == null)
				return;

			var frametimes = RecordDataServer.GetFrametimeTimeWindow();
			StringBuilder builder = new StringBuilder();

			foreach (var frametime in frametimes)
			{
				builder.Append(frametime.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyFrametimePoints()
		{
			if (RecordSession == null)
				return;

			var frametimePoints = RecordDataServer.GetFrametimePointTimeWindow();
			StringBuilder builder = new StringBuilder();

			for (int i = 0; i < frametimePoints.Count; i++)
			{
				builder.Append(frametimePoints[i].X.ToString(CultureInfo.InvariantCulture) + "\t" +
					frametimePoints[i].Y.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}
	}
}
