using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.IntelGPU
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct IgclDeviceInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = IGCL.CTL_MAX_DEVICE_NAME_LEN)]
        public char[] DeviceName;
        public int AdapterID;
        public uint Pci_vendor_id;
        public uint Pci_device_id;
        public uint Rev_id;
        public ulong DriverVersion;
        public bool Isvalid;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IgclTelemetryData
    {
        // GPU TDP
        public bool gpuEnergyCounterSupported;
        public float gpuEnergyCounterValue;

        // GPU Voltage
        public bool gpuVoltageSupported;
        public float gpuVoltagValue;

        // GPU Core Frequency
        public bool gpuCurrentClockFrequencySupported;
        public float gpuCurrentClockFrequencyValue;

        // GPU Core Temperature
        public bool gpuCurrentTemperatureSupported;
        public float gpuCurrentTemperatureValue;

        // GPU Usage
        public bool globalActivityCounterSupported;
        public float globalActivityCounterValue;

        // Render Engine Usage
        public bool renderComputeActivityCounterSupported;
        public float renderComputeActivityCounterValue;

        // Media Engine Usage
        public bool mediaActivityCounterSupported;
        public float mediaActivityCounterValue;

        // VRAM Power Consumption
        public bool vramEnergyCounterSupported;
        public float vramEnergyCounterValue;

        // VRAM Voltage
        public bool vramVoltageSupported;
        public float vramVoltageValue;

        // VRAM Frequency
        public bool vramCurrentClockFrequencySupported;
        public float vramCurrentClockFrequencyValue;

        // VRAM Read Bandwidth
        public bool vramReadBandwidthCounterSupported;
        public float vramReadBandwidthCounterValue;

        // VRAM Write Bandwidth
        public bool vramWriteBandwidthCounterSupported;
        public float vramWriteBandwidthCounterValue;

        // VRAM Temperature
        public bool vramCurrentTemperatureSupported;
        public float vramCurrentTemperatureValue;

        // Fanspeed (n Fans)
        public bool fanSpeedSupported;
        public float fanSpeedValue;
    }

    internal class IGCL
    {
        public const int CTL_MAX_DEVICE_NAME_LEN = 100;

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
        public static extern IgclDeviceInfo GetDeviceInfo(uint index);

        [DllImport("CapFrameX.IGCL.dll")]
        public static extern IgclTelemetryData GetIgclTelemetryData(uint index);
    }
}
