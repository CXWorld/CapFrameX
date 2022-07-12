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
                if (IGCL.IntializeIntelGpuLib())
                {
                    int numberOfAdapters = (int)IGCL.GetAdpaterCount();

                    if (numberOfAdapters > 0)
                    {
                        for (int index = 0; index < numberOfAdapters; index++)
                        {
                            var deviceInfo = IGCL.GetDeviceInfo((uint)index);

                            if (deviceInfo.Pci_device_id != 0 &&
                              deviceInfo.Pci_vendor_id == IGCL.Intel_VENDOR_ID)
                            {
                                hardware.Add(new IntelGPU(
                                      new string(deviceInfo.DeviceName),
                                      index,
                                      deviceInfo.AdapterID,
                                      (int)deviceInfo.Pci_device_id,
                                      settings,
                                      sensorConfig,
                                      processService));
                            }
                        }
                    }
                }
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        public IHardware[] Hardware => hardware.ToArray();

        public string GetReport() => string.Empty;

        public void Close()
        {
            foreach (Hardware gpu in hardware)
                gpu.Close();

            if (IGCL.IsInitialized)
            {
                IGCL.CloseIntelGpuLib();
            }
        }
    }
}
