using CapFrameX.Contracts.Configuration;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

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
			};
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
			var fpsSeries = new LineSeries { Title = "FPS", StrokeThickness = 1, Color = OxyColor.FromRgb(139, 35, 35) };
			var averageSeries = new LineSeries { Title = "Average FPS", StrokeThickness = 1, Color = OxyColor.FromRgb(35, 139, 123) };

			fpsSeries.Points.AddRange(fps.Select((x, i) => new DataPoint(i, x)));
			var yMin = fps.Min();
			var yMax = fps.Max();
			var frametimes = RecordDataServer.GetFrametimeSampleWindow();
			double average = frametimes.Count * 1000 / frametimes.Sum();
			averageSeries.Points.AddRange(Enumerable.Repeat(average, frametimes.Count).Select((x, i) => new DataPoint(i, x)));

			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				var tmp = new PlotModel
				{
					PlotMargins = new OxyThickness(40, 10, 0, 40),
					PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
					LegendPosition = LegendPosition.TopCenter,
					LegendOrientation = LegendOrientation.Horizontal
				};

				tmp.Series.Add(fpsSeries);
				tmp.Series.Add(averageSeries);

				//Axes
				//X
				tmp.Axes.Add(new LinearAxis()
				{
					Key = "xAxis",
					Position = OxyPlot.Axes.AxisPosition.Bottom,
					Title = "Samples",
					Minimum = 0,
					Maximum = fps.Count,
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
					Title = "FPS [1/s]",
					Minimum = yMin - (yMax - yMin) / 6,
					Maximum = yMax + (yMax - yMin) / 6,
					MajorGridlineStyle = LineStyle.Solid,
					MajorGridlineThickness = 1,
					MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
					MinorTickSize = 0,
					MajorTickSize = 0
				});

				FpsModel = tmp;
			}));
		}
	}
}
