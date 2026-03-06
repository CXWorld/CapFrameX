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
using System;
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
    public class OverlayConfigMigrationNvMobileIntelTest
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
            var jsonPath = Path.Combine(testAssemblyDir, "Sensor", "OverlayConfigMigrationNvMobileIntelTest.json");
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

        private void PopulateOverlayEntryCoreFromConfig()
        {
            var sensorTypes = new HashSet<EOverlayEntryType>
            {
                EOverlayEntryType.GPU, EOverlayEntryType.CPU, EOverlayEntryType.RAM
            };

            foreach (var configEntry in _loadedConfigEntries)
            {
                if (!sensorTypes.Contains(configEntry.OverlayEntryType))
                    continue;

                var entry = new OverlayEntryWrapper(configEntry.Identifier)
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

                _overlayEntryCore.OverlayEntryDict.TryAdd(configEntry.Identifier, entry);
            }

            _overlayEntryCore.OverlayEntryCoreCompletionSource.SetResult(true);
        }

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
                    StableIdentifier = null,
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
            var parts = identifier.Split('/');
            if (parts.Length >= 4)
            {
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

        /// <summary>
        /// Simulate sensor indices shifting within the same ID range for Intel CPU clock entries.
        /// </summary>
        private void PopulateOverlayEntryCoreWithShiftedIndicesWithinSameIds()
        {
            var sensorTypes = new HashSet<EOverlayEntryType>
            {
                EOverlayEntryType.GPU, EOverlayEntryType.CPU, EOverlayEntryType.RAM
            };

            var sensorConfigEntries = _loadedConfigEntries
                .Where(e => sensorTypes.Contains(e.OverlayEntryType))
                .ToList();

            var cpuClockEntries = sensorConfigEntries
                .Where(e => e.Identifier.Contains("/intelcpu/") && e.Identifier.Contains("/clock/"))
                .OrderBy(e => e.Identifier)
                .ToList();

            var otherEntries = sensorConfigEntries
                .Where(e => !(e.Identifier.Contains("/intelcpu/") && e.Identifier.Contains("/clock/")))
                .ToList();

            for (int i = 0; i < cpuClockEntries.Count; i++)
            {
                var configEntry = cpuClockEntries[i];
                var shiftedEntry = cpuClockEntries[(i + 1) % cpuClockEntries.Count];

                var entry = new OverlayEntryWrapper(configEntry.Identifier)
                {
                    StableIdentifier = null,
                    SortKey = shiftedEntry.SortKey,
                    Description = shiftedEntry.Description,
                    OverlayEntryType = configEntry.OverlayEntryType,
                    GroupName = shiftedEntry.Description,
                    ShowOnOverlay = false,
                    ShowOnOverlayIsEnabled = true,
                    ShowGraph = false,
                    ShowGraphIsEnabled = false,
                    Value = 0
                };

                _overlayEntryCore.OverlayEntryDict.TryAdd(configEntry.Identifier, entry);
            }

            foreach (var configEntry in otherEntries)
            {
                var entry = new OverlayEntryWrapper(configEntry.Identifier)
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

                _overlayEntryCore.OverlayEntryDict.TryAdd(configEntry.Identifier, entry);
            }

            _overlayEntryCore.OverlayEntryCoreCompletionSource.SetResult(true);
        }

        // ==================== TESTS ====================

        [TestMethod]
        public void LoadedConfig_NvMobileIntel_HasExpected233Entries()
        {
            Assert.AreEqual(233, _loadedConfigEntries.Count,
                "OverlayConfigMigrationNvMobileIntelTest.json should contain exactly 233 entries.");
        }

        [TestMethod]
        public void LoadedConfig_NvMobileIntel_ContainsAllOverlayEntryTypes()
        {
            var types = _loadedConfigEntries.Select(e => e.OverlayEntryType).Distinct().OrderBy(t => t).ToList();

            Assert.IsTrue(types.Contains(EOverlayEntryType.CX), "Should contain CX entries.");
            Assert.IsTrue(types.Contains(EOverlayEntryType.OnlineMetric), "Should contain OnlineMetric entries.");
            Assert.IsTrue(types.Contains(EOverlayEntryType.GPU), "Should contain GPU entries.");
            Assert.IsTrue(types.Contains(EOverlayEntryType.CPU), "Should contain CPU entries.");
            Assert.IsTrue(types.Contains(EOverlayEntryType.RAM), "Should contain RAM entries.");
        }

        [TestMethod]
        public void LoadedConfig_NvMobileIntel_ShowOnOverlayFlags_ArePreserved()
        {
            var shownEntries = _loadedConfigEntries.Where(e => e.ShowOnOverlay).ToList();
            var shownIds = shownEntries.Select(e => e.Identifier).ToHashSet();

            // GPU entries that are shown
            Assert.IsTrue(shownIds.Contains("/gpu-nvidia/0/clock/0"), "GPU Core Clock should be shown.");
            Assert.IsTrue(shownIds.Contains("/gpu-nvidia/0/load/0"), "GPU Core Load should be shown.");
            Assert.IsTrue(shownIds.Contains("/gpu-nvidia/0/temperature/0"), "GPU Core Temp should be shown.");
            Assert.IsTrue(shownIds.Contains("/gpu-nvidia/0/power/0"), "GPU Power should be shown.");

            // CPU entries that are shown
            Assert.IsTrue(shownIds.Contains("/intelcpu/0/clock/25"), "CPU Max Clock should be shown.");
            Assert.IsTrue(shownIds.Contains("/intelcpu/0/power/0"), "CPU Package Power should be shown.");

            // Framerate/Frametime
            Assert.IsTrue(shownIds.Contains("Framerate"), "Framerate should be shown.");
            Assert.IsTrue(shownIds.Contains("Frametime"), "Frametime should be shown.");
        }

        [TestMethod]
        public void LoadedConfig_NvMobileIntel_SortKeyOrder_IsCorrect()
        {
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
        public void LoadedConfig_NvMobileIntel_GroupNames_ArePreserved()
        {
            var entryById = _loadedConfigEntries.ToDictionary(e => e.Identifier);

            Assert.AreEqual("RTX 4060 Mobile", entryById["/gpu-nvidia/0/clock/0"].GroupName, "GPU Clock group name.");
            Assert.AreEqual("GPU Load", entryById["/gpu-nvidia/0/load/0"].GroupName, "GPU Load group name.");
            Assert.AreEqual("GPU Temp", entryById["/gpu-nvidia/0/temperature/0"].GroupName, "GPU Temp group name.");
            Assert.AreEqual("VRAM Hot Spot", entryById["/gpu-nvidia/0/temperature/3"].GroupName, "VRAM Hot Spot group name.");
            Assert.AreEqual("CPU Max", entryById["/intelcpu/0/clock/25"].GroupName, "CPU Max Clock group name.");
            Assert.AreEqual("CPU Model", entryById["CustomCPU"].GroupName, "CustomCPU group name.");
            Assert.AreEqual("RAM Info", entryById["CustomRAM"].GroupName, "CustomRAM group name.");
        }

        [TestMethod]
        public async Task IdenticalHardware_NvMobileIntel_AllEntriesLoaded_OrderPreserved()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);

            Assert.IsNotNull(entries, "Entries should not be null.");
            Assert.IsTrue(entries.Length > 0, "Should have loaded entries.");

            var entryIds = new HashSet<string>(entries.Select(e => e.Identifier));
            foreach (var configEntry in _loadedConfigEntries)
            {
                Assert.IsTrue(entryIds.Contains(configEntry.Identifier),
                    $"Entry '{configEntry.Identifier}' should be present.");
            }
        }

        [TestMethod]
        public async Task IdenticalHardware_NvMobileIntel_ShowOnOverlay_Preserved()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var entryById = entries.ToDictionary(e => e.Identifier);

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
        public async Task IdenticalHardware_NvMobileIntel_GroupNames_Preserved()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var entryById = entries.ToDictionary(e => e.Identifier);

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
        public async Task IdenticalHardware_NvMobileIntel_Colors_Preserved()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var entryById = entries.ToDictionary(e => e.Identifier);

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
        public async Task IdenticalHardware_NvMobileIntel_EntryOrder_MatchesConfig()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);

            var configIds = _loadedConfigEntries.Select(e => e.Identifier).ToList();
            var loadedIds = entries.Select(e => e.Identifier).ToList();

            var commonIds = configIds.Where(id => loadedIds.Contains(id)).ToList();
            var commonIdsInLoaded = loadedIds.Where(id => configIds.Contains(id)).ToList();

            CollectionAssert.AreEqual(commonIds, commonIdsInLoaded,
                "Config entries should maintain their relative order.");
        }

        [TestMethod]
        public async Task ShiftedIds_NvMobileIntel_StableIdMigration_AllSensorEntriesPreserved()
        {
            PopulateOverlayEntryCoreWithShiftedIds();

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

            Assert.IsTrue(sensorEntriesInResult.Count >= configSensorCount,
                $"Should have at least {configSensorCount} sensor entries after migration, got {sensorEntriesInResult.Count}.");
        }

        [TestMethod]
        public async Task ShiftedIds_NvMobileIntel_StableIdMigration_ShowOnOverlayPreserved()
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
        public async Task ShiftedIds_NvMobileIntel_StableIdMigration_GroupNamesPreserved()
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
        public async Task ShiftedIds_NvMobileIntel_DescriptionFallback_AllSensorEntriesPreserved()
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

            var configSensorCount = _loadedConfigEntries
                .Count(e => e.OverlayEntryType == EOverlayEntryType.GPU
                         || e.OverlayEntryType == EOverlayEntryType.CPU
                         || e.OverlayEntryType == EOverlayEntryType.RAM);

            var sensorEntriesInResult = entries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.GPU
                          || e.OverlayEntryType == EOverlayEntryType.CPU
                          || e.OverlayEntryType == EOverlayEntryType.RAM)
                .ToList();

            Assert.IsTrue(sensorEntriesInResult.Count >= configSensorCount,
                $"Should have at least {configSensorCount} sensor entries after Description fallback migration, got {sensorEntriesInResult.Count}.");
        }

        [TestMethod]
        public async Task ShiftedIds_NvMobileIntel_DescriptionFallback_ShowOnOverlayPreserved()
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

            var resultByDescType = entries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.GPU
                          || e.OverlayEntryType == EOverlayEntryType.CPU
                          || e.OverlayEntryType == EOverlayEntryType.RAM)
                .GroupBy(e => (e.Description, e.OverlayEntryType))
                .Where(g => g.Count() == 1)
                .ToDictionary(g => g.Key, g => g.Single());

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
        public void LoadedConfig_IntelCpuEntries_AreCorrectlyTyped()
        {
            var cpuEntries = _loadedConfigEntries
                .Where(e => e.OverlayEntryType == EOverlayEntryType.CPU)
                .ToList();

            Assert.IsTrue(cpuEntries.Count > 0, "Should contain CPU entries.");

            foreach (var entry in cpuEntries)
            {
                Assert.IsTrue(entry.Identifier.StartsWith("/intelcpu/"),
                    $"CPU entry '{entry.Identifier}' should start with '/intelcpu/'.");
            }
        }

        [TestMethod]
        public void LoadedConfig_NvMobileIntel_RamEntries_AreCorrectlyTyped()
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
        public void LoadedConfig_NvMobileIntel_OnlineMetricEntries_ArePresent()
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
        public void LoadedConfig_NvMobileIntel_ShownEntries_OrderedBySortKey()
        {
            var shownEntries = _loadedConfigEntries.Where(e => e.ShowOnOverlay).ToList();
            Assert.IsTrue(shownEntries.Count > 10, "Should have more than 10 shown entries.");

            // Verify GPU entries come before CPU entries
            var firstGpuShown = shownEntries.FirstOrDefault(e => e.OverlayEntryType == EOverlayEntryType.GPU);
            var firstCpuShown = shownEntries.FirstOrDefault(e => e.OverlayEntryType == EOverlayEntryType.CPU);

            if (firstGpuShown != null && firstCpuShown != null)
            {
                int gpuIndex = shownEntries.IndexOf(firstGpuShown);
                int cpuIndex = shownEntries.IndexOf(firstCpuShown);

                Assert.IsTrue(gpuIndex < cpuIndex,
                    "GPU entries should appear before CPU entries in overlay order.");
            }

            // Verify CPU entries come before RAM entries
            var firstRamShown = shownEntries.FirstOrDefault(e => e.OverlayEntryType == EOverlayEntryType.RAM);
            if (firstCpuShown != null && firstRamShown != null)
            {
                int cpuIndex = shownEntries.IndexOf(firstCpuShown);
                int ramIndex = shownEntries.IndexOf(firstRamShown);
                Assert.IsTrue(cpuIndex < ramIndex,
                    "CPU entries should appear before RAM entries in overlay order.");
            }
        }

        [TestMethod]
        public async Task ShiftedIndicesSameIds_NvMobileIntel_CpuCoreClockShowOnOverlay_Preserved()
        {
            PopulateOverlayEntryCoreWithShiftedIndicesWithinSameIds();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);

            // The Intel CPU Core Clock entries had ShowOnOverlay=true in config.
            // After index shift, exact ID match finds wrong sensor -> should fall through to
            // Description+Type fallback and preserve ShowOnOverlay=true.
            var cpuCoreClockDescriptions = new HashSet<string>
            {
                "Core #1 P (MHz)", "Core #2 P (MHz)", "Core #3 P (MHz)", "Core #4 P (MHz)",
                "Core #5 P (MHz)", "Core #6 P (MHz)", "Core #7 P (MHz)", "Core #8 P (MHz)",
                "Core #9 E (MHz)", "Core #10 E (MHz)", "Core #11 E (MHz)", "Core #12 E (MHz)",
                "Core #13 E (MHz)", "Core #14 E (MHz)", "Core #15 E (MHz)", "Core #16 E (MHz)",
                "Core #17 E (MHz)", "Core #18 E (MHz)", "Core #19 E (MHz)", "Core #20 E (MHz)",
                "Core #21 E (MHz)", "Core #22 E (MHz)", "Core #23 E (MHz)", "Core #24 E (MHz)",
                "CPU Max (MHz)"
            };

            var matchedEntries = entries
                .Where(e => cpuCoreClockDescriptions.Contains(e.Description))
                .ToList();

            Assert.AreEqual(cpuCoreClockDescriptions.Count, matchedEntries.Count,
                $"All {cpuCoreClockDescriptions.Count} CPU Core Clock entries should be present after migration.");

            foreach (var entry in matchedEntries)
            {
                Assert.IsTrue(entry.ShowOnOverlay,
                    $"CPU Core Clock '{entry.Description}' (ID: {entry.Identifier}) should have ShowOnOverlay=true after index shift migration.");
            }
        }

        [TestMethod]
        public async Task IdenticalHardware_NvMobileIntel_LimitValues_Preserved()
        {
            PopulateOverlayEntryCoreFromConfig();

            var provider = CreateProvider();
            await Task.Delay(500);

            var entries = await provider.GetOverlayEntries(updateFormats: false);
            var entryById = entries.ToDictionary(e => e.Identifier);

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
