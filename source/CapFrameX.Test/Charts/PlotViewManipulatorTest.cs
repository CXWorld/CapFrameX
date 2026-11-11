using CapFrameX.View.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;

namespace CapFrameX.Test.Charts
{
    /// <summary>
    /// Drives the wheel zoom and right-drag pan of PlotViewCustomZoom against rendered models whose
    /// axes carry the limits of the charts: AbsoluteMinimum = 0 on every value axis and
    /// AbsoluteMaximum = 101 on the frame time distribution. OxyPlot enforces those limits itself,
    /// so these tests catch a package update that changes how.
    /// </summary>
    [TestClass]
    public class PlotViewManipulatorTest
    {
        // One wheel notch: Delta 120 * 0.001, as PlotViewCustomZoom computes it.
        private const double WheelNotch = 0.12;
        private const double Tolerance = 1e-6;

        [TestMethod]
        public void WheelZoomIn_KeepsTheValueUnderTheCursor()
        {
            var view = new TestPlotView(CreateModel(yMaximum: 40));
            var cursor = new ScreenPoint(view.XAxis.Transform(60), view.YAxis.Transform(20));

            Wheel(view, EAxisDescription.XY, cursor, notches: 3);

            Assert.AreEqual(60, view.XAxis.InverseTransform(cursor.X), 1e-3);
            Assert.AreEqual(20, view.YAxis.InverseTransform(cursor.Y), 1e-3);
            Assert.IsTrue(Range(view.XAxis) < 120);
            Assert.IsTrue(Range(view.YAxis) < 40);
        }

        [TestMethod]
        public void WheelZoomOut_StopsAtTheAbsoluteLimits()
        {
            var view = new TestPlotView(CreateModel(yMaximum: 20, yAbsoluteMaximum: 101));

            Wheel(view, EAxisDescription.XY, view.ActualModel.PlotArea.Center, notches: -30);

            Assert.AreEqual(0, view.XAxis.ActualMinimum, Tolerance);
            Assert.IsTrue(view.XAxis.ActualMaximum > 120);
            Assert.AreEqual(0, view.YAxis.ActualMinimum, Tolerance);
            Assert.AreEqual(101, view.YAxis.ActualMaximum, Tolerance);
        }

        [TestMethod]
        public void CtrlWheel_ZoomsOnlyTheValueAxis()
        {
            var view = new TestPlotView(CreateModel(yMaximum: 40));

            Wheel(view, EAxisDescription.Y, view.ActualModel.PlotArea.Center, notches: 3);

            AssertRange(view.XAxis, 0, 120);
            Assert.IsTrue(Range(view.YAxis) < 40);
        }

        [TestMethod]
        public void ShiftWheel_ZoomsOnlyTheTimeAxis()
        {
            var view = new TestPlotView(CreateModel(yMaximum: 40));

            Wheel(view, EAxisDescription.X, view.ActualModel.PlotArea.Center, notches: 3);

            Assert.IsTrue(Range(view.XAxis) < 120);
            AssertRange(view.YAxis, 0, 40);
        }

        [TestMethod]
        public void Pan_PastTheAbsoluteMinimum_StopsThereAndKeepsTheZoomedRange()
        {
            var view = new TestPlotView(CreateModel(yMaximum: 40));
            var center = view.ActualModel.PlotArea.Center;
            Wheel(view, EAxisDescription.XY, center, notches: 3);
            var xRange = Range(view.XAxis);
            var yRange = Range(view.YAxis);

            // Dragging right and up moves the view towards lower values.
            Drag(view, EAxisDescription.XY, center, new ScreenPoint(center.X + 5000, center.Y - 5000));

            AssertRange(view.XAxis, 0, xRange);
            AssertRange(view.YAxis, 0, yRange);
        }

