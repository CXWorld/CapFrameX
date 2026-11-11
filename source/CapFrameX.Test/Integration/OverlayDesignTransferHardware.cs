using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.OSD.Integration;
using CapFrameX.Overlay;
using CapFrameX.PresentMonInterface;
using CapFrameX.Sensor;
using Moq;

namespace CapFrameX.Test.Integration
{
    /// <summary>A sensor as LibreHardwareMonitor reports it: type ("Load") and name ("GPU Core").</summary>
    internal sealed record SimSensor(string Type, string Name);

    /// <summary>A CPU core with its type label (null on CPUs without core types) and thread count.</summary>
    internal sealed record SimCore(int Index, string Type, int Threads);

    internal abstract class SimDevice
    {
        public string Name { get; init; }
        public string HardwareType { get; init; }
        public string Vendor { get; init; }
        public string IdentifierRoot { get; init; }
        public abstract IReadOnlyList<SimSensor> Sensors { get; }
        public bool Has(string type, string name) => Sensors.Any(sensor => sensor.Type == type && sensor.Name == name);
    }

    internal sealed class SimGpu : SimDevice
    {
        private readonly SimSensor[] _sensors;
        public SimGpu(string name, string hardwareType, string vendor, string root, params (string Type, string Name)[] sensors)
        {
            Name = name; HardwareType = hardwareType; Vendor = vendor; IdentifierRoot = root;
            _sensors = sensors.Select(sensor => new SimSensor(sensor.Type, sensor.Name)).ToArray();
        }
        public override IReadOnlyList<SimSensor> Sensors => _sensors;
    }

    internal sealed class SimOther : SimDevice
    {
        private readonly SimSensor[] _sensors;
        public SimOther(string name, string hardwareType, string root, params (string Type, string Name)[] sensors)
        {
            Name = name; HardwareType = hardwareType; IdentifierRoot = root;
            _sensors = sensors.Select(sensor => new SimSensor(sensor.Type, sensor.Name)).ToArray();
        }
        public override IReadOnlyList<SimSensor> Sensors => _sensors;
    }

    /// <summary>A CPU whose sensors follow the names GenericCpu, IntelCpu and Amd17Cpu give them.</summary>
    internal sealed class SimCpu : SimDevice
    {
        private readonly Lazy<SimSensor[]> _sensors;
        public SimCore[] Cores { get; }
        public bool Hybrid => Cores.Select(core => core.Type).Where(type => type != null).Distinct().Count() > 1;
        public IReadOnlyList<string> CoreTypes => Cores.Select(core => core.Type).Distinct().ToArray();

        public SimCpu(string name, string vendor, string root, params (string Type, int Count, int Threads)[] layout)
        {
            Name = name; Vendor = vendor; HardwareType = "Cpu"; IdentifierRoot = root;
            int index = 1;
            Cores = layout.SelectMany(run => Enumerable.Range(0, run.Count).Select(_ => new SimCore(index++, run.Type, run.Threads))).ToArray();
            _sensors = new Lazy<SimSensor[]>(BuildSensors);
        }

        public override IReadOnlyList<SimSensor> Sensors => _sensors.Value;

        public static string CoreName(SimCore core) => "Core #" + core.Index + (core.Type == null ? "" : " " + core.Type);

        public static IEnumerable<string> LoadNames(SimCore core) => core.Threads == 1
            ? new[] { CoreName(core) }
            : Enumerable.Range(1, core.Threads).Select(thread => CoreName(core) + " Thread #" + thread);

