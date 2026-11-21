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
    /// The "Upscaler version" and "Frame generation version" entries name what the in-game hook
    /// proves to be running. Only that renderer has the proof, so they are gated like the other
    /// frame generation entries; unlike those, CapFrameX fills in their values itself.
    /// </summary>
    [TestClass]
    public class UpscalingVersionEntryTest
    {
        private const string UpscalerVersion = "UpscalerVersion";
        private const string FrameGenerationVersion = "FrameGenerationVersion";
        private const string FrameGenerationFactor = "FrameGenerationFactor";
        private const int TargetPid = 4711;

        private string _testConfigFolder;
        private MockSensorService _mockSensorService;
        private OverlayEntryCore _overlayEntryCore;
        private Mock<IAppConfiguration> _appConfigMock;
        private Mock<IUpscalingTechnologyVersionService> _versionsMock;
        private Subject<(string key, object value)> _configurationChanges;
        private BehaviorSubject<int> _processIds;

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
            _appConfigMock.Setup(x => x.EnableHookOverlay).Returns(true);
            _configurationChanges = new Subject<(string key, object value)>();
            _appConfigMock.Setup(x => x.OnValueChanged).Returns(_configurationChanges);

            _versionsMock = new Mock<IUpscalingTechnologyVersionService>();
            _processIds = new BehaviorSubject<int>(TargetPid);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _mockSensorService?.Dispose();
            _configurationChanges?.Dispose();
            _processIds?.Dispose();

            try
            {
                if (Directory.Exists(_testConfigFolder))
                    Directory.Delete(_testConfigFolder, true);
            }
            catch { }
        }

        [TestMethod]
        public void Defaults_AreHiddenTextEntriesBehindTheFrameGenerationStatus()
        {
            var defaults = OverlayUtils.GetOverlayEntryDefaults(_appConfigMock.Object);
            var identifiers = defaults.Select(entry => entry.Identifier).ToList();
            var frameGeneration = defaults.Single(entry => entry.Identifier == FrameGenerationVersion);
            var factor = defaults.Single(entry => entry.Identifier == FrameGenerationFactor);
            var upscaler = defaults.Single(entry => entry.Identifier == UpscalerVersion);

            // Position is not cosmetic: a saved profile gets a new entry behind its predecessor.
            Assert.AreEqual(identifiers.IndexOf("FrameGenerationStatus") + 1,
                identifiers.IndexOf(FrameGenerationVersion));
            Assert.AreEqual(identifiers.IndexOf(FrameGenerationVersion) + 1,
                identifiers.IndexOf(FrameGenerationFactor));
            Assert.AreEqual(identifiers.IndexOf(FrameGenerationFactor) + 1, identifiers.IndexOf(UpscalerVersion));
            Assert.AreEqual("Frame generation version", frameGeneration.Description);
            Assert.AreEqual("Frame generation factor", factor.Description);
            Assert.AreEqual("Frame Generation", factor.GroupName);
            Assert.AreEqual("Frame Generation", frameGeneration.GroupName,
                "One row with the technology and the status.");
            Assert.AreEqual("Upscaler version", upscaler.Description);
            Assert.AreEqual("Upscaler", upscaler.GroupName);
            foreach (var entry in new[] { frameGeneration, factor, upscaler })
            {
                Assert.AreEqual(EOverlayEntryType.CX, entry.OverlayEntryType);
                Assert.IsTrue(entry.IsEntryEnabled);
                Assert.IsTrue(entry.ShowOnOverlayIsEnabled);
                Assert.IsFalse(entry.ShowOnOverlay);
                Assert.IsFalse(entry.ShowGraphIsEnabled, "A text value has no graph.");
                Assert.AreEqual("N/A", entry.Value);
            }
        }

        [TestMethod]
        public void Defaults_HaveNoSourceWithoutTheInGameRenderer()
        {
            var defaults = OverlayUtils.GetOverlayEntryDefaults(new Mock<IAppConfiguration>().Object);

            foreach (string identifier in new[] { FrameGenerationVersion, FrameGenerationFactor, UpscalerVersion })
            {
                var entry = defaults.Single(item => item.Identifier == identifier);
                Assert.IsFalse(entry.IsEntryEnabled, "RTSS and the hook-free window cannot tell what runs.");
                Assert.IsFalse(entry.ShowOnOverlayIsEnabled);
            }
        }

        [TestMethod]
        public async Task ShownEntries_NameWhatTheTargetRuns()
        {
            _versionsMock.Setup(v => v.GetUpscalerVersion(TargetPid)).Returns("FSR 3.1.5");
            _versionsMock.Setup(v => v.GetFrameGenerationVersion(TargetPid)).Returns("DLSS 310.7.130");
            _versionsMock.Setup(v => v.GetFrameGenerationFactor(TargetPid)).Returns("3x");
            var provider = CreateProvider();
            await provider.GetOverlayEntries();
            provider.GetOverlayEntry(UpscalerVersion).ShowOnOverlay = true;
            provider.GetOverlayEntry(FrameGenerationVersion).ShowOnOverlay = true;
            provider.GetOverlayEntry(FrameGenerationFactor).ShowOnOverlay = true;

            var entries = await provider.GetOverlayEntries();
            var upscaler = entries.Single(entry => entry.Identifier == UpscalerVersion);
            var frameGeneration = entries.Single(entry => entry.Identifier == FrameGenerationVersion);

            Assert.AreEqual("FSR 3.1.5", upscaler.Value);
            Assert.AreEqual("DLSS 310.7.130", frameGeneration.Value);
            Assert.AreEqual("3x", entries.Single(entry => entry.Identifier == FrameGenerationFactor).Value);
            Assert.IsFalse(upscaler.IsNumeric);
            StringAssert.Contains(RtssTextFormatter.FormatValue(upscaler, 65001), "FSR 3.1.5");

            // The game switched to an upscaler the hook cannot see, and frame generation off.
            _versionsMock.Setup(v => v.GetUpscalerVersion(TargetPid)).Returns((string)null);
            _versionsMock.Setup(v => v.GetFrameGenerationVersion(TargetPid)).Returns((string)null);
            _versionsMock.Setup(v => v.GetFrameGenerationFactor(TargetPid)).Returns((string)null);
            entries = await provider.GetOverlayEntries();
            Assert.AreEqual("N/A", entries.Single(entry => entry.Identifier == UpscalerVersion).Value);
            Assert.AreEqual("N/A", entries.Single(entry => entry.Identifier == FrameGenerationVersion).Value);
            Assert.AreEqual("N/A", entries.Single(entry => entry.Identifier == FrameGenerationFactor).Value);
        }

        [TestMethod]
        public async Task HiddenEntries_ReadNothing()
        {
            var provider = CreateProvider();

            await provider.GetOverlayEntries();

            _versionsMock.Verify(v => v.GetUpscalerVersion(It.IsAny<int>()), Times.Never);
            _versionsMock.Verify(v => v.GetFrameGenerationVersion(It.IsAny<int>()), Times.Never);
            _versionsMock.Verify(v => v.GetFrameGenerationFactor(It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public async Task ShownEntries_ReadNothingWithoutATargetOrWithoutTheInGameRenderer()
        {
            _versionsMock.Setup(v => v.GetUpscalerVersion(It.IsAny<int>())).Returns("FSR 3.1.5");
            _versionsMock.Setup(v => v.GetFrameGenerationVersion(It.IsAny<int>())).Returns("FSR 3.1.6");
            _versionsMock.Setup(v => v.GetFrameGenerationFactor(It.IsAny<int>())).Returns("2x");
            var provider = CreateProvider();
            await provider.GetOverlayEntries();
            provider.GetOverlayEntry(UpscalerVersion).ShowOnOverlay = true;
            provider.GetOverlayEntry(FrameGenerationVersion).ShowOnOverlay = true;
            provider.GetOverlayEntry(FrameGenerationFactor).ShowOnOverlay = true;

            _processIds.OnNext(0);
            var entries = await provider.GetOverlayEntries();
            Assert.AreEqual("N/A", entries.Single(entry => entry.Identifier == UpscalerVersion).Value);
            Assert.AreEqual("N/A", entries.Single(entry => entry.Identifier == FrameGenerationVersion).Value);
            Assert.AreEqual("N/A", entries.Single(entry => entry.Identifier == FrameGenerationFactor).Value);

            _processIds.OnNext(TargetPid);
            _appConfigMock.Setup(x => x.EnableHookOverlay).Returns(false);
            entries = await provider.GetOverlayEntries();
            Assert.AreEqual("N/A", entries.Single(entry => entry.Identifier == UpscalerVersion).Value,
                "A hook left in the game by a former selection is not asked for another renderer.");
            Assert.AreEqual("N/A", entries.Single(entry => entry.Identifier == FrameGenerationVersion).Value);
            Assert.AreEqual("N/A", entries.Single(entry => entry.Identifier == FrameGenerationFactor).Value);

            _versionsMock.Verify(v => v.GetUpscalerVersion(It.IsAny<int>()), Times.Never);
            _versionsMock.Verify(v => v.GetFrameGenerationVersion(It.IsAny<int>()), Times.Never);
            _versionsMock.Verify(v => v.GetFrameGenerationFactor(It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public async Task TelemetryValues_AreTheVersionsWhenRequested()
        {
            _versionsMock.Setup(v => v.GetUpscalerVersion(TargetPid)).Returns("FSR 3.1.2 (DLL)");
            _versionsMock.Setup(v => v.GetFrameGenerationVersion(TargetPid)).Returns("XeSS 1.3.1");
            _versionsMock.Setup(v => v.GetFrameGenerationFactor(TargetPid)).Returns("2.5x");
            var provider = CreateProvider();
            await provider.GetTelemetrySourcesAsync();

            var values = provider.GetTelemetryValues(new HashSet<string>
                { UpscalerVersion, FrameGenerationVersion, FrameGenerationFactor });
            Assert.AreEqual("FSR 3.1.2 (DLL)", values[UpscalerVersion],
                "Tile designs show the same text as the overlay entry.");
            Assert.AreEqual("XeSS 1.3.1", values[FrameGenerationVersion]);
            Assert.AreEqual("2.5x", values[FrameGenerationFactor]);

            _versionsMock.Invocations.Clear();
            values = provider.GetTelemetryValues(new HashSet<string> { "CaptureTimer" });
            Assert.IsFalse(values.ContainsKey(UpscalerVersion));
            Assert.IsFalse(values.ContainsKey(FrameGenerationVersion));
            Assert.IsFalse(values.ContainsKey(FrameGenerationFactor));
            _versionsMock.Verify(v => v.GetUpscalerVersion(It.IsAny<int>()), Times.Never);
            _versionsMock.Verify(v => v.GetFrameGenerationVersion(It.IsAny<int>()), Times.Never);
            _versionsMock.Verify(v => v.GetFrameGenerationFactor(It.IsAny<int>()), Times.Never);
        }

        [TestMethod]
        public async Task SavedProfileWithoutTheEntries_GetsThemHiddenBehindTheFrameGenerationStatus()
        {
            var savedEntries = OverlayUtils.GetOverlayEntryDefaults(_appConfigMock.Object)
                .Where(entry => entry.Identifier != UpscalerVersion && entry.Identifier != FrameGenerationVersion &&
                    entry.Identifier != FrameGenerationFactor)
                .ToList();
            File.WriteAllText(
                Path.Combine(_testConfigFolder, "OverlayEntryConfiguration_0.json"),
                JsonConvert.SerializeObject(new OverlayEntryPersistence { OverlayEntries = savedEntries }));

            var provider = CreateProvider();
            var entries = (await provider.GetOverlayEntries(updateFormats: false)).ToList();
            var identifiers = entries.Select(entry => entry.Identifier).ToList();

            Assert.AreEqual(identifiers.IndexOf("FrameGenerationStatus") + 1,
                identifiers.IndexOf(FrameGenerationVersion));
            Assert.AreEqual(identifiers.IndexOf(FrameGenerationVersion) + 1,
                identifiers.IndexOf(FrameGenerationFactor));
            Assert.AreEqual(identifiers.IndexOf(FrameGenerationFactor) + 1, identifiers.IndexOf(UpscalerVersion));
            Assert.IsFalse(entries.Single(entry => entry.Identifier == FrameGenerationFactor).ShowOnOverlay);
            Assert.IsFalse(entries.Single(entry => entry.Identifier == UpscalerVersion).ShowOnOverlay);
            Assert.IsFalse(entries.Single(entry => entry.Identifier == FrameGenerationVersion).ShowOnOverlay);
            Assert.IsFalse(provider.HasPendingChanges,
                "automatic profile migration during load is the clean startup baseline");
        }

        private OverlayEntryProvider CreateProvider()
        {
            var eventAggregator = new Mock<IEventAggregator>();
            eventAggregator
                .Setup(x => x.GetEvent<PubSubEvent<ViewMessages.OptionPopupClosed>>())
                .Returns(new PubSubEvent<ViewMessages.OptionPopupClosed>());

            var rtssService = new Mock<IRTSSService>();
            rtssService.Setup(x => x.ProcessIdStream).Returns(_processIds);
            rtssService.Setup(x => x.GetCurrentFramerate(It.IsAny<int>())).Returns(Tuple.Create(0.0, 0.0));

            var pathService = new Mock<IPathService>();
            pathService.Setup(x => x.ConfigFolder).Returns(_testConfigFolder);

            var hookOverlayStatus = new Mock<IHookOverlayStatusService>();
            hookOverlayStatus.Setup(x => x.Current).Returns(
                new HookOverlayStatus(EHookOverlayStatus.Active, processId: TargetPid));
            hookOverlayStatus.Setup(x => x.StatusStream).Returns(new Subject<HookOverlayStatus>());

            return new OverlayEntryProvider(
                _mockSensorService,
                _appConfigMock.Object,
                eventAggregator.Object,
                new Mock<IOnlineMetricService>().Object,
                new Mock<ISystemInfo>().Object,
                rtssService.Object,
                new Mock<ISensorConfig>().Object,
                _overlayEntryCore,
                new Mock<IThreadAffinityController>().Object,
                pathService.Object,
                hookOverlayStatus.Object,
                new Mock<ILogger<OverlayEntryProvider>>().Object,
                () => Array.Empty<DetectedDisplay>(),
                upscalingTechnologyVersions: _versionsMock.Object);
        }
    }
}
