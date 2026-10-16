using System.Linq;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// A metadata edit no longer loads the record again, so the pages that show its metadata
    /// take it from the record update instead.
    /// </summary>
    [STATestClass]
    public class RecordMetadataDisplayTest
    {
        [TestMethod]
        public void AnalysisPage_ShowsTheEditedMetadataOfItsRecord()
        {
            var events = new EventAggregator();
            var model = new DataViewModel(Mock.Of<IStatisticProvider>(), Mock.Of<IFrametimeAnalyzer>(), events,
                Configuration(), CreateRecordManager(), NullLogger<DataViewModel>.Instance);
            var record = Record("record");
            Show(events, record);

            record.GameName = "Renamed";
            record.Comment = "edited";
            events.GetEvent<PubSubEvent<ViewMessages.UpdateRecordInfos>>().Publish(new ViewMessages.UpdateRecordInfos(record));

            Assert.AreEqual("Renamed", model.CurrentGameName);
            Assert.AreEqual("edited", model.SystemInfos.Single(info => info.Key == "Comment").Value);
            Assert.AreSame(record, model.RecordInfo);
        }

        [TestMethod]
        public void AnalysisPage_IgnoresTheEditOfAnotherRecord()
        {
            var events = new EventAggregator();
            var model = new DataViewModel(Mock.Of<IStatisticProvider>(), Mock.Of<IFrametimeAnalyzer>(), events,
                Configuration(), CreateRecordManager(), NullLogger<DataViewModel>.Instance);
            var record = Record("record");
            Show(events, record);
            var other = Record("other");

            other.GameName = "Renamed";
            events.GetEvent<PubSubEvent<ViewMessages.UpdateRecordInfos>>().Publish(new ViewMessages.UpdateRecordInfos(other));

            Assert.AreSame(record, model.RecordInfo);
            Assert.AreNotEqual("Renamed", model.CurrentGameName);
            Assert.IsNull(model.SystemInfos);
        }

        [TestMethod]
        public void SynchronizationPage_ShowsTheEditedGameNameOfItsRecord()
        {
            var events = new EventAggregator();
            var model = new SynchronizationViewModel(Mock.Of<IStatisticProvider>(), events, Configuration(),
                NullLogger<SynchronizationViewModel>.Instance);
            var record = Record("record");
            Show(events, record);
            var other = Record("other");

            other.GameName = "Other";
            events.GetEvent<PubSubEvent<ViewMessages.UpdateRecordInfos>>().Publish(new ViewMessages.UpdateRecordInfos(other));
            Assert.AreNotEqual("Other", model.CurrentGameName);

            record.GameName = "Renamed";
            events.GetEvent<PubSubEvent<ViewMessages.UpdateRecordInfos>>().Publish(new ViewMessages.UpdateRecordInfos(record));
            Assert.AreEqual("Renamed", model.CurrentGameName);
        }

        private static void Show(IEventAggregator events, IFileRecordInfo record)
            => events.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
                .Publish(new ViewMessages.UpdateSession(new Session(), record));

        private static IFileRecordInfo Record(string id)
        {
            var record = new Mock<IFileRecordInfo>();
            record.SetupAllProperties();
            record.SetupGet(info => info.Id).Returns(id);
            record.Object.GameName = "Game";
            record.Object.Comment = "comment";
            return record.Object;
        }

        private static IAppConfiguration Configuration()
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupAllProperties();
            return configuration.Object;
        }

        private static RecordManager CreateRecordManager()
            => new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null, new EventAggregator(), null, null);
    }
}
