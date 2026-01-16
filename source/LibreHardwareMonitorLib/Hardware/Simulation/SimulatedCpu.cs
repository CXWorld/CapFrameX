using System;
using System.Collections.Generic;
using System.Globalization;

namespace LibreHardwareMonitor.Hardware.Simulation;

internal abstract class SimulatedCpuBase : Hardware
{
    private readonly List<SimulatedSensorSlot> _simulatedSensors = new();
    private readonly DateTime _startTime = DateTime.UtcNow;
    private readonly Random _random;

    protected SimulatedCpuBase(string name, Identifier identifier, ISettings settings)
        : base(name, identifier, settings)
    {
        _random = new Random(identifier.ToString().GetHashCode());
    }

    public override HardwareType HardwareType => HardwareType.Cpu;

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

internal sealed class SimulatedAmd17Cpu : SimulatedCpuBase
{
    private const int CoreCount = 16;

    public SimulatedAmd17Cpu(int processorIndex, ISettings settings)
        : base("AMD Ryzen 9 7950X (Simulated)",
            new Identifier("amdcpu-sim", processorIndex.ToString(CultureInfo.InvariantCulture)),
            settings)
    {
        AddSimulatedSensor("CPU Total", 0, SensorType.Load, 2f, 98f, 0.25f, 2.5f, true, "1_2_1");
        AddSimulatedSensor("CPU Max", 1, SensorType.Load, 5f, 100f, 0.35f, 3f, false, "1_2_2");

        int sensorIndex = 2;
        for (int core = 0; core < CoreCount; core++)
        {
            string coreLabel = $"Core #{core + 1}";
            AddSimulatedSensor(coreLabel, sensorIndex++, SensorType.Load, 1f, 100f, 0.28f, 2.2f, false, $"1_1_{core + 1}");
        }

        for (int core = 0; core < CoreCount; core++)
        {
            string coreLabel = $"Core #{core + 1}";
            AddSimulatedSensor(coreLabel, sensorIndex++, SensorType.Clock, 2800f, 5450f, 0.18f, 35f, false, $"2_1_{core + 1}");
        }

        AddSimulatedSensor("CPU Max", sensorIndex++, SensorType.Clock, 3000f, 5600f, 0.2f, 40f, false, "2_2_1");

        AddSimulatedSensor("CPU Package", sensorIndex++, SensorType.Temperature, 32f, 88f, 0.12f, 1.2f, true, "3_1_1");
        AddSimulatedSensor("CPU Package", sensorIndex++, SensorType.Power, 25f, 170f, 0.2f, 2.5f, false, "4_1_1");
        AddSimulatedSensor("CPU Core", sensorIndex, SensorType.Voltage, 0.9f, 1.35f, 0.22f, 0.02f, false, "5_1_1");
    }
}

internal sealed class SimulatedIntelCpu : SimulatedCpuBase
{
    private const int PerformanceCoreCount = 8;
    private const int EfficientCoreCount = 16;
    private const int CoreCount = 24;

    public SimulatedIntelCpu(int processorIndex, ISettings settings)
        : base("Intel Core i9-14900K (Simulated)",
            new Identifier("intelcpu-sim", processorIndex.ToString(CultureInfo.InvariantCulture)),
            settings)
    {
        AddSimulatedSensor("CPU Total", 0, SensorType.Load, 3f, 96f, 0.24f, 2.2f, true, "1_2_1");
        AddSimulatedSensor("CPU Max", 1, SensorType.Load, 6f, 100f, 0.34f, 2.8f, false, "1_2_2");

        int sensorIndex = 2;
        for (int core = 0; core < CoreCount; core++)
        {
            string coreLabel = GetIntelCoreLabel(core);
            AddSimulatedSensor(coreLabel, sensorIndex++, SensorType.Load, 1f, 100f, 0.3f, 2.4f, false, $"1_1_{core + 1}");
        }

        for (int core = 0; core < CoreCount; core++)
        {
            string coreLabel = GetIntelCoreLabel(core);
            AddSimulatedSensor(coreLabel, sensorIndex++, SensorType.Clock, 2400f, 6000f, 0.17f, 40f, false, $"0_1_0_{core}");
            AddSimulatedSensor($"{coreLabel} (Effective)", sensorIndex++, SensorType.Clock, 2000f, 6000f, 0.18f, 45f, false, $"0_1_1_{core}");
        }

        AddSimulatedSensor("CPU Effective", sensorIndex++, SensorType.Clock, 2200f, 6000f, 0.19f, 40f, false, "0_1_2");
        AddSimulatedSensor("CPU Max Effective", sensorIndex++, SensorType.Clock, 2600f, 6200f, 0.2f, 45f, false, "0_1_3");
        AddSimulatedSensor("CPU Max", sensorIndex++, SensorType.Clock, 2600f, 6200f, 0.19f, 45f, false, "0_2");

        AddSimulatedSensor("CPU Package", sensorIndex++, SensorType.Temperature, 30f, 92f, 0.13f, 1.3f, true, "3_1_1");
        AddSimulatedSensor("CPU Package", sensorIndex++, SensorType.Power, 30f, 200f, 0.21f, 3f, false, "4_1_1");
        AddSimulatedSensor("CPU Core", sensorIndex, SensorType.Voltage, 0.85f, 1.4f, 0.23f, 0.02f, false, "5_1_1");
    }

    private static string GetIntelCoreLabel(int coreIndex)
    {
        string coreType = coreIndex < PerformanceCoreCount ? "P" : "E";
        return $"Core #{coreIndex + 1} {coreType}";
    }
}
