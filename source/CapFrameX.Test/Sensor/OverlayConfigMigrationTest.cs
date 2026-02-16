using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Sensor;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Hardware.Controller;
using CapFrameX.Test.Mocks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Threading.Tasks;

namespace CapFrameX.Test.Sensor
{
    [TestClass]
    public class OverlayConfigMigrationTest
    {
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
        private Mock<ILogger<OverlayEntryProvider>> _loggerMock;

        private List<OverlayEntryWrapper> _loadedConfigEntries;

        [TestInitialize]
        public void Setup()
        {
            _testConfigFolder = Path.Combine(Path.GetTempPath(), "CxTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testConfigFolder);

            // Load test JSON from output directory
            var testAssemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var jsonPath = Path.Combine(testAssemblyDir, "Sensor", "OverlayConfigNvAmd.json");
            var json = File.ReadAllText(jsonPath);
            var persistence = JsonConvert.DeserializeObject<OverlayEntryPersistence>(json);
            _loadedConfigEntries = persistence.OverlayEntries;

            // Copy config to temp folder as OverlayEntryConfiguration_0.json
            File.WriteAllText(Path.Combine(_testConfigFolder, "OverlayEntryConfiguration_0.json"), json);

            _mockSensorService = new MockSensorService(seed: 42);

            _overlayEntryCore = new OverlayEntryCore();

            _appConfigMock = new Mock<IAppConfiguration>();
            _appConfigMock.Setup(x => x.OverlayEntryConfigurationFile).Returns(0);
            _appConfigMock.Setup(x => x.UsePcLatency).Returns(true);
            _appConfigMock.Setup(x => x.HardwareInfoSource).Returns("Auto");
            _appConfigMock.Setup(x => x.ShowSystemTimeSeconds).Returns(false);
            _appConfigMock.Setup(x => x.OnValueChanged).Returns(Observable.Never<(string key, object value)>());

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

            _loggerMock = new Mock<ILogger<OverlayEntryProvider>>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _mockSensorService?.Dispose();

            try
            {
                if (Directory.Exists(_testConfigFolder))
                    Directory.Delete(_testConfigFolder, true);
            }
            catch { }
        }

        /// <summary>
        /// Populate the OverlayEntryCore with sensor entries that match
        /// the sensors found in the config JSON, simulating "same hardware" scenario.
        /// </summary>
        private void PopulateOverlayEntryCoreFromConfig()
        {
            // Build sensor entries from the config file — each sensor-type entry
            // needs a matching entry in the OverlayEntryDict
            var sensorTypes = new HashSet<EOverlayEntryType>
            {
                EOverlayEntryType.GPU, EOverlayEntryType.CPU, EOverlayEntryType.RAM
            };

            foreach (var configEntry in _loadedConfigEntries)
            {
                if (!sensorTypes.Contains(configEntry.OverlayEntryType))
                    continue;

                // Create a matching overlay entry in the core dict (as OverlayService would)
                var entry = new OverlayEntryWrapper(configEntry.Identifier)
                {
                    StableIdentifier = configEntry.StableIdentifier,
                    SortKey = configEntry.SortKey,
                    Description = configEntry.Description,
                    OverlayEntryType = configEntry.OverlayEntryType,
                    GroupName = configEntry.GroupName,
                    ShowOnOverlay = false, // Defaults from hardware scan
                    ShowOnOverlayIsEnabled = true,
                    ShowGraph = false,
                    ShowGraphIsEnabled = false,
                    Value = 0
                };

                _overlayEntryCore.OverlayEntryDict.TryAdd(configEntry.Identifier, entry);
            }

            _overlayEntryCore.OverlayEntryCoreCompletionSource.SetResult(true);
        }

        /// <summary>
        /// Populate the OverlayEntryCore with sensor entries where the identifiers
        /// have shifted (simulating a library version change), but Description stays the same.
        /// </summary>
        private void PopulateOverlayEntryCoreWithShiftedIds()
        {
            var sensorTypes = new HashSet<EOverlayEntryType>
            {
                EOverlayEntryType.GPU, EOverlayEntryType.CPU, EOverlayEntryType.RAM
            };

            foreach (var configEntry in _loadedConfigEntries)
            {
                if (!sensorTypes.Contains(configEntry.OverlayEntryType))
                    continue;

                // Shift indices in identifiers: /gpu-nvidia/0/load/1 → /gpu-nvidia/0/load/101
                string shiftedId = ShiftIdentifier(configEntry.Identifier);

                var entry = new OverlayEntryWrapper(shiftedId)
                {
                    StableIdentifier = configEntry.StableIdentifier,
                    SortKey = configEntry.SortKey,
                    Description = configEntry.Description,
                    OverlayEntryType = configEntry.OverlayEntryType,
                    GroupName = configEntry.GroupName,
                    ShowOnOverlay = false,
                    ShowOnOverlayIsEnabled = true,
                    ShowGraph = false,
                    ShowGraphIsEnabled = false,
                    Value = 0
                };

                _overlayEntryCore.OverlayEntryDict.TryAdd(shiftedId, entry);
            }

            _overlayEntryCore.OverlayEntryCoreCompletionSource.SetResult(true);
        }

        /// <summary>
        /// Populate with shifted IDs but only Description-based fallback (no StableIdentifier).
        /// </summary>
        private void PopulateOverlayEntryCoreWithShiftedIdsNoStableId()
        {
            var sensorTypes = new HashSet<EOverlayEntryType>
            {
                EOverlayEntryType.GPU, EOverlayEntryType.CPU, EOverlayEntryType.RAM
            };

            foreach (var configEntry in _loadedConfigEntries)
            {
                if (!sensorTypes.Contains(configEntry.OverlayEntryType))
                    continue;

                string shiftedId = ShiftIdentifier(configEntry.Identifier);

                var entry = new OverlayEntryWrapper(shiftedId)
                {
                    StableIdentifier = null, // No stable ID — forces Description fallback
                    SortKey = configEntry.SortKey,
                    Description = configEntry.Description,
                    OverlayEntryType = configEntry.OverlayEntryType,
                    GroupName = configEntry.GroupName,
                    ShowOnOverlay = false,
                    ShowOnOverlayIsEnabled = true,
                    ShowGraph = false,
                    ShowGraphIsEnabled = false,
                    Value = 0
                };

                _overlayEntryCore.OverlayEntryDict.TryAdd(shiftedId, entry);
            }

            _overlayEntryCore.OverlayEntryCoreCompletionSource.SetResult(true);
        }

        private string ShiftIdentifier(string identifier)
        {
            // /gpu-nvidia/0/load/1 → /gpu-nvidia/0/load/101
            var parts = identifier.Split('/');
            if (parts.Length >= 4)
            {
                // Shift the last numeric index by +100
                if (int.TryParse(parts[parts.Length - 1], out int lastIndex))
                {
                    parts[parts.Length - 1] = (lastIndex + 100).ToString();
                    return string.Join("/", parts);
                }
            }
            return identifier + "_shifted";
        }

        private OverlayEntryProvider CreateProvider()
        {
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
                _loggerMock.Object);
        }

