using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Linq;

namespace CapFrameX.Test.Data
{
    [TestClass]
    public class LocalRecordDataServerTest
    {
        [TestMethod]
        public void DerivedWindowCache_ReusesResultsAndInvalidatesDisplayMetricMode()
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupProperty(value => value.UseDisplayChangeMetrics, false);
            configuration.Setup(value => value.FpsValuesRoundingDigits).Returns(2);
            configuration.Setup(value => value.IntervalAverageWindowTime).Returns(500);

            var captureData = new SessionCaptureData(2)
            {
                TimeInSeconds = new[] { 0d, 1d },
                MsBetweenPresents = new[] { 10d, 20d },
                MsBetweenDisplayChange = new[] { 5d, 10d }
            };
            var session = new Session();
            session.Runs.Add(new SessionRun { CaptureData = captureData });
            var server = new LocalRecordDataServer(configuration.Object)
            {
                CurrentSession = session
            };
            server.SetTimeWindow(0, 1);

            var presentFps = server.GetFpsTimeWindow();
            var repeatedPresentFps = server.GetFpsTimeWindow();

            Assert.AreSame(presentFps, repeatedPresentFps);
            CollectionAssert.AreEqual(new[] { 100d, 50d }, presentFps.ToArray());

            configuration.Object.UseDisplayChangeMetrics = true;
            var displayFps = server.GetFpsTimeWindow();

            Assert.AreNotSame(presentFps, displayFps);
            CollectionAssert.AreEqual(new[] { 200d, 100d }, displayFps.ToArray());
        }
    }
}
