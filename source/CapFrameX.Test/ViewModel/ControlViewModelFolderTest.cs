using System;
using System.IO;
using CapFrameX.EventAggregation.Messages;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// The capture root and the observed folder of the record list. The folder tree shows the
    /// root and the folders below it, so the record list keeps the observed folder among them.
    /// </summary>
    [STATestClass]
    public class ControlViewModelFolderTest
    {
        private const string DocumentsPlaceholder = @"MyDocuments\CapFrameX\Captures";

        private string _folder;
        private string _root;
        private string _elsewhere;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.ControlViewModelFolderTests", Guid.NewGuid().ToString("N"));
            _root = Path.Combine(_folder, "Captures");
            _elsewhere = Path.Combine(_folder, "Elsewhere");
            Directory.CreateDirectory(Path.Combine(_root, "Cloud"));
            Directory.CreateDirectory(_elsewhere);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void EnsureRootDirectory_KeepsARootThatExists()
        {
            using var context = NewContext(root: DocumentsPlaceholder, observed: _root);
            context.PathService.Setup(service => service.ResolveDocumentsPlaceholder(DocumentsPlaceholder)).Returns(_root);

            Assert.AreEqual(_root, context.Model.EnsureRootDirectory());
            Assert.AreEqual(DocumentsPlaceholder, context.Configuration.CaptureRootDirectory);
        }

        [TestMethod]
        public void EnsureRootDirectory_ReplacesARootThatIsGoneByTheDefaultCapturesFolder()
        {
            string defaultFolder = Path.Combine(_folder, "Default", "Captures");
            using var context = NewContext(root: Path.Combine(_folder, "Deleted"), observed: _root);
            context.PathService.SetupGet(service => service.CapturesFolder).Returns(defaultFolder);

            Assert.AreEqual(defaultFolder, context.Model.EnsureRootDirectory());
            Assert.AreEqual(defaultFolder, context.Configuration.CaptureRootDirectory);
            Assert.IsTrue(Directory.Exists(defaultFolder));
        }

        [TestMethod]
        public void ObserveRootDirectory_ObservesTheRootAsItIsWritten()
        {
            // Issue 485: the observed folder of another installation. The root may be the
            // placeholder that follows the documents folder; the observed folder stays one too.
            using var context = NewContext(root: DocumentsPlaceholder, observed: _elsewhere);
            context.PathService.Setup(service => service.ResolveDocumentsPlaceholder(DocumentsPlaceholder)).Returns(_root);

            context.Model.ObserveRootDirectory();

            Assert.AreEqual(DocumentsPlaceholder, context.Configuration.ObservedDirectory);
            context.Observer.Verify(observer => observer.ObserveDirectory(_root), Times.Once);
        }

        [TestMethod]
        public void ObservingTheDownloadFolder_MovesTheRootToADownloadFolderElsewhere()
        {
            // The Cloud page stores its download folder as the observed one before it asks for it.
            using var context = NewContext(root: _root, observed: _elsewhere);
            int treeUpdates = 0;
            using var updates = context.Model.TreeViewUpdateStream.Subscribe(_ => treeUpdates++);

            context.Events.GetEvent<PubSubEvent<AppMessages.SelectCloudFolder>>().Publish(new AppMessages.SelectCloudFolder());

            Assert.AreEqual(_elsewhere, context.Model.RootDirectory);
            Assert.AreEqual(1, treeUpdates);
        }

        [TestMethod]
        [DataRow("Cloud", DisplayName = "a download folder below the root")]
        [DataRow(@"..\Deleted", DisplayName = "a download folder that is gone")]
        public void ObservingTheDownloadFolder_KeepsTheRootOtherwise(string downloadFolder)
        {
            using var context = NewContext(root: _root, observed: Path.Combine(_root, downloadFolder));
            int treeUpdates = 0;
            using var updates = context.Model.TreeViewUpdateStream.Subscribe(_ => treeUpdates++);

            context.Events.GetEvent<PubSubEvent<AppMessages.SelectCloudFolder>>().Publish(new AppMessages.SelectCloudFolder());

            Assert.AreEqual(_root, context.Model.RootDirectory);
            Assert.AreEqual(1, treeUpdates);
        }

        private RecordListContext NewContext(string root, string observed)
        {
            var context = new RecordListContext(_root);
            context.Configuration.CaptureRootDirectory = root;
            context.Configuration.ObservedDirectory = observed;
            return context;
        }
    }
}
