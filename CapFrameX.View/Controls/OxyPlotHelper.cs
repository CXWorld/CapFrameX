using OxyPlot;
using OxyPlot.Wpf;

namespace CapFrameX.View.Controls
{
	public static class OxyPlotHelper
	{
		public static void SetYAxisZoomWheelAndPan(PlotView plotView)
		{
			if (plotView == null)
				return;

			plotView.Controller = new PlotController();

			var zoomWheel = new DelegatePlotCommand<OxyMouseWheelEventArgs>((view, controller, args) => HandleZoomByWheel(view, args));
			plotView.Controller.BindMouseWheel(OxyModifierKeys.None, zoomWheel);

			var customPanAt = new DelegatePlotCommand<OxyMouseDownEventArgs>((view, controller, args) => controller.AddMouseManipulator(view, new CustomPanManipulator(view), args));
			plotView.Controller.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, customPanAt);
		}

		private static void HandleZoomByWheel(IPlotView view, OxyMouseWheelEventArgs args, double factor = 1)
		{
			var m = new CustomZoomStepManipulator(view) { Step = args.Delta * 0.001 * factor, FineControl = args.IsControlDown };
			m.Started(args);
		}
	}
}
