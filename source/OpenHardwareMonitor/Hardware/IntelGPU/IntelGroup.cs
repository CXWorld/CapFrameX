using CapFrameX.Monitoring.Contracts;
using System;
using System.Collections.Generic;

namespace OpenHardwareMonitor.Hardware.IntelGPU
{
    internal class IntelGroup : IGroup
    {
        private readonly List<Hardware> hardware = new List<Hardware>();

        public IntelGroup(ISettings settings, ISensorConfig sensorConfig, IProcessService processService)
        {
            try
            {
                int igclStatus = IGCL.CtlInit();

                if (igclStatus == IGCL.ADL_OK)
                {
                    int numberOfAdapters = 0;
                    IGCL.ADL_Adapter_NumberOfAdapters_Get(ref numberOfAdapters);

                    if (numberOfAdapters > 0)
                    {
                        IGCLAdapterInfo[] adapterInfo = new IGCLAdapterInfo[numberOfAdapters];
                        if (IGCL.ADL_Adapter_AdapterInfo_Get(adapterInfo) == IGCL.ADL_OK)
                            for (int i = 0; i < numberOfAdapters; i++)
                            {
                                IGCL.ADL_Adapter_Active_Get(adapterInfo[i].AdapterIndex,
                                  out int isActive);
                                IGCL.ADL_Adapter_ID_Get(adapterInfo[i].AdapterIndex,
                                  out int adapterID);

                                if (!string.IsNullOrEmpty(adapterInfo[i].LUID) &&
                                  adapterInfo[i].VendorID == IGCL.Intel_VENDOR_ID)
                                {
                                    bool found = false;
                                    foreach (IntelGPU gpu in hardware)
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
                                        hardware.Add(new IntelGPU(
                                          adapterInfo[i].AdapterName,
                                          adapterInfo[i].AdapterIndex,
                                          adapterInfo[i].BusNumber,
                                          adapterInfo[i].DeviceNumber,
                                          settings,
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
            foreach (Hardware gpu in hardware)
                gpu.Close();

            if (IGCL.IsInitialized)
            {
                IGCL.CtlClose();
            }
        }
    }
}
