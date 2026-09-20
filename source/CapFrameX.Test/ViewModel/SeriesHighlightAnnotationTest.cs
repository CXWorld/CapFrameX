using CapFrameX.Statistics.PlotBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.ViewModel
{
    [TestClass]
    public class SeriesHighlightAnnotationTest
    {
        /// <summary>
        /// Records the stroke color of every drawn line so the render order of the series can be
        /// observed. Everything else is a no-op.
        /// </summary>
        private sealed class RecordingRenderContext : RenderContextBase
        {
            private int _clipCount;

            public List<OxyColor> StrokeColors { get; } = new List<OxyColor>();

            public override int ClipCount => _clipCount;

            public override void DrawLine(IList<ScreenPoint> points, OxyColor stroke,
                double thickness, EdgeRenderingMode edgeRenderingMode, double[] dashArray,
                LineJoin lineJoin)
            {
                StrokeColors.Add(stroke);
            }

            public override void DrawPolygon(IList<ScreenPoint> points, OxyColor fill,
                OxyColor stroke, double thickness, EdgeRenderingMode edgeRenderingMode,
                double[] dashArray, LineJoin lineJoin)
            {
            }

            public override void DrawText(ScreenPoint p, string text, OxyColor fill,
                string fontFamily, double fontSize, double fontWeight, double rotate,
                HorizontalAlignment halign, VerticalAlignment valign, OxySize? maxSize)
            {
            }

            public override OxySize MeasureText(string text, string fontFamily, double fontSize,
                double fontWeight)
            {
                return new OxySize(text?.Length * 5 ?? 0, fontSize);
            }

            public override void PushClip(OxyRect clippingRectangle) => _clipCount++;

            public override void PopClip() => _clipCount--;
        }

        private static PlotModel CreateModel(params OxyColor[] seriesColors)
        {
            var model = new PlotModel();
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom });
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left });

            foreach (var color in seriesColors)
            {
                var series = new LineSeries { Color = color, StrokeThickness = 1.5 };
                series.Points.Add(new DataPoint(0, 0));
                series.Points.Add(new DataPoint(1, 1));
                series.Points.Add(new DataPoint(2, 0.5));
                model.Series.Add(series);
            }

            return model;
        }

        private static List<OxyColor> RenderAndRecordSeriesStrokes(PlotModel model,
            params OxyColor[] seriesColors)
        {
            var rc = new RecordingRenderContext();
            ((IPlotModel)model).Update(true);
            ((IPlotModel)model).Render(rc, new OxyRect(0, 0, 800, 600));

            // Axes and gridlines draw lines too - only the series colors are of interest.
            return rc.StrokeColors.Where(color => seriesColors.Contains(color)).ToList();
        }

        [TestMethod]
        public void Highlight_RendersAfterEverySeries_WithoutReorderingTheCollection()
        {
            var first = OxyColors.Red;
            var second = OxyColors.Blue;
            var model = CreateModel(first, second);
            var firstSeries = model.Series[0];

            SeriesHighlightAnnotation.SetHighlight(model, firstSeries);
            var strokes = RenderAndRecordSeriesStrokes(model, first, second);

            // The first series is drawn at its own position and once more on top.
            Assert.AreEqual(first, strokes.First(), "The series keep their own draw order.");
            Assert.AreEqual(first, strokes.Last(), "The highlighted series must be drawn last.");
            Assert.IsTrue(strokes.IndexOf(second) < strokes.LastIndexOf(first),
                "The highlight has to come after the series it is drawn over.");

            // The legend order follows the series collection, which must be untouched.
            Assert.AreSame(firstSeries, model.Series[0]);
            Assert.AreEqual(2, model.Series.Count);
        }

        [TestMethod]
        public void SetHighlight_Null_RemovesTheOverlay()
        {
            var first = OxyColors.Red;
            var second = OxyColors.Blue;
            var model = CreateModel(first, second);

            SeriesHighlightAnnotation.SetHighlight(model, model.Series[0]);
            SeriesHighlightAnnotation.SetHighlight(model, null);

            Assert.AreEqual(0, model.Annotations.Count);
            var strokes = RenderAndRecordSeriesStrokes(model, first, second);
            Assert.AreEqual(second, strokes.Last(), "Without a highlight the plain order remains.");
        }

        [TestMethod]
        public void SetHighlight_RepeatedCalls_KeepASingleOverlay()
        {
            var model = CreateModel(OxyColors.Red, OxyColors.Blue);

            SeriesHighlightAnnotation.SetHighlight(model, model.Series[0]);
            SeriesHighlightAnnotation.SetHighlight(model, model.Series[1]);
            SeriesHighlightAnnotation.SetHighlight(model, model.Series[0]);

            Assert.AreEqual(1, model.Annotations.Count);
            Assert.AreSame(model.Series[0],
                ((SeriesHighlightAnnotation)model.Annotations[0]).Series);
        }

        [TestMethod]
        public void Highlight_SeriesNoLongerInModel_DrawsNothing()
        {
            var stale = OxyColors.Green;
            var staleModel = CreateModel(stale);
            var staleSeries = staleModel.Series[0];

            var model = CreateModel(OxyColors.Red);
            model.Annotations.Add(new SeriesHighlightAnnotation { Series = staleSeries });

            // A chart rebuild replaces the series; the overlay must not keep drawing the old one.
            var strokes = RenderAndRecordSeriesStrokes(model, OxyColors.Red, stale);

            Assert.IsFalse(strokes.Contains(stale));
            Assert.IsTrue(strokes.Contains(OxyColors.Red));
        }

        [TestMethod]
        public void Highlight_UsesTheAboveSeriesLayer()
        {
            Assert.AreEqual(AnnotationLayer.AboveSeries, new SeriesHighlightAnnotation().Layer);
        }
    }
}
