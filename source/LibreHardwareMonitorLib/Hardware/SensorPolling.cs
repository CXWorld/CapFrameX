// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using CapFrameX.Monitoring.Contracts;

namespace LibreHardwareMonitor.Hardware;

internal static class SensorPolling
{
    internal static bool ShouldEvaluate(ISensorConfig sensorConfig, ISensor sensor)
    {
        return sensor != null &&
               (sensorConfig == null || sensorConfig.GetSensorEvaluate(sensor.Identifier.ToString()));
    }

    internal static bool ShouldEvaluateAny(ISensorConfig sensorConfig, IEnumerable<ISensor> sensors)
    {
        if (sensors == null)
            return false;

        bool evaluate = false;

        // Do not short-circuit. GetSensorEvaluate deliberately permits the first read of
        // every sensor, so every identifier must be registered in the same update cycle.
        foreach (ISensor sensor in sensors)
            evaluate |= ShouldEvaluate(sensorConfig, sensor);

        return evaluate;
    }
}