        [TestMethod]
        public void Pan_PastTheAbsoluteMaximum_StopsThereAndKeepsTheZoomedRange()
        {
            var view = new TestPlotView(CreateModel(yMaximum: 20, yAbsoluteMaximum: 101));
            var center = view.ActualModel.PlotArea.Center;
            var yRange = Range(view.YAxis);

            Drag(view, EAxisDescription.Y, center, new ScreenPoint(center.X, center.Y + 5000));

            AssertRange(view.XAxis, 0, 120);
            AssertRange(view.YAxis, 101 - yRange, 101);
        }

        [TestMethod]
        public void ResetAllAxes_RestoresTheConfiguredRange()
        {
            var view = new TestPlotView(CreateModel(yMaximum: 20, yAbsoluteMaximum: 101));
            var center = view.ActualModel.PlotArea.Center;
            Wheel(view, EAxisDescription.XY, center, notches: 4);
            Drag(view, EAxisDescription.XY, center, new ScreenPoint(center.X - 200, center.Y + 100));

            // What a double click does.
            view.ActualModel.ResetAllAxes();
            view.InvalidatePlot(false);

            AssertRange(view.XAxis, 0, 120);
            AssertRange(view.YAxis, 0, 20);
        }

        private static PlotModel CreateModel(double yMaximum, double yAbsoluteMaximum = double.MaxValue)
        {
            var model = new PlotModel();
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Minimum = 0,
                Maximum = 120,
                AbsoluteMinimum = 0
            });
            model.Axes.Add(new LinearAxis
            {
                Position = AxisPosition.Left,
                Minimum = 0,
                Maximum = yMaximum,
                AbsoluteMinimum = 0,
                AbsoluteMaximum = yAbsoluteMaximum
            });

            var series = new LineSeries();
            for (var i = 0; i <= 120; i++)
            {
                series.Points.Add(new DataPoint(i, 5 + i % 10));
            }

            model.Series.Add(series);
            return model;
        }

        private static void Wheel(TestPlotView view, EAxisDescription axes, ScreenPoint position, int notches)
        {
            for (var i = 0; i < Math.Abs(notches); i++)
            {
                new CustomZoomStepManipulator(view, axes) { Step = Math.Sign(notches) * WheelNotch }
                    .Started(new OxyMouseWheelEventArgs { Position = position });
            }
        }

        private static void Drag(TestPlotView view, EAxisDescription axes, ScreenPoint from, ScreenPoint to)
        {
            var pan = new CustomPanManipulator(view, axes);
            pan.Started(new OxyMouseEventArgs { Position = from });
            pan.Delta(new OxyMouseEventArgs { Position = to });
            pan.Completed(new OxyMouseEventArgs { Position = to });
        }

        private static double Range(Axis axis) => axis.ActualMaximum - axis.ActualMinimum;

        private static void AssertRange(Axis axis, double minimum, double maximum)
        {
            Assert.AreEqual(minimum, axis.ActualMinimum, Tolerance);
            Assert.AreEqual(maximum, axis.ActualMaximum, Tolerance);
        }

        /// <summary>
        /// Stands in for the WPF plot view: invalidating renders the model, which updates the
        /// axis transforms the manipulators read the mouse position through.
        /// </summary>
        private sealed class TestPlotView : IPlotView
        {
            public TestPlotView(PlotModel model)
            {
                ActualModel = model;
                InvalidatePlot();
            }

            public PlotModel ActualModel { get; }

            Model IView.ActualModel => ActualModel;

            public IController ActualController => null;

            public OxyRect ClientArea { get; } = new OxyRect(0, 0, 800, 400);

            public Axis XAxis => ActualModel.Axes[0];

            public Axis YAxis => ActualModel.Axes[1];

            public void InvalidatePlot(bool updateData = true)
                => SvgExporter.ExportToString(ActualModel, ClientArea.Width, ClientArea.Height, false);

            public void HideTracker() { }

            public void HideZoomRectangle() { }

            public void SetClipboardText(string text) { }

            public void SetCursorType(CursorType cursorType) { }

            public void ShowTracker(TrackerHitResult trackerHitResult) { }

            public void ShowZoomRectangle(OxyRect rectangle) { }
        }
    }
}
