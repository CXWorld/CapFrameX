using CapFrameX.Capture.Contracts;
using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace CapFrameX.PresentMonInterface
{
    public static class CaptureServiceConfiguration
    {
        // CapFrameX's own PresentMon build: Intel's 2.6.0 release plus fixes, see PresentMon\README.md.
        public static string PresentMonAppName = "CX-PresentMon-2.6.1-x64";

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
            => GetCaptureFilename(processName, DateTime.Now);

        public static string GetCaptureFilename(string processName, DateTime time)
        {
            if (processName.Contains("?"))
                processName = string.Empty;

            return $"CapFrameX-{processName}.exe-{FormatFileTimestamp(time)}.json";
        }

        /// <summary>
        /// The time stamp of a capture file name. The time is zero-padded: unpadded, 1:11:11, 11:01:11
        /// and 11:11:01 all read "T11111", and saving a later capture of that day overwrote the earlier one.
        /// </summary>
        public static string FormatFileTimestamp(DateTime time)
            => time.ToString("yyyy-MM-dd'T'HHmmss", CultureInfo.InvariantCulture);
    }
}
