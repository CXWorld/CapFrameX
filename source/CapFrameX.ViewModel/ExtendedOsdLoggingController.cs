using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.ViewModel
{
    public sealed class ExtendedOsdLoggingController
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
        private readonly Action _notifyEnvironmentChanged;

        public ExtendedOsdLoggingController()
            : this(GetDefaultDebugConfigurationPath(), GetUserEnvironmentVariable,
                SetUserEnvironmentVariable,
                name => Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process),
                (name, value) => Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Process),
                NotifyEnvironmentChanged)
        {
        }

        internal ExtendedOsdLoggingController(string debugConfigurationPath,
            Func<string, string> getUserEnvironmentVariable,
            Action<string, string> setUserEnvironmentVariable,
            Func<string, string> getProcessEnvironmentVariable,
            Action<string, string> setProcessEnvironmentVariable,
            Action notifyEnvironmentChanged)
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
            _notifyEnvironmentChanged = notifyEnvironmentChanged ??
                throw new ArgumentNullException(nameof(notifyEnvironmentChanged));
        }

        internal bool IsEnabled()
        {
            // The JSON stores the user's selection. A launcher or IDE can retain old environment
            // values across app restarts; those must never turn a saved "off" back into "on".
            JObject configuration = ReadDebugConfiguration();
            return configuration.Value<bool?>(PresentStatsPropertyName) == true ||
                configuration.Value<bool?>(VerboseLogPropertyName) == true;
        }

        public void ApplyProcessSettings()
        {
            bool enabled = false;
            try
            {
                enabled = IsEnabled();
            }
            finally
            {
                // Run before loading native overlay modules, which cache these flags. If the
                // configuration cannot be read, keep diagnostics off and report the read error.
                string value = enabled ? "1" : "0";
                foreach (string name in LoggingEnvironmentVariables)
                    _setProcessEnvironmentVariable(name, value);
            }
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
                // No environment notification has been sent yet. Restore the previous values
                // if a registry update or the atomic JSON write fails.
                for (int index = changedVariables.Count - 1; index >= 0; index--)
                {
                    var previous = changedVariables[index];
                    TryRestoreEnvironmentVariable(_setProcessEnvironmentVariable,
                        previous.Name, previous.ProcessValue);
                    TryRestoreEnvironmentVariable(_setUserEnvironmentVariable,
                        previous.Name, previous.UserValue);
                }

                throw;
            }

            // Notify the shell only once, after all four variables and the JSON are consistent.
            // The view model performs this entire operation on a worker thread.
            _notifyEnvironmentChanged();
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

        private static void SetUserEnvironmentVariable(string name, string value)
        {
            // Persist directly so each variable does not launch setx and broadcast separately.
            using RegistryKey environment = Registry.CurrentUser.CreateSubKey("Environment", true) ??
                throw new InvalidOperationException("The user environment could not be opened.");
            if (value == null)
                environment.DeleteValue(name, false);
            else
                environment.SetValue(name, value, RegistryValueKind.String);
        }

        private static void NotifyEnvironmentChanged()
        {
            const uint wmSettingChange = 0x001A;
            const uint smtoAbortIfHung = 0x0002;
            // A timeout in another application must not undo successfully saved settings.
            // Native modules already loaded in a game still require a restart.
            SendMessageTimeout(new IntPtr(0xffff), wmSettingChange, UIntPtr.Zero,
                "Environment", smtoAbortIfHung, 1000, out _);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam,
            string lParam, uint flags, uint timeout, out UIntPtr result);
    }
}
