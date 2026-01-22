// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using LibreHardwareMonitor.Interop;
using Microsoft.Win32;
using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace LibreHardwareMonitor.Hardware.Gpu;

internal sealed class AmdGpu : GenericGpu
{
    private readonly uint _adapterIndex;
    private readonly ADLX.AdlxDeviceInfo _deviceInfo;
    private readonly string _d3dDeviceId;

    // Temperature sensors - keeping existing names
    private readonly Sensor _temperatureCore;
    private readonly Sensor _temperatureHotSpot;
    private readonly Sensor _temperatureMemory;
    private readonly Sensor _temperatureIntake;

    // Clock sensors - keeping existing names
    private readonly Sensor _coreClock;
    private readonly Sensor _memoryClock;
    private readonly Sensor _npuClock;

    // Voltage sensors - keeping existing names
    private readonly Sensor _coreVoltage;

    // Load sensors - keeping existing names
    private readonly Sensor _coreLoad;
    private readonly Sensor _npuLoad;

    // Fan sensor - keeping existing name
    private readonly Sensor _fan;

    // Power sensors - keeping existing names
    private readonly Sensor _powerTotal;
    private readonly Sensor _powerTotalBoardPower;

    // Memory sensors (VRAM) - keeping existing names
    private readonly Sensor _memoryUsed;
    private readonly Sensor _memoryTotal;
    private readonly Sensor _memoryFree;

    // D3D memory sensors - keeping existing names
    private readonly Sensor _gpuDedicatedMemoryUsage;
    private readonly Sensor _gpuDedicatedMemoryFree;
    private readonly Sensor _gpuDedicatedMemoryTotal;
    private readonly Sensor _gpuSharedMemoryUsage;
    private readonly Sensor _gpuSharedMemoryFree;
    private readonly Sensor _gpuSharedMemoryTotal;

    // D3D node usage sensors
    private readonly Sensor[] _gpuNodeUsage;
    private readonly DateTime[] _gpuNodeUsagePrevTick;
    private readonly long[] _gpuNodeUsagePrevValue;

    // ADLX shared memory from telemetry
    private readonly Sensor _adlxSharedMemory;

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

