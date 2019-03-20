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
		/// <summary>
		/// Sample interval -> update rate of plot
		/// </summary>
		public static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(60);

		/// <summary>
		/// Dependency property buffer 
		/// </summary>
		public static readonly DependencyProperty BufferProperty = DependencyProperty.Register(
			"Buffer", typeof(IBufferObservable), typeof(LivePlotter), new PropertyMetadata(default(IBufferObservable), OnBufferPropertyChanged));

		// ReSharper disable InconsistentNaming
		private static void OnBufferPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		// ReSharper restore InconsistentNaming
		{
			var plotter = d as LivePlotter;
			var newValue = e.NewValue as IBufferObservable;

			if (plotter != null && newValue != null)
				plotter.SubscribeToNewBuffer(newValue, plotter.Sample);
		}

		/// <summary>
		/// Dependency property sample 
		/// </summary>
		public static readonly DependencyProperty SampleProperty = DependencyProperty.Register(
			"Sample", typeof(bool), typeof(LivePlotter), new PropertyMetadata(true, SampleChanged));

		private static void SampleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var plotter = d as LivePlotter;
			var newValue = (bool)e.NewValue;

			if (plotter != null)
				plotter.SubscribeToNewBuffer(plotter.Buffer, newValue);
		}

		/// <summary>
		/// Property sample 
		/// </summary>
		public bool Sample
		{
			get { return (bool)GetValue(SampleProperty); }
			set { SetValue(SampleProperty, value); }
		}

		/// <summary>
		/// Property buffer
		/// </summary>
		public IBufferObservable Buffer
		{
			get { return (IBufferObservable)GetValue(BufferProperty); }
			set { SetValue(BufferProperty, value); }
		}


		/// <summary>
		/// The total time property
		/// </summary>
		public static readonly DependencyProperty TotalPointCountProperty = DependencyProperty.Register(
			"TotalPointCount", typeof(int), typeof(LivePlotter), new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.AffectsRender));


		public int TotalPointCount
		{
			get { return (int)GetValue(TotalPointCountProperty); }
			set { SetValue(TotalPointCountProperty, value); }
		}

		/// <summary>
		/// Dependancy 
		/// </summary>
		public static readonly DependencyProperty TimeOffsetProperty = DependencyProperty.Register(
			"TimeOffset", typeof(double), typeof(LivePlotter), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));


		/// <summary>
		/// Gets or sets the time offset.
		/// </summary>
		/// <value>
		/// The time offset.
		/// </value>
		public double TimeOffset
		{
			get { return (double)GetValue(TimeOffsetProperty); }
			set { SetValue(TimeOffsetProperty, value); }
		}

		/// <summary>
		/// The cut line on negative time delta property
		/// </summary>
		public static readonly DependencyProperty CutLineOnNegativeTimeDeltaProperty = DependencyProperty.Register(
			"CutLineOnNegativeTimeDelta", typeof(bool), typeof(LivePlotter), new PropertyMetadata(true));


		/// <summary>
		/// Gets or sets a value indicating whether [cut line on negative time delta].
		/// </summary>
		/// <value>
		/// <c>true</c> if [cut line on negative time delta]; otherwise, <c>false</c>.
		/// </value>
		public bool CutLineOnNegativeTimeDelta
		{
			get { return (bool)GetValue(CutLineOnNegativeTimeDeltaProperty); }
			set { SetValue(CutLineOnNegativeTimeDeltaProperty, value); }
		}


		/// <summary>
		/// The total time property
		/// </summary>
		public static readonly DependencyProperty TotalTimeProperty = DependencyProperty.Register(
			"TotalTime", typeof(double), typeof(LivePlotter), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Gets or sets the total time.
		/// </summary>
		/// <value>
		/// The total time.
		/// </value>
		public double TotalTime
		{
			get { return (double)GetValue(TotalTimeProperty); }
			set { SetValue(TotalTimeProperty, value); }
		}

		/// <summary>
		/// The row amplitude property
		/// </summary>
		public static readonly DependencyProperty RowAmplitudeProperty = DependencyProperty.Register(
			"RowAmplitude", typeof(double), typeof(LivePlotter), new FrameworkPropertyMetadata(default(double), FrameworkPropertyMetadataOptions.AffectsRender));


		/// <summary>
		/// Gets or sets the row amplitude.
		/// </summary>
		/// <value>
		/// The row amplitude.
		/// </value>
		public double RowAmplitude
		{
			get { return (double)GetValue(RowAmplitudeProperty); }
			set { SetValue(RowAmplitudeProperty, value); }
		}

		public static readonly DependencyProperty XScaleProperty = DependencyProperty.Register(
			"XScale", typeof(double), typeof(LivePlotter), new FrameworkPropertyMetadata(default(double), FrameworkPropertyMetadataOptions.AffectsRender));

		public double XScale
		{
			get { return (double)GetValue(XScaleProperty); }
			set { SetValue(XScaleProperty, value); }
		}

		/// <summary>
		/// The graph pen property
		/// </summary>
		public static readonly DependencyProperty GraphPenProperty = DependencyProperty.Register(
			"GraphPen", typeof(Pen), typeof(LivePlotter), new FrameworkPropertyMetadata(new Pen(new SolidColorBrush(Color.FromRgb(139, 35, 35)), 1), FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Gets or sets the graph pen.
		/// </summary>
		/// <value>
		/// The graph pen.
		/// </value>
		public Pen GraphPen
		{
			get { return (Pen)GetValue(GraphPenProperty); }
			set { SetValue(GraphPenProperty, value); }
		}

		private IDisposable bufferSubscription = Disposable.Empty;

		/// <summary>
		/// The last buffer
		/// </summary>
		protected IEnumerable<Point> lastBuffer;

		/// <summary>
		/// Initializes the <see cref="LivePlotter"/> class.
		/// </summary>
		static LivePlotter()
		{
			//Set default ClipToBounds=true
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

		/// <summary>
		/// Nimmt beim Überschreiben in einer abgeleiteten Klasse an Renderingvorgängen teil, die durch das Layoutsystem gesteuert werden.Die Renderinganweisungen für dieses Element werden beim Aufrufen dieser Methode nicht direkt verwendet, sondern stattdessen für spätere asynchrone Layout- und Zeichnungsvorgänge beibehalten.
		/// </summary>
		/// <param name="drawingContext">Die Zeichnungsanweisungen für ein bestimmtes Element.Dieser Kontext wird für das Layoutsystem bereitgestellt.</param>
		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			var pen = GraphPen.Clone(); //optimization - frozen pen is faster
			pen.Freeze();

			if (lastBuffer == null) return;
			var chart = GetChartGeometry();
			drawingContext.DrawGeometry(null, pen, chart);
		}

		/// <summary>
		/// Gets the chart geometry.
		/// </summary>
		/// <returns></returns>
		protected StreamGeometry GetChartGeometry()
		{
			double minX = lastBuffer.Min(p => p.X);
			var slidingWindow = lastBuffer.Select(p => new Point(p.X - minX, p.Y)).Where(p => p.X >= TimeOffset && p.X <= TimeOffset + TotalTime);
			double minXWindow = slidingWindow.Min(p => p.X);
			var geometry = GetGeometry(slidingWindow.Select(p => new Point(p.X - minXWindow, p.Y)));

			//apply placement transforms
			var xScale = ActualWidth / TotalTime; //XScale;
			var yScale = -ActualHeight/(lastBuffer.Max(p => p.Y) - lastBuffer.Min(p => p.Y)); /// RowAmplitude; //inverting, coz y-axis is top-down in WPF but bottom-up in chart
			//var yStep = 2*ActualHeight;

			var scaleTransform = new ScaleTransform(xScale, yScale);
			//double row = 0.5;

			var translation = new TranslateTransform(0d, ActualHeight);
			var transfrom = new TransformGroup();
			transfrom.Children.Add(scaleTransform);
			transfrom.Children.Add(translation);
			transfrom.Freeze();

			geometry.Transform = transfrom;
			geometry.Freeze();

			return geometry;
		}

		/// <summary>
		/// Geometries from channel.
		/// </summary>
		/// <param name="data">The data.</param>
		/// <returns></returns>
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
