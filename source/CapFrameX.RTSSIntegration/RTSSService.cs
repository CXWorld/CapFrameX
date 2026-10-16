using CapFrameX.Contracts.RTSS;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace CapFrameX.RTSSIntegration
{
    public class RTSSService : RTSSCSharpWrapper, IRTSSService
    {
        private const string RTSS_PROCESS_NAME = "RTSS";
        // How long a valid shared memory header is trusted before the process list confirms it.
        internal const int ProcessListIntervalMs = 10_000;
        private bool _isRTSSInstalled;

        private static ILogger<RTSSService> _logger;

        private readonly BehaviorSubject<int> _processIdStream;
        private readonly BehaviorSubject<int> _processCountStream;
        private readonly object _launchGate = new object();
        private string _lastLaunchState;
        private long? _lastProcessListCheckTicks;

        public ISubject<int> ProcessIdStream => _processIdStream;

        public ISubject<int> ProcessCountStream => _processCountStream;

        public Func<int, bool> VulkanPresentationProbe { get; set; }

        public RTSSService(ILogger<RTSSService> logger) : base(ExceptionAction)
        {
            _logger = logger;
            _processIdStream = new BehaviorSubject<int>(default);
            _processCountStream = new BehaviorSubject<int>(default);
            _isRTSSInstalled = !string.IsNullOrEmpty(GetRTSSFullPath());
        }

        public bool IsRTSSInstalled()
        {
            return _isRTSSInstalled;
        }

        public string GetRTSSVersion()
        {
            try
            {
                string path = GetRTSSFullPath();
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    return null;

                var versionInfo = FileVersionInfo.GetVersionInfo(path);
                string version = string.IsNullOrWhiteSpace(versionInfo.ProductVersion)
                    ? versionInfo.FileVersion : versionInfo.ProductVersion;
                return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Reading the RTSS version failed");
                return null;
            }
        }

        public Task CheckRTSSRunningAndRefresh()
        {
            return Task.Run(() =>
            {
                EnsureRTSSRunning();
                Refresh();
            });
        }

        public Task CheckRTSSRunning()
        {
            return Task.Run(() => EnsureRTSSRunning());
        }

        /// <summary>
        /// Starts RTSS unless a game is already running.
        ///
        /// RTSS pulls in RTSSHooksLoader64, which injects into every process that is live when it
        /// starts. Doing that to a game mid-frame is exactly the intervention the in-game hook path
        /// guards against with its target policy and renderer arbitration, and it has taken a
        /// running Vulkan title down together with the Vulkan loader. Launching RTSS now cannot
        /// even deliver an overlay for that session: the loader binds implicit layers — RTSS' own
        /// included — at vkCreateInstance, which the running game is long past. So the launch is
        /// deferred, not lost: this runs on every overlay tick and starts RTSS as soon as no game
        /// is detected.
        /// </summary>
        private void EnsureRTSSRunning()
        {
            try
            {
                bool isRTSSRunning = _isRTSSInstalled && IsRTSSRunning();
                int gameProcessId = _processIdStream.Value;
                // Only the launch decision needs the game and its Vulkan state; with RTSS
                // running neither is looked up.
                bool gameIsRunning = _isRTSSInstalled && !isRTSSRunning
                    && gameProcessId > 0 && IsProcessAlive(gameProcessId);
                bool presentsWithVulkan = gameIsRunning &&
                    (VulkanPresentationProbe?.Invoke(gameProcessId) ?? false);

                switch (DecideLaunch(_isRTSSInstalled, isRTSSRunning, gameIsRunning, presentsWithVulkan))
                {
                    case RTSSLaunchDecision.NotInstalled:
                        LogLaunchState("not-installed", () => _logger.LogWarning(
                            "RTSS not installed (no InstallPath) — RTSS overlay unavailable"));
                        return;

                    case RTSSLaunchDecision.AlreadyRunning:
                        LogLaunchState("running", () => _logger.LogDebug("RTSS already running"));
                        return;

                    case RTSSLaunchDecision.DeferredVulkanGameRunning:
                        LogLaunchState($"deferred:{gameProcessId}", () => _logger.LogWarning(
                            "RTSS is not running and Vulkan game PID {pid} is: deferring the launch. A running " +
                            "Vulkan title can no longer pick up RTSS' implicit layer, so injecting into it now " +
                            "could only destabilize it. RTSS starts automatically once the game has exited — " +
                            "restart the game to use the RTSS overlay in this session.",
                            gameProcessId));
                        return;
                }

                var path = GetRTSSFullPath();
                LogLaunchState("launching", () => _logger.LogInformation(
                    "RTSS not running and no game detected -> launching '{path}'", path));
                Process proc = new Process();
                proc.StartInfo.FileName = Path.Combine(path);
                proc.StartInfo.UseShellExecute = false;
                proc.Start();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while starting RTSS process");
            }
        }

        /// <summary>
        /// Whether RTSS is running, answered from its shared memory where possible. RTSS writes
        /// the section's header when it starts and invalidates it when it exits, so a valid header
        /// means RTSS is running, unless it crashed and a game it hooked still maps the section,
        /// and a missing one means it is not, unless it is still starting. The process list settles
        /// both and is consulted only then: whenever the header is missing, and every
        /// <see cref="ProcessListIntervalMs"/> while it is present. Listing every process on each
        /// overlay tick took a system-wide snapshot several times a second.
        /// </summary>
        private bool IsRTSSRunning()
        {
            bool sharedMemoryAvailable = IsSharedMemoryAvailable();
            long now = Environment.TickCount64;
            long sinceProcessListCheck = _lastProcessListCheckTicks is long last ? now - last : long.MaxValue;
            if (!ShouldConsultProcessList(sharedMemoryAvailable, sinceProcessListCheck))
                return true;

            _lastProcessListCheckTicks = now;
            // Release the handles GetProcessesByName hands out instead of leaving them to the
            // finalizer.
            var processes = Process.GetProcessesByName(RTSS_PROCESS_NAME);
            foreach (var process in processes) process.Dispose();
            return processes.Length > 0;
        }

        internal static bool ShouldConsultProcessList(bool sharedMemoryAvailable, long millisecondsSinceProcessListCheck)
            => !sharedMemoryAvailable || millisecondsSinceProcessListCheck >= ProcessListIntervalMs;

        internal enum RTSSLaunchDecision
        {
            NotInstalled,
            AlreadyRunning,
            DeferredVulkanGameRunning,
            Launch
        }

        /// <summary>
        /// The launch decision, free of side effects so it can be exercised directly.
        ///
        /// Only a running VULKAN title defers the launch. Injecting into a live DXGI game is
        /// RTSS' normal operating mode and works, so deferring there would take away a renderer
        /// switch that has always been possible. A running Vulkan title is the one case where
        /// the injection can only do harm: RTSS' implicit layer is bound at vkCreateInstance and
        /// cannot be added afterwards, so no overlay can result either way.
        /// </summary>
        internal static RTSSLaunchDecision DecideLaunch(bool isInstalled, bool isRTSSRunning,
            bool gameIsRunning, bool gamePresentsWithVulkan)
        {
            if (!isInstalled) return RTSSLaunchDecision.NotInstalled;
            if (isRTSSRunning) return RTSSLaunchDecision.AlreadyRunning;
            if (gameIsRunning && gamePresentsWithVulkan)
                return RTSSLaunchDecision.DeferredVulkanGameRunning;
            return RTSSLaunchDecision.Launch;
        }

        // Called on every overlay tick, so only log when the decision actually changes.
        private void LogLaunchState(string state, Action log)
        {
            lock (_launchGate)
            {
                if (string.Equals(_lastLaunchState, state, StringComparison.Ordinal)) return;
                _lastLaunchState = state;
            }
            log();
        }

        // An inconclusive answer must count as "alive": deferring the launch is always safe,
        // launching into a game that is still running is what this guard exists to prevent.
        private static bool IsProcessAlive(int processId)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                    return !process.HasExited;
            }
            catch (ArgumentException) { return false; }        // no such process
            catch (InvalidOperationException) { return false; }
            catch (Exception) { return true; }
        }

        private static void ExceptionAction(Exception ex)
        {
            _logger.LogError(ex, "Exception thrown in RTSSCSharpWrapper");
        }

        private string GetRTSSFullPath()
        {
            string installPath = string.Empty;

            try
            {
                // SOFTWARE\WOW6432Node\Unwinder\RTSS
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\WOW6432Node\\Unwinder\\RTSS"))
                {
                    if (key != null)
                    {
                        object o = key.GetValue("InstallPath");
                        if (o != null)
                        {
                            installPath = o as string;  //"as" because it's REG_SZ...otherwise ToString() might be safe(r)
                        }
                    }
                }

                // SOFTWARE\Unwinder\RTSS
                if (string.IsNullOrWhiteSpace(installPath))
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey("Software\\Unwinder\\RTSS"))
                    {
                        if (key != null)
                        {
                            object o = key.GetValue("InstallPath");
                            if (o != null)
                            {
                                installPath = o as string;  //"as" because it's REG_SZ...otherwise ToString() might be safe(r)
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }

            return installPath;
        }
    }
}
