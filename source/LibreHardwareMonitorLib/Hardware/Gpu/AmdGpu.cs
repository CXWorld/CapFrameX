// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using LibreHardwareMonitor.Interop;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.Text;

namespace LibreHardwareMonitor.Hardware.Gpu;

internal sealed class AmdGpu : GenericGpu
{
    private readonly uint _adapterIndex;
    private readonly ADLX.AdlxDeviceInfo _deviceInfo;
    private readonly string _d3dDeviceId;

    // Temperature sensors
    private readonly Sensor _temperatureCore;
    private readonly Sensor _temperatureHotSpot;
    private readonly Sensor _temperatureMemory;
    private readonly Sensor _temperatureIntake;

    // Clock sensors
    private readonly Sensor _coreClock;
    private readonly Sensor _memoryClock;
    private readonly Sensor _npuClock;

    // Voltage sensors
    private readonly Sensor _coreVoltage;

    // Load sensors
    private readonly Sensor _coreLoad;
    private readonly Sensor _npuLoad;

    // Fan sensor
    private readonly Sensor _fan;

    // Power sensors
    private readonly Sensor _powerTotal;
    private readonly Sensor _powerTotalBoardPower;

    // Memory sensors (VRAM)
    private readonly Sensor _memoryUsed;
    private readonly Sensor _sharedMemory;

