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

internal sealed class SimulatedAmdGpu : SimulatedGpuBase
{
    public SimulatedAmdGpu(int index, ISettings settings)
        : base("AMD Radeon RX 7900 XTX (Simulated)",
            new Identifier("gpu-amd-sim", index.ToString(CultureInfo.InvariantCulture)),
            $"SIM-AMD-{index}",
            settings)
    {
        IsDiscreteGpu = true;
        AddSimulatedSensor("GPU Core", 0, SensorType.Load, 1f, 99f, 0.32f, 2.5f, true, "1_1_1");
        AddSimulatedSensor("GPU Memory", 1, SensorType.Load, 1f, 95f, 0.28f, 2.0f, false, "1_1_2");
        AddSimulatedSensor("GPU Core", 2, SensorType.Clock, 500f, 2800f, 0.2f, 25f, true, "2_1_1");
        AddSimulatedSensor("GPU Memory", 3, SensorType.Clock, 500f, 2400f, 0.18f, 15f, false, "2_1_2");
        AddSimulatedSensor("GPU Core", 4, SensorType.Temperature, 30f, 88f, 0.15f, 1.2f, true, "3_1_1");
        AddSimulatedSensor("GPU Memory", 5, SensorType.Temperature, 34f, 90f, 0.14f, 1.1f, false, "3_1_2");
        AddSimulatedSensor("GPU Hot Spot", 6, SensorType.Temperature, 36f, 96f, 0.16f, 1.3f, false, "3_1_3");
        AddSimulatedSensor("GPU Power", 7, SensorType.Power, 40f, 350f, 0.22f, 3f, false, "4_1_1");
        AddSimulatedSensor("GPU Fan", 8, SensorType.Fan, 0f, 2100f, 0.28f, 30f, false, "5_1_1");
        AddSimulatedSensor("GPU Memory Used", 9, SensorType.Data, 1f, 20f, 0.1f, 0.4f, false, "6_1_1");
    }

    public override HardwareType HardwareType => HardwareType.GpuAmd;
}

internal sealed class SimulatedNvidiaGpu : SimulatedGpuBase
{
    public SimulatedNvidiaGpu(int index, ISettings settings)
        : base("NVIDIA GeForce RTX 4090 (Simulated)",
            new Identifier("gpu-nvidia-sim", index.ToString(CultureInfo.InvariantCulture)),
            $"SIM-NVIDIA-{index}",
            settings)
    {
        IsDiscreteGpu = true;
        AddSimulatedSensor("GPU Core", 0, SensorType.Load, 2f, 100f, 0.34f, 2.2f, true, "1_1_1");
        AddSimulatedSensor("GPU Memory Controller", 1, SensorType.Load, 1f, 90f, 0.3f, 2.0f, false, "1_1_2");
        AddSimulatedSensor("GPU Video Engine", 2, SensorType.Load, 0f, 80f, 0.26f, 1.8f, false, "1_1_3");
        AddSimulatedSensor("GPU Bus", 3, SensorType.Load, 0f, 70f, 0.22f, 1.6f, false, "1_1_4");
        AddSimulatedSensor("GPU Core", 4, SensorType.Clock, 450f, 2850f, 0.21f, 20f, true, "2_1_1");
        AddSimulatedSensor("GPU Memory", 5, SensorType.Clock, 500f, 2300f, 0.19f, 12f, false, "2_1_2");
        AddSimulatedSensor("GPU Core", 6, SensorType.Temperature, 28f, 86f, 0.14f, 1.1f, true, "3_1_1");
        AddSimulatedSensor("GPU Hot Spot", 7, SensorType.Temperature, 32f, 95f, 0.16f, 1.2f, false, "3_1_2");
        AddSimulatedSensor("GPU Memory Junction", 8, SensorType.Temperature, 34f, 96f, 0.15f, 1.2f, false, "3_1_3");
        AddSimulatedSensor("GPU Power", 9, SensorType.Power, 35f, 450f, 0.23f, 3.5f, false, "4_1_1");
        AddSimulatedSensor("GPU Fan", 10, SensorType.Fan, 0f, 2000f, 0.26f, 25f, false, "5_1_1");
        AddSimulatedSensor("GPU Memory Used", 11, SensorType.Data, 1f, 24f, 0.1f, 0.5f, false, "6_1_1");
    }

    public override HardwareType HardwareType => HardwareType.GpuNvidia;
}

