namespace CapFrameX.Data.Session.Contracts
{
	public interface ISessionSensorData
	{
		int[] GpuPower { get; set; }
		int[] GpuTemp { get; set; }
		int[] GpuUsage { get; set; }
		int[] CpuUsage { get; set; }
		int[] CpuMaxThreadUsage { get; set; }
		int[] CpuPower { get; set; }
		int[] CpuTemp { get; set; }
		bool[] IsInGpuLimit { get; set; }
		double[] MeasureTime { get; set; }
		double[] RamUsage { get; set; }
		int[] VRamUsage { get; set; }
		double[] BetweenMeasureTimes { get; set; }
	}
}