    public AmdGpu(uint adapterIndex, ADLX.AdlxDeviceInfo deviceInfo, ISettings settings)
        : base(deviceInfo.GpuName?.Trim() ?? "AMD GPU",
               new Identifier("gpu-amd", adapterIndex.ToString(CultureInfo.InvariantCulture)),
               settings)
    {
        _adapterIndex = adapterIndex;
        _deviceInfo = deviceInfo;

        // Determine if discrete GPU based on GpuType
        IsDiscreteGpu = deviceInfo.GpuType == (uint)ADLX.GpuType.Discrete;

        int index = (int)adapterIndex;

        // Temperature sensors
        _temperatureCore = new Sensor("GPU Core", 0, SensorType.Temperature, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_2_0" };
        _temperatureMemory = new Sensor("GPU Memory", 1, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{index}_2_1" };
        _temperatureHotSpot = new Sensor("GPU Hot Spot", 2, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{index}_2_2" };
        _temperatureIntake = new Sensor("GPU Intake", 3, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{index}_2_3" };

        // Clock sensors
        _coreClock = new Sensor("GPU Core", 0, SensorType.Clock, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_0_0" };
        _memoryClock = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_0_1" };
        _npuClock = new Sensor("NPU", 2, SensorType.Clock, this, settings)
        { PresentationSortKey = $"{index}_0_2" };

        // Fan sensor
        _fan = new Sensor("GPU Fan", 0, SensorType.Fan, this, settings)
        { PresentationSortKey = $"{index}_5_0" };

        // Voltage sensor
        _coreVoltage = new Sensor("GPU Core", 0, SensorType.Voltage, this, settings)
        { PresentationSortKey = $"{index}_4_0" };

        // Load sensors
        _coreLoad = new Sensor("GPU Core", 0, SensorType.Load, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_1_0" };
        _npuLoad = new Sensor("NPU", 1, SensorType.Load, this, settings)
        { PresentationSortKey = $"{index}_1_1" };

        // Power sensors
        _powerTotal = new Sensor("GPU Power", 0, SensorType.Power, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_3_0" };
        _powerTotalBoardPower = new Sensor("GPU TBP", 1, SensorType.Power, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_3_1" };

        // Memory sensors
        _memoryUsed = new Sensor("GPU Memory Dedicated", 0, SensorType.Data, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_6_0" };
        _sharedMemory = new Sensor("GPU Memory Shared", 3, SensorType.Data, this, settings)
        { PresentationSortKey = $"{index}_6_1" };

        // Activate sensors based on support flags BEFORE calling Update()
        // This ensures sensors are visible in the UI even if the first telemetry call returns no data
        ActivateSensorsFromSupportFlags();
    }

    /// <summary>
    /// Activates sensors based on what metrics the GPU supports, without needing actual telemetry data.
    /// This is called in the constructor to ensure sensors are visible even before telemetry history is available.
    /// </summary>
    private void ActivateSensorsFromSupportFlags()
    {
        ADLX.AdlxTelemetrySupport support = new();
        if (!ADLX.GetTelemetrySupport(_adapterIndex, ref support))
            return;

        // Temperature sensors
        if (support.GpuTemperatureSupported)
            ActivateSensor(_temperatureCore);
        if (support.GpuHotspotTemperatureSupported)
            ActivateSensor(_temperatureHotSpot);
        if (support.GpuMemoryTemperatureSupported)
            ActivateSensor(_temperatureMemory);
        if (support.GpuIntakeTemperatureSupported)
            ActivateSensor(_temperatureIntake);

        // Clock sensors
        if (support.GpuClockSpeedSupported)
            ActivateSensor(_coreClock);
        if (support.GpuVRAMClockSpeedSupported)
            ActivateSensor(_memoryClock);
        if (support.NpuFrequencySupported)
            ActivateSensor(_npuClock);

        // Fan sensor
        if (support.GpuFanSpeedSupported)
            ActivateSensor(_fan);

        // Voltage sensor
        if (support.GpuVoltageSupported)
            ActivateSensor(_coreVoltage);

        // Load sensors
        if (support.GpuUsageSupported)
            ActivateSensor(_coreLoad);
        if (support.NpuActivityLevelSupported)
            ActivateSensor(_npuLoad);

        // Power sensors
        if (support.GpuPowerSupported)
            ActivateSensor(_powerTotal);
        if (support.GpuTotalBoardPowerSupported)
            ActivateSensor(_powerTotalBoardPower);

        // Memory sensors
        if (support.GpuVramSupported)
            ActivateSensor(_memoryUsed);
        if (support.GpuSharedMemorySupported)
            ActivateSensor(_sharedMemory);
    }

    public override string DeviceId => _deviceInfo.DriverPath ?? string.Empty;

    public override HardwareType HardwareType => HardwareType.GpuAmd;

    public override void Update()
    {
        // Get ADLX telemetry data
        ADLX.AdlxTelemetryData telemetry = new();
        if (ADLX.GetTelemetry(_adapterIndex, 1000, ref telemetry))
        {
            // Temperature sensors
            _temperatureCore.Value = telemetry.GpuTemperatureSupported ? (float)telemetry.GpuTemperatureValue : null;
            _temperatureHotSpot.Value = telemetry.GpuHotspotTemperatureSupported ? (float)telemetry.GpuHotspotTemperatureValue : null;
            _temperatureMemory.Value = telemetry.GpuMemoryTemperatureSupported ? (float)telemetry.GpuMemoryTemperatureValue : null;
            _temperatureIntake.Value = telemetry.GpuIntakeTemperatureSupported ? (float)telemetry.GpuIntakeTemperatureValue : null;

            // Clock sensors
            _coreClock.Value = telemetry.GpuClockSpeedSupported ? (float)telemetry.GpuClockSpeedValue : null;
            _memoryClock.Value = telemetry.GpuVRAMClockSpeedSupported ? (float)telemetry.GpuVRAMClockSpeedValue : null;
            _npuClock.Value = telemetry.NpuFrequencySupported ? (float)telemetry.NpuFrequencyValue : null;

            // Fan sensor
            _fan.Value = telemetry.GpuFanSpeedSupported ? (float)telemetry.GpuFanSpeedValue : null;

            // Voltage sensor (ADLX returns voltage in mV, convert to V)
            _coreVoltage.Value = telemetry.GpuVoltageSupported ? (float)(telemetry.GpuVoltageValue / 1000.0) : null;

            // Load sensors
            _coreLoad.Value = telemetry.GpuUsageSupported ? (float)Math.Min(telemetry.GpuUsageValue, 100) : null;
            _npuLoad.Value = telemetry.NpuActivityLevelSupported ? (float)Math.Min(telemetry.NpuActivityLevelValue, 100) : null;

            // Power sensors
            _powerTotal.Value = telemetry.GpuPowerSupported ? (float)telemetry.GpuPowerValue : null;
            _powerTotalBoardPower.Value = telemetry.GpuTotalBoardPowerSupported ? (float)telemetry.GpuTotalBoardPowerValue : null;

            // VRAM usage from ADLX (in MB, convert to GB)
            _memoryUsed.Value = telemetry.GpuVramSupported ? (float)(telemetry.GpuVramValue / 1024.0) : null;

            // Shared memory from ADLX (in MB, convert to GB)
            _sharedMemory.Value = telemetry.GpuSharedMemorySupported ? (float)(telemetry.GpuSharedMemoryValue / 1024.0) : null;
        }
    }

    public override void Close()
    {
        base.Close();
    }

    public override string GetDriverVersion()
    {
        string GetDriverStringFromPath(string path)
        {
            try
            {
                using RegistryKey key = Registry.LocalMachine.OpenSubKey(path);
                if (key != null)
                {
                    var sv = key.GetValue("RadeonSoftwareVersion");
                    var radeonSoftwareVersion = sv == null ? string.Empty : sv.ToString();
                    return $"Adrenalin {radeonSoftwareVersion}";
                }
            }
            catch
            {
                // Ignore registry access errors
            }

            return null;
        }

        const string prefix = @"\Registry\Machine\";

        if (string.IsNullOrEmpty(_deviceInfo.DriverPath))
            return null;

        string subPath = _deviceInfo.DriverPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? _deviceInfo.DriverPath.Substring(prefix.Length)
            : _deviceInfo.DriverPath;

        return GetDriverStringFromPath(subPath);
    }

    public override string GetReport()
    {
        var r = new StringBuilder();

        r.AppendLine("AMD GPU (ADLX)");
        r.AppendLine();

        r.Append("AdapterIndex: ");
        r.AppendLine(_adapterIndex.ToString(CultureInfo.InvariantCulture));
        r.Append("GpuName: ");
        r.AppendLine(_deviceInfo.GpuName);
        r.Append("GpuType: ");
        r.AppendLine(((ADLX.GpuType)_deviceInfo.GpuType).ToString());
        r.Append("VendorId: ");
        r.AppendLine(_deviceInfo.VendorId);
        r.Append("DriverPath: ");
        r.AppendLine(_deviceInfo.DriverPath);
        r.Append("UniqueId: ");
        r.AppendLine(_deviceInfo.Id.ToString(CultureInfo.InvariantCulture));
        r.AppendLine();

        r.AppendLine("ADLX Telemetry Support");
        r.AppendLine();

        ADLX.AdlxTelemetryData telemetry = new();
        if (ADLX.GetTelemetry(_adapterIndex, 1000, ref telemetry))
        {
            r.AppendFormat(" GPU Usage: Supported={0}, Value={1}%{2}", telemetry.GpuUsageSupported, telemetry.GpuUsageValue, Environment.NewLine);
            r.AppendFormat(" GPU Clock Speed: Supported={0}, Value={1} MHz{2}", telemetry.GpuClockSpeedSupported, telemetry.GpuClockSpeedValue, Environment.NewLine);
            r.AppendFormat(" GPU VRAM Clock Speed: Supported={0}, Value={1} MHz{2}", telemetry.GpuVRAMClockSpeedSupported, telemetry.GpuVRAMClockSpeedValue, Environment.NewLine);
            r.AppendFormat(" GPU Temperature: Supported={0}, Value={1}°C{2}", telemetry.GpuTemperatureSupported, telemetry.GpuTemperatureValue, Environment.NewLine);
            r.AppendFormat(" GPU Hotspot Temperature: Supported={0}, Value={1}°C{2}", telemetry.GpuHotspotTemperatureSupported, telemetry.GpuHotspotTemperatureValue, Environment.NewLine);
            r.AppendFormat(" GPU Intake Temperature: Supported={0}, Value={1}°C{2}", telemetry.GpuIntakeTemperatureSupported, telemetry.GpuIntakeTemperatureValue, Environment.NewLine);
            r.AppendFormat(" GPU Memory Temperature: Supported={0}, Value={1}°C{2}", telemetry.GpuMemoryTemperatureSupported, telemetry.GpuMemoryTemperatureValue, Environment.NewLine);
            r.AppendFormat(" GPU Power: Supported={0}, Value={1} W{2}", telemetry.GpuPowerSupported, telemetry.GpuPowerValue, Environment.NewLine);
            r.AppendFormat(" GPU Total Board Power: Supported={0}, Value={1} W{2}", telemetry.GpuTotalBoardPowerSupported, telemetry.GpuTotalBoardPowerValue, Environment.NewLine);
            r.AppendFormat(" GPU Fan Speed: Supported={0}, Value={1} RPM{2}", telemetry.GpuFanSpeedSupported, telemetry.GpuFanSpeedValue, Environment.NewLine);
            r.AppendFormat(" GPU VRAM: Supported={0}, Value={1} MB{2}", telemetry.GpuVramSupported, telemetry.GpuVramValue, Environment.NewLine);
            r.AppendFormat(" GPU Voltage: Supported={0}, Value={1} mV{2}", telemetry.GpuVoltageSupported, telemetry.GpuVoltageValue, Environment.NewLine);
            r.AppendFormat(" NPU Frequency: Supported={0}, Value={1} MHz{2}", telemetry.NpuFrequencySupported, telemetry.NpuFrequencyValue, Environment.NewLine);
            r.AppendFormat(" NPU Activity Level: Supported={0}, Value={1}%{2}", telemetry.NpuActivityLevelSupported, telemetry.NpuActivityLevelValue, Environment.NewLine);
            r.AppendFormat(" GPU Shared Memory: Supported={0}, Value={1} MB{2}", telemetry.GpuSharedMemorySupported, telemetry.GpuSharedMemoryValue, Environment.NewLine);
        }
        else
        {
            r.AppendLine(" Failed to get telemetry data");
        }

        r.AppendLine();

        if (_d3dDeviceId != null)
        {
            r.AppendLine("D3D");
            r.AppendLine();
            r.AppendLine(" Id: " + _d3dDeviceId);
        }

        return r.ToString();
    }
}
