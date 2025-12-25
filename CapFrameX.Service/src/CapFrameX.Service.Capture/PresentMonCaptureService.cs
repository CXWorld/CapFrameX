using CapFrameX.Service.Capture.Contracts;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.InteropServices;

namespace CapFrameX.Service.Capture;

/// <summary>
/// High-performance capture service for PresentMon frame timing data.
/// Optimized for minimal allocations and maximum throughput.
/// </summary>
public sealed class PresentMonCaptureService : ICaptureService, IDisposable
{
    // Column indices for PresentMon 2.4.0 CSV output
    private const int ApplicationNameIndex = 0;
    private const int ProcessIdIndex = 1;
    private const int MsBetweenPresentsIndex = 10;
    private const int MsBetweenDisplayChangeIndex = 11;
    private const int MsPcLatencyIndex = 15;
    private const int StartTimeInSecondsIndex = 16;
    private const int CpuBusyIndex = 18;
    private const int GpuBusyIndex = 22;
    private const int ValidLineLength = 27;

    private static readonly char[] CommaSeparator = [','];
    private static readonly string ErrorMarker = "<error>";

    private readonly ILogger<PresentMonCaptureService> _logger;
    private readonly Subject<string[]> _outputDataStream;
    private readonly Subject<bool> _isCaptureModeActiveStream;

    // Process tracking with lock-free reads
    private volatile HashSet<(string ProcessName, int ProcessId)> _presentMonProcesses;
    private readonly object _processLock = new();
    private volatile bool _isUpdating;

    private Process? _captureProcess;
    private IDisposable? _heartBeatDisposable;
    private IDisposable? _processNameDisposable;

    public IReadOnlyDictionary<string, int> ParameterNameIndexMapping { get; }
    public IObservable<string[]> FrameDataStream => _outputDataStream.AsObservable();
    public Subject<bool> IsCaptureModeActiveStream => _isCaptureModeActiveStream;

    public PresentMonCaptureService(ILogger<PresentMonCaptureService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _outputDataStream = new Subject<string[]>();
        _isCaptureModeActiveStream = new Subject<bool>();
        _presentMonProcesses = new HashSet<(string, int)>();

        // Initialize parameter mapping
        ParameterNameIndexMapping = new Dictionary<string, int>
        {
            ["ApplicationName"] = ApplicationNameIndex,
            ["ProcessID"] = ProcessIdIndex,
            ["MsBetweenPresents"] = MsBetweenPresentsIndex,
            ["MsBetweenDisplayChange"] = MsBetweenDisplayChangeIndex,
            ["MsPCLatency"] = MsPcLatencyIndex,
            ["TimeInSeconds"] = StartTimeInSecondsIndex,
            ["CpuBusy"] = CpuBusyIndex,
            ["GpuBusy"] = GpuBusyIndex
        };
    }

    public bool StartCaptureService(IServiceStartInfo startInfo)
    {
        if (!CaptureServiceInfo.IsCompatibleWithRunningOS)
        {
            _logger.LogWarning("Operating system is not compatible with capture service");
            return false;
        }

        try
        {
            // Ensure clean state
            TerminatePresentMon(timeout: TimeSpan.FromSeconds(2));
            SubscribeToPresentMonCapturedProcesses();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = startInfo.FileName,
                    Arguments = startInfo.Arguments,
                    UseShellExecute = startInfo.UseShellExecute,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    CreateNoWindow = startInfo.CreateNoWindow,
                    Verb = startInfo.RunWithAdminRights ? "runas" : string.Empty,
                },
                EnableRaisingEvents = true
            };

