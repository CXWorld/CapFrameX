using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LibreHardwareMonitor.Hardware.Cpu;

namespace LibreHardwareMonitor.Hardware.Simulation;

/// <summary>
/// A run of identical cores inside a simulated processor: the core type, how many of them follow
/// each other in enumeration order and the ranges their per-core sensors move in.
/// </summary>
internal sealed class SimulatedCoreClass
{
    public SimulatedCoreClass(CpuCoreType type, int count, float minClock, float maxClock, float maxTemperature)
    {
        Type = type;
        Count = count;
        MinClock = minClock;
        MaxClock = maxClock;
        MaxTemperature = maxTemperature;
    }

    public CpuCoreType Type { get; }

    public int Count { get; }

    public float MinClock { get; }

    public float MaxClock { get; }

    public float MaxTemperature { get; }
}

/// <summary>
/// Describes a simulated processor: marketing name, core layout in enumeration order and the
/// package-level ranges. The layout is expanded to one entry per core so the sensor builders can
/// index it the way the real drivers index logical processors.
/// </summary>
internal sealed class SimulatedCpuModel
{
    public SimulatedCpuModel(
        string name,
        IReadOnlyList<SimulatedCoreClass> coreClasses,
        float minPackagePower,
        float maxPackagePower,
        float minPackageTemperature,
        float maxPackageTemperature)
    {
        Name = name;
        CoreClasses = coreClasses;
        MinPackagePower = minPackagePower;
        MaxPackagePower = maxPackagePower;
        MinPackageTemperature = minPackageTemperature;
        MaxPackageTemperature = maxPackageTemperature;

        List<SimulatedCoreClass> cores = new();
        foreach (SimulatedCoreClass coreClass in coreClasses)
        {
            for (int i = 0; i < coreClass.Count; i++)
                cores.Add(coreClass);
        }

        Cores = cores;
        MinClock = cores.Min(core => core.MinClock);
        MaxClock = cores.Max(core => core.MaxClock);
        MaxCoreTemperature = cores.Max(core => core.MaxTemperature);
        FastestCore = cores.OrderByDescending(core => core.MaxClock).First();
    }

    public string Name { get; }

    /// <summary>The layout as declared: one entry per run of identical cores.</summary>
    public IReadOnlyList<SimulatedCoreClass> CoreClasses { get; }

    /// <summary>The layout expanded to one entry per core, in enumeration order.</summary>
    public IReadOnlyList<SimulatedCoreClass> Cores { get; }

    public int CoreCount => Cores.Count;

    public bool IsHybrid => Cores.Any(core => core.Type != CpuCoreType.Standard);

    /// <summary>The core class with the highest boost clock; the "CPU Max" style aggregates track it.</summary>
    public SimulatedCoreClass FastestCore { get; }

    public float MinClock { get; }

    public float MaxClock { get; }

    public float MaxCoreTemperature { get; }

    public float MinPackagePower { get; }

    public float MaxPackagePower { get; }

    public float MinPackageTemperature { get; }

    public float MaxPackageTemperature { get; }

    /// <summary>
    /// "Core #N" plus the hybrid suffix, exactly like <see cref="GenericCpu.GetCoreLabel(CpuId)"/>
    /// produces it for real hardware; homogeneous parts get no suffix.
    /// </summary>
    public string GetCoreName(int coreIndex)
    {
        string label = GetCoreLabel(Cores[coreIndex].Type);
        return label.Length == 0 ? $"Core #{coreIndex + 1}" : $"Core #{coreIndex + 1} {label}";
    }

    /// <summary>Human-readable layout for the group report, e.g. "6 P + 8 E + 2 LPE".</summary>
    public string DescribeLayout()
    {
        return string.Join(" + ", CoreClasses.Select(coreClass =>
        {
            string label = GetCoreLabel(coreClass.Type);
            return label.Length == 0 ? $"{coreClass.Count} cores" : $"{coreClass.Count} {label}";
        }));
    }

    /// <summary>
    /// Suffix per core type, mirroring the CPUID-derived labels in <see cref="GenericCpu.GetCoreLabel(CpuId)"/>:
    /// "P"/"E"/"LPE" on Intel, "P"/"D"/"LP" on AMD, nothing for <see cref="CpuCoreType.Standard"/>.
    /// </summary>
    public static string GetCoreLabel(CpuCoreType type)
    {
        return type switch
        {
            CpuCoreType.PerformanceCore => "P",
            CpuCoreType.EfficiencyCore => "E",
            CpuCoreType.LowPowerEfficiencyCore => "LPE",
            CpuCoreType.DenseCore => "D",
            CpuCoreType.LowPowerCore => "LP",
            _ => string.Empty
        };
    }
}

