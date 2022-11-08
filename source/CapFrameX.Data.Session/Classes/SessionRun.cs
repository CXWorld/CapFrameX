using CapFrameX.Data.Session.Contracts;
using CapFrameX.Data.Session.Converters;
using Newtonsoft.Json;

namespace CapFrameX.Data.Session.Classes
{
	public class SessionRun : ISessionRun
	{
		public string Hash { get; set; }
		public string PresentMonRuntime { get; set; }
		[JsonProperty("CaptureData")]
		[JsonConverter(typeof(ConcreteTypeConverter<SessionCaptureData>))]
		public ISessionCaptureData CaptureData { get; set; }
		[JsonProperty("SensorData")]
		[JsonConverter(typeof(ConcreteTypeConverter<SessionSensorData>))]
		public ISessionSensorData SensorData { get; set; }
		[JsonProperty("SensorData2")]
		[JsonConverter(typeof(SessionSensorData2TypeConverter))]
		public ISessionSensorData2 SensorData2 { get; set; }
		public float[] RTSSFrameTimes { get; set; }
        public float[] PmdGpuPower { get; set; }
        public float[] PmdCpuPower { get; set; }
        public float[] PmdSystemPower { get; set; }
        public int SampleTime { get; set; }

        [JsonConstructor]
		public SessionRun(SessionCaptureData captureData, SessionSensorData sensorData)
		{
			CaptureData = captureData;
			SensorData = sensorData;
		}

		public SessionRun() { }
	}
}
