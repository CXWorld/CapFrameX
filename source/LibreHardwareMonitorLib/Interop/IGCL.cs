using System.Runtime.InteropServices;

namespace LibreHardwareMonitor.Interop
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
        public uint Adapter_Property_Flag;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IgclTelemetryItem
    {
        public bool supported;
        public double value;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IgclPsuRail
    {
        // ctl_psu_type_t: PCIe slot, 6-pin or 8-pin connector (0 = unknown)
        public int type;

        // Average power over the last sample interval (W)
        public IgclTelemetryItem power;

        // Voltage (V)
        public IgclTelemetryItem voltage;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IgclTelemetryData
    {
        // GPU TDP
        public bool gpuEnergySupported;
        public double gpuEnergyValue;

        // GPU TBP
        public bool totalCardEnergySupported;
        public double totalCardEnergyValue;

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

        // The items below require ctl_power_telemetry_t version 1 or newer.

        // GPU VR Temperature
        public bool gpuVrTemperatureSupported;
        public double gpuVrTemperatureValue;

        // VRAM VR Temperature
        public bool vramVrTemperatureSupported;
        public double vramVrTemperatureValue;

        // System Agent VR Temperature
        public bool saVrTemperatureSupported;
        public double saVrTemperatureValue;

        // GPU Effective Frequency
        public bool gpuEffectiveClockSupported;
        public double gpuEffectiveClockValue;

        // GPU Overvoltage (% of the maximum over-voltage increment)
        public bool gpuOverVoltagePercentSupported;
        public double gpuOverVoltagePercentValue;

        // GPU Power (% of the default maximum power)
        public bool gpuPowerPercentSupported;
        public double gpuPowerPercentValue;

        // GPU Temperature (% of the thermal margin)
        public bool gpuTemperaturePercentSupported;
        public double gpuTemperaturePercentValue;

        // VRAM Read Bandwidth (GB/s)
        public bool vramReadBandwidthGBpsSupported;
        public double vramReadBandwidthGBpsValue;

        // VRAM Write Bandwidth (GB/s)
        public bool vramWriteBandwidthGBpsSupported;
        public double vramWriteBandwidthGBpsValue;

        // Fans 2..5; fan 1 is fanSpeedSupported/fanSpeedValue above.
        public IgclTelemetryItem fan2Speed;
        public IgclTelemetryItem fan3Speed;
        public IgclTelemetryItem fan4Speed;
        public IgclTelemetryItem fan5Speed;

        // Power supply rails (CTL_PSU_COUNT)
        public IgclPsuRail psu1;
        public IgclPsuRail psu2;
        public IgclPsuRail psu3;
        public IgclPsuRail psu4;
        public IgclPsuRail psu5;
    }

    internal class IGCL
    {
        public const int CTL_MAX_DEVICE_NAME_LEN = 100;
        public const int CTL_MAX_DRIVER_VERSION_LEN = 25;

        // ctl_psu_type_t
        public const int CTL_PSU_TYPE_PSU_PCIE = 1;
        public const int CTL_PSU_TYPE_PSU_6PIN = 2;
        public const int CTL_PSU_TYPE_PSU_8PIN = 3;

        public static int Intel_VENDOR_ID = 0x8086;

        public static bool IsInitialized { get; internal set; }

        internal static bool IntializeIntelGpuLib()
        {
            return IsInitialized = IntializeIgcl();
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
