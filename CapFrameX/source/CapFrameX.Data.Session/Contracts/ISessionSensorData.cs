using System;

namespace CapFrameX.Data.Session.Contracts
{
	public interface ISessionSensorData
	{
		[SensorDataExport("Time(s)")]
		double[] MeasureTime { get; set; }
		[SensorDataExport("CPU load(%)")]
		int[] CpuUsage { get; set; }
		[SensorDataExport("CPU max thread load(%)")]
		int[] CpuMaxThreadUsage { get; set; }
		[SensorDataExport("CPU max clock(MHz)")]
		int[] CpuMaxClock { get; set; }
		[SensorDataExport("CPU power(W)")]
		int[] CpuPower { get; set; }
		[SensorDataExport("CPU temp(°C)")]
		int[] CpuTemp { get; set; }
		[SensorDataExport("GPU load(%)")]
		int[] GpuUsage { get; set; }
		[SensorDataExport("GPU clock (MHz)")]
		int[] GpuClock { get; set; }
		[SensorDataExport("GPU power (W)")]
		int[] GpuPower { get; set; }
		[SensorDataExport("GPU TBP Sim (W)")]
		int[] GpuTBPSim { get; set; }
		[SensorDataExport("GPU temp(°C)")]
		int[] GpuTemp { get; set; }
		[SensorDataExport("RAM usage(GB)")]
		double[] RamUsage { get; set; }
		[SensorDataExport("VRAM usage(MB)")]
		int[] VRamUsage { get; set; }
		[SensorDataExport("VRAM usage(GB)")]
		double[] VRamUsageGB { get; set; }
		[SensorDataExport("GPU power limit")]
		int[] GPUPowerLimit { get; set; }
		double[] BetweenMeasureTimes { get; set; }
	}

	public class SensorDataExportAttribute: Attribute {
		public string Description;
		public SensorDataExportAttribute(string desc)
		{
			Description = desc;
		}
	}
}