        // Temperature sensors - keeping existing names for backward compatibility
        _temperatureCore = new Sensor("GPU Core", 0, SensorType.Temperature, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_2_0" };
        _temperatureMemory = new Sensor("GPU Memory", 1, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{index}_2_1" };
        _temperatureHotSpot = new Sensor("GPU Hot Spot", 2, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{index}_2_2" };
        _temperatureIntake = new Sensor("GPU Intake", 3, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{index}_2_3" };

        // Clock sensors - keeping existing names
        _coreClock = new Sensor("GPU Core", 0, SensorType.Clock, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_0_0" };
        _memoryClock = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_0_1" };
        _npuClock = new Sensor("NPU", 2, SensorType.Clock, this, settings)
        { PresentationSortKey = $"{index}_0_2" };

        // Fan sensor - keeping existing name
        _fan = new Sensor("GPU Fan", 0, SensorType.Fan, this, settings)
        { PresentationSortKey = $"{index}_5_0" };

        // Voltage sensor - keeping existing name
        _coreVoltage = new Sensor("GPU Core", 0, SensorType.Voltage, this, settings)
        { PresentationSortKey = $"{index}_4_0" };

        // Load sensors - keeping existing names
        _coreLoad = new Sensor("GPU Core", 0, SensorType.Load, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_1_0" };
        _npuLoad = new Sensor("NPU", 1, SensorType.Load, this, settings)
        { PresentationSortKey = $"{index}_1_1" };

        // Power sensors - keeping existing names
        _powerTotal = new Sensor("GPU Power", 0, SensorType.Power, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_3_0" };
        _powerTotalBoardPower = new Sensor("GPU TBP", 1, SensorType.Power, this, settings)
        { PresentationSortKey = $"{index}_3_1" };

        // Memory sensors - keeping existing names
        _memoryUsed = new Sensor("GPU Memory Used", 0, SensorType.Data, this, settings)
        { PresentationSortKey = $"{index}_6_0" };
        _memoryFree = new Sensor("GPU Memory Free", 1, SensorType.Data, this, settings)
        { PresentationSortKey = $"{index}_6_1" };
        _memoryTotal = new Sensor("GPU Memory Total", 2, SensorType.Data, this, settings)
        { PresentationSortKey = $"{index}_6_2" };

        // ADLX shared memory sensor
        _adlxSharedMemory = new Sensor("GPU Shared Memory", 3, SensorType.Data, this, settings)
        { PresentationSortKey = $"{index}_6_3" };

        // D3D integration for additional memory metrics
        if (!Software.OperatingSystem.IsUnix)
        {
            string[] deviceIds = D3DDisplayDevice.GetDeviceIdentifiers();
            if (deviceIds != null)
            {
                foreach (string deviceId in deviceIds)
                {
                    string actualDeviceId = D3DDisplayDevice.GetActualDeviceIdentifier(deviceId);

                    // Match by GPU name or driver path
                    bool isMatch = false;
                    if (!string.IsNullOrEmpty(deviceInfo.GpuName))
                    {
                        isMatch = actualDeviceId.IndexOf(deviceInfo.GpuName, StringComparison.OrdinalIgnoreCase) != -1;
                    }
                    if (!isMatch && !string.IsNullOrEmpty(deviceInfo.DriverPath))
                    {
                        isMatch = actualDeviceId.IndexOf(deviceInfo.DriverPath, StringComparison.OrdinalIgnoreCase) != -1 ||
                                  deviceInfo.DriverPath.IndexOf(actualDeviceId, StringComparison.OrdinalIgnoreCase) != -1;
                    }

                    if (isMatch && D3DDisplayDevice.GetDeviceInfoByIdentifier(deviceId, out D3DDisplayDevice.D3DDeviceInfo d3dDeviceInfo))
                    {
                        _d3dDeviceId = deviceId;

                        int nodeSensorIndex = 2;
                        int memorySensorIndex = 4;

                        _gpuDedicatedMemoryUsage = new Sensor("GPU Memory Dedicated", memorySensorIndex++, SensorType.Data, this, settings)
                        { PresentationSortKey = $"{index}_7_0" };
                        _gpuDedicatedMemoryFree = new Sensor("GPU Memory Dedicated Free", memorySensorIndex++, SensorType.Data, this, settings)
                        { PresentationSortKey = $"{index}_7_1" };
                        _gpuDedicatedMemoryTotal = new Sensor("GPU Memory Dedicated Total", memorySensorIndex++, SensorType.Data, this, settings)
                        { PresentationSortKey = $"{index}_7_2" };
                        _gpuSharedMemoryUsage = new Sensor("GPU Memory Shared", memorySensorIndex++, SensorType.Data, this, settings)
                        { PresentationSortKey = $"{index}_8_0" };
                        _gpuSharedMemoryFree = new Sensor("GPU Memory Shared Free", memorySensorIndex++, SensorType.Data, this, settings)
                        { PresentationSortKey = $"{index}_8_1" };
                        _gpuSharedMemoryTotal = new Sensor("GPU Memory Shared Total", memorySensorIndex++, SensorType.Data, this, settings)
                        { PresentationSortKey = $"{index}_8_2" };

                        _gpuNodeUsage = new Sensor[d3dDeviceInfo.Nodes.Length];
                        _gpuNodeUsagePrevValue = new long[d3dDeviceInfo.Nodes.Length];
                        _gpuNodeUsagePrevTick = new DateTime[d3dDeviceInfo.Nodes.Length];

                        foreach (D3DDisplayDevice.D3DDeviceNodeInfo node in d3dDeviceInfo.Nodes.OrderBy(x => x.Name))
                        {
                            _gpuNodeUsage[node.Id] = new Sensor(node.Name, nodeSensorIndex++, SensorType.Load, this, settings)
                            { PresentationSortKey = $"{index}_9_{nodeSensorIndex}" };
                            _gpuNodeUsagePrevValue[node.Id] = node.RunningTime;
                            _gpuNodeUsagePrevTick[node.Id] = node.QueryTime;
                        }

                        break;
                    }
                }
            }
        }

        Update();
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
            if (telemetry.GpuTemperatureSupported)
            {
                _temperatureCore.Value = (float)telemetry.GpuTemperatureValue;
                ActivateSensor(_temperatureCore);
            }
            else
            {
                _temperatureCore.Value = null;
            }

            if (telemetry.GpuHotspotTemperatureSupported)
            {
                _temperatureHotSpot.Value = (float)telemetry.GpuHotspotTemperatureValue;
                ActivateSensor(_temperatureHotSpot);
            }
            else
            {
                _temperatureHotSpot.Value = null;
            }

            if (telemetry.GpuMemoryTemperatureSupported)
            {
                _temperatureMemory.Value = (float)telemetry.GpuMemoryTemperatureValue;
                ActivateSensor(_temperatureMemory);
            }
            else
            {
                _temperatureMemory.Value = null;
            }

            if (telemetry.GpuIntakeTemperatureSupported)
            {
                _temperatureIntake.Value = (float)telemetry.GpuIntakeTemperatureValue;
                ActivateSensor(_temperatureIntake);
            }
            else
            {
                _temperatureIntake.Value = null;
            }

            // Clock sensors
            if (telemetry.GpuClockSpeedSupported)
            {
                _coreClock.Value = (float)telemetry.GpuClockSpeedValue;
                ActivateSensor(_coreClock);
            }
            else
            {
                _coreClock.Value = null;
            }

            if (telemetry.GpuVRAMClockSpeedSupported)
            {
                _memoryClock.Value = (float)telemetry.GpuVRAMClockSpeedValue;
                ActivateSensor(_memoryClock);
            }
            else
            {
                _memoryClock.Value = null;
            }

            if (telemetry.NpuFrequencySupported)
            {
                _npuClock.Value = (float)telemetry.NpuFrequencyValue;
                ActivateSensor(_npuClock);
            }
            else
            {
                _npuClock.Value = null;
            }

            // Fan sensor
            if (telemetry.GpuFanSpeedSupported)
            {
                _fan.Value = (float)telemetry.GpuFanSpeedValue;
                ActivateSensor(_fan);
            }
            else
            {
                _fan.Value = null;
            }

            // Voltage sensor
            if (telemetry.GpuVoltageSupported)
            {
                // ADLX returns voltage in mV, convert to V
                _coreVoltage.Value = (float)(telemetry.GpuVoltageValue / 1000.0);
                ActivateSensor(_coreVoltage);
            }
            else
            {
                _coreVoltage.Value = null;
            }

            // Load sensors
            if (telemetry.GpuUsageSupported)
            {
                _coreLoad.Value = (float)Math.Min(telemetry.GpuUsageValue, 100);
                ActivateSensor(_coreLoad);
            }
            else
            {
                _coreLoad.Value = null;
            }

            if (telemetry.NpuActivityLevelSupported)
            {
                _npuLoad.Value = (float)Math.Min(telemetry.NpuActivityLevelValue, 100);
                ActivateSensor(_npuLoad);
            }
            else
            {
                _npuLoad.Value = null;
            }

            // Power sensors
            if (telemetry.GpuPowerSupported)
            {
                _powerTotal.Value = (float)telemetry.GpuPowerValue;
                ActivateSensor(_powerTotal);
            }
            else
            {
                _powerTotal.Value = null;
            }

            if (telemetry.GpuTotalBoardPowerSupported)
            {
                _powerTotalBoardPower.Value = (float)telemetry.GpuTotalBoardPowerValue;
                ActivateSensor(_powerTotalBoardPower);
            }
            else
            {
                _powerTotalBoardPower.Value = null;
            }

            // VRAM usage from ADLX (in MB)
            if (telemetry.GpuVramSupported)
            {
                _memoryUsed.Value = (float)(telemetry.GpuVramValue / 1024.0); // Convert MB to GB
                ActivateSensor(_memoryUsed);
            }
            else
            {
                _memoryUsed.Value = null;
            }

            // Shared memory from ADLX (in MB)
            if (telemetry.GpuSharedMemorySupported)
            {
                _adlxSharedMemory.Value = (float)(telemetry.GpuSharedMemoryValue / 1024.0); // Convert MB to GB
                ActivateSensor(_adlxSharedMemory);
            }
            else
            {
                _adlxSharedMemory.Value = null;
            }
        }

        // D3D memory and usage metrics
        if (_d3dDeviceId != null && D3DDisplayDevice.GetDeviceInfoByIdentifier(_d3dDeviceId, out D3DDisplayDevice.D3DDeviceInfo deviceInfo))
        {
            _gpuDedicatedMemoryTotal.Value = 1f * deviceInfo.GpuVideoMemoryLimit / 1024 / 1024 / 1024;
            _gpuDedicatedMemoryUsage.Value = 1f * deviceInfo.GpuDedicatedUsed / 1024 / 1024 / 1024;
            _gpuDedicatedMemoryFree.Value = _gpuDedicatedMemoryTotal.Value - _gpuDedicatedMemoryUsage.Value;
            _gpuSharedMemoryUsage.Value = 1f * deviceInfo.GpuSharedUsed / 1024 / 1024 / 1024;
            _gpuSharedMemoryTotal.Value = 1f * deviceInfo.GpuSharedLimit / 1024 / 1024 / 1024;
            _gpuSharedMemoryFree.Value = _gpuSharedMemoryTotal.Value - _gpuSharedMemoryUsage.Value;

            ActivateSensor(_gpuDedicatedMemoryUsage);
            ActivateSensor(_gpuSharedMemoryUsage);

            // Update memory total/free based on D3D data
            if (_memoryTotal.Value == null || _memoryTotal.Value == 0)
            {
                _memoryTotal.Value = _gpuDedicatedMemoryTotal.Value;
                ActivateSensor(_memoryTotal);
            }
            if (_memoryUsed.Value != null && _memoryTotal.Value != null)
            {
                _memoryFree.Value = _memoryTotal.Value - _memoryUsed.Value;
                ActivateSensor(_memoryFree);
            }

            // Node usage
            foreach (D3DDisplayDevice.D3DDeviceNodeInfo node in deviceInfo.Nodes)
            {
                if (_gpuNodeUsage != null && (int)node.Id < _gpuNodeUsage.Length && _gpuNodeUsage[node.Id] != null)
                {
                    long runningTimeDiff = node.RunningTime - _gpuNodeUsagePrevValue[node.Id];
                    long timeDiff = node.QueryTime.Ticks - _gpuNodeUsagePrevTick[node.Id].Ticks;

                    _gpuNodeUsage[node.Id].Value = 100f * runningTimeDiff / timeDiff;
                    _gpuNodeUsagePrevValue[node.Id] = node.RunningTime;
                    _gpuNodeUsagePrevTick[node.Id] = node.QueryTime;
                    ActivateSensor(_gpuNodeUsage[node.Id]);
                }
            }
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
