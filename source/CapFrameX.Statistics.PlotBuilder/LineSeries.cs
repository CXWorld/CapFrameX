using OxyPlot;

namespace CapFrameX.Statistics.PlotBuilder
{
    public class LineSeries: OxyPlot.Series.LineSeries
	{
		public int LegendStrokeThickness { get; set; }

        public override void RenderLegend(IRenderContext rc, OxyRect legendBox)
        {
            double xmid = (legendBox.Left + legendBox.Right) / 2;
            double ymid = (legendBox.Top + legendBox.Bottom) / 2;
            var pts = new[] { new ScreenPoint(legendBox.Left, ymid), new ScreenPoint(legendBox.Right, ymid) };
            rc.DrawLine(
                pts,
                this.GetSelectableColor(this.ActualColor),
                this.LegendStrokeThickness,
                this.ActualDashArray);
            var midpt = new ScreenPoint(xmid, ymid);
            rc.DrawMarker(
                legendBox,
                midpt,
                this.MarkerType,
                this.MarkerOutline,
                this.MarkerSize,
                this.ActualMarkerFill,
                this.MarkerStroke,
                this.MarkerStrokeThickness);
        }
    }
}
