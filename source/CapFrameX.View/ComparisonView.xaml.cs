using CapFrameX.Configuration;
using CapFrameX.Data;
using CapFrameX.Statistics;
using CapFrameX.View.Controls;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Prism.Events;
using System;
using System.ComponentModel;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for ComparisonDataView.xaml
	/// </summary>
	public partial class ComparisonView : UserControl
	{
		public ComparisonView()
		{
			InitializeComponent();
			OxyPlotHelper.SetAxisZoomWheelAndPan(ComparisonPlotView);

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				var loggerFactory = new LoggerFactory();
				DataContext = new ComparisonViewModel(new FrametimeStatisticProvider(appConfiguration),
					new FrametimeAnalyzer(), new EventAggregator(), appConfiguration, new RecordManager(loggerFactory.CreateLogger<RecordManager>(), appConfiguration, new RecordDirectoryObserver(appConfiguration, loggerFactory.CreateLogger<RecordDirectoryObserver>()), new AppVersionProvider()));
			}

			var context = SynchronizationContext.Current;
			(DataContext as ComparisonViewModel)?.ResetLShapeChart
				.ObserveOn(context)
				.SubscribeOn(context)
				.Subscribe(dummy => ResetLShapeChart());
		}

		private void ResetFrametimeChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			ComparisonPlotView.ResetAllAxes();
		}

		private void ResetLShapeChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
			=> ResetLShapeChart();

		private void ResetLShapeChart()
		{
			//Use the axis MinValue/MaxValue properties to specify the values to display.
			//use double.Nan to clear it.

			LShapeX.MinValue = double.NaN;
			LShapeX.MaxValue = double.NaN;
			LShapeY.MinValue = double.NaN;
			LShapeY.MaxValue = double.NaN;
		}

		private void SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) { }
	}
}
