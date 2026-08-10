using CapFrameX.Data.Session.Classes;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Linq;

namespace CapFrameX.Test.Statistics
{
    [TestClass]
    public class PlotBuilderRenderingTest
    {
        [TestMethod]
        public void FpsGraph_ConstantReferenceLines_UseTwoPoints()
        {
            var context = CreateContext();
            var builder = new FpsGraphPlotBuilder(context.Options.Object, context.Provider);

            builder.BuildPlotmodel(context.Session, context.PlotSettings.Object, 0, 2,
                ERemoveOutlierMethod.None, EFilterMode.None);

            Assert.AreEqual(2, GetSeries(builder, "Avg FPS").Points.Count);
            Assert.AreEqual(2, GetSeries(builder, "LowFPS").Points.Count);
        }

        [TestMethod]
        public void FrametimeGraph_LowFpsReferenceLine_UsesTwoPoints()
        {
            var context = CreateContext();
            var builder = new FrametimePlotBuilder(context.Options.Object, context.Provider);

            builder.BuildPlotmodel(context.Session, context.PlotSettings.Object, 0, 2,
                ERemoveOutlierMethod.None);

            Assert.AreEqual(2, GetSeries(builder, "Low FPS").Points.Count);
        }

        private static OxyPlot.Series.LineSeries GetSeries(PlotBuilder builder, string title)
            => builder.PlotModel.Series.OfType<OxyPlot.Series.LineSeries>()
                .Single(series => series.Title == title);

        private static TestContext CreateContext()
        {
            var options = new Mock<IFrametimeStatisticProviderOptions>();
            options.SetupGet(value => value.FpsValuesRoundingDigits).Returns(2);
            options.SetupGet(value => value.IntervalAverageWindowTime).Returns(500);
            var plotSettings = new Mock<IPlotSettings>();
            plotSettings.SetupGet(value => value.ShowThresholds).Returns(true);
            plotSettings.SetupGet(value => value.LowFPSThreshold).Returns(30);
            var captureData = new SessionCaptureData(3)
            {
                TimeInSeconds = new[] { 0d, 1d, 2d },
                MsBetweenPresents = new[] { 10d, 20d, 40d },
                MsBetweenDisplayChange = new[] { 0d, 0d, 0d }
            };
            var session = new Session();
            session.Runs.Add(new SessionRun { CaptureData = captureData });

            return new TestContext
            {
                Options = options,
                PlotSettings = plotSettings,
                Provider = new FrametimeStatisticProvider(options.Object),
                Session = session
            };
        }

        private sealed class TestContext
        {
            public Mock<IFrametimeStatisticProviderOptions> Options { get; set; }
            public Mock<IPlotSettings> PlotSettings { get; set; }
            public FrametimeStatisticProvider Provider { get; set; }
            public Session Session { get; set; }
        }
    }
}
