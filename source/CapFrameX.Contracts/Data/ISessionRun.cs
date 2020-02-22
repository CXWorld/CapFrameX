namespace CapFrameX.Contracts.Data
{
	public interface ISessionRun
	{
		ISessionCaptureData CaptureData { get; set; }
		ISessionSensorData SensorData { get; set; }
	}
}