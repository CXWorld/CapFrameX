// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Interop;

namespace LibreHardwareMonitor.Hardware.Gpu;

internal class AmdGpuGroup : IGroup
{
    private readonly List<AmdGpu> _hardware = new();
    private readonly StringBuilder _report = new();
    private readonly bool _adlxInitialized;

    public AmdGpuGroup(ISettings settings, ISensorConfig sensorConfig = null)
    {
        try
        {
            _report.AppendLine("AMD Display Library X (ADLX)");
            _report.AppendLine();

            // Only load ADLX when an AMD GPU is actually present among the active display adapters.
            // Initializing ADLX (which loads atiadlxx.dll) on a machine that merely has leftover AMD
            // driver files — e.g. a disabled Ryzen iGPU while rendering on an NVIDIA card — makes
            // ADLX.Close() deadlock during COM teardown on the STA thread at application shutdown.
            if (!D3DDisplayDevice.IsAdapterVendorPresent("VEN_1002"))
            {
                _report.AppendLine("Status: skipped (no AMD display adapter present)");
                _report.AppendLine();
                return;
            }

            _adlxInitialized = ADLX.Initialize();

            _report.Append("Status: ");
            _report.AppendLine(_adlxInitialized ? "OK" : "Failed to initialize");
            _report.AppendLine();

            if (_adlxInitialized)
            {
                uint numberOfAdapters = ADLX.GetAdapterCount();

                _report.Append("Number of adapters: ");
                _report.AppendLine(numberOfAdapters.ToString(CultureInfo.InvariantCulture));
                _report.AppendLine();

                if (numberOfAdapters > 0)
                {
                    var adapters = new List<(uint Index, ADLX.AdlxDeviceInfo DeviceInfo)>();

                    for (uint i = 0; i < numberOfAdapters; i++)
                    {
                        ADLX.AdlxDeviceInfo deviceInfo = new();

                        if (ADLX.GetDeviceInfo(i, ref deviceInfo))
                        {
                            _report.Append("AdapterIndex: ");
                            _report.AppendLine(i.ToString(CultureInfo.InvariantCulture));
                            _report.Append("GpuName: ");
                            _report.AppendLine(deviceInfo.GpuName);
                            _report.Append("GpuType: ");
                            _report.AppendLine(((ADLX.GpuType)deviceInfo.GpuType).ToString());
                            _report.Append("VendorId: ");
                            _report.AppendLine(deviceInfo.VendorId);
                            _report.Append("DriverPath: ");
                            _report.AppendLine(deviceInfo.DriverPath);
                            _report.Append("PnpString: ");
                            _report.AppendLine(deviceInfo.PnpString);
                            _report.Append("Luid: ");
                            _report.AppendLine(deviceInfo.LuidValid != 0
                                ? D3DDisplayDevice.GetAdapterLuidInstanceName(deviceInfo.LuidHighPart, deviceInfo.LuidLowPart)
                                : "Unavailable");
                            _report.Append("UniqueId: ");
                            _report.AppendLine(deviceInfo.Id.ToString(CultureInfo.InvariantCulture));
                            _report.AppendLine();

                            // Check for valid AMD GPU
                            if (!string.IsNullOrEmpty(deviceInfo.GpuName))
                            {
                                adapters.Add((i, deviceInfo));
                            }
                        }
                    }

                    int systemMetricsAdapter = GetSystemMetricsAdapter(adapters.ConvertAll(adapter => adapter.DeviceInfo.GpuType));

                    for (int n = 0; n < adapters.Count; n++)
                    {
                        _hardware.Add(new AmdGpu(adapters[n].Index, adapters[n].DeviceInfo, settings, sensorConfig,
                            reportsSystemMetrics: n == systemMetricsAdapter));
                    }
                }
            }
        }
        catch (DllNotFoundException)
        {
            _report.AppendLine("ADLX DLL not found");
        }
        catch (EntryPointNotFoundException e)
        {
            _report.AppendLine();
            _report.AppendLine(e.ToString());
            _report.AppendLine();
        }
        catch (Exception e)
        {
            _report.AppendLine();
            _report.AppendLine("Exception: " + e.Message);
            _report.AppendLine();
        }
    }

    /// <summary>
    /// System-wide metrics (SmartShift) are reported by one adapter only: the first discrete GPU,
    /// which SmartShift shifts power to, otherwise the first adapter. Returns -1 for no adapters.
    /// </summary>
    internal static int GetSystemMetricsAdapter(IReadOnlyList<uint> gpuTypes)
    {
        for (int i = 0; i < gpuTypes.Count; i++)
        {
            if (gpuTypes[i] == (uint)ADLX.GpuType.Discrete)
                return i;
        }

        return gpuTypes.Count > 0 ? 0 : -1;
    }

    public IReadOnlyList<IHardware> Hardware => _hardware;

    public string GetReport()
    {
        return _report.ToString();
    }

    public void Close()
    {
        try
        {
            foreach (AmdGpu gpu in _hardware)
                gpu.Close();

            if (_adlxInitialized)
                ADLX.Close();
        }
        catch (Exception)
        {
            // Ignore cleanup exceptions
        }
    }
}
