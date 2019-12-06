using System;
using OxyPlot;
using OxyPlot.Wpf;

namespace CapFrameX.View.Controls
{
	public static class OxyPlotHelper
	{
		public static void SetAxisZoomWheelAndPan(PlotView plotView)
		{
			if (plotView == null)
				return;

			plotView.Controller = new PlotController();

			var zoomWheel = new DelegatePlotCommand<OxyMouseWheelEventArgs>((view, controller, args) => HandleZoomByWheel(view, args));
			plotView.Controller.BindMouseWheel(OxyModifierKeys.None, zoomWheel);

			var zoomWheelCtrl = new DelegatePlotCommand<OxyMouseWheelEventArgs>((view, controller, args) => HandleZoomByWheelAndCtrl(view, args));
			plotView.Controller.BindMouseWheel(OxyModifierKeys.Control, zoomWheelCtrl);

			var zoomWheelShift = new DelegatePlotCommand<OxyMouseWheelEventArgs>((view, controller, args) => HandleZoomByWheelAndShift(view, args));
			plotView.Controller.BindMouseWheel(OxyModifierKeys.Shift, zoomWheelShift);

			var customPanAt = new DelegatePlotCommand<OxyMouseDownEventArgs>((view, controller, args) => controller.AddMouseManipulator(view, new CustomPanManipulator(view, EAxisDescription.XY), args));
			plotView.Controller.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.None, customPanAt);

			var customPanAtCtrl = new DelegatePlotCommand<OxyMouseDownEventArgs>((view, controller, args) => controller.AddMouseManipulator(view, new CustomPanManipulator(view, EAxisDescription.Y), args));
			plotView.Controller.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.Control, customPanAtCtrl);

			var customPanAtShift = new DelegatePlotCommand<OxyMouseDownEventArgs>((view, controller, args) => controller.AddMouseManipulator(view, new CustomPanManipulator(view, EAxisDescription.X), args));
			plotView.Controller.BindMouseDown(OxyMouseButton.Right, OxyModifierKeys.Shift, customPanAtShift);
		}

		private static void HandleZoomByWheel(IPlotView view, OxyMouseWheelEventArgs args, double factor = 1)
		{
			var m = new CustomZoomStepManipulator(view, EAxisDescription.XY) { Step = args.Delta * 0.001 * factor};
			m.Started(args);
		}

		private static void HandleZoomByWheelAndShift(IPlotView view, OxyMouseWheelEventArgs args, double factor = 1)
		{
			var m = new CustomZoomStepManipulator(view, EAxisDescription.X) { Step = args.Delta * 0.001 * factor };
			m.Started(args);
		}

		private static void HandleZoomByWheelAndCtrl(IPlotView view, OxyMouseWheelEventArgs args, double factor = 1)
		{
			var m = new CustomZoomStepManipulator(view, EAxisDescription.Y) { Step = args.Delta * 0.001 * factor };
			m.Started(args);
		}
	}
}
