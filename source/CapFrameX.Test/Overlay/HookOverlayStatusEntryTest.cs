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
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// The hook-only entries have to survive in the list while the in-game overlay is *not* the
    /// selected renderer. A disabled entry is otherwise dropped when a configuration is loaded
    /// (GetIsEntryKeptInList), which would make it vanish for everyone who uses RTSS or the hook-free
    /// OSD. The hook status value additionally follows <see cref="IHookOverlayStatusService"/> rather
    /// than any sensor.
    /// </summary>
    [TestClass]
    public class HookOverlayStatusEntryTest
    {
        private const string HookOverlayStatusIdentifier = "HookOverlayStatus";
        private const string FrameGenerationTechnologyIdentifier = "FrameGenerationTechnology";
        private const string FrameGenerationStatusIdentifier = "FrameGenerationStatus";

        private string _testConfigFolder;
        private MockSensorService _mockSensorService;
        private OverlayEntryCore _overlayEntryCore;
        private Mock<IAppConfiguration> _appConfigMock;
        private Mock<ISensorConfig> _sensorConfigMock;
        private Mock<IEventAggregator> _eventAggregatorMock;
        private Mock<IOnlineMetricService> _onlineMetricServiceMock;
        private Mock<ISystemInfo> _systemInfoMock;
        private Mock<IRTSSService> _rtssServiceMock;
        private Mock<IThreadAffinityController> _threadAffinityMock;
        private Mock<IPathService> _pathServiceMock;
        private Mock<IHookOverlayStatusService> _hookOverlayStatusMock;
        private Mock<ILogger<OverlayEntryProvider>> _loggerMock;
        private Subject<HookOverlayStatus> _statusStream;
        private Subject<(string key, object value)> _configurationChanges;
        private bool _enableHookOverlay;
        private bool _enableHookFreeOverlay;
        private bool _hookOverlayUsePresentMonFrametimes;

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
            _appConfigMock.SetupGet(x => x.EnableHookOverlay).Returns(() => _enableHookOverlay);
            _appConfigMock.SetupGet(x => x.EnableHookFreeOverlay).Returns(() => _enableHookFreeOverlay);
            _appConfigMock.SetupGet(x => x.HookOverlayUsePresentMonFrametimes)
                .Returns(() => _hookOverlayUsePresentMonFrametimes);

            _sensorConfigMock = new Mock<ISensorConfig>();

            _eventAggregatorMock = new Mock<IEventAggregator>();
            _eventAggregatorMock
                .Setup(x => x.GetEvent<PubSubEvent<ViewMessages.OptionPopupClosed>>())
                .Returns(new PubSubEvent<ViewMessages.OptionPopupClosed>());

            _onlineMetricServiceMock = new Mock<IOnlineMetricService>();
            _systemInfoMock = new Mock<ISystemInfo>();

            _rtssServiceMock = new Mock<IRTSSService>();
            _rtssServiceMock.Setup(x => x.ProcessIdStream).Returns(new BehaviorSubject<int>(0));
            _rtssServiceMock.Setup(x => x.GetCurrentFramerate(It.IsAny<int>()))
                .Returns(Tuple.Create(0.0, 0.0));

            _threadAffinityMock = new Mock<IThreadAffinityController>();

            _pathServiceMock = new Mock<IPathService>();
            _pathServiceMock.Setup(x => x.ConfigFolder).Returns(_testConfigFolder);

            _statusStream = new Subject<HookOverlayStatus>();
            _hookOverlayStatusMock = new Mock<IHookOverlayStatusService>();
            _hookOverlayStatusMock.Setup(x => x.Current)
                .Returns(new HookOverlayStatus(EHookOverlayStatus.Disabled));
            _hookOverlayStatusMock.Setup(x => x.StatusStream).Returns(_statusStream);

            _loggerMock = new Mock<ILogger<OverlayEntryProvider>>();
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

        private OverlayEntryProvider CreateProvider(bool hookOverlayEnabled)
        {
            _enableHookOverlay = hookOverlayEnabled;

            return new OverlayEntryProvider(
                _mockSensorService,
                _appConfigMock.Object,
                _eventAggregatorMock.Object,
                _onlineMetricServiceMock.Object,
                _systemInfoMock.Object,
                _rtssServiceMock.Object,
                _sensorConfigMock.Object,
                _overlayEntryCore,
                _threadAffinityMock.Object,
                _pathServiceMock.Object,
                _hookOverlayStatusMock.Object,
                _loggerMock.Object,
                () => Array.Empty<DetectedDisplay>());
        }

        [TestMethod]
        public void Defaults_HookOnlyEntriesFollowTheCaptureServiceStatus()
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.Setup(x => x.EnableHookOverlay).Returns(true);

            var identifiers = OverlayUtils.GetOverlayEntryDefaults(configuration.Object)
                .Select(entry => entry.Identifier)
                .ToList();

            int captureServiceStatusIndex = identifiers.IndexOf("CaptureServiceStatus");
            int hookStatusIndex = identifiers.IndexOf(HookOverlayStatusIdentifier);
            int frameGenerationTechnologyIndex = identifiers.IndexOf(
                FrameGenerationTechnologyIdentifier);
            int frameGenerationStatusIndex = identifiers.IndexOf(FrameGenerationStatusIdentifier);

            Assert.AreNotEqual(-1, hookStatusIndex, "the hook status entry is missing from the defaults");
            Assert.AreNotEqual(-1, frameGenerationTechnologyIndex,
                "the frame generation technology entry is missing from the defaults");
            Assert.AreNotEqual(-1, frameGenerationStatusIndex,
                "the frame generation status entry is missing from the defaults");

            // Position is not cosmetic: a configuration written before this entry existed gets it
            // inserted behind its predecessor in the defaults list (OverlayEntryProvider migration).
            Assert.AreEqual(captureServiceStatusIndex + 1, hookStatusIndex,
                "the hook status entry has to follow the capture service status");
            Assert.AreEqual(hookStatusIndex + 1, frameGenerationTechnologyIndex,
                "the frame generation technology entry has to follow the hook status");
            Assert.AreEqual(frameGenerationTechnologyIndex + 1, frameGenerationStatusIndex,
                "the frame generation status entry has to follow the technology");
        }

        [TestMethod]
        public async Task HookOverlayOff_HookOnlyEntriesStayInTheListButDisabled()
        {
            var provider = CreateProvider(hookOverlayEnabled: false);
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var hookStatus = entries.SingleOrDefault(entry => entry.Identifier == HookOverlayStatusIdentifier);
            var frameGenerationTechnology = entries.SingleOrDefault(
                entry => entry.Identifier == FrameGenerationTechnologyIdentifier);
            var frameGenerationStatus = entries.SingleOrDefault(
                entry => entry.Identifier == FrameGenerationStatusIdentifier);

            Assert.IsNotNull(hookStatus,
                "the entry must stay in the list so switching the OSD mode can flip it in place");
            Assert.IsFalse(hookStatus.IsEntryEnabled, "without the in-game overlay there is no hook to report");
            Assert.IsFalse(hookStatus.ShowOnOverlayIsEnabled, "a disabled entry must not be selectable for the OSD");
            Assert.AreEqual("Off", hookStatus.Value, "a disabled hook reports Off, not a stale state");

            Assert.IsNotNull(frameGenerationTechnology,
                "the frame generation technology entry must stay available for renderer switches");
            Assert.IsFalse(frameGenerationTechnology.IsEntryEnabled,
                "without the in-game hook there is no frame generation technology source");
            Assert.IsFalse(frameGenerationTechnology.ShowOnOverlayIsEnabled,
                "RTSS and the hook-free OSD must not expose the unsupported entry");
            Assert.AreEqual("N/A", frameGenerationTechnology.Value);

            Assert.IsNotNull(frameGenerationStatus,
                "the frame generation entry must stay available for renderer switches");
            Assert.IsFalse(frameGenerationStatus.IsEntryEnabled,
                "without the in-game hook there is no frame generation source");
            Assert.IsFalse(frameGenerationStatus.ShowOnOverlayIsEnabled,
                "RTSS and the hook-free OSD must not expose the unsupported entry");
            Assert.AreEqual("N/A", frameGenerationStatus.Value);
        }

        [TestMethod]
        public async Task HookOverlayOn_HookOnlyEntriesAreEnabled()
        {
            var provider = CreateProvider(hookOverlayEnabled: true);
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var hookStatus = entries.Single(entry => entry.Identifier == HookOverlayStatusIdentifier);
            var frameGenerationTechnology = entries.Single(
                entry => entry.Identifier == FrameGenerationTechnologyIdentifier);
            var frameGenerationStatus = entries.Single(
                entry => entry.Identifier == FrameGenerationStatusIdentifier);

            Assert.IsTrue(hookStatus.IsEntryEnabled);
            Assert.IsTrue(hookStatus.ShowOnOverlayIsEnabled);
            Assert.IsFalse(hookStatus.ShowOnOverlay,
                "the entry ships hidden — it is a diagnostic, not a default overlay row");

            Assert.IsTrue(frameGenerationTechnology.IsEntryEnabled);
            Assert.IsTrue(frameGenerationTechnology.ShowOnOverlayIsEnabled);
            Assert.IsFalse(frameGenerationTechnology.ShowOnOverlay,
                "frame generation technology is selectable but ships hidden");
            Assert.AreEqual("N/A", frameGenerationTechnology.Value,
                "the native hook replaces the managed placeholder only inside the game");

            Assert.IsTrue(frameGenerationStatus.IsEntryEnabled);
            Assert.IsTrue(frameGenerationStatus.ShowOnOverlayIsEnabled);
            Assert.IsFalse(frameGenerationStatus.ShowOnOverlay,
                "frame generation status is selectable but ships hidden");
            Assert.AreEqual("N/A", frameGenerationStatus.Value,
                "the native hook replaces the managed placeholder only inside the game");
        }

        [TestMethod]
        public async Task RendererSwitch_PreservesUnsavedOverlayItemOptions()
        {
            _enableHookFreeOverlay = false;
            _hookOverlayUsePresentMonFrametimes = false;
            var provider = CreateProvider(hookOverlayEnabled: true);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var framerate = entries.Single(entry => entry.Identifier == "Framerate");
            var displayTime = entries.Single(entry => entry.Identifier == "DisplayTime");
            var resolution = entries.Single(entry => entry.Identifier == "Resolution");
            var hookStatus = entries.Single(
                entry => entry.Identifier == HookOverlayStatusIdentifier);

            framerate.GroupName = "Unsaved framerate group";
            framerate.Color = "123456";
            displayTime.GroupName = "Unsaved displaytime group";
            resolution.ShowOnOverlay = true;
            hookStatus.ShowOnOverlay = true;
            bool availabilityMarkedProfileDirty = false;
            displayTime.PropertyChangedAction = () => availabilityMarkedProfileDirty = true;
            resolution.PropertyChangedAction = () => availabilityMarkedProfileDirty = true;
            hookStatus.PropertyChangedAction = () => availabilityMarkedProfileDirty = true;

            Assert.IsFalse(displayTime.IsEntryEnabled,
                "the local hook source has no display-time data");

            // Match OverlayViewModel.SetOverlayMode: arm the destination before releasing the
            // current hook so there is no transient RTSS mode between the two writes.
            _enableHookFreeOverlay = true;
            _configurationChanges.OnNext(
                (nameof(IAppConfiguration.EnableHookFreeOverlay), true));
            _enableHookOverlay = false;
            _configurationChanges.OnNext(
                (nameof(IAppConfiguration.EnableHookOverlay), false));

            var hookFreeEntries = await provider.GetOverlayEntries(updateFormats: false);
            Assert.AreSame(framerate,
                hookFreeEntries.Single(entry => entry.Identifier == "Framerate"));
            Assert.AreEqual("Unsaved framerate group", framerate.GroupName);
            Assert.AreEqual("123456", framerate.Color);
            Assert.AreSame(displayTime,
                hookFreeEntries.Single(entry => entry.Identifier == "DisplayTime"));
            Assert.AreEqual("Unsaved displaytime group", displayTime.GroupName);
            Assert.IsTrue(displayTime.IsEntryEnabled);
            Assert.AreSame(resolution,
                hookFreeEntries.Single(entry => entry.Identifier == "Resolution"));
            Assert.IsFalse(resolution.IsEntryEnabled);
            Assert.IsTrue(resolution.ShowOnOverlay,
                "renderer gating must preserve the user's profile selection");
            Assert.IsFalse(hookStatus.IsEntryEnabled);
            Assert.IsTrue(hookStatus.ShowOnOverlay,
                "hook-only selections must survive a temporary renderer switch too");
            Assert.IsFalse(availabilityMarkedProfileDirty,
                "derived renderer availability is not a profile edit");

            _enableHookOverlay = true;
            _configurationChanges.OnNext(
                (nameof(IAppConfiguration.EnableHookOverlay), true));
            _enableHookFreeOverlay = false;
            _configurationChanges.OnNext(
                (nameof(IAppConfiguration.EnableHookFreeOverlay), false));

            var hookEntries = await provider.GetOverlayEntries(updateFormats: false);
            Assert.AreSame(framerate,
                hookEntries.Single(entry => entry.Identifier == "Framerate"));
            Assert.AreEqual("Unsaved framerate group", framerate.GroupName);
            Assert.AreEqual("123456", framerate.Color);
            Assert.IsFalse(displayTime.IsEntryEnabled);
            Assert.IsTrue(resolution.IsEntryEnabled);
            Assert.IsTrue(resolution.ShowOnOverlay);
            Assert.IsTrue(hookStatus.IsEntryEnabled);
            Assert.IsTrue(hookStatus.ShowOnOverlay);
        }

        [TestMethod]
        public async Task InitialFormattingPass_DoesNotLookLikeAProfileEdit()
        {
            var provider = CreateProvider(hookOverlayEnabled: true);
            var entries = await provider.GetOverlayEntries(updateFormats: false);
            bool profileChanged = false;

            foreach (var entry in entries)
            {
                entry.PropertyChangedAction = () => profileChanged = true;
            }

            // OverlayViewModel marks every format stale after loading. The first regular OSD
            // refresh then generates GroupNameFormat for every entry. This is derived output,
            // not a user edit, and therefore must leave the Save button disabled.
            provider.UpdateOverlayEntryFormats();
            await provider.GetOverlayEntries(updateFormats: true);

            Assert.IsFalse(profileChanged,
                "the generated formatting pass must not enable saving after startup");

            entries.Single(entry => entry.Identifier == "Framerate").Color = "123456";
            Assert.IsTrue(profileChanged,
                "a real Overlay Items edit must still enable saving");
        }

        [TestMethod]
        public async Task SaveProfile_ClearsPendingChangesOnlyForTheActiveSlot()
        {
            var provider = CreateProvider(hookOverlayEnabled: true);
            await provider.GetOverlayEntries(updateFormats: false);

            provider.MarkPendingChanges();
            Assert.IsTrue(provider.HasPendingChanges);

            await provider.SaveOverlayEntriesToJson(targetConfig: 1);
            Assert.IsTrue(provider.HasPendingChanges,
                "saving a copy must not mark the active profile as saved");

            await provider.SaveOverlayEntriesToJson(targetConfig: 0);
            Assert.IsFalse(provider.HasPendingChanges,
                "saving the active profile must clear the shutdown warning");
        }

        [TestMethod]
        public async Task StatusStream_DrivesTheEntryValue()
        {
            var provider = CreateProvider(hookOverlayEnabled: true);
            await Task.Delay(500);

            _statusStream.OnNext(new HookOverlayStatus(EHookOverlayStatus.Active, processId: 4711,
                runtime: "DXGI", detail: "hook active"));

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var hookStatus = entries.Single(entry => entry.Identifier == HookOverlayStatusIdentifier);

            Assert.AreEqual("Active", hookStatus.Value);

            _statusStream.OnNext(new HookOverlayStatus(EHookOverlayStatus.Fallback, processId: 4711,
                runtime: "Vulkan", detail: "hook-free overlay serves it"));

            entries = await provider.GetOverlayEntries(updateFormats: false);
            hookStatus = entries.Single(entry => entry.Identifier == HookOverlayStatusIdentifier);

            Assert.AreEqual("Fallback", hookStatus.Value,
                "the entry has to follow later states, not just the first one");
        }

        [TestMethod]
        public async Task SavedConfigWithoutConditionalEntries_GetsThemInsertedInDefaultOrder()
        {
            // Older configurations can predate hook-only entries and can also have renderer-gated
            // rows omitted by the old load filter. All of them need to rejoin the shared profile.
            var legacyEntries = OverlayUtils.GetOverlayEntryDefaults(_appConfigMock.Object)
                .Where(entry => entry.Identifier != HookOverlayStatusIdentifier
                    && entry.Identifier != FrameGenerationTechnologyIdentifier
                    && entry.Identifier != FrameGenerationStatusIdentifier
                    && entry.Identifier != "DisplayTime"
                    && entry.Identifier != "Resolution")
                .ToList();

            File.WriteAllText(
                Path.Combine(_testConfigFolder, "OverlayEntryConfiguration_0.json"),
                JsonConvert.SerializeObject(new OverlayEntryPersistence { OverlayEntries = legacyEntries }));

            var provider = CreateProvider(hookOverlayEnabled: true);
            await Task.Delay(500);

            var identifiers = (await provider.GetOverlayEntries(updateFormats: false))
                .Select(entry => entry.Identifier)
                .ToList();

            int hookStatusIndex = identifiers.IndexOf(HookOverlayStatusIdentifier);
            int frameGenerationTechnologyIndex = identifiers.IndexOf(
                FrameGenerationTechnologyIdentifier);
            int frameGenerationStatusIndex = identifiers.IndexOf(FrameGenerationStatusIdentifier);

            Assert.AreNotEqual(-1, hookStatusIndex,
                "a configuration saved before the entry existed has to receive it on load");
            Assert.AreEqual(identifiers.IndexOf("CaptureServiceStatus") + 1, hookStatusIndex,
                "the migrated entry belongs behind its predecessor from the defaults list");
            Assert.AreEqual(hookStatusIndex + 1, frameGenerationTechnologyIndex,
                "the migrated technology entry belongs behind the hook status");
            Assert.AreEqual(frameGenerationTechnologyIndex + 1, frameGenerationStatusIndex,
                "the migrated status entry belongs behind the technology");
            CollectionAssert.Contains(identifiers, "DisplayTime",
                "the renderer-gated Displaytime entry must be restored to the shared profile");
            CollectionAssert.Contains(identifiers, "Resolution",
                "the renderer-gated resolution entry must be restored to the shared profile");
            Assert.IsFalse(provider.HasPendingChanges,
                "automatic profile migration during load is the clean startup baseline");
        }

        [TestMethod]
        public async Task RuntimeDisplayTopologyChange_MarksTheLoadedProfilePending()
        {
            var provider = CreateProvider(hookOverlayEnabled: true);
            await provider.GetOverlayEntries(updateFormats: false);

            Assert.IsFalse(provider.HasPendingChanges);

            provider.RefreshDisplayEntries(new[]
            {
                new DetectedDisplay(@"\\.\DISPLAY1", 2560, 1440, isPrimary: true)
            });

            Assert.IsTrue(provider.HasPendingChanges,
                "a structural change after loading must still enable saving and the shutdown prompt");
        }

        [TestMethod]
        public async Task WithoutAnyPublishedStatus_EntryWaitsInsteadOfReportingOff()
        {
            _hookOverlayStatusMock.Setup(x => x.Current).Returns((HookOverlayStatus)null);

            var provider = CreateProvider(hookOverlayEnabled: true);
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var hookStatus = entries.Single(entry => entry.Identifier == HookOverlayStatusIdentifier);

            Assert.AreEqual("Waiting", hookStatus.Value);
        }
    }
}
