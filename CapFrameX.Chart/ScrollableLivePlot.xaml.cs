using CapFrameX.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IBufferObservable = System.IObservable<System.Collections.Generic.IEnumerable<System.Windows.Point>>;

namespace CapFrameX.Chart
{
	/// <summary>
	/// Interaktionslogik für ScrollableRxPlot.xaml
	/// </summary>
	public partial class ScrollableRxPlot
	{
		private static readonly TimeSpan timeSampleDelay = TimeSpan.FromMilliseconds(100);

		public static readonly DependencyProperty BufferObservableProperty = DependencyProperty.Register(
			"BufferObservable", typeof(IBufferObservable), typeof(ScrollableRxPlot), new FrameworkPropertyMetadata(default(IBufferObservable), BufferObservableChanged));

		private static void BufferObservableChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{
			var plot = (ScrollableRxPlot)dependencyObject;
			var observable = (IBufferObservable)dependencyPropertyChangedEventArgs.NewValue;

			if (plot.bufferSubscription != null)
				plot.bufferSubscription.Dispose();

			plot.bufferSubscription = observable.Sample(timeSampleDelay).ObserveOnDispatcher().Subscribe(wnd =>
			{
				if (plot.IsEnabled)
				{
					//var start = wnd.FirstOrDefault().GetOrFallbackOnNull(s => s.X, 0U);
					//var end = wnd.LastOrDefault().GetOrFallbackOnNull(s => s.X, 0U);
					var start = wnd.FirstOrDefault().X;
					var end = wnd.LastOrDefault().X;

					if (start < end)
					{
						plot.BufferStartTime = start;
						plot.BufferEndTime = end;
					}
				}

			});
		}

		public IBufferObservable BufferObservable
		{
			get { return (IBufferObservable)GetValue(BufferObservableProperty); }
			set { SetValue(BufferObservableProperty, value); }
		}

		public static readonly DependencyProperty GraphPenProperty = DependencyProperty.Register(
			"GraphPen", typeof(Pen), typeof(ScrollableRxPlot), new PropertyMetadata(new Pen(new SolidColorBrush(Color.FromRgb(139, 35, 35)), 0.2f)));

		public Pen GraphPen
		{
			get { return (Pen)GetValue(GraphPenProperty); }
			set { SetValue(GraphPenProperty, value); }
		}

		public static readonly DependencyProperty PlotTimeProperty = DependencyProperty.Register(
			"PlotTime", typeof(double), typeof(ScrollableRxPlot), new PropertyMetadata(10.0, PlotTimeChanged));

		private static void PlotTimeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{
			//var plot = (ScrollableRxPlot)dependencyObject;
			//var time = ((double)dependencyPropertyChangedEventArgs.NewValue);

			//plot.TotalTimeLabel.Content = String.Format("{0} s", time);
		}

		public double PlotTime
		{
			get { return (double)GetValue(PlotTimeProperty); }
			set { SetValue(PlotTimeProperty, value); }
		}

		public static readonly DependencyProperty SelectableTimeProperty = DependencyProperty.Register(
			"SelectableTime", typeof(double), typeof(ScrollableRxPlot), new PropertyMetadata(5000.0, SelectableTimeChanged));

		private static void SelectableTimeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{
			var plot = (ScrollableRxPlot)dependencyObject;
			plot.UpdateScrollEndTime();
		}

		public double SelectableTime
		{
			get { return (double)GetValue(SelectableTimeProperty); }
			set { SetValue(SelectableTimeProperty, value); }
		}

		public static readonly DependencyProperty RowAmplitudeProperty = DependencyProperty.Register(
			"RowAmplitude", typeof(double), typeof(ScrollableRxPlot), new PropertyMetadata(default(double)));

		public double RowAmplitude
		{
			get { return (double)GetValue(RowAmplitudeProperty); }
			set { SetValue(RowAmplitudeProperty, value); }
		}

		public static readonly DependencyProperty TimeOffsetProperty = DependencyProperty.Register(
			"TimeOffset", typeof(double), typeof(ScrollableRxPlot), new PropertyMetadata(0.0, TimeOffsetChanged));

		private static void TimeOffsetChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{
			var plot = (ScrollableRxPlot)dependencyObject;
			var newValue = (double)dependencyPropertyChangedEventArgs.NewValue;

			plot.scrollController.CurValue = newValue;
		}

