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
		[JsonConverter(typeof(ConcreteTypeConverter<SessionSensorData2>))]
		public ISessionSensorData2 SensorData2 { get; set; }

		[JsonConstructor]
		public SessionRun(SessionCaptureData captureData, SessionSensorData sensorData)
		{
			CaptureData = captureData;
			SensorData = sensorData;
		}

		public SessionRun() { }
	}
}
