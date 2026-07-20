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
            // OxyPlot passes transformed screen coordinates to the decimator.
            // Keep the raw data intact for zooming, tracking and exporting.
            Decimator = DecimateScreenPoints;
            MinimumSegmentLength = 0;
        }

        public static void DecimateScreenPoints(List<ScreenPoint> input, List<ScreenPoint> output)
        {
            if (input == null || output == null || input.Count == 0)
                return;

            int runStart = 0;
            while (runStart < input.Count)
            {
                if (!IsFinite(input[runStart]))
                {
                    AddDistinct(output, input[runStart]);
                    runStart++;
                    continue;
                }

                int runEnd = runStart + 1;
                while (runEnd < input.Count && IsFinite(input[runEnd]))
                    runEnd++;

                DecimateFiniteRun(input, output, runStart, runEnd);
                runStart = runEnd;
            }
        }

        private static void DecimateFiniteRun(IList<ScreenPoint> input, IList<ScreenPoint> output,
            int runStart, int runEnd)
        {
            int count = runEnd - runStart;
            if (count <= 2)
            {
                for (int i = runStart; i < runEnd; i++)
                    AddDistinct(output, input[i]);
                return;
            }

            AddDistinct(output, input[runStart]);
            int index = runStart + 1;
            int lastIndex = runEnd - 1;

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
            if (x <= int.MinValue)
                return int.MinValue;
            if (x >= int.MaxValue)
                return int.MaxValue;

            return (int)Math.Floor(x);
        }

        private static bool IsFinite(ScreenPoint point)
            => !double.IsNaN(point.X) && !double.IsInfinity(point.X)
                && !double.IsNaN(point.Y) && !double.IsInfinity(point.Y);

        private static void AddDistinct(IList<ScreenPoint> output, ScreenPoint point)
        {
            if (output.Count == 0)
            {
                output.Add(point);
                return;
            }

            ScreenPoint previous = output[output.Count - 1];
            if ((!IsFinite(previous) && !IsFinite(point))
                || (previous.X == point.X && previous.Y == point.Y))
            {
                return;
            }

            output.Add(point);
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
