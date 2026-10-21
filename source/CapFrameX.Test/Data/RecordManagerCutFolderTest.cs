using CapFrameX.Capture.Contracts;
using CapFrameX.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Test.ViewModel;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Test.Data
{
    /// <summary>
    /// The folder a cut record is written to: the Analysis page takes it when the cut is clicked,
    /// since the cut waits for the record write queue and the record folder can change meanwhile.
    /// </summary>
    [TestClass]
    public class RecordManagerCutFolderTest
    {
        private string _firstFolder;
        private string _switchedFolder;
        private BehaviorSubject<DirectoryInfo> _observedDirectory;
        private RecordManager _manager;

        [TestInitialize]
        public void Initialize()
        {
            var root = Path.Combine(Path.GetTempPath(), "CapFrameX.RecordManagerCutFolderTests", Guid.NewGuid().ToString("N"));
            _firstFolder = Directory.CreateDirectory(Path.Combine(root, "first")).FullName;
            _switchedFolder = Directory.CreateDirectory(Path.Combine(root, "switched")).FullName;
            _observedDirectory = new BehaviorSubject<DirectoryInfo>(new DirectoryInfo(_firstFolder));

            var observer = new Mock<IRecordDirectoryObserver>();
            observer.SetupGet(o => o.ObservingDirectoryStream).Returns(_observedDirectory);
            _manager = new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, observer.Object, null, null, null, null, null, new EventAggregator(), null, null);
        }

        [TestCleanup]
        public void Cleanup()
        {
            var root = Path.GetDirectoryName(_firstFolder);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
        }

        [TestMethod]
        public async Task GetRecordDirectory_IsTheObservedFolder()
        {
            Assert.AreEqual(_firstFolder, await _manager.GetRecordDirectory());
        }

        [TestMethod]
        public async Task Cut_IsWrittenToTheFolderTakenAtTheClick()
        {
            string folderAtTheClick = await _manager.GetRecordDirectory();
            // The user switches the record folder while the cut waits.
            _observedDirectory.OnNext(new DirectoryInfo(_switchedFolder));

            await _manager.DuplicateSession(Session(), inverse: false, startTime: 2, endTime: 4, folderAtTheClick);

            Assert.AreEqual(1, Directory.GetFiles(_firstFolder, "*.json").Length);
            Assert.AreEqual(0, Directory.GetFiles(_switchedFolder, "*.json").Length);
        }

        [TestMethod]
        public async Task CutWithoutAFolder_IsWrittenToTheObservedFolder()
        {
            await _manager.DuplicateSession(Session(), inverse: false, startTime: 2, endTime: 4);

            Assert.AreEqual(1, Directory.GetFiles(_firstFolder, "*.json").Length);
        }

        private static Session Session()
        {
            var session = RecordFiles.SessionWith("Game", RecordFiles.Run(Enumerable.Repeat(10d, 1000).ToArray()));
            session.Info.ProcessName = "Game.exe";
            return session;
        }
    }
}
