// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using CapFrameX.Monitoring.Contracts;

namespace LibreHardwareMonitor.Hardware.Memory.Sensors;

/// <summary>
/// A DIMM temperature. Every read is an SMBus transaction that PawnIO spends spinning in the kernel,
/// while the temperature changes over seconds, so the throttle limits the reads and the sensor keeps
/// its value in between. readTemperature returns null when a read fails.
/// </summary>
internal class SpdThermalSensor(string name, int index, SensorType sensorType, Hardware hardware, ISettings settings, Func<float?> readTemperature, RefreshThrottle throttle, ISensorConfig sensorConfig)
    : Sensor(name, index, sensorType, hardware, settings)
{
    public bool UpdateSensor()
    {
        // The selection comes first: an unselected sensor must not use up the throttle, so it reads
        // at once when it is selected again.
        if (!SensorPolling.ShouldEvaluate(sensorConfig, this))
            return false;

        if (!throttle.TryBegin())
            return false;

        float? temperature = readTemperature();
        if (temperature == null)
            return false;

        Value = temperature;

        return true;
    }
}
