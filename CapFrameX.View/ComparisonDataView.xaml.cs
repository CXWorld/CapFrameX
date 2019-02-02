using CapFrameX.Configuration;
using CapFrameX.Statistics;
using CapFrameX.ViewModel;
using Prism.Events;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CapFrameX.View
{
	/// <summary>
	/// Interaction logic for ComparisonDataView.xaml
	/// </summary>
	public partial class ComparisonDataView : UserControl
	{
		public ComparisonDataView()
		{
			InitializeComponent();

			// Design time!
			if (DesignerProperties.GetIsInDesignMode(this))
			{
				var appConfiguration = new CapFrameXConfiguration();
				DataContext = new ComparisonDataViewModel(new FrametimeStatisticProvider(), new FrametimeAnalyzer(), new EventAggregator(), appConfiguration);
			}
		}

		private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			//Use the axis MinValue/MaxValue properties to specify the values to display.
			//use double.Nan to clear it.

			FrametimesX.MinValue = double.NaN;
			FrametimesX.MaxValue = double.NaN;
			FrametimesY.MinValue = double.NaN;
			FrametimesY.MaxValue = double.NaN;

			LShapeX.MinValue = double.NaN;
			LShapeX.MaxValue = double.NaN;
			LShapeY.MinValue = double.NaN;
			LShapeY.MaxValue = double.NaN;
		}

		private void SelectedColorChanged(object sender, RoutedPropertyChangedEventArgs<Color?> e) { }
	}
}
