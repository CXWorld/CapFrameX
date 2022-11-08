/*
 
  This Source Code Form is subject to the terms of the Mozilla Public
  License, v. 2.0. If a copy of the MPL was not distributed with this
  file, You can obtain one at http://mozilla.org/MPL/2.0/.
 
  Copyright (C) 2009-2020 Michael Möller <mmoeller@openhardwaremonitor.org>
	
*/

using CapFrameX.Monitoring.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenHardwareMonitor.Hardware.ATI
{
    internal class ATIGroup : IGroup
    {
        private readonly List<ATIGPU> hardware = new List<ATIGPU>();

        private IntPtr context = IntPtr.Zero;

        public ATIGroup(ISettings settings, ISensorConfig sensorConfig, IProcessService processService)
        {
            try
            {
                int adlStatus = ADL.ADL_Main_Control_Create(1);
                int adl2Status = ADL.ADL2_Main_Control_Create(1, out context);

                if (adlStatus == ADL.ADL_OK || adl2Status == ADL.ADL_OK)
                {
                    int numberOfAdapters = 0;
                    ADL.ADL_Adapter_NumberOfAdapters_Get(ref numberOfAdapters);

                    if (numberOfAdapters > 0)
                    {
                        ADLAdapterInfo[] adapterInfo = new ADLAdapterInfo[numberOfAdapters];
                        if (ADL.ADL_Adapter_AdapterInfo_Get(adapterInfo) == ADL.ADL_OK)
                            for (int i = 0; i < numberOfAdapters; i++)
                            {
                                ADL.ADL_Adapter_Active_Get(adapterInfo[i].AdapterIndex,
                                  out int isActive);
                                ADL.ADL_Adapter_ID_Get(adapterInfo[i].AdapterIndex,
                                  out int adapterID);

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
                                          context, settings,
                                          sensorConfig,
                                          processService));
                                    }
                                }
                            }
                    }
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        public void RemoveInternalGpu()
        {
            var internalGpu = hardware.FirstOrDefault(gpu => gpu.Name == "AMD Radeon(TM) Graphics");

            if (internalGpu != null)
                hardware.Remove(internalGpu);
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
            }
            catch (Exception) { }
        }
    }
}
