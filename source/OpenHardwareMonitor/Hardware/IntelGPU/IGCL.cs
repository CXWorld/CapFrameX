using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.IntelGPU
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct IgclDeviceInfo
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = IGCL.CTL_MAX_DEVICE_NAME_LEN)]
        public string DeviceName;
        public int AdapterID;
        public uint Pci_vendor_id;
        public uint Pci_device_id;
        public uint Rev_id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = IGCL.CTL_MAX_DRIVER_VERSION_LEN)]
        public string DriverVersion;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IgclTelemetryData
    {
        // GPU TDP
        public bool gpuEnergySupported;
        public double gpuEnergyValue;

        // GPU Voltage
        public bool gpuVoltageSupported;
        public double gpuVoltagValue;

        // GPU Core Frequency
        public bool gpuCurrentClockFrequencySupported;
        public double gpuCurrentClockFrequencyValue;

        // GPU Core Temperature
        public bool gpuCurrentTemperatureSupported;
        public double gpuCurrentTemperatureValue;

        // GPU Usage
        public bool globalActivitySupported;
        public double globalActivityValue;

        // Render Engine Usage
        public bool renderComputeActivitySupported;
        public double renderComputeActivityValue;

        // Media Engine Usage
        public bool mediaActivitySupported;
        public double mediaActivityValue;

        // VRAM Power Consumption
        public bool vramEnergySupported;
        public double vramEnergyValue;

        // VRAM Voltage
        public bool vramVoltageSupported;
        public double vramVoltageValue;

        // VRAM Frequency
        public bool vramCurrentClockFrequencySupported;
        public double vramCurrentClockFrequencyValue;

        // VRAM Read Bandwidth
        public bool vramReadBandwidthSupported;
        public double vramReadBandwidthValue;

        // VRAM Write Bandwidth
        public bool vramWriteBandwidthSupported;
        public double vramWriteBandwidthValue;

        // VRAM Temperature
        public bool vramCurrentTemperatureSupported;
        public double vramCurrentTemperatureValue;

        // Fanspeed (n Fans)
        public bool fanSpeedSupported;
        public double fanSpeedValue;
    }

    internal class IGCL
    {
        public const int CTL_MAX_DEVICE_NAME_LEN = 100;
        public const int CTL_MAX_DRIVER_VERSION_LEN = 25;

        public static int Intel_VENDOR_ID = 0x8086;

        public static bool IsInitialized { get; internal set; }

        internal static bool IntializeIntelGpuLib()
        {
            return IsInitialized = IntializeIgcl();
        }

        internal static void CloseIntelGpuLib()
        {
            CloseIgcl();
        }

        [DllImport("CapFrameX.IGCL.dll")]
        public static extern bool IntializeIgcl();

        [DllImport("CapFrameX.IGCL.dll")]
        public static extern void CloseIgcl();

        [DllImport("CapFrameX.IGCL.dll")]
        public static extern uint GetAdpaterCount();

        [DllImport("CapFrameX.IGCL.dll")]
        public static extern uint GetBusWidth(uint index);

        [DllImport("CapFrameX.IGCL.dll")]
        public static extern bool GetDeviceInfo(uint index, ref IgclDeviceInfo igclDeviceInfo);

        [DllImport("CapFrameX.IGCL.dll")]
        public static extern bool GetIgclTelemetryData(uint index, ref IgclTelemetryData igclTelemetryData);
    }
}
