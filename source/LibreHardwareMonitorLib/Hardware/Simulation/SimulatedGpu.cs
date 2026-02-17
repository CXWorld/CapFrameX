using System;
using System.Collections.Generic;
using System.Globalization;
using LibreHardwareMonitor.Hardware.Gpu;

namespace LibreHardwareMonitor.Hardware.Simulation;

internal abstract class SimulatedGpuBase : GenericGpu
{
    private readonly List<SimulatedSensorSlot> _simulatedSensors = new();
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly Random _random;
    private readonly string _deviceId;

    protected SimulatedGpuBase(string name, Identifier identifier, string deviceId, ISettings settings)
        : base(name, identifier, settings)
    {
        _deviceId = deviceId;
        _random = new Random(identifier.ToString().GetHashCode());
    }

    public override string DeviceId => _deviceId;

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

    public override string GetDriverVersion()
    {
        return "Simulated";
    }
}

/// <summary>
/// Simulates AMD Radeon RX 7900 XTX.
/// Sensors match AmdGpu.cs: Temperature (Core, Memory, Hot Spot, Intake),
/// Clock (Core, Memory), Load (Core), Power (GPU, TBP), Voltage, Fan, Data (Dedicated, Shared).
/// </summary>
internal sealed class SimulatedAmdGpu : SimulatedGpuBase
{
    public SimulatedAmdGpu(int index, ISettings settings)
        : base("AMD Radeon RX 7900 XTX (Simulated)",
            new Identifier("gpu-amd-sim", index.ToString(CultureInfo.InvariantCulture)),
            $"SIM-AMD-{index}",
            settings)
    {
        IsDiscreteGpu = true;

        // Clock sensors (category 0) - matches AmdGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Clock, 500f, 2800f, 0.2f, 25f, true, $"{index}_0_0");
        AddSimulatedSensor("GPU Memory", 1, SensorType.Clock, 500f, 2400f, 0.18f, 15f, true, $"{index}_0_1");

        // Load sensors (category 1) - matches AmdGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Load, 1f, 99f, 0.32f, 2.5f, true, $"{index}_1_0");

        // Temperature sensors (category 2) - matches AmdGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Temperature, 30f, 88f, 0.15f, 1.2f, true, $"{index}_2_0");
        AddSimulatedSensor("GPU Memory", 1, SensorType.Temperature, 34f, 90f, 0.14f, 1.1f, false, $"{index}_2_1");
        AddSimulatedSensor("GPU Hot Spot", 2, SensorType.Temperature, 36f, 96f, 0.16f, 1.3f, false, $"{index}_2_2");
        AddSimulatedSensor("GPU Intake", 3, SensorType.Temperature, 25f, 55f, 0.12f, 1.0f, false, $"{index}_2_3");

        // Power sensors (category 3) - matches AmdGpu.cs
        AddSimulatedSensor("GPU Power", 0, SensorType.Power, 40f, 350f, 0.22f, 3f, true, $"{index}_3_0");
        AddSimulatedSensor("GPU TBP", 1, SensorType.Power, 50f, 400f, 0.22f, 4f, true, $"{index}_3_1");

        // Voltage sensors (category 4) - matches AmdGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Voltage, 0.7f, 1.2f, 0.18f, 0.02f, false, $"{index}_4_0");

        // Fan sensors (category 5) - matches AmdGpu.cs
        AddSimulatedSensor("GPU Fan", 0, SensorType.Fan, 0f, 2100f, 0.28f, 30f, false, $"{index}_5_0");

        // Data sensors (category 6) - matches AmdGpu.cs
        AddSimulatedSensor("GPU Memory Dedicated", 0, SensorType.Data, 1f, 20f, 0.1f, 0.4f, true, $"{index}_6_0");
        AddSimulatedSensor("GPU Memory Shared", 3, SensorType.Data, 0.5f, 8f, 0.08f, 0.3f, false, $"{index}_6_1");
    }

    public override HardwareType HardwareType => HardwareType.GpuAmd;
}

