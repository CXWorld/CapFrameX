using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// The file event streams of the record folder, driven by a real file system watcher on a
    /// temporary folder. The streams forward events to the current subscribers only: a late
    /// subscriber takes the current files from DirectoryFilesStream.
    /// </summary>
    [TestClass]
    public class RecordDirectoryObserverTest
    {
        private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

        private string _folder;
        private RecordDirectoryObserver _observer;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordDirectoryObserverTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _observer?.Dispose();
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void FileEventsBeforeASubscription_AreNotReplayed()
        {
            // Regression: the streams were unbounded replay subjects, so every file event of the
            // session stayed in memory and each new subscriber received all of them again.
            var observer = CreateObserver();
            using var probe = new EventRecorder(observer);
            string path = Path.Combine(_folder, "a.json");

            File.WriteAllText(path, "{}");
            probe.WaitFor("created", "a.json");
            File.AppendAllText(path, " ");
            probe.WaitFor("changed", "a.json");
            File.Delete(path);
            probe.WaitFor("deleted", "a.json");

            using var late = new EventRecorder(observer);

            Assert.IsEmpty(late.Events);
        }

        [TestMethod]
        public void Subscriber_ReceivesTheEventsOfRecordFiles()
        {
            var observer = CreateObserver();
            using var recorder = new EventRecorder(observer);
            string path = Path.Combine(_folder, "a.json");

            File.WriteAllText(Path.Combine(_folder, "notes.txt"), "not a record");
            File.WriteAllText(path, "{}");
            recorder.WaitFor("created", "a.json");
            File.AppendAllText(path, " ");
            recorder.WaitFor("changed", "a.json");
            File.Move(path, Path.Combine(_folder, "b.json"));
            recorder.WaitFor("created", "b.json");
            File.Delete(Path.Combine(_folder, "b.json"));
            recorder.WaitFor("deleted", "b.json");

            var events = recorder.Events;
            Assert.IsFalse(events.Any(e => e.Name == "notes.txt"), "A file that is no record raised an event.");
            int renamedAway = events.FindIndex(e => e == ("deleted", "a.json"));
            Assert.IsGreaterThan(-1, renamedAway, "The rename did not remove the old name.");
            Assert.IsLessThan(events.IndexOf(("created", "b.json")), renamedAway);
        }

        [TestMethod]
        public void DeletedFile_IsNotKeptAliveByTheObserver()
        {
            var observer = CreateObserver();
            var deleted = new List<WeakReference>();
            using var weak = observer.FileDeletedStream.Subscribe(fileInfo =>
            {
                lock (deleted)
                    deleted.Add(new WeakReference(fileInfo));
            });
            using var recorder = new EventRecorder(observer);
            string path = Path.Combine(_folder, "a.json");
            File.WriteAllText(path, "{}");
            recorder.WaitFor("created", "a.json");

            File.Delete(path);
            recorder.WaitFor("deleted", "a.json");

            // The watcher thread may still hold the file info for a moment after the event.
            var watch = Stopwatch.StartNew();
            bool alive;
            do
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                lock (deleted)
                    alive = deleted.Any(reference => reference.IsAlive);
                if (alive)
                    Thread.Sleep(50);
            }
            while (alive && watch.Elapsed < TimeSpan.FromSeconds(5));

            Assert.HasCount(1, deleted);
            Assert.IsFalse(alive, "The observer keeps the file info of a deleted file.");
        }

        [TestMethod]
        public void LateSubscriber_FindsTheCurrentFilesInTheDirectoryList()
        {
            // Why the event streams need no replay: the directory list follows the events.
            File.WriteAllText(Path.Combine(_folder, "old.json"), "{}");
            var observer = CreateObserver();
            using var probe = new EventRecorder(observer);

            File.WriteAllText(Path.Combine(_folder, "new.csv"), "x");
            probe.WaitFor("created", "new.csv");
            File.Delete(Path.Combine(_folder, "old.json"));
            probe.WaitFor("deleted", "old.json");

            IEnumerable<FileInfo> files = null;
            using (observer.DirectoryFilesStream.Subscribe(value => files = value))
            {
            }

            CollectionAssert.AreEqual(new[] { "new.csv" }, files.Select(file => file.Name).ToArray());
        }

        [TestMethod]
        public void DirectoryList_StaysAsItWasHandedOut()
        {
            // Regression: subscribers got the watcher's own list, which changed under them.
            File.WriteAllText(Path.Combine(_folder, "old.json"), "{}");
            var observer = CreateObserver();
            using var probe = new EventRecorder(observer);
            IEnumerable<FileInfo> files = null;
            using (observer.DirectoryFilesStream.Subscribe(value => files = value))
            {
            }

            File.WriteAllText(Path.Combine(_folder, "new.json"), "{}");
            probe.WaitFor("created", "new.json");
            File.Delete(Path.Combine(_folder, "old.json"));
            probe.WaitFor("deleted", "old.json");

            CollectionAssert.AreEqual(new[] { "old.json" }, files.Select(file => file.Name).ToArray());
        }

        [TestMethod]
        public void ReadingTheDirectoryList_SurvivesAFileCreatedMeanwhile()
        {
            // The record list reads the files one after the other while new captures arrive.
            File.WriteAllText(Path.Combine(_folder, "a.json"), "{}");
            File.WriteAllText(Path.Combine(_folder, "b.json"), "{}");
            var observer = CreateObserver();
            using var probe = new EventRecorder(observer);
            IEnumerable<FileInfo> files = null;
            using (observer.DirectoryFilesStream.Subscribe(value => files = value))
            {
            }
            var read = new List<string>();
            using var reader = files.GetEnumerator();
            Assert.IsTrue(reader.MoveNext());
            read.Add(reader.Current.Name);

            File.WriteAllText(Path.Combine(_folder, "c.json"), "{}");
            probe.WaitFor("created", "c.json");
            while (reader.MoveNext())
                read.Add(reader.Current.Name);

            CollectionAssert.AreEquivalent(new[] { "a.json", "b.json" }, read);
        }

        [TestMethod]
        public void RenamingATempFileTheListingLeftOut_ReportsTheRecord()
        {
            // Regression: the old name was looked up with First, which threw on the watcher's
            // thread for a temp file the listing had left out.
            File.WriteAllText(Path.Combine(_folder, "other.json"), "{}");
            File.WriteAllText(Path.Combine(_folder, "capture.json.tmp"), "{}");
            var observer = CreateObserver();
            using var probe = new EventRecorder(observer);

            File.Move(Path.Combine(_folder, "capture.json.tmp"), Path.Combine(_folder, "capture.json"));
            probe.WaitFor("created", "capture.json");
            File.WriteAllText(Path.Combine(_folder, "next.json"), "{}");
            probe.WaitFor("created", "next.json");

            CollectionAssert.AreEquivalent(new[] { "other.json", "capture.json", "next.json" }, Listing(observer));
        }

        [TestMethod]
        public void RenamingIntoAFolderWithoutRecords_ReportsTheRecord()
        {
            File.WriteAllText(Path.Combine(_folder, "capture.json.tmp"), "{}");
            var observer = CreateObserver();
            using var probe = new EventRecorder(observer);

            File.Move(Path.Combine(_folder, "capture.json.tmp"), Path.Combine(_folder, "capture.json"));
            probe.WaitFor("created", "capture.json");

            CollectionAssert.AreEqual(new[] { "capture.json" }, Listing(observer));
        }

        [TestMethod]
        public void RenamingAFileIntoARecordName_ReportsTheRecord()
        {
            File.WriteAllText(Path.Combine(_folder, "other.json"), "{}");
            File.WriteAllText(Path.Combine(_folder, "capture.part"), "{}");
            var observer = CreateObserver();
            using var probe = new EventRecorder(observer);

            File.Move(Path.Combine(_folder, "capture.part"), Path.Combine(_folder, "capture.json"));
            probe.WaitFor("created", "capture.json");

            Assert.IsFalse(probe.Events.Any(e => e.Name == "capture.part"));
            CollectionAssert.AreEquivalent(new[] { "other.json", "capture.json" }, Listing(observer));
        }

        [TestMethod]
        public void Dispose_WithoutAnObservedFolder_EndsTheStreams()
        {
            var observer = CreateObserver(Path.Combine(_folder, "missing"), keep: false);
            bool completed = false;
            using var subscription = observer.FileCreatedStream.Subscribe(_ => { }, () => completed = true);

            observer.Dispose();

            Assert.IsTrue(completed);
        }

        private static string[] Listing(RecordDirectoryObserver observer)
        {
            IEnumerable<FileInfo> files = null;
            using (observer.DirectoryFilesStream.Subscribe(value => files = value))
            {
            }
            return files.Select(file => file.Name).ToArray();
        }

        private RecordDirectoryObserver CreateObserver(string folder = null, bool keep = true)
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupAllProperties();
            configuration.Object.ObservedDirectory = folder ?? _folder;
            var pathService = new Mock<IPathService>();
            pathService.Setup(service => service.ResolveDocumentsPlaceholder(It.IsAny<string>()))
                .Returns((string path) => path);
            var observer = new RecordDirectoryObserver(configuration.Object, pathService.Object,
                NullLogger<RecordDirectoryObserver>.Instance);
            if (keep)
                _observer = observer;
            return observer;
        }

        /// <summary>Records the kind and file name of every event, in order.</summary>
        private sealed class EventRecorder : IDisposable
        {
            private readonly List<(string Kind, string Name)> _events = new List<(string, string)>();
            private readonly IDisposable[] _subscriptions;

            public EventRecorder(RecordDirectoryObserver observer)
            {
                _subscriptions = new[]
                {
                    observer.FileCreatedStream.Subscribe(file => Add("created", file)),
                    observer.FileChangedStream.Subscribe(file => Add("changed", file)),
                    observer.FileDeletedStream.Subscribe(file => Add("deleted", file)),
                };
            }

            public List<(string Kind, string Name)> Events
            {
                get
                {
                    lock (_events)
                        return _events.ToList();
                }
            }

            public void WaitFor(string kind, string name)
            {
                lock (_events)
                {
                    var deadline = DateTime.UtcNow + EventTimeout;
                    while (!_events.Contains((kind, name)))
                    {
                        var left = deadline - DateTime.UtcNow;
                        if (left <= TimeSpan.Zero)
                            Assert.Fail($"No {kind} event for {name}; got: {string.Join(", ", _events)}");
                        Monitor.Wait(_events, left);
                    }
                }
            }

            public void Dispose()
            {
                foreach (var subscription in _subscriptions)
                    subscription.Dispose();
            }

            private void Add(string kind, FileInfo file)
            {
                lock (_events)
                {
                    _events.Add((kind, file.Name));
                    Monitor.PulseAll(_events);
                }
            }
        }
    }
}
