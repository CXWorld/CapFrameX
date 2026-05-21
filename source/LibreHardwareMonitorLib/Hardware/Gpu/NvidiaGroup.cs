// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Interop;
using Serilog;

namespace LibreHardwareMonitor.Hardware.Gpu;

internal class NvidiaGroup : IGroup
{
    private readonly List<Hardware> _hardware = new();
    private readonly StringBuilder _report = new();

    public NvidiaGroup(ISettings settings, ISensorConfig sensorConfig = null)
    {
        NvApi.Initialize();

        if (!NvApi.IsAvailable)
            return;

        _report.AppendLine("NvApi");
        _report.AppendLine();

        if (NvApi.NvAPI_GetInterfaceVersionString(out string version) == NvApi.NvStatus.OK)
        {
            _report.Append("Version: ");
            _report.AppendLine(version);
        }

        NvApi.NvPhysicalGpuHandle[] handles = new NvApi.NvPhysicalGpuHandle[NvApi.MAX_PHYSICAL_GPUS];
        if (NvApi.NvAPI_EnumPhysicalGPUs == null)
        {
            _report.AppendLine("Error: NvAPI_EnumPhysicalGPUs not available");
            _report.AppendLine();
            return;
        }

        NvApi.NvStatus status = NvApi.NvAPI_EnumPhysicalGPUs(handles, out int count);
        if (status != NvApi.NvStatus.OK)
        {
            _report.AppendLine("Status: " + status);
            _report.AppendLine();
            return;
        }

        IDictionary<NvApi.NvPhysicalGpuHandle, List<NvDisplayHandleInfo>> displayHandles = new Dictionary<NvApi.NvPhysicalGpuHandle, List<NvDisplayHandleInfo>>();
        if (NvApi.NvAPI_EnumNvidiaDisplayHandle != null && NvApi.NvAPI_GetPhysicalGPUsFromDisplay != null)
        {
            Log.Logger.Information("NvidiaGroup: Starting display handle enumeration");
            status = NvApi.NvStatus.OK;
            int i = 0;
            while (status == NvApi.NvStatus.OK)
            {
                NvApi.NvDisplayHandle displayHandle = new();
                status = NvApi.NvAPI_EnumNvidiaDisplayHandle(i, ref displayHandle);

                if (status == NvApi.NvStatus.OK)
                {
                    Log.Logger.Information("NvidiaGroup: Found display handle at index {Index}, Handle pointer: {HandlePtr}", i, displayHandle);

                    string displayName = null;
                    if (NvApi.NvAPI_GetAssociatedDisplayName != null)
                    {
                        var nameStatus = NvApi.GetAssociatedDisplayName(displayHandle, out string associatedDisplayName);
                        if (nameStatus == NvApi.NvStatus.OK)
                        {
                            displayName = associatedDisplayName;
                            Log.Logger.Information("NvidiaGroup: Display {Index} name: {DisplayName}", i, displayName);
                        }
                        else
                        {
                            Log.Logger.Warning("NvidiaGroup: Failed to get display name for index {Index}, Status: {Status}", i, nameStatus);
                        }
                    }
                    else
                    {
                        Log.Logger.Warning("NvidiaGroup: NvAPI_GetAssociatedDisplayName not available");
                    }

                    NvApi.NvPhysicalGpuHandle[] handlesFromDisplay = new NvApi.NvPhysicalGpuHandle[NvApi.MAX_PHYSICAL_GPUS];
                    var gpuFromDisplayStatus = NvApi.NvAPI_GetPhysicalGPUsFromDisplay(displayHandle, handlesFromDisplay, out uint countFromDisplay);
                    if (gpuFromDisplayStatus == NvApi.NvStatus.OK)
                    {
                        Log.Logger.Information("NvidiaGroup: Display {Index} is associated with {GpuCount} GPU(s)", i, countFromDisplay);
                        for (int j = 0; j < countFromDisplay; j++)
                        {
                            Log.Logger.Information("NvidiaGroup: Display {DisplayIndex} -> GPU handle {GpuIndex}: {GpuHandle}", i, j, handlesFromDisplay[j]);
                            if (!displayHandles.TryGetValue(handlesFromDisplay[j], out List<NvDisplayHandleInfo> handlesForGpu))
                            {
                                handlesForGpu = [];
                                displayHandles.Add(handlesFromDisplay[j], handlesForGpu);
                            }

                            handlesForGpu.Add(new NvDisplayHandleInfo(displayHandle, displayName));
                        }
                    }
                    else
                    {
                        Log.Logger.Warning("NvidiaGroup: Failed to get GPUs from display {Index}, Status: {Status}", i, gpuFromDisplayStatus);
                    }
                }
                else if (status != NvApi.NvStatus.EndEnumeration)
                {
                    Log.Logger.Warning("NvidiaGroup: Display enumeration ended with status: {Status} at index {Index}", status, i);
                }

                i++;
            }
            Log.Logger.Information("NvidiaGroup: Display handle enumeration complete. Found {DisplayCount} display handle mappings for {GpuCount} GPU(s)",
                displayHandles.Values.Sum(list => list.Count), displayHandles.Count);
        }
        else
        {
            Log.Logger.Warning("NvidiaGroup: Display handle enumeration skipped - NvAPI_EnumNvidiaDisplayHandle: {EnumAvailable}, NvAPI_GetPhysicalGPUsFromDisplay: {GetPhysicalAvailable}",
                NvApi.NvAPI_EnumNvidiaDisplayHandle != null, NvApi.NvAPI_GetPhysicalGPUsFromDisplay != null);
        }

        _report.Append("Number of GPUs: ");
        _report.AppendLine(count.ToString(CultureInfo.InvariantCulture));

        for (int i = 0; i < count; i++)
        {
            displayHandles.TryGetValue(handles[i], out List<NvDisplayHandleInfo> displayHandleInfos);
            int displayCount = displayHandleInfos?.Count ?? 0;
            Log.Logger.Information("NvidiaGroup: Creating NvidiaGpu {GpuIndex} with handle {GpuHandle}, DisplayHandles count: {DisplayCount}",
                i, handles[i], displayCount);
            if (displayHandleInfos != null)
            {
                foreach (var displayInfo in displayHandleInfos)
                {
                    Log.Logger.Information("NvidiaGroup: GPU {GpuIndex} has display: {DisplayName}, Handle: {DisplayHandle}",
                        i, displayInfo.DisplayName ?? "(no name)", displayInfo.Handle);
                }
            }
            else
            {
                Log.Logger.Warning("NvidiaGroup: GPU {GpuIndex} has no display handles associated", i);
            }
            _hardware.Add(new NvidiaGpu(i, handles[i], displayHandleInfos, settings, sensorConfig));
        }

        _report.AppendLine();
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

        NvidiaML.Close();
    }
}
