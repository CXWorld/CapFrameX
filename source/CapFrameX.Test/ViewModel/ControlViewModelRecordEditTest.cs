using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Windows.Threading;
using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
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
    /// Metadata edits in the record list: the files are rewritten off the UI thread, the Changed
    /// events of a rewrite are read once per file, and an entry whose hash did not change takes
    /// the new metadata in place instead of the record being loaded and analysed again.
    /// </summary>
    [STATestClass]
    public class ControlViewModelRecordEditTest
    {
        private string _folder;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.ControlViewModelRecordEditTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void MetadataEdit_UpdatesTheEntryInPlaceWithoutLoadingTheRecordAgain()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new ControlContext(_folder);
            var entry = context.Model.RecordInfoList.Single();
            var session = context.SelectAndLoad(entry);

            context.Model.CustomComment = "after";
            context.Save();
            context.ReportChanges(path, path, path);
            context.SettleUntil(() => context.RecordInfoUpdates.Any());

            Assert.AreSame(entry, context.Model.RecordInfoList.Single());
            Assert.AreSame(entry, context.Model.SelectedRecordInfo);
            Assert.AreEqual("after", entry.Comment);
            Assert.AreEqual("after", session.Info.Comment, "The loaded session keeps the old metadata.");
            Assert.HasCount(1, context.LoadedSessions, "The record was loaded again.");
            CollectionAssert.AreEqual(new[] { entry }, context.RecordInfoUpdates);
        }

        [TestMethod]
        public void ChangedEventsOfOneRewrite_AreReadOnce()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new ControlContext(_folder);
            context.SelectAndLoad(context.Model.RecordInfoList.Single());
            context.Model.CustomComment = "after";
            context.Save();

            // Each event comes before the file has been quiet long enough.
            for (int index = 0; index < 4; index++)
            {
                context.ReportChanges(path);
                context.Advance(TimeSpan.FromMilliseconds(300));
            }
            context.SettleUntil(() => context.RecordInfoUpdates.Any());

            Assert.HasCount(1, context.RecordInfoUpdates);
        }

        [TestMethod]
        public void ChangesOfTwoRecords_AreReadSeparately()
        {
            string first = WriteRecord("a.json", "a");
            string second = WriteRecord("b.json", "b", hash: "hash-b");
            using var context = new ControlContext(_folder);
            var entries = context.Model.RecordInfoList.OrderBy(entry => entry.FullPath).ToArray();
            context.SelectAndLoad(entries[0]);
            context.Model.CustomComment = "a edited";
            context.Save();
            WriteRecord("b.json", "b edited elsewhere", hash: "hash-b");

            context.ReportChanges(first, second, first, second);
            context.SettleUntil(() => entries[1].Comment == "b edited elsewhere" && context.RecordInfoUpdates.Any());

            Assert.AreEqual("a edited", entries[0].Comment);
            CollectionAssert.AreEqual(entries, context.Model.RecordInfoList.OrderBy(entry => entry.FullPath).ToArray());
        }

        [TestMethod]
        public void EditByAnotherWriter_ReloadsTheSelectedRecord()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new ControlContext(_folder);
            var entry = context.Model.RecordInfoList.Single();
            context.SelectAndLoad(entry);

            // Same capture, new comment, but the loaded session was not updated by this edit.
            WriteRecord("a.json", "edited elsewhere");
            context.ReportChanges(path);
            context.SettleUntil(() => context.LoadedSessions.Count == 2);

            Assert.AreSame(entry, context.Model.SelectedRecordInfo);
            Assert.AreEqual("edited elsewhere", entry.Comment);
            Assert.AreEqual("edited elsewhere", context.Model.CustomComment);
            Assert.AreEqual("edited elsewhere", context.LoadedSessions[1].CurrentSession.Info.Comment);
            CollectionAssert.AreEqual(new[] { entry }, context.RecordInfoUpdates);
        }

        [TestMethod]
        public void NewCaptureUnderTheSamePath_ReplacesTheEntry()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new ControlContext(_folder);
            var entry = context.Model.RecordInfoList.Single();
            context.SelectAndLoad(entry);

            WriteRecord("a.json", "another capture", hash: "other hash");
            context.ReportChanges(path);
            context.SettleUntil(() => context.LoadedSessions.Count == 2);

            var replacement = context.Model.RecordInfoList.Single();
            Assert.AreNotSame(entry, replacement);
            Assert.AreSame(replacement, context.Model.SelectedRecordInfo);
            Assert.AreEqual("other hash", context.LoadedSessions[1].CurrentSession.Hash);
        }

        [TestMethod]
        public void OwnEdit_KeepsWhatIsBeingTypedInAnotherField()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new ControlContext(_folder);
            context.SelectAndLoad(context.Model.RecordInfoList.Single());
            context.Model.CustomComment = "after";
            context.Save();

            context.Model.CustomCpuDescription = "typing";
            context.ReportChanges(path);
            context.SettleUntil(() => context.RecordInfoUpdates.Any());

            Assert.AreEqual("typing", context.Model.CustomCpuDescription);
            Assert.AreEqual("after", context.Model.CustomComment);
        }

        [TestMethod]
        public void SaveDescriptions_WritesTheRecordOffTheUiThread()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new ControlContext(_folder);
            context.SelectAndLoad(context.Model.RecordInfoList.Single());
            using var gate = new ManualResetEventSlim();
            context.Queue.Enqueue(() => gate.Wait(TimeSpan.FromSeconds(10)));

            context.Model.CustomComment = "after";
            context.Model.SaveDescriptions();

            Assert.AreEqual("before", ReadComment(path), "The record was written on the UI thread.");
            gate.Set();
            Assert.IsTrue(context.Queue.WaitForPendingWrites(TimeSpan.FromSeconds(10)));
            Assert.AreEqual("after", ReadComment(path));
        }

        [TestMethod]
        public void EditOfSeveralRecords_WritesTheEditedFieldAndKeepsTheirOwnValues()
        {
            string first = WriteRecord("a.json", "a", cpu: "CPU A");
            string second = WriteRecord("b.json", "b", cpu: "CPU B", hash: "hash-b");
            using var context = new ControlContext(_folder);
            var entries = context.Model.RecordInfoList.OrderBy(entry => entry.FullPath).ToArray();
            context.SelectAndLoad(entries);

            context.Model.CustomComment = "both";
            context.Save();

            Assert.AreEqual("both", ReadComment(first));
            Assert.AreEqual("both", ReadComment(second));
            Assert.AreEqual("CPU A", ReadSession(first).Info.Processor);
            Assert.AreEqual("CPU B", ReadSession(second).Info.Processor);
        }

        [TestMethod]
        public void EditOfSeveralRecords_SkipsARecordThatCannotBeRead()
        {
            string first = WriteRecord("a.json", "a");
            string second = WriteRecord("b.json", "b", hash: "hash-b");
            using var context = new ControlContext(_folder);
            var entries = context.Model.RecordInfoList.OrderBy(entry => entry.FullPath).ToArray();
            context.SelectAndLoad(entries);
            File.Delete(first);

            context.Model.CustomComment = "both";
            context.Save();

            Assert.IsFalse(File.Exists(first));
            Assert.AreEqual("both", ReadComment(second));
        }

        private string WriteRecord(string name, string comment, string hash = "hash", string cpu = "CPU")
        {
            var session = RecordFiles.SessionWith("Game", RecordFiles.Run());
            session.Hash = hash;
            session.Info.Processor = cpu;
            session.Info.GPU = "GPU";
            session.Info.SystemRam = "RAM";
            session.Info.Motherboard = "Board";
            session.Info.Comment = comment;
            string path = Path.Combine(_folder, name);
            File.WriteAllText(path, JsonConvert.SerializeObject(session));
            return path;
        }

        private static CapFrameX.Data.Session.Contracts.ISession ReadSession(string path)
            => new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null, new EventAggregator(), null, null).LoadData(path);

        private static string ReadComment(string path) => ReadSession(path).Info.Comment;

        /// <summary>
        /// The record list on a dispatcher thread, fed by a stub folder observer. The file
        /// access delay and the quiet time of changed files run on a virtual clock.
        /// </summary>
        private sealed class ControlContext : IDisposable
        {
            private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

            private readonly SynchronizationContext _previousContext = SynchronizationContext.Current;
            private readonly Subject<FileInfo> _changed = new Subject<FileInfo>();

            public ControlContext(string folder)
            {
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

                var files = new DirectoryInfo(folder).GetFiles("*.json");
                var observer = new Mock<IRecordDirectoryObserver>();
                observer.SetupGet(value => value.FileCreatedStream).Returns(Observable.Never<FileInfo>());
                observer.SetupGet(value => value.FileDeletedStream).Returns(Observable.Never<FileInfo>());
                observer.SetupGet(value => value.FileChangedStream).Returns(_changed);
                observer.SetupGet(value => value.DirectoryFilesStream)
                    .Returns(new BehaviorSubject<IEnumerable<FileInfo>>(files));
                observer.SetupGet(value => value.ObservingDirectoryStream)
                    .Returns(new BehaviorSubject<DirectoryInfo>(new DirectoryInfo(folder)));

                var events = new EventAggregator();
                events.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
                    .Subscribe(LoadedSessions.Add, ThreadOption.PublisherThread, true);
                events.GetEvent<PubSubEvent<ViewMessages.UpdateRecordInfos>>()
                    .Subscribe(message => RecordInfoUpdates.Add(message.RecordInfo), ThreadOption.PublisherThread, true);

                var configuration = new Mock<IAppConfiguration>();
                configuration.SetupAllProperties();
                configuration.Object.ObservedDirectory = folder;
                var pathService = new Mock<IPathService>();
                pathService.Setup(service => service.ResolveDocumentsPlaceholder(It.IsAny<string>()))
                    .Returns((string path) => path);

                var recordManager = new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                    null, null, null, null, null, null, null, new EventAggregator(), null, null)
                {
                    FileAccessScheduler = Clock
                };
                Model = new ControlViewModel(observer.Object, events, configuration.Object, pathService.Object,
                    recordManager, Mock.Of<ISystemInfo>(), null, NullLogger<ControlViewModel>.Instance,
                    new ApplicationState(), Queue)
                {
                    FileChangeScheduler = Clock
                };

                PumpUntil(() => Model.RecordInfoList.Count == files.Length && !Model.DirectoryLoading, "the record list");
            }

            public HistoricalScheduler Clock { get; } = new HistoricalScheduler();
            public RecordWriteQueue Queue { get; } = new RecordWriteQueue(null);
            public ControlViewModel Model { get; }
            public List<ViewMessages.UpdateSession> LoadedSessions { get; } = new List<ViewMessages.UpdateSession>();
            public List<IFileRecordInfo> RecordInfoUpdates { get; } = new List<IFileRecordInfo>();

            /// <summary>Selects the records like the list does and waits for the first one to load.</summary>
            public CapFrameX.Data.Session.Contracts.ISession SelectAndLoad(params IFileRecordInfo[] records)
            {
                int loaded = LoadedSessions.Count;
                Model.SelectedRecordInfos = records.ToList();
                Model.SelectedRecordInfo = records[0];
                PumpUntil(() => LoadedSessions.Count > loaded, "the selected record");
                return LoadedSessions.Last().CurrentSession;
            }

            public void Save()
            {
                Model.SaveDescriptions();
                Assert.IsTrue(Queue.WaitForPendingWrites(Timeout), "The record was not written.");
            }

            /// <summary>What the folder observer reports while a file is rewritten.</summary>
            public void ReportChanges(params string[] paths)
            {
                foreach (var path in paths)
                    _changed.OnNext(new FileInfo(path));
            }

            public void Advance(TimeSpan time)
            {
                Clock.AdvanceBy(time);
                Pump();
            }

            /// <summary>Lets the files become quiet, then waits for the UI to catch up.</summary>
            public void SettleUntil(Func<bool> condition)
            {
                Advance(TimeSpan.FromSeconds(1));
                PumpUntil(condition, "the changed records");
                // Anything else that is still on its way arrives too.
                Advance(TimeSpan.FromSeconds(1));
                Thread.Sleep(100);
                Pump();
            }

            public void PumpUntil(Func<bool> condition, string what)
            {
                var deadline = DateTime.UtcNow + Timeout;
                while (!condition())
                {
                    if (DateTime.UtcNow > deadline)
                        Assert.Fail($"Timed out waiting for {what}.");
                    // Work due now on the virtual clock (a read without delay) runs too.
                    Clock.AdvanceBy(TimeSpan.FromTicks(1));
                    Pump();
                    Thread.Sleep(5);
                }
            }

            private static void Pump()
            {
                for (int round = 0; round < 10; round++)
                    Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.Background, new Action(() => { }));
            }

            public void Dispose()
            {
                Queue.WaitForPendingWrites(Timeout);
                SynchronizationContext.SetSynchronizationContext(_previousContext);
            }
        }
    }
}
