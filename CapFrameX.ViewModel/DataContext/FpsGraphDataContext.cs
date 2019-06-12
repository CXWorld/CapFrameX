using CapFrameX.Contracts.Configuration;
using CapFrameX.OcatInterface;
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

		public FpsGraphDataContext(IRecordDataServer recordDataServer,
			IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider) :
			base(recordDataServer, appConfiguration, frametimesStatisticProvider)
		{
			CopyFpsValuesCommand = new DelegateCommand(OnCopyFpsValues);

			// Update Chart after changing index slider
			RecordDataServer.FpsDataStream.Subscribe(sequence =>
			{
				SetFpsChart(sequence);
				GraphNumberSamples = sequence.Count;
			});

			FpsModel = new PlotModel
			{
				PlotMargins = new OxyThickness(40, 10, 0, 40),
				PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
				LegendPosition = LegendPosition.TopCenter,
				LegendOrientation = LegendOrientation.Horizontal
			};

			//Axes
			//X
			FpsModel.Axes.Add(new LinearAxis()
			{
				Key = "xAxis",
				Position = AxisPosition.Bottom,
				Title = "Samples",
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
		}

		private void OnCopyFpsValues()
		{
			if (RecordSession == null)
				return;

			RecordDataServer.RemoveOutlierMethod
				= UseRemovingOutlier ? ERemoveOutlierMethod.DeciPercentile : ERemoveOutlierMethod.None;
			var fps = RecordDataServer.GetFpsSampleWindow();
			StringBuilder builder = new StringBuilder();

			foreach (var framerate in fps)
			{
				builder.Append(framerate.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		public void SetFpsChart(IList<double> fps)
		{
			int count = fps.Count;
			var fpsDataPoints = fps.Select((x, i) => new DataPoint(i, x));
			var yMin = fps.Min();
			var yMax = fps.Max();
			var frametimes = RecordDataServer.GetFrametimeSampleWindow();
			double average = frametimes.Count * 1000 / frametimes.Sum();
			var averageDataPoints = Enumerable.Repeat(average, frametimes.Count).Select((x, i) => new DataPoint(i, x));

			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				FpsModel.Series.Clear();

				var fpsSeries = new LineSeries { Title = "FPS", StrokeThickness = 1, Color = ColorRessource.FpsStroke };
				var averageSeries = new LineSeries { Title = "Average FPS", StrokeThickness = 2, Color = ColorRessource.FpsAverageStroke };

				fpsSeries.Points.AddRange(fpsDataPoints);
				averageSeries.Points.AddRange(averageDataPoints);

				var xAxis = FpsModel.GetAxisOrDefault("xAxis", null);
				var yAxis = FpsModel.GetAxisOrDefault("yAxis", null);

				xAxis.Minimum = 0;
				xAxis.Maximum = count;
				yAxis.Minimum = yMin - (yMax - yMin) / 6;
				yAxis.Maximum = yMax + (yMax - yMin) / 6;

				FpsModel.Series.Add(fpsSeries);
				FpsModel.Series.Add(averageSeries);

				FpsModel.InvalidatePlot(true);
			}));
		}
	}
}
