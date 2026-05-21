using System;
using System.Collections.Generic;

namespace LibreHardwareMonitor.Hardware.Simulation;

internal sealed class SimulatedSensorSlot
{
    public SimulatedSensorSlot(Sensor sensor, float min, float max, float speed, float phase, float noise)
    {
        Sensor = sensor;
        Min = min;
        Max = max;
        Speed = speed;
        Phase = phase;
        Noise = noise;
    }

    public Sensor Sensor { get; }

    public float Min { get; }

    public float Max { get; }

    public float Speed { get; }

    public float Phase { get; }

    public float Noise { get; }
}

internal static class SimulationHelpers
{
    private const float TwoPi = (float)(Math.PI * 2.0);

    public static float NextPhase(Random random)
    {
        return (float)(random.NextDouble() * TwoPi);
    }

    public static void UpdateSensors(List<SimulatedSensorSlot> sensors, DateTime startTime, Random random)
    {
        double elapsed = (DateTime.UtcNow - startTime).TotalSeconds;

        foreach (SimulatedSensorSlot slot in sensors)
        {
            float normalized = (float)((Math.Sin((elapsed * slot.Speed) + slot.Phase) + 1.0) * 0.5);
            float value = slot.Min + ((slot.Max - slot.Min) * normalized);

            if (slot.Noise > 0)
            {
                float noise = (float)((random.NextDouble() * 2.0 - 1.0) * slot.Noise);
                value += noise;
            }

            if (value < slot.Min)
                value = slot.Min;
            else if (value > slot.Max)
                value = slot.Max;

            slot.Sensor.Value = value;
        }
    }
}
