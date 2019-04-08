using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CapFrameX.Chart
{
	public class LineGrid : FrameworkElement
	{
		public static readonly DependencyProperty TicksProperty = DependencyProperty.Register(
			"Ticks", typeof(int), typeof(LineGrid), new FrameworkPropertyMetadata(8, FrameworkPropertyMetadataOptions.AffectsRender));

		public int Ticks
		{
			get { return (int)GetValue(TicksProperty); }
			set { SetValue(TicksProperty, value); }
		}

		public static readonly DependencyProperty TickPenProperty = DependencyProperty.Register(
			"TickPen", typeof(Pen), typeof(LineGrid), new FrameworkPropertyMetadata(new Pen(Brushes.DarkGray, 0.1), FrameworkPropertyMetadataOptions.AffectsRender));

		public Pen TickPen
		{
			get { return (Pen)GetValue(TickPenProperty); }
			set { SetValue(TickPenProperty, value); }
		}

		public static readonly DependencyProperty LinesOrientationProperty = DependencyProperty.Register(
			"LinesOrientation", typeof(Orientation), typeof(LineGrid), new FrameworkPropertyMetadata(default(Orientation), FrameworkPropertyMetadataOptions.AffectsRender));

		public Orientation LinesOrientation
		{
			get { return (Orientation)GetValue(LinesOrientationProperty); }
			set { SetValue(LinesOrientationProperty, value); }
		}

		public static readonly DependencyProperty AlignToDevicePixelsProperty = DependencyProperty.Register(
			"AlignToDevicePixels", typeof(bool), typeof(LineGrid), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

		public bool AlignToDevicePixels
		{
			get { return (bool)GetValue(AlignToDevicePixelsProperty); }
			set { SetValue(AlignToDevicePixelsProperty, value); }
		}

		protected override void OnRender(DrawingContext drawingContext)
		{
			//cache the properties to avoid multiple passes through the inefficient wpf property system
			var tickPen = TickPen;
			var orientation = LinesOrientation;
			var ticks = Ticks;
			var alignToDevicePixels = AlignToDevicePixels;
			var width = ActualWidth;
			var height = ActualHeight;

			var rangeEnd = orientation == Orientation.Vertical ? width : height;
			var step = rangeEnd / ticks;

			var lineDefs = new List<Tuple<Pen, Point, Point>>();
			var guidelines = new GuidelineSet();

			for (var i = 0; i < ticks + 1; i++)
			{
				var coordinate = step * i;
				var startPoint = GetPointInCurrentOrientation(orientation, coordinate, false, width, height);
				var endPoint = GetPointInCurrentOrientation(orientation, coordinate, true, width, height);

				lineDefs.Add(Tuple.Create(tickPen, startPoint, endPoint));

				if (alignToDevicePixels)
					PushGuideline(guidelines, orientation, coordinate, tickPen.Thickness);
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

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
			InvalidateVisual();
		}
	}
}
