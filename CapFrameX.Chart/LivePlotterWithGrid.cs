using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace CapFrameX.Chart
{
	public class LivePlotterWithGrid : LivePlotter
	{
		/// <summary>
		/// The amplitude minor tick property
		/// </summary>
		public static readonly DependencyProperty AmplitudeMinorTickProperty = DependencyProperty.Register(
			"AmplitudeMinorTick", typeof(double), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(0.2d, FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Gets or sets the amplitude minor tick.
		/// </summary>
		/// <value>
		/// The amplitude minor tick.
		/// </value>
		public double AmplitudeMinorTick
		{
			get { return (double)GetValue(AmplitudeMinorTickProperty); }
			set { SetValue(AmplitudeMinorTickProperty, value); }
		}

		/// <summary>
		/// The time minor tick property
		/// </summary>
		public static readonly DependencyProperty TimeMinorTickProperty = DependencyProperty.Register(
			"TimeMinorTick", typeof(double), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Gets or sets the time minor tick.
		/// </summary>
		/// <value>
		/// The time minor tick.
		/// </value>
		public double TimeMinorTick
		{
			get { return (double)GetValue(TimeMinorTickProperty); }
			set { SetValue(TimeMinorTickProperty, value); }
		}

		/// <summary>
		/// The amplitude tick multiplier property
		/// </summary>
		public static readonly DependencyProperty AmplitudeTickMultiplierProperty = DependencyProperty.Register(
			"AmplitudeTickMultiplier", typeof(int), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender));


		/// <summary>
		/// Gets or sets the amplitude tick multiplier.
		/// </summary>
		/// <value>
		/// The amplitude tick multiplier.
		/// </value>
		public int AmplitudeTickMultiplier
		{
			get { return (int)GetValue(AmplitudeTickMultiplierProperty); }
			set { SetValue(AmplitudeTickMultiplierProperty, value); }
		}


		/// <summary>
		/// The time tick multiplier property
		/// </summary>
		public static readonly DependencyProperty TimeTickMultiplierProperty = DependencyProperty.Register(
			"TimeTickMultiplier", typeof(int), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender));


		/// <summary>
		/// Gets or sets the time tick multiplier.
		/// </summary>
		/// <value>
		/// The time tick multiplier.
		/// </value>
		public int TimeTickMultiplier
		{
			get { return (int)GetValue(TimeTickMultiplierProperty); }
			set { SetValue(TimeTickMultiplierProperty, value); }
		}

		/// <summary>
		/// The minor tick pen property
		/// </summary>
		public static readonly DependencyProperty MinorTickPenProperty = DependencyProperty.Register(
			"MinorTickPen", typeof(Pen), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(new Pen(Brushes.Gray, 0.2), FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Gets or sets the minor tick pen.
		/// </summary>
		/// <value>
		/// The minor tick pen.
		/// </value>
		public Pen MinorTickPen
		{
			get { return (Pen)GetValue(MinorTickPenProperty); }
			set { SetValue(MinorTickPenProperty, value); }
		}

		/// <summary>
		/// The major tick pen property
		/// </summary>
		public static readonly DependencyProperty MajorTickPenProperty = DependencyProperty.Register(
			"MajorTickPen", typeof(Pen), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(new Pen(Brushes.Gray, 0.8), FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Gets or sets the major tick pen.
		/// </summary>
		/// <value>
		/// The major tick pen.
		/// </value>
		public Pen MajorTickPen
		{
			get { return (Pen)GetValue(MajorTickPenProperty); }
			set { SetValue(MajorTickPenProperty, value); }
		}

		/// <summary>
		/// The enable grid property
		/// </summary>
		public static readonly DependencyProperty EnableGridProperty = DependencyProperty.Register(
			"EnableGrid", typeof(bool), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));


		/// <summary>
		/// Gets or sets a value indicating whether [enable grid].
		/// </summary>
		/// <value>
		///   <c>true</c> if [enable grid]; otherwise, <c>false</c>.
		/// </value>
		public bool EnableGrid
		{
			get { return (bool)GetValue(EnableGridProperty); }
			set { SetValue(EnableGridProperty, value); }
		}

		/// <summary>
		/// Nimmt beim Überschreiben in einer abgeleiteten Klasse an Renderingvorgängen teil, die durch das Layoutsystem gesteuert werden.Die Renderinganweisungen für dieses Element werden beim Aufrufen dieser Methode nicht direkt verwendet, sondern stattdessen für spätere asynchrone Layout- und Zeichnungsvorgänge beibehalten.
		/// </summary>
		/// <param name="drawingContext">Die Zeichnungsanweisungen für ein bestimmtes Element.Dieser Kontext wird für das Layoutsystem bereitgestellt.</param>
		protected override void OnRender(DrawingContext drawingContext)
		{
			if (lastBuffer != null && EnableGrid)
				DrawGrid(drawingContext);

			base.OnRender(drawingContext);
		}

		private void DrawGrid(DrawingContext drawingContext)
		{
			var minPen = MinorTickPen.Clone();
			minPen.Freeze();
			var mjPen = MajorTickPen.Clone();
			mjPen.Freeze();

			var timeTickMult = TimeTickMultiplier;
			var amplitudeTickMult = AmplitudeTickMultiplier;
			var actualWidth = ActualWidth;
			var actualHeight = ActualHeight;

			var timeTick = actualWidth / TotalTime * TimeMinorTick;
			var amplitudeTick = 1;//actualHeight / (lastBuffer.First().ChannelsCount + 1) / (RowAmplitude * 2) * AmplitudeMinorTick;

			var totalTimeTicks = actualWidth / timeTick;
			var totalAmplTicks = actualHeight / amplitudeTick;

			for (int t = 0; t <= totalTimeTicks; t++)
				drawingContext.DrawLine(t % timeTickMult == 0 ? mjPen : minPen, new Point(t * timeTick, 0), new Point(t * timeTick, actualHeight));

			for (int t = 0; t <= totalAmplTicks; t++)
				drawingContext.DrawLine(t % amplitudeTickMult == 0 ? mjPen : minPen, new Point(0, t * amplitudeTick), new Point(actualWidth, t * amplitudeTick));
		}
	}
}
