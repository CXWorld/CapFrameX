using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Media;
using IBufferObservable = System.IObservable<System.Collections.Generic.IEnumerable<System.Windows.Point>>;

namespace CapFrameX.Chart
{
	public class LivePlotter : FrameworkElement
	{
		public static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(60);

		public static readonly DependencyProperty BufferProperty = DependencyProperty.Register(
			"Buffer", typeof(IBufferObservable), typeof(LivePlotter), new PropertyMetadata(default(IBufferObservable), OnBufferPropertyChanged));

		private static void OnBufferPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var newValue = e.NewValue as IBufferObservable;

			if (d is LivePlotter plotter && newValue != null)
				plotter.SubscribeToNewBuffer(newValue, plotter.Sample);
		}

		public static readonly DependencyProperty SampleProperty = DependencyProperty.Register(
			"Sample", typeof(bool), typeof(LivePlotter), new PropertyMetadata(true, SampleChanged));

		private static void SampleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var newValue = (bool)e.NewValue;

			if (d is LivePlotter plotter)
				plotter.SubscribeToNewBuffer(plotter.Buffer, newValue);
		}

		public bool Sample
		{
			get { return (bool)GetValue(SampleProperty); }
			set { SetValue(SampleProperty, value); }
		}

		public IBufferObservable Buffer
		{
			get { return (IBufferObservable)GetValue(BufferProperty); }
			set { SetValue(BufferProperty, value); }
		}

		public static readonly DependencyProperty TotalPointCountProperty = DependencyProperty.Register(
			"TotalPointCount", typeof(int), typeof(LivePlotter), new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.AffectsRender));


		public int TotalPointCount
		{
			get { return (int)GetValue(TotalPointCountProperty); }
			set { SetValue(TotalPointCountProperty, value); }
		}

		public static readonly DependencyProperty TimeOffsetProperty = DependencyProperty.Register(
			"TimeOffset", typeof(double), typeof(LivePlotter), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

		public double TimeOffset
		{
			get { return (double)GetValue(TimeOffsetProperty); }
			set { SetValue(TimeOffsetProperty, value); }
		}

		public static readonly DependencyProperty CutLineOnNegativeTimeDeltaProperty = DependencyProperty.Register(
			"CutLineOnNegativeTimeDelta", typeof(bool), typeof(LivePlotter), new PropertyMetadata(true));


		public bool CutLineOnNegativeTimeDelta
		{
			get { return (bool)GetValue(CutLineOnNegativeTimeDeltaProperty); }
			set { SetValue(CutLineOnNegativeTimeDeltaProperty, value); }
		}

		public static readonly DependencyProperty TotalTimeProperty = DependencyProperty.Register(
			"TotalTime", typeof(double), typeof(LivePlotter), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

		public double TotalTime
		{
			get { return (double)GetValue(TotalTimeProperty); }
			set { SetValue(TotalTimeProperty, value); }
		}

		public static readonly DependencyProperty XScaleProperty = DependencyProperty.Register(
			"XScale", typeof(double), typeof(LivePlotter), new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

		public double XScale
		{
			get { return (double)GetValue(XScaleProperty); }
			set { SetValue(XScaleProperty, value); }
		}

		public static readonly DependencyProperty YScaleProperty = DependencyProperty.Register(
			"YScale", typeof(double), typeof(LivePlotter), new FrameworkPropertyMetadata(2d / 3d, FrameworkPropertyMetadataOptions.AffectsRender));

		public double YScale
		{
			get { return (double)GetValue(YScaleProperty); }
			set { SetValue(YScaleProperty, value); }
		}

		public static readonly DependencyProperty GraphPenProperty = DependencyProperty.Register(
			"GraphPen", typeof(Pen), typeof(LivePlotter), new FrameworkPropertyMetadata(new Pen(new SolidColorBrush(Color.FromRgb(139, 35, 35)), 1), FrameworkPropertyMetadataOptions.AffectsRender));

		public Pen GraphPen
		{
			get { return (Pen)GetValue(GraphPenProperty); }
			set { SetValue(GraphPenProperty, value); }
		}

		private IDisposable bufferSubscription = Disposable.Empty;

		protected IEnumerable<Point> lastBuffer;

		static LivePlotter()
		{
			ClipToBoundsProperty.OverrideMetadata(typeof(LivePlotter),
				new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));
		}

		private void SubscribeToNewBuffer(IBufferObservable bufToSubscribe, bool sample)
		{
			if (bufferSubscription != null)
				bufferSubscription.Dispose();

			if (bufToSubscribe == null) return;

			var sampled = sample ? bufToSubscribe.Sample(SampleInterval).Select(wnd => wnd.ToArray()) : bufToSubscribe;
			bufferSubscription = sampled.ObserveOnDispatcher().Subscribe(buf =>
			{
				lastBuffer = buf;
				InvalidateVisual();
			});
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			//optimization - frozen pen is faster
			var pen = GraphPen.Clone();
			pen.Freeze();

			if (lastBuffer == null) return;
			var chart = GetChartGeometry();
			drawingContext.DrawGeometry(null, pen, chart);
		}

		protected StreamGeometry GetChartGeometry()
		{
			double minX = lastBuffer.First().X;
			var slidingWindow = lastBuffer.Select(p => new Point(p.X - minX, p.Y)).Where(p => p.X >= TimeOffset && p.X <= TimeOffset + TotalTime);
			double minXWindow = slidingWindow.First().X;
			double minYWindow = slidingWindow.Min(p => p.Y);
			var geometry = GetGeometry(slidingWindow.Select(p => new Point(p.X - minXWindow, p.Y - minYWindow)));

			//apply transformation (scale and tanslation)
			var xScale = XScale * ActualWidth / TotalTime;
			var yScale = -YScale * ActualHeight / (slidingWindow.Max(p => p.Y) - minYWindow);
			var scaleTransform = new ScaleTransform(xScale, yScale);

			var translation = new TranslateTransform(0d, (YScale + (1 - YScale) / 2) * ActualHeight);
			var transfrom = new TransformGroup();
			transfrom.Children.Add(scaleTransform);
			transfrom.Children.Add(translation);
			transfrom.Freeze();

			geometry.Transform = transfrom;
			geometry.Freeze();

			return geometry;
		}

		StreamGeometry GetGeometry(IEnumerable<Point> data)
		{
			var geometry = new StreamGeometry();

			using (var ctx = geometry.Open())
			{
				bool figureOpen = false;

				foreach (var p in data)
				{
					if (figureOpen)
					{
						ctx.LineTo(p, true, false);
					}
					else
					{
						ctx.BeginFigure(p, false, false);
						figureOpen = true;
					}
				}
			}

			return geometry;
		}
	}
}
