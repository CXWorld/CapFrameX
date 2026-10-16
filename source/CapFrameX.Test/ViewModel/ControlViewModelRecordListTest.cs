using System;
using System.IO;
using System.Linq;
using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.ViewModel
{
    /// <summary>
    /// The record list fills in batches while a folder is read: one slow file no longer keeps
    /// every other record out of the list until the last file is done.
    /// </summary>
    [STATestClass]
    public class ControlViewModelRecordListTest
    {
        private string _folder;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX.ControlViewModelRecordListTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void RecordsAppearWhileAnotherFileIsStillBeingRead()
        {
            WriteRecords("r", 5);
            string slow = RecordListContext.WriteRecord(_folder, "slow.json");
            using var reading = RecordListContext.BlockReading(slow);
            using var context = new RecordListContext(_folder, waitForList: false);

            context.PumpUntil(() => context.Model.RecordInfoList.Count == 5, "the readable records");

            Assert.IsTrue(context.Model.DirectoryLoading);
            Assert.IsFalse(context.Model.ShowLoadingHint, "The loading hint covers the listed records.");

            reading.Dispose();
            context.Advance(TimeSpan.FromSeconds(1));
            context.PumpUntil(() => !context.Model.DirectoryLoading, "the end of the listing");
            Assert.HasCount(6, context.Model.RecordInfoList);
            Assert.IsFalse(context.Model.DirectoryIsEmpty);
        }

        [TestMethod]
        public void ReadRecordsWaitForTheBatchWindow()
        {
            WriteRecords("r", 5);
            string slow = RecordListContext.WriteRecord(_folder, "slow.json");
            using var reading = RecordListContext.BlockReading(slow);
            using var context = new RecordListContext(_folder, waitForList: false, batchWindowOnClock: true);

            context.RunDueWork();

            Assert.IsEmpty(context.Model.RecordInfoList, "Each record went to the list on its own.");
            Assert.IsTrue(context.Model.ShowLoadingHint);

            context.Advance(ControlViewModel.RecordListBatchTime);

            CollectionAssert.AreEquivalent(new[] { "r0.json", "r1.json", "r2.json", "r3.json", "r4.json" }, context.ListedNames);
            Assert.IsTrue(context.Model.DirectoryLoading);
            Assert.IsFalse(context.Model.ShowLoadingHint);

            reading.Dispose();
            context.Advance(TimeSpan.FromSeconds(1));
            context.PumpUntil(() => !context.Model.DirectoryLoading, "the end of the listing");
            Assert.HasCount(6, context.Model.RecordInfoList);
        }

        [TestMethod]
        public void NewListing_DropsTheBatchesOfThePreviousOne()
        {
            var first = WriteRecords("a", 3).Append(RecordListContext.WriteRecord(_folder, "slow.json")).ToArray();
            var second = WriteRecords("b", 2);
            using var reading = RecordListContext.BlockReading(first.Last());
            using var context = new RecordListContext(_folder, first.Select(path => new FileInfo(path)),
                waitForList: false, batchWindowOnClock: true);
            context.RunDueWork();

            // The new listing is on its way to the UI thread when the old one's batch closes.
            context.ShowListing(second.Select(path => new FileInfo(path)));
            context.Clock.AdvanceBy(ControlViewModel.RecordListBatchTime);
            context.PumpUntil(() => !context.Model.DirectoryLoading, "the new listing");
            RecordListContext.Pump();

            CollectionAssert.AreEquivalent(new[] { "b0.json", "b1.json" }, context.ListedNames);
        }

        [TestMethod]
        public void RecordCreatedWhileTheFolderIsRead_IsListedOnce()
        {
            string created = RecordListContext.WriteRecord(_folder, "new.json");
            string slow = RecordListContext.WriteRecord(_folder, "slow.json");
            using var reading = RecordListContext.BlockReading(slow);
            using var context = new RecordListContext(_folder, waitForList: false, batchWindowOnClock: true);
            context.RunDueWork();

            // Its Created event arrives before the listing's batch with it.
            context.ReportCreated(created);
            context.RunDueWork();
            CollectionAssert.AreEqual(new[] { "new.json" }, context.ListedNames);
            context.Advance(ControlViewModel.RecordListBatchTime);

            reading.Dispose();
            context.Advance(TimeSpan.FromSeconds(1));
            context.PumpUntil(() => !context.Model.DirectoryLoading, "the end of the listing");
            CollectionAssert.AreEquivalent(new[] { "new.json", "slow.json" }, context.ListedNames);
        }

        [TestMethod]
        public void CreatedEventOfAListedRecord_DoesNotListItAgain()
        {
            string path = RecordListContext.WriteRecord(_folder, "a.json");
            using var context = new RecordListContext(_folder);

            context.ReportCreated(path);
            context.Advance(TimeSpan.FromSeconds(1));
            context.RunDueWork();

            CollectionAssert.AreEqual(new[] { "a.json" }, context.ListedNames);
        }

        [TestMethod]
        public void EmptyFolder_EndsLoadingAndShowsTheEmptyHint()
        {
            using var context = new RecordListContext(_folder, waitForList: false, batchWindowOnClock: true);

            context.PumpUntil(() => !context.Model.DirectoryLoading, "the end of the listing");

            Assert.IsEmpty(context.Model.RecordInfoList);
            Assert.IsFalse(context.Model.ShowLoadingHint);
            Assert.IsTrue(context.Model.DirectoryIsEmpty);
        }

        private string[] WriteRecords(string prefix, int count)
            => Enumerable.Range(0, count)
                .Select(index => RecordListContext.WriteRecord(_folder, $"{prefix}{index}.json"))
                .ToArray();
    }
}
