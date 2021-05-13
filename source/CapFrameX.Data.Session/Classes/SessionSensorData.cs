using CapFrameX.Data.Session.Contracts;
using CapFrameX.Data.Session.Converters;
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
		public int[] GpuClock{ get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public double[] RamUsage { get; set; } = new double[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] GpuPower { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] GpuTemp { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public int[] VRamUsage { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public double[] VRamUsageGB { get; set; } = new double[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore, ItemConverterType = typeof(BoolToZeroOrOneConverter))]
		public int[] GPUPowerLimit { get; set; } = new int[] { };
		[JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
		public double[] BetweenMeasureTimes { get; set; } = new double[] { };
	}
}
