using System;

namespace CapFrameX.PresentMonInterface
{
	public static class CaptureServiceInfo
	{
		public static bool IsCompatibleWithRunningOS
		{
			get { return Environment.OSVersion.Version >= new Version(6, 1); }
		}
	}
}

