using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IBufferObservable = System.IObservable<System.Collections.Generic.IEnumerable<System.Windows.Point>>;

namespace CapFrameX.Chart
{
	public class PlotBase : UserControl
	{
		public static readonly DependencyProperty BufferObservableProperty = DependencyProperty.Register(
			"BufferObservable", typeof(IBufferObservable), typeof(PlotBase), new FrameworkPropertyMetadata(default(IBufferObservable), BufferObservableChanged));

		private static void BufferObservableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var plotter = (PlotBase)d;
			var newValue = (IBufferObservable)e.NewValue;

			plotter.OnBufferObservableChanged(newValue);
		}

		public IBufferObservable BufferObservable
		{
			get { return (IBufferObservable)GetValue(BufferObservableProperty); }
			set { SetValue(BufferObservableProperty, value); }
		}

		public static readonly DependencyProperty SampleProperty = DependencyProperty.Register(
			"Sample", typeof(bool), typeof(PlotBase), new PropertyMetadata(true));

		public bool Sample
		{
			get { return (bool)GetValue(SampleProperty); }
			set { SetValue(SampleProperty, value); }
		}

		public static readonly DependencyProperty GraphPenProperty = DependencyProperty.Register(
			"GraphPen", typeof(Pen), typeof(PlotBase), new PropertyMetadata(new Pen(Brushes.Blue, 1)));

		public Pen GraphPen
		{
			get { return (Pen)GetValue(GraphPenProperty); }
			set { SetValue(GraphPenProperty, value); }
		}

		public static readonly DependencyProperty PlotTimeProperty = DependencyProperty.Register(
			"PlotTime", typeof(double), typeof(PlotBase), new FrameworkPropertyMetadata(5000.0, PlotTimeChanged));

		private static void PlotTimeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{

			var plot = (PlotBase)dependencyObject;
			plot.OnPlotTimeChanged((double)dependencyPropertyChangedEventArgs.NewValue);
		}

		public double PlotTime
		{
			get { return (double)GetValue(PlotTimeProperty); }
			set { SetValue(PlotTimeProperty, value); }
		}

		public static readonly DependencyProperty CutLineOnNegativeTimeDeltaProperty = DependencyProperty.Register(
			"CutLineOnNegativeTimeDelta", typeof(bool), typeof(PlotBase), new PropertyMetadata(true));

		public bool CutLineOnNegativeTimeDelta
		{
			get { return (bool)GetValue(CutLineOnNegativeTimeDeltaProperty); }
			set { SetValue(CutLineOnNegativeTimeDeltaProperty, value); }
		}

		public static readonly DependencyProperty TimeOffsetProperty = DependencyProperty.Register(
			"TimeOffset", typeof(double), typeof(PlotBase), new FrameworkPropertyMetadata(0.0, TimeOffsetChanged));

		public double TimeOffset
		{
			get { return (double)GetValue(TimeOffsetProperty); }
			set { SetValue(TimeOffsetProperty, value); }
		}

		private static void TimeOffsetChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
		{

			var plot = (PlotBase)dependencyObject;
			plot.OnTimeOffsetChanged((double)dependencyPropertyChangedEventArgs.NewValue);
		}

		protected virtual void OnBufferObservableChanged(IBufferObservable newObservable) { }

		protected virtual void OnPlotTimeChanged(double newTimeMs) { }

		protected virtual void OnTimeOffsetChanged(double newTimeMs) { }
	}
}
