using CapFrameX.Extensions;
using CapFrameX.Monitoring.Contracts;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

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
                    int apdapterCount = (int)IGCL.GetAdpaterCount();

                    if (apdapterCount > 0)
                    {
                        for (int index = 0; index < apdapterCount; index++)
                        {
                            var deviceInfo = new IgclDeviceInfo();
                            if (IGCL.GetDeviceInfo((uint)index, ref deviceInfo))
                            {
                                if (deviceInfo.Pci_device_id != 0 &&
                                  deviceInfo.Pci_vendor_id == IGCL.Intel_VENDOR_ID)
                                {
                                    // Filter integrated graphics
                                    if (!deviceInfo.DeviceName.Contains("UHD Graphics")
                                        && !deviceInfo.DeviceName.Contains("Xe Graphics")
                                        && !deviceInfo.DeviceName.Contains("Intel(R) Graphics"))
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

        public void RemoveIntegratedGpu()
        {
            var internalGpu = hardware.FirstOrDefault(gpu => gpu.Name == "Intel(R) Arc(TM) Graphics");

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
