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

        public ATIGroup(ISettings settings, ISensorConfig sensorConfig, IProcessService processService, bool useAdlFallback)
        {
            try
            {
                if (useAdlFallback || !TryUseAdlx(settings, sensorConfig, processService))
                {
                    Log.Information("Failed to load ADLX, use ADL fallback instead.");
                    UseAdlFallback(settings, sensorConfig, processService);
                }
            }
            catch (DllNotFoundException ex) { Log.Logger.Error(ex, "AMD GPU lib DLL error."); }
            catch (EntryPointNotFoundException ex) { Log.Logger.Error(ex, "AMD GPU lib entry point error."); }
            catch (AccessViolationException ex) { Log.Logger.Error(ex, $"Access violation exception while accessing ADLX."); }
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

        private void UseAdlFallback(ISettings settings, ISensorConfig sensorConfig, IProcessService processService)
        {
            int adlStatus = ADL.ADL_Main_Control_Create(1);
            int adl2Status = ADL.ADL2_Main_Control_Create(1, out context);

            if (adlStatus == ADL.ADL_OK || adl2Status == ADL.ADL_OK)
            {
                int numberOfAdapters = 0;
                ADL.ADL_Adapter_NumberOfAdapters_Get(ref numberOfAdapters);

                Log.Information($"ADL_Adapter_NumberOfAdapters_Get: {numberOfAdapters}");

                if (numberOfAdapters > 0)
                {
                    ADLAdapterInfo[] adapterInfo = new ADLAdapterInfo[numberOfAdapters];
                    if (ADL.ADL_Adapter_AdapterInfo_Get(adapterInfo) == ADL.ADL_OK)
                    {
                        for (int i = 0; i < numberOfAdapters; i++)
                        {
                            ADL.ADL_Adapter_Active_Get(adapterInfo[i].AdapterIndex,
                              out int isActive);
                            ADL.ADL_Adapter_ID_Get(adapterInfo[i].AdapterIndex, out _);

                            if (!string.IsNullOrEmpty(adapterInfo[i].UDID) &&
                              adapterInfo[i].VendorID == ADL.ATI_VENDOR_ID)
                            {
                                bool found = false;
                                foreach (ATIGPU gpu in hardware)
                                {
                                    if (gpu.BusNumber == adapterInfo[i].BusNumber &&
                                      gpu.DeviceNumber == adapterInfo[i].DeviceNumber)
                                    {
                                        found = true;
                                        break;
                                    }
                                }

                                if (!found)
                                {
                                    var adapterName = adapterInfo[i].AdapterName.Trim();
                                    hardware.Add(new ATIGPU(
                                      adapterName,
                                      adapterInfo[i].AdapterIndex,
                                      adapterInfo[i].BusNumber,
                                      adapterInfo[i].DeviceNumber,
                                      context,
                                      AdlGeneration.ADL,
                                      string.Empty,
                                      settings,
                                      sensorConfig,
                                      processService));
                                }
                            }
                        }
                    }
                    else
                    {
                        Log.Logger.Error($"ADL_Adapter_AdapterInfo_Get: error");
                    }
                }
            }
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

                if (context != IntPtr.Zero)
                {
                    ADL.ADL2_Main_Control_Destroy(context);
                    context = IntPtr.Zero;
                }

                ADL.ADL_Main_Control_Destroy();

                if (ADLX.IsInitialized)
                    ADLX.CloseAMDGpuLib();
            }
            catch (AccessViolationException ex) { Log.Logger.Error(ex, $"Access violation exception while closing ADLX."); }
            catch (Exception ex) { Log.Logger.Error(ex, "Error while closing ADL/ADLX"); }
        }
    }
}
