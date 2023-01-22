using CapFrameX.Monitoring.Contracts;
using Serilog;
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
                            var deviceInfo = new IgclDeviceInfo();
                            if (IGCL.GetDeviceInfo((uint)index, ref deviceInfo))
                            {
                                if (deviceInfo.Pci_device_id != 0 &&
                                  deviceInfo.Pci_vendor_id == IGCL.Intel_VENDOR_ID)
                                {
                                    // Filter integrated graphics
                                    if (deviceInfo.DeviceName != "Intel(R) UHD Graphics")
                                    {
                                        var igclTelemetryData = new IgclTelemetryData();
                                        if (IGCL.GetIgclTelemetryData((uint)index, ref igclTelemetryData))
                                        {
                                            hardware.Add(new IntelGPU(
                                                  deviceInfo.DeviceName,
                                                  index,
                                                  deviceInfo.AdapterID,
                                                  (int)deviceInfo.Pci_device_id,
                                                  (int)IGCL.GetBusWidth((uint)index),
                                                  deviceInfo.DriverVersion,
                                                  settings,
                                                  sensorConfig,
                                                  processService));

                                            Log.Logger.Information($"Intel graphics card detected:: {deviceInfo.DeviceName}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (DllNotFoundException ex) { Log.Logger.Error(ex, $"Error while loading CapFrameX.IGCL.dll."); }
			catch (AccessViolationException ex) { Log.Logger.Error(ex, $"Access violation exception while accessing Intel GPU lib."); }
			catch (Exception ex) { Log.Logger.Error(ex, $"Error while getting Intel GPU device info."); }
        }

        public IHardware[] Hardware => hardware.ToArray();

        public string GetReport() => string.Empty;

        public void Close()
        {
            foreach (Hardware gpu in hardware)
                gpu.Close();

            if (IGCL.IsInitialized)
            {
                try
                {
                    IGCL.CloseIntelGpuLib();
                }
				catch (AccessViolationException ex) { Log.Logger.Error(ex, $"Access violation exception while closing Intel GPU lib."); }
				catch (Exception ex) { Log.Logger.Error(ex, $"Error while closing Intel GPU lib."); }
			}
        }
    }
}
