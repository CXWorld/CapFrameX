using CapFrameX.Capture.Contracts;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CapFrameX.PresentMonInterface
{
    public static class CaptureServiceConfiguration
    {
        public static string PresentMonAppName = "PresentMon-2.6.0-x64";

        /// <summary>
        /// Full path of the bundled PresentMon executable.
        /// </summary>
        public static string GetPresentMonPath()
            => Path.Combine(AppContext.BaseDirectory, "PresentMon", PresentMonAppName + ".exe");

        /// <summary>
        /// Version of the bundled PresentMon. The executable carries no version resource, so the
        /// version comes from its file name.
        /// </summary>
        public static string GetPresentMonVersion() => ParseVersionFromAppName(PresentMonAppName);

        internal static string ParseVersionFromAppName(string appName)
        {
            if (string.IsNullOrEmpty(appName))
                return null;

            Match match = Regex.Match(appName, @"\d+(?:\.\d+)+");
            return match.Success ? match.Value : null;
        }

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
