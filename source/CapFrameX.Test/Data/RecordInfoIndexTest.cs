using CapFrameX.Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace CapFrameX.Test.Data
{
    [TestClass]
    public class RecordInfoIndexTest
    {
        private string _directory;
        private string _indexPath;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordInfoIndexTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _indexPath = Path.Combine(_directory, "RecordInfoIndex.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [TestMethod]
        public void StoredEntry_IsFoundByPathSizeAndStamp_AlsoByTheNextRun()
        {
            string record = Record(0);
            using (var index = new RecordInfoIndex(_indexPath))
            {
                index.Store(record, 123, 456, Entry("Game.exe"));

                Assert.IsTrue(index.TryGet(record, 123, 456, out var same));
                Assert.AreEqual("Game.exe", same.ProcessName);
                Assert.IsFalse(index.TryGet(record, 124, 456, out _), "Another size is another file.");
                Assert.IsFalse(index.TryGet(record, 123, 457, out _), "Another write time is another file.");
                Assert.IsFalse(index.TryGet(Record(1), 123, 456, out _));
                Assert.IsFalse(File.Exists(_indexPath), "The index is written a moment after the change, not at once.");
                index.Flush();
                Assert.IsTrue(File.Exists(_indexPath));
            }

            using var next = new RecordInfoIndex(_indexPath);
            Assert.IsTrue(next.TryGet(Path.Combine(_directory, ".", "RECORD-0.json"), 123, 456, out var persisted),
                "Paths are compared normalized and without regard to case.");
            Assert.AreEqual("Game.exe", persisted.ProcessName);
            Assert.AreEqual("hash", persisted.Hash);
            Assert.AreEqual(1, next.Count);
        }

        [TestMethod]
        public void UnreadableIndex_IsRebuilt()
        {
            File.WriteAllText(_indexPath, "{ not json");
            using var index = new RecordInfoIndex(_indexPath);

            Assert.IsFalse(index.TryGet(Record(0), 1, 1, out _));
            index.Store(Record(0), 1, 1, Entry("Game.exe"));
            index.Flush();

            using var next = new RecordInfoIndex(_indexPath);
            Assert.IsTrue(next.TryGet(Record(0), 1, 1, out _));
        }

        [TestMethod]
        public void IndexOfAnotherFormat_IsDiscarded()
        {
            using (var index = new RecordInfoIndex(_indexPath))
            {
                index.Store(Record(0), 1, 1, Entry("Game.exe"));
                index.Flush();
            }
            string json = File.ReadAllText(_indexPath);
            StringAssert.Contains(json, "\"version\":" + RecordInfoIndex.FormatVersion);
            File.WriteAllText(_indexPath, json.Replace("\"version\":" + RecordInfoIndex.FormatVersion,
                "\"version\":" + (RecordInfoIndex.FormatVersion + 1)));

            using var next = new RecordInfoIndex(_indexPath);

            Assert.AreEqual(0, next.Count);
        }

        [TestMethod]
        public void EntriesBeyondTheCapacity_DropTheLeastRecentlyUsed()
        {
            var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
            using var index = new RecordInfoIndex(_indexPath) { Capacity = 100, Clock = () => now };
            for (int i = 0; i < 100; i++)
            {
                index.Store(Record(i), 1, 1, Entry($"Game{i}.exe"));
                now = now.AddMinutes(1);
            }
            // Listing an entry later keeps it: its use stamp is refreshed.
            now = now.AddDays(2);
            Assert.IsTrue(index.TryGet(Record(0), 1, 1, out _));

            index.Store(Record(100), 1, 1, Entry("Game100.exe"));

            Assert.IsTrue(index.Count <= 100);
            Assert.IsTrue(index.TryGet(Record(0), 1, 1, out _), "The entry used last stays.");
            Assert.IsTrue(index.TryGet(Record(100), 1, 1, out _));
            Assert.IsFalse(index.TryGet(Record(1), 1, 1, out _), "The entry used longest ago goes first.");
            Assert.IsTrue(index.TryGet(Record(50), 1, 1, out _));
        }

        [TestMethod]
        public void EntriesUnusedForTheRetentionPeriod_AreDroppedWhenTheIndexIsLoaded()
        {
            var now = new DateTime(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);
            using (var index = new RecordInfoIndex(_indexPath) { Clock = () => now })
            {
                index.Store(Record(0), 1, 1, Entry("Old.exe"));
                now = now.AddDays(10);
                index.Store(Record(1), 1, 1, Entry("Recent.exe"));
                index.Flush();
            }
            now += RecordInfoIndex.Retention - TimeSpan.FromDays(5);

            using var next = new RecordInfoIndex(_indexPath) { Clock = () => now };

            Assert.IsFalse(next.TryGet(Record(0), 1, 1, out _));
            Assert.IsTrue(next.TryGet(Record(1), 1, 1, out _));
        }

        [TestMethod]
        public void IndexWithoutAPath_KeepsNothing()
        {
            using var index = new RecordInfoIndex(null);

            index.Store(Record(0), 1, 1, Entry("Game.exe"));
            index.Flush();

            Assert.IsFalse(index.TryGet(Record(0), 1, 1, out _));
            Assert.AreEqual(0, index.Count);
        }

        [TestMethod]
        public void SavedIndex_LeavesNoTemporaryFile_AndIsNotRewrittenWithoutAChange()
        {
            using var index = new RecordInfoIndex(_indexPath);
            index.Store(Record(0), 1, 1, Entry("Game.exe"));
            index.Flush();
            var written = File.GetLastWriteTimeUtc(_indexPath);

            index.TryGet(Record(0), 1, 1, out _);
            index.Flush();

            CollectionAssert.AreEqual(new[] { Path.GetFileName(_indexPath) },
                Directory.GetFiles(_directory).Select(Path.GetFileName).ToArray());
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(_indexPath), "A hit within a day changes nothing.");
        }

        private string Record(int number) => Path.Combine(_directory, $"record-{number}.json");

        private static RecordInfoIndexEntry Entry(string processName)
            => new RecordInfoIndexEntry { ProcessName = processName, GameName = processName, Hash = "hash", IsValid = true };
    }
}
