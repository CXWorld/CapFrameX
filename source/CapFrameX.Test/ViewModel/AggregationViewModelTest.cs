using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.ViewModel;
using GongSolutions.Wpf.DragDrop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// The aggregation keeps its records and their table entries in step: outlier flags and the
    /// written aggregate map entries to records by position.
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

        private sealed class AggregationContext
        {
            private readonly TaskCompletionSource<IReadOnlyList<ISessionRun>> _saved =
                new TaskCompletionSource<IReadOnlyList<ISessionRun>>(TaskCreationOptions.RunContinuationsAsynchronously);

            public AggregationContext()
            {
                var recordManager = new Mock<IRecordManager>();
                recordManager.Setup(manager => manager.LoadData(It.IsAny<string>()))
                    .Returns((string path) => Sessions.TryGetValue(path, out var session) ? session : null);
                recordManager.Setup(manager => manager.SaveSessionRunsToFile(It.IsAny<IEnumerable<ISessionRun>>(),
                        It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<List<ISessionInfo>>()))
                    .Callback((IEnumerable<ISessionRun> runs, string process, string comment, string directory,
                        List<ISessionInfo> info) => _saved.TrySetResult(runs.ToList()))
                    .ReturnsAsync(true);

                var statistics = new Mock<IStatisticProvider>();
                statistics.Setup(provider => provider.GetMetricAnalysis(It.IsAny<IList<double>>(),
                        It.IsAny<IList<double>>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()))
                    .Returns(() => Mock.Of<IMetricAnalysis>(analysis => analysis.ResultString == "result"));
                statistics.Setup(provider => provider.GetOutlierAnalysis(It.IsAny<IList<IMetricAnalysis>>(),
                        It.IsAny<string>(), It.IsAny<int>()))
                    .Returns((IList<IMetricAnalysis> analyses, string metric, int percentage)
                        => OutlierFlags ?? new bool[analyses.Count]);

                var configuration = new Mock<IAppConfiguration>();
                configuration.SetupAllProperties();
                configuration.Object.SecondMetricAggregation = "P1";
                configuration.Object.ThirdMetricAggregation = "P0dot2";
                configuration.Object.RelatedMetricAggregation = "Second";
                configuration.Object.OutlierPercentageAggregation = 3;

                Model = new AggregationViewModel(statistics.Object, new EventAggregator(),
                    configuration.Object, recordManager.Object);
            }

            public AggregationViewModel Model { get; }
            public Dictionary<string, ISession> Sessions { get; } = new Dictionary<string, ISession>();
            public bool[] OutlierFlags { get; set; }

            public IFileRecordInfo[] Records(int count)
                => Enumerable.Range(0, count).Select(index =>
                {
                    var record = new Mock<IFileRecordInfo>();
                    record.SetupGet(info => info.ProcessName).Returns("game.exe");
                    record.SetupGet(info => info.GameName).Returns("Game");
                    record.SetupGet(info => info.FullPath).Returns($"record-{index}.json");
                    Sessions[record.Object.FullPath] = RecordFiles.SessionWith("Game", RecordFiles.Run());
                    return record.Object;
                }).ToArray();

            public ISessionRun RunOf(IFileRecordInfo record) => Sessions[record.FullPath].Runs.Single();

            public IReadOnlyList<ISessionRun> SavedRuns()
            {
                Assert.IsTrue(_saved.Task.Wait(TimeSpan.FromSeconds(5)), "No aggregate was written.");
                return _saved.Task.Result;
            }

            public void Drop(IFileRecordInfo[] records)
            {
                var dropInfo = new Mock<IDropInfo>();
                dropInfo.SetupGet(info => info.Data).Returns(new ArrayList(records));
                dropInfo.SetupGet(info => info.VisualTarget).Returns(new DataGrid { Name = "AggregationItemDataGrid" });
                ((IDropTarget)Model).Drop(dropInfo.Object);
            }
        }
    }
}