/// <summary>
/// The processors the simulation can publish. Clock and temperature ranges are per core type so the
/// graphs of P-, E-, LPE- and dense cores stay distinguishable. Adding a layout is one entry here plus
/// a <see cref="SimulatedCpuKind"/> value wired up in <see cref="SimulatedCpuGroup"/>.
/// </summary>
internal static class SimulatedCpuModels
{
    /// <summary>AMD Ryzen 9 7950X (Zen 4): 16 identical cores, no hybrid suffix.</summary>
    public static readonly SimulatedCpuModel AmdRyzen9_7950X = new(
        "AMD Ryzen 9 7950X (Simulated)",
        [new SimulatedCoreClass(CpuCoreType.Standard, 16, 2800f, 5450f, 88f)],
        25f, 170f, 32f, 88f);

    /// <summary>AMD Ryzen AI 9 HX 370 (Strix Point): 4 Zen 5 cores followed by 8 Zen 5c cores ("P" / "D").</summary>
    public static readonly SimulatedCpuModel AmdRyzenAi9Hx370 = new(
        "AMD Ryzen AI 9 HX 370 (Simulated)",
        [
            new SimulatedCoreClass(CpuCoreType.PerformanceCore, 4, 2000f, 5100f, 95f),
            new SimulatedCoreClass(CpuCoreType.DenseCore, 8, 1500f, 3300f, 90f)
        ],
        8f, 54f, 35f, 95f);

    /// <summary>Intel Core i9-14900K (Raptor Lake Refresh): 8 P-cores followed by 16 E-cores.</summary>
    public static readonly SimulatedCpuModel IntelCoreI9_14900K = new(
        "Intel Core i9-14900K (Simulated)",
        [
            new SimulatedCoreClass(CpuCoreType.PerformanceCore, 8, 2400f, 6000f, 95f),
            new SimulatedCoreClass(CpuCoreType.EfficiencyCore, 16, 1800f, 4400f, 85f)
        ],
        30f, 200f, 30f, 92f);

    /// <summary>
    /// Intel Core Ultra 9 285H (Arrow Lake-H): 6 P-cores, 8 E-cores and 2 low-power E-cores on the SoC tile
    /// ("P" / "E" / "LPE"). The LPE cores sit outside the L3, which is what the real driver keys the label on.
    /// </summary>
    public static readonly SimulatedCpuModel IntelCoreUltra9_285H = new(
        "Intel Core Ultra 9 285H (Simulated)",
        [
            new SimulatedCoreClass(CpuCoreType.PerformanceCore, 6, 2000f, 5400f, 95f),
            new SimulatedCoreClass(CpuCoreType.EfficiencyCore, 8, 1500f, 4500f, 85f),
            new SimulatedCoreClass(CpuCoreType.LowPowerEfficiencyCore, 2, 700f, 2500f, 75f)
        ],
        15f, 115f, 30f, 98f);
}

internal abstract class SimulatedCpuBase : Hardware
{
    private readonly List<SimulatedSensorSlot> _simulatedSensors = new();
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly Random _random;

    protected SimulatedCpuBase(SimulatedCpuModel model, Identifier identifier, ISettings settings)
        : base(model.Name, identifier, settings)
    {
        Model = model;
        _random = new Random(identifier.ToString().GetHashCode());
    }

    public override HardwareType HardwareType => HardwareType.Cpu;

    public SimulatedCpuModel Model { get; }

    protected void AddSimulatedSensor(
        string name,
        int index,
        SensorType type,
        float min,
        float max,
        float speed,
        float noise,
        bool isPresentationDefault,
        string presentationSortKey)
    {
        Sensor sensor = new Sensor(name, index, type, this, _settings)
        {
            IsPresentationDefault = isPresentationDefault
        };

        if (!string.IsNullOrWhiteSpace(presentationSortKey))
            sensor.PresentationSortKey = presentationSortKey;

        ActivateSensor(sensor);
        _simulatedSensors.Add(new SimulatedSensorSlot(sensor, min, max, speed, SimulationHelpers.NextPhase(_random), noise));
    }

    public override void Update()
    {
        SimulationHelpers.UpdateSensors(_simulatedSensors, _startTime, _random);
    }
}