		public double TimeOffset
		{
			get { return (double)GetValue(TimeOffsetProperty); }
			set { SetValue(TimeOffsetProperty, value); }
		}

		public static readonly DependencyProperty BufferStartTimeProperty = DependencyProperty.Register(
			"BufferStartTime", typeof(double), typeof(ScrollableRxPlot), new PropertyMetadata(default(double), BufferStartTimeChanged));

		private static void BufferStartTimeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{
			var plot = (ScrollableRxPlot)dependencyObject;
			var newValue = (double)dependencyPropertyChangedEventArgs.NewValue;

			plot.scrollController.MinValue = newValue;
		}

		public double BufferStartTime
		{
			get { return (double)GetValue(BufferStartTimeProperty); }
			set { SetValue(BufferStartTimeProperty, value); }
		}

		public static readonly DependencyProperty BufferEndTimeProperty = DependencyProperty.Register(
			"BufferEndTime", typeof(double), typeof(ScrollableRxPlot), new PropertyMetadata(default(double), BufferEndTimeChanged));

		private static void BufferEndTimeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{
			var plot = (ScrollableRxPlot)dependencyObject;
			plot.UpdateScrollEndTime();
		}

		public double BufferEndTime
		{
			get { return (double)GetValue(BufferEndTimeProperty); }
			private set { SetValue(BufferEndTimeProperty, value); }
		}

		public static readonly DependencyProperty ScrollEndTimeProperty = DependencyProperty.Register(
			"ScrollEndTime", typeof(double), typeof(ScrollableRxPlot), new PropertyMetadata(default(double), ScrollEndTimeChanged));

		private static void ScrollEndTimeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{
			var plot = (ScrollableRxPlot)dependencyObject;
			var newVal = (double)dependencyPropertyChangedEventArgs.NewValue;

			plot.scrollController.MaxValue = newVal;
		}

		public double ScrollEndTime
		{
			get { return (double)GetValue(ScrollEndTimeProperty); }
			private set { SetValue(ScrollEndTimeProperty, value); }
		}

		private IDisposable bufferSubscription;

		private readonly AxisMotionScrollingController scrollController;

		public ScrollableRxPlot()
		{
			InitializeComponent();

			scrollController = new AxisMotionScrollingController(time => TimeOffset = time)
			{
				MinValue = BufferStartTime,
				MaxValue = ScrollEndTime,
				CurValue = TimeOffset,
				Enabled = IsEnabled
			};

			SelectedTimeComboBox.ItemsSource = new List<int>() { 5, 10, 15, 20, 25, 30, 40, 50, 60 };
			SelectedTimeComboBox.SelectedValue = 10;
			PlotTime = 10;
			SelectableTime = 10;
			IsEnabledChanged += (s, e) => scrollController.Enabled = (bool)e.NewValue;
		}


		private void UpdateScrollEndTime()
		{
			var newEnd = BufferEndTime - SelectableTime;
			ScrollEndTime = newEnd < BufferStartTime ? BufferStartTime : newEnd;
		}

		private void Grid_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
		{
			if (IsInsidePlotter(e))
			{
				scrollController.StartInput(TransformMouseCoordinate(e));
				e.Handled = true;
			}
		}

		private void Grid_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
		{
			scrollController.StopInput(TransformMouseCoordinate(e));
		}

		private void Grid_OnPreviewMouseMove(object sender, MouseEventArgs e)
		{
			scrollController.IncrementInput(TransformMouseCoordinate(e));
		}

		private void Grid_OnMouseLeave(object sender, MouseEventArgs e)
		{
			scrollController.StopInput(TransformMouseCoordinate(e));
		}


		private double TransformMouseCoordinate(MouseEventArgs e)
		{
			return e.GetPosition(RootGrid).X / RootGrid.ActualWidth * PlotTime * -1;
		}

		private bool IsInsidePlotter(MouseEventArgs e)
		{
			var posInsidePlotter = e.GetPosition(Plotter);
			return posInsidePlotter.X >= 0 && posInsidePlotter.Y >= 0
				   && posInsidePlotter.X <= Plotter.ActualWidth && posInsidePlotter.Y <= Plotter.ActualHeight;
		}

		private void ComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
		{
			int value = Convert.ToInt32((sender as ComboBox).SelectedValue);
			PlotTime = value;
			SelectableTime = value;
			TimeOffset = 0;
		}
	}
}
