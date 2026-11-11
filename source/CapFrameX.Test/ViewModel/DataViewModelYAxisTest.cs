using System;
using System.Linq;
using System.Reflection;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Test.Mocks;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// The y-axis range of the analysis frame time chart.
    /// </summary>
    [STATestClass]
    public class DataViewModelYAxisTest
    {
        [TestMethod]
        public void IqrAxis_BuildsTheFrametimeWindowOnce()
        {
            // Regression: the interquartile range and the median each built the window.
            var viewModel = CreateViewModel();
            var frametimes = Frametimes();
            var data = SetSession(viewModel, frametimes);

            var setting = GetYAxisSetting(viewModel, EChartYAxisSetting.IQR);

            Assert.AreEqual(1, data.FrametimeReads);
            double iqr = MathNet.Numerics.Statistics.Statistics.InterquartileRange(frametimes);
            double median = MathNet.Numerics.Statistics.Statistics.Median(frametimes);
            Assert.AreEqual(median - 4 * iqr, setting.Item1, 1e-12);
            Assert.AreEqual(median + 6 * iqr, setting.Item2, 1e-12);
        }

        [TestMethod]
        public void FullFitAxis_SpansEveryFrametime()
        {
            var viewModel = CreateViewModel();
            var frametimes = Frametimes();
            SetSession(viewModel, frametimes);

            var setting = GetYAxisSetting(viewModel, EChartYAxisSetting.FullFit);

            double min = frametimes.Min(), max = frametimes.Max();
            Assert.AreEqual(min - (max - min) / 6, setting.Item1, 1e-12);
            Assert.AreEqual(max + (max - min) / 6, setting.Item2, 1e-12);
        }

        private static double[] Frametimes()
        {
            var random = new Random(5);
            return Enumerable.Range(0, 500).Select(_ => 8 + random.NextDouble() * 20).ToArray();
        }

        private static CountingCaptureData SetSession(DataViewModel viewModel, double[] frametimes)
        {
            double time = 0;
            var data = new CountingCaptureData(new SessionCaptureData(frametimes.Length)
            {
                MsBetweenPresents = frametimes,
                TimeInSeconds = frametimes.Select(frametime => (time += frametime) / 1000).ToArray(),
            });
            var session = new Session();
            session.Runs.Add(new SessionRun { CaptureData = data });
            var server = (LocalRecordDataServer)typeof(DataViewModel)
                .GetField("_localRecordDataServer", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(viewModel);
            server.CurrentSession = session;
            server.CurrentTime = 0;
            server.WindowLength = time / 1000 + 1;
            server.RemoveOutlierMethod = ERemoveOutlierMethod.None;
            return data;
        }

        private static Tuple<double, double> GetYAxisSetting(DataViewModel viewModel, EChartYAxisSetting selection)
            => (Tuple<double, double>)typeof(DataViewModel)
                .GetMethod("GetYAxisSettingFromSelection", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(viewModel, new object[] { selection });

        private static DataViewModel CreateViewModel()
        {
            // Every chart option off: no stuttering threshold, no GPU or CPU active chart.
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupAllProperties();
            return new DataViewModel(Mock.Of<IStatisticProvider>(), Mock.Of<IFrametimeAnalyzer>(),
                new EventAggregator(), configuration.Object, null, null, NullLogger<DataViewModel>.Instance);
        }
    }
}
