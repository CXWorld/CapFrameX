using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Test.Localization;
using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// Dropped records are loaded off the UI thread and added as one batch: the charts of
    /// Comparison are drawn once and the average row of Report is computed once.
    /// </summary>
    [STATestClass]
    [DoNotParallelize]
    public class RecordBatchDropTest
    {
        private RecordFiles _files;

        [TestInitialize]
        public void Initialize() => _files = new RecordFiles();

        [TestCleanup]
        public void Cleanup() => _files.Dispose();

        [TestMethod]
        public void ComparisonDrop_DrawsTheChartsOnceForAllRecords()
        {
            // Regression: every record redrew the charts of all records added before it.
            using var subscriptions = new CxLangSubscriptions();
            int single = CountChartRedraws(_files.CreateComparison(), model =>
                Drop(model, _files.Record("valid", "Single")));

            var batch = _files.CreateComparison();
            int forFive = CountChartRedraws(batch, model =>
                Drop(model, Enumerable.Range(0, 5).Select(i => _files.Record("valid", "Game" + i)).ToArray()));

            Assert.AreEqual(5, batch.ComparisonRecords.Count);
            Assert.AreEqual(single, forFive, "Five dropped records must be drawn like one.");
        }

        [TestMethod]
        public void ComparisonDrop_MatchesAddingTheRecordsOneByOne()
        {
            using var subscriptions = new CxLangSubscriptions();
            var frametimes = new[] { 8.0, 16.0, 11.0, 25.0 };
            var records = frametimes.Select((frametime, i) =>
                _files.Record("valid", "Game" + i, RecordFiles.Constant(frametime, 3))).ToArray();
            var oneByOne = _files.CreateComparison();
            foreach (var record in records)
                AddComparisonItem(oneByOne, record);

            var dropped = _files.CreateComparison();
            Drop(dropped, records);

            CollectionAssert.AreEqual(Games(oneByOne), Games(dropped), "Same sort order");
            CollectionAssert.AreEqual(
                oneByOne.ComparisonRecords.Select(record => record.FrametimeGraphColor).ToArray(),
                dropped.ComparisonRecords.Select(record => record.FrametimeGraphColor).ToArray(), "Same colors");
            CollectionAssert.AreEqual(
                oneByOne.ComparisonRecords.Select(record => record.WrappedRecordInfo.FirstMetric).ToArray(),
                dropped.ComparisonRecords.Select(record => record.WrappedRecordInfo.FirstMetric).ToArray(), "Same metrics");
            Assert.AreEqual(oneByOne.CurrentGameName, dropped.CurrentGameName);
            Assert.AreEqual(oneByOne.MaxRecordingTime, dropped.MaxRecordingTime);
            Assert.AreEqual(oneByOne.HasUniqueGameNames, dropped.HasUniqueGameNames);
        }

        [TestMethod]
        public void ComparisonDrop_RecordsAlreadyInTheList_AreAddedOnceAndReported()
        {
            using var subscriptions = new CxLangSubscriptions();
            var model = _files.CreateComparison();
            var first = _files.Record("valid", "A");
            var second = _files.Record("valid", "B");
            Drop(model, first);
            Assert.IsFalse(model.MessageDialogContentIsOpen);

            Drop(model, first, second, second);

            CollectionAssert.AreEquivalent(new[] { "A", "B" }, Games(model));
            Assert.IsTrue(model.MessageDialogContentIsOpen);
        }

        [TestMethod]
        public void ComparisonDrop_MetricsOfALongerRecordCoverAllOfIt()
        {
            // Adding a record resets the range to the longest record. A longer record's metrics
            // used to be computed for the range from before and covered only part of it.
            using var subscriptions = new CxLangSubscriptions();
            var model = _files.CreateComparison();
            Drop(model, _files.Record("valid", "Short", RecordFiles.Constant(10, 2)));
            double[] longFrametimes = RecordFiles.Constant(10, 2).Concat(RecordFiles.Constant(20, 2)).ToArray();

            Drop(model, _files.Record("valid", "Long", longFrametimes));

            var longRecord = model.ComparisonRecords.Single(record => record.WrappedRecordInfo.Game == "Long");
            var session = longRecord.WrappedRecordInfo.Session;
            double lastFrameStart = session.Runs.Last().CaptureData.TimeInSeconds.Last();
            double expected = RecordFiles.StatisticProvider().GetFpsMetricValue(
                session.GetFrametimeTimeWindow(0, lastFrameStart, null), model.SelectedFirstMetric);
            Assert.AreEqual(expected, longRecord.WrappedRecordInfo.FirstMetric);
            Assert.AreEqual(0, model.FirstSeconds);
            Assert.AreEqual(model.MaxRecordingTime, model.LastSeconds);
        }

        [TestMethod]
        public void ComparisonDrop_ChangesTheListOnTheUiThread()
        {
            using var subscriptions = new CxLangSubscriptions();
            var model = _files.CreateComparison();
            var threads = new List<int>();
            model.ComparisonRecords.CollectionChanged += (_, _) => threads.Add(Thread.CurrentThread.ManagedThreadId);

            Drop(model, _files.Record("valid", "A"), _files.Record("valid", "B"));

            Assert.AreEqual(2, threads.Count);
            Assert.IsTrue(threads.All(thread => thread == Thread.CurrentThread.ManagedThreadId));
        }

        [TestMethod]
        public void ReportDrop_AddsEveryRowInDropOrderAndOneAverageRow()
        {
            using var subscriptions = new CxLangSubscriptions();
            var model = _files.CreateReport(new EventAggregator(), showAverageRow: true);

            DropOnReport(model, _files.Record("valid", "A", RecordFiles.Constant(10, 2)),
                _files.Record("corrupt"), _files.Record("valid", "B", RecordFiles.Constant(20, 2)),
                _files.Record("valid", "C", RecordFiles.Constant(16, 2)));

            CollectionAssert.AreEqual(new[] { "A", "B", "C" },
                model.ReportInfoCollection.Where(row => !row.IsAverageRow).Select(row => row.Game).ToArray());
            Assert.AreEqual(1, model.ReportInfoCollection.Count(row => row.IsAverageRow));
            var average = model.ReportInfoCollection.Last();
            Assert.IsTrue(average.IsAverageRow);
            Assert.AreEqual(Math.Round(model.ReportInfoCollection.Take(3).Average(row => row.AverageFps), 1), average.AverageFps);
        }

        [TestMethod]
        public void ReportDrop_ComputesTheAverageRowOnce()
        {
            // Regression: every dropped row removed and recomputed the average row.
            using var subscriptions = new CxLangSubscriptions();
            var model = _files.CreateReport(new EventAggregator(), showAverageRow: true);
            var averageRowChanges = new List<NotifyCollectionChangedAction>();
            model.ReportInfoCollection.CollectionChanged += (_, change) =>
            {
                var rows = (change.NewItems ?? change.OldItems)?.Cast<ReportInfo>() ?? Enumerable.Empty<ReportInfo>();
                if (rows.Any(row => row.IsAverageRow))
                    averageRowChanges.Add(change.Action);
            };

            DropOnReport(model, Enumerable.Range(0, 4).Select(i => _files.Record("valid", "Game" + i)).ToArray());

            CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Add }, averageRowChanges);
        }

        [TestMethod]
        public void ReportDrop_NextToEarlierRows_KeepsOneAverageRowAtTheEnd()
        {
            using var subscriptions = new CxLangSubscriptions();
            var model = _files.CreateReport(new EventAggregator(), showAverageRow: true);
            DropOnReport(model, _files.Record("valid", "A"), _files.Record("valid", "B"));

            DropOnReport(model, _files.Record("valid", "C"));

            CollectionAssert.AreEqual(new[] { "A", "B", "C" },
                model.ReportInfoCollection.Where(row => !row.IsAverageRow).Select(row => row.Game).ToArray());
            Assert.AreEqual(1, model.ReportInfoCollection.Count(row => row.IsAverageRow));
            Assert.IsTrue(model.ReportInfoCollection.Last().IsAverageRow);
        }

        [TestMethod]
        public void ReportDrop_OfUnreadableRecordsOnly_ChangesNothing()
        {
            using var subscriptions = new CxLangSubscriptions();
            var model = _files.CreateReport(new EventAggregator(), showAverageRow: true);

            DropOnReport(model, _files.Record("corrupt"), _files.Record("missing"));

            Assert.AreEqual(0, model.ReportInfoCollection.Count);
        }

        private static string[] Games(ComparisonViewModel model)
            => model.ComparisonRecords.Select(record => record.WrappedRecordInfo.Game).ToArray();

        private static int CountChartRedraws(ComparisonViewModel model, Action<ComparisonViewModel> action)
        {
            // UpdateCharts resets the L-shape chart on every redraw.
            int redraws = 0;
            using var subscription = model.ResetLShapeChart.Subscribe(_ => redraws++);
            action(model);
            return redraws;
        }

        private static void Drop(ComparisonViewModel model, params IFileRecordInfo[] records)
            => RecordFiles.RunOnDispatcher(() => (Task)typeof(ComparisonViewModel)
                .GetMethod("AddComparisonItemsAsync", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(model, new object[] { records }));

        private static void DropOnReport(ReportViewModel model, params IFileRecordInfo[] records)
            => RecordFiles.RunOnDispatcher(() => (Task)typeof(ReportViewModel)
                .GetMethod("AddReportRecordsAsync", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(model, new object[] { records }));

        private static void AddComparisonItem(ComparisonViewModel model, IFileRecordInfo record)
            => typeof(ComparisonViewModel).GetMethod("AddComparisonItem", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(model, new object[] { record });
    }
}
