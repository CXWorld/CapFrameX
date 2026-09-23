using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.ViewModel;
using GongSolutions.Wpf.DragDrop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    [STATestClass]
    public class RecordDragDropTest
    {
        [TestMethod]
        [DataRow(typeof(ComparisonViewModel), 1)]
        [DataRow(typeof(ComparisonViewModel), 3)]
        [DataRow(typeof(ReportViewModel), 1)]
        [DataRow(typeof(ReportViewModel), 3)]
        public void ResolveRecords_DefaultDragHandlerPayload_PreservesAllSelectedRecords(
            Type viewModelType, int recordCount)
        {
            var records = CreateRecords(recordCount);
            object payload = CreateDragPayload(records);
            var configuration = CreateConfiguration();
            object viewModel = viewModelType == typeof(ComparisonViewModel)
                ? (object)new ComparisonViewModel(null, null, new EventAggregator(), configuration, null, null)
                : new ReportViewModel(null, new EventAggregator(), configuration, null, null);

            var method = viewModelType.GetMethod("GetDroppedRecordInfosAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var resolvedRecords = ((Task<List<IFileRecordInfo>>)method.Invoke(viewModel, new[] { payload }))
                .GetAwaiter().GetResult();

            CollectionAssert.AreEqual(records, resolvedRecords.ToArray());
        }

        [TestMethod]
        [DataRow("AggregationItemDataGrid", 1)]
        [DataRow("AggregationItemDataGrid", 3)]
        [DataRow("DragAndDropInfoTextTextBlock", 1)]
        [DataRow("DragAndDropInfoTextTextBlock", 3)]
        public void AggregationDrop_DefaultDragHandlerPayload_AddsAllSelectedRecords(
            string targetName, int recordCount)
        {
            var records = CreateRecords(recordCount);
            var session = new Mock<ISession>();
            session.SetupGet(value => value.Runs).Returns(new List<ISessionRun>());
            var recordManager = new Mock<IRecordManager>();
            recordManager.Setup(value => value.LoadData(It.IsAny<string>())).Returns(session.Object);
            var statistics = new Mock<IStatisticProvider>();
            statistics.Setup(value => value.GetMetricAnalysis(
                It.IsAny<IList<double>>(), It.IsAny<IList<double>>(), It.IsAny<bool>(),
                It.IsAny<string>(), It.IsAny<string>())).Returns(Mock.Of<IMetricAnalysis>());
            statistics.Setup(value => value.GetOutlierAnalysis(
                It.IsAny<IList<IMetricAnalysis>>(), It.IsAny<string>(), It.IsAny<int>()))
                .Returns((IList<IMetricAnalysis> analyses, string relatedMetric, int percentage)
                    => new bool[analyses.Count]);
            var viewModel = new AggregationViewModel(statistics.Object, new EventAggregator(),
                CreateConfiguration(), recordManager.Object);
            var dropInfo = new Mock<IDropInfo>();
            dropInfo.SetupGet(value => value.Data).Returns(CreateDragPayload(records));
            dropInfo.SetupGet(value => value.VisualTarget).Returns(targetName == "AggregationItemDataGrid"
                ? (System.Windows.UIElement)new DataGrid { Name = targetName }
                : new TextBlock { Name = targetName });

            ((IDropTarget)viewModel).Drop(dropInfo.Object);

            CollectionAssert.AreEqual(records,
                viewModel.AggregationEntries.Select(entry => entry.FileRecordInfo).ToArray());
        }

        private static object CreateDragPayload(IFileRecordInfo[] records)
        {
            // Use Gong's actual source handler so changes to its multi-selection payload are covered.
            var dragInfo = new Mock<IDragInfo>();
            dragInfo.SetupAllProperties();
            dragInfo.SetupGet(value => value.SourceItems).Returns(new ArrayList(records));
            new DefaultDragHandler().StartDrag(dragInfo.Object);
            return dragInfo.Object.Data;
        }

        private static IFileRecordInfo[] CreateRecords(int count)
        {
            return Enumerable.Range(0, count).Select(index =>
            {
                var record = new Mock<IFileRecordInfo>();
                record.SetupGet(value => value.ProcessName).Returns("game.exe");
                record.SetupGet(value => value.FullPath).Returns($"record-{index}.json");
                return record.Object;
            }).ToArray();
        }

        private static IAppConfiguration CreateConfiguration()
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupAllProperties();
            var settings = configuration.Object;
            settings.ComparisonContext = "DateTime";
            settings.SecondComparisonContext = "None";
            settings.ComparisonFirstMetric = "Average";
            settings.ComparisonSecondMetric = "P1";
            settings.ComparisonThirdMetric = "P0dot2";
            settings.FirstMetricBarColor = "#336699";
            settings.SecondMetricBarColor = "#669933";
            settings.ThirdMetricBarColor = "#993366";
            settings.ComparisonLineGraphColors = new List<string>();
            settings.SecondMetricAggregation = "P1";
            settings.ThirdMetricAggregation = "P0dot2";
            return settings;
        }
    }
}
