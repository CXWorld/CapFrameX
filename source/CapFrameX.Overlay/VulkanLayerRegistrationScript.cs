using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Overlay
{
    public sealed class VulkanLayerScriptResult
    {
        public VulkanLayerScriptResult(int? exitCode, bool timedOut, string output, string error)
        {
            ExitCode = exitCode;
            TimedOut = timedOut;
            Output = output;
            Error = error;
        }

        /// <summary>
        /// 0 when the layers are registered; null when the script was stopped after the timeout.
        /// </summary>
        public int? ExitCode { get; }

        public bool TimedOut { get; }

        public string Output { get; }

        public string Error { get; }
    }

    /// <summary>
    /// Runs <see cref="PortableVulkanLayerCheck.ScriptFileName"/> without a window. It inherits the
    /// rights of the caller: CapFrameX always runs elevated, so the script can write HKLM.
    /// </summary>
    public static class VulkanLayerRegistrationScript
    {
        public static VulkanLayerScriptResult Run(string scriptPath, TimeSpan timeout, params string[] arguments)
        {
            // Windows PowerShell by its full path: the script is written for 5.1, which every
            // Windows 10/11 has, and nothing on PATH gets to run elevated in its place.
            string powerShell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");

            // The script switches its redirected output to UTF-8.
            var startInfo = new ProcessStartInfo(powerShell)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            foreach (string argument in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", scriptPath })
                startInfo.ArgumentList.Add(argument);
            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            using (var process = Process.Start(startInfo))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception)
                    {
                        // It exited meanwhile or cannot be stopped; either way the result is a timeout.
                    }

                    return new VulkanLayerScriptResult(null, true, Collect(output), Collect(error));
                }

                // The parameterless wait also waits for the redirected streams to close.
                process.WaitForExit();
                return new VulkanLayerScriptResult(process.ExitCode, false, output.Result, error.Result);
            }
        }

        private static string Collect(Task<string> read)
            => read.Wait(TimeSpan.FromSeconds(2)) ? read.Result : string.Empty;
    }
}
