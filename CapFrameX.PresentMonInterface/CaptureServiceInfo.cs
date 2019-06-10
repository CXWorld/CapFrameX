using System;

namespace CapFrameX.PresentMonInterface
{
    public static class CaptureServiceInfo
    {
        public static string Version => "1.4.0";

        public static bool IsCompatibleWithRunningOS
        {
            get { return Environment.OSVersion.Version.Major >= 10; }
        }
    }
}

