namespace CapFrameX.Contracts.Data
{
	public interface ISessionRun
	{
		string Hash { get; set; }
		bool IsVR { get; set; }
		ISessionCaptureData CaptureData { get; set; }
		ISessionSensorData SensorData { get; set; }
	}
}