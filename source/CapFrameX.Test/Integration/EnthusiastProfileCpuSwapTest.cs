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
using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.Hardware.Simulation;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;
using Match = System.Text.RegularExpressions.Match;
using System.Threading.Tasks;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// A CPU swap under the Enthusiast profile, end to end: the simulated hardware monitor supplies
    /// the sensors, the sensor-to-entry mapping and the template produce the profile, the profile is
    /// persisted and reloaded by the real <see cref="OverlayEntryProvider"/> against a different,
    /// smaller CPU. The overlay's per-core rows must mirror the core layout of whichever CPU is
    /// present: one clock and one load row per core, in enumeration order, labelled with that
    /// core's own type (P/E/LPE/D), no rows of cores that no longer exist and no stale labels
    /// carried over from the previous CPU.
    /// </summary>
    [TestClass]
    [DoNotParallelize] // Computer.Open touches process-global state (OpCode, mutexes)
    public class EnthusiastProfileCpuSwapTest
    {
        private static readonly Regex CoreRowPattern = new Regex(
            @"^Core #(?<core>\d+)(?: (?<suffix>[A-Z]+))? \((?<unit>MHz|%)\)$", RegexOptions.Compiled);

        private string _configFolder;

        [TestInitialize]
        public void Setup()
        {
            _configFolder = Path.Combine(Path.GetTempPath(), "CxCpuSwap_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_configFolder);
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                if (Directory.Exists(_configFolder))
                    Directory.Delete(_configFolder, true);
            }
            catch { }
        }

        [DataTestMethod]
        [DataRow(SimulatedCpuKind.IntelCpu, "8P 16E", SimulatedCpuKind.IntelLpeCpu, "6P 8E 2LPE",
            DisplayName = "Intel 8P+16E -> Intel 6P+8E+2LPE: same identifiers, core types change in place")]
        [DataRow(SimulatedCpuKind.Amd17Cpu, "16", SimulatedCpuKind.AmdHybridCpu, "4P 8D",
            DisplayName = "AMD 16 homogeneous -> AMD 4P+8D: same identifiers, cores gain a type")]
        [DataRow(SimulatedCpuKind.IntelLpeCpu, "6P 8E 2LPE", SimulatedCpuKind.AmdHybridCpu, "4P 8D",
            DisplayName = "Intel 6P+8E+2LPE -> AMD 4P+8D: vendor change, only the P cores match by name")]
        [DataRow(SimulatedCpuKind.IntelCpu, "8P 16E", SimulatedCpuKind.AmdHybridCpu, "4P 8D",
            DisplayName = "Intel 8P+16E -> AMD 4P+8D: vendor change, half the core count")]
        public async Task EnthusiastProfile_MirrorsTheCoreLayout_BeforeAndAfterSwappingToASmallerCpu(
            SimulatedCpuKind cpuBefore, string layoutBefore, SimulatedCpuKind cpuAfter, string layoutAfter)
        {
            var expectedBefore = ExpandLayout(layoutBefore);
            var expectedAfter = ExpandLayout(layoutAfter);
            Assert.IsTrue(expectedAfter.Count < expectedBefore.Count,
                "the scenario has to swap to a CPU with fewer cores");

            // Session 1: the Enthusiast template is applied on the first CPU and the profile is saved.
            using (var rig = SimulatedRig.Open(cpuBefore))
            {
                var provider = CreateProvider(rig, out var sensorService);
                var entries = await provider.GetOverlayEntries(updateFormats: false);

                var templated = ApplyEnthusiastTemplate(sensorService, entries);
                provider.UpdateOverlayEntries(templated);
                await provider.SaveOverlayEntriesToJson(0);

                var applied = await provider.GetOverlayEntries(updateFormats: false);
                AssertCoreRowsMirrorLayout(applied, rig, expectedBefore, "before the swap");
                Assert.IsTrue(applied.Any(entry => entry.ShowOnOverlay && IsPackageRow(entry, "(°C)")),
                    "before the swap: the template must switch the package temperature row on");
            }

            // Session 2: the same profile is loaded on the smaller CPU.
            using (var rig = SimulatedRig.Open(cpuAfter))
            {
                var provider = CreateProvider(rig, out _);
                var entries = await provider.GetOverlayEntries(updateFormats: false);

                Assert.IsTrue(provider.HasHardwareChanged, "the provider must notice the CPU swap");
                AssertCoreRowsMirrorLayout(entries, rig, expectedAfter, "after the swap");

                // Session 3: saving the reconciled profile and loading it again must be a fixed point.
                await provider.SaveOverlayEntriesToJson(0);
                var reloaded = await CreateProvider(rig, out _).GetOverlayEntries(updateFormats: false);

                CollectionAssert.AreEqual(
                    DescribeCoreRows(entries), DescribeCoreRows(reloaded),
                    "reloading the reconciled profile on the same CPU must not change the core rows");
            }
        }

        // ---------------------------------------------------------------- assertions

        private static void AssertCoreRowsMirrorLayout(IReadOnlyList<IOverlayEntry> entries, SimulatedRig rig,
            IReadOnlyList<string> expectedCores, string phase)
        {
            var shownCpuRows = entries
                .Where(entry => entry.OverlayEntryType == EOverlayEntryType.CPU && entry.ShowOnOverlay)
                .ToList();

            var coreRows = shownCpuRows
                .Select(entry => (Entry: entry, Match: CoreRowPattern.Match(entry.Description ?? string.Empty)))
                .Where(row => row.Match.Success)
                .ToList();

            foreach (var unit in new[] { "MHz", "%" })
            {
                var rows = coreRows.Where(row => row.Match.Groups["unit"].Value == unit).ToList();
                var actualCores = rows.Select(row => CoreName(row.Match)).ToList();

                CollectionAssert.AreEqual(expectedCores.ToList(), actualCores,
                    $"{phase}: the {unit} rows must list exactly the cores of '{rig.CpuName}' in layout order, " +
                    $"got [{string.Join(", ", actualCores)}]");

                foreach (var row in rows)
                {
                    Assert.AreEqual(CoreName(row.Match), row.Entry.GroupName,
                        $"{phase}: '{row.Entry.Description}' carries a stale group name");
                    Assert.IsTrue(rig.SensorIdentifiers.Contains(row.Entry.Identifier),
                        $"{phase}: '{row.Entry.Description}' ({row.Entry.Identifier}) is not a sensor of '{rig.CpuName}'");
                }
            }

            // No shown CPU row of any kind (thread loads, effective clocks, temperatures) may point past the layout.
            foreach (var row in shownCpuRows)
            {
                var match = Regex.Match(row.Description ?? string.Empty, @"^Core #(\d+)");
                if (match.Success)
                {
                    Assert.IsTrue(int.Parse(match.Groups[1].Value) <= expectedCores.Count,
                        $"{phase}: '{row.Description}' belongs to a core '{rig.CpuName}' does not have");
                }
            }

            // The rest of the Enthusiast CPU section stays intact. (The package temperature is not
            // checked here: its sensor is named differently per vendor, so it does not survive a
            // vendor change — a sensor-name matter, not a core-layout one.)
            Assert.IsTrue(shownCpuRows.Any(row => row.Description == "CPU Max (MHz)"),
                $"{phase}: 'CPU Max (MHz)' must stay on");
            Assert.IsTrue(shownCpuRows.Any(row => IsPackageRow(row, "(W)")),
                $"{phase}: the package power row must stay on");

            // All core rows share one formatting, i.e. rows added for the new CPU look like their siblings.
            var formats = coreRows
                .Select(row => (row.Entry.GroupColor, row.Entry.Color, row.Entry.ValueFontSize, row.Entry.GroupFontSize))
                .Distinct()
                .ToList();
            Assert.AreEqual(1, formats.Count,
                $"{phase}: all core rows must share one formatting, found {formats.Count} variants");
        }

        private static bool IsPackageRow(IOverlayEntry entry, string unit)
        {
            var description = entry.Description ?? string.Empty;
            return description.StartsWith("CPU Package", StringComparison.Ordinal)
                && description.EndsWith(unit, StringComparison.Ordinal);
        }

        private static List<string> DescribeCoreRows(IEnumerable<IOverlayEntry> entries)
        {
            return entries
                .Where(entry => entry.OverlayEntryType == EOverlayEntryType.CPU && entry.ShowOnOverlay)
                .Where(entry => CoreRowPattern.IsMatch(entry.Description ?? string.Empty))
                .Select(entry => $"{entry.Identifier}|{entry.Description}|{entry.GroupName}")
                .ToList();
        }

        private static string CoreName(Match match)
        {
            var suffix = match.Groups["suffix"].Value;
            var core = match.Groups["core"].Value;
            return suffix.Length == 0 ? $"Core #{core}" : $"Core #{core} {suffix}";
        }

        /// <summary>"6P 8E 2LPE" -> Core #1 P .. Core #6 P, Core #7 E .. Core #14 E, Core #15 LPE, Core #16 LPE.</summary>
        private static List<string> ExpandLayout(string layout)
        {
            var names = new List<string>();
            foreach (var token in layout.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var digits = new string(token.TakeWhile(char.IsDigit).ToArray());
                var suffix = token.Substring(digits.Length);
                for (int i = 0; i < int.Parse(digits); i++)
                {
                    int number = names.Count + 1;
                    names.Add(suffix.Length == 0 ? $"Core #{number}" : $"Core #{number} {suffix}");
                }
            }

            return names;
        }

        // ---------------------------------------------------------------- wiring

        /// <summary>Mirrors OverlayViewModel.OnApplyOverlayTemplate: clone, apply, sort by template section.</summary>
        private static List<IOverlayEntry> ApplyEnthusiastTemplate(ISensorService sensorService, IEnumerable<IOverlayEntry> entries)
        {
            var clones = entries.Select(entry => entry.Clone()).ToList();
            new OverlayTemplateService(sensorService).ApplyTemplate(EOverlayTemplate.Enthusiast, clones);
            return OverlayUtils.SortForTemplate(clones);
        }

        private OverlayEntryProvider CreateProvider(SimulatedRig rig, out MockSensorService sensorService)
        {
            sensorService = new MockSensorService(seed: 42);
            sensorService.SetCpuInfo(rig.CpuName);

            var appConfig = new Mock<IAppConfiguration>();
            appConfig.Setup(x => x.OverlayEntryConfigurationFile).Returns(0);
            appConfig.Setup(x => x.UsePcLatency).Returns(true);
            appConfig.Setup(x => x.HardwareInfoSource).Returns("Auto");
            appConfig.Setup(x => x.ShowSystemTimeSeconds).Returns(false);
            appConfig.Setup(x => x.OnValueChanged).Returns(Observable.Never<(string key, object value)>());

            var eventAggregator = new Mock<IEventAggregator>();
            eventAggregator.Setup(x => x.GetEvent<PubSubEvent<ViewMessages.OptionPopupClosed>>())
                .Returns(new PubSubEvent<ViewMessages.OptionPopupClosed>());

            var rtss = new Mock<IRTSSService>();
            rtss.Setup(x => x.ProcessIdStream).Returns(new BehaviorSubject<int>(0));
            rtss.Setup(x => x.GetCurrentFramerate(It.IsAny<int>())).Returns(Tuple.Create(0.0, 0.0));

            var pathService = new Mock<IPathService>();
            pathService.Setup(x => x.ConfigFolder).Returns(_configFolder);

            var hookStatus = new Mock<IHookOverlayStatusService>();
            hookStatus.Setup(x => x.Current).Returns(new HookOverlayStatus(EHookOverlayStatus.Disabled));
            hookStatus.Setup(x => x.StatusStream).Returns(Observable.Never<HookOverlayStatus>());

            return new OverlayEntryProvider(
                sensorService,
                appConfig.Object,
                eventAggregator.Object,
                new Mock<IOnlineMetricService>().Object,
                new Mock<ISystemInfo>().Object,
                rtss.Object,
                new Mock<ISensorConfig>().Object,
                rig.Core,
                new Mock<IThreadAffinityController>().Object,
                pathService.Object,
                hookStatus.Object,
                new Mock<ILogger<OverlayEntryProvider>>().Object,
                () => Array.Empty<DetectedDisplay>());
        }

        /// <summary>
        /// One simulated machine: the hardware monitor with a simulated CPU, its sensors mapped through the
        /// production path (SensorEntry.FromSensor, SensorOverlayEntryFactory) into the overlay entry core.
        /// </summary>
        private sealed class SimulatedRig : IDisposable
        {
            private readonly Computer _computer;

            private SimulatedRig(Computer computer, IHardware cpu)
            {
                _computer = computer;
                CpuName = cpu.Name;

                var sensors = cpu.Sensors.Select(SensorEntry.FromSensor).ToList();
                SensorIdentifiers = new HashSet<string>(sensors.Select(sensor => sensor.Identifier));

                Core = new OverlayEntryCore();
                foreach (var sensor in sensors)
                    Core.OverlayEntryDict.TryAdd(sensor.Identifier, SensorOverlayEntryFactory.Create(sensor));
                Core.OverlayEntryCoreCompletionSource.SetResult(true);
            }

            public string CpuName { get; }

            public OverlayEntryCore Core { get; }

            public HashSet<string> SensorIdentifiers { get; }

            public static SimulatedRig Open(SimulatedCpuKind kind)
            {
                var computer = new Computer(new SimulationConfiguration { Mode = SimulationMode.Enabled, Cpu = kind })
                {
                    IsCpuEnabled = true
                };
                computer.Open();

                var cpu = computer.Hardware.Single(hardware => hardware.HardwareType == HardwareType.Cpu);
                return new SimulatedRig(computer, cpu);
            }

            public void Dispose() => _computer.Close();
        }
    }
}
