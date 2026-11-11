using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Mcp.Tools;
using CapFrameX.Test.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CapFrameX.Test.Mcp
{
    /// <summary>
    /// cfx_set_record_comment changes the comment and nothing else, also for a record whose game
    /// name the list takes from the process list.
    /// </summary>
    [TestClass]
    public class RecordCommentToolTest
    {
        private string _folder;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordCommentToolTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_folder, true);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("Game.exe")]
        public async Task JsonRecordNamedByTheProcessList_KeepsItsStoredFields(string storedGameName)
        {
            // Regression: the tool wrote the process list's display name into the record.
            var session = RecordFiles.SessionWith("Game", RecordFiles.Run());
            session.Info.ProcessName = "Game.exe";
            session.Info.GameName = storedGameName;
            session.Info.Processor = "CPU";
            session.Info.GPU = "GPU";
            session.Info.SystemRam = "RAM";
            session.Info.Motherboard = "Board";
            session.Info.ResolutionInfo = "2560x1440";
            session.Info.Comment = "old";
            string path = Path.Combine(_folder, "record.json");
            File.WriteAllText(path, JsonConvert.SerializeObject(session));
            var manager = RecordManagerWith(ProcessListWith("Game.exe", "Display Name"));
            Assert.AreEqual("Display Name", (await manager.GetFileRecordInfo(new FileInfo(path))).GameName);

            var result = await Tool(manager).SetRecordComment(path, "new");

            var stored = RecordManagerWith(null).LoadData(path).Info;
            Assert.AreEqual(storedGameName, stored.GameName);
            Assert.AreEqual("new", stored.Comment);
            CollectionAssert.AreEqual(new[] { "Game.exe", "CPU", "GPU", "RAM", "Board", "2560x1440" },
                new[] { stored.ProcessName, stored.Processor, stored.GPU, stored.SystemRam, stored.Motherboard, stored.ResolutionInfo });
            Assert.AreEqual("old", result.OldComment);
            Assert.AreEqual("new", result.NewComment);
        }

        [TestMethod]
        public async Task CsvRecordNamedByTheProcessList_KeepsItsStoredFields()
        {
            string path = Path.Combine(_folder, "record.csv");
            var lines = File.ReadAllLines(@"TestRecordFiles\CapFrameXFileWithHeader.csv")
                .Select(line => line.StartsWith("//GameName=") ? "//GameName=" : line)
                .ToArray();
            File.WriteAllLines(path, lines);
            var manager = RecordManagerWith(ProcessListWith("Resident Evil 2 Remake", "Display Name"));
            Assert.AreEqual("Display Name", (await manager.GetFileRecordInfo(new FileInfo(path))).GameName);

            var result = await Tool(manager).SetRecordComment(path, "new");

            var expected = lines.Select(line => line.StartsWith("//Comment=") ? "//Comment=new" : line).ToArray();
            CollectionAssert.AreEqual(expected, File.ReadAllLines(path));
            Assert.AreEqual("Test", result.OldComment);
        }

        [TestMethod]
        public async Task UnreadableRecord_IsReportedAndLeftAlone()
        {
            string path = Path.Combine(_folder, "record.json");
            File.WriteAllText(path, "{\"Hash\":\"broken\",\"Runs\":[{\"CaptureData\":");

            await Assert.ThrowsAsync<Exception>(() => Tool(RecordManagerWith(null)).SetRecordComment(path, "new"));

            Assert.AreEqual("{\"Hash\":\"broken\",\"Runs\":[{\"CaptureData\":", File.ReadAllText(path));
        }

        private static ConfigWriteTools Tool(RecordManager manager)
            => new ConfigWriteTools(null, null, null, null, manager, null);

        private ProcessList ProcessListWith(string processName, string displayName)
        {
            var processList = (ProcessList)Activator.CreateInstance(typeof(ProcessList),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[]
                {
                    Path.Combine(_folder, "processes.json"),
                    new Mock<IAppConfiguration>().Object,
                    new Mock<ILogger<ProcessList>>().Object
                }, null);
            processList.AddEntry(processName, displayName);
            return processList;
        }

        private static RecordManager RecordManagerWith(ProcessList processList)
            => new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, processList, null, new EventAggregator(), null, null);
    }
}
