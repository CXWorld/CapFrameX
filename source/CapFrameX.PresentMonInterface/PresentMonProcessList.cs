using CapFrameX.Capture.Contracts;
using CapFrameX.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace CapFrameX.PresentMonInterface
{
    /// <summary>
    /// The processes PresentMon reports presents for. A data row adds its process if the
    /// process is alive; a pass once per second drops the processes that exited. PresentMon
    /// can deliver rows after their process exited (on 2026-10-25 one nine seconds after a
    /// test app closed, under the name "&lt;unknown&gt;"). Listed again, the process stood
    /// beside the game until the next pass, and with two entries the automatic target
    /// selection had less to go on.
    /// Every entry also counts its presents and GPU busy time per pass, the input of the
    /// automatic target selection (<see cref="GetProcessActivity"/>).
    /// </summary>
    internal sealed class PresentMonProcessList
    {
        private static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Completed passes (seconds) the present statistics cover: enough to smooth a frame
        /// cap or a loading hitch, short enough to follow a switch within a few seconds.
        /// </summary>
        internal const int ActivityWindowSeconds = 3;

        private readonly ILogger _logger;
        private readonly Func<int, bool> _isProcessAlive;
        private readonly Func<EventLoopScheduler> _createRowScheduler;
        private readonly IScheduler _pruneScheduler;
        private readonly object _listLock = new object();
        private Dictionary<(string, int), PresentActivity> _processes = new Dictionary<(string, int), PresentActivity>();
        // Rows whose process had exited, so that a burst of late rows costs one check; the next
        // pass clears it, after which a reused PID is checked again.
        private readonly HashSet<(string, int)> _exitedRows = new HashSet<(string, int)>();
        // Prune passes so far; the present statistics count per pass.
        private long _pass;
        private volatile bool _tracksGpuBusy;
        private volatile bool _isUpdating;
        private bool _hasInitialData;
        private IDisposable _pruneSubscription;
        private IDisposable _rowSubscription;
        private EventLoopScheduler _rowScheduler;

        public PresentMonProcessList(ILogger logger)
            : this(logger, ProcessHelper.IsProcessAlive, () => new EventLoopScheduler(),
                Scheduler.Default)
        {
        }

        internal PresentMonProcessList(ILogger logger, Func<int, bool> isProcessAlive,
            Func<EventLoopScheduler> createRowScheduler, IScheduler pruneScheduler)
        {
            _logger = logger;
            _isProcessAlive = isProcessAlive;
            _createRowScheduler = createRowScheduler;
            _pruneScheduler = pruneScheduler;
        }

        /// <summary>
        /// Follows the output of a newly started PresentMon instance, whose first row is the
        /// CSV header. A previous instance's subscription is released first.
        /// </summary>
        public void Start(IObservable<string[]> rows)
        {
            ReleaseSubscriptions();

            try
            {
                _hasInitialData = false;
                _pruneSubscription = Observable.Interval(PruneInterval, _pruneScheduler)
                    .Subscribe(_ => RemoveExitedProcesses());

                // The header is read on the producer thread, so it never reaches the scheduler.
                var columns = new RowColumns();
                _rowScheduler = _createRowScheduler();
                _rowSubscription = rows
                    .Where(lineSplit => !TakeHeader(columns, lineSplit))
                    .ObserveOn(_rowScheduler)
                    .Where(lineSplit => _isUpdating == false)
                    .Subscribe(lineSplit => AddProcess(lineSplit, columns));
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to get process resources");
            }
        }

        public void Stop()
        {
            ReleaseSubscriptions();

            lock (_listLock)
            {
                _processes.Clear();
                _exitedRows.Clear();
            }
        }

        public IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter)
        {
            // A snapshot taken inside the lock. A lazy query ran after the lock was released,
            // while the row thread kept adding to the same set ("Collection was modified").
            lock (_listLock)
            {
                return _processes.Keys.Where(processInfo => !filter.Contains(processInfo.Item1)).ToArray();
            }
        }

        /// <summary>
        /// The present statistics of every listed process over the last
        /// <see cref="ActivityWindowSeconds"/> completed passes.
        /// </summary>
        public IReadOnlyList<CaptureProcessActivity> GetProcessActivity()
        {
            lock (_listLock)
            {
                var activity = new CaptureProcessActivity[_processes.Count];
                int i = 0;
                foreach (var entry in _processes)
                    activity[i++] = entry.Value.ToActivity(entry.Key.Item1, entry.Key.Item2, _pass, _tracksGpuBusy);
                return activity;
            }
        }

        /// <summary>
        /// FrameType is "Application" for a frame the game rendered; a frame generation source
        /// names itself instead. Not set, unspecified and repeated frames are not generated.
        /// </summary>
        internal static bool IsGeneratedFrame(string frameType)
        {
            if (string.IsNullOrEmpty(frameType))
                return false;

            switch (frameType)
            {
                case "Application":
                case "NotSet":
                case "Unspecified":
                case "Repeated":
                case "NA":
                    return false;
                default:
                    return true;
            }
        }

        private bool TakeHeader(RowColumns columns, string[] lineSplit)
        {
            if (columns.HasHeader)
                return false;

            columns.GpuBusy = Array.IndexOf(lineSplit, "MsGPUBusy");
            columns.PresentMode = Array.IndexOf(lineSplit, "PresentMode");
            columns.FrameType = Array.IndexOf(lineSplit, "FrameType");
            columns.HasHeader = true;
            _tracksGpuBusy = columns.GpuBusy >= 0;
            return true;
        }

        private void ReleaseSubscriptions()
        {
            _pruneSubscription?.Dispose();
            _pruneSubscription = null;
            _rowSubscription?.Dispose();
            _rowSubscription = null;
            // An EventLoopScheduler keeps its thread parked until it is disposed. Disposing only
            // the subscription leaked one thread per PresentMon restart.
            _rowScheduler?.Dispose();
            _rowScheduler = null;
        }

        private void AddProcess(string[] lineSplit, RowColumns columns)
        {
            if (!_hasInitialData)
            {
                _logger.LogInformation("Process name stream has initial data.");
                _hasInitialData = true;
            }

            string processName = lineSplit[PresentMonCaptureService.ApplicationName_INDEX].Replace(".exe", "");

            if (!int.TryParse(lineSplit[PresentMonCaptureService.ProcessID_INDEX], out int processId))
            {
                _logger.LogError("Failed to parse process ID from line split. {lineSplit}", string.Join(",", lineSplit));
                return;
            }

            // Parsed before the lock, which the UI thread shares.
            double? gpuBusyMs = null;
            if (columns.GpuBusy >= 0 && columns.GpuBusy < lineSplit.Length &&
                double.TryParse(lineSplit[columns.GpuBusy], NumberStyles.Float, CultureInfo.InvariantCulture, out double gpuBusy) &&
                gpuBusy > 0 && !double.IsInfinity(gpuBusy))
                gpuBusyMs = gpuBusy;
            string presentMode = columns.PresentMode >= 0 && columns.PresentMode < lineSplit.Length
                ? lineSplit[columns.PresentMode] : null;
            bool generatedFrame = columns.FrameType >= 0 && columns.FrameType < lineSplit.Length &&
                IsGeneratedFrame(lineSplit[columns.FrameType]);

            lock (_listLock)
            {
                var processInfo = (processName, processId);
                if (processName == null || _exitedRows.Contains(processInfo))
                    return;

                if (_processes.TryGetValue(processInfo, out PresentActivity activity))
                {
                    activity.Record(_pass, gpuBusyMs, presentMode, generatedFrame);
                    return;
                }

                // Only a new entry is checked, so the rows of a listed process cost a lookup.
                if (_isProcessAlive(processId))
                {
                    activity = new PresentActivity(_pass);
                    activity.Record(_pass, gpuBusyMs, presentMode, generatedFrame);
                    _processes.Add(processInfo, activity);
                }
                else
                {
                    _exitedRows.Add(processInfo);
                    if (Environment.GetEnvironmentVariable("CFX_OSD_VERBOSE_LOG") == "1")
                        _logger.LogInformation(
                            "Process list: ignored a row of '{process}' (PID {pid}) - the process has exited",
                            processName, processId);
                }
            }
        }

        private void RemoveExitedProcesses()
        {
            _isUpdating = true;
            var updatedList = new Dictionary<(string, int), PresentActivity>();
            // Extended OSD logging (the switch ExtendedOsdLoggingController mirrors into the
            // process environment); evaluated once per 1 s pass, never per entry.
            bool logRemovals = Environment.GetEnvironmentVariable("CFX_OSD_VERBOSE_LOG") == "1";

            lock (_listLock)
            {
                foreach (var entry in _processes)
                {
                    var processInfo = entry.Key;
                    try
                    {
                        if (_isProcessAlive(processInfo.Item2))
                        {
                            updatedList.Add(processInfo, entry.Value);
                        }
                        else if (logRemovals)
                        {
                            // A removal changes the detected list and may change the target, so
                            // every one is worth a line.
                            _logger.LogInformation(
                                "Process list: removed '{process}' (PID {pid}) - exited (Win32 error {error})",
                                processInfo.Item1, processInfo.Item2, Marshal.GetLastWin32Error());
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to get process resources from {processInfo.Item1}");
                    }
                }

                _processes = updatedList;
                _exitedRows.Clear();
                _pass++;
            }

            _isUpdating = false;
        }

        /// <summary>
        /// Column positions read from the header of one PresentMon instance; -1 when absent.
        /// </summary>
        private sealed class RowColumns
        {
            public bool HasHeader;
            public int GpuBusy = -1;
            public int PresentMode = -1;
            public int FrameType = -1;
        }

        /// <summary>
        /// Presents and GPU busy time of one process, one bucket per pass. A bucket is reset when
        /// a row of a new pass reuses it, so no pass has to touch the entries. Guarded by the
        /// list lock.
        /// </summary>
        private sealed class PresentActivity
        {
            private readonly long[] _bucketPass = new long[ActivityWindowSeconds + 1];
            private readonly int[] _presents = new int[ActivityWindowSeconds + 1];
            private readonly double[] _gpuBusyMs = new double[ActivityWindowSeconds + 1];
            private readonly long _firstPass;
            private long _lastPresentPass;
            private long _lastGeneratedFramePass = long.MinValue;
            private string _presentMode;

            public PresentActivity(long pass)
            {
                _firstPass = pass;
                _lastPresentPass = pass;
                for (int i = 0; i < _bucketPass.Length; i++)
                    _bucketPass[i] = -1;
            }

            public void Record(long pass, double? gpuBusyMs, string presentMode, bool generatedFrame)
            {
                int index = (int)(pass % _bucketPass.Length);
                if (_bucketPass[index] != pass)
                {
                    _bucketPass[index] = pass;
                    _presents[index] = 0;
                    _gpuBusyMs[index] = 0;
                }

                _presents[index]++;
                if (gpuBusyMs.HasValue)
                    _gpuBusyMs[index] += gpuBusyMs.Value;
                _lastPresentPass = pass;
                if (!string.IsNullOrEmpty(presentMode))
                    _presentMode = presentMode;
                if (generatedFrame)
                    _lastGeneratedFramePass = pass;
            }

            public CaptureProcessActivity ToActivity(string name, int processId, long pass, bool tracksGpuBusy)
            {
                // Only completed passes count: the current one is still filling.
                long completed = Math.Min(ActivityWindowSeconds, pass - _firstPass);
                int presents = 0;
                double gpuBusyMs = 0;
                for (int i = 0; i < _bucketPass.Length; i++)
                {
                    if (_bucketPass[i] >= pass - completed && _bucketPass[i] < pass)
                    {
                        presents += _presents[i];
                        gpuBusyMs += _gpuBusyMs[i];
                    }
                }

                // Several swap chains of one process can add up to more than the time measured.
                double? gpuBusyShare = completed > 0 && tracksGpuBusy
                    ? Math.Min(1.0, gpuBusyMs / (completed * 1000.0))
                    : (double?)null;
                double presentsPerSecond = completed > 0 ? presents / (double)completed : 0;
                int idleSeconds = (int)Math.Min(int.MaxValue, pass - _lastPresentPass);

                return new CaptureProcessActivity(name, processId, idleSeconds, presentsPerSecond,
                    gpuBusyShare, _presentMode, _lastGeneratedFramePass >= pass - ActivityWindowSeconds);
            }
        }
    }
}
