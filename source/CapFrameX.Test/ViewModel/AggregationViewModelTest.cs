using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.ViewModel;
using GongSolutions.Wpf.DragDrop;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// The aggregation keeps its records and their table entries in step: outlier flags and the
    /// written aggregate map entries to records by position. It keeps the samples of every
    /// record, so a metric change or the aggregate does not parse the files again, and a drop
    /// parses them off the UI thread.
    /// </summary>
    [STATestClass]
    public class AggregationViewModelTest
    {
        [TestMethod]
        public void EntriesRebuiltForAnotherMetric_KeepTheirRecords()
        {
            var context = new AggregationContext();
            var records = context.Records(3);
            context.Drop(records);

            context.Model.SelectedSecondMetric = EMetric.P5;

            CollectionAssert.AreEqual(records, context.Model.AggregationEntries.Select(entry => entry.FileRecordInfo).ToArray());
        }

        [TestMethod]
        public void RemovingAnEntryAfterAMetricChange_LeavesItsRecordOutOfTheAggregate()
        {
            // Regression: the rebuilt entries had no record, so removing one removed nothing
            // from the record list and the aggregate still contained it.
            var context = new AggregationContext();
            var records = context.Records(3);
            context.Drop(records);
            context.Model.SelectedSecondMetric = EMetric.P5;

            context.Model.RemoveAggregationEntry(context.Model.AggregationEntries[1]);
            context.Model.AggregateIncludeCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { context.RunOf(records[0]), context.RunOf(records[2]) },
                context.SavedRuns().ToArray());
        }

        [TestMethod]
        public void ExcludeAfterRemovingAnEntry_LeavesOutTheFlaggedRecord()
        {
            var context = new AggregationContext();
            var records = context.Records(3);
            context.Drop(records);
            context.Model.SelectedSecondMetric = EMetric.P5;
            context.Model.RemoveAggregationEntry(context.Model.AggregationEntries[0]);

            context.OutlierFlags = new[] { false, true };
            context.Model.AggregateExcludeCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { context.RunOf(records[1]) }, context.SavedRuns().ToArray());
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void RecordThatCannotBeLoaded_IsNotAdded(bool hasNoFrames)
        {
            var context = new AggregationContext();
            var records = context.Records(3);
            context.Sessions[records[1].FullPath] = hasNoFrames ? new Session() : null;

            context.Drop(records);
            context.Model.AggregateIncludeCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { records[0], records[2] },
                context.Model.AggregationEntries.Select(entry => entry.FileRecordInfo).ToArray());
            CollectionAssert.AreEqual(new[] { context.RunOf(records[0]), context.RunOf(records[2]) },
                context.SavedRuns().ToArray());
        }

        [TestMethod]
        public void Drop_ComputesEachEntryFromTheSamplesOfAllItsRuns()
        {
            var context = new AggregationContext();
            var records = context.Records(2, runsPerRecord: 2);

            context.Drop(records);

            Assert.HasCount(2, context.MetricCalls);
            for (int index = 0; index < records.Length; index++)
            {
                CollectionAssert.AreEqual(context.FrametimesOf(records[index]), context.MetricCalls[index].Frametimes.ToArray());
                CollectionAssert.AreEqual(context.DisplaytimesOf(records[index]), context.MetricCalls[index].Displaytimes.ToArray());
                Assert.AreEqual("P1", context.MetricCalls[index].Second);
                Assert.AreEqual("P0dot2", context.MetricCalls[index].Third);
            }
        }

        [TestMethod]
        [DataRow("second")]
        [DataRow("third")]
        public void ChangingTheSecondOrThirdMetric_RecomputesTheEntriesFromTheKeptSamples(string metric)
        {
            var context = new AggregationContext();
            var records = context.Records(3, runsPerRecord: 2);
            context.Drop(records);
            context.ResetCalls();

            if (metric == "second")
                context.Model.SelectedSecondMetric = EMetric.P5;
            else
                context.Model.SelectedThirdMetric = EMetric.Median;

            Assert.AreEqual(0, context.TotalLoads, "A metric change parsed the records again.");
            Assert.HasCount(3, context.MetricCalls);
            for (int index = 0; index < records.Length; index++)
            {
                var call = context.MetricCalls[index];
                CollectionAssert.AreEqual(context.FrametimesOf(records[index]), call.Frametimes.ToArray());
                CollectionAssert.AreEqual(context.DisplaytimesOf(records[index]), call.Displaytimes.ToArray());
                Assert.AreEqual(metric == "second" ? "P5" : "P1", call.Second);
                Assert.AreEqual(metric == "third" ? "Median" : "P0dot2", call.Third);
            }
            Assert.HasCount(1, context.OutlierCalls, "The rebuilt entries need one outlier analysis.");
            Assert.HasCount(3, context.OutlierCalls[0].Analyses);
        }

        [TestMethod]
        [DataRow("related")]
        [DataRow("percentage")]
        public void ChangingTheOutlierSettings_OnlyRepeatsTheOutlierAnalysis(string setting)
        {
            var context = new AggregationContext();
            var records = context.Records(3);
            context.Drop(records);
            var entries = context.Model.AggregationEntries.ToArray();
            context.ResetCalls();

            if (setting == "related")
                context.Model.SelectedRelatedMetric = "Third";
            else
                context.Model.SelectedOutlierPercentage = 7;

            Assert.AreEqual(0, context.TotalLoads);
            Assert.IsEmpty(context.MetricCalls, "The entries do not depend on the outlier settings.");
            CollectionAssert.AreEqual(entries, context.Model.AggregationEntries.ToArray());
            Assert.HasCount(1, context.OutlierCalls);
            var call = context.OutlierCalls[0];
            CollectionAssert.AreEqual(entries.Select(entry => entry.MetricAnalysis).ToArray(), call.Analyses.ToArray());
            Assert.AreEqual(setting == "related" ? "Third" : "Second", call.Metric);
            Assert.AreEqual(setting == "percentage" ? 7 : 3, call.Percentage);
        }

        [TestMethod]
        public void ChangingTheOutlierSettings_PublishesTheNewFlags()
        {
            var context = new AggregationContext();
            context.Drop(context.Records(3));
            bool[] published = null;
            using var subscription = context.Model.OutlierFlagStream.Subscribe(flags => published = flags);

            context.OutlierFlags = new[] { false, true, false };
            context.Model.SelectedOutlierPercentage = 5;

            CollectionAssert.AreEqual(new[] { false, true, false }, published);
            Assert.IsTrue(context.Model.ShowIncludeExcludeButtons);
            Assert.IsFalse(context.Model.EnableAggregationButton);
        }

        [TestMethod]
        public void ChangingAMetricWithoutRecords_DoesNothing()
        {
            var context = new AggregationContext();

            context.Model.SelectedSecondMetric = EMetric.P5;
            context.Model.SelectedRelatedMetric = "Average";
            context.Model.SelectedOutlierPercentage = 4;

            Assert.IsEmpty(context.MetricCalls);
            Assert.IsEmpty(context.OutlierCalls);
            Assert.IsTrue(context.Model.ShowHelpText);
            Assert.IsFalse(context.Model.EnableAggregationButton);
        }

        [TestMethod]
        public void Include_AggregatesTheKeptSamplesInTableOrder()
        {
            var context = new AggregationContext();
            var records = context.Records(3, runsPerRecord: 2);
            context.Drop(records);
            context.ResetCalls();

            context.Model.AggregateIncludeCommand.Execute(null);

            var aggregate = context.MetricCalls.Single();
            CollectionAssert.AreEqual(records.SelectMany(context.FrametimesOf).ToArray(), aggregate.Frametimes.ToArray());
            CollectionAssert.AreEqual(records.SelectMany(context.DisplaytimesOf).ToArray(), aggregate.Displaytimes.ToArray());
            Assert.AreEqual("Result: result", context.Model.AggregationResultString);
            CollectionAssert.AreEqual(records.SelectMany(record => context.Sessions[record.FullPath].Runs).ToArray(),
                context.SavedRuns().ToArray());
            // Only writing the aggregate file reads the records, once each.
            Assert.AreEqual(3, context.TotalLoads);
        }

        [TestMethod]
        public void Exclude_AggregatesTheKeptSamplesOfTheRecordsThatAreNoOutliers()
        {
            var context = new AggregationContext();
            var records = context.Records(4);
            context.Drop(records);
            context.ResetCalls();

            context.OutlierFlags = new[] { false, true, false, true };
            context.Model.AggregateExcludeCommand.Execute(null);

            var aggregate = context.MetricCalls.Single();
            CollectionAssert.AreEqual(context.FrametimesOf(records[0]).Concat(context.FrametimesOf(records[2])).ToArray(),
                aggregate.Frametimes.ToArray());
            CollectionAssert.AreEqual(context.DisplaytimesOf(records[0]).Concat(context.DisplaytimesOf(records[2])).ToArray(),
                aggregate.Displaytimes.ToArray());
            CollectionAssert.AreEqual(new[] { context.RunOf(records[0]), context.RunOf(records[2]) },
                context.SavedRuns().ToArray());
            Assert.AreEqual(2, context.TotalLoads);
        }

        [TestMethod]
        public void Drop_LoadsTheRecordsOffTheUiThread()
        {
            var context = new AggregationContext();
            var records = context.Records(5);

            context.Drop(records);

            Assert.AreEqual(5, context.TotalLoads);
            Assert.IsFalse(context.LoadedOnUiThread, "A dropped record was parsed on the UI thread.");
            Assert.HasCount(5, context.Model.AggregationEntries);
        }

        [TestMethod]
        public void Drop_KeepsTheDropOrderWhenTheLoadsFinishInAnotherOrder()
        {
            var context = new AggregationContext();
            var records = context.Records(4);
            context.LoadDelays[records[0].FullPath] = TimeSpan.FromMilliseconds(300);
            context.LoadDelays[records[1].FullPath] = TimeSpan.FromMilliseconds(150);

            context.Drop(records);

            CollectionAssert.AreEqual(records, context.Model.AggregationEntries.Select(entry => entry.FileRecordInfo).ToArray());
            context.ResetCalls();
            context.Model.AggregateIncludeCommand.Execute(null);
            CollectionAssert.AreEqual(records.SelectMany(context.FrametimesOf).ToArray(),
                context.MetricCalls.Single().Frametimes.ToArray());
        }

        [TestMethod]
        public void Drop_RunsTheOutlierAnalysisOncePerDrop()
        {
            var context = new AggregationContext();

            context.Drop(context.Records(4));
            context.Drop(context.Records(2, firstIndex: 4));

            CollectionAssert.AreEqual(new[] { 4, 6 }, context.OutlierCalls.Select(call => call.Analyses.Count).ToArray());
        }

        [TestMethod]
        public void Drop_OfASingleRecord_AddsIt()
        {
            var context = new AggregationContext();
            var records = context.Records(2);

            context.DropData(records[0]);
            context.DropData(records[1]);

            CollectionAssert.AreEqual(records, context.Model.AggregationEntries.Select(entry => entry.FileRecordInfo).ToArray());
            Assert.HasCount(1, context.OutlierCalls);
        }

        [TestMethod]
        public void Drop_LeavesOutRecordsOfAnotherProcess()
        {
            var context = new AggregationContext();
            var game = context.Records(2);
            var other = context.Records(2, firstIndex: 2, processName: "other.exe");

            context.Drop(new[] { game[0], other[0], game[1] });

            CollectionAssert.AreEqual(game, context.Model.AggregationEntries.Select(entry => entry.FileRecordInfo).ToArray());

            context.ResetCalls();
            context.Drop(new[] { other[1] });

            Assert.HasCount(2, context.Model.AggregationEntries);
            Assert.AreEqual(0, context.LoadCount(other[1]), "A record of another process was parsed only to be rejected.");
        }

        [TestMethod]
        public void Drop_TheFirstRecordThatLoadsDecidesTheProcess()
        {
            var context = new AggregationContext();
            var other = context.Records(1, processName: "other.exe");
            var game = context.Records(2, firstIndex: 1);
            context.Sessions[other[0].FullPath] = null;

            context.Drop(new[] { other[0], game[0], game[1] });

            CollectionAssert.AreEqual(game, context.Model.AggregationEntries.Select(entry => entry.FileRecordInfo).ToArray());
        }

        [TestMethod]
        public void Drop_LogsARecordThatFailsToLoadAndAddsTheOthers()
        {
            var context = new AggregationContext();
            var records = context.Records(3);
            context.FailingPaths.Add(records[1].FullPath);

            context.Drop(records);

            CollectionAssert.AreEqual(new[] { records[0], records[2] },
                context.Model.AggregationEntries.Select(entry => entry.FileRecordInfo).ToArray());
            context.Logger.Verify(logger => logger.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<IOException>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
        }

        [TestMethod]
        public void ClearTable_ForgetsTheKeptRecords()
        {
            var context = new AggregationContext();
            context.Drop(context.Records(2));

            context.Model.ClearTableCommand.Execute(null);
            var other = context.Records(2, firstIndex: 2, processName: "other.exe");
            context.Drop(other);
            context.ResetCalls();
            context.Model.AggregateIncludeCommand.Execute(null);

            CollectionAssert.AreEqual(other, context.Model.AggregationEntries.Select(entry => entry.FileRecordInfo).ToArray());
            CollectionAssert.AreEqual(other.SelectMany(context.FrametimesOf).ToArray(),
                context.MetricCalls.Single().Frametimes.ToArray());
        }

        [TestMethod]
        public void SelectedSession_IsAddedFromTheSessionWithoutLoadingIt()
        {
            var context = new AggregationContext();
            var records = context.Records(2);
            context.Model.OnNavigatedTo(null);

            foreach (var record in records)
                context.Select(record);
            context.Model.SelectedSecondMetric = EMetric.P5;
            context.Model.AggregateIncludeCommand.Execute(null);
            context.SavedRuns();

            CollectionAssert.AreEqual(records, context.Model.AggregationEntries.Select(entry => entry.FileRecordInfo).ToArray());
            Assert.AreEqual(2, context.TotalLoads, "Only writing the aggregate may read the records.");
        }

        [TestMethod]
        public void SelectedSession_IsIgnoredWhileTheViewIsNotShown()
        {
            var context = new AggregationContext();
            var records = context.Records(1);
            context.Model.OnNavigatedTo(null);
            context.Model.OnNavigatedFrom(null);

            context.Select(records[0]);

            Assert.IsEmpty(context.Model.AggregationEntries);
        }

        private sealed class AggregationContext
        {
            private readonly TaskCompletionSource<IReadOnlyList<ISessionRun>> _saved =
                new TaskCompletionSource<IReadOnlyList<ISessionRun>>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly ConcurrentDictionary<string, int> _loads = new ConcurrentDictionary<string, int>();
            private readonly ConcurrentBag<int> _loadThreads = new ConcurrentBag<int>();
            private readonly int _uiThread = Environment.CurrentManagedThreadId;
            private readonly IEventAggregator _eventAggregator = new EventAggregator();

            public AggregationContext()
            {
                var recordManager = new Mock<IRecordManager>();
                recordManager.Setup(manager => manager.LoadData(It.IsAny<string>()))
                    .Returns((string path) =>
                    {
                        _loads.AddOrUpdate(path, 1, (key, count) => count + 1);
                        _loadThreads.Add(Environment.CurrentManagedThreadId);
                        if (LoadDelays.TryGetValue(path, out var delay))
                            Thread.Sleep(delay);
                        if (FailingPaths.Contains(path))
                            throw new IOException("The record cannot be read.");
                        return Sessions.TryGetValue(path, out var session) ? session : null;
                    });
                recordManager.Setup(manager => manager.SaveSessionRunsToFile(It.IsAny<IEnumerable<ISessionRun>>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ISessionInfo>>()))
                    .Callback((IEnumerable<ISessionRun> runs, string process, string comment, string directory,
                        List<ISessionInfo> info) => _saved.TrySetResult(runs.ToList()))
                    .ReturnsAsync(true);

                var statistics = new Mock<IStatisticProvider>();
                statistics.Setup(provider => provider.GetMetricAnalysis(It.IsAny<IList<double>>(),
                        It.IsAny<IList<double>>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns((IList<double> frametimes, IList<double> displaytimes, bool useDisplayChange,
                        string second, string third) =>
                    {
                        MetricCalls.Add((frametimes, displaytimes, second, third));
                        return Mock.Of<IMetricAnalysis>(analysis => analysis.ResultString == "result");
                    });
                statistics.Setup(provider => provider.GetOutlierAnalysis(It.IsAny<IList<IMetricAnalysis>>(),
                        It.IsAny<string>(), It.IsAny<int>()))
                    .Returns((IList<IMetricAnalysis> analyses, string metric, int percentage) =>
                    {
                        OutlierCalls.Add((analyses.ToList(), metric, percentage));
                        return OutlierFlags ?? new bool[analyses.Count];
                    });

                var configuration = new Mock<IAppConfiguration>();
                configuration.SetupAllProperties();
                configuration.Object.SecondMetricAggregation = "P1";
                configuration.Object.ThirdMetricAggregation = "P0dot2";
                configuration.Object.RelatedMetricAggregation = "Second";
                configuration.Object.OutlierPercentageAggregation = 3;

                Model = new AggregationViewModel(statistics.Object, _eventAggregator,
                    configuration.Object, recordManager.Object, Logger.Object);
            }

            public AggregationViewModel Model { get; }
            public Mock<ILogger<AggregationViewModel>> Logger { get; } = new Mock<ILogger<AggregationViewModel>>();
            public Dictionary<string, ISession> Sessions { get; } = new Dictionary<string, ISession>();
            public Dictionary<string, TimeSpan> LoadDelays { get; } = new Dictionary<string, TimeSpan>();
            public HashSet<string> FailingPaths { get; } = new HashSet<string>();
            public bool[] OutlierFlags { get; set; }

            public List<(IList<double> Frametimes, IList<double> Displaytimes, string Second, string Third)> MetricCalls { get; }
                = new List<(IList<double>, IList<double>, string, string)>();

            public List<(IList<IMetricAnalysis> Analyses, string Metric, int Percentage)> OutlierCalls { get; }
                = new List<(IList<IMetricAnalysis>, string, int)>();

            public int TotalLoads => _loads.Values.Sum();

            public bool LoadedOnUiThread => _loadThreads.Contains(_uiThread);

            public int LoadCount(IFileRecordInfo record) => _loads.TryGetValue(record.FullPath, out int count) ? count : 0;

            public void ResetCalls()
            {
                _loads.Clear();
                MetricCalls.Clear();
                OutlierCalls.Clear();
            }

            /// <summary>
            /// Records with their own frame and display times, so each entry shows which samples
            /// it was computed from.
            /// </summary>
            public IFileRecordInfo[] Records(int count, int firstIndex = 0, int runsPerRecord = 1,
                string processName = "game.exe")
                => Enumerable.Range(firstIndex, count).Select(index =>
                {
                    var record = new Mock<IFileRecordInfo>();
                    record.SetupGet(info => info.ProcessName).Returns(processName);
                    record.SetupGet(info => info.GameName).Returns("Game");
                    record.SetupGet(info => info.FullPath).Returns($"record-{index}.json");
                    var runs = Enumerable.Range(0, runsPerRecord).Select(run =>
                    {
                        var sessionRun = RecordFiles.Run(Enumerable.Repeat(10d + index + run / 10d, 20).ToArray());
                        sessionRun.CaptureData.MsBetweenDisplayChange = Enumerable.Repeat(30d + index + run / 10d, 20).ToArray();
                        return (ISessionRun)sessionRun;
                    }).ToArray();
                    Sessions[record.Object.FullPath] = RecordFiles.SessionWith("Game", runs);
                    return record.Object;
                }).ToArray();

            public ISessionRun RunOf(IFileRecordInfo record) => Sessions[record.FullPath].Runs.Single();

            public double[] FrametimesOf(IFileRecordInfo record)
                => Sessions[record.FullPath].Runs.SelectMany(run => run.CaptureData.MsBetweenPresents).ToArray();

            public double[] DisplaytimesOf(IFileRecordInfo record)
                => Sessions[record.FullPath].Runs.SelectMany(run => run.CaptureData.MsBetweenDisplayChange).ToArray();

            public IReadOnlyList<ISessionRun> SavedRuns()
            {
                Assert.IsTrue(_saved.Task.Wait(TimeSpan.FromSeconds(5)), "No aggregate was written.");
                return _saved.Task.Result;
            }

            public void Drop(IFileRecordInfo[] records) => DropData(new ArrayList(records));

            /// <summary>Drops like the data grid does and waits until the records are added.</summary>
            public void DropData(object data)
            {
                var dropInfo = new Mock<IDropInfo>();
                dropInfo.SetupGet(info => info.Data).Returns(data);
                dropInfo.SetupGet(info => info.VisualTarget).Returns(new DataGrid { Name = "AggregationItemDataGrid" });
                RecordFiles.RunOnDispatcher(() =>
                {
                    ((IDropTarget)Model).Drop(dropInfo.Object);
                    return Model.PendingDrop;
                });
            }

            public void Select(IFileRecordInfo record)
                => _eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>()
                    .Publish(new ViewMessages.SelectSession(Sessions[record.FullPath], record));
        }
    }
}
