using OxyPlot;

namespace CapFrameX.View.Controls
{
	public class CustomZoomStepManipulator : MouseManipulator
	{
		public CustomZoomStepManipulator(IPlotView plotView)
			: base(plotView)
		{
		}

		public bool FineControl { get; set; }

		public double Step { get; set; }

		public override void Started(OxyMouseEventArgs e)
		{
			base.Started(e);

			var isZoomEnabled = (this.XAxis != null && this.XAxis.IsZoomEnabled)
								|| (this.YAxis != null && this.YAxis.IsZoomEnabled);

			if (!isZoomEnabled)
			{
				return;
			}

			var current = this.InverseTransform(e.Position.X, e.Position.Y);

			var scale = this.Step;
			if (this.FineControl)
			{
				scale *= 3;
			}

			scale = 1 + scale;

			// make sure the zoom factor is not negative
			if (scale < 0.1)
			{
				scale = 0.1;
			}

			//if (this.XAxis != null)
			//{
			//	this.XAxis.ZoomAt(scale, current.X);
			//}

			if (this.YAxis != null)
			{
				this.YAxis.ZoomAt(scale, current.Y);
			}

			this.PlotView.InvalidatePlot(false);
			e.Handled = true;
		}
	}
}