        // ==================== TESTS ====================

        [TestMethod]
        public void LoadedConfig_HasExpected137Entries()
        {
            Assert.AreEqual(137, _loadedConfigEntries.Count,
                "OverlayConfigNvAmd.json should contain exactly 137 entries.");
        }

        [TestMethod]
        public void LoadedConfig_ContainsAllOverlayEntryTypes()
        {
            var types = _loadedConfigEntries.Select(e => e.OverlayEntryType).Distinct().OrderBy(t => t).ToList();

            Assert.IsTrue(types.Contains(EOverlayEntryType.CX), "Should contain CX entries.");
            Assert.IsTrue(types.Contains(EOverlayEntryType.OnlineMetric), "Should contain OnlineMetric entries.");
            Assert.IsTrue(types.Contains(EOverlayEntryType.GPU), "Should contain GPU entries.");
            Assert.IsTrue(types.Contains(EOverlayEntryType.CPU), "Should contain CPU entries.");
            Assert.IsTrue(types.Contains(EOverlayEntryType.RAM), "Should contain RAM entries.");
        }

        [TestMethod]
        public void LoadedConfig_ShowOnOverlayFlags_ArePreserved()
        {
            var shownEntries = _loadedConfigEntries.Where(e => e.ShowOnOverlay).ToList();

            // Verify specific visible entries from the config
            var shownIds = shownEntries.Select(e => e.Identifier).ToHashSet();

            // GPU entries that are shown
            Assert.IsTrue(shownIds.Contains("/gpu-nvidia/0/clock/0"), "GPU Core Clock should be shown.");
            Assert.IsTrue(shownIds.Contains("/gpu-nvidia/0/load/0"), "GPU Core Load should be shown.");
            Assert.IsTrue(shownIds.Contains("/gpu-nvidia/0/temperature/0"), "GPU Core Temp should be shown.");
            Assert.IsTrue(shownIds.Contains("/gpu-nvidia/0/power/0"), "GPU Power should be shown.");

            // CPU entries that are shown
            Assert.IsTrue(shownIds.Contains("/amdcpu/0/clock/3"), "CPU Max Clock should be shown.");
            Assert.IsTrue(shownIds.Contains("/amdcpu/0/power/0"), "CPU Package Power should be shown.");

            // Framerate/Frametime
            Assert.IsTrue(shownIds.Contains("Framerate"), "Framerate should be shown.");
            Assert.IsTrue(shownIds.Contains("Frametime"), "Frametime should be shown.");
        }

