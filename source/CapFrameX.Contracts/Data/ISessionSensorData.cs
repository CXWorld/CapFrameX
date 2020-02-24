namespace CapFrameX.Contracts.Data
{
	public interface ISessionSensorData
	{
		int[] GpuPower { get; set; }
		int[] GpuTemp { get; set; }
		int[] GpuUsage { get; set; }
		int[] CpuUsage { get; set; }
		int[] CpuMaxThreadUsage { get; set; }
		bool[] IsInGpuLimit { get; set; }
		double[] MeasureTime { get; set; }
		double[] RamUsage { get; set; }
	}
}