        private SimSensor[] BuildSensors()
        {
            bool intel = Vendor == "Intel";
            var sensors = new List<SimSensor> { new("Load", "CPU Total"), new("Load", "CPU Core Max") };
            sensors.AddRange(Cores.SelectMany(LoadNames).Select(name => new SimSensor("Load", name)));
            sensors.Add(new SimSensor("Temperature", intel ? "CPU Package" : "CPU Package (Tctl/Tdie)"));
            if (intel)
            {
                sensors.Add(new SimSensor("Temperature", "Core Max"));
                sensors.AddRange(Cores.Select(core => new SimSensor("Temperature", CoreName(core))));
            }
            else
            {
                sensors.AddRange(Enumerable.Range(1, Math.Max(1, Cores.Length / 8)).Select(ccd => new SimSensor("Temperature", "CCD" + ccd + " (Tdie)")));
            }
            sensors.Add(new SimSensor("Clock", "CPU Max"));
            sensors.AddRange(Cores.Select(core => new SimSensor("Clock", CoreName(core))));
            if (Hybrid)
                sensors.AddRange(CoreTypes.Select(type => new SimSensor("Clock", "CPU " + type + " Max")));
            // Intel names its per-thread effective clocks without the core type, AMD with it.
            sensors.AddRange(Cores.SelectMany(core => Enumerable.Range(1, core.Threads).Select(thread =>
                new SimSensor("Clock", (intel ? "Core #" + core.Index : CoreName(core)) + " Thread #" + thread + " (Effective)"))));
            sensors.Add(new SimSensor("Power", "CPU Package"));
            sensors.Add(new SimSensor("Power", "CPU Cores"));
            if (!intel) sensors.AddRange(Cores.Select(core => new SimSensor("Power", CoreName(core) + " (SMU)")));
            return sensors.ToArray();
        }
    }

    /// <summary>A PC: one CPU, its GPUs, memory and a drive, with the source catalog CapFrameX builds for it.</summary>
    internal sealed class SimMachine
    {
        private readonly Lazy<IReadOnlyList<OverlayTelemetrySource>> _catalog;
        public string Id { get; }
        public SimCpu Cpu { get; }
        public SimGpu[] Gpus { get; }
        public SimOther Memory { get; } = new SimOther("Total Memory", "Memory", "ram",
            ("Data", "Memory Used"), ("Data", "Memory Available"), ("Load", "Memory"));
        public SimOther Drive { get; }
        public IReadOnlyList<SimDevice> Devices => new SimDevice[] { Cpu }.Concat(Gpus).Append(Memory).Append(Drive).ToArray();
        public IReadOnlyList<OverlayTelemetrySource> Catalog => _catalog.Value;

        public SimMachine(string id, SimCpu cpu, SimOther drive, params SimGpu[] gpus)
        {
            Id = id; Cpu = cpu; Gpus = gpus; Drive = drive;
            _catalog = new Lazy<IReadOnlyList<OverlayTelemetrySource>>(BuildCatalog);
        }

        public override string ToString() => Id;

        public IEnumerable<ISensorEntry> SensorEntries() => Devices.SelectMany(device => device.Sensors.Select((sensor, index) => (ISensorEntry)new SensorEntry
        {
            Identifier = "/" + device.IdentifierRoot + "/" + sensor.Type.ToLowerInvariant() + "/" + index,
            Name = sensor.Name, HardwareName = device.Name, HardwareType = device.HardwareType,
            SensorType = sensor.Type, IsPresentationDefault = false
        }));

        /// <summary>The catalog the real telemetry service discovers, with its metrics and classic entries.</summary>
        private IReadOnlyList<OverlayTelemetrySource> BuildCatalog()
        {
            var sensors = new Mock<ISensorService>();
            sensors.Setup(service => service.GetSensorEntries()).ReturnsAsync(SensorEntries().ToArray());
            sensors.SetupGet(service => service.SensorSnapshotStream)
                .Returns(Observable.Never<(DateTime, Dictionary<ISensorEntry, float>)>());
            var classic = new Mock<IOverlayEntryProvider>();
            classic.Setup(provider => provider.GetTelemetrySourcesAsync()).ReturnsAsync(
                OverlayUtils.GetOverlayEntryDefaults(Mock.Of<IAppConfiguration>()).Cast<IOverlayEntry>().ToArray());
            using var telemetry = new OverlayTelemetryService(sensors.Object, Mock.Of<ISensorConfig>(), Mock.Of<IOnlineMetricService>(), classic.Object);
            return telemetry.GetSourcesAsync().GetAwaiter().GetResult();
        }

        // The identifier root tells identical devices apart.
        public OverlayTelemetrySource Source(SimDevice device, string type, string name) => Catalog.Single(source =>
            source.IsHardwareSensor && source.HardwareName == device.Name && source.SensorType == type && source.Name == name
            && source.Identifier.StartsWith("/" + device.IdentifierRoot + "/", StringComparison.Ordinal));
    }