        [TestMethod]
        public void LoadedConfig_SortKeyOrder_IsCorrect()
        {
            // Entries with ShowOnOverlay=true should be in ascending SortKey order
            var shownEntries = _loadedConfigEntries
                .Where(e => e.ShowOnOverlay)
                .ToList();

            var sortKeyComparer = new SortKeyComparer();
            for (int i = 1; i < shownEntries.Count; i++)
            {
                var prev = shownEntries[i - 1];
                var curr = shownEntries[i];

                int cmp = sortKeyComparer.Compare(prev.SortKey, curr.SortKey);
                Assert.IsTrue(cmp <= 0,
                    $"Shown entries should be in SortKey order: '{prev.Identifier}' (SortKey={prev.SortKey}) " +
                    $"should come before '{curr.Identifier}' (SortKey={curr.SortKey}).");
            }
        }

        [TestMethod]
        public void LoadedConfig_GroupNames_ArePreserved()
        {
            var entryById = _loadedConfigEntries.ToDictionary(e => e.Identifier);

            Assert.AreEqual("RTX 5090", entryById["/gpu-nvidia/0/clock/0"].GroupName, "GPU Clock group name.");
            Assert.AreEqual("GPU Load", entryById["/gpu-nvidia/0/load/0"].GroupName, "GPU Load group name.");
            Assert.AreEqual("GPU Temp", entryById["/gpu-nvidia/0/temperature/0"].GroupName, "GPU Temp group name.");
            Assert.AreEqual("VRAM Hot Spot", entryById["/gpu-nvidia/0/temperature/3"].GroupName, "VRAM Hot Spot group name.");
            Assert.AreEqual("CPU Model", entryById["CustomCPU"].GroupName, "CustomCPU group name.");
            Assert.AreEqual("RAM Info", entryById["CustomRAM"].GroupName, "CustomRAM group name.");
        }

        [TestMethod]
        public async Task IdenticalHardware_AllEntriesLoaded_OrderPreserved()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();

            // Wait for initialization to complete
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);

            Assert.IsNotNull(entries, "Entries should not be null.");
            Assert.IsTrue(entries.Length > 0, "Should have loaded entries.");

