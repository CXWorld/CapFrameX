// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.System.SystemInformation;

namespace LibreHardwareMonitor.Hardware.Memory;

internal static unsafe class MemoryWindows
{
    private static PerformanceCounter _processWorkingSetCounter;
    private static int _counterProcessId;

    public static void Update(TotalMemory memory)
    {
        MEMORYSTATUSEX status = new() { dwLength = (uint)sizeof(MEMORYSTATUSEX) };

        if (!PInvoke.GlobalMemoryStatusEx(ref status))
            return;

        memory.PhysicalMemoryUsed.Value = (float)(status.ullTotalPhys - status.ullAvailPhys) / (1024 * 1024 * 1024);
        memory.PhysicalMemoryAvailable.Value = (float)status.ullAvailPhys / (1024 * 1024 * 1024);
        memory.PhysicalMemoryLoad.Value = 100.0f - ((100.0f * status.ullAvailPhys) / status.ullTotalPhys);

        UpdateGameMemory(memory);
    }

    public static void Update(VirtualMemory memory)
    {
        MEMORYSTATUSEX status = new() { dwLength = (uint)sizeof(MEMORYSTATUSEX) };

        if (!PInvoke.GlobalMemoryStatusEx(ref status))
            return;

        memory.VirtualMemoryUsed.Value = (float)(status.ullTotalPageFile - status.ullAvailPageFile) / (1024 * 1024 * 1024);
        memory.VirtualMemoryAvailable.Value = (float)status.ullAvailPageFile / (1024 * 1024 * 1024);
        memory.VirtualMemoryLoad.Value = 100.0f - ((100.0f * status.ullAvailPageFile) / status.ullTotalPageFile);
    }

    internal static void UpdateProcessCounter(TotalMemory memory, int processId)
    {
        try
        {
            _processWorkingSetCounter?.Dispose();
            _processWorkingSetCounter = null;
            _counterProcessId = 0;
        }
        catch { }

        if (processId == 0)
        {
            memory.PhysicalMemoryGameUsed.Value = 0;
            return;
        }

        try
        {
            var process = Process.GetProcessById(processId);
            string processName = process.ProcessName;
            string instanceName = ResolveInstanceName(processName, processId);

            if (instanceName != null)
            {
                _processWorkingSetCounter = new PerformanceCounter(
                    "Process", "Working Set - Private", instanceName, true);
                _counterProcessId = processId;
            }
        }
        catch
        {
            _processWorkingSetCounter = null;
            _counterProcessId = 0;
        }
    }

    private static void UpdateGameMemory(TotalMemory memory)
    {
        if (_processWorkingSetCounter == null)
            return;

        try
        {
            float bytes = _processWorkingSetCounter.NextValue();
            memory.PhysicalMemoryGameUsed.Value = bytes / (1024 * 1024 * 1024);
        }
        catch
        {
            // Process exited or counter invalidated
            try { _processWorkingSetCounter?.Dispose(); } catch { }
            _processWorkingSetCounter = null;
            _counterProcessId = 0;
            memory.PhysicalMemoryGameUsed.Value = 0;
        }
    }

    private static string ResolveInstanceName(string processName, int processId)
    {
        try
        {
            var category = new PerformanceCounterCategory("Process");
            string[] instances = category.GetInstanceNames();

            foreach (string instance in instances)
            {
                if (!instance.StartsWith(processName, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    using var pidCounter = new PerformanceCounter("Process", "ID Process", instance, true);
                    int pid = (int)pidCounter.RawValue;

                    if (pid == processId)
                        return instance;
                }
                catch { }
            }
        }
        catch { }

        return null;
    }
}