/// <summary>
/// Simulates an AMD Zen processor with the sensor set of Amd17Cpu.cs: per-core Clock (IsPresentationDefault=true)
/// and Load, CPU Package Temperature/Power, CPU Core/SoC Voltage, Bus/Max/Effective Clocks.
/// On hybrid layouts the core names carry the "P"/"D"/"LP" suffix like the real driver.
/// </summary>
internal sealed class SimulatedAmd17Cpu : SimulatedCpuBase
{
    public SimulatedAmd17Cpu(SimulatedCpuModel model, int processorIndex, ISettings settings)
        : base(model,
            new Identifier("amdcpu-sim", processorIndex.ToString(CultureInfo.InvariantCulture)),
            settings)
    {
        int sensorIndex = 0;
        SimulatedCoreClass fastest = model.FastestCore;

        // Per-core Clock sensors - matches Amd17Cpu Core class (IsPresentationDefault=true, sortkey 0_0_0_{id})
        for (int core = 0; core < model.CoreCount; core++)
        {
            SimulatedCoreClass coreClass = model.Cores[core];
            AddSimulatedSensor(model.GetCoreName(core), sensorIndex++, SensorType.Clock, coreClass.MinClock, coreClass.MaxClock, 0.18f, 35f, true, $"0_0_0_{core}");
        }

        // Bus and aggregate Clock sensors - matches Amd17Cpu Processor class
        AddSimulatedSensor("CPU Bus", sensorIndex++, SensorType.Clock, 99f, 101f, 0.02f, 0.5f, false, "0_1_0");
        AddSimulatedSensor("CPU Clock", sensorIndex++, SensorType.Clock, model.MinClock, model.MaxClock, 0.18f, 35f, false, "0_1_1");
        AddSimulatedSensor("CPU Effective", sensorIndex++, SensorType.Clock, model.MinClock - 200f, model.MaxClock - 50f, 0.19f, 40f, false, "0_1_2");
        AddSimulatedSensor("CPU Max", sensorIndex++, SensorType.Clock, fastest.MinClock + 200f, fastest.MaxClock + 150f, 0.2f, 40f, false, "0_1_3");
        AddSimulatedSensor("CPU Max Effective", sensorIndex++, SensorType.Clock, fastest.MinClock + 400f, fastest.MaxClock + 150f, 0.2f, 45f, false, "0_1_4");

        // Per-core Load sensors - matches Amd17Cpu (sortkey 1_1_{core})
        for (int core = 0; core < model.CoreCount; core++)
            AddSimulatedSensor(model.GetCoreName(core), sensorIndex++, SensorType.Load, 1f, 100f, 0.28f, 2.2f, false, $"1_1_{core + 1}");

        // Aggregate Load sensors
        AddSimulatedSensor("CPU Total", sensorIndex++, SensorType.Load, 2f, 98f, 0.25f, 2.5f, true, "1_2_1");
        AddSimulatedSensor("CPU Max", sensorIndex++, SensorType.Load, 5f, 100f, 0.35f, 3f, false, "1_2_2");

        // Power sensor - matches Amd17Cpu Processor class (IsPresentationDefault=true, sortkey 2_1_0)
        AddSimulatedSensor("CPU Package", sensorIndex++, SensorType.Power, model.MinPackagePower, model.MaxPackagePower, 0.2f, 2.5f, true, "2_1_0");

        // Temperature sensors - matches Amd17Cpu Processor class
        AddSimulatedSensor("CPU Package (Tctl/Tdie)", sensorIndex++, SensorType.Temperature, model.MinPackageTemperature, model.MaxPackageTemperature, 0.12f, 1.2f, true, "3_1_2");

        // Voltage sensors - matches Amd17Cpu Processor class
        AddSimulatedSensor("CPU Core (SVI2 TFN)", sensorIndex++, SensorType.Voltage, 0.9f, 1.35f, 0.22f, 0.02f, false, "4_1_0");
        AddSimulatedSensor("CPU SoC (SVI2 TFN)", sensorIndex, SensorType.Voltage, 0.85f, 1.2f, 0.18f, 0.02f, false, "4_1_1");
    }
}

