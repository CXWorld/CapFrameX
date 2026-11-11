using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using CapFrameX.Test.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Statistics
{
    /// <summary>
    /// Every time window copies the whole session, so a chart builds each window it draws once
    /// and none it does not draw. A window reads its data array once per run, which is what the
    /// counting capture data below observes.
    /// </summary>
    [TestClass]
    public class PlotBuilderWindowTest
    {
        [TestMethod]
        public void FrametimeGraph_BuildsTheFrametimeWindowOnce()
        {
            // Regression: a frame time window was built and never used.
            var context = new Context();

            context.BuildFrametimeGraph(showDisplayTimes: false);

            Assert.AreEqual(1, context.CaptureData.FrametimeReads);
        }

        [TestMethod]
        public void FrametimeGraph_WithoutDisplayTimes_DoesNotBuildTheirWindow()
        {
            var context = new Context();

            var model = context.BuildFrametimeGraph(showDisplayTimes: false);

            Assert.AreEqual(0, context.CaptureData.DisplayChangeReads);
            Assert.IsFalse(model.Series.Any(series => series.Title == "Display Times"));
        }

        [TestMethod]
        public void FrametimeGraph_WithDisplayTimes_DrawsThem()
        {
            var context = new Context();

            var model = context.BuildFrametimeGraph(showDisplayTimes: true);

            Assert.AreEqual(1, context.CaptureData.DisplayChangeReads);
            var expected = context.Reference.GetDisplayChangeTimePointsTimeWindow(0, Context.End, context.Options.Object);
            AssertPoints(expected, Series(model, "Display Times"));
            // The moving average follows the display times when they are shown.
            Assert.AreEqual(expected.Count, Series(model, "Moving Average").Points.Count);
        }

        [TestMethod]
        public void FrametimeGraph_DrawsTheFrametimeWindow()
        {
            var context = new Context();

            var model = context.BuildFrametimeGraph(showDisplayTimes: false);

            var expected = context.Reference.GetFrametimePointsTimeWindow(0, Context.End, context.Options.Object);
            AssertPoints(expected, Series(model, "Frame Times"));
            Assert.AreEqual(expected.Count, Series(model, "Moving Average").Points.Count);
        }

        [TestMethod]
        public void FpsGraph_WithoutFilter_BuildsTheFpsPointsOnce()
        {
            // Regression: without a filter the raw FPS points were built twice.
            var context = new Context();

            context.BuildFpsGraph(EFilterMode.None);

            // One window for the average, one for the FPS points.
            Assert.AreEqual(2, context.CaptureData.FrametimeReads);
        }

        [TestMethod]
        public void FpsGraph_WithoutFilter_DrawsTheRawFps()
        {
            var context = new Context();

            var model = context.BuildFpsGraph(EFilterMode.None);

            AssertPoints(context.Reference.GetFpsPointsTimeWindow(0, Context.End, context.Options.Object,
                ERemoveOutlierMethod.None, EFilterMode.None), Series(model, "FPS"));
        }

        [TestMethod]
        public void FpsGraph_WithIntervalAverage_StillDrawsTheFilteredFps()
        {
            var context = new Context();

            var model = context.BuildFpsGraph(EFilterMode.TimeIntervalAverage);

            var filtered = context.Reference.GetFpsPointsTimeWindow(0, Context.End, context.Options.Object,
                ERemoveOutlierMethod.None, EFilterMode.TimeIntervalAverage);
            var raw = context.Reference.GetFpsPointsTimeWindow(0, Context.End, context.Options.Object,
                ERemoveOutlierMethod.None, EFilterMode.None);
            Assert.AreNotEqual(raw.Count, filtered.Count, "The test needs a filter that changes the points.");
            AssertPoints(filtered, Series(model, "FPS"));
            Assert.AreEqual(3, context.CaptureData.FrametimeReads);
        }

        private static OxyPlot.Series.LineSeries Series(OxyPlot.PlotModel model, string title)
            => model.Series.OfType<OxyPlot.Series.LineSeries>().Single(series => series.Title == title);

        private static void AssertPoints(IList<Point> expected, OxyPlot.Series.LineSeries series)
        {
            Assert.AreEqual(expected.Count, series.Points.Count, series.Title);
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].X, series.Points[i].X, $"{series.Title} X[{i}]");
                Assert.AreEqual(expected[i].Y, series.Points[i].Y, $"{series.Title} Y[{i}]");
            }
        }

        private sealed class Context
        {
            public const double End = 10;

            public Context()
            {
                var random = new Random(3);
                int count = 600;
                double[] frametimes = Enumerable.Range(0, count).Select(_ => 10 + random.NextDouble() * 15).ToArray();
                double time = 0;
                var data = new SessionCaptureData(count)
                {
                    MsBetweenPresents = frametimes,
                    MsBetweenDisplayChange = frametimes.Select(frametime => frametime + 1).ToArray(),
                    TimeInSeconds = frametimes.Select(frametime => (time += frametime) / 1000).ToArray(),
                };
                CaptureData = new CountingCaptureData(data);
                Reference = SessionWith(data);
                Options = new Mock<IFrametimeStatisticProviderOptions>();
                Options.SetupGet(value => value.FpsValuesRoundingDigits).Returns(2);
                Options.SetupGet(value => value.IntervalAverageWindowTime).Returns(500);
            }

            public CountingCaptureData CaptureData { get; }
            public ISession Reference { get; }
            public Mock<IFrametimeStatisticProviderOptions> Options { get; }

            public OxyPlot.PlotModel BuildFrametimeGraph(bool showDisplayTimes)
            {
                var builder = new FrametimePlotBuilder(Options.Object, new FrametimeStatisticProvider(Options.Object));
                builder.BuildPlotmodel(SessionWith(CaptureData), Settings(showDisplayTimes), 0, End, ERemoveOutlierMethod.None);
                return builder.PlotModel;
            }

            public OxyPlot.PlotModel BuildFpsGraph(EFilterMode filterMode)
            {
                var builder = new FpsGraphPlotBuilder(Options.Object, new FrametimeStatisticProvider(Options.Object));
                builder.BuildPlotmodel(SessionWith(CaptureData), Settings(false), 0, End, ERemoveOutlierMethod.None, filterMode);
                return builder.PlotModel;
            }

            private static IPlotSettings Settings(bool showDisplayTimes)
            {
                var settings = new Mock<IPlotSettings>();
                settings.SetupGet(value => value.ShowThresholds).Returns(true);
                settings.SetupGet(value => value.LowFPSThreshold).Returns(30);
                settings.SetupGet(value => value.StutteringFactor).Returns(2.5);
                settings.SetupGet(value => value.ShowDisplayTimes).Returns(showDisplayTimes);
                return settings.Object;
            }

            private static ISession SessionWith(ISessionCaptureData data)
            {
                var session = new Session();
                session.Runs.Add(new SessionRun { CaptureData = data });
                return session;
            }
        }
    }
}
