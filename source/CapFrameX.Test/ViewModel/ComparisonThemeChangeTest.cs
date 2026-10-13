using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Test.Localization;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using OxyPlot;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    // A theme change replaces the OxyPlot models with new, empty ones. The charts have to be
    // drawn again from the records, on the active tab right away and on the others once shown.
    [STATestClass]
    [DoNotParallelize]
    public class ComparisonThemeChangeTest
    {
        private const int FrameCount = 120;
        private const double FrametimeMs = 16.6;

        [TestMethod]
        [DataRow(EChartTab.Distribution, EChartTab.LineCharts)]
        [DataRow(EChartTab.LineCharts, EChartTab.Distribution)]
        public void ThemeChanged_RedrawsActiveTabAndLaterTheOtherChartTab(EChartTab activeTab, EChartTab otherTab)
        {
            using var subscriptions = new CxLangSubscriptions();
            var eventAggregator = new EventAggregator();
            var configuration = Configuration();
            var logger = new Mock<ILogger<ComparisonViewModel>>();
            var model = CreateViewModel(eventAggregator, configuration, logger.Object);
            AddRecord(model);

            // Both tabs were drawn once, so no flag is left dirty from the initial load.
            model.SelectedChartItem = new TabItem { Tag = otherTab };
            model.SelectedChartItem = new TabItem { Tag = activeTab };
            AssertDrawn(model, otherTab);
            AssertDrawn(model, activeTab);

            var previousModels = PlotModels(model, EChartTab.LineCharts).Concat(PlotModels(model, EChartTab.Distribution)).ToArray();
            configuration.UseDarkMode = !configuration.UseDarkMode;
            eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>().Publish(new ViewMessages.ThemeChanged());

            foreach (var plotModel in PlotModels(model, EChartTab.LineCharts).Concat(PlotModels(model, EChartTab.Distribution)))
            {
                CollectionAssert.DoesNotContain(previousModels, plotModel, "The theme change must build new plot models.");
                Assert.AreEqual(configuration.UseDarkMode ? OxyColors.White : OxyColors.Black, plotModel.TextColor);
            }
            AssertDrawn(model, activeTab);

            model.SelectedChartItem = new TabItem { Tag = otherTab };
            AssertDrawn(model, otherTab);

            logger.Verify(log => log.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Never);
        }

        private static void AssertDrawn(ComparisonViewModel model, EChartTab tab)
        {
            foreach (var plotModel in PlotModels(model, tab))
            {
                Assert.AreEqual(1, plotModel.Series.Count, $"The {tab} chart has to show the record.");
                Assert.IsTrue(((OxyPlot.Series.LineSeries)plotModel.Series[0]).Points.Count > 0,
                    $"The {tab} series has to contain the record's data.");
            }
        }

        private static IEnumerable<PlotModel> PlotModels(ComparisonViewModel model, EChartTab tab)
        {
            return tab == EChartTab.Distribution
                ? new[] { model.ComparisonDistributionModel }
                : new[] { model.ComparisonFrametimesModel, model.ComparisonFpsModel };
        }

        private static ComparisonViewModel CreateViewModel(IEventAggregator eventAggregator,
            CapFrameXConfiguration configuration, ILogger<ComparisonViewModel> logger)
        {
            var frametimeAnalyzer = new Mock<IFrametimeAnalyzer>();
            frametimeAnalyzer.Setup(analyzer => analyzer.GetLShapeQuantiles(It.IsAny<ELShapeMetrics>()))
                .Returns(new[] { 50d, 99d });
            var model = new ComparisonViewModel(Mock.Of<IStatisticProvider>(), frametimeAnalyzer.Object,
                eventAggregator, configuration, null, logger);
            typeof(ComparisonViewModel).GetMethod("InitializePlotModels", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(model, null);
            return model;
        }

        private static void AddRecord(ComparisonViewModel model)
        {
            var fileRecordInfo = new Mock<IFileRecordInfo>();
            fileRecordInfo.SetupGet(info => info.Id).Returns("record-1");

            var times = Enumerable.Range(0, FrameCount).Select(frame => frame * FrametimeMs / 1000).ToArray();
            ISession session = new Session
            {
                Runs = new List<ISessionRun>
                {
                    new SessionRun
                    {
                        CaptureData = new SessionCaptureData(FrameCount)
                        {
                            TimeInSeconds = times,
                            MsBetweenPresents = Enumerable.Repeat(FrametimeMs, FrameCount).ToArray()
                        }
                    }
                }
            };

            var record = new ComparisonRecordInfo
            {
                Game = "Game",
                Session = session,
                FileRecordInfo = fileRecordInfo.Object
            };
            model.ComparisonRecords.Add(new ComparisonRecordInfoWrapper(record, model)
            {
                FrametimeGraphColor = Colors.SteelBlue
            });
            model.FirstSeconds = 0;
            model.LastSeconds = times.Last();
        }

        private static CapFrameXConfiguration Configuration()
            => new CapFrameXConfiguration(NullLogger<CapFrameXConfiguration>.Instance, new MemorySettings());

        // The view model reads settings from background work while its constructor still runs.
        private sealed class MemorySettings : ISettingsStorage
        {
            private readonly ConcurrentDictionary<string, object> _values = new ConcurrentDictionary<string, object>();
            public Task Load() => Task.CompletedTask;
            public T GetValue<T>(string key) => (T)_values[key];
            public void SetValue(string key, object value) => _values[key] = value;
        }
    }
}
