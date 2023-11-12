using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace OpenHardwareMonitor.Hardware.ATI
{
	[StructLayout(LayoutKind.Sequential)]
	internal struct AdlxDeviceInfo
	{
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ADLX.MAX_GPU_NAME_LEN)]
		public string GpuName;
		// Undefinied = 0, Integrated = 1, Discrete = 2
		public uint GpuType;
		public int Id;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ADLX.MAX_VENDOR_ID_LEN)]
		public string VendorId;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = ADLX.MAX_DRIVER_PATH_LEN)]
		public string DriverPath;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct AdlxTelemetryData
	{
		// GPU Usage
		public bool gpuUsageSupported;
		public double gpuUsageValue;

		// GPU Core Frequency
		public bool gpuClockSpeedSupported;
		public double gpuClockSpeedValue;

		// GPU VRAM Frequency
		public bool gpuVRAMClockSpeedSupported;
		public double gpuVRAMClockSpeedValue;

		// GPU Core Temperature
		public bool gpuTemperatureSupported;
		public double gpuTemperatureValue;

		// GPU Hotspot Temperature
		public bool gpuHotspotTemperatureSupported;
		public double gpuHotspotTemperatureValue;

		// GPU Power
		public bool gpuPowerSupported;
		public double gpuPowerValue;

		// Fan Speed
		public bool gpuFanSpeedSupported;
		public double gpuFanSpeedValue;

		// VRAM Usage
		public bool gpuVramSupported;
		public double gpuVramValue;

		// GPU Voltage
		public bool gpuVoltageSupported;
		public double gpuVoltageValue;

		// GPU TBP
		public bool gpuTotalBoardPowerSupported;
		public double gpuTotalBoardPowerValue;
	}

	internal class ADLX
	{
		public const int MAX_DRIVER_PATH_LEN = 200;
		public const int MAX_GPU_NAME_LEN = 100;
		public const int MAX_VENDOR_ID_LEN = 20;

		public static string AMD_VENDOR_ID = "1002";

		public static bool IsInitialized { get; internal set; }

		internal static bool IntializeAMDGpuLib()
		{
			return IsInitialized = IntializeAdlx();
		}

		internal static void CloseAMDGpuLib()
		{
			CloseAdlx();
		}

		[HandleProcessCorruptedStateExceptions]
		[DllImport("CapFrameX.ADLX.dll")]
		public static extern bool IntializeAdlx();

		[HandleProcessCorruptedStateExceptions]
		[DllImport("CapFrameX.ADLX.dll")]
		public static extern void CloseAdlx();

		[HandleProcessCorruptedStateExceptions]
		[DllImport("CapFrameX.ADLX.dll")]
		public static extern uint GetAtiAdpaterCount();

		[HandleProcessCorruptedStateExceptions]
		[DllImport("CapFrameX.ADLX.dll")]
		public static extern bool GetAdlxDeviceInfo(uint index, ref AdlxDeviceInfo adlxDeviceInfo);

		[HandleProcessCorruptedStateExceptions]
		[DllImport("CapFrameX.ADLX.dll")]
		public static extern bool GetAdlxTelemetry(uint index, uint historyLength, ref AdlxTelemetryData adlxTelemetryData);
	}
}
