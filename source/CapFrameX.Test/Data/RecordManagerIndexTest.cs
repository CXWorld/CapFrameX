using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// A record folder read before is listed from the on-disk index: its files are not read
    /// again until they change.
    /// </summary>
    [TestClass]
    public class RecordManagerIndexTest
    {
        private string _directory;
        private string _indexPath;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordManagerIndexTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            _indexPath = Path.Combine(_directory, "index", "RecordInfoIndex.json");
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [TestMethod]
        public async Task JsonRecord_IsListedFromTheIndexWithoutBeingRead()
        {
            string path = WriteRecord("capture.json", JsonRecord("Game.exe", "Game Display", "metadata"));
            using (var index = new RecordInfoIndex(_indexPath))
            {
                var read = await CreateRecordManager(index).GetFileRecordInfo(new FileInfo(path));
                Assert.AreEqual("Game Display", read.GameName);
                index.Flush();
            }
            // Same size and write time, other content: a hit lists what was read before.
            ReplaceContentKeepingTheStamp(path, JsonRecord("Gxme.exe", "Gxme Display", "mxtadata"));

            using var next = new RecordInfoIndex(_indexPath);
            var listed = await CreateRecordManager(next).GetFileRecordInfo(new FileInfo(path));

            Assert.AreEqual("Game.exe", listed.ProcessName);
            Assert.AreEqual("Game Display", listed.GameName);
            Assert.AreEqual("metadata", listed.Comment);
            Assert.AreEqual("SESSION-HASH", listed.Hash);
            Assert.AreEqual("8f9f8c95-f1c1-4a38-90b4-61c74a40df35", listed.Id);
            Assert.AreEqual("True", listed.IsAggregated);
            Assert.AreEqual(2.75, listed.RecordTime, 1E-12);
            Assert.AreEqual("CPU", listed.ProcessorName);
            Assert.AreEqual(path, listed.FullPath);
            Assert.AreEqual(path, listed.FileInfo.FullName);
            Assert.IsTrue(listed.IsValid);
        }

        [TestMethod]
        public async Task ChangedRecord_IsReadAgainAndIndexedAnew()
        {
            string path = WriteRecord("capture.json", JsonRecord("Game.exe", "Game Display", "metadata"));
            using (var index = new RecordInfoIndex(_indexPath))
            {
                await CreateRecordManager(index).GetFileRecordInfo(new FileInfo(path));
                index.Flush();
            }
            var stamp = File.GetLastWriteTimeUtc(path);
            File.WriteAllText(path, JsonRecord("Other.exe", "Other Display", "edited"));
            File.SetLastWriteTimeUtc(path, stamp.AddSeconds(2));

            using (var next = new RecordInfoIndex(_indexPath))
            {
                var listed = await CreateRecordManager(next).GetFileRecordInfo(new FileInfo(path));
                Assert.AreEqual("Other.exe", listed.ProcessName);
                Assert.AreEqual("edited", listed.Comment);
                next.Flush();
            }
            ReplaceContentKeepingTheStamp(path, JsonRecord("Oxher.exe", "Oxher Display", "exited"));

            using var last = new RecordInfoIndex(_indexPath);
            Assert.AreEqual("Other.exe", (await CreateRecordManager(last).GetFileRecordInfo(new FileInfo(path))).ProcessName);
        }

        [TestMethod]
        public async Task CsvRecord_KeepsItsHashAndHeaderInTheIndex()
        {
            // Hashing a CSV record parses it in full; the index remembers the result.
            string path = Path.Combine(_directory, "record.csv");
            File.Copy(Path.Combine("TestRecordFiles", "CapFrameXFileWithHeader.csv"), path);
            IFileRecordInfo read;
            using (var index = new RecordInfoIndex(_indexPath))
            {
                read = await CreateRecordManager(index).GetFileRecordInfo(new FileInfo(path));
                index.Flush();
            }
            Assert.IsNotNull(read?.Hash);
            ReplaceContentKeepingTheStamp(path, new string('x', (int)new FileInfo(path).Length));

            using var next = new RecordInfoIndex(_indexPath);
            var listed = await CreateRecordManager(next).GetFileRecordInfo(new FileInfo(path));

            Assert.IsNotNull(listed);
            Assert.AreNotSame(read, listed);
            Assert.AreEqual(read.Hash, listed.Hash);
            Assert.AreEqual(read.Id, listed.Id);
            Assert.AreEqual(read.ProcessName, listed.ProcessName);
            Assert.AreEqual(read.GameName, listed.GameName);
            Assert.AreEqual(read.Comment, listed.Comment);
            Assert.AreEqual(read.CreationDate, listed.CreationDate);
            Assert.AreEqual(read.CreationTime, listed.CreationTime);
            Assert.AreEqual(read.RecordTime, listed.RecordTime);
            Assert.AreEqual(read.ProcessorName, listed.ProcessorName);
            Assert.AreEqual(read.GraphicCardName, listed.GraphicCardName);
            Assert.AreEqual(read.SystemRamInfo, listed.SystemRamInfo);
            Assert.AreEqual(read.MotherboardName, listed.MotherboardName);
            Assert.AreEqual(read.OsVersion, listed.OsVersion);
            Assert.AreEqual(read.IsAggregated, listed.IsAggregated);
            Assert.AreEqual(read.HasInfoHeader, listed.HasInfoHeader);
            Assert.AreEqual(read.IsValid, listed.IsValid);
            Assert.AreEqual(read.CombinedInfo, listed.CombinedInfo);
        }

        [TestMethod]
        public async Task IndexedRecord_ShowsTheProcessListNameOfTheDay()
        {
            // The index keeps the game name stored in the file; the display name comes from the
            // process list on every listing, as for a record read from its file.
            var processList = (ProcessList)Activator.CreateInstance(typeof(ProcessList),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[]
                {
                    Path.Combine(_directory, "processes.json"),
                    new Mock<IAppConfiguration>().Object,
                    new Mock<ILogger<ProcessList>>().Object
                }, null);
            processList.AddEntry("RenameMe.exe", "First Name");
            string path = WriteRecord("rename.json", JsonRecord("RenameMe.exe", "RenameMe.exe", "metadata"));
            using (var index = new RecordInfoIndex(_indexPath))
            {
                var first = await CreateRecordManager(index, processList).GetFileRecordInfo(new FileInfo(path));
                Assert.AreEqual("First Name", first.GameName);
                index.Flush();
            }
            processList.FindProcessByName("RenameMe.exe").UpdateDisplayName("Second Name");

            using var next = new RecordInfoIndex(_indexPath);
            var listed = await CreateRecordManager(next, processList).GetFileRecordInfo(new FileInfo(path));

            Assert.AreEqual("Second Name", listed.GameName);
        }

        private static RecordManager CreateRecordManager(RecordInfoIndex index, ProcessList processList = null)
            => new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, processList, null, new EventAggregator(), null, null, index);

        private string WriteRecord(string fileName, string content)
        {
            string path = Path.Combine(_directory, fileName);
            File.WriteAllText(path, content);
            return path;
        }

        private static void ReplaceContentKeepingTheStamp(string path, string content)
        {
            var stamp = File.GetLastWriteTimeUtc(path);
            long length = new FileInfo(path).Length;
            File.WriteAllText(path, content);
            Assert.AreEqual(length, new FileInfo(path).Length, "The replacement must keep the size.");
            File.SetLastWriteTimeUtc(path, stamp);
        }

        private static string JsonRecord(string processName, string gameName, string comment)
            => "{\"Hash\":\"SESSION-HASH\",\"Info\":{" +
               "\"Id\":\"8f9f8c95-f1c1-4a38-90b4-61c74a40df35\"," +
               "\"CreationDate\":\"2026-07-18T12:00:00Z\",\"ProcessName\":\"" + processName + "\"," +
               "\"GameName\":\"" + gameName + "\",\"Processor\":\"CPU\",\"GPU\":\"GPU\"," +
               "\"SystemRam\":\"32 GB\",\"Comment\":\"" + comment + "\"}," +
               "\"Runs\":[" +
               "{\"CaptureData\":{\"TimeInSeconds\":[0.0,1.25],\"MsBetweenPresents\":[16.0,17.0]}}," +
               "{\"CaptureData\":{\"TimeInSeconds\":[1.25,2.75],\"MsBetweenPresents\":[18.0,19.0]}}]}";
    }
}
