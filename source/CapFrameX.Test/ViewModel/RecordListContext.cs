using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Subjects;
using System.Threading;
using System.Windows.Threading;
using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
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
    /// The record list on a dispatcher thread, fed by a stub folder observer and reading real
    /// record files. The file access delay and the quiet time of changed files run on a virtual
    /// clock, the batch window of the list too when asked to.
    /// </summary>
    internal sealed class RecordListContext : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly SynchronizationContext _previousContext = SynchronizationContext.Current;
        private readonly Subject<FileInfo> _created = new Subject<FileInfo>();
        private readonly Subject<FileInfo> _changed = new Subject<FileInfo>();
        private readonly BehaviorSubject<IEnumerable<FileInfo>> _listing;

        /// <param name="listing">The files of the first listing; all record files of the folder by default.</param>
        /// <param name="batchWindowOnClock">Whether the batch window of the list runs on <see cref="Clock"/>.</param>
        public RecordListContext(string folder, IEnumerable<FileInfo> listing = null, bool waitForList = true,
            bool batchWindowOnClock = false)
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));

            var files = (listing ?? new DirectoryInfo(folder).GetFiles("*.json")).ToArray();
            _listing = new BehaviorSubject<IEnumerable<FileInfo>>(files);
            var observer = new Mock<IRecordDirectoryObserver>();
            observer.SetupGet(value => value.FileCreatedStream).Returns(_created);
            observer.SetupGet(value => value.FileDeletedStream).Returns(new Subject<FileInfo>());
            observer.SetupGet(value => value.FileChangedStream).Returns(_changed);
            observer.SetupGet(value => value.DirectoryFilesStream).Returns(_listing);
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
            // The listing reaches the list through the dispatcher, after these are set.
            Model = new ControlViewModel(observer.Object, events, configuration.Object, pathService.Object,
                recordManager, Mock.Of<ISystemInfo>(), null, NullLogger<ControlViewModel>.Instance,
                new ApplicationState(), Queue)
            {
                FileChangeScheduler = Clock,
                RecordListScheduler = batchWindowOnClock ? Clock : DefaultScheduler.Instance
            };

            if (waitForList)
                PumpUntil(() => Model.RecordInfoList.Count == files.Length && !Model.DirectoryLoading, "the record list");
        }

        public HistoricalScheduler Clock { get; } = new HistoricalScheduler();
        public RecordWriteQueue Queue { get; } = new RecordWriteQueue(null);
        public ControlViewModel Model { get; }
        public List<ViewMessages.UpdateSession> LoadedSessions { get; } = new List<ViewMessages.UpdateSession>();
        public List<IFileRecordInfo> RecordInfoUpdates { get; } = new List<IFileRecordInfo>();

        public string[] ListedNames => Model.RecordInfoList.Select(record => Path.GetFileName(record.FullPath)).ToArray();

        /// <summary>A capture with its own hash and the given metadata.</summary>
        public static string WriteRecord(string folder, string name, string comment = "comment",
            string hash = null, string cpu = "CPU", string gameName = "Game")
        {
            var session = RecordFiles.SessionWith("Game", RecordFiles.Run());
            session.Info.GameName = gameName;
            session.Hash = hash ?? "hash-" + name;
            session.Info.Processor = cpu;
            session.Info.GPU = "GPU";
            session.Info.SystemRam = "RAM";
            session.Info.Motherboard = "Board";
            session.Info.Comment = comment;
            string path = Path.Combine(folder, name);
            File.WriteAllText(path, JsonConvert.SerializeObject(session));
            return path;
        }

        /// <summary>Keeps every other reader out of the file until disposed.</summary>
        public static IDisposable BlockReading(string path)
            => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);

        /// <summary>Selects the records like the list does and waits for the first one to load.</summary>
        public ISession SelectAndLoad(params IFileRecordInfo[] records)
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

        /// <summary>A new listing of the folder, as after a refresh or a folder switch.</summary>
        public void ShowListing(IEnumerable<FileInfo> files) => _listing.OnNext(files.ToArray());

        /// <summary>What the folder observer reports when a file is created.</summary>
        public void ReportCreated(string path) => _created.OnNext(new FileInfo(path));

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

        /// <summary>
        /// Lets the work that is due run: reads without delay on the clock and what they post
        /// to the UI thread. The clock moves by ticks only, so no delay or window elapses.
        /// </summary>
        public void RunDueWork()
        {
            for (int round = 0; round < 20; round++)
            {
                Clock.AdvanceBy(TimeSpan.FromTicks(1));
                Pump();
                Thread.Sleep(5);
            }
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

        public static void Pump()
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
