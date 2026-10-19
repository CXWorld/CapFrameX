using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.OSD.Integration;
using CapFrameX.PresentMonInterface;
using CapFrameX.Overlay;
using CapFrameX.Sensor;
using CapFrameX.Statistics.NetStandard.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayTelemetryServiceTest
    {
        [TestMethod]
        public async Task MemoryTotalUsesMatchingLiveUsedAndAvailableReadings()
        {
            var used = Sensor("/ram/data/0", "RAM Used", "Memory", "Data", "RAM");
            var free = Sensor("/ram/data/1", "RAM Available", "Memory", "Data", "RAM");
            using var fixture = new Fixture(new[] { used, free });
            var sources = await fixture.Service.GetSourcesAsync();
            var total = sources.Single(source => source.SemanticKey == "ramTotal");
            Assert.AreEqual("GB", total.Unit);
            Assert.AreEqual("Capacity & usage", total.Subcategory);
            var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float> { [used] = 26.25f, [free] = 5.75f });
            Assert.AreEqual(32d, snapshot.Values[total.Id]);
            var missing = await fixture.Next(new Dictionary<ISensorEntry, float> { [used] = 26.25f });
            Assert.IsNull(missing.Values[total.Id], "Never reuse an old total when one of its readings is absent.");
            var invalid = await fixture.Next(new Dictionary<ISensorEntry, float> { [used] = 26.25f, [free] = -1 });
            Assert.IsNull(invalid.Values[total.Id]);
        }

        [TestMethod]
        public async Task MemoryTotalsNeverCombineDifferentDevices()
        {
            using var fixture = new Fixture(new[]
            {
                Sensor("/ram/0/data/0", "RAM Used", "Memory", "Data", "RAM A"),
                Sensor("/ram/1/data/1", "RAM Available", "Memory", "Data", "RAM B")
            });
            Assert.IsFalse((await fixture.Service.GetSourcesAsync()).Any(source => source.SemanticKey == "ramTotal"));
        }

        [TestMethod]
        public async Task FanPercentAndRpmAreIndependentRoles()
        {
            using var fixture = new Fixture(new[]
            {
                Sensor("/gpu/0/fan/0", "GPU Fan", "GpuNvidia", "Fan", "GPU"),
                Sensor("/gpu/0/control/0", "GPU Fan", "GpuNvidia", "Control", "GPU"),
                Sensor("/gpu/0/data/2", "GPU Memory Total", "GpuNvidia", "Data", "GPU")
            });
            var sources = await fixture.Service.GetSourcesAsync();
            Assert.AreEqual("RPM", sources.Single(source => source.SemanticKey == "gpuFan").Unit);
            Assert.AreEqual("%", sources.Single(source => source.SemanticKey == "gpuFanPercent").Unit);
            Assert.AreEqual("GB", sources.Single(source => source.SemanticKey == "vramTotal").Unit);
        }

        [TestMethod]
        public async Task CatalogIncludesUnselectedAndUnusualSensorsWithUnits()
        {
            var entries = new[]
            {
                Sensor("/gpu/0/load/0", "GPU Core", "GpuNvidia", "Load", "GPU"),
                Sensor("/network/0/throughput/0", "Download Speed", "Network", "Throughput", "NIC"),
                Sensor("/storage/0/throughput/0", "Drive Read Rate", "Storage", "Throughput", "SSD"),
                Sensor("/board/humidity/0", "Humidity", "SuperIO", "Humidity", "Board"),
                Sensor("pmcreader/cpu/bandwidth", "DRAM bandwidth", "Cpu", "Throughput", "CPU")
            };
            using var fixture = new Fixture(entries);
            var sources = await fixture.Service.GetSourcesAsync();
            Assert.AreEqual(entries.Length, sources.Count);
            Assert.AreEqual("B/s", sources.Single(s => s.HardwareType == "Network").Unit);
            Assert.AreEqual("GB/s", sources.Single(s => s.HardwareType == "Storage").Unit);
            Assert.AreEqual("%", sources.Single(s => s.SensorType == "Humidity").Unit);
            Assert.AreEqual("gpuLoad", sources.Single(s => s.HardwareType == "GpuNvidia").SemanticKey);
            fixture.Config.Verify(c => c.SelectForOverlay(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
            fixture.Config.Verify(c => c.AcquireAllSensors(), Times.Never);
        }

        [TestMethod]
        public async Task CatalogCoversEveryHardwareSensorUnitIncludingLessCommonTypes()
        {
            var entries = Enum.GetNames<LibreHardwareMonitor.Hardware.SensorType>()
                .Select(type => Sensor("/board/" + type, type, "Motherboard", type, "Board")).ToArray();
            using var fixture = new Fixture(entries);
            var sources = await fixture.Service.GetSourcesAsync();
            foreach (var source in sources.Where(s => s.SensorType != "Factor"))
                Assert.IsFalse(string.IsNullOrWhiteSpace(source.Unit), "Missing unit for " + source.SensorType);
            Assert.AreEqual("mWh", sources.Single(s => s.SensorType == "Energy").Unit);
            Assert.AreEqual("s", sources.Single(s => s.SensorType == "TimeSpan").Unit);
            Assert.AreEqual("µS/cm", sources.Single(s => s.SensorType == "Conductivity").Unit);
            Assert.AreEqual("MT/s", sources.Single(s => s.SensorType == "DataRate").Unit);
            Assert.AreEqual("dBA", sources.Single(s => s.SensorType == "Noise").Unit);
        }

        [TestMethod]
        public async Task DuplicateStableNamesRemainDistinctAndDoNotPickAPresetDevice()
        {
            var first = Sensor("/gpu/0/load/0", "GPU Core", "GpuNvidia", "Load", "Identical GPU");
            var second = Sensor("/gpu/1/load/0", "GPU Core", "GpuNvidia", "Load", "Identical GPU");
            using var fixture = new Fixture(new[] { first, second });
            var sources = await fixture.Service.GetSourcesAsync();
            Assert.AreEqual(2, sources.Select(s => s.Id).Distinct().Count());
            Assert.IsTrue(sources.All(s => s.IsStableIdentifierAmbiguous));
            Assert.IsTrue(sources.All(s => s.SemanticKey == null));

            var snapshot = fixture.Next(new Dictionary<ISensorEntry, float> { [first] = 12, [second] = 81 });
            var values = (await snapshot).Values;
            Assert.AreEqual(12d, values[sources.Single(s => s.Identifier == first.Identifier).Id]);
            Assert.AreEqual(81d, values[sources.Single(s => s.Identifier == second.Identifier).Id]);
        }

        [TestMethod]
        public async Task PresetDoesNotChooseBetweenDifferentDetectedGpus()
        {
            using var fixture = new Fixture(new[]
            {
                Sensor("/gpu-nvidia/0/power/0", "GPU Power", "GpuNvidia", "Power", "NVIDIA GPU"),
                Sensor("/gpu-amd/0/power/0", "GPU TBP", "GpuAmd", "Power", "AMD GPU")
            });
            var sources = await fixture.Service.GetSourcesAsync();
            Assert.IsTrue(sources.All(s => s.SemanticKey == null),
                "A preset must leave multi-GPU device choice to the user.");
        }

        [TestMethod]
        public async Task StableKeySurvivesRuntimeIndexChangesAndMissingSamplesStayUnavailable()
        {
            var sensor = Sensor("/cpu/0/temp/1", "CPU Package", "Cpu", "Temperature", "CPU");
            using var fixture = new Fixture(new[] { sensor });
            var oldSource = (await fixture.Service.GetSourcesAsync()).Single();
            sensor.Identifier = "/cpu/0/temp/42";
            var newSource = (await fixture.Service.GetSourcesAsync()).Single();
            Assert.AreEqual(oldSource.Id, newSource.Id);
            Assert.AreEqual(oldSource.DeviceKey, newSource.DeviceKey);
            var measured = await fixture.Next(new Dictionary<ISensorEntry, float> { [sensor] = 62 });
            Assert.AreEqual(62d, measured.Values[newSource.Id]);
            var missing = await fixture.Next(new Dictionary<ISensorEntry, float>());
            Assert.IsNull(missing.Values[newSource.Id]);
            var invalid = await fixture.Next(new Dictionary<ISensorEntry, float> { [sensor] = float.NaN });
            Assert.IsNull(invalid.Values[newSource.Id]);
            Assert.AreEqual(62d, measured.Values[newSource.Id], "Published snapshots must not mutate.");
        }

        [TestMethod]
        public async Task FreshMetricSamplesAndNonconsumingPmdReadAreIndependentOfClassicRows()
        {
            var metrics = new Mock<IOnlineMetricService>();
            metrics.SetupGet(m => m.LastFrameTimestampUtc).Returns(() => DateTime.UtcNow);
            metrics.Setup(m => m.GetOnlineFpsMetricValue(EMetric.Average)).Returns(144);
            metrics.Setup(m => m.GetPmdTelemetrySnapshot()).Returns(() => new OnlinePmdMetrics
            {
                TimestampUtc = DateTime.UtcNow, GpuPowerCurrent = 250, CpuPowerCurrent = 75, SystemPowerCurrent = 350
            });
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), metrics.Object);
            var sources = await fixture.Service.GetSourcesAsync();
            Assert.AreEqual("fps", sources.Single(s => s.Id == "metric/OnlineAverage").SemanticKey);
            Assert.IsTrue(sources.All(s => !string.IsNullOrWhiteSpace(s.SemanticKey)),
                "Every performance and PMD source must be available to template role binding.");
            Assert.AreEqual(sources.Count, sources.Select(s => s.SemanticKey).Distinct().Count(),
                "Template roles must not bind ambiguously to distinct performance metrics.");
            Assert.AreEqual("fpsP1", sources.Single(s => s.Id == "metric/OnlineP1").SemanticKey);
            Assert.AreEqual("fps1pctLow", sources.Single(s => s.Id == "metric/Online1PercentLow").SemanticKey,
                "Percentile and low-average templates must select distinct readings.");
            Assert.AreEqual("pmdSystemPower", sources.Single(s => s.Id == "metric/PmdSystemPowerCurrent").SemanticKey);
            var current = await fixture.Next(new Dictionary<ISensorEntry, float>());
            Assert.AreEqual(144d, current.Values["metric/OnlineAverage"]);
            Assert.AreEqual(250d, current.Values["metric/PmdGpuPowerCurrent"]);
            metrics.Verify(m => m.GetPmdMetricsPowerCurrent(), Times.Never);

            metrics.SetupGet(m => m.LastFrameTimestampUtc).Returns(DateTime.UtcNow.AddMinutes(-1));
            metrics.Setup(m => m.GetPmdTelemetrySnapshot()).Returns((OnlinePmdMetrics)null);
            var absent = await fixture.Next(new Dictionary<ISensorEntry, float>());
            Assert.IsNull(absent.Values["metric/OnlineAverage"]);
            Assert.IsNull(absent.Values["metric/PmdGpuPowerCurrent"]);
        }

        [TestMethod]
        public void IndependentPreviewLeasesAreReleasedExactlyOnceIncludingOnServiceDisposal()
        {
            using var fixture = new Fixture(Array.Empty<ISensorEntry>());
            int active = 0;
            fixture.Config.Setup(c => c.AcquireAllSensors()).Returns(() =>
            {
                active++;
                return Disposable.Create(() => active--);
            });
            var first = fixture.Service.AcquirePreview();
            var second = fixture.Service.AcquirePreview();
            Assert.AreEqual(2, active);
            first.Dispose();
            first.Dispose();
            Assert.AreEqual(1, active);
            fixture.Service.Dispose();
            Assert.AreEqual(0, active);
            second.Dispose();
            Assert.AreEqual(0, active);
            Assert.ThrowsExactly<ObjectDisposedException>(() => fixture.Service.AcquirePreview());
        }

        [TestMethod]
        public async Task CatalogHasEveryClassicEntryIncludingDisabledFeaturesAndDynamicDisplays()
        {
            var defaults = OverlayUtils.GetOverlayEntryDefaults(Mock.Of<IAppConfiguration>()).Cast<IOverlayEntry>().ToList();
            defaults.Add(OverlayEntryProvider.CreateDisplayResolutionEntry(new DetectedDisplay("DISPLAY2", 2560, 1440, false)));
            var classic = new Mock<IOverlayEntryProvider>();
            classic.Setup(p => p.GetTelemetrySourcesAsync()).ReturnsAsync(defaults.ToArray());
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), Mock.Of<IOnlineMetricService>(), classic.Object);
            var sources = await fixture.Service.GetSourcesAsync();
            CollectionAssert.AreEquivalent(defaults.Select(d => d.Identifier)
                    .Concat(new[] { "CustomGPUShort", "CustomCPUShort", "ProcessName", "GraphicsAPI" }).ToArray(),
                sources.Select(s => s.Identifier).ToArray());
            Assert.IsTrue(sources.All(s => !string.IsNullOrWhiteSpace(s.Category) && !string.IsNullOrWhiteSpace(s.Subcategory)));
            Assert.IsTrue(sources.All(s => s.IsAvailable), "Sources stay selectable while classic features/renderers are disabled.");
            Assert.IsTrue(sources.Single(s => s.Identifier == "FrameGenerationStatus").IsText);
            Assert.IsTrue(sources.Single(s => s.Identifier == "DisplayResolution:DISPLAY2").IsText);
            Assert.AreEqual("Percentiles", sources.Single(s => s.Identifier == "OnlineP1").Subcategory);
            Assert.AreEqual("Low averages", sources.Single(s => s.Identifier == "Online1PercentLow").Subcategory);
            classic.Verify(p => p.GetOverlayEntries(It.IsAny<bool>()), Times.Never);
            classic.Verify(p => p.GetDefaultOverlayEntries(), Times.Never);
        }

        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task CatalogDistinguishesGameMetricsFromIndependentHardwareAndSystemSources(bool includeOnlineMetrics)
        {
            var sensors = new[]
            {
                Sensor("/cpu/load/0", "CPU Total", "Cpu", "Load", "CPU"),
                Sensor("/gpu/load/0", "GPU Core", "GpuNvidia", "Load", "GPU"),
                Sensor("/ram/data/0", "RAM Used", "Memory", "Data", "RAM"),
                Sensor("/ram/data/1", "RAM Available", "Memory", "Data", "RAM")
            };
            var classic = new Mock<IOverlayEntryProvider>();
            classic.Setup(provider => provider.GetTelemetrySourcesAsync()).ReturnsAsync(
                OverlayUtils.GetOverlayEntryDefaults(Mock.Of<IAppConfiguration>()).Cast<IOverlayEntry>()
                    .Append(OverlayEntryProvider.CreateDisplayResolutionEntry(new DetectedDisplay("DISPLAY2", 2560, 1440, false)))
                    .ToArray());
            using var fixture = new Fixture(sensors,
                includeOnlineMetrics ? Mock.Of<IOnlineMetricService>() : null, classic.Object, Mock.Of<IOverlayService>());
            var sources = await fixture.Service.GetSourcesAsync();
            var processSpecific = new List<string>
            {
                "Framerate", "Frametime", "DisplayTime", "OnlineAverage", "OnlineP1", "OnlineP0dot1", "OnlineP0dot2",
                "Online1PercentLow", "Online0dot1PercentLow", "Online0dot2PercentLow", "OnlineFrameTimeAverage",
                "OnlineGpuActiveTimeAverage", "OnlineCpuActiveTimeAverage", "OnlineGpuActiveTimePercentageDeviation",
                "OnlineStutteringPercentage", "OnlinePcLatency", "OnlineAnimationError", "HookOverlayStatus",
                "FrameGenerationTechnology", "FrameGenerationStatus", "Resolution"
            };
            if (includeOnlineMetrics) processSpecific.AddRange(new[] { "ProcessName", "GraphicsAPI" });
            CollectionAssert.AreEquivalent(processSpecific.ToArray(),
                sources.Where(source => source.IsProcessSpecific).Select(source => source.Identifier).ToArray(),
                "Only readings belonging to a game may be invalidated when the target changes.");

            foreach (var sensor in sensors)
                Assert.IsFalse(sources.Single(source => source.Identifier == sensor.Identifier).IsProcessSpecific,
                    "Hardware polling must remain independent of the selected game: " + sensor.Identifier);
            Assert.IsFalse(sources.Single(source => source.SemanticKey == "ramTotal").IsProcessSpecific,
                "A total derived from machine-wide hardware readings has the same lifetime as its inputs.");
            foreach (string identifier in new[]
            {
                "PmdGpuPowerCurrent", "PmdCpuPowerCurrent", "PmdSystemPowerCurrent", "BatteryLifePercent",
                "BatteryLifeRemaining", "CustomCPU", "CustomGPU", "CustomRAM", "Mainboard", "OS", "GPUDriver",
                "DisplayResolution:DISPLAY2", "SystemTime", "CxAppCpuUsage", "Ping", "RunHistoryCount", "RunHistoryAggregation"
            })
                Assert.IsFalse(sources.Single(source => source.Identifier == identifier).IsProcessSpecific,
                    "Fresh machine-wide readings must survive a temporary loss of game detection: " + identifier);
        }

        [TestMethod]
        public async Task ClassicTextAndNumericValuesStayTypedAndRunHistoryIsCopiedWithoutProfileMutation()
        {
            var classic = new Mock<IOverlayEntryProvider>();
            classic.Setup(p => p.GetTelemetrySourcesAsync()).ReturnsAsync(
                OverlayUtils.GetOverlayEntryDefaults(Mock.Of<IAppConfiguration>()).Cast<IOverlayEntry>().ToArray());
            var readings = new Dictionary<string, object>
            {
                ["CustomGPU"] = "GPU 9000", ["CaptureTimer"] = "12", ["CaptureServiceStatus"] = "Recording...",
                ["BatteryLifePercent"] = double.NaN, ["Resolution"] = "N/A", ["Ping"] = 4d
            };
            classic.Setup(p => p.GetTelemetryValues()).Returns(readings);
            var overlay = new Mock<IOverlayService>();
            overlay.SetupGet(o => o.RunHistory).Returns(new[] { "100 FPS", "N/A", "120 FPS" });
            overlay.SetupGet(o => o.RunHistoryAggregation).Returns("110 FPS average");
            overlay.SetupGet(o => o.RunHistoryCount).Returns(2);
            overlay.SetupGet(o => o.RunHistoryOutlierFlags).Returns(new[] { false, false, true });
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), Mock.Of<IOnlineMetricService>(), classic.Object, overlay.Object);
            await fixture.Service.GetSourcesAsync();
            var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float>());
            Assert.AreEqual("GPU 9000", snapshot.MetricValues["metric/CustomGPU"]);
            Assert.AreEqual(12d, snapshot.Values["metric/CaptureTimer"]);
            Assert.AreEqual("Recording...", snapshot.MetricValues["metric/CaptureServiceStatus"]);
            Assert.IsNull(snapshot.MetricValues["metric/BatteryLifePercent"]);
            Assert.IsNull(snapshot.MetricValues["metric/Resolution"]);
            Assert.AreEqual(4d, snapshot.MetricValues["metric/Ping"]);
            Assert.AreEqual("100 FPS\n120 FPS [outlier]\n110 FPS average", snapshot.MetricValues["metric/RunHistory"]);
            Assert.AreEqual("110 FPS average", snapshot.MetricValues["metric/RunHistoryAggregation"]);
            Assert.AreEqual(2d, snapshot.Values["metric/RunHistoryCount"]);
            Assert.AreEqual(1d, snapshot.Values["metric/RunHistoryOutlierCount"]);
            readings["CustomGPU"] = "Changed";
            Assert.AreEqual("GPU 9000", snapshot.MetricValues["metric/CustomGPU"]);
            classic.Verify(p => p.GetOverlayEntries(It.IsAny<bool>()), Times.Never);
            classic.Verify(p => p.MarkPendingChanges(), Times.Never);
        }

        [TestMethod]
        public async Task CurrentFrameSourcesAreDistinctFromConfiguredWindowAveragesAndExpire()
        {
            var metrics = new Mock<IOnlineMetricService>();
            metrics.SetupGet(m => m.LastFrameTimestampUtc).Returns(() => DateTime.UtcNow);
            metrics.Setup(m => m.GetOnlineFpsMetricValue(EMetric.Average)).Returns(90);
            metrics.Setup(m => m.GetFrameTelemetrySnapshot()).Returns(() => new OnlineFrameTelemetrySnapshot
            {
                TimestampUtc = DateTime.UtcNow, Framerate = 100, Frametime = 10, DisplayTime = 5,
                ProcessName = "Test Game", Runtime = "DXGI"
            });
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), metrics.Object);
            var sources = await fixture.Service.GetSourcesAsync();
            Assert.AreEqual("fpsCurrent", sources.Single(s => s.Identifier == "Framerate").SemanticKey);
            var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float>());
            Assert.AreEqual(100d, snapshot.Values["metric/Framerate"]);
            Assert.AreEqual(90d, snapshot.Values["metric/OnlineAverage"]);
            Assert.AreEqual(5d, snapshot.Values["metric/DisplayTime"]);
            Assert.AreEqual("Test Game", snapshot.MetricValues["metric/ProcessName"]);
            Assert.AreEqual("DXGI", snapshot.MetricValues["metric/GraphicsAPI"]);
            metrics.Setup(m => m.GetFrameTelemetrySnapshot()).Returns(new OnlineFrameTelemetrySnapshot
            {
                TimestampUtc = DateTime.UtcNow.AddSeconds(-3), Framerate = 100
            });
            var expired = await fixture.Next(new Dictionary<ISensorEntry, float>());
            Assert.IsNull(expired.Values["metric/Framerate"]);
            Assert.IsNull(expired.MetricValues["metric/ProcessName"]);
            Assert.IsNull(expired.MetricValues["metric/GraphicsAPI"]);
        }

        [TestMethod]
        public async Task HardwareSourcesHaveDeviceAndSensorGroupsAndConservativeClockPowerRoles()
        {
            using var fixture = new Fixture(new[]
            {
                Sensor("/cpu/clock", "CPU Clock", "Cpu", "Clock", "CPU"),
                Sensor("/cpu/power", "CPU Package", "Cpu", "Power", "CPU"),
                Sensor("/gpu/clock", "GPU Core", "GpuNvidia", "Clock", "GPU"),
                Sensor("/gpu/memclock", "GPU Memory", "GpuNvidia", "Clock", "GPU"),
                Sensor("/gpu/fan", "GPU Fan", "GpuNvidia", "Fan", "GPU"),
                Sensor("/ram/load", "Memory", "Memory", "Load", "RAM")
            });
            var sources = await fixture.Service.GetSourcesAsync();
            CollectionAssert.AreEquivalent(new[] { "cpuClock", "cpuPower", "gpuClock", "gpuMemoryClock", "gpuFan", "ramLoad" },
                sources.Select(s => s.SemanticKey).ToArray());
            Assert.AreEqual("Processor", sources.Single(s => s.SemanticKey == "cpuClock").Category);
            Assert.AreEqual("Clocks & speed", sources.Single(s => s.SemanticKey == "cpuClock").Subcategory);
            Assert.AreEqual("Cooling", sources.Single(s => s.SemanticKey == "gpuFan").Subcategory);
            Assert.IsTrue(sources.All(s => !s.IsText && s.IsAvailable));
        }

        [TestMethod]
        public async Task ThirtyTwoThreadLoadsFormOneNaturallyOrderedGroupWithoutAggregateReadings()
        {
            var entries = Enumerable.Range(1, 16).SelectMany(core => Enumerable.Range(1, 2)
                .Select(thread => Sensor($"/amdcpu/0/load/{core * 2 + thread}",
                    $"Core #{core} Thread #{thread}", "Cpu", "Load", "32-thread CPU")))
                .Reverse().Concat(new[]
                {
                    Sensor("/amdcpu/0/load/0", "CPU Total", "Cpu", "Load", "32-thread CPU"),
                    Sensor("/amdcpu/0/load/1", "CPU Max", "Cpu", "Load", "32-thread CPU")
                }).ToArray();
            using var fixture = new Fixture(entries);
            var sources = await fixture.Service.GetSourcesAsync();
            var members = sources.Where(s => s.GroupKey != null).OrderBy(s => s.GroupOrder).ToArray();
            Assert.AreEqual(34, sources.Count, "Grouping must not remove individual readings.");
            Assert.AreEqual(32, members.Length);
            Assert.AreEqual(1, members.Select(s => s.GroupKey).Distinct().Count());
            Assert.AreEqual(1, sources.Select(s => s.DeviceKey).Distinct().Count());
            CollectionAssert.AreEqual(Enumerable.Range(1, 16).SelectMany(core => Enumerable.Range(1, 2)
                .Select(thread => $"Core #{core} Thread #{thread}")).ToArray(), members.Select(s => s.Name).ToArray());
            Assert.IsTrue(sources.Where(s => s.Name.StartsWith("CPU ")).All(s => s.GroupKey == null));
        }

        [TestMethod]
        public async Task HybridCoreFamiliesSeparateClocksEffectiveClocksAndThermalHeadroom()
        {
            var entries = new List<SensorEntry>();
            string[] names = { "Core #1 P", "Core #2 P", "Core #3 E", "Core #10 LPE" };
            for (int index = 0; index < names.Length; index++)
            {
                entries.Add(Sensor($"/intelcpu/0/clock/{index}", names[index], "Cpu", "Clock", "Hybrid CPU"));
                entries.Add(Sensor($"/intelcpu/0/clock/{index + 10}", names[index] + " Thread #1 (Effective)", "Cpu", "Clock", "Hybrid CPU"));
                entries.Add(Sensor($"/intelcpu/0/temperature/{index}", names[index], "Cpu", "Temperature", "Hybrid CPU"));
                entries.Add(Sensor($"/intelcpu/0/temperature/{index + 10}", names[index] + " Distance to TjMax", "Cpu", "Temperature", "Hybrid CPU"));
                entries.Add(Sensor($"/intelcpu/0/load/{index}", names[index] + (index < 2 ? " Thread #1" : ""), "Cpu", "Load", "Hybrid CPU"));
            }
            entries.Add(Sensor("/intelcpu/0/clock/90", "CPU Effective", "Cpu", "Clock", "Hybrid CPU"));
            entries.Add(Sensor("/intelcpu/0/clock/91", "CPU Max Effective", "Cpu", "Clock", "Hybrid CPU"));
            entries.Add(Sensor("/intelcpu/0/temperature/90", "Core Average", "Cpu", "Temperature", "Hybrid CPU"));
            using var fixture = new Fixture(entries);
            var sources = await fixture.Service.GetSourcesAsync();
            var groups = sources.Where(s => s.GroupKey != null).GroupBy(s => s.GroupKey).ToArray();
            Assert.AreEqual(5, groups.Length);
            Assert.IsTrue(groups.All(g => g.Count() == 4));
            Assert.IsTrue(groups.All(g => g.Select(s => s.Unit).Distinct().Count() == 1));
            CollectionAssert.AreEquivalent(new[] { "CPU core clocks", "CPU effective clocks", "CPU core temperatures",
                    "CPU thermal headroom", "CPU core / thread loads" },
                groups.Select(g => g.First().GroupName).ToArray());
            Assert.AreEqual(3, sources.Count(s => s.GroupKey == null));
            foreach (var group in groups)
                Assert.AreEqual("Core #10 LPE", group.OrderBy(s => s.GroupOrder).Last().Name.Substring(0, 12));
        }

        [TestMethod]
        public async Task IdenticallyNamedCpuDevicesAndUnknownProvidersNeverMergeTheirCoreGroups()
        {
            var entries = Enumerable.Range(0, 2).SelectMany(device => Enumerable.Range(1, 2)
                .Select(core => Sensor($"/amdcpu/{device}/load/{core}", $"Core #{core}", "Cpu", "Load", "Identical CPU")))
                .Concat(new[]
                {
                    Sensor("plugin:deviceA:load1", "Core #1", "Cpu", "Load", "Plugin CPU"),
                    Sensor("plugin:deviceB:load2", "Core #2", "Cpu", "Load", "Plugin CPU"),
                    Sensor("/board/0/load/0", "Core #1", "SuperIO", "Load", "Board"),
                    Sensor("/board/0/load/1", "Core #2", "SuperIO", "Load", "Board")
                }).ToArray();
            using var fixture = new Fixture(entries);
            var sources = await fixture.Service.GetSourcesAsync();
            var native = sources.Where(s => s.HardwareName == "Identical CPU").ToArray();
            Assert.AreEqual(2, native.Select(s => s.GroupKey).Distinct().Count());
            Assert.IsTrue(native.GroupBy(s => s.GroupKey).All(g => g.Count() == 2));
            Assert.IsTrue(native.All(s => s.IsStableIdentifierAmbiguous));
            Assert.AreEqual(2, sources.Where(s => s.HardwareName == "Plugin CPU").Select(s => s.DeviceKey).Distinct().Count());
            Assert.IsTrue(sources.Where(s => s.HardwareName == "Board").All(s => s.GroupKey == null));
            Assert.AreEqual(entries.Length, sources.Count);
        }

        [TestMethod]
        public async Task CpuPowerVoltageAndUnrecognizedSuffixesDoNotMixIntoClockGroups()
        {
            using var fixture = new Fixture(new[]
            {
                Sensor("/amdcpu/0/power/1", "Core #1 P (SMU)", "Cpu", "Power", "CPU"),
                Sensor("/amdcpu/0/power/2", "Core #2 D (SMU)", "Cpu", "Power", "CPU"),
                Sensor("/amdcpu/0/voltage/1", "Core #1 P VID", "Cpu", "Voltage", "CPU"),
                Sensor("/amdcpu/0/voltage/2", "Core #2 D VID", "Cpu", "Voltage", "CPU"),
                Sensor("/amdcpu/0/clock/1", "Core #1 LP (Effective)", "Cpu", "Clock", "CPU"),
                Sensor("/amdcpu/0/clock/2", "Core #2 LP (Effective)", "Cpu", "Clock", "CPU"),
                Sensor("/amdcpu/0/clock/3", "Core #1 Maximum", "Cpu", "Clock", "CPU"),
                Sensor("/amdcpu/0/load/3", "Core #1 (Effective)", "Cpu", "Load", "CPU")
            });
            var sources = await fixture.Service.GetSourcesAsync();
            Assert.AreEqual(3, sources.Where(s => s.GroupKey != null).Select(s => s.GroupKey).Distinct().Count());
            Assert.IsTrue(sources.Where(s => s.Name.EndsWith("Maximum") || s.SensorType == "Load").All(s => s.GroupKey == null));
            Assert.AreEqual("CPU core power", sources.First(s => s.SensorType == "Power").GroupName);
            Assert.AreEqual("CPU core VID", sources.First(s => s.SensorType == "Voltage").GroupName);
        }

        [TestMethod]
        [DataRow("Core #1 P", "Clock", "P", 1, null)]
        [DataRow("Core #2 P Thread #2", "Load", "P", 2, 2)]
        [DataRow("Core #9 E (Effective)", "Clock", "E", 9, null)]
        [DataRow("Core #10 E Thread #1 (Effective)", "Clock", "E", 10, 1)]
        [DataRow("Core #25 LPE", "Temperature", "LPE", 25, null)]
        [DataRow("Core #26 LPE Distance to TjMax", "Temperature", "LPE", 26, null)]
        [DataRow("Core #7 D (SMU)", "Power", "D", 7, null)]
        [DataRow("Core #8 LP VID", "Voltage", "LP", 8, null)]
        [DataRow("CPU Core #12 P", "Clock", "P", 12, null)]
        [DataRow("Core #4", "Clock", null, 4, null)]
        [DataRow("Core #5 Thread #2 (Effective)", "Clock", null, 5, 2)]
        public async Task NativeCpuNamesExposeCoreIdentityWithoutInferringMissingTopology(
            string name, string sensorType, string coreType, int coreIndex, int? threadIndex)
        {
            using var fixture = new Fixture(new[]
            {
                Sensor("/cpu/0/" + sensorType.ToLowerInvariant() + "/1", name, "Cpu", sensorType, "CPU")
            });
            var source = (await fixture.Service.GetSourcesAsync()).Single();
            Assert.IsNotNull(source.GroupKey);
            Assert.AreEqual(coreType, source.CoreType);
            Assert.AreEqual((int?)coreIndex, source.CoreIndex);
            Assert.AreEqual(threadIndex, source.ThreadIndex);
            Assert.AreEqual(coreIndex * 1024 + threadIndex.GetValueOrDefault(), source.GroupOrder);
        }

        [TestMethod]
        [DataRow("CPU Total", "Cpu", "Load")]
        [DataRow("CPU Effective", "Cpu", "Clock")]
        [DataRow("Core Average", "Cpu", "Temperature")]
        [DataRow("Core #1 P Maximum", "Cpu", "Clock")]
        [DataRow("Core #1 P (Effective)", "Cpu", "Load")]
        [DataRow("Core #1 P VID", "Cpu", "Clock")]
        [DataRow("Core #1 P", "GpuNvidia", "Clock")]
        [DataRow("Core #1 P", "SuperIO", "Load")]
        [DataRow("Core #1 SMT", "Cpu", "Clock")]
        [DataRow("Core #1 p", "Cpu", "Clock")]
        public async Task UnrecognizedOrAggregateSensorsDoNotAcquireCoreTopology(
            string name, string hardwareType, string sensorType)
        {
            using var fixture = new Fixture(new[]
            {
                Sensor("/device/0/" + sensorType.ToLowerInvariant() + "/1", name, hardwareType, sensorType, "Hybrid CPU")
            });
            var source = (await fixture.Service.GetSourcesAsync()).Single();
            Assert.IsNull(source.GroupKey);
            Assert.IsNull(source.CoreType);
            Assert.IsNull(source.CoreIndex);
            Assert.IsNull(source.ThreadIndex);
        }

        [TestMethod]
        public async Task EightPerformanceAndSixteenEfficiencyCoresKeepPhysicalAndThreadIdentitiesDistinct()
        {
            var entries = Enumerable.Range(1, 24).SelectMany(core =>
            {
                string type = core <= 8 ? "P" : "E";
                var clock = Sensor($"/intelcpu/0/clock/{core}", $"Core #{core} {type}", "Cpu", "Clock", "Hybrid CPU");
                var threads = Enumerable.Range(1, core <= 8 ? 2 : 1).Select(thread =>
                    Sensor($"/intelcpu/0/load/{core * 2 + thread}",
                        $"Core #{core} {type}" + (core <= 8 ? $" Thread #{thread}" : ""), "Cpu", "Load", "Hybrid CPU"));
                return new[] { clock }.Concat(threads);
            });
            using var fixture = new Fixture(entries.Reverse());
            var sources = await fixture.Service.GetSourcesAsync();
            var clocks = sources.Where(source => source.SensorType == "Clock").OrderBy(source => source.GroupOrder).ToArray();
            var loads = sources.Where(source => source.SensorType == "Load").OrderBy(source => source.GroupOrder).ToArray();
            Assert.AreEqual(24, clocks.Length);
            Assert.AreEqual(32, loads.Length);
            Assert.AreEqual(8, clocks.Count(source => source.CoreType == "P"));
            Assert.AreEqual(16, clocks.Count(source => source.CoreType == "E"));
            Assert.IsTrue(clocks.All(source => source.ThreadIndex == null));
            Assert.AreEqual(16, loads.Count(source => source.CoreType == "P"));
            Assert.AreEqual(16, loads.Count(source => source.CoreType == "E"));
            Assert.AreEqual(8, loads.Where(source => source.CoreType == "P").Select(source => source.CoreIndex).Distinct().Count());
            CollectionAssert.AreEqual(Enumerable.Range(1, 24).ToArray(), clocks.Select(source => source.CoreIndex.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, loads.Where(source => source.CoreIndex == 1).Select(source => source.ThreadIndex.Value).ToArray());
            Assert.IsTrue(loads.Where(source => source.CoreType == "E").All(source => source.ThreadIndex == null));
        }

        [TestMethod]
        public void LongAsciiAndUnicodeLabelsCannotInvalidateTheNativeMetricSnapshot()
        {
            var snapshot = new OverlayTelemetrySnapshot(DateTime.UtcNow,
                new Dictionary<string, double?> { ["fps"] = 144 }, new Dictionary<string, object>
                {
                    ["cpu"] = new string('x', 9000),
                    ["history"] = string.Concat(Enumerable.Repeat("🚀 GPU 温度\n", 900))
                });
            Assert.AreEqual(144d, snapshot.MetricValues["fps"]);
            foreach (string key in new[] { "cpu", "history" })
            {
                string text = (string)snapshot.MetricValues[key];
                Assert.IsTrue(Encoding.UTF8.GetByteCount(text) <= 2048);
                Assert.IsTrue(text.EndsWith("…"));
                Assert.IsFalse(text.Contains('\uFFFD'), "Truncation must not split a Unicode code point.");
                Assert.AreEqual(text, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text)));
            }
        }

        [TestMethod]
        public void FailedMetricLeaseAcquisitionReleasesSensorDemandAndAllowsRetry()
        {
            var metrics = new Mock<IOnlineMetricService>();
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), metrics.Object);
            int sensorDemand = 0;
            int metricDemand = 0;
            fixture.Config.Setup(config => config.AcquireAllSensors()).Returns(() =>
            {
                sensorDemand++;
                return Disposable.Create(() => sensorDemand--);
            });
            metrics.Setup(service => service.AcquireTelemetry()).Throws(new InvalidOperationException("Unavailable"));

            Assert.ThrowsExactly<InvalidOperationException>(() => fixture.Service.AcquirePreview());
            Assert.AreEqual(0, sensorDemand, "A failed preview must not keep all hardware sensors polling.");

            metrics.Setup(service => service.AcquireTelemetry()).Returns(() =>
            {
                metricDemand++;
                return Disposable.Create(() => metricDemand--);
            });
            using var preview = fixture.Service.AcquirePreview();
            Assert.AreEqual(1, sensorDemand);
            Assert.AreEqual(1, metricDemand);
            fixture.Service.Dispose();
            preview.Dispose();
            Assert.AreEqual(0, sensorDemand);
            Assert.AreEqual(0, metricDemand);
        }

        [TestMethod]
        public void PreviewLeasesKeepSensorAndMetricDemandUntilTheLastConsumerLeaves()
        {
            var metrics = new Mock<IOnlineMetricService>();
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), metrics.Object);
            int sensorDemand = 0;
            int metricDemand = 0;
            fixture.Config.Setup(config => config.AcquireAllSensors()).Returns(() =>
            {
                sensorDemand++;
                return Disposable.Create(() => sensorDemand--);
            });
            metrics.Setup(service => service.AcquireTelemetry()).Returns(() =>
            {
                metricDemand++;
                return Disposable.Create(() => metricDemand--);
            });
            using var first = fixture.Service.AcquirePreview();
            using var second = fixture.Service.AcquirePreview();
            first.Dispose();
            first.Dispose();
            Assert.AreEqual(1, sensorDemand);
            Assert.AreEqual(1, metricDemand);

            second.Dispose();
            Assert.AreEqual(0, sensorDemand);
            Assert.AreEqual(0, metricDemand);
            using var reopened = fixture.Service.AcquirePreview();
            Assert.AreEqual(1, sensorDemand);
            Assert.AreEqual(1, metricDemand);
            fixture.Service.Dispose();
            Assert.AreEqual(0, sensorDemand);
            Assert.AreEqual(0, metricDemand);
        }

        [TestMethod]
        public async Task SnapshotConsumersSharePollingAndDisposalCompletesCurrentAndLateObservers()
        {
            using var stream = new Subject<(DateTime, Dictionary<ISensorEntry, float>)>();
            int subscriptions = 0;
            int releases = 0;
            var provider = new Mock<ISensorService>();
            provider.SetupGet(service => service.SensorSnapshotStream).Returns(Observable.Defer(() =>
            {
                Interlocked.Increment(ref subscriptions);
                return stream.Finally(() => Interlocked.Increment(ref releases));
            }));
            var config = new Mock<ISensorConfig>();
            using var service = new OverlayTelemetryService(provider.Object, config.Object);
            var firstValue = new TaskCompletionSource<OverlayTelemetrySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondValue = new TaskCompletionSource<OverlayTelemetrySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var first = service.Snapshots.Subscribe(value => firstValue.TrySetResult(value), firstValue.SetException);
            using var second = service.Snapshots.Subscribe(value => secondValue.TrySetResult(value),
                secondValue.SetException, () => completed.TrySetResult(true));
            var timestamp = new DateTime(2026, 10, 12, 8, 30, 0, DateTimeKind.Utc);
            stream.OnNext((timestamp, new Dictionary<ISensorEntry, float>()));
            var snapshots = await Task.WhenAll(firstValue.Task, secondValue.Task).WaitAsync(TimeSpan.FromSeconds(5));

            Assert.AreSame(snapshots[0], snapshots[1], "Editor and runtime must observe one coherent sample.");
            Assert.AreEqual(timestamp, snapshots[0].TimestampUtc);
            Assert.AreEqual(1, subscriptions);
            first.Dispose();
            Assert.IsTrue(stream.HasObservers, "Closing one consumer must not detach the remaining consumer.");
            service.Dispose();
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.AreEqual(1, releases);
            Assert.IsFalse(stream.HasObservers);

            var lateCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var late = service.Snapshots.Subscribe(_ => Assert.Fail("A disposed service cannot publish new samples."),
                lateCompletion.SetException, () => lateCompletion.TrySetResult(true));
            await lateCompletion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(stream.HasObservers);
            config.Verify(sensorConfig => sensorConfig.AcquireAllSensors(), Times.Never,
                "Subscribing alone must not change sensor demand; the host owns the preview leases.");
        }

        [TestMethod]
        public async Task CatalogDiscoveryCompletingAfterDisposalIsRejectedAndReleasesTheCatalogGate()
        {
            var discoveryStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var discovered = new TaskCompletionSource<IEnumerable<ISensorEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var fixture = new Fixture(Array.Empty<ISensorEntry>());
            fixture.Provider.Setup(provider => provider.GetSensorEntries()).Returns(() =>
            {
                discoveryStarted.TrySetResult(true);
                return discovered.Task;
            });

            var pending = fixture.Service.GetSourcesAsync();
            await discoveryStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            fixture.Service.Dispose();
            discovered.SetResult(new[] { Sensor("/cpu/load/0", "CPU Total", "Cpu", "Load", "CPU") });

            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() =>
                fixture.Service.GetSourcesAsync().WaitAsync(TimeSpan.FromSeconds(5)));
            fixture.Provider.Verify(provider => provider.GetSensorEntries(), Times.Once);
        }

        [TestMethod]
        public async Task RediscoveredSensorsKeepStableIdsButRejectReadingsFromTheRemovedRuntimeIdentity()
        {
            var original = Sensor("/cpu/0/temperature/1", "CPU Package", "Cpu", "Temperature", "CPU");
            var rediscovered = Sensor("/cpu/0/temperature/42", "CPU Package", "Cpu", "Temperature", "CPU");
            using var fixture = new Fixture(new[] { original });
            var oldSource = (await fixture.Service.GetSourcesAsync()).Single();
            var beforeRemoval = await fixture.Next(new Dictionary<ISensorEntry, float> { [original] = 61 });
            fixture.Provider.Setup(provider => provider.GetSensorEntries()).ReturnsAsync(Array.Empty<ISensorEntry>());
            Assert.AreEqual(0, (await fixture.Service.GetSourcesAsync()).Count);
            var removed = await fixture.Next(new Dictionary<ISensorEntry, float> { [original] = 62 });
            Assert.IsFalse(removed.Values.ContainsKey(oldSource.Id));

            fixture.Provider.Setup(provider => provider.GetSensorEntries()).ReturnsAsync(new[] { rediscovered });
            var newSource = (await fixture.Service.GetSourcesAsync()).Single();
            Assert.AreEqual(oldSource.Id, newSource.Id);
            Assert.AreEqual(oldSource.DeviceKey, newSource.DeviceKey);
            var stale = await fixture.Next(new Dictionary<ISensorEntry, float> { [original] = 99 });
            Assert.IsNull(stale.Values[newSource.Id]);
            var resumed = await fixture.Next(new Dictionary<ISensorEntry, float> { [rediscovered] = 63 });
            Assert.AreEqual(63d, resumed.Values[newSource.Id]);
            Assert.AreEqual(61d, beforeRemoval.Values[oldSource.Id]);
        }

        [TestMethod]
        public async Task MeasuredMemoryCapacityTakesPrecedenceWithoutInventingAFallbackWhenItIsMissing()
        {
            var used = Sensor("/ram/data/0", "RAM Used", "Memory", "Data", "RAM");
            var free = Sensor("/ram/data/1", "RAM Available", "Memory", "Data", "RAM");
            var total = Sensor("/ram/data/2", "RAM Total", "Memory", "Data", "RAM");
            using var fixture = new Fixture(new[] { used, free, total });
            var sources = await fixture.Service.GetSourcesAsync();
            var capacity = sources.Single(source => source.SemanticKey == "ramTotal");
            Assert.AreEqual(3, sources.Count);
            Assert.AreEqual(total.Identifier, capacity.Identifier);
            var measured = await fixture.Next(new Dictionary<ISensorEntry, float>
            {
                [used] = 20, [free] = 11, [total] = 32
            });
            Assert.AreEqual(32d, measured.Values[capacity.Id]);
            var missing = await fixture.Next(new Dictionary<ISensorEntry, float> { [used] = 20, [free] = 11 });
            Assert.IsNull(missing.Values[capacity.Id]);
        }

        [TestMethod]
        public async Task NonfiniteSensorValuesStayUnavailableWhileZeroAndNegativeTemperaturesRemainValid()
        {
            var temperature = Sensor("/cpu/temperature/0", "CPU Package", "Cpu", "Temperature", "CPU");
            var load = Sensor("/cpu/load/0", "CPU Total", "Cpu", "Load", "CPU");
            var used = Sensor("/ram/data/0", "RAM Used", "Memory", "Data", "RAM");
            var free = Sensor("/ram/data/1", "RAM Available", "Memory", "Data", "RAM");
            using var fixture = new Fixture(new[] { temperature, load, used, free });
            var sources = await fixture.Service.GetSourcesAsync();
            string temperatureId = sources.Single(source => source.Identifier == temperature.Identifier).Id;
            string loadId = sources.Single(source => source.Identifier == load.Identifier).Id;
            string totalId = sources.Single(source => source.SemanticKey == "ramTotal").Id;
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float>
                {
                    [temperature] = -12.5f, [load] = invalid, [used] = 8, [free] = invalid
                });
                Assert.AreEqual(-12.5d, snapshot.Values[temperatureId]);
                Assert.IsNull(snapshot.Values[loadId]);
                Assert.IsNull(snapshot.Values[totalId], "An invalid capacity component must not enter a derived total.");
            }
            var idle = await fixture.Next(new Dictionary<ISensorEntry, float>
            {
                [temperature] = 0, [load] = 0, [used] = 0, [free] = 32
            });
            Assert.AreEqual(0d, idle.Values[temperatureId]);
            Assert.AreEqual(0d, idle.Values[loadId]);
            Assert.AreEqual(32d, idle.Values[totalId]);
        }

        [TestMethod]
        public async Task FrameAndPmdFreshnessAreIndependentAndFutureTimestampsCannotRestoreStaleGameData()
        {
            var metrics = new Mock<IOnlineMetricService>();
            var temperature = Sensor("/cpu/temperature/0", "CPU Package", "Cpu", "Temperature", "CPU");
            metrics.SetupGet(service => service.LastFrameTimestampUtc).Returns(() => DateTime.UtcNow.AddMinutes(1));
            metrics.Setup(service => service.GetOnlineFpsMetricValue(EMetric.Average)).Returns(144);
            metrics.Setup(service => service.GetFrameTelemetrySnapshot()).Returns(() => new OnlineFrameTelemetrySnapshot
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(1), Framerate = 165, ProcessName = "Previous game", Runtime = "DXGI"
            });
            metrics.Setup(service => service.GetPmdTelemetrySnapshot()).Returns(() => new OnlinePmdMetrics
            {
                TimestampUtc = DateTime.UtcNow, GpuPowerCurrent = 220, CpuPowerCurrent = 65, SystemPowerCurrent = 320
            });
            using var fixture = new Fixture(new[] { temperature }, metrics.Object);
            string temperatureId = (await fixture.Service.GetSourcesAsync()).Single(source => source.IsHardwareSensor).Id;
            var loading = await fixture.Next(new Dictionary<ISensorEntry, float> { [temperature] = 58 });
            Assert.AreEqual(58d, loading.Values[temperatureId]);
            Assert.AreEqual(220d, loading.Values["metric/PmdGpuPowerCurrent"]);
            Assert.IsNull(loading.Values["metric/Framerate"]);
            Assert.IsNull(loading.Values["metric/OnlineAverage"]);
            Assert.IsNull(loading.MetricValues["metric/ProcessName"]);
            Assert.IsNull(loading.MetricValues["metric/GraphicsAPI"]);
            metrics.Verify(service => service.GetOnlineFpsMetricValue(It.IsAny<EMetric>()), Times.Never);

            metrics.SetupGet(service => service.LastFrameTimestampUtc).Returns(() => DateTime.UtcNow);
            metrics.Setup(service => service.GetFrameTelemetrySnapshot()).Returns(() => new OnlineFrameTelemetrySnapshot
            {
                TimestampUtc = DateTime.UtcNow, Framerate = 120, ProcessName = "Current game", Runtime = "Vulkan"
            });
            metrics.Setup(service => service.GetPmdTelemetrySnapshot()).Returns(new OnlinePmdMetrics
            {
                TimestampUtc = DateTime.UtcNow.AddMinutes(-1), GpuPowerCurrent = 220
            });
            var resumed = await fixture.Next(new Dictionary<ISensorEntry, float> { [temperature] = 59 });
            Assert.AreEqual(59d, resumed.Values[temperatureId]);
            Assert.AreEqual(120d, resumed.Values["metric/Framerate"]);
            Assert.AreEqual(144d, resumed.Values["metric/OnlineAverage"]);
            Assert.AreEqual("Current game", resumed.MetricValues["metric/ProcessName"]);
            Assert.AreEqual("Vulkan", resumed.MetricValues["metric/GraphicsAPI"]);
            Assert.IsNull(resumed.Values["metric/PmdGpuPowerCurrent"]);
        }

        private static SensorEntry Sensor(string id, string name, string hardwareType, string sensorType, string hardwareName)
            => new SensorEntry
            {
                Identifier = id, Name = name, HardwareName = hardwareName,
                HardwareType = hardwareType, SensorType = sensorType, IsPresentationDefault = false
            };

        [TestMethod]
        public async Task NarrowLease_ComputesOnlyTheOnlineMetricsItShows()
        {
            // Regression: every snapshot computed all 14 online metrics, each copying the whole
            // metric window, whatever the tile design showed.
            var metrics = OnlineMetrics();
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), metrics.Object);
            await fixture.Service.GetSourcesAsync();
            using var lease = fixture.Service.AcquirePreview(() => new[] { "metric/OnlineAverage" });

            var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float>());

            Assert.AreEqual(144d, snapshot.Values["metric/OnlineAverage"]);
            Assert.IsNull(snapshot.Values["metric/OnlineP1"]);
            metrics.Verify(m => m.GetOnlineFpsMetricValue(EMetric.Average), Times.Once);
            metrics.Verify(m => m.GetOnlineFpsMetricValue(It.Is<EMetric>(metric => metric != EMetric.Average)), Times.Never);
            metrics.Verify(m => m.GetOnlineFrameTimeMetricValue(It.IsAny<EMetric>()), Times.Never);
            metrics.Verify(m => m.GetOnlineGpuActiveTimeMetricValue(It.IsAny<EMetric>()), Times.Never);
            metrics.Verify(m => m.GetOnlineCpuActiveTimeMetricValue(It.IsAny<EMetric>()), Times.Never);
            metrics.Verify(m => m.GetOnlineGpuActiveTimeDeviationMetricValue(), Times.Never);
            metrics.Verify(m => m.GetOnlineStutteringPercentageValue(), Times.Never);
            metrics.Verify(m => m.GetOnlinePcLatencyAverageValue(), Times.Never);
            metrics.Verify(m => m.GetOnlineAnimationErrorValue(), Times.Never);
        }

        [TestMethod]
        public async Task FullLease_NextToANarrowOne_ComputesEverything()
        {
            // The editors take the full lease: their preview shows every source.
            var metrics = OnlineMetrics();
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), metrics.Object);
            await fixture.Service.GetSourcesAsync();
            using var narrow = fixture.Service.AcquirePreview(() => new[] { "metric/OnlineAverage" });
            using var full = fixture.Service.AcquirePreview();

            var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float>());

            Assert.AreEqual(90d, snapshot.Values["metric/OnlineP1"]);
            metrics.Verify(m => m.GetOnlineStutteringPercentageValue(), Times.Once);
        }

        [TestMethod]
        public async Task TwoNarrowLeases_ComputeWhatEitherShows()
        {
            var metrics = OnlineMetrics();
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), metrics.Object);
            await fixture.Service.GetSourcesAsync();
            using var first = fixture.Service.AcquirePreview(() => new[] { "metric/OnlineAverage" });
            using var second = fixture.Service.AcquirePreview(() => new[] { "metric/OnlineP1" });

            var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float>());

            Assert.AreEqual(144d, snapshot.Values["metric/OnlineAverage"]);
            Assert.AreEqual(90d, snapshot.Values["metric/OnlineP1"]);
            metrics.Verify(m => m.GetOnlineFpsMetricValue(EMetric.P0dot1), Times.Never);
        }

        [TestMethod]
        public async Task NarrowLease_FollowsTheSourcesItReportsNow()
        {
            // A design change only swaps the set the function returns; the lease stays.
            var metrics = OnlineMetrics();
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), metrics.Object);
            await fixture.Service.GetSourcesAsync();
            IReadOnlyCollection<string> shown = new[] { "metric/OnlineAverage" };
            using var lease = fixture.Service.AcquirePreview(() => shown);
            var before = await fixture.Next(new Dictionary<ISensorEntry, float>());

            shown = new[] { "metric/OnlineP1" };
            var after = await fixture.Next(new Dictionary<ISensorEntry, float>());

            Assert.AreEqual(144d, before.Values["metric/OnlineAverage"]);
            Assert.IsNull(before.Values["metric/OnlineP1"]);
            Assert.IsNull(after.Values["metric/OnlineAverage"]);
            Assert.AreEqual(90d, after.Values["metric/OnlineP1"]);
        }

        [TestMethod]
        public async Task NarrowLease_ReportingNull_NeedsEverySource()
        {
            var metrics = OnlineMetrics();
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), metrics.Object);
            await fixture.Service.GetSourcesAsync();
            using var lease = fixture.Service.AcquirePreview(() => null);

            var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float>());

            Assert.AreEqual(90d, snapshot.Values["metric/OnlineP1"]);
        }

        [TestMethod]
        public async Task ReleasedNarrowLease_NoLongerNarrowsTheSnapshot()
        {
            // A subscriber without a lease, like the existing consumers, still gets everything.
            var metrics = OnlineMetrics();
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), metrics.Object);
            await fixture.Service.GetSourcesAsync();
            var lease = fixture.Service.AcquirePreview(() => new[] { "metric/OnlineAverage" });
            lease.Dispose();

            var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float>());

            Assert.AreEqual(90d, snapshot.Values["metric/OnlineP1"]);
        }

        [TestMethod]
        public async Task NarrowLease_AsksTheClassicProviderOnlyForItsValues()
        {
            // Regression: the ping went out every second even without a ping tile.
            var classic = new Mock<IOverlayEntryProvider>();
            classic.Setup(p => p.GetTelemetrySourcesAsync()).ReturnsAsync(
                OverlayUtils.GetOverlayEntryDefaults(Mock.Of<IAppConfiguration>()).Cast<IOverlayEntry>().ToArray());
            IReadOnlySet<string> requested = null;
            classic.Setup(p => p.GetTelemetryValues(It.IsAny<IReadOnlySet<string>>()))
                .Callback((IReadOnlySet<string> identifiers) => requested = identifiers)
                .Returns(new Dictionary<string, object> { ["SystemTime"] = "12:34" });
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), Mock.Of<IOnlineMetricService>(), classic.Object);
            await fixture.Service.GetSourcesAsync();
            using var lease = fixture.Service.AcquirePreview(() => new[]
            {
                "metric/SystemTime", "metric/OnlineAverage", "sensor:cpu%2F0%2Fload%2F0"
            });

            var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float>());

            CollectionAssert.AreEquivalent(new[] { "SystemTime", "OnlineAverage" }, requested.ToArray());
            Assert.AreEqual("12:34", snapshot.MetricValues["metric/SystemTime"]);
            classic.Verify(p => p.GetTelemetryValues(), Times.Never);
        }

        [TestMethod]
        public async Task ShortHardwareNames_FollowTheFullNamesAndOnlyNeedThem()
        {
            var classic = new Mock<IOverlayEntryProvider>();
            classic.Setup(p => p.GetTelemetrySourcesAsync()).ReturnsAsync(
                OverlayUtils.GetOverlayEntryDefaults(Mock.Of<IAppConfiguration>()).Cast<IOverlayEntry>().ToArray());
            IReadOnlySet<string> requested = null;
            classic.Setup(p => p.GetTelemetryValues(It.IsAny<IReadOnlySet<string>>()))
                .Callback((IReadOnlySet<string> identifiers) => requested = identifiers)
                .Returns(new Dictionary<string, object>
                {
                    ["CustomGPU"] = "NVIDIA GeForce RTX 5070 Ti", ["CustomCPU"] = "AMD Ryzen 7 9800X3D 8-Core Processor"
                });
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), Mock.Of<IOnlineMetricService>(), classic.Object);
            var sources = await fixture.Service.GetSourcesAsync();
            Assert.AreEqual("metric/CustomGPUShort", sources.Single(source => source.SemanticKey == "gpuNameShort").Id);
            Assert.AreEqual("metric/CustomCPUShort", sources.Single(source => source.SemanticKey == "cpuNameShort").Id);
            Assert.IsTrue(sources.Where(source => source.SemanticKey is "gpuNameShort" or "cpuNameShort")
                .All(source => source.IsText && !source.IsProcessSpecific));
            using var lease = fixture.Service.AcquirePreview(() => new[] { "metric/CustomGPUShort", "metric/CustomCPUShort" });

            var snapshot = await fixture.Next(new Dictionary<ISensorEntry, float>());

            CollectionAssert.AreEquivalent(new[] { "CustomGPU", "CustomCPU" }, requested.ToArray(), "A short name is read from its full name.");
            Assert.AreEqual("RTX 5070 Ti", snapshot.MetricValues["metric/CustomGPUShort"]);
            Assert.AreEqual("R7 9800X3D", snapshot.MetricValues["metric/CustomCPUShort"]);
        }

        [TestMethod]
        public async Task FullLease_ReadsEveryClassicValue()
        {
            var classic = new Mock<IOverlayEntryProvider>();
            classic.Setup(p => p.GetTelemetrySourcesAsync()).ReturnsAsync(Array.Empty<IOverlayEntry>());
            classic.Setup(p => p.GetTelemetryValues()).Returns(new Dictionary<string, object>());
            using var fixture = new Fixture(Array.Empty<ISensorEntry>(), Mock.Of<IOnlineMetricService>(), classic.Object);
            await fixture.Service.GetSourcesAsync();
            using var lease = fixture.Service.AcquirePreview();

            await fixture.Next(new Dictionary<ISensorEntry, float>());

            classic.Verify(p => p.GetTelemetryValues(), Times.Once);
            classic.Verify(p => p.GetTelemetryValues(It.IsAny<IReadOnlySet<string>>()), Times.Never);
        }

        [TestMethod]
        [DataRow(null, false)]
        [DataRow("metric/FrameGenerationTechnology", true)]
        [DataRow("metric/FrameGenerationStatus", true)]
        public async Task NarrowLease_ReadsTheHookStatusOnlyForFrameGenerationTiles(string shown, bool read)
        {
            var hookStatus = new Mock<IHookOverlayStatusService>();
            hookStatus.SetupGet(h => h.Current).Returns(new HookOverlayStatus(EHookOverlayStatus.Disabled));
            using var stream = new Subject<(DateTime, Dictionary<ISensorEntry, float>)>();
            var sensors = new Mock<ISensorService>();
            sensors.Setup(p => p.GetSensorEntries()).ReturnsAsync(Array.Empty<ISensorEntry>());
            sensors.SetupGet(p => p.SensorSnapshotStream).Returns(stream);
            using var service = new OverlayTelemetryService(sensors.Object, new Mock<ISensorConfig>().Object,
                hookStatus: hookStatus.Object);
            using var lease = service.AcquirePreview(() => shown == null ? new[] { "metric/OnlineAverage" } : new[] { shown });
            var next = new TaskCompletionSource<OverlayTelemetrySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var subscription = service.Snapshots.Take(1).Subscribe(next.SetResult, next.SetException);

            stream.OnNext((DateTime.UtcNow, new Dictionary<ISensorEntry, float>()));
            await next.Task.WaitAsync(TimeSpan.FromSeconds(5));

            hookStatus.VerifyGet(h => h.Current, read ? Times.Once() : Times.Never());
        }

        [TestMethod]
        public async Task NarrowLease_PollsOnlyTheHardwareBehindItsSources()
        {
            // Regression: a tile design took the all-sensors lease, so every per-core, SMU, SMART
            // and PMC value was read for a design that showed two of them.
            var used = Sensor("/ram/data/0", "RAM Used", "Memory", "Data", "RAM");
            var free = Sensor("/ram/data/1", "RAM Available", "Memory", "Data", "RAM");
            var load = Sensor("/cpu/load/0", "CPU Total", "Cpu", "Load", "CPU");
            var temperature = Sensor("/cpu/temperature/0", "CPU Package", "Cpu", "Temperature", "CPU");
            using var fixture = new Fixture(new[] { used, free, load, temperature }, OnlineMetrics().Object);
            Func<IReadOnlySet<string>> leased = null;
            bool released = false;
            fixture.Config.Setup(c => c.AcquireSensors(It.IsAny<Func<IReadOnlySet<string>>>()))
                .Callback((Func<IReadOnlySet<string>> identifiers) => leased = identifiers)
                .Returns(Disposable.Create(() => released = true));
            IReadOnlyCollection<string> shown = null;
            var lease = fixture.Service.AcquirePreview(() => shown);

            Assert.IsNull(leased(), "An unbound design needs every sensor, as before it was known.");
            var sources = await fixture.Service.GetSourcesAsync();
            string loadId = sources.Single(source => source.Identifier == load.Identifier).Id;
            string totalId = sources.Single(source => source.SemanticKey == "ramTotal").Id;
            shown = new[] { loadId, totalId, "metric/OnlineAverage", "sensor:unknown" };
            var identifiers = leased();

            CollectionAssert.AreEquivalent(new[] { load.Identifier, used.Identifier, free.Identifier }, identifiers.ToArray());
            Assert.AreSame(identifiers, leased(), "Resolved once per design and catalog, not per evaluation.");
            shown = new[] { loadId };
            CollectionAssert.AreEquivalent(new[] { load.Identifier }, leased().ToArray());
            fixture.Config.Verify(c => c.AcquireAllSensors(), Times.Never);
            Assert.IsFalse(released);
            lease.Dispose();
            Assert.IsTrue(released, "Releasing the design releases its sensors.");
        }

        [TestMethod]
        public async Task NarrowLease_NamesNoSensorUntilTheCatalogIsKnown()
        {
            var load = Sensor("/cpu/load/0", "CPU Total", "Cpu", "Load", "CPU");
            string loadId;
            using (var catalog = new Fixture(new[] { load }))
                loadId = (await catalog.Service.GetSourcesAsync()).Single().Id;
            using var fixture = new Fixture(new[] { load });
            Func<IReadOnlySet<string>> leased = null;
            fixture.Config.Setup(c => c.AcquireSensors(It.IsAny<Func<IReadOnlySet<string>>>()))
                .Callback((Func<IReadOnlySet<string>> identifiers) => leased = identifiers)
                .Returns(Disposable.Empty);
            var shown = new[] { loadId };
            using var lease = fixture.Service.AcquirePreview(() => shown);

            Assert.AreEqual(0, leased().Count, "The sources are not known yet.");
            await fixture.Service.GetSourcesAsync();

            CollectionAssert.AreEquivalent(new[] { load.Identifier }, leased().ToArray(),
                "The same design resolves again once the catalog is there.");
        }

        [TestMethod]
        public async Task FullLease_StillTakesEverySensor()
        {
            using var fixture = new Fixture(Array.Empty<ISensorEntry>());
            fixture.Config.Setup(c => c.AcquireAllSensors()).Returns(Disposable.Empty);
            await fixture.Service.GetSourcesAsync();

            using var lease = fixture.Service.AcquirePreview();

            fixture.Config.Verify(c => c.AcquireAllSensors(), Times.Once);
            fixture.Config.Verify(c => c.AcquireSensors(It.IsAny<Func<IReadOnlySet<string>>>()), Times.Never);
        }

        private static Mock<IOnlineMetricService> OnlineMetrics()
        {
            var metrics = new Mock<IOnlineMetricService>();
            metrics.SetupGet(m => m.LastFrameTimestampUtc).Returns(() => DateTime.UtcNow);
            metrics.Setup(m => m.GetOnlineFpsMetricValue(EMetric.Average)).Returns(144);
            metrics.Setup(m => m.GetOnlineFpsMetricValue(EMetric.P1)).Returns(90);
            return metrics;
        }

        private sealed class Fixture : IDisposable
        {
            public Subject<(DateTime, Dictionary<ISensorEntry, float>)> Stream { get; } = new Subject<(DateTime, Dictionary<ISensorEntry, float>)>();
            public Mock<ISensorConfig> Config { get; } = new Mock<ISensorConfig>();
            public Mock<ISensorService> Provider { get; } = new Mock<ISensorService>();
            public OverlayTelemetryService Service { get; }

            public Fixture(IEnumerable<ISensorEntry> sensors, IOnlineMetricService metrics = null,
                IOverlayEntryProvider classic = null, IOverlayService overlay = null)
            {
                Provider.Setup(p => p.GetSensorEntries()).ReturnsAsync(sensors);
                Provider.SetupGet(p => p.SensorSnapshotStream).Returns(Stream);
                Service = new OverlayTelemetryService(Provider.Object, Config.Object, metrics, classic, overlay);
            }

            public async Task<OverlayTelemetrySnapshot> Next(Dictionary<ISensorEntry, float> values)
            {
                var next = new TaskCompletionSource<OverlayTelemetrySnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var subscription = Service.Snapshots.Take(1).Subscribe(next.SetResult, next.SetException);
                Stream.OnNext((DateTime.UtcNow, values));
                return await next.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }

            public void Dispose()
            {
                Service.Dispose();
                Stream.Dispose();
            }
        }
    }
}
