using CapFrameX.Capture.Contracts;
using System;
using System.IO;

namespace CapFrameX.PresentMonInterface
{
    public static class CaptureServiceConfiguration
    {
        public static string PresentMonAppName = "PresentMon-2.3.1-x64";

        public static IServiceStartInfo GetServiceStartInfo(string arguments)
        {
            var startInfo = new PresentMonStartInfo
            {
                FileName = Path.Combine("PresentMon", PresentMonAppName + ".exe"),
                Arguments = arguments,
                CreateNoWindow = true,
                RunWithAdminRights = true,
                RedirectStandardOutput = false,
                UseShellExecute = false
            };

            return startInfo;
        }

        public static string GetCaptureFilename(string processName)
        {
            if (processName.Contains("?"))
                processName = string.Empty;

            DateTime now = DateTime.Now;
            string dateTimeFormat = $"{now.Year}-{now.Month:d2}-" +
                $"{now.Day:d2}T{now.Hour}{now.Minute}{now.Second}";
            return $"CapFrameX-{processName}.exe-{dateTimeFormat}.json";
        }
    }
}
