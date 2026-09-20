// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using CapFrameX.Monitoring.Contracts;
using RAMSPDToolkit.SPD.Interfaces;

namespace LibreHardwareMonitor.Hardware.Memory.Sensors;

internal class SpdThermalSensor(string name, int index, SensorType sensorType, Hardware hardware, ISettings settings, IThermalSensor thermalSensor, ISensorConfig sensorConfig)
    : Sensor(name, index, sensorType, hardware, settings)
{
    public bool UpdateSensor()
    {
        if (!SensorPolling.ShouldEvaluate(sensorConfig, this))
            return false;

        if (!thermalSensor.UpdateTemperature())
            return false;

        Value = thermalSensor.Temperature;

        return true;
    }
}
