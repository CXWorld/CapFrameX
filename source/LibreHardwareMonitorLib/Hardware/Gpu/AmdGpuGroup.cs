// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LibreHardwareMonitor.Interop;

namespace LibreHardwareMonitor.Hardware.Gpu;

internal class AmdGpuGroup : IGroup
{
    private readonly List<AmdGpu> _hardware = new();
    private readonly StringBuilder _report = new();
    private readonly bool _adlxInitialized;

    public AmdGpuGroup(ISettings settings)
    {
        try
        {
            _report.AppendLine("AMD Display Library X (ADLX)");
            _report.AppendLine();

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
                            _report.Append("UniqueId: ");
                            _report.AppendLine(deviceInfo.Id.ToString(CultureInfo.InvariantCulture));
                            _report.AppendLine();

                            // Check for valid AMD GPU
                            if (!string.IsNullOrEmpty(deviceInfo.GpuName))
                            {
                                _hardware.Add(new AmdGpu(i, deviceInfo, settings));
                            }
                        }
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