/// <summary>
/// Simulates NVIDIA GeForce RTX 4090.
/// Sensors match NvidiaGpu.cs: Load (Core, Memory Controller, Video Engine, Bus),
/// Clock (Core, Memory), Temperature (Core, Hot Spot, Memory Junction),
/// Power, Voltage, Fan, Data (Dedicated, Shared).
/// </summary>
internal sealed class SimulatedNvidiaGpu : SimulatedGpuBase
{
    public SimulatedNvidiaGpu(int index, ISettings settings)
        : base("NVIDIA GeForce RTX 4090 (Simulated)",
            new Identifier("gpu-nvidia-sim", index.ToString(CultureInfo.InvariantCulture)),
            $"SIM-NVIDIA-{index}",
            settings)
    {
        IsDiscreteGpu = true;

        // Clock sensors - matches NvidiaGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Clock, 450f, 2850f, 0.21f, 20f, true, $"{index}_0_0");
        AddSimulatedSensor("GPU Memory", 1, SensorType.Clock, 500f, 2300f, 0.19f, 12f, false, $"{index}_0_1");

        // Load sensors - matches NvidiaGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Load, 2f, 100f, 0.34f, 2.2f, true, $"{index}_1_0");
        AddSimulatedSensor("GPU Memory Controller", 1, SensorType.Load, 1f, 90f, 0.3f, 2.0f, false, $"{index}_1_1");
        AddSimulatedSensor("GPU Video Engine", 2, SensorType.Load, 0f, 80f, 0.26f, 1.8f, false, $"{index}_1_2");
        AddSimulatedSensor("GPU Bus", 3, SensorType.Load, 0f, 70f, 0.22f, 1.6f, false, $"{index}_1_3");

        // Temperature sensors - matches NvidiaGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Temperature, 28f, 86f, 0.14f, 1.1f, true, $"{index}_2_0");
        AddSimulatedSensor("GPU Hot Spot", 1, SensorType.Temperature, 32f, 95f, 0.16f, 1.2f, false, $"{index}_2_1");
        AddSimulatedSensor("GPU Memory Junction", 2, SensorType.Temperature, 34f, 96f, 0.15f, 1.2f, false, $"{index}_2_2");

        // Power sensors - matches NvidiaGpu.cs
        AddSimulatedSensor("GPU Power", 0, SensorType.Power, 35f, 450f, 0.23f, 3.5f, true, $"{index}_3_0");

        // Voltage sensors - matches NvidiaGpu.cs
        AddSimulatedSensor("GPU Voltage", 0, SensorType.Voltage, 0.65f, 1.1f, 0.18f, 0.02f, false, $"{index}_4_0");

        // Fan sensors - matches NvidiaGpu.cs
        AddSimulatedSensor("GPU Fan", 0, SensorType.Fan, 0f, 2000f, 0.26f, 25f, false, $"{index}_5_0");

        // Data sensors - matches NvidiaGpu.cs (D3D memory)
        AddSimulatedSensor("GPU Memory Dedicated", 0, SensorType.Data, 1f, 24f, 0.1f, 0.5f, true, $"{index}_8_0");
        AddSimulatedSensor("GPU Memory Shared", 1, SensorType.Data, 0.5f, 8f, 0.08f, 0.3f, false, $"{index}_8_1");
    }

    public override HardwareType HardwareType => HardwareType.GpuNvidia;
}

