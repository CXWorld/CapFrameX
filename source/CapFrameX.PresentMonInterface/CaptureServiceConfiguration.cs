using CapFrameX.Contracts.PresentMonInterface;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace CapFrameX.PresentMonInterface
{
    public static class CaptureServiceConfiguration
    {

        public static IServiceStartInfo GetServiceStartInfo(string arguments)
        {
            var startInfo = new PresentMonStartInfo
            {
                FileName = Path.Combine("PresentMon", "PresentMon64-1.5.2.exe"),
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
            DateTime now = DateTime.Now;
            string dateTimeFormat = $"{now.Year}-{now.Month.ToString("d2")}-" +
				$"{now.Day.ToString("d2")}T{now.Hour}{now.Minute}{now.Second}";
            return $"CapFrameX-{processName}.exe-{dateTimeFormat}.json";
        }
    }
}
