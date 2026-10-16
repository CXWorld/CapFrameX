using System;
using System.Linq;
using System.Reflection;
using CapFrameX.Contracts.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Test.Localization;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
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
        private RecordFiles _files;

        [TestInitialize]
        public void Initialize() => _files = new RecordFiles();

        [TestCleanup]
        public void Cleanup() => _files.Dispose();

        [TestMethod]
        [DataRow("missing")]
        [DataRow("empty file")]
        [DataRow("corrupt")]
        [DataRow("no frames")]
        public void Comparison_LeavesOutARecordThatCannotBeLoaded(string kind)
        {
            using var subscriptions = new CxLangSubscriptions();
            var logger = new Mock<ILogger<ComparisonViewModel>>();
            var model = _files.CreateComparison(logger.Object);

            AddComparisonItem(model, _files.Record(kind));

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
            var model = _files.CreateComparison();

            foreach (var record in new[] { _files.Record("corrupt"), _files.Record("valid", "A"),
                _files.Record("no frames"), _files.Record("valid", "B") })
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
            var model = _files.CreateReport(eventAggregator);

            Select(eventAggregator, model, _files.Record(kind));

            Assert.AreEqual(0, model.ReportInfoCollection.Count);
        }

        [TestMethod]
        public void Report_AddsAReadableRecordNextToAnUnreadableOne()
        {
            using var subscriptions = new CxLangSubscriptions();
            var eventAggregator = new EventAggregator();
            var model = _files.CreateReport(eventAggregator);

            Select(eventAggregator, model, _files.Record("corrupt"));
            Select(eventAggregator, model, _files.Record("valid", "A"));

            Assert.AreEqual(1, model.ReportInfoCollection.Count);
        }

        [TestMethod]
        public void HasFrames_RequiresAtLeastOneFrame()
        {
            Assert.IsFalse(ComparisonViewModel.HasFrames(null));
            Assert.IsFalse(ComparisonViewModel.HasFrames(new Session { Runs = null }));
            Assert.IsFalse(ComparisonViewModel.HasFrames(new Session()));
            Assert.IsFalse(ComparisonViewModel.HasFrames(RecordFiles.SessionWith("Game", new SessionRun())));
            Assert.IsFalse(ComparisonViewModel.HasFrames(RecordFiles.SessionWith("Game",
                new SessionRun { CaptureData = new SessionCaptureData(0) })));
            Assert.IsTrue(ComparisonViewModel.HasFrames(RecordFiles.SessionWith("Game",
                new SessionRun { CaptureData = new SessionCaptureData(0) }, RecordFiles.Run())));
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
    }
}
