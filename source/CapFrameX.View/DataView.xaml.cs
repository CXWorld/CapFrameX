using CapFrameX.View.UITracker;
using CapFrameX.ViewModel;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for DataView.xaml
    /// </summary>
    public partial class DataView : UserControl
    {
        public DataView()
        {
            InitializeComponent();

            var viewmodel = (DataContext as DataViewModel);

            var context = SynchronizationContext.Current;
            viewmodel?.ResetLShapeChart
                .ObserveOn(context)
                .SubscribeOn(context)
                .Subscribe(dummy => ResetLShapeChart());

            var rowAWidthTracker = new RowHeightTracker(Application.Current.MainWindow);
            var rowBWidthTracker = new RowHeightTracker(Application.Current.MainWindow);

            rowAWidthTracker.Tracker.Track(UpperRow);
            rowBWidthTracker.Tracker.Track(LowerRow);
            
            // L-shape chart y axis formatter
            Func<double, string> formatFunc = (x) => string.Format("{0:0.0}", x);
            LShapeY.LabelFormatter = formatFunc;
        }

        private void Chart_OnDataClick(object sender, ChartPoint chartpoint)
        {
            var chart = (PieChart)chartpoint.ChartView;
            var selectedSeries = (PieSeries)chartpoint.SeriesView;

            //clear selected slice
            if (selectedSeries.PushOut == 8)
            {
                selectedSeries.PushOut = 0;
                selectedSeries.StrokeThickness = 0;
            }

            else
            {
                foreach (PieSeries series in chart.Series)
                {
                    series.PushOut = 0;
                    series.StrokeThickness = 0;
                }

                selectedSeries.PushOut = 8;
                selectedSeries.StrokeThickness = 1;
            }
        }

        private void ResetChart_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
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

        private void RangeSlider_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            (DataContext as DataViewModel).OnRangeSliderDragCompleted();
        }

        private void FirstSecondsTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key;

            if (key == Key.Enter)
            {
                GraphTab.Focus();
                (DataContext as DataViewModel).OnRangeSliderDragCompleted();
            }
        }

        private void LastSecondsTextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key;

            if (key == Key.Enter)
            {
                GraphTab.Focus();
                (DataContext as DataViewModel).OnRangeSliderDragCompleted();
            }
        }

        private void StutteringThreshold_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key;

            if (key == Key.Enter)
            {
                GraphTab.Focus();
                (DataContext as DataViewModel).OnStutteringOptionsChanged();
            }
        }

        private void StutteringFactor_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            var key = e.Key;

            if (key == Key.Enter)
            {
                GraphTab.Focus();
                (DataContext as DataViewModel).OnStutteringOptionsChanged();
            }
        }

        private void CustomTitle_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var key = e.Key;

            if (key == Key.Enter)
            {
                GraphTab.Focus();
            }
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9.-]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void Grid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ActualWidth >= 2100)
            {
                SensorDataTab.Visibility = Visibility.Collapsed;
                ThresholdTab.Visibility = Visibility.Collapsed;
               
                SecondThresholdTab.Visibility = Visibility.Visible;
                SecondSensorTab.Visibility = Visibility.Visible;

                if (StatisticsTabControl.SelectedIndex == 2 )
                    StatisticsTabControl.SelectedIndex = 0;
            }
            else if (ActualWidth >= 1650)
            {
                SensorDataTab.Visibility = Visibility.Collapsed;
                ThresholdTab.Visibility = Visibility.Visible;

                SecondSensorTab.Visibility = Visibility.Visible;
                SecondThresholdTab.Visibility = Visibility.Collapsed;

                if (StatisticsTabControl.SelectedIndex == 3)
                    StatisticsTabControl.SelectedIndex = 0;
            }
            else
            {
                SensorDataTab.Visibility = Visibility.Visible;
                ThresholdTab.Visibility = Visibility.Visible;

                SecondSensorTab.Visibility = Visibility.Collapsed;
                SecondThresholdTab.Visibility = Visibility.Collapsed;
            }
        }
    }
}