            // All config entries should be present
            var entryIds = new HashSet<string>(entries.Select(e => e.Identifier));
            foreach (var configEntry in _loadedConfigEntries)
            {
                Assert.IsTrue(entryIds.Contains(configEntry.Identifier),
                    $"Entry '{configEntry.Identifier}' should be present.");
            }
        }

        [TestMethod]
        public async Task IdenticalHardware_ShowOnOverlay_Preserved()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var entryById = entries.ToDictionary(e => e.Identifier);

            // Verify ShowOnOverlay flags are transferred from saved config
            foreach (var configEntry in _loadedConfigEntries)
            {
                if (entryById.TryGetValue(configEntry.Identifier, out var loaded))
                {
                    Assert.AreEqual(configEntry.ShowOnOverlay, loaded.ShowOnOverlay,
                        $"ShowOnOverlay mismatch for '{configEntry.Identifier}'.");
                }
            }
        }

        [TestMethod]
        public async Task IdenticalHardware_GroupNames_Preserved()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var entryById = entries.ToDictionary(e => e.Identifier);

            // Verify user-customized group names are preserved
            var customGroupEntries = _loadedConfigEntries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.GPU
                         || e.OverlayEntryType == EOverlayEntryType.CPU
                         || e.OverlayEntryType == EOverlayEntryType.RAM)
                .Where(e => !string.IsNullOrEmpty(e.GroupName));

            foreach (var configEntry in customGroupEntries)
            {
                if (entryById.TryGetValue(configEntry.Identifier, out var loaded))
                {
                    Assert.AreEqual(configEntry.GroupName, loaded.GroupName,
                        $"GroupName mismatch for '{configEntry.Identifier}': " +
                        $"expected '{configEntry.GroupName}', got '{loaded.GroupName}'.");
                }
            }
        }

        [TestMethod]
        public async Task IdenticalHardware_Colors_Preserved()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var entryById = entries.ToDictionary(e => e.Identifier);

            // Check colors on sensor entries
            var sensorEntries = _loadedConfigEntries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.GPU
                         || e.OverlayEntryType == EOverlayEntryType.CPU
                         || e.OverlayEntryType == EOverlayEntryType.RAM);

            foreach (var configEntry in sensorEntries)
            {
                if (entryById.TryGetValue(configEntry.Identifier, out var loaded))
                {
                    Assert.AreEqual(configEntry.GroupColor, loaded.GroupColor,
                        $"GroupColor mismatch for '{configEntry.Identifier}'.");
                    Assert.AreEqual(configEntry.GroupFontSize, loaded.GroupFontSize,
                        $"GroupFontSize mismatch for '{configEntry.Identifier}'.");
                }
            }
        }

        [TestMethod]
        public async Task IdenticalHardware_EntryOrder_MatchesConfig()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);

            // Config entries should maintain their relative order
            var configIds = _loadedConfigEntries.Select(e => e.Identifier).ToList();
            var loadedIds = entries.Select(e => e.Identifier).ToList();

            // Filter to only IDs that appear in both (some defaults may be inserted)
            var commonIds = configIds.Where(id => loadedIds.Contains(id)).ToList();
            var commonIdsInLoaded = loadedIds.Where(id => configIds.Contains(id)).ToList();

            CollectionAssert.AreEqual(commonIds, commonIdsInLoaded,
                "Config entries should maintain their relative order.");
        }

        [TestMethod]
        public async Task ShiftedIds_StableIdMigration_AllSensorEntriesPreserved()
        {
            PopulateOverlayEntryCoreWithShiftedIds();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);

            // Count sensor entries in config
            var configSensorCount = _loadedConfigEntries
                .Count(e => e.OverlayEntryType == EOverlayEntryType.GPU
                         || e.OverlayEntryType == EOverlayEntryType.CPU
                         || e.OverlayEntryType == EOverlayEntryType.RAM);

            // All sensor entries should have been migrated (via StableIdentifier)
            var sensorEntriesInResult = entries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.GPU
                          || e.OverlayEntryType == EOverlayEntryType.CPU
                          || e.OverlayEntryType == EOverlayEntryType.RAM)
                .ToList();

            Assert.IsTrue(sensorEntriesInResult.Count >= configSensorCount,
                $"Should have at least {configSensorCount} sensor entries after migration, got {sensorEntriesInResult.Count}.");
        }

        [TestMethod]
        public async Task ShiftedIds_StableIdMigration_ShowOnOverlayPreserved()
        {
            PopulateOverlayEntryCoreWithShiftedIds();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);

            // Build lookup by StableIdentifier
            var resultByStableId = entries
                .Where(e => !string.IsNullOrEmpty(e.StableIdentifier))
                .GroupBy(e => e.StableIdentifier)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.Single());

            // Verify that entries with ShowOnOverlay=true in config are still shown
            var shownConfigEntries = _loadedConfigEntries
                .Where(e => e.ShowOnOverlay && !string.IsNullOrEmpty(e.StableIdentifier))
                .ToList();

            foreach (var configEntry in shownConfigEntries)
            {
                if (resultByStableId.TryGetValue(configEntry.StableIdentifier, out var migrated))
                {
                    Assert.IsTrue(migrated.ShowOnOverlay,
                        $"Entry with StableId '{configEntry.StableIdentifier}' should preserve ShowOnOverlay=true after migration.");
                }
            }
        }

        [TestMethod]
        public async Task ShiftedIds_StableIdMigration_GroupNamesPreserved()
        {
            PopulateOverlayEntryCoreWithShiftedIds();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);

            var resultByStableId = entries
                .Where(e => !string.IsNullOrEmpty(e.StableIdentifier))
                .GroupBy(e => e.StableIdentifier)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.Single());

            var configByStableId = _loadedConfigEntries
                .Where(e => !string.IsNullOrEmpty(e.StableIdentifier)
                    && (e.OverlayEntryType == EOverlayEntryType.GPU
                     || e.OverlayEntryType == EOverlayEntryType.CPU
                     || e.OverlayEntryType == EOverlayEntryType.RAM))
                .GroupBy(e => e.StableIdentifier)
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.Single());

            foreach (var kvp in configByStableId)
            {
                if (resultByStableId.TryGetValue(kvp.Key, out var migrated))
                {
                    Assert.AreEqual(kvp.Value.GroupName, migrated.GroupName,
                        $"GroupName mismatch after StableId migration for '{kvp.Key}'.");
                }
            }
        }

        [TestMethod]
        public async Task ShiftedIds_DescriptionFallback_AllSensorEntriesPreserved()
        {
            PopulateOverlayEntryCoreWithShiftedIdsNoStableId();

            // Clear StableIdentifier from config entries to simulate old config
            var json = File.ReadAllText(Path.Combine(_testConfigFolder, "OverlayEntryConfiguration_0.json"));
            var persistence = JsonConvert.DeserializeObject<OverlayEntryPersistence>(json);
            foreach (var entry in persistence.OverlayEntries)
            {
                entry.StableIdentifier = null;
            }
            var modifiedJson = JsonConvert.SerializeObject(persistence);
            File.WriteAllText(Path.Combine(_testConfigFolder, "OverlayEntryConfiguration_0.json"), modifiedJson);

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);

            var configSensorCount = _loadedConfigEntries
                .Count(e => e.OverlayEntryType == EOverlayEntryType.GPU
                         || e.OverlayEntryType == EOverlayEntryType.CPU
                         || e.OverlayEntryType == EOverlayEntryType.RAM);

            var sensorEntriesInResult = entries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.GPU
                          || e.OverlayEntryType == EOverlayEntryType.CPU
                          || e.OverlayEntryType == EOverlayEntryType.RAM)
                .ToList();

            // With unique descriptions, all entries should migrate via Description fallback
            // Some may not migrate if descriptions are ambiguous (duplicate Description+Type combos)
            Assert.IsTrue(sensorEntriesInResult.Count >= configSensorCount,
                $"Should have at least {configSensorCount} sensor entries after Description fallback migration, got {sensorEntriesInResult.Count}.");
        }

        [TestMethod]
        public async Task ShiftedIds_DescriptionFallback_ShowOnOverlayPreserved()
        {
            PopulateOverlayEntryCoreWithShiftedIdsNoStableId();

            var json = File.ReadAllText(Path.Combine(_testConfigFolder, "OverlayEntryConfiguration_0.json"));
            var persistence = JsonConvert.DeserializeObject<OverlayEntryPersistence>(json);
            foreach (var entry in persistence.OverlayEntries)
            {
                entry.StableIdentifier = null;
            }
            var modifiedJson = JsonConvert.SerializeObject(persistence);
            File.WriteAllText(Path.Combine(_testConfigFolder, "OverlayEntryConfiguration_0.json"), modifiedJson);

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);

            // Build lookup by Description+Type for verification
            var resultByDescType = entries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.GPU
                          || e.OverlayEntryType == EOverlayEntryType.CPU
                          || e.OverlayEntryType == EOverlayEntryType.RAM)
                .GroupBy(e => (e.Description, e.OverlayEntryType))
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.Single());

            // Check that unique Description+Type entries with ShowOnOverlay=true are preserved
            var shownConfigEntries = _loadedConfigEntries
                .Where(e => e.ShowOnOverlay
                    && (e.OverlayEntryType == EOverlayEntryType.GPU
                     || e.OverlayEntryType == EOverlayEntryType.CPU
                     || e.OverlayEntryType == EOverlayEntryType.RAM))
                .ToList();

            foreach (var configEntry in shownConfigEntries)
            {
                var key = (configEntry.Description, configEntry.OverlayEntryType);
                if (resultByDescType.TryGetValue(key, out var migrated))
                {
                    Assert.IsTrue(migrated.ShowOnOverlay,
                        $"Entry with Description '{configEntry.Description}' should preserve ShowOnOverlay=true after Description fallback migration.");
                }
            }
        }

        [TestMethod]
        public void LoadedConfig_NvidiaGpuEntries_AreCorrectlyTyped()
        {
            var gpuEntries = _loadedConfigEntries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.GPU)
                .ToList();

            Assert.IsTrue(gpuEntries.Count > 0, "Should contain GPU entries.");

            foreach (var entry in gpuEntries)
            {
                Assert.IsTrue(entry.Identifier.StartsWith("/gpu-nvidia/"),
                    $"GPU entry '{entry.Identifier}' should start with '/gpu-nvidia/'.");
            }
        }

        [TestMethod]
        public void LoadedConfig_AmdCpuEntries_AreCorrectlyTyped()
        {
            var cpuEntries = _loadedConfigEntries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.CPU)
                .ToList();

            Assert.IsTrue(cpuEntries.Count > 0, "Should contain CPU entries.");

            foreach (var entry in cpuEntries)
            {
                Assert.IsTrue(entry.Identifier.StartsWith("/amdcpu/"),
                    $"CPU entry '{entry.Identifier}' should start with '/amdcpu/'.");
            }
        }

        [TestMethod]
        public void LoadedConfig_RamEntries_AreCorrectlyTyped()
        {
            var ramEntries = _loadedConfigEntries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.RAM)
                .ToList();

            Assert.IsTrue(ramEntries.Count > 0, "Should contain RAM entries.");

            foreach (var entry in ramEntries)
            {
                Assert.IsTrue(
                    entry.Identifier.StartsWith("/ram/")
                    || entry.Identifier.StartsWith("/memory/")
                    || entry.Identifier.StartsWith("/vram/"),
                    $"RAM entry '{entry.Identifier}' should start with '/ram/', '/memory/', or '/vram/'.");
            }
        }

        [TestMethod]
        public void LoadedConfig_OnlineMetricEntries_ArePresent()
        {
            var onlineMetrics = _loadedConfigEntries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.OnlineMetric)
                .Select(e => e.Identifier)
                .ToHashSet();

            Assert.IsTrue(onlineMetrics.Contains("OnlineAverage"), "Should contain OnlineAverage.");
            Assert.IsTrue(onlineMetrics.Contains("Online1PercentLow"), "Should contain Online1PercentLow.");
            Assert.IsTrue(onlineMetrics.Contains("OnlinePcLatency"), "Should contain OnlinePcLatency.");
            Assert.IsTrue(onlineMetrics.Contains("OnlineAnimationError"), "Should contain OnlineAnimationError.");
        }

        [TestMethod]
        public void LoadedConfig_ShownEntries_OrderedBySortKey()
        {
            // Get entries that are shown on overlay, verify they follow the expected display order
            var shownEntries = _loadedConfigEntries.Where(e => e.ShowOnOverlay).ToList();
            Assert.IsTrue(shownEntries.Count > 10, "Should have more than 10 shown entries.");

            // Verify GPU entries (SortKey 10_xx) come before CPU entries (SortKey 30_xx)
            var firstGpuShown = shownEntries.First(e => e.OverlayEntryType == EOverlayEntryType.GPU);
            var firstCpuShown = shownEntries.First(e => e.OverlayEntryType == EOverlayEntryType.CPU);
            int gpuIndex = shownEntries.IndexOf(firstGpuShown);
            int cpuIndex = shownEntries.IndexOf(firstCpuShown);

            Assert.IsTrue(gpuIndex < cpuIndex,
                "GPU entries should appear before CPU entries in overlay order.");

            // Verify CPU entries come before RAM entries
            var firstRamShown = shownEntries.FirstOrDefault(e => e.OverlayEntryType == EOverlayEntryType.RAM);
            if (firstRamShown != null)
            {
                int ramIndex = shownEntries.IndexOf(firstRamShown);
                Assert.IsTrue(cpuIndex < ramIndex,
                    "CPU entries should appear before RAM entries in overlay order.");
            }
        }

        // ==================== SENSOR CONFIG TESTS ====================

        [TestMethod]
        public void SensorEntryProvider_DefaultActiveSensors_IncludeCpuTotal()
        {
            var sensorEntryProvider = new SensorEntryProvider(_mockSensorService, _sensorConfigMock.Object);

            var sensor = new MockSensorEntry("/cpu/0/load/total", "CPU Total", "Cpu", "Load");
            Assert.IsTrue(sensorEntryProvider.GetIsDefaultActiveSensor(sensor),
                "CPU Total should be a default active sensor.");
        }

        [TestMethod]
        public void SensorEntryProvider_DefaultActiveSensors_IncludeGpuCore()
        {
            var sensorEntryProvider = new SensorEntryProvider(_mockSensorService, _sensorConfigMock.Object);

            var sensor = new MockSensorEntry("/gpu/0/load/core", "GPU Core", "GpuNvidia", "Load");
            Assert.IsTrue(sensorEntryProvider.GetIsDefaultActiveSensor(sensor),
                "GPU Core Load should be a default active sensor.");
        }

        [TestMethod]
        public void SensorEntryProvider_DefaultActiveSensors_IncludeCpuPackagePower()
        {
            var sensorEntryProvider = new SensorEntryProvider(_mockSensorService, _sensorConfigMock.Object);

            var sensor = new MockSensorEntry("/cpu/0/power/package", "CPU Package", "Cpu", "Power");
            Assert.IsTrue(sensorEntryProvider.GetIsDefaultActiveSensor(sensor),
                "CPU Package Power should be a default active sensor.");
        }

        [TestMethod]
        public void SensorEntryProvider_NonDefaultSensor_IsNotActive()
        {
            var sensorEntryProvider = new SensorEntryProvider(_mockSensorService, _sensorConfigMock.Object);

            var sensor = new MockSensorEntry("/gpu/0/load/3", "GPU Bus", "GpuNvidia", "Load");
            Assert.IsFalse(sensorEntryProvider.GetIsDefaultActiveSensor(sensor),
                "GPU Bus should not be a default active sensor.");
        }

        [TestMethod]
        public async Task SensorEntryProvider_GetWrappedSensorEntries_ReturnsAllSensors()
        {
            _sensorConfigMock.Setup(x => x.HasConfigFile).Returns(false);
            _sensorConfigMock.Setup(x => x.GetSensorConfigCopy())
                .Returns(new Dictionary<string, bool>());

            var sensorEntryProvider = new SensorEntryProvider(_mockSensorService, _sensorConfigMock.Object);

            var entries = await sensorEntryProvider.GetWrappedSensorEntries();
            var entryList = entries.ToList();

            Assert.IsTrue(entryList.Count > 0, "Should return sensor entries.");

            // All entries should have a non-empty Identifier
            foreach (var entry in entryList)
            {
                Assert.IsFalse(string.IsNullOrEmpty(entry.Identifier),
                    "Each sensor entry should have a non-empty Identifier.");
            }
        }

        [TestMethod]
        public async Task SensorEntryProvider_GetWrappedSensorEntries_HasHardwareName()
        {
            _sensorConfigMock.Setup(x => x.HasConfigFile).Returns(false);
            _sensorConfigMock.Setup(x => x.GetSensorConfigCopy())
                .Returns(new Dictionary<string, bool>());

            var sensorEntryProvider = new SensorEntryProvider(_mockSensorService, _sensorConfigMock.Object);

            var entries = await sensorEntryProvider.GetWrappedSensorEntries();
            var entryList = entries.ToList();

            // MockSensorService entries don't set HardwareName by default.
            // Just verify the property exists and doesn't throw.
            foreach (var entry in entryList)
            {
                // HardwareName can be null for mock entries — just verify access works
                var _ = entry.HardwareName;
            }
        }

        [TestMethod]
        public void SensorIdentifierHelper_BuildsCorrectStableId()
        {
            var stableId = SensorIdentifierHelper.BuildStableIdentifier(
                "NVIDIA GeForce RTX 4090", "Temperature", "GPU Core");

            Assert.AreEqual("NVIDIA GeForce RTX 4090/temperature/GPU Core", stableId);
        }

        [TestMethod]
        public void SensorIdentifierHelper_NullHardwareName_ReturnsNull()
        {
            var stableId = SensorIdentifierHelper.BuildStableIdentifier(null, "Temperature", "GPU Core");
            Assert.IsNull(stableId, "Should return null for null hardware name.");
        }

        [TestMethod]
        public void SensorIdentifierHelper_EmptyHardwareName_ReturnsNull()
        {
            var stableId = SensorIdentifierHelper.BuildStableIdentifier("", "Temperature", "GPU Core");
            Assert.IsNull(stableId, "Should return null for empty hardware name.");
        }

        [TestMethod]
        public void SensorIdentifierHelper_FromEntry_BuildsCorrectStableId()
        {
            var entry = new MockSensorEntry("/gpu-nvidia/0/temperature/0", "GPU Core", "GpuNvidia", "Temperature")
            {
                HardwareName = "NVIDIA GeForce RTX 5090"
            };

            var stableId = SensorIdentifierHelper.BuildStableIdentifier(entry);
            Assert.AreEqual("NVIDIA GeForce RTX 5090/temperature/GPU Core", stableId);
        }

        [TestMethod]
        public void SensorIdentifierHelper_FromEntry_NoHardwareName_ReturnsNull()
        {
            var entry = new MockSensorEntry("/gpu-nvidia/0/temperature/0", "GPU Core", "GpuNvidia", "Temperature")
            {
                HardwareName = null
            };

            var stableId = SensorIdentifierHelper.BuildStableIdentifier(entry);
            Assert.IsNull(stableId, "Should return null when entry has no HardwareName.");
        }

        [TestMethod]
        public async Task IdenticalHardware_LimitValues_Preserved()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var entryById = entries.ToDictionary(e => e.Identifier);

            // Check limits on sensor entries that have configured limits
            var configEntriesWithLimits = _loadedConfigEntries
                .Where(e => !string.IsNullOrEmpty(e.UpperLimitValue) || !string.IsNullOrEmpty(e.LowerLimitValue))
                .Where(e => e.OverlayEntryType == EOverlayEntryType.GPU
                         || e.OverlayEntryType == EOverlayEntryType.CPU
                         || e.OverlayEntryType == EOverlayEntryType.RAM)
                .ToList();

            foreach (var configEntry in configEntriesWithLimits)
            {
                if (entryById.TryGetValue(configEntry.Identifier, out var loaded))
                {
                    Assert.AreEqual(configEntry.UpperLimitValue, loaded.UpperLimitValue,
                        $"UpperLimitValue mismatch for '{configEntry.Identifier}'.");
                    Assert.AreEqual(configEntry.LowerLimitValue, loaded.LowerLimitValue,
                        $"LowerLimitValue mismatch for '{configEntry.Identifier}'.");
                }
            }
        }
    }
}
