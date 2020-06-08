using System.ComponentModel;

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
		[Description("GPU power (W)")]
		GpuPower,
		[Description("GPU temp. (°C)")]
		GpuTemp,
		[Description("GPU VRAM usage (MB)")]
		VRamUsage,
		[Description("RAM usage (GB)")]
		RamUsage
	}
}
