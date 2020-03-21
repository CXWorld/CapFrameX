using CapFrameX.Data.Session.Contracts;
using Newtonsoft.Json;

namespace CapFrameX.Data.Session.Classes
{
	public class SessionSensorData : ISessionSensorData
	{
		public double[] MeasureTime { get; set; }
		public int[] CpuUsage { get; set; }
		public int[] CpuMaxThreadUsage { get; set; }
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] CpuPower { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] CpuTemp { get; set; } = new int[] { };
		public int[] GpuUsage { get; set; }
		public double[] RamUsage { get; set; }
		public bool[] IsInGpuLimit { get; set; }
		public int[] GpuPower { get; set; }
		public int[] GpuTemp { get; set; }
		public int[] VRamUsage { get; set; }

	}
}
