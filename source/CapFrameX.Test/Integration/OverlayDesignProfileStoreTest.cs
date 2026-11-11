using System;
using System.IO;
using System.Linq;
using System.Text;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayDesignProfileStoreTest
    {
        private string _folder;
        private string LibraryPath => Path.Combine(_folder, "OverlayDesigns", "profiles.json");

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX-profile-tests", Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void CrudPersistsNamesDocumentsAndLastSelectionTogether()
        {
            var store = Open();
            var first = store.Create("  Gaming  ", Design(1));
            var second = store.Duplicate(first.Id, "Benchmark");
            Assert.AreEqual("Gaming", first.Name);
            Assert.AreEqual("Benchmark", (string)JObject.Parse(second.DesignJson)["name"]);
            Assert.AreEqual(second.Id, store.ActiveProfileId);
            store.Save(first.Id, Design(2));
            store.Rename(first.Id, "CPU test");
            store.SetActive(first.Id);

            store = Open();
            Assert.AreEqual(first.Id, store.ActiveProfileId);
            Assert.AreEqual("CPU test", store.Get(first.Id).Name);
            Assert.AreEqual("CPU test", (string)JObject.Parse(store.Get(first.Id).DesignJson)["name"]);
            Assert.AreEqual(2, Value(store.Get(first.Id).DesignJson));
            Assert.AreEqual(1, Value(store.Get(second.Id).DesignJson));
            store.Delete(first.Id);
            store = Open();
            Assert.AreEqual(second.Id, store.ActiveProfileId);
            Assert.AreEqual(1, store.Profiles.Count);
            Assert.ThrowsExactly<InvalidOperationException>(() => store.Delete(second.Id));
        }

        [TestMethod]
        public void CanonicalFormattingDoesNotMakeSessionDirty()
        {
            var session = Session(Open());
            session.SetWorkingTemplate(JObject.Parse(session.WorkingTemplateJson).ToString(Formatting.Indented));
            Assert.IsFalse(session.IsDirty);
            session.SetWorkingTemplate(Design(2, "Initial"));
            Assert.IsTrue(session.IsDirty);
            session.SetWorkingTemplate(Design(1, "Initial"));
            Assert.IsFalse(session.IsDirty);
        }

        [TestMethod]
        public void CancelAndDiscardTransitionsPreserveOrResetExactlyTheWorkingDocument()
        {
            var store = Open();
            var session = Session(store);
            string initialId = session.ActiveProfile.Id;
            var other = store.Create("Other", Design(3));
            store.SetActive(initialId);
            session.SetWorkingTemplate(Design(2));
            string unsaved = session.WorkingTemplateJson;

            Assert.IsFalse(session.TrySwitch(other.Id, OverlayDesignUnsavedChangesDecision.Cancel));
            Assert.IsFalse(session.TryClose(OverlayDesignUnsavedChangesDecision.Cancel));
            Assert.IsFalse(session.Create("Cancelled", Design(4), OverlayDesignUnsavedChangesDecision.Cancel));
            Assert.IsFalse(session.DeleteActive(OverlayDesignUnsavedChangesDecision.Cancel));
            Assert.AreEqual(initialId, session.ActiveProfile.Id);
            Assert.AreEqual(initialId, store.ActiveProfileId);
            Assert.AreEqual(unsaved, session.WorkingTemplateJson);
            Assert.AreEqual(2, store.Profiles.Count);

            Assert.IsTrue(session.TrySwitch(other.Id, OverlayDesignUnsavedChangesDecision.Discard));
            Assert.IsFalse(session.IsDirty);
            Assert.AreEqual(3, Value(session.WorkingTemplateJson));
            Assert.AreEqual(1, Value(store.Get(initialId).DesignJson));
            Assert.AreEqual(other.Id, Open().ActiveProfileId);
        }

        [TestMethod]
        public void SaveBeforeSwitchAndCloseSurvivesAReopenedSession()
        {
            var store = Open();
            var session = Session(store);
            string initialId = session.ActiveProfile.Id;
            var other = store.Create("Other", Design(3));
            store.SetActive(initialId);
            session.SetWorkingTemplate(Design(2));
            Assert.IsTrue(session.TrySwitch(other.Id, OverlayDesignUnsavedChangesDecision.Save));
            Assert.AreEqual(2, Value(Open().Get(initialId).DesignJson));
            session.SetWorkingTemplate(Design(4));
            Assert.IsTrue(session.TryClose(OverlayDesignUnsavedChangesDecision.Save));
            Assert.IsFalse(session.IsDirty);
            var reopened = Session(Open());
            Assert.AreEqual(other.Id, reopened.ActiveProfile.Id);
            Assert.AreEqual(4, Value(reopened.WorkingTemplateJson));
        }

        [TestMethod]
        public void InvalidEditsCannotBeSavedDuplicatedOrSilentlyDiscarded()
        {
            var store = Open();
            var session = Session(store);
            var other = store.Create("Other", Design(3));
            store.SetActive(session.ActiveProfile.Id);
            session.MarkInvalidEdit();
            Assert.IsTrue(session.IsDirty);
            Assert.ThrowsExactly<InvalidOperationException>(() => session.Save());
            Assert.ThrowsExactly<InvalidOperationException>(() => session.Duplicate("Invalid"));
            Assert.ThrowsExactly<InvalidOperationException>(() => session.TryClose(OverlayDesignUnsavedChangesDecision.Save));
            Assert.ThrowsExactly<InvalidOperationException>(() => session.TrySwitch(other.Id, OverlayDesignUnsavedChangesDecision.Save));
            Assert.IsTrue(session.HasValidationErrors);
            Assert.IsFalse(session.TryClose(OverlayDesignUnsavedChangesDecision.Cancel));
            Assert.IsTrue(session.TryClose(OverlayDesignUnsavedChangesDecision.Discard));
            Assert.IsFalse(session.HasValidationErrors);
            Assert.IsFalse(session.IsDirty);

            Assert.ThrowsExactly<JsonReaderException>(() => session.SetWorkingTemplate("{"));
            Assert.IsTrue(session.HasValidationErrors);
            Assert.IsTrue(session.TrySwitch(other.Id, OverlayDesignUnsavedChangesDecision.Discard));
            Assert.IsFalse(session.HasValidationErrors);
        }

        [TestMethod]
        public void DuplicateSavesWorkingEditsWithoutChangingOriginalAndRenameRetainsDirtyEdits()
        {
            var store = Open();
            var session = Session(store);
            string originalId = session.ActiveProfile.Id;
            session.SetWorkingTemplate(Design(2));
            session.Rename("Renamed");
            Assert.IsTrue(session.IsDirty);
            Assert.AreEqual(2, Value(session.WorkingTemplateJson));
            Assert.AreEqual(1, Value(store.Get(originalId).DesignJson));
            Assert.AreEqual("Renamed", (string)JObject.Parse(session.WorkingTemplateJson)["name"]);
            session.Duplicate("Copy");
            Assert.IsFalse(session.IsDirty);
            Assert.AreNotEqual(originalId, session.ActiveProfile.Id);
            Assert.AreEqual(2, Value(session.WorkingTemplateJson));
            Assert.AreEqual("Copy", (string)JObject.Parse(session.WorkingTemplateJson)["name"]);
            Assert.AreEqual(1, Value(Open().Get(originalId).DesignJson));
        }

        [TestMethod]
        public void RenameKeepsCleanProfilesCleanAndDoesNotClearInvalidEdits()
        {
            var session = Session(Open());
            session.Rename("Renamed");
            Assert.IsFalse(session.IsDirty);
            session.MarkInvalidEdit();
            session.Rename("Still invalid");
            Assert.IsTrue(session.HasValidationErrors);
            Assert.IsTrue(session.IsDirty);
            Assert.ThrowsExactly<InvalidOperationException>(() => session.Save());
        }

        [TestMethod]
        public void FailedCreateOrDuplicateDoesNotDiscardWorkingEdits()
        {
            var store = Open();
            var session = Session(store);
            session.SetWorkingTemplate(Design(2));
            string working = session.WorkingTemplateJson;
            Assert.ThrowsExactly<ArgumentException>(() => session.Create("Initial", Design(3), OverlayDesignUnsavedChangesDecision.Discard));
            Assert.ThrowsExactly<ArgumentException>(() => session.Duplicate("../invalid"));
            Assert.IsTrue(session.IsDirty);
            Assert.AreEqual(working, session.WorkingTemplateJson);
            Assert.AreEqual(1, store.Profiles.Count);
            Assert.AreEqual(1, Value(store.Get(session.ActiveProfile.Id).DesignJson));
        }

        [TestMethod]
        public void InvalidNamesIdentifiersDuplicatesAndOversizedDesignsNeverModifyLibrary()
        {
            var store = Open();
            var profile = store.Create("Original", Design(1));
            byte[] before = File.ReadAllBytes(LibraryPath);
            foreach (string name in new[] { "", "  ", "../escape", "..\\escape", "new\nline", new string('a', 81) })
                Assert.ThrowsExactly<ArgumentException>(() => store.Create(name, Design(2)));
            Assert.ThrowsExactly<ArgumentException>(() => store.Create("ORIGINAL", Design(2)));
            Assert.ThrowsExactly<ArgumentException>(() => store.Get("../profiles"));
            Assert.ThrowsExactly<ArgumentException>(() => store.Save(profile.Id, new string('a', OverlayDesignProfileStore.MaximumDesignBytes + 1)));
            Assert.ThrowsExactly<JsonReaderException>(() => store.Save(profile.Id, "{"));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(LibraryPath));
            Assert.AreEqual(1, store.Profiles.Count);
        }

        [TestMethod]
        public void ProfileLimitIsEnforcedWithoutLosingExistingProfiles()
        {
            var store = Open();
            for (int index = 0; index < OverlayDesignProfileStore.MaximumProfiles; index++)
                store.Create("Profile " + index, Design(index));
            Assert.ThrowsExactly<InvalidOperationException>(() => store.Create("Too many", Design(1)));
            Assert.AreEqual(OverlayDesignProfileStore.MaximumProfiles, Open().Profiles.Count);
        }

        [TestMethod]
        public void DamagedPrimaryRecoversBackupAndRetainsBothEvidenceAndGoodBackupOnSave()
        {
            var store = Open();
            var profile = store.Create("Original", Design(1));
            store.Save(profile.Id, Design(2));
            byte[] goodBackup = File.ReadAllBytes(LibraryPath + ".bak");
            File.WriteAllText(LibraryPath, "{ incomplete", Encoding.UTF8);
            var recovered = Open();
            Assert.IsFalse(string.IsNullOrWhiteSpace(recovered.RecoveryWarning));
            Assert.AreEqual(1, Value(recovered.Get(profile.Id).DesignJson));
            recovered.Save(profile.Id, Design(3));
            Assert.IsNull(recovered.RecoveryWarning);
            Assert.AreEqual(3, Value(Open().Get(profile.Id).DesignJson));
            CollectionAssert.AreEqual(goodBackup, File.ReadAllBytes(LibraryPath + ".bak"));
            string retained = Directory.GetFiles(Path.GetDirectoryName(LibraryPath), "profiles.corrupt-*.json").Single();
            Assert.AreEqual("{ incomplete", File.ReadAllText(retained));
        }

        [TestMethod]
        public void MissingPrimaryRecoversBackupButDamagedFilesWithoutBackupAreNeverReplaced()
        {
            var store = Open();
            var profile = store.Create("Original", Design(1));
            store.Save(profile.Id, Design(2));
            File.Delete(LibraryPath);
            var recovered = Open();
            Assert.AreEqual(1, Value(recovered.Get(profile.Id).DesignJson));
            recovered.Save(profile.Id, Design(3));
            Assert.AreEqual(3, Value(Open().Get(profile.Id).DesignJson));
            File.Delete(LibraryPath + ".bak");
            File.WriteAllText(LibraryPath, "broken");
            Assert.ThrowsExactly<InvalidDataException>(() => Open());
            Assert.AreEqual("broken", File.ReadAllText(LibraryPath));
        }

        [TestMethod]
        public void SwitchingAfterRecoveryRetainsDamagedFileAndPersistsTheSelection()
        {
            var store = Open();
            var first = store.Create("First", Design(1));
            var second = store.Create("Second", Design(2));
            store.Save(first.Id, Design(3));
            File.WriteAllText(LibraryPath, "interrupted write");
            var recovered = Open();
            var session = Session(recovered);
            Assert.AreEqual(second.Id, session.ActiveProfile.Id);
            Assert.IsTrue(session.TrySwitch(first.Id, OverlayDesignUnsavedChangesDecision.Cancel));
            Assert.IsNull(recovered.RecoveryWarning);
            Assert.AreEqual(first.Id, Open().ActiveProfileId);
            Assert.AreEqual(1, Value(session.WorkingTemplateJson));
            string retained = Directory.GetFiles(Path.GetDirectoryName(LibraryPath), "profiles.corrupt-*.json").Single();
            Assert.AreEqual("interrupted write", File.ReadAllText(retained));
        }

        [TestMethod]
        public void CorruptBackupAndUnsupportedFutureVersionsNeverOverwriteUserFiles()
        {
            var store = Open();
            var profile = store.Create("Original", Design(1));
            store.Save(profile.Id, Design(2));
            var document = JObject.Parse(File.ReadAllText(LibraryPath));
            document["Version"] = 2;
            document["FutureData"] = "keep";
            document["Profiles"] = "A new version may change this schema completely.";
            File.WriteAllText(LibraryPath, document.ToString());
            byte[] future = File.ReadAllBytes(LibraryPath);
            Assert.ThrowsExactly<NotSupportedException>(() => Open());
            CollectionAssert.AreEqual(future, File.ReadAllBytes(LibraryPath));
            File.WriteAllText(LibraryPath, "broken primary");
            File.WriteAllText(LibraryPath + ".bak", "broken backup");
            Assert.ThrowsExactly<InvalidDataException>(() => Open());
            Assert.AreEqual("broken primary", File.ReadAllText(LibraryPath));
            Assert.AreEqual("broken backup", File.ReadAllText(LibraryPath + ".bak"));
        }

        [TestMethod]
        public void ASecondWriterCannotOverwriteChangesFromAnotherSession()
        {
            var firstStore = Open();
            var profile = firstStore.Create("Original", Design(1));
            var secondStore = Open();
            firstStore.Save(profile.Id, Design(2));
            Assert.ThrowsExactly<IOException>(() => secondStore.Save(profile.Id, Design(3)));
            Assert.AreEqual(1, Value(secondStore.Get(profile.Id).DesignJson));
            Assert.AreEqual(2, Value(Open().Get(profile.Id).DesignJson));
        }

        [TestMethod]
        public void FailedAtomicWriteLeavesSavedProfileAndWorkingEditsIntact()
        {
            var store = Open();
            var session = Session(store);
            var other = store.Create("Other", Design(3));
            store.SetActive(session.ActiveProfile.Id);
            session.SetWorkingTemplate(Design(2));
            string activeId = session.ActiveProfile.Id;
            byte[] before = File.ReadAllBytes(LibraryPath);
            // Read sharing allows the concurrency check, but denies File.Replace its delete access.
            using (var locked = new FileStream(LibraryPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.ThrowsExactly<IOException>(() => session.Save());
                Assert.ThrowsExactly<IOException>(() => session.TrySwitch(other.Id, OverlayDesignUnsavedChangesDecision.Discard));
            }
            Assert.IsTrue(session.IsDirty);
            Assert.AreEqual(2, Value(session.WorkingTemplateJson));
            Assert.AreEqual(activeId, session.ActiveProfile.Id);
            Assert.AreEqual(1, Value(store.Get(activeId).DesignJson));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(LibraryPath));
            Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(LibraryPath), "*.tmp").Length);
            session.Save();
            Assert.IsFalse(session.IsDirty);
            Assert.AreEqual(2, Value(Open().Get(activeId).DesignJson));
        }

        [TestMethod]
        public void InvalidLibraryReferencesAndDesignsRecoverLastValidSnapshot()
        {
            var store = Open();
            var profile = store.Create("Original", Design(1));
            store.Save(profile.Id, Design(2));
            var document = JObject.Parse(File.ReadAllText(LibraryPath));
            document["ActiveProfileId"] = Guid.NewGuid().ToString("N");
            File.WriteAllText(LibraryPath, document.ToString());
            Assert.AreEqual(1, Value(Open().Get(profile.Id).DesignJson));
            document["ActiveProfileId"] = profile.Id;
            document["Profiles"][0]["DesignJson"] = "{}";
            File.WriteAllText(LibraryPath, document.ToString());
            Assert.AreEqual(1, Value(Open().Get(profile.Id).DesignJson));
        }

        [TestMethod]
        public void SystemTextJsonValidationErrorsAlsoRecoverTheBackup()
        {
            string ValidateWithSystemTextJson(string json)
            {
                using (System.Text.Json.JsonDocument.Parse(json)) { }
                return Canonicalize(json);
            }
            var store = new OverlayDesignProfileStore(_folder, ValidateWithSystemTextJson);
            var profile = store.Create("Original", Design(1));
            store.Save(profile.Id, Design(2));
            var document = JObject.Parse(File.ReadAllText(LibraryPath));
            document["Profiles"][0]["DesignJson"] = "{";
            File.WriteAllText(LibraryPath, document.ToString());
            var recovered = new OverlayDesignProfileStore(_folder, ValidateWithSystemTextJson);
            Assert.IsFalse(string.IsNullOrWhiteSpace(recovered.RecoveryWarning));
            Assert.AreEqual(1, Value(recovered.Get(profile.Id).DesignJson));
        }

        [TestMethod]
        public void OversizedLibraryIsRejectedBeforeReplacingTheLastValidSnapshot()
        {
            var store = Open();
            var largeDesign = JObject.Parse(Design(1));
            largeDesign["padding"] = new string('x', 900000);
            string json = largeDesign.ToString(Formatting.None);
            bool reachedLimit = false;
            for (int index = 0; index < 24; index++)
            {
                int previousCount = store.Profiles.Count;
                try
                {
                    store.Create("Profile " + index, json);
                }
                catch (InvalidOperationException)
                {
                    reachedLimit = true;
                    Assert.AreEqual(previousCount, store.Profiles.Count);
                    Assert.AreEqual(previousCount, Open().Profiles.Count);
                    break;
                }
            }
            Assert.IsTrue(reachedLimit, "The total library must enforce a limit even when each design is individually valid.");
            Assert.IsTrue(new FileInfo(LibraryPath).Length <= OverlayDesignProfileStore.MaximumLibraryBytes);
        }

        private OverlayDesignProfileStore Open() => new OverlayDesignProfileStore(_folder, Canonicalize);

        private static OverlayDesignProfileSession Session(OverlayDesignProfileStore store)
        {
            var session = new OverlayDesignProfileSession(store);
            session.Initialize("Initial", Design(1));
            return session;
        }

        private static string Canonicalize(string json)
        {
            var document = JObject.Parse(json);
            if (document["value"]?.Type != JTokenType.Integer)
                throw new InvalidDataException("A design requires an integer value.");
            return document.ToString(Formatting.None);
        }

        private static string Design(int value, string name = "Example") => new JObject
        {
            ["name"] = name,
            ["value"] = value
        }.ToString(Formatting.None);

        private static int Value(string json) => (int)JObject.Parse(json)["value"];
    }
}
