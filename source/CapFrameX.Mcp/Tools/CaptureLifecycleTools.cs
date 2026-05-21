using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Mcp.Attributes;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CapFrameX.Mcp.Tools
{
    [McpServerToolType]
    public class CaptureLifecycleTools
    {
        private readonly CaptureManager _captureManager;
        private readonly ProcessList _processList;
        private readonly IAppConfiguration _appConfiguration;

        public CaptureLifecycleTools(CaptureManager captureManager, ProcessList processList, IAppConfiguration appConfiguration)
        {
            _captureManager = captureManager;
            _processList = processList;
            _appConfiguration = appConfiguration;
        }

        [McpServerTool(Name = "cfx_list_processes",
            Description = "Lists processes that PresentMon currently sees and considers capture-eligible. " +
                "Includes process name, PID, optional display name, and whether the process is blacklisted in the user's CapFrameX process list. " +
                "Use this to pick a target for cfx_start_capture.")]
        public ProcessListResult ListProcesses(
            [Description("If true (default), filter out blacklisted processes. Set false to inspect every running candidate.")] bool excludeBlacklisted = true)
        {
            var ignored = _processList.GetIgnoredProcessNames() ?? Array.Empty<string>();
            var blacklist = new HashSet<string>(ignored, StringComparer.OrdinalIgnoreCase);
            var filter = excludeBlacklisted ? blacklist : new HashSet<string>();
            var raw = _captureManager.GetAllFilteredProcesses(filter) ?? Enumerable.Empty<(string, int)>();

            var configured = _processList.Processes ?? Array.Empty<CXProcess>();
            var byName = new Dictionary<string, CXProcess>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in configured)
            {
                if (string.IsNullOrEmpty(p?.Name)) continue;
                if (!byName.ContainsKey(p.Name)) byName[p.Name] = p;
            }

            var result = new ProcessListResult();
            foreach (var (name, pid) in raw)
            {
                byName.TryGetValue(name ?? string.Empty, out var cx);
                result.Processes.Add(new ProcessInfoEntry
                {
                    ProcessName = name,
                    Pid = pid,
                    DisplayName = cx?.DisplayName,
                    IsBlacklisted = cx?.IsBlacklisted ?? false,
                    LastCaptureTime = cx?.LastCaptureTime,
                });
            }

            result.Processes = result.Processes
                .OrderBy(p => p.IsBlacklisted)
                .ThenBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            result.Count = result.Processes.Count;
            return result;
        }

        [McpServerTool(Name = "cfx_start_capture",
            Description = "Starts a capture for the given process. Mutates state: starts PresentMon, sensor logging, and writes a record on completion. " +
                "If captureSeconds is 0 (default), the capture runs until cfx_stop_capture is called. " +
                "Throws if a capture is already running or the process is not capture-eligible (call cfx_list_processes first).")]
        public async Task<StartCaptureResult> StartCapture(
            [Description("Process name (without .exe) as returned by cfx_list_processes")] string processName,
            [Description("Capture duration in seconds. 0 (default) = open-ended; stop with cfx_stop_capture.")] double captureSeconds = 0,
            [Description("Optional comment annotated on the resulting record.")] string comment = null,
            [Description("Optional capture-start delay in seconds (countdown before capture starts).")] double delaySeconds = 0)
        {
            if (string.IsNullOrWhiteSpace(processName))
                throw new ArgumentException("processName must be provided", nameof(processName));
            if (_captureManager.IsCapturing)
                throw new InvalidOperationException("A capture is already running. Use cfx_stop_capture first.");

            var processes = _captureManager.GetAllFilteredProcesses(new HashSet<string>())
                ?? Enumerable.Empty<(string, int)>();
            var match = processes.FirstOrDefault(p =>
                string.Equals(p.Item1, processName, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(match.Item1))
                throw new InvalidOperationException(
                    $"Process '{processName}' is not currently capture-eligible. Run cfx_list_processes for the current candidates.");

            var options = new CaptureOptions
            {
                ProcessInfo = (match.Item1, match.Item2),
                CaptureTime = captureSeconds,
                CaptureDelay = delaySeconds,
                CaptureFileMode = _appConfiguration.CaptureFileMode,
                Remote = true,
                Comment = comment,
            };

            Log.Logger.Information("MCP cfx_start_capture: {process} (pid {pid}) for {seconds}s, delay {delay}s",
                match.Item1, match.Item2, captureSeconds, delaySeconds);

            await _captureManager.StartCapture(options).ConfigureAwait(false);

            return new StartCaptureResult
            {
                ProcessName = match.Item1,
                Pid = match.Item2,
                CaptureSeconds = captureSeconds,
                DelaySeconds = delaySeconds,
                Comment = comment,
                Started = true,
            };
        }

        [McpServerTool(Name = "cfx_stop_capture",
            Description = "Stops the currently running capture (or cancels an active start-delay countdown). " +
                "Returns immediately; the record file is written asynchronously. Use cfx_wait_for_capture_completion to wait until processing is done.")]
        public async Task<StopCaptureResult> StopCapture()
        {
            bool wasDelay = _captureManager.DelayCountdownRunning;
            bool wasCapturing = _captureManager.IsCapturing;
            if (!wasDelay && !wasCapturing)
                throw new InvalidOperationException("No capture is running.");

            Log.Logger.Information("MCP cfx_stop_capture: wasCapturing={wasCapturing} wasDelay={wasDelay}", wasCapturing, wasDelay);
            await _captureManager.StopCapture().ConfigureAwait(false);

            return new StopCaptureResult
            {
                WasCapturing = wasCapturing,
                CancelledDelayCountdown = wasDelay && !wasCapturing,
            };
        }

        [McpServerTool(Name = "cfx_wait_for_capture_completion",
            Description = "Blocks until the active capture has been processed and written to disk (state == Stopped) or until the timeout elapses. " +
                "Use after cfx_start_capture(captureSeconds=N) to await the timer-based completion before analyzing the record.")]
        public async Task<WaitForCaptureResult> WaitForCaptureCompletion(
            [Description("Timeout in seconds. Default 120.")] int timeoutSec = 120)
        {
            if (timeoutSec <= 0) timeoutSec = 120;

            // Already idle — return immediately.
            if (!_captureManager.IsCapturing && !_captureManager.DelayCountdownRunning && !_captureManager.LockCaptureService)
            {
                return new WaitForCaptureResult { TimedOut = false, FinalState = "Stopped", WaitedSec = 0 };
            }

            var stopAt = DateTime.UtcNow.AddSeconds(timeoutSec);
            var startedAt = DateTime.UtcNow;
            string lastState = "unknown";

            try
            {
                lastState = await _captureManager.CaptureStatusChange
                    .Select(s => s.Status?.ToString() ?? "Stopped")
                    .Where(s => s == nameof(ECaptureStatus.Stopped))
                    .Take(1)
                    .Timeout(TimeSpan.FromSeconds(timeoutSec));

                // Also wait briefly for the lock to release (file write happens after the Stopped state).
                while (_captureManager.LockCaptureService && DateTime.UtcNow < stopAt)
                    await Task.Delay(100).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                return new WaitForCaptureResult
                {
                    TimedOut = true,
                    FinalState = lastState,
                    WaitedSec = (DateTime.UtcNow - startedAt).TotalSeconds,
                };
            }

            return new WaitForCaptureResult
            {
                TimedOut = false,
                FinalState = lastState,
                WaitedSec = (DateTime.UtcNow - startedAt).TotalSeconds,
            };
        }
    }
}
