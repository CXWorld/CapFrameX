using System;
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

        private const string PresentStatsPropertyName = "presentStats";
        private readonly string _debugConfigurationPath;
        private readonly Func<string, string> _getUserEnvironmentVariable;
        private readonly Action<string, string> _setUserEnvironmentVariable;

        internal ExtendedOsdLoggingController()
            : this(GetDefaultDebugConfigurationPath(), GetUserEnvironmentVariable,
                SetUserEnvironmentVariableWithSetx)
        {
        }

        internal ExtendedOsdLoggingController(string debugConfigurationPath,
            Func<string, string> getUserEnvironmentVariable,
            Action<string, string> setUserEnvironmentVariable)
        {
            _debugConfigurationPath = debugConfigurationPath ??
                throw new ArgumentNullException(nameof(debugConfigurationPath));
            _getUserEnvironmentVariable = getUserEnvironmentVariable ??
                throw new ArgumentNullException(nameof(getUserEnvironmentVariable));
            _setUserEnvironmentVariable = setUserEnvironmentVariable ??
                throw new ArgumentNullException(nameof(setUserEnvironmentVariable));
        }

        internal bool IsEnabled()
        {
            bool hookLogEnabled = IsEnabledValue(
                _getUserEnvironmentVariable(HookLogEnvironmentVariable));
            bool vulkanLayerLogEnabled = IsEnabledValue(
                _getUserEnvironmentVariable(VulkanLayerLogEnvironmentVariable));
            bool presentStatsEnabled =
                ReadDebugConfiguration().Value<bool?>(PresentStatsPropertyName) == true;

            // Treat a legacy/manually configured partial state as enabled. The user can then turn
            // the bundle off with one click and bring all three diagnostics back into sync.
            return hookLogEnabled || vulkanLayerLogEnabled || presentStatsEnabled;
        }

        internal void SetEnabled(bool enabled)
        {
            // Parse before changing the environment so malformed hand-edited diagnostics are never
            // silently discarded and cannot leave the three logging switches partially updated.
            JObject debugConfiguration = ReadDebugConfiguration();
            debugConfiguration[PresentStatsPropertyName] = enabled;

            string value = enabled ? "1" : "0";
            string previousHookValue = _getUserEnvironmentVariable(HookLogEnvironmentVariable);
            string previousVulkanValue = _getUserEnvironmentVariable(VulkanLayerLogEnvironmentVariable);
            bool hookValueChanged = false;
            bool vulkanValueChanged = false;

            try
            {
                _setUserEnvironmentVariable(HookLogEnvironmentVariable, value);
                hookValueChanged = true;
                _setUserEnvironmentVariable(VulkanLayerLogEnvironmentVariable, value);
                vulkanValueChanged = true;
                WriteDebugConfiguration(debugConfiguration);
            }
            catch
            {
                // Best-effort rollback keeps the two persistent variables aligned if setx or the
                // atomic JSON write fails. An unset previous value is equivalent to logging off.
                if (vulkanValueChanged)
                    TryRestoreEnvironmentVariable(VulkanLayerLogEnvironmentVariable, previousVulkanValue);
                if (hookValueChanged)
                    TryRestoreEnvironmentVariable(HookLogEnvironmentVariable, previousHookValue);

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

        private void TryRestoreEnvironmentVariable(string name, string previousValue)
        {
            try
            {
                _setUserEnvironmentVariable(name, previousValue ?? "0");
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
            // only affects processes started after the change; the UI therefore shows a restart hint.
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
