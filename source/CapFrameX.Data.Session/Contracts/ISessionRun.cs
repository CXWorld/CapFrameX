namespace CapFrameX.Data.Session.Contracts
{
	public interface ISessionRun
	{
		string Hash { get; set; }
		string PresentMonRuntime { get; set; }
		ISessionCaptureData CaptureData { get; set; }
		ISessionSensorData SensorData { get; set; }
        ISessionSensorData2 SensorData2 { get; set; }
		float[] RTSSFrameTimes { get; set; }
        float[] PmdGpuPower { get; set; }
        float[] PmdCpuPower { get; set; }
		int SampleTime { get; set; }
    }
}