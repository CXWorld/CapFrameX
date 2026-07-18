using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Extensions;
using CapFrameX.Test.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    [TestClass]
    public class RecordManagerCacheTest
    {
        private string _testDirectory;
        private RecordManager _recordManager;
        private IPathService _previousPathService;

        [TestInitialize]
        public void Initialize()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordCacheTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testDirectory);
            _previousPathService = PathServiceProvider.PathService;
            var pathService = new Mock<IPathService>();
            pathService.SetupGet(service => service.ConfigFolder).Returns(Path.Combine(_testDirectory, "Config"));
            PathServiceProvider.PathService = pathService.Object;
            _recordManager = CreateRecordManager();
        }

        private static RecordManager CreateRecordManager(ProcessList processList = null)
        {
            return new RecordManager(
                new Mock<ILogger<RecordManager>>().Object,
                null,
                null,
                null,
                null,
                null,
                processList,
                null,
                new EventAggregator(),
                null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Allow the bounded background writer to finish before removing its isolated folder.
            Thread.Sleep(400);
            PathServiceProvider.PathService = _previousPathService;
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }

        [TestMethod]
        public void LoadData_UnchangedNormalizedPath_ReusesSession()
        {
            string path = CreateSessionFile("capture.json", "First.exe");
            string equivalentPath = Path.Combine(_testDirectory, ".", "capture.json");

            ISession first = _recordManager.LoadData(path);
            ISession second = _recordManager.LoadData(equivalentPath);

            Assert.IsNotNull(first);
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void LoadData_ChangedFileStamp_ReloadsSession()
        {
            string path = CreateSessionFile("capture.json", "First.exe");
            ISession first = _recordManager.LoadData(path);

            DateTime changedStamp = File.GetLastWriteTimeUtc(path).AddSeconds(2);
            File.WriteAllText(path, CreateSessionJson("Other.exe"));
            File.SetLastWriteTimeUtc(path, changedStamp);

            ISession second = _recordManager.LoadData(path);

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
            Assert.AreEqual("Other.exe", second.Info.ProcessName);
        }

        [TestMethod]
        public async Task LoadData_ConcurrentRequests_ShareSession()
        {
            string path = CreateSessionFile("capture.json", "First.exe");
            var loads = Enumerable.Range(0, 12)
                .Select(_ => Task.Run(() => _recordManager.LoadData(path)))
                .ToArray();

            ISession[] sessions = await Task.WhenAll(loads);

            Assert.IsTrue(sessions.All(session => session != null));
            Assert.IsTrue(sessions.All(session => ReferenceEquals(sessions[0], session)));
        }

        [TestMethod]
        public void IncrementalPresentHash_MatchesLegacyContract()
        {
            var cases = new[]
            {
                new string[0],
                new[] { "single" },
                new[] { "first", "second", "third" },
                new[] { "first", string.Empty, "third" },
                new[] { "embedded,comma", "\u00e4\ud83d\ude80" }
            };
            var method = typeof(RecordManager).GetMethod("GetPresentLinesSha1",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method);
            foreach (var lines in cases)
            {
                string expected = string.Join(",", lines).GetSha1();
                string actual = (string)method.Invoke(null, new object[] { lines });
                Assert.AreEqual(expected, actual);
            }
            Assert.AreEqual("DA39A3EE5E6B4B0D3255BFEF95601890AFD80709",
                (string)method.Invoke(null, new object[] { new string[0] }));
        }

        [TestMethod]
        public void ConvertPresentData_DominantSwapChain_PreservesSelectedRowsAndValues()
        {
            var recordManager = new RecordManager(
                new Mock<ILogger<RecordManager>>().Object,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                new EventAggregator(),
                new MockCaptureService());
            const string header = "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents,MsBetweenDisplayChange";
            var selectedRows = new[]
            {
                "P2,2,C,10.00,20,21",
                "P2,2,C,10.02,22,23",
                "P2,2,C,10.04,24,25",
                "P2,2,C,10.06,26,27"
            };
            var lines = new List<string> { header };
            lines.AddRange(new[]
            {
                "P1,1,A,1.00,10,11",
                "P1,1,A,1.01,10,11",
                "P1,1,A,1.02,10,11",
                "P1,1,B,2.00,12,13",
                "P1,1,B,2.01,12,13"
            });
            lines.AddRange(selectedRows);

            var run = recordManager.ConvertPresentDataLinesToSessionRun(lines);

            Assert.AreEqual(string.Join(",", selectedRows).GetSha1(), run.Hash);
            CollectionAssert.AreEqual(new[] { 20d, 22d, 24d, 26d }, run.CaptureData.MsBetweenPresents);
            CollectionAssert.AreEqual(new[] { 21d, 23d, 25d, 27d }, run.CaptureData.MsBetweenDisplayChange);
            var expectedTimes = new[] { 0d, 0.02d, 0.04d, 0.06d };
            for (int i = 0; i < expectedTimes.Length; i++)
            {
                Assert.AreEqual(expectedTimes[i], run.CaptureData.TimeInSeconds[i], 1E-12);
            }
            Assert.IsTrue(run.CaptureData.AnimationError.All(double.IsNaN));
        }

        [TestMethod]
        public async Task GetFileRecordInfo_JsonMetadataScan_MatchesFullSessionAndCachesResult()
        {
            string path = Path.Combine(_testDirectory, "metadata.json");
            File.WriteAllText(path,
                "{\"Hash\":\"SESSION-HASH\",\"Info\":{" +
                "\"Id\":\"8f9f8c95-f1c1-4a38-90b4-61c74a40df35\"," +
                "\"CreationDate\":\"2026-07-18T12:00:00Z\",\"ProcessName\":\"Game.exe\"," +
                "\"GameName\":\"Game Display\",\"Processor\":\"CPU\",\"GPU\":\"GPU\"," +
                "\"SystemRam\":\"32 GB\",\"Comment\":\"metadata\"}," +
                "\"Runs\":[" +
                "{\"CaptureData\":{\"TimeInSeconds\":[0.0,1.25],\"MsBetweenPresents\":[16.0,17.0]}}," +
                "{\"CaptureData\":{\"TimeInSeconds\":[1.25,2.75],\"MsBetweenPresents\":[18.0,19.0]}}]}");
            var fileInfo = new FileInfo(path);

            var lightweight = await _recordManager.GetFileRecordInfo(fileInfo);
            var cached = await _recordManager.GetFileRecordInfo(new FileInfo(path));
            var fullSession = _recordManager.LoadData(path);
            var full = FileRecordInfo.Create(fileInfo, fullSession);

            Assert.IsNotNull(lightweight);
            Assert.AreSame(lightweight, cached);
            Assert.AreEqual(full.Hash, lightweight.Hash);
            Assert.AreEqual(full.Id, lightweight.Id);
            Assert.AreEqual(full.ProcessName, lightweight.ProcessName);
            Assert.AreEqual(full.GameName, lightweight.GameName);
            Assert.AreEqual(full.ProcessorName, lightweight.ProcessorName);
            Assert.AreEqual(full.GraphicCardName, lightweight.GraphicCardName);
            Assert.AreEqual(full.SystemRamInfo, lightweight.SystemRamInfo);
            Assert.AreEqual(full.Comment, lightweight.Comment);
            Assert.AreEqual(full.IsAggregated, lightweight.IsAggregated);
            Assert.AreEqual(2.75, lightweight.RecordTime, 1E-12);
        }

        [TestMethod]
        public async Task GetFileRecordInfo_PersistentIndex_ReusesMetadataWithoutOpeningCapture()
        {
            string path = CreateMetadataSessionFile("persistent.json", "Persistent.exe", "Persistent Game");
            var first = await _recordManager.GetFileRecordInfo(new FileInfo(path));
            string indexPath = await WaitForRecordIndexAsync();

            var secondManager = CreateRecordManager();
            IFileRecordInfo cached;
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                cached = await secondManager.GetFileRecordInfo(new FileInfo(path));
            }

            Assert.IsTrue(File.Exists(indexPath));
            Assert.IsNotNull(cached);
            Assert.AreEqual(first.Hash, cached.Hash);
            Assert.AreEqual(first.ProcessName, cached.ProcessName);
            Assert.AreEqual(first.GameName, cached.GameName);
            Assert.AreEqual(first.RecordTime, cached.RecordTime, 1E-12);
        }

        [TestMethod]
        public async Task GetFileRecordInfo_CorruptPersistentIndex_FallsBackAndRepairs()
        {
            string cacheDirectory = Path.Combine(_testDirectory, "Config", "Cache");
            Directory.CreateDirectory(cacheDirectory);
            string indexPath = Path.Combine(cacheDirectory, "RecordMetadataIndex.bin");
            File.WriteAllBytes(indexPath, new byte[] { 1, 2, 3, 4, 5 });
            string path = CreateMetadataSessionFile("corrupt-index.json", "Fallback.exe", "Fallback Game");

            var recordInfo = await _recordManager.GetFileRecordInfo(new FileInfo(path));
            await WaitForRecordIndexAsync();

            Assert.IsNotNull(recordInfo);
            Assert.AreEqual("Fallback.exe", recordInfo.ProcessName);
            byte[] header = File.ReadAllBytes(indexPath).Take(4).ToArray();
            CollectionAssert.AreEqual(new byte[] { 0x43, 0x46, 0x49, 0x58 }, header);
        }

        [TestMethod]
        public async Task GetFileRecordInfo_PersistentIndex_FileChanged_ReloadsMetadata()
        {
            string path = CreateMetadataSessionFile("changed.json", "Before.exe", "Before Game");
            await _recordManager.GetFileRecordInfo(new FileInfo(path));
            await WaitForRecordIndexAsync();
            DateTime changedStamp = File.GetLastWriteTimeUtc(path).AddSeconds(2);

            CreateMetadataSessionFile("changed.json", "Afterx.exe", "Afterx Game");
            File.SetLastWriteTimeUtc(path, changedStamp);
            var secondManager = CreateRecordManager();
            var refreshed = await secondManager.GetFileRecordInfo(new FileInfo(path));

            Assert.IsNotNull(refreshed);
            Assert.AreEqual("Afterx.exe", refreshed.ProcessName);
            Assert.AreEqual("Afterx Game", refreshed.GameName);
        }

        [TestMethod]
        public async Task GetFileRecordInfo_ProcessListNameChanged_RefreshesMemoryAndPersistentCaches()
        {
            var processList = (ProcessList)Activator.CreateInstance(typeof(ProcessList),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[]
                {
                    Path.Combine(_testDirectory, "processes.json"),
                    new Mock<IAppConfiguration>().Object,
                    new Mock<ILogger<ProcessList>>().Object
                }, null);
            processList.AddEntry("RenameMe.exe", "First Name");
            var manager = CreateRecordManager(processList);
            string path = CreateMetadataSessionFile("rename.json", "RenameMe.exe", "RenameMe.exe");

            var first = await manager.GetFileRecordInfo(new FileInfo(path));
            Assert.AreEqual("First Name", first.GameName);
            await WaitForRecordIndexAsync();

            processList.FindProcessByName("RenameMe.exe").UpdateDisplayName("Second Name");
            var memoryCached = await manager.GetFileRecordInfo(new FileInfo(path));
            var persistentCached = await CreateRecordManager(processList)
                .GetFileRecordInfo(new FileInfo(path));

            Assert.AreEqual("Second Name", memoryCached.GameName);
            Assert.AreEqual("Second Name", persistentCached.GameName);
        }

        [TestMethod]
        public async Task GetFileRecordInfo_IncompleteJsonMetadata_ReturnsNull()
        {
            string infoOnlyPath = Path.Combine(_testDirectory, "info-only.json");
            string runsOnlyPath = Path.Combine(_testDirectory, "runs-only.json");
            File.WriteAllText(infoOnlyPath,
                "{\"Hash\":\"hash\",\"Info\":{\"ProcessName\":\"MissingRuns.exe\"}}");
            File.WriteAllText(runsOnlyPath,
                "{\"Hash\":\"hash\",\"Runs\":[{\"CaptureData\":{\"TimeInSeconds\":[0,1]}}]}");

            var infoOnly = await _recordManager.GetFileRecordInfo(new FileInfo(infoOnlyPath));
            var runsOnly = await _recordManager.GetFileRecordInfo(new FileInfo(runsOnlyPath));

            Assert.IsNull(infoOnly);
            Assert.IsNull(runsOnly);
        }

        [TestMethod]
        public void ConvertPresentData_QuotedCsvFields_FilterAndParseCorrectly()
        {
            var recordManager = new RecordManager(
                new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null,
                new EventAggregator(), new MockCaptureService());
            const string header = "Application,ProcessID,SwapChainAddress,TimeInSeconds,MsBetweenPresents,MsBetweenDisplayChange";
            var selectedRows = new[]
            {
                "\"P,One\",1,\"A,1\",\"10.00\",\"20\",\"21\"",
                "\"P,One\",1,\"A,1\",\"10.02\",\"22\",\"23\""
            };
            var lines = new List<string>
            {
                header,
                "\"Other\",2,\"B\",1.0,11,12"
            };
            lines.AddRange(selectedRows);

            var run = recordManager.ConvertPresentDataLinesToSessionRun(lines);

            Assert.AreEqual(string.Join(",", selectedRows).GetSha1(), run.Hash);
            CollectionAssert.AreEqual(new[] { 20d, 22d }, run.CaptureData.MsBetweenPresents);
            CollectionAssert.AreEqual(new[] { 21d, 23d }, run.CaptureData.MsBetweenDisplayChange);
            Assert.AreEqual(0d, run.CaptureData.TimeInSeconds[0], 1E-12);
            Assert.AreEqual(0.02d, run.CaptureData.TimeInSeconds[1], 1E-12);
        }

        private string CreateSessionFile(string fileName, string processName)
        {
            string path = Path.Combine(_testDirectory, fileName);
            File.WriteAllText(path, CreateSessionJson(processName));
            return path;
        }

        private static string CreateSessionJson(string processName)
        {
            return "{\"Hash\":\"hash\",\"Info\":{\"ProcessName\":\"" + processName + "\"},\"Runs\":[]}";
        }

        private string CreateMetadataSessionFile(string fileName, string processName, string gameName)
        {
            string path = Path.Combine(_testDirectory, fileName);
            File.WriteAllText(path,
                "{\"Hash\":\"persistent-hash\",\"Info\":{" +
                "\"Id\":\"8f9f8c95-f1c1-4a38-90b4-61c74a40df35\"," +
                "\"CreationDate\":\"2026-07-18T12:00:00Z\"," +
                "\"ProcessName\":\"" + processName + "\"," +
                "\"GameName\":\"" + gameName + "\",\"Comment\":\"indexed\"}," +
                "\"Runs\":[{\"CaptureData\":{\"TimeInSeconds\":[0.0,2.5]}}]}");
            return path;
        }

        private async Task<string> WaitForRecordIndexAsync()
        {
            string indexPath = Path.Combine(_testDirectory, "Config", "Cache", "RecordMetadataIndex.bin");
            for (int attempt = 0; attempt < 40; attempt++)
            {
                if (File.Exists(indexPath) && new FileInfo(indexPath).Length > 12)
                {
                    return indexPath;
                }
                await Task.Delay(50);
            }

            Assert.Fail("The persistent record metadata index was not written.");
            return indexPath;
        }
    }
}
