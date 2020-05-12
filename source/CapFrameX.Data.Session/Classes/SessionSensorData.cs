using CapFrameX.Data.Session.Contracts;
using Newtonsoft.Json;

namespace CapFrameX.Data.Session.Classes
{
	public class SessionSensorData : ISessionSensorData
	{
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public double[] MeasureTime { get; set; } = new double[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] CpuUsage { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] CpuMaxThreadUsage { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] CpuMaxClock { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] CpuPower { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] CpuTemp { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] GpuUsage { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public double[] RamUsage { get; set; } = new double[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public bool[] IsInGpuLimit { get; set; } = new bool[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public bool[] GpuPowerLimit { get; set; } = new bool[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] GpuPower { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] GpuTemp { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] VRamUsage { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public double[] BetweenMeasureTimes { get; set; } = new double[] { };
	}
}
