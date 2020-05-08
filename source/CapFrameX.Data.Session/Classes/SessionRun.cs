using CapFrameX.Data.Session.Contracts;
using CapFrameX.Data.Session.Converters;
using Newtonsoft.Json;

namespace CapFrameX.Data.Session.Classes
{
	public class SessionRun : ISessionRun
	{
		public string Hash { get; set; }
		public string PresentMonRuntime { get; set; }
		public bool IsVR { get; set; }
		[JsonProperty("CaptureData")]
		[JsonConverter(typeof(ConcreteTypeConverter<SessionCaptureData>))]
		public ISessionCaptureData CaptureData { get; set; }
		[JsonProperty("SensorData")]
		[JsonConverter(typeof(ConcreteTypeConverter<SessionSensorData>))]
		public ISessionSensorData SensorData { get; set; }

		[JsonConstructor]
		public SessionRun(SessionCaptureData captureData, SessionSensorData sensorData)
		{
			CaptureData = captureData;
			SensorData = sensorData;
		}

		public SessionRun() { }
	}
}
