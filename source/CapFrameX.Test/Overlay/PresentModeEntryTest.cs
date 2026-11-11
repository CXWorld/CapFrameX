using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Classes;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Hardware.Controller;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Test.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// The present mode entry shows PresentMon's PresentMode of the target as text, for every
    /// renderer: RTSS, the CapFrameX renderers and the remote API all read the overlay entries.
    /// </summary>
    [TestClass]
    public class PresentModeEntryTest
    {
        private const string PresentMode = "OnlinePresentMode";

        private string _testConfigFolder;
        private MockSensorService _mockSensorService;
        private OverlayEntryCore _overlayEntryCore;
        private Mock<IAppConfiguration> _appConfigMock;
        private Mock<IOnlineMetricService> _onlineMetricServiceMock;
        private Subject<(string key, object value)> _configurationChanges;

        [TestInitialize]
        public void Setup()
        {
            // Empty folder: no saved configuration, so the provider builds the defaults.
            _testConfigFolder = Path.Combine(Path.GetTempPath(), "CxTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testConfigFolder);

            _mockSensorService = new MockSensorService(seed: 42);
            _overlayEntryCore = new OverlayEntryCore();
            _overlayEntryCore.OverlayEntryCoreCompletionSource.SetResult(true);

            _appConfigMock = new Mock<IAppConfiguration>();
            _appConfigMock.Setup(x => x.OverlayEntryConfigurationFile).Returns(0);
            _appConfigMock.Setup(x => x.HardwareInfoSource).Returns("Auto");
            _configurationChanges = new Subject<(string key, object value)>();
            _appConfigMock.Setup(x => x.OnValueChanged).Returns(_configurationChanges);

            _onlineMetricServiceMock = new Mock<IOnlineMetricService>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _mockSensorService?.Dispose();
            _configurationChanges?.Dispose();

            try
            {
                if (Directory.Exists(_testConfigFolder))
                    Directory.Delete(_testConfigFolder, true);
            }
            catch { }
        }

        [TestMethod]
        public void Default_IsAHiddenTextEntryBehindPcLatency()
        {
            var defaults = OverlayUtils.GetOverlayEntryDefaults(new Mock<IAppConfiguration>().Object);
            var identifiers = defaults.Select(entry => entry.Identifier).ToList();
            var presentMode = defaults.Single(entry => entry.Identifier == PresentMode);

            Assert.AreEqual(identifiers.IndexOf("OnlinePcLatency") + 1, identifiers.IndexOf(PresentMode));
            Assert.AreEqual(EOverlayEntryType.OnlineMetric, presentMode.OverlayEntryType);
            Assert.IsTrue(presentMode.IsEntryEnabled, "PresentMon reports the mode for every renderer.");
            Assert.IsTrue(presentMode.ShowOnOverlayIsEnabled);
            Assert.IsFalse(presentMode.ShowOnOverlay);
            Assert.IsFalse(presentMode.ShowGraphIsEnabled, "A text value has no graph.");
            Assert.AreEqual("Present Mode", presentMode.Description);
            Assert.AreEqual("Present Mode", presentMode.GroupName);
            Assert.AreEqual("N/A", presentMode.Value);
        }

        [TestMethod]
        public void ShortNames_KeepEveryKnownModeDistinctAndShort()
        {
            var presentMonNames = Enum.GetValues(typeof(EPresentMode)).Cast<EPresentMode>()
                .Select(mode => mode.GetDescription()).ToList();
            var shortNames = presentMonNames.Select(PresentModeLabel.Shorten).ToList();

            CollectionAssert.AllItemsAreUnique(shortNames, "Two modes must not read alike.");
            foreach (string shortName in shortNames)
                Assert.IsTrue(shortName.Length <= 21, shortName);
            Assert.AreEqual("HW: Legacy Copy", PresentModeLabel.Shorten("Hardware: Legacy Copy to front buffer"));
            Assert.AreEqual("MPO: Independent Flip", PresentModeLabel.Shorten("Hardware Composed: Independent Flip"));
            Assert.AreEqual("Composed: GPU Copy", PresentModeLabel.Shorten("Composed: Copy with GPU GDI"));
            Assert.AreEqual("Unknown", PresentModeLabel.Shorten("Unknown"));
            Assert.AreEqual("Hardware: Future Mode", PresentModeLabel.Shorten("Hardware: Future Mode"),
                "A mode of a later PresentMon shows as PresentMon names it.");
            Assert.IsNull(PresentModeLabel.Shorten(null));
        }

        [TestMethod]
        public async Task ShownEntry_ShowsTheTargetsPresentModeAsText()
        {
            _onlineMetricServiceMock.Setup(m => m.GetOnlinePresentMode()).Returns("Composed: Copy with GPU GDI");
            var provider = CreateProvider();
            await provider.GetOverlayEntries();
            provider.GetOverlayEntry(PresentMode).ShowOnOverlay = true;

            var presentMode = (await provider.GetOverlayEntries()).Single(entry => entry.Identifier == PresentMode);

            Assert.AreEqual("Composed: GPU Copy", presentMode.Value);
            Assert.IsFalse(presentMode.IsNumeric);
            StringAssert.Contains(RtssTextFormatter.FormatValue(presentMode, 65001), "Composed: GPU Copy",
                "RTSS shows the short name untranslated.");
            Assert.AreSame(presentMode, _overlayEntryCore.GetRealtimeMetricEntry(PresentMode),
                "The online metric service only evaluates the target's rows for an entry it knows.");

            _onlineMetricServiceMock.Setup(m => m.GetOnlinePresentMode()).Returns((string)null);
            Assert.AreEqual("N/A", (await provider.GetOverlayEntries())
                .Single(entry => entry.Identifier == PresentMode).Value);
        }

        [TestMethod]
        public async Task HiddenEntry_DoesNotReadThePresentMode()
        {
            var provider = CreateProvider();

            await provider.GetOverlayEntries();

            _onlineMetricServiceMock.Verify(m => m.GetOnlinePresentMode(), Times.Never);
        }

        [TestMethod]
        public async Task TelemetryValue_IsThePresentModeWhenRequested()
        {
            var provider = CreateProvider();
            await provider.GetTelemetrySourcesAsync();
            var requested = new HashSet<string> { PresentMode };

            _onlineMetricServiceMock.Setup(m => m.GetOnlinePresentMode()).Returns("Hardware: Independent Flip");
            Assert.AreEqual("HW: Independent Flip", provider.GetTelemetryValues(requested)[PresentMode],
                "Tile designs show the same short name as the overlay entry.");
            _onlineMetricServiceMock.Setup(m => m.GetOnlinePresentMode()).Returns((string)null);
            Assert.IsNull(provider.GetTelemetryValues(requested)[PresentMode]);

            _onlineMetricServiceMock.Invocations.Clear();
            Assert.IsFalse(provider.GetTelemetryValues(new HashSet<string> { "CaptureTimer" }).ContainsKey(PresentMode));
            _onlineMetricServiceMock.Verify(m => m.GetOnlinePresentMode(), Times.Never);
        }

        [TestMethod]
        public async Task SavedProfileWithoutTheEntry_GetsItHiddenBehindPcLatency()
        {
            var savedEntries = OverlayUtils.GetOverlayEntryDefaults(_appConfigMock.Object)
                .Where(entry => entry.Identifier != PresentMode)
                .ToList();
            File.WriteAllText(
                Path.Combine(_testConfigFolder, "OverlayEntryConfiguration_0.json"),
                JsonConvert.SerializeObject(new OverlayEntryPersistence { OverlayEntries = savedEntries }));

            var provider = CreateProvider();
            var entries = (await provider.GetOverlayEntries(updateFormats: false)).ToList();
            var identifiers = entries.Select(entry => entry.Identifier).ToList();

            Assert.AreEqual(identifiers.IndexOf("OnlinePcLatency") + 1, identifiers.IndexOf(PresentMode));
            Assert.IsFalse(entries.Single(entry => entry.Identifier == PresentMode).ShowOnOverlay);
        }

        private OverlayEntryProvider CreateProvider()
        {
            var eventAggregator = new Mock<IEventAggregator>();
            eventAggregator
                .Setup(x => x.GetEvent<PubSubEvent<ViewMessages.OptionPopupClosed>>())
                .Returns(new PubSubEvent<ViewMessages.OptionPopupClosed>());

            var rtssService = new Mock<IRTSSService>();
            rtssService.Setup(x => x.ProcessIdStream).Returns(new BehaviorSubject<int>(0));
            rtssService.Setup(x => x.GetCurrentFramerate(It.IsAny<int>())).Returns(Tuple.Create(0.0, 0.0));

            var pathService = new Mock<IPathService>();
            pathService.Setup(x => x.ConfigFolder).Returns(_testConfigFolder);

            var hookOverlayStatus = new Mock<IHookOverlayStatusService>();
            hookOverlayStatus.Setup(x => x.Current).Returns(new HookOverlayStatus(EHookOverlayStatus.Disabled));
            hookOverlayStatus.Setup(x => x.StatusStream).Returns(new Subject<HookOverlayStatus>());

            return new OverlayEntryProvider(
                _mockSensorService,
                _appConfigMock.Object,
                eventAggregator.Object,
                _onlineMetricServiceMock.Object,
                new Mock<ISystemInfo>().Object,
                rtssService.Object,
                new Mock<ISensorConfig>().Object,
                _overlayEntryCore,
                new Mock<IThreadAffinityController>().Object,
                pathService.Object,
                hookOverlayStatus.Object,
                new Mock<ILogger<OverlayEntryProvider>>().Object,
                () => Array.Empty<DetectedDisplay>());
        }
    }
}
