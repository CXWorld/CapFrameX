/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using CapFrameX.Extensions;
using CapFrameX.Monitoring.Contracts;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace OpenHardwareMonitor.Hardware.ATI
{
    internal enum AdlGeneration
    {
        ADL,
        ADLX
    }

    internal class ATIGroup : IGroup
    {
        private readonly List<ATIGPU> hardware = new List<ATIGPU>();

        private IntPtr context = IntPtr.Zero;

        public ATIGroup(ISettings settings, ISensorConfig sensorConfig, IProcessService processService)
        {
            try
            {
                if (!TryUseAdlx(settings, sensorConfig, processService))
                {
                    Log.Information("Failed to load ADLX, use ADL fallback instead.");
                }
            }
            catch (DllNotFoundException ex) { Log.Logger.Error(ex, "AMD GPU lib DLL error."); }
            catch (EntryPointNotFoundException ex) { Log.Logger.Error(ex, "AMD GPU lib entry point error."); }
            catch (AccessViolationException ex) { Log.Logger.Error(ex, $"Access violation exception while accessing ADLX."); }
            catch (Exception ex) { Log.Logger.Error(ex, $"Unexpected exception while accessing ADLX."); }
        }

        private bool TryUseAdlx(ISettings settings, ISensorConfig sensorConfig, IProcessService processService)
        {
            try
            {
                bool check = false;
                if (ADLX.IntializeAMDGpuLib())
                {
                    var adapterCount = ADLX.GetAtiAdpaterCount();

                    if (adapterCount > 0)
                    {
                        for (int index = 0; index < adapterCount; index++)
                        {
                            var deviceInfo = new AdlxDeviceInfo();
                            if (ADLX.GetAdlxDeviceInfo((uint)index, ref deviceInfo))
                            {
                                if (deviceInfo.VendorId == ADLX.AMD_VENDOR_ID)
                                {
                                    // Filter integrated graphics
                                    if (deviceInfo.GpuType == 2 || adapterCount == 1)
                                    {
                                        var adlxTelemetryData = new AdlxTelemetryData();
                                        var hasData = ADLX.GetAdlxTelemetry((uint)index, 1000u, ref adlxTelemetryData);

                                        if (!hasData)
                                        {
                                            for (int i = 0; i < 10; i++)
                                            {
                                                hasData = ADLX.GetAdlxTelemetry((uint)index, 1000u, ref adlxTelemetryData);
                                                Thread.Sleep(500);

                                                if (hasData)
                                                    break;
                                            }
                                        }

                                        // Initial 1 second history length
                                        if (hasData)
                                        {
                                            hardware.Add(new ATIGPU(
                                                  deviceInfo.GpuName,
                                                  index,
                                                  0,
                                                  0,
                                                  IntPtr.Zero,
                                                  AdlGeneration.ADLX,
                                                  deviceInfo.DriverPath,
                                                  settings,
                                                  sensorConfig,
                                                  processService));

                                            check = true;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return check;
            }
            catch (DllNotFoundException) { throw; }
            catch (EntryPointNotFoundException) { throw; }
            catch (AccessViolationException) { throw; }
            catch { return false; }
        }

        public void RemoveIntegratedGpu()
        {
            var internalGpu = hardware.FirstOrDefault(gpu => gpu.Name == "AMD Radeon(TM) Graphics");

            if (internalGpu != null)
                hardware.Remove(internalGpu);
        }

        public void RemoveDefaultAdapter()
        {
            if (!hardware.IsNullOrEmpty())
            {
                var defaultAdapter = hardware.FirstOrDefault(gpu => gpu.Name.Contains("Microsoft"));

                if (defaultAdapter != null)
                    hardware.Remove(defaultAdapter);
            }
        }

        public IHardware[] Hardware
        {
            get
            {
                return hardware.ToArray();
            }
        }

        public string GetReport()
        {
            return string.Empty;
        }

        public void Close()
        {
            try
            {
                foreach (ATIGPU gpu in hardware)
                    gpu.Close();

                if (ADLX.IsInitialized)
                    ADLX.CloseAMDGpuLib();
            }
            catch (AccessViolationException ex) { Log.Logger.Error(ex, $"Access violation exception while closing ADLX."); }
            catch (Exception ex) { Log.Logger.Error(ex, "Error while closing ADLX"); }
        }
    }
}
