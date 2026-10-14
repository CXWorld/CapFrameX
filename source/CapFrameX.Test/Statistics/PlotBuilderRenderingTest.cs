using CapFrameX.Data.Session.Classes;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OxyPlot;
using OxyPlot.Axes;
using System.Globalization;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;

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

        [TestMethod]
        public void FrametimeGraph_LargeFrametimes_KeepTheAxisTitleInPlace()
        {
            // Loading screens produce spikes of tens of seconds. Their five-digit tick labels must
            // not push the frame time axis title further out than a regular recording does; with a
            // fixed left margin it ended up cut off at the edge of the chart.
            double regular = GetLeftAxisTitleX(BuildFrametimeGraph(16, 17, 18, 16));
            double spikes = GetLeftAxisTitleX(BuildFrametimeGraph(16, 22000, 18, 16));

            Assert.IsTrue(spikes >= regular - 0.5,
                $"The axis title moved from x={regular} to x={spikes} for long tick labels.");
        }

        [TestMethod]
        public void DistributionGraph_LongStall_StaysInTheSeriesButOutsideTheDefaultRange()
        {
            var frametimes = Enumerable.Repeat(10d, 10000).Append(22728.0369).ToArray();
            var context = CreateContext(frametimes);
            var builder = new FrametimeDistributionPlotBuilder(context.Options.Object, context.Provider);

            builder.BuildPlotmodel(context.Session, context.PlotSettings.Object, 0, frametimes.Length,
                ERemoveOutlierMethod.None);

            var xAxis = builder.PlotModel.GetAxisOrDefault(EPlotAxis.XAXISFRAMETIMES.GetDescription(), null);
            var yAxis = builder.PlotModel.GetAxisOrDefault(EPlotAxis.YAXISDISTRIBUTION.GetDescription(), null);
            var series = GetSeries(builder, "Distribution");
            Assert.AreEqual(2, series.Points.Count);
            Assert.AreEqual(22728.1, series.Points[1].X, 1e-9);
            Assert.AreEqual(9.1, xAxis.Minimum, 1e-9);
            Assert.AreEqual(11.1, xAxis.Maximum, 1e-9);
            Assert.AreEqual(series.Points[0].Y * 1.2, yAxis.Maximum, 1e-9);
        }

        [TestMethod]
        public void DistributionGraph_WithoutValidFrames_StaysEmpty()
        {
            var context = CreateContext(0d, 0d, 0d);
            var builder = new FrametimeDistributionPlotBuilder(context.Options.Object, context.Provider);

            builder.BuildPlotmodel(context.Session, context.PlotSettings.Object, 0, 2,
                ERemoveOutlierMethod.None);

            Assert.AreEqual(0, builder.PlotModel.Series.Count);
        }

        private static PlotModel BuildFrametimeGraph(params double[] frametimes)
        {
            var context = CreateContext(frametimes);
            var builder = new FrametimePlotBuilder(context.Options.Object, context.Provider);
            builder.BuildPlotmodel(context.Session, context.PlotSettings.Object, 0, frametimes.Length - 1,
                ERemoveOutlierMethod.None);
            return builder.PlotModel;
        }

        // Renders the model and returns the x position of the rotated title of its left axis.
        private static double GetLeftAxisTitleX(PlotModel model)
        {
            string title = model.Axes.First(axis => axis.Position == AxisPosition.Left && axis.IsAxisVisible).Title;
            string svg = SvgExporter.ExportToString(model, 1100, 500, false);
            var match = Regex.Match(svg, @"translate\(([-\d.]+),[-\d.]+\) rotate\(-90\)""[^>]*>"
                + Regex.Escape(SecurityElement.Escape(title)) + "</text>");
            Assert.IsTrue(match.Success, "The left axis title was not rendered.");
            return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private static OxyPlot.Series.LineSeries GetSeries(PlotBuilder builder, string title)
            => builder.PlotModel.Series.OfType<OxyPlot.Series.LineSeries>()
                .Single(series => series.Title == title);

        private static TestContext CreateContext(params double[] frametimes)
        {
            if (frametimes.Length == 0)
            {
                frametimes = new[] { 10d, 20d, 40d };
            }

            var options = new Mock<IFrametimeStatisticProviderOptions>();
            options.SetupGet(value => value.FpsValuesRoundingDigits).Returns(2);
            options.SetupGet(value => value.IntervalAverageWindowTime).Returns(500);
            var plotSettings = new Mock<IPlotSettings>();
            plotSettings.SetupGet(value => value.ShowThresholds).Returns(true);
            plotSettings.SetupGet(value => value.LowFPSThreshold).Returns(30);
            var captureData = new SessionCaptureData(frametimes.Length)
            {
                TimeInSeconds = frametimes.Select((_, index) => (double)index).ToArray(),
                MsBetweenPresents = frametimes,
                MsBetweenDisplayChange = new double[frametimes.Length]
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
