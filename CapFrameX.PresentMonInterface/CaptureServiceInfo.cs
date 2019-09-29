using System;

namespace CapFrameX.PresentMonInterface
{
    public static class CaptureServiceInfo
    {
		// PresentMon64-1.5.2
		public static string Version => "1.5.2";

        public static bool IsCompatibleWithRunningOS
        {
            get { return Environment.OSVersion.Version.Major >= 10; }
        }
    }
}