    internal static class SimHardware
    {
        private static (string, string)[] Nvidia(bool hotSpot) => new[]
        {
            ("Load", "GPU Core"), ("Load", "GPU Memory Controller"), ("Load", "GPU Video Engine"), ("Load", "GPU Memory"),
            ("Temperature", "GPU Core"), ("Temperature", "GPU Memory Junction"),
            ("Clock", "GPU Core"), ("Clock", "GPU Memory"), ("Clock", "GPU Video"), ("Power", "GPU Power"),
            ("Data", "GPU Memory Used"), ("Data", "GPU Memory Total"), ("Data", "GPU Memory Free"), ("Data", "GPU Memory Dedicated"),
            ("Fan", "GPU"), ("Control", "GPU Fan"), ("Factor", "GPU Power Limit")
        }.Concat(hotSpot ? new[] { ("Temperature", "GPU Hot Spot") } : Array.Empty<(string, string)>()).ToArray();

        private static (string, string)[] Radeon(bool totalBoardPower) => new[]
        {
            ("Load", "GPU Core"), ("Load", "GPU Memory"), ("Temperature", "GPU Core"), ("Temperature", "GPU Hot Spot"),
            ("Temperature", "GPU Memory"), ("Clock", "GPU Core"), ("Clock", "GPU Memory"), ("Power", "GPU Power"),
            ("Data", "GPU Memory Dedicated"), ("Data", "GPU Memory Total"), ("Data", "GPU Memory Shared"),
            ("Fan", "GPU Fan"), ("Control", "GPU Fan")
        }.Concat(totalBoardPower ? new[] { ("Power", "GPU TBP") } : Array.Empty<(string, string)>()).ToArray();

        private static readonly (string, string)[] IntegratedRadeon =
            { ("Load", "GPU Core"), ("Temperature", "GPU Core"), ("Clock", "GPU Core"), ("Power", "GPU Power"), ("Data", "GPU Memory Shared") };

        public static readonly SimGpu Rtx5090 = new("NVIDIA GeForce RTX 5090", "GpuNvidia", "NVIDIA", "gpu-nvidia/0", Nvidia(false));
        public static readonly SimGpu Rtx5070Ti = new("NVIDIA GeForce RTX 5070 Ti", "GpuNvidia", "NVIDIA", "gpu-nvidia/0", Nvidia(false));
        public static readonly SimGpu Rtx4060 = new("NVIDIA GeForce RTX 4060", "GpuNvidia", "NVIDIA", "gpu-nvidia/1", Nvidia(true));
        public static readonly SimGpu Rtx4060Laptop = new("NVIDIA GeForce RTX 4060 Laptop GPU", "GpuNvidia", "NVIDIA", "gpu-nvidia/0", Nvidia(true));
        public static readonly SimGpu Rx9070Xt = new("AMD Radeon RX 9070 XT", "GpuAmd", "AMD", "gpu-amd/0", Radeon(true));
        public static readonly SimGpu Rx7900Xtx = new("AMD Radeon RX 7900 XTX", "GpuAmd", "AMD", "gpu-amd/0", Radeon(false));
        public static readonly SimGpu Rx7900XtxTwin = new("AMD Radeon RX 7900 XTX", "GpuAmd", "AMD", "gpu-amd/1", Radeon(false));
        public static readonly SimGpu RadeonGraphics = new("AMD Radeon(TM) Graphics", "GpuAmd", "AMD", "gpu-amd/1", IntegratedRadeon);
        public static readonly SimGpu Radeon890M = new("AMD Radeon 890M", "GpuAmd", "AMD", "gpu-amd/1", IntegratedRadeon);
        public static readonly SimGpu ArcB580 = new("Intel(R) Arc(TM) B580 Graphics", "GpuIntel", "Intel", "gpu-intel/0",
            ("Load", "GPU Core"), ("Load", "GPU Media Engine"), ("Temperature", "GPU Core"), ("Temperature", "GPU Memory"),
            ("Clock", "GPU Core"), ("Clock", "GPU Memory"), ("Power", "GPU Power"), ("Power", "GPU TBP"),
            ("Data", "GPU Memory Dedicated"), ("Data", "GPU Memory Total"), ("Fan", "GPU Fan"));
        public static readonly SimGpu ArcGraphics = new("Intel(R) Arc(TM) Graphics", "GpuIntel", "Intel", "gpu-intel/0",
            ("Load", "GPU Core"), ("Temperature", "GPU Core"), ("Clock", "GPU Core"), ("Power", "GPU Power"), ("Data", "D3D Shared Memory Used"));
        public static readonly SimGpu Uhd770 = new("Intel(R) UHD Graphics 770", "GpuIntel", "Intel", "gpu-intel/1",
            ("Load", "GPU Core"), ("Data", "D3D Shared Memory Used"));

