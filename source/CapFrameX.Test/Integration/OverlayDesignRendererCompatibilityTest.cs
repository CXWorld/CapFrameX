using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Hardware.Controller;
using CapFrameX.OSD.Integration;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.ViewModel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignRendererCompatibilityTest
    {
        private string _folder;
        private CapFrameXConfiguration _configuration;
        private OverlayDesignProfileStore _store;
        private string _profileId;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX-renderer-design-tests", Guid.NewGuid().ToString("N"));
            _configuration = new CapFrameXConfiguration(NullLogger<CapFrameXConfiguration>.Instance, new MemorySettings());
            _configuration.EnableHookOverlay = true;
            _configuration.EnableHookFreeOverlay = false;
            _configuration.OverlayEntryConfigurationFile = 2;
            _configuration.OverlayHotKey = "F8";
            _configuration.IsOverlayActive = true;
            _store = new OverlayDesignProfileStore(_folder, OverlayRuntimeDesign.Canonicalize);
            OverlayDesignStarterCatalog.EnsureProfiles(_store);
            _profileId = _store.Create("Mixed profile", "{\"version\":1,\"root\":{\"type\":\"panel\",\"children\":[{\"type\":\"metric\",\"key\":\"fps\"}]}}").Id;
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void StartingWithRtssClearsPersistedDesignWithoutChangingEitherProfile()
        {
            _configuration.ActiveOverlayDesignProfileId = _profileId;
            _configuration.EnableHookOverlay = false;
            string saved = File.ReadAllText(LibraryPath);
            using var service = Service();
            AssertClassicRtss(service);
            Assert.AreEqual(saved, File.ReadAllText(LibraryPath));
            Assert.AreEqual(_profileId, _store.ActiveProfileId, "The editor selection must survive disabling runtime design mode.");
        }

        [TestMethod]
        public void RtssRejectsDirectActivationBeforeReadingTheLibrary()
        {
            _configuration.EnableHookOverlay = false;
            using var service = Service();
            File.WriteAllText(LibraryPath, "unreadable design library");
            var error = Assert.ThrowsException<InvalidOperationException>(() => service.Activate(_profileId));
            StringAssert.Contains(error.Message, "RTSS");
            AssertClassicRtss(service);
            Assert.AreEqual("unreadable design library", File.ReadAllText(LibraryPath));
        }

        [TestMethod]
        public void SwitchingToRtssImmediatelyDisablesDesignAndReturningToNativeRequiresActivation()
        {
            using var service = Service();
            var model = CreateModel(service);
            service.Activate(_profileId);
            string saved = File.ReadAllText(LibraryPath);
            model.OverlayModeRtss = true;
            AssertClassicRtss(service);
            Assert.IsFalse(model.HasActiveDesign);
            Assert.IsFalse(model.UseDesignCommand.CanExecute());
            Assert.AreEqual(saved, File.ReadAllText(LibraryPath));

            model.OverlayModeHookFree = true;
            Assert.IsTrue(service.CanActivate);
            Assert.IsFalse(service.IsEnabled);
            Assert.IsNull(_configuration.ActiveOverlayDesignProfileId);
            StringAssert.Contains(service.Status, "Row overlay");
            StringAssert.Contains(service.Status, "Hook-free");
            Assert.IsTrue(model.UseDesignCommand.CanExecute());
            model.UseDesignCommand.Execute();
            Assert.AreEqual(_profileId, service.ActiveProfileId);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void SwitchingBetweenNativeRenderersNeverTemporarilyDisablesTheDesign(bool startWithHook)
        {
            _configuration.EnableHookOverlay = startWithHook;
            _configuration.EnableHookFreeOverlay = !startWithHook;
            using var service = Service();
            var model = CreateModel(service);
            service.Activate(_profileId);
            int disabledEvents = 0;
            service.Changed += (_, _) => { if (!service.IsEnabled) disabledEvents++; };
            if (startWithHook) model.OverlayModeHookFree = true;
            else model.OverlayModeHook = true;
            Assert.AreEqual(0, disabledEvents);
            Assert.AreEqual(_profileId, service.ActiveProfileId);
            Assert.AreEqual(_profileId, _configuration.ActiveOverlayDesignProfileId);
            StringAssert.Contains(service.Status, startWithHook ? "Hook-free" : "In-game");
        }

        [TestMethod]
        public void RtssClearsLastValidatedDesignEvenIfItsLibraryCannotBeRefreshed()
        {
            using var service = Service();
            service.Activate(_profileId);
            File.WriteAllText(LibraryPath, "invalid");
            service.RefreshProfiles();
            Assert.IsTrue(service.IsEnabled, "Native rendering retains the last validated scene on a library read error.");
            _configuration.EnableHookOverlay = false;
            AssertClassicRtss(service);
            service.RefreshProfiles();
            Assert.IsFalse(service.IsEnabled);
            Assert.IsNull(_configuration.ActiveOverlayDesignProfileId);
            Assert.AreEqual("invalid", File.ReadAllText(LibraryPath));
        }

        [TestMethod]
        public void ExternalSelectionCannotReactivateDesignWhileRtssIsSelected()
        {
            _configuration.EnableHookOverlay = false;
            using var service = Service();
            _configuration.ActiveOverlayDesignProfileId = _profileId;
            AssertClassicRtss(service);
            service.RefreshProfiles();
            AssertClassicRtss(service);
        }

        [TestMethod]
        public void ConfigurationSelectionChangesRefreshNativeRuntimeDesign()
        {
            using var service = Service();
            _configuration.ActiveOverlayDesignProfileId = _profileId;
            Assert.AreEqual(_profileId, service.ActiveProfileId);
            _configuration.ActiveOverlayDesignProfileId = null;
            Assert.IsFalse(service.IsEnabled);
            Assert.IsTrue(service.CanActivate);
        }

        [TestMethod]
        public void UsingATileDesignShowsTheOverlayButBrowsingAndRestartingDoNot()
        {
            _configuration.IsOverlayActive = false;
            using var service = Service();
            var model = CreateModel(service);
            model.SelectedDesign = service.Profiles.Single(profile => profile.Id == _profileId);
            service.RefreshProfiles();
            Assert.IsFalse(_configuration.IsOverlayActive);
            Assert.IsFalse(service.IsEnabled);

            model.UseDesignCommand.Execute();
            Assert.IsTrue(_configuration.IsOverlayActive);
            Assert.AreEqual(_profileId, service.ActiveProfileId);
            Assert.IsTrue(_configuration.EnableHookOverlay);
            Assert.IsFalse(_configuration.EnableHookFreeOverlay);

            _configuration.IsOverlayActive = false;
            service.RefreshProfiles();
            using var restarted = Service();
            Assert.AreEqual(_profileId, restarted.ActiveProfileId);
            Assert.IsFalse(_configuration.IsOverlayActive, "A saved selection must respect the user's hidden overlay setting.");

            restarted.Activate(_profileId);
            Assert.IsTrue(_configuration.IsOverlayActive, "Explicitly using the same design must show it again.");
        }

        [TestMethod]
        public void FailedDesignActivationDoesNotShowTheOverlayOrReplaceTheActiveScene()
        {
            using var service = Service();
            service.Activate(_profileId);
            _configuration.IsOverlayActive = false;
            Assert.ThrowsException<KeyNotFoundException>(() => service.Activate(Guid.NewGuid().ToString("N")));
            Assert.IsFalse(_configuration.IsOverlayActive);
            Assert.AreEqual(_profileId, service.ActiveProfileId);
        }

        [TestMethod]
        public void ExplicitActivationNotifiesTheSharedOverlayLifecycleAfterLoadingTheScene()
        {
            int activations = 0;
            OverlayDesignService service = null;
            using (service = new OverlayDesignService(_folder, _configuration, () =>
            {
                Assert.AreEqual(_profileId, service.ActiveProfileId);
                _configuration.IsOverlayActive = true;
                activations++;
            }))
            {
                service.RefreshProfiles();
                Assert.AreEqual(0, activations);
                service.Activate(_profileId);
                Assert.AreEqual(1, activations);
                service.Activate(_profileId);
                Assert.AreEqual(2, activations);
                Assert.ThrowsException<KeyNotFoundException>(() => service.Activate(Guid.NewGuid().ToString("N")));
                service.Deactivate();
                Assert.AreEqual(2, activations);
            }
        }

        [TestMethod]
        public void LateNativeRendererErrorDoesNotReplaceTheRtssModeStatus()
        {
            using var service = Service();
            service.Activate(_profileId);
            _configuration.EnableHookOverlay = false;
            string status = service.Status;
            service.ReportRuntimeError("The previous native channel failed.");
            service.ReportRuntimeReady();
            Assert.AreEqual(status, service.Status);
            AssertClassicRtss(service);
        }

        private string LibraryPath => Path.Combine(_folder, "OverlayDesigns", "profiles.json");

        private OverlayDesignService Service() => new OverlayDesignService(_folder, _configuration);

        private void AssertClassicRtss(OverlayDesignService service)
        {
            Assert.IsFalse(service.CanActivate);
            Assert.IsFalse(service.IsEnabled);
            Assert.IsNull(service.CurrentDesign);
            Assert.IsNull(_configuration.ActiveOverlayDesignProfileId);
            StringAssert.Contains(service.Status, "RTSS");
            Assert.AreEqual(2, _configuration.OverlayEntryConfigurationFile);
            Assert.AreEqual("F8", _configuration.OverlayHotKey);
            Assert.IsTrue(_configuration.IsOverlayActive);
        }

        private OverlayViewModel CreateModel(IOverlayDesignService designs)
        {
            var provider = new Mock<IOverlayEntryProvider>();
            provider.Setup(p => p.GetOverlayEntries(false)).ReturnsAsync(Array.Empty<IOverlayEntry>());
            provider.Setup(p => p.SwitchConfigurationTo(It.IsAny<int>())).Returns(Task.CompletedTask);
            var model = new OverlayViewModel(Mock.Of<IOverlayService>(), provider.Object, _configuration,
                Mock.Of<IPathService>(), Mock.Of<ISensorService>(), Mock.Of<IRTSSService>(),
                Mock.Of<IThreadAffinityController>(), Mock.Of<IOnlineMetricService>(),
                new OverlayTemplateService(Mock.Of<ISensorService>()), null, null, designs);
            model.SelectedDesign = designs.Profiles.Single(profile => profile.Id == _profileId);
            return model;
        }

        private sealed class MemorySettings : ISettingsStorage
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();
            public Task Load() => Task.CompletedTask;
            public T GetValue<T>(string key) => (T)_values[key];
            public void SetValue(string key, object value) => _values[key] = value;
        }
    }
}
