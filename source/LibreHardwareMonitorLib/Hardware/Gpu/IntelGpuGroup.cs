// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LibreHardwareMonitor.Hardware.Cpu;
using LibreHardwareMonitor.Interop;

namespace LibreHardwareMonitor.Hardware.Gpu;

internal class IntelGpuGroup : IGroup
{
    private readonly List<Hardware> _hardware = new();
    private readonly StringBuilder _report = new();

    public IntelGpuGroup(List<IntelCpu> intelCpus, ISettings settings)
    {
        if (!Software.OperatingSystem.IsUnix)
        {
            // Initialize Intel GCL for discrete GPUs
            bool gclInitialized = false;

            try
            {
                gclInitialized = IGCL.IntializeIgcl();
            }
            catch (Exception ex)
            {
                _report.Append("Intel GCL initialization failed: ");
                _report.AppendLine(ex.Message);
            }

            _report.AppendLine("Intel GPU Detection");
            _report.AppendLine();
            _report.Append("Intel GCL Initialized: ");
            _report.AppendLine(gclInitialized.ToString(CultureInfo.InvariantCulture));
            _report.AppendLine();

            uint adapterCount = 0;
            // Enumerate discrete GPUs using Intel GCL
            if (gclInitialized)
            {
                try
                {
                     adapterCount = IGCL.GetAdpaterCount();
                    _report.Append("Device handles found: ");
                    _report.AppendLine(adapterCount.ToString());

                    for (uint index = 0; index < adapterCount; index++)
                    {
                        try
                        {
                            var deviceInfo = new IgclDeviceInfo();
                            IGCL.GetDeviceInfo(index, ref deviceInfo);

                            if (deviceInfo.Pci_vendor_id == IGCL.Intel_VENDOR_ID)
                            {
                                var gpu = new IntelGclGpu(index, deviceInfo, settings);

                                _report.Append("Discrete GPU: ");
                                _report.AppendLine(gpu.Name);
                                _report.Append("Device ID: ");
                                _report.AppendLine(gpu.DeviceId);
                                _report.AppendLine();

                                _hardware.Add(gpu);
                                _report.AppendLine("Successfully added discrete GPU to hardware list");
                            }

                        }
                        catch (Exception ex)
                        {
                            _report.Append("Failed to create IntelDiscreteGpu: ");
                            _report.AppendLine(ex.Message);
                            _report.AppendLine(ex.StackTrace);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _report.Append("Failed to enumerate Intel GPU devices: ");
                    _report.AppendLine(ex.Message);
                }
            }

            // Enumerate integrated GPUs using D3D (existing logic)
            if (intelCpus?.Count > 0)
            {
                _report.AppendLine("Intel GPU (D3D - Integrated)");
                _report.AppendLine();

                string[] ids = D3DDisplayDevice.GetDeviceIdentifiers();

                _report.Append("Number of D3D adapters: ");
                _report.AppendLine(ids.Length.ToString(CultureInfo.InvariantCulture));
                _report.AppendLine();

                for (int i = 0; i < ids.Length; i++)
                {
                    string deviceId = ids[i];
                    bool isIntel = deviceId.IndexOf("VEN_8086", StringComparison.Ordinal) != -1;

                    _report.Append("AdapterIndex: ");
                    _report.AppendLine(i.ToString(CultureInfo.InvariantCulture));
                    _report.Append("DeviceId: ");
                    _report.AppendLine(deviceId);
                    _report.Append("IsIntel: ");
                    _report.AppendLine(isIntel.ToString(CultureInfo.InvariantCulture));

                    if (isIntel && D3DDisplayDevice.GetDeviceInfoByIdentifier(deviceId, out D3DDisplayDevice.D3DDeviceInfo deviceInfo))
                    {
                        _report.Append("GpuSharedLimit: ");
                        _report.AppendLine(deviceInfo.GpuSharedLimit.ToString(CultureInfo.InvariantCulture));
                        _report.Append("GpuSharedUsed: ");
                        _report.AppendLine(deviceInfo.GpuSharedUsed.ToString(CultureInfo.InvariantCulture));
                        _report.Append("GpuSharedMax: ");
                        _report.AppendLine(deviceInfo.GpuSharedMax.ToString(CultureInfo.InvariantCulture));
                        _report.Append("GpuDedicatedLimit: ");
                        _report.AppendLine(deviceInfo.GpuDedicatedLimit.ToString(CultureInfo.InvariantCulture));
                        _report.Append("GpuDedicatedUsed: ");
                        _report.AppendLine(deviceInfo.GpuDedicatedUsed.ToString(CultureInfo.InvariantCulture));
                        _report.Append("GpuDedicatedMax: ");
                        _report.AppendLine(deviceInfo.GpuDedicatedMax.ToString(CultureInfo.InvariantCulture));
                        _report.Append("Integrated: ");
                        _report.AppendLine(deviceInfo.Integrated.ToString(CultureInfo.InvariantCulture));

                        // We also want D3D VRAM counters for discrete Intel GPUs
                        _hardware.Add(new IntelD3dGpu(intelCpus[0], (int)adapterCount + i, deviceId, deviceInfo, settings));

                        //if (deviceInfo.Integrated)
                        //{
                        //    // It may seem strange to only use the first cpu here, but in-case we have a multi cpu system with integrated graphics (does that exist?),
                        //    // we would pick up the multiple device identifiers above and would add one instance for each CPU.
                        //    _hardware.Add(new IntelD3dGpu(intelCpus[0], (int)adapterCount + i, deviceId, deviceInfo, settings));
                        //}
                    }

                    _report.AppendLine();
                }
            }
        }
    }

    public IReadOnlyList<IHardware> Hardware => _hardware;

    public string GetReport()
    {
        return _report.ToString();
    }

    public void Close()
    {
        foreach (Hardware gpu in _hardware)
            gpu.Close();

        // Shutdown Intel GCL
        try
        {
            if (IGCL.IsInitialized)
            {
                IGCL.CloseIgcl();
            }
        }
        catch
        {
            // Ignore shutdown errors
        }
    }
}
