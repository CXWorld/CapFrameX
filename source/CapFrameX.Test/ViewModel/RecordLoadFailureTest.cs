using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Test.Localization;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// A record that cannot be loaded is left out of Comparison and Report instead of ending the
    /// whole drop with an exception.
    /// </summary>
    [STATestClass]
    [DoNotParallelize]
    public class RecordLoadFailureTest
    {
        private string _folder;
        private RecordManager _recordManager;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordLoadFailureTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
            _recordManager = new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null, new EventAggregator(), null, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        [DataRow("missing")]
        [DataRow("empty file")]
        [DataRow("corrupt")]
        [DataRow("no frames")]
        public void Comparison_LeavesOutARecordThatCannotBeLoaded(string kind)
        {
            using var subscriptions = new CxLangSubscriptions();
            var logger = new Mock<ILogger<ComparisonViewModel>>();
            var model = CreateComparison(logger.Object);

            AddComparisonItem(model, Record(kind));

            Assert.AreEqual(0, model.ComparisonRecords.Count);
            Assert.IsFalse(model.HasComparisonItems);
            logger.Verify(log => log.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [TestMethod]
        public void Comparison_DropWithUnreadableRecords_AddsEveryReadableOne()
        {
            // Regression: the first unreadable record threw inside the drop and the rest was lost.
            using var subscriptions = new CxLangSubscriptions();
            var model = CreateComparison(NullLogger<ComparisonViewModel>.Instance);

            foreach (var record in new[] { Record("corrupt"), Record("valid", "A"), Record("no frames"), Record("valid", "B") })
                AddComparisonItem(model, record);

            CollectionAssert.AreEquivalent(new[] { "A", "B" },
                model.ComparisonRecords.Select(record => record.WrappedRecordInfo.Game).ToArray());
            Assert.AreNotEqual(model.ComparisonRecords[0].FrametimeGraphColor, model.ComparisonRecords[1].FrametimeGraphColor,
                "A skipped record must not take a color.");
        }

        [TestMethod]
        [DataRow("missing")]
        [DataRow("empty file")]
        [DataRow("corrupt")]
        [DataRow("no frames")]
        public void Report_LeavesOutARecordThatCannotBeLoaded(string kind)
        {
            using var subscriptions = new CxLangSubscriptions();
            var eventAggregator = new EventAggregator();
            var model = CreateReport(eventAggregator);

            Select(eventAggregator, model, Record(kind));

            Assert.AreEqual(0, model.ReportInfoCollection.Count);
        }

        [TestMethod]
        public void Report_AddsAReadableRecordNextToAnUnreadableOne()
        {
            using var subscriptions = new CxLangSubscriptions();
            var eventAggregator = new EventAggregator();
            var model = CreateReport(eventAggregator);

            Select(eventAggregator, model, Record("corrupt"));
            Select(eventAggregator, model, Record("valid", "A"));

            Assert.AreEqual(1, model.ReportInfoCollection.Count);
        }

        [TestMethod]
        public void HasFrames_RequiresAtLeastOneFrame()
        {
            Assert.IsFalse(ComparisonViewModel.HasFrames(null));
            Assert.IsFalse(ComparisonViewModel.HasFrames(new Session { Runs = null }));
            Assert.IsFalse(ComparisonViewModel.HasFrames(new Session()));
            Assert.IsFalse(ComparisonViewModel.HasFrames(SessionWith(new SessionRun())));
            Assert.IsFalse(ComparisonViewModel.HasFrames(SessionWith(new SessionRun { CaptureData = new SessionCaptureData(0) })));
            Assert.IsTrue(ComparisonViewModel.HasFrames(SessionWith(
                new SessionRun { CaptureData = new SessionCaptureData(0) }, ValidRun())));
        }

        private IFileRecordInfo Record(string kind, string game = "Game")
        {
            string path = Path.Combine(_folder, Guid.NewGuid().ToString("N") + ".json");
            switch (kind)
            {
                case "missing":
                    break;
                case "empty file":
                    File.WriteAllText(path, string.Empty);
                    break;
                case "corrupt":
                    File.WriteAllText(path, "{\"Hash\":\"broken\",\"Runs\":[{\"CaptureData\":");
                    break;
                case "no frames":
                    File.WriteAllText(path, JsonConvert.SerializeObject(new Session { Hash = "empty", Info = Info(game) }));
                    break;
                default:
                    File.WriteAllText(path, JsonConvert.SerializeObject(SessionWith(ValidRun())));
                    break;
            }

            var record = new Mock<IFileRecordInfo>();
            record.SetupGet(info => info.Id).Returns(path);
            record.SetupGet(info => info.FullPath).Returns(path);
            record.SetupGet(info => info.FileInfo).Returns(new FileInfo(path));
            record.SetupGet(info => info.GameName).Returns(game);
            record.SetupGet(info => info.ProcessName).Returns(game + ".exe");
            record.SetupGet(info => info.CreationDate).Returns("2026-10-16");
            record.SetupGet(info => info.CreationTime).Returns("12:00:00");
            return record.Object;
        }

        // Saved records always carry the app version; the version converter rejects a null one.
        private static SessionInfo Info(string game)
            => new SessionInfo { AppVersion = new Version(1, 9, 2, 0), Id = Guid.NewGuid(), GameName = game, ProcessName = game + ".exe" };

        private static Session SessionWith(params ISessionRun[] runs)
            => new Session { Hash = Guid.NewGuid().ToString("N"), Info = Info("Game"), Runs = new List<ISessionRun>(runs) };

        private static SessionRun ValidRun()
        {
            const int count = 240;
            double[] frametimes = Enumerable.Range(0, count).Select(frame => 15d + frame % 4).ToArray();
            double time = 0;
            return new SessionRun
            {
                Hash = Guid.NewGuid().ToString("N"),
                CaptureData = new SessionCaptureData(count)
                {
                    MsBetweenPresents = frametimes,
                    MsBetweenDisplayChange = frametimes,
                    MsUntilDisplayed = frametimes,
                    TimeInSeconds = frametimes.Select(frametime => (time += frametime) / 1000).ToArray(),
                }
            };
        }

        private ComparisonViewModel CreateComparison(ILogger<ComparisonViewModel> logger)
        {
            var frametimeAnalyzer = new Mock<IFrametimeAnalyzer>();
            frametimeAnalyzer.Setup(analyzer => analyzer.GetLShapeQuantiles(It.IsAny<ELShapeMetrics>()))
                .Returns(new[] { 50d, 99d });
            var options = new Mock<IFrametimeStatisticProviderOptions>();
            options.SetupGet(value => value.FpsValuesRoundingDigits).Returns(2);
            options.SetupGet(value => value.IntervalAverageWindowTime).Returns(500);
            var model = new ComparisonViewModel(new FrametimeStatisticProvider(options.Object), frametimeAnalyzer.Object,
                new EventAggregator(), Configuration(), _recordManager, logger);
            typeof(ComparisonViewModel).GetMethod("InitializePlotModels", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(model, null);
            return model;
        }

        private ReportViewModel CreateReport(IEventAggregator eventAggregator)
        {
            var model = new ReportViewModel(Mock.Of<IStatisticProvider>(), eventAggregator, Configuration(),
                _recordManager, NullLogger<ReportViewModel>.Instance);
            typeof(ReportViewModel).GetField("_useEventMessages", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(model, true);
            return model;
        }

        private static void AddComparisonItem(ComparisonViewModel model, IFileRecordInfo record)
            => typeof(ComparisonViewModel).GetMethod("AddComparisonItem", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(model, new object[] { record });

        private static void Select(IEventAggregator eventAggregator, ReportViewModel model, IFileRecordInfo record)
        {
            GC.KeepAlive(model);
            eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>()
                .Publish(new ViewMessages.SelectSession(null, record));
        }

        private static CapFrameXConfiguration Configuration()
            => new CapFrameXConfiguration(NullLogger<CapFrameXConfiguration>.Instance, new MemorySettings());

        // The view models read settings from background work while their constructors still run.
        private sealed class MemorySettings : ISettingsStorage
        {
            private readonly ConcurrentDictionary<string, object> _values = new ConcurrentDictionary<string, object>();
            public Task Load() => Task.CompletedTask;
            public T GetValue<T>(string key) => (T)_values[key];
            public void SetValue(string key, object value) => _values[key] = value;
        }
    }
}
