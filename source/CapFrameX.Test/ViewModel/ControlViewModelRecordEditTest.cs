using System;
using System.IO;
using System.Linq;
using System.Threading;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// Metadata edits in the record list: the files are rewritten off the UI thread, the Changed
    /// events of a rewrite are read once per file, and an entry whose hash did not change takes
    /// the new metadata in place instead of the record being loaded and analysed again.
    /// </summary>
    [STATestClass]
    public class ControlViewModelRecordEditTest
    {
        private string _folder;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.ControlViewModelRecordEditTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void MetadataEdit_UpdatesTheEntryInPlaceWithoutLoadingTheRecordAgain()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new RecordListContext(_folder);
            var entry = context.Model.RecordInfoList.Single();
            var session = context.SelectAndLoad(entry);

            context.Model.CustomComment = "after";
            context.Save();
            context.ReportChanges(path, path, path);
            context.SettleUntil(() => context.RecordInfoUpdates.Any());

            Assert.AreSame(entry, context.Model.RecordInfoList.Single());
            Assert.AreSame(entry, context.Model.SelectedRecordInfo);
            Assert.AreEqual("after", entry.Comment);
            Assert.AreEqual("after", session.Info.Comment, "The loaded session keeps the old metadata.");
            Assert.HasCount(1, context.LoadedSessions, "The record was loaded again.");
            CollectionAssert.AreEqual(new[] { entry }, context.RecordInfoUpdates);
        }

        [TestMethod]
        public void ChangedEventsOfOneRewrite_AreReadOnce()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new RecordListContext(_folder);
            context.SelectAndLoad(context.Model.RecordInfoList.Single());
            context.Model.CustomComment = "after";
            context.Save();

            // Each event comes before the file has been quiet long enough.
            for (int index = 0; index < 4; index++)
            {
                context.ReportChanges(path);
                context.Advance(TimeSpan.FromMilliseconds(300));
            }
            context.SettleUntil(() => context.RecordInfoUpdates.Any());

            Assert.HasCount(1, context.RecordInfoUpdates);
        }

        [TestMethod]
        public void ChangesOfTwoRecords_AreReadSeparately()
        {
            string first = WriteRecord("a.json", "a");
            string second = WriteRecord("b.json", "b", hash: "hash-b");
            using var context = new RecordListContext(_folder);
            var entries = context.Model.RecordInfoList.OrderBy(entry => entry.FullPath).ToArray();
            context.SelectAndLoad(entries[0]);
            context.Model.CustomComment = "a edited";
            context.Save();
            WriteRecord("b.json", "b edited elsewhere", hash: "hash-b");

            context.ReportChanges(first, second, first, second);
            context.SettleUntil(() => entries[1].Comment == "b edited elsewhere" && context.RecordInfoUpdates.Any());

            Assert.AreEqual("a edited", entries[0].Comment);
            CollectionAssert.AreEqual(entries, context.Model.RecordInfoList.OrderBy(entry => entry.FullPath).ToArray());
        }

        [TestMethod]
        public void EditByAnotherWriter_ReloadsTheSelectedRecord()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new RecordListContext(_folder);
            var entry = context.Model.RecordInfoList.Single();
            context.SelectAndLoad(entry);

            // Same capture, new comment, but the loaded session was not updated by this edit.
            WriteRecord("a.json", "edited elsewhere");
            context.ReportChanges(path);
            context.SettleUntil(() => context.LoadedSessions.Count == 2);

            Assert.AreSame(entry, context.Model.SelectedRecordInfo);
            Assert.AreEqual("edited elsewhere", entry.Comment);
            Assert.AreEqual("edited elsewhere", context.Model.CustomComment);
            Assert.AreEqual("edited elsewhere", context.LoadedSessions[1].CurrentSession.Info.Comment);
            CollectionAssert.AreEqual(new[] { entry }, context.RecordInfoUpdates);
        }

        [TestMethod]
        public void NewCaptureUnderTheSamePath_ReplacesTheEntry()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new RecordListContext(_folder);
            var entry = context.Model.RecordInfoList.Single();
            context.SelectAndLoad(entry);

            WriteRecord("a.json", "another capture", hash: "other hash");
            context.ReportChanges(path);
            context.SettleUntil(() => context.LoadedSessions.Count == 2);

            var replacement = context.Model.RecordInfoList.Single();
            Assert.AreNotSame(entry, replacement);
            Assert.AreSame(replacement, context.Model.SelectedRecordInfo);
            Assert.AreEqual("other hash", context.LoadedSessions[1].CurrentSession.Hash);
        }

        [TestMethod]
        public void OwnEdit_KeepsWhatIsBeingTypedInAnotherField()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new RecordListContext(_folder);
            context.SelectAndLoad(context.Model.RecordInfoList.Single());
            context.Model.CustomComment = "after";
            context.Save();

            context.Model.CustomCpuDescription = "typing";
            context.ReportChanges(path);
            context.SettleUntil(() => context.RecordInfoUpdates.Any());

            Assert.AreEqual("typing", context.Model.CustomCpuDescription);
            Assert.AreEqual("after", context.Model.CustomComment);
        }

        [TestMethod]
        public void SaveDescriptions_WritesTheRecordOffTheUiThread()
        {
            string path = WriteRecord("a.json", "before");
            using var context = new RecordListContext(_folder);
            context.SelectAndLoad(context.Model.RecordInfoList.Single());
            using var gate = new ManualResetEventSlim();
            context.Queue.Enqueue(() => gate.Wait(TimeSpan.FromSeconds(10)));

            context.Model.CustomComment = "after";
            context.Model.SaveDescriptions();

            Assert.AreEqual("before", ReadComment(path), "The record was written on the UI thread.");
            gate.Set();
            Assert.IsTrue(context.Queue.WaitForPendingWrites(TimeSpan.FromSeconds(10)));
            Assert.AreEqual("after", ReadComment(path));
        }

        [TestMethod]
        public void EditOfSeveralRecords_WritesTheEditedFieldAndKeepsTheirOwnValues()
        {
            string first = WriteRecord("a.json", "a", cpu: "CPU A");
            string second = WriteRecord("b.json", "b", cpu: "CPU B", hash: "hash-b");
            using var context = new RecordListContext(_folder);
            var entries = context.Model.RecordInfoList.OrderBy(entry => entry.FullPath).ToArray();
            context.SelectAndLoad(entries);

            context.Model.CustomComment = "both";
            context.Save();

            Assert.AreEqual("both", ReadComment(first));
            Assert.AreEqual("both", ReadComment(second));
            Assert.AreEqual("CPU A", ReadSession(first).Info.Processor);
            Assert.AreEqual("CPU B", ReadSession(second).Info.Processor);
        }

        [TestMethod]
        public void EditOfSeveralRecords_SkipsARecordThatCannotBeRead()
        {
            string first = WriteRecord("a.json", "a");
            string second = WriteRecord("b.json", "b", hash: "hash-b");
            using var context = new RecordListContext(_folder);
            var entries = context.Model.RecordInfoList.OrderBy(entry => entry.FullPath).ToArray();
            context.SelectAndLoad(entries);
            File.Delete(first);

            context.Model.CustomComment = "both";
            context.Save();

            Assert.IsFalse(File.Exists(first));
            Assert.AreEqual("both", ReadComment(second));
        }

        private string WriteRecord(string name, string comment, string hash = null, string cpu = "CPU")
            => RecordListContext.WriteRecord(_folder, name, comment, hash, cpu);

        private static CapFrameX.Data.Session.Contracts.ISession ReadSession(string path)
            => new RecordManager(new Mock<ILogger<RecordManager>>().Object,
                null, null, null, null, null, null, null, new EventAggregator(), null, null).LoadData(path);

        private static string ReadComment(string path) => ReadSession(path).Info.Comment;
    }
}
