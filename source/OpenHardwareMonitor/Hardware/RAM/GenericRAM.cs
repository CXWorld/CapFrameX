/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2012 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System;
using System.Diagnostics;
using Serilog;
using System.Linq;
using CapFrameX.Monitoring.Contracts;

namespace OpenHardwareMonitor.Hardware.RAM
{
    internal class GenericRAM : Hardware
    {
        private const float SCALE = 1024 * 1024 * 1024;

        private readonly object _performanceCounterLock = new object();

        private Sensor loadSensor;
        private Sensor usedMemory;
        private Sensor availableMemory;
        private Sensor usedMemoryProcess;
        private Sensor usedMemoryAndCacheProcess;
        private ISensorConfig sensorConfig;
        private PerformanceCounter ramUsageGamePerformanceCounter;
        private PerformanceCounter ramAndCacheUsageGamePerformanceCounter;

        public GenericRAM(string name, ISettings settings, ISensorConfig config, IProcessService processService)
          : base(name, new Identifier("ram"), settings)
        {
            sensorConfig = config;

            try
            {
                if (PerformanceCounterCategory.Exists("Process"))
                {
                    processService
                    .ProcessIdStream
                    .DistinctUntilChanged()
                    .Subscribe(id =>
                    {
                        try
                        {
                            lock (_performanceCounterLock)
                            {
                                try
                                {
                                    if (id == 0)
                                    {
                                        ramUsageGamePerformanceCounter = null;
                                        ramAndCacheUsageGamePerformanceCounter = null;
                                        return;
                                    }

                                    var process = Process.GetProcessById(id);
                                    if (process != null)
                                    {
                                        var validInstanceName = GetValidInstanceName(process);
                                        Log.Logger.Information("Valid instance name for memory performance counter: {instanceName}", validInstanceName);

                                        // Working Set - Private
                                        ramUsageGamePerformanceCounter = new PerformanceCounter("Process", "Working Set - Private", validInstanceName, true);
                                        // Working Set (private + resources)
                                        ramAndCacheUsageGamePerformanceCounter = new PerformanceCounter("Process", "Working Set", validInstanceName, true);
                                    }
                                    else
                                    {
                                        Log.Logger.Error("Failed to get process by Id={Id}.", id);
                                        ramUsageGamePerformanceCounter = null;
                                        ramAndCacheUsageGamePerformanceCounter = null;
                                    }
                                }
                                catch
                                {
                                    ramUsageGamePerformanceCounter = null;
                                    ramAndCacheUsageGamePerformanceCounter = null;
                                    Log.Logger.Error("Failed to create performance counter Working Set or Working Set - Private");
                                }
                            }
                        }
                        catch
                        {
                            Log.Logger.Error("Error while subscribing to process ID changed. Working Set PerformanceCounter.");
                        }
                    });
                }
                else
                {
                    Log.Logger.Error("Failed to create memory performance counter. Category Process does not exist.");
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Failed to create memory performance counter.");
            }

            loadSensor = new Sensor("Memory", 0, SensorType.Load, this, settings);
            ActivateSensor(loadSensor);

            usedMemory = new Sensor("Used Memory", 0, SensorType.Data, this,
              settings);
            ActivateSensor(usedMemory);

            availableMemory = new Sensor("Available Memory", 1, SensorType.Data, this,
              settings);
            ActivateSensor(availableMemory);

            usedMemoryProcess = new Sensor("Used Memory Game", 2, SensorType.Data, this,
              settings);
            ActivateSensor(usedMemoryProcess);

            usedMemoryAndCacheProcess = new Sensor("Memory + Cache Game", 3, SensorType.Data, this,
              settings);
            ActivateSensor(usedMemoryAndCacheProcess);
        }

        public override HardwareType HardwareType => HardwareType.RAM;

        public override Vendor Vendor => Vendor.Unknown;

        public override void Update()
        {
            NativeMethods.MemoryStatusEx status = new NativeMethods.MemoryStatusEx
            {
                Length = checked((uint)Marshal.SizeOf(
                typeof(NativeMethods.MemoryStatusEx)))
            };

            if (sensorConfig.GetSensorEvaluate(usedMemory.IdentifierString)
                || sensorConfig.GetSensorEvaluate(availableMemory.IdentifierString)
                || sensorConfig.GetSensorEvaluate(loadSensor.IdentifierString))
            {
                if (!NativeMethods.GlobalMemoryStatusEx(ref status))
                    return;

                loadSensor.Value = 100.0f -
                  (100.0f * status.AvailablePhysicalMemory) /
                  status.TotalPhysicalMemory;

                usedMemory.Value = (status.TotalPhysicalMemory
                  - status.AvailablePhysicalMemory) / SCALE;

                availableMemory.Value = status.AvailablePhysicalMemory / SCALE;
            }

            if (sensorConfig.GetSensorEvaluate(usedMemoryProcess.IdentifierString))
            {
                lock (_performanceCounterLock)
                {
                    try
                    {
                        usedMemoryProcess.Value = ramUsageGamePerformanceCounter != null
                            ? ramUsageGamePerformanceCounter.NextValue() / SCALE : 0f;
                    }
                    catch { usedMemoryProcess.Value = null; }
                }
            }

            if (sensorConfig.GetSensorEvaluate(usedMemoryAndCacheProcess.IdentifierString))
            {
                lock (_performanceCounterLock)
                {
                    try
                    {
                        usedMemoryAndCacheProcess.Value = ramAndCacheUsageGamePerformanceCounter != null
                            ? ramAndCacheUsageGamePerformanceCounter.NextValue() / SCALE : 0f;
                    }
                    catch { usedMemoryAndCacheProcess.Value = null; }
                }
            }
        }

        private class NativeMethods
        {
            [StructLayout(LayoutKind.Sequential)]
            public struct MemoryStatusEx
            {
                public uint Length;
                public uint MemoryLoad;
                public ulong TotalPhysicalMemory;
                public ulong AvailablePhysicalMemory;
                public ulong TotalPageFile;
                public ulong AvailPageFile;
                public ulong TotalVirtual;
                public ulong AvailVirtual;
                public ulong AvailExtendedVirtual;
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
        }

        private string GetValidInstanceName(Process process)
        {
            var nameToUseForMemory = string.Empty;
            var category = new PerformanceCounterCategory("Process");
            var instanceNames = category.GetInstanceNames().Where(x => x.Contains(process.ProcessName));
            foreach (var instanceName in instanceNames)
            {
                using (var performanceCounter = new PerformanceCounter("Process", "ID Process", instanceName, true))
                {
                    if (performanceCounter.RawValue != process.Id)
                        continue;
                    nameToUseForMemory = instanceName;
                    break;
                }
            }

            return nameToUseForMemory;
        }
    }
}
