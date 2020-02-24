using CapFrameX.Contracts.Data;
using Newtonsoft.Json;

namespace CapFrameX.Data
{
	public class SessionRun : ISessionRun
	{
		public string Hash { get; set; }
		public bool IsVR { get; set; }
		[JsonProperty("CaptureData")]
		public ISessionCaptureData CaptureData { get; set; }
		[JsonProperty("SensorData")]
		public ISessionSensorData SensorData { get; set; }
	}
}
