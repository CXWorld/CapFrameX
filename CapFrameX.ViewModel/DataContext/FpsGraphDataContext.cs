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
	public class FpsGraphDataContext : GraphDataContextBase
	{
		private SeriesCollection _fpsCollection;

		public SeriesCollection FpsCollection
		{
			get { return _fpsCollection; }
			set
			{
				_fpsCollection = value;
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
				builder.Append(framerate + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}

		public void SetFpsChart(IList<double> fps)
		{
			//var gradientBrush = new LinearGradientBrush
			//{
			//	StartPoint = new Point(0, 0),
			//	EndPoint = new Point(0, 1)
			//};

			// ToDo: Get color from ressources
			//gradientBrush.GradientStops.Add(new GradientStop(Color.FromRgb(139, 35, 35), 0));
			//gradientBrush.GradientStops.Add(new GradientStop(Colors.Transparent, 1));

			var fpsValues = new GearedValues<double>();
			fpsValues.AddRange(fps);
			fpsValues.WithQuality(AppConfiguration.ChartQualityLevel.ConverToEnum<Quality>());

			var averageValues = new GearedValues<double>();
			var frametimes = RecordDataServer.GetFrametimeSampleWindow();
			double average = frametimes.Count * 1000 / frametimes.Sum();
			averageValues.AddRange(Enumerable.Repeat(average, frametimes.Count));
			averageValues.WithQuality(AppConfiguration.ChartQualityLevel.ConverToEnum<Quality>());

			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				FpsCollection = new SeriesCollection()
				{
					new GLineSeries
					{
						Title = "FPS",
						Values = fpsValues,
						Fill = Brushes.Transparent,
						Stroke = new SolidColorBrush(Color.FromRgb(139,35,35)),
						StrokeThickness = 1,
						LineSmoothness= 0,
						PointGeometrySize = 0
					},
					new GLineSeries
					{
						Title = string.Format(CultureInfo.InvariantCulture, "Average FPS", AppConfiguration.MovingAverageWindowSize),
						Values = averageValues,
						Fill = Brushes.Transparent,
						Stroke = new SolidColorBrush(Color.FromRgb(35, 139, 123)),
						StrokeThickness = 1,
						LineSmoothness= 0,
						PointGeometrySize = 0
					}
				};
			}));
		}
	}
}
