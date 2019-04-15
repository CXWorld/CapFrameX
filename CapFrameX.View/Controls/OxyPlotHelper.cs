using OxyPlot;
using OxyPlot.Wpf;

namespace CapFrameX.View.Controls
{
	public static class OxyPlotHelper
	{
		public static void SetYAxisZoomer(PlotView plotView)
		{
			if (plotView == null)
				return;

			var zoomer = new DelegatePlotCommand<OxyMouseWheelEventArgs>((view, controller, args) =>
			{
				view.ActualModel.GetAxesFromPoint(args.Position, out OxyPlot.Axes.Axis xAxis, out OxyPlot.Axes.Axis yAxis);

				//if zoom is disabled, return
				var isZoomEnabled = ((xAxis != null && xAxis.IsZoomEnabled) || (yAxis != null && yAxis.IsZoomEnabled));
				if (!isZoomEnabled) return;

				var current = InverseTransform(yAxis, xAxis, args.Position.X, args.Position.Y);
				var scale = args.Delta * 0.001;
				if (args.IsControlDown) scale *= 3;
				scale = 1 + scale;

				// make sure the zoom factor is not negative
				if (scale < 0.1) scale = 0.1;

				//always set xAxis to null if both y ans x axis are present
				if (xAxis != null && yAxis != null) xAxis = null;

				xAxis?.ZoomAt(scale, current.X);
				yAxis?.ZoomAt(scale, current.Y);

				plotView.Model.InvalidatePlot(false);
			});

			plotView.Controller = new PlotController();
			plotView.Controller.BindMouseWheel(OxyModifierKeys.None, zoomer);
			//plotView.Controller.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, OxyPlot.PlotCommands.PanAt);
		}

		private static DataPoint InverseTransform(OxyPlot.Axes.Axis yAxis, OxyPlot.Axes.Axis xAxis, double x, double y)
		{
			if (xAxis != null)
			{
				return xAxis.InverseTransform(x, y, yAxis);
			}

			if (yAxis != null)
			{
				return new DataPoint(0, yAxis.InverseTransform(y));
			}

			return new DataPoint();
		}
	}
}
