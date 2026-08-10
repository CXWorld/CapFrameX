// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Text;
using LibreHardwareMonitor.Interop;

namespace LibreHardwareMonitor.Hardware.Storage;

/// <summary>
/// An NVMe storage device, monitored through its SMART health log.
/// </summary>
public sealed class NVMeGeneric : AbstractStorage
{
    private const ulong Scale = 1000000;
    private const ulong Units = 512;
    private readonly NVMeInfo _info;
    private readonly List<NVMeSensor> _sensors = new();

    /// <summary>
    /// Gets the SMART data.
    /// </summary>
    public NVMeSmart Smart { get; }

    private NVMeGeneric(StorageInfo storageInfo, NVMeInfo info, int index, ISettings settings)
        : base(storageInfo, info.Model, info.Revision, "nvme", index, settings)
    {
        Smart = new NVMeSmart(storageInfo);
        _info = info;
        CreateSensors();
    }

    private static NVMeInfo GetDeviceInfo(StorageInfo storageInfo)
    {
        using var smart = new NVMeSmart(storageInfo, enableHealthReader: false);
        return smart.GetInfo();
    }

    internal static AbstractStorage CreateInstance(StorageInfo storageInfo, ISettings settings)
    {
        NVMeInfo nvmeInfo = GetDeviceInfo(storageInfo);
        return nvmeInfo == null ? null : new NVMeGeneric(storageInfo, nvmeInfo, storageInfo.Index, settings);
    }

    /// <inheritdoc />
    protected override void CreateSensors()
    {
        NVMeHealthInfo log = Smart.GetInitialHealthInfo();
        if (log != null)
        {
            AddSensor("Temperature", 0, false, SensorType.Temperature, health => health.Temperature, $"{Index}_1_0");
            AddSensor("Available Spare", 1, false, SensorType.Level, health => health.AvailableSpare, $"{Index}_3_1");
            AddSensor("Available Spare Threshold", 2, false, SensorType.Level, health => health.AvailableSpareThreshold, $"{Index}_3_2");
            AddSensor("Lifetime Used", 3, false, SensorType.Level, health => health.PercentageUsed, $"{Index}_3_3");
            AddSensor("Data Read", 4, false, SensorType.Data, health => UnitsToData(health.DataUnitRead), $"{Index}_3_4");
            AddSensor("Data Written", 5, false, SensorType.Data, health => UnitsToData(health.DataUnitWritten), $"{Index}_3_5");

            int sensorIdx = 6;
            for (int i = 0; i < log.TemperatureSensors.Length; i++)
            {
                int idx = i;
                if (log.TemperatureSensors[idx] > short.MinValue)
                {
                    AddSensor("Temperature " + (idx + 1), sensorIdx, false, SensorType.Temperature, health => health.TemperatureSensors[idx], $"{Index}_1_{idx + 1}");
                    sensorIdx++;
                }
            }
        }

        base.CreateSensors();
    }

    private void AddSensor(string name, int index, bool defaultHidden, SensorType sensorType, GetSensorValue getValue, string presentationSortKey)
    {
        var sensor = new NVMeSensor(WithDrivePrefix(name), index, defaultHidden, sensorType, this, _settings, getValue)
        {
            Value = 0,
            PresentationSortKey = presentationSortKey
        };
        ActivateSensor(sensor);
        _sensors.Add(sensor);
    }

    private static float UnitsToData(ulong u)
    {
        // one unit is 512 * 1000 bytes, return in GB (not GiB)
        return Units * u / Scale;
    }

    /// <inheritdoc />
    protected override void UpdateSensors()
    {
        Smart.RequestHealthInfo();
        if (!Smart.TryGetHealthInfo(out NVMeHealthInfo health, out _))
            return;

        foreach (NVMeSensor sensor in _sensors)
            sensor.Update(health);
    }

    /// <inheritdoc />
    protected override void GetReport(StringBuilder r)
    {
        if (_info == null)
            return;

        r.AppendLine("PCI Vendor ID: 0x" + _info.VID.ToString("x04"));
        if (_info.VID != _info.SSVID)
            r.AppendLine("PCI Subsystem Vendor ID: 0x" + _info.VID.ToString("x04"));

        r.AppendLine("IEEE OUI Identifier: 0x" + _info.IEEE[2].ToString("x02") + _info.IEEE[1].ToString("x02") + _info.IEEE[0].ToString("x02"));
        r.AppendLine("Total NVM Capacity: " + _info.TotalCapacity);
        r.AppendLine("Unallocated NVM Capacity: " + _info.UnallocatedCapacity);
        r.AppendLine("Controller ID: " + _info.ControllerId);
        r.AppendLine("Number of Namespaces: " + _info.NumberNamespaces);

        Smart.RequestHealthInfo();
        if (!Smart.TryGetHealthInfo(out NVMeHealthInfo health, out System.TimeSpan age))
            return;

        r.AppendLine("Health Data Age: " + age.TotalSeconds.ToString("F1") + " seconds");

        r.AppendLine("Temperature: " + health.Temperature + " Celsius");
        r.AppendLine("Available Spare: " + health.AvailableSpare + "%");
        r.AppendLine("Available Spare Threshold: " + health.AvailableSpareThreshold + "%");
        r.AppendLine("Percentage Used: " + health.PercentageUsed + "%");
        r.AppendLine("Data Units Read: " + health.DataUnitRead);
        r.AppendLine("Data Units Written: " + health.DataUnitWritten);
        r.AppendLine("Host Read Commands: " + health.HostReadCommands);
        r.AppendLine("Host Write Commands: " + health.HostWriteCommands);
        r.AppendLine("Controller Busy Time: " + health.ControllerBusyTime);
        r.AppendLine("Power Cycles: " + health.PowerCycle);
        r.AppendLine("Power On Hours: " + health.PowerOnHours);
        r.AppendLine("Unsafe Shutdowns: " + health.UnsafeShutdowns);
        r.AppendLine("Media Errors: " + health.MediaErrors);
        r.AppendLine("Number of Error Information Log Entries: " + health.ErrorInfoLogEntryCount);
        r.AppendLine("Warning Composite Temperature Time: " + health.WarningCompositeTemperatureTime);
        r.AppendLine("Critical Composite Temperature Time: " + health.CriticalCompositeTemperatureTime);
        for (int i = 0; i < health.TemperatureSensors.Length; i++)
        {
            if (health.TemperatureSensors[i] > short.MinValue)
                r.AppendLine("Temperature Sensor " + (i + 1) + ": " + health.TemperatureSensors[i] + " Celsius");
        }
    }

    /// <inheritdoc />
    public override void Close()
    {
        Smart?.Close();

        base.Close();
    }

    private delegate float GetSensorValue(NVMeHealthInfo health);

    private class NVMeSensor : Sensor
    {
        private readonly GetSensorValue _getValue;

        public NVMeSensor(string name, int index, bool defaultHidden, SensorType sensorType, Hardware hardware, ISettings settings, GetSensorValue getValue)
            : base(name, index, defaultHidden, sensorType, hardware, null, settings)
        {
            _getValue = getValue;
        }

        public void Update(NVMeHealthInfo health)
        {
            float v = _getValue(health);
            if (SensorType == SensorType.Temperature && v is < -1000 or > 1000)
                return;

            Value = v;
        }
    }
}
