using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
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
            CollectionAssert.AreEquivalent(defaults.Select(d => d.Identifier).Concat(new[] { "ProcessName", "GraphicsAPI" }).ToArray(),
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

        private static SensorEntry Sensor(string id, string name, string hardwareType, string sensorType, string hardwareName)
            => new SensorEntry
            {
                Identifier = id, Name = name, HardwareName = hardwareName,
                HardwareType = hardwareType, SensorType = sensorType, IsPresentationDefault = false
            };

        private sealed class Fixture : IDisposable
        {
            public Subject<(DateTime, Dictionary<ISensorEntry, float>)> Stream { get; } = new Subject<(DateTime, Dictionary<ISensorEntry, float>)>();
            public Mock<ISensorConfig> Config { get; } = new Mock<ISensorConfig>();
            public OverlayTelemetryService Service { get; }

            public Fixture(IEnumerable<ISensorEntry> sensors, IOnlineMetricService metrics = null,
                IOverlayEntryProvider classic = null, IOverlayService overlay = null)
            {
                var provider = new Mock<ISensorService>();
                provider.Setup(p => p.GetSensorEntries()).ReturnsAsync(sensors);
                provider.SetupGet(p => p.SensorSnapshotStream).Returns(Stream);
                Service = new OverlayTelemetryService(provider.Object, Config.Object, metrics, classic, overlay);
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