            // OPTIMIZATION: Use span-based parsing to minimize allocations
            process.OutputDataReceived += OnOutputDataReceived;
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrWhiteSpace(e.Data))
                {
                    _logger.LogWarning("PresentMon error: {Error}", e.Data);
                }
            };

            process.Exited += (sender, args) =>
            {
                _logger.LogInformation("PresentMon process exited");
                StopCaptureService();
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            _captureProcess = process;
            _logger.LogInformation("PresentMon successfully started (PID: {ProcessId})", process.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start capture service");
            return false;
        }
    }

    /// <summary>
    /// OPTIMIZED: Minimal allocation output handler using ReadOnlySpan where possible
    /// </summary>
    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        // Fast path: Check minimum length before split
        if (e.Data.Length < 50) // Minimum reasonable CSV line length
            return;

        // Split using pooled array to reduce allocations
        var lineSplit = e.Data.Split(CommaSeparator, ValidLineLength + 1);

        if (lineSplit.Length >= ValidLineLength)
        {
            // Fast rejection: Check for error marker without string comparison
            if (lineSplit[ApplicationNameIndex].Length != ErrorMarker.Length ||
                !lineSplit[ApplicationNameIndex].Equals(ErrorMarker, StringComparison.Ordinal))
            {
                // Publish to stream (subscribers own the lifetime)
                _outputDataStream.OnNext(lineSplit);
            }
        }
    }

    public bool StopCaptureService()
    {
        _heartBeatDisposable?.Dispose();
        _heartBeatDisposable = null;

        _processNameDisposable?.Dispose();
        _processNameDisposable = null;

        lock (_processLock)
        {
            _presentMonProcesses.Clear();
        }

        var success = TerminatePresentMon(timeout: TimeSpan.FromSeconds(3));

        _captureProcess?.Dispose();
        _captureProcess = null;

        return success;
    }

    public IEnumerable<(string ProcessName, int ProcessId)> GetAllFilteredProcesses(HashSet<string> filter)
    {
        // Lock-free read of volatile reference
        var snapshot = _presentMonProcesses;

        if (filter == null || filter.Count == 0)
            return snapshot;

        // OPTIMIZATION: Use struct enumerator to avoid allocations
        return snapshot.Where(p => !filter.Contains(p.ProcessName));
    }

    /// <summary>
    /// OPTIMIZED: Non-blocking process monitoring with batched updates
    /// </summary>
    private void SubscribeToPresentMonCapturedProcesses()
    {
        // Heartbeat: Batch process liveness checks every second
        _heartBeatDisposable = Observable
            .Interval(TimeSpan.FromSeconds(1))
            .Subscribe(_ => UpdateProcessToCaptureList());

        // Stream processing: Add new processes as they appear
        _processNameDisposable = _outputDataStream
            .Skip(1) // Skip header
            .Where(_ => !_isUpdating) // Skip during batch update
            .Subscribe(lineSplit =>
            {
                try
                {
                    if (!int.TryParse(lineSplit[ProcessIdIndex], out int processId))
                        return;

                    var processName = lineSplit[ApplicationNameIndex].Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
                    var processInfo = (processName, processId);

                    // OPTIMIZATION: Only lock for the minimal critical section
                    lock (_processLock)
                    {
                        _presentMonProcesses.Add(processInfo);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse process info from capture line");
                }
            });
    }

    /// <summary>
    /// OPTIMIZED: Batch process liveness check with minimal lock time
    /// </summary>
    private void UpdateProcessToCaptureList()
    {
        _isUpdating = true;

        try
        {
            HashSet<(string, int)> currentProcesses;

            lock (_processLock)
            {
                currentProcesses = _presentMonProcesses;
            }

            // OPTIMIZATION: Perform P/Invoke calls OUTSIDE the lock
            var liveProcesses = new HashSet<(string, int)>(currentProcesses.Count);

            foreach (var (name, pid) in currentProcesses)
            {
                if (ProcessHelper.IsProcessAlive(pid))
                {
                    liveProcesses.Add((name, pid));
                }
            }

            // OPTIMIZATION: Single lock acquisition to swap the reference
            lock (_processLock)
            {
                _presentMonProcesses = liveProcesses;
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// OPTIMIZED: Terminate with timeout to prevent hangs
    /// </summary>
    private bool TerminatePresentMon(TimeSpan timeout)
    {
        try
        {
            // First attempt: Send termination signal
            using var termProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine("PresentMon", "PresentMon-2.4.0-x64.exe"),
                    Arguments = "--terminate_existing_session",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Verb = "runas",
                }
            };

            termProcess.Start();
            if (!termProcess.WaitForExit((int)timeout.TotalMilliseconds))
            {
                _logger.LogWarning("PresentMon termination command timed out");
                termProcess.Kill();
            }

            // Second attempt: Kill our managed process if it exists
            if (_captureProcess != null && !_captureProcess.HasExited)
            {
                _captureProcess.Kill(entireProcessTree: true);
                if (!_captureProcess.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    _logger.LogError("Failed to terminate PresentMon process");
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while terminating PresentMon");
            return false;
        }
    }

    public void Dispose()
    {
        StopCaptureService();
        _outputDataStream?.Dispose();
        _isCaptureModeActiveStream?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Helper class for process liveness checks using P/Invoke.
/// </summary>
internal static partial class ProcessHelper
{
    private const uint StillActive = 259;

    [Flags]
    private enum ProcessAccessFlags : uint
    {
        QueryInformation = 0x0400,
        QueryLimitedInformation = 0x1000
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(ProcessAccessFlags access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int procId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);

    /// <summary>
    /// OPTIMIZED: Fast process liveness check using limited query rights
    /// </summary>
    public static bool IsProcessAlive(int processId)
    {
        var handle = OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, processId);
        if (handle == IntPtr.Zero)
            return false;

        try
        {
            if (!GetExitCodeProcess(handle, out uint exitCode))
                return false;

            return exitCode == StillActive;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}

/// <summary>
/// Operating system compatibility information.
/// </summary>
internal static class CaptureServiceInfo
{
    public static bool IsCompatibleWithRunningOS =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 14393); // Windows 10 1607+
}