internal sealed class SimulatedIntelGclGpu : SimulatedGpuBase
{
    public SimulatedIntelGclGpu(int index, ISettings settings)
        : base("Intel Arc A770 (Simulated)",
            new Identifier("gpu-intel-gcl-sim", index.ToString(CultureInfo.InvariantCulture)),
            $"SIM-INTEL-GCL-{index}",
            settings)
    {
        IsDiscreteGpu = true;
        AddSimulatedSensor("GPU Core", 0, SensorType.Temperature, 30f, 84f, 0.13f, 1.1f, true, "3_1_1");
        AddSimulatedSensor("GPU Memory", 1, SensorType.Temperature, 32f, 86f, 0.13f, 1.1f, false, "3_1_2");
        AddSimulatedSensor("GPU TDP", 2, SensorType.Power, 20f, 200f, 0.18f, 2.2f, false, "4_1_1");
        AddSimulatedSensor("GPU TBP", 3, SensorType.Power, 25f, 230f, 0.2f, 2.5f, false, "4_1_2");
        AddSimulatedSensor("GPU VRAM", 4, SensorType.Power, 5f, 45f, 0.16f, 1.0f, false, "4_1_3");
        AddSimulatedSensor("GPU Core", 5, SensorType.Clock, 300f, 2300f, 0.2f, 18f, true, "2_1_1");
        AddSimulatedSensor("GPU Memory", 6, SensorType.Clock, 500f, 2000f, 0.17f, 10f, false, "2_1_2");
        AddSimulatedSensor("GPU Core", 7, SensorType.Voltage, 0.6f, 1.1f, 0.18f, 0.02f, false, "5_1_1");
        AddSimulatedSensor("GPU Memory", 8, SensorType.Voltage, 0.7f, 1.2f, 0.16f, 0.02f, false, "5_1_2");
        AddSimulatedSensor("GPU Core", 9, SensorType.Load, 2f, 98f, 0.3f, 2f, true, "1_1_1");
        AddSimulatedSensor("GPU Computing", 10, SensorType.Load, 0f, 95f, 0.26f, 1.8f, false, "1_1_2");
        AddSimulatedSensor("GPU Media Engine", 11, SensorType.Load, 0f, 90f, 0.24f, 1.6f, false, "1_1_3");
        AddSimulatedSensor("GPU Memory Read", 12, SensorType.Throughput, 0f, 500f, 0.22f, 8f, false, "6_1_1");
        AddSimulatedSensor("GPU Memory Write", 13, SensorType.Throughput, 0f, 450f, 0.22f, 8f, false, "6_1_2");
        AddSimulatedSensor("GPU Fan", 14, SensorType.Fan, 0f, 1800f, 0.25f, 20f, false, "5_1_1");
    }

    public override HardwareType HardwareType => HardwareType.GpuIntel;
}

internal sealed class SimulatedIntelD3DGpu : SimulatedGpuBase
{
    public SimulatedIntelD3DGpu(int index, ISettings settings)
        : base("Intel UHD 770 (Simulated)",
            new Identifier("gpu-intel-d3d-sim", index.ToString(CultureInfo.InvariantCulture)),
            $"SIM-VEN_8086&DEV_7D55-{index}",
            settings)
    {
        IsDiscreteGpu = false;
        AddSimulatedSensor("GPU Power", 0, SensorType.Power, 8f, 65f, 0.18f, 1.2f, false, "4_1_1");
        AddSimulatedSensor("D3D Dedicated Memory Used", 1, SensorType.SmallData, 0.2f, 1.5f, 0.1f, 0.1f, false, "6_1_1");
        AddSimulatedSensor("D3D Shared Memory Used", 2, SensorType.SmallData, 0.5f, 6f, 0.1f, 0.2f, false, "6_1_2");
        AddSimulatedSensor("D3D Shared Memory Free", 3, SensorType.SmallData, 0.5f, 6f, 0.1f, 0.2f, false, "6_1_3");
        AddSimulatedSensor("D3D Shared Memory Total", 4, SensorType.SmallData, 6f, 12f, 0.05f, 0.1f, false, "6_1_4");
        AddSimulatedSensor("GPU Core", 5, SensorType.Load, 1f, 95f, 0.28f, 2f, true, "1_1_1");
        AddSimulatedSensor("GPU Core", 6, SensorType.Clock, 300f, 1550f, 0.18f, 12f, true, "2_1_1");
        AddSimulatedSensor("GPU Core", 7, SensorType.Temperature, 32f, 78f, 0.12f, 1.0f, true, "3_1_1");
    }

    public override HardwareType HardwareType => HardwareType.GpuIntel;
}
