namespace CapFrameX.Contracts.Data
{
	public interface ISessionRun
	{
		ISessionCaptureData CaptureData { get; set; }
		string Filename { get; set; }
		bool IsVR { get; set; }
		double LastFrameTime { get; }
		string Path { get; set; }
		ISessionSensorData SensorData { get; set; }
		int ValidReproFrames { get; set; }
		int WarpMissesCount { get; }
	}
}