/// <summary>
/// Simulates Intel Arc A770.
/// Sensors match IntelGclGpu.cs: Temperature (Core, Memory), Power (TDP, TBP, VRAM),
/// Clock (Core, Memory), Voltage (Core, Memory), Load (Core, Computing, Media Engine),
/// Throughput (Memory Read/Write), Fan.
/// </summary>
internal sealed class SimulatedIntelGclGpu : SimulatedGpuBase
{
    public SimulatedIntelGclGpu(int index, ISettings settings)
        : base("Intel Arc A770 (Simulated)",
            new Identifier("gpu-intel-gcl-sim", index.ToString(CultureInfo.InvariantCulture)),
            $"SIM-INTEL-GCL-{index}",
            settings)
    {
        IsDiscreteGpu = true;

        // Temperature sensors - matches IntelGclGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Temperature, 30f, 84f, 0.13f, 1.1f, true, $"{index}_2_0");
        AddSimulatedSensor("GPU Memory", 1, SensorType.Temperature, 32f, 86f, 0.13f, 1.1f, false, $"{index}_2_1");

        // Power sensors - matches IntelGclGpu.cs
        AddSimulatedSensor("GPU TDP", 0, SensorType.Power, 20f, 200f, 0.18f, 2.2f, false, $"{index}_3_0");
        AddSimulatedSensor("GPU TBP", 1, SensorType.Power, 25f, 230f, 0.2f, 2.5f, true, $"{index}_3_1");
        AddSimulatedSensor("GPU VRAM", 2, SensorType.Power, 5f, 45f, 0.16f, 1.0f, false, $"{index}_3_2");

        // Clock sensors - matches IntelGclGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Clock, 300f, 2300f, 0.2f, 18f, true, $"{index}_0_0");
        AddSimulatedSensor("GPU Memory", 1, SensorType.Clock, 500f, 2000f, 0.17f, 10f, true, $"{index}_0_1");

        // Voltage sensors - matches IntelGclGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Voltage, 0.6f, 1.1f, 0.18f, 0.02f, false, $"{index}_4_0");
        AddSimulatedSensor("GPU Memory", 1, SensorType.Voltage, 0.7f, 1.2f, 0.16f, 0.02f, false, $"{index}_4_1");

        // Load sensors - matches IntelGclGpu.cs
        AddSimulatedSensor("GPU Core", 0, SensorType.Load, 2f, 98f, 0.3f, 2f, true, $"{index}_1_0");
        AddSimulatedSensor("GPU Computing", 1, SensorType.Load, 0f, 95f, 0.26f, 1.8f, false, $"{index}_1_1");
        AddSimulatedSensor("GPU Media Engine", 2, SensorType.Load, 0f, 90f, 0.24f, 1.6f, false, $"{index}_1_2");

        // Throughput sensors - matches IntelGclGpu.cs
        AddSimulatedSensor("GPU Memory Read", 4, SensorType.Throughput, 0f, 500f, 0.22f, 8f, false, $"{index}_6_0");
        AddSimulatedSensor("GPU Memory Write", 5, SensorType.Throughput, 0f, 450f, 0.22f, 8f, false, $"{index}_6_1");

        // Fan sensors - matches IntelGclGpu.cs
        AddSimulatedSensor("GPU Fan", 0, SensorType.Fan, 0f, 1800f, 0.25f, 20f, false, $"{index}_5_0");
    }

    public override HardwareType HardwareType => HardwareType.GpuIntel;
}

/// <summary>
/// Simulates Intel UHD 770 (integrated graphics).
/// Sensors match IntelD3dGpu.cs: Power (GPU Power), Data (D3D Dedicated/Shared Memory).
/// Real IntelD3dGpu only has D3D memory sensors + optional power sensor for iGPU.
/// </summary>
internal sealed class SimulatedIntelD3DGpu : SimulatedGpuBase
{
    public SimulatedIntelD3DGpu(int index, ISettings settings)
        : base("Intel UHD 770 (Simulated)",
            new Identifier("gpu-intel-d3d-sim", index.ToString(CultureInfo.InvariantCulture)),
            $"SIM-VEN_8086&DEV_7D55-{index}",
            settings)
    {
        IsDiscreteGpu = false;

        // Power sensor - matches IntelD3dGpu.cs (iGPU only)
        AddSimulatedSensor("GPU Power", 0, SensorType.Power, 8f, 65f, 0.18f, 1.2f, true, $"{index}_0");

        // Data sensors - matches IntelD3dGpu.cs (SensorType.Data, not SmallData)
        AddSimulatedSensor("D3D Dedicated Memory Used", 0, SensorType.Data, 0.2f, 1.5f, 0.1f, 0.1f, true, $"{index}_1");
        AddSimulatedSensor("D3D Shared Memory Used", 1, SensorType.Data, 0.5f, 6f, 0.1f, 0.2f, false, $"{index}_2_0");
        AddSimulatedSensor("D3D Shared Memory Free", 2, SensorType.Data, 0.5f, 6f, 0.1f, 0.2f, false, $"{index}_2_1");
        AddSimulatedSensor("D3D Shared Memory Total", 3, SensorType.Data, 6f, 12f, 0.05f, 0.1f, false, $"{index}_2_2");
    }

    public override HardwareType HardwareType => HardwareType.GpuIntel;
}
