// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.

using System.Collections.Generic;
using System.Linq;

namespace LibreHardwareMonitor.Hardware.Cpu;

internal sealed class CoreTypeClockSensor
{
    private readonly ISensor[] _sources;

    private CoreTypeClockSensor(Sensor sensor, ISensor[] sources)
    {
        Sensor = sensor;
        _sources = sources;
    }

    internal Sensor Sensor { get; }

    internal static CoreTypeClockSensor[] Create(
        bool isHybrid,
        Hardware hardware,
        ISettings settings,
        IEnumerable<(CpuCoreType CoreType, ISensor Sensor)> sources,
        bool effective,
        ref int sensorIndex)
    {
        if (!isHybrid || sources == null)
            return [];

        List<CoreTypeClockSensor> sensors = new();
        foreach (var group in sources.Where(source => source.Sensor != null)
                     .GroupBy(source => source.CoreType).OrderBy(group => group.Key))
        {
            string label = group.Key switch
            {
                CpuCoreType.PerformanceCore => "P",
                CpuCoreType.EfficiencyCore => "E",
                CpuCoreType.LowPowerEfficiencyCore => "LPE",
                CpuCoreType.DenseCore => "D",
                CpuCoreType.LowPowerCore => "LP",
                _ => null
            };

            if (label == null)
                continue;

            string name = effective ? $"CPU {label} Max Effective" : $"CPU {label} Max";
            Sensor sensor = new(name, sensorIndex++, SensorType.Clock, hardware, settings)
            {
                PresentationSortKey = $"0_2_1_{(int)group.Key}_{(effective ? 1 : 0)}"
            };
            sensors.Add(new CoreTypeClockSensor(sensor, group.Select(source => source.Sensor).ToArray()));
        }

        return sensors.ToArray();
    }

    internal void Update()
    {
        float? max = null;
        foreach (ISensor source in _sources)
        {
            float? value = source.Value;
            if (value.HasValue && float.IsFinite(value.Value) && (!max.HasValue || value.Value > max.Value))
                max = value.Value;
        }

        // This is the maximum of the current sample, not a historical peak.
        Sensor.Value = max;
    }
}
