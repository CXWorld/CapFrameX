using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Hardware.Controller;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Sensor;
using CapFrameX.Test.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Prism.Events;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class StorageOverlayProfileTest
    {
        private string _configFolder;
        private MockSensorService _sensorService;

        [TestInitialize]
        public void Setup()
        {
            _configFolder = Path.Combine(Path.GetTempPath(), "CxStorageProfile_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_configFolder);
            _sensorService = new MockSensorService(seed: 42);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _sensorService.Dispose();
            Directory.Delete(_configFolder, recursive: true);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task LegacyDescription_SameIdentifier_PreservesSelectionAndFormatting(bool hasStableIdentifier)
        {
            var sensor = CreateSensor("/nvme/0/temperature/0", "0_1_0", "Samsung SSD 990 PRO");
            var saved = CreateLegacyEntry(sensor);
            if (!hasStableIdentifier)
                saved.StableIdentifier = null;
            WriteProfile(saved);

            var provider = CreateProvider(sensor);
            var loaded = (await provider.GetOverlayEntries(updateFormats: false))
                .Single(entry => entry.Identifier == sensor.Identifier);

            Assert.AreEqual("Samsung SSD 990 PRO Temperature (°C)", loaded.Description);
            Assert.AreEqual(SensorIdentifierHelper.BuildStableIdentifier(sensor), loaded.StableIdentifier);
            AssertSavedPresentation(saved, loaded);
            Assert.IsFalse(provider.HasPendingChanges,
                "Refreshing a legacy drive description must establish a clean profile baseline.");
        }

        [TestMethod]
        public async Task LegacyDescription_ShiftedIdentifier_UsesStableIdentifierToSelectCorrectDevice()
        {
            var oldSensor = CreateSensor("/nvme/0/temperature/0", "0_1_0", "Samsung SSD 990 PRO");
            var saved = CreateLegacyEntry(oldSensor);
            WriteProfile(saved);

            var otherSensor = CreateSensor("/nvme/1/temperature/0", "1_1_0", "WD_BLACK SN850X");
            var currentSensor = CreateSensor("/nvme/2/temperature/0", "2_1_0", oldSensor.HardwareName);
            var provider = CreateProvider(otherSensor, currentSensor);
            var storageEntries = (await provider.GetOverlayEntries(updateFormats: false))
                .Where(entry => entry.OverlayEntryType == EOverlayEntryType.HDD)
                .ToArray();

            Assert.AreEqual(2, storageEntries.Length);
            var migrated = storageEntries.Single(entry => entry.Identifier == currentSensor.Identifier);
            var other = storageEntries.Single(entry => entry.Identifier == otherSensor.Identifier);
            Assert.AreEqual("Samsung SSD 990 PRO Temperature (°C)", migrated.Description);
            Assert.AreEqual("WD_BLACK SN850X Temperature (°C)", other.Description);
            AssertSavedPresentation(saved, migrated);
            Assert.IsFalse(other.ShowOnOverlay,
                "The saved selection belongs to the Samsung device, not the other temperature sensor.");
            Assert.AreEqual("Drive Temperature", other.GroupName);
        }

        private static SensorEntry CreateSensor(string identifier, string sortKey, string hardwareName)
        {
            return new SensorEntry
            {
                Identifier = identifier,
                SortKey = sortKey,
                Name = "Drive Temperature",
                HardwareName = hardwareName,
                HardwareType = "Storage",
                SensorType = "Temperature"
            };
        }

        [TestMethod]
        public async Task PercentageDisplayMode_SurvivesSensorReconciliationAndProfileReload()
        {
            var sensor = CreateSensor("/nvme/0/load/0", "0_2_0", "Samsung SSD 990 PRO");
            sensor.Name = "Drive Activity";
            sensor.SensorType = "Load";
            var saved = (OverlayEntryWrapper)SensorOverlayEntryFactory.Create(sensor);
            saved.ValueDisplayMode = EOverlayValueDisplayMode.TextAndBar;
            WriteProfile(saved);

            var provider = CreateProvider(sensor);
            var loaded = (await provider.GetOverlayEntries(false)).Single(entry => entry.Identifier == sensor.Identifier);
            Assert.AreEqual(EOverlayValueDisplayMode.TextAndBar, loaded.ValueDisplayMode);
            Assert.IsTrue(loaded.SupportsPercentageBar);
            Assert.IsFalse(provider.HasPendingChanges);

            loaded.ValueDisplayMode = EOverlayValueDisplayMode.Bar;
            await provider.SaveOverlayEntriesToJson(0);
            await provider.SwitchConfigurationTo(0);
            var reloaded = (await provider.GetOverlayEntries(false)).Single(entry => entry.Identifier == sensor.Identifier);
            Assert.AreEqual(EOverlayValueDisplayMode.Bar, reloaded.ValueDisplayMode);
            Assert.IsFalse(provider.HasPendingChanges);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task BulkFormatting_CopiesPercentageModeOnlyToEligibleEntries(bool sameGroupOnly)
        {
            var provider = CreateProvider();
            var entries = await provider.GetOverlayEntries(false);
            var selected = entries.First(entry => entry.SupportsPercentageBar);
            var percentageTarget = entries.Last(entry => entry.SupportsPercentageBar && entry != selected);
            var textTarget = entries.First(entry => !entry.SupportsPercentageBar);
            percentageTarget.GroupName = selected.GroupName;
            textTarget.GroupName = selected.GroupName;
            selected.ValueDisplayMode = EOverlayValueDisplayMode.TextAndBar;
            var checkboxes = new OverlayEntryFormatChange { Colors = false, Limits = false, Format = true };

            if (sameGroupOnly)
                provider.SetFormatForGroupName(selected.GroupName, selected, checkboxes);
            else
                provider.SetFormatForAllValues(selected, checkboxes);

            foreach (var entry in entries.Where(entry => !sameGroupOnly || entry.GroupName == selected.GroupName))
            {
                Assert.AreEqual(entry.SupportsPercentageBar ? EOverlayValueDisplayMode.TextAndBar
                    : EOverlayValueDisplayMode.Text, entry.ValueDisplayMode, entry.Identifier);
            }

            checkboxes.Format = false;
            selected.ValueDisplayMode = EOverlayValueDisplayMode.Bar;
            provider.SetFormatForAllValues(selected, checkboxes);
            foreach (var entry in entries.Where(entry => entry != selected && entry.SupportsPercentageBar
                && (!sameGroupOnly || entry.GroupName == selected.GroupName)))
            {
                Assert.AreEqual(EOverlayValueDisplayMode.TextAndBar, entry.ValueDisplayMode,
                    "The Format checkbox must control copying of the display mode.");
            }
        }

        private static OverlayEntryWrapper CreateLegacyEntry(SensorEntry sensor)
        {
            var entry = (OverlayEntryWrapper)SensorOverlayEntryFactory.Create(sensor);
            entry.Description = "Drive Temperature (°C)";
            entry.ShowOnOverlay = true;
            entry.GroupName = "System SSD";
            entry.ShowGraph = true;
            entry.Color = "123456";
            entry.ValueFontSize = 120;
            entry.UpperLimitValue = "70";
            entry.LowerLimitValue = "20";
            entry.GroupColor = "654321";
            entry.GroupFontSize = 90;
            entry.GroupSeparators = 2;
            entry.UpperLimitColor = "FF0000";
            entry.LowerLimitColor = "00FF00";
            return entry;
        }

        private static void AssertSavedPresentation(IOverlayEntry saved, IOverlayEntry loaded)
        {
            Assert.AreEqual(saved.ShowOnOverlay, loaded.ShowOnOverlay);
            Assert.AreEqual(saved.GroupName, loaded.GroupName);
            Assert.AreEqual(saved.ShowGraph, loaded.ShowGraph);
            Assert.AreEqual(saved.Color, loaded.Color);
            Assert.AreEqual(saved.ValueFontSize, loaded.ValueFontSize);
            Assert.AreEqual(saved.UpperLimitValue, loaded.UpperLimitValue);
            Assert.AreEqual(saved.LowerLimitValue, loaded.LowerLimitValue);
            Assert.AreEqual(saved.GroupColor, loaded.GroupColor);
            Assert.AreEqual(saved.GroupFontSize, loaded.GroupFontSize);
            Assert.AreEqual(saved.GroupSeparators, loaded.GroupSeparators);
            Assert.AreEqual(saved.UpperLimitColor, loaded.UpperLimitColor);
            Assert.AreEqual(saved.LowerLimitColor, loaded.LowerLimitColor);
        }

        private void WriteProfile(OverlayEntryWrapper entry)
        {
            File.WriteAllText(Path.Combine(_configFolder, "OverlayEntryConfiguration_0.json"),
                JsonConvert.SerializeObject(new OverlayEntryPersistence
                {
                    OverlayEntries = new List<OverlayEntryWrapper> { entry }
                }));
        }

        private OverlayEntryProvider CreateProvider(params SensorEntry[] sensors)
        {
            var core = new OverlayEntryCore();
            foreach (var sensor in sensors)
                core.OverlayEntryDict.TryAdd(sensor.Identifier, SensorOverlayEntryFactory.Create(sensor));
            core.OverlayEntryCoreCompletionSource.SetResult(true);

            var appConfiguration = new Mock<IAppConfiguration>();
            appConfiguration.Setup(x => x.OverlayEntryConfigurationFile).Returns(0);
            appConfiguration.Setup(x => x.HardwareInfoSource).Returns("Auto");
            appConfiguration.Setup(x => x.OnValueChanged).Returns(Observable.Never<(string key, object value)>());

            var eventAggregator = new Mock<IEventAggregator>();
            eventAggregator.Setup(x => x.GetEvent<PubSubEvent<ViewMessages.OptionPopupClosed>>())
                .Returns(new PubSubEvent<ViewMessages.OptionPopupClosed>());

            var rtssService = new Mock<IRTSSService>();
            rtssService.Setup(x => x.ProcessIdStream).Returns(new BehaviorSubject<int>(0));
            rtssService.Setup(x => x.GetCurrentFramerate(It.IsAny<int>())).Returns(Tuple.Create(0.0, 0.0));

            var pathService = new Mock<IPathService>();
            pathService.Setup(x => x.ConfigFolder).Returns(_configFolder);

            var hookStatus = new Mock<IHookOverlayStatusService>();
            hookStatus.Setup(x => x.Current).Returns(new HookOverlayStatus(EHookOverlayStatus.Disabled));
            hookStatus.Setup(x => x.StatusStream).Returns(Observable.Never<HookOverlayStatus>());

            return new OverlayEntryProvider(_sensorService, appConfiguration.Object, eventAggregator.Object,
                Mock.Of<IOnlineMetricService>(), Mock.Of<ISystemInfo>(), rtssService.Object,
                Mock.Of<ISensorConfig>(), core, Mock.Of<IThreadAffinityController>(), pathService.Object,
                hookStatus.Object, Mock.Of<ILogger<OverlayEntryProvider>>(), () => Array.Empty<DetectedDisplay>());
        }
    }
}
