﻿using System.ComponentModel;

namespace CapFrameX.Sensor.Reporting
{
	public enum EReportSensorName
	{
		[Description("CPU load (%)")]
		CpuUsage,
		[Description("CPU max thread load (%)")]
		CpuMaxThreadUsage,
		[Description("CPU max clock (MHz)")]
		CpuMaxClock,
		[Description("CPU power (W)")]
		CpuPower,
		[Description("CPU temp (°C)")]
		CpuTemp,
		[Description("GPU load (%)")]
		GpuUsage,
		[Description("Time in GPU load limit (%)")]
		GpuLoadLimit,
		[Description("GPU clock (MHz)")]
		GpuClock,
		[Description("GPU power (W)")]
		GpuPower,
		[Description("GPU TBP Sim (W)")]
		GpuTBPSim,
		[Description("GPU temp. (°C)")]
		GpuTemp,
		[Description("GPU VRAM usage (MB)")]
		VRamUsage,
		[Description("GPU VRAM usage (GB)")]
		VRamUsageGB,
		[Description("RAM usage (GB)")]
		RamUsage,
	}
}
