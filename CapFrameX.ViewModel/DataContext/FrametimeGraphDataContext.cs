using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
using LiveCharts;
using LiveCharts.Geared;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.ViewModel.DataContext
{
	public class FrametimeGraphDataContext : GraphDataContextBase
	{
		private SeriesCollection _frametimeCollection;

		public SeriesCollection FrametimeCollection
		{
			get { return _frametimeCollection; }
			set
			{
				_frametimeCollection = value;
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
		}

		public void SetFrametimeChart(IList<double> frametimes)
		{
			//var gradientBrush = new LinearGradientBrush
			//{
			//	StartPoint = new Point(0, 0),
			//	EndPoint = new Point(0, 1)
			//};

			// ToDo: Get color from ressources
			//gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(139, 35, 35), 0));
			//gradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));

			var frametimeValues = new GearedValues<double>();
			frametimeValues.AddRange(frametimes);
			frametimeValues.WithQuality(AppConfiguration.ChartQualityLevel.ConverToEnum<Quality>());

			var movingAverageValues = new GearedValues<double>();
			movingAverageValues.AddRange(FrametimesStatisticProvider.GetMovingAverage(frametimes, AppConfiguration.MovingAverageWindowSize));
			movingAverageValues.WithQuality(AppConfiguration.ChartQualityLevel.ConverToEnum<Quality>());

			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				SeriesCollection = new SeriesCollection()
				{
					new GLineSeries
					{
						Title = "Frametimes",
						Values = frametimeValues,
						Fill = Brushes.Transparent,
						Stroke = new SolidColorBrush(Color.FromRgb(139,35,35)),
						StrokeThickness = 1,
						LineSmoothness= 0,
						PointGeometrySize = 0
					},
					new GLineSeries
					{
						Title = string.Format(CultureInfo.InvariantCulture, "Moving average (window size = {0})", AppConfiguration.MovingAverageWindowSize),
						Values = movingAverageValues,
						Fill = Brushes.Transparent,
						Stroke = new SolidColorBrush(Color.FromRgb(35, 139, 123)),
						StrokeThickness = 1,
						LineSmoothness= 0,
						PointGeometrySize = 0
					}
				};
			}));
		}

		private void OnCopyFrametimeValues()
		{
			if (RecordSession == null)
				return;

			RecordDataServer.RemoveOutlierMethod 
				= UseRemovingOutlier ? ERemoveOutlierMethod.DeciPercentile : ERemoveOutlierMethod.None;
			var frametimes = 
				UseSlidingWindow ? RecordDataServer.GetFrametimeSampleWindow() : RecordDataServer.GetFrametimeSampleWindow();
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
			var frametimePoints =
				UseSlidingWindow ? RecordDataServer.GetFrametimePointTimeWindow() : RecordDataServer.GetFrametimePointSampleWindow();
			StringBuilder builder = new StringBuilder();

			for (int i = 0; i < frametimePoints.Count; i++)
			{
				builder.Append(frametimePoints[i].X + "\t" + frametimePoints[i].Y + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}
	}
}
