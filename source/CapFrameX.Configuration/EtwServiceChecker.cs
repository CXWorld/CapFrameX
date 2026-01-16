using System;
using System.Diagnostics;

namespace CapFrameX.Configuration
{
    /// <summary>
    /// Checks for conflicting ETW trace sessions that may interfere with PresentMon.
    /// </summary>
    public static class EtwServiceChecker
    {
        private const string FrameViewServiceName = "FrameViewService";

        /// <summary>
        /// Checks if FrameViewService ETW session is running.
        /// </summary>
        /// <returns>True if FrameViewService is detected, false otherwise.</returns>
        public static bool IsFrameViewServiceRunning()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "logman",
                    Arguments = "query -ets",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process == null)
                        return false;

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    return output.IndexOf(FrameViewServiceName, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (Exception)
            {
                // If we can't check, assume it's not running
                return false;
            }
        }
    }
}