/// <summary>
/// Simulates an Intel hybrid processor with the sensor set of IntelCpu.cs: per-core Clock (IsPresentationDefault=true)
/// and effective Clock, per-core Temperature, CPU Package Temperature/Power, Bus Speed, Voltage.
/// The core names carry the "P"/"E"/"LPE" suffix like the real driver.
/// </summary>
internal sealed class SimulatedIntelCpu : SimulatedCpuBase
{
    public SimulatedIntelCpu(SimulatedCpuModel model, int processorIndex, ISettings settings)
        : base(model,
            new Identifier("intelcpu-sim", processorIndex.ToString(CultureInfo.InvariantCulture)),
            settings)
    {
        int sensorIndex = 0;
        SimulatedCoreClass fastest = model.FastestCore;

        // Bus Speed - matches IntelCpu.cs (sortkey 0_0)
        AddSimulatedSensor("Bus Speed", sensorIndex++, SensorType.Clock, 99f, 101f, 0.02f, 0.5f, false, "0_0");

        // Per-core Clock sensors - matches IntelCpu.cs (IsPresentationDefault=true, sortkey 0_1_0_{core})
        for (int core = 0; core < model.CoreCount; core++)
        {
            SimulatedCoreClass coreClass = model.Cores[core];
            AddSimulatedSensor(model.GetCoreName(core), sensorIndex++, SensorType.Clock, coreClass.MinClock, coreClass.MaxClock, 0.17f, 40f, true, $"0_1_0_{core}");
        }

        // Per-core effective Clock sensors - matches IntelCpu.cs (sortkey 0_1_1_{core})
        for (int core = 0; core < model.CoreCount; core++)
        {
            SimulatedCoreClass coreClass = model.Cores[core];
            AddSimulatedSensor($"{model.GetCoreName(core)} (Effective)", sensorIndex++, SensorType.Clock, Math.Max(0f, coreClass.MinClock - 400f), coreClass.MaxClock, 0.18f, 45f, false, $"0_1_1_{core}");
        }

        // Aggregate Clock sensors - matches IntelCpu.cs
        AddSimulatedSensor("CPU Effective", sensorIndex++, SensorType.Clock, Math.Max(0f, model.MinClock - 200f), model.MaxClock, 0.19f, 40f, false, "0_1_2");
        AddSimulatedSensor("CPU Max Effective", sensorIndex++, SensorType.Clock, fastest.MinClock + 200f, fastest.MaxClock + 200f, 0.2f, 45f, false, "0_1_3");
        AddSimulatedSensor("CPU Max", sensorIndex++, SensorType.Clock, fastest.MinClock + 200f, fastest.MaxClock + 200f, 0.19f, 45f, false, "0_2");

        // Per-core Load sensors
        for (int core = 0; core < model.CoreCount; core++)
            AddSimulatedSensor(model.GetCoreName(core), sensorIndex++, SensorType.Load, 1f, 100f, 0.3f, 2.4f, false, $"1_1_{core + 1}");

        // Aggregate Load sensors
        AddSimulatedSensor("CPU Total", sensorIndex++, SensorType.Load, 3f, 96f, 0.24f, 2.2f, true, "1_2_1");
        AddSimulatedSensor("CPU Max", sensorIndex++, SensorType.Load, 6f, 100f, 0.34f, 2.8f, false, "1_2_2");

        // Power sensors - matches IntelCpu.cs (multiple power domains)
        AddSimulatedSensor("CPU Package", sensorIndex++, SensorType.Power, model.MinPackagePower, model.MaxPackagePower, 0.21f, 3f, true, "2_0");
        AddSimulatedSensor("CPU Cores", sensorIndex++, SensorType.Power, Math.Max(0f, model.MinPackagePower - 10f), model.MaxPackagePower - 20f, 0.21f, 2.5f, false, "2_1");
        AddSimulatedSensor("CPU Graphics", sensorIndex++, SensorType.Power, 0f, 30f, 0.15f, 1f, false, "2_2");
        AddSimulatedSensor("CPU Memory", sensorIndex++, SensorType.Power, 2f, 20f, 0.12f, 0.5f, false, "2_3");

        // Temperature sensors - matches IntelCpu.cs
        AddSimulatedSensor("Core Max", sensorIndex++, SensorType.Temperature, model.MinPackageTemperature, model.MaxCoreTemperature, 0.14f, 1.3f, false, "3_0");
        AddSimulatedSensor("Core Average", sensorIndex++, SensorType.Temperature, model.MinPackageTemperature - 2f, model.MaxCoreTemperature - 7f, 0.13f, 1.1f, false, "3_1");

        // Per-core Temperature sensors - matches IntelCpu.cs (sortkey 3_2_{core}); E and LPE cores run cooler than P cores
        for (int core = 0; core < model.CoreCount; core++)
        {
            SimulatedCoreClass coreClass = model.Cores[core];
            AddSimulatedSensor(model.GetCoreName(core), sensorIndex++, SensorType.Temperature, 28f, coreClass.MaxTemperature, 0.14f, 1.2f, false, $"3_2_{core}");
        }

        AddSimulatedSensor("CPU Package", sensorIndex++, SensorType.Temperature, model.MinPackageTemperature, model.MaxPackageTemperature, 0.13f, 1.3f, true, "3_3");

        // Voltage sensors - matches IntelCpu.cs
        AddSimulatedSensor("CPU Core", sensorIndex, SensorType.Voltage, 0.85f, 1.4f, 0.23f, 0.02f, false, "4_0");
    }
}
