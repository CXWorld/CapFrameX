using CapFrameX.Contracts.Data;

namespace CapFrameX.Data
{
	public class SessionSensorData : ISessionSensorData
	{
		public double[] MeasureTime { get; set; }
		public int[] CpuUsage { get; set; }
		public int[] CpuMaxThreadUsage { get; set; }
		public int[] GpuUsage { get; set; }
		public double[] RamUsage { get; set; }
		public bool[] IsInGpuLimit { get; set; }
		public int[] GpuPower { get; set; }
		public int[] GpuTemp { get; set; }
	}
}
