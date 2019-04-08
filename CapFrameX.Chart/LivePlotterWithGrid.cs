using System.Windows;
using System.Windows.Media;

namespace CapFrameX.Chart
{
	public class LivePlotterWithGrid : LivePlotter
	{
		public static readonly DependencyProperty AmplitudeMinorTickProperty = DependencyProperty.Register(
			"AmplitudeMinorTick", typeof(double), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(0.2d, FrameworkPropertyMetadataOptions.AffectsRender));

		public double AmplitudeMinorTick
		{
			get { return (double)GetValue(AmplitudeMinorTickProperty); }
			set { SetValue(AmplitudeMinorTickProperty, value); }
		}

		public static readonly DependencyProperty TimeMinorTickProperty = DependencyProperty.Register(
			"TimeMinorTick", typeof(double), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(100d, FrameworkPropertyMetadataOptions.AffectsRender));

		public double TimeMinorTick
		{
			get { return (double)GetValue(TimeMinorTickProperty); }
			set { SetValue(TimeMinorTickProperty, value); }
		}

		public static readonly DependencyProperty AmplitudeTickMultiplierProperty = DependencyProperty.Register(
			"AmplitudeTickMultiplier", typeof(int), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender));

		public int AmplitudeTickMultiplier
		{
			get { return (int)GetValue(AmplitudeTickMultiplierProperty); }
			set { SetValue(AmplitudeTickMultiplierProperty, value); }
		}

		public static readonly DependencyProperty TimeTickMultiplierProperty = DependencyProperty.Register(
			"TimeTickMultiplier", typeof(int), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(5, FrameworkPropertyMetadataOptions.AffectsRender));


		public int TimeTickMultiplier
		{
			get { return (int)GetValue(TimeTickMultiplierProperty); }
			set { SetValue(TimeTickMultiplierProperty, value); }
		}

		public static readonly DependencyProperty MinorTickPenProperty = DependencyProperty.Register(
			"MinorTickPen", typeof(Pen), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(new Pen(Brushes.Gray, 0.2), FrameworkPropertyMetadataOptions.AffectsRender));

		public Pen MinorTickPen
		{
			get { return (Pen)GetValue(MinorTickPenProperty); }
			set { SetValue(MinorTickPenProperty, value); }
		}

		public static readonly DependencyProperty MajorTickPenProperty = DependencyProperty.Register(
			"MajorTickPen", typeof(Pen), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(new Pen(Brushes.Gray, 0.8), FrameworkPropertyMetadataOptions.AffectsRender));

		public Pen MajorTickPen
		{
			get { return (Pen)GetValue(MajorTickPenProperty); }
			set { SetValue(MajorTickPenProperty, value); }
		}

		public static readonly DependencyProperty EnableGridProperty = DependencyProperty.Register(
			"EnableGrid", typeof(bool), typeof(LivePlotterWithGrid), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

		public bool EnableGrid
		{
			get { return (bool)GetValue(EnableGridProperty); }
			set { SetValue(EnableGridProperty, value); }
		}

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
			var amplitudeTick = 1;

			var totalTimeTicks = actualWidth / timeTick;
			var totalAmplTicks = actualHeight / amplitudeTick;

			for (int t = 0; t <= totalTimeTicks; t++)
				drawingContext.DrawLine(t % timeTickMult == 0 ? mjPen : minPen, new Point(t * timeTick, 0), new Point(t * timeTick, actualHeight));

			for (int t = 0; t <= totalAmplTicks; t++)
				drawingContext.DrawLine(t % amplitudeTickMult == 0 ? mjPen : minPen, new Point(0, t * amplitudeTick), new Point(actualWidth, t * amplitudeTick));
		}
	}
}
