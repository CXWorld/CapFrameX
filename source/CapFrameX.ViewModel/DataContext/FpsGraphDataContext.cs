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

namespace CapFrameX.ViewModel.DataContext
{
	public class FpsGraphDataContext : GraphDataContextBase
	{
		private PlotModel _fpsModel;

		public PlotModel FpsModel
		{
			get { return _fpsModel; }
			set
			{
				_fpsModel = value;
				RaisePropertyChanged();
			}
		}

		public ICommand CopyFpsValuesCommand { get; }
		public ICommand CopyFpsPointsCommand { get; }

		public FpsGraphDataContext(IRecordDataServer recordDataServer,
			IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider) :
			base(recordDataServer, appConfiguration, frametimesStatisticProvider)
		{
			CopyFpsValuesCommand = new DelegateCommand(OnCopyFpsValues);
			CopyFpsPointsCommand = new DelegateCommand(OnCopyFpsPoints);

			// Update Chart after changing index slider
			RecordDataServer.FpsPointDataStream.Subscribe(sequence =>
			{
				SetFpsChart(sequence);
			});

			FpsModel = new PlotModel
			{
				PlotMargins = new OxyThickness(40, 0, 0, 40),
				PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
				LegendPlacement = LegendPlacement.Outside,
				LegendPosition = LegendPosition.BottomCenter,
				LegendOrientation = LegendOrientation.Horizontal,
				LegendMaxHeight = 25
			};

			//Axes
			//X
			FpsModel.Axes.Add(new LinearAxis()
			{
				Key = "xAxis",
				Position = AxisPosition.Bottom,
				Title = "Recording time [s]",
				MajorGridlineStyle = LineStyle.Solid,
				MajorGridlineThickness = 1,
				MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
				MinorTickSize = 0,
				MajorTickSize = 0
			});

			//Y
			FpsModel.Axes.Add(new LinearAxis()
			{
				Key = "yAxis",
				Position = AxisPosition.Left,
				Title = "FPS [1/s]",
				MajorGridlineStyle = LineStyle.Solid,
				MajorGridlineThickness = 1,
				MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
				MinorTickSize = 0,
				MajorTickSize = 0
			});

			//Y2
			FpsModel.Axes.Add(new LinearAxis()
			{
				Key = "yAxis2",
				Position = AxisPosition.Right,
				Title = "Load [%]",
				MajorGridlineStyle = LineStyle.Solid,
				MajorGridlineThickness = 1,
				MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
				MinorTickSize = 0,
				MajorTickSize = 0
			});
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

		public void SetFpsChart(IList<Point> fpsPoints)
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
				FpsModel.Series.Clear();

                var fpsSeries = new LineSeries { Title = "FPS", StrokeThickness = 1, LegendStrokeThickness = 4, Color = ColorRessource.FpsStroke };
				var averageSeries = new LineSeries { Title = "Average FPS", StrokeThickness = 2, LegendStrokeThickness = 4, Color = ColorRessource.FpsAverageStroke };

				fpsSeries.Points.AddRange(fpsDataPoints);
				averageSeries.Points.AddRange(averageDataPoints);

				var xAxis = FpsModel.GetAxisOrDefault("xAxis", null);
				var yAxis = FpsModel.GetAxisOrDefault("yAxis", null);

				xAxis.Minimum = fpsPoints.First().X;
				xAxis.Maximum = fpsPoints.Last().X;
				yAxis.Minimum = yMin - (yMax - yMin) / 6;
				yAxis.Maximum = yMax + (yMax - yMin) / 6;

				FpsModel.Series.Add(fpsSeries);
				FpsModel.Series.Add(averageSeries);

				FpsModel.InvalidatePlot(true);
			}));
		}
	}
}
