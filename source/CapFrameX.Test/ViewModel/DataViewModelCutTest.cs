using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// The cut buttons of the Analysis page: a cut waits for the record write queue, and the buttons
    /// stay disabled until it is written, so a second click is not lost.
    /// </summary>
    [STATestClass]
    public class DataViewModelCutTest
    {
        private string _folder;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.DataViewModelCutTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public async Task CutButtons_AreDisabledUntilTheCutIsWritten()
        {
            var queue = new RecordWriteQueue(null);
            using var metadataWrite = new ManualResetEventSlim();
            // A metadata edit is still being written, so the cut has to wait for it.
            _ = queue.Enqueue(() => metadataWrite.Wait());
            var viewModel = CreateViewModel(queue);

            // The inverse cut of the empty range 0 to 0 keeps the whole record.
            viewModel.CutRecordInverseCommand.Execute();

            Assert.IsFalse(viewModel.CutRecordCommand.CanExecute());
            Assert.IsFalse(viewModel.CutRecordInverseCommand.CanExecute());
            Assert.AreEqual(0, Directory.GetFiles(_folder, "*.json").Length);

            metadataWrite.Set();
            var waited = Stopwatch.StartNew();
            while (!viewModel.CutRecordCommand.CanExecute() && waited.Elapsed < TimeSpan.FromSeconds(30))
                await Task.Delay(10);

            Assert.IsTrue(viewModel.CutRecordCommand.CanExecute());
            Assert.IsTrue(viewModel.CutRecordInverseCommand.CanExecute());
            Assert.AreEqual(1, Directory.GetFiles(_folder, "*.json").Length);
        }

        private DataViewModel CreateViewModel(RecordWriteQueue queue)
        {
            var observer = new Mock<IRecordDirectoryObserver>();
            observer.SetupGet(o => o.ObservingDirectoryStream).Returns(Observable.Return(new DirectoryInfo(_folder)));
            var recordManager = new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, observer.Object, null, null, null, null, null, new EventAggregator(), null, null);
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupAllProperties();
            var viewModel = new DataViewModel(Mock.Of<IStatisticProvider>(), Mock.Of<IFrametimeAnalyzer>(),
                new EventAggregator(), configuration.Object, recordManager, queue, NullLogger<DataViewModel>.Instance);

            var session = RecordFiles.SessionWith("Game", RecordFiles.Run(Enumerable.Repeat(10d, 500).ToArray()));
            session.Info.ProcessName = "Game.exe";
            var server = (LocalRecordDataServer)typeof(DataViewModel)
                .GetField("_localRecordDataServer", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(viewModel);
            server.CurrentSession = session;
            return viewModel;
        }
    }
}
