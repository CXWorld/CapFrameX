using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CapFrameX.Chart
{
	public class LineGrid : FrameworkElement
	{
		/// <summary>
		/// Dependency property minor ticks per major tick
		/// </summary>
		public static readonly DependencyProperty MinorTicksPerMajorTickProperty = DependencyProperty.Register(
			"MinorTicksPerMajorTick", typeof(int), typeof(LineGrid), new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Property minor ticks per major tick
		/// </summary>
		public int MinorTicksPerMajorTick
		{
			get { return (int)GetValue(MinorTicksPerMajorTickProperty); }
			set { SetValue(MinorTicksPerMajorTickProperty, value); }
		}

		/// <summary>
		/// Dependency property minor ticks
		/// </summary>
		public static readonly DependencyProperty MinorTicksProperty = DependencyProperty.Register(
			"MinorTicks", typeof(int), typeof(LineGrid), new FrameworkPropertyMetadata(24, FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Property minor ticks
		/// </summary>
		public int MinorTicks
		{
			get { return (int)GetValue(MinorTicksProperty); }
			set { SetValue(MinorTicksProperty, value); }
		}

		/// <summary>
		/// Dependency property major ticks offset in minor ticks
		/// </summary>
		public static readonly DependencyProperty MajorTickOffsetInMinorTicksProperty = DependencyProperty.Register(
			"MajorTickOffsetInMinorTicks", typeof(int), typeof(LineGrid), new PropertyMetadata(2));

		/// <summary>
		/// Property major ticks offset in minor ticks
		/// </summary>
		public int MajorTickOffsetInMinorTicks
		{
			get { return (int)GetValue(MajorTickOffsetInMinorTicksProperty); }
			set { SetValue(MajorTickOffsetInMinorTicksProperty, value); }
		}

		/// <summary>
		/// Dependency property minor tick pen
		/// </summary>
		public static readonly DependencyProperty MinorTickPenProperty = DependencyProperty.Register(
			"MinorTickPen", typeof(Pen), typeof(LineGrid), new FrameworkPropertyMetadata(new Pen(Brushes.DarkGray, 0.1), FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Property minor tick pen
		/// </summary>
		public Pen MinorTickPen
		{
			get { return (Pen)GetValue(MinorTickPenProperty); }
			set { SetValue(MinorTickPenProperty, value); }
		}

		/// <summary>
		/// Dependency property major tick pen
		/// </summary>
		public static readonly DependencyProperty MajorTickPenProperty = DependencyProperty.Register(
			"MajorTickPen", typeof(Pen), typeof(LineGrid), new FrameworkPropertyMetadata(new Pen(Brushes.DarkSlateGray, 0.1), FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Property major tick pen
		/// </summary>
		public Pen MajorTickPen
		{
			get { return (Pen)GetValue(MajorTickPenProperty); }
			set { SetValue(MajorTickPenProperty, value); }
		}

		/// <summary>
		/// Dependency property lines orientations
		/// </summary>
		public static readonly DependencyProperty LinesOrientationProperty = DependencyProperty.Register(
			"LinesOrientation", typeof(Orientation), typeof(LineGrid), new FrameworkPropertyMetadata(default(Orientation), FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Property lines orientations
		/// </summary>
		public Orientation LinesOrientation
		{
			get { return (Orientation)GetValue(LinesOrientationProperty); }
			set { SetValue(LinesOrientationProperty, value); }
		}

		/// <summary>
		/// Dependency property align to device pixels
		/// </summary>
		public static readonly DependencyProperty AlignToDevicePixelsProperty = DependencyProperty.Register(
			"AlignToDevicePixels", typeof(bool), typeof(LineGrid), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

		/// <summary>
		/// Property align to device pixels
		/// </summary>
		public bool AlignToDevicePixels
		{
			get { return (bool)GetValue(AlignToDevicePixelsProperty); }
			set { SetValue(AlignToDevicePixelsProperty, value); }
		}

		/// <summary>
		/// Called on rendering
		/// </summary>
		/// <param name="drawingContext">The drawing context.</param>
		protected override void OnRender(DrawingContext drawingContext)
		{
			//cache the properties to avoid multiple passes through the inefficient wpf property system
			var majorTickPen = MajorTickPen;
			var minorTickPen = MinorTickPen;
			var orientation = LinesOrientation;
			var minorTicksPerMajorTick = MinorTicksPerMajorTick;
			var majorTickOffsetInMinorTicks = MajorTickOffsetInMinorTicks;
			var minorTicks = MinorTicks;
			var alignToDevicePixels = AlignToDevicePixels;
			var width = ActualWidth;
			var height = ActualHeight;

			var halfMajorTickPenThickness = majorTickPen.Thickness / 2;
			var halfMinorTickPenThickness = minorTickPen.Thickness / 2;

			var rangeEnd = orientation == Orientation.Vertical ? width : height;
			var step = rangeEnd / minorTicks;

			var lineDefs = new List<Tuple<Pen, Point, Point>>();
			var guidelines = new GuidelineSet();


			for (var i = 0; i < minorTicks + 1; i++)
			{
				var isMajorTick = (i % minorTicksPerMajorTick) == majorTickOffsetInMinorTicks;

				var coordinate = step * i;
				var startPoint = GetPointInCurrentOrientation(orientation, coordinate, false, width, height);
				var endPoint = GetPointInCurrentOrientation(orientation, coordinate, true, width, height);

				lineDefs.Add(Tuple.Create(isMajorTick ? majorTickPen : minorTickPen, startPoint, endPoint));

				if (alignToDevicePixels)
					PushGuideline(guidelines, orientation, coordinate,
						isMajorTick ? halfMajorTickPenThickness : halfMinorTickPenThickness);
			}			

			if (alignToDevicePixels)
				drawingContext.PushGuidelineSet(guidelines);

			//FormattedText ft = new FormattedText("Test", CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface("Klavika"), 10, Brushes.Black);

			foreach (var line in lineDefs)
			{
				drawingContext.DrawLine(line.Item1, line.Item2, line.Item3);
				//drawingContext.DrawText(ft, line.Item2);
			}

			if (alignToDevicePixels)
				drawingContext.Pop();
		}

		private Point GetPointInCurrentOrientation(Orientation orientation, double coordinate, bool rearEnd, double width, double height)
		{
			if (orientation == Orientation.Horizontal)
				return new Point(rearEnd ? width : 0, coordinate);

			return new Point(coordinate, rearEnd ? height : 0);
		}

		private void PushGuideline(GuidelineSet guidelines, Orientation orientation, double coordinate, double halfPenThickness)
		{
			var coordinateWithPen = coordinate + halfPenThickness;
			if (orientation == Orientation.Horizontal)
				guidelines.GuidelinesY.Add(coordinateWithPen);
			else
				guidelines.GuidelinesX.Add(coordinateWithPen);
		}

		/// <summary>
		/// Called when render size changes
		/// </summary>
		/// <param name="sizeInfo">The size information.</param>
		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			InvalidateVisual();
		}
	}
}
