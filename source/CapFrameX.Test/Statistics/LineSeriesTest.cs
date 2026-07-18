using CapFrameX.Data.Session.Classes;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OxyPlot;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.Test.Statistics
{
    [TestClass]
    public class LineSeriesTest
    {
        [TestMethod]
        public void DecimateScreenPoints_PreservesEndpointsAndPixelExtrema()
        {
            var input = new List<ScreenPoint>
            {
                new ScreenPoint(0.1, 5),
                new ScreenPoint(0.2, 10),
                new ScreenPoint(0.3, -20),
                new ScreenPoint(0.4, 30),
                new ScreenPoint(1.1, 8),
                new ScreenPoint(1.2, -4),
                new ScreenPoint(2.1, 7)
            };
            var output = new List<ScreenPoint>();

            LineSeries.DecimateScreenPoints(input, output);

            Assert.AreEqual(input.First(), output.First());
            Assert.AreEqual(input.Last(), output.Last());
            Assert.IsTrue(output.Contains(new ScreenPoint(0.3, -20)));
            Assert.IsTrue(output.Contains(new ScreenPoint(0.4, 30)));
            Assert.IsTrue(output.Contains(new ScreenPoint(1.2, -4)));
            Assert.IsTrue(output.Count < input.Count);
        }

        [TestMethod]
        public void FpsGraph_DisplayTimesUnavailable_FallsBackToPresentTimes()
        {
            var options = new Mock<IFrametimeStatisticProviderOptions>();
            options.SetupGet(value => value.FpsValuesRoundingDigits).Returns(2);
            options.SetupGet(value => value.IntervalAverageWindowTime).Returns(500);
            var plotSettings = new Mock<IPlotSettings>();
            plotSettings.SetupGet(value => value.ShowDisplayTimes).Returns(true);
            var captureData = new SessionCaptureData(3)
            {
                TimeInSeconds = new[] { 0d, 1d, 2d },
                MsBetweenPresents = new[] { 10d, 20d, 40d },
                MsBetweenDisplayChange = new[] { 0d, 0d, 0d }
            };
            var session = new Session();
            session.Runs.Add(new SessionRun { CaptureData = captureData });
            var provider = new FrametimeStatisticProvider(options.Object);
            var builder = new FpsGraphPlotBuilder(options.Object, provider);

            builder.BuildPlotmodel(session, plotSettings.Object, 0, 2,
                ERemoveOutlierMethod.None, EFilterMode.None);

            var fpsSeries = builder.PlotModel.Series
                .OfType<OxyPlot.Series.LineSeries>()
                .Single(series => series.Title == "FPS");
            Assert.AreEqual(3, fpsSeries.Points.Count);
            Assert.AreEqual(100, fpsSeries.Points[0].Y, 0.001);
        }
    }
}
