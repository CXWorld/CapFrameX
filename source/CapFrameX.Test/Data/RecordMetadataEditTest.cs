using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Test.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// A metadata edit rewrites the record and hands the new values to the session that is
    /// already loaded, so the views can keep it instead of parsing the record again.
    /// </summary>
    [TestClass]
    public class RecordMetadataEditTest
    {
        private string _folder;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordMetadataEditTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            foreach (var file in Directory.GetFiles(_folder))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_folder, true);
        }

        [TestMethod]
        public async Task JsonEdit_UpdatesTheLoadedSessionLikeAFreshRead()
        {
            var manager = CreateRecordManager();
            string path = WriteJsonRecord("a.json", "before");
            var session = manager.LoadData(path);
            var record = await manager.GetFileRecordInfo(new FileInfo(path));

            manager.UpdateCustomData(record, "CPU 2", "GPU 2", "RAM 2", "Board 2", "Game 2", "after", "2560x1440");

            var expected = new[] { "CPU 2", "GPU 2", "RAM 2", "Board 2", "Game 2", "after", "2560x1440" };
            CollectionAssert.AreEqual(expected, Metadata(session.Info));
            var reread = CreateRecordManager().LoadData(path);
            CollectionAssert.AreEqual(expected, Metadata(reread.Info));
            Assert.AreEqual(reread.Hash, session.Hash);
        }

        [TestMethod]
        public async Task JsonEditWithoutResolution_KeepsTheResolution()
        {
            var manager = CreateRecordManager();
            string path = WriteJsonRecord("a.json", "before");
            var session = manager.LoadData(path);
            var record = await manager.GetFileRecordInfo(new FileInfo(path));

            manager.UpdateCustomData(record, "CPU", "GPU", "RAM", "Board", "Game", "after");

            Assert.AreEqual("after", session.Info.Comment);
            Assert.AreEqual("1920x1080", session.Info.ResolutionInfo);
            Assert.AreEqual("1920x1080", CreateRecordManager().LoadData(path).Info.ResolutionInfo);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task CsvEdit_UpdatesTheLoadedSessionLikeAFreshRead(bool withMotherboardHeader)
        {
            var manager = CreateRecordManager();
            string path = Path.Combine(_folder, "record.csv");
            var lines = File.ReadAllLines(@"TestRecordFiles\CapFrameXFileWithHeader.csv")
                .Where(line => withMotherboardHeader || !line.StartsWith("//Motherboard="));
            File.WriteAllLines(path, lines);
            var session = manager.LoadData(path);
            var record = await manager.GetFileRecordInfo(new FileInfo(path));
            string motherboard = session.Info.Motherboard;

            manager.UpdateCustomData(record, "CPU 2", "GPU 2", "RAM 2", "Board 2", "Game 2", "after");

            var reread = CreateRecordManager().LoadData(path);
            CollectionAssert.AreEqual(Metadata(reread.Info), Metadata(session.Info));
            Assert.AreEqual("after", session.Info.Comment);
            Assert.AreEqual(withMotherboardHeader ? "Board 2" : motherboard, session.Info.Motherboard);
        }

        [TestMethod]
        public async Task EditOfAFileChangedSinceItWasLoaded_LeavesThatSessionAlone()
        {
            var manager = CreateRecordManager();
            string path = WriteJsonRecord("a.json", "before");
            var session = manager.LoadData(path);
            WriteJsonRecord("a.json", "rewritten", hash: "other");
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(5));
            var record = await manager.GetFileRecordInfo(new FileInfo(path));

            manager.UpdateCustomData(record, "CPU", "GPU", "RAM", "Board", "Game", "after");

            Assert.AreEqual("before", session.Info.Comment);
            Assert.AreEqual("after", CreateRecordManager().LoadData(path).Info.Comment);
        }

        [TestMethod]
        public async Task FailedWrite_LeavesTheLoadedSessionAlone()
        {
            var manager = CreateRecordManager();
            string path = WriteJsonRecord("a.json", "before");
            var session = manager.LoadData(path);
            var record = await manager.GetFileRecordInfo(new FileInfo(path));
            File.SetAttributes(path, FileAttributes.ReadOnly);

            manager.UpdateCustomData(record, "CPU", "GPU", "RAM", "Board", "Game", "after");

            Assert.AreEqual("before", session.Info.Comment);
            Assert.AreEqual("before", CreateRecordManager().LoadData(path).Info.Comment);
        }

        [TestMethod]
        public void UpdateFrom_TakesOverEveryPropertyAndNotifiesTheBindings()
        {
            var entry = (FileRecordInfo)FileRecordInfo.Create(new FileInfo(Path.Combine(_folder, "a.json")),
                RecordFiles.SessionWith("Game", RecordFiles.Run()));
            var update = (FileRecordInfo)FileRecordInfo.Create(new FileInfo(Path.Combine(_folder, "b.json")),
                RecordFiles.SessionWith("Game", RecordFiles.Run()));
            var properties = typeof(FileRecordInfo)
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.CanWrite)
                .ToArray();
            // Every value of the update differs from the entry, so a property left out shows.
            foreach (var property in properties)
                property.SetValue(update, OtherValue(property, property.GetValue(entry)));
            var lines = typeof(FileRecordInfo).GetField("_lines", BindingFlags.Instance | BindingFlags.NonPublic);
            lines.SetValue(update, new[] { "line" });
            var raised = new List<string>();
            entry.PropertyChanged += (sender, args) => raised.Add(args.PropertyName);

            entry.UpdateFrom(update);

            foreach (var property in properties)
                Assert.AreEqual(property.GetValue(update), property.GetValue(entry), property.Name);
            Assert.AreSame(lines.GetValue(update), lines.GetValue(entry));
            Assert.AreEqual(update.CreationTimestamp, entry.CreationTimestamp);
            CollectionAssert.AreEqual(new[] { string.Empty }, raised);
        }

        private static object OtherValue(PropertyInfo property, object current)
        {
            if (property.PropertyType == typeof(string))
                return "other " + property.Name;
            if (property.PropertyType == typeof(bool))
                return !(bool)current;
            if (property.PropertyType == typeof(double))
                return (double)current + 42;
            if (property.PropertyType == typeof(FileInfo))
                return new FileInfo(Path.Combine(Path.GetTempPath(), "other.json"));
            throw new AssertFailedException($"No other value for {property.Name} ({property.PropertyType}).");
        }

        private static string[] Metadata(ISessionInfo info)
            => new[] { info.Processor, info.GPU, info.SystemRam, info.Motherboard, info.GameName, info.Comment, info.ResolutionInfo };

        private string WriteJsonRecord(string name, string comment, string hash = "hash")
        {
            var session = RecordFiles.SessionWith("Game", RecordFiles.Run());
            session.Hash = hash;
            session.Info.Processor = "CPU";
            session.Info.GPU = "GPU";
            session.Info.SystemRam = "RAM";
            session.Info.Motherboard = "Board";
            session.Info.Comment = comment;
            session.Info.ResolutionInfo = "1920x1080";
            string path = Path.Combine(_folder, name);
            File.WriteAllText(path, JsonConvert.SerializeObject(session));
            return path;
        }

        private static RecordManager CreateRecordManager()
            => new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null, new EventAggregator(), null, null);
    }
}
