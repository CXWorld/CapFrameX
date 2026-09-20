using CapFrameX.Service.Capture.Contracts;
using CapFrameX.Service.Capture.Frames;
using CapFrameX.Service.Contracts.Frames;
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
    private static readonly char[] CommaSeparator = [','];

    private readonly ILogger<PresentMonCaptureService> _logger;
    private readonly Subject<string[]> _outputDataStream;
    private readonly Subject<FrameSample> _frameStream;
    private readonly Subject<bool> _isCaptureModeActiveStream;

    // Replaced as soon as PresentMon writes its header; read on the output thread, written on it
    // too, so a single volatile reference is enough to publish both objects together.
    private volatile ColumnBinding _columns;

    // Process tracking with lock-free reads
    private volatile HashSet<(string ProcessName, int ProcessId)> _presentMonProcesses;
    private readonly object _processLock = new();
    private volatile bool _isUpdating;

    private Process? _captureProcess;
    private IDisposable? _heartBeatDisposable;
    private IDisposable? _processNameDisposable;

    public IReadOnlyDictionary<string, int> ParameterNameIndexMapping => _columns.NameIndexMapping;
    public IObservable<string[]> FrameDataStream => _outputDataStream.AsObservable();

    /// <summary>
    /// Presents as platform-neutral samples. This is what the capture orchestrator consumes;
    /// <see cref="FrameDataStream"/> stays for callers that want the raw CSV fields.
    /// </summary>
    public IObservable<FrameSample> FrameStream => _frameStream.AsObservable();

    /// <summary>Optional metrics the current column set delivers.</summary>
    public FrameMetrics AvailableMetrics => _columns.Mapper.AvailableMetrics;

    public Subject<bool> IsCaptureModeActiveStream => _isCaptureModeActiveStream;

    public PresentMonCaptureService(ILogger<PresentMonCaptureService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _outputDataStream = new Subject<string[]>();
        _frameStream = new Subject<FrameSample>();
        _isCaptureModeActiveStream = new Subject<bool>();
        _presentMonProcesses = new HashSet<(string, int)>();
        _columns = ColumnBinding.For(PresentMonColumnLayout.FromHeader(PresentMonKnownLayouts.WithPcLatency));
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
    /// Handles one line of PresentMon output: the header once, frame rows after it.
    /// </summary>
    private void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
            return;

        if (TryBindColumns(e.Data))
            return;

        var columns = _columns;
        var lineSplit = e.Data.Split(CommaSeparator, columns.Layout.ColumnCount + 1);

        if (lineSplit.Length < columns.Layout.ColumnCount)
            return;

        if (columns.ApplicationIndex >= 0 &&
            lineSplit[columns.ApplicationIndex].Equals(PresentMonColumns.ErrorMarker, StringComparison.Ordinal))
            return;

        // Subscribers own the lifetime of the array.
        _outputDataStream.OnNext(lineSplit);

        if (columns.Mapper.TryMap(lineSplit, out var frame))
            _frameStream.OnNext(frame);
    }

    /// <summary>
    /// Re-binds the column indices when the line is PresentMon's header.
    /// </summary>
    /// <remarks>
    /// Reading the indices from the header rather than hard-coding them is what keeps a PresentMon
    /// update from silently shifting every metric by one column.
    /// </remarks>
    /// <param name="line">One line of PresentMon output.</param>
    /// <returns>True when the line was the header and must not be treated as frame data.</returns>
    private bool TryBindColumns(string line)
    {
        if (!line.StartsWith(PresentMonKnownLayouts.HeaderPrefix, StringComparison.Ordinal))
            return false;

        try
        {
            _columns = ColumnBinding.For(PresentMonColumnLayout.FromHeader(line));
            _logger.LogInformation(
                "PresentMon column layout bound from header: {ColumnCount} columns, metrics {Metrics}.",
                _columns.Layout.ColumnCount,
                _columns.Mapper.AvailableMetrics);
        }
        catch (ArgumentException exception)
        {
            _logger.LogWarning(
                exception,
                "PresentMon header could not be interpreted; keeping the previous column layout.");
        }

        return true;
    }

    /// <summary>One column layout together with everything derived from it.</summary>
    private sealed class ColumnBinding
    {
        private ColumnBinding(
            PresentMonColumnLayout layout,
            PresentMonFrameMapper mapper,
            int applicationIndex,
            IReadOnlyDictionary<string, int> nameIndexMapping)
        {
            Layout = layout;
            Mapper = mapper;
            ApplicationIndex = applicationIndex;
            NameIndexMapping = nameIndexMapping;
        }

        public PresentMonColumnLayout Layout { get; }

        public PresentMonFrameMapper Mapper { get; }

        public int ApplicationIndex { get; }

        public IReadOnlyDictionary<string, int> NameIndexMapping { get; }

        public static ColumnBinding For(PresentMonColumnLayout layout)
        {
            var application = layout.IndexOf(PresentMonColumns.Application);
            var mapping = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var column in new[]
                     {
                         PresentMonColumns.Application,
                         PresentMonColumns.ProcessId,
                         PresentMonColumns.TimeInSeconds,
                         PresentMonColumns.MsBetweenPresents,
                         PresentMonColumns.MsBetweenDisplayChange,
                         PresentMonColumns.MsPcLatency,
                         PresentMonColumns.MsCpuBusy,
                         PresentMonColumns.MsGpuBusy,
                     })
            {
                var index = layout.IndexOf(column);

                if (index >= 0)
                {
                    mapping[column] = index;
                }
            }

            // Names the capture consumers have always used, kept so a column rename in PresentMon
            // does not ripple through them.
            AddAlias("ApplicationName", PresentMonColumns.Application);
            AddAlias("CpuBusy", PresentMonColumns.MsCpuBusy);
            AddAlias("GpuBusy", PresentMonColumns.MsGpuBusy);

            return new ColumnBinding(layout, new PresentMonFrameMapper(layout), application, mapping);

            void AddAlias(string alias, string column)
            {
                if (mapping.TryGetValue(column, out var index))
                {
                    mapping[alias] = index;
                }
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
        // The header is consumed while binding the columns and never reaches this stream, so every
        // line arriving here is frame data.
        _processNameDisposable = _outputDataStream
            .Where(_ => !_isUpdating) // Skip during batch update
            .Subscribe(lineSplit =>
            {
                try
                {
                    var columns = _columns;

                    if (columns.ApplicationIndex < 0 ||
                        !columns.NameIndexMapping.TryGetValue(PresentMonColumns.ProcessId, out var processIdIndex) ||
                        !int.TryParse(lineSplit[processIdIndex], out int processId))
                        return;

                    var processName = lineSplit[columns.ApplicationIndex]
                        .Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
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
                    FileName = Path.Combine("PresentMon", "PresentMon-2.5.1-x64.exe"),
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
