using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// When GetFileRecordInfo reads a file. The waits run on a virtual clock, so the tests show
    /// exactly which read waits and how long.
    /// </summary>
    [TestClass]
    public class RecordManagerFileAccessTest
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan Moment = TimeSpan.FromTicks(1);

        private string _directory;
        private HistoricalScheduler _clock;
        private RecordManager _recordManager;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordFileAccessTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _clock = new HistoricalScheduler();
            _recordManager = new RecordManager(
                new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null,
                new EventAggregator(), null, null)
            {
                FileAccessScheduler = _clock
            };
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [TestMethod]
        public void FileFromADirectoryScan_IsReadWithoutWaiting()
        {
            string path = WriteRecord("scan.json", "Scan.exe");

            Task<IFileRecordInfo> read = _recordManager.GetFileRecordInfo(new FileInfo(path));
            _clock.AdvanceBy(Moment);

            Assert.AreEqual("Scan.exe", Await(read).ProcessName);
        }

        [TestMethod]
        public void ManyFilesFromADirectoryScan_AreAllReadWithoutWaiting()
        {
            // Regression: every file waited 200 ms first, so a folder of N records took
            // ceil(N / 30) x 200 ms with the 30 parallel reads of the record list.
            var reads = Enumerable.Range(0, 64)
                .Select(i => _recordManager.GetFileRecordInfo(
                    new FileInfo(WriteRecord($"scan-{i}.json", $"Game{i}.exe"))))
                .ToArray();

            _clock.AdvanceBy(Moment);

            Assert.IsTrue(Task.WaitAll(reads, WaitTimeout));
            CollectionAssert.AreEqual(
                Enumerable.Range(0, 64).Select(i => $"Game{i}.exe").ToArray(),
                reads.Select(read => read.Result.ProcessName).ToArray());
        }

        [TestMethod]
        public void FileFromACreatedOrChangedEvent_WaitsOneIntervalFirst()
        {
            string path = WriteRecord("created.json", "Created.exe");

            Task<IFileRecordInfo> read = _recordManager.GetFileRecordInfo(new FileInfo(path),
                fileMayStillBeWritten: true);
            _clock.AdvanceBy(Interval - Moment);
            Assert.IsFalse(read.IsCompleted, "The writer may not be done yet.");

            _clock.AdvanceBy(Moment);

            Assert.AreEqual("Created.exe", Await(read).ProcessName);
        }

        [TestMethod]
        [DataRow(false, DisplayName = "Directory scan")]
        [DataRow(true, DisplayName = "Created or changed event")]
        public void LockedFile_IsRetriedAfterTheInterval(bool fileMayStillBeWritten)
        {
            string path = WriteRecord("locked.json", "Locked.exe");
            TimeSpan firstAttempt = fileMayStillBeWritten ? Interval : TimeSpan.Zero;
            Task<IFileRecordInfo> read;

            using (LockFile(path))
            {
                read = _recordManager.GetFileRecordInfo(new FileInfo(path), fileMayStillBeWritten);
                _clock.AdvanceBy(firstAttempt + Moment);
                Assert.IsFalse(read.IsCompleted, "The first attempt fails on the locked file.");
            }

            _clock.AdvanceBy(Interval - Moment - Moment);
            Assert.IsFalse(read.IsCompleted, "A retry must wait the interval.");
            _clock.AdvanceBy(Moment);

            Assert.AreEqual("Locked.exe", Await(read).ProcessName);
        }

        [TestMethod]
        public void LockedFile_GivesUpAfterTheRetryLimit()
        {
            string path = WriteRecord("stays-locked.json", "Locked.exe");

            using (LockFile(path))
            {
                Task<IFileRecordInfo> read = _recordManager.GetFileRecordInfo(new FileInfo(path));
                // The first attempt runs at once, each of the 49 retries one interval later.
                _clock.AdvanceBy(TimeSpan.FromTicks(Interval.Ticks * 49) - Moment);
                Assert.IsFalse(read.IsCompleted);

                _clock.AdvanceBy(Moment);

                var error = Assert.ThrowsExactly<AggregateException>(() => read.Wait(WaitTimeout));
                Assert.IsInstanceOfType<IOException>(error.InnerException);
            }
        }

        [TestMethod]
        public void CachedFile_IsReturnedWithoutTheClock()
        {
            string path = WriteRecord("cached.json", "Cached.exe");
            Task<IFileRecordInfo> first = _recordManager.GetFileRecordInfo(new FileInfo(path));
            _clock.AdvanceBy(Moment);
            IFileRecordInfo firstInfo = Await(first);

            Task<IFileRecordInfo> second = _recordManager.GetFileRecordInfo(new FileInfo(path),
                fileMayStillBeWritten: true);

            Assert.IsTrue(second.IsCompleted, "An unchanged file is served from the cache.");
            Assert.AreSame(firstInfo, second.Result);
        }

        private static IFileRecordInfo Await(Task<IFileRecordInfo> read)
        {
            Assert.IsTrue(read.Wait(WaitTimeout), "The read did not finish.");
            Assert.IsNotNull(read.Result);
            return read.Result;
        }

        private static FileStream LockFile(string path)
            => new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        private string WriteRecord(string fileName, string processName)
        {
            string path = Path.Combine(_directory, fileName);
            File.WriteAllText(path,
                "{\"Hash\":\"" + fileName + "\",\"Info\":{" +
                "\"Id\":\"" + Guid.NewGuid() + "\"," +
                "\"CreationDate\":\"2026-07-18T12:00:00Z\"," +
                "\"ProcessName\":\"" + processName + "\",\"Comment\":\"file access\"}," +
                "\"Runs\":[{\"CaptureData\":{\"TimeInSeconds\":[0.0,2.5]}}]}");
            return path;
        }
    }
}
