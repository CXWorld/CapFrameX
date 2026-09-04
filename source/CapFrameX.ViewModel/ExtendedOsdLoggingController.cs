using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.ViewModel
{
    internal sealed class ExtendedOsdLoggingController
    {
        internal const string HookLogEnvironmentVariable = "CFX_HOOK_LOG";
        internal const string VulkanLayerLogEnvironmentVariable = "CFX_VKLAYER_LOG";
        internal const string PresentStatsEnvironmentVariable = "CFX_OSD_PRESENT_STATS";
        internal const string VerboseLogEnvironmentVariable = "CFX_OSD_VERBOSE_LOG";

        private const string PresentStatsPropertyName = "presentStats";
        private const string VerboseLogPropertyName = "verboseLog";
        private static readonly string[] LoggingEnvironmentVariables =
        {
            HookLogEnvironmentVariable, VulkanLayerLogEnvironmentVariable,
            PresentStatsEnvironmentVariable, VerboseLogEnvironmentVariable
        };
        private readonly string _debugConfigurationPath;
        private readonly Func<string, string> _getUserEnvironmentVariable;
        private readonly Action<string, string> _setUserEnvironmentVariable;
        private readonly Func<string, string> _getProcessEnvironmentVariable;
        private readonly Action<string, string> _setProcessEnvironmentVariable;

        internal ExtendedOsdLoggingController()
            : this(GetDefaultDebugConfigurationPath(), GetUserEnvironmentVariable,
                SetUserEnvironmentVariableWithSetx,
                name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process),
                (name, value) => Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process))
        {
        }

        internal ExtendedOsdLoggingController(string debugConfigurationPath,
            Func<string, string> getUserEnvironmentVariable,
            Action<string, string> setUserEnvironmentVariable,
            Func<string, string> getProcessEnvironmentVariable,
            Action<string, string> setProcessEnvironmentVariable)
        {
            _debugConfigurationPath = debugConfigurationPath ??
                throw new ArgumentNullException(nameof(debugConfigurationPath));
            _getUserEnvironmentVariable = getUserEnvironmentVariable ??
                throw new ArgumentNullException(nameof(getUserEnvironmentVariable));
            _setUserEnvironmentVariable = setUserEnvironmentVariable ??
                throw new ArgumentNullException(nameof(setUserEnvironmentVariable));
            _getProcessEnvironmentVariable = getProcessEnvironmentVariable ??
                throw new ArgumentNullException(nameof(getProcessEnvironmentVariable));
            _setProcessEnvironmentVariable = setProcessEnvironmentVariable ??
                throw new ArgumentNullException(nameof(setProcessEnvironmentVariable));
        }

        internal bool IsEnabled()
        {
            // Native modules also read inherited process variables. A user-scope value or the
            // JSON flag alone can otherwise report "off" while a native diagnostic is still on.
            foreach (string name in LoggingEnvironmentVariables)
            {
                if (IsEnabledValue(_getUserEnvironmentVariable(name)) ||
                    IsEnabledValue(_getProcessEnvironmentVariable(name)))
                    return true;
            }

            JObject configuration = ReadDebugConfiguration();
            return configuration.Value<bool?>(PresentStatsPropertyName) == true ||
                configuration.Value<bool?>(VerboseLogPropertyName) == true;
        }

        internal void SetEnabled(bool enabled)
        {
            // Parse before changing the environment so malformed hand-edited diagnostics are never
            // silently discarded and cannot leave the logging switches partially updated.
            JObject debugConfiguration = ReadDebugConfiguration();
            debugConfiguration[PresentStatsPropertyName] = enabled;
            debugConfiguration[VerboseLogPropertyName] = enabled;

            string value = enabled ? "1" : "0";
            var changedVariables = new List<(string Name, string UserValue, string ProcessValue)>();

            try
            {
                foreach (string name in LoggingEnvironmentVariables)
                {
                    changedVariables.Add((name, _getUserEnvironmentVariable(name),
                        _getProcessEnvironmentVariable(name)));
                    _setUserEnvironmentVariable(name, value);
                    _setProcessEnvironmentVariable(name, value);
                }
                WriteDebugConfiguration(debugConfiguration);
            }
            catch
            {
                // Best-effort rollback keeps the variables aligned if setx or the
                // atomic JSON write fails. An unset previous value is equivalent to logging off.
                for (int index = changedVariables.Count - 1; index >= 0; index--)
                {
                    var previous = changedVariables[index];
                    TryRestoreEnvironmentVariable(_setProcessEnvironmentVariable,
                        previous.Name, previous.ProcessValue);
                    TryRestoreEnvironmentVariable(_setUserEnvironmentVariable,
                        previous.Name, previous.UserValue ?? "0");
                }

                throw;
            }
        }

        private JObject ReadDebugConfiguration()
        {
            if (!File.Exists(_debugConfigurationPath))
                return new JObject();

            string json = File.ReadAllText(_debugConfigurationPath);
            return string.IsNullOrWhiteSpace(json) ? new JObject() : JObject.Parse(json);
        }

        private void WriteDebugConfiguration(JObject debugConfiguration)
        {
            string directory = Path.GetDirectoryName(_debugConfigurationPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = Path.Combine(directory ?? string.Empty,
                $".{Path.GetFileName(_debugConfigurationPath)}.{Guid.NewGuid():N}.tmp");

            try
            {
                File.WriteAllText(temporaryPath, debugConfiguration.ToString(Formatting.Indented));
                File.Move(temporaryPath, _debugConfigurationPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static void TryRestoreEnvironmentVariable(Action<string, string> setter,
            string name, string previousValue)
        {
            try
            {
                setter(name, previousValue);
            }
            catch
            {
                // Preserve the original error. The UI reports that the update failed and a retry
                // writes a consistent value to every logging switch.
            }
        }

        private static bool IsEnabledValue(string value)
        {
            return string.Equals(value, "1", StringComparison.Ordinal);
        }

        private static string GetDefaultDebugConfigurationPath()
        {
            // The native modules run inside target processes and always resolve this file from
            // roaming AppData, even when the CapFrameX host itself is in portable mode.
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataPath, "CapFrameX", "Configuration", "OsdDebug.json");
        }

        private static string GetUserEnvironmentVariable(string name)
        {
            return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
        }

        private static void SetUserEnvironmentVariableWithSetx(string name, string value)
        {
            // setx persists at user scope and broadcasts the environment change. As intended, it
            // affects subsequently launched processes. Already loaded native modules cache their
            // flags, so the UI still shows a restart hint after updating the host's environment.
            string setxPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "setx.exe");
            var startInfo = new ProcessStartInfo
            {
                FileName = setxPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(name);
            startInfo.ArgumentList.Add(value);

            using Process process = Process.Start(startInfo) ??
                throw new InvalidOperationException("setx.exe could not be started.");
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
                return;

            string details = string.IsNullOrWhiteSpace(error) ? output : error;
            throw new InvalidOperationException(
                $"setx.exe could not update {name} (exit code {process.ExitCode}). {details}".Trim());
        }
    }
}
