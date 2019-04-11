using CapFrameX.Contracts.Configuration;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel.DataContext
{
	public class FrametimeGraphDataContext : GraphDataContextBase
	{
		private PlotModel _frametimeModel;

		public PlotModel FrametimeModel
		{
			get { return _frametimeModel; }
			set
			{
				_frametimeModel = value;
				RaisePropertyChanged();
			}
		}

		public ICommand CopyFrametimeValuesCommand { get; }

		public ICommand CopyFrametimePointsCommand { get; }

		public FrametimeGraphDataContext(IRecordDataServer recordDataServer, IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider) :
			base(recordDataServer, appConfiguration, frametimesStatisticProvider)
		{
			CopyFrametimeValuesCommand = new DelegateCommand(OnCopyFrametimeValues);
			CopyFrametimePointsCommand = new DelegateCommand(OnCopyFrametimePoints);

			// Update Chart after changing index slider
			RecordDataServer.FrametimeDataStream.Subscribe(sequence =>
			{
				SetFrametimeChart(sequence);
				GraphNumberSamples = sequence.Count;
			});

			FrametimeModel = new PlotModel
			{
				PlotMargins = new OxyThickness(40, 10, 0, 40),
				PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
			};
		}

		public void SetFrametimeChart(IList<double> frametimes)
		{
			var frametimeSeries = new LineSeries { Title = "Frametimes", StrokeThickness = 1, Color = OxyColor.FromRgb(139, 35, 35) };
			var movingAverageSeries = new LineSeries
			{
				Title = string.Format(CultureInfo.InvariantCulture,
				"Moving average (window size = {0})", AppConfiguration.MovingAverageWindowSize),
				StrokeThickness = 1,
				Color = OxyColor.FromRgb(35, 139, 123)
			};

			frametimeSeries.Points.AddRange(frametimes.Select((x, i) => new DataPoint(i, x)));
			var movingAverage = FrametimesStatisticProvider.GetMovingAverage(frametimes, AppConfiguration.MovingAverageWindowSize);
			movingAverageSeries.Points.AddRange(movingAverage.Select((x, i) => new DataPoint(i, x)));

			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				var tmp = new PlotModel
				{
					PlotMargins = new OxyThickness(40, 10, 0, 40),
					PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
					LegendPosition = LegendPosition.TopCenter,
					LegendOrientation = LegendOrientation.Horizontal
				};

				tmp.Series.Add(frametimeSeries);
				tmp.Series.Add(movingAverageSeries);

				//Axes
				//X
				tmp.Axes.Add(new LinearAxis()
				{
					Key = "xAxis",
					Position = OxyPlot.Axes.AxisPosition.Bottom,
					Title = "Samples",
					MajorGridlineStyle = LineStyle.Solid,
					MajorGridlineThickness = 1,
					MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
					MinorTickSize = 0,
					MajorTickSize = 0
				});

				//Y
				tmp.Axes.Add(new LinearAxis()
				{
					Key = "yAxis",
					Position = OxyPlot.Axes.AxisPosition.Left,
					Title = "Frametimes [ms]",
					MajorGridlineStyle = LineStyle.Solid,
					MajorGridlineThickness = 1,
					MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
					MinorTickSize = 0,
					MajorTickSize = 0
				});

				FrametimeModel = tmp;
			}));
		}

		private void OnCopyFrametimeValues()
		{
			if (RecordSession == null)
				return;

			RecordDataServer.RemoveOutlierMethod
				= UseRemovingOutlier ? ERemoveOutlierMethod.DeciPercentile : ERemoveOutlierMethod.None;
			var frametimes = RecordDataServer.GetFrametimeSampleWindow();
			StringBuilder builder = new StringBuilder();

			foreach (var frametime in frametimes)
			{
				builder.Append(frametime + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		private void OnCopyFrametimePoints()
		{
			if (RecordSession == null)
				return;

			RecordDataServer.RemoveOutlierMethod
				= UseRemovingOutlier ? ERemoveOutlierMethod.DeciPercentile : ERemoveOutlierMethod.None;
			var frametimePoints = RecordDataServer.GetFrametimePointSampleWindow();
			StringBuilder builder = new StringBuilder();

			for (int i = 0; i < frametimePoints.Count; i++)
			{
				builder.Append(frametimePoints[i].X + "\t" + frametimePoints[i].Y + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}
	}
}
