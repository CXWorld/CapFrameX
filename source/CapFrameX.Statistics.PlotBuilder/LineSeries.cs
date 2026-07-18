using OxyPlot;

using System;
using System.Collections.Generic;

namespace CapFrameX.Statistics.PlotBuilder
{
    public class LineSeries: OxyPlot.Series.LineSeries
	{
		public int LegendStrokeThickness { get; set; }

        public LineSeries()
        {
            // Decimate after transformation to screen space. Raw samples remain in
            // the series so zooming and the tracker retain their full precision.
            Decimator = DecimateScreenPoints;
            MinimumSegmentLength = 0;
        }

        public static void DecimateScreenPoints(List<ScreenPoint> input, List<ScreenPoint> output)
        {
            if (input == null || input.Count == 0)
                return;

            if (input.Count <= 2)
            {
                output.AddRange(input);
                return;
            }

            AddDistinct(output, input[0]);
            int index = 1;
            int lastIndex = input.Count - 1;

            while (index < lastIndex)
            {
                int pixelColumn = GetPixelColumn(input[index].X);
                int minimumIndex = index;
                int maximumIndex = index;
                int groupEnd = index + 1;

                while (groupEnd < lastIndex && GetPixelColumn(input[groupEnd].X) == pixelColumn)
                {
                    if (input[groupEnd].Y < input[minimumIndex].Y)
                        minimumIndex = groupEnd;

                    if (input[groupEnd].Y > input[maximumIndex].Y)
                        maximumIndex = groupEnd;

                    groupEnd++;
                }

                if (minimumIndex <= maximumIndex)
                {
                    AddDistinct(output, input[minimumIndex]);
                    AddDistinct(output, input[maximumIndex]);
                }
                else
                {
                    AddDistinct(output, input[maximumIndex]);
                    AddDistinct(output, input[minimumIndex]);
                }

                index = groupEnd;
            }

            AddDistinct(output, input[lastIndex]);
        }

        private static int GetPixelColumn(double x)
        {
            if (double.IsNaN(x))
                return int.MinValue;
            if (x <= int.MinValue)
                return int.MinValue;
            if (x >= int.MaxValue)
                return int.MaxValue;

            return (int)Math.Floor(x);
        }

        private static void AddDistinct(IList<ScreenPoint> output, ScreenPoint point)
        {
            if (output.Count == 0
                || output[output.Count - 1].X != point.X
                || output[output.Count - 1].Y != point.Y)
            {
                output.Add(point);
            }
        }

        public override void RenderLegend(IRenderContext rc, OxyRect legendBox)
        {
            double xmid = (legendBox.Left + legendBox.Right) / 2;
            double ymid = (legendBox.Top + legendBox.Bottom) / 2;
            var pts = new[] { new ScreenPoint(legendBox.Left, ymid), new ScreenPoint(legendBox.Right, ymid) };
            rc.DrawLine(
                pts,
                this.GetSelectableColor(this.ActualColor),
                this.LegendStrokeThickness,
                this.EdgeRenderingMode,
                this.ActualDashArray);

            var midpt = new ScreenPoint(xmid, ymid);

            rc.DrawMarker(
                midpt,
                this.MarkerType,
                this.MarkerOutline,
                this.MarkerSize,
                this.ActualMarkerFill,
                this.MarkerStroke,
                this.MarkerStrokeThickness,
                this.EdgeRenderingMode);
        }
    }
}