        public static SimOther Drive(string name) => new(name, "Storage", "nvme/0", ("Temperature", "Temperature"), ("Load", "Used Space"));

        public static readonly SimCpu Ultra9285K = new("Intel Core Ultra 9 285K", "Intel", "intelcpu/0", ("P", 8, 1), ("E", 16, 1));
        public static readonly SimCpu Ultra5245K = new("Intel Core Ultra 5 245K", "Intel", "intelcpu/0", ("P", 6, 1), ("E", 8, 1));
        public static readonly SimCpu I914900K = new("Intel Core i9-14900K", "Intel", "intelcpu/0", ("P", 8, 2), ("E", 16, 1));
        public static readonly SimCpu I513600K = new("Intel Core i5-13600K", "Intel", "intelcpu/0", ("P", 6, 2), ("E", 8, 1));
        public static readonly SimCpu I512400 = new("Intel Core i5-12400", "Intel", "intelcpu/0", (null, 6, 2));
        public static readonly SimCpu Ultra7155H = new("Intel Core Ultra 7 155H", "Intel", "intelcpu/0", ("P", 6, 2), ("E", 8, 1), ("LPE", 2, 1));
        public static readonly SimCpu Ryzen9800X3D = new("AMD Ryzen 7 9800X3D", "AMD", "amdcpu/0", (null, 8, 2));
        public static readonly SimCpu Ryzen9950X3D = new("AMD Ryzen 9 9950X3D", "AMD", "amdcpu/0", (null, 16, 2));
        public static readonly SimCpu Ryzen7600X = new("AMD Ryzen 5 7600X", "AMD", "amdcpu/0", (null, 6, 2));
        public static readonly SimCpu RyzenHx370 = new("AMD Ryzen AI 9 HX 370 w/ Radeon 890M", "AMD", "amdcpu/0", ("P", 4, 2), ("D", 8, 2));

        public static readonly SimMachine[] Machines =
        {
            new("Ultra9-285K+RTX5090", Ultra9285K, Drive("Samsung SSD 990 PRO 2TB"), Rtx5090),
            new("Ultra5-245K+RTX5070Ti", Ultra5245K, Drive("WD_BLACK SN850X 2000GB"), Rtx5070Ti),
            new("i9-14900K+RTX4060+UHD770", I914900K, Drive("Samsung SSD 990 PRO 2TB"), Rtx4060, Uhd770),
            new("i5-13600K+RX9070XT", I513600K, Drive("WD_BLACK SN850X 2000GB"), Rx9070Xt),
            new("i5-12400+ArcB580", I512400, Drive("Crucial T700 2TB"), ArcB580),
            new("Ultra7-155H+ArcGraphics", Ultra7155H, Drive("Samsung SSD 990 PRO 2TB"), ArcGraphics),
            new("R7-9800X3D+RTX5090", Ryzen9800X3D, Drive("Samsung SSD 990 PRO 2TB"), Rtx5090, RadeonGraphics),
            new("R9-9950X3D+RX7900XTX", Ryzen9950X3D, Drive("Crucial T700 2TB"), Rx7900Xtx, RadeonGraphics),
            new("R5-7600X+RX9070XT", Ryzen7600X, Drive("WD_BLACK SN850X 2000GB"), Rx9070Xt, RadeonGraphics),
            new("HX370+RTX4060Laptop", RyzenHx370, Drive("Samsung SSD 990 PRO 2TB"), Rtx4060Laptop, Radeon890M),
            new("Ultra9-285K+RTX5090+RTX4060", Ultra9285K, Drive("Crucial T700 2TB"), Rtx5090, Rtx4060),
            new("R7-9800X3D+ArcB580", Ryzen9800X3D, Drive("WD_BLACK SN850X 2000GB"), ArcB580, RadeonGraphics),
            // Identical cards: their sensor IDs carry the runtime identifier.
            new("R9-9950X3D+2xRX7900XTX", Ryzen9950X3D, Drive("Crucial T700 2TB"), Rx7900Xtx, Rx7900XtxTwin)
        };

        public static SimMachine Machine(string id) => Machines.Single(machine => machine.Id == id);
    }
}
