using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using CapFrameX.Contracts.Configuration;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayDesignDeletionTest
    {
        private const string Scene = "{\"version\":1,\"root\":{\"type\":\"panel\",\"children\":[{\"type\":\"metric\",\"key\":\"fps\"}]}}";
        private string _folder;
        private Mock<IAppConfiguration> _configuration;
        private Subject<(string key, object value)> _changes;
        private OverlayDesignService _service;
        private int _showRequests;

        private string LibraryPath => Path.Combine(_folder, "OverlayDesigns", "profiles.json");

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX-design-deletion-tests", Guid.NewGuid().ToString("N"));
            _configuration = new Mock<IAppConfiguration>();
            _configuration.SetupAllProperties();
            _configuration.Object.EnableHookOverlay = true;
            _changes = new Subject<(string, object)>();
            _configuration.SetupGet(config => config.OnValueChanged).Returns(_changes);
            _service = new OverlayDesignService(_folder, _configuration.Object, () =>
            {
                _showRequests++;
                _configuration.Object.IsOverlayActive = true;
            });
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service.Dispose();
            _changes.Dispose();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void DeletingTheActiveProfileRestoresRowsAfterCommitWithoutChangingVisibility(bool visible)
        {
            var profile = Store().Create("Active design", Scene);
            _service.Activate(profile.Id);
            _configuration.Object.IsOverlayActive = visible;
            int requests = _showRequests;
            int notifications = 0;
            _service.Changed += (_, _) =>
            {
                notifications++;
                Assert.IsFalse(Store().Profiles.Any(item => item.Id == profile.Id), "Notify only after the deletion is persisted.");
                Assert.IsNull(_service.CurrentDesign);
                Assert.IsNull(_configuration.Object.ActiveOverlayDesignProfileId);
            };

            _service.Delete(profile.Id);

            Assert.AreEqual(1, notifications);
            Assert.AreEqual(requests, _showRequests);
            Assert.IsFalse(_service.Profiles.Any(item => item.Id == profile.Id));
            Assert.IsFalse(_service.IsEnabled);
            Assert.IsTrue(_configuration.Object.EnableHookOverlay);
            Assert.IsFalse(_configuration.Object.EnableHookFreeOverlay);
            Assert.AreEqual(visible, _configuration.Object.IsOverlayActive);
            StringAssert.Contains(_service.Status, "Row overlay restored");
        }

        [TestMethod]
        public void DeletingAnotherProfileReadsTheLatestLibraryAndPreservesTheActiveRuntimeSnapshot()
        {
            var active = Store().Create("Active", Scene);
            _service.Activate(active.Id);
            var current = _service.CurrentDesign;
            _service.ReportRuntimeError("Existing renderer error");
            _configuration.Object.IsOverlayActive = false;
            int requests = _showRequests;
            // These changes occurred after the service last loaded its profile list.
            var editorStore = Store();
            var victim = editorStore.Create("Delete this", Scene);
            var latest = editorStore.Create("Keep this new profile", Scene);
            editorStore.Save(active.Id, Scene.Replace("fps", "gpuLoad"));
            int notifications = 0;
            _service.Changed += (_, _) => notifications++;

            _service.Delete(victim.Id);

            Assert.AreSame(current, _service.CurrentDesign, "Deleting another profile must not apply unsynchronized edits to the active scene.");
            Assert.AreEqual(active.Id, _configuration.Object.ActiveOverlayDesignProfileId);
            Assert.AreEqual("Existing renderer error", _service.Status);
            Assert.AreEqual(requests, _showRequests);
            Assert.IsFalse(_configuration.Object.IsOverlayActive);
            Assert.IsTrue(_configuration.Object.EnableHookOverlay);
            Assert.IsFalse(_configuration.Object.EnableHookFreeOverlay);
            Assert.AreEqual(1, notifications);
            Assert.IsFalse(_service.Profiles.Any(profile => profile.Id == victim.Id));
            Assert.IsTrue(_service.Profiles.Any(profile => profile.Id == latest.Id));
            StringAssert.Contains(Store().Get(active.Id).DesignJson, "gpuLoad");
            Assert.AreEqual(latest.Id, Store().ActiveProfileId, "Deleting a different profile preserves the editor selection.");
        }

        [TestMethod]
        public void DeletingAnInactiveProfileAlsoWorksWithRtssWithoutChangingRendererOrVisibility()
        {
            var profile = Store().Create("Delete under RTSS", Scene);
            _configuration.Object.EnableHookOverlay = false;
            _changes.OnNext((nameof(IAppConfiguration.EnableHookOverlay), false));
            _configuration.Object.IsOverlayActive = true;

            _service.Delete(profile.Id);

            Assert.IsFalse(Store().Profiles.Any(item => item.Id == profile.Id));
            Assert.IsNull(_service.CurrentDesign);
            Assert.IsNull(_configuration.Object.ActiveOverlayDesignProfileId);
            Assert.IsFalse(_configuration.Object.EnableHookOverlay);
            Assert.IsFalse(_configuration.Object.EnableHookFreeOverlay);
            Assert.IsTrue(_configuration.Object.IsOverlayActive);
            Assert.AreEqual(0, _showRequests);
        }

        [TestMethod]
        public void DeletingTheLastProfileFailsWithoutRemovingItOrStoppingItsOverlay()
        {
            var store = Store();
            string activeId = store.Profiles.First().Id;
            foreach (var profile in store.Profiles.Where(profile => profile.Id != activeId).ToArray()) store.Delete(profile.Id);
            _service.Activate(activeId);
            AssertFailedDeletionPreservesState<InvalidOperationException>(activeId);
            Assert.AreEqual(1, Store().Profiles.Count);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void AWriterLockFailurePreservesBothFilesAndTheActiveScene(bool deleteActive)
        {
            var store = Store();
            var active = store.Create("Active", Scene);
            var other = store.Create("Other", Scene);
            _service.Activate(active.Id);
            using var writer = new FileStream(Path.Combine(_folder, "OverlayDesigns", "profiles.lock"),
                FileMode.Open, FileAccess.ReadWrite, FileShare.None);

            AssertFailedDeletionPreservesState<IOException>(deleteActive ? active.Id : other.Id);
        }

        [TestMethod]
        public void AnUnrecoverableLibraryFailureDoesNotClearTheLastValidatedDesign()
        {
            var active = Store().Create("Active", Scene);
            _service.Activate(active.Id);
            File.WriteAllText(LibraryPath, "damaged primary");
            File.WriteAllText(LibraryPath + ".bak", "damaged backup");

            AssertFailedDeletionPreservesState<InvalidDataException>(active.Id);
        }

        [TestMethod]
        public void ANewerLibraryIsNotReplacedWhileDeletingAnOlderLoadedProfile()
        {
            var active = Store().Create("Active", Scene);
            _service.Activate(active.Id);
            var future = JObject.Parse(File.ReadAllText(LibraryPath));
            future["Version"] = 99;
            File.WriteAllText(LibraryPath, future.ToString());

            AssertFailedDeletionPreservesState<NotSupportedException>(active.Id);
        }

        [TestMethod]
        public void AMissingProfileDoesNotSeedTheLibraryOrChangeTheRuntime()
        {
            var active = Store().Create("Active", Scene);
            _service.Activate(active.Id);
            var library = JObject.Parse(File.ReadAllText(LibraryPath));
            library.Remove("StarterCatalogVersion");
            File.WriteAllText(LibraryPath, library.ToString());

            AssertFailedDeletionPreservesState<KeyNotFoundException>(Guid.NewGuid().ToString("N"));
        }

        private OverlayDesignProfileStore Store() => new OverlayDesignProfileStore(_folder, OverlayRuntimeDesign.Canonicalize);

        private void AssertFailedDeletionPreservesState<TException>(string profileId) where TException : Exception
        {
            var current = _service.CurrentDesign;
            var profiles = _service.Profiles;
            string selection = _configuration.Object.ActiveOverlayDesignProfileId;
            string status = _service.Status;
            bool visible = _configuration.Object.IsOverlayActive;
            bool hook = _configuration.Object.EnableHookOverlay;
            bool hookFree = _configuration.Object.EnableHookFreeOverlay;
            int requests = _showRequests;
            byte[] primary = File.ReadAllBytes(LibraryPath);
            byte[] backup = File.ReadAllBytes(LibraryPath + ".bak");
            int notifications = 0;
            _service.Changed += (_, _) => notifications++;

            Assert.ThrowsExactly<TException>(() => _service.Delete(profileId));

            Assert.AreSame(current, _service.CurrentDesign);
            Assert.AreSame(profiles, _service.Profiles);
            Assert.AreEqual(selection, _configuration.Object.ActiveOverlayDesignProfileId);
            Assert.AreEqual(status, _service.Status);
            Assert.AreEqual(visible, _configuration.Object.IsOverlayActive);
            Assert.AreEqual(hook, _configuration.Object.EnableHookOverlay);
            Assert.AreEqual(hookFree, _configuration.Object.EnableHookFreeOverlay);
            Assert.AreEqual(requests, _showRequests);
            Assert.AreEqual(0, notifications);
            CollectionAssert.AreEqual(primary, File.ReadAllBytes(LibraryPath));
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(LibraryPath + ".bak"));
        }
    }
}
