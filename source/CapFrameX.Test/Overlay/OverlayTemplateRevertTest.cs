using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Hardware.Controller;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Test.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// "Revert" in the overlay template section used to be available before any template had been
    /// applied. The stored state was then empty, the view model replaced the whole entry list with
    /// it, and the provider stored null for the empty list (ToBlockingCollection). Every following
    /// refresh failed on that null list, and the failure on the refresh thread ended CapFrameX
    /// (1.9.0.8 crash reports).
    /// </summary>
    [TestClass]
    public class OverlayTemplateRevertTest
    {
        private string _testConfigFolder;
        private MockSensorService _mockSensorService;
        private Subject<HookOverlayStatus> _statusStream;
        private Subject<(string key, object value)> _configurationChanges;

        [TestInitialize]
        public void Setup()
        {
            // Empty folder: no saved configuration, so the provider builds the defaults.
            _testConfigFolder = Path.Combine(Path.GetTempPath(), "CxTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testConfigFolder);
            _mockSensorService = new MockSensorService(seed: 42);
            _statusStream = new Subject<HookOverlayStatus>();
            _configurationChanges = new Subject<(string key, object value)>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _mockSensorService?.Dispose();
            _statusStream?.Dispose();
            _configurationChanges?.Dispose();

            try
            {
                if (Directory.Exists(_testConfigFolder))
                    Directory.Delete(_testConfigFolder, true);
            }
            catch { }
        }

        [TestMethod]
        public async Task EmptyEntryList_KeepsRefreshAndSortWorking()
        {
            var provider = CreateProvider();
            var defaults = await provider.GetOverlayEntries(updateFormats: false);
            Assert.IsTrue(defaults.Any(), "the provider should start with its default entries");

            provider.UpdateOverlayEntries(Array.Empty<IOverlayEntry>());

            var entries = await provider.GetOverlayEntries();
            Assert.AreEqual(0, entries.Length);
            provider.SortOverlayEntriesByType();
            Assert.AreEqual(0, (await provider.GetOverlayEntries()).Length);

            provider.UpdateOverlayEntries(defaults);
            CollectionAssert.AreEqual(
                defaults.Select(entry => entry.Identifier).ToArray(),
                (await provider.GetOverlayEntries(updateFormats: false)).Select(entry => entry.Identifier).ToArray(),
                "entries pushed after an empty list must be served again");
        }

        [TestMethod]
        public void TemplateService_WithoutAppliedTemplate_HasNothingToRevert()
        {
            var service = new OverlayTemplateService(Mock.Of<ISensorService>());

            Assert.IsFalse(service.HasStoredState);
            Assert.IsFalse(service.GetStoredOverlayEntries().Any());
        }

        [TestMethod]
        public void TemplateService_RevertReturnsFreshCopiesOfTheStoredState()
        {
            var service = new OverlayTemplateService(Mock.Of<ISensorService>());
            var original = new IOverlayEntry[]
            {
                new OverlayEntryWrapper("Framerate") { GroupName = "Framerate" },
                new OverlayEntryWrapper("Frametime") { GroupName = "Frametime" }
            };

            service.StoreCurrentState(original);
            var firstRevert = service.GetStoredOverlayEntries().ToArray();
            var secondRevert = service.GetStoredOverlayEntries().ToArray();

            Assert.IsTrue(service.HasStoredState);
            CollectionAssert.AreEqual(new[] { "Framerate", "Frametime" },
                secondRevert.Select(entry => entry.Identifier).ToArray());
            CollectionAssert.AreEqual(new[] { "Framerate", "Frametime" },
                secondRevert.Select(entry => entry.GroupName).ToArray());

            // The view model disposes the entries a revert replaces. A second revert must not
            // bring back those disposed instances.
            for (int i = 0; i < original.Length; i++)
            {
                Assert.AreNotSame(firstRevert[i], secondRevert[i]);
                Assert.AreNotSame(original[i], secondRevert[i]);
            }
        }

        [TestMethod]
        public void TemplateService_ClearedState_HasNothingToRevert()
        {
            var service = new OverlayTemplateService(Mock.Of<ISensorService>());
            service.StoreCurrentState(new IOverlayEntry[] { new OverlayEntryWrapper("Framerate") });

            service.ClearStoredState();

            Assert.IsFalse(service.HasStoredState);
            Assert.IsFalse(service.GetStoredOverlayEntries().Any());
        }

        private OverlayEntryProvider CreateProvider()
        {
            var overlayEntryCore = new OverlayEntryCore();
            overlayEntryCore.OverlayEntryCoreCompletionSource.SetResult(true);

            var appConfiguration = new Mock<IAppConfiguration>();
            appConfiguration.Setup(x => x.OverlayEntryConfigurationFile).Returns(0);
            appConfiguration.Setup(x => x.HardwareInfoSource).Returns("Auto");
            appConfiguration.Setup(x => x.OnValueChanged).Returns(_configurationChanges);

            var eventAggregator = new Mock<IEventAggregator>();
            eventAggregator
                .Setup(x => x.GetEvent<PubSubEvent<ViewMessages.OptionPopupClosed>>())
                .Returns(new PubSubEvent<ViewMessages.OptionPopupClosed>());

            var rtssService = new Mock<IRTSSService>();
            rtssService.Setup(x => x.ProcessIdStream).Returns(new BehaviorSubject<int>(0));
            rtssService.Setup(x => x.GetCurrentFramerate(It.IsAny<int>()))
                .Returns(Tuple.Create(0.0, 0.0));

            var pathService = new Mock<IPathService>();
            pathService.Setup(x => x.ConfigFolder).Returns(_testConfigFolder);

            var hookOverlayStatus = new Mock<IHookOverlayStatusService>();
            hookOverlayStatus.Setup(x => x.Current)
                .Returns(new HookOverlayStatus(EHookOverlayStatus.Disabled));
            hookOverlayStatus.Setup(x => x.StatusStream).Returns(_statusStream);

            return new OverlayEntryProvider(
                _mockSensorService,
                appConfiguration.Object,
                eventAggregator.Object,
                Mock.Of<IOnlineMetricService>(),
                Mock.Of<ISystemInfo>(),
                rtssService.Object,
                Mock.Of<ISensorConfig>(),
                overlayEntryCore,
                Mock.Of<IThreadAffinityController>(),
                pathService.Object,
                hookOverlayStatus.Object,
                Mock.Of<ILogger<OverlayEntryProvider>>(),
                () => Array.Empty<DetectedDisplay>());
        }
    }
}
