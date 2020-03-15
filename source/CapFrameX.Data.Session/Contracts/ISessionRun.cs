﻿namespace CapFrameX.Data.Session.Contracts
{
	public interface ISessionRun
	{
		string Hash { get; set; }
		bool IsVR { get; set; }
		ISessionCaptureData CaptureData { get; set; }
		ISessionSensorData SensorData { get; set; }
